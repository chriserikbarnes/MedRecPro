using MedRecPro.Data;
using MedRecPro.Models;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using static MedRecPro.Models.Label;
using MedRecPro.Helpers;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses an ingredient element, normalizes the IngredientSubstance via a "get-or-create"
    /// pattern, and links it to the current product in the context.
    /// </summary>
    /// <remarks>
    /// This parser handles ingredient elements within SPL documents, extracting substance
    /// information and creating normalized relationships between products and their ingredient
    /// substances. It implements deduplication logic using UNII codes to prevent duplicate
    /// substance records and supports quantity parsing for ingredient measurements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Ingredient"/>
    /// <seealso cref="IngredientSubstance"/>
    /// <seealso cref="SplParseContext"/>
    public class IngredientParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, representing the ingredient element.
        /// </summary>
        public string SectionName => "ingredient";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an ingredient element from an SPL document, creating normalized ingredient
        /// substance entities and linking them to the current product.
        /// </summary>
        /// <param name="element">The XElement representing the ingredient section to parse.</param>
        /// <param name="context">The current parsing context containing the product to link ingredients to.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new IngredientParser();
        /// var result = await parser.ParseAsync(ingredientElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Ingredients created: {result.IngredientsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a product context exists
        /// 2. Extracts the ingredientSubstance element
        /// 3. Gets or creates a normalized IngredientSubstance entity
        /// 4. Creates an Ingredient link between the product and substance
        /// 5. Parses quantity information (numerator/denominator)
        /// 6. Saves the ingredient relationship to the database
        /// 
        /// The method uses UNII codes for substance normalization to prevent duplicates.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientSubstance"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that we have a valid product context to link ingredients to
            if (context.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse ingredient because no product context exists.");
                return result;
            }

            // Navigate to the ingredientSubstance element within the ingredient
            var ingredientSubstanceEl = element.GetSplElement(sc.E.IngredientSubstance)
                ?? element.GetSplElement(sc.E.InactiveIngredientSubstance)
                ?? element.GetSplElement(sc.E.ActiveIngredientSubstance);

            if (ingredientSubstanceEl == null)
            {
                result.Success = false;
                result.Errors.Add($"Could not find <ingredientSubstance> for ProductID {context.CurrentProduct.ProductID}.");
                return result;
            }

            try
            {
                // Step 1: Get or Create the normalized IngredientSubstance entity
                var substance = await getOrCreateIngredientSubstanceAsync(ingredientSubstanceEl, context);
                if (substance?.IngredientSubstanceID == null)
                {
                    throw new InvalidOperationException("Failed to get or create an IngredientSubstance.");
                }

                // Step 2: Create the Ingredient link record between product and substance
                var ingredient = new Ingredient
                {
                    ProductID = context.CurrentProduct.ProductID,

                    IngredientSubstanceID = substance.IngredientSubstanceID,

                    // Extract class code from the ingredient element's classCode attribute
                    ClassCode = element.GetAttrVal(sc.A.ClassCode),

                    // Determine confidentiality based on confidentiality code value
                    IsConfidential = element.GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
                };

                // Step 3: Parse quantity information from the quantity element
                var quantityEl = element.GetSplElement(sc.E.Quantity);
                if (quantityEl != null)
                {
                    // Extract numerator information (amount value and unit)
                    var numeratorEl = quantityEl.GetSplElement(sc.E.Numerator);

                    // Extract denominator information (amount value and unit)
                    var denominatorEl = quantityEl.GetSplElement(sc.E.Denominator);

                    // Parse and assign numerator value if it's a valid decimal
                    if (decimal.TryParse(numeratorEl?.GetAttrVal(sc.A.Value), out var numValue))
                        ingredient.QuantityNumerator = numValue;

                    // Extract numerator unit from the unit attribute
                    ingredient.QuantityNumeratorUnit = numeratorEl?.GetAttrVal(sc.A.Unit);

                    // Parse and assign denominator value if it's a valid decimal
                    if (decimal.TryParse(denominatorEl?.GetAttrVal(sc.A.Value), out var denValue))
                        ingredient.QuantityDenominator = denValue;

                    // Extract denominator unit from the value attribute
                    ingredient.QuantityDenominatorUnit = denominatorEl?.GetAttrVal(sc.A.Value);
                }

                // Save the ingredient relationship to the database
                var ingredientRepo = context.GetRepository<Ingredient>();
                await ingredientRepo.CreateAsync(ingredient);

                if(!ingredient.IngredientID.HasValue)
                {
                    throw new InvalidOperationException("IngredientID was not populated by the database after creation.");
                }

                // Step 4: Create the specified substance entity and link it to the ingredient
                await createSpecifiedSubstanceAsync(ingredientSubstanceEl, context, ingredient.IngredientID.Value);

                result.IngredientsCreated++;

                // Step 5: (Optional) Parse Active Moiety if it exists
                // This would be another parser or private method call.
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during ingredient parsing
                result.Success = false;
                result.Errors.Add($"Error parsing ingredient for ProductID {context.CurrentProduct.ProductID}: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <ingredient> element.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new SpecifiedSubstance entity asynchronously from an XML element.
        /// </summary>
        /// <param name="substanceEl">The XML element containing substance information from the SPL document.</param>
        /// <param name="context">The parsing context providing access to services and shared state.</param>
        /// <param name="ingredientID">The identifier of the ingredient this substance belongs to.</param>
        /// <returns>A task representing the asynchronous operation that returns the created SpecifiedSubstance, or null if creation fails.</returns>
        /// <remarks>
        /// This method extracts substance code, code system, and display name from the XML element,
        /// then creates and persists a new SpecifiedSubstance entity to the database.
        /// The entity is saved immediately to ensure the ID is populated for subsequent operations.
        /// </remarks>
        /// <example>
        /// <code>
        /// var substanceElement = splDocument.Descendants("substance").First();
        /// var newSubstance = await createSpecifiedSubstanceAsync(substanceElement, parseContext, 123);
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="SpecifiedSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<SpecifiedSubstance?> createSpecifiedSubstanceAsync(XElement substanceEl, SplParseContext context, int ingredientID)
        {
            #region implementation

            #region extract substance data from xml
            // Extract the substance code from the XML element
            var substanceCode = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);

            // Extract the code system identifier for the substance
            var substanceCodeSystem = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);

            // Extract the human-readable display name for the substance
            var displayName = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName);
            #endregion

            #region database operations
            // Use the DbContext directly for the specification
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Log the creation of the new substance for tracking purposes
            context.Logger.LogInformation("Creating new SpecifiedSubstance '{code}' with code system {system}", substanceCode, substanceCodeSystem);

            // Create new substance with extracted UNII and name
            var newSpecifiedSubstance = new SpecifiedSubstance
            {
                SubstanceCode = substanceCode,
                SubstanceCodeSystem = substanceCodeSystem,
                SubstanceDisplayName = displayName,
                IngredientID = ingredientID
            };

            // don't create empty items
            if (!string.IsNullOrWhiteSpace(substanceCode) && !string.IsNullOrWhiteSpace(substanceCodeSystem))
            {
                // Get the DbSet for SpecifiedSubstance entities
                var substanceDbSet = dbContext.Set<SpecifiedSubstance>();

                // Add to DbSet and save immediately to get the new ID populated
                substanceDbSet.Add(newSpecifiedSubstance);

                // Save immediately to get the new ID for subsequent operations
                await dbContext.SaveChangesAsync(); 
            }
            #endregion

            return newSpecifiedSubstance;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing IngredientSubstance by its UNII. If not found, creates a new one.
        /// This ensures that substance data is normalized in the database.
        /// </summary>
        /// <param name="substanceEl">The XElement representing the ingredientSubstance to process.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <returns>An IngredientSubstance entity, either existing or newly created, or null if creation fails.</returns>
        /// <example>
        /// <code>
        /// var substance = await getOrCreateIngredientSubstanceAsync(substanceElement, context);
        /// if (substance != null)
        /// {
        ///     Console.WriteLine($"Substance: {substance.SubstanceName} (UNII: {substance.UNII})");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements substance normalization using UNII (Unique Ingredient Identifier) codes.
        /// The process:
        /// 1. Extracts UNII and name from the XML element
        /// 2. If UNII is missing, creates a non-normalized substance record
        /// 3. If UNII exists, searches for existing substance with same UNII
        /// 4. If found, returns existing substance; otherwise creates new one
        /// 
        /// This approach prevents duplicate substance records for the same chemical entity.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions.GetSplElementAttrVal(XElement, string, string)"/>
        private async Task<IngredientSubstance?> getOrCreateIngredientSubstanceAsync(XElement substanceEl, SplParseContext context)
        {
            #region implementation
            // Extract UNII code from the code element's codeValue attribute
            var unii = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);

            // Extract substance name from the name element
            var name = substanceEl.GetSplElement(sc.E.Name);

            // Use the DbContext directly for the specific 'find by UNII' query
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var substanceDbSet = dbContext.Set<IngredientSubstance>();

            // Search for existing substance with the same UNII code
            var existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s => s.UNII == unii 
            || (name != null 
                && !string.IsNullOrEmpty(name.Value) 
                && name.Value.Equals(s.SubstanceName, StringComparison.InvariantCultureIgnoreCase)));

            // Return existing substance if found
            if (existingSubstance != null)
            {
                context.Logger.LogDebug("Found existing IngredientSubstance '{Name}' with UNII {UNII}", name, unii);
                return existingSubstance;
            }

            // Handle case where UNII is missing - create non-normalized substance
            if (string.IsNullOrWhiteSpace(unii))
            {
                context.Logger.LogWarning("Ingredient substance '{Name}' is missing a UNII. It will not be normalized and a new record may be created.", name);

                // Fallback: create a new substance record every time if UNII is missing
                var nonNormalizedSubstance = new IngredientSubstance { SubstanceName = name?.Value };

                var repo = context.GetRepository<IngredientSubstance>();
                await repo.CreateAsync(nonNormalizedSubstance);
                return nonNormalizedSubstance;
            }

            // If not found, create, save, and return the new entity
            context.Logger.LogInformation("Creating new IngredientSubstance '{Name}' with UNII {UNII}", name, unii);

            // Create new substance with extracted UNII and name
            var newSubstance = new IngredientSubstance { UNII = unii, SubstanceName = name?.Value };

            // Add to DbSet and save immediately to get the new ID populated
            substanceDbSet.Add(newSubstance);
            await dbContext.SaveChangesAsync(); // Save immediately to get the new ID

            return newSubstance;
            #endregion
        }
    }
}