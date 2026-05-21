using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Classifies adverse-event table columns into treatment-arm, value-axis,
    /// paired-subcolumn, structural, or unresolved contexts.
    /// </summary>
    /// <remarks>
    /// AE tables frequently encode the real arm in a spanning parent header while
    /// the leaf header names a measurement axis such as incidence, discontinuation,
    /// count, or percent. This helper centralizes those decisions so parser-layer
    /// emission and Stage 5 denormalization use the same invalid-arm vocabulary.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="HeaderColumn"/>
    internal static class AeColumnContextResolver
    {
        #region Patterns

        /**************************************************************/
        /// <summary>Exact non-arm leaf/header tokens used by AE paired columns.</summary>
        private static readonly Regex _exactValueAxisTokenPattern = new(
            @"^\s*(?:" +
              @"Incidence(?:\s*\([^)]*\))?|" +
              @"Discontinuation(?:\s*\([^)]*\))?|" +
              @"Adverse\s+Events?\s+Leading\s+to\s+Discontinuation|" +
              @"Events?|" +
              @"Body\s+System(?:\s*[\(/\-].*)?|" +
              @"System\s+Organ\s+Class|SOC|" +
              @"MedDRA(?:\s+(?:Term|Preferred\s+Term))?|" +
              @"Preferred\s+Term|Term|" +
              @"Adverse\s+(?:Reactions?|Events?|Experiences?)|" +
              @"Reactions?|" +
              @"Comparison|" +
              @"Outcomes?|" +
              @"Variables?|" +
              @"Parameters?|" +
              @"Percent|Percentage|%|" +
              @"n\s*\(\s*%\s*\)|" +
              @"[nN]|" +
              @"Number|No\.?|Count|Frequency" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Severity and measure-axis labels that require arm inheritance.</summary>
        private static readonly Regex _valueAxisLabelPattern = new(
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

        /**************************************************************/
        /// <summary>Caption-like text that must not become a treatment arm.</summary>
        private static readonly Regex _captionLikePattern = new(
            @"^\s*(?:" +
              @"Table\s+[\w\d\-]+\s*[:.\-].*|" +
              @".*\b(?:Incidence|Frequency|Percent|Percentage)\s+(?:\(\s*%\s*\)\s+)?of\s+.*Adverse\s+(?:Reactions?|Events?|Experiences?).*" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>MedDRA SOC/body-system labels that are context, not arms.</summary>
        private static readonly Regex _bodySystemLabelPattern = new(
            @"^\s*(?:" +
              @"Ocular|Cardiovascular|Hepatic|" +
              @"Body\s+as\s+a\s+Whole|" +
              @"Cardiac|Congenital|Ear|Endocrine|Eye|Gastrointestinal|General|" +
              @"Hepatobiliary|Immune\s+system|Infections\s+and\s+infestations|" +
              @"Injury,\s*poisoning\s+and\s+procedural\s+complications|Investigations|" +
              @"Metabolism\s+and\s+nutrition|Musculoskeletal\s+and\s+connective\s+tissue|" +
              @"Neoplasms\s+benign,\s*malignant\s+and\s+unspecified|Nervous\s+system|" +
              @"Pregnancy,\s*puerperium\s+and\s+perinatal|Psychiatric|Renal\s+and\s+urinary|" +
              @"Reproductive\s+system\s+and\s+breast|Respiratory,\s*thoracic\s+and\s+mediastinal|" +
              @"Skin\s+and\s+subcutaneous\s+tissue|Social\s+circumstances|" +
              @"Surgical\s+and\s+medical\s+procedures|Vascular" +
            @")(?:\s+disorders)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Composite arm-plus-axis headers, such as "Placebo Incidence".</summary>
        private static readonly Regex _compositeArmAxisPattern = new(
            @"^\s*(?<arm>.+?)\s+(?<axis>Incidence(?:\s*\([^)]*\))?|Discontinuation(?:\s*\([^)]*\))?|Adverse\s+Events?\s+Leading\s+to\s+Discontinuation)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Percent/count format leaves that affect value interpretation.</summary>
        private static readonly Regex _formatAxisPattern = new(
            @"^\s*(?:n\s*\(\s*%\s*\)|%|Percent(?:age)?|Number|No\.?|Count)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Patterns

        #region Context Resolution

        /**************************************************************/
        /// <summary>
        /// Resolves the AE column context for one header column.
        /// </summary>
        /// <param name="column">Resolved header column to classify.</param>
        /// <param name="columns">All resolved header columns in display order.</param>
        /// <param name="columnOffset">Offset of <paramref name="column"/> in <paramref name="columns"/>.</param>
        /// <returns>Resolved column context for parser emission.</returns>
        /// <seealso cref="AeColumnContext"/>
        /// <seealso cref="AeColumnContextKind"/>
        internal static AeColumnContext Resolve(
            HeaderColumn column,
            IReadOnlyList<HeaderColumn> columns,
            int columnOffset)
        {
            #region implementation

            // Normalize the leaf text once so every branch evaluates the same header value.
            var leaf = clean(column.LeafHeaderText);

            // Preserve leaf-axis meaning before arm repair so inherited columns keep their subtype intent.
            TryExtractAxisMetadata(leaf, out var subtype, out var formatHint);

            // Prefer direct leaf/parent recovery so the nearest header-path arm is selected first.
            var armCandidate = recoverHeaderArmCandidate(column, out var inheritedFromParent);

            // Track sibling repair separately so the returned context can explain lateral inheritance.
            var inheritedFromSibling = false;

            // Use sibling repair only when this column cannot resolve a safe arm by itself.
            if (armCandidate == null)
            {
                armCandidate = recoverSiblingArmCandidate(columns, columnOffset, out inheritedFromSibling);
            }

            // Split composite labels so "Placebo Incidence" yields arm "Placebo" and subtype "Incidence".
            if (TrySplitCompositeArmAxis(armCandidate, out var splitArm, out var splitSubtype, out var splitFormatHint))
            {
                armCandidate = splitArm;
                subtype ??= splitSubtype;
                formatHint ??= splitFormatHint;
            }

            // Reject caption, SOC/body-system, and value-axis text before it can become TreatmentArm.
            if (IsInvalidTreatmentArm(armCandidate))
            {
                return new AeColumnContext
                {
                    ColumnIndex = column.ColumnIndex,
                    Kind = classifyInvalidHeader(leaf),
                    TreatmentArm = null,
                    ParameterSubtype = subtype ?? (IsBodySystemLabel(leaf) || IsCaptionLikeText(leaf) ? leaf : null),
                    FormatHint = formatHint,
                    StudyContext = getContextAxisLabel(leaf),
                    SourceHeaderText = leaf,
                    Reason = classifyInvalidReason(leaf)
                };
            }

            // Mark paired-subcolumn contexts when the arm came from a parent/sibling or the leaf is an axis.
            var inherited = inheritedFromParent || inheritedFromSibling || IsValueAxisToken(leaf);
            return new AeColumnContext
            {
                ColumnIndex = column.ColumnIndex,
                Kind = inherited ? AeColumnContextKind.PairedSubcolumn : AeColumnContextKind.TreatmentArm,
                TreatmentArm = armCandidate,
                ParameterSubtype = subtype,
                FormatHint = formatHint,
                StudyContext = getStudyContext(column, armCandidate, leaf),
                SourceHeaderText = leaf,
                Reason = inherited ? "Inherited parent treatment arm for paired AE subcolumn" : null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a final treatment-arm candidate is invalid.
        /// </summary>
        /// <param name="text">Treatment-arm candidate.</param>
        /// <returns><c>true</c> for null, caption, body-system, or value-axis text.</returns>
        internal static bool IsInvalidTreatmentArm(string? text)
        {
            #region implementation

            // Treat blank candidates as invalid so callers do not emit null or whitespace arms.
            if (string.IsNullOrWhiteSpace(text))
                return true;

            // Trim once so caption, SOC, and value-axis classifiers inspect identical text.
            var trimmed = text.Trim();

            // Reject structural text so only true arm labels survive into parser output.
            return IsCaptionLikeText(trimmed) ||
                   IsBodySystemLabel(trimmed) ||
                   IsValueAxisToken(trimmed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a SOC/body-system label.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <returns><c>true</c> when the text is body-system context.</returns>
        internal static bool IsBodySystemLabel(string? text)
        {
            #region implementation

            // Match SOC/body-system labels so they can be preserved as context, never as arms.
            return !string.IsNullOrWhiteSpace(text) &&
                   _bodySystemLabelPattern.IsMatch(text.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text looks like a table caption instead of an arm.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <returns><c>true</c> when the text should be rejected as caption leakage.</returns>
        internal static bool IsCaptionLikeText(string? text)
        {
            #region implementation

            // Match caption-shaped text so table titles cannot leak into TreatmentArm.
            return !string.IsNullOrWhiteSpace(text) &&
                   _captionLikePattern.IsMatch(text.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a value/header axis token rather than an arm.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <returns><c>true</c> when the text is a non-arm header axis.</returns>
        internal static bool IsValueAxisToken(string? text)
        {
            #region implementation

            // Match exact and severity/measure axis labels so leaf meanings inherit real parent arms.
            return !string.IsNullOrWhiteSpace(text) &&
                   (_exactValueAxisTokenPattern.IsMatch(text.Trim()) ||
                    _valueAxisLabelPattern.IsMatch(text.Trim()));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an AE name is a threshold/range fragment or structural
        /// visualization row rather than an adverse-event term.
        /// </summary>
        /// <param name="text">Candidate ParameterName text.</param>
        /// <returns><c>true</c> when the row should be excluded before Stage 5 grouping.</returns>
        /// <seealso cref="AdverseEventTableFlattening.AeMeddraTermStandardizer"/>
        internal static bool IsThresholdOnlyOrExcludedAeName(string? text)
        {
            #region implementation

            return AdverseEventTableFlattening.AeMeddraTermStandardizer.IsExcludedFromVisualization(text);

            #endregion
        }

        #endregion Context Resolution

        #region Metadata Extraction

        /**************************************************************/
        /// <summary>
        /// Splits a composite arm/axis header into the arm and the leaf meaning.
        /// </summary>
        /// <param name="text">Header text, such as <c>Placebo Incidence</c>.</param>
        /// <param name="arm">Recovered treatment arm.</param>
        /// <param name="subtype">Recovered parameter subtype.</param>
        /// <param name="formatHint">Recovered format hint, when present.</param>
        /// <returns><c>true</c> when a composite header was split.</returns>
        internal static bool TrySplitCompositeArmAxis(
            string? text,
            out string? arm,
            out string? subtype,
            out string? formatHint)
        {
            #region implementation

            // Reset outputs first so every false return leaves a clean "no split" result.
            arm = null;
            subtype = null;
            formatHint = null;

            // Blank text cannot encode either an arm or an axis, so no split is possible.
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Detect only known arm-plus-axis forms so ordinary multi-word arm names remain intact.
            var match = _compositeArmAxisPattern.Match(text.Trim());

            // Leave the header unchanged when it does not match the composite arm-axis pattern.
            if (!match.Success)
                return false;

            // Extract the possible arm so it can be validated before callers trust it.
            var armCandidate = match.Groups["arm"].Value.Trim();

            // Extract the leaf axis so its incidence/discontinuation meaning can become subtype metadata.
            var axis = match.Groups["axis"].Value.Trim();

            // Refuse splits whose left side is still structural text, preserving suppression behavior.
            if (IsInvalidTreatmentArm(armCandidate))
                return false;

            // Commit the validated arm and derive leaf metadata for the emitted observation.
            arm = armCandidate;
            TryExtractAxisMetadata(axis, out subtype, out formatHint);
            subtype ??= axis;
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts AE paired-column subtype and format metadata from a header leaf.
        /// </summary>
        /// <param name="text">Header leaf or axis label.</param>
        /// <param name="subtype">Parameter subtype to preserve.</param>
        /// <param name="formatHint">Value format hint to preserve.</param>
        /// <returns><c>true</c> when metadata was found.</returns>
        internal static bool TryExtractAxisMetadata(
            string? text,
            out string? subtype,
            out string? formatHint)
        {
            #region implementation

            // Reset outputs first so non-axis text cannot leak stale subtype or format metadata.
            subtype = null;
            formatHint = null;

            // Blank text has no axis meaning, so callers should continue without metadata hints.
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Normalize the candidate so regex and string checks share one display value.
            var trimmed = text.Trim();

            // Identify incidence/discontinuation leaves that should become explicit ParameterSubtype values.
            var incidenceAxis = Regex.IsMatch(trimmed,
                @"^(?:Incidence|Discontinuation|Adverse\s+Events?\s+Leading\s+to\s+Discontinuation)\b",
                RegexOptions.IgnoreCase);

            // Identify pure count/percent leaves that guide value interpretation but are not arms.
            var formatAxis = _formatAxisPattern.IsMatch(trimmed);

            // Identify severity/measure leaves that should inherit an arm while preserving their label.
            var valueAxis = _valueAxisLabelPattern.IsMatch(trimmed);

            // Ignore ordinary arm text, including product names that happen to contain percent symbols.
            if (!incidenceAxis && !formatAxis && !valueAxis)
                return false;

            // Convert percent-bearing axis labels into the canonical percent format hint.
            if (trimmed.Contains('%') ||
                trimmed.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("percentage", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "%";
            }
            // Convert count-like axis labels into the paired count/percent format hint.
            else if (trimmed.Contains("n(", StringComparison.OrdinalIgnoreCase) ||
                     Regex.IsMatch(trimmed, @"^[nN]$", RegexOptions.IgnoreCase) ||
                     trimmed.Contains("number", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("No.", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "n(%)";
            }

            // Keep incidence/discontinuation wording intact so paired leaves remain distinguishable.
            if (incidenceAxis)
            {
                subtype = trimmed;
            }
            // Pure format leaves only guide parsing; they should not become ParameterSubtype.
            else if (formatAxis)
            {
                subtype = null;
            }
            // Severity/value leaves become cleaned ParameterSubtype labels after unit suffix removal.
            else if (valueAxis)
            {
                // Remove display units so subtype stores the clinical axis, not the value format.
                var clean = Regex.Replace(trimmed, @"\s*(?:\(\s*%\s*\)|%)\s*$", "", RegexOptions.IgnoreCase).Trim();

                // Accept only recognized severity/value labels so non-axis text is not mislabeled.
                if (Regex.IsMatch(clean,
                        @"^(?:Any|All\s+(?:CTC\s+)?Grades?|(?:CTC|CTCAE|NCI)\s+Grades?|Grades?|Grade|(?:>=|>|\u2265)\s*Grade|Toxicity\s+Grade|Severity\s+Grade)\b",
                        RegexOptions.IgnoreCase))
                {
                    subtype = clean;
                }
            }

            // Report success only when this method found subtype or format metadata.
            return subtype != null || formatHint != null;

            #endregion
        }

        #endregion Metadata Extraction

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Recovers a candidate arm from the column's leaf or parent header path.
        /// </summary>
        /// <param name="column">Header column to inspect.</param>
        /// <param name="inheritedFromParent">Whether the selected candidate came from a parent.</param>
        /// <returns>Candidate arm text, or <c>null</c>.</returns>
        private static string? recoverHeaderArmCandidate(HeaderColumn column, out bool inheritedFromParent)
        {
            #region implementation

            // Start as not inherited so direct leaf recovery is not misreported as parent repair.
            inheritedFromParent = false;

            // Normalize the leaf before deciding whether it is already a safe treatment arm.
            var leaf = clean(column.LeafHeaderText);

            // Use a valid leaf directly so ordinary single-row headers do not need fallback repair.
            if (!IsInvalidTreatmentArm(leaf))
                return leaf;

            // Without a header path there is no parent label available to rescue this structural leaf.
            if (column.HeaderPath == null)
                return null;

            // Walk from nearest parent upward so the closest valid arm wins over broader context headers.
            for (int i = column.HeaderPath.Count - 2; i >= 0; i--)
            {
                // Normalize each parent before applying the same invalid-arm rejection rules.
                var candidate = clean(column.HeaderPath[i]);

                // Return the first valid parent and record that the arm was inherited from the header path.
                if (!IsInvalidTreatmentArm(candidate))
                {
                    inheritedFromParent = true;
                    return candidate;
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recovers an arm from an adjacent sibling under the same parent header path.
        /// </summary>
        /// <param name="columns">All resolved header columns.</param>
        /// <param name="columnOffset">Current column offset.</param>
        /// <param name="inheritedFromSibling">Whether a sibling supplied the candidate.</param>
        /// <returns>Sibling arm text, or <c>null</c>.</returns>
        private static string? recoverSiblingArmCandidate(
            IReadOnlyList<HeaderColumn> columns,
            int columnOffset,
            out bool inheritedFromSibling)
        {
            #region implementation

            // Start as not inherited so false returns do not imply a sibling repair happened.
            inheritedFromSibling = false;

            // Guard the index before reading the source column so malformed offsets fail closed.
            if (columnOffset < 0 || columnOffset >= columns.Count)
                return null;

            // Capture the unresolved source column so sibling scans stay within the same parent group.
            var source = columns[columnOffset];

            // Scan left first so repeated paired leaves inherit the nearest prior arm under the same parent.
            for (int i = columnOffset - 1; i >= 1; i--)
            {
                // Stop at parent boundaries so arms from neighboring groups cannot bleed into this column.
                if (!hasSameHeaderParent(source, columns[i]))
                    break;

                // Reuse header recovery so siblings can contribute direct leaves or their own parent arms.
                var candidate = recoverHeaderArmCandidate(columns[i], out _);

                // Accept the first safe sibling candidate and record the lateral inheritance reason.
                if (!IsInvalidTreatmentArm(candidate))
                {
                    inheritedFromSibling = true;
                    return candidate;
                }
            }

            // Scan right as a fallback for layouts where the valid arm appears after a structural leaf.
            for (int i = columnOffset + 1; i < columns.Count; i++)
            {
                // Stop at parent boundaries so the repair cannot cross into another treatment group.
                if (!hasSameHeaderParent(source, columns[i]))
                    break;

                // Reuse header recovery so the sibling decision stays consistent with direct resolution.
                var candidate = recoverHeaderArmCandidate(columns[i], out _);

                // Accept the first safe right-side sibling and record the lateral inheritance reason.
                if (!IsInvalidTreatmentArm(candidate))
                {
                    inheritedFromSibling = true;
                    return candidate;
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when two columns share the same non-leaf header path.
        /// </summary>
        /// <param name="left">First header column.</param>
        /// <param name="right">Second header column.</param>
        /// <returns><c>true</c> when the columns are siblings.</returns>
        private static bool hasSameHeaderParent(HeaderColumn left, HeaderColumn right)
        {
            #region implementation

            // Compare parent keys so only columns from the same header group are treated as siblings.
            return string.Equals(getHeaderParentKey(left), getHeaderParentKey(right), StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a stable key from non-leaf header path entries.
        /// </summary>
        /// <param name="column">Header column to inspect.</param>
        /// <returns>Parent key, or an empty string.</returns>
        private static string getHeaderParentKey(HeaderColumn column)
        {
            #region implementation

            // Columns with no parent path cannot have a sibling group key, so use an empty sentinel.
            if (column.HeaderPath == null || column.HeaderPath.Count <= 1)
                return string.Empty;

            // Join non-leaf path parts with a delimiter unlikely to occur in real header text.
            return string.Join("\u001F", column.HeaderPath.Take(column.HeaderPath.Count - 1)
                .Select(p => clean(p) ?? string.Empty));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves study/context text for a recovered arm.
        /// </summary>
        /// <param name="column">Header column.</param>
        /// <param name="armCandidate">Recovered treatment arm.</param>
        /// <param name="leaf">Leaf header text.</param>
        /// <returns>Study context candidate, or <c>null</c>.</returns>
        private static string? getStudyContext(HeaderColumn column, string? armCandidate, string? leaf)
        {
            #region implementation

            // Prefer the first parent as study context when it is distinct from the recovered arm.
            if (column.HeaderPath != null && column.HeaderPath.Count > 1)
            {
                // Normalize the parent before checking whether it is safe to preserve as StudyContext.
                var candidate = clean(column.HeaderPath[0]);

                // Preserve valid study/context headers without allowing them to become arm replacements.
                if (!string.Equals(candidate, armCandidate, StringComparison.OrdinalIgnoreCase) &&
                    !IsInvalidTreatmentArm(candidate))
                {
                    return candidate;
                }
            }

            // Fall back to known non-arm context axes when no valid parent study label exists.
            return getContextAxisLabel(leaf);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Preserves non-arm context-axis labels for product-title fallback.
        /// </summary>
        /// <param name="text">Header text.</param>
        /// <returns>Context label, or <c>null</c>.</returns>
        private static string? getContextAxisLabel(string? text)
        {
            #region implementation

            // Blank text carries no context axis and should not affect product-title fallback.
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Normalize once so context-axis matching uses the display text without outer spaces.
            var trimmed = text.Trim();

            // Preserve only known context axes so random structural text remains suppressible.
            if (Regex.IsMatch(trimmed,
                    @"^(?:\d+\s*[-\u2013]\s*day\s+Treatment|Study\s+\d+[A-Z]?|Treatment\s+Regimen|Overall)$",
                    RegexOptions.IgnoreCase))
            {
                return trimmed;
            }

            // Return null when the text is not useful non-arm context.
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies an invalid header as structural or unresolved.
        /// </summary>
        /// <param name="leaf">Leaf header text.</param>
        /// <returns>Column-context kind.</returns>
        private static AeColumnContextKind classifyInvalidHeader(string? leaf)
        {
            #region implementation

            // Structural labels are suppressible context rather than unresolved parser failures.
            if (IsCaptionLikeText(leaf) || IsBodySystemLabel(leaf) || IsValueAxisToken(leaf))
                return AeColumnContextKind.Structural;

            // Anything else without a safe arm remains unresolved for later diagnostics.
            return AeColumnContextKind.Unresolved;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the stable suppression flag for an invalid header.
        /// </summary>
        /// <param name="leaf">Leaf header text.</param>
        /// <returns>Suppression flag.</returns>
        private static string classifyInvalidReason(string? leaf)
        {
            #region implementation

            // Surface caption leakage with a stable reason so audits can count this failure mode.
            if (IsCaptionLikeText(leaf))
                return AeSuppressionKind.CaptionArm.ToValidationFlag();

            // Surface SOC/body-system leakage separately from generic unresolved-arm failures.
            if (IsBodySystemLabel(leaf))
                return AeSuppressionKind.BodySystemArm.ToValidationFlag();

            // Default to unresolved-arm when no more specific structural leak is identified.
            return AeSuppressionKind.UnresolvedArm.ToValidationFlag();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Trims a nullable string and converts blanks to null.
        /// </summary>
        /// <param name="text">Text to clean.</param>
        /// <returns>Trimmed text, or <c>null</c>.</returns>
        private static string? clean(string? text)
        {
            #region implementation

            // Collapse blank strings to null so every caller shares the same missing-text representation.
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

            #endregion
        }

        #endregion Private Helpers
    }

    /**************************************************************/
    /// <summary>
    /// Column-classification kinds used by the AE column-context resolver.
    /// </summary>
    /// <seealso cref="AeColumnContextResolver"/>
    internal enum AeColumnContextKind
    {
        /**************************************************************/
        /// <summary>Column directly identifies a treatment arm.</summary>
        TreatmentArm,

        /**************************************************************/
        /// <summary>Column describes a value axis and requires parent-arm inheritance.</summary>
        ValueAxis,

        /**************************************************************/
        /// <summary>Column is a paired subcolumn under a recovered treatment arm.</summary>
        PairedSubcolumn,

        /**************************************************************/
        /// <summary>Column is structural context and must not emit observations as an arm.</summary>
        Structural,

        /**************************************************************/
        /// <summary>Column could not be resolved to a safe treatment arm.</summary>
        Unresolved
    }

    /**************************************************************/
    /// <summary>
    /// Stable AE suppression kinds used by parser diagnostics and Stage 5 filters.
    /// </summary>
    /// <seealso cref="TableSuppressionAuditRecord"/>
    internal enum AeSuppressionKind
    {
        /**************************************************************/
        /// <summary>Text-only AE row could not be rescued into an observation.</summary>
        UnrescuableTextRow,

        /**************************************************************/
        /// <summary>Caption text leaked into the treatment-arm slot.</summary>
        CaptionArm,

        /**************************************************************/
        /// <summary>SOC/body-system text leaked into the treatment-arm slot.</summary>
        BodySystemArm,

        /**************************************************************/
        /// <summary>No valid treatment arm could be resolved.</summary>
        UnresolvedArm
    }

    /**************************************************************/
    /// <summary>
    /// Extension methods for AE suppression-kind diagnostics.
    /// </summary>
    /// <seealso cref="AeSuppressionKind"/>
    internal static class AeSuppressionKindExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Converts an AE suppression kind to its stable diagnostic flag.
        /// </summary>
        /// <param name="kind">Suppression kind.</param>
        /// <returns>Stable suppression flag.</returns>
        internal static string ToValidationFlag(this AeSuppressionKind kind)
        {
            #region implementation

            // Map each internal suppression kind to the external diagnostic flag consumed by audits/tests.
            return kind switch
            {
                AeSuppressionKind.UnrescuableTextRow => "SUPPRESSED_AE_UNRESCUABLE_TEXT_ROW",
                AeSuppressionKind.CaptionArm => "SUPPRESSED_AE_CAPTION_ARM",
                AeSuppressionKind.BodySystemArm => "SUPPRESSED_AE_BODY_SYSTEM_ARM",
                AeSuppressionKind.UnresolvedArm => "SUPPRESSED_AE_UNRESOLVED_ARM",
                _ => "SUPPRESSED_STRUCTURAL_ROW"
            };

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Resolved AE column context used to build parser arm definitions.
    /// </summary>
    /// <seealso cref="AeColumnContextKind"/>
    /// <seealso cref="AeColumnContextResolver"/>
    internal sealed record AeColumnContext
    {
        /**************************************************************/
        /// <summary>Resolved source column index.</summary>
        public int? ColumnIndex { get; init; }

        /**************************************************************/
        /// <summary>Column classification kind.</summary>
        public AeColumnContextKind Kind { get; init; }

        /**************************************************************/
        /// <summary>Recovered treatment arm, when available.</summary>
        public string? TreatmentArm { get; init; }

        /**************************************************************/
        /// <summary>Leaf-axis meaning preserved as <c>ParameterSubtype</c>.</summary>
        public string? ParameterSubtype { get; init; }

        /**************************************************************/
        /// <summary>Value format hint such as <c>%</c> or <c>n(%)</c>.</summary>
        public string? FormatHint { get; init; }

        /**************************************************************/
        /// <summary>Study or context axis preserved outside <c>TreatmentArm</c>.</summary>
        public string? StudyContext { get; init; }

        /**************************************************************/
        /// <summary>Original header text that drove the context decision.</summary>
        public string? SourceHeaderText { get; init; }

        /**************************************************************/
        /// <summary>Diagnostic reason for inherited or rejected contexts.</summary>
        public string? Reason { get; init; }
    }
}
