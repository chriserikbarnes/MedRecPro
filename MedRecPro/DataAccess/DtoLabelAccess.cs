
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
        #region Primary Entry Point
        /**************************************************************/
        /// <summary>
        /// The primary entry point for building a list of Document DTOs with
        /// their full hierarchy of related data. Constructs complete
        /// document hierarchies including structured bodies, authors,
        /// relationships, and legal authenticators.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <param name="useBatchLoading">
        /// Optional flag to enable batch document loading optimization.
        /// Pass the value from IConfiguration.GetValue&lt;bool&gt;("FeatureFlags:UseBatchDocumentLoading").
        /// When true, uses batch loading pattern to reduce database round-trips by 10-20x.
        /// When false or null, uses sequential loading (legacy behavior).
        /// </param>
        /// <returns>List of <see cref="DocumentDto"/> representing the fetched documents and their related entities.</returns>
        /// <example>
        /// <code>
        /// // Sequential loading (default)
        /// var documents = await DtoLabelAccess.BuildDocumentsAsync(db, secret, logger, 1, 10);
        ///
        /// // Batch loading (with feature flag)
        /// var useBatch = _configuration.GetValue&lt;bool&gt;("FeatureFlags:UseBatchDocumentLoading");
        /// var documents = await DtoLabelAccess.BuildDocumentsAsync(db, secret, logger, 1, 10, useBatch);
        /// </code>
        /// </example>
        /// <remarks>
        /// The loading strategy is controlled by the useBatchLoading parameter:
        /// - true: Batch loading - fetches all child entities in single queries per entity type (10-20x faster)
        /// - false/null: Sequential loading - fetches child entities one at a time (legacy behavior)
        ///
        /// IMPORTANT: The cache key includes the loading mode to prevent serving cached data
        /// from a different loading strategy when the feature flag is toggled.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.StructuredBody"/>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="Label.RelatedDocument"/>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.LegalAuthenticator"/>
        public static async Task<List<DocumentDto>> BuildDocumentsAsync(
           ApplicationDbContext db,
           string pkSecret,
           ILogger logger,
           int? page = null,
           int? size = null,
           bool? useBatchLoading = null)
        {
            #region implementation

            // Include loading mode in cache key to prevent cross-mode cache hits
            var loadingMode = useBatchLoading == true ? "batch" : "sequential";
            string key = ($"{nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)}_{page}_{size}_{loadingMode}").Base64Encode();

            var cached = Cached.GetCache<List<DocumentDto>>(key);

            if(cached != null && page == null && size == null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} documents (loading mode: {loadingMode}).");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)} Cache Hit for {key} (mode: {loadingMode}) ===");
#endif

                return cached;
            }

            // Build query for paginated documents
            var query = db.Set<Label.Document>().AsNoTracking();

            if (page.HasValue && size.HasValue)
            {
                query = query
                    .OrderBy(d => d.DocumentID)
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value);
            }

            var docs = await query.ToListAsync();

            // Pass the feature flag to the internal method for strategy selection
            var ret = await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger, useBatchLoading);

            if(ret != null)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} documents (loading mode: {loadingMode}).");
            }

            return ret ?? new List<DocumentDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Document DTOs for a specific document identified by its GUID.
        /// Constructs complete document hierarchies including structured bodies, authors,
        /// relationships, and legal authenticators for the specified document.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">The unique identifier for the specific document to retrieve.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="useBatchLoading">
        /// Optional flag to enable batch document loading optimization.
        /// Pass the value from IConfiguration.GetValue&lt;bool&gt;("FeatureFlags:UseBatchDocumentLoading").
        /// When true, uses batch loading pattern to reduce database round-trips by 10-20x.
        /// When false or null, uses sequential loading (legacy behavior).
        /// </param>
        /// <returns>List of <see cref="DocumentDto"/> containing the document if found, empty list otherwise.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        ///
        /// // Sequential loading (default)
        /// var documents = await DtoLabelAccess.BuildDocumentsAsync(db, documentGuid, secret, logger);
        ///
        /// // Batch loading (with feature flag)
        /// var useBatch = _configuration.GetValue&lt;bool&gt;("FeatureFlags:UseBatchDocumentLoading");
        /// var documents = await DtoLabelAccess.BuildDocumentsAsync(db, documentGuid, secret, logger, useBatch);
        /// </code>
        /// </example>
        /// <remarks>
        /// The loading strategy is controlled by the useBatchLoading parameter:
        /// - true: Batch loading - fetches all child entities in single queries per entity type (10-20x faster)
        /// - false/null: Sequential loading - fetches child entities one at a time (legacy behavior)
        ///
        /// Returns a list for consistency with the paginated overload, but will contain at most one document.
        ///
        /// IMPORTANT: The cache key includes the loading mode to prevent serving cached data
        /// from a different loading strategy when the feature flag is toggled.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        /// <seealso cref="Label.StructuredBody"/>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="Label.RelatedDocument"/>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.LegalAuthenticator"/>
        public static async Task<List<DocumentDto>> BuildDocumentsAsync(
           ApplicationDbContext db,
           Guid documentGuid,
           string pkSecret,
           ILogger logger,
           bool? useBatchLoading = null)
        {
            #region implementation

            // Include loading mode in cache key to prevent cross-mode cache hits
            var loadingMode = useBatchLoading == true ? "batch" : "sequential";
            string key = ($"{nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)}.{documentGuid}_{loadingMode}").Base64Encode();

            var cached = Cached.GetCache<List<DocumentDto>>(key);

            if(cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} documents (loading mode: {loadingMode}).");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)} Cache Hit for {key} (mode: {loadingMode}) ===");
#endif
                return cached;
            }

            // Build query for specific document by GUID
            var docs = await db.Set<Label.Document>()
                .AsNoTracking()
                .Where(d => d.DocumentGUID == documentGuid)
                .ToListAsync();

            // Pass the feature flag to the internal method for strategy selection
            var ret = await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger, useBatchLoading);

            if(ret != null)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} documents (loading mode: {loadingMode}).");
            }

            return ret ?? new List<DocumentDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a package identifier DTO for a specified packaging level ID.
        /// </summary>
        /// <param name="db">The database context for querying package identifier entities.</param>
        /// <param name="packagingLevelID">The packaging level identifier to retrieve package identifier for.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A PackageIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="PackageIdentifierDto"/>
        public static async Task<PackageIdentifierDto?> GetPackageIdentifierAsync(ApplicationDbContext db,
            int? packagingLevelID,
            string pkSecret,
            ILogger logger)
        {
            return await buildPackageIdentifierDtoAsync(db, packagingLevelID, pkSecret, logger, null);
        }

        #endregion

        #region View-Based Public Entry Points

        #region Application Number Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by regulatory application number (NDA, ANDA, BLA).
        /// Returns products sharing the same regulatory approval with navigation data.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="applicationNumber">The application number to search for (e.g., "NDA014526").</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductsByApplicationNumberDto"/> matching the application number.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchByApplicationNumberAsync(db, "NDA014526", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <remarks>
        /// Uses the vw_ProductsByApplicationNumber view for optimized joins.
        /// Results are cached for improved performance on repeated queries.
        /// </remarks>
        /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
        /// <seealso cref="Label.MarketingCategory"/>
        public static async Task<List<ProductsByApplicationNumberDto>> SearchByApplicationNumberAsync(
            ApplicationDbContext db,
            string applicationNumber,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Generate cache key including search and pagination parameters
            string key = generateCacheKey(nameof(SearchByApplicationNumberAsync), applicationNumber, page, size);

            var cached = Cached.GetCache<List<ProductsByApplicationNumberDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByApplicationNumberAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Parse input into normalized search components
            var terms = ApplicationNumberSearch.Parse(applicationNumber);

            // Build flexible query supporting multiple match strategies
            var query = db.Set<LabelView.ProductsByApplicationNumber>()
                .AsNoTracking()
                .Where(p => p.ApplicationNumber != null &&
                    (
                        // Exact match after normalization (e.g., "ANDA125669" == "ANDA125669")
                        p.ApplicationNumber.Replace(" ", "").ToUpper() == terms.Normalized ||

                        // Prefix-only search (e.g., "ANDA" matches all ANDA applications)
                        (terms.IsPrefixOnly && p.ApplicationNumber.ToUpper().StartsWith(terms.AlphaOnly)) ||

                        // Number-only search (e.g., "125669" matches "ANDA125669")
                        (terms.IsNumericOnly && p.ApplicationNumber.Contains(terms.NumericOnly)) ||

                        // Fallback: contains normalized input for edge cases
                        p.ApplicationNumber.ToUpper().Contains(terms.Normalized)
                    ))
                .OrderBy(p => p.ProductID);

            // Apply pagination
            query = (IOrderedQueryable<LabelView.ProductsByApplicationNumber>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductsByApplicationNumberDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductsByApplicationNumberDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets aggregated summaries of application numbers with product/document counts.
        /// Useful for understanding the scope of regulatory approvals.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="marketingCategory">Optional filter by marketing category (NDA, ANDA, BLA).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ApplicationNumberSummaryDto"/> with aggregated counts.</returns>
        /// <example>
        /// <code>
        /// var summaries = await DtoLabelAccess.GetApplicationNumberSummariesAsync(db, "NDA", secret, logger, 1, 50);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ApplicationNumberSummary"/>
        public static async Task<List<ApplicationNumberSummaryDto>> GetApplicationNumberSummariesAsync(
            ApplicationDbContext db,
            string? marketingCategory,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetApplicationNumberSummariesAsync), marketingCategory, page, size);

            var cached = Cached.GetCache<List<ApplicationNumberSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetApplicationNumberSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with optional category filter
            var query = db.Set<LabelView.ApplicationNumberSummary>()
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(marketingCategory))
            {
                // Handle NDA/ANDA edge case - "NDA" is a substring of "ANDA"
                string? exclusion = marketingCategory.Equals("NDA", StringComparison.OrdinalIgnoreCase)
                    ? "ANDA"
                    : null;

                query = query.FilterBySearchTerms(
                    marketingCategory,
                    MultiTermBehavior.PartialMatchAny,
                    exclusion,
                    x => x.MarketingCategoryCode,
                    x => x.MarketingCategoryName);

#if DEBUG
                var sql = query.ToQueryString();
                Debug.WriteLine($"Generated SQL: {sql}");
#endif
            }

            query = query.OrderByDescending(s => s.ProductCount);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildApplicationNumberSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ApplicationNumberSummaryDto>();

            #endregion
        }

        #endregion Application Number Navigation

        #region Pharmacologic Class Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by pharmacologic/therapeutic class.
        /// Enables drug discovery by therapeutic category (e.g., "Beta-Adrenergic Blockers").
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="classNameSearch">Search term to match against pharmacologic class names.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductsByPharmacologicClassDto"/> matching the therapeutic class.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchByPharmacologicClassAsync(db, "Beta-Blocker", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ProductsByPharmacologicClass"/>
        /// <seealso cref="Label.PharmacologicClass"/>
        public static async Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassAsync(
            ApplicationDbContext db,
            string classNameSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation
            if(string.IsNullOrWhiteSpace(classNameSearch) || string.IsNullOrWhiteSpace(pkSecret))
            {
                throw new ArgumentException("Class name search and PK secret must be provided.");
            }

            string key = generateCacheKey(nameof(SearchByPharmacologicClassAsync), classNameSearch, page, size);

            var cached = Cached.GetCache<List<ProductsByPharmacologicClassDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByPharmacologicClassAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with class name search
            var query = db.Set<LabelView.ProductsByPharmacologicClass>()
                .AsNoTracking()
                .FilterBySearchTerms(p => p.PharmClassName, classNameSearch, MultiTermBehavior.PartialMatchAny)
                .OrderBy(p => p.PharmClassName)
                .ThenBy(p => p.ProductName);

            query = (IOrderedQueryable<LabelView.ProductsByPharmacologicClass>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductsByPharmacologicClassDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductsByPharmacologicClassDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the pharmacologic class hierarchy showing parent-child relationships.
        /// Enables navigation through therapeutic classification levels.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="PharmacologicClassHierarchyViewDto"/> with hierarchy relationships.</returns>
        /// <seealso cref="LabelView.PharmacologicClassHierarchy"/>
        public static async Task<List<PharmacologicClassHierarchyViewDto>> GetPharmacologicClassHierarchyAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetPharmacologicClassHierarchyAsync), null, page, size);

            var cached = Cached.GetCache<List<PharmacologicClassHierarchyViewDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetPharmacologicClassHierarchyAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.PharmacologicClassHierarchy>()
                .AsNoTracking()
                .OrderBy(h => h.ParentClassName)
                .ThenBy(h => h.ChildClassName);

            query = (IOrderedQueryable<LabelView.PharmacologicClassHierarchy>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildPharmacologicClassHierarchyDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<PharmacologicClassHierarchyViewDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets pharmacologic class summaries with product counts.
        /// Discover which therapeutic classes have the most products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="PharmacologicClassSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.PharmacologicClassSummary"/>
        public static async Task<List<PharmacologicClassSummaryDto>> GetPharmacologicClassSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetPharmacologicClassSummariesAsync), null, page, size);

            var cached = Cached.GetCache<List<PharmacologicClassSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetPharmacologicClassSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.PharmacologicClassSummary>()
                .AsNoTracking()
                .OrderByDescending(s => s.ProductCount);

            query = (IOrderedQueryable<LabelView.PharmacologicClassSummary>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildPharmacologicClassSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<PharmacologicClassSummaryDto>();

            #endregion
        }

        #endregion Pharmacologic Class Navigation

        #region Ingredient Navigation

        /**************************************************************/
        /// <summary>
        /// Gets active ingredient summaries with product, document, and labeler counts.
        /// Discover the most common active ingredients across products in the database.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="minProductCount">Optional minimum product count filter.</param>
        /// <param name="ingredient">Optional ingredient name filter for partial matching on SubstanceName.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientActiveSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.IngredientActiveSummary"/>
        public static async Task<List<IngredientActiveSummaryDto>> GetIngredientActiveSummariesAsync(
            ApplicationDbContext db,
            int? minProductCount,
            string? ingredient,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Include ingredient in cache key for varied results (concatenate filter params for cache key)
            string searchParams = $"{minProductCount}_{ingredient}";
            string key = generateCacheKey(nameof(GetIngredientActiveSummariesAsync), searchParams, page, size);

            var cached = Cached.GetCache<List<IngredientActiveSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetIngredientActiveSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.IngredientActiveSummary>()
                .AsNoTracking()
                .AsQueryable();

            // Apply ingredient name filter if specified (partial match on SubstanceName)
            if (!string.IsNullOrWhiteSpace(ingredient))
            {
                query = query.Where(s => s.SubstanceName != null && s.SubstanceName.Contains(ingredient));
            }

            // Apply minimum product count filter if specified
            if (minProductCount.HasValue)
            {
                query = query.Where(s => s.ProductCount >= minProductCount.Value);
            }

            // Order by product count descending (most common first)
            query = query.OrderByDescending(s => s.ProductCount);

            // Apply pagination
            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildIngredientActiveSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<IngredientActiveSummaryDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets inactive ingredient summaries with product, document, and labeler counts.
        /// Discover the most common inactive (excipient) ingredients across products in the database.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="minProductCount">Optional minimum product count filter.</param>
        /// <param name="ingredient">Optional ingredient name filter for partial matching on SubstanceName.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientInactiveSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.IngredientInactiveSummary"/>
        public static async Task<List<IngredientInactiveSummaryDto>> GetIngredientInactiveSummariesAsync(
            ApplicationDbContext db,
            int? minProductCount,
            string? ingredient,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Include ingredient in cache key for varied results (concatenate filter params for cache key)
            string searchParams = $"{minProductCount}_{ingredient}";
            string key = generateCacheKey(nameof(GetIngredientInactiveSummariesAsync), searchParams, page, size);

            var cached = Cached.GetCache<List<IngredientInactiveSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetIngredientInactiveSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.IngredientInactiveSummary>()
                .AsNoTracking()
                .AsQueryable();

            // Apply ingredient name filter if specified (partial match on SubstanceName)
            if (!string.IsNullOrWhiteSpace(ingredient))
            {
                query = query.Where(s => s.SubstanceName != null && s.SubstanceName.Contains(ingredient));
            }

            // Apply minimum product count filter if specified
            if (minProductCount.HasValue)
            {
                query = query.Where(s => s.ProductCount >= minProductCount.Value);
            }

            // Order by product count descending (most common first)
            query = query.OrderByDescending(s => s.ProductCount);

            // Apply pagination
            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildIngredientInactiveSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<IngredientInactiveSummaryDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches products by ingredient UNII code or substance name.
        /// Enables drug composition queries and ingredient-based discovery.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="unii">Optional UNII code to search for.</param>
        /// <param name="substanceNameSearch">Optional substance name search term.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductsByIngredientDto"/> matching the ingredient criteria.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchByIngredientAsync(db, "R16CO5Y76E", null, secret, logger, 1, 25);
        /// var products2 = await DtoLabelAccess.SearchByIngredientAsync(db, null, "aspirin", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ProductsByIngredient"/>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        public static async Task<List<ProductsByIngredientDto>> SearchByIngredientAsync(
            ApplicationDbContext db,
            string? unii,
            string? substanceNameSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string searchKey = $"{unii ?? ""}-{substanceNameSearch ?? ""}";
            string key = generateCacheKey(nameof(SearchByIngredientAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<ProductsByIngredientDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByIngredientAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with UNII or substance name filter
            var query = db.Set<LabelView.ProductsByIngredient>()
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(p => p.UNII == unii);
            }
            else if (!string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                query = query.FilterBySearchTerms(
                    substanceNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.SubstanceName);
            }


#if DEBUG
            var sql = query.ToQueryString();
            Debug.WriteLine($"Generated SQL: {sql}");
#endif


            query = query.OrderBy(p => p.SubstanceName).ThenBy(p => p.ProductName);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductsByIngredientDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductsByIngredientDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets ingredient summaries with product counts.
        /// Discover most common ingredients across products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="minProductCount">Optional minimum product count filter.</param>
        /// <param name="ingredient">Optional ingredient name filter for partial matching on SubstanceName.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.IngredientSummary"/>
        public static async Task<List<IngredientSummaryDto>> GetIngredientSummariesAsync(
            ApplicationDbContext db,
            int? minProductCount,
            string? ingredient,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Include ingredient in cache key for varied results (concatenate filter params for cache key)
            string searchParams = $"{minProductCount}_{ingredient}";
            string key = generateCacheKey(nameof(GetIngredientSummariesAsync), searchParams, page, size);

            var cached = Cached.GetCache<List<IngredientSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetIngredientSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.IngredientSummary>()
                .AsNoTracking()
                .AsQueryable();

            // Apply ingredient name filter if specified (partial match on SubstanceName)
            if (!string.IsNullOrWhiteSpace(ingredient))
            {
                query = query.Where(s => s.SubstanceName != null && s.SubstanceName.Contains(ingredient));
            }

            // Apply minimum product count filter if specified
            if (minProductCount.HasValue)
            {
                query = query.Where(s => s.ProductCount >= minProductCount.Value);
            }

            query = query.OrderByDescending(s => s.ProductCount);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildIngredientSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<IngredientSummaryDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Advanced ingredient search using the new vw_Ingredients, vw_ActiveIngredients, and vw_InactiveIngredients views.
        /// Provides comprehensive search capabilities with application number filtering, document linkage, and product name matching.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="unii">Optional UNII code for exact match.</param>
        /// <param name="substanceNameSearch">Optional substance name for partial/phonetic matching.</param>
        /// <param name="applicationNumber">Optional application number (e.g., NDA020702, 020702) for filtering.</param>
        /// <param name="applicationType">Optional application type (NDA, ANDA, BLA) for exact match.</param>
        /// <param name="productNameSearch">Optional product name for partial/phonetic matching.</param>
        /// <param name="activeOnly">Optional filter: true = active only, false = inactive only, null = all.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientViewDto"/> matching the search criteria.</returns>
        /// <example>
        /// <code>
        /// // Search by UNII for all ingredient types
        /// var results = await DtoLabelAccess.SearchIngredientsAdvancedAsync(db, "R16CO5Y76E", null, null, null, null, null, secret, logger);
        ///
        /// // Search by substance name with application number filter
        /// var results2 = await DtoLabelAccess.SearchIngredientsAdvancedAsync(db, null, "aspirin", "020702", null, null, null, secret, logger);
        ///
        /// // Search for active ingredients only in a specific product
        /// var results3 = await DtoLabelAccess.SearchIngredientsAdvancedAsync(db, null, null, null, null, "TYLENOL", true, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.IngredientView"/>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        /// <seealso cref="LabelView.InactiveIngredientView"/>
        /// <seealso cref="SearchByIngredientAsync"/>
        public static async Task<List<IngredientViewDto>> SearchIngredientsAdvancedAsync(
            ApplicationDbContext db,
            string? unii,
            string? substanceNameSearch,
            string? applicationNumber,
            string? applicationType,
            string? productNameSearch,
            bool? activeOnly,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Build cache key from all search parameters
            string searchKey = $"{unii ?? ""}-{substanceNameSearch ?? ""}-{applicationNumber ?? ""}-{applicationType ?? ""}-{productNameSearch ?? ""}-{activeOnly?.ToString() ?? "all"}";
            string key = generateCacheKey(nameof(SearchIngredientsAdvancedAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<IngredientViewDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchIngredientsAdvancedAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Determine which view to use based on activeOnly parameter
            List<IngredientViewDto> ret;

            if (activeOnly == true)
            {
                // Use vw_ActiveIngredients
                var query = db.Set<LabelView.ActiveIngredientView>()
                    .AsNoTracking()
                    .AsQueryable();

                query = applyIngredientViewFilters(query, unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch);
                query = query.OrderBy(i => i.SubstanceName).ThenBy(i => i.ProductName);
                query = applyPagination(query, page, size);

                var entities = await query.ToListAsync();
                ret = buildActiveIngredientViewDtos(db, entities, pkSecret, logger);
            }
            else if (activeOnly == false)
            {
                // Use vw_InactiveIngredients
                var query = db.Set<LabelView.InactiveIngredientView>()
                    .AsNoTracking()
                    .AsQueryable();

                query = applyIngredientViewFilters(query, unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch);
                query = query.OrderBy(i => i.SubstanceName).ThenBy(i => i.ProductName);
                query = applyPagination(query, page, size);

                var entities = await query.ToListAsync();
                ret = buildInactiveIngredientViewDtos(db, entities, pkSecret, logger);
            }
            else
            {
                // Use vw_Ingredients (all ingredients)
                var query = db.Set<LabelView.IngredientView>()
                    .AsNoTracking()
                    .AsQueryable();

                query = applyIngredientViewFilters(query, unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch);
                query = query.OrderBy(i => i.SubstanceName).ThenBy(i => i.ProductName);
                query = applyPagination(query, page, size);

                var entities = await query.ToListAsync();
                ret = buildIngredientViewDtos(db, entities, pkSecret, logger);
            }

#if DEBUG
            Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchIngredientsAdvancedAsync)} returned {ret.Count} results ===");
#endif

            // Cache results
            if (ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds products that share the same ingredient as the specified application number.
        /// Useful for finding generic equivalents or related brand products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="applicationNumber">The application number to search (e.g., NDA020702, 020702).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientViewDto"/> for products with the same active ingredients.</returns>
        /// <example>
        /// <code>
        /// // Find all products with the same active ingredient as NDA020702
        /// var results = await DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync(db, "020702", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        /// <seealso cref="SearchIngredientsAdvancedAsync"/>
        public static async Task<List<IngredientViewDto>> FindProductsByApplicationNumberWithSameIngredientAsync(
            ApplicationDbContext db,
            string applicationNumber,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(FindProductsByApplicationNumberWithSameIngredientAsync), applicationNumber, page, size);

            var cached = Cached.GetCache<List<IngredientViewDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(FindProductsByApplicationNumberWithSameIngredientAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Parse the application number to extract normalized form
            var terms = ApplicationNumberSearch.Parse(applicationNumber);

            // Step 1: Find the UNIIs for the specified application number
            var targetUniis = await db.Set<LabelView.ActiveIngredientView>()
                .AsNoTracking()
                .Where(i => i.ApplicationNumber != null &&
                    (
                        i.ApplicationNumber == terms.Normalized ||
                        (terms.IsNumericOnly && i.ApplicationNumber.Contains(terms.NumericOnly)) ||
                        i.ApplicationNumber.Contains(terms.Normalized)
                    ))
                .Select(i => i.UNII)
                .Where(u => u != null)
                .Distinct()
                .ToListAsync();

            if (targetUniis.Count == 0)
            {
                logger.LogDebug($"No ingredients found for application number {applicationNumber}");
                return new List<IngredientViewDto>();
            }

            // Step 2: Find all products with those UNIIs
            var query = db.Set<LabelView.ActiveIngredientView>()
                .AsNoTracking()
                .Where(i => targetUniis.Contains(i.UNII))
                .OrderBy(i => i.SubstanceName)
                .ThenBy(i => i.ProductName);

            query = (IOrderedQueryable<LabelView.ActiveIngredientView>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildActiveIngredientViewDtos(db, entities, pkSecret, logger);

#if DEBUG
            Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(FindProductsByApplicationNumberWithSameIngredientAsync)} returned {ret.Count} results ===");
#endif

            // Cache results
            if (ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds related ingredients for a given ingredient.
        /// Given an ingredient (by UNII or name), finds all products containing it and their other ingredients.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="unii">Optional UNII code to search.</param>
        /// <param name="substanceNameSearch">Optional substance name to search.</param>
        /// <param name="isSearchingActive">True if the input is an active ingredient, false if inactive.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="maxProducts">Maximum number of products to process (default 50).</param>
        /// <returns><see cref="IngredientRelatedResultsDto"/> containing searched, related ingredients, and products.</returns>
        /// <example>
        /// <code>
        /// // Find all products and ingredients related to aspirin (active ingredient)
        /// var related = await DtoLabelAccess.FindRelatedIngredientsAsync(db, "R16CO5Y76E", null, true, secret, logger);
        /// Console.WriteLine($"Found {related.TotalProductCount} products containing this ingredient");
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.IngredientView"/>
        /// <seealso cref="IngredientRelatedResultsDto"/>
        public static async Task<IngredientRelatedResultsDto> FindRelatedIngredientsAsync(
            ApplicationDbContext db,
            string? unii,
            string? substanceNameSearch,
            bool isSearchingActive,
            string pkSecret,
            ILogger logger,
            int maxProducts = 50)
        {
            #region implementation

            string searchKey = $"{unii ?? ""}-{substanceNameSearch ?? ""}-{(isSearchingActive ? "active" : "inactive")}";
            string key = generateCacheKey(nameof(FindRelatedIngredientsAsync), searchKey, null, maxProducts);

            var cached = Cached.GetCache<IngredientRelatedResultsDto>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key}");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(FindRelatedIngredientsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var result = new IngredientRelatedResultsDto();

            // Step 1: Find the searched ingredients and their product IDs
            List<int?> productIds;
            if (isSearchingActive)
            {
                var query = db.Set<LabelView.ActiveIngredientView>()
                    .AsNoTracking()
                    .AsQueryable();

                query = applyIngredientViewFilters(query, unii, substanceNameSearch, null, null, null);
                var searched = await query.Take(maxProducts).ToListAsync();
                result.SearchedIngredients = buildActiveIngredientViewDtos(db, searched, pkSecret, logger);
                productIds = searched.Select(s => s.ProductID).Distinct().ToList();
            }
            else
            {
                var query = db.Set<LabelView.InactiveIngredientView>()
                    .AsNoTracking()
                    .AsQueryable();

                query = applyIngredientViewFilters(query, unii, substanceNameSearch, null, null, null);
                var searched = await query.Take(maxProducts).ToListAsync();
                result.SearchedIngredients = buildInactiveIngredientViewDtos(db, searched, pkSecret, logger);
                productIds = searched.Select(s => s.ProductID).Distinct().ToList();
            }

            if (productIds.Count == 0)
            {
                logger.LogDebug($"No ingredients found for search criteria");
                return result;
            }

            // Step 2: Find all active ingredients for these products
            var activeIngredients = await db.Set<LabelView.ActiveIngredientView>()
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductID))
                .ToListAsync();
            result.RelatedActiveIngredients = buildActiveIngredientViewDtos(db, activeIngredients, pkSecret, logger);
            result.TotalActiveCount = activeIngredients.Select(a => a.UNII).Distinct().Count();

            // Step 3: Find all inactive ingredients for these products
            var inactiveIngredients = await db.Set<LabelView.InactiveIngredientView>()
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductID))
                .ToListAsync();
            result.RelatedInactiveIngredients = buildInactiveIngredientViewDtos(db, inactiveIngredients, pkSecret, logger);
            result.TotalInactiveCount = inactiveIngredients.Select(a => a.UNII).Distinct().Count();

            // Step 4: Get unique products (from active ingredients for product info)
            var uniqueProducts = activeIngredients
                .GroupBy(a => a.ProductID)
                .Select(g => g.First())
                .ToList();
            result.RelatedProducts = buildActiveIngredientViewDtos(db, uniqueProducts, pkSecret, logger);
            result.TotalProductCount = productIds.Count;

#if DEBUG
            Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(FindRelatedIngredientsAsync)} found {result.TotalProductCount} products, {result.TotalActiveCount} active, {result.TotalInactiveCount} inactive ===");
#endif

            // Cache results
            Cached.SetCacheManageKey(key, result, 1.0);
            logger.LogDebug($"Cache set for {key}");

            return result;

            #endregion
        }

        #region Private Ingredient View Helpers

        /**************************************************************/
        /// <summary>
        /// Applies common filters to IngredientView queries.
        /// Shared helper for DRY filter application across ingredient view types.
        /// </summary>
        /// <typeparam name="T">The ingredient view type (IngredientView, ActiveIngredientView, or InactiveIngredientView).</typeparam>
        /// <param name="query">The base query to filter.</param>
        /// <param name="unii">Optional UNII for exact match.</param>
        /// <param name="substanceNameSearch">Optional substance name for partial/phonetic match.</param>
        /// <param name="applicationNumber">Optional application number for flexible match.</param>
        /// <param name="applicationType">Optional application type for exact match.</param>
        /// <param name="productNameSearch">Optional product name for partial/phonetic match.</param>
        /// <returns>The filtered query.</returns>
        private static IQueryable<LabelView.IngredientView> applyIngredientViewFilters(
            IQueryable<LabelView.IngredientView> query,
            string? unii,
            string? substanceNameSearch,
            string? applicationNumber,
            string? applicationType,
            string? productNameSearch)
        {
            #region implementation

            // UNII exact match
            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(i => i.UNII == unii);
            }

            // Substance name partial match
            if (!string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                query = query.FilterBySearchTerms(
                    substanceNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.SubstanceName);
            }

            // Application number flexible match using ApplicationNumberSearch
            if (!string.IsNullOrWhiteSpace(applicationNumber))
            {
                var terms = ApplicationNumberSearch.Parse(applicationNumber);
                query = query.Where(i => i.ApplicationNumber != null &&
                    (
                        // Exact match after normalization
                        i.ApplicationNumber == terms.Normalized ||
                        // Number-only search
                        (terms.IsNumericOnly && i.ApplicationNumber.Contains(terms.NumericOnly)) ||
                        // Prefix-only search
                        (terms.IsPrefixOnly && i.ApplicationNumber.StartsWith(terms.AlphaOnly)) ||
                        // Fallback contains
                        i.ApplicationNumber.Contains(terms.Normalized)
                    ));
            }

            // Application type exact match
            if (!string.IsNullOrWhiteSpace(applicationType))
            {
                var normalizedType = applicationType.ToUpperInvariant();
                query = query.Where(i => i.ApplicationType != null &&
                    i.ApplicationType.ToUpper().Contains(normalizedType));
            }

            // Product name partial match
            if (!string.IsNullOrWhiteSpace(productNameSearch))
            {
                query = query.FilterBySearchTerms(
                    productNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.ProductName);
            }

            return query;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies common filters to ActiveIngredientView queries.
        /// </summary>
        private static IQueryable<LabelView.ActiveIngredientView> applyIngredientViewFilters(
            IQueryable<LabelView.ActiveIngredientView> query,
            string? unii,
            string? substanceNameSearch,
            string? applicationNumber,
            string? applicationType,
            string? productNameSearch)
        {
            #region implementation

            // UNII exact match
            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(i => i.UNII == unii);
            }

            // Substance name partial match
            if (!string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                query = query.FilterBySearchTerms(
                    substanceNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.SubstanceName);
            }

            // Application number flexible match using ApplicationNumberSearch
            if (!string.IsNullOrWhiteSpace(applicationNumber))
            {
                var terms = ApplicationNumberSearch.Parse(applicationNumber);
                query = query.Where(i => i.ApplicationNumber != null &&
                    (
                        i.ApplicationNumber == terms.Normalized ||
                        (terms.IsNumericOnly && i.ApplicationNumber.Contains(terms.NumericOnly)) ||
                        (terms.IsPrefixOnly && i.ApplicationNumber.StartsWith(terms.AlphaOnly)) ||
                        i.ApplicationNumber.Contains(terms.Normalized)
                    ));
            }

            // Application type exact match
            if (!string.IsNullOrWhiteSpace(applicationType))
            {
                var normalizedType = applicationType.ToUpperInvariant();
                query = query.Where(i => i.ApplicationType != null &&
                    i.ApplicationType.ToUpper().Contains(normalizedType));
            }

            // Product name partial match
            if (!string.IsNullOrWhiteSpace(productNameSearch))
            {
                query = query.FilterBySearchTerms(
                    productNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.ProductName);
            }

            return query;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies common filters to InactiveIngredientView queries.
        /// </summary>
        private static IQueryable<LabelView.InactiveIngredientView> applyIngredientViewFilters(
            IQueryable<LabelView.InactiveIngredientView> query,
            string? unii,
            string? substanceNameSearch,
            string? applicationNumber,
            string? applicationType,
            string? productNameSearch)
        {
            #region implementation

            // UNII exact match
            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(i => i.UNII == unii);
            }

            // Substance name partial match
            if (!string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                query = query.FilterBySearchTerms(
                    substanceNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.SubstanceName);
            }

            // Application number flexible match using ApplicationNumberSearch
            if (!string.IsNullOrWhiteSpace(applicationNumber))
            {
                var terms = ApplicationNumberSearch.Parse(applicationNumber);
                query = query.Where(i => i.ApplicationNumber != null &&
                    (
                        i.ApplicationNumber == terms.Normalized ||
                        (terms.IsNumericOnly && i.ApplicationNumber.Contains(terms.NumericOnly)) ||
                        (terms.IsPrefixOnly && i.ApplicationNumber.StartsWith(terms.AlphaOnly)) ||
                        i.ApplicationNumber.Contains(terms.Normalized)
                    ));
            }

            // Application type exact match
            if (!string.IsNullOrWhiteSpace(applicationType))
            {
                var normalizedType = applicationType.ToUpperInvariant();
                query = query.Where(i => i.ApplicationType != null &&
                    i.ApplicationType.ToUpper().Contains(normalizedType));
            }

            // Product name partial match
            if (!string.IsNullOrWhiteSpace(productNameSearch))
            {
                query = query.FilterBySearchTerms(
                    productNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    x => x.ProductName);
            }

            return query;

            #endregion
        }

        #endregion Private Ingredient View Helpers

        #endregion Ingredient Navigation

        #region Product Identifier Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by NDC code or other product identifiers.
        /// Critical for pharmacy system integration and product lookup.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="productCode">The NDC or product code to search for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductsByNDCDto"/> matching the product code.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchByNDCAsync(db, "12345-678-90", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ProductsByNDC"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        public static async Task<List<ProductsByNDCDto>> SearchByNDCAsync(
            ApplicationDbContext db,
            string productCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(SearchByNDCAsync), productCode, page, size);

            var cached = Cached.GetCache<List<ProductsByNDCDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByNDCAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with product code filter - supports partial matches
            var query = db.Set<LabelView.ProductsByNDC>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.ProductCode, productCode, MultiTermBehavior.PartialMatchAny)
                .OrderBy(p => p.ProductCode);

            query = (IOrderedQueryable<LabelView.ProductsByNDC>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductsByNDCDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductsByNDCDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches package configurations by NDC package code.
        /// Shows packaging hierarchy and quantities.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="packageCode">The NDC package code to search for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="PackageByNDCDto"/> matching the package code.</returns>
        /// <seealso cref="LabelView.PackageByNDC"/>
        /// <seealso cref="Label.PackageIdentifier"/>
        public static async Task<List<PackageByNDCDto>> SearchByPackageNDCAsync(
            ApplicationDbContext db,
            string packageCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(SearchByPackageNDCAsync), packageCode, page, size);

            var cached = Cached.GetCache<List<PackageByNDCDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByPackageNDCAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.PackageByNDC>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.PackageCode, packageCode, MultiTermBehavior.PartialMatchAny)
                .OrderBy(p => p.PackageCode);

            query = (IOrderedQueryable<LabelView.PackageByNDC>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildPackageByNDCDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<PackageByNDCDto>();

            #endregion
        }

        #endregion Product Identifier Navigation

        #region Organization Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by labeler organization name.
        /// Lists products by labeler/marketing organization.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="labelerNameSearch">Search term to match against labeler names.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductsByLabelerDto"/> matching the labeler name.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchByLabelerAsync(db, "Pfizer", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ProductsByLabeler"/>
        /// <seealso cref="Label.Organization"/>
        public static async Task<List<ProductsByLabelerDto>> SearchByLabelerAsync(
            ApplicationDbContext db,
            string labelerNameSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(SearchByLabelerAsync), labelerNameSearch, page, size);

            var cached = Cached.GetCache<List<ProductsByLabelerDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchByLabelerAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.ProductsByLabeler>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.LabelerName, labelerNameSearch, MultiTermBehavior.PartialMatchAny)
                .OrderBy(p => p.LabelerName)
                .ThenBy(p => p.ProductName);

            query = (IOrderedQueryable<LabelView.ProductsByLabeler>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductsByLabelerDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductsByLabelerDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets labeler summaries with product counts.
        /// Discover which labelers have the most products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="LabelerSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.LabelerSummary"/>
        public static async Task<List<LabelerSummaryDto>> GetLabelerSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetLabelerSummariesAsync), null, page, size);

            var cached = Cached.GetCache<List<LabelerSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetLabelerSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.LabelerSummary>()
                .AsNoTracking()
                .OrderByDescending(s => s.ProductCount);

            query = (IOrderedQueryable<LabelView.LabelerSummary>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildLabelerSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<LabelerSummaryDto>();

            #endregion
        }

        #endregion Organization Navigation

        #region Document Navigation

        /**************************************************************/
        /// <summary>
        /// Gets document navigation data with version tracking.
        /// Supports discovery of latest document versions and version history.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="latestOnly">If true, returns only the latest version of each document set.</param>
        /// <param name="setGuid">Optional filter by SetGUID to get all versions of a specific document.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="DocumentNavigationDto"/> with document navigation data.</returns>
        /// <example>
        /// <code>
        /// var latestDocs = await DtoLabelAccess.GetDocumentNavigationAsync(db, true, null, secret, logger, 1, 25);
        /// var allVersions = await DtoLabelAccess.GetDocumentNavigationAsync(db, false, setGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.DocumentNavigation"/>
        /// <seealso cref="Label.Document"/>
        public static async Task<List<DocumentNavigationDto>> GetDocumentNavigationAsync(
            ApplicationDbContext db,
            bool latestOnly,
            Guid? setGuid,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string searchKey = $"{latestOnly}-{setGuid?.ToString() ?? "all"}";
            string key = generateCacheKey(nameof(GetDocumentNavigationAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<DocumentNavigationDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetDocumentNavigationAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.DocumentNavigation>()
                .AsNoTracking()
                .AsQueryable();

            // Filter by latest version if requested
            if (latestOnly)
            {
                query = query.Where(d => d.IsLatestVersion>= 1);
            }

            // Filter by SetGUID if provided
            if (setGuid.HasValue)
            {
                query = query.Where(d => d.SetGUID == setGuid.Value);
            }

            query = query.OrderByDescending(d => d.EffectiveDate);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildDocumentNavigationDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<DocumentNavigationDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets document version history for a specific document set.
        /// Tracks all versions over time within a SetGUID or DocumentGUID.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="setGuidOrDocumentGuid">The SetGUID to retrieve version history for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentVersionHistoryDto"/> with version history.</returns>
        /// <seealso cref="LabelView.DocumentVersionHistory"/>
        public static async Task<List<DocumentVersionHistoryDto>> GetDocumentVersionHistoryAsync(
            ApplicationDbContext db,
            Guid setGuidOrDocumentGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetDocumentVersionHistoryAsync), setGuidOrDocumentGuid.ToString(), null, null);

            var cached = Cached.GetCache<List<DocumentVersionHistoryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetDocumentVersionHistoryAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var entities = await db.Set<LabelView.DocumentVersionHistory>()
                .AsNoTracking()
                .Where(d => d.SetGUID == setGuidOrDocumentGuid 
                    || d.DocumentGUID == setGuidOrDocumentGuid)
                .OrderByDescending(d => d.VersionNumber)
                .ToListAsync();

            var ret = buildDocumentVersionHistoryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<DocumentVersionHistoryDto>();

            #endregion
        }

        #endregion Document Navigation

        #region Section Navigation

        /**************************************************************/
        /// <summary>
        /// Searches sections by section code (LOINC).
        /// Enables navigation to specific labeling sections across documents.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="sectionCode">The LOINC section code to search for (e.g., "34066-1" for Boxed Warning).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="SectionNavigationDto"/> matching the section code.</returns>
        /// <example>
        /// <code>
        /// var boxedWarnings = await DtoLabelAccess.SearchBySectionCodeAsync(db, "34066-1", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.SectionNavigation"/>
        /// <seealso cref="Label.Section"/>
        public static async Task<List<SectionNavigationDto>> SearchBySectionCodeAsync(
            ApplicationDbContext db,
            string sectionCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(SearchBySectionCodeAsync), sectionCode, page, size);

            var cached = Cached.GetCache<List<SectionNavigationDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchBySectionCodeAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.SectionNavigation>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.SectionCode, sectionCode)
                .OrderBy(s => s.DocumentTitle);

            query = (IOrderedQueryable<LabelView.SectionNavigation>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildSectionNavigationDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<SectionNavigationDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section type summaries with document counts.
        /// Discover which section types are most common.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="SectionTypeSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.SectionTypeSummary"/>
        public static async Task<List<SectionTypeSummaryDto>> GetSectionTypeSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetSectionTypeSummariesAsync), null, page, size);

            var cached = Cached.GetCache<List<SectionTypeSummaryDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetSectionTypeSummariesAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.SectionTypeSummary>()
                .AsNoTracking()
                .OrderByDescending(s => s.DocumentCount);

            query = (IOrderedQueryable<LabelView.SectionTypeSummary>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildSectionTypeSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<SectionTypeSummaryDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section text content for a document, optionally filtered by section.
        /// Provides efficient text retrieval for AI summarization workflows.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">The document GUID to retrieve section content for.</param>
        /// <param name="sectionGuid">Optional section GUID to filter to a specific section.</param>
        /// <param name="sectionCode">Optional LOINC section code to filter by section type (e.g., "34084-4" for Adverse Reactions).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="SectionContentDto"/> containing section text content.</returns>
        /// <example>
        /// <code>
        /// // Get all section content for a document
        /// var allContent = await DtoLabelAccess.GetSectionContentAsync(db, documentGuid, null, null, secret, logger);
        /// 
        /// // Get specific section by GUID
        /// var section = await DtoLabelAccess.GetSectionContentAsync(db, documentGuid, sectionGuid, null, secret, logger);
        /// 
        /// // Get all Adverse Reactions sections
        /// var adverseReactions = await DtoLabelAccess.GetSectionContentAsync(db, documentGuid, null, "34084-4", secret, logger);
        /// </code>
        /// </example>
        /// <remarks>
        /// Uses the vw_SectionContent view for optimized joins.
        /// Results are cached for improved performance on repeated queries.
        /// Content is ordered by SectionCode then SequenceNumber for proper reading order.
        /// 
        /// Common LOINC codes:
        /// - 34066-1: Boxed Warning
        /// - 34067-9: Indications and Usage
        /// - 34068-7: Dosage and Administration
        /// - 34069-5: Contraindications
        /// - 43685-7: Warnings and Precautions
        /// - 34084-4: Adverse Reactions
        /// - 34073-7: Drug Interactions
        /// - 34088-5: Overdosage
        /// </remarks>
        /// <seealso cref="LabelView.SectionContent"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.SectionTextContent"/>
        public static async Task<List<SectionContentDto>> GetSectionContentAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            Guid? sectionGuid,
            string? sectionCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Generate cache key including all filter parameters
            string key = generateCacheKey(
                nameof(GetSectionContentAsync),
                $"{documentGuid}_{sectionGuid}_{sectionCode}",
                page,
                size);

            var cached = Cached.GetCache<List<SectionContentDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetSectionContentAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with filters
            var query = db.Set<LabelView.SectionContent>()
                .AsNoTracking()
                .Where(s => s.DocumentGUID == documentGuid);

            // Apply optional section filters - match on either GUID OR Code
            if (sectionGuid.HasValue || !string.IsNullOrWhiteSpace(sectionCode))
            {
                query = query.Where(s =>
                    (sectionGuid.HasValue && s.SectionGUID == sectionGuid.Value) ||
                    (!string.IsNullOrWhiteSpace(sectionCode) && s.SectionCode != null && s.SectionCode.Contains(sectionCode)));
            }

            // Order by section code then sequence for proper reading order
            query = query
                .OrderBy(s => s.SectionCode)
                .ThenBy(s => s.SequenceNumber);

            // Apply pagination
            query = (IOrderedQueryable<LabelView.SectionContent>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildSectionContentDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<SectionContentDto>();

            #endregion
        }

        #endregion Section Navigation

        #region Drug Safety Navigation

        /**************************************************************/
        /// <summary>
        /// Gets potential drug interactions based on shared active ingredients.
        /// Supports pharmacist review and clinical decision support.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="ingredientUNIIs">List of UNII codes to check for interactions.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="DrugInteractionLookupDto"/> with potential interactions.</returns>
        /// <example>
        /// <code>
        /// var interactions = await DtoLabelAccess.GetDrugInteractionsAsync(db, new[] { "UNII1", "UNII2" }, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.DrugInteractionLookup"/>
        public static async Task<List<DrugInteractionLookupDto>> GetDrugInteractionsAsync(
            ApplicationDbContext db,
            IEnumerable<string> ingredientUNIIs,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string searchKey = string.Join(",", ingredientUNIIs.OrderBy(u => u));
            string key = generateCacheKey(nameof(GetDrugInteractionsAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<DrugInteractionLookupDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetDrugInteractionsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var uniiList = ingredientUNIIs.ToList();

            var query = db.Set<LabelView.DrugInteractionLookup>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.IngredientUNII, searchKey, MultiTermBehavior.PartialMatchAny)
                .OrderBy(d => d.ProductName);

            query = (IOrderedQueryable<LabelView.DrugInteractionLookup>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildDrugInteractionLookupDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<DrugInteractionLookupDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets products with DEA controlled substance schedules.
        /// Important for pharmacy compliance and controlled substance management.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="scheduleCode">Optional filter by specific DEA schedule (e.g., "CII", "CIII").</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="DEAScheduleLookupDto"/> with DEA schedule information.</returns>
        /// <seealso cref="LabelView.DEAScheduleLookup"/>
        public static async Task<List<DEAScheduleLookupDto>> GetDEAScheduleProductsAsync(
            ApplicationDbContext db,
            string? scheduleCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetDEAScheduleProductsAsync), scheduleCode, page, size);

            var cached = Cached.GetCache<List<DEAScheduleLookupDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetDEAScheduleProductsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.DEAScheduleLookup>()
                .AsNoTracking()
                .Where(d => d.DEAScheduleCode != null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(scheduleCode))
            {
                query = query.FilterBySearchTerms(x => x.DEAScheduleCode, scheduleCode);
            }

            query = query.OrderBy(d => d.DEAScheduleCode).ThenBy(d => d.ProductName);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildDEAScheduleLookupDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<DEAScheduleLookupDto>();

            #endregion
        }

        #endregion Drug Safety Navigation

        #region Product Summary and Cross-Reference

        /**************************************************************/
        /// <summary>
        /// Searches for products with comprehensive summary information.
        /// Provides a complete product overview with key attributes.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="productNameSearch">Search term to match against product names.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductSummaryViewDto"/> matching the product name.</returns>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.SearchProductSummaryAsync(db, "Lipitor", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.ProductSummary"/>
        public static async Task<List<ProductSummaryViewDto>> SearchProductSummaryAsync(
            ApplicationDbContext db,
            string productNameSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateCacheKey(nameof(SearchProductSummaryAsync), productNameSearch, page, size);

            var cached = Cached.GetCache<List<ProductSummaryViewDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(SearchProductSummaryAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.ProductSummary>()
                .AsNoTracking()
                .FilterBySearchTerms(x => x.ProductName, productNameSearch, MultiTermBehavior.PartialMatchAny)
                .OrderBy(p => p.ProductName);

            query = (IOrderedQueryable<LabelView.ProductSummary>)applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductSummaryDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductSummaryViewDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets related products by shared application number or active ingredient.
        /// Useful for finding alternatives, generics, or similar drugs.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="sourceProductId">Optional source product ID to find related products for. Either this or sourceDocumentGuid must be provided.</param>
        /// <param name="sourceDocumentGuid">Optional source document GUID to find related products for. Either this or sourceProductId must be provided.</param>
        /// <param name="relationshipType">Optional filter by relationship type (SameApplicationNumber, SameActiveIngredient).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="RelatedProductsDto"/> with related products.</returns>
        /// <seealso cref="LabelView.RelatedProducts"/>
        /// <seealso cref="LabelView.ProductLatestLabel"/>
        public static async Task<List<RelatedProductsDto>> GetRelatedProductsAsync(
            ApplicationDbContext db,
            int? sourceProductId,
            Guid? sourceDocumentGuid,
            string? relationshipType,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Build search key including both identifiers
            string searchKey = $"{sourceProductId?.ToString() ?? "null"}-{sourceDocumentGuid?.ToString() ?? "null"}-{relationshipType ?? "all"}";
            string key = generateCacheKey(nameof(GetRelatedProductsAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<RelatedProductsDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetRelatedProductsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.RelatedProducts>()
                .AsNoTracking()
                .AsQueryable();

            // Filter by sourceProductId if provided
            if (sourceProductId.HasValue && sourceProductId.Value > 0)
            {
                query = query.Where(r => r.SourceProductID == sourceProductId.Value);
            }

            // Filter by sourceDocumentGuid if provided (takes precedence if both are provided)
            if (sourceDocumentGuid.HasValue)
            {
                query = query.Where(r => r.SourceDocumentGUID == sourceDocumentGuid.Value);
            }

            // Filter by relationship type if provided
            if (!string.IsNullOrWhiteSpace(relationshipType))
            {
                query = query.Where(r => r.RelationshipType == relationshipType);
            }

            query = query.OrderBy(r => r.RelatedProductName);

            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildRelatedProductsDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<RelatedProductsDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the API endpoint guide for AI-assisted endpoint discovery.
        /// Claude API queries this to understand available navigation views and usage patterns.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="category">Optional filter by category.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="APIEndpointGuideDto"/> with endpoint metadata.</returns>
        /// <seealso cref="LabelView.APIEndpointGuide"/>
        public static async Task<List<APIEndpointGuideDto>> GetAPIEndpointGuideAsync(
            ApplicationDbContext db,
            string? category,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            string key = generateCacheKey(nameof(GetAPIEndpointGuideAsync), category, null, null);

            var cached = Cached.GetCache<List<APIEndpointGuideDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetAPIEndpointGuideAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            var query = db.Set<LabelView.APIEndpointGuide>()
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(e => e.Category == category);
            }

            query = query.OrderBy(e => e.Category).ThenBy(e => e.ViewName);

            var entities = await query.ToListAsync();
            var ret = buildAPIEndpointGuideDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 5.0); // Cache longer since this is metadata
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<APIEndpointGuideDto>();

            #endregion
        }

        #endregion Product Summary and Cross-Reference

        #region Latest Label Navigation

        /**************************************************************/
        /// <summary>
        /// Gets the latest label for each product/active ingredient combination.
        /// Use this to find the most recent labeling for a product or ingredient.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="unii">Optional UNII code for exact match filtering.</param>
        /// <param name="productNameSearch">Optional product name for partial matching.</param>
        /// <param name="activeIngredientSearch">Optional active ingredient name for partial matching.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductLatestLabelDto"/> with latest label for each product/ingredient.</returns>
        /// <example>
        /// <code>
        /// // Find latest label for a specific UNII
        /// var labels = await DtoLabelAccess.GetProductLatestLabelsAsync(db, "R16CO5Y76E", null, null, secret, logger, 1, 25);
        ///
        /// // Find latest label for a product name search
        /// var labels2 = await DtoLabelAccess.GetProductLatestLabelsAsync(db, null, "Lipitor", null, secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <remarks>
        /// The view returns only one row per UNII/ProductName combination, selecting the document
        /// with the most recent EffectiveTime. This is useful for finding the current labeling
        /// rather than historical versions.
        /// </remarks>
        /// <seealso cref="LabelView.ProductLatestLabel"/>
        /// <seealso cref="LabelView.IngredientActiveSummary"/>
        /// <seealso cref="LabelView.ProductsByIngredient"/>
        public static async Task<List<ProductLatestLabelDto>> GetProductLatestLabelsAsync(
            ApplicationDbContext db,
            string? unii,
            string? productNameSearch,
            string? activeIngredientSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Build cache key from search parameters
            string searchKey = $"{unii ?? ""}-{productNameSearch ?? ""}-{activeIngredientSearch ?? ""}";
            string key = generateCacheKey(nameof(GetProductLatestLabelsAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<ProductLatestLabelDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetProductLatestLabelsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with optional filters
            var query = db.Set<LabelView.ProductLatestLabel>()
                .AsNoTracking()
                .AsQueryable();

            // Apply UNII exact match filter
            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(p => p.UNII == unii);
            }

            // Apply product name partial match filter (no phonetic matching for precision)
            if (!string.IsNullOrWhiteSpace(productNameSearch))
            {
                query = query.FilterBySearchTerms(
                    productNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.ProductName);
            }

            // Apply active ingredient partial match filter (no phonetic matching for precision)
            if (!string.IsNullOrWhiteSpace(activeIngredientSearch))
            {
                query = query.FilterBySearchTerms(
                    activeIngredientSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.ActiveIngredient);
            }

#if DEBUG
            var sql = query.ToQueryString();
            Debug.WriteLine($"Generated SQL: {sql}");
#endif

            // Order by active ingredient then product name
            query = query.OrderBy(p => p.ActiveIngredient).ThenBy(p => p.ProductName);

            // Apply pagination
            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductLatestLabelDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductLatestLabelDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets product indication text combined with active ingredients.
        /// Use this to find indication information for products by UNII, product name, substance name, or indication text.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="unii">Optional UNII code for exact match filtering.</param>
        /// <param name="productNameSearch">Optional product name for partial matching.</param>
        /// <param name="substanceNameSearch">Optional substance name for partial matching.</param>
        /// <param name="indicationSearch">Optional indication text search for finding products by clinical indication.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="ProductIndicationsDto"/> with indication text.</returns>
        /// <example>
        /// <code>
        /// // Find indications for a specific UNII
        /// var indications = await DtoLabelAccess.GetProductIndicationsAsync(db, "R16CO5Y76E", null, null, null, secret, logger, 1, 25);
        ///
        /// // Find indications for a product name search
        /// var indications2 = await DtoLabelAccess.GetProductIndicationsAsync(db, null, "Lipitor", null, null, secret, logger, 1, 25);
        ///
        /// // Find products indicated for hypertension
        /// var indications3 = await DtoLabelAccess.GetProductIndicationsAsync(db, null, null, null, "hypertension", secret, logger, 1, 25);
        /// </code>
        /// </example>
        /// <remarks>
        /// The view filters to INDICATION sections only and excludes inactive ingredients (IACT class).
        /// Combines ContentText and ItemText from SectionTextContent and TextListItem into a single text column.
        /// The indicationSearch parameter searches within the ContentText field for clinical indication terms.
        /// Multiple search terms are OR-matched (any term can match).
        /// </remarks>
        /// <seealso cref="LabelView.ProductIndications"/>
        /// <seealso cref="LabelView.SectionNavigation"/>
        /// <seealso cref="LabelView.IngredientView"/>
        public static async Task<List<ProductIndicationsDto>> GetProductIndicationsAsync(
            ApplicationDbContext db,
            string? unii,
            string? productNameSearch,
            string? substanceNameSearch,
            string? indicationSearch,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Build cache key from search parameters
            string searchKey = $"{unii ?? ""}-{productNameSearch ?? ""}-{substanceNameSearch ?? ""}-{indicationSearch ?? ""}";
            string key = generateCacheKey(nameof(GetProductIndicationsAsync), searchKey, page, size);

            var cached = Cached.GetCache<List<ProductIndicationsDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetProductIndicationsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with optional filters
            var query = db.Set<LabelView.ProductIndications>()
                .AsNoTracking()
                .AsQueryable();

            // Apply UNII exact match filter
            if (!string.IsNullOrWhiteSpace(unii))
            {
                query = query.Where(p => p.UNII == unii);
            }

            // Apply product name partial match filter (no phonetic matching for precision)
            if (!string.IsNullOrWhiteSpace(productNameSearch))
            {
                query = query.FilterBySearchTerms(
                    productNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.ProductName);
            }

            // Apply substance name partial match filter (no phonetic matching for precision)
            if (!string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                query = query.FilterBySearchTerms(
                    substanceNameSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.SubstanceName);
            }

            // Apply indication text search filter (no phonetic matching for clinical precision)
            // Uses PartialMatchAny - any of the search terms can match within ContentText
            if (!string.IsNullOrWhiteSpace(indicationSearch))
            {
                query = query.FilterBySearchTerms(
                    indicationSearch,
                    MultiTermBehavior.PartialMatchAny,
                    null,
                    PhoneticMatchOptions.None,
                    p => p.ContentText);
            }

#if DEBUG
            var sql = query.ToQueryString();
            Debug.WriteLine($"Generated SQL: {sql}");
#endif

            // Order by substance name then product name
            query = query.OrderBy(p => p.SubstanceName).ThenBy(p => p.ProductName);

            // Apply pagination
            query = applyPagination(query, page, size);

            var entities = await query.ToListAsync();
            var ret = buildProductIndicationsDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<ProductIndicationsDto>();

            #endregion
        }

        #endregion Latest Label Navigation

        #region Section Markdown Navigation

        /**************************************************************/
        /// <summary>
        /// Gets markdown-formatted section content for a document by DocumentGUID.
        /// Returns aggregated, LLM-ready section text from the vw_LabelSectionMarkdown view.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">The unique identifier for the document to retrieve sections for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="sectionCode">
        /// Optional LOINC section code to filter results (e.g., "34067-9" for Indications).
        /// When provided, only sections matching this code are returned, significantly reducing payload size.
        /// </param>
        /// <returns>List of <see cref="LabelSectionMarkdownDto"/> with markdown-formatted section content.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        ///
        /// // Get all sections
        /// var sections = await DtoLabelAccess.GetLabelSectionMarkdownAsync(db, documentGuid, secret, logger);
        ///
        /// // Get specific section (reduces payload from ~88KB to ~1-2KB)
        /// var indications = await DtoLabelAccess.GetLabelSectionMarkdownAsync(db, documentGuid, secret, logger, "34067-9");
        ///
        /// // Access section content
        /// foreach (var section in sections)
        /// {
        ///     Console.WriteLine($"Section: {section.SectionTitle}");
        ///     Console.WriteLine(section.FullSectionText);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Uses the vw_LabelSectionMarkdown view which:
        /// - Aggregates all ContentText rows for each section using STRING_AGG
        /// - Converts SPL formatting tags to markdown (bold, italics, underline)
        /// - Prepends section title as markdown header (## SectionTitle)
        /// - Returns sections ordered by SectionCode for consistent reading order
        ///
        /// This method is designed for AI/LLM summarization workflows where
        /// authoritative label content is needed rather than relying on training data.
        ///
        /// **Token Optimization:** When comparing multiple drugs, use the sectionCode parameter
        /// to fetch only the relevant section(s), reducing payload from ~88KB to ~1-2KB per section.
        /// </remarks>
        /// <seealso cref="LabelView.LabelSectionMarkdown"/>
        /// <seealso cref="LabelSectionMarkdownDto"/>
        /// <seealso cref="GenerateLabelMarkdownAsync"/>
        public static async Task<List<LabelSectionMarkdownDto>> GetLabelSectionMarkdownAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            string? sectionCode = null)
        {
            #region implementation

            // Generate cache key for this document (include sectionCode if provided)
            string key = generateCacheKey(nameof(GetLabelSectionMarkdownAsync), $"{documentGuid.ToString()}{sectionCode}", null, null);

            var cached = Cached.GetCache<List<LabelSectionMarkdownDto>>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} results.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GetLabelSectionMarkdownAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query with optional section code filter for server-side filtering
            var query = db.Set<LabelView.LabelSectionMarkdown>()
                .AsNoTracking()
                .Where(s => s.DocumentGUID == documentGuid);

            // Apply section code filter if provided (reduces payload from ~88KB to ~1-2KB)
            if (!string.IsNullOrWhiteSpace(sectionCode))
            {
                query = query.Where(s => s.SectionCode == sectionCode);
            }

            var entities = await query
                .OrderBy(s => s.SectionCode)
                .ToListAsync();

            var ret = buildLabelSectionMarkdownDtos(db, entities, pkSecret, logger);

            if (ret != null && ret.Count > 0)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} results.");
            }

            return ret ?? new List<LabelSectionMarkdownDto>();

            #endregion
        }

        /**************************************************************/
            /// <summary>
            /// Generates a complete markdown document from all sections for a given DocumentGUID.
            /// Combines section content with header information for AI skill augmentation.
            /// </summary>
            /// <param name="db">The application database context.</param>
            /// <param name="documentGuid">The unique identifier for the document to export.</param>
            /// <param name="pkSecret">Secret used for ID encryption.</param>
            /// <param name="logger">Logger instance for diagnostics.</param>
            /// <returns>A <see cref="LabelMarkdownExportDto"/> containing the complete markdown and metadata.</returns>
            /// <example>
            /// <code>
            /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
            /// var export = await DtoLabelAccess.GenerateLabelMarkdownAsync(db, documentGuid, secret, logger);
            ///
            /// // Use the complete markdown
            /// Console.WriteLine(export.FullMarkdown);
            ///
            /// // Or access metadata
            /// Console.WriteLine($"Document: {export.DocumentTitle}, Sections: {export.SectionCount}");
            /// </code>
            /// </example>
            /// <remarks>
            /// The generated markdown includes:
            /// - Header with document title and metadata
            /// - Data dictionary explaining the structure
            /// - All sections in order with ## headers
            /// - Content with markdown formatting converted from SPL tags
            ///
            /// This format is designed for AI skill augmentation workflows where the Claude API
            /// needs authoritative, complete label content to generate accurate summaries
            /// rather than relying on training data.
            ///
            /// **Use Case:** Building Claude API skills that need to summarize or analyze
            /// FDA drug label content accurately and completely.
            /// </remarks>
            /// <seealso cref="GetLabelSectionMarkdownAsync"/>
            /// <seealso cref="LabelMarkdownExportDto"/>
            /// <seealso cref="LabelView.LabelSectionMarkdown"/>
        public static async Task<LabelMarkdownExportDto> GenerateLabelMarkdownAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Generate cache key for this document export
            string key = generateCacheKey(nameof(GenerateLabelMarkdownAsync), documentGuid.ToString(), null, null);

            var cached = Cached.GetCache<LabelMarkdownExportDto>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for {key}.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GenerateLabelMarkdownAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Get all sections for this document
            var sections = await GetLabelSectionMarkdownAsync(db, documentGuid, pkSecret, logger);

            // Extract document metadata from first section (all sections share the same document info)
            var firstSection = sections.FirstOrDefault();
            var documentTitle = firstSection?.DocumentTitle;
            var setGuid = firstSection?.SetGUID;

            // Generate the complete markdown document
            var fullMarkdown = generateLabelMarkdown(sections, documentGuid, setGuid, documentTitle);

            // Build the export DTO
            var ret = new LabelMarkdownExportDto
            {
                DocumentGUID = documentGuid,
                SetGUID = setGuid,
                DocumentTitle = documentTitle,
                SectionCount = sections.Count,
                TotalContentBlocks = sections.Sum(s => s.ContentBlockCount ?? 0),
                FullMarkdown = fullMarkdown
            };

            Cached.SetCacheManageKey(key, ret, 1.0);
            logger.LogDebug($"Cache set for {key} with {sections.Count} sections.");

            return ret;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates clean, human-readable markdown for display from a document's label sections.
        /// Uses Claude AI to transform raw SPL content into properly formatted markdown.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">The unique identifier for the document to export.</param>
        /// <param name="claudeApiService">The Claude API service for markdown cleanup.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>Clean, formatted markdown string suitable for display.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        /// var cleanMarkdown = await DtoLabelAccess.GenerateCleanLabelMarkdownAsync(
        ///     db, documentGuid, claudeApiService, secret, logger);
        ///
        /// // Returns clean markdown like:
        /// // # Lipitor Tablets
        /// // ## INDICATIONS AND USAGE
        /// // Lipitor is indicated for...
        /// </code>
        /// </example>
        /// <remarks>
        /// This method:
        /// 1. Retrieves all sections from vw_LabelSectionMarkdown for the document
        /// 2. Combines section content into raw markdown
        /// 3. Passes the raw content to Claude API for cleanup and formatting
        /// 4. Returns clean, human-readable markdown
        ///
        /// The result is cached for 1 hour to minimize Claude API calls.
        /// If Claude API fails, the raw markdown is returned as fallback.
        ///
        /// **Use Case:** Static web app display, documentation generation,
        /// markdown rendering in React/Angular applications.
        /// </remarks>
        /// <seealso cref="GetLabelSectionMarkdownAsync"/>
        /// <seealso cref="GenerateLabelMarkdownAsync"/>
        /// <seealso cref="IClaudeApiService.GenerateCleanMarkdownAsync"/>
        public static async Task<string> GenerateCleanLabelMarkdownAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            Service.IClaudeApiService claudeApiService,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Generate cache key for this clean markdown export
            string key = generateCacheKey(nameof(GenerateCleanLabelMarkdownAsync), documentGuid.ToString(), null, null);

            var cached = Cached.GetCache<string>(key);

            if (cached != null)
            {
                logger.LogDebug($"Cache hit for clean markdown {key}.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(GenerateCleanLabelMarkdownAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Get all sections for this document
            var sections = await GetLabelSectionMarkdownAsync(db, documentGuid, pkSecret, logger);

            if (sections == null || sections.Count == 0)
            {
                logger.LogWarning("No sections found for DocumentGUID {DocumentGuid}", documentGuid);
                return string.Empty;
            }

            // Extract document title from first section
            var documentTitle = sections.FirstOrDefault()?.DocumentTitle;

            // Combine all section content into raw markdown (without our metadata header)
            var rawSectionContent = string.Join(
                "\n\n",
                sections
                    .Where(s => !string.IsNullOrWhiteSpace(s.FullSectionText))
                    .Select(s => s.FullSectionText));

            // Call Claude API to clean up the markdown
            var cleanMarkdown = await claudeApiService.GenerateCleanMarkdownAsync(rawSectionContent, documentTitle);

            // Cache the result for 1 hour
            Cached.SetCacheManageKey(key, cleanMarkdown, 1.0);
            logger.LogDebug($"Cache set for clean markdown {key}.");

            return cleanMarkdown;

            #endregion
        }

        #endregion Section Markdown Navigation

        #endregion View-Based Public Entry Points

    }
}