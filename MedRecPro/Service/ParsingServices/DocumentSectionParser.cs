using System.Xml.Linq;

using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Helpers;
using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses the main document element of an SPL document, extracting metadata
    /// and creating the primary document entity in the database.
    /// </summary>
    /// <remarks>
    /// This parser handles the root document element which contains essential
    /// metadata such as document GUID, codes, titles, effective times, and version
    /// information. It creates the primary Document entity that serves as the
    /// foundation for all other parsing operations in the SPL processing pipeline.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Document"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="SplParseResult"/>
    public class DocumentSectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, using the constant for Document element.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.SplConstants"/>
        public string SectionName => "document";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses the document section of an SPL file, extracting core document metadata
        /// and creating the primary Document entity in the database.
        /// </summary>
        /// <param name="element">The XElement representing the document section to parse.</param>
        /// <param name="context">The current parsing context that will be updated with the created document.</param>
        /// <param name="reportProgress">Progress delegate reporter</param>
        /// <param name="isParentCallingForAllSubElements"></param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new DocumentSectionParser();
        /// var result = await parser.ParseAsync(documentElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Created document with ID: {parseContext.Document.DocumentID}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Extracts document metadata from the XML element
        /// 2. Creates a new Document entity with the extracted data
        /// 3. Saves the document to the database via repository
        /// 4. Updates the parsing context with the created document
        /// 5. Validates that the document ID was properly assigned
        /// 
        /// The created document becomes the foundation for all subsequent parsing operations.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Document"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();
            int docID = 0;

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            try
            {

                reportProgress?.Invoke($"Starting Document XML Elements {context.FileNameInZip}");

                // Extract document metadata from the XML element
                var document = parseDocumentElement(element, context.Logger);

                // Validate that document parsing was successful
                if (document == null)
                {
                    result.Success = false;
                    result.Errors.Add("Could not parse main document metadata.");
                    return result;
                }

                // Set the source file name for tracking purposes
                document.SubmissionFileName = context.FileNameInZip;

                // Get the document repository and create the document entity
                var docRepo = context.GetRepository<Document>();
                await docRepo.CreateAsync(document);

                // Validate that the database assigned a document ID
                if (!document.DocumentID.HasValue)
                {
                    throw new InvalidOperationException("DocumentID was not populated by the database after creation.");
                }

                docID = document.DocumentID.Value;

                // Update the parsing context with the newly created document
                context.Document = document;
                result.DocumentsCreated = 1;
                result.ParsedEntity = document;
                result.DocumentCode = document?.DocumentCode;

                // Log successful document creation
                context.Logger.LogInformation("Created Document with ID {DocumentID} for file {FileName}",
                    docID, context.FileNameInZip);

                // --- PARSE LEGAL AUTHENTICATOR ---
                var legalAuthenticators = await getOrCreateLegalAuthenticatorsAsync(element, docID, context);
                result.DocumentAttributesCreated += legalAuthenticators.Count;

                // --- PARSE RELATED DOCUMENTS ---
                var relatedDocs = await getOrCreateRelatedDocumentsAsync(element, docID, context);
                result.DocumentAttributesCreated += relatedDocs.Count;

                reportProgress?.Invoke($"Completed Document XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during document parsing
                result.Success = false;
                result.Errors.Add($"Error parsing document: {ex.Message}");
                context.Logger.LogError(ex, "Error parsing document element");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates LegalAuthenticator records for all [legalAuthenticator] elements under a parent element.
        /// This method orchestrates the parsing, organization lookup, and entity creation for each authenticator.
        /// </summary>
        /// <param name="parentElement">The root XElement to search under (e.g., [document]).</param>
        /// <param name="sourceDocumentId">The Source Document ID (current document's primary key).</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>A list of LegalAuthenticator entities that were found or created.</returns>
        /// <remarks>
        /// This method orchestrates the process by:
        /// 1. Iterating through each [legalAuthenticator] element.
        /// 2. Parsing the raw data from the XML.
        /// 3. Finding or creating the associated [representedOrganization].
        /// 4. Finding or creating the final LegalAuthenticator entity, handling deduplication.
        /// </remarks>
        /// <seealso cref="LegalAuthenticator"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="parseLegalAuthenticatorData"/>
        /// <seealso cref="getOrCreateSignerOrganizationAsync"/>
        /// <seealso cref="findOrCreateSingleAuthenticatorAsync"/>
        private static async Task<List<LegalAuthenticator>> getOrCreateLegalAuthenticatorsAsync(
            XElement parentElement,
            int sourceDocumentId,
            SplParseContext context)
        {
            #region implementation
            var authenticators = new List<LegalAuthenticator>();

            // Validate required input parameters to prevent null reference exceptions
            if (parentElement == null || sourceDocumentId <= 0)
                return authenticators;

            // Validate required context dependencies to ensure proper service resolution
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return authenticators;

            // Find all <legalAuthenticator> elements and process each one
            foreach (var legalAuthEl in parentElement.SplElements(sc.E.LegalAuthenticator))
            {
                // 1. Parse the data from the current XML element
                var parsedData = parseLegalAuthenticatorData(legalAuthEl);

                // 2. Find or create the associated organization and get its ID
                var signerOrganizationId = await getOrCreateSignerOrganizationAsync(
                    parsedData.RepresentedOrgEl, context);

                // 3. Find or create the LegalAuthenticator entity based on the parsed data
                var authenticator = await findOrCreateSingleAuthenticatorAsync(
                    parsedData,
                    signerOrganizationId,
                    sourceDocumentId,
                    context);

                if (authenticator != null)
                {
                    // Add successfully created or found authenticator to the result list
                    authenticators.Add(authenticator);
                }
            }

            return authenticators;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// A private record to hold data parsed from a [legalAuthenticator] XElement.
        /// This record encapsulates all the relevant data extracted from the XML structure.
        /// </summary>
        /// <seealso cref="LegalAuthenticator"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Organization"/>
        private record LegalAuthenticatorData(
            DateTime? TimeValue,
            string? SignatureText,
            string? NoteText,
            string? AssignedPersonName,
            XElement? RepresentedOrgEl);

        /**************************************************************/
        /// <summary>
        /// Parses the relevant data from a single [legalAuthenticator] XElement.
        /// Extracts time, signature text, note text, assigned person name, and represented organization element.
        /// </summary>
        /// <param name="legalAuthEl">The [legalAuthenticator] XElement to parse.</param>
        /// <returns>A <see cref="LegalAuthenticatorData"/> record containing the extracted values.</returns>
        /// <seealso cref="LegalAuthenticatorData"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Util.ParseNullableDateTime"/>
        private static LegalAuthenticatorData parseLegalAuthenticatorData(XElement legalAuthEl)
        {
            #region implementation
            // Extract signature time from the time element's value attribute
            var timeValue = Util.ParseNullableDateTime(
                legalAuthEl.GetSplElementAttrVal(sc.E.Time, sc.A.Value) ?? string.Empty);

            // Extract signature text and note from their respective elements
            var signatureText = legalAuthEl.GetSplElementVal(sc.E.SignatureText);
            var noteText = legalAuthEl.GetSplElementVal(sc.E.NoteText);

            // Navigate to the assignedEntity to get person and organization details
            var assignedEntityEl = legalAuthEl.SplElement(sc.E.AssignedEntity);
            string? assignedPersonName = null;
            XElement? representedOrgEl = null;

            if (assignedEntityEl != null)
            {
                // Extract assigned person name from the nested structure
                assignedPersonName = assignedEntityEl.SplElement(sc.E.AssignedPerson, sc.E.Name)?.Value;
                // Get the represented organization element for further processing
                representedOrgEl = assignedEntityEl.SplElement(sc.E.RepresentedOrganization);
            }

            return new LegalAuthenticatorData(
                TimeValue: timeValue,
                SignatureText: signatureText,
                NoteText: noteText,
                AssignedPersonName: assignedPersonName,
                RepresentedOrgEl: representedOrgEl
            );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// This method handles the organization lookup and creation logic for legal authenticators.
        /// </summary>
        /// <param name="representedOrgEl">The [representedOrganization] XElement.</param>
        /// <param name="context">The parsing context for database access.</param>
        /// <returns>The primary key (OrganizationID) of the found or created organization, or null if no name is provided.</returns>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        private static async Task<int?> getOrCreateSignerOrganizationAsync(
            XElement? representedOrgEl,
            SplParseContext context)
        {
            #region implementation
            // Return null if no organization element is provided
            if (representedOrgEl == null || context?.ServiceProvider == null)
                return null;

            // Extract organization name from the XML element
            var orgName = representedOrgEl.GetSplElementVal(sc.E.Name);
            if (string.IsNullOrWhiteSpace(orgName))
                return null;

            // Get database context and organization dataset for queries
            var dbContext = context.GetDbContext();
            var orgDbSet = dbContext.Set<Organization>();

            // Look up the organization by name to check for existing record
            var existingOrg = await orgDbSet.FirstOrDefaultAsync(o => o.OrganizationName == orgName);
            if (existingOrg != null)
            {
                // Return existing organization ID if found
                return existingOrg.OrganizationID;
            }

            // If not found, create it with the extracted name
            var orgRepo = context.GetRepository<Organization>();
            var newOrg = new Organization { OrganizationName = orgName };
            await orgRepo.CreateAsync(newOrg);

            // Return the newly created organization ID
            return newOrg.OrganizationID;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing LegalAuthenticator for the document or creates a new one.
        /// This method handles the deduplication and creation logic for a single authenticator.
        /// </summary>
        /// <param name="data">The parsed data from the XML element.</param>
        /// <param name="signerOrganizationId">The ID of the signer's organization.</param>
        /// <param name="sourceDocumentId">The ID of the source document.</param>
        /// <param name="context">The parsing context for database access.</param>
        /// <returns>The existing or newly created <see cref="LegalAuthenticator"/> entity.</returns>
        /// <seealso cref="LegalAuthenticator"/>
        /// <seealso cref="LegalAuthenticatorData"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        private static async Task<LegalAuthenticator?> findOrCreateSingleAuthenticatorAsync(
            LegalAuthenticatorData data,
            int? signerOrganizationId,
            int sourceDocumentId,
            SplParseContext context)
        {
            #region implementation
            // Return null if no organization element is provided
            if (data == null || context?.ServiceProvider == null)
                return null;

            // Get database context and LegalAuthenticator dataset for queries
            var dbContext = context.GetDbContext();
            var dbSet = dbContext.Set<LegalAuthenticator>();

            // Check if an identical authenticator already exists for this document
            // Deduplication based on document ID, time, signature text, and assigned person name
            var existing = await dbSet.FirstOrDefaultAsync(la =>
                la.DocumentID == sourceDocumentId &&
                la.TimeValue == data.TimeValue &&
                la.SignatureText == data.SignatureText &&
                la.AssignedPersonName == data.AssignedPersonName);

            if (existing != null)
            {
                // Return the existing entity to avoid duplicates
                return existing;
            }

            // --- Create New Entity ---
            // Build new LegalAuthenticator entity with all parsed data
            var newAuthenticator = new LegalAuthenticator
            {
                DocumentID = sourceDocumentId,
                TimeValue = data.TimeValue,
                SignatureText = data.SignatureText,
                NoteText = data.NoteText,
                AssignedPersonName = data.AssignedPersonName,
                SignerOrganizationID = signerOrganizationId
            };

            // Persist the new entity to the database
            var repo = context.GetRepository<LegalAuthenticator>();
            await repo.CreateAsync(newAuthenticator);

            // Return the newly created authenticator
            return newAuthenticator;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates RelatedDocument records for all [relatedDocument] elements under a parent element.
        /// Handles APND, RPLC, DRIV, SUBJ, XCRPT, etc. relationships, supporting multiple/nested related documents.
        /// </summary>
        /// <param name="parentElement">The root XElement to search under (e.g., [document]).</param>
        /// <param name="sourceDocumentId">The Source Document ID (current document's primary key).</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>List of RelatedDocument entities (created or found).</returns>
        /// <seealso cref="RelatedDocument"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<RelatedDocument>> getOrCreateRelatedDocumentsAsync(
            XElement parentElement,
            int sourceDocumentId,
            SplParseContext context)
        {
            #region implementation
            var relatedDocs = new List<RelatedDocument>();

            // Validate required input parameters
            if (parentElement == null || sourceDocumentId <= 0)
                return relatedDocs;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return relatedDocs;

            // Get database context and repository for related document operations
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<RelatedDocument>();
            var dbSet = dbContext.Set<RelatedDocument>();

            // Find all <relatedDocument> elements that have a typeCode attribute (relationship type)
            // Process outer related document elements that define the relationship type
            foreach (var outerRelDocEl in parentElement.Descendants(sc.E.RelatedDocument).Where(e => e.Attribute(sc.A.TypeCode) != null))
            {
                // Extract the relationship type (APND, RPLC, DRIV, SUBJ, XCRPT, etc.)
                var relationshipType = outerRelDocEl.Attribute(sc.A.TypeCode)?.Value?.Trim();

                // One or more nested <relatedDocument> (can be direct or deeper descendants)
                // Process inner related document elements containing the actual document references
                foreach (var innerRelDocEl in outerRelDocEl.SplElements(sc.E.RelatedDocument))
                {
                    // -- Parse SetID (always required) --
                    // Extract the document set identifier which groups related document versions
                    var setIdStr = innerRelDocEl.SplElement(sc.E.SetId)?.Attribute(sc.A.Root)?.Value?.Trim();
                    Guid? referencedSetGuid = null;
                    if (!string.IsNullOrWhiteSpace(setIdStr) && Guid.TryParse(setIdStr, out var setGuid))
                        referencedSetGuid = setGuid;

                    // -- Parse ID (for RPLC/predecessor; optional, must be GUID if present) --
                    // Extract specific document identifier, primarily used for replacement relationships
                    var docIdStr = innerRelDocEl.SplElement(sc.E.Id)?.Attribute(sc.A.Root)?.Value?.Trim();
                    Guid? referencedDocGuid = null;
                    if (!string.IsNullOrWhiteSpace(docIdStr) && Guid.TryParse(docIdStr, out var docGuid))
                        referencedDocGuid = docGuid;

                    // -- Parse versionNumber (optional) --
                    // Extract version number to identify specific document version
                    int? versionNumber = null;
                    var versionStr = innerRelDocEl.SplElement(sc.E.VersionNumber)?.Attribute(sc.A.Value)?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(versionStr) && int.TryParse(versionStr, out var ver) && ver > 0)
                        versionNumber = ver;

                    // -- Parse code (optional, used for RPLC) --
                    // Extract document code information for additional document classification
                    var codeEl = innerRelDocEl.SplElement(sc.E.Code);
                    var refDocCode = codeEl?.Attribute(sc.A.CodeValue)?.Value?.Trim();
                    var refDocCodeSystem = codeEl?.Attribute(sc.A.CodeSystem)?.Value?.Trim();
                    var refDocDisplayName = codeEl?.Attribute(sc.A.DisplayName)?.Value?.Trim();

                    // --- Deduplication (same source, set, doc, version, and type) ---
                    // Search for existing related document with matching relationship attributes
                    var existing = await dbSet.FirstOrDefaultAsync(rd =>
                        rd.SourceDocumentID == sourceDocumentId &&
                        rd.RelationshipTypeCode == relationshipType &&
                        rd.ReferencedSetGUID == referencedSetGuid &&
                        rd.ReferencedDocumentGUID == referencedDocGuid &&
                        rd.ReferencedVersionNumber == versionNumber &&
                        rd.ReferencedDocumentCode == refDocCode);

                    // Return existing related document if found to avoid duplicates
                    if (existing != null)
                    {
                        relatedDocs.Add(existing);
                        continue;
                    }

                    // Create new related document entity with all extracted attributes
                    var newRelated = new RelatedDocument
                    {
                        SourceDocumentID = sourceDocumentId,
                        RelationshipTypeCode = relationshipType,
                        ReferencedSetGUID = referencedSetGuid,
                        ReferencedDocumentGUID = referencedDocGuid,
                        ReferencedVersionNumber = versionNumber,
                        ReferencedDocumentCode = refDocCode,
                        ReferencedDocumentCodeSystem = refDocCodeSystem,
                        ReferencedDocumentDisplayName = refDocDisplayName
                    };

                    // Persist new related document to database
                    await repo.CreateAsync(newRelated);
                    relatedDocs.Add(newRelated);
                }
            }

            return relatedDocs;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts document metadata from the document XML element and creates a Document entity.
        /// </summary>
        /// <param name="docEl">The XElement representing the document element to parse.</param>
        /// <param name="logger">The logger instance for error reporting.</param>
        /// <returns>A populated Document entity, or null if parsing fails.</returns>
        /// <example>
        /// <code>
        /// var document = parseDocumentElement(documentElement, logger);
        /// if (document != null)
        /// {
        ///     Console.WriteLine($"Document title: {document.Title}");
        ///     Console.WriteLine($"Document GUID: {document.DocumentGUID}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method extracts the following document metadata:
        /// - Document GUID from the id/root attribute
        /// - Document code information (code, system, display name)
        /// - Document title from the title element
        /// - Effective time from the effectiveTime/value attribute
        /// - Set GUID from the setId/root attribute
        /// - Version number from the versionNumber/value attribute
        /// 
        /// All parsing operations use utility methods to handle null values and data type conversions safely.
        /// </remarks>
        /// <seealso cref="Document"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Util.ParseNullableGuid(string)"/>
        /// <seealso cref="Util.ParseNullableDateTime(string)"/>
        /// <seealso cref="Util.ParseNullableInt(string)"/>
        private Document? parseDocumentElement(XElement docEl, ILogger logger)
        {
            #region implementation
            try
            {
                // Find the 'code' element once to reuse it for multiple attribute extractions
                var ce = docEl.SplElement(sc.E.Code);
                var titleEl = docEl.GetSplElement(sc.E.Title);
                var titleHtml = titleEl?.GetSplHtml(stripNamespaces: true)?.Trim();

                // Create and populate the Document entity with extracted metadata
                var document = new Document
                {
                    // Extract document GUID from the id element's root attribute
                    DocumentGUID = Util.ParseNullableGuid(docEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty),

                    // Extract code value from the code element's codeValue attribute
                    DocumentCode = ce?.GetAttrVal(sc.A.CodeValue),

                    // Extract code system from the code element's codeSystem attribute
                    DocumentCodeSystem = ce?.GetAttrVal(sc.A.CodeSystem),

                    // Extract display name from the code element's displayName attribute
                    DocumentDisplayName = ce?.GetAttrVal(sc.A.DisplayName),

                    // Document title from the title element
                    Title = titleHtml,

                    // Extract and parse effective time from the effectiveTime element's value attribute
                    EffectiveTime = Util.ParseNullableDateTime(docEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty),

                    // Extract set GUID from the setId element's root attribute
                    SetGUID = Util.ParseNullableGuid(docEl.GetSplElementAttrVal(sc.E.SetId, sc.A.Root) ?? string.Empty),

                    // Extract and parse version number from the versionNumber element's value attribute
                    VersionNumber = Util.ParseNullableInt(docEl.GetSplElementAttrVal(sc.E.VersionNumber, sc.A.Value) ?? string.Empty),
                };

                return document;
            }
            catch (Exception ex)
            {
                // Log parsing errors and return null to indicate failure
                logger.LogError(ex, "Error parsing <document> element attributes.");
                return null;
            }
            #endregion
        }
    }
}