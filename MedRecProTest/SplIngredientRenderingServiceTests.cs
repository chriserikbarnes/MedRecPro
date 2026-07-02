using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests SPL ingredient rendering behavior using fixture-derived product data.
    /// </summary>
    /// <seealso cref="IngredientRenderingService"/>
    /// <seealso cref="IngredientDto"/>
    [TestClass]
    public class SplIngredientRenderingServiceTests
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
        /// Verifies PrepareForRendering computes active ingredient, quantity, substance, and ordering flags.
        /// </summary>
        /// <seealso cref="IngredientRenderingService.PrepareForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFirstProduct"/>
        [TestMethod]
        public void PrepareForRendering_FixtureIngredient_ComputesExpectedFlags()
        {
            #region implementation
            var product = SplRenderingFixtureHelper.LoadFirstProduct();
            var ingredient = product.Ingredients.First();
            var service = new IngredientRenderingService();

            var result = service.PrepareForRendering(ingredient, product);

            Assert.AreEqual(ingredient, result.IngredientDto);
            Assert.IsTrue(result.IsActiveIngredient);
            Assert.IsTrue(result.HasQuantity);
            Assert.IsTrue(result.HasSubstance);
            Assert.AreEqual("classCode=\"ACTIM\"", result.ClassCodeAttribute);
            Assert.AreEqual("2.5", result.FormattedQuantityNumerator);
            Assert.AreEqual("1", result.FormattedQuantityDenominator);
            Assert.IsTrue(result.HasSpecifiedSubstances);
            Assert.IsTrue(result.HasActiveMoieties);
            Assert.IsFalse(result.RequiresReferenceSubstance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies public ingredient helper methods handle active, inactive, and reference-basis cases.
        /// </summary>
        /// <seealso cref="IngredientRenderingService.IsActiveIngredient"/>
        /// <seealso cref="IngredientRenderingService.RequiresReferenceSubstance"/>
        /// <seealso cref="IngredientRenderingService.GetPrimaryReferenceSubstance"/>
        [TestMethod]
        public void IngredientHelpers_CommonCases_ReturnExpectedValues()
        {
            #region implementation
            var product = SplRenderingFixtureHelper.LoadFirstProduct();
            var ingredient = product.Ingredients.First();
            var service = new IngredientRenderingService();

            var referenceIngredient = new IngredientDto
            {
                Ingredient = new Dictionary<string, object?>
                {
                    [nameof(IngredientDto.ClassCode)] = Constant.ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE
                },
                ReferenceSubstances = ingredient.ReferenceSubstances
            };
            var translatedIngredient = new IngredientDto
            {
                Ingredient = new Dictionary<string, object?>
                {
                    [nameof(IngredientDto.NumeratorTranslationCode)] = "C28253",
                    [nameof(IngredientDto.DenominatorTranslationCode)] = "C48542"
                }
            };

            Assert.AreEqual(string.Empty, service.GenerateClassCodeAttribute(null));
            Assert.IsTrue(service.IsActiveIngredient(ingredient));
            Assert.IsFalse(service.IsActiveIngredient(null));
            Assert.IsTrue(service.HasQuantityData(ingredient));
            Assert.IsTrue(service.HasSubstanceData(ingredient));
            Assert.AreEqual("AMLODIPINE", service.FormatSubstanceName("AMamp;LODIPINE"));
            Assert.IsTrue(service.HasNumeratorTranslation(translatedIngredient));
            Assert.IsTrue(service.HasDenominatorTranslation(translatedIngredient));
            Assert.IsNotNull(service.GetOrderedSpecifiedSubstances(ingredient));
            Assert.IsNotNull(service.GetOrderedActiveMoieties(ingredient));
            Assert.IsTrue(service.RequiresReferenceSubstance(referenceIngredient));
            Assert.AreEqual("AMLODIPINE", service.GetPrimaryReferenceSubstance(referenceIngredient)!.RefSubstanceName);
            #endregion
        }
    }
}
