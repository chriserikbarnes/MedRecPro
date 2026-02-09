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
        /// View entity for vw_IngredientActiveSummary.
        /// Aggregates active ingredient statistics across the document hierarchy.
        /// </summary>
        [Table("vw_IngredientActiveSummary")]
        public class IngredientActiveSummary
        {
            public int IngredientSubstanceID { get; set; }
            public string? UNII { get; set; }
            public string? SubstanceName { get; set; }
            public string? IngredientType { get; set; }
            public int ProductCount { get; set; }
            public int DocumentCount { get; set; }
            public int LabelerCount { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_IngredientInactiveSummary.
        /// Aggregates inactive ingredient statistics across the document hierarchy.
        /// </summary>
        [Table("vw_IngredientInactiveSummary")]
        public class IngredientInactiveSummary
        {
            public int IngredientSubstanceID { get; set; }
            public string? UNII { get; set; }
            public string? SubstanceName { get; set; }
            public string? IngredientType { get; set; }
            public int ProductCount { get; set; }
            public int DocumentCount { get; set; }
            public int LabelerCount { get; set; }
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

        /**************************************************************/
        /// <summary>
        /// View entity for vw_Ingredients.
        /// Flattened view of all ingredients (active and inactive) with product and document linkage.
        /// </summary>
        /// <remarks>
        /// Provides comprehensive ingredient search with:
        /// <list type="bullet">
        ///   <item><description>Application Number/Type filtering for regulatory context</description></item>
        ///   <item><description>DocumentGUID for direct label retrieval</description></item>
        ///   <item><description>Product name searching including misspellings via phonetic matching</description></item>
        ///   <item><description>ClassCode to distinguish active vs inactive ingredients</description></item>
        /// </list>
        /// Use this view when searching across all ingredient types.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for all ingredients with a specific UNII
        /// var ingredients = await db.Set&lt;LabelView.IngredientView&gt;()
        ///     .AsNoTracking()
        ///     .Where(i => i.UNII == "R16CO5Y76E")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="ActiveIngredientView"/>
        /// <seealso cref="InactiveIngredientView"/>
        [Table("vw_Ingredients")]
        public class IngredientView
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Document GUID for linking to label retrieval endpoints.
            /// Use with /api/label/generate/{documentGuid} or /api/label/single/{documentGuid}.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID for version history navigation.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section GUID containing the ingredient.
            /// </summary>
            public Guid? SectionGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient ID (primary key).
            /// </summary>
            public int? IngredientID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID containing this ingredient.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID linking to substance details.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category ID for regulatory context.
            /// </summary>
            public int? MarketingCategoryID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section ID within the document.
            /// </summary>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID (internal).
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient class code.
            /// IACT = inactive ingredient; other values indicate active ingredients.
            /// </summary>
            public string? ClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name containing this ingredient.
            /// Supports partial/phonetic matching for misspelling tolerance.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Substance name of the ingredient.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// FDA UNII code for unique ingredient identification.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application type (NDA, ANDA, BLA, etc.) from marketing category.
            /// </summary>
            public string? ApplicationType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number (numeric portion) for regulatory lookup.
            /// </summary>
            public string? ApplicationNumber { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ActiveIngredients.
        /// Flattened view of active ingredients only (ClassCode != 'IACT').
        /// </summary>
        /// <remarks>
        /// Use this view when specifically searching for active ingredients.
        /// Provides the same properties as <see cref="IngredientView"/> but filtered
        /// to exclude inactive ingredients (excipients).
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find all active ingredients for a specific application number
        /// var actives = await db.Set&lt;LabelView.ActiveIngredientView&gt;()
        ///     .AsNoTracking()
        ///     .Where(i => i.ApplicationNumber == "020702")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="IngredientView"/>
        /// <seealso cref="InactiveIngredientView"/>
        /// <seealso cref="Label.Ingredient"/>
        [Table("vw_ActiveIngredients")]
        public class ActiveIngredientView
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Document GUID for linking to label retrieval endpoints.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID for version history navigation.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section GUID containing the ingredient.
            /// </summary>
            public Guid? SectionGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient ID (primary key).
            /// </summary>
            public int? IngredientID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID containing this ingredient.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID linking to substance details.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category ID for regulatory context.
            /// </summary>
            public int? MarketingCategoryID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section ID within the document.
            /// </summary>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID (internal).
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient class code (will not be 'IACT' in this view).
            /// </summary>
            public string? ClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name containing this ingredient.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Substance name of the ingredient.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// FDA UNII code for unique ingredient identification.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application type (NDA, ANDA, BLA, etc.) from marketing category.
            /// </summary>
            public string? ApplicationType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number (numeric portion) for regulatory lookup.
            /// </summary>
            public string? ApplicationNumber { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_InactiveIngredients.
        /// Flattened view of inactive ingredients only (ClassCode = 'IACT').
        /// </summary>
        /// <remarks>
        /// Use this view when specifically searching for inactive ingredients (excipients).
        /// Provides the same properties as <see cref="IngredientView"/> but filtered
        /// to include only inactive ingredients.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find all inactive ingredients for a specific product
        /// var inactives = await db.Set&lt;LabelView.InactiveIngredientView&gt;()
        ///     .AsNoTracking()
        ///     .Where(i => i.ProductName.Contains("TYLENOL"))
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="IngredientView"/>
        /// <seealso cref="ActiveIngredientView"/>
        /// <seealso cref="Label.Ingredient"/>
        [Table("vw_InactiveIngredients")]
        public class InactiveIngredientView
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Document GUID for linking to label retrieval endpoints.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID for version history navigation.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section GUID containing the ingredient.
            /// </summary>
            public Guid? SectionGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient ID (primary key).
            /// </summary>
            public int? IngredientID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product ID containing this ingredient.
            /// </summary>
            public int? ProductID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient substance ID linking to substance details.
            /// </summary>
            public int? IngredientSubstanceID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Marketing category ID for regulatory context.
            /// </summary>
            public int? MarketingCategoryID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Section ID within the document.
            /// </summary>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Document ID (internal).
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Ingredient class code (will always be 'IACT' in this view).
            /// </summary>
            public string? ClassCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Product name containing this ingredient.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Substance name of the ingredient.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// FDA UNII code for unique ingredient identification.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application type (NDA, ANDA, BLA, etc.) from marketing category.
            /// </summary>
            public string? ApplicationType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Application number (numeric portion) for regulatory lookup.
            /// </summary>
            public string? ApplicationNumber { get; set; }

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

        /**************************************************************/
        /// <summary>
        /// View entity for vw_SectionContent.
        /// Provides section text content for AI summarization workflows.
        /// Returns flattened section content with document context for quick text retrieval.
        /// </summary>
        /// <remarks>
        /// Designed for efficient section content retrieval supporting:
        /// - AI-powered summarization of drug label sections
        /// - Quick text extraction by DocumentGUID and optional SectionGUID/SectionCode
        /// - Content aggregation for multi-section analysis
        /// 
        /// Filters out null/empty content (ContentText must be > 3 characters).
        /// Indexes Used: IX_Section_DocumentID, IX_SectionTextContent_SectionID
        /// </remarks>
        /// <example>
        /// <code>
        /// var sections = await db.Set&lt;LabelView.SectionContent&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.DocumentGUID == documentGuid)
        ///     .OrderBy(s => s.SectionCode)
        ///     .ThenBy(s => s.SequenceNumber)
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.SectionTextContent"/>
        [Table("tmp_SectionContent")]
        public class SectionContent
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Primary key for navigation to Document entity.
            /// </summary>
            public int? DocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Primary key for navigation to Section entity.
            /// </summary>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique identifier for the document version.
            /// Use this to retrieve specific document content.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID (constant across document versions).
            /// Use this to track all versions of a document.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique identifier for the section.
            /// Use this to retrieve specific section content.
            /// </summary>
            public Guid? SectionGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Version number of the document.
            /// Higher numbers indicate more recent versions.
            /// </summary>
            public int? VersionNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable display name for the document.
            /// </summary>
            public string? DocumentDisplayName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Title of the document.
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// LOINC code identifying the section type (e.g., 34084-4 for Adverse Reactions).
            /// </summary>
            public string? SectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable display name for the section.
            /// </summary>
            public string? SectionDisplayName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Title of the section as specified in the SPL document.
            /// </summary>
            public string? SectionTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// The actual text content of the section.
            /// Filtered to exclude null/empty content (> 3 characters).
            /// </summary>
            public string? ContentText { get; set; }

            /**************************************************************/
            /// <summary>
            /// Sequence number for ordering content within a section.
            /// Use this to maintain proper content order.
            /// </summary>
            public int? SequenceNumber { get; set; }

            /**************************************************************/
            /// <summary>
            /// Content type indicator (e.g., "text", "paragraph", "list").
            /// </summary>
            public string? ContentType { get; set; }

            /**************************************************************/
            /// <summary>
            /// Code system for the section code (typically LOINC OID).
            /// </summary>
            public string? SectionCodeSystem { get; set; }

            #endregion
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

        #region Latest Label Navigation Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductLatestLabel.
        /// Returns the single most recent label (document) for each UNII/ProductName combination.
        /// </summary>
        /// <remarks>
        /// Uses ROW_NUMBER() partitioned by UNII and ProductName, ordered by EffectiveTime DESC.
        /// Only returns active ingredients (excludes IACT class).
        /// Indexes Used: IX_Document_EffectiveTime_LatestLabel, IX_Ingredient_IngredientSubstanceID, IX_IngredientSubstance_UNII
        /// </remarks>
        /// <example>
        /// <code>
        /// var latestLabels = await db.Set&lt;LabelView.ProductLatestLabel&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.UNII == "R16CO5Y76E")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="IngredientActiveSummary"/>
        /// <seealso cref="ProductsByIngredient"/>
        [Table("vw_ProductLatestLabel")]
        public class ProductLatestLabel
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Proprietary product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active ingredient substance name.
            /// </summary>
            public string? ActiveIngredient { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code for the active ingredient.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique document identifier for the latest label.
            /// Use this to retrieve the complete label via /api/label/single/{documentGuid}.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            #endregion properties
        }

        /**************************************************************/
        /// <summary>
        /// View entity for vw_ProductIndications.
        /// Returns product indication text combined with active ingredients.
        /// </summary>
        /// <remarks>
        /// Filters to INDICATION sections only and excludes inactive ingredients (IACT).
        /// Combines ContentText and ItemText into a single ContentText column.
        /// Related tables: vw_SectionNavigation, vw_Ingredients, SectionTextContent, TextList, TextListItem
        /// </remarks>
        /// <example>
        /// <code>
        /// var indications = await db.Set&lt;LabelView.ProductIndications&gt;()
        ///     .AsNoTracking()
        ///     .Where(p => p.UNII == "R16CO5Y76E")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.SectionTextContent"/>
        /// <seealso cref="SectionNavigation"/>
        /// <seealso cref="IngredientView"/>
        [Table("vw_ProductIndications")]
        public class ProductIndications
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Proprietary product name.
            /// </summary>
            public string? ProductName { get; set; }

            /**************************************************************/
            /// <summary>
            /// Active ingredient substance name.
            /// </summary>
            public string? SubstanceName { get; set; }

            /**************************************************************/
            /// <summary>
            /// UNII code for the active ingredient.
            /// </summary>
            public string? UNII { get; set; }

            /**************************************************************/
            /// <summary>
            /// Globally unique document identifier.
            /// Use this to retrieve the complete label via /api/label/single/{documentGuid}.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Combined indication text content from SectionTextContent and TextListItem.
            /// Contains the clinical indication information for the product.
            /// </summary>
            public string? ContentText { get; set; }

            #endregion properties
        }

        #endregion Latest Label Navigation Views

        #region Section Markdown Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_LabelSectionMarkdown.
        /// Provides aggregated, markdown-formatted section text for LLM/API consumption.
        /// </summary>
        /// <remarks>
        /// This view addresses the need for complete, contiguous section text that can
        /// be efficiently consumed by AI/LLM APIs for label summarization. The view:
        /// <list type="bullet">
        ///   <item><description>Aggregates all ContentText rows for each section using STRING_AGG</description></item>
        ///   <item><description>Converts HTML-style SPL content tags to markdown formatting</description></item>
        ///   <item><description>Prepends section title as markdown header (## SectionTitle)</description></item>
        ///   <item><description>Provides ContentBlockCount for diagnostics</description></item>
        /// </list>
        ///
        /// Markdown Conversion:
        /// - bold tags → **text**
        /// - italics tags → *text*
        /// - underline tags → _text_
        ///
        /// Indexes Used: IX_Section_DocumentID, IX_SectionTextContent_SectionID
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all sections for a specific document
        /// var sections = await db.Set&lt;LabelView.LabelSectionMarkdown&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.DocumentGUID == documentGuid)
        ///     .OrderBy(s => s.SectionCode)
        ///     .ToListAsync();
        ///
        /// // Combine all sections for complete document markdown
        /// var fullMarkdown = string.Join("\n\n", sections.Select(s => s.FullSectionText));
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.SectionTextContent"/>
        /// <seealso cref="SectionContent"/>
        /// <seealso cref="SectionNavigation"/>
        [Table("tmp_LabelSectionMarkdown")]
        public class LabelSectionMarkdown
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Globally unique identifier for the document version.
            /// Use this to retrieve the complete label via /api/label/single/{documentGuid}.
            /// </summary>
            public Guid? DocumentGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Set GUID (constant across document versions).
            /// Use this to track all versions of a document.
            /// </summary>
            public Guid? SetGUID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Title of the document (e.g., "LIPITOR- atorvastatin calcium tablet").
            /// </summary>
            public string? DocumentTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// LOINC section code identifying the section type (e.g., "34084-4" for Adverse Reactions).
            /// May be NULL for some sections.
            /// </summary>
            public string? SectionCode { get; set; }

            /**************************************************************/
            /// <summary>
            /// Human-readable section title (e.g., "ADVERSE REACTIONS", "INDICATIONS AND USAGE").
            /// </summary>
            public string? SectionTitle { get; set; }

            /**************************************************************/
            /// <summary>
            /// Computed unique key combining DocumentGUID, SectionCode, and SectionTitle.
            /// Format: {DocumentGUID}|{SectionCode or 'NULL'}|{SectionTitle}
            /// Used for grouping and identification.
            /// </summary>
            public string? SectionKey { get; set; }

            /**************************************************************/
            /// <summary>
            /// Complete markdown-formatted section text with header.
            /// Includes "## SectionTitle" header followed by aggregated, markdown-converted content.
            /// Ready for direct consumption by LLM APIs.
            /// </summary>
            public string? FullSectionText { get; set; }

            /**************************************************************/
            /// <summary>
            /// Number of content blocks aggregated into this section.
            /// Useful for diagnostics and understanding section complexity.
            /// </summary>
            public int? ContentBlockCount { get; set; }

            #endregion properties
        }

        #endregion Section Markdown Views

        #region Inventory Summary Views

        /**************************************************************/
        /// <summary>
        /// View entity for vw_InventorySummary.
        /// Comprehensive inventory summary providing aggregated counts across multiple dimensions
        /// for answering questions about what products are available in the database.
        /// </summary>
        /// <remarks>
        /// Provides counts across dimensions: Documents, Products, Labelers, Active Ingredients,
        /// Pharmacologic Classes, NDCs, Marketing Categories, Dosage Forms, Top Labelers,
        /// Top Pharmacologic Classes, and Top Ingredients.
        ///
        /// Categories include:
        /// - TOTALS: High-level entity counts
        /// - BY_MARKETING_CATEGORY: Products by marketing category (NDA, ANDA, BLA, etc.)
        /// - BY_DOSAGE_FORM: Products by dosage form (top 15)
        /// - TOP_LABELERS: Top 10 labelers by product count
        /// - TOP_PHARM_CLASSES: Top 10 pharmacologic classes by product count
        /// - TOP_INGREDIENTS: Top 10 active ingredients by product count
        ///
        /// Target: ~50 rows covering all major database dimensions.
        /// Use this view to provide accurate, comprehensive inventory answers.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all inventory summaries
        /// var summary = await db.Set&lt;LabelView.InventorySummary&gt;()
        ///     .AsNoTracking()
        ///     .OrderBy(s => s.SortOrder)
        ///     .ToListAsync();
        ///
        /// // Get totals only
        /// var totals = await db.Set&lt;LabelView.InventorySummary&gt;()
        ///     .AsNoTracking()
        ///     .Where(s => s.Category == "TOTALS")
        ///     .ToListAsync();
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="Label.PharmacologicClass"/>
        [Table("tmp_InventorySummary")]
        public class InventorySummary
        {
            #region properties

            /**************************************************************/
            /// <summary>
            /// Grouping category for the summary row (e.g., TOTALS, BY_MARKETING_CATEGORY, TOP_LABELERS).
            /// </summary>
            public string? Category { get; set; }

            /**************************************************************/
            /// <summary>
            /// Dimension being measured (e.g., Documents, Products, Marketing Category, Labeler).
            /// </summary>
            public string? Dimension { get; set; }

            /**************************************************************/
            /// <summary>
            /// Specific value within the dimension (e.g., "NDA" for marketing category, labeler name).
            /// Null for TOTALS category where dimension itself is the entity type.
            /// </summary>
            public string? DimensionValue { get; set; }

            /**************************************************************/
            /// <summary>
            /// Count of items in this dimension/value combination.
            /// </summary>
            public int? ItemCount { get; set; }

            /**************************************************************/
            /// <summary>
            /// Sort order for display purposes. Lower numbers appear first.
            /// Grouped by category: TOTALS (1-10), BY_MARKETING_CATEGORY (100+),
            /// BY_DOSAGE_FORM (200+), TOP_LABELERS (300+), TOP_PHARM_CLASSES (400+), TOP_INGREDIENTS (500+).
            /// </summary>
            public int? SortOrder { get; set; }

            #endregion properties
        }

        #endregion Inventory Summary Views
    }
}