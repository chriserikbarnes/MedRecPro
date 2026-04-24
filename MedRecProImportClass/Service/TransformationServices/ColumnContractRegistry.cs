namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Hardcoded <see cref="IColumnContractRegistry"/> implementation. The R/E/O/N sets
    /// for each TableCategory are transcribed from the canonical Markdown table in
    /// <c>MedRecProImportClass/TableStandards/column-contracts.md</c>. Kept as code (not
    /// parsed at runtime) so registration has zero filesystem dependency.
    /// </summary>
    /// <remarks>
    /// ## Update Cadence
    /// If <c>column-contracts.md</c> changes, the static dictionary below must be updated
    /// to match. The document is the single source of truth; the code is a cached view.
    ///
    /// ## Category Name Canonicalization
    /// Lookups fold to lowercase before matching. Callers may pass either the ParsedObservation
    /// form (<c>ADVERSE_EVENT</c>, <c>DRUG_INTERACTION</c>) or the contracts document form
    /// (<c>AdverseEvent</c>, <c>DrugInteraction</c>); both map to the same entry.
    /// </remarks>
    /// <seealso cref="IColumnContractRegistry"/>
    /// <seealso cref="CategoryContract"/>
    public class ColumnContractRegistry : IColumnContractRegistry
    {
        #region Contract Map

        /**************************************************************/
        /// <summary>
        /// Per-TableCategory contracts. Keys are the canonical document form; a separate
        /// alias map in <see cref="normalize"/> handles the underscore-uppercase variants
        /// used by <see cref="ParsedObservation.TableCategory"/>.
        /// </summary>
        private static readonly Dictionary<string, CategoryContract> _contracts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AdverseEvent"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "TreatmentArm", "PrimaryValue", "PrimaryValueType" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "ArmN", "Unit" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "ParameterSubtype", "StudyContext", "DoseRegimen", "Dose", "DoseUnit", "Population", "SecondaryValue", "SecondaryValueType", "LowerBound", "UpperBound", "BoundType", "PValue" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "Timepoint", "Time", "TimeUnit" }),

            ["PK"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "PrimaryValue", "PrimaryValueType", "Unit" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "DoseRegimen", "Dose", "DoseUnit", "SecondaryValue", "SecondaryValueType" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "ParameterSubtype", "TreatmentArm", "ArmN", "StudyContext", "Population", "Timepoint", "Time", "TimeUnit", "LowerBound", "UpperBound", "BoundType" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "PValue" }),

            ["DrugInteraction"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "ParameterSubtype", "PrimaryValue", "PrimaryValueType", "LowerBound", "UpperBound", "BoundType" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "TreatmentArm", "DoseRegimen", "Dose", "DoseUnit" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "ArmN", "StudyContext", "Population", "Unit" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "Timepoint", "Time", "TimeUnit", "SecondaryValue", "SecondaryValueType", "PValue" }),

            ["Efficacy"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "TreatmentArm", "PrimaryValue", "PrimaryValueType" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "ArmN", "LowerBound", "UpperBound", "BoundType" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "ParameterSubtype", "StudyContext", "DoseRegimen", "Dose", "DoseUnit", "Population", "Timepoint", "Time", "TimeUnit", "PValue", "Unit" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "SecondaryValue", "SecondaryValueType" }),

            ["Dosing"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "DoseRegimen", "Population" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "ParameterSubtype", "TreatmentArm", "Dose", "DoseUnit", "Timepoint", "Time", "TimeUnit", "PrimaryValue", "PrimaryValueType", "LowerBound", "UpperBound", "BoundType", "Unit" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "ArmN", "StudyContext", "SecondaryValue", "SecondaryValueType", "PValue" }),

            ["BMD"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "TreatmentArm", "PrimaryValue", "PrimaryValueType" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "ArmN", "Timepoint", "Time", "TimeUnit", "Unit" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "StudyContext", "DoseRegimen", "Dose", "DoseUnit", "Population", "SecondaryValue", "SecondaryValueType", "LowerBound", "UpperBound", "BoundType", "PValue" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "ParameterSubtype" }),

            ["TissueDistribution"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "ParameterName", "PrimaryValue", "PrimaryValueType", "Unit" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "DoseRegimen", "Dose", "DoseUnit", "Timepoint", "Time", "TimeUnit", "SecondaryValue", "SecondaryValueType" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "TreatmentArm", "ArmN", "Population" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "ParameterSubtype", "StudyContext", "LowerBound", "UpperBound", "BoundType", "PValue" }),

            ["TextDescriptive"] = new CategoryContract(
                Required: new(StringComparer.OrdinalIgnoreCase) { "PrimaryValueType", "RawValue" },
                Expected: new(StringComparer.OrdinalIgnoreCase) { "ParameterName" },
                Optional: new(StringComparer.OrdinalIgnoreCase) { "TreatmentArm", "DoseRegimen", "Dose", "DoseUnit", "Population" },
                NullExpected: new(StringComparer.OrdinalIgnoreCase) { "ParameterCategory", "ParameterSubtype", "ArmN", "Timepoint", "PrimaryValue", "SecondaryValue", "SecondaryValueType", "LowerBound", "UpperBound", "BoundType", "PValue", "Unit" }),
        };

        /**************************************************************/
        /// <summary>
        /// Aliases for the uppercase-underscore forms used by
        /// <see cref="ParsedObservation.TableCategory"/> that map to the documentation form
        /// keys of <see cref="_contracts"/>.
        /// </summary>
        private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVERSE_EVENT"] = "AdverseEvent",
            ["DRUG_INTERACTION"] = "DrugInteraction",
            ["TISSUE_DISTRIBUTION"] = "TissueDistribution",
            ["TEXT_DESCRIPTIVE"] = "TextDescriptive",
        };

        #endregion

        /**************************************************************/
        /// <inheritdoc/>
        public CategoryContract GetContract(string? tableCategory)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(tableCategory))
                return CategoryContract.Empty;

            var key = normalize(tableCategory);
            return _contracts.TryGetValue(key, out var contract)
                ? contract
                : CategoryContract.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes the caller's TableCategory value to the documentation form key used in
        /// <see cref="_contracts"/>. Trims whitespace and resolves aliases.
        /// </summary>
        /// <param name="tableCategory">Input category string (non-null, non-whitespace).</param>
        /// <returns>Canonical key for dictionary lookup.</returns>
        private static string normalize(string tableCategory)
        {
            #region implementation

            var trimmed = tableCategory.Trim();
            return _aliases.TryGetValue(trimmed, out var canonical)
                ? canonical
                : trimmed;

            #endregion
        }
    }
}
