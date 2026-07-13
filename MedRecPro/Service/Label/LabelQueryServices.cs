using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.Service.LabelQuery.Implementation;

namespace MedRecPro.Service.LabelQuery
{
    /**************************************************************/
    /// <summary>
    /// Holds the scoped dependencies shared by the narrow label query services.
    /// </summary>
    /// <remarks>
    /// This base class owns dependency wiring only. Query, cache, mapping, and graph behavior remains in
    /// <see cref="LabelQueryDataAccess"/>, allowing the service contracts to stay cohesive.
    /// </remarks>
    /// <seealso cref="LabelQueryDataAccess"/>
    internal abstract class LabelQueryServiceBase
    {
        #region implementation

        /// <summary>Gets the request-scoped database context.</summary>
        protected ApplicationDbContext DbContext { get; }

        /// <summary>Gets the configured primary-key encryption secret.</summary>
        protected string PkSecret { get; }

        /// <summary>Gets the feature-service logger.</summary>
        protected ILogger Logger { get; }

        /// <summary>Gets the relocated label query implementation.</summary>
        protected LabelQueryDataAccess Query { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes scoped dependencies for a label query service.
        /// </summary>
        /// <param name="dbContext">The request-scoped application database context.</param>
        /// <param name="configuration">The configuration source containing the encryption secret.</param>
        /// <param name="query">The relocated query implementation.</param>
        /// <param name="logger">The feature-service logger.</param>
        /// <exception cref="InvalidOperationException">Thrown when the encryption secret is absent.</exception>
        /// <seealso cref="ApplicationDbContext"/>
        protected LabelQueryServiceBase(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger logger)
        {
            #region implementation

            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            ArgumentNullException.ThrowIfNull(configuration);
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PkSecret = configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Implements ingredient query operations through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="IIngredientSearchService"/>
    internal sealed class IngredientSearchService : LabelQueryServiceBase, IIngredientSearchService
    {
        /**************************************************************/
        /// <summary>Initializes the ingredient query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public IngredientSearchService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<IngredientSearchService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<IngredientActiveSummaryDto>> GetIngredientActiveSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null)
            => Query.GetIngredientActiveSummariesAsync(DbContext, minProductCount, ingredient, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<IngredientInactiveSummaryDto>> GetIngredientInactiveSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null)
            => Query.GetIngredientInactiveSummariesAsync(DbContext, minProductCount, ingredient, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByIngredientDto>> SearchByIngredientAsync(string? unii, string? substanceNameSearch, int? page = null, int? size = null)
            => Query.SearchByIngredientAsync(DbContext, unii, substanceNameSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<IngredientSummaryDto>> GetIngredientSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null)
            => Query.GetIngredientSummariesAsync(DbContext, minProductCount, ingredient, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<IngredientViewDto>> SearchIngredientsAdvancedAsync(string? unii, string? substanceNameSearch, string? applicationNumber, string? applicationType, string? productNameSearch, bool? activeOnly, int? page = null, int? size = null)
            => Query.SearchIngredientsAdvancedAsync(DbContext, unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch, activeOnly, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<IngredientViewDto>> FindProductsByApplicationNumberWithSameIngredientAsync(string applicationNumber, int? page = null, int? size = null)
            => Query.FindProductsByApplicationNumberWithSameIngredientAsync(DbContext, applicationNumber, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<IngredientRelatedResultsDto> FindRelatedIngredientsAsync(string? unii, string? substanceNameSearch, bool isSearchingActive, int maxProducts = 50)
            => Query.FindRelatedIngredientsAsync(DbContext, unii, substanceNameSearch, isSearchingActive, PkSecret, Logger, maxProducts);
    }

    /**************************************************************/
    /// <summary>
    /// Implements pharmacologic-class query operations through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="IPharmacologicClassSearchService"/>
    internal sealed class PharmacologicClassSearchService : LabelQueryServiceBase, IPharmacologicClassSearchService
    {
        /**************************************************************/
        /// <summary>Initializes the pharmacologic-class query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public PharmacologicClassSearchService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<PharmacologicClassSearchService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassAsync(string classNameSearch, int? page = null, int? size = null)
            => Query.SearchByPharmacologicClassAsync(DbContext, classNameSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassExactAsync(string classNameSearch, int? page = null, int? size = null)
            => Query.SearchByPharmacologicClassExactAsync(DbContext, classNameSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<PharmacologicClassHierarchyViewDto>> GetPharmacologicClassHierarchyAsync(int? page = null, int? size = null)
            => Query.GetPharmacologicClassHierarchyAsync(DbContext, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<PharmacologicClassSummaryDto>> GetPharmacologicClassSummariesAsync(int? page = null, int? size = null)
            => Query.GetPharmacologicClassSummariesAsync(DbContext, PkSecret, Logger, page, size);
    }

    /**************************************************************/
    /// <summary>
    /// Implements product and identifier query operations through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="IProductSearchService"/>
    internal sealed class ProductSearchService : LabelQueryServiceBase, IProductSearchService
    {
        /**************************************************************/
        /// <summary>Initializes the product query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public ProductSearchService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<ProductSearchService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByApplicationNumberDto>> SearchByApplicationNumberAsync(string applicationNumber, int? page = null, int? size = null)
            => Query.SearchByApplicationNumberAsync(DbContext, applicationNumber, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ApplicationNumberSummaryDto>> GetApplicationNumberSummariesAsync(string? marketingCategory, int? page = null, int? size = null)
            => Query.GetApplicationNumberSummariesAsync(DbContext, marketingCategory, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByNDCDto>> SearchByNDCAsync(string productCode, int? page = null, int? size = null)
            => Query.SearchByNDCAsync(DbContext, productCode, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<PackageByNDCDto>> SearchByPackageNDCAsync(string packageCode, int? page = null, int? size = null)
            => Query.SearchByPackageNDCAsync(DbContext, packageCode, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductsByLabelerDto>> SearchByLabelerAsync(string labelerNameSearch, int? page = null, int? size = null)
            => Query.SearchByLabelerAsync(DbContext, labelerNameSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<LabelerSummaryDto>> GetLabelerSummariesAsync(int? page = null, int? size = null)
            => Query.GetLabelerSummariesAsync(DbContext, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductLatestLabelDto>> GetProductLatestLabelsAsync(string? unii, string? productNameSearch, string? activeIngredientSearch, int? page = null, int? size = null)
            => Query.GetProductLatestLabelsAsync(DbContext, unii, productNameSearch, activeIngredientSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductIndicationsDto>> GetProductIndicationsAsync(string? unii, string? productNameSearch, string? substanceNameSearch, string? indicationSearch, int? page = null, int? size = null)
            => Query.GetProductIndicationsAsync(DbContext, unii, productNameSearch, substanceNameSearch, indicationSearch, PkSecret, Logger, page, size);
    }

    /**************************************************************/
    /// <summary>
    /// Implements label content query operations through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="ILabelContentQueryService"/>
    internal sealed class LabelContentQueryService : LabelQueryServiceBase, ILabelContentQueryService
    {
        /**************************************************************/
        /// <summary>Initializes the label content query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public LabelContentQueryService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<LabelContentQueryService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<SectionNavigationDto>> SearchBySectionCodeAsync(string sectionCode, int? page = null, int? size = null)
            => Query.SearchBySectionCodeAsync(DbContext, sectionCode, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<SectionTypeSummaryDto>> GetSectionTypeSummariesAsync(int? page = null, int? size = null)
            => Query.GetSectionTypeSummariesAsync(DbContext, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<SectionContentDto>> GetSectionContentAsync(Guid documentGuid, Guid? sectionGuid, string? sectionCode, int? page = null, int? size = null)
            => Query.GetSectionContentAsync(DbContext, documentGuid, sectionGuid, sectionCode, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DrugInteractionLookupDto>> GetDrugInteractionsAsync(IEnumerable<string> ingredientUNIIs, int? page = null, int? size = null)
            => Query.GetDrugInteractionsAsync(DbContext, ingredientUNIIs, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DEAScheduleLookupDto>> GetDEAScheduleProductsAsync(string? scheduleCode, int? page = null, int? size = null)
            => Query.GetDEAScheduleProductsAsync(DbContext, scheduleCode, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<ProductSummaryViewDto>> SearchProductSummaryAsync(string productNameSearch, int? page = null, int? size = null)
            => Query.SearchProductSummaryAsync(DbContext, productNameSearch, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<RelatedProductsDto>> GetRelatedProductsAsync(int? sourceProductId, Guid? sourceDocumentGuid, string? relationshipType, int? page = null, int? size = null)
            => Query.GetRelatedProductsAsync(DbContext, sourceProductId, sourceDocumentGuid, relationshipType, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<APIEndpointGuideDto>> GetAPIEndpointGuideAsync(string? category)
            => Query.GetAPIEndpointGuideAsync(DbContext, category, PkSecret, Logger);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<InventorySummaryDto>> GetInventorySummaryAsync(string? category)
            => Query.GetInventorySummaryAsync(DbContext, category, Logger);
    }

    /**************************************************************/
    /// <summary>
    /// Implements markdown query operations through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="ILabelMarkdownService"/>
    internal sealed class LabelMarkdownService : LabelQueryServiceBase, ILabelMarkdownService
    {
        /**************************************************************/
        /// <summary>Initializes the markdown query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public LabelMarkdownService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<LabelMarkdownService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<LabelSectionMarkdownDto>> GetLabelSectionMarkdownAsync(Guid documentGuid, string? sectionCode = null)
            => Query.GetLabelSectionMarkdownAsync(DbContext, documentGuid, PkSecret, Logger, sectionCode);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<LabelMarkdownExportDto> GenerateLabelMarkdownAsync(Guid documentGuid)
            => Query.GenerateLabelMarkdownAsync(DbContext, documentGuid, PkSecret, Logger);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<string> GenerateCleanLabelMarkdownAsync(Guid documentGuid, IClaudeApiService claudeApiService)
            => Query.GenerateCleanLabelMarkdownAsync(DbContext, documentGuid, claudeApiService, PkSecret, Logger);
    }

    /**************************************************************/
    /// <summary>
    /// Implements full document graph and navigation queries through the relocated label implementation.
    /// </summary>
    /// <seealso cref="ILabelDocumentQueryService"/>
    internal sealed class LabelDocumentQueryService : LabelQueryServiceBase, ILabelDocumentQueryService
    {
        /**************************************************************/
        /// <summary>Initializes the document query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public LabelDocumentQueryService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<LabelDocumentQueryService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentDto>> BuildDocumentsAsync(int? page = null, int? size = null, bool? useBatchLoading = null)
            => Query.BuildDocumentsAsync(DbContext, PkSecret, Logger, page, size, useBatchLoading);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentDto>> BuildDocumentsAsync(Guid documentGuid, bool? useBatchLoading = null)
            => Query.BuildDocumentsAsync(DbContext, documentGuid, PkSecret, Logger, useBatchLoading);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<PackageIdentifierDto?> GetPackageIdentifierAsync(int? packagingLevelID)
            => Query.GetPackageIdentifierAsync(DbContext, packagingLevelID, PkSecret, Logger);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentNavigationDto>> GetDocumentNavigationAsync(bool latestOnly, Guid? setGuid, int? page = null, int? size = null)
            => Query.GetDocumentNavigationAsync(DbContext, latestOnly, setGuid, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentVersionHistoryDto>> GetDocumentVersionHistoryAsync(Guid setGuidOrDocumentGuid)
            => Query.GetDocumentVersionHistoryAsync(DbContext, setGuidOrDocumentGuid, PkSecret, Logger);
    }

    /**************************************************************/
    /// <summary>
    /// Implements Orange Book patent queries through the relocated label query implementation.
    /// </summary>
    /// <seealso cref="IOrangeBookPatentQueryService"/>
    internal sealed class OrangeBookPatentQueryService : LabelQueryServiceBase, IOrangeBookPatentQueryService
    {
        /**************************************************************/
        /// <summary>Initializes the Orange Book patent query service.</summary>
        /// <seealso cref="LabelQueryServiceBase"/>
        public OrangeBookPatentQueryService(ApplicationDbContext dbContext, IConfiguration configuration, LabelQueryDataAccess query, ILogger<OrangeBookPatentQueryService> logger)
            : base(dbContext, configuration, query, logger) { }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<OrangeBookPatentDto>> SearchOrangeBookPatentsAsync(int? expiringInMonths, Guid? documentGuid, string? applicationNumber, string? ingredient, string? tradeName, string? patentNo, DateOnly? patentExpireDate, bool? hasPediatricFlag, bool? hasWithdrawnCommercialReasonFlag, int? page = null, int? size = null)
            => Query.SearchOrangeBookPatentsAsync(DbContext, expiringInMonths, documentGuid, applicationNumber, ingredient, tradeName, patentNo, patentExpireDate, hasPediatricFlag, hasWithdrawnCommercialReasonFlag, PkSecret, Logger, page, size);

        /**************************************************************/
        /// <inheritdoc/>
        public Task<int> CountExpiringPatentsAsync(int? expiringInMonths, int maxExpirationMonths, string? tradeName, string? ingredient)
            => Query.CountExpiringPatentsAsync(DbContext, expiringInMonths, maxExpirationMonths, tradeName, ingredient);
    }
}
