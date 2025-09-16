using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.DataAccess;
using System;
using System.Threading.Tasks;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses marketing status elements from SPL documents, handling both product-level and packaging-level marketing acts.
    /// This parser can traverse nested packaging structures to find marketing status information at any depth.
    /// </summary>
    /// <remarks>
    /// Marketing status information can appear at multiple levels within an SPL document:
    /// - At the product level (manufacturedProduct/subjectOf/marketingAct)
    /// - At various packaging levels (asContent/subjectOf/marketingAct or containerPackagedProduct/subjectOf/marketingAct)
    /// 
    /// This parser determines the appropriate association (Product vs PackagingLevel) based on the parsing context
    /// and recursively processes nested packaging structures to ensure complete coverage.
    /// 
    /// The parser validates marketing activity codes against the FDA SPL code system (2.16.840.1.113883.3.26.1.1)
    /// and accepts only permitted status codes: active, completed, new, cancelled.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="PackagingLevel"/>
    /// <seealso cref="MarketingStatus"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class MarketingStatusParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        /// <seealso cref="Label"/>
        public string SectionName => "marketingstatus";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        /// <seealso cref="Label"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses marketing status elements from the specified XML element and all nested packaging levels.
        /// </summary>
        /// <param name="element">The XElement to parse for marketing status information.</param>
        /// <param name="context">The current parsing context containing product and packaging level information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and count of created MarketingStatus entities.</returns>
        /// <remarks>
        /// This method serves as the main entry point for marketing status parsing. It processes marketing acts
        /// at the current element level and recursively processes all nested packaging structures to ensure
        /// complete coverage of marketing status information throughout the packaging hierarchy.
        /// 
        /// The method determines whether to associate marketing status with a Product or PackagingLevel based
        /// on the current parsing context. If CurrentPackagingLevel is set, the status is associated with
        /// the packaging level; otherwise, it's associated with the current product.
        /// </remarks>
        /// <example>
        /// <code>
        /// var parser = new MarketingStatusParser();
        /// var result = await parser.ParseAsync(xmlElement, context, progress => Console.WriteLine(progress));
        /// Console.WriteLine($"Created {result.ProductElementsCreated} marketing status records");
        /// </code>
        /// </example>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate required context and element
            if (context == null || element == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse marketing status because context or element is null.");
                return result;
            }

            // Validate that either a product or packaging level context exists
            if (context.CurrentProduct?.ProductID == null && context.CurrentPackagingLevel?.PackagingLevelID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse marketing status because no product or packaging level context exists.");
                context.Logger?.LogError("MarketingStatusParser was called without a valid product or packaging level in the context.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting Marketing Status parsing for {context.FileNameInZip}");

                // Parse marketing status at current level and all nested levels
                var marketingStatusCount = await parseMarketingStatusRecursiveAsync(element, context);
                result.ProductElementsCreated += marketingStatusCount;

                reportProgress?.Invoke($"Completed Marketing Status parsing: {marketingStatusCount} records created");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing marketing status: {ex.Message}");
                context.Logger?.LogError(ex, "Error in MarketingStatusParser.ParseAsync");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to find an existing MarketingStatus record that matches the new marketing status criteria.
        /// </summary>
        /// <param name="newMarketingStatus">The new MarketingStatus entity containing criteria to match against.</param>
        /// <param name="repo">The repository for accessing MarketingStatus data.</param>
        /// <param name="context">The parsing context for logging and error handling.</param>
        /// <returns>The existing MarketingStatus record if found, otherwise null.</returns>
        /// <remarks>
        /// This method searches for existing MarketingStatus records that may have been created upstream
        /// without a PackagingLevelID. The search criteria include ProductID, MarketingActCode, StatusCode,
        /// and effective dates to ensure we're matching the same logical marketing status entry.
        /// 
        /// The method prioritizes finding records with null PackagingLevelID that match the current product,
        /// as these are most likely candidates for updating with packaging level context.
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Repository{T}"/>
        /// <seealso cref="Label"/>
        private async Task<MarketingStatus?> findExistingMarketingStatusAsync(
            MarketingStatus newMarketingStatus,
            Repository<MarketingStatus> repo,
            SplParseContext context)
        {
            #region implementation
            try
            {
                // Get all existing marketing status records for analysis
                var allMarketingStatuses = await repo.ReadAllAsync(null, null);

                if (allMarketingStatuses == null || !allMarketingStatuses.Any())
                {
                    return null;
                }

                // Look for existing records that match the new marketing status criteria
                // Priority: Records with matching ProductID and null PackagingLevelID
                var matchingRecord = allMarketingStatuses.FirstOrDefault(ms =>
                    ms.ProductID == newMarketingStatus.ProductID &&
                    ms.PackagingLevelID == null && // Looking for records created upstream without packaging context
                    ms.MarketingActCode == newMarketingStatus.MarketingActCode &&
                    ms.MarketingActCodeSystem == newMarketingStatus.MarketingActCodeSystem &&
                    ms.StatusCode == newMarketingStatus.StatusCode &&
                    ms.EffectiveStartDate == newMarketingStatus.EffectiveStartDate &&
                    ms.EffectiveEndDate == newMarketingStatus.EffectiveEndDate);

                if (matchingRecord != null)
                {
                    context.Logger?.LogDebug(
                        "Found existing MarketingStatus for potential update: ID={MarketingStatusID}, ProductID={ProductID}",
                        matchingRecord.MarketingStatusID, matchingRecord.ProductID);
                }

                return matchingRecord;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex,
                    "Error searching for existing MarketingStatus records for ProductID={ProductID}, ActCode={ActCode}",
                    newMarketingStatus.ProductID, newMarketingStatus.MarketingActCode);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates an existing MarketingStatus record with packaging level context if needed.
        /// </summary>
        /// <param name="existingMarketingStatus">The existing MarketingStatus record to potentially update.</param>
        /// <param name="newMarketingStatus">The new MarketingStatus entity containing updated context information.</param>
        /// <param name="repo">The repository for performing the update operation.</param>
        /// <param name="context">The parsing context for logging and error handling.</param>
        /// <returns>True if the record was updated, false if no update was needed.</returns>
        /// <remarks>
        /// This method updates existing MarketingStatus records that were created upstream without packaging
        /// level context. It specifically looks for cases where the existing record has a null PackagingLevelID
        /// and the new context provides a valid PackagingLevelID.
        /// 
        /// The method only updates the PackagingLevelID field to maintain data integrity and avoid overwriting
        /// other fields that may have been correctly set during initial creation.
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Repository{T}"/>
        /// <seealso cref="Label"/>
        private async Task<bool> updateExistingMarketingStatusAsync(
            MarketingStatus existingMarketingStatus,
            MarketingStatus newMarketingStatus,
            Repository<MarketingStatus> repo,
            SplParseContext context)
        {
            #region implementation
            try
            {
                // Only update if the existing record lacks PackagingLevelID and the new one provides it
                if (existingMarketingStatus.PackagingLevelID == null &&
                    newMarketingStatus.PackagingLevelID.HasValue)
                {
                    // Update the existing record with the packaging level context
                    existingMarketingStatus.PackagingLevelID = newMarketingStatus.PackagingLevelID;

                    await repo.UpdateAsync(existingMarketingStatus);

                    context.Logger?.LogDebug(
                        "Updated MarketingStatus with PackagingLevelID: ID={MarketingStatusID}, PackagingLevelID={PackagingLevelID}",
                        existingMarketingStatus.MarketingStatusID, newMarketingStatus.PackagingLevelID);

                    return true;
                }

                // No update needed
                context.Logger?.LogDebug(
                    "No update needed for MarketingStatus: ID={MarketingStatusID}, existing PackagingLevelID={ExistingPackagingLevelID}, new PackagingLevelID={NewPackagingLevelID}",
                    existingMarketingStatus.MarketingStatusID, existingMarketingStatus.PackagingLevelID, newMarketingStatus.PackagingLevelID);

                return false;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex,
                    "Error updating MarketingStatus: ID={MarketingStatusID}, PackagingLevelID={PackagingLevelID}",
                    existingMarketingStatus.MarketingStatusID, newMarketingStatus.PackagingLevelID);
                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively parses marketing status elements from the current element and all nested packaging levels.
        /// </summary>
        /// <param name="element">The XElement to process for marketing status information.</param>
        /// <param name="context">The parsing context containing current product and packaging level information.</param>
        /// <returns>The total count of MarketingStatus records created from this element and all nested elements.</returns>
        /// <remarks>
        /// This method implements a depth-first traversal of the packaging hierarchy to ensure all marketing
        /// status information is captured regardless of nesting level. It processes:
        /// 1. Direct marketing acts at the current element level
        /// 2. Marketing acts within nested asContent elements
        /// 3. Marketing acts within containerPackagedProduct elements
        /// 
        /// The recursive nature ensures that complex packaging hierarchies with multiple nesting levels
        /// are fully processed, which is essential for products with sophisticated packaging structures.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseMarketingStatusRecursiveAsync(XElement element, SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Process marketing acts at the current element level
            count += await processMarketingActsAtCurrentLevelAsync(element, context);

            // Recursively process all nested asContent elements
            foreach (var asContentEl in element.SplElements(sc.E.AsContent))
            {
                // Process marketing acts within asContent
                count += await parseMarketingStatusRecursiveAsync(asContentEl, context);

                // Process containerPackagedProduct elements within asContent
                foreach (var containerEl in asContentEl.SplElements(sc.E.ContainerPackagedProduct))
                {
                    count += await parseMarketingStatusRecursiveAsync(containerEl, context);
                }

                // Also check containerPackagedMedicine elements for completeness
                foreach (var medicineEl in asContentEl.SplElements(sc.E.ContainerPackagedMedicine))
                {
                    count += await parseMarketingStatusRecursiveAsync(medicineEl, context);
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes marketing acts found directly under the current XML element level using get/create/update pattern.
        /// </summary>
        /// <param name="element">The XElement to scan for direct subjectOf/marketingAct children.</param>
        /// <param name="context">The parsing context containing repository access and current entity information.</param>
        /// <returns>The count of MarketingStatus records created or updated at this specific level.</returns>
        /// <remarks>
        /// This method handles the actual extraction and validation of marketing act data from XML elements
        /// using a get/create/update pattern. It first attempts to find existing MarketingStatus records
        /// that may have been created upstream without PackagingLevelID, then either updates them with
        /// the current packaging context or creates new records.
        /// 
        /// The method validates activity codes against the FDA SPL code system and ensures status codes
        /// are within the permitted set. It determines whether to associate the marketing status with a
        /// Product or PackagingLevel based on the current parsing context.
        /// 
        /// Marketing acts must have:
        /// - Activity code from FDA SPL code system (2.16.840.1.113883.3.26.1.1)
        /// - Valid status code (active, completed, new, cancelled)
        /// - Optional effective time with low and high date boundaries
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="Label"/>
        private async Task<int> processMarketingActsAtCurrentLevelAsync(XElement element, SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<MarketingStatus>();

            // Validate required dependencies
            if (repo == null || context.Logger == null)
            {
                context?.Logger?.LogWarning("Cannot process marketing acts due to missing repository or logger.");
                return count;
            }

            // Process all subjectOf/marketingAct structures at this level
            foreach (var subjOfEl in element.SplElements(sc.E.SubjectOf))
            {
                foreach (var marketingActEl in subjOfEl.SplElements(sc.E.MarketingAct))
                {
                    var newMarketingStatus = extractMarketingStatusFromXml(marketingActEl, context);
                    if (newMarketingStatus != null)
                    {
                        // Attempt to find existing MarketingStatus record that may need updating
                        var existingMarketingStatus = await findExistingMarketingStatusAsync(newMarketingStatus, repo, context);

                        if (existingMarketingStatus != null)
                        {
                            // Update existing record with packaging level context if needed
                            var wasUpdated = await updateExistingMarketingStatusAsync(existingMarketingStatus, newMarketingStatus, repo, context);
                            if (wasUpdated)
                            {
                                count++;
                                context.Logger.LogInformation(
                                    "MarketingStatus updated: ID={MarketingStatusID}, PackagingLevelID={PackagingLevelID}, ActCode={ActCode}, Status={Status}",
                                    existingMarketingStatus.MarketingStatusID, newMarketingStatus.PackagingLevelID,
                                    newMarketingStatus.MarketingActCode, newMarketingStatus.StatusCode);
                            }
                        }
                        else
                        {
                            // Create new record
                            await repo.CreateAsync(newMarketingStatus);
                            count++;

                            context.Logger.LogInformation(
                                "MarketingStatus created: ID={MarketingStatusID}, ProductID={ProductID}, PackagingLevelID={PackagingLevelID}, ActCode={ActCode}, Status={Status}",
                                newMarketingStatus.MarketingStatusID, newMarketingStatus.ProductID, newMarketingStatus.PackagingLevelID,
                                newMarketingStatus.MarketingActCode, newMarketingStatus.StatusCode);
                        }
                    }
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and validates marketing status information from a marketingAct XML element.
        /// </summary>
        /// <param name="marketingActEl">The marketingAct XElement containing the marketing status data.</param>
        /// <param name="context">The parsing context used to determine product or packaging level association.</param>
        /// <returns>A new MarketingStatus entity if the data is valid, otherwise null.</returns>
        /// <remarks>
        /// This method performs comprehensive validation of marketing act data:
        /// - Validates activity code against FDA SPL code system (2.16.840.1.113883.3.26.1.1)
        /// - Ensures status code is one of the permitted values (active, completed, new, cancelled)
        /// - Parses effective time intervals with proper date handling using utility methods
        /// - Determines appropriate entity association based on current parsing context
        /// 
        /// The method returns null for invalid or incomplete data, which prevents creation of
        /// malformed database records while logging appropriate warnings.
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private MarketingStatus? extractMarketingStatusFromXml(XElement marketingActEl, SplParseContext context)
        {
            #region implementation
            // Extract activity code - must be from FDA SPL code system
            var codeEl = marketingActEl.SplElement(sc.E.Code);
            string? activityCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            string? activityCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

            // Validate activity code system - only accept FDA SPL codes
            if (activityCodeSystem != c.FDA_SPL_CODE_SYSTEM)
            {
                context?.Logger?.LogDebug("Skipping marketing act with invalid code system: {CodeSystem}", activityCodeSystem);
                return null;
            }

            // Extract and validate status code
            var statusCodeEl = marketingActEl.SplElement(sc.E.StatusCode);
            string? statusCode = statusCodeEl?.GetAttrVal(sc.A.CodeValue);

            // Only accept permitted status codes according to SPL standards
            if (!isValidStatusCode(statusCode))
            {
                context?.Logger?.LogDebug("Skipping marketing act with invalid status code: {StatusCode}", statusCode);
                return null;
            }

            // Extract effective time period with proper date parsing
            var effectiveDates = extractEffectiveTimePeriod(marketingActEl);

            // Determine entity association based on current parsing context
            var (productId, packagingLevelId) = determineEntityAssociation(context);

            // Create and return the marketing status entity
            var marketingStatus = new MarketingStatus
            {
                ProductID = productId,
                PackagingLevelID = packagingLevelId,
                MarketingActCode = activityCode,
                MarketingActCodeSystem = activityCodeSystem,
                StatusCode = statusCode,
                EffectiveStartDate = effectiveDates.StartDate,
                EffectiveEndDate = effectiveDates.EndDate
            };

            return marketingStatus;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates whether the provided status code is permitted according to SPL standards.
        /// </summary>
        /// <param name="statusCode">The status code to validate.</param>
        /// <returns>True if the status code is valid, false otherwise.</returns>
        /// <remarks>
        /// According to SPL Implementation Guide Section 3.1.8, marketing status codes are restricted
        /// to a specific set of values. This method performs case-insensitive validation against
        /// the permitted status codes: active, completed, new, cancelled.
        /// </remarks>
        /// <seealso cref="MarketingStatus"/>
        /// <seealso cref="Label"/>
        private bool isValidStatusCode(string? statusCode)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(statusCode))
            {
                return false;
            }

            // Validate against permitted status codes with case-insensitive comparison
            return statusCode.Equals("active", StringComparison.OrdinalIgnoreCase) ||
                   statusCode.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
                   statusCode.Equals("new", StringComparison.OrdinalIgnoreCase) ||
                   statusCode.Equals("cancelled", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts effective time period information from a marketingAct element.
        /// </summary>
        /// <param name="marketingActEl">The marketingAct XElement containing effectiveTime data.</param>
        /// <returns>A tuple containing the parsed start and end dates.</returns>
        /// <remarks>
        /// This method parses the effectiveTime element structure to extract marketing period boundaries.
        /// The structure follows SPL standards with low/high date elements. Date parsing uses utility
        /// methods to handle various date formats and null values appropriately.
        /// 
        /// The effective time structure in XML:
        /// &lt;effectiveTime&gt;
        ///   &lt;low value="YYYYMMDD"/&gt;
        ///   &lt;high value="YYYYMMDD"/&gt;
        /// &lt;/effectiveTime&gt;
        /// </remarks>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private (DateTime? StartDate, DateTime? EndDate) extractEffectiveTimePeriod(XElement marketingActEl)
        {
            #region implementation
            DateTime? startDate = null;
            DateTime? endDate = null;

            var effectiveTimeEl = marketingActEl.SplElement(sc.E.EffectiveTime);
            if (effectiveTimeEl != null)
            {
                // Parse start date from low element
                var lowEl = effectiveTimeEl.SplElement(sc.E.Low);
                if (lowEl != null)
                {
                    string? lowValue = lowEl.GetAttrVal(sc.A.Value);
                    if (!string.IsNullOrWhiteSpace(lowValue))
                    {
                        startDate = Util.ParseNullableDateTime(lowValue);
                    }
                }

                // Parse end date from high element
                var highEl = effectiveTimeEl.SplElement(sc.E.High);
                if (highEl != null)
                {
                    string? highValue = highEl.GetAttrVal(sc.A.Value);
                    if (!string.IsNullOrWhiteSpace(highValue))
                    {
                        endDate = Util.ParseNullableDateTime(highValue);
                    }
                }
            }

            return (startDate, endDate);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the appropriate entity association for the marketing status based on parsing context.
        /// </summary>
        /// <param name="context">The current parsing context containing product and packaging level information.</param>
        /// <returns>A tuple containing the ProductID and PackagingLevelID for entity association.</returns>
        /// <remarks>
        /// This method implements the business logic for determining whether a marketing status should be
        /// associated with a Product or PackagingLevel entity. The decision is based on the current
        /// parsing context:
        /// - If CurrentPackagingLevel is set, associate with packaging level
        /// - Otherwise, associate with the current product
        /// 
        /// This approach ensures that marketing status found within packaging structures is properly
        /// linked to the specific package level, while product-level marketing status is linked to the product.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="Label"/>
        private (int? ProductID, int? PackagingLevelID) determineEntityAssociation(SplParseContext context)
        {
            #region implementation
            // If we're currently processing a packaging level, associate with packaging
            if (context.CurrentPackagingLevel?.PackagingLevelID != null && context.CurrentProduct?.ProductID == null)
                return (null, context.CurrentPackagingLevel.PackagingLevelID);

            // If we are at the packing level hand have a product id then add that
            else if (context.CurrentPackagingLevel?.PackagingLevelID != null && context.CurrentProduct?.ProductID != null)
                return (context.CurrentProduct?.ProductID, context.CurrentPackagingLevel.PackagingLevelID);

            // Otherwise, associate with the current product
            return (context.CurrentProduct?.ProductID, null);
            #endregion
        }
    }
}