using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public ErrorHelper behavior using unique messages.
    /// </summary>
    /// <seealso cref="ErrorHelper"/>
    [TestClass]
    public class ErrorHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies AddErrorMsg stores encoded messages that IsErrorLogged can find.
        /// </summary>
        /// <seealso cref="ErrorHelper.AddErrorMsg"/>
        /// <seealso cref="ErrorHelper.GetErrorMsg"/>
        /// <seealso cref="ErrorHelper.IsErrorLogged"/>
        [TestMethod]
        public void AddErrorMsg_GetErrorMsg_IsErrorLogged_UniqueMessage_IsRetrievable()
        {
            #region implementation
            var uniquePrefix = $"ErrorHelperTests-{Guid.NewGuid():N}";
            var uniqueMessage = $"{uniquePrefix}<tag>";

            ErrorHelper.AddErrorMsg(uniqueMessage);
            var messages = ErrorHelper.GetErrorMsg();

            Assert.IsNotNull(messages);
            Assert.IsTrue(messages.Any(message => message.Contains(uniquePrefix)));
            Assert.IsTrue(ErrorHelper.IsErrorLogged(uniquePrefix));
            Assert.IsFalse(ErrorHelper.IsErrorLogged($"missing-{Guid.NewGuid():N}"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetErrorMsg returns null for empty explicit usernames.
        /// </summary>
        /// <seealso cref="ErrorHelper.GetErrorMsg(string)"/>
        [TestMethod]
        public void GetErrorMsg_EmptyUserName_ReturnsNull()
        {
            #region implementation
            Assert.IsNull(ErrorHelper.GetErrorMsg(string.Empty));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetLineNumber returns a sentinel when no debug line is available.
        /// </summary>
        /// <seealso cref="ErrorHelper.GetLineNumber"/>
        [TestMethod]
        public void GetLineNumber_ExceptionWithoutFileInfo_ReturnsNonPositiveLine()
        {
            #region implementation
            var exception = new InvalidOperationException("test");

            var lineNumber = ErrorHelper.GetLineNumber(exception);

            Assert.IsTrue(lineNumber <= 0);
            #endregion
        }

        #endregion
    }
}
