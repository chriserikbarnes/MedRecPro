using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Rendering
{
    /**************************************************************/
    /// <summary>
    /// Tests section hierarchy organization using fixture-derived sections.
    /// </summary>
    /// <seealso cref="SectionHierarchyService"/>
    /// <seealso cref="StructuredBodyDto"/>
    [TestClass]
    [TestCategory("Unit")]
    public class SplSectionHierarchyServiceTests
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
        /// Verifies OrganizeSections separates standalone and hierarchical fixture sections.
        /// </summary>
        /// <seealso cref="SectionHierarchyService.OrganizeSections"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFixtureDocument"/>
        [TestMethod]
        public void OrganizeSections_FixtureStructuredBody_ReturnsStandaloneAndRootSections()
        {
            #region implementation
            var structuredBody = SplRenderingFixtureHelper.LoadFixtureDocument().StructuredBodies.Single();
            var service = new SectionHierarchyService();

            var result = service.OrganizeSections(structuredBody);

            Assert.IsTrue(result.StandaloneSections.Count > 0);
            Assert.AreEqual(1, result.RootSections.Count);
            Assert.AreEqual(structuredBody.Sections[0].SectionID, result.RootSections[0].SectionID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies hierarchy helper methods validate sections and order children by sequence.
        /// </summary>
        /// <seealso cref="SectionHierarchyService.GetValidSections"/>
        /// <seealso cref="SectionHierarchyService.CreateSectionLookup"/>
        /// <seealso cref="SectionHierarchyService.BuildChildSections"/>
        [TestMethod]
        public void HierarchyHelpers_FixtureSections_ReturnExpectedLookupAndChildren()
        {
            #region implementation
            var structuredBody = SplRenderingFixtureHelper.LoadFixtureDocument().StructuredBodies.Single();
            var service = new SectionHierarchyService();

            var validSections = service.GetValidSections(structuredBody.Sections);
            var lookup = service.CreateSectionLookup(validSections);
            var children = service.BuildChildSections(
                structuredBody.Sections[0].SectionID!.Value,
                structuredBody.SectionHierarchies,
                lookup);

            Assert.AreEqual(structuredBody.Sections.Count, validSections.Count);
            Assert.AreEqual(structuredBody.Sections.Count, lookup.Count);
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual(structuredBody.Sections[2].SectionID, children[0].SectionID);
            Assert.AreEqual(structuredBody.Sections[1].SectionID, children[1].SectionID);
            #endregion
        }
    }
}
