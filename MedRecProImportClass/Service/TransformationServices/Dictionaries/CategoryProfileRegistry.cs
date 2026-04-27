namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Static registry of per-TableCategory profiles. Each profile bundles the column contract,
    /// row-required fields, completeness fields, allowed value types, default bound type, and
    /// arm/time validation switches into one record.
    /// </summary>
    /// <remarks>
    /// ## Single source of truth
    /// Replaces the parallel category-keyed dictionaries that previously lived in
    /// <c>RowValidationService</c> (3 dicts), <c>TableValidationService</c> (2 sets), and
    /// <c>ColumnStandardizationService</c> (1 dict). The data here is transcribed faithfully
    /// from those sources to preserve byte-identical behavior.
    ///
    /// ## Key form
    /// Lookups accept either the underscore-uppercase form (<c>ADVERSE_EVENT</c>) used by
    /// <see cref="MedRecProImportClass.Models.ParsedObservation.TableCategory"/> or the documentation form
    /// (<c>AdverseEvent</c>) used by <see cref="ColumnContractRegistry"/>. Internal lookup is keyed on
    /// the documentation form via <see cref="CategoryNameNormalizer.Normalize"/>.
    ///
    /// ## OTHER vs unknown
    /// <c>OTHER</c> is a registered profile (with <c>RowRequiredFields = ["ParameterName"]</c>) —
    /// distinct from truly unknown categories which return <see cref="CategoryProfile.Empty"/>.
    /// </remarks>
    /// <seealso cref="CategoryProfile"/>
    /// <seealso cref="ColumnContractRegistry"/>
    /// <seealso cref="CategoryNameNormalizer"/>
    public static class CategoryProfileRegistry
    {
        #region Profile Map

        /**************************************************************/
        /// <summary>
        /// Backing contract registry instance. <see cref="CategoryProfile.Contract"/> on
        /// each profile is sourced from this instance so the R/E/O/N data has one home.
        /// </summary>
        private static readonly IColumnContractRegistry _contractRegistry = new ColumnContractRegistry();

        /**************************************************************/
        /// <summary>
        /// Profiles keyed on documentation form. Built once at class-load.
        /// </summary>
        private static readonly Dictionary<string, CategoryProfile> _profiles = buildProfiles();

        #endregion

        /**************************************************************/
        /// <summary>
        /// Returns the profile for a given category. Accepts either underscore-uppercase
        /// or documentation form. Never returns <c>null</c> — unknown categories return
        /// <see cref="CategoryProfile.Empty"/>.
        /// </summary>
        /// <param name="tableCategory">Category label. May be <c>null</c>, empty, or in either key form.</param>
        /// <returns>The profile. Never <c>null</c>.</returns>
        /// <example>
        /// <code>
        /// var profile = CategoryProfileRegistry.Get("ADVERSE_EVENT");
        /// if (profile.UsesArmCoverage) { ... }
        /// </code>
        /// </example>
        public static CategoryProfile Get(string? tableCategory)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(tableCategory))
                return CategoryProfile.Empty;

            var key = CategoryNameNormalizer.Normalize(tableCategory);
            return _profiles.TryGetValue(key, out var profile) ? profile : CategoryProfile.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Constructs the per-category profile map. Transcribed faithfully from the source
        /// dictionaries — preserve list ordering and exact strings; no paraphrasing.
        /// </summary>
        private static Dictionary<string, CategoryProfile> buildProfiles()
        {
            #region implementation

            var result = new Dictionary<string, CategoryProfile>(StringComparer.OrdinalIgnoreCase);

            // AdverseEvent — UsesArmCoverage=true
            result["AdverseEvent"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("AdverseEvent"),
                RowRequiredFields: new[] { "ParameterName", "TreatmentArm" },
                CompletenessFields: new[] { "ParameterName", "TreatmentArm", "ArmN", "PrimaryValueType", "Unit" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "Percentage", "Count", "Numeric", "CodedExclusion", "Text", "RiskDifference",
                    "RelativeRiskReduction", "PValue", "SampleSize"
                },
                DefaultBoundType: "95CI",
                UsesArmCoverage: true,
                UsesTimeConsistency: false);

            // PK — UsesTimeConsistency=true
            result["PK"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("PK"),
                RowRequiredFields: new[] { "ParameterName", "DoseRegimen" },
                CompletenessFields: new[] { "ParameterName", "DoseRegimen", "Population", "Unit", "Timepoint", "Time", "TimeUnit" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "Mean", "Median", "Numeric", "Ratio", "Text", "CodedExclusion", "SampleSize",
                    "GeometricMean", "ArithmeticMean", "LSMean", "GeometricMeanRatio"
                },
                DefaultBoundType: "90CI",
                UsesArmCoverage: false,
                UsesTimeConsistency: true);

            // DrugInteraction — DefaultBoundType=90CI, no completeness fields in legacy dict
            result["DrugInteraction"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("DrugInteraction"),
                RowRequiredFields: new[] { "ParameterName" },
                CompletenessFields: Array.Empty<string>(),
                AllowedValueTypes: new HashSet<string>()
                {
                    "GeometricMeanRatio", "GeometricMean", "Ratio", "Numeric", "Text", "Mean", "Median"
                },
                DefaultBoundType: "90CI",
                UsesArmCoverage: false,
                UsesTimeConsistency: false);

            // Efficacy — UsesArmCoverage=true
            result["Efficacy"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("Efficacy"),
                RowRequiredFields: new[] { "ParameterName", "TreatmentArm" },
                CompletenessFields: new[] { "ParameterName", "TreatmentArm", "ArmN", "PrimaryValueType", "StudyContext", "Unit" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "Percentage", "Count", "Numeric", "Mean", "Median", "RiskDifference",
                    "RelativeRiskReduction", "Ratio", "PValue", "Text", "CodedExclusion",
                    "SampleSize", "MeanPercentChange", "HazardRatio", "OddsRatio",
                    "RelativeRisk", "PercentChange", "ArithmeticMean", "GeometricMean", "LSMean"
                },
                DefaultBoundType: "95CI",
                UsesArmCoverage: true,
                UsesTimeConsistency: false);

            // Dosing — no DefaultBoundType, no arm/time switches
            result["Dosing"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("Dosing"),
                RowRequiredFields: new[] { "ParameterName" },
                CompletenessFields: new[] { "ParameterName", "Unit", "DoseRegimen" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "Numeric", "Percentage", "Mean", "Text", "SampleSize"
                },
                DefaultBoundType: null,
                UsesArmCoverage: false,
                UsesTimeConsistency: false);

            // BMD — UsesTimeConsistency=true
            result["BMD"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("BMD"),
                RowRequiredFields: new[] { "ParameterName", "Timepoint" },
                CompletenessFields: new[] { "ParameterName", "Timepoint", "Population", "Time", "TimeUnit", "Unit" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "MeanPercentChange", "Percentage", "Numeric", "Mean", "Text", "PercentChange", "ArithmeticMean"
                },
                DefaultBoundType: "95CI",
                UsesArmCoverage: false,
                UsesTimeConsistency: true);

            // TissueDistribution — no DefaultBoundType
            result["TissueDistribution"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("TissueDistribution"),
                RowRequiredFields: new[] { "ParameterName" },
                CompletenessFields: new[] { "ParameterName", "Unit" },
                AllowedValueTypes: new HashSet<string>()
                {
                    "Ratio", "Numeric", "Text", "ArithmeticMean", "GeometricMean"
                },
                DefaultBoundType: null,
                UsesArmCoverage: false,
                UsesTimeConsistency: false);

            // TextDescriptive — narrative tables; no completeness or allowed-value-types in legacy
            result["TextDescriptive"] = new CategoryProfile(
                Contract: _contractRegistry.GetContract("TextDescriptive"),
                RowRequiredFields: Array.Empty<string>(),
                CompletenessFields: Array.Empty<string>(),
                AllowedValueTypes: new HashSet<string>(),
                DefaultBoundType: null,
                UsesArmCoverage: false,
                UsesTimeConsistency: false);

            // OTHER — only RowRequiredFields populated in legacy dict
            result["OTHER"] = new CategoryProfile(
                Contract: CategoryContract.Empty,
                RowRequiredFields: new[] { "ParameterName" },
                CompletenessFields: Array.Empty<string>(),
                AllowedValueTypes: new HashSet<string>(),
                DefaultBoundType: null,
                UsesArmCoverage: false,
                UsesTimeConsistency: false);

            return result;

            #endregion
        }
    }
}
