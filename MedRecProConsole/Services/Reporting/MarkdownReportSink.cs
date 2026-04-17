using System.Text;
using System.Threading.Channels;
using Spectre.Console;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Append-only markdown file writer for <see cref="TableReportEntry"/> snapshots.
    /// Safe to share across concurrent producers (e.g. a batched stages run) — entries
    /// are serialized through an internal channel and written sequentially by a single
    /// consumer task so no two tables ever interleave on disk.
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
    /// When <paramref>path</paramref> is null or whitespace, <see cref="CreateOrNullAsync"/>
    /// returns <c>null</c> so callers can pass the result straight into
    /// <c>service.ExecuteParseSingleAsync(..., sink)</c> without branching.
    /// </remarks>
    /// <seealso cref="TableReportEntry"/>
    /// <seealso cref="TableStandardizationMarkdownWriter"/>
    public sealed class MarkdownReportSink : IAsyncDisposable
    {
        #region private fields

        private readonly Channel<TableReportEntry> _channel;
        private readonly StreamWriter _writer;
        private readonly Task _consumerTask;
        private bool _disposed;

        #endregion

        #region public properties

        /**************************************************************/
        /// <summary>
        /// Resolved absolute path of the markdown file being written.
        /// </summary>
        public string Path { get; }

        #endregion

        #region construction

        private MarkdownReportSink(string path, StreamWriter writer)
        {
            #region implementation

            Path = path;
            _writer = writer;

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
        /// <param name="path">Target markdown file path. Relative paths resolve against the
        /// current working directory. Parent directories are created as needed.</param>
        /// <param name="interactiveAppendPrompt">When true and the target file already exists,
        /// the user is prompted via <see cref="AnsiConsole"/> to choose append or overwrite.
        /// CLI/quiet callers should pass false (always append silently).</param>
        /// <returns>Open sink, or null when <paramref name="path"/> is null/whitespace.</returns>
        public static Task<MarkdownReportSink?> CreateOrNullAsync(string? path, bool interactiveAppendPrompt)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult<MarkdownReportSink?>(null);

            var resolved = System.IO.Path.GetFullPath(path);
            var parent = System.IO.Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var mode = FileMode.Append;
            if (File.Exists(resolved) && interactiveAppendPrompt)
            {
                // User intent unclear — ask. Default = append (matches the --markdown-log semantics).
                var append = AnsiConsole.Confirm(
                    $"[yellow]Markdown file exists:[/] [white]{Markup.Escape(resolved)}[/]\n" +
                    "[green]Append[/] to existing content? [grey](No = overwrite)[/]",
                    defaultValue: true);

                mode = append ? FileMode.Append : FileMode.Create;
            }

            var stream = new FileStream(resolved, mode, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = false
            };

            // Mark a separator comment so appended sessions are visually distinguishable in the file.
            if (mode == FileMode.Append && stream.Length > 0)
            {
                writer.WriteLine();
                writer.WriteLine($"<!-- Session appended {DateTimeOffset.Now:yyyy-MM-ddTHH:mm:sszzz} -->");
                writer.WriteLine();
            }
            else
            {
                writer.WriteLine($"<!-- Session started {DateTimeOffset.Now:yyyy-MM-ddTHH:mm:sszzz} -->");
                writer.WriteLine();
            }

            return Task.FromResult<MarkdownReportSink?>(new MarkdownReportSink(resolved, writer));

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
        public ValueTask AppendAsync(TableReportEntry entry, CancellationToken ct = default)
        {
            #region implementation

            if (_disposed)
                throw new ObjectDisposedException(nameof(MarkdownReportSink));
            ArgumentNullException.ThrowIfNull(entry);

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
        /// Consumer loop — reads entries from the channel and writes them to the file one at a
        /// time. The single-reader constraint plus sequential writes guarantees no inter-table
        /// interleaving even under heavy parallel producer load.
        /// </summary>
        private async Task consumeAsync()
        {
            #region implementation

            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var entry))
                {
                    var markdown = TableStandardizationMarkdownWriter.BuildSection(entry);
                    await _writer.WriteAsync(markdown).ConfigureAwait(false);
                    // Flush per-entry so concurrent readers (e.g. `tail -f`) see progress.
                    await _writer.FlushAsync().ConfigureAwait(false);
                }
            }

            #endregion
        }

        #endregion
    }
}
