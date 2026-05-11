using MedRecProImportClass.Helpers;
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
        /// Router skip or downgrade reason, when available.
        /// </summary>
        public string? RouteReason { get; init; }

        /**************************************************************/
        /// <summary>
        /// Post-Stage-3 observations. Already includes any Stage 3.5 (Claude) corrections applied.
        /// </summary>
        public IReadOnlyList<ParsedObservation> Observations { get; init; } = Array.Empty<ParsedObservation>();

        /**************************************************************/
        /// <summary>
        /// Structural row/cell suppressions captured during Stage 3. These are
        /// diagnostic metadata only and are not persisted as observations.
        /// </summary>
        /// <seealso cref="TableSuppressionAuditRecord"/>
        public IReadOnlyList<TableSuppressionAuditRecord> SuppressedRows { get; init; } =
            Array.Empty<TableSuppressionAuditRecord>();

        /**************************************************************/
        /// <summary>
        /// Snapshot of each observation's <see cref="ParsedObservation.ValidationFlags"/> taken
        /// immediately before Stage 3.5. Keyed by source table/row/cell plus occurrence
        /// index so observations emitted from the same source cell do not collide.
        /// Null when Claude was skipped.
        /// </summary>
        /// <seealso cref="ObservationFlagKey"/>
        public IReadOnlyDictionary<ObservationFlagKey, string?>? BeforeClaudeFlags { get; init; }

        /**************************************************************/
        /// <summary>
        /// True when Stage 3.5 was skipped (e.g. <c>--no-claude</c>). When true,
        /// <see cref="BeforeClaudeFlags"/> is ignored.
        /// </summary>
        public bool ClaudeSkipped { get; init; }
    }
}
