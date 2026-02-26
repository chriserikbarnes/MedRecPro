using MedRecPro.Helpers;


namespace MedRecPro.Models
{
    /*******************************************************************************/
    /// <summary>
    /// Container for all SPL Label navigation view DTOs. These DTOs provide
    /// a flexible Dictionary-based structure for API responses with encrypted IDs
    /// for security.
    /// </summary>
    /// <remarks>
    /// All DTOs use the Dictionary&lt;string, object?&gt; pattern for flexible
    /// serialization. Helper properties with [Newtonsoft.Json.JsonIgnore] provide
    /// type-safe access to commonly used fields with automatic ID decryption.
    /// </remarks>
    /// <seealso cref="LabelView"/>
    /// <seealso cref="DtoLabelAccess"/>

    #region Application Number Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductsByApplicationNumber view results.
    /// Provides navigation data for products sharing the same regulatory application number.
    /// </summary>
    /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
    /// <seealso cref="Label.MarketingCategory"/>
    public class ProductsByApplicationNumberDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductsByApplicationNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Primary key for navigation to Product entity.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductsByApplicationNumber.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID for full label retrieval.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductsByApplicationNumber.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Section ID for navigation path.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            ProductsByApplicationNumber.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Labeler organization ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerOrgID =>
            ProductsByApplicationNumber.TryGetValue("EncryptedLabelerOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Application identification number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationNumber =>
            ProductsByApplicationNumber.TryGetValue(nameof(ApplicationNumber), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Proprietary product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductsByApplicationNumber.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductsByApplicationNumber.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            ProductsByApplicationNumber.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for ApplicationNumberSummary view results.
    /// Provides aggregated counts per application number.
    /// </summary>
    /// <seealso cref="LabelView.ApplicationNumberSummary"/>
    public class ApplicationNumberSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> ApplicationNumberSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Application number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationNumber =>
            ApplicationNumberSummary.TryGetValue(nameof(ApplicationNumber), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Marketing category code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MarketingCategoryCode =>
            ApplicationNumberSummary.TryGetValue(nameof(MarketingCategoryCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            ApplicationNumberSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Document count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentCount =>
            ApplicationNumberSummary.TryGetValue(nameof(DocumentCount), out var value)
                ? value as int?
                : null;
    }

    #endregion Application Number Navigation DTOs

    #region Pharmacologic Class Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductsByPharmacologicClass view results.
    /// Provides products grouped by therapeutic class.
    /// </summary>
    /// <seealso cref="LabelView.ProductsByPharmacologicClass"/>
    /// <seealso cref="Label.PharmacologicClass"/>
    public class ProductsByPharmacologicClassDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductsByPharmacologicClass { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pharmacologic class ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            ProductsByPharmacologicClass.TryGetValue("EncryptedPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductsByPharmacologicClass.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductsByPharmacologicClass.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Pharmacologic class name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PharmClassName =>
            ProductsByPharmacologicClass.TryGetValue(nameof(PharmClassName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductsByPharmacologicClass.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductsByPharmacologicClass.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for PharmacologicClassHierarchy view results.
    /// Provides parent-child relationships in therapeutic classification.
    /// </summary>
    /// <seealso cref="LabelView.PharmacologicClassHierarchy"/>
    public class PharmacologicClassHierarchyViewDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> PharmacologicClassHierarchy { get; set; }

        /**************************************************************/
        /// <summary>
        /// Hierarchy ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmClassHierarchyID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedPharmClassHierarchyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Child class ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ChildClassID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedChildClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Parent class ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentClassID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedParentClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Child class name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ChildClassName =>
            PharmacologicClassHierarchy.TryGetValue(nameof(ChildClassName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Parent class name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ParentClassName =>
            PharmacologicClassHierarchy.TryGetValue(nameof(ParentClassName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for PharmacologicClassSummary view results.
    /// Provides aggregated statistics per pharmacologic class.
    /// </summary>
    /// <seealso cref="LabelView.PharmacologicClassSummary"/>
    public class PharmacologicClassSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> PharmacologicClassSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pharmacologic class ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            PharmacologicClassSummary.TryGetValue("EncryptedPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Class name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PharmClassName =>
            PharmacologicClassSummary.TryGetValue(nameof(PharmClassName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            PharmacologicClassSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;
    }

    #endregion Pharmacologic Class Navigation DTOs

    #region Ingredient Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for IngredientActiveSummary view results.
    /// Provides aggregated statistics per active ingredient including product, document, and labeler counts.
    /// </summary>
    /// <remarks>
    /// Active ingredients are identified by ClassCode != 'IACT' in the Ingredient table.
    /// This view aggregates counts across the full document hierarchy.
    /// </remarks>
    /// <seealso cref="Label.IngredientSubstance"/>
    /// <seealso cref="Label.Ingredient"/>
    public class IngredientActiveSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> IngredientActiveSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ingredient substance ID (encrypted).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            IngredientActiveSummary.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII (Unique Ingredient Identifier) code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            IngredientActiveSummary.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            IngredientActiveSummary.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Ingredient type from originating element.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IngredientType =>
            IngredientActiveSummary.TryGetValue(nameof(IngredientType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of products containing this active ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            IngredientActiveSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of documents containing this active ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentCount =>
            IngredientActiveSummary.TryGetValue(nameof(DocumentCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of labelers (marketing organizations) using this active ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerCount =>
            IngredientActiveSummary.TryGetValue(nameof(LabelerCount), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for IngredientInactiveSummary view results.
    /// Provides aggregated statistics per inactive ingredient including product, document, and labeler counts.
    /// </summary>
    /// <remarks>
    /// Inactive ingredients are identified by ClassCode = 'IACT' in the Ingredient table.
    /// This view aggregates counts across the full document hierarchy.
    /// </remarks>
    /// <seealso cref="Label.IngredientSubstance"/>
    /// <seealso cref="Label.Ingredient"/>
    public class IngredientInactiveSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> IngredientInactiveSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ingredient substance ID (encrypted).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            IngredientInactiveSummary.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII (Unique Ingredient Identifier) code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            IngredientInactiveSummary.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            IngredientInactiveSummary.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Ingredient type from originating element.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IngredientType =>
            IngredientInactiveSummary.TryGetValue(nameof(IngredientType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of products containing this inactive ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            IngredientInactiveSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of documents containing this inactive ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentCount =>
            IngredientInactiveSummary.TryGetValue(nameof(DocumentCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of labelers (marketing organizations) using this inactive ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerCount =>
            IngredientInactiveSummary.TryGetValue(nameof(LabelerCount), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for ProductsByIngredient view results.
    /// Provides products containing specific ingredients.
    /// </summary>
    /// <seealso cref="LabelView.ProductsByIngredient"/>
    /// <seealso cref="Label.Ingredient"/>
    public class ProductsByIngredientDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductsByIngredient { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ingredient ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientID =>
            ProductsByIngredient.TryGetValue("EncryptedIngredientID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductsByIngredient.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductsByIngredient.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            ProductsByIngredient.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            ProductsByIngredient.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductsByIngredient.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductsByIngredient.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for IngredientSummary view results.
    /// Provides aggregated statistics per ingredient.
    /// </summary>
    /// <seealso cref="LabelView.IngredientSummary"/>
    public class IngredientSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> IngredientSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ingredient substance ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            IngredientSummary.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            IngredientSummary.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            IngredientSummary.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            IngredientSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for IngredientView, ActiveIngredientView, and InactiveIngredientView results.
    /// Provides comprehensive ingredient search with document linkage and regulatory context.
    /// </summary>
    /// <remarks>
    /// This DTO includes helper properties for:
    /// <list type="bullet">
    ///   <item><description>Direct access to key fields via JsonIgnore properties</description></item>
    ///   <item><description>IsActive computed property to distinguish ingredient type</description></item>
    ///   <item><description>GetXmlDocumentUrl and GetCompleteLabelUrl for label retrieval</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var results = await DtoLabelAccess.SearchIngredientsAdvancedAsync(db, "R16CO5Y76E", null, null, null, null, null, secret, logger);
    /// foreach (var dto in results)
    /// {
    ///     Console.WriteLine($"{dto.SubstanceName} in {dto.ProductName} - {(dto.IsActive ? "Active" : "Inactive")}");
    ///     Console.WriteLine($"View label at: {dto.GetCompleteLabelUrl}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="LabelView.IngredientView"/>
    /// <seealso cref="LabelView.ActiveIngredientView"/>
    /// <seealso cref="LabelView.InactiveIngredientView"/>
    public class IngredientViewDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> IngredientView { get; set; }

        #region Helper Properties

        /**************************************************************/
        /// <summary>
        /// Document GUID for linking to label retrieval endpoints.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            IngredientView.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID for version history navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            IngredientView.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name containing this ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            IngredientView.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Substance name of the ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            IngredientView.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// FDA UNII code for unique ingredient identification.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            IngredientView.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Application type (NDA, ANDA, BLA, etc.) from marketing category.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationType =>
            IngredientView.TryGetValue(nameof(ApplicationType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Application number (numeric portion) for regulatory lookup.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationNumber =>
            IngredientView.TryGetValue(nameof(ApplicationNumber), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Ingredient class code.
        /// IACT = inactive ingredient; other values indicate active ingredients.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ClassCode =>
            IngredientView.TryGetValue(nameof(ClassCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Indicates whether this is an active ingredient (ClassCode != 'IACT').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsActive => ClassCode != "IACT";

        /**************************************************************/
        /// <summary>
        /// URL for retrieving the SPL XML document.
        /// Returns null if DocumentGUID is not available.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GetXmlDocumentUrl => DocumentGUID.HasValue
            ? $"/api/label/generate/{DocumentGUID}"
            : null;

        /**************************************************************/
        /// <summary>
        /// URL for retrieving the complete label as DTO.
        /// Returns null if DocumentGUID is not available.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GetCompleteLabelUrl => DocumentGUID.HasValue
            ? $"/api/label/single/{DocumentGUID}"
            : null;

        #endregion Helper Properties
    }

    /**************************************************************/
    /// <summary>
    /// Composite DTO for related ingredient search results.
    /// Groups searched ingredients with their related active/inactive ingredients and products.
    /// </summary>
    /// <remarks>
    /// Use this DTO when performing cross-reference searches to find:
    /// <list type="bullet">
    ///   <item><description>All products containing a specific ingredient</description></item>
    ///   <item><description>Related active ingredients for a given inactive ingredient</description></item>
    ///   <item><description>Related inactive ingredients for a given active ingredient</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var related = await DtoLabelAccess.FindRelatedByActiveIngredientAsync(db, "R16CO5Y76E", null, secret, logger);
    /// Console.WriteLine($"Found {related.TotalProductCount} products with this active ingredient");
    /// Console.WriteLine($"These products contain {related.TotalInactiveCount} unique inactive ingredients");
    /// </code>
    /// </example>
    /// <seealso cref="IngredientViewDto"/>
    /// <seealso cref="LabelView.IngredientView"/>
    public class IngredientRelatedResultsDto
    {
        /**************************************************************/
        /// <summary>
        /// The ingredients that matched the search criteria.
        /// </summary>
        public List<IngredientViewDto> SearchedIngredients { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Active ingredients related to the search results.
        /// </summary>
        public List<IngredientViewDto> RelatedActiveIngredients { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Inactive ingredients (excipients) related to the search results.
        /// </summary>
        public List<IngredientViewDto> RelatedInactiveIngredients { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Products containing the searched ingredients.
        /// </summary>
        public List<IngredientViewDto> RelatedProducts { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Total count of unique active ingredients found.
        /// </summary>
        public int TotalActiveCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total count of unique inactive ingredients found.
        /// </summary>
        public int TotalInactiveCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total count of unique products found.
        /// </summary>
        public int TotalProductCount { get; set; }
    }

    #endregion Ingredient Navigation DTOs

    #region Product Identifier DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductsByNDC view results.
    /// Provides products by NDC or other product codes.
    /// </summary>
    /// <seealso cref="LabelView.ProductsByNDC"/>
    /// <seealso cref="Label.ProductIdentifier"/>
    public class ProductsByNDCDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductsByNDC { get; set; }

        /**************************************************************/
        /// <summary>
        /// Product identifier ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductIdentifierID =>
            ProductsByNDC.TryGetValue("EncryptedProductIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductsByNDC.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductsByNDC.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product code (NDC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductCode =>
            ProductsByNDC.TryGetValue(nameof(ProductCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductsByNDC.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductsByNDC.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for PackageByNDC view results.
    /// Provides package configurations by NDC.
    /// </summary>
    /// <seealso cref="LabelView.PackageByNDC"/>
    /// <seealso cref="Label.PackageIdentifier"/>
    public class PackageByNDCDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> PackageByNDC { get; set; }

        /**************************************************************/
        /// <summary>
        /// Package identifier ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackageIdentifierID =>
            PackageByNDC.TryGetValue("EncryptedPackageIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            PackageByNDC.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Package code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageCode =>
            PackageByNDC.TryGetValue(nameof(PackageCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            PackageByNDC.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    #endregion Product Identifier DTOs

    #region Organization DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductsByLabeler view results.
    /// Provides products by labeler organization.
    /// </summary>
    /// <seealso cref="LabelView.ProductsByLabeler"/>
    /// <seealso cref="Label.Organization"/>
    public class ProductsByLabelerDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductsByLabeler { get; set; }

        /**************************************************************/
        /// <summary>
        /// Labeler organization ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerOrgID =>
            ProductsByLabeler.TryGetValue("EncryptedLabelerOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductsByLabeler.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductsByLabeler.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Labeler name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LabelerName =>
            ProductsByLabeler.TryGetValue(nameof(LabelerName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductsByLabeler.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductsByLabeler.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for LabelerSummary view results.
    /// Provides aggregated statistics per labeler.
    /// </summary>
    /// <seealso cref="LabelView.LabelerSummary"/>
    public class LabelerSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> LabelerSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Labeler organization ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerOrgID =>
            LabelerSummary.TryGetValue("EncryptedLabelerOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Labeler name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LabelerName =>
            LabelerSummary.TryGetValue(nameof(LabelerName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Product count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductCount =>
            LabelerSummary.TryGetValue(nameof(ProductCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Document count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentCount =>
            LabelerSummary.TryGetValue(nameof(DocumentCount), out var value)
                ? value as int?
                : null;
    }

    #endregion Organization DTOs

    #region Document Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for DocumentNavigation view results.
    /// Provides lightweight document index with version tracking.
    /// </summary>
    /// <seealso cref="LabelView.DocumentNavigation"/>
    /// <seealso cref="Label.Document"/>
    public class DocumentNavigationDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> DocumentNavigation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DocumentNavigation.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Labeler organization ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerOrgID =>
            DocumentNavigation.TryGetValue("EncryptedLabelerOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            DocumentNavigation.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            DocumentNavigation.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Document title.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentTitle =>
            DocumentNavigation.TryGetValue(nameof(DocumentTitle), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Flag indicating if latest version.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IsLatestVersion =>
            DocumentNavigation.TryGetValue(nameof(IsLatestVersion), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for DocumentVersionHistory view results.
    /// Provides version history for label sets.
    /// </summary>
    /// <seealso cref="LabelView.DocumentVersionHistory"/>
    public class DocumentVersionHistoryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> DocumentVersionHistory { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DocumentVersionHistory.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            DocumentVersionHistory.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            DocumentVersionHistory.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Version number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? VersionNumber =>
            DocumentVersionHistory.TryGetValue(nameof(VersionNumber), out var value)
                ? value as int?
                : null;
    }

    #endregion Document Navigation DTOs

    #region Section Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for SectionNavigation view results.
    /// Provides section index for document navigation.
    /// </summary>
    /// <seealso cref="LabelView.SectionNavigation"/>
    /// <seealso cref="Label.Section"/>
    public class SectionNavigationDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> SectionNavigation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            SectionNavigation.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            SectionNavigation.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Section code (LOINC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCode =>
            SectionNavigation.TryGetValue(nameof(SectionCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Section type name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionType =>
            SectionNavigation.TryGetValue(nameof(SectionType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            SectionNavigation.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for SectionTypeSummary view results.
    /// Provides summary of section types with counts.
    /// </summary>
    /// <seealso cref="LabelView.SectionTypeSummary"/>
    public class SectionTypeSummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> SectionTypeSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section code (LOINC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCode =>
            SectionTypeSummary.TryGetValue(nameof(SectionCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Section type name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionType =>
            SectionTypeSummary.TryGetValue(nameof(SectionType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Section count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionCount =>
            SectionTypeSummary.TryGetValue(nameof(SectionCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Document count.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentCount =>
            SectionTypeSummary.TryGetValue(nameof(DocumentCount), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for SectionContent view results.
    /// Provides section text content for AI summarization workflows.
    /// </summary>
    /// <remarks>
    /// Designed for efficient text retrieval supporting:
    /// - AI-powered summarization of drug label sections
    /// - Quick text extraction by DocumentGUID and optional SectionGUID/SectionCode
    /// - Content aggregation for multi-section analysis
    /// </remarks>
    /// <seealso cref="LabelView.SectionContent"/>
    /// <seealso cref="Label.Section"/>
    /// <seealso cref="Label.SectionTextContent"/>
    public class SectionContentDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> SectionContent { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document ID for navigation to Document entity.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            SectionContent.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Section ID for navigation to Section entity.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            SectionContent.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID for document identification.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            SectionContent.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID (constant across document versions).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            SectionContent.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Section GUID for section identification.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SectionGUID =>
            SectionContent.TryGetValue(nameof(SectionGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// LOINC section code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCode =>
            SectionContent.TryGetValue(nameof(SectionCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Section display name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionDisplayName =>
            SectionContent.TryGetValue(nameof(SectionDisplayName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Section title.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionTitle =>
            SectionContent.TryGetValue(nameof(SectionTitle), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Content text for AI summarization.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentText =>
            SectionContent.TryGetValue(nameof(ContentText), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Sequence number for content ordering.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            SectionContent.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Content type indicator.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentType =>
            SectionContent.TryGetValue(nameof(ContentType), out var value)
                ? value as string
                : null;
    }

    #endregion Section Navigation DTOs

    #region Drug Safety DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for DrugInteractionLookup view results.
    /// Provides drug interaction data for safety checking.
    /// </summary>
    /// <seealso cref="LabelView.DrugInteractionLookup"/>
    /// <seealso cref="Label.Ingredient"/>
    public class DrugInteractionLookupDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> DrugInteractionLookup { get; set; }

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            DrugInteractionLookup.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DrugInteractionLookup.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            DrugInteractionLookup.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Ingredient UNII.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IngredientUNII =>
            DrugInteractionLookup.TryGetValue(nameof(IngredientUNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Moiety UNII.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyUNII =>
            DrugInteractionLookup.TryGetValue(nameof(MoietyUNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            DrugInteractionLookup.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for DEAScheduleLookup view results.
    /// Provides DEA controlled substance schedule data.
    /// </summary>
    /// <seealso cref="LabelView.DEAScheduleLookup"/>
    /// <seealso cref="Label.Policy"/>
    public class DEAScheduleLookupDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> DEAScheduleLookup { get; set; }

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            DEAScheduleLookup.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DEAScheduleLookup.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            DEAScheduleLookup.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// DEA schedule code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DEAScheduleCode =>
            DEAScheduleLookup.TryGetValue(nameof(DEAScheduleCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// DEA schedule display name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DEASchedule =>
            DEAScheduleLookup.TryGetValue(nameof(DEASchedule), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            DEAScheduleLookup.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;
    }

    #endregion Drug Safety DTOs

    #region Product Summary DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductSummary view results.
    /// Provides comprehensive product profile data.
    /// </summary>
    /// <seealso cref="LabelView.ProductSummary"/>
    /// <seealso cref="Label.Product"/>
    public class ProductSummaryViewDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> ProductSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductSummary.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Document ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            ProductSummary.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Labeler organization ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LabelerOrgID =>
            ProductSummary.TryGetValue("EncryptedLabelerOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductSummary.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Generic name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GenericName =>
            ProductSummary.TryGetValue(nameof(GenericName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Primary NDC.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PrimaryNDC =>
            ProductSummary.TryGetValue(nameof(PrimaryNDC), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Application number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationNumber =>
            ProductSummary.TryGetValue(nameof(ApplicationNumber), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductSummary.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            ProductSummary.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;
    }

    #endregion Product Summary DTOs

    #region Cross-Reference DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for RelatedProducts view results.
    /// Provides related products by shared attributes.
    /// </summary>
    /// <seealso cref="LabelView.RelatedProducts"/>
    public class RelatedProductsDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> RelatedProducts { get; set; }

        /**************************************************************/
        /// <summary>
        /// Source product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SourceProductID =>
            RelatedProducts.TryGetValue("EncryptedSourceProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Related product ID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RelatedProductID =>
            RelatedProducts.TryGetValue("EncryptedRelatedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /**************************************************************/
        /// <summary>
        /// Source product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SourceProductName =>
            RelatedProducts.TryGetValue(nameof(SourceProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Related product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RelatedProductName =>
            RelatedProducts.TryGetValue(nameof(RelatedProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Relationship type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RelationshipType =>
            RelatedProducts.TryGetValue(nameof(RelationshipType), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Shared value (application number or UNII).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SharedValue =>
            RelatedProducts.TryGetValue(nameof(SharedValue), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for APIEndpointGuide view results.
    /// Provides metadata for AI-assisted endpoint discovery.
    /// </summary>
    /// <seealso cref="LabelView.APIEndpointGuide"/>
    public class APIEndpointGuideDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> APIEndpointGuide { get; set; }

        /**************************************************************/
        /// <summary>
        /// View name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ViewName =>
            APIEndpointGuide.TryGetValue(nameof(ViewName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Endpoint name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EndpointName =>
            APIEndpointGuide.TryGetValue(nameof(EndpointName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Description.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Description =>
            APIEndpointGuide.TryGetValue(nameof(Description), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Category.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Category =>
            APIEndpointGuide.TryGetValue(nameof(Category), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Usage hint.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UsageHint =>
            APIEndpointGuide.TryGetValue(nameof(UsageHint), out var value)
                ? value as string
                : null;
    }

    #endregion Cross-Reference DTOs

    #region Latest Label Navigation DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for ProductLatestLabel view results.
    /// Provides the most recent label for each UNII/ProductName combination.
    /// </summary>
    /// <remarks>
    /// Use this DTO to find the current label for a product or active ingredient.
    /// The view returns only one row per UNII/ProductName, selecting the document with the most recent EffectiveTime.
    /// </remarks>
    /// <seealso cref="LabelView.ProductLatestLabel"/>
    /// <seealso cref="LabelView.IngredientActiveSummary"/>
    public class ProductLatestLabelDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> ProductLatestLabel { get; set; }

        /**************************************************************/
        /// <summary>
        /// Proprietary product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductLatestLabel.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Active ingredient substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActiveIngredient =>
            ProductLatestLabel.TryGetValue(nameof(ActiveIngredient), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII code for the active ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            ProductLatestLabel.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID for the latest label.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductLatestLabel.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// URL for retrieving the complete label as DTO.
        /// Returns null if DocumentGUID is not available.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GetCompleteLabelUrl => DocumentGUID.HasValue
            ? $"/api/label/single/{DocumentGUID}"
            : null;
    }

    /**************************************************************/
    /// <summary>
    /// DTO for GetProductLatestLabelDetails endpoint results.
    /// Combines product search results with section markdown content and absolute URLs
    /// to original XML documents for AI/MCP consumption.
    /// </summary>
    /// <remarks>
    /// This DTO extends the ProductLatestLabel data with:
    /// - All markdown-formatted section content from the label
    /// - Absolute URLs to the original XML document (required for MCP/AI chat contexts
    ///   where relative paths would be broken links)
    ///
    /// **Use Case:** AI skill augmentation workflows where an MCP server needs to return
    /// complete product information including authoritative label content and links
    /// to source documents.
    /// </remarks>
    /// <example>
    /// Response structure:
    /// <code>
    /// {
    ///   "ProductLatestLabel": {
    ///     "ProductName": "LIPITOR",
    ///     "ActiveIngredient": "ATORVASTATIN CALCIUM",
    ///     "UNII": "A0JWA85V8F",
    ///     "DocumentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2"
    ///   },
    ///   "ViewLabelUrl": "https://example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/false",
    ///   "ViewLabelMinifiedUrl": "https://example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/true",
    ///   "Sections": [
    ///     {
    ///       "LabelSectionMarkdown": {
    ///         "SectionCode": "34067-9",
    ///         "SectionTitle": "INDICATIONS AND USAGE",
    ///         "FullSectionText": "## INDICATIONS AND USAGE\n\nLIPITOR is indicated..."
    ///       }
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ProductLatestLabelDto"/>
    /// <seealso cref="LabelSectionMarkdownDto"/>
    /// <seealso cref="LabelView.ProductLatestLabel"/>
    /// <seealso cref="LabelView.LabelSectionMarkdown"/>
    public class ProductLatestLabelDetailsDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all ProductLatestLabel view columns.
        /// Includes ProductName, ActiveIngredient, UNII, DocumentGUID, and other product identifiers.
        /// </summary>
        /// <seealso cref="ProductLatestLabelDto.ProductLatestLabel"/>
        public required Dictionary<string, object?> ProductLatestLabel { get; set; }

        /**************************************************************/
        /// <summary>
        /// Absolute URL to view the FDA drug label in a web browser.
        /// When opened in a browser, renders as formatted HTML via XSL stylesheet transformation.
        /// This URL includes the full scheme and host to ensure it works in MCP/AI contexts.
        /// </summary>
        /// <remarks>
        /// **CRITICAL FOR AI RESPONSES:** This URL MUST be included in every response to users
        /// for source verification and traceability to the official FDA label document.
        /// Format in responses as: [View Full Label ({ProductName})]({ViewLabelUrl})
        /// </remarks>
        /// <example>https://medrecpro.example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/false</example>
        /// <seealso cref="LabelController.OriginalXmlDocument"/>
        public string? ViewLabelUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Absolute URL to view the FDA drug label (minified version for reduced bandwidth).
        /// When opened in a browser, renders as formatted HTML via XSL stylesheet transformation.
        /// This URL includes the full scheme and host to ensure it works in MCP/AI contexts.
        /// </summary>
        /// <remarks>
        /// Use this URL when bandwidth is a concern. The minified version removes unnecessary
        /// whitespace from the XML but renders identically in a browser.
        /// </remarks>
        /// <example>https://medrecpro.example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/true</example>
        /// <seealso cref="LabelController.OriginalXmlDocument"/>
        public string? ViewLabelMinifiedUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Collection of markdown-formatted label sections for this product.
        /// Each section contains the full markdown text ready for AI/LLM consumption.
        /// </summary>
        /// <remarks>
        /// Sections are returned in LOINC code order and include common sections like:
        /// - 34067-9: Indications and Usage
        /// - 34084-4: Adverse Reactions
        /// - 34070-3: Contraindications
        /// - 43685-7: Warnings and Precautions
        /// - 34068-7: Dosage and Administration
        /// </remarks>
        /// <seealso cref="LabelSectionMarkdownDto"/>
        public List<LabelSectionMarkdownDto>? Sections { get; set; }

        #region Helper Properties

        /**************************************************************/
        /// <summary>
        /// Proprietary product name.
        /// </summary>
        /// <seealso cref="ProductLatestLabelDto.ProductName"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductLatestLabel.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Active ingredient substance name.
        /// </summary>
        /// <seealso cref="ProductLatestLabelDto.ActiveIngredient"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActiveIngredient =>
            ProductLatestLabel.TryGetValue(nameof(ActiveIngredient), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII code for the active ingredient.
        /// </summary>
        /// <seealso cref="ProductLatestLabelDto.UNII"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            ProductLatestLabel.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID for the latest label.
        /// </summary>
        /// <seealso cref="ProductLatestLabelDto.DocumentGUID"/>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductLatestLabel.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        #endregion Helper Properties
    }

    /**************************************************************/
    /// <summary>
    /// DTO for ProductIndications view results.
    /// Provides product indication text combined with active ingredients.
    /// </summary>
    /// <remarks>
    /// Use this DTO to retrieve indication text for products by active ingredient.
    /// The view filters to INDICATION sections only and excludes inactive ingredients.
    /// </remarks>
    /// <seealso cref="LabelView.ProductIndications"/>
    /// <seealso cref="LabelView.SectionNavigation"/>
    public class ProductIndicationsDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> ProductIndications { get; set; }

        /**************************************************************/
        /// <summary>
        /// Proprietary product name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            ProductIndications.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Active ingredient substance name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            ProductIndications.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// UNII code for the active ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            ProductIndications.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Document GUID for label retrieval.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            ProductIndications.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Combined indication text content from SectionTextContent and TextListItem.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentText =>
            ProductIndications.TryGetValue(nameof(ContentText), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// URL for retrieving the complete label as DTO.
        /// Returns null if DocumentGUID is not available.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GetCompleteLabelUrl => DocumentGUID.HasValue
            ? $"/api/label/single/{DocumentGUID}"
            : null;
    }

    #endregion Latest Label Navigation DTOs

    #region Section Markdown DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for LabelSectionMarkdown view results.
    /// Provides aggregated, markdown-formatted section text for LLM/API consumption.
    /// </summary>
    /// <remarks>
    /// Use this DTO to retrieve complete section text formatted as markdown.
    /// The view aggregates all content blocks for each section and converts
    /// SPL formatting tags to markdown equivalents (bold, italics, underline).
    ///
    /// This DTO is designed for AI summarization workflows where the Claude API
    /// needs authoritative label content rather than relying on training data.
    /// </remarks>
    /// <seealso cref="LabelView.LabelSectionMarkdown"/>
    /// <seealso cref="LabelView.SectionContent"/>
    public class LabelSectionMarkdownDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> LabelSectionMarkdown { get; set; }

        #region Helper Properties

        /**************************************************************/
        /// <summary>
        /// Document GUID for linking to label retrieval endpoints.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            LabelSectionMarkdown.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Set GUID for version history navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            LabelSectionMarkdown.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Title of the document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentTitle =>
            LabelSectionMarkdown.TryGetValue(nameof(DocumentTitle), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// LOINC section code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCode =>
            LabelSectionMarkdown.TryGetValue(nameof(SectionCode), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Human-readable section title.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionTitle =>
            LabelSectionMarkdown.TryGetValue(nameof(SectionTitle), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Computed unique key for this section.
        /// Format: {DocumentGUID}|{SectionCode or 'NULL'}|{SectionTitle}
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionKey =>
            LabelSectionMarkdown.TryGetValue(nameof(SectionKey), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Complete markdown-formatted section text with header.
        /// Ready for direct consumption by LLM APIs.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FullSectionText =>
            LabelSectionMarkdown.TryGetValue(nameof(FullSectionText), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Number of content blocks aggregated into this section.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContentBlockCount =>
            LabelSectionMarkdown.TryGetValue(nameof(ContentBlockCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// URL for retrieving the complete label as DTO.
        /// Returns null if DocumentGUID is not available.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GetCompleteLabelUrl => DocumentGUID.HasValue
            ? $"/api/label/single/{DocumentGUID}"
            : null;

        #endregion Helper Properties
    }

    /**************************************************************/
    /// <summary>
    /// Result DTO for complete document markdown generation.
    /// Contains the full markdown text along with metadata about the document.
    /// </summary>
    /// <remarks>
    /// This DTO is designed for AI skill augmentation workflows where the Claude API
    /// needs authoritative, complete label content to generate accurate summaries.
    ///
    /// The markdown includes:
    /// - Document header with title and metadata
    /// - All sections in order with ## headers
    /// - Markdown formatting converted from SPL tags
    /// </remarks>
    /// <seealso cref="LabelSectionMarkdownDto"/>
    /// <seealso cref="LabelView.LabelSectionMarkdown"/>
    public class LabelMarkdownExportDto
    {
        /**************************************************************/
        /// <summary>
        /// Document GUID identifying this label version.
        /// </summary>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Set GUID for version tracking (constant across versions).
        /// </summary>
        public Guid? SetGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Title of the document.
        /// </summary>
        public string? DocumentTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of sections included in the markdown.
        /// </summary>
        public int SectionCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total content blocks aggregated across all sections.
        /// </summary>
        public int TotalContentBlocks { get; set; }

        /**************************************************************/
        /// <summary>
        /// Complete markdown text for the entire document.
        /// Includes header information and all section content.
        /// </summary>
        public string? FullMarkdown { get; set; }
    }

    #endregion Section Markdown DTOs

    #region Inventory Summary DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for InventorySummary view results.
    /// Provides comprehensive inventory summary data for answering "what products do you have" questions.
    /// </summary>
    /// <remarks>
    /// Returns aggregated counts across multiple dimensions: Documents, Products, Labelers,
    /// Active Ingredients, Pharmacologic Classes, NDCs, Marketing Categories, Dosage Forms, etc.
    ///
    /// This DTO does NOT encrypt IDs because the view contains only aggregate counts, not navigation IDs.
    /// </remarks>
    /// <seealso cref="LabelView.InventorySummary"/>
    public class InventorySummaryDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns.
        /// </summary>
        public required Dictionary<string, object?> InventorySummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Grouping category for the summary row (TOTALS, BY_MARKETING_CATEGORY, TOP_LABELERS, etc.).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Category =>
            InventorySummary.TryGetValue(nameof(Category), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Dimension being measured (Documents, Products, Marketing Category, Labeler, etc.).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Dimension =>
            InventorySummary.TryGetValue(nameof(Dimension), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Specific value within the dimension (e.g., "NDA", labeler name, dosage form).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DimensionValue =>
            InventorySummary.TryGetValue(nameof(DimensionValue), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Count of items in this dimension/value combination.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ItemCount =>
            InventorySummary.TryGetValue(nameof(ItemCount), out var value)
                ? value as int?
                : null;

        /**************************************************************/
        /// <summary>
        /// Sort order for display purposes.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public long? SortOrder =>
            InventorySummary.TryGetValue(nameof(SortOrder), out var value)
                ? value as long?
                : null;
    }

    #endregion Inventory Summary DTOs

    #region Orange Book Patent DTOs

    /**************************************************************/
    /// <summary>
    /// DTO for OrangeBookPatent view results.
    /// Provides Orange Book NDA patent data with SPL label cross-references.
    /// Includes a computed LabelLink for navigating to the FDA label when a DocumentGUID is available.
    /// </summary>
    /// <remarks>
    /// The OrangeBookPatent dictionary contains all view columns with encrypted IDs.
    /// The LabelLink property is a separate serialized field computed at runtime —
    /// it is NOT inside the dictionary.
    /// </remarks>
    /// <seealso cref="LabelView.OrangeBookPatent"/>
    /// <seealso cref="DataAccess.DtoLabelAccess"/>
    public class OrangeBookPatentDto
    {
        /**************************************************************/
        /// <summary>
        /// Dictionary containing all view columns with encrypted IDs.
        /// </summary>
        public required Dictionary<string, object?> OrangeBookPatent { get; set; }

        /**************************************************************/
        /// <summary>
        /// Relative URL to view the FDA label in a browser via XSL stylesheet transformation.
        /// Null when no DocumentGUID is available for the patent row.
        /// </summary>
        /// <remarks>
        /// Format: /api/Label/original/{DocumentGUID}/false
        /// Consumers should prepend the base URL (scheme + host) to form an absolute URL.
        /// </remarks>
        /// <example>/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/false</example>
        public string? LabelLink { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document GUID for the cross-referenced SPL label.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            OrangeBookPatent.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /**************************************************************/
        /// <summary>
        /// Trade/brand name of the product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TradeName =>
            OrangeBookPatent.TryGetValue(nameof(TradeName), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Active ingredient name(s) for the product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Ingredient =>
            OrangeBookPatent.TryGetValue(nameof(Ingredient), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// NDA application number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationNumber =>
            OrangeBookPatent.TryGetValue(nameof(ApplicationNumber), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Patent number assigned by the USPTO.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PatentNo =>
            OrangeBookPatent.TryGetValue(nameof(PatentNo), out var value)
                ? value as string
                : null;

        /**************************************************************/
        /// <summary>
        /// Patent expiration date.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? PatentExpireDate =>
            OrangeBookPatent.TryGetValue(nameof(PatentExpireDate), out var value)
                ? value as DateTime?
                : null;
    }

    #endregion Orange Book Patent DTOs
}