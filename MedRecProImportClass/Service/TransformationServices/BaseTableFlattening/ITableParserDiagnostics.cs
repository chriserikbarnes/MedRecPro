using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Optional diagnostic surface for parsers that suppress structural table rows
    /// before emitting observations.
    /// </summary>
    /// <remarks>
    /// Implementations expose suppressed-row audit records to orchestrator and report
    /// metadata without changing the primary <see cref="ITableParser.Parse"/> return
    /// contract. Callers should clear diagnostics immediately before each parse.
    /// </remarks>
    /// <seealso cref="ITableParser"/>
    /// <seealso cref="TableSuppressionAuditRecord"/>
    public interface ITableParserDiagnostics
    {
        /**************************************************************/
        /// <summary>
        /// Structural row or cell suppressions captured during the most recent parse.
        /// </summary>
        IReadOnlyList<TableSuppressionAuditRecord> SuppressedRows { get; }

        /**************************************************************/
        /// <summary>
        /// Clears diagnostic state before a new parse operation.
        /// </summary>
        void ClearDiagnostics();
    }
}
