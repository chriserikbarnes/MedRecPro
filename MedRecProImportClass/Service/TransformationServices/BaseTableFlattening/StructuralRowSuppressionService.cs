using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Evaluates structural AE/Efficacy rows and owns parser suppression diagnostics.
    /// </summary>
    /// <remarks>
    /// The service preserves the parser-facing diagnostics contract while moving audit
    /// list ownership and structural-row predicates out of <see cref="BaseTableParser"/>.
    /// </remarks>
    /// <seealso cref="ITableParserDiagnostics"/>
    /// <seealso cref="TableSuppressionAuditRecord"/>
    internal sealed class StructuralRowSuppressionService
    {
        #region Compiled Patterns

        private static readonly Regex _structuralAeValueCellPattern = new(
            @"^\s*(?:-{1,}|[\u2013\u2014]+|\(?\s*[Nn]\s*=\s*\d[\d,]*\s*\)?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex _aeHeaderEchoParameterPattern = new(
            @"^\s*Adverse\s+(?:Drug\s+)?(?:Reaction|Reactions|Event|Events)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _aeNonObservationMetricPattern = new(
            @"^\s*Mean\s+Duration\s+of\s+Therapy(?:\s*\(.*\))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _knownStructuralRowLabelPattern = new(
            @"^\s*(?:" +
              @"Patients\s+with\s+any\s+adverse\s+reaction|" +
              @"Respiratory,\s*thoracic,\s*and\s*mediastinal\s+disorders|" +
              @"CHD\s+events|" +
              @"System\s+Organ\s+Class|SOC|Body\s+System(?:\s*[\(/\-].*)?|" +
              @"(?:Cardiac|Congenital|Ear|Endocrine|Eye|Gastrointestinal|General|Hepatobiliary|Immune\s+system|Infections\s+and\s+infestations|Injury,\s*poisoning\s+and\s+procedural\s+complications|Investigations|Metabolism\s+and\s+nutrition|Musculoskeletal\s+and\s+connective\s+tissue|Neoplasms\s+benign,\s*malignant\s+and\s+unspecified|Nervous\s+system|Pregnancy,\s*puerperium\s+and\s+perinatal|Psychiatric|Renal\s+and\s+urinary|Reproductive\s+system\s+and\s+breast|Respiratory,\s*thoracic\s+and\s+mediastinal|Skin\s+and\s+subcutaneous\s+tissue|Social\s+circumstances|Surgical\s+and\s+medical\s+procedures|Vascular)\s+disorders" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _combinedPopulationRowLabelPattern = new(
            @"^\s*(?:" +
              @"(?:male\s+and\s+female|female\s+and\s+male|men\s+and\s+women|women\s+and\s+men)\s+patients?(?:\s+combined)?|" +
              @"(?:all|total|overall)\s+patients?(?:\s+combined)?|" +
              @"(?:combined|pooled)\s+populations?|" +
              @"overall|total" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _eventPseudoArmPattern = new(
            @"^\s*Events?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _placeholderStatValuePattern = new(
            @"^\s*(?:[-\u2013\u2014]+|\u2194|\u2192|\u2190|\u27F7|N/?A|No\.\s*Analyzed|No\.\s*Erad\.\s*\(%\))\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _nEqualsCellPattern = new(
            @"^\(?\s*[Nn]\s*=\s*(\d[\d,]*)\s*\)?\s*$",
            RegexOptions.Compiled);

        #endregion Compiled Patterns

        /**************************************************************/
        /// <summary>
        /// Suppressed structural rows captured during the current parse.
        /// </summary>
        private readonly List<TableSuppressionAuditRecord> _suppressedRows = new();

        /**************************************************************/
        /// <summary>
        /// Gets the suppressed-row audit records captured during the current parse.
        /// </summary>
        /// <seealso cref="ITableParserDiagnostics.SuppressedRows"/>
        internal IReadOnlyList<TableSuppressionAuditRecord> SuppressedRows => _suppressedRows;

        /**************************************************************/
        /// <summary>
        /// Clears parser suppression diagnostics before a new parse run.
        /// </summary>
        /// <seealso cref="ITableParserDiagnostics.ClearDiagnostics"/>
        internal void ClearDiagnostics()
        {
            #region implementation

            _suppressedRows.Clear();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE row is structural metadata.
        /// </summary>
        /// <param name="obs">Observation candidate.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns>True when the row should be suppressed.</returns>
        internal static bool ShouldSuppressAeStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            return ShouldSuppressStructuralObservation(obs, parsed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE or Efficacy row is structural metadata.
        /// </summary>
        /// <param name="obs">Observation candidate.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns>True when the row should be suppressed.</returns>
        internal static bool ShouldSuppressStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            var isAe = string.Equals(obs.TableCategory, TableCategory.ADVERSE_EVENT.ToString(), StringComparison.OrdinalIgnoreCase);
            var isEfficacy = string.Equals(obs.TableCategory, TableCategory.EFFICACY.ToString(), StringComparison.OrdinalIgnoreCase);
            if (!isAe && !isEfficacy)
                return false;

            var rawValue = obs.RawValue?.Trim();
            var parameterName = obs.ParameterName?.Trim();

            if (isAe && AeColumnContextResolver.IsInvalidTreatmentArm(obs.TreatmentArm))
                return true;

            var parameterIsStructural =
                string.IsNullOrWhiteSpace(parameterName) ||
                IsStructuralRowLabel(parameterName) ||
                _aeHeaderEchoParameterPattern.IsMatch(parameterName) ||
                _aeNonObservationMetricPattern.IsMatch(parameterName);

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                _placeholderStatValuePattern.IsMatch(rawValue) &&
                parameterIsStructural)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                _structuralAeValueCellPattern.IsMatch(rawValue) &&
                isAe &&
                parameterIsStructural)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                _aeNonObservationMetricPattern.IsMatch(parameterName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                parsed.PrimaryValue == null &&
                parsed.SecondaryValue == null &&
                parsed.PValue == null &&
                _aeHeaderEchoParameterPattern.IsMatch(parameterName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                !string.IsNullOrWhiteSpace(parameterName) &&
                string.Equals(rawValue, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) && IsStructuralRowLabel(rawValue))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                _eventPseudoArmPattern.IsMatch(obs.TreatmentArm ?? string.Empty) &&
                IsStructuralRowLabel(parameterName))
            {
                return true;
            }

            return parsed.PrimaryValue == null &&
                   string.Equals(parsed.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase) &&
                   (_aeHeaderEchoParameterPattern.IsMatch(rawValue ?? string.Empty) ||
                    IsStructuralRowLabel(rawValue) ||
                    string.Equals(rawValue, parameterName, StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a known structural row label.
        /// </summary>
        /// <param name="text">Candidate row or cell text.</param>
        /// <returns>True when the text should be retained as context only.</returns>
        internal static bool IsStructuralRowLabel(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   (_knownStructuralRowLabelPattern.IsMatch(text.Trim()) ||
                    AeColumnContextResolver.IsBodySystemLabel(text));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a data-body row is structural context only.
        /// </summary>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <param name="parameterName">Cleaned column-zero row label.</param>
        /// <param name="category">Parser category.</param>
        /// <returns>True when the row should update context without emitting observations.</returns>
        internal static bool IsStructuralContextRow(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? parameterName,
            TableCategory category)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(parameterName))
                return false;

            var rowLabelIsStructural =
                IsStructuralRowLabel(parameterName) ||
                _aeHeaderEchoParameterPattern.IsMatch(parameterName) ||
                _aeNonObservationMetricPattern.IsMatch(parameterName);

            if (!rowLabelIsStructural)
                return false;

            var usableArms = arms.Where(ArmDefinitionExtractor.HasUsableTreatmentArm).ToList();
            if (usableArms.Count == 0)
                return true;

            return !usableArms.Any(arm =>
            {
                var cell = GetCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    return false;

                var text = cell.CleanedText.Trim();
                if (_structuralAeValueCellPattern.IsMatch(text) ||
                    IsStructuralRowLabel(text) ||
                    string.Equals(text, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var parsed = ValueParser.Parse(text, arm.SampleSize);
                return parsed.PrimaryValue.HasValue ||
                       parsed.SecondaryValue.HasValue ||
                       parsed.PValue.HasValue;
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects mid-body subpopulation header rows with per-arm sample-size overrides.
        /// </summary>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <param name="paramName">Cleaned column-zero row label.</param>
        /// <param name="subpopName">Resolved subpopulation label.</param>
        /// <param name="nOverrides">Per-arm sample-size overrides.</param>
        /// <returns>True when the row should be suppressed as a subpopulation header.</returns>
        internal static bool TryDetectSubpopulationHeader(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? paramName,
            out string? subpopName,
            out IDictionary<int, int> nOverrides)
        {
            #region implementation

            subpopName = null;
            nOverrides = new Dictionary<int, int>();

            if (string.IsNullOrWhiteSpace(paramName))
                return false;

            if (IsStructuralRowLabel(paramName) ||
                _aeHeaderEchoParameterPattern.IsMatch(paramName) ||
                _aeNonObservationMetricPattern.IsMatch(paramName))
            {
                return false;
            }

            var usableArms = arms.Where(ArmDefinitionExtractor.HasUsableTreatmentArm).ToList();
            if (usableArms.Count == 0)
                return false;

            int parsedNCount = 0;

            foreach (var arm in usableArms)
            {
                var cell = GetCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();
                var nMatch = _nEqualsCellPattern.Match(text);
                if (nMatch.Success)
                {
                    var raw = nMatch.Groups[1].Value.Replace(",", string.Empty);
                    if (int.TryParse(raw, out var nValue) && nValue > 0)
                    {
                        if (arm.ColumnIndex.HasValue)
                            nOverrides[arm.ColumnIndex.Value] = nValue;
                        parsedNCount++;
                        continue;
                    }

                    nOverrides.Clear();
                    return false;
                }

                if (!_structuralAeValueCellPattern.IsMatch(text))
                {
                    nOverrides.Clear();
                    return false;
                }
            }

            if (parsedNCount == 0)
            {
                nOverrides.Clear();
                return false;
            }

            subpopName = paramName.Trim();
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects combined/all-patient rows that reset subpopulation context.
        /// </summary>
        /// <param name="paramName">Cleaned column-zero row label.</param>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <returns>True when the row is structural and should be suppressed.</returns>
        internal static bool IsCombinedPopulationRowLabel(
            string? paramName,
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(paramName))
                return false;

            if (!_combinedPopulationRowLabelPattern.IsMatch(paramName.Trim()))
                return false;

            foreach (var arm in arms.Where(ArmDefinitionExtractor.HasUsableTreatmentArm))
            {
                var cell = GetCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();
                if (_structuralAeValueCellPattern.IsMatch(text))
                    continue;

                return false;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a structural value-cell placeholder.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns>True for dash and N equals structural value cells.</returns>
        internal static bool IsStructuralValueCell(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _structuralAeValueCellPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a placeholder statistic value.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns>True for dash, ditto, N/A, and related placeholder values.</returns>
        internal static bool IsPlaceholderStatValue(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _placeholderStatValuePattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is an AE header echo rather than a parameter.
        /// </summary>
        /// <param name="text">Text to inspect.</param>
        /// <returns>True when text names the generic AE header.</returns>
        internal static bool IsAeHeaderEchoParameter(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _aeHeaderEchoParameterPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an audit entry for a structural row or cell suppression.
        /// </summary>
        /// <param name="table">Source table.</param>
        /// <param name="row">Source row.</param>
        /// <param name="cell">Source cell, or null for row-level suppression.</param>
        /// <param name="category">Parser category.</param>
        /// <param name="parserName">Parser type name.</param>
        /// <param name="parameterName">Parameter label or row label.</param>
        /// <param name="treatmentArm">Treatment arm, when available.</param>
        /// <param name="rawValue">Raw value text suppressed.</param>
        /// <param name="structuralLabel">Structural label preserved as context.</param>
        /// <param name="contextTarget">Context field receiving the label.</param>
        /// <param name="reason">Human-readable suppression reason.</param>
        /// <param name="validationFlag">Stable validation flag.</param>
        internal void RecordSuppressedStructuralRow(
            ReconstructedTable table,
            ReconstructedRow row,
            ProcessedCell? cell,
            TableCategory category,
            string parserName,
            string? parameterName,
            string? treatmentArm,
            string? rawValue,
            string? structuralLabel,
            string? contextTarget,
            string reason,
            string validationFlag = "SUPPRESSED_STRUCTURAL_ROW")
        {
            #region implementation

            _suppressedRows.Add(new TableSuppressionAuditRecord
            {
                TextTableID = table.TextTableID,
                SourceRowSeq = row.SequenceNumberTextTableRow,
                SourceCellSeq = cell?.SequenceNumber,
                TableCategory = category.ToString(),
                ParserName = parserName,
                ParameterName = parameterName,
                TreatmentArm = treatmentArm,
                RawValue = rawValue,
                StructuralLabel = structuralLabel,
                ContextTarget = contextTarget,
                Reason = reason,
                ValidationFlag = validationFlag
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records AE rows that still have no usable treatment arm after rescue attempts.
        /// </summary>
        /// <param name="table">Source table.</param>
        /// <param name="rows">Rows that could not be emitted safely.</param>
        /// <param name="reason">Stable suppression flag.</param>
        /// <param name="parserName">Parser type name.</param>
        internal void RecordUnrescuableAeRows(
            ReconstructedTable table,
            IEnumerable<ReconstructedRow> rows,
            string reason,
            string parserName)
        {
            #region implementation

            foreach (var row in rows.Where(r => r.Classification == RowClassification.DataBody))
            {
                var firstCell = row.Cells?
                    .Where(c => !string.IsNullOrWhiteSpace(c.CleanedText))
                    .OrderBy(c => c.ResolvedColumnStart ?? int.MaxValue)
                    .FirstOrDefault();

                if (firstCell == null)
                    continue;

                var (parameterName, _) = GetParameterName(row);
                RecordSuppressedStructuralRow(
                    table,
                    row,
                    null,
                    TableCategory.ADVERSE_EVENT,
                    parserName,
                    parameterName,
                    null,
                    firstCell.CleanedText,
                    parameterName ?? firstCell.CleanedText,
                    "ParameterCategory",
                    reason,
                    reason);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Chooses the most specific AE suppression flag for unresolved arm definitions.
        /// </summary>
        /// <param name="arms">Arm definitions after all rescue attempts.</param>
        /// <returns>Stable suppression validation flag.</returns>
        internal static string GetUnrescuableAeReason(IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            var armList = arms.ToList();
            if (armList.Any(a =>
                    AeColumnContextResolver.IsCaptionLikeText(a.Name) ||
                    AeColumnContextResolver.IsCaptionLikeText(a.ParameterSubtype) ||
                    AeColumnContextResolver.IsCaptionLikeText(a.StudyContext)))
            {
                return AeSuppressionKind.CaptionArm.ToValidationFlag();
            }

            if (armList.Any(a =>
                    AeColumnContextResolver.IsBodySystemLabel(a.Name) ||
                    AeColumnContextResolver.IsBodySystemLabel(a.ParameterSubtype) ||
                    AeColumnContextResolver.IsBodySystemLabel(a.StudyContext)))
            {
                return AeSuppressionKind.BodySystemArm.ToValidationFlag();
            }

            return AeSuppressionKind.UnresolvedArm.ToValidationFlag();

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

        /**************************************************************/
        /// <summary>
        /// Gets the cleaned parameter name from column zero of a row.
        /// </summary>
        /// <param name="row">Source body row.</param>
        /// <returns>Cleaned parameter name and footnote marker string.</returns>
        private static (string? name, string? markers) GetParameterName(ReconstructedRow row)
        {
            #region implementation

            var cell = GetCellAtColumn(row, 0);
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return (null, null);

            var (name, markers) = ValueParser.CleanParameterName(cell.CleanedText);
            return (name, markers);

            #endregion
        }
    }
}
