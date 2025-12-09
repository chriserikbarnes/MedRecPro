using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using Microsoft.EntityFrameworkCore;
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
    /// Specialized parser for handling indexing-related elements within SPL sections.
    /// Processes various types of indexing including pharmacologic class indexing,
    /// billing unit indexing, product concept indexing, drug interactions, and NCT links.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex indexing structures defined in SPL specifications,
    /// creating appropriate cross-reference and classification entities to support
    /// regulatory compliance and data interoperability requirements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="IdentifiedSubstance"/>
    /// <seealso cref="PharmacologicClass"/>
    /// <seealso cref="BillingUnitIndex"/>
    /// <seealso cref="ProductConcept"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionIndexingParser : ISplSectionParser
    {
        #region Private Fields and Helper Classes
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /**************************************************************/
        /// <summary>
        /// Represents the main subject information extracted from an identified substance.
        /// </summary>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Label"/>
        private class MainSubjectInfo
        {
            #region implementation
            /// <summary>
            /// Gets or sets the identifier code value.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? Identifier { get; set; }

            /// <summary>
            /// Gets or sets the system OID.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? SystemOid { get; set; }

            /// <summary>
            /// Gets or sets the display name.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? DisplayName { get; set; }

            /// <summary>
            /// Gets or sets the subject type (ActiveMoiety or PharmacologicClass).
            /// </summary>
            /// <seealso cref="Label"/>
            public string? SubjectType { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is a definition.
            /// </summary>
            /// <seealso cref="Label"/>
            public bool IsDefinition { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents pharmacologic class information extracted from a specialized kind element.
        /// </summary>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="Label"/>
        private class PharmacologicClassInfo
        {
            #region implementation
            /// <summary>
            /// Gets or sets the pharmacologic class code.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? Code { get; set; }

            /// <summary>
            /// Gets or sets the pharmacologic class system OID.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? System { get; set; }

            /// <summary>
            /// Gets or sets the pharmacologic class display name.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? DisplayName { get; set; }
            #endregion
        }
        #endregion

        #region Constants
        /// <summary>
        /// Constants for database parsing operations to avoid magic strings.
        /// </summary>
        /// <seealso cref="Label"/>
        private static class DbParsingConstants
        {
            public const string EquivalentEntityLogMessage = "Created or retrieved EquivalentEntity for product {ProductId} with code {Code}";
            public const string MarketingCategoryLogMessage = "Created or retrieved MarketingCategory for approval {ApprovalId} for product {ProductId}";
            public const string MissingProductContextWarning = "Cannot process {EntityType} without current product context";
            public const string ProcessingErrorMessage = "Error processing {EntityType} for product {ProductId}";

            public const string EquivalentEntityType = "subject-level equivalent entity";
            public const string ApprovalEntityType = "subject-level approval";
        }
        #endregion

        /**************************************************************/
        public SectionIndexingParser() { }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing indexing processing.
        /// </summary>
        public string SectionName => "indexing";

        /**************************************************************/
        /// <summary>
        /// Parses indexing elements from an SPL section, processing various types of 
        /// indexing information based on document and section codes.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for indexing.</param>
        /// <param name="context">The current parsing context containing section and document information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and indexing elements created.</returns>
        /// <seealso cref="parseAndSaveIdentifiedSubstancesAsync"/>
        /// <seealso cref="parseAndSaveBillingUnitIndexAsync"/>
        /// <seealso cref="parseAndSaveProductConceptsAsync"/>
        /// <seealso cref="parseAndSaveDrugInteractionsAsync"/>
        /// <seealso cref="parseAndSaveNCTLinksAsync"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate context and current section
                if (context?.CurrentSection?.SectionID == null || context.Document == null)
                {
                    result.Success = false;
                    result.Errors.Add("No current section or document available for indexing parsing.");
                    return result;
                }

                var section = context.CurrentSection;
                reportProgress?.Invoke("Processing indexing elements...");

                // Parse Identified Substances for Pharmacologic Class Indexing
                var identifiedSubstancesCreated = await parseAndSaveIdentifiedSubstancesAsync(element, section, context);
                result.ProductElementsCreated += identifiedSubstancesCreated;

                // Parse compliance actions for FDA-initiated compliance action documents
                if (context.Document.DocumentCode == "89600-1") // FDA-INITIATED COMPLIANCE ACTION DRUG REGISTRATION AND LISTING
                {
                    var complianceResult = await parseIndexingComplianceActionsAsync(element, section, context);
                    result.ProductElementsCreated += complianceResult;
                }

                // Parse general indexing products for documents with code 73815-3
                if (context.Document.DocumentCode == "73815-3"
                    && section.SectionCode == c.WARNING_LETTER_SECTION_CODE)
                {
                    var indexingProductResult = await parseAndSaveIndexingProductsAsync(element, section, context);
                    result.ProductElementsCreated += indexingProductResult;
                }

                // Parse Billing Unit Indexing if applicable
                // Check if this is a Billing Unit Indexing section (Code 48779-3)
                // inside a Billing Unit Indexing document (Code 71446-9).
                if (context.Document.DocumentCode == "71446-9" && section.SectionCode == c.WARNING_LETTER_SECTION_CODE)
                {
                    var billingUnitResult = await parseAndSaveBillingUnitIndexAsync(element, section, context);
                    result.SectionAttributesCreated += billingUnitResult;
                }

                // Parse Product Concept Indexing if applicable
                // Check if this is a Product Concept Indexing section (Code 48779-3)
                // inside a Product Concept Indexing document (Code 71445-1).
                if (context.Document.DocumentCode == "71445-1" && section.SectionCode == c.WARNING_LETTER_SECTION_CODE)
                {
                    var conceptResult = await parseAndSaveProductConceptsAsync(element, section, context);
                    result.SectionAttributesCreated += conceptResult;
                }

                // Parse Drug Interactions Indexing if applicable
                // Check if this is a Drug Interactions Indexing document (Code 71444-4)
                if (context.Document.DocumentCode == "71444-4")
                {
                    var interactionResult = await parseAndSaveDrugInteractionsAsync(element, section, context);
                    result.SectionAttributesCreated += interactionResult;
                }

                // Parse National Clinical Trials Indexing if applicable
                // Check if this section contains NCT protocol elements
                var nctLinkResult = await parseAndSaveNCTLinksAsync(element, section, context);
                result.SectionAttributesCreated += nctLinkResult;

                reportProgress?.Invoke($"Processed {result.SectionAttributesCreated + result.ProductElementsCreated} indexing attributes");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing indexing elements: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing indexing elements for section {SectionId}", context.CurrentSection?.SectionID);
            }

            return result;
            #endregion
        }

        #region Identified Substances and Pharmacologic Class Indexing

        /**************************************************************/
        /// <summary>
        /// ENHANCED: Parses the complete substance indexing structure including moieties and characteristics.
        /// Processes both substance definitions and their associated chemical structure data.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all substance-related records created.</returns>
        /// <remarks>
        /// This enhanced version processes the complete substance definition including:
        /// - IdentifiedSubstance records
        /// - Associated Moiety components with quantity data
        /// - Characteristic records containing chemical structure data (MOLFILE, InChI, InChI-Key)
        /// Follows FDA Substance Registration System standards per ISO/FDIS 11238.
        /// </remarks>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveIdentifiedSubstancesAsync(
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

            // Extract the main identified substance element
            var identifiedSubstanceEl = extractIdentifiedSubstanceElement(sectionEl);
            if (identifiedSubstanceEl == null) return count;

            // Parse main subject information
            var mainSubjectInfo = extractMainSubjectInfo(identifiedSubstanceEl);
            if (mainSubjectInfo == null) return count;

            // Create the main identified substance record
            var mainIdentifiedSubstance = await getOrCreateIdentifiedSubstanceAsync(
                dbContext, section.SectionID, mainSubjectInfo.SubjectType,
                mainSubjectInfo.Identifier, mainSubjectInfo.SystemOid, mainSubjectInfo.IsDefinition);
            count++;

            // ENHANCED: Parse associated moieties and their characteristics
            // This is critical for substance indexing as each substance may have multiple 
            // chemical components (moieties) each with their own structural data
            count += await parseAndSaveMoietiesAsync(dbContext, identifiedSubstanceEl, mainIdentifiedSubstance, context);

            // Log summary of what was processed for this substance
            context?.Logger?.LogInformation("Substance {UNII} processing complete: {TotalRecords} total records created",
                mainSubjectInfo.Identifier, count);

            // Process based on subject type (Active Moiety or Pharmacologic Class)
            if (mainSubjectInfo.SubjectType == "ActiveMoiety")
            {
                count += await processActiveMoietyIndexing(dbContext, identifiedSubstanceEl, mainIdentifiedSubstance);
            }
            else
            {
                count += await processPharmacologicClassDefinition(dbContext, identifiedSubstanceEl, mainIdentifiedSubstance, mainSubjectInfo);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all moiety components associated with an identified substance.
        /// Processes chemical structure components including quantity ratios and molecular data.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement containing moiety data.</param>
        /// <param name="identifiedSubstance">The parent IdentifiedSubstance entity.</param>
        /// <param name="context">The parsing context for logging and services.</param>
        /// <returns>The count of moiety and characteristic records created.</returns>
        /// <remarks>
        /// Processes moiety elements that define the chemical components of a substance.
        /// Each moiety contains quantity information (ratios, units) and multiple characteristics
        /// representing different molecular structure formats (MOLFILE, InChI, InChI-Key).
        /// </remarks>
        /// <example>
        /// // XML structure being parsed:
        /// // &lt;moiety&gt;
        /// //   &lt;code code="C103243" displayName="mixture component"/&gt;
        /// //   &lt;quantity&gt;...&lt;/quantity&gt;
        /// //   &lt;subjectOf&gt;&lt;characteristic&gt;...&lt;/characteristic&gt;&lt;/subjectOf&gt;
        /// // &lt;/moiety&gt;
        /// </example>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveMoietiesAsync(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            IdentifiedSubstance identifiedSubstance,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Find all moiety elements within the identified substance
            var moietyElements = identifiedSubstanceEl.Elements(ns + sc.E.Moiety).ToList();

            context?.Logger?.LogInformation("Found {Count} moiety elements for substance {UNII}",
                moietyElements.Count, identifiedSubstance.SubstanceIdentifierValue);

            // CORRECTED: Use sequence number to distinguish moieties with same code
            int sequenceNumber = 0;
            foreach (var moietyEl in moietyElements)
            {
                try
                {
                    sequenceNumber++; // Increment for each moiety in this substance

                    // Extract moiety code information
                    var moietyCodeEl = moietyEl.GetSplElement(sc.E.Code);
                    var moietyCode = moietyCodeEl?.GetAttrVal(sc.A.CodeValue);
                    var moietyCodeSystem = moietyCodeEl?.GetAttrVal(sc.A.CodeSystem);
                    var moietyDisplayName = moietyCodeEl?.GetAttrVal(sc.A.DisplayName);

                    // Extract quantity information
                    var quantityInfo = extractMoietyQuantityInfo(moietyEl);

                    context?.Logger?.LogDebug("Processing moiety {Sequence} with code {Code} for substance {UNII}",
                        sequenceNumber, moietyCode, identifiedSubstance.SubstanceIdentifierValue);

                    // Create or get the moiety record
                    var moiety = await getOrCreateMoietyAsync(
                        dbContext,
                        identifiedSubstance.IdentifiedSubstanceID,
                        moietyCode,
                        moietyCodeSystem,
                        moietyDisplayName,
                        quantityInfo,
                        sequenceNumber);
                    count++;

                    // Parse characteristics for this moiety
                    count += await parseAndSaveCharacteristicsAsync(dbContext, moietyEl, moiety, context);

                    context?.Logger?.LogDebug("Created moiety {MoietyID} (sequence {Sequence}) with {CharCount} characteristics",
                        moiety.MoietyID, sequenceNumber, moietyEl.SplElements(sc.E.SubjectOf, sc.E.Characteristic).Count());
                }
                catch (Exception ex)
                {
                    context?.Logger?.LogError(ex, "Error parsing moiety {Sequence} for substance {UNII}",
                        sequenceNumber, identifiedSubstance.SubstanceIdentifierValue);
                    throw; // Re-throw to maintain existing error handling behavior
                }
            }

            context?.Logger?.LogInformation("Completed processing {MoietyCount} moieties for substance {UNII}: {TotalRecords} total records created",
                moietyElements.Count, identifiedSubstance.SubstanceIdentifierValue, count);

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves characteristic elements containing chemical structure data for a moiety.
        /// Processes multiple molecular representation formats including MOLFILE, InChI, and InChI-Key.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="moietyEl">The moiety XElement containing characteristic data.</param>
        /// <param name="moiety">The parent Moiety entity.</param>
        /// <param name="context">The parsing context for logging and services.</param>
        /// <returns>The count of characteristic records created.</returns>
        /// <remarks>
        /// Characteristics define the identifying properties of chemical substances.
        /// For chemical structures, this includes CDATA content containing:
        /// - MOLFILE format molecular connection tables
        /// - InChI (IUPAC International Chemical Identifier) strings
        /// - InChI-Key hash-based compact identifiers
        /// Preserves exact formatting for scientific integrity per ISO/FDIS 11238.
        /// </remarks>
        /// <example>
        /// // XML structure being parsed:
        /// // &lt;subjectOf&gt;
        /// //   &lt;characteristic&gt;
        /// //     &lt;code code="C103240" displayName="Chemical Structure"/&gt;
        /// //     &lt;value mediaType="application/x-mdl-molfile"&gt;&lt;![CDATA[...]]&gt;&lt;/value&gt;
        /// //   &lt;/characteristic&gt;
        /// // &lt;/subjectOf&gt;
        /// </example>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="XElementExtensions.GetChemicalStructureData"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveCharacteristicsAsync(
            ApplicationDbContext dbContext,
            XElement moietyEl,
            Moiety moiety,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Find all characteristic elements within subjectOf elements
            var characteristicElements = moietyEl.SplElements(sc.E.SubjectOf, sc.E.Characteristic).ToList();

            context?.Logger?.LogInformation("Found {Count} characteristics for moiety {MoietyID}",
                characteristicElements.Count, moiety.MoietyID);

            foreach (var characteristicEl in characteristicElements)
            {
                try
                {
                    // Extract characteristic code information
                    var charCodeEl = characteristicEl.GetSplElement(sc.E.Code);
                    var characteristicCode = charCodeEl?.GetAttrVal(sc.A.CodeValue);
                    var characteristicCodeSystem = charCodeEl?.GetAttrVal(sc.A.CodeSystem);

                    // Skip non-chemical structure characteristics for now
                    if (characteristicCode != "C103240") // Chemical Structure code
                    {
                        context?.Logger?.LogDebug("Skipping non-chemical structure characteristic: {Code}", characteristicCode);
                        continue;
                    }

                    // Extract chemical structure data from value element
                    var valueEl = characteristicEl.GetSplElement(sc.E.Value);
                    var structureData = valueEl?.GetChemicalStructureData();

                    if (structureData.HasValue)
                    {
                        var (mediaType, content) = structureData.Value;

                        // Create characteristic record
                        var characteristic = await getOrCreateCharacteristicAsync(
                            dbContext,
                            moiety.MoietyID,
                            characteristicCode,
                            characteristicCodeSystem,
                            "ED", // Encapsulated Data type for chemical structures
                            mediaType,
                            content);
                        count++;

                        context?.Logger?.LogDebug("Created characteristic {CharID} for moiety {MoietyID}: {MediaType} ({ContentLength} chars)",
                            characteristic.CharacteristicID, moiety.MoietyID, mediaType, content?.Length ?? 0);
                    }
                    else
                    {
                        context?.Logger?.LogWarning("No chemical structure data found in characteristic for moiety {MoietyID}",
                            moiety.MoietyID);
                    }
                }
                catch (Exception ex)
                {
                    context?.Logger?.LogError(ex, "Error parsing characteristic for moiety {MoietyID}", moiety.MoietyID);
                    throw; // Re-throw to maintain existing error handling behavior
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts quantity information from a moiety element including numerator and denominator data.
        /// Parses complex quantity structures with ranges, units, and inclusive boundaries.
        /// </summary>
        /// <param name="moietyEl">The moiety XElement containing quantity data.</param>
        /// <returns>A tuple containing parsed quantity information or null if not found.</returns>
        /// <remarks>
        /// Quantity information defines mixture ratios and proportions for chemical components.
        /// Handles complex structures including range specifications with inclusive/exclusive boundaries.
        /// </remarks>
        /// <example>
        /// // XML structure being parsed:
        /// // &lt;quantity&gt;
        /// //   &lt;numerator p3:type="URG_PQ"&gt;
        /// //     &lt;low value="0" unit="1" inclusive="false"/&gt;
        /// //   &lt;/numerator&gt;
        /// //   &lt;denominator value="1" unit="1"/&gt;
        /// // &lt;/quantity&gt;
        /// </example>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="XElementExtensions.GetInclusiveAttribute"/>
        /// <seealso cref="Label"/>
        private (decimal? numeratorLow, string? numeratorUnit, bool? inclusive, decimal? denominatorValue, string? denominatorUnit)? extractMoietyQuantityInfo(XElement moietyEl)
        {
            #region implementation
            var quantityEl = moietyEl.GetSplElement(sc.E.Quantity);
            if (quantityEl == null) return null;

            // Extract numerator information (may be a range with 'low' value)
            var numeratorEl = quantityEl.GetSplElement(sc.E.Numerator);
            decimal? numeratorLow = null;
            string? numeratorUnit = null;
            bool? inclusive = null;

            if (numeratorEl != null)
            {
                // Check for low value (range specification)
                var lowEl = numeratorEl.GetSplElement(sc.E.Low);
                if (lowEl != null)
                {
                    if (decimal.TryParse(lowEl.GetAttrVal(sc.A.Value), out decimal lowValue))
                    {
                        numeratorLow = lowValue;
                    }
                    numeratorUnit = lowEl.GetAttrVal(sc.A.Unit);
                    inclusive = lowEl.GetInclusiveAttribute();
                }
                else
                {
                    // Simple numerator value
                    if (decimal.TryParse(numeratorEl.GetAttrVal(sc.A.Value), out decimal value))
                    {
                        numeratorLow = value;
                    }
                    numeratorUnit = numeratorEl.GetAttrVal(sc.A.Unit);
                }
            }

            // Extract denominator information
            var denominatorEl = quantityEl.GetSplElement(sc.E.Denominator);
            decimal? denominatorValue = null;
            string? denominatorUnit = null;

            if (denominatorEl != null)
            {
                if (decimal.TryParse(denominatorEl.GetAttrVal(sc.A.Value), out decimal denValue))
                {
                    denominatorValue = denValue;
                }
                denominatorUnit = denominatorEl.GetAttrVal(sc.A.Unit);
            }

            return (numeratorLow, numeratorUnit, inclusive, denominatorValue, denominatorUnit);
            #endregion
        }

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

        /**************************************************************/
        /// <summary>
        /// Extracts the inner identified substance element from the section element.
        /// </summary>
        /// <param name="sectionEl">The section XElement to search within.</param>
        /// <returns>The inner identified substance XElement, or null if not found.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private XElement? extractIdentifiedSubstanceElement(XElement sectionEl)
        {
            #region implementation
            // Navigate through the SPL structure: section > subject > identifiedSubstance > identifiedSubstance
            var subjectEl = sectionEl.GetSplElement(sc.E.Subject);
            var identifiedSubstanceEl = subjectEl?.GetSplElement(sc.E.IdentifiedSubstance);

            // Return the inner identified substance element
            return identifiedSubstanceEl?.GetSplElement(sc.E.IdentifiedSubstance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the main subject information from the identified substance element.
        /// </summary>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <returns>Main subject information, or null if required data is missing.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private MainSubjectInfo? extractMainSubjectInfo(XElement identifiedSubstanceEl)
        {
            #region implementation
            // Extract code information from the identified substance
            var mainCodeEl = identifiedSubstanceEl.GetSplElement(sc.E.Code);
            var mainIdentifier = mainCodeEl?.GetAttrVal(sc.A.CodeValue);
            var mainSystemOid = mainCodeEl?.GetAttrVal(sc.A.CodeSystem);
            var mainDisplayName = mainCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(mainIdentifier) || string.IsNullOrWhiteSpace(mainSystemOid))
            {
                return null;
            }

            // Determine subject type based on system OID
            string subjectType = mainSystemOid == "2.16.840.1.113883.4.9" ? "ActiveMoiety" : "PharmacologicClass";
            bool isDefinition = subjectType == "PharmacologicClass";

            return new MainSubjectInfo
            {
                Identifier = mainIdentifier,
                SystemOid = mainSystemOid,
                DisplayName = mainDisplayName,
                SubjectType = subjectType,
                IsDefinition = isDefinition
            };
            #endregion
        }

        #region Active Moiety Indexing - Orchestrator Pattern

        /**************************************************************/
        /// <summary>
        /// Data transfer object containing extracted pharmacologic class information
        /// from an SPL XML element.
        /// </summary>
        /// <remarks>
        /// Used to pass pharmacologic class data between orchestrator and processing methods
        /// without coupling to XML element structures.
        /// </remarks>
        /// <seealso cref="processActiveMoietyIndexing"/>
        /// <seealso cref="extractPharmacologicClassFromGeneralizedKind"/>
        private class PharmacologicClassExtractionResult
        {
            #region implementation

            /// <summary>The code value identifying the pharmacologic class.</summary>
            public string? Code { get; set; }

            /// <summary>The code system OID (e.g., NDF-RT).</summary>
            public string? System { get; set; }

            /// <summary>The human-readable display name for the class.</summary>
            public string? DisplayName { get; set; }

            /// <summary>The source generalizedMaterialKind element for further processing.</summary>
            public XElement? GeneralizedKindElement { get; set; }

            /// <summary>Indicates whether the extraction yielded valid data.</summary>
            public bool IsValid => !string.IsNullOrWhiteSpace(Code);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates Active Moiety indexing (Spec 8.2.2) by coordinating the processing
        /// of pharmacologic class associations, names, links, and hierarchies.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainIdentifiedSubstance">The main identified substance entity.</param>
        /// <returns>The count of records created during processing.</returns>
        /// <remarks>
        /// This orchestrator delegates to specialized methods for:
        /// <list type="bullet">
        ///   <item>Extracting pharmacologic class information from XML</item>
        ///   <item>Creating or retrieving pharmacologic class records</item>
        ///   <item>Processing class names</item>
        ///   <item>Creating moiety-to-class links</item>
        ///   <item>Processing nested class hierarchies</item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// var count = await processActiveMoietyIndexing(dbContext, substanceElement, identifiedSubstance);
        /// </code>
        /// </example>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="processSpecializedKindElement"/>
        private async Task<int> processActiveMoietyIndexing(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            IdentifiedSubstance mainIdentifiedSubstance)
        {
            #region implementation

            int totalCount = 0;

            // Retrieve all asSpecializedKind elements using direct LINQ to XML
            // to bypass any SplElements wrapper issues
            var specializedKindElements = identifiedSubstanceEl
                .Elements(ns + sc.E.AsSpecializedKind)
                .ToList();

            Console.WriteLine($"Found {specializedKindElements.Count} asSpecializedKind elements for substance {mainIdentifiedSubstance.SubstanceIdentifierValue}");

            // Process each specialized kind element through the dedicated handler
            foreach (var specializedKindEl in specializedKindElements)
            {
                var elementCount = await processSpecializedKindElement(
                    dbContext,
                    specializedKindEl,
                    mainIdentifiedSubstance);

                totalCount += elementCount;
            }

            Console.WriteLine($"processActiveMoietyIndexing completed. Total records created: {totalCount}");
            return totalCount;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single asSpecializedKind element, extracting pharmacologic class
        /// information and creating all associated records.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="specializedKindEl">The asSpecializedKind XElement to process.</param>
        /// <param name="mainIdentifiedSubstance">The main identified substance entity.</param>
        /// <returns>The count of records created for this element.</returns>
        /// <remarks>
        /// Coordinates extraction, class creation, name processing, link creation,
        /// and hierarchy processing for a single pharmacologic class association.
        /// </remarks>
        /// <seealso cref="processActiveMoietyIndexing"/>
        /// <seealso cref="extractPharmacologicClassFromGeneralizedKind"/>
        /// <seealso cref="processPharmacologicClassNames"/>
        /// <seealso cref="createPharmacologicClassLink"/>
        /// <seealso cref="processNestedHierarchies"/>
        private async Task<int> processSpecializedKindElement(
            ApplicationDbContext dbContext,
            XElement specializedKindEl,
            IdentifiedSubstance mainIdentifiedSubstance)
        {
            #region implementation

            int count = 0;

            // Extract pharmacologic class information from the generalizedMaterialKind
            var extractionResult = extractPharmacologicClassFromGeneralizedKind(specializedKindEl);

            // Validate extraction result before proceeding
            if (extractionResult == null || !extractionResult.IsValid)
            {
                return count;
            }

            Console.WriteLine($"Processing pharmacologic class: Code={extractionResult.Code}, System={extractionResult.System}, DisplayName={extractionResult.DisplayName}");

            try
            {
                // Look up substance record by UNII to get the ID
                var substanceRec = await dbContext.Set<IdentifiedSubstance>()
                    .FirstOrDefaultAsync(s => s.SubstanceIdentifierValue == mainIdentifiedSubstance.SubstanceIdentifierValue);

                // Create or retrieve the pharmacologic class record
                var pharmClass = await getOrCreatePharmacologicClassAsync(
                    dbContext,
                    substanceRec?.IdentifiedSubstanceID,
                    extractionResult.Code,
                    extractionResult.System,
                    extractionResult.DisplayName);

                count++;
                Console.WriteLine($"Created/found PharmacologicClass ID: {pharmClass.PharmacologicClassID}");

                // Process names within the generalizedMaterialKind element
                if (extractionResult?.GeneralizedKindElement != null)                    
                    count += await processPharmacologicClassNames(
                        dbContext,
                        extractionResult.GeneralizedKindElement,
                        pharmClass.PharmacologicClassID);

                // Create link between the moiety and the pharmacologic class
                count += await createPharmacologicClassLink(
                    dbContext,
                    mainIdentifiedSubstance,
                    pharmClass.PharmacologicClassID);

                // Process any nested hierarchy relationships
                if (extractionResult?.GeneralizedKindElement != null && extractionResult?.Code != null)
                    count += await processNestedHierarchies(
                      dbContext,
                      extractionResult.GeneralizedKindElement,
                      pharmClass,
                      extractionResult.Code);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing pharmacologic class {extractionResult.Code}: {ex.Message}");
                throw; // Re-throw to maintain existing error handling behavior
            }

            return count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts pharmacologic class information from a generalizedMaterialKind element
        /// within an asSpecializedKind container.
        /// </summary>
        /// <param name="specializedKindEl">The asSpecializedKind XElement containing the class info.</param>
        /// <returns>
        /// A <see cref="PharmacologicClassExtractionResult"/> containing the extracted data,
        /// or null if required elements are missing.
        /// </returns>
        /// <remarks>
        /// Performs validation at each extraction step and logs warnings for missing elements.
        /// Returns null early if critical elements (generalizedMaterialKind or code) are absent.
        /// </remarks>
        /// <seealso cref="PharmacologicClassExtractionResult"/>
        /// <seealso cref="processSpecializedKindElement"/>
        private PharmacologicClassExtractionResult? extractPharmacologicClassFromGeneralizedKind(
            XElement specializedKindEl)
        {
            #region implementation

            // Navigate to the generalizedMaterialKind child element
            var generalizedKindEl = specializedKindEl.GetSplElement(sc.E.GeneralizedMaterialKind);
            if (generalizedKindEl == null)
            {
                Console.WriteLine("Warning: generalizedMaterialKind element not found");
                return null;
            }

            // Extract the code element containing class identifiers
            var classCodeEl = generalizedKindEl.GetSplElement(sc.E.Code);
            if (classCodeEl == null)
            {
                Console.WriteLine("Warning: code element not found in generalizedMaterialKind");
                return null;
            }

            // Extract attribute values from the code element
            var classCode = classCodeEl.GetAttrVal(sc.A.CodeValue);
            var classSystem = classCodeEl.GetAttrVal(sc.A.CodeSystem);
            var classDisplayName = classCodeEl.GetAttrVal(sc.A.DisplayName);

            // Validate the required code attribute
            if (string.IsNullOrWhiteSpace(classCode))
            {
                Console.WriteLine("Warning: code attribute is empty or missing");
                return null;
            }

            // Return populated extraction result with reference to source element
            return new PharmacologicClassExtractionResult
            {
                Code = classCode,
                System = classSystem,
                DisplayName = classDisplayName,
                GeneralizedKindElement = generalizedKindEl
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes name elements within a generalizedMaterialKind element,
        /// creating PharmacologicClassName records for each valid name.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="generalizedKindEl">The generalizedMaterialKind XElement containing names.</param>
        /// <param name="pharmacologicClassId">The ID of the parent pharmacologic class.</param>
        /// <returns>The count of name records created.</returns>
        /// <remarks>
        /// Iterates through all name child elements, extracting value and use attribute.
        /// Defaults to "A" (Alternate) for the use attribute if not specified.
        /// </remarks>
        /// <seealso cref="PharmacologicClassName"/>
        /// <seealso cref="getOrCreatePharmacologicClassNameAsync"/>
        private async Task<int> processPharmacologicClassNames(
            ApplicationDbContext dbContext,
            XElement generalizedKindEl,
            int? pharmacologicClassId)
        {
            #region implementation

            int count = 0;

            // Retrieve all name elements from the generalizedMaterialKind
            var nameElements = generalizedKindEl.Elements(ns + sc.E.Name).ToList();
            Console.WriteLine($"Found {nameElements.Count} name elements in generalizedMaterialKind");

            foreach (var nameEl in nameElements)
            {
                // Extract and clean the name value
                var nameValue = nameEl.Value?.Trim();

                // Extract use attribute, defaulting to "A" (Alternate) if not present
                var nameUse = nameEl.GetAttrVal(sc.A.Use) ?? "A";

                // Only create record if name value is non-empty
                if (!string.IsNullOrWhiteSpace(nameValue))
                {
                    await getOrCreatePharmacologicClassNameAsync(
                        dbContext,
                        pharmacologicClassId,
                        nameValue,
                        nameUse);

                    count++;
                    Console.WriteLine($"Created PharmacologicClassName: {nameValue} (use: {nameUse})");
                }
            }

            return count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates or retrieves a link between an identified substance (moiety)
        /// and a pharmacologic class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="mainIdentifiedSubstance">The identified substance to link.</param>
        /// <param name="pharmacologicClassId">The ID of the pharmacologic class.</param>
        /// <returns>1 if a link was created/found, 0 otherwise.</returns>
        /// <remarks>
        /// Performs a database lookup for the UNII on active moiety.
        /// If the substance is not found, no link is recorded and the method returns 0.
        /// </remarks>
        /// <seealso cref="PharmacologicClassLink"/>
        /// <seealso cref="getOrCreatePharmacologicClassLinkAsync"/>
        private async Task<int> createPharmacologicClassLink(
            ApplicationDbContext dbContext,
            IdentifiedSubstance mainIdentifiedSubstance,
            int? pharmacologicClassId)
        {
            #region implementation

            // Attempt to create the link between moiety and pharmacologic class
            var link = await getOrCreatePharmacologicClassLinkAsync(
                dbContext,
                mainIdentifiedSubstance.IdentifiedSubstanceID,
                pharmacologicClassId,
                mainIdentifiedSubstance.SubstanceIdentifierValue);

            // Log result based on link creation outcome
            if (link != null && link?.PharmacologicClassLinkID >= 0)
            {
                Console.WriteLine($"Created/found PharmacologicClassLink ID: {link?.PharmacologicClassLinkID}");
            }
            else
            {
                Console.WriteLine($"Created/found PharmacologicClassLink ID: No link was available for passed moiety");
            }

            // Return 1 to indicate the operation was performed
            return 1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes nested asSpecializedKind elements within a generalizedMaterialKind
        /// to establish pharmacologic class hierarchy relationships.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="generalizedKindEl">The generalizedMaterialKind element containing nested hierarchies.</param>
        /// <param name="childPharmClass">The child pharmacologic class in the hierarchy.</param>
        /// <param name="childClassCode">The code of the child class for logging purposes.</param>
        /// <returns>The count of hierarchy records created (2 per hierarchy: parent class + relationship).</returns>
        /// <remarks>
        /// Complex pharmacologic class structures may contain nested asSpecializedKind elements
        /// that define parent-child relationships. This method processes those relationships,
        /// creating parent class records and establishing hierarchy links.
        /// </remarks>
        /// <seealso cref="PharmacologicClassHierarchy"/>
        /// <seealso cref="extractPharmacologicClassInfoFromElement"/>
        /// <seealso cref="getOrCreatePharmacologicClassHierarchyAsync"/>
        private async Task<int> processNestedHierarchies(
            ApplicationDbContext dbContext,
            XElement generalizedKindEl,
            PharmacologicClass childPharmClass,
            string childClassCode)
        {
            #region implementation

            int count = 0;

            // Find all nested asSpecializedKind elements representing hierarchy relationships
            var nestedSpecializedKinds = generalizedKindEl
                .Elements(ns + sc.E.AsSpecializedKind)
                .ToList();

            foreach (var nestedKindEl in nestedSpecializedKinds)
            {
                // Extract parent class information from nested element
                var nestedInfo = extractPharmacologicClassInfoFromElement(nestedKindEl);

                if (nestedInfo != null)
                {
                    // Create or retrieve the parent pharmacologic class
                    // Note: null passed for IdentifiedSubstanceID as this is a hierarchy reference
                    var parentClass = await getOrCreatePharmacologicClassAsync(
                        dbContext,
                        null,
                        nestedInfo.Code,
                        nestedInfo.System,
                        nestedInfo.DisplayName);
                    count++;

                    // Establish the hierarchy relationship between child and parent
                    await getOrCreatePharmacologicClassHierarchyAsync(
                        dbContext,
                        childPharmClass.PharmacologicClassID,
                        parentClass.PharmacologicClassID);
                    count++;

                    Console.WriteLine($"Created hierarchy: {childClassCode} -> {nestedInfo.Code}");
                }
            }

            return count;

            #endregion
        }

        #endregion

        /**************************************************************/
        /// <summary>
        /// Helper method to extract pharmacologic class info from any element containing generalizedMaterialKind > code
        /// </summary>
        private PharmacologicClassInfo? extractPharmacologicClassInfoFromElement(XElement parentEl)
        {
            try
            {
                var generalizedKindEl = parentEl.GetSplElement(sc.E.GeneralizedMaterialKind);
                var classCodeEl = generalizedKindEl?.GetSplElement(sc.E.Code);

                if (classCodeEl == null) return null;

                var classCode = classCodeEl.GetAttrVal(sc.A.CodeValue);
                var classSystem = classCodeEl.GetAttrVal(sc.A.CodeSystem);
                var classDisplayName = classCodeEl.GetAttrVal(sc.A.DisplayName);

                if (string.IsNullOrWhiteSpace(classCode)) return null;

                return new PharmacologicClassInfo
                {
                    Code = classCode,
                    System = classSystem,
                    DisplayName = classDisplayName
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Processes Pharmacologic Class definition (Spec 8.2.3) including names and hierarchy.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainIdentifiedSubstance">The main identified substance entity.</param>
        /// <param name="mainSubjectInfo">The main subject information.</param>
        /// <returns>The count of records created during processing.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="MainSubjectInfo"/>
        /// <seealso cref="getOrCreatePharmacologicClassAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processPharmacologicClassDefinition(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            IdentifiedSubstance mainIdentifiedSubstance,
            MainSubjectInfo mainSubjectInfo)
        {
            #region implementation
            int count = 0;

            // Create the main pharmacologic class record for this definition
            var mainPharmClass = await getOrCreatePharmacologicClassAsync(
                dbContext, mainIdentifiedSubstance.IdentifiedSubstanceID,
                mainSubjectInfo.Identifier, mainSubjectInfo.SystemOid, mainSubjectInfo.DisplayName);
            count++;

            // Process all names (preferred and alternate) for this class definition
            count += await processPharmacologicClassNames(dbContext, identifiedSubstanceEl, mainPharmClass);

            // Process all defining super-classes (hierarchy)
            count += await processPharmacologicClassHierarchy(dbContext, identifiedSubstanceEl, mainPharmClass);

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all names (preferred and alternate) for a pharmacologic class definition.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainPharmClass">The main pharmacologic class entity.</param>
        /// <returns>The count of name records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="getOrCreatePharmacologicClassNameAsync"/>
        /// <seealso cref="SectionIndexingParser.processPharmacologicClassNames(ApplicationDbContext, XElement, int?)"/>
        /// <seealso cref="SectionIndexingParser.processPharmacologicClassNames(ApplicationDbContext, XElement, Label.PharmacologicClass)"/>
        private async Task<int> processPharmacologicClassNames(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            PharmacologicClass mainPharmClass)
        {
            #region implementation
            int count = 0;

            // Parse all name elements within the identified substance
            foreach (var nameEl in identifiedSubstanceEl.SplElements(sc.E.Name))
            {
                var nameValue = nameEl.Value?.Trim();
                var nameUse = nameEl.Attribute(sc.A.Use)?.Value ?? "A"; // Default to Alternate if 'use' is missing

                // Skip empty names
                if (string.IsNullOrWhiteSpace(nameValue)) continue;

                // Create the pharmacologic class name record
                await getOrCreatePharmacologicClassNameAsync(
                    dbContext, mainPharmClass.PharmacologicClassID, nameValue, nameUse);
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all defining super-classes (hierarchy) for a pharmacologic class definition.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainPharmClass">The main pharmacologic class entity.</param>
        /// <returns>The count of hierarchy records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="getOrCreatePharmacologicClassAsync"/>
        /// <seealso cref="getOrCreatePharmacologicClassHierarchyAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processPharmacologicClassHierarchy(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            PharmacologicClass mainPharmClass)
        {
            #region implementation
            int count = 0;

            // Process each specialized kind (parent class relationship)
            foreach (var specializedKindEl in identifiedSubstanceEl.SplElements(sc.E.AsSpecializedKind))
            {
                // Extract parent class information
                var parentClassInfo = extractPharmacologicClassInfo(specializedKindEl);
                if (parentClassInfo == null) continue;

                // Create or get the referenced parent pharmacologic class
                var parentPharmClass = await getOrCreatePharmacologicClassAsync(
                    dbContext, null, parentClassInfo.Code, parentClassInfo.System, parentClassInfo.DisplayName);
                count++;

                // Create the hierarchy link
                await getOrCreatePharmacologicClassHierarchyAsync(
                    dbContext, mainPharmClass.PharmacologicClassID, parentPharmClass.PharmacologicClassID);
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts pharmacologic class information from a specialized kind element.
        /// </summary>
        /// <param name="specializedKindEl">The specialized kind XElement.</param>
        /// <returns>Pharmacologic class information, or null if required data is missing.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private PharmacologicClassInfo? extractPharmacologicClassInfo(XElement specializedKindEl)
        {
            #region implementation
            // Navigate to the code element within the generalized material kind
            var classCodeEl = specializedKindEl.SplElement(sc.E.GeneralizedMaterialKind, sc.E.Code);
            var classCode = classCodeEl?.GetAttrVal(sc.A.CodeValue);
            var classSystem = classCodeEl?.GetAttrVal(sc.A.CodeSystem);
            var classDisplayName = classCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Validate required code value
            if (string.IsNullOrWhiteSpace(classCode))
            {
                return null;
            }

            return new PharmacologicClassInfo
            {
                Code = classCode,
                System = classSystem,
                DisplayName = classDisplayName
            };
            #endregion
        }

        #endregion

        #region Database Entity Creation Methods

        /**************************************************************/
        /// <summary>
        /// Gets an existing Moiety or creates and saves it if not found.
        /// CORRECTED: Modified deduplication to allow multiple moieties with same code but different structures.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="identifiedSubstanceId">The ID of the parent IdentifiedSubstance.</param>
        /// <param name="moietyCode">The code identifying the moiety type.</param>
        /// <param name="moietyCodeSystem">The code system for the moiety code.</param>
        /// <param name="moietyDisplayName">The display name for the moiety code.</param>
        /// <param name="quantityInfo">Tuple containing quantity information.</param>
        /// <param name="sequenceNumber">Sequence number to distinguish moieties with same code.</param>
        /// <returns>The existing or newly created Moiety entity.</returns>
        /// <remarks>
        /// Creates moiety records representing chemical components within substance definitions.
        /// ENHANCED: Multiple moieties can have the same type code (e.g., "mixture component") 
        /// but represent different chemical structures. Uses sequence-based deduplication 
        /// to allow multiple moieties with the same code within a single substance.
        /// </remarks>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Moiety> getOrCreateMoietyAsync(
            ApplicationDbContext dbContext,
            int? identifiedSubstanceId,
            string? moietyCode,
            string? moietyCodeSystem,
            string? moietyDisplayName,
            (decimal? numeratorLow, string? numeratorUnit, bool? inclusive, decimal? denominatorValue, string? denominatorUnit)? quantityInfo,
            int sequenceNumber)
        {
            #region implementation
            // Deduplication that includes quantity information
            // to distinguish between moieties with same code but different structures
            var numLow = quantityInfo?.numeratorLow ?? null;
            var numUnit = quantityInfo?.numeratorUnit ?? null;
            var numDenom = quantityInfo?.denominatorValue ?? null;
            var existing = await dbContext.Set<Moiety>().FirstOrDefaultAsync(m =>
                m.IdentifiedSubstanceID == identifiedSubstanceId &&
                m.MoietyCode == moietyCode &&
                m.MoietyCodeSystem == moietyCodeSystem &&
                m.QuantityNumeratorLowValue == numLow &&
                m.QuantityNumeratorUnit == numUnit &&
                m.QuantityDenominatorValue == numDenom &&
                m.SequenceNumber == sequenceNumber);

            if (existing != null)
            {
                return existing;
            }

            // Create new Moiety entity with provided data
            var newMoiety = new Moiety
            {
                IdentifiedSubstanceID = identifiedSubstanceId,
                MoietyCode = moietyCode,
                MoietyCodeSystem = moietyCodeSystem,
                MoietyDisplayName = moietyDisplayName,
                SequenceNumber = sequenceNumber
            };


            // Add quantity information if provided
            if (quantityInfo.HasValue)
            {
                var (numeratorLow, numeratorUnit, inclusive, denominatorValue, denominatorUnit) = quantityInfo.Value;
                newMoiety.QuantityNumeratorLowValue = numeratorLow;
                newMoiety.QuantityNumeratorUnit = numeratorUnit;
                newMoiety.QuantityNumeratorInclusive = inclusive;
                newMoiety.QuantityDenominatorValue = denominatorValue;
                newMoiety.QuantityDenominatorUnit = denominatorUnit;
            }

            // Save the new moiety to the database
            dbContext.Set<Moiety>().Add(newMoiety);
            await dbContext.SaveChangesAsync();

            return newMoiety;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing Characteristic or creates and saves it if not found.
        /// Implements deduplication based on moiety ID, characteristic code, and media type.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="moietyId">The ID of the parent Moiety.</param>
        /// <param name="characteristicCode">The code identifying the characteristic type.</param>
        /// <param name="characteristicCodeSystem">The code system for the characteristic.</param>
        /// <param name="valueType">The XML schema type (e.g., "ED" for encapsulated data).</param>
        /// <param name="mediaType">The media type for chemical structure data.</param>
        /// <param name="cdataContent">The raw CDATA content containing chemical structure data.</param>
        /// <returns>The existing or newly created Characteristic entity.</returns>
        /// <remarks>
        /// Creates characteristic records for chemical structure data preservation.
        /// Maintains exact formatting of MOLFILE, InChI, and InChI-Key data for scientific integrity.
        /// No HTML sanitization is applied to chemical data to preserve molecular structure information.
        /// </remarks>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Moiety"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Characteristic> getOrCreateCharacteristicAsync(
            ApplicationDbContext dbContext,
            int? moietyId,
            string? characteristicCode,
            string? characteristicCodeSystem,
            string? valueType,
            string? mediaType,
            string? cdataContent)
        {
            #region implementation
            // Search for existing characteristic based on moiety ID, code, and media type
            // Deduplication prevents duplicate chemical structure records
            var existing = await dbContext.Set<Characteristic>().FirstOrDefaultAsync(c =>
                c.MoietyID == moietyId &&
                c.CharacteristicCode == characteristicCode &&
                c.ValueED_MediaType == mediaType);

            if (existing != null)
            {
                return existing;
            }

            // Create new Characteristic entity for chemical structure data
            var newCharacteristic = new Characteristic
            {
                MoietyID = moietyId,
                CharacteristicCode = characteristicCode,
                CharacteristicCodeSystem = characteristicCodeSystem,
                ValueType = valueType,
                ValueED_MediaType = mediaType,
                ValueED_CDATAContent = cdataContent // Preserve chemical data exactly - no sanitization
            };

            // Save the new characteristic to the database
            dbContext.Set<Characteristic>().Add(newCharacteristic);
            await dbContext.SaveChangesAsync();

            return newCharacteristic;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing IdentifiedSubstance or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Section.</param>
        /// <param name="subjectType">The type of subject (e.g., "ActiveMoiety", "PharmacologicClass").</param>
        /// <param name="identifierValue">The identifier value (UNII or class code).</param>
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
        /// Gets an existing PharmacologicClass by its code and system, or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceId">The parent IdentifiedSubstance ID (for definitions).</param>
        /// <param name="classCode">The MED-RT or MeSH code for the class.</param>
        /// <param name="classCodeSystem">The OID for the code system.</param>
        /// <param name="classDisplayName">The display name for the class.</param>
        /// <returns>The existing or newly created PharmacologicClass entity.</returns>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClass> getOrCreatePharmacologicClassAsync(
            ApplicationDbContext dbContext,
            int? identifiedSubstanceId,
            string? classCode,
            string? classCodeSystem,
            string? classDisplayName)
        {
            #region implementation
            // Deduplicate by the unique class code and system to prevent duplicate class records
            var existing = await dbContext.Set<PharmacologicClass>().FirstOrDefaultAsync(pc =>
                pc.ClassCode == classCode &&
                pc.ClassCodeSystem == classCodeSystem);

            if (existing != null)
            {
                // Return existing pharmacologic class if found
                return existing;
            }

            // Create new PharmacologicClass entity with the provided classification data
            var newClass = new PharmacologicClass
            {
                IdentifiedSubstanceID = identifiedSubstanceId, // Only linked for definitions
                ClassCode = classCode,
                ClassCodeSystem = classCodeSystem,
                ClassDisplayName = classDisplayName
            };

            // Save the new pharmacologic class to the database and persist changes immediately
            dbContext.Set<PharmacologicClass>().Add(newClass);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class
            return newClass;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassName or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="pharmacologicClassId">The parent PharmacologicClass ID.</param>
        /// <param name="nameValue">The text of the name.</param>
        /// <param name="nameUse">The use code ('L' for preferred, 'A' for alternate).</param>
        /// <returns>The existing or newly created PharmacologicClassName entity.</returns>
        /// <seealso cref="PharmacologicClassName"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassName> getOrCreatePharmacologicClassNameAsync(
            ApplicationDbContext dbContext,
            int? pharmacologicClassId,
            string? nameValue,
            string? nameUse)
        {
            #region implementation
            // Search for existing name with matching class ID, name value, and use code
            // Deduplication based on PharmacologicClassID, NameValue, and NameUse
            var existing = await dbContext.Set<PharmacologicClassName>().FirstOrDefaultAsync(pcn =>
                pcn.PharmacologicClassID == pharmacologicClassId &&
                pcn.NameValue == nameValue &&
                pcn.NameUse == nameUse);

            if (existing != null)
            {
                // Return existing pharmacologic class name if found
                return existing;
            }

            // Create new PharmacologicClassName entity with the provided name data
            var newName = new PharmacologicClassName
            {
                PharmacologicClassID = pharmacologicClassId,
                NameValue = nameValue,
                NameUse = nameUse // 'L' for preferred, 'A' for alternate
            };

            // Save the new pharmacologic class name to the database and persist changes immediately
            dbContext.Set<PharmacologicClassName>().Add(newName);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class name
            return newName;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassLink or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="activeMoietySubstanceId">The ID of the active moiety IdentifiedSubstance.</param>
        /// <param name="pharmacologicClassId">The ID of the associated PharmacologicClass.</param>
        /// <param name="substanceIdentifierValue">The UNII for the ActiveMoiety to link</param>
        /// <returns>The existing or newly created PharmacologicClassLink entity.</returns>
        /// <seealso cref="PharmacologicClassLink"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassLink?> getOrCreatePharmacologicClassLinkAsync(
            ApplicationDbContext dbContext,
            int? activeMoietySubstanceId,
            int? pharmacologicClassId,
            string? substanceIdentifierValue)
        {
            #region implementation
            // Search for existing link between active moiety and pharmacologic class
            // Deduplication based on ActiveMoietySubstanceID and PharmacologicClassID
            var existing = await dbContext.Set<PharmacologicClassLink>().FirstOrDefaultAsync(pcl =>
                pcl.ActiveMoietySubstanceID == activeMoietySubstanceId &&
                pcl.PharmacologicClassID == pharmacologicClassId);

            if (existing != null)
            {
                // Return existing pharmacologic class link if found
                return existing;
            }

            //Find Existing Active Moiety to Link the to the class. NOTE: this depends upon
            //Order of operation in terms of Label vs Index file. If a matching label exists
            //then the moiety will be found. If the index file is imported with no matching
            //label then there will be no link.

            var moiety = await dbContext.Set<ActiveMoiety>().FirstOrDefaultAsync(pcl =>
                pcl.MoietyUNII == substanceIdentifierValue);

            if (moiety != null && moiety.IngredientSubstanceID >= 0)
            {
                // Create new PharmacologicClassLink entity connecting active moiety to pharmacologic class
                var newLink = new PharmacologicClassLink
                {
                    ActiveMoietySubstanceID = moiety.IngredientSubstanceID,
                    PharmacologicClassID = pharmacologicClassId
                };

                // Save the new pharmacologic class link to the database and persist changes immediately
                dbContext.Set<PharmacologicClassLink>().Add(newLink);
                await dbContext.SaveChangesAsync();

                // Return the newly created and persisted pharmacologic class link
                return newLink;
            }

            else return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassHierarchy or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="childClassId">The ID of the child (more specific) class.</param>
        /// <param name="parentClassId">The ID of the parent (super-class).</param>
        /// <returns>The existing or newly created PharmacologicClassHierarchy entity.</returns>
        /// <seealso cref="PharmacologicClassHierarchy"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassHierarchy> getOrCreatePharmacologicClassHierarchyAsync(
            ApplicationDbContext dbContext,
            int? childClassId,
            int? parentClassId)
        {
            #region implementation
            // Search for existing hierarchy relationship between child and parent classes
            // Deduplication based on ChildPharmacologicClassID and ParentPharmacologicClassID
            var existing = await dbContext.Set<PharmacologicClassHierarchy>().FirstOrDefaultAsync(pch =>
                pch.ChildPharmacologicClassID == childClassId &&
                pch.ParentPharmacologicClassID == parentClassId);

            if (existing != null)
            {
                // Return existing pharmacologic class hierarchy if found
                return existing;
            }

            // Create new PharmacologicClassHierarchy entity establishing parent-child relationship
            var newHierarchy = new PharmacologicClassHierarchy
            {
                ChildPharmacologicClassID = childClassId,
                ParentPharmacologicClassID = parentClassId
            };

            // Save the new pharmacologic class hierarchy to the database and persist changes immediately
            dbContext.Set<PharmacologicClassHierarchy>().Add(newHierarchy);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class hierarchy
            return newHierarchy;
            #endregion
        }

        #endregion

        #region Billing Unit Indexing

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a Billing Unit Indexing section to create BillingUnitIndex records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement (must be the '48779-3' indexing section).</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of BillingUnitIndex records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 12. It finds each NDC package code
        /// and its corresponding NCPDP billing unit characteristic within the section's subject.
        /// </remarks>
        /// <seealso cref="BillingUnitIndex"/>
        /// <seealso cref="getOrCreateBillingUnitIndexAsync"/>
        private async Task<int> parseAndSaveBillingUnitIndexAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.GetDbContext();

            // The structure is a series of <manufacturedProduct> elements inside the <subject>.
            foreach (var manufacturedProductEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.ManufacturedProduct))
            {
                // 1. Extract the NDC Package Code
                var ndcCodeEl = manufacturedProductEl.SplElement(sc.E.AsContent, sc.E.ContainerPackagedProduct, sc.E.Code);
                var packageNdc = ndcCodeEl?.GetAttrVal(sc.A.CodeValue);
                var packageNdcSystem = ndcCodeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(packageNdc))
                {
                    context.Logger.LogWarning("Found a billing unit index entry without an NDC Package Code in SectionID {SectionID}.", section.SectionID);
                    continue;
                }

                // 2. Extract the Billing Unit from the characteristic
                string? billingUnitCode = null;
                string? billingUnitSystem = null;

                var characteristicEl = manufacturedProductEl.SplElement(sc.E.SubjectOf, sc.E.Characteristic);
                if (characteristicEl != null)
                {
                    // Check if the characteristic code is for NCPDP Billing Unit
                    var charCode = characteristicEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                    if (charCode == "NCPDPBILLINGUNIT")
                    {
                        var valueEl = characteristicEl.GetSplElement(sc.E.Value);
                        if (valueEl?.GetXsiType() == "CV" || valueEl?.GetXsiType() == "CE")
                        {
                            billingUnitCode = valueEl.GetAttrVal(sc.A.CodeValue);
                            billingUnitSystem = valueEl.GetAttrVal(sc.A.CodeSystem);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(billingUnitCode))
                {
                    context.Logger.LogWarning("Could not find a valid NCPDP Billing Unit for NDC {NDC} in SectionID {SectionID}.", packageNdc, section.SectionID);
                    continue;
                }

                // 3. Get or create the BillingUnitIndex record
                await getOrCreateBillingUnitIndexAsync(
                    dbContext,
                    section.SectionID,
                    packageNdc,
                    packageNdcSystem,
                    billingUnitCode,
                    billingUnitSystem
                );
                count++;
                context.Logger.LogInformation("Created BillingUnitIndex for NDC {NDC} -> {BU}", packageNdc, billingUnitCode);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing BillingUnitIndex or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="packageNdc">The NDC Package Code being linked.</param>
        /// <param name="packageNdcSystem">The OID for the NDC system.</param>
        /// <param name="billingUnitCode">The NCPDP Billing Unit Code (GM, ML, or EA).</param>
        /// <param name="billingUnitSystem">The OID for the NCPDP Billing Unit system.</param>
        /// <returns>The existing or newly created BillingUnitIndex entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate billing unit index records.
        /// Uniqueness is determined by the combination of the SectionID and the Package NDC value.
        /// </remarks>
        /// <seealso cref="BillingUnitIndex"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<BillingUnitIndex> getOrCreateBillingUnitIndexAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? packageNdc,
            string? packageNdcSystem,
            string? billingUnitCode,
            string? billingUnitSystem)
        {
            #region implementation
            // Deduplicate based on the unique combination of SectionID and the NDC Package Code.
            var existing = await dbContext.Set<BillingUnitIndex>().FirstOrDefaultAsync(bui =>
                bui.SectionID == sectionId &&
                bui.PackageNDCValue == packageNdc);

            // If a record already exists, return it.
            if (existing != null)
            {
                return existing;
            }

            // Create a new BillingUnitIndex entity.
            var newIndex = new BillingUnitIndex
            {
                SectionID = sectionId,
                PackageNDCValue = packageNdc,
                PackageNDCSystemOID = packageNdcSystem,
                BillingUnitCode = billingUnitCode,
                BillingUnitCodeSystemOID = billingUnitSystem
            };

            // Save the new entity to the database.
            dbContext.Set<BillingUnitIndex>().Add(newIndex);
            await dbContext.SaveChangesAsync();

            return newIndex;
            #endregion
        }

        #endregion

        #region Product Concept Indexing

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a Product Concept Indexing section to create ProductConcept records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement (must be the '48779-3' indexing section).</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all product concept related records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 15. It handles both Abstract (15.2.2)
        /// and Application (15.2.6) product concepts, including their equivalence links.
        /// </remarks>
        /// <seealso cref="ProductConcept"/>
        /// <seealso cref="ProductConceptEquivalence"/>
        private async Task<int> parseAndSaveProductConceptsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.GetDbContext();

            // The structure is a series of <manufacturedProduct> elements inside the <subject>.
            foreach (var manufacturedProductEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.ManufacturedProduct))
            {
                // 1. Extract the main concept code
                var conceptCodeEl = manufacturedProductEl.GetSplElement(sc.E.Code);
                var conceptCode = conceptCodeEl?.GetAttrVal(sc.A.CodeValue);
                var conceptCodeSystem = conceptCodeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(conceptCode)) continue;

                // 2. Determine if it's an Abstract or Application concept
                var equivalentEntityEl = manufacturedProductEl.GetSplElement(sc.E.AsEquivalentEntity);
                bool isApplicationConcept = equivalentEntityEl != null;
                string conceptType = isApplicationConcept ? "Application" : "Abstract";

                // 3. Get or create the ProductConcept
                var formCodeEl = manufacturedProductEl.GetSplElement(sc.E.FormCode); // Only used for Abstract
                var productConcept = await getOrCreateProductConceptAsync(
                    dbContext,
                    section.SectionID,
                    conceptCode,
                    conceptCodeSystem,
                    conceptType,
                    formCodeEl
                );
                count++;
                context.Logger.LogInformation("Created ProductConcept: Type={type}, Code={code}", conceptType, conceptCode);

                // 4. If it's an Application concept, create the equivalence link
                if (isApplicationConcept && productConcept.ProductConceptID.HasValue && equivalentEntityEl != null)
                {
                    var equivalenceCodeEl = equivalentEntityEl.GetSplElement(sc.E.Code);
                    var equivalenceCode = equivalenceCodeEl?.GetAttrVal(sc.A.CodeValue);
                    var equivalenceCodeSystem = equivalenceCodeEl?.GetAttrVal(sc.A.CodeSystem);

                    var definingKindEl = equivalentEntityEl.GetSplElement(sc.E.DefiningMaterialKind);
                    var abstractConceptCode = definingKindEl?.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);

                    if (!string.IsNullOrWhiteSpace(abstractConceptCode))
                    {
                        // Find the abstract concept it links to (it should already be in the DB from a previous loop or file)
                        var abstractConcept = await dbContext.Set<ProductConcept>()
                            .FirstOrDefaultAsync(pc => pc.ConceptCode == abstractConceptCode);

                        if (abstractConcept != null)
                        {
                            await getOrCreateProductConceptEquivalenceAsync(
                                dbContext,
                                productConcept.ProductConceptID,
                                abstractConcept.ProductConceptID,
                                equivalenceCode,
                                equivalenceCodeSystem
                            );
                            count++;
                            context.Logger.LogInformation("Created ProductConceptEquivalence link: App({app}) -> Abstract({abs})", productConcept.ProductConceptID, abstractConcept.ProductConceptID);
                        }
                        else
                        {
                            context.Logger.LogWarning("Application concept {appCode} referenced an abstract concept {absCode} that was not found in the database.", conceptCode, abstractConceptCode);
                        }
                    }
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductConcept by its unique concept code, or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="conceptCode">The MD5 hash code for the product concept.</param>
        /// <param name="conceptCodeSystem">The OID for the product concept code system.</param>
        /// <param name="conceptType">The type of concept ("Abstract" or "Application").</param>
        /// <param name="formCodeEl">The XElement for the [formCode], used only for Abstract concepts.</param>
        /// <returns>The existing or newly created ProductConcept entity.</returns>
        private async Task<ProductConcept> getOrCreateProductConceptAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? conceptCode,
            string? conceptCodeSystem,
            string? conceptType,
            XElement? formCodeEl)
        {
            #region implementation
            // Deduplicate based on the globally unique concept code.
            var existing = await dbContext.Set<ProductConcept>()
                .FirstOrDefaultAsync(pc => pc.ConceptCode == conceptCode);

            if (existing != null)
            {
                return existing;
            }

            var newConcept = new ProductConcept
            {
                SectionID = sectionId,
                ConceptCode = conceptCode,
                ConceptCodeSystem = conceptCodeSystem,
                ConceptType = conceptType,
                // Form code details are only applicable for Abstract concepts
                FormCode = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.CodeValue) : null,
                FormCodeSystem = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.CodeSystem) : null,
                FormDisplayName = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.DisplayName) : null
            };

            dbContext.Set<ProductConcept>().Add(newConcept);
            await dbContext.SaveChangesAsync();
            return newConcept;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductConceptEquivalence link or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="applicationConceptId">The ID of the Application ProductConcept.</param>
        /// <param name="abstractConceptId">The ID of the Abstract ProductConcept.</param>
        /// <param name="equivalenceCode">The code for the equivalence relationship (A, B, OTC, N).</param>
        /// <param name="equivalenceCodeSystem">The OID for the equivalence code system.</param>
        /// <returns>The existing or newly created ProductConceptEquivalence entity.</returns>
        private async Task<ProductConceptEquivalence> getOrCreateProductConceptEquivalenceAsync(
            ApplicationDbContext dbContext,
            int? applicationConceptId,
            int? abstractConceptId,
            string? equivalenceCode,
            string? equivalenceCodeSystem)
        {
            #region implementation
            var existing = await dbContext.Set<ProductConceptEquivalence>().FirstOrDefaultAsync(pce =>
                pce.ApplicationProductConceptID == applicationConceptId &&
                pce.AbstractProductConceptID == abstractConceptId);

            if (existing != null)
            {
                return existing;
            }

            var newEquivalence = new ProductConceptEquivalence
            {
                ApplicationProductConceptID = applicationConceptId,
                AbstractProductConceptID = abstractConceptId,
                EquivalenceCode = equivalenceCode,
                EquivalenceCodeSystem = equivalenceCodeSystem
            };

            dbContext.Set<ProductConceptEquivalence>().Add(newEquivalence);
            await dbContext.SaveChangesAsync();
            return newEquivalence;
            #endregion
        }

        #endregion

        #region Drug Interactions Indexing

        /**************************************************************/
        /// <summary>
        /// Parses drug interaction issues from a section's subject.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all interaction-related records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 32. It finds each [issue]
        /// and parses its contributing factors and consequences.
        /// </remarks>
        /// <seealso cref="InteractionIssue"/>
        /// <seealso cref="ContributingFactor"/>
        /// <seealso cref="InteractionConsequence"/>
        private async Task<int> parseAndSaveDrugInteractionsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.GetDbContext();

            // The structure is <substanceAdministration><subjectOf><issue>
            foreach (var issueEl in sectionEl.SplElements(sc.E.SubstanceAdministration, sc.E.SubjectOf, sc.E.Issue))
            {
                // 1. Create the main InteractionIssue record
                var interactionIssue = await getOrCreateInteractionIssueAsync(dbContext, section.SectionID, issueEl);
                count++;

                if (!interactionIssue.InteractionIssueID.HasValue) continue;

                // 2. Parse Contributing Factors
                var factorEl = issueEl.SplElement(sc.E.Subject, sc.E.SubstanceAdministrationCriterion, sc.E.Consumable, sc.E.AdministrableMaterial, sc.E.AdministrableMaterialKind, sc.E.Code);
                if (factorEl != null)
                {
                    var factorIdentifier = factorEl.GetAttrVal(sc.A.CodeValue);
                    var factorSystem = factorEl.GetAttrVal(sc.A.CodeSystem);

                    // The contributing factor is itself an IdentifiedSubstance. We need to find it.
                    // This assumes the Pharmacologic Class Indexing documents have already been processed.
                    var factorSubstance = await dbContext.Set<IdentifiedSubstance>().FirstOrDefaultAsync(i =>
                        i.SubstanceIdentifierValue == factorIdentifier &&
                        i.SubstanceIdentifierSystemOID == factorSystem);

                    if (factorSubstance != null)
                    {
                        await getOrCreateContributingFactorAsync(dbContext, interactionIssue.InteractionIssueID, factorSubstance.IdentifiedSubstanceID);
                        count++;
                    }
                    else
                    {
                        context.Logger.LogWarning("Could not find IdentifiedSubstance for contributing factor {factorId} in Section {sectionId}", factorIdentifier, section.SectionID);
                    }
                }

                // 3. Parse Consequences
                foreach (var consequenceEl in issueEl.SplElements(sc.E.Risk, sc.E.ConsequenceObservation))
                {
                    await getOrCreateInteractionConsequenceAsync(dbContext, interactionIssue.InteractionIssueID, consequenceEl);
                    count++;
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing InteractionIssue or creates a new one for a given section.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The ID of the parent Section.</param>
        /// <param name="issueEl">The XElement for the [issue].</param>
        /// <returns>The existing or newly created InteractionIssue entity.</returns>
        private async Task<InteractionIssue> getOrCreateInteractionIssueAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            XElement issueEl)
        {
            #region implementation
            var codeEl = issueEl.GetSplElement(sc.E.Code);
            var interactionCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            var interactionCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            var interactionDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

            // An issue is unique per section. We can deduplicate based on this.
            var existing = await dbContext.Set<InteractionIssue>().FirstOrDefaultAsync(i =>
                i.SectionID == sectionId &&
                i.InteractionCode == interactionCode);

            if (existing != null)
            {
                return existing;
            }

            var newIssue = new InteractionIssue
            {
                SectionID = sectionId,
                InteractionCode = interactionCode,
                InteractionCodeSystem = interactionCodeSystem,
                InteractionDisplayName = interactionDisplayName
            };

            dbContext.Set<InteractionIssue>().Add(newIssue);
            await dbContext.SaveChangesAsync();
            return newIssue;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ContributingFactor link or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="interactionIssueId">The ID of the parent InteractionIssue.</param>
        /// <param name="factorSubstanceId">The ID of the IdentifiedSubstance that is the factor.</param>
        /// <returns>The existing or newly created ContributingFactor entity.</returns>
        private async Task<ContributingFactor> getOrCreateContributingFactorAsync(
            ApplicationDbContext dbContext,
            int? interactionIssueId,
            int? factorSubstanceId)
        {
            #region implementation
            var existing = await dbContext.Set<ContributingFactor>().FirstOrDefaultAsync(cf =>
                cf.InteractionIssueID == interactionIssueId &&
                cf.FactorSubstanceID == factorSubstanceId);

            if (existing != null)
            {
                return existing;
            }

            var newFactor = new ContributingFactor
            {
                InteractionIssueID = interactionIssueId,
                FactorSubstanceID = factorSubstanceId
            };

            dbContext.Set<ContributingFactor>().Add(newFactor);
            await dbContext.SaveChangesAsync();
            return newFactor;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing InteractionConsequence or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="interactionIssueId">The ID of the parent InteractionIssue.</param>
        /// <param name="consequenceEl">The XElement for the [consequenceObservation].</param>
        /// <returns>The existing or newly created InteractionConsequence entity.</returns>
        private async Task<InteractionConsequence> getOrCreateInteractionConsequenceAsync(
            ApplicationDbContext dbContext,
            int? interactionIssueId,
            XElement consequenceEl)
        {
            #region implementation
            var typeCodeEl = consequenceEl.GetSplElement(sc.E.Code);
            var valueEl = consequenceEl.GetSplElement(sc.E.Value);

            var consequenceTypeCode = typeCodeEl?.GetAttrVal(sc.A.CodeValue);
            var consequenceValueCode = valueEl?.GetAttrVal(sc.A.CodeValue);

            // Deduplicate based on the issue and the specific consequence value code.
            var existing = await dbContext.Set<InteractionConsequence>().FirstOrDefaultAsync(ic =>
                ic.InteractionIssueID == interactionIssueId &&
                ic.ConsequenceValueCode == consequenceValueCode);

            if (existing != null)
            {
                return existing;
            }

            var newConsequence = new InteractionConsequence
            {
                InteractionIssueID = interactionIssueId,
                ConsequenceTypeCode = consequenceTypeCode,
                ConsequenceTypeCodeSystem = typeCodeEl?.GetAttrVal(sc.A.CodeSystem),
                ConsequenceTypeDisplayName = typeCodeEl?.GetAttrVal(sc.A.DisplayName),
                ConsequenceValueCode = consequenceValueCode,
                ConsequenceValueCodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem),
                ConsequenceValueDisplayName = valueEl?.GetAttrVal(sc.A.DisplayName)
            };

            dbContext.Set<InteractionConsequence>().Add(newConsequence);
            await dbContext.SaveChangesAsync();
            return newConsequence;
            #endregion
        }

        #endregion

        #region NCT Links Indexing

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a National Clinical Trials Indexing section to create NCTLink records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of NCTLink records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 33.2.2. It finds each
        /// NCT number within the section's subject and creates a link record.
        /// </remarks>
        /// <seealso cref="NCTLink"/>
        /// <seealso cref="getOrCreateNCTLinkAsync"/>
        private async Task<int> parseAndSaveNCTLinksAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.GetDbContext();

            // The structure is <subject2><substanceAdministration><componentOf><protocol><id>
            // The XElementExtensions helper `SplElements` is perfect for this deep navigation.
            foreach (var idEl in sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration, sc.E.ComponentOf, sc.E.Protocol, sc.E.Id))
            {
                // 1. Extract the NCT number and its root OID
                var nctNumber = idEl.GetAttrVal(sc.A.Extension);
                var nctRootOid = idEl.GetAttrVal(sc.A.Root);

                // 2. Validate the data according to the specification
                if (string.IsNullOrWhiteSpace(nctNumber) || nctRootOid != "2.16.840.1.113883.3.1077")
                {
                    context.Logger.LogWarning("Found an invalid or non-NCT protocol ID in Section {SectionID}. Skipping.", section.SectionID);
                    continue;
                }

                // Validate NCT number format: "NCT" + 8 digits
                if (!System.Text.RegularExpressions.Regex.IsMatch(nctNumber, @"^NCT\d{8}$"))
                {
                    context.Logger.LogWarning("NCT number '{NCT}' has an invalid format in Section {SectionID}. Skipping.", nctNumber, section.SectionID);
                    continue;
                }

                // 3. Get or create the NCTLink record
                await getOrCreateNCTLinkAsync(
                    dbContext,
                    section.SectionID,
                    nctNumber,
                    nctRootOid
                );
                count++;
                context.Logger.LogInformation("Created NCTLink for Section {SectionID} to NCT# {NCT}", section.SectionID, nctNumber);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing NCTLink or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="nctNumber">The National Clinical Trials number.</param>
        /// <param name="nctRootOid">The root OID for the NCT number system.</param>
        /// <returns>The existing or newly created NCTLink entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate NCT links within a section.
        /// Uniqueness is determined by the combination of the SectionID and the NCTNumber.
        /// </remarks>
        /// <seealso cref="NCTLink"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<NCTLink> getOrCreateNCTLinkAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? nctNumber,
            string? nctRootOid)
        {
            #region implementation
            // Deduplicate based on the unique combination of SectionID and NCTNumber.
            var existing = await dbContext.Set<NCTLink>().FirstOrDefaultAsync(nctl =>
                nctl.SectionID == sectionId &&
                nctl.NCTNumber == nctNumber);

            // If a record already exists, return it.
            if (existing != null)
            {
                return existing;
            }

            // Create a new NCTLink entity.
            var newLink = new NCTLink
            {
                SectionID = sectionId,
                NCTNumber = nctNumber,
                NCTRootOID = nctRootOid
            };

            // Save the new entity to the database.
            dbContext.Set<NCTLink>().Add(newLink);
            await dbContext.SaveChangesAsync();

            return newLink;
            #endregion
        }

        #endregion

        #region Compliance Action Indexing
        /**************************************************************/
        /// <summary>
        /// Parses compliance actions associated with package identifiers in indexing sections.
        /// Handles inactivation/reactivation actions for drug listings.
        /// </summary>
        /// <param name="sectionEl">The section element containing indexing information.</param>
        /// <param name="section">The current section entity.</param>
        /// <param name="context">The parsing context for database access and logging.</param>
        /// <returns>The count of compliance actions processed.</returns>
        private async Task<int> parseIndexingComplianceActionsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            int count = 0;

            if (context?.ServiceProvider == null || !section.SectionID.HasValue)
            {
                Console.WriteLine("DEBUG: Context validation failed");
                return count;
            }

            var dbContext = context.GetDbContext();

            // DEBUG: Log what we're starting with
            Console.WriteLine($"DEBUG: Starting to parse section {section.SectionCode}");
            Console.WriteLine($"DEBUG: Section XML name: {sectionEl.Name.LocalName}");

            // Look for subject elements first
            var subjectElements = sectionEl.SplElements(sc.E.Subject);
            Console.WriteLine($"DEBUG: Found {subjectElements.Count()} subject elements");

            foreach (var subjectEl in subjectElements)
            {
                // Look for manufactured products within each subject
                var manufacturedProductElements = subjectEl.SplElements(sc.E.ManufacturedProduct);
                Console.WriteLine($"DEBUG: Found {manufacturedProductElements.Count()} manufacturedProduct elements in subject");

                foreach (var topLevelProductEl in manufacturedProductElements)
                {
                    // Look for nested manufactured product
                    var nestedProductElements = topLevelProductEl.SplElements(sc.E.ManufacturedProduct);
                    Console.WriteLine($"DEBUG: Found {nestedProductElements.Count()} nested manufacturedProduct elements");

                    foreach (var manufacturedProductEl in nestedProductElements)
                    {
                        // Look for asContent elements
                        var asContentElements = manufacturedProductEl.SplElements(sc.E.AsContent);
                        Console.WriteLine($"DEBUG: Found {asContentElements.Count()} asContent elements");

                        foreach (var asContentEl in asContentElements)
                        {
                            // 1. Get the package identifier from containerPackagedProduct
                            var packageCodeEl = asContentEl.SplElement(sc.E.ContainerPackagedProduct, sc.E.Code);
                            if (packageCodeEl == null)
                            {
                                Console.WriteLine("DEBUG: No packageCodeEl found in asContent");
                                continue;
                            }

                            var packageCode = packageCodeEl.GetAttrVal(sc.A.CodeValue);
                            var packageCodeSystem = packageCodeEl.GetAttrVal(sc.A.CodeSystem);

                            Console.WriteLine($"DEBUG: Found package code: {packageCode}, system: {packageCodeSystem}");

                            if (string.IsNullOrEmpty(packageCode) || string.IsNullOrEmpty(packageCodeSystem))
                            {
                                Console.WriteLine("DEBUG: Package code or system is null/empty");
                                continue;
                            }

                            // 2. Find or create the PackageIdentifier
                            var packageIdentifier = await getOrCreatePackageIdentifierAsync(
                                dbContext, packageCode, packageCodeSystem, context);

                            if (packageIdentifier?.PackageIdentifierID == null)
                            {
                                Console.WriteLine($"DEBUG: Failed to create PackageIdentifier for {packageCode}");
                                continue;
                            }

                            Console.WriteLine($"DEBUG: Created PackageIdentifier {packageIdentifier.PackageIdentifierID} for {packageCode}");

                            // 3. Look for subjectOf/action elements
                            var subjectOfElements = asContentEl.SplElements(sc.E.SubjectOf);
                            Console.WriteLine($"DEBUG: Found {subjectOfElements.Count()} subjectOf elements in asContent");

                            foreach (var subjectOfEl in subjectOfElements)
                            {
                                if (subjectOfEl.SplElement(sc.E.Action) != null)
                                {
                                    Console.WriteLine($"DEBUG: Found action element! Processing for package {packageCode}");

                                    // Set context for the ComplianceActionParser
                                    var oldPackageIdentifier = context.CurrentPackageIdentifier;
                                    context.CurrentPackageIdentifier = packageIdentifier;

                                    try
                                    {
                                        // Delegate to existing ComplianceActionParser
                                        var complianceParser = new ComplianceActionParser();
                                        var result = await complianceParser.ParseAsync(subjectOfEl, context, null);

                                        Console.WriteLine($"DEBUG: ComplianceActionParser result - Success: {result.Success}, Count: {result.ProductElementsCreated}");

                                        if (result.Success)
                                        {
                                            count += result.ProductElementsCreated;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"DEBUG: ComplianceActionParser errors: {string.Join(", ", result.Errors)}");
                                        }
                                    }
                                    finally
                                    {
                                        // Restore context
                                        context.CurrentPackageIdentifier = oldPackageIdentifier;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("DEBUG: No action element found in subjectOf");
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"DEBUG: parseIndexingComplianceActionsAsync completed, total count: {count}");
            return count;
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing PackageIdentifier or creates a new one for compliance action context.
        /// </summary>
        private async Task<PackageIdentifier?> getOrCreatePackageIdentifierAsync(
            ApplicationDbContext dbContext,
            string packageCode,
            string packageCodeSystem,
            SplParseContext context)
        {
            try
            {
                // Try to find existing package identifier
                var existing = await dbContext.Set<PackageIdentifier>()
                    .FirstOrDefaultAsync(pi =>
                        pi.IdentifierValue == packageCode &&
                        pi.IdentifierSystemOID == packageCodeSystem);

                if (existing != null)
                {
                    return existing;
                }

                // Create new package identifier if not found
                var newPackageIdentifier = new PackageIdentifier
                {
                    IdentifierValue = packageCode,
                    IdentifierSystemOID = packageCodeSystem,
                    IdentifierType = determinePackageIdentifierType(packageCodeSystem)
                };

                dbContext.Set<PackageIdentifier>().Add(newPackageIdentifier);
                await dbContext.SaveChangesAsync();

                return newPackageIdentifier;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error finding/creating PackageIdentifier for {PackageCode}", packageCode);
                return null;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Determines package identifier type based on OID system.
        /// </summary>
        private static string? determinePackageIdentifierType(string? oidSystem)
        {
            return oidSystem switch
            {
                "2.16.840.1.113883.6.69" => "NDCPackage",
                "1.3.160" => "GS1Package",
                _ => "OTHER"
            };
        }

        #endregion

        #region Product Indexing for General Indexing Documents

        // Replace the parseAndSaveIndexingProductsAsync method in SectionIndexingParser.cs

        /**************************************************************/
        /// <summary>
        /// Parses manufactured product information from general indexing documents (73815-3).
        /// IMPROVED: Now leverages existing parsers for comprehensive product processing.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement containing product data.</param>
        /// <param name="section">The Section entity that has just been created and saved to database.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all product-related records created during processing.</returns>
        /// <remarks>
        /// This improved version reuses existing specialized parsers rather than duplicating logic.
        /// It delegates to ManufacturedProductParser for comprehensive product processing while
        /// handling indexing-specific requirements.
        /// </remarks>
        private async Task<int> parseAndSaveIndexingProductsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Validate required dependencies before processing
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            // Store the original section context to restore later
            var originalSection = context.CurrentSection;
            context.CurrentSection = section;

            try
            {
                // Create the manufactured product parser for delegation
                var manufacturedProductParser = new ManufacturedProductParser();

                // Find all subject elements containing manufactured products
                var subjectElements = sectionEl.SplElements(sc.E.Subject);
                context.Logger.LogInformation("Found {Count} subject elements in indexing section", subjectElements.Count());

                foreach (var subjectEl in subjectElements)
                {
                    // Look for manufactured product element within current subject
                    var manufacturedProductEl = subjectEl.SplElement(sc.E.ManufacturedProduct);
                    if (manufacturedProductEl == null) continue;

                    try
                    {
                        context.Logger.LogInformation("Processing indexing manufactured product");

                        // This handles all aspects: products, ingredients, characteristics, marketing, etc.
                        var productResult = await manufacturedProductParser.ParseAsync(manufacturedProductEl, context, null);

                        // Merge results from the manufactured product parser
                        count += productResult.ProductsCreated;
                        count += productResult.IngredientsCreated;
                        count += productResult.ProductElementsCreated;

                        // Handle indexing-specific processing for equivalent entities and approvals
                        // These are at the subject level in indexing documents, not product level
                        await processIndexingSpecificElements(subjectEl, context, productResult.ProductsCreated);

                        if (!productResult.Success)
                        {
                            context.Logger.LogWarning("ManufacturedProductParser reported errors: {Errors}",
                                string.Join(", ", productResult.Errors));
                        }

                        context.Logger.LogInformation("Completed processing indexing product with {Records} total records",
                            productResult.ProductsCreated + productResult.IngredientsCreated + productResult.ProductElementsCreated);

                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other products
                        context.Logger.LogError(ex, "Error parsing indexing manufactured product in section {SectionId}", section.SectionID);
                    }
                }

                context.Logger.LogInformation("Completed processing {Count} indexing products with {TotalRecords} total records",
                    subjectElements.Count(), count);
            }
            finally
            {
                // Restore the original section context
                context.CurrentSection = originalSection;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes indexing-specific elements that appear at the subject level rather than product level.
        /// Handles equivalent entities and approvals that are structured differently in indexing documents.
        /// </summary>
        /// <param name="subjectEl">The subject element containing indexing-specific structures.</param>
        /// <param name="context">The parsing context for database access and logging.</param>
        /// <param name="productsCreated">Number of products created (for logging purposes).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// In indexing documents, some elements like equivalent entities and approvals appear
        /// at the subject level rather than nested within the manufactured product. This method
        /// handles these indexing-specific structural differences.
        /// </remarks>
        private async Task processIndexingSpecificElements(
            XElement subjectEl,
            SplParseContext context,
            int productsCreated)
        {
            #region implementation
            if (context?.ServiceProvider == null || context.Logger == null)
                return;

            try
            {
                // Handle equivalent entities at subject level (indexing-specific structure)
                var equivalentEntityElements = subjectEl.SplElements(sc.E.ManufacturedProduct, sc.E.AsEquivalentEntity);
                foreach (var equivalentEntityEl in equivalentEntityElements)
                {
                    await processSubjectLevelEquivalentEntity(equivalentEntityEl, context);
                }

                // Handle approvals at subject level (indexing-specific structure)
                var approvalElements = subjectEl.SplElements(sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Approval);
                foreach (var approvalEl in approvalElements)
                {
                    await processSubjectLevelApproval(approvalEl, context);
                }

                context.Logger.LogDebug("Processed indexing-specific elements for subject with {ProductCount} products", productsCreated);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Error processing indexing-specific elements");
                // Don't rethrow - this shouldn't stop the main parsing process
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes equivalent entity elements that appear at the subject level in indexing documents.
        /// Uses getOrCreate pattern to avoid duplicate records.
        /// </summary>
        /// <param name="equivalentEntityEl">The equivalent entity XML element.</param>
        /// <param name="context">The parsing context for database access and logging.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="EquivalentEntity"/>
        /// <seealso cref="getOrCreateEquivalentEntity(ApplicationDbContext, int, string, string, string)"/>
        /// <example>
        /// <code>
        /// await processSubjectLevelEquivalentEntity(xmlElement, parseContext);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method extracts equivalence and defining material information from XML elements
        /// and creates or retrieves existing EquivalentEntity records to prevent duplicates.
        /// </remarks>
        private async Task processSubjectLevelEquivalentEntity(
            XElement equivalentEntityEl,
            SplParseContext context)
        {
            #region implementation
            // Validate product context before processing
            if (!validateProductContext(context, DbParsingConstants.EquivalentEntityType))
                return;

            var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            try
            {
                // Extract XML elements using helper method
                var extractedData = extractEquivalentEntityData(equivalentEntityEl);

                // Use getOrCreate pattern to avoid duplicates
                var equivalentEntity = await getOrCreateEquivalentEntity(
                    dbContext,
                    context.CurrentProduct?.ProductID ?? 0,
                    extractedData.EquivalenceCode,
                    extractedData.EquivalenceCodeSystem,
                    extractedData.DefiningMaterialKindCode);

                // Log successful operation
                context?.Logger?.LogInformation(
                    DbParsingConstants.EquivalentEntityLogMessage,
                    context.CurrentProduct.ProductID,
                    equivalentEntity.EquivalenceCode);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    DbParsingConstants.ProcessingErrorMessage,
                    DbParsingConstants.EquivalentEntityType,
                    context.CurrentProduct?.ProductID);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes approval elements that appear at the subject level in indexing documents.
        /// Uses getOrCreate pattern to avoid duplicate records.
        /// </summary>
        /// <param name="approvalEl">The approval XML element.</param>
        /// <param name="context">The parsing context for database access and logging.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="getOrCreateMarketingCategory(ApplicationDbContext, int, string, string, string, string, string, string)"/>
        /// <example>
        /// <code>
        /// await processSubjectLevelApproval(xmlElement, parseContext);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method extracts approval identification and categorization elements from XML
        /// and creates or retrieves existing MarketingCategory records to prevent duplicates.
        /// </remarks>
        private async Task processSubjectLevelApproval(
            XElement approvalEl,
            SplParseContext context)
        {
            #region implementation
            // Validate product context before processing
            if (!validateProductContext(context, DbParsingConstants.ApprovalEntityType))
                return;



            var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            try
            {
                // Extract XML elements using helper method
                var extractedData = extractApprovalData(approvalEl);

                // Use getOrCreate pattern to avoid duplicates
                var marketingCategory = await getOrCreateMarketingCategory(
                    dbContext,
                    context.CurrentProduct?.ProductID ?? 0,
                    extractedData.ApplicationOrMonographIDValue,
                    extractedData.ApplicationOrMonographIDOID,
                    extractedData.CategoryCode,
                    extractedData.CategoryCodeSystem,
                    extractedData.CategoryDisplayName,
                    extractedData.TerritoryCode);

                // Log successful operation
                context?.Logger?.LogInformation(
                    DbParsingConstants.MarketingCategoryLogMessage,
                    marketingCategory.ApplicationOrMonographIDValue,
                    context?.CurrentProduct?.ProductID ?? 0);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    DbParsingConstants.ProcessingErrorMessage,
                    DbParsingConstants.ApprovalEntityType,
                    context.CurrentProduct?.ProductID);
            }
            #endregion
        }

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Validates that the parsing context contains a valid product for processing.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="entityType">The type of entity being processed for logging purposes.</param>
        /// <returns>True if context is valid, false otherwise.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="SplParseContext"/>
        private static bool validateProductContext(SplParseContext context, string entityType)
        {
            #region implementation
            if (context?.CurrentProduct?.ProductID == null)
            {
                context?.Logger?.LogWarning(
                    DbParsingConstants.MissingProductContextWarning,
                    entityType);
                return false;
            }
            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts equivalent entity data from XML element.
        /// </summary>
        /// <param name="equivalentEntityEl">The equivalent entity XML element.</param>
        /// <returns>Extracted data structure containing equivalence information.</returns>
        /// <seealso cref="Label"/>
        private static (string? EquivalenceCode, string? EquivalenceCodeSystem, string? DefiningMaterialKindCode)
            extractEquivalentEntityData(XElement equivalentEntityEl)
        {
            #region implementation
            // Extract code element for equivalence information
            var codeEl = equivalentEntityEl.GetSplElement(sc.E.Code);

            // Extract defining material kind element
            var definingKindEl = equivalentEntityEl.GetSplElement(sc.E.DefiningMaterialKind);

            return (
                        EquivalenceCode: codeEl?.GetAttrVal(sc.A.CodeValue),
                        EquivalenceCodeSystem: codeEl?.GetAttrVal(sc.A.CodeSystem),
                        DefiningMaterialKindCode: definingKindEl?.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue)
                    );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts approval data from XML element.
        /// </summary>
        /// <param name="approvalEl">The approval XML element.</param>
        /// <returns>Extracted data structure containing approval information.</returns>
        /// <seealso cref="Label"/>
        private static (string? ApplicationOrMonographIDValue, string? ApplicationOrMonographIDOID,
            string? CategoryCode, string? CategoryCodeSystem, string? CategoryDisplayName, string? TerritoryCode)
            extractApprovalData(XElement approvalEl)
        {
            #region implementation
            // Extract identification element
            var idEl = approvalEl.GetSplElement(sc.E.Id);

            // Extract code element for categorization
            var codeEl = approvalEl.GetSplElement(sc.E.Code);

            // Extract territory element from nested hierarchy
            var territoryEl = approvalEl.SplElement(sc.E.Author, sc.E.TerritorialAuthority, sc.E.Territory, sc.E.Code);

            return (
                ApplicationOrMonographIDValue: idEl?.GetAttrVal(sc.A.Extension),
                ApplicationOrMonographIDOID: idEl?.GetAttrVal(sc.A.Root),
                CategoryCode: codeEl?.GetAttrVal(sc.A.CodeValue),
                CategoryCodeSystem: codeEl?.GetAttrVal(sc.A.CodeSystem),
                CategoryDisplayName: codeEl?.GetAttrVal(sc.A.DisplayName),
                TerritoryCode: territoryEl?.GetAttrVal(sc.A.CodeValue)
            );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing EquivalentEntity or creates a new one if it doesn't exist.
        /// Prevents duplicate records by checking unique constraints.
        /// </summary>
        /// <param name="dbContext">The database context for operations.</param>
        /// <param name="productId">The product identifier.</param>
        /// <param name="equivalenceCode">The equivalence code value.</param>
        /// <param name="equivalenceCodeSystem">The equivalence code system.</param>
        /// <param name="definingMaterialKindCode">The defining material kind code.</param>
        /// <returns>The existing or newly created EquivalentEntity.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="EquivalentEntity"/>
        /// <remarks>
        /// This method implements the getOrCreate pattern to avoid duplicate database records.
        /// It first attempts to find an existing record with matching criteria before creating a new one.
        /// </remarks>
        private static async Task<EquivalentEntity> getOrCreateEquivalentEntity(
            ApplicationDbContext dbContext,
            int productId,
            string equivalenceCode,
            string equivalenceCodeSystem,
            string definingMaterialKindCode)
        {
            #region implementation
            // Check for existing record to avoid duplicates
            var existingEntity = await dbContext.Set<EquivalentEntity>()
                .FirstOrDefaultAsync(ee =>
                    ee.ProductID == productId &&
                    ee.EquivalenceCode == equivalenceCode &&
                    ee.EquivalenceCodeSystem == equivalenceCodeSystem &&
                    ee.DefiningMaterialKindCode == definingMaterialKindCode);

            // Return existing entity if found
            if (existingEntity != null)
                return existingEntity;

            // Create new entity if not found
            var newEntity = new EquivalentEntity
            {
                ProductID = productId,
                EquivalenceCode = equivalenceCode,
                EquivalenceCodeSystem = equivalenceCodeSystem,
                DefiningMaterialKindCode = definingMaterialKindCode
            };

            dbContext.Set<EquivalentEntity>().Add(newEntity);
            await dbContext.SaveChangesAsync();

            return newEntity;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing MarketingCategory or creates a new one if it doesn't exist.
        /// Prevents duplicate records by checking unique constraints.
        /// </summary>
        /// <param name="dbContext">The database context for operations.</param>
        /// <param name="productId">The product identifier.</param>
        /// <param name="applicationOrMonographIDValue">The application or monograph ID value.</param>
        /// <param name="applicationOrMonographIDOID">The application or monograph ID OID.</param>
        /// <param name="categoryCode">The category code value.</param>
        /// <param name="categoryCodeSystem">The category code system.</param>
        /// <param name="categoryDisplayName">The category display name.</param>
        /// <param name="territoryCode">The territory code.</param>
        /// <returns>The existing or newly created MarketingCategory.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="MarketingCategory"/>
        /// <remarks>
        /// This method implements the getOrCreate pattern to avoid duplicate database records.
        /// It first attempts to find an existing record with matching criteria before creating a new one.
        /// </remarks>
        private static async Task<MarketingCategory> getOrCreateMarketingCategory(
            ApplicationDbContext dbContext,
            int productId,
            string applicationOrMonographIDValue,
            string applicationOrMonographIDOID,
            string categoryCode,
            string categoryCodeSystem,
            string categoryDisplayName,
            string territoryCode)
        {
            #region implementation
            // Check for existing record to avoid duplicates
            var existingCategory = await dbContext.Set<MarketingCategory>()
                .FirstOrDefaultAsync(mc =>
                    mc.ProductID == productId &&
                    mc.ApplicationOrMonographIDValue == applicationOrMonographIDValue &&
                    mc.ApplicationOrMonographIDOID == applicationOrMonographIDOID &&
                    mc.CategoryCode == categoryCode &&
                    mc.CategoryCodeSystem == categoryCodeSystem);

            // Return existing category if found
            if (existingCategory != null)
                return existingCategory;

            // Create new category if not found
            var newCategory = new MarketingCategory
            {
                ProductID = productId,
                ApplicationOrMonographIDValue = applicationOrMonographIDValue,
                ApplicationOrMonographIDOID = applicationOrMonographIDOID,
                CategoryCode = categoryCode,
                CategoryCodeSystem = categoryCodeSystem,
                CategoryDisplayName = categoryDisplayName,
                TerritoryCode = territoryCode
            };

            dbContext.Set<MarketingCategory>().Add(newCategory);
            await dbContext.SaveChangesAsync();

            return newCategory;
            #endregion
        }

        #endregion
    }

    #endregion
}
