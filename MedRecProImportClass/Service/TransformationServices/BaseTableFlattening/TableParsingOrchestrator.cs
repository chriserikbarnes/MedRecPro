using System.Diagnostics;
using Humanizer;
using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
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
        /// Optional Stage 5 (Phase 2) AdverseEvent denormalization service. When non-null,
        /// invoked at the end of <see cref="ProcessAllWithValidationAsync"/> after Stage 4
        /// validation completes. Null when AE denormalization is not configured.
        /// </summary>
        private readonly IAdverseEventDenormalizationService? _aeDenormalizer;

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
        private readonly IQCNetCorrectionService? _qcNetCorrectionService;

        /**************************************************************/
        /// <summary>
        /// Whether the ML.NET correction service has been initialized (lazy init on first batch).
        /// </summary>
        private bool _qcNetInitialized;

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

        /**************************************************************/
        /// <summary>
        /// Optional Stage 0 bioequivalent-label dedup filter. When non-null and
        /// <c>disableBioequivalentDedup</c> is false, the UNII-ordered document list
        /// is pruned to one canonical label per (Ingredient, DosageForm, Route) group
        /// before batching begins.
        /// </summary>
        private readonly IBioequivalentLabelDedupService? _bioequivalentDedup;

        /**************************************************************/
        /// <summary>
        /// Result returned by a per-batch dispatch delegate inside the shared full-corpus loop.
        /// </summary>
        /// <remarks>
        /// Keeps the loop independent from whether the caller dispatches parse-only
        /// batches or stage-visible parse/validation batches.
        /// </remarks>
        /// <seealso cref="executeFullCorpusBatchLoopAsync"/>
        private sealed class FullCorpusBatchOutcome
        {
            /**************************************************************/
            /// <summary>
            /// Number of observations written by the dispatched batch.
            /// </summary>
            public int ObservationsWritten { get; init; }

            /**************************************************************/
            /// <summary>
            /// Skip reasons captured by stage-visible batch processing.
            /// </summary>
            public Dictionary<int, string> SkipReasons { get; init; } = new();
        }

        /**************************************************************/
        /// <summary>
        /// Aggregate result returned by the shared full-corpus batch loop.
        /// </summary>
        /// <seealso cref="executeFullCorpusBatchLoopAsync"/>
        private sealed class FullCorpusBatchLoopResult
        {
            /**************************************************************/
            /// <summary>
            /// Total observations written across all completed batches.
            /// </summary>
            public int TotalObservations { get; init; }

            /**************************************************************/
            /// <summary>
            /// Number of batches dispatched before completion or max-batch cutoff.
            /// </summary>
            public int BatchCount { get; init; }

            /**************************************************************/
            /// <summary>
            /// Combined skip reasons across all completed batches.
            /// </summary>
            public Dictionary<int, string> SkipReasons { get; init; } = new();
        }

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
        /// <param name="qcNetCorrectionService">Optional Stage 3.4 ML.NET correction and anomaly scoring service. Pass null to skip ML correction.</param>
        /// <param name="correctionService">Optional Stage 3.5 Claude API correction service. Pass null to skip AI correction.</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// Optional Stage 3.25 quality gate. When true, observations with BOTH
        /// <see cref="ParsedObservation.ArmN"/> and <see cref="ParsedObservation.PrimaryValue"/>
        /// null are dropped at the end of Stage 3.25. Default false preserves legacy behavior.
        /// </param>
        /// <param name="bioequivalentDedup">
        /// Optional Stage 0 filter that prunes bioequivalent-ANDA duplicates from the
        /// UNII-ordered document list before Stage 1 begins. When null, no dedup is
        /// applied (legacy behavior). When non-null, used by
        /// <see cref="ProcessAllAsync"/> and <see cref="ProcessAllWithValidationAsync"/>
        /// unless the caller passes <c>disableBioequivalentDedup: true</c>.
        /// </param>
        /// <param name="aeDenormalizer">
        /// Optional Stage 5 (Phase 2) AdverseEvent denormalization service. When non-null,
        /// invoked at the end of <see cref="ProcessAllWithValidationAsync"/> to populate
        /// <c>tmp_FlattenedAdverseEventTable</c>. Pass null to skip Stage 5.
        /// </param>
        public TableParsingOrchestrator(
            ITableReconstructionService reconstructionService,
            ITableCellContextService cellContextService,
            ITableParserRouter router,
            ApplicationDbContext dbContext,
            ILogger<TableParsingOrchestrator> logger,
            IBatchValidationService? batchValidator = null,
            IColumnStandardizationService? columnStandardizer = null,
            IQCNetCorrectionService? qcNetCorrectionService = null,
            IClaudeApiCorrectionService? correctionService = null,
            bool dropRowsMissingArmNOrPrimaryValue = false,
            IBioequivalentLabelDedupService? bioequivalentDedup = null,
            IAdverseEventDenormalizationService? aeDenormalizer = null)
        {
            #region implementation

            _reconstructionService = reconstructionService;
            _cellContextService = cellContextService;
            _router = router;
            _dbContext = dbContext;
            _logger = logger;
            _batchValidator = batchValidator;
            _columnStandardizer = columnStandardizer;
            _qcNetCorrectionService = qcNetCorrectionService;
            _correctionService = correctionService;
            _dropRowsMissingArmNOrPrimaryValue = dropRowsMissingArmNOrPrimaryValue;
            _bioequivalentDedup = bioequivalentDedup;
            _aeDenormalizer = aeDenormalizer;

            // Bulk-insert only — no read-modify-write — so change detection is pure overhead.
            // Paired with explicit Clear() after SaveChanges so the tracker never grows.
            // Guarded because single-table debug paths (ParseSingleTableAsync) legitimately
            // run without a DbContext; only the batch writers need the optimization.
            if (_dbContext is not null)
            {
                _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            }

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

            // Reuse the stage-visible path so the simpler API cannot drift from
            // validation runs; the expected outcome is the same persisted row count.
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
            bool disableBioequivalentDedup = false,
            CancellationToken ct = default)
        {
            #region implementation

            // Own the run clock in the public entry point so batch progress and
            // optional Stage 5 logging report one continuous elapsed duration.
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Stage 3 \u2014 Starting full corpus run (batch size={BatchSize}, resume={Resume}, maxBatches={MaxBatches})",
                batchSize, resumeFromId.HasValue ? resumeFromId.Value : "fresh", maxBatches?.ToString() ?? "all");

            // Fresh runs clear the flattened table; resume runs preserve existing rows
            // so the next batch can append without destroying prior completed work.
            if (!resumeFromId.HasValue)
            {
                await TruncateAsync(ct);
            }

            // Delegate only the caller-specific batch work; the shared loop owns
            // ordering, dedup, progress, cancellation, and cumulative totals.
            var loopResult = await executeFullCorpusBatchLoopAsync(
                batchSize,
                maxBatches,
                disableBioequivalentDedup,
                rowProgress,
                progress,
                stopwatch,
                includeSkipCounts: false,
                async (filter, innerProgress, token) => new FullCorpusBatchOutcome
                {
                    ObservationsWritten = await ProcessBatchAsync(filter, innerProgress, token)
                },
                ct);

            _logger.LogInformation("Stage 3 \u2014 Complete: {Total} total observations in {Batches} batches ({Elapsed})",
                loopResult.TotalObservations, loopResult.BatchCount, stopwatch.Elapsed);

            // Stage 5 is optional for environments that do not register the AE
            // denormalizer; when present it must run after Stage 3 has persisted data.
            if (_aeDenormalizer != null)
            {
                _logger.LogInformation("Stage 5 \u2014 Starting AdverseEvent denormalization");
                var aeRows = await _aeDenormalizer.PopulateAsync(ct: ct);
                _logger.LogInformation("Stage 5 \u2014 Complete: {Rows} AE rows denormalized ({Elapsed})",
                    aeRows, stopwatch.Elapsed);
            }

            stopwatch.Stop();

            return loopResult.TotalObservations;

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

            // Reconstruct the exact source table so diagnostics can exercise the
            // parser without a full corpus batch or database write.
            var table = await _reconstructionService.ReconstructTableAsync(textTableId, ct);

            // Missing source data is not exceptional for ad hoc diagnostics; return
            // an empty result and leave the caller with a clear warning.
            if (table == null)
            {
                _logger.LogDebug("No table found for TextTableID={Id}", textTableId);
                return new List<ParsedObservation>();
            }

            // Route before parsing so the single-table path uses the same parser
            // selection and skip rules as the full batch pipeline.
            var (category, parser) = _router.Route(table);

            // A SKIP decision has no safe parser to invoke; the expected outcome is
            // an empty observation list rather than a partial or guessed parse.
            if (category == TableCategory.SKIP || parser == null)
            {
                _logger.LogDebug("TextTableID={Id} categorized as {Category} \u2014 no parser", textTableId, category);
                return new List<ParsedObservation>();
            }

            clearParserDiagnostics(parser);
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
        /// Full corpus run with Stage 4 validation: truncate, batch loop, validate, then report.
        /// </summary>
        /// <param name="batchSize">TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from.</param>
        /// <param name="maxBatches">Optional maximum number of batches to process.</param>
        /// <param name="rowProgress">Optional per-table progress callback for each batch.</param>
        /// <param name="disableBioequivalentDedup">When true, bypass Stage 0 bioequivalent dedup.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validation report with coverage metrics and issues.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="IBatchValidationService"/> was not provided in the constructor.
        /// </exception>
        public async Task<BatchValidationReport> ProcessAllWithValidationAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            IProgress<TransformBatchProgress>? rowProgress = null,
            bool disableBioequivalentDedup = false,
            CancellationToken ct = default)
        {
            #region implementation

            // Validation mode requires the Stage 4 service because the final report
            // is generated from the persisted batch output and skip diagnostics.
            if (_batchValidator == null)
            {
                throw new InvalidOperationException(
                    "IBatchValidationService was not provided. Use ProcessAllAsync for runs without validation.");
            }

            // Keep one stopwatch across Stage 3, Stage 4, and optional Stage 5 so
            // log timings describe the whole validation run.
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Stage 3+4 \u2014 Starting full corpus run with validation (batch size={BatchSize}, resume={Resume}, maxBatches={MaxBatches})",
                batchSize, resumeFromId.HasValue ? resumeFromId.Value : "fresh", maxBatches?.ToString() ?? "all");

            // Only truncate for clean runs; resume mode intentionally leaves prior
            // completed batch rows in place for validation continuity.
            if (!resumeFromId.HasValue)
            {
                await TruncateAsync(ct);
            }

            // Run the same full-corpus template as ProcessAllAsync, but request skip
            // counts and preserve the per-table reasons required by the validator.
            var loopResult = await executeFullCorpusBatchLoopAsync(
                batchSize,
                maxBatches,
                disableBioequivalentDedup,
                rowProgress,
                progress,
                stopwatch,
                includeSkipCounts: true,
                async (filter, innerProgress, token) =>
                {
                    // Stage-visible batch processing is required here because skip
                    // reasons are populated on BatchStageResult, not the thin API.
                    var stageResult = await ProcessBatchWithStagesAsync(filter, innerProgress, token);
                    return new FullCorpusBatchOutcome
                    {
                        ObservationsWritten = stageResult.ObservationsWritten,
                        SkipReasons = new Dictionary<int, string>(stageResult.SkipReasons)
                    };
                },
                ct);

            _logger.LogInformation("Stage 3 \u2014 Complete: {Total} total observations in {Batches} batches ({Elapsed}). Starting validation...",
                loopResult.TotalObservations, loopResult.BatchCount, stopwatch.Elapsed);

            // Generate coverage/issue metrics against the rows just written, pairing
            // them with the skip reasons accumulated during routing.
            var report = await _batchValidator.GenerateReportFromDatabaseAsync(loopResult.SkipReasons, ct: ct);

            // Cross-version concordance is a separate validator pass; attach it to
            // the same report so callers receive one Stage 4 artifact.
            var discrepancies = await _batchValidator.CheckCrossVersionConcordanceAsync(ct);
            report.CrossVersionDiscrepancies = discrepancies;

            _logger.LogInformation("Stage 4 \u2014 Validation complete. {Discrepancies} cross-version discrepancies ({Elapsed})",
                discrepancies.Count, stopwatch.Elapsed);

            // Optional Stage 5 consumes the standardized table after validation has
            // completed, producing the AE-specific flattened table as a downstream view.
            if (_aeDenormalizer != null)
            {
                _logger.LogInformation("Stage 5 \u2014 Starting AdverseEvent denormalization");
                var aeRows = await _aeDenormalizer.PopulateAsync(ct: ct);
                _logger.LogInformation("Stage 5 \u2014 Complete: {Rows} AE rows denormalized ({Elapsed})",
                    aeRows, stopwatch.Elapsed);
            }

            stopwatch.Stop();

            return report;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables with full stage visibility, capturing intermediate results at each stage boundary.
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

            // Accumulate every intermediate artifact for diagnostics; each stage
            // below mutates this result rather than returning scattered side data.
            var result = new BatchStageResult();

            await ensureServicesInitializedAsync(ct);

            // Helper to fire intra-batch progress reports. Expected outcome:
            // callers see a stable operation name, percent, and table counts even
            // though each internal stage computes progress differently.
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

            // Materialize tables before sorting so the rest of the batch has a
            // stable, inspectable source collection.
            var tables = await _reconstructionService.ReconstructTablesAsync(filter, ct);

            // Sort by UNII so all observations for the same product flow into ML scoring together.
            // ReconstructTablesAsync iterates a Dictionary<TextTableID, ...> whose iteration order
            // follows hash buckets, not the UNII-ordered cell retrieval. Sorting here restores the
            // UNII walk order established by GetDocumentGuidsOrderedByUniiAsync, ensuring the flat
            // table writes and training accumulation reflect product-clustered data.
            tables = tables
                .OrderBy(t => t.UNII ?? string.Empty)
                .ThenBy(t => t.TextTableID)
                .ToList();

            result.ReconstructedTables = tables;

            // Stage 3: Route + Parse (0% → 20%)
            // Keep route/parse side effects together because this stage owns routing
            // decisions, parser diagnostics, skip reasons, and raw observations.
            var (allObservations, tablesProcessed, tableCount) = routeAndParseTables(tables, result, reportProgress, ct);
            result.PreCorrectionObservations = allObservations;

            // Stage 3.25: Column standardization (deterministic, pre-AI)
            reportProgress("Column standardization...", 21, tablesProcessed, tableCount);

            // Deterministic standardization runs before any AI stage so later
            // correction services receive the most normalized parser output possible.
            allObservations = runColumnStandardization(allObservations);

            // Stage 3.25 quality gate (opt-in): drop rows missing both ArmN and PrimaryValue
            // When enabled, this gate prevents downstream correction work on rows
            // that cannot be recovered for meta-analysis.
            allObservations = dropIncompleteRows(allObservations);

            // Stage 3.35 (R13): Pre-ML PK filter — drop non-analyzable PK rows
            // before ML scoring / Claude correction / DB write. Other categories
            // pass through unchanged (troubleshooting data retained until each
            // category's filter contract is audited individually).
            reportProgress("Pre-ML PK filter...", 22, tablesProcessed, tableCount);

            // First PK analyzability pass keeps the ML input free of PK rows that
            // have no parameter name, numeric value type, or primary value.
            allObservations = dropNonAnalyzablePkRows(allObservations);

            // Stage 3.4: ML.NET correction and anomaly scoring
            reportProgress("ML.NET scoring...", 23, tablesProcessed, tableCount);

            // ML correction may adjust categories or flags in place; the returned
            // list is treated as the canonical stream for subsequent filters.
            allObservations = runMlCorrection(allObservations);

            // Stage 3.45 (R15.3): Post-ML PK filter re-run. Defense-in-depth against
            // the known R9 ML.NET classifier bug where non-PK rows (e.g., HSV
            // mutation tables) are erroneously CATEGORY_CORRECTED to PK. Those rows
            // pass through Stage 3.35 untouched (their pre-ML tableCategory was
            // non-PK), then ML flips their inner tableCategory to PK. Re-applying
            // the same analyzability contract here catches them before Claude
            // correction or DB write. Pre-ML 3.35 stays in place so the ML training
            // input is clean.
            reportProgress("Post-ML PK filter re-run...", 24, tablesProcessed, tableCount);

            // Second PK analyzability pass catches rows that ML reclassified into
            // PK after they bypassed the pre-ML PK-only filter.
            allObservations = dropNonAnalyzablePkRows(allObservations);

            // Stage 3.5: Claude AI Correction (25% → 95%)
            reportProgress("Claude AI correction...", 25, tablesProcessed, tableCount);

            // Claude receives both observations and original table context; the
            // expected outcome is corrected fields plus correction-count diagnostics.
            allObservations = await runClaudeCorrectionAsync(allObservations, tables, reportProgress, tablesProcessed, tableCount, result, ct);

            // Stage 3.6: Post-processing extraction (catch units/N= values Claude corrected into extractable form)
            reportProgress("Post-processing extraction...", 95.5, tablesProcessed, tableCount);

            // Re-run deterministic extraction after AI correction so corrected text
            // can yield units, sample sizes, and other structured fields.
            allObservations = runPostProcessExtraction(allObservations);

            result.PostCorrectionObservations = allObservations;

            // DB Write (96% → 100%)
            reportProgress("Writing to database...", 96, tablesProcessed, tableCount);

            // Persist only after all correction/filter stages finish; failures here
            // mark the batch as unwritten without corrupting earlier diagnostics.
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

            // Initialize deterministic standardization lazily so single-table debug
            // paths do not pay startup cost until the first batch needs it.
            if (_columnStandardizer != null && !_columnStdInitialized)
            {
                await _columnStandardizer.InitializeAsync(ct);
                _columnStdInitialized = true;
            }

            // Initialize ML scoring lazily for the same reason, and set the flag only
            // after successful initialization so a later batch can retry after faults.
            if (_qcNetCorrectionService != null && !_qcNetInitialized)
            {
                await _qcNetCorrectionService.InitializeAsync(ct);
                _qcNetInitialized = true;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the Stage 0 bioequivalent-label dedup filter over the UNII-ordered
        /// document list. Returns the input unchanged when the service is not
        /// registered or when <paramref name="disable"/> is true.
        /// </summary>
        /// <param name="orderedGuids">Documents in UNII walk order from Stage 0.</param>
        /// <param name="disable">When true, bypass dedup and return the input list
        /// (preserving materialized-List semantics expected by the downstream batch loop).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The filtered or unfiltered list, always materialized as a <see cref="List{Guid}"/>
        /// so the caller can safely index into it.</returns>
        /// <seealso cref="IBioequivalentLabelDedupService"/>
        private async Task<List<Guid>> applyBioequivalentDedupAsync(
            List<Guid> orderedGuids,
            bool disable,
            CancellationToken ct)
        {
            #region implementation

            // Missing service means this deployment has no Stage 0 dedup contract;
            // preserve the original ordered list unchanged.
            if (_bioequivalentDedup == null)
            {
                _logger.LogDebug("Bioequivalent dedup service not registered — skipping filter");
                return orderedGuids;
            }
            // Explicit disable is a smoke-test escape hatch: keep ordering and count
            // unchanged while still exercising the same downstream loop.
            if (disable)
            {
                _logger.LogInformation("Bioequivalent dedup disabled by caller — processing {Count} documents unfiltered", orderedGuids.Count);
                return orderedGuids;
            }

            // Execute dedup before batching so all later progress counts describe
            // the actual documents that will be processed.
            var result = await _bioequivalentDedup.DeduplicateAsync(orderedGuids, options: null, ct);

            _logger.LogInformation(
                "Bioequivalent dedup: kept {Kept}/{Input} documents ({Groups} groups, {Unclass} unclassifiable dropped)",
                result.KeptDocumentGuids.Count,
                orderedGuids.Count,
                result.GroupCount,
                result.UnclassifiableCount);

            // The dedup service already preserves input order; materialize to a List
            // so the batch loop can Skip/Take efficiently.
            return result.KeptDocumentGuids.ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the shared UNII-ordered full-corpus batching template used by both public full-run entry points.
        /// </summary>
        /// <param name="batchSize">Document GUIDs per batch.</param>
        /// <param name="maxBatches">Optional maximum number of batches to process.</param>
        /// <param name="disableBioequivalentDedup">When true, bypass the Stage 0 dedup filter.</param>
        /// <param name="rowProgress">Optional per-table progress callback.</param>
        /// <param name="progress">Optional per-batch progress callback.</param>
        /// <param name="stopwatch">Stopwatch created by the public caller.</param>
        /// <param name="includeSkipCounts">When true, batch progress and logs include skip counts.</param>
        /// <param name="dispatchBatchAsync">Caller-specific batch dispatcher.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Aggregate observations, dispatched-batch count, and skip reasons.</returns>
        /// <seealso cref="ProcessAllAsync"/>
        /// <seealso cref="ProcessAllWithValidationAsync"/>
        private async Task<FullCorpusBatchLoopResult> executeFullCorpusBatchLoopAsync(
            int batchSize,
            int? maxBatches,
            bool disableBioequivalentDedup,
            IProgress<TransformBatchProgress>? rowProgress,
            IProgress<TransformBatchProgress>? progress,
            Stopwatch stopwatch,
            bool includeSkipCounts,
            Func<TableCellContextFilter, IProgress<TransformBatchProgress>?, CancellationToken, Task<FullCorpusBatchOutcome>> dispatchBatchAsync,
            CancellationToken ct)
        {
            #region implementation

            // Start from the UNII walk order because downstream ML accumulation and
            // output writes expect product-clustered batches.
            var orderedGuidsRaw = await _cellContextService.GetDocumentGuidsOrderedByUniiAsync(ct);
            _logger.LogInformation("UNII-ordered document batch: {Count} documents", orderedGuidsRaw.Count);

            // Apply optional Stage 0 dedup before batch math so total batch counts
            // reflect the final document set.
            var orderedGuids = await applyBioequivalentDedupAsync(orderedGuidsRaw, disableBioequivalentDedup, ct);

            // Compute the visible batch count once so progress reporters can present
            // a stable denominator even when the final batch is partial.
            var totalBatches = (int)Math.Ceiling((double)orderedGuids.Count / batchSize);

            // A max-batch cutoff is a deliberate partial run; clamp the progress
            // denominator to the number of batches that will actually dispatch.
            if (maxBatches.HasValue)
            {
                totalBatches = Math.Min(totalBatches, maxBatches.Value);
            }

            // Running totals are owned by the template so parse-only and validation
            // full runs cannot disagree on cumulative progress semantics.
            var totalObservations = 0;
            var batchNumber = 0;
            var skipReasons = new Dictionary<int, string>();

            // Walk the materialized GUID list in fixed-size slices; the loop index
            // is intentionally document-based rather than table-based.
            for (int i = 0; i < orderedGuids.Count; i += batchSize)
            {
                // Honor cancellation between batches so a partially completed batch
                // remains coherent and the caller can resume from the next unit of work.
                ct.ThrowIfCancellationRequested();

                // Batch numbers are one-based for user-facing progress and logs.
                batchNumber++;

                // Stop before dispatching any batch beyond the requested smoke-test
                // or resume limit.
                if (maxBatches.HasValue && batchNumber > maxBatches.Value)
                {
                    break;
                }

                // Slice the current document set and wrap it in the filter contract
                // expected by reconstruction services.
                var guids = orderedGuids.Skip(i).Take(batchSize).ToList();
                var filter = new TableCellContextFilter
                {
                    DocumentGUIDs = guids
                };

                // Capture pre-dispatch values so nested row progress reports show
                // stable cumulative totals for the batch that is currently running.
                var capturedBatchNumber = batchNumber;
                var capturedTotalObs = totalObservations;

                // Wrap row progress only when the caller supplied a sink; the wrapper
                // enriches intra-batch reports with batch number, totals, and elapsed time.
                IProgress<TransformBatchProgress>? innerProgress = rowProgress != null
                    ? new Helpers.SynchronousProgress<TransformBatchProgress>(p =>
                    {
                        p.BatchNumber = capturedBatchNumber;
                        p.TotalBatches = totalBatches;
                        p.CumulativeObservationCount = capturedTotalObs + p.BatchObservationCount;
                        p.Elapsed = stopwatch.Elapsed;
                        rowProgress.Report(p);
                    })
                    : null;

                // Dispatch is the one caller-specific step; expected outcome is a
                // row count and optional skip diagnostics for this batch.
                var outcome = await dispatchBatchAsync(filter, innerProgress, ct);
                totalObservations += outcome.ObservationsWritten;

                // Last writer wins is intentional: a table can only appear in one GUID
                // batch, so duplicate keys would indicate a later, more specific reason.
                foreach (var kvp in outcome.SkipReasons)
                {
                    skipReasons[kvp.Key] = kvp.Value;
                }

                // Validation runs need skip counts in logs and progress; parse-only
                // runs preserve the leaner legacy progress surface.
                if (includeSkipCounts)
                {
                    _logger.LogInformation(
                        "Batch {Batch}/{TotalBatches}: {DocCount} documents, {BatchCount} observations, {Skipped} skipped, {Total} cumulative",
                        batchNumber, totalBatches, guids.Count, outcome.ObservationsWritten, outcome.SkipReasons.Count, totalObservations);
                }
                else
                {
                    _logger.LogInformation(
                        "Batch {Batch}/{TotalBatches}: {DocCount} documents, {BatchCount} observations, {Total} cumulative",
                        batchNumber, totalBatches, guids.Count, outcome.ObservationsWritten, totalObservations);
                }

                // Report after the batch commits its outcome so persisted totals and
                // UI totals describe the same completed work.
                progress?.Report(new TransformBatchProgress
                {
                    BatchNumber = batchNumber,
                    TotalBatches = totalBatches,
                    BatchObservationCount = outcome.ObservationsWritten,
                    CumulativeObservationCount = totalObservations,
                    TablesSkippedThisBatch = includeSkipCounts ? outcome.SkipReasons.Count : 0,
                    Elapsed = stopwatch.Elapsed
                });
            }

            return new FullCorpusBatchLoopResult
            {
                TotalObservations = totalObservations,
                BatchCount = batchNumber,
                SkipReasons = skipReasons
            };

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

            // Collect every successfully parsed observation in source-table order so
            // downstream correction stages receive one contiguous batch stream.
            var allObservations = new List<ParsedObservation>();

            // Track both the denominator and completed count for progress mapping
            // into the 0-20 percent parse segment.
            var tableCount = tables.Count;
            var tablesProcessed = 0;

            // Route and parse each reconstructed table independently so a failed
            // table is skipped without aborting the rest of the batch.
            foreach (var table in tables)
            {
                // Allow cancellation between tables, preserving table-level atomicity
                // for any parser currently executing.
                ct.ThrowIfCancellationRequested();

                // Route immediately before parse so router diagnostics describe the
                // exact table that will be parsed or skipped.
                var (category, parser) = _router.Route(table);

                // Build the decision before branching so all outcomes, including
                // skips and exceptions, record the same routing metadata.
                var decision = new TableRoutingDecision
                {
                    TextTableID = table.TextTableID ?? 0,
                    Category = category,
                    ParserName = parser?.GetType().Name,
                    RouteReason = getRouterRouteReason()
                };

                // A SKIP category or missing parser is a deliberate routing outcome;
                // record it as a skipped table and advance progress.
                if (category == TableCategory.SKIP || parser == null)
                {
                    decision.ObservationCount = 0;
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] =
                            decision.RouteReason ?? $"SKIP:{category}";
                    }

                    _logger.LogDebug("Skipping TextTableID={Id} — category={Category}",
                        table.TextTableID, category);

                    tablesProcessed++;

                    // Map completed table count into the route/parse segment of the
                    // batch progress bar; zero tables safely reports zero percent.
                    var tablePct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                    reportProgress("Parsing tables...", tablePct, tablesProcessed, tableCount);
                    continue;
                }

                try
                {
                    // Clear diagnostics immediately before parser invocation so
                    // suppression audits only describe this table.
                    clearParserDiagnostics(parser);

                    // Parse returns candidate observations; diagnostics capture rows
                    // that were intentionally suppressed before emission.
                    var observations = parser.Parse(table);
                    var suppressedRows = snapshotParserDiagnostics(parser);

                    // Persist routing diagnostics whether the parser emitted rows or
                    // only suppression records.
                    decision.SuppressedRowCount = suppressedRows.Count;
                    result.SuppressedRows.AddRange(suppressedRows);
                    decision.ObservationCount = observations.Count;
                    result.RoutingDecisions.Add(decision);

                    // Empty parser output is tracked separately from router SKIP so
                    // validation can distinguish "not applicable" from "no rows emitted."
                    if (observations.Count == 0)
                    {
                        if (table.TextTableID.HasValue)
                        {
                            result.SkipReasons[table.TextTableID.Value] = $"EMPTY:{parser.GetType().Name}";
                        }

                        tablesProcessed++;

                        // Advance progress before continuing so empty tables do not
                        // make the UI appear stalled.
                        var tablePct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                        reportProgress("Parsing tables...", tablePct, tablesProcessed, tableCount);
                        continue;
                    }

                    // Successful parse output joins the batch stream for deterministic
                    // standardization, filtering, AI correction, and persistence.
                    allObservations.AddRange(observations);
                }
                catch (TableParseException tpx)
                {
                    // Parser-raised table faults carry structured row/parser context;
                    // convert them into a skip reason while preserving diagnostics.
                    decision.ObservationCount = 0;
                    var suppressedRows = snapshotParserDiagnostics(parser);
                    decision.SuppressedRowCount = suppressedRows.Count;
                    result.SuppressedRows.AddRange(suppressedRows);
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] =
                            $"ERROR:{tpx.ParserName}:Row{tpx.RowSequence}";
                    }

                    _logger.LogDebug(tpx,
                        "Table-level fault: TextTableID={Id}, Row={Row}, Parser={Parser} — entire table skipped",
                        tpx.TextTableID, tpx.RowSequence, tpx.ParserName);
                }
                catch (Exception ex)
                {
                    // Unexpected parser faults are still table-scoped; record a generic
                    // parser skip reason and continue with the next table.
                    decision.ObservationCount = 0;
                    var suppressedRows = snapshotParserDiagnostics(parser);
                    decision.SuppressedRowCount = suppressedRows.Count;
                    result.SuppressedRows.AddRange(suppressedRows);
                    result.RoutingDecisions.Add(decision);

                    if (table.TextTableID.HasValue)
                    {
                        result.SkipReasons[table.TextTableID.Value] = $"ERROR:{parser.GetType().Name}";
                    }

                    _logger.LogDebug(ex,
                        "Failed to parse TextTableID={Id} with {Parser} — skipping",
                        table.TextTableID, parser.GetType().Name);
                }

                tablesProcessed++;

                // Every non-continued path reaches this progress update, including
                // table-level faults, so completed count remains accurate.
                var pct = tableCount > 0 ? (double)tablesProcessed / tableCount * 20.0 : 0;
                reportProgress("Parsing tables...", pct, tablesProcessed, tableCount);
            }

            return (allObservations, tablesProcessed, tableCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Clears optional parser diagnostics before invoking a parser.
        /// </summary>
        /// <param name="parser">Parser about to run.</param>
        /// <seealso cref="ITableParserDiagnostics"/>
        private static void clearParserDiagnostics(ITableParser parser)
        {
            #region implementation

            if (parser is ITableParserDiagnostics diagnostics)
                diagnostics.ClearDiagnostics();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Copies optional parser diagnostics after a parser completes or faults.
        /// </summary>
        /// <param name="parser">Parser that just ran.</param>
        /// <returns>Suppressed-row audit records captured by the parser.</returns>
        /// <seealso cref="TableSuppressionAuditRecord"/>
        private static List<TableSuppressionAuditRecord> snapshotParserDiagnostics(ITableParser parser)
        {
            #region implementation

            return parser is ITableParserDiagnostics diagnostics
                ? diagnostics.SuppressedRows.ToList()
                : new List<TableSuppressionAuditRecord>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the most recent router diagnostic reason, when the router exposes
        /// one.
        /// </summary>
        /// <returns>Router skip or downgrade reason, or null.</returns>
        /// <seealso cref="ITableParserRouterDiagnostics"/>
        private string? getRouterRouteReason()
        {
            #region implementation

            return _router is ITableParserRouterDiagnostics diagnostics
                ? diagnostics.LastRouteReason
                : null;

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

            // No service or no rows means there is nothing to standardize; return
            // the current list so downstream stages can remain null-free.
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

            // Keep the original count so logs can report how much data the gate removed.
            var originalCount = observations.Count;

            // Keep only rows where BOTH ArmN AND PrimaryValue are populated.
            // Cross-product meta-analysis requires both fields — a row missing either
            // cannot participate downstream and is unrecoverable.
            var surviving = observations
                .Where(o => o.ArmN != null && o.PrimaryValue != null)
                .ToList();

            // Compute the removal count after materialization so the returned list and
            // log message describe the same filtered population.
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
        /// R13 (Stage 3.35) + R15.3 (Stage 3.45): Drops PK observations that cannot
        /// be analyzed — either because they lack a canonical
        /// <see cref="ParsedObservation.ParameterName"/>, carry a text-typed
        /// <see cref="ParsedObservation.PrimaryValueType"/>, or have no populated
        /// <see cref="ParsedObservation.PrimaryValue"/>. PK analysis requires all
        /// three: a named parameter AND a numeric value type AND an extractable
        /// primary value. Rows missing any of the three pollute ML training data
        /// and downstream compliance metrics.
        /// </summary>
        /// <remarks>
        /// ## Scope
        /// PK-only. Observations with <c>TableCategory != "PK"</c> pass through
        /// unchanged. Other categories (ADVERSE_EVENT, EFFICACY, DRUG_INTERACTION)
        /// retain their rows for troubleshooting until each category's filter
        /// contract is audited individually.
        ///
        /// ## Ordering — called TWICE
        /// - **Stage 3.35 (pre-ML)**: runs after R11/R12/R15.1 parser rescues so
        ///   rescued rows survive; runs before ML correction so the ML training
        ///   input is clean of Text-typed / null-PrimaryValue PK rows.
        /// - **Stage 3.45 (post-ML, R15.3)**: defense-in-depth against the known
        ///   R9 ML.NET classifier bug where non-PK rows (HSV mutation tables,
        ///   AE observations, etc.) are erroneously CATEGORY_CORRECTED to PK.
        ///   The pre-ML pass cannot catch these because their pre-ML
        ///   <c>TableCategory</c> was non-PK.
        ///
        /// ## Filter condition (R13 + R15.2)
        /// Drop if: <c>TableCategory == "PK"</c> AND (<c>ParameterName IS NULL</c>
        /// OR <c>PrimaryValueType == "Text"</c> OR <c>PrimaryValue IS NULL</c>).
        /// The <c>PrimaryValue</c> null check catches ND/NA/dash rows parsed as
        /// <c>ParseRule="empty_or_na"</c> that have a non-Text PrimaryValueType
        /// but no actual numeric content.
        ///
        /// ## Unconditional
        /// Unlike <see cref="dropIncompleteRows"/>, this filter is not gated by a
        /// config flag — it is a hard contract: PK without a parameter name,
        /// numeric value type, and extractable value has no analytical utility.
        /// </remarks>
        /// <param name="observations">Observations to filter.</param>
        /// <returns>
        /// A new list containing the surviving observations. Non-PK observations
        /// are always retained; PK observations are retained only when all three
        /// analyzability criteria are met.
        /// </returns>
        /// <seealso cref="dropIncompleteRows"/>
        /// <seealso cref="runMlCorrection"/>
        /// <seealso cref="ProcessBatchWithStagesAsync"/>
        private List<ParsedObservation> dropNonAnalyzablePkRows(List<ParsedObservation> observations)
        {
            #region implementation

            // Empty input is a no-op and returns the same list for callers that rely
            // on reference stability in diagnostic paths.
            if (observations.Count == 0)
            {
                return observations;
            }

            // Capture the starting count so the filter can log the exact drop volume.
            var before = observations.Count;

            // Keep rule: non-PK rows always pass; PK rows require:
            //   - ParameterName populated
            //   - PrimaryValueType != "Text"
            //   - PrimaryValue populated (R15.2 — catches ND/NA/dash/empty_or_na rows
            //     that have non-Text PrimaryValueType but no analyzable value)
            // Case-insensitive category match to tolerate caller variance ("pk" / "PK").
            var kept = observations
                .Where(o =>
                    !string.Equals(o.TableCategory, "PK", StringComparison.OrdinalIgnoreCase) ||
                    (
                        !string.IsNullOrWhiteSpace(o.ParameterName) &&
                        !string.Equals(o.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase) &&
                        o.PrimaryValue.HasValue
                    ))
                .ToList();

            // Difference is computed after materialization to avoid re-enumerating the
            // LINQ predicate and to keep the logged after-count precise.
            var dropped = before - kept.Count;
            if (dropped > 0)
            {
                _logger.LogInformation(
                    "R13/R15 PK analyzability filter dropped {Dropped} non-analyzable rows ({Before} → {After})",
                    dropped, before, kept.Count);
            }

            return kept;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.4: Runs ML.NET correction and anomaly scoring on all observations.
        /// No-ops if the ML correction service is null or there are no observations.
        /// </summary>
        /// <param name="observations">Observations to score and correct.</param>
        /// <returns>The corrected observations (same list, modified in-place).</returns>
        /// <seealso cref="IQCNetCorrectionService.ScoreAndCorrect"/>
        private List<ParsedObservation> runMlCorrection(List<ParsedObservation> observations)
        {
            #region implementation

            // ML is optional and list-preserving; no configured service or no rows
            // means downstream stages receive the unchanged observation stream.
            if (_qcNetCorrectionService != null && observations.Count > 0)
            {
                observations = _qcNetCorrectionService.ScoreAndCorrect(observations);
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

            // Correction is optional; a missing service or empty batch means there
            // is no work and no correction-count delta to record.
            if (_correctionService == null || observations.Count == 0)
                return observations;

            // Forwarding progress: map correction service's 0–100 into orchestrator's 25–95 range
            var claudeProgress = new Helpers.SynchronousProgress<TransformBatchProgress>(p =>
            {
                // Scale Claude's local percent into the orchestrator's Stage 3.5
                // band so the overall batch progress remains monotonic.
                var scaledPct = 25.0 + (p.IntraBatchPercent / 100.0) * 70.0;
                reportProgress(p.CurrentOperation ?? "Claude AI correction...", scaledPct, tablesProcessed, tableCount);
            });

            // Snapshot flags before Claude so the result can count changed rows
            // without trying to infer correction details from service internals.
            var preCorrectionFlags = observations
                .Select(o => o.ValidationFlags)
                .ToList();
            result.PreClaudeValidationFlags =
                MedRecProImportClass.Helpers.ObservationFlagSnapshotBuilder.Capture(observations);

            // Build table lookup so each TextTableID group gets its original table context
            var tableLookup = tables
                .Where(t => t.TextTableID.HasValue)
                .ToDictionary(t => t.TextTableID!.Value);

            // Correct in batch with source-table context so guardrails can compare
            // model suggestions against the original table shape.
            observations = await _correctionService.CorrectBatchAsync(observations, originalTables: tableLookup, progress: claudeProgress, ct: ct);

            // Count corrections by checking ValidationFlags changes
            // Index alignment is safe because CorrectBatchAsync returns the same
            // observation stream shape rather than inserting/removing rows.
            result.CorrectionCount = observations
                .Select((o, i) => o.ValidationFlags != preCorrectionFlags[i] ? 1 : 0)
                .Sum();

            // Stage 3.4 feedback: feed Claude corrections back to ML as ground truth
            // This branch is optional, but when present it lets later ML runs learn
            // from accepted Claude corrections.
            if (_qcNetCorrectionService != null)
            {
                await _qcNetCorrectionService.FeedClaudeCorrectedBatchAsync(observations, ct);
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

            // Empty batches are valid after filters; do not touch the DbContext or
            // report a write count.
            if (observations.Count == 0)
                return;

            try
            {
                // Map DTOs to the EF entity only at the persistence boundary so
                // parsing and correction stages stay independent of DB column limits.
                var entities = observations.Select(mapToEntity).ToList();
                _dbContext.AddRange(entities);
                await _dbContext.SaveChangesAsync(ct);
                result.ObservationsWritten = entities.Count;

                // Prevent ChangeTracker accumulation across batches — the same DbContext instance
                // is reused for the entire run, so tracked entities from prior batches would
                // otherwise pin memory and force DetectChanges to walk an ever-growing list.
                _dbContext.ChangeTracker.Clear();
            }
            catch (OperationCanceledException)
            {
                // Cancellation should propagate, but the tracker is still cleared so
                // a resumed run does not inherit half-tracked entities.
                _dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception ex)
            {
                // Treat write failures as batch-scoped: clear tracked entities, log,
                // and leave ObservationsWritten at zero for the shared loop totals.
                _dbContext.ChangeTracker.Clear();
                _logger.LogDebug(ex,
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

            // Post-processing extraction is optional and only meaningful when there
            // are observations whose corrected text can yield additional fields.
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
        public (TableCategory category, string? parserName, List<ParsedObservation> observations, List<TableSuppressionAuditRecord> suppressedRows)
            RouteAndParseSingleTable(ReconstructedTable table)
        {
            #region implementation

            // Route through the same router as full batches so diagnostics match
            // production parser selection.
            var (category, parser) = _router.Route(table);

            // Diagnostic callers need to see explicit SKIP outcomes without forcing
            // a parser invocation that the router rejected.
            if (category == TableCategory.SKIP || parser == null)
            {
                _logger.LogDebug("TextTableID={Id} categorized as {Category} — no parser",
                    table.TextTableID, category);
                return (category, null, new List<ParsedObservation>(), new List<TableSuppressionAuditRecord>());
            }

            clearParserDiagnostics(parser);

            // Parse and immediately snapshot suppression diagnostics so callers can
            // inspect both emitted observations and intentionally suppressed rows.
            var observations = parser.Parse(table);
            var suppressedRows = snapshotParserDiagnostics(parser);
            return (category, parser.GetType().Name, observations, suppressedRows);

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

            // Correction diagnostics mirror production behavior: a missing service
            // or empty list leaves the supplied observations unchanged.
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
                Subpopulation = obs.Subpopulation.Truncate(SML_TEXT_LENGTH),
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
                ValidationFlags = obs.ValidationFlags.Truncate(MED_TEXT_LENGTH),
                UNII = obs.UNII.Truncate(MED_TEXT_LENGTH)
            };

            #endregion
        }

        #endregion Private Helpers

    }
}
