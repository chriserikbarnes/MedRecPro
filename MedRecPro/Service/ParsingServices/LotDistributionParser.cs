using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using static MedRecPro.Models.Label;
using MedRecPro.Helpers;
using MedRecPro.Data;
using MedRecPro.Models;
using System.Threading.Tasks;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses lot distribution report elements including Fill Lots, Label Lots, Package Lots, 
    /// Bulk Lots, and Salvaged Lots according to SPL IG Section 16.2.
    /// </summary>
    /// <remarks>
    /// This parser handles lot distribution reporting structures, extracting lot identifiers,
    /// product instances, and ingredient instances while maintaining proper relationships.
    /// It supports various lot types including Fill Lots (16.2.5), Bulk Lots (16.2.6), 
    /// Label Lots (16.2.7), and Kit Package Lots (16.2.11).
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="LotIdentifier"/>
    /// <seealso cref="ProductInstance"/>
    /// <seealso cref="IngredientInstance"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class LotDistributionParser : ISplSectionParser
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing lot distribution elements.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="ISplSectionParser"/>
        public string SectionName => "lotdistribution";

        /**************************************************************/
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        /// <seealso cref="Label"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses lot distribution elements from an SPL document, creating lot identifiers,
        /// product instances, and ingredient instances with proper relationships.
        /// </summary>
        /// <param name="element">The XElement representing the lot distribution section to parse.</param>
        /// <param name="context">The current parsing context containing the section to link lots to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new LotDistributionParser();
        /// var result = await parser.ParseAsync(lotDistributionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Product instances created: {result.ProductInstancesCreated}");
        ///     Console.WriteLine($"Ingredient instances created: {result.IngredientInstancesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a section context exists
        /// 2. Searches for productInstance elements of various types
        /// 3. Creates LotIdentifier entities for each lot number found
        /// 4. Creates ProductInstance entities for Fill Lots, Label Lots, Package Lots
        /// 5. Creates IngredientInstance entities for Bulk Lots
        /// 6. Maintains proper foreign key relationships between entities
        /// 
        /// The method supports multiple lot types and maintains context isolation.
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
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

            // Validate that we have a valid section context to link lots to
            if (context.CurrentSection?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse lot distribution because no section context exists.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting Lot Distribution XML Elements {context.FileNameInZip}");

                // Find all product instance elements that represent lots
                var productInstanceElements = element.SplFindElements(sc.E.ProductInstance);

                foreach (var productInstanceEl in productInstanceElements)
                {
                    // Determine the type of lot based on context and structure
                    var lotType = determineLotType(productInstanceEl, context);

                    if (string.IsNullOrWhiteSpace(lotType))
                    {
                        context.Logger.LogWarning("Could not determine lot type for productInstance element, skipping.");
                        continue;
                    }

                    // Parse and create lot identifier
                    var lotIdentifier = await getOrCreateLotIdentifierAsync(productInstanceEl, context);
                    if (lotIdentifier?.LotIdentifierID == null)
                    {
                        context.Logger.LogWarning($"Failed to create LotIdentifier for {lotType}, skipping.");
                        continue;
                    }

                    // Create product instance based on lot type
                    if (isFillLot(lotType) || isLabelLot(lotType) || isPackageLot(lotType) || isSalvagedLot(lotType))
                    {
                        var productInstance = await getOrCreateProductInstanceAsync(
                            productInstanceEl, lotIdentifier, lotType, context);

                        if (productInstance?.ProductInstanceID != null)
                        {
                            result.ProductsCreated++;

                            // Parse ingredient instances (bulk lots) for this fill lot
                            if (isFillLot(lotType))
                            {
                                var ingredientCount = await parseAndCreateIngredientInstancesAsync(
                                    productInstanceEl, productInstance, context);
                                result.IngredientsCreated += ingredientCount;
                            }

                            // Parse lot hierarchy relationships (label lots and package lot members)
                            var hierarchyCount = await parseAndCreateLotHierarchiesAsync(
                                productInstanceEl, productInstance, context);
                            result.LotHierarchiesCreated += hierarchyCount;
                        }
                    }
                }

                reportProgress?.Invoke($"Completed Lot Distribution XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing lot distribution: {ex.Message}");
                context.Logger.LogError(ex, "Error processing lot distribution elements.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates lot hierarchies for existing product instances when parent-child relationships are discovered later.
        /// </summary>
        /// <param name="parentInstanceId">The parent ProductInstance ID.</param>
        /// <param name="childInstanceIds">Collection of child ProductInstance IDs.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of LotHierarchy entities successfully created.</returns>
        /// <remarks>
        /// Public method that can be called from other parsers or services when lot relationships
        /// need to be established after initial parsing is complete. This is useful for scenarios
        /// where lot relationships are defined in separate XML sections or documents.
        /// </remarks>
        /// <example>
        /// <code>
        /// // External usage from another service
        /// var lotParser = new LotDistributionParser();
        /// var hierarchyCount = await lotParser.CreateLotHierarchiesAsync(
        ///     parentLotId, labelLotIds, parseContext);
        /// </code>
        /// </example>
        /// <seealso cref="LotHierarchy"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="createMultipleLotHierarchiesAsync"/>
        /// <seealso cref="Label"/>
        public async Task<int> CreateLotHierarchiesAsync(
            int? parentInstanceId,
            IEnumerable<int> childInstanceIds,
            SplParseContext context)
        {
            #region implementation
            // Public wrapper for the private batch creation method
            return await createMultipleLotHierarchiesAsync(parentInstanceId, childInstanceIds, context);
            #endregion
        }

        #region Private Helper Methods

        /**************************************************************/
        /// <summary>
        /// Determines the type of lot based on the XML element context and structure.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A string indicating the lot type: "FillLot", "LabelLot", "PackageLot", or "SalvagedLot".</returns>
        /// <remarks>
        /// Uses element hierarchy and presence of specific child elements to determine lot type.
        /// Fill lots contain ingredient elements, label lots have expiration dates, package lots
        /// are found in kit contexts, and salvaged lots are in salvage reports.
        /// </remarks>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private string? determineLotType(XElement productInstanceEl, SplParseContext context)
        {
            #region implementation
            // Check if this is a salvaged lot (in salvage report context)
            if (isSalvageReportContext(productInstanceEl, context))
            {
                return "SalvagedLot";
            }

            // Check if this is a package lot (kit with multiple licensed products)
            if (isKitPackageLotContext(productInstanceEl))
            {
                return "PackageLot";
            }

            // Check if this has ingredient elements (bulk lots) - indicates Fill Lot
            if (productInstanceEl.SplFindElements(sc.E.Ingredient).Any())
            {
                return "FillLot";
            }

            // Check if this has expiration time - indicates Label Lot
            if (productInstanceEl.GetSplElement(sc.E.ExpirationTime) != null)
            {
                return "LabelLot";
            }

            // Check parent context to determine if this is a member (Label Lot)
            if (productInstanceEl.Parent?.Name.LocalName == sc.E.Member)
            {
                return "LabelLot";
            }

            // Default to Fill Lot if structure is unclear
            return "FillLot";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if the current context represents a salvage report.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>True if this is a salvage report context, otherwise false.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private bool isSalvageReportContext(XElement productInstanceEl, SplParseContext context)
        {
            #region implementation
            // Check document type or section context for salvage indicators
            var documentCode = context.Document?.DocumentCode;
            return !string.IsNullOrEmpty(documentCode) &&
                   documentCode.Contains("salvage", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if this productInstance represents a kit package lot.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element.</param>
        /// <returns>True if this is a kit package lot context, otherwise false.</returns>
        /// <remarks>
        /// Kit package lots are identified by the presence of part elements that reference
        /// other label lots, as specified in SPL IG Section 16.2.11.
        /// </remarks>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private bool isKitPackageLotContext(XElement productInstanceEl)
        {
            #region implementation
            // Kit package lots have part elements that reference other lots
            return productInstanceEl.SplFindElements(sc.E.Part).Any();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the lot type represents a Fill Lot.
        /// </summary>
        /// <param name="lotType">The lot type string to check.</param>
        /// <returns>True if the lot type is "FillLot", otherwise false.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private bool isFillLot(string? lotType)
        {
            #region implementation
            return string.Equals(lotType, "FillLot", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the lot type represents a Label Lot.
        /// </summary>
        /// <param name="lotType">The lot type string to check.</param>
        /// <returns>True if the lot type is "LabelLot", otherwise false.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private bool isLabelLot(string? lotType)
        {
            #region implementation
            return string.Equals(lotType, "LabelLot", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the lot type represents a Package Lot.
        /// </summary>
        /// <param name="lotType">The lot type string to check.</param>
        /// <returns>True if the lot type is "PackageLot", otherwise false.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private bool isPackageLot(string? lotType)
        {
            #region implementation
            return string.Equals(lotType, "PackageLot", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the lot type represents a Salvaged Lot.
        /// </summary>
        /// <param name="lotType">The lot type string to check.</param>
        /// <returns>True if the lot type is "SalvagedLot", otherwise false.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private bool isSalvagedLot(string? lotType)
        {
            #region implementation
            return string.Equals(lotType, "SalvagedLot", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses lot identifier information from the XML element and creates a LotIdentifier entity.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element containing the lot identifier.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created LotIdentifier entity, or null if creation fails.</returns>
        /// <remarks>
        /// Extracts lot number and root OID from the id element according to SPL IG Section 16.2.5.
        /// The root OID is computed based on product item code type (NDC or ISBT 128).
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<LotIdentifier?> getOrCreateLotIdentifierAsync(XElement productInstanceEl, SplParseContext context)
        {
            #region implementation
            if (context?.ServiceProvider == null || context?.Logger == null)
            {
                context?.Logger?.LogWarning("Invalid context.");
                return null;
            }

            var idEl = productInstanceEl.GetSplElement(sc.E.Id);
            if (idEl == null)
            {
                context.Logger.LogWarning("No id element found in productInstance for lot identifier.");
                return null;
            }

            // Extract lot number from extension attribute
            var lotNumber = idEl.GetAttrVal(sc.A.Extension);
            var rootOid = idEl.GetAttrVal(sc.A.Root);

            if (string.IsNullOrWhiteSpace(lotNumber))
            {
                context.Logger.LogWarning("Lot number (extension) is missing from productInstance id element.");
                return null;
            }

            // Validate lot number format according to SPL IG 16.2.5.4
            if (!isValidLotNumber(lotNumber))
            {
                context.Logger.LogWarning($"Invalid lot number format: {lotNumber}. Must contain only digits, uppercase letters, '-', and '/'.");
                return null;
            }

            // Compute or validate the globally unique root OID
            var computedRootOid = await computeGloballyUniqueRootOidAsync(rootOid, context);

            var lotIdentifier = new LotIdentifier
            {
                LotNumber = lotNumber,
                LotRootOID = computedRootOid
            };

            // Check for existing lot identifier to avoid duplicates
            var existingLot = await findExistingLotIdentifierAsync(lotIdentifier, context);
            if (existingLot != null)
            {
                context.Logger.LogDebug($"Found existing LotIdentifier for lot number {lotNumber}");
                return existingLot;
            }

            // Create new lot identifier
            var repo = context.GetRepository<LotIdentifier>();
            await repo.CreateAsync(lotIdentifier);

            context.Logger.LogInformation($"Created LotIdentifier: LotNumber={lotNumber}, RootOID={computedRootOid}");
            return lotIdentifier;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates lot number format according to SPL IG Section 16.2.5.4.
        /// </summary>
        /// <param name="lotNumber">The lot number string to validate.</param>
        /// <returns>True if the lot number format is valid, otherwise false.</returns>
        /// <remarks>
        /// Lot numbers can contain digits, upper case letters, and the characters "-" and "/".
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private bool isValidLotNumber(string lotNumber)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(lotNumber))
                return false;

            // Check each character for valid format
            foreach (char c in lotNumber)
            {
                if (!char.IsDigit(c) && !char.IsUpper(c) && c != '-' && c != '/')
                {
                    return false;
                }
            }
            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the globally unique root OID based on product item code according to SPL IG 16.2.5.6-16.2.5.7.
        /// </summary>
        /// <param name="rootOid">The root OID from the XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The computed globally unique root OID.</returns>
        /// <remarks>
        /// For NDC codes, uses prefix "1.3.6.1.4.1.32366.1.2.10." followed by product code.
        /// For ISBT 128 codes, uses prefix "1.3.6.1.4.1.32366.1.2.13." with base 36 conversion.
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<string?> computeGloballyUniqueRootOidAsync(string? rootOid, SplParseContext context)
        {
            #region implementation
            if (context?.ServiceProvider == null || context?.Logger == null)
            {
                context?.Logger?.LogWarning("Invalid context.");
                return null;
            }

            // If root OID is already provided and follows expected pattern, use it
            if (!string.IsNullOrWhiteSpace(rootOid) &&
                (rootOid.StartsWith("1.3.6.1.4.1.32366.1.2.10.") ||
                 rootOid.StartsWith("1.3.6.1.4.1.32366.1.2.13.")))
            {
                return rootOid;
            }

            // Try to compute based on current product context
            var currentProduct = context.CurrentProduct;
            if (currentProduct == null)
            {
                context.Logger.LogWarning("No current product context available for computing lot root OID.");
                return rootOid; // Return as-is if we can't compute
            }

            // Get product identifier to determine if it's NDC or ISBT 128
            var productCode = await getProductCodeAsync(currentProduct, context);
            if (string.IsNullOrWhiteSpace(productCode))
            {
                return rootOid; // Return as-is if we can't determine product code
            }

            // Compute based on product code type
            if (isNdcCode(productCode))
            {
                return computeNdcRootOid(productCode);
            }
            else if (isIsbt128Code(productCode))
            {
                return computeIsbt128RootOid(productCode);
            }

            return rootOid; // Return as-is if code type is unrecognized
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the product code from the current product context by querying ProductIdentifier table.
        /// </summary>
        /// <param name="product">The current product entity.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The product code string, or null if not found.</returns>
        /// <remarks>
        /// Searches for NDC and ISBT 128 product codes in order of preference.
        /// NDC codes are preferred for pharmaceutical products, while ISBT 128 codes
        /// are used for blood and biological products. Returns the first valid code found.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<string?> getProductCodeAsync(Product product, SplParseContext context)
        {
            #region implementation
            if (product?.ProductID == null || context?.ServiceProvider == null || context.Logger == null)
            {
                context?.Logger?.LogWarning("Invalid product or context provided for product code lookup.");
                return null;
            }

            try
            {
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var productIdentifierDbSet = dbContext.Set<ProductIdentifier>();

                // Get all identifiers for this product
                var productIdentifiers = await productIdentifierDbSet
                    .Where(pi => pi.ProductID == product.ProductID)
                    .ToListAsync();

                if (!productIdentifiers.Any())
                {
                    context.Logger.LogDebug($"No ProductIdentifiers found for ProductID {product.ProductID}");
                    return null;
                }

                // Priority 1: Look for NDC codes (most common for pharmaceuticals)
                var ndcIdentifier = findIdentifierByOidPattern(productIdentifiers, "2.16.840.1.113883.6.69");
                if (ndcIdentifier != null)
                {
                    context.Logger.LogDebug($"Found NDC product code: {ndcIdentifier.IdentifierValue}");
                    return ndcIdentifier.IdentifierValue;
                }

                // Priority 2: Look for ISBT 128 codes (blood and biological products)
                var isbtIdentifier = findIdentifierByOidPattern(productIdentifiers, "2.16.840.1.113883.6.18");
                if (isbtIdentifier != null)
                {
                    context.Logger.LogDebug($"Found ISBT 128 product code: {isbtIdentifier.IdentifierValue}");
                    return isbtIdentifier.IdentifierValue;
                }

                // Priority 3: Look for GS1 GTIN codes (devices and some products)
                var gs1Identifier = findIdentifierByOidPattern(productIdentifiers, "1.3.160");
                if (gs1Identifier != null)
                {
                    context.Logger.LogDebug($"Found GS1 GTIN product code: {gs1Identifier.IdentifierValue}");
                    return gs1Identifier.IdentifierValue;
                }

                // Priority 4: Look for any identifier that could be used for lot OID computation
                var firstValidIdentifier = productIdentifiers
                    .Where(pi => !string.IsNullOrWhiteSpace(pi.IdentifierValue))
                    .OrderBy(pi => pi.IdentifierType) // Alphabetical order for consistency
                    .FirstOrDefault();

                if (firstValidIdentifier != null)
                {
                    context.Logger.LogDebug($"Using fallback product code: {firstValidIdentifier.IdentifierValue} (Type: {firstValidIdentifier.IdentifierType})");
                    return firstValidIdentifier.IdentifierValue;
                }

                context.Logger.LogWarning($"No valid product identifiers found for ProductID {product.ProductID}");
                return null;
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Error retrieving product code for ProductID {product.ProductID}");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds a ProductIdentifier by matching the system OID pattern.
        /// </summary>
        /// <param name="identifiers">The collection of product identifiers to search.</param>
        /// <param name="oidPattern">The OID pattern to match against.</param>
        /// <returns>The matching ProductIdentifier, or null if not found.</returns>
        /// <remarks>
        /// Performs case-insensitive matching on the IdentifierSystemOID property.
        /// Returns the first identifier that matches the specified OID pattern.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private ProductIdentifier? findIdentifierByOidPattern(IEnumerable<ProductIdentifier> identifiers, string oidPattern)
        {
            #region implementation
            return identifiers.FirstOrDefault(pi =>
                !string.IsNullOrWhiteSpace(pi.IdentifierSystemOID) &&
                pi.IdentifierSystemOID.Equals(oidPattern, StringComparison.OrdinalIgnoreCase));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a product code is an NDC format.
        /// </summary>
        /// <param name="productCode">The product code to check.</param>
        /// <returns>True if the code appears to be NDC format, otherwise false.</returns>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private bool isNdcCode(string productCode)
        {
            #region implementation
            // NDC codes are typically 10-11 digits with dashes (e.g., "0001-0123-04")
            return !string.IsNullOrWhiteSpace(productCode) &&
                   productCode.Contains("-") &&
                   productCode.Replace("-", "").All(char.IsDigit);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a product code is an ISBT 128 format.
        /// </summary>
        /// <param name="productCode">The product code to check.</param>
        /// <returns>True if the code appears to be ISBT 128 format, otherwise false.</returns>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private bool isIsbt128Code(string productCode)
        {
            #region implementation
            // ISBT 128 codes contain alphanumeric characters (e.g., "W0123-E0404")
            return !string.IsNullOrWhiteSpace(productCode) &&
                   productCode.Contains("-") &&
                   productCode.Any(char.IsLetter);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the NDC-based root OID according to SPL IG 16.2.5.6.
        /// </summary>
        /// <param name="ndcCode">The NDC product code.</param>
        /// <returns>The computed root OID for NDC codes.</returns>
        /// <remarks>
        /// Uses fixed prefix "1.3.6.1.4.1.32366.1.2.10." followed by NDC product code
        /// with dashes removed and initial zeroes from labeler code removed.
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private string computeNdcRootOid(string ndcCode)
        {
            #region implementation
            // Remove dashes and convert according to SPL IG 16.2.5.6
            var cleanNdc = ndcCode.Replace("-", "");

            // Remove initial zeroes from labeler code segment (first 4-5 digits)
            // Example: "0001-0123" becomes "10123"
            if (cleanNdc.Length >= 4)
            {
                var labelerPart = cleanNdc.Substring(0, 4).TrimStart('0');
                var remainderPart = cleanNdc.Substring(4);
                var processedNdc = labelerPart + remainderPart;

                return $"1.3.6.1.4.1.32366.1.2.10.{processedNdc}";
            }

            return $"1.3.6.1.4.1.32366.1.2.10.{cleanNdc}";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the ISBT 128-based root OID according to SPL IG 16.2.5.7.
        /// </summary>
        /// <param name="isbtCode">The ISBT 128 product code.</param>
        /// <returns>The computed root OID for ISBT 128 codes.</returns>
        /// <remarks>
        /// Uses fixed prefix "1.3.6.1.4.1.32366.1.2.13." followed by base 36 conversion
        /// of facility identification and product code components.
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private string computeIsbt128RootOid(string isbtCode)
        {
            #region implementation
            var parts = isbtCode.Split('-');
            if (parts.Length < 2)
                return $"1.3.6.1.4.1.32366.1.2.13.{isbtCode}"; // Fallback

            try
            {
                // Convert facility ID (e.g., "W0123") from base 36
                var facilityId = convertBase36ToDecimal(parts[0]);

                // Convert product code (e.g., "E0404") from base 36
                var productCode = convertBase36ToDecimal(parts[1]);

                return $"1.3.6.1.4.1.32366.1.2.13.{facilityId}.{productCode}";
            }
            catch (Exception)
            {
                // Fallback if base 36 conversion fails
                return $"1.3.6.1.4.1.32366.1.2.13.{isbtCode}";
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a base 36 string to decimal according to SPL IG 16.2.5.7.
        /// </summary>
        /// <param name="base36String">The base 36 string to convert.</param>
        /// <returns>The decimal equivalent of the base 36 string.</returns>
        /// <remarks>
        /// Uses digits 0-9 and letters A-Z, with letter values calculated as 
        /// ordinal position + 9 (A=10, B=11, ..., Z=35).
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="Label"/>
        private long convertBase36ToDecimal(string base36String)
        {
            #region implementation
            long result = 0;
            long baseValue = 1;

            // Process from right to left
            for (int i = base36String.Length - 1; i >= 0; i--)
            {
                char c = base36String[i];
                long digitValue;

                if (char.IsDigit(c))
                {
                    digitValue = c - '0'; // 0-9
                }
                else if (char.IsLetter(c))
                {
                    // A=10, B=11, ..., Z=35 (ordinal position + 9)
                    digitValue = char.ToUpper(c) - 'A' + 10;
                }
                else
                {
                    throw new ArgumentException($"Invalid base 36 character: {c}");
                }

                result += digitValue * baseValue;
                baseValue *= 36;
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for an existing LotIdentifier to avoid duplicates.
        /// </summary>
        /// <param name="lotIdentifier">The lot identifier to search for.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The existing LotIdentifier if found, otherwise null.</returns>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<LotIdentifier?> findExistingLotIdentifierAsync(LotIdentifier lotIdentifier, SplParseContext context)
        {
            #region implementation
            if (context.ServiceProvider == null)
                return null;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lotDbSet = dbContext.Set<LotIdentifier>();

            // Search by lot number and root OID
            return await lotDbSet.FirstOrDefaultAsync(l =>
                l.LotNumber == lotIdentifier.LotNumber &&
                l.LotRootOID == lotIdentifier.LotRootOID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates a ProductInstance entity from the XML element.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element.</param>
        /// <param name="lotIdentifier">The associated lot identifier.</param>
        /// <param name="instanceType">The type of product instance.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created ProductInstance entity, or null if creation fails.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<ProductInstance?> getOrCreateProductInstanceAsync(
            XElement productInstanceEl,
            LotIdentifier lotIdentifier,
            string instanceType,
            SplParseContext context)
        {
            #region implementation

            // Parse expiration date for label lots
            DateTime? expirationDate = null;

            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            if (isLabelLot(instanceType))
            {
                expirationDate = parseExpirationDate(productInstanceEl);
            }

            var productInstance = new ProductInstance
            {
                ProductID = context.CurrentProduct?.ProductID,
                InstanceType = instanceType,
                LotIdentifierID = lotIdentifier.LotIdentifierID,
                ExpirationDate = expirationDate
            };

            // Check for existing product instance to avoid duplicates
            var existingInstance = await findExistingProductInstanceAsync(productInstance, context);
            if (existingInstance != null)
            {
                context.Logger.LogDebug($"Found existing ProductInstance for lot {lotIdentifier.LotNumber}");
                return existingInstance;
            }

            // Create new product instance
            var repo = context.GetRepository<ProductInstance>();
            await repo.CreateAsync(productInstance);

            context.Logger.LogInformation($"Created ProductInstance: Type={instanceType}, LotNumber={lotIdentifier.LotNumber}");
            return productInstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses expiration date from the productInstance XML element.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element containing expiration information.</param>
        /// <returns>The parsed expiration date, or null if not found or invalid.</returns>
        /// <remarks>
        /// Extracts expiration date from the high value of expirationTime element
        /// according to SPL IG Section 16.2.7.5-16.2.7.6. Date must have at least
        /// month precision in YYYYMM format.
        /// </remarks>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="Label"/>
        private DateTime? parseExpirationDate(XElement productInstanceEl)
        {
            #region implementation
            var expirationTimeEl = productInstanceEl.GetSplElement(sc.E.ExpirationTime);
            var highEl = expirationTimeEl?.GetSplElement(sc.E.High);
            var dateValue = highEl?.GetAttrVal(sc.A.Value);

            if (string.IsNullOrWhiteSpace(dateValue))
                return null;

            // Parse using utility method that handles SPL date formats
            return Util.ParseNullableDateTime(dateValue);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for an existing ProductInstance to avoid duplicates.
        /// </summary>
        /// <param name="productInstance">The product instance to search for.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The existing ProductInstance if found, otherwise null.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<ProductInstance?> findExistingProductInstanceAsync(ProductInstance productInstance, SplParseContext context)
        {
            #region implementation

            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var instanceDbSet = dbContext.Set<ProductInstance>();

            // Search by product ID, lot identifier ID, and instance type
            return await instanceDbSet.FirstOrDefaultAsync(pi =>
                pi.ProductID == productInstance.ProductID &&
                pi.LotIdentifierID == productInstance.LotIdentifierID &&
                pi.InstanceType == productInstance.InstanceType);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates IngredientInstance entities for bulk lots associated with a fill lot.
        /// </summary>
        /// <param name="fillLotEl">The fill lot productInstance XML element.</param>
        /// <param name="fillLotInstance">The created fill lot ProductInstance.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of IngredientInstance entities created.</returns>
        /// <remarks>
        /// Processes ingredient elements within the fill lot to create bulk lot instances
        /// according to SPL IG Section 16.2.6. Each ingredient represents a bulk lot
        /// that contributes to the fill lot.
        /// </remarks>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndCreateIngredientInstancesAsync(
            XElement fillLotEl,
            ProductInstance fillLotInstance,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return count;

            // Find all ingredient elements representing bulk lots
            var ingredientElements = fillLotEl.SplFindElements(sc.E.Ingredient);

            foreach (var ingredientEl in ingredientElements)
            {
                try
                {
                    var ingredientInstance = await parseAndCreateSingleIngredientInstanceAsync(
                        ingredientEl, fillLotInstance, context);

                    if (ingredientInstance != null)
                    {
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, "Error creating IngredientInstance for fill lot {FillLotID}",
                        fillLotInstance.ProductInstanceID);
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates a single IngredientInstance entity from an ingredient XML element.
        /// </summary>
        /// <param name="ingredientEl">The ingredient XML element representing a bulk lot.</param>
        /// <param name="fillLotInstance">The parent fill lot instance.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created IngredientInstance entity, or null if creation fails.</returns>
        /// <remarks>
        /// Extracts bulk lot information including lot identifier, ingredient substance,
        /// and manufacturer organization according to SPL IG Section 16.2.6.
        /// </remarks>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientInstance?> parseAndCreateSingleIngredientInstanceAsync(
            XElement ingredientEl,
            ProductInstance fillLotInstance,
            SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            // Parse the ingredientProductInstance element
            var ingredientProductInstanceEl = ingredientEl.GetSplElement(sc.E.IngredientProductInstance);
            if (ingredientProductInstanceEl == null)
            {
                context.Logger.LogWarning("No ingredientProductInstance found in ingredient element for bulk lot.");
                return null;
            }

            // Parse bulk lot identifier
            var bulkLotIdentifier = await getOrCreateLotIdentifierAsync(ingredientProductInstanceEl, context);
            if (bulkLotIdentifier?.LotIdentifierID == null)
            {
                context.Logger.LogWarning("Failed to create bulk lot identifier for ingredient instance.");
                return null;
            }

            // Get ingredient substance ID
            var ingredientSubstanceId = await getIngredientSubstanceIdAsync(ingredientEl, context);

            // Get manufacturer organization ID
            var manufacturerOrgId = await getManufacturerOrganizationIdAsync(ingredientEl, context);

            var ingredientInstance = new IngredientInstance
            {
                FillLotInstanceID = fillLotInstance.ProductInstanceID,
                IngredientSubstanceID = ingredientSubstanceId,
                LotIdentifierID = bulkLotIdentifier.LotIdentifierID,
                ManufacturerOrganizationID = manufacturerOrgId
            };

            // Check for existing ingredient instance to avoid duplicates
            var existingIngredient = await findExistingIngredientInstanceAsync(ingredientInstance, context);
            if (existingIngredient != null)
            {
                context.Logger.LogDebug($"Found existing IngredientInstance for bulk lot {bulkLotIdentifier.LotNumber}");
                return existingIngredient;
            }

            // Create new ingredient instance
            var repo = context.GetRepository<IngredientInstance>();
            await repo.CreateAsync(ingredientInstance);

            context.Logger.LogInformation($"Created IngredientInstance: BulkLot={bulkLotIdentifier.LotNumber}, FillLot={fillLotInstance.ProductInstanceID}");
            return ingredientInstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the ingredient substance ID from the ingredient XML element.
        /// </summary>
        /// <param name="ingredientEl">The ingredient XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The ingredient substance ID, or null if not found.</returns>
        /// <remarks>
        /// Extracts the ingredient substance reference from the asInstanceOfKind/kindOfMaterialKind
        /// structure and looks up the corresponding IngredientSubstance entity.
        /// </remarks>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> getIngredientSubstanceIdAsync(XElement ingredientEl, SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            // Navigate to the substance code element
            var substanceCodeEl = ingredientEl
                .SplElement(sc.E.IngredientProductInstance)?
                .SplElement(sc.E.AsInstanceOfKind)?
                .SplElement(sc.E.KindOfMaterialKind)?
                .SplElement(sc.E.Code);

            if (substanceCodeEl == null)
            {
                context.Logger.LogWarning("No substance code found in ingredient element.");
                return null;
            }

            var substanceCode = substanceCodeEl.GetAttrVal(sc.A.CodeValue);
            var substanceName = ingredientEl
                .SplElement(sc.E.IngredientProductInstance)?
                .SplElement(sc.E.AsInstanceOfKind)?
                .SplElement(sc.E.KindOfMaterialKind)?
                .GetSplElementVal(sc.E.Name);

            if (string.IsNullOrWhiteSpace(substanceCode))
            {
                context.Logger.LogWarning("Substance code is missing from ingredient element.");
                return null;
            }

            // Look up existing ingredient substance by UNII or name
            return await findIngredientSubstanceIdAsync(substanceCode, substanceName, context);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds the IngredientSubstance ID by UNII code or substance name.
        /// </summary>
        /// <param name="substanceCode">The substance UNII code.</param>
        /// <param name="substanceName">The substance name.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The IngredientSubstance ID, or null if not found.</returns>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> findIngredientSubstanceIdAsync(string substanceCode, string? substanceName, SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var substanceDbSet = dbContext.Set<IngredientSubstance>();

            // First try to find by UNII code
            var substance = await substanceDbSet.FirstOrDefaultAsync(s => s.UNII == substanceCode);

            // If not found by UNII, try by name
            if (substance == null && !string.IsNullOrWhiteSpace(substanceName))
            {
                substance = await substanceDbSet.FirstOrDefaultAsync(s =>
                    s.SubstanceName != null && s.SubstanceName.ToLower() == substanceName.ToLower());
            }

            if (substance == null)
            {
                context.Logger.LogWarning($"IngredientSubstance not found for code {substanceCode} or name {substanceName}");
            }

            return substance?.IngredientSubstanceID;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the manufacturer organization ID from the ingredient XML element.
        /// </summary>
        /// <param name="ingredientEl">The ingredient XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The manufacturer organization ID, or null if not found.</returns>
        /// <remarks>
        /// Extracts manufacturer information from the subjectOf/productEvent/performer structure
        /// according to SPL IG Section 16.2.6.10-16.2.6.14.
        /// </remarks>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> getManufacturerOrganizationIdAsync(XElement ingredientEl, SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            // Navigate to the manufacturer organization element
            var manufacturerOrgEl = ingredientEl
                .SplElement(sc.E.IngredientProductInstance)?
                .SplElement(sc.E.SubjectOf)?
                .SplElement(sc.E.ProductEvent)?
                .SplElement(sc.E.Performer)?
                .SplElement(sc.E.AssignedEntity)?
                .SplElement(sc.E.RepresentedOrganization);

            if (manufacturerOrgEl == null)
            {
                context.Logger.LogWarning("No manufacturer organization found in ingredient element.");
                return null;
            }

            // Get DUNS number from id element
            var dunsNumber = manufacturerOrgEl.GetSplElementAttrVal(sc.E.Id, sc.A.Extension);
            var rootOid = manufacturerOrgEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);

            // Validate DUNS number format and root OID
            if (string.IsNullOrWhiteSpace(dunsNumber) || rootOid != "1.3.6.1.4.1.519.1")
            {
                context.Logger.LogWarning($"Invalid DUNS number or root OID for manufacturer: DUNS={dunsNumber}, Root={rootOid}");
                return null;
            }

            // Look up organization by DUNS number
            return await findOrganizationByDunsAsync(dunsNumber, context);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an organization by its DUNS number.
        /// </summary>
        /// <param name="dunsNumber">The DUNS number to search for.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The organization ID, or null if not found.</returns>
        /// <seealso cref="OrganizationIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> findOrganizationByDunsAsync(string dunsNumber, SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgDbSet = dbContext.Set<OrganizationIdentifier>();

            // Find organization by DUNS number
            // Note: This assumes Organization has a DunsNumber property - may need to adjust based on actual schema
            var organization = await orgDbSet.FirstOrDefaultAsync(o =>
                o.IdentifierValue == dunsNumber); // TODO: Verify property name in Organization model

            if (organization == null)
            {
                context.Logger.LogWarning($"Organization not found for DUNS number {dunsNumber}");
            }

            return organization?.OrganizationID;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for an existing IngredientInstance to avoid duplicates.
        /// </summary>
        /// <param name="ingredientInstance">The ingredient instance to search for.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The existing IngredientInstance if found, otherwise null.</returns>
        /// <seealso cref="IngredientInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientInstance?> findExistingIngredientInstanceAsync(IngredientInstance ingredientInstance, SplParseContext context)
        {
            #region implementation
            if (context.ServiceProvider == null)
                return null;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ingredientDbSet = dbContext.Set<IngredientInstance>();

            // Search by fill lot instance ID, ingredient substance ID, and lot identifier ID
            return await ingredientDbSet.FirstOrDefaultAsync(ii =>
                ii.FillLotInstanceID == ingredientInstance.FillLotInstanceID &&
                ii.IngredientSubstanceID == ingredientInstance.IngredientSubstanceID &&
                ii.LotIdentifierID == ingredientInstance.LotIdentifierID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses lot identifier information from the XML element and creates a LotIdentifier entity.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element containing the lot identifier.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created LotIdentifier entity, or null if creation fails.</returns>
        /// <remarks>
        /// Extracts lot number and root OID from the id element according to SPL IG Section 16.2.5.
        /// The root OID is computed based on product item code type (NDC or ISBT 128).
        /// </remarks>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<LotIdentifier?> parseAndCreateLotIdentifierAsync(XElement productInstanceEl, SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            var idEl = productInstanceEl.GetSplElement(sc.E.Id);
            if (idEl == null)
            {
                context.Logger.LogWarning("No id element found in productInstance for lot identifier.");
                return null;
            }

            // Extract lot number from extension attribute
            var lotNumber = idEl.GetAttrVal(sc.A.Extension);
            var rootOid = idEl.GetAttrVal(sc.A.Root);

            if (string.IsNullOrWhiteSpace(lotNumber))
            {
                context.Logger.LogWarning("Lot number (extension) is missing from productInstance id element.");
                return null;
            }

            // Validate lot number format according to SPL IG 16.2.5.4
            if (!isValidLotNumber(lotNumber))
            {
                context.Logger.LogWarning($"Invalid lot number format: {lotNumber}. Must contain only digits, uppercase letters, '-', and '/'.");
                return null;
            }

            // Compute or validate the globally unique root OID
            var computedRootOid = await computeGloballyUniqueRootOidAsync(rootOid, context);

            var lotIdentifier = new LotIdentifier
            {
                LotNumber = lotNumber,
                LotRootOID = computedRootOid
            };

            // Check for existing lot identifier to avoid duplicates
            var existingLot = await findExistingLotIdentifierAsync(lotIdentifier, context);
            if (existingLot != null)
            {
                context.Logger.LogDebug($"Found existing LotIdentifier for lot number {lotNumber}");
                return existingLot;
            }

            // Create new lot identifier
            var repo = context.GetRepository<LotIdentifier>();
            await repo.CreateAsync(lotIdentifier);

            context.Logger.LogInformation($"Created LotIdentifier: LotNumber={lotNumber}, RootOID={computedRootOid}");
            return lotIdentifier;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates LotHierarchy relationships between parent lots and their member label lots.
        /// </summary>
        /// <param name="parentInstanceEl">The parent lot productInstance XML element (Fill Lot or Package Lot).</param>
        /// <param name="parentInstance">The created parent ProductInstance entity.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of LotHierarchy entities created.</returns>
        /// <remarks>
        /// Processes member elements within fill lots and package lots to create label lot hierarchies
        /// according to SPL IG Sections 16.2.7 and 16.2.11. Each member represents a label lot
        /// that is a portion of the parent fill lot or a component of a package lot.
        /// 
        /// Uses batch processing when multiple label lots are found for improved performance.
        /// </remarks>
        /// <seealso cref="LotHierarchy"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="createMultipleLotHierarchiesAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndCreateLotHierarchiesAsync(
            XElement parentInstanceEl,
            ProductInstance parentInstance,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return count;

            // Find all member elements representing label lots
            var memberElements = parentInstanceEl.SplFindElements(sc.E.Member);

            if (!memberElements.Any())
            {
                // No member elements found - this is normal for some lot types
                return count;
            }

            // Collect child instance IDs for batch processing
            var childInstanceIds = new List<int>();

            foreach (var memberEl in memberElements)
            {
                try
                {
                    var memberInstanceEl = memberEl.GetSplElement(sc.E.MemberProductInstance);
                    if (memberInstanceEl == null)
                    {
                        context.Logger.LogWarning("No memberProductInstance found in member element.");
                        continue;
                    }

                    // Create the child label lot instance
                    var childLotIdentifier = await parseAndCreateLotIdentifierAsync(memberInstanceEl, context);
                    if (childLotIdentifier?.LotIdentifierID == null)
                    {
                        context.Logger.LogWarning("Failed to create lot identifier for member label lot.");
                        continue;
                    }

                    var childInstance = await parseAndCreateProductInstanceAsync(
                        memberInstanceEl, childLotIdentifier, "LabelLot", context);

                    if (childInstance?.ProductInstanceID == null)
                    {
                        context.Logger.LogWarning("Failed to create ProductInstance for member label lot.");
                        continue;
                    }

                    // Add to collection for batch processing
                    childInstanceIds.Add(childInstance.ProductInstanceID.Value);
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, "Error creating child ProductInstance for parent instance {ParentInstanceID}",
                        parentInstance.ProductInstanceID);
                }
            }

            // Use batch processing if we have multiple children, otherwise process individually
            if (childInstanceIds.Count > 1)
            {
                context.Logger.LogDebug($"Using batch processing for {childInstanceIds.Count} LotHierarchy relationships for parent {parentInstance.ProductInstanceID}");

                count = await createMultipleLotHierarchiesAsync(
                    parentInstance.ProductInstanceID, childInstanceIds, context);
            }
            else if (childInstanceIds.Count == 1)
            {
                // Single hierarchy - use individual method
                var hierarchy = await getOrCreateLotHierarchyAsync(
                    parentInstance.ProductInstanceID, childInstanceIds[0], context);
                count = hierarchy != null ? 1 : 0;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates a ProductInstance entity from the XML element.
        /// </summary>
        /// <param name="productInstanceEl">The productInstance XML element.</param>
        /// <param name="lotIdentifier">The associated lot identifier.</param>
        /// <param name="instanceType">The type of product instance.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created ProductInstance entity, or null if creation fails.</returns>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="LotIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<ProductInstance?> parseAndCreateProductInstanceAsync(
            XElement productInstanceEl,
            LotIdentifier lotIdentifier,
            string instanceType,
            SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            // Parse expiration date for label lots
            DateTime? expirationDate = null;
            if (isLabelLot(instanceType))
            {
                expirationDate = parseExpirationDate(productInstanceEl);
            }

            var productInstance = new ProductInstance
            {
                ProductID = context.CurrentProduct?.ProductID,
                InstanceType = instanceType,
                LotIdentifierID = lotIdentifier.LotIdentifierID,
                ExpirationDate = expirationDate
            };

            // Check for existing product instance to avoid duplicates
            var existingInstance = await findExistingProductInstanceAsync(productInstance, context);
            if (existingInstance != null)
            {
                context.Logger.LogDebug($"Found existing ProductInstance for lot {lotIdentifier.LotNumber}");
                return existingInstance;
            }

            // Create new product instance
            var repo = context.GetRepository<ProductInstance>();
            await repo.CreateAsync(productInstance);

            context.Logger.LogInformation($"Created ProductInstance: Type={instanceType}, LotNumber={lotIdentifier.LotNumber}");
            return productInstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing LotHierarchy or creates and saves it if not found.
        /// </summary>
        /// <param name="parentInstanceId">The ID of the parent ProductInstance (Fill Lot or Package Lot).</param>
        /// <param name="childInstanceId">The ID of the child ProductInstance (Label Lot).</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The existing or newly created LotHierarchy entity, or null if creation fails.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate hierarchy relationships.
        /// Uses a composite key match on parent and child instance IDs for uniqueness.
        /// Validates that both parent and child instances exist before creating the relationship.
        /// </remarks>
        /// <seealso cref="LotHierarchy"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<LotHierarchy?> getOrCreateLotHierarchyAsync(
            int? parentInstanceId,
            int? childInstanceId,
            SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            // Validate input parameters
            if (parentInstanceId == null || childInstanceId == null)
            {
                context.Logger.LogWarning("Parent or child instance ID is null, cannot create LotHierarchy.");
                return null;
            }

            if (context.ServiceProvider == null || context.Logger == null)
            {
                context.Logger?.LogError("Context or ServiceProvider is null for LotHierarchy creation.");
                return null;
            }

            try
            {
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hierarchyDbSet = dbContext.Set<LotHierarchy>();

                // Check for existing hierarchy relationship to avoid duplicates
                var existingHierarchy = await findExistingLotHierarchyAsync(
                    parentInstanceId, childInstanceId, context);

                if (existingHierarchy != null)
                {
                    context.Logger.LogDebug($"Found existing LotHierarchy: Parent={parentInstanceId}, Child={childInstanceId}");
                    return existingHierarchy;
                }

                // Validate that both parent and child instances exist
                if (!await validateProductInstancesExistAsync(parentInstanceId, childInstanceId, context))
                {
                    context.Logger.LogWarning($"Parent or child ProductInstance does not exist: Parent={parentInstanceId}, Child={childInstanceId}");
                    return null;
                }

                // Create new lot hierarchy entity
                var newHierarchy = new LotHierarchy
                {
                    ParentInstanceID = parentInstanceId,
                    ChildInstanceID = childInstanceId
                };

                // Save to database
                hierarchyDbSet.Add(newHierarchy);
                await dbContext.SaveChangesAsync();

                context.Logger.LogInformation($"Created LotHierarchy: ID={newHierarchy.LotHierarchyID}, Parent={parentInstanceId}, Child={childInstanceId}");
                return newHierarchy;
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Error creating LotHierarchy: Parent={parentInstanceId}, Child={childInstanceId}");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for an existing LotHierarchy to avoid duplicates.
        /// </summary>
        /// <param name="parentInstanceId">The parent ProductInstance ID to search for.</param>
        /// <param name="childInstanceId">The child ProductInstance ID to search for.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The existing LotHierarchy if found, otherwise null.</returns>
        /// <remarks>
        /// Uses a composite key match on ParentInstanceID and ChildInstanceID for uniqueness.
        /// This prevents duplicate relationships between the same parent and child lots.
        /// </remarks>
        /// <seealso cref="LotHierarchy"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<LotHierarchy?> findExistingLotHierarchyAsync(
            int? parentInstanceId,
            int? childInstanceId,
            SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return null;

            try
            {
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hierarchyDbSet = dbContext.Set<LotHierarchy>();

                // Search by parent and child instance IDs
                return await hierarchyDbSet.FirstOrDefaultAsync(lh =>
                    lh.ParentInstanceID == parentInstanceId &&
                    lh.ChildInstanceID == childInstanceId);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Error searching for existing LotHierarchy: Parent={parentInstanceId}, Child={childInstanceId}");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that both parent and child ProductInstance entities exist in the database.
        /// </summary>
        /// <param name="parentInstanceId">The parent ProductInstance ID to validate.</param>
        /// <param name="childInstanceId">The child ProductInstance ID to validate.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>True if both instances exist, otherwise false.</returns>
        /// <remarks>
        /// Performs referential integrity validation before creating LotHierarchy relationships.
        /// This ensures that the foreign key constraints will be satisfied.
        /// </remarks>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<bool> validateProductInstancesExistAsync(
            int? parentInstanceId,
            int? childInstanceId,
            SplParseContext context)
        {
            #region implementation
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return false;

            try
            {
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var instanceDbSet = dbContext.Set<ProductInstance>();

                // Check if parent instance exists
                var parentExists = await instanceDbSet.AnyAsync(pi => pi.ProductInstanceID == parentInstanceId);
                if (!parentExists)
                {
                    context.Logger.LogWarning($"Parent ProductInstance {parentInstanceId} does not exist.");
                    return false;
                }

                // Check if child instance exists
                var childExists = await instanceDbSet.AnyAsync(pi => pi.ProductInstanceID == childInstanceId);
                if (!childExists)
                {
                    context.Logger.LogWarning($"Child ProductInstance {childInstanceId} does not exist.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Error validating ProductInstance existence: Parent={parentInstanceId}, Child={childInstanceId}");
                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates multiple LotHierarchy relationships for a collection of child instances.
        /// </summary>
        /// <param name="parentInstanceId">The parent ProductInstance ID.</param>
        /// <param name="childInstanceIds">Collection of child ProductInstance IDs.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of LotHierarchy entities successfully created.</returns>
        /// <remarks>
        /// Batch creation method for scenarios where one parent lot has multiple label lots.
        /// Each relationship is created independently to ensure partial success if some fail.
        /// </remarks>
        /// <seealso cref="LotHierarchy"/>
        /// <seealso cref="ProductInstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> createMultipleLotHierarchiesAsync(
            int? parentInstanceId,
            IEnumerable<int> childInstanceIds,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return count;

            if (parentInstanceId == null || !childInstanceIds.Any())
            {
                context.Logger.LogWarning("Invalid parent ID or no child IDs provided for batch LotHierarchy creation.");
                return count;
            }

            foreach (var childInstanceId in childInstanceIds)
            {
                try
                {
                    var hierarchy = await getOrCreateLotHierarchyAsync(parentInstanceId, childInstanceId, context);
                    if (hierarchy != null)
                    {
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, $"Error creating LotHierarchy in batch: Parent={parentInstanceId}, Child={childInstanceId}");
                    // Continue with next child instead of failing entire batch
                }
            }

            context.Logger.LogInformation($"Created {count} LotHierarchy relationships for parent {parentInstanceId}");
            return count;
            #endregion
        }

        #endregion
    }
}
