using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Deterministic, rule-based column standardization service that detects and corrects
    /// systematic misclassification of values across TreatmentArm, ArmN, DoseRegimen,
    /// StudyContext, and ParameterSubtype columns.
    /// </summary>
    /// <remarks>
    /// ## Drug Name Dictionary
    /// Loads distinct ProductName and SubstanceName values from <c>vw_ProductsByIngredient</c>
    /// at initialization. Used for content classification to distinguish drug names from
    /// doses, sample sizes, and other metadata.
    ///
    /// ## Processing Phases
    /// 1. **Classify** — Determine what type of content is in TreatmentArm and StudyContext
    /// 2. **Correct** — Apply 9 ordered rules to relocate values to correct columns
    /// 3. **Flag** — Append audit flags to ValidationFlags
    ///
    /// ## Rule Ordering
    /// Rules are applied most-specific to least-specific:
    /// N= values → format hints → severity grades → pure doses → bare numbers →
    /// drug+dose splits → embedded N in context → arm/context swap → descriptor clearing
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
            @"^\(?\s*[Nn]\s*=\s*(\d+)\s*\)?$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Embedded N= in text — drug/arm name followed by N=xxx.
        /// Matches: "Doxazosin N=339", "Placebo N=300", "HBP Foam N=351", "KANUMA N = 36"
        /// </summary>
        private static readonly Regex _embeddedNPattern = new(
            @"^(.+?)\s+[Nn]\s*=\s*(\d+)$",
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
            @"^(.+?)\s*\(\s*[Nn]\s*=\s*(\d+)\s*\)\s*(?:n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled);

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
        /// Applies column standardization rules to observations. Only processes ADVERSE_EVENT
        /// and EFFICACY categories. Modifies observations in-place.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.</param>
        /// <returns>The same list with corrected column assignments.</returns>
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
                // Only process ADVERSE_EVENT and EFFICACY
                if (!isTargetCategory(obs.TableCategory))
                    continue;

                // Skip comparison/stat rows
                if (string.Equals(obs.TreatmentArm, "Comparison", StringComparison.OrdinalIgnoreCase))
                    continue;

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
                    correctionCount++;
                    // Re-classify after arm correction for subsequent context rules
                    ctxType = classifyContent(obs.StudyContext);
                }

                // Context rules (can apply independently or after arm correction)
                if (applyRule7_CtxIsArmWithN(obs, ctxType))
                    correctionCount++;

                if (applyRule8_CtxIsDrugName(obs, ctxType))
                    correctionCount++;

                if (applyRule9_CtxIsDescriptor(obs, ctxType))
                    correctionCount++;
            }

            if (correctionCount > 0)
            {
                _logger.LogDebug("Column standardization applied {Count} corrections to {Total} observations",
                    correctionCount, observations.Count);
            }

            return observations;

            #endregion
        }

        #endregion IColumnStandardizationService Implementation

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
            if (nMatch.Success && int.TryParse(nMatch.Groups[1].Value, out var n))
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
                        // Use ProductTitle as fallback
                        obs.TreatmentArm = obs.ProductTitle;
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
                if (int.TryParse(hintMatch.Groups[2].Value, out var n))
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
                if (int.TryParse(embMatch.Groups[2].Value, out var n))
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

        #endregion Rule Methods

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

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Checks if the table category is one of the target categories for standardization.
        /// </summary>
        /// <param name="category">Table category string.</param>
        /// <returns>True if ADVERSE_EVENT or EFFICACY.</returns>
        private static bool isTargetCategory(string? category)
        {
            #region implementation

            return string.Equals(category, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(category, "EFFICACY", StringComparison.OrdinalIgnoreCase);

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

        #endregion Helper Methods
    }
}
