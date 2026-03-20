using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Routes reconstructed tables to the appropriate Stage 3 parser based on
    /// ParentSectionCode (LOINC) and structural characteristics.
    /// </summary>
    /// <remarks>
    /// ## Section Routing Map
    /// | ParentSectionCode | Category |
    /// |---|---|
    /// | 34090-1 (CLINICAL PHARMACOLOGY) | PK |
    /// | 43682-4 (12.3 Pharmacokinetics) | PK |
    /// | 34084-4 (ADVERSE REACTIONS) | ADVERSE_EVENT |
    /// | 34092-7 (CLINICAL STUDIES) | EFFICACY |
    /// | 42232-9 (PRECAUTIONS) | Caption-inspect → AE or EFFICACY |
    /// | 34068-7 (DOSAGE AND ADMINISTRATION) | DOSING |
    /// | 42229-5 (SPL UNCLASSIFIED) | SectionTitle fallback |
    ///
    /// ## Skip Detection
    /// - SectionCode 68498-5 (patient info leaflet)
    /// - TotalColumnCount ≤ 1
    /// - Caption contains "NDC", "How Supplied", "Inactive Ingredients"
    ///
    /// ## Parser Selection
    /// Within a category, parsers are tried in Priority order (lower first).
    /// The first parser where CanParse returns true is selected.
    /// </remarks>
    /// <seealso cref="ITableParserRouter"/>
    /// <seealso cref="ITableParser"/>
    public class TableParserRouter : ITableParserRouter
    {
        #region LOINC Section Code Constants

        // Clinical Pharmacology
        private const string CODE_CLINICAL_PHARMACOLOGY = "34090-1";
        private const string CODE_PHARMACOKINETICS = "43682-4";

        // Adverse Reactions
        private const string CODE_ADVERSE_REACTIONS = "34084-4";

        // Clinical Studies
        private const string CODE_CLINICAL_STUDIES = "34092-7";

        // Precautions
        private const string CODE_PRECAUTIONS = "42232-9";

        // Dosage and Administration
        private const string CODE_DOSAGE_ADMINISTRATION = "34068-7";

        // SPL Unclassified
        private const string CODE_UNCLASSIFIED = "42229-5";

        // Patient Information (skip)
        private const string CODE_PATIENT_INFO = "68498-5";

        // Skip caption keywords
        private static readonly string[] _skipCaptionKeywords = new[]
        {
            "NDC", "How Supplied", "Inactive Ingredients", "Storage", "Package"
        };

        #endregion LOINC Section Code Constants

        #region Fields

        /**************************************************************/
        /// <summary>
        /// Registered parsers grouped by category and sorted by priority.
        /// </summary>
        private readonly Dictionary<TableCategory, List<ITableParser>> _parsersByCategory;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the router with all available parsers, grouped by category
        /// and sorted by priority (lower = tried first).
        /// </summary>
        /// <param name="parsers">All registered <see cref="ITableParser"/> implementations.</param>
        public TableParserRouter(IEnumerable<ITableParser> parsers)
        {
            #region implementation

            _parsersByCategory = parsers
                .GroupBy(p => p.SupportedCategory)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(p => p.Priority).ToList());

            #endregion
        }

        #endregion Constructor

        #region ITableParserRouter Implementation

        /**************************************************************/
        /// <summary>
        /// Routes a table to a category and selects the appropriate parser.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>Tuple of (category, parser). Parser is null for SKIP.</returns>
        public (TableCategory category, ITableParser? parser) Route(ReconstructedTable table)
        {
            #region implementation

            // Skip detection
            if (shouldSkip(table))
                return (TableCategory.SKIP, null);

            // Determine category from section code
            var category = categorizeTable(table);

            if (category == TableCategory.SKIP)
                return (TableCategory.SKIP, null);

            // Select parser by priority within category
            var parser = selectParser(table, category);

            // If no specific parser found, try OTHER category parsers
            if (parser == null && category != TableCategory.OTHER)
            {
                parser = selectParser(table, TableCategory.OTHER);
                if (parser != null)
                    category = TableCategory.OTHER;
            }

            return (category, parser);

            #endregion
        }

        #endregion ITableParserRouter Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Determines if a table should be skipped entirely.
        /// </summary>
        private static bool shouldSkip(ReconstructedTable table)
        {
            #region implementation

            // Patient info leaflet section
            if (table.SectionCode == CODE_PATIENT_INFO ||
                table.ParentSectionCode == CODE_PATIENT_INFO)
                return true;

            // Single-column or no-column tables
            if (table.TotalColumnCount <= 1)
                return true;

            // Skip based on caption keywords
            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                foreach (var keyword in _skipCaptionKeywords)
                {
                    if (table.Caption.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps ParentSectionCode to TableCategory.
        /// </summary>
        private static TableCategory categorizeTable(ReconstructedTable table)
        {
            #region implementation

            var code = table.ParentSectionCode;

            return code switch
            {
                CODE_CLINICAL_PHARMACOLOGY => TableCategory.PK,
                CODE_PHARMACOKINETICS => TableCategory.PK,
                CODE_ADVERSE_REACTIONS => TableCategory.ADVERSE_EVENT,
                CODE_CLINICAL_STUDIES => TableCategory.EFFICACY,
                CODE_DOSAGE_ADMINISTRATION => TableCategory.DOSING,
                CODE_PRECAUTIONS => categorizeFromCaption(table),
                CODE_UNCLASSIFIED => categorizeFromSectionTitle(table),
                null => categorizeFromCaption(table),
                _ => categorizeFromCaption(table)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fallback categorization from SectionTitle keywords.
        /// </summary>
        private static TableCategory categorizeFromSectionTitle(ReconstructedTable table)
        {
            #region implementation

            var title = table.SectionTitle;
            if (string.IsNullOrWhiteSpace(title))
                return categorizeFromCaption(table);

            if (title.Contains("Pharmacokinetics", StringComparison.OrdinalIgnoreCase))
                return TableCategory.PK;
            if (title.Contains("Adverse", StringComparison.OrdinalIgnoreCase))
                return TableCategory.ADVERSE_EVENT;
            if (title.Contains("Dosage", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Dosing", StringComparison.OrdinalIgnoreCase))
                return TableCategory.DOSING;
            if (title.Contains("Drug Interaction", StringComparison.OrdinalIgnoreCase))
                return TableCategory.DRUG_INTERACTION;
            if (title.Contains("Clinical Studies", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Efficacy", StringComparison.OrdinalIgnoreCase))
                return TableCategory.EFFICACY;

            return categorizeFromCaption(table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fallback categorization from Caption keywords.
        /// </summary>
        private static TableCategory categorizeFromCaption(ReconstructedTable table)
        {
            #region implementation

            var caption = table.Caption;
            if (string.IsNullOrWhiteSpace(caption))
                return TableCategory.OTHER;

            if (caption.Contains("Adverse", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("Side Effect", StringComparison.OrdinalIgnoreCase))
                return TableCategory.ADVERSE_EVENT;
            if (caption.Contains("Pharmacokinetic", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("PK Parameter", StringComparison.OrdinalIgnoreCase))
                return TableCategory.PK;
            if (caption.Contains("Efficacy", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("Clinical", StringComparison.OrdinalIgnoreCase))
                return TableCategory.EFFICACY;
            if (caption.Contains("Bone Mineral Density", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("BMD", StringComparison.OrdinalIgnoreCase))
                return TableCategory.BMD;
            if (caption.Contains("Tissue", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("Ratio", StringComparison.OrdinalIgnoreCase))
                return TableCategory.TISSUE_DISTRIBUTION;

            return TableCategory.OTHER;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Selects the best parser for a category by testing CanParse in priority order.
        /// </summary>
        private ITableParser? selectParser(ReconstructedTable table, TableCategory category)
        {
            #region implementation

            if (!_parsersByCategory.TryGetValue(category, out var parsers))
                return null;

            return parsers.FirstOrDefault(p => p.CanParse(table));

            #endregion
        }

        #endregion Private Helpers
    }
}
