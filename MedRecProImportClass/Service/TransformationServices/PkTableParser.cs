using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for pharmacokinetic (PK) tables in Stage 3 of the SPL Table Normalization pipeline.
    /// PK tables have columns as parameters (Cmax, AUC, t½) and rows as dose regimens.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Header columns = PK parameters with optional units: "Cmax (mcg/mL)", "AUC (mcg·h/mL)"
    /// - Column 0 = dose regimen label (e.g., "50 mg oral (once daily x 7 days)")
    /// - Data cells typically use value(CV%) format
    ///
    /// ## Unpivot Pattern
    /// One observation per (row, paramColumn): a 5-parameter table with 3 dose rows
    /// produces ~15 observations.
    ///
    /// ## PrimaryValueType
    /// Defaults to "Mean" when ValueParser returns "Numeric" (PK values are means).
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ValueParser"/>
    public class PkTableParser : BaseTableParser
    {
        // Pattern for extracting parameter name and unit from header: "Cmax (mcg/mL)"
        private static readonly Regex _paramUnitPattern = new(
            @"^(.+?)\s*\((.+?)\)\s*$",
            RegexOptions.Compiled);

        // Pattern for "x 7 days", "x 14 days", "x 4 weeks" — multiplier schedules
        private static readonly Regex _durationMultiplierPattern = new(
            @"x\s*(\d+)\s*(days?|weeks?|months?|hours?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for "for 14 days", "for 4 weeks" — duration phrases
        private static readonly Regex _durationForPattern = new(
            @"for\s+(\d+)\s*(days?|weeks?|months?|hours?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for single-dose regimens
        private static readonly Regex _singleDosePattern = new(
            @"\b(single)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Known pure time-unit strings. When a column header's unit matches one of these,
        /// the parameter is a time measurement (e.g., Half-life, Tmax) and its PrimaryValue
        /// should override the row-derived Time/TimeUnit.
        /// </summary>
        /// <remarks>
        /// Composite units like "mcg·h/mL" are NOT matched — only pure time units.
        /// </remarks>
        private static readonly HashSet<string> _timeUnitStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            "hours", "hrs", "hr", "h",
            "minutes", "min",
            "seconds", "sec",
            "days", "weeks", "months"
        };

        /**************************************************************/
        /// <summary>
        /// Sample size column names. When a column header matches one of these exactly,
        /// the column contains sample sizes (counts), not PK parameter measurements.
        /// </summary>
        private static readonly HashSet<string> _sampleSizeHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "n", "N", "n=", "sample size"
        };

        /**************************************************************/
        /// <summary>
        /// Column 0 header keywords indicating a population descriptor rather than dose regimen.
        /// When column 0 header contains one of these, row labels are treated as Population
        /// instead of DoseRegimen.
        /// </summary>
        private static readonly HashSet<string> _populationHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "age group", "age", "population", "patient group", "subgroup", "cohort",
            "volunteers", "subjects"
        };

        /**************************************************************/
        /// <summary>
        /// Column 1 header keywords indicating a dedicated dose/route column (not a PK parameter).
        /// When column 1 header matches, the table uses a two-column context layout:
        /// col 0 = category/subtype, col 1 = dose regimen, cols 2+ = PK parameters.
        /// </summary>
        private static readonly HashSet<string> _doseColumnHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dose/Route", "Dose / Route", "Dose", "Regimen", "Dose Regimen", "Dosage", "Route"
        };

        /**************************************************************/
        /// <summary>
        /// R2 — Column-header patterns for non-PK context columns that should be
        /// suppressed by <see cref="extractParameterDefinitions"/>. Matching columns
        /// do NOT emit <c>ParameterName</c> observations — they either carry label
        /// content handled elsewhere (co-administered drug name, subject group) or
        /// carry values already surfaced on a sibling column (Dose of X, Number of
        /// Subjects).
        /// </summary>
        /// <remarks>
        /// Derived from the TID 571 / 2069 audit (2026-04-21). Pre-R2, columns like
        /// "Co-administered Drug" / "Dose of Azithromycin" / "Subject Group" / "n"
        /// emitted spurious <c>ParameterName="&lt;header&gt;"</c> text observations
        /// that polluted the PK corpus (~6,000–8,000 rows). Bare "n" / "N" is
        /// retained by <see cref="_sampleSizeHeaders"/> as a Count-typed column
        /// (legitimate sample-size metric), so it is NOT in this suppression set.
        /// </remarks>
        /// <seealso cref="_sampleSizeHeaders"/>
        /// <seealso cref="_doseColumnHeaders"/>
        /// <seealso cref="isContextColumnHeader"/>
        private static readonly Regex[] _contextColumnHeaderPatterns = new[]
        {
            // "Co-administered Drug", "Coadministered Drug" (both hyphenations)
            new Regex(@"^\s*Co-?administered\s+Drug\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Dose of Azithromycin", "Dose of Co-administered Drug", "Dose of CYP3A4"
            // Uses uppercase-first token to avoid matching "Dose" alone (which is a
            // legitimate dose column handled by _doseColumnHeaders).
            new Regex(@"^\s*Dose\s+of\s+[A-Z][\w\-\s/,]{1,60}\s*$",
                RegexOptions.Compiled),
            // "Subject Group", "Patient Group", "Treatment Group"
            new Regex(@"^\s*(?:Subject|Patient|Treatment|Study)\s+Group\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Number of Subjects", "Number of Patients"
            new Regex(@"^\s*Number\s+of\s+(?:Subjects?|Patients?|Volunteers?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Condition" / "Conditions" / "Treatment" / "Treatments" — generic context
            // labels that carry population/timepoint text, not PK values.
            new Regex(@"^\s*(?:Condition|Conditions|Treatment|Treatments)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Formulation" / "Route of Administration" — context columns on some
            // bioequivalence / formulation-comparison PK tables.
            new Regex(@"^\s*(?:Formulation|Formulations)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"^\s*Route\s+of\s+Administration\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        /**************************************************************/
        /// <summary>
        /// R2 — Returns true when the given column-header text matches one of the
        /// non-PK context column patterns defined by
        /// <see cref="_contextColumnHeaderPatterns"/>. Used by
        /// <see cref="extractParameterDefinitions"/> to skip emission of paramDefs
        /// (and therefore observations) for columns that don't carry PK values.
        /// </summary>
        /// <param name="headerText">Trimmed header cell text.</param>
        /// <returns>True when the text matches a known non-PK context column.</returns>
        /// <seealso cref="_contextColumnHeaderPatterns"/>
        internal static bool isContextColumnHeader(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            foreach (var rx in _contextColumnHeaderPatterns)
            {
                if (rx.IsMatch(headerText))
                    return true;
            }
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R3 — Pattern matching the "divider shell" used in SPL tables to separate
        /// dosing regimens or study phases: text wrapped in one-or-more leading/
        /// trailing asterisks (<c>**Single dose**</c>), optionally ending with a
        /// colon (<c>**500 mg tablet single dose:**</c>), or a bare phrase ending in
        /// a colon (<c>Single dose:</c>). The inner text is captured in group 1.
        /// </summary>
        /// <remarks>
        /// This intentionally accepts a trailing colon so phrases like
        /// "effects of gender and age:" — which TID 2069 uses as a section title
        /// inside a data row — are recognized as dividers.
        /// </remarks>
        private static readonly Regex _sectionDividerShellPattern = new(
            @"^\s*\*+\s*(.+?)\s*[:：]?\s*\*+\s*$" +
            @"|^\s*(.+?)\s*[:：]\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// R3 — Qualifier detection for section-divider text. Returns a canonical
        /// qualifier token (<c>single_dose</c>, <c>multiple_dose</c>,
        /// <c>steady_state</c>, <c>fasted</c>, <c>fed</c>) when the divider text
        /// contains a recognized PK condition phrase, otherwise null.
        /// </summary>
        /// <remarks>
        /// Used by <see cref="detectSectionDivider"/> so subsequent data rows can
        /// carry the sticky qualifier in <c>ParameterSubtype</c>.
        /// </remarks>
        private static readonly (Regex pattern, string qualifier)[] _sectionDividerQualifiers = new[]
        {
            (new Regex(@"\bsingle[\s-]?dose\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "single_dose"),
            (new Regex(@"\bmultiple[\s-]?dose\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "multiple_dose"),
            (new Regex(@"\bsteady[\s-]?state\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "steady_state"),
            (new Regex(@"\bfasted\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "fasted"),
            (new Regex(@"\bfed\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "fed"),
        };

        /**************************************************************/
        /// <summary>
        /// R3 — Outcome of <see cref="detectSectionDivider"/>. When the scanned row
        /// qualifies as a section divider, carries the detected qualifier string
        /// (stored sticky by the parse loop) and optional embedded dose regimen
        /// string (extracted from phrases like
        /// <c>"500 mg oral tablet single dose, effects of gender and age:"</c>).
        /// </summary>
        internal readonly struct SectionDividerResult
        {
            /// <summary>True when the row is a section divider (suppress the row's observations).</summary>
            public bool IsDivider { get; init; }
            /// <summary>Canonical qualifier token (single_dose / multiple_dose / steady_state / fasted / fed), or null.</summary>
            public string? StickyQualifier { get; init; }
            /// <summary>Embedded dose regimen extracted from the divider text, or null.</summary>
            public string? StickyDoseRegimen { get; init; }
            /// <summary>Raw divider text (inner content, shell stripped) for audit/logging.</summary>
            public string? DividerText { get; init; }

            /// <summary>Default non-divider result.</summary>
            public static readonly SectionDividerResult None = new() { IsDivider = false };
        }

        /**************************************************************/
        /// <summary>
        /// R3 — Detects whether a data row is a section divider — a row with a
        /// single non-empty cell that wraps its text in asterisks or ends in a
        /// colon, and whose text carries a recognized PK qualifier phrase.
        /// </summary>
        /// <remarks>
        /// Examples detected:
        /// <list type="bullet">
        /// <item><c>**Single dose**</c> → qualifier="single_dose"</item>
        /// <item><c>**Multiple dose**</c> → qualifier="multiple_dose"</item>
        /// <item><c>**500 mg oral tablet single dose, effects of gender and age:**</c>
        /// → qualifier="single_dose", dose="500 mg oral tablet"</item>
        /// <item><c>Fasted conditions:</c> → qualifier="fasted"</item>
        /// </list>
        /// A row is NOT a divider when it has multiple non-empty cells
        /// (data row with col 0 label plus PK values).
        /// </remarks>
        /// <param name="row">Candidate data row.</param>
        /// <returns>Detection result with sticky qualifier and dose, or
        /// <see cref="SectionDividerResult.None"/> when the row is not a divider.</returns>
        /// <seealso cref="_sectionDividerShellPattern"/>
        /// <seealso cref="_sectionDividerQualifiers"/>
        internal static SectionDividerResult detectSectionDivider(ReconstructedRow row)
        {
            #region implementation

            if (row?.Cells == null)
                return SectionDividerResult.None;

            // Require exactly one non-empty cell. A typical divider has a single
            // col 0 span; multi-cell rows are data rows.
            ProcessedCell? soleCell = null;
            int nonEmptyCount = 0;
            foreach (var c in row.Cells)
            {
                if (!string.IsNullOrWhiteSpace(c.CleanedText))
                {
                    nonEmptyCount++;
                    soleCell = c;
                    if (nonEmptyCount > 1)
                        return SectionDividerResult.None;
                }
            }
            if (nonEmptyCount != 1 || soleCell == null)
                return SectionDividerResult.None;

            var text = soleCell.CleanedText!.Trim();

            // The cell text must match the divider shell (asterisk-wrapped OR
            // trailing colon). Bare plaintext is treated as a normal row.
            var shellMatch = _sectionDividerShellPattern.Match(text);
            if (!shellMatch.Success)
                return SectionDividerResult.None;

            // Capture group 1 is the asterisk-wrapped form; group 2 is the
            // trailing-colon form. Use whichever matched.
            var inner = shellMatch.Groups[1].Success && shellMatch.Groups[1].Length > 0
                ? shellMatch.Groups[1].Value.Trim()
                : shellMatch.Groups[2].Success
                    ? shellMatch.Groups[2].Value.Trim()
                    : text;

            if (string.IsNullOrWhiteSpace(inner))
                return SectionDividerResult.None;

            // Require at least one qualifier match OR an embedded dose regimen.
            // Otherwise a bare "Summary:" line would be suppressed wrongly.
            string? qualifier = null;
            foreach (var (pattern, qname) in _sectionDividerQualifiers)
            {
                if (pattern.IsMatch(inner))
                {
                    qualifier = qname;
                    break;
                }
            }

            // Extract any embedded dose regimen from the divider text.
            var (dose, doseUnit) = DoseExtractor.Extract(inner);
            string? stickyDose = null;
            if (dose.HasValue)
            {
                // The dose fragment is the substring starting at the first digit.
                var firstDigit = inner.IndexOfAny("0123456789".ToCharArray());
                if (firstDigit >= 0)
                {
                    stickyDose = inner[firstDigit..].TrimEnd('*', ':', ' ', '\t', '，', ',');
                }
                // Guard against regressions: "100 mg" alone is a pure dose — caller
                // handles via classifyRowLabel, not as a section divider.
                _ = doseUnit;
            }

            // Treat as divider only when we got at least a qualifier or a dose.
            // This prevents arbitrary "Title:" rows from being suppressed.
            if (qualifier == null && stickyDose == null)
                return SectionDividerResult.None;

            return new SectionDividerResult
            {
                IsDivider = true,
                StickyQualifier = qualifier,
                StickyDoseRegimen = stickyDose,
                DividerText = inner,
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Regex patterns for ± dispersion type resolution. Scanned against header paths,
        /// footnotes, and caption text to determine what the ± symbol represents.
        /// Ordered by specificity: full phrase before abbreviation.
        /// </summary>
        private static readonly (Regex pattern, string secondaryValueType, string boundType)[] _dispersionKeywords =
        {
            (new Regex(@"\bstandard\s+deviation\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "SD", "SD"),
            (new Regex(@"\bS\.?D\.?\b", RegexOptions.Compiled), "SD", "SD"),
            (new Regex(@"\bstandard\s+error\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "SE", "SE"),
            (new Regex(@"\bS\.?E\.?M?\.?\b", RegexOptions.Compiled), "SE", "SE"),
            (new Regex(@"\bconfidence\s+interval\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CI", "CI"),
            (new Regex(@"\b\d+\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CI", "CI"),
        };

        /**************************************************************/
        /// <summary>
        /// Pattern for extracting all parenthetical groups from compound headers
        /// like "AUC(0-96h)(mcgh/mL)". Used by <see cref="parseCompoundParameterHeader"/>
        /// to separate subtype qualifiers from units.
        /// </summary>
        private static readonly Regex _allParentheticalsPattern = new(
            @"\(([^)]+)\)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Pattern for stripping common PK category prefixes from spanning header
        /// and SocDivider text. Matches "Pharmacokinetic Parameters for/in/of".
        /// </summary>
        private static readonly Regex _pkCategoryPrefixPattern = new(
            @"^Pharmacokinetic\s+Parameters?\s+(?:for|in|of)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Pattern for extracting sample size (n=X) from treatment arm row labels.
        /// Matches "(n=6)", "(n = 18)", "(n=1,234)" with optional whitespace and commas.
        /// </summary>
        private static readonly Regex _armNFromLabelPattern = new(
            @"\(\s*n\s*=\s*(\d[\d,]*)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Column 0 header keywords that signal a transposed PK layout where rows carry
        /// PK metric names and columns carry dose levels. These headers are generic
        /// "parameter"-style labels with no unit or dose qualifier.
        /// </summary>
        /// <seealso cref="detectTransposedPkLayout"/>
        private static readonly HashSet<string> _transposedLayoutCol0Headers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Parameter", "Parameters", "PK Parameter", "PK Parameters",
            "Pharmacokinetic Parameter", "Pharmacokinetic Parameters"
        };

        // _pkMetricRowLabelPattern was retired — PK metric detection in transposed
        // layouts now goes through the shared PkParameterDictionary so long-form
        // English variants ("Maximum Plasma Concentrations") and Unicode-laden
        // forms ("AUC0-∞(mcg⋅hr/mL)") match in addition to short abbreviations.

        /**************************************************************/
        /// <summary>
        /// Dose-like header pattern for detecting transposed PK layouts where column headers
        /// carry dose levels ("0.1 mg/day", "500 mcg", "1 g/kg").
        /// </summary>
        private static readonly Regex _doseHeaderPattern = new(
            @"^\s*\d+(?:\.\d+)?\s*(mg|mcg|µg|μg|g|ng|kg|mL|units?|U|IU)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches pure timepoint row labels — "Day N", "Week N", "N days", "N weeks",
        /// "N hours", "N months", "single dose", "steady state", "predose", clock
        /// ranges ("08:00 to 13:00"), and the short-form "C0", "C24", "C72".
        /// </summary>
        /// <remarks>
        /// Anchored to start of string so free-form prose with an embedded
        /// "Day N" fragment is not misrouted to Timepoint. Used by
        /// <see cref="classifyRowLabel"/>.
        /// </remarks>
        private static readonly Regex _timepointLabelPattern = new(
            @"^\s*(?:"
          + @"(?:Day|Week|Month|Cycle|Visit)\s+\d+"
          + @"|\d+(?:\.\d+)?\s*(?:days?|weeks?|hours?|hrs?|h|months?|minutes?|min)"
          + @"|\d+\s*(?:to|[-–])\s*\d+\s*(?:days?|weeks?|hours?|months?)"
          + @"|single\s+dose|steady[\s-]?state|pre[-\s]?dose|post[-\s]?dose|baseline"
          + @"|\d{1,2}:\d{2}(?:\s*(?:to|[-–])\s*\d{1,2}:\d{2})?"
          + @"|C\d{1,3}(?:h|hr|hrs|hour|hours)?"
          // R1.1: food / fasting state qualifiers — these appear as col 0 labels
          // in food-effect PK tables and belong in Timepoint (or a fasting qualifier)
          // rather than TreatmentArm. "Fed" alone is intentionally excluded to avoid
          // accidentally matching drug-name substrings; the multi-word forms are
          // specific enough.
          + @"|fasted|fasting|fed\s+state|light\s+breakfast"
          + @"|high[\s-]?fat\s+(?:meal|breakfast|lunch|dinner)"
          + @"|moderate[\s-]?fat\s+(?:meal|breakfast|lunch|dinner)"
          + @"|low[\s-]?fat\s+(?:meal|breakfast|lunch|dinner)"
          + @"|standard\s+breakfast|regular\s+breakfast|breakfast"
          + @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Heuristic pattern for "looks like a single drug name" — a capitalized
        /// token (or two tokens separated by a slash / hyphen) with no digits and
        /// no dose unit. Used only as a last-resort routing hint when other
        /// classifications fail. Deliberately conservative to avoid false routing
        /// of descriptive phrases or captions.
        /// </summary>
        /// <remarks>
        /// Rejects strings with digits, parentheses, or trailing verbs. Accepts:
        /// "Atorvastatin", "Trimethoprim/Sulfamethoxazole", "Lopinavir-Ritonavir".
        /// Rejects: "Healthy Subjects", "Adults given 50 mg once daily for 7 days".
        /// </remarks>
        private static readonly Regex _drugNameHeuristicPattern = new(
            @"^[A-Z][a-zA-Z]{2,24}(?:[/\-][A-Z]?[a-zA-Z]{2,24}){0,2}\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// R1.1 — Negative list for the drug-name heuristic. These capitalized
        /// tokens pass the permissive <see cref="_drugNameHeuristicPattern"/>
        /// but are NOT drug names — they are ADME section-divider words,
        /// generic row labels, or schema-keyword echoes. Rejecting them forces
        /// <see cref="classifyRowLabel"/> to return <see cref="RowLabelKind.Unknown"/>
        /// so the pre-R1 fallback (col 0 → DoseRegimen / ParameterSubtype)
        /// applies — never route these to TreatmentArm.
        /// </summary>
        /// <remarks>
        /// Derived from the 2026-04-21 post-R1 audit: the top 50 Arm values
        /// included 11 families of non-drug content (Metabolism, Distribution,
        /// Elimination, Absorption, Parameter, Subject/Subjects, etc.) that
        /// were polluting TreatmentArm. Single-word sex / age-strata / dialysis
        /// terms are handled differently — they route to Population via the
        /// expanded <see cref="PopulationDetector"/> dictionary, which is checked
        /// BEFORE this negative list.
        /// </remarks>
        private static readonly HashSet<string> _nonDrugNegativeList = new(StringComparer.OrdinalIgnoreCase)
        {
            // ADME section dividers (pharmacology process categories)
            "Absorption", "Distribution", "Metabolism", "Elimination",
            "Excretion", "Protein Binding", "Disposition",
            // Generic schema / header echoes
            "Parameter", "Parameters", "Value", "Values",
            "Estimate", "Estimates", "Mean", "Median", "Range",
            "Subject", "Subjects", "Patient", "Patients",
            "Group", "Groups", "Dose", "Doses",
            "Route", "Routes", "Schedule", "Regimen", "Regimens",
            "Formulation", "Formulations", "Comparison", "Comparisons",
            "Treatment", "Treatments", "Condition", "Conditions",
            "Study", "Studies", "Trial", "Trials", "Analyte", "Analytes",
            "Control", "Controls", "Placebo", "Baseline",
            // Compound-header row-1 echoes observed in TID 569 / 2069 shape
            "Single dose", "Multiple dose", "Steady state",
        };

        /**************************************************************/
        /// <summary>
        /// Classification for the leading (col 0) row-label text in a PK data row.
        /// Drives which context column(s) the label populates: Population / TreatmentArm /
        /// DoseRegimen / Timepoint / compound TreatmentArm+DoseRegimen. Returns
        /// <see cref="Unknown"/> when no confident classification can be made — in
        /// that case the existing fallback path (DoseRegimen or ParameterSubtype)
        /// preserves backward compatibility.
        /// </summary>
        private enum RowLabelKind
        {
            /// <summary>No confident classification — caller retains existing fallback behavior.</summary>
            Unknown = 0,
            /// <summary>Label matches the population dictionary / regex (e.g., "Healthy Subjects", "CLCR 50-80 mL/min").</summary>
            Population,
            /// <summary>Label is a pure drug name with no embedded dose (e.g., "Atorvastatin").</summary>
            TreatmentArm,
            /// <summary>Label is a pure dose regimen with no drug-name prefix (e.g., "500 mg oral").</summary>
            DoseRegimen,
            /// <summary>Label is a timepoint / visit descriptor (e.g., "Day 14", "Single Dose", "C72").</summary>
            Timepoint,
            /// <summary>Label is a compound drug + dose string (e.g., "Atorvastatin 10 mg/day for 8 days").</summary>
            DrugPlusDose,
        }

        /**************************************************************/
        /// <summary>
        /// Outcome of <see cref="classifyRowLabel"/> — the chosen <see cref="RowLabelKind"/>
        /// plus the destination-column values the parser should apply to each PK
        /// observation in the row. Values default to null; only the fields that
        /// correspond to the chosen kind are populated.
        /// </summary>
        private readonly struct RowLabelClassification
        {
            public RowLabelKind Kind { get; init; }
            public string? Population { get; init; }
            public string? TreatmentArm { get; init; }
            public string? DoseRegimen { get; init; }
            public string? Timepoint { get; init; }
            public double? Time { get; init; }
            public string? TimeUnit { get; init; }
            public bool MatchedPopulationViaRegex { get; init; }

            public static readonly RowLabelClassification Unknown = new() { Kind = RowLabelKind.Unknown };
        }

        /**************************************************************/
        /// <summary>
        /// Classifies the col 0 row-label text into one of the <see cref="RowLabelKind"/>
        /// categories and returns the resolved destination-column values. Priority order:
        /// 1. Pure dose (no text prefix)      → DoseRegimen
        /// 2. Timepoint pattern match         → Timepoint + Time + TimeUnit
        /// 3. Population dictionary / regex   → Population
        /// 4. Drug + dose compound            → TreatmentArm + DoseRegimen
        /// 5. Drug-name heuristic (bare word) → TreatmentArm
        /// 6. Otherwise                       → Unknown (caller falls back)
        /// </summary>
        /// <remarks>
        /// Only returns non-Unknown when the classification is confident. This
        /// preserves backward compatibility: rows that don't match any rule
        /// continue to receive the pre-R1 behavior (single-column → DoseRegimen,
        /// two-column → ParameterSubtype). Population tests already fire in both
        /// paths; this method consolidates and extends the routing so drug-name
        /// and timepoint-only labels also find their correct column.
        /// </remarks>
        /// <param name="col0Text">The trimmed col 0 cell text.</param>
        /// <returns>Classification with resolved values, or Unknown when no rule fires.</returns>
        private static RowLabelClassification classifyRowLabel(string? col0Text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(col0Text))
                return RowLabelClassification.Unknown;

            var text = col0Text.Trim();

            // 1. Pure dose regimen — col 0 is just a dose like "500 mg" or "1 g/kg oral"
            //    with no text prefix before the number. DoseExtractor returns a dose
            //    and the text BEFORE the first digit is empty / whitespace.
            var (dose, doseUnit) = DoseExtractor.Extract(text);
            if (dose.HasValue)
            {
                var firstDigit = text.IndexOfAny("0123456789".ToCharArray());
                var prefix = firstDigit > 0 ? text[..firstDigit].Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    return new RowLabelClassification
                    {
                        Kind = RowLabelKind.DoseRegimen,
                        DoseRegimen = text,
                    };
                }

                // 4. Drug + dose compound — prefix is a non-empty word that passes the
                //    conservative drug-name heuristic AND is not a known population.
                //    Split into TreatmentArm (prefix) + DoseRegimen (everything from
                //    the first digit onward).
                var isPop = PopulationDetector.TryMatchLabel(prefix, out _);
                var isPkTerm = PkParameterDictionary.IsPkParameter(prefix);
                if (!isPop && !isPkTerm && _drugNameHeuristicPattern.IsMatch(prefix))
                {
                    return new RowLabelClassification
                    {
                        Kind = RowLabelKind.DrugPlusDose,
                        TreatmentArm = prefix,
                        DoseRegimen = text[firstDigit..].Trim(),
                    };
                }
                // Prefix didn't pass drug heuristic — fall through so existing
                // behavior (col0Text → DoseRegimen or ParameterSubtype) applies.
            }

            // 2. Timepoint — "Day 14", "5 days", "Single Dose", "08:00 to 13:00", "C72"
            if (_timepointLabelPattern.IsMatch(text))
            {
                var (time, timeUnit, timepoint) = extractDuration(text);
                return new RowLabelClassification
                {
                    Kind = RowLabelKind.Timepoint,
                    Timepoint = timepoint ?? text,
                    Time = time,
                    TimeUnit = timeUnit,
                };
            }

            // 3. Population — TryMatchLabel runs both the strict dictionary and the
            //    regex second-pass (age ranges, renal bands, trimesters).
            if (PopulationDetector.TryMatchLabel(text, out var popCanonical, out var matchedViaRegex))
            {
                return new RowLabelClassification
                {
                    Kind = RowLabelKind.Population,
                    Population = popCanonical,
                    MatchedPopulationViaRegex = matchedViaRegex,
                };
            }

            // 5. Drug-name heuristic — last-resort bare drug token. Conservative
            //    so descriptive phrases fall through to Unknown. R1.1: reject
            //    ADME section dividers and generic schema-keyword echoes via
            //    the negative list so these don't pollute TreatmentArm.
            if (!PkParameterDictionary.IsPkParameter(text)
                && !_nonDrugNegativeList.Contains(text)
                && _drugNameHeuristicPattern.IsMatch(text))
            {
                return new RowLabelClassification
                {
                    Kind = RowLabelKind.TreatmentArm,
                    TreatmentArm = text,
                };
            }

            return RowLabelClassification.Unknown;

            #endregion
        }

        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports PK table category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.PK;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only PK parser for this category.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for PK-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a PK table: header columns are parameters, data rows are dose regimens.
        /// Each data cell becomes one observation with DoseRegimen from row label.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);
            var captionHint = detectCaptionValueHint(table.Caption);

            // Detect compound header layout (spanning header + embedded sub-headers + SocDividers)
            if (detectCompoundHeaderLayout(table))
                return parseCompoundLayout(table, observations, population, captionHint);

            // Detect whether column 1 is a dedicated dose column (two-column context layout)
            var (doseColumnIndex, paramStartColumn) = detectDoseColumn(table);
            var hasDoseColumn = doseColumnIndex >= 0;

            // Extract parameter definitions from header
            var paramDefs = extractParameterDefinitions(table, paramStartColumn);
            if (paramDefs.Count == 0)
                return observations;

            // Detect whether column 0 is a population descriptor (single-column layout only)
            var col0IsPopulation = !hasDoseColumn && isColumn0Population(table);

            // Two-column layout state: category header tracking
            string? currentCategory = null;
            if (hasDoseColumn)
            {
                // Col 0 header text is the initial category (e.g., "Healthy Volunteers")
                currentCategory = table.Header?.Columns?.Count > 0
                    ? table.Header.Columns[0].LeafHeaderText?.Trim()
                    : null;
            }

            // R3 — Sticky state carried across data rows within this parse.
            // Updated when a section-divider row fires, applied to subsequent
            // observations whose own columns are empty. Keeps pre-R3 behavior
            // intact when no divider is present (both values stay null).
            string? stickyQualifier = null;
            string? stickyDoseRegimen = null;

            // Iterate data rows
            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                // R3 — Detect section-divider rows BEFORE any other processing.
                // These rows carry a single cell with text like "**Single dose**"
                // that both (a) carries a qualifier state to apply to following
                // rows and (b) should NOT emit observations of its own.
                var divider = detectSectionDivider(row);
                if (divider.IsDivider)
                {
                    if (divider.StickyQualifier != null)
                        stickyQualifier = divider.StickyQualifier;
                    if (divider.StickyDoseRegimen != null)
                        stickyDoseRegimen = divider.StickyDoseRegimen;
                    continue;
                }

                // Column 0 text (always read for both layouts)
                var col0Cell = getCellAtColumn(row, 0);
                var col0Text = col0Cell?.CleanedText?.Trim();

                // Skip empty label rows
                if (string.IsNullOrWhiteSpace(col0Text))
                    continue;

                // Classify col 0 row label once per row — feeds both layout paths
                // and both paths preserve their existing fallback when the classifier
                // returns Unknown. Kind == Population is honored before the legacy
                // "col0 → ParameterSubtype" placement (two-column) and before the
                // "col0 → DoseRegimen" placement (single-column).
                var rowLabel = classifyRowLabel(col0Text);

                // --- Two-column context layout ---
                if (hasDoseColumn)
                {
                    // Check for sub-header row (category divider echoing column headers)
                    var categoryFromSubHeader = detectSubHeaderRow(row, paramDefs, doseColumnIndex);
                    if (categoryFromSubHeader != null)
                    {
                        currentCategory = categoryFromSubHeader;
                        continue;
                    }

                    // Data row: col 0 = subtype, col doseColumnIndex = dose regimen
                    var doseCell = getCellAtColumn(row, doseColumnIndex);
                    var doseRegimen = doseCell?.CleanedText?.Trim();
                    var (time, timeUnit, timepoint) = extractDuration(doseRegimen);

                    parseRowSafe(table, row, observations, (r, obs) =>
                    {
                        foreach (var param in paramDefs)
                        {
                            var cell = getCellAtColumn(r, param.columnIndex);
                            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                                continue;

                            var o = createBaseObservation(table, r, cell, TableCategory.PK);
                            o.ParameterName = param.name;
                            o.ParameterCategory = currentCategory;

                            // R1 — Apply row-label classification. Each Kind routes col 0
                            // to its contract-assigned column(s); Unknown falls through to
                            // the pre-R1 behavior (col 0 → ParameterSubtype) for backward
                            // compatibility. Population routing via TryMatchLabel dictionary
                            // or regex keeps firing under Kind == Population.
                            switch (rowLabel.Kind)
                            {
                                case RowLabelKind.Population:
                                    o.Population = rowLabel.Population;
                                    o.ValidationFlags = appendFlag(
                                        o.ValidationFlags,
                                        rowLabel.MatchedPopulationViaRegex
                                            ? "PK_COL0_POP_ROUTED_REGEX"
                                            : "PK_COL0_POP_ROUTED");
                                    break;

                                case RowLabelKind.TreatmentArm:
                                    o.TreatmentArm = rowLabel.TreatmentArm;
                                    o.Population = population;
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_ARM_ROUTED");
                                    break;

                                case RowLabelKind.DrugPlusDose:
                                    o.TreatmentArm = rowLabel.TreatmentArm;
                                    o.Population = population;
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_ARM_DOSE_SPLIT");
                                    // Dose regimen from the col 0 split — only used when the
                                    // dedicated dose column (doseColumnIndex) cell is blank.
                                    // Otherwise the explicit dose column wins per existing
                                    // two-column contract.
                                    if (string.IsNullOrWhiteSpace(doseRegimen))
                                    {
                                        doseRegimen = rowLabel.DoseRegimen;
                                        (time, timeUnit, timepoint) = extractDuration(doseRegimen);
                                    }
                                    break;

                                case RowLabelKind.Timepoint:
                                    o.Timepoint = rowLabel.Timepoint;
                                    o.Time = rowLabel.Time;
                                    o.TimeUnit = rowLabel.TimeUnit;
                                    o.Population = population;
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_TIMEPOINT_ROUTED");
                                    break;

                                case RowLabelKind.DoseRegimen:
                                    // Rare in two-column (dedicated dose col usually present)
                                    // but handle for defensive completeness.
                                    if (string.IsNullOrWhiteSpace(doseRegimen))
                                    {
                                        doseRegimen = rowLabel.DoseRegimen;
                                        (time, timeUnit, timepoint) = extractDuration(doseRegimen);
                                    }
                                    o.Population = population;
                                    break;

                                case RowLabelKind.Unknown:
                                default:
                                    // Pre-R1 fallback: park col 0 in ParameterSubtype. Phase 2
                                    // enforcement (Stage 3.25 applyPkCanonicalization) will
                                    // further route non-qualifier content out of Subtype.
                                    o.ParameterSubtype = col0Text;
                                    o.Population = population;
                                    break;
                            }

                            o.DoseRegimen = doseRegimen;
                            var (pkDose1, pkDoseUnit1) = DoseExtractor.Extract(doseRegimen);
                            o.Dose = pkDose1;
                            o.DoseUnit = pkDoseUnit1;
                            if (o.Timepoint == null)
                            {
                                o.Timepoint = timepoint;
                                o.Time = time;
                                o.TimeUnit = timeUnit;
                            }
                            o.Unit = param.unit;

                            parseAndApplyPkValue(table, o, cell, param, captionHint);

                            obs.Add(o);
                        }
                    });
                }
                // --- Standard single-column layout (existing behavior, R1-enhanced) ---
                else
                {
                    // Baseline behavior: when col0IsPopulation (header keyword), col 0 → Population
                    // and DoseRegimen is null. Else col 0 → DoseRegimen. R1 layers row-label
                    // classification on top so "Atorvastatin 10 mg/day" splits to TreatmentArm
                    // + DoseRegimen, "Healthy Subjects" routes to Population even when the
                    // header didn't flag col 0 as Population, and "Day 14" routes to Timepoint.
                    string? doseRegimen = null;
                    string? rowTreatmentArm = null;
                    string? rowPopulation = population;
                    string? rowTimepointOverride = null;
                    double? rowTimeOverride = null;
                    string? rowTimeUnitOverride = null;

                    if (col0IsPopulation)
                    {
                        rowPopulation = col0Text;
                    }
                    else
                    {
                        switch (rowLabel.Kind)
                        {
                            case RowLabelKind.Population:
                                rowPopulation = rowLabel.Population;
                                break;

                            case RowLabelKind.TreatmentArm:
                                rowTreatmentArm = rowLabel.TreatmentArm;
                                break;

                            case RowLabelKind.DrugPlusDose:
                                rowTreatmentArm = rowLabel.TreatmentArm;
                                doseRegimen = rowLabel.DoseRegimen;
                                break;

                            case RowLabelKind.Timepoint:
                                rowTimepointOverride = rowLabel.Timepoint;
                                rowTimeOverride = rowLabel.Time;
                                rowTimeUnitOverride = rowLabel.TimeUnit;
                                break;

                            case RowLabelKind.DoseRegimen:
                                doseRegimen = rowLabel.DoseRegimen;
                                break;

                            case RowLabelKind.Unknown:
                            default:
                                // Pre-R1 fallback: col 0 → DoseRegimen (preserves all
                                // existing single-column behavior).
                                doseRegimen = col0Text;
                                break;
                        }
                    }

                    // R3 — Apply sticky dose from the most recent section divider
                    // when this row's classifier did not yield its own DoseRegimen.
                    // Preserves pre-R3 behavior when no divider has fired (sticky
                    // values are null → effectiveDose falls through to doseRegimen).
                    var effectiveDose = !string.IsNullOrWhiteSpace(doseRegimen)
                        ? doseRegimen
                        : stickyDoseRegimen;

                    var (time, timeUnit, timepoint) = extractDuration(effectiveDose);

                    // Capture sticky-state snapshots for the closure below so the
                    // lambda binds values rather than mutable outer locals.
                    var rowStickyQualifier = stickyQualifier;
                    var rowStickyDose = stickyDoseRegimen;

                    parseRowSafe(table, row, observations, (r, obs) =>
                    {
                        foreach (var param in paramDefs)
                        {
                            var cell = getCellAtColumn(r, param.columnIndex);
                            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                                continue;

                            var o = createBaseObservation(table, r, cell, TableCategory.PK);
                            o.ParameterName = param.name;
                            o.DoseRegimen = effectiveDose;
                            var (pkDose2, pkDoseUnit2) = DoseExtractor.Extract(effectiveDose);
                            o.Dose = pkDose2;
                            o.DoseUnit = pkDoseUnit2;
                            o.Population = rowPopulation;
                            o.TreatmentArm = rowTreatmentArm;
                            // Timepoint from the row-label classifier wins over the
                            // dose-regimen-derived duration; else fall back to the
                            // regimen-derived value.
                            o.Timepoint = rowTimepointOverride ?? timepoint;
                            o.Time = rowTimeOverride ?? time;
                            o.TimeUnit = rowTimeUnitOverride ?? timeUnit;
                            o.Unit = param.unit;

                            // R3 — Apply sticky qualifier to ParameterSubtype when
                            // no subtype has been set by upstream routing. The
                            // qualifier is a contract-allowed PK condition token
                            // (single_dose / multiple_dose / steady_state / fasted / fed)
                            // per column-contracts.md.
                            if (rowStickyQualifier != null && string.IsNullOrWhiteSpace(o.ParameterSubtype))
                            {
                                o.ParameterSubtype = rowStickyQualifier;
                                o.ValidationFlags = appendFlag(
                                    o.ValidationFlags, "PK_SECTION_QUALIFIER_APPLIED");
                            }

                            // Attribution flags so downstream can audit which R1 rule fired
                            switch (rowLabel.Kind)
                            {
                                case RowLabelKind.Population:
                                    o.ValidationFlags = appendFlag(
                                        o.ValidationFlags,
                                        rowLabel.MatchedPopulationViaRegex
                                            ? "PK_COL0_POP_ROUTED_REGEX"
                                            : "PK_COL0_POP_ROUTED");
                                    break;
                                case RowLabelKind.TreatmentArm:
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_ARM_ROUTED");
                                    break;
                                case RowLabelKind.DrugPlusDose:
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_ARM_DOSE_SPLIT");
                                    break;
                                case RowLabelKind.Timepoint:
                                    o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_COL0_TIMEPOINT_ROUTED");
                                    break;
                            }

                            if (rowStickyDose != null
                                && !string.IsNullOrWhiteSpace(o.DoseRegimen)
                                && ReferenceEquals(o.DoseRegimen, rowStickyDose))
                            {
                                o.ValidationFlags = appendFlag(
                                    o.ValidationFlags, "PK_SECTION_DOSE_APPLIED");
                            }

                            parseAndApplyPkValue(table, o, cell, param, captionHint);

                            obs.Add(o);
                        }
                    });
                }
            }

            // Sanity check: transposed layout where rows are PK metrics and columns
            // are doses. Only activates on the standard single-column path when no
            // dose column and no population column 0 are present.
            if (!hasDoseColumn && !col0IsPopulation && detectTransposedPkLayout(table))
            {
                applyTransposedPkLayoutSwap(observations);
            }

            // Post-parse: refine generic "CI" bound type from table context
            // (footer rows, spanning data rows that contain "N% CI" text)
            if (observations.Any(o => o.BoundType == "CI"))
            {
                var ciLevel = detectCILevelFromTableText(table);
                if (ciLevel != null)
                {
                    foreach (var o in observations.Where(o => o.BoundType == "CI"))
                        o.BoundType = ciLevel;
                }
            }

            // Sanity check: populate ArmN from caption "(N=X)" when parser did not set it
            applyCaptionArmNFallback(table, observations);

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Shared value parsing logic for PK cells. Handles ValueParser dispatch, caption hint
        /// application, ± dispersion type resolution, PK fallback (Numeric → Mean), and time
        /// measurement override. Used by both single-column and two-column layout paths.
        /// </summary>
        /// <param name="table">Table for footnote/header context in dispersion resolution.</param>
        /// <param name="o">Target observation to populate.</param>
        /// <param name="cell">Source cell with text to parse.</param>
        /// <param name="param">Parameter definition (column index, name, unit, flags).</param>
        /// <param name="captionHint">Caption-derived value type hint.</param>
        private static void parseAndApplyPkValue(
            ReconstructedTable table,
            ParsedObservation o,
            ProcessedCell cell,
            (int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize) param,
            CaptionValueHint captionHint)
        {
            #region implementation

            // Parse value — PK cells use value(CV%), value (±SD) (n=X), plain numbers, etc.
            var parsed = ValueParser.Parse(cell.CleanedText);

            // Sample size column: override Numeric → Count
            if (param.isSampleSize && parsed.PrimaryValueType == "Numeric")
            {
                parsed.PrimaryValueType = "Count";
                parsed.ParseConfidence *= ParsedValue.ConfidenceAdjustment.PositionalSampleSize;
            }

            // Caption-based type inference (e.g., "Mean (SD)" reinterprets n_pct → mean_sd)
            if (!captionHint.IsEmpty)
            {
                parsed = applyCaptionHint(parsed, captionHint);
            }

            // PK fallback: bare Numeric → Mean (only if caption didn't already set it)
            // Skip for Count (sample size) — should not be promoted to Mean
            if (parsed.PrimaryValueType == "Numeric")
            {
                parsed.PrimaryValueType = "Mean";
                // Reduce confidence — fallback without caption confirmation
                parsed.ParseConfidence *= ParsedValue.ConfidenceAdjustment.UncaptionedTypePromotion;
            }

            applyParsedValue(o, parsed);

            // Resolve ± dispersion type from context when SecondaryValueType is unresolved
            if (o.SecondaryValue.HasValue && string.IsNullOrEmpty(o.SecondaryValueType)
                && parsed.ParseRule == "value_plusminus_sample")
            {
                var (svt, bt, flag) = resolveDispersionType(table, o, param.columnIndex);
                o.SecondaryValueType = svt;
                o.BoundType = bt;
                if (flag != null)
                    o.ValidationFlags = appendFlag(o.ValidationFlags, flag);
            }

            // Unit from header takes precedence over parsed unit
            if (!string.IsNullOrEmpty(param.unit))
                o.Unit = param.unit;

            // Column-derived time: when the parameter IS a time measurement
            // (e.g., Half-life, Tmax), override Time/TimeUnit with the measured value
            if (param.isTimeMeasure && o.PrimaryValue.HasValue)
            {
                o.Time = o.PrimaryValue;
                o.TimeUnit = normalizeTimeUnit(param.unit ?? "hours");
            }

            #endregion
        }

        // Pattern for detecting CI level in table text: "90% CI", "95% CI"
        private static readonly Regex _ciLevelPattern = new(
            @"(\d+)\s*%\s*CI\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Scans table footer rows, caption, and spanning data rows for CI level indicators
        /// (e.g., "90% CI", "95% CI"). Returns the BoundType string ("90CI", "95CI") or null.
        /// </summary>
        /// <remarks>
        /// Drug interaction PK tables often specify the CI level in a footer or spanning
        /// annotation row rather than in the caption. This method searches all non-header
        /// row text to find the CI specification.
        /// </remarks>
        /// <param name="table">The reconstructed table to scan.</param>
        /// <returns>"90CI", "95CI", or null if no CI level found.</returns>
        private static string? detectCILevelFromTableText(ReconstructedTable table)
        {
            #region implementation

            // Check caption first
            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                var captionMatch = _ciLevelPattern.Match(table.Caption);
                if (captionMatch.Success)
                    return $"{captionMatch.Groups[1].Value}CI";
            }

            // Check footer rows and data body rows for CI level text
            foreach (var row in table.Rows ?? Enumerable.Empty<ReconstructedRow>())
            {
                if (row.Classification != RowClassification.Footer &&
                    row.Classification != RowClassification.DataBody)
                    continue;

                foreach (var cell in row.Cells ?? Enumerable.Empty<ProcessedCell>())
                {
                    if (string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var match = _ciLevelPattern.Match(cell.CleanedText);
                    if (match.Success)
                        return $"{match.Groups[1].Value}CI";
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts parameter name, unit, time-measure flag, and sample-size flag from header columns.
        /// Parses patterns like "Cmax (mcg/mL)" into structured definitions.
        /// </summary>
        /// <example>
        /// <code>
        /// "Cmax (mcg/mL)"      → ("Cmax", "mcg/mL", false, false)
        /// "Half-life (hours)"   → ("Half-life", "hours", true, false)
        /// "n"                   → ("n", null, false, true)
        /// </code>
        /// </example>
        private static List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> extractParameterDefinitions(
            ReconstructedTable table, int paramStartColumn = 1)
        {
            #region implementation

            var defs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>();
            if (table.Header?.Columns == null)
                return defs;

            // Skip non-parameter columns (col 0 = label, optionally col 1 = dose)
            for (int i = paramStartColumn; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // R2 — Suppress non-PK context columns (Co-administered Drug,
                // Dose of X, Subject Group, Number of Subjects, …). Without this
                // filter, such columns emit spurious ParameterName="<header>"
                // text observations polluting the corpus.
                if (isContextColumnHeader(text))
                    continue;

                // Check if column is a sample size column
                var isSampleSize = _sampleSizeHeaders.Contains(text);

                var match = _paramUnitPattern.Match(text);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    var unit = match.Groups[2].Value.Trim();
                    var isTime = _timeUnitStrings.Contains(unit);
                    defs.Add((col.ColumnIndex ?? i, name, unit, isTime, isSampleSize));
                }
                else
                {
                    defs.Add((col.ColumnIndex ?? i, text, null, false, isSampleSize));
                }
            }

            return defs;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether column 0 header text indicates a population descriptor
        /// (e.g., "Age Group (y)", "Population") rather than a dose regimen.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True when column 0 contains population-related keywords.</returns>
        private static bool isColumn0Population(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count == 0)
                return false;

            var col0Text = table.Header.Columns[0].LeafHeaderText?.Trim();
            if (string.IsNullOrWhiteSpace(col0Text))
                return false;

            // Check if the header contains any population keyword
            foreach (var keyword in _populationHeaderKeywords)
            {
                if (col0Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the dosing duration from a dose regimen string.
        /// Recognizes "x N days/weeks/months", "for N days/weeks", and "single" dose patterns.
        /// </summary>
        /// <param name="doseRegimen">Dose regimen text (e.g., "50 mg oral (once daily x 7 days)").</param>
        /// <returns>
        /// Tuple of (time, timeUnit, timepoint):
        /// - time: numeric duration value, or null for single/unrecognized doses
        /// - timeUnit: normalized unit ("days", "weeks", "months", "hours"), or null
        /// - timepoint: human-readable label ("7 days", "single dose"), or null if unrecognized
        /// </returns>
        /// <example>
        /// <code>
        /// extractDuration("50 mg oral (once daily x 7 days)")  → (7, "days", "7 days")
        /// extractDuration("150 mg single oral")                → (null, null, "single dose")
        /// extractDuration("400 mg IV (once weekly x 4 weeks)") → (4, "weeks", "4 weeks")
        /// extractDuration("unknown format")                    → (null, null, null)
        /// </code>
        /// </example>
        internal static (double? time, string? timeUnit, string? timepoint) extractDuration(string? doseRegimen)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(doseRegimen))
                return (null, null, null);

            // Try "x N days/weeks/months/hours" pattern first (most common in PK tables)
            var match = _durationMultiplierPattern.Match(doseRegimen);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = normalizeTimeUnit(match.Groups[2].Value);
                return (value, unit, $"{(int)value} {unit}");
            }

            // Try "for N days/weeks/months" pattern
            match = _durationForPattern.Match(doseRegimen);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = normalizeTimeUnit(match.Groups[2].Value);
                return (value, unit, $"{(int)value} {unit}");
            }

            // Check for single-dose pattern
            if (_singleDosePattern.IsMatch(doseRegimen))
            {
                return (null, null, "single dose");
            }

            return (null, null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes time unit strings to plural lowercase canonical form.
        /// Handles abbreviations (hrs → hours, min → minutes, sec → seconds).
        /// </summary>
        /// <example>
        /// <code>
        /// normalizeTimeUnit("day")    → "days"
        /// normalizeTimeUnit("Week")   → "weeks"
        /// normalizeTimeUnit("months") → "months"
        /// normalizeTimeUnit("hrs")    → "hours"
        /// normalizeTimeUnit("hr")     → "hours"
        /// normalizeTimeUnit("h")      → "hours"
        /// normalizeTimeUnit("min")    → "minutes"
        /// normalizeTimeUnit("sec")    → "seconds"
        /// </code>
        /// </example>
        internal static string normalizeTimeUnit(string unit)
        {
            #region implementation

            var lower = unit.ToLowerInvariant().TrimEnd('s');

            // Map abbreviations to canonical forms
            return lower switch
            {
                "hr" or "h" => "hours",
                "hour" => "hours",
                "min" => "minutes",
                "minute" => "minutes",
                "sec" => "seconds",
                "second" => "seconds",
                "day" => "days",
                "week" => "weeks",
                "month" => "months",
                "year" => "years",
                _ => lower + "s"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether the table has a dedicated dose column (e.g., "Dose/Route") at
        /// column 1, separate from the context/label column at column 0. When detected,
        /// PK parameters start at column 2 instead of column 1.
        /// </summary>
        /// <remarks>
        /// Guard: if column 1 header matches the parameter unit pattern (e.g., "Dose (mg)"),
        /// it is a numeric parameter column, NOT a categorical dose column.
        /// </remarks>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>
        /// Tuple of (doseColumnIndex, paramStartColumn):
        /// - (1, 2) when column 1 is a dose column → parameters start at col 2
        /// - (-1, 1) when no dose column → existing behavior, parameters start at col 1
        /// </returns>
        private static (int doseColumnIndex, int paramStartColumn) detectDoseColumn(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count < 3)
                return (-1, 1);

            var col1Text = table.Header.Columns.Count > 1
                ? table.Header.Columns[1].LeafHeaderText?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(col1Text))
                return (-1, 1);

            // Guard: if col 1 header has a parenthesized unit, it's a parameter (e.g., "Dose (mg)")
            if (_paramUnitPattern.IsMatch(col1Text))
                return (-1, 1);

            // Check if col 1 header matches a dose column keyword
            if (_doseColumnHeaders.Contains(col1Text))
                return (1, 2);

            return (-1, 1);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether a data row is a sub-header row that re-states column headers
        /// (e.g., "Kidney Transplant Patients... | Dose/Route | Tmax(h) | Cmax(mcg/mL) | ...").
        /// These rows serve as category dividers within the table.
        /// </summary>
        /// <remarks>
        /// Detection requires TWO signals to avoid false positives:
        /// 1. The dose column cell echoes the dose column header (contains "dose" or "route")
        /// 2. At least 50% of parameter columns echo their header names
        /// </remarks>
        /// <param name="row">The data row to evaluate.</param>
        /// <param name="paramDefs">Parameter definitions from header.</param>
        /// <param name="doseColumnIndex">Index of the dose column (1 for two-column layout).</param>
        /// <returns>Col 0 text as the category name if this is a sub-header row, null otherwise.</returns>
        private static string? detectSubHeaderRow(
            ReconstructedRow row,
            List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> paramDefs,
            int doseColumnIndex)
        {
            #region implementation

            if (paramDefs.Count == 0)
                return null;

            // Signal 1: dose column cell echoes "dose" or "route"
            var doseCell = getCellAtColumn(row, doseColumnIndex);
            var doseCellText = doseCell?.CleanedText?.Trim();
            if (string.IsNullOrWhiteSpace(doseCellText))
                return null;

            var doseEcho = doseCellText.Contains("dose", StringComparison.OrdinalIgnoreCase) ||
                           doseCellText.Contains("route", StringComparison.OrdinalIgnoreCase);
            if (!doseEcho)
                return null;

            // Signal 2: >= 50% of parameter columns echo their header name
            int echoCount = 0;
            foreach (var param in paramDefs)
            {
                var cell = getCellAtColumn(row, param.columnIndex);
                var cellText = cell?.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(cellText))
                    continue;

                // Match if cell starts with the parameter name (case-insensitive)
                // Handles "Tmax(h)" matching param "Tmax", "Cmax (mcg/mL)" matching "Cmax"
                if (cellText.StartsWith(param.name, StringComparison.OrdinalIgnoreCase))
                    echoCount++;
            }

            if (echoCount < (paramDefs.Count + 1) / 2) // Ceiling division: >= 50%
                return null;

            // Both signals confirmed — return col 0 text as the category name
            var col0Cell = getCellAtColumn(row, 0);
            return col0Cell?.CleanedText?.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves the dispersion type for ± values when <see cref="ParsedObservation.SecondaryValueType"/>
        /// is null after ValueParser and caption hint processing. Checks header path, observation
        /// footnotes, and table-wide footnotes before falling back to SD with validation flag.
        /// </summary>
        /// <remarks>
        /// Resolution priority chain (first match wins):
        /// 1. Column header path text (multi-level headers may contain "Mean ± SD")
        /// 2. Observation's resolved footnote text
        /// 3. Table-wide footnotes (any footnote value)
        /// 4. Default: SD with <c>PLUSMINUS_TYPE_INFERRED:SD</c> flag
        /// </remarks>
        /// <param name="table">Table for footnote access.</param>
        /// <param name="obs">Observation for footnote text access.</param>
        /// <param name="paramColumnIndex">Column index of the parameter for header path lookup.</param>
        /// <returns>Tuple of (secondaryValueType, boundType, validationFlag or null).</returns>
        private static (string secondaryValueType, string boundType, string? flag) resolveDispersionType(
            ReconstructedTable table,
            ParsedObservation obs,
            int paramColumnIndex)
        {
            #region implementation

            // Source 1: Column header path (multi-level headers)
            if (table.Header?.Columns != null)
            {
                var headerCol = table.Header.Columns.FirstOrDefault(c =>
                    c.ColumnIndex == paramColumnIndex);
                if (headerCol?.CombinedHeaderText != null)
                {
                    var resolved = matchDispersionKeywords(headerCol.CombinedHeaderText);
                    if (resolved != null)
                        return (resolved.Value.svt, resolved.Value.bt, null);
                }
            }

            // Source 2: Observation's resolved footnote text
            if (!string.IsNullOrWhiteSpace(obs.FootnoteText))
            {
                var resolved = matchDispersionKeywords(obs.FootnoteText);
                if (resolved != null)
                    return (resolved.Value.svt, resolved.Value.bt, null);
            }

            // Source 3: Table-wide footnotes
            if (table.Footnotes != null)
            {
                foreach (var footnote in table.Footnotes.Values)
                {
                    if (string.IsNullOrWhiteSpace(footnote))
                        continue;
                    var resolved = matchDispersionKeywords(footnote);
                    if (resolved != null)
                        return (resolved.Value.svt, resolved.Value.bt, null);
                }
            }

            // Source 4: Default — SD is the most common ± type in PK tables
            return ("SD", "SD", "PLUSMINUS_TYPE_INFERRED:SD");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans text for dispersion type keywords (SD, SE, CI) and returns the matching type.
        /// </summary>
        /// <param name="text">Text to scan (header path, footnote, caption).</param>
        /// <returns>Tuple of (svt, bt) if matched, null otherwise.</returns>
        private static (string svt, string bt)? matchDispersionKeywords(string text)
        {
            #region implementation

            foreach (var (pattern, svt, bt) in _dispersionKeywords)
            {
                if (pattern.IsMatch(text))
                    return (svt, bt);
            }

            return null;

            #endregion
        }

        #endregion Private Helpers

        #region Compound Header Layout

        /**************************************************************/
        /// <summary>
        /// Parses a compound header layout PK table: spanning header provides ParameterCategory,
        /// embedded data rows serve as sub-headers for parameter definitions, SocDivider rows
        /// reset context and trigger sub-header re-parsing, and column 0 carries TreatmentArm labels.
        /// </summary>
        /// <remarks>
        /// This is the third layout path in the PK parser, alongside single-column and two-column.
        /// It activates only when <see cref="detectCompoundHeaderLayout"/> returns true.
        ///
        /// ## Structure
        /// ```
        /// [Spanning header] → ParameterCategory (all cols identical LeafHeaderText)
        /// [Sub-header row]  → Parameter definitions (Dose | Tmax (h) | Cmax (mcg/mL) | AUC(0-96h)(mcgh/mL))
        /// [Data rows]       → Col 0 = TreatmentArm, Dose col = DoseRegimen, Param cols = observations
        /// [SocDivider]      → New ParameterCategory
        /// [Sub-header row]  → Refreshed parameter definitions (may differ from first section)
        /// [Data rows]       → Continue with new context
        /// ```
        /// </remarks>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <param name="observations">The observation list to populate.</param>
        /// <param name="population">Detected population from caption/section.</param>
        /// <param name="captionHint">Caption-derived value type hint.</param>
        /// <returns>The populated observations list.</returns>
        private List<ParsedObservation> parseCompoundLayout(
            ReconstructedTable table,
            List<ParsedObservation> observations,
            string? population,
            CaptionValueHint captionHint)
        {
            #region implementation

            // 1. Extract initial ParameterCategory from spanning header
            var spanningText = table.Header?.Columns?.FirstOrDefault()?.LeafHeaderText;
            var currentCategory = extractCategoryFromSpanningHeader(spanningText);

            // 2. Get all data rows (includes SocDividers)
            var dataRows = getDataBodyRows(table);
            if (dataRows.Count == 0)
                return observations;

            // 3. Consume first DataBody row as sub-header
            int rowIndex = 0;
            var firstSubHeader = dataRows[rowIndex];
            var doseColIndex = detectDoseColumnInSubHeader(firstSubHeader);
            var (paramDefs, subtypeMap) = extractParameterDefinitionsFromDataRow(firstSubHeader, doseColIndex);
            rowIndex++;

            if (paramDefs.Count == 0)
                return observations;

            // 4. Iterate remaining rows
            string? currentDoseRegimen = null;

            for (; rowIndex < dataRows.Count; rowIndex++)
            {
                var row = dataRows[rowIndex];

                // Handle SocDivider: update category, consume next row as sub-header
                if (row.Classification == RowClassification.SocDivider)
                {
                    var dividerText = row.SocName ?? row.Cells?.FirstOrDefault()?.CleanedText;
                    currentCategory = extractCategoryFromSpanningHeader(dividerText);
                    currentDoseRegimen = null;

                    // Next row after SocDivider is likely a new sub-header
                    if (rowIndex + 1 < dataRows.Count
                        && dataRows[rowIndex + 1].Classification == RowClassification.DataBody
                        && looksLikeSubHeader(dataRows[rowIndex + 1]))
                    {
                        rowIndex++;
                        doseColIndex = detectDoseColumnInSubHeader(dataRows[rowIndex]);
                        (paramDefs, subtypeMap) = extractParameterDefinitionsFromDataRow(dataRows[rowIndex], doseColIndex);
                    }

                    continue;
                }

                // Regular data row
                var col0Cell = getCellAtColumn(row, 0);
                var col0Text = col0Cell?.CleanedText?.Trim();

                // Skip empty label rows
                if (string.IsNullOrWhiteSpace(col0Text))
                    continue;

                // R1.1 — Classify col 0 row label so compound-layout tables
                // (e.g., TID 2069 Norfloxacin with `Male`/`Female`/`Young`/`Elderly`/
                // `Hemodialysis`/`CLCR X to Y mL/min` row labels and `InferredHeader +
                // SocDividers` flags) route population stratifiers to Population
                // rather than sending col 0 straight to TreatmentArm. Pre-R1.1 the
                // compound path unconditionally parked col 0 into TreatmentArm,
                // producing the 832+ false-positive rows observed in the 2026-04-21
                // post-R1 validation audit. `Unknown` falls back to the pre-R1.1
                // behavior (TreatmentArm = col 0) to preserve drug-name / treatment
                // arms (the common happy path for this layout).
                var rowLabel = classifyRowLabel(col0Text);

                // Col 0 = treatment arm label (default) — overridden per classification
                string? armLabel = col0Text;
                string? rowPopulationOverride = null;
                string? rowTimepointOverride = null;
                double? rowTimeOverride = null;
                string? rowTimeUnitOverride = null;
                string? attributionFlag = null;

                switch (rowLabel.Kind)
                {
                    case RowLabelKind.Population:
                        armLabel = null;
                        rowPopulationOverride = rowLabel.Population;
                        attributionFlag = rowLabel.MatchedPopulationViaRegex
                            ? "PK_COMPOUND_POP_ROUTED_REGEX"
                            : "PK_COMPOUND_POP_ROUTED";
                        break;

                    case RowLabelKind.Timepoint:
                        armLabel = null;
                        rowTimepointOverride = rowLabel.Timepoint;
                        rowTimeOverride = rowLabel.Time;
                        rowTimeUnitOverride = rowLabel.TimeUnit;
                        attributionFlag = "PK_COMPOUND_TIMEPOINT_ROUTED";
                        break;

                    case RowLabelKind.DrugPlusDose:
                        armLabel = rowLabel.TreatmentArm;
                        // Only override the dose column if it's empty — the
                        // explicit dose column keeps precedence in compound tables.
                        if (string.IsNullOrWhiteSpace(currentDoseRegimen))
                        {
                            currentDoseRegimen = rowLabel.DoseRegimen;
                        }
                        attributionFlag = "PK_COMPOUND_ARM_DOSE_SPLIT";
                        break;

                    case RowLabelKind.TreatmentArm:
                    case RowLabelKind.DoseRegimen:
                    case RowLabelKind.Unknown:
                    default:
                        // Pre-R1.1 behavior: col 0 → TreatmentArm. This is the
                        // dominant happy path for compound layouts (drug-name
                        // row labels in renal/hepatic impairment tables).
                        break;
                }

                var armN = extractArmNFromLabel(col0Text);

                // Dose from dose column (carry forward if dose column present)
                if (doseColIndex >= 0)
                {
                    var doseCell = getCellAtColumn(row, doseColIndex);
                    var doseCellText = doseCell?.CleanedText?.Trim();
                    if (!string.IsNullOrWhiteSpace(doseCellText))
                        currentDoseRegimen = doseCellText;
                }

                var (time, timeUnit, timepoint) = extractDuration(currentDoseRegimen);

                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    foreach (var param in paramDefs)
                    {
                        var cell = getCellAtColumn(r, param.columnIndex);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.PK);
                        o.ParameterName = param.name;
                        o.ParameterCategory = currentCategory;
                        o.ParameterSubtype = subtypeMap.GetValueOrDefault(param.columnIndex);
                        o.TreatmentArm = armLabel;
                        o.ArmN = armN;
                        o.DoseRegimen = currentDoseRegimen;
                        var (pkDose3, pkDoseUnit3) = DoseExtractor.Extract(currentDoseRegimen);
                        o.Dose = pkDose3;
                        o.DoseUnit = pkDoseUnit3;
                        o.Population = rowPopulationOverride ?? population;
                        o.Timepoint = rowTimepointOverride ?? timepoint;
                        o.Time = rowTimeOverride ?? time;
                        o.TimeUnit = rowTimeUnitOverride ?? timeUnit;
                        o.Unit = param.unit;
                        if (attributionFlag != null)
                            o.ValidationFlags = appendFlag(o.ValidationFlags, attributionFlag);

                        parseAndApplyPkValue(table, o, cell, param, captionHint);

                        obs.Add(o);
                    }
                });
            }

            // Post-parse: refine generic "CI" bound type from table context
            if (observations.Any(o => o.BoundType == "CI"))
            {
                var ciLevel = detectCILevelFromTableText(table);
                if (ciLevel != null)
                {
                    foreach (var o in observations.Where(o => o.BoundType == "CI"))
                        o.BoundType = ciLevel;
                }
            }

            // Sanity check: populate ArmN from caption "(N=X)" when parser did not set it
            // (compound layout already derives ArmN from "(n=X)" in row labels when present)
            applyCaptionArmNFallback(table, observations);

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a multi-parenthetical PK parameter header string into its constituent parts.
        /// Handles compound headers like "AUC(0-96h)(mcgh/mL)" where multiple parenthetical
        /// groups carry different semantic roles (qualifier vs unit).
        /// </summary>
        /// <remarks>
        /// The last parenthetical group is always treated as the unit. Any preceding groups
        /// become part of the ParameterSubtype. For single-parenthetical headers like
        /// "Cmax (mcg/mL)", subtype is null.
        /// </remarks>
        /// <example>
        /// <code>
        /// parseCompoundParameterHeader("AUC(0-96h)(mcgh/mL)") → ("AUC", "mcgh/mL", "AUC(0-96h)")
        /// parseCompoundParameterHeader("Cmax (mcg/mL)")        → ("Cmax", "mcg/mL", null)
        /// parseCompoundParameterHeader("Tmax (h)")             → ("Tmax", "h", null)
        /// parseCompoundParameterHeader("Dose")                 → ("Dose", null, null)
        /// </code>
        /// </example>
        /// <param name="text">The parameter header text to parse.</param>
        /// <returns>Tuple of (name, unit, subtype).</returns>
        internal static (string name, string? unit, string? subtype) parseCompoundParameterHeader(string text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return (text ?? "", null, null);

            var matches = _allParentheticalsPattern.Matches(text);
            if (matches.Count == 0)
                return (text.Trim(), null, null);

            // Name is everything before the first parenthetical
            var firstParenIndex = text.IndexOf('(');
            var name = text[..firstParenIndex].Trim();

            // Last parenthetical = unit
            var unit = matches[^1].Groups[1].Value.Trim();

            // If multiple parentheticals, build subtype from name + all but last
            string? subtype = null;
            if (matches.Count > 1)
            {
                var subtypeParts = new List<string> { name };
                for (int i = 0; i < matches.Count - 1; i++)
                    subtypeParts.Add($"({matches[i].Groups[1].Value})");
                subtype = string.Join("", subtypeParts);
            }

            return (name, unit, subtype);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the ParameterCategory from a spanning header or SocDivider text by
        /// stripping common PK prefixes like "Pharmacokinetic Parameters for".
        /// </summary>
        /// <example>
        /// <code>
        /// extractCategoryFromSpanningHeader("Pharmacokinetic Parameters for Renal Impairment")
        ///     → "Renal Impairment"
        /// extractCategoryFromSpanningHeader("Hepatic Impairment")
        ///     → "Hepatic Impairment"
        /// </code>
        /// </example>
        /// <param name="spanningText">The spanning header or SocDivider text.</param>
        /// <returns>The category portion of the text, or the full text if no prefix found.</returns>
        private static string? extractCategoryFromSpanningHeader(string? spanningText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(spanningText))
                return spanningText;

            var stripped = _pkCategoryPrefixPattern.Replace(spanningText.Trim(), "");
            return string.IsNullOrWhiteSpace(stripped) ? spanningText.Trim() : stripped.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the sample size from a table caption containing a parenthesized
        /// "(N=X)" or "(n=X)" expression. Uses the same pattern as
        /// <see cref="extractArmNFromLabel"/> — case-insensitive, comma-friendly.
        /// </summary>
        /// <remarks>
        /// PK table captions frequently embed the study population size in a trailing
        /// parenthetical (e.g., "... ESTRADIOL TRANSDERMAL SYSTEM (N=36)"). When the
        /// parser cannot recover ArmN from row labels or cell expressions, the caption
        /// provides a reasonable fallback that is applied via
        /// <see cref="applyCaptionArmNFallback"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// extractArmNFromCaption("Table 2: ... (N=36)")  → 36
        /// extractArmNFromCaption("PK Summary (n=1,234)") → 1234
        /// extractArmNFromCaption("No sample size here")  → null
        /// extractArmNFromCaption(null)                   → null
        /// </code>
        /// </example>
        /// <param name="caption">The table caption text to scan.</param>
        /// <returns>The sample size as int, or null if not found.</returns>
        /// <seealso cref="applyCaptionArmNFallback"/>
        internal static int? extractArmNFromCaption(string? caption)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(caption))
                return null;

            // Reuse _armNFromLabelPattern — same parenthesized N= form, case-insensitive
            var match = _armNFromLabelPattern.Match(caption);
            if (!match.Success)
                return null;

            var rawN = match.Groups[1].Value.Replace(",", "");
            return int.TryParse(rawN, out var n) ? n : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sanity-check post-process: when observations have null ArmN and the table
        /// caption contains a parenthesized "(N=X)" expression, populates ArmN on those
        /// observations from the caption value.
        /// </summary>
        /// <remarks>
        /// ## Safeguards
        /// - Never overrides an existing ArmN (guards against parser-derived values).
        /// - Appends "PK_CAPTION_ARMN_FALLBACK:{n}" to ValidationFlags for audit trail.
        /// - No-op when caption is null/whitespace or does not contain "(N=X)".
        /// </remarks>
        /// <param name="table">Source table carrying the caption.</param>
        /// <param name="observations">Observations to update in place.</param>
        /// <seealso cref="extractArmNFromCaption"/>
        private static void applyCaptionArmNFallback(
            ReconstructedTable table,
            List<ParsedObservation> observations)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(table.Caption) || observations.Count == 0)
                return;

            var captionN = extractArmNFromCaption(table.Caption);
            if (!captionN.HasValue)
                return;

            foreach (var o in observations)
            {
                // Only populate when ArmN is currently null — never override
                if (o.ArmN.HasValue)
                    continue;

                o.ArmN = captionN;
                o.ValidationFlags = appendFlag(o.ValidationFlags, $"PK_CAPTION_ARMN_FALLBACK:{captionN}");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects transposed PK table layouts where rows carry PK metric names and
        /// columns carry dose levels — the inverse of the canonical layout where rows
        /// are dose regimens and columns are PK parameters.
        /// </summary>
        /// <remarks>
        /// ## Detection signals (all three must hold)
        /// 1. Column 0 header matches a generic "Parameter"-style label
        ///    (see <see cref="_transposedLayoutCol0Headers"/>) OR is blank/missing.
        /// 2. Every non-col-0 header is dose-shaped (matches <see cref="_doseHeaderPattern"/>).
        /// 3. At least 2 data-body rows have col 0 text matching a canonical PK
        ///    metric name via <see cref="PkParameterDictionary.IsPkParameter"/>
        ///    AND they form the majority of data-body rows.
        ///
        /// Requiring all three signals simultaneously avoids false positives on
        /// ambiguous tables.
        /// </remarks>
        /// <example>
        /// <code>
        /// Header: Parameter | 0.1 mg/day | 0.05 mg/day | 0.025 mg/day
        /// Rows:   AUC84(pg·hr/mL) | ...
        ///         Cmax(pg/mL)     | ...
        /// → true (transposed)
        /// </code>
        /// </example>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True when the table is a transposed PK layout.</returns>
        /// <seealso cref="applyTransposedPkLayoutSwap"/>
        internal static bool detectTransposedPkLayout(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count < 2)
                return false;

            // Signal 1: col 0 header is either a generic "Parameter"-style label
            // OR blank/missing (common when the source HTML omitted a label for the
            // row-description column — e.g., the Ceftriaxone tables in the corpus).
            var col0Text = table.Header.Columns[0].LeafHeaderText?.Trim();
            var col0IsGenericOrBlank = string.IsNullOrWhiteSpace(col0Text)
                || _transposedLayoutCol0Headers.Contains(col0Text);
            if (!col0IsGenericOrBlank)
                return false;

            // Signal 2 (R1.2-broadened): every non-col-0 header is either
            // dose-shaped OR classifies as a PK row-label kind (Timepoint,
            // Population, DrugPlusDose, TreatmentArm, DoseRegimen) via
            // <see cref="classifyRowLabel"/>. Pre-R1.2 this signal required
            // dose-shape only; that rejected food-state ("Light Breakfast") and
            // population-stratifier transposed tables that R1.2 now routes
            // correctly. Generic prose headers (Unknown kind) still disqualify.
            int recognizedHeaders = 0;
            int totalHeaders = 0;
            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var h = table.Header.Columns[i].LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(h))
                    continue;
                totalHeaders++;
                if (_doseHeaderPattern.IsMatch(h) ||
                    classifyRowLabel(h).Kind != RowLabelKind.Unknown)
                    recognizedHeaders++;
            }
            if (totalHeaders == 0 || recognizedHeaders != totalHeaders)
                return false;

            // Signal 3: majority of data rows carry canonical PK metric row labels.
            // Uses PkParameterDictionary so long-form English phrases like
            // "Maximum Plasma Concentrations" and "Elimination Half-life (hr)" match
            // in addition to abbreviated forms (Cmax, AUC, t½).
            int metricRows = 0;
            int totalRows = 0;
            foreach (var row in getDataBodyRows(table))
            {
                var t = getCellAtColumn(row, 0)?.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(t))
                    continue;
                totalRows++;
                if (PkParameterDictionary.IsPkParameter(t))
                    metricRows++;
            }

            // Require ≥ 2 PK-metric rows AND they form the majority
            return metricRows >= 2 && metricRows * 2 >= totalRows;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Post-process swap applied after standard PK parsing detects a transposed
        /// layout via <see cref="detectTransposedPkLayout"/>. Exchanges ParameterName
        /// (originally a dose header like "0.1 mg/day") with DoseRegimen (originally
        /// the PK metric row label like "AUC84(pg·hr/mL)"), extracting unit from the
        /// parenthesized metric and re-running <see cref="DoseExtractor.Extract"/>
        /// on the new DoseRegimen.
        /// </summary>
        /// <remarks>
        /// ## Swap mechanics per observation
        /// 1. originalParamName ← o.ParameterName (dose header)
        /// 2. originalDoseRegimen ← o.DoseRegimen (PK metric)
        /// 3. Split originalDoseRegimen via <see cref="_paramUnitPattern"/> → (name, unit)
        /// 4. o.ParameterName = name; o.Unit = unit (if not already set)
        /// 5. R1.2 — Classify originalParamName via <see cref="classifyRowLabel"/>.
        ///    - Timepoint kind (food-state, "Day N") → write Timepoint / Time / TimeUnit;
        ///      leave DoseRegimen = null. Flag <c>PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED</c>.
        ///    - Population kind (sex, age stratum, impairment) → write Population;
        ///      leave DoseRegimen = null. Flag <c>PK_TRANSPOSED_HEADER_POP_ROUTED</c>.
        ///    - TreatmentArm / DrugPlusDose / DoseRegimen / Unknown → preserve pre-R1.2
        ///      behavior (DoseRegimen = originalParamName, re-extract Dose/DoseUnit).
        /// 6. If the unit is a time-measure, surface PrimaryValue on Time/TimeUnit
        /// 7. Append "PK_TRANSPOSED_LAYOUT_SWAP" to ValidationFlags
        /// </remarks>
        /// <param name="observations">Observations to swap in place.</param>
        /// <seealso cref="detectTransposedPkLayout"/>
        /// <seealso cref="classifyRowLabel"/>
        internal static void applyTransposedPkLayoutSwap(List<ParsedObservation> observations)
        {
            #region implementation

            foreach (var o in observations)
            {
                // Capture originals before mutation
                var originalParamName = o.ParameterName;       // was dose header
                var originalDoseRegimen = o.DoseRegimen;       // was PK metric row label

                // Rebuild ParameterName from the row-label metric, extracting unit if parenthesized
                if (!string.IsNullOrWhiteSpace(originalDoseRegimen))
                {
                    var m = _paramUnitPattern.Match(originalDoseRegimen);
                    string rawParamName;
                    if (m.Success)
                    {
                        rawParamName = m.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(o.Unit))
                            o.Unit = m.Groups[2].Value.Trim();
                    }
                    else
                    {
                        rawParamName = originalDoseRegimen.Trim();
                    }

                    // Collapse long-form English and Unicode-laden variants to canonical
                    // PK names (e.g., "Maximum Plasma Concentrations" → "Cmax",
                    // "Elimination Half-life" → "t½", "AUC0-∞" → "AUC0-inf").
                    if (PkParameterDictionary.TryCanonicalize(rawParamName, out var canonicalName))
                    {
                        o.ParameterName = canonicalName;
                        o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_TRANSPOSED_CANONICALIZED");
                    }
                    else
                    {
                        o.ParameterName = rawParamName;
                    }
                }

                // R1.2 — Classify the original column header (now about to become
                // DoseRegimen/TreatmentArm) so food-state, population, or timepoint
                // headers route to their contract-correct columns. The pre-R1.2
                // behavior parked every header in DoseRegimen regardless of content.
                var headerClass = classifyRowLabel(originalParamName);

                switch (headerClass.Kind)
                {
                    case RowLabelKind.Timepoint:
                        // Food-state ("Light Breakfast", "Fasted") and timepoint
                        // ("Day 14", "C72") headers populate Timepoint, not DoseRegimen.
                        o.Timepoint = headerClass.Timepoint;
                        o.Time = headerClass.Time;
                        o.TimeUnit = headerClass.TimeUnit;
                        o.DoseRegimen = null;
                        o.Dose = null;
                        o.DoseUnit = null;
                        o.ValidationFlags = appendFlag(
                            o.ValidationFlags, "PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED");
                        break;

                    case RowLabelKind.Population:
                        // Stratifier headers ("Healthy Volunteers", "Renal Impairment",
                        // "Male"/"Female" in food-effect tables) populate Population.
                        // Only write when empty so an upstream caption-derived
                        // population is not clobbered.
                        if (string.IsNullOrWhiteSpace(o.Population))
                            o.Population = headerClass.Population;
                        o.DoseRegimen = null;
                        o.Dose = null;
                        o.DoseUnit = null;
                        o.ValidationFlags = appendFlag(
                            o.ValidationFlags,
                            headerClass.MatchedPopulationViaRegex
                                ? "PK_TRANSPOSED_HEADER_POP_ROUTED_REGEX"
                                : "PK_TRANSPOSED_HEADER_POP_ROUTED");
                        break;

                    case RowLabelKind.TreatmentArm:
                    case RowLabelKind.DrugPlusDose:
                    case RowLabelKind.DoseRegimen:
                    case RowLabelKind.Unknown:
                    default:
                        // Pre-R1.2 behavior: column header → DoseRegimen, re-extract
                        // Dose/DoseUnit. This is the happy path for dose-level column
                        // headers like "50 mg/kg IV" (TID 13202 Ceftriaxone shape).
                        o.DoseRegimen = originalParamName;
                        var (dose, doseUnit) = DoseExtractor.Extract(originalParamName);
                        if (dose.HasValue)
                        {
                            o.Dose = dose;
                            o.DoseUnit = doseUnit;
                        }
                        break;
                }

                // If the metric is a time measurement (Tmax, Half-life), surface PrimaryValue
                if (o.PrimaryValue.HasValue && !string.IsNullOrWhiteSpace(o.Unit)
                    && _timeUnitStrings.Contains(o.Unit))
                {
                    o.Time = o.PrimaryValue;
                    o.TimeUnit = normalizeTimeUnit(o.Unit);
                }

                o.ValidationFlags = appendFlag(o.ValidationFlags, "PK_TRANSPOSED_LAYOUT_SWAP");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the sample size (ArmN) from a treatment arm row label containing an
        /// "(n=X)" suffix. Returns null if no n= pattern is found.
        /// </summary>
        /// <example>
        /// <code>
        /// extractArmNFromLabel("Healthy Volunteers (n=6)")   → 6
        /// extractArmNFromLabel("Alcoholic Cirrhosis (n=18)") → 18
        /// extractArmNFromLabel("Severe Renal Impairment")    → null
        /// </code>
        /// </example>
        /// <param name="label">The treatment arm row label text.</param>
        /// <returns>The sample size as int, or null if not found.</returns>
        internal static int? extractArmNFromLabel(string? label)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(label))
                return null;

            var match = _armNFromLabelPattern.Match(label);
            if (!match.Success)
                return null;

            var rawN = match.Groups[1].Value.Replace(",", "");
            return int.TryParse(rawN, out var n) ? n : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans a data row (acting as a sub-header) for a cell whose text matches
        /// <see cref="_doseColumnHeaders"/>. Returns the resolved column index of
        /// the dose cell, or -1 if no dose column found.
        /// </summary>
        /// <param name="row">The data row to inspect as a sub-header.</param>
        /// <returns>The column index of the dose cell, or -1.</returns>
        private static int detectDoseColumnInSubHeader(ReconstructedRow row)
        {
            #region implementation

            if (row.Cells == null)
                return -1;

            foreach (var cell in row.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && _doseColumnHeaders.Contains(text))
                    return cell.ResolvedColumnStart ?? -1;
            }

            return -1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts PK parameter definitions from a data row that serves as an embedded
        /// sub-header (e.g., the first data row in a compound header layout). Returns the
        /// same tuple structure as <see cref="extractParameterDefinitions"/> for compatibility,
        /// plus a separate subtype dictionary for compound headers like "AUC(0-96h)(mcgh/mL)".
        /// </summary>
        /// <param name="row">The data row containing sub-header text in its cells.</param>
        /// <param name="doseColumnIndex">Column index of the dose column to skip (-1 if none).</param>
        /// <returns>
        /// Tuple of (paramDefs, subtypeMap):
        /// - paramDefs: same structure as extractParameterDefinitions
        /// - subtypeMap: columnIndex → subtype string for compound headers (e.g., "AUC(0-96h)")
        /// </returns>
        internal static (
            List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> paramDefs,
            Dictionary<int, string?> subtypeMap
        ) extractParameterDefinitionsFromDataRow(ReconstructedRow row, int doseColumnIndex)
        {
            #region implementation

            var paramDefs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>();
            var subtypeMap = new Dictionary<int, string?>();

            if (row.Cells == null)
                return (paramDefs, subtypeMap);

            foreach (var cell in row.Cells)
            {
                var colIndex = cell.ResolvedColumnStart ?? -1;
                if (colIndex < 0)
                    continue;

                // Skip col 0 (row label axis) and the dose column
                if (colIndex == 0 || colIndex == doseColumnIndex)
                    continue;

                var text = cell.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // R2 — Suppress non-PK context columns in compound sub-headers too.
                // Prevents compound-layout tables from emitting ParameterName for
                // columns like "Co-administered Drug" if they ever surface here.
                if (isContextColumnHeader(text))
                    continue;

                // Check if column is a sample size column
                var isSampleSize = _sampleSizeHeaders.Contains(text);

                // Parse compound parameter header
                var (name, unit, subtype) = parseCompoundParameterHeader(text);

                var isTimeMeasure = unit != null && _timeUnitStrings.Contains(unit);

                paramDefs.Add((colIndex, name, unit, isTimeMeasure, isSampleSize));
                subtypeMap[colIndex] = subtype;
            }

            return (paramDefs, subtypeMap);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether a table has a compound header layout: a spanning header row
        /// (all columns share identical LeafHeaderText), embedded sub-header rows with
        /// parameter definitions, and SocDivider rows for section context switches.
        /// </summary>
        /// <remarks>
        /// Detection requires ALL of the following signals:
        /// 1. HasSocDividers=true AND HasInferredHeader=true
        /// 2. At least 3 header columns
        /// 3. All header columns have identical LeafHeaderText (spanning header repeated)
        /// 4. First DataBody row contains at least one dose keyword cell AND one param unit cell
        /// </remarks>
        /// <param name="table">The reconstructed table to evaluate.</param>
        /// <returns>True if compound header layout is detected.</returns>
        internal static bool detectCompoundHeaderLayout(ReconstructedTable table)
        {
            #region implementation

            // Guard: need both structural flags
            if (table.HasSocDividers != true || table.HasInferredHeader != true)
                return false;

            // Guard: need at least 3 columns (label + dose + 1 param minimum)
            if (table.Header?.Columns == null || table.Header.Columns.Count < 3)
                return false;

            // Signal 1: All header columns have identical LeafHeaderText
            var distinctTexts = table.Header.Columns
                .Select(c => c.LeafHeaderText?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctTexts != 1)
                return false;

            // Signal 2: First DataBody row looks like a sub-header
            var firstDataRow = table.Rows?
                .FirstOrDefault(r => r.Classification == RowClassification.DataBody);
            if (firstDataRow?.Cells == null)
                return false;

            bool hasDoseCell = false;
            bool hasParamCell = false;

            foreach (var cell in firstDataRow.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (_doseColumnHeaders.Contains(text))
                    hasDoseCell = true;

                if (_paramUnitPattern.IsMatch(text))
                    hasParamCell = true;
            }

            return hasDoseCell && hasParamCell;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether a data row looks like an embedded sub-header row by testing
        /// if any cell matches a dose column keyword. Used to confirm rows following
        /// SocDividers should be consumed as new sub-headers rather than parsed as data.
        /// </summary>
        /// <param name="row">The data row to evaluate.</param>
        /// <returns>True if the row appears to be a sub-header.</returns>
        private static bool looksLikeSubHeader(ReconstructedRow row)
        {
            #region implementation

            if (row.Cells == null)
                return false;

            foreach (var cell in row.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && _doseColumnHeaders.Contains(text))
                    return true;
            }

            return false;

            #endregion
        }

        #endregion Compound Header Layout
    }
}
