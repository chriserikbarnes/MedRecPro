
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
using sc = MedRecPro.Models.SplConstants;
using c = MedRecPro.Models.Constant;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses attached document references within an SPL section.
    /// </summary>
    /// <remarks>
    /// This parser handles [document] elements that reference external files (e.g., PDFs).
    /// It is designed to be called from other parsers (like ComplianceActionParser or SectionParser)
    /// that encounter these references. It extracts metadata such as title, media type, and filename.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "attachedDocument";

        /**************************************************************/
        /// <summary>
        /// Parses [document] elements from a given parent element and creates AttachedDocument records.
        /// </summary>
        /// <param name="element">The parent XElement containing one or more <![CDATA[<subjectOf><document>]]> structures.</param>
        /// <param name="context">The current parsing context, used to access the current section and parent entities.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult indicating success and the number of documents created.</returns>
        /// <example>
        /// <code>
        /// // Called from another parser, passing the element that contains the document reference
        /// var docParser = new AttachedDocumentParser();
        /// var docResult = await docParser.ParseAsync(actionElement, context, reportProgress);
        /// result.MergeFrom(docResult);
        /// </code>
        /// </example>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Find all <document> elements nested within <subjectOf> children of the current element.
            var docElements = element.SplElements(sc.E.SubjectOf, sc.E.Document);

            foreach (var docEl in docElements)
            {
                reportProgress?.Invoke($"Parsing attached document reference in {context.FileNameInZip}");

                var attachedDoc = new AttachedDocument();

                // Link to the broader section context
                attachedDoc.SectionID = context.CurrentSection?.SectionID;

                // Link to the specific parent entity (e.g., ComplianceAction, Product) if available in the context.
                // The calling parser is responsible for setting this context.
                if (context.CurrentComplianceAction?.ComplianceActionID != null)
                {
                    attachedDoc.ComplianceActionID = context.CurrentComplianceAction.ComplianceActionID;
                }
                if (context.CurrentProduct?.ProductID != null)
                {
                    attachedDoc.ProductID = context.CurrentProduct.ProductID;
                }

                // Parse document metadata from XML
                parseDocumentMetadata(docEl, attachedDoc);

                // Validate the parsed data against spec requirements
                var validationErrors = validateAttachedDocument(attachedDoc, context);
                if (validationErrors.Any())
                {
                    result.Success = false;
                    result.Errors.AddRange(validationErrors);
                    continue; // Skip saving this invalid document
                }

                // Get or create the document record in the database
                await getOrCreateAttachedDocumentAsync(attachedDoc, context);
                result.ProductElementsCreated++; // Re-using this counter for created entities
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts metadata from the [document] XElement and populates the AttachedDocument object.
        /// </summary>
        /// <param name="docEl">The [document] XElement.</param>
        /// <param name="attachedDoc">The AttachedDocument object to populate.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseDocumentMetadata(XElement docEl, AttachedDocument attachedDoc)
        {
            #region implementation
            // Extract the document's root ID (Spec 23.2.9.1)
            attachedDoc.DocumentIdRoot = docEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);

            // The title can contain a <reference> element. We need to extract its value
            // and then get the clean title text.
            var titleEl = docEl.SplElement(sc.E.Title);
            if (titleEl != null)
            {
                var titleRefEl = titleEl.SplElement(sc.E.Reference);
                if (titleRefEl != null)
                {
                    attachedDoc.TitleReference = titleRefEl.GetAttrVal(sc.A.Value);
                    // Temporarily remove the reference to get the clean title text
                    titleRefEl.Remove();
                }
                attachedDoc.Title = titleEl.Value.Trim();
            }

            // Extract mediaType and the file name from the reference (Spec 23.2.9.4, 23.2.9.5)
            var textEl = docEl.SplElement(sc.E.Text);
            if (textEl != null)
            {
                attachedDoc.MediaType = textEl.GetAttrVal(sc.A.MediaType);
                attachedDoc.FileName = textEl.GetSplElementAttrVal(sc.E.Reference, sc.A.Value);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the AttachedDocument object based on SPL specifications.
        /// </summary>
        /// <param name="doc">The AttachedDocument to validate.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>A list of validation error messages.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private List<string> validateAttachedDocument(AttachedDocument doc, SplParseContext context)
        {
            #region implementation
            var errors = new List<string>();

            // Spec 18.1.7.17, 23.2.9.5: Reference value is the file name for a valid document attachment.
            if (string.IsNullOrWhiteSpace(doc.FileName))
            {
                errors.Add($"Attached document in file {context.FileNameInZip} is missing a file name reference.");
            }

            // Spec 18.1.7.19, 23.2.9.6: File name extension matches the media type ".pdf".
            if (doc.MediaType?.Equals(c.PDF_MEDIA_TYPE, StringComparison.OrdinalIgnoreCase) == true
                && !doc.FileName?.EndsWith(c.PDF_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) == true)
            {
                errors.Add($"File '{doc.FileName}' has mediaType 'application/pdf' but does not have a .pdf extension.");
            }

            // Spec 23.2.9.1: Each reference document has an id root.
            if (string.IsNullOrWhiteSpace(doc.DocumentIdRoot))
            {
                errors.Add($"Attached document '{doc.FileName}' is missing a required id root.");
            }

            return errors;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if an AttachedDocument already exists; if not, it creates a new record.
        /// </summary>
        /// <param name="doc">The AttachedDocument entity to get or create.</param>
        /// <param name="context">The parsing context for database access.</param>
        /// <returns>The existing or newly created AttachedDocument entity.</returns>
        /// <remarks>This is a shim-able method. A full implementation would query the database to prevent duplicates.</remarks>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<AttachedDocument?> getOrCreateAttachedDocumentAsync(AttachedDocument doc, SplParseContext context)
        {
            #region implementation
            if (context == null || context.ServiceProvider == null)
            {
                return null;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<AttachedDocument>();
            var dbSet = dbContext.Set<AttachedDocument>();

            // Query the database to find if an identical document reference already exists for this section.
            // A unique document is defined by its file name within the scope of its section.
            AttachedDocument? existingDocument = await dbSet.FirstOrDefaultAsync(d =>
                d.SectionID == doc.SectionID &&
                d.ComplianceActionID == doc.ComplianceActionID &&
                d.ProductID == doc.ProductID &&
                d.ParentEntityType == doc.ParentEntityType &&
                d.ParentEntityID == doc.ParentEntityID &&
                d.FileName == doc.FileName) 
                    ?? await dbSet.FirstOrDefaultAsync(d =>
                    d.SectionID == doc.SectionID &&
                    d.FileName == doc.FileName);

            // Check if the document was found in the database
            if (existingDocument != null)
            {
                // If it exists, log it and return the existing entity.
                context?.Logger?.LogInformation(
                    "Found existing AttachedDocument with ID {AttachedDocumentID} for file '{FileName}' in SectionID {SectionID}",
                    existingDocument.AttachedDocumentID,
                    existingDocument.FileName,
                    existingDocument.SectionID);
                return existingDocument;
            }
            else
            {
                // If it does not exist, create a new record using the repository.
                await repo.CreateAsync(doc);
                context?.Logger?.LogInformation(
                    "Created new AttachedDocument for file '{FileName}' in SectionID {SectionID}",
                    doc.FileName,
                    doc.SectionID);
                // Return the newly created document, which now has its database-assigned ID.
                return doc;
            }
            #endregion
        }
        #endregion
    }
}
