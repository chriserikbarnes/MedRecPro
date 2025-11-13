
﻿using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Helpers;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses product-to-product and product-to-organization relationships, such as kit components,
    /// assembly parts, and responsible person links.
    /// </summary>
    /// <remarks>
    /// This parser is unique because it orchestrates calls back to the main `ManufacturedProductParser`
    /// to parse entire sub-products (like kit parts). It requires a reference to the calling parser
    /// to handle this recursion. It assumes `SplParseContext.CurrentProduct` is set.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="ProductPart"/>
    /// <seealso cref="PartOfAssembly"/>
    /// <seealso cref="ResponsiblePersonLink"/>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="SplParseContext"/>
    public class ProductRelationshipParser : ISplSectionParser
    {
        #region implementation
        private readonly ManufacturedProductParser _mainProductParser;

        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "productrelationship";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ProductRelationshipParser"/> class.
        /// </summary>
        /// <param name="mainProductParser">A reference to the main product parser for handling recursive parsing of sub-products.</param>
        public ProductRelationshipParser(ManufacturedProductParser mainProductParser)
        {
            #region implementation
            _mainProductParser = mainProductParser;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a parent XML element to extract and save all product relationship entities.
        /// </summary>
        /// <param name="element">The XElement (e.g., manufacturedProduct) containing the relationship data.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of responsible person links, kit parts, and assembly parts.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();
            var product = context.CurrentProduct;

            if (product?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse product relationships because no product context exists.");
                context?.Logger?.LogError("ProductRelationshipParser was called without a valid product in the context.");
                return result;
            }

            // --- PARSE RESPONSIBLE PERSON LINK (for cosmetics) ---
            reportProgress?.Invoke($"Starting Responsible Person XML Elements {context.FileNameInZip}");
            var responsiblePersonLinksCreated = await parseAndSaveResponsiblePersonLinkAsync(element, product, context);
            result.ProductElementsCreated += responsiblePersonLinksCreated;

            // --- PARSE KIT PARTS (if any) ---
            reportProgress?.Invoke($"Starting Kit/Part XML Elements {context.FileNameInZip}");
            var kitParsingResult = await parseAndSaveProductPartsAsync(element, product, context, reportProgress);
            result.MergeFrom(kitParsingResult);

            // --- PARSE PART OF ASSEMBLY (if any) ---
            reportProgress?.Invoke($"Starting Part of Assembly XML Elements {context.FileNameInZip}");
            var assemblyParsingResult = await parseAndSavePartOfAssemblyAsync(element, product, context, reportProgress);
            result.MergeFrom(assemblyParsingResult);

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [part] elements for a given kit product, creating the part products and linking them.
        /// </summary>
        /// <param name="parentEl">The parent XElement (usually [manufacturedProduct]) containing [part] elements.</param>
        /// <param name="kitProduct">The parent Product entity representing the kit.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult aggregating the results of parsing all parts.</returns>
        /// <seealso cref="ProductPart"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        private async Task<SplParseResult> parseAndSaveProductPartsAsync(
            XElement parentEl,
            Product kitProduct,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            if (context?.ServiceProvider == null || context.Logger == null)
            {
                return new SplParseResult();
            }

            var aggregateResult = new SplParseResult();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var partEl in parentEl.SplElements(sc.E.Part))
            {
                var quantityEl = partEl.SplElement(sc.E.Quantity);
                var numeratorEl = quantityEl?.SplElement(sc.E.Numerator);
                decimal? partQuantity = numeratorEl?.GetAttrDecimal(sc.A.Value);
                string? partUnit = numeratorEl?.GetAttrVal(sc.A.Unit);

                var partProductEl = partEl.SplElement(sc.E.PartProduct);
                if (partProductEl == null)
                {
                    context.Logger.LogWarning("Found <part> element without a <partProduct> child; skipping.");
                    continue;
                }

                // Recursively parse the <partProduct> by calling back to the main parser.
                var partResult = await _mainProductParser.ParseAsync(partProductEl, context, reportProgress);
                aggregateResult.MergeFrom(partResult);

                // Link the newly created part product back to the kit product.
                if (context.CurrentProduct?.ProductID != null && kitProduct.ProductID != null)
                {
                    await saveOrGetProductPartAsync(
                        dbContext, kitProduct.ProductID.Value, context.CurrentProduct.ProductID.Value, partQuantity, partUnit);
                    aggregateResult.ProductElementsCreated++;
                    context.Logger.LogInformation(
                        "ProductPart link created: KitID={KitID}, PartID={PartID}, Quantity={Quantity}{Unit}",
                        kitProduct.ProductID, context.CurrentProduct.ProductID, partQuantity, partUnit);
                }
                else
                {
                    context.Logger.LogError("Failed to create ProductPart link: Kit or Part ProductID was null.");
                }
            }
            return aggregateResult;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [asPartOfAssembly] elements, creating the accessory products and linking them.
        /// </summary>
        /// <param name="parentEl">The parent XElement containing the assembly info.</param>
        /// <param name="primaryProduct">The primary Product entity being described.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult aggregating the results of parsing all assembly parts.</returns>
        /// <seealso cref="PartOfAssembly"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        private async Task<SplParseResult> parseAndSavePartOfAssemblyAsync(
            XElement parentEl,
            Product primaryProduct,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            if (context?.Logger == null || context?.ServiceProvider == null)
            {
                return new SplParseResult();
            }

            var aggregateResult = new SplParseResult();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var assemblyEl in parentEl.SplElements(sc.E.AsPartOfAssembly))
            {
                var accessoryProductEl = assemblyEl.SplElement(sc.E.WholeProduct, sc.E.Part, sc.E.PartProduct);
                if (accessoryProductEl == null)
                {
                    context.Logger.LogWarning("Found <asPartOfAssembly> without a valid <partProduct> structure; skipping.");
                    continue;
                }

                // Recursively parse the accessory product by calling back to the main parser.
                var accessoryResult = await _mainProductParser.ParseAsync(accessoryProductEl, context, reportProgress);
                aggregateResult.MergeFrom(accessoryResult);

                var accessoryProduct = context.CurrentProduct;
                if (accessoryProduct != null && primaryProduct.ProductID.HasValue && accessoryProduct.ProductID.HasValue)
                {
                    await saveOrGetPartOfAssemblyAsync(dbContext, primaryProduct.ProductID.Value, accessoryProduct.ProductID.Value);
                    aggregateResult.ProductElementsCreated++;
                    context.Logger.LogInformation(
                        "PartOfAssembly link created: ProductID1={P1}, ProductID2={P2}",
                        primaryProduct.ProductID.Value, accessoryProduct.ProductID.Value);
                }
                else
                {
                    context.Logger.LogError("Failed to create PartOfAssembly link: A ProductID was null.");
                }
            }
            return aggregateResult;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the [manufacturerOrganization] element to create a ResponsiblePersonLink.
        /// </summary>
        /// <param name="manufacturedProductEl">The [manufacturedProduct] XElement containing the link.</param>
        /// <param name="product">The Product entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of links created (0 or 1).</returns>
        /// <seealso cref="ResponsiblePersonLink"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        private async Task<int> parseAndSaveResponsiblePersonLinkAsync(
            XElement manufacturedProductEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !product.ProductID.HasValue)
            {
                return count;
            }

            var responsibleOrgEl = manufacturedProductEl.GetSplElement(sc.E.ManufacturerOrganization);
            if (responsibleOrgEl == null)
            {
                return count;
            }

            var (responsibleOrg, created) = await getOrCreateOrganizationAsync(responsibleOrgEl, context);
            if (responsibleOrg?.OrganizationID == null)
            {
                context.Logger.LogWarning("Found <manufacturerOrganization> but could not parse or create an organization record for ProductID {ProductID}.", product.ProductID);
                return count;
            }

            if (created)
            {
                context.Logger.LogInformation("Created new Organization (Responsible Person) '{OrgName}' with ID {OrgID}", responsibleOrg.OrganizationName, responsibleOrg.OrganizationID);
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await getOrSaveResponsiblePersonLinkAsync(dbContext, product.ProductID, responsibleOrg.OrganizationID);
            context.Logger.LogInformation("Created ResponsiblePersonLink for ProductID {ProductID} to OrganizationID {OrgID}", product.ProductID, responsibleOrg.OrganizationID);
            count++;

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ResponsiblePersonLink or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="productId">The ID of the cosmetic product.</param>
        /// <param name="responsiblePersonOrgId">The ID of the responsible person organization.</param>
        /// <returns>The existing or newly created ResponsiblePersonLink entity.</returns>
        /// <seealso cref="ResponsiblePersonLink"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<ResponsiblePersonLink> getOrSaveResponsiblePersonLinkAsync(
            ApplicationDbContext dbContext,
            int? productId,
            int? responsiblePersonOrgId)
        {
            #region implementation
            var existing = await dbContext.Set<ResponsiblePersonLink>().FirstOrDefaultAsync(rpl =>
                rpl.ProductID == productId &&
                rpl.ResponsiblePersonOrgID == responsiblePersonOrgId);

            if (existing != null)
            {
                return existing;
            }

            var newLink = new ResponsiblePersonLink
            {
                ProductID = productId,
                ResponsiblePersonOrgID = responsiblePersonOrgId
            };

            dbContext.Set<ResponsiblePersonLink>().Add(newLink);
            await dbContext.SaveChangesAsync();
            return newLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// </summary>
        /// <param name="orgElement">The XElement representing the organization.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and a boolean indicating if it was newly created.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        private static async Task<(Organization? Organization, bool Created)> getOrCreateOrganizationAsync(XElement orgElement, SplParseContext context)
        {
            #region implementation
            var orgName = orgElement.GetSplElementVal(sc.E.Name)?.Trim();
            if (context == null || context.Logger == null || context.ServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(context), "Parsing context, logger, and provider cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(orgName))
            {
                context.Logger.LogWarning("Organization name is missing in file {FileName}. Cannot create organization.", context.FileNameInZip);
                return (null, false);
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgRepo = context.GetRepository<Organization>();
            var orgDbSet = dbContext.Set<Organization>();

            var existingOrg = await orgDbSet.FirstOrDefaultAsync(o => o.OrganizationName == orgName);
            if (existingOrg != null)
            {
                return (existingOrg, false);
            }

            var newOrganization = new Organization
            {
                OrganizationName = orgName,
                IsConfidential = orgElement.GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };

            await orgRepo.CreateAsync(newOrganization);
            return (newOrganization, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PartOfAssembly link or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="product1Id">The ID of the first product in the assembly.</param>
        /// <param name="product2Id">The ID of the second product in the assembly.</param>
        /// <returns>The existing or newly created PartOfAssembly entity.</returns>
        /// <seealso cref="PartOfAssembly"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PartOfAssembly> saveOrGetPartOfAssemblyAsync(
            ApplicationDbContext dbContext,
            int product1Id,
            int product2Id)
        {
            #region implementation
            int primaryId = Math.Min(product1Id, product2Id);
            int accessoryId = Math.Max(product1Id, product2Id);

            var existing = await dbContext.Set<PartOfAssembly>().FirstOrDefaultAsync(pa =>
                pa.PrimaryProductID == primaryId &&
                pa.AccessoryProductID == accessoryId);

            if (existing != null)
                return existing;

            var assemblyLink = new PartOfAssembly
            {
                PrimaryProductID = primaryId,
                AccessoryProductID = accessoryId
            };

            dbContext.Set<PartOfAssembly>().Add(assemblyLink);
            await dbContext.SaveChangesAsync();
            return assemblyLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductPart or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="kitProductId">The ID of the parent (kit) product.</param>
        /// <param name="partProductId">The ID of the child (part) product.</param>
        /// <param name="quantity">The quantity of the part within the kit.</param>
        /// <param name="unit">The unit for the part quantity.</param>
        /// <returns>The existing or newly created ProductPart entity.</returns>
        /// <seealso cref="ProductPart"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<ProductPart> saveOrGetProductPartAsync(
            ApplicationDbContext dbContext,
            int kitProductId,
            int partProductId,
            decimal? quantity,
            string? unit)
        {
            #region implementation
            var existing = await dbContext.Set<ProductPart>().FirstOrDefaultAsync(pp =>
                pp.KitProductID == kitProductId &&
                pp.PartProductID == partProductId);

            if (existing != null)
                return existing;

            var productPart = new ProductPart
            {
                KitProductID = kitProductId,
                PartProductID = partProductId,
                PartQuantityNumerator = quantity,
                PartQuantityNumeratorUnit = unit
            };

            dbContext.Set<ProductPart>().Add(productPart);
            await dbContext.SaveChangesAsync();
            return productPart;
            #endregion
        }
    }
}
