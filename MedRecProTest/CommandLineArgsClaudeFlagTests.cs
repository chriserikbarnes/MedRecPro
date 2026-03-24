using MedRecProConsole.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="CommandLineArgs"/> parsing of the --no-claude flag.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - --no-claude flag with --standardize-tables operations
    /// - --no-claude without --standardize-tables produces error
    /// - Default value is false
    /// </remarks>
    /// <seealso cref="CommandLineArgs"/>
    [TestClass]
    public class CommandLineArgsClaudeFlagTests
    {
        #region --no-claude Tests

        /**************************************************************/
        /// <summary>
        /// --no-claude with --standardize-tables sets NoClaude to true.
        /// </summary>
        [TestMethod]
        public void Parse_NoClaude_WithStandardize_SetsFlag()
        {
            #region implementation

            var result = CommandLineArgs.Parse(new[]
            {
                "--standardize-tables", "parse-single",
                "--table-id", "123",
                "--no-claude"
            });

            Assert.IsTrue(result.NoClaude);
            Assert.IsFalse(result.HasErrors);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// --no-claude without --standardize-tables produces a validation error.
        /// </summary>
        [TestMethod]
        public void Parse_NoClaude_WithoutStandardize_ProducesError()
        {
            #region implementation

            var result = CommandLineArgs.Parse(new[] { "--no-claude" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("--no-claude")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Default NoClaude value is false.
        /// </summary>
        [TestMethod]
        public void Parse_Default_NoClaudeIsFalse()
        {
            #region implementation

            var result = CommandLineArgs.Parse(new[]
            {
                "--standardize-tables", "parse-single",
                "--table-id", "456"
            });

            Assert.IsFalse(result.NoClaude);
            Assert.IsFalse(result.HasErrors);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// --no-claude works with parse operation (batch mode).
        /// </summary>
        [TestMethod]
        public void Parse_NoClaude_WithParse_SetsFlag()
        {
            #region implementation

            var result = CommandLineArgs.Parse(new[]
            {
                "--standardize-tables", "parse",
                "--no-claude"
            });

            Assert.IsTrue(result.NoClaude);
            Assert.IsFalse(result.HasErrors);

            #endregion
        }

        #endregion --no-claude Tests
    }
}
