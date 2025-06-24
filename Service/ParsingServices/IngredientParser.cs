// Add this new class to your ParsingServices/SplSectionParsers.cs file or a new file.
using MedRecPro.Data;
using MedRecPro.Models;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore; // Add this using statement
using c = MedRecPro.Models.Constant;

namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Parses an <ingredient> element, normalizes the IngredientSubstance via a "get-or-create"
    /// pattern, and links it to the current product in the context.
    /// </summary>
    public class IngredientParser : ISplSectionParser
    {
        public string SectionName => "ingredient";
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            if (context.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse ingredient because no product context exists.");
                return result;
            }

            var ingredientSubstanceEl = element.Element(ns + "ingredientSubstance");
            if (ingredientSubstanceEl == null)
            {
                result.Success = false;
                result.Errors.Add($"Could not find <ingredientSubstance> for ProductID {context.CurrentProduct.ProductID}.");
                return result;
            }

            try
            {
                // 1. Get or Create the normalized IngredientSubstance
                var substance = await getOrCreateIngredientSubstanceAsync(ingredientSubstanceEl, context);
                if (substance?.IngredientSubstanceID == null)
                {
                    throw new InvalidOperationException("Failed to get or create an IngredientSubstance.");
                }

                // 2. Create the Ingredient link record
                var ingredient = new Label.Ingredient
                {
                    ProductID = context.CurrentProduct.ProductID,
                    IngredientSubstanceID = substance.IngredientSubstanceID,
                    ClassCode = element.Attribute("classCode")?.Value,
                    IsConfidential = element.Element(ns + "confidentialityCode")?.Attribute("code")?.Value == "B"
                };

                // 3. Parse quantity information
                var quantityEl = element.Element(ns + "quantity");
                if (quantityEl != null)
                {
                    var numeratorEl = quantityEl.Element(ns + "numerator");
                    var denominatorEl = quantityEl.Element(ns + "denominator");

                    if (decimal.TryParse(numeratorEl?.Attribute("value")?.Value, out var numValue))
                        ingredient.QuantityNumerator = numValue;
                    ingredient.QuantityNumeratorUnit = numeratorEl?.Attribute("unit")?.Value;

                    if (decimal.TryParse(denominatorEl?.Attribute("value")?.Value, out var denValue))
                        ingredient.QuantityDenominator = denValue;
                    ingredient.QuantityDenominatorUnit = denominatorEl?.Attribute("unit")?.Value;
                }

                var ingredientRepo = context.GetRepository<Label.Ingredient>();
                await ingredientRepo.CreateAsync(ingredient);
                result.IngredientsCreated++;

                // 4. (Optional) Parse Active Moiety if it exists
                // This would be another parser or private method call.
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing ingredient for ProductID {context.CurrentProduct.ProductID}: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <ingredient> element.");
            }

            return result;
        }

        /// <summary>
        /// Finds an existing IngredientSubstance by its UNII. If not found, creates a new one.
        /// This ensures that substance data is normalized in the database.
        /// </summary>
        private async Task<Label.IngredientSubstance?> getOrCreateIngredientSubstanceAsync(XElement substanceEl, SplParseContext context)
        {
            var unii = substanceEl.Element(ns + "code")?.Attribute("code")?.Value;
            var name = substanceEl.Element(ns + "name")?.Value;

            if (string.IsNullOrWhiteSpace(unii))
            {
                context.Logger.LogWarning("Ingredient substance '{Name}' is missing a UNII. It will not be normalized and a new record may be created.", name);

                // Fallback: create a new one every time if UNII is missing
                var nonNormalizedSubstance = new Label.IngredientSubstance { SubstanceName = name };

                var repo = context.GetRepository<Label.IngredientSubstance>();
                await repo.CreateAsync(nonNormalizedSubstance);
                return nonNormalizedSubstance;
            }

            // Use the DbContext directly for the specific 'find by UNII' query
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var substanceDbSet = dbContext.Set<Label.IngredientSubstance>();

            var existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s => s.UNII == unii);

            if (existingSubstance != null)
            {
                context.Logger.LogDebug("Found existing IngredientSubstance '{Name}' with UNII {UNII}", name, unii);
                return existingSubstance;
            }

            // If not found, create, save, and return the new entity
            context.Logger.LogInformation("Creating new IngredientSubstance '{Name}' with UNII {UNII}", name, unii);

            var newSubstance = new Label.IngredientSubstance { UNII = unii, SubstanceName = name };

            substanceDbSet.Add(newSubstance);
            await dbContext.SaveChangesAsync(); // Save immediately to get the new ID

            return newSubstance;
        }
    }
}