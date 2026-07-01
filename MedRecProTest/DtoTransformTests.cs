using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public DTO transformation helper methods.
    /// </summary>
    /// <seealso cref="DtoTransform"/>
    [TestClass]
    public class DtoTransformTests
    {
        #region implementation

        private const string TestSecret = "DtoTransformTests-Fixed-Secret";

        /**************************************************************/
        /// <summary>
        /// Verifies ToEntityWithEncryptedId encrypts primary and foreign keys and omits raw IDs.
        /// </summary>
        /// <seealso cref="DtoTransform.ToEntityWithEncryptedId"/>
        [TestMethod]
        public void ToEntityWithEncryptedId_EntityWithKeys_ReturnsEncryptedKeyDictionary()
        {
            #region implementation
            var hierarchy = new SectionHierarchy
            {
                SectionHierarchyID = 10,
                ParentSectionID = 20,
                ChildSectionID = 30,
                SequenceNumber = 1
            };

            var result = hierarchy.ToEntityWithEncryptedId(TestSecret, NullLogger.Instance);

            Assert.IsFalse(result.ContainsKey(nameof(SectionHierarchy.SectionHierarchyID)));
            Assert.IsFalse(result.ContainsKey(nameof(SectionHierarchy.ParentSectionID)));
            Assert.AreEqual("10", result["EncryptedSectionHierarchyID"]?.ToString()?.Decrypt(TestSecret));
            Assert.AreEqual("20", result["EncryptedParentSectionID"]?.ToString()?.Decrypt(TestSecret));
            Assert.AreEqual("30", result["EncryptedChildSectionID"]?.ToString()?.Decrypt(TestSecret));
            Assert.AreEqual(1, result[nameof(SectionHierarchy.SequenceNumber)]);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ToEntityMenu returns sorted public nested class names for Label.
        /// </summary>
        /// <seealso cref="DtoTransform.ToEntityMenu"/>
        [TestMethod]
        public void ToEntityMenu_Label_ReturnsSortedNestedTypeNames()
        {
            #region implementation
            var result = new Label().ToEntityMenu(NullLogger.Instance);

            Assert.IsTrue(result.Count > 0);
            CollectionAssert.Contains(result, nameof(Document));
            CollectionAssert.Contains(result, nameof(Organization));
            CollectionAssert.AreEqual(result.OrderBy(x => x).ToList(), result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetClassDocumentation returns reflection-based property metadata.
        /// </summary>
        /// <seealso cref="DtoTransform.GetClassDocumentation"/>
        [TestMethod]
        public void GetClassDocumentation_KnownType_ReturnsClassAndPropertyMetadata()
        {
            #region implementation
            var result = DtoTransform.GetClassDocumentation(typeof(UserFacingUpdateDto), NullLogger.Instance);

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(UserFacingUpdateDto), result.Name);
            Assert.IsTrue(result.Properties.Any(property => property.Name == nameof(UserFacingUpdateDto.DisplayName)));
            Assert.IsTrue(result.Properties.Any(property => property.Name == nameof(UserFacingUpdateDto.MfaEnabled) && !property.IsNullable));
            #endregion
        }

        #endregion
    }
}
