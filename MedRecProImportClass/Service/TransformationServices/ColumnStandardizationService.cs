using System.Net;
using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
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
            ["ASA"] = "Aspirin"
        };

        /**************************************************************/
        /// <summary>Whether the dictionary has been loaded.</summary>
        private bool _initialized;

        /**************************************************************/
        /// <summary>Whether the category should skip Phase 1 arm/context corrections (only AE+EFFICACY apply).</summary>
        private static bool isPhase1Category(string? category) =>
            string.Equals(category, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "EFFICACY", StringComparison.OrdinalIgnoreCase);

        #region Phase 2 Static Dictionaries

        /**************************************************************/
        /// <summary>
        /// PK sub-parameter names that should NOT be in DoseRegimen.
        /// When found in DoseRegimen, route to ParameterSubtype.
        /// </summary>
        private static readonly HashSet<string> _pkSubParams = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cmax", "Cmin", "Tmax", "AUC", "AUC0-inf", "AUC0-t", "AUC0-24", "AUCtau",
            "AUC(0-inf)", "AUC(0-t)", "AUC(0-24)", "AUClast",
            "t1/2", "t½", "half-life", "elimination half-life",
            "CL/F", "CLss/F", "CL", "CLss", "Clearance", "Apparent Clearance",
            "V/F", "Vd/F", "Vss", "Vd", "Vz/F", "Volume of Distribution",
            "ke", "MRT", "MAT", "F(%)", "Bioavailability",
            "CV(%)", "Cavg", "Cthrough", "Ctrough"
        };

        /**************************************************************/
        /// <summary>Regex to detect PK sub-parameter names in DoseRegimen (prefix match).</summary>
        private static readonly Regex _pkSubParamPrefixPattern = new(
            @"^(?:AUC|Cmax|Cmin|Tmax|CL(?:/F|ss)?|V(?:d|ss|z)?(?:/F)?|t(?:1/2|½)|half-?life|clearance|volume\s+of\s+distribution|MRT|MAT|ke|bioavailability|F\s*\(?\s*%\s*\)?|CV\s*\(?\s*%\s*\)?|Serum\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        /**************************************************************/
        /// <summary>
        /// Known canonical units — values that are legitimate Unit field content.
        /// </summary>
        private static readonly HashSet<string> _knownUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "%", "%CV", "h", "hr", "min", "days", "weeks", "months", "years",
            "mg", "mcg", "µg", "g", "kg",
            "mcg/mL", "ng/mL", "pg/mL", "µg/mL", "mg/L", "ng/dL", "mg/dL",
            "mcg·h/mL", "ng·h/mL", "µg·h/mL", "pg·h/mL",
            "mL/min", "mL/min/kg", "L/h", "L/h/kg", "mL/h/kg",
            "L", "mL", "L/kg",
            "mcg/kg/min", "mg/h", "IU/mL",
            "mg/kg", "mcg/kg", "mg/m²", "mg/kg/day",
            "ratio", "g/cm²", "beats/min", "mmHg", "mEq/L", "mOsm/kg",
            "percentage points", "subjects", "events", "patients",
            "ng/g", "mcg/g",
            "mg/day", "mg/d", "mcg/day"
        };

        /**************************************************************/
        /// <summary>Unit variant normalization map — non-canonical spelling → canonical form.</summary>
        private static readonly Dictionary<string, string> _unitNormalizationMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mcg h/mL"] = "mcg·h/mL",
            ["mcgh/mL"] = "mcg·h/mL",
            ["ng h/mL"] = "ng·h/mL",
            ["ngh/mL"] = "ng·h/mL",
            ["nghr/mL"] = "ng·h/mL",
            ["ug/mL"] = "mcg/mL",
            ["ug/mL"] = "mcg/mL",
            ["L/kghr"] = "L/kg/h",
            ["hrs"] = "h",
            ["hr"] = "h",
            ["pp"] = "percentage points",
            ["percent"] = "%",
            ["pct"] = "%",
            ["pg·hr/mL"] = "pg·h/mL",
            ["mcg·hr/mL"] = "mcg·h/mL",
            ["ng·hr/mL"] = "ng·h/mL",
            ["ug·hr/mL"] = "mcg·h/mL"
        };

        /**************************************************************/
        /// <summary>Keywords indicating a Unit value is actually a leaked column header.</summary>
        private static readonly HashSet<string> _unitHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Regimen", "Dosage", "Patients", "Titration", "Starting",
            "Recommended", "Duration", "TAKING", "Tablets", "Injection",
            "Therapy", "Combination", "Divided", "Subjects"
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
            ["Percentage"] = "Proportion",
            ["MeanPercentChange"] = "PercentChange",
            ["RiskDifference"] = "RiskDifference",
            ["Median"] = "Median",
            ["Count"] = "Count",
            ["Text"] = "Text",
            ["PValue"] = "PValue",
            ["SampleSize"] = "SampleSize",
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

        /**************************************************************/
        /// <summary>
        /// Structural fallback regex for novel PK unit patterns not in the known-units hash set.
        /// Matches patterns like "pg/mL", "mcg·h/mL", "ng·hr/mL", "mg/kg", "IU/mL".
        /// </summary>
        /// <seealso cref="extractUnitFromParameterSubtype"/>
        private static readonly Regex _pkUnitStructurePattern = new(
            @"^(?:(?:mc?g|ng|pg|µg|mg|IU)(?:·(?:h|hr))?/(?:mL|L|kg|m²))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// "All" prefix on drug/arm names — e.g., "All PGB", "All Doses".
        /// Used to strip "All" prefix when recovering drug name after bracket extraction.
        /// Captures: Group 1 = the actual name after "All".
        /// </summary>
        private static readonly Regex _allPrefixPattern = new(
            @"^All\s+(.+)$",
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
            ILogger<ColumnStandardizationService> logger)
        {
            #region implementation

            _dbContext = dbContext;
            _logger = logger;

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
                appendFlag(obs, $"CONFIDENCE:PATTERN:{obs.ParseConfidence ?? 0:F2}:{reason}({obsCorrectionCount})");
            }

            if (correctionCount > 0)
            {
                _logger.LogDebug("Column standardization applied {Count} corrections to {Total} observations",
                    correctionCount, observations.Count);
            }

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

            appendFlag(obs, "COL_STD:ARM_WAS_N");
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

            appendFlag(obs, "COL_STD:ARM_WAS_FMT");
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

            appendFlag(obs, "COL_STD:ARM_WAS_SEVERITY");
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

            appendFlag(obs, "COL_STD:ARM_WAS_DOSE");
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
            appendFlag(obs, "COL_STD:ARM_WAS_BARE_DOSE");
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

            appendFlag(obs, "COL_STD:SPLIT_DRUG_DOSE");
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
                appendFlag(obs, "COL_STD:CTX_WAS_ARM_N");
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
                appendFlag(obs, "COL_STD:CTX_WAS_ARM_N");
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

            appendFlag(obs, "COL_STD:SWAP_ARM_CTX");
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
            appendFlag(obs, "COL_STD:CTX_WAS_DESC");
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

            appendFlag(obs, "COL_STD:ARM_STRIP_PCT");
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

            appendFlag(obs, "COL_STD:ARM_BRACKET_N");
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
            if (normalizeUnit(obs)) corrections++;
            if (normalizeParameterCategory(obs)) corrections++;

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
                    appendFlag(obs, $"COL_STD:N_STRIPPED:{colName}");
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
                    appendFlag(obs, "COL_STD:N_STRIPPED:RawValue");
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

            // Priority 1: PK sub-parameter match → route to ParameterSubtype
            if (_pkSubParams.Contains(val) || _pkSubParamPrefixPattern.IsMatch(val))
            {
                if (string.IsNullOrEmpty(obs.ParameterSubtype))
                    obs.ParameterSubtype = val;
                obs.DoseRegimen = null;
                appendFlag(obs, "COL_STD:PK_SUBPARAM_ROUTED");
                return true;
            }

            // Priority 2: Actual dose regex → keep
            if (_actualDosePattern.IsMatch(val))
                return false;

            // Priority 3: Drug name match AND category is PK or DDI → route to ParameterSubtype
            if (isDrugName(val) &&
                (string.Equals(obs.TableCategory, "PK", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(obs.ParameterSubtype))
                    obs.ParameterSubtype = val;
                obs.DoseRegimen = null;
                appendFlag(obs, "COL_STD:COADMIN_ROUTED");
                return true;
            }

            // Priority 4: Residual population pattern
            if (_residualPopulationPattern.IsMatch(val))
            {
                if (string.IsNullOrEmpty(obs.Population))
                    obs.Population = val;
                obs.DoseRegimen = null;
                appendFlag(obs, "COL_STD:POPULATION_EXTRACTED");
                return true;
            }

            // Priority 5: Residual timepoint pattern
            if (_residualTimepointPattern.IsMatch(val))
            {
                if (string.IsNullOrEmpty(obs.Timepoint))
                    obs.Timepoint = val;
                obs.DoseRegimen = null;
                appendFlag(obs, "COL_STD:TIMEPOINT_EXTRACTED");
                return true;
            }

            // Priority 6: "Co-administered Drug" literal header echo
            if (val.Equals("Co-administered Drug", StringComparison.OrdinalIgnoreCase) ||
                val.Equals("Coadministered Drug", StringComparison.OrdinalIgnoreCase))
            {
                obs.DoseRegimen = null;
                appendFlag(obs, "COL_STD:ROW_TYPE=HEADER");
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
                    appendFlag(obs, "COL_STD:ROW_TYPE=CAPTION");
                    return true;
                }
            }

            // Priority 2: Header echo (bare "n" or "N")
            if (_paramHeaderEchoPattern.IsMatch(val))
            {
                obs.ParameterName = null;
                appendFlag(obs, "COL_STD:ROW_TYPE=HEADER");
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
                appendFlag(obs, "COL_STD:PARAM_WAS_DOSE");
                return true;
            }

            // Priority 4: DDI drug name (not a PK param) → route to ParameterSubtype
            if (string.Equals(obs.TableCategory, "DRUG_INTERACTION", StringComparison.OrdinalIgnoreCase) &&
                isDrugName(val) && !_pkSubParams.Contains(val) && !_pkSubParamPrefixPattern.IsMatch(val))
            {
                if (string.IsNullOrEmpty(obs.ParameterSubtype))
                    obs.ParameterSubtype = val;
                obs.ParameterName = null;
                appendFlag(obs, "COL_STD:COADMIN_ROUTED");
                return true;
            }

            // Priority 5: HTML entity decode
            if (_htmlEntityPattern.IsMatch(val))
            {
                obs.ParameterName = WebUtility.HtmlDecode(val);
                appendFlag(obs, "COL_STD:HTML_ENTITY_DECODED");
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
                appendFlag(obs, "COL_STD:ARM_WAS_HEADER");
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
                    appendFlag(obs, "COL_STD:ARM_BRACKET_N");
                    return true;
                }

                // Also check simple embedded N= pattern
                var embMatch = _embeddedNPattern.Match(val);
                if (embMatch.Success)
                {
                    if (tryParseNValue(embMatch.Groups[2].Value, out var n2))
                        obs.ArmN = n2;
                    obs.TreatmentArm = embMatch.Groups[1].Value.Trim();
                    appendFlag(obs, "COL_STD:ARM_BRACKET_N");
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
                    appendFlag(obs, "COL_STD:DOSE_EXTRACTED");
                    return true;
                }
            }

            // Priority 4: Generic arm labels
            if (_genericArmLabels.Contains(val))
            {
                obs.TreatmentArm = null;
                appendFlag(obs, "COL_STD:ARM_WAS_GENERIC");
                return true;
            }

            // Priority 5: Study name (all-caps short token) — only if it's NOT a drug name
            if (_studyNamePattern.IsMatch(val) && !isDrugName(val) && !_pkSubParams.Contains(val))
            {
                if (string.IsNullOrEmpty(obs.StudyContext))
                    obs.StudyContext = val;
                obs.TreatmentArm = null;
                appendFlag(obs, "COL_STD:ARM_WAS_STUDY");
                return true;
            }

            return false;

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
        /// <seealso cref="_pkUnitStructurePattern"/>
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
                if (isRecognizedUnit(candidateUnit))
                {
                    extractedUnit = candidateUnit;
                    qualifier = innerText.Substring(0, lastCommaIdx).Trim();
                }
            }

            // If no qualifier+unit match, check if the whole inner text is a unit
            if (extractedUnit == null)
            {
                if (isRecognizedUnit(innerText))
                {
                    extractedUnit = innerText;
                }
            }

            if (extractedUnit == null)
                return false;

            // Normalize the extracted unit
            if (_unitNormalizationMap.TryGetValue(extractedUnit, out var canonical))
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

            appendFlag(obs, "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a candidate string is a recognized unit — either in the known-units
        /// hash set, the normalization map, or matching the structural PK unit pattern.
        /// </summary>
        /// <param name="candidate">The candidate unit string to check.</param>
        /// <returns>True if recognized as a valid unit.</returns>
        /// <seealso cref="_knownUnits"/>
        /// <seealso cref="_unitNormalizationMap"/>
        /// <seealso cref="_pkUnitStructurePattern"/>
        private bool isRecognizedUnit(string candidate)
        {
            #region implementation

            return _knownUnits.Contains(candidate)
                || _unitNormalizationMap.ContainsKey(candidate)
                || _pkUnitStructurePattern.IsMatch(candidate);

            #endregion
        }

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

            var val = obs.Unit.Trim();

            // Rule 1: Exact match in known units → keep
            if (_knownUnits.Contains(val))
                return false;

            // Rule 2: len > 30 → likely a leaked column header
            if (val.Length > 30)
            {
                obs.Unit = null;
                appendFlag(obs, "COL_STD:UNIT_HEADER_LEAK");
                return true;
            }

            // Rule 3: Contains a drug name → leaked header
            if (isDrugName(val))
            {
                obs.Unit = null;
                appendFlag(obs, "COL_STD:UNIT_HEADER_LEAK");
                return true;
            }

            // Rule 4: Contains header keywords → leaked header
            foreach (var keyword in _unitHeaderKeywords)
            {
                if (val.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    obs.Unit = null;
                    appendFlag(obs, "COL_STD:UNIT_HEADER_LEAK");
                    return true;
                }
            }

            // Rule 5: Extractable real unit inside parentheses
            var extractMatch = _extractableUnitPattern.Match(val);
            if (extractMatch.Success)
            {
                var extracted = extractMatch.Groups[1].Value.Trim();
                if (_knownUnits.Contains(extracted))
                {
                    obs.Unit = extracted;
                    appendFlag(obs, "COL_STD:UNIT_NORMALIZED");
                    return true;
                }
            }

            // Rule 6: Variant spelling normalization
            if (_unitNormalizationMap.TryGetValue(val, out var canonical))
            {
                obs.Unit = canonical;
                appendFlag(obs, "COL_STD:UNIT_NORMALIZED");
                return true;
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
                    appendFlag(obs, "COL_STD:SOC_NORMALIZED");
                    return true;
                }
                return false;
            }

            // If original (pre-repair) was different, try that too
            if (repaired != val && _socCanonicalMap.TryGetValue(val, out canonicalSoc))
            {
                obs.ParameterCategory = canonicalSoc;
                appendFlag(obs, "COL_STD:SOC_NORMALIZED");
                return true;
            }

            // No match — flag if it doesn't already match a canonical value
            if (!_socCanonicalMap.ContainsValue(val) && !_socCanonicalMap.ContainsValue(repaired))
            {
                appendFlag(obs, "COL_STD:SOC_UNMATCHED");
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
                appendFlag(obs, $"COL_STD:PVT_MIGRATED:{oldType}→{newType}");
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
        /// Resolves "RelativeRiskReduction" → HazardRatio, OddsRatio, or RelativeRisk based on caption.
        /// </summary>
        private static string resolveRiskType(ParsedObservation obs)
        {
            #region implementation

            var caption = obs.Caption ?? "";

            if (caption.Contains("hazard", StringComparison.OrdinalIgnoreCase))
                return "HazardRatio";
            if (caption.Contains("odds", StringComparison.OrdinalIgnoreCase))
                return "OddsRatio";

            return "RelativeRisk";

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

            // AE: % → Proportion, null + integer → Count
            if (string.Equals(category, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(unit, "%", StringComparison.OrdinalIgnoreCase))
                    return "Proportion";
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
            appendFlag(obs, "COL_STD:PVT_UNRESOLVED");
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

                var currentValue = getColumnValue(obs, column);
                if (currentValue == null)
                    continue;

                setColumnValue(obs, column, null);
                appendFlag(obs, $"COL_STD:NULL_{column}");
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

                var currentValue = getColumnValue(obs, column);
                if (string.IsNullOrWhiteSpace(currentValue?.ToString()))
                {
                    appendFlag(obs, $"COL_STD:MISSING_R_{column}");
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
                appendFlag(obs, "COL_STD:BOUND_TYPE_INFERRED");
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current value of a named column from the observation using reflection-free switching.
        /// </summary>
        private static object? getColumnValue(ParsedObservation obs, string column)
        {
            #region implementation

            return column switch
            {
                "ParameterName" => obs.ParameterName,
                "ParameterCategory" => obs.ParameterCategory,
                "ParameterSubtype" => obs.ParameterSubtype,
                "TreatmentArm" => obs.TreatmentArm,
                "ArmN" => obs.ArmN,
                "StudyContext" => obs.StudyContext,
                "DoseRegimen" => obs.DoseRegimen,
                "Population" => obs.Population,
                "Timepoint" => obs.Timepoint,
                "Time" => obs.Time,
                "TimeUnit" => obs.TimeUnit,
                "PrimaryValueType" => obs.PrimaryValueType,
                "Unit" => obs.Unit,
                _ => null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets a named column to null on the observation using reflection-free switching.
        /// </summary>
        private static void setColumnValue(ParsedObservation obs, string column, object? value)
        {
            #region implementation

            switch (column)
            {
                case "ParameterName": obs.ParameterName = value as string; break;
                case "ParameterCategory": obs.ParameterCategory = value as string; break;
                case "ParameterSubtype": obs.ParameterSubtype = value as string; break;
                case "TreatmentArm": obs.TreatmentArm = value as string; break;
                case "ArmN": obs.ArmN = value as int?; break;
                case "StudyContext": obs.StudyContext = value as string; break;
                case "DoseRegimen": obs.DoseRegimen = value as string; break;
                case "Population": obs.Population = value as string; break;
                case "Timepoint": obs.Timepoint = value as string; break;
                case "Time": obs.Time = value as double?; break;
                case "TimeUnit": obs.TimeUnit = value as string; break;
                case "PrimaryValueType": obs.PrimaryValueType = value as string; break;
                case "Unit": obs.Unit = value as string; break;
            }

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
        /// Appends a standardization flag to the observation's ValidationFlags field.
        /// Follows the existing semicolon-delimited convention used by BatchValidationService.
        /// </summary>
        /// <param name="obs">Observation to flag.</param>
        /// <param name="flag">Flag string to append (e.g., "COL_STD:ARM_WAS_N").</param>
        private static void appendFlag(ParsedObservation obs, string flag)
        {
            #region implementation

            obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                ? flag
                : $"{obs.ValidationFlags}; {flag}";

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
