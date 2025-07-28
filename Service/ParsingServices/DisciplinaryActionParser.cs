using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses disciplinary action information and attached documents from SPL approval elements.
    /// Handles disciplinary actions on licenses according to SPL Implementation Guide Section 18.1.7
    /// and REMS material documents according to Section 23.2.9.
    /// </summary>
    /// <remarks>
    /// This parser extracts disciplinary action data from approval elements within business operations,
    /// creating both DisciplinaryAction and AttachedDocument entities. It supports all standard
    /// disciplinary action types (suspension, revocation, activation, resolution, other) and validates
    /// all data against SPL Implementation Guide requirements before saving to the database.
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

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// FDA SPL code system for disciplinary action codes.
        /// </summary>
        /// <seealso cref="Label"/>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses approval elements to extract disciplinary actions and their attached documents.
        /// </summary>
        /// <param name="element">The XElement containing approval elements to parse.</param>
        /// <param name="context">The current parsing context, which must contain valid license information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <example>
        /// <code>
        /// var disciplinaryActionParser = new DisciplinaryActionParser();
        /// var result = await disciplinaryActionParser.ParseAsync(approvalElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Disciplinary actions created: {result.DisciplinaryActionsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method processes approval elements within business operations to create:
        /// 1. DisciplinaryAction entities for licensing disciplinary actions  
        /// 2. AttachedDocument entities for associated document references
        /// 
        /// The method handles all disciplinary action types as specified in SPL Implementation Guide
        /// Section 18.1.7 and validates all entities before saving to ensure compliance.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="License"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that the context contains valid parsing prerequisites
            if (context?.ServiceProvider == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse disciplinary actions due to invalid service provider or logger context.");
                return result;
            }

            // Validate that we have a valid license context
            if (context.CurrentLicense?.LicenseID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse disciplinary actions without a valid license context.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting Disciplinary Action XML Elements {context.FileNameInZip}");

                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Use the validated license ID
                var licenseId = context.CurrentLicense.LicenseID.Value;
                var disciplinaryActionCount = await parseAndSaveDisciplinaryActionsAsync(
                    element,
                    licenseId,
                    dbContext,
                    context.Logger);

                result.DisciplinaryActionsCreated += disciplinaryActionCount;
                result.Success = true;

                reportProgress?.Invoke($"Completed Disciplinary Action XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing disciplinary actions: {ex.Message}");
                context.Logger.LogError(ex, "Error processing disciplinary action elements.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses disciplinary actions for a specific license from approval elements.
        /// </summary>
        /// <param name="approvalEl">The approval XML element containing disciplinary actions.</param>
        /// <param name="licenseId">The license ID to associate disciplinary actions with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The number of disciplinary actions created.</returns>
        /// <example>
        /// <code>
        /// var disciplinaryActionParser = new DisciplinaryActionParser();
        /// var count = await disciplinaryActionParser.ParseDisciplinaryActionsForLicenseAsync(approvalElement, license.LicenseID.Value, dbContext, logger);
        /// Console.WriteLine($"Created {count} disciplinary actions for license");
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is specifically designed to be called from the LicenseParser after a license
        /// has been successfully created. It processes the same approval element to extract any
        /// disciplinary actions associated with that license.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="License"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        public async Task<int> ParseDisciplinaryActionsForLicenseAsync(
            XElement approvalEl,
            int licenseId,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                return await parseAndSaveDisciplinaryActionsAsync(approvalEl, licenseId, dbContext, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error parsing disciplinary actions for License {licenseId}");
                return 0;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves disciplinary action entities from approval elements.
        /// </summary>
        /// <param name="parentEl">The parent XML element containing approval elements.</param>
        /// <param name="licenseId">The license id for the disciplinary action</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The number of disciplinary actions created.</returns>
        /// <remarks>
        /// This method searches for approval elements that contain disciplinary action information
        /// and creates both the disciplinary action records and associated document references.
        /// It follows SPL Implementation Guide Section 18.1.7 for disciplinary action structure.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveDisciplinaryActionsAsync(
            XElement parentEl,
            int licenseId,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            int disciplinaryActionsCreated = 0;

            // Find all approval elements that contain disciplinary actions
            foreach (var approvalEl in parentEl.SplElements(sc.E.SubjectOf, sc.E.Approval))
            {
                try
                {
                    // Look for action elements within the approval
                    foreach (var actionEl in approvalEl.SplElements(sc.E.SubjectOf, sc.E.Action))
                    {
                        var disciplinaryAction = await parseDisciplinaryActionFromActionAsync(actionEl, licenseId, dbContext, logger);
                        if (disciplinaryAction?.DisciplinaryActionID != null)
                        {
                            // Parse any attached documents for this disciplinary action
                            await parseAttachedDocumentsForDisciplinaryActionAsync(
                                actionEl, disciplinaryAction.DisciplinaryActionID.Value, dbContext, logger);

                            disciplinaryActionsCreated++;
                            logger.LogInformation($"Created DisciplinaryAction ID {disciplinaryAction.DisciplinaryActionID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing individual disciplinary action from approval element.");
                }
            }

            return disciplinaryActionsCreated;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a disciplinary action entity from an action XML element.
        /// </summary>
        /// <param name="actionEl">The action XML element containing disciplinary action information.</param>
        /// <param name="licenseId">The license ID to associate the disciplinary action with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The created or existing DisciplinaryAction entity, or null if parsing failed.</returns>
        /// <remarks>
        /// This method extracts disciplinary action metadata from the action element including
        /// code, display name, effective time, and text descriptions. It validates all data
        /// according to SPL Implementation Guide Section 18.1.7 before saving.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<DisciplinaryAction?> parseDisciplinaryActionFromActionAsync(
            XElement actionEl,
            int licenseId,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Extract disciplinary action code information
                var codeEl = actionEl.GetSplElement(sc.E.Code);
                string? actionCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? actionCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? actionDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                if (string.IsNullOrWhiteSpace(actionCode))
                {
                    logger.LogWarning("Disciplinary action missing required action code.");
                    return null;
                }

                // Extract effective time (18.1.7.7-18.1.7.8)
                DateTime? effectiveTime = extractDisciplinaryActionEffectiveTime(actionEl, logger);

                // Extract action text for "other" actions (18.1.7.5-18.1.7.6)
                string? actionText = extractDisciplinaryActionText(actionEl, actionCode, logger);

                // Create or get existing disciplinary action with validation
                var disciplinaryAction = await getOrCreateDisciplinaryActionAsync(
                    dbContext,
                    licenseId,
                    actionCode,
                    actionCodeSystem,
                    actionDisplayName,
                    effectiveTime,
                    actionText,
                    logger);

                return disciplinaryAction;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing disciplinary action from action element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the disciplinary action effective time from the action element.
        /// </summary>
        /// <param name="actionEl">The action XML element containing effective time information.</param>
        /// <param name="logger">Logger for warning messages.</param>
        /// <returns>The parsed effective time, or null if not found or invalid.</returns>
        /// <remarks>
        /// This method extracts the effective time from the action element, which represents
        /// when the disciplinary action became effective according to SPL Implementation Guide
        /// Section 18.1.7.7-18.1.7.8. The date must have at least day precision in YYYYMMDD format.
        /// </remarks>
        /// <seealso cref="Label"/>
        private DateTime? extractDisciplinaryActionEffectiveTime(XElement actionEl, ILogger logger)
        {
            #region implementation
            try
            {
                var effectiveTimeEl = actionEl.GetSplElement(sc.E.EffectiveTime);
                string? effectiveTimeValue = effectiveTimeEl?.GetAttrVal(sc.A.Value);

                if (string.IsNullOrWhiteSpace(effectiveTimeValue))
                {
                    logger.LogWarning("No effective time found in disciplinary action.");
                    return null;
                }

                // SPL dates must have at least day precision in YYYYMMDD format (18.1.7.8)
                if (effectiveTimeValue.Length >= 8 &&
                    DateTime.TryParseExact(effectiveTimeValue.Substring(0, 8), "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime effectiveDate))
                {
                    return effectiveDate;
                }

                logger.LogWarning($"Could not parse disciplinary action effective time: {effectiveTimeValue}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting effective time from disciplinary action element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the disciplinary action text for "other" action types.
        /// </summary>
        /// <param name="actionEl">The action XML element containing text information.</param>
        /// <param name="actionCode">The action code to determine if text is required.</param>
        /// <param name="logger">Logger for warning messages.</param>
        /// <returns>The action text if required and present, otherwise null.</returns>
        /// <remarks>
        /// This method extracts the text description for disciplinary actions with code "other" (C118472)
        /// according to SPL Implementation Guide Section 18.1.7.5-18.1.7.6. The text must be plain text
        /// without markup and of xsi:type "ST".
        /// </remarks>
        /// <seealso cref="Label"/>
        private string? extractDisciplinaryActionText(XElement actionEl, string? actionCode, ILogger logger)
        {
            #region implementation
            try
            {
                // Text is only required for "other" actions (C118472) per 18.1.7.5
                if (string.Equals(actionCode, "C118472", StringComparison.OrdinalIgnoreCase))
                {
                    var textEl = actionEl.GetSplElement(sc.E.Text);
                    string? actionText = textEl?.Value;

                    if (string.IsNullOrWhiteSpace(actionText))
                    {
                        logger.LogWarning("Action text is required when action code is 'other' (C118472).");
                        return null;
                    }

                    // Validate that text is plain text without markup (18.1.7.6)
                    if (actionText.Contains('<') || actionText.Contains('>'))
                    {
                        logger.LogWarning("Action text must be plain text without markup for 'other' actions.");
                        return null;
                    }

                    return actionText;
                }

                return null; // Text not required for non-"other" actions
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting action text from disciplinary action element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses attached documents for a disciplinary action from the action element.
        /// </summary>
        /// <param name="actionEl">The action XML element containing document references.</param>
        /// <param name="disciplinaryActionId">The ID of the disciplinary action to associate documents with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method searches for document elements within the disciplinary action and creates
        /// AttachedDocument entities according to SPL Implementation Guide Section 18.1.7.15-18.1.7.19.
        /// </remarks>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task parseAttachedDocumentsForDisciplinaryActionAsync(
            XElement actionEl,
            int disciplinaryActionId,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Find all document elements within the disciplinary action (18.1.7.15)
                foreach (var documentEl in actionEl.SplElements(sc.E.SubjectOf, sc.E.Document))
                {
                    var attachedDocument = await parseAttachedDocumentFromDocumentAsync(
                        documentEl, "DisciplinaryAction", disciplinaryActionId, dbContext, logger);

                    if (attachedDocument?.AttachedDocumentID != null)
                    {
                        logger.LogInformation($"Created AttachedDocument ID {attachedDocument.AttachedDocumentID} for DisciplinaryAction {disciplinaryActionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing attached documents for disciplinary action.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an attached document entity from a document XML element.
        /// </summary>
        /// <param name="documentEl">The document XML element containing attachment information.</param>
        /// <param name="parentEntityType">The type of the parent entity (e.g., "DisciplinaryAction", "REMSMaterial").</param>
        /// <param name="parentEntityId">The ID of the parent entity.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The created or existing AttachedDocument entity, or null if parsing failed.</returns>
        /// <remarks>
        /// This method extracts document attachment information from the document element including
        /// media type and file name. It validates all data according to SPL Implementation Guide
        /// Sections 18.1.7.16-18.1.7.19 and 23.2.9.4-23.2.9.6 before saving.
        /// </remarks>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<AttachedDocument?> parseAttachedDocumentFromDocumentAsync(
            XElement documentEl,
            string parentEntityType,
            int parentEntityId,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Extract document reference information (18.1.7.16, 23.2.9.4)
                var textEl = documentEl.GetSplElement(sc.E.Text);
                string? mediaType = textEl?.GetAttrVal(sc.A.MediaType);

                var referenceEl = textEl?.GetSplElement(sc.E.Reference);
                string? fileName = referenceEl?.GetAttrVal(sc.A.Value);

                if (string.IsNullOrWhiteSpace(mediaType))
                {
                    logger.LogWarning("Document reference missing required media type.");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    logger.LogWarning("Document reference missing required file name.");
                    return null;
                }

                // Create or get existing attached document with validation
                var attachedDocument = await getOrCreateAttachedDocumentAsync(
                    dbContext,
                    parentEntityType,
                    parentEntityId,
                    mediaType,
                    fileName,
                    logger);

                return attachedDocument;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing attached document from document element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing DisciplinaryAction or creates and saves it if not found.
        /// Validates the disciplinary action data before saving.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="licenseId">The license ID to associate the disciplinary action with.</param>
        /// <param name="actionCode">The disciplinary action code.</param>
        /// <param name="actionCodeSystem">The code system for the action code.</param>
        /// <param name="actionDisplayName">The display name for the action code.</param>
        /// <param name="effectiveTime">The effective time of the disciplinary action.</param>
        /// <param name="actionText">The action text for "other" actions.</param>
        /// <param name="logger">Logger for validation warning and error messages.</param>
        /// <returns>The existing or newly created DisciplinaryAction entity, or null if validation failed.</returns>
        /// <remarks>
        /// This method implements the DRY principle by checking for existing disciplinary actions
        /// before creating new ones. It follows SPL Implementation Guide Section 18.1.7 for
        /// validation and ensures all data complies with SPL requirements before saving.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<DisciplinaryAction?> getOrCreateDisciplinaryActionAsync(
            ApplicationDbContext dbContext,
            int licenseId,
            string? actionCode,
            string? actionCodeSystem,
            string? actionDisplayName,
            DateTime? effectiveTime,
            string? actionText,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Try to find existing disciplinary action
                var existing = await dbContext.Set<DisciplinaryAction>().FirstOrDefaultAsync(da =>
                    da.LicenseID == licenseId &&
                    da.ActionCode == actionCode &&
                    da.ActionCodeSystem == actionCodeSystem &&
                    da.EffectiveTime == effectiveTime);

                if (existing != null)
                {
                    // Update existing disciplinary action with current information
                    existing.ActionDisplayName = actionDisplayName;
                    existing.ActionText = actionText;

                    // Validate updated disciplinary action before saving
                    if (validateDisciplinaryAction(existing, logger))
                    {
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation($"Updated existing disciplinary action: {existing.DisciplinaryActionID}");
                        return existing;
                    }
                    else
                    {
                        logger.LogWarning($"Updated disciplinary action failed validation: {existing.DisciplinaryActionID}");
                        return null;
                    }
                }

                // Create new disciplinary action
                var newDisciplinaryAction = new DisciplinaryAction
                {
                    LicenseID = licenseId,
                    ActionCode = actionCode,
                    ActionCodeSystem = actionCodeSystem,
                    ActionDisplayName = actionDisplayName,
                    EffectiveTime = effectiveTime,
                    ActionText = actionText
                };

                // Validate before saving
                if (!validateDisciplinaryAction(newDisciplinaryAction, logger))
                {
                    logger.LogWarning("New disciplinary action failed validation and will not be saved.");
                    return null;
                }

                dbContext.Set<DisciplinaryAction>().Add(newDisciplinaryAction);
                await dbContext.SaveChangesAsync();

                logger.LogInformation($"Created new disciplinary action: {newDisciplinaryAction.DisciplinaryActionID}");
                return newDisciplinaryAction;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating or retrieving disciplinary action.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing AttachedDocument or creates and saves it if not found.
        /// Validates the attached document data before saving.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="parentEntityType">The type of the parent entity.</param>
        /// <param name="parentEntityId">The ID of the parent entity.</param>
        /// <param name="mediaType">The MIME type of the document.</param>
        /// <param name="fileName">The file name of the document.</param>
        /// <param name="logger">Logger for validation warning and error messages.</param>
        /// <returns>The existing or newly created AttachedDocument entity, or null if validation failed.</returns>
        /// <remarks>
        /// This method implements the DRY principle by checking for existing attached documents
        /// before creating new ones. It validates all data against SPL compliance rules before saving
        /// according to SPL Implementation Guide Sections 18.1.7 and 23.2.9.
        /// </remarks>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<AttachedDocument?> getOrCreateAttachedDocumentAsync(
            ApplicationDbContext dbContext,
            string parentEntityType,
            int parentEntityId,
            string? mediaType,
            string? fileName,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Try to find existing attached document
                var existing = await dbContext.Set<AttachedDocument>().FirstOrDefaultAsync(ad =>
                    ad.ParentEntityType == parentEntityType &&
                    ad.ParentEntityID == parentEntityId &&
                    ad.FileName == fileName);

                if (existing != null)
                {
                    // Update existing attached document with current information
                    existing.MediaType = mediaType;

                    // Validate updated attached document before saving
                    if (validateAttachedDocument(existing, logger))
                    {
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation($"Updated existing attached document: {existing.AttachedDocumentID}");
                        return existing;
                    }
                    else
                    {
                        logger.LogWarning($"Updated attached document failed validation: {existing.AttachedDocumentID}");
                        return null;
                    }
                }

                // Create new attached document
                var newAttachedDocument = new AttachedDocument
                {
                    ParentEntityType = parentEntityType,
                    ParentEntityID = parentEntityId,
                    MediaType = mediaType,
                    FileName = fileName
                };

                // Validate before saving
                if (!validateAttachedDocument(newAttachedDocument, logger))
                {
                    logger.LogWarning("New attached document failed validation and will not be saved.");
                    return null;
                }

                dbContext.Set<AttachedDocument>().Add(newAttachedDocument);
                await dbContext.SaveChangesAsync();

                logger.LogInformation($"Created new attached document: {newAttachedDocument.AttachedDocumentID}");
                return newAttachedDocument;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating or retrieving attached document.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a DisciplinaryAction entity against SPL Implementation Guide requirements.
        /// </summary>
        /// <param name="disciplinaryAction">The disciplinary action to validate.</param>
        /// <param name="logger">Logger for validation messages.</param>
        /// <returns>True if the disciplinary action is valid, false otherwise.</returns>
        /// <remarks>
        /// This method uses the validation attributes defined in the validation namespace
        /// to ensure compliance with SPL Implementation Guide Section 18.1.7.
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="Label"/>
        private static bool validateDisciplinaryAction(DisciplinaryAction disciplinaryAction, ILogger logger)
        {
            #region implementation
            if (disciplinaryAction == null)
            {
                logger.LogWarning("DisciplinaryAction is null and cannot be validated.");
                return false;
            }

            var validationContext = new ValidationContext(disciplinaryAction);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Perform comprehensive validation using validation attributes
            bool isValid = Validator.TryValidateObject(disciplinaryAction, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    // Handle the case where ErrorMessage might be null
                    var errorMessage = validationResult.ErrorMessage ?? "Unknown validation error";
                    logger.LogWarning($"DisciplinaryAction validation error: {errorMessage}");
                }
                return false;
            }

            logger.LogDebug("DisciplinaryAction validation successful.");
            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates an AttachedDocument entity against SPL Implementation Guide requirements.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="logger">Logger for validation messages.</param>
        /// <returns>True if the attached document is valid, false otherwise.</returns>
        /// <remarks>
        /// This method uses the validation attributes defined in the validation namespace
        /// to ensure compliance with SPL Implementation Guide Sections 18.1.7 and 23.2.9.
        /// </remarks>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static bool validateAttachedDocument(AttachedDocument attachedDocument, ILogger logger)
        {
            #region implementation
            if (attachedDocument == null)
            {
                logger.LogWarning("AttachedDocument is null and cannot be validated.");
                return false;
            }

            var validationContext = new ValidationContext(attachedDocument);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Perform comprehensive validation using validation attributes
            bool isValid = Validator.TryValidateObject(attachedDocument, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    // Handle the case where ErrorMessage might be null
                    var errorMessage = validationResult.ErrorMessage ?? "Unknown validation error";
                    logger.LogWarning($"AttachedDocument validation error: {errorMessage}");
                }
                return false;
            }

            logger.LogDebug("AttachedDocument validation successful.");
            return true;
            #endregion
        }
    }
}