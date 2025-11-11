

﻿using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Helpers;
using MedRecPro.Models;
using static MedRecPro.Models.Label;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses product identity elements, including identifiers (NDC, GTIN), specialized kinds (product categories),
    /// and equivalency relationships from an SPL document.
    /// </summary>
    /// <remarks>
    /// This parser is responsible for the core identification and classification of a product. It is called
    /// by other parsers (like ManufacturedProductParser) to handle a specific subset of the product data.
    /// It relies on the SplParseContext to have the CurrentProduct already established.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="ProductIdentifier"/>
    /// <seealso cref="SpecializedKind"/>
    /// <seealso cref="EquivalentEntity"/>
    /// <seealso cref="SplParseContext"/>
    public class ProductIdentityParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "productidentity";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an XML element (typically manufacturedProduct) to extract and save product identity information.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of three key areas:
        /// 1. Equivalent Entities - Links to other equivalent products.
        /// 2. Product Identifiers - NDC, GTIN, UDI, and other codes.
        /// 3. Specialized Kinds - Product classification and categories.
        /// It assumes that `context.CurrentProduct` has been set by the calling parser.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that a product is available in the context to link entities to.
            if (context?.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse product identity because no product context exists.");
                context?.Logger?.LogError("ProductIdentityParser was called without a valid product in the context.");
                return result;
            }
            var product = context.CurrentProduct;

            if (context.UseBulkOperations)
            {
                // Bulk operation mode - process all product identity elements in one go
                result = await parseProductIdentityBulk(element, product, context, reportProgress);
            }
            else
            {
                // Non-bulk mode - process each product identity element individually
                // --- PARSE EQUIVALENT ENTITIES ---
                var equivCount = await parseAndSaveEquivalentEntitiesAsync(element, product, context);
                result.ProductElementsCreated += equivCount;

                // --- PARSE IDENTIFIER ENTITIES ---
                var idResult = await parseAndSaveProductIdentifiersAsync(element, product, context, reportProgress);
                result.MergeFrom(idResult);

                // --- PARSE SPECIALIZED KINDS ---
                // The document type code is needed for business rule validation.
                var kindCount = await parseAndSaveSpecializedKindsAsync(element, product, context, context.Document?.DocumentCode);
                result.ProductElementsCreated += kindCount;
            }

            return result;
            #endregion
        }

        #region Product Parsing - Individual Operations (N + 1)
        /**************************************************************/
        /// <summary>
        /// Parses and creates EquivalentEntity records from the manufacturedProduct XML element.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="product">The Product entity to link equivalence relationships to.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of EquivalentEntity records created.</returns>
        /// <remarks>
        /// Extracts each 'asEquivalentEntity' under the element. Grabs code/codeSystem for the equivalence,
        /// and definingMaterialKind/code/codeSystem for the referenced equivalent product.
        /// </remarks>
        /// <seealso cref="EquivalentEntity"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveEquivalentEntitiesAsync(XElement mmEl, Product product, SplParseContext context)
        {
            #region implementation
            int createdCount = 0;
            var repo = context.GetRepository<EquivalentEntity>();

            // Find all <asEquivalentEntity> descendants (may be more than one)
            foreach (var equivEl in mmEl.Descendants(ns + sc.E.AsEquivalentEntity))
            {
                // Only process if classCode is "EQUIV" or not specified
                var classCode = equivEl.GetAttrVal("classCode");
                if (!string.IsNullOrEmpty(classCode) && !string.Equals(classCode, "EQUIV", StringComparison.OrdinalIgnoreCase))
                    continue;

                // --- Equivalence code and system from <code> child ---
                var codeEl = equivEl.GetSplElement(sc.E.Code);
                string? equivalenceCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? equivalenceCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

                // --- Defining material kind (referenced product) ---
                var definingMatEl = equivEl.GetSplElement(sc.E.DefiningMaterialKind);
                var definingMatCodeEl = definingMatEl?.GetSplElement(sc.E.Code);
                string? definingMaterialKindCode = definingMatCodeEl?.GetAttrVal(sc.A.CodeValue);
                string? definingMaterialKindSystem = definingMatCodeEl?.GetAttrVal(sc.A.CodeSystem);

                // If nothing meaningful, skip
                if (string.IsNullOrWhiteSpace(equivalenceCode) &&
                    string.IsNullOrWhiteSpace(definingMaterialKindCode))
                    continue;

                var entity = new EquivalentEntity
                {
                    ProductID = product.ProductID,
                    EquivalenceCode = equivalenceCode,
                    EquivalenceCodeSystem = equivalenceCodeSystem,
                    DefiningMaterialKindCode = definingMaterialKindCode,
                    DefiningMaterialKindSystem = definingMaterialKindSystem
                };

                await repo.CreateAsync(entity);
                createdCount++;
                context?.Logger?.LogInformation($"Created EquivalentEntity for ProductID {product.ProductID}: EquivCode={equivalenceCode}, DefMatCode={definingMaterialKindCode}");
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates the parsing and creation of all ProductIdentifier entities.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="product">The Product entity to link identifiers to.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>An SplParseResult containing the results of the operations.</returns>
        /// <remarks>
        /// This method first consolidates all relevant code elements (direct, packaging, and approval)
        /// and then processes them in a single, unified function to create identifiers and,
        /// conditionally, certification links.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseAndSaveProductIdentifiersAsync(XElement mmEl, Product product, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // Step 1: Consolidate all relevant identifier code elements into one list.
            var allIdentifierElements = getAllRelevantIdentifierElements(mmEl);

            // Step 2: Process the consolidated list to create identifiers and links.
            var result = await createIdentifiersAndLinksAsync(allIdentifierElements, product, context, reportProgress);

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gathers all relevant product identifier `code` elements from direct, packaging, and approval locations.
        /// </summary>
        /// <param name="element">The root XML element (e.g., manufacturedProduct) to search within.</param>
        /// <returns>A distinct collection of all `code` XElements that represent product identifiers.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        private IEnumerable<XElement> getAllRelevantIdentifierElements(XElement element)
        {
            #region implementation
            // 1. Get product-level and nested packaging codes
            var productAndPackageCodes = getAllProductAndPackagingCodes(element);

            // 2. Get approval/marketing codes
            var approvalCodes = element.SplElements(sc.E.SubjectOf, sc.E.Approval, sc.E.Code);

            // 3. Combine and return a distinct list to avoid processing the same element twice
            return productAndPackageCodes.Union(approvalCodes);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and saves ProductIdentifier entities from a collection of code XML elements,
        /// and conditionally creates CertificationProductLink records.
        /// </summary>
        /// <param name="codeElements">The consolidated collection of all identifier XML code elements.</param>
        /// <param name="product">The Product entity to associate identifiers with.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>An SplParseResult containing the results of the operations.</returns>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="CertificationProductLinkParser"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> createIdentifiersAndLinksAsync(IEnumerable<XElement> codeElements,
            Product product, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            if (context == null || context.CurrentSection == null)
            {
                result.Success = false;
                result.Errors.Add("No parsing context provided for ProductIdentifier creation.");
                return result;
            }

            var repo = context.GetRepository<ProductIdentifier>();

            foreach (var codeEl in codeElements)
            {
                string? codeVal = codeEl.GetAttrVal(sc.A.CodeValue);
                string? codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(codeVal) || string.IsNullOrWhiteSpace(codeSystem))
                    continue;

                var identifier = new ProductIdentifier
                {
                    ProductID = product.ProductID,
                    IdentifierValue = codeVal,
                    IdentifierSystemOID = codeSystem,
                    IdentifierType = inferIdentifierType(codeSystem)
                };

                await repo.CreateAsync(identifier);
                result.ProductElementsCreated++;
                context?.Logger?.LogInformation($"ProductIdentifier created: ProductID={product.ProductID} Value={codeVal} OID={codeSystem}");

                // If this is a certification document and the identifier was successfully saved, create the link.
                // The section code "BNCC" is an example for "Blanket No Changes Certification".
                if (context?.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE && identifier.ProductIdentifierID.HasValue)
                {
                    var oldIdentifier = context.CurrentProductIdentifier;
                    context.CurrentProductIdentifier = identifier; // Set context for the child parser
                    try
                    {
                        var certLinkParser = new CertificationProductLinkParser();
                        var certLinkResult = await certLinkParser.ParseAsync(codeEl, context, reportProgress);
                        result.MergeFrom(certLinkResult);
                    }
                    finally
                    {
                        context.CurrentProductIdentifier = oldIdentifier; // Restore context
                    }
                }
            }
            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively retrieves all product and packaging code elements from the XML hierarchy.
        /// </summary>
        /// <param name="element">The XML element to search for code elements.</param>
        /// <returns>An enumerable of XElement objects representing code elements found at all levels.</returns>
        /// <remarks>
        /// Searches for codes at multiple levels:
        /// 1. Direct product-level codes under manufacturedProduct
        /// 2. Packaging codes in nested containerPackagedProduct elements
        /// 3. Codes in nested manufacturedProduct elements
        /// </remarks>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        private IEnumerable<XElement> getAllProductAndPackagingCodes(XElement element)
        {
            #region implementation
            // 1. Product-level codes: <code> directly under <manufacturedProduct>
            foreach (var code in element.SplElements(sc.E.Code))
                yield return code;

            // 2. Recurse into nested packaging: <asContent>/<containerPackagedProduct>/<code>
            foreach (var container in element.Descendants(ns + sc.E.ContainerPackagedProduct))
            {
                foreach (var code in container.SplElements(sc.E.Code))
                    yield return code;
            }

            // 3. Recurse into any nested <manufacturedProduct>
            foreach (var nested in element.SplElements(sc.E.ManufacturedProduct))
            {
                foreach (var code in getAllProductAndPackagingCodes(nested))
                    yield return code;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers the identifier type based on the OID (Object Identifier) of the code system.
        /// </summary>
        /// <param name="oid">The OID string representing the code system.</param>
        /// <returns>A human-readable identifier type string, or null if the OID is not recognized.</returns>
        /// <remarks>
        /// Maps standard healthcare OIDs to their corresponding identifier types including NDC, GTIN, UDI, etc.
        /// Supports drugs, biologics, devices, and cosmetics identifier systems.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private static string? inferIdentifierType(string? oid)
        {
            #region implementation
            // Extend with additional mappings as needed
            return oid switch
            {
                // Drug, Biologic, and Device Item Codes
                "2.16.840.1.113883.6.69" => "NDC",          // National Drug Code
                "2.16.840.1.113883.6.96" => "UNII",         // Unique Ingredient Identifier
                "2.16.840.1.113883.6.43.1" => "SNOMED CT",  // SNOMED Clinical Terms
                "2.16.840.1.113883.3.26.1.1" => "FDA",      // FDA Structured Product Labeling (for drug approval, etc.)
                "2.16.840.1.113883.3.26.1.5" => "RxNorm",   // RxNorm
                "2.16.840.1.113883.6.1" => "LOINC",         // LOINC codes
                "2.16.840.1.113883.6.278" => "RxCUI",       // RxNorm Concept Unique Identifier

                // Device Identifiers
                "1.3.160" => "GS1",                         // GS1 Global Trade Item Number (GTIN)
                "2.16.840.1.113883.6.40" => "HIBCC",        // Health Industry Barcode Council
                "2.16.840.1.113883.6.18" => "ISBT 128",     // International Society of Blood Transfusion
                "2.16.840.1.113883.6.301.5" => "UDI",       // Unique Device Identifier (used by FDA for UDI)

                // Cosmetics
                "2.16.840.1.113883.3.9848" => "CLN",        // Cosmetic Listing Number

                // Special Purpose
                "2.16.840.1.113883.6.3" => "ICD-9-CM",      // ICD-9-CM
                "2.16.840.1.113883.6.4" => "ICD-10",        // ICD-10
                "2.16.840.1.113883.6.42" => "UCUM",         // Unified Code for Units of Measure

                // FDA SPL implementation (additional/fallback OIDs)
                "2.16.840.1.113883.3.26.1.2" => "FDA Substance",
                "2.16.840.1.113883.3.26.1.3" => "FDA Route",
                "2.16.840.1.113883.3.26.1.4" => "FDA Dose Form",
                "2.16.840.1.113883.3.26.1.6" => "FDA Packaging",
                "2.16.840.1.113883.3.26.1.7" => "FDA Pharmaceutical",
                "2.16.840.1.113883.3.26.1.8" => "FDA Status",

                _ => null
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses, validates, and saves SpecializedKind entities from the manufacturedProduct XML element.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct XML element containing specialized kind information.</param>
        /// <param name="product">The Product entity to associate specialized kinds with.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <param name="documentTypeCode">The document type code used for business rule validation.</param>
        /// <returns>The number of SpecializedKind records successfully created after validation.</returns>
        /// <remarks>
        /// This method performs a three-step process:
        /// 1. Parses all SpecializedKind entities from XML without saving
        /// 2. Validates them against cosmetic category mutual exclusion business rules
        /// 3. Saves only the validated entities to the database
        ///
        /// Uses SpecializedKindValidator to enforce SPL Implementation Guide 3.4.3 rules.
        /// </remarks>
        /// <seealso cref="SpecializedKind"/>
        /// <seealso cref="SpecializedKindValidatorService"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveSpecializedKindsAsync(
        XElement mmEl,
        Product product,
        SplParseContext context,
        string? documentTypeCode)
        {
            #region implementation
            if (mmEl == null
                || context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return 0;
            }

            var repo = context.GetRepository<SpecializedKind>();
            var allKinds = new List<SpecializedKind>();

            // Step 1: Parse ALL SpecializedKind entities, but don't save yet
            foreach (var asSpecializedKindEl in mmEl.SplElements(sc.E.AsSpecializedKind))
            {
                // Navigate to the code element within the specialized kind structure
                var codeEl = asSpecializedKindEl
                    .GetSplElement(sc.E.GeneralizedMaterialKind)?
                    .GetSplElement(sc.E.Code);

                if (codeEl == null)
                {
                    context?.Logger?.LogWarning("No <code> found under <generalizedMaterialKind> in <asSpecializedKind>; skipping.");
                    continue;
                }

                // Extract and clean the code attributes
                var kindCode = codeEl.GetAttrVal(sc.A.CodeValue)?.Trim();
                var kindCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem)?.Trim();
                var kindDisplayName = codeEl.GetAttrVal(sc.A.DisplayName)?.Trim();

                // Validate required fields are present
                if (string.IsNullOrWhiteSpace(kindCode) || string.IsNullOrWhiteSpace(kindCodeSystem))
                {
                    context?.Logger?.LogWarning("Missing code or codeSystem in <asSpecializedKind>/<code>; skipping.");
                    continue;
                }

                // Create the specialized kind entity (not saved yet)
                allKinds.Add(new SpecializedKind
                {
                    ProductID = product.ProductID,
                    KindCode = kindCode,
                    KindCodeSystem = kindCodeSystem,
                    KindDisplayName = kindDisplayName
                });
            }

            // Step 2: Validate with business rules for mutually exclusive cosmetic codes
            var validatedKinds = SpecializedKindValidatorService.ValidateCosmeticCategoryRules(
                allKinds,
                documentTypeCode,
                context.Logger,
                out var rejectedKinds
            );


            // Step 3: Save validated entities to the database
            int createdCount = 0;
            foreach (var kind in validatedKinds)
            {
                await repo.CreateAsync(kind);
                createdCount++;
                context.Logger.LogInformation(
                    $"Created SpecializedKind: code={kind.KindCode}, codeSystem={kind.KindCodeSystem}, displayName={kind.KindDisplayName} for ProductID {product.ProductID}"
                );
            }

            return createdCount;
            #endregion
        }
        #endregion

        #region Product Parsing - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Data transfer object for EquivalentEntity during bulk operations.
        /// </summary>
        /// <seealso cref="EquivalentEntity"/>
        /// <seealso cref="Label"/>
        private class EquivalentEntityDto
        {
            #region implementation
            public string? EquivalenceCode { get; set; }
            public string? EquivalenceCodeSystem { get; set; }
            public string? DefiningMaterialKindCode { get; set; }
            public string? DefiningMaterialKindSystem { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object for ProductIdentifier during bulk operations.
        /// </summary>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private class ProductIdentifierDto
        {
            #region implementation
            public string? IdentifierType { get; set; }
            public string? IdentifierValue { get; set; }
            public string? IdentifierSystemOID { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object for SpecializedKind during bulk operations.
        /// </summary>
        /// <seealso cref="SpecializedKind"/>
        /// <seealso cref="Label"/>
        private class SpecializedKindDto
        {
            #region implementation
            public string? KindCode { get; set; }
            public string? KindCodeSystem { get; set; }
            public string? KindDisplayName { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all product identity elements using bulk operations for optimal performance.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="product">The Product entity to associate identities with.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: (3 entity types × 2) × N items × 45ms = ~270ms per item on Azure
        /// - After: (3 entity types × 2) × 45ms = ~270ms total
        /// Parses all three identity types (EquivalentEntity, ProductIdentifier, SpecializedKind) in bulk.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseProductIdentityBulk(
            XElement element,
            Product product,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            if (context?.ServiceProvider == null || product?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Invalid context or product for bulk operations.");
                return result;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Process Equivalent Entities
            var equivCount = await bulkCreateEquivalentEntitiesAsync(element, product.ProductID.Value, dbContext, context);
            result.ProductElementsCreated += equivCount;

            // Process Product Identifiers
            var idCount = await bulkCreateProductIdentifiersAsync(element, product.ProductID.Value, dbContext, context, reportProgress);
            result.ProductElementsCreated += idCount;

            // Process Specialized Kinds
            var kindCount = await bulkCreateSpecializedKindsAsync(
                element,
                product.ProductID.Value,
                dbContext,
                context,
                context.Document?.DocumentCode);
            result.ProductElementsCreated += kindCount;

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates EquivalentEntity records using bulk operations pattern.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="productId">The ID of the Product entity to link equivalence relationships to.</param>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>The number of EquivalentEntity records created.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: N database calls (one per entity)
        /// - After: 2 database calls (one query + one insert)
        /// Collects all equivalent entities into memory, deduplicates against existing entities,
        /// then performs batch insert for optimal performance.
        /// </remarks>
        /// <seealso cref="EquivalentEntity"/>
        /// <seealso cref="EquivalentEntityDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateEquivalentEntitiesAsync(
            XElement mmEl,
            int productId,
            ApplicationDbContext dbContext,
            SplParseContext context)
        {
            #region implementation

            var dtos = new List<EquivalentEntityDto>();

            // Find all <asEquivalentEntity> descendants
            foreach (var equivEl in mmEl.Descendants(ns + sc.E.AsEquivalentEntity))
            {
                // Only process if classCode is "EQUIV" or not specified
                var classCode = equivEl.GetAttrVal("classCode");
                if (!string.IsNullOrEmpty(classCode) && !string.Equals(classCode, "EQUIV", StringComparison.OrdinalIgnoreCase))
                    continue;

                // --- Equivalence code and system from <code> child ---
                var codeEl = equivEl.GetSplElement(sc.E.Code);
                string? equivalenceCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? equivalenceCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

                // --- Defining material kind (referenced product) ---
                var definingMatEl = equivEl.GetSplElement(sc.E.DefiningMaterialKind);
                var definingMatCodeEl = definingMatEl?.GetSplElement(sc.E.Code);
                string? definingMaterialKindCode = definingMatCodeEl?.GetAttrVal(sc.A.CodeValue);
                string? definingMaterialKindSystem = definingMatCodeEl?.GetAttrVal(sc.A.CodeSystem);

                // If nothing meaningful, skip
                if (string.IsNullOrWhiteSpace(equivalenceCode) &&
                    string.IsNullOrWhiteSpace(definingMaterialKindCode))
                    continue;

                dtos.Add(new EquivalentEntityDto
                {
                    EquivalenceCode = equivalenceCode,
                    EquivalenceCodeSystem = equivalenceCodeSystem,
                    DefiningMaterialKindCode = definingMaterialKindCode,
                    DefiningMaterialKindSystem = definingMaterialKindSystem
                });
            }

            if (!dtos.Any())
                return 0;

            var dbSet = dbContext.Set<EquivalentEntity>();

            // Query existing entities for this product
            var existing = await dbSet
                .Where(e => e.ProductID == productId)
                .Select(e => new
                {
                    e.EquivalenceCode,
                    e.EquivalenceCodeSystem,
                    e.DefiningMaterialKindCode,
                    e.DefiningMaterialKindSystem
                })
                .ToListAsync();

            // Build composite key set for deduplication
            var existingKeys = new HashSet<(string?, string?, string?, string?)>(
                existing.Select(e => (
                    e.EquivalenceCode,
                    e.EquivalenceCodeSystem,
                    e.DefiningMaterialKindCode,
                    e.DefiningMaterialKindSystem
                ))
            );

            // Filter to only new entities
            var newEntities = dtos
                .Where(dto => !existingKeys.Contains((
                    dto.EquivalenceCode,
                    dto.EquivalenceCodeSystem,
                    dto.DefiningMaterialKindCode,
                    dto.DefiningMaterialKindSystem
                )))
                .Select(dto => new EquivalentEntity
                {
                    ProductID = productId,
                    EquivalenceCode = dto.EquivalenceCode,
                    EquivalenceCodeSystem = dto.EquivalenceCodeSystem,
                    DefiningMaterialKindCode = dto.DefiningMaterialKindCode,
                    DefiningMaterialKindSystem = dto.DefiningMaterialKindSystem
                })
                .ToList();

            if (newEntities.Any())
            {
                dbSet.AddRange(newEntities);
                await dbContext.SaveChangesAsync();
            }

            return newEntities.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates ProductIdentifier records using bulk operations pattern.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="productId">The ID of the Product entity to associate identifiers with.</param>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>The number of ProductIdentifier records created.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: N database calls (one per identifier)
        /// - After: 2 database calls (one query + one insert)
        /// Collects all product identifiers into memory, deduplicates against existing entities,
        /// then performs batch insert for optimal performance.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="ProductIdentifierDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateProductIdentifiersAsync(
            XElement mmEl,
            int productId,
            ApplicationDbContext dbContext,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var dtos = new List<ProductIdentifierDto>();

            // --- PARSE <id> ELEMENTS ---
            foreach (var idEl in mmEl.SplElements(sc.E.Id))
            {
                var root = idEl.GetAttrVal(sc.A.Root);
                var extension = idEl.GetAttrVal(sc.A.Extension);

                if (string.IsNullOrWhiteSpace(root))
                    continue;

                var idType = inferIdentifierType(root);

                dtos.Add(new ProductIdentifierDto
                {
                    IdentifierType = idType,
                    IdentifierValue = extension,
                    IdentifierSystemOID = root
                });
            }

            // --- PARSE <asEntityWithGeneric> / <genericMedicine> / <id> ---
            var genericMedicineEl = mmEl
                .GetSplElement(sc.E.AsEntityWithGeneric)?
                .GetSplElement(sc.E.GenericMedicine);

            if (genericMedicineEl != null)
            {
                foreach (var idEl in genericMedicineEl.SplElements(sc.E.Id))
                {
                    var root = idEl.GetAttrVal(sc.A.Root);
                    var extension = idEl.GetAttrVal(sc.A.Extension);

                    if (string.IsNullOrWhiteSpace(root))
                        continue;

                    var idType = inferIdentifierType(root);

                    dtos.Add(new ProductIdentifierDto
                    {
                        IdentifierType = idType,
                        IdentifierValue = extension,
                        IdentifierSystemOID = root
                    });
                }
            }

            // --- PARSE <code> ELEMENTS (NDC, GTIN, etc.) ---
            foreach (var codeEl in mmEl.SplElements(sc.E.Code))
            {
                var codeValue = codeEl.GetAttrVal(sc.A.CodeValue);
                var codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(codeValue) || string.IsNullOrWhiteSpace(codeSystem))
                    continue;

                var idType = inferIdentifierType(codeSystem);

                dtos.Add(new ProductIdentifierDto
                {
                    IdentifierType = idType,
                    IdentifierValue = codeValue,
                    IdentifierSystemOID = codeSystem
                });
            }

            if (!dtos.Any())
                return 0;

            var dbSet = dbContext.Set<ProductIdentifier>();

            // Query existing identifiers for this product
            var existing = await dbSet
                .Where(i => i.ProductID == productId)
                .Select(i => new { i.IdentifierSystemOID, i.IdentifierValue })
                .ToListAsync();

            // Build composite key set for deduplication
            var existingKeys = new HashSet<(string?, string?)>(
                existing.Select(i => (i.IdentifierSystemOID, i.IdentifierValue))
            );

            // Filter to only new identifiers
            var newIdentifiers = dtos
                .Where(dto => !existingKeys.Contains((dto.IdentifierSystemOID, dto.IdentifierValue)))
                .Select(dto => new ProductIdentifier
                {
                    ProductID = productId,
                    IdentifierType = dto.IdentifierType,
                    IdentifierValue = dto.IdentifierValue,
                    IdentifierSystemOID = dto.IdentifierSystemOID
                })
                .ToList();

            if (newIdentifiers.Any())
            {
                dbSet.AddRange(newIdentifiers);
                await dbContext.SaveChangesAsync();
            }

            return newIdentifiers.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses, validates, and creates SpecializedKind records using bulk operations pattern.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct XML element containing specialized kind information.</param>
        /// <param name="productId">The ID of the Product entity to associate specialized kinds with.</param>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="context">The parsing context for logging and validation.</param>
        /// <param name="documentTypeCode">The document type code used for business rule validation.</param>
        /// <returns>The number of SpecializedKind records successfully created after validation.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: N database calls (one per kind)
        /// - After: 2 database calls (one query + one insert)
        ///
        /// This method performs a multi-step process:
        /// 1. Parses all SpecializedKind entities from XML into DTOs
        /// 2. Validates them against cosmetic category mutual exclusion business rules
        /// 3. Checks for existing kinds in database (bulk query)
        /// 4. Bulk inserts only new, validated kinds
        ///
        /// Uses SpecializedKindValidator to enforce SPL Implementation Guide 3.4.3 rules.
        /// </remarks>
        /// <seealso cref="SpecializedKind"/>
        /// <seealso cref="SpecializedKindDto"/>
        /// <seealso cref="SpecializedKindValidatorService"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateSpecializedKindsAsync(
            XElement mmEl,
            int productId,
            ApplicationDbContext dbContext,
            SplParseContext context,
            string? documentTypeCode)
        {
            #region implementation

            if (mmEl == null
                || context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return 0;
            }

            var dtos = new List<SpecializedKindDto>();

            // Parse ALL SpecializedKind entities from XML
            foreach (var asSpecializedKindEl in mmEl.SplElements(sc.E.AsSpecializedKind))
            {
                // Navigate to the code element within the specialized kind structure
                var codeEl = asSpecializedKindEl
                    .GetSplElement(sc.E.GeneralizedMaterialKind)?
                    .GetSplElement(sc.E.Code);

                if (codeEl == null)
                {
                    context?.Logger?.LogWarning("No <code> found under <generalizedMaterialKind> in <asSpecializedKind>; skipping.");
                    continue;
                }

                // Extract and clean the code attributes
                var kindCode = codeEl.GetAttrVal(sc.A.CodeValue)?.Trim();
                var kindCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem)?.Trim();
                var kindDisplayName = codeEl.GetAttrVal(sc.A.DisplayName)?.Trim();

                // Validate required fields are present
                if (string.IsNullOrWhiteSpace(kindCode) || string.IsNullOrWhiteSpace(kindCodeSystem))
                {
                    context?.Logger?.LogWarning("Missing code or codeSystem in <asSpecializedKind>/<code>; skipping.");
                    continue;
                }

                dtos.Add(new SpecializedKindDto
                {
                    KindCode = kindCode,
                    KindCodeSystem = kindCodeSystem,
                    KindDisplayName = kindDisplayName
                });
            }

            if (!dtos.Any())
                return 0;

            // Convert DTOs to entities for validation
            var allKinds = dtos.Select(dto => new SpecializedKind
            {
                ProductID = productId,
                KindCode = dto.KindCode,
                KindCodeSystem = dto.KindCodeSystem,
                KindDisplayName = dto.KindDisplayName
            }).ToList();

            // Validate with business rules for mutually exclusive cosmetic codes
            var validatedKinds = SpecializedKindValidatorService.ValidateCosmeticCategoryRules(
                allKinds,
                documentTypeCode,
                context.Logger,
                out var rejectedKinds
            );

            if (!validatedKinds.Any())
                return 0;

            var dbSet = dbContext.Set<SpecializedKind>();

            // Query existing specialized kinds for this product
            var existing = await dbSet
                .Where(k => k.ProductID == productId)
                .Select(k => new { k.KindCode, k.KindCodeSystem })
                .ToListAsync();

            // Build composite key set for deduplication
            var existingKeys = new HashSet<(string?, string?)>(
                existing.Select(k => (k.KindCode, k.KindCodeSystem))
            );

            // Filter to only new kinds
            var newKinds = validatedKinds
                .Where(kind => !existingKeys.Contains((kind.KindCode, kind.KindCodeSystem)))
                .ToList();

            if (newKinds.Any())
            {
                dbSet.AddRange(newKinds);
                await dbContext.SaveChangesAsync();

                // Log each created kind
                foreach (var kind in newKinds)
                {
                    context.Logger?.LogInformation(
                        $"Created SpecializedKind: code={kind.KindCode}, codeSystem={kind.KindCodeSystem}, displayName={kind.KindDisplayName} for ProductID {productId}"
                    );
                }
            }

            return newKinds.Count;

            #endregion
        }

        #endregion
    }
}

