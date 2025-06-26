using System.Xml.Linq;

using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Helpers;

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
        public string SectionName => sc.E.Document;

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
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
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

                // Update the parsing context with the newly created document
                context.Document = document;
                result.DocumentsCreated = 1;
                result.ParsedEntity = document;

                // Log successful document creation
                context.Logger.LogInformation("Created Document with ID {DocumentID} for file {FileName}",
                    document.DocumentID, context.FileNameInZip);
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
                var ce = docEl.Element(ns + sc.E.Code);

                // Create and populate the Document entity with extracted metadata
                return new Document
                {
                    // Extract document GUID from the id element's root attribute
                    DocumentGUID = Util.ParseNullableGuid(docEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty),

                    // Extract code value from the code element's codeValue attribute
                    DocumentCode = ce?.GetAttrVal(sc.A.CodeValue),

                    // Extract code system from the code element's codeSystem attribute
                    DocumentCodeSystem = ce?.GetAttrVal(sc.A.CodeSystem),

                    // Extract display name from the code element's displayName attribute
                    DocumentDisplayName = ce?.GetAttrVal(sc.A.DisplayName),

                    // Extract and trim the document title from the title element
                    Title = docEl.GetSplElementVal(sc.E.Title)?.Trim(),

                    // Extract and parse effective time from the effectiveTime element's value attribute
                    EffectiveTime = Util.ParseNullableDateTime(docEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty),

                    // Extract set GUID from the setId element's root attribute
                    SetGUID = Util.ParseNullableGuid(docEl.GetSplElementAttrVal(sc.E.SetId, sc.A.Root) ?? string.Empty),

                    // Extract and parse version number from the versionNumber element's value attribute
                    VersionNumber = Util.ParseNullableInt(docEl.GetSplElementAttrVal(sc.E.VersionNumber, sc.A.Value) ?? string.Empty),
                };
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