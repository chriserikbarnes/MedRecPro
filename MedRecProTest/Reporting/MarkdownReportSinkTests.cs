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
        /// per-entry headers.
        /// </summary>
        private static TableReportEntry buildEntry(int textTableId)
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
                Category = TableCategory.SKIP,
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
