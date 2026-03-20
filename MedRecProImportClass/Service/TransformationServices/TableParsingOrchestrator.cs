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
        public TableParsingOrchestrator(
            ITableReconstructionService reconstructionService,
            ITableCellContextService cellContextService,
            ITableParserRouter router,
            ApplicationDbContext dbContext,
            ILogger<TableParsingOrchestrator> logger)
        {
            #region implementation

            _reconstructionService = reconstructionService;
            _cellContextService = cellContextService;
            _router = router;
            _dbContext = dbContext;
            _logger = logger;

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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to parse TextTableID={Id} with {Parser} — skipping",
                        table.TextTableID, parser.GetType().Name);
                }
            }

            if (totalObservations > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
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
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total observations written.</returns>
        public async Task<int> ProcessAllAsync(int batchSize = 1000, CancellationToken ct = default)
        {
            #region implementation

            _logger.LogInformation("Stage 3 — Starting full corpus run (batch size={BatchSize})", batchSize);

            await TruncateAsync(ct);

            var (minId, maxId) = await _cellContextService.GetTextTableIdRangeAsync(ct);
            _logger.LogInformation("TextTableID range: {Min} to {Max}", minId, maxId);

            var totalObservations = 0;
            var batchNumber = 0;

            for (int start = minId; start <= maxId; start += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                batchNumber++;

                var end = Math.Min(start + batchSize - 1, maxId);
                var filter = new TableCellContextFilter
                {
                    TextTableIdRangeStart = start,
                    TextTableIdRangeEnd = end
                };

                var batchCount = await ProcessBatchAsync(filter, ct);
                totalObservations += batchCount;

                _logger.LogInformation(
                    "Batch {Batch}: IDs [{Start}-{End}], {BatchCount} observations, {Total} cumulative",
                    batchNumber, start, end, batchCount, totalObservations);
            }

            _logger.LogInformation("Stage 3 — Complete: {Total} total observations in {Batches} batches",
                totalObservations, batchNumber);

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

        #endregion ITableParsingOrchestrator Implementation

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
