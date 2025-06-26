using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using static MedRecPro.Models.Label;
using MedRecPro.Helpers;

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
                var mmEl = element.Element(ns + sc.E.ManufacturedMedicine) ?? element;

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
                var ingredientElements = mmEl.SplElements(sc.E.Ingredient)
                    .Concat(mmEl.SplElements(sc.E.ActiveIngredient))
                    .Concat(mmEl.SplElements(sc.E.InactiveIngredient));

                // Process each ingredient element found
                foreach (var ingredientEl in ingredientElements)
                {
                    // The ingredient element itself might have a classCode like 'ACTIB' or 'IACT'
                    // which the ingredient parser can use for classification
                    var ingredientResult = await ingredientParser.ParseAsync(ingredientEl, context);
                    result.MergeFrom(ingredientResult); // Aggregate results from ingredient parsing
                }

                // TODO: Parse ProductIdentifier, GenericMedicine, SpecializedKind, EquivalentEntity, 
                // PackagingLevel, MarketingCategory, Characteristic etc.

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
    }
}