using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service
{
    // ========== INTERFACE ==========

    /**************************************************************/
    /// <summary>
    /// Interface for preparing packaging data for rendering by handling formatting,
    /// ordering, and attribute generation logic.
    /// </summary>
    /// <seealso cref="PackagingLevelDto"/>
    /// <seealso cref="PackageIdentifierDto"/>
    /// <seealso cref="PackageRendering"/>
    public interface IPackageRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete PackageRendering object with all computed properties
        /// for efficient template rendering.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to prepare for rendering</param>
        /// <param name="parentProduct">Required ProductDto for identifier correlation</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared PackageRendering object</returns>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="PackagingLevelDto"/>
        PackageRendering PrepareForRendering(PackagingLevelDto packagingLevel, ProductDto parentProduct, object? additionalParams = null);

        /**************************************************************/
        /// <summary>
        /// Generates appropriate display attributes for packaging level.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to generate attributes for</param>
        /// <returns>Formatted attribute string</returns>
        /// <seealso cref="PackagingLevelDto"/>
        string GenerateDisplayAttributes(PackagingLevelDto packagingLevel);

        /**************************************************************/
        /// <summary>
        /// Determines if packaging level has valid data for rendering operations.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to validate</param>
        /// <returns>True if packaging level has valid data</returns>
        /// <seealso cref="PackagingLevelDto"/>
        bool HasValidData(PackagingLevelDto packagingLevel);

        /**************************************************************/
        /// <summary>
        /// Gets package identifiers ordered by business rules.
        /// </summary>
        /// <param name="packagingLevel">The packaging level containing identifiers</param>
        /// <returns>Ordered list of identifiers or null if none exists</returns>
        /// <seealso cref="PackageIdentifierDto"/>
        List<PackageIdentifierDto>? GetOrderedPackageIdentifiers(PackagingLevelDto packagingLevel);

        /**************************************************************/
        /// <summary>
        /// Gets child packaging ordered by business rules.
        /// </summary>
        /// <param name="packagingLevel">The packaging level containing child packaging</param>
        /// <returns>Ordered list of child packaging or null if none exists</returns>
        /// <seealso cref="PackagingHierarchyDto"/>
        List<PackagingHierarchyDto>? GetOrderedChildPackaging(PackagingLevelDto packagingLevel);

        /**************************************************************/
        /// <summary>
        /// Formats quantity values according to SPL specifications.
        /// </summary>
        /// <param name="quantity">The decimal quantity to format</param>
        /// <returns>Formatted quantity string using G29 format</returns>
        string FormatQuantity(decimal? quantity);

        #endregion
    }

    // ========== IMPLEMENTATION ==========

    /**************************************************************/
    /// <summary>
    /// Service for preparing packaging data for rendering by handling formatting,
    /// ordering, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="IPackageRenderingService"/>
    /// <seealso cref="PackagingLevelDto"/>
    /// <seealso cref="PackageIdentifierDto"/>
    /// <remarks>
    /// This service encapsulates all business logic that was previously
    /// embedded in Razor views, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class PackageRenderingService : IPackageRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Default values used when packaging level doesn't specify required data.
        /// </summary>
        private const string DEFAULT_IDENTIFIER_TYPE = "NDC";
        private const string DEFAULT_QUANTITY_FORMAT = "G29";

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete PackageRendering object with all computed properties
        /// for efficient template rendering. Pre-computes all formatting and ordering
        /// operations to minimize processing in the view layer.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to prepare for rendering</param>
        /// <param name="parentProduct">Required ProductDto for identifier correlation</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared PackageRendering object with computed properties</returns>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <example>
        /// <code>
        /// var preparedPackage = service.PrepareForRendering(
        ///     packagingLevel: packagingLevelDto,
        ///     additionalParams: contextData
        /// );
        /// // preparedPackage now has all computed properties ready for rendering
        /// </code>
        /// </example>
        public PackageRendering PrepareForRendering(PackagingLevelDto packagingLevel, ProductDto parentProduct, object? additionalParams = null)
        {
            #region implementation
            if (packagingLevel == null)
                throw new ArgumentNullException(nameof(packagingLevel));

            if (parentProduct == null)
                throw new ArgumentNullException(nameof(parentProduct));

            var packageRendering = new PackageRendering
            {
                PackagingLevelDto = packagingLevel,

                // Pre-compute all existing rendering properties
                DisplayAttributes = GenerateDisplayAttributes(packagingLevel),
                HasValidData = HasValidData(packagingLevel),
                OrderedPackageIdentifiers = GetOrderedPackageIdentifiers(packagingLevel),
                OrderedChildPackaging = GetOrderedChildPackaging(packagingLevel),

                // Pre-compute availability flags
                HasPackageIdentifiers = GetOrderedPackageIdentifiers(packagingLevel)?.Any() == true,
                HasChildPackaging = GetOrderedChildPackaging(packagingLevel)?.Any() == true,
                HasMarketing = packagingLevel?.MarketingStatuses?.Any() == true,

                // Pre-compute formatted values with translation support
                FormattedQuantityNumerator = FormatQuantity(packagingLevel.QuantityNumerator),
                FormattedQuantityDenominator = FormatQuantity(packagingLevel.QuantityDenominator),

                // Enhanced unit code information with translation codes from parent product
                UnitCode = packagingLevel.PackageCode,
                UnitCodeSystem = packagingLevel.PackageCodeSystem ?? "2.16.840.1.113883.6.8", // UCUM
                UnitDisplayName = packagingLevel.QuantityNumeratorUnit,

                // Enhanced package form information with parent product context
                PackageFormCode = packagingLevel.PackageFormCode,
                PackageFormCodeSystem = packagingLevel.PackageFormCodeSystem ?? parentProduct.FormCodeSystem ?? "2.16.840.1.113883.3.26.1.1",
                PackageFormDisplayName = packagingLevel.PackageFormDisplayName,

                // Set translation codes directly from packaging level data
                // Numerator translation should use package form, not ingredient data
                NumeratorTranslationCode = packagingLevel.NumeratorTranslationCode,
                NumeratorCodeSystem = packagingLevel.NumeratorTranslationCodeSystem ?? parentProduct.FormCodeSystem ?? "2.16.840.1.113883.3.26.1.1",
                NumeratorDisplayName = packagingLevel.NumeratorTranslationDisplayName,

                // Explicitly set denominator properties to null to prevent unwanted XML attributes
                DenominatorTranslationCode = null,
                DenominatorCodeSystem = null,
                DenominatorDisplayName = null
            };

            // Build recursive ChildPackageRendering structure
            buildRecursiveChildPackaging(packageRendering, parentProduct, additionalParams);

            // Correlate ProductDto.PackageIdentifiers with packaging levels
            correlatePackageIdentifiers(packageRendering, parentProduct);

            return packageRendering;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates appropriate display attributes for packaging level based on
        /// business rules and formatting requirements.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to generate attributes for</param>
        /// <returns>Formatted attribute string with appropriate fallbacks</returns>
        /// <seealso cref="PackagingLevelDto"/>
        /// <example>
        /// <code>
        /// var attributes = service.GenerateDisplayAttributes(packagingLevel);
        /// // Returns: "package_123" or similar formatted identifier
        /// </code>
        /// </example>
        public string GenerateDisplayAttributes(PackagingLevelDto packagingLevel)
        {
            #region implementation

            if (packagingLevel == null)
                return string.Empty;

            // Apply domain-specific formatting logic for package display
            return !string.IsNullOrEmpty(packagingLevel.PackageCode)
                ? formatPackageCode(packagingLevel.PackageCode)
                : packagingLevel.PackagingLevelID?.ToString() ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if packaging level has valid data for rendering operations.
        /// </summary>
        /// <param name="packagingLevel">The packaging level to validate</param>
        /// <returns>True if packaging level has valid data</returns>
        /// <seealso cref="PackagingLevelDto"/>
        public bool HasValidData(PackagingLevelDto packagingLevel)
        {
            #region implementation

            if (packagingLevel == null)
                return false;

            // A packaging level is valid if it has either a quantity or identifiers
            return (packagingLevel.QuantityNumerator.HasValue && packagingLevel.QuantityNumerator > 0) ||
                   (packagingLevel.PackageIdentifiers?.Any() == true) ||
                   (!string.IsNullOrEmpty(packagingLevel.PackageFormCode));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets package identifiers ordered by business rules.
        /// Filters out empty identifiers and orders by identifier ID.
        /// </summary>
        /// <param name="packagingLevel">The packaging level containing identifiers</param>
        /// <returns>Ordered list of identifiers or null if none exists</returns>
        /// <seealso cref="PackageIdentifierDto"/>
        public List<PackageIdentifierDto>? GetOrderedPackageIdentifiers(PackagingLevelDto packagingLevel)
        {
            #region implementation

            if (packagingLevel?.PackageIdentifiers == null)
                return null;

            // Apply the same filtering and ordering logic from the original view
            var orderedIdentifiers = packagingLevel.PackageIdentifiers
                .Where(pi => !string.IsNullOrEmpty(pi.IdentifierValue))
                .OrderBy(pi => pi.PackageIdentifierID)
                .ToList();

            return orderedIdentifiers.Any() ? orderedIdentifiers : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets child packaging ordered by business rules.
        /// Filters to only packaging with valid child levels and orders by sequence.
        /// </summary>
        /// <param name="packagingLevel">The packaging level containing child packaging</param>
        /// <returns>Ordered list of child packaging or null if none exists</returns>
        /// <seealso cref="PackagingHierarchyDto"/>
        public List<PackagingHierarchyDto>? GetOrderedChildPackaging(PackagingLevelDto packagingLevel)
        {
            #region implementation

            if (packagingLevel?.PackagingHierarchy == null)
                return null;

            // Apply the same filtering and ordering logic from the original view
            var orderedChildPackaging = packagingLevel.PackagingHierarchy
                .Where(h => h.ChildPackagingLevel != null)
                .OrderBy(h => h.SequenceNumber)
                .ToList();

            return orderedChildPackaging.Any() ? orderedChildPackaging : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats quantity values according to SPL specifications.
        /// </summary>
        /// <param name="quantity">The decimal quantity to format</param>
        /// <returns>Formatted quantity string using G29 format</returns>
        public string FormatQuantity(decimal? quantity)
        {
            #region implementation

            return quantity?.ToString(DEFAULT_QUANTITY_FORMAT) ?? string.Empty;

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Builds recursive ChildPackageRendering structure instead of flat packaging.
        /// Processes PackagingHierarchy to create proper nested packaging structure.
        /// </summary>
        /// <param name="packageRendering">The parent package rendering context</param>
        /// <param name="parentProduct">Parent product for context</param>
        /// <param name="additionalParams">Additional parameters</param>
        private void buildRecursiveChildPackaging(PackageRendering packageRendering, ProductDto parentProduct, object? additionalParams)
        {
            #region implementation

            if (!packageRendering.HasChildPackaging || packageRendering.OrderedChildPackaging == null)
            {
                packageRendering.ChildPackageRendering = null;
                packageRendering.HasChildPackageRendering = false;
                return;
            }

            var childRenderings = new List<PackageRendering>();

            foreach (var childHierarchy in packageRendering.OrderedChildPackaging)
            {
                if (childHierarchy.ChildPackagingLevel != null)
                {
                    // Recursively prepare child packaging with product context
                    var childPackageRendering = PrepareForRendering(
                        packagingLevel: childHierarchy.ChildPackagingLevel,
                        parentProduct: parentProduct, // Pass product context for correlation
                        additionalParams: additionalParams
                    );

                    childRenderings.Add(childPackageRendering);
                }
            }

            packageRendering.ChildPackageRendering = childRenderings.Any() ? childRenderings : null;
            packageRendering.HasChildPackageRendering = childRenderings.Any();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Correlates ProductDto.PackageIdentifiers with packaging levels by business rules.
        /// Extracts NDC codes and populates them in the appropriate packaging levels.
        /// </summary>
        /// <param name="packageRendering">The package rendering context to enhance</param>
        /// <param name="parentProduct">Parent product containing package identifiers</param>
        private static void correlatePackageIdentifiers(PackageRendering packageRendering, ProductDto parentProduct)
        {
            #region implementation

            if (parentProduct.PackageIdentifiers == null || !parentProduct.PackageIdentifiers.Any())
                return;

            // Create enhanced package identifier list with NDC correlation
            var enhancedIdentifiers = new List<PackageIdentifierDto>();

            // Business rule: Match package identifiers to packaging levels
            // This could be based on sequence, quantity, or other business logic
            var packagingLevel = packageRendering.PackagingLevelDto;

            // Find matching NDC identifiers for this packaging level
            var matchingIdentifiers = parentProduct.PackageIdentifiers
                .Where(pi => pi.PackagingLevelID == packagingLevel.PackagingLevelID 
                    && !string.IsNullOrEmpty(pi.IdentifierValue))
                .ToList();

            if (matchingIdentifiers.Any())
            {              
                foreach (var primaryIdentifier in matchingIdentifiers)
                {

                    if (primaryIdentifier != null)
                    {
                        var newIdentifier = new PackageIdentifierDto
                        {
                            PackageIdentifier = new Dictionary<string, object?>
                            {
                                [nameof(PackageIdentifierDto.PackageIdentifierID)] = primaryIdentifier.PackageIdentifierID,
                                [nameof(PackageIdentifierDto.PackagingLevelID)] = packagingLevel.PackagingLevelID,
                                [nameof(PackageIdentifierDto.IdentifierValue)] = primaryIdentifier.IdentifierValue,
                                [nameof(PackageIdentifierDto.IdentifierSystemOID)] = primaryIdentifier.IdentifierSystemOID ?? "2.16.840.1.113883.6.69",
                                [nameof(PackageIdentifierDto.IdentifierType)] = primaryIdentifier.IdentifierType ?? DEFAULT_IDENTIFIER_TYPE
                            }
                        };

                        enhancedIdentifiers.Add(newIdentifier);
                    }
                }

                if (enhancedIdentifiers != null && enhancedIdentifiers.Any())
                {
                    packageRendering.OrderedPackageIdentifiers = enhancedIdentifiers.OrderBy(x => x.PackagingLevelID).ToList();
                    packageRendering.HasPackageIdentifiers = true;
                }
            }
            else
            {
                // No matching NDC for this specific packaging level - render empty code
                packageRendering.OrderedPackageIdentifiers = null;
                packageRendering.HasPackageIdentifiers = false;
            }




            #endregion
        }
        
        /**************************************************************/
        /// <summary>
        /// Formats package code according to domain-specific rules.
        /// </summary>
        /// <param name="packageCode">The package code to format</param>
        /// <returns>Formatted package code string</returns>
        /// <seealso cref="PackagingLevelDto"/>
        private string formatPackageCode(string packageCode)
        {
            #region implementation

            // Apply domain-specific formatting rules
            return packageCode
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToLowerInvariant()
                .Trim();

            #endregion
        }

        #endregion
    }
}