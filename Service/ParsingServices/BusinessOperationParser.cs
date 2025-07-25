
using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses business operations, their links to products, and the underlying document and
    /// organizational relationships from an SPL document.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex `performance` sections within a product definition, which
    /// describe activities like manufacturing, packing, and labeling. It requires both a valid
    /// `CurrentProduct` and `Document` in the `SplParseContext`.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="BusinessOperation"/>
    /// <seealso cref="BusinessOperationProductLink"/>
    /// <seealso cref="DocumentRelationship"/>
    /// <seealso cref="SplParseContext"/>
    public class BusinessOperationParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "businessoperation";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a parent XML element to extract and save all business operations and their related entities.
        /// </summary>
        /// <param name="element">The XElement (e.g., manufacturedProduct) containing the business operations.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct and Document.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This is the main entry point for the parser. It validates the context and then calls the
        /// main orchestration method to handle the parsing logic.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that both a product and a document are available in the context.
            if (context?.CurrentProduct?.ProductID == null || !isDocumentContextValid(context))
            {
                result.Success = false;
                result.Errors.Add("Cannot parse business operations due to invalid product or document context.");
                context?.Logger?.LogError("BusinessOperationParser was called without a valid product or document in the context.");
                return result;
            }

            reportProgress?.Invoke($"Starting Business Operation XML Elements {context.FileNameInZip}");
            var createdCount = await parseAndSaveBusinessOperationAndLinksAsync(element, context.CurrentProduct, context);
            result.ProductElementsCreated += createdCount;

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all BusinessOperation and BusinessOperationProductLink entities.
        /// </summary>
        /// <param name="parentEl">The parent XML element to search for performance elements.</param>
        /// <param name="product"> The product to link business operations to.</param>
        /// <param name="context">The parsing context containing document and service provider access.</param>
        /// <returns>The total count of business operation product links created.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of business operations by:
        /// 1. Validating the parsing context and retrieving the labeler organization.
        /// 2. Getting all document relationships for the current document.
        /// 3. Processing performance elements for each document relationship.
        /// 4. Creating business operations and their product links.
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
            int createdCount = 0;

            // Validate the parsing context.
            if (context == null
                || context.ServiceProvider == null
                || context.Logger == null
                || context.Document == null)
            {
                context?.Logger?.LogError("BusinessOperationParser called with invalid context.");
                return 0;
            }

            var dbContext = context?.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int? docId = context?.Document.DocumentID;

            if (dbContext != null)
            {
                // Get the labeler organization ID for this document.
                var labelerOrgId = await getLabelerOrganizationIdAsync(dbContext, docId, context.Logger);
                if (labelerOrgId == null) return 0;

                // Get all document relationships for processing.
                var docRels = await getDocumentRelationshipsAsync(dbContext, docId, context.Logger);
                if (!docRels.Any()) return 0;


                // Process business operations for each document relationship.
                foreach (var docRel in docRels)
                {
                    // Ensure the document relationship exists or create it.
                    var thisDocRel = await saveOrGetDocumentRelationshipAsync(
                        dbContext, docId, labelerOrgId, docRel.ChildOrganizationID, docRel.RelationshipType, docRel.RelationshipLevel);

                    // Parse performance elements and create business operation links.
                    createdCount += await parsePerformanceElementsAsync(
                        parentEl, dbContext, product, thisDocRel.DocumentRelationshipID, context.Logger, context);
                }
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
            var labeler = await dbContext.Set<DocumentAuthor>()
                .FirstOrDefaultAsync(a => a.DocumentID == docId && a.AuthorType == "Labeler");
            if (labeler == null)
            {
                logger.LogWarning("Labeler DocumentAuthor not found for this document.");
                return null;
            }

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
            var docRels = await dbContext.Set<DocumentRelationship>()
                .Where(r => r.DocumentID == docId)
                .ToListAsync();

            if (!docRels.Any())
                logger.LogWarning("No DocumentRelationships found for this document.");

            return docRels;
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
            var existing = await dbContext.Set<DocumentRelationship>().FirstOrDefaultAsync(dr =>
                dr.DocumentID == docId &&
                dr.ParentOrganizationID == parentOrgId &&
                dr.ChildOrganizationID == childOrgId &&
                dr.RelationshipType == relationshipType &&
                dr.RelationshipLevel == relationshipLevel);

            if (existing != null)
                return existing;

            var rel = new DocumentRelationship
            {
                DocumentID = docId,
                ParentOrganizationID = parentOrgId,
                ChildOrganizationID = childOrgId,
                RelationshipType = relationshipType,
                RelationshipLevel = relationshipLevel
            };

            dbContext.Set<DocumentRelationship>().Add(rel);
            await dbContext.SaveChangesAsync();
            return rel;
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
        /// <param name="context">The parsing context for delegation to child parsers.</param>
        /// <returns>The number of product links created.</returns>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="BusinessOperationProductLink"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parsePerformanceElementsAsync(
            XElement parentEl,
            ApplicationDbContext dbContext,
            Product product,
            int? documentRelationshipId,
            ILogger logger,
            SplParseContext context) // <-- Add context parameter
        {
            #region implementation
            int createdLinks = 0;

            foreach (var perfEl in parentEl.SplElements(sc.E.Performance))
            {
                foreach (var actDefEl in perfEl.SplElements(sc.E.ActDefinition))
                {
                    // Updated method call with context parameter
                    var bizOp = await parseBusinessOperationAsync(dbContext, documentRelationshipId, actDefEl, context);
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
        /// <param name="context">The parsing context for license parsing delegation.</param>
        /// <returns>The existing or newly created BusinessOperation entity.</returns>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperation> parseBusinessOperationAsync(
            ApplicationDbContext dbContext, int? docRelId, XElement actDefEl, SplParseContext context)
        {
            #region implementation
            var opCodeEl = actDefEl.GetSplElement(sc.E.Code);
            string? opCode = opCodeEl?.GetAttrVal(sc.A.CodeValue);
            string? opCodeSystem = opCodeEl?.GetAttrVal(sc.A.CodeSystem);
            string? opDisplayName = opCodeEl?.GetAttrVal(sc.A.DisplayName);

            var bizOp = await getOrSaveBusinessOperationAsync(
                dbContext, docRelId, opCode, opCodeSystem, opDisplayName);

            if (bizOp != null && bizOp.BusinessOperationID > 0)
            {
                // --- SET BUSINESS OPERATION CONTEXT FOR LICENSE PARSER ---
                var oldBusinessOperation = context.CurrentBusinessOperation;
                context.CurrentBusinessOperation = bizOp;

                try
                {
                    foreach (var approvalEl in actDefEl.SplElements(sc.E.SubjectOf, sc.E.Approval))
                    {
                        var qualifierCodeEl = approvalEl.GetSplElement(sc.E.Code);
                        if (qualifierCodeEl == null) continue;

                        string? qualifierCode = qualifierCodeEl.GetAttrVal(sc.A.CodeValue);
                        string? qualifierCodeSystem = qualifierCodeEl.GetAttrVal(sc.A.CodeSystem);
                        string? qualifierDisplayName = qualifierCodeEl.GetAttrVal(sc.A.DisplayName);

                        if (string.IsNullOrWhiteSpace(qualifierCode) || bizOp.BusinessOperationID == null)
                        {
                            continue;
                        }

                        // --- EXISTING QUALIFIER PROCESSING ---
                        await getOrSaveBusinessOperationQualifierAsync(
                            dbContext, bizOp.BusinessOperationID, qualifierCode, qualifierCodeSystem, qualifierDisplayName);

                        // --- NEW LICENSE PARSING INTEGRATION ---
                        // Check if this approval represents a license (C118777 = licensing)
                        if (string.Equals(qualifierCode, "C118777", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Logger?.LogInformation($"Processing license for BusinessOperation {bizOp.BusinessOperationID}");

                            // Delegate license parsing to specialized parser
                            var licenseParser = new LicenseParser();
                            var licenseResult = await licenseParser.ParseAsync(approvalEl, context, null);

                            if (!licenseResult.Success)
                            {
                                foreach (var error in licenseResult.Errors)
                                {
                                    context.Logger?.LogWarning($"License parsing warning: {error}");
                                }
                            }
                            else if (licenseResult.LicensesCreated > 0)
                            {
                                context.Logger?.LogInformation($"Created {licenseResult.LicensesCreated} license(s) for BusinessOperation {bizOp.BusinessOperationID}");
                            }
                        }
                    }
                }
                finally
                {
                    // Restore previous business operation context
                    context.CurrentBusinessOperation = oldBusinessOperation;
                }
            }

            return bizOp ?? new BusinessOperation();
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
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperation> getOrSaveBusinessOperationAsync(
            ApplicationDbContext dbContext,
            int? documentRelationshipId,
            string? operationCode,
            string? operationCodeSystem,
            string? operationDisplayName)
        {
            #region implementation
            var existing = await dbContext.Set<BusinessOperation>().FirstOrDefaultAsync(op =>
                op.DocumentRelationshipID == documentRelationshipId &&
                op.OperationCode == operationCode &&
                op.OperationCodeSystem == operationCodeSystem &&
                op.OperationDisplayName == operationDisplayName);

            if (existing != null)
                return existing;

            var newOp = new BusinessOperation
            {
                DocumentRelationshipID = documentRelationshipId,
                OperationCode = operationCode,
                OperationCodeSystem = operationCodeSystem,
                OperationDisplayName = operationDisplayName
            };

            dbContext.Set<BusinessOperation>().Add(newOp);
            await dbContext.SaveChangesAsync();
            return newOp;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing BusinessOperationQualifier or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="businessOperationId">The business operation ID to associate with the qualifier.</param>
        /// <param name="qualifierCode">The code identifying the business operation qualifier.</param>
        /// <param name="qualifierCodeSystem">The code system for the qualifier code.</param>
        /// <param name="qualifierDisplayName">The display name for the qualifier.</param>
        /// <returns>The existing or newly created BusinessOperationQualifier entity.</returns>
        /// <seealso cref="BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperationQualifier> getOrSaveBusinessOperationQualifierAsync(
            ApplicationDbContext dbContext,
            int? businessOperationId,
            string? qualifierCode,
            string? qualifierCodeSystem,
            string? qualifierDisplayName)
        {
            #region implementation
            var existing = await dbContext.Set<BusinessOperationQualifier>().FirstOrDefaultAsync(q =>
                q.BusinessOperationID == businessOperationId &&
                q.QualifierCode == qualifierCode &&
                q.QualifierCodeSystem == qualifierCodeSystem &&
                q.QualifierDisplayName == qualifierDisplayName);

            if (existing != null)
                return existing;

            var newQualifier = new BusinessOperationQualifier
            {
                BusinessOperationID = businessOperationId,
                QualifierCode = qualifierCode,
                QualifierCodeSystem = qualifierCodeSystem,
                QualifierDisplayName = qualifierDisplayName
            };

            dbContext.Set<BusinessOperationQualifier>().Add(newQualifier);
            await dbContext.SaveChangesAsync();
            return newQualifier;
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

            if (bizOp == null || bizOp.BusinessOperationID == null)
            {
                logger.LogWarning("Business operation is null or has no ID, skipping product link creation.");
                return linksCreated;
            }

            foreach (var productEl in actDefEl.SplElements(sc.E.Product))
            {
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
            dbContext.Set<BusinessOperationProductLink>().Add(new BusinessOperationProductLink
            {
                BusinessOperationID = businessOperationId,
                ProductID = productId
            });
            await dbContext.SaveChangesAsync();
            #endregion
        }
    }
}