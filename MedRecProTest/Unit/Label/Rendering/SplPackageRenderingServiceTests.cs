using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Rendering
{
    /**************************************************************/
    /// <summary>
    /// Tests SPL package rendering behavior using fixture-derived product data.
    /// </summary>
    /// <seealso cref="PackageRenderingService"/>
    /// <seealso cref="PackagingLevelDto"/>
    [TestClass]
    [TestCategory("Unit")]
    public class SplPackageRenderingServiceTests
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
        /// Verifies PrepareForRendering computes fixture package identifiers, quantities, marketing, and characteristics.
        /// </summary>
        /// <seealso cref="PackageRenderingService.PrepareForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFirstProduct"/>
        [TestMethod]
        public void PrepareForRendering_FixturePackage_ReturnsExpectedRenderingContext()
        {
            #region implementation
            var product = SplRenderingFixtureHelper.LoadFirstProduct();
            var package = product.PackagingLevels.First();
            var service = new PackageRenderingService(new CharacteristicRenderingService());

            var result = service.PrepareForRendering(package, product);

            Assert.AreEqual(package, result.PackagingLevelDto);
            Assert.IsTrue(result.HasValidData);
            Assert.IsTrue(result.HasPackageIdentifiers);
            Assert.IsTrue(result.HasMarketingAct);
            Assert.IsTrue(result.HasCharacteristics);
            Assert.IsTrue(result.HasCharacteristicRendering);
            Assert.AreEqual("68180_755_01", result.DisplayAttributes);
            Assert.AreEqual("100", result.FormattedQuantityNumerator);
            Assert.AreEqual("1", result.FormattedQuantityDenominator);
            Assert.AreEqual("C43169", result.PackageFormCode);
            Assert.AreEqual("C53292", result.OrderedMarketingStatuses!.Single().MarketingActCode);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies public package helper methods handle ordering, filtering, and empty values.
        /// </summary>
        /// <seealso cref="PackageRenderingService.GetOrderedPackageIdentifiers"/>
        /// <seealso cref="PackageRenderingService.GetOrderedChildPackaging"/>
        /// <seealso cref="PackageRenderingService.GetOrderedCharacteristicsForPackaging"/>
        [TestMethod]
        public void PackageHelpers_CommonCases_ReturnExpectedValues()
        {
            #region implementation
            var product = SplRenderingFixtureHelper.LoadFirstProduct();
            var package = product.PackagingLevels.First();
            var childPackage = product.PackagingLevels.Skip(1).First();
            var service = new PackageRenderingService();

            package.PackagingHierarchy.Add(new PackagingHierarchyDto
            {
                ChildPackagingLevel = childPackage,
                PackagingHierarchy = new Dictionary<string, object?>
                {
                    ["EncryptedPackagingHierarchyID"] = SplRenderingFixtureHelper.EncryptedId(900),
                    ["EncryptedOuterPackagingLevelID"] = SplRenderingFixtureHelper.EncryptedId(package.PackagingLevelID!.Value),
                    ["EncryptedInnerPackagingLevelID"] = SplRenderingFixtureHelper.EncryptedId(childPackage.PackagingLevelID!.Value),
                    [nameof(PackagingHierarchyDto.SequenceNumber)] = 1
                }
            });

            Assert.AreEqual(string.Empty, service.GenerateDisplayAttributes(null!));
            Assert.IsTrue(service.HasValidData(package));
            Assert.AreEqual("100", service.FormatQuantity(100.000m));
            Assert.AreEqual(string.Empty, service.FormatQuantity(null));
            Assert.AreEqual(package.PackageIdentifiers.Count, service.GetOrderedPackageIdentifiers(package)!.Count);
            Assert.AreEqual(1, service.GetOrderedChildPackaging(package)!.Count);
            Assert.AreEqual(1, service.GetOrderedMarketingStatusesForPackage(product, package.PackagingLevelID)!.Count);
            Assert.AreEqual(1, service.GetOrderedCharacteristicsForPackaging(product, package.PackagingLevelID)!.Count);
            Assert.IsNull(service.GetOrderedCharacteristicsForPackaging(product, null));
            #endregion
        }
    }
}
