using System.Diagnostics;
using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Stage 5 (Phase 2) implementation of <see cref="IAdverseEventDenormalizationService"/>.
    /// Reads <c>tmp_FlattenedStandardizedTable</c> rows where
    /// <c>TableCategory = 'ADVERSE_EVENT'</c>, classifies trial design per Document,
    /// selects comparators per <c>(DocumentGUID, TextTableID, ParameterName,
    /// ParameterSubtype)</c> study group, computes RR/DNRR/CI via
    /// <see cref="RelativeRiskCalculator"/>, and bulk-writes the result.
    /// </summary>
    /// <remarks>
    /// ## Failure Mode
    /// Unlike Stage 3 / Stage 4 which log-and-continue, this service is **fail-fast**: a
    /// partial denormalized table is more dangerous than a failed run. Any exception
    /// during a batch save propagates after clearing the change tracker so callers see
    /// the failure and can retry from a clean state.
    /// </remarks>
    /// <seealso cref="IAdverseEventDenormalizationService"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    public sealed class AdverseEventDenormalizationService : IAdverseEventDenormalizationService
    {
        #region Constants

        /**************************************************************/
        /// <summary>Source-table TableCategory value identifying AE rows.</summary>
        private const string AeCategory = "ADVERSE_EVENT";

        /**************************************************************/
        /// <summary>Statistical method label persisted in <c>CalculationMethod</c>.</summary>
        private const string KatzLogMethod = "KATZ_LOG";

        /**************************************************************/
        /// <summary>
        /// Comparator-kind flag emitted into <c>CalculationFlags</c> when the chosen
        /// comparator was a placebo arm (matches placebo|sham|vehicle, or has Dose=0).
        /// Also drives the persisted <c>IsPlaceboControlled</c> bit one-for-one.
        /// </summary>
        private const string PlaceboComparatorFlag = "PLACEBO_COMPARATOR";

        /**************************************************************/
        /// <summary>EF Core provider name returned by the InMemoryDatabase test provider.</summary>
        private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

        /**************************************************************/
        /// <summary>Target table — used for both raw TRUNCATE and DbSet access.</summary>
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
        /// for bulk-insert efficiency. Paired with explicit <c>ChangeTracker.Clear()</c>
        /// after every <c>SaveChangesAsync</c> so the tracker never grows.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger.</param>
        public AdverseEventDenormalizationService(
            ApplicationDbContext dbContext,
            ILogger<AdverseEventDenormalizationService> logger)
        {
            #region implementation

            _dbContext = dbContext;
            _logger = logger;

            // Bulk-insert only — no read-modify-write — so change detection is overhead.
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

            // Inventory diagnostics: count NULL-DocumentGUID AE rows so the operator knows
            // how many source rows are being silently skipped by the (DocumentGUID required)
            // group key.
            var nullDocCount = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory && r.DocumentGUID == null)
                .CountAsync(ct);

            if (nullDocCount > 0)
            {
                _logger.LogWarning(
                    "Stage 5 — Skipping {Count} AE rows with NULL DocumentGUID (cannot be safely grouped)",
                    nullDocCount);
            }

            // Discover documents with at least one AE row
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
                // The InMemoryDatabase provider does not support raw SQL — fall back to a
                // DbSet-level RemoveRange + SaveChanges so unit tests can rerun cleanly.
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
        /// Normalizes a nullable string for use as a comparator group key. Trims, collapses
        /// internal whitespace, folds case via <see cref="string.ToUpperInvariant"/>, and
        /// returns <see cref="string.Empty"/> for null / whitespace-only inputs so they
        /// share a single group bucket.
        /// </summary>
        /// <remarks>
        /// Anonymous-type <c>GroupBy</c> is case-sensitive by default; without case folding,
        /// trailing-whitespace or case variants ("Adults" vs "adults ") would split a valid
        /// comparator group and produce incorrect RR pairings.
        /// </remarks>
        /// <param name="s">Raw string value (typically from a nullable column).</param>
        /// <returns>Normalized key suitable for grouping.</returns>
        private static string normalizeKey(string? s)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;
            var collapsed = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
            return collapsed.ToUpperInvariant();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads AE rows for a batch of documents, classifies trial design per
        /// (DocumentGUID, TextTableID) for diagnostic purposes, selects comparators per
        /// study group, computes statistics for non-comparator rows, and bulk-writes
        /// the resulting entities. The persisted <c>IsPlaceboControlled</c> bit is set
        /// per-row from the comparator selection (see <see cref="buildEntity"/>);
        /// the trial-design classifier feeds only the <c>AMBIGUOUS_TRIAL_DESIGN</c>
        /// diagnostic flag in <c>CalculationFlags</c>. Fail-fast on save errors.
        /// </summary>
        /// <param name="docIds">Document GUIDs in this batch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of output rows written.</returns>
        /// <exception cref="OperationCanceledException">Cancellation observed.</exception>
        private async Task<int> processBatchAsync(List<Guid> docIds, CancellationToken ct)
        {
            #region implementation

            var rows = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .Where(r => r.TableCategory == AeCategory
                            && r.DocumentGUID != null
                            && docIds.Contains(r.DocumentGUID.Value))
                .ToListAsync(ct);

            if (rows.Count == 0)
                return 0;

            var entities = new List<LabelView.FlattenedAdverseEventTable>();

            // Default classification for rows with NULL TextTableID — keeps null-table rows
            // from being silently merged into a single doc-wide arm set, which would re-create
            // the original coarse-grouping bug for the diagnostic flag. The bit itself is
            // unaffected (it's comparator-driven below).
            var nullTableDesign = new RelativeRiskCalculator.TrialDesignClassification(
                false, RelativeRiskCalculator.TrialDesignKind.SINGLE_ARM, null);

            foreach (var docGroup in rows.GroupBy(r => r.DocumentGUID!.Value))
            {
                // Trial-design classification is diagnostic only (drives AMBIGUOUS_TRIAL_DESIGN
                // flag in CalculationFlags). Computed per (DocumentGUID, TextTableID) so the
                // diagnostic stays scoped to the row's source table — a single Document can
                // carry multiple sub-trials with different comparator structures, and a
                // doc-wide classification would contaminate the diagnostic across them.
                // The IsPlaceboControlled bit is comparator-driven, not design-driven
                // (see buildEntity).
                foreach (var tableGroup in docGroup.GroupBy(r => r.TextTableID))
                {
                    var tableRows = tableGroup.ToList();

                    RelativeRiskCalculator.TrialDesignClassification design;
                    if (tableGroup.Key is null)
                    {
                        design = nullTableDesign;
                    }
                    else
                    {
                        var distinctArms = tableRows
                            .GroupBy(r => new
                            {
                                Name = r.TreatmentArm,
                                Dose = r.Dose,
                                Unit = r.DoseUnit
                            })
                            .Select(g => new RelativeRiskCalculator.ArmInfo(g.Key.Name, g.Key.Dose, g.Key.Unit))
                            .ToList();

                        design = RelativeRiskCalculator.ClassifyTrialDesign(distinctArms);
                    }

                    // Comparator pairing per (TextTableID, ParameterName, ParameterSubtype,
                    // StudyContext, Population, Subpopulation) — TextTableID prevents cross-study
                    // leakage when a single document carries the same AE term across multiple
                    // study tables; StudyContext / Population / Subpopulation prevent
                    // cross-population pairing (e.g., Adults vs Children, Female-only vs Male-only).
                    // String keys are normalized (trim + collapse whitespace + ToUpperInvariant)
                    // so casing/whitespace variants don't fragment a valid comparator group.
                    foreach (var grp in tableRows.GroupBy(r => new
                    {
                        ParameterName    = normalizeKey(r.ParameterName),
                        ParameterSubtype = normalizeKey(r.ParameterSubtype),
                        StudyContext     = normalizeKey(r.StudyContext),
                        Population       = normalizeKey(r.Population),
                        Subpopulation    = normalizeKey(r.Subpopulation),
                    }))
                    {
                        var groupRows = grp.ToList();
                        var (comparator, comparatorFlag) = selectComparator(groupRows);

                        // D_ref = MIN(Dose) WHERE Dose > 0 over the study group. Includes the
                        // comparator row's dose by design (matches user spec). Used only for
                        // DNRR denominator.
                        var dosedRows = groupRows
                            .Where(r => r.Dose != null && r.Dose > 0m)
                            .ToList();
                        decimal? dRef = dosedRows.Count > 0 ? dosedRows.Min(r => r.Dose) : null;
                        string? dRefUnit = dRef is null
                            ? null
                            : dosedRows.First(r => r.Dose == dRef).DoseUnit;

                        foreach (var row in groupRows)
                        {
                            // No self-comparison: the chosen comparator row is excluded from output.
                            if (comparator is not null && ReferenceEquals(row, comparator))
                                continue;

                            entities.Add(buildEntity(row, comparator, comparatorFlag, dRef, dRefUnit, design));
                        }
                    }
                }
            }

            if (entities.Count == 0)
                return 0;

            // Fail-fast write: any save exception aborts Phase 2 (post-truncate state is
            // acceptable since reruns are idempotent).
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

        #endregion Batch Processing

        #region Comparator Selection

        /**************************************************************/
        /// <summary>
        /// Three-tier comparator cascade with deterministic tie-breakers (Dose nulls-first,
        /// then SourceRowSeq, SourceCellSeq, source Id) so reruns are byte-identical.
        /// </summary>
        /// <param name="groupRows">Rows in one (TextTableID, ParameterName, ParameterSubtype) group.</param>
        /// <returns>
        /// Tuple of <c>(comparator, flag)</c>. The flag is the comparator-kind diagnostic
        /// emitted in <c>CalculationFlags</c>: <c>PLACEBO_COMPARATOR</c>,
        /// <c>LOW_DOSE_COMPARATOR</c>, or <c>NO_COMPARATOR</c>. <c>ACTIVE_COMPARATOR</c>
        /// is reserved for a future phase that has arm-level UNII.
        /// </returns>
        private static (LabelView.FlattenedStandardizedTable? comparator, string flag)
            selectComparator(List<LabelView.FlattenedStandardizedTable> groupRows)
        {
            #region implementation

            // Tier 1: placebo arm — `placebo`/`sham`/`vehicle` OR Dose == 0
            var placeboCandidates = groupRows
                .Where(r => RelativeRiskCalculator.IsPlaceboArm(r.TreatmentArm, r.Dose))
                .OrderBy(r => r.Dose ?? decimal.MinValue) // nulls first
                .ThenBy(r => r.SourceRowSeq ?? int.MaxValue)
                .ThenBy(r => r.SourceCellSeq ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            if (placeboCandidates.Count > 0)
                return (placeboCandidates[0], PlaceboComparatorFlag);

            // Tier 2: lowest non-zero dose. Requires the group to have at least one
            // additional row (otherwise selecting the only dosed row leaves no rows to
            // emit, which is equivalent to single-arm).
            var dosedCandidates = groupRows
                .Where(r => r.Dose != null && r.Dose > 0m)
                .OrderBy(r => r.Dose)
                .ThenBy(r => r.SourceRowSeq ?? int.MaxValue)
                .ThenBy(r => r.SourceCellSeq ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            if (dosedCandidates.Count > 0 && groupRows.Count > 1)
                return (dosedCandidates[0], "LOW_DOSE_COMPARATOR");

            // Tier 3: single-arm fallback
            return (null, "NO_COMPARATOR");

            #endregion
        }

        #endregion Comparator Selection

        #region Entity Construction

        /**************************************************************/
        /// <summary>
        /// Builds one output entity for a non-comparator source row, populating
        /// source-projected columns verbatim, comparator metadata from the chosen
        /// comparator, derived event counts, RR/CI, DNRR/CI, and the semicolon-delimited
        /// flag accumulator. Comparator-kind flag is always emitted first.
        /// </summary>
        /// <remarks>
        /// <para>The persisted <c>IsPlaceboControlled</c> bit is set strictly per-row from
        /// <paramref name="comparatorFlag"/>: <c>true</c> iff the chosen comparator was a
        /// placebo arm (<c>PLACEBO_COMPARATOR</c>). The <paramref name="design"/> argument
        /// is diagnostic-only — it can append <c>AMBIGUOUS_TRIAL_DESIGN</c> to
        /// <c>CalculationFlags</c> but never drives the bit. This intentional decoupling
        /// answers "is this row's comparison placebo-controlled?" rather than the older
        /// document-level "is this trial pure placebo-vs-drug?" question.</para>
        /// </remarks>
        /// <param name="row">The non-comparator source row to project.</param>
        /// <param name="comparator">Comparator row chosen for this group, or null when no comparator was selectable.</param>
        /// <param name="comparatorFlag">Comparator-kind flag (<c>PLACEBO_COMPARATOR</c>, <c>LOW_DOSE_COMPARATOR</c>, or <c>NO_COMPARATOR</c>). Drives <c>IsPlaceboControlled</c>.</param>
        /// <param name="dRef">Group D_ref (MIN(Dose) WHERE Dose &gt; 0).</param>
        /// <param name="dRefUnit">Dose unit at D_ref.</param>
        /// <param name="design">Per-table trial-design classification (diagnostic only — emits <c>AMBIGUOUS_TRIAL_DESIGN</c> into <c>CalculationFlags</c>).</param>
        /// <returns>Entity ready for AddRange + SaveChangesAsync.</returns>
        private static LabelView.FlattenedAdverseEventTable buildEntity(
            LabelView.FlattenedStandardizedTable row,
            LabelView.FlattenedStandardizedTable? comparator,
            string comparatorFlag,
            decimal? dRef,
            string? dRefUnit,
            RelativeRiskCalculator.TrialDesignClassification design)
        {
            #region implementation

            // Source projection — copied verbatim, never derived
            var entity = new LabelView.FlattenedAdverseEventTable
            {
                FlattenedStandardizedTableId = row.Id,
                DocumentGUID = row.DocumentGUID,
                UNII = row.UNII,
                ParameterName = row.ParameterName,
                ParameterCategory = row.ParameterCategory,
                ArmN = row.ArmN,
                Dose = row.Dose,
                DoseUnit = row.DoseUnit,
                PrimaryValue = row.PrimaryValue,
                PrimaryValueType = row.PrimaryValueType,
                StudyContext = row.StudyContext,
                Population = row.Population,
                Subpopulation = row.Subpopulation,
                TreatmentArm = row.TreatmentArm,
                ComparatorArm = comparator?.TreatmentArm,
                ComparatorN = comparator?.ArmN,
                // Strictly row-level: the bit is on iff THIS row's comparator was a placebo
                // arm. The trial-design classifier no longer drives the bit (its result
                // surfaces only as the AMBIGUOUS_TRIAL_DESIGN diagnostic flag below).
                IsPlaceboControlled = string.Equals(comparatorFlag, PlaceboComparatorFlag, StringComparison.Ordinal)
            };

            var flags = new List<string> { comparatorFlag };
            if (design.Flag is not null)
                flags.Add(design.Flag);

            // No comparator → stats stay null
            if (comparator is null)
            {
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            var rowPvt = row.PrimaryValueType?.Trim();
            var compPvt = comparator.PrimaryValueType?.Trim();

            // Type-mismatch check
            if (!string.Equals(rowPvt, compPvt, StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("MIXED_VALUE_TYPES");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            // Same type, but not a comparable type
            bool isPercentage = string.Equals(rowPvt, "Percentage", StringComparison.OrdinalIgnoreCase);
            bool isCount = string.Equals(rowPvt, "Count", StringComparison.OrdinalIgnoreCase);
            if (!isPercentage && !isCount)
            {
                flags.Add("UNCOMPARABLE_VALUE_TYPE");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            // PrimaryValue guards
            if (row.PrimaryValue is null || comparator.PrimaryValue is null)
            {
                flags.Add("INVALID_EVENT_COUNT");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (row.PrimaryValue < 0d || comparator.PrimaryValue < 0d)
            {
                flags.Add("INVALID_EVENT_COUNT");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (isPercentage && (row.PrimaryValue > 100d || comparator.PrimaryValue > 100d))
            {
                flags.Add("PERCENT_OUT_OF_RANGE");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            // Percentage path with missing armN: compute RR-only, no CI, no DNRR.
            // Per user direction: "If there is no ArmN then the CI's must be null."
            // For percentages this is still a meaningful point estimate (RR = pt / pc).
            bool armNMissing = row.ArmN is null || row.ArmN <= 0
                            || comparator.ArmN is null || comparator.ArmN <= 0;

            if (isPercentage && armNMissing)
            {
                if (comparator.PrimaryValue > 0d)
                {
                    entity.RR = row.PrimaryValue.Value / comparator.PrimaryValue.Value;
                    entity.CalculationMethod = KatzLogMethod;
                }
                flags.Add("NO_ARMN");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            // Standard path: derive event counts, run Katz, compute DNRR
            var (rowEvents, rowEventFlag) = RelativeRiskCalculator.DeriveEventCount(
                row.PrimaryValue, rowPvt, row.ArmN);
            var (compEvents, compEventFlag) = RelativeRiskCalculator.DeriveEventCount(
                comparator.PrimaryValue, compPvt, comparator.ArmN);

            entity.EventsTreatment = rowEvents;
            entity.EventsComparator = compEvents;

            if (rowEvents is null || compEvents is null)
            {
                // Defensive: should not happen for the Percentage+armN or Count paths
                // because the guards above filter first. Surface whichever flag came back.
                var flag = rowEventFlag ?? compEventFlag ?? "INVALID_EVENT_COUNT";
                flags.Add(flag);
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            var rrResult = RelativeRiskCalculator.Compute(
                rowEvents, row.ArmN, compEvents, comparator.ArmN);

            entity.RR = rrResult.Rr;
            entity.RRLowerBound = rrResult.RrLower;
            entity.RRUpperBound = rrResult.RrUpper;

            if (rrResult.Flags is not null)
                flags.Add(rrResult.Flags);

            // DNRR computed only when RR was computable
            if (rrResult.Rr is not null)
            {
                var dnrrResult = RelativeRiskCalculator.ComputeDnrr(
                    rrResult, row.Dose, row.DoseUnit, dRef, dRefUnit);
                entity.DNRR = dnrrResult.Dnrr;
                entity.DNRRLowerBound = dnrrResult.DnrrLower;
                entity.DNRRUpperBound = dnrrResult.DnrrUpper;

                if (dnrrResult.Flags is not null)
                    flags.Add(dnrrResult.Flags);
            }

            entity.CalculationMethod = rrResult.Rr is not null ? KatzLogMethod : null;
            entity.CalculationFlags = string.Join(";", flags);

            return entity;

            #endregion
        }

        #endregion Entity Construction
    }
}
