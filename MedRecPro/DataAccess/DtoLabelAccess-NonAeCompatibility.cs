using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.Service.LabelQuery.Implementation;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Provides the legacy non-AE static DtoLabelAccess compatibility surface.
    /// </summary>
    /// <remarks>
    /// Each member forwards unchanged inputs to the relocated Label query implementation. Querying,
    /// caching, encryption, DTO construction, and graph assembly intentionally live outside this facade.
    /// </remarks>
    /// <seealso cref="LabelQueryDataAccess"/>
    public static partial class DtoLabelAccess
    {
        #region legacy non-AE compatibility

        /**************************************************************/
        /// <summary>Forwards full document graph retrieval to the label document implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess"/>
        public static Task<List<DocumentDto>> BuildDocumentsAsync(ApplicationDbContext db, string pkSecret, ILogger logger, int? page = null, int? size = null, bool? useBatchLoading = null)
            => LabelQueryLegacyCompatibility.Create().BuildDocumentsAsync(db, pkSecret, logger, page, size, useBatchLoading);

        /**************************************************************/
        /// <summary>Forwards single-document graph retrieval to the label document implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess"/>
        public static Task<List<DocumentDto>> BuildDocumentsAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, bool? useBatchLoading = null)
            => LabelQueryLegacyCompatibility.Create().BuildDocumentsAsync(db, documentGuid, pkSecret, logger, useBatchLoading);

        /**************************************************************/
        /// <summary>Forwards package identifier retrieval to the label document implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetPackageIdentifierAsync"/>
        public static Task<PackageIdentifierDto?> GetPackageIdentifierAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GetPackageIdentifierAsync(db, packagingLevelID, pkSecret, logger);

        /**************************************************************/
        /// <summary>Forwards application-number product search to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByApplicationNumberAsync"/>
        public static Task<List<ProductsByApplicationNumberDto>> SearchByApplicationNumberAsync(ApplicationDbContext db, string applicationNumber, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByApplicationNumberAsync(db, applicationNumber, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards application-number summary retrieval to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetApplicationNumberSummariesAsync"/>
        public static Task<List<ApplicationNumberSummaryDto>> GetApplicationNumberSummariesAsync(ApplicationDbContext db, string? marketingCategory, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetApplicationNumberSummariesAsync(db, marketingCategory, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards pharmacologic-class search to the classification query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByPharmacologicClassAsync"/>
        public static Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassAsync(ApplicationDbContext db, string classNameSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByPharmacologicClassAsync(db, classNameSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards exact pharmacologic-class search to the classification query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByPharmacologicClassExactAsync"/>
        public static Task<List<ProductsByPharmacologicClassDto>> SearchByPharmacologicClassExactAsync(ApplicationDbContext db, string classNameSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByPharmacologicClassExactAsync(db, classNameSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards pharmacologic-class hierarchy retrieval to the classification query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetPharmacologicClassHierarchyAsync"/>
        public static Task<List<PharmacologicClassHierarchyViewDto>> GetPharmacologicClassHierarchyAsync(ApplicationDbContext db, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetPharmacologicClassHierarchyAsync(db, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards pharmacologic-class summary retrieval to the classification query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetPharmacologicClassSummariesAsync"/>
        public static Task<List<PharmacologicClassSummaryDto>> GetPharmacologicClassSummariesAsync(ApplicationDbContext db, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetPharmacologicClassSummariesAsync(db, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards active ingredient summary retrieval to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetIngredientActiveSummariesAsync"/>
        public static Task<List<IngredientActiveSummaryDto>> GetIngredientActiveSummariesAsync(ApplicationDbContext db, int? minProductCount, string? ingredient, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetIngredientActiveSummariesAsync(db, minProductCount, ingredient, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards inactive ingredient summary retrieval to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetIngredientInactiveSummariesAsync"/>
        public static Task<List<IngredientInactiveSummaryDto>> GetIngredientInactiveSummariesAsync(ApplicationDbContext db, int? minProductCount, string? ingredient, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetIngredientInactiveSummariesAsync(db, minProductCount, ingredient, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards ingredient product search to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByIngredientAsync"/>
        public static Task<List<ProductsByIngredientDto>> SearchByIngredientAsync(ApplicationDbContext db, string? unii, string? substanceNameSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByIngredientAsync(db, unii, substanceNameSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards ingredient summary retrieval to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetIngredientSummariesAsync"/>
        public static Task<List<IngredientSummaryDto>> GetIngredientSummariesAsync(ApplicationDbContext db, int? minProductCount, string? ingredient, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetIngredientSummariesAsync(db, minProductCount, ingredient, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards advanced ingredient search to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchIngredientsAdvancedAsync"/>
        public static Task<List<IngredientViewDto>> SearchIngredientsAdvancedAsync(ApplicationDbContext db, string? unii, string? substanceNameSearch, string? applicationNumber, string? applicationType, string? productNameSearch, bool? activeOnly, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchIngredientsAdvancedAsync(db, unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch, activeOnly, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards related-application ingredient retrieval to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        public static Task<List<IngredientViewDto>> FindProductsByApplicationNumberWithSameIngredientAsync(ApplicationDbContext db, string applicationNumber, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().FindProductsByApplicationNumberWithSameIngredientAsync(db, applicationNumber, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards related-ingredient retrieval to the ingredient query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.FindRelatedIngredientsAsync"/>
        public static Task<IngredientRelatedResultsDto> FindRelatedIngredientsAsync(ApplicationDbContext db, string? unii, string? substanceNameSearch, bool isSearchingActive, string pkSecret, ILogger logger, int maxProducts = 50)
            => LabelQueryLegacyCompatibility.Create().FindRelatedIngredientsAsync(db, unii, substanceNameSearch, isSearchingActive, pkSecret, logger, maxProducts);

        /**************************************************************/
        /// <summary>Forwards NDC product search to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByNDCAsync"/>
        public static Task<List<ProductsByNDCDto>> SearchByNDCAsync(ApplicationDbContext db, string productCode, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByNDCAsync(db, productCode, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards package NDC search to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByPackageNDCAsync"/>
        public static Task<List<PackageByNDCDto>> SearchByPackageNDCAsync(ApplicationDbContext db, string packageCode, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByPackageNDCAsync(db, packageCode, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards labeler product search to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchByLabelerAsync"/>
        public static Task<List<ProductsByLabelerDto>> SearchByLabelerAsync(ApplicationDbContext db, string labelerNameSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchByLabelerAsync(db, labelerNameSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards labeler summary retrieval to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetLabelerSummariesAsync"/>
        public static Task<List<LabelerSummaryDto>> GetLabelerSummariesAsync(ApplicationDbContext db, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetLabelerSummariesAsync(db, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards document navigation retrieval to the label document implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetDocumentNavigationAsync"/>
        public static Task<List<DocumentNavigationDto>> GetDocumentNavigationAsync(ApplicationDbContext db, bool latestOnly, Guid? setGuid, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetDocumentNavigationAsync(db, latestOnly, setGuid, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards document version-history retrieval to the label document implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetDocumentVersionHistoryAsync"/>
        public static Task<List<DocumentVersionHistoryDto>> GetDocumentVersionHistoryAsync(ApplicationDbContext db, Guid setGuidOrDocumentGuid, string pkSecret, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GetDocumentVersionHistoryAsync(db, setGuidOrDocumentGuid, pkSecret, logger);

        /**************************************************************/
        /// <summary>Forwards section-code navigation search to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchBySectionCodeAsync"/>
        public static Task<List<SectionNavigationDto>> SearchBySectionCodeAsync(ApplicationDbContext db, string sectionCode, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchBySectionCodeAsync(db, sectionCode, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards section-type summary retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetSectionTypeSummariesAsync"/>
        public static Task<List<SectionTypeSummaryDto>> GetSectionTypeSummariesAsync(ApplicationDbContext db, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetSectionTypeSummariesAsync(db, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards section content retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetSectionContentAsync"/>
        public static Task<List<SectionContentDto>> GetSectionContentAsync(ApplicationDbContext db, Guid documentGuid, Guid? sectionGuid, string? sectionCode, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetSectionContentAsync(db, documentGuid, sectionGuid, sectionCode, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards drug interaction retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetDrugInteractionsAsync"/>
        public static Task<List<DrugInteractionLookupDto>> GetDrugInteractionsAsync(ApplicationDbContext db, IEnumerable<string> ingredientUNIIs, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetDrugInteractionsAsync(db, ingredientUNIIs, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards DEA schedule product retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetDEAScheduleProductsAsync"/>
        public static Task<List<DEAScheduleLookupDto>> GetDEAScheduleProductsAsync(ApplicationDbContext db, string? scheduleCode, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetDEAScheduleProductsAsync(db, scheduleCode, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards product summary search to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchProductSummaryAsync"/>
        public static Task<List<ProductSummaryViewDto>> SearchProductSummaryAsync(ApplicationDbContext db, string productNameSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchProductSummaryAsync(db, productNameSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards related-product retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetRelatedProductsAsync"/>
        public static Task<List<RelatedProductsDto>> GetRelatedProductsAsync(ApplicationDbContext db, int? sourceProductId, Guid? sourceDocumentGuid, string? relationshipType, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetRelatedProductsAsync(db, sourceProductId, sourceDocumentGuid, relationshipType, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards API endpoint-guide retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetAPIEndpointGuideAsync"/>
        public static Task<List<APIEndpointGuideDto>> GetAPIEndpointGuideAsync(ApplicationDbContext db, string? category, string pkSecret, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GetAPIEndpointGuideAsync(db, category, pkSecret, logger);

        /**************************************************************/
        /// <summary>Forwards inventory-summary retrieval to the content query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetInventorySummaryAsync"/>
        public static Task<List<InventorySummaryDto>> GetInventorySummaryAsync(ApplicationDbContext db, string? category, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GetInventorySummaryAsync(db, category, logger);

        /**************************************************************/
        /// <summary>Forwards latest product-label retrieval to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetProductLatestLabelsAsync"/>
        public static Task<List<ProductLatestLabelDto>> GetProductLatestLabelsAsync(ApplicationDbContext db, string? unii, string? productNameSearch, string? activeIngredientSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetProductLatestLabelsAsync(db, unii, productNameSearch, activeIngredientSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards product-indication retrieval to the product query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetProductIndicationsAsync"/>
        public static Task<List<ProductIndicationsDto>> GetProductIndicationsAsync(ApplicationDbContext db, string? unii, string? productNameSearch, string? substanceNameSearch, string? indicationSearch, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().GetProductIndicationsAsync(db, unii, productNameSearch, substanceNameSearch, indicationSearch, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards section markdown retrieval to the markdown query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GetLabelSectionMarkdownAsync"/>
        public static Task<List<LabelSectionMarkdownDto>> GetLabelSectionMarkdownAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger, string? sectionCode = null)
            => LabelQueryLegacyCompatibility.Create().GetLabelSectionMarkdownAsync(db, documentGuid, pkSecret, logger, sectionCode);

        /**************************************************************/
        /// <summary>Forwards markdown export generation to the markdown query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GenerateLabelMarkdownAsync"/>
        public static Task<LabelMarkdownExportDto> GenerateLabelMarkdownAsync(ApplicationDbContext db, Guid documentGuid, string pkSecret, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GenerateLabelMarkdownAsync(db, documentGuid, pkSecret, logger);

        /**************************************************************/
        /// <summary>Forwards clean markdown generation to the markdown query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.GenerateCleanLabelMarkdownAsync"/>
        public static Task<string> GenerateCleanLabelMarkdownAsync(ApplicationDbContext db, Guid documentGuid, MedRecPro.Service.IClaudeApiService claudeApiService, string pkSecret, ILogger logger)
            => LabelQueryLegacyCompatibility.Create().GenerateCleanLabelMarkdownAsync(db, documentGuid, claudeApiService, pkSecret, logger);

        /**************************************************************/
        /// <summary>Forwards Orange Book patent search to the patent query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.SearchOrangeBookPatentsAsync"/>
        public static Task<List<OrangeBookPatentDto>> SearchOrangeBookPatentsAsync(ApplicationDbContext db, int? expiringInMonths, Guid? documentGuid, string? applicationNumber, string? ingredient, string? tradeName, string? patentNo, DateOnly? patentExpireDate, bool? hasPediatricFlag, bool? hasWithdrawnCommercialReasonFlag, string pkSecret, ILogger logger, int? page = null, int? size = null)
            => LabelQueryLegacyCompatibility.Create().SearchOrangeBookPatentsAsync(db, expiringInMonths, documentGuid, applicationNumber, ingredient, tradeName, patentNo, patentExpireDate, hasPediatricFlag, hasWithdrawnCommercialReasonFlag, pkSecret, logger, page, size);

        /**************************************************************/
        /// <summary>Forwards Orange Book expiring-patent counting to the patent query implementation.</summary>
        /// <seealso cref="LabelQueryDataAccess.CountExpiringPatentsAsync"/>
        public static Task<int> CountExpiringPatentsAsync(ApplicationDbContext db, int? expiringInMonths, int maxExpirationMonths, string? tradeName, string? ingredient)
            => LabelQueryLegacyCompatibility.Create().CountExpiringPatentsAsync(db, expiringInMonths, maxExpirationMonths, tradeName, ingredient);

        #endregion
    }
}
