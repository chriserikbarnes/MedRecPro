using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace MedRecProTest.TestInfrastructure
{
    /**************************************************************/
    /// <summary>
    /// Counts and records relational database commands executed by an EF Core test context.
    /// </summary>
    /// <remarks>
    /// This interceptor is intentionally provider-agnostic and is used only by
    /// relational SQLite tests where query count and SQL-side paging matter.
    /// </remarks>
    /// <seealso cref="DbCommandInterceptor"/>
    public sealed class CountingDbCommandInterceptor : DbCommandInterceptor
    {
        private readonly List<string> _commands = new();

        /**************************************************************/
        /// <summary>
        /// Gets the command texts observed by the interceptor in execution order.
        /// </summary>
        public IReadOnlyList<string> Commands => _commands;

        /**************************************************************/
        /// <summary>
        /// Records a synchronous reader command.
        /// </summary>
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            #region implementation

            record(command);
            return base.ReaderExecuting(command, eventData, result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an asynchronous reader command.
        /// </summary>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            #region implementation

            record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records a synchronous scalar command.
        /// </summary>
        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            #region implementation

            record(command);
            return base.ScalarExecuting(command, eventData, result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an asynchronous scalar command.
        /// </summary>
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            #region implementation

            record(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records a synchronous non-query command.
        /// </summary>
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            #region implementation

            record(command);
            return base.NonQueryExecuting(command, eventData, result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an asynchronous non-query command.
        /// </summary>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            #region implementation

            record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captures a command's SQL text.
        /// </summary>
        /// <param name="command">The relational command being executed.</param>
        private void record(DbCommand command)
        {
            #region implementation

            _commands.Add(command.CommandText);

            #endregion
        }
    }
}
