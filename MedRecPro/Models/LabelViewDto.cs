using MedRecPro.Helpers;
using MedRecPro.Service;

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
}