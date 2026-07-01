using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests the public EF Core database function surface on ApplicationDbContext.
    /// </summary>
    /// <seealso cref="ApplicationDbContext"/>
    [TestClass]
    public class ApplicationDbContextFunctionTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies Soundex and Difference throw when called outside query translation.
        /// </summary>
        /// <seealso cref="ApplicationDbContext.Soundex"/>
        /// <seealso cref="ApplicationDbContext.Difference"/>
        [TestMethod]
        public void Soundex_Difference_DirectInvocation_ThrowsNotSupported()
        {
            #region implementation
            Assert.ThrowsException<NotSupportedException>(() => ApplicationDbContext.Soundex("Smith"));
            Assert.ThrowsException<NotSupportedException>(() => ApplicationDbContext.Difference("Smith", "Smyth"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies Soundex and Difference are registered as built-in EF functions.
        /// </summary>
        /// <seealso cref="ApplicationDbContext.Soundex"/>
        /// <seealso cref="ApplicationDbContext.Difference"/>
        [TestMethod]
        public void Soundex_Difference_ModelMetadata_RegistersBuiltInFunctions()
        {
            #region implementation
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ApplicationDbContextFunctionTests_{Guid.NewGuid():N}")
                .Options;

            using var context = new ApplicationDbContext(options);
            var soundexMethod = typeof(ApplicationDbContext).GetMethod(nameof(ApplicationDbContext.Soundex))!;
            var differenceMethod = typeof(ApplicationDbContext).GetMethod(nameof(ApplicationDbContext.Difference))!;

            var soundexFunction = context.Model.FindDbFunction(soundexMethod);
            var differenceFunction = context.Model.FindDbFunction(differenceMethod);

            Assert.IsNotNull(soundexFunction);
            Assert.IsNotNull(differenceFunction);
            Assert.AreEqual("SOUNDEX", soundexFunction.Name);
            Assert.AreEqual("DIFFERENCE", differenceFunction.Name);
            Assert.IsTrue(soundexFunction.IsBuiltIn);
            Assert.IsTrue(differenceFunction.IsBuiltIn);
            #endregion
        }

        #endregion
    }
}
