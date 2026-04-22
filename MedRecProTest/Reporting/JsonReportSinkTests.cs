using System.Text.Json;
using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Smoke-level tests for <see cref="JsonReportSink"/>. Covers null-path behaviour, file
    /// creation, append semantics, disposal flushing, and category-filter integrity. Mirrors
    /// the coverage of <c>MarkdownReportSinkTests</c> at a shallower depth — the NDJSON
    /// line shape is tested separately in <see cref="TableStandardizationJsonWriterTests"/>.
    /// </summary>
    /// <seealso cref="JsonReportSink"/>
    [TestClass]
    public class JsonReportSinkTests
    {
        #region Test Fixture

        private readonly List<string> _tempFiles = new();

        /**************************************************************/
        /// <summary>
        /// Returns a unique temp path under the OS temp directory. Registered for cleanup.
        /// </summary>
        private string newTempPath()
        {
            #region implementation

            var path = Path.Combine(Path.GetTempPath(), $"medrecpro-jsonsink-{Guid.NewGuid():N}.jsonl");
            _tempFiles.Add(path);
            return path;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Best-effort cleanup after each test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            #region implementation

            foreach (var path in _tempFiles)
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
            _tempFiles.Clear();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a minimal entry with a unique TID + one observation carrying enough fields
        /// for meaningful downstream verification.
        /// </summary>
        private static TableReportEntry buildEntry(int textTableId,
            TableCategory category = TableCategory.PK, int observationCount = 1,
            string? parameterSubtype = "single_dose")
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            for (int i = 0; i < observationCount; i++)
            {
                observations.Add(new ParsedObservation
                {
                    TextTableID = textTableId,
                    SourceRowSeq = i + 1,
                    SourceCellSeq = 1,
                    ParameterName = "Cmax",
                    ParameterSubtype = parameterSubtype,
                    RawValue = "5.5",
                    PrimaryValue = 5.5,
                    PrimaryValueType = "Mean"
                });
            }

            return new TableReportEntry
            {
                Table = new ReconstructedTable
                {
                    TextTableID = textTableId,
                    Caption = $"Table {textTableId}",
                    TotalColumnCount = 1,
                    TotalRowCount = 1,
                    Header = new ResolvedHeader
                    {
                        Columns = new List<HeaderColumn> { new() { LeafHeaderText = "Col" } }
                    },
                    Rows = new List<ReconstructedRow>()
                },
                Category = category,
                ParserName = observationCount > 0 ? "PkTableParser" : null,
                Observations = observations,
                ClaudeSkipped = true
            };

            #endregion
        }

        #endregion

        #region Null Path & Creation

        /**************************************************************/
        /// <summary>
        /// A null path returns null — callers pass the result straight through.
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_NullPath_ReturnsNull()
        {
            var sink = await JsonReportSink.CreateOrNullAsync(null, interactiveAppendPrompt: false);
            Assert.IsNull(sink);
        }

        /**************************************************************/
        /// <summary>
        /// A whitespace-only path returns null.
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_WhitespacePath_ReturnsNull()
        {
            var sink = await JsonReportSink.CreateOrNullAsync("  ", interactiveAppendPrompt: false);
            Assert.IsNull(sink);
        }

        /**************************************************************/
        /// <summary>
        /// A valid path creates an empty file (no session banner — JSON consumers parse
        /// line-by-line).
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_ValidPath_CreatesEmptyFile()
        {
            var path = newTempPath();

            await using (var sink = await JsonReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                Assert.IsNotNull(sink);
                Assert.AreEqual(path, sink!.Path);
            }

            Assert.IsTrue(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.AreEqual(string.Empty, content, "JSON sink emits no session banner — consumers parse NDJSON line-by-line");
        }

        #endregion

        #region Append & Disposal

        /**************************************************************/
        /// <summary>
        /// A single appended entry results in one NDJSON line with the expected TID. Verifies
        /// the producer-consumer channel drains on disposal.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_SingleEntry_WritesOneValidJsonLine()
        {
            var path = newTempPath();

            await using (var sink = await JsonReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(101));
            }

            var lines = (await File.ReadAllLinesAsync(path))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            Assert.AreEqual(1, lines.Length, "One observation → one JSON line");
            using var doc = JsonDocument.Parse(lines[0]);
            Assert.AreEqual(101, doc.RootElement.GetProperty("textTableId").GetInt32());
            Assert.AreEqual("single_dose",
                doc.RootElement.GetProperty("observation").GetProperty("parameterSubtype").GetString());
        }

        /**************************************************************/
        /// <summary>
        /// Multiple entries with different TIDs preserve order; each line is independently
        /// valid JSON. Confirms single-reader channel guarantees no inter-table interleaving.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_MultipleEntries_PreserveOrderAndStayValid()
        {
            var path = newTempPath();

            await using (var sink = await JsonReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(111, observationCount: 2));
                await sink.AppendAsync(buildEntry(222, observationCount: 1));
                await sink.AppendAsync(buildEntry(333, observationCount: 3));
            }

            var lines = (await File.ReadAllLinesAsync(path))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            Assert.AreEqual(6, lines.Length, "2 + 1 + 3 observations = 6 lines");

            var tids = lines
                .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("textTableId").GetInt32())
                .ToArray();
            CollectionAssert.AreEqual(new[] { 111, 111, 222, 333, 333, 333 }, tids,
                "Order preserved across entries; each observation expands to a line in sequence");
        }

        /**************************************************************/
        /// <summary>
        /// Category filter drops entries outside the allowed set silently — no lines written.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_CategoryFilter_DropsOutOfScopeEntries()
        {
            var path = newTempPath();
            var filter = new HashSet<TableCategory> { TableCategory.PK };

            await using (var sink = await JsonReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false, filter))
            {
                await sink!.AppendAsync(buildEntry(1, category: TableCategory.PK));
                await sink.AppendAsync(buildEntry(2, category: TableCategory.SKIP));
                await sink.AppendAsync(buildEntry(3, category: TableCategory.PK));
            }

            var lines = (await File.ReadAllLinesAsync(path))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            Assert.AreEqual(2, lines.Length, "SKIP entry dropped by filter");
            var tids = lines
                .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("textTableId").GetInt32())
                .ToArray();
            CollectionAssert.AreEqual(new[] { 1, 3 }, tids);
        }

        /**************************************************************/
        /// <summary>
        /// A zero-observation entry still produces a meta line — routing decisions remain
        /// visible even for SKIP tables.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_ZeroObservations_EmitsMetaLine()
        {
            var path = newTempPath();

            await using (var sink = await JsonReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(555, observationCount: 0));
            }

            var lines = (await File.ReadAllLinesAsync(path))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            Assert.AreEqual(1, lines.Length);
            using var doc = JsonDocument.Parse(lines[0]);
            Assert.AreEqual(555, doc.RootElement.GetProperty("textTableId").GetInt32());
            Assert.AreEqual(0, doc.RootElement.GetProperty("observationCount").GetInt32());
            Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("observation").ValueKind);
        }

        #endregion
    }
}
