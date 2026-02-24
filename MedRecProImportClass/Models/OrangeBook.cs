using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MedRecProImportClass.Models
{
    /*************************************************************/
    /// <summary>
    /// Container for all FDA Orange Book entity classes. The nested classes map to
    /// normalized tables sourced from the three Orange Book flat files: products.txt,
    /// patent.txt, and exclusivity.txt. Junction tables link Orange Book data to
    /// existing SPL label entities (MarketingCategory, IngredientSubstance, Organization).
    /// </summary>
    /// <remarks>
    /// Table names are specified via <see cref="TableAttribute"/> because the SQL table names
    /// include the "OrangeBook" prefix (e.g., "OrangeBookProduct") which differs from
    /// the nested class names (e.g., "Product"). The ApplicationDbContext reflection
    /// block for OrangeBook reads these attributes to set the correct table mapping.
    /// Source data uses tilde (~) as the field delimiter with a header row in each file.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="LabelView"/>
    public class OrangeBook
    {
        /*************************************************************/
        /// <summary>
        /// Lookup table for pharmaceutical companies that hold FDA application approvals.
        /// Sourced from the Applicant and Applicant_Full_Name columns in products.txt.
        /// </summary>
        /// <remarks>
        /// Each unique Applicant short name from the products file is stored once.
        /// The full legal name is also captured when available from Applicant_Full_Name.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="ApplicantOrganization"/>
        [Table("OrangeBookApplicant")]
        public class Applicant
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookApplicantID { get; set; }

            /*************************************************************/
            /// <summary>
            /// Short applicant name/code as published in the Orange Book (e.g., "TEVA", "SALIX").
            /// Maps to the Applicant column in products.txt.
            /// </summary>
            public string? ApplicantName { get; set; }

            /*************************************************************/
            /// <summary>
            /// Full legal name of the applicant company
            /// (e.g., "TEVA PHARMACEUTICALS USA INC").
            /// Maps to the Applicant_Full_Name column in products.txt.
            /// </summary>
            public string? ApplicantFullName { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Central fact table for FDA-approved drug products from products.txt.
        /// Each row represents one product number under one application number.
        /// The composite natural key is (ApplType, ApplNo, ProductNo).
        /// </summary>
        /// <remarks>
        /// The source DF;Route column is split into separate DosageForm and Route fields.
        /// Approval_Date values of "Approved Prior to Jan 1, 1982" are stored as NULL in
        /// ApprovalDate with ApprovalDateIsPremarket set to true. The Yes/No flags for
        /// RLD and RS are converted to boolean values during import.
        /// </remarks>
        /// <seealso cref="Applicant"/>
        /// <seealso cref="Patent"/>
        /// <seealso cref="Exclusivity"/>
        /// <seealso cref="ProductMarketingCategory"/>
        /// <seealso cref="ProductIngredientSubstance"/>
        [Table("OrangeBookProduct")]
        public class Product
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookProductID { get; set; }

            /*************************************************************/
            /// <summary>
            /// Application type: "N" for NDA (New Drug Application) or "A" for ANDA
            /// (Abbreviated New Drug Application). Part of the composite natural key.
            /// Maps to Appl_Type in products.txt.
            /// </summary>
            public string? ApplType { get; set; }

            /*************************************************************/
            /// <summary>
            /// FDA-assigned application number, zero-padded to 6 digits (e.g., "020610").
            /// Part of the composite natural key. Stored as varchar to preserve leading zeros.
            /// Maps to Appl_No in products.txt.
            /// </summary>
            public string? ApplNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// FDA-assigned product number within the application, always 3 digits (e.g., "001").
            /// Each strength/form is a separate product. Part of the composite natural key.
            /// Maps to Product_No in products.txt.
            /// </summary>
            public string? ProductNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// Active ingredient(s) as published. Multiple ingredients are semicolon-delimited
            /// in alphabetical order. Maps to the Ingredient column in products.txt.
            /// </summary>
            public string? Ingredient { get; set; }

            /*************************************************************/
            /// <summary>
            /// Pharmaceutical dosage form, extracted from the DF;Route source column
            /// (e.g., "TABLET", "AEROSOL, FOAM"). The source column is split on the
            /// semicolon to separate DosageForm from Route during import.
            /// </summary>
            public string? DosageForm { get; set; }

            /*************************************************************/
            /// <summary>
            /// Route of administration, extracted from the DF;Route source column
            /// (e.g., "ORAL", "TOPICAL", "RECTAL"). The source column is split on the
            /// semicolon to separate Route from DosageForm during import.
            /// </summary>
            public string? Route { get; set; }

            /*************************************************************/
            /// <summary>
            /// Proprietary/brand name of the drug product (e.g., "LIPITOR").
            /// Maps to the Trade_Name column in products.txt.
            /// </summary>
            public string? TradeName { get; set; }

            /*************************************************************/
            /// <summary>
            /// Potency of the active ingredient as published (e.g., "10MG", "2MG/ACTUATION").
            /// Maps to the Strength column in products.txt.
            /// </summary>
            public string? Strength { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Applicant"/>. Resolved during import by matching
            /// the Applicant short name. No foreign key constraint enforced in the database.
            /// </summary>
            public int? OrangeBookApplicantID { get; set; }

            /*************************************************************/
            /// <summary>
            /// Therapeutic Equivalence evaluation code (e.g., "AB", "AP", "BX").
            /// Indicates interchangeability with the reference listed drug.
            /// Maps to the TE_Code column in products.txt.
            /// </summary>
            public string? TECode { get; set; }

            /*************************************************************/
            /// <summary>
            /// Product type classification: "RX" (prescription), "OTC" (over-the-counter),
            /// or "DISCN" (discontinued). Maps to the Type column in products.txt.
            /// </summary>
            public string? Type { get; set; }

            /*************************************************************/
            /// <summary>
            /// FDA approval date. NULL when the product was approved prior to Jan 1, 1982
            /// (see <see cref="ApprovalDateIsPremarket"/>).
            /// Parsed from the Approval_Date column in products.txt.
            /// </summary>
            public DateTime? ApprovalDate { get; set; }

            /*************************************************************/
            /// <summary>
            /// Set to true when the source Approval_Date text equals
            /// "Approved Prior to Jan 1, 1982". Default false.
            /// </summary>
            /// <seealso cref="ApprovalDate"/>
            public bool? ApprovalDateIsPremarket { get; set; } = false;

            /*************************************************************/
            /// <summary>
            /// Reference Listed Drug flag. True if this product is an RLD approved under
            /// section 505(c) of the FD&amp;C Act. Converted from "Yes"/"No" in products.txt.
            /// </summary>
            public bool? IsRLD { get; set; } = false;

            /*************************************************************/
            /// <summary>
            /// Reference Standard flag. True if this product is the reference standard
            /// selected for bioequivalence studies in ANDA submissions.
            /// Converted from "Yes"/"No" in products.txt.
            /// </summary>
            public bool? IsRS { get; set; } = false;

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Patent records associated with Orange Book products. Sourced from patent.txt.
        /// One product can have multiple patents. The natural key columns (ApplType, ApplNo,
        /// ProductNo) are preserved for import matching to resolve OrangeBookProductID.
        /// </summary>
        /// <remarks>
        /// The source file uses "Y" or empty/null for the boolean flag columns
        /// (Drug_Substance_Flag, Drug_Product_Flag, Delist_Flag). Dates are in
        /// "MMM dd, yyyy" format (e.g., "Aug 24, 2026").
        /// </remarks>
        /// <seealso cref="Product"/>
        [Table("OrangeBookPatent")]
        public class Patent
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookPatentID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Product"/>. Resolved during import by matching
            /// ApplType + ApplNo + ProductNo. No foreign key constraint enforced.
            /// </summary>
            public int? OrangeBookProductID { get; set; }

            /*************************************************************/
            /// <summary>
            /// Application type, matches the parent product. Preserved for import matching.
            /// Maps to the Appl_Type column in patent.txt.
            /// </summary>
            public string? ApplType { get; set; }

            /*************************************************************/
            /// <summary>
            /// FDA application number, zero-padded. Preserved for import matching.
            /// Maps to the Appl_No column in patent.txt.
            /// </summary>
            public string? ApplNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// Product number within the application. Preserved for import matching.
            /// Maps to the Product_No column in patent.txt.
            /// </summary>
            public string? ProductNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// U.S. patent number. Typically 7–8 digits but may include an exclusivity code
            /// suffix such as *PED (pediatric), *ODE (orphan drug), *NCE (new chemical entity),
            /// *GAIN (antibiotic incentive), *PC (patent challenge), or *CGT (competitive generic).
            /// Example: "11931377*PED". VARCHAR(17) in SQL. Maps to Patent_No in patent.txt.
            /// </summary>
            public string? PatentNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// Patent expiration date including applicable extensions.
            /// Parsed from the Patent_Expire_Date_Text column in patent.txt.
            /// </summary>
            public DateTime? PatentExpireDate { get; set; }

            /*************************************************************/
            /// <summary>
            /// Code designating a use patent covering the approved indication
            /// (e.g., "U-141"). Maps to the Patent_Use_Code column in patent.txt.
            /// </summary>
            public string? PatentUseCode { get; set; }

            /*************************************************************/
            /// <summary>
            /// Indicates the patent covers the drug substance (active ingredient).
            /// Converted from "Y"/empty in the Drug_Substance_Flag column. Default false.
            /// </summary>
            public bool? DrugSubstanceFlag { get; set; } = false;

            /*************************************************************/
            /// <summary>
            /// Indicates the patent covers the drug product formulation.
            /// Converted from "Y"/empty in the Drug_Product_Flag column. Default false.
            /// </summary>
            public bool? DrugProductFlag { get; set; } = false;

            /*************************************************************/
            /// <summary>
            /// Sponsor has requested the patent be delisted per Section 505(j)(5)(D)(i)
            /// of the FD&amp;C Act. Converted from "Y"/empty in the Delist_Flag column.
            /// Default false.
            /// </summary>
            public bool? DelistFlag { get; set; } = false;

            /*************************************************************/
            /// <summary>
            /// Date the FDA received patent information from the NDA holder.
            /// Parsed from the Submission_Date column in patent.txt.
            /// </summary>
            public DateTime? SubmissionDate { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Marketing exclusivity records associated with Orange Book products.
        /// Sourced from exclusivity.txt. One product can have multiple exclusivity
        /// periods with different codes (e.g., NCE, ODE, RTO, M).
        /// </summary>
        /// <remarks>
        /// The natural key columns (ApplType, ApplNo, ProductNo) are preserved for
        /// import matching to resolve OrangeBookProductID. Exclusivity dates are in
        /// "MMM dd, yyyy" format in the source file.
        /// </remarks>
        /// <seealso cref="Product"/>
        [Table("OrangeBookExclusivity")]
        public class Exclusivity
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookExclusivityID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Product"/>. Resolved during import by matching
            /// ApplType + ApplNo + ProductNo. No foreign key constraint enforced.
            /// </summary>
            public int? OrangeBookProductID { get; set; }

            /*************************************************************/
            /// <summary>
            /// Application type, matches the parent product. Preserved for import matching.
            /// Maps to the Appl_Type column in exclusivity.txt.
            /// </summary>
            public string? ApplType { get; set; }

            /*************************************************************/
            /// <summary>
            /// FDA application number, zero-padded. Preserved for import matching.
            /// Maps to the Appl_No column in exclusivity.txt.
            /// </summary>
            public string? ApplNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// Product number within the application. Preserved for import matching.
            /// Maps to the Product_No column in exclusivity.txt.
            /// </summary>
            public string? ProductNo { get; set; }

            /*************************************************************/
            /// <summary>
            /// Type of marketing exclusivity granted.
            /// Common codes: "NCE" (New Chemical Entity), "ODE" (Orphan Drug),
            /// "RTO" (Rare Therapeutic Orphan), "M" (Method of Use).
            /// Maps to the Exclusivity_Code column in exclusivity.txt.
            /// </summary>
            public string? ExclusivityCode { get; set; }

            /*************************************************************/
            /// <summary>
            /// Expiration date of the exclusivity period.
            /// Parsed from the Exclusivity_Date column in exclusivity.txt.
            /// </summary>
            public DateTime? ExclusivityDate { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Junction table linking Orange Book products to SPL MarketingCategory records
        /// via application number. Enables cross-referencing Orange Book TE codes, patents,
        /// and exclusivities with full SPL label content.
        /// </summary>
        /// <seealso cref="Product"/>
        /// <seealso cref="Label.MarketingCategory"/>
        [Table("OrangeBookProductMarketingCategory")]
        public class ProductMarketingCategory
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookProductMarketingCategoryID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Product"/>. No foreign key constraint enforced.
            /// </summary>
            public int? OrangeBookProductID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References the existing MarketingCategory table (SPL data).
            /// No foreign key constraint enforced.
            /// </summary>
            /// <seealso cref="Label.MarketingCategory"/>
            public int? MarketingCategoryID { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Junction table linking Orange Book products to SPL IngredientSubstance records.
        /// Many-to-many: one OB product can contain multiple active ingredients, and one
        /// IngredientSubstance can appear in many OB products.
        /// </summary>
        /// <seealso cref="Product"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        [Table("OrangeBookProductIngredientSubstance")]
        public class ProductIngredientSubstance
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookProductIngredientSubstanceID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Product"/>. No foreign key constraint enforced.
            /// </summary>
            public int? OrangeBookProductID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References the existing IngredientSubstance table (SPL data).
            /// No foreign key constraint enforced.
            /// </summary>
            /// <seealso cref="Label.IngredientSubstance"/>
            public int? IngredientSubstanceID { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Junction table linking Orange Book applicant companies to SPL Organization records.
        /// Many-to-many: one OB applicant may map to multiple SPL organizations, and one
        /// Organization can match multiple OB applicant entries.
        /// </summary>
        /// <seealso cref="Applicant"/>
        /// <seealso cref="Label.Organization"/>
        [Table("OrangeBookApplicantOrganization")]
        public class ApplicantOrganization
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// Surrogate primary key (auto-increment IDENTITY).
            /// </summary>
            [Key]
            public int? OrangeBookApplicantOrganizationID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References <see cref="Applicant"/>. No foreign key constraint enforced.
            /// </summary>
            public int? OrangeBookApplicantID { get; set; }

            /*************************************************************/
            /// <summary>
            /// References the existing Organization table (SPL data).
            /// No foreign key constraint enforced.
            /// </summary>
            /// <seealso cref="Label.Organization"/>
            public int? OrganizationID { get; set; }

            #endregion properties
        }

        /*************************************************************/
        /// <summary>
        /// Lookup table for FDA Orange Book patent use code definitions.
        /// Maps each Patent_Use_Code value (e.g., "U-141") to its human-readable
        /// description of the approved therapeutic indication covered by the patent.
        /// </summary>
        /// <remarks>
        /// The FDA Orange Book ZIP file (patent.txt) contains use code values but NOT
        /// their definitions. Definitions are published separately on the FDA website
        /// and maintained as an embedded JSON resource in MedRecProImportClass.
        /// This lookup table is populated from that JSON during Orange Book import.
        ///
        /// Uses PatentUseCode as the natural primary key (no surrogate IDENTITY column).
        /// The class is named <c>PatentUseCodeDefinition</c> rather than <c>PatentUseCode</c>
        /// to avoid the C# "Color Color" ambiguity where the class name would collide
        /// with the primary key property name.
        ///
        /// The <see cref="Code"/> property maps to the <c>PatentUseCode</c> database column
        /// via <see cref="ColumnAttribute"/>.
        /// </remarks>
        /// <seealso cref="Patent"/>
        /// <seealso cref="Patent.PatentUseCode"/>
        [Table("OrangeBookPatentUseCode")]
        public class PatentUseCodeDefinition
        {
            #region properties

            /*************************************************************/
            /// <summary>
            /// The patent use code identifier (e.g., "U-1", "U-141", "U-4412").
            /// Serves as the natural primary key. Matches Patent_Use_Code values
            /// in patent.txt and the <see cref="Patent.PatentUseCode"/> column.
            /// </summary>
            /// <remarks>
            /// Mapped to the <c>PatentUseCode</c> column in the database via
            /// <see cref="ColumnAttribute"/>. Named <c>Code</c> in the C# model to
            /// avoid shadowing the enclosing <see cref="PatentUseCodeDefinition"/> type name.
            /// </remarks>
            [Key]
            [Column("PatentUseCode")]
            public string? Code { get; set; }

            /*************************************************************/
            /// <summary>
            /// Human-readable description of the approved indication or method of use
            /// covered by the patent (e.g., "PREVENTION OF PREGNANCY",
            /// "TREATMENT OF HYPERTENSION"). Sourced from the FDA Patent Use Codes
            /// and Definitions publication.
            /// </summary>
            public string? Definition { get; set; }

            #endregion properties
        }
    }
}
