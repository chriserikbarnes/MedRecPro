using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableReconstructionService"/> (Stage 2 of the SPL Table Normalization pipeline).
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Constructor guard clauses
    /// - Footnote marker extraction from &lt;sup&gt; tags
    /// - HTML stripping and whitespace normalization
    /// - StyleCode attribute extraction
    /// - Row classification (ExplicitHeader, InferredHeader, ContinuationHeader, SocDivider, DataBody, Footer)
    /// - SOC divider detection
    /// - Column position resolution via occupancy grid (ColSpan/RowSpan)
    /// - Multi-level header resolution with column paths
    /// - Footnote extraction from footer rows
    /// - Full reconstruction integration tests with mocked data access
    ///
    /// Uses Moq to mock <see cref="ITableCellContextService"/> — no database needed.
    /// Internal helpers are tested directly via InternalsVisibleTo.
    /// </remarks>
    /// <seealso cref="TableReconstructionService"/>
    /// <seealso cref="ReconstructedTable"/>
    /// <seealso cref="ITableCellContextService"/>
    [TestClass]
    public class TableReconstructionServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Known DocumentGUID for test data.
        /// </summary>
        private static readonly Guid TestDocumentGuid = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

        /// <summary>
        /// Known SectionGUID for test data.
        /// </summary>
        private static readonly Guid TestSectionGuid = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="TableReconstructionService"/> with mocked dependencies.
        /// </summary>
        /// <param name="mockService">The mocked <see cref="ITableCellContextService"/>.</param>
        /// <returns>A configured service instance.</returns>
        private static TableReconstructionService createService(Mock<ITableCellContextService> mockService)
        {
            #region implementation
            var logger = new Mock<ILogger<TableReconstructionService>>();
            return new TableReconstructionService(mockService.Object, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal <see cref="TableCellContext"/> with the specified properties.
        /// </summary>
        private static TableCellContext createCell(
            int cellId, int rowId, int tableId,
            string? cellText = null, string? cellType = "td",
            int? sequenceNumber = null, int? colSpan = null, int? rowSpan = null,
            string? rowGroupType = "Body", int? seqRow = null)
        {
            #region implementation
            return new TableCellContext
            {
                TextTableCellID = cellId,
                TextTableRowID = rowId,
                TextTableID = tableId,
                CellText = cellText,
                CellType = cellType,
                SequenceNumber = sequenceNumber ?? 1,
                ColSpan = colSpan,
                RowSpan = rowSpan,
                RowGroupType = rowGroupType,
                SequenceNumberTextTableRow = seqRow ?? 1,
                DocumentGUID = TestDocumentGuid,
                SectionGUID = TestSectionGuid,
                Title = "Test Document",
                VersionNumber = 1,
                SectionCode = "34084-4",
                SectionType = "LOINC",
                SectionTitle = "ADVERSE REACTIONS",
                ParentSectionCode = "34084-4",
                ParentSectionTitle = "ADVERSE REACTIONS",
                LabelerName = "Test Labeler",
                Caption = "Table 1"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="ReconstructedRow"/> with the specified cells and properties.
        /// </summary>
        private static ReconstructedRow createRow(
            List<ProcessedCell> cells,
            string? rowGroupType = "Body",
            int? seqRow = null,
            int? rowId = null)
        {
            #region implementation
            return new ReconstructedRow
            {
                TextTableRowID = rowId,
                RowGroupType = rowGroupType,
                SequenceNumberTextTableRow = seqRow ?? 1,
                Cells = cells
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="ProcessedCell"/> with the specified properties.
        /// </summary>
        private static ProcessedCell createProcessedCell(
            string? cleanedText = null, string? cellType = "td",
            int? colSpan = null, int? rowSpan = null,
            int? resolvedStart = null, int? resolvedEnd = null,
            List<string>? footnoteMarkers = null)
        {
            #region implementation
            return new ProcessedCell
            {
                CleanedText = cleanedText,
                CellType = cellType,
                ColSpan = colSpan,
                RowSpan = rowSpan,
                ResolvedColumnStart = resolvedStart,
                ResolvedColumnEnd = resolvedEnd,
                FootnoteMarkers = footnoteMarkers ?? new List<string>()
            };
            #endregion
        }

        #endregion

        #region Constructor Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the constructor throws <see cref="ArgumentNullException"/>
        /// when the <see cref="ITableCellContextService"/> parameter is null.
        /// </summary>
        /// <seealso cref="TableReconstructionService"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullTableCellContextService_ThrowsArgumentNullException()
        {
            #region implementation
            var logger = new Mock<ILogger<TableReconstructionService>>();
            _ = new TableReconstructionService(null!, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the constructor throws <see cref="ArgumentNullException"/>
        /// when the logger parameter is null.
        /// </summary>
        /// <seealso cref="TableReconstructionService"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            #region implementation
            var mockService = new Mock<ITableCellContextService>();
            _ = new TableReconstructionService(mockService.Object, null!);
            #endregion
        }

        #endregion

        #region Footnote Marker Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a single &lt;sup&gt; tag extracts one marker.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_SingleSup_ReturnsMarker()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers("Value<sup>a</sup>");
            Assert.AreEqual(1, result.Count, "Should extract one marker");
            Assert.AreEqual("a", result[0], "Marker should be 'a'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple &lt;sup&gt; tags extract all markers.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_MultipleSups_ReturnsAll()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers("Val<sup>a</sup> text<sup>b</sup>");
            Assert.AreEqual(2, result.Count, "Should extract two markers");
            CollectionAssert.Contains(result, "a", "Should contain marker 'a'");
            CollectionAssert.Contains(result, "b", "Should contain marker 'b'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that comma-separated markers within a single &lt;sup&gt; tag are split.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_CommaSeparated_SplitsMarkers()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers("Val<sup>a,b</sup>");
            Assert.AreEqual(2, result.Count, "Should split into two markers");
            CollectionAssert.Contains(result, "a", "Should contain marker 'a'");
            CollectionAssert.Contains(result, "b", "Should contain marker 'b'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that HTML without &lt;sup&gt; tags returns an empty list.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_NoSup_ReturnsEmpty()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers("Plain text with no sup tags");
            Assert.AreEqual(0, result.Count, "Should return empty list");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that &lt;sup&gt; tags nested inside other SPL HTML elements are extracted correctly.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_NestedHtml_ExtractsCorrectly()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers(
                "<paragraph><content>Val<sup>1</sup></content></paragraph>");
            Assert.AreEqual(1, result.Count, "Should extract marker from nested HTML");
            Assert.AreEqual("1", result[0], "Marker should be '1'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Unicode symbol markers (†, ‡, etc.) are extracted correctly.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_SymbolMarkers_ExtractsCorrectly()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers("Val<sup>†</sup>");
            Assert.AreEqual(1, result.Count, "Should extract symbol marker");
            Assert.AreEqual("†", result[0], "Marker should be '†'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null input returns an empty list.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnoteMarkers"/>
        [TestMethod]
        public void ExtractFootnoteMarkers_NullInput_ReturnsEmpty()
        {
            #region implementation
            var result = TableReconstructionService.extractFootnoteMarkers(null);
            Assert.AreEqual(0, result.Count, "Null input should return empty list");
            #endregion
        }

        #endregion

        #region HTML Stripping Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that SPL paragraph and content wrapper tags are stripped to plain text.
        /// </summary>
        /// <seealso cref="TableReconstructionService.stripHtml"/>
        [TestMethod]
        public void StripHtml_ParagraphAndContent_ReturnsPlainText()
        {
            #region implementation
            var result = TableReconstructionService.stripHtml(
                "<paragraph><content styleCode=\"bold\">Hello World</content></paragraph>");
            Assert.AreEqual("Hello World", result, "Should strip all tags and return plain text");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that &lt;br /&gt; tags are replaced with spaces.
        /// </summary>
        /// <seealso cref="TableReconstructionService.stripHtml"/>
        [TestMethod]
        public void StripHtml_BrTag_ReplacedWithSpace()
        {
            #region implementation
            var result = TableReconstructionService.stripHtml("Line1<br />Line2");
            Assert.AreEqual("Line1 Line2", result, "br tags should become spaces");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null input passes through as null.
        /// </summary>
        /// <seealso cref="TableReconstructionService.stripHtml"/>
        [TestMethod]
        public void StripHtml_NullInput_ReturnsNull()
        {
            #region implementation
            var result = TableReconstructionService.stripHtml(null);
            Assert.IsNull(result, "Null input should return null");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that empty/whitespace input passes through.
        /// </summary>
        /// <seealso cref="TableReconstructionService.stripHtml"/>
        [TestMethod]
        public void StripHtml_EmptyInput_ReturnsEmpty()
        {
            #region implementation
            var result = TableReconstructionService.stripHtml("   ");
            Assert.AreEqual("   ", result, "Whitespace-only input should pass through");
            #endregion
        }

        #endregion

        #region StyleCode Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a styleCode="bold" attribute is extracted from content tags.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractStyleCode"/>
        [TestMethod]
        public void ExtractStyleCode_BoldContent_ReturnsBold()
        {
            #region implementation
            var result = TableReconstructionService.extractStyleCode(
                "<content styleCode=\"bold\">Text</content>");
            Assert.AreEqual("bold", result, "Should extract 'bold' styleCode");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that HTML without styleCode returns null.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractStyleCode"/>
        [TestMethod]
        public void ExtractStyleCode_NoStyleCode_ReturnsNull()
        {
            #region implementation
            var result = TableReconstructionService.extractStyleCode("<content>Plain</content>");
            Assert.IsNull(result, "Should return null when no styleCode present");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the first styleCode is extracted from nested elements with multiple styleCode attributes.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractStyleCode"/>
        [TestMethod]
        public void ExtractStyleCode_MultipleStyleCodes_ReturnsFirst()
        {
            #region implementation
            var result = TableReconstructionService.extractStyleCode(
                "<content styleCode=\"Botrule\"><content styleCode=\"bold\">Text</content></content>");
            Assert.AreEqual("Botrule", result, "Should return first styleCode found");
            #endregion
        }

        #endregion

        #region Row Classification Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with RowGroupType="Header" are classified as ExplicitHeader.
        /// </summary>
        /// <seealso cref="TableReconstructionService.classifyRows"/>
        [TestMethod]
        public void ClassifyRows_ExplicitHeaderRows_ClassifiedCorrectly()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell> { createProcessedCell("Col1"), createProcessedCell("Col2") },
                    rowGroupType: "Header", seqRow: 1),
                createRow(new List<ProcessedCell> { createProcessedCell("Data1"), createProcessedCell("Data2") },
                    rowGroupType: "Body", seqRow: 1)
            };

            TableReconstructionService.classifyRows(rows, 2);

            Assert.AreEqual(RowClassification.ExplicitHeader, rows[0].Classification,
                "Header row should be ExplicitHeader");
            Assert.AreEqual(RowClassification.DataBody, rows[1].Classification,
                "Body row after ExplicitHeader should be DataBody (not InferredHeader)");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when no ExplicitHeader rows exist, the first body row is promoted to InferredHeader.
        /// </summary>
        /// <seealso cref="TableReconstructionService.classifyRows"/>
        [TestMethod]
        public void ClassifyRows_NoExplicitHeader_FirstBodyRowInferred()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell> { createProcessedCell("Header1"), createProcessedCell("Header2") },
                    rowGroupType: "Body", seqRow: 1),
                createRow(new List<ProcessedCell> { createProcessedCell("Data1"), createProcessedCell("Data2") },
                    rowGroupType: "Body", seqRow: 2)
            };

            TableReconstructionService.classifyRows(rows, 2);

            Assert.AreEqual(RowClassification.InferredHeader, rows[0].Classification,
                "First body row should be InferredHeader");
            Assert.AreEqual(RowClassification.DataBody, rows[1].Classification,
                "Second body row should be DataBody");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Footer rows are classified correctly.
        /// </summary>
        /// <seealso cref="TableReconstructionService.classifyRows"/>
        [TestMethod]
        public void ClassifyRows_FooterRows_ClassifiedCorrectly()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell> { createProcessedCell("Col1") },
                    rowGroupType: "Body", seqRow: 1),
                createRow(new List<ProcessedCell> { createProcessedCell("a = footnote text") },
                    rowGroupType: "Footer", seqRow: 1)
            };

            TableReconstructionService.classifyRows(rows, 1);

            Assert.AreEqual(RowClassification.Footer, rows[1].Classification,
                "Footer row should be classified as Footer");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that consecutive body rows with all CellType="th" are classified as ContinuationHeader.
        /// </summary>
        /// <seealso cref="TableReconstructionService.classifyRows"/>
        [TestMethod]
        public void ClassifyRows_ConsecutiveThCells_ContinuationHeader()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Treatment", cellType: "th", colSpan: 2)
                }, rowGroupType: "Body", seqRow: 1),
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Drug A", cellType: "th"),
                    createProcessedCell("Placebo", cellType: "th")
                }, rowGroupType: "Body", seqRow: 2),
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("10%", cellType: "td"),
                    createProcessedCell("5%", cellType: "td")
                }, rowGroupType: "Body", seqRow: 3)
            };

            TableReconstructionService.classifyRows(rows, 2);

            Assert.AreEqual(RowClassification.InferredHeader, rows[0].Classification,
                "First body row should be InferredHeader");
            Assert.AreEqual(RowClassification.ContinuationHeader, rows[1].Classification,
                "Second body row with all th cells should be ContinuationHeader");
            Assert.AreEqual(RowClassification.DataBody, rows[2].Classification,
                "Third body row with td cells should be DataBody");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that normal body rows are classified as DataBody.
        /// </summary>
        /// <seealso cref="TableReconstructionService.classifyRows"/>
        [TestMethod]
        public void ClassifyRows_NormalBody_DataBody()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                    { createProcessedCell("Header1"), createProcessedCell("Header2") },
                    rowGroupType: "Header", seqRow: 1),
                createRow(new List<ProcessedCell>
                    { createProcessedCell("Data1"), createProcessedCell("Data2") },
                    rowGroupType: "Body", seqRow: 1),
                createRow(new List<ProcessedCell>
                    { createProcessedCell("Data3"), createProcessedCell("Data4") },
                    rowGroupType: "Body", seqRow: 2)
            };

            TableReconstructionService.classifyRows(rows, 2);

            Assert.AreEqual(RowClassification.DataBody, rows[1].Classification,
                "Body rows with explicit header should be DataBody");
            Assert.AreEqual(RowClassification.DataBody, rows[2].Classification,
                "Second body row should be DataBody");
            #endregion
        }

        #endregion

        #region SOC Divider Detection Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a single cell spanning the full table width is detected as a SOC divider.
        /// </summary>
        /// <seealso cref="TableReconstructionService.detectSocDivider"/>
        [TestMethod]
        public void DetectSocDivider_SingleCellFullSpan_ReturnsTrue()
        {
            #region implementation
            var row = createRow(new List<ProcessedCell>
            {
                createProcessedCell("Body as a Whole", colSpan: 5)
            });

            var result = TableReconstructionService.detectSocDivider(row, 5);

            Assert.IsTrue(result, "Single cell spanning full width should be SOC divider");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with multiple cells are not SOC dividers.
        /// </summary>
        /// <seealso cref="TableReconstructionService.detectSocDivider"/>
        [TestMethod]
        public void DetectSocDivider_MultipleCells_ReturnsFalse()
        {
            #region implementation
            var row = createRow(new List<ProcessedCell>
            {
                createProcessedCell("Cell1"),
                createProcessedCell("Cell2"),
                createProcessedCell("Cell3")
            });

            var result = TableReconstructionService.detectSocDivider(row, 3);

            Assert.IsFalse(result, "Multiple cells should not be SOC divider");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a single spanning cell with empty text is not a SOC divider.
        /// </summary>
        /// <seealso cref="TableReconstructionService.detectSocDivider"/>
        [TestMethod]
        public void DetectSocDivider_EmptyCell_ReturnsFalse()
        {
            #region implementation
            var row = createRow(new List<ProcessedCell>
            {
                createProcessedCell(null, colSpan: 5)
            });

            var result = TableReconstructionService.detectSocDivider(row, 5);

            Assert.IsFalse(result, "Empty spanning cell should not be SOC divider");
            #endregion
        }

        #endregion

        #region Column Position Resolution Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that cells without spans are assigned sequential column positions.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveColumnPositions"/>
        [TestMethod]
        public void ResolveColumnPositions_NoSpans_Sequential()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("A"),
                    createProcessedCell("B"),
                    createProcessedCell("C")
                })
            };

            TableReconstructionService.resolveColumnPositions(rows, 3);

            Assert.AreEqual(0, rows[0].Cells![0].ResolvedColumnStart, "First cell starts at 0");
            Assert.AreEqual(1, rows[0].Cells![0].ResolvedColumnEnd, "First cell ends at 1");
            Assert.AreEqual(1, rows[0].Cells![1].ResolvedColumnStart, "Second cell starts at 1");
            Assert.AreEqual(2, rows[0].Cells![1].ResolvedColumnEnd, "Second cell ends at 2");
            Assert.AreEqual(2, rows[0].Cells![2].ResolvedColumnStart, "Third cell starts at 2");
            Assert.AreEqual(3, rows[0].Cells![2].ResolvedColumnEnd, "Third cell ends at 3");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that ColSpan causes subsequent cells to shift right.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveColumnPositions"/>
        [TestMethod]
        public void ResolveColumnPositions_WithColSpan_SkipsColumns()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Wide", colSpan: 2),
                    createProcessedCell("Normal")
                })
            };

            TableReconstructionService.resolveColumnPositions(rows, 3);

            Assert.AreEqual(0, rows[0].Cells![0].ResolvedColumnStart, "Wide cell starts at 0");
            Assert.AreEqual(2, rows[0].Cells![0].ResolvedColumnEnd, "Wide cell ends at 2 (span=2)");
            Assert.AreEqual(2, rows[0].Cells![1].ResolvedColumnStart, "Normal cell starts at 2");
            Assert.AreEqual(3, rows[0].Cells![1].ResolvedColumnEnd, "Normal cell ends at 3");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that RowSpan from a cell in row 0 occupies column slots in row 1.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveColumnPositions"/>
        [TestMethod]
        public void ResolveColumnPositions_WithRowSpan_OccupiesNextRow()
        {
            #region implementation
            // Row 0: [Tall(rowspan=2)] [B]
            // Row 1:                   [C]  (column 0 occupied by Tall's rowspan)
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Tall", rowSpan: 2),
                    createProcessedCell("B")
                }),
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("C")
                })
            };

            TableReconstructionService.resolveColumnPositions(rows, 2);

            Assert.AreEqual(0, rows[0].Cells![0].ResolvedColumnStart, "Tall cell starts at 0");
            Assert.AreEqual(1, rows[0].Cells![1].ResolvedColumnStart, "B starts at 1");
            Assert.AreEqual(1, rows[1].Cells![0].ResolvedColumnStart,
                "C starts at 1 (column 0 occupied by Tall's rowspan)");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies correct resolution of a complex multi-row multi-column span scenario.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveColumnPositions"/>
        [TestMethod]
        public void ResolveColumnPositions_ComplexSpans_ResolvesCorrectly()
        {
            #region implementation
            // Row 0: [Label] [Treatment(colspan=2)]
            // Row 1: [---]   [Drug A] [Placebo]   (label column empty due to rowspan? No, just 2 cells)
            // Simulating: Row 0 has "Label" at col 0 (rowspan=2), "Treatment" at col 1 (colspan=2)
            //             Row 1 has "Drug A" at col 1, "Placebo" at col 2
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Label", rowSpan: 2),
                    createProcessedCell("Treatment", colSpan: 2)
                }),
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Drug A"),
                    createProcessedCell("Placebo")
                })
            };

            TableReconstructionService.resolveColumnPositions(rows, 3);

            // Row 0
            Assert.AreEqual(0, rows[0].Cells![0].ResolvedColumnStart, "Label starts at 0");
            Assert.AreEqual(1, rows[0].Cells![1].ResolvedColumnStart, "Treatment starts at 1");
            Assert.AreEqual(3, rows[0].Cells![1].ResolvedColumnEnd, "Treatment ends at 3 (span=2)");

            // Row 1: column 0 occupied by Label's rowspan
            Assert.AreEqual(1, rows[1].Cells![0].ResolvedColumnStart,
                "Drug A starts at 1 (col 0 occupied by Label rowspan)");
            Assert.AreEqual(2, rows[1].Cells![1].ResolvedColumnStart, "Placebo starts at 2");
            #endregion
        }

        #endregion

        #region Header Resolution Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a single header row produces leaf-only columns with no path nesting.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveHeaders"/>
        [TestMethod]
        public void ResolveHeaders_SingleRow_LeafOnly()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Name", resolvedStart: 0, resolvedEnd: 1),
                    createProcessedCell("Value", resolvedStart: 1, resolvedEnd: 2)
                })
            };
            rows[0].Classification = RowClassification.InferredHeader;
            rows[0].AbsoluteRowIndex = 0;

            var header = TableReconstructionService.resolveHeaders(rows, 2);

            Assert.AreEqual(1, header.HeaderRowCount, "Should have 1 header row");
            Assert.AreEqual(2, header.Columns!.Count, "Should have 2 columns");
            Assert.AreEqual("Name", header.Columns[0].LeafHeaderText, "First column leaf text");
            Assert.AreEqual("Value", header.Columns[1].LeafHeaderText, "Second column leaf text");
            Assert.AreEqual("Name", header.Columns[0].CombinedHeaderText,
                "Single-level combined text should equal leaf");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a two-level header builds correct paths (e.g., "Treatment > Drug A").
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveHeaders"/>
        [TestMethod]
        public void ResolveHeaders_TwoLevel_BuildsCorrectPath()
        {
            #region implementation
            // Row 0: [empty(col0)] [Treatment(col1-2, span=2)]
            // Row 1: [Adverse(col0)] [Drug A(col1)] [Placebo(col2)]
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("", resolvedStart: 0, resolvedEnd: 1),
                    createProcessedCell("Treatment", resolvedStart: 1, resolvedEnd: 3)
                }),
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("Adverse Reaction", resolvedStart: 0, resolvedEnd: 1),
                    createProcessedCell("Drug A", resolvedStart: 1, resolvedEnd: 2),
                    createProcessedCell("Placebo", resolvedStart: 2, resolvedEnd: 3)
                })
            };
            rows[0].Classification = RowClassification.InferredHeader;
            rows[0].AbsoluteRowIndex = 0;
            rows[1].Classification = RowClassification.ContinuationHeader;
            rows[1].AbsoluteRowIndex = 1;

            var header = TableReconstructionService.resolveHeaders(rows, 3);

            Assert.AreEqual(2, header.HeaderRowCount, "Should have 2 header rows");
            Assert.AreEqual(3, header.Columns!.Count, "Should have 3 columns");

            // Column 0: only "Adverse Reaction" (row 0 had empty cell)
            Assert.AreEqual("Adverse Reaction", header.Columns[0].LeafHeaderText);

            // Column 1: Treatment > Drug A
            Assert.AreEqual("Drug A", header.Columns[1].LeafHeaderText);
            Assert.AreEqual("Treatment > Drug A", header.Columns[1].CombinedHeaderText,
                "Two-level path should be joined with ' > '");

            // Column 2: Treatment > Placebo
            Assert.AreEqual("Placebo", header.Columns[2].LeafHeaderText);
            Assert.AreEqual("Treatment > Placebo", header.Columns[2].CombinedHeaderText);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that tables with no header rows return an empty header structure.
        /// </summary>
        /// <seealso cref="TableReconstructionService.resolveHeaders"/>
        [TestMethod]
        public void ResolveHeaders_NoHeaders_ReturnsEmptyStructure()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell> { createProcessedCell("Data") })
            };
            rows[0].Classification = RowClassification.DataBody;
            rows[0].AbsoluteRowIndex = 0;

            var header = TableReconstructionService.resolveHeaders(rows, 1);

            Assert.AreEqual(0, header.HeaderRowCount, "Should have 0 header rows");
            Assert.AreEqual(0, header.Columns!.Count, "Should have no columns");
            #endregion
        }

        #endregion

        #region Footnote Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that footer rows with marker-text patterns are parsed correctly.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnotes"/>
        [TestMethod]
        public void ExtractFootnotes_MarkerText_ParsesCorrectly()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell>
                {
                    createProcessedCell("a Includes patients who discontinued early")
                })
            };
            rows[0].Classification = RowClassification.Footer;
            rows[0].AbsoluteRowIndex = 2;

            var footnotes = TableReconstructionService.extractFootnotes(rows);

            Assert.AreEqual(1, footnotes.Count, "Should parse one footnote");
            Assert.IsTrue(footnotes.ContainsKey("a"), "Marker should be 'a'");
            Assert.AreEqual("Includes patients who discontinued early", footnotes["a"],
                "Footnote text should be extracted");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that tables without footer rows return an empty dictionary.
        /// </summary>
        /// <seealso cref="TableReconstructionService.extractFootnotes"/>
        [TestMethod]
        public void ExtractFootnotes_NoFooter_ReturnsEmpty()
        {
            #region implementation
            var rows = new List<ReconstructedRow>
            {
                createRow(new List<ProcessedCell> { createProcessedCell("Data") })
            };
            rows[0].Classification = RowClassification.DataBody;

            var footnotes = TableReconstructionService.extractFootnotes(rows);

            Assert.AreEqual(0, footnotes.Count, "No footer rows should produce empty dictionary");
            #endregion
        }

        #endregion

        #region Full Reconstruction Integration Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid TextTableID produces a fully reconstructed table
        /// with classified rows, resolved headers, and document context.
        /// </summary>
        /// <seealso cref="TableReconstructionService.ReconstructTableAsync"/>
        [TestMethod]
        public async Task ReconstructTableAsync_ValidTableId_ReturnsReconstructedTable()
        {
            #region implementation
            var mockService = new Mock<ITableCellContextService>();
            var cells = new List<TableCellContext>
            {
                createCell(1, 100, 1, "Header1", "td", 1, rowGroupType: "Body", seqRow: 1),
                createCell(2, 100, 1, "Header2", "td", 2, rowGroupType: "Body", seqRow: 1),
                createCell(3, 101, 1, "Data1", "td", 1, rowGroupType: "Body", seqRow: 2),
                createCell(4, 101, 1, "Data2", "td", 2, rowGroupType: "Body", seqRow: 2)
            };

            mockService.Setup(s => s.GetTableCellContextsAsync(
                    It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cells);

            var service = createService(mockService);
            var result = await service.ReconstructTableAsync(1);

            Assert.IsNotNull(result, "Should return a reconstructed table");
            Assert.AreEqual(1, result.TextTableID, "TextTableID should match");
            Assert.AreEqual(2, result.TotalRowCount, "Should have 2 rows");
            Assert.AreEqual(2, result.TotalColumnCount, "Should have 2 columns");
            Assert.IsTrue(result.HasInferredHeader!.Value, "Should have inferred header");
            Assert.AreEqual(TestDocumentGuid, result.DocumentGUID, "Document context should propagate");
            Assert.AreEqual("Test Labeler", result.LabelerName, "Labeler should propagate");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty result returns null.
        /// </summary>
        /// <seealso cref="TableReconstructionService.ReconstructTableAsync"/>
        [TestMethod]
        public async Task ReconstructTableAsync_EmptyResult_ReturnsNull()
        {
            #region implementation
            var mockService = new Mock<ITableCellContextService>();
            mockService.Setup(s => s.GetTableCellContextsAsync(
                    It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TableCellContext>());

            var service = createService(mockService);
            var result = await service.ReconstructTableAsync(999);

            Assert.IsNull(result, "Empty cells should return null");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple table groups are all reconstructed.
        /// </summary>
        /// <seealso cref="TableReconstructionService.ReconstructTablesAsync"/>
        [TestMethod]
        public async Task ReconstructTablesAsync_MultipleGroups_ReturnsAll()
        {
            #region implementation
            var mockService = new Mock<ITableCellContextService>();
            var grouped = new Dictionary<int, List<TableCellContext>>
            {
                {
                    1, new List<TableCellContext>
                    {
                        createCell(1, 100, 1, "Cell1", seqRow: 1),
                        createCell(2, 101, 1, "Cell2", seqRow: 2)
                    }
                },
                {
                    2, new List<TableCellContext>
                    {
                        createCell(3, 200, 2, "Cell3", seqRow: 1),
                        createCell(4, 201, 2, "Cell4", seqRow: 2)
                    }
                }
            };

            mockService.Setup(s => s.GetTableCellContextsGroupedByTableAsync(
                    It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(grouped);

            var service = createService(mockService);
            var result = await service.ReconstructTablesAsync();

            Assert.AreEqual(2, result.Count, "Should reconstruct both tables");
            Assert.IsTrue(result.Any(t => t.TextTableID == 1), "Should contain table 1");
            Assert.IsTrue(result.Any(t => t.TextTableID == 2), "Should contain table 2");
            #endregion
        }

        #endregion
    }
}
