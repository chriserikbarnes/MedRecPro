using MedRecPro.Data;
using MedRecPro.Features.AeDashboard.Mapping;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Creates short-lived AE dashboard services for legacy static facade callers.
    /// </summary>
    /// <remarks>
    /// This is the sole construction boundary outside the composition root. It uses
    /// the caller-supplied context and logger, never creates a scope, never disposes
    /// those caller-owned dependencies, and never caches a constructed service.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDashboardDataAccess"/>
    internal static class AeDashboardLegacyCompatibility
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Creates the common AE dashboard implementation for a legacy call.
        /// </summary>
        /// <returns>A non-cached feature implementation with legacy-safe policies.</returns>
        /// <seealso cref="AeDashboardDataAccess"/>
        internal static AeDashboardDataAccess CreateDataAccess()
        {
            #region implementation

            var encryptedIdMapper = new AeDashboardEncryptedIdMapper();
            return new AeDashboardDataAccess(
                new AeDashboardCachePolicy(new PerformanceAppCache()),
                encryptedIdMapper,
                new AeDashboardCorrelationPolicy(),
                new AeDashboardDtoMapper(encryptedIdMapper));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a legacy product catalog service.
        /// </summary>
        /// <param name="db">The caller-owned application database context.</param>
        /// <param name="logger">The caller-owned diagnostics logger.</param>
        /// <returns>The product catalog service backed by the feature implementation.</returns>
        internal static AeDashboardProductCatalogService CreateProductCatalogService(ApplicationDbContext db, ILogger logger)
            => new(db, logger, CreateDataAccess());

        /**************************************************************/
        /// <summary>
        /// Creates a legacy product detail service.
        /// </summary>
        /// <param name="db">The caller-owned application database context.</param>
        /// <param name="logger">The caller-owned diagnostics logger.</param>
        /// <returns>The product detail service backed by the feature implementation.</returns>
        internal static AeDashboardProductDetailService CreateProductDetailService(ApplicationDbContext db, ILogger logger)
            => new(db, logger, CreateDataAccess());

        /**************************************************************/
        /// <summary>
        /// Creates a legacy favorite service.
        /// </summary>
        /// <param name="db">The caller-owned application database context.</param>
        /// <param name="logger">The caller-owned diagnostics logger.</param>
        /// <returns>The favorite service backed by the feature implementation.</returns>
        internal static AeDashboardFavoriteService CreateFavoriteService(ApplicationDbContext db, ILogger logger)
            => new(db, logger, CreateDataAccess());

        /**************************************************************/
        /// <summary>
        /// Creates a legacy class-correlation service.
        /// </summary>
        /// <param name="db">The caller-owned application database context.</param>
        /// <param name="logger">The caller-owned diagnostics logger.</param>
        /// <returns>The class-correlation service backed by the feature implementation.</returns>
        internal static AeDashboardClassCorrelationService CreateClassCorrelationService(ApplicationDbContext db, ILogger logger)
            => new(db, logger, CreateDataAccess());

        /**************************************************************/
        /// <summary>
        /// Creates a legacy system-correlation service.
        /// </summary>
        /// <param name="db">The caller-owned application database context.</param>
        /// <param name="logger">The caller-owned diagnostics logger.</param>
        /// <returns>The system-correlation service backed by the feature implementation.</returns>
        internal static AeDashboardSystemCorrelationService CreateSystemCorrelationService(ApplicationDbContext db, ILogger logger)
            => new(db, logger, CreateDataAccess());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Preserves the legacy static AE dashboard API while services own its implementation.
    /// </summary>
    /// <remarks>
    /// Every method in this partial is forwarding-only. EF queries, cache access,
    /// encryption, mapping, and derivation are owned by <see cref="AeDashboardDataAccess"/>
    /// through the matching injectable service.
    /// </remarks>
    /// <seealso cref="AeDashboardLegacyCompatibility"/>
    public static partial class DtoLabelAccess
    {
        #region AE dashboard compatibility forwarders

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-summary request to the injectable catalog service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductCatalogService"/>
        public static Task<List<AeDrugSummaryDto>> GetAeDrugSummariesAsync(ApplicationDbContext db, string pkSecret, ILogger logger, string? productSearch = null, long? userId = null, int? page = null, int? size = null)
            => AeDashboardLegacyCompatibility.CreateProductCatalogService(db, logger).GetDrugSummariesAsync(pkSecret, productSearch, userId, page, size);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-catalog request to the injectable catalog service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductCatalogService"/>
        public static Task<List<AeProductCatalogItemDto>> GetAeProductCatalogAsync(ApplicationDbContext db, string pkSecret, ILogger logger, string? productSearch = null, long? userId = null, int? page = null, int? size = null)
            => AeDashboardLegacyCompatibility.CreateProductCatalogService(db, logger).GetProductCatalogAsync(pkSecret, productSearch, userId, page, size);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-count request to the injectable catalog service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductCatalogService"/>
        public static Task<int> GetAeProductCountAsync(ApplicationDbContext db, ILogger logger)
            => AeDashboardLegacyCompatibility.CreateProductCatalogService(db, logger).GetProductCountAsync();

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-signal request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<List<AeRiskSignalDto>> GetAeRiskSignalsByDocumentAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, AeComparatorMix? comparator = null, bool includeFragile = true)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetRiskSignalsByDocumentAsync(documentGuid, pkSecret, comparator, includeFragile);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-detail request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeDashboardProductDetailData?> GetAeProductDetailDataAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, AeComparatorMix? comparator = null, bool includeFragile = true)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetProductDetailDataAsync(documentGuid, pkSecret, comparator, includeFragile);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy triage request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeTriageViewDto?> GetAeTriageViewAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, AeComparatorMix? comparator = null, bool includeFragile = true)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetTriageViewAsync(documentGuid, pkSecret, comparator, includeFragile);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy forest-plot request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeForestPlotDto?> GetAeForestPlotAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, AeComparatorMix? comparator = null, bool includeFragile = true)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetForestPlotAsync(documentGuid, pkSecret, comparator, includeFragile);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy quadrant request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeQuadrantViewDto?> GetAeQuadrantViewAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, AeComparatorMix? comparator = null, bool includeFragile = true)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetQuadrantViewAsync(documentGuid, pkSecret, comparator, includeFragile);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy reverse-lookup request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeReverseLookupResultDto> GetAeReverseLookupAsync(ApplicationDbContext db, string symptom, string pkSecret, ILogger logger, IEnumerable<Guid>? documentGuids = null)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetReverseLookupAsync(symptom, pkSecret, documentGuids);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy product-interchange request to the injectable detail service.
        /// </summary>
        /// <seealso cref="IAeDashboardProductDetailService"/>
        public static Task<AeInterchangeComparisonDto?> GetAeInterchangeAsync(ApplicationDbContext db, Guid documentGuidA, Guid documentGuidB, string pkSecret, ILogger logger, bool differencesOnly = false, bool sharedSignalsOnly = false, AeComparatorMix? comparator = null)
            => AeDashboardLegacyCompatibility.CreateProductDetailService(db, logger).GetInterchangeAsync(documentGuidA, documentGuidB, pkSecret, differencesOnly, sharedSignalsOnly, comparator);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy favorite-list request to the injectable favorite service.
        /// </summary>
        /// <seealso cref="IAeDashboardFavoriteService"/>
        public static Task<List<AeDrugSummaryDto>> GetAeFavoriteDrugSummariesAsync(ApplicationDbContext db, long userId, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => AeDashboardLegacyCompatibility.CreateFavoriteService(db, logger).GetFavoriteDrugSummariesAsync(userId, pkSecret, page, size);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy favorite write to the injectable favorite service.
        /// </summary>
        /// <seealso cref="IAeDashboardFavoriteService"/>
        public static Task<bool> SetAeProductFavoriteAsync(ApplicationDbContext db, long userId, Guid documentGuid, bool isFavorite, ILogger logger)
            => AeDashboardLegacyCompatibility.CreateFavoriteService(db, logger).SetProductFavoriteAsync(userId, documentGuid, isFavorite);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy class-correlation map request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardClassCorrelationService"/>
        public static Task<AeCorrelationMapDto?> GetAeCorrelationMapAsync(ApplicationDbContext db, string pharmClassCode, string pkSecret, ILogger logger, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, int minDrugsPerCell = 4, AeCorrelationMethod method = AeCorrelationMethod.Spearman, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool seriousSocOnly = false, bool excludeCombos = false, int minEvents = 0)
            => AeDashboardLegacyCompatibility.CreateClassCorrelationService(db, logger).GetCorrelationMapAsync(pharmClassCode, pkSecret, comparator, includeNonSignificant, excludeFragile, minDrugsPerCell, method, aggregation, seriousSocOnly, excludeCombos, minEvents);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy class-picker request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardClassCorrelationService"/>
        public static Task<AeCorrelationClassPickerPage> GetAeCorrelationClassesAsync(ApplicationDbContext db, string pkSecret, ILogger logger, string? classSearch = null, int? page = null, int? size = null, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, bool excludeCombos = false, int minEvents = 0, int minDrugsPerCell = 4, bool seriousSocOnly = false)
            => AeDashboardLegacyCompatibility.CreateClassCorrelationService(db, logger).GetCorrelationClassesAsync(pkSecret, classSearch, page, size, comparator, includeNonSignificant, excludeFragile, excludeCombos, minEvents, minDrugsPerCell, seriousSocOnly);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy class-heatmap request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardClassCorrelationService"/>
        public static Task<AeCorrelationHeatmapDto?> GetAeCorrelationHeatmapAsync(ApplicationDbContext db, string pharmClassCode, string pkSecret, ILogger logger, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool seriousSocOnly = false, bool excludeCombos = false, int minEvents = 0)
            => AeDashboardLegacyCompatibility.CreateClassCorrelationService(db, logger).GetCorrelationHeatmapAsync(pharmClassCode, pkSecret, comparator, includeNonSignificant, excludeFragile, aggregation, seriousSocOnly, excludeCombos, minEvents);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy class-cell-detail request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardClassCorrelationService"/>
        public static Task<AeCorrelationCellDetailDto?> GetAeCorrelationCellDetailAsync(ApplicationDbContext db, string pharmClassCode, string socX, string socY, string pkSecret, ILogger logger, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, int minDrugsPerCell = 4, AeCorrelationMethod method = AeCorrelationMethod.Spearman, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool seriousSocOnly = false, bool excludeCombos = false, int minEvents = 0)
            => AeDashboardLegacyCompatibility.CreateClassCorrelationService(db, logger).GetCorrelationCellDetailAsync(pharmClassCode, socX, socY, pkSecret, comparator, includeNonSignificant, excludeFragile, minDrugsPerCell, method, aggregation, seriousSocOnly, excludeCombos, minEvents);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy system-picker request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardSystemCorrelationService"/>
        public static Task<AeSystemPickerPage> GetAeCorrelationSystemsAsync(ApplicationDbContext db, string pkSecret, ILogger logger, string? systemSearch = null, int? page = null, int? size = null, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, bool excludeCombos = false, int minEvents = 0, int minTermsPerCell = 4)
            => AeDashboardLegacyCompatibility.CreateSystemCorrelationService(db, logger).GetCorrelationSystemsAsync(pkSecret, systemSearch, page, size, comparator, includeNonSignificant, excludeFragile, excludeCombos, minEvents, minTermsPerCell);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy system-class map request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardSystemCorrelationService"/>
        public static Task<AeSystemClassCorrelationMapDto?> GetAeSystemCorrelationMapAsync(ApplicationDbContext db, IEnumerable<string> systems, string pkSecret, ILogger logger, string? classSearch = null, int classPageNumber = 1, int classPageSize = 20, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, int minTermsPerCell = 4, AeCorrelationMethod method = AeCorrelationMethod.Spearman, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool excludeCombos = false, int minEvents = 0, bool includeFullMatrix = false, string? classType = null)
            => AeDashboardLegacyCompatibility.CreateSystemCorrelationService(db, logger).GetSystemCorrelationMapAsync(systems, pkSecret, classSearch, classPageNumber, classPageSize, comparator, includeNonSignificant, excludeFragile, minTermsPerCell, method, aggregation, excludeCombos, minEvents, includeFullMatrix, classType);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy system-class heatmap request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardSystemCorrelationService"/>
        public static Task<AeSystemClassHeatmapDto?> GetAeSystemCorrelationHeatmapAsync(ApplicationDbContext db, IEnumerable<string> systems, string pkSecret, ILogger logger, string? classSearch = null, string? drugSearch = null, int classPageNumber = 1, int classPageSize = 40, int drugPageNumber = 1, int drugPageSize = 50, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool excludeCombos = false, int minEvents = 0, string? classType = null)
            => AeDashboardLegacyCompatibility.CreateSystemCorrelationService(db, logger).GetSystemCorrelationHeatmapAsync(systems, pkSecret, classSearch, drugSearch, classPageNumber, classPageSize, drugPageNumber, drugPageSize, comparator, includeNonSignificant, excludeFragile, aggregation, excludeCombos, minEvents, classType);

        /**************************************************************/
        /// <summary>
        /// Forwards a legacy system-class cell-detail request to its injectable service.
        /// </summary>
        /// <seealso cref="IAeDashboardSystemCorrelationService"/>
        public static Task<AeSystemClassCorrelationCellDetailDto?> GetAeSystemCorrelationCellDetailAsync(ApplicationDbContext db, IEnumerable<string> systems, string classX, string classY, string pkSecret, ILogger logger, AeComparatorMix comparator = AeComparatorMix.Placebo, bool includeNonSignificant = true, bool excludeFragile = true, int minTermsPerCell = 4, AeCorrelationMethod method = AeCorrelationMethod.Spearman, AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr, bool excludeCombos = false, int minEvents = 0, int pageNumber = 1, int pageSize = 100)
            => AeDashboardLegacyCompatibility.CreateSystemCorrelationService(db, logger).GetSystemCorrelationCellDetailAsync(systems, classX, classY, pkSecret, comparator, includeNonSignificant, excludeFragile, minTermsPerCell, method, aggregation, excludeCombos, minEvents, pageNumber, pageSize);

        #endregion
    }
}
