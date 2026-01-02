
using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
using static MedRecProImportClass.Models.Label;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses compliance action information from SPL documents.
    /// </summary>
    /// <remarks>
    /// This parser handles the [action] element within a [subjectOf] tag, creating a
    /// ComplianceAction record. It is responsible for extracting the action code,
    /// effective time, and delegating the parsing of any attached documents.
    /// It uses the built-in validation attributes on the ComplianceAction model.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="AttachedDocumentParser"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "complianceAction";

        /**************************************************************/
        /// <summary>
        /// Parses an [action] element and its children to create a ComplianceAction entity.
        /// </summary>
        /// <param name="element">The parent XElement, typically [subjectOf], which contains the [action] element.</param>
        /// <param name="context">The current parsing context, which provides foreign key context (e.g., PackageIdentifierID).</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult indicating success and any errors encountered.</returns>
        /// <example>
        /// <code>
        /// // To be called from a parent parser like ManufacturedProductParser or a future EstablishmentParser
        /// var complianceParser = new ComplianceActionParser();
        /// var complianceResult = await complianceParser.ParseAsync(subjectOfElement, context, reportProgress);
        /// result.MergeFrom(complianceResult);
        /// </code>
        /// </example>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();
            var actionEl = element.SplElement(sc.E.Action);

            if (actionEl == null)
            {
                // No action element found in the provided parent, which is valid.
                return result;
            }

            reportProgress?.Invoke($"Parsing compliance action in {context.FileNameInZip}");

            var complianceAction = new ComplianceAction();

            // 1. Populate the object with data from the XML
            populateComplianceAction(actionEl, complianceAction, context);

            // 2. Validate the populated object using its data annotations
            var validationErrors = validateComplianceActionWithAttributes(complianceAction, context);

            if (validationErrors.Any())
            {
                result.Success = false;
                foreach (var error in validationErrors)
                {
                    result.Errors.Add(error);
                }
                return result; // Do not proceed with an invalid object
            }

            // 3. Get or create the database record
            var createdAction = await getOrCreateComplianceActionAsync(complianceAction, context);
            result.ProductElementsCreated++; // Re-using counter

            // 4. Delegate parsing of attached documents within the <action> scope
            if (createdAction?.ComplianceActionID != null)
            {
                // Set context for the child parser
                var oldAction = context.CurrentComplianceAction;
                context.CurrentComplianceAction = createdAction;

                try
                {
                    var attachedDocParser = new AttachedDocumentParser();
                    var docResult = await attachedDocParser.ParseAsync(actionEl, context, reportProgress);
                    result.MergeFrom(docResult);
                }
                finally
                {
                    // Restore context
                    context.CurrentComplianceAction = oldAction;
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a ComplianceAction entity using both standard DataAnnotations and custom validation attributes.
        /// </summary>
        /// <param name="action">The ComplianceAction entity to validate.</param>
        /// <param name="context">The current parsing context for dependency injection and service provider access.</param>
        /// <returns>A list of validation error messages. Empty list indicates successful validation.</returns>
        /// <remarks>
        /// This method provides comprehensive validation by triggering all validation attributes including:
        /// - ComplianceActionContextValidation
        /// - ComplianceActionConsistencyValidation  
        /// - ComplianceActionCodeValidation
        /// It also handles IValidatableObject implementations for custom validation logic.
        /// </remarks>
        /// <example>
        /// <code>
        /// var errors = validateComplianceActionWithAttributes(complianceAction, context);
        /// if (errors.Any())
        /// {
        ///     // Handle validation errors
        ///     foreach (var error in errors)
        ///     {
        ///         result.Errors.Add(error);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ValidationContext"/>
        /// <seealso cref="Validator"/>
        /// <seealso cref="IValidatableObject"/>
        /// <seealso cref="Label"/>
        private List<string> validateComplianceActionWithAttributes(ComplianceAction action, SplParseContext context)
        {
            #region implementation
            var errors = new List<string>();

            // Use the validation service that handles custom attributes with dependency injection
            var validationContext = new ValidationContext(action, context.ServiceProvider, null);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // This will trigger all validation attributes including custom ones like:
            // [ComplianceActionContextValidation], [ComplianceActionConsistencyValidation], [ComplianceActionCodeValidation]
            bool isValid = Validator.TryValidateObject(action, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                // Add each validation attribute error to our error collection
                foreach (var result in validationResults)
                {
                    errors.Add($"ComplianceAction validation: {result.ErrorMessage}");
                }
            }

            // Also call IValidatableObject.Validate if the entity implements it
            // This allows for complex cross-property validation logic
            if (action is IValidatableObject validatable)
            {
                var customValidationResults = validatable.Validate(validationContext);
                foreach (var result in customValidationResults)
                {
                    errors.Add($"ComplianceAction custom validation: {result.ErrorMessage}");
                }
            }

            return errors;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts data from the [action] XElement and populates a ComplianceAction object.
        /// </summary>
        /// <param name="actionEl">The [action] XElement.</param>
        /// <param name="complianceAction">The ComplianceAction object to populate.</param>
        /// <param name="context">The current parsing context to get linkage information.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private void populateComplianceAction(XElement actionEl, ComplianceAction complianceAction, SplParseContext context)
        {
            #region implementation
            // Link to the current section
            complianceAction.SectionID = context.CurrentSection?.SectionID;

            // Link to the parent entity (Package or Establishment) based on the context
            // The calling parser is responsible for setting one of these in the context.
            if (context.CurrentPackageIdentifier?.PackageIdentifierID != null)
            {
                complianceAction.PackageIdentifierID = context.CurrentPackageIdentifier.PackageIdentifierID;
            }
            else if (context.CurrentDocumentRelationship?.DocumentRelationshipID != null)
            {
                complianceAction.DocumentRelationshipID = context.CurrentDocumentRelationship.DocumentRelationshipID;
            }

            // Parse <code> element attributes (Spec 18.1.7.1 - 18.1.7.4)
            var codeEl = actionEl.SplElement(sc.E.Code);
            if (codeEl != null)
            {
                complianceAction.ActionCode = codeEl.GetAttrVal(sc.A.CodeValue);
                complianceAction.ActionCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);
                complianceAction.ActionDisplayName = codeEl.GetAttrVal(sc.A.DisplayName);
            }

            // Parse <effectiveTime> (Spec 18.1.7.7, 18.1.7.8)
            var effectiveTimeEl = actionEl.SplElement(sc.E.EffectiveTime);
            if (effectiveTimeEl != null)
            {
                // The model supports a low/high range, common for inactivations
                var lowVal = effectiveTimeEl.GetSplElementAttrVal(sc.E.Low, sc.A.Value);
                var highVal = effectiveTimeEl.GetSplElementAttrVal(sc.E.High, sc.A.Value);
                // The spec also shows a single 'value' attribute for simpler cases
                var singleVal = effectiveTimeEl.GetAttrVal(sc.A.Value);

                complianceAction.EffectiveTimeLow = Util.ParseNullableDateTime(lowVal ?? singleVal ?? string.Empty);
                complianceAction.EffectiveTimeHigh = Util.ParseNullableDateTime(highVal ?? string.Empty);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a ComplianceAction already exists based on a unique combination of keys.
        /// If it does not exist, it creates a new record in the database.
        /// </summary>
        /// <param name="action">The ComplianceAction entity populated with data from the SPL file.</param>
        /// <param name="context">The parsing context for database access and logging.</param>
        /// <returns>The existing or newly created ComplianceAction entity, attached to the current context.</returns>
        /// <remarks>
        /// This method prevents duplicate compliance actions by querying for an existing record that matches
        /// either the PackageIdentifierID or DocumentRelationshipID, combined with the action code and effective start date.
        /// This unique combination defines a specific inactivation event.
        /// </remarks>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="DbSet{TEntity}"/>
        /// <seealso cref="Label"/>
        private async Task<ComplianceAction?> getOrCreateComplianceActionAsync(ComplianceAction action, SplParseContext context)
        {
            #region implementation
            if(context == null || context.ServiceProvider == null)
            {
                return null;
            }

            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<ComplianceAction>();
            var dbSet = dbContext.Set<ComplianceAction>();

            ComplianceAction? existingAction = null;

            // Query the database to find if an identical compliance action already exists.
            if (action.PackageIdentifierID.HasValue && action.DocumentRelationshipID.HasValue)
            {
                // Both are set - check for exact match
                existingAction = await dbSet.FirstOrDefaultAsync(c =>
                    c.PackageIdentifierID == action.PackageIdentifierID &&
                    c.DocumentRelationshipID == action.DocumentRelationshipID &&
                    c.ActionCode == action.ActionCode &&
                    c.EffectiveTimeLow == action.EffectiveTimeLow);
            }
            else if (action.PackageIdentifierID.HasValue)
            {
                // Package-specific action
                existingAction = await dbSet.FirstOrDefaultAsync(c =>
                    c.PackageIdentifierID == action.PackageIdentifierID &&
                    c.ActionCode == action.ActionCode &&
                    c.EffectiveTimeLow == action.EffectiveTimeLow &&
                    c.DocumentRelationshipID == null);
            }
            else if (action.DocumentRelationshipID.HasValue)
            {
                // Establishment-specific action
                existingAction = await dbSet.FirstOrDefaultAsync(c =>
                    c.DocumentRelationshipID == action.DocumentRelationshipID &&
                    c.ActionCode == action.ActionCode &&
                    c.EffectiveTimeLow == action.EffectiveTimeLow &&
                    c.PackageIdentifierID == null);
            }
            else if (!action.PackageIdentifierID.HasValue && !action.DocumentRelationshipID.HasValue)
            {
                // Handle orphaned compliance actions
                existingAction = await dbSet.FirstOrDefaultAsync(c =>
                    c.PackageIdentifierID == null &&
                    c.DocumentRelationshipID == null &&
                    c.SectionID == action.SectionID &&
                    c.ActionCode == action.ActionCode &&
                    c.EffectiveTimeLow == action.EffectiveTimeLow);
            }

            // Check if the action was found in the database
            if (existingAction != null)
            {
                // If it exists, log it and return the existing entity.
                context?.Logger?.LogInformation(
                    "Found existing ComplianceAction with ID {ComplianceActionID} for PackageIdentifierID {PackageId} or DocumentRelationshipID {DocRelId}",
                    existingAction.ComplianceActionID,
                    action.PackageIdentifierID,
                    action.DocumentRelationshipID);
                return existingAction;
            }
            else
            {
                // If it does not exist, create a new record using the repository.
                await repo.CreateAsync(action);
                context?.Logger?.LogInformation(
                    "Created new ComplianceAction for PackageIdentifierID {PackageId} or DocumentRelationshipID {DocRelId}",
                    action.PackageIdentifierID,
                    action.DocumentRelationshipID);
                // Return the newly created action, which now has its database-assigned ID.
                return action;
            }
            #endregion
        }
        #endregion
    }
}
