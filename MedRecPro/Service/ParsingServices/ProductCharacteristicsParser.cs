using System.Xml.Linq;
#pragma warning disable CS8981
using sc = MedRecPro.Models.SplConstants;
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981

using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.DataAccess;
using System;
using System.Threading.Tasks;
using static MedRecPro.Models.Label;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses product characteristics and attributes, including physical properties, additional identifiers
    /// (like model/catalog numbers), and routes of administration. Supports both product-level and 
    /// package-level characteristic parsing.
    /// </summary>
    /// <remarks>
    /// This parser is responsible for detailing the specific attributes of a product and its packaging.
    /// It is called by a parent parser and requires that the `SplParseContext.CurrentProduct` has been established.
    /// For package-level characteristics, `SplParseContext.CurrentPackagingLevel` should also be set.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="Characteristic"/>
    /// <seealso cref="AdditionalIdentifier"/>
    /// <seealso cref="ProductRouteOfAdministration"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class ProductCharacteristicsParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "productcharacteristics";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an XML element to extract and save product characteristics, identifiers, and administration routes.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of product attributes by delegating to specialized
        /// private methods. It assumes `context.CurrentProduct` is set by the calling parser.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that a product is available in the context to link entities to.
            if (context?.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse product characteristics because no product context exists.");
                context?.Logger?.LogError("ProductCharacteristicsParser was called without a valid product in the context.");
                return result;
            }
            var product = context.CurrentProduct;

            // --- PARSE CHARACTERISTIC ---
            reportProgress?.Invoke($"Starting Characteristic XML Elements {context.FileNameInZip}");
            var characteristicCt = await parseAndSaveCharacteristicsAsync(element, product, context);
            result.ProductElementsCreated += characteristicCt;

            // --- PARSE ADDITIONAL IDENTIFIER ---
            reportProgress?.Invoke($"Starting Additional Identifier XML Elements {context.FileNameInZip}");
            var identifiersCt = await parseAndSaveAdditionalIdentifiersAsync(element, product, context);
            result.ProductElementsCreated += identifiersCt;

            // --- PARSE ROUTE OF ADMIN ---
            reportProgress?.Invoke($"Starting Product Route Of Administration XML Elements {context.FileNameInZip}");
            var routeCt = await parseAndSaveProductRoutesOfAdministrationAsync(element, product, context);
            result.ProductElementsCreated += routeCt;

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all Characteristic entities under [subjectOf][characteristic] nodes for a given product.
        /// Recursively processes nested [asContent] structures to capture characteristics at all packaging levels.
        /// Uses getOrCreate pattern to avoid duplicate characteristic records.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [partProduct]) to scan for characteristics.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of Characteristic records created or retrieved.</returns>
        /// <remarks>
        /// Handles characteristic value types PQ, INT, IVL_PQ, CV, ST, ED, and BL according to SPL IG.
        /// Supports complex value types including intervals, coded values, and multimedia references.
        /// Each characteristic includes both the code identifying the characteristic type and the
        /// appropriately typed value based on the xsi:type attribute.
        /// Now recursively processes asContent/containerPackagedProduct structures to capture 
        /// characteristics nested within packaging hierarchies WITH proper PackagingLevelID association.
        /// Uses FirstOrDefaultAsync to check for existing characteristics before creating new ones.
        /// </remarks>
        /// <example>
        /// // Parse characteristics from a manufactured product
        /// var count = await parseAndSaveCharacteristicsAsync(productElement, product, context);
        /// </example>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveCharacteristicsAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<Characteristic>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Determine the packaging level ID from context if available
            // This allows the method to work for both product-level and package-level characteristics
            int? packagingLevelId = context.CurrentPackagingLevel?.PackagingLevelID;

            // Parse characteristics directly under the parent element
            // If context.CurrentPackagingLevel is set, these will be package-level characteristics
            // Otherwise, they will be product-level characteristics (PackagingLevelID = null)
            count += await parseCharacteristicsFromSubjectOfAsync(parentEl, product, context, packagingLevelId);

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses characteristics directly from subjectOf/characteristic structures under the given element.
        /// Uses getOrCreate pattern with FirstOrDefaultAsync to avoid creating duplicate characteristic records.
        /// </summary>
        /// <param name="element">The XElement to search for direct subjectOf/characteristic structures.</param>
        /// <param name="product">The Product entity associated with these characteristics.</param>
        /// <param name="context">The parsing context containing repository and logging services.</param>
        /// <param name="packagingLevelId">Optional PackagingLevelID for package-level characteristics.</param>
        /// <returns>The count of Characteristic records created or retrieved from direct structures.</returns>
        /// <remarks>
        /// This method handles the original parsing logic for characteristics that appear
        /// directly under the parent element. When packagingLevelId is provided, creates
        /// package-level characteristics; otherwise creates product-level characteristics.
        /// Implements getOrCreate pattern by first querying existing characteristics using FirstOrDefaultAsync
        /// before creating new records to prevent duplicates.
        /// </remarks>
        /// <example>
        /// // Parse product-level characteristics
        /// var count = await parseCharacteristicsFromSubjectOfAsync(productElement, product, context, null);
        /// // Parse package-level characteristics
        /// var count = await parseCharacteristicsFromSubjectOfAsync(asContentElement, product, context, packagingLevelId);
        /// </example>
        /// <seealso cref="parseAndSaveCharacteristicsAsync"/>
        /// <seealso cref="parseCharacteristicsFromAsContentAsync"/>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseCharacteristicsFromSubjectOfAsync(
            XElement element,
            Product product,
            SplParseContext context,
            int? packagingLevelId)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<Characteristic>();

            if (context == null || context.ServiceProvider == null || repo == null || context.Logger == null)
                return count;

            // Get DbContext for getOrCreate operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var characteristicDbSet = dbContext.Set<Characteristic>();

            // Get all characteristics for parsing at once to minimize database calls
            var characteristicsToProcess = new List<Characteristic>();

            // First pass: build all characteristics from XML
            foreach (var subjOf in element.SplElements(sc.E.SubjectOf))
            {
                foreach (var charEl in subjOf.SplElements(sc.E.Characteristic))
                {
                    var characteristic = buildCharacteristicFromElement(charEl, product, context!, packagingLevelId);
                    if (characteristic != null)
                    {
                        characteristicsToProcess.Add(characteristic);
                    }
                }
            }

            // Group characteristics by ProductID and PackagingLevelID for efficient database queries
            var characteristicGroups = characteristicsToProcess
                .GroupBy(c => new { ProductID = product.ProductID, c.PackagingLevelID })
                .ToList();

            foreach (var group in characteristicGroups)
            {
                #region getOrCreate logic

                // Get existing characteristics from database for this ProductID/PackagingLevelID combination
                var existingCharacteristics = await characteristicDbSet
                    .Where(c => c.ProductID == group.Key.ProductID && c.PackagingLevelID == group.Key.PackagingLevelID)
                    .ToListAsync();

                // Create HashSet with all characteristic properties for fast uniqueness checking
                var existingCharacteristicKeys = existingCharacteristics
                    .Select(c => new CharacteristicKey
                    {
                        CharacteristicCode = c.CharacteristicCode ?? string.Empty,
                        ValueType = c.ValueType ?? string.Empty,
                        ValueCV_Code = c.ValueCV_Code ?? string.Empty,
                        ValueST = c.ValueST ?? string.Empty,
                        ValuePQ_Value = c.ValuePQ_Value,
                        ValuePQ_Unit = c.ValuePQ_Unit ?? string.Empty,
                        ValueINT = c.ValueINT,
                        ValueBL = c.ValueBL,
                        ValueED_MediaType = c.ValueED_MediaType ?? string.Empty,
                        ValueED_CDATAContent = c.ValueED_CDATAContent ?? string.Empty,
                        ValueNullFlavor = c.ValueNullFlavor ?? string.Empty,
                        OriginalText = c.OriginalText ?? string.Empty
                    })
                    .ToHashSet();

                // Process each characteristic in this group
                foreach (var characteristic in group)
                {
                    // Create key for current characteristic using all properties
                    var currentKey = new CharacteristicKey
                    {
                        CharacteristicCode = characteristic.CharacteristicCode ?? string.Empty,
                        ValueType = characteristic.ValueType ?? string.Empty,
                        ValueCV_Code = characteristic.ValueCV_Code ?? string.Empty,
                        ValueST = characteristic.ValueST ?? string.Empty,
                        ValuePQ_Value = characteristic.ValuePQ_Value,
                        ValuePQ_Unit = characteristic.ValuePQ_Unit ?? string.Empty,
                        ValueINT = characteristic.ValueINT,
                        ValueBL = characteristic.ValueBL,
                        ValueED_MediaType = characteristic.ValueED_MediaType ?? string.Empty,
                        ValueED_CDATAContent = characteristic.ValueED_CDATAContent ?? string.Empty,
                        ValueNullFlavor = characteristic.ValueNullFlavor ?? string.Empty,
                        OriginalText = characteristic.OriginalText ?? string.Empty
                    };

                    // Check if characteristic already exists using HashSet
                    if (!existingCharacteristicKeys.Contains(currentKey))
                    {
                        // Create new characteristic if it doesn't exist
                        await repo.CreateAsync(characteristic);
                        count++;

                        // Add to HashSet to prevent duplicates within this parsing session
                        existingCharacteristicKeys.Add(currentKey);

                        var levelType = packagingLevelId.HasValue ? "package-level" : "product-level";
                        context?.Logger?.LogInformation($"New {levelType} characteristic created: ProductID={product.ProductID}, PackagingLevelID={characteristic.PackagingLevelID}, Code={characteristic.CharacteristicCode}, ValueType={characteristic.ValueType}");
                    }
                    else
                    {
                        var levelType = packagingLevelId.HasValue ? "package-level" : "product-level";
                        context?.Logger?.LogInformation($"Duplicate {levelType} characteristic skipped: ProductID={product.ProductID}, PackagingLevelID={characteristic.PackagingLevelID}, Code={characteristic.CharacteristicCode}, ValueType={characteristic.ValueType}");
                    }
                }

                #endregion
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents a complete characteristic signature for uniqueness checking.
        /// Includes all characteristic properties to ensure comprehensive duplicate detection.
        /// </summary>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="Label"/>
        private class CharacteristicKey : IEquatable<CharacteristicKey>
        {
            #region properties
            public string CharacteristicCode { get; set; } = string.Empty;
            public string ValueType { get; set; } = string.Empty;
            public string ValueCV_Code { get; set; } = string.Empty;
            public string ValueST { get; set; } = string.Empty;
            public decimal? ValuePQ_Value { get; set; }
            public string ValuePQ_Unit { get; set; } = string.Empty;
            public int? ValueINT { get; set; }
            public bool? ValueBL { get; set; }
            public string ValueED_MediaType { get; set; } = string.Empty;
            public string ValueED_CDATAContent { get; set; } = string.Empty;
            public string ValueNullFlavor { get; set; } = string.Empty;
            public string OriginalText { get; set; } = string.Empty;
            #endregion

            #region equality implementation
            public bool Equals(CharacteristicKey? other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;

                return CharacteristicCode == other.CharacteristicCode &&
                       ValueType == other.ValueType &&
                       ValueCV_Code == other.ValueCV_Code &&
                       ValueST == other.ValueST &&
                       ValuePQ_Value == other.ValuePQ_Value &&
                       ValuePQ_Unit == other.ValuePQ_Unit &&
                       ValueINT == other.ValueINT &&
                       ValueBL == other.ValueBL &&
                       ValueED_MediaType == other.ValueED_MediaType &&
                       ValueED_CDATAContent == other.ValueED_CDATAContent &&
                       ValueNullFlavor == other.ValueNullFlavor &&
                       OriginalText == other.OriginalText;
            }

            public override bool Equals(object? obj) => Equals(obj as CharacteristicKey);

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(CharacteristicCode);
                hash.Add(ValueType);
                hash.Add(ValueCV_Code);
                hash.Add(ValueST);
                hash.Add(ValuePQ_Value);
                hash.Add(ValuePQ_Unit);
                hash.Add(ValueINT);
                hash.Add(ValueBL);
                hash.Add(ValueED_MediaType);
                hash.Add(ValueED_CDATAContent);
                hash.Add(ValueNullFlavor);
                hash.Add(OriginalText);
                return hash.ToHashCode();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively parses characteristics from nested asContent/containerPackagedProduct structures.
        /// CRITICAL: This method must be called AFTER PackagingParser has created PackagingLevel entities,
        /// because it needs to look up PackagingLevelID values to associate with characteristics.
        /// </summary>
        /// <param name="element">The XElement to search for asContent structures.</param>
        /// <param name="product">The Product entity associated with these characteristics.</param>
        /// <param name="context">The parsing context containing repository and logging services.</param>
        /// <returns>The count of Characteristic records created from nested asContent structures.</returns>
        /// <remarks>
        /// This method addresses the missing functionality where characteristics nested within
        /// packaging hierarchies (asContent/containerPackagedProduct/asContent/subjectOf/characteristic)
        /// were not being captured with proper PackagingLevelID associations.
        /// 
        /// The method works by:
        /// 1. Finding containerPackagedProduct elements
        /// 2. Extracting the package NDC code to identify the PackagingLevel
        /// 3. Looking up the corresponding PackagingLevelID from the database
        /// 4. Parsing characteristics with that PackagingLevelID
        /// 5. Recursively processing nested asContent structures
        /// </remarks>
        /// <seealso cref="parseAndSaveCharacteristicsAsync"/>
        /// <seealso cref="parseCharacteristicsFromSubjectOfAsync"/>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseCharacteristicsFromAsContentAsync(
            XElement element,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            if (context == null || context.ServiceProvider == null)
                return count;

            // Get repository for PackagingLevel lookups
            var packagingRepo = context.GetRepository<PackagingLevel>();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var packagingDbSet = dbContext.Set<PackagingLevel>();

            // Process all asContent elements
            foreach (var asContentEl in element.SplElements(sc.E.AsContent))
            {
                // Find the containerPackagedProduct within this asContent
                var containerEl = asContentEl.GetSplElement(sc.E.ContainerPackagedProduct);

                if (containerEl != null)
                {
                    // Extract the package code (NDC) to identify the PackagingLevel
                    var codeEl = containerEl.GetSplElement(sc.E.Code);
                    var packageCode = codeEl?.GetAttrVal(sc.A.CodeValue);

                    int? packagingLevelId = null;

                    // If we have a package code, look up the PackagingLevel
                    if (!string.IsNullOrWhiteSpace(packageCode))
                    {
                        // Query PackagingLevel by ProductID and PackageCode
                        var packagingLevel = await packagingDbSet.FirstOrDefaultAsync(
                            pl => pl.ProductID == product.ProductID && pl.PackageCode == packageCode);

                        if (packagingLevel != null)
                        {
                            packagingLevelId = packagingLevel.PackagingLevelID;
                            context.Logger?.LogInformation($"Found PackagingLevel {packagingLevelId} for package code {packageCode}");
                        }
                        else
                        {
                            context.Logger?.LogWarning($"Could not find PackagingLevel for ProductID={product.ProductID}, PackageCode={packageCode}");
                        }
                    }

                    // Parse characteristics at this asContent level (these are characteristics of the container)
                    // These should be associated with the packaging level we just identified
                    count += await parseCharacteristicsFromSubjectOfAsync(asContentEl, product, context, packagingLevelId);

                    // Also check within the containerPackagedProduct for characteristics
                    count += await parseCharacteristicsFromSubjectOfAsync(containerEl, product, context, packagingLevelId);

                    // Recursively process any nested asContent structures within the container
                    count += await parseCharacteristicsFromAsContentAsync(containerEl, product, context);
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a Characteristic entity from an XML characteristic element.
        /// </summary>
        /// <param name="charEl">The characteristic XElement containing the characteristic data.</param>
        /// <param name="product">The Product entity this characteristic belongs to.</param>
        /// <param name="context">The parsing context for logging and validation.</param>
        /// <param name="packagingLevelId">Optional PackagingLevelID for package-level characteristics.</param>
        /// <returns>A populated Characteristic entity, or null if the element is invalid.</returns>
        /// <remarks>
        /// This method extracts the characteristic code, value type, and all possible value fields
        /// based on the xsi:type attribute. Supports PQ, INT, IVL_PQ, CV, ST, ED, and BL value types.
        /// Centralizes the characteristic building logic to maintain consistency across parsing methods.
        /// When packagingLevelId is provided, creates package-level characteristics; otherwise creates
        /// product-level characteristics.
        /// </remarks>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="parseCharacteristicsFromSubjectOfAsync"/>
        /// <seealso cref="Label"/>
        private Characteristic? buildCharacteristicFromElement(
            XElement charEl,
            Product product,
            SplParseContext context,
            int? packagingLevelId)
        {
            #region implementation
            if (charEl == null)
                return null;

            // Parse Characteristic code & codeSystem
            var codeEl = charEl.GetSplElement(sc.E.Code);
            string? charCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            string? charCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            string? originalText = charEl
                ?.GetSplElement(sc.E.Value)
                ?.GetSplElement(sc.E.OriginalText)
                ?.Value;

            // Parse <value> node and its type
            var valueEl = charEl.GetSplElement(sc.E.Value);
            string? valueType = valueEl?.GetXsiType();

            // Initialize all possible value fields to null
            decimal? valuePQ_Value = null;
            string? valuePQ_Unit = null;
            int? valueINT = null;
            string? valueNullFlavor = null;
            string? valueCV_Code = null;
            string? valueCV_CodeSystem = null;
            string? valueCV_DisplayName = null;
            string? valueST = null;
            bool? valueBL = null;
            decimal? valueIVLPQ_LowValue = null;
            string? valueIVLPQ_LowUnit = null;
            decimal? valueIVLPQ_HighValue = null;
            string? valueIVLPQ_HighUnit = null;
            string? valueED_MediaType = null;
            string? valueED_FileName = null;

            // Parse based on xsi:type to populate appropriate value fields
            if (!string.IsNullOrWhiteSpace(valueType))
            {
                switch (valueType.ToUpperInvariant())
                {
                    case "PQ":
                    case "REAL":
                        if (valueEl != null)
                        {
                            var valueAttr = valueEl.GetAttrVal(sc.A.Value);
                            valuePQ_Value = valueAttr != null ? Util.ParseNullableDecimal(valueAttr) : null;
                            valuePQ_Unit = valueEl.GetAttrVal(sc.A.Unit);
                        }
                        break;

                    case "INT":
                        if (valueEl != null)
                        {
                            valueNullFlavor = valueEl.GetAttrVal(sc.A.NullFlavor);
                            var intAttr = valueEl.GetAttrVal(sc.A.Value);
                            valueINT = intAttr != null ? Util.ParseNullableInt(intAttr) : null;
                        }
                        break;

                    case "CV":
                    case "CE":
                        valueCV_Code = valueEl?.GetAttrVal(sc.A.CodeValue);
                        valueCV_CodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem);
                        valueCV_DisplayName = valueEl?.GetAttrVal(sc.A.DisplayName);
                        break;

                    case "ST":
                        valueCV_Code = valueEl?.GetAttrVal(sc.A.CodeValue);
                        valueCV_CodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem);
                        valueST = valueEl?.Value;
                        break;

                    case "IVL_PQ":
                        var lowEl = valueEl?.GetSplElement(sc.E.Low);
                        if (lowEl != null)
                        {
                            var lowValueAttr = lowEl.GetAttrVal(sc.A.Value);
                            valueIVLPQ_LowValue = lowValueAttr != null ? Util.ParseNullableDecimal(lowValueAttr) : null;
                            valueIVLPQ_LowUnit = lowEl.GetAttrVal(sc.A.Unit);
                        }

                        var highEl = valueEl?.GetSplElement(sc.E.High);
                        if (highEl != null)
                        {
                            var highValueAttr = highEl.GetAttrVal(sc.A.Value);
                            valueIVLPQ_HighValue = highValueAttr != null ? Util.ParseNullableDecimal(highValueAttr) : null;
                            valueIVLPQ_HighUnit = highEl.GetAttrVal(sc.A.Unit);
                        }
                        break;

                    case "ED":
                        valueED_MediaType = valueEl?.GetAttrVal(sc.A.MediaType);
                        valueED_FileName = valueEl?.GetAttrVal(sc.A.DisplayName);
                        break;

                    case "BL":
                        if (valueEl != null)
                        {
                            var boolAttr = valueEl.GetAttrVal(sc.A.Value);
                            valueBL = boolAttr != null ? Util.ParseNullableBoolWithStringValue(boolAttr) : null;
                        }
                        break;
                }
            }

            // Build and return the Characteristic entity WITH PackagingLevelID when provided
            return new Characteristic
            {
                ProductID = product.ProductID,
                PackagingLevelID = packagingLevelId,
                CharacteristicCode = charCode,
                CharacteristicCodeSystem = charCodeSystem,
                OriginalText = originalText,
                ValueType = valueType,
                ValuePQ_Value = valuePQ_Value,
                ValuePQ_Unit = valuePQ_Unit,
                ValueINT = valueINT,
                ValueCV_Code = valueCV_Code,
                ValueCV_CodeSystem = valueCV_CodeSystem,
                ValueCV_DisplayName = valueCV_DisplayName,
                ValueST = valueST,
                ValueBL = valueBL,
                ValueIVLPQ_LowValue = valueIVLPQ_LowValue,
                ValueIVLPQ_LowUnit = valueIVLPQ_LowUnit,
                ValueIVLPQ_HighValue = valueIVLPQ_HighValue,
                ValueIVLPQ_HighUnit = valueIVLPQ_HighUnit,
                ValueED_MediaType = valueED_MediaType,
                ValueED_FileName = valueED_FileName,
                ValueNullFlavor = valueNullFlavor
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all AdditionalIdentifier entities under [asIdentifiedEntity classCode="IDENT"] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct], [partProduct], or [product]) to scan for additional identifiers.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of AdditionalIdentifier records created.</returns>
        /// <remarks>
        /// Handles identifier types Model Number (C99286), Catalog Number (C99285), Reference Number (C99287), and related.
        /// Only processes entities with classCode="IDENT" and validates against NCI Thesaurus code system.
        /// Requires both identifier value and root OID to be present for data integrity.
        /// </remarks>
        /// <seealso cref="AdditionalIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveAdditionalIdentifiersAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<AdditionalIdentifier>();

            if (context == null || repo == null || context.Logger == null)
                return count;

            foreach (var idEnt in parentEl.SplElements(sc.E.AsIdentifiedEntity))
            {
                string? classCode = idEnt.GetAttrVal(sc.A.ClassCode);
                if (!string.Equals(classCode, "IDENT", StringComparison.OrdinalIgnoreCase))
                    continue;

                var idEl = idEnt.GetSplElement(sc.E.Id);
                string? identifierValue = idEl?.GetAttrVal(sc.A.Extension);
                string? identifierRootOID = idEl?.GetAttrVal(sc.A.Root);

                var codeEl = idEnt.GetSplElement(sc.E.Code);
                string? typeCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? typeCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? typeDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                if (string.IsNullOrWhiteSpace(typeCodeSystem) ||
                    typeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                    continue;

                if (string.IsNullOrWhiteSpace(identifierValue) || string.IsNullOrWhiteSpace(identifierRootOID))
                    continue;

                bool recognized = typeCode == "C99286" || typeCode == "C99285" || typeCode == "C99287";

                if (!recognized)
                    continue;

                var additionalIdentifier = new AdditionalIdentifier
                {
                    ProductID = product.ProductID,
                    IdentifierTypeCode = typeCode,
                    IdentifierTypeCodeSystem = typeCodeSystem,
                    IdentifierTypeDisplayName = typeDisplayName,
                    IdentifierValue = identifierValue,
                    IdentifierRootOID = identifierRootOID
                };

                await repo.CreateAsync(additionalIdentifier);
                count++;
                context.Logger.LogInformation($"AdditionalIdentifier created: ProductID={product.ProductID}, TypeCode={typeCode}, Value={identifierValue}, Root={identifierRootOID}");
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all ProductRouteOfAdministration entities from [consumedIn][substanceAdministration][routeCode] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [part]) to scan for routes of administration.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of ProductRouteOfAdministration records created.</returns>
        /// <remarks>
        /// Handles route code, code system, display name, and nullFlavor according to SPL IG Section 3.2.20.
        /// Enforces SPL specification: accepts either correct code system (2.16.840.1.113883.3.26.1.1) or nullFlavor.
        /// Validates route codes against FDA SPL standards for pharmaceutical products.
        /// </remarks>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveProductRoutesOfAdministrationAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<ProductRouteOfAdministration>();

            if (context == null || repo == null || context.Logger == null)
                return count;

            foreach (var consumedInEl in parentEl.SplElements(sc.E.ConsumedIn))
            {
                foreach (var substAdminEl in consumedInEl.SplElements(sc.E.SubstanceAdministration))
                {
                    var routeCodeEl = substAdminEl.GetSplElement(sc.E.RouteCode);

                    if (routeCodeEl == null)
                        continue;

                    string? routeCode = routeCodeEl.GetAttrVal(sc.A.CodeValue);
                    string? routeCodeSystem = routeCodeEl.GetAttrVal(sc.A.CodeSystem);
                    string? displayName = routeCodeEl.GetAttrVal(sc.A.DisplayName);
                    string? nullFlavor = routeCodeEl.GetAttrVal(sc.A.NullFlavor);

                    if (string.IsNullOrWhiteSpace(nullFlavor))
                    {
                        if (routeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                            continue;
                    }

                    var route = new ProductRouteOfAdministration
                    {
                        ProductID = product.ProductID,
                        RouteCode = routeCode,
                        RouteCodeSystem = routeCodeSystem,
                        RouteDisplayName = displayName,
                        RouteNullFlavor = nullFlavor
                    };

                    await repo.CreateAsync(route);
                    count++;
                    context.Logger.LogInformation(
                        $"ProductRouteOfAdministration created: ProductID={product.ProductID}, RouteCode={routeCode}, DisplayName={displayName}, NullFlavor={nullFlavor}");
                }
            }

            return count;
            #endregion
        }
    }
}