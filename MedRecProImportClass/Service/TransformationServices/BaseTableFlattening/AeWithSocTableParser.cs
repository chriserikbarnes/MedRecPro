using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for single-header AE tables with SOC (System Organ Class) divider rows
    /// in the body. Like <see cref="SimpleArmTableParser"/> but propagates SOC category
    /// from divider rows into ParameterCategory.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Single-row header with arm definitions
    /// - Body contains SOC divider rows (single cell spanning full width)
    /// - DataBody rows following a SOC divider inherit its category
    ///
    /// ## Type Promotion
    /// Bare Numeric values are promoted to Percentage in AE context.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="SimpleArmTableParser"/>
    public class AeWithSocTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports ADVERSE_EVENT category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;

        /**************************************************************/
        /// <summary>
        /// Priority 20 — tried after MultilevelAeTableParser but before SimpleArmTableParser.
        /// </summary>
        public override int Priority => 20;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has SOC divider rows.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if HasSocDividers is true.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            return table.HasSocDividers == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an AE table with SOC dividers. Propagates SOC name from divider
        /// rows into ParameterCategory for subsequent data rows.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            ClearDiagnostics();
            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract arm definitions from header
            var arms = extractArmDefinitions(table);
            if (arms.Count == 0)
                return observations;

            // Caption-derived StudyContext fallback for single-header AE tables
            // that don't carry a colspan study-context header. Returns null for
            // captions that don't match the canonical AE grammar, so it's safe
            // to compute unconditionally.
            var captionStudyContext = extractStudyContextFromCaption(table.Caption);

            // Iterate data rows with SOC propagation
            string? currentSoc = null;
            string? currentSubpopulation = null;
            IDictionary<int, int> subpopArmNOverrides = new Dictionary<int, int>();
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
                // SOC divider — update current category, reset subpopulation context.
                if (row.Classification == RowClassification.SocDivider)
                {
                    currentSoc = row.SocName;
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    continue;
                }

                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                // Mid-body subpopulation header.
                if (tryDetectSubpopulationHeader(row, arms, paramName, out var subpopName, out var nOverrides))
                {
                    currentSubpopulation = subpopName;
                    subpopArmNOverrides = nOverrides;
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.ADVERSE_EVENT,
                        paramName, null, paramName, subpopName!, "Subpopulation",
                        "Mid-body subpopulation N-row captured as subpopulation context");
                    continue;
                }

                // Combined / all-patients row — suppress AND reset subpopulation context.
                if (isCombinedPopulationRowLabel(paramName, row, arms))
                {
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.ADVERSE_EVENT,
                        paramName, null, paramName, paramName, "Subpopulation",
                        "Combined/all-patients row suppressed and subpopulation context reset");
                    continue;
                }

                if (isStructuralContextRow(row, arms, paramName, TableCategory.ADVERSE_EVENT))
                {
                    currentSoc = paramName;
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.ADVERSE_EVENT,
                        paramName, null, paramName, paramName, "ParameterCategory",
                        "Structural AE/SOC row captured as category context");
                    continue;
                }

                var effectiveParamName = paramName;
                var interspersedLabelCellSequence = (int?)null;
                if (tryRecoverInterspersedRowLabel(
                    row, arms, paramName, TableCategory.ADVERSE_EVENT,
                    out var recoveredParamName,
                    out var recoveredCategory,
                    out var recoveredLabelCellSequence))
                {
                    effectiveParamName = recoveredParamName ?? paramName;
                    interspersedLabelCellSequence = recoveredLabelCellSequence;
                    if (!string.IsNullOrWhiteSpace(recoveredCategory))
                        currentSoc = recoveredCategory;
                }

                // Capture locals for the lambda below.
                var capturedSubpopulation = currentSubpopulation;
                var capturedSubpopArmNOverrides = subpopArmNOverrides;

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
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

                        var o = createBaseObservation(table, r, cell, TableCategory.ADVERSE_EVENT);
                        o.ParameterName = effectiveParamName;
                        o.ParameterCategory = currentSoc;
                        o.ParameterSubtype = arm.ParameterSubtype;
                        o.TreatmentArm = arm.Name;
                        // Subpopulation override: per-arm N, falling back to arm.SampleSize.
                        o.ArmN = (arm.ColumnIndex.HasValue &&
                                  capturedSubpopArmNOverrides.TryGetValue(arm.ColumnIndex.Value, out var overrideN))
                            ? overrideN
                            : arm.SampleSize;
                        // Arm-derived StudyContext (from header colspan) always wins;
                        // caption extractor only fills the blank.
                        o.StudyContext = arm.StudyContext ?? captionStudyContext;
                        o.DoseRegimen = arm.DoseRegimen;
                        o.Dose = arm.Dose;
                        o.DoseUnit = arm.DoseUnit;
                        o.Population = population;
                        o.Subpopulation = capturedSubpopulation;

                        var parsed = parseValueWithAeEfficacyContext(
                            cell.CleanedText,
                            TableCategory.ADVERSE_EVENT,
                            effectiveParamName,
                            currentSoc,
                            arm,
                            table.Caption);

                        applyParsedValue(o, parsed);
                        if (shouldSuppressAeStructuralObservation(o, parsed))
                        {
                            recordSuppressedStructuralRow(
                                table, r, cell, TableCategory.ADVERSE_EVENT,
                                effectiveParamName, arm.Name, cell.CleanedText, effectiveParamName,
                                "ParameterCategory",
                                "Structural AE cell suppressed before observation emission");
                            continue;
                        }

                        obs.Add(o);
                    }
                });
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation
    }
}
