using MedRecProImportClass.Models;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Immutable snapshot of one table's standardization output, ready for markdown serialization.
    /// </summary>
    /// <remarks>
    /// Carries the same data the Spectre.Console <c>display*</c> methods consume:
    /// <list type="bullet">
    ///   <item><description>Stage 2 reconstructed table (metadata, pivot grid, footnotes)</description></item>
    ///   <item><description>Stage 3 routing decision (category + parser)</description></item>
    ///   <item><description>Stage 3 observations (parameter rows)</description></item>
    ///   <item><description>Stage 3.5 before-flags snapshot, used to diff AI_CORRECTED entries</description></item>
    /// </list>
    /// Records are pushed onto a <see cref="MarkdownReportSink"/> which serializes them sequentially
    /// via <see cref="TableStandardizationMarkdownWriter"/>.
    /// </remarks>
    /// <seealso cref="MarkdownReportSink"/>
    /// <seealso cref="TableStandardizationMarkdownWriter"/>
    public sealed record TableReportEntry
    {
        /**************************************************************/
        /// <summary>
        /// Stage 2 pivot output. Supplies metadata, column headers, body rows, and footnotes.
        /// </summary>
        public ReconstructedTable Table { get; init; } = new();

        /**************************************************************/
        /// <summary>
        /// Routing decision — which category the router chose for this table.
        /// </summary>
        public TableCategory Category { get; init; }

        /**************************************************************/
        /// <summary>
        /// Parser name that produced <see cref="Observations"/>. Null when the table was skipped.
        /// </summary>
        public string? ParserName { get; init; }

        /**************************************************************/
        /// <summary>
        /// Post-Stage-3 observations. Already includes any Stage 3.5 (Claude) corrections applied.
        /// </summary>
        public IReadOnlyList<ParsedObservation> Observations { get; init; } = Array.Empty<ParsedObservation>();

        /**************************************************************/
        /// <summary>
        /// Snapshot of each observation's <see cref="ParsedObservation.ValidationFlags"/> taken
        /// immediately before Stage 3.5. Keyed by <c>(SourceRowSeq * 10000 + SourceCellSeq)</c>
        /// matching the convention in <c>TableStandardizationService.displayClaudeCorrections</c>.
        /// Null when Claude was skipped.
        /// </summary>
        public IReadOnlyDictionary<int, string?>? BeforeClaudeFlags { get; init; }

        /**************************************************************/
        /// <summary>
        /// True when Stage 3.5 was skipped (e.g. <c>--no-claude</c>). When true,
        /// <see cref="BeforeClaudeFlags"/> is ignored.
        /// </summary>
        public bool ClaudeSkipped { get; init; }
    }
}
