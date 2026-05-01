using System.Text.RegularExpressions;
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
    /// | 34090-1 (CLINICAL PHARMACOLOGY) | PK (DDI fast-path overrides) |
    /// | 43682-4 (12.3 Pharmacokinetics) | PK (DDI fast-path overrides) |
    /// | 34084-4 (ADVERSE REACTIONS) | ADVERSE_EVENT |
    /// | 34092-7 (CLINICAL STUDIES) | EFFICACY |
    /// | 42232-9 (PRECAUTIONS) | Caption-inspect → AE or EFFICACY |
    /// | 42229-5 (SPL UNCLASSIFIED) | SectionTitle fallback |
    ///
    /// Tables that do not classify into PK / ADVERSE_EVENT / EFFICACY /
    /// DRUG_INTERACTION fall through to <see cref="TableCategory.SKIP"/>.
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
    public class TableParserRouter : ITableParserRouter, ITableParserRouterDiagnostics
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

        // SPL Unclassified
        private const string CODE_UNCLASSIFIED = "42229-5";

        // Patient Information (skip)
        private const string CODE_PATIENT_INFO = "68498-5";

        // Skip caption keywords
        private static readonly string[] _skipCaptionKeywords = new[]
        {
            "NDC", "How Supplied", "Inactive Ingredients", "Storage", "Package",
            "Dosage and Administration", "Recommended Dosage", "Dose Modification",
            "Drug Exposure", "Patient Exposure", "Medication Guide",
            "Patient Information", "Instructions for Use", "Packaging",
            "Package Insert", "Preparation Instructions", "Administration Instructions"
        };

        #endregion LOINC Section Code Constants

        #region Fields

        /**************************************************************/
        /// <summary>
        /// Registered parsers grouped by category and sorted by priority.
        /// </summary>
        private readonly Dictionary<TableCategory, List<ITableParser>> _parsersByCategory;

        /**************************************************************/
        /// <inheritdoc/>
        public string? LastRouteReason { get; private set; }

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

            LastRouteReason = null;

            // Skip detection
            var skipReason = getSkipReason(table);
            if (skipReason != null)
            {
                LastRouteReason = skipReason;
                return (TableCategory.SKIP, null);
            }

            // Determine category from section code
            var category = categorizeTable(table);

            if (category == TableCategory.SKIP)
            {
                LastRouteReason ??= "SKIP:No viable parser category";
                return (TableCategory.SKIP, null);
            }

            // Select parser by priority within category
            var parser = selectParser(table, category);

            return (category, parser);

            #endregion
        }

        #endregion ITableParserRouter Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Determines if a table should be skipped entirely.
        /// </summary>
        private static string? getSkipReason(ReconstructedTable table)
        {
            #region implementation

            // Patient info leaflet section
            if (table.SectionCode == CODE_PATIENT_INFO ||
                table.ParentSectionCode == CODE_PATIENT_INFO)
                return "SKIP:Patient information section";

            // Single-column or no-column tables
            if (table.TotalColumnCount <= 1)
                return "SKIP:Single-column table";

            // Skip based on caption keywords
            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                foreach (var keyword in _skipCaptionKeywords)
                {
                    if (table.Caption.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return $"SKIP:CaptionKeyword:{keyword}";
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps ParentSectionCode to TableCategory.
        /// </summary>
        private TableCategory categorizeTable(ReconstructedTable table)
        {
            #region implementation

            var code = table.ParentSectionCode;

            return code switch
            {
                CODE_CLINICAL_PHARMACOLOGY => validatePkOrDowngrade(table),
                CODE_PHARMACOKINETICS => validatePkOrDowngrade(table),
                CODE_ADVERSE_REACTIONS => validateAeOrDowngrade(table),
                CODE_CLINICAL_STUDIES => validateEfficacyOrDowngrade(table),
                CODE_PRECAUTIONS => categorizeFromCaption(table),
                CODE_UNCLASSIFIED => categorizeFromSectionTitle(table),
                null => categorizeFromSectionTitle(table),
                _ => categorizeFromCaption(table)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fallback categorization from SectionTitle keywords.
        /// </summary>
        private TableCategory categorizeFromSectionTitle(ReconstructedTable table)
        {
            #region implementation

            var title = table.SectionTitle;
            if (string.IsNullOrWhiteSpace(title))
                return categorizeFromCaption(table);

            if (title.Contains("Pharmacokinetics", StringComparison.OrdinalIgnoreCase))
                return validatePkOrDowngrade(table);
            if (title.Contains("Adverse", StringComparison.OrdinalIgnoreCase))
                return validateAeOrDowngrade(table);
            if (title.Contains("Drug Interaction", StringComparison.OrdinalIgnoreCase))
                return TableCategory.DRUG_INTERACTION;
            if (title.Contains("Clinical Studies", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Efficacy", StringComparison.OrdinalIgnoreCase))
                return validateEfficacyOrDowngrade(table);

            return categorizeFromCaption(table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fallback categorization from Caption keywords.
        /// </summary>
        private TableCategory categorizeFromCaption(ReconstructedTable table)
        {
            #region implementation

            var caption = table.Caption;
            if (string.IsNullOrWhiteSpace(caption))
                return TableCategory.SKIP;

            // Wave 3 R8 — DDI keywords beat PK/Efficacy/etc. Drug-interaction captions
            // (`Drug Interaction`, `Co-administered`, `in the Presence of`) can appear
            // alongside `Pharmacokinetic` so the DDI check must run first.
            if (looksLikeDdi(table))
                return TableCategory.DRUG_INTERACTION;

            if (caption.Contains("Adverse", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("Side Effect", StringComparison.OrdinalIgnoreCase))
                return validateAeOrDowngrade(table);
            if (caption.Contains("Pharmacokinetic", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("PK Parameter", StringComparison.OrdinalIgnoreCase))
                return validatePkOrDowngrade(table);
            if (caption.Contains("Efficacy", StringComparison.OrdinalIgnoreCase) ||
                caption.Contains("Clinical", StringComparison.OrdinalIgnoreCase))
                return validateEfficacyOrDowngrade(table);

            return TableCategory.SKIP;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates an adverse-event section or caption hint against minimum
        /// arm-table structure before assigning the AE parser category.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>ADVERSE_EVENT when viable, otherwise SKIP.</returns>
        /// <seealso cref="validateArmBasedOrDowngrade"/>
        private TableCategory validateAeOrDowngrade(ReconstructedTable table)
        {
            #region implementation

            return validateArmBasedOrDowngrade(table, TableCategory.ADVERSE_EVENT);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates an efficacy section or caption hint against minimum arm-table
        /// structure before assigning the Efficacy parser category.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>EFFICACY when viable, otherwise SKIP.</returns>
        /// <seealso cref="validateArmBasedOrDowngrade"/>
        private TableCategory validateEfficacyOrDowngrade(ReconstructedTable table)
        {
            #region implementation

            return validateArmBasedOrDowngrade(table, TableCategory.EFFICACY);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Shared AE/Efficacy validator for arm-based parsers. Requires at least
        /// one recoverable arm, at least one parseable outcome cell, and no dominant
        /// header-like/text-only body pattern.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <param name="category">Target category under validation.</param>
        /// <returns>The target category when viable, otherwise SKIP.</returns>
        private TableCategory validateArmBasedOrDowngrade(ReconstructedTable table, TableCategory category)
        {
            #region implementation

            if (!hasRecoverableTreatmentArm(table))
            {
                LastRouteReason = $"DOWNGRADE:{category}:No recoverable treatment arm";
                return TableCategory.SKIP;
            }

            var dataRows = table.DataRows().ToList();
            if (!hasParseableOutcomeCell(dataRows))
            {
                LastRouteReason = $"DOWNGRADE:{category}:No parseable outcome cell";
                return TableCategory.SKIP;
            }

            if (hasDominantStructuralBodyPattern(dataRows))
            {
                LastRouteReason = $"DOWNGRADE:{category}:Dominant structural text-only body";
                return TableCategory.SKIP;
            }

            LastRouteReason = null;
            return category;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when any non-label header column can yield a real treatment
        /// arm from its leaf or parent header path.
        /// </summary>
        /// <param name="table">Table whose header should be inspected.</param>
        private static bool hasRecoverableTreatmentArm(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count <= 1)
                return false;

            foreach (var column in table.Header.Columns.Skip(1))
            {
                var candidate = recoverHeaderArmCandidate(column);
                if (!string.IsNullOrWhiteSpace(candidate))
                    return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recovers a treatment-arm candidate from a leaf or parent header.
        /// </summary>
        /// <param name="column">Header column to inspect.</param>
        private static string? recoverHeaderArmCandidate(HeaderColumn column)
        {
            #region implementation

            var leaf = column.LeafHeaderText?.Trim();
            if (!BaseTableParser.LooksLikeGenericArmLabelForRouting(leaf))
                return ValueParser.ParseArmHeader(leaf)?.Name ?? leaf;

            if (column.HeaderPath != null)
            {
                for (int i = column.HeaderPath.Count - 1; i >= 0; i--)
                {
                    var candidate = column.HeaderPath[i]?.Trim();
                    if (BaseTableParser.LooksLikeGenericArmLabelForRouting(candidate))
                        continue;

                    return ValueParser.ParseArmHeader(candidate)?.Name ?? candidate;
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when any body cell outside column 0 parses as a numeric or
        /// comparison outcome.
        /// </summary>
        /// <param name="dataRows">Candidate body rows.</param>
        private static bool hasParseableOutcomeCell(List<ReconstructedRow> dataRows)
        {
            #region implementation

            foreach (var row in dataRows)
            {
                if (row.Cells == null)
                    continue;

                foreach (var cell in row.Cells)
                {
                    if ((cell.ResolvedColumnStart ?? 0) == 0 || string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var parsed = ValueParser.Parse(cell.CleanedText);
                    if (parsed.PrimaryValue.HasValue || parsed.SecondaryValue.HasValue || parsed.PValue.HasValue)
                        return true;
                }
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects tables whose body is mostly structural/header-like text rows
        /// rather than observations.
        /// </summary>
        /// <param name="dataRows">Candidate body rows.</param>
        private static bool hasDominantStructuralBodyPattern(List<ReconstructedRow> dataRows)
        {
            #region implementation

            if (dataRows.Count == 0)
                return true;

            var structuralRows = 0;
            var outcomeRows = 0;
            var outcomeCells = 0;
            foreach (var row in dataRows)
            {
                if (row.Classification == RowClassification.SocDivider)
                {
                    structuralRows++;
                    continue;
                }

                var nonLabelCells = row.Cells?
                    .Where(c => (c.ResolvedColumnStart ?? 0) > 0 && !string.IsNullOrWhiteSpace(c.CleanedText))
                    .ToList() ?? new List<ProcessedCell>();

                if (nonLabelCells.Count == 0)
                {
                    structuralRows++;
                    continue;
                }

                var numericCells = nonLabelCells.Count(c =>
                {
                    var parsed = ValueParser.Parse(c.CleanedText);
                    return parsed.PrimaryValue.HasValue ||
                           parsed.SecondaryValue.HasValue ||
                           parsed.PValue.HasValue ||
                           looksLikeOutcomeBearingCell(c.CleanedText);
                });

                if (numericCells == 0)
                {
                    structuralRows++;
                    continue;
                }

                outcomeRows++;
                outcomeCells += numericCells;
            }

            if (outcomeRows >= 2 && outcomeCells >= 2)
                return false;

            return structuralRows >= Math.Max(2, (int)Math.Ceiling(dataRows.Count * 0.6));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects compact outcome strings that the validator should treat as
        /// data-bearing even when the parser later decomposes them with richer
        /// table context.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        private static bool looksLikeOutcomeBearingCell(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Trim();
            if (normalized is "-" or "—" or "–" or "↔")
                return false;

            return Regex.IsMatch(
                normalized,
                @"(?:[<>≤≥=]\s*)?\d[\d,.]*(?:\s*/\s*\d[\d,.]*)?(?:\s*(?:%|percent|patients?|subjects?|events?))?|(?:\d[\d,.]*\s+){1,}\d[\d,.]*",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a PK section-code or caption hint against actual table content.
        /// Returns <see cref="TableCategory.PK"/> when at least one canonical PK
        /// parameter name appears in a column header or row label, or
        /// <see cref="TableCategory.DRUG_INTERACTION"/> when DDI signals dominate;
        /// otherwise routes to <see cref="TableCategory.SKIP"/>.
        /// </summary>
        /// <remarks>
        /// Prevents the common failure where every table in LOINC 34090-1 / 43682-4
        /// gets tagged PK regardless of content — narrative DDI tables, hormone
        /// physiology summaries, and non-PK pharmacogenomic tables end up under PK
        /// and produce 0 observations from the parser. Content validation keeps PK
        /// reserved for tables the parser can actually decompose; everything else
        /// drops to SKIP.
        /// </remarks>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Resolved category after content check.</returns>
        /// <seealso cref="PkParameterDictionary"/>
        /// <seealso cref="looksLikeDdi"/>
        private static TableCategory validatePkOrDowngrade(ReconstructedTable table)
        {
            #region implementation

            // Wave 3 R8 — DDI downgrade. A PK-coded section (34090-1 / 43682-4) frequently
            // also hosts drug-interaction tables whose shape reuses PK parameter names
            // (AUC, Cmax, Tmax) but whose semantic is drug-on-drug effect, not a simple
            // subject-PK readout. Route these to DRUG_INTERACTION BEFORE the PK-content
            // check so the DDI-specific parser can decompose them correctly.
            if (looksLikeDdi(table))
                return TableCategory.DRUG_INTERACTION;

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

            // No PK content — drop to SKIP rather than parsing as a non-PK fallback
            return TableCategory.SKIP;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Wave 3 R8 — Strong-signal detector for drug-interaction tables. Returns
        /// <c>true</c> when the caption or section title contains explicit DDI
        /// keywords (<c>Drug Interaction</c>, <c>Co-administered</c>,
        /// <c>Coadministered</c>, <c>in the Presence of</c>, etc.). Used by
        /// <see cref="validatePkOrDowngrade"/> to re-route PK-coded sections whose
        /// tables are actually drug-interaction panels.
        /// </summary>
        /// <remarks>
        /// ## Strong signals (each sufficient)
        /// <list type="bullet">
        /// <item><description><c>Drug Interaction</c> phrase (any case).</description></item>
        /// <item><description><c>Co-administered</c> / <c>Coadministered</c>
        ///   / <c>Co-administration</c> / <c>Coadministration</c> — all hyphenation
        ///   variants.</description></item>
        /// <item><description><c>in the Presence of</c> — the "Effect of X in the
        ///   Presence of Y" DDI pattern.</description></item>
        /// <item><description><c>DDI</c> abbreviation (standalone token).</description></item>
        /// </list>
        ///
        /// ## Deliberately excluded (weak/ambiguous signals)
        /// <list type="bullet">
        /// <item><description><c>Effect of X on Pharmacokinetics of Y</c> alone —
        ///   also matches legitimate PK tables on population / demographic
        ///   stratification (e.g., "Effect of Renal Impairment on PK"). Requires
        ///   one of the strong signals above to qualify as DDI.</description></item>
        /// <item><description>Single occurrences of <c>inhibitor</c> / <c>inducer</c>
        ///   — too noisy (appears in many PK captions as mechanism of action).</description></item>
        /// </list>
        /// </remarks>
        /// <param name="table">The reconstructed table.</param>
        /// <returns><c>true</c> when caption or section title carries a strong DDI signal.</returns>
        internal static bool looksLikeDdi(ReconstructedTable table)
        {
            #region implementation

            if (table == null)
                return false;

            // Combine caption and section title for a single scan — either can carry
            // the signal. Null-safe concatenation keeps the regex work minimal.
            var caption = table.Caption ?? string.Empty;
            var sectionTitle = table.SectionTitle ?? string.Empty;
            if (caption.Length == 0 && sectionTitle.Length == 0)
                return false;

            // Spanning-header text is a second possible carrier: DDI tables often
            // have a banner row like "Coadministered Drug" before the column
            // parameter names.
            string? spanning = null;
            if (table.Header?.Columns != null)
            {
                foreach (var col in table.Header.Columns)
                {
                    var txt = col.LeafHeaderText;
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        spanning = string.IsNullOrEmpty(spanning) ? txt : spanning + " " + txt;
                    }
                }
            }
            spanning ??= string.Empty;

            return _ddiStrongSignalPattern.IsMatch(caption)
                || _ddiStrongSignalPattern.IsMatch(sectionTitle)
                || _ddiStrongSignalPattern.IsMatch(spanning);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Wave 3 R8 — Combined regex for strong DDI signals. One pre-compiled pattern
        /// with several alternations is cheaper than iterating independent checks.
        /// </summary>
        /// <remarks>
        /// The patterns use word boundaries where meaningful to avoid substring false
        /// positives (e.g., "coadministered" inside a longer word). <c>DDI</c> is
        /// wrapped in <c>\b</c> so it does not match words like "middi".
        /// </remarks>
        private static readonly Regex _ddiStrongSignalPattern = new(
            @"\bDrug[\s-]?Interaction"
          + @"|\bCo[\s-]?administ"                    // co-administered / coadministered / co-administration / coadministration
          + @"|\bin\s+the\s+[Pp]resence\s+of\b"
          + @"|\bDDI\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
