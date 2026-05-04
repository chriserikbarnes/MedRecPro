using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for two-row header AE tables with colspan study contexts and arm sub-headers.
    /// HeaderPath[0] = StudyContext (from colspan row), HeaderPath[last] = arm definition.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Row 1: colspan headers providing study context ("Treatment", "Prevention")
    /// - Row 2: arm sub-headers with N= ("EVISTA (N=2557) %", "Placebo (N=2576) %")
    /// - Body: SOC divider rows + data rows
    ///
    /// ## Type Promotion
    /// Bare Numeric values in AE context are promoted to Percentage.
    ///
    /// ## Positional Mapping
    /// Uses ResolvedColumnStart from header columns to map data cells to arms.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ValueParser"/>
    public class MultilevelAeTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports ADVERSE_EVENT category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — tried before SimpleArmTableParser for multi-level headers.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has 2+ header rows (multi-level header structure).
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if HeaderRowCount >= 2.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            return table.Header?.HeaderRowCount >= 2;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a multi-level AE table: study context from colspan row, arms from
        /// sub-header row. SOC divider rows set ParameterCategory.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            ClearDiagnostics();
            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract arms with study context from multi-level header
            var arms = extractMultilevelArms(table);
            if (arms.Count == 0)
                return observations;

            // Caption-derived StudyContext fallback for multilevel tables
            // whose colspan header path is missing or empty. Header-derived
            // StudyContext always wins — see the per-observation assignment
            // below.
            var captionStudyContext = extractStudyContextFromCaption(table.Caption);

            // Iterate data rows
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

                // Mid-body subpopulation header (e.g., "Female Patients Only" with per-arm
                // (N=…) cells). Replace overrides; arms missing an N fall back to arm.SampleSize.
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
                    // Map data cells to arms by column position
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
                        // Subpopulation override: if this section has per-arm N overrides,
                        // use them; otherwise fall back to the arm-level SampleSize.
                        o.ArmN = (arm.ColumnIndex.HasValue &&
                                  capturedSubpopArmNOverrides.TryGetValue(arm.ColumnIndex.Value, out var overrideN))
                            ? overrideN
                            : arm.SampleSize;
                        // Header-derived StudyContext always wins; caption fallback
                        // only fills the blank (e.g., when a colspan row is present
                        // but HeaderPath[0] ended up empty).
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

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Extracts arm definitions from a multi-level header. Uses HeaderPath[0]
        /// for study context and leaf text for arm name/N.
        /// </summary>
        private static List<ArmDefinition> extractMultilevelArms(ReconstructedTable table)
        {
            #region implementation

            return extractArmDefinitions(table);

            #endregion
        }

        #endregion Private Helpers
    }
}
