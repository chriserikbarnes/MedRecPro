using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses secondary product information, including generic medicine names and associated web links,
    /// from an SPL document.
    /// </summary>
    /// <remarks>
    /// This parser handles supplementary data that extends the core product definition. It is designed
    /// to be called by a parent parser and assumes that `SplParseContext.CurrentProduct` has been set.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="GenericMedicine"/>
    /// <seealso cref="ProductWebLink"/>
    /// <seealso cref="SplParseContext"/>
    public class ProductExtensionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "productextension";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecProImportClass.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an XML element to extract and save secondary product information.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of generic medicines and product web links by calling
        /// specialized private methods. It requires `context.CurrentProduct` to be set by the caller.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that a product is available in the context to link entities to.
            if (context?.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse product extensions because no product context exists.");
                context?.Logger?.LogError("ProductExtensionParser was called without a valid product in the context.");
                return result;
            }
            var product = context.CurrentProduct;

            // --- PARSE GENERIC MEDICINE ---
            reportProgress?.Invoke($"Starting Generic Medicine XML Elements {context.FileNameInZip}");
            var genericMedicinesCreated = await parseAndSaveGenericMedicinesAsync(element, product, context);
            result.ProductElementsCreated += genericMedicinesCreated;

            // --- PARSE WEB LINK ---
            reportProgress?.Invoke($"Starting Web Link XML Elements {context.FileNameInZip}");
            var webCt = await parseAndSaveProductWebLinksAsync(element, product, context);
            result.ProductElementsCreated += webCt;

            return result;
            #endregion
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
        /// <seealso cref="GenericMedicine"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
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
        /// Parses and saves all ProductWebLink entities under [subjectOf][document][text][reference] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or similar) to scan for product web links.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of ProductWebLink records created.</returns>
        /// <remarks>
        /// Handles absolute web URLs (http/https) per SPL IG Section 3.4.7.
        /// Only processes references without mediaType attributes to focus on web URLs.
        /// Validates URL format and filters out non-web references.
        /// </remarks>
        /// <seealso cref="ProductWebLink"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveProductWebLinksAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<ProductWebLink>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all subjectOf/document/text/reference structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var docEl in subjOf.SplElements(sc.E.Document))
                {
                    var textEl = docEl.GetSplElement(sc.E.Text);
                    if (textEl == null)
                        continue;

                    var refEl = textEl.GetSplElement(sc.E.Reference);
                    if (refEl == null)
                        continue;

                    // Only use reference if it has no mediaType and has a valid URL
                    var url = refEl.GetAttrVal(sc.A.Value);
                    var hasMediaType = refEl.GetAttrVal(sc.A.MediaType);

                    // Validate URL format (must be http or https)
                    if (string.IsNullOrWhiteSpace(url) ||
                        !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Skip if mediaType is present (not a web URL reference)
                    if (!string.IsNullOrEmpty(hasMediaType))
                        continue;

                    // Create and save the product web link
                    var productWebLink = new ProductWebLink
                    {
                        ProductID = product.ProductID,
                        WebURL = url
                    };

                    await repo.CreateAsync(productWebLink);
                    count++;
                    context.Logger.LogInformation(
                        $"ProductWebLink created: ProductID={product.ProductID}, WebURL={url}");
                }
            }

            return count;
            #endregion
        }
    }
}
