using System.Diagnostics;
using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Stage 5 (Phase 2) implementation of <see cref="IAdverseEventDenormalizationService"/>.
    /// Reads tmp_FlattenedStandardizedTable rows where TableCategory is ADVERSE_EVENT,
    /// classifies trial design per document/table, selects comparators per study group,
    /// computes RR/DNRR/CI values, and bulk-writes the denormalized result.
    /// </summary>
    /// <remarks>
    /// ## Failure Mode
    /// Unlike Stage 3 / Stage 4 which log-and-continue, this service is fail-fast: a
    /// partial denormalized table is more dangerous than a failed run. Any exception
    /// during a batch save propagates after clearing the change tracker so callers see
    /// the failure and can retry from a clean state.
    /// </remarks>
    /// <seealso cref="IAdverseEventDenormalizationService"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    /// <seealso cref="ComparatorGrouper"/>
    /// <seealso cref="AeStatEntityBuilder"/>
    public sealed class AdverseEventDenormalizationService : IAdverseEventDenormalizationService
    {
        #region Constants

        /**************************************************************/
        /// <summary>Source-table TableCategory value identifying AE rows.</summary>
        private const string AeCategory = "ADVERSE_EVENT";

        /**************************************************************/
        /// <summary>EF Core provider name returned by the InMemoryDatabase test provider.</summary>
        private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

        /**************************************************************/
        /// <summary>Target table used for both raw TRUNCATE and DbSet access.</summary>
        private const string TargetTable = "dbo.tmp_FlattenedAdverseEventTable";

        /**************************************************************/
        /// <summary>Coverage/audit table used to explain RR-ready and non-RR Stage 5 rows.</summary>
        private const string CoverageTargetTable = "dbo.tmp_FlattenedAdverseEventCoverageTable";

        /**************************************************************/
        /// <summary>Materialized risk table refreshed from <see cref="RiskSourceView"/>.</summary>
        private const string RiskTargetTable = "dbo.tmp_FlattenedAdverseEventRiskTable";

        /**************************************************************/
        /// <summary>Materialized AE dashboard product catalog refreshed from <see cref="CatalogSourceView"/>.</summary>
        private const string CatalogTargetTable = "dbo.tmp_AeDashboardProductCatalog";

        /**************************************************************/
        /// <summary>Source view for the materialized AE dashboard product catalog.</summary>
        private const string CatalogSourceView = "dbo.vw_AeDashboardProductCatalog";

        /**************************************************************/
        /// <summary>Source view for the materialized adverse-event risk table.</summary>
        private const string RiskSourceView = "dbo.vw_AeRisk";

        #endregion Constants

        #region Fields

        /**************************************************************/
        /// <summary>Database context for read (source) and bulk writes (target).</summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>Logger for stage progress, warnings, and errors.</summary>
        private readonly ILogger<AdverseEventDenormalizationService> _logger;

        /**************************************************************/
        /// <summary>Stage 5-only AE name/category standardizer.</summary>
        private readonly AeMeddraTermStandardizer _termStandardizer;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the service and disables EF Core change-tracker auto-detection
        /// for bulk-insert efficiency.
        /// </summary>
        /// <remarks>
        /// The service only bulk-inserts and never read-modify-writes tracked target
        /// entities. Each save clears the tracker so long denormalization runs do not
        /// accumulate entity state.
        /// </remarks>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="aeDictionary">Optional AE dictionary seed for Stage 5 standardization.</param>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="IAeParameterCategoryDictionaryService"/>
        public AdverseEventDenormalizationService(
            ApplicationDbContext dbContext,
            ILogger<AdverseEventDenormalizationService> logger,
            IAeParameterCategoryDictionaryService? aeDictionary = null)
        {
            #region implementation

            _dbContext = dbContext;
            _logger = logger;
            _termStandardizer = new AeMeddraTermStandardizer(aeDictionary);
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            #endregion
        }

        #endregion Constructor

        #region IAdverseEventDenormalizationService Implementation

        /// <inheritdoc/>
        public async Task<int> PopulateAsync(
            int batchSize = 5000,
            IProgress<DenormProgress>? progress = null,
            CancellationToken ct = default)
        {
            #region implementation

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Stage 5 — Phase 2 AdverseEvent denormalization starting (batchSize={BatchSize})",
                batchSize);

            await TruncateAsync(ct);

            var nullDocumentCoverageRows = await persistUngroupableSourceCoverageAsync(ct);
            if (nullDocumentCoverageRows > 0)
            {
                _logger.LogDebug(
                    "Stage 5 - Audited {Count} AE rows with NULL DocumentGUID in {CoverageTable}",
                    nullDocumentCoverageRows,
                    CoverageTargetTable);
            }

            var docIds = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory && r.DocumentGUID != null)
                .Select(r => r.DocumentGUID!.Value)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync(ct);

            if (docIds.Count == 0)
            {
                var riskRows = await materializeRiskTableAsync(ct);
                var catalogRows = await materializeProductCatalogAsync(ct);
                _logger.LogDebug(
                    "Stage 5 - {CatalogTable} refreshed with {Rows} rows",
                    CatalogTargetTable,
                    catalogRows);
                _logger.LogInformation(
                    "Stage 5 — No AE rows found; {RiskTable} refreshed with {Rows} rows",
                    RiskTargetTable,
                    riskRows);
                return 0;
            }

            _logger.LogInformation("Stage 5 — {DocCount} documents have AE rows", docIds.Count);

            int totalBatches = (int)Math.Ceiling((double)docIds.Count / batchSize);
            int totalRows = 0;
            int batchNumber = 0;

            for (int i = 0; i < docIds.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                batchNumber++;

                var batchDocs = docIds.Skip(i).Take(batchSize).ToList();
                var batchRows = await processBatchAsync(batchDocs, ct);
                totalRows += batchRows;

                _logger.LogInformation(
                    "Stage 5 — Batch {Batch}/{Total}: {Rows} rows, {Cumulative} cumulative",
                    batchNumber, totalBatches, batchRows, totalRows);

                progress?.Report(new DenormProgress
                {
                    BatchNumber = batchNumber,
                    TotalBatches = totalBatches,
                    BatchRowsWritten = batchRows,
                    CumulativeRowsWritten = totalRows,
                    Elapsed = stopwatch.Elapsed
                });
            }

            var riskRowCount = await materializeRiskTableAsync(ct);
            var catalogRowCount = await materializeProductCatalogAsync(ct);
            stopwatch.Stop();
            _logger.LogInformation(
                "Stage 5 — Phase 2 complete: {Rows} AE rows in {Batches} batches; {RiskRows} risk rows materialized ({Elapsed})",
                totalRows, batchNumber, riskRowCount, stopwatch.Elapsed);
            _logger.LogDebug(
                "Stage 5 - {CatalogTable} refreshed with {Rows} rows",
                CatalogTargetTable,
                catalogRowCount);

            return totalRows;

            #endregion
        }

        /// <inheritdoc/>
        public async Task TruncateAsync(CancellationToken ct = default)
        {
            #region implementation

            var providerName = _dbContext.Database.ProviderName ?? string.Empty;

            if (providerName.Equals(InMemoryProvider, StringComparison.OrdinalIgnoreCase))
            {
                var existingCatalogRows = _dbContext.Set<LabelView.AeDashboardProductCatalog>().ToList();
                var existingRiskRows = _dbContext.Set<LabelView.FlattenedAdverseEventRiskTable>().ToList();
                var existingCoverageRows = _dbContext.Set<LabelView.FlattenedAdverseEventCoverageTable>().ToList();
                var existingAeRows = _dbContext.Set<LabelView.FlattenedAdverseEventTable>().ToList();
                if (existingCatalogRows.Count > 0 || existingRiskRows.Count > 0 || existingCoverageRows.Count > 0 || existingAeRows.Count > 0)
                {
                    _dbContext.RemoveRange(existingCatalogRows);
                    _dbContext.RemoveRange(existingRiskRows);
                    _dbContext.RemoveRange(existingCoverageRows);
                    _dbContext.RemoveRange(existingAeRows);
                    await _dbContext.SaveChangesAsync(ct);
                    _dbContext.ChangeTracker.Clear();
                }
                return;
            }

            _logger.LogInformation("Stage 5 — Truncating {Table}", RiskTargetTable);
            _logger.LogInformation("Stage 5 - Truncating {Table}", CatalogTargetTable);
            await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {CatalogTargetTable}", ct);

            await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {RiskTargetTable}", ct);

            _logger.LogInformation("Stage 5 - Truncating {Table}", CoverageTargetTable);
            await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {CoverageTargetTable}", ct);

            _logger.LogInformation("Stage 5 — Truncating {Table}", TargetTable);
            await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {TargetTable}", ct);

            #endregion
        }

        #endregion IAdverseEventDenormalizationService Implementation

        #region Risk Materialization

        /**************************************************************/
        /// <summary>
        /// Materializes the final Stage 5 risk projection from <c>dbo.vw_AeRisk</c>.
        /// </summary>
        /// <remarks>
        /// The view remains the single SQL definition for joins and number-needed
        /// math. This method only snapshots the view output after the AE stats table
        /// has been populated. The EF Core InMemory provider skips the SQL-only view
        /// so service tests can continue exercising the C# denormalization path.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of risk rows inserted.</returns>
        /// <seealso cref="PopulateAsync"/>
        /// <seealso cref="TruncateAsync"/>
        private async Task<int> materializeRiskTableAsync(CancellationToken ct)
        {
            #region implementation

            var providerName = _dbContext.Database.ProviderName ?? string.Empty;
            if (providerName.Equals(InMemoryProvider, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Stage 5 — Skipping {RiskTable} materialization for EF InMemory provider",
                    RiskTargetTable);
                return 0;
            }

            _logger.LogInformation(
                "Stage 5 — Materializing {RiskTable} from {RiskSourceView}",
                RiskTargetTable,
                RiskSourceView);

            return await _dbContext.Database.ExecuteSqlRawAsync($"""
                INSERT INTO {RiskTargetTable} (
                    [DocumentGUID],
                    [tmp_FlattenedAdverseEventTableID],
                    [tmp_FlattenedStandardizedTableID],
                    [ActiveMoietyID],
                    [IngredientSubstanceID],
                    [PharmacologicClassID],
                    [ProductName],
                    [SubstanceName],
                    [PharmClassCode],
                    [PharmClassName],
                    [IsPlaceboControlled],
                    [ParameterName],
                    [ParameterCategory],
                    [Significance],
                    [NumberNeededType],
                    [ArmN],
                    [ComparatorN],
                    [EventsTreatment],
                    [EventsComparator],
                    [NumberNeeded],
                    [NumberNeededLowerBound],
                    [NumberNeededUpperBound],
                    [RR],
                    [RRLowerBound],
                    [RRUpperBound],
                    [LogRR],
                    [LogRRLowerBound],
                    [LogRRUpperBound],
                    [UNII],
                    [IsCombo],
                    [CalculationFlags],
                    [StudyContext],
                    [Population],
                    [Subpopulation],
                    [Dose],
                    [DoseUnit]
                )
                SELECT
                    [DocumentGUID],
                    [tmp_FlattenedAdverseEventTableID],
                    [tmp_FlattenedStandardizedTableID],
                    [ActiveMoietyID],
                    [IngredientSubstanceID],
                    [PharmacologicClassID],
                    [ProductName],
                    [SubstanceName],
                    [PharmClassCode],
                    [PharmClassName],
                    [IsPlaceboControlled],
                    [ParameterName],
                    [ParameterCategory],
                    [Significance],
                    [NumberNeededType],
                    [ArmN],
                    [ComparatorN],
                    [EventsTreatment],
                    [EventsComparator],
                    [NumberNeeded],
                    [NumberNeededLowerBound],
                    [NumberNeededUpperBound],
                    [RR],
                    [RRLowerBound],
                    [RRUpperBound],
                    [LogRR],
                    [LogRRLowerBound],
                    [LogRRUpperBound],
                    [UNII],
                    [IsCombo],
                    [CalculationFlags],
                    [StudyContext],
                    [Population],
                    [Subpopulation],
                    [Dose],
                    [DoseUnit]
                FROM {RiskSourceView};
                """, ct);

            #endregion
        }

        #endregion Risk Materialization

        #region Product Catalog Materialization

        /**************************************************************/
        /// <summary>
        /// Materializes the one-row-per-document AE dashboard product catalog.
        /// </summary>
        /// <remarks>
        /// The catalog is built from <see cref="CatalogSourceView"/> after the
        /// risk snapshot is refreshed. It persists the picker-ready aggregate,
        /// preferred ingredient/class JSON, sort keys, and searchable text so
        /// dashboard product search and paging remain provider-side.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of catalog rows inserted.</returns>
        /// <seealso cref="materializeRiskTableAsync"/>
        /// <seealso cref="TruncateAsync"/>
        private async Task<int> materializeProductCatalogAsync(CancellationToken ct)
        {
            #region implementation

            var providerName = _dbContext.Database.ProviderName ?? string.Empty;
            if (providerName.Equals(InMemoryProvider, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Stage 5 - Skipping {CatalogTable} materialization for EF InMemory provider",
                    CatalogTargetTable);
                return 0;
            }

            _logger.LogInformation(
                "Stage 5 - Materializing {CatalogTable} from {CatalogSourceView}",
                CatalogTargetTable,
                CatalogSourceView);

            return await _dbContext.Database.ExecuteSqlRawAsync($"""
                INSERT INTO {CatalogTargetTable} (
                    [DocumentGUID],
                    [ProductName],
                    [PrimarySubstanceName],
                    [PrimaryUNII],
                    [PrimaryPharmClassCode],
                    [PrimaryPharmClassName],
                    [ActiveIngredientsJson],
                    [ActiveMoietyID],
                    [IngredientSubstanceID],
                    [PharmacologicClassID],
                    [ArmN],
                    [ComparatorN],
                    [RowCount],
                    [SignificantCount],
                    [SignificantProtectiveCount],
                    [SignificantElevatedCount],
                    [PlaceboCoverage],
                    [ActiveCoverage],
                    [DoseCoverage],
                    [SocBreadth],
                    [SocTotal],
                    [MonoComboMix],
                    [Score],
                    [ScoreReason],
                    [SortSignificantElevatedCount],
                    [SortProductName],
                    [SearchText],
                    [RefreshedAt]
                )
                SELECT
                    [DocumentGUID],
                    [ProductName],
                    [PrimarySubstanceName],
                    [PrimaryUNII],
                    [PrimaryPharmClassCode],
                    [PrimaryPharmClassName],
                    [ActiveIngredientsJson],
                    [ActiveMoietyID],
                    [IngredientSubstanceID],
                    [PharmacologicClassID],
                    [ArmN],
                    [ComparatorN],
                    [RowCount],
                    [SignificantCount],
                    [SignificantProtectiveCount],
                    [SignificantElevatedCount],
                    [PlaceboCoverage],
                    [ActiveCoverage],
                    [DoseCoverage],
                    [SocBreadth],
                    [SocTotal],
                    [MonoComboMix],
                    [Score],
                    [ScoreReason],
                    [SortSignificantElevatedCount],
                    [SortProductName],
                    [SearchText],
                    SYSUTCDATETIME()
                FROM {CatalogSourceView};
                """, ct);

            #endregion
        }

        #endregion Product Catalog Materialization

        #region Batch Processing

        /**************************************************************/
        /// <summary>
        /// Loads, filters, groups, calculates, and writes AE rows for a batch of documents.
        /// </summary>
        /// <remarks>
        /// The service keeps EF orchestration and fail-fast writes here. Source-row
        /// eligibility, comparator grouping/selection, reference-dose selection, and
        /// output entity construction are delegated to Phase C collaborators.
        /// </remarks>
        /// <param name="docIds">Document GUIDs in this batch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of output rows written.</returns>
        /// <exception cref="OperationCanceledException">Cancellation observed.</exception>
        /// <seealso cref="SourceRowEligibility"/>
        /// <seealso cref="ComparatorGrouper"/>
        /// <seealso cref="ComparatorSelector"/>
        /// <seealso cref="AeStatEntityBuilder"/>
        private async Task<int> processBatchAsync(List<Guid> docIds, CancellationToken ct)
        {
            #region implementation

            var sourceRows = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory
                            && r.DocumentGUID != null
                            && docIds.Contains(r.DocumentGUID.Value))
                .ToListAsync(ct);

            var coverageRows = new List<LabelView.FlattenedAdverseEventCoverageTable>();
            var eligibleRows = new List<LabelView.FlattenedStandardizedTable>(sourceRows.Count);
            foreach (var sourceRow in sourceRows)
            {
                var exclusionReason = SourceRowEligibility.GetExclusionReason(sourceRow);
                if (exclusionReason is null)
                {
                    eligibleRows.Add(sourceRow);
                    continue;
                }

                coverageRows.Add(buildCoverageEntity(
                    sourceRow,
                    comparator: null,
                    status: exclusionReason,
                    exclusionReason: exclusionReason,
                    coverageFlags: exclusionReason));
            }

            var rows = eligibleRows;
            var skippedInvalidRows = coverageRows.Count;
            if (skippedInvalidRows > 0)
            {
                _logger.LogDebug(
                    "Stage 5 - Audited {Count} AE source rows with invalid arms or no analyzable value",
                    skippedInvalidRows);
            }

            if (rows.Count == 0)
            {
                await saveCoverageRowsAsync(coverageRows, ct);
                return 0;
            }

            var (standardizedRows, standardizationFlagsByRowId, skippedStandardizationRows, standardizationCoverageRows) =
                applyStage5Standardization(rows);
            coverageRows.AddRange(standardizationCoverageRows);
            if (skippedStandardizationRows > 0)
            {
                _logger.LogDebug(
                    "Stage 5 - Audited {Count} AE source rows during MedDRA/name standardization ({Reason})",
                    skippedStandardizationRows,
                    "AE_STD:EXCLUDED_NON_AE");
            }

            rows = standardizedRows;
            if (rows.Count == 0)
            {
                await saveCoverageRowsAsync(coverageRows, ct);
                return 0;
            }

            var entities = new List<LabelView.FlattenedAdverseEventTable>();
            var nullTableDesign = new RelativeRiskCalculator.TrialDesignClassification(
                false, RelativeRiskCalculator.TrialDesignKind.SINGLE_ARM, null);

            foreach (var docGroup in rows.GroupBy(r => r.DocumentGUID!.Value))
            {
                foreach (var tableGroup in docGroup.GroupBy(r => r.TextTableID))
                {
                    var tableRows = tableGroup.ToList();
                    var design = tableGroup.Key is null
                        ? nullTableDesign
                        : classifyTableDesign(tableRows);

                    foreach (var groupRows in ComparatorGrouper.Group(tableRows))
                    {
                        var stage5ArmNFlags = applySameArmNBackfill(groupRows);
                        var (comparator, comparatorFlag) = ComparatorSelector.Select(groupRows);
                        var (dRef, dRefUnit) = ComparatorSelector.SelectReferenceDose(groupRows);

                        foreach (var row in groupRows)
                        {
                            if (comparator is not null && ReferenceEquals(row, comparator))
                            {
                                coverageRows.Add(buildCoverageEntity(
                                    row,
                                    comparator,
                                    AeDenormalizationConstants.SelectedComparatorStatus,
                                    AeDenormalizationConstants.SelectedComparatorStatus,
                                    comparatorFlag));
                                continue;
                            }

                            var entity = AeStatEntityBuilder.Build(
                                row,
                                comparator,
                                comparatorFlag,
                                dRef,
                                dRefUnit,
                                design,
                                getStage5CalculationFlags(
                                    row,
                                    comparator,
                                    stage5ArmNFlags,
                                    standardizationFlagsByRowId));

                            entities.Add(entity);
                            var coverageStatus = getCoverageStatus(entity);
                            coverageRows.Add(buildCoverageEntity(
                                row,
                                comparator,
                                coverageStatus,
                                coverageStatus == AeDenormalizationConstants.RrReadyStatus ? null : coverageStatus,
                                entity.CalculationFlags,
                                entity));
                        }
                    }
                }
            }

            if (entities.Count == 0)
            {
                await saveCoverageRowsAsync(coverageRows, ct);
                return 0;
            }

            var persistableEntities = entities
                .Where(e => e.RR is not null)
                .ToList();

            logNullRrExclusions(entities);

            if (persistableEntities.Count == 0 && coverageRows.Count == 0)
                return 0;

            try
            {
                _dbContext.AddRange(coverageRows);
                _dbContext.AddRange(persistableEntities);
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
                return persistableEntities.Count;
            }
            catch (OperationCanceledException)
            {
                _dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception ex)
            {
                _dbContext.ChangeTracker.Clear();
                _logger.LogError(ex,
                    "Stage 5 — Batch save failed; aborting Phase 2 to avoid leaving a partial denormalized table");
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Persists coverage rows for AE source rows that cannot be grouped by document.
        /// </summary>
        /// <remarks>
        /// Rows with NULL <c>DocumentGUID</c> are outside the normal document-batched
        /// processing path, so they are audited immediately after Stage 5 truncation.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of coverage rows written.</returns>
        /// <seealso cref="SourceRowEligibility.GetExclusionReason"/>
        private async Task<int> persistUngroupableSourceCoverageAsync(CancellationToken ct)
        {
            #region implementation

            var rows = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory && r.DocumentGUID == null)
                .ToListAsync(ct);

            if (rows.Count == 0)
                return 0;

            var coverageRows = rows
                .Select(row => buildCoverageEntity(
                    row,
                    comparator: null,
                    status: AeDenormalizationConstants.NoDocumentGuidFlag,
                    exclusionReason: AeDenormalizationConstants.NoDocumentGuidFlag,
                    coverageFlags: AeDenormalizationConstants.NoDocumentGuidFlag))
                .ToList();

            await saveCoverageRowsAsync(coverageRows, ct);
            return coverageRows.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves Stage 5 coverage rows and clears EF tracking state.
        /// </summary>
        /// <param name="coverageRows">Coverage rows to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <seealso cref="LabelView.FlattenedAdverseEventCoverageTable"/>
        private async Task saveCoverageRowsAsync(
            IReadOnlyCollection<LabelView.FlattenedAdverseEventCoverageTable> coverageRows,
            CancellationToken ct)
        {
            #region implementation

            if (coverageRows.Count == 0)
                return;

            try
            {
                _dbContext.AddRange(coverageRows);
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }
            catch (OperationCanceledException)
            {
                _dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception ex)
            {
                _dbContext.ChangeTracker.Clear();
                _logger.LogError(ex,
                    "Stage 5 - Coverage save failed; aborting to avoid leaving an unexplained audit surface");
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one durable Stage 5 coverage/audit row.
        /// </summary>
        /// <param name="row">Source standardized row.</param>
        /// <param name="comparator">Selected comparator row, when available.</param>
        /// <param name="status">Coverage status to persist.</param>
        /// <param name="exclusionReason">Primary exclusion reason, when applicable.</param>
        /// <param name="coverageFlags">Semicolon-delimited coverage flags.</param>
        /// <param name="entity">Built AE stats entity, when the row reached calculation.</param>
        /// <returns>Coverage entity ready for persistence.</returns>
        /// <seealso cref="LabelView.FlattenedAdverseEventCoverageTable"/>
        private static LabelView.FlattenedAdverseEventCoverageTable buildCoverageEntity(
            LabelView.FlattenedStandardizedTable row,
            LabelView.FlattenedStandardizedTable? comparator,
            string status,
            string? exclusionReason,
            string? coverageFlags,
            LabelView.FlattenedAdverseEventTable? entity = null)
        {
            #region implementation

            return new LabelView.FlattenedAdverseEventCoverageTable
            {
                FlattenedStandardizedTableId = row.Id,
                TextTableID = row.TextTableID,
                DocumentGUID = row.DocumentGUID,
                UNII = row.UNII,
                ParameterName = entity?.ParameterName ?? row.ParameterName,
                ParameterCategory = entity?.ParameterCategory ?? row.ParameterCategory,
                TreatmentArm = row.TreatmentArm,
                ArmN = entity?.ArmN ?? row.ArmN,
                Dose = row.Dose,
                DoseUnit = row.DoseUnit,
                PrimaryValue = row.PrimaryValue,
                PrimaryValueType = row.PrimaryValueType,
                ComparatorArm = entity?.ComparatorArm ?? comparator?.TreatmentArm,
                ComparatorN = entity?.ComparatorN ?? comparator?.ArmN,
                ComparatorDose = comparator?.Dose,
                ComparatorDoseUnit = comparator?.DoseUnit,
                ComparatorPrimaryValue = comparator?.PrimaryValue,
                ComparatorPrimaryValueType = comparator?.PrimaryValueType,
                IsPlaceboControlled = entity?.IsPlaceboControlled ??
                                      (comparator is null
                                          ? null
                                          : RelativeRiskCalculator.IsPlaceboArm(comparator.TreatmentArm, comparator.Dose)),
                RR = entity?.RR,
                CoverageStatus = status,
                ExclusionReason = exclusionReason,
                CoverageFlags = coverageFlags,
                CalculationFlags = entity?.CalculationFlags,
                StudyContext = entity?.StudyContext ?? row.StudyContext,
                Population = entity?.Population ?? row.Population,
                Subpopulation = entity?.Subpopulation ?? row.Subpopulation
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the durable coverage status for a built AE statistics entity.
        /// </summary>
        /// <param name="entity">Built Stage 5 entity before RR filtering.</param>
        /// <returns><c>RR_READY</c> for persisted rows, otherwise the primary null-RR reason.</returns>
        /// <seealso cref="AeStatEntityBuilder"/>
        private static string getCoverageStatus(LabelView.FlattenedAdverseEventTable entity)
        {
            #region implementation

            if (entity.RR is not null)
                return AeDenormalizationConstants.RrReadyStatus;

            return getPrimaryNullRrReason(entity.CalculationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Selects the primary reason from semicolon-delimited calculation flags.
        /// </summary>
        /// <param name="calculationFlags">Raw CalculationFlags value.</param>
        /// <returns>Primary null-RR reason flag.</returns>
        private static string getPrimaryNullRrReason(string? calculationFlags)
        {
            #region implementation

            var flags = splitFlags(calculationFlags);
            var reasonPriority = new[]
            {
                AeDenormalizationConstants.SingleArmFlag,
                AeDenormalizationConstants.AmbiguousComparatorFlag,
                AeDenormalizationConstants.NoComparatorFlag,
                AeDenormalizationConstants.NoArmNFlag,
                AeDenormalizationConstants.NoComparatorNFlag,
                "MIXED_VALUE_TYPES",
                "UNCOMPARABLE_VALUE_TYPE",
                "INVALID_EVENT_COUNT",
                "EVENTS_EXCEED_ARMN",
                "PERCENT_OUT_OF_RANGE",
                AeDenormalizationConstants.ArmNRejectedConflictingNFlag
            };

            return reasonPriority.FirstOrDefault(reason => flags.Contains(reason, StringComparer.Ordinal)) ??
                   AeDenormalizationConstants.UnknownNullRrFlag;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Splits semicolon-delimited Stage 5 flags into a stable token list.
        /// </summary>
        /// <param name="flags">Raw flag text.</param>
        /// <returns>Distinct non-empty flag tokens.</returns>
        private static IReadOnlyList<string> splitFlags(string? flags)
        {
            #region implementation

            return (flags ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies Stage 5-only AE term/SOC standardization before comparator grouping.
        /// </summary>
        /// <remarks>
        /// Standardizing before <see cref="ComparatorGrouper"/> is required because
        /// the grouper keys on <see cref="LabelView.FlattenedStandardizedTable.ParameterName"/>.
        /// Rows are loaded <c>AsNoTracking()</c>, so mutations are batch-local and only
        /// affect the denormalized Stage 5 output.
        /// </remarks>
        /// <param name="rows">Eligible source rows.</param>
        /// <returns>Standardized rows, audit flags by row id, and skipped-row count.</returns>
        /// <seealso cref="AeMeddraTermStandardizer"/>
        /// <seealso cref="ComparatorGrouper"/>
        private (List<LabelView.FlattenedStandardizedTable> Rows,
                 Dictionary<int, HashSet<string>> FlagsByRowId,
                 int SkippedRows,
                 List<LabelView.FlattenedAdverseEventCoverageTable> CoverageRows) applyStage5Standardization(
            IReadOnlyList<LabelView.FlattenedStandardizedTable> rows)
        {
            #region implementation

            var standardizedRows = new List<LabelView.FlattenedStandardizedTable>(rows.Count);
            var flagsByRowId = new Dictionary<int, HashSet<string>>();
            var coverageRows = new List<LabelView.FlattenedAdverseEventCoverageTable>();
            var skippedRows = 0;

            foreach (var row in rows)
            {
                var result = _termStandardizer.Standardize(row);
                if (result.IsExcluded)
                {
                    skippedRows++;
                    var coverageFlags = result.Flags.Count == 0
                        ? AeDenormalizationConstants.StandardizerExcludedFlag
                        : string.Join(";", result.Flags);
                    coverageRows.Add(buildCoverageEntity(
                        row,
                        comparator: null,
                        status: AeDenormalizationConstants.StandardizerExcludedFlag,
                        exclusionReason: AeDenormalizationConstants.StandardizerExcludedFlag,
                        coverageFlags: coverageFlags));
                    continue;
                }

                if (result.Flags.Count > 0)
                    flagsByRowId[row.Id] = new HashSet<string>(result.Flags, StringComparer.Ordinal);

                standardizedRows.Add(row);
            }

            return (standardizedRows, flagsByRowId, skippedRows, coverageRows);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Backfills missing ArmN values from a unique same-arm N inside one comparator group.
        /// </summary>
        /// <remarks>
        /// This is an in-memory safety net only. Source rows are loaded with
        /// <c>AsNoTracking()</c>, so the backfill affects only the denormalized Stage 5
        /// entities created from this batch.
        /// </remarks>
        /// <param name="groupRows">Rows in a single comparator cohort.</param>
        /// <returns>Calculation flags by source row id.</returns>
        /// <seealso cref="ComparatorGrouper"/>
        /// <seealso cref="AeStatEntityBuilder"/>
        private static Dictionary<int, HashSet<string>> applySameArmNBackfill(
            IReadOnlyList<LabelView.FlattenedStandardizedTable> groupRows)
        {
            #region implementation

            var flagsByRowId = new Dictionary<int, HashSet<string>>();

            foreach (var armGroup in groupRows
                         .Where(r => !string.IsNullOrWhiteSpace(r.TreatmentArm))
                         .GroupBy(r => ComparatorGrouper.NormalizeKey(r.TreatmentArm)))
            {
                var positiveNs = armGroup
                    .Where(r => r.ArmN is > 0)
                    .Select(r => r.ArmN!.Value)
                    .Distinct()
                    .ToList();

                var missingRows = armGroup
                    .Where(r => r.ArmN is null || r.ArmN <= 0)
                    .ToList();

                if (missingRows.Count == 0)
                    continue;

                if (positiveNs.Count == 1)
                {
                    foreach (var row in missingRows)
                    {
                        row.ArmN = positiveNs[0];
                        addStage5ArmNFlag(
                            flagsByRowId,
                            row.Id,
                            AeDenormalizationConstants.ArmNStage5GroupBackfillFlag);
                    }
                    continue;
                }

                if (positiveNs.Count > 1)
                {
                    foreach (var row in armGroup)
                    {
                        addStage5ArmNFlag(
                            flagsByRowId,
                            row.Id,
                            AeDenormalizationConstants.ArmNRejectedConflictingNFlag);
                    }
                }
            }

            return flagsByRowId;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds one Stage 5 ArmN flag to the per-row flag dictionary.
        /// </summary>
        /// <param name="flagsByRowId">Mutable flag dictionary.</param>
        /// <param name="rowId">Source row identifier.</param>
        /// <param name="flag">Flag to add.</param>
        /// <seealso cref="applySameArmNBackfill"/>
        private static void addStage5ArmNFlag(
            Dictionary<int, HashSet<string>> flagsByRowId,
            int rowId,
            string flag)
        {
            #region implementation

            if (!flagsByRowId.TryGetValue(rowId, out var flags))
            {
                flags = new HashSet<string>(StringComparer.Ordinal);
                flagsByRowId[rowId] = flags;
            }

            flags.Add(flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collects row and comparator ArmN flags for the output entity.
        /// </summary>
        /// <param name="row">Current treatment row.</param>
        /// <param name="comparator">Selected comparator row.</param>
        /// <param name="flagsByRowId">Backfill/conflict flags by source row id.</param>
        /// <returns>Distinct calculation flags to append.</returns>
        /// <seealso cref="AeStatEntityBuilder.Build"/>
        private static IReadOnlyList<string> getStage5ArmNFlags(
            LabelView.FlattenedStandardizedTable row,
            LabelView.FlattenedStandardizedTable? comparator,
            IReadOnlyDictionary<int, HashSet<string>> flagsByRowId)
        {
            #region implementation

            var flags = new List<string>();
            if (flagsByRowId.TryGetValue(row.Id, out var rowFlags))
                flags.AddRange(rowFlags);
            if (comparator is not null && flagsByRowId.TryGetValue(comparator.Id, out var comparatorFlags))
                flags.AddRange(comparatorFlags);

            return flags
                .Distinct(StringComparer.Ordinal)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collects row, comparator, and MedDRA standardization flags for output.
        /// </summary>
        /// <param name="row">Current treatment row.</param>
        /// <param name="comparator">Selected comparator row.</param>
        /// <param name="armNFlagsByRowId">Backfill/conflict flags by source row id.</param>
        /// <param name="standardizationFlagsByRowId">Name/category standardization flags by source row id.</param>
        /// <returns>Distinct calculation flags to append.</returns>
        /// <seealso cref="AeStatEntityBuilder.Build"/>
        private static IReadOnlyList<string> getStage5CalculationFlags(
            LabelView.FlattenedStandardizedTable row,
            LabelView.FlattenedStandardizedTable? comparator,
            IReadOnlyDictionary<int, HashSet<string>> armNFlagsByRowId,
            IReadOnlyDictionary<int, HashSet<string>> standardizationFlagsByRowId)
        {
            #region implementation

            var flags = new List<string>();
            flags.AddRange(getStage5ArmNFlags(row, comparator, armNFlagsByRowId));

            if (standardizationFlagsByRowId.TryGetValue(row.Id, out var rowStandardizationFlags))
                flags.AddRange(rowStandardizationFlags);
            if (comparator is not null &&
                standardizationFlagsByRowId.TryGetValue(comparator.Id, out var comparatorStandardizationFlags))
            {
                flags.AddRange(comparatorStandardizationFlags);
            }

            return flags
                .Distinct(StringComparer.Ordinal)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies trial design for one non-null TextTableID group.
        /// </summary>
        /// <remarks>
        /// Trial-design classification is diagnostic only. It can append an ambiguous
        /// design flag through <see cref="AeStatEntityBuilder"/>, but it never drives
        /// the persisted row-level IsPlaceboControlled bit.
        /// </remarks>
        /// <param name="tableRows">Rows in one document/table group.</param>
        /// <returns>Trial design classification for diagnostics.</returns>
        /// <seealso cref="RelativeRiskCalculator"/>
        private static RelativeRiskCalculator.TrialDesignClassification classifyTableDesign(
            IEnumerable<LabelView.FlattenedStandardizedTable> tableRows)
        {
            #region implementation

            var distinctArms = tableRows
                .GroupBy(r => new
                {
                    Name = r.TreatmentArm,
                    Dose = r.Dose,
                    Unit = r.DoseUnit
                })
                .Select(g => new RelativeRiskCalculator.ArmInfo(g.Key.Name, g.Key.Dose, g.Key.Unit))
                .ToList();

            return RelativeRiskCalculator.ClassifyTrialDesign(distinctArms);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs rows that were calculated but excluded from persistence because RR is null.
        /// </summary>
        /// <param name="entities">Built Stage 5 entities before RR filtering.</param>
        /// <seealso cref="AeStatEntityBuilder"/>
        private void logNullRrExclusions(IReadOnlyList<LabelView.FlattenedAdverseEventTable> entities)
        {
            #region implementation

            var nullRrEntities = entities
                .Where(e => e.RR is null)
                .ToList();

            if (nullRrEntities.Count == 0)
                return;

            _logger.LogDebug(
                "Stage 5 - Skipping {Count} AE output rows with NULL RR ({ReasonSummary})",
                nullRrEntities.Count,
                summarizeReasonFamilies(nullRrEntities));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Summarizes null-RR exclusion reason families for structured batch logging.
        /// </summary>
        /// <param name="entities">Null-RR entities.</param>
        /// <returns>Comma-delimited family counts.</returns>
        private static string summarizeReasonFamilies(IEnumerable<LabelView.FlattenedAdverseEventTable> entities)
        {
            #region implementation

            var reasonFamilies = new[]
            {
                AeDenormalizationConstants.NoArmNFlag,
                AeDenormalizationConstants.NoComparatorNFlag,
                AeDenormalizationConstants.SingleArmFlag,
                AeDenormalizationConstants.AmbiguousComparatorFlag,
                AeDenormalizationConstants.NoComparatorFlag,
                "MIXED_VALUE_TYPES",
                "UNCOMPARABLE_VALUE_TYPE",
                "INVALID_EVENT_COUNT",
                "EVENTS_EXCEED_ARMN",
                "PERCENT_OUT_OF_RANGE",
                AeDenormalizationConstants.ArmNRejectedConflictingNFlag
            };

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                var flags = (entity.CalculationFlags ?? string.Empty)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var family = reasonFamilies.FirstOrDefault(reason => flags.Contains(reason, StringComparer.Ordinal)) ??
                             "UNKNOWN_NULL_RR";
                counts[family] = counts.TryGetValue(family, out var count) ? count + 1 : 1;
            }

            return string.Join(
                ", ",
                counts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}"));

            #endregion
        }

        #endregion Batch Processing
    }
}
