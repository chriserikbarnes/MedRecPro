using System.Xml.Linq;
using MedRecPro.Models;
using MedRecPro.Helpers;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using static MedRecPro.Models.Label;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Provides DRY methods for parsing and creating ProductEvent entities from SPL XML elements.
    /// Implements parsing logic for SPL Implementation Guide Section 16.2.9 (Containers Distributed) and 16.2.10 (Containers Returned).
    /// </summary>
    /// <remarks>
    /// This class contains reusable methods for extracting product event data from XML elements
    /// and creating validated ProductEvent entities. Designed to be called from ManufacturedProductParser
    /// and other parsing services to maintain consistency and reduce code duplication.
    /// ProductEvent entities represent distribution and return events for packaging levels.
    /// </remarks>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="PackagingLevel"/>
    /// <seealso cref="ProductEventValidationService"/>
    /// <seealso cref="Label"/>
    public static class ProductEventParser
    {
        #region implementation
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// FDA SPL code system for product event codes.
        /// </summary>
        private static readonly string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid product event codes for lot distribution reporting.
        /// </summary>
        private static readonly HashSet<string> ValidEventCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "C106325", // Distributed per reporting interval
            "C106328"  // Returned
        };
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses and creates ProductEvent entities from subjectOf/productEvent XML elements within a packaging level.
        /// Implements SPL Implementation Guide Section 16.2.9 and 16.2.10 requirements for product events.
        /// </summary>
        /// <param name="parentEl">The parent XML element (usually asContent/containerPackagedProduct) containing subjectOf elements.</param>
        /// <param name="packagingLevel">The PackagingLevel entity to associate with created product events.</param>
        /// <param name="context">The parsing context containing repository access and logging services.</param>
        /// <returns>The count of ProductEvent records successfully created and saved.</returns>
        /// <example>
        /// <code>
        /// var count = await ProductEventParser.BuildProductEventAsync(
        ///     containerEl, packagingLevel, context);
        /// Console.WriteLine($"Created {count} product events");
        /// </code>
        /// </example>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="Label"/>
        public static async Task<int> BuildProductEventAsync(
            XElement parentEl,
            PackagingLevel packagingLevel,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required dependencies
            if (parentEl == null || packagingLevel == null || context?.Logger == null || !packagingLevel.PackagingLevelID.HasValue)
            {
                context?.Logger?.LogWarning("Invalid parameters for product event parsing. Skipping.");
                return createdCount;
            }

            var repository = context.GetRepository<ProductEvent>();
            if (repository == null)
            {
                context.Logger.LogError("ProductEvent repository not available in context.");
                return createdCount;
            }

            context.Logger.LogDebug("Starting product event parsing for PackagingLevelID {PackagingLevelID}", packagingLevel.PackagingLevelID);

            try
            {
                // Find all subjectOf elements containing productEvent structures
                var subjectOfElements = parentEl.SplFindElements(sc.E.SubjectOf);

                foreach (var subjectOfEl in subjectOfElements)
                {
                    var productEventElements = subjectOfEl.SplElements(sc.E.ProductEvent);

                    foreach (var productEventEl in productEventElements)
                    {
                        // Parse product event from productEvent element
                        var productEvent = parseProductEventFromXml(
                            subjectOfEl, productEventEl, packagingLevel.PackagingLevelID.Value, context);

                        if (productEvent != null)
                        {
                            // Validate the product event before saving
                            if (validateProductEvent(productEvent, context))
                            {
                                // Use get-or-create pattern to avoid duplicates
                                var existingEvent = await getOrCreateProductEventAsync(productEvent, context);
                                if (existingEvent != null)
                                {
                                    createdCount++;
                                    context.Logger.LogInformation(
                                        "Created/Retrieved ProductEvent for PackagingLevelID {PackagingLevelID} with code {EventCode}",
                                        packagingLevel.PackagingLevelID, productEvent.EventCode);
                                }
                            }
                        }
                    }
                }

                context.Logger.LogInformation(
                    "Completed product event parsing for PackagingLevelID {PackagingLevelID}. Created {Count} events.",
                    packagingLevel.PackagingLevelID, createdCount);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex,
                    "Error parsing product events for PackagingLevelID {PackagingLevelID}", packagingLevel.PackagingLevelID);
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single ProductEvent entity from subjectOf and productEvent XML elements.
        /// Extracts event codes, quantities, and effective times according to SPL standards.
        /// </summary>
        /// <param name="subjectOfEl">The subjectOf XML element containing quantity information.</param>
        /// <param name="productEventEl">The productEvent XML element containing event code and timing.</param>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with the product event.</param>
        /// <param name="context">The parsing context for logging and error handling.</param>
        /// <returns>A ProductEvent entity with populated data, or null if parsing fails.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static ProductEvent? parseProductEventFromXml(
            XElement subjectOfEl,
            XElement productEventEl,
            int packagingLevelId,
            SplParseContext context)
        {
            #region implementation
            try
            {
                var productEvent = new ProductEvent
                {
                    PackagingLevelID = packagingLevelId
                };

                // Parse event code information (SPL IG 16.2.9.5-16.2.9.7)
                var eventInfo = parseEventCodeFromXml(productEventEl, context);
                productEvent.EventCode = eventInfo.EventCode;
                productEvent.EventCodeSystem = eventInfo.EventCodeSystem;
                productEvent.EventDisplayName = eventInfo.EventDisplayName;

                // Parse quantity information (SPL IG 16.2.9.2-16.2.9.4)
                var quantityInfo = parseQuantityFromXml(subjectOfEl, context);
                productEvent.QuantityValue = quantityInfo.QuantityValue;
                productEvent.QuantityUnit = quantityInfo.QuantityUnit;

                // Parse effective time information (SPL IG 16.2.9.9-16.2.9.11)
                var timeInfo = parseEffectiveTimeFromXml(productEventEl, context);
                productEvent.EffectiveTimeLow = timeInfo;

                return productEvent;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    "Error parsing ProductEvent from XML for PackagingLevelID {PackagingLevelID}", packagingLevelId);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts event code information from a productEvent XML element.
        /// Handles code, codeSystem, and displayName attributes according to SPL standards.
        /// </summary>
        /// <param name="productEventEl">The productEvent XML element containing event code information.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A tuple containing event code data elements.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        private static (string? EventCode, string? EventCodeSystem, string? EventDisplayName)
            parseEventCodeFromXml(XElement productEventEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Find the code element within productEvent
                var codeEl = productEventEl.GetSplElement(sc.E.Code);

                if (codeEl == null)
                {
                    context?.Logger?.LogWarning("No code element found in productEvent");
                    return (null, null, null);
                }

                // Extract event code attributes
                var eventCode = codeEl.GetAttrVal(sc.A.CodeValue)?.Trim();
                var eventCodeSystem = codeEl.GetAttrVal(sc.A.CodeSystem)?.Trim();
                var eventDisplayName = codeEl.GetAttrVal(sc.A.DisplayName)?.Trim();

                return (eventCode, eventCodeSystem, eventDisplayName);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error parsing event code from XML");
                return (null, null, null);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts quantity information from a subjectOf XML element.
        /// Handles quantity value and unit attributes according to SPL standards.
        /// </summary>
        /// <param name="subjectOfEl">The subjectOf XML element containing quantity information.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A tuple containing quantity value and unit.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private static (int? QuantityValue, string? QuantityUnit)
            parseQuantityFromXml(XElement subjectOfEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Find the quantity element within subjectOf
                var quantityEl = subjectOfEl.GetSplElement(sc.E.Quantity);

                if (quantityEl == null)
                {
                    context?.Logger?.LogWarning("No quantity element found in subjectOf");
                    return (null, null);
                }

                // Extract quantity value and unit
                var valueStr = quantityEl.GetAttrVal(sc.A.Value)?.Trim();
                var unit = quantityEl.GetAttrVal(sc.A.Unit)?.Trim();

                // Parse the numeric value using utility method
                int? quantityValue = null;
                if (!string.IsNullOrWhiteSpace(valueStr))
                {
                    quantityValue = Util.ParseNullableInt(valueStr);

                    if (quantityValue == null)
                    {
                        context?.Logger?.LogWarning(
                            "Could not parse quantity value '{ValueStr}' as integer", valueStr);
                    }
                }

                return (quantityValue, unit);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error parsing quantity from XML");
                return (null, null);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts effective time information from a productEvent XML element.
        /// Handles effectiveTime/low elements for distribution date tracking.
        /// </summary>
        /// <param name="productEventEl">The productEvent XML element containing effective time information.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A tuple containing effective time low boundary.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private static DateTime? parseEffectiveTimeFromXml(XElement productEventEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Find the effectiveTime element within productEvent
                var effectiveTimeEl = productEventEl.GetSplElement(sc.E.EffectiveTime);

                if (effectiveTimeEl == null)
                {
                    // Some events (like returned) may not have effective time
                    context?.Logger?.LogDebug("No effectiveTime element found in productEvent");
                    return (null);
                }

                // Extract low boundary value
                var lowEl = effectiveTimeEl.GetSplElement(sc.E.Low);
                var lowValueStr = lowEl?.GetAttrVal(sc.A.Value)?.Trim();

                // Parse the date value using utility method
                DateTime? effectiveTimeLow = null;
                if (!string.IsNullOrWhiteSpace(lowValueStr))
                {
                    effectiveTimeLow = Util.ParseNullableDateTime(lowValueStr);

                    if (effectiveTimeLow == null)
                    {
                        context?.Logger?.LogWarning(
                            "Could not parse effective time low '{ValueStr}' as DateTime", lowValueStr);
                    }
                }

                return (effectiveTimeLow);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error parsing effective time from XML");
                return (null);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductEvent from the database or creates a new one if not found.
        /// Implements get-or-create pattern to prevent duplicate product event records.
        /// </summary>
        /// <param name="productEvent">The ProductEvent entity to find or create.</param>
        /// <param name="context">The parsing context containing database access services.</param>
        /// <returns>The existing or newly created ProductEvent entity.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<ProductEvent?> getOrCreateProductEventAsync(
            ProductEvent productEvent,
            SplParseContext context)
        {
            #region implementation
            if (productEvent == null || context?.ServiceProvider == null || context?.Logger == null)
            {
                return null;
            }

            // Get database context for direct queries
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productEventDbSet = dbContext.Set<ProductEvent>();

            // Search for existing product event with same packaging level and event code
            var existingEvent = await productEventDbSet.FirstOrDefaultAsync(pe =>
                pe.PackagingLevelID == productEvent.PackagingLevelID &&
                pe.EventCode == productEvent.EventCode);

            if (existingEvent != null)
            {
                context.Logger.LogDebug(
                    "Found existing ProductEvent for PackagingLevelID {PackagingLevelID} with code {EventCode}",
                    productEvent.PackagingLevelID, productEvent.EventCode);
                return existingEvent;
            }

            // Create new product event if not found
            context.Logger.LogInformation(
                "Creating new ProductEvent for PackagingLevelID {PackagingLevelID} with code {EventCode}",
                productEvent.PackagingLevelID, productEvent.EventCode);

            var repository = context.GetRepository<ProductEvent>();
            await repository.CreateAsync(productEvent);

            return productEvent;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a ProductEvent entity using the validation service before saving to database.
        /// Ensures compliance with SPL Implementation Guide Section 16.2.9 and 16.2.10 requirements.
        /// </summary>
        /// <param name="productEvent">The ProductEvent entity to validate.</param>
        /// <param name="context">The parsing context containing logging services.</param>
        /// <returns>True if validation passes, false if validation fails.</returns>
        /// <seealso cref="ProductEventValidationService"/>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        private static bool validateProductEvent(ProductEvent productEvent, SplParseContext context)
        {
            #region implementation
            try
            {
                if (context == null || context.Logger == null)
                {
                    return false;
                }

                if (productEvent == null)
                {
                    context.Logger.LogWarning("ProductEvent cannot be null for validation");
                    return false;
                }

                // Create validation service instance
                var validationService = new ProductEventValidationService(context.Logger);

                // Perform comprehensive validation
                var validationResult = validationService.ValidateProductEvent(productEvent);

                if (!validationResult.IsValid)
                {
                    // Log all validation errors
                    foreach (var error in validationResult.Errors)
                    {
                        context.Logger.LogWarning(
                            "ProductEvent validation error for PackagingLevelID {PackagingLevelID}: {Error}",
                            productEvent.PackagingLevelID, error);
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    "Error during ProductEvent validation for PackagingLevelID {PackagingLevelID}",
                    productEvent.PackagingLevelID);
                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a ProductEvent entity with default values for testing or fallback scenarios.
        /// Provides a standardized way to create valid product event entities.
        /// </summary>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with the product event.</param>
        /// <param name="eventCode">Optional event code (defaults to "C106325" for distributed).</param>
        /// <param name="quantityValue">Optional quantity value (defaults to 1).</param>
        /// <param name="effectiveTimeLow">Optional effective time low boundary.</param>
        /// <returns>A ProductEvent entity with specified or default values.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public static ProductEvent CreateDefault(
            int packagingLevelId,
            string? eventCode = "C106325",
            int? quantityValue = 1,
            DateTime? effectiveTimeLow = null)
        {
            #region implementation
            return new ProductEvent
            {
                PackagingLevelID = packagingLevelId,
                EventCode = eventCode,
                EventCodeSystem = FdaSplCodeSystem,
                EventDisplayName = eventCode == "C106325" ? "Distributed per reporting interval" : 
                                  eventCode == "C106328" ? "Returned" : null,
                QuantityValue = quantityValue,
                QuantityUnit = "1",
                EffectiveTimeLow = effectiveTimeLow
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Populates a ProductEvent entity from parsed XML data with validation.
        /// Provides a DRY method for creating valid ProductEvent entities.
        /// </summary>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with the product event.</param>
        /// <param name="eventCode">The event code for the product event.</param>
        /// <param name="eventCodeSystem">The code system for the event code.</param>
        /// <param name="eventDisplayName">The display name for the event code.</param>
        /// <param name="quantityValue">The quantity value for the event.</param>
        /// <param name="quantityUnit">The quantity unit for the event.</param>
        /// <param name="effectiveTimeLow">The effective time low boundary.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A validated ProductEvent entity or null if validation fails.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="ProductEventValidationService"/>
        /// <seealso cref="Label"/>
        public static ProductEvent? PopulateProductEvent(
            int packagingLevelId,
            string? eventCode,
            string? eventCodeSystem,
            string? eventDisplayName,
            int? quantityValue,
            string? quantityUnit,
            DateTime? effectiveTimeLow,
            SplParseContext context)
        {
            #region implementation
            try
            {
                // Create and populate the ProductEvent entity
                var productEvent = new ProductEvent
                {
                    PackagingLevelID = packagingLevelId,
                    EventCode = eventCode,
                    EventCodeSystem = eventCodeSystem,
                    EventDisplayName = eventDisplayName,
                    QuantityValue = quantityValue,
                    QuantityUnit = quantityUnit,
                    EffectiveTimeLow = effectiveTimeLow
                };

                // Validate the product event before returning
                if (context?.Logger != null && !validateProductEvent(productEvent, context))
                {
                    return null;
                }

                return productEvent;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error populating ProductEvent for PackagingLevelID {PackagingLevelID}", packagingLevelId);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a ProductEvent entity for a distributed event with validation.
        /// Provides a DRY method for creating distributed product events.
        /// </summary>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with the product event.</param>
        /// <param name="quantityValue">The quantity value for the distributed event.</param>
        /// <param name="effectiveTimeLow">The initial distribution date.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A validated ProductEvent entity for distribution or null if validation fails.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public static ProductEvent? CreateDistributedEvent(
            int packagingLevelId,
            int quantityValue,
            DateTime effectiveTimeLow,
            SplParseContext context)
        {
            #region implementation
            return PopulateProductEvent(
                packagingLevelId,
                "C106325",
                FdaSplCodeSystem,
                "Distributed per reporting interval",
                quantityValue,
                "1",
                effectiveTimeLow,
                context);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a ProductEvent entity for a returned event with validation.
        /// Provides a DRY method for creating returned product events.
        /// </summary>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with the product event.</param>
        /// <param name="quantityValue">The quantity value for the returned event.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A validated ProductEvent entity for return or null if validation fails.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public static ProductEvent? CreateReturnedEvent(
            int packagingLevelId,
            int quantityValue,
            SplParseContext context)
        {
            #region implementation
            return PopulateProductEvent(
                packagingLevelId,
                "C106328",
                FdaSplCodeSystem,
                "Returned",
                quantityValue,
                "1",
                null, // Returned events have no effective time per SPL IG 16.2.10.2
                context);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bulk creates ProductEvent entities from XML data with validation and deduplication.
        /// Provides a DRY method for processing multiple product events efficiently.
        /// </summary>
        /// <param name="parentEl">The parent XML element containing product event data.</param>
        /// <param name="packagingLevelId">The PackagingLevelID to associate with product events.</param>
        /// <param name="context">The parsing context containing repository access and logging services.</param>
        /// <returns>The count of ProductEvent records successfully created and saved.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public static async Task<int> BulkCreateProductEventsAsync(
            XElement parentEl,
            int packagingLevelId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            if (parentEl == null || context?.Logger == null)
            {
                return createdCount;
            }

            try
            {
                var productEvents = new List<ProductEvent>();

                // Parse all product events from XML
                var subjectOfElements = parentEl.SplFindElements(sc.E.SubjectOf);
                foreach (var subjectOfEl in subjectOfElements)
                {
                    var productEventElements = subjectOfEl.SplElements(sc.E.ProductEvent);
                    foreach (var productEventEl in productEventElements)
                    {
                        var productEvent = parseProductEventFromXml(
                            subjectOfEl, productEventEl, packagingLevelId, context);

                        if (productEvent != null)
                        {
                            productEvents.Add(productEvent);
                        }
                    }
                }

                // Bulk create with deduplication
                foreach (var productEvent in productEvents)
                {
                    var existingEvent = await getOrCreateProductEventAsync(productEvent, context);
                    if (existingEvent != null)
                    {
                        createdCount++;
                    }
                }

                context.Logger.LogInformation(
                    "Bulk created {Count} ProductEvent records for PackagingLevelID {PackagingLevelID}",
                    createdCount, packagingLevelId);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex,
                    "Error bulk creating ProductEvent records for PackagingLevelID {PackagingLevelID}", packagingLevelId);
            }

            return createdCount;
            #endregion
        }
    }
}