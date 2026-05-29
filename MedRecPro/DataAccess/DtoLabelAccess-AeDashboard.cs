using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Partial data-access surface for AE dashboard read models.
    /// </summary>
    /// <remarks>
    /// These methods query the materialized AE risk table and product summary
    /// view, map rows into API-safe DTOs with encrypted integer identifiers, and
    /// call <see cref="AeDashboardDerivation"/> for deterministic dashboard fields.
    ///
    /// Comments in this partial class document the flow from EF query construction
    /// to materialization, DTO mapping, encrypted identifier projection, favorite
    /// enrichment, and derivation. The goal is to make clear which operations are
    /// executed in SQL, which run in memory, and why cache boundaries are drawn
    /// where they are.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDashboardDerivation"/>
    public static partial class DtoLabelAccess
    {
        #region AE Dashboard Public Read Methods

        /**************************************************************/
        /// <summary>
        /// Gets AE dashboard product summaries for the product picker and catalog.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="userId">Optional authenticated user identifier used to mark favorite products.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Product summary DTOs with derived scores and optional favorite flags.</returns>
        /// <remarks>
        /// Only anonymous, unfiltered, unpaged catalog results are cached. Search,
        /// paging, and user-specific favorite enrichment are intentionally not shared
        /// through the global cache.
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.GetAeDrugSummariesAsync(db, secret, logger, "aspirin", userId);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        public static async Task<List<AeDrugSummaryDto>> GetAeDrugSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Anonymous, unfiltered, unpaged catalog requests are the only safe
            // candidates for global caching because they contain no user state.
            var canUseAnonymousCache = string.IsNullOrWhiteSpace(productSearch)
                && !userId.HasValue
                && !page.HasValue
                && !size.HasValue;

            // The cache key intentionally names the anonymous full catalog shape;
            // searches, paging, and favorites bypass this key.
            var cacheKey = generateCacheKey(nameof(GetAeDrugSummariesAsync), "anonymous-full", null, null);

            // Check the cache before building the EF query so repeat catalog loads
            // do not hit the database unnecessarily.
            if (canUseAnonymousCache)
            {
                // Cached values are already mapped and derived DTOs, not EF rows.
                var cached = Cached.GetCache<List<AeDrugSummaryDto>>(cacheKey);

                // A non-null cached value is returned as-is because anonymous DTOs
                // have no user-specific IsFavorite flags.
                if (cached != null)
                {
                    logger.LogDebug("AE dashboard product summary cache hit for {CacheKey} with {Count} rows.", cacheKey, cached.Count);
#if DEBUG
                    Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetAeDrugSummariesAsync)} Cache Hit for {cacheKey} ===");
#endif
                    return cached;
                }
            }

            // Start from the keyless product summary view. AsNoTracking keeps the
            // read-only dashboard query lightweight.
            var query = db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .AsQueryable();

            // Product search remains an EF expression so SQL Server performs the
            // filtering before rows are materialized.
            query = applyProductSearch(query, productSearch);

            // Sort the product catalog by most elevated findings first, then use
            // product name and DocumentGUID for deterministic tie-breaking.
            query = query
                .OrderByDescending(summary => summary.SignificantElevatedCount)
                .ThenBy(summary => summary.ProductName)
                .ThenBy(summary => summary.DocumentGUID);

            // Apply paging last so the ordered query returns a stable page.
            query = applyPagination(query, page, size);

            // Materialize EF rows before encryption because encryption cannot be
            // translated into SQL.
            var entities = await query.ToListAsync();

            // Convert SQL view rows to client DTOs and mask integer identifiers.
            var summaries = buildAeDrugSummaryDtos(entities, pkSecret, logger);

            // Add score and score reason after mapping so derivation works on the
            // same DTO shape returned to controllers.
            AeDashboardDerivation.DeriveProducts(summaries);

            // Favorite flags are user-specific and therefore applied only after the
            // shared product DTOs have been created.
            if (userId.HasValue)
            {
                // Load the user's favorite document set once and mark DTOs in
                // memory to avoid per-row database checks.
                var favoriteDocumentGuids = await loadFavoriteDocumentGuidsAsync(db, userId.Value);
                markFavorites(summaries, favoriteDocumentGuids);
            }

            // Store only non-empty anonymous catalog results to avoid caching an
            // empty result during transient database or import states.
            if (canUseAnonymousCache && summaries.Count > 0)
            {
                Cached.SetCacheManageKey(cacheKey, summaries, 1.0);
                logger.LogDebug("AE dashboard product summary cache set for {CacheKey} with {Count} rows.", cacheKey, summaries.Count);
            }

            // Return mapped, encrypted, optionally favorite-enriched, and derived
            // product summaries.
            return summaries;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets derived AE risk signals for one SPL document.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>Derived signal DTOs for the requested product.</returns>
        /// <remarks>
        /// This is the shared per-product signal source used by triage, forest,
        /// quadrant, reverse-lookup, and interchange assembly methods.
        /// </remarks>
        /// <example>
        /// <code>
        /// var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        /// <seealso cref="AeRiskSignalDto"/>
        public static async Task<List<AeRiskSignalDto>> GetAeRiskSignalsByDocumentAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Start from the materialized risk table and constrain by SPL document
            // in SQL before any DTO projection happens.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.DocumentGUID == documentGuid);

            // Apply optional comparator filtering while the query is still
            // translated to SQL.
            query = applyComparatorFilter(query, comparator);

            // Materialize rows in deterministic AE-term/RR order for stable chart
            // and test output.
            var entities = await query
                .OrderBy(signal => signal.ParameterName)
                .ThenByDescending(signal => signal.RR)
                .ToListAsync();

            // Map rows into API-safe DTOs before running pure derivation logic.
            var signals = buildAeRiskSignalDtos(entities, pkSecret, logger);

            // Derive typed significance, flags, precision, and counseling tier for
            // every signal in the returned list.
            AeDashboardDerivation.DeriveSignals(signals);

            // The caller can suppress fragile rows after derivation because the
            // fragile decision depends on parsed flags and confidence intervals.
            if (!includeFragile)
            {
                signals = signals
                    .Where(signal => signal.PrecisionClass != AePrecisionClass.Fragile)
                    .ToList();
            }

            // Return only signals that survived document, comparator, and optional
            // fragile filtering.
            return signals;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the tiered AE dashboard triage view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A triage view DTO, or null when the product is absent from the summary view.</returns>
        /// <remarks>
        /// The method keeps controller work thin by loading product context, loading
        /// signals, and delegating all tier assembly to <see cref="AeDashboardDerivation"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var triage = await DtoLabelAccess.GetAeTriageViewAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="AeTriageViewDto"/>
        public static async Task<AeTriageViewDto?> GetAeTriageViewAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Load the product summary first because triage output needs product
            // context as well as per-signal rows.
            var product = await getAeDrugSummaryByDocumentGuidAsync(db, documentGuid, pkSecret, logger);

            // A missing product summary means the document is not dashboard-ready,
            // so the controller can translate null into an appropriate response.
            if (product == null)
            {
                return null;
            }

            // Reuse the shared signal source so triage honors the same comparator
            // and fragile filters as other dashboard views.
            var signals = await GetAeRiskSignalsByDocumentAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // Delegate tier assembly and sort decisions to the pure derivation
            // helper.
            return AeDashboardDerivation.BuildTriageView(product, signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard forest plot view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A forest plot DTO, or null when the product is absent from the summary view.</returns>
        /// <remarks>
        /// Signals are sorted by descending RR after comparator and fragile filters are
        /// applied.
        /// </remarks>
        /// <example>
        /// <code>
        /// var forest = await DtoLabelAccess.GetAeForestPlotAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeForestPlotDto"/>
        public static async Task<AeForestPlotDto?> GetAeForestPlotAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Check product existence before returning a chart shell; absent product
            // summaries should behave as missing dashboard products.
            var productExists = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .AnyAsync(product => product.DocumentGUID == documentGuid);

            // Null tells the caller there is no forest plot for this document.
            if (!productExists)
            {
                return null;
            }

            // Load the same filtered signal set used by other product-level views.
            var signals = await GetAeRiskSignalsByDocumentAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // The derivation helper owns forest-plot sorting and payload assembly.
            return AeDashboardDerivation.BuildForestPlot(signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard quadrant view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A quadrant view DTO, or null when the product is absent from the summary view.</returns>
        /// <remarks>
        /// Coordinates are derived after query filtering and are clamped from zero to
        /// one for direct chart use.
        /// </remarks>
        /// <example>
        /// <code>
        /// var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeQuadrantViewDto"/>
        public static async Task<AeQuadrantViewDto?> GetAeQuadrantViewAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Check product existence first so empty risk rows do not masquerade as
            // a valid dashboard product.
            var productExists = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .AnyAsync(product => product.DocumentGUID == documentGuid);

            // Missing product summary maps to null rather than an empty quadrant.
            if (!productExists)
            {
                return null;
            }

            // Load document signals with the requested comparator and fragile-row
            // policy before calculating chart coordinates.
            var signals = await GetAeRiskSignalsByDocumentAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // The derivation helper converts each signal into clamped chart points.
            return AeDashboardDerivation.BuildQuadrantView(signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets reverse-lookup AE dashboard matches for one symptom term.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="symptom">Adverse-event term to look up.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="documentGuids">Optional product scope limiting candidate documents.</param>
        /// <returns>A ranked symptom-to-product reverse lookup result.</returns>
        /// <remarks>
        /// The lookup matches <see cref="AeRiskSignalDto.ParameterName"/> case-insensitively,
        /// optionally scopes to supplied documents, joins product summaries, and ranks
        /// causal-looking rows ahead of reassuring rows.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = await DtoLabelAccess.GetAeReverseLookupAsync(db, "Headache", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeReverseLookupResultDto"/>
        public static async Task<AeReverseLookupResultDto> GetAeReverseLookupAsync(
            ApplicationDbContext db,
            string symptom,
            string pkSecret,
            ILogger logger,
            IEnumerable<Guid>? documentGuids = null)
        {
            #region implementation

            // A blank symptom cannot be matched against AE terms, so return an
            // empty, reassuring result without querying the database.
            if (string.IsNullOrWhiteSpace(symptom))
            {
                return new AeReverseLookupResultDto { Symptom = symptom, AllReassuring = true };
            }

            // Normalize once for the SQL-side exact term comparison.
            var normalizedSymptom = symptom.Trim().ToLower();

            // Distinct optional scope prevents duplicate document filters from
            // inflating generated SQL parameters.
            var scopedDocuments = documentGuids?.Distinct().ToList();

            // Build the exact-term risk query. ParameterName is lowered in SQL so
            // the match is case-insensitive across providers used in tests.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.ParameterName != null && signal.ParameterName.ToLower() == normalizedSymptom);

            // If a product scope was supplied, keep only risk rows from those
            // documents before materialization.
            if (scopedDocuments is { Count: > 0 })
            {
                query = query.Where(signal => signal.DocumentGUID.HasValue && scopedDocuments.Contains(signal.DocumentGUID.Value));
            }

            // Materialize matched risk rows, then map/encrypt them in memory.
            var signalEntities = await query.ToListAsync();

            // Build DTOs only after materialization because encrypted identifiers
            // and derivation helpers cannot be translated into SQL.
            var signals = buildAeRiskSignalDtos(signalEntities, pkSecret, logger);

            // Extract the matched document set so product summaries can be loaded
            // only for products that actually had the searched AE term.
            var matchedDocuments = signals
                .Where(signal => signal.DocumentGUID.HasValue)
                .Select(signal => signal.DocumentGUID!.Value)
                .Distinct()
                .ToList();

            // Load product cards for the matched documents and keep product scoring
            // consistent with the product picker.
            var products = await getProductSummariesByDocumentGuidsAsync(db, matchedDocuments, pkSecret, logger);

            // Delegate verdict calculation and ranking to the pure derivation layer.
            return AeDashboardDerivation.BuildReverseLookupResult(symptom, products, signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an AE dashboard interchange comparison for two products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuidA">SPL document identifier for product A.</param>
        /// <param name="documentGuidB">SPL document identifier for product B.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="differencesOnly">Whether rows classified as similar should be removed.</param>
        /// <returns>An interchange comparison DTO, or null when either product is missing.</returns>
        /// <remarks>
        /// The comparison unions adverse-event terms across both products and delegates
        /// row-level classification to <see cref="AeDashboardDerivation"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var comparison = await DtoLabelAccess.GetAeInterchangeAsync(db, a, b, secret, logger, true);
        /// </code>
        /// </example>
        /// <seealso cref="AeInterchangeComparisonDto"/>
        public static async Task<AeInterchangeComparisonDto?> GetAeInterchangeAsync(
            ApplicationDbContext db,
            Guid documentGuidA,
            Guid documentGuidB,
            string pkSecret,
            ILogger logger,
            bool differencesOnly = false)
        {
            #region implementation

            // Fetch both product summaries together to avoid two separate summary
            // view queries and to guarantee the same mapping rules for both sides.
            var products = await getProductSummariesByDocumentGuidsAsync(
                db,
                new[] { documentGuidA, documentGuidB },
                pkSecret,
                logger);

            // Identify product A and product B by their requested document GUIDs
            // after the shared product query returns.
            var productA = products.FirstOrDefault(product => product.DocumentGUID == documentGuidA);

            // Product B is resolved independently so missing-side detection can
            // report a null comparison if either product is absent.
            var productB = products.FirstOrDefault(product => product.DocumentGUID == documentGuidB);

            // Interchange requires both products; missing either one means the
            // comparison cannot be assembled.
            if (productA == null || productB == null)
            {
                return null;
            }

            // Load each side's complete signal set before row-level comparison.
            var signalsA = await GetAeRiskSignalsByDocumentAsync(db, documentGuidA, pkSecret, logger);

            // Product B gets its own signal list so duplicate terms can be collapsed
            // separately by the interchange derivation helper.
            var signalsB = await GetAeRiskSignalsByDocumentAsync(db, documentGuidB, pkSecret, logger);

            // The derivation helper unions terms, classifies rows, and counts the
            // rendered comparison classes.
            return AeDashboardDerivation.BuildInterchangeComparison(
                productA,
                productB,
                signalsA,
                signalsB,
                differencesOnly);

            #endregion
        }

        #endregion AE Dashboard Public Read Methods

        #region AE Dashboard Private Query Helpers

        /**************************************************************/
        /// <summary>
        /// Applies product, substance, UNII, and class search to the product summary query.
        /// </summary>
        private static IQueryable<LabelView.AeDrugSummary> applyProductSearch(
            IQueryable<LabelView.AeDrugSummary> query,
            string? productSearch)
        {
            #region implementation

            // Leave the original query untouched when no search term is supplied;
            // this keeps anonymous catalog caching and ordering behavior unchanged.
            if (string.IsNullOrWhiteSpace(productSearch))
            {
                return query;
            }

            // Wrap the trimmed term in SQL LIKE wildcards so one input can match
            // product names, substance names, UNII values, or class metadata.
            var pattern = $"%{productSearch.Trim()}%";

            // Keep this as an IQueryable expression so EF translates the search
            // predicates into SQL instead of filtering after materialization.
            return query.Where(summary =>
                EF.Functions.Like(summary.ProductName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.SubstanceName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.UNII ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PharmClassCode ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PharmClassName ?? string.Empty, pattern));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies comparator mix filtering to materialized AE risk rows.
        /// </summary>
        private static IQueryable<LabelView.FlattenedAdverseEventRiskTable> applyComparatorFilter(
            IQueryable<LabelView.FlattenedAdverseEventRiskTable> query,
            AeComparatorMix? comparator)
        {
            #region implementation

            // Convert the optional comparator enum into a SQL predicate; null or an
            // unsupported value intentionally leaves the query unfiltered.
            return comparator switch
            {
                AeComparatorMix.Placebo => query.Where(signal => signal.IsPlaceboControlled),
                AeComparatorMix.Active => query.Where(signal => !signal.IsPlaceboControlled),
                _ => query
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets one mapped AE product summary by document GUID.
        /// </summary>
        private static async Task<AeDrugSummaryDto?> getAeDrugSummaryByDocumentGuidAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Query a single product summary row using the document GUID that backs
            // dashboard product selection.
            var entity = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .FirstOrDefaultAsync(summary => summary.DocumentGUID == documentGuid);

            // Missing summary rows mean the document is not represented in
            // vw_AeDrugSummary.
            if (entity == null)
            {
                return null;
            }

            // Map the view row to an encrypted DTO and derive score fields before
            // returning it to a product-level view builder.
            var dto = buildAeDrugSummaryDto(entity, pkSecret, logger);
            return AeDashboardDerivation.DeriveProduct(dto);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets mapped AE product summaries by document GUID set.
        /// </summary>
        private static async Task<List<AeDrugSummaryDto>> getProductSummariesByDocumentGuidsAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> documentGuids,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // De-duplicate document GUID input so SQL contains the smallest useful
            // IN-list and the mapper sees each product at most once.
            var guidList = documentGuids.Distinct().ToList();

            // Avoid an unnecessary database round trip when no documents need
            // product summary rows.
            if (guidList.Count == 0)
            {
                return new List<AeDrugSummaryDto>();
            }

            // Load all matching product summary rows as read-only EF data.
            var entities = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .Where(summary => summary.DocumentGUID.HasValue && guidList.Contains(summary.DocumentGUID.Value))
                .ToListAsync();

            // Convert rows to encrypted DTOs and calculate product score fields.
            var summaries = buildAeDrugSummaryDtos(entities, pkSecret, logger);
            return AeDashboardDerivation.DeriveProducts(summaries);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads favorite document GUIDs for one authenticated user.
        /// </summary>
        private static async Task<HashSet<Guid>> loadFavoriteDocumentGuidsAsync(
            ApplicationDbContext db,
            long userId)
        {
            #region implementation

            // Load only the persisted document GUIDs because favorite marking only
            // needs set membership by document.
            var favoriteDocumentGuids = await db.AspNetUserFavorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .Select(favorite => favorite.DocumentGUID)
                .ToListAsync();

            // A HashSet makes per-product favorite checks O(1) during DTO marking.
            return favoriteDocumentGuids.ToHashSet();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Marks product summary DTOs as favorites using a loaded favorite set.
        /// </summary>
        private static void markFavorites(
            IEnumerable<AeDrugSummaryDto> summaries,
            IReadOnlySet<Guid> favoriteDocumentGuids)
        {
            #region implementation

            // Walk the already-materialized product DTOs and set IsFavorite based on
            // the user's favorite document set.
            foreach (var summary in summaries)
            {
                // Products without a DocumentGUID cannot be favorited because the
                // favorite table stores DocumentGUID as its product key.
                summary.IsFavorite = summary.DocumentGUID.HasValue
                    && favoriteDocumentGuids.Contains(summary.DocumentGUID.Value);
            }

            #endregion
        }

        #endregion AE Dashboard Private Query Helpers

        #region AE Dashboard Private Mapping Helpers

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE product summary DTOs from EF rows.
        /// </summary>
        private static List<AeDrugSummaryDto> buildAeDrugSummaryDtos(
            IEnumerable<LabelView.AeDrugSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Map each EF product summary row through the single-row mapper so
            // encryption and default handling stay centralized.
            return entities
                .Select(entity => buildAeDrugSummaryDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE product summary DTO from an EF row.
        /// </summary>
        private static AeDrugSummaryDto buildAeDrugSummaryDto(
            LabelView.AeDrugSummary entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Build the client DTO directly from the summary view. Integer database
            // identifiers are encrypted before they leave the data-access layer.
            return new AeDrugSummaryDto
            {
                // Active moiety, ingredient, and pharmacologic-class IDs are masked
                // according to the existing DTO encrypted-ID convention.
                EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(entity.IngredientSubstanceID, pkSecret, logger, nameof(entity.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(entity.PharmacologicClassID, pkSecret, logger, nameof(entity.PharmacologicClassID)),

                // Document and product identity fields remain readable because they
                // are already public dashboard keys or display text.
                DocumentGUID = entity.DocumentGUID,
                ProductName = entity.ProductName,
                SubstanceName = entity.SubstanceName,
                UNII = entity.UNII,
                PharmClassCode = entity.PharmClassCode,
                PharmClassName = entity.PharmClassName,

                // Denominator and row-count fields are numeric metrics, not raw row
                // identifiers, so they stay visible in the client DTO.
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                RowCount = entity.RowCount,
                SignificantCount = entity.SignificantCount,
                SignificantProtectiveCount = entity.SignificantProtectiveCount,
                SignificantElevatedCount = entity.SignificantElevatedCount,

                // Coverage values are consumed by score derivation and product
                // picker filtering.
                PlaceboCoverage = entity.PlaceboCoverage,
                ActiveCoverage = entity.ActiveCoverage,
                DoseCoverage = (double)entity.DoseCoverage,
                SocBreadth = entity.SocBreadth,

                // Fall back to dashboard metadata when the view does not provide a
                // positive SOC total.
                SocTotal = entity.SocTotal > 0 ? entity.SocTotal : AeDashboardMetadata.SocTotal,

                // Convert the view's text classification into the dashboard enum.
                MonoComboMix = parseMonoComboMix(entity.MonoComboMix)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE risk signal DTOs from EF rows.
        /// </summary>
        private static List<AeRiskSignalDto> buildAeRiskSignalDtos(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Collapse duplicate risk rows to one signal per viewer-visible clinical stratum
            // before mapping. This removes both the pharmacologic-class cartesian fan-out from
            // vw_AeRisk (the class subquery is joined on DocumentGUID only, so a product mapped
            // to N pharmacologic classes emits each AE statistic N times) and the multi-arm
            // duplication where the same term/dose/comparator is reported for both a pooled arm
            // and a smaller, unlabeled subgroup arm. Collapsing at the entity grain also avoids
            // encrypting throwaway rows.
            return collapseToMostPoweredStratum(entities)
                .Select(entity => buildAeRiskSignalDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses duplicate AE risk rows to one representative per clinical stratum.
        /// </summary>
        /// <param name="entities">Materialized risk-table rows for one or more documents.</param>
        /// <returns>One row per (document, term, SOC, dose, comparator, study/population context), keeping the most statistically powered arm.</returns>
        /// <remarks>
        /// Two sources of duplication are removed here. First, <c>vw_AeRisk</c> joins the
        /// pharmacologic-class subquery on <c>DocumentGUID</c> only, so a product mapped to
        /// several pharmacologic classes emits identical copies of every AE statistic. Second,
        /// the same adverse event at the same dose and comparator can be reported for both a
        /// pooled arm and a smaller subgroup arm that carries no
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable.Population"/> or
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable.Subpopulation"/> label, which
        /// renders as the same signal with conflicting numbers.
        ///
        /// The grouping key intentionally includes study context, population, and subpopulation
        /// so genuinely labeled strata stay distinct; only rows a reader cannot tell apart are
        /// merged. Within a group the representative is the most statistically powered arm
        /// (largest treatment denominator), preferring a significant, tighter-CI row on ties,
        /// with the source row identifier as a final deterministic tiebreaker.
        /// </remarks>
        /// <seealso cref="buildAeRiskSignalDtos"/>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        private static List<LabelView.FlattenedAdverseEventRiskTable> collapseToMostPoweredStratum(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            // Group by the clinical identity a dashboard reader actually sees. Class and moiety
            // identifiers are deliberately excluded so class-fan-out copies merge; study and
            // population context is included so legitimately labeled strata are preserved.
            return entities
                .GroupBy(entity => new
                {
                    entity.DocumentGUID,
                    entity.ParameterName,
                    entity.ParameterCategory,
                    entity.Dose,
                    entity.DoseUnit,
                    entity.IsPlaceboControlled,
                    entity.StudyContext,
                    entity.Population,
                    entity.Subpopulation
                })
                // Keep the most statistically powered arm as the stratum representative so the
                // pooled, tighter-CI estimate wins over a small unlabeled subgroup.
                .Select(group => group
                    .OrderByDescending(entity => entity.ArmN ?? 0)
                    .ThenByDescending(entity => entity.ComparatorN ?? 0)
                    .ThenBy(entity => string.Equals(entity.Significance, "not significant", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(entity => (entity.RRUpperBound ?? double.MaxValue) - (entity.RRLowerBound ?? 0.0))
                    .ThenBy(entity => entity.FlattenedAdverseEventTableId)
                    .First())
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE risk signal DTO from an EF row.
        /// </summary>
        private static AeRiskSignalDto buildAeRiskSignalDto(
            LabelView.FlattenedAdverseEventRiskTable entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Build the risk-signal DTO directly from the risk table row. Raw
            // integer table IDs are encrypted while statistical and context fields
            // remain readable for dashboard rendering.
            return new AeRiskSignalDto
            {
                // These encrypted identifiers let clients request detail/drill-in
                // later without exposing database integer keys.
                EncryptedFlattenedAdverseEventRiskTableID = encryptNullableInt(entity.Id, pkSecret, logger, nameof(entity.Id)),
                EncryptedFlattenedAdverseEventTableID = encryptNullableInt(entity.FlattenedAdverseEventTableId, pkSecret, logger, nameof(entity.FlattenedAdverseEventTableId)),
                EncryptedFlattenedStandardizedTableID = encryptNullableInt(entity.FlattenedStandardizedTableId, pkSecret, logger, nameof(entity.FlattenedStandardizedTableId)),

                // AE term and product context identify what the row means.
                ParameterName = entity.ParameterName,
                ParameterCategory = entity.ParameterCategory,
                Significance = entity.Significance,
                NumberNeededType = entity.NumberNeededType,
                UNII = entity.UNII,
                ProductName = entity.ProductName,
                DocumentGUID = entity.DocumentGUID,

                // Denominators and event counts support downstream precision,
                // number-needed, and bubble-size derivation.
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                EventsTreatment = entity.EventsTreatment,
                EventsComparator = entity.EventsComparator,

                // Relative-risk statistics are copied as calculated by Stage 5 so
                // derivation can classify direction and chart coordinates.
                RR = entity.RR,
                RRLowerBound = entity.RRLowerBound,
                RRUpperBound = entity.RRUpperBound,
                LogRR = entity.LogRR,
                LogRRLowerBound = entity.LogRRLowerBound,
                LogRRUpperBound = entity.LogRRUpperBound,

                // Number-needed fields are displayed after the derivation layer
                // classifies NNH versus NNT.
                NumberNeeded = entity.NumberNeeded,
                NumberNeededLowerBound = entity.NumberNeededLowerBound,
                NumberNeededUpperBound = entity.NumberNeededUpperBound,

                // Comparator and combo flags drive filtering and product summaries.
                IsPlaceboControlled = entity.IsPlaceboControlled,
                IsCombo = entity.IsCombo,
                CalculationFlags = entity.CalculationFlags,

                // Study, population, and dose context remain display metadata for
                // signal detail panels.
                StudyContext = entity.StudyContext,
                Population = entity.Population,
                Subpopulation = entity.Subpopulation,
                Dose = entity.Dose,
                DoseUnit = entity.DoseUnit
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a nullable integer identifier for client-safe DTO exposure.
        /// </summary>
        private static string? encryptNullableInt(
            int? value,
            string pkSecret,
            ILogger logger,
            string fieldName)
        {
            #region implementation

            // Null source IDs stay null on the DTO so callers can distinguish
            // absent relationships from encryption failures.
            if (!value.HasValue)
            {
                return null;
            }

            // Encryption can fail if the secret is invalid; isolate that failure to
            // the affected field and log the identifier name for diagnostics.
            try
            {
                // Fast strength matches the existing DTO ID masking convention.
                return StringCipher.Encrypt(value.Value.ToString(), pkSecret, StringCipher.EncryptionStrength.Fast);
            }
            // Encryption failures are caught per field so one bad ID does not fail
            // the entire AE dashboard payload.
            catch (Exception ex)
            {
                // Returning null prevents a bad identifier from breaking the whole
                // dashboard response while still preserving an error log.
                logger.LogError(ex, "Failed to encrypt AE dashboard identifier {FieldName} with value {Value}.", fieldName, value.Value);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the mono/combo text persisted by the product summary view.
        /// </summary>
        private static AeMonoComboMix? parseMonoComboMix(string? monoComboMix)
        {
            #region implementation

            // Normalize the view text before matching so casing and surrounding
            // whitespace do not change the enum result.
            return monoComboMix?.Trim().ToLowerInvariant() switch
            {
                "mono" => AeMonoComboMix.Mono,
                "combo" => AeMonoComboMix.Combo,
                "mixed" => AeMonoComboMix.Mixed,
                _ => null
            };

            #endregion
        }

        #endregion AE Dashboard Private Mapping Helpers
    }
}
