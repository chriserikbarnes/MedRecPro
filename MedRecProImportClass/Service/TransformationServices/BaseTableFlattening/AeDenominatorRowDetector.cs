using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.SampleSize;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Detects adverse-event body rows that carry denominator metadata.
    /// </summary>
    /// <remarks>
    /// The detector centralizes row-level denominator recognition so the shared AE
    /// parser loop and the simple-arm AE loop do not grow parallel N-row policies.
    /// It reports expected "no evidence" and ambiguous evidence as result values,
    /// not exceptions.
    /// </remarks>
    /// <seealso cref="AeDenominatorRowDetection"/>
    /// <seealso cref="SampleSizeParser"/>
    internal static class AeDenominatorRowDetector
    {
        #region Compiled Patterns

        private static readonly Regex _tableLevelLabelPattern = new(
            @"^\s*(?:Total\s+patients\s+studied|Number\s+of\s+Patients|Patients?\s+studied)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _bareNLabelPattern = new(
            @"^\s*[Nn]\s*$",
            RegexOptions.Compiled);

        private static readonly Regex _subpopulationLabelPattern = new(
            @"^\s*(?:" +
              @"(?:.+\b(?:population|cohort)\b.*(?:,\s*)?[Nn]\b)|" +
              @"(?:(?:female|male|women|men)\s+patients?(?:\s+only)?|patients?\s+(?:female|male|women|men)(?:\s+only)?)|" +
              @"[Nn]\s*\(\s*(?:males?|females?|men|women)\s*\)" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _dittoCellPattern = new(
            @"^\s*(?:\u2194|\u2192|\u2190|\u27F7|\u27F6|<->|->|<-)\s*$",
            RegexOptions.Compiled);

        #endregion Compiled Patterns

        /**************************************************************/
        /// <summary>
        /// Detects denominator metadata in one AE body row.
        /// </summary>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <param name="rowLabel">Cleaned column-zero row label.</param>
        /// <param name="context">Current parser row-state context.</param>
        /// <returns>Structured detection outcome.</returns>
        /// <seealso cref="AeDenominatorRowContext"/>
        internal static AeDenominatorRowDetection Detect(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? rowLabel,
            AeDenominatorRowContext context)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(rowLabel))
                return AeDenominatorRowDetection.None();

            var label = rowLabel.Trim();
            var scope = classifyLabel(label, context);
            if (scope == AeDenominatorRowScope.None)
                return AeDenominatorRowDetection.None();

            if (StructuralRowSuppressionService.IsStructuralRowLabel(label) ||
                StructuralRowSuppressionService.IsAeHeaderEchoParameter(label))
            {
                return AeDenominatorRowDetection.None();
            }

            var usableArms = arms.Where(ArmDefinitionExtractor.HasUsableTreatmentArm).ToList();
            if (usableArms.Count == 0)
                return AeDenominatorRowDetection.None();

            if (StructuralRowSuppressionService.IsCombinedPopulationRowLabel(label, row, usableArms))
                return AeDenominatorRowDetection.None();

            var perColumnN = new Dictionary<int, int>();
            var parsedExactCount = 0;
            var denominatorLikeCount = 0;
            int? previousN = null;

            foreach (var arm in usableArms)
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();
                if (isDittoCell(text))
                {
                    if (previousN.HasValue && arm.ColumnIndex.HasValue)
                    {
                        perColumnN[arm.ColumnIndex.Value] = previousN.Value;
                        parsedExactCount++;
                        denominatorLikeCount++;
                    }

                    continue;
                }

                if (tryParseExactDenominator(text, out var n))
                {
                    if (arm.ColumnIndex.HasValue)
                        perColumnN[arm.ColumnIndex.Value] = n;
                    previousN = n;
                    parsedExactCount++;
                    denominatorLikeCount++;
                    continue;
                }

                if (SampleSizeParser.TryParseRangeOnlySampleSize(text, out _) ||
                    StructuralRowSuppressionService.IsStructuralValueCell(text))
                {
                    denominatorLikeCount++;
                    continue;
                }

                return AeDenominatorRowDetection.None();
            }

            if (parsedExactCount > 0)
            {
                return new AeDenominatorRowDetection(
                    scope,
                    scope == AeDenominatorRowScope.Subpopulation ? label : null,
                    perColumnN,
                    ArmNResolver.FromMetadataRowFlag,
                    "AE denominator metadata row captured before observation emission.");
            }

            if (denominatorLikeCount > 0)
            {
                return new AeDenominatorRowDetection(
                    AeDenominatorRowScope.Rejected,
                    null,
                    perColumnN,
                    SampleSizeParser.RangeOnlyDiagnostic,
                    "Denominator-like row had no exact sample size.");
            }

            return AeDenominatorRowDetection.None();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether column-zero text names a leading denominator row.
        /// </summary>
        /// <param name="text">Column-zero text.</param>
        /// <returns>True for labels such as <c>Total patients studied</c>.</returns>
        internal static bool IsLeadingMetadataRowLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            return _tableLevelLabelPattern.IsMatch(trimmed) ||
                   _bareNLabelPattern.IsMatch(trimmed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies the row label into a denominator lifetime.
        /// </summary>
        private static AeDenominatorRowScope classifyLabel(
            string label,
            AeDenominatorRowContext context)
        {
            #region implementation

            if (_subpopulationLabelPattern.IsMatch(label))
                return AeDenominatorRowScope.Subpopulation;

            if (_tableLevelLabelPattern.IsMatch(label))
                return context.FollowsResetBoundary || context.HasEmittedAeObservation
                    ? AeDenominatorRowScope.SectionLevel
                    : AeDenominatorRowScope.TableLevel;

            if (_bareNLabelPattern.IsMatch(label))
                return context.FollowsResetBoundary || context.HasEmittedAeObservation
                    ? AeDenominatorRowScope.SectionLevel
                    : AeDenominatorRowScope.TableLevel;

            return AeDenominatorRowScope.None;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses exact denominator evidence accepted by N-row context.
        /// </summary>
        private static bool tryParseExactDenominator(string text, out int value)
        {
            #region implementation

            value = 0;
            if (SampleSizeParser.TryParseStandaloneSampleSizeCell(text, out var standalone) &&
                standalone.Value is > 0)
            {
                value = standalone.Value.Value;
                return true;
            }

            if (SampleSizeParser.TryParseNRowDenominatorCell(text, out var nRow) &&
                nRow.Value is > 0)
            {
                value = nRow.Value.Value;
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a cell is a ditto marker.
        /// </summary>
        private static bool isDittoCell(string text)
        {
            #region implementation

            return _dittoCellPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds the cell in a row that covers the given resolved column index.
        /// </summary>
        private static ProcessedCell? getCellAtColumn(ReconstructedRow row, int columnIndex)
        {
            #region implementation

            if (row.Cells == null)
                return null;

            return row.Cells.FirstOrDefault(c =>
                c.ResolvedColumnStart != null &&
                c.ResolvedColumnEnd != null &&
                c.ResolvedColumnStart <= columnIndex &&
                c.ResolvedColumnEnd > columnIndex);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Parser row-state values used for AE denominator lifetime decisions.
    /// </summary>
    /// <param name="HasEmittedAeObservation">Whether any reportable AE row has already been emitted.</param>
    /// <param name="FollowsResetBoundary">Whether the current row follows a SOC/category reset.</param>
    /// <seealso cref="AeDenominatorRowDetector"/>
    internal readonly record struct AeDenominatorRowContext(
        bool HasEmittedAeObservation,
        bool FollowsResetBoundary);

    /**************************************************************/
    /// <summary>
    /// Result of AE denominator row detection.
    /// </summary>
    /// <param name="Scope">Detected denominator lifetime.</param>
    /// <param name="SubpopulationName">Subpopulation label when applicable.</param>
    /// <param name="PerColumnN">Exact sample sizes by resolved column index.</param>
    /// <param name="DiagnosticFlag">Stable validation flag for suppression diagnostics.</param>
    /// <param name="DiagnosticReason">Human-readable suppression reason.</param>
    /// <seealso cref="AeDenominatorRowDetector"/>
    internal sealed record AeDenominatorRowDetection(
        AeDenominatorRowScope Scope,
        string? SubpopulationName,
        IReadOnlyDictionary<int, int> PerColumnN,
        string? DiagnosticFlag,
        string? DiagnosticReason)
    {
        /**************************************************************/
        /// <summary>
        /// Creates an empty detection outcome.
        /// </summary>
        /// <returns>A no-evidence result.</returns>
        public static AeDenominatorRowDetection None()
        {
            #region implementation

            return new AeDenominatorRowDetection(
                AeDenominatorRowScope.None,
                null,
                new Dictionary<int, int>(),
                null,
                null);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Denominator lifetimes recognized in AE body rows.
    /// </summary>
    /// <seealso cref="AeDenominatorRowDetection"/>
    internal enum AeDenominatorRowScope
    {
        /**************************************************************/
        /// <summary>No denominator metadata was detected.</summary>
        None,

        /**************************************************************/
        /// <summary>Denominator applies across the whole table unless overridden.</summary>
        TableLevel,

        /**************************************************************/
        /// <summary>Denominator applies until the next section/category reset.</summary>
        SectionLevel,

        /**************************************************************/
        /// <summary>Denominator applies to a named subpopulation context.</summary>
        Subpopulation,

        /**************************************************************/
        /// <summary>Denominator-like row was rejected for exact ArmN assignment.</summary>
        Rejected
    }
}
