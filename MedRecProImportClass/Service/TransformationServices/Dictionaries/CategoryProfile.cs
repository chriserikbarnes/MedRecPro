namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Per-TableCategory profile bundling every per-category fact downstream services need:
    /// the column contract (R/E/O/N), the row-validation required-field subset, the
    /// completeness-scoring fields, the allowed PrimaryValueType set, the default BoundType,
    /// and the arm-coverage / time-consistency switches used by table-level validation.
    /// </summary>
    /// <remarks>
    /// ## Single source of truth
    /// Replaces six parallel category-keyed dictionaries that previously lived in
    /// <c>ColumnStandardizationService</c>, <c>RowValidationService</c>, and
    /// <c>TableValidationService</c>. Each field in this record corresponds to one of those
    /// dictionaries, transcribed faithfully so behavior is byte-identical.
    ///
    /// ## Composition over extension
    /// <see cref="Contract"/> wraps the existing <see cref="CategoryContract"/> rather than
    /// extending it, so <see cref="IColumnContractRegistry"/> consumers (notably
    /// <see cref="IParseQualityService"/>) keep their narrow contract unchanged.
    ///
    /// ## RowRequiredFields vs Contract.Required
    /// <see cref="RowRequiredFields"/> is intentionally narrower than <see cref="Contract"/>'s
    /// Required set. Row validation historically uses a smaller required-field list than the
    /// column contract (e.g., PK row requires <c>{ParameterName, DoseRegimen}</c>; the contract
    /// additionally requires <c>{PrimaryValue, PrimaryValueType, Unit}</c>). The two are kept
    /// separate to preserve behavior — they are not merged.
    /// </remarks>
    /// <param name="Contract">Column R/E/O/N contract for this category. Source of truth lives
    /// in <see cref="ColumnContractRegistry"/>; this profile delegates rather than duplicating.</param>
    /// <param name="RowRequiredFields">Field names that <c>RowValidationService</c> requires
    /// to be populated. Narrower than <c>Contract.Required</c> by design.</param>
    /// <param name="CompletenessFields">Field names counted toward
    /// <c>RowValidationService.calculateFieldCompleteness</c> scoring. May be empty.</param>
    /// <param name="AllowedValueTypes">Allowed <c>PrimaryValueType</c> values for this category.
    /// Empty set means "no constraint" — the value-type appropriateness check is skipped.</param>
    /// <param name="DefaultBoundType">Default <c>BoundType</c> applied by Phase 4 column standardization
    /// when bounds are populated but BoundType is null. <c>null</c> = no default.</param>
    /// <param name="UsesArmCoverage">When <c>true</c>, <c>TableValidationService</c> runs
    /// arm-coverage and count-reasonableness checks for tables of this category.</param>
    /// <param name="UsesTimeConsistency">When <c>true</c>, <c>TableValidationService</c> runs
    /// the time-extraction-consistency check for tables of this category.</param>
    /// <seealso cref="CategoryContract"/>
    /// <seealso cref="CategoryProfileRegistry"/>
    /// <seealso cref="MedRecProImportClass.Models.ParsedObservation"/>
    public sealed record CategoryProfile(
        CategoryContract Contract,
        IReadOnlyList<string> RowRequiredFields,
        IReadOnlyList<string> CompletenessFields,
        IReadOnlySet<string> AllowedValueTypes,
        string? DefaultBoundType,
        bool UsesArmCoverage,
        bool UsesTimeConsistency)
    {
        /**************************************************************/
        /// <summary>
        /// Empty profile returned for unknown TableCategory values. Every collection is
        /// non-null but empty so consumers can iterate without per-call null checks.
        /// </summary>
        public static readonly CategoryProfile Empty = new(
            Contract: CategoryContract.Empty,
            RowRequiredFields: Array.Empty<string>(),
            CompletenessFields: Array.Empty<string>(),
            AllowedValueTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DefaultBoundType: null,
            UsesArmCoverage: false,
            UsesTimeConsistency: false);
    }
}
