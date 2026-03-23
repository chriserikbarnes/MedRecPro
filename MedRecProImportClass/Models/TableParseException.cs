namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Exception thrown when a row-level error occurs during table parsing,
    /// carrying structured context about which table, row, and parser were involved.
    /// </summary>
    /// <remarks>
    /// This exception enforces table-level atomicity: if any row fails during parsing,
    /// the entire table is skipped and no observations from that table are written to
    /// the database. The orchestrator catches this exception and logs the structured
    /// context for diagnostics.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.BaseTableParser"/>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.TableParsingOrchestrator"/>
    public class TableParseException : Exception
    {
        /**************************************************************/
        /// <summary>
        /// The TextTableID of the table that failed parsing.
        /// </summary>
        public int? TextTableID { get; }

        /**************************************************************/
        /// <summary>
        /// The sequence number of the row where the error occurred.
        /// </summary>
        public int? RowSequence { get; }

        /**************************************************************/
        /// <summary>
        /// The sequence number of the cell where the error occurred, if applicable.
        /// </summary>
        public int? CellSequence { get; }

        /**************************************************************/
        /// <summary>
        /// The name of the parser that was processing the table when the error occurred.
        /// </summary>
        public string? ParserName { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of <see cref="TableParseException"/> with structured
        /// context about the failure location.
        /// </summary>
        /// <param name="message">Description of the error.</param>
        /// <param name="textTableId">The TextTableID of the failing table.</param>
        /// <param name="rowSequence">The row sequence number where the error occurred.</param>
        /// <param name="cellSequence">The cell sequence number, if applicable.</param>
        /// <param name="parserName">The parser class name.</param>
        /// <param name="innerException">The original exception that caused the failure.</param>
        public TableParseException(
            string message,
            int? textTableId,
            int? rowSequence,
            string? parserName,
            int? cellSequence = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            #region implementation

            TextTableID = textTableId;
            RowSequence = rowSequence;
            CellSequence = cellSequence;
            ParserName = parserName;

            #endregion
        }
    }
}
