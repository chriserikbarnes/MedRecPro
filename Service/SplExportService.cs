using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Service for exporting label documents from the database into properly formatted 
    /// SPL (Structured Product Labeling) XML files following FDA specifications.
    /// Transforms database DTOs into SPL-compliant XML structures for regulatory submission.
    /// </summary>
    /// <remarks>
    /// This service orchestrates the complete transformation pipeline from database
    /// entities to SPL XML, ensuring compliance with HL7 v3 standards and FDA requirements.
    /// Handles encrypted ID decryption to establish proper entity relationships.
    /// </remarks>
    /// <example>
    /// <code>
    /// var splService = new SplExportService(db, pkSecret, logger);
    /// var xmlContent = await splService.ExportDocumentToSplAsync(documentGuid);
    /// await File.WriteAllTextAsync($"{documentGuid}.xml", xmlContent);
    /// </code>
    /// </example>
    /// <seealso cref="Label.Document"/>
    /// <seealso cref="SplDocumentDto"/>
    /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, ILogger)"/>
    public class SplExportService
    {
        #region implementation

        private readonly ApplicationDbContext _db;
        private readonly string _pkSecret;
        private readonly ILogger _logger;
        private readonly StringCipher _cipher;

        // ID lookup caches for decrypted relationships
        private readonly Dictionary<string, SectionDto> _sectionLookup = new();
        private readonly Dictionary<string, ProductDto> _productLookup = new();
        private readonly Dictionary<string, IngredientDto> _ingredientLookup = new();

        // Code system OIDs as constants to avoid magic strings
        private const string LOINC_CODE_SYSTEM = "2.16.840.1.113883.6.1";
        private const string LOINC_CODE_SYSTEM_NAME = "LOINC";
        private const string FDA_SRS_CODE_SYSTEM = "2.16.840.1.113883.4.9";
        private const string FDA_SRS_CODE_SYSTEM_NAME = "FDA SRS";
        private const string FDA_FORM_CODE_SYSTEM = "2.16.840.1.113883.3.26.1.1";
        private const string NDC_CODE_SYSTEM = "2.16.840.1.113883.6.69";
        private const string NDC_CODE_SYSTEM_NAME = "NDC";
        private const string CONFIDENTIALITY_CODE_SYSTEM = "2.16.840.1.113883.5.25";
        private const string TERRITORY_CODE_SYSTEM = "2.16.840.1.113883.5.28";

        // Marketing and status codes
        private const string DEFAULT_MARKETING_ACT_CODE = "C53292";
        private const string DEFAULT_STATUS_CODE = "active";
        private const string DEFAULT_TERRITORY_CODE = "USA";
        private const string CONFIDENTIAL_CODE = "B";

        // Class codes
        private const string INGREDIENT_CLASS_IACT = "IACT";
        private const string SPECIALIZED_KIND_CLASS_GEN = "GEN";

        // Relationship codes
        private const string RELATIONSHIP_TYPE_RPLC = "RPLC";

        // Media types
        private const string TITLE_MEDIA_TYPE = "text/x-hl7-title+xml";

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SPL export service with required dependencies.
        /// </summary>
        /// <param name="db">The application database context for data access.</param>
        /// <param name="pkSecret">Secret key used for ID encryption/decryption.</param>
        /// <param name="logger">Logger instance for diagnostic and error logging.</param>
        /// <seealso cref="ApplicationDbContext"/>
        public SplExportService(ApplicationDbContext db, string pkSecret, ILogger logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _pkSecret = pkSecret ?? throw new ArgumentNullException(nameof(pkSecret));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cipher = new StringCipher();
        }

        /**************************************************************/
        /// <summary>
        /// Exports a document from the database as SPL XML format by its GUID identifier.
        /// Main entry point for SPL export functionality that orchestrates the complete
        /// transformation from database entities to formatted XML output.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to export.</param>
        /// <returns>SPL-formatted XML string ready for FDA submission or file storage.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("A4FD2D68-019F-4C89-8923-4E61262F6EEE");
        /// string splXml = await ExportDocumentToSplAsync(documentGuid);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the complete export pipeline:
        /// 1. Fetches document data from database using BuildDocumentsAsync
        /// 2. Builds lookup caches for encrypted ID relationships
        /// 3. Transforms DocumentDto to SplDocumentDto structure
        /// 4. Serializes to XML with proper namespaces and formatting
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, ILogger)"/>
        /// <seealso cref="transformDocumentToSpl"/>
        /// <seealso cref="serializeToSplXml"/>
        public async Task<string> ExportDocumentToSplAsync(Guid documentGuid)
        {
            #region implementation

            _logger.LogInformation("Starting SPL export for document {DocumentGuid}", documentGuid);

            try
            {
                // Fetch document data from database
                var documents = await DtoLabelAccess.BuildDocumentsAsync(_db, documentGuid, _pkSecret, _logger);

                if (documents == null || !documents.Any())
                {
                    throw new InvalidOperationException($"Document with GUID {documentGuid} not found");
                }

                var documentDto = documents.First();

                // Build lookup caches for encrypted ID relationships
                buildLookupCaches(documentDto);

                // Transform to SPL DTO structure
                var splDocument = transformDocumentToSpl(documentDto);

                // Serialize to XML
                var xmlContent = serializeToSplXml(splDocument);

                _logger.LogInformation("Successfully exported document {DocumentGuid} to SPL XML", documentGuid);

                return xmlContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting document {DocumentGuid} to SPL", documentGuid);
                throw;
            }

            #endregion
        }

        #region lookup cache methods

        /**************************************************************/
        /// <summary>
        /// Builds lookup caches for encrypted ID relationships to enable proper entity linking.
        /// Decrypts all encrypted IDs and creates dictionaries for fast relationship resolution.
        /// </summary>
        /// <param name="documentDto">The document DTO containing all entities.</param>
        /// <remarks>
        /// This method creates lookup dictionaries that map decrypted IDs to their corresponding
        /// entities, enabling the transformation methods to establish proper relationships.
        /// </remarks>
        private void buildLookupCaches(DocumentDto documentDto)
        {
            #region implementation

            _logger.LogDebug("Building lookup caches for encrypted ID relationships");

            // Clear existing caches
            _sectionLookup.Clear();
            _productLookup.Clear();
            _ingredientLookup.Clear();

            // Build section lookup cache
            foreach (var structuredBody in documentDto.StructuredBodies)
            {
                foreach (var section in structuredBody.Sections)
                {
                    var encryptedSectionId = section.Section.GetValueOrDefault("encryptedSectionID")?.ToString();
                    if (!string.IsNullOrEmpty(encryptedSectionId))
                    {
                        try
                        {
                            var decryptedId = _cipher.Decrypt(encryptedSectionId, _pkSecret);
                            _sectionLookup[decryptedId] = section;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to decrypt section ID {EncryptedId}", encryptedSectionId);
                        }
                    }

                    // Build product lookup cache
                    foreach (var product in section.Products)
                    {
                        var encryptedProductId = product.Product.GetValueOrDefault("encryptedProductID")?.ToString();
                        if (!string.IsNullOrEmpty(encryptedProductId))
                        {
                            try
                            {
                                var decryptedId = _cipher.Decrypt(encryptedProductId, _pkSecret);
                                _productLookup[decryptedId] = product;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to decrypt product ID {EncryptedId}", encryptedProductId);
                            }
                        }

                        // Build ingredient lookup cache
                        foreach (var ingredient in product.Ingredients)
                        {
                            var encryptedIngredientId = ingredient.Ingredient.GetValueOrDefault("encryptedIngredientID")?.ToString();
                            if (!string.IsNullOrEmpty(encryptedIngredientId))
                            {
                                try
                                {
                                    var decryptedId = _cipher.Decrypt(encryptedIngredientId, _pkSecret);
                                    _ingredientLookup[decryptedId] = ingredient;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to decrypt ingredient ID {EncryptedId}", encryptedIngredientId);
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogDebug("Built lookup caches: {SectionCount} sections, {ProductCount} products, {IngredientCount} ingredients",
                _sectionLookup.Count, _productLookup.Count, _ingredientLookup.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted ID string using the configured cipher and secret.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID string to decrypt.</param>
        /// <returns>The decrypted ID string, or null if decryption fails.</returns>
        /// <remarks>
        /// This method safely handles decryption failures and logs warnings for debugging.
        /// </remarks>
        private string? decryptId(string? encryptedId)
        {
            #region implementation

            if (string.IsNullOrEmpty(encryptedId))
                return null;

            try
            {
                return _cipher.Decrypt(encryptedId, _pkSecret);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt ID {EncryptedId}", encryptedId);
                return null;
            }

            #endregion
        }

        #endregion

        #region transformation methods

        /**************************************************************/
        /// <summary>
        /// Transforms a DocumentDto from the database into an SPL-compliant document structure.
        /// Primary transformation method that coordinates all sub-transformations for document components.
        /// </summary>
        /// <param name="documentDto">The document DTO containing database data to transform.</param>
        /// <returns>Fully populated SPL document DTO ready for XML serialization.</returns>
        /// <remarks>
        /// This method orchestrates the transformation of all major document components including
        /// header information, authors, related documents, and structured body content.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="SplDocumentDto"/>
        /// <seealso cref="transformDocumentHeader"/>
        /// <seealso cref="transformAuthors"/>
        /// <seealso cref="transformStructuredBody"/>
        private SplDocumentDto transformDocumentToSpl(DocumentDto documentDto)
        {
            #region implementation

            var splDocument = new SplDocumentDto();
            var doc = documentDto.Document;

            // Transform document header information
            transformDocumentHeader(splDocument, doc);

            // Transform authors
            if (documentDto.DocumentAuthors.Any())
            {
                splDocument.Authors = transformAuthors(documentDto.DocumentAuthors);
            }

            // Transform related documents
            if (documentDto.SourceRelatedDocuments.Any())
            {
                splDocument.RelatedDocuments = transformRelatedDocuments(documentDto.SourceRelatedDocuments);
            }

            // Transform legal authenticator
            if (documentDto.LegalAuthenticators.Any())
            {
                splDocument.LegalAuthenticator = transformLegalAuthenticator(documentDto.LegalAuthenticators.First());
            }

            // Transform structured body
            if (documentDto.StructuredBodies.Any())
            {
                splDocument.Component = transformStructuredBody(documentDto.StructuredBodies.First());
            }

            return splDocument;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms document header metadata including identification, versioning, and timing information.
        /// Populates the core document identification elements required by SPL specification.
        /// </summary>
        /// <param name="splDocument">The SPL document DTO to populate with header information.</param>
        /// <param name="doc">Dictionary containing document metadata from the database.</param>
        /// <remarks>
        /// Transforms document GUID, code, title, effective time, set ID, and version number
        /// into their corresponding SPL representations.
        /// </remarks>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        /// <seealso cref="Label.Document.DocumentCode"/>
        /// <seealso cref="Label.Document.Title"/>
        /// <seealso cref="Label.Document.EffectiveTime"/>
        /// <seealso cref="Label.Document.SetGUID"/>
        /// <seealso cref="Label.Document.VersionNumber"/>
        private void transformDocumentHeader(SplDocumentDto splDocument, Dictionary<string, object?> doc)
        {
            #region implementation

            // Document ID
            splDocument.DocumentId = new SplIdentifierDto
            {
                Root = doc.GetValueOrDefault("documentGUID")?.ToString() ?? string.Empty
            };

            // Document code
            splDocument.DocumentCode = new SplCodeDto
            {
                Code = doc.GetValueOrDefault("documentCode")?.ToString() ?? string.Empty,
                CodeSystem = doc.GetValueOrDefault("documentCodeSystem")?.ToString() ?? LOINC_CODE_SYSTEM,
                DisplayName = doc.GetValueOrDefault("documentDisplayName")?.ToString(),
                CodeSystemName = LOINC_CODE_SYSTEM_NAME
            };

            // Title
            var title = doc.GetValueOrDefault("title")?.ToString();
            if (!string.IsNullOrEmpty(title))
            {
                splDocument.Title = new SplTitleDto
                {
                    MediaType = TITLE_MEDIA_TYPE,
                    Content = title
                };
            }

            // Effective time
            var effectiveTime = doc.GetValueOrDefault("effectiveTime")?.ToString();
            if (!string.IsNullOrEmpty(effectiveTime))
            {
                splDocument.EffectiveTime = new SplEffectiveTimeDto
                {
                    Value = formatDateTimeForSpl(effectiveTime)
                };
            }

            // Set ID
            splDocument.SetId = new SplIdentifierDto
            {
                Root = doc.GetValueOrDefault("setGUID")?.ToString() ?? string.Empty
            };

            // Version number
            var versionNumber = doc.GetValueOrDefault("versionNumber")?.ToString() ?? "1";
            splDocument.VersionNumber = new SplVersionNumberDto
            {
                Value = versionNumber
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms document author information including organizational hierarchies and contact details.
        /// Supports both simple authors and complex labeler organizations with business operations.
        /// </summary>
        /// <param name="documentAuthors">Collection of document author DTOs from the database.</param>
        /// <returns>List of SPL author DTOs with complete organizational structures.</returns>
        /// <remarks>
        /// Handles different author types including labelers with complex organizational
        /// hierarchies, business operations, and contact information.
        /// Creates the nested assignedEntity/assignedOrganization structure seen in SPL templates.
        /// </remarks>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="SplAuthorDto"/>
        /// <seealso cref="transformOrganization"/>
        private List<SplAuthorDto> transformAuthors(List<DocumentAuthorDto> documentAuthors)
        {
            #region implementation

            var authors = new List<SplAuthorDto>();

            foreach (var authorDto in documentAuthors)
            {
                var author = new SplAuthorDto
                {
                    Time = new SplTimeValueDto(), // Empty time element as seen in templates
                    AssignedEntity = new SplAssignedEntityDto()
                };

                var authorData = authorDto.DocumentAuthor;
                var authorType = authorData.GetValueOrDefault("authorType")?.ToString();

                if (authorType == "Labeler" && authorDto.Organization != null)
                {
                    // Complex labeler organization with nested structure
                    var organization = transformOrganization(authorDto.Organization);

                    // Create the nested assignedEntity/assignedOrganization structure
                    // as seen in the drug template
                    author.AssignedEntity.RepresentedOrganization = organization;

                    // Add nested assigned entities for child organizations
                    if (authorDto.Organization.ChildRelationships.Any())
                    {
                        author.AssignedEntity.RepresentedOrganization.AssignedEntities =
                            transformChildOrganizationsForAuthor(authorDto.Organization.ChildRelationships);
                    }
                }
                else
                {
                    // Simple author
                    author.AssignedEntity.Id = new SplIdentifierDto
                    {
                        Root = Guid.NewGuid().ToString()
                    };
                }

                authors.Add(author);
            }

            return authors;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms organization data including identifiers, contact information, and business operations.
        /// Handles complex organizational hierarchies with establishments and regulatory relationships.
        /// </summary>
        /// <param name="organizationDto">The organization DTO containing database data.</param>
        /// <returns>SPL organization DTO with complete structure including nested entities.</returns>
        /// <remarks>
        /// Processes organization identifiers (DUNS, FEI), contact details, addresses,
        /// business operations, and hierarchical relationships between organizations.
        /// </remarks>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="Label.OrganizationIdentifier"/>
        /// <seealso cref="Label.BusinessOperation"/>
        /// <seealso cref="SplOrganizationDto"/>
        /// <seealso cref="transformAddress"/>
        /// <seealso cref="transformContactParty"/>
        private SplOrganizationDto transformOrganization(OrganizationDto organizationDto)
        {
            #region implementation

            var splOrg = new SplOrganizationDto();
            var org = organizationDto.Organization;

            // Organization name
            splOrg.Name = org.GetValueOrDefault("organizationName")?.ToString() ?? string.Empty;

            // Organization identifiers
            if (organizationDto.Identifiers.Any())
            {
                splOrg.Identifiers = organizationDto.Identifiers.Select(id =>
                {
                    var identifier = id.OrganizationIdentifier;
                    return new SplIdentifierDto
                    {
                        Root = identifier.GetValueOrDefault("identifierSystemOID")?.ToString() ?? string.Empty,
                        Extension = identifier.GetValueOrDefault("identifierValue")?.ToString()
                    };
                }).ToList();
            }

            // Confidentiality code
            var isConfidential = org.GetValueOrDefault("isConfidential") as bool? ?? false;
            if (isConfidential)
            {
                splOrg.ConfidentialityCode = new SplCodeDto
                {
                    Code = CONFIDENTIAL_CODE,
                    CodeSystem = CONFIDENTIALITY_CODE_SYSTEM
                };
            }

            // Contact parties
            if (organizationDto.ContactParties.Any())
            {
                var contactParty = organizationDto.ContactParties.First();
                splOrg.ContactParty = transformContactParty(contactParty);
            }

            // Handle document relationships for business operations
            if (organizationDto.ChildRelationships.Any())
            {
                splOrg.AssignedEntities = transformChildOrganizations(organizationDto.ChildRelationships);
                splOrg.Performances = transformBusinessOperations(organizationDto.ChildRelationships);
            }

            return splOrg;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms child organizational relationships for author structures.
        /// Creates the specific nested assignedEntity/assignedOrganization pattern seen in SPL templates.
        /// </summary>
        /// <param name="relationships">Collection of document relationship DTOs.</param>
        /// <returns>List of assigned entity DTOs with nested organization and performance structures.</returns>
        /// <remarks>
        /// This method creates the specific structure seen in drug labels where there are
        /// nested assignedEntity/assignedOrganization elements with performance operations.
        /// </remarks>
        private List<SplAssignedEntityDto> transformChildOrganizationsForAuthor(List<DocumentRelationshipDto> relationships)
        {
            #region implementation

            var entities = new List<SplAssignedEntityDto>();

            foreach (var rel in relationships.Where(r => r.ChildOrganization != null))
            {
                var entity = new SplAssignedEntityDto
                {
                    AssignedOrganization = new SplAssignedOrganizationDto
                    {
                        AssignedEntity = new SplAssignedEntityDto
                        {
                            AssignedOrganization = new SplAssignedOrganizationDto
                            {
                                Id = rel.ChildOrganization!.Identifiers.Select(id => new SplIdentifierDto
                                {
                                    Root = id.OrganizationIdentifier.GetValueOrDefault("identifierSystemOID")?.ToString() ?? string.Empty,
                                    Extension = id.OrganizationIdentifier.GetValueOrDefault("identifierValue")?.ToString()
                                }).FirstOrDefault() ?? new SplIdentifierDto(),
                                Name = rel.ChildOrganization.Organization.GetValueOrDefault("organizationName")?.ToString() ?? string.Empty
                            }
                        }
                    }
                };

                // Add performance operations as seen in the template
                if (rel.BusinessOperations.Any())
                {
                    entity.AssignedOrganization.AssignedEntity.Performances =
                        transformBusinessOperationsSimple(rel.BusinessOperations);
                }

                entities.Add(entity);
            }

            return entities;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms business operations into simple performance structures for author organizations.
        /// Creates the actDefinition structure seen in SPL templates.
        /// </summary>
        /// <param name="businessOperations">Collection of business operation DTOs.</param>
        /// <returns>List of simple performance DTOs.</returns>
        private List<SplPerformanceDto> transformBusinessOperationsSimple(List<BusinessOperationDto> businessOperations)
        {
            #region implementation

            return businessOperations.Select(busOp =>
            {
                var operation = busOp.BusinessOperation;
                return new SplPerformanceDto
                {
                    ActDefinition = new SplActDefinitionDto
                    {
                        Code = new SplCodeDto
                        {
                            Code = operation.GetValueOrDefault("operationCode")?.ToString() ?? string.Empty,
                            CodeSystem = operation.GetValueOrDefault("operationCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                            DisplayName = operation.GetValueOrDefault("operationDisplayName")?.ToString()
                        }
                    }
                };
            }).ToList();

            #endregion
        }
        /// Handles establishment and other subordinate organization relationships.
        /// </summary>
        /// <param name="relationships">Collection of document relationship DTOs.</param>
        /// <returns>List of assigned entity DTOs representing child organizations.</returns>
        /// <remarks>
        /// Processes organizational hierarchies such as registrant-establishment relationships
        /// maintaining the proper parent-child structure required by SPL.
        /// </remarks>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="SplAssignedEntityDto"/>
        private List<SplAssignedEntityDto> transformChildOrganizations(List<DocumentRelationshipDto> relationships)
        {
            #region implementation

            var entities = new List<SplAssignedEntityDto>();

            foreach (var rel in relationships.Where(r => r.ChildOrganization != null))
            {
                var entity = new SplAssignedEntityDto
                {
                    RepresentedOrganization = transformOrganization(rel.ChildOrganization!)
                };
                entities.Add(entity);
            }

            return entities;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms business operations from document relationships into performance structures.
        /// Converts manufacturing, testing, and other regulatory operations into SPL format.
        /// </summary>
        /// <param name="relationships">Collection of document relationships containing business operations.</param>
        /// <returns>List of performance DTOs representing business operations.</returns>
        /// <remarks>
        /// Processes business operations with their qualifiers and associated products,
        /// maintaining FDA-required operation codes and relationships.
        /// </remarks>
        /// <seealso cref="Label.BusinessOperation"/>
        /// <seealso cref="Label.BusinessOperationQualifier"/>
        /// <seealso cref="Label.BusinessOperationProductLink"/>
        /// <seealso cref="SplPerformanceDto"/>
        private List<SplPerformanceDto> transformBusinessOperations(List<DocumentRelationshipDto> relationships)
        {
            #region implementation

            var performances = new List<SplPerformanceDto>();

            foreach (var rel in relationships)
            {
                foreach (var busOp in rel.BusinessOperations)
                {
                    var operation = busOp.BusinessOperation;
                    var performance = new SplPerformanceDto
                    {
                        ActDefinition = new SplActDefinitionDto
                        {
                            Code = new SplCodeDto
                            {
                                Code = operation.GetValueOrDefault("operationCode")?.ToString() ?? string.Empty,
                                CodeSystem = operation.GetValueOrDefault("operationCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                                DisplayName = operation.GetValueOrDefault("operationDisplayName")?.ToString()
                            }
                        }
                    };

                    // Add qualifiers
                    if (busOp.BusinessOperationQualifiers.Any())
                    {
                        performance.ActDefinition.SubjectOf = busOp.BusinessOperationQualifiers.Select(q =>
                        {
                            var qualifier = q.BusinessOperationQualifier;
                            return new SplSubjectOfDto
                            {
                                Approval = new SplApprovalDto
                                {
                                    Code = new SplCodeDto
                                    {
                                        Code = qualifier.GetValueOrDefault("qualifierCode")?.ToString() ?? string.Empty,
                                        CodeSystem = qualifier.GetValueOrDefault("qualifierCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                                        DisplayName = qualifier.GetValueOrDefault("qualifierDisplayName")?.ToString()
                                    }
                                }
                            };
                        }).ToList();
                    }

                    // Add product links
                    foreach (var certLink in rel.CertificationProductLinks)
                    {
                        var link = certLink.CertificationProductLink;
                        performance.ActDefinition.Products.Add(new SplProductLinkDto
                        {
                            ManufacturedProduct = new SplManufacturedProductLinkDto
                            {
                                ManufacturedMaterialKind = new SplMaterialKindDto
                                {
                                    Code = new SplCodeDto
                                    {
                                        Code = link.GetValueOrDefault("productCode")?.ToString() ?? string.Empty,
                                        CodeSystem = NDC_CODE_SYSTEM
                                    }
                                }
                            }
                        });
                    }

                    performances.Add(performance);
                }
            }

            return performances;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms contact party information including addresses and telecommunications.
        /// Handles organizational contact details with multiple communication methods.
        /// </summary>
        /// <param name="contactPartyDto">The contact party DTO containing contact information.</param>
        /// <returns>SPL contact party DTO with complete contact details.</returns>
        /// <remarks>
        /// Processes physical addresses, phone numbers, email addresses, and contact persons
        /// maintaining the proper structure for regulatory submissions.
        /// </remarks>
        /// <seealso cref="Label.ContactParty"/>
        /// <seealso cref="Label.ContactPartyTelecom"/>
        /// <seealso cref="Label.ContactPerson"/>
        /// <seealso cref="SplContactPartyDto"/>
        private SplContactPartyDto transformContactParty(ContactPartyDto contactPartyDto)
        {
            #region implementation

            var splContactParty = new SplContactPartyDto();

            // Address
            if (contactPartyDto.Address != null)
            {
                splContactParty.Address = transformAddress(contactPartyDto.Address);
            }

            // Telecoms
            if (contactPartyDto.Telecoms.Any())
            {
                splContactParty.Telecoms = contactPartyDto.Telecoms.Select(t =>
                {
                    var telecom = t.ContactPartyTelecom;
                    var telecomData = t.Telecom?.Telecom;
                    return new SplTelecomDto
                    {
                        Value = telecomData?.GetValueOrDefault("telecomValue")?.ToString() ?? string.Empty
                    };
                }).ToList();
            }

            // Contact person
            if (contactPartyDto.ContactPerson != null)
            {
                var person = contactPartyDto.ContactPerson.ContactPerson;
                splContactParty.ContactPerson = new SplContactPersonDto
                {
                    Name = person.GetValueOrDefault("contactPersonName")?.ToString() ?? string.Empty
                };
            }

            return splContactParty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms address information into SPL-compliant address structure.
        /// Handles both domestic and international addresses with proper formatting.
        /// </summary>
        /// <param name="addressDto">The address DTO containing location information.</param>
        /// <returns>SPL address DTO with formatted address components.</returns>
        /// <remarks>
        /// Processes street addresses, city, state, postal code, and country information
        /// following SPL requirements for address representation.
        /// </remarks>
        /// <seealso cref="Label.Address"/>
        /// <seealso cref="SplAddressDto"/>
        private SplAddressDto transformAddress(AddressDto addressDto)
        {
            #region implementation

            var splAddress = new SplAddressDto();
            var addr = addressDto.Address;

            // Street address lines
            var street1 = addr.GetValueOrDefault("streetAddressLine1")?.ToString();
            var street2 = addr.GetValueOrDefault("streetAddressLine2")?.ToString();

            if (!string.IsNullOrEmpty(street1))
                splAddress.StreetAddressLines.Add(street1);
            if (!string.IsNullOrEmpty(street2))
                splAddress.StreetAddressLines.Add(street2);

            // City, state, postal code
            splAddress.City = addr.GetValueOrDefault("city")?.ToString();
            splAddress.State = addr.GetValueOrDefault("stateProvince")?.ToString();
            splAddress.PostalCode = addr.GetValueOrDefault("postalCode")?.ToString();

            // Country
            var countryCode = addr.GetValueOrDefault("countryCode")?.ToString();
            if (!string.IsNullOrEmpty(countryCode))
            {
                splAddress.Country = new SplCountryDto
                {
                    Code = countryCode,
                    Name = addr.GetValueOrDefault("countryName")?.ToString()
                };
            }

            return splAddress;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms related document references establishing document relationships.
        /// Handles document versioning, replacements, and core document references.
        /// </summary>
        /// <param name="relatedDocuments">Collection of related document DTOs.</param>
        /// <returns>List of SPL related document DTOs with relationship metadata.</returns>
        /// <remarks>
        /// Processes document relationships including RPLC (replaces), APND (appends),
        /// and DRIV (derived from) maintaining document version history.
        /// </remarks>
        /// <seealso cref="Label.RelatedDocument"/>
        /// <seealso cref="SplRelatedDocumentDto"/>
        private List<SplRelatedDocumentDto> transformRelatedDocuments(List<RelatedDocumentDto> relatedDocuments)
        {
            #region implementation

            return relatedDocuments.Select(rd =>
            {
                var related = rd.RelatedDocument;
                var splRelated = new SplRelatedDocumentDto
                {
                    TypeCode = related.GetValueOrDefault("relationshipTypeCode")?.ToString() ?? RELATIONSHIP_TYPE_RPLC,
                    RelatedDocument = new SplRelatedDocumentContentDto
                    {
                        SetId = new SplIdentifierDto
                        {
                            Root = related.GetValueOrDefault("referencedSetGUID")?.ToString() ?? string.Empty
                        }
                    }
                };

                // Optional document ID for RPLC relationships
                var docGuid = related.GetValueOrDefault("referencedDocumentGUID")?.ToString();
                if (!string.IsNullOrEmpty(docGuid))
                {
                    splRelated.RelatedDocument.Id = new SplIdentifierDto { Root = docGuid };
                }

                // Optional version number
                var version = related.GetValueOrDefault("referencedVersionNumber")?.ToString();
                if (!string.IsNullOrEmpty(version))
                {
                    splRelated.RelatedDocument.VersionNumber = new SplVersionNumberDto { Value = version };
                }

                // Optional document code
                var docCode = related.GetValueOrDefault("referencedDocumentCode")?.ToString();
                if (!string.IsNullOrEmpty(docCode))
                {
                    splRelated.RelatedDocument.Code = new SplCodeDto
                    {
                        Code = docCode,
                        CodeSystem = related.GetValueOrDefault("referencedDocumentCodeSystem")?.ToString() ?? LOINC_CODE_SYSTEM
                    };
                }

                return splRelated;
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms legal authenticator information for signed documents.
        /// Handles electronic signatures and signer identification for regulatory compliance.
        /// </summary>
        /// <param name="legalAuthDto">The legal authenticator DTO containing signature information.</param>
        /// <returns>SPL legal authenticator DTO with signature and signer details.</returns>
        /// <remarks>
        /// Processes signature timestamps, signature text, and signer organization information
        /// required for legally authenticated SPL documents.
        /// </remarks>
        /// <seealso cref="Label.LegalAuthenticator"/>
        /// <seealso cref="SplLegalAuthenticatorDto"/>
        private SplLegalAuthenticatorDto transformLegalAuthenticator(LegalAuthenticatorDto legalAuthDto)
        {
            #region implementation

            var legalAuth = legalAuthDto.LegalAuthenticator;
            var splLegalAuth = new SplLegalAuthenticatorDto
            {
                Time = new SplTimeValueDto
                {
                    Value = formatDateTimeForSpl(legalAuth.GetValueOrDefault("timeValue")?.ToString())
                },
                SignatureText = legalAuth.GetValueOrDefault("signatureText")?.ToString() ?? string.Empty,
                NoteText = legalAuth.GetValueOrDefault("noteText")?.ToString(),
                AssignedEntity = new SplLegalAuthenticatorEntityDto
                {
                    AssignedPerson = new SplAssignedPersonDto
                    {
                        Name = legalAuth.GetValueOrDefault("assignedPersonName")?.ToString() ?? string.Empty
                    }
                }
            };

            // Add organization if present
            if (legalAuthDto.Organization != null)
            {
                splLegalAuth.AssignedEntity.RepresentedOrganization = transformOrganization(legalAuthDto.Organization);
            }

            return splLegalAuth;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms the document's structured body containing all sections and content.
        /// Primary method for converting document content into SPL component structure.
        /// </summary>
        /// <param name="structuredBodyDto">The structured body DTO containing sections.</param>
        /// <returns>SPL component DTO with complete document body structure.</returns>
        /// <remarks>
        /// Orchestrates the transformation of all document sections including their
        /// hierarchies, content, and embedded products or substances.
        /// Creates an empty structured body if no sections are present, as seen in templates.
        /// </remarks>
        /// <seealso cref="Label.StructuredBody"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="SplComponentDto"/>
        /// <seealso cref="transformSection"/>
        private SplComponentDto transformStructuredBody(StructuredBodyDto structuredBodyDto)
        {
            #region implementation

            var component = new SplComponentDto
            {
                StructuredBody = new SplStructuredBodyDto()
            };

            // Transform sections - only top-level sections (those with no parent hierarchies)
            var topLevelSections = structuredBodyDto.Sections
                .Where(s => s.ParentSectionHierarchies.Count == 0)
                .OrderBy(s => s.Section.GetValueOrDefault("sequenceNumber"))
                .ToList();

            if (topLevelSections.Any())
            {
                component.StructuredBody.Components = topLevelSections
                    .Select(s => new SplSectionComponentDto
                    {
                        Section = transformSection(s)
                    }).ToList();
            }
            else
            {
                // Create empty components list for templates that show empty structured body
                component.StructuredBody.Components = new List<SplSectionComponentDto>();
            }

            return component;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms a section including its metadata, content, and nested structures.
        /// Handles complex section hierarchies with products, substances, and rich text content.
        /// </summary>
        /// <param name="sectionDto">The section DTO containing section data.</param>
        /// <returns>SPL section DTO with complete content and metadata.</returns>
        /// <remarks>
        /// Processes section identification, titles, text content, effective times,
        /// products, and nested subsections maintaining document structure integrity.
        /// </remarks>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.IdentifiedSubstance"/>
        /// <seealso cref="SplSectionDto"/>
        /// <seealso cref="transformSectionText"/>
        /// <seealso cref="transformProduct"/>
        private SplSectionDto transformSection(SectionDto sectionDto)
        {
            #region implementation

            var section = sectionDto.Section;
            var splSection = new SplSectionDto
            {
                Id = new SplIdentifierDto
                {
                    Root = section.GetValueOrDefault("sectionGUID")?.ToString() ?? Guid.NewGuid().ToString()
                }
            };

            // Section link ID for cross-references
            var linkGuid = section.GetValueOrDefault("sectionLinkGUID")?.ToString();
            if (!string.IsNullOrEmpty(linkGuid))
            {
                splSection.ID = linkGuid;
            }

            // Section code
            var sectionCode = section.GetValueOrDefault("sectionCode")?.ToString();
            if (!string.IsNullOrEmpty(sectionCode))
            {
                splSection.Code = new SplCodeDto
                {
                    Code = sectionCode,
                    CodeSystem = section.GetValueOrDefault("sectionCodeSystem")?.ToString() ?? LOINC_CODE_SYSTEM,
                    DisplayName = section.GetValueOrDefault("sectionDisplayName")?.ToString(),
                    CodeSystemName = LOINC_CODE_SYSTEM_NAME
                };
            }

            // Title
            var title = section.GetValueOrDefault("title")?.ToString();
            if (!string.IsNullOrEmpty(title))
            {
                var mediaType = section.GetValueOrDefault("titleMediaType")?.ToString();
                splSection.Title = new SplTitleDto
                {
                    MediaType = mediaType,
                    Content = title
                };
            }

            // Text content
            if (sectionDto.TextContents.Any())
            {
                splSection.Text = transformSectionText(sectionDto.TextContents);
            }

            // Observation media
            if (sectionDto.ObservationMedia.Any())
            {
                splSection.ObservationMedia = transformObservationMedia(sectionDto.ObservationMedia);
            }

            // Effective time
            var effectiveTime = section.GetValueOrDefault("effectiveTime")?.ToString();
            if (!string.IsNullOrEmpty(effectiveTime))
            {
                splSection.EffectiveTime = new SplEffectiveTimeDto
                {
                    Value = formatDateTimeForSpl(effectiveTime)
                };
            }
            else
            {
                // Check for time range
                var lowTime = section.GetValueOrDefault("effectiveTimeLow")?.ToString();
                var highTime = section.GetValueOrDefault("effectiveTimeHigh")?.ToString();
                if (!string.IsNullOrEmpty(lowTime) || !string.IsNullOrEmpty(highTime))
                {
                    splSection.EffectiveTime = new SplEffectiveTimeDto();
                    if (!string.IsNullOrEmpty(lowTime))
                    {
                        splSection.EffectiveTime.Low = new SplTimeValueDto
                        {
                            Value = formatDateTimeForSpl(lowTime)
                        };
                    }
                    if (!string.IsNullOrEmpty(highTime))
                    {
                        splSection.EffectiveTime.High = new SplTimeValueDto
                        {
                            Value = formatDateTimeForSpl(highTime)
                        };
                    }
                }
            }

            // Products
            if (sectionDto.Products.Any())
            {
                splSection.Subjects = sectionDto.Products.Select(p => new SplSubjectDto
                {
                    ManufacturedProduct = transformProduct(p)
                }).ToList();
            }

            // Identified substances
            if (sectionDto.IdentifiedSubstances.Any())
            {
                if (splSection.Subjects == null)
                    splSection.Subjects = new List<SplSubjectDto>();

                splSection.Subjects.AddRange(sectionDto.IdentifiedSubstances.Select(s => new SplSubjectDto
                {
                    IdentifiedSubstance = transformIdentifiedSubstance(s)
                }));
            }

            // Nested sections using lookup cache
            if (sectionDto.ChildSectionHierarchies.Any())
            {
                splSection.Components = transformNestedSections(sectionDto);
            }

            return splSection;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms observation media including images and multimedia content.
        /// Handles media references within document sections.
        /// </summary>
        /// <param name="observationMedia">Collection of observation media DTOs.</param>
        /// <returns>List of SPL observation media DTOs with media references.</returns>
        /// <remarks>
        /// Processes media files including images, videos, and other multimedia content
        /// maintaining proper media type classification and file references.
        /// </remarks>
        /// <seealso cref="Label.ObservationMedia"/>
        /// <seealso cref="SplObservationMediaDto"/>
        private List<SplObservationMediaDto> transformObservationMedia(List<ObservationMediaDto> observationMedia)
        {
            #region implementation

            return observationMedia.Select(om =>
            {
                var media = om.ObservationMedia;
                return new SplObservationMediaDto
                {
                    ID = media.GetValueOrDefault("observationMediaID")?.ToString(),
                    ClassName = "ED",
                    MediaType = media.GetValueOrDefault("mediaType")?.ToString() ?? string.Empty,
                    Reference = new SplReferenceDto
                    {
                        Value = media.GetValueOrDefault("fileName")?.ToString() ?? string.Empty
                    }
                };
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms section text content including paragraphs, lists, and tables.
        /// Handles rich text content with proper formatting and structure preservation.
        /// </summary>
        /// <param name="textContents">Collection of text content DTOs from the section.</param>
        /// <returns>SPL text DTO containing formatted content elements.</returns>
        /// <remarks>
        /// Processes various content types including paragraphs with style codes,
        /// ordered/unordered lists, complex tables, and multimedia references
        /// maintaining original formatting.
        /// </remarks>
        /// <seealso cref="Label.SectionTextContent"/>
        /// <seealso cref="Label.TextList"/>
        /// <seealso cref="Label.TextTable"/>
        /// <seealso cref="SplTextDto"/>
        private SplTextDto transformSectionText(List<SectionTextContentDto> textContents)
        {
            #region implementation

            var splText = new SplTextDto();

            foreach (var content in textContents.OrderBy(c => c.SectionTextContent.GetValueOrDefault("sequenceNumber")))
            {
                var textContent = content.SectionTextContent;
                var contentType = textContent.GetValueOrDefault("contentType")?.ToString();
                var contentText = textContent.GetValueOrDefault("contentText")?.ToString();

                switch (contentType)
                {
                    case "Paragraph":
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            splText.Paragraphs.Add(new SplParagraphDto
                            {
                                StyleCode = textContent.GetValueOrDefault("styleCode")?.ToString(),
                                Content = contentText
                            });
                        }
                        break;

                    case "List":
                        if (content.TextLists.Any())
                        {
                            splText.Lists.AddRange(transformTextLists(content.TextLists));
                        }
                        break;

                    case "Table":
                        if (content.TextTables.Any())
                        {
                            splText.Tables.AddRange(transformTextTables(content.TextTables));
                        }
                        break;

                    case "RenderMultiMedia":
                        // Handle multimedia references
                        splText.RenderMultiMedia.Add(new SplRenderMultiMediaDto
                        {
                            ReferencedObject = textContent.GetValueOrDefault("referencedObject")?.ToString() ?? string.Empty
                        });
                        break;

                    default:
                        // Handle other content types as generic content
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            splText.Content.Add(new SplContentDto
                            {
                                StyleCode = textContent.GetValueOrDefault("styleCode")?.ToString(),
                                Content = contentText
                            });
                        }
                        break;
                }
            }

            return splText;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms text lists including ordered and unordered lists with items.
        /// Maintains list formatting and item structure for proper display.
        /// </summary>
        /// <param name="textLists">Collection of text list DTOs.</param>
        /// <returns>List of SPL list DTOs with formatted items.</returns>
        /// <remarks>
        /// Handles both ordered and unordered lists with custom style codes
        /// and optional item captions for specialized formatting.
        /// </remarks>
        /// <seealso cref="Label.TextList"/>
        /// <seealso cref="Label.TextListItem"/>
        /// <seealso cref="SplListDto"/>
        private List<SplListDto> transformTextLists(List<TextListDto> textLists)
        {
            #region implementation

            return textLists.Select(listDto =>
            {
                var list = listDto.TextList;
                var splList = new SplListDto
                {
                    ListType = list.GetValueOrDefault("listType")?.ToString(),
                    StyleCode = list.GetValueOrDefault("styleCode")?.ToString()
                };

                // Transform list items
                if (listDto.TextListItems.Any())
                {
                    splList.Items = listDto.TextListItems
                        .OrderBy(i => i.TextListItem.GetValueOrDefault("sequenceNumber"))
                        .Select(itemDto =>
                        {
                            var item = itemDto.TextListItem;
                            return new SplListItemDto
                            {
                                Caption = item.GetValueOrDefault("itemCaption")?.ToString(),
                                Content = item.GetValueOrDefault("itemText")?.ToString() ?? string.Empty
                            };
                        }).ToList();
                }

                return splList;
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms text tables including headers, body rows, and formatting.
        /// Maintains table structure with proper cell spanning and alignment.
        /// </summary>
        /// <param name="textTables">Collection of text table DTOs.</param>
        /// <returns>List of SPL table DTOs with complete structure.</returns>
        /// <remarks>
        /// Processes complex tables with headers, footers, cell spanning,
        /// and various alignment options maintaining tabular data integrity.
        /// </remarks>
        /// <seealso cref="Label.TextTable"/>
        /// <seealso cref="Label.TextTableRow"/>
        /// <seealso cref="Label.TextTableCell"/>
        /// <seealso cref="SplTableDto"/>
        private List<SplTableDto> transformTextTables(List<TextTableDto> textTables)
        {
            #region implementation

            return textTables.Select(tableDto =>
            {
                var table = tableDto.TextTable;
                var splTable = new SplTableDto
                {
                    Width = table.GetValueOrDefault("width")?.ToString(),
                    Summary = table.GetValueOrDefault("summary")?.ToString(),
                    Border = table.GetValueOrDefault("border")?.ToString()
                };

                // Group rows by section type
                var rowsBySection = tableDto.TextTableRows
                    .GroupBy(r => r.TextTableRow.GetValueOrDefault("rowGroupType")?.ToString() ?? "Body")
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Transform header rows
                if (rowsBySection.ContainsKey("Header"))
                {
                    splTable.THead = new SplTableSectionDto
                    {
                        Rows = transformTableRows(rowsBySection["Header"])
                    };
                }

                // Transform body rows
                if (rowsBySection.ContainsKey("Body"))
                {
                    splTable.TBody = new SplTableSectionDto
                    {
                        Rows = transformTableRows(rowsBySection["Body"])
                    };
                }

                // Transform footer rows
                if (rowsBySection.ContainsKey("Footer"))
                {
                    splTable.TFoot = new SplTableSectionDto
                    {
                        Rows = transformTableRows(rowsBySection["Footer"])
                    };
                }

                return splTable;
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms table rows including cells with formatting and spanning attributes.
        /// Maintains row structure and cell properties for proper table rendering.
        /// </summary>
        /// <param name="rows">Collection of table row DTOs.</param>
        /// <returns>List of SPL table row DTOs with formatted cells.</returns>
        /// <remarks>
        /// Processes table rows with their cells including alignment, spanning,
        /// and style attributes for complex table layouts.
        /// </remarks>
        /// <seealso cref="Label.TextTableRow"/>
        /// <seealso cref="Label.TextTableCell"/>
        /// <seealso cref="SplTableRowDto"/>
        private List<SplTableRowDto> transformTableRows(List<TextTableRowDto> rows)
        {
            #region implementation

            return rows.OrderBy(r => r.TextTableRow.GetValueOrDefault("sequenceNumber"))
                .Select(rowDto =>
                {
                    var row = rowDto.TextTableRow;
                    var splRow = new SplTableRowDto
                    {
                        StyleCode = row.GetValueOrDefault("styleCode")?.ToString(),
                        ID = row.GetValueOrDefault("rowID")?.ToString()
                    };

                    // Transform cells
                    if (rowDto.TextTableCells.Any())
                    {
                        splRow.Cells = rowDto.TextTableCells
                            .OrderBy(c => c.TextTableCell.GetValueOrDefault("sequenceNumber"))
                            .Select(cellDto =>
                            {
                                var cell = cellDto.TextTableCell;
                                return new SplTableCellDto
                                {
                                    StyleCode = cell.GetValueOrDefault("styleCode")?.ToString(),
                                    Align = cell.GetValueOrDefault("align")?.ToString(),
                                    VAlign = cell.GetValueOrDefault("vAlign")?.ToString(),
                                    RowSpan = cell.GetValueOrDefault("rowSpan") as int?,
                                    ColSpan = cell.GetValueOrDefault("colSpan") as int?,
                                    Content = cell.GetValueOrDefault("cellText")?.ToString() ?? string.Empty,
                                    ID = cell.GetValueOrDefault("cellID")?.ToString()
                                };
                            }).ToList();
                    }

                    return splRow;
                }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms nested section hierarchies maintaining parent-child relationships.
        /// Handles complex document structures with multiple levels of sections.
        /// Uses the lookup cache to resolve encrypted ID relationships.
        /// </summary>
        /// <param name="parentSection">The parent section containing child hierarchies.</param>
        /// <returns>List of section component DTOs representing nested sections.</returns>
        /// <remarks>
        /// Recursively processes section hierarchies to maintain document structure
        /// with proper nesting of subsections and their content.
        /// Uses decrypted IDs to establish relationships between sections.
        /// </remarks>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <seealso cref="SplSectionComponentDto"/>
        private List<SplSectionComponentDto> transformNestedSections(SectionDto parentSection)
        {
            #region implementation

            var components = new List<SplSectionComponentDto>();

            // Get child sections ordered by sequence
            var childHierarchies = parentSection.ChildSectionHierarchies
                .OrderBy(h => h.SectionHierarchy.GetValueOrDefault("sequenceNumber"))
                .ToList();

            foreach (var hierarchy in childHierarchies)
            {
                var hierarchyData = hierarchy.SectionHierarchy;
                var encryptedChildSectionId = hierarchyData.GetValueOrDefault("encryptedChildSectionID")?.ToString();

                if (!string.IsNullOrEmpty(encryptedChildSectionId))
                {
                    var decryptedChildId = decryptId(encryptedChildSectionId);
                    if (!string.IsNullOrEmpty(decryptedChildId) && _sectionLookup.TryGetValue(decryptedChildId, out var childSection))
                    {
                        components.Add(new SplSectionComponentDto
                        {
                            Section = transformSection(childSection)
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Child section not found for encrypted ID {EncryptedId}", encryptedChildSectionId);
                    }
                }
            }

            return components;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms a product including all components, ingredients, and packaging.
        /// Handles complex product structures with complete regulatory information.
        /// </summary>
        /// <param name="productDto">The product DTO containing product data.</param>
        /// <returns>SPL manufactured product DTO with complete product information.</returns>
        /// <remarks>
        /// Processes product identification, ingredients, packaging levels, marketing information,
        /// and regulatory metadata required for SPL submission.
        /// </remarks>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="SplManufacturedProductDto"/>
        /// <seealso cref="transformIngredient"/>
        /// <seealso cref="transformPackagingLevel"/>
        private SplManufacturedProductDto transformProduct(ProductDto productDto)
        {
            #region implementation

            var product = productDto.Product;
            var splProduct = new SplManufacturedProductDto
            {
                ManufacturedProduct = new SplProductDto()
            };

            var innerProduct = splProduct.ManufacturedProduct;

            // Product codes (NDC)
            if (productDto.ProductIdentifiers.Any())
            {
                innerProduct.Codes = productDto.ProductIdentifiers.Select(id =>
                {
                    var identifier = id.ProductIdentifier;
                    return new SplCodeDto
                    {
                        Code = identifier.GetValueOrDefault("identifierValue")?.ToString() ?? string.Empty,
                        CodeSystem = identifier.GetValueOrDefault("identifierSystemOID")?.ToString() ?? NDC_CODE_SYSTEM
                    };
                }).ToList();
            }

            // Product name and form
            innerProduct.Name = product.GetValueOrDefault("productName")?.ToString();
            innerProduct.Suffix = product.GetValueOrDefault("productSuffix")?.ToString();
            innerProduct.Description = product.GetValueOrDefault("descriptionText")?.ToString();

            var formCode = product.GetValueOrDefault("formCode")?.ToString();
            if (!string.IsNullOrEmpty(formCode))
            {
                innerProduct.FormCode = new SplCodeDto
                {
                    Code = formCode,
                    CodeSystem = product.GetValueOrDefault("formCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                    DisplayName = product.GetValueOrDefault("formDisplayName")?.ToString()
                };
            }

            // Generic medicines
            if (productDto.GenericMedicines.Any())
            {
                innerProduct.AsEntityWithGeneric = productDto.GenericMedicines.Select(gm =>
                {
                    var generic = gm.GenericMedicine;
                    return new SplGenericMedicineDto
                    {
                        GenericMedicine = new SplGenericMedicineContentDto
                        {
                            Names = new List<SplGenericNameDto>
                            {
                                new SplGenericNameDto
                                {
                                    Content = generic.GetValueOrDefault("genericName")?.ToString() ?? string.Empty
                                }
                            }
                        }
                    };
                }).ToList();
            }

            // Specialized kinds
            if (productDto.SpecializedKinds.Any())
            {
                innerProduct.AsSpecializedKind = productDto.SpecializedKinds.Select(sk =>
                {
                    var kind = sk.SpecializedKind;
                    return new SplSpecializedKindDto
                    {
                        ClassCode = SPECIALIZED_KIND_CLASS_GEN,
                        GeneralizedMaterialKind = new SplCodeDto
                        {
                            Code = kind.GetValueOrDefault("kindCode")?.ToString() ?? string.Empty,
                            CodeSystem = kind.GetValueOrDefault("kindCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                            DisplayName = kind.GetValueOrDefault("kindDisplayName")?.ToString()
                        }
                    };
                }).ToList();
            }

            // Ingredients
            if (productDto.Ingredients.Any())
            {
                innerProduct.Ingredients = productDto.Ingredients.Select(i => transformIngredient(i)).ToList();
            }

            // Packaging levels
            if (productDto.PackagingLevels.Any())
            {
                innerProduct.AsContent = productDto.PackagingLevels
                    .OrderBy(p => p.PackagingLevel.GetValueOrDefault("sequenceNumber"))
                    .Select(p => transformPackagingLevel(p)).ToList();
            }

            // Marketing information
            if (productDto.MarketingCategories.Any() || productDto.MarketingStatuses.Any())
            {
                splProduct.SubjectOf = transformProductMarketingInfo(productDto);
            }

            // Product characteristics (color, shape, size, score, imprint, flavor)
            if (productDto.ProductCharacteristics.Any())
            {
                if (splProduct.SubjectOf == null)
                    splProduct.SubjectOf = new List<SplProductSubjectOfDto>();

                splProduct.SubjectOf.AddRange(transformProductCharacteristics(productDto.ProductCharacteristics));
            }

            // Route of administration
            if (productDto.Routes.Any())
            {
                splProduct.ConsumedIn = productDto.Routes.Select(r => new SplConsumedInDto
                {
                    SubstanceAdministration = new SplSubstanceAdministrationDto
                    {
                        RouteCode = new SplCodeDto
                        {
                            Code = r.Route.GetValueOrDefault("routeCode")?.ToString() ?? string.Empty,
                            CodeSystem = r.Route.GetValueOrDefault("routeCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                            DisplayName = r.Route.GetValueOrDefault("routeDisplayName")?.ToString()
                        }
                    }
                }).ToList();
            }

            return splProduct;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms ingredient information including substance details and strengths.
        /// Handles active and inactive ingredients with proper classification.
        /// </summary>
        /// <param name="ingredientDto">The ingredient DTO containing component data.</param>
        /// <returns>SPL ingredient DTO with substance and quantity information.</returns>
        /// <remarks>
        /// Processes ingredient classification, strength specifications with numerator/denominator,
        /// substance identification with UNII codes, and active moieties.
        /// </remarks>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <seealso cref="SplIngredientDto"/>
        private SplIngredientDto transformIngredient(IngredientDto ingredientDto)
        {
            #region implementation

            var ingredient = ingredientDto.Ingredient;
            var splIngredient = new SplIngredientDto
            {
                ClassCode = ingredient.GetValueOrDefault("classCode")?.ToString() ?? INGREDIENT_CLASS_IACT,
                SequenceNumber = ingredient.GetValueOrDefault("sequenceNumber") as int?,
                IsConfidential = ingredient.GetValueOrDefault("isConfidential") as bool? ?? false
            };

            // Quantity (strength)
            var numeratorValue = ingredient.GetValueOrDefault("quantityNumerator");
            var numeratorUnit = ingredient.GetValueOrDefault("quantityNumeratorUnit")?.ToString();

            if (numeratorValue != null && !string.IsNullOrEmpty(numeratorUnit))
            {
                splIngredient.Quantity = new SplQuantityDto
                {
                    Numerator = new SplQuantityValueDto
                    {
                        Value = numeratorValue.ToString() ?? "0",
                        Unit = numeratorUnit
                    }
                };

                // Add denominator if present
                var denominatorValue = ingredient.GetValueOrDefault("quantityDenominator");
                var denominatorUnit = ingredient.GetValueOrDefault("quantityDenominatorUnit")?.ToString();

                if (denominatorValue != null && !string.IsNullOrEmpty(denominatorUnit))
                {
                    splIngredient.Quantity.Denominator = new SplQuantityValueDto
                    {
                        Value = denominatorValue.ToString() ?? "1",
                        Unit = denominatorUnit
                    };
                }
            }

            // Ingredient substance
            if (ingredientDto.IngredientSubstance != null)
            {
                var substance = ingredientDto.IngredientSubstance.IngredientSubstance;
                splIngredient.IngredientSubstance = new SplIngredientSubstanceDto
                {
                    Name = substance.GetValueOrDefault("substanceName")?.ToString() ?? string.Empty
                };

                // UNII code
                var unii = substance.GetValueOrDefault("unii")?.ToString();
                if (!string.IsNullOrEmpty(unii))
                {
                    splIngredient.IngredientSubstance.Code = new SplCodeDto
                    {
                        Code = unii,
                        CodeSystem = FDA_SRS_CODE_SYSTEM,
                        CodeSystemName = FDA_SRS_CODE_SYSTEM_NAME
                    };
                }

                // Active moieties
                if (ingredientDto.IngredientSubstance.ActiveMoieties.Any())
                {
                    splIngredient.IngredientSubstance.ActiveMoieties = ingredientDto.IngredientSubstance.ActiveMoieties
                        .Select(am =>
                        {
                            var moiety = am.ActiveMoiety;
                            return new SplActiveMoietyDto
                            {
                                ActiveMoiety = new SplActiveMoietyContentDto
                                {
                                    Code = new SplCodeDto
                                    {
                                        Code = moiety.GetValueOrDefault("moietyUNII")?.ToString() ?? string.Empty,
                                        CodeSystem = FDA_SRS_CODE_SYSTEM
                                    },
                                    Name = moiety.GetValueOrDefault("moietyName")?.ToString() ?? string.Empty
                                }
                            };
                        }).ToList();
                }
            }

            // Specified substances
            if (ingredientDto.SpecifiedSubstances.Any())
            {
                splIngredient.SpecifiedSubstances = ingredientDto.SpecifiedSubstances.Select(ss =>
                {
                    var specSubstance = ss.SpecifiedSubstance;
                    return new SplSpecifiedSubstanceDto
                    {
                        Code = new SplCodeDto
                        {
                            Code = specSubstance.GetValueOrDefault("substanceCode")?.ToString() ?? string.Empty,
                            CodeSystem = specSubstance.GetValueOrDefault("substanceCodeSystem")?.ToString() ?? FDA_SRS_CODE_SYSTEM,
                            CodeSystemName = specSubstance.GetValueOrDefault("substanceCodeSystemName")?.ToString()
                        }
                    };
                }).ToList();
            }

            return splIngredient;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms packaging level information including container types and quantities.
        /// Handles hierarchical packaging structures from unit dose to shipping containers.
        /// </summary>
        /// <param name="packagingDto">The packaging level DTO containing container data.</param>
        /// <returns>SPL as-content DTO with packaging specifications.</returns>
        /// <remarks>
        /// Processes packaging quantities, container forms, package identifiers (NDC),
        /// and nested packaging hierarchies for complex product configurations.
        /// </remarks>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="SplAsContentDto"/>
        private SplAsContentDto transformPackagingLevel(PackagingLevelDto packagingDto)
        {
            #region implementation

            var packaging = packagingDto.PackagingLevel;
            var splContent = new SplAsContentDto
            {
                Quantity = new SplQuantityDto
                {
                    Numerator = new SplQuantityValueDto
                    {
                        Value = packaging.GetValueOrDefault("quantityNumerator")?.ToString() ?? "1",
                        Unit = packaging.GetValueOrDefault("quantityNumeratorUnit")?.ToString() ?? "1"
                    },
                    Denominator = new SplQuantityValueDto
                    {
                        Value = packaging.GetValueOrDefault("quantityDenominator")?.ToString() ?? "1",
                        Unit = "1"
                    }
                },
                ContainerPackagedProduct = new SplContainerPackagedProductDto
                {
                    FormCode = new SplCodeDto
                    {
                        Code = packaging.GetValueOrDefault("packageFormCode")?.ToString() ?? string.Empty,
                        CodeSystem = packaging.GetValueOrDefault("packageFormCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                        DisplayName = packaging.GetValueOrDefault("packageFormDisplayName")?.ToString()
                    }
                }
            };

            // Package identifier (NDC)
            var packageCode = packaging.GetValueOrDefault("packageCode")?.ToString();
            if (!string.IsNullOrEmpty(packageCode))
            {
                splContent.ContainerPackagedProduct.Code = new SplCodeDto
                {
                    Code = packageCode,
                    CodeSystem = NDC_CODE_SYSTEM,
                    CodeSystemName = NDC_CODE_SYSTEM_NAME
                };
            }

            // Marketing status for this package
            if (packagingDto.MarketingStatuses.Any())
            {
                splContent.ContainerPackagedProduct.SubjectOf = packagingDto.MarketingStatuses
                    .Select(ms => new SplPackageSubjectOfDto
                    {
                        MarketingAct = transformMarketingStatus(ms)
                    }).ToList();
            }

            return splContent;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms product marketing information including categories and statuses.
        /// Handles regulatory classification and market availability data.
        /// </summary>
        /// <param name="productDto">The product DTO containing marketing information.</param>
        /// <returns>List of subject-of DTOs with marketing metadata.</returns>
        /// <remarks>
        /// Processes marketing categories (NDA, ANDA, OTC), application numbers,
        /// approval dates, and current marketing statuses for regulatory compliance.
        /// </remarks>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="SplProductSubjectOfDto"/>
        private List<SplProductSubjectOfDto> transformProductMarketingInfo(ProductDto productDto)
        {
            #region implementation

            var subjectOf = new List<SplProductSubjectOfDto>();

            // Marketing categories
            foreach (var catDto in productDto.MarketingCategories)
            {
                var category = catDto.MarketingCategory;
                var subject = new SplProductSubjectOfDto
                {
                    Approval = new SplMarketingApprovalDto
                    {
                        Id = new SplIdentifierDto
                        {
                            Root = category.GetValueOrDefault("applicationOrMonographIDSystemOID")?.ToString() ?? string.Empty,
                            Extension = category.GetValueOrDefault("applicationOrMonographIDValue")?.ToString()
                        },
                        Code = new SplCodeDto
                        {
                            Code = category.GetValueOrDefault("categoryCode")?.ToString() ?? string.Empty,
                            CodeSystem = category.GetValueOrDefault("categoryCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                            DisplayName = category.GetValueOrDefault("categoryDisplayName")?.ToString()
                        },
                        Author = new SplMarketingAuthorDto
                        {
                            TerritorialAuthority = new SplTerritorialAuthorityDto
                            {
                                Territory = new SplTerritoryDto
                                {
                                    Code = new SplCodeDto
                                    {
                                        Code = category.GetValueOrDefault("territoryCode")?.ToString() ?? DEFAULT_TERRITORY_CODE,
                                        CodeSystem = TERRITORY_CODE_SYSTEM
                                    }
                                }
                            }
                        }
                    }
                };

                // Approval date
                var approvalDate = category.GetValueOrDefault("approvalDate")?.ToString();
                if (!string.IsNullOrEmpty(approvalDate))
                {
                    subject.Approval.EffectiveTime = new SplEffectiveTimeDto
                    {
                        Value = formatDateTimeForSpl(approvalDate)
                    };
                }

                subjectOf.Add(subject);
            }

            // Marketing statuses
            foreach (var statusDto in productDto.MarketingStatuses)
            {
                subjectOf.Add(new SplProductSubjectOfDto
                {
                    MarketingAct = transformMarketingStatus(statusDto)
                });
            }

            return subjectOf;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms product characteristics including color, shape, size, score, imprint, and flavor.
        /// Handles physical and visual product attributes as seen in drug label templates.
        /// </summary>
        /// <param name="characteristics">Collection of product characteristic DTOs.</param>
        /// <returns>List of subject-of DTOs with characteristic information.</returns>
        /// <remarks>
        /// Processes physical product attributes like color, shape, size, scoring,
        /// imprint text, and flavor information for drug products.
        /// </remarks>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="SplProductSubjectOfDto"/>
        private List<SplProductSubjectOfDto> transformProductCharacteristics(List<ProductCharacteristicDto> characteristics)
        {
            #region implementation

            return characteristics.Select(charDto =>
            {
                var characteristic = charDto.ProductCharacteristic;
                var characteristicType = characteristic.GetValueOrDefault("characteristicType")?.ToString();

                var subject = new SplProductSubjectOfDto
                {
                    Characteristic = new SplCharacteristicDto
                    {
                        Code = new SplCodeDto
                        {
                            Code = getCharacteristicCode(characteristicType),
                            CodeSystem = "2.16.840.1.113883.1.11.19255"
                        }
                    }
                };

                // Set the appropriate value based on characteristic type
                var value = characteristic.GetValueOrDefault("characteristicValue")?.ToString();
                var valueCode = characteristic.GetValueOrDefault("characteristicValueCode")?.ToString();
                var unit = characteristic.GetValueOrDefault("characteristicUnit")?.ToString();

                switch (characteristicType?.ToUpper())
                {
                    case "COLOR":
                    case "SHAPE":
                    case "FLAVOR":
                        subject.Characteristic.Value = new SplCharacteristicValueDto
                        {
                            Code = valueCode ?? string.Empty,
                            CodeSystem = FDA_FORM_CODE_SYSTEM,
                            DisplayName = value,
                            XsiType = "CE"
                        };
                        break;

                    case "SIZE":
                        subject.Characteristic.Value = new SplCharacteristicValueDto
                        {
                            Value = value,
                            Unit = unit ?? "mm",
                            XsiType = "PQ"
                        };
                        break;

                    case "SCORE":
                        subject.Characteristic.Value = new SplCharacteristicValueDto
                        {
                            Value = value,
                            XsiType = "INT"
                        };
                        break;

                    case "IMPRINT":
                        subject.Characteristic.Value = new SplCharacteristicValueDto
                        {
                            Value = value,
                            XsiType = "ST"
                        };
                        break;
                }

                return subject;
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the appropriate SPL characteristic code for a given characteristic type.
        /// </summary>
        /// <param name="characteristicType">The type of characteristic (color, shape, etc.).</param>
        /// <returns>The corresponding SPL characteristic code.</returns>
        private string getCharacteristicCode(string? characteristicType)
        {
            return characteristicType?.ToUpper() switch
            {
                "COLOR" => "SPLCOLOR",
                "SHAPE" => "SPLSHAPE",
                "SIZE" => "SPLSIZE",
                "SCORE" => "SPLSCORE",
                "IMPRINT" => "SPLIMPRINT",
                "FLAVOR" => "SPLFLAVOR",
                _ => "SPLOTHER"
            };
        }
        /// Handles product market availability and lifecycle status.
        /// </summary>
        /// <param name="marketingStatusDto">The marketing status DTO.</param>
        /// <returns>SPL marketing act DTO with status information.</returns>
        /// <remarks>
        /// Processes marketing activity codes, status codes (active, completed),
        /// and effective date ranges for market presence tracking.
        /// </remarks>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="SplMarketingActDto"/>
        private SplMarketingActDto transformMarketingStatus(MarketingStatusDto marketingStatusDto)
        {
            #region implementation

            var status = marketingStatusDto.MarketingStatus;
            var splAct = new SplMarketingActDto
            {
                Code = new SplCodeDto
                {
                    Code = status.GetValueOrDefault("marketingActCode")?.ToString() ?? DEFAULT_MARKETING_ACT_CODE,
                    CodeSystem = FDA_FORM_CODE_SYSTEM
                },
                StatusCode = new SplStatusCodeDto
                {
                    Code = status.GetValueOrDefault("statusCode")?.ToString() ?? DEFAULT_STATUS_CODE
                },
                EffectiveTime = new SplEffectiveTimeDto()
            };

            // Effective time range
            var startDate = status.GetValueOrDefault("effectiveStartDate")?.ToString();
            var endDate = status.GetValueOrDefault("effectiveEndDate")?.ToString();

            if (!string.IsNullOrEmpty(startDate))
            {
                splAct.EffectiveTime.Low = new SplTimeValueDto
                {
                    Value = formatDateTimeForSpl(startDate)
                };
            }

            if (!string.IsNullOrEmpty(endDate))
            {
                splAct.EffectiveTime.High = new SplTimeValueDto
                {
                    Value = formatDateTimeForSpl(endDate)
                };
            }

            return splAct;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms identified substance information for substance indexing documents.
        /// Handles substance classification and pharmacologic class relationships.
        /// </summary>
        /// <param name="substanceDto">The identified substance DTO.</param>
        /// <returns>SPL identified substance DTO with classification data.</returns>
        /// <remarks>
        /// Processes substance identifiers (UNII), names, and pharmacologic
        /// class associations for substance indexing and classification.
        /// </remarks>
        /// <seealso cref="Label.IdentifiedSubstance"/>
        /// <seealso cref="Label.PharmacologicClass"/>
        /// <seealso cref="SplIdentifiedSubstanceDto"/>
        private SplIdentifiedSubstanceDto transformIdentifiedSubstance(IdentifiedSubstanceDto substanceDto)
        {
            #region implementation

            var substance = substanceDto.IdentifiedSubstance;
            var splSubstance = new SplIdentifiedSubstanceDto
            {
                Code = new SplCodeDto
                {
                    Code = substance.GetValueOrDefault("substanceIdentifierValue")?.ToString() ?? string.Empty,
                    CodeSystem = substance.GetValueOrDefault("substanceIdentifierSystemOID")?.ToString() ?? FDA_SRS_CODE_SYSTEM
                },
                Names = new List<SplGenericNameDto>
                {
                    new SplGenericNameDto
                    {
                        Content = substance.GetValueOrDefault("substanceName")?.ToString() ?? string.Empty
                    }
                }
            };

            // Pharmacologic classes
            if (substanceDto.PharmacologicClasses.Any())
            {
                splSubstance.AsSpecializedKind = substanceDto.PharmacologicClasses
                    .SelectMany(pc => pc.PharmacologicClassLinks)
                    .Select(link =>
                    {
                        var classLink = link.PharmacologicClassLink;
                        return new SplSpecializedKindDto
                        {
                            ClassCode = SPECIALIZED_KIND_CLASS_GEN,
                            GeneralizedMaterialKind = new SplCodeDto
                            {
                                Code = classLink.GetValueOrDefault("pharmacologicClassCode")?.ToString() ?? string.Empty,
                                CodeSystem = classLink.GetValueOrDefault("pharmacologicClassCodeSystem")?.ToString() ?? FDA_FORM_CODE_SYSTEM,
                                DisplayName = classLink.GetValueOrDefault("pharmacologicClassDisplayName")?.ToString()
                            }
                        };
                    }).ToList();
            }

            return splSubstance;

            #endregion
        }

        #endregion

        #region utility methods

        /**************************************************************/
        /// <summary>
        /// Formats a date/time string into SPL-compliant HL7 timestamp format.
        /// Converts various date formats to YYYYMMDD or YYYYMMDDHHMMSS format.
        /// </summary>
        /// <param name="dateTimeString">The date/time string to format.</param>
        /// <returns>HL7-formatted timestamp string, or original value if parsing fails.</returns>
        /// <remarks>
        /// SPL requires dates in HL7 format without separators. This method handles
        /// conversion from standard DateTime formats to the required format.
        /// </remarks>
        /// <example>
        /// <code>
        /// string hl7Date = formatDateTimeForSpl("2006-08-30");
        /// // Returns "20060830"
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document.EffectiveTime"/>
        private string formatDateTimeForSpl(string? dateTimeString)
        {
            #region implementation

            if (string.IsNullOrEmpty(dateTimeString))
                return string.Empty;

            if (DateTime.TryParse(dateTimeString, out DateTime dt))
            {
                // Format as YYYYMMDD for dates or YYYYMMDDHHMMSS for date/times
                if (dt.TimeOfDay == TimeSpan.Zero)
                    return dt.ToString("yyyyMMdd");
                else
                    return dt.ToString("yyyyMMddHHmmss");
            }

            // Return original if can't parse
            return dateTimeString;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Serializes an SPL document DTO to properly formatted XML string.
        /// Applies required namespaces, formatting, and XML processing instructions.
        /// </summary>
        /// <param name="splDocument">The SPL document DTO to serialize.</param>
        /// <returns>Formatted XML string ready for file output or transmission.</returns>
        /// <remarks>
        /// Generates XML with proper HL7 v3 namespace declarations, UTF-8 encoding,
        /// schema location, and stylesheet reference for FDA SPL rendering.
        /// </remarks>
        /// <seealso cref="SplDocumentDto"/>
        /// <seealso cref="XmlSerializer"/>
        private string serializeToSplXml(SplDocumentDto splDocument)
        {
            #region implementation

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            // Add proper namespace handling for xsi:type
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", c.XML_NAMESPACE);
            namespaces.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            // Create serializer with proper namespace handling
            var serializer = new XmlSerializer(typeof(SplDocumentDto), c.XML_NAMESPACE);

            using (var stringWriter = new StringWriter())
            {
                // Write XML declaration and stylesheet reference matching FDA templates
                stringWriter.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                stringWriter.WriteLine("<?xml-stylesheet href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\" type=\"text/xsl\"?>");

                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    // Write document element with schema location
                    xmlWriter.WriteStartElement("document", c.XML_NAMESPACE);
                    xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                    xmlWriter.WriteAttributeString("xsi", "schemaLocation", "http://www.w3.org/2001/XMLSchema-instance",
                        "urn:hl7-org:v3 https://www.accessdata.fda.gov/spl/schema/spl.xsd");

                    // Serialize with proper namespaces
                    try
                    {
                        // Create a temporary serializer for just the content
                        var tempDoc = new XmlDocument();
                        using (var tempStream = new MemoryStream())
                        {
                            serializer.Serialize(tempStream, splDocument, namespaces);
                            tempStream.Position = 0;
                            tempDoc.Load(tempStream);
                        }

                        // Write only the inner content (child elements) of the document root
                        if (tempDoc.DocumentElement != null)
                        {
                            foreach (XmlNode child in tempDoc.DocumentElement.ChildNodes)
                            {
                                child.WriteTo(xmlWriter);
                            }
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("xsi:type"))
                    {
                        _logger.LogError(ex, "XML Serialization error with xsi:type attribute");
                        throw new InvalidOperationException("Failed to serialize SPL document due to xsi:type attribute issue. Check SplCharacteristicValueDto XML attributes.", ex);
                    }

                    xmlWriter.WriteEndElement(); // document
                }

                return stringWriter.ToString();
               
            }

            #endregion

        }

        /**************************************************************/
        /// <summary>
        /// Checks if a string value is meaningful for inclusion in SPL output.
        /// Helps avoid outputting empty or placeholder elements.
        /// </summary>
        /// <param name="value">The string value to check.</param>
        /// <returns>True if the value should be included in output, false otherwise.</returns>
        private bool hasMeaningfulValue(string? value)
        {
            return !string.IsNullOrEmpty(value) && !string.IsNullOrWhiteSpace(value);
        }

        /**************************************************************/
        /// <summary>
        /// Safely gets a string value from a dictionary, returning null if empty or whitespace.
        /// Helps ensure only meaningful values are used in SPL output.
        /// </summary>
        /// <param name="dict">The dictionary to get the value from.</param>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value if meaningful, null otherwise.</returns>
        private string? getMeaningfulValue(Dictionary<string, object?> dict, string key)
        {
            var value = dict.GetValueOrDefault(key)?.ToString();
            return hasMeaningfulValue(value) ? value : null;
        }

        #endregion

        #endregion
    }
}