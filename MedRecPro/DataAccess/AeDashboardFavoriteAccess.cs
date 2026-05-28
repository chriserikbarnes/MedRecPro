using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Partial data-access surface for AE dashboard favorite persistence.
    /// </summary>
    /// <remarks>
    /// Favorites are stored per authenticated ASP.NET Core Identity user and joined
    /// back to <see cref="LabelView.AeDrugSummary"/> before they are returned to
    /// the dashboard. Favorite reads and writes are intentionally not cached because
    /// they are user-specific state.
    ///
    /// Comments in this file document the user-state flow explicitly: favorite rows
    /// are loaded from <see cref="AspNetUserFavorite"/>, rejoined to product
    /// summaries, marked as favorite in memory, and saved idempotently so repeated
    /// UI toggles do not create duplicate rows or false failures.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AspNetUserFavorite"/>
    public static partial class DtoLabelAccess
    {
        #region AE Dashboard Favorite Public Methods

        /**************************************************************/
        /// <summary>
        /// Gets the current user's favorited AE dashboard product summaries.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="userId">Authenticated user identifier resolved server-side.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Favorite product summaries for the supplied user.</returns>
        /// <remarks>
        /// The initial supported ordering is CreatedAtDescending. Unsupported ordering
        /// values fall back to CreatedAtDescending through
        /// <see cref="AeDashboardDerivationSettings.FavoriteOrdering"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var favorites = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(db, userId, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="SetAeProductFavoriteAsync(ApplicationDbContext, long, Guid, bool, ILogger)"/>
        /// <seealso cref="AspNetUserFavorite"/>
        public static async Task<List<AeDrugSummaryDto>> GetAeFavoriteDrugSummariesAsync(
            ApplicationDbContext db,
            long userId,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Use the default dashboard settings until controller/config binding
            // passes a customized favorite ordering policy.
            var settings = AeDashboardDerivationSettings.Default;

            // Start with the current user's favorite rows only; this is user state
            // and must never be shared through a global cache.
            var query = db.AspNetUserFavorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId);

            // Apply the configured favorite ordering before paging so page contents
            // are stable and chronological.
            query = applyFavoriteOrdering(query, settings.FavoriteOrdering);

            // Page favorite rows, not product summaries, because the user's
            // favorite timeline is the source order.
            query = applyPagination(query, page, size);

            // Materialize favorite rows before loading product summary DTOs.
            var favorites = await query.ToListAsync();

            // Extract the document GUIDs that connect favorite rows to product
            // summary view rows.
            var favoriteDocumentGuids = favorites
                .Select(favorite => favorite.DocumentGUID)
                .ToList();

            // Load encrypted, derived product summaries for the favorited documents.
            var summaries = await getProductSummariesByDocumentGuidsAsync(db, favoriteDocumentGuids, pkSecret, logger);

            // Build a lookup so favorites can be mapped back into the original
            // favorite ordering after product summaries are loaded.
            var summaryByDocument = summaries
                .Where(summary => summary.DocumentGUID.HasValue)
                .ToDictionary(summary => summary.DocumentGUID!.Value);

            // Preserve favorite row order and silently skip orphaned favorites whose
            // product summary is no longer present in vw_AeDrugSummary.
            var orderedSummaries = favorites
                .Where(favorite => summaryByDocument.ContainsKey(favorite.DocumentGUID))
                .Select(favorite => summaryByDocument[favorite.DocumentGUID])
                .ToList();

            // Mark every returned product as favorited because this method only
            // returns the authenticated user's favorite collection.
            foreach (var summary in orderedSummaries)
            {
                summary.IsFavorite = true;
            }

            // Return product summaries in favorite-row order with encrypted IDs and
            // derived score fields already populated.
            return orderedSummaries;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds or removes one AE dashboard product favorite for an authenticated user.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="userId">Authenticated user identifier resolved server-side.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="isFavorite">True to add the favorite; false to remove it.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>True when the product exists in the dashboard summary view; otherwise false.</returns>
        /// <remarks>
        /// Add and remove operations are idempotent. The current FeatureFlags:AeDashboard
        /// delete mode is HardDelete, so removing a favorite deletes the row and relies on
        /// the existing activity-log path for controller-level audit history.
        /// </remarks>
        /// <example>
        /// <code>
        /// var saved = await DtoLabelAccess.SetAeProductFavoriteAsync(db, userId, documentGuid, true, logger);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeFavoriteDrugSummariesAsync(ApplicationDbContext, long, string, ILogger, int?, int?)"/>
        /// <seealso cref="AspNetUserFavorite"/>
        public static async Task<bool> SetAeProductFavoriteAsync(
            ApplicationDbContext db,
            long userId,
            Guid documentGuid,
            bool isFavorite,
            ILogger logger)
        {
            #region implementation

            // Verify the product exists in the dashboard summary view before writing
            // favorite state; this avoids storing favorites for unsupported products.
            var productExists = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .AnyAsync(product => product.DocumentGUID == documentGuid);

            // A missing product is a valid no-op result that callers can surface as
            // "not found" or "not dashboard eligible".
            if (!productExists)
            {
                logger.LogDebug("AE dashboard favorite skipped because DocumentGUID {DocumentGuid} is not in vw_AeDrugSummary.", documentGuid);
                return false;
            }

            // Load any existing favorite row so add and remove operations can be
            // idempotent.
            var existing = await db.AspNetUserFavorites
                .FirstOrDefaultAsync(favorite => favorite.UserId == userId && favorite.DocumentGUID == documentGuid);

            // The true branch represents the UI toggling the product into the
            // favorite set.
            if (isFavorite)
            {
                // If the row already exists, the desired state is already saved.
                if (existing != null)
                {
                    return true;
                }

                // Insert a new favorite row keyed by user and document GUID.
                db.AspNetUserFavorites.Add(new AspNetUserFavorite
                {
                    UserId = userId,
                    DocumentGUID = documentGuid,
                    CreatedAt = DateTime.UtcNow
                });

                // Persist the add before returning success to the caller.
                await db.SaveChangesAsync();
                return true;
            }

            // The false branch represents the UI toggling the product out of the
            // favorite set; absence is already the desired state.
            if (existing == null)
            {
                return true;
            }

            // Hard-delete is the current configured mode, so removal deletes the row
            // instead of soft-marking it.
            db.AspNetUserFavorites.Remove(existing);

            // Persist the remove before returning success.
            await db.SaveChangesAsync();
            return true;

            #endregion
        }

        #endregion AE Dashboard Favorite Public Methods

        #region AE Dashboard Favorite Private Methods

        /**************************************************************/
        /// <summary>
        /// Applies favorite ordering, falling back to CreatedAt descending.
        /// </summary>
        private static IQueryable<AspNetUserFavorite> applyFavoriteOrdering(
            IQueryable<AspNetUserFavorite> query,
            string? favoriteOrdering)
        {
            #region implementation

            // Unknown future ordering modes fall back to the only supported current
            // mode instead of throwing from the data-access layer.
            if (!string.Equals(favoriteOrdering, "CreatedAtDescending", StringComparison.OrdinalIgnoreCase))
            {
                favoriteOrdering = "CreatedAtDescending";
            }

            // Sort newest favorites first and use DocumentGUID as a deterministic
            // tie-breaker when timestamps match.
            return query
                .OrderByDescending(favorite => favorite.CreatedAt)
                .ThenBy(favorite => favorite.DocumentGUID);

            #endregion
        }

        #endregion AE Dashboard Favorite Private Methods
    }
}
