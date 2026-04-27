using System.Net;
using System.Text.RegularExpressions;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Deterministic, rule-based column standardization service that detects and corrects
    /// systematic misclassification of values across all observation context columns.
    /// Processes ALL table categories (except SKIP) through a 4-phase pipeline.
    /// </summary>
    /// <remarks>
    /// ## Drug Name Dictionary
    /// Loads distinct ProductName and SubstanceName values from <c>vw_ProductsByIngredient</c>
    /// at initialization. Used for content classification to distinguish drug names from
    /// doses, sample sizes, and other metadata.
    ///
    /// ## Processing Phases
    /// 1. **Phase 1: Arm/Context Corrections** — (AE + EFFICACY only) Apply 11 ordered rules
    ///    to relocate misclassified TreatmentArm/StudyContext values
    /// 2. **Phase 2: Content Normalization** — (ALL categories) DoseRegimen triage,
    ///    ParameterName cleanup, TreatmentArm cleanup, Unit scrub, SOC mapping
    /// 3. **Phase 3: PrimaryValueType Migration** — (ALL categories) Map old type strings
    ///    to tightened enum values using table category and caption context
    /// 4. **Phase 4: Column Contract Enforcement** — (ALL categories) NULL out N/A columns,
    ///    flag missing required columns, apply default BoundType
    ///
    /// All corrections are flagged in <see cref="ParsedObservation.ValidationFlags"/>
    /// with <c>COL_STD:</c> prefixed flags for audit trail.
    /// </remarks>
    /// <seealso cref="IColumnStandardizationService"/>
    /// <seealso cref="ParsedObservation"/>
    public class ColumnStandardizationService : IColumnStandardizationService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Database context for drug name dictionary loading.</summary>
        private readonly DbContext _dbContext;

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<ColumnStandardizationService> _logger;

        /**************************************************************/
        /// <summary>
        /// Drug name dictionary — exact match lookup (case-insensitive).
        /// Loaded from ProductName and SubstanceName in vw_ProductsByIngredient.
        /// </summary>
        private HashSet<string> _drugNames = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>
        /// First-word index for partial matching (e.g., "Mycophenolate" from "Mycophenolate Mofetil").
        /// Maps first word → set of full drug names that start with that word.
        /// </summary>
        private HashSet<string> _drugFirstWords = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Common drug abbreviations not found in the formal product name dictionary.
        /// </summary>
        private static readonly Dictionary<string, string> _knownAbbreviations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AZA"] = "Azathioprine",
            ["MMF"] = "Mycophenolate Mofetil",
            ["CsA"] = "Cyclosporine",
            ["MTX"] = "Methotrexate",
            ["5-FU"] = "Fluorouracil",
            ["6-MP"] = "Mercaptopurine",
            ["IFN"] = "Interferon",
            ["EPO"] = "Epoetin",
            ["G-CSF"] = "Filgrastim",
            ["TNF"] = "Tumor Necrosis Factor",
            ["PGB"] = "Pregabalin",
            ["HCT"] = "Hydrochlorothiazide",
            ["ASA"] = "Aspirin",
            // R11 — HIV antiretroviral abbreviations leaking from regimen columns
            // into Unit cells (observed standalone "BIC"/"FTC"/"TAF" values).
            ["BIC"] = "Bictegravir",
            ["FTC"] = "Emtricitabine",
            ["TAF"] = "Tenofovir Alafenamide"
        };

        /**************************************************************/
        /// <summary>
        /// Optional AE ParameterName → SOC dictionary resolver. Null when not configured.
        /// Used in Phase 2 to fill NULL ParameterCategory from a static lookup dictionary.
        /// </summary>
        /// <seealso cref="IAeParameterCategoryDictionaryService"/>
        private readonly IAeParameterCategoryDictionaryService? _aeDictionary;

        /**************************************************************/
        /// <summary>Whether the dictionary has been loaded.</summary>
        private bool _initialized;

        /**************************************************************/
        /// <summary>Whether the category should skip Phase 1 arm/context corrections (only AE+EFFICACY apply).</summary>
        private static bool isPhase1Category(string? category) =>
            string.Equals(category, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "EFFICACY", StringComparison.OrdinalIgnoreCase);

        #region Phase 2 Static Dictionaries

        // Former PK sub-parameter fields (_pkSubParams, _pkSubParamPrefixPattern)
        // were migrated to the shared PkParameterDictionary. Callers now use
        // PkParameterDictionary.IsPkParameter / StartsWithPk.

        /**************************************************************/
        /// <summary>Regex to detect residual population content in DoseRegimen.</summary>
        private static readonly Regex _residualPopulationPattern = new(
            @"^(?:adult|pediatric|elderly|geriatric|renal|hepatic|child(?:ren)?|healthy|volunteer|neonat|\d+-\d+\s*(?:kg|years?))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Regex to detect residual timepoint content in DoseRegimen.</summary>
        private static readonly Regex _residualTimepointPattern = new(
            @"^(?:day|week|month|cycle|baseline|steady[\s\-]?state|single[\s\-]?dose|pre[\s\-]?dose|visit|hour)\s*\d*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Regex to detect actual dose content — keeps DoseRegimen value.</summary>
        private static readonly Regex _actualDosePattern = new(
            @"\d+\.?\d*\s*(?:mg|mcg|µg|g|mL|units?|IU)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Stat-form column-header echoes that leak into DoseRegimen — e.g., when a
        /// PK table has a header row "Mean ± Standard Deviation" and the parser
        /// inherited it into DoseRegimen via section-divider context. Per
        /// normalization-rules.md §0.2 (header-echo carve-out), these are nulled.
        /// </summary>
        /// <remarks>
        /// Match is full-string equality after trim — never substring — so values
        /// like "Mean concentration 50 mg" are not erased. Comparer is
        /// case-insensitive.
        /// </remarks>
        /// <seealso cref="normalizeDoseRegimen"/>
        private static readonly HashSet<string> _doseRegimenStatEchoSet = new(StringComparer.OrdinalIgnoreCase)
        {
            "Mean ± Standard Deviation",
            "Mean ± SD",
            "Mean (SD)",
            "Median (Range)",
            "Median (IQR)",
            "Range",
            "Geometric Mean (CV%)",
            "Arithmetic Mean ± SD",
            "Mean",
            "Median"
        };

        /**************************************************************/
        /// <summary>Regex to detect caption echo rows in ParameterName.</summary>
        private static readonly Regex _captionEchoPattern = new(
            @"^Table\s+\d|Pharmacokinetic\s+Parameters|Geometric\s+Mean\s+Ratio\s*\(|Drug\s+Interactions\s*:",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Regex to detect header echo rows in ParameterName (bare "n" or "N").</summary>
        private static readonly Regex _paramHeaderEchoPattern = new(
            @"^[nN]\s*(?:\(|$)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Common bare dose integers that indicate a dose level, not a parameter name.</summary>
        private static readonly HashSet<string> _bareDoseLevels = new()
        {
            "5", "10", "15", "20", "25", "30", "40", "50", "100", "150", "200",
            "250", "300", "400", "500", "600", "800", "1200", "1600", "2400", "3600"
        };

        /**************************************************************/
        /// <summary>
        /// Regex to detect header echo patterns in TreatmentArm.
        /// Matches: "Number of Patients", "Percent of Subjects", "Percentage Reporting"
        /// </summary>
        private static readonly Regex _armHeaderEchoPattern = new(
            @"(?:Number|Percent(?:age)?)\s+(?:of\s+)?(?:Patients|Subjects|Reporting)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Generic arm labels that carry no semantic information.</summary>
        private static readonly HashSet<string> _genericArmLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Comparison", "Treatment", "PD", "SAD"
        };

        /**************************************************************/
        /// <summary>Regex to detect study name patterns (all-caps short tokens).</summary>
        private static readonly Regex _studyNamePattern = new(
            @"^[A-Z][A-Z0-9\-]{2,15}(?:\s+\d+)?$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Regex to extract embedded dose from TreatmentArm (e.g., "150 mg/d").</summary>
        private static readonly Regex _armEmbeddedDosePattern = new(
            @"(\d+\.?\d*\s*(?:mg|mcg|µg|g|mL|IU|units?)(?:\s*/\s*(?:day|d|kg|m²))?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // R10 DRY — canonical unit token set and variant normalization map moved
        // to Dictionaries.UnitDictionary (single source of truth shared with
        // PkTableParser's parser-time unit extraction). Access as
        // UnitDictionary.KnownUnits.Contains(x) /
        // UnitDictionary.NormalizationMap.TryGetValue(k, out v) — identical
        // semantics to the prior private fields, minus duplication.

        /**************************************************************/
        /// <summary>Keywords indicating a Unit value is actually a leaked column header.</summary>
        private static readonly HashSet<string> _unitHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Regimen", "Dosage", "Patients", "Titration", "Starting",
            "Recommended", "Duration", "TAKING", "Tablets", "Injection",
            "Therapy", "Combination", "Divided", "Subjects",

            // R11 — Dose regimen / frequency descriptors leaked into Unit
            "Suspension",      // "20 mg/kg Oral Suspension"
            "every",           // "20mg/kg every 8 hours"
            "b.i.d.", "t.i.d.", "q.d.",
            "loading", "Maintenance", // "loading dose", "Maintenance dose"
            "daily dose",
            "LD/MD",           // "LD/MD, mg", "LD/MD, mg/kg"

            // R11 — Age / population descriptors (NOT units)
            "Ages",            // "Ages 27-58 yrs", "Ages 27 to 58 yrs"
            "yrs",             // "≥65 yrs"
            "years",           // "19 to 78 years", "20-48 years", "age range 18 to 32 years"
            "age range",       // "age range 18 to 32 years"

            // R11 — Subgroup descriptors
            "eGFR",            // "eGFR ≥ 90 mL/min", "eGFR 60 to < 90 mL/min"

            // R11 — Statistical markers (NOT units; downstream dispersion fields handle these)
            "Mean ±", "mean ±",
            "±SD", "± SD",
            "% CI",            // "90% CI", "95% CI"

            // R11 — Time-window / interval markers (AUC subscripts that leaked)
            "0-24",            // "0-24", "0-24ss"
            "0-τ",             // "0-τ"

            // R11 — Footnote / qualifier leaks
            "except where"     // "except where noted"
        };

        /**************************************************************/
        /// <summary>Regex to extract a real unit from inside a verbose description.</summary>
        private static readonly Regex _extractableUnitPattern = new(
            @"\(([^)]{1,20})\)\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Canonical MedDRA SOC mapping — normalizes ~140 variants to 26 canonical names.
        /// Only applies when TableCategory = ADVERSE_EVENT.
        /// </summary>
        private static readonly Dictionary<string, string> _socCanonicalMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Blood and Lymphatic System Disorders
            ["blood and lymphatic system disorders"] = "Blood and Lymphatic System Disorders",
            ["blood and lymphatic"] = "Blood and Lymphatic System Disorders",
            ["hematologic"] = "Blood and Lymphatic System Disorders",
            ["hemic and lymphatic system"] = "Blood and Lymphatic System Disorders",
            // Cardiac Disorders
            ["cardiac disorders"] = "Cardiac Disorders",
            ["cardiac"] = "Cardiac Disorders",
            // Ear and Labyrinth Disorders
            ["ear and labyrinth disorders"] = "Ear and Labyrinth Disorders",
            ["ear disorders"] = "Ear and Labyrinth Disorders",
            // Endocrine Disorders
            ["endocrine disorders"] = "Endocrine Disorders",
            // Eye Disorders
            ["eye disorders"] = "Eye Disorders",
            ["special senses"] = "Eye Disorders",
            ["eye disorders (other than field or acuity changes)"] = "Eye Disorders",
            // Gastrointestinal Disorders
            ["gastrointestinal disorders"] = "Gastrointestinal Disorders",
            ["gastrointestinal"] = "Gastrointestinal Disorders",
            ["digestive system"] = "Gastrointestinal Disorders",
            ["gastro - intestinal system disorders"] = "Gastrointestinal Disorders",
            ["gastro-intestinal system disorders"] = "Gastrointestinal Disorders",
            // General Disorders and Administration Site Conditions
            ["general disorders and administration site conditions"] = "General Disorders",
            ["general disorders"] = "General Disorders",
            ["body as a whole"] = "General Disorders",
            // Hepatobiliary Disorders
            ["hepatobiliary disorders"] = "Hepatobiliary Disorders",
            ["liver and biliary system disorders"] = "Hepatobiliary Disorders",
            // Immune System Disorders
            ["immune system disorders"] = "Immune System Disorders",
            // Infections and Infestations
            ["infections and infestations"] = "Infections and Infestations",
            ["resistance mechanism disorders"] = "Infections and Infestations",
            // Injury, Poisoning and Procedural Complications
            ["injury, poisoning and procedural complications"] = "Injury, Poisoning and Procedural Complications",
            // Investigations
            ["investigations"] = "Investigations",
            // Metabolism and Nutrition Disorders
            ["metabolism and nutrition disorders"] = "Metabolism and Nutrition Disorders",
            ["metabolic and nutritional"] = "Metabolism and Nutrition Disorders",
            // Musculoskeletal and Connective Tissue Disorders
            ["musculoskeletal and connective tissue disorders"] = "Musculoskeletal Disorders",
            ["musculoskeletal disorders"] = "Musculoskeletal Disorders",
            ["musculo-skeletal system disorders"] = "Musculoskeletal Disorders",
            // Neoplasms Benign, Malignant and Unspecified
            ["neoplasms benign, malignant and unspecified"] = "Neoplasms",
            // Nervous System Disorders
            ["nervous system disorders"] = "Nervous System Disorders",
            ["nervous system"] = "Nervous System Disorders",
            ["central & peripheral nervous system disorders"] = "Nervous System Disorders",
            ["central and peripheral nervous system disorders"] = "Nervous System Disorders",
            ["cns"] = "Nervous System Disorders",
            // Psychiatric Disorders
            ["psychiatric disorders"] = "Psychiatric Disorders",
            ["psychiatric"] = "Psychiatric Disorders",
            // Renal and Urinary Disorders
            ["renal and urinary disorders"] = "Renal and Urinary Disorders",
            ["urogenital system"] = "Renal and Urinary Disorders",
            // Reproductive System and Breast Disorders
            ["reproductive system and breast disorders"] = "Reproductive System and Breast Disorders",
            // Respiratory, Thoracic and Mediastinal Disorders
            ["respiratory, thoracic and mediastinal disorders"] = "Respiratory Disorders",
            ["respiratory disorders"] = "Respiratory Disorders",
            ["respiratory system"] = "Respiratory Disorders",
            // Skin and Subcutaneous Tissue Disorders
            ["skin and subcutaneous tissue disorders"] = "Skin and Subcutaneous Tissue Disorders",
            ["skin and subcutaneous tissues disorders"] = "Skin and Subcutaneous Tissue Disorders",
            ["skin"] = "Skin and Subcutaneous Tissue Disorders",
            ["dermatologic"] = "Skin and Subcutaneous Tissue Disorders",
            // Vascular Disorders
            ["vascular disorders"] = "Vascular Disorders",
            ["cardiovascular"] = "Vascular Disorders"
        };

        /**************************************************************/
        /// <summary>Regex to collapse OCR spacing artifacts in SOC names.</summary>
        private static readonly Regex _ocrSpacingPattern = new(
            @"(?<=\w)\s+(?=\w(?:\s+\w)*ders\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Regex to collapse isolated single characters with spaces (OCR artifacts).</summary>
        private static readonly Regex _ocrSingleCharPattern = new(
            @"(?<=[A-Za-z])\s([A-Za-z])\s(?=[A-Za-z])",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Regex to detect HTML entities in text.</summary>
        private static readonly Regex _htmlEntityPattern = new(
            @"&(?:gt|lt|amp|quot|apos|nbsp);",
            RegexOptions.Compiled);

        #endregion Phase 2 Static Dictionaries

        #region Phase 3 Static Dictionaries

        /**************************************************************/
        /// <summary>
        /// PrimaryValueType direct migration map — old value → new value.
        /// Context-dependent migrations (Mean, Numeric, RelativeRiskReduction)
        /// are handled in code, not in this map.
        /// </summary>
        private static readonly Dictionary<string, string> _pvtDirectMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Percentage"] = "Percentage",
            ["MeanPercentChange"] = "PercentChange",
            ["RiskDifference"] = "RiskDifference",
            ["Median"] = "Median",
            ["Count"] = "Count",
            ["Text"] = "Text",
            ["PValue"] = "PValue",
            // SampleSize is not in the canonical PrimaryValueType enum
            // (column-contracts.md). Coerce to Count.
            ["SampleSize"] = "Count",
            // Range is not in the canonical enum either; range_to rows already
            // populate LowerBound/UpperBound + BoundType="Range" with the
            // midpoint in PrimaryValue, so the correct PVT label is the
            // statistic for that midpoint — ArithmeticMean.
            ["Range"] = "ArithmeticMean",
            ["CodedExclusion"] = "CodedExclusion"
        };

        #endregion Phase 3 Static Dictionaries

        #region Phase 4 Column Contract Definitions

        /**************************************************************/
        /// <summary>Column requirement level for contract enforcement.</summary>
        private enum ColumnRequirement
        {
            /// <summary>Must be populated — flag if missing.</summary>
            Required,
            /// <summary>Usually populated — no flag, but tracked in completeness.</summary>
            Expected,
            /// <summary>Populated when data stratifies on this dimension.</summary>
            Optional,
            /// <summary>Must be NULL for this table type — enforce null.</summary>
            NotApplicable
        }

        /**************************************************************/
        /// <summary>
        /// Per-TableCategory column contracts. Keys are observation context column names.
        /// Provenance, Classification, and Validation columns are not enforced here (always populated by parser).
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, ColumnRequirement>> _columnContracts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVERSE_EVENT"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.Expected,
                ["ParameterSubtype"] = ColumnRequirement.Optional,
                ["TreatmentArm"] = ColumnRequirement.Required,
                ["ArmN"] = ColumnRequirement.Expected,
                ["StudyContext"] = ColumnRequirement.Optional,
                ["DoseRegimen"] = ColumnRequirement.Optional,
                ["Dose"] = ColumnRequirement.Optional,
                ["DoseUnit"] = ColumnRequirement.Optional,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.NotApplicable,
                ["Time"] = ColumnRequirement.NotApplicable,
                ["TimeUnit"] = ColumnRequirement.NotApplicable,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Expected
            },
            ["PK"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.Optional,
                ["TreatmentArm"] = ColumnRequirement.Optional,
                ["ArmN"] = ColumnRequirement.Optional,
                ["StudyContext"] = ColumnRequirement.Optional,
                ["DoseRegimen"] = ColumnRequirement.Expected,
                ["Dose"] = ColumnRequirement.Expected,
                ["DoseUnit"] = ColumnRequirement.Expected,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.Optional,
                ["Time"] = ColumnRequirement.Optional,
                ["TimeUnit"] = ColumnRequirement.Optional,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Required
            },
            ["DRUG_INTERACTION"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.Required,
                ["TreatmentArm"] = ColumnRequirement.Expected,
                ["ArmN"] = ColumnRequirement.Optional,
                ["StudyContext"] = ColumnRequirement.Optional,
                ["DoseRegimen"] = ColumnRequirement.Expected,
                ["Dose"] = ColumnRequirement.Expected,
                ["DoseUnit"] = ColumnRequirement.Expected,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.NotApplicable,
                ["Time"] = ColumnRequirement.NotApplicable,
                ["TimeUnit"] = ColumnRequirement.NotApplicable,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Optional
            },
            ["EFFICACY"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.Optional,
                ["TreatmentArm"] = ColumnRequirement.Required,
                ["ArmN"] = ColumnRequirement.Expected,
                ["StudyContext"] = ColumnRequirement.Optional,
                ["DoseRegimen"] = ColumnRequirement.Optional,
                ["Dose"] = ColumnRequirement.Optional,
                ["DoseUnit"] = ColumnRequirement.Optional,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.Optional,
                ["Time"] = ColumnRequirement.Optional,
                ["TimeUnit"] = ColumnRequirement.Optional,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Optional
            },
            ["DOSING"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.Optional,
                ["TreatmentArm"] = ColumnRequirement.Optional,
                ["ArmN"] = ColumnRequirement.NotApplicable,
                ["StudyContext"] = ColumnRequirement.NotApplicable,
                ["DoseRegimen"] = ColumnRequirement.Expected,
                ["Dose"] = ColumnRequirement.Optional,
                ["DoseUnit"] = ColumnRequirement.Optional,
                ["Population"] = ColumnRequirement.Expected,
                ["Timepoint"] = ColumnRequirement.Optional,
                ["Time"] = ColumnRequirement.Optional,
                ["TimeUnit"] = ColumnRequirement.Optional,
                ["PrimaryValueType"] = ColumnRequirement.Optional,
                ["Unit"] = ColumnRequirement.Optional
            },
            ["BMD"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.NotApplicable,
                ["TreatmentArm"] = ColumnRequirement.Required,
                ["ArmN"] = ColumnRequirement.Expected,
                ["StudyContext"] = ColumnRequirement.Optional,
                ["DoseRegimen"] = ColumnRequirement.Optional,
                ["Dose"] = ColumnRequirement.Optional,
                ["DoseUnit"] = ColumnRequirement.Optional,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.Expected,
                ["Time"] = ColumnRequirement.Expected,
                ["TimeUnit"] = ColumnRequirement.Expected,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Expected
            },
            ["TISSUE_DISTRIBUTION"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ParameterName"] = ColumnRequirement.Required,
                ["ParameterCategory"] = ColumnRequirement.NotApplicable,
                ["ParameterSubtype"] = ColumnRequirement.NotApplicable,
                ["TreatmentArm"] = ColumnRequirement.Optional,
                ["ArmN"] = ColumnRequirement.Optional,
                ["StudyContext"] = ColumnRequirement.NotApplicable,
                ["DoseRegimen"] = ColumnRequirement.Expected,
                ["Dose"] = ColumnRequirement.Expected,
                ["DoseUnit"] = ColumnRequirement.Expected,
                ["Population"] = ColumnRequirement.Optional,
                ["Timepoint"] = ColumnRequirement.Expected,
                ["Time"] = ColumnRequirement.Expected,
                ["TimeUnit"] = ColumnRequirement.Expected,
                ["PrimaryValueType"] = ColumnRequirement.Required,
                ["Unit"] = ColumnRequirement.Required
            }
        };

        /**************************************************************/
        /// <summary>Default BoundType by TableCategory when bounds are present but BoundType is null.</summary>
        private static readonly Dictionary<string, string> _defaultBoundType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PK"] = "90CI",
            ["DRUG_INTERACTION"] = "90CI",
            ["EFFICACY"] = "95CI",
            ["BMD"] = "95CI",
            ["ADVERSE_EVENT"] = "95CI"
        };

        #endregion Phase 4 Column Contract Definitions

        #endregion Fields

        #region Compiled Regex Patterns

        /**************************************************************/
        /// <summary>
        /// Pure dose regimen — number + unit, optional schedule/route.
        /// Matches: "10 mg", "500 mcg BID", "1000 mg Once Daily", "40 mg daily subcutaneously"
        /// Also matches: semicolon-separated thousands like "1;000 mg"
        /// </summary>
        private static readonly Regex _pureDosePattern = new(
            @"^[\d;]+\.?\d*\s*(?:mg|mcg|µg|g|ml|mL|IU|units?)\s*(?:/\s*(?:day|kg(?:/day)?|m²))?\s*(?:(?:once|twice|BID|TID|QID|QD|q\d+h)\s*(?:daily)?)?\s*(?:(?:oral(?:ly)?|subcutaneous(?:ly)?|IV|IM|intravenous(?:ly)?)\s*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Trailing dose pattern for drug+dose splitting.
        /// Matches trailing portion: " 2g/day", " 10 mg", " 1.5 to 3 mg/kg /day"
        /// </summary>
        private static readonly Regex _trailingDosePattern = new(
            @"\s+([\d;]+\.?\d*(?:\s*to\s*[\d;]+\.?\d*)?\s*(?:mg|mcg|µg|g|ml|mL|IU|units?)\s*(?:/\s*(?:day|kg(?:\s*/?\s*day)?|m²))?\s*(?:or\s+[\d;]+\s*to\s+[\d;]+\s*(?:mg|mcg|µg|g|ml|mL)\s*(?:/?\s*day)?)?\s*(?:(?:once|twice|BID|TID|QID|QD|q\d+h|a\.m\.)\s*(?:daily)?)?\s*(?:(?:oral(?:ly)?|subcutaneous(?:ly)?|IV|IM)\s*)?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// N= value with or without parentheses.
        /// Matches: "(N=267)", "(n = 99)", "N=677", "n=48"
        /// </summary>
        private static readonly Regex _nValuePattern = new(
            @"^\(?\s*[Nn]\s*=\s*(\d[\d,]*)\s*\)?$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Embedded N= in text — drug/arm name followed by N=xxx.
        /// Matches: "Doxazosin N=339", "Placebo N=300", "HBP Foam N=351", "KANUMA N = 36"
        /// </summary>
        private static readonly Regex _embeddedNPattern = new(
            @"^(.+?)\s+[Nn]\s*=\s*(\d[\d,]*)$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Format hint patterns — unit/format descriptors, not arm names.
        /// Matches: "%", "#", "n(%)", "% of patients", "N (%)"
        /// </summary>
        private static readonly Regex _formatHintPattern = new(
            @"^(?:%|#|n\s*\(\s*%\s*\)|%\s+of\s+(?:patients|subjects)|N\s*\(\s*%\s*\))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Severity grade patterns.
        /// Matches: "Severe", "Total", "Grades 1–4", "Grades 3/4", "All Grades"
        /// </summary>
        private static readonly Regex _severityPattern = new(
            @"^(?:Severe|Total|Grades?\s*[\d/–\-]+(?:\s*(?:and|or)\s*\d+)?|All\s+Grades?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Descriptor hints in StudyContext — column descriptors, not study names.
        /// Matches: "% of Patients", "Incidence", "Adverse Event", "Dosing Regimen",
        /// "Percentage of Patients Reporting Event", "N (%)", "Discontinuation",
        /// "Number (%) of Patients", "Reaction", "% Major Bleeding", "% Subjects"
        /// </summary>
        private static readonly Regex _descriptorHintPattern = new(
            @"^(?:%\s+(?:of\s+)?(?:Patients|Subjects|Major\s+Bleeding)|Incidence|Adverse\s+Event|Reaction|N\s*\(\s*%\s*\)|Percentage.*(?:Reporting|Breast\s+Cancer).*|Dosing\s+Regimen|Discontinuation|Number\s*\(\s*%?\s*\)\s*of\s*Patients|Percent\s+of\s+Patients)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Bare integer — just digits, no units or qualifiers.
        /// Matches: "200", "600", "1" but not "200 mg" or negative numbers.
        /// </summary>
        private static readonly Regex _bareNumberPattern = new(
            @"^\d{1,5}$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Dose descriptor in StudyContext — extracts drug name + unit.
        /// Matches: "Target Topiramate Tablets Dosage (mg/day)", "Dofetilide Capsules Dose",
        /// "Buprenorphine Dose", "Oxcarbazepine Dosage (mg/day)"
        /// </summary>
        private static readonly Regex _doseDescriptorPattern = new(
            @"^(?:Target\s+)?(.+?)\s+(?:Dose|Dosage)\s*(?:\(\s*(mg|mcg|µg|g)(?:\s*(?:per|/)\s*day)?\s*\))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Study-like keywords indicating the value is a study/trial name.
        /// Matches: "Kidney Studies", "Trial 1", "Phase III", "Heart Study", "ADORING 1"
        /// </summary>
        private static readonly Regex _studyPattern = new(
            @"(?:Study|Studies|Trial|Phase\s+[IViv\d]+|ADORING|PSOARING|DIAMOND|GFEA|MA-\d|SC-[IV]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// StudyContext values that contain arm name with embedded N= and format hint.
        /// Matches: "Control Arm (N=18) n (%)", "EMPAVELI (N=46) n (%)"
        /// </summary>
        private static readonly Regex _ctxArmWithNAndHintPattern = new(
            @"^(.+?)\s*\(\s*[Nn]\s*=\s*(\d[\d,]*)\s*\)\s*(?:n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Trailing format hint on TreatmentArm — drug name followed by % or n(%).
        /// Matches: "MYCAPSSA %", "PLACEBO %", "Drug n(%)", "Paroxetine n (%)"
        /// Captures: Group 1 = drug name, Group 2 = format hint
        /// </summary>
        private static readonly Regex _trailingFormatHintPattern = new(
            @"^(.+?)\s+(n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Bracketed N= value at end of TreatmentArm — drug/dose followed by [N=xxx].
        /// Matches: "Placebo [N=459]", "75 mg/day [N=77]", "All PGB [N=979]",
        ///          "600 mg/day [N=369]", "Drug [n=100]"
        /// Captures: Group 1 = text before bracket, Group 2 = N value
        /// </summary>
        private static readonly Regex _bracketedNPattern = new(
            @"^(.+?)\s*\[\s*[Nn]\s*=\s*(\d[\d,]*)\s*\]\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Standalone [N=xxx] as whole value — e.g., "[N=60]", "[ n = 34 ]".
        /// Complements <see cref="_nValuePattern"/> which handles parenthesized/bare forms.
        /// Captures: Group 1 = N value.
        /// </summary>
        private static readonly Regex _standaloneBracketNPattern = new(
            @"^\[\s*[Nn]\s*=\s*(\d[\d,]*)\s*\]$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Inline (N=xxx) or [N=xxx] embedded anywhere in text — e.g., "(n=963)", "[N=60]" mid-string.
        /// Used by the universal N= pre-pass to strip sample-size annotations from any column.
        /// Captures: Group 1 = N value.
        /// </summary>
        /// <seealso cref="_nValuePattern"/>
        /// <seealso cref="_standaloneBracketNPattern"/>
        /// <seealso cref="_bracketedNPattern"/>
        private static readonly Regex _inlineNPattern = new(
            @"[\(\[]\s*[Nn]\s*=\s*(\d[\d,]*)\s*[\)\]]",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Bare trailing N= sample-size annotation — no surrounding brackets or parens.
        /// Matches values like "60–89 mL per minute N=10", "Postpartum (6–12 weeks) N=6",
        /// "Adults given 50 mg once daily for 7 days N=12", "Abdomen N=113".
        /// </summary>
        /// <remarks>
        /// End-anchored so it only fires when N=X sits at the trailing edge of the value
        /// (the observed pattern across 83+ DoseRegimen / StudyContext rows). The leading
        /// `\s+` requirement prevents the regex from biting into a value that happens to
        /// end with `…N=12` as part of a larger token.
        /// Captures: Group 1 = N value.
        /// </remarks>
        /// <seealso cref="_inlineNPattern"/>
        /// <seealso cref="tryStripBareInlineN"/>
        private static readonly Regex _bareInlineNPattern = new(
            @"\s+[Nn]\s*=\s*(\d[\d,]*)\b\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Trailing N= in RawValue — preceded by optional footnote markers (^, *, †, ‡)
        /// or whitespace. Captures the N value from the end of composite RawValue strings.
        /// Matches: "2.9 (22%) N=16", "94.7 (34%)^N=14", "362.5 (58%)*N=14", "0.29 (35%) N=8"
        /// Captures: Group 1 = everything before the N= portion, Group 2 = N value.
        /// </summary>
        /// <seealso cref="_inlineNPattern"/>
        /// <seealso cref="normalizeInlineNValues"/>
        private static readonly Regex _rawValueTrailingNPattern = new(
            @"^(.+?)\s*[*^†‡]?\s*[Nn]\s*=\s*(\d[\d,]*)\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Trailing parenthesized content at end of ParameterSubtype — extracts unit from
        /// values like "Cmax(pg/mL)", "AUC120(pg·hr/mL)", "Cmax(serum, mcg/mL)", "Tmax(hr)".
        /// Captures: Group 1 = inner text between parentheses.
        /// </summary>
        /// <seealso cref="extractUnitFromParameterSubtype"/>
        private static readonly Regex _subtypeTrailingParenPattern = new(
            @"\(([^)]+)\)\s*$",
            RegexOptions.Compiled);

        // R10 DRY — structural PK unit regex moved to
        // Dictionaries.UnitDictionary.PkUnitStructurePattern (same pattern,
        // shared with PkTableParser). Callers previously using
        // _pkUnitStructurePattern.IsMatch(x) now call
        // UnitDictionary.PkUnitStructurePattern.IsMatch(x) directly.

        /**************************************************************/
        /// <summary>
        /// "All" prefix on drug/arm names — e.g., "All PGB", "All Doses".
        /// Used to strip "All" prefix when recovering drug name after bracket extraction.
        /// Captures: Group 1 = the actual name after "All".
        /// </summary>
        private static readonly Regex _allPrefixPattern = new(
            @"^All\s+(.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects percentage-related keywords in free-text columns (TreatmentArm,
        /// ParameterName, ParameterCategory, ParameterSubtype).
        /// Matches: "%", "percent", "proportion", "incidence", "rate of", "frequency".
        /// </summary>
        /// <seealso cref="correctCountToPercentageType"/>
        private static readonly Regex _percentageHintPattern = new(
            @"%|percent|proportion|incidence|rate\s+of|frequency",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Regex Patterns

        #region Content Classification

        /**************************************************************/
        /// <summary>
        /// Classification of what type of data a string value represents.
        /// Used to detect misplaced values and determine correct column assignment.
        /// </summary>
        private enum ContentType
        {
            /// <summary>Could not be classified.</summary>
            Unknown,
            /// <summary>Matches a known drug/product name.</summary>
            DrugName,
            /// <summary>Dose number + units, optional schedule/route.</summary>
            DoseRegimen,
            /// <summary>Sample size in N=xxx or (N=xxx) format.</summary>
            SampleSizeN,
            /// <summary>Format descriptor like %, #, n(%).</summary>
            FormatHint,
            /// <summary>Severity qualifier like "Severe", "Grades 3/4".</summary>
            SeverityGrade,
            /// <summary>Study/trial name.</summary>
            StudyName,
            /// <summary>Column descriptor like "% of Patients", "Incidence".</summary>
            DescriptorHint,
            /// <summary>Bare integer with no units.</summary>
            BareNumber,
            /// <summary>Drug name followed by dose regimen.</summary>
            DrugPlusDose,
            /// <summary>Arm/drug name followed by N=xxx.</summary>
            ArmNameWithN
        }

        #endregion Content Classification

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the column standardization service.
        /// </summary>
        /// <param name="dbContext">Database context for drug name dictionary loading.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public ColumnStandardizationService(
            DbContext dbContext,
            ILogger<ColumnStandardizationService> logger,
            IAeParameterCategoryDictionaryService? aeDictionary = null)
        {
            #region implementation

            _dbContext = dbContext;
            _logger = logger;
            _aeDictionary = aeDictionary;

            #endregion
        }

        #endregion Constructor

        #region IColumnStandardizationService Implementation

        /**************************************************************/
        /// <summary>
        /// Loads the drug name dictionary from vw_ProductsByIngredient.
        /// Populates both exact-match and first-word partial-match sets.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            #region implementation

            if (_initialized)
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Load distinct ProductName and SubstanceName values
            var productNames = await _dbContext
                .Set<LabelView.ProductsByIngredient>()
                .AsNoTracking()
                .Where(p => p.ProductName != null)
                .Select(p => p.ProductName!)
                .Distinct()
                .ToListAsync(ct);

            var substanceNames = await _dbContext
                .Set<LabelView.ProductsByIngredient>()
                .AsNoTracking()
                .Where(p => p.SubstanceName != null)
                .Select(p => p.SubstanceName!)
                .Distinct()
                .ToListAsync(ct);

            // Build exact-match dictionary
            _drugNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in productNames)
                _drugNames.Add(name.Trim());
            foreach (var name in substanceNames)
                _drugNames.Add(name.Trim());

            // Add known abbreviations
            foreach (var abbr in _knownAbbreviations.Keys)
                _drugNames.Add(abbr);

            // Also add common comparator names that may not be in the product DB
            _drugNames.Add("Placebo");
            _drugNames.Add("Vehicle");
            _drugNames.Add("Control");
            _drugNames.Add("Active Control");

            // Build first-word index for partial matching
            _drugFirstWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in _drugNames)
            {
                var firstWord = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstWord != null && firstWord.Length >= 3)
                    _drugFirstWords.Add(firstWord);
            }

            sw.Stop();
            _logger.LogInformation(
                "Column standardization dictionary loaded: {Count} drug names, {WordCount} first-words in {Ms}ms",
                _drugNames.Count, _drugFirstWords.Count, sw.ElapsedMilliseconds);

            _initialized = true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies 4-phase column standardization to all observations. Processes ALL table
        /// categories (except SKIP). Modifies observations in-place.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.</param>
        /// <returns>The same list with corrected column assignments and validation flags appended.</returns>
        public List<ParsedObservation> Standardize(List<ParsedObservation> observations)
        {
            #region implementation

            if (!_initialized)
            {
                _logger.LogWarning("ColumnStandardizationService not initialized — skipping standardization");
                return observations;
            }

            int correctionCount = 0;

            foreach (var obs in observations)
            {
                // Skip non-processable categories
                if (string.Equals(obs.TableCategory, "SKIP", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip comparison/stat rows
                if (string.Equals(obs.TreatmentArm, "Comparison", StringComparison.OrdinalIgnoreCase))
                    continue;

                int obsCorrectionCount = 0;

                // Phase 1: Arm/context corrections (AE + EFFICACY only — existing Rules 1-11)
                if (isPhase1Category(obs.TableCategory))
                {
                    obsCorrectionCount += applyPhase1_ArmContextCorrections(obs);
                }

                // Phase 2: Content normalization (ALL categories)
                obsCorrectionCount += applyPhase2_ContentNormalization(obs);

                // Phase 3: PrimaryValueType migration (ALL categories)
                obsCorrectionCount += applyPhase3_PrimaryValueTypeMigration(obs);

                // Phase 4: Column contract enforcement (ALL categories)
                obsCorrectionCount += applyPhase4_ColumnContractEnforcement(obs);

                correctionCount += obsCorrectionCount;

                // Confidence provenance flag
                var reason = obsCorrectionCount == 0 ? "clean" : obsCorrectionCount <= 2 ? "minor" : "major";
                obs.AppendValidationFlag($"CONFIDENCE:PATTERN:{obs.ParseConfidence ?? 0:F2}:{reason}({obsCorrectionCount})");
            }

            if (correctionCount > 0)
            {
                _logger.LogDebug("Column standardization applied {Count} corrections to {Total} observations",
                    correctionCount, observations.Count);
            }

            // Batch-level pass: backfill placebo arms with Dose=0, DoseUnit inherited
            // from non-placebo arms in the same table (requires all per-obs corrections complete)
            DoseExtractor.BackfillPlaceboArms(observations);

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3.6 post-processing: re-applies targeted extraction rules after Claude correction.
        /// Catches units and N-values that Claude may have corrected into extractable form.
        /// Flags use <c>COL_STD:POST_</c> prefix to distinguish from Phase 2 corrections.
        /// </summary>
        /// <param name="observations">Observations after all correction stages.</param>
        /// <returns>The same list with additional extractions applied.</returns>
        /// <seealso cref="Standardize"/>
        /// <seealso cref="extractUnitFromParameterSubtype"/>
        /// <seealso cref="normalizeInlineNValues"/>
        public List<ParsedObservation> PostProcessExtraction(List<ParsedObservation> observations)
        {
            #region implementation

            int extractionCount = 0;

            foreach (var obs in observations)
            {
                // Skip non-processable categories
                if (string.Equals(obs.TableCategory, "SKIP", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Re-try unit extraction from ParameterSubtype (may now have extractable values after Claude)
                var preFlags = obs.ValidationFlags;
                if (extractUnitFromParameterSubtype(obs))
                {
                    // Replace the Phase 2 flag with the POST_ prefixed version if it was just added
                    if (obs.ValidationFlags != preFlags &&
                        obs.ValidationFlags != null &&
                        obs.ValidationFlags.Contains("COL_STD:PK_SUBPARAM_UNIT_EXTRACTED") &&
                        (preFlags == null || !preFlags.Contains("COL_STD:PK_SUBPARAM_UNIT_EXTRACTED")))
                    {
                        obs.ValidationFlags = obs.ValidationFlags.Replace(
                            "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED",
                            "COL_STD:POST_PK_SUBPARAM_UNIT_EXTRACTED");
                    }
                    extractionCount++;
                }

                // Re-try inline N= extraction (Claude may have restored N= values)
                preFlags = obs.ValidationFlags;
                if (normalizeInlineNValues(obs))
                {
                    // Replace N_STRIPPED flags with POST_ prefixed versions for any new flags
                    if (obs.ValidationFlags != preFlags &&
                        obs.ValidationFlags != null)
                    {
                        // Only replace flags that were just added (not already present before this call)
                        var newPart = preFlags != null
                            ? obs.ValidationFlags.Substring(preFlags.Length)
                            : obs.ValidationFlags;
                        if (newPart.Contains("COL_STD:N_STRIPPED"))
                        {
                            var updatedPart = newPart.Replace("COL_STD:N_STRIPPED", "COL_STD:POST_N_STRIPPED");
                            obs.ValidationFlags = preFlags != null
                                ? preFlags + updatedPart
                                : updatedPart;
                        }
                    }
                    extractionCount++;
                }

                // Correct Count → Percentage when contextual fields contain percentage keywords
                if (correctCountToPercentageType(obs))
                {
                    extractionCount++;
                }
            }

            if (extractionCount > 0)
            {
                _logger.LogDebug("Post-processing extracted {Count} additional values from {Total} observations",
                    extractionCount, observations.Count);
            }

            return observations;

            #endregion
        }

        #endregion IColumnStandardizationService Implementation

        #region Phase 1: Arm/Context Corrections

        /**************************************************************/
        /// <summary>
        /// Phase 1: Applies the original 11 arm/context correction rules.
        /// Only runs for ADVERSE_EVENT and EFFICACY categories.
        /// </summary>
        /// <returns>Number of corrections applied.</returns>
        private int applyPhase1_ArmContextCorrections(ParsedObservation obs)
        {
            #region implementation

            int corrections = 0;

            // Rule 11 (structural): bracketed [N=xxx] in TreatmentArm — runs first
            if (applyRule11_ArmHasBracketedN(obs))
                corrections++;

            var armType = classifyContent(obs.TreatmentArm);
            var ctxType = classifyContent(obs.StudyContext);

            // Apply rules in priority order (most specific first)
            if (applyRule1_ArmIsN(obs, armType, ctxType) ||
                applyRule2_ArmIsFormatHint(obs, armType, ctxType) ||
                applyRule3_ArmIsSeverity(obs, armType, ctxType) ||
                applyRule4_ArmIsDose(obs, armType, ctxType) ||
                applyRule5_ArmIsBareNumber(obs, armType, ctxType) ||
                applyRule6_ArmIsDrugPlusDose(obs, armType))
            {
                corrections++;
                ctxType = classifyContent(obs.StudyContext);
            }

            // Context rules (can apply independently or after arm correction)
            if (applyRule7_CtxIsArmWithN(obs, ctxType))
                corrections++;

            if (applyRule8_CtxIsDrugName(obs, ctxType))
                corrections++;

            if (applyRule9_CtxIsDescriptor(obs, ctxType))
                corrections++;

            // Rule 10: Strip trailing % from arm name
            if (applyRule10_ArmHasTrailingPercent(obs))
                corrections++;

            return corrections;

            #endregion
        }

        #endregion Phase 1: Arm/Context Corrections

        #region Rule Methods

        /**************************************************************/
        /// <summary>
        /// Rule 1: TreatmentArm contains an N= value (e.g., "(N=267)", "N=677").
        /// Moves parsed N to ArmN. If StudyContext contains a drug name, moves it to TreatmentArm.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule1_ArmIsN(ParsedObservation obs, ContentType armType, ContentType ctxType)
        {
            #region implementation

            if (armType != ContentType.SampleSizeN)
                return false;

            // Parse N value from TreatmentArm
            var nMatch = _nValuePattern.Match(obs.TreatmentArm!);
            if (nMatch.Success && tryParseNValue(nMatch.Groups[1].Value, out var n))
            {
                obs.ArmN = n;
            }

            // Try to recover arm name from StudyContext
            if (ctxType == ContentType.DrugName || isDrugName(obs.StudyContext))
            {
                obs.TreatmentArm = obs.StudyContext;
                obs.StudyContext = null;
            }
            else
            {
                obs.TreatmentArm = null;
            }

            obs.AppendValidationFlag("COL_STD:ARM_WAS_N");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 2: TreatmentArm contains a format hint (e.g., "%", "#", "n(%)").
        /// Discards the hint. If StudyContext contains a drug name, moves it to TreatmentArm.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule2_ArmIsFormatHint(ParsedObservation obs, ContentType armType, ContentType ctxType)
        {
            #region implementation

            if (armType != ContentType.FormatHint)
                return false;

            // Try to recover arm name from StudyContext
            if (ctxType == ContentType.DrugName || isDrugName(obs.StudyContext))
            {
                obs.TreatmentArm = obs.StudyContext;
                obs.StudyContext = null;
            }
            else
            {
                obs.TreatmentArm = null;
            }

            obs.AppendValidationFlag("COL_STD:ARM_WAS_FMT");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 3: TreatmentArm contains a severity grade (e.g., "Severe", "Grades 3/4").
        /// Moves it to ParameterSubtype. Attempts to recover arm from StudyContext.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule3_ArmIsSeverity(ParsedObservation obs, ContentType armType, ContentType ctxType)
        {
            #region implementation

            if (armType != ContentType.SeverityGrade)
                return false;

            // Move severity to ParameterSubtype (only if not already populated)
            if (string.IsNullOrEmpty(obs.ParameterSubtype))
            {
                obs.ParameterSubtype = obs.TreatmentArm;
            }

            // Try to recover arm name from StudyContext
            if (ctxType == ContentType.DrugName || isDrugName(obs.StudyContext))
            {
                obs.TreatmentArm = obs.StudyContext;
                obs.StudyContext = null;
            }
            else
            {
                obs.TreatmentArm = null;
            }

            obs.AppendValidationFlag("COL_STD:ARM_WAS_SEVERITY");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 4: TreatmentArm contains a pure dose regimen (e.g., "10 mg daily subcutaneously").
        /// Moves it to DoseRegimen. Attempts to extract drug name from StudyContext.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule4_ArmIsDose(ParsedObservation obs, ContentType armType, ContentType ctxType)
        {
            #region implementation

            if (armType != ContentType.DoseRegimen)
                return false;

            // Move dose to DoseRegimen
            if (string.IsNullOrEmpty(obs.DoseRegimen))
            {
                obs.DoseRegimen = obs.TreatmentArm;
            }

            // Try to extract drug name from StudyContext
            if (ctxType == ContentType.DrugName || isDrugName(obs.StudyContext))
            {
                obs.TreatmentArm = obs.StudyContext;
                obs.StudyContext = null;
            }
            else
            {
                // Check if StudyContext is a dose descriptor ("Dosing Regimen", "Dofetilide Capsules Dose")
                var descMatch = _doseDescriptorPattern.Match(obs.StudyContext ?? "");
                if (descMatch.Success)
                {
                    var drugPart = descMatch.Groups[1].Value.Trim();
                    if (isDrugName(drugPart))
                    {
                        obs.TreatmentArm = drugPart;
                        obs.StudyContext = null;
                    }
                    else
                    {
                        // Use drug dictionary match against ProductTitle as fallback
                        obs.TreatmentArm = resolveDrugNameFromProductTitle(obs.ProductTitle);
                        obs.StudyContext = null;
                    }
                }
                else
                {
                    obs.TreatmentArm = null;
                }
            }

            obs.AppendValidationFlag("COL_STD:ARM_WAS_DOSE");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 5: TreatmentArm is a bare number (e.g., "200") and StudyContext contains
        /// a dose descriptor (e.g., "Target Topiramate Tablets Dosage (mg/day)").
        /// Reconstructs DoseRegimen from number + unit, extracts drug name from descriptor.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule5_ArmIsBareNumber(ParsedObservation obs, ContentType armType, ContentType ctxType)
        {
            #region implementation

            if (armType != ContentType.BareNumber)
                return false;

            var descMatch = _doseDescriptorPattern.Match(obs.StudyContext ?? "");
            if (!descMatch.Success)
                return false;

            var drugPart = descMatch.Groups[1].Value.Trim();
            var unitPart = descMatch.Groups[2].Success ? descMatch.Groups[2].Value.Trim() : null;

            // Reconstruct DoseRegimen: "200" + "mg" → "200 mg" (with /day if unit had it)
            if (!string.IsNullOrEmpty(unitPart))
            {
                obs.DoseRegimen = $"{obs.TreatmentArm} {unitPart}/day";
            }
            else
            {
                obs.DoseRegimen = obs.TreatmentArm;
            }

            // Extract drug name
            if (isDrugName(drugPart))
            {
                obs.TreatmentArm = drugPart;
            }
            else
            {
                // Remove "Tablets", "Capsules" etc. and try again
                var cleanDrug = Regex.Replace(drugPart, @"\s+(?:Tablets?|Capsules?|Oral\s+Solution)\s*$", "",
                    RegexOptions.IgnoreCase).Trim();
                obs.TreatmentArm = isDrugName(cleanDrug) ? cleanDrug : drugPart;
            }

            obs.StudyContext = null;
            obs.AppendValidationFlag("COL_STD:ARM_WAS_BARE_DOSE");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 6: TreatmentArm contains drug name + dose combined
        /// (e.g., "Mycophenolate Mofetil 2g/day"). Splits the drug name and dose.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule6_ArmIsDrugPlusDose(ParsedObservation obs, ContentType armType)
        {
            #region implementation

            if (armType != ContentType.DrugPlusDose)
                return false;

            // Only split if DoseRegimen is not already populated
            if (!string.IsNullOrEmpty(obs.DoseRegimen))
                return false;

            var doseMatch = _trailingDosePattern.Match(obs.TreatmentArm!);
            if (!doseMatch.Success)
                return false;

            var drugPart = obs.TreatmentArm![..doseMatch.Index].Trim();
            var dosePart = doseMatch.Groups[1].Value.Trim();

            // Verify the drug part is actually a drug name
            if (!isDrugName(drugPart))
                return false;

            obs.TreatmentArm = drugPart;
            obs.DoseRegimen = dosePart;

            obs.AppendValidationFlag("COL_STD:SPLIT_DRUG_DOSE");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 7: StudyContext contains an arm name with embedded N=
        /// (e.g., "Doxazosin N=339", "KANUMA N = 36", "Control Arm (N=18) n (%)").
        /// Splits drug name → TreatmentArm, N → ArmN, clears StudyContext.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule7_CtxIsArmWithN(ParsedObservation obs, ContentType ctxType)
        {
            #region implementation

            if (ctxType != ContentType.ArmNameWithN)
                return false;

            // Try the "(N=xx) n(%)" pattern first
            var hintMatch = _ctxArmWithNAndHintPattern.Match(obs.StudyContext!);
            if (hintMatch.Success)
            {
                var drugPart = hintMatch.Groups[1].Value.Trim();
                if (tryParseNValue(hintMatch.Groups[2].Value, out var n))
                    obs.ArmN = n;

                // Overwrite TreatmentArm unless it's already a valid drug name
                var currentArmType = classifyContent(obs.TreatmentArm);
                if (currentArmType != ContentType.DrugName)
                    obs.TreatmentArm = drugPart;

                obs.StudyContext = null;
                obs.AppendValidationFlag("COL_STD:CTX_WAS_ARM_N");
                return true;
            }

            // Try the simple "Drug N=xxx" pattern
            var embMatch = _embeddedNPattern.Match(obs.StudyContext!);
            if (embMatch.Success)
            {
                var drugPart = embMatch.Groups[1].Value.Trim();
                if (tryParseNValue(embMatch.Groups[2].Value, out var n))
                    obs.ArmN = n;

                // Overwrite TreatmentArm unless it's already a valid drug name
                var currentArmType = classifyContent(obs.TreatmentArm);
                if (currentArmType != ContentType.DrugName)
                    obs.TreatmentArm = drugPart;

                obs.StudyContext = null;
                obs.AppendValidationFlag("COL_STD:CTX_WAS_ARM_N");
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 8: StudyContext contains a drug name and TreatmentArm does not.
        /// Swaps StudyContext → TreatmentArm when the arm is misclassified.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule8_CtxIsDrugName(ParsedObservation obs, ContentType ctxType)
        {
            #region implementation

            if (ctxType != ContentType.DrugName)
                return false;

            // Only swap if current TreatmentArm is NOT a drug name
            var armType = classifyContent(obs.TreatmentArm);
            if (armType == ContentType.DrugName || armType == ContentType.DrugPlusDose)
                return false;

            // Swap
            var temp = obs.TreatmentArm;
            obs.TreatmentArm = obs.StudyContext;
            obs.StudyContext = null; // Don't keep the old arm value as StudyContext

            obs.AppendValidationFlag("COL_STD:SWAP_ARM_CTX");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 9: StudyContext contains a descriptor hint (e.g., "Incidence", "Reaction")
        /// or a format hint (e.g., "% of Patients", "N (%)").
        /// Clears StudyContext since these are column format/metric descriptors, not study names.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule9_CtxIsDescriptor(ParsedObservation obs, ContentType ctxType)
        {
            #region implementation

            if (ctxType != ContentType.DescriptorHint && ctxType != ContentType.FormatHint)
                return false;

            obs.StudyContext = null;
            obs.AppendValidationFlag("COL_STD:CTX_WAS_DESC");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 10: TreatmentArm has a trailing format hint (e.g., "MYCAPSSA %", "PLACEBO %",
        /// "Drug n(%)"). Strips the hint from the arm name and promotes PrimaryValueType
        /// from "Numeric" to "Percentage" when the hint contains %.
        /// </summary>
        /// <remarks>
        /// This handles cases where the parser's <c>_trailingFormatHintPattern</c> failed to
        /// strip the hint (e.g., due to non-breaking spaces or other whitespace variations),
        /// or where the format hint was stripped but the type promotion was not applied.
        /// </remarks>
        /// <returns>True if a correction was applied.</returns>
        private static bool applyRule10_ArmHasTrailingPercent(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.TreatmentArm))
                return false;

            var hintMatch = _trailingFormatHintPattern.Match(obs.TreatmentArm);
            if (!hintMatch.Success)
                return false;

            var cleanName = hintMatch.Groups[1].Value.Trim();
            var hint = hintMatch.Groups[2].Value.Trim();

            // Strip the format hint from the arm name
            obs.TreatmentArm = cleanName;

            // Promote Numeric → Percentage when the hint contains %
            if (hint.Contains('%') &&
                string.Equals(obs.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase))
            {
                obs.PrimaryValueType = "Percentage";
                obs.Unit = "%";
            }

            obs.AppendValidationFlag("COL_STD:ARM_STRIP_PCT");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Rule 11: TreatmentArm contains a bracketed [N=xxx] value, optionally preceded
        /// by a drug name, dose regimen, or "All Drug" prefix.
        /// Extracts N → ArmN, identifies the remaining text as drug name or dose,
        /// and routes accordingly. When TreatmentArm resolves to a dose (not a drug),
        /// the drug name is recovered from the drug dictionary by matching against
        /// the observation's ProductTitle.
        /// </summary>
        /// <remarks>
        /// Examples:
        /// - "Placebo [N=459]" → TreatmentArm="Placebo", ArmN=459
        /// - "75 mg/day [N=77]" → DoseRegimen="75 mg/day", ArmN=77, TreatmentArm from dictionary
        /// - "All PGB [N=979]" → TreatmentArm="PGB" (abbreviation), ArmN=979
        /// - "600 mg/day [N=369]" → DoseRegimen="600 mg/day", ArmN=369, TreatmentArm from dictionary
        ///
        /// Drug name recovery searches the drug dictionary for a name that appears
        /// as a substring of ProductTitle (e.g., ProductTitle="LYRICA- pregabalin capsule"
        /// matches dictionary entry "pregabalin" or "LYRICA").
        /// </remarks>
        /// <returns>True if a correction was applied.</returns>
        private bool applyRule11_ArmHasBracketedN(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.TreatmentArm))
                return false;

            var bracketMatch = _bracketedNPattern.Match(obs.TreatmentArm);
            if (!bracketMatch.Success)
                return false;

            var textBeforeBracket = bracketMatch.Groups[1].Value.Trim();
            if (tryParseNValue(bracketMatch.Groups[2].Value, out var n))
            {
                obs.ArmN = n;
            }

            // Strip "All" prefix if present (e.g., "All PGB" → "PGB")
            var allMatch = _allPrefixPattern.Match(textBeforeBracket);
            var coreText = allMatch.Success ? allMatch.Groups[1].Value.Trim() : textBeforeBracket;

            // Classify what's left: is it a drug name or a dose regimen?
            if (isDrugName(coreText))
            {
                // Drug name — keep in TreatmentArm
                obs.TreatmentArm = coreText;
            }
            else if (_pureDosePattern.IsMatch(coreText))
            {
                // Dose regimen — move to DoseRegimen, recover drug name from dictionary
                if (string.IsNullOrEmpty(obs.DoseRegimen))
                {
                    obs.DoseRegimen = coreText;
                }

                obs.TreatmentArm = resolveDrugNameFromProductTitle(obs.ProductTitle);
            }
            else
            {
                // Unknown content — try partial drug name match, else leave as-is
                var resolved = resolveDrugNameFromProductTitle(obs.ProductTitle);
                if (resolved != null)
                {
                    // If the remaining text looks like it could be a dose (has numbers),
                    // move it to DoseRegimen
                    if (Regex.IsMatch(coreText, @"\d") && string.IsNullOrEmpty(obs.DoseRegimen))
                    {
                        obs.DoseRegimen = coreText;
                    }
                    obs.TreatmentArm = resolved;
                }
                else
                {
                    obs.TreatmentArm = coreText;
                }
            }

            obs.AppendValidationFlag("COL_STD:ARM_BRACKET_N");
            return true;

            #endregion
        }

        #endregion Rule Methods

        #region Inline N= Helpers

        /**************************************************************/
        /// <summary>
        /// Attempts to extract and strip an N= sample-size annotation from a column value.
        /// Three-tier check: (1) standalone (N=xxx) via <see cref="_nValuePattern"/>,
        /// (2) standalone [N=xxx] via <see cref="_standaloneBracketNPattern"/>,
        /// (3) embedded (N=xxx) or [N=xxx] via <see cref="_inlineNPattern"/>.
        /// </summary>
        /// <param name="val">The raw column value to inspect.</param>
        /// <param name="cleaned">The value after stripping N=, or null if the entire value was N=.</param>
        /// <param name="n">The extracted sample-size integer.</param>
        /// <returns>True if an N= pattern was found and stripped.</returns>
        /// <seealso cref="normalizeInlineNValues"/>
        private bool tryStripInlineN(string? val, out string? cleaned, out int n)
        {
            #region implementation

            cleaned = val;
            n = 0;
            if (string.IsNullOrWhiteSpace(val)) return false;

            var trimmed = val.Trim();

            // Check 1: Whole value is N=xxx or (N=xxx)
            var nMatch = _nValuePattern.Match(trimmed);
            if (nMatch.Success && tryParseNValue(nMatch.Groups[1].Value, out n))
            {
                cleaned = null;
                return true;
            }

            // Check 2: Whole value is [N=xxx]
            var sqMatch = _standaloneBracketNPattern.Match(trimmed);
            if (sqMatch.Success && tryParseNValue(sqMatch.Groups[1].Value, out n))
            {
                cleaned = null;
                return true;
            }

            // Check 3: N= embedded anywhere as (N=xxx) or [N=xxx] — strip all occurrences,
            // take the first match's number
            var inlineMatch = _inlineNPattern.Match(trimmed);
            if (inlineMatch.Success && tryParseNValue(inlineMatch.Groups[1].Value, out n))
            {
                var stripped = _inlineNPattern.Replace(trimmed, " ").Trim();
                // Collapse internal double-spaces and trailing punctuation artifacts
                stripped = Regex.Replace(stripped, @"\s{2,}", " ").Trim();
                cleaned = string.IsNullOrWhiteSpace(stripped) ? null : stripped;
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tier-4 fallback to <see cref="tryStripInlineN"/> for bare trailing N= forms
        /// without surrounding brackets or parens — e.g., "Postpartum (6–12 weeks) N=6",
        /// "Adults given 50 mg once daily for 7 days N=12", "Abdomen N=113". Called only
        /// when the bracket/paren-aware tiers all miss, to avoid double-stripping.
        /// </summary>
        /// <remarks>
        /// End-anchored via <see cref="_bareInlineNPattern"/> to keep the regex away from
        /// arbitrary mid-string prose ("N=12 patients in cohort A" stays untouched).
        /// </remarks>
        /// <example>
        /// <code>
        /// tryStripBareInlineN("60–89 mL per minute N=10", out var c, out var n);
        /// // → c = "60–89 mL per minute", n = 10, returns true
        /// </code>
        /// </example>
        /// <param name="val">The raw column value to inspect.</param>
        /// <param name="cleaned">The value after stripping the bare N= suffix, or null if the entire value was N=.</param>
        /// <param name="n">The extracted sample-size integer.</param>
        /// <returns>True if a bare trailing N= pattern was found and stripped.</returns>
        /// <seealso cref="tryStripInlineN"/>
        /// <seealso cref="_bareInlineNPattern"/>
        /// <seealso cref="normalizeInlineNValues"/>
        private bool tryStripBareInlineN(string? val, out string? cleaned, out int n)
        {
            #region implementation

            cleaned = val;
            n = 0;
            if (string.IsNullOrWhiteSpace(val)) return false;

            var trimmed = val.Trim();

            var match = _bareInlineNPattern.Match(trimmed);
            if (!match.Success) return false;
            if (!tryParseNValue(match.Groups[1].Value, out n)) return false;

            // Strip the matched bare-N suffix (e.g., " N=10") from the trailing edge.
            var stripped = _bareInlineNPattern.Replace(trimmed, "").Trim();
            // Collapse internal double-spaces left behind by the strip.
            stripped = Regex.Replace(stripped, @"\s{2,}", " ").Trim();
            cleaned = string.IsNullOrWhiteSpace(stripped) ? null : stripped;
            return true;

            #endregion
        }

        #endregion Inline N= Helpers

        #region Classification Methods

        /**************************************************************/
        /// <summary>
        /// Classifies what type of content a string value represents.
        /// Priority order ensures most specific patterns match first.
        /// </summary>
        /// <param name="value">The string value to classify.</param>
        /// <returns>The detected content type.</returns>
        private ContentType classifyContent(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
                return ContentType.Unknown;

            var trimmed = value.Trim();

            // 1. Format hint (%, #, n(%)) — most specific structural pattern
            if (_formatHintPattern.IsMatch(trimmed))
                return ContentType.FormatHint;

            // 2. N= value ((N=267), N=677)
            if (_nValuePattern.IsMatch(trimmed))
                return ContentType.SampleSizeN;

            // 3. Severity grade (Severe, Grades 3/4)
            if (_severityPattern.IsMatch(trimmed))
                return ContentType.SeverityGrade;

            // 4. Pure dose regimen (10 mg, 500 mcg BID)
            if (_pureDosePattern.IsMatch(trimmed))
                return ContentType.DoseRegimen;

            // 5. Embedded N in text AND leading part is a drug name → ArmNameWithN
            var embMatch = _embeddedNPattern.Match(trimmed);
            if (embMatch.Success && isDrugName(embMatch.Groups[1].Value.Trim()))
                return ContentType.ArmNameWithN;

            // 5b. Also check "(N=xx) n(%)" pattern for StudyContext
            if (_ctxArmWithNAndHintPattern.IsMatch(trimmed))
                return ContentType.ArmNameWithN;

            // 6. Exact drug dictionary match
            if (_drugNames.Contains(trimmed))
                return ContentType.DrugName;

            // 7. Drug + trailing dose → DrugPlusDose
            var doseMatch = _trailingDosePattern.Match(trimmed);
            if (doseMatch.Success)
            {
                var leadingPart = trimmed[..doseMatch.Index].Trim();
                if (isDrugName(leadingPart))
                    return ContentType.DrugPlusDose;
            }

            // 8. Descriptor hint (% of Patients, Incidence, Dosing Regimen)
            if (_descriptorHintPattern.IsMatch(trimmed))
                return ContentType.DescriptorHint;

            // 9. Dose descriptor ("Target Topiramate Tablets Dosage (mg/day)")
            if (_doseDescriptorPattern.IsMatch(trimmed))
                return ContentType.DescriptorHint;

            // 10. Study keyword match
            if (_studyPattern.IsMatch(trimmed))
                return ContentType.StudyName;

            // 11. Bare number (200, 600)
            if (_bareNumberPattern.IsMatch(trimmed))
                return ContentType.BareNumber;

            // 12. Partial drug name match (first word in drug dictionary)
            var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstWord != null && _drugFirstWords.Contains(firstWord) && firstWord.Length >= 3)
                return ContentType.DrugName;

            return ContentType.Unknown;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a string is a known drug name (exact match, abbreviation, or first-word match).
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns>True if the value matches a known drug name.</returns>
        private bool isDrugName(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();

            // Exact match
            if (_drugNames.Contains(trimmed))
                return true;

            // Known abbreviation
            if (_knownAbbreviations.ContainsKey(trimmed))
                return true;

            // First-word partial match (for multi-word drug names)
            // But NOT if the string contains N= patterns or numbers that indicate it's a composite value
            var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstWord != null && firstWord.Length >= 3 && _drugFirstWords.Contains(firstWord))
            {
                // Reject partial match if string contains embedded N= or is clearly a composite
                if (!_embeddedNPattern.IsMatch(trimmed) && !_nValuePattern.IsMatch(trimmed))
                    return true;
            }

            return false;

            #endregion
        }

        #endregion Classification Methods

        #region Phase 2: Content Normalization

        /**************************************************************/
        /// <summary>
        /// Phase 2: Applies content normalization across ALL table categories.
        /// Runs 6 sub-passes: inline N= stripping, DoseRegimen triage, ParameterName cleanup,
        /// TreatmentArm cleanup, Unit scrub, and SOC mapping.
        /// </summary>
        /// <returns>Number of corrections applied.</returns>
        /// <seealso cref="normalizeInlineNValues"/>
        private int applyPhase2_ContentNormalization(ParsedObservation obs)
        {
            #region implementation

            int corrections = 0;

            if (normalizeInlineNValues(obs)) corrections++;
            if (normalizeDoseRegimen(obs)) corrections++;
            if (normalizeParameterName(obs)) corrections++;
            if (normalizeTreatmentArm(obs)) corrections++;
            if (extractUnitFromParameterSubtype(obs)) corrections++;
            // PK post-parse canonicalization MUST run after unit extraction so the
            // embedded unit (e.g., "AUC0-∞(mcg·hr/mL)") gets pulled out before the
            // Name ↔ Subtype swap moves the PK term out of the Subtype field.
            if (applyPkCanonicalization(obs)) corrections++;
            if (normalizeUnit(obs)) corrections++;
            if (normalizeParameterCategory(obs)) corrections++;

            // Dictionary-based SOC resolution for NULL ParameterCategory (AE only).
            // Runs after normalizeParameterCategory so existing non-NULL categories
            // are normalized first; only fills in genuinely missing categories.
            if (_aeDictionary != null && _aeDictionary.TryResolveObservation(obs)) corrections++;

            // Final sub-pass: scan all columns for misplaced dose patterns.
            // Runs last so all column movements/cleanups have settled first.
            if (DoseExtractor.ScanAllColumnsForDose(obs)) corrections++;

            return corrections;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 pre-pass: strips N= patterns from every non-RawValue column and
        /// populates <see cref="ParsedObservation.ArmN"/>. Handles (N=xxx), [N=xxx],
        /// and standalone N=xxx forms across TreatmentArm, StudyContext, DoseRegimen,
        /// ParameterName, ParameterSubtype, Population, Timepoint, and Unit.
        /// Runs before all other Phase 2 sub-passes so downstream methods see clean data.
        /// </summary>
        /// <param name="obs">The observation to normalize.</param>
        /// <returns>True if any column was modified.</returns>
        /// <seealso cref="tryStripInlineN"/>
        /// <seealso cref="applyPhase2_ContentNormalization"/>
        private bool normalizeInlineNValues(ParsedObservation obs)
        {
            #region implementation

            // Define all columns eligible for N= stripping (excludes RawValue)
            var columns = new (Func<string?> get, Action<string?> set, string name)[]
            {
                (() => obs.TreatmentArm,    v => obs.TreatmentArm = v,    "TreatmentArm"),
                (() => obs.StudyContext,     v => obs.StudyContext = v,     "StudyContext"),
                (() => obs.DoseRegimen,      v => obs.DoseRegimen = v,      "DoseRegimen"),
                (() => obs.ParameterName,    v => obs.ParameterName = v,    "ParameterName"),
                (() => obs.ParameterSubtype, v => obs.ParameterSubtype = v, "ParameterSubtype"),
                (() => obs.Population,       v => obs.Population = v,       "Population"),
                (() => obs.Timepoint,        v => obs.Timepoint = v,        "Timepoint"),
                (() => obs.Unit,             v => obs.Unit = v,             "Unit"),
            };

            bool anyChange = false;

            foreach (var (get, set, colName) in columns)
            {
                if (tryStripInlineN(get(), out var cleaned, out var n))
                {
                    set(cleaned);
                    if (!obs.ArmN.HasValue)
                        obs.ArmN = n;
                    obs.AppendValidationFlag($"COL_STD:N_STRIPPED:{colName}");
                    anyChange = true;
                }
                else if (tryStripBareInlineN(get(), out cleaned, out n))
                {
                    // Tier-4: bare trailing N= (no brackets/parens) — observed in 83+
                    // DoseRegimen / StudyContext rows. Suffix `:BARE` lets us audit
                    // how many cases needed the no-bracket fallback vs. tiers 1–3.
                    set(cleaned);
                    if (!obs.ArmN.HasValue)
                        obs.ArmN = n;
                    obs.AppendValidationFlag($"COL_STD:N_STRIPPED:{colName}:BARE");
                    anyChange = true;
                }
            }

            // RawValue: extract trailing N= (e.g., "2.9 (22%) N=16", "94.7 (34%)^N=14")
            // Strip the N= portion from RawValue and populate ArmN if not already set.
            if (!string.IsNullOrWhiteSpace(obs.RawValue))
            {
                var rawNMatch = _rawValueTrailingNPattern.Match(obs.RawValue.Trim());
                if (rawNMatch.Success && tryParseNValue(rawNMatch.Groups[2].Value, out var rawN))
                {
                    if (!obs.ArmN.HasValue)
                    {
                        obs.ArmN = rawN;
                    }
                    obs.RawValue = rawNMatch.Groups[1].Value.Trim();
                    obs.AppendValidationFlag("COL_STD:N_STRIPPED:RawValue");
                    anyChange = true;
                }
            }

            return anyChange;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: DoseRegimen triage — routes PK sub-parameters, co-admin drug names,
        /// residual population/timepoint content out of DoseRegimen.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool normalizeDoseRegimen(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.DoseRegimen))
                return false;

            var val = obs.DoseRegimen.Trim();

            // Priority 0: Stat-form column-header echo (e.g., "Mean ± Standard Deviation",
            // "Median (Range)") leaked into DoseRegimen via section-divider inheritance.
            // Must run BEFORE Priority 1 — otherwise bare "Median" would be classified as
            // a PK qualifier and routed to ParameterSubtype.
            if (_doseRegimenStatEchoSet.Contains(val))
            {
                obs.DoseRegimen = null;
                obs.AppendValidationFlag("COL_STD:DOSEREGIMEN_STAT_ECHO_DROPPED");
                return true;
            }

            // Priority 1: PK sub-parameter match → route to ParameterSubtype
            if (PkParameterDictionary.IsPkParameter(val) || PkParameterDictionary.StartsWithPk(val))
            {
                DoseRegimenRoutingPolicy.ApplyRoute(obs, DoseRegimenRoutingPolicy.RouteTarget.ParameterSubtype, val);
                obs.AppendValidationFlag(DoseRegimenRoutingPolicy.FlagPkSubparamRouted);
                return true;
            }

            // Priority 2: Actual dose regex → keep, but extract Dose/DoseUnit if missing
            if (_actualDosePattern.IsMatch(val))
            {
                if (!obs.Dose.HasValue)
                {
                    var (dose, doseUnit) = DoseExtractor.Extract(obs.DoseRegimen);
                    if (dose.HasValue)
                    {
                        obs.Dose = dose;
                        obs.DoseUnit = doseUnit;
                    }
                }
                return false;
            }

            // Priority 3: Drug name match AND category is PK or DDI → route to ParameterSubtype
            if (isDrugName(val) &&
                (string.Equals(obs.TableCategory, "PK", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase)))
            {
                DoseRegimenRoutingPolicy.ApplyRoute(obs, DoseRegimenRoutingPolicy.RouteTarget.ParameterSubtype, val);
                obs.AppendValidationFlag(DoseRegimenRoutingPolicy.FlagCoAdminRouted);
                return true;
            }

            // Priority 4: Residual population pattern
            if (_residualPopulationPattern.IsMatch(val))
            {
                DoseRegimenRoutingPolicy.ApplyRoute(obs, DoseRegimenRoutingPolicy.RouteTarget.Population, val);
                obs.AppendValidationFlag(DoseRegimenRoutingPolicy.FlagPopulationExtracted);
                return true;
            }

            // Priority 5: Residual timepoint pattern
            if (_residualTimepointPattern.IsMatch(val))
            {
                DoseRegimenRoutingPolicy.ApplyRoute(obs, DoseRegimenRoutingPolicy.RouteTarget.Timepoint, val);
                obs.AppendValidationFlag(DoseRegimenRoutingPolicy.FlagTimepointExtracted);
                return true;
            }

            // Priority 6: "Co-administered Drug" literal header echo
            if (val.Equals("Co-administered Drug", StringComparison.OrdinalIgnoreCase) ||
                val.Equals("Coadministered Drug", StringComparison.OrdinalIgnoreCase))
            {
                DoseRegimenRoutingPolicy.ApplyRoute(obs, DoseRegimenRoutingPolicy.RouteTarget.None);
                obs.AppendValidationFlag("COL_STD:ROW_TYPE=HEADER");
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2b: ParameterName cleanup — removes caption echoes, header echoes,
        /// routes bare dose numbers, drug names in DDI, decodes HTML entities.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool normalizeParameterName(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.ParameterName))
                return false;

            var val = obs.ParameterName.Trim();
            bool changed = false;

            // Priority 1: Caption echo
            if (_captionEchoPattern.IsMatch(val) || val.Length > 60)
            {
                // Only null out if it really looks like a caption (has "Table" prefix or is very long)
                if (_captionEchoPattern.IsMatch(val))
                {
                    obs.ParameterName = null;
                    obs.AppendValidationFlag("COL_STD:ROW_TYPE=CAPTION");
                    return true;
                }
            }

            // Priority 2: Header echo (bare "n" or "N")
            if (_paramHeaderEchoPattern.IsMatch(val))
            {
                obs.ParameterName = null;
                obs.AppendValidationFlag("COL_STD:ROW_TYPE=HEADER");
                return true;
            }

            // Priority 3: Bare integer matching common dose level
            if (_bareDoseLevels.Contains(val) &&
                (string.Equals(obs.TableCategory, "DOSING", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(obs.TableCategory, "PK", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(obs.DoseRegimen))
                    obs.DoseRegimen = val;
                obs.ParameterName = null;
                obs.AppendValidationFlag("COL_STD:PARAM_WAS_DOSE");
                return true;
            }

            // Priority 4: DDI drug name (not a PK param) → route to ParameterSubtype
            if (string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase) &&
                isDrugName(val) && !PkParameterDictionary.IsPkParameter(val) && !PkParameterDictionary.StartsWithPk(val))
            {
                if (string.IsNullOrEmpty(obs.ParameterSubtype))
                    obs.ParameterSubtype = val;
                obs.ParameterName = null;
                obs.AppendValidationFlag("COL_STD:COADMIN_ROUTED");
                return true;
            }

            // Priority 5: HTML entity decode
            if (_htmlEntityPattern.IsMatch(val))
            {
                obs.ParameterName = WebUtility.HtmlDecode(val);
                obs.AppendValidationFlag("COL_STD:HTML_ENTITY_DECODED");
                changed = true;
            }

            // Priority 6: OCR spacing artifact collapse
            var collapsed = _ocrSingleCharPattern.Replace(obs.ParameterName ?? val, "$1");
            if (collapsed != (obs.ParameterName ?? val))
            {
                obs.ParameterName = collapsed;
                changed = true;
            }

            return changed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2c: TreatmentArm cleanup — removes header echoes, extracts embedded N=,
        /// extracts embedded doses, nulls generic labels, routes study names.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool normalizeTreatmentArm(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.TreatmentArm))
                return false;

            var val = obs.TreatmentArm.Trim();

            // Priority 1: Header echo ("Number of Patients", "Percent of Subjects")
            if (_armHeaderEchoPattern.IsMatch(val))
            {
                obs.TreatmentArm = null;
                obs.AppendValidationFlag("COL_STD:ARM_WAS_HEADER");
                return true;
            }

            // Priority 2: Embedded [N=xxx] — already handled in Phase 1 for AE/EFFICACY,
            // but needed here for other categories. Skip if pre-pass already extracted ArmN.
            if (!isPhase1Category(obs.TableCategory) && !obs.ArmN.HasValue)
            {
                var bracketMatch = _bracketedNPattern.Match(val);
                if (bracketMatch.Success)
                {
                    if (tryParseNValue(bracketMatch.Groups[2].Value, out var n))
                        obs.ArmN = n;
                    obs.TreatmentArm = bracketMatch.Groups[1].Value.Trim();
                    obs.AppendValidationFlag("COL_STD:ARM_BRACKET_N");
                    return true;
                }

                // Also check simple embedded N= pattern
                var embMatch = _embeddedNPattern.Match(val);
                if (embMatch.Success)
                {
                    if (tryParseNValue(embMatch.Groups[2].Value, out var n2))
                        obs.ArmN = n2;
                    obs.TreatmentArm = embMatch.Groups[1].Value.Trim();
                    obs.AppendValidationFlag("COL_STD:ARM_BRACKET_N");
                    return true;
                }
            }

            // Priority 3: Embedded dose in arm (e.g., "Drug 150 mg/d")
            if (!isPhase1Category(obs.TableCategory))
            {
                var doseMatch = _armEmbeddedDosePattern.Match(val);
                if (doseMatch.Success && isDrugName(val[..doseMatch.Index].Trim()))
                {
                    if (string.IsNullOrEmpty(obs.DoseRegimen))
                        obs.DoseRegimen = doseMatch.Groups[1].Value.Trim();
                    obs.TreatmentArm = val[..doseMatch.Index].Trim();
                    obs.AppendValidationFlag("COL_STD:DOSE_EXTRACTED");
                    return true;
                }
            }

            // Priority 4: Generic arm labels
            if (_genericArmLabels.Contains(val))
            {
                obs.TreatmentArm = null;
                obs.AppendValidationFlag("COL_STD:ARM_WAS_GENERIC");
                return true;
            }

            // Priority 5: Study name (all-caps short token) — only if it's NOT a drug name
            if (_studyNamePattern.IsMatch(val) && !isDrugName(val) && !PkParameterDictionary.IsPkParameter(val))
            {
                if (string.IsNullOrEmpty(obs.StudyContext))
                    obs.StudyContext = val;
                obs.TreatmentArm = null;
                obs.AppendValidationFlag("COL_STD:ARM_WAS_STUDY");
                return true;
            }

            return false;

            #endregion
        }

        // Column-header echoes that sometimes leak into ParameterName. When the
        // routed-out Name matches one of these exactly (case-insensitive), it is
        // a header echo per NULL Preservation Rule §0.2 and may be dropped.
        private static readonly HashSet<string> _headerEchoSet = new(StringComparer.OrdinalIgnoreCase)
        {
            "Population Estimates", "Population Estimate",
            "Pharmacokinetic Parameter", "Pharmacokinetic Parameters",
            "PK Parameter", "PK Parameters",
            "Parameter", "Parameters",
            "Values", "Value", "Estimate", "Estimates",
            "N",
            // R6 — Observed header echoes that leaked into Subtype/Name in the
            // 2026-04-21 corpus audit. These are statistic descriptors or
            // column-caption echoes, not PK terms or populations.
            "Mean (SD)", "Mean ± SD", "Mean+/-SD", "Mean +/- SD",
            "Geometric Mean", "Arithmetic Mean", "Median", "Mean", "SD",
            "Mean (CV%)", "Mean (CV)", "Geometric Mean (CV%)",
            "Pharmacokinetic Parameter [mean (SD)]", "Pharmacokinetic Parameter [mean (SD )]",
            "Pharmacokinetic Parameter (mean)", "Pharmacokinetic Parameter (median)",
            "Major route of elimination", "% of dose excreted",
            "Apparent terminal elimination half-life",
        };

        /**************************************************************/
        /// <summary>
        /// R6 — Contract-allowed <see cref="ParsedObservation.ParameterSubtype"/>
        /// qualifier tokens per <c>column-contracts.md</c> §PK. Any Subtype value
        /// that does NOT match one of these (after trimming and lowercasing) is
        /// treated as misplaced content and routed via
        /// <see cref="routeOrParkNameContent"/> by Step 3b of
        /// <see cref="applyPkCanonicalization"/>.
        /// </summary>
        /// <remarks>
        /// Includes both the underscore-separated canonical forms emitted by
        /// <see cref="PkParameterDictionary.TryExtractCanonicalFromPhrase"/>
        /// and the human-readable forms ("steady state", "single dose") that
        /// may appear in upstream cell text.
        /// </remarks>
        private static readonly HashSet<string> _allowedPkQualifierSet = new(StringComparer.OrdinalIgnoreCase)
        {
            // Variability statistic
            "CV(%)", "CV%", "CV", "%CV", "Coefficient of Variation",
            // Dosing-state qualifiers
            "steady_state", "steady state", "steady-state",
            "single_dose", "single dose", "single-dose",
            "multiple_dose", "multiple dose", "multiple-dose",
            // Fasting-state qualifiers
            "fasted", "fasting",
            "fed",
            // Phase qualifiers
            "terminal", "terminal phase",
            "distribution", "distribution phase",
            "absorption", "elimination",
        };

        /**************************************************************/
        /// <summary>
        /// Phase 2 sub-pass: Enforces the PK column contract across every PK
        /// observation. Guarantees that <see cref="ParsedObservation.ParameterName"/>
        /// holds a canonical PK term and <see cref="ParsedObservation.ParameterSubtype"/>
        /// holds only a short qualifier (never a PK term or a descriptive phrase).
        /// Displaced Name content is routed to its contract-assigned column
        /// (Population / TreatmentArm / DoseRegimen / StudyContext) to honor the
        /// NULL Preservation Rule.
        /// </summary>
        /// <remarks>
        /// ## Five-step ordered enforcement
        /// 1. **Fast path** — Name already canonicalizes. Normalize to canonical form.
        /// 2. **Rescue** — if Name is not a PK term, try to find one in Subtype
        ///    (direct canonical, descriptive phrase, or Name-as-phrase). Route the
        ///    displaced Name content to its best-fit column.
        /// 3. **Subtype scrub** — even on the fast path, strip any residual PK
        ///    terms from Subtype (they don't belong there).
        /// 4. **PD marker flag** — unchanged. Flag non-PK markers for review.
        /// 5. Return `true` when any correction was applied.
        ///
        /// ## Why this runs after <see cref="extractUnitFromParameterSubtype"/>
        /// Unit extraction strips the trailing "(unit)" from Subtype, leaving the
        /// canonical PK term alone to be rescued by step 2. Running before unit
        /// extraction would canonicalize "Cmax(mcg/mL)" → Cmax but then the unit
        /// would never be stripped because the Subtype is already gone.
        /// </remarks>
        /// <param name="obs">The observation to standardize.</param>
        /// <returns>True if any correction was applied.</returns>
        /// <seealso cref="PkParameterDictionary"/>
        /// <seealso cref="PdMarkerDictionary"/>
        /// <seealso cref="PopulationDetector.TryMatchLabel"/>
        /// <seealso cref="routeOrParkNameContent"/>
        private bool applyPkCanonicalization(ParsedObservation obs)
        {
            #region implementation

            // Guard: only PK rows — DRUG_INTERACTION has its own column conventions
            if (!string.Equals(obs.TableCategory, "PK", StringComparison.OrdinalIgnoreCase))
                return false;

            bool changed = false;

            // Step 0: Clinical-trial study identifier in ParameterName (e.g.,
            // "TMC114-C230", "TMC125-C234/IMPAACT P1090"). Must run BEFORE the PK
            // canonicalization fast path because the C-prefix-plus-digits motif
            // inside study codes (e.g., "C230") false-matches the broad
            // `ContainsPkParameter` check (which treats `C230` as concentration-at-230h).
            // Routing here short-circuits the rest of the machinery and lands the
            // value in StudyContext per its column contract.
            //
            // GUARD: If ParameterSubtype carries a recoverable PK term, do NOT short-
            // circuit. The existing Step 2 rescue path (PK_NAME_SUBTYPE_SWAPPED) will
            // promote the PK term from Subtype to Name and route the displaced study-id
            // through `routeOrParkNameContent`, where the (i.6) study-id step picks it
            // up. Bypassing that path would leave Name=null with the PK term stranded
            // in Subtype — which is what caused 10 rows of table 37517 (OP-1118 +
            // Cmax-in-Subtype pattern) to lose their PK statistic entirely.
            if (!string.IsNullOrWhiteSpace(obs.ParameterName)
                && !PkParameterDictionary.ContainsPkParameter(obs.ParameterSubtype))
            {
                var nameTrimmed = obs.ParameterName.Trim();
                if (_studyIdPattern.IsMatch(nameTrimmed) &&
                    !isDrugName(nameTrimmed) &&
                    !PkParameterDictionary.IsPkParameter(nameTrimmed))
                {
                    if (string.IsNullOrWhiteSpace(obs.StudyContext))
                        obs.StudyContext = nameTrimmed;
                    obs.ParameterName = null;
                    obs.AppendValidationFlag("COL_STD:PK_NAME_ROUTED_STUDY_ID");
                    return true;
                }
            }

            // Step 1: Fast path — Name already canonicalizes to a PK term.
            if (PkParameterDictionary.TryCanonicalize(obs.ParameterName, out var canonical1))
            {
                if (!string.Equals(canonical1, obs.ParameterName, StringComparison.Ordinal))
                {
                    obs.ParameterName = canonical1;
                    obs.AppendValidationFlag("COL_STD:PK_NAME_CANONICALIZED");
                    changed = true;
                }
                // fall through to Step 3 (Subtype scrub)
            }
            else
            {
                // Step 2: Rescue a PK term from Subtype or Name.
                //   2a. Subtype yields a canonical (via phrase extraction which
                //       prefers specificity — finds AUC0-inf inside
                //       "Area under the curve, AUC0-∞" rather than generic AUC).
                //   2b. Name itself yields a canonical via phrase extraction.
                string? rescuedCanonical = null;
                string? rescuedQualifier = null;
                bool rescuedFromSubtype = false;
                bool nameWasConsumedWhole = false;

                if (PkParameterDictionary.TryExtractCanonicalFromPhrase(
                        obs.ParameterSubtype, out var fromSub, out var qSub))
                {
                    rescuedCanonical = fromSub;
                    rescuedQualifier = qSub;
                    rescuedFromSubtype = true;
                }
                else if (PkParameterDictionary.TryExtractCanonicalFromPhrase(
                             obs.ParameterName, out var fromName, out var qName))
                {
                    rescuedCanonical = fromName;
                    rescuedQualifier = qName;
                    rescuedFromSubtype = false;

                    // When Name IS exactly the canonical (fast path in step 1 would
                    // have caught this), we skip routing. Otherwise Name had extra
                    // content that must be routed.
                    nameWasConsumedWhole = string.Equals(
                        rescuedCanonical,
                        obs.ParameterName ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);
                }

                if (rescuedCanonical != null)
                {
                    var oldName = obs.ParameterName;

                    // Route displaced Name content when applicable.
                    if (!string.IsNullOrWhiteSpace(oldName) && !nameWasConsumedWhole)
                    {
                        routeOrParkNameContent(obs, oldName);
                    }

                    obs.ParameterName = rescuedCanonical;

                    // Choose flag based on the source shape. A "bare" Subtype is
                    // a single token with no whitespace/commas — that's the
                    // PK_NAME_SUBTYPE_SWAPPED case (e.g., Subtype="TPEAK" or
                    // Subtype="Cmax"). Anything more complex (descriptive
                    // phrase, comma-separated qualifier, etc.) is the
                    // PK_NAME_FROM_PHRASE case.
                    string flag;
                    if (rescuedFromSubtype)
                    {
                        var trimmed = obs.ParameterSubtype?.Trim() ?? string.Empty;
                        var isBareToken = trimmed.Length > 0 &&
                                          !trimmed.Contains(' ') &&
                                          !trimmed.Contains(',');
                        flag = isBareToken
                            ? "COL_STD:PK_NAME_SUBTYPE_SWAPPED"
                            : "COL_STD:PK_NAME_FROM_PHRASE";

                        // Subtype was the source — clear it and use the qualifier
                        // (side-channel may have detected "steady_state" etc.)
                        obs.ParameterSubtype = rescuedQualifier;
                    }
                    else
                    {
                        // Name was the source — Subtype untouched unless empty,
                        // in which case we may use the detected qualifier.
                        flag = "COL_STD:PK_NAME_FROM_PHRASE";
                        if (string.IsNullOrWhiteSpace(obs.ParameterSubtype))
                            obs.ParameterSubtype = rescuedQualifier;
                    }

                    obs.AppendValidationFlag(flag);
                    changed = true;
                }
            }

            // Step 3: Subtype scrub — PK terms never live in Subtype.
            if (!string.IsNullOrWhiteSpace(obs.ParameterSubtype))
            {
                if (PkParameterDictionary.IsPkParameter(obs.ParameterSubtype))
                {
                    obs.ParameterSubtype = null;
                    obs.AppendValidationFlag("COL_STD:PK_SUBTYPE_SCRUBBED");
                    changed = true;
                }
                else if (PkParameterDictionary.ContainsPkParameter(obs.ParameterSubtype)
                         && PkParameterDictionary.TryExtractCanonicalFromPhrase(
                                obs.ParameterSubtype, out _, out var residualQualifier))
                {
                    obs.ParameterSubtype = residualQualifier;
                    obs.AppendValidationFlag("COL_STD:PK_SUBTYPE_SCRUBBED");
                    changed = true;
                }
            }

            // Step 3b (R6): Route non-qualifier Subtype content out of Subtype.
            // Anything left in Subtype that isn't an allowed qualifier token
            // (steady_state/single_dose/fasted/fed/terminal/distribution/CV(%)
            // — see _allowedPkQualifierSet) is displaced content. Reuse the
            // same 7-step decision tree the Step-2 rescue uses via
            // routeOrParkNameContent, then null Subtype and flag.
            if (!string.IsNullOrWhiteSpace(obs.ParameterSubtype)
                && !isAllowedPkQualifier(obs.ParameterSubtype))
            {
                var oldSubtype = obs.ParameterSubtype;
                routeOrParkNameContent(obs, oldSubtype);
                obs.ParameterSubtype = null;
                obs.AppendValidationFlag("COL_STD:PK_SUBTYPE_ROUTED");
                changed = true;
            }

            // Step 4: PD marker flagging. Preserves the row and just emits a flag
            // so downstream review can audit PD markers (IPA, VASP-PRI) that
            // shouldn't be in PK tables at all.
            if (PdMarkerDictionary.IsPdMarker(obs.ParameterSubtype)
                || PdMarkerDictionary.IsPdMarker(obs.ParameterName))
            {
                obs.AppendValidationFlag("COL_STD:PK_NON_PK_MARKER_DETECTED");
                changed = true;
            }

            // Step 5 (R7): Unconditional ParameterName fitness check. At this
            // point Steps 1-4 have had their chance to resolve Name to a PK
            // canonical. If Name is still populated and does NOT canonicalize
            // AND does not contain a PK term anywhere, it's non-PK content
            // that leaked through — route it out so ParameterName holds only
            // canonical PK terms (or null).
            if (!string.IsNullOrWhiteSpace(obs.ParameterName)
                && !PkParameterDictionary.IsPkParameter(obs.ParameterName)
                && !PkParameterDictionary.ContainsPkParameter(obs.ParameterName))
            {
                var oldName = obs.ParameterName;
                routeOrParkNameContent(obs, oldName);
                obs.ParameterName = null;
                obs.AppendValidationFlag("COL_STD:PK_NAME_CLEANED_NONCANON");
                changed = true;
            }

            return changed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R6 — Returns true when <paramref name="subtype"/> is a contract-allowed
        /// PK qualifier per <see cref="_allowedPkQualifierSet"/>. Applies lightweight
        /// normalization (trim, lowercase) before membership check so variants like
        /// "Steady State" / "steady_state" / "Steady-State" all resolve.
        /// </summary>
        /// <param name="subtype">Candidate ParameterSubtype value.</param>
        /// <returns>True when the value is an allowed qualifier.</returns>
        /// <seealso cref="_allowedPkQualifierSet"/>
        private static bool isAllowedPkQualifier(string? subtype)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(subtype))
                return true; // empty is trivially allowed
            return _allowedPkQualifierSet.Contains(subtype.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decides where displaced <see cref="ParsedObservation.ParameterName"/>
        /// content should go when a PK term is being moved in. Honors the NULL
        /// Preservation Rule — routes to Population / TreatmentArm / DoseRegimen
        /// / StudyContext when possible, and only drops content that matches a
        /// known column-header echo.
        /// </summary>
        /// <remarks>
        /// ## Routing priority (first match wins)
        /// 1. Population (dictionary or regex second pass)
        /// 2. Drug + dose compound ("Guanfacine 1 mg once daily")
        /// 3. Pure drug name
        /// 4. Pure dose regimen (digits but no drug prefix)
        /// 5. Known column-header echo ("Population Estimates", "Values")
        /// 6. StudyContext park (preserves unclassifiable data)
        /// 7. Last-resort drop with <c>PK_NAME_DROPPED_UNCLASSIFIED</c> flag
        /// </remarks>
        /// <param name="obs">The observation receiving the routed content.</param>
        /// <param name="oldName">The displaced ParameterName to route.</param>
        /// <seealso cref="applyPkCanonicalization"/>
        /// <seealso cref="PopulationDetector.TryMatchLabel"/>
        /// <seealso cref="isDrugName"/>
        /// <seealso cref="DoseExtractor.Extract"/>
        private void routeOrParkNameContent(ParsedObservation obs, string oldName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(oldName))
                return;

            var trimmed = oldName.Trim();

            // (i) Population — dictionary OR regex second pass
            if (PopulationDetector.TryMatchLabel(trimmed, out var popCanon, out var viaRegex))
            {
                if (string.IsNullOrWhiteSpace(obs.Population))
                    obs.Population = popCanon;
                obs.AppendValidationFlag(viaRegex
                    ? "COL_STD:PK_POPULATION_ROUTED_REGEX"
                    : "COL_STD:PK_POPULATION_ROUTED");
                return;
            }

            // (i.5 — R7) Timepoint descriptor — route to Timepoint column so
            // content like "Day 14", "5 days", "Single Dose", "C72",
            // "08:00 to 13:00" lands in its contract column instead of
            // falling through to StudyContext.
            if (_timepointRoutingPattern.IsMatch(trimmed))
            {
                if (string.IsNullOrWhiteSpace(obs.Timepoint))
                    obs.Timepoint = trimmed;
                obs.AppendValidationFlag("COL_STD:PK_TIMEPOINT_ROUTED");
                return;
            }

            // (i.6) Clinical-trial study identifier — route to StudyContext.
            // Drug names and PK parameters are guarded out so this branch only
            // catches study codes (e.g., TMC114-C230, TMC125-C234/IMPAACT P1090).
            // Must precede the dose-extractor (ii): codes like "C230" or "1090"
            // would otherwise look dose-like to DoseExtractor.
            if (_studyIdPattern.IsMatch(trimmed) &&
                !isDrugName(trimmed) &&
                !PkParameterDictionary.IsPkParameter(trimmed))
            {
                if (string.IsNullOrWhiteSpace(obs.StudyContext))
                    obs.StudyContext = trimmed;
                obs.AppendValidationFlag("COL_STD:PK_NAME_ROUTED_STUDY_ID");
                return;
            }

            // (ii) Drug + dose compound: "Guanfacine Ext-Release Tablets 1 mg once daily"
            var (dose, doseUnit) = DoseExtractor.Extract(trimmed);
            if (dose.HasValue)
            {
                // Strip the matched dose fragment to see what's left (drug prefix).
                var drugPart = stripDoseFragment(trimmed);
                if (!string.IsNullOrWhiteSpace(drugPart) && isDrugName(drugPart))
                {
                    if (string.IsNullOrWhiteSpace(obs.TreatmentArm))
                        obs.TreatmentArm = drugPart.Trim();
                    if (string.IsNullOrWhiteSpace(obs.DoseRegimen))
                    {
                        // Put the dose fragment (everything BUT the drug prefix) into DoseRegimen.
                        var doseFragment = trimmed.Substring(drugPart.Length).Trim();
                        obs.DoseRegimen = string.IsNullOrWhiteSpace(doseFragment) ? trimmed : doseFragment;
                        if (!obs.Dose.HasValue)
                        {
                            obs.Dose = dose;
                            obs.DoseUnit = doseUnit;
                        }
                    }
                    obs.AppendValidationFlag("COL_STD:PK_NAME_ROUTED_ARM");
                    return;
                }

                // Pure dose regimen (no drug prefix).
                if (string.IsNullOrWhiteSpace(obs.DoseRegimen))
                    obs.DoseRegimen = trimmed;
                if (!obs.Dose.HasValue)
                {
                    obs.Dose = dose;
                    obs.DoseUnit = doseUnit;
                }
                obs.AppendValidationFlag("COL_STD:PK_NAME_ROUTED_DOSE");
                return;
            }

            // (iii) Pure drug name (no dose)
            if (isDrugName(trimmed))
            {
                if (string.IsNullOrWhiteSpace(obs.TreatmentArm))
                    obs.TreatmentArm = trimmed;
                obs.AppendValidationFlag("COL_STD:PK_NAME_ROUTED_ARM");
                return;
            }

            // (iv) Column-header echo — NULL Preservation Rule §0.2 carve-out.
            if (_headerEchoSet.Contains(trimmed))
            {
                obs.AppendValidationFlag("COL_STD:PK_NAME_ECHO_DROPPED");
                return;
            }

            // (v) Unclassifiable — park into StudyContext to preserve the data.
            if (string.IsNullOrWhiteSpace(obs.StudyContext))
            {
                obs.StudyContext = trimmed;
                obs.AppendValidationFlag("COL_STD:PK_NAME_PARKED_CTX");
            }
            else
            {
                // Last resort: StudyContext is also occupied. Drop with a flag so
                // the incident can be audited; this should be 0 on clean data.
                obs.AppendValidationFlag("COL_STD:PK_NAME_DROPPED_UNCLASSIFIED");
            }

            #endregion
        }

        // Matches the LAST numeric dose token plus its unit in a string (used to
        // strip the dose fragment from "Guanfacine ... 1 mg once daily" → "Guanfacine ...").
        // Intentionally greedy left-anchor so "Drug Name 1 mg once daily" → "Drug Name".
        private static readonly System.Text.RegularExpressions.Regex _doseFragmentPattern =
            new(@"\s*\b\d+(?:\.\d+)?\s*(?:mg|mcg|µg|μg|g|ng|kg|mL|units?|U|IU)\b.*$",
                System.Text.RegularExpressions.RegexOptions.Compiled |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// R7 — Anchored pattern recognizing timepoint descriptors for the
        /// Timepoint routing step inside <see cref="routeOrParkNameContent"/>.
        /// Covers visit labels ("Day N", "Week N"), numeric durations
        /// ("5 days", "24 hours"), clock ranges ("08:00 to 13:00"), C<N>h
        /// concentration labels, and PK dosing state tokens ("single dose",
        /// "steady state", "pre-dose").
        /// </summary>
        /// <remarks>
        /// Anchored to start of string so stray prose with an embedded day
        /// fragment does not misroute to Timepoint. Kept in sync with the
        /// parser-side <c>_timepointLabelPattern</c> in <c>PkTableParser.cs</c>.
        /// </remarks>
        private static readonly System.Text.RegularExpressions.Regex _timepointRoutingPattern = new(
            @"^\s*(?:"
          + @"(?:Day|Week|Month|Cycle|Visit)\s+\d+"
          + @"|\d+(?:\.\d+)?\s*(?:days?|weeks?|hours?|hrs?|h|months?|minutes?|min)"
          + @"|\d+\s*(?:to|[-–])\s*\d+\s*(?:days?|weeks?|hours?|months?)"
          + @"|single\s+dose|steady[\s-]?state|pre[-\s]?dose|post[-\s]?dose|baseline"
          + @"|\d{1,2}:\d{2}(?:\s*(?:to|[-–])\s*\d{1,2}:\d{2})?"
          + @"|C\d{1,3}(?:h|hr|hrs|hour|hours)?"
          + @")\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Clinical-trial study identifier pattern — matches codes like
        /// "TMC114-C230", "TMC125-C234/IMPAACT P1090", "ABC-12345". Used by
        /// <see cref="routeOrParkNameContent"/> to route study IDs out of
        /// ParameterName to StudyContext.
        /// </summary>
        /// <remarks>
        /// Anchored full-string. Structure: 2–5 uppercase letters, optional
        /// separator, 2–5 digits, optional follow-on group (separator +
        /// alphanumeric token). Drug names and PK parameters are excluded by
        /// the caller via <see cref="isDrugName"/> /
        /// <see cref="PkParameterDictionary.IsPkParameter"/> guards.
        /// </remarks>
        /// <seealso cref="routeOrParkNameContent"/>
        private static readonly System.Text.RegularExpressions.Regex _studyIdPattern = new(
            @"^[A-Z]{2,5}[\s\-_]?\d{2,5}(?:[\-_/][A-Z0-9][A-Z0-9\s/\-]*)?$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Strips the numeric dose fragment and everything after it from a
        /// drug+dose string, leaving just the drug-name prefix. Used by
        /// <see cref="routeOrParkNameContent"/> to separate "Guanfacine Extended-Release Tablets"
        /// from "1 mg once daily".
        /// </summary>
        private static string stripDoseFragment(string input)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            return _doseFragmentPattern.Replace(input, "").TrimEnd();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 sub-pass: Extracts units from trailing parenthesized content in ParameterSubtype.
        /// Only applies to PK and DRUG_INTERACTION table categories where ParameterSubtype often
        /// encodes units inline (e.g., "Cmax(pg/mL)", "AUC120(pg·hr/mL)", "Cmax(serum, mcg/mL)").
        /// </summary>
        /// <remarks>
        /// ## Algorithm
        /// 1. Guards: skip if ParameterSubtype is null/whitespace or category is not PK/DRUG_INTERACTION
        /// 2. Regex matches trailing parenthesized content
        /// 3. Two sub-cases:
        ///    - Simple unit: inner text is a known unit or matches structural PK pattern
        ///    - Qualifier + unit: "serum, mcg/mL" — split on last comma, right token is unit
        /// 4. Sets Unit if empty (does not overwrite existing), strips parenthesized portion from Subtype
        /// </remarks>
        /// <param name="obs">The observation to process.</param>
        /// <returns>True if a unit was extracted.</returns>
        /// <seealso cref="_subtypeTrailingParenPattern"/>
        /// <seealso cref="Dictionaries.UnitDictionary.PkUnitStructurePattern"/>
        /// <seealso cref="normalizeUnit"/>
        private bool extractUnitFromParameterSubtype(ParsedObservation obs)
        {
            #region implementation

            // Guard: skip if ParameterSubtype is null/whitespace
            if (string.IsNullOrWhiteSpace(obs.ParameterSubtype))
                return false;

            // Guard: only applies to PK and DRUG_INTERACTION categories
            if (!string.Equals(obs.TableCategory, "PK", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase))
                return false;

            // Fold Unicode variants (⋅ → ·) before regex match and unit lookup
            obs.ParameterSubtype = PkParameterDictionary.NormalizeUnicode(obs.ParameterSubtype);

            // Match trailing parenthesized content
            var match = _subtypeTrailingParenPattern.Match(obs.ParameterSubtype);
            if (!match.Success)
                return false;

            var innerText = match.Groups[1].Value.Trim();
            string? extractedUnit = null;
            string? qualifier = null;

            // Check for qualifier + unit pattern: "serum, mcg/mL"
            var lastCommaIdx = innerText.LastIndexOf(',');
            if (lastCommaIdx > 0)
            {
                var candidateUnit = innerText.Substring(lastCommaIdx + 1).Trim();
                if (Dictionaries.UnitDictionary.IsRecognized(candidateUnit))
                {
                    extractedUnit = candidateUnit;
                    qualifier = innerText.Substring(0, lastCommaIdx).Trim();
                }
            }

            // If no qualifier+unit match, check if the whole inner text is a unit
            if (extractedUnit == null)
            {
                if (Dictionaries.UnitDictionary.IsRecognized(innerText))
                {
                    extractedUnit = innerText;
                }
            }

            if (extractedUnit == null)
                return false;

            // Normalize the extracted unit. TryNormalize combines NormalizationMap
            // lookup, KnownUnits canonical lookup, structural pattern, and the
            // whitespace-tolerant fallback in one call — so a spaced source like
            // "mcg /mL" still ends up as "mcg/mL" rather than the spaced literal.
            var canonical = Dictionaries.UnitDictionary.TryNormalize(extractedUnit);
            if (!string.IsNullOrWhiteSpace(canonical))
                extractedUnit = canonical;

            // Only set Unit if it's currently empty
            if (string.IsNullOrWhiteSpace(obs.Unit))
            {
                obs.Unit = extractedUnit;
            }
            else
            {
                // Unit already set — don't overwrite, but still clean up ParameterSubtype
            }

            // Strip parenthesized portion from ParameterSubtype
            var subtypeBase = obs.ParameterSubtype.Substring(0, match.Index).Trim();
            if (qualifier != null)
            {
                // "Cmax(serum, mcg/mL)" → "Cmax, serum"
                obs.ParameterSubtype = string.IsNullOrWhiteSpace(subtypeBase)
                    ? qualifier
                    : $"{subtypeBase}, {qualifier}";
            }
            else
            {
                // "Cmax(pg/mL)" → "Cmax"
                obs.ParameterSubtype = string.IsNullOrWhiteSpace(subtypeBase)
                    ? null
                    : subtypeBase;
            }

            obs.AppendValidationFlag("COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");
            return true;

            #endregion
        }

        // R10 DRY — private isRecognizedUnit method removed. Callers now invoke
        // UnitDictionary.IsRecognized directly (identical semantics; also folds
        // Unicode variants, matching what PkParameterDictionary.NormalizeUnicode
        // already applies upstream in extractUnitFromParameterSubtype).

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Unit scrub — detects header leaks, normalizes variant spellings,
        /// extracts real units from verbose descriptions.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool normalizeUnit(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.Unit))
                return false;

            // Fold Unicode dot operator (U+22C5), bullet operator (U+2219), bullet
            // (U+2022), multiplication sign (U+00D7) to middle dot (U+00B7); and
            // micro sign (U+00B5) to Greek mu (U+03BC), so the dictionaries keyed
            // on canonical codepoints match cells like "mcg⋅h/mL", "mcg∙h/mL",
            // "mcg•h/mL", "ng×hr/mL", "µg/mL".
            var val = PkParameterDictionary.NormalizeUnicode(obs.Unit).Trim();

            // Rule 1: Match in known units (post-Unicode-fold). If the post-fold
            // form differs from the original obs.Unit (e.g., U+2219 folded to
            // U+00B7, or U+00B5 folded to U+03BC), apply the canonical form as a
            // correction. Otherwise the value is already canonical — no work.
            if (Dictionaries.UnitDictionary.KnownUnits.TryGetValue(val, out var knownCanonical))
            {
                if (!string.Equals(knownCanonical, obs.Unit, StringComparison.Ordinal))
                {
                    obs.Unit = knownCanonical;
                    obs.AppendValidationFlag("COL_STD:UNIT_NORMALIZED");
                    return true;
                }
                return false;
            }

            // Rule 2: len > 30 → likely a leaked column header
            if (val.Length > 30)
            {
                obs.Unit = null;
                obs.AppendValidationFlag("COL_STD:UNIT_HEADER_LEAK");
                return true;
            }

            // Rule 3: Contains a drug name → leaked header
            if (isDrugName(val))
            {
                obs.Unit = null;
                obs.AppendValidationFlag("COL_STD:UNIT_HEADER_LEAK");
                return true;
            }

            // Rule 4: Contains header keywords → leaked header
            foreach (var keyword in _unitHeaderKeywords)
            {
                if (val.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    obs.Unit = null;
                    obs.AppendValidationFlag("COL_STD:UNIT_HEADER_LEAK");
                    return true;
                }
            }

            // Rule 5: Extractable real unit inside parentheses. Use TryNormalize
            // so a parenthesized variant or spaced unit ("(mcg /mL)", "(hr)")
            // canonicalizes correctly via NormalizationMap + whitespace fallback.
            var extractMatch = _extractableUnitPattern.Match(val);
            if (extractMatch.Success)
            {
                var extracted = extractMatch.Groups[1].Value.Trim();
                var extractedCanonical = Dictionaries.UnitDictionary.TryNormalize(extracted);
                if (!string.IsNullOrWhiteSpace(extractedCanonical))
                {
                    obs.Unit = extractedCanonical;
                    obs.AppendValidationFlag("COL_STD:UNIT_NORMALIZED");
                    return true;
                }
            }

            // Rule 6: Variant spelling normalization. TryNormalize subsumes the
            // NormalizationMap lookup AND adds whitespace-tolerance + structural
            // pattern matching, so spaced PDF artifacts like "mcg /mL" or
            // "mcg . hr /mL" canonicalize to "mcg/mL" / "mcg·h/mL".
            var canonical = Dictionaries.UnitDictionary.TryNormalize(val);
            if (!string.IsNullOrWhiteSpace(canonical) &&
                !string.Equals(canonical, val, StringComparison.OrdinalIgnoreCase))
            {
                obs.Unit = canonical;
                obs.AppendValidationFlag("COL_STD:UNIT_NORMALIZED");
                return true;
            }

            // Rule 7 (post-extract sanity sweep): If none of Rules 1-6 fired, the
            // value is neither a clean known unit nor a recognized verbose form.
            // Catch malformed leakage that slipped past earlier rules — values like
            // "mcg•hr/mL) Amoxicillin (±S.D." (length 29 — slips past Rule 2's >30
            // check; not exact-match drug name — slips past Rule 3; trailing parens
            // unbalanced — Rule 5 fails; not in Rule 6 map). Detection signals:
            //   • Unbalanced parens (stray ')' before any '(' OR open/close mismatch)
            //   • A drug-name token embedded inside the value
            if (hasUnbalancedParens(val) || containsDrugNameToken(val))
            {
                obs.Unit = null;
                obs.AppendValidationFlag("COL_STD:UNIT_HEADER_LEAK:POST_EXTRACT");
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the value's parentheses are unbalanced — either the
        /// counts don't match, or a closing paren appears before any matching open.
        /// </summary>
        /// <remarks>
        /// Used by <see cref="normalizeUnit"/> Rule 7 to detect malformed leaked
        /// header text like "mcg•hr/mL) Amoxicillin (±S.D." where a stray close
        /// paren and an unclosed open paren both signal corrupted input.
        /// </remarks>
        /// <seealso cref="normalizeUnit"/>
        private static bool hasUnbalancedParens(string val)
        {
            #region implementation

            int depth = 0;
            foreach (var ch in val)
            {
                if (ch == '(') depth++;
                else if (ch == ')')
                {
                    depth--;
                    if (depth < 0) return true; // close before any open
                }
            }
            return depth != 0;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when any token within the value matches a known drug name.
        /// Tokens are split on whitespace and bracket characters and stripped of
        /// trailing punctuation.
        /// </summary>
        /// <remarks>
        /// Differs from <see cref="isDrugName"/>: this scans embedded tokens (the
        /// other does whole-string and first-word match). Used by
        /// <see cref="normalizeUnit"/> Rule 7 to catch drug names that leaked into
        /// the middle of a malformed Unit string.
        /// </remarks>
        /// <seealso cref="normalizeUnit"/>
        private bool containsDrugNameToken(string val)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(val)) return false;

            var tokens = Regex.Split(val, @"[\s\(\)\[\]\,]+");
            foreach (var raw in tokens)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var token = raw.Trim().TrimEnd('.', ';', ':');
                if (token.Length < 4) continue; // minimum drug-name length
                if (_drugNames.Contains(token)) return true;
            }
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2e: ParameterCategory SOC mapping — normalizes MedDRA SOC variants to
        /// canonical names. Only applies to ADVERSE_EVENT tables.
        /// </summary>
        /// <returns>True if a correction was applied.</returns>
        private bool normalizeParameterCategory(ParsedObservation obs)
        {
            #region implementation

            // Only applies to AE tables
            if (!string.Equals(obs.TableCategory, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(obs.ParameterCategory))
                return false;

            var val = obs.ParameterCategory.Trim();

            // OCR artifact repair: collapse isolated single chars
            var repaired = _ocrSingleCharPattern.Replace(val, "$1");

            // Canonical lookup
            if (_socCanonicalMap.TryGetValue(repaired, out var canonicalSoc))
            {
                if (!string.Equals(obs.ParameterCategory, canonicalSoc, StringComparison.Ordinal))
                {
                    obs.ParameterCategory = canonicalSoc;
                    obs.AppendValidationFlag("COL_STD:SOC_NORMALIZED");
                    return true;
                }
                return false;
            }

            // If original (pre-repair) was different, try that too
            if (repaired != val && _socCanonicalMap.TryGetValue(val, out canonicalSoc))
            {
                obs.ParameterCategory = canonicalSoc;
                obs.AppendValidationFlag("COL_STD:SOC_NORMALIZED");
                return true;
            }

            // No match — flag if it doesn't already match a canonical value
            if (!_socCanonicalMap.ContainsValue(val) && !_socCanonicalMap.ContainsValue(repaired))
            {
                obs.AppendValidationFlag("COL_STD:SOC_UNMATCHED");
            }

            return false;

            #endregion
        }

        #endregion Phase 2: Content Normalization

        #region Phase 3: PrimaryValueType Migration

        /**************************************************************/
        /// <summary>
        /// Phase 3: Migrates old PrimaryValueType strings to the tightened enum.
        /// Uses TableCategory, Unit, Caption, and bounds to resolve ambiguous mappings.
        /// </summary>
        /// <returns>Number of corrections applied (0 or 1).</returns>
        private int applyPhase3_PrimaryValueTypeMigration(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.PrimaryValueType))
                return 0;

            var oldType = obs.PrimaryValueType.Trim();
            string? newType = null;

            // Direct 1:1 mappings
            if (_pvtDirectMap.TryGetValue(oldType, out var directMapping))
            {
                if (!string.Equals(oldType, directMapping, StringComparison.Ordinal))
                {
                    newType = directMapping;
                }
                else
                {
                    return 0; // Already canonical
                }
            }
            // Context-dependent: "Mean"
            else if (string.Equals(oldType, "Mean", StringComparison.OrdinalIgnoreCase))
            {
                newType = resolveMeanType(obs);
            }
            // Context-dependent: "GeometricMean" — already correct, no change
            else if (string.Equals(oldType, "GeometricMean", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(oldType, "LSMean", StringComparison.OrdinalIgnoreCase))
            {
                return 0; // Already in new enum
            }
            // Context-dependent: "RelativeRiskReduction"
            else if (string.Equals(oldType, "RelativeRiskReduction", StringComparison.OrdinalIgnoreCase))
            {
                newType = resolveRiskType(obs);
                if (isNonEfficacyRiskCategory(obs.TableCategory))
                    obs.AppendValidationFlag("COL_STD:PVT_RR_CI_CATEGORY_REMAP");
            }
            // Already-canonical "RelativeRisk" on a non-Efficacy category is a
            // contract violation produced by the rr_ci parse rule misfiring on
            // PK / DrugInteraction `value (CI, CI)` cells. Remap before the
            // direct-map "already canonical" early return below would skip it.
            else if (string.Equals(oldType, "RelativeRisk", StringComparison.OrdinalIgnoreCase) &&
                     isNonEfficacyRiskCategory(obs.TableCategory))
            {
                newType = resolveRiskType(obs);
                obs.AppendValidationFlag("COL_STD:PVT_RR_CI_CATEGORY_REMAP");
            }
            // Context-dependent: "Ratio"
            else if (string.Equals(oldType, "Ratio", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase))
                    newType = "GeometricMeanRatio";
                else
                    return 0; // Keep as Ratio for other categories
            }
            // Context-dependent: "Numeric"
            else if (string.Equals(oldType, "Numeric", StringComparison.OrdinalIgnoreCase))
            {
                newType = resolveNumericType(obs);
                if (string.Equals(newType, "Numeric", StringComparison.OrdinalIgnoreCase))
                    return 0; // Couldn't resolve — leave as-is (flag already set in resolveNumericType)
            }
            else
            {
                return 0; // Unknown type — leave as-is
            }

            if (newType != null && !string.Equals(oldType, newType, StringComparison.OrdinalIgnoreCase))
            {
                obs.PrimaryValueType = newType;
                obs.AppendValidationFlag($"COL_STD:PVT_MIGRATED:{oldType}→{newType}");
                return 1;
            }

            return 0;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves "Mean" → ArithmeticMean or GeometricMean based on table category and caption.
        /// </summary>
        private static string resolveMeanType(ParsedObservation obs)
        {
            #region implementation

            var caption = obs.Caption ?? "";

            // Caption explicit mentions override everything
            if (caption.Contains("arithmetic", StringComparison.OrdinalIgnoreCase))
                return "ArithmeticMean";
            if (caption.Contains("geometric", StringComparison.OrdinalIgnoreCase))
                return "GeometricMean";
            if (caption.Contains("LS mean", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("least square", StringComparison.OrdinalIgnoreCase))
                return "LSMean";

            // Trust parser's CAPTION_HINT over category defaults — when the parser
            // extracted "Mean" from the caption, it means the caption explicitly said
            // "Mean" (not "Geometric Mean"), so honour that as ArithmeticMean.
            var captionHint = extractCaptionHintType(obs);
            if (captionHint != null)
            {
                if (string.Equals(captionHint, "Mean", StringComparison.OrdinalIgnoreCase))
                    return "ArithmeticMean";
                if (string.Equals(captionHint, "GeometricMean", StringComparison.OrdinalIgnoreCase))
                    return "GeometricMean";
                if (string.Equals(captionHint, "LSMean", StringComparison.OrdinalIgnoreCase))
                    return "LSMean";
                if (string.Equals(captionHint, "Median", StringComparison.OrdinalIgnoreCase))
                    return "Median";
            }

            // Default: ArithmeticMean for ALL categories when no explicit hint exists.
            // GeometricMean is the outlier — only used when caption/header/footer explicitly says so.
            return "ArithmeticMean";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a risk-type PrimaryValueType to a canonical value, honoring the
        /// per-TableCategory contract in column-contracts.md.
        /// </summary>
        /// <remarks>
        /// For Efficacy tables, picks HazardRatio / OddsRatio / RelativeRisk based on
        /// caption hints. For non-Efficacy categories (PK, DrugInteraction, BMD,
        /// TissueDistribution), the rr_ci parse rule misfires on `value (CI, CI)`
        /// patterns and produces a contract-violating RelativeRisk label. Remap to
        /// ArithmeticMean (or GeometricMean if the caption indicates geometric stats).
        /// Bounds are preserved by the migration framework — only the PVT label changes.
        /// </remarks>
        /// <seealso cref="isNonEfficacyRiskCategory"/>
        /// <seealso cref="applyPhase3_PrimaryValueTypeMigration"/>
        private static string resolveRiskType(ParsedObservation obs)
        {
            #region implementation

            var caption = obs.Caption ?? "";

            // Non-Efficacy override: rr_ci parse rule produced a contract violation.
            if (isNonEfficacyRiskCategory(obs.TableCategory))
            {
                return caption.Contains("geometric", StringComparison.OrdinalIgnoreCase)
                    ? "GeometricMean"
                    : "ArithmeticMean";
            }

            if (caption.Contains("hazard", StringComparison.OrdinalIgnoreCase))
                return "HazardRatio";
            if (caption.Contains("odds", StringComparison.OrdinalIgnoreCase))
                return "OddsRatio";

            return "RelativeRisk";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the TableCategory is one whose contract forbids the
        /// risk-type PrimaryValueTypes (HazardRatio / OddsRatio / RelativeRisk).
        /// </summary>
        /// <remarks>
        /// Per column-contracts.md, only the Efficacy contract permits these labels.
        /// PK and DrugInteraction use ArithmeticMean / GeometricMean / GeometricMeanRatio;
        /// BMD uses PercentChange / ArithmeticMean; TissueDistribution uses ArithmeticMean
        /// / GeometricMean. Empty / unknown category returns false (no remap).
        /// </remarks>
        /// <seealso cref="resolveRiskType"/>
        private static bool isNonEfficacyRiskCategory(string? category)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(category)) return false;
            return string.Equals(category, "PK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "BMD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "TISSUE_DISTRIBUTION", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves "Numeric" → specific type using TableCategory, Unit, bounds, and caption context.
        /// Returns "Numeric" (unchanged) when resolution is not possible.
        /// </summary>
        private string resolveNumericType(ParsedObservation obs)
        {
            #region implementation

            var category = obs.TableCategory ?? "";
            var unit = obs.Unit ?? "";
            var caption = obs.Caption ?? "";

            // AE: % → Percentage, null + integer → Count
            if (string.Equals(category, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(unit, "%", StringComparison.OrdinalIgnoreCase))
                    return "Percentage";
                if (string.IsNullOrEmpty(unit) && obs.PrimaryValue.HasValue &&
                    obs.PrimaryValue.Value == Math.Floor(obs.PrimaryValue.Value))
                    return "Count";
            }

            // PK — default to ArithmeticMean; GeometricMean only when caption
            // explicitly says "geometric" (handled by caption checks below)
            if (string.Equals(category, "PK", StringComparison.OrdinalIgnoreCase))
                return "ArithmeticMean";

            // DDI → GeometricMeanRatio (caption hint "Mean" is uncommon for DDI tables)
            if (string.Equals(category, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase))
                return "GeometricMeanRatio";

            // BMD → PercentChange
            if (string.Equals(category, "BMD", StringComparison.OrdinalIgnoreCase))
                return "PercentChange";

            // EFFICACY + bounds → HazardRatio
            if (string.Equals(category, "EFFICACY", StringComparison.OrdinalIgnoreCase) &&
                (obs.LowerBound.HasValue || obs.UpperBound.HasValue))
                return "HazardRatio";

            // DOSING → keep as Numeric (prescriptive)
            if (string.Equals(category, "DOSING", StringComparison.OrdinalIgnoreCase))
                return "Numeric";

            // Caption-based resolution (any category)
            if (caption.Contains("geometric mean", StringComparison.OrdinalIgnoreCase))
                return "GeometricMean";
            if (caption.Contains("arithmetic mean", StringComparison.OrdinalIgnoreCase))
                return "ArithmeticMean";
            if (caption.Contains("LS mean", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("least square", StringComparison.OrdinalIgnoreCase))
                return "LSMean";
            if (caption.Contains("median", StringComparison.OrdinalIgnoreCase))
                return "Median";

            // Unresolved
            obs.AppendValidationFlag("COL_STD:PVT_UNRESOLVED");
            return "Numeric";

            #endregion
        }

        #endregion Phase 3: PrimaryValueType Migration

        #region Phase 4: Column Contract Enforcement

        /**************************************************************/
        /// <summary>
        /// Phase 4: Enforces per-TableCategory column contracts — NULLs out N/A columns,
        /// flags missing required columns, applies default BoundType.
        /// </summary>
        /// <returns>Number of corrections applied.</returns>
        private int applyPhase4_ColumnContractEnforcement(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(obs.TableCategory))
                return 0;

            if (!_columnContracts.TryGetValue(obs.TableCategory, out var contract))
                return 0;

            int corrections = 0;

            // NULL enforcement: set N/A columns to null
            corrections += enforceNullColumns(obs, contract);

            // Missing required: flag R columns that are null/empty
            flagMissingRequired(obs, contract);

            // Default BoundType: apply when bounds present but type missing
            if (applyDefaultBoundType(obs))
                corrections++;

            return corrections;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets columns marked NotApplicable to null for the row's TableCategory.
        /// </summary>
        /// <returns>Number of columns nulled out.</returns>
        private static int enforceNullColumns(ParsedObservation obs, Dictionary<string, ColumnRequirement> contract)
        {
            #region implementation

            int nulled = 0;

            foreach (var (column, requirement) in contract)
            {
                if (requirement != ColumnRequirement.NotApplicable)
                    continue;

                var currentValue = ParsedObservationFieldAccess.Get(obs, column);
                if (currentValue == null)
                    continue;

                ParsedObservationFieldAccess.Set(obs, column, null);
                obs.AppendValidationFlag($"COL_STD:NULL_{column}");
                nulled++;
            }

            return nulled;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Flags columns marked Required that are null or empty.
        /// Does not modify data — only appends validation flags.
        /// </summary>
        private static void flagMissingRequired(ParsedObservation obs, Dictionary<string, ColumnRequirement> contract)
        {
            #region implementation

            foreach (var (column, requirement) in contract)
            {
                if (requirement != ColumnRequirement.Required)
                    continue;

                var currentValue = ParsedObservationFieldAccess.Get(obs, column);
                if (string.IsNullOrWhiteSpace(currentValue?.ToString()))
                {
                    obs.AppendValidationFlag($"COL_STD:MISSING_R_{column}");
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies default BoundType when bounds are populated but BoundType is null.
        /// </summary>
        /// <returns>True if a default was applied.</returns>
        private static bool applyDefaultBoundType(ParsedObservation obs)
        {
            #region implementation

            if (!string.IsNullOrWhiteSpace(obs.BoundType))
                return false;

            if (!obs.LowerBound.HasValue && !obs.UpperBound.HasValue)
                return false;

            if (obs.TableCategory != null && _defaultBoundType.TryGetValue(obs.TableCategory, out var defaultType))
            {
                obs.BoundType = defaultType;
                obs.AppendValidationFlag("COL_STD:BOUND_TYPE_INFERRED");
                return true;
            }

            return false;

            #endregion
        }

        #endregion Phase 4: Column Contract Enforcement

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Extracts the PrimaryValueType hint from a <c>CAPTION_HINT:caption:X</c> flag
        /// in ValidationFlags. Returns null if no caption hint is present.
        /// </summary>
        /// <remarks>
        /// The parsers (BaseTableParser, PkTableParser) produce CAPTION_HINT flags during
        /// Stage 3 parsing. This method reads those flags so Phase 3 can respect upstream
        /// evidence instead of re-analyzing the caption with simpler heuristics.
        /// </remarks>
        /// <param name="obs">Observation whose ValidationFlags to inspect.</param>
        /// <returns>The caption hint value (e.g., "Mean", "GeometricMean", "Median"), or null.</returns>
        /// <seealso cref="applyPhase3_PrimaryValueTypeMigration"/>
        private static string? extractCaptionHintType(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.ValidationFlags))
                return null;

            // Parse "CAPTION_HINT:caption:X" — the type is after the second colon
            const string prefix = "CAPTION_HINT:caption:";
            var flags = obs.ValidationFlags;
            int idx = flags.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            int start = idx + prefix.Length;
            // Find the end — either next semicolon or end of string
            int end = flags.IndexOf(';', start);
            if (end < 0) end = flags.Length;

            var hint = flags[start..end].Trim();
            return string.IsNullOrEmpty(hint) ? null : hint;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects when PrimaryValueType is "Count" but contextual fields
        /// (ParameterName, ParameterCategory, ParameterSubtype, TreatmentArm)
        /// contain percentage-related keywords, suggesting the value is actually
        /// a percentage. Flips PrimaryValueType to "Percentage" when conditions are met.
        /// </summary>
        /// <remarks>
        /// Guards:
        /// <list type="bullet">
        ///   <item>PrimaryValueType must be "Count" (case-insensitive)</item>
        ///   <item>SecondaryValueType must be null/empty (presence of a secondary type
        ///         means the parser already resolved the type pairing)</item>
        ///   <item>PrimaryValue must be &lt;= 100 (values over 100 cannot be percentages)</item>
        ///   <item>At least one of the four scanned fields must contain a percentage keyword</item>
        /// </list>
        /// </remarks>
        /// <param name="obs">The observation to inspect and potentially correct.</param>
        /// <returns>True if a correction was applied.</returns>
        /// <seealso cref="PostProcessExtraction"/>
        /// <seealso cref="_percentageHintPattern"/>
        private static bool correctCountToPercentageType(ParsedObservation obs)
        {
            #region implementation

            // Guard: only applies when PrimaryValueType is "Count"
            if (!string.Equals(obs.PrimaryValueType, "Count", StringComparison.OrdinalIgnoreCase))
                return false;

            // Guard: if SecondaryValueType is set, the parser already resolved the type pairing
            if (!string.IsNullOrWhiteSpace(obs.SecondaryValueType))
                return false;

            // Guard: PrimaryValue must exist and be <= 100
            if (!obs.PrimaryValue.HasValue || obs.PrimaryValue.Value > 100.0)
                return false;

            // Scan contextual fields for percentage hints (short-circuit on first match)
            string? matchedField = null;

            if (!string.IsNullOrWhiteSpace(obs.TreatmentArm) &&
                _percentageHintPattern.IsMatch(obs.TreatmentArm))
            {
                matchedField = "TreatmentArm";
            }
            else if (!string.IsNullOrWhiteSpace(obs.ParameterName) &&
                     _percentageHintPattern.IsMatch(obs.ParameterName))
            {
                matchedField = "ParameterName";
            }
            else if (!string.IsNullOrWhiteSpace(obs.ParameterCategory) &&
                     _percentageHintPattern.IsMatch(obs.ParameterCategory))
            {
                matchedField = "ParameterCategory";
            }
            else if (!string.IsNullOrWhiteSpace(obs.ParameterSubtype) &&
                     _percentageHintPattern.IsMatch(obs.ParameterSubtype))
            {
                matchedField = "ParameterSubtype";
            }

            if (matchedField == null)
                return false;

            // Apply correction
            obs.PrimaryValueType = "Percentage";
            obs.AppendValidationFlag($"COL_STD:POST_PCT_TYPE_CORRECTED:{matchedField}");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an N-value string that may contain comma formatting (e.g., "8,506" → 8506).
        /// Strips commas before parsing to handle both plain and comma-formatted integers.
        /// </summary>
        /// <param name="raw">The raw string from a regex capture group (e.g., "8,506" or "267").</param>
        /// <param name="n">The parsed integer value.</param>
        /// <returns>True if parsing succeeded.</returns>
        /// <seealso cref="_nValuePattern"/>
        /// <seealso cref="_embeddedNPattern"/>
        private static bool tryParseNValue(string raw, out int n)
        {
            #region implementation

            n = 0;
            return int.TryParse(raw.Replace(",", ""), out n);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a drug name from the drug dictionary by matching against the observation's
        /// ProductTitle. Searches for any dictionary entry (ProductName or SubstanceName) that
        /// appears as a case-insensitive substring of ProductTitle.
        /// </summary>
        /// <remarks>
        /// Returns the longest matching drug name to prefer specific matches over short ones
        /// (e.g., "pregabalin" over "PGB" when ProductTitle is "LYRICA- pregabalin capsule").
        /// Returns null if no match is found or ProductTitle is empty.
        /// </remarks>
        /// <param name="productTitle">The observation's ProductTitle field.</param>
        /// <returns>The best-matching drug name from the dictionary, or null.</returns>
        private string? resolveDrugNameFromProductTitle(string? productTitle)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(productTitle))
                return null;

            string? bestMatch = null;
            int bestLength = 0;

            foreach (var drugName in _drugNames)
            {
                // Skip very short names (3 chars or less) to avoid false substring matches
                if (drugName.Length <= 3)
                    continue;

                if (productTitle.Contains(drugName, StringComparison.OrdinalIgnoreCase) &&
                    drugName.Length > bestLength)
                {
                    bestMatch = drugName;
                    bestLength = drugName.Length;
                }
            }

            return bestMatch;

            #endregion
        }

        #endregion Helper Methods
    }
}
