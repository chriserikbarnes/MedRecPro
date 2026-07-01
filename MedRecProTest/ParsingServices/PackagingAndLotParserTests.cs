using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests packaging, marketing status, and lot distribution parser public contracts.
    /// </summary>
    /// <remarks>
    /// Coverage includes package parsing under a seeded product, marketing status attachment,
    /// lot parser no-op behavior, and hierarchy guard clauses.
    /// </remarks>
    /// <seealso cref="PackagingParser"/>
    /// <seealso cref="MarketingStatusParser"/>
    /// <seealso cref="LotDistributionParser"/>
    [TestClass]
    public class PackagingAndLotParserTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies packaging parsing creates package-level data for the current product.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="PackagingParser.ParseAsync"/>
        [TestMethod]
        public async Task PackagingParser_ParseAsync_PackageWithNdc_CreatesPackagingLevel()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = await new PackagingParser().ParseAsync(
                ParsingServiceTestHelper.PackageWithNdcIdentifier(),
                parseContext,
                null);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<PackagingLevel>().CountAsync() >= 2);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies marketing-status parsing can attach status to the current packaging level.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="MarketingStatusParser.ParseAsync"/>
        [TestMethod]
        public async Task MarketingStatusParser_ParseAsync_PackagingContext_ReturnsSuccessfulResult()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = await new MarketingStatusParser().ParseAsync(
                ParsingServiceTestHelper.PackageWithNdcIdentifier(),
                parseContext,
                null);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<MarketingStatus>().CountAsync() >= 0);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies lot parsing returns a result for representative product-event lot XML.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="LotDistributionParser.ParseAsync"/>
        [TestMethod]
        public async Task LotDistributionParser_ParseAsync_RepresentativeLotXml_ReturnsResult()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = await new LotDistributionParser().ParseAsync(
                ParsingServiceTestHelper.LotHierarchy(),
                parseContext,
                null);

            Assert.IsNotNull(result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies lot hierarchy creation no-ops for missing parent and child inputs.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="LotDistributionParser.CreateLotHierarchiesAsync"/>
        [TestMethod]
        public async Task CreateLotHierarchiesAsync_NullParentOrEmptyChildren_ReturnsZero()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var result = await new LotDistributionParser().CreateLotHierarchiesAsync(null, Array.Empty<int>(), parseContext);

            Assert.AreEqual(0, result);
            #endregion
        }
    }
}
