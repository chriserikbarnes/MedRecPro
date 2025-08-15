using System.Xml.Linq;
using MedRecPro.Models;
using MedRecPro.Helpers;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Provides DRY methods for parsing and creating DosingSpecification entities from SPL XML elements.
    /// Implements parsing logic for SPL Implementation Guide Section 16.2.4 dosing specifications.
    /// </summary>
    /// <remarks>
    /// This class contains reusable methods for extracting dosing specification data from XML elements
    /// and creating validated DosingSpecification entities. Designed to be called from ManufacturedProductParser
    /// and other parsing services to maintain consistency and reduce code duplication.
    /// </remarks>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="DosingSpecification"/>
    /// <seealso cref="DosingSpecificationValidationService"/>
    /// <seealso cref="Label"/>
    public static class DosingSpecificationParser
    {
        #region implementation
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses and creates DosingSpecification entities from consumedIn/substanceAdministration XML elements within a product.
        /// Implements SPL Implementation Guide Section 16.2.4 requirements for dosing specifications.
        /// </summary>
        /// <param name="parentEl">The parent XML element (usually manufacturedProduct) containing consumedIn elements.</param>
        /// <param name="product">The Product entity to associate with created dosing specifications.</param>
        /// <param name="context">The parsing context containing repository access and logging services.</param>
        /// <returns>The count of DosingSpecification records successfully created and saved.</returns>
        /// <example>
        /// <code>
        /// var count = await DosingSpecificationParser.BuildDosingSpecificationAsync(
        ///     manufacturedProductEl, product, context);
        /// Console.WriteLine($"Created {count} dosing specifications");
        /// </code>
        /// </example>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="Label"/>
        public static async Task<int> BuildDosingSpecificationAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required dependencies
            if (parentEl == null || product == null || context?.Logger == null || !product.ProductID.HasValue)
            {
                context?.Logger?.LogWarning("Invalid parameters for dosing specification parsing. Skipping.");
                return createdCount;
            }

            var repository = context.GetRepository<DosingSpecification>();
            if (repository == null)
            {
                context.Logger.LogError("DosingSpecification repository not available in context.");
                return createdCount;
            }

            context.Logger.LogDebug("Starting dosing specification parsing for ProductID {ProductID}", product.ProductID);

            try
            {
                // Find all consumedIn/substanceAdministration structures for dosing specifications
                var consumedInElements = parentEl.SplFindElements(sc.E.ConsumedIn);

                foreach (var consumedInEl in consumedInElements)
                {
                    var substanceAdminElements = consumedInEl.SplElements(sc.E.SubstanceAdministration);

                    foreach (var substanceAdminEl in substanceAdminElements)
                    {
                        // Parse dosing specification from substanceAdministration element
                        var dosingSpec = await parseDosingSpecificationFromXmlAsync(
                            substanceAdminEl, product.ProductID.Value, context);

                        if (dosingSpec != null)
                        {
                            // Validate the dosing specification before saving
                            if (validateDosingSpecification(dosingSpec, context))
                            {
                                await repository.CreateAsync(dosingSpec);
                                createdCount++;

                                context.Logger.LogInformation(
                                    "Created DosingSpecification for ProductID {ProductID} with route {RouteCode}",
                                    product.ProductID, dosingSpec.RouteCode);
                            }
                        }
                    }
                }

                context.Logger.LogInformation(
                    "Completed dosing specification parsing for ProductID {ProductID}. Created {Count} specifications.",
                    product.ProductID, createdCount);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex,
                    "Error parsing dosing specifications for ProductID {ProductID}", product.ProductID);
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a single DosingSpecification entity from a substanceAdministration XML element.
        /// Extracts route codes, dose quantities, and units according to SPL standards.
        /// </summary>
        /// <param name="substanceAdminEl">The substanceAdministration XML element to parse.</param>
        /// <param name="productId">The ProductID to associate with the dosing specification.</param>
        /// <param name="context">The parsing context for logging and error handling.</param>
        /// <returns>A DosingSpecification entity with populated data, or null if parsing fails.</returns>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<DosingSpecification?> parseDosingSpecificationFromXmlAsync(
            XElement substanceAdminEl,
            int productId,
            SplParseContext context)
        {
            #region implementation
            try
            {
                var dosingSpec = new DosingSpecification
                {
                    ProductID = productId
                };

                // Parse route code information (SPL IG 16.2.4.2)
                var routeInfo = parseRouteCodeFromXml(substanceAdminEl, context);
                dosingSpec.RouteCode = routeInfo.RouteCode;
                dosingSpec.RouteCodeSystem = routeInfo.RouteCodeSystem;
                dosingSpec.RouteDisplayName = routeInfo.RouteDisplayName;
                dosingSpec.RouteNullFlavor = routeInfo.RouteNullFlavor;

                // Parse dose quantity information (SPL IG 16.2.4.3-16.2.4.7)
                var doseInfo = parseDoseQuantityFromXml(substanceAdminEl, context);
                dosingSpec.DoseQuantityValue = doseInfo.DoseQuantityValue;
                dosingSpec.DoseQuantityUnit = doseInfo.DoseQuantityUnit;

                return dosingSpec;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    "Error parsing DosingSpecification from XML for ProductID {ProductID}", productId);
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts route code information from a substanceAdministration XML element.
        /// Handles routeCode elements with code, codeSystem, displayName, and nullFlavor attributes.
        /// </summary>
        /// <param name="substanceAdminEl">The substanceAdministration XML element containing route information.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A tuple containing route code data elements.</returns>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Label"/>
        private static (string? RouteCode, string? RouteCodeSystem, string? RouteDisplayName, string? RouteNullFlavor)
            parseRouteCodeFromXml(XElement substanceAdminEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Find the routeCode element within substanceAdministration
                var routeCodeEl = substanceAdminEl.GetSplElement(sc.E.RouteCode);

                if (routeCodeEl == null)
                {
                    context?.Logger?.LogWarning("No routeCode element found in substanceAdministration");
                    return (null, null, null, null);
                }

                // Extract route code attributes
                var routeCode = routeCodeEl.GetAttrVal(sc.A.CodeValue)?.Trim();
                var routeCodeSystem = routeCodeEl.GetAttrVal(sc.A.CodeSystem)?.Trim();
                var routeDisplayName = routeCodeEl.GetAttrVal(sc.A.DisplayName)?.Trim();
                var routeNullFlavor = routeCodeEl.GetAttrVal(sc.A.NullFlavor)?.Trim();

                return (routeCode, routeCodeSystem, routeDisplayName, routeNullFlavor);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error parsing route code from XML");
                return (null, null, null, null);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts dose quantity information from a substanceAdministration XML element.
        /// Handles doseQuantity elements with value and unit attributes according to SPL standards.
        /// </summary>
        /// <param name="substanceAdminEl">The substanceAdministration XML element containing dose information.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A tuple containing dose quantity value and unit.</returns>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private static (decimal? DoseQuantityValue, string? DoseQuantityUnit)
            parseDoseQuantityFromXml(XElement substanceAdminEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Find the doseQuantity element within substanceAdministration
                var doseQuantityEl = substanceAdminEl.GetSplElement(sc.E.DoseQuantity);

                if (doseQuantityEl == null)
                {
                    // Variable dose products may not have doseQuantity element (SPL IG 16.2.4.3)
                    context?.Logger?.LogDebug("No doseQuantity element found - may be variable dose product");
                    return (null, null);
                }

                // Extract dose quantity value and unit
                var valueStr = doseQuantityEl.GetAttrVal(sc.A.Value)?.Trim();
                var unit = doseQuantityEl.GetAttrVal(sc.A.Unit)?.Trim();

                // Parse the numeric value using utility method
                decimal? doseValue = null;
                if (!string.IsNullOrWhiteSpace(valueStr))
                {
                    doseValue = Util.ParseNullableDecimal(valueStr);

                    if (doseValue == null)
                    {
                        context?.Logger?.LogWarning(
                            "Could not parse dose quantity value '{ValueStr}' as decimal", valueStr);
                    }
                }

                return (doseValue, unit);
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error parsing dose quantity from XML");
                return (null, null);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a DosingSpecification entity using the validation service before saving to database.
        /// Ensures compliance with SPL Implementation Guide Section 16.2.4 requirements.
        /// </summary>
        /// <param name="dosingSpec">The DosingSpecification entity to validate.</param>
        /// <param name="context">The parsing context containing logging services.</param>
        /// <returns>True if validation passes, false if validation fails.</returns>
        /// <seealso cref="DosingSpecificationValidationService"/>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Label"/>
        private static bool validateDosingSpecification(DosingSpecification dosingSpec, SplParseContext context)
        {
            #region implementation
            try
            {
                if(context == null || context.Logger == null)
                {
                    return false;
                }
                if (dosingSpec == null)
                {
                    context.Logger.LogWarning("DosingSpecification cannot be null for validation");
                    return false;
                }

                // Create validation service instance
                var validationService = new DosingSpecificationValidationService(context.Logger);

                // Perform comprehensive validation
                var validationResult = validationService.ValidateDosingSpecification(dosingSpec);

                if (!validationResult.IsValid)
                {
                    // Log all validation errors
                    foreach (var error in validationResult.Errors)
                    {
                        context.Logger.LogWarning(
                            "DosingSpecification validation error for ProductID {ProductID}: {Error}",
                            dosingSpec.ProductID, error);
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex,
                    "Error during DosingSpecification validation for ProductID {ProductID}",
                    dosingSpec.ProductID);
                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a DosingSpecification entity with default values for testing or fallback scenarios.
        /// Provides a standardized way to create valid dosing specification entities.
        /// </summary>
        /// <param name="productId">The ProductID to associate with the dosing specification.</param>
        /// <param name="routeCode">Optional route code (defaults to "ORAL").</param>
        /// <param name="doseValue">Optional dose value (defaults to null for variable dose).</param>
        /// <param name="doseUnit">Optional dose unit (defaults to null for variable dose).</param>
        /// <returns>A DosingSpecification entity with specified or default values.</returns>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Label"/>
        public static DosingSpecification CreateDefault(
            int productId,
            string? routeCode = "ORAL",
            decimal? doseValue = null,
            string? doseUnit = null)
        {
            #region implementation
            return new DosingSpecification
            {
                ProductID = productId,
                RouteCode = routeCode,
                RouteCodeSystem = "2.16.840.1.113883.3.26.1.1", // FDA SPL code system
                RouteDisplayName = routeCode == "ORAL" ? "Oral route" : null,
                DoseQuantityValue = doseValue,
                DoseQuantityUnit = doseUnit
            };
            #endregion
        }
    }
}