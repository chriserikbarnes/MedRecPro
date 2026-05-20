using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.SampleSize;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Enriches arm definitions from leading body rows that carry metadata instead of observations.
    /// </summary>
    /// <remarks>
    /// SPL tables often put dose regimens, arm names, sample sizes, or format hints in
    /// the first body rows. This helper owns that body-row enrichment pass.
    /// </remarks>
    /// <seealso cref="ArmDefinition"/>
    /// <seealso cref="BaseTableParser"/>
    internal static class ArmMetadataEnrichmentService
    {
        #region Compiled Patterns

        private static readonly Regex _doseRegimenPattern = new(
            @"^\d+\s*(?:mg|mcg|\u00B5g|g|ml|mL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _formatHintCellPattern = new(
            @"^(?:n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Patterns

        /**************************************************************/
        /// <summary>
        /// Scans leading body rows and enriches arm definitions from metadata rows.
        /// </summary>
        /// <param name="dataRows">Filtered table body rows.</param>
        /// <param name="arms">Arm definitions to update in place.</param>
        /// <returns>Number of leading enrichment rows consumed.</returns>
        /// <seealso cref="ArmDefinition"/>
        internal static int EnrichArmsFromBodyRows(List<ReconstructedRow> dataRows, List<ArmDefinition> arms)
        {
            #region implementation

            int enrichmentCount = 0;
            var consumed = new HashSet<string>();
            var limit = Math.Min(dataRows.Count, 5);

            for (int r = 0; r < limit; r++)
            {
                var row = dataRows[r];
                if (row.Classification == RowClassification.SocDivider)
                    break;

                var rowType = ClassifyEnrichmentRow(row, arms);
                if (rowType == null || consumed.Contains(rowType))
                    break;

                consumed.Add(rowType);
                enrichmentCount++;
                ApplyEnrichmentRow(row, arms, rowType);
            }

            return enrichmentCount;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a body row as arm metadata when most cells match one metadata shape.
        /// </summary>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Current arm definitions.</param>
        /// <returns>Metadata row type, or null for observation rows.</returns>
        private static string? ClassifyEnrichmentRow(ReconstructedRow row, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0)
                return null;

            var rowLabel = GetCellAtColumn(row, 0)?.CleanedText;
            if (!LooksLikeMetadataRowLabel(rowLabel))
                return null;

            var allowArmNameEnrichment = LooksLikeArmNameMetadataRowLabel(rowLabel) ||
                arms.Any(a => !ArmDefinitionExtractor.HasUsableTreatmentArm(a) || LooksLikeStudyIdentifier(a.Name));

            int armNameCount = 0, doseCount = 0, nCount = 0, fmtCount = 0, cellCount = 0;
            var previousText = (string?)null;

            foreach (var arm in arms)
            {
                var cell = GetCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                cellCount++;
                var text = cell.CleanedText.Trim();

                if (IsDittoCellText(text))
                {
                    if (!string.IsNullOrWhiteSpace(previousText))
                    {
                        if (LooksLikeArmNameCell(previousText)) armNameCount++;
                        else if (_doseRegimenPattern.IsMatch(previousText)) doseCount++;
                        else if (SampleSizeParser.TryParseStandaloneSampleSizeCell(previousText, out _)) nCount++;
                        else if (LooksLikeFormatAxisCell(previousText)) fmtCount++;
                    }
                    continue;
                }

                previousText = text;

                if (LooksLikeArmNameCell(text)) armNameCount++;
                else if (_doseRegimenPattern.IsMatch(text)) doseCount++;
                else if (SampleSizeParser.TryParseStandaloneSampleSizeCell(text, out _)) nCount++;
                else if (LooksLikeFormatAxisCell(text)) fmtCount++;
            }

            if (cellCount == 0)
                return null;

            if (allowArmNameEnrichment && armNameCount * 2 >= cellCount) return "arm_name";
            if (doseCount * 2 >= cellCount) return "dose";
            if (nCount * 2 >= cellCount) return "n_equals";
            if (fmtCount * 2 >= cellCount) return "format_hint";

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether column zero names a leading metadata row.
        /// </summary>
        /// <param name="text">Column zero text.</param>
        /// <returns>True when the row can safely enrich arm metadata.</returns>
        private static bool LooksLikeMetadataRowLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return true;

            var trimmed = text.Trim();
            if (trimmed is "-" or "--")
                return true;

            return Regex.IsMatch(trimmed,
                @"^(?:Col\s*0|Adverse\s+(?:Reaction|Reactions|Event|Events)(?:\s*\(.*\))?|Body\s+System(?:\s*\(.*\))?|System\s+Organ\s+Class|Preferred\s+Term|Treatment\s+Arm|Arm|Group|Study\s+Drug)\s*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether column zero explicitly names treatment-arm metadata.
        /// </summary>
        /// <param name="text">Column zero text.</param>
        /// <returns>True when body cells should be interpreted as arm names.</returns>
        private static bool LooksLikeArmNameMetadataRowLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text.Trim(),
                @"^(?:Treatment\s+Arm|Arm|Group|Study\s+Drug)\s*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a recovered header looks like a compact study identifier.
        /// </summary>
        /// <param name="text">Candidate header text.</param>
        /// <returns>True for study labels such as TAX323 or TMC114-C230.</returns>
        private static bool LooksLikeStudyIdentifier(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text.Trim(),
                @"^(?:[A-Z]{2,}[-\s]?)?\d{2,}[A-Z0-9/-]*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a cell is a ditto marker that inherits prior row metadata.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns>True when the cell means same as previous.</returns>
        private static bool IsDittoCellText(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            return trimmed is "\u2194" or "\u2192" or "\u2190" or "\u27F7" or "\u27F6" or "<->" or "->" or "<-";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a body-row cell looks like a treatment-arm name.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns>True when the cell can supply an arm name.</returns>
        private static bool LooksLikeArmNameCell(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (IsDittoCellText(trimmed) ||
                SampleSizeParser.TryParseStandaloneSampleSizeCell(trimmed, out _) ||
                LooksLikeFormatAxisCell(trimmed) ||
                _doseRegimenPattern.IsMatch(trimmed) ||
                ArmDefinitionExtractor.LooksLikeGenericArmLabel(trimmed))
            {
                return false;
            }

            if (Regex.IsMatch(trimmed, @"^[<>=]?\s*\d+(?:\.\d+)?\s*(?:%|\([^)]*\))?$"))
                return false;

            return Regex.IsMatch(trimmed, @"[A-Za-z]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a cell contains a value format or axis hint.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns>True for n(%), percent, or severity-axis cells.</returns>
        private static bool LooksLikeFormatAxisCell(string? text)
        {
            #region implementation

            return _formatHintCellPattern.IsMatch(text ?? string.Empty) ||
                   ArmDefinitionExtractor.TryExtractAxisMetadata(text, out _, out _);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies one classified enrichment row to all matching arm definitions.
        /// </summary>
        /// <param name="row">Source enrichment row.</param>
        /// <param name="arms">Arm definitions to update.</param>
        /// <param name="rowType">Metadata row type.</param>
        private static void ApplyEnrichmentRow(
            ReconstructedRow row, List<ArmDefinition> arms, string rowType)
        {
            #region implementation

            string? previousArmName = null;
            string? previousDoseRegimen = null;
            int? previousN = null;
            string? previousFormatHint = null;
            string? previousSubtype = null;

            for (int i = 0; i < arms.Count; i++)
            {
                var cell = GetCellAtColumn(row, arms[i].ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();

                switch (rowType)
                {
                    case "arm_name":
                        if (IsDittoCellText(text))
                            text = previousArmName;
                        else
                            previousArmName = text;

                        ApplyArmNameMetadata(arms[i], text);
                        break;

                    case "dose":
                        if (IsDittoCellText(text))
                            text = previousDoseRegimen;
                        else
                            previousDoseRegimen = text;

                        if (string.IsNullOrWhiteSpace(text))
                            break;

                        arms[i].DoseRegimen = text;
                        var (dose, doseUnit) = DoseExtractor.Extract(text);
                        arms[i].Dose = dose;
                        arms[i].DoseUnit = doseUnit;
                        break;

                    case "n_equals":
                        if (IsDittoCellText(text))
                        {
                            if (previousN.HasValue)
                                arms[i].SampleSize = previousN;
                            break;
                        }

                        if (SampleSizeParser.TryParseStandaloneSampleSizeCell(text, out var evidence) &&
                            evidence.Value is > 0)
                        {
                            arms[i].SampleSize = evidence.Value.Value;
                            previousN = evidence.Value.Value;
                        }
                        break;

                    case "format_hint":
                        if (IsDittoCellText(text))
                        {
                            if (!string.IsNullOrWhiteSpace(previousFormatHint))
                                arms[i].FormatHint = previousFormatHint;
                            if (!string.IsNullOrWhiteSpace(previousSubtype))
                                arms[i].ParameterSubtype = previousSubtype;
                            break;
                        }

                        ArmDefinitionExtractor.ApplyAxisMetadata(text, arms[i]);
                        if (_formatHintCellPattern.IsMatch(text))
                            arms[i].FormatHint = text;
                        previousFormatHint = arms[i].FormatHint;
                        previousSubtype = arms[i].ParameterSubtype;
                        break;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies body-row arm-name text to an arm definition.
        /// </summary>
        /// <param name="arm">Arm definition to update.</param>
        /// <param name="text">Recovered arm-name text.</param>
        /// <seealso cref="ValueParser.ParseArmHeader"/>
        private static void ApplyArmNameMetadata(ArmDefinition arm, string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text) || !LooksLikeArmNameCell(text))
                return;

            var parsedArm = ValueParser.ParseArmHeader(text);
            var recoveredName = parsedArm?.Name ?? text.Trim();

            if (!string.IsNullOrWhiteSpace(arm.Name) &&
                !ArmDefinitionExtractor.LooksLikeGenericArmLabel(arm.Name) &&
                !string.Equals(arm.Name, recoveredName, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(arm.StudyContext))
            {
                arm.StudyContext = arm.Name;
            }

            arm.Name = recoveredName;
            if (parsedArm?.SampleSize != null)
                arm.SampleSize = parsedArm.SampleSize;
            if (!string.IsNullOrWhiteSpace(parsedArm?.FormatHint))
                arm.FormatHint = parsedArm.FormatHint;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds the cell in a row that covers the given resolved column index.
        /// </summary>
        /// <param name="row">Row to search.</param>
        /// <param name="columnIndex">Resolved column index.</param>
        /// <returns>The covering cell, or null.</returns>
        private static ProcessedCell? GetCellAtColumn(ReconstructedRow row, int columnIndex)
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
}
