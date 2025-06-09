using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using MedRecPro.DataModels;
using MedRecPro.DataAccess;
using MedRecPro.Models; // For ImportResult
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Services
{
    /// <summary>
    /// Parses SPL (Structured Product Labeling) XML files and saves the extracted data to the database.
    /// Handles HL7 v3 XML format documents containing pharmaceutical product information.
    /// </summary>
    /// <remarks>
    /// This parser is designed to handle various SPL XML schema variations and extract key entities
    /// including documents, organizations, sections, products, and ingredients.
    /// </remarks>
    public class SplXmlParser
    {
        #region implementation
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SplXmlParser> _logger;
        private static readonly XNamespace ns = "urn:hl7-org:v3"; // Default HL7 v3 namespace
        #endregion

        /// <summary>
        /// Initializes a new instance of the SplXmlParser with dependency injection services.
        /// </summary>
        /// <param name="serviceProvider">Service provider for creating scoped repositories</param>
        /// <param name="logger">Logger instance for tracking parsing operations</param>
        public SplXmlParser(IServiceProvider serviceProvider, ILogger<SplXmlParser> logger)
        {
            #region implementation
            _serviceProvider = serviceProvider;
            _logger = logger;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses SPL XML content and saves all extracted entities to the database.
        /// </summary>
        /// <param name="xmlContent">Raw XML content string to parse</param>
        /// <param name="fileNameInZip">Name of the file within the ZIP archive for logging</param>
        /// <returns>Import result containing success status, counts, and any errors encountered</returns>
        /// <example>
        /// var result = await parser.ParseAndSaveSplDataAsync(xmlContent, "drugLabel.xml");
        /// if (result.Success) {
        ///     Console.WriteLine($"Created {result.ProductsCreated} products");
        /// }
        /// </example>
        /// <remarks>
        /// Processes the document in order: Document metadata, Author organizations, 
        /// Structured body sections, and embedded products with ingredients.
        /// Uses database transactions through scoped service provider.
        /// </remarks>
        public async Task<SplFileImportResult> ParseAndSaveSplDataAsync(string xmlContent, string fileNameInZip)
        {
            #region implementation
            var fileResult = new SplFileImportResult { FileName = fileNameInZip };
            XDocument xdoc;

            // Parse XML document with error handling
            try
            {
                xdoc = XDocument.Parse(xmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse XML for file {FileName}", fileNameInZip);
                fileResult.Success = false;
                fileResult.Message = "XML parsing error.";
                fileResult.Errors.Add(ex.Message);
                return fileResult;
            }

            // Validate root document element
            var docElement = xdoc.Root;
            if (docElement == null || docElement.Name.LocalName != "document")
            {
                fileResult.Success = false;
                fileResult.Message = "XML root element is not <document>.";
                fileResult.Errors.Add("Invalid SPL XML structure.");
                return fileResult;
            }

            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;

            try
            {
                // 1. Parse and Save Label.Document
                var labelDocument = parseDocumentElement(docElement);

                if (labelDocument == null)
                {
                    fileResult.Errors.Add("Could not parse main document metadata.");
                    // Decide if this is a fatal error for the file
                }
                else
                {
                    var docRepo = getRepository<Label.Document>(sp);

                    await docRepo.CreateAsync(labelDocument); // PK labelDocument.DocumentID is now populated
                    fileResult.DocumentsCreated++;
                    _logger.LogInformation("Created Document with ID {DocumentID} for file {FileName}", labelDocument.DocumentID, fileNameInZip);

                    // 2. Parse and Save Author Organization and DocumentAuthor link
                    var authorOrgElement = docElement.Element(ns + "author")?.Element(ns + "assignedEntity")?.Element(ns + "representedOrganization");
                    if (authorOrgElement != null)
                    {
                        var organization = parseOrganizationElement(authorOrgElement, "Author");

                        if (organization != null)
                        {
                            var orgRepo = getRepository<Label.Organization>(sp);

                            await orgRepo.CreateAsync(organization); // PK organization.OrganizationID populated
                            fileResult.OrganizationsCreated++;

                            _logger.LogInformation("Created Organization (Author) with ID {OrganizationID}", organization.OrganizationID);

                            // Create document-author relationship
                            if (labelDocument.DocumentID.HasValue && organization.OrganizationID.HasValue)
                            {
                                var docAuthor = new Label.DocumentAuthor
                                {
                                    DocumentID = labelDocument.DocumentID.Value,
                                    OrganizationID = organization.OrganizationID.Value,
                                    AuthorType = "Labeler" // Default or determine from XML if possible
                                };
                                var docAuthorRepo = getRepository<Label.DocumentAuthor>(sp);

                                await docAuthorRepo.CreateAsync(docAuthor);
                                _logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
                                    labelDocument.DocumentID.Value, organization.OrganizationID.Value);
                            }
                        }
                    }

                    // 3. Parse Structured Body and Sections
                    var structuredBodyEl = docElement.Element(ns + "component")?.Element(ns + "structuredBody");

                    if (structuredBodyEl != null && labelDocument.DocumentID.HasValue)
                    {
                        var labelStructuredBody = new Label.StructuredBody { DocumentID = labelDocument.DocumentID.Value };

                        var sbRepo = getRepository<Label.StructuredBody>(sp);

                        await sbRepo.CreateAsync(labelStructuredBody); // PK labelStructuredBody.StructuredBodyID populated

                        _logger.LogInformation("Created StructuredBody with ID {StructuredBodyID} for DocumentID {DocumentID}",
                            labelStructuredBody.StructuredBodyID, labelDocument.DocumentID.Value);

                        // Process all section components
                        foreach (var sectionCompEl in structuredBodyEl.Elements(ns + "component"))
                        {
                            var sectionEl = sectionCompEl.Element(ns + "section");

                            if (sectionEl != null && labelStructuredBody.StructuredBodyID.HasValue)
                            {
                                await parseAndSaveSectionAsync(sectionEl, labelStructuredBody.StructuredBodyID.Value, null, sp, fileResult);
                            }
                        }
                    }

                    // Example: Parse top-level subject's manufacturedProduct (common in drug listings)
                    var subjectManufacturedProductEl = docElement.Element(ns + "component")?
                                                              .Element(ns + "structuredBody")?
                                                              .Elements(ns + "component")
                                                              .Select(c => c.Element(ns + "section"))
                                                              .FirstOrDefault(s => s != null)?
                                                              .Element(ns + "subject")?
                                                              .Element(ns + "manufacturedProduct");

                    // Or sometimes it's directly under a section, or even a section subject
                    // This part of SPL is very variable. For the given dd9bfbf7...xml, it's under a section.
                    // For A0067DCB...xml, it's under section/subject/manufacturedProduct
                    // For db6ed7ab...xml, it might be different as well.
                    // We'll assume a common pattern of it being under a section's subject for now.

                    // Placeholder for finding and parsing manufactured products - This is complex
                    // You would iterate sections, check for <subject><manufacturedProduct>
                    // Or find <manufacturedProduct> elements wherever they might appear based on SPL schema variations.
                    // For brevity, a direct parse of first found product is shown, but a loop is needed.

                    // Find first section containing a manufactured product
                    var firstSectionWithProduct = structuredBodyEl?.Elements(ns + "component")
                        .Select(c => c.Element(ns + "section"))
                        .FirstOrDefault(s => s?.Element(ns + "subject")?.Element(ns + "manufacturedProduct") != null);

                    if (firstSectionWithProduct != null)
                    {
                        var productEl = firstSectionWithProduct
                            ?.Element(ns + "subject")
                            ?.Element(ns + "manufacturedProduct")!;

                        var sectionIdForProduct = getSectionIdFromElement(firstSectionWithProduct, sp, fileResult); // TODO: Refine Helper method

                        if (productEl != null && sectionIdForProduct.HasValue)
                        {
                            await parseAndSaveManufacturedProductAsync(productEl, sectionIdForProduct, sp, fileResult);
                        }
                    }
                }

                // Set final result status based on error count
                fileResult.Success = fileResult.Errors.Count == 0;
                fileResult.Message = fileResult.Success ? "Imported successfully." : "Imported with errors.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SPL data for file {FileName}", fileNameInZip);
                fileResult.Success = false;
                fileResult.Message = "Database save error.";
                fileResult.Errors.Add($"Database save error: {ex.Message}");
            }
            return fileResult;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively parses and saves a section element and all its child sections.
        /// </summary>
        /// <param name="sectionEl">The section XML element to parse</param>
        /// <param name="structuredBodyId">ID of the parent structured body (for top-level sections)</param>
        /// <param name="parentSectionId">ID of the parent section (for nested sections)</param>
        /// <param name="sp">Service provider for repository access</param>
        /// <param name="fileResult">Import result to track progress and errors</param>
        /// <remarks>
        /// Handles both top-level sections (linked to structured body) and nested child sections.
        /// Also processes any manufactured products found as subjects within the section.
        /// </remarks>
        private async Task parseAndSaveSectionAsync(XElement sectionEl, int? structuredBodyId, int? parentSectionId, IServiceProvider sp, SplFileImportResult fileResult)
        {            
            #region implementation
            var labelSection = parseSectionElement(sectionEl, structuredBodyId, parentSectionId);

            if (labelSection == null)
            {
                fileResult.Errors.Add($"Could not parse section element: {sectionEl.Element(ns + "title")?.Value ?? "Untitled Section"}");
                return;
            }

            var sectionRepo = getRepository<Label.Section>(sp);

            await sectionRepo.CreateAsync(labelSection); // PK labelSection.SectionID populated

            fileResult.SectionsCreated++;

            _logger.LogInformation("Created Section {SectionTitle} with ID {SectionID}", labelSection.Title, labelSection.SectionID);

            // Parse and save child sections recursively
            foreach (var childSectionCompEl in sectionEl.Elements(ns + "component"))
            {
                var childSectionEl = childSectionCompEl.Element(ns + "section");

                if (childSectionEl != null && labelSection.SectionID.HasValue)
                {
                    // Create SectionHierarchy entry
                    var sectionHierarchy = new Label.SectionHierarchy
                    {
                        ParentSectionID = labelSection.SectionID.Value,
                        // ChildSectionID will be set after the child is created
                    };

                    // Recursively parse the child section
                    // The ChildSectionID in sectionHierarchy needs to be updated after child save.
                    // This requires careful handling, perhaps saving hierarchy after child.
                    // For simplicity here, we're not fully implementing SectionHierarchy
                    await parseAndSaveSectionAsync(childSectionEl, null, labelSection.SectionID.Value, sp, fileResult);
                }
            }

            // Parse manufacturedProduct if it's a subject of this section
            var subjectManufacturedProductEl = sectionEl?.Element(ns + "subject")?.Element(ns + "manufacturedProduct")!;

            if (subjectManufacturedProductEl != null
                && labelSection.SectionID.HasValue)
            {
                await parseAndSaveManufacturedProductAsync(subjectManufacturedProductEl, labelSection.SectionID, sp, fileResult);
            }

            // TODO: Parse SectionTextContent, TextList, TextTable, ObservationMedia, RenderedMedia etc.
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves a manufactured product element along with its ingredients.
        /// </summary>
        /// <param name="productEl">The manufacturedProduct XML element</param>
        /// <param name="sectionId">ID of the section containing this product</param>
        /// <param name="sp">Service provider for repository access</param>
        /// <param name="fileResult">Import result to track progress and errors</param>
        /// <remarks>
        /// Handles SPL variations where the product may be nested within manufacturedMedicine 
        /// or be the manufacturedProduct element itself. Processes all ingredient types.
        /// </remarks>
        private async Task parseAndSaveManufacturedProductAsync(XElement productEl, int? sectionId, IServiceProvider sp, SplFileImportResult fileResult)
        {           
            #region implementation
            var manufacturedMedicineEl = productEl.Element(ns + "manufacturedMedicine") ?? productEl.Element(ns + "manufacturedProduct"); // SPL variations

            if (manufacturedMedicineEl == null)
            {
                manufacturedMedicineEl = productEl; // Sometimes productEl is already manufacturedMedicine

                if (manufacturedMedicineEl.Name.LocalName != "manufacturedMedicine" && manufacturedMedicineEl.Name.LocalName != "manufacturedProduct")
                {
                    fileResult.Errors.Add("Could not find <manufacturedMedicine> or <manufacturedProduct> element for product parsing.");
                    return;
                }
            }

            // Create product entity with basic information
            var labelProduct = new Label.Product
            {
                SectionID = sectionId,
                ProductName = manufacturedMedicineEl.Element(ns + "name")?.Value,
                FormCode = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("code")?.Value,
                FormCodeSystem = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("codeSystem")?.Value,
                FormDisplayName = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("displayName")?.Value
            };

            var productRepo = getRepository<Label.Product>(sp);

            await productRepo.CreateAsync(labelProduct);

            fileResult.ProductsCreated++;
            _logger.LogInformation("Created Product {ProductName} with ID {ProductID}", labelProduct.ProductName, labelProduct.ProductID);

            // Parse all ingredient types
            foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "ingredient")) // Or productEl.Elements for other SPLs
            {
                await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult);
            }
            foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "activeIngredient")) // Common in older SPLs
            {
                await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult, isExplicitlyActive: true);
            }
            foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "inactiveIngredient"))
            {
                await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult, isExplicitlyInactive: true);
            }

            // TODO: Parse ProductIdentifier, GenericMedicine, SpecializedKind, EquivalentEntity, PackagingLevel, MarketingCategory, Characteristic etc.
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves an ingredient element including its substance and active moiety information.
        /// </summary>
        /// <param name="ingredientEl">The ingredient XML element to parse</param>
        /// <param name="productId">ID of the product this ingredient belongs to</param>
        /// <param name="sp">Service provider for repository access</param>
        /// <param name="fileResult">Import result to track progress and errors</param>
        /// <param name="isExplicitlyActive">Whether this ingredient is explicitly marked as active</param>
        /// <param name="isExplicitlyInactive">Whether this ingredient is explicitly marked as inactive</param>
        /// <remarks>
        /// Handles quantity information with numerator/denominator units and creates
        /// associated active moiety records when present.
        /// </remarks>
        private async Task parseAndSaveIngredientAsync(XElement ingredientEl, int? productId, IServiceProvider sp, SplFileImportResult fileResult, bool isExplicitlyActive = false, bool isExplicitlyInactive = false)
        {     
            #region implementation
            if (!productId.HasValue) return;

            // Find ingredient substance element (various naming patterns)
            var ingredientSubstanceEl = ingredientEl?.Element(ns + "ingredientSubstance")
                ?? ingredientEl?.Element(ns + "activeIngredientSubstance")
                ?? ingredientEl?.Element(ns + "inactiveIngredientSubstance");

            if (ingredientSubstanceEl == null)
            {
                fileResult.Errors.Add($"Could not parse ingredient substance for ProductID {productId}.");
                return;
            }

            // Create ingredient substance record
            var labelIngSubstance = new Label.IngredientSubstance
            {
                UNII = ingredientSubstanceEl.Element(ns + "code")
                ?.Attribute("code")
                ?.Value,

                SubstanceName = ingredientSubstanceEl.Element(ns + "name")?.Value
            };
            var ingSubRepo = getRepository<Label.IngredientSubstance>(sp);

            await ingSubRepo.CreateAsync(labelIngSubstance); // PK labelIngSubstance.IngredientSubstanceID populated

            _logger.LogInformation("Created IngredientSubstance {SubstanceName} with ID {IngredientSubstanceID}", labelIngSubstance.SubstanceName, labelIngSubstance.IngredientSubstanceID);

            // Create ingredient record linking product to substance
            var labelIngredient = new Label.Ingredient
            {
                ProductID = productId,
                IngredientSubstanceID = labelIngSubstance.IngredientSubstanceID,
                ClassCode = ingredientEl?.Attribute("classCode")?.Value
            };

            // Set default class codes for explicitly active/inactive ingredients
            if (isExplicitlyActive) labelIngredient.ClassCode = labelIngredient.ClassCode ?? "ACTIB"; // Active Ingredient Base

            if (isExplicitlyInactive) labelIngredient.ClassCode = labelIngredient.ClassCode ?? "IACT"; // Inactive

            // Parse quantity information (numerator/denominator pattern)
            var quantityEl = ingredientEl?.Element(ns + "quantity");

            if (quantityEl != null)
            {
                var numeratorEl = quantityEl.Element(ns + "numerator");
                var denominatorEl = quantityEl.Element(ns + "denominator");

                if (numeratorEl != null && numeratorEl?.Attribute("value") != null)
                    labelIngredient.QuantityNumerator = parseNullableDecimal(numeratorEl?.Attribute("value")?.Value!);

                labelIngredient.QuantityNumeratorUnit = numeratorEl?.Attribute("unit")?.Value;

                if (denominatorEl != null && denominatorEl?.Attribute("value") != null)
                    labelIngredient.QuantityDenominator = parseNullableDecimal(denominatorEl?.Attribute("value")?.Value!);

                labelIngredient.QuantityDenominatorUnit = denominatorEl?.Attribute("unit")?.Value;
            }

            var ingredientRepo = getRepository<Label.Ingredient>(sp);

            await ingredientRepo.CreateAsync(labelIngredient);

            _logger.LogInformation("Created Ingredient for SubstanceID {IngredientSubstanceID} linked to ProductID {ProductID}", labelIngredient.IngredientSubstanceID, productId);

            // Parse Active Moiety information
            var activeMoietyContainerEl = ingredientSubstanceEl.Element(ns + "activeMoiety"); // could be plural
            if (activeMoietyContainerEl != null)
            {
                foreach (var activeMoietyEl in activeMoietyContainerEl.Elements(ns + "activeMoiety")) // Handle nested activeMoiety
                {
                    if (labelIngSubstance.IngredientSubstanceID.HasValue)
                    {
                        var labelActiveMoiety = new Label.ActiveMoiety
                        {
                            IngredientSubstanceID = labelIngSubstance.IngredientSubstanceID.Value,
                            MoietyUNII = activeMoietyEl.Element(ns + "code")?.Attribute("code")?.Value,
                            MoietyName = activeMoietyEl.Element(ns + "name")?.Value
                        };
                        var amRepo = getRepository<Label.ActiveMoiety>(sp);
                        await amRepo.CreateAsync(labelActiveMoiety);
                        _logger.LogInformation("Created ActiveMoiety {MoietyName} for IngredientSubstanceID {IngredientSubstanceID}", labelActiveMoiety.MoietyName, labelIngSubstance.IngredientSubstanceID.Value);
                    }
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the main document element extracting metadata and identifiers.
        /// </summary>
        /// <param name="docElement">The document root XML element</param>
        /// <returns>Label.Document entity or null if parsing fails</returns>
        /// <remarks>
        /// Extracts document GUID, codes, title, effective time, set ID, and version information.
        /// </remarks>
        private Label.Document? parseDocumentElement(XElement docElement)
        {
            
            #region implementation
            try
            {
                return new Label.Document
                {
                    DocumentGUID = parseNullableGuid(docElement.Element(ns + "id")?.Attribute("root")?.Value!),

                    DocumentCode = docElement.Element(ns + "code")?.Attribute("code")?.Value,

                    DocumentCodeSystem = docElement.Element(ns + "code")?.Attribute("codeSystem")?.Value,

                    DocumentDisplayName = docElement.Element(ns + "code")?.Attribute("displayName")?.Value,

                    Title = docElement.Element(ns + "title")?.Value.Trim(),

                    EffectiveTime = parseNullableDateTime(docElement.Element(ns + "effectiveTime")?.Attribute("value")?.Value!),

                    SetGUID = parseNullableGuid(docElement.Element(ns + "setId")?.Attribute("root")?.Value!),

                    VersionNumber = parseNullableInt(docElement.Element(ns + "versionNumber")?.Attribute("value")?.Value!),
                    // SubmissionFileName might not be in the XML itself, but rather the name of the file in the ZIP.
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing <document> element attributes.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an organization element extracting name and confidentiality information.
        /// </summary>
        /// <param name="orgElement">The organization XML element to parse</param>
        /// <param name="typeHint">Type hint for logging context (e.g., "Author")</param>
        /// <returns>Label.Organization entity or null if parsing fails</returns>
        private Label.Organization? parseOrganizationElement(XElement orgElement, string typeHint)
        {
            
            #region implementation
            try
            {
                return new Label.Organization
                {
                    OrganizationName = orgElement.Element(ns + "name")?.Value.Trim(),

                    IsConfidential = parseNullableBool(orgElement.Element(ns + "confidentialityCode")?.Attribute("code")?.Value == "B")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing <organization> element ({TypeHint}).", typeHint);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a section element extracting metadata and hierarchy information.
        /// </summary>
        /// <param name="sectionEl">The section XML element to parse</param>
        /// <param name="structuredBodyId">ID of parent structured body (for top-level sections)</param>
        /// <param name="parentSectionId">ID of parent section (for nested sections)</param>
        /// <returns>Label.Section entity or null if parsing fails</returns>
        private Label.Section? parseSectionElement(XElement sectionEl, int? structuredBodyId, int? parentSectionId)
        {        
            #region implementation
            try
            {
                var section = new Label.Section
                {
                    // Only for top-level sections. ParentSectionID would
                    // be set if this is a child, handled by SectionHierarchy
                    StructuredBodyID = structuredBodyId ?? null, 
    
                    SectionGUID = parseNullableGuid(sectionEl.Element(ns + "id")?.Attribute("root")?.Value!) ?? Guid.Empty,

                    SectionCode = sectionEl.Element(ns + "code")?.Attribute("code")?.Value ?? string.Empty,

                    SectionCodeSystem = sectionEl.Element(ns + "code")?.Attribute("codeSystem")?.Value ?? string.Empty,

                    SectionDisplayName = sectionEl.Element(ns + "code")?.Attribute("displayName")?.Value ?? string.Empty,

                    Title = sectionEl.Element(ns + "title")?.Value.Trim() ?? string.Empty,

                    EffectiveTime = parseNullableDateTime(sectionEl.Element(ns + "effectiveTime")?.Attribute("value")?.Value!) ?? DateTime.MinValue
                };
                return section;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing <section> element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to retrieve the database ID for a section element.
        /// </summary>
        /// <param name="sectionEl">The section XML element</param>
        /// <param name="sp">Service provider for potential database queries</param>
        /// <param name="fileResult">Import result to record errors</param>
        /// <returns>Section ID if found, otherwise null</returns>
        /// <remarks>
        /// INCOMPLETE IMPLEMENTATION: This method currently always returns null and logs an error.
        /// A complete implementation would need to query existing sections by GUID or maintain
        /// a mapping of parsed sections to their database IDs.
        /// </remarks>
        private int? getSectionIdFromElement(XElement sectionEl, IServiceProvider sp, SplFileImportResult fileResult)
        {
            
            #region implementation
            // This is tricky. If the section was just saved, its ID would be on the entity.
            // If it's a reference to an *existing* section by GUID, you'd need to query.
            // For simplicity, this assumes the section might have an ID attribute that we could use,
            // or relies on a prior save. The current ParseAndSaveSectionAsync populates the ID on the entity.
            // This helper needs to be used in a context where the section's DB ID is known.
            var sectionGuid = parseNullableGuid(sectionEl.Element(ns + "id")?.Attribute("root")?.Value);

            if (sectionGuid.HasValue)
            {
                // In a real scenario, you might need to query the DB if this section was saved in a different pass
                // For now, we'll assume this is insufficient to get a DB ID without prior context.
                // This highlights a complexity in mapping deeply nested or referenced structures.
                // The current structure implies ParseAndSaveSectionAsync is called, which *gets* the ID.
                // This helper is more for when a Product refers to a Section that *should* already exist or be parseable.
                _logger.LogWarning("GetSectionIdFromElement by GUID is not fully implemented for querying existing sections. Product may not be linked correctly if section not parsed in same flow.");
            }

            fileResult.Errors.Add("Cannot reliably get SectionID for product linking without a more robust section tracking mechanism or assuming it's the current section being parsed.");

            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a repository instance for the specified entity type from the service provider.
        /// </summary>
        /// <typeparam name="TRepoType">The entity type for which to get a repository</typeparam>
        /// <param name="sp">Service provider to resolve dependencies from</param>
        /// <returns>Repository instance for the specified type</returns>
        /// <exception cref="InvalidOperationException">Thrown when repository cannot be resolved</exception>
        private Repository<TRepoType> getRepository<TRepoType>(IServiceProvider sp) where TRepoType : class
        {            
            #region implementation

            var repo = sp.GetService<Repository<TRepoType>>();

            if (repo == null)
            {
                throw new InvalidOperationException($"Could not resolve repository for type {typeof(TRepoType).FullName}. Ensure it and its dependencies are registered.");
            }

            return repo;
            #endregion
        }

        #region Helper Methods for Type Parsing

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable integer.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed integer or null if parsing fails</returns>
        private int? parseNullableInt(string value) => int.TryParse(value, out int result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable GUID.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed GUID or null if parsing fails</returns>
        private Guid? parseNullableGuid(string value) => Guid.TryParse(value, out Guid result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses SPL date/time strings which can be in various formats.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed DateTime or null if parsing fails</returns>
        /// <remarks>
        /// Handles SPL date formats: YYYY, YYYYMM, YYYYMMDD, YYYYMMDDHHMMSS, etc.
        /// </remarks>
        private DateTime? parseNullableDateTime(string value)
        {
            /**************************************************************/
            #region implementation
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Dates in SPL can be YYYY, YYYYMM, YYYYMMDD, YYYYMMDDHHMMSS etc.
            // DateTime.Parse/TryParse might need specific format strings.
            // For YYYYMMDD:
            if (value.Length == 8 && int.TryParse(value.Substring(0, 4), out int year) &&
                int.TryParse(value.Substring(4, 2), out int month) &&
                int.TryParse(value.Substring(6, 2), out int day))
            {
                try { return new DateTime(year, month, day); }
                catch { /* Fall through */ }
            }
            return DateTime.TryParse(value, out DateTime result) ? result : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable decimal.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed decimal or null if parsing fails</returns>
        private decimal? parseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable boolean.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed boolean or null if parsing fails</returns>
        private bool? parseNullableBool(string value) => bool.TryParse(value, out bool result) ? result : (bool?)null;

        /**************************************************************/
        /// <summary>
        /// Converts a boolean value to nullable boolean (overload for direct bool input).
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <returns>The boolean value as nullable boolean</returns>
        private bool? parseNullableBool(bool value) => value; // Overload for direct bool

        #endregion
    }
}