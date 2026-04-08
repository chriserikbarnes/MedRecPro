using System.Diagnostics;
using Humanizer;
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

        private const int MAX_TEXT_LENGTH = 1994;  // NVARCHAR(2000): 1994 + " ..." = 1998 ≤ 2000
        private const int MED_TEXT_LENGTH = 994;  // NVARCHAR(1000): 994 + " ..." = 998 ≤ 1000
        private const int SML_TEXT_LENGTH = 494;  // NVARCHAR(500):  494 + " ..." = 498 ≤ 500
        private const int XSM_TEXT_LENGTH = 94;   // NVARCHAR(100):  94 + " ..." = 98 ≤ 100
        private const int TINY_TEXT_LENGTH = 44;  // NVARCHAR(50):   44 + " ..." = 48 ≤ 50

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

        /**************************************************************/
        /// <summary>
        /// Optional Stage 3.25 column standardization service. Null if standardization is not configured.
        /// Applies deterministic rules to correct misclassified TreatmentArm/ArmN/DoseRegimen/StudyContext.
        /// </summary>
        private readonly IColumnStandardizationService? _columnStandardizer;

        /**************************************************************/
        /// <summary>
        /// Whether the column standardizer has been initialized (lazy init on first batch).
        /// </summary>
        private bool _columnStdInitialized;

        /**************************************************************/
        /// <summary>
        /// Optional Stage 3.4 ML.NET correction and anomaly scoring service.
        /// Null if ML correction is not configured.
        /// </summary>
        private readonly IMlNetCorrectionService? _mlNetCorrectionService;

        /**************************************************************/
        /// <summary>
        /// Whether the ML.NET correction service has been initialized (lazy init on first batch).
        /// </summary>
        private bool _mlNetInitialized;

        /**************************************************************/
        /// <summary>
        /// Optional Stage 3.5 Claude API correction service. Null if AI correction is not configured.
        /// </summary>
        private readonly IClaudeApiCorrectionService? _correctionService;

        /**************************************************************/
        /// <summary>
        /// When true, observations where BOTH <see cref="ParsedObservation.ArmN"/> and
        /// <see cref="ParsedObservation.PrimaryValue"/> are null are dropped at the conclusion
        /// of Stage 3.25 (column standardization). Such rows cannot participate in cross-product
        /// meta-analysis and are considered unrecoverable for downstream processing.
        /// Default false — change is opt-in and backward compatible.
        /// </summary>
        private readonly bool _dropRowsMissingArmNOrPrimaryValue;

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
        /// <param name="columnStandardizer">Optional Stage 3.25 column standardization service. Pass null to skip standardization.</param>
        /// <param name="mlNetCorrectionService">Optional Stage 3.4 ML.NET correction and anomaly scoring service. Pass null to skip ML correction.</param>
        /// <param name="correctionService">Optional Stage 3.5 Claude API correction service. Pass null to skip AI correction.</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// Optional Stage 3.25 quality gate. When true, observations with BOTH
        /// <see cref="ParsedObservation.ArmN"/> and <see cref="ParsedObservation.PrimaryValue"/>
        /// null are dropped at the end of Stage 3.25. Default false preserves legacy behavior.
        /// </param>
        public TableParsingOrchestrator(
            ITableReconstructionService reconstructionService,
            ITableCellContextService cellContextService,
            ITableParserRouter router,
            ApplicationDbContext dbContext,
            ILogger<TableParsingOrchestrator> logger,
            IBatchValidationService? batchValidator = null,
            IColumnStandardizationService? columnStandardizer = null,
            IMlNetCorrectionService? mlNetCorrectionService = null,
            IClaudeApiCorrectionService? correctionService = null,
            bool dropRowsMissingArmNOrPrimaryValue = false)
        {
            #region implementation

            _reconstructionService = reconstructionService;
            _cellContextService = cellContextService;
            _router = router;
            _dbContext = dbContext;
            _logger = logger;
            _batchValidator = batchValidator;
            _columnStandardizer = columnStandardizer;
            _mlNetCorrectionService = mlNetCorrectionService;
            _correctionService = correctionService;
            _dropRowsMissingArmNOrPrimaryValue = dropRowsMissingArmNOrPrimaryValue;

            #endregion
        }

        #endregion Constructor

        #region ITableParsingOrchestrator Implementation

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables: reconstruct → route → parse → write to DB.
        /// </summary>
        /// <param name="filter">Filter for table ID range.</param>
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within the batch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of observations written.</returns>
        public async Task<int> ProcessBatchAsync(TableCellContextFilter filter, IProgress<TransformBatchProgress>? rowProgress = null, CancellationToken ct = default)
        {
            #region implementation

            var result = await ProcessBatchWithStagesAsync(filter, rowProgress, ct);
            return result.ObservationsWritten;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Full corpus run: truncate → discover ID range → batch loop.
        /// </summary>
        /// <param name="batchSize">TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes (persisted to disk).</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within each batch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total observations written.</returns>
        public async Task<int> ProcessAllAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            IProgress<TransformBatchProgress>? rowProgress = null,
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

                // Wrap rowProgress to inject batch-level context into each per-table report.
                // Uses SynchronousProgress to avoid double-async posting: Progress<T> posts
                // to ThreadPool, and chaining two of them delays callbacks until after the
                // batch completes — defeating the purpose of per-table progress.
                var capturedBatchNumber = batchNumber;
                var capturedTotalObs = totalObservations;
                IProgress<TransformBatchProgress>? innerProgress = rowProgress != null
                    ? new Helpers.SynchronousProgress<TransformBatchProgress>(p =>
                    {
                        p.BatchNumber = capturedBatchNumber;
                        p.TotalBatches = totalBatches;
                        p.RangeStart = start;
                        p.RangeEnd = end;
                        p.CumulativeObservationCount = capturedTotalObs + p.BatchObservationCount;
                        p.Elapsed = stopwatch.Elapsed;
                        rowProgress.Report(p);
                    })
                    : null;

                var batchCount = await ProcessBatchAsync(filter, innerProgress, ct);
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
        /// <param name="progress">Optional progress callback invoked after each batch completes (persisted to disk).</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within each batch.</param>
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
            IProgress<TransformBatchProgress>? rowProgress = null,
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

                // Wrap rowProgress to inject batch-level context into each per-table report.
                // Uses SynchronousProgress to avoid double-async posting (see ProcessAllAsync).
                var capturedBatchNumber = batchNumber;
                var capturedTotalObs = totalObservations;
                IProgress<TransformBatchProgress>? innerProgress = rowProgress != null
                    ? new Helpers.SynchronousProgress<TransformBatchProgress>(p =>
                    {
                        p.BatchNumber = capturedBatchNumber;
                        p.TotalBatches = totalBatches;
                        p.RangeStart = start;
                        p.RangeEnd = end;
                        p.CumulativeObservationCount = capturedTotalObs + p.BatchObservationCount;
                        p.Elapsed = stopwatch.Elapsed;
                        rowProgress.Report(p);
                    })
                    : null;

                var stageResult = await ProcessBatchWithStagesAsync(filter, innerProgress, ct);
                var batchCount = stageResult.ObservationsWritten;
                var batchSkips = stageResult.SkipReasons;
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

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables with full stage visibility, capturing intermediate results
        /// at each stage boundary. Same pipeline as <see cref="ProcessBatchAsync"/> but returns
        /// a <see cref="BatchStageResult"/> with reconstructed tables, routing decisions,
        /// pre/post-correction observations, and skip reasons.
        /// </summary>
        /// <param name="filter">Filter for table ID range.</param>
        /// <param name="rowProgress">Optional intra-batch progress callback for per-table and per-stage updates.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Batch stage result with all intermediate data.</returns>
        /// <seealso cref="ProcessBatchAsync"/>
        /// <seealso cref="BatchStageResult"/>
        public async Task<BatchStageResult> ProcessBatchWithStagesAsync(
            TableCellContextFilter filter,
            IProgress<TransformBatchProgress>? rowProgress = null,
            CancellationToken ct = default)
        {
            #region implementation

            var result = new BatchStageResult();

            await ensureServicesInitializedAsync(ct);

            // Helper to fire intra-batch progress reports (captures rowProgress)
            void reportProgress(string operation, double pct, int processed, int total)
            {
                rowProgress?.Report(new TransformBatchProgress
                {
                    CurrentOperation = operation,
                    IntraBatchPercent = pct,
                    TablesProcessedInBatch = processed,
                    TotalTablesInBatch = total
                });
            }

            // Stage 2: Reconstruct tables
            reportProgress("Reconstructing tables...", 0, 0, 0);
            var tables = await _reconstructionService.ReconstructTablesAsync(filter, ct);
            result.ReconstructedTables = tables;

            // Stage 3: Route + Parse (0% → 20%)
            var (allObservations, tablesProcessed, tableCount) = routeAndParseTables(tables, result, reportProgress, ct);
            result.PreCorrectionObservations = allObservations;

            // Stage 3.25: Column standardization (deterministic, pre-AI)
            reportProgress("Column standardization...", 21, tablesProcessed, tableCount);
            allObservations = runColumnStandardization(allObservations);

            // Stage 3.25 quality gate (opt-in): drop rows missing both ArmN and PrimaryValue
            allObservations = dropIncompleteRows(allObservations);

            // Stage 3.4: ML.NET correction and anomaly scoring
            reportProgress("ML.NET scoring...", 23, tablesProcessed, tableCount);
            allObservations = runMlCorrection(allObservations);

            // Stage 3.5: Claude AI Correction (25% → 95%)
            reportProgress("Claude AI correction...", 25, tablesProcessed, tableCount);
            allObservations = await runClaudeCorrectionAsync(allObservations, tables, reportProgress, tablesProcessed, tableCount, result, ct);

            // Stage 3.6: Post-processing extraction (catch units/N= values Claude corrected into extractable form)
            reportProgress("Post-processing extraction...", 95.5, tablesProcessed, tableCount);
            allObservations = runPostProcessExtraction(allObservations);

            result.PostCorrectionObservations = allObservations;

            // DB Write (96% → 100%)
            reportProgress("Writing to database...", 96, tablesProcessed, tableCount);
            await writeObservationsAsync(allObservations, result, ct);

            reportProgress("Batch complete", 100, tablesProcessed, tableCount);

            _logger.LogDebug("Batch with stages complete: {Count} observations from {Tables} tables",
                result.ObservationsWritten, tables.Count);

            return result;

            #endregion
        }

        #region ProcessBatchWithStages — Extracted Sub-Methods

        /**************************************************************/
        /// <summary>
        /// Lazy-initializes the column standardizer dictionary and ML.NET correction service
        /// on the first batch. Safe to call multiple times — no-ops after first initialization.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <seealso cref="ProcessBatchWithStagesAsync"/>
        private async Task ensureServicesInitializedAsync(CancellationToken ct)
        {
            #region implementation

            if (_columnStandardizer != null && !_columnStdInitialized)
            {
                await _columnStandardizer.InitializeAsync(ct);
                _columnStdInitialized = true;
            }

            if (_mlNetCorrectionService != null && !_mlNetInitialized)
            {
                await _mlNetCorrectionService.InitializeAsync(ct);
                _mlNetInitialized = true;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3: Routes each reconstructed table through the parser router and collects
        /// parsed observations. Populates routing decisions and skip reasons on the result.
        /// Progress maps to 0%–20% of intra-batch range.
        /// </summary>
        /// <param name="tables">Reconstructed tables from Stage 2.</param>
        /// <param name="result">Batch result to populate with routing decisions and skip reasons.</param>
        /// <param name="reportProgress">Progress callback.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Tuple of (observations, tablesProcessed, tableCount).</returns>
        /// <seealso cref="ProcessBatchWithStagesAsync"/>
        private (List<ParsedObservation> observations, int tablesProcessed, int tableCount) routeAndParseTables(
            List<ReconstructedTable> tables,
            BatchStageResult result,
            Action<string, double, int, int> reportProgress,
            CancellationToken ct)
        {
            #region implementation

            var allObservations = new List<ParsedObservation>();
            var tableCount = tables.Count;
            var tablesProcessed = 0;

            foreach (var table in tables)
            {
                ct.ThrowIfCancellationRequested();

                var (category, parser) = _router.Route(table);

                var decision = new TableRoutingDecision
                {
                    TextTableID = table.TextTableID ?? 0,
                    Category = category,
                    ParserName = parser?.GetType().Name
                };

                if (category == TableCategory.SKIP || parser == null)
                {
                    decision.ObservationCount = 0;
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] = $"SKIP:{category}";
                    }

                    _logger.LogDebug("Skipping TextTableID={Id} — category={Category}",
                        table.TextTableID, category);

                    tablesProcessed++;
                    var tablePct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                    reportProgress("Parsing tables...", tablePct, tablesProcessed, tableCount);
                    continue;
                }

                try
                {
                    var observations = parser.Parse(table);
                    decision.ObservationCount = observations.Count;
                    result.RoutingDecisions.Add(decision);

                    if (observations.Count == 0)
                    {
                        if (table.TextTableID.HasValue)
                        {
                            result.SkipReasons[table.TextTableID.Value] = $"EMPTY:{parser.GetType().Name}";
                        }

                        tablesProcessed++;
                        var tablePct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                        reportProgress("Parsing tables...", tablePct, tablesProcessed, tableCount);
                        continue;
                    }

                    allObservations.AddRange(observations);
                }
                catch (TableParseException tpx)
                {
                    decision.ObservationCount = 0;
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] =
                            $"ERROR:{tpx.ParserName}:Row{tpx.RowSequence}";
                    }

                    _logger.LogWarning(tpx,
                        "Table-level fault: TextTableID={Id}, Row={Row}, Parser={Parser} — entire table skipped",
                        tpx.TextTableID, tpx.RowSequence, tpx.ParserName);
                }
                catch (Exception ex)
                {
                    decision.ObservationCount = 0;
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] = $"ERROR:{parser.GetType().Name}";
                    }

                    _logger.LogWarning(ex,
                        "Failed to parse TextTableID={Id} with {Parser} — skipping",
                        table.TextTableID, parser.GetType().Name);
                }

                tablesProcessed++;
                var pct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                reportProgress("Parsing tables...", pct, tablesProcessed, tableCount);
            }

            return (allObservations, tablesProcessed, tableCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.25: Runs deterministic column standardization on all observations.
        /// No-ops if the column standardizer is null or there are no observations.
        /// </summary>
        /// <param name="observations">Observations to standardize.</param>
        /// <returns>The standardized observations (same list, modified in-place).</returns>
        /// <seealso cref="IColumnStandardizationService.Standardize"/>
        private List<ParsedObservation> runColumnStandardization(List<ParsedObservation> observations)
        {
            #region implementation

            if (_columnStandardizer != null && observations.Count > 0)
            {
                observations = _columnStandardizer.Standardize(observations);
            }

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.25 quality gate (opt-in): drops observations where either
        /// <see cref="ParsedObservation.ArmN"/> or <see cref="ParsedObservation.PrimaryValue"/>
        /// is null. Cross-product meta-analysis downstream requires BOTH fields populated,
        /// so any row missing either one is unrecoverable and is removed before
        /// ML.NET / Claude / post-processing to avoid spending work on it.
        /// </summary>
        /// <remarks>
        /// No-op when <c>_dropRowsMissingArmNOrPrimaryValue</c> is false (the default),
        /// preserving legacy behavior. Enabled via the console CLI flag
        /// <c>--drop-incomplete-rows</c>, the interactive prompt, or the
        /// <c>Standardization.DropRowsMissingArmNOrPrimaryValue</c> config setting.
        /// </remarks>
        /// <param name="observations">Observations to filter.</param>
        /// <returns>
        /// The filtered list. Returns the original list unchanged when the gate is disabled
        /// or no rows match; otherwise returns a new list containing only the surviving rows.
        /// </returns>
        /// <seealso cref="ProcessBatchWithStagesAsync"/>
        /// <seealso cref="runColumnStandardization"/>
        private List<ParsedObservation> dropIncompleteRows(List<ParsedObservation> observations)
        {
            #region implementation

            // Opt-in gate: default false preserves legacy behavior for all existing callers.
            if (!_dropRowsMissingArmNOrPrimaryValue || observations.Count == 0)
            {
                return observations;
            }

            var originalCount = observations.Count;

            // Keep only rows where BOTH ArmN AND PrimaryValue are populated.
            // Cross-product meta-analysis requires both fields — a row missing either
            // cannot participate downstream and is unrecoverable.
            var surviving = observations
                .Where(o => o.ArmN != null && o.PrimaryValue != null)
                .ToList();

            var droppedCount = originalCount - surviving.Count;

            if (droppedCount > 0)
            {
                _logger.LogInformation(
                    "Stage 3.25 quality gate: dropped {Dropped}/{Total} rows missing ArmN or PrimaryValue",
                    droppedCount, originalCount);
            }

            return surviving;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.4: Runs ML.NET correction and anomaly scoring on all observations.
        /// No-ops if the ML correction service is null or there are no observations.
        /// </summary>
        /// <param name="observations">Observations to score and correct.</param>
        /// <returns>The corrected observations (same list, modified in-place).</returns>
        /// <seealso cref="IMlNetCorrectionService.ScoreAndCorrect"/>
        private List<ParsedObservation> runMlCorrection(List<ParsedObservation> observations)
        {
            #region implementation

            if (_mlNetCorrectionService != null && observations.Count > 0)
            {
                observations = _mlNetCorrectionService.ScoreAndCorrect(observations);
            }

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.5: Runs Claude AI correction on all observations. Builds table lookup,
        /// forwards progress (25%–95%), counts corrections, and feeds corrections back to ML.
        /// No-ops if the correction service is null or there are no observations.
        /// </summary>
        /// <param name="observations">Observations to correct.</param>
        /// <param name="tables">Reconstructed tables for context lookup.</param>
        /// <param name="reportProgress">Progress callback.</param>
        /// <param name="tablesProcessed">Number of tables processed so far.</param>
        /// <param name="tableCount">Total number of tables in batch.</param>
        /// <param name="result">Batch result to populate with correction count.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The corrected observations.</returns>
        /// <seealso cref="IClaudeApiCorrectionService.CorrectBatchAsync"/>
        private async Task<List<ParsedObservation>> runClaudeCorrectionAsync(
            List<ParsedObservation> observations,
            List<ReconstructedTable> tables,
            Action<string, double, int, int> reportProgress,
            int tablesProcessed,
            int tableCount,
            BatchStageResult result,
            CancellationToken ct)
        {
            #region implementation

            if (_correctionService == null || observations.Count == 0)
                return observations;

            // Forwarding progress: map correction service's 0–100 into orchestrator's 25–95 range
            var claudeProgress = new Helpers.SynchronousProgress<TransformBatchProgress>(p =>
            {
                var scaledPct = 25.0 + (p.IntraBatchPercent / 100.0) * 70.0;
                reportProgress(p.CurrentOperation ?? "Claude AI correction...", scaledPct, tablesProcessed, tableCount);
            });

            var preCorrectionFlags = observations
                .Select(o => o.ValidationFlags)
                .ToList();

            // Build table lookup so each TextTableID group gets its original table context
            var tableLookup = tables
                .Where(t => t.TextTableID.HasValue)
                .ToDictionary(t => t.TextTableID!.Value);

            observations = await _correctionService.CorrectBatchAsync(observations, originalTables: tableLookup, progress: claudeProgress, ct: ct);

            // Count corrections by checking ValidationFlags changes
            result.CorrectionCount = observations
                .Select((o, i) => o.ValidationFlags != preCorrectionFlags[i] ? 1 : 0)
                .Sum();

            // Stage 3.4 feedback: feed Claude corrections back to ML as ground truth
            if (_mlNetCorrectionService != null)
            {
                await _mlNetCorrectionService.FeedClaudeCorrectedBatchAsync(observations, ct);
            }

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps observations to entities and bulk-writes to the database via SaveChangesAsync.
        /// Handles cancellation (re-throws) and general failures (clears tracker, logs warning).
        /// </summary>
        /// <param name="observations">Observations to persist.</param>
        /// <param name="result">Batch result to populate with write count.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <seealso cref="mapToEntity"/>
        private async Task writeObservationsAsync(
            List<ParsedObservation> observations,
            BatchStageResult result,
            CancellationToken ct)
        {
            #region implementation

            if (observations.Count == 0)
                return;

            try
            {
                var entities = observations.Select(mapToEntity).ToList();
                _dbContext.AddRange(entities);
                await _dbContext.SaveChangesAsync(ct);
                result.ObservationsWritten = entities.Count;
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
                    observations.Count);
                result.ObservationsWritten = 0;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.6: Runs post-processing extraction to catch units and N-values that
        /// Claude may have corrected into extractable form. No-ops if the column standardizer
        /// is null or there are no observations.
        /// </summary>
        /// <param name="observations">Observations after all correction stages.</param>
        /// <returns>The post-processed observations.</returns>
        /// <seealso cref="IColumnStandardizationService.PostProcessExtraction"/>
        private List<ParsedObservation> runPostProcessExtraction(List<ParsedObservation> observations)
        {
            #region implementation

            if (_columnStandardizer != null && observations.Count > 0)
            {
                observations = _columnStandardizer.PostProcessExtraction(observations);
            }

            return observations;

            #endregion
        }

        #endregion ProcessBatchWithStages — Extracted Sub-Methods

        #endregion ITableParsingOrchestrator Implementation

        #region Stage-by-Stage Diagnostic Methods

        /**************************************************************/
        /// <summary>
        /// Stage 2 only: reconstructs a single table by TextTableID without routing or parsing.
        /// Thin facade over <see cref="ITableReconstructionService.ReconstructTableAsync"/>.
        /// </summary>
        /// <param name="textTableId">The TextTableID to reconstruct.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A <see cref="ReconstructedTable"/> with classified rows and resolved headers,
        /// or null if no cells exist for the given TextTableID.
        /// </returns>
        /// <seealso cref="RouteAndParseSingleTable"/>
        public async Task<ReconstructedTable?> ReconstructSingleTableAsync(int textTableId, CancellationToken ct = default)
        {
            #region implementation

            return await _reconstructionService.ReconstructTableAsync(textTableId, ct);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3 only: routes a reconstructed table to a parser and parses it. Does not
        /// write to the database or apply Claude correction.
        /// </summary>
        /// <param name="table">A reconstructed table from <see cref="ReconstructSingleTableAsync"/>.</param>
        /// <returns>
        /// Tuple of (category, parserName, observations):
        /// - category: the <see cref="TableCategory"/> determined by the router
        /// - parserName: the concrete parser type name, or null if SKIP
        /// - observations: parsed observations, or empty list if skipped
        /// </returns>
        /// <seealso cref="ReconstructSingleTableAsync"/>
        /// <seealso cref="CorrectObservationsAsync"/>
        public (TableCategory category, string? parserName, List<ParsedObservation> observations) RouteAndParseSingleTable(ReconstructedTable table)
        {
            #region implementation

            var (category, parser) = _router.Route(table);

            if (category == TableCategory.SKIP || parser == null)
            {
                _logger.LogDebug("TextTableID={Id} categorized as {Category} — no parser",
                    table.TextTableID, category);
                return (category, null, new List<ParsedObservation>());
            }

            var observations = parser.Parse(table);
            return (category, parser.GetType().Name, observations);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.5 only: applies Claude AI correction to a list of parsed observations.
        /// Returns the original list unmodified if the correction service is not configured.
        /// </summary>
        /// <param name="observations">Observations from <see cref="RouteAndParseSingleTable"/>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The corrected observations with <c>AI_CORRECTED:*</c> flags appended to
        /// <see cref="ParsedObservation.ValidationFlags"/> for each correction applied.
        /// </returns>
        /// <seealso cref="IClaudeApiCorrectionService.CorrectBatchAsync"/>
        public async Task<List<ParsedObservation>> CorrectObservationsAsync(List<ParsedObservation> observations, CancellationToken ct = default)
        {
            #region implementation

            if (_correctionService == null || observations.Count == 0)
            {
                return observations;
            }

            return await _correctionService.CorrectBatchAsync(observations, originalTables: null, ct: ct);

            #endregion
        }

        #endregion Stage-by-Stage Diagnostic Methods

        #region Private Helpers

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
                LabelerName = obs.LabelerName.Truncate(SML_TEXT_LENGTH),
                ProductTitle = obs.ProductTitle.Truncate(SML_TEXT_LENGTH),
                VersionNumber = obs.VersionNumber,
                TextTableID = obs.TextTableID,
                Caption = obs.Caption,
                SourceRowSeq = obs.SourceRowSeq,
                SourceCellSeq = obs.SourceCellSeq,
                TableCategory = obs.TableCategory.Truncate(SML_TEXT_LENGTH),
                ParentSectionCode = obs.ParentSectionCode.Truncate(TINY_TEXT_LENGTH),
                ParentSectionTitle = obs.ParentSectionTitle.Truncate(MED_TEXT_LENGTH),
                SectionTitle = obs.SectionTitle.Truncate(MED_TEXT_LENGTH),
                ParameterName = obs.ParameterName.Truncate(MED_TEXT_LENGTH),
                ParameterCategory = obs.ParameterCategory.Truncate(SML_TEXT_LENGTH),
                ParameterSubtype = obs.ParameterSubtype.Truncate(SML_TEXT_LENGTH),
                TreatmentArm = obs.TreatmentArm.Truncate(MED_TEXT_LENGTH),
                ArmN = obs.ArmN,
                StudyContext = obs.StudyContext.Truncate(MED_TEXT_LENGTH),
                DoseRegimen = obs.DoseRegimen.Truncate(MED_TEXT_LENGTH),
                Dose = obs.Dose,
                DoseUnit = obs.DoseUnit.Truncate(TINY_TEXT_LENGTH),
                Population = obs.Population.Truncate(SML_TEXT_LENGTH),
                Timepoint = obs.Timepoint.Truncate(SML_TEXT_LENGTH),
                Time = obs.Time,
                TimeUnit = obs.TimeUnit.Truncate(TINY_TEXT_LENGTH),
                RawValue = obs.RawValue.Truncate(MAX_TEXT_LENGTH),
                PrimaryValue = obs.PrimaryValue,
                PrimaryValueType = obs.PrimaryValueType.Truncate(XSM_TEXT_LENGTH),
                SecondaryValue = obs.SecondaryValue,
                SecondaryValueType = obs.SecondaryValueType.Truncate(XSM_TEXT_LENGTH),
                LowerBound = obs.LowerBound,
                UpperBound = obs.UpperBound,
                BoundType = obs.BoundType.Truncate(TINY_TEXT_LENGTH),
                PValue = obs.PValue,
                Unit = obs.Unit.Truncate(SML_TEXT_LENGTH),
                ParseConfidence = obs.ParseConfidence,
                ParseRule = obs.ParseRule.Truncate(XSM_TEXT_LENGTH),
                FootnoteMarkers = obs.FootnoteMarkers.Truncate(SML_TEXT_LENGTH),
                FootnoteText = obs.FootnoteText,
                ValidationFlags = obs.ValidationFlags.Truncate(MED_TEXT_LENGTH)
            };

            #endregion
        }

        #endregion Private Helpers

    }
}
