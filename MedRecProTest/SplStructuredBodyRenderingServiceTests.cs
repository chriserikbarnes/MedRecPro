using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests structured body helper behavior with fixture section organization data.
    /// </summary>
    /// <seealso cref="StructuredBodyService"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    [TestClass]
    public class SplStructuredBodyRenderingServiceTests
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
        /// Verifies structured body helpers detect standalone and hierarchical section groups.
        /// </summary>
        /// <seealso cref="StructuredBodyService.HasStandaloneSections"/>
        /// <seealso cref="StructuredBodyService.HasHierarchicalSections"/>
        /// <seealso cref="StructuredBodyService.GetSectionHierarchies"/>
        [TestMethod]
        public void StructuredBodyHelpers_FixtureOrganization_ReturnExpectedValues()
        {
            #region implementation
            var structuredBody = SplRenderingFixtureHelper.LoadFixtureDocument().StructuredBodies.Single();
            var organized = new SectionHierarchyService().OrganizeSections(structuredBody);
            var service = new StructuredBodyService();

            Assert.IsTrue(service.HasStandaloneSections(organized));
            Assert.IsTrue(service.HasHierarchicalSections(organized));
            Assert.AreEqual(2, service.GetSectionHierarchies(structuredBody).Count);
            Assert.AreEqual(0, service.GetSectionHierarchies(new StructuredBodyDto { StructuredBody = new Dictionary<string, object?>() }).Count);
            #endregion
        }
    }
}
