
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using static MedRecPro.Models.LabelView;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /*******************************************************************************/
    /// <summary>
    /// Partial class containing Orange Book patent data access methods.
    /// Provides search, count, and DTO-building operations against the
    /// <see cref="LabelView.OrangeBookPatent"/> view.
    /// </summary>
    /// <remarks>
    /// All query methods use AsNoTracking() for optimal read performance.
    /// Search results are cached via <see cref="PerformanceHelper"/>.
    /// </remarks>
    /// <seealso cref="OrangeBookPatentDto"/>
    /// <seealso cref="LabelView.OrangeBookPatent"/>
    public static partial class DtoLabelAccess
    {
        #region Orange Book Patent Navigation

        /**************************************************************/
        /// <summary>
        /// Searches Orange Book patent data joined with SPL label cross-references.
        /// Returns NDA products with their patent details, computed flags, and links to FDA labels.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="expiringInMonths">
        /// Optional. Filters patents expiring between today and today + N months (inclusive).
        /// Takes precedence over <paramref name="patentExpireDate"/> when both are provided.
        /// </param>
        /// <param name="documentGuid">Optional DocumentGUID exact match for cross-referenced SPL label.</param>
        /// <param name="applicationNumber">Optional NDA application number exact match (e.g., "020702").</param>
        /// <param name="ingredient">Optional ingredient name partial match (no phonetic). Supports multi-term OR matching.</param>
        /// <param name="tradeName">Optional trade/brand name partial match (no phonetic). Supports multi-term OR matching.</param>
        /// <param name="patentNo">Optional patent number exact match.</param>
        /// <param name="patentExpireDate">
        /// Optional exact patent expiration date match.
        /// Ignored when <paramref name="expiringInMonths"/> is provided.
        /// </param>
        /// <param name="hasPediatricFlag">Optional filter for pediatric exclusivity flag. Only applied when non-null.</param>
        /// <param name="hasWithdrawnCommercialReasonFlag">Optional filter for withdrawn commercial reason flag. Only applied when non-null.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="OrangeBookPatentDto"/> ordered by soonest expiring patent first, then by trade name.</returns>
        /// <example>
        /// <code>
        /// // Find patents expiring in the next 6 months
        /// var expiring = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
        ///     db, expiringInMonths: 6, null, null, null, null, null, null, null, null, secret, logger, 1, 25);
        ///
        /// // Find patents for a specific ingredient
        /// var byIngredient = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
        ///     db, null, null, null, "atorvastatin", null, null, null, null, null, secret, logger, 1, 25);
        ///
        /// // Find pediatric patents for a trade name
        /// var pediatric = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
        ///     db, null, null, null, null, "Lipitor", null, null, true, null, secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <remarks>
        /// All filters use AND logic. Partial matching (Ingredient, TradeName) uses
        /// FilterBySearchTerms with MultiTermBehavior.PartialMatchAny and PhoneticMatchOptions.None.
        /// All other string parameters use exact matching.
        ///
        /// Precedence rule: when both expiringInMonths and patentExpireDate are provided,
        /// expiringInMonths takes precedence and patentExpireDate is ignored.
        ///
        /// Results are ordered by PatentExpireDate ascending (soonest first), then TradeName ascending.
        /// </remarks>
        /// <seealso cref="LabelView.OrangeBookPatent"/>
        /// <seealso cref="OrangeBookPatentDto"/>
        public static async Task<List<OrangeBookPatentDto>> SearchOrangeBookPatentsAsync(
            ApplicationDbContext db,
            int? expiringInMonths,
            Guid? documentGuid,
            string? applicationNumber,
            string? ingredient,
            string? tradeName,
            string? patentNo,
            DateOnly? patentExpireDate,
            bool? hasPediatricFlag,
            bool? hasWithdrawnCommercialReasonFlag,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Build composite cache key from all search parameters
            string searchKey = string.Join("-",
                expiringInMonths?.ToString() ?? "",
                documentGuid?.ToString() ?? "",
                applicationNumber ?? "",
                ingredient ?? "",
                tradeName ?? "",
                patentNo ?? "",
                patentExpireDate?.ToString("yyyy-MM-dd") ?? "",
                hasPediatricFlag?.ToString() ?? "",
                hasWithdrawnCommercialReasonFlag?.ToString() ?? "");
            string key = generateCacheKey(nameof(SearchOrangeBookPatentsAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<OrangeBookPatentDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchOrangeBookPatentsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with optional filters (AND logic)
            var query = db.Set<LabelView.OrangeBookPatent>()
                .AsNoTracking()
                .AsQueryable();

            // ExpiringInMonths: date range filter (takes precedence over exact PatentExpireDate)
            if (expiringInMonths.HasValue)
            {
                var rangeStart = DateTime.Today;
                var rangeEnd = DateTime.Today.AddMonths(expiringInMonths.Value);
                query = query.Where(p => p.PatentExpireDate >= rangeStart && p.PatentExpireDate <= rangeEnd);
            }
            else if (patentExpireDate.HasValue)
            {
                // Exact date filter (only when ExpiringInMonths is NOT provided)
                var exactDate = patentExpireDate.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(p => p.PatentExpireDate == exactDate);
            }

            // DocumentGUID exact match
            if (documentGuid.HasValue)
            {
                query = query.Where(p => p.DocumentGUID == documentGuid.Value);
            }

            // ApplicationNumber exact match
            if (!string.IsNullOrWhiteSpace(applicationNumber))
            {
                query = query.Where(p => p.ApplicationNumber == applicationNumber);
            }

            // Ingredient partial match (no phonetic matching)
            if (!string.IsNullOrWhiteSpace(ingredient))
            {
                query = query.FilterBySearchTerms(
                    ingredient,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.Ingredient);
            }

            // TradeName partial match (no phonetic matching)
            if (!string.IsNullOrWhiteSpace(tradeName))
            {
                query = query.FilterBySearchTerms(
                    tradeName,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.TradeName);
            }

            // PatentNo exact match
            if (!string.IsNullOrWhiteSpace(patentNo))
            {
                query = query.Where(p => p.PatentNo == patentNo);
            }

            // Boolean flag filters (only applied when non-null)
            if (hasPediatricFlag.HasValue)
            {
                query = query.Where(p => p.HasPediatricFlag == hasPediatricFlag.Value);
            }

            if (hasWithdrawnCommercialReasonFlag.HasValue)
            {
                query = query.Where(p => p.HasWithdrawnCommercialReasonFlag == hasWithdrawnCommercialReasonFlag.Value);
            }

#if DEBUG
            var sql = query.ToQueryString();
            Debug.WriteLine($"=== {nameof(SearchOrangeBookPatentsAsync)} SQL ===\n{sql}");
#endif

            // Order by soonest expiring first, then by trade name
            query = query.OrderBy(p => p.PatentExpireDate).ThenBy(p => p.TradeName);

            // Apply pagination
            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildOrangeBookPatentDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<OrangeBookPatentDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the total number of patents expiring within the specified month horizon,
        /// optionally filtered by trade name and/or ingredient.
        /// Used to compute <see cref="OrangeBookPatentExpirationResponseDto.TotalPages"/>.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="expiringInMonths">
        /// Number of months from today to search. When null, uses <paramref name="maxExpirationMonths"/>
        /// to scope from today through all future patents.
        /// </param>
        /// <param name="maxExpirationMonths">
        /// Fallback month horizon when <paramref name="expiringInMonths"/> is null.
        /// Defines the upper bound of the date range (today + N months).
        /// </param>
        /// <param name="tradeName">Optional trade name partial match filter.</param>
        /// <param name="ingredient">Optional ingredient partial match filter.</param>
        /// <returns>Total count of matching patent records in the view.</returns>
        /// <remarks>
        /// Runs a lightweight COUNT(*) query against vw_OrangeBookPatent with the same
        /// date range and text filters used by <see cref="SearchOrangeBookPatentsAsync"/>.
        /// Text filters use EF.Functions.Like with '%term%' to match the PartialMatchAny
        /// behavior of <see cref="SearchFilterExtensions.FilterBySearchTerms{T}"/>.
        /// </remarks>
        /// <seealso cref="SearchOrangeBookPatentsAsync"/>
        public static async Task<int> CountExpiringPatentsAsync(
            ApplicationDbContext db,
            int? expiringInMonths,
            int maxExpirationMonths,
            string? tradeName,
            string? ingredient)
        {
            #region implementation

            var rangeStart = DateTime.Today;
            var rangeEnd = DateTime.Today.AddMonths(expiringInMonths ?? maxExpirationMonths);

            var query = db.Set<OrangeBookPatent>()
                .AsNoTracking()
                .Where(p => p.PatentExpireDate >= rangeStart && p.PatentExpireDate <= rangeEnd);

            // Apply tradeName partial match filter (mirrors SearchOrangeBookPatentsAsync behavior)
            if (!string.IsNullOrWhiteSpace(tradeName))
            {
                query = query.Where(p => EF.Functions.Like(p.TradeName, $"%{tradeName}%"));
            }

            // Apply ingredient partial match filter (mirrors SearchOrangeBookPatentsAsync behavior)
            if (!string.IsNullOrWhiteSpace(ingredient))
            {
                query = query.Where(p => EF.Functions.Like(p.Ingredient, $"%{ingredient}%"));
            }

            return await query.CountAsync();

            #endregion
        }

        #endregion Orange Book Patent Navigation

        #region Orange Book Patent Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of OrangeBookPatent DTOs from the navigation view.
        /// Transforms view entities to DTOs with encrypted IDs and computes
        /// LabelLink URLs for rows with available DocumentGUIDs.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="OrangeBookPatentDto"/> with encrypted IDs and computed LabelLinks.</returns>
        /// <seealso cref="LabelView.OrangeBookPatent"/>
        /// <seealso cref="OrangeBookPatentDto"/>
        private static List<OrangeBookPatentDto> buildOrangeBookPatentDtos(
            ApplicationDbContext db,
            List<LabelView.OrangeBookPatent> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity =>
            {
                var dto = new OrangeBookPatentDto
                {
                    OrangeBookPatent = entity.ToEntityWithEncryptedId(pkSecret, logger)
                };

                // Compute LabelLink when a cross-referenced SPL label DocumentGUID is available
                if (entity.DocumentGUID.HasValue)
                {
                    dto.LabelLink = $"/api/Label/original/{entity.DocumentGUID.Value}/false";
                }

                return dto;
            }).ToList();

            #endregion
        }

        #endregion Orange Book Patent Views
    }
}
