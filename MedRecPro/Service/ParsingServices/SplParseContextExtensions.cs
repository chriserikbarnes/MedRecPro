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
    }
}