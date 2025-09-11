using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Models;
using System.Security.Cryptography;
using AngleSharp.Svg.Dom;
using System.Collections.Generic;
using static MedRecPro.Models.Constant;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses the author element, finds or creates the representedOrganization,
    /// and links it to the document. Normalizes organization data to prevent duplicates.
    /// Enhanced to capture business operations during author parsing to ensure complete
    /// organizational hierarchy with associated business operations.
    /// </summary>
    /// <remarks>
    /// This parser specifically handles the author section of SPL documents, extracting
    /// organization information from the representedOrganization element and creating
    /// appropriate database entities and relationships. It implements deduplication
    /// logic to prevent duplicate organizations in the database. Enhanced to integrate
    /// business operation parsing to address order-of-operations issues where business
    /// operations were missed during author processing.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Organization"/>
    /// <seealso cref="DocumentAuthor"/>
    /// <seealso cref="BusinessOperation"/>
    /// <seealso cref="Label"/>
    public class AuthorSectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, using the constant for Author element.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.SplConstants"/>
        public string SectionName => sc.E.Author;

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Reference to the Business Operation Parser for delegating business operation processing.
        /// </summary>
        /// <seealso cref="BusinessOperationParser"/>
        private readonly BusinessOperationParser _businessOperationParser;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the AuthorSectionParser with business operation parsing capability.
        /// </summary>
        /// <remarks>
        /// Creates the parser with an integrated BusinessOperationParser to handle business operations
        /// during author processing, ensuring operations are captured regardless of parser execution order.
        /// </remarks>
        /// <seealso cref="BusinessOperationParser"/>
        public AuthorSectionParser()
        {
            #region implementation
            _businessOperationParser = new BusinessOperationParser();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the author section of an SPL document, extracting organization information
        /// and creating necessary database entities and relationships. Enhanced to capture
        /// business operations during author processing.
        /// </summary>
        /// <param name="element">The XElement representing the author section to parse.</param>
        /// <param name="context">The current parsing context containing document and service information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new AuthorSectionParser();
        /// var result = await parser.ParseAsync(authorElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Organizations created: {result.OrganizationsCreated}");
        ///     Console.WriteLine($"Business operations captured: {result.ProductElementsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates the document context exists
        /// 2. Extracts the representedOrganization element from the author structure
        /// 3. Gets or creates the organization entity in the database
        /// 4. Creates a DocumentAuthor link between the document and organization
        /// 5. Processes organizational hierarchy and captures associated business operations
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element,
            SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();
            int orgID = 0;
            int docID = 0;

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate that we have a valid document context to work with
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse author because no document context exists.");
                return result;
            }

            docID = context.Document.DocumentID.Value;

            reportProgress?.Invoke($"Starting Author XML Elements {context.FileNameInZip}");

            // Navigate to the organization element using the SPL structure constants
            // Path: author/assignedEntity/representedOrganization
            var authorOrgElement = element.GetSplElement(sc.E.AssignedEntity)
                ?.GetSplElement(sc.E.RepresentedOrganization);

            // If no organization element found, log warning and return successful result
            if (authorOrgElement == null)
            {
                context.Logger.LogWarning("No <{OrganizationElement}> found within <{AuthorElement}> for file {FileName}",
                    sc.E.RepresentedOrganization, sc.E.Author, context.FileNameInZip);
                return result;
            }

            try
            {
                // --- PARSE ORGANIZATION ---
                var (organization, orgCreated) = await getOrCreateOrganizationAsync(authorOrgElement, context);

                // Validate that we successfully obtained an organization
                if (organization?.OrganizationID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to get or create an organization for the author.");
                    return result;
                }

                // Set org id
                orgID = organization.OrganizationID.Value;

                // Log the organization creation or retrieval result
                if (orgCreated)
                {
                    result.OrganizationsCreated++;
                    context?.Logger?.LogInformation("Created new Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }
                else
                {
                    context.Logger.LogInformation("Found existing Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }

                // --- DETERMINE AUTHOR TYPE FROM BUSINESS OPERATIONS ---
                var authorType = determineDocumentAuthorType(element, context!);

                // --- PARSE AUTHOR ---
                var (docAuthor, docAuthorCreated) = await getOrCreateDocumentAuthorAsync(
                    context!.Document.DocumentID.Value,
                    orgID,
                    authorType,
                    context);

                // Log the document author link creation if it was newly created
                if (docAuthorCreated)
                {
                    context.Logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID} as {AuthorType}",
                       docID, orgID, authorType);

                    reportProgress?.Invoke($"Completed Author XML Elements {context.FileNameInZip}");
                }

                // --- PARSE DOCUMENT RELATIONSHIP WITH BUSINESS OPERATIONS ---
                var relationshipsCount = await parseOrganizationalHierarchyWithBusinessOperationsAsync(
                     element, context, docID, orgID, authorType);
                result.OrganizationsCreated += relationshipsCount.OrganizationsCreated;
                result.ProductElementsCreated += relationshipsCount.BusinessOperationsCreated;

                // --- PARSE CONTACT PARTIES ---
                var (partiesCreated, telecomsCreated) = await parseAndSaveContactPartiesAsync(element, context, orgID);
                result.OrganizationAttributesCreated += partiesCreated;
                result.OrganizationAttributesCreated += telecomsCreated;

                // --- PARSE TELECOMS ---
                var telecomCt = await parseAndSaveOrganizationTelecomsAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += telecomCt;

                // --- PARSE ORGANIZATION IDENTIFIERS ---
                var identifiers = await getOrCreateOrganizationIdentifierAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += identifiers?.Count ?? 0;

                // --- PARSE ORGANIZATION NAMED ENTITIES ---
                var namedEntities = await getOrCreateNamedEntitiesAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += namedEntities?.Count ?? 0;

            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during parsing
                result.Success = false;
                result.Errors.Add($"Error parsing author: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <{AuthorElement}> element.", sc.E.Author);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates an Organization by its identifier (e.g., FEI, DUNS). This is the preferred method for establishments.
        /// </summary>
        /// <param name="orgEl">The XElement containing the organization's data (e.g., representedOrganization).</param>
        /// <param name="context">The parsing context for DB access.</param>
        /// <returns>The existing or newly created Organization entity.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        public static async Task<(Organization? Organization, bool Created)> GetOrCreateOrganizationByIdentifierAsync(XElement orgEl, SplParseContext context)
        {
            #region implementation
            if (orgEl == null) return (null, false);

            var idEl = orgEl.SplElement(sc.E.Id);
            var identifierValue = idEl?.GetAttrVal(sc.A.Extension);
            var identifierRoot = idEl?.GetAttrVal(sc.A.Root);

            if (string.IsNullOrWhiteSpace(identifierValue))
            {
                context?.Logger?.LogWarning("Organization element is missing an identifier. Falling back to name-based lookup.");
                return await GetOrCreateOrganizationByNameAsync(orgEl, context!);
            }

            if (context == null || context.ServiceProvider == null)
            {
                context?.Logger?.LogError("Parsing context or service provider is null.");
                return (null, false);
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Step 1: Search for an existing OrganizationIdentifier
            var existingIdentifier = await dbContext.Set<OrganizationIdentifier>()
                .Include(oi => oi.Organization) // Eager load the related Organization
                .FirstOrDefaultAsync(oi => oi.IdentifierValue == identifierValue && oi.IdentifierSystemOID == identifierRoot);

            if (existingIdentifier?.Organization != null)
            {
                // Step 2: If found, return the associated Organization
                return (existingIdentifier.Organization, false);
            }

            // Step 3: If not found, create both the Organization and the OrganizationIdentifier
            var orgRepo = context.GetRepository<Organization>();
            var orgName = orgEl.GetSplElementVal(sc.E.Name)?.Trim();

            if (string.IsNullOrWhiteSpace(orgName))
            {
                context?.Logger?.LogError("Cannot create new organization for identifier '{Identifier}' because its name is missing.", identifierValue);
                return (null, false);
            }

            // Step 3a: Create the new Organization
            var newOrganization = new Organization
            {
                OrganizationName = orgName,
                IsConfidential = orgEl.GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };
            await orgRepo.CreateAsync(newOrganization);

            if (newOrganization.OrganizationID == null)
            {
                context?.Logger?.LogError("Failed to save new organization '{OrgName}'.", orgName);
                return (null, false);
            }

            // Step 3b: Create the new OrganizationIdentifier and link it
            var identifierRepo = context.GetRepository<OrganizationIdentifier>();
            var newIdentifier = new OrganizationIdentifier
            {
                OrganizationID = newOrganization.OrganizationID,
                IdentifierValue = identifierValue,
                IdentifierSystemOID = identifierRoot,
                IdentifierType = inferIdentifierTypeFromOid(identifierRoot) // Helper to map OID to a friendly type
            };
            await identifierRepo.CreateAsync(newIdentifier);

            context?.Logger?.LogInformation("Created new Organization '{OrgName}' (ID: {OrgId}) with Identifier '{Identifier}'", orgName, newOrganization.OrganizationID, identifierValue);
            return (newOrganization, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps FDA business operation codes to corresponding author types based on SPL Implementation Guide v1.
        /// </summary>
        /// <param name="operationCode">The business operation code from FDA's standardized list (e.g., "C43360", "C82401").</param>
        /// <returns>The corresponding author type string, or null if the operation code is not recognized.</returns>
        /// <remarks>
        /// This method maps FDA business operation codes to logical author types based on the nature of the operation.
        /// Manufacturing operations map to "Manufacturer", packaging operations to "Packager", etc.
        /// All codes are validated against the SPL Implementation Guide with Validation Procedures v1, Section 4.1.4.8.
        /// </remarks>
        /// <example>
        /// <code>
        /// string? authorType = mapOperationCodeToAuthorType("C43360"); // Returns "Manufacturer"
        /// string? authorType2 = mapOperationCodeToAuthorType("C84731"); // Returns "Packager"
        /// string? authorType3 = mapOperationCodeToAuthorType("INVALID"); // Returns null
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        private string? mapOperationCodeToAuthorType(string operationCode)
        {
            #region implementation
            // Validate input parameter
            if (string.IsNullOrWhiteSpace(operationCode))
                return null;

            // Map FDA business operation codes to author types based on SPL Implementation Guide
            return operationCode switch
            {
                // Manufacturing operations - entities that produce or manufacture products
                "C43360" => "Manufacturer",        // MANUFACTURE
                "C82401" => "Manufacturer",        // API MANUFACTURE  
                "C91403" => "Manufacturer",        // POSITRON EMISSION TOMOGRAPHY DRUG PRODUCTION
                "C101510" => "Manufacturer",       // FDF MANUFACTURE

                // Packaging operations - entities that package or repackage products
                "C84731" => "Packager",           // PACK
                "C73606" => "Packager",           // REPACK

                // Labeling operations - entities that label or relabel products
                "C84732" => "Labeler",            // LABEL
                "C73607" => "Labeler",            // RELABEL

                // Analysis and testing operations - entities that perform analytical testing
                "C101509" => "Analyzer",          // API/FDF ANALYTICAL TESTING
                "C101511" => "Analyzer",          // CLINICAL BIOEQUIVALENCE OR BIOAVAILABILITY STUDY
                "C101512" => "Analyzer",          // IN VITRO BIOEQUIVALENCE OR BIOANALYTICAL TESTING

                // Distribution operations - entities that distribute products
                "C73608" => "Distributor",        // DISTRIBUTES DRUG PRODUCTS UNDER OWN PRIVATE LABEL
                "C118411" => "Distributor",       // WHOLESALE DRUG DISTRIBUTOR
                "C118412" => "Distributor",       // THIRD-PARTY LOGISTICS PROVIDER

                // Import operations - entities that import products
                "C73599" => "Importer",           // IMPORT

                // Agent operations - entities acting as agents
                "C73330" => "Agent",              // UNITED STATES AGENT

                // Compounding operations - entities that compound drugs
                "C112113" => "Compounder",        // HUMAN DRUG COMPOUNDING OUTSOURCING FACILITY
                "C122061" => "Compounder",        // OUTSOURCING ANIMAL DRUG COMPOUNDING

                // Salvage operations - entities that salvage products
                "C70827" => "Salvager",           // SALVAGE

                // Unknown or unsupported operation codes
                _ => null
            };
            #endregion
        }

        /**************************************************************/

        /// <summary>
        /// Infers a friendly identifier type from its OID root based on FDA and industry standards.
        /// </summary>
        /// <param name="oid">The OID root string to analyze for identifier type determination.</param>
        /// <returns>A friendly identifier type string corresponding to the OID, or "Other" if not recognized.</returns>
        /// <remarks>
        /// Maps standard OID roots to their corresponding identifier types used in pharmaceutical 
        /// and healthcare industries. Includes FDA-specific identifiers (DUNS, FEI, NDC) and 
        /// industry standards (GS1, HIBCC, ISBT 128). Based on SPL Implementation Guide and 
        /// regulatory identifier specifications.
        /// </remarks>
        /// <example>
        /// <code>
        /// string type1 = inferIdentifierTypeFromOid("1.3.6.1.4.1.519.1"); // Returns "DUNS"
        /// string type2 = inferIdentifierTypeFromOid("2.16.840.1.113883.6.69"); // Returns "NDC/NHRIC"
        /// string type3 = inferIdentifierTypeFromOid("1.3.160"); // Returns "GS1"
        /// string type4 = inferIdentifierTypeFromOid("unknown.oid"); // Returns "Other"
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        private static string inferIdentifierTypeFromOid(string? oid)
        {
            #region implementation
            // Handle null or empty OID inputs
            if (string.IsNullOrWhiteSpace(oid))
                return "Other";

            // Map OID roots to friendly identifier types based on industry and regulatory standards
            return oid switch
            {
                // FDA and regulatory identifiers
                "1.3.6.1.4.1.519.1" => "DUNS",                    // Data Universal Numbering System
                "2.16.840.1.113883.4.82" => "FEI",                // FDA Facility Establishment Identifier
                "2.16.840.1.113883.6.69" => "NDC/NHRIC",          // National Drug Code / National Health Related Item Code
                "2.16.840.1.113883.3.9848" => "Cosmetic Product Listing Number", // FDA Cosmetic Product Listing Number

                // Industry standard identifiers  
                "1.3.160" => "GS1",                               // GS1 Global Trade Item Number
                "2.16.840.1.113883.6.40" => "HIBCC",              // Health Industry Business Communications Council
                "2.16.840.1.113883.6.18" => "ISBT 128",           // International Society of Blood Transfusion 128

                // Unknown or unsupported OID roots
                _ => "Other"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the document author type based on business operations found in performance elements.
        /// Maps business operation codes to appropriate author types with intelligent fallback logic.
        /// </summary>
        /// <param name="authorElement">The author XML element containing performance/business operation data.</param>
        /// <param name="context">The parsing context for logging and database access.</param>
        /// <returns>The determined author type (e.g., "Manufacturer", "Packager", "Labeler", etc.).</returns>
        /// <remarks>
        /// This method analyzes business operation codes to determine the organization's primary role:
        /// - Manufacturing operations (C43360, C82401, etc.) → "Manufacturer"
        /// - Packaging operations (C84731, C73606) → "Packager" 
        /// - Labeling operations (C84732, C73607) → "Labeler"
        /// - Analysis/Testing operations → "Analyzer"
        /// - Multiple operations → Combined type (e.g., "Manufacturer/Packager")
        /// - No operations found → "Labeler" (default for author section)
        /// </remarks>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private string determineDocumentAuthorType(XElement authorElement, SplParseContext context)
        {
            #region implementation
            var operationTypes = new HashSet<string>();

            try
            {
                // Find all business operations in performance elements
                var assignedEntityEl = authorElement.GetSplElement(sc.E.AssignedEntity);
                if (assignedEntityEl != null)
                {
                    foreach (var performanceEl in assignedEntityEl.SplElements(sc.E.Performance))
                    {
                        foreach (var actDefEl in performanceEl.SplElements(sc.E.ActDefinition))
                        {
                            var codeEl = actDefEl.GetSplElement(sc.E.Code);
                            var operationCode = codeEl?.GetAttrVal(sc.A.CodeValue);

                            if (!string.IsNullOrWhiteSpace(operationCode))
                            {
                                var authorType = mapOperationCodeToAuthorType(operationCode);
                                if (!string.IsNullOrWhiteSpace(authorType))
                                {
                                    operationTypes.Add(authorType);
                                }
                            }
                        }
                    }
                }

                // If no operations found, default to "Labeler" for author section
                if (!operationTypes.Any())
                {
                    context?.Logger?.LogInformation("No business operations found in author section, defaulting to 'Labeler'");
                    return "Labeler";
                }

                // If multiple operation types found, combine them in priority order
                if (operationTypes.Count > 1)
                {
                    var priorityOrder = new[] { "Manufacturer", "Packager", "Labeler", "Analyzer", "Distributor", "Importer" };
                    var sortedTypes = operationTypes
                        .OrderBy(type => Array.IndexOf(priorityOrder, type) == -1 ? int.MaxValue : Array.IndexOf(priorityOrder, type))
                        .ToList();

                    var combinedType = string.Join("/", sortedTypes);
                    context?.Logger?.LogInformation("Multiple business operations found, combined author type: {AuthorType}", combinedType);
                    return combinedType;
                }

                // Single operation type found
                var singleType = operationTypes.First();
                context?.Logger?.LogInformation("Single business operation found, author type: {AuthorType}", singleType);
                return singleType;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogWarning(ex, "Error determining author type from business operations, defaulting to 'Labeler'");
                return "Labeler";
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enhanced organizational hierarchy parsing that captures business operations
        /// during author processing to address order-of-operations issues.
        /// </summary>
        /// <param name="authorEl">The author XML element containing organization hierarchy information.</param>
        /// <param name="context">The parsing context providing database access and logging services.</param>
        /// <param name="documentId">The document ID to associate relationships with.</param>
        /// <param name="labelerId">The labeler organization ID as the root of the hierarchy.</param>
        /// <param name="authorType">The determined author type for this organization.</param>
        /// <returns>A tuple containing counts of organizations and business operations created.</returns>
        /// <remarks>
        /// This enhanced method processes the organizational hierarchy while simultaneously
        /// capturing business operations from performance elements. This ensures business
        /// operations are available during author processing, solving the issue where
        /// business operations were missed due to parser execution order.
        /// </remarks>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task<(int OrganizationsCreated, int BusinessOperationsCreated)> parseOrganizationalHierarchyWithBusinessOperationsAsync(
            XElement authorEl, SplParseContext context, int documentId, int labelerId, string authorType)
        {
            #region implementation
            int orgCount = 0;
            int bizOpCount = 0;

            if (context?.ServiceProvider == null || context.Logger == null)
                return (orgCount, bizOpCount);

            var labelerEntityEl = authorEl.GetSplElement(sc.E.AssignedEntity);
            if (labelerEntityEl == null) return (orgCount, bizOpCount);

            var representedOrgEl = labelerEntityEl.GetSplElement(sc.E.RepresentedOrganization);
            if (representedOrgEl == null) return (orgCount, bizOpCount);

            // Start recursive parsing from the represented organization
            var result = await parseHierarchyLevelWithBusinessOperationsAsync(
                representedOrgEl, context, documentId, labelerId, authorType, 1);

            return (result.OrganizationsCreated, result.BusinessOperationsCreated);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// </summary>
        /// <param name="orgElement">The XElement representing the organization (e.g., representedOrganization).</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and a boolean indicating if it was newly created.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        public static async Task<(Organization? Organization, bool Created)> GetOrCreateOrganizationByNameAsync(XElement orgElement, SplParseContext context)
        {
            #region implementation
            var orgName = orgElement.GetSplElementVal(sc.E.Name)?.Trim();
            if (string.IsNullOrWhiteSpace(orgName)) return (null, false);

            var dbContext = context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>();
            var orgRepo = context?.GetRepository<Organization>();

            if (dbContext == null || orgRepo == null)
            {
                context?.Logger?.LogError("Database context or repository is not available.");
                return (null, false);
            }

            var existingOrg = await dbContext.Set<Organization>().FirstOrDefaultAsync(o => o.OrganizationName == orgName);

            if (existingOrg != null) return (existingOrg, false);

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
        /// Recursively parses a single level of the organizational hierarchy while capturing business operations.
        /// </summary>
        /// <param name="currentEl">Current element to examine for child organizations.</param>
        /// <param name="context">Parsing context.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="parentOrgId">Parent organization ID.</param>
        /// <param name="relationshipPrefix">Prefix for relationship type (e.g., "Labeler", "Registrant").</param>
        /// <param name="currentLevel">Current hierarchy level (1-4).</param>
        /// <returns>A tuple containing counts of organizations and business operations created.</returns>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task<(int OrganizationsCreated, int BusinessOperationsCreated)> parseHierarchyLevelWithBusinessOperationsAsync(
            XElement currentEl, SplParseContext context, int documentId,
            int parentOrgId, string relationshipPrefix, int currentLevel)
        {
            #region implementation
            int orgCount = 0;
            int bizOpCount = 0;

            // Check for direct assignedEntity children (facilities/establishments)
            var childEntities = currentEl.SplElements(sc.E.AssignedEntity).ToList();

            if (childEntities.Any())
            {
                foreach (var childEntityEl in childEntities)
                {
                    var result = await processChildEntityWithBusinessOperationsAsync(
                        childEntityEl, context, documentId, parentOrgId,
                        relationshipPrefix, currentLevel);

                    orgCount += result.OrganizationsCreated;
                    bizOpCount += result.BusinessOperationsCreated;
                }
            }
            else
            {
                // No direct children - this might be a terminal level
                context?.Logger?.LogInformation($"Terminal level reached at level {currentLevel} for organization {parentOrgId}");
            }

            return (orgCount, bizOpCount);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single child entity while capturing associated business operations.
        /// </summary>
        /// <param name="entityEl">The entity element to process.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="documentId">The document ID.</param>
        /// <param name="parentOrgId">The parent organization ID.</param>
        /// <param name="relationshipPrefix">The relationship prefix.</param>
        /// <param name="currentLevel">The current hierarchy level.</param>
        /// <returns>A tuple containing counts of organizations and business operations created.</returns>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task<(int OrganizationsCreated, int BusinessOperationsCreated)> processChildEntityWithBusinessOperationsAsync(
            XElement entityEl, SplParseContext context, int documentId,
            int parentOrgId, string relationshipPrefix, int currentLevel)
        {
            #region implementation
            int orgCount = 0;
            int bizOpCount = 0;

            if (entityEl == null || context?.ServiceProvider == null) return (orgCount, bizOpCount);

            // Try to get organization from this entity
            var (childOrg, childOrgEl) = await getOrgFromEntityAsync(entityEl, context);

            if (childOrg?.OrganizationID == null)
            {
                // No organization found - check if this entity has assignedOrganization with nested structure
                var assignedOrgEl = entityEl.GetSplElement(sc.E.AssignedOrganization);
                if (assignedOrgEl != null)
                {
                    // This is an intermediate level - recurse deeper
                    var result = await parseHierarchyLevelWithBusinessOperationsAsync(
                        assignedOrgEl, context, documentId, parentOrgId,
                        relationshipPrefix, currentLevel + 1);
                    return (result.OrganizationsCreated, result.BusinessOperationsCreated);
                }
                return (orgCount, bizOpCount);
            }

            // We have a valid organization - create relationship
            var relationshipType = determineRelationshipType(relationshipPrefix, currentLevel);
            var relationship = await saveOrGetDocumentRelationshipAsync(
                context.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                documentId, parentOrgId, childOrg.OrganizationID,
                relationshipType, currentLevel);

            orgCount++;

            // Process organization attributes
            if (childOrgEl != null)
                await processOrganizationAttributesAsync(childOrgEl, childOrg.OrganizationID.Value, context);

            // --- ENHANCED: PROCESS BUSINESS OPERATIONS FROM PERFORMANCE ELEMENTS ---
            if (entityEl.SplElements(sc.E.Performance).Any())
            {
                // Set document relationship context for business operation processing
                var oldDocRel = context.CurrentDocumentRelationship;
                context.CurrentDocumentRelationship = relationship;

                try
                {
                    // Process both facility-product links and business operations
                    var facilityLinksCreated = await parseAndSaveFacilityProductLinksAsync(
                        entityEl, relationship.DocumentRelationshipID, context);

                    // NEW: Process business operations using the BusinessOperationParser methods
                    var businessOpsCreated = await parseBusinessOperationsFromPerformanceElementsAsync(
                        entityEl, context, relationship.DocumentRelationshipID, childOrg.OrganizationID.Value);

                    bizOpCount += businessOpsCreated;

                    context.Logger?.LogInformation(
                        "Processed performance elements for organization {OrgId}: {FacilityLinks} facility links, {BusinessOps} business operations",
                        childOrg.OrganizationID, facilityLinksCreated, businessOpsCreated);
                }
                finally
                {
                    // Restore previous document relationship context
                    context.CurrentDocumentRelationship = oldDocRel;
                }
            }

            // Check for further hierarchy levels
            var assignedOrgElement = entityEl.GetSplElement(sc.E.AssignedOrganization);
            if (assignedOrgElement != null)
            {
                // This organization has children - recurse deeper
                var result = await parseHierarchyLevelWithBusinessOperationsAsync(
                    assignedOrgElement, context, documentId, childOrg.OrganizationID.Value,
                    getNextRelationshipPrefix(relationshipPrefix, currentLevel), currentLevel + 1);

                orgCount += result.OrganizationsCreated;
                bizOpCount += result.BusinessOperationsCreated;
            }

            return (orgCount, bizOpCount);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses business operations from performance elements within the author context.
        /// Leverages BusinessOperationParser methods to maintain DRY principles.
        /// </summary>
        /// <param name="parentEl">The parent element containing performance elements.</param>
        /// <param name="context">The parsing context with document relationship set.</param>
        /// <param name="documentRelationshipId">The document relationship ID for linking operations.</param>
        /// <param name="performingOrganizationId">The organization performing the action</param>
        /// <returns>The count of business operations created.</returns>
        /// <remarks>
        /// This method addresses the core issue by extracting business operations during
        /// author processing using the same logic as BusinessOperationParser. This ensures
        /// business operations are captured regardless of parser execution order.
        /// </remarks>
        /// <seealso cref="BusinessOperationParser"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseBusinessOperationsFromPerformanceElementsAsync(
            XElement parentEl, SplParseContext context, int? documentRelationshipId, int? performingOrganizationId)
        {
            #region implementation
            int createdCount = 0;

            if (context?.ServiceProvider == null || context.Logger == null || !documentRelationshipId.HasValue)
                return createdCount;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Process each performance element
            foreach (var actDefEl in parentEl.SplElements(sc.E.Performance, sc.E.ActDefinition))
            {
                // Extract business operation details from the actDefinition
                var bizOp = await parseAndSaveBusinessOperationAsync(
                    dbContext, documentRelationshipId, performingOrganizationId, actDefEl, context);

                if (bizOp?.BusinessOperationID != null)
                {
                    createdCount++;

                    context.Logger?.LogInformation(
                        "Created/found BusinessOperation {OperationCode} ({DisplayName}) for DocumentRelationship {DocRelId}",
                        bizOp.OperationCode, bizOp.OperationDisplayName, documentRelationshipId);

                    // Process any business operation qualifiers (approvals, licenses, etc.)
                    await processBusinessOperationQualifiersAsync(actDefEl, bizOp, dbContext, context);
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves a business operation from an act definition element.
        /// Uses similar logic to BusinessOperationParser to maintain consistency.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="documentRelationshipId">The document relationship ID to associate with the operation.</param>
        /// <param name="performingOrganizationId">The organization performing the action</param>
        /// <param name="actDefEl">The act definition XML element to parse.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>The existing or newly created BusinessOperation entity.</returns>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task<BusinessOperation?> parseAndSaveBusinessOperationAsync(
            ApplicationDbContext dbContext, int? documentRelationshipId, int? performingOrganizationId, XElement actDefEl, SplParseContext context)
        {
            #region implementation
            // Extract business operation code details
            var opCodeEl = actDefEl.GetSplElement(sc.E.Code);
            string? opCode = opCodeEl?.GetAttrVal(sc.A.CodeValue);
            string? opCodeSystem = opCodeEl?.GetAttrVal(sc.A.CodeSystem);
            string? opDisplayName = opCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(opCode) || string.IsNullOrWhiteSpace(opCodeSystem))
            {
                context.Logger?.LogWarning("Business operation missing required code or code system, skipping.");
                return null;
            }

            // Check for existing business operation
            var existing = await dbContext.Set<BusinessOperation>().FirstOrDefaultAsync(op =>
                op.DocumentRelationshipID == documentRelationshipId &&
                op.PerformingOrganizationID == performingOrganizationId &&
                op.OperationCode == opCode &&
                op.OperationCodeSystem == opCodeSystem);

            if (existing != null)
                return existing;

            // Create new business operation
            var newOp = new BusinessOperation
            {
                DocumentRelationshipID = documentRelationshipId,
                PerformingOrganizationID = performingOrganizationId,
                OperationCode = opCode,
                OperationCodeSystem = opCodeSystem,
                OperationDisplayName = opDisplayName
            };

            dbContext.Set<BusinessOperation>().Add(newOp);
            await dbContext.SaveChangesAsync();

            return newOp;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes business operation qualifiers (approvals, licenses) from an act definition element.
        /// </summary>
        /// <param name="actDefEl">The act definition element containing qualifiers.</param>
        /// <param name="businessOperation">The business operation to associate qualifiers with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="Label"/>
        private async Task processBusinessOperationQualifiersAsync(
            XElement actDefEl, BusinessOperation businessOperation,
            ApplicationDbContext dbContext, SplParseContext context)
        {
            #region implementation
            // Process approval elements that contain qualifiers
            foreach (var approvalEl in actDefEl.SplElements(sc.E.SubjectOf, sc.E.Approval))
            {
                var qualifierCodeEl = approvalEl.GetSplElement(sc.E.Code);
                if (qualifierCodeEl == null) continue;

                string? qualifierCode = qualifierCodeEl.GetAttrVal(sc.A.CodeValue);
                string? qualifierCodeSystem = qualifierCodeEl.GetAttrVal(sc.A.CodeSystem);
                string? qualifierDisplayName = qualifierCodeEl.GetAttrVal(sc.A.DisplayName);

                if (string.IsNullOrWhiteSpace(qualifierCode) || businessOperation.BusinessOperationID == null)
                    continue;

                // Create or find business operation qualifier
                await getOrSaveBusinessOperationQualifierAsync(
                    dbContext, businessOperation.BusinessOperationID,
                    qualifierCode, qualifierCodeSystem, qualifierDisplayName);

                context.Logger?.LogInformation(
                    "Processed qualifier {QualifierCode} ({DisplayName}) for BusinessOperation {BusinessOpId}",
                    qualifierCode, qualifierDisplayName, businessOperation.BusinessOperationID);
            }
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
                q.QualifierCodeSystem == qualifierCodeSystem);

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
        /// Processes organization attributes like identifiers, telecoms, etc.
        /// </summary>
        /// <param name="orgEl">The organization element to process.</param>
        /// <param name="organizationId">The organization ID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="Label"/>
        private async Task processOrganizationAttributesAsync(
            XElement orgEl, int organizationId, SplParseContext context)
        {
            #region implementation
            if (orgEl == null) return;

            // Process identifiers
            var identifiers = await getOrCreateOrganizationIdentifierAsync(
                orgEl, organizationId, context);

            // Process telecoms
            var telecoms = await parseAndSaveOrganizationTelecomsAsync(
                orgEl, organizationId, context);

            // Process named entities
            var namedEntities = await getOrCreateNamedEntitiesAsync(
                orgEl, organizationId, context);

            context.Logger?.LogInformation(
                "Processed organization {OrgId}: {IdentifierCount} identifiers, {TelecomCount} telecoms, {EntityCount} named entities",
                organizationId, identifiers?.Count ?? 0, telecoms, namedEntities?.Count ?? 0);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the relationship type based on the current context and level.
        /// </summary>
        /// <param name="prefix">The relationship prefix.</param>
        /// <param name="level">The hierarchy level.</param>
        /// <returns>The relationship type string.</returns>
        /// <seealso cref="Label"/>
        private string determineRelationshipType(string prefix, int level)
        {
            #region implementation
            return level switch
            {
                1 => $"{prefix}ToRegistrant",
                2 => "RegistrantToEstablishment",
                3 => "EstablishmentToFacility",
                4 => "FacilityToSubFacility",
                _ => $"Level{level}Relationship"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the next relationship prefix for deeper hierarchy levels.
        /// </summary>
        /// <param name="currentPrefix">The current relationship prefix.</param>
        /// <param name="level">The current level.</param>
        /// <returns>The next relationship prefix.</returns>
        /// <seealso cref="Label"/>
        private string getNextRelationshipPrefix(string currentPrefix, int level)
        {
            #region implementation
            return level switch
            {
                1 => "Registrant",
                2 => "Establishment",
                3 => "Facility",
                _ => $"Level{level + 1}"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates NamedEntity records for all [asNamedEntity] elements under orgElement.
        /// Handles DBA (Doing Business As) names per Section 2.1.9, 18.1.3, and 18.1.4.
        /// </summary>
        /// <param name="orgElement">The XElement representing [assignedOrganization] or similar.</param>
        /// <param name="organizationId">The parent OrganizationID.</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>List of NamedEntity (both created and found).</returns>
        /// <seealso cref="NamedEntity"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<NamedEntity>> getOrCreateNamedEntitiesAsync(
            XElement orgElement,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            var entities = new List<NamedEntity>();

            // Validate required input parameters
            if (orgElement == null || organizationId <= 0)
                return entities;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return entities;

            // Get database context and repository for named entity operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<NamedEntity>();
            var dbSet = dbContext.Set<NamedEntity>();

            // Find all <asNamedEntity> elements (direct children of org)
            foreach (var asNamedEntityEl in orgElement.SplElements(sc.E.AsNamedEntity))
            {
                // <code> sub element (required for DBA)
                // Extract entity type code information from the code element
                var codeEl = asNamedEntityEl.SplElement(sc.E.Code);
                var entityTypeCode = codeEl?.Attribute(sc.A.CodeValue)?.Value?.Trim();
                var entityTypeCodeSystem = codeEl?.Attribute(sc.A.CodeSystem)?.Value?.Trim();
                var entityTypeDisplayName = codeEl?.Attribute(sc.A.DisplayName)?.Value?.Trim();

                // Validation: Ensure we have valid entity type code and code system
                // Process all valid named entity types, not just DBA
                if (string.IsNullOrWhiteSpace(entityTypeCode) || string.IsNullOrWhiteSpace(entityTypeCodeSystem))
                    continue; // Skip entries without proper coding

                // Validate that the code system is from the expected healthcare coding system
                if (entityTypeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                    continue; // Only process entities from the standard healthcare code system

                // <name> is required for DBA
                // Extract the entity name which is mandatory for DBA entries
                var entityName = asNamedEntityEl.SplElement(sc.E.Name)?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(entityName))
                    continue;

                // Optional: <suffix> (used in WDD/3PL only)
                // Extract optional suffix used in specific workflow scenarios
                var entitySuffix = asNamedEntityEl.SplElement(sc.E.Suffix)?.Value?.Trim();

                // Deduplicate on OrganizationID + EntityName + EntityTypeCode + Suffix
                // Search for existing named entity with matching organization and attributes
                var existing = await dbSet.FirstOrDefaultAsync(e =>
                    e.OrganizationID == organizationId &&
                    e.EntityName == entityName &&
                    e.EntityTypeCode == entityTypeCode &&
                    e.EntitySuffix == entitySuffix);

                // Return existing entity if found to avoid duplicates
                if (existing != null)
                {
                    entities.Add(existing);
                    continue;
                }

                // Create new NamedEntity
                // Build new named entity with all extracted attributes
                var newEntity = new NamedEntity
                {
                    OrganizationID = organizationId,
                    EntityTypeCode = entityTypeCode,
                    EntityTypeCodeSystem = entityTypeCodeSystem,
                    EntityTypeDisplayName = entityTypeDisplayName,
                    EntityName = entityName,
                    EntitySuffix = entitySuffix
                };

                // Persist new named entity to database
                await repo.CreateAsync(newEntity);
                entities.Add(newEntity);
            }

            return entities;
            #endregion
        }


        /**************************************************************/
        /// <summary>
        /// Finds or creates OrganizationIdentifier(s) for all [id] elements under the orgElement.
        /// Handles DUNS, FEI, Labeler Code, etc. per Section 2.1.4/2.1.5.
        /// </summary>
        /// <param name="orgElement">The XElement representing [representedOrganization] or [assignedOrganization].</param>
        /// <param name="organizationId">The parent OrganizationID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>List of OrganizationIdentifier (both created and found).</returns>
        /// <remarks>
        /// Processes all direct [id] child elements to extract organization identifiers including:
        /// - DUNS numbers (validated as 9-digit format)
        /// - FEI (FDA Establishment Identifier) numbers
        /// - NDC Labeler Codes
        /// - Other identifier types based on OID root values
        /// 
        /// Implements deduplication logic to prevent duplicate identifier records for the same
        /// organization. Validates DUNS numbers against the required 9-digit format per SPL standards.
        /// Maps identifier types based on standard healthcare OID roots.
        /// </remarks>
        /// <seealso cref="OrganizationIdentifier"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<OrganizationIdentifier>> getOrCreateOrganizationIdentifierAsync(
            XElement orgElement,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            var identifiers = new List<OrganizationIdentifier>();

            // Validate input parameters before proceeding
            if (orgElement == null || organizationId <= 0)
                return identifiers;

            // Validate required context before proceeding
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return identifiers;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<OrganizationIdentifier>();
            var dbSet = dbContext.Set<OrganizationIdentifier>();

            // Find all <id> child elements (direct only)
            foreach (var idEl in orgElement.SplElements(sc.E.Id))
            {
                // Extract identifier value and system root from XML attributes
                var extension = idEl.Attribute(sc.A.Extension)?.Value?.Trim();
                var root = idEl.Attribute(sc.A.Root)?.Value?.Trim();

                // Spec: must have a DUNS root unless cosmetic/animal/other types
                if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(extension))
                    continue;

                // --- Infer type by OID ---
                // (Extend as needed: add other roots for FEI, NDC, etc.)
                string identifierType = root switch
                {
                    "1.3.6.1.4.1.519.1" => "DUNS",                   // Data Universal Numbering System
                    "2.16.840.1.113883.4.82" => "FEI",               // FDA Establishment Identifier
                    "2.16.840.1.113883.6.69" => "NDC Labeler Code",  // National Drug Code Labeler
                                                                     // Add more OIDs as needed here:
                    _ => "Other"
                };

                // --- DUNS number validation: must be 9 digits if type is DUNS ---
                if (identifierType == "DUNS" && !System.Text.RegularExpressions.Regex.IsMatch(extension, @"^\d{9}$"))
                {
                    context.Logger?.LogWarning("DUNS identifier '{Value}' is not 9 digits.", extension);
                    continue;
                }

                // --- Deduplication: check if identifier already exists ---
                var existing = await dbSet.FirstOrDefaultAsync(oi =>
                    oi.OrganizationID == organizationId &&
                    oi.IdentifierValue == extension &&
                    oi.IdentifierSystemOID == root);

                if (existing != null)
                {
                    // Add existing identifier to result list
                    identifiers.Add(existing);
                    continue;
                }

                // --- Create new identifier ---
                var newIdentifier = new OrganizationIdentifier
                {
                    OrganizationID = organizationId,
                    IdentifierValue = extension,
                    IdentifierSystemOID = root,
                    IdentifierType = identifierType
                };

                // Save the new identifier to database
                await repo.CreateAsync(newIdentifier);
                identifiers.Add(newIdentifier);

                context.Logger?.LogInformation("OrganizationIdentifier created: OrganizationID={OrganizationID}, Type={Type}, Value={Value}",
                    organizationId, identifierType, extension);
            }

            return identifiers;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [telecom] elements under the given parent (usually [contactParty]), creates Telecom records,
        /// and links them via ContactPartyTelecom. Returns count of new telecoms created.
        /// </summary>
        /// <param name="parentEl">XElement containing [telecom] elements.</param>
        /// <param name="contactPartyId">The owning ContactPartyID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>Count of new Telecoms created and linked.</returns>
        /// <seealso cref="Telecom"/>
        /// <seealso cref="ContactPartyTelecom"/>
        /// <seealso cref="ContactParty"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndSaveContactPartyTelecomsAsync(
            XElement parentEl,
            int contactPartyId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Find all direct <telecom> children elements
            var telecomEls = parentEl.SplElements(sc.E.Telecom).ToList();
            if (telecomEls == null || !telecomEls.Any())
                return 0;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return 0;

            // Process each telecom element individually
            foreach (var telecomEl in telecomEls)
            {
                // Extract telecom value from the 'value' attribute
                var value = telecomEl.Attribute("value")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Determine telecom type: "tel", "mailto", "fax"
                string telecomType = null;
                if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) telecomType = "tel";
                else if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) telecomType = "mailto";
                else if (value.StartsWith("fax:", StringComparison.OrdinalIgnoreCase)) telecomType = "fax";
                else continue; // skip unsupported telecom types

                // --- Validation (basic, expand as needed for spec) ---
                // Validate phone and fax number formats
                if (telecomType == "tel" || telecomType == "fax")
                {
                    // US/international number validation - extract number after protocol prefix
                    var number = value.Substring(value.IndexOf(':') + 1);
                    if (!number.StartsWith("+") || number.Any(char.IsLetter) || number.Contains(" "))
                    {
                        context.Logger?.LogWarning("Invalid {TelecomType} format: {Value}", telecomType, value);
                    }
                    // US pattern check (if needed): +1-aaa-bbb-cccc
                }
                // Validate email address format
                else if (telecomType == "mailto")
                {
                    // Basic email validation - check format after 'mailto:' prefix
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value.Substring(7), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        context.Logger?.LogWarning("Invalid email address: {Value}", value);
                    }
                }

                // --- Deduplication: By TelecomValue (case-insensitive) ---
                // Get database context and repositories for telecom operations
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var telecomRepo = context.GetRepository<Telecom>();
                var cptRepo = context.GetRepository<ContactPartyTelecom>();
                var telecomDbSet = dbContext.Set<Telecom>();
                var cptDbSet = dbContext.Set<ContactPartyTelecom>();

                // Search for existing telecom with same value (case-insensitive)
                var existingTelecom = await telecomDbSet.FirstOrDefaultAsync(t =>
                    t != null && t.TelecomValue != null && t.TelecomValue.ToLower() == value.ToLower());

                // Create new telecom if none exists with this value
                if (existingTelecom == null)
                {
                    existingTelecom = new Telecom
                    {
                        TelecomType = telecomType,
                        TelecomValue = value
                    };
                    await telecomRepo.CreateAsync(existingTelecom);
                    createdCount++;
                }

                // Link to ContactParty via ContactPartyTelecom (deduplication)
                // Check if link already exists between contact party and telecom
                var existingLink = await cptDbSet.FirstOrDefaultAsync(
                    link => link.ContactPartyID == contactPartyId && link.TelecomID == existingTelecom.TelecomID);

                // Create new link if none exists
                if (existingLink == null)
                {
                    var link = new ContactPartyTelecom
                    {
                        ContactPartyID = contactPartyId,
                        TelecomID = existingTelecom.TelecomID
                    };
                    await cptRepo.CreateAsync(link);
                }
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [telecom] elements under the given parent (usually [assignedOrganization]),
        /// creates Telecom records, and links them via OrganizationTelecom.
        /// Returns count of new telecoms created and linked.
        /// </summary>
        /// <param name="parentEl">XElement containing [telecom] elements.</param>
        /// <param name="organizationId">Owning OrganizationID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>Count of new Telecoms created and linked.</returns>
        /// <seealso cref="Telecom"/>
        /// <seealso cref="OrganizationTelecom"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndSaveOrganizationTelecomsAsync(
            XElement parentEl,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Find all direct <telecom> children elements
            var telecomEls = parentEl.SplElements(sc.E.Telecom).ToList();
            if (telecomEls == null || !telecomEls.Any())
                return 0;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return 0;

            // Process each telecom element individually
            foreach (var telecomEl in telecomEls)
            {
                // Extract telecom value from the 'value' attribute
                var value = telecomEl.Attribute("value")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Determine telecom type based on protocol prefix
                string telecomType = null;
                if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) telecomType = "tel";
                else if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) telecomType = "mailto";
                else if (value.StartsWith("fax:", StringComparison.OrdinalIgnoreCase)) telecomType = "fax";
                else continue; // skip unsupported telecom types

                // Validation (same as above)
                // Validate phone and fax number formats
                if (telecomType == "tel" || telecomType == "fax")
                {
                    // Extract number portion after protocol prefix
                    var number = value.Substring(value.IndexOf(':') + 1);
                    // Check for valid international format requirements
                    if (!number.StartsWith("+") || number.Any(char.IsLetter) || number.Contains(" "))
                    {
                        context.Logger?.LogWarning("Invalid {TelecomType} format: {Value}", telecomType, value);
                    }
                }
                // Validate email address format
                else if (telecomType == "mailto")
                {
                    // Basic email validation - check format after 'mailto:' prefix
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value.Substring(7), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        context.Logger?.LogWarning("Invalid email address: {Value}", value);
                    }
                }

                // Deduplication
                // Get database context and repositories for telecom operations
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var telecomRepo = context.GetRepository<Telecom>();
                var orgTelecomRepo = context.GetRepository<OrganizationTelecom>();
                var telecomDbSet = dbContext.Set<Telecom>();
                var orgTelecomDbSet = dbContext.Set<OrganizationTelecom>();

                // Search for existing telecom with same value (case-insensitive)
                var existingTelecom = await telecomDbSet.FirstOrDefaultAsync(t =>
                     t != null && t.TelecomValue != null && t.TelecomValue.ToLower() == value.ToLower());

                // Create new telecom if none exists with this value
                if (existingTelecom == null)
                {
                    existingTelecom = new Telecom
                    {
                        TelecomType = telecomType,
                        TelecomValue = value
                    };
                    await telecomRepo.CreateAsync(existingTelecom);
                    createdCount++;
                }

                // Link to Organization via OrganizationTelecom (deduplication)
                // Check if link already exists between organization and telecom
                var existingLink = await orgTelecomDbSet.FirstOrDefaultAsync(
                    link => link.OrganizationID == organizationId && link.TelecomID == existingTelecom.TelecomID);

                // Create new link if none exists
                if (existingLink == null)
                {
                    var link = new OrganizationTelecom
                    {
                        OrganizationID = organizationId,
                        TelecomID = existingTelecom.TelecomID
                    };
                    await orgTelecomRepo.CreateAsync(link);
                }
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all [contactParty] entities under the specified element,
        /// associating each with the given organization. Returns the count created.
        /// </summary>
        /// <param name="element">Parent XElement to scan for contactParty nodes.</param>
        /// <param name="context">Parsing context (repos, logger, etc).</param>
        /// <param name="organizationId">Owning OrganizationID (required).</param>
        /// <returns>Count of new ContactParty entities created.</returns>
        /// <seealso cref="ContactParty"/>
        /// <seealso cref="Label"/>
        private static async Task<(int createdct, int telecomCt)> parseAndSaveContactPartiesAsync(
            XElement element,
            SplParseContext context,
            int organizationId)
        {
            #region implementation
            int createdCt = 0;
            int telecomCt = 0;

            // Find all <contactParty> nodes (case-insensitive, supports namespace)
            var contactPartyEls = element.SplFindElements(sc.E.ContactParty);
            if (contactPartyEls != null)
            {
                foreach (var contactPartyEl in contactPartyEls)
                {
                    var (contactParty, partyCreated) = await getOrCreateContactPartyAsync(contactPartyEl, organizationId, context);

                    if (contactParty?.ContactPartyID == null)
                    {
                        context.Logger?.LogWarning("Failed to create ContactParty for OrganizationID {OrgId}.", organizationId);
                        context.Logger?.LogError($"Failed to create contact party for organization {organizationId}.");
                    }
                    else if (partyCreated)
                    {
                        context.Logger?.LogInformation("Created ContactParty for OrganizationID {OrgId} with AddressID {AddrId} and ContactPersonID {PersonId}",
                            organizationId, contactParty.AddressID, contactParty.ContactPersonID);
                        createdCt++;

                        // --- PARSE TELECOMS ---
                        var telecomsCreated = await parseAndSaveContactPartyTelecomsAsync(contactPartyEl, contactParty.ContactPartyID.Value, context);
                        telecomCt += telecomsCreated;

                    }
                    else
                    {
                        context.Logger?.LogInformation("Found existing ContactParty for OrganizationID {OrgId}", organizationId);
                    }
                }
            }
            return (createdCt, telecomCt);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to get or create an Organization from an [assignedEntity] or similar element.
        /// </summary>
        /// <param name="entityEl">The entity element (e.g., assignedEntity) containing the organization info.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and its corresponding XElement.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="getOrCreateOrganizationAsync"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<(Organization? org, XElement? orgEl)> getOrgFromEntityAsync(XElement entityEl, SplParseContext context)
        {
            #region implementation
            // Find either AssignedOrganization or RepresentedOrganization element
            var orgEl = entityEl.SplElement(sc.E.AssignedOrganization)
                        ?? entityEl.SplElement(sc.E.RepresentedOrganization);

            if (orgEl == null)
                return (null, null);

            // Create or retrieve the organization entity from the XML element
            var (org, _) = await getOrCreateOrganizationAsync(orgEl, context);
            return (org, orgEl);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all FacilityProductLink entities from [performance][actDefinition][product] nodes.
        /// </summary>
        /// <param name="parentEl">The parent XElement (e.g., [assignedOrganization] for a facility) to scan for product links.</param>
        /// <param name="documentRelationshipId">The ID of the DocumentRelationship linking the document to this facility.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The count of FacilityProductLink records created.</returns>
        /// <remarks>
        /// This method orchestrates the linking of a facility to its cosmetic products as defined in SPL IG Section 36.1.6.
        /// It iterates through each product reference, resolves the product by its Cosmetic Listing Number (CLN) or name,
        /// and then creates the link record in the database.
        /// </remarks>
        /// <seealso cref="FacilityProductLink"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="resolveProductFromLinkElementAsync"/>
        /// <seealso cref="getOrSaveFacilityProductLinkAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveFacilityProductLinksAsync(
            XElement parentEl,
            int? documentRelationshipId,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Validate required dependencies for processing facility product links
            if (context?.ServiceProvider == null || context.Logger == null || !documentRelationshipId.HasValue)
            {
                return count; // Exit if context or parent relationship ID is invalid
            }

            // Get database context for entity operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get the elements defining product links i.e. <performance><actDefinition> elements
            var productLinkEls = parentEl.SplElements(sc.E.Performance, sc.E.ActDefinition);

            // Operate on all <performance><actDefinition> elements that define product links
            foreach (var actDefEl in productLinkEls)
            {
                // Each actDefinition should reference one product
                var productEl = actDefEl.GetSplElement(sc.E.Product);
                if (productEl == null)
                {
                    // Log warning and skip if product reference is missing
                    context.Logger.LogWarning("Found <actDefinition> for a facility without a <product> reference; skipping.");
                    continue;
                }

                // 1. Resolve the product by CLN or Name using the product element
                var (productId, productIdentifierId, productName) = await resolveProductFromLinkElementAsync(productEl, dbContext, context.Logger);

                // If we couldn't find the product, we can't create a link
                if (!productId.HasValue && string.IsNullOrWhiteSpace(productName))
                {
                    // Log warning when product cannot be resolved from the XML element
                    context.Logger.LogWarning("Could not resolve product for facility link from element: {ProductElementXml}", productEl.ToString());
                    continue;
                }

                // 2. Get or create the FacilityProductLink with resolved product information
                await getOrSaveFacilityProductLinkAsync(
                    dbContext,
                    documentRelationshipId,
                    productId,
                    productIdentifierId,
                    productName
                );

                // Increment count for each successfully created facility-product link
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a product reference from a facility link XML element by querying the database.
        /// </summary>
        /// <param name="productEl">The [product] XElement containing the reference.</param>
        /// <param name="dbContext">The database context for querying.</param>
        /// <param name="logger">Logger for reporting warnings if a product cannot be found.</param>
        /// <returns>A tuple containing the resolved ProductID, ProductIdentifierID (if by CLN), and ProductName (if by name).</returns>
        /// <remarks>
        /// Implements the logic from SPL IG Section 36.1.6, first attempting to find the product by its
        /// Cosmetic Listing Number (CLN), and falling back to the product name if the CLN is not provided.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="Label"/>
        private async Task<(int? ProductId, int? ProductIdentifierId, string? ProductName)> resolveProductFromLinkElementAsync(
            XElement productEl,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            // Navigate to the manufactured material kind element containing product details
            var materialKindEl = productEl.SplElement(sc.E.ManufacturedProduct, sc.E.ManufacturedMaterialKind);
            if (materialKindEl == null) return (null, null, null);

            // Try to resolve by Cosmetic Listing Number (CLN) first
            var codeEl = materialKindEl.GetSplElement(sc.E.Code);
            var clnValue = codeEl?.GetAttrVal(sc.A.CodeValue);

            if (!string.IsNullOrWhiteSpace(clnValue))
            {
                // Find the ProductIdentifier record for this CLN using the standard CLN OID
                var productIdentifier = await dbContext.Set<ProductIdentifier>()
                    .FirstOrDefaultAsync(pi => pi.IdentifierValue == clnValue);

                if (productIdentifier != null)
                {
                    // Return product information when CLN match is found
                    return (productIdentifier.ProductID, productIdentifier.ProductIdentifierID, null);
                }
                else
                {
                    // Product doesn't exist yet - this is normal during author parsing
                    logger.LogInformation("CLN '{cln}' not found in database yet - will be resolved later when products are created.", clnValue);
                    // Return the CLN as a name reference so the link can be created with deferred resolution
                    return (null, null, clnValue);
                }
            }

            // Fallback to resolving by product name when CLN is not available
            var nameEl = materialKindEl.GetSplElement(sc.E.Name);
            var productName = nameEl?.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(productName))
            {
                // Search for product by exact name match
                var product = await dbContext.Set<Product>()
                    .FirstOrDefaultAsync(p => p.ProductName == productName);

                if (product != null)
                {
                    // Return product information when name match is found
                    return (product.ProductID, null, productName);
                }
                else
                {
                    // Product doesn't exist yet - return name for deferred resolution
                    logger.LogInformation("Product name '{productName}' not found in database yet - will be resolved later.", productName);
                    return (null, null, productName);
                }
            }

            // Return null values when no valid reference is found
            return (null, null, null);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing FacilityProductLink or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="documentRelationshipId">The ID of the parent DocumentRelationship (linking to the facility).</param>
        /// <param name="productId">The ID of the product being linked (if resolved).</param>
        /// <param name="productIdentifierId">The ID of the product identifier (if linked by CLN).</param>
        /// <param name="productName">The name of the product (if linked by name).</param>
        /// <returns>The existing or newly created FacilityProductLink entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate facility-to-product links.
        /// Uniqueness is determined by the combination of the document relationship and the specific product reference
        /// (either by its internal ID or by its name).
        /// </remarks>
        /// <seealso cref="FacilityProductLink"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private async Task<FacilityProductLink> getOrSaveFacilityProductLinkAsync(
            ApplicationDbContext dbContext,
            int? documentRelationshipId,
            int? productId,
            int? productIdentifierId,
            string? productName)
        {
            #region implementation
            // Search for an existing link matching the relationship and product reference.
            var existing = await dbContext.Set<FacilityProductLink>().FirstOrDefaultAsync(fpl =>
                fpl.DocumentRelationshipID == documentRelationshipId &&
                (
                    (productId.HasValue && fpl.ProductID == productId) ||
                    (!string.IsNullOrWhiteSpace(productName) && fpl.ProductName == productName)
                ));

            // Return existing link if found to avoid creating duplicates
            if (existing != null)
            {
                return existing;
            }

            // Create a new facility product link entity
            var newLink = new FacilityProductLink
            {
                DocumentRelationshipID = documentRelationshipId,
                ProductID = productId,
                ProductIdentifierID = productIdentifierId,
                ProductName = productName, // This might be a CLN code if not resolved yet
                IsResolved = productId.HasValue // Track whether this link is resolved
            };

            // Save the new link to the database and persist changes immediately
            dbContext.Set<FacilityProductLink>().Add(newLink);
            await dbContext.SaveChangesAsync();

            // Return the newly created facility-product link
            return newLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing Address by all normalized fields or creates a new one.
        /// Validates per Section 2.1.6.
        /// </summary>
        /// <param name="addrEl">XElement for [addr] (may be null).</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>(Address entity, wasCreated)</returns>
        /// <seealso cref="Address"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(Address? Address, bool Created)> getOrCreateAddressAsync(XElement? addrEl, SplParseContext context)
        {
            #region implementation
            // Return early if no address element provided
            if (addrEl == null) return (null, false);

            // Validate required context dependencies
            if (context == null || context.ServiceProvider == null || context.Logger == null)
                return (null, false);

            // Extract and normalize address field values from XML elements
            var streetLines = addrEl.SplElements(sc.E.StreetAddressLine).Select(x => x.Value?.Trim()).ToList();
            var city = addrEl.SplElement(sc.E.City)?.Value?.Trim();
            var state = addrEl.SplElement(sc.E.State)?.Value?.Trim();
            var postalCode = addrEl.SplElement(sc.E.PostalCode)?.Value?.Trim();

            // Extract country information from nested country element
            var countryEl = addrEl.SplElement(sc.E.Country);
            var countryCode = countryEl?.Attribute(sc.A.CodeValue)?.Value?.Trim();
            var countryName = countryEl?.Value?.Trim();
            var countryCodeSystem = countryEl?.Attribute(sc.A.CodeSystem)?.Value?.Trim();

            // --- Country code normalization/validation ---
            // If no country code but country name exists, check if name is 3-char code
            if (string.IsNullOrWhiteSpace(countryCode) && !string.IsNullOrWhiteSpace(countryName))
                countryCode = countryName.Length == 3 ? countryName.ToUpper() : null;

            // Enforce ISO 3166-1 alpha-3 for countryCode if codeSystem is present
            if (!string.IsNullOrWhiteSpace(countryCode) && countryCodeSystem == "1.0.3166.1.2.3" && countryCode.Length != 3)
            {
                context.Logger.LogWarning("Country code {CountryCode} is not ISO 3166-1 alpha-3.", countryCode);
            }

            // --- USA rules ---
            // Apply specific validation rules for USA addresses
            if (countryCode == "USA")
            {
                // Must have state and 5 or 5+4 digit zip
                if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(postalCode))
                {
                    context.Logger.LogWarning("USA address must have state and postalCode.");
                }
                // Validate ZIP code format (5 digits or ZIP+4)
                if (!System.Text.RegularExpressions.Regex.IsMatch(postalCode ?? "", @"^\d{5}(-\d{4})?$"))
                {
                    context.Logger.LogWarning("USA postalCode must be 5 digits or ZIP+4.");
                }
            }
            // For non-USA, just require postal code (per spec)
            else if (string.IsNullOrWhiteSpace(postalCode))
            {
                context.Logger.LogWarning("Non-USA address missing postalCode.");
            }

            // --- Deduplication: full match on all address fields ---
            // Get database context and repository for address lookups
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var addrRepo = context.GetRepository<Address>();
            var dbSet = dbContext.Set<Address>();

            // Search for existing address with matching normalized fields
            var existing = await dbSet.FirstOrDefaultAsync(a =>
                a.StreetAddressLine1 == (streetLines.Count > 0 ? streetLines[0] : null) &&
                a.StreetAddressLine2 == (streetLines.Count > 1 ? streetLines[1] : null) &&
                a.City == city &&
                a.StateProvince == state &&
                a.PostalCode == postalCode &&
                a.CountryCode == countryCode &&
                a.CountryName == countryName);

            // Return existing address if found
            if (existing != null)
                return (existing, false);

            // Create new address entity with normalized values
            var newAddr = new Address
            {
                StreetAddressLine1 = streetLines.Count > 0 ? streetLines[0] : null,
                StreetAddressLine2 = streetLines.Count > 1 ? streetLines[1] : null,
                City = city,
                StateProvince = state,
                PostalCode = postalCode,
                CountryCode = countryCode,
                CountryName = countryName
            };

            // Persist new address to database
            await addrRepo.CreateAsync(newAddr);
            return (newAddr, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and persists a ContactParty and related entities (Address, ContactPerson) from XML.
        /// Follows Sections 2.1.6 and 2.1.8, validates address per specification, and enforces deduplication.
        /// </summary>
        /// <param name="contactPartyEl">XElement representing [contactParty].</param>
        /// <param name="organizationId">Owning OrganizationID (nullable, but must be provided).</param>
        /// <param name="context">The current parsing context (repo, logger, etc).</param>
        /// <returns>Tuple: (ContactParty entity, wasCreated), or (null, false) if not created.</returns>
        /// <seealso cref="ContactParty"/>
        /// <seealso cref="Address"/>
        /// <seealso cref="ContactPerson"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(ContactParty? ContactParty, bool Created)> getOrCreateContactPartyAsync(
            XElement contactPartyEl,
            int? organizationId,
            SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (contactPartyEl == null || organizationId == null)
                return (null, false);

            // Validate context dependencies
            if (context == null || context.ServiceProvider == null)
                return (null, false);

            // --- ADDRESS ---
            // Extract and process address element if present
            var addrEl = contactPartyEl.SplElement(sc.E.Addr);
            var (address, addrCreated) = await getOrCreateAddressAsync(addrEl, context);

            // --- CONTACT PERSON ---
            // Extract and process contact person element if present
            var contactPersonEl = contactPartyEl.SplElement(sc.E.ContactPerson);
            var (contactPerson, personCreated) = await getOrCreateContactPersonAsync(contactPersonEl, context);

            // --- CONTACT PARTY DEDUPLICATION ---
            // Get database context and repository for contact party operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var partyRepo = context.GetRepository<ContactParty>();
            var dbSet = dbContext.Set<ContactParty>();

            // Check for existing contact party with same organization, address, and person
            if (address != null
                && contactPerson != null
                && address.AddressID > 0
                && contactPerson.ContactPersonID > 0)
            {
                var existingParty = await dbSet.FirstOrDefaultAsync(cp =>
                    cp.OrganizationID == organizationId &&
                    cp.AddressID == address.AddressID &&
                    cp.ContactPersonID == contactPerson.ContactPersonID);

                // Return existing party if found
                if (existingParty != null)
                    return (existingParty, false);
            }

            // Create new contact party entity linking organization, address, and person
            var newParty = new ContactParty
            {
                OrganizationID = organizationId,
                AddressID = address?.AddressID,
                ContactPersonID = contactPerson?.ContactPersonID
            };

            // Persist new contact party to database
            await partyRepo.CreateAsync(newParty);

            // Optionally: Handle telecom/email here if required (store on Organization, separate table, etc.)
            // TODO: Extract and link telecom if needed

            return (newParty, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates a ContactPerson by normalized name. For Section 2.1.8.
        /// </summary>
        /// <param name="contactPersonEl">XElement for [contactPerson] (may be null).</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>(ContactPerson entity, wasCreated)</returns>
        /// <seealso cref="ContactPerson"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(ContactPerson? ContactPerson, bool Created)> getOrCreateContactPersonAsync(XElement? contactPersonEl, SplParseContext context)
        {
            #region implementation
            // Return early if no contact person element provided
            if (contactPersonEl == null) return (null, false);

            // Validate required context dependencies
            if (context == null || context.ServiceProvider == null)
                return (null, false);

            // Extract and normalize contact person name
            var name = contactPersonEl.SplElement(sc.E.Name)?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return (null, false);

            // Get database context and repository for contact person operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var personRepo = context.GetRepository<ContactPerson>();
            var dbSet = dbContext.Set<ContactPerson>();

            // Search for existing contact person with matching name
            var existing = await dbSet.FirstOrDefaultAsync(p => p.ContactPersonName == name);
            if (existing != null)
                return (existing, false);

            // Create new contact person entity with normalized name
            var newPerson = new ContactPerson { ContactPersonName = name };
            await personRepo.CreateAsync(newPerson);

            return (newPerson, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing DocumentRelationship or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="docId">The document ID to associate with the relationship.</param>
        /// <param name="parentOrgId">The parent organization ID in the relationship hierarchy.</param>
        /// <param name="childOrgId">The child organization ID in the relationship hierarchy.</param>
        /// <param name="relationshipType">The type of relationship between the organizations.</param>
        /// <param name="relationshipLevel">The hierarchical level of the relationship.</param>
        /// <returns>The existing or newly created DocumentRelationship entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
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
            // Validate inputs to ensure data integrity
            if (dbContext == null || docId == null || parentOrgId == null)
                throw new ArgumentNullException("A required argument for DocumentRelationship is null.");

            // Try to find an existing relationship with exact parameter match
            var existing = await dbContext.Set<DocumentRelationship>().FirstOrDefaultAsync(dr =>
                dr.DocumentID == docId &&
                dr.ParentOrganizationID == parentOrgId &&
                dr.ChildOrganizationID == childOrgId &&
                dr.RelationshipType == relationshipType);

            // Fallback: was deleted b/c of multiple child orgs
            // that can appear in the author section and need
            // to be captured as separate relationships

            // Create new relationship entity with provided parameters
            var newRel = new DocumentRelationship
            {
                DocumentID = docId,
                ParentOrganizationID = parentOrgId,
                ChildOrganizationID = childOrgId,
                RelationshipType = relationshipType,
                RelationshipLevel = relationshipLevel
            };

            // Save the new relationship to database
            dbContext.Set<DocumentRelationship>().Add(newRel);
            await dbContext.SaveChangesAsync();
            return newRel;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// This normalizes organization data, preventing duplicates.
        /// </summary>
        /// <param name="orgElement">The XElement representing the organization (e.g., representedOrganization).</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and a boolean indicating if it was newly created.</returns>
        /// <example>
        /// <code>
        /// var (org, wasCreated) = await getOrCreateOrganizationAsync(orgElement, context);
        /// if (wasCreated)
        /// {
        ///     Console.WriteLine($"Created new organization: {org.OrganizationName}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements deduplication logic by first checking if an organization
        /// with the same name already exists in the database. If found, it returns the
        /// existing entity. Otherwise, it creates a new organization with data extracted
        /// from the XML element.
        /// </remarks>
        /// <seealso cref="Organization"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions.GetSplElementVal(XElement, string)"/>
        /// <seealso cref="Label"/>
        private static async Task<(Organization? Organization, bool Created)> getOrCreateOrganizationAsync(XElement orgElement, SplParseContext context)
        {
            #region implementation
            // Extract organization name using the helper extension method
            var orgName = orgElement.GetSplElementVal(sc.E.Name)?.Trim();

            if (context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(context), "Parsing context, logger, and provider cannot be null.");
            }

            // Validate that we have a valid organization name
            if (string.IsNullOrWhiteSpace(orgName))
            {
                context.Logger.LogWarning("Organization name is missing in file {FileName}. Cannot create organization.", context.FileNameInZip);
                return (null, false);
            }

            // Get database context and repository for organization operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgRepo = context.GetRepository<Organization>();
            var orgDbSet = dbContext.Set<Organization>();

            // Check if the organization already exists in the database by name
            var existingOrg = await orgDbSet
                .FirstOrDefaultAsync(o => o.OrganizationName == orgName);

            // Return existing organization if found
            if (existingOrg != null)
            {
                return (existingOrg, false); // Return existing organization
            }

            // Create a new organization entity with extracted data
            var newOrganization = new Organization
            {
                OrganizationName = orgName,

                // Extract confidentiality information from XML attributes
                // Check if the confidentiality code value equals "B" (confidential)
                IsConfidential = orgElement
                .GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };

            // Save the new organization to the database (CreateAsync populates the ID)
            await orgRepo.CreateAsync(newOrganization);

            return (newOrganization, true); // Return newly created organization
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a link between a document and an authoring organization if it doesn't already exist.
        /// </summary>
        /// <param name="docId">The ID of the document.</param>
        /// <param name="orgId">The ID of the authoring organization.</param>
        /// <param name="authorType">The type of the author (e.g., "Labeler").</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the DocumentAuthor entity and a boolean indicating if it was newly created.</returns>
        /// <example>
        /// <code>
        /// var (docAuthor, wasCreated) = await getOrCreateDocumentAuthorAsync(123, 456, "Labeler", context);
        /// if (wasCreated)
        /// {
        ///     Console.WriteLine("Created new document author relationship");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements deduplication logic for document-author relationships.
        /// It first checks if a link between the specified document and organization already
        /// exists. If not found, it creates a new DocumentAuthor entity to establish the relationship.
        /// </remarks>
        /// <seealso cref="DocumentAuthor"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(DocumentAuthor? DocumentAuthor, bool Created)> getOrCreateDocumentAuthorAsync(int docId, int orgId, string authorType, SplParseContext context)
        {
            #region implementation

            if (context == null
               || context.Logger == null
               || context.ServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(context), "Parsing context, logger, and provider cannot be null.");
            }

            // Get database context and repository for document author operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var docAuthorRepo = context.GetRepository<DocumentAuthor>();
            var docAuthorDbSet = dbContext.Set<DocumentAuthor>();

            // Check if the document-author link already exists in the database
            var existingLink = await docAuthorDbSet
                .FirstOrDefaultAsync(da =>
                da.DocumentID == docId && da.OrganizationID == orgId);

            // Return existing link if found
            if (existingLink != null)
            {
                return (existingLink, false);
            }

            // Create a new document author relationship entity
            var newDocAuthor = new DocumentAuthor
            {
                DocumentID = docId,
                OrganizationID = orgId,
                AuthorType = authorType
            };

            // Save the new document author link to the database
            await docAuthorRepo.CreateAsync(newDocAuthor);

            return (newDocAuthor, true);
            #endregion
        }
    }
}