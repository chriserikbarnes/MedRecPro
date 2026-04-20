using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="MarkdownReportSink"/>. Covers null-path behaviour, file
    /// creation, append vs. overwrite semantics, disposal-flushing, and concurrent-producer
    /// integrity (no inter-entry interleaving).
    /// </summary>
    /// <seealso cref="MarkdownReportSink"/>
    [TestClass]
    public class MarkdownReportSinkTests
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

            var path = Path.Combine(Path.GetTempPath(), $"medrecpro-mdsink-{Guid.NewGuid():N}.md");
            _tempFiles.Add(path);
            return path;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes all temp files created during the test.
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
        /// Builds a trivial entry with a unique TextTableID so tests can count
        /// per-entry headers. Optional category override lets filter tests drive
        /// the sink with a mix of categories.
        /// </summary>
        /// <param name="textTableId">Identifier embedded in the H2 header.</param>
        /// <param name="category">Category assigned to the entry. Defaults to
        /// <see cref="TableCategory.SKIP"/> to preserve pre-existing test behaviour.</param>
        private static TableReportEntry buildEntry(int textTableId, TableCategory category = TableCategory.SKIP)
        {
            #region implementation

            return new TableReportEntry
            {
                Table = new ReconstructedTable
                {
                    TextTableID = textTableId,
                    Caption = $"Table {textTableId}",
                    TotalColumnCount = 1,
                    TotalRowCount = 0,
                    Header = new ResolvedHeader
                    {
                        Columns = new List<HeaderColumn> { new() { LeafHeaderText = "Col" } }
                    },
                    Rows = new List<ReconstructedRow>()
                },
                Category = category,
                ParserName = null,
                Observations = Array.Empty<ParsedObservation>(),
                ClaudeSkipped = true
            };

            #endregion
        }

        #endregion

        #region Null Path & Creation

        /**************************************************************/
        /// <summary>
        /// A null path returns null — callers can pass the result straight through without
        /// branching.
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_NullPath_ReturnsNull()
        {
            var sink = await MarkdownReportSink.CreateOrNullAsync(null, interactiveAppendPrompt: false);
            Assert.IsNull(sink);
        }

        /**************************************************************/
        /// <summary>
        /// A whitespace-only path returns null (treated as disabled).
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_WhitespacePath_ReturnsNull()
        {
            var sink = await MarkdownReportSink.CreateOrNullAsync("   ", interactiveAppendPrompt: false);
            Assert.IsNull(sink);
        }

        /**************************************************************/
        /// <summary>
        /// A valid path creates the file (with a session-started banner).
        /// </summary>
        [TestMethod]
        public async Task CreateOrNullAsync_NewPath_CreatesFile()
        {
            var path = newTempPath();

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                Assert.IsNotNull(sink);
                Assert.AreEqual(Path.GetFullPath(path), sink!.Path);
            }

            Assert.IsTrue(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "Session started");
        }

        #endregion

        #region Append & Order

        /**************************************************************/
        /// <summary>
        /// A single appended entry produces one H2 header in the file.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_SingleEntry_WritesH2Header()
        {
            var path = newTempPath();

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(42));
            }

            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "## TextTableID=42 — Table 42");
        }

        /**************************************************************/
        /// <summary>
        /// Sequential appends preserve call order in the file.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_MultipleEntriesSequential_AppendsInCallOrder()
        {
            var path = newTempPath();

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(1));
                await sink.AppendAsync(buildEntry(2));
                await sink.AppendAsync(buildEntry(3));
            }

            var content = await File.ReadAllTextAsync(path);
            var idx1 = content.IndexOf("TextTableID=1");
            var idx2 = content.IndexOf("TextTableID=2");
            var idx3 = content.IndexOf("TextTableID=3");

            Assert.IsTrue(idx1 > 0 && idx2 > idx1 && idx3 > idx2,
                $"Expected ascending order of entries but got indices {idx1}, {idx2}, {idx3}");
        }

        /**************************************************************/
        /// <summary>
        /// Many concurrent producers all enqueue entries and the resulting file contains
        /// exactly one H2 header per enqueued entry, with no split or interleaved sections.
        /// Verified by counting both header occurrences and per-entry trailing separators.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_ConcurrentProducers_PreservesPerEntryIntegrity()
        {
            var path = newTempPath();
            const int producers = 50;

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                var tasks = Enumerable.Range(0, producers)
                    .Select(i => Task.Run(async () => await sink!.AppendAsync(buildEntry(i))))
                    .ToArray();
                await Task.WhenAll(tasks);
            }

            var content = await File.ReadAllTextAsync(path);

            // Count H2 headers — there must be exactly `producers`
            var headerCount = CountOccurrences(content, "## TextTableID=");
            Assert.AreEqual(producers, headerCount,
                $"Expected {producers} per-entry headers, got {headerCount}.");

            // Each entry ends with a horizontal rule '---' so the count should match
            var ruleCount = CountOccurrences(content, "\n---");
            Assert.AreEqual(producers, ruleCount,
                $"Expected {producers} section separators, got {ruleCount}.");
        }

        #endregion

        #region File Mode Semantics

        /**************************************************************/
        /// <summary>
        /// Second sink opened on the same path (without interactive prompt) appends to the
        /// existing content rather than overwriting it.
        /// </summary>
        [TestMethod]
        public async Task AppendMode_ExistingFile_PreservesPreviousContent()
        {
            var path = newTempPath();

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(100));
            }

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                await sink!.AppendAsync(buildEntry(200));
            }

            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "TextTableID=100");
            StringAssert.Contains(content, "TextTableID=200");
            StringAssert.Contains(content, "Session appended");
        }

        /**************************************************************/
        /// <summary>
        /// DisposeAsync flushes any queued entries to disk before returning.
        /// </summary>
        [TestMethod]
        public async Task DisposeAsync_FlushesPendingEntries()
        {
            var path = newTempPath();

            var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false);
            Assert.IsNotNull(sink);

            // Queue a burst of entries without awaiting writes
            for (int i = 0; i < 10; i++)
            {
                await sink!.AppendAsync(buildEntry(i));
            }

            await sink!.DisposeAsync();

            var content = await File.ReadAllTextAsync(path);
            for (int i = 0; i < 10; i++)
            {
                StringAssert.Contains(content, $"TextTableID={i}");
            }
        }

        #endregion

        #region Category Filter

        /**************************************************************/
        /// <summary>
        /// With no category filter (default <c>null</c>), entries of every category land in
        /// the file. Regression guard: the filter parameter must not affect the un-filtered
        /// code path callers relied on before this change.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_NullCategoryFilter_WritesAllEntries()
        {
            var path = newTempPath();

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false))
            {
                Assert.IsNull(sink!.SelectedCategory, "Null filter should surface as null SelectedCategory.");
                await sink.AppendAsync(buildEntry(1, TableCategory.PK));
                await sink.AppendAsync(buildEntry(2, TableCategory.ADVERSE_EVENT));
                await sink.AppendAsync(buildEntry(3, TableCategory.EFFICACY));
            }

            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "TextTableID=1");
            StringAssert.Contains(content, "TextTableID=2");
            StringAssert.Contains(content, "TextTableID=3");
        }

        /**************************************************************/
        /// <summary>
        /// When a filter includes the entry's category, the entry is written to the file
        /// normally.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_CategoryFilterMatches_WritesMatchingEntry()
        {
            var path = newTempPath();
            IReadOnlySet<TableCategory> filter = new HashSet<TableCategory> { TableCategory.PK };

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false, filter))
            {
                Assert.AreEqual(TableCategory.PK, sink!.SelectedCategory);
                await sink.AppendAsync(buildEntry(77, TableCategory.PK));
            }

            var content = await File.ReadAllTextAsync(path);
            StringAssert.Contains(content, "## TextTableID=77");
        }

        /**************************************************************/
        /// <summary>
        /// When the filter excludes the entry's category, the entry is silently dropped —
        /// no placeholder comment, no H2 header, nothing.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_CategoryFilterDoesNotMatch_SilentlySkipsEntry()
        {
            var path = newTempPath();
            IReadOnlySet<TableCategory> filter = new HashSet<TableCategory> { TableCategory.PK };

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false, filter))
            {
                await sink!.AppendAsync(buildEntry(88, TableCategory.ADVERSE_EVENT));
            }

            var content = await File.ReadAllTextAsync(path);
            Assert.AreEqual(0, CountOccurrences(content, "## TextTableID="),
                "Non-matching entry must not produce any per-entry H2 header.");
            Assert.IsFalse(content.Contains("TextTableID=88"),
                "The filtered-out TextTableID must not appear anywhere in the file.");
        }

        /**************************************************************/
        /// <summary>
        /// Mixed input: three entries submitted, only the single matching one lands in the
        /// file and in the right place.
        /// </summary>
        [TestMethod]
        public async Task AppendAsync_CategoryFilterMixed_WritesOnlyMatches()
        {
            var path = newTempPath();
            IReadOnlySet<TableCategory> filter = new HashSet<TableCategory> { TableCategory.PK };

            await using (var sink = await MarkdownReportSink.CreateOrNullAsync(path, interactiveAppendPrompt: false, filter))
            {
                await sink!.AppendAsync(buildEntry(10, TableCategory.ADVERSE_EVENT));
                await sink.AppendAsync(buildEntry(20, TableCategory.PK));
                await sink.AppendAsync(buildEntry(30, TableCategory.EFFICACY));
            }

            var content = await File.ReadAllTextAsync(path);
            Assert.AreEqual(1, CountOccurrences(content, "## TextTableID="),
                "Exactly one entry (the PK one) should be written.");
            StringAssert.Contains(content, "TextTableID=20");
            Assert.IsFalse(content.Contains("TextTableID=10"));
            Assert.IsFalse(content.Contains("TextTableID=30"));
        }

        #endregion

        #region Helpers

        /**************************************************************/
        /// <summary>
        /// Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.
        /// </summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            #region implementation

            if (string.IsNullOrEmpty(needle)) return 0;
            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += needle.Length;
            }
            return count;

            #endregion
        }

        #endregion
    }
}
