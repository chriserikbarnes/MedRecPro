using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LabelContainer = MedRecProImportClass.Models.Label;
using LabelViewContainer = MedRecProImportClass.Models.LabelView;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 1 implementation of the SPL Table Normalization pipeline.
    /// Assembles the flat 26-column <see cref="TableCellContext"/> projection by joining
    /// cell-level data with section and document context via EF Core LINQ.
    /// </summary>
    /// <remarks>
    /// ## Join Strategy
    /// Navigation properties exist for: TextTableCell → TextTableRow → TextTable → SectionTextContent.
    /// Explicit LINQ joins are required for:
    /// - SectionTextContent.SectionID → vw_SectionNavigation.SectionID
    /// - vw_SectionNavigation.DocumentID → Document.DocumentID
    ///
    /// ## Batch Processing
    /// The full corpus is 250K+ labels. Use <see cref="GetTextTableIdRangeAsync"/> to discover
    /// bounds and iterate with <see cref="TableCellContextFilter"/> range parameters.
    /// </remarks>
    /// <seealso cref="ITableCellContextService"/>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="TableCellContextFilter"/>
    public class TableCellContextService : ITableCellContextService
    {
        #region Private Fields

        private readonly ApplicationDbContext _context;
        private readonly ILogger<TableCellContextService> _logger;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="TableCellContextService"/> class.
        /// </summary>
        /// <param name="context">Database context for EF Core queries.</param>
        /// <param name="logger">Logger for tracking operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <seealso cref="ApplicationDbContext"/>
        public TableCellContextService(
            ApplicationDbContext context,
            ILogger<TableCellContextService> logger)
        {
            #region implementation
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Protected parameterless constructor for mocking purposes.
        /// </summary>
        protected TableCellContextService()
        {
            // Initialize with null! to satisfy compiler - only used for mocking
            _context = null!;
            _logger = null!;
        }

        #endregion

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Retrieves a flat list of <see cref="TableCellContext"/> records matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional filter. Call <see cref="TableCellContextFilter.Validate"/> before passing.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of projected <see cref="TableCellContext"/> DTOs.</returns>
        /// <seealso cref="buildQuery"/>
        public async Task<List<TableCellContext>> GetTableCellContextsAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            #region implementation
            _logger.LogDebug("GetTableCellContextsAsync called with filter: {Filter}",
                filter != null ? formatFilter(filter) : "null");

            var query = buildQuery(filter);
            var results = await query.ToListAsync(cancellationToken);

            _logger.LogDebug("GetTableCellContextsAsync returned {Count} rows", results.Count);
            return results;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves <see cref="TableCellContext"/> records grouped by TextTableID.
        /// </summary>
        /// <param name="filter">Optional filter. Call <see cref="TableCellContextFilter.Validate"/> before passing.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary keyed by TextTableID with lists of cells for each table.</returns>
        /// <seealso cref="GetTableCellContextsAsync"/>
        public async Task<Dictionary<int, List<TableCellContext>>> GetTableCellContextsGroupedByTableAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            #region implementation
            var results = await GetTableCellContextsAsync(filter, cancellationToken);

            var grouped = results
                .Where(x => x.TextTableID.HasValue)
                .GroupBy(x => x.TextTableID!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            _logger.LogDebug("GetTableCellContextsGroupedByTableAsync returned {Count} table groups", grouped.Count);
            return grouped;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the minimum and maximum TextTableID values in the TextTable entity set.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of (MinId, MaxId) from the TextTable table.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no TextTable records exist.</exception>
        /// <seealso cref="LabelContainer.TextTable"/>
        public async Task<(int MinId, int MaxId)> GetTextTableIdRangeAsync(
            CancellationToken cancellationToken = default)
        {
            #region implementation
            var textTables = _context.Set<LabelContainer.TextTable>().AsNoTracking();

            var minId = await textTables
                .Select(t => t.TextTableID!.Value)
                .MinAsync(cancellationToken);

            var maxId = await textTables
                .Select(t => t.TextTableID!.Value)
                .MaxAsync(cancellationToken);

            _logger.LogDebug("TextTableID range: {MinId} to {MaxId}", minId, maxId);
            return (minId, maxId);
            #endregion
        }

        #endregion

        #region Internal Methods

        /**************************************************************/
        /// <summary>
        /// Constructs the EF Core LINQ query that joins cell, row, table, section content,
        /// section navigation, and document data into a <see cref="TableCellContext"/> projection.
        /// </summary>
        /// <param name="filter">Optional filter to apply. Supports document, table, range, and row cap filtering.</param>
        /// <returns>An <see cref="IQueryable{TableCellContext}"/> that can be materialized or further composed.</returns>
        /// <remarks>
        /// Uses explicit joins for the SectionTextContent → vw_SectionNavigation → Document links
        /// where EF Core navigation properties do not exist. All entity sets use AsNoTracking()
        /// since this is a read-only projection.
        /// </remarks>
        /// <seealso cref="TableCellContext"/>
        /// <seealso cref="TableCellContextFilter"/>
        internal IQueryable<TableCellContext> buildQuery(TableCellContextFilter? filter)
        {
            #region implementation
            var query =
                from tc in _context.Set<LabelContainer.TextTableCell>().AsNoTracking()
                join tr in _context.Set<LabelContainer.TextTableRow>().AsNoTracking()
                    on tc.TextTableRowID equals tr.TextTableRowID
                join tt in _context.Set<LabelContainer.TextTable>().AsNoTracking()
                    on tr.TextTableID equals tt.TextTableID
                join stc in _context.Set<LabelContainer.SectionTextContent>().AsNoTracking()
                    on tt.SectionTextContentID equals stc.SectionTextContentID
                join sn in _context.Set<LabelViewContainer.SectionNavigation>().AsNoTracking()
                    on stc.SectionID equals sn.SectionID
                join d in _context.Set<LabelContainer.Document>().AsNoTracking()
                    on sn.DocumentID equals d.DocumentID
                select new TableCellContext
                {
                    // Cell
                    TextTableCellID = tc.TextTableCellID,
                    CellType = tc.CellType,
                    CellText = tc.CellText,
                    SequenceNumber = tc.SequenceNumber,
                    RowSpan = tc.RowSpan,
                    ColSpan = tc.ColSpan,
                    // Row
                    TextTableRowID = tr.TextTableRowID,
                    RowGroupType = tr.RowGroupType,
                    SequenceNumberTextTableRow = tr.SequenceNumber,
                    // Table
                    TextTableID = tt.TextTableID,
                    SectionTextContentID = tt.SectionTextContentID,
                    Caption = tt.Caption,
                    // Content
                    ContentType = stc.ContentType,
                    SequenceNumberSectionTextContent = stc.SequenceNumber,
                    ContentText = stc.ContentText,
                    // Document
                    DocumentGUID = d.DocumentGUID,
                    Title = d.Title,
                    VersionNumber = d.VersionNumber,
                    // Section Navigation
                    SectionGUID = sn.SectionGUID,
                    SectionCode = sn.SectionCode,
                    SectionType = sn.SectionType,
                    SectionTitle = sn.SectionTitle,
                    ParentSectionID = sn.ParentSectionID,
                    ParentSectionCode = sn.ParentSectionCode,
                    ParentSectionTitle = sn.ParentSectionTitle,
                    LabelerName = sn.LabelerName,
                };

            // Apply filters
            if (filter?.DocumentGUID != null)
                query = query.Where(x => x.DocumentGUID == filter.DocumentGUID);

            if (filter?.TextTableID != null)
                query = query.Where(x => x.TextTableID == filter.TextTableID);

            if (filter?.TextTableIdRangeStart != null && filter?.TextTableIdRangeEnd != null)
                query = query.Where(x =>
                    x.TextTableID >= filter.TextTableIdRangeStart &&
                    x.TextTableID <= filter.TextTableIdRangeEnd);

            if (filter?.MaxRows != null)
                query = query.Take(filter.MaxRows.Value);

            return query;
            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Formats a filter for debug logging.
        /// </summary>
        /// <param name="filter">The filter to format.</param>
        /// <returns>A human-readable string representation of the filter.</returns>
        private static string formatFilter(TableCellContextFilter filter)
        {
            #region implementation
            var parts = new List<string>();

            if (filter.DocumentGUID.HasValue)
                parts.Add($"DocumentGUID={filter.DocumentGUID}");
            if (filter.TextTableID.HasValue)
                parts.Add($"TextTableID={filter.TextTableID}");
            if (filter.TextTableIdRangeStart.HasValue)
                parts.Add($"Range=[{filter.TextTableIdRangeStart}-{filter.TextTableIdRangeEnd}]");
            if (filter.MaxRows.HasValue)
                parts.Add($"MaxRows={filter.MaxRows}");

            return parts.Count > 0 ? string.Join(", ", parts) : "(no filters)";
            #endregion
        }

        #endregion
    }
}
