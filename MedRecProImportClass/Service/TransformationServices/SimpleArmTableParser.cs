using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for single-header tables with treatment arm columns (AE and Efficacy).
    /// Column 0 = parameter name, remaining columns = arm data. Detects P-value and
    /// stat columns from header text.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Single-row header with arm definitions: "Drug A (N=188) n(%)"
    /// - Column 0 = parameter/adverse event name
    /// - Remaining columns = arm data values
    /// - Optional stat columns: "P-value", "Difference (95% CI)", "ARR"
    ///
    /// ## Type Promotion
    /// Bare Numeric values are promoted to Percentage when the arm's FormatHint
    /// contains "%" or "n(" (common in AE tables).
    ///
    /// ## Subtype Detection
    /// Rows with empty data cells but non-empty parameter name are treated as
    /// subtype/group dividers (e.g., "Components of primary endpoint").
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ValueParser"/>
    public class SimpleArmTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports both ADVERSE_EVENT and EFFICACY categories (fallback parser).
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;

        /**************************************************************/
        /// <summary>
        /// Priority 30 — fallback after more specific AE parsers.
        /// </summary>
        public override int Priority => 30;

        /**************************************************************/
        /// <summary>
        /// Always returns true — this is the default fallback parser for arm-based tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a simple arm table: header defines arms, each data row × arm column
        /// produces one observation. Stat columns emit Comparison rows.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            return parseInternal(table, SupportedCategory);

            #endregion
        }

        #endregion ITableParser Implementation

        #region Internal Parse Method

        /**************************************************************/
        /// <summary>
        /// Internal parse method that accepts a category parameter, allowing reuse
        /// for both ADVERSE_EVENT and EFFICACY contexts.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <param name="category">The table category to assign.</param>
        /// <returns>List of parsed observations.</returns>
        internal List<ParsedObservation> parseInternal(ReconstructedTable table, TableCategory category)
        {
            #region implementation

            ClearDiagnostics();
            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);
            var captionHint = detectCaptionValueHint(table.Caption);

            // Extract arm definitions and identify stat columns
            var allArms = extractArmDefinitions(table);
            var arms = new List<ArmDefinition>();
            var statColumns = new Dictionary<string, ArmDefinition>();

            foreach (var arm in allArms)
            {
                if (isStatColumn(arm.Name))
                {
                    // Classify stat column type
                    var name = arm.Name!;
                    if (name.Contains("P-value", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("P value", StringComparison.OrdinalIgnoreCase))
                        statColumns["pvalue"] = arm;
                    else if (name.Contains("Difference", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("95% CI", StringComparison.OrdinalIgnoreCase))
                        statColumns["diff_ci"] = arm;
                    else if (name.Contains("ARR", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Absolute Risk", StringComparison.OrdinalIgnoreCase))
                        statColumns["arr"] = arm;
                    else if (name.Contains("Relative Risk", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("RR", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("HR", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("OR", StringComparison.OrdinalIgnoreCase))
                        statColumns["rr_ci"] = arm;
                }
                else
                {
                    arms.Add(arm);
                }
            }

            // Iterate data rows
            string? currentSubtype = null;
            string? currentCategory = null; // SOC category for AE tables (from empty-data rows)
            var dataRows = getDataBodyRows(table);

            // Enrich arms from body-row header metadata (dose, N=, format hints)
            var skipRows = enrichArmsFromBodyRows(dataRows, arms);
            if (skipRows > 0)
            {
                dataRows = dataRows.Skip(skipRows).ToList();
            }
            applySingleProductArmFallback(table, arms);

            foreach (var row in dataRows)
            {
                // SOC divider handling is done by AeWithSocTableParser; skip here
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                if (isStructuralContextRow(row, arms, paramName, category))
                {
                    if (category == TableCategory.ADVERSE_EVENT)
                    {
                        currentCategory = paramName;
                        currentSubtype = null;
                    }
                    else
                    {
                        currentSubtype = paramName;
                    }

                    recordSuppressedStructuralRow(
                        table, row, null, category,
                        paramName, null, paramName, paramName,
                        category == TableCategory.ADVERSE_EVENT ? "ParameterCategory" : "ParameterSubtype",
                        "Structural arm-table row captured as context");
                    continue;
                }

                // Subtype detection: row with parameter name but all arm cells empty
                var hasData = arms.Where(hasUsableTreatmentArm).Any(arm =>
                {
                    var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                    return cell != null && !string.IsNullOrWhiteSpace(cell.CleanedText);
                });

                if (!hasData)
                {
                    if (category == TableCategory.ADVERSE_EVENT)
                    {
                        // In AE tables, empty-data rows are SOC dividers
                        // (e.g., "Body as a Whole", "Cardiovascular")
                        currentCategory = paramName;
                        currentSubtype = null; // Reset subtype on new category
                    }
                    else
                    {
                        currentSubtype = paramName;
                    }
                    continue;
                }

                var effectiveParamName = paramName;
                var interspersedLabelCellSequence = (int?)null;
                if (tryRecoverInterspersedRowLabel(
                    row, arms, paramName, category,
                    out var recoveredParamName,
                    out var recoveredCategory,
                    out var recoveredLabelCellSequence))
                {
                    effectiveParamName = recoveredParamName ?? paramName;
                    interspersedLabelCellSequence = recoveredLabelCellSequence;
                    if (category == TableCategory.ADVERSE_EVENT && !string.IsNullOrWhiteSpace(recoveredCategory))
                    {
                        currentCategory = recoveredCategory;
                    }
                    else if (category == TableCategory.EFFICACY && !string.IsNullOrWhiteSpace(recoveredCategory))
                    {
                        currentSubtype = recoveredCategory;
                    }
                }

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    if (category == TableCategory.EFFICACY && isComparisonRowLabel(effectiveParamName))
                    {
                        emitRowLabelComparisonRows(
                            table, r, category, effectiveParamName, currentSubtype,
                            population, arms, obs);
                        return;
                    }

                    // Extract p-value from stat column if present
                    double? rowPValue = extractPValue(r, statColumns);

                    // Arm data columns
                    foreach (var arm in arms)
                    {
                        if (!hasUsableTreatmentArm(arm))
                            continue;

                        var cell = getCellAtColumn(r, arm.ColumnIndex ?? 0);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;
                        if (interspersedLabelCellSequence.HasValue &&
                            cell.SequenceNumber == interspersedLabelCellSequence.Value)
                            continue;

                        var o = createBaseObservation(table, r, cell, category);
                        o.ParameterName = effectiveParamName;
                        o.ParameterCategory = currentCategory;
                        o.ParameterSubtype = currentSubtype ?? arm.ParameterSubtype;
                        o.TreatmentArm = arm.Name;
                        o.ArmN = arm.SampleSize;
                        o.StudyContext = arm.StudyContext;
                        o.DoseRegimen = arm.DoseRegimen;
                        o.Dose = arm.Dose;
                        o.DoseUnit = arm.DoseUnit;
                        o.Population = population;
                        o.PValue = rowPValue;

                        var parsed = parseValueWithAeEfficacyContext(
                            cell.CleanedText,
                            category,
                            effectiveParamName,
                            currentCategory ?? currentSubtype,
                            arm,
                            table.Caption);

                        // Caption-based type inference (e.g., "Mean (SD)" in efficacy tables)
                        if (!captionHint.IsEmpty)
                        {
                            parsed = applyCaptionHint(parsed, captionHint);
                        }

                        applyParsedValue(o, parsed);
                        if (shouldSuppressStructuralObservation(o, parsed))
                        {
                            recordSuppressedStructuralRow(
                                table, r, cell, category,
                                paramName, arm.Name, cell.CleanedText, paramName,
                                category == TableCategory.ADVERSE_EVENT ? "ParameterCategory" : "ParameterSubtype",
                                "Structural arm-table cell suppressed before observation emission");
                            continue;
                        }

                        obs.Add(o);
                    }

                    // Stat/comparison columns
                    emitComparisonRows(table, r, category, effectiveParamName, currentSubtype,
                        population, rowPValue, statColumns, obs);
                });
            }

            return observations;

            #endregion
        }

        #endregion Internal Parse Method

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Extracts p-value from the stat column of a data row.
        /// </summary>
        private static double? extractPValue(ReconstructedRow row, Dictionary<string, ArmDefinition> statColumns)
        {
            #region implementation

            if (!statColumns.TryGetValue("pvalue", out var pvalArm))
                return null;

            var cell = getCellAtColumn(row, pvalArm.ColumnIndex ?? 0);
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return null;

            var parsed = ValueParser.Parse(cell.CleanedText);
            return parsed.PValue ?? parsed.PrimaryValue;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Emits Comparison observations for statistic rows whose labels appear in
        /// the first column rather than in dedicated statistic columns.
        /// </summary>
        /// <remarks>
        /// Efficacy tables often encode p-values and placebo differences as ordinary
        /// body rows, with the actual value in the first treatment column and a
        /// placeholder in the reference arm. These rows describe a comparison, so the
        /// value should not be emitted under the treatment/reference arm name.
        /// </remarks>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="row">Current data row.</param>
        /// <param name="category">Parser category.</param>
        /// <param name="paramName">Statistic row label.</param>
        /// <param name="subtype">Current efficacy subtype/group.</param>
        /// <param name="population">Detected population context.</param>
        /// <param name="arms">Treatment arms used only to locate source cells.</param>
        /// <param name="observations">Observation sink.</param>
        /// <seealso cref="BaseTableParser"/>
        private void emitRowLabelComparisonRows(
            ReconstructedTable table, ReconstructedRow row, TableCategory category,
            string paramName, string? subtype, string? population,
            IEnumerable<ArmDefinition> arms, List<ParsedObservation> observations)
        {
            #region implementation

            var emittedCellSequences = new HashSet<int?>();
            foreach (var arm in arms.Where(hasUsableTreatmentArm))
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;
                if (!emittedCellSequences.Add(cell.SequenceNumber))
                    continue;

                var obs = createBaseObservation(table, row, cell, category);
                obs.ParameterName = paramName;
                obs.ParameterSubtype = subtype;
                obs.TreatmentArm = "Comparison";
                obs.Population = population;

                var parsed = parseValueWithAeEfficacyContext(
                    cell.CleanedText,
                    category,
                    paramName,
                    subtype,
                    arm,
                    table.Caption,
                    forcePValue: isPValueRowLabel(paramName));
                parsed = applyRowLabelComparisonType(paramName, parsed);

                applyParsedValue(obs, parsed);
                if (shouldSuppressStructuralObservation(obs, parsed))
                {
                    recordSuppressedStructuralRow(
                        table, row, cell, category,
                        paramName, "Comparison", cell.CleanedText, paramName,
                        "ParameterSubtype",
                        "Structural comparison-row cell suppressed before observation emission");
                    continue;
                }

                if (obs.PrimaryValue == null && obs.SecondaryValue == null && obs.PValue == null)
                    continue;

                observations.Add(obs);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies statistic-row value types to parsed row-label comparison values.
        /// </summary>
        /// <param name="rowLabel">Statistic row label.</param>
        /// <param name="parsed">Parsed value to adjust.</param>
        /// <returns>Adjusted parsed value.</returns>
        /// <seealso cref="ParsedValue"/>
        private static ParsedValue applyRowLabelComparisonType(string? rowLabel, ParsedValue parsed)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(rowLabel) || !parsed.PrimaryValue.HasValue)
                return parsed;

            if (isPValueRowLabel(rowLabel))
                return parsed;

            if (rowLabel.Contains("Difference", StringComparison.OrdinalIgnoreCase) ||
                rowLabel.Contains("Risk Difference", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "RiskDifference";
                if (rowLabel.Contains("%", StringComparison.OrdinalIgnoreCase) ||
                    rowLabel.Contains("percent", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Unit = "%";
                }
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "HazardRatio";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Odds", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "OddsRatio";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Relative Risk", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "RelativeRisk";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }

            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Emits Comparison rows for stat columns (RiskDifference, ARR, RelativeRiskReduction).
        /// </summary>
        private static void emitComparisonRows(
            ReconstructedTable table, ReconstructedRow row, TableCategory category,
            string paramName, string? subtype, string? population, double? pValue,
            Dictionary<string, ArmDefinition> statColumns, List<ParsedObservation> observations)
        {
            #region implementation

            var statTypes = new[]
            {
                ("diff_ci", "RiskDifference"),
                ("arr", "ARR"),
                ("rr_ci", "RelativeRiskReduction")
            };

            foreach (var (key, defaultType) in statTypes)
            {
                if (!statColumns.TryGetValue(key, out var statArm))
                    continue;

                var cell = getCellAtColumn(row, statArm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var parsed = ValueParser.Parse(cell.CleanedText);
                if (parsed.PrimaryValue == null && parsed.PrimaryValueType != "PValue")
                    continue;

                var obs = createBaseObservation(table, row, cell, category);
                obs.ParameterName = paramName;
                obs.ParameterSubtype = subtype;
                obs.TreatmentArm = "Comparison";
                obs.Population = population;
                obs.PValue = pValue;

                // Default type if parser didn't determine one
                if (parsed.PrimaryValueType == "Numeric" || parsed.PrimaryValueType == null)
                    parsed.PrimaryValueType = defaultType;

                applyParsedValue(obs, parsed);
                observations.Add(obs);
            }

            #endregion
        }

        #endregion Private Helpers
    }
}
