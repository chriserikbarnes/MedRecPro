namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Filter and batching options for the <see cref="TableCellContext"/> source query.
    /// Supports single-document, single-table, or batch-range filtering for scalable
    /// processing of the 250K+ label corpus.
    /// </summary>
    /// <remarks>
    /// ## Mutual Exclusivity
    /// <see cref="TextTableID"/> and the range parameters (<see cref="TextTableIdRangeStart"/>,
    /// <see cref="TextTableIdRangeEnd"/>) are mutually exclusive. Call <see cref="Validate"/>
    /// before passing to the service layer.
    ///
    /// ## Batch Processing Pattern
    /// <code>
    /// var (min, max) = await service.GetTextTableIdRangeAsync();
    /// for (int start = min; start &lt;= max; start += batchSize)
    /// {
    ///     var filter = new TableCellContextFilter
    ///     {
    ///         TextTableIdRangeStart = start,
    ///         TextTableIdRangeEnd = Math.Min(start + batchSize - 1, max)
    ///     };
    ///     filter.Validate();
    ///     var batch = await service.GetTableCellContextsAsync(filter);
    /// }
    /// </code>
    /// </remarks>
    /// <seealso cref="TableCellContext"/>
    public class TableCellContextFilter
    {
        /**************************************************************/
        /// <summary>
        /// Filter to a single document by its GUID.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Filter to a single table by its ID. Mutually exclusive with range parameters.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Batch range lower bound (inclusive). Must be paired with <see cref="TextTableIdRangeEnd"/>.
        /// Mutually exclusive with <see cref="TextTableID"/>.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public int? TextTableIdRangeStart { get; set; }

        /**************************************************************/
        /// <summary>
        /// Batch range upper bound (inclusive). Must be paired with <see cref="TextTableIdRangeStart"/>.
        /// Mutually exclusive with <see cref="TextTableID"/>.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public int? TextTableIdRangeEnd { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional row cap applied via <c>.Take(N)</c>. Must be greater than 0 if specified.
        /// </summary>
        public int? MaxRows { get; set; }

        /**************************************************************/
        /// <summary>
        /// Validates filter constraints: mutual exclusivity of TextTableID and range parameters,
        /// completeness of range pairs, and MaxRows positivity.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when TextTableID is combined with range parameters, when only one range bound
        /// is specified, or when MaxRows is not positive.
        /// </exception>
        /// <example>
        /// <code>
        /// var filter = new TableCellContextFilter { TextTableIdRangeStart = 1, TextTableIdRangeEnd = 1000 };
        /// filter.Validate(); // OK
        ///
        /// var bad = new TableCellContextFilter { TextTableID = 5, TextTableIdRangeStart = 1 };
        /// bad.Validate(); // throws ArgumentException
        /// </code>
        /// </example>
        public void Validate()
        {
            #region implementation
            if (TextTableID.HasValue && (TextTableIdRangeStart.HasValue || TextTableIdRangeEnd.HasValue))
            {
                throw new ArgumentException(
                    "TextTableID and range parameters (TextTableIdRangeStart/TextTableIdRangeEnd) are mutually exclusive.");
            }

            if (TextTableIdRangeStart.HasValue != TextTableIdRangeEnd.HasValue)
            {
                throw new ArgumentException(
                    "Both TextTableIdRangeStart and TextTableIdRangeEnd must be specified together.");
            }

            if (MaxRows.HasValue && MaxRows.Value <= 0)
            {
                throw new ArgumentException(
                    "MaxRows must be greater than 0.", nameof(MaxRows));
            }
            #endregion
        }
    }
}
