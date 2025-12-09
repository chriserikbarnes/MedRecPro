
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /*******************************************************************************/
    /// <summary>
    /// Partial class containing private methods for building DTOs from database views.
    /// Provides high-performance access to pre-joined navigation data optimized for
    /// search and discovery workflows.
    /// </summary>
    /// <remarks>
    /// All methods use AsNoTracking() for optimal read performance.
    /// Views are designed to support AI-assisted query workflows.
    /// </remarks>
    /// <seealso cref="LabelView"/>
    /// <seealso cref="DtoLabelAccess"/>
    public static partial class DtoLabelAccess
    {
        #region Private View Builder Methods

        #region Application Number Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductsByApplicationNumber DTOs from the navigation view.
        /// Transforms view entities to DTOs with encrypted IDs for secure API responses.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductsByApplicationNumberDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
        /// <seealso cref="ProductsByApplicationNumberDto"/>
        private static List<ProductsByApplicationNumberDto> buildProductsByApplicationNumberDtos(
            ApplicationDbContext db,
            List<LabelView.ProductsByApplicationNumber> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Transform each entity to DTO with encrypted IDs
            return entities.Select(entity => new ProductsByApplicationNumberDto
            {
                ProductsByApplicationNumber = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ApplicationNumberSummary DTOs from the navigation view.
        /// Provides aggregated counts per application number.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ApplicationNumberSummaryDto"/> with summary data.</returns>
        /// <seealso cref="LabelView.ApplicationNumberSummary"/>
        /// <seealso cref="ApplicationNumberSummaryDto"/>
        private static List<ApplicationNumberSummaryDto> buildApplicationNumberSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.ApplicationNumberSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Transform each entity to DTO - summary views don't typically have IDs to encrypt
            return entities.Select(entity => new ApplicationNumberSummaryDto
            {
                ApplicationNumberSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Application Number Views

        #region Pharmacologic Class Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductsByPharmacologicClass DTOs from the navigation view.
        /// Links products to therapeutic classes via active moiety relationships.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductsByPharmacologicClassDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductsByPharmacologicClass"/>
        /// <seealso cref="ProductsByPharmacologicClassDto"/>
        private static List<ProductsByPharmacologicClassDto> buildProductsByPharmacologicClassDtos(
            ApplicationDbContext db,
            List<LabelView.ProductsByPharmacologicClass> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new ProductsByPharmacologicClassDto
            {
                ProductsByPharmacologicClass = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of PharmacologicClassHierarchy DTOs from the navigation view.
        /// Provides parent-child relationships in therapeutic classification.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="PharmacologicClassHierarchyViewDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.PharmacologicClassHierarchy"/>
        /// <seealso cref="PharmacologicClassHierarchyViewDto"/>
        private static List<PharmacologicClassHierarchyViewDto> buildPharmacologicClassHierarchyDtos(
            ApplicationDbContext db,
            List<LabelView.PharmacologicClassHierarchy> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new PharmacologicClassHierarchyViewDto
            {
                PharmacologicClassHierarchy = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of PharmacologicClassSummary DTOs from the navigation view.
        /// Provides aggregated statistics per pharmacologic class.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="PharmacologicClassSummaryDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.PharmacologicClassSummary"/>
        /// <seealso cref="PharmacologicClassSummaryDto"/>
        private static List<PharmacologicClassSummaryDto> buildPharmacologicClassSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.PharmacologicClassSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new PharmacologicClassSummaryDto
            {
                PharmacologicClassSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Pharmacologic Class Views

        #region Ingredient Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductsByIngredient DTOs from the navigation view.
        /// Links products to their ingredients for drug composition queries.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductsByIngredientDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductsByIngredient"/>
        /// <seealso cref="ProductsByIngredientDto"/>
        private static List<ProductsByIngredientDto> buildProductsByIngredientDtos(
            ApplicationDbContext db,
            List<LabelView.ProductsByIngredient> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new ProductsByIngredientDto
            {
                ProductsByIngredient = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IngredientSummary DTOs from the navigation view.
        /// Provides aggregated statistics per ingredient substance.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="IngredientSummaryDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.IngredientSummary"/>
        /// <seealso cref="IngredientSummaryDto"/>
        private static List<IngredientSummaryDto> buildIngredientSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.IngredientSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new IngredientSummaryDto
            {
                IngredientSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Ingredient Views

        #region Product Identifier Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductsByNDC DTOs from the navigation view.
        /// Provides products by NDC or other product codes.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductsByNDCDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductsByNDC"/>
        /// <seealso cref="ProductsByNDCDto"/>
        private static List<ProductsByNDCDto> buildProductsByNDCDtos(
            ApplicationDbContext db,
            List<LabelView.ProductsByNDC> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new ProductsByNDCDto
            {
                ProductsByNDC = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of PackageByNDC DTOs from the navigation view.
        /// Provides package configurations by NDC package code.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="PackageByNDCDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.PackageByNDC"/>
        /// <seealso cref="PackageByNDCDto"/>
        private static List<PackageByNDCDto> buildPackageByNDCDtos(
            ApplicationDbContext db,
            List<LabelView.PackageByNDC> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new PackageByNDCDto
            {
                PackageByNDC = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Product Identifier Views

        #region Organization Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductsByLabeler DTOs from the navigation view.
        /// Provides products grouped by labeler organization.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductsByLabelerDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductsByLabeler"/>
        /// <seealso cref="ProductsByLabelerDto"/>
        private static List<ProductsByLabelerDto> buildProductsByLabelerDtos(
            ApplicationDbContext db,
            List<LabelView.ProductsByLabeler> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new ProductsByLabelerDto
            {
                ProductsByLabeler = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LabelerSummary DTOs from the navigation view.
        /// Provides aggregated statistics per labeler organization.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="LabelerSummaryDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.LabelerSummary"/>
        /// <seealso cref="LabelerSummaryDto"/>
        private static List<LabelerSummaryDto> buildLabelerSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.LabelerSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new LabelerSummaryDto
            {
                LabelerSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Organization Views

        #region Document Navigation Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of DocumentNavigation DTOs from the navigation view.
        /// Provides document discovery with version tracking.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentNavigationDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.DocumentNavigation"/>
        /// <seealso cref="DocumentNavigationDto"/>
        private static List<DocumentNavigationDto> buildDocumentNavigationDtos(
            ApplicationDbContext db,
            List<LabelView.DocumentNavigation> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new DocumentNavigationDto
            {
                DocumentNavigation = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of DocumentVersionHistory DTOs from the navigation view.
        /// Tracks document versions over time within a SetGUID.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentVersionHistoryDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.DocumentVersionHistory"/>
        /// <seealso cref="DocumentVersionHistoryDto"/>
        private static List<DocumentVersionHistoryDto> buildDocumentVersionHistoryDtos(
            ApplicationDbContext db,
            List<LabelView.DocumentVersionHistory> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new DocumentVersionHistoryDto
            {
                DocumentVersionHistory = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Document Navigation Views

        #region Section Navigation Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of SectionNavigation DTOs from the navigation view.
        /// Provides section discovery by code and content type.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="SectionNavigationDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.SectionNavigation"/>
        /// <seealso cref="SectionNavigationDto"/>
        private static List<SectionNavigationDto> buildSectionNavigationDtos(
            ApplicationDbContext db,
            List<LabelView.SectionNavigation> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new SectionNavigationDto
            {
                SectionNavigation = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of SectionTypeSummary DTOs from the navigation view.
        /// Provides aggregated statistics per section type.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="SectionTypeSummaryDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.SectionTypeSummary"/>
        /// <seealso cref="SectionTypeSummaryDto"/>
        private static List<SectionTypeSummaryDto> buildSectionTypeSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.SectionTypeSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new SectionTypeSummaryDto
            {
                SectionTypeSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Section Navigation Views

        #region Drug Safety Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of DrugInteractionLookup DTOs from the navigation view.
        /// Provides potential drug interactions based on shared ingredients.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DrugInteractionLookupDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.DrugInteractionLookup"/>
        /// <seealso cref="DrugInteractionLookupDto"/>
        private static List<DrugInteractionLookupDto> buildDrugInteractionLookupDtos(
            ApplicationDbContext db,
            List<LabelView.DrugInteractionLookup> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new DrugInteractionLookupDto
            {
                DrugInteractionLookup = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of DEAScheduleLookup DTOs from the navigation view.
        /// Provides products with DEA controlled substance schedules.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DEAScheduleLookupDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.DEAScheduleLookup"/>
        /// <seealso cref="DEAScheduleLookupDto"/>
        private static List<DEAScheduleLookupDto> buildDEAScheduleLookupDtos(
            ApplicationDbContext db,
            List<LabelView.DEAScheduleLookup> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new DEAScheduleLookupDto
            {
                DEAScheduleLookup = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Drug Safety Views

        #region Product Summary Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductSummary DTOs from the navigation view.
        /// Provides comprehensive product overview with key attributes.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="ProductSummaryViewDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.ProductSummary"/>
        /// <seealso cref="ProductSummaryViewDto"/>
        private static List<ProductSummaryViewDto> buildProductSummaryDtos(
            ApplicationDbContext db,
            List<LabelView.ProductSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new ProductSummaryViewDto
            {
                ProductSummary = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Product Summary Views

        #region Cross-Reference Views

        /**************************************************************/
        /// <summary>
        /// Builds a list of RelatedProducts DTOs from the navigation view.
        /// Identifies related products by shared application number or ingredient.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="RelatedProductsDto"/> with encrypted IDs.</returns>
        /// <seealso cref="LabelView.RelatedProducts"/>
        /// <seealso cref="RelatedProductsDto"/>
        private static List<RelatedProductsDto> buildRelatedProductsDtos(
            ApplicationDbContext db,
            List<LabelView.RelatedProducts> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new RelatedProductsDto
            {
                RelatedProducts = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of APIEndpointGuide DTOs from the navigation view.
        /// Provides metadata for AI-assisted endpoint discovery.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="entities">Collection of view entities to transform.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="APIEndpointGuideDto"/> with endpoint metadata.</returns>
        /// <seealso cref="LabelView.APIEndpointGuide"/>
        /// <seealso cref="APIEndpointGuideDto"/>
        private static List<APIEndpointGuideDto> buildAPIEndpointGuideDtos(
            ApplicationDbContext db,
            List<LabelView.APIEndpointGuide> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities.Select(entity => new APIEndpointGuideDto
            {
                APIEndpointGuide = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();

            #endregion
        }

        #endregion Cross-Reference Views

        #region Generic Query Helpers

        /**************************************************************/
        /// <summary>
        /// Generates a unique cache key for view queries with pagination and search parameters.
        /// </summary>
        /// <param name="viewName">The name of the view being queried.</param>
        /// <param name="searchTerm">Optional search term for filtering.</param>
        /// <param name="page">Optional page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Base64-encoded cache key string.</returns>
        private static string generateCacheKey(string viewName, string? searchTerm, int? page, int? size)
        {
            #region implementation

            // Normalize search term by replacing spaces with underscores
            searchTerm = searchTerm?.Replace(" ", "_");

            // Construct key parts
            var keyParts = $"{nameof(DtoLabelAccess)}.{viewName}_{searchTerm ?? "all"}_{page}_{size}";


            return keyParts.Base64Encode();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies standard pagination to a query.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The base query to paginate.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="size">The page size.</param>
        /// <returns>The paginated query.</returns>
        private static IQueryable<T> applyPagination<T>(IQueryable<T> query, int? page, int? size)
        {
            #region implementation

            if (page.HasValue && size.HasValue && page.Value > 0 && size.Value > 0)
            {
                return query
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value);
            }

            return query;

            #endregion
        }

        #endregion Generic Query Helpers

        #endregion Private View Builder Methods
    }
}
