

﻿using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Helpers;
using MedRecPro.Models;
using static MedRecPro.Models.Label;

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

            // --- PARSE EQUIVALENT ENTITIES ---
            var equivCount = await parseAndSaveEquivalentEntitiesAsync(element, product, context);
            result.ProductElementsCreated += equivCount;

            // --- PARSE IDENTIFIER ENTITIES ---
            var idCount = await parseAndSaveProductIdentifiersAsync(element, product, context);
            result.ProductElementsCreated += idCount;

            // --- PARSE SPECIALIZED KINDS ---
            // The document type code is needed for business rule validation.
            var kindCount = await parseAndSaveSpecializedKindsAsync(element, product, context, context.Document?.DocumentCode);
            result.ProductElementsCreated += kindCount;

            return result;
            #endregion
        }

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
        /// Parses and creates ProductIdentifier entities from the manufacturedProduct XML element.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="product">The Product entity to link identifiers to.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of ProductIdentifier records created.</returns>
        /// <remarks>
        /// Handles all 'code' elements representing product/item codes (NDC, GTIN, etc.) under 'manufacturedProduct'
        /// and 'subjectOf'/'approval' sections, supporting drugs, devices, and cosmetics.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveProductIdentifiersAsync(XElement mmEl, Product product, SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // 1. Save product/package identifiers from direct code elements
            var codes = getAllProductAndPackagingCodes(mmEl);
            createdCount += await saveProductIdentifiersAsync(codes, product, context);

            // 2. Save approval/marketing identifiers from subjectOf/approval sections
            createdCount += await saveApprovalIdentifiersAsync(mmEl, product, context);

            return createdCount;
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
        /// Creates and saves ProductIdentifier entities from a collection of code XML elements.
        /// </summary>
        /// <param name="codeElements">The collection of XML code elements to process.</param>
        /// <param name="product">The Product entity to associate identifiers with.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of ProductIdentifier records successfully created.</returns>
        /// <remarks>
        /// Validates that both code value and code system are present before creating identifiers.
        /// Logs the creation of each identifier for audit purposes.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> saveProductIdentifiersAsync(IEnumerable<XElement> codeElements, Product product, SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<ProductIdentifier>();

            // Process each code element to create identifiers
            foreach (var codeEl in codeElements)
            {
                // Extract code value and system from XML attributes
                string? codeVal = codeEl.GetAttrVal(sc.A.CodeValue);
                string? codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                // Skip elements that don't have required values
                if (string.IsNullOrWhiteSpace(codeVal) || string.IsNullOrWhiteSpace(codeSystem))
                    continue;

                // Create the identifier entity with inferred type
                var identifier = new ProductIdentifier
                {
                    ProductID = product.ProductID,
                    IdentifierValue = codeVal,
                    IdentifierSystemOID = codeSystem,
                    IdentifierType = inferIdentifierType(codeSystem)
                };

                // Save to database and track count
                await repo.CreateAsync(identifier);
                count++;
                context?.Logger?.LogInformation($"ProductIdentifier created: ProductID={product.ProductID} Value={codeVal} OID={codeSystem}");
            }
            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and saves ProductIdentifier entities from approval/marketing code elements.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct XML element containing approval information.</param>
        /// <param name="product">The Product entity to associate identifiers with.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of approval ProductIdentifier records successfully created.</returns>
        /// <remarks>
        /// Searches for approval codes in subjectOf/approval XML structures.
        /// These typically contain regulatory approval numbers or marketing authorization codes.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> saveApprovalIdentifiersAsync(XElement mmEl, Product product, SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<ProductIdentifier>();

            // Find all approval code elements in the XML structure
            foreach (var approvalCodeEl in mmEl.SplElements(sc.E.SubjectOf, sc.E.Approval, sc.E.Code))
            {
                // Extract approval code details
                string? codeVal = approvalCodeEl.GetAttrVal(sc.A.CodeValue);
                string? codeSystem = approvalCodeEl.GetAttrVal(sc.A.CodeSystem);

                // Skip if required values are missing
                if (string.IsNullOrWhiteSpace(codeVal) || string.IsNullOrWhiteSpace(codeSystem))
                    continue;

                // Create approval identifier entity
                var identifier = new ProductIdentifier
                {
                    ProductID = product.ProductID,
                    IdentifierValue = codeVal,
                    IdentifierSystemOID = codeSystem,
                    IdentifierType = inferIdentifierType(codeSystem)
                };

                // Save and log the approval identifier
                await repo.CreateAsync(identifier);
                count++;
                context?.Logger?.LogInformation("Approval ProductIdentifier created: ProductID={ProductID} Value={IdentifierValue} OID={IdentifierSystemOID}",
                    product.ProductID, codeVal, codeSystem);
            }
            return count;
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
    }
}

