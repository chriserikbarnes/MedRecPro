using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Standardizes Stage 5 adverse-event parameter names and MedDRA System Organ
    /// Class values before comparator grouping and RR calculation.
    /// </summary>
    /// <remarks>
    /// This standardizer is intentionally scoped to Stage 5. It uses the existing
    /// Phase 2 AE dictionary as a seed, translates shortened legacy SOC labels into
    /// the official MedDRA SOC vocabulary, and adds JSONL-derived rescue mappings
    /// for null-category and high-frequency visualization gaps.
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = standardizer.Standardize(row);
    /// if (!result.IsExcluded)
    /// {
    ///     // row.ParameterName and row.ParameterCategory are now canonical.
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="AeStatEntityBuilder"/>
    /// <seealso cref="IAeParameterCategoryDictionaryService"/>
    internal sealed class AeMeddraTermStandardizer
    {
        #region Constants

        /**************************************************************/
        /// <summary>Audit flag emitted when a row name is normalized.</summary>
        private const string NameNormalizedFlag = "AE_STD:NAME_NORMALIZED";

        /**************************************************************/
        /// <summary>Audit flag emitted when a known AE term supplies the SOC.</summary>
        private const string SocFromNameFlag = "AE_STD:SOC_FROM_NAME";

        /**************************************************************/
        /// <summary>Audit flag emitted when category-only evidence supplies the SOC.</summary>
        private const string SocFromCategoryFlag = "AE_STD:SOC_FROM_CATEGORY";

        /**************************************************************/
        /// <summary>Audit flag emitted when only category spelling/casing changed.</summary>
        private const string SocNormalizedFlag = "AE_STD:SOC_NORMALIZED";

        /**************************************************************/
        /// <summary>Audit flag emitted when no name or category evidence resolves a SOC.</summary>
        private const string SocUnmappedFlag = "AE_STD:SOC_UNMAPPED";

        /**************************************************************/
        /// <summary>Audit flag emitted when a structural or threshold row is excluded.</summary>
        private const string ExcludedFlag = "AE_STD:EXCLUDED_NON_AE";

        #endregion Constants

        #region Static Data

        /**************************************************************/
        /// <summary>Official MedDRA 27 System Organ Class names in title case.</summary>
        private static readonly HashSet<string> _officialSocSet = new(StringComparer.Ordinal)
        {
            "Blood and Lymphatic System Disorders",
            "Cardiac Disorders",
            "Congenital, Familial and Genetic Disorders",
            "Ear and Labyrinth Disorders",
            "Endocrine Disorders",
            "Eye Disorders",
            "Gastrointestinal Disorders",
            "General Disorders and Administration Site Conditions",
            "Hepatobiliary Disorders",
            "Immune System Disorders",
            "Infections and Infestations",
            "Injury, Poisoning and Procedural Complications",
            "Investigations",
            "Metabolism and Nutrition Disorders",
            "Musculoskeletal and Connective Tissue Disorders",
            "Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)",
            "Nervous System Disorders",
            "Pregnancy, Puerperium and Perinatal Conditions",
            "Product Issues",
            "Psychiatric Disorders",
            "Renal and Urinary Disorders",
            "Reproductive System and Breast Disorders",
            "Respiratory, Thoracic and Mediastinal Disorders",
            "Skin and Subcutaneous Tissue Disorders",
            "Social Circumstances",
            "Surgical and Medical Procedures",
            "Vascular Disorders"
        };

        /**************************************************************/
        /// <summary>HTML, OCR, and parser whitespace cleanup regex.</summary>
        private static readonly Regex _whiteSpacePattern = new(@"\s+", RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Raw category aliases, shortened repo SOC labels, and JSONL category variants.</summary>
        private static readonly Dictionary<string, string> _rawCategoryToOfficialSoc = createCategoryMap();

        /**************************************************************/
        /// <summary>Raw category strings that are not SOC evidence.</summary>
        private static readonly HashSet<string> _unmappableCategorySet = createUnmappableCategorySet();

        /**************************************************************/
        /// <summary>Name variants that should collapse to one display name before grouping.</summary>
        private static readonly Dictionary<string, string> _canonicalNameByVariant = createCanonicalNameMap();

        /**************************************************************/
        /// <summary>JSONL-derived and curated canonical AE terms not covered reliably by Phase 2.</summary>
        private static readonly Dictionary<string, string> _officialSocByCanonicalName = createNameSocMap();

        /**************************************************************/
        /// <summary>Exact threshold-only names supplied in the visualization-quality plan.</summary>
        private static readonly HashSet<string> _exactBadNameSet = createBadNameSet();

        /**************************************************************/
        /// <summary>Structural name strings that are not analyzable AE preferred terms.</summary>
        private static readonly HashSet<string> _structuralNameSet = createStructuralNameSet();

        /**************************************************************/
        /// <summary>Tokens that must remain uppercase or symbol-preserving after title casing.</summary>
        private static readonly string[] _protectedTokens =
        [
            "ALT",
            "AST",
            "ALP",
            "ECG",
            "EEG",
            "CPK",
            "LDL-C",
            "HDL-C",
            "COVID-19",
            "URI",
            "ULN",
            "SGOT",
            "SGPT",
            "QTc",
            "GGT",
            "IOP",
            "EPS",
            "CHF",
            "MI",
            "CV",
            "T2DM"
        ];

        /**************************************************************/
        /// <summary>Threshold or range fragments that are unsafe as standalone AE names.</summary>
        private static readonly Regex _thresholdOnlyPattern = new(
            @"^\s*(?:[<>]=?\s*)?\d[\d,]*(?:\.\d+)?(?:" +
            @"\s*(?:to|-)\s*(?:[<>]=?\s*)?\d[\d,]*(?:\.\d+)?)?" +
            @"(?:\s*(?:(?:x\s*)?ULN|cells?\s*/\s*mm(?:3)?|/\s*mm(?:3)?|g\s*/\s*L|mmol\s*/\s*L|mg\s*/\s*dL|%))?" +
            @"(?:\s*(?:[<>]=?|to|-)\s*\d[\d,]*(?:\.\d+)?" +
            @"(?:\s*(?:(?:x\s*)?ULN|cells?\s*/\s*mm(?:3)?|/\s*mm(?:3)?|g\s*/\s*L|mmol\s*/\s*L|mg\s*/\s*dL|%))?)*\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Leading category-name prefixes embedded into JSONL ParameterName values.</summary>
        private static readonly IReadOnlyList<KeyValuePair<string, string>> _categoryPrefixLookup =
            _rawCategoryToOfficialSoc
                .OrderByDescending(kvp => kvp.Key.Length)
                .ToList();

        #endregion Static Data

        #region Fields

        /**************************************************************/
        /// <summary>Existing Phase 2 dictionary used as the seed name-to-SOC source.</summary>
        private readonly IAeParameterCategoryDictionaryService _aeDictionary;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a Stage 5 AE MedDRA term standardizer.
        /// </summary>
        /// <remarks>
        /// The optional dictionary parameter keeps the type testable while production
        /// callers can use the existing committed <see cref="AeParameterCategoryDictionaryService"/>.
        /// </remarks>
        /// <param name="aeDictionary">AE dictionary seed, or null to use the default service.</param>
        /// <seealso cref="AeParameterCategoryDictionaryService"/>
        internal AeMeddraTermStandardizer(IAeParameterCategoryDictionaryService? aeDictionary = null)
        {
            #region implementation

            _aeDictionary = aeDictionary ?? new AeParameterCategoryDictionaryService();

            #endregion
        }

        #endregion Constructor

        #region Public Test Surface

        /**************************************************************/
        /// <summary>Gets the number of official MedDRA SOC labels in the guard set.</summary>
        internal static int OfficialSocCount
        {
            get
            {
                #region implementation

                return _officialSocSet.Count;

                #endregion
            }
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a display SOC belongs to the official MedDRA SOC set.
        /// </summary>
        /// <param name="soc">Candidate SOC display value.</param>
        /// <returns><c>true</c> when the value is one of the 27 official SOC labels.</returns>
        internal static bool IsOfficialSoc(string? soc)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(soc) &&
                   _officialSocSet.Contains(soc.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether the supplied name should be excluded before AE grouping.
        /// </summary>
        /// <param name="parameterName">Candidate ParameterName value.</param>
        /// <returns><c>true</c> when the value is a threshold, rollup, endpoint, or header.</returns>
        internal static bool IsExcludedFromVisualization(string? parameterName)
        {
            #region implementation

            var cleanName = cleanText(parameterName);
            if (string.IsNullOrWhiteSpace(cleanName))
                return true;

            var key = normalizeLookupKey(cleanName);
            if (_exactBadNameSet.Contains(key) || _structuralNameSet.Contains(key))
                return true;

            if (_thresholdOnlyPattern.IsMatch(cleanName))
                return true;

            return isStructuralByPattern(cleanName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a raw category to an official SOC for tests and JSONL coverage checks.
        /// </summary>
        /// <param name="category">Raw category value.</param>
        /// <returns>Official SOC when the category is mappable; otherwise null.</returns>
        internal static string? ResolveRawCategoryForTesting(string? category)
        {
            #region implementation

            return resolveRawCategory(category);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a category is explicitly known to be non-SOC evidence.
        /// </summary>
        /// <param name="category">Raw category value.</param>
        /// <returns><c>true</c> when the value is an explicit unmappable category/header.</returns>
        internal static bool IsExplicitlyUnmappableCategory(string? category)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(category))
                return false;

            var cleanCategory = cleanText(category);
            var key = normalizeLookupKey(cleanCategory);
            return _unmappableCategorySet.Contains(key) || isUnmappableCategoryByPattern(cleanCategory);

            #endregion
        }

        #endregion Public Test Surface

        #region Standardization

        /**************************************************************/
        /// <summary>
        /// Standardizes one Stage 5 source row in place and returns audit flags.
        /// </summary>
        /// <remarks>
        /// Name-derived SOC evidence is authoritative when present because category
        /// headers in historical AE tables often leak body systems, severity bands,
        /// study arms, or laboratory families that conflict with a known AE term.
        /// </remarks>
        /// <param name="row">Source row loaded from <c>tmp_FlattenedStandardizedTable</c>.</param>
        /// <returns>Standardization outcome and audit flags.</returns>
        /// <seealso cref="AdverseEventDenormalizationService"/>
        internal AeMeddraStandardizationResult Standardize(LabelView.FlattenedStandardizedTable row)
        {
            #region implementation

            var flags = new List<string>();
            var originalName = row.ParameterName;
            var originalCategory = row.ParameterCategory;
            var cleanName = cleanText(originalName);
            var cleanCategory = cleanText(originalCategory);

            if (IsExcludedFromVisualization(cleanName))
            {
                flags.Add(ExcludedFlag);
                return new AeMeddraStandardizationResult(true, flags);
            }

            var canonicalName = resolveCanonicalName(cleanName);
            if (!string.Equals(originalName, canonicalName, StringComparison.Ordinal))
            {
                row.ParameterName = canonicalName;
                flags.Add(NameNormalizedFlag);
                addValueChangeFlag(flags, NameNormalizedFlag, originalName, canonicalName);
            }

            var nameSoc = resolveSocFromName(canonicalName, cleanName);
            var categorySoc = resolveRawCategory(cleanCategory);
            var rawCategoryIsUnmappable = string.IsNullOrWhiteSpace(cleanCategory) ||
                                          IsExplicitlyUnmappableCategory(cleanCategory);

            if (nameSoc is not null)
            {
                if (rawCategoryIsUnmappable || categorySoc is null)
                {
                    row.ParameterCategory = nameSoc;
                    flags.Add(SocFromNameFlag);
                    addValueChangeFlag(flags, SocFromNameFlag, originalCategory, nameSoc);
                }
                else if (!string.Equals(categorySoc, nameSoc, StringComparison.Ordinal))
                {
                    row.ParameterCategory = nameSoc;
                    flags.Add("AE_STD:SOC_ALIGNED");
                    addValueChangeFlag(flags, "AE_STD:SOC_ALIGNED", originalCategory, nameSoc);
                }
                else
                {
                    row.ParameterCategory = nameSoc;
                    if (!string.Equals(cleanCategory, nameSoc, StringComparison.Ordinal))
                    {
                        flags.Add(SocNormalizedFlag);
                        addValueChangeFlag(flags, SocNormalizedFlag, originalCategory, nameSoc);
                    }
                }

                return new AeMeddraStandardizationResult(false, flags);
            }

            if (categorySoc is not null)
            {
                row.ParameterCategory = categorySoc;
                if (!string.Equals(cleanCategory, categorySoc, StringComparison.Ordinal))
                {
                    flags.Add(SocFromCategoryFlag);
                    addValueChangeFlag(flags, SocFromCategoryFlag, originalCategory, categorySoc);
                }

                return new AeMeddraStandardizationResult(false, flags);
            }

            row.ParameterCategory = null;
            flags.Add(SocUnmappedFlag);
            if (!string.IsNullOrWhiteSpace(originalCategory))
                addValueChangeFlag(flags, SocUnmappedFlag, originalCategory, null);
            return new AeMeddraStandardizationResult(false, flags);

            #endregion
        }

        #endregion Standardization

        #region Name Resolution

        /**************************************************************/
        /// <summary>
        /// Resolves the canonical display name for an AE term.
        /// </summary>
        /// <param name="name">Cleaned ParameterName text.</param>
        /// <returns>Canonical title-case display name.</returns>
        private string resolveCanonicalName(string name)
        {
            #region implementation

            var key = normalizeLookupKey(name);
            if (_canonicalNameByVariant.TryGetValue(key, out var mappedName))
                return mappedName;

            var dictionaryName = _aeDictionary.NormalizeParameterName(name);
            if (!string.IsNullOrWhiteSpace(dictionaryName))
            {
                var dictionaryKey = normalizeLookupKey(dictionaryName);
                if (_canonicalNameByVariant.TryGetValue(dictionaryKey, out mappedName))
                    return mappedName;

                return toDisplayTitleCase(dictionaryName);
            }

            return toDisplayTitleCase(stripNoiseSuffixes(name));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves an official SOC from canonical or raw name evidence.
        /// </summary>
        /// <param name="canonicalName">Canonical AE name.</param>
        /// <param name="rawName">Cleaned raw AE name.</param>
        /// <returns>Official SOC when name evidence is sufficient; otherwise null.</returns>
        private string? resolveSocFromName(string canonicalName, string rawName)
        {
            #region implementation

            foreach (var candidate in getNameLookupCandidates(canonicalName, rawName))
            {
                var key = normalizeLookupKey(candidate);
                if (_officialSocByCanonicalName.TryGetValue(key, out var explicitSoc))
                    return explicitSoc;

                var dictionarySoc = _aeDictionary.Resolve(candidate);
                var officialDictionarySoc = resolveRawCategory(dictionarySoc);
                if (officialDictionarySoc is not null)
                    return officialDictionarySoc;
            }

            var prefixSoc = resolveEmbeddedCategoryPrefixSoc(canonicalName);
            if (prefixSoc is not null)
                return prefixSoc;

            return resolveSocByNamePattern(canonicalName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds name lookup candidates from canonical, raw, and stripped variants.
        /// </summary>
        /// <param name="canonicalName">Canonical AE name.</param>
        /// <param name="rawName">Raw cleaned AE name.</param>
        /// <returns>Distinct lookup candidates.</returns>
        private static IEnumerable<string> getNameLookupCandidates(string canonicalName, string rawName)
        {
            #region implementation

            var candidates = new[]
            {
                canonicalName,
                rawName,
                stripNoiseSuffixes(canonicalName),
                stripNoiseSuffixes(rawName)
            };

            return candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves names that embed a category prefix before the actual AE term.
        /// </summary>
        /// <param name="name">Canonical display name.</param>
        /// <returns>Official SOC from the suffix term or prefix SOC.</returns>
        private string? resolveEmbeddedCategoryPrefixSoc(string name)
        {
            #region implementation

            var key = normalizeLookupKey(name);
            foreach (var prefix in _categoryPrefixLookup)
            {
                if (!key.StartsWith(prefix.Key, StringComparison.Ordinal) || key.Length == prefix.Key.Length)
                    continue;

                var suffix = key[prefix.Key.Length..];
                if (suffix.Length < 3)
                    return prefix.Value;

                var suffixDisplay = toDisplayTitleCase(suffix);
                var suffixSoc = resolveSocFromName(suffixDisplay, suffixDisplay);
                return suffixSoc ?? prefix.Value;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves official SOCs from conservative JSONL-derived term patterns.
        /// </summary>
        /// <param name="name">Canonical AE name.</param>
        /// <returns>Official SOC when a stable pattern is recognized.</returns>
        private static string? resolveSocByNamePattern(string name)
        {
            #region implementation

            var key = normalizeLookupKey(name);

            if (containsAny(key, "ALANINEAMINOTRANSFERASE", "ASPARTATEAMINOTRANSFERASE", "AMINOTRANSFERASE", "ALT", "AST", "ALP", "SGOT", "SGPT", "BILIRUBIN", "CREATININE", "CPK", "PHOSPHOKINASE", "GAMMAGLUTAMYL", "GGT", "TRANSAMINASE", "LDH", "INR", "LIPASE", "AMYLASE", "QTC", "ELECTROCARDIOGRAM", "BLOODGLUCOSE", "BLOODPRESSURE", "BLOODTESTOSTERONE", "EOSINOPHIL", "LACTATEDEHYDROGENASE", "WHITEBLOODCELL", "WBC", "HEMATOCRIT", "HEMOGLOBIN", "CORTISOL", "INSULIN"))
                return "Investigations";

            if (containsAny(key, "BLOODANDLYMPHATIC", "ANEMIA", "BLEEDING", "HEMORRHAGE", "HEMORRHAGIC", "GRANULOCYTOPENIA", "NEUTROPHIL", "LEUKOCYTE", "PLATELET"))
                return "Blood and Lymphatic System Disorders";

            if (containsAny(key, "CANDIDIASIS", "INFECTION", "INFECT", "TINEA", "NASOPHARYNGITIS", "SINUSITIS", "GINGIVITIS", "TONSILLITIS", "FLU"))
                return "Infections and Infestations";

            if (containsAny(key, "GIDISTURBANCES", "NAUSEA", "DIARRHEA", "VOMITING", "CONSTIPATION", "DYSPEPSIA", "INDIGESTION", "HEARTBURN", "STOMATITIS", "ESOPHAGITIS", "ABDOMINAL", "RECTAL", "HERNIA", "STOOLS", "ULCER", "MOUTHANDAPHTHOUS", "FECES", "FLATULENCE", "GAS", "SALIVATION"))
                return "Gastrointestinal Disorders";

            if (containsAny(key, "CHOLELITHIASIS", "LIVER", "HEPATIC"))
                return "Hepatobiliary Disorders";

            if (containsAny(key, "EXTRAPYRAMIDAL", "DYSTON", "DYSKIN", "PARKINSON", "TREMOR", "PARESTHESIA", "PARAESTHESIA", "TINGLING", "SENSORY", "DIZZINESS", "SOMNOL", "SEDATION", "BRADYKINESIA", "CATAPLEXY", "GAIT", "HEADACHE", "HANGOVER", "MENTALSTATUS", "MOTIONSICKNESS", "NEUROMUSCULAR", "PARESIS", "MULTIPLESCLEROSIS", "SLUGGISHNESS", "STROKE", "TORTICOLLIS", "INTELLECTUALABILITY", "ATYPICALSENSATIONS"))
                return "Nervous System Disorders";

            if (containsAny(key, "HYPERSENSITIVITY"))
                return "Immune System Disorders";

            if (containsAny(key, "ANXIETY", "DEPRESS", "MOOD", "PANIC", "PSYCH", "SCHIZOPHRENIA", "OBSESSIVE", "JITTERY", "NERVOUSNESS", "SLEEPDISTURB", "INSOMNIA"))
                return "Psychiatric Disorders";

            if (containsAny(key, "OCULAR", "INTRAOCULAR", "IOP", "VISION", "VISUAL", "AMBLYOPIA", "KERATITIS", "RETINAL", "MACULAR", "EYE", "BLEPHAROSPASM"))
                return "Eye Disorders";

            if (containsAny(key, "DYSPNEA", "COUGH", "WHEEZ", "RESPIRATORY", "LUNG", "ASTHMA", "SPUTUM", "VOICE", "NASAL", "PHARYNG", "BRADYPNEA", "ATELECTASIS", "PULMONARYEDEMA", "SINUSABNORMALITY"))
                return "Respiratory, Thoracic and Mediastinal Disorders";

            if (containsAny(key, "RASH", "PRURITUS", "ITCH", "SKIN", "DERMATITIS", "PIGMENTATION", "SCALING", "STINGING", "BURNING", "HYPERHIDROSIS", "SWEATING", "PILOERECTION", "STRIAE", "DRYNESS", "OILINESS", "PEELING", "EXFOLIATION", "EXCORIATION", "EROSION", "FLAKING", "SCABBING", "URTICARIA", "VESICLES"))
                return "Skin and Subcutaneous Tissue Disorders";

            if (containsAny(key, "BACKPAIN", "ARTHRALGIA", "ARTHRITIS", "MYALGIA", "MUSCLE", "LIGAMENT", "JOINT", "SKELETAL", "EXTREMITIES", "JAWPAIN", "TENOSYNOVITIS", "MUSCULOSKELETAL", "BONE"))
                return "Musculoskeletal and Connective Tissue Disorders";

            if (containsAny(key, "HYPERTENSION", "HYPOTENSION", "ORTHOSTATIC", "FLUSH", "HOTFLASH", "VASOMOTOR"))
                return "Vascular Disorders";

            if (containsAny(key, "ACUTECORONARYSYNDROME", "HEARTRATE", "CARDIAC", "TACHYCARDIA", "VENTRICULAR", "TORSAD", "AVBLOCK", "BRADYCARDIA", "ANGINA", "CHF", "RHYTHM", "FIBRILLATIONATRIAL", "HEARTBLOCK"))
                return "Cardiac Disorders";

            if (containsAny(key, "OLIGURIA", "URINE", "URINARY", "GLYCOSURIA", "ALBUMINURINE", "URINATION", "UROGENITAL"))
                return "Renal and Urinary Disorders";

            if (containsAny(key, "IMPOTENCE", "EJACULATION", "ERECTION", "LIBIDO", "ORGASMIC", "BREAST", "UTERINE", "ENDOMETRIAL", "VAGINAL", "GENITAL", "GYNECOLOGICAL"))
                return "Reproductive System and Breast Disorders";

            if (containsAny(key, "HYPOGLYCEMIA", "HYPOPHENYLALANINEMIA", "APPETITE", "WEIGHT", "VITAMINDEFICIENCY", "CENTRALOBESITY"))
                return "Metabolism and Nutrition Disorders";

            if (containsAny(key, "CUSHINGOID", "HORMONELEVEL"))
                return "Endocrine Disorders";

            if (containsAny(key, "INJURY", "LACERATION", "HEMATOMA", "BRUISING", "PROCEDURAL", "POSTPROCEDURAL", "ROADTRAFFICACCIDENT", "SURGICALINTERVENTION", "NERVEINJURY"))
                return "Injury, Poisoning and Procedural Complications";

            if (containsAny(key, "APPLICATIONSITE", "INFUSIONSITE", "FATIGUE", "EDEMA", "PAIN", "HYPERPYREXIA", "BODYTEMPERATURE", "HEAVINESS", "COLD", "WARM", "DISCOMFORT", "ENERGYINCREASED", "INDURATION", "TEETHING"))
                return "General Disorders and Administration Site Conditions";

            if (containsAny(key, "POLYP", "NEOPLASM", "NODULE", "CARCINOMA"))
                return "Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)";

            if (containsAny(key, "SMELL", "TASTE", "VERTIGO"))
                return "Ear and Labyrinth Disorders";

            return null;

            #endregion
        }

        #endregion Name Resolution

        #region Category Resolution

        /**************************************************************/
        /// <summary>
        /// Resolves a raw category alias to an official MedDRA SOC value.
        /// </summary>
        /// <param name="category">Raw category value.</param>
        /// <returns>Official SOC when resolved; otherwise null.</returns>
        private static string? resolveRawCategory(string? category)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(category))
                return null;

            var cleanCategory = cleanText(category);
            var key = normalizeLookupKey(cleanCategory);

            if (_officialSocSet.Contains(cleanCategory))
                return cleanCategory;

            if (_rawCategoryToOfficialSoc.TryGetValue(key, out var mappedSoc))
                return mappedSoc;

            if (_unmappableCategorySet.Contains(key) || isUnmappableCategoryByPattern(cleanCategory))
                return null;

            return resolveSocByCategoryPattern(cleanCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves category aliases with conservative normalized-pattern matching.
        /// </summary>
        /// <param name="category">Clean raw category value.</param>
        /// <returns>Official SOC when a category family is recognized; otherwise null.</returns>
        private static string? resolveSocByCategoryPattern(string category)
        {
            #region implementation

            var key = normalizeLookupKey(category);

            if (containsAny(key, "BLOODANDLYMPHATIC", "HEMATOLOG", "PLATELETBLEEDING", "REDBLOODCELL"))
                return "Blood and Lymphatic System Disorders";

            if (containsAny(key, "CARDIAC", "HEARTRATE", "CARDIOVASCULAR", "VARIOUSFORMSOFBLOCK"))
                return "Cardiac Disorders";

            if (containsAny(key, "EARANDLABYRINTH", "EARNOSEANDTHROAT", "SPECIALSENSE"))
                return "Ear and Labyrinth Disorders";

            if (containsAny(key, "ENDOCRINE"))
                return "Endocrine Disorders";

            if (containsAny(key, "EYE", "OCULAR", "VISION"))
                return "Eye Disorders";

            if (containsAny(key, "GASTROINTESTINAL", "DIGESTIVE", "DIARRHEA", "VOMITING", "NECROTIZINGENTEROCOLITIS"))
                return "Gastrointestinal Disorders";

            if (containsAny(key, "GENERAL", "BODYASWHOLE", "BODYGENERAL", "WHOLEBODY", "APPLICATIONSITEDISORDERS", "CONSTITUTIONALSYMPTOMS", "DISCOMFORT", "NONOCULAR", "NONSITE", "NONSPECIFIC", "PAINANDPRESSURESENSATIONS", "MISCELLANEOUS", "OTHER"))
                return "General Disorders and Administration Site Conditions";

            if (containsAny(key, "HEPATIC", "HEPATOBILIARY", "TOTALBILIRUBIN"))
                return "Hepatobiliary Disorders";

            if (containsAny(key, "IMMUNESYSTEM"))
                return "Immune System Disorders";

            if (containsAny(key, "INFECTIONS", "INFECTION", "RESISTANCEMECHANISM"))
                return "Infections and Infestations";

            if (containsAny(key, "INJURY", "POISONING", "ACCIDENTALINJURY"))
                return "Injury, Poisoning and Procedural Complications";

            if (containsAny(key, "INVESTIGATIONS", "CHEMISTRY", "BIOCHEMICAL", "LABORATORY", "CLINICASSESSMENTS", "ALANINEAMINOTRANSFERASE", "ASPARTATEAMINOTRANSFERASE", "SERUMCREATININE", "SGOT", "SGPT", "LDLC", "TRIGLYCERIDES"))
                return "Investigations";

            if (containsAny(key, "METABOL", "ACIDOSIS"))
                return "Metabolism and Nutrition Disorders";

            if (containsAny(key, "MUSCULOSKELETAL"))
                return "Musculoskeletal and Connective Tissue Disorders";

            if (containsAny(key, "NEOPLASMS"))
                return "Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)";

            if (containsAny(key, "NERVOUS", "NEURO", "DROWSINESS", "PARESTHESIA", "ATYPICALSENSATIONS", "AUTONOMIC"))
                return "Nervous System Disorders";

            if (containsAny(key, "PSYCHIATRIC"))
                return "Psychiatric Disorders";

            if (containsAny(key, "RENAL", "URINARY", "UROGENITAL", "UROGENTIAL", "GENITOURINARY", "URINARYSYSTEM"))
                return "Renal and Urinary Disorders";

            if (containsAny(key, "REPRODUCTIVE"))
                return "Reproductive System and Breast Disorders";

            if (containsAny(key, "RESPIRATORY", "RESPIRATION", "DYSPNEA", "LOWERRESPIRATORY"))
                return "Respiratory, Thoracic and Mediastinal Disorders";

            if (containsAny(key, "SKIN", "DERMATOLOG", "IRRITATION", "DRYSKIN", "INCREASEDSWEATING"))
                return "Skin and Subcutaneous Tissue Disorders";

            if (containsAny(key, "SOCIALCIRCUMSTANCES"))
                return "Social Circumstances";

            if (containsAny(key, "SURGICALANDMEDICALPROCEDURES"))
                return "Surgical and Medical Procedures";

            if (containsAny(key, "VASCULAR", "VASCULAREXTRACARDIAC"))
                return "Vascular Disorders";

            return null;

            #endregion
        }

        #endregion Category Resolution

        #region Exclusion Helpers

        /**************************************************************/
        /// <summary>
        /// Determines whether a cleaned name is a structural, endpoint, or rollup row.
        /// </summary>
        /// <param name="name">Cleaned ParameterName.</param>
        /// <returns><c>true</c> when the value should not enter visualization output.</returns>
        private static bool isStructuralByPattern(string name)
        {
            #region implementation

            var key = normalizeLookupKey(name);

            if (key.Length <= 2 && !string.Equals(key, "MI", StringComparison.Ordinal))
                return true;

            if (containsAny(key,
                    "ALLADVERSEREACTIONS",
                    "ALLADVERSEEVENTS",
                    "ALLCAUSEMORTALITY",
                    "ALLEPSEVENTS",
                    "ADJUDICATEDCARDIOVASCULARMORTALITY",
                    "ADVERSEREACTIONSGRADES",
                    "AMPUTATIONINCIDENCERATE",
                    "ANYEVENT",
                    "ANYSYSTEMANYTERM",
                    "ATLEASTONE",
                    "AVERAGEDURATIONOFEXPOSURE",
                    "AVERAGEEXPOSURE",
                    "MEDIANEXPOSURE",
                    "MORTALITY",
                    "NEEDFORCORONARYREVASCULARIZATION",
                    "NEWDIAGNOSISOFPERIPHERALVASCULARDISEASE",
                    "NONFATALMYOCARDIALINFARCTION",
                    "PATIENTSWITHATLEAST",
                    "REVASCULARIZATION",
                    "SUBJECTSWITHATLEAST",
                    "SUBJECTSWITHAE",
                    "TOTALAMPUTATIONS",
                    "TOTALNUMBEROFAE",
                    "TOTALNUMBEROFADVERSE",
                    "TOTALOFREPORTS",
                    "PERMANENTDISCONTINUATION",
                    "DISCONTINUATIONATANYTIME",
                    "ADVERSEREACTIONSLEADINGTOTREATMENTDISCONTINUATION",
                    "TREATMENTDISCONTINUATION",
                    "ANYIOPLOWERINGMEDICATION",
                    "ANYSURGICALINTERVENTION",
                    "DICTIONARYDERIVEDTERM",
                    "BODYASWHOLE",
                    "BODYASAWHOLE",
                    "PATIENTS",
                    "WOMENONLY",
                    "MENONLY",
                    "FEMALE",
                    "MALE",
                    "OVERALL"))
            {
                return true;
            }

            if (containsAny(key, "TIMIMAJOR", "CRNM", "MACE", "CVDEATH", "NONFATALMI", "NONFATALSTROKE"))
                return true;

            if (Regex.IsMatch(name, @"^\s*(?:Major|Minor|Fatal|All|Overall|Patients|Female|Male)\s*$", RegexOptions.IgnoreCase))
                return true;

            if (Regex.IsMatch(name, @"^\s*(?:Add-on|Monotherapy|With\s+).*(?:weeks?|insulin|metformin|sulfonylurea|glimepiride|pioglitazone|DPP4)", RegexOptions.IgnoreCase))
                return true;

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a raw category matches a structural or regimen pattern.
        /// </summary>
        /// <param name="category">Cleaned category value.</param>
        /// <returns><c>true</c> when the category has no SOC value.</returns>
        private static bool isUnmappableCategoryByPattern(string category)
        {
            #region implementation

            var key = normalizeLookupKey(category);

            if (containsAny(key,
                    "ADVERSEREACTION",
                    "ADVERSEEVENT",
                    "PERCENTOVERALL",
                    "PREFERREDTERM",
                    "BODYSYSTEMEVENT",
                    "EVENTDISCONTINUING",
                    "DISCONTINUATIONSDUETOADVERSEREACTIONS",
                    "DURATIONRECEIVINGDRUGTREATMENT",
                    "DURATIONOFSUSPENSIONFROMDRUGTREATMENT",
                    "TOTALDURATIONOFTREATMENT",
                    "REASONSFORSTOPPING",
                    "MONOTHERAPY",
                    "WITHMETFORMIN",
                    "WITHASULFONYLUREA",
                    "WITHMETFORMINANDASULFONYLUREA",
                    "GRADE2",
                    "GRADE3",
                    "GRADE4"))
            {
                return true;
            }

            return Regex.IsMatch(category, @"^\s*(?:Grade\s*\d+|%|Rate|Preferred\s+Term)\b", RegexOptions.IgnoreCase);

            #endregion
        }

        #endregion Exclusion Helpers

        #region Text Helpers

        /**************************************************************/
        /// <summary>
        /// Cleans raw table text for dictionary lookup and display normalization.
        /// </summary>
        /// <param name="text">Raw text from Stage 5 source rows.</param>
        /// <returns>Cleaned text with repaired entities, mojibake, and whitespace.</returns>
        private static string cleanText(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var decoded = WebUtility.HtmlDecode(text);
            decoded = decoded
                .Replace("â‰¥", ">=", StringComparison.Ordinal)
                .Replace("â‰¤", "<=", StringComparison.Ordinal)
                .Replace("â€“", "-", StringComparison.Ordinal)
                .Replace("â€”", "-", StringComparison.Ordinal)
                .Replace("â€‘", "-", StringComparison.Ordinal)
                .Replace("Â", "", StringComparison.Ordinal)
                .Replace('\u2212', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('\u00A0', ' ');

            return _whiteSpacePattern.Replace(decoded, " ").Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a punctuation-insensitive uppercase lookup key.
        /// </summary>
        /// <param name="text">Raw or cleaned text.</param>
        /// <returns>Normalized lookup key.</returns>
        private static string normalizeLookupKey(string? text)
        {
            #region implementation

            var clean = cleanText(text);
            if (clean.Length == 0)
                return string.Empty;

            clean = clean
                .Replace("&", "and", StringComparison.Ordinal)
                .Replace("+", "plus", StringComparison.Ordinal)
                .Replace("%", "percent", StringComparison.Ordinal)
                .Replace(">=", "greaterthanorequalto", StringComparison.Ordinal)
                .Replace("<=", "lessthanorequalto", StringComparison.Ordinal)
                .Replace(">", "greaterthan", StringComparison.Ordinal)
                .Replace("<", "lessthan", StringComparison.Ordinal);

            return Regex.Replace(clean.ToUpperInvariant(), @"[^A-Z0-9]+", string.Empty);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes suffixes and annotations that should not define separate AE concepts.
        /// </summary>
        /// <param name="name">AE term candidate.</param>
        /// <returns>Name without common non-concept suffixes.</returns>
        private static string stripNoiseSuffixes(string name)
        {
            #region implementation

            var stripped = Regex.Replace(name, @"^\s*[-]+\s*", "", RegexOptions.None);
            stripped = Regex.Replace(stripped, @"\s*\((?:nonserious|NOS|NEC)\)\s*$", "", RegexOptions.IgnoreCase);
            stripped = Regex.Replace(stripped, @"\s+(?:NOS|NEC)\s*$", "", RegexOptions.IgnoreCase);
            stripped = stripped.Trim('*', '^', ' ', '\t');
            return stripped.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts normal AE text to title case while preserving medical acronyms.
        /// </summary>
        /// <param name="text">Candidate display text.</param>
        /// <returns>Display title case with protected tokens restored.</returns>
        private static string toDisplayTitleCase(string text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var stripped = stripNoiseSuffixes(cleanText(text));
            if (string.IsNullOrWhiteSpace(stripped))
                return string.Empty;

            var titled = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stripped.ToLowerInvariant());
            foreach (var token in _protectedTokens)
            {
                titled = Regex.Replace(
                    titled,
                    $@"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])",
                    token,
                    RegexOptions.IgnoreCase);
            }

            titled = Regex.Replace(titled, @"\bNos\b", "NOS", RegexOptions.IgnoreCase);
            titled = Regex.Replace(titled, @"\bNec\b", "NEC", RegexOptions.IgnoreCase);
            return titled;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds an auditable old-value to new-value flag when a displayed field changes.
        /// </summary>
        /// <param name="flags">Mutable audit flag collection.</param>
        /// <param name="flagBase">Base flag reason, such as <c>AE_STD:SOC_FROM_NAME</c>.</param>
        /// <param name="oldValue">Original value before standardization.</param>
        /// <param name="newValue">Final value after standardization.</param>
        private static void addValueChangeFlag(
            ICollection<string> flags,
            string flagBase,
            string? oldValue,
            string? newValue)
        {
            #region implementation

            var oldAuditValue = formatAuditValue(oldValue);
            var newAuditValue = formatAuditValue(newValue);

            if (string.Equals(oldAuditValue, newAuditValue, StringComparison.Ordinal))
                return;

            flags.Add($"{flagBase}:{oldAuditValue}->{newAuditValue}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a field value for semicolon-delimited CalculationFlags audit text.
        /// </summary>
        /// <param name="value">Raw field value.</param>
        /// <returns>Sanitized display value, or <c>&lt;null&gt;</c> when absent.</returns>
        private static string formatAuditValue(string? value)
        {
            #region implementation

            if (value is null)
                return "<null>";

            var cleanValue = cleanText(value);
            if (cleanValue.Length == 0)
                return "<blank>";

            return sanitizeFlagValue(cleanValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes delimiter characters that would corrupt semicolon-delimited flag storage.
        /// </summary>
        /// <param name="value">Value to embed in a calculation flag.</param>
        /// <returns>Sanitized flag token value.</returns>
        private static string sanitizeFlagValue(string value)
        {
            #region implementation

            return value
                .Replace(';', ',')
                .Replace('|', '/')
                .Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a normalized lookup key contains any expected tokens.
        /// </summary>
        /// <param name="key">Normalized lookup key.</param>
        /// <param name="tokens">Normalized token fragments to search.</param>
        /// <returns><c>true</c> when any token is present.</returns>
        private static bool containsAny(string key, params string[] tokens)
        {
            #region implementation

            return tokens.Any(token => key.Contains(token, StringComparison.Ordinal));

            #endregion
        }

        #endregion Text Helpers

        #region Dictionary Construction

        /**************************************************************/
        /// <summary>
        /// Creates a normalized alias map from raw categories to official SOCs.
        /// </summary>
        /// <returns>Category alias map.</returns>
        private static Dictionary<string, string> createCategoryMap()
        {
            #region implementation

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            void add(string soc, params string[] aliases)
            {
                foreach (var alias in aliases.Concat([soc]))
                    map[normalizeLookupKey(alias)] = soc;
            }

            add("Blood and Lymphatic System Disorders", "Blood and lymphatic disorders", "Blood and lymphatic system", "Hematology", "Hematological parameters", "Hematological changes", "Hematologic and Lymphatic", "Platelet, Bleeding and Clotting Disorders", "Platelet, Bleeding & Clotting Disorders", "Red blood cell");
            add("Cardiac Disorders", "Cardiovascular system", "Cardiovascular Disorders, General", "Heart Rate and Rhythm Disorder", "Various forms of block");
            add("Congenital, Familial and Genetic Disorders", "Congenital");
            add("Ear and Labyrinth Disorders", "Ear and labyrinth", "Ear and labyrinth disorder", "Ear, nose, and throat", "Special Sense Other, Disorders", "Special Senses Other, Disorders", "Special Senses, Other Disorders");
            add("Endocrine Disorders", "Endocrine", "Endocrine System");
            add("Eye Disorders", "Ocular", "Vision", "Vision Disorders", "Special senses blurred vision");
            add("Gastrointestinal Disorders", "Digestive", "Digestive System Disorders", "Gastrointestinal Disorder", "Gastrointestinal System", "Gastrointestinal System Disorders", "Diarrhea", "Vomiting", "Necrotizing Enterocolitis");
            add("General Disorders and Administration Site Conditions", "General", "General Disorders", "General disorders and administration", "General disorders and administration-site conditions", "General disorders and administrative site conditions", "General Disorders and Administrative Site Disorders", "Body (General)", "Body as a Whole", "Body as a Whole - General Disorders", "Body as a Whole-General Disorders", "Body as Whole", "Body asawhole", "Body asaWhole", "Whole Body", "Application Site Disorders", "Constitutional symptoms", "Discomfort", "Non-ocular", "Non-site specific", "Nonspecific", "Pain and other pressure sensations", "Pain and Pressure Sensations", "Miscellaneous");
            add("Hepatobiliary Disorders", "Hepatic", "Hepatic Disorders", "Total bilirubin");
            add("Immune System Disorders", "Immune System");
            add("Infections and Infestations", "Infections", "Infections and infestation", "Infectionsand infestations", "Infections and I nfestations", "Resistance Mechanism");
            add("Injury, Poisoning and Procedural Complications", "Accidental Injury", "Injury and Poisoning", "Injury, Poisoning, and Procedural Complications");
            add("Investigations", "Chemistry", "Biochemical parameters", "Laboratory Investigations", "Laboratory Adverse Reactions", "Clinic assessments", "Alanine aminotransferase (ALT)", "Aspartate aminotransferase (AST)", "Serum creatinine", "SGOT", "SGPT", "LDL-C (mg/dL)", "Triglycerides (mg/dL)");
            add("Metabolism and Nutrition Disorders", "Metabolic", "Metabolic & Nutritional System", "Metabolic and Nutritional Disorders", "Metabolic and Nutritional System", "Metabolic/Nutritional", "Metabolism and nutrition", "Metabolism and Nutritional Disorders", "Metabolism disorders", "Acidosis");
            add("Musculoskeletal and Connective Tissue Disorders", "Musculoskeletal", "Musculoskeletal and Connective Tissue", "Musculoskeletal and trauma", "Musculoskeletal Disorders", "Musculoskeletal system", "Musculoskeletal System Disorders", "Musculoskeletal, Connective Tissue and Bone Disorders");
            add("Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)", "Neoplasms benign, malignant and unspecified (including cysts and polyps)", "Neoplasms benign, malignant, and unspecified (including cysts and polyps)");
            add("Nervous System Disorders", "Autonomic Nervous", "Autonomic Nervous System", "Autonomic Nervous System Disorders", "Central and Peripheral Nervous System", "Central and Peripheral Nervous Systems", "Central Nervous System", "Central, Peripheral Nervous system", "Central/Peripheral Nervous", "Central/peripheral nervous system", "Nervous", "Nervous System /Psychiatric", "Nervous System/Psychiatric", "Neurological", "Neurology", "Neuropsychiatric", "Drowsiness", "Paresthesia", "Atypical Sensations");
            add("Pregnancy, Puerperium and Perinatal Conditions", "Pregnancy");
            add("Product Issues", "Product Issues");
            add("Psychiatric Disorders", "Psychiatric D isorders", "Psychiatric Adverse Events in any treatment group");
            add("Renal and Urinary Disorders", "Genitourinary", "Genitourinary Disorders", "Renal and U rinary D isorders", "Renal and Urinary", "Renal disorders", "Urinary", "Urinary System", "Urinary System Disorder", "Urinary System Disorders", "Urogenital", "Urogential System");
            add("Reproductive System and Breast Disorders", "Reproductive Disorders", "Reproductive Disorders, Female", "Reproductive Disorders, Male", "Reproductive male");
            add("Respiratory, Thoracic and Mediastinal Disorders", "Lower respiratory", "Respiration", "Respiratory", "Respiratory Disorders", "Respiratory System (Upper)", "Respiratory System Disorders", "Respiratory, thoracic and mediastinal", "Respiratory, thoracic, and mediastinal", "Respiratory, Thoracic, and Mediastinal Disorders", "Dyspnea");
            add("Skin and Subcutaneous Tissue Disorders", "Dermatological", "Dermatology/Skin", "Dry Skin", "Increased sweating", "Irritation", "Skin & Appendages", "Skin and Appendage Disorders", "Skin and Appendages", "Skin and Appendages Disorders", "Skin and Cutaneous Disorders", "Skin and s ubcutaneous t issue d isorders", "Skin and subcutaneous", "Skin and subcutaneous disorders", "Skin and subcutaneous system disorders", "Skin and subcutaneous tissue", "Skin Disorders", "Skin/Appendages", "Skin/Skin Appendages Disorder");
            add("Social Circumstances", "Social circumstances");
            add("Surgical and Medical Procedures", "Surgical and medical procedures");
            add("Vascular Disorders", "Vascular", "Vascular D isorders", "Vascular extracardiac");

            return map;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the set of category values that are known not to be SOCs.
        /// </summary>
        /// <returns>Normalized unmappable category set.</returns>
        private static HashSet<string> createUnmappableCategorySet()
        {
            #region implementation

            return new HashSet<string>(
                new[]
                {
                    "(excluding akathisia)",
                    "Adverse reaction",
                    "Adverse reactions",
                    "Adverse Reactions (%)",
                    "Adverse Events >=10% in the varenicline group",
                    "Body System/Event",
                    "Discontinuations due to adverse reactions",
                    "Discontinuations due to A dverse R eaction s",
                    "Duration of Suspension from Drug Treatment",
                    "Duration Receiving Drug Treatment",
                    "Event/% Discontinuing",
                    "Grade 2",
                    "Grade 3",
                    "Grade 4",
                    "Monotherapy (24 Weeks)",
                    "Other",
                    "Other adverse reactions",
                    "Preferred Term",
                    "Psychiatric Adverse Events >=2% in any treatment group",
                    "Reasons for stopping",
                    "Reproductive System and Breast Disorders General Disorders and Administration Site Conditions",
                    "Total Duration of Treatment",
                    "With a Sulfonylurea (30 Weeks)",
                    "With Metformin (30 Weeks)",
                    "With Metformin and a Sulfonylurea (30 Weeks)"
                }.Select(normalizeLookupKey),
                StringComparer.Ordinal);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates canonical name variant mappings from JSONL and existing dictionary review.
        /// </summary>
        /// <returns>Normalized variant-to-canonical map.</returns>
        private static Dictionary<string, string> createCanonicalNameMap()
        {
            #region implementation

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            void add(string canonical, params string[] variants)
            {
                map[normalizeLookupKey(canonical)] = canonical;
                foreach (var variant in variants)
                    map[normalizeLookupKey(variant)] = canonical;
            }

            add("Abdominal Discomfort", "Abdominal - pain/discomfort/stomach pain/ cramps/pressure", "Abdominal pain, discomfort", "Abdominal Pain or Discomfort");
            add("Abdominal Pain", "Abdominal pain", "Abdominal Pain NOS", "Abdominal Pain (stomachache)");
            add("ALT Increased", "ALT increased", "Increased ALT", "ALT greater than 3x ULN", "ALT (>5 x ULN)", "SGPT Elevation");
            add("AST Increased", "AST increased", "Increased AST", "AST (>5 x ULN)", "SGOT increased");
            add("Anemia", "Anemia NOS", "Postoperative Anemia");
            add("Agitation/Irritability", "Irritability, agitation");
            add("Anxiety", "Anxiety NEC", "Anxiety/anxiety aggravated");
            add("Application Site Pain", "Application site pain", "Procedural Pain");
            add("Back Pain", "Back pain", "Back Ache", "Body asaWhole Back Pain", "Musculoskeletal disorders Back pain");
            add("Blurred Vision", "Blurry Vision", "Amblyopia (blurred vision)", "Special Senses Blurred Vision/Amblyopia");
            add("Blood Bilirubin Increased", "Blood bilirubin increased", "High Total Bilirubin", "Total bilirubin");
            add("Blood Creatine Phosphokinase Increased", "Blood creatine phosphokinase increased (CPK)", "Creatine Phosphokinase Increase", "CPK Increased");
            add("Blood Glucose Increased", "Blood glucose increased", "Glucose increased");
            add("Buffalo Hump", "Buffalo Hump");
            add("Cholelithiasis", "Cholelithiasis");
            add("Conjunctivitis", "Conjunctivitis NEC");
            add("Cough", "Cough Increased", "Respiratory, thoracic and mediastinal disorders Cough");
            add("Creatinine Increased", "Serum Creatinine Elevated", "Serum creatinine increase", "Serum creatinine increased");
            add("Death", "Death");
            add("Delayed Recovery From Anesthesia", "Delayed recovery from anesthesia");
            add("Dermatological Rash", "Dermatological Rash");
            add("Diarrhea", "Gastrointestinal system Diarrhea", "Gastrointestinal disorders Diarrhea");
            add("Dizziness", "Central/Peripheral nervous system Dizziness", "Dizziness, lightheadedness, giddiness", "Dizziness, postural");
            add("Dry Eye", "Eye disorders Dry eyes");
            add("Dyskinetic Event", "Dyskinetic event");
            add("Dystonic Event", "Dystonic event", "Dystonia**");
            add("Edema Peripheral", "General disorders Edema peripheral", "Pedal Edema", "Other Edema", "Edema/swelling");
            add("Elevated Liver Enzymes", "Elevated liver enzymes", "Increased Hepatic Enzyme", "Liver Function Abnormalities", "Liver test abnormality", "Transaminase elevations", "Transaminases increased (ALT, AST)");
            add("Female Genital Mycotic Infections", "Female genital mycotic infections");
            add("Fever In Absence Of Neutropenia", "Fever in absence of neutropenia (ANC < 1.0 x 10/L)");
            add("Headache", "Headache NOS", "Headache (NOS)", "Sinus headache");
            add("Hot Flush", "Hot flushes NOS", "Hot flushes", "Vascular disorders Hot flushes", "Hot flashes/sweats", "Flushing, heat sensation");
            add("Hunger", "Hunger");
            add("Hypertension", "Hypertension NOS", "Vascular disorders Hypertension", "Supine hypertension");
            add("Hypoacusis", "Hypoacusis");
            add("Hypoesthesia Oral", "Hypoesthesia oral");
            add("Hypomagnesaemia", "Hypomagnesaemia");
            add("Hypopnea", "Hypopnea");
            add("Increased ALP", "Increased ALP", "Alkaline Phosphatase Increased", "Blood alkaline phosphatase increased");
            add("Increased Heart Rate", "Increased Heart Rate");
            add("Increased Lacrimation", "Increased Lacrimation");
            add("Infused Vein Complication", "Infused vein complication");
            add("Injection Site Reactions", "Injection Site Reactions, any");
            add("Joint Sprain", "Joint Sprain");
            add("Ligament Sprain", "Ligament Sprain");
            add("Lip Swelling", "Lip Swelling");
            add("Male Genital Mycotic Infections", "Male genital mycotic infections");
            add("Nausea", "Digestive System Nausea");
            add("Oral Moniliasis", "Oral moniliasis");
            add("Other Constitutional Symptoms", "Other constitutional symptoms");
            add("Other Extrapyramidal Event", "Other extrapyramidal event", "Any extrapyramidal event", "Extrapyramidal event");
            add("Other GI Toxicity", "Other GI toxicity");
            add("Otitis Media", "Special Senses Otitis Media");
            add("Paresthesia", "Circumoral paresthesia", "Paraesthesia Oral");
            add("Pneumonia", "Pneumonia NOS");
            add("Pruritus", "Itching", "Generalized Pruritus");
            add("Rash", "Rash (including dermatitis)", "Skin disorders Rash", "Skin and subcutaneous tissue disorders Rash", "Skin/Skin Appendages Disorder Rash");
            add("Rigors/Chills", "Rigors/chills");
            add("Restless Legs Syndrome", "Restless Legs Syndrome");
            add("Seroma", "Seroma");
            add("Serum Alkaline Phosphatase Increased", "Serum alkaline phosphatase increased");
            add("Shivering", "Shivering");
            add("Small Intestinal Obstruction", "Small intestinal obstruction");
            add("Sneezing", "Sneezing");
            add("Swollen Ankles", "Swollen Ankles");
            add("Symptom Of Nose", "Symptom of Nose");
            add("Tachypnea", "Tachypnea");
            add("Tongue Discoloration", "Tongue discoloration");
            add("Upper Respiratory Tract Infection", "Upper Respiratory Tract Infection NOS", "Upper Respiratory Infection (URI)", "Upper Respiratory Tract Inf. NOS");
            add("Urinary Tract Infection", "Urinary Tract Infection (NOS)", "Urogenital System Urinary Tract Infection", "Infections Urinary tract infection", "Infections and Infestations Urinary tract infections");
            add("Visual Acuity Reduced", "Visual acuity reduced");
            add("Visual Disturbance", "Visual disturbance", "Visual Disturbances", "Visual disturbances");
            add("Visual Impairment", "Visual Impairment");
            add("Viral Infection", "Viral Infection");
            add("Vulvovaginal Dryness", "Vulvovaginal dryness");
            add("Vulvovaginal Pruritus", "Vulvovaginal pruritus");
            add("Vulvovaginitis", "Vulvovaginitis");
            add("Weight Decreased", "Weight decreased", "Weight decrease", "Decreased weight", "Decreased Weight*", "Weight Loss", "Weight loss", "Weight decreased*", "Lost >5 lbs");
            add("Weight Increased", "Weight increased", "Weight increase", "Increased weight", "Weight, increased", "Weight gain", "Weight Gain", "Weight gain/increased", "Gained >5 lbs", "Investigations Weight increased");
            add("Wound Complication", "Wound complication");

            return map;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates JSONL-derived canonical name-to-official-SOC mappings.
        /// </summary>
        /// <returns>Name-to-SOC map keyed by normalized canonical name.</returns>
        private static Dictionary<string, string> createNameSocMap()
        {
            #region implementation

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            void add(string soc, params string[] names)
            {
                foreach (var name in names)
                    map[normalizeLookupKey(name)] = soc;
            }

            add("Gastrointestinal Disorders", "Nausea", "Diarrhea", "Vomiting", "Constipation", "Dyspepsia", "Dry Mouth", "Abdominal Pain", "Abdominal Discomfort", "Other GI Toxicity", "Small Intestinal Obstruction", "Tongue Discoloration", "Digestive System Nausea", "Nausea, heartburn", "Abnormal stools", "Indigestion", "Rectal pain", "Mouth and aphthous ulcers", "Ulcerative stomatitis", "Esophagitis", "Large intestine polyp");
            add("Nervous System Disorders", "Headache", "Dizziness", "Somnolence", "Tremor", "Paresthesia", "Hypoesthesia Oral", "Other Extrapyramidal Event", "Dystonic Event", "Dyskinetic Event", "Restless Legs Syndrome", "Atypical Sensations", "Parkinsonism", "Parkinsonism/bradykinesia", "Bradykinesia", "Tardive Dyskinesia", "Fatigue/Somnolence", "Sluggishness", "Paresis", "Sensory Loss", "Unsteady gait", "Multiple sclerosis relapse", "Cataplexy", "Gait Disturbances", "Mental Status Change", "Taste Disorder", "Taste Disorders", "Special Senses taste perversion", "Smell Disorder");
            add("Psychiatric Disorders", "Agitation/Irritability", "Anxiety", "Feeling Jittery", "Nervousness, mood changes", "Mood changes", "Mood altered, mood swings", "Depression/emotional lability", "Depressed mood, depression, depressive symptoms and/or tearfulness", "Panic reaction", "Psychosomatic disorder", "Schizophrenia", "Obsessive reaction", "Psychiatric Disorders Insomnia", "Sleep changes", "Sleep disturbances");
            add("Skin and Subcutaneous Tissue Disorders", "Rash", "Dermatological Rash", "Pruritus", "Alopecia", "Dry Skin", "Generalized Pruritus", "Skin hemorrhage", "Dryness", "Itching", "Burning", "Piloerection", "Striae rubrae", "Oiliness/Peeling", "Rash (including dermatitis)", "Skin disorders Rash", "Skin warm", "Somnolece", "Pain of skin", "Skin burning sensation", "Hyperhydrosis", "Skin reactions", "Pigmentation", "Scaling", "Stinging", "Application site burning/stinging", "Exfoliation", "Irritation", "Abnormal skin odor", "nodule", "site warmth", "skin tightness", "Lip Swelling");
            add("General Disorders and Administration Site Conditions", "Application Site Pain", "Application Site Exfoliation", "Application Site Reactions", "Application Site Erythema", "Application site burning/stinging", "Infusion site erythema", "Infusion site nodule", "Injection Site Reactions", "General pain", "Pain (generalized)", "Pedal Edema", "Other Edema", "Edema/swelling", "Swollen Ankles", "Fatigue", "Body asawhole Fatigue", "Chest discomfort/pain", "Chest Pain (non-cardiac)", "Pain Chest, Non-Cardiac", "Pressure sensation", "Feeling of heaviness", "Heaviness", "Cold sensation", "Warm/hot sensation", "Rigors/Chills", "Shivering", "Moon face", "Hyperpyrexia", "Fever In Absence Of Neutropenia", "Other Constitutional Symptoms", "Increased Body Temperature", "Decreased Mobility", "Hospitalized, nonfatal", "Death");
            add("Eye Disorders", "Conjunctivitis", "Blurred Vision", "Eye Pain", "Blurry Vision", "Dry Eye", "Increased Lacrimation", "Intraocular pressure increased", "IOP elevation >= 10 mmHg from Baseline", "IOP elevation > 30 mmHg", "IOP elevation >= 30 mmHg", "Ocular hypertension", "Punctate keratitis", "Retinal hemorrhage", "Posterior capsule opacification", "Intraocular inflammation", "Neovascular age-related macular degeneration", "Ocular discomfort", "Blepharospasm", "Visual Acuity Reduced", "Visual Disturbance", "Visual Impairment");
            add("Respiratory, Thoracic and Mediastinal Disorders", "Cough", "Dyspnea", "Rhinitis", "Pharyngolaryngeal Pain", "Hypopnea", "Sneezing", "Symptom Of Nose", "Tachypnea", "Nasal congestion, sore throat", "Dyspnea, cough, wheezing", "Bradypnea", "Atelectasis", "Pulmonary Edema", "Decreased lung function", "Respiratory System Dyspnea", "Asthma symptoms", "Respiratory System Nasopharyngitis", "Nasal burning/nasal irritation", "Respiratory Tract Infection (Upper and Lower)", "Nasal Congestion (Including sinus congestion)", "Respiratory failure, respiratory disorder, hypoxia", "Sputum Increased", "Voice Alteration", "Lung Function Decreased", "Respiratory, thoracic and mediastinal disorders Hyperventilation", "Respiratory, thoracic, and mediastinal disorders Dyspnea", "Respiratory system disorder Sinusitis", "Respiration abnormal");
            add("Infections and Infestations", "Upper Respiratory Tract Infection", "Nasopharyngitis", "Sinusitis", "Viral Infection", "Female Genital Mycotic Infections", "Male Genital Mycotic Infections", "Urinary Tract Infection", "Dialysis Access Site Infection", "Tinea infection", "Vaginal Candidiasis", "Oral Moniliasis", "Otitis Media", "Sinusitis (NOS)", "Upper Respiratory Infection (URI)", "Upper Respiratory Tract Inf. NOS", "Gingivitis", "Ingrown toenail");
            add("Investigations", "Blood Glucose Increased", "Blood Bilirubin Increased", "Serum Creatinine Elevated", "Creatinine Increased", "ALT Increased", "AST Increased", "Leukocytes Decreased", "Platelets Decreased", "Increased ALP", "Serum Alkaline Phosphatase Increased", "Elevated Alkaline Phosphatase", "Elevated Bilirubin", "Elevated Creatinine", "Elevated SGOT (AST)", "Elevated SGPT (ALT)", "Elevated Liver Enzymes", "Decreased blood cortisol", "Albumin urine present", "Blood insulin increased", "Elevated Amylase", "Elevated INR", "Elevated Lipase", "QTc prolonged", "Electrocardiogram QT prolongation", "Blood creatine phosphokinase increased (CPK)", "Gamma-glutamyl transferase increased (GGT)", "Increased Gamma-Glutamyltransferase", "Serum creatinine increase", "Laboratory Abnormality", "Weight Increased", "Weight Decreased");
            add("Musculoskeletal and Connective Tissue Disorders", "Back Pain", "Arthralgia", "Myalgia", "Ligament Sprain", "Joint Sprain", "Muscle cramps, tremor", "Musculoskeletal (Bone, Muscle Or Joint) Pain", "Musculoskeletal System Pain - Extremities", "Jaw pain", "Skeletal Pain", "Musculoskeletal Traumatism", "Arthralgia and arthritis", "Musculoskeletal and connective tissue disorders Muscle twitching", "Tenosynovitis");
            add("Vascular Disorders", "Hypertension", "Hot Flush", "Peripheral Edema", "Flushing, heat sensation", "Vasomotor symptoms", "Procedural Hypotension", "Hypotension, postural", "Supine hypertension");
            add("Cardiac Disorders", "Increased Heart Rate", "Torsade de Pointes", "Ventricular fibrillation", "Ventricular arrhythmias", "AV Block First Degree", "Cardiac Failure", "Increased Angina", "Sustained Tachycardia", "Subjective Cardiac Rhythm Disturbance");
            add("Hepatobiliary Disorders", "Cholelithiasis", "Liver test abnormality", "Liver Function Abnormalities", "High Total Bilirubin");
            add("Renal and Urinary Disorders", "Oliguria", "Urine Output Decreased", "Urogenital System Urinary Tract Infection", "Discomfort with urination", "Glycosuria");
            add("Reproductive System and Breast Disorders", "Gynecological disorder", "UROGENITAL SYSTEM Impotence", "Urogenital System Endometrial thickening", "Spontaneous Penile Erection", "Retrograde Ejaculation", "Libido change", "Orgasmic Disturbance", "Decreased sexual desire and arousal", "Uterine Pain", "Breast changes/tenderness/pain", "Vulvovaginal Dryness", "Vulvovaginal Pruritus", "Vulvovaginitis");
            add("Metabolism and Nutrition Disorders", "Hunger", "Hypomagnesaemia", "Hypovolemia", "Hypoglycemia in T2DM", "Fat-soluble vitamin deficiency (A, D, E)", "Central obesity", "Metabolism and nutrition disorders Appetite decreased", "Metabolism and nutrition disorders Hyponatremia");
            add("Endocrine Disorders", "Buffalo Hump", "Cushingoid appearance", "Hormone Level Altered");
            add("Blood and Lymphatic System Disorders", "Postoperative Anemia", "Hematocrit / hemoglobin Increased", "Any noncerebral bleeding", "Any noncerebral bleeding (nonmajor)", "Fatal bleeding", "Major noncerebral", "Major noncerebral or cerebral bleeding", "Intracranial hemorrhage", "Hemorrhagic stroke");
            add("Injury, Poisoning and Procedural Complications", "Delayed Recovery From Anesthesia", "Infused Vein Complication", "Seroma", "Wound Complication", "Head Injury", "Injury", "Laceration (head)", "Post-procedural Hemorrhage", "Post Procedural Hematoma", "nerve injury", "hematoma/bruising", "Injury, poisoning, and procedural complications Road traffic accident", "Injury, poisoning and procedural complications Fall");
            add("Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)", "Large intestine polyp");
            add("Ear and Labyrinth Disorders", "Hypoacusis");

            return map;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates exact bad names supplied by the visualization-quality plan.
        /// </summary>
        /// <returns>Normalized exact bad-name set.</returns>
        private static HashSet<string> createBadNameSet()
        {
            #region implementation

            return new HashSet<string>(
                new[]
                {
                    "< 1,000/mm",
                    "< 25,000/mm",
                    "< 500/mm",
                    "< 70 g/L",
                    "> 1.4 to 1.8 x ULN",
                    "> 1.5 to 2 x ULN",
                    "> 1.5 to 3 x ULN",
                    "> 1.9 to 3.4 x ULN",
                    "> 10 x ULN",
                    "> 13.56 mmol/L > 1200 mg/dL",
                    "> 2 to 5 x ULN",
                    "> 27.75 mmol/L > 500 mg/dL",
                    "> 3 to 5 x ULN",
                    "> 3.4 x ULN",
                    "> 4.9 mmol/L > 190 mg/dL",
                    "> 5 x ULN",
                    "> 6.20 to 7.77 mmol/L 240 mg/dL to 300 mg/dL",
                    "> 7.77 mmol/L > 300 mg/dL",
                    ">5 x ULN",
                    "1,000 to 1,499/mm",
                    "1,500 to 1,999/mm",
                    "13.89 mmol/L to 27.75 mmol/L 251 mg/dL to 500 mg/dL",
                    "25,000 to 49,999/mm",
                    "4.13 mmol/L to 4.9 mmol/L 160 mg/dL to 190 mg/dL",
                    "5.65 mmol/L to 8.48 mmol/L 500 mg/dL to 750 mg/dL",
                    "50,000 to 99,999/mm",
                    "6.95 mmol/L to 13.88 mmol/L 161 mg/dL to 250 mg/dL",
                    "70 g/L to 89 g/L",
                    "8.49 mmol/L to 13.56 mmol/L 751 mg/dL to 1,200 mg/dL",
                    "90 g/L to 99 g/L"
                }.Select(normalizeLookupKey),
                StringComparer.Ordinal);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates exact structural names to exclude before grouping.
        /// </summary>
        /// <returns>Normalized structural name set.</returns>
        private static HashSet<string> createStructuralNameSet()
        {
            #region implementation

            return new HashSet<string>(
                new[]
                {
                    "% Overall",
                    "% Severe",
                    "All",
                    "All adverse reactions",
                    "All EPS events",
                    "All EPS events, excluding Akathisia/Restlessness",
                    "At least 1 AE",
                    "At least one treatment-emergent event",
                    "Average exposure duration (days)",
                    "Cardiovascular System",
                    "Composite of first event of CV death, non-fatal myocardial infarction (MI), or non-fatal stroke (MACE)",
                    "CRNM",
                    "CV death*",
                    "Dictionary-derived Term",
                    "Digestive",
                    "Discontinuation at any time",
                    "Fatal",
                    "Hemic and Lymphatic System",
                    "Hemic and Lymphatic Systems",
                    "Major",
                    "Major leg amputation",
                    "Major + CRNM",
                    "Median, months",
                    "Median exposure (weeks)",
                    "Men only",
                    "Minor",
                    "Neurological",
                    "Non-fatal MI*",
                    "Non-fatal stroke*",
                    "Other",
                    "Overall",
                    "Patients",
                    "PATIENTS WITH AT LEAST ONE AR",
                    "Permanent discontinuation",
                    "Preferred Term",
                    "Rate (episodes/patient-year)",
                    "Serious adverse reactions",
                    "Subjects with at least one adverse reaction, Number (%) of Subjects",
                    "TIMI Major",
                    "TIMI Major or Minor",
                    "TIMI Major or Minor or Requiring medical attention",
                    "Total",
                    "Total # of reports",
                    "Total number of AEs",
                    "Weight gain/loss",
                    "Women only"
                }.Select(normalizeLookupKey),
                StringComparer.Ordinal);

            #endregion
        }

        #endregion Dictionary Construction
    }

    /**************************************************************/
    /// <summary>
    /// Result from Stage 5 AE MedDRA term standardization.
    /// </summary>
    /// <remarks>
    /// <see cref="IsExcluded"/> controls whether the source row can enter comparator
    /// grouping. <see cref="Flags"/> carries audit tokens that are appended to
    /// <see cref="LabelView.FlattenedAdverseEventTable.CalculationFlags"/>.
    /// </remarks>
    /// <param name="IsExcluded">Whether the row should be dropped before grouping.</param>
    /// <param name="Flags">Audit flags emitted by standardization.</param>
    /// <seealso cref="AeMeddraTermStandardizer"/>
    internal sealed record AeMeddraStandardizationResult(bool IsExcluded, IReadOnlyList<string> Flags);
}
