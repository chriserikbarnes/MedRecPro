using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests FDA product concept helper public methods.
    /// </summary>
    /// <seealso cref="FdaProductConceptHelper"/>
    [TestClass]
    public class FdaProductConceptHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies product concept code generation is deterministic and order-insensitive.
        /// </summary>
        /// <seealso cref="FdaProductConceptHelper.GenerateProductConceptCode"/>
        /// <seealso cref="FdaProductConceptHelper.ValidateConceptCodeFormat"/>
        [TestMethod]
        public void GenerateProductConceptCode_UnsortedIngredients_ReturnsStableMd5Format()
        {
            #region implementation
            var ingredients = new List<FdaProductConceptHelper.ActiveIngredient>
            {
                new()
                {
                    UniiCode = "B-ING",
                    StrengthNumerator = 1,
                    StrengthNumeratorUnit = "g",
                    StrengthDenominator = 1,
                    StrengthDenominatorUnit = "1"
                },
                new()
                {
                    UniiCode = "A-ING",
                    MoietyUniiCode = "A-MOIETY",
                    StrengthNumerator = 500,
                    StrengthNumeratorUnit = "mg",
                    StrengthDenominator = 5,
                    StrengthDenominatorUnit = "mL"
                }
            };
            var reversed = ingredients.AsEnumerable().Reverse().ToList();

            var first = FdaProductConceptHelper.GenerateProductConceptCode("C42972", ingredients);
            var second = FdaProductConceptHelper.GenerateProductConceptCode("C42972", reversed);

            Assert.AreEqual(first, second);
            Assert.IsTrue(FdaProductConceptHelper.ValidateConceptCodeFormat(first));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies kit concept code generation is deterministic and order-insensitive.
        /// </summary>
        /// <seealso cref="FdaProductConceptHelper.GenerateKitConceptCode"/>
        /// <seealso cref="FdaProductConceptHelper.ValidateConceptCodeFormat"/>
        [TestMethod]
        public void GenerateKitConceptCode_UnsortedParts_ReturnsStableMd5Format()
        {
            #region implementation
            var parts = new List<FdaProductConceptHelper.KitPart>
            {
                new()
                {
                    ProductConceptCode = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                    QuantityNumerator = 1,
                    QuantityNumeratorUnit = "g"
                },
                new()
                {
                    ProductConceptCode = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    QuantityNumerator = 2,
                    QuantityNumeratorUnit = "mL"
                }
            };
            var reversed = parts.AsEnumerable().Reverse().ToList();

            var first = FdaProductConceptHelper.GenerateKitConceptCode(parts);
            var second = FdaProductConceptHelper.GenerateKitConceptCode(reversed);

            Assert.AreEqual(first, second);
            Assert.IsTrue(FdaProductConceptHelper.ValidateConceptCodeFormat(first));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies concept-code validation rejects malformed and empty values.
        /// </summary>
        /// <seealso cref="FdaProductConceptHelper.ValidateConceptCodeFormat"/>
        [TestMethod]
        public void ValidateConceptCodeFormat_ValidAndInvalidValues_ReturnsExpectedBooleans()
        {
            #region implementation
            Assert.IsTrue(FdaProductConceptHelper.ValidateConceptCodeFormat("7fead104-1147-b435-1545-606b40a2cd6b"));
            Assert.IsFalse(FdaProductConceptHelper.ValidateConceptCodeFormat(""));
            Assert.IsFalse(FdaProductConceptHelper.ValidateConceptCodeFormat("not-a-code"));
            Assert.IsFalse(FdaProductConceptHelper.ValidateConceptCodeFormat("7fead1041147b4351545606b40a2cd6b"));
            #endregion
        }

        #endregion
    }
}
