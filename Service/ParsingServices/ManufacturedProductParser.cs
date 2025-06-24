// This is the updated ManufacturedProductParser class
using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Service.ParsingServices.SplConstants; // Constant class for SPL elements and attributes
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Parses a <manufacturedProduct> element and orchestrates the parsing of its child ingredients.
    /// </summary>
    public class ManufacturedProductParser : ISplSectionParser
    {
        /*******************************************************************************/
        public string SectionName => "manufacturedproduct";
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            if (context.CurrentSection?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse manufacturedProduct because no section context exists.");
                return result;
            }

            try
            {
                // Handle SPL variations where the main data is in a nested element
                var manufacturedMedicineEl = element.Element(ns + "manufacturedMedicine") ?? element;

                var product = new Product
                {
                    SectionID = context.CurrentSection.SectionID.Value,
                    ProductName = manufacturedMedicineEl.Element(ns + sc.E.Name)?.Value,
                    FormCode = manufacturedMedicineEl.Element(ns + sc.E.FormCode)?.Attribute(sc.A.CodeValue)?.Value,
                    FormCodeSystem = manufacturedMedicineEl.Element(ns + sc.E.FormCode)?.Attribute(sc.A.CodeSystem)?.Value,
                    FormDisplayName = manufacturedMedicineEl.Element(ns + sc.E.FormCode)?.Attribute(sc.A.DisplayName)?.Value
                    // ... other product properties
                };

                var productRepo = context.GetRepository<Product>();
                await productRepo.CreateAsync(product);
                result.ProductsCreated++;
                context.Logger.LogInformation("Created Product '{ProductName}' with ID {ProductID}", product.ProductName, product.ProductID);

                // --- DELEGATION TO INGREDIENT PARSER ---

                // Set the current product in the context so the child parser can access it
                var oldProduct = context.CurrentProduct;
                context.CurrentProduct = product;

                var ingredientParser = new IngredientParser();

                // Find all possible ingredient elements. SPL uses different names.
                var ingredientElements = manufacturedMedicineEl.Elements(ns + sc.E.Ingredient)
                    .Concat(manufacturedMedicineEl.Elements(ns + sc.E.ActiveIngredient))
                    .Concat(manufacturedMedicineEl.Elements(ns + sc.E.InactiveIngredient));

                foreach (var ingredientEl in ingredientElements)
                {
                    // The ingredient element itself might have a classCode like 'ACTIB' or 'IACT'
                    // which the ingredient parser can use.
                    var ingredientResult = await ingredientParser.ParseAsync(ingredientEl, context);
                    result.MergeFrom(ingredientResult); // Aggregate results
                }

                // Restore context to avoid side effects
                context.CurrentProduct = oldProduct;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing manufacturedProduct: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <manufacturedProduct> element.");
            }
            return result;
        }
    }
}