using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Extension methods for SplParseContext to simplify common operations.
    /// </summary>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public static class SplParseContextExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Gets the database context for the current parsing operation.
        /// Returns the shared DbContext if available (for connection reuse), 
        /// otherwise creates a new instance from the service provider.
        /// </summary>
        /// <param name="context">The parsing context</param>
        /// <returns>ApplicationDbContext instance (shared or new)</returns>
        /// <remarks>
        /// This method enables transparent connection reuse without modifying parser code.
        /// When a shared DbContext is available (set by service layer with transaction),
        /// all parsers automatically reuse the same connection.
        /// When no shared DbContext exists (legacy/non-transactional path), 
        /// parsers get their own DbContext instance as before.
        /// </remarks>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        public static ApplicationDbContext GetDbContext(this SplParseContext? context)
        {
            #region implementation
            // Return shared DbContext if available (transaction path - connection reuse)
            if (context?.DbContext != null)
            {
#if DEBUG
                Debug.WriteLine($"✓ Using shared DbContext: {context.DbContext.ContextId}"); 
#endif
                return context.DbContext;
            }

#if DEBUG
            Debug.WriteLine("❌ Creating NEW DbContext - shared context not available!");
#endif

            // Fallback to creating new DbContext (legacy path - backward compatible)
            return context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>()
                ?? throw new InvalidOperationException("SplParseContext must have either DbContext or ServiceProvider set");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves changes to the database if bulk saving is not enabled.
        /// When UseBulkSaving is true, changes are deferred for later bulk commit.
        /// When UseBulkSaving is false, changes are saved immediately.
        /// </summary>
        /// <param name="context">The parsing context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method supports the performance optimization pattern where individual
        /// SaveChanges calls are deferred when bulk operations are in progress.
        /// The service layer commits all changes in a single transaction at the end.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="GetDbContext"/>
        /// <seealso cref="Label"/>
        public static async Task SaveChangesIfAllowedAsync(this SplParseContext? context)
        {
            #region implementation
            // When bulk saving is enabled, defer the save operation
            if (context?.UseBatchSaving == true)
            {
#if DEBUG
                Debug.WriteLine("→ Deferring SaveChanges (bulk saving enabled)");
#endif
                return;
            }

            // Get the DbContext and save changes immediately
            var dbContext = context.GetDbContext();

#if DEBUG
            Debug.WriteLine("→ Executing SaveChangesAsync (bulk saving disabled)");
#endif

            await dbContext.SaveChangesAsync();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Commits all deferred changes to the database when bulk saving is enabled.
        /// This method should be called at the end of a bulk operation to persist
        /// all changes that were deferred by SaveChangesIfAllowedAsync.
        /// </summary>
        /// <param name="context">The parsing context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method is the counterpart to SaveChangesIfAllowedAsync.
        /// When UseBulkSaving is true, individual saves are deferred throughout
        /// the parsing operation. This method commits all accumulated changes
        /// in a single database round-trip at the end of the operation.
        /// When UseBulkSaving is false, this method does nothing since changes
        /// were already saved individually.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="GetDbContext"/>
        /// <seealso cref="SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        public static async Task CommitDeferredChangesAsync(this SplParseContext? context)
        {
            #region implementation
            // Only commit if bulk saving was enabled (changes were deferred)
            if (context?.UseBatchSaving != true)
            {
#if DEBUG
                Debug.WriteLine("→ No deferred changes to commit (bulk saving was not enabled)");
#endif
                return;
            }

            // Get the DbContext and commit all deferred changes
            var dbContext = context.GetDbContext();
            if (dbContext != null)
            {
#if DEBUG
                Debug.WriteLine("✓ Committing all deferred changes to database");
#endif
                await dbContext.SaveChangesAsync();
            }
            #endregion
        }
    }
}