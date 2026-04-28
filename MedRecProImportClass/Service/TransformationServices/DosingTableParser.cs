using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for dosage and administration tables.
    /// </summary>
    /// <remarks>
    /// Dosing tables use several layouts: header-carried dose regimens, row
    /// labels that are dose-reduction levels, body-weight bands, and
    /// lab-threshold dose-modification triggers. The parser classifies row and
    /// header text before applying cell-level dose promotion so dose phrases do
    /// not leak into <see cref="ParsedObservation.Unit"/>.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="DoseExtractor"/>
    /// <seealso cref="DosingShapeClassifier"/>
    public class DosingTableParser : BaseTableParser
    {
        #region Compiled Regex Patterns

        /**************************************************************/
        /// <summary>
        /// Timepoint-like header labels such as Week 1, Day 8, or Month 3.
        /// </summary>
        private static readonly Regex _timepointPattern = new(
            @"\b(?:(?:Week|Month|Year|Day)s?\s*\d+|\d+\s*(?:Weeks?|Months?|Years?|Days?))\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Row label that is purely numeric (with optional decimal). Used by
        /// typed col-0 detection to recognize bare body-weight, height, BSA,
        /// CrCl, or age values that lack their unit suffix because the unit
        /// was hoisted into the column header.
        /// </summary>
        private static readonly Regex _bareNumericRowLabelPattern = new(
            @"^\s*\d+(?:\.\d+)?\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Numeric-range row label such as <c>50-59</c> or <c>1.30 to 1.49</c>
        /// (no unit suffix — the unit is in the column header).
        /// </summary>
        private static readonly Regex _bareNumericRangeRowLabelPattern = new(
            @"^\s*\d+(?:\.\d+)?\s*(?:to|[-–—])\s*\d+(?:\.\d+)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects column-0 headers that hoist a unit into header context so
        /// rows can be bare numeric values. Patterns intentionally narrow:
        /// each pattern fires on one axis type so the parser can decide
        /// whether to inherit "kg", "m", "mL/min", or "years".
        /// </summary>
        private static readonly Regex _bodyWeightHeaderPattern = new(
            @"\b(?:body\s*weight|weight)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _heightOrBsaHeaderPattern = new(
            @"\b(?:height|body\s+surface\s+area|BSA)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _crClHeaderPattern = new(
            @"\b(?:Cr\s*Cl|creatinine\s+clearance)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _ageHeaderPattern = new(
            @"^\s*age\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects col-0 headers indicating renal or hepatic adjustment so
        /// bare severity labels (Mild/Moderate/Severe) can be promoted to
        /// Population only in that context. Outside this context the words
        /// are too generic to safely treat as population strata.
        /// </summary>
        private static readonly Regex _renalHepaticContextHeaderPattern = new(
            @"\b(?:renal|hepatic|kidney|liver)\s+(?:impairment|function|adjustment)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Bare severity label that becomes a Population stratum only when the
        /// surrounding col-0 header indicates a renal/hepatic-impairment
        /// context. Without that context these words are too generic.
        /// </summary>
        private static readonly Regex _severityRowLabelPattern = new(
            @"^\s*(?:mild|moderate|severe)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Heuristic for header text that is too long or too descriptive to
        /// safely use as a clinical ParameterName (UNIT_HEADER_LEAK-style
        /// guard for ParameterName instead of Unit). Mirrors the column
        /// standardization scrub's len > 30 rule but tightened for clinical
        /// parameter labels.
        /// </summary>
        private const int LongHeaderTextThreshold = 50;

        #endregion Compiled Regex Patterns

        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports DOSING category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.DOSING;

        /**************************************************************/
        /// <summary>
        /// Priority 10 - only dosing parser.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for DOSING-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a dosing table into one observation per non-empty data cell.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (tablePopulation, _) = detectPopulation(table);
            var shape = DosingShapeClassifier.Classify(table);
            var headers = extractDosingHeaders(table);
            var col0HeaderText = table.Header?.Columns?.FirstOrDefault()?.LeafHeaderText?.Trim() ?? string.Empty;
            var axis = inferRowAxisFromHeader(col0HeaderText);

            foreach (var row in getDataBodyRows(table))
            {
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                var rowContext = classifyRow(row, shape, tablePopulation, axis);
                if (string.IsNullOrWhiteSpace(rowContext.ParameterName)
                    && string.IsNullOrWhiteSpace(rowContext.Population))
                {
                    continue;
                }

                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    foreach (var header in headers)
                    {
                        var cell = getCellAtColumn(r, header.ColumnIndex);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var observation = createBaseObservation(table, r, cell, TableCategory.DOSING);
                        applyAxisContext(observation, rowContext, header, shape);
                        applyCellValue(observation, cell.CleanedText, shape);
                        obs.Add(observation);
                    }
                });
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Types

        private enum HeaderRole
        {
            Unknown,
            DoseRegimen,
            Population,
            Timepoint,
            Unit,
            Descriptor
        }

        private readonly record struct DosingHeader(
            int ColumnIndex,
            string HeaderText,
            HeaderRole Role,
            decimal? Dose,
            string? DoseUnit,
            string? Population,
            string? Unit);

        private readonly record struct DosingRowContext(
            string? ParameterName,
            string? Population,
            bool IsLabThreshold,
            string? Timepoint = null,
            string? TreatmentArm = null,
            string? DoseRegimen = null,
            decimal? Dose = null,
            string? DoseUnit = null);

        /**************************************************************/
        /// <summary>
        /// Axis type encoded by the column-0 header. When the header carries a
        /// unit (e.g., "Body Weight (kg)"), bare-numeric row labels can inherit
        /// that unit and route to <see cref="ParsedObservation.Population"/>
        /// instead of falling through to <see cref="ParsedObservation.ParameterName"/>.
        /// </summary>
        private enum RowAxisType
        {
            None,
            BodyWeight,
            HeightOrBsa,
            CreatinineClearance,
            Age,
            RenalHepaticAdjustment
        }

        #endregion Private Types

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Extracts dosing header context from header columns, skipping col 0.
        /// </summary>
        /// <param name="table">The reconstructed dosing table.</param>
        /// <returns>Header context for every parseable data column.</returns>
        private static List<DosingHeader> extractDosingHeaders(ReconstructedTable table)
        {
            #region implementation

            var headers = new List<DosingHeader>();
            if (table.Header?.Columns == null)
                return headers;

            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim() ?? $"Col{i}";
                var columnIndex = col.ColumnIndex ?? i;
                headers.Add(classifyHeader(columnIndex, text));
            }

            return headers;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a header label as dose regimen, population, timepoint,
        /// unit, descriptor, or unknown.
        /// </summary>
        private static DosingHeader classifyHeader(int columnIndex, string headerText)
        {
            #region implementation

            var (dose, doseUnit) = DoseExtractor.Extract(headerText);
            if (dose.HasValue && !PopulationDetector.LooksLikeLabThresholdDoseModification(headerText))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.DoseRegimen,
                    dose,
                    doseUnit,
                    null,
                    null);
            }

            if (PopulationDetector.TryMatchLabel(headerText, out var population))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.Population,
                    null,
                    null,
                    population,
                    null);
            }

            if (PopulationDetector.LooksLikeWeightBand(headerText))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.Population,
                    null,
                    null,
                    headerText,
                    null);
            }

            if (_timepointPattern.IsMatch(headerText))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.Timepoint,
                    null,
                    null,
                    null,
                    null);
            }

            var unit = UnitDictionary.TryExtractFromHeaderLikeText(headerText);
            if (!string.IsNullOrWhiteSpace(unit))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.Unit,
                    null,
                    null,
                    null,
                    unit);
            }

            if (DosingDescriptorDictionary.ContainsDosingDescriptor(headerText))
            {
                return new DosingHeader(
                    columnIndex,
                    headerText,
                    HeaderRole.Descriptor,
                    null,
                    null,
                    null,
                    null);
            }

            return new DosingHeader(columnIndex, headerText, HeaderRole.Unknown, null, null, null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Inspects the column-0 header text to decide whether row labels are
        /// values of a typed axis. When the header is something like
        /// <c>"Body Weight (kg)"</c>, <c>"BSA (m²)"</c>, or <c>"CrCl (mL/min)"</c>,
        /// the unit sits in header context and bare-numeric row labels should
        /// inherit it rather than leak into <see cref="ParsedObservation.ParameterName"/>.
        /// </summary>
        /// <param name="col0HeaderText">Column-0 leaf header text, trimmed.</param>
        /// <returns>The axis type encoded by the header.</returns>
        private static RowAxisType inferRowAxisFromHeader(string col0HeaderText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(col0HeaderText))
                return RowAxisType.None;

            // Order matters: more specific patterns first. Renal/hepatic
            // adjustment headers are checked before height/CrCl because a
            // header like "Renal Impairment" mentions neither weight nor
            // height nor a clearance unit.
            if (_renalHepaticContextHeaderPattern.IsMatch(col0HeaderText))
                return RowAxisType.RenalHepaticAdjustment;

            if (_crClHeaderPattern.IsMatch(col0HeaderText))
                return RowAxisType.CreatinineClearance;

            if (_heightOrBsaHeaderPattern.IsMatch(col0HeaderText))
                return RowAxisType.HeightOrBsa;

            if (_bodyWeightHeaderPattern.IsMatch(col0HeaderText))
                return RowAxisType.BodyWeight;

            if (_ageHeaderPattern.IsMatch(col0HeaderText))
                return RowAxisType.Age;

            return RowAxisType.None;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the canonical unit suffix for a typed-axis row value, or
        /// null when the axis does not have a single unit. Used to format
        /// inherited row-label values into their canonical Population string.
        /// </summary>
        private static string? unitSuffixForAxis(RowAxisType axis)
        {
            #region implementation

            return axis switch
            {
                RowAxisType.BodyWeight => "kg",
                RowAxisType.HeightOrBsa => "m",
                RowAxisType.CreatinineClearance => "mL/min",
                RowAxisType.Age => "years",
                _ => null,
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies the column-0 row label for dosing-specific context.
        /// </summary>
        /// <param name="row">The reconstructed data row.</param>
        /// <param name="shape">The table shape profile.</param>
        /// <param name="tablePopulation">Population detected from caption / section title.</param>
        /// <param name="axis">Axis type encoded by the column-0 header.</param>
        /// <returns>Row context used for axis routing during cell parsing.</returns>
        private static DosingRowContext classifyRow(
            ReconstructedRow row,
            DosingShapeClassifier.Shape shape,
            string? tablePopulation,
            RowAxisType axis)
        {
            #region implementation

            var (rowLabel, _) = getParameterName(row);
            if (string.IsNullOrWhiteSpace(rowLabel))
                return new DosingRowContext(null, tablePopulation, false);

            // Lab thresholds first — must precede typed-axis numeric handling
            // because a row like "Hemoglobin <8 g/dL" contains a digit but is
            // not a body-weight value.
            if (shape == DosingShapeClassifier.Shape.LabThresholdDoseModification
                || PopulationDetector.LooksLikeLabThresholdDoseModification(rowLabel))
            {
                return new DosingRowContext(rowLabel, tablePopulation, true);
            }

            // Typed-axis col 0: bare numeric or numeric-range row labels under
            // a unit-bearing header (e.g., "Body Weight (kg)" → row "60" gets
            // Population="60 kg", ParameterName="Dose"). Recovers TID 19220
            // (Gadoteridol) and similar height/CrCl/age-typed layouts.
            if (axis != RowAxisType.None
                && axis != RowAxisType.RenalHepaticAdjustment
                && (_bareNumericRowLabelPattern.IsMatch(rowLabel)
                    || _bareNumericRangeRowLabelPattern.IsMatch(rowLabel)))
            {
                var unit = unitSuffixForAxis(axis);
                var population = string.IsNullOrEmpty(unit)
                    ? rowLabel.Trim()
                    : $"{rowLabel.Trim()} {unit}";
                return new DosingRowContext("Dose", population, false);
            }

            if (shape == DosingShapeClassifier.Shape.BodyWeight
                || PopulationDetector.LooksLikeWeightBand(rowLabel))
            {
                var population = PopulationDetector.TryMatchLabel(rowLabel, out var weightBand)
                    ? weightBand
                    : rowLabel;
                return new DosingRowContext("Dose", population, false);
            }

            // Height / BSA bands, CrCl bands — always Population values.
            if (PopulationDetector.LooksLikeHeightBand(rowLabel)
                || PopulationDetector.LooksLikeCreatinineClearanceBand(rowLabel))
            {
                return new DosingRowContext("Dose", rowLabel.Trim(), false);
            }

            // Severity bands (Mild/Moderate/Severe) — only promoted to
            // Population when the col 0 header indicates renal/hepatic
            // impairment. Without that context the bare word is too generic.
            if (axis == RowAxisType.RenalHepaticAdjustment
                && _severityRowLabelPattern.IsMatch(rowLabel))
            {
                return new DosingRowContext("Dose", $"{rowLabel.Trim()} Impairment", false);
            }

            // Timepoint row labels: "Day 1", "Week 5 onward to maintenance",
            // "Weeks 1 and 2". Route to Timepoint instead of ParameterName.
            if (_timepointPattern.IsMatch(rowLabel))
            {
                return new DosingRowContext(
                    "Dose",
                    tablePopulation,
                    IsLabThreshold: false,
                    Timepoint: rowLabel.Trim());
            }

            // Drug + dose compound row labels: "TRISENOX 0.15 mg/kg once daily
            // intravenously" → split into TreatmentArm + DoseRegimen via
            // DoseExtractor.StripDoseFragment. The stripped prefix is treated
            // as TreatmentArm only when it survives population/descriptor
            // checks (so generic "Patient" / "Subject" don't get promoted).
            var stripped = DoseExtractor.StripDoseFragment(rowLabel).Trim();
            if (!string.IsNullOrWhiteSpace(stripped)
                && stripped.Length >= 3
                && stripped.Length < rowLabel.Length
                && !PopulationDetector.TryMatchLabel(stripped, out _)
                && !DosingDescriptorDictionary.ContainsDosingDescriptor(stripped))
            {
                var doseFragment = rowLabel[stripped.Length..].Trim();
                var (dose, doseUnit) = DoseExtractor.Extract(doseFragment);
                if (dose.HasValue)
                {
                    return new DosingRowContext(
                        ParameterName: "Dose",
                        Population: tablePopulation,
                        IsLabThreshold: false,
                        TreatmentArm: stripped,
                        DoseRegimen: doseFragment,
                        Dose: dose,
                        DoseUnit: doseUnit);
                }
            }

            if (PopulationDetector.TryMatchLabel(rowLabel, out var canonicalPopulation))
                return new DosingRowContext("Dose", canonicalPopulation, false);

            return new DosingRowContext(rowLabel, tablePopulation, false);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies row and header axis context to an observation before cell
        /// value parsing.
        /// </summary>
        private static void applyAxisContext(
            ParsedObservation observation,
            DosingRowContext rowContext,
            DosingHeader header,
            DosingShapeClassifier.Shape shape)
        {
            #region implementation

            observation.ParameterName = rowContext.ParameterName;
            observation.Population = rowContext.Population;

            // Drug + dose row-label split (TRISENOX 0.15 mg/kg → ...)
            if (!string.IsNullOrWhiteSpace(rowContext.TreatmentArm))
                observation.TreatmentArm = rowContext.TreatmentArm;
            if (!string.IsNullOrWhiteSpace(rowContext.DoseRegimen))
                observation.DoseRegimen = rowContext.DoseRegimen;
            if (rowContext.Dose.HasValue)
                observation.Dose = rowContext.Dose;
            if (!string.IsNullOrWhiteSpace(rowContext.DoseUnit))
                observation.DoseUnit = rowContext.DoseUnit;

            // Timepoint row label (Day 1, Week 5 onward to maintenance)
            if (!string.IsNullOrWhiteSpace(rowContext.Timepoint))
                observation.Timepoint = rowContext.Timepoint;

            // BodyWeight shape: header text usually carries the dose descriptor
            // (e.g., "Recommended Dose"). Overwrite ParameterName with the
            // header — but only when the header is short enough to be a
            // clinical ParameterName. Long header text (e.g., a full caption
            // echo such as "Recommended Dosage Total Volume of Oral Solution
            // Once Daily (Trametinib Content)") would otherwise leak into
            // ParameterName the way header text used to leak into Unit.
            if (shape == DosingShapeClassifier.Shape.BodyWeight
                && !isGenericHeader(header.HeaderText)
                && header.HeaderText.Length <= LongHeaderTextThreshold)
            {
                observation.ParameterName = header.HeaderText;
            }

            switch (header.Role)
            {
                case HeaderRole.DoseRegimen:
                    observation.DoseRegimen = header.HeaderText;
                    observation.Dose = header.Dose;
                    observation.DoseUnit = header.DoseUnit;
                    break;

                case HeaderRole.Population:
                    observation.Population = header.Population;
                    break;

                case HeaderRole.Timepoint:
                    // Header timepoint takes precedence over row-label timepoint
                    // because column-axis timepoints are a stronger signal.
                    observation.Timepoint = header.HeaderText;
                    break;

                case HeaderRole.Unit:
                    observation.Unit = header.Unit;
                    break;

                case HeaderRole.Descriptor:
                    // Same long-header guard as the BodyWeight branch — long
                    // header text (full caption echoes, multi-clause column
                    // labels) must not replace a clean ParameterName like
                    // "Dose" with a 90-character string.
                    if (isGenericHeader(observation.ParameterName)
                        && header.HeaderText.Length <= LongHeaderTextThreshold)
                    {
                        observation.ParameterName = header.HeaderText;
                    }
                    break;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies cell-level value parsing, promoting real dose phrases into
        /// DoseRegimen/Dose/DoseUnit before falling back to ValueParser.
        /// </summary>
        private static void applyCellValue(
            ParsedObservation observation,
            string? rawCellText,
            DosingShapeClassifier.Shape shape)
        {
            #region implementation

            var text = rawCellText?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var isLabThreshold = PopulationDetector.LooksLikeLabThresholdDoseModification(text);
            var (dose, doseUnit) = isLabThreshold
                ? ((decimal?)null, (string?)null)
                : DoseExtractor.Extract(text);

            if (dose.HasValue)
            {
                observation.DoseRegimen = text;
                observation.Dose = dose;
                observation.DoseUnit = doseUnit;
                observation.PrimaryValue = decimal.ToDouble(dose.Value);
                observation.PrimaryValueType = "Numeric";
                observation.Unit = doseUnit;
                observation.ParseConfidence = 1.0;
                observation.ParseRule = "dosing_cell_dose";
                observation.ValidationFlags = appendFlag(observation.ValidationFlags, "DOSING_CELL_DOSE");
                return;
            }

            if (shape == DosingShapeClassifier.Shape.LabThresholdDoseModification)
            {
                observation.DoseRegimen = text;
                observation.ParseConfidence = 0.8;
                observation.ParseRule = isLabThreshold
                    ? "dosing_lab_threshold"
                    : "dosing_lab_action";
                return;
            }

            var parsed = ValueParser.Parse(text);
            applyParsedValue(observation, parsed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header or parameter label is too generic to use
        /// as a clinical parameter.
        /// </summary>
        private static bool isGenericHeader(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return true;

            return value.Equals("Value", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Dose", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Dosage", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Population", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Body Weight", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        #endregion Private Helpers
    }
}
