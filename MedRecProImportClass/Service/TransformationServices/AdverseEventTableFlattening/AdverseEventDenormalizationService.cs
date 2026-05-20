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
        private const string TargetTable = "tmp_FlattenedAdverseEventTable";

        #endregion Constants

        #region Fields

        /**************************************************************/
        /// <summary>Database context for read (source) and bulk writes (target).</summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>Logger for stage progress, warnings, and errors.</summary>
        private readonly ILogger<AdverseEventDenormalizationService> _logger;

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
        /// <seealso cref="ApplicationDbContext"/>
        public AdverseEventDenormalizationService(
            ApplicationDbContext dbContext,
            ILogger<AdverseEventDenormalizationService> logger)
        {
            #region implementation

            _dbContext = dbContext;
            _logger = logger;
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

            var nullDocCount = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory && r.DocumentGUID == null)
                .CountAsync(ct);

            if (nullDocCount > 0)
            {
                _logger.LogDebug(
                    "Stage 5 — Skipping {Count} AE rows with NULL DocumentGUID (cannot be safely grouped)",
                    nullDocCount);
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
                _logger.LogInformation("Stage 5 — No AE rows found, exiting");
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

            stopwatch.Stop();
            _logger.LogInformation(
                "Stage 5 — Phase 2 complete: {Rows} rows in {Batches} batches ({Elapsed})",
                totalRows, batchNumber, stopwatch.Elapsed);

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
                var existing = _dbContext.Set<LabelView.FlattenedAdverseEventTable>().ToList();
                if (existing.Count > 0)
                {
                    _dbContext.RemoveRange(existing);
                    await _dbContext.SaveChangesAsync(ct);
                    _dbContext.ChangeTracker.Clear();
                }
                return;
            }

            _logger.LogInformation("Stage 5 — Truncating {Table}", TargetTable);
            await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {TargetTable}", ct);

            #endregion
        }

        #endregion IAdverseEventDenormalizationService Implementation

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

            var rows = sourceRows
                .Where(SourceRowEligibility.IsDenormalizableAeSourceRow)
                .ToList();
            var skippedInvalidRows = sourceRows.Count - rows.Count;
            if (skippedInvalidRows > 0)
            {
                _logger.LogDebug(
                    "Stage 5 - Skipping {Count} AE source rows with invalid arms or no analyzable value",
                    skippedInvalidRows);
            }

            if (rows.Count == 0)
                return 0;

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
                                continue;

                            entities.Add(AeStatEntityBuilder.Build(
                                row,
                                comparator,
                                comparatorFlag,
                                dRef,
                                dRefUnit,
                                design,
                                getStage5ArmNFlags(row, comparator, stage5ArmNFlags)));
                        }
                    }
                }
            }

            if (entities.Count == 0)
                return 0;

            try
            {
                _dbContext.AddRange(entities);
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
                return entities.Count;
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

        #endregion Batch Processing
    }
}
