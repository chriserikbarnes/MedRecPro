
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
        /// <returns>List of <see cref="DocumentDto"/> representing the fetched documents and their related entities.</returns>
        /// <example>
        /// <code>
        /// var documents = await DtoLabelHelper.BuildDocumentsAsync(db, secret, logger, 1, 10);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses sequential awaits instead of Task.WhenAll to ensure DbContext thread-safety.
        /// Each document is processed individually to build its complete DTO graph.
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
           int? size = null)
        {
            #region implementation

            string key = ($"{nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)}_{page}_{size}").Base64Encode();

            var cached = Cached.GetCache<List<DocumentDto>>(key);

            if(cached != null && page == null && size == null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} documents.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)} Cache Hit for {key} ===");
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
            var ret = await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger);

            if(ret != null)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} documents.");
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
        /// <returns>List of <see cref="DocumentDto"/> containing the document if found, empty list otherwise.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        /// var documents = await DtoLabelHelper.BuildDocumentsAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses sequential awaits instead of Task.WhenAll to ensure DbContext thread-safety.
        /// Returns a list for consistency with the paginated overload, but will contain at most one document.
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
           ILogger logger)
        {
            #region implementation

            string key = ($"{nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)}.{documentGuid}").Base64Encode();

            var cached = Cached.GetCache<List<DocumentDto>>(key);

            if(cached != null)
            {
                logger.LogDebug($"Cache hit for {key} with {cached.Count} documents.");
#if DEBUG
                Debug.WriteLine($"=== {nameof(DtoLabelAccess)}.{nameof(BuildDocumentsAsync)} Cache Hit for {key} ===");
#endif
                return cached;
            }

            // Build query for specific document by GUID
            var docs = await db.Set<Label.Document>()
                .AsNoTracking()
                .Where(d => d.DocumentGUID == documentGuid)
                .ToListAsync();

            var ret = await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger);

            if(ret != null)
            {
                Cached.SetCacheManageKey(key, ret, 1.0);
                logger.LogDebug($"Cache set for {key} with {ret.Count} documents.");
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
            string key = generateViewCacheKey(nameof(SearchByApplicationNumberAsync), applicationNumber, page, size);

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
        /// <param name="marketingCategoryCode">Optional filter by marketing category (NDA, ANDA, BLA).</param>
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
            string? marketingCategoryCode,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateViewCacheKey(nameof(GetApplicationNumberSummariesAsync), marketingCategoryCode, page, size);

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

            if (!string.IsNullOrWhiteSpace(marketingCategoryCode))
            {
                query = query.Where(s => s.MarketingCategoryCode == marketingCategoryCode);
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

            string key = generateViewCacheKey(nameof(SearchByPharmacologicClassAsync), classNameSearch, page, size);

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
                .Where(p => p.PharmClassName != null && p.PharmClassName.Contains(classNameSearch))
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

            string key = generateViewCacheKey(nameof(GetPharmacologicClassHierarchyAsync), null, page, size);

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

            string key = generateViewCacheKey(nameof(GetPharmacologicClassSummariesAsync), null, page, size);

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
            string key = generateViewCacheKey(nameof(SearchByIngredientAsync), searchKey, page, size);

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
                query = query.Where(p => p.SubstanceName != null && p.SubstanceName.Contains(substanceNameSearch));
            }

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
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="IngredientSummaryDto"/> with aggregated counts.</returns>
        /// <seealso cref="LabelView.IngredientSummary"/>
        public static async Task<List<IngredientSummaryDto>> GetIngredientSummariesAsync(
            ApplicationDbContext db,
            int? minProductCount,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string key = generateViewCacheKey(nameof(GetIngredientSummariesAsync), minProductCount?.ToString(), page, size);

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

            string key = generateViewCacheKey(nameof(SearchByNDCAsync), productCode, page, size);

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
                .Where(p => p.ProductCode != null && p.ProductCode.Contains(productCode))
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

            string key = generateViewCacheKey(nameof(SearchByPackageNDCAsync), packageCode, page, size);

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
                .Where(p => p.PackageCode != null && p.PackageCode.Contains(packageCode))
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

            string key = generateViewCacheKey(nameof(SearchByLabelerAsync), labelerNameSearch, page, size);

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
                .Where(p => p.LabelerName != null && p.LabelerName.Contains(labelerNameSearch))
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

            string key = generateViewCacheKey(nameof(GetLabelerSummariesAsync), null, page, size);

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
            string key = generateViewCacheKey(nameof(GetDocumentNavigationAsync), searchKey, page, size);

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
        /// Tracks all versions over time within a SetGUID.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="setGuid">The SetGUID to retrieve version history for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentVersionHistoryDto"/> with version history.</returns>
        /// <seealso cref="LabelView.DocumentVersionHistory"/>
        public static async Task<List<DocumentVersionHistoryDto>> GetDocumentVersionHistoryAsync(
            ApplicationDbContext db,
            Guid setGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            string key = generateViewCacheKey(nameof(GetDocumentVersionHistoryAsync), setGuid.ToString(), null, null);

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
                .Where(d => d.SetGUID == setGuid)
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

            string key = generateViewCacheKey(nameof(SearchBySectionCodeAsync), sectionCode, page, size);

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
                .Where(s => s.SectionCode == sectionCode)
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

            string key = generateViewCacheKey(nameof(GetSectionTypeSummariesAsync), null, page, size);

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
            string key = generateViewCacheKey(nameof(GetDrugInteractionsAsync), searchKey, page, size);

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
                .Where(d => d.IngredientUNII != null && uniiList.Contains(d.IngredientUNII))
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

            string key = generateViewCacheKey(nameof(GetDEAScheduleProductsAsync), scheduleCode, page, size);

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
                query = query.Where(d => d.DEAScheduleCode == scheduleCode);
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

            string key = generateViewCacheKey(nameof(SearchProductSummaryAsync), productNameSearch, page, size);

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
                .Where(p => p.ProductName != null && p.ProductName.Contains(productNameSearch))
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
        /// <param name="sourceProductId">The source product ID to find related products for.</param>
        /// <param name="relationshipType">Optional filter by relationship type (SameApplicationNumber, SameActiveIngredient).</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="RelatedProductsDto"/> with related products.</returns>
        /// <seealso cref="LabelView.RelatedProducts"/>
        public static async Task<List<RelatedProductsDto>> GetRelatedProductsAsync(
            ApplicationDbContext db,
            int sourceProductId,
            string? relationshipType,
            string pkSecret,
            ILogger logger,
            int? page = null,
            int? size = null)
        {
            #region implementation

            string searchKey = $"{sourceProductId}-{relationshipType ?? "all"}";
            string key = generateViewCacheKey(nameof(GetRelatedProductsAsync), searchKey, page, size);

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
                .Where(r => r.SourceProductID == sourceProductId)
                .AsQueryable();

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

            string key = generateViewCacheKey(nameof(GetAPIEndpointGuideAsync), category, null, null);

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

        #endregion View-Based Public Entry Points

       

    }
}