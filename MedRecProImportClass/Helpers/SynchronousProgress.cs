namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Synchronous <see cref="IProgress{T}"/> implementation that invokes the handler
    /// inline on the calling thread. Unlike <see cref="Progress{T}"/>, this avoids
    /// posting to <see cref="System.Threading.ThreadPool"/> via
    /// <see cref="System.Threading.SynchronizationContext"/>, which delays callbacks
    /// and breaks chained progress forwarding in console apps without a
    /// <see cref="System.Threading.SynchronizationContext"/>.
    /// </summary>
    /// <remarks>
    /// Use this instead of <see cref="Progress{T}"/> when the callback must execute
    /// immediately — e.g., updating Spectre.Console task values from within a tight
    /// processing loop. Spectre.Console's auto-refresh timer (separate thread) handles
    /// rendering; the callback only needs to set property values synchronously.
    /// </remarks>
    /// <typeparam name="T">The type of progress update value.</typeparam>
    /// <seealso cref="Progress{T}"/>
    public sealed class SynchronousProgress<T> : IProgress<T>
    {
        /**************************************************************/
        /// <summary>The handler to invoke synchronously on each progress report.</summary>
        private readonly Action<T> _handler;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance with the specified handler.
        /// </summary>
        /// <param name="handler">Action to invoke synchronously when <see cref="Report"/> is called.</param>
        public SynchronousProgress(Action<T> handler) => _handler = handler;

        /**************************************************************/
        /// <summary>
        /// Reports a progress update by invoking the handler synchronously on the calling thread.
        /// </summary>
        /// <param name="value">The progress value.</param>
        public void Report(T value) => _handler(value);
    }
}
