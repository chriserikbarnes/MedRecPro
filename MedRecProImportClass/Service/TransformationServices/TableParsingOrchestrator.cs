using System.Diagnostics;
using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Orchestrates Stage 3 of the SPL Table Normalization pipeline. Reconstructs tables
    /// via Stage 2, routes to parsers, collects observations, and bulk-writes to
    /// tmp_FlattenedStandardizedTable.
    /// </summary>
    /// <remarks>
    /// ## Batch Processing
    /// Uses <see cref="ITableCellContextService.GetTextTableIdRangeAsync"/> to discover
    /// ID bounds, then iterates in configurable batch sizes via
    /// <see cref="TableCellContextFilter.TextTableIdRangeStart"/>/<see cref="TableCellContextFilter.TextTableIdRangeEnd"/>.
    ///
    /// ## Entity Mapping
    /// ParsedObservation DTOs are mapped to <see cref="LabelView.FlattenedStandardizedTable"/>
    /// entities before bulk insert via AddRange + SaveChangesAsync.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    /// <seealso cref="ITableReconstructionService"/>
    /// <seealso cref="ITableParserRouter"/>
    public class TableParsingOrchestrator : ITableParsingOrchestrator
    {
        #region Fields

        /**************************************************************/
        /// <summary>Stage 2 reconstruction service.</summary>
        private readonly ITableReconstructionService _reconstructionService;

        /**************************************************************/
        /// <summary>Stage 1 cell context service for ID range discovery.</summary>
        private readonly ITableCellContextService _cellContextService;

        /**************************************************************/
        /// <summary>Parser router for category determination and parser selection.</summary>
        private readonly ITableParserRouter _router;

        /**************************************************************/
        /// <summary>Database context for bulk writes.</summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>Logger for progress and diagnostics.</summary>
        private readonly ILogger<TableParsingOrchestrator> _logger;

        /**************************************************************/
        /// <summary>
        /// Optional Stage 4 batch validation service. Null if validation is not configured.
        /// </summary>
        private readonly IBatchValidationService? _batchValidator;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the orchestrator with all required dependencies.
        /// </summary>
        /// <param name="reconstructionService">Stage 2 reconstruction service.</param>
        /// <param name="cellContextService">Stage 1 cell context service.</param>
        /// <param name="router">Parser router.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="batchValidator">Optional Stage 4 batch validation service. Pass null to skip validation.</param>
        public TableParsingOrchestrator(
            ITableReconstructionService reconstructionService,
            ITableCellContextService cellContextService,
            ITableParserRouter router,
            ApplicationDbContext dbContext,
            ILogger<TableParsingOrchestrator> logger,
            IBatchValidationService? batchValidator = null)
        {
            #region implementation

            _reconstructionService = reconstructionService;
            _cellContextService = cellContextService;
            _router = router;
            _dbContext = dbContext;
            _logger = logger;
            _batchValidator = batchValidator;

            #endregion
        }

        #endregion Constructor

        #region ITableParsingOrchestrator Implementation

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables: reconstruct → route → parse → write to DB.
        /// </summary>
        /// <param name="filter">Filter for table ID range.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of observations written.</returns>
        public async Task<int> ProcessBatchAsync(TableCellContextFilter filter, CancellationToken ct = default)
        {
            #region implementation

            var tables = await _reconstructionService.ReconstructTablesAsync(filter, ct);
            var totalObservations = 0;

            foreach (var table in tables)
            {
                ct.ThrowIfCancellationRequested();

                var (category, parser) = _router.Route(table);

                if (category == TableCategory.SKIP || parser == null)
                {
                    _logger.LogDebug("Skipping TextTableID={Id} — category={Category}",
                        table.TextTableID, category);
                    continue;
                }

                try
                {
                    var observations = parser.Parse(table);
                    if (observations.Count == 0)
                        continue;

                    var entities = observations.Select(mapToEntity).ToList();
                    _dbContext.AddRange(entities);
                    totalObservations += entities.Count;
                }
                catch (TableParseException tpx)
                {
                    _logger.LogWarning(tpx,
                        "Table-level fault: TextTableID={Id}, Row={Row}, Parser={Parser} — entire table skipped",
                        tpx.TextTableID, tpx.RowSequence, tpx.ParserName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to parse TextTableID={Id} with {Parser} — skipping",
                        table.TextTableID, parser.GetType().Name);
                }
            }

            if (totalObservations > 0)
            {
                try
                {
                    await _dbContext.SaveChangesAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    _dbContext.ChangeTracker.Clear();
                    throw;
                }
                catch (Exception ex)
                {
                    _dbContext.ChangeTracker.Clear();
                    _logger.LogWarning(ex,
                        "Failed to save batch — cleared {Count} tracked entities, batch skipped",
                        totalObservations);
                    return 0;
                }
            }

            _logger.LogDebug("Batch complete: {Count} observations from {Tables} tables",
                totalObservations, tables.Count);

            return totalObservations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Full corpus run: truncate → discover ID range → batch loop.
        /// </summary>
        /// <param name="batchSize">TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total observations written.</returns>
        public async Task<int> ProcessAllAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            CancellationToken ct = default)
        {
            #region implementation

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Stage 3 — Starting full corpus run (batch size={BatchSize}, resume={Resume}, maxBatches={MaxBatches})",
                batchSize, resumeFromId.HasValue ? resumeFromId.Value : "fresh", maxBatches?.ToString() ?? "all");

            // Only truncate on fresh runs — resuming means data already exists
            if (!resumeFromId.HasValue)
            {
                await TruncateAsync(ct);
            }

            var (minId, maxId) = await _cellContextService.GetTextTableIdRangeAsync(ct);
            _logger.LogInformation("TextTableID range: {Min} to {Max}", minId, maxId);

            var effectiveMinId = resumeFromId ?? minId;
            var totalBatches = (int)Math.Ceiling((double)(maxId - effectiveMinId + 1) / batchSize);
            if (maxBatches.HasValue)
            {
                totalBatches = Math.Min(totalBatches, maxBatches.Value);
            }
            var totalObservations = 0;
            var batchNumber = 0;

            for (int start = effectiveMinId; start <= maxId; start += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                batchNumber++;

                // Stop if we've reached the max batches limit
                if (maxBatches.HasValue && batchNumber > maxBatches.Value)
                {
                    break;
                }

                var end = Math.Min(start + batchSize - 1, maxId);
                var filter = new TableCellContextFilter
                {
                    TextTableIdRangeStart = start,
                    TextTableIdRangeEnd = end
                };

                var batchCount = await ProcessBatchAsync(filter, ct);
                totalObservations += batchCount;

                _logger.LogInformation(
                    "Batch {Batch}/{TotalBatches}: IDs [{Start}-{End}], {BatchCount} observations, {Total} cumulative",
                    batchNumber, totalBatches, start, end, batchCount, totalObservations);

                progress?.Report(new TransformBatchProgress
                {
                    BatchNumber = batchNumber,
                    TotalBatches = totalBatches,
                    RangeStart = start,
                    RangeEnd = end,
                    BatchObservationCount = batchCount,
                    CumulativeObservationCount = totalObservations,
                    Elapsed = stopwatch.Elapsed
                });
            }

            stopwatch.Stop();
            _logger.LogInformation("Stage 3 — Complete: {Total} total observations in {Batches} batches ({Elapsed})",
                totalObservations, batchNumber, stopwatch.Elapsed);

            return totalObservations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Debug/test path: parse a single table without DB write.
        /// </summary>
        /// <param name="textTableId">The TextTableID to parse.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of parsed observations.</returns>
        public async Task<List<ParsedObservation>> ParseSingleTableAsync(int textTableId, CancellationToken ct = default)
        {
            #region implementation

            var table = await _reconstructionService.ReconstructTableAsync(textTableId, ct);
            if (table == null)
            {
                _logger.LogWarning("No table found for TextTableID={Id}", textTableId);
                return new List<ParsedObservation>();
            }

            var (category, parser) = _router.Route(table);
            if (category == TableCategory.SKIP || parser == null)
            {
                _logger.LogDebug("TextTableID={Id} categorized as {Category} — no parser", textTableId, category);
                return new List<ParsedObservation>();
            }

            return parser.Parse(table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Truncates tmp_FlattenedStandardizedTable for a clean rerun.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task TruncateAsync(CancellationToken ct = default)
        {
            #region implementation

            _logger.LogInformation("Truncating tmp_FlattenedStandardizedTable");
            await _dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE tmp_FlattenedStandardizedTable", ct);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Full corpus run with Stage 4 validation: truncate → batch loop → validate → report.
        /// </summary>
        /// <param name="batchSize">TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validation report with coverage metrics and issues.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when IBatchValidationService was not provided in the constructor.
        /// </exception>
        public async Task<BatchValidationReport> ProcessAllWithValidationAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            CancellationToken ct = default)
        {
            #region implementation

            if (_batchValidator == null)
            {
                throw new InvalidOperationException(
                    "IBatchValidationService was not provided. Use ProcessAllAsync for runs without validation.");
            }

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Stage 3+4 — Starting full corpus run with validation (batch size={BatchSize}, resume={Resume}, maxBatches={MaxBatches})",
                batchSize, resumeFromId.HasValue ? resumeFromId.Value : "fresh", maxBatches?.ToString() ?? "all");

            // Only truncate on fresh runs — resuming means data already exists
            if (!resumeFromId.HasValue)
            {
                await TruncateAsync(ct);
            }

            var (minId, maxId) = await _cellContextService.GetTextTableIdRangeAsync(ct);
            _logger.LogInformation("TextTableID range: {Min} to {Max}", minId, maxId);

            var effectiveMinId = resumeFromId ?? minId;
            var totalBatches = (int)Math.Ceiling((double)(maxId - effectiveMinId + 1) / batchSize);
            if (maxBatches.HasValue)
            {
                totalBatches = Math.Min(totalBatches, maxBatches.Value);
            }
            var totalObservations = 0;
            var batchNumber = 0;
            var skipReasons = new Dictionary<int, string>();

            for (int start = effectiveMinId; start <= maxId; start += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                batchNumber++;

                // Stop if we've reached the max batches limit
                if (maxBatches.HasValue && batchNumber > maxBatches.Value)
                {
                    break;
                }

                var end = Math.Min(start + batchSize - 1, maxId);
                var filter = new TableCellContextFilter
                {
                    TextTableIdRangeStart = start,
                    TextTableIdRangeEnd = end
                };

                var (batchCount, batchSkips) = await processBatchWithSkipTrackingAsync(filter, ct);
                totalObservations += batchCount;

                foreach (var kvp in batchSkips)
                {
                    skipReasons[kvp.Key] = kvp.Value;
                }

                _logger.LogInformation(
                    "Batch {Batch}/{TotalBatches}: IDs [{Start}-{End}], {BatchCount} observations, {Skipped} skipped, {Total} cumulative",
                    batchNumber, totalBatches, start, end, batchCount, batchSkips.Count, totalObservations);

                progress?.Report(new TransformBatchProgress
                {
                    BatchNumber = batchNumber,
                    TotalBatches = totalBatches,
                    RangeStart = start,
                    RangeEnd = end,
                    BatchObservationCount = batchCount,
                    CumulativeObservationCount = totalObservations,
                    TablesSkippedThisBatch = batchSkips.Count,
                    Elapsed = stopwatch.Elapsed
                });
            }

            _logger.LogInformation("Stage 3 — Complete: {Total} total observations in {Batches} batches ({Elapsed}). Starting validation...",
                totalObservations, batchNumber, stopwatch.Elapsed);

            // Stage 4: Generate validation report from DB
            var report = await _batchValidator.GenerateReportFromDatabaseAsync(skipReasons, ct: ct);

            // Cross-version concordance
            var discrepancies = await _batchValidator.CheckCrossVersionConcordanceAsync(ct);
            report.CrossVersionDiscrepancies = discrepancies;

            stopwatch.Stop();
            _logger.LogInformation("Stage 4 — Validation complete. {Discrepancies} cross-version discrepancies ({Elapsed})",
                discrepancies.Count, stopwatch.Elapsed);

            return report;

            #endregion
        }

        #endregion ITableParsingOrchestrator Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables with skip reason tracking.
        /// Same logic as <see cref="ProcessBatchAsync"/> but also returns a dictionary
        /// of skipped TextTableIDs and their reasons.
        /// </summary>
        /// <param name="filter">Filter for table ID range.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Tuple of (observation count, skip reasons dictionary).</returns>
        private async Task<(int observationCount, Dictionary<int, string> skipReasons)> processBatchWithSkipTrackingAsync(
            TableCellContextFilter filter, CancellationToken ct)
        {
            #region implementation

            var tables = await _reconstructionService.ReconstructTablesAsync(filter, ct);
            var totalObservations = 0;
            var skipReasons = new Dictionary<int, string>();

            foreach (var table in tables)
            {
                ct.ThrowIfCancellationRequested();

                var (category, parser) = _router.Route(table);

                if (category == TableCategory.SKIP || parser == null)
                {
                    if (table.TextTableID.HasValue)
                    {
                        skipReasons[table.TextTableID.Value] = $"SKIP:{category}";
                    }

                    _logger.LogDebug("Skipping TextTableID={Id} — category={Category}",
                        table.TextTableID, category);
                    continue;
                }

                try
                {
                    var observations = parser.Parse(table);
                    if (observations.Count == 0)
                    {
                        if (table.TextTableID.HasValue)
                        {
                            skipReasons[table.TextTableID.Value] = $"EMPTY:{parser.GetType().Name}";
                        }

                        continue;
                    }

                    var entities = observations.Select(mapToEntity).ToList();
                    _dbContext.AddRange(entities);
                    totalObservations += entities.Count;
                }
                catch (TableParseException tpx)
                {
                    if (table.TextTableID.HasValue)
                    {
                        skipReasons[table.TextTableID.Value] =
                            $"ERROR:{tpx.ParserName}:Row{tpx.RowSequence}";
                    }

                    _logger.LogWarning(tpx,
                        "Table-level fault: TextTableID={Id}, Row={Row}, Parser={Parser} — entire table skipped",
                        tpx.TextTableID, tpx.RowSequence, tpx.ParserName);
                }
                catch (Exception ex)
                {
                    if (table.TextTableID.HasValue)
                    {
                        skipReasons[table.TextTableID.Value] = $"ERROR:{parser.GetType().Name}";
                    }

                    _logger.LogWarning(ex,
                        "Failed to parse TextTableID={Id} with {Parser} — skipping",
                        table.TextTableID, parser.GetType().Name);
                }
            }

            if (totalObservations > 0)
            {
                try
                {
                    await _dbContext.SaveChangesAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    _dbContext.ChangeTracker.Clear();
                    throw;
                }
                catch (Exception ex)
                {
                    _dbContext.ChangeTracker.Clear();
                    _logger.LogWarning(ex,
                        "Failed to save batch — cleared {Count} tracked entities, batch skipped",
                        totalObservations);
                    return (0, skipReasons);
                }
            }

            return (totalObservations, skipReasons);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps a <see cref="ParsedObservation"/> DTO to a <see cref="LabelView.FlattenedStandardizedTable"/> entity.
        /// </summary>
        private static LabelView.FlattenedStandardizedTable mapToEntity(ParsedObservation obs)
        {
            #region implementation

            return new LabelView.FlattenedStandardizedTable
            {
                DocumentGUID = obs.DocumentGUID,
                LabelerName = obs.LabelerName,
                ProductTitle = obs.ProductTitle,
                VersionNumber = obs.VersionNumber,
                TextTableID = obs.TextTableID,
                Caption = obs.Caption,
                SourceRowSeq = obs.SourceRowSeq,
                SourceCellSeq = obs.SourceCellSeq,
                TableCategory = obs.TableCategory,
                ParentSectionCode = obs.ParentSectionCode,
                ParentSectionTitle = obs.ParentSectionTitle,
                SectionTitle = obs.SectionTitle,
                ParameterName = obs.ParameterName,
                ParameterCategory = obs.ParameterCategory,
                ParameterSubtype = obs.ParameterSubtype,
                TreatmentArm = obs.TreatmentArm,
                ArmN = obs.ArmN,
                StudyContext = obs.StudyContext,
                DoseRegimen = obs.DoseRegimen,
                Population = obs.Population,
                Timepoint = obs.Timepoint,
                RawValue = obs.RawValue,
                PrimaryValue = obs.PrimaryValue,
                PrimaryValueType = obs.PrimaryValueType,
                SecondaryValue = obs.SecondaryValue,
                SecondaryValueType = obs.SecondaryValueType,
                LowerBound = obs.LowerBound,
                UpperBound = obs.UpperBound,
                BoundType = obs.BoundType,
                PValue = obs.PValue,
                Unit = obs.Unit,
                ParseConfidence = obs.ParseConfidence,
                ParseRule = obs.ParseRule,
                FootnoteMarkers = obs.FootnoteMarkers,
                FootnoteText = obs.FootnoteText,
                ValidationFlags = obs.ValidationFlags
            };

            #endregion
        }

        #endregion Private Helpers
    }
}
