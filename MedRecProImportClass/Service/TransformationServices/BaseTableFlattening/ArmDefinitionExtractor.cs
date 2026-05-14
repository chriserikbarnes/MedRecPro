using System.Text.RegularExpressions;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Extracts treatment-arm definitions and arm-level metadata from resolved table headers.
    /// </summary>
    /// <remarks>
    /// This helper keeps header recovery and arm-axis interpretation outside the parser base
    /// class while preserving the same conservative recovery rules.
    /// </remarks>
    /// <seealso cref="ArmDefinition"/>
    /// <seealso cref="BaseTableParser"/>
    internal static class ArmDefinitionExtractor
    {
        #region Compiled Patterns

        private static readonly Regex _statColumnPattern = new(
            @"(?:P[\s-]*[Vv]alue|Difference|Risk\s+Reduction|\b(?:ARR|RR|HR|OR)\b|95\s*%?\s*CI|Relative\s+Risk)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _timepointPattern = new(
            @"(?:(?:Week|Month|Year|Day)\s*\d+|\d+\s*(?:Weeks?|Months?|Years?|Days?))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _trailingFormatHintPattern = new(
            @"^(.+?)\s+(n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _genericArmLabelPattern = new(
            @"^\s*(?:" +
              @"Events?|" +
              @"Body\s+System(?:\s*[\(/\-].*)?|" +
              @"System\s+Organ\s+Class|SOC|" +
              @"MedDRA(?:\s+(?:Term|Preferred\s+Term))?|" +
              @"Adverse\s+(?:Reactions?|Events?|Experiences?)|" +
              @"Reactions?|" +
              @"Outcomes?|" +
              @"Variables?|" +
              @"Parameters?|" +
              @"Preferred\s+Term|Term|" +
              @"Percent|Percentage|%|" +
              @"n\s*\(\s*%\s*\)|" +
              @"[nN]|" +
              @"Number|Count|Frequency" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _armAxisLabelPattern = new(
            @"^\s*(?:" +
              @"Any(?:\s+(?:Grade|Grades?))?(?:\s+Adverse\s+(?:Reactions?|Events?))?|" +
              @"All\s+(?:CTC\s+)?Grades?|" +
              @"(?:CTC|CTCAE|NCI)\s+Grades?.*|" +
              @"(?:>=|>|\u2265)\s*Grade\s*\d+(?:\s+.*)?|" +
              @"(?:Toxicity|Severity)\s+Grades?\s*(?:(?:>=|>|\u2265)\s*)?(?:\d+|[IVX]+)?(?:\s+.*)?|" +
              @"Grades?\s*(?:(?:>=|>|\u2265)\s*)?(?:\d+|[IVX]+)(?:\s*(?:[-/\u2013,&]|and|or|to)\s*(?:\d+|[IVX]+)|\s+or\s+(?:Higher|Greater))*" +
                  @"(?:\s+Adverse\s+(?:Reactions?|Events?))?|" +
              @"(?:Number|No\.?|Percent(?:age)?)\s*(?:\(\s*%\s*\))?\s*(?:of\s+)?(?:Patients|Subjects|Participants|Reporting)?(?:\s+.*)?|" +
              @"%\s+of\s+(?:Patients|Subjects|Participants)(?:\s+.*)?|" +
              @"Incidence\s+of\s+adverse\s+(?:reactions?|events?)" +
            @")\s*(?:\(\s*%\s*\)|%)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _contextAxisLabelPattern = new(
            @"^\s*(?:" +
              @"\d+\s*[-\u2013]\s*day\s+Treatment|" +
              @"(?:Kidney|Heart|Liver)\s+Stud(?:y|ies)|" +
              @"(?:MDD|OCD|GAD)(?:\s*/\s*(?:MDD|OCD|GAD))*|" +
              @"Age\s+Group|" +
              @"Study\s+\d+[A-Z]?|" +
              @"Treatment\s+Regimen|" +
              @"Overall|" +
              @"All\s+Adverse\s+Reactions?|" +
              @"Grade\s+\d+(?:\s*/\s*\d+)?\s+Adverse\s+Reactions?|" +
              @"(?:HDD|NDD|PDD)\s*-\s*CKD|" +
              @"OXC\s+\d+(?:\s*/\s*\d+)*" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _formatHintCellPattern = new(
            @"^(?:n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _tableNumberPrefixPattern = new(
            @"^\s*Table\s+[\w\d\-]+\s*[:.\-]\s*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _aeCaptionMeasurePhrasePattern = new(
            @"\b(?:(?:Treatment[-\s]*Emergent|Common|Serious|Most\s+Frequent|Drug[-\s]*Related)\s+)*" +
            @"Adverse\s+(?:Reactions?|Events?|Experiences?)" +
            @"|\bIncidence\s+(?:\(\s*%\s*\)\s+)?of\s+[\w\s\-,]*Adverse" +
            @"|\bFrequency\s+of\s+[\w\s\-,]*Adverse" +
            @"|\bPercent\s+of\s+Patients\s+(?:Reporting|With)\s+[\w\s\-,]*Adverse",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _aeCaptionConnectorPattern = new(
            @"\b(?:reported\s+in|observed\s+in|occurring\s+in|seen\s+in|during|from|among|in)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _htmlTagPattern = new(
            @"<[^>]+>",
            RegexOptions.Compiled);

        private static readonly Regex _trailingFootnoteMarkerPattern = new(
            @"(?:\s*\([\*\u2020\u2021\u00A7\u00B6]\)|\s*[\*\u2020\u2021\u00A7\u00B6]+)+\s*$",
            RegexOptions.Compiled);

        #endregion Compiled Patterns

        /**************************************************************/
        /// <summary>
        /// Extracts arm definitions from the resolved table header.
        /// </summary>
        /// <param name="table">Table with resolved header metadata.</param>
        /// <returns>Arm definitions keyed by resolved column positions.</returns>
        /// <seealso cref="ArmDefinition"/>
        internal static List<ArmDefinition> ExtractArmDefinitions(ReconstructedTable table)
        {
            #region implementation

            var arms = new List<ArmDefinition>();
            if (table.Header?.Columns == null || table.Header.Columns.Count <= 1)
                return arms;

            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var context = AeColumnContextResolver.Resolve(col, table.Header.Columns, i);
                if (context.TreatmentArm == null)
                {
                    arms.Add(CreatePlaceholderArmDefinition(col, context));
                    continue;
                }

                var leafText = context.TreatmentArm;
                var arm = ValueParser.ParseArmHeader(leafText);
                if (arm != null)
                {
                    arm.ColumnIndex = col.ColumnIndex;

                    if (!string.IsNullOrWhiteSpace(context.StudyContext))
                    {
                        arm.StudyContext = context.StudyContext;
                    }
                    else if (col.HeaderPath != null && col.HeaderPath.Count > 1)
                    {
                        var contextCandidate = col.HeaderPath[0]?.Trim();
                        if (!string.Equals(contextCandidate, leafText, StringComparison.OrdinalIgnoreCase))
                            arm.StudyContext = contextCandidate;
                    }

                    ApplyAxisMetadata(col.LeafHeaderText, arm);
                    ApplyColumnContextMetadata(context, arm);
                    arms.Add(arm);
                }
                else
                {
                    var trimmed = leafText.Trim();
                    var hintMatch = _trailingFormatHintPattern.Match(trimmed);
                    var armName = hintMatch.Success ? hintMatch.Groups[1].Value.Trim() : trimmed;
                    var formatHint = hintMatch.Success ? hintMatch.Groups[2].Value.Trim() : (string?)null;

                    var fallbackArm = new ArmDefinition
                    {
                        Name = armName,
                        FormatHint = formatHint,
                        ColumnIndex = col.ColumnIndex,
                        StudyContext = !string.IsNullOrWhiteSpace(context.StudyContext)
                            ? context.StudyContext
                            : col.HeaderPath != null &&
                              col.HeaderPath.Count > 1 &&
                              !string.Equals(col.HeaderPath[0]?.Trim(), armName, StringComparison.OrdinalIgnoreCase)
                                ? col.HeaderPath[0]?.Trim()
                                : null
                    };
                    ApplyAxisMetadata(col.LeafHeaderText, fallbackArm);
                    ApplyColumnContextMetadata(context, fallbackArm);
                    arms.Add(fallbackArm);
                }
            }

            return arms;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recovers the best treatment-arm header text for a resolved header column.
        /// </summary>
        /// <param name="col">Header column to inspect.</param>
        /// <returns>Recovered arm text, or null when the header path is structural only.</returns>
        internal static string? RecoverArmHeaderText(HeaderColumn col)
        {
            #region implementation

            var leaf = col.LeafHeaderText?.Trim();
            if (!LooksLikeGenericArmLabel(leaf))
                return leaf;

            if (col.HeaderPath != null)
            {
                for (int i = col.HeaderPath.Count - 2; i >= 0; i--)
                {
                    var candidate = col.HeaderPath[i]?.Trim();
                    if (!LooksLikeGenericArmLabel(candidate))
                        return candidate;
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether header text is generic structural axis text rather than an arm.
        /// </summary>
        /// <param name="text">Header text to inspect.</param>
        /// <returns>True when the text should not be emitted as a treatment arm.</returns>
        internal static bool LooksLikeGenericArmLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return true;

            return _genericArmLabelPattern.IsMatch(text) ||
                   _armAxisLabelPattern.IsMatch(text) ||
                   _contextAxisLabelPattern.IsMatch(text) ||
                   AeColumnContextResolver.IsInvalidTreatmentArm(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an arm definition carries a usable treatment-arm name.
        /// </summary>
        /// <param name="arm">Arm definition to inspect.</param>
        /// <returns>True when observations may be emitted for this arm.</returns>
        internal static bool HasUsableTreatmentArm(ArmDefinition? arm)
        {
            #region implementation

            return arm != null &&
                   !string.IsNullOrWhiteSpace(arm.Name) &&
                   !LooksLikeGenericArmLabel(arm.Name);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies a conservative single-product treatment-arm fallback to unresolved arms.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="arms">Arm definitions to update.</param>
        /// <returns>True when the fallback populated all unresolved arm names.</returns>
        internal static bool ApplySingleProductArmFallback(ReconstructedTable table, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0 || arms.Any(HasUsableTreatmentArm))
                return false;

            if (arms.Any(HasInvalidStructuralArmContext))
                return false;

            var productArm = InferSingleProductArmFromTitle(table);
            if (string.IsNullOrWhiteSpace(productArm))
                return false;

            foreach (var arm in arms)
            {
                if (string.IsNullOrWhiteSpace(arm.StudyContext))
                    arm.StudyContext = GetContextAxisLabel(arm.Name);

                arm.Name = productArm;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a comparison-statistic column.
        /// </summary>
        /// <param name="headerText">Header text to inspect.</param>
        /// <returns>True for p-value, risk-ratio, confidence-interval, and similar columns.</returns>
        internal static bool IsStatColumn(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            return _statColumnPattern.IsMatch(headerText);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a timepoint.
        /// </summary>
        /// <param name="headerText">Header text to inspect.</param>
        /// <returns>True when the header names a day, week, month, or year timepoint.</returns>
        internal static bool IsTimepointColumn(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            return _timepointPattern.IsMatch(headerText);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies value-axis metadata to an arm definition.
        /// </summary>
        /// <param name="text">Header or body metadata text.</param>
        /// <param name="arm">Arm definition to update.</param>
        /// <seealso cref="ArmDefinition.ParameterSubtype"/>
        internal static void ApplyAxisMetadata(string? text, ArmDefinition arm)
        {
            #region implementation

            if (!TryExtractAxisMetadata(text, out var subtype, out var formatHint))
                return;

            if (!string.IsNullOrWhiteSpace(subtype))
                arm.ParameterSubtype = subtype;

            if (!string.IsNullOrWhiteSpace(formatHint))
                arm.FormatHint = formatHint;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies resolved AE column-context metadata to an arm definition.
        /// </summary>
        /// <param name="context">Resolved column context.</param>
        /// <param name="arm">Arm definition to update.</param>
        internal static void ApplyColumnContextMetadata(AeColumnContext context, ArmDefinition arm)
        {
            #region implementation

            if (!string.IsNullOrWhiteSpace(context.ParameterSubtype))
                arm.ParameterSubtype = context.ParameterSubtype;

            if (!string.IsNullOrWhiteSpace(context.FormatHint))
                arm.FormatHint = context.FormatHint;

            if (!string.IsNullOrWhiteSpace(context.StudyContext) &&
                string.IsNullOrWhiteSpace(arm.StudyContext))
            {
                arm.StudyContext = context.StudyContext;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts value-axis metadata and format hints from non-arm axis text.
        /// </summary>
        /// <param name="text">Axis label text.</param>
        /// <param name="subtype">Resolved parameter subtype.</param>
        /// <param name="formatHint">Resolved value format hint.</param>
        /// <returns>True when the text is recognized as axis metadata.</returns>
        internal static bool TryExtractAxisMetadata(string? text, out string? subtype, out string? formatHint)
        {
            #region implementation

            subtype = null;
            formatHint = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (AeColumnContextResolver.TryExtractAxisMetadata(trimmed, out subtype, out formatHint))
                return true;

            if (!_armAxisLabelPattern.IsMatch(trimmed) && !_formatHintCellPattern.IsMatch(trimmed))
                return false;

            if (trimmed.Contains("n(", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("number", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("No.", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "n(%)";
            }
            else if (trimmed.Contains('%') ||
                     trimmed.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("percentage", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "%";
            }

            var clean = Regex.Replace(trimmed, @"\s*(?:\(\s*%\s*\)|%)\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (Regex.IsMatch(clean, @"^(?:Any|All\s+(?:CTC\s+)?Grades?|(?:CTC|CTCAE|NCI)\s+Grades?|Grades?|Grade|(?:>=|>|\u2265)\s*Grade|Toxicity\s+Grade|Severity\s+Grade)\b",
                    RegexOptions.IgnoreCase))
            {
                subtype = clean;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts an AE study context descriptor from a table caption.
        /// </summary>
        /// <param name="caption">Raw table caption text.</param>
        /// <returns>Study context descriptor, or null when the caption is not AE-shaped.</returns>
        internal static string? ExtractStudyContextFromCaption(string? caption)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(caption))
                return null;

            var normalized = System.Net.WebUtility.HtmlDecode(caption);
            normalized = _htmlTagPattern.Replace(normalized, " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var stripped = _tableNumberPrefixPattern.Replace(normalized, "");
            var measureMatch = _aeCaptionMeasurePhrasePattern.Match(stripped);
            if (!measureMatch.Success)
                return null;

            var searchStart = measureMatch.Index + measureMatch.Length;
            if (searchStart >= stripped.Length)
                return null;

            var connectorMatch = _aeCaptionConnectorPattern.Match(stripped, searchStart);
            if (!connectorMatch.Success)
                return null;

            var descriptor = stripped.Substring(connectorMatch.Index + connectorMatch.Length).Trim();
            descriptor = _trailingFootnoteMarkerPattern.Replace(descriptor, "").Trim();
            descriptor = descriptor.TrimEnd('.', ',', ';', ':', ' ');

            if (descriptor.Length <= 3)
                return null;

            return descriptor;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a placeholder arm carries structural context.
        /// </summary>
        /// <param name="arm">Arm placeholder to inspect.</param>
        /// <returns>True when title-based fallback should not rewrite the placeholder.</returns>
        private static bool HasInvalidStructuralArmContext(ArmDefinition arm)
        {
            #region implementation

            return AeColumnContextResolver.IsCaptionLikeText(arm.Name) ||
                   AeColumnContextResolver.IsCaptionLikeText(arm.ParameterSubtype) ||
                   AeColumnContextResolver.IsCaptionLikeText(arm.StudyContext) ||
                   AeColumnContextResolver.IsBodySystemLabel(arm.Name) ||
                   AeColumnContextResolver.IsBodySystemLabel(arm.ParameterSubtype) ||
                   AeColumnContextResolver.IsBodySystemLabel(arm.StudyContext);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a placeholder arm definition for body-row metadata recovery.
        /// </summary>
        /// <param name="col">Header column that did not yield an arm name.</param>
        /// <param name="context">Resolved AE column context.</param>
        /// <returns>A placeholder arm retaining column and axis metadata.</returns>
        private static ArmDefinition CreatePlaceholderArmDefinition(HeaderColumn col, AeColumnContext? context = null)
        {
            #region implementation

            var arm = new ArmDefinition
            {
                ColumnIndex = col.ColumnIndex,
                StudyContext = context?.StudyContext ?? GetHeaderStudyContext(col) ?? GetContextAxisLabel(col.LeafHeaderText),
                ParameterSubtype = context?.ParameterSubtype,
                FormatHint = context?.FormatHint
            };
            ApplyAxisMetadata(col.LeafHeaderText, arm);
            return arm;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a non-generic parent header as study context for a placeholder arm.
        /// </summary>
        /// <param name="col">Header column to inspect.</param>
        /// <returns>Study context candidate, or null.</returns>
        private static string? GetHeaderStudyContext(HeaderColumn col)
        {
            #region implementation

            if (col.HeaderPath == null || col.HeaderPath.Count <= 1)
                return null;

            var candidate = col.HeaderPath[0]?.Trim();
            return LooksLikeGenericArmLabel(candidate) ? null : candidate;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns a context-axis label that should be preserved outside TreatmentArm.
        /// </summary>
        /// <param name="text">Header text to inspect.</param>
        /// <returns>Context-axis label, or null.</returns>
        private static string? GetContextAxisLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var trimmed = text.Trim();
            return _contextAxisLabelPattern.IsMatch(trimmed) ? trimmed : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers a single product arm from concise document-title evidence.
        /// </summary>
        /// <param name="table">Reconstructed table with title metadata.</param>
        /// <returns>Product arm candidate, or null.</returns>
        private static string? InferSingleProductArmFromTitle(ReconstructedTable table)
        {
            #region implementation

            var title = TextUtil.RemoveTags(table.Title ?? string.Empty);
            title = Regex.Replace(title, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var candidate = ExtractProductTitleCandidate(title);
            return candidate != null && IsProductTitleCandidate(candidate) ? candidate : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts a concise product-name candidate from a document title.
        /// </summary>
        /// <param name="title">Cleaned document title.</param>
        /// <returns>Candidate product name, or null.</returns>
        private static string? ExtractProductTitleCandidate(string title)
        {
            #region implementation

            var firstSentence = Regex.Split(title, @"[\r\n.]")
                .Select(p => p.Trim())
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            if (string.IsNullOrWhiteSpace(firstSentence))
                return null;

            var productMatch = Regex.Match(firstSentence,
                @"^([A-Z][A-Za-z0-9-]{2,}(?:\s+[A-Z][A-Za-z0-9-]{2,}){0,3})(?=\s*(?:\(|-|,|:|$|\b(?:Tablets?|Capsules?|Injection|Nasal|Oral|Solution|Gel)\b))");
            if (productMatch.Success)
                return productMatch.Groups[1].Value.Trim();

            return firstSentence;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a title-derived candidate is specific enough for an arm name.
        /// </summary>
        /// <param name="candidate">Title-derived product candidate.</param>
        /// <returns>True when the candidate is safe to use.</returns>
        private static bool IsProductTitleCandidate(string candidate)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            var trimmed = candidate.Trim();
            if (trimmed.Length > 80)
                return false;

            if (Regex.IsMatch(trimmed,
                    @"^(?:Test\s+Drug|Drug|Treatment|Placebo|These\s+Highlights|Highlights\s+of\s+Prescribing\s+Information|Full\s+Prescribing\s+Information)\b",
                    RegexOptions.IgnoreCase))
            {
                return false;
            }

            return Regex.IsMatch(trimmed, @"^[A-Za-z][A-Za-z0-9'\-]*(?:\s+[A-Za-z0-9'\-]+){0,4}$");

            #endregion
        }
    }
}
