using MedRecProConsole.Services;
using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableStandardizationService.BuildReportEntry"/> and for the
    /// end-to-end behaviour of a markdown sink driven by the service's batch aggregator shape.
    /// </summary>
    /// <remarks>
    /// The public pipeline entry points (<c>ExecuteParseSingleAsync</c>,
    /// <c>ExecuteParseWithStagesAsync</c>) build their own DI containers and connect to a database,
    /// so they are not unit-testable in isolation without refactoring. These tests focus on the
    /// parts that are unit-testable: the report entry factory and the sink round-trip using
    /// fixture data that mimics the service's post-pipeline state.
    /// </remarks>
    /// <seealso cref="TableStandardizationService.BuildReportEntry"/>
    /// <seealso cref="MarkdownReportSink"/>
    [TestClass]
    public class TableStandardizationServiceMarkdownTests
    {
        #region Fixture

        private readonly List<string> _tempFiles = new();

        /**************************************************************/
        /// <summary>
        /// Allocates a unique temp file for markdown output. Cleaned up in TestCleanup.
        /// </summary>
        private string newTempPath()
        {
            #region implementation

            var path = Path.Combine(Path.GetTempPath(), $"medrecpro-svcmd-{Guid.NewGuid():N}.md");
            _tempFiles.Add(path);
            return path;

            #endregion
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var path in _tempFiles)
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
            _tempFiles.Clear();
        }

        /**************************************************************/
        /// <summary>
        /// Builds a minimal reconstructed table for entry-building tests.
        /// </summary>
        private static ReconstructedTable buildTable(int id = 100) => new()
        {
            TextTableID = id,
            Caption = $"Fixture {id}",
            TotalColumnCount = 1,
            TotalRowCount = 0,
            Header = new ResolvedHeader
            {
                Columns = new List<HeaderColumn> { new() { LeafHeaderText = "C" } }
            },
            Rows = new List<ReconstructedRow>()
        };

        #endregion

        #region BuildReportEntry

        /**************************************************************/
        /// <summary>
        /// When Claude ran, before-flags are carried through so the writer can diff them.
        /// </summary>
        [TestMethod]
        public void BuildReportEntry_PostClaude_CapturesBeforeFlags()
        {
            // Arrange
            var observations = new List<ParsedObservation>
            {
                new() { SourceRowSeq = 1, SourceCellSeq = 2, ValidationFlags = "AI_CORRECTED:X" }
            };
            var before = new Dictionary<int, string?> { [1 * 10000 + 2] = null };

            // Act
            var entry = TableStandardizationService.BuildReportEntry(
                buildTable(), TableCategory.PK, "PkTableParser",
                observations, before, claudeSkipped: false);

            // Assert
            Assert.IsFalse(entry.ClaudeSkipped);
            Assert.IsNotNull(entry.BeforeClaudeFlags);
            Assert.AreEqual(1, entry.BeforeClaudeFlags!.Count);
            Assert.AreEqual(TableCategory.PK, entry.Category);
            Assert.AreEqual("PkTableParser", entry.ParserName);
            Assert.AreEqual(1, entry.Observations.Count);
        }

        /**************************************************************/
        /// <summary>
        /// When Claude was skipped, before-flags are discarded (null) even if supplied.
        /// </summary>
        [TestMethod]
        public void BuildReportEntry_NoClaudeRun_DropsBeforeFlags()
        {
            var before = new Dictionary<int, string?> { [12] = "SOMETHING" };

            var entry = TableStandardizationService.BuildReportEntry(
                buildTable(), TableCategory.ADVERSE_EVENT, "AdverseEventParser",
                new List<ParsedObservation>(), before, claudeSkipped: true);

            Assert.IsTrue(entry.ClaudeSkipped);
            Assert.IsNull(entry.BeforeClaudeFlags,
                "BeforeClaudeFlags must be null when ClaudeSkipped is true so the writer renders the skipped note.");
        }

        /**************************************************************/
        /// <summary>
        /// Null parser name (skip routing) passes through unchanged so the writer can render
        /// the "(none — skipped)" placeholder.
        /// </summary>
        [TestMethod]
        public void BuildReportEntry_NullParser_PreservesNull()
        {
            var entry = TableStandardizationService.BuildReportEntry(
                buildTable(), TableCategory.SKIP, null,
                new List<ParsedObservation>(), null, claudeSkipped: true);

            Assert.IsNull(entry.ParserName);
            Assert.AreEqual(TableCategory.SKIP, entry.Category);
        }

        #endregion

        #region End-to-End Sink Round-Trip

        /**************************************************************/
        /// <summary>
        /// A sink accepting several entries (as the batch aggregator would produce) writes one
        /// section per table with no interleaving, mirroring what <c>ExecuteParseWithStagesAsync</c>
        /// does internally.
        /// </summary>
        [TestMethod]
        public async Task BatchRoundTrip_ThreeEntries_ProducesThreeSections()
        {
            // Arrange — simulate a 3-table batch
            var path = newTempPath();
            var entries = new[]
            {
                TableStandardizationService.BuildReportEntry(
                    buildTable(101), TableCategory.PK, "PkTableParser",
                    new List<ParsedObservation>(), null, claudeSkipped: true),
                TableStandardizationService.BuildReportEntry(
                    buildTable(102), TableCategory.ADVERSE_EVENT, "AdverseEventParser",
                    new List<ParsedObservation>(), null, claudeSkipped: true),
                TableStandardizationService.BuildReportEntry(
                    buildTable(103), TableCategory.SKIP, null,
                    new List<ParsedObservation>(), null, claudeSkipped: true)
            };

            // Act
            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                foreach (var e in entries)
                    await sink!.AppendAsync(e);
            }

            // Assert
            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "TextTableID=101");
            StringAssert.Contains(content, "TextTableID=102");
            StringAssert.Contains(content, "TextTableID=103");
            StringAssert.Contains(content, "- **Category:** PK");
            StringAssert.Contains(content, "- **Category:** ADVERSE_EVENT");
            StringAssert.Contains(content, "- **Category:** SKIP");
        }

        #endregion
    }
}
