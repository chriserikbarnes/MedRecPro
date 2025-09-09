using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering characteristics with pre-computed properties.
    /// Provides characteristic data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="CharacteristicDto"/>
    public class CharacteristicRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The characteristic to be rendered.
        /// </summary>
        /// <seealso cref="CharacteristicDto"/>
        public required CharacteristicDto CharacteristicDto { get; set; }

        #endregion

        #region pre-computed value type properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed normalized value type for rendering logic.
        /// Converted to uppercase for consistent comparison operations.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueType"/>
        public string? NormalizedValueType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has a coded value.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueCV_Code"/>
        public bool HasCodedValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has a quantity value.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        public bool HasQuantityValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has a boolean value.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        public bool HasBooleanValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has an integer value.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        public bool HasIntegerValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has a string value.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueST"/>
        public bool HasStringValue { get; set; }

        #endregion

        #region pre-computed rendering logic flags

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as coded element (CE) value type.
        /// Based on explicit value type or presence of coded value when no type specified.
        /// </summary>
        /// <seealso cref="NormalizedValueType"/>
        /// <seealso cref="HasCodedValue"/>
        public bool ShouldRenderAsCodedElement { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as physical quantity (PQ) value type.
        /// Based on explicit value type or presence of quantity value when no type specified.
        /// </summary>
        /// <seealso cref="NormalizedValueType"/>
        /// <seealso cref="HasQuantityValue"/>
        public bool ShouldRenderAsPhysicalQuantity { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as boolean (BL) value type.
        /// Based on explicit value type or presence of boolean value when no type specified.
        /// </summary>
        /// <seealso cref="NormalizedValueType"/>
        /// <seealso cref="HasBooleanValue"/>
        public bool ShouldRenderAsBoolean { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as integer (INT) value type.
        /// Based on explicit value type or presence of integer value when no type specified.
        /// </summary>
        /// <seealso cref="NormalizedValueType"/>
        /// <seealso cref="HasIntegerValue"/>
        public bool ShouldRenderAsInteger { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as string (ST) value type.
        /// Based on explicit value type or presence of string value when no type specified.
        /// </summary>
        /// <seealso cref="NormalizedValueType"/>
        /// <seealso cref="HasStringValue"/>
        public bool ShouldRenderAsString { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as fallback coded value.
        /// Used when no explicit type matches but coded value exists.
        /// </summary>
        /// <seealso cref="HasCodedValue"/>
        public bool ShouldRenderAsFallbackCoded { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether to render as fallback string value.
        /// Used when no other rendering options apply but string value exists.
        /// </summary>
        /// <seealso cref="HasStringValue"/>
        public bool ShouldRenderAsFallbackString { get; set; }

        #endregion

        #region pre-computed value properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted boolean value string for rendering.
        /// Converts boolean value to "true" or "false" string representation.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueBL"/>
        public string? FormattedBooleanValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted quantity value string for rendering.
        /// Formatted using G29 format for precise numeric representation.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        public string? FormattedQuantityValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted integer value string for rendering.
        /// Formatted using G29 format for precise numeric representation.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueINT"/>
        public string? FormattedIntegerValue { get; set; }

        #endregion

        #region pre-computed validation flags

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic has any renderable content.
        /// </summary>
        public bool HasRenderableContent { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this characteristic should display original text.
        /// Based on presence of coded value display name.
        /// </summary>
        /// <seealso cref="CharacteristicDto.ValueCV_DisplayName"/>
        public bool ShouldDisplayOriginalText { get; set; }

        #endregion
    }
}