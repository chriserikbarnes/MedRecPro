using System.Xml.Linq;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models.Validation;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Specialized parser for handling Warning Letter Alert indexing sections (48779-3).
    /// Processes product information and date elements according to SPL Implementation Guide Section 21.2.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex structures found in Warning Letter Alert sections,
    /// extracting product identification details and alert dates while ensuring compliance
    /// with FDA SPL Implementation Guide validation requirements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="WarningLetterProductInfo"/>
    /// <seealso cref="WarningLetterDate"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class WarningLetterParser : ISplSectionParser
    {
        #region Private Fields
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Warning Letter Alert section code identifier (48779-3).
        /// </summary>
        /// <seealso cref="Label"/>
        private const string WARNING_LETTER_SECTION_CODE = c.WARNING_LETTER_SECTION_CODE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the WarningLetterParser.
        /// Uses parameterless constructor - gets dependencies from SplParseContext.
        /// </summary>
        /// <seealso cref="Label"/>
        public WarningLetterParser() { }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing warning letter processing.
        /// </summary>
        /// <seealso cref="Label"/>
        public string SectionName => "warningLetter";

        /**************************************************************/
        /// <summary>
        /// Parses Warning Letter Alert section elements, extracting product information
        /// and date elements according to SPL Implementation Guide Section 21.2.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for warning letter content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and warning letter elements created.</returns>
        /// <example>
        /// <code>
        /// var parser = new WarningLetterParser();
        /// var result = await parser.ParseAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Product info records: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method validates that the section is a Warning Letter Alert section (48779-3)
        /// before processing. It extracts both product information and date elements,
        /// applying the validation rules specified in the SPL Implementation Guide.
        /// </remarks>
        /// <seealso cref="parseWarningLetterProductInfoAsync"/>
        /// <seealso cref="parseWarningLetterDateAsync"/>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate context and current section
                if (context?.CurrentSection?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("No current section available for warning letter parsing.");
                    return result;
                }

                // Validate this is a Warning Letter Alert section (48779-3)
                if (!isWarningLetterSection(context.CurrentSection, context))
                {
                    // Not an error - just not applicable for this parser
                    return new SplParseResult { Success = true };
                }

                reportProgress?.Invoke("Processing Warning Letter Alert section...");

                var sectionId = context.CurrentSection.SectionID.Value;

                // Parse product information elements (SPL IG Section 21.2.2)
                var productInfoResult = await parseWarningLetterProductInfoAsync(element, sectionId, context, reportProgress);
                result.MergeFrom(productInfoResult);

                // Parse date elements (SPL IG Section 21.2.3)
                var dateResult = await parseWarningLetterDateAsync(element, sectionId, context, reportProgress);
                result.MergeFrom(dateResult);

                reportProgress?.Invoke($"Warning Letter processing completed: {result.SectionAttributesCreated} elements created");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing warning letter section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing warning letter section for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        #region Product Information Processing Methods

        /**************************************************************/
        /// <summary>
        /// Parses Warning Letter product information from manufacturedProduct elements
        /// according to SPL Implementation Guide Section 21.2.2.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the Warning Letter section.</param>
        /// <param name="sectionId">The database ID of the current section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A SplParseResult containing the outcome of product information parsing.</returns>
        /// <remarks>
        /// Extracts product identification details including proprietary name, generic name,
        /// form code, strength information, and item codes. Applies validation rules
        /// from SPL IG sections 21.2.2.1 through 21.2.2.10.
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="extractProductIdentificationDetails"/>
        /// <seealso cref="getOrCreateWarningLetterProductInfoAsync"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseWarningLetterProductInfoAsync(
            XElement sectionEl,
            int sectionId,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Find all manufacturedProduct elements within subject elements
                // Navigate: section/subject/manufacturedProduct/manufacturedProduct (nested structure)
                var productElements = sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.ManufacturedProduct);

                foreach (var productEl in productElements)
                {
                    try
                    {
                        reportProgress?.Invoke("Parsing product information...");

                        // Extract product identification details from XML
                        var productInfo = extractProductIdentificationDetails(productEl, sectionId);

                        if (productInfo != null)
                        {
                            // Apply validation before saving
                            var validationResult = validateProductInfo(productInfo);
                            if (!string.IsNullOrEmpty(validationResult))
                            {
                                result.Errors.Add($"Product validation failed: {validationResult}");
                                continue;
                            }

                            // Create or retrieve existing product info record
                            var createdProductInfo = await getOrCreateWarningLetterProductInfoAsync(productInfo, context);
                            if (createdProductInfo != null)
                            {
                                result.SectionAttributesCreated++;
                                reportProgress?.Invoke($"Product info processed: {createdProductInfo.ProductName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error parsing individual product: {ex.Message}");
                        context?.Logger?.LogError(ex, "Error parsing product information for section {SectionId}", sectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing warning letter product information: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing product information for warning letter section");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts product identification details from a manufacturedProduct XML element
        /// according to SPL Implementation Guide requirements.
        /// </summary>
        /// <param name="productEl">The XElement representing the manufacturedProduct element.</param>
        /// <param name="sectionId">The database ID of the owning section.</param>
        /// <returns>A WarningLetterProductInfo entity populated with extracted data, or null if extraction fails.</returns>
        /// <remarks>
        /// Implements extraction for SPL IG sections 21.2.2.1-21.2.2.6:
        /// - Proprietary name (21.2.2.1)
        /// - Generic name (21.2.2.2) 
        /// - Form code and code system (21.2.2.3-21.2.2.4)
        /// - Strength amounts (21.2.2.5)
        /// - Product item codes (21.2.2.6)
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="extractStrengthText"/>
        /// <seealso cref="extractItemCodesText"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private WarningLetterProductInfo? extractProductIdentificationDetails(XElement productEl, int sectionId)
        {
            #region implementation
            try
            {
                // 21.2.2.1 - Extract proprietary name with optional suffix
                var nameEl = productEl.SplElement(sc.E.Name);
                var proprietaryName = nameEl?.Value?.Trim();
                var suffixEl = nameEl?.SplElement(sc.E.Suffix);
                if (suffixEl != null && !string.IsNullOrEmpty(suffixEl.Value))
                {
                    proprietaryName = $"{proprietaryName} {suffixEl.Value.Trim()}";
                }

                // 21.2.2.2 - Extract generic medicine name
                var genericName = productEl.SplElement(sc.E.AsEntityWithGeneric, sc.E.GenericMedicine, sc.E.Name)?.Value?.Trim();

                // 21.2.2.3-21.2.2.4 - Extract form code information
                var formCodeEl = productEl.SplElement(sc.E.FormCode);
                var formCode = formCodeEl?.GetAttrVal(sc.A.CodeValue);
                var formCodeSystem = formCodeEl?.GetAttrVal(sc.A.CodeSystem);
                var formDisplayName = formCodeEl?.GetAttrVal(sc.A.DisplayName);

                // 21.2.2.5 - Extract strength information
                var strengthText = extractStrengthText(productEl);

                // 21.2.2.6 - Extract product item codes
                var itemCodesText = extractItemCodesText(productEl);

                // Create product info entity with extracted data
                return new WarningLetterProductInfo
                {
                    SectionID = sectionId,
                    ProductName = proprietaryName,
                    GenericName = genericName,
                    FormCode = formCode,
                    FormCodeSystem = formCodeSystem,
                    FormDisplayName = formDisplayName,
                    StrengthText = strengthText,
                    ItemCodesText = itemCodesText
                };
            }
            catch (Exception ex)
            {
                // Log error but don't throw - return null to indicate failure
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts strength information text from ingredient elements within the product.
        /// Combines numerator and denominator values for active ingredients as per SPL IG 21.2.2.5.
        /// </summary>
        /// <param name="productEl">The XElement representing the manufacturedProduct element.</param>
        /// <returns>A formatted string containing strength information, or null if no strength data found.</returns>
        /// <remarks>
        /// Looks for ingredient elements with quantity/numerator/denominator structures
        /// and formats them as human-readable strength text for storage.
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private string? extractStrengthText(XElement productEl)
        {
            #region implementation
            var strengthParts = new List<string>();

            try
            {
                // Look for ingredient elements with quantity information
                var ingredientElements = productEl.SplElements(sc.E.Ingredient);

                foreach (var ingredientEl in ingredientElements)
                {
                    var quantityEl = ingredientEl.SplElement(sc.E.Quantity);
                    if (quantityEl != null)
                    {
                        var numeratorEl = quantityEl.SplElement(sc.E.Numerator);
                        var denominatorEl = quantityEl.SplElement(sc.E.Denominator);

                        if (numeratorEl != null)
                        {
                            var numValue = numeratorEl.GetAttrVal(sc.A.Value);
                            var numUnit = numeratorEl.GetAttrVal(sc.A.Unit);
                            var denValue = denominatorEl?.GetAttrVal(sc.A.Value);
                            var denUnit = denominatorEl?.GetAttrVal(sc.A.Unit);

                            // Format as "value unit/value unit" or just "value unit" if no denominator
                            if (!string.IsNullOrEmpty(numValue))
                            {
                                var strengthText = $"{numValue} {numUnit}";
                                if (!string.IsNullOrEmpty(denValue))
                                {
                                    strengthText += $"/{denValue} {denUnit}";
                                }
                                strengthParts.Add(strengthText);
                            }
                        }
                    }
                }

                return strengthParts.Any() ? string.Join("; ", strengthParts) : null;
            }
            catch
            {
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts product item codes from code elements within the product.
        /// Handles NDC/NHRIC codes and other identification codes as per SPL IG 21.2.2.6-21.2.2.10.
        /// </summary>
        /// <param name="productEl">The XElement representing the manufacturedProduct element.</param>
        /// <returns>A formatted string containing item codes, or null if no codes found.</returns>
        /// <remarks>
        /// Searches for code elements with different code systems and formats them
        /// as a semicolon-separated list for storage in the database.
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private string? extractItemCodesText(XElement productEl)
        {
            #region implementation
            var itemCodes = new List<string>();

            try
            {
                // Look for code elements that represent product identifiers
                var codeElements = productEl.SplElements(sc.E.Code);

                foreach (var codeEl in codeElements)
                {
                    var codeValue = codeEl.GetAttrVal(sc.A.CodeValue);
                    var codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                    if (!string.IsNullOrEmpty(codeValue))
                    {
                        // Format as "codeSystem:codeValue" or just "codeValue" if no system
                        var itemCode = !string.IsNullOrEmpty(codeSystem)
                            ? $"{codeSystem}:{codeValue}"
                            : codeValue;
                        itemCodes.Add(itemCode);
                    }
                }

                return itemCodes.Any() ? string.Join("; ", itemCodes) : null;
            }
            catch
            {
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates warning letter product information using the configured validation attributes.
        /// </summary>
        /// <param name="productInfo">The WarningLetterProductInfo entity to validate.</param>
        /// <returns>A validation error message if validation fails, or null if validation passes.</returns>
        /// <remarks>
        /// Applies the validation rules from WarningLetterProductInfoValidationAttribute
        /// and related validation classes to ensure compliance with SPL IG requirements.
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="WarningLetterProductInfoValidationAttribute"/>
        /// <seealso cref="Label"/>
        private string? validateProductInfo(WarningLetterProductInfo productInfo)
        {
            #region implementation
            try
            {
                // Use the built-in validation method
                productInfo.ValidateAll();
                return null; // Validation passed
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets or creates a WarningLetterProductInfo record in the database,
        /// performing deduplication based on section and product characteristics.
        /// </summary>
        /// <param name="productInfo">The WarningLetterProductInfo entity to create or find.</param>
        /// <param name="context">The current parsing context for database access.</param>
        /// <returns>The created or existing WarningLetterProductInfo entity, or null if operation fails.</returns>
        /// <remarks>
        /// Performs deduplication based on SectionID and ProductName to avoid
        /// creating duplicate records for the same product in the same section.
        /// </remarks>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<WarningLetterProductInfo?> getOrCreateWarningLetterProductInfoAsync(
            WarningLetterProductInfo productInfo,
            SplParseContext context)
        {
            #region implementation
            try
            {
                if (context?.ServiceProvider == null)
                    return null;

                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var repo = context.GetRepository<WarningLetterProductInfo>();
                var dbSet = dbContext.Set<WarningLetterProductInfo>();

                // Deduplication: Look for existing record by section and product name
                var existing = await dbSet.FirstOrDefaultAsync(p =>
                    p.SectionID == productInfo.SectionID &&
                    p.ProductName == productInfo.ProductName);

                if (existing != null)
                {
                    return existing;
                }

                // Create new record if not found
                await repo.CreateAsync(productInfo);
                return productInfo;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating WarningLetterProductInfo for section {SectionId}", productInfo.SectionID);
                return null;
            }
            #endregion
        }

        #endregion

        #region Date Processing Methods

        /**************************************************************/
        /// <summary>
        /// Parses Warning Letter date information from effectiveTime elements
        /// according to SPL Implementation Guide Section 21.2.3.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the Warning Letter section.</param>
        /// <param name="sectionId">The database ID of the current section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A SplParseResult containing the outcome of date parsing.</returns>
        /// <remarks>
        /// Extracts alert issue date and optional resolution date from effectiveTime
        /// elements, applying validation rules from SPL IG sections 21.2.3.1-21.2.3.5.
        /// </remarks>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="extractWarningLetterDates"/>
        /// <seealso cref="getOrCreateWarningLetterDateAsync"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseWarningLetterDateAsync(
            XElement sectionEl,
            int sectionId,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                reportProgress?.Invoke("Parsing warning letter dates...");

                // Extract date information from effectiveTime elements
                var warningLetterDate = extractWarningLetterDates(sectionEl, sectionId);

                if (warningLetterDate != null)
                {
                    // Apply validation before saving
                    var validationResult = validateWarningLetterDate(warningLetterDate);
                    if (!string.IsNullOrEmpty(validationResult))
                    {
                        result.Errors.Add($"Date validation failed: {validationResult}");
                        return result;
                    }

                    // Create or retrieve existing date record
                    var createdDate = await getOrCreateWarningLetterDateAsync(warningLetterDate, context);
                    if (createdDate != null)
                    {
                        result.SectionAttributesCreated++;
                        reportProgress?.Invoke($"Warning letter dates processed: {createdDate.AlertIssueDate:yyyy-MM-dd}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing warning letter dates: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing dates for warning letter section");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts Warning Letter date information from effectiveTime elements
        /// according to SPL Implementation Guide Section 21.2.3.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the Warning Letter section.</param>
        /// <param name="sectionId">The database ID of the owning section.</param>
        /// <returns>A WarningLetterDate entity populated with extracted dates, or null if no dates found.</returns>
        /// <remarks>
        /// Implements extraction for SPL IG sections 21.2.3.1-21.2.3.4:
        /// - Alert issue date from effectiveTime low boundary (21.2.3.1, 21.2.3.3)
        /// - Optional resolution date from effectiveTime high boundary (21.2.3.2, 21.2.3.4)
        /// </remarks>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private WarningLetterDate? extractWarningLetterDates(XElement sectionEl, int sectionId)
        {
            #region implementation
            try
            {
                // Look for effectiveTime element with low/high boundaries
                var effectiveTimeEl = sectionEl.SplElement(sc.E.EffectiveTime);
                if (effectiveTimeEl == null)
                {
                    return null;
                }

                // 21.2.3.1, 21.2.3.3 - Extract alert issue date from low boundary
                var lowEl = effectiveTimeEl.SplElement(sc.E.Low);
                var alertIssueDate = lowEl != null
                    ? Util.ParseNullableDateTime(lowEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // 21.2.3.2, 21.2.3.4 - Extract optional resolution date from high boundary
                var highEl = effectiveTimeEl.SplElement(sc.E.High);
                var resolutionDate = highEl != null
                    ? Util.ParseNullableDateTime(highEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Only create entity if we have at least an alert issue date
                if (alertIssueDate.HasValue)
                {
                    return new WarningLetterDate
                    {
                        SectionID = sectionId,
                        AlertIssueDate = alertIssueDate,
                        ResolutionDate = resolutionDate
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log error but don't throw - return null to indicate failure
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates warning letter date information using the configured validation attributes.
        /// </summary>
        /// <param name="warningLetterDate">The WarningLetterDate entity to validate.</param>
        /// <returns>A validation error message if validation fails, or null if validation passes.</returns>
        /// <remarks>
        /// Applies the validation rules from WarningLetterDateValidationAttribute
        /// and WarningLetterDateConsistencyValidationAttribute to ensure compliance with SPL IG requirements.
        /// </remarks>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="WarningLetterDateValidationAttribute"/>
        /// <seealso cref="WarningLetterDateConsistencyValidationAttribute"/>
        /// <seealso cref="Label"/>
        private string? validateWarningLetterDate(WarningLetterDate warningLetterDate)
        {
            #region implementation
            try
            {
                // Use the built-in validation method
                warningLetterDate.ValidateAll();
                return null; // Validation passed
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets or creates a WarningLetterDate record in the database,
        /// performing deduplication based on section ID.
        /// </summary>
        /// <param name="warningLetterDate">The WarningLetterDate entity to create or find.</param>
        /// <param name="context">The current parsing context for database access.</param>
        /// <returns>The created or existing WarningLetterDate entity, or null if operation fails.</returns>
        /// <remarks>
        /// Performs deduplication based on SectionID since each Warning Letter section
        /// should have only one set of dates.
        /// </remarks>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<WarningLetterDate?> getOrCreateWarningLetterDateAsync(
            WarningLetterDate warningLetterDate,
            SplParseContext context)
        {
            #region implementation
            try
            {
                if (context?.ServiceProvider == null)
                    return null;

                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var repo = context.GetRepository<WarningLetterDate>();
                var dbSet = dbContext.Set<WarningLetterDate>();

                // Deduplication: Look for existing record by section ID
                var existing = await dbSet.FirstOrDefaultAsync(d => d.SectionID == warningLetterDate.SectionID);

                if (existing != null)
                {
                    // Update existing record with new information if needed
                    bool needsUpdate = false;

                    if (warningLetterDate.AlertIssueDate.HasValue &&
                        existing.AlertIssueDate != warningLetterDate.AlertIssueDate)
                    {
                        existing.AlertIssueDate = warningLetterDate.AlertIssueDate;
                        needsUpdate = true;
                    }

                    if (warningLetterDate.ResolutionDate.HasValue &&
                        existing.ResolutionDate != warningLetterDate.ResolutionDate)
                    {
                        existing.ResolutionDate = warningLetterDate.ResolutionDate;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        await dbContext.SaveChangesAsync();
                    }

                    return existing;
                }

                // Create new record if not found
                await repo.CreateAsync(warningLetterDate);
                return warningLetterDate;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating WarningLetterDate for section {SectionId}", warningLetterDate.SectionID);
                return null;
            }
            #endregion
        }

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Determines if the current section is a Warning Letter Alert section (48779-3) 
        /// within a Warning Letter Alert document (77288-9).
        /// </summary>
        /// <param name="section">The Section entity to check.</param>
        /// <param name="context">The parsing context containing document information.</param>
        /// <returns>True if this is a Warning Letter Alert section within a Warning Letter Alert document, false otherwise.</returns>
        /// <remarks>
        /// This method performs a dual check to ensure we only process warning letter content
        /// when both conditions are met:
        /// 1. The section code is "48779-3" (SPL indexing data elements section)
        /// 2. The containing document code is "77288-9" (Warning Letter Alert document)
        /// 
        /// This prevents false positives from other indexing documents (like Biologic or Drug Substance 
        /// documents with code "77648-4") that may contain the same section code but should not be 
        /// processed for warning letter content.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private bool isWarningLetterSection(Section section, SplParseContext context)
        {
            #region implementation
            // First check: Must be an SPL indexing data elements section (48779-3)
            if (!section?.SectionCode?.Equals(c.WARNING_LETTER_SECTION_CODE, StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }

            // Second check: Must be within a Warning Letter Alert document (77288-9)
            if (context?.Document?.DocumentCode?.Equals(c.WARNING_LETTER_DOCUMENT_CODE, StringComparison.OrdinalIgnoreCase) != true)
            {
                return false;
            }

            return true;
            #endregion
        }

        #endregion
    }
}
