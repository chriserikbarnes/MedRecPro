
﻿using System.Xml.Linq;
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
    /// Parses product characteristics and attributes, including physical properties, additional identifiers
    /// (like model/catalog numbers), and routes of administration.
    /// </summary>
    /// <remarks>
    /// This parser is responsible for detailing the specific attributes of a product. It is called by a
    /// parent parser and requires that the `SplParseContext.CurrentProduct` has been established.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="Characteristic"/>
    /// <seealso cref="AdditionalIdentifier"/>
    /// <seealso cref="ProductRouteOfAdministration"/>
    /// <seealso cref="SplParseContext"/>
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
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of product attributes by delegating to specialized
        /// private methods. It assumes `context.CurrentProduct` is set by the calling parser.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
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
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct] or [partProduct]) to scan for characteristics.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of Characteristic records created.</returns>
        /// <remarks>
        /// Handles characteristic value types PQ, INT, IVL_PQ, CV, ST, ED, and BL according to SPL IG.
        /// Supports complex value types including intervals, coded values, and multimedia references.
        /// Each characteristic includes both the code identifying the characteristic type and the
        /// appropriately typed value based on the xsi:type attribute.
        /// </remarks>
        /// <seealso cref="Characteristic"/>
        /// <seealso cref="Product"/>
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

            // Process all subjectOf/characteristic structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var charEl in subjOf.SplElements(sc.E.Characteristic))
                {
                    // --- Parse Characteristic code & codeSystem ---
                    var codeEl = charEl.GetSplElement(sc.E.Code);
                    string? charCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                    string? charCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);

                    // --- Parse <value> node and its type ---
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

                    // --- Parse based on xsi:type to populate appropriate value fields ---
                    if (!string.IsNullOrWhiteSpace(valueType))
                    {
                        switch (valueType.ToUpperInvariant())
                        {
                            case "PQ":
                            case "REAL": // treat as decimal/quantity
                                if (valueEl != null)
                                {
                                    // Parse physical quantity with value and unit
                                    var valueAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valuePQ_Value = valueAttr != null ? Util.ParseNullableDecimal(valueAttr) : null;
                                    valuePQ_Unit = valueEl.GetAttrVal(sc.A.Unit);
                                }
                                break;

                            case "INT":
                                if (valueEl != null)
                                {
                                    // Parse integer value with optional null flavor
                                    valueNullFlavor = valueEl.GetAttrVal(sc.A.NullFlavor);
                                    var intAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valueINT = intAttr != null ? Util.ParseNullableInt(intAttr) : null;
                                }
                                break;

                            case "CV":
                            case "CE": // Handle CV and CE the same way
                                       // Parse coded value with code, system, and display name
                                valueCV_Code = valueEl?.GetAttrVal(sc.A.CodeValue);
                                valueCV_CodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem);
                                valueCV_DisplayName = valueEl?.GetAttrVal(sc.A.DisplayName);
                                break;

                            case "ST":
                                // Parse string value from element text content
                                valueST = valueEl?.Value;
                                break;

                            case "IVL_PQ":
                                // Parse interval of physical quantities (low and high values)
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
                                // Parse encapsulated data (multimedia references)
                                valueED_MediaType = valueEl?.GetAttrVal(sc.A.MediaType);
                                valueED_FileName = valueEl?.GetAttrVal(sc.A.DisplayName);
                                break;

                            case "BL":
                                if (valueEl != null)
                                {
                                    // Parse boolean value from string representation
                                    var boolAttr = valueEl.GetAttrVal(sc.A.Value);
                                    valueBL = boolAttr != null ? Util.ParseNullableBoolWithStringValue(boolAttr) : null;
                                }
                                break;
                        }
                    }

                    // --- Build and save the Characteristic entity ---
                    var characteristic = new Characteristic
                    {
                        ProductID = product.ProductID,
                        CharacteristicCode = charCode,
                        CharacteristicCodeSystem = charCodeSystem,
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

                    // Save the characteristic entity to the database
                    await repo.CreateAsync(characteristic);
                    count++;
                    context.Logger.LogInformation($"Characteristic created: ProductID={product.ProductID}, Code={charCode}, ValueType={valueType}");
                }
            }

            return count;
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

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Find all <asIdentifiedEntity> nodes with classCode="IDENT"
            foreach (var idEnt in parentEl.SplElements(sc.E.AsIdentifiedEntity))
            {
                // Only process if classCode="IDENT"
                string? classCode = idEnt.GetAttrVal(sc.A.ClassCode);
                if (!string.Equals(classCode, "IDENT", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse the <id> child for identifier value and root
                var idEl = idEnt.GetSplElement(sc.E.Id);
                string? identifierValue = idEl?.GetAttrVal(sc.A.Extension);
                string? identifierRootOID = idEl?.GetAttrVal(sc.A.Root);

                // Parse the <code> child (type of identifier)
                var codeEl = idEnt.GetSplElement(sc.E.Code);
                string? typeCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? typeCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? typeDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                // Validation: Only accept NCI Thesaurus code system for type (per Table 3)
                if (string.IsNullOrWhiteSpace(typeCodeSystem) ||
                    typeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                    continue;

                // At least one id (extension/root) must be present, and a recognized code type
                if (string.IsNullOrWhiteSpace(identifierValue) || string.IsNullOrWhiteSpace(identifierRootOID))
                    continue;

                // Recognized identifier codes (per Table 3)
                bool recognized = typeCode == "C99286" // Model Number
                               || typeCode == "C99285" // Catalog Number
                               || typeCode == "C99287"; // Reference Number

                if (!recognized)
                    continue;

                // Build and save the AdditionalIdentifier entity
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

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all consumedIn/substanceAdministration structures
            foreach (var consumedInEl in parentEl.SplElements(sc.E.ConsumedIn))
            {
                foreach (var substAdminEl in consumedInEl.SplElements(sc.E.SubstanceAdministration))
                {
                    var routeCodeEl = substAdminEl.GetSplElement(sc.E.RouteCode);

                    if (routeCodeEl == null)
                        continue;

                    // Parse route attributes from the XML element
                    string? routeCode = routeCodeEl.GetAttrVal(sc.A.CodeValue);
                    string? routeCodeSystem = routeCodeEl.GetAttrVal(sc.A.CodeSystem);
                    string? displayName = routeCodeEl.GetAttrVal(sc.A.DisplayName);
                    string? nullFlavor = routeCodeEl.GetAttrVal(sc.A.NullFlavor);

                    // Enforce SPL spec: Either code system is correct or nullFlavor is set
                    if (string.IsNullOrWhiteSpace(nullFlavor))
                    {
                        // Only accept route codes with the proper FDA SPL code system
                        if (routeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                            continue;
                    }

                    // Build and save the ProductRouteOfAdministration entity
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
