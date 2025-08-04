using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Models.Validation;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Specialized parser for handling tolerance specification elements within SPL sections.
    /// Processes tolerance document structures defined in SPL Implementation Guide Sections 19.2.3 and 19.2.4,
    /// including substance specifications, analytes, commodities, application types, and observation criteria.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex tolerance specification structures defined in 40 CFR 180 tolerance documents,
    /// creating appropriate entities to support EPA regulatory compliance and data interoperability requirements.
    /// The parser follows the established patterns from SectionIndexingParser and uses validation classes
    /// to ensure compliance with the specifications.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SubstanceSpecification"/>
    /// <seealso cref="Analyte"/>
    /// <seealso cref="Commodity"/>
    /// <seealso cref="ApplicationType"/>
    /// <seealso cref="ObservationCriterion"/>
    /// <seealso cref="SplParseContext"/>
    public class ToleranceSpecificationParser : ISplSectionParser
    {
        #region Private Fields
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        public ToleranceSpecificationParser() { }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing tolerance specification processing.
        /// </summary>
        public string SectionName => "tolerance-specification";

        /**************************************************************/
        /// <summary>
        /// Parses tolerance specification elements from an SPL section, processing substance specifications,
        /// analytes, commodities, application types, and observation criteria based on SPL IG Sections 19.2.3-19.2.4.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for tolerance specifications.</param>
        /// <param name="context">The current parsing context containing section and document information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and tolerance specification elements created.</returns>
        /// <example>
        /// <code>
        /// var parser = new ToleranceSpecificationParser();
        /// var result = await parser.ParseAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Substance specifications created: {result.ProductElementsCreated}");
        ///     Console.WriteLine($"Observation criteria created: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method orchestrates the parsing of tolerance specifications by:
        /// 1. Validating the parsing context and section requirements
        /// 2. Processing substance specifications from identified substances (SPL IG 19.2.3)
        /// 3. Processing observation criteria with tolerance ranges (SPL IG 19.2.4)
        /// 4. Creating appropriate links between specifications, analytes, commodities, and application types
        /// 5. Applying validation rules defined in the validation attribute classes
        /// </remarks>
        /// <seealso cref="parseAndSaveSubstanceSpecificationsAsync"/>
        /// <seealso cref="parseAndSaveObservationCriteriaAsync"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate context and current section
                if (context?.CurrentSection?.SectionID == null || context.Document == null)
                {
                    result.Success = false;
                    result.Errors.Add("No current section or document available for tolerance specification parsing.");
                    return result;
                }

                var section = context.CurrentSection;
                reportProgress?.Invoke("Processing tolerance specification elements...");

                // Parse Substance Specifications (SPL IG 19.2.3)
                var substanceSpecsCreated = await parseAndSaveSubstanceSpecificationsAsync(element, section, context);
                result.ProductElementsCreated += substanceSpecsCreated;

                // Parse Observation Criteria with tolerance ranges (SPL IG 19.2.4)
                var observationCriteriaCreated = await parseAndSaveObservationCriteriaAsync(element, section, context);
                result.SectionAttributesCreated += observationCriteriaCreated;

                reportProgress?.Invoke($"Processed {result.ProductElementsCreated + result.SectionAttributesCreated} tolerance specification elements");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing tolerance specification elements: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing tolerance specification elements for section {SectionId}", context.CurrentSection?.SectionID);
            }

            return result;
            #endregion
        }

        #region Substance Specification Processing (SPL IG 19.2.3)

        /**************************************************************/
        /// <summary>
        /// Parses the [subject][identifiedSubstance][subjectOf][substanceSpecification] elements within a section
        /// for tolerance specification information according to SPL Implementation Guide Section 19.2.3.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of SubstanceSpecification and related records created.</returns>
        /// <remarks>
        /// Handles the parsing of tolerance specifications as defined in 19.2.3 Tolerance Specification:
        /// - Creates SubstanceSpecification records with 40-CFR- codes
        /// - Processes enforcement analytical method information
        /// - Creates Analyte records linking to measured substances
        /// - Applies validation rules from SubstanceSpecificationCodeValidation and EnforcementMethodCodeValidation
        /// </remarks>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="Analyte"/>
        /// <seealso cref="getOrCreateSubstanceSpecificationAsync"/>
        /// <seealso cref="getOrCreateAnalyteAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveSubstanceSpecificationsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Validate input parameters and context
            if (!validateParseContext(context, section))
            {
                return count;
            }

            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            // Navigate to substance specification elements: section > subject > identifiedSubstance > subjectOf > substanceSpecification
            var substanceSpecElements = sectionEl.SplElements(sc.E.Subject, sc.E.IdentifiedSubstance, sc.E.SubjectOf, sc.E.SubstanceSpecification);

            foreach (var substanceSpecEl in substanceSpecElements)
            {
                // Extract substance specification code information (SPL IG 19.2.3.8-19.2.3.9)
                var specCodeEl = substanceSpecEl.GetSplElement(sc.E.Code);
                var specCode = specCodeEl?.GetAttrVal(sc.A.CodeValue);
                var specCodeSystem = specCodeEl?.GetAttrVal(sc.A.CodeSystem);

                // Skip if missing required specification code
                if (string.IsNullOrWhiteSpace(specCode))
                {
                    context.Logger?.LogWarning("Found substance specification without code in section {SectionID}", section.SectionID);
                    continue;
                }

                // First, get or create the IdentifiedSubstance for this specification
                var identifiedSubstance = await getOrCreateIdentifiedSubstanceForSpecificationAsync(sectionEl, section, context, dbContext);
                if (identifiedSubstance?.IdentifiedSubstanceID == null)
                {
                    context.Logger?.LogWarning("Could not create IdentifiedSubstance for specification {SpecCode} in section {SectionID}", specCode, section.SectionID);
                    continue;
                }

                // Extract enforcement analytical method information from observation element (SPL IG 19.2.3.10-19.2.3.12)
                var observationEl = substanceSpecEl.SplElement(sc.E.Component, sc.E.ObservationMedia) ??
                                   substanceSpecEl.SplElement(sc.E.Component, "observation");

                string? enforcementMethodCode = null;
                string? enforcementMethodCodeSystem = null;
                string? enforcementMethodDisplayName = null;

                if (observationEl != null)
                {
                    var methodCodeEl = observationEl.GetSplElement(sc.E.Code);
                    enforcementMethodCode = methodCodeEl?.GetAttrVal(sc.A.CodeValue);
                    enforcementMethodCodeSystem = methodCodeEl?.GetAttrVal(sc.A.CodeSystem);
                    enforcementMethodDisplayName = methodCodeEl?.GetAttrVal(sc.A.DisplayName);
                }

                // Create the SubstanceSpecification record
                var substanceSpec = await getOrCreateSubstanceSpecificationAsync(
                    dbContext,
                    identifiedSubstance.IdentifiedSubstanceID,
                    specCode,
                    specCodeSystem,
                    enforcementMethodCode,
                    enforcementMethodCodeSystem,
                    enforcementMethodDisplayName);
                count++;

                if (substanceSpec?.SubstanceSpecificationID == null) continue;

                // Process analytes (SPL IG 19.2.3.13-19.2.3.15)
                count += await processAnalytesAsync(observationEl, substanceSpec, context, dbContext);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets or creates an IdentifiedSubstance entity for a tolerance specification based on the main subject.
        /// </summary>
        /// <param name="sectionEl">The parent section XElement.</param>
        /// <param name="section">The Section entity.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="dbContext">The database context.</param>
        /// <returns>The existing or newly created IdentifiedSubstance entity.</returns>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="getOrCreateIdentifiedSubstanceAsync"/>
        /// <seealso cref="Label"/>
        private async Task<IdentifiedSubstance?> getOrCreateIdentifiedSubstanceForSpecificationAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context,
            ApplicationDbContext dbContext)
        {
            #region implementation
            // Extract the main identified substance from the subject
            var identifiedSubstanceEl = sectionEl.SplElement(sc.E.Subject, sc.E.IdentifiedSubstance, sc.E.IdentifiedSubstance);
            if (identifiedSubstanceEl == null) return null;

            var codeEl = identifiedSubstanceEl.GetSplElement(sc.E.Code);
            var identifier = codeEl?.GetAttrVal(sc.A.CodeValue);
            var systemOid = codeEl?.GetAttrVal(sc.A.CodeSystem);

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(systemOid))
            {
                return null;
            }

            // Determine subject type based on system OID (SPL IG 19.2.3.2-19.2.3.4)
            string subjectType = systemOid == "2.16.840.1.113883.4.9" ? "ActiveMoiety" : "SubstanceSpecification";

            return await getOrCreateIdentifiedSubstanceAsync(
                dbContext,
                section.SectionID,
                subjectType,
                identifier,
                systemOid,
                false); // Not a definition for tolerance specifications
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes analyte elements within an observation, creating Analyte records for each measured substance.
        /// </summary>
        /// <param name="observationEl">The observation XElement containing analyte information.</param>
        /// <param name="substanceSpec">The parent SubstanceSpecification entity.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="dbContext">The database context.</param>
        /// <returns>The count of Analyte records created.</returns>
        /// <seealso cref="Analyte"/>
        /// <seealso cref="getOrCreateAnalyteAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processAnalytesAsync(
            XElement? observationEl,
            SubstanceSpecification substanceSpec,
            SplParseContext context,
            ApplicationDbContext dbContext)
        {
            #region implementation
            int count = 0;

            if (observationEl == null || substanceSpec.SubstanceSpecificationID == null) return count;

            // Find all analyte elements within the observation (SPL IG 19.2.3.13)
            var analyteElements = observationEl.SplElements(sc.E.Analyte);

            foreach (var analyteEl in analyteElements)
            {
                // Extract the identified substance within the analyte
                var analyteSubstanceEl = analyteEl.SplElement(sc.E.IdentifiedSubstance, sc.E.IdentifiedSubstance);
                if (analyteSubstanceEl == null) continue;

                var codeEl = analyteSubstanceEl.GetSplElement(sc.E.Code);
                var identifier = codeEl?.GetAttrVal(sc.A.CodeValue);
                var systemOid = codeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(identifier)) continue;

                // Get or create the IdentifiedSubstance for this analyte
                var analyteSubstance = await getOrCreateIdentifiedSubstanceAsync(
                    dbContext,
                    context.CurrentSection?.SectionID,
                    "Analyte",
                    identifier,
                    systemOid,
                    false);

                if (analyteSubstance?.IdentifiedSubstanceID == null) continue;

                // Create the Analyte link
                await getOrCreateAnalyteAsync(
                    dbContext,
                    substanceSpec.SubstanceSpecificationID,
                    analyteSubstance.IdentifiedSubstanceID);
                count++;
            }

            return count;
            #endregion
        }

        #endregion

        #region Observation Criteria Processing (SPL IG 19.2.4)

        /**************************************************************/
        /// <summary>
        /// Parses the [referenceRange][observationCriterion] elements within substance specifications
        /// for tolerance range and commodity information according to SPL Implementation Guide Section 19.2.4.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of ObservationCriterion and related records created.</returns>
        /// <remarks>
        /// Handles the parsing of tolerance ranges and commodities as defined in 19.2.4 Tolerance Range and Commodity:
        /// - Creates ObservationCriterion records with tolerance high values and units
        /// - Processes commodity information for tolerance specifications
        /// - Creates ApplicationType records for approval information
        /// - Applies validation rules from ObservationCriterionConsistencyValidation and related validators
        /// </remarks>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Commodity"/>
        /// <seealso cref="ApplicationType"/>
        /// <seealso cref="getOrCreateObservationCriterionAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveObservationCriteriaAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            if (!validateParseContext(context, section))
            {
                return count;
            }

            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            // Navigate to observation elements within substance specifications
            var observationElements = sectionEl.SplElements(sc.E.Subject, sc.E.IdentifiedSubstance, sc.E.SubjectOf, sc.E.SubstanceSpecification, sc.E.Component, "observation");

            foreach (var observationEl in observationElements)
            {
                // Find reference range elements (SPL IG 19.2.4.1)
                var referenceRangeElements = observationEl.SplElements(sc.E.ReferenceRange);

                foreach (var refRangeEl in referenceRangeElements)
                {
                    var observationCriterionEl = refRangeEl.GetSplElement(sc.E.ObservationCriterion);
                    if (observationCriterionEl == null) continue;

                    // Extract tolerance value information (SPL IG 19.2.4.2-19.2.4.7)
                    var valueEl = observationCriterionEl.GetSplElement(sc.E.Value);
                    var highEl = valueEl?.GetSplElement(sc.E.High);

                    decimal? toleranceHighValue = null;
                    string? toleranceHighUnit = null;

                    if (highEl != null)
                    {
                        var valueStr = highEl.GetAttrVal(sc.A.Value);
                        toleranceHighValue = Util.ParseNullableDecimal(valueStr);
                        toleranceHighUnit = highEl.GetAttrVal(sc.A.Unit);
                    }

                    // Skip if no valid tolerance value found
                    if (!toleranceHighValue.HasValue)
                    {
                        context.Logger?.LogWarning("Found observation criterion without valid tolerance value in section {SectionID}", section.SectionID);
                        continue;
                    }

                    // Get the parent SubstanceSpecification (required for FK relationship)
                    var substanceSpec = await getParentSubstanceSpecificationAsync(observationEl, dbContext);
                    if (substanceSpec?.SubstanceSpecificationID == null)
                    {
                        context.Logger?.LogWarning("Could not find parent SubstanceSpecification for observation criterion in section {SectionID}", section.SectionID);
                        continue;
                    }

                    // Process commodity information (SPL IG 19.2.4.8-19.2.4.12)
                    var commodity = await processCommodityAsync(observationCriterionEl, context, dbContext);

                    // Process application type information (SPL IG 19.2.4.13-19.2.4.17)
                    var applicationType = await processApplicationTypeAsync(observationCriterionEl, context, dbContext);

                    // Extract expiration date and text note (SPL IG 19.2.4.18-19.2.4.22)
                    var expirationDate = extractExpirationDate(observationCriterionEl);
                    var textNote = extractTextNote(observationCriterionEl);

                    // Create the ObservationCriterion record
                    var observationCriterion = await getOrCreateObservationCriterionAsync(
                        dbContext,
                        substanceSpec.SubstanceSpecificationID,
                        toleranceHighValue,
                        toleranceHighUnit,
                        commodity?.CommodityID,
                        applicationType?.ApplicationTypeID,
                        expirationDate,
                        textNote);
                    count++;

                    // Count associated records
                    if (commodity != null) count++;
                    if (applicationType != null) count++;
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the parent SubstanceSpecification for an observation element by traversing the XML hierarchy.
        /// </summary>
        /// <param name="observationEl">The observation XElement.</param>
        /// <param name="dbContext">The database context.</param>
        /// <returns>The parent SubstanceSpecification entity, or null if not found.</returns>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="Label"/>
        private async Task<SubstanceSpecification?> getParentSubstanceSpecificationAsync(
            XElement observationEl,
            ApplicationDbContext dbContext)
        {
            #region implementation
            // Navigate up to find the parent substance specification
            var substanceSpecEl = observationEl.Parent?.Parent; // component -> substanceSpecification
            if (substanceSpecEl?.Name.LocalName != sc.E.SubstanceSpecification) return null;

            // Extract the specification code to find the matching record
            var codeEl = substanceSpecEl.GetSplElement(sc.E.Code);
            var specCode = codeEl?.GetAttrVal(sc.A.CodeValue);

            if (string.IsNullOrWhiteSpace(specCode)) return null;

            // Find the existing SubstanceSpecification record
            return await dbContext.Set<SubstanceSpecification>()
                .FirstOrDefaultAsync(ss => ss.SpecCode == specCode);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes commodity information from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="dbContext">The database context.</param>
        /// <returns>The existing or newly created Commodity entity, or null if no commodity found.</returns>
        /// <seealso cref="Commodity"/>
        /// <seealso cref="getOrCreateCommodityAsync"/>
        /// <seealso cref="Label"/>
        private async Task<Commodity?> processCommodityAsync(
            XElement observationCriterionEl,
            SplParseContext context,
            ApplicationDbContext dbContext)
        {
            #region implementation
            // Look for commodity information in subject > presentSubstance (SPL IG 19.2.4.8)
            var commodityEl = observationCriterionEl.SplElement(sc.E.Subject, sc.E.PresentSubstance, sc.E.PresentSubstance);
            if (commodityEl == null) return null;

            var codeEl = commodityEl.GetSplElement(sc.E.Code);
            var commodityCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            var commodityCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            var commodityDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);
            var commodityName = commodityEl.GetSplElementVal(sc.E.Name);

            if (string.IsNullOrWhiteSpace(commodityCode)) return null;

            return await getOrCreateCommodityAsync(
                dbContext,
                commodityCode,
                commodityCodeSystem,
                commodityDisplayName,
                commodityName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes application type information from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="dbContext">The database context.</param>
        /// <returns>The existing or newly created ApplicationType entity, or null if no application type found.</returns>
        /// <seealso cref="ApplicationType"/>
        /// <seealso cref="getOrCreateApplicationTypeAsync"/>
        /// <seealso cref="Label"/>
        private async Task<ApplicationType?> processApplicationTypeAsync(
            XElement observationCriterionEl,
            SplParseContext context,
            ApplicationDbContext dbContext)
        {
            #region implementation
            // Look for application type information in subjectOf > approval (SPL IG 19.2.4.13)
            var approvalEl = observationCriterionEl.SplElement(sc.E.SubjectOf, sc.E.Approval);
            if (approvalEl == null) return null;

            var codeEl = approvalEl.GetSplElement(sc.E.Code);
            var appTypeCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            var appTypeCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            var appTypeDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

            if (string.IsNullOrWhiteSpace(appTypeCode)) return null;

            return await getOrCreateApplicationTypeAsync(
                dbContext,
                appTypeCode,
                appTypeCodeSystem,
                appTypeDisplayName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts expiration or revocation date from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <returns>The parsed expiration date, or null if not found or invalid.</returns>
        /// <remarks>
        /// Implements SPL Implementation Guide Section 19.2.4.18-19.2.4.20 requirements for 
        /// expiration or revocation date extraction with proper YYYYMMDD format validation.
        /// </remarks>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="ToleranceExpirationDateValidationAttribute"/>
        /// <seealso cref="Label"/>
        private DateTime? extractExpirationDate(XElement observationCriterionEl)
        {
            #region implementation
            // Look for expiration date in subjectOf > approval > effectiveTime > high (SPL IG 19.2.4.18-19.2.4.20)
            // Now uses the 5-parameter GetSplElementAttrVal overload: E1, E2, E3, E4, A
            var expirationDateStr = observationCriterionEl.GetSplElementAttrVal(
                sc.E.SubjectOf,           // E1
                sc.E.Approval,            // E2  
                sc.E.EffectiveTime,       // E3
                sc.E.High,                // E4
                sc.A.Value);              // A (attribute)

            return Util.ParseNullableDateTime(expirationDateStr ?? string.Empty);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts text note from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <returns>The text note content, sanitized for XSS protection, or null if not found.</returns>
        /// <remarks>
        /// Implements SPL Implementation Guide Section 19.2.4.22 requirements for optional 
        /// text annotation extraction with proper HTML sanitization.
        /// </remarks>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="ToleranceTextNoteValidationAttribute"/>
        /// <seealso cref="Label"/>
        private string? extractTextNote(XElement observationCriterionEl)
        {
            #region implementation
            // Look for text note in subjectOf > approval > text (SPL IG 19.2.4.22)
            // Now uses the 3-parameter GetSplElementVal overload: E1, E2, E3
            var textContent = observationCriterionEl.GetSplElementVal(
                sc.E.SubjectOf,           // E1
                sc.E.Approval,            // E2
                sc.E.Text);               // E3

            // Apply HTML sanitization as per existing patterns in the codebase
            return textContent?.RemoveHtmlXss();
            #endregion
        }

        // ALTERNATIVE: If you prefer to use the SplElement method for more complex navigation
        // These methods demonstrate alternative approaches using the existing SplElement helper

        /**************************************************************/
        /// <summary>
        /// Alternative implementation using SplElement for more complex navigation.
        /// Extracts expiration or revocation date from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <returns>The parsed expiration date, or null if not found or invalid.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        private DateTime? extractExpirationDateAlternative(XElement observationCriterionEl)
        {
            #region implementation
            // Alternative approach using SplElement for deep navigation
            var highElement = observationCriterionEl.SplElement(
                sc.E.SubjectOf,
                sc.E.Approval,
                sc.E.EffectiveTime,
                sc.E.High);

            var expirationDateStr = highElement?.GetAttrVal(sc.A.Value);
            return Util.ParseNullableDateTime(expirationDateStr ?? string.Empty);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Alternative implementation using SplElement for navigation.
        /// Extracts text note from an observation criterion element.
        /// </summary>
        /// <param name="observationCriterionEl">The observation criterion XElement.</param>
        /// <returns>The text note content, sanitized for XSS protection, or null if not found.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        private string? extractTextNoteAlternative(XElement observationCriterionEl)
        {
            #region implementation
            // Alternative approach using SplElement for navigation
            var textElement = observationCriterionEl.SplElement(
                sc.E.SubjectOf,
                sc.E.Approval,
                sc.E.Text);

            return textElement?.Value?.RemoveHtmlXss();
            #endregion
        }

        #endregion

        #region Validation and Utilities

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context and section parameters.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="section">The section to validate.</param>
        /// <returns>True if validation passes, false otherwise.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private bool validateParseContext(SplParseContext context, Section section)
        {
            #region implementation
            // Check for required context components
            return context?.ServiceProvider != null &&
                   context.Logger != null &&
                   section.SectionID.HasValue;
            #endregion
        }

        #endregion

        #region Database Entity Creation Methods

        /**************************************************************/
        /// <summary>
        /// Gets an existing IdentifiedSubstance or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Section.</param>
        /// <param name="subjectType">The type of subject (e.g., "ActiveMoiety", "SubstanceSpecification").</param>
        /// <param name="identifierValue">The identifier value (UNII or substance code).</param>
        /// <param name="identifierSystemOid">The OID for the identifier system.</param>
        /// <param name="isDefinition">Flag indicating if this is a definition record.</param>
        /// <returns>The existing or newly created IdentifiedSubstance entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate indexing records within a section.
        /// Uniqueness is determined by the combination of the SectionID, identifier value, and system OID.
        /// </remarks>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<IdentifiedSubstance> getOrCreateIdentifiedSubstanceAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? subjectType,
            string? identifierValue,
            string? identifierSystemOid,
            bool? isDefinition)
        {
            #region implementation
            // Search for an existing record with the same key identifiers
            // Deduplication based on SectionID, identifier value, and system OID
            var existing = await dbContext.Set<IdentifiedSubstance>().FirstOrDefaultAsync(i =>
                i.SectionID == sectionId &&
                i.SubstanceIdentifierValue == identifierValue &&
                i.SubstanceIdentifierSystemOID == identifierSystemOid);

            // If a record already exists, return it to avoid creating duplicates
            if (existing != null)
            {
                return existing;
            }

            // Create a new IdentifiedSubstance entity with the provided indexing data
            var newIdentifiedSubstance = new IdentifiedSubstance
            {
                SectionID = sectionId,
                SubjectType = subjectType,
                SubstanceIdentifierValue = identifierValue,
                SubstanceIdentifierSystemOID = identifierSystemOid,
                IsDefinition = isDefinition
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<IdentifiedSubstance>().Add(newIdentifiedSubstance);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted identified substance
            return newIdentifiedSubstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing SubstanceSpecification or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="identifiedSubstanceId">The ID of the parent IdentifiedSubstance.</param>
        /// <param name="specCode">The specification code (40-CFR- format).</param>
        /// <param name="specCodeSystem">The specification code system OID.</param>
        /// <param name="enforcementMethodCode">The enforcement analytical method code.</param>
        /// <param name="enforcementMethodCodeSystem">The enforcement method code system OID.</param>
        /// <param name="enforcementMethodDisplayName">The enforcement method display name.</param>
        /// <returns>The existing or newly created SubstanceSpecification entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate substance specification records.
        /// Uniqueness is determined by the IdentifiedSubstanceID and specification code combination.
        /// </remarks>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<SubstanceSpecification> getOrCreateSubstanceSpecificationAsync(
            ApplicationDbContext dbContext,
            int? identifiedSubstanceId,
            string? specCode,
            string? specCodeSystem,
            string? enforcementMethodCode,
            string? enforcementMethodCodeSystem,
            string? enforcementMethodDisplayName)
        {
            #region implementation
            // Search for existing specification with same identified substance and spec code
            var existing = await dbContext.Set<SubstanceSpecification>().FirstOrDefaultAsync(ss =>
                ss.IdentifiedSubstanceID == identifiedSubstanceId &&
                ss.SpecCode == specCode);

            if (existing != null)
            {
                return existing;
            }

            // Create new SubstanceSpecification entity with validation
            var newSpecification = new SubstanceSpecification
            {
                IdentifiedSubstanceID = identifiedSubstanceId,
                SpecCode = specCode,
                SpecCodeSystem = specCodeSystem,
                EnforcementMethodCode = enforcementMethodCode,
                EnforcementMethodCodeSystem = enforcementMethodCodeSystem,
                EnforcementMethodDisplayName = enforcementMethodDisplayName
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<SubstanceSpecification>().Add(newSpecification);
            await dbContext.SaveChangesAsync();

            return newSpecification;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Analyte or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="substanceSpecificationId">The ID of the parent SubstanceSpecification.</param>
        /// <param name="analyteSubstanceId">The ID of the IdentifiedSubstance being measured.</param>
        /// <returns>The existing or newly created Analyte entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate analyte records.
        /// Uniqueness is determined by the combination of SubstanceSpecificationID and AnalyteSubstanceID.
        /// </remarks>
        /// <seealso cref="Analyte"/>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<Analyte> getOrCreateAnalyteAsync(
            ApplicationDbContext dbContext,
            int? substanceSpecificationId,
            int? analyteSubstanceId)
        {
            #region implementation
            // Search for existing analyte with same specification and substance IDs
            var existing = await dbContext.Set<Analyte>().FirstOrDefaultAsync(a =>
                a.SubstanceSpecificationID == substanceSpecificationId &&
                a.AnalyteSubstanceID == analyteSubstanceId);

            if (existing != null)
            {
                return existing;
            }

            // Create new Analyte entity with validation
            var newAnalyte = new Analyte
            {
                SubstanceSpecificationID = substanceSpecificationId,
                AnalyteSubstanceID = analyteSubstanceId
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<Analyte>().Add(newAnalyte);
            await dbContext.SaveChangesAsync();

            return newAnalyte;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Commodity or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="commodityCode">The commodity code value.</param>
        /// <param name="commodityCodeSystem">The commodity code system OID.</param>
        /// <param name="commodityDisplayName">The commodity display name.</param>
        /// <param name="commodityName">The optional commodity name.</param>
        /// <returns>The existing or newly created Commodity entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate commodity records.
        /// Uniqueness is determined by the combination of commodity code and code system.
        /// </remarks>
        /// <seealso cref="Commodity"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<Commodity> getOrCreateCommodityAsync(
            ApplicationDbContext dbContext,
            string? commodityCode,
            string? commodityCodeSystem,
            string? commodityDisplayName,
            string? commodityName)
        {
            #region implementation
            // Search for existing commodity with same code and system
            var existing = await dbContext.Set<Commodity>().FirstOrDefaultAsync(c =>
                c.CommodityCode == commodityCode &&
                c.CommodityCodeSystem == commodityCodeSystem);

            if (existing != null)
            {
                return existing;
            }

            // Create new Commodity entity with validation
            var newCommodity = new Commodity
            {
                CommodityCode = commodityCode,
                CommodityCodeSystem = commodityCodeSystem,
                CommodityDisplayName = commodityDisplayName,
                CommodityName = commodityName
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<Commodity>().Add(newCommodity);
            await dbContext.SaveChangesAsync();

            return newCommodity;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ApplicationType or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="appTypeCode">The application type code value.</param>
        /// <param name="appTypeCodeSystem">The application type code system OID.</param>
        /// <param name="appTypeDisplayName">The application type display name.</param>
        /// <returns>The existing or newly created ApplicationType entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate application type records.
        /// Uniqueness is determined by the combination of application type code and code system.
        /// </remarks>
        /// <seealso cref="ApplicationType"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<ApplicationType> getOrCreateApplicationTypeAsync(
            ApplicationDbContext dbContext,
            string? appTypeCode,
            string? appTypeCodeSystem,
            string? appTypeDisplayName)
        {
            #region implementation
            // Search for existing application type with same code and system
            var existing = await dbContext.Set<ApplicationType>().FirstOrDefaultAsync(at =>
                at.AppTypeCode == appTypeCode &&
                at.AppTypeCodeSystem == appTypeCodeSystem);

            if (existing != null)
            {
                return existing;
            }

            // Create new ApplicationType entity with validation
            var newApplicationType = new ApplicationType
            {
                AppTypeCode = appTypeCode,
                AppTypeCodeSystem = appTypeCodeSystem,
                AppTypeDisplayName = appTypeDisplayName
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<ApplicationType>().Add(newApplicationType);
            await dbContext.SaveChangesAsync();

            return newApplicationType;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ObservationCriterion or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="substanceSpecificationId">The ID of the parent SubstanceSpecification.</param>
        /// <param name="toleranceHighValue">The upper limit of the tolerance range in ppm.</param>
        /// <param name="toleranceHighUnit">The tolerance unit (should be [ppm]).</param>
        /// <param name="commodityId">The optional link to the specific commodity.</param>
        /// <param name="applicationTypeId">The link to the type of application.</param>
        /// <param name="expirationDate">The optional expiration or revocation date.</param>
        /// <param name="textNote">The optional text annotation about the tolerance.</param>
        /// <returns>The existing or newly created ObservationCriterion entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate observation criterion records.
        /// Uniqueness is determined by the combination of SubstanceSpecificationID and tolerance value.
        /// </remarks>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<ObservationCriterion> getOrCreateObservationCriterionAsync(
            ApplicationDbContext dbContext,
            int? substanceSpecificationId,
            decimal? toleranceHighValue,
            string? toleranceHighUnit,
            int? commodityId,
            int? applicationTypeId,
            DateTime? expirationDate,
            string? textNote)
        {
            #region implementation
            // Search for existing observation criterion with same specification and tolerance value
            var existing = await dbContext.Set<ObservationCriterion>().FirstOrDefaultAsync(oc =>
                oc.SubstanceSpecificationID == substanceSpecificationId &&
                oc.ToleranceHighValue == toleranceHighValue &&
                oc.CommodityID == commodityId);

            if (existing != null)
            {
                return existing;
            }

            // Create new ObservationCriterion entity with validation
            var newObservationCriterion = new ObservationCriterion
            {
                SubstanceSpecificationID = substanceSpecificationId,
                ToleranceHighValue = toleranceHighValue,
                ToleranceHighUnit = toleranceHighUnit,
                CommodityID = commodityId,
                ApplicationTypeID = applicationTypeId,
                ExpirationDate = expirationDate,
                TextNote = textNote
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<ObservationCriterion>().Add(newObservationCriterion);
            await dbContext.SaveChangesAsync();

            return newObservationCriterion;
            #endregion
        }

        #endregion
    }
}