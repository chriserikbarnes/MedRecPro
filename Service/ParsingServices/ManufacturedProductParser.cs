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

                // --- PARSE GENERIC MEDICINE ---
                reportProgress?.Invoke($"Starting Generic Medicine XML Elements {context.FileNameInZip}");
                var genericMedicinesCreated = await parseAndSaveGenericMedicinesAsync(mmEl, product, context);
                result.ProductElementsCreated += genericMedicinesCreated;  

                // --- PARSE EQUIVALENT ENTITIES ---
                reportProgress?.Invoke($"Starting Equivalent XML Elements {context.FileNameInZip}");
                var equivCount = await parseAndSaveEquivalentEntitiesAsync(mmEl, product, context);
                result.ProductElementsCreated += equivCount;

                // --- PARSE IDENTIFIER ENTITIES ---
                reportProgress?.Invoke($"Starting Identifier XML Elements {context.FileNameInZip}");
                var idCount = await parseAndSaveProductIdentifiersAsync(mmEl, product, context);
                result.ProductElementsCreated += idCount;

                // --- PARSE SPECIALIZED KINDS ---
                reportProgress?.Invoke($"Starting Specialized Kind XML Elements {context.FileNameInZip}");
                var kindCount = await parseAndSaveSpecializedKindsAsync(mmEl, product, context, result.DocumentCode);
                result.ProductElementsCreated += kindCount;
                reportProgress?.Invoke($"Completed Specialized Kind XML Elements {context.FileNameInZip}");

                // --- PARSE MARKETING CATEGORY ---
                reportProgress?.Invoke($"Starting Marketing Category XML Elements {context.FileNameInZip}");
                var marketingCatCreated = await parseAndSaveMarketingCategoriesAsync(mmEl, product, context);
                result.ProductElementsCreated += marketingCatCreated;

                // --- PARSE CHARACTERISTIC ---
                reportProgress?.Invoke($"Starting Characteristic XML Elements {context.FileNameInZip}");
                var characteristicCt = await parseAndSaveCharacteristicsAsync(mmEl, product, context);
                result.ProductElementsCreated += characteristicCt;

                // --- PARSE ADDITIONAL IDENTIFIER ---
                reportProgress?.Invoke($"Starting Additional Identifier XML Elements {context.FileNameInZip}");
                var identifiersCt = await parseAndSaveAdditionalIdentifiersAsync(mmEl, product, context);
                result.ProductElementsCreated += identifiersCt;

                // --- PARSE MARKETING STATUS ---
                reportProgress?.Invoke($"Starting Marketing Status XML Elements {context.FileNameInZip}");
                var marketingCt = await parseAndSaveMarketingStatusesAsync(mmEl, product, context);
                result.ProductElementsCreated += marketingCt;

                // --- PARSE POLICY ---
                reportProgress?.Invoke($"Starting Policy XML Elements {context.FileNameInZip}");
                var policyCt = await parseAndSavePoliciesAsync(mmEl, product, context);
                result.ProductElementsCreated += policyCt;

                // --- PARSE ROUTE OF ADMIN ---
                reportProgress?.Invoke($"Starting Product Route Of Administration XML Elements {context.FileNameInZip}");
                var routeCt = await parseAndSaveProductRoutesOfAdministrationAsync(mmEl, product, context);
                result.ProductElementsCreated += routeCt;

                // --- PARSE WEB LINK ---
                reportProgress?.Invoke($"Starting Web Link XML Elements {context.FileNameInZip}");
                var webCt = await parseAndSaveProductWebLinksAsync(mmEl, product, context);
                result.ProductElementsCreated += webCt;

                // --- PARSE BUSINESS OPERATION ---
                reportProgress?.Invoke($"Starting Business Operation XML Elements {context.FileNameInZip}");
                var opsCt = await parseAndSaveBusinessOperationAndLinksAsync(mmEl, product, context);
                result.ProductElementsCreated += opsCt;

                // --- PARSE KIT PARTS (if any) ---
                reportProgress?.Invoke($"Starting Kit/Part XML Elements {context.FileNameInZip}");
                var kitParsingResult = await parseAndSaveProductPartsAsync(mmEl, product, context, reportProgress);
                result.MergeFrom(kitParsingResult);

                // --- PARSE PART OF ASSEMBLY (if any) ---
                reportProgress?.Invoke($"Starting Part of Assembly XML Elements {context.FileNameInZip}");
                var assemblyParsingResult = await parseAndSavePartOfAssemblyAsync(mmEl, product, context, reportProgress);
                result.MergeFrom(assemblyParsingResult);

                // --- PARSE PACKAGING LEVELS ---
                reportProgress?.Invoke($"Starting Packaging Level XML Elements {context.FileNameInZip}");
                var asContentEls = mmEl.SplFindElements(sc.E.AsContent);
                // If asContent elements exist, parse and save packaging levels
                if (asContentEls != null && asContentEls.Any())
                {
                    foreach (var asContentEl in asContentEls)
                    {
                        result.ProductElementsCreated += await parseAndSavePackagingLevelsAsync(asContentEl, product, context);
                    }
                }

                result.ProductsCreated++;
                context.Logger.LogInformation("Created Product '{ProductName}' with ID {ProductID}", product.ProductName, product.ProductID);
                reportProgress?.Invoke($"Completed Packaging Level XML Elements {context.FileNameInZip}");

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
        /// Gets an existing DocumentRelationship or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="docId">The document ID to associate with the relationship.</param>
        /// <param name="parentOrgId">The parent organization ID in the relationship.</param>
        /// <param name="childOrgId">The child organization ID in the relationship.</param>
        /// <param name="relationshipType">The type of relationship between organizations.</param>
        /// <param name="relationshipLevel">The hierarchical level of the relationship.</param>
        /// <returns>The existing or newly created DocumentRelationship entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate relationship records.
        /// Uses a composite key match on all relationship parameters for uniqueness.
        /// </remarks>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<DocumentRelationship> saveOrGetDocumentRelationshipAsync(
            ApplicationDbContext dbContext,
            int? docId,
            int? parentOrgId,
            int? childOrgId,
            string? relationshipType,
            int? relationshipLevel)
        {
            #region implementation
            // Search for existing relationship with matching parameters
            var existing = await dbContext.Set<DocumentRelationship>().FirstOrDefaultAsync(dr =>
                dr.DocumentID == docId &&
                dr.ParentOrganizationID == parentOrgId &&
                dr.ChildOrganizationID == childOrgId &&
                dr.RelationshipType == relationshipType &&
                dr.RelationshipLevel == relationshipLevel);

            // Return existing relationship if found
            if (existing != null)
                return existing;

            // Create new relationship entity with provided parameters
            var rel = new DocumentRelationship
            {
                DocumentID = docId,
                ParentOrganizationID = parentOrgId,
                ChildOrganizationID = childOrgId,
                RelationshipType = relationshipType,
                RelationshipLevel = relationshipLevel
            };

            // Save the new relationship to database
            dbContext.Set<DocumentRelationship>().Add(rel);
            await dbContext.SaveChangesAsync();
            return rel;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing BusinessOperation or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="documentRelationshipId">The document relationship ID to associate with the operation.</param>
        /// <param name="operationCode">The operation code identifying the business operation type.</param>
        /// <param name="operationCodeSystem">The code system for the operation code.</param>
        /// <param name="operationDisplayName">The display name for the operation.</param>
        /// <returns>The existing or newly created BusinessOperation entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate operation records.
        /// Uses a composite key match on document relationship ID and operation details.
        /// </remarks>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperation> saveOrGetBusinessOperationAsync(
            ApplicationDbContext dbContext,
            int? documentRelationshipId,
            string? operationCode,
            string? operationCodeSystem,
            string? operationDisplayName)
        {
            #region implementation
            // Search for existing operation with matching parameters
            var existing = await dbContext.Set<BusinessOperation>().FirstOrDefaultAsync(op =>
                op.DocumentRelationshipID == documentRelationshipId &&
                op.OperationCode == operationCode &&
                op.OperationCodeSystem == operationCodeSystem &&
                op.OperationDisplayName == operationDisplayName);

            // Return existing operation if found
            if (existing != null)
                return existing;

            // Create new business operation entity with provided parameters
            var newOp = new BusinessOperation
            {
                DocumentRelationshipID = documentRelationshipId,
                OperationCode = operationCode,
                OperationCodeSystem = operationCodeSystem,
                OperationDisplayName = operationDisplayName
            };

            // Save the new operation to database
            dbContext.Set<BusinessOperation>().Add(newOp);
            await dbContext.SaveChangesAsync();
            return newOp;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all BusinessOperation and BusinessOperationProductLink entities,
        /// connecting each operation to the appropriate DocumentRelationship for the org performing it,
        /// and linking to all referenced Products.
        /// </summary>
        /// <param name="parentEl">The parent XML element to search for performance elements.</param>
        /// <param name="product"> The product to link business operations to.</param>
        /// <param name="context">The parsing context containing document and service provider access.</param>
        /// <returns>The total count of business operation product links created.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of business operations by:
        /// 1. Validating the parsing context and retrieving the labeler organization
        /// 2. Getting all document relationships for the current document
        /// 3. Processing performance elements for each document relationship
        /// 4. Creating business operations and their product links
        /// </remarks>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveBusinessOperationAndLinksAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            // Validate context before proceeding
            if (!isDocumentContextValid(context))
                return 0;

            var dbContext = context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>();
            int? docId = context?.Document?.DocumentID;

            // Ensure we have a valid database context and document ID
            if (dbContext == null || context == null || docId == null || context.Logger == null)
            {
                return 0;
            }

            // Get the labeler organization ID for this document
            var labelerOrgId = await getLabelerOrganizationIdAsync(dbContext, docId, context.Logger);
            if (labelerOrgId == null) return 0;

            // Get all document relationships for processing
            var docRels = await getDocumentRelationshipsAsync(dbContext, docId, context.Logger);
            if (!docRels.Any()) return 0;

            int createdCount = 0;

            // Process business operations for each document relationship
            foreach (var docRel in docRels)
            {
                // Ensure the document relationship exists or create it
                var thisDocRel = await saveOrGetDocumentRelationshipAsync(
                    dbContext, docId, labelerOrgId, docRel.ChildOrganizationID, docRel.RelationshipType, docRel.RelationshipLevel);

                // Parse performance elements and create business operation links
                createdCount += await parsePerformanceElementsAsync(
                    parentEl, dbContext, product, thisDocRel.DocumentRelationshipID, context.Logger);
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the parsing context contains all required dependencies.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <returns>True if the context is valid, false otherwise.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private bool isDocumentContextValid(SplParseContext? context)
        {
            #region implementation
            return context != null &&
                   context.Logger != null &&
                   context.Document != null &&
                   context.ServiceProvider != null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the labeler organization ID for the specified document.
        /// </summary>
        /// <param name="dbContext">The database context for querying.</param>
        /// <param name="docId">The document ID to search for.</param>
        /// <param name="logger">Logger for warning messages.</param>
        /// <returns>The labeler organization ID, or null if not found.</returns>
        /// <seealso cref="DocumentAuthor"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> getLabelerOrganizationIdAsync(
            ApplicationDbContext dbContext, int? docId, ILogger logger)
        {
            #region implementation
            // Find the labeler document author for this document
            var labeler = await dbContext.Set<DocumentAuthor>()
                .FirstOrDefaultAsync(a => a.DocumentID == docId && a.AuthorType == "Labeler");
            if (labeler == null)
            {
                logger.LogWarning("Labeler DocumentAuthor not found for this document.");
                return null;
            }

            // Validate that the labeler has an organization ID
            if (labeler.OrganizationID == null)
            {
                logger.LogWarning("Labeler OrganizationID not found for this document.");
                return null;
            }

            return labeler.OrganizationID;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves all document relationships for the specified document.
        /// </summary>
        /// <param name="dbContext">The database context for querying.</param>
        /// <param name="docId">The document ID to search for.</param>
        /// <param name="logger">Logger for warning messages.</param>
        /// <returns>A list of document relationships, or empty list if none found.</returns>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<List<DocumentRelationship>> getDocumentRelationshipsAsync(
            ApplicationDbContext dbContext, int? docId, ILogger logger)
        {
            #region implementation
            // Query for all document relationships for this document
            var docRels = await dbContext.Set<DocumentRelationship>()
                .Where(r => r.DocumentID == docId)
                .ToListAsync();

            // Log warning if no relationships found
            if (!docRels.Any())
                logger.LogWarning("No DocumentRelationships found for this document.");

            return docRels;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses performance elements and creates business operations with product links.
        /// </summary>
        /// <param name="parentEl">The parent XML element to search for performance elements.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="product">The product to link business operations to.</param>
        /// <param name="documentRelationshipId">The document relationship ID to associate operations with.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The number of product links created.</returns>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="Label"/>
        private async Task<int> parsePerformanceElementsAsync(
            XElement parentEl,
            ApplicationDbContext dbContext,
            Product product,
            int? documentRelationshipId,
            ILogger logger)
        {
            #region implementation
            int createdLinks = 0;

            // Process each performance element
            foreach (var perfEl in parentEl.SplElements(sc.E.Performance))
            {
                foreach (var actDefEl in perfEl.SplElements(sc.E.ActDefinition))
                {
                    // Parse or create the operation for this document relationship
                    var bizOp = await parseBusinessOperationAsync(dbContext, documentRelationshipId, actDefEl);

                    // Parse and save any product links for this operation
                    createdLinks += await parseAndSaveProductLinksAsync(dbContext, bizOp, actDefEl, product, logger);
                }
            }

            return createdLinks;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a business operation from an act definition XML element.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="docRelId">The document relationship ID to associate with the operation.</param>
        /// <param name="actDefEl">The act definition XML element to parse.</param>
        /// <returns>The existing or newly created BusinessOperation entity.</returns>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperation> parseBusinessOperationAsync(
            ApplicationDbContext dbContext, int? docRelId, XElement actDefEl)
        {
            #region implementation
            // Extract operation code details from the XML element
            var opCodeEl = actDefEl.GetSplElement(sc.E.Code);
            string? opCode = opCodeEl?.GetAttrVal(sc.A.CodeValue);
            string? opCodeSystem = opCodeEl?.GetAttrVal(sc.A.CodeSystem);
            string? opDisplayName = opCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Always use the helper to get or create the operation
            return await saveOrGetBusinessOperationAsync(
                dbContext, docRelId, opCode, opCodeSystem, opDisplayName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves product links for a business operation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="bizOp">The business operation to create links for.</param>
        /// <param name="actDefEl">The act definition XML element containing product references.</param>
        /// <param name="product">The product to link to the business operation.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The number of product links created.</returns>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveProductLinksAsync(
            ApplicationDbContext dbContext,
            BusinessOperation bizOp,
            XElement actDefEl,
            Product product,
            ILogger logger)
        {
            #region implementation
            int linksCreated = 0;

            // Validate business operation before proceeding
            if (bizOp == null || bizOp.BusinessOperationID == null)
            {
                logger.LogWarning("Business operation is null or has no ID, skipping product link creation.");
                return linksCreated;
            }

            // Process each product reference in the act definition
            foreach (var productEl in actDefEl.SplElements(sc.E.Product))
            {
                // Extract product item code from the XML structure
                string? itemCode = parseProductItemCode(productEl);
                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    logger.LogWarning("Missing product item code for business operation link.");
                    continue;
                }

                if (product == null)
                {
                    logger.LogWarning($"No Product found for item code {itemCode} (op={bizOp.OperationCode}).");
                    continue;
                }

                // Create link if it doesn't already exist
                if (bizOp != null && bizOp.BusinessOperationID != null && product.ProductID != null)
                    if (!await businessOperationProductLinkExistsAsync(dbContext, bizOp.BusinessOperationID, product.ProductID))
                    {
                        await saveBusinessOperationProductLinkAsync(dbContext, bizOp.BusinessOperationID, product.ProductID);
                        logger.LogInformation($"BusinessOperationProductLink created: OperationID={bizOp.BusinessOperationID}, ProductID={product.ProductID} (item code={itemCode})");
                        linksCreated++;
                    }
            }

            return linksCreated;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the product item code from a product XML element.
        /// </summary>
        /// <param name="productEl">The product XML element to parse.</param>
        /// <returns>The product item code, or null if not found.</returns>
        /// <seealso cref="Label"/>
        private string? parseProductItemCode(XElement productEl)
        {
            #region implementation
            // Navigate the XML hierarchy to find the product code
            var manuProdEl = productEl.GetSplElement(sc.E.ManufacturedProduct);
            var matKindEl = manuProdEl?.GetSplElement(sc.E.ManufacturedMaterialKind);
            var codeEl = matKindEl?.GetSplElement(sc.E.Code);
            return codeEl?.GetAttrVal(sc.A.CodeValue);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a business operation product link already exists.
        /// </summary>
        /// <param name="dbContext">The database context for querying.</param>
        /// <param name="businessOperationId">The business operation ID.</param>
        /// <param name="productId">The product ID.</param>
        /// <returns>True if the link exists, false otherwise.</returns>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="Label"/>
        private async Task<bool> businessOperationProductLinkExistsAsync(
            ApplicationDbContext dbContext, int? businessOperationId, int? productId)
        {
            #region implementation
            return await dbContext.Set<BusinessOperationProductLink>().AnyAsync(link =>
                link.BusinessOperationID == businessOperationId && link.ProductID == productId);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves a new business operation product link to the database.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="businessOperationId">The business operation ID.</param>
        /// <param name="productId">The product ID.</param>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="Label"/>
        private async Task saveBusinessOperationProductLinkAsync(
            ApplicationDbContext dbContext, int? businessOperationId, int? productId)
        {
            #region implementation
            // Create and save the new business operation product link
            dbContext.Set<BusinessOperationProductLink>().Add(new BusinessOperationProductLink
            {
                BusinessOperationID = businessOperationId,
                ProductID = productId
            });
            await dbContext.SaveChangesAsync();
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
                        continue; // skip if mediaType is present

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


        /**************************************************************/
        /// <summary>
        /// Parses and saves all ProductRouteOfAdministration entities from [consumedIn][substanceAdministration][routeCode] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [part]) to scan for routes of administration.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of ProductRouteOfAdministration records created.</returns>
        /// <remarks>
        /// Handles route code, code system, display name, and nullFlavor according to SPL IG Section 3.2.20.
        /// Enforces SPL specification: accepts either correct code system (2.16.840.1.113883.3.26.1.1) or nullFlavor.
        /// Validates route codes against FDA SPL standards for pharmaceutical products.
        /// </remarks>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveProductRoutesOfAdministrationAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<ProductRouteOfAdministration>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all consumedIn/substanceAdministration structures
            foreach (var consumedInEl in parentEl.SplElements(sc.E.ConsumedIn))
            {
                foreach (var substAdminEl in consumedInEl.SplElements(sc.E.SubstanceAdministration))
                {
                    var routeCodeEl = substAdminEl.GetSplElement(sc.E.RouteCode);

                    if (routeCodeEl == null)
                        continue;

                    // Parse route attributes from the XML element
                    string? routeCode = routeCodeEl.GetAttrVal(sc.A.CodeValue);
                    string? routeCodeSystem = routeCodeEl.GetAttrVal(sc.A.CodeSystem);
                    string? displayName = routeCodeEl.GetAttrVal(sc.A.DisplayName);
                    string? nullFlavor = routeCodeEl.GetAttrVal(sc.A.NullFlavor);

                    // Enforce SPL spec: Either code system is correct or nullFlavor is set
                    if (string.IsNullOrWhiteSpace(nullFlavor))
                    {
                        // Only accept route codes with the proper FDA SPL code system
                        if (routeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                            continue;
                    }

                    // Build and save the ProductRouteOfAdministration entity
                    var route = new ProductRouteOfAdministration
                    {
                        ProductID = product.ProductID,
                        RouteCode = routeCode,
                        RouteCodeSystem = routeCodeSystem,
                        RouteDisplayName = displayName,
                        RouteNullFlavor = nullFlavor
                    };

                    await repo.CreateAsync(route);
                    count++;
                    context.Logger.LogInformation(
                        $"ProductRouteOfAdministration created: ProductID={product.ProductID}, RouteCode={routeCode}, DisplayName={displayName}, NullFlavor={nullFlavor}");
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all DEA Policy entities under [subjectOf][policy] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct]) to scan for DEA schedule policies.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of Policy records created.</returns>
        /// <remarks>
        /// Handles DEA schedule code, system, display name, and class code according to SPL IG Section 3.2.11.
        /// Only processes policies with classCode="DEADrugSchedule" and correct FDA code system.
        /// Requires both policy code and display name to be present for data integrity.
        /// </remarks>
        /// <seealso cref="Policy"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSavePoliciesAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<Policy>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all subjectOf/policy structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var policyEl in subjOf.SplElements(sc.E.Policy))
                {
                    // <policy> must have classCode="DEADrugSchedule"
                    string? classCode = policyEl.GetAttrVal(sc.A.ClassCode);
                    if (!string.Equals(classCode, "DEADrugSchedule", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // <code> is required for DEA schedule identification
                    var codeEl = policyEl.GetSplElement(sc.E.Code);
                    string? policyCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                    string? policyCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                    string? displayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                    // Only allow correct FDA SPL code system
                    if (policyCodeSystem != "2.16.840.1.113883.3.26.1.1")
                        continue;

                    // Display name must be present and match code (for safety, allow override if needed)
                    if (string.IsNullOrWhiteSpace(policyCode) || string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Build and save the Policy entity
                    var policy = new Policy
                    {
                        ProductID = product.ProductID,
                        PolicyClassCode = classCode,
                        PolicyCode = policyCode,
                        PolicyCodeSystem = policyCodeSystem,
                        PolicyDisplayName = displayName
                    };

                    await repo.CreateAsync(policy);
                    count++;
                    context.Logger.LogInformation(
                        $"Policy (DEA Schedule) created: ProductID={product.ProductID}, PolicyCode={policyCode}, DisplayName={displayName}");
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all MarketingStatus entities under [subjectOf][marketingAct] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [containerPackagedProduct]) to scan for marketing status.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of MarketingStatus records created.</returns>
        /// <remarks>
        /// Handles activity codes, status codes, and effective time periods according to SPL IG Section 3.1.8.
        /// Validates marketing activity codes against FDA SPL code system (2.16.840.1.113883.3.26.1.1).
        /// Accepts only permitted status codes: active, completed, new, cancelled.
        /// Parses effective time intervals with low and high date boundaries.
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveMarketingStatusesAsync(
        XElement parentEl,
        Product product,
        SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<MarketingStatus>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all subjectOf/marketingAct structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var mktAct in subjOf.SplElements(sc.E.MarketingAct))
                {
                    // <code> (activity of marketing/sample)
                    var codeEl = mktAct.GetSplElement(sc.E.Code);
                    string? actCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                    string? actCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

                    // Only accept act codes for marketing or drug sample (per SPL Table)
                    if (actCodeSystem != "2.16.840.1.113883.3.26.1.1")
                        continue;

                    // <statusCode> (active, completed, new, cancelled)
                    var statusCodeEl = mktAct.GetSplElement(sc.E.StatusCode);
                    string? statusCode = statusCodeEl?.GetAttrVal(sc.A.CodeValue);

                    // Accept only permitted status codes according to SPL standards
                    if (statusCode == null ||
                        !(statusCode.Equals("active", StringComparison.OrdinalIgnoreCase) ||
                          statusCode.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
                          statusCode.Equals("new", StringComparison.OrdinalIgnoreCase) ||
                          statusCode.Equals("cancelled", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // <effectiveTime> block - parse start and end dates
                    var effTimeEl = mktAct.GetSplElement(sc.E.EffectiveTime);
                    DateTime? effectiveStartDate = null;
                    DateTime? effectiveEndDate = null;

                    if (effTimeEl != null)
                    {
                        // Parse low (start) date
                        var lowEl = effTimeEl.GetSplElement(sc.E.Low);
                        if (lowEl != null)
                        {
                            var lowValue = lowEl.GetAttrVal(sc.A.Value);
                            if (!string.IsNullOrEmpty(lowValue))
                            {
                                effectiveStartDate = Util.ParseNullableDateTime(lowValue);
                            }
                        }

                        // Parse high (end) date
                        var highEl = effTimeEl.GetSplElement(sc.E.High);
                        if (highEl != null)
                        {
                            var highValue = highEl.GetAttrVal(sc.A.Value);
                            if (!string.IsNullOrEmpty(highValue))
                            {
                                effectiveEndDate = Util.ParseNullableDateTime(highValue);
                            }
                        }
                    }

                    // Build and save the MarketingStatus entity
                    var marketingStatus = new MarketingStatus
                    {
                        ProductID = product.ProductID,
                        MarketingActCode = actCode,
                        MarketingActCodeSystem = actCodeSystem,
                        StatusCode = statusCode,
                        EffectiveStartDate = effectiveStartDate,
                        EffectiveEndDate = effectiveEndDate
                    };

                    await repo.CreateAsync(marketingStatus);
                    count++;
                    context.Logger.LogInformation(
                        $"MarketingStatus created: ProductID={product.ProductID}, ActCode={actCode}, Status={statusCode}, Start={effectiveStartDate:yyyy-MM-dd}, End={effectiveEndDate:yyyy-MM-dd}");
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all AdditionalIdentifier entities under [asIdentifiedEntity classCode="IDENT"] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct], [partProduct], or [product]) to scan for additional identifiers.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of AdditionalIdentifier records created.</returns>
        /// <remarks>
        /// Handles identifier types Model Number (C99286), Catalog Number (C99285), Reference Number (C99287), and related.
        /// Only processes entities with classCode="IDENT" and validates against NCI Thesaurus code system.
        /// Requires both identifier value and root OID to be present for data integrity.
        /// </remarks>
        /// <seealso cref="AdditionalIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveAdditionalIdentifiersAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<AdditionalIdentifier>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Find all <asIdentifiedEntity> nodes with classCode="IDENT"
            foreach (var idEnt in parentEl.SplElements(sc.E.AsIdentifiedEntity))
            {
                // Only process if classCode="IDENT"
                string? classCode = idEnt.GetAttrVal(sc.A.ClassCode);
                if (!string.Equals(classCode, "IDENT", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse the <id> child for identifier value and root
                var idEl = idEnt.GetSplElement(sc.E.Id);
                string? identifierValue = idEl?.GetAttrVal(sc.A.Extension);
                string? identifierRootOID = idEl?.GetAttrVal(sc.A.Root);

                // Parse the <code> child (type of identifier)
                var codeEl = idEnt.GetSplElement(sc.E.Code);
                string? typeCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? typeCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? typeDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                // Validation: Only accept NCI Thesaurus code system for type (per Table 3)
                if (string.IsNullOrWhiteSpace(typeCodeSystem) ||
                    typeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                    continue;

                // At least one id (extension/root) must be present, and a recognized code type
                if (string.IsNullOrWhiteSpace(identifierValue) || string.IsNullOrWhiteSpace(identifierRootOID))
                    continue;

                // Recognized identifier codes (per Table 3)
                bool recognized = typeCode == "C99286" // Model Number
                               || typeCode == "C99285" // Catalog Number
                               || typeCode == "C99287"; // Reference Number

                if (!recognized)
                    continue;

                // Build and save the AdditionalIdentifier entity
                var additionalIdentifier = new AdditionalIdentifier
                {
                    ProductID = product.ProductID,
                    IdentifierTypeCode = typeCode,
                    IdentifierTypeCodeSystem = typeCodeSystem,
                    IdentifierTypeDisplayName = typeDisplayName,
                    IdentifierValue = identifierValue,
                    IdentifierRootOID = identifierRootOID
                };

                await repo.CreateAsync(additionalIdentifier);
                count++;
                context.Logger.LogInformation($"AdditionalIdentifier created: ProductID={product.ProductID}, TypeCode={typeCode}, Value={identifierValue}, Root={identifierRootOID}");
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all Characteristic entities under [subjectOf][characteristic] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [partProduct]) to scan for characteristics.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of Characteristic records created.</returns>
        /// <remarks>
        /// Handles characteristic value types PQ, INT, IVL_PQ, CV, ST, ED, and BL according to SPL IG.
        /// Supports complex value types including intervals, coded values, and multimedia references.
        /// Each characteristic includes both the code identifying the characteristic type and the
        /// appropriately typed value based on the xsi:type attribute.
        /// </remarks>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveCharacteristicsAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<Characteristic>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all subjectOf/characteristic structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var charEl in subjOf.SplElements(sc.E.Characteristic))
                {
                    // --- Parse Characteristic code & codeSystem ---
                    var codeEl = charEl.GetSplElement(sc.E.Code);
                    string? charCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                    string? charCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

                    // --- Parse <value> node and its type ---
                    var valueEl = charEl.GetSplElement(sc.E.Value);
                    string? valueType = valueEl?.GetXsiType();

                    // Initialize all possible value fields to null
                    decimal? valuePQ_Value = null;
                    string? valuePQ_Unit = null;
                    int? valueINT = null;
                    string? valueNullFlavor = null;
                    string? valueCV_Code = null;
                    string? valueCV_CodeSystem = null;
                    string? valueCV_DisplayName = null;
                    string? valueST = null;
                    bool? valueBL = null;
                    decimal? valueIVLPQ_LowValue = null;
                    string? valueIVLPQ_LowUnit = null;
                    decimal? valueIVLPQ_HighValue = null;
                    string? valueIVLPQ_HighUnit = null;
                    string? valueED_MediaType = null;
                    string? valueED_FileName = null;

                    // --- Parse based on xsi:type to populate appropriate value fields ---
                    if (!string.IsNullOrWhiteSpace(valueType))
                    {
                        switch (valueType.ToUpperInvariant())
                        {
                            case "PQ":
                            case "REAL": // treat as decimal/quantity
                                if (valueEl != null)
                                {
                                    // Parse physical quantity with value and unit
                                    var valueAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valuePQ_Value = valueAttr != null ? Util.ParseNullableDecimal(valueAttr) : null;
                                    valuePQ_Unit = valueEl.GetAttrVal(sc.A.Unit);
                                }
                                break;

                            case "INT":
                                if (valueEl != null)
                                {
                                    // Parse integer value with optional null flavor
                                    valueNullFlavor = valueEl.GetAttrVal(sc.A.NullFlavor);
                                    var intAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valueINT = intAttr != null ? Util.ParseNullableInt(intAttr) : null;
                                }
                                break;

                            case "CV":
                            case "CE": // Handle CV and CE the same way
                                       // Parse coded value with code, system, and display name
                                valueCV_Code = valueEl?.GetAttrVal(sc.A.CodeValue);
                                valueCV_CodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem);
                                valueCV_DisplayName = valueEl?.GetAttrVal(sc.A.DisplayName);
                                break;

                            case "ST":
                                // Parse string value from element text content
                                valueST = valueEl?.Value;
                                break;

                            case "IVL_PQ":
                                // Parse interval of physical quantities (low and high values)
                                var lowEl = valueEl?.GetSplElement(sc.E.Low);
                                if (lowEl != null)
                                {
                                    var lowValueAttr = lowEl.GetAttrVal(sc.A.Value);
                                    valueIVLPQ_LowValue = lowValueAttr != null ? Util.ParseNullableDecimal(lowValueAttr) : null;
                                    valueIVLPQ_LowUnit = lowEl.GetAttrVal(sc.A.Unit);
                                }

                                var highEl = valueEl?.GetSplElement(sc.E.High);
                                if (highEl != null)
                                {
                                    var highValueAttr = highEl.GetAttrVal(sc.A.Value);
                                    valueIVLPQ_HighValue = highValueAttr != null ? Util.ParseNullableDecimal(highValueAttr) : null;
                                    valueIVLPQ_HighUnit = highEl.GetAttrVal(sc.A.Unit);
                                }
                                break;

                            case "ED":
                                // Parse encapsulated data (multimedia references)
                                valueED_MediaType = valueEl?.GetAttrVal(sc.A.MediaType);
                                valueED_FileName = valueEl?.GetAttrVal(sc.A.DisplayName);
                                break;

                            case "BL":
                                if (valueEl != null)
                                {
                                    // Parse boolean value from string representation
                                    var boolAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valueBL = boolAttr != null ? Util.ParseNullableBoolWithStringValue(boolAttr) : null;
                                }
                                break;
                        }
                    }

                    // --- Build and save the Characteristic entity ---
                    var characteristic = new Characteristic
                    {
                        ProductID = product.ProductID,
                        // PackagingLevelID is not handled here, add logic if needed
                        CharacteristicCode = charCode,
                        CharacteristicCodeSystem = charCodeSystem,
                        ValueType = valueType,
                        ValuePQ_Value = valuePQ_Value,
                        ValuePQ_Unit = valuePQ_Unit,
                        ValueINT = valueINT,
                        ValueCV_Code = valueCV_Code,
                        ValueCV_CodeSystem = valueCV_CodeSystem,
                        ValueCV_DisplayName = valueCV_DisplayName,
                        ValueST = valueST,
                        ValueBL = valueBL,
                        ValueIVLPQ_LowValue = valueIVLPQ_LowValue,
                        ValueIVLPQ_LowUnit = valueIVLPQ_LowUnit,
                        ValueIVLPQ_HighValue = valueIVLPQ_HighValue,
                        ValueIVLPQ_HighUnit = valueIVLPQ_HighUnit,
                        ValueED_MediaType = valueED_MediaType,
                        ValueED_FileName = valueED_FileName,
                        ValueNullFlavor = valueNullFlavor
                    };

                    // Save the characteristic entity to the database
                    await repo.CreateAsync(characteristic);
                    count++;
                    context.Logger.LogInformation($"Characteristic created: ProductID={product.ProductID}, Code={charCode}, ValueType={valueType}");
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all MarketingCategory entities under [subjectOf][approval] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (either [manufacturedProduct] or [partProduct]) to scan for marketing categories.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of MarketingCategory records created.</returns>
        /// <remarks>
        /// Extracts marketing category information from approval nodes including category codes, 
        /// application/monograph IDs, approval dates, and territory codes. Handles the complex
        /// XML structure of subjectOf/approval elements according to SPL standards.
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveMarketingCategoriesAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<MarketingCategory>();

            if (context == null || repo == null || context.Logger == null)
            {
                return count;
            }

            // Find all <subjectOf><approval> nodes for processing
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                var approvalEl = subjOf.SplElement(sc.E.Approval);
                if (approvalEl == null)
                    continue;

                // 1. <id> - Application/monograph number and root
                var idEl = approvalEl.SplElement(sc.E.Id);
                string? idExtension = idEl?.GetAttrVal(sc.A.Extension);
                string? idRoot = idEl?.GetAttrVal(sc.A.Root);

                // 2. <code> - Marketing category code, codeSystem, displayName
                var codeEl = approvalEl.SplElement(sc.E.Code);
                string? categoryCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? categoryCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? categoryDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                // 3. <effectiveTime><low value="YYYYMMDD"> - Parse with Util.ParseNullableDateTime
                DateTime? approvalDate = null;
                var effTimeEl = approvalEl.SplElement(sc.E.EffectiveTime);
                var lowEl = effTimeEl?.SplElement(sc.E.Low);
                string? dateStr = lowEl?.GetAttrVal(sc.A.Value);

                if (!string.IsNullOrWhiteSpace(dateStr))
                {
                    approvalDate = MedRecPro.Helpers.Util.ParseNullableDateTime(dateStr);
                }

                // 4. <author><territorialAuthority><territory><code code="USA">
                string? territoryCode = null;
                var terrCodeEl = approvalEl
                    .SplElement(sc.E.Author)?
                    .SplElement(sc.E.TerritorialAuthority)?
                    .SplElement(sc.E.Territory)?
                    .SplElement(sc.E.Code);

                if (terrCodeEl != null)
                    territoryCode = terrCodeEl.GetAttrVal(sc.A.CodeValue);

                // 5. Build and save the marketing category entity
                var marketingCategory = new MarketingCategory
                {
                    ProductID = product.ProductID,
                    CategoryCode = categoryCode,
                    CategoryCodeSystem = categoryCodeSystem,
                    CategoryDisplayName = categoryDisplayName,
                    ApplicationOrMonographIDValue = idExtension,
                    ApplicationOrMonographIDOID = idRoot,
                    ApprovalDate = approvalDate,
                    TerritoryCode = territoryCode,
                    // ProductConceptID = null (if needed, add logic)
                };

                // Persist the marketing category to the database
                await repo.CreateAsync(marketingCategory);
                count++;
                context.Logger.LogInformation($"MarketingCategory created: ProductID={product.ProductID}, Code={categoryCode}, ApplicationID={idExtension}");
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all PackagingLevel entities under a given 'asContent' 
        /// node (including nested asContent/containerPackagedProduct nodes).
        /// </summary>
        /// <param name="asContentEl">Root [asContent] XElement.</param>
        /// <param name="product">The Product entity associated (if outermost).</param>
        /// <param name="context">The parsing context (repo, logger, docTypeCode, etc).</param>
        /// <param name="parentPackagingLevelId">The ID of the parent (outer) packaging level for creating hierarchy links. Null for the top level.</param>
        /// <param name="sequenceNumber">The sequence of this package within its parent. Null for the top level.</param>
        /// <param name="parentProductInstanceId">For lot/container context (16.2.8), null otherwise.</param>
        /// <returns>The count of PackagingLevel records created (recursively).</returns>
        /// <remarks>
        /// Handles both outermost and nested package levels. Recursively processes nested packaging
        /// structures to create a full packaging tree using PackagingHierarchy links.
        /// Extracts quantity, package codes, and form codes from the XML.
        /// </remarks>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="PackagingHierarchy"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="saveOrGetPackagingHierarchyAsync"/>
        private async Task<int> parseAndSavePackagingLevelsAsync(
            XElement asContentEl,
            Product? product,
            SplParseContext context,
            int? parentPackagingLevelId = null,
            int? sequenceNumber = null,
            int? parentProductInstanceId = null)
        {
            #region implementation
            int count = 0;

            if(context?.ServiceProvider == null || context.Logger == null)
            {
                return count; // Exit early if context is not properly initialized
            }

            // Get repository and database context for PackagingLevel operations
            var repo = context.GetRepository<PackagingLevel>();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Extract <quantity>/<numerator> from current <asContent>
            var quantityEl = asContentEl.SplElement(sc.E.Quantity);
            decimal? quantityValue = null;
            decimal? quantityDenominator = null;
            string? quantityUnit = null;

            if (quantityEl != null)
            {
                // Extract numerator value and unit from the quantity element
                var numeratorEl = quantityEl.SplElement(sc.E.Numerator);
                if (numeratorEl != null)
                {
                    quantityValue = numeratorEl.GetAttrDecimal(sc.A.Value);
                    quantityUnit = numeratorEl.GetAttrVal(sc.A.Unit);
                }

                // Extract denominator value if present (for ratio quantities)
                var denominatorEl = quantityEl.SplElement(sc.E.Denominator);
                if (denominatorEl != null)
                {
                    quantityDenominator = denominatorEl.GetAttrDecimal(sc.A.Value);
                }
            }

            // 2. Extract <containerPackagedProduct> information
            var cppEl = asContentEl.SplElement(sc.E.ContainerPackagedProduct);
            string? packageFormCode = null,
                packageFormCodeSystem = null,
                packageFormDisplayName = null,
                packageCode = null,
                packageCodeSystem = null;

            if (cppEl != null)
            {
                // Extract package form information (e.g., bottle, vial, tube)
                var formCodeEl = cppEl.SplElement(sc.E.FormCode);
                if (formCodeEl != null)
                {
                    packageFormCode = formCodeEl.GetAttrVal(sc.A.CodeValue);
                    packageFormCodeSystem = formCodeEl.GetAttrVal(sc.A.CodeSystem);
                    packageFormDisplayName = formCodeEl.GetAttrVal(sc.A.DisplayName);
                }

                // Extract package identification code (e.g., NDC, UPC)
                var codeEl = cppEl.SplElement(sc.E.Code);
                if (codeEl != null)
                {
                    packageCode = codeEl.GetAttrVal(sc.A.CodeValue);
                    packageCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);
                }
            }

            // 3. Create and save the current packaging level entity
            var packagingLevel = new PackagingLevel
            {
                // Link to Product only for top-level packaging (no parent)
                ProductID = (parentPackagingLevelId == null && product != null) ? product.ProductID : null,
                ProductInstanceID = parentProductInstanceId,
                QuantityNumerator = quantityValue,
                QuantityNumeratorUnit = quantityUnit,
                QuantityDenominator = quantityDenominator,
                PackageCode = packageCode,
                PackageCodeSystem = packageCodeSystem,
                PackageFormCode = packageFormCode,
                PackageFormCodeSystem = packageFormCodeSystem,
                PackageFormDisplayName = packageFormDisplayName,
            };

            // Persist the new packaging level to the database
            await repo.CreateAsync(packagingLevel);
            count++;

            // Log successful creation with key identifying information
            context.Logger.LogInformation($"PackagingLevel created: ID={packagingLevel.PackagingLevelID}, ProductID={packagingLevel.ProductID}, FormCode={packageFormCode}");

            // 4. If this is an inner package, create the hierarchy link to its parent
            if (parentPackagingLevelId.HasValue && packagingLevel.PackagingLevelID.HasValue)
            {
                // Create parent-child relationship in PackagingHierarchy table
                await saveOrGetPackagingHierarchyAsync(
                    dbContext,
                    parentPackagingLevelId.Value,
                    packagingLevel.PackagingLevelID.Value,
                    sequenceNumber
                );

                // Log hierarchy creation for debugging and audit purposes
                context.Logger.LogInformation($"PackagingHierarchy created: OuterID={parentPackagingLevelId}, InnerID={packagingLevel.PackagingLevelID}, Seq={sequenceNumber}");
            }

            // 5. Recursively process nested <asContent> for child packaging levels
            if (cppEl != null && packagingLevel.PackagingLevelID.HasValue)
            {
                int innerSequence = 1;

                // Process each nested asContent element as a child packaging level
                foreach (var nestedAsContent in cppEl.SplElements(sc.E.AsContent))
                {
                    // Pass the current level's ID as the parent for the next level down
                    count += await parseAndSavePackagingLevelsAsync(
                        nestedAsContent,
                        product,
                        context,
                        packagingLevel.PackagingLevelID, // This level is the parent for the next
                        innerSequence,                   // Sequence of the inner package
                        parentProductInstanceId
                    );

                    // Increment sequence for next sibling package at this level
                    innerSequence++;
                }
            }

            return count;
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
        /// <remarks>
        /// This method orchestrates the parsing of a kit structure as defined in SPL IG Section 3.1.6.
        /// For each [part] element, it:
        /// 1. Extracts the quantity of the part within the kit.
        /// 2. Identifies the nested [partProduct] element.
        /// 3. Calls the main `ParseAsync` method to parse the [partProduct] as a new, complete Product.
        /// 4. Creates a `ProductPart` link between the kit and the newly created part product.
        /// </remarks>
        /// <seealso cref="ProductPart"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="ParseAsync"/>
        /// <seealso cref="saveOrGetProductPartAsync"/>
        private async Task<SplParseResult> parseAndSaveProductPartsAsync(
            XElement parentEl,
            Product kitProduct,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            if (context == null 
                || context.ServiceProvider == null 
                || context.Logger == null)
            {
                return new SplParseResult();
            }

            // Initialize aggregate result to collect outcomes from all part parsing operations
            var aggregateResult = new SplParseResult();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find all direct <part> children of the parent element
            foreach (var partEl in parentEl.SplElements(sc.E.Part))
            {
                // Report progress for tracking kit part processing
                reportProgress?.Invoke($"Starting Kit Part XML Elements {context.FileNameInZip}");

                // 1. Extract the quantity of this part within the kit
                var quantityEl = partEl.SplElement(sc.E.Quantity);
                var numeratorEl = quantityEl?.SplElement(sc.E.Numerator);

                // Parse quantity value and unit from the numerator element
                decimal? partQuantity = numeratorEl?.GetAttrDecimal(sc.A.Value);
                string? partUnit = numeratorEl?.GetAttrVal(sc.A.Unit);

                // 2. The actual product data is inside <partProduct>
                var partProductEl = partEl.SplElement(sc.E.PartProduct);
                if (partProductEl == null)
                {
                    // Log warning and skip if partProduct element is missing
                    context.Logger.LogWarning("Found <part> element without a <partProduct> child; skipping.");
                    continue;
                }

                // 3. Recursively parse the <partProduct> as a new Product.
                // We use the main parser to handle all its nested details (ingredients, packaging, etc.).
                // The `partProductEl` is treated just like a `manufacturedProduct` element.
                var partResult = await this.ParseAsync(partProductEl, context, reportProgress);

                // Merge the part parsing results into the aggregate result
                aggregateResult.MergeFrom(partResult);

                // 4. Link the newly created part product back to the kit product.
                // The `CurrentProduct` in the context will now be the part product we just created.
                if (context.CurrentProduct?.ProductID != null && kitProduct.ProductID != null)
                {
                    // Create the ProductPart relationship linking kit to part
                    await saveOrGetProductPartAsync(
                        dbContext,
                        kitProduct.ProductID.Value,
                        context.CurrentProduct.ProductID.Value,
                        partQuantity,
                        partUnit
                    );

                    // Increment count for the ProductPart link creation
                    aggregateResult.ProductElementsCreated++; // Count the ProductPart link

                    // Log successful creation of kit-part relationship
                    context.Logger.LogInformation(
                        "ProductPart link created: KitID={KitID}, PartID={PartID}, Quantity={Quantity}{Unit}",
                        kitProduct.ProductID, context.CurrentProduct.ProductID, partQuantity, partUnit);
                }
                else
                {
                    // Log error when ProductIDs are not available for linking
                    context.Logger.LogError("Failed to create ProductPart link: Kit or Part ProductID was null.");
                }

                // Report completion of this kit part processing
                reportProgress?.Invoke($"Completed Kit Part XML Elements {context.FileNameInZip}");
            }

            // Return aggregated results from all part parsing operations
            return aggregateResult;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [asPartOfAssembly] elements, creating the accessory products and linking them.
        /// </summary>
        /// <param name="parentEl">The parent XElement (usually [manufacturedProduct]) containing the assembly info.</param>
        /// <param name="primaryProduct">The primary Product entity being described.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult aggregating the results of parsing all assembly parts.</returns>
        /// <remarks>
        /// This method handles products sold separately but used together, as defined in SPL IG Sections 3.1.6 and 3.3.8.
        /// For each [asPartOfAssembly] element, it:
        /// 1. Navigates to the nested [partProduct] which defines the accessory product.
        /// 2. Recursively calls the main `ParseAsync` method to parse the accessory product.
        /// 3. Creates a `PartOfAssembly` link between the primary product and the newly created accessory product.
        /// </remarks>
        /// <seealso cref="PartOfAssembly"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="ParseAsync"/>

        /// <seealso cref="saveOrGetPartOfAssemblyAsync"/>
        private async Task<SplParseResult> parseAndSavePartOfAssemblyAsync(
            XElement parentEl,
            Product primaryProduct,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            // Validate required dependencies to ensure proper database operations
            if (context?.Logger == null || context?.ServiceProvider == null)
            {
                return new SplParseResult();
            }

            // Initialize aggregate result to collect outcomes from all assembly part parsing operations
            var aggregateResult = new SplParseResult();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find all <asPartOfAssembly> elements that define accessory products
            foreach (var assemblyEl in parentEl.SplElements(sc.E.AsPartOfAssembly))
            {
                // Report progress for tracking assembly part processing
                reportProgress?.Invoke($"Starting Part of Assembly XML Elements {context.FileNameInZip}");

                // The accessory product is defined within <wholeProduct><part><partProduct>
                // Navigate through the nested XML structure to find the partProduct element
                var accessoryProductEl = assemblyEl.SplElement(sc.E.WholeProduct, sc.E.Part, sc.E.PartProduct);
                if (accessoryProductEl == null)
                {
                    // Log warning and skip if the expected XML structure is not found
                    context.Logger.LogWarning("Found <asPartOfAssembly> without a valid <partProduct> structure; skipping.");
                    continue;
                }

                // Recursively parse the accessory product by treating its <partProduct> element
                // as a standard <manufacturedProduct> element to handle all nested details
                var accessoryResult = await this.ParseAsync(accessoryProductEl, context, reportProgress);

                // Merge the accessory parsing results into the aggregate result
                aggregateResult.MergeFrom(accessoryResult);

                // The newly parsed accessory product is now available in context.CurrentProduct
                var accessoryProduct = context.CurrentProduct;

                // Create the bidirectional assembly link if both products were created successfully
                if (accessoryProduct != null 
                    && primaryProduct.ProductID.HasValue 
                    && accessoryProduct.ProductID.HasValue)
                {
                    // Create the PartOfAssembly relationship linking primary and accessory products
                    await saveOrGetPartOfAssemblyAsync(
                        dbContext,
                        primaryProduct.ProductID.Value,
                        accessoryProduct.ProductID.Value
                    );

                    // Increment count for the PartOfAssembly link creation
                    aggregateResult.ProductElementsCreated++; // Count the PartOfAssembly link

                    // Log successful creation of assembly relationship
                    context.Logger.LogInformation(
                        "PartOfAssembly link created: ProductID1={P1}, ProductID2={P2}",
                        primaryProduct.ProductID.Value, accessoryProduct.ProductID.Value);
                }
                else
                {
                    // Log error when ProductIDs are not available for linking
                    context.Logger.LogError("Failed to create PartOfAssembly link: A ProductID was null.");
                }

                // Report completion of this assembly part processing
                reportProgress?.Invoke($"Completed Part of Assembly XML Elements {context.FileNameInZip}");
            }

            // Return aggregated results from all assembly part parsing operations
            return aggregateResult;
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
        /// <remarks>
        /// Implements a get-or-create pattern for the bidirectional relationship between two products
        /// in an assembly. To prevent duplicate entries (e.g., A-B and B-A), it stores the relationship
        /// in a canonical order, with the lower ProductID always in `PrimaryProductID`.
        /// </remarks>
        /// <seealso cref="PartOfAssembly"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Product"/>
        private async Task<PartOfAssembly> saveOrGetPartOfAssemblyAsync(
            ApplicationDbContext dbContext,
            int product1Id,
            int product2Id)
        {
            #region implementation
            // Store the relationship canonically to avoid duplicates (e.g., A-B vs. B-A)
            // Always place the smaller ID in PrimaryProductID for consistent ordering
            int primaryId = Math.Min(product1Id, product2Id);
            int accessoryId = Math.Max(product1Id, product2Id);

            // Search for an existing assembly link with the canonical IDs
            // Deduplication based on canonical ordering of PrimaryProductID and AccessoryProductID
            var existing = await dbContext.Set<PartOfAssembly>().FirstOrDefaultAsync(pa =>
                pa.PrimaryProductID == primaryId &&
                pa.AccessoryProductID == accessoryId);

            // Return the existing link if found to avoid creating duplicates
            if (existing != null)
                return existing;

            // Create a new assembly link entity with canonical ordering
            var assemblyLink = new PartOfAssembly
            {
                PrimaryProductID = primaryId,
                AccessoryProductID = accessoryId
            };

            // Save the new link to the database and persist changes immediately
            dbContext.Set<PartOfAssembly>().Add(assemblyLink);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted assembly relationship
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
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate kit-part relationships.
        /// It uses a composite key match on the kit and part product IDs for uniqueness.
        /// </remarks>
        /// <seealso cref="ProductPart"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Product"/>
        private async Task<ProductPart> saveOrGetProductPartAsync(
            ApplicationDbContext dbContext,
            int kitProductId,
            int partProductId,
            decimal? quantity,
            string? unit)
        {
            #region implementation
            // Search for an existing part link with matching kit and part IDs
            // Deduplication based on composite key: KitProductID and PartProductID
            var existing = await dbContext.Set<ProductPart>().FirstOrDefaultAsync(pp =>
                pp.KitProductID == kitProductId &&
                pp.PartProductID == partProductId);

            // Return the existing link if found to avoid creating duplicates
            if (existing != null)
                return existing;

            // Create a new product part link entity with the provided relationship data
            var productPart = new ProductPart
            {
                KitProductID = kitProductId,
                PartProductID = partProductId,
                PartQuantityNumerator = quantity,
                PartQuantityNumeratorUnit = unit
            };

            // Save the new link to the database and persist changes immediately
            dbContext.Set<ProductPart>().Add(productPart);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted product part relationship
            return productPart;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PackagingHierarchy or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="outerId">The ID of the outer (containing) packaging level.</param>
        /// <param name="innerId">The ID of the inner (contained) packaging level.</param>
        /// <param name="sequence">The sequence number of the inner package within the outer package.</param>
        /// <returns>The existing or newly created PackagingHierarchy entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate hierarchy records.
        /// It uses a composite key match on the outer ID, inner ID, and sequence number for uniqueness.
        /// </remarks>
        /// <seealso cref="PackagingHierarchy"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="PackagingLevel"/>
        private async Task<PackagingHierarchy> saveOrGetPackagingHierarchyAsync(
            ApplicationDbContext dbContext,
            int outerId,
            int innerId,
            int? sequence)
        {
            #region implementation
            // Search for an existing hierarchy link with matching parameters
            // Deduplication based on composite key: OuterPackagingLevelID, InnerPackagingLevelID, and SequenceNumber
            var existing = await dbContext.Set<PackagingHierarchy>().FirstOrDefaultAsync(ph =>
                ph.OuterPackagingLevelID == outerId &&
                ph.InnerPackagingLevelID == innerId &&
                ph.SequenceNumber == sequence);

            // Return the existing link if found to avoid creating duplicates
            if (existing != null)
                return existing;

            // Create a new hierarchy link entity with the provided relationship data
            var hierarchyLink = new PackagingHierarchy
            {
                OuterPackagingLevelID = outerId,
                InnerPackagingLevelID = innerId,
                SequenceNumber = sequence
            };

            // Save the new link to the database and persist changes immediately
            dbContext.Set<PackagingHierarchy>().Add(hierarchyLink);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted hierarchy link
            return hierarchyLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers the package identifier type (e.g., 'NDCPackage') based on the OID.
        /// </summary>
        /// <param name="oid">The OID string for the code system.</param>
        /// <returns>A formatted package identifier type string, or null if not recognized.</returns>
        /// <remarks>
        /// This helper calls the general <see cref="inferIdentifierType"/> method and then formats
        /// the result to match the package-specific naming convention (e.g., 'ISBT 128' becomes 'ISBTPackage').
        /// </remarks>
        /// <seealso cref="inferIdentifierType"/>
        /// <seealso cref="PackageIdentifier"/>
        private static string? inferPackageIdentifierType(string? oid)
        {
            #region implementation
            // Call the general identifier type inference method to get base type
            var baseType = inferIdentifierType(oid);
            if (string.IsNullOrEmpty(baseType))
            {
                // Return null if no base type could be inferred from the OID
                return null;
            }

            // Format to match model examples: "ISBT 128" -> "ISBTPackage"
            // Remove spaces and append "Package" suffix for consistent naming convention
            return $"{baseType.Replace(" ", "")}Package";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves PackageIdentifier entities from a [containerPackagedProduct] element for a given packaging level.
        /// </summary>
        /// <param name="containerPackagedProductEl">The [containerPackagedProduct] XElement to parse.</param>
        /// <param name="packagingLevel">The PackagingLevel entity to link the identifiers to.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of PackageIdentifier records created.</returns>
        /// <remarks>
        /// Handles the [code] element within a [containerPackagedProduct] node, which represents the
        /// package item code (e.g., NDC Package Code). It infers the identifier type from the code system OID
        /// and creates a link to the parent packaging level, as specified in SPL IG Section 3.1.5.
        /// </remarks>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="inferPackageIdentifierType"/>
        private async Task<int> parseAndSavePackageIdentifiersAsync(
            XElement containerPackagedProductEl,
            PackagingLevel packagingLevel,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Get repository for PackageIdentifier database operations
            var repo = context.GetRepository<PackageIdentifier>();

            // Validate required dependencies to ensure proper database operations
            if (context?.Logger == null || repo == null || !packagingLevel.PackagingLevelID.HasValue)
            {
                // Log warning when critical dependencies are missing
                context?.Logger?.LogWarning("Could not parse PackageIdentifier due to invalid context or missing PackagingLevelID.");
                return 0;
            }

            // The package item code is in the <code> child of <containerPackagedProduct>
            var codeEl = containerPackagedProductEl.SplElement(sc.E.Code);
            if (codeEl == null)
            {
                // No code element found, which is valid in some cases (e.g., compounded drugs per IG 3.1.5.12)
                return 0;
            }

            // Extract identifier value and system OID from the code element attributes
            string? identifierValue = codeEl.GetAttrVal(sc.A.CodeValue);
            string? identifierSystemOID = codeEl.GetAttrVal(sc.A.CodeSystem);

            // Both value and system are required to create a valid identifier
            if (string.IsNullOrWhiteSpace(identifierValue) || string.IsNullOrWhiteSpace(identifierSystemOID))
            {
                // Return early if required data is missing - no error logging as this is expected in some scenarios
                return 0;
            }

            // Infer the type (e.g., 'NDCPackage', 'GS1Package') from the system OID
            string? identifierType = inferPackageIdentifierType(identifierSystemOID);

            // Create and save the PackageIdentifier entity with extracted data
            var packageIdentifier = new PackageIdentifier
            {
                PackagingLevelID = packagingLevel.PackagingLevelID,
                IdentifierValue = identifierValue,
                IdentifierSystemOID = identifierSystemOID,
                IdentifierType = identifierType
            };

            // Persist the new PackageIdentifier to the database
            await repo.CreateAsync(packageIdentifier);
            count++;

            // Log successful creation with key details for debugging and audit purposes
            context.Logger.LogInformation(
                "PackageIdentifier created: PackagingLevelID={PackagingLevelID}, Value={IdentifierValue}, Type={IdentifierType}",
                packagingLevel.PackagingLevelID, identifierValue, identifierType);

            return count;
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

                // Example: Custom OIDs (if needed, add here)
                //"X.Y.Z..." => "CustomType",

                _ => null
            };
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
                context.Logger.LogInformation($"ProductIdentifier created: ProductID={product.ProductID} Value={codeVal} OID={codeSystem}");
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
                context.Logger.LogInformation("Approval ProductIdentifier created: ProductID={ProductID} Value={IdentifierValue} OID={IdentifierSystemOID}",
                    product.ProductID, codeVal, codeSystem);
            }
            return count;
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
        /// <seealso cref="SpecializedKindValidator"/>
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
                    context.Logger.LogWarning("No <code> found under <generalizedMaterialKind> in <asSpecializedKind>; skipping.");
                    continue;
                }

                // Extract and clean the code attributes
                var kindCode = codeEl.GetAttrVal(sc.A.CodeValue)?.Trim();
                var kindCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem)?.Trim();
                var kindDisplayName = codeEl.GetAttrVal(sc.A.DisplayName)?.Trim();

                // Validate required fields are present
                if (string.IsNullOrWhiteSpace(kindCode) || string.IsNullOrWhiteSpace(kindCodeSystem))
                {
                    context.Logger.LogWarning("Missing code or codeSystem in <asSpecializedKind>/<code>; skipping.");
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
            var validatedKinds = SpecializedKindValidator.ValidateCosmeticCategoryRules(
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