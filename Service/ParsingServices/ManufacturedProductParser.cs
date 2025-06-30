using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using static MedRecPro.Models.Label;
using MedRecPro.Helpers;
using AngleSharp.Common;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses a manufacturedProduct element and orchestrates the parsing of its child ingredients.
    /// </summary>
    /// <remarks>
    /// This parser handles the manufacturedProduct section of SPL documents, extracting product
    /// information and coordinating the parsing of associated ingredients. It manages context
    /// switching to ensure that ingredient parsers have access to the current product being
    /// processed and supports various SPL structural variations.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="IngredientParser"/>
    /// <seealso cref="SplParseContext"/>
    public class ManufacturedProductParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, representing the manufacturedProduct element.
        /// </summary>
        public string SectionName => "manufacturedproduct";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a manufacturedProduct element from an SPL document, creating the product entity
        /// and orchestrating the parsing of its associated ingredients.
        /// </summary>
        /// <param name="element">The XElement representing the manufacturedProduct section to parse.</param>
        /// <param name="context">The current parsing context containing the section to link products to.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new ManufacturedProductParser();
        /// var result = await parser.ParseAsync(productElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Products created: {result.ProductsCreated}");
        ///     Console.WriteLine($"Ingredients created: {result.IngredientsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a section context exists
        /// 2. Handles SPL structural variations (manufacturedMedicine nesting)
        /// 3. Extracts product metadata and creates the Product entity
        /// 4. Sets up context for ingredient parsing
        /// 5. Delegates ingredient parsing to specialized parsers
        /// 6. Aggregates results from all child parsers
        /// 7. Restores context to prevent side effects
        /// 
        /// The method supports multiple ingredient element types and maintains proper context
        /// isolation to ensure thread safety and predictable behavior.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="IngredientParser"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that we have a valid section context to link products to
            if (context.CurrentSection?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse manufacturedProduct because no section context exists.");
                return result;
            }

            try
            {
                // Handle SPL variations where the main data is in a nested manufacturedMedicine element
                // Use the nested element if it exists, otherwise use the main element
                var mmEl = element.SplElement(sc.E.ManufacturedMedicine) ?? element;

                // Create the Product entity with extracted metadata
                var product = new Product
                {
                    SectionID = context.CurrentSection.SectionID.Value,

                    // Extract product name from the name element
                    ProductName = mmEl.GetSplElementVal(sc.E.Name),

                    // Extract product suffix from the suffix element
                    ProductSuffix = mmEl.GetSplElementVal(sc.E.Suffix),

                    // Extract form code value from the formCode element's codeValue attribute
                    FormCode = mmEl.GetSplElementAttrVal(sc.E.FormCode, sc.A.CodeValue),

                    // Extract form code system from the formCode element's codeSystem attribute
                    FormCodeSystem = mmEl.GetSplElementAttrVal(sc.E.FormCode, sc.A.CodeSystem),

                    // Extract form display name from the formCode element's displayName attribute
                    FormDisplayName = mmEl.GetSplElementAttrVal(sc.E.FormCode, sc.A.DisplayName),

                    // Extract and trim description text from the desc element
                    DescriptionText = mmEl.GetSplElementVal(sc.E.Desc)?.Trim()
                };

                // Save the product entity to the database
                var productRepo = context.GetRepository<Product>();
                await productRepo.CreateAsync(product);

                // Validate that the database assigned a product ID
                if (!product.ProductID.HasValue)
                {
                    throw new InvalidOperationException("ProductID was not populated by the database after creation.");
                }

                // --- PARSE GENERIC MEDICINE ---
                var genericMedicinesCreated = await parseAndSaveGenericMedicinesAsync(mmEl, product, context);
                result.ProductElementsCreated += genericMedicinesCreated;

                // --- PARSE EQUIVALENT ENTITIES ---
                var equivCount = await parseAndSaveEquivalentEntitiesAsync(mmEl, product, context);
                result.ProductElementsCreated += equivCount;

                // --- PARSE IDENTIFIER ENTITIES ---
                var idCount = await parseAndSaveProductIdentifiersAsync(mmEl, product, context);
                result.ProductElementsCreated += idCount;


                result.ProductsCreated++;
                context.Logger.LogInformation("Created Product '{ProductName}' with ID {ProductID}", product.ProductName, product.ProductID);

                // --- DELEGATION TO INGREDIENT PARSER ---
                // Set the current product in the context so child parsers can access it
                // Store the previous product context to restore later
                var oldProduct = context.CurrentProduct;
                context.CurrentProduct = product;

                // Create ingredient parser for delegated parsing of child ingredients
                var ingredientParser = new IngredientParser();

                // Find all possible ingredient elements across different SPL naming conventions
                // SPL documents may use ingredient, activeIngredient, or inactiveIngredient
                var ingredientElements = mmEl.SplFindIngredients(excludingFieldsContaining: "substance");

                // Process each ingredient element found
                foreach (var ingredientEl in ingredientElements)
                {
                    // The ingredient element itself might have a classCode like 'ACTIB' or 'IACT'
                    // which the ingredient parser can use for classification
                    var ingredientResult = await ingredientParser.ParseAsync(ingredientEl, context);
                    result.MergeFrom(ingredientResult); // Aggregate results from ingredient parsing
                }

                // TODO: Parse SpecializedKind, PackagingLevel, MarketingCategory, Characteristic etc.

                // Restore the previous product context to avoid side effects on other parsers
                context.CurrentProduct = oldProduct;
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during product parsing
                result.Success = false;
                result.Errors.Add($"Error parsing manufacturedProduct: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <manufacturedProduct> element.");
            }

            return result;
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
        private async Task<int> parseAndSaveProductIdentifiersAsync(XElement mmEl, Product product, SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // 1. Save product/package identifiers
            var codes = getAllProductAndPackagingCodes(mmEl);
            createdCount += await SaveProductIdentifiersAsync(codes, product, context);

            // 2. Save approval/marketing identifiers
            createdCount += await SaveApprovalIdentifiersAsync(mmEl, product, context);

            return createdCount;
            #endregion
        }

        /**************************************************************/
        private static string? inferIdentifierType(string? oid)
        {
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

                // Example: Custom OIDs (if needed, add here)
                //"X.Y.Z..." => "CustomType",

                _ => null
            };
        }

        /**************************************************************/
        private IEnumerable<XElement> getAllProductAndPackagingCodes(XElement element)
        {
            // 1. Product-level codes: <code> directly under <manufacturedProduct>
            foreach (var code in element.Elements(ns + sc.E.Code))
                yield return code;

            // 2. Recurse into nested packaging: <asContent>/<containerPackagedProduct>/<code>
            foreach (var container in element.Descendants(ns + sc.E.ContainerPackagedProduct))
            {
                foreach (var code in container.Elements(ns + sc.E.Code))
                    yield return code;
            }

            // 3. Recurse into any nested <manufacturedProduct>
            foreach (var nested in element.Elements(ns + sc.E.ManufacturedProduct))
            {
                foreach (var code in getAllProductAndPackagingCodes(nested))
                    yield return code;
            }
        }

        /**************************************************************/
        private async Task<int> SaveProductIdentifiersAsync(IEnumerable<XElement> codeElements, Product product, SplParseContext context)
        {
            int count = 0;
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
                count++;
                context.Logger.LogInformation($"ProductIdentifier created: ProductID={product.ProductID} Value={codeVal} OID={codeSystem}");
            }
            return count;
        }

        /**************************************************************/
        private async Task<int> SaveApprovalIdentifiersAsync(XElement mmEl, Product product, SplParseContext context)
        {
            int count = 0;
            var repo = context.GetRepository<ProductIdentifier>();
            foreach (var approvalCodeEl in mmEl.SplElements(sc.E.SubjectOf, sc.E.Approval, sc.E.Code))
            {
                string? codeVal = approvalCodeEl.GetAttrVal(sc.A.CodeValue);
                string? codeSystem = approvalCodeEl.GetAttrVal(sc.A.CodeSystem);
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
                count++;
                context.Logger.LogInformation("Approval ProductIdentifier created: ProductID={ProductID} Value={IdentifierValue} OID={IdentifierSystemOID}",
                    product.ProductID, codeVal, codeSystem);
            }
            return count;
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates GenericMedicine entities from the manufacturedProduct XML element.
        /// </summary>
        /// <param name="mmEl">The manufacturedProduct or manufacturedMedicine XElement.</param>
        /// <param name="product">The Product entity to link generic medicines to.</param>
        /// <param name="context">The parsing context (provides access to logger and repositories).</param>
        /// <returns>A Task representing the asynchronous operation, returning the count of GenericMedicine records created.</returns>
        /// <remarks>
        /// Handles extraction of all genericMedicine elements under asEntityWithGeneric. For each genericMedicine,
        /// extracts the main name and the optional phonetic name (use="PHON"). Links them to the provided product.
        /// </remarks>
        private async Task<int> parseAndSaveGenericMedicinesAsync(XElement mmEl, Product product, SplParseContext context)
        {
            #region implementation
            int createdCount = 0;
            var repo = context.GetRepository<GenericMedicine>();

            // Locate all <asEntityWithGeneric>/<genericMedicine> descendants
            foreach (var genericMedicineEl in mmEl
                .Descendants(ns + sc.E.AsEntityWithGeneric)
                .Elements(ns + sc.E.GenericMedicine))
            {
                // Extract main name (where no "use" attribute or "use" != "PHON")
                var nameEl = genericMedicineEl
                    .SplElements(sc.E.Name)
                    .FirstOrDefault(e =>
                        !string.Equals((string?)e.Attribute("use"), "PHON", StringComparison.OrdinalIgnoreCase)
                    );

                var genericName = nameEl?.Value?.Trim();

                // Extract phonetic name (where "use" == "PHON")
                var phoneticEl = genericMedicineEl
                    .SplElements(sc.E.Name)
                    .FirstOrDefault(e =>
                        string.Equals((string?)e.Attribute("use"), "PHON", StringComparison.OrdinalIgnoreCase)
                    );

                var phoneticName = phoneticEl?.Value?.Trim();

                // If neither name is present, skip
                if (string.IsNullOrWhiteSpace(genericName) && string.IsNullOrWhiteSpace(phoneticName))
                    continue;

                // Build the entity
                var genMed = new GenericMedicine
                {
                    ProductID = product.ProductID,
                    GenericName = genericName,
                    PhoneticName = phoneticName
                };

                await repo.CreateAsync(genMed);
                createdCount++;
                context.Logger.LogInformation($"Created GenericMedicine '{genericName}' for ProductID {product.ProductID}");
            }
            return createdCount;
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
                context.Logger.LogInformation($"Created EquivalentEntity for ProductID {product.ProductID}: EquivCode={equivalenceCode}, DefMatCode={definingMaterialKindCode}");
            }

            return createdCount;
            #endregion
        }
    }
}