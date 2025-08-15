using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Main orchestrator for parsing SPL section elements and coordinating specialized parsers.
    /// This refactored parser delegates specific responsibilities to focused parsers while
    /// maintaining the core section creation and orchestration logic.
    /// </summary>
    /// <remarks>
    /// This parser serves as the main entry point for section parsing operations,
    /// coordinating the work of specialized parsers for content, media, indexing,
    /// and hierarchy processing. It maintains backward compatibility with existing
    /// interfaces while providing a cleaner, more maintainable architecture.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SectionContentParser"/>
    /// <seealso cref="SectionMediaParser"/>
    /// <seealso cref="SectionIndexingParser"/>
    /// <seealso cref="SectionHierarchyParser"/>
    /// <seealso cref="Section"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionParser : ISplSectionParser
    {
        #region Private Fields
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Specialized parser for handling text content, lists, tables, and excerpts.
        /// </summary>
        private readonly SectionContentParser _contentParser;

        /// <summary>
        /// Specialized parser for handling indexing operations and cross-references.
        /// </summary>
        private readonly SectionIndexingParser _indexingParser;

        /// <summary>
        /// Specialized parser for handling section hierarchies and child relationships.
        /// </summary>
        private readonly SectionHierarchyParser _hierarchyParser;

        /// <summary>
        /// Specialized parser for handling multimedia content and media references.
        /// </summary>
        private readonly SectionMediaParser _mediaParser;

        /// <summary>
        /// Specialized parser for handling tolerance specifications and observation criteria.
        /// </summary>
        private readonly ToleranceSpecificationParser _toleranceParser;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionParser with specialized parsers injected.
        /// </summary>
        /// <param name="contentParser">Parser for text content, lists, tables, and excerpts.</param>
        /// <param name="indexingParser">Parser for indexing operations and cross-references.</param>
        /// <param name="hierarchyParser">Parser for section hierarchies and child relationships.</param>
        /// <param name="mediaParser">Parser for multimedia content and media references.</param>
        /// <param name="toleranceParser">Parser for tolerance specifications and observation criteria.</param>
        public SectionParser(
            SectionContentParser? contentParser = null,
            SectionIndexingParser? indexingParser = null,
            SectionHierarchyParser? hierarchyParser = null,
            SectionMediaParser? mediaParser = null,
            ToleranceSpecificationParser? toleranceParser = null)
        {
            _contentParser = contentParser ?? new SectionContentParser();
            _indexingParser = indexingParser ?? new SectionIndexingParser();
            _hierarchyParser = hierarchyParser ?? new SectionHierarchyParser();
            _mediaParser = mediaParser ?? new SectionMediaParser();
            _toleranceParser = toleranceParser ?? new ToleranceSpecificationParser();
        }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing the main section element.
        /// </summary>
        public string SectionName => "section";

        /**************************************************************/
        /// <summary>
        /// Parses a section element from an SPL document, creating the section entity
        /// and orchestrating specialized parsers for different aspects of section processing.
        /// </summary>
        /// <param name="xEl">The XElement representing the section element to parse.</param>
        /// <param name="context">The current parsing context containing the structuredBody to link sections to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new SectionParser(contentParser, indexingParser, hierarchyParser, mediaParser);
        /// var result = await parser.ParseAsync(sectionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Sections created: {result.SectionsCreated}");
        ///     Console.WriteLine($"Content attributes: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method orchestrates the following operations:
        /// 1. Validates the parsing context and creates the core Section entity
        /// 2. Delegates content processing to SectionContentParser
        /// 3. Delegates hierarchy processing to SectionHierarchyParser
        /// 4. Delegates media processing to SectionMediaParser
        /// 5. Delegates indexing processing to SectionIndexingParser
        /// 6. Handles product parsing and REMS protocol detection
        /// 7. Aggregates results from all specialized parsers
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="REMSParser"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement xEl,
            SplParseContext context,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate parsing context to ensure all required dependencies are available
            if (!validateContext(context, result))
            {
                return result;
            }

            try
            {
                if (xEl == null)
                {
                    result.Success = false;
                    result.Errors.Add("Invalid section element provided for parsing.");
                    return result;
                }

                // Report parsing start for monitoring and debugging purposes
                reportProgress?.Invoke($"Starting Section parsing for " +
                    $"{xEl?.GetSplElement(sc.E.Title)?.Value?.Replace("\t", " ") ?? xEl?.Name.LocalName ?? "Undefined"}, " +
                    $"file: {context.FileNameInZip}");

                // 1. Create the core Section entity from the XML element
                // Parse section metadata and persist the primary section entity
                var section = await createAndSaveSectionAsync(xEl, context);
                if (section?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to create and save the Section entity.");
                    return result;
                }
                result.SectionsCreated++;

                // 2. Set context for child parsers and ensure it's restored
                // Manage parsing context state to provide section context to child parsers
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                try
                {
                    // Before parsing products, check if this section requires a DocumentRelationship context.
                    var docRelResult = await parseDocumentRelationshipAsync(xEl, context, reportProgress);
                    result.MergeFrom(docRelResult);

                    // Related docs for index files
                    var docLevelRelatedDocResult = await parseDocumentLevelRelatedDocumentsAsync(context);
                    result.MergeFrom(docLevelRelatedDocResult);

                    // 3. Delegate to specialized parsers for different aspects of section processing

                    // Parse the content within this section (text, highlights, etc.)
                    var contentResult = await _contentParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(contentResult);

                    // Parse section hierarchies and child sections
                    var hierarchyResult = await _hierarchyParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(hierarchyResult);

                    // Parse media elements (observation media, rendered media)
                    var mediaResult = await _mediaParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(mediaResult);

                    // Parse indexing information (pharmacologic classes, billing units, etc.)
                    var indexingResult = await _indexingParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(indexingResult);

                    // Parse tolerance specifications and observation criteria for 40 CFR 180 documents
                    // Check if this section contains tolerance specification elements
                    if (containsToleranceSpecifications(xEl))
                    {
                        var toleranceResult = await _toleranceParser.ParseAsync(xEl, context, reportProgress);
                        result.MergeFrom(toleranceResult);
                    }


                    // Parse warning letter information if this is a warning letter alert section 
                    var warningLetterResult = await parseWarningLetterContentAsync(xEl, context, reportProgress);
                    result.MergeFrom(warningLetterResult);

                    // Parse compliance actions for this section
                    var complianceResult = await parseComplianceActionsAsync(xEl, context, reportProgress);
                    result.MergeFrom(complianceResult);

                    // Parse certification links if this is a certification section
                    if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
                    {
                        var certificationResult = await parseCertificationLinksAsync(xEl, context, reportProgress);
                        result.MergeFrom(certificationResult);
                    }

                    // 4. Parse the associated manufactured product, if it exists
                    // Process product information contained within the section
                    var productResult = await parseManufacturedProductAsync(xEl, context, reportProgress);
                    result.MergeFrom(productResult);

                    // 5. Parse REMS protocols if applicable
                    // Check if this section contains REMS protocol elements
                    if (containsRemsProtocols(xEl))
                    {
                        var remsParser = new REMSParser();
                        var remsResult = await remsParser.ParseAsync(xEl, context, reportProgress);
                        result.MergeFrom(remsResult);
                    }
                }
                finally
                {
                    // Restore the context to prevent side effects for sibling or parent parsers
                    context.CurrentSection = oldSection;
                }

                // Report parsing completion for monitoring purposes
                reportProgress?.Invoke($"Section completed: {result.SectionAttributesCreated} attributes, " +
                    $"{result.SectionsCreated} sections for {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle unexpected errors and log them for debugging
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing <section> element for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        #region Core Section Processing Methods

        /**************************************************************/
        /// <summary>
        /// Parses Warning Letter Alert content if the current section is a warning letter section (48779-3).
        /// Delegates to the specialized WarningLetterParser for processing product and date information.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse for warning letter content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the results from warning letter parsing operations.</returns>
        /// <example>
        /// <code>
        /// var result = await parseWarningLetterContentAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Warning letter elements created: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method checks if the current section is a Warning Letter Alert section (48779-3)
        /// and delegates processing to the specialized WarningLetterParser. If the section is not
        /// a warning letter section, it returns a successful result without processing.
        /// </remarks>
        /// <seealso cref="WarningLetterParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseWarningLetterContentAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            try
            {
                // Delegate to specialized warning letter parser
                var warningLetterParser = new WarningLetterParser();
                return await warningLetterParser.ParseAsync(sectionEl, context, reportProgress);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during warning letter parsing
                var result = new SplParseResult
                {
                    Success = false
                };
                result.Errors.Add($"Error parsing warning letter content: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing warning letter content for section in {FileName}", context.FileNameInZip);
                return result;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Conditionally parses a DocumentRelationship if the section requires it (e.g., for certifications).
        /// </summary>
        /// <param name="sectionEl">The XElement of the section.</param>
        /// <param name="context">The current parsing context. This will be populated with the CurrentDocumentRelationship.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>The SplParseResult from the relationship parser.</returns>
        /// <seealso cref="DocumentRelationshipParser"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseDocumentRelationshipAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // The section code "BNCC" is an example for "Blanket No Changes Certification".
            if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
            {
                var subjectEl = sectionEl.SplElement(sc.E.Subject);
                if (subjectEl != null)
                {
                    var relationshipParser = new DocumentRelationshipParser();
                    return await relationshipParser.ParseAsync(subjectEl, context, reportProgress);
                }
            }
            return new SplParseResult { Success = true };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses document-level relatedDocument elements if they haven't been processed yet.
        /// This method processes XML relatedDocument elements at the document level, extracts relationship
        /// information, and creates RelatedDocument entities in the database. It includes duplicate
        /// prevention logic to avoid reprocessing already handled documents.
        /// </summary>
        /// <param name="context">The parsing context containing document information and services</param>
        /// <returns>A SplParseResult indicating success/failure and containing processing statistics</returns>
        /// <remarks>
        /// This method is designed to handle document-level related document parsing that may have
        /// been missed in previous processing steps. The method checks for existing related documents
        /// before processing to prevent duplicates.
        /// 
        /// The method expects the XML structure to contain relatedDocument elements with typeCode attributes
        /// and nested relatedDocument elements containing setId elements with root attributes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var context = new SplParseContext 
        /// { 
        ///     Document = document, 
        ///     ServiceProvider = serviceProvider,
        ///     DocumentElement = xmlElement 
        /// };
        /// var result = await parseDocumentLevelRelatedDocumentsAsync(context);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Created {result.ProductElementsCreated} related documents");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="RelatedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseDocumentLevelRelatedDocumentsAsync(SplParseContext context)
        {
            #region implementation

            // Initialize result object to track processing outcome
            var result = new SplParseResult();

            try
            {
                #region duplicate prevention check

                // Check if we've already processed related documents for this document
                // This prevents duplicate processing and maintains data integrity
                if (context.ServiceProvider != null && context.Document?.DocumentID != null)
                {
                    // Get database context from service provider for data access
                    var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Query existing related documents count for this source document
                    var existingCount = await dbContext.Set<RelatedDocument>()
                        .CountAsync(rd => rd.SourceDocumentID == context.Document.DocumentID);

                    if (existingCount > 0)
                    {
                        // Already processed, skip to avoid duplicates
                        return new SplParseResult { Success = true };
                    }
                }

                #endregion

                #region xml document validation

                // Use the stored document root element from context
                var documentEl = context.DocumentElement;
                if (documentEl == null)
                {
                    // No document element available, return success as this is not an error condition
                    return new SplParseResult { Success = true };
                }

                #endregion

                #region related document processing

                // Use XmlHelpers and constants to find relatedDocument elements at document level
                var relatedDocElements = documentEl.Elements(ns + sc.E.RelatedDocument);

                // Process each related document element found in the XML
                foreach (var relatedDocEl in relatedDocElements)
                {
                    #region extract relationship data

                    // Extract the type code that defines the relationship type
                    var typeCode = relatedDocEl.GetAttrVal(sc.A.TypeCode);

                    // Get the nested relatedDocument element containing reference information
                    var innerRelatedDocEl = relatedDocEl.GetSplElement(sc.E.RelatedDocument);

                    // Extract the setId root attribute which contains the referenced document GUID
                    var setIdRoot = innerRelatedDocEl?.GetSplElementAttrVal(sc.E.SetId, sc.A.Root);

                    #endregion

                    #region create related document entity

                    // Only create entity if we have valid setId root data
                    if (!string.IsNullOrEmpty(setIdRoot))
                    {
                        // Create new RelatedDocument entity with extracted data
                        var relatedDoc = new RelatedDocument
                        {
                            SourceDocumentID = context.Document?.DocumentID,
                            RelationshipTypeCode = typeCode,
                            ReferencedSetGUID = Util.ParseNullableGuid(setIdRoot)
                        };

                        // Get repository instance and persist the related document
                        var relatedDocRepo = context.GetRepository<RelatedDocument>();
                        await relatedDocRepo.CreateAsync(relatedDoc);

                        // Increment counter to track created elements
                        result.ProductElementsCreated++;
                    }

                    #endregion
                }

                #endregion
            }
            catch (Exception ex)
            {
                #region error handling

                // Set failure state and capture error information
                result.Success = false;
                result.Errors.Add($"Error parsing document-level related documents: {ex.Message}");

                // Log error with context information for debugging
                context?.Logger?.LogError(ex, "Error parsing document-level related documents for {FileName}", context.FileNameInZip);

                #endregion
            }

            // Return processing result with success status and statistics
            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a section contains tolerance specification elements that should be parsed.
        /// </summary>
        /// <param name="sectionEl">The section XElement to check.</param>
        /// <returns>True if the section contains tolerance specifications, false otherwise.</returns>
        /// <seealso cref="ToleranceSpecificationParser"/>
        /// <seealso cref="Label"/>
        private static bool containsToleranceSpecifications(XElement sectionEl)
        {
            #region implementation
            // Check for tolerance-specific elements that indicate this section should be processed by ToleranceSpecificationParser
            // Look for 40-CFR- prefix codes in substance specifications
            var substanceSpecElements = sectionEl.SplElements(sc.E.Subject, sc.E.IdentifiedSubstance, sc.E.SubjectOf, sc.E.SubstanceSpecification);

            foreach (var specEl in substanceSpecElements)
            {
                var codeEl = specEl.GetSplElement(sc.E.Code);
                var specCode = codeEl?.GetAttrVal(sc.A.CodeValue);

                // Check for 40-CFR- prefix as specified in SPL IG 19.2.3.8
                if (!string.IsNullOrEmpty(specCode) && specCode.StartsWith("40-CFR-", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Also check for observation criteria with tolerance ranges
            return sectionEl.SplElements(sc.E.ReferenceRange, sc.E.ObservationCriterion).Any();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context to ensure it's properly initialized.
        /// Checks for required dependencies and structured body context.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="result">The result object to add errors to if validation fails.</param>
        /// <returns>True if the context is valid; otherwise, false.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private bool validateContext(SplParseContext context, SplParseResult result)
        {
            #region implementation
            // Validate logger availability for error reporting and debugging
            if (context?.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context or its logger is null.");
                return false;
            }

            // Validate structured body context for section association
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return false;
            }

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new Section entity from the given XML element and saves it to the database.
        /// Extracts section metadata including GUID, codes, title, and effective time.
        /// </summary>
        /// <param name="xEl">The XElement representing the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created and saved Section entity, or null if creation failed.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<Section?> createAndSaveSectionAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Build section entity with extracted metadata from XML attributes and elements
                var section = new Section
                {
                    StructuredBodyID = context.StructuredBody!.StructuredBodyID!.Value,
                    SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
                    SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                    SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                    SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                    SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                    Title = xEl.GetSplElementVal(sc.E.Title)?.Trim()
                };

                // Enhanced EffectiveTime parsing to handle both simple and complex structures
                parseEffectiveTime(xEl, section);

                // Persist section to database using repository pattern
                var sectionRepo = context.GetRepository<Section>();
                await sectionRepo.CreateAsync(section);

                // Return section if successfully created with valid ID
                return section.SectionID > 0 ? section : null;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating section entity");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses effectiveTime element handling both simple value and low/high range structures.
        /// </summary>
        /// <param name="xEl">The section XElement containing effectiveTime information.</param>
        /// <param name="section">The Section entity to populate with effectiveTime data.</param>
        private static void parseEffectiveTime(XElement xEl, Section section)
        {
            var effectiveTimeEl = xEl.GetSplElement(sc.E.EffectiveTime);

            if (effectiveTimeEl == null)
            {
                section.EffectiveTime = DateTime.MinValue;
                return;
            }

            // Check for simple value attribute first
            var simpleValue = effectiveTimeEl.GetAttrVal(sc.A.Value);
            if (!string.IsNullOrEmpty(simpleValue))
            {
                section.EffectiveTime = Util.ParseNullableDateTime(simpleValue) ?? DateTime.MinValue;
                return;
            }

            // Check for low/high structure
            var lowEl = effectiveTimeEl.GetSplElement(sc.E.Low);
            var highEl = effectiveTimeEl.GetSplElement(sc.E.High);

            if (lowEl != null || highEl != null)
            {
                // Parse low boundary
                section.EffectiveTimeLow = lowEl != null
                    ? Util.ParseNullableDateTime(lowEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Parse high boundary  
                section.EffectiveTimeHigh = highEl != null
                    ? Util.ParseNullableDateTime(highEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Set the main EffectiveTime to the low value for backward compatibility
                section.EffectiveTime = section.EffectiveTimeLow ?? DateTime.MinValue;
            }
            else
            {
                section.EffectiveTime = DateTime.MinValue;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Finds and delegates parsing of a manufacturedProduct element within the section.
        /// Navigates SPL hierarchy to locate product elements for specialized processing.
        /// </summary>
        /// <param name="sectionEl">The XElement of the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>The SplParseResult from the product parser, or an empty result if no product exists.</returns>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseManufacturedProductAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // Navigate through the SPL hierarchy: section/subject/manufacturedProduct
            // Follow standard SPL document structure to locate product elements
            var productEl = sectionEl.SplElement(sc.E.Subject, sc.E.ManufacturedProduct);

            // Delegate to specialized product parser if product element exists
            if (productEl != null)
            {
                var productParser = new ManufacturedProductParser();
                return await productParser.ParseAsync(productEl, context, reportProgress);
            }

            // Return a default successful result if no product element is found
            return new SplParseResult { Success = true };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a section contains REMS protocol elements that should be parsed.
        /// </summary>
        /// <param name="sectionEl">The section XElement to check.</param>
        /// <returns>True if the section contains REMS protocols, false otherwise.</returns>
        /// <seealso cref="REMSParser"/>
        /// <seealso cref="Label"/>
        private static bool containsRemsProtocols(XElement sectionEl)
        {
            #region implementation
            // Check for REMS-specific elements that indicate this section should be processed by REMSParser
            return sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration, sc.E.ComponentOf, sc.E.Protocol).Any() ||
                   sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Document).Any();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses compliance actions contained within the section by looking for subjectOf elements
        /// that contain action elements, delegating to the specialized ComplianceActionParser.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse for compliance actions.</param>
        /// <param name="context">The current parsing context containing the section and other contextual information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the aggregated results from all compliance action parsing operations.</returns>
        /// <example>
        /// <code>
        /// var result = await parseComplianceActionsAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Compliance actions created: {result.ProductElementsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method searches for XML structures matching the pattern:
        /// &lt;section&gt;&lt;subjectOf&gt;&lt;action&gt;...&lt;/action&gt;&lt;/subjectOf&gt;&lt;/section&gt;
        /// Each found action element is processed by the ComplianceActionParser to create
        /// ComplianceAction entities and any associated AttachedDocument entities.
        /// The method ensures proper context management for DocumentRelationship when available.
        /// </remarks>
        /// <seealso cref="ComplianceActionParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseComplianceActionsAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Look for compliance actions in subjectOf elements within the section
                // This follows the SPL document structure: <section><subjectOf><action>...</action></subjectOf></section>
                var subjectOfElements = sectionEl.SplElements(sc.E.SubjectOf);

                foreach (var subjectEl in subjectOfElements)
                {
                    // Check if this subjectOf element contains an action element
                    if (subjectEl.SplElement(sc.E.Action) != null)
                    {
                        // --- START: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                        // The ComplianceActionParser requires either CurrentDocumentRelationship or CurrentPackageIdentifier
                        // For section-level compliance actions, we typically use DocumentRelationship context if available
                        // No need to set context here as it should already be established by ParseDocumentRelationshipAsync
                        // or inherited from parent parsing context

                        try
                        {
                            // Delegate to specialized compliance action parser
                            var complianceParser = new ComplianceActionParser();
                            var complianceResult = await complianceParser.ParseAsync(subjectEl, context, reportProgress);

                            // Merge results to accumulate counts and errors
                            result.MergeFrom(complianceResult);

                            // Log errors if compliance parsing failed
                            if (!complianceResult.Success)
                            {
                                context?.Logger?.LogError("Failed to parse compliance action for SectionID {SectionID} in file {FileName}.",
                                    context.CurrentSection?.SectionID, context.FileNameInZip);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Handle errors during individual compliance action parsing
                            result.Success = false;
                            result.Errors.Add($"Error parsing individual compliance action: {ex.Message}");
                            context?.Logger?.LogError(ex, "Error parsing compliance action in section for {FileName}", context.FileNameInZip);
                        }
                        // --- END: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during compliance action parsing
                result.Success = false;
                result.Errors.Add($"Error parsing compliance actions in section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing compliance actions for section in {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses certification links for Blanket No Changes Certification (BNCC) sections by
        /// finding product identifiers and setting appropriate context for the specialized parser.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the BNCC section to parse for certification links.</param>
        /// <param name="context">The current parsing context containing the section and document relationship information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the results from certification link parsing operations.</returns>
        /// <example>
        /// <code>
        /// if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
        /// {
        ///     var result = await parseCertificationLinksAsync(sectionElement, parseContext, progress);
        ///     if (result.Success)
        ///     {
        ///         Console.WriteLine($"Certification links created: {result.ProductElementsCreated}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is specifically designed for BNCC sections and looks for product-related
        /// elements within the section that can be linked to certifications. It expects the
        /// DocumentRelationship context to be properly set before calling. The method finds
        /// product identifiers and sets the CurrentProductIdentifier context for each certification
        /// link parsing operation, following the established context management pattern.
        /// </remarks>
        /// <seealso cref="CertificationProductLinkParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseCertificationLinksAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate that we have the required DocumentRelationship context for certification links
                if (context.CurrentDocumentRelationship?.DocumentRelationshipID == null)
                {
                    // This is not necessarily an error - some BNCC sections may not have certification links
                    return new SplParseResult { Success = true };
                }

                // Look for product-related elements within the section that can have certification links
                // In BNCC sections, we need to find manufactured products or product identifiers
                var productElements = sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct);

                foreach (var productEl in productElements)
                {
                    // Look for product identification codes within the manufactured product
                    var codeElements = productEl.SplElements(sc.E.Code);

                    foreach (var codeEl in codeElements)
                    {
                        // Check if this code element represents a product identifier with certification potential
                        if (!string.IsNullOrEmpty(codeEl.GetAttrVal(sc.A.CodeValue)))
                        {
                            // Create a ProductIdentifier to set the required context for certification link parsing
                            // This provides the ProductIdentifier context that CertificationProductLinkParser expects
                            var productIdentifier = await createProductIdentifierForContextAsync(codeEl, context);

                            if (productIdentifier?.ProductIdentifierID.HasValue == true)
                            {
                                // --- START: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                                var oldIdentifier = context.CurrentProductIdentifier;
                                context.CurrentProductIdentifier = productIdentifier; // Set context for the child parser
                                try
                                {
                                    // Delegate to specialized certification link parser
                                    var certLinkParser = new CertificationProductLinkParser();
                                    var certLinkResult = await certLinkParser.ParseAsync(codeEl, context, reportProgress);

                                    // Merge results to accumulate counts and errors
                                    result.MergeFrom(certLinkResult);

                                    // Log errors if certification link parsing failed
                                    if (!certLinkResult.Success)
                                    {
                                        context?.Logger?.LogError("Failed to parse certification link for ProductIdentifierID {ProductIdentifierID} in file {FileName}.",
                                            productIdentifier.ProductIdentifierID, context.FileNameInZip);
                                    }
                                }
                                finally
                                {
                                    context.CurrentProductIdentifier = oldIdentifier; // Restore context
                                }
                                // --- END: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during certification link parsing
                result.Success = false;
                result.Errors.Add($"Error parsing certification links in BNCC section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing certification links for BNCC section in {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a temporary ProductIdentifier from the given code element to establish
        /// the proper context for certification link parsing.
        /// </summary>
        /// <param name="codeEl">The XElement containing product identification information.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A ProductIdentifier entity with a valid ProductIdentifierID, or null if creation failed.</returns>
        /// <remarks>
        /// This helper method creates a ProductIdentifier entity to provide the required context
        /// for CertificationProductLinkParser. Since repository FindAsync is not available,
        /// this method creates new ProductIdentifier entities based on the code element data.
        /// The ProductID will be set if a current product exists in the parsing context.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<ProductIdentifier?> createProductIdentifierForContextAsync(XElement codeEl, SplParseContext context)
        {
            #region implementation
            try
            {
                var codeValue = codeEl.GetAttrVal(sc.A.CodeValue);
                var codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrEmpty(codeValue))
                {
                    return null;
                }

                // Create a new ProductIdentifier using the correct model properties
                var newIdentifier = new ProductIdentifier
                {
                    ProductID = context.CurrentProduct?.ProductID, // Link to current product if available
                    IdentifierValue = codeValue, // Maps to [code code=] attribute
                    IdentifierSystemOID = codeSystem, // Maps to [code codeSystem=] attribute
                    IdentifierType = determineIdentifierType(codeSystem) // Classify based on OID
                };

                // Get repository for ProductIdentifier operations
                var identifierRepo = context.GetRepository<ProductIdentifier>();
                await identifierRepo.CreateAsync(newIdentifier);

                return newIdentifier.ProductIdentifierID > 0 ? newIdentifier : null;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating ProductIdentifier for certification link context");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the identifier type classification based on the OID system.
        /// </summary>
        /// <param name="oidSystem">The OID system string from the code element.</param>
        /// <returns>A string classification of the identifier type, or null if not recognized.</returns>
        /// <remarks>
        /// This method maps common OID systems to their corresponding identifier types
        /// for proper classification in the ProductIdentifier entity.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private static string? determineIdentifierType(string? oidSystem)
        {
            #region implementation
            if (string.IsNullOrEmpty(oidSystem))
            {
                return null;
            }

            // Map common OID systems to identifier types
            // These mappings should be updated based on your system's OID registry
            return oidSystem switch
            {
                "2.16.840.1.113883.6.69" => "NDC", // National Drug Code
                "1.3.160" => "GTIN", // Global Trade Item Number
                "2.16.840.1.113883.6.162" => "UPC", // Universal Product Code
                _ => "OTHER" // Generic classification for unrecognized OIDs
            };
            #endregion
        }

        #endregion
    }
}
