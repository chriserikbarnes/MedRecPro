using MedRecPro.Models;

namespace MedRecPro.Service.LabelQuery
{
    /**************************************************************/
    /// <summary>
    /// Retrieves ingredient-focused label search results.
    /// </summary>
    /// <seealso cref="IngredientSearchService"/>
    public interface IIngredientSearchService
    {
        #region implementation

        /// <summary>Gets active ingredient summaries.</summary>
        Task<List<IngredientActiveSummaryDto>> GetIngredientActiveSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null);

        /// <summary>Gets inactive ingredient summaries.</summary>
        Task<List<IngredientInactiveSummaryDto>> GetIngredientInactiveSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null);

        /// <summary>Searches products by ingredient identifier or substance name.</summary>
        Task<List<ProductsByIngredientDto>> SearchByIngredientAsync(string? unii, string? substanceNameSearch, int? page = null, int? size = null);

        /// <summary>Gets ingredient summaries.</summary>
        Task<List<IngredientSummaryDto>> GetIngredientSummariesAsync(int? minProductCount, string? ingredient, int? page = null, int? size = null);

        /// <summary>Searches ingredients with combined filters.</summary>
        Task<List<IngredientViewDto>> SearchIngredientsAdvancedAsync(string? unii, string? substanceNameSearch, string? applicationNumber, string? applicationType, string? productNameSearch, bool? activeOnly, int? page = null, int? size = null);

        /// <summary>Finds application-related products with the same ingredient.</summary>
        Task<List<IngredientViewDto>> FindProductsByApplicationNumberWithSameIngredientAsync(string applicationNumber, int? page = null, int? size = null);

        /// <summary>Finds ingredients related to a product search.</summary>
        Task<IngredientRelatedResultsDto> FindRelatedIngredientsAsync(string? unii, string? substanceNameSearch, bool isSearchingActive, int maxProducts = 50);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves pharmacologic-class search and navigation results.
    /// </summary>
    /// <seealso cref="PharmacologicClassSearchService"/>
    public interface IPharmacologicClassSearchService
    {
        #region implementation

        /// <summary>Searches products by pharmacologic class.</summary>
        Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassAsync(string classNameSearch, int? page = null, int? size = null);

        /// <summary>Searches products by an exact pharmacologic class.</summary>
        Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassExactAsync(string classNameSearch, int? page = null, int? size = null);

        /// <summary>Gets the pharmacologic class hierarchy.</summary>
        Task<List<PharmacologicClassHierarchyViewDto>> GetPharmacologicClassHierarchyAsync(int? page = null, int? size = null);

        /// <summary>Gets pharmacologic class summaries.</summary>
        Task<List<PharmacologicClassSummaryDto>> GetPharmacologicClassSummariesAsync(int? page = null, int? size = null);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves product, identifier, labeler, and indication search results.
    /// </summary>
    /// <seealso cref="ProductSearchService"/>
    public interface IProductSearchService
    {
        #region implementation

        /// <summary>Searches products by application number.</summary>
        Task<List<ProductsByApplicationNumberDto>> SearchByApplicationNumberAsync(string applicationNumber, int? page = null, int? size = null);

        /// <summary>Gets application-number summaries.</summary>
        Task<List<ApplicationNumberSummaryDto>> GetApplicationNumberSummariesAsync(string? marketingCategory, int? page = null, int? size = null);

        /// <summary>Searches products by NDC.</summary>
        Task<List<ProductsByNDCDto>> SearchByNDCAsync(string productCode, int? page = null, int? size = null);

        /// <summary>Searches package identifiers by NDC.</summary>
        Task<List<PackageByNDCDto>> SearchByPackageNDCAsync(string packageCode, int? page = null, int? size = null);

        /// <summary>Searches products by labeler name.</summary>
        Task<List<ProductsByLabelerDto>> SearchByLabelerAsync(string labelerNameSearch, int? page = null, int? size = null);

        /// <summary>Gets labeler summaries.</summary>
        Task<List<LabelerSummaryDto>> GetLabelerSummariesAsync(int? page = null, int? size = null);

        /// <summary>Gets the latest label for matching products.</summary>
        Task<List<ProductLatestLabelDto>> GetProductLatestLabelsAsync(string? unii, string? productNameSearch, string? activeIngredientSearch, int? page = null, int? size = null);

        /// <summary>Gets product indications.</summary>
        Task<List<ProductIndicationsDto>> GetProductIndicationsAsync(string? unii, string? productNameSearch, string? substanceNameSearch, string? indicationSearch, int? page = null, int? size = null);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves label section, relationship, lookup, guide, and inventory content.
    /// </summary>
    /// <seealso cref="LabelContentQueryService"/>
    public interface ILabelContentQueryService
    {
        #region implementation

        /// <summary>Searches document navigation by section code.</summary>
        Task<List<SectionNavigationDto>> SearchBySectionCodeAsync(string sectionCode, int? page = null, int? size = null);

        /// <summary>Gets section type summaries.</summary>
        Task<List<SectionTypeSummaryDto>> GetSectionTypeSummariesAsync(int? page = null, int? size = null);

        /// <summary>Gets document section content.</summary>
        Task<List<SectionContentDto>> GetSectionContentAsync(Guid documentGuid, Guid? sectionGuid, string? sectionCode, int? page = null, int? size = null);

        /// <summary>Gets drug interaction lookups.</summary>
        Task<List<DrugInteractionLookupDto>> GetDrugInteractionsAsync(IEnumerable<string> ingredientUNIIs, int? page = null, int? size = null);

        /// <summary>Gets DEA schedule products.</summary>
        Task<List<DEAScheduleLookupDto>> GetDEAScheduleProductsAsync(string? scheduleCode, int? page = null, int? size = null);

        /// <summary>Searches product summary views.</summary>
        Task<List<ProductSummaryViewDto>> SearchProductSummaryAsync(string productNameSearch, int? page = null, int? size = null);

        /// <summary>Gets related products.</summary>
        Task<List<RelatedProductsDto>> GetRelatedProductsAsync(int? sourceProductId, Guid? sourceDocumentGuid, string? relationshipType, int? page = null, int? size = null);

        /// <summary>Gets API endpoint guide content.</summary>
        Task<List<APIEndpointGuideDto>> GetAPIEndpointGuideAsync(string? category);

        /// <summary>Gets inventory summaries.</summary>
        Task<List<InventorySummaryDto>> GetInventorySummaryAsync(string? category);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves and generates markdown representations of label content.
    /// </summary>
    /// <seealso cref="LabelMarkdownService"/>
    public interface ILabelMarkdownService
    {
        #region implementation

        /// <summary>Gets section markdown for one label document.</summary>
        Task<List<LabelSectionMarkdownDto>> GetLabelSectionMarkdownAsync(Guid documentGuid, string? sectionCode = null);

        /// <summary>Generates a complete markdown export.</summary>
        Task<LabelMarkdownExportDto> GenerateLabelMarkdownAsync(Guid documentGuid);

        /// <summary>Generates cleaned markdown using the supplied Claude client.</summary>
        Task<string> GenerateCleanLabelMarkdownAsync(Guid documentGuid, IClaudeApiService claudeApiService);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves complete label document graphs and document navigation metadata.
    /// </summary>
    /// <seealso cref="LabelDocumentQueryService"/>
    public interface ILabelDocumentQueryService
    {
        #region implementation

        /// <summary>Builds paged complete document graphs.</summary>
        Task<List<DocumentDto>> BuildDocumentsAsync(int? page = null, int? size = null, bool? useBatchLoading = null);

        /// <summary>Builds one complete document graph.</summary>
        Task<List<DocumentDto>> BuildDocumentsAsync(Guid documentGuid, bool? useBatchLoading = null);

        /// <summary>Gets a package identifier.</summary>
        Task<PackageIdentifierDto?> GetPackageIdentifierAsync(int? packagingLevelID);

        /// <summary>Gets document navigation rows.</summary>
        Task<List<DocumentNavigationDto>> GetDocumentNavigationAsync(bool latestOnly, Guid? setGuid, int? page = null, int? size = null);

        /// <summary>Gets document version history.</summary>
        Task<List<DocumentVersionHistoryDto>> GetDocumentVersionHistoryAsync(Guid setGuidOrDocumentGuid);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves Orange Book patent discovery and expiration-count data.
    /// </summary>
    /// <seealso cref="OrangeBookPatentQueryService"/>
    public interface IOrangeBookPatentQueryService
    {
        #region implementation

        /// <summary>Searches Orange Book patents with the supplied filters.</summary>
        Task<List<OrangeBookPatentDto>> SearchOrangeBookPatentsAsync(int? expiringInMonths, Guid? documentGuid, string? applicationNumber, string? ingredient, string? tradeName, string? patentNo, DateOnly? patentExpireDate, bool? hasPediatricFlag, bool? hasWithdrawnCommercialReasonFlag, int? page = null, int? size = null);

        /// <summary>Counts expiring Orange Book patents.</summary>
        Task<int> CountExpiringPatentsAsync(int? expiringInMonths, int maxExpirationMonths, string? tradeName, string? ingredient);

        #endregion
    }
}
