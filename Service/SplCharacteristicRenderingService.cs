using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing characteristic data for rendering by handling value type
    /// determination, formatting, and rendering logic computation.
    /// </summary>
    /// <seealso cref="CharacteristicDto"/>
    /// <seealso cref="CharacteristicRendering"/>
    public interface ICharacteristicRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Prepares a characteristic for rendering with pre-computed properties and logic flags.
        /// </summary>
        /// <param name="characteristic">The characteristic to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared CharacteristicRendering object with computed properties</returns>
        /// <seealso cref="CharacteristicRendering"/>
        /// <seealso cref="CharacteristicDto"/>
        CharacteristicRendering PrepareForRendering(CharacteristicDto characteristic, object? additionalParams = null);

        /**************************************************************/
        /// <summary>
        /// Determines the normalized value type for consistent comparison operations.
        /// </summary>
        /// <param name="characteristic">The characteristic containing value type information</param>
        /// <returns>Uppercase normalized value type or null if not specified</returns>
        /// <seealso cref="CharacteristicDto.ValueType"/>
        string? GetNormalizedValueType(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a coded value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for coded value</param>
        /// <returns>True if coded value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueCV_Code"/>
        bool HasCodedValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a quantity value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for quantity value</param>
        /// <returns>True if quantity value exists</returns>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        bool HasQuantityValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a boolean value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for boolean value</param>
        /// <returns>True if boolean value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        bool HasBooleanValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has an integer value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for integer value</param>
        /// <returns>True if integer value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        bool HasIntegerValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a string value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for string value</param>
        /// <returns>True if string value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueST"/>
        bool HasStringValue(CharacteristicDto characteristic);

        #endregion

        #region rendering logic methods

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as coded element (CE) based on business rules.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasCodedValue">Pre-computed coded value flag</param>
        /// <returns>True if should render as coded element</returns>
        /// <seealso cref="CharacteristicDto"/>
        bool ShouldRenderAsCodedElement(CharacteristicDto characteristic, string? normalizedValueType, bool hasCodedValue);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as physical quantity (PQ) based on business rules.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasQuantityValue">Pre-computed quantity value flag</param>
        /// <returns>True if should render as physical quantity</returns>
        /// <seealso cref="CharacteristicDto"/>
        bool ShouldRenderAsPhysicalQuantity(CharacteristicDto characteristic, string? normalizedValueType, bool hasQuantityValue);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as boolean (BL) based on business rules.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasBooleanValue">Pre-computed boolean value flag</param>
        /// <returns>True if should render as boolean</returns>
        /// <seealso cref="CharacteristicDto"/>
        bool ShouldRenderAsBoolean(CharacteristicDto characteristic, string? normalizedValueType, bool hasBooleanValue);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as integer (INT) based on business rules.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasIntegerValue">Pre-computed integer value flag</param>
        /// <returns>True if should render as integer</returns>
        /// <seealso cref="CharacteristicDto"/>
        bool ShouldRenderAsInteger(CharacteristicDto characteristic, string? normalizedValueType, bool hasIntegerValue);

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as string (ST) based on business rules.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasStringValue">Pre-computed string value flag</param>
        /// <returns>True if should render as string</returns>
        /// <seealso cref="CharacteristicDto"/>
        bool ShouldRenderAsString(CharacteristicDto characteristic, string? normalizedValueType, bool hasStringValue);

        #endregion

        #region value formatting methods

        /**************************************************************/
        /// <summary>
        /// Formats boolean value for rendering as string representation.
        /// </summary>
        /// <param name="characteristic">The characteristic containing boolean value</param>
        /// <returns>Formatted boolean string or null if no boolean value</returns>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        string? FormatBooleanValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Formats quantity value for rendering with precision formatting.
        /// </summary>
        /// <param name="characteristic">The characteristic containing quantity value</param>
        /// <returns>Formatted quantity string or null if no quantity value</returns>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        string? FormatQuantityValue(CharacteristicDto characteristic);

        /**************************************************************/
        /// <summary>
        /// Formats integer value for rendering with precision formatting.
        /// </summary>
        /// <param name="characteristic">The characteristic containing integer value</param>
        /// <returns>Formatted integer string or null if no integer value</returns>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        string? FormatIntegerValue(CharacteristicDto characteristic);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing characteristic data for rendering by handling value type
    /// determination, formatting, and rendering logic computation. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="ICharacteristicRenderingService"/>
    /// <seealso cref="CharacteristicDto"/>
    /// <seealso cref="CharacteristicRendering"/>
    /// <remarks>
    /// This service encapsulates all business logic that was previously
    /// embedded in the _Characteristic.cshtml view, promoting better separation of concerns
    /// and testability. It follows the established patterns for ingredient and packaging rendering services.
    /// </remarks>
    public class CharacteristicRenderingService : ICharacteristicRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Value type constants for rendering logic operations.
        /// </summary>
        private const string CODED_ELEMENT_TYPE = "CE";
        private const string PHYSICAL_QUANTITY_TYPE = "PQ";
        private const string BOOLEAN_TYPE = "BL";
        private const string INTEGER_TYPE = "INT";
        private const string STRING_TYPE = "ST";

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a characteristic for rendering with comprehensive pre-computed properties and logic flags.
        /// Computes all rendering decisions, value formatting, and display flags to eliminate template complexity
        /// following the established pattern for ingredient and packaging rendering services.
        /// </summary>
        /// <param name="characteristic">The characteristic to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared CharacteristicRendering object with all computed properties</returns>
        /// <seealso cref="CharacteristicRendering"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <example>
        /// <code>
        /// var preparedCharacteristic = service.PrepareForRendering(
        ///     characteristic: characteristicDto,
        ///     additionalParams: new { DocumentGuid = documentGuid }
        /// );
        /// // preparedCharacteristic now has all computed properties ready for rendering
        /// </code>
        /// </example>
        /// <remarks>
        /// The preparation process computes:
        /// - Normalized value type for consistent comparisons
        /// - Boolean flags for all value type availability checks
        /// - Rendering logic flags for template decision making
        /// - Formatted value strings for direct template output
        /// - Validation flags for content availability
        /// </remarks>
        public CharacteristicRendering PrepareForRendering(CharacteristicDto characteristic, object? additionalParams = null)
        {
            #region implementation

            if (characteristic == null)
                throw new ArgumentNullException(nameof(characteristic));

            // Compute basic value availability flags
            var normalizedValueType = GetNormalizedValueType(characteristic);
            var hasCodedValue = HasCodedValue(characteristic);
            var hasQuantityValue = HasQuantityValue(characteristic);
            var hasBooleanValue = HasBooleanValue(characteristic);
            var hasIntegerValue = HasIntegerValue(characteristic);
            var hasStringValue = HasStringValue(characteristic);

            // Compute rendering logic flags based on business rules from original template
            var shouldRenderAsCodedElement = ShouldRenderAsCodedElement(characteristic, normalizedValueType, hasCodedValue);
            var shouldRenderAsPhysicalQuantity = ShouldRenderAsPhysicalQuantity(characteristic, normalizedValueType, hasQuantityValue);
            var shouldRenderAsBoolean = ShouldRenderAsBoolean(characteristic, normalizedValueType, hasBooleanValue);
            var shouldRenderAsInteger = ShouldRenderAsInteger(characteristic, normalizedValueType, hasIntegerValue);
            var shouldRenderAsString = ShouldRenderAsString(characteristic, normalizedValueType, hasStringValue);

            // Compute fallback rendering flags for complex template logic
            var shouldRenderAsFallbackCoded = !shouldRenderAsCodedElement && !shouldRenderAsPhysicalQuantity &&
                                               !shouldRenderAsBoolean && !shouldRenderAsInteger &&
                                               !shouldRenderAsString && hasCodedValue;

            var shouldRenderAsFallbackString = !shouldRenderAsCodedElement && !shouldRenderAsPhysicalQuantity &&
                                               !shouldRenderAsBoolean && !shouldRenderAsInteger &&
                                               !shouldRenderAsString && !shouldRenderAsFallbackCoded && hasStringValue;

            // Create comprehensive characteristic rendering object
            var characteristicRendering = new CharacteristicRendering
            {
                CharacteristicDto = characteristic,

                // Value type and availability properties
                NormalizedValueType = normalizedValueType,
                HasCodedValue = hasCodedValue,
                HasQuantityValue = hasQuantityValue,
                HasBooleanValue = hasBooleanValue,
                HasIntegerValue = hasIntegerValue,
                HasStringValue = hasStringValue,

                // Rendering logic flags
                ShouldRenderAsCodedElement = shouldRenderAsCodedElement,
                ShouldRenderAsPhysicalQuantity = shouldRenderAsPhysicalQuantity,
                ShouldRenderAsBoolean = shouldRenderAsBoolean,
                ShouldRenderAsInteger = shouldRenderAsInteger,
                ShouldRenderAsString = shouldRenderAsString,
                ShouldRenderAsFallbackCoded = shouldRenderAsFallbackCoded,
                ShouldRenderAsFallbackString = shouldRenderAsFallbackString,

                // Formatted value properties for direct template output
                FormattedBooleanValue = FormatBooleanValue(characteristic),
                FormattedQuantityValue = FormatQuantityValue(characteristic),
                FormattedIntegerValue = FormatIntegerValue(characteristic),

                // Validation and display flags
                HasRenderableContent = hasRenderableContent(characteristic),
                ShouldDisplayOriginalText = shouldDisplayOriginalText(characteristic)
            };

            return characteristicRendering;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the normalized value type for consistent comparison operations.
        /// Converts value type to uppercase for standardized string matching.
        /// </summary>
        /// <param name="characteristic">The characteristic containing value type information</param>
        /// <returns>Uppercase normalized value type or null if not specified</returns>
        /// <seealso cref="CharacteristicDto.ValueType"/>
        public string? GetNormalizedValueType(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValueType?.ToString()?.ToUpperInvariant();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a coded value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for coded value</param>
        /// <returns>True if coded value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueCV_Code"/>
        public bool HasCodedValue(CharacteristicDto characteristic)
        {
            #region implementation

            return !string.IsNullOrEmpty(characteristic?.ValueCV_Code);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a quantity value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for quantity value</param>
        /// <returns>True if quantity value exists</returns>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        public bool HasQuantityValue(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValuePQ_Value.HasValue == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a boolean value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for boolean value</param>
        /// <returns>True if boolean value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        public bool HasBooleanValue(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValueBL != null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has an integer value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for integer value</param>
        /// <returns>True if integer value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        public bool HasIntegerValue(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValueINT.HasValue == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has a string value available for rendering.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for string value</param>
        /// <returns>True if string value exists</returns>
        /// <seealso cref="CharacteristicDto.ValueST"/>
        public bool HasStringValue(CharacteristicDto characteristic)
        {
            #region implementation

            return !string.IsNullOrEmpty(characteristic?.ValueST);

            #endregion
        }

        #endregion

        #region rendering logic methods

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as coded element (CE) based on business rules.
        /// Priority: explicit CE type or coded value present with no explicit type.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasCodedValue">Pre-computed coded value flag</param>
        /// <returns>True if should render as coded element</returns>
        /// <seealso cref="CharacteristicDto"/>
        public bool ShouldRenderAsCodedElement(CharacteristicDto characteristic, string? normalizedValueType, bool hasCodedValue)
        {
            #region implementation

            // Render as CE if explicitly specified or if coded value exists with no type specified
            return normalizedValueType == CODED_ELEMENT_TYPE ||
                   (hasCodedValue && string.IsNullOrEmpty(normalizedValueType));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as physical quantity (PQ) based on business rules.
        /// Priority: explicit PQ type or quantity value present with no explicit type.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasQuantityValue">Pre-computed quantity value flag</param>
        /// <returns>True if should render as physical quantity</returns>
        /// <seealso cref="CharacteristicDto"/>
        public bool ShouldRenderAsPhysicalQuantity(CharacteristicDto characteristic, string? normalizedValueType, bool hasQuantityValue)
        {
            #region implementation

            // Render as PQ if explicitly specified or if quantity value exists with no type specified
            return normalizedValueType == PHYSICAL_QUANTITY_TYPE ||
                   (hasQuantityValue && string.IsNullOrEmpty(normalizedValueType));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as boolean (BL) based on business rules.
        /// Priority: explicit BL type or boolean value present with no explicit type.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasBooleanValue">Pre-computed boolean value flag</param>
        /// <returns>True if should render as boolean</returns>
        /// <seealso cref="CharacteristicDto"/>
        public bool ShouldRenderAsBoolean(CharacteristicDto characteristic, string? normalizedValueType, bool hasBooleanValue)
        {
            #region implementation

            // Render as BL if explicitly specified or if boolean value exists with no type specified
            return normalizedValueType == BOOLEAN_TYPE ||
                   (hasBooleanValue && string.IsNullOrEmpty(normalizedValueType));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as integer (INT) based on business rules.
        /// Priority: explicit INT type or integer value present with no explicit type.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasIntegerValue">Pre-computed integer value flag</param>
        /// <returns>True if should render as integer</returns>
        /// <seealso cref="CharacteristicDto"/>
        public bool ShouldRenderAsInteger(CharacteristicDto characteristic, string? normalizedValueType, bool hasIntegerValue)
        {
            #region implementation

            // Render as INT if explicitly specified or if integer value exists with no type specified
            return normalizedValueType == INTEGER_TYPE ||
                   (hasIntegerValue && string.IsNullOrEmpty(normalizedValueType));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should render as string (ST) based on business rules.
        /// Priority: explicit ST type or string value present with no explicit type.
        /// </summary>
        /// <param name="characteristic">The characteristic to evaluate</param>
        /// <param name="normalizedValueType">Pre-computed normalized value type</param>
        /// <param name="hasStringValue">Pre-computed string value flag</param>
        /// <returns>True if should render as string</returns>
        /// <seealso cref="CharacteristicDto"/>
        public bool ShouldRenderAsString(CharacteristicDto characteristic, string? normalizedValueType, bool hasStringValue)
        {
            #region implementation

            // Render as ST if explicitly specified or if string value exists with no type specified
            return normalizedValueType == STRING_TYPE ||
                   (hasStringValue && string.IsNullOrEmpty(normalizedValueType));

            #endregion
        }

        #endregion

        #region value formatting methods

        /**************************************************************/
        /// <summary>
        /// Formats boolean value for rendering as string representation.
        /// Converts boolean values to standardized "true"/"false" strings.
        /// </summary>
        /// <param name="characteristic">The characteristic containing boolean value</param>
        /// <returns>Formatted boolean string or null if no boolean value</returns>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        public string? FormatBooleanValue(CharacteristicDto characteristic)
        {
            #region implementation

            if (characteristic?.ValueBL == null)
                return null;

            return characteristic.ValueBL == true ? "true" : "false";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats quantity value for rendering with precision formatting.
        /// Uses G29 format for precise numeric representation matching original template logic.
        /// </summary>
        /// <param name="characteristic">The characteristic containing quantity value</param>
        /// <returns>Formatted quantity string or null if no quantity value</returns>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        public string? FormatQuantityValue(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValuePQ_Value?.ToString("G29");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats integer value for rendering with precision formatting.
        /// Uses G29 format for precise numeric representation matching original template logic.
        /// </summary>
        /// <param name="characteristic">The characteristic containing integer value</param>
        /// <returns>Formatted integer string or null if no integer value</returns>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        public string? FormatIntegerValue(CharacteristicDto characteristic)
        {
            #region implementation

            return characteristic?.ValueINT?.ToString("G29");

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic has any content available for rendering.
        /// Checks for presence of any value type that can be displayed.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for renderable content</param>
        /// <returns>True if any renderable content exists</returns>
        /// <seealso cref="CharacteristicDto"/>
        private static bool hasRenderableContent(CharacteristicDto characteristic)
        {
            #region implementation

            if (characteristic == null)
                return false;

            // Check if any value type has content
            return !string.IsNullOrEmpty(characteristic.ValueCV_Code) ||
                   characteristic.ValuePQ_Value.HasValue ||
                   characteristic.ValueBL != null ||
                   characteristic.ValueINT.HasValue ||
                   !string.IsNullOrEmpty(characteristic.ValueST);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if characteristic should display original text for coded values.
        /// Based on presence of display name for coded values matching original template logic.
        /// </summary>
        /// <param name="characteristic">The characteristic to check for original text display</param>
        /// <returns>True if original text should be displayed</returns>
        /// <seealso cref="CharacteristicDto.ValueCV_DisplayName"/>
        private static bool shouldDisplayOriginalText(CharacteristicDto characteristic)
        {
            #region implementation

            return !string.IsNullOrEmpty(characteristic?.ValueCV_DisplayName);

            #endregion
        }

        #endregion
    }
}