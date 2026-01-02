
using MedRecProImportClass.Data;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Xml.Linq;
using static MedRecProImportClass.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses disciplinary action information and attached documents from SPL approval elements.
    /// Handles disciplinary actions on licenses according to SPL Implementation Guide Section 18.1.7.
    /// </summary>
    /// <remarks>
    /// This parser extracts disciplinary action data from approval elements within business operations,
    /// creating both DisciplinaryAction and AttachedDocument entities. It supports all standard
    /// disciplinary action types and validates all data against SPL Implementation Guide requirements
    /// before saving to the database.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="License"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "disciplinaryAction";

        /**************************************************************/
        /// <summary>
        /// Parses approval elements to extract disciplinary actions and their attached documents.
        /// </summary>
        /// <param name="element">The parent XElement containing one or more <![CDATA[<subjectOf><approval>]]> elements to parse.</param>
        /// <param name="context">The current parsing context, which must contain valid license information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements"></param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <example>
        /// <code>
        /// var disciplinaryActionParser = new DisciplinaryActionParser();
        /// var result = await disciplinaryActionParser.ParseAsync(businessOperationElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Disciplinary actions created: {result.DisciplinaryActionsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="License"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that we have a valid license context to link actions to.
            if (context.CurrentLicense?.LicenseID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse disciplinary actions without a valid license context.");
                return result;
            }

            reportProgress?.Invoke($"Starting Disciplinary Action XML Elements {context.FileNameInZip}");

            // Find all <approval> elements that contain disciplinary <action>s.
            var approvalElements = element
                .SplElements(sc.E.SubjectOf, sc.E.Approval)
                .Where(app => app.SplElement(sc.E.SubjectOf, sc.E.Action) != null);

            foreach (var approvalEl in approvalElements)
            {
                // Process each <action> within the <approval> element.
                foreach (var actionEl in approvalEl.SplElements(sc.E.SubjectOf, sc.E.Action))
                {
                    var actionResult = await parseSingleActionAsync(actionEl, context, reportProgress);
                    result.MergeFrom(actionResult);
                }
            }

            reportProgress?.Invoke($"Completed Disciplinary Action XML Elements {context.FileNameInZip}");
            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single [action] element, creating the DisciplinaryAction and any associated AttachedDocument records.
        /// </summary>
        /// <param name="actionEl">The [action] XElement to parse.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult for the single action parsed.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseSingleActionAsync(XElement actionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();
            var disciplinaryAction = new DisciplinaryAction();

            if(context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context or logger is not initialized.");
                return result;
            }

            // Populate the DisciplinaryAction object from the XML element.
            populateDisciplinaryAction(actionEl, disciplinaryAction, context);

            // Validate the populated object using its data annotations.
            var validationErrors = validateEntity(disciplinaryAction, context.Logger, context);
            if (validationErrors.Any())
            {
                result.Success = false;
                result.Errors.AddRange(validationErrors);
                return result; // Stop processing this invalid action.
            }

            // Get or create the disciplinary action in the database.
            var savedAction = await getOrCreateDisciplinaryActionAsync(disciplinaryAction, context);
            if (savedAction?.DisciplinaryActionID == null)
            {
                result.Success = false;
                result.Errors.Add("Failed to save the disciplinary action to the database.");
                return result;
            }
            result.DisciplinaryActionsCreated++;

            // Now parse any attached documents associated with this action.
            var docResult = await parseAttachedDocumentsAsync(actionEl, savedAction.DisciplinaryActionID.Value, context, reportProgress);
            result.MergeFrom(docResult);

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts data from the [action] XElement and populates a DisciplinaryAction object.
        /// </summary>
        /// <param name="actionEl">The [action] XElement.</param>
        /// <param name="disciplinaryAction">The DisciplinaryAction object to populate.</param>
        /// <param name="context">The current parsing context for linkage.</param>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private void populateDisciplinaryAction(XElement actionEl, DisciplinaryAction disciplinaryAction, SplParseContext context)
        {
            #region implementation
            if (context == null 
                || context.CurrentLicense == null
                || context.CurrentLicense.LicenseID == null)
            {
                return;
            }

            disciplinaryAction.LicenseID = context.CurrentLicense.LicenseID;

            // Extract code attributes (Spec 18.1.7.1 - 18.1.7.4)
            var codeEl = actionEl.SplElement(sc.E.Code);
            disciplinaryAction.ActionCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            disciplinaryAction.ActionCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            disciplinaryAction.ActionDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

            // Extract effective time (Spec 18.1.7.7 - 18.1.7.8)
            var effectiveTimeStr = actionEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value);
            disciplinaryAction.EffectiveTime = Util.ParseNullableDateTime(effectiveTimeStr ?? string.Empty);

            // Extract text for "other" action type (Spec 18.1.7.5 - 18.1.7.6)
            if (disciplinaryAction.ActionCode == "C118472") // "other"
            {
                disciplinaryAction.ActionText = actionEl.GetSplElementVal(sc.E.Text);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [document] references within a parent element (like [action]).
        /// </summary>
        /// <param name="parentEl">The parent element containing <![CDATA[<subjectOf><document>]]> structures.</param>
        /// <param name="parentEntityId">The ID of the parent entity (e.g., DisciplinaryActionID).</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult containing the outcome of parsing documents.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseAttachedDocumentsAsync(XElement parentEl, int parentEntityId, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context or logger is not initialized.");
                return result;
            }

            // Find all document references (Spec 18.1.7.15)
            var docElements = parentEl.SplElements(sc.E.SubjectOf, sc.E.Document);

            foreach (var docEl in docElements)
            {
                reportProgress?.Invoke($"Parsing attached document for DisciplinaryActionID {parentEntityId}");
                var attachedDoc = new AttachedDocument();

                // Populate the AttachedDocument object from the XML.
                populateAttachedDocument(docEl, attachedDoc, c.DISCIPLINARY_ACTION_ENTITY_TYPE, parentEntityId);

                // Validate the populated document.
                var validationErrors = validateEntity(attachedDoc, context.Logger, context);
                if (validationErrors.Any())
                {
                    result.Success = false;
                    result.Errors.AddRange(validationErrors);
                    continue; // Skip invalid document.
                }

                // Get or create the document in the database.
                await getOrCreateAttachedDocumentAsync(attachedDoc, context);
                result.ProductElementsCreated++; // Re-using this counter for any created sub-entity.
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts data from a [document] XElement and populates an AttachedDocument object.
        /// </summary>
        /// <param name="docEl">The [document] XElement.</param>
        /// <param name="doc">The AttachedDocument object to populate.</param>
        /// <param name="parentEntityType">The string name of the parent entity type.</param>
        /// <param name="parentEntityId">The ID of the parent entity.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void populateAttachedDocument(XElement docEl, AttachedDocument doc, string parentEntityType, int parentEntityId)
        {
            #region implementation
            doc.ParentEntityType = parentEntityType;
            doc.ParentEntityID = parentEntityId;

            // Extract metadata for both Disciplinary Action and REMS documents.
            // Spec 18.1.7.16, 23.2.9.4: text with mediaType and reference
            var textEl = docEl.SplElement(sc.E.Text);
            doc.MediaType = textEl?.GetAttrVal(sc.A.MediaType);
            doc.FileName = textEl?.GetSplElementAttrVal(sc.E.Reference, sc.A.Value);

            // Extract REMS-specific fields (Spec 23.2.9.1 - 23.2.9.3)
            doc.DocumentIdRoot = docEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
            var titleEl = docEl.SplElement(sc.E.Title);
            if (titleEl != null)
            {
                doc.TitleReference = titleEl.GetSplElementAttrVal(sc.E.Reference, sc.A.Value);
                // Clone to avoid modifying the original tree when getting the clean title text.
                var clone = new XElement(titleEl);
                clone.Element(titleEl.GetDefaultNamespace() + sc.E.Reference)?.Remove();
                doc.Title = clone.Value.Trim();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing DisciplinaryAction or creates it if not found.
        /// </summary>
        /// <param name="action">The DisciplinaryAction entity to get or create.</param>
        /// <param name="context">The parsing context for database access and dependency resolution.</param>
        /// <returns>The existing or newly created DisciplinaryAction entity, or null on failure.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<DisciplinaryAction?> getOrCreateDisciplinaryActionAsync(DisciplinaryAction action, SplParseContext context)
        {
            #region implementation
            try
            {
                if (context == null 
                    || context.Logger == null
                    || context.ServiceProvider == null)
                {
                    return null;
                }

                var dbContext = context.GetDbContext();
                var dbSet = dbContext.Set<DisciplinaryAction>();

                var existingAction = await dbSet.FirstOrDefaultAsync(d =>
                    d.LicenseID == action.LicenseID &&
                    d.ActionCode == action.ActionCode &&
                    d.EffectiveTime == action.EffectiveTime);

                if (existingAction != null)
                {
                    context.Logger.LogInformation("Found existing DisciplinaryAction ID {ActionId}", existingAction.DisciplinaryActionID);
                    return existingAction;
                }

                var repo = context.GetRepository<DisciplinaryAction>();
                await repo.CreateAsync(action);
                context.Logger.LogInformation("Created new DisciplinaryAction for LicenseID {LicenseId}", action.LicenseID);
                return action;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating or retrieving disciplinary action for LicenseID {LicenseId}", action.LicenseID);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing AttachedDocument or creates it if not found.
        /// </summary>
        /// <param name="doc">The AttachedDocument entity to get or create.</param>
        /// <param name="context">The parsing context for database access and dependency resolution.</param>
        /// <returns>The existing or newly created AttachedDocument entity, or null on failure.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<AttachedDocument?> getOrCreateAttachedDocumentAsync(AttachedDocument doc, SplParseContext context)
        {
            #region implementation
            try
            {
                if (context == null
                  || context.Logger == null
                  || context.ServiceProvider == null)
                {
                    return null;
                }

                var dbContext = context.GetDbContext();
                var dbSet = dbContext.Set<AttachedDocument>();

                var existingDoc = await dbSet.FirstOrDefaultAsync(d =>
                    d.ParentEntityID == doc.ParentEntityID &&
                    d.ParentEntityType == doc.ParentEntityType &&
                    d.FileName == doc.FileName);

                if (existingDoc != null)
                {
                    context.Logger.LogInformation("Found existing AttachedDocument ID {DocId}", existingDoc.AttachedDocumentID);
                    return existingDoc;
                }

                var repo = context.GetRepository<AttachedDocument>();
                await repo.CreateAsync(doc);
                context.Logger.LogInformation("Created new AttachedDocument for {ParentType} ID {ParentId}", doc.ParentEntityType, doc.ParentEntityID);
                return doc;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating or retrieving attached document for {ParentType} ID {ParentId}", doc.ParentEntityType, doc.ParentEntityID);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates an entity using its data annotations.
        /// </summary>
        /// <typeparam name="T">The type of the entity to validate.</typeparam>
        /// <param name="entity">The entity instance to validate.</param>
        /// <param name="logger">Logger for recording validation errors.</param>
        /// <param name="context"></param>
        /// <returns>A list of validation error messages.</returns>
        /// <seealso cref="Label"/>
        private List<string> validateEntity<T>(T entity, ILogger logger, SplParseContext context) where T : class
        {
            #region implementation
            var errors = new List<string>();
            if (entity == null)
            {
                var nullError = $"{typeof(T).Name} is null and cannot be validated.";
                logger.LogWarning(nullError);
                errors.Add(nullError);
                return errors;
            }

            var validationContext = new ValidationContext(entity, context.ServiceProvider, null);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    var errorMessage = $"{typeof(T).Name} validation error: {validationResult.ErrorMessage}";
                    logger.LogWarning(errorMessage);
                    errors.Add(errorMessage);
                }
            }
            return errors;
            #endregion
        }
        #endregion
    }
}
