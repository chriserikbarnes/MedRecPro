using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedRecPro.Models
{
    /*******************************************************************************/
    /// <summary>
    /// Container for all SPL Label navigation view entities. These read-only views
    /// provide lightweight navigation objects for API consumption and AI-assisted
    /// query workflows. Views return IDs/GUIDs for navigation rather than full entity graphs.
    /// </summary>
    /// <remarks>
    /// Views are designed to support the following information flow:
    /// User Input -> Claude API -> API Endpoints -> Views -> Results -> Claude Interpretation -> Report
    /// 
    /// All views are read-only and should be queried with AsNoTracking() for optimal performance.
    /// Views leverage indexes created in MedRecPro_Indexes.sql.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="Label.Document"/>
    /// <seealso cref="Label.Product"/>
    public class LabelView
    {
        #region Application Number Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductsByApplicationNumber.
        /// Locates all products sharing the same regulatory application number (NDA, ANDA, BLA).
        /// Enables cross-referencing of labels under a single approval.
        /// </summary>
        /// <remarks>
        /// Returns lightweight navigation objects with Product and Document IDs.
        /// Indexes Used: IX_ProductIdentifier_ProductID, IX_Document_DocumentGUID
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductsByApplicationNumber&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.ApplicationNumber == "NDA014526")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.Document"/>
        [Table("vw_ProductsByApplicationNumber")]
        public class ProductsByApplicationNumber
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Application identification number (e.g., NDA014526, ANDA125654, BLA103948).
            /// </summary>
            public string? ApplicationNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category code (e.g., NDA, ANDA, BLA).
            /// </summary>
            public string? MarketingCategoryCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable marketing category name.
            /// </summary>
            public string? MarketingCategoryName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Date the application was approved by FDA.
            /// </summary>
            public DateTime? ApprovalDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Primary key for navigation to Product entity.
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Proprietary name of the product.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code (e.g., ORAL TABLET).
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID for full label retrieval.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique identifier for document version.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID (constant across versions).
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number of the document.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Effective date of the label.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Title of the document.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type code (LOINC).
            /// </summary>
            public string? DocumentCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable document type name.
            /// </summary>
            public string? DocumentType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section ID for navigation path.
            /// </summary>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section code (LOINC).
            /// </summary>
            public string? SectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ApplicationNumberSummary.
        /// Aggregated summary showing product and document counts per application number.
        /// </summary>
        /// <remarks>
        /// Use to understand the scope of NDA/ANDA/BLA approvals.
        /// Provides counts of products and documents per application number.
        /// </remarks>
        /// <example>
        /// <code>
        /// var summaries = await db.Set&lt;LabelView.ApplicationNumberSummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.MarketingCategoryCode == "NDA")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingCategory"/>
        [Table("vw_ApplicationNumberSummary")]
        public class ApplicationNumberSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Application number (part of composite key).
            /// </summary>            
            public string? ApplicationNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category code (part of composite key).
            /// </summary>
            public string? MarketingCategoryCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable marketing category name.
            /// </summary>
            public string? MarketingCategoryName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Earliest approval date for this application.
            /// </summary>
            public DateTime? EarliestApprovalDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Latest approval date for this application.
            /// </summary>
            public DateTime? LatestApprovalDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of distinct products under this application.
            /// </summary>
            public int? ProductCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of distinct documents under this application.
            /// </summary>
            public int? DocumentCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of distinct label sets under this application.
            /// </summary>
            public int? LabelSetCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Most recent label effective date.
            /// </summary>
            public DateTime? MostRecentLabelDate { get; set; }

            #endregion properties
        }

        #endregion Application Number Navigation Views

        #region Pharmacologic Class Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductsByPharmacologicClass.
        /// Locates products by pharmacologic/therapeutic class via active moiety linkage.
        /// </summary>
        /// <remarks>
        /// Links products to their pharmacologic classes via active moieties.
        /// Enables therapeutic category-based drug discovery (e.g., find all Beta-Adrenergic Blockers).
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductsByPharmacologicClass&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.PharmClassName.Contains("Beta-Blocker"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClass"/>
        /// <seealso cref="Label.PharmacologicClassLink"/>
        [Table("vw_ProductsByPharmacologicClass")]
        public class ProductsByPharmacologicClass
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class ID.
            /// </summary>
            public int? PharmacologicClassID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class code.
            /// </summary>
            public string? PharmClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable pharmacologic class name.
            /// </summary>
            public string? PharmClassName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active moiety ID.
            /// </summary>
            public int? ActiveMoietyID { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code of the active moiety.
            /// </summary>
            public string? MoietyUNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Name of the active moiety.
            /// </summary>
            public string? MoietyName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code of the substance.
            /// </summary>
            public string? SubstanceUNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Name of the substance.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID (primary key for navigation).
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Proprietary product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code.
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID for full label retrieval.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique document identifier.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID constant across versions.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label effective date.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_PharmacologicClassHierarchy.
        /// Shows parent-child relationships in the pharmacologic class hierarchy.
        /// </summary>
        /// <remarks>
        /// Exposes the pharmacologic class hierarchy for navigation.
        /// Enables drill-down through therapeutic categories.
        /// </remarks>
        /// <example>
        /// <code>
        /// var hierarchy = await db.Set&lt;LabelView.PharmacologicClassHierarchy&gt;()
        ///     .AsNoTracking()
        ///     .Where(h => h.ParentClassCode == "N0000000181")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClass"/>
        [Table("vw_PharmacologicClassHierarchy")]
        public class PharmacologicClassHierarchy
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Child (more specific) class ID.
            /// </summary>
            public int? ChildClassID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Child class code.
            /// </summary>
            public string? ChildClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Child class name.
            /// </summary>
            public string? ChildClassName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent (more general) class ID.
            /// </summary>
            public int? ParentClassID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent class code.
            /// </summary>
            public string? ParentClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent class name.
            /// </summary>
            public string? ParentClassName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Hierarchy linkage ID (primary key).
            /// </summary>
            
            public int? PharmClassHierarchyID { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_PharmacologicClassSummary.
        /// Summary of pharmacologic classes with substance and product counts.
        /// </summary>
        /// <remarks>
        /// Aggregated view for therapeutic category analysis.
        /// Use to discover which therapeutic classes have the most products.
        /// </remarks>
        /// <example>
        /// <code>
        /// var summaries = await db.Set&lt;LabelView.PharmacologicClassSummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.ProductCount > 100)
        ///     .OrderByDescending(s => s.ProductCount)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClass"/>
        [Table("vw_PharmacologicClassSummary")]
        public class PharmacologicClassSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class ID (primary key).
            /// </summary>
            
            public int? PharmacologicClassID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class code.
            /// </summary>
            public string? PharmClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable class name.
            /// </summary>
            public string? PharmClassName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of linked substances.
            /// </summary>
            public int? LinkedSubstanceCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of products in this class.
            /// </summary>
            public int? ProductCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of documents in this class.
            /// </summary>
            public int? DocumentCount { get; set; }

            #endregion properties
        }

        #endregion Pharmacologic Class Navigation Views

        #region Ingredient and Substance Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductsByIngredient.
        /// Locates products by active/inactive ingredient via UNII code or substance name.
        /// </summary>
        /// <remarks>
        /// Links products to their ingredients for drug composition queries.
        /// Includes strength and active moiety information.
        /// Indexes Used: IX_Ingredient_IngredientSubstanceID, IX_IngredientSubstance_UNII
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductsByIngredient&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.UNII == "R16CO5Y76E" || p.SubstanceName.Contains("aspirin"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        [Table("vw_ProductsByIngredient")]
        public class ProductsByIngredient
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code for substance lookup.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Substance name.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Type of ingredient (active, inactive, etc.).
            /// </summary>
            public string? IngredientType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient ID (primary key).
            /// </summary>
            
            public int? IngredientID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient class code.
            /// </summary>
            public string? IngredientClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Quantity numerator value.
            /// </summary>
            public decimal? QuantityNumerator { get; set; }

            /**************************************************************/
            /// <summary>
            /// Quantity numerator unit.
            /// </summary>
            public string? QuantityNumeratorUnit { get; set; }

            /**************************************************************/
            /// <summary>
            /// Quantity denominator value.
            /// </summary>
            public decimal? QuantityDenominator { get; set; }

            /**************************************************************/
            /// <summary>
            /// Strength display name.
            /// </summary>
            public string? StrengthDisplayName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Sequence number of ingredient in product.
            /// </summary>
            public int? IngredientSequence { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active moiety ID.
            /// </summary>
            public int? ActiveMoietyID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active moiety UNII code.
            /// </summary>
            public string? MoietyUNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active moiety name.
            /// </summary>
            public string? MoietyName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID for navigation.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Proprietary product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code.
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label effective date.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_IngredientSummary.
        /// Summary of ingredients with product and labeler counts.
        /// </summary>
        /// <remarks>
        /// Use for ingredient prevalence analysis.
        /// Discover most common ingredients across products.
        /// </remarks>
        /// <example>
        /// <code>
        /// var summaries = await db.Set&lt;LabelView.IngredientSummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.ProductCount > 50)
        ///     .OrderByDescending(s => s.ProductCount)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.IngredientSubstance"/>
        [Table("vw_IngredientSummary")]
        public class IngredientSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID (primary key).
            /// </summary>
            
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Substance name.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient type.
            /// </summary>
            public string? IngredientType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of products containing this ingredient.
            /// </summary>
            public int? ProductCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of documents with this ingredient.
            /// </summary>
            public int? DocumentCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of labelers using this ingredient.
            /// </summary>
            public int? LabelerCount { get; set; }

            #endregion properties
        }

        #endregion Ingredient and Substance Navigation Views

        #region Product Identifier Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductsByNDC.
        /// Locates products by NDC, GTIN, or other product codes.
        /// </summary>
        /// <remarks>
        /// Critical for pharmacy system integration and product lookup.
        /// Indexes Used: IX_ProductIdentifier_IdentifierValue_on_IdentifierType
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductsByNDC&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.ProductCode == "12345-678-90")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.ProductIdentifier"/>
        [Table("vw_ProductsByNDC")]
        public class ProductsByNDC
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Product identifier ID (primary key).
            /// </summary>
            
            public int? ProductIdentifierID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product code (NDC, GTIN, etc.).
            /// </summary>
            public string? ProductCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Code type (NDC, GTIN, etc.).
            /// </summary>
            public string? CodeType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Code system OID.
            /// </summary>
            public string? CodeSystemOID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code.
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Generic medicine ID.
            /// </summary>
            public int? GenericMedicineID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Generic name.
            /// </summary>
            public string? GenericName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category code.
            /// </summary>
            public string? MarketingCategoryCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category name.
            /// </summary>
            public string? MarketingCategoryName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number.
            /// </summary>
            public string? ApplicationNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label effective date.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_PackageByNDC.
        /// Locates package configurations by NDC package code.
        /// </summary>
        /// <remarks>
        /// Shows packaging hierarchy and quantities.
        /// Enables package lookup by NDC package code.
        /// </remarks>
        /// <example>
        /// <code>
        /// var packages = await db.Set&lt;LabelView.PackageByNDC&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.PackageCode.StartsWith("12345"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="Label.PackagingLevel"/>
        [Table("vw_PackageByNDC")]
        public class PackageByNDC
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Package identifier ID (primary key).
            /// </summary>
            
            public int? PackageIdentifierID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Package code.
            /// </summary>
            public string? PackageCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Code type.
            /// </summary>
            public string? CodeType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Packaging level ID.
            /// </summary>
            public int? PackagingLevelID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Package item code.
            /// </summary>
            public string? PackageItemCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Package form code.
            /// </summary>
            public string? PackageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Package type display name.
            /// </summary>
            public string? PackageType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Quantity numerator.
            /// </summary>
            public decimal? QuantityNumerator { get; set; }

            /**************************************************************/
            /// <summary>
            /// Quantity numerator unit.
            /// </summary>
            public string? QuantityNumeratorUnit { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            #endregion properties
        }

        #endregion Product Identifier Navigation Views

        #region Organization Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductsByLabeler.
        /// Locates products by labeler organization via name or identifier.
        /// </summary>
        /// <remarks>
        /// Supports lookup by organization name or identifier (DUNS, Labeler Code).
        /// Lists products by labeler/marketing organization.
        /// Indexes Used: IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductsByLabeler&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.LabelerName.Contains("Pfizer"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="Label.DocumentAuthor"/>
        [Table("vw_ProductsByLabeler")]
        public class ProductsByLabeler
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Organization identifier ID.
            /// </summary>
            public int? OrganizationIdentifierID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Organization identifier value (DUNS, Labeler Code, etc.).
            /// </summary>
            public string? OrgIdentifierValue { get; set; }

            /**************************************************************/
            /// <summary>
            /// Organization identifier type.
            /// </summary>
            public string? OrgIdentifierType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID (primary key).
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code.
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Generic name.
            /// </summary>
            public string? GenericName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number.
            /// </summary>
            public string? ApplicationNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category.
            /// </summary>
            public string? MarketingCategory { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label effective date.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_LabelerSummary.
        /// Summary of labelers with product and document counts.
        /// </summary>
        /// <remarks>
        /// Aggregated view of labelers with portfolio statistics.
        /// Use for labeler portfolio analysis.
        /// </remarks>
        /// <example>
        /// <code>
        /// var summaries = await db.Set&lt;LabelView.LabelerSummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.ProductCount > 100)
        ///     .OrderByDescending(s => s.ProductCount)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Organization"/>
        [Table("vw_LabelerSummary")]
        public class LabelerSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID (primary key).
            /// </summary>
            
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product count.
            /// </summary>
            public int? ProductCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document count.
            /// </summary>
            public int? DocumentCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label set count.
            /// </summary>
            public int? LabelSetCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Earliest label date.
            /// </summary>
            public DateTime? EarliestLabelDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Most recent label date.
            /// </summary>
            public DateTime? MostRecentLabelDate { get; set; }

            #endregion properties
        }

        #endregion Organization Navigation Views

        #region Document Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_DocumentNavigation.
        /// Lightweight document index for navigation with version tracking.
        /// </summary>
        /// <remarks>
        /// Shows version info, product counts, and latest version flag.
        /// Use for document listings and version navigation.
        /// Indexes Used: IX_Document_DocumentGUID, IX_Document_SetGUID
        /// </remarks>
        /// <example>
        /// <code>
        /// var documents = await db.Set&lt;LabelView.DocumentNavigation&gt;()
        ///     .AsNoTracking()
        ///     .Where(d => d.IsLatestVersion == 1)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        [Table("vw_DocumentNavigation")]
        public class DocumentNavigation
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Document ID (primary key).
            /// </summary>
            
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type code.
            /// </summary>
            public string? DocumentCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type name.
            /// </summary>
            public string? DocumentType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Effective date.
            /// </summary>
            public DateTime? EffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product count for this document.
            /// </summary>
            public int? ProductCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Total versions in this set.
            /// </summary>
            public int? TotalVersions { get; set; }

            /**************************************************************/
            /// <summary>
            /// Flag indicating if this is the latest version (1=yes, 0=no).
            /// </summary>
            public int? IsLatestVersion { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_DocumentVersionHistory.
        /// Shows version history for label sets with predecessor references.
        /// </summary>
        /// <remarks>
        /// Includes predecessor document references for revision tracking.
        /// Enables revision comparison across versions.
        /// </remarks>
        /// <example>
        /// <code>
        /// var history = await db.Set&lt;LabelView.DocumentVersionHistory&gt;()
        ///     .AsNoTracking()
        ///     .Where(h => h.SetGUID == targetSetGuid)
        ///     .OrderBy(h => h.VersionNumber)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.RelatedDocument"/>
        [Table("vw_DocumentVersionHistory")]
        public class DocumentVersionHistory
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Set GUID for grouping versions.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID (primary key).
            /// </summary>
            
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Effective date.
            /// </summary>
            public DateTime? EffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type code.
            /// </summary>
            public string? DocumentCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type name.
            /// </summary>
            public string? DocumentType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Predecessor document GUID.
            /// </summary>
            public Guid? PredecessorDocGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Predecessor version number.
            /// </summary>
            public int? PredecessorVersion { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        #endregion Document Navigation Views

        #region Section Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_SectionNavigation.
        /// Section index for navigating document structure by LOINC code.
        /// </summary>
        /// <remarks>
        /// Supports lookup by LOINC code to find specific section types across documents.
        /// Common codes: 34066-1 (Boxed Warning), 34067-9 (Indications), etc.
        /// Indexes Used: IX_Section_SectionCode_on_DocumentID
        /// </remarks>
        /// <example>
        /// <code>
        /// var sections = await db.Set&lt;LabelView.SectionNavigation&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.SectionCode == "34066-1") // Boxed Warnings
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Section"/>
        [Table("vw_SectionNavigation")]
        public class SectionNavigation
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Section ID (primary key).
            /// </summary>
            
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section GUID.
            /// </summary>
            public Guid? SectionGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section code (LOINC).
            /// </summary>
            public string? SectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section type name.
            /// </summary>
            public string? SectionType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section title.
            /// </summary>
            public string? SectionTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent section ID.
            /// </summary>
            public int? ParentSectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent section code.
            /// </summary>
            public string? ParentSectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Parent section title.
            /// </summary>
            public string? ParentSectionTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Content block count.
            /// </summary>
            public int? ContentBlockCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_SectionTypeSummary.
        /// Summary of section types (LOINC codes) with document counts.
        /// </summary>
        /// <remarks>
        /// Use to understand section type prevalence across documents.
        /// </remarks>
        /// <example>
        /// <code>
        /// var summaries = await db.Set&lt;LabelView.SectionTypeSummary&gt;()
        ///     .AsNoTracking()
        ///     .OrderByDescending(s => s.DocumentCount)
        ///     .Take(20)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Section"/>
        [Table("vw_SectionTypeSummary")]
        public class SectionTypeSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Section code (LOINC) - primary key.
            /// </summary>
            
            public string? SectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section type name.
            /// </summary>
            public string? SectionType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Total sections count.
            /// </summary>
            public int? SectionCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document count.
            /// </summary>
            public int? DocumentCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label set count.
            /// </summary>
            public int? LabelSetCount { get; set; }

            #endregion properties
        }

        #endregion Section Navigation Views

        #region Drug Safety Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_DrugInteractionLookup.
        /// Drug interaction lookup data with active ingredients and moieties.
        /// </summary>
        /// <remarks>
        /// Provides active ingredients for interaction checking systems.
        /// Use for drug interaction analysis workflows.
        /// </remarks>
        /// <example>
        /// <code>
        /// var lookupData = await db.Set&lt;LabelView.DrugInteractionLookup&gt;()
        ///     .AsNoTracking()
        ///     .Where(d => new[] {"UNII1", "UNII2"}.Contains(d.IngredientUNII))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.ActiveMoiety"/>
        [Table("vw_DrugInteractionLookup")]
        public class DrugInteractionLookup
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Product ID (primary key).
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// NDC code.
            /// </summary>
            public string? NDC { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient UNII for interaction checking.
            /// </summary>
            public string? IngredientUNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient name.
            /// </summary>
            public string? IngredientName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active moiety ID.
            /// </summary>
            public int? ActiveMoietyID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Moiety UNII (often used for interaction databases).
            /// </summary>
            public string? MoietyUNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Moiety name.
            /// </summary>
            public string? MoietyName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient class code.
            /// </summary>
            public string? IngredientClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class ID.
            /// </summary>
            public int? PharmacologicClassID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class code.
            /// </summary>
            public string? PharmClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Pharmacologic class name.
            /// </summary>
            public string? PharmClassName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_DEAScheduleLookup.
        /// DEA controlled substance schedule lookup.
        /// </summary>
        /// <remarks>
        /// Critical for pharmacy dispensing compliance.
        /// Shows DEA schedule classification for products.
        /// </remarks>
        /// <example>
        /// <code>
        /// var scheduled = await db.Set&lt;LabelView.DEAScheduleLookup&gt;()
        ///     .AsNoTracking()
        ///     .Where(d => d.DEAScheduleCode != null)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Policy"/>
        [Table("vw_DEAScheduleLookup")]
        public class DEAScheduleLookup
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Product ID (primary key).
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// NDC code.
            /// </summary>
            public string? NDC { get; set; }

            /**************************************************************/
            /// <summary>
            /// DEA schedule code (CII, CIII, etc.).
            /// </summary>
            public string? DEAScheduleCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// DEA schedule display name.
            /// </summary>
            public string? DEASchedule { get; set; }

            /**************************************************************/
            /// <summary>
            /// Generic name.
            /// </summary>
            public string? GenericName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            #endregion properties
        }

        #endregion Drug Safety Views

        #region Product Summary Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductSummary.
        /// Comprehensive product summary consolidating key attributes.
        /// </summary>
        /// <remarks>
        /// Primary view for product profile API responses.
        /// Consolidates product information from multiple related tables.
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await db.Set&lt;LabelView.ProductSummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.ProductName.Contains("Lipitor"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Product"/>
        [Table("vw_ProductSummary")]
        public class ProductSummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Product ID (primary key).
            /// </summary>
            
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product suffix.
            /// </summary>
            public string? ProductSuffix { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form code.
            /// </summary>
            public string? DosageFormCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dosage form name.
            /// </summary>
            public string? DosageFormName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product description text.
            /// </summary>
            public string? DescriptionText { get; set; }

            /**************************************************************/
            /// <summary>
            /// Generic name.
            /// </summary>
            public string? GenericName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Primary NDC code.
            /// </summary>
            public string? PrimaryNDC { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category code.
            /// </summary>
            public string? MarketingCategoryCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category.
            /// </summary>
            public string? MarketingCategory { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number.
            /// </summary>
            public string? ApplicationNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Approval date.
            /// </summary>
            public DateTime? ApprovalDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Route of administration.
            /// </summary>
            public string? RouteOfAdministration { get; set; }

            /**************************************************************/
            /// <summary>
            /// DEA schedule.
            /// </summary>
            public string? DEASchedule { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active ingredient count.
            /// </summary>
            public int? ActiveIngredientCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document GUID.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document title.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Label effective date.
            /// </summary>
            public DateTime? LabelEffectiveDate { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type code.
            /// </summary>
            public string? DocumentCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document type.
            /// </summary>
            public string? DocumentType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler organization ID.
            /// </summary>
            public int? LabelerOrgID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler name.
            /// </summary>
            public string? LabelerName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Labeler code.
            /// </summary>
            public string? LabelerCode { get; set; }

            #endregion properties
        }

        #endregion Product Summary Views

        #region Cross-Reference and Discovery Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_RelatedProducts.
        /// Identifies related products by shared application number or active ingredient.
        /// </summary>
        /// <remarks>
        /// Useful for finding alternatives, generics, or similar drugs.
        /// Relationship types: SameApplicationNumber, SameActiveIngredient
        /// </remarks>
        /// <example>
        /// <code>
        /// var related = await db.Set&lt;LabelView.RelatedProducts&gt;()
        ///     .AsNoTracking()
        ///     .Where(r => r.SourceProductID == 123)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="Label.Ingredient"/>
        [Table("vw_RelatedProducts")]
        public class RelatedProducts
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Source product ID.
            /// </summary>
            public int? SourceProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Source product name.
            /// </summary>
            public string? SourceProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Source document GUID.
            /// </summary>
            public Guid? SourceDocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Related product ID.
            /// </summary>
            public int? RelatedProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Related product name.
            /// </summary>
            public string? RelatedProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Related document GUID.
            /// </summary>
            public Guid? RelatedDocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Relationship type (SameApplicationNumber, SameActiveIngredient).
            /// </summary>
            public string? RelationshipType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Shared value (application number or UNII).
            /// </summary>
            public string? SharedValue { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_APIEndpointGuide.
        /// Metadata view for AI-assisted API endpoint discovery.
        /// </summary>
        /// <remarks>
        /// Claude API queries this to understand available navigation views and usage patterns.
        /// Returns view names, descriptions, categories, and usage hints.
        /// </remarks>
        /// <example>
        /// <code>
        /// var endpoints = await db.Set&lt;LabelView.APIEndpointGuide&gt;()
        ///     .AsNoTracking()
        ///     .Where(e => e.Category == "Product Information")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        [Table("vw_APIEndpointGuide")]
        public class APIEndpointGuide
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// View name (primary key).
            /// </summary>
            
            public string? ViewName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Endpoint name (view name without vw_ prefix).
            /// </summary>
            public string? EndpointName { get; set; }

            /**************************************************************/
            /// <summary>
            /// View description.
            /// </summary>
            public string? Description { get; set; }

            /**************************************************************/
            /// <summary>
            /// Category (e.g., Product Information, Drug Safety).
            /// </summary>
            public string? Category { get; set; }

            /**************************************************************/
            /// <summary>
            /// Usage hint for querying.
            /// </summary>
            public string? UsageHint { get; set; }

            #endregion properties
        }

        #endregion Cross-Reference and Discovery Views
    }
}