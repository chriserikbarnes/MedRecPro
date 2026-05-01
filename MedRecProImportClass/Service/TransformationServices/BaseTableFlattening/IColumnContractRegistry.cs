namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Read-only lookup of per-TableCategory column contracts derived from
    /// <c>MedRecProImportClass/TableStandards/column-contracts.md</c>. Exposes the
    /// Required (R) / Expected (E) / Optional (O) / NullExpected (N) column sets per
    /// TableCategory so services can assert which columns matter for a given row type
    /// without duplicating the table-standards contract.
    /// </summary>
    /// <remarks>
    /// ## Intended Consumer
    /// <see cref="IParseQualityService"/> reads the Required set for each observation's
    /// TableCategory to apply the per-category "Required column NULL" penalty in its
    /// quality formula. Other future consumers may use the Expected/Optional/NullExpected
    /// sets for validation or stripping logic.
    ///
    /// ## Source of Truth
    /// The source of truth is the Markdown table in
    /// <c>MedRecProImportClass/TableStandards/column-contracts.md</c>. The implementation
    /// of this interface hardcodes the same contracts in code so registrations don't depend
    /// on runtime filesystem access to the skill files.
    ///
    /// ## Matching
    /// Category keys are case-insensitive. Unknown TableCategory values return
    /// <see cref="CategoryContract.Empty"/> — no Required columns — which means the
    /// per-category penalty in <see cref="IParseQualityService"/> is silently skipped
    /// rather than firing erroneously.
    /// </remarks>
    public interface IColumnContractRegistry
    {
        /**************************************************************/
        /// <summary>
        /// Returns the contract for a given TableCategory. Case-insensitive.
        /// Returns <see cref="CategoryContract.Empty"/> for unknown categories.
        /// </summary>
        /// <param name="tableCategory">Category label (e.g. "AdverseEvent", "PK", "DrugInteraction").</param>
        /// <returns>The contract. Never null.</returns>
        CategoryContract GetContract(string? tableCategory);
    }

    /**************************************************************/
    /// <summary>
    /// Per-TableCategory contract: which columns are Required, Expected, Optional, or
    /// NullExpected. Field sets use case-insensitive comparers keyed on the column names
    /// from <see cref="ParsedObservation"/> (e.g. "ParameterName", "PrimaryValue", "Unit").
    /// </summary>
    /// <param name="Required">Columns that MUST be populated for this category. Drives the
    /// per-category NULL penalty in <see cref="IParseQualityService"/>.</param>
    /// <param name="Expected">Columns usually populated for this category. Informational.</param>
    /// <param name="Optional">Columns populated when stratified data exists. Informational.</param>
    /// <param name="NullExpected">Columns defined as NULL for this category per the
    /// schema contract (e.g. Timepoint on AdverseEvent, PValue on PK).</param>
    public sealed record CategoryContract(
        HashSet<string> Required,
        HashSet<string> Expected,
        HashSet<string> Optional,
        HashSet<string> NullExpected)
    {
        /**************************************************************/
        /// <summary>
        /// Empty contract used as the fallback for unknown TableCategory values. All four
        /// sets are empty (but non-null), allowing the quality service to iterate them safely
        /// without per-call null checks.
        /// </summary>
        public static readonly CategoryContract Empty = new(
            Required: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Expected: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Optional: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NullExpected: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
