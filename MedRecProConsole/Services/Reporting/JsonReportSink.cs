using System.Text;
using System.Threading.Channels;
using MedRecProImportClass.Models;
using Spectre.Console;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Append-only NDJSON file writer for <see cref="TableReportEntry"/> snapshots — the
    /// structured-data companion to <see cref="MarkdownReportSink"/>. Each observation
    /// becomes one JSON line carrying both table-level context and the full
    /// <see cref="ParsedObservation"/> payload, enabling programmatic diffing
    /// (e.g. <c>jq -c 'select(.observation.parameterSubtype == "single_dose")'</c>).
    /// </summary>
    /// <remarks>
    /// ## Lifecycle
    /// <list type="number">
    ///   <item><description><see cref="CreateOrNullAsync"/> opens the file (after an optional
    ///     append/overwrite prompt when the file already exists) and starts the consumer.</description></item>
    ///   <item><description>Producers call <see cref="AppendAsync"/>; entries queue immediately.</description></item>
    ///   <item><description><see cref="DisposeAsync"/> completes the channel and awaits the consumer
    ///     to drain and flush before returning.</description></item>
    /// </list>
    ///
    /// Safe to share across concurrent producers — entries are serialized through an
    /// internal channel and written sequentially by a single consumer task, so no two
    /// tables ever interleave on disk.
    ///
    /// When <paramref>path</paramref> is null or whitespace, <see cref="CreateOrNullAsync"/>
    /// returns <c>null</c> so callers can pass the result straight into
    /// <c>service.ExecuteParseSingleAsync(..., jsonSink: sink)</c> without branching.
    /// </remarks>
    /// <seealso cref="TableReportEntry"/>
    /// <seealso cref="TableStandardizationJsonWriter"/>
    /// <seealso cref="MarkdownReportSink"/>
    public sealed class JsonReportSink : IAsyncDisposable
    {
        #region private fields

        private readonly Channel<TableReportEntry> _channel;
        private readonly StreamWriter _writer;
        private readonly Task _consumerTask;
        private readonly IReadOnlySet<TableCategory>? _categoryFilter;
        private bool _disposed;

        #endregion

        #region public properties

        /**************************************************************/
        /// <summary>
        /// Resolved absolute path of the NDJSON file being written.
        /// </summary>
        public string Path { get; }

        /**************************************************************/
        /// <summary>
        /// The single category this sink is filtering to, or <c>null</c> when no filter is
        /// active (all categories pass through).
        /// </summary>
        /// <remarks>
        /// Mirrors <see cref="MarkdownReportSink.SelectedCategory"/>. When multiple values
        /// are present in the filter, returns the first enumerated value.
        /// </remarks>
        public TableCategory? SelectedCategory =>
            _categoryFilter is null ? null : _categoryFilter.FirstOrDefault();

        #endregion

        #region construction

        private JsonReportSink(string path, StreamWriter writer, IReadOnlySet<TableCategory>? categoryFilter)
        {
            #region implementation

            Path = path;
            _writer = writer;
            _categoryFilter = categoryFilter;

            _channel = Channel.CreateUnbounded<TableReportEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            _consumerTask = Task.Run(consumeAsync);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a sink for the given path, or returns null when the path is null/blank.
        /// </summary>
        /// <param name="path">Target NDJSON file path (conventionally <c>.jsonl</c>).
        /// Relative paths resolve against the current working directory. Parent
        /// directories are created as needed.</param>
        /// <param name="interactiveAppendPrompt">When true and the target file already exists,
        /// the user is prompted via <see cref="AnsiConsole"/> to choose append or overwrite.
        /// CLI/quiet callers should pass false (always append silently).</param>
        /// <param name="categoryFilter">Optional set of <see cref="TableCategory"/> values to
        /// restrict writing. Null (default) lets every entry through. When non-null,
        /// <see cref="AppendAsync"/> silently drops entries whose category is not in the set
        /// — no placeholder line is written.</param>
        /// <returns>Open sink, or null when <paramref name="path"/> is null/whitespace.</returns>
        public static Task<JsonReportSink?> CreateOrNullAsync(
            string? path,
            bool interactiveAppendPrompt,
            IReadOnlySet<TableCategory>? categoryFilter = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult<JsonReportSink?>(null);

            var resolved = System.IO.Path.GetFullPath(path);
            var parent = System.IO.Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var mode = FileMode.Append;
            if (File.Exists(resolved) && interactiveAppendPrompt)
            {
                // Default = append (matches the --json-log semantics).
                var append = AnsiConsole.Confirm(
                    $"[yellow]JSON log exists:[/] [white]{Markup.Escape(resolved)}[/]\n" +
                    "[green]Append[/] to existing content? [grey](No = overwrite)[/]",
                    defaultValue: true);

                mode = append ? FileMode.Append : FileMode.Create;
            }

            var stream = new FileStream(resolved, mode, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = false
            };

            return Task.FromResult<JsonReportSink?>(new JsonReportSink(resolved, writer, categoryFilter));

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Enqueues an entry for serialization. Returns once the entry is accepted by the
        /// internal channel — the actual disk write happens on the consumer task.
        /// </summary>
        /// <param name="entry">The per-table snapshot to append.</param>
        /// <param name="ct">Cancellation token; cancels the enqueue, not pending writes.</param>
        /// <exception cref="ObjectDisposedException">Thrown when called after <see cref="DisposeAsync"/>.</exception>
        /// <remarks>
        /// When a category filter is active and <paramref name="entry"/>'s category is not in
        /// the set, the call completes synchronously with no side effects — the entry is not
        /// enqueued and nothing is written to disk.
        /// </remarks>
        public ValueTask AppendAsync(TableReportEntry entry, CancellationToken ct = default)
        {
            #region implementation

            if (_disposed)
                throw new ObjectDisposedException(nameof(JsonReportSink));
            ArgumentNullException.ThrowIfNull(entry);

            // Silently skip entries outside the user-chosen category filter.
            if (_categoryFilter is not null && !_categoryFilter.Contains(entry.Category))
                return ValueTask.CompletedTask;

            return _channel.Writer.WriteAsync(entry, ct);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Completes the channel, waits for the consumer to drain all queued entries, then
        /// flushes and closes the underlying file.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            #region implementation

            if (_disposed)
                return;
            _disposed = true;

            _channel.Writer.TryComplete();
            try
            {
                await _consumerTask.ConfigureAwait(false);
            }
            finally
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Consumer loop — reads entries from the channel and writes them to the file one at
        /// a time. The single-reader constraint plus sequential writes guarantees no
        /// inter-table interleaving even under heavy parallel producer load.
        /// </summary>
        private async Task consumeAsync()
        {
            #region implementation

            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var entry))
                {
                    var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
                    await _writer.WriteAsync(ndjson).ConfigureAwait(false);
                    // Flush per-entry so concurrent readers (e.g. `tail -f`) see progress.
                    await _writer.FlushAsync().ConfigureAwait(false);
                }
            }

            #endregion
        }

        #endregion
    }
}
