using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests characteristic rendering value detection, formatting, and rendering decisions.
    /// </summary>
    /// <seealso cref="CharacteristicRenderingService"/>
    /// <seealso cref="CharacteristicDto"/>
    [TestClass]
    public class SplCharacteristicRenderingServiceTests
    {
        /**************************************************************/
        /// <summary>
        /// Initializes deterministic DTO decryption before each test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            SplRenderingFixtureHelper.InitializeUtil();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies PrepareForRendering computes quantity rendering flags and formatted values.
        /// </summary>
        /// <seealso cref="CharacteristicRenderingService.PrepareForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.CreateCharacteristic"/>
        [TestMethod]
        public void PrepareForRendering_QuantityCharacteristic_ComputesExpectedFlags()
        {
            #region implementation
            var characteristic = SplRenderingFixtureHelper.CreateCharacteristic(1);
            var service = new CharacteristicRenderingService();

            var result = service.PrepareForRendering(characteristic);

            Assert.AreEqual("PQ", result.NormalizedValueType);
            Assert.IsTrue(result.HasQuantityValue);
            Assert.IsTrue(result.ShouldRenderAsPhysicalQuantity);
            Assert.IsFalse(result.ShouldRenderAsCodedElement);
            Assert.AreEqual("12.5", result.FormattedQuantityValue);
            Assert.IsTrue(result.HasRenderableContent);
            Assert.IsTrue(result.ShouldRenderClassCode);
            Assert.IsTrue(service.ShouldRenderAsPhysicalQuantity(characteristic, "PQ", true));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies public characteristic helper methods handle all supported value kinds.
        /// </summary>
        /// <seealso cref="CharacteristicRenderingService.ShouldRenderAsCodedElement"/>
        /// <seealso cref="CharacteristicRenderingService.ShouldRenderAsBoolean"/>
        /// <seealso cref="CharacteristicRenderingService.ShouldRenderAsInteger"/>
        /// <seealso cref="CharacteristicRenderingService.ShouldRenderAsString"/>
        [TestMethod]
        public void CharacteristicHelpers_AllValueKinds_ReturnExpectedValues()
        {
            #region implementation
            var service = new CharacteristicRenderingService();
            var coded = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValueType)] = "ce", [nameof(CharacteristicDto.ValueCV_Code)] = "C42998" } };
            var boolean = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValueType)] = "BL", [nameof(CharacteristicDto.ValueBL)] = false } };
            var integer = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValueType)] = "INT", [nameof(CharacteristicDto.ValueINT)] = 7 } };
            var text = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValueType)] = "ST", [nameof(CharacteristicDto.ValueST)] = "capsule" } };
            var originalText = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.OriginalText)] = "Original capsule text" } };

            Assert.AreEqual("CE", service.GetNormalizedValueType(coded));
            Assert.IsTrue(service.HasCodedValue(coded));
            Assert.IsTrue(service.ShouldRenderAsCodedElement(coded, "CE", true));
            Assert.IsTrue(service.HasBooleanValue(boolean));
            Assert.IsTrue(service.ShouldRenderAsBoolean(boolean, "BL", true));
            Assert.AreEqual("false", service.FormatBooleanValue(boolean));
            Assert.IsTrue(service.HasIntegerValue(integer));
            Assert.IsTrue(service.ShouldRenderAsInteger(integer, "INT", true));
            Assert.AreEqual("7", service.FormatIntegerValue(integer));
            Assert.IsTrue(service.HasStringValue(text));
            Assert.IsTrue(service.HasOriginalText(originalText));
            Assert.IsTrue(service.ShouldRenderAsString(text, "ST", true));
            Assert.IsNull(service.FormatBooleanValue(text));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies FormatQuantityValue renders the physical quantity with G29
        /// precision (trailing zeros trimmed) and returns null when no
        /// quantity value exists.
        /// </summary>
        /// <seealso cref="CharacteristicRenderingService.FormatQuantityValue"/>
        /// <seealso cref="CharacteristicDto.ValuePQ_Value"/>
        [TestMethod]
        public void FormatQuantityValue_QuantityAndMissingValues_FormatWithG29OrNull()
        {
            #region implementation
            // Arrange
            var service = new CharacteristicRenderingService();
            var quantity = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValuePQ_Value)] = 12.50m } };
            var wholeNumber = new CharacteristicDto { Characteristic = new Dictionary<string, object?> { [nameof(CharacteristicDto.ValuePQ_Value)] = 100m } };
            var noQuantity = new CharacteristicDto { Characteristic = new Dictionary<string, object?>() };

            // Act + Assert - G29 trims trailing zeros; missing values yield null.
            Assert.AreEqual("12.5", service.FormatQuantityValue(quantity));
            Assert.AreEqual("100", service.FormatQuantityValue(wholeNumber));
            Assert.IsNull(service.FormatQuantityValue(noQuantity));
            Assert.IsNull(service.FormatQuantityValue(null!));
            #endregion
        }
    }
}
