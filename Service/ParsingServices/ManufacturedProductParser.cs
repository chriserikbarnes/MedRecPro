using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using static MedRecPro.Models.Label;
using MedRecPro.Helpers;
using AngleSharp.Common;
using MedRecPro.Models;
using MedRecPro.DataAccess;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Azure;

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
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
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
        /// <seealso cref="AdditionalIdentifier"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Characteristic"/> 
        /// <seealso cref="EquivalentEntity"/>
        /// <seealso cref="GenericMedicine"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientParser"/>
        /// <seealso cref="Label"/>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="PackagingHierarchy"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="PartOfAssembly"/>
        /// <seealso cref="Policy"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="ProductPart"/>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="ProductWebLink"/>
        /// <seealso cref="ResponsiblePersonLink"/>
        /// <seealso cref="SpecializedKind"/>  
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate that we have a valid section context to link products to
            if (context.CurrentSection?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse manufacturedProduct because no section context exists.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting Manufactured Product XML Elements {context.FileNameInZip}");

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

                // --- SET PRODUCT CONTEXT FOR CHILD PARSERS ---
                // Store the previous product context to restore later. This ensures that even if an
                // exception occurs, the context is restored, preventing side effects.
                var oldProduct = context.CurrentProduct;
                context.CurrentProduct = product;

                try
                {

                    // --- PARSE IDENTIES ---
                    reportProgress?.Invoke($"Starting Product Identity XML Elements {context.FileNameInZip}");
                    var identityParser = new ProductIdentityParser();
                    var identityResult = await identityParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(identityResult);

                    // --- PARSE MARKETING CATEGORY ---
                    reportProgress?.Invoke($"Starting Product Marketing XML Elements {context.FileNameInZip}");
                    var marketingParser = new ProductMarketingParser();
                    var marketingResult = await marketingParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(marketingResult);
        
                    // --- PARSE CHARACTERISTICS ---
                    reportProgress?.Invoke($"Starting Product Characteristics XML Elements {context.FileNameInZip}");
                    var characteristicsParser = new ProductCharacteristicsParser();
                    var characteristicsResult = await characteristicsParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(characteristicsResult);

                    // --- PARSE BUSINESS OPERATION ---
                    reportProgress?.Invoke($"Starting Business Operation XML Elements {context.FileNameInZip}");
                    var businessOperationParser = new BusinessOperationParser();
                    var businessOperationResult = await businessOperationParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(businessOperationResult);
             
                    // --- PARSE RODUCT RELATIONSHIP ---
                    reportProgress?.Invoke($"Starting Product Relation XML Elements {context.FileNameInZip}");
                    var relationshipParser = new ProductRelationshipParser(this); // 'this' provides the needed recursion callback
                    var relationshipResult = await relationshipParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(relationshipResult);

                    // --- PARSE PRODUCT EXTENSION ---
                    reportProgress?.Invoke($"Starting Product Extension XML Elements {context.FileNameInZip}");
                    var extensionParser = new ProductExtensionParser();
                    var extensionResult = await extensionParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(extensionResult);

                    // --- PARSE PACKAGING ---
                    reportProgress?.Invoke($"Starting Packaging Level XML Elements {context.FileNameInZip}");
                    var packagingParser = new PackagingParser();
                    var packagingResult = await packagingParser.ParseAsync(mmEl, context, reportProgress);
                    result.MergeFrom(packagingResult);
            
                    result.ProductsCreated++;
                    context.Logger.LogInformation("Created Product '{ProductName}' with ID {ProductID}", product.ProductName, product.ProductID);
                    reportProgress?.Invoke($"Completed Packaging Level XML Elements {context.FileNameInZip}");

                    // --- PARSE DOSING SPECIFICATIONS ---
                    reportProgress?.Invoke($"Starting Dosing Specification XML Elements {context.FileNameInZip}");
                    var dosingSpecCount = await DosingSpecificationParser.BuildDosingSpecificationAsync(mmEl, product, context);
                    result.ProductElementsCreated += dosingSpecCount;

                    // --- DELEGATION TO INGREDIENT PARSER ---
                    // Set the current product in the context so child parsers can access it
                    // Store the previous product context to restore later
                    context.CurrentProduct = product;

                    // Create ingredient parser for delegated parsing of child ingredients
                    var ingredientParser = new IngredientParser();

                    // Find all possible ingredient elements across different SPL naming conventions
                    // SPL documents may use ingredient, activeIngredient, or inactiveIngredient
                    var ingredientElements = mmEl.SplFindIngredients(excludingFieldsContaining: "substance");

                    reportProgress?.Invoke($"Starting Ingredient Level XML Elements {context.FileNameInZip}");

                    context.SeqNumber = 0; // Reset sequence number for ingredients

                    // Process each ingredient element found
                    foreach (var ingredientEl in ingredientElements)
                    {
                        // The ingredient element itself might have a classCode like 'ACTIB' or 'IACT'
                        // which the ingredient parser can use for classification
                        var ingredientResult = await ingredientParser.ParseAsync(ingredientEl, context, reportProgress);
                        result.MergeFrom(ingredientResult); // Aggregate results from ingredient parsing
                        context.SeqNumber++; // Increment sequence for next ingredient
                    }

                    // Restore the previous product context to avoid side effects on other parsers
                    context.CurrentProduct = oldProduct;
                    reportProgress?.Invoke($"Completed Ingredient Level XML Elements {context.FileNameInZip}");

                    // --- DELEGATION TO LOT DISTRIBUTION PARSER ---
                    var lotDistributionElements = mmEl.SplFindElements(sc.E.ProductInstance);
                    if (lotDistributionElements.Any())
                    {
                        reportProgress?.Invoke($"Starting Lot Distribution XML Elements {context.FileNameInZip}");

                        // Set the current product in the context for lot distribution parsing
                        var oldProductForLots = context.CurrentProduct;
                        context.CurrentProduct = product;

                        // Create lot distribution parser for delegated parsing
                        var lotDistributionParser = new LotDistributionParser();

                        // Process the parent element containing lot instances
                        var lotResult = await lotDistributionParser.ParseAsync(mmEl, context, reportProgress);
                        result.MergeFrom(lotResult); // Aggregate results from lot distribution parsing

                        // Restore the previous product context
                        context.CurrentProduct = oldProductForLots;
                        reportProgress?.Invoke($"Completed Lot Distribution XML Elements {context.FileNameInZip}");
                    }
                }
                finally
                {

                    // Restore the previous product context to avoid side effects on other parsers
                    context.CurrentProduct = oldProduct;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during product parsing
                result.Success = false;
                result.Errors.Add($"Error parsing manufacturedProduct: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <manufacturedProduct> element.");
            }

            reportProgress?.Invoke($"Completed Manufactured Product XML Elements {context.FileNameInZip}");

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
    }
}