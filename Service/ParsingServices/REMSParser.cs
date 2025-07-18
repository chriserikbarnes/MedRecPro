using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.ParsingValidators;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses REMS (Risk Evaluation and Mitigation Strategy) elements according to SPL Implementation Guide Section 23.
    /// Implements DRY methods for populating Protocol, Stakeholder, REMSMaterial, Requirement, REMSApproval, and REMSElectronicResource models.
    /// Enhanced with comprehensive validation using custom validation attributes.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex structure of REMS documents including protocols, requirements, stakeholders,
    /// materials, and approval information. It follows the patterns established in SectionParser and ManufacturedProductParser.
    /// All methods use getOrCreate patterns to avoid record duplications and include validation before database saves.
    /// </remarks>
    /// <seealso cref="Protocol"/>
    /// <seealso cref="Stakeholder"/>
    /// <seealso cref="REMSMaterial"/>
    /// <seealso cref="Requirement"/>
    /// <seealso cref="REMSApproval"/>
    /// <seealso cref="REMSElectronicResource"/>
    /// <seealso cref="SectionParser"/>
    /// <seealso cref="REMSValidationService"/>
    /// <seealso cref="Label"/>
    public class REMSParser : ISplSectionParser
    {
        #region private vars
        /// <summary>
        /// Gets the section name for this parser, representing the REMS protocol elements.
        /// </summary>
        public string SectionName => "protocol";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// REMS approval code constant from the specification.
        /// </summary>
        private const string RemsApprovalCode = "C128899";

        /// <summary>
        /// FDA SPL code system for REMS-related codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Validation service for REMS entities.
        /// </summary>
        private readonly REMSValidationService _validationService;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the REMSParser with validation capabilities.
        /// </summary>
        public REMSParser()
        {
            _validationService = new REMSValidationService();
        }

        /**************************************************************/
        /// <summary>
        /// Parses REMS protocol elements from an SPL document section, creating the necessary entities
        /// and establishing relationships between protocols, requirements, stakeholders, and materials.
        /// Enhanced with validation to ensure data integrity.
        /// </summary>
        /// <param name="xEl">The XElement representing the section containing REMS protocol elements.</param>
        /// <param name="context">The current parsing context containing the section to link REMS entities to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and counts of entities created.</returns>
        /// <example>
        /// <code>
        /// var parser = new REMSParser();
        /// var result = await parser.ParseAsync(sectionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"REMS entities created: {result.ProductElementsCreated}");
        /// }
        /// else
        /// {
        ///     foreach (var error in result.Errors)
        ///         Console.WriteLine($"Validation Error: {error}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method orchestrates the parsing of REMS protocols, requirements, stakeholders, materials,
        /// approvals, and electronic resources according to SPL Implementation Guide Section 23.
        /// It handles both REMS Summary (82347-6) and REMS Participant Requirements (87525-2) sections.
        /// All entities are validated before being saved to ensure SPL compliance.
        /// </remarks>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="REMSApproval"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
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
                    result.Errors.Add("Invalid REMS element provided for parsing.");
                    return result;
                }

                // Report parsing start for monitoring and debugging purposes
                reportProgress?.Invoke($"Starting REMS parsing with validation for section " +
                    $"{context.CurrentSection?.Title ?? "Unknown"}, " +
                    $"file: {context.FileNameInZip}");

                // 1. Parse REMS protocols within the section
                var protocolsResult = await parseAndSaveProtocolsAsync(xEl, context);
                result.ProductElementsCreated += protocolsResult.EntitiesCreated;
                result.Errors.AddRange(protocolsResult.ValidationErrors);

                // 2. Parse REMS materials (82346-8 section)
                var materialsResult = await parseAndSaveRemsmaterialsAsync(xEl, context);
                result.ProductElementsCreated += materialsResult.EntitiesCreated;
                result.Errors.AddRange(materialsResult.ValidationErrors);

                // 3. Parse REMS electronic resources
                var resourcesResult = await parseAndSaveElectronicResourcesAsync(xEl, context);
                result.ProductElementsCreated += resourcesResult.EntitiesCreated;
                result.Errors.AddRange(resourcesResult.ValidationErrors);

                // Set overall success based on whether we have any validation errors
                if (result.Errors.Any())
                {
                    result.Success = false;
                    context?.Logger?.LogWarning("REMS parsing completed with validation errors for {FileName}. Errors: {Errors}",
                        context.FileNameInZip, string.Join("; ", result.Errors));
                }

                // Report parsing completion for monitoring purposes
                reportProgress?.Invoke($"REMS parsing completed. Entities created: {result.ProductElementsCreated}, " +
                    $"Validation errors: {result.Errors.Count} for {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle unexpected errors and log them for debugging
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing REMS: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing REMS elements for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        #region Private Helper Methods

        /**************************************************************/
        /// <summary>
        /// Represents the result of a parsing operation with validation.
        /// </summary>
        /// <seealso cref="Label"/>
        private class ParseOperationResult
        {
            #region implementation
            public int EntitiesCreated { get; set; }
            public List<string> ValidationErrors { get; set; } = new List<string>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context to ensure it contains the required dependencies for REMS parsing.
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

            // Validate current section context for REMS association
            if (context.CurrentSection?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse REMS because no current section context exists.");
                return false;
            }

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates an entity using the validation service and logs any errors.
        /// </summary>
        /// <typeparam name="T">The type of entity to validate.</typeparam>
        /// <param name="entity">The entity to validate.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="entityType">The name of the entity type for error messages.</param>
        /// <returns>A list of validation error messages.</returns>
        /// <seealso cref="REMSValidationService"/>
        /// <seealso cref="Label"/>
        private List<string> validateEntity<T>(T entity, SplParseContext context, string entityType)
        {
            #region implementation
            var errors = new List<string>();

            try
            {
                REMSValidationService.ValidationResultSummary validationResult = entity switch
                {
                    Protocol protocol => _validationService.ValidateProtocol(protocol),
                    Stakeholder stakeholder => _validationService.ValidateStakeholder(stakeholder),
                    Requirement requirement => _validationService.ValidateRequirement(requirement),
                    _ => new REMSValidationService.ValidationResultSummary { IsValid = true }
                };

                if (!validationResult.IsValid)
                {
                    var entityErrors = validationResult.Errors.Select(e => $"{entityType} validation: {e}").ToList();
                    errors.AddRange(entityErrors);

                    context?.Logger?.LogWarning("Validation failed for {EntityType} in file {FileName}: {Errors}",
                        entityType, context.FileNameInZip, string.Join("; ", entityErrors));
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Validation error for {entityType}: {ex.Message}";
                errors.Add(errorMsg);
                context?.Logger?.LogError(ex, "Error validating {EntityType} in file {FileName}", entityType, context.FileNameInZip);
            }

            return errors;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves REMS protocol elements and their associated requirements from subject2/substanceAdministration elements.
        /// Enhanced with validation for all created entities.
        /// </summary>
        /// <param name="sectionEl">The section XElement containing REMS protocol structures.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="getOrCreateProtocolAsync"/>
        /// <seealso cref="parseRequirementsForProtocolAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseAndSaveProtocolsAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            if (context == null 
                || context.ServiceProvider == null
                || context.CurrentSection == null
                || context.CurrentSection.SectionID == null)
            {
                return result;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find all subject2/substanceAdministration/componentOf/protocol elements
            foreach (var substanceAdminEl in sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration))
            {
                var protocolEl = substanceAdminEl.SplElement(sc.E.ComponentOf, sc.E.Protocol);
                if (protocolEl == null) continue;

                try
                {
                    // Create or get the protocol entity with validation
                    var protocolCreateResult = await getOrCreateProtocolAsync(dbContext, protocolEl, context.CurrentSection.SectionID.Value, context);
                    result.EntitiesCreated += protocolCreateResult.EntitiesCreated;
                    result.ValidationErrors.AddRange(protocolCreateResult.ValidationErrors);

                    // Parse approval information if this is the first protocol mention (23.2.8)
                    if (protocolCreateResult.Protocol?.ProtocolID.HasValue == true)
                    {
                        var approvalResult = await parseAndSaveRemsApprovalAsync(substanceAdminEl, protocolCreateResult.Protocol.ProtocolID.Value, dbContext, context);
                        result.EntitiesCreated += approvalResult.EntitiesCreated;
                        result.ValidationErrors.AddRange(approvalResult.ValidationErrors);

                        // Parse requirements and monitoring observations for this protocol
                        var requirementsResult = await parseRequirementsForProtocolAsync(protocolEl, protocolCreateResult.Protocol.ProtocolID.Value, context);
                        result.EntitiesCreated += requirementsResult.EntitiesCreated;
                        result.ValidationErrors.AddRange(requirementsResult.ValidationErrors);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error processing protocol element: {ex.Message}";
                    result.ValidationErrors.Add(errorMsg);
                    context?.Logger?.LogError(ex, "Error processing protocol in file {FileName}", context.FileNameInZip);
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Result class for entity creation operations with validation.
        /// </summary>
        /// <typeparam name="T">The type of entity created.</typeparam>
        /// <seealso cref="Label"/>
        private class EntityCreateResult<T>
        {
            #region implementation
            public T? Entity { get; set; }
            public int EntitiesCreated { get; set; }
            public List<string> ValidationErrors { get; set; } = new List<string>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Specialized result class for Protocol creation.
        /// </summary>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="Label"/>
        private class ProtocolCreateResult : EntityCreateResult<Protocol>
        {
            #region implementation
            public Protocol? Protocol => Entity;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Protocol or creates and saves it if not found, implementing getOrCreate pattern with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="protocolEl">The XElement representing the protocol.</param>
        /// <param name="sectionId">The ID of the parent section.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created Protocol entity and any validation errors.</returns>
        /// <remarks>
        /// Implements deduplication based on SectionID and ProtocolCode to prevent duplicate records.
        /// Follows SPL Implementation Guide Section 23.2.6 for protocol structure.
        /// Enhanced with validation to ensure SPL compliance before saving.
        /// </remarks>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<ProtocolCreateResult> getOrCreateProtocolAsync(ApplicationDbContext dbContext, XElement protocolEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new ProtocolCreateResult();

            try
            {
                // Extract protocol code information
                var protocolCode = protocolEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                var protocolCodeSystem = protocolEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
                var protocolDisplayName = protocolEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName);

                // Deduplication based on SectionID and ProtocolCode
                var existing = await dbContext.Set<Protocol>().FirstOrDefaultAsync(p =>
                    p.SectionID == sectionId &&
                    p.ProtocolCode == protocolCode);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new Protocol entity with extracted data
                var newProtocol = new Protocol
                {
                    SectionID = sectionId,
                    ProtocolCode = protocolCode,
                    ProtocolCodeSystem = protocolCodeSystem,
                    ProtocolDisplayName = protocolDisplayName
                };

                // Validate the protocol before saving
                var validationErrors = validateEntity(newProtocol, context, "Protocol");
                result.ValidationErrors.AddRange(validationErrors);

                // Save the protocol even if there are validation errors (but log them)
                // This allows processing to continue while capturing compliance issues
                dbContext.Set<Protocol>().Add(newProtocol);
                await dbContext.SaveChangesAsync();

                result.Entity = newProtocol;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("Protocol saved with validation errors for {FileName}: {ProtocolCode}",
                        context.FileNameInZip, protocolCode);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating protocol: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating protocol in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses requirements and monitoring observations for a specific protocol according to SPL IG Section 23.2.7.
        /// Enhanced with validation for all created entities.
        /// </summary>
        /// <param name="protocolEl">The protocol XElement containing requirement components.</param>
        /// <param name="protocolId">The ID of the parent protocol.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="parseRequirementComponentAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseRequirementsForProtocolAsync(XElement protocolEl, int protocolId, SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            if (context?.ServiceProvider == null)
            {
                return result;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Process each component within the protocol
            foreach (var componentEl in protocolEl.SplElements(sc.E.Component))
            {
                try
                {
                    // Check for requirement or monitoring observation
                    var requirementEl = componentEl.SplElement(sc.E.Requirement);
                    var monitoringObsEl = componentEl.SplElement(sc.E.MonitoringObservation);

                    if (requirementEl != null)
                    {
                        var componentResult = await parseRequirementComponentAsync(componentEl, requirementEl, protocolId, false, dbContext, context);
                        result.EntitiesCreated += componentResult.EntitiesCreated;
                        result.ValidationErrors.AddRange(componentResult.ValidationErrors);
                    }
                    else if (monitoringObsEl != null)
                    {
                        var componentResult = await parseRequirementComponentAsync(componentEl, monitoringObsEl, protocolId, true, dbContext, context);
                        result.EntitiesCreated += componentResult.EntitiesCreated;
                        result.ValidationErrors.AddRange(componentResult.ValidationErrors);
                    }
                }
                catch (Exception ex)
                {
                    result.ValidationErrors.Add($"Error processing requirement component: {ex.Message}");
                    context?.Logger?.LogError(ex, "Error processing requirement component in file {FileName}", context.FileNameInZip);
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single requirement or monitoring observation component, creating Requirement and Stakeholder entities.
        /// Enhanced with validation for all created entities.
        /// </summary>
        /// <param name="componentEl">The component XElement containing sequence and pause information.</param>
        /// <param name="requirementEl">The requirement or monitoring observation XElement.</param>
        /// <param name="protocolId">The ID of the parent protocol.</param>
        /// <param name="isMonitoringObservation">Flag indicating if this is a monitoring observation.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="getOrCreateRequirementAsync"/>
        /// <seealso cref="getOrCreateStakeholderAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseRequirementComponentAsync(XElement componentEl, XElement requirementEl,
            int protocolId, bool isMonitoringObservation, ApplicationDbContext dbContext, SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            try
            {
                // Extract sequence number and pause quantity information
                var qty = componentEl.GetSplElementAttrVal(sc.E.PauseQuantity, sc.A.Value) ?? string.Empty;
                var sequenceNumber = Util.ParseNullableInt(componentEl.GetSplElementAttrVal(sc.E.SequenceNumber, sc.A.Value) ?? string.Empty);
                var pauseQuantityValue = Util.ParseNullableDecimal(qty);
                var pauseQuantityUnit = componentEl.GetSplElementAttrVal(sc.E.PauseQuantity, sc.A.Unit);

                // Create or get the requirement entity with validation
                var requirementResult = await getOrCreateRequirementAsync(dbContext, requirementEl, protocolId,
                    sequenceNumber, isMonitoringObservation, pauseQuantityValue, pauseQuantityUnit, context);

                result.EntitiesCreated += requirementResult.EntitiesCreated;
                result.ValidationErrors.AddRange(requirementResult.ValidationErrors);

                // Parse stakeholder participation if present
                var participationEl = requirementEl.SplElement(sc.E.Participation);
                if (participationEl != null && requirementResult.Entity?.RequirementID.HasValue == true)
                {
                    var stakeholderResult = await parseStakeholderParticipationAsync(participationEl, requirementResult.Entity.RequirementID.Value, dbContext, context);
                    result.EntitiesCreated += stakeholderResult.EntitiesCreated;
                    result.ValidationErrors.AddRange(stakeholderResult.ValidationErrors);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error parsing requirement component: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing requirement component in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Requirement or creates and saves it if not found, with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="requirementEl">The XElement representing the requirement or monitoring observation.</param>
        /// <param name="protocolId">The ID of the parent protocol.</param>
        /// <param name="sequenceNumber">The sequence number for this requirement.</param>
        /// <param name="isMonitoringObservation">Flag indicating if this is a monitoring observation.</param>
        /// <param name="pauseQuantityValue">Optional pause quantity value.</param>
        /// <param name="pauseQuantityUnit">Optional pause quantity unit.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created Requirement entity and any validation errors.</returns>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<EntityCreateResult<Requirement>> getOrCreateRequirementAsync(ApplicationDbContext dbContext,
            XElement requirementEl, int protocolId, int? sequenceNumber, bool isMonitoringObservation,
            decimal? pauseQuantityValue, string? pauseQuantityUnit, SplParseContext context)
        {
            #region implementation
            var result = new EntityCreateResult<Requirement>();

            try
            {
                // Extract requirement code information
                var requirementCode = requirementEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                var requirementCodeSystem = requirementEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
                var requirementDisplayName = requirementEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName);
                var originalTextReference = requirementEl.SplElement(sc.E.Code)?.SplElement(sc.E.OriginalText)?.GetAttrVal(sc.A.Value);

                // Extract period information for repetitive requirements
                var periodValue = Util.ParseNullableDecimal(requirementEl
                    .GetSplElementAttrVal(sc.E.EffectiveTime, sc.E.Period, sc.A.Value) ?? string.Empty);
                var periodUnit = requirementEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.E.Period, sc.A.Unit);

                // Deduplication based on ProtocolID, RequirementCode, and SequenceNumber
                var existing = await dbContext.Set<Requirement>().FirstOrDefaultAsync(r =>
                    r.ProtocolID == protocolId &&
                    r.RequirementCode == requirementCode &&
                    r.RequirementSequenceNumber == sequenceNumber);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new Requirement entity
                var newRequirement = new Requirement
                {
                    ProtocolID = protocolId,
                    RequirementSequenceNumber = sequenceNumber,
                    IsMonitoringObservation = isMonitoringObservation,
                    PauseQuantityValue = pauseQuantityValue,
                    PauseQuantityUnit = pauseQuantityUnit,
                    RequirementCode = requirementCode,
                    RequirementCodeSystem = requirementCodeSystem,
                    RequirementDisplayName = requirementDisplayName,
                    OriginalTextReference = originalTextReference,
                    PeriodValue = periodValue,
                    PeriodUnit = periodUnit
                };

                // Validate the requirement before saving
                var validationErrors = validateEntity(newRequirement, context, "Requirement");
                result.ValidationErrors.AddRange(validationErrors);

                dbContext.Set<Requirement>().Add(newRequirement);
                await dbContext.SaveChangesAsync();

                result.Entity = newRequirement;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("Requirement saved with validation errors for {FileName}: {RequirementCode}",
                        context.FileNameInZip, requirementCode);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating requirement: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating requirement in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses stakeholder participation information and links stakeholders to requirements, with validation.
        /// </summary>
        /// <param name="participationEl">The participation XElement containing stakeholder information.</param>
        /// <param name="requirementId">The ID of the parent requirement.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="getOrCreateStakeholderAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseStakeholderParticipationAsync(XElement participationEl, 
            int requirementId, 
            ApplicationDbContext dbContext, 
            SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            try
            {
                var stakeholderEl = participationEl.SplElement(sc.E.Stakeholder);
                if (stakeholderEl == null) return result;

                // Create or get the stakeholder entity with validation
                var stakeholderResult = await getOrCreateStakeholderAsync(dbContext, stakeholderEl, context);
                result.EntitiesCreated += stakeholderResult.EntitiesCreated;
                result.ValidationErrors.AddRange(stakeholderResult.ValidationErrors);

                // Link stakeholder to requirement by updating the requirement record
                if (stakeholderResult.Entity?.StakeholderID.HasValue == true)
                {
                    var requirement = await dbContext.Set<Requirement>().FindAsync(requirementId);
                    if (requirement != null)
                    {
                        requirement.StakeholderID = stakeholderResult.Entity.StakeholderID;
                        await dbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error parsing stakeholder participation: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing stakeholder participation in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Stakeholder or creates and saves it if not found, with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="stakeholderEl">The XElement representing the stakeholder.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created Stakeholder entity and any validation errors.</returns>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<EntityCreateResult<Stakeholder>> getOrCreateStakeholderAsync(ApplicationDbContext dbContext, 
            XElement stakeholderEl, 
            SplParseContext context)
        {
            #region implementation
            var result = new EntityCreateResult<Stakeholder>();

            try
            {
                // Extract stakeholder code information
                var stakeholderCode = stakeholderEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                var stakeholderCodeSystem = stakeholderEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
                var stakeholderDisplayName = stakeholderEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName);

                // Deduplication based on StakeholderCode
                var existing = await dbContext.Set<Stakeholder>().FirstOrDefaultAsync(s =>
                    s.StakeholderCode == stakeholderCode);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new Stakeholder entity
                var newStakeholder = new Stakeholder
                {
                    StakeholderCode = stakeholderCode,
                    StakeholderCodeSystem = stakeholderCodeSystem,
                    StakeholderDisplayName = stakeholderDisplayName
                };

                // Validate the stakeholder before saving
                var validationErrors = validateEntity(newStakeholder, context, "Stakeholder");
                result.ValidationErrors.AddRange(validationErrors);

                dbContext.Set<Stakeholder>().Add(newStakeholder);
                await dbContext.SaveChangesAsync();

                result.Entity = newStakeholder;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("Stakeholder saved with validation errors for {FileName}: {StakeholderCode}",
                        context.FileNameInZip, stakeholderCode);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating stakeholder: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating stakeholder in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves REMS approval information for the first protocol mention according to SPL IG Section 23.2.8.
        /// Enhanced with validation.
        /// </summary>
        /// <param name="substanceAdminEl">The substanceAdministration XElement to check for approval information.</param>
        /// <param name="protocolId">The ID of the protocol to associate the approval with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="REMSApproval"/>
        /// <seealso cref="getOrCreateRemsApprovalAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseAndSaveRemsApprovalAsync(XElement substanceAdminEl, 
            int protocolId,
            ApplicationDbContext dbContext, 
            SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            try
            {
                // Check if this substanceAdministration has a subjectOf/approval element
                var approvalEl = substanceAdminEl.SplElement(sc.E.SubjectOf, sc.E.Approval);
                if (approvalEl == null) return result;

                // Create the REMS approval entity with validation
                var approvalResult = await getOrCreateRemsApprovalAsync(dbContext, approvalEl, protocolId, context);
                result.EntitiesCreated += approvalResult.EntitiesCreated;
                result.ValidationErrors.AddRange(approvalResult.ValidationErrors);
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error parsing REMS approval: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing REMS approval in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing REMSApproval or creates and saves it if not found, with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="approvalEl">The XElement representing the approval information.</param>
        /// <param name="protocolId">The ID of the protocol this approval is associated with.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created REMSApproval entity and any validation errors.</returns>
        /// <seealso cref="REMSApproval"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<EntityCreateResult<REMSApproval>> getOrCreateRemsApprovalAsync(ApplicationDbContext dbContext, 
            XElement approvalEl, 
            int protocolId, 
            SplParseContext context)
        {
            #region implementation
            var result = new EntityCreateResult<REMSApproval>();

            try
            {
                // Extract approval information
                var apprDate = approvalEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.E.Low, sc.A.Value) ?? string.Empty;
                var approvalCode = approvalEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                var approvalCodeSystem = approvalEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
                var approvalDisplayName = approvalEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName);
                var approvalDate = Util.ParseNullableDateTime(apprDate);
                var territoryCode = approvalEl.SplElement(sc.E.Author)?.SplElement(sc.E.TerritorialAuthority)?.GetSplElementAttrVal(sc.E.Territory, sc.E.Code, sc.A.CodeValue);

                // Deduplication based on ProtocolID (should only be one approval per protocol)
                var existing = await dbContext.Set<REMSApproval>().FirstOrDefaultAsync(a =>
                    a.ProtocolID == protocolId);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new REMSApproval entity
                var newApproval = new REMSApproval
                {
                    ProtocolID = protocolId,
                    ApprovalCode = approvalCode,
                    ApprovalCodeSystem = approvalCodeSystem,
                    ApprovalDisplayName = approvalDisplayName,
                    ApprovalDate = approvalDate,
                    TerritoryCode = territoryCode
                };

                // Basic validation for approval entities (no specific validation attributes defined)
                var validationErrors = new List<string>();
                if (string.IsNullOrWhiteSpace(approvalCode))
                {
                    validationErrors.Add("REMS Approval validation: Approval code is required");
                }
                if (string.IsNullOrWhiteSpace(approvalCodeSystem))
                {
                    validationErrors.Add("REMS Approval validation: Approval code system is required");
                }

                result.ValidationErrors.AddRange(validationErrors);

                dbContext.Set<REMSApproval>().Add(newApproval);
                await dbContext.SaveChangesAsync();

                result.Entity = newApproval;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("REMS Approval saved with validation errors for {FileName}: {ApprovalCode}",
                        context.FileNameInZip, approvalCode);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating REMS approval: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating REMS approval in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves REMS materials from the REMS Material section (82346-8) according to SPL IG Section 23.2.9.
        /// Enhanced with validation.
        /// </summary>
        /// <param name="sectionEl">The section XElement to search for material references.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="parseRemsMaterialDocumentAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseAndSaveRemsmaterialsAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            if (context == null || context?.ServiceProvider == null || context.CurrentSection?.SectionID == null)
            {
                return result;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find all subject/manufacturedProduct/subjectOf/document elements (materials)
            foreach (var documentEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Document))
            {
                try
                {
                    var materialResult = await parseRemsMaterialDocumentAsync(documentEl, context.CurrentSection.SectionID.Value, dbContext, context);
                    result.EntitiesCreated += materialResult.EntitiesCreated;
                    result.ValidationErrors.AddRange(materialResult.ValidationErrors);
                }
                catch (Exception ex)
                {
                    result.ValidationErrors.Add($"Error processing REMS material: {ex.Message}");
                    context?.Logger?.LogError(ex, "Error processing REMS material in file {FileName}", context.FileNameInZip);
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single REMS material document, determining if it's an attached file or electronic resource.
        /// Enhanced with validation.
        /// </summary>
        /// <param name="documentEl">The document XElement containing material information.</param>
        /// <param name="sectionId">The ID of the parent section.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="getOrCreateRemsMaterialAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseRemsMaterialDocumentAsync(XElement documentEl, 
            int sectionId, 
            ApplicationDbContext dbContext, 
            SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            try
            {
                var textEl = documentEl.SplElement(sc.E.Text);
                if (textEl == null) return result;

                // Check if this is an attached document (has mediaType) or electronic resource
                var mediaType = textEl.GetAttrVal(sc.A.MediaType);
                if (!string.IsNullOrWhiteSpace(mediaType))
                {
                    // This is an attached document (e.g., PDF)
                    var materialResult = await getOrCreateRemsMaterialAsync(dbContext, documentEl, sectionId, context);
                    result.EntitiesCreated += materialResult.EntitiesCreated;
                    result.ValidationErrors.AddRange(materialResult.ValidationErrors);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error parsing REMS material document: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing REMS material document in file {FileName}", context.FileNameInZip);
            }

            return result; // Electronic resources are handled separately
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing REMSMaterial or creates and saves it if not found, with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="documentEl">The XElement representing the material document.</param>
        /// <param name="sectionId">The ID of the parent section.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created REMSMaterial entity and any validation errors.</returns>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<EntityCreateResult<REMSMaterial>> getOrCreateRemsMaterialAsync(ApplicationDbContext dbContext,
            XElement documentEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new EntityCreateResult<REMSMaterial>();

            try
            {
                // Extract material document information
                var documentGuid = Util.ParseNullableGuid(documentEl.GetAttrVal(sc.A.Root) ?? string.Empty);
                var title = documentEl.GetSplElementVal(sc.E.Title);
                var titleReference = extractTitleReference(title);
                var textEl = documentEl.SplElement(sc.E.Text);
                var fileName = textEl?.GetAttrVal(sc.A.Value);

                // Deduplication based on SectionID and MaterialDocumentGUID
                var existing = await dbContext.Set<REMSMaterial>().FirstOrDefaultAsync(m =>
                    m.SectionID == sectionId &&
                    m.MaterialDocumentGUID == documentGuid);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new REMSMaterial entity
                var newMaterial = new REMSMaterial
                {
                    SectionID = sectionId,
                    MaterialDocumentGUID = documentGuid,
                    Title = cleanTitle(title),
                    TitleReference = titleReference
                    // AttachedDocumentID would be set separately when processing attachments
                };

                // Basic validation for material entities
                var validationErrors = new List<string>();
                if (documentGuid == null || documentGuid == Guid.Empty)
                {
                    validationErrors.Add("REMS Material validation: Document GUID is required and must be valid");
                }
                if (string.IsNullOrWhiteSpace(newMaterial.Title))
                {
                    validationErrors.Add("REMS Material validation: Title is required");
                }

                result.ValidationErrors.AddRange(validationErrors);

                dbContext.Set<REMSMaterial>().Add(newMaterial);
                await dbContext.SaveChangesAsync();

                result.Entity = newMaterial;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("REMS Material saved with validation errors for {FileName}: {DocumentGuid}",
                        context.FileNameInZip, documentGuid);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating REMS material: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating REMS material in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves REMS electronic resources according to SPL IG Section 23.2.10.
        /// Enhanced with validation.
        /// </summary>
        /// <param name="sectionEl">The section XElement to search for electronic resource references.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="REMSElectronicResource"/>
        /// <seealso cref="parseElectronicResourceDocumentAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseAndSaveElectronicResourcesAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            if (context?.ServiceProvider == null || context.CurrentSection?.SectionID == null)
            {
                return result;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find all subject/manufacturedProduct/subjectOf/document elements (electronic resources)
            foreach (var documentEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Document))
            {
                try
                {
                    var resourceResult = await parseElectronicResourceDocumentAsync(documentEl, context.CurrentSection.SectionID.Value, dbContext, context);
                    result.EntitiesCreated += resourceResult.EntitiesCreated;
                    result.ValidationErrors.AddRange(resourceResult.ValidationErrors);
                }
                catch (Exception ex)
                {
                    result.ValidationErrors.Add($"Error processing electronic resource: {ex.Message}");
                    context?.Logger?.LogError(ex, "Error processing electronic resource in file {FileName}", context.FileNameInZip);
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single electronic resource document, checking for URL or URN references.
        /// Enhanced with validation.
        /// </summary>
        /// <param name="documentEl">The document XElement containing electronic resource information.</param>
        /// <param name="sectionId">The ID of the parent section.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing count of entities created and any validation errors.</returns>
        /// <seealso cref="REMSElectronicResource"/>
        /// <seealso cref="getOrCreateElectronicResourceAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ParseOperationResult> parseElectronicResourceDocumentAsync(XElement documentEl, 
            int sectionId, 
            ApplicationDbContext dbContext, 
            SplParseContext context)
        {
            #region implementation
            var result = new ParseOperationResult();

            try
            {
                var textEl = documentEl.SplElement(sc.E.Text);
                if (textEl == null) return result;

                // Check if this is an electronic resource (no mediaType, has URI reference)
                var mediaType = textEl.GetAttrVal(sc.A.MediaType);
                var referenceValue = textEl.GetAttrVal(sc.A.Value);

                if (string.IsNullOrWhiteSpace(mediaType) && !string.IsNullOrWhiteSpace(referenceValue))
                {
                    // Check if reference is a URI (starts with http:// or urn:)
                    if (referenceValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        referenceValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        referenceValue.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
                    {
                        var resourceResult = await getOrCreateElectronicResourceAsync(dbContext, documentEl, sectionId, context);
                        result.EntitiesCreated += resourceResult.EntitiesCreated;
                        result.ValidationErrors.AddRange(resourceResult.ValidationErrors);
                    }
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error parsing electronic resource document: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing electronic resource document in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing REMSElectronicResource or creates and saves it if not found, with validation.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="documentEl">The XElement representing the electronic resource document.</param>
        /// <param name="sectionId">The ID of the parent section.</param>
        /// <param name="context">The parsing context for validation and logging.</param>
        /// <returns>The result containing the existing or newly created REMSElectronicResource entity and any validation errors.</returns>
        /// <seealso cref="REMSElectronicResource"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<EntityCreateResult<REMSElectronicResource>> getOrCreateElectronicResourceAsync(ApplicationDbContext dbContext,
            XElement documentEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new EntityCreateResult<REMSElectronicResource>();

            try
            {
                // Extract electronic resource information
                var resourceGuid = Util.ParseNullableGuid(documentEl.GetAttrVal(sc.A.Root) ?? string.Empty);
                var title = documentEl.GetSplElementVal(sc.E.Title);
                var titleReference = extractTitleReference(title);
                var textEl = documentEl.SplElement(sc.E.Text);
                var resourceReferenceValue = textEl?.GetAttrVal(sc.A.Value);

                // Deduplication based on SectionID and ResourceDocumentGUID
                var existing = await dbContext.Set<REMSElectronicResource>().FirstOrDefaultAsync(r =>
                    r.SectionID == sectionId &&
                    r.ResourceDocumentGUID == resourceGuid);

                if (existing != null)
                {
                    result.Entity = existing;
                    return result;
                }

                // Create new REMSElectronicResource entity
                var newResource = new REMSElectronicResource
                {
                    SectionID = sectionId,
                    ResourceDocumentGUID = resourceGuid,
                    Title = cleanTitle(title),
                    TitleReference = titleReference,
                    ResourceReferenceValue = resourceReferenceValue
                };

                // Basic validation for electronic resource entities
                var validationErrors = new List<string>();
                if (resourceGuid == null || resourceGuid == Guid.Empty)
                {
                    validationErrors.Add("REMS Electronic Resource validation: Resource document GUID is required and must be valid");
                }
                if (string.IsNullOrWhiteSpace(newResource.Title))
                {
                    validationErrors.Add("REMS Electronic Resource validation: Title is required");
                }
                if (string.IsNullOrWhiteSpace(resourceReferenceValue))
                {
                    validationErrors.Add("REMS Electronic Resource validation: Resource reference value (URI) is required");
                }
                else if (!Uri.TryCreate(resourceReferenceValue, UriKind.Absolute, out _))
                {
                    validationErrors.Add("REMS Electronic Resource validation: Resource reference value must be a valid URI");
                }

                result.ValidationErrors.AddRange(validationErrors);

                dbContext.Set<REMSElectronicResource>().Add(newResource);
                await dbContext.SaveChangesAsync();

                result.Entity = newResource;
                result.EntitiesCreated = 1;

                if (validationErrors.Any())
                {
                    context?.Logger?.LogWarning("REMS Electronic Resource saved with validation errors for {FileName}: {ResourceGuid}",
                        context.FileNameInZip, resourceGuid);
                }
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Error creating electronic resource: {ex.Message}");
                context?.Logger?.LogError(ex, "Error creating electronic resource in file {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the reference ID from a title that contains embedded link references.
        /// </summary>
        /// <param name="title">The title string that may contain reference links.</param>
        /// <returns>The extracted reference ID, or null if no reference found.</returns>
        /// <example>
        /// extractTitleReference("REMS Material <reference value=\"#T001\"/>") returns "#T001"
        /// </example>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="REMSElectronicResource"/>
        /// <seealso cref="Label"/>
        private static string? extractTitleReference(string? title)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            // Look for reference patterns like <reference value="#T001"/>
            var referencePattern = @"<reference\s+value\s*=\s*[""']([^""']+)[""']\s*/>";
            var match = System.Text.RegularExpressions.Regex.Match(title, referencePattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cleans the title by removing embedded reference elements while preserving the main text.
        /// </summary>
        /// <param name="title">The title string that may contain embedded references.</param>
        /// <returns>The cleaned title without reference elements.</returns>
        /// <example>
        /// cleanTitle("REMS Material <reference value=\"#T001\"/>") returns "REMS Material"
        /// </example>
        /// <seealso cref="REMSMaterial"/>
        /// <seealso cref="REMSElectronicResource"/>
        /// <seealso cref="Label"/>
        private static string? cleanTitle(string? title)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            // Remove reference elements from the title
            var referencePattern = @"<reference\s+value\s*=\s*[""'][^""']+[""']\s*/>";
            var cleanedTitle = System.Text.RegularExpressions.Regex.Replace(title, referencePattern, "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return cleanedTitle.Trim();
            #endregion
        }

        #endregion
    }
}