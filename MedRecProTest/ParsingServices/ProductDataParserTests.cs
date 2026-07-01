using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests product, ingredient, marketing, identity, characteristic, extension, and relationship parser contracts.
    /// </summary>
    /// <remarks>
    /// The tests use seeded parser context rows and focused product XML snippets to exercise public
    /// entry points without depending on private parser helpers.
    /// </remarks>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="IngredientParser"/>
    [TestClass]
    public class ProductDataParserTests
    {
        #region Product Parser Tests

        /**************************************************************/
        /// <summary>
        /// Verifies manufactured-product parsing requires a current section.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ManufacturedProductParser.ParseAsync"/>
        [TestMethod]
        public async Task ManufacturedProductParser_ParseAsync_NoCurrentSection_ReturnsFailure()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var result = await new ManufacturedProductParser().ParseAsync(
                ParsingServiceTestHelper.MinimalManufacturedProduct(),
                parseContext,
                null);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Errors.Single(), "section context");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies manufactured-product parsing creates a product and restores prior context.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ManufacturedProductParser.ParseAsync"/>
        [TestMethod]
        public async Task ManufacturedProductParser_ParseAsync_MinimalProduct_CreatesProductAndRestoresContext()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);

            var result = await new ManufacturedProductParser().ParseAsync(
                ParsingServiceTestHelper.MinimalManufacturedProduct(),
                parseContext,
                null);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<Product>().AnyAsync(x => x.ProductName == "CIPROFLOXACIN"));
            Assert.AreSame(seed.Product, parseContext.CurrentProduct);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ingredient parsing creates active and inactive ingredient records for the current product.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="IngredientParser.ParseAsync"/>
        [TestMethod]
        public async Task IngredientParser_ParseAsync_ActiveAndInactiveIngredients_CreatesIngredientRows()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = await new IngredientParser().ParseAsync(
                ParsingServiceTestHelper.ProductWithIngredients(),
                parseContext,
                null);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<Ingredient>().CountAsync() >= 2);
            #endregion
        }

        #endregion

        #region Focused Entry Point Tests

        /**************************************************************/
        /// <summary>
        /// Verifies product identity, marketing, characteristic, extension, and relationship parsers accept valid context.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ProductIdentityParser.ParseAsync"/>
        /// <seealso cref="ProductMarketingParser.ParseAsync"/>
        /// <seealso cref="ProductCharacteristicsParser.ParseAsync"/>
        /// <seealso cref="ProductExtensionParser.ParseAsync"/>
        /// <seealso cref="ProductRelationshipParser.ParseAsync"/>
        [TestMethod]
        public async Task ProductFocusedParsers_ParseAsync_SeededContext_ReturnResultsWithoutThrowing()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            var productElement = ParsingServiceTestHelper.MinimalManufacturedProduct();
            var marketingElement = ParsingServiceTestHelper.ProductMarketingCategory();
            var results = new[]
            {
                await new ProductIdentityParser().ParseAsync(productElement, parseContext, null),
                await new ProductMarketingParser().ParseAsync(marketingElement, parseContext, null),
                await new ProductCharacteristicsParser().ParseAsync(productElement, parseContext, null),
                await new ProductExtensionParser().ParseAsync(productElement, parseContext, null),
                await new ProductRelationshipParser(new ManufacturedProductParser()).ParseAsync(productElement, parseContext, null)
            };

            Assert.IsTrue(results.All(result => result != null));
            Assert.IsTrue(results.All(result => result.Errors.Count == 0), string.Join(Environment.NewLine, results.SelectMany(result => result.Errors)));
            #endregion
        }

        #endregion

        #region Validation Service Tests

        /**************************************************************/
        /// <summary>
        /// Verifies cosmetic specialized-kind validation rejects mutually exclusive category pairs.
        /// </summary>
        /// <seealso cref="SpecializedKindValidatorService.ValidateCosmeticCategoryRules"/>
        [TestMethod]
        public void ValidateCosmeticCategoryRules_MutuallyExclusivePair_RejectsOneKind()
        {
            #region implementation
            var kinds = new[]
            {
                new SpecializedKind { KindCode = "01D1", KindCodeSystem = "2.16.840.1.113883.6.303" },
                new SpecializedKind { KindCode = "01D2", KindCodeSystem = "2.16.840.1.113883.6.303" }
            };

            var result = SpecializedKindValidatorService.ValidateCosmeticCategoryRules(
                kinds,
                "34391-3",
                NullLogger.Instance,
                out var rejectedKinds);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, rejectedKinds.Count);
            Assert.AreEqual("01D2", rejectedKinds.Single().Kind.KindCode);
            #endregion
        }

        #endregion
    }
}
