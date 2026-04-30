using System.Collections.Generic;
using System.Linq;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests category-specific router downgrade gates for arm-based parsers.
    /// </summary>
    /// <remarks>
    /// These tests keep structurally incompatible AE and Efficacy-shaped tables from
    /// entering parsers that would otherwise emit low-quality observations.
    /// </remarks>
    /// <seealso cref="TableParserRouter"/>
    [TestClass]
    public class TableParserRouterGateTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Creates a router with the production parser set used for category routing.
        /// </summary>
        private static TableParserRouter createRouter()
        {
            #region implementation

            return new TableParserRouter(new List<ITableParser>
            {
                new PkTableParser(),
                new SimpleArmTableParser(),
                new MultilevelAeTableParser(),
                new AeWithSocTableParser(),
                new EfficacyMultilevelTableParser(),
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a minimal arm-based table with caller-supplied body text.
        /// </summary>
        private static ReconstructedTable createArmTable(string caption, string parentSectionCode, string parentSectionTitle, params string[] bodyLabels)
        {
            #region implementation

            var rows = bodyLabels
                .Select(label => (Label: label, Drug: string.Empty, Placebo: string.Empty))
                .ToList();

            return createArmTableWithRows(caption, parentSectionCode, parentSectionTitle, rows);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a minimal arm-based table with caller-supplied row labels and
        /// outcome values.
        /// </summary>
        private static ReconstructedTable createArmTableWithRows(
            string? caption,
            string? parentSectionCode,
            string? parentSectionTitle,
            IReadOnlyList<(string Label, string Drug, string Placebo)> bodyRows,
            string? sectionTitle = null)
        {
            #region implementation

            var rows = bodyRows.Select((row, index) => new ReconstructedRow
            {
                SequenceNumberTextTableRow = index + 2,
                Classification = RowClassification.DataBody,
                AbsoluteRowIndex = index + 1,
                Cells = new List<ProcessedCell>
                {
                    new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = row.Label, CellType = "td" },
                    new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = row.Drug, CellType = "td" },
                    new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = row.Placebo, CellType = "td" },
                },
            }).ToList();

            return new ReconstructedTable
            {
                TextTableID = 7001,
                Caption = caption,
                ParentSectionCode = parentSectionCode,
                ParentSectionTitle = parentSectionTitle,
                SectionTitle = sectionTitle,
                TotalColumnCount = 3,
                TotalRowCount = rows.Count + 1,
                HasExplicitHeader = true,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 3,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Event", HeaderPath = new List<string> { "Event" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Drug X (N=100)", HeaderPath = new List<string> { "Drug X (N=100)" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "Placebo (N=100)", HeaderPath = new List<string> { "Placebo (N=100)" } },
                    },
                },
                Rows = rows,
            };

            #endregion
        }

        #endregion Test Helpers

        #region Downgrade Gates

        /**************************************************************/
        /// <summary>
        /// AE-shaped tables with only structural/text-only body rows are downgraded to
        /// <see cref="TableCategory.SKIP"/> before parser selection.
        /// </summary>
        [TestMethod]
        public void Route_AeTextOnlyStructuralBody_DowngradesToSkip()
        {
            #region implementation

            var router = createRouter();
            var table = createArmTable(
                "Adverse Reactions",
                "34084-4",
                "Adverse Reactions",
                "Patients with any adverse reaction",
                "Respiratory, thoracic, and mediastinal disorders");

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
            StringAssert.StartsWith(router.LastRouteReason, "DOWNGRADE:ADVERSE_EVENT:");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Efficacy-shaped tables with no parseable outcome cells are downgraded to
        /// <see cref="TableCategory.SKIP"/> before parser selection.
        /// </summary>
        [TestMethod]
        public void Route_EfficacyTextOnlyStructuralBody_DowngradesToSkip()
        {
            #region implementation

            var router = createRouter();
            var table = createArmTable(
                "Clinical Studies: CHD events",
                "34092-7",
                "Clinical Studies",
                "Event",
                "CHD events");

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
            StringAssert.StartsWith(router.LastRouteReason, "DOWNGRADE:EFFICACY:");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Caption-level instruction tables are routed to <see cref="TableCategory.SKIP"/>
        /// before category heuristics can send them to an arm-based parser.
        /// </summary>
        [TestMethod]
        public void Route_SkipCaptionKeyword_RoutesToSkipWithReason()
        {
            #region implementation

            var router = createRouter();
            var table = createArmTable(
                "Recommended Dosage and Administration",
                "34084-4",
                "Adverse Reactions",
                "Step",
                "Take with food");

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
            StringAssert.StartsWith(router.LastRouteReason, "SKIP:CaptionKeyword:");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Section-title clinical-study tables without parent LOINC metadata still
        /// route to Efficacy when the body has arm outcomes.
        /// </summary>
        [TestMethod]
        public void Route_EfficacySectionTitleWithoutParentCode_UsesSectionTitleFallback()
        {
            #region implementation

            var router = createRouter();
            var table = createArmTableWithRows(
                null,
                null,
                null,
                new List<(string Label, string Drug, string Placebo)>
                {
                    ("Pain Free at 2 hours", string.Empty, string.Empty),
                    ("n/N", "147/623", "96/646"),
                    ("% Responders", "23.6", "14.9"),
                    ("p-value", "<0.001", "↔"),
                },
                "14 CLINICAL STUDIES");

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.EFFICACY, category);
            Assert.IsNotNull(parser);
            Assert.IsInstanceOfType(parser, typeof(EfficacyMultilevelTableParser));
            Assert.IsNull(router.LastRouteReason);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Arm tables with SOC/header divider rows are not downgraded when multiple
        /// body rows still carry numeric outcomes.
        /// </summary>
        [TestMethod]
        public void Route_AeStructuralRowsWithOutcomeRows_DoesNotDowngrade()
        {
            #region implementation

            var router = createRouter();
            var table = createArmTableWithRows(
                "Table 1 Adverse Reactions",
                "34084-4",
                "Adverse Reactions",
                new List<(string Label, string Drug, string Placebo)>
                {
                    ("Body System (Event)", string.Empty, string.Empty),
                    ("Patients with any adverse reaction", "46", "52"),
                    ("Respiratory, thoracic, and mediastinal disorders", "-", "-"),
                    ("Cough Dyspnea", "3 2", "2 2"),
                    ("Headache", "3", "2"),
                });

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.ADVERSE_EVENT, category);
            Assert.IsNotNull(parser);
            Assert.IsNull(router.LastRouteReason);

            #endregion
        }

        #endregion Downgrade Gates
    }
}
