using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

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
                CODE_CLINICAL_PHARMACOLOGY => validatePkOrDowngrade(table),
                CODE_PHARMACOKINETICS => validatePkOrDowngrade(table),
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
                return validatePkOrDowngrade(table);
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
                return validatePkOrDowngrade(table);
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
        /// Validates a PK section-code or caption hint against actual table content.
        /// Returns <see cref="TableCategory.PK"/> when at least one canonical PK
        /// parameter name appears in a column header or row label; otherwise
        /// downgrades to <see cref="TableCategory.TEXT_DESCRIPTIVE"/> when the table
        /// is prose-heavy, or <see cref="TableCategory.OTHER"/> when the content
        /// shape is structured but non-PK.
        /// </summary>
        /// <remarks>
        /// Prevents the common failure where every table in LOINC 34090-1 / 43682-4
        /// gets tagged PK regardless of content — narrative DDI tables, hormone
        /// physiology summaries, and non-PK pharmacogenomic tables end up under PK
        /// and produce 0 observations from the parser. Content validation keeps PK
        /// reserved for tables the parser can actually decompose.
        /// </remarks>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Resolved category after content check.</returns>
        /// <seealso cref="PkParameterDictionary"/>
        /// <seealso cref="computeProseRatio"/>
        private static TableCategory validatePkOrDowngrade(ReconstructedTable table)
        {
            #region implementation

            // Count PK hits in header columns — uses ContainsPkParameter so modifier
            // phrases like "Change in AUC" or "Ratio of Cmax" still count as PK.
            int headerHits = 0;
            if (table.Header?.Columns != null)
            {
                foreach (var col in table.Header.Columns)
                {
                    if (PkParameterDictionary.ContainsPkParameter(col.LeafHeaderText))
                        headerHits++;
                }
            }

            // Count PK hits in col 0 row labels (row-label axis). Also uses the
            // contains-style match so long-form English labels and trailing unit
            // parens do not defeat recognition.
            int rowHits = 0;
            foreach (var row in table.DataRows())
            {
                var col0 = row.CellAt(0)?.CleanedText?.Trim();
                if (PkParameterDictionary.ContainsPkParameter(col0))
                    rowHits++;
            }

            // Confirm PK when either axis carries ≥1 canonical PK term
            if (headerHits + rowHits >= 1)
                return TableCategory.PK;

            // No PK content — decide between TEXT_DESCRIPTIVE (prose) and OTHER (structured)
            if (computeProseRatio(table) >= 0.30)
                return TableCategory.TEXT_DESCRIPTIVE;

            return TableCategory.OTHER;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Ratio of data cells that look like prose: cell length &gt; 120 characters
        /// OR whitespace-delimited word count &gt; 20. A table with predominantly
        /// narrative cells falls into <see cref="TableCategory.TEXT_DESCRIPTIVE"/>
        /// rather than <see cref="TableCategory.OTHER"/>.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Prose ratio in [0.0, 1.0]; 0.0 when the table has no data cells.</returns>
        private static double computeProseRatio(ReconstructedTable table)
        {
            #region implementation

            int proseCells = 0;
            int totalCells = 0;

            foreach (var row in table.DataRows())
            {
                if (row.Cells == null)
                    continue;

                foreach (var cell in row.Cells)
                {
                    var text = cell.CleanedText;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    totalCells++;

                    if (text.Length > 120)
                    {
                        proseCells++;
                        continue;
                    }

                    // Rough word count — split on whitespace, skip empties
                    var wordCount = 0;
                    foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        wordCount++;
                        if (wordCount > 20)
                            break;
                    }
                    if (wordCount > 20)
                        proseCells++;
                }
            }

            if (totalCells == 0)
                return 0.0;

            return (double)proseCells / totalCells;

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
