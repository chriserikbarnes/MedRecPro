using MedRecProConsole.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="CommandLineArgs"/> parsing of --standardize-tables,
    /// --batch-size, and --table-id arguments.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Valid operation values (parse, validate, truncate, parse-single)
    /// - Invalid operation values
    /// - Batch size range validation
    /// - Table ID parsing
    /// - Mutual exclusion with --folder and --orange-book
    /// - Required combinations (parse-single + table-id)
    /// - IsStandardizeTablesMode computed property
    /// </remarks>
    /// <seealso cref="CommandLineArgs"/>
    [TestClass]
    public class CommandLineArgsStandardizeTablesTests
    {
        #region Operation Parsing Tests

        /**************************************************************/
        /// <summary>
        /// --standardize-tables parse sets StandardizeTablesOperation to "parse".
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesParse_SetsOperation()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse" });

            Assert.AreEqual("parse", result.StandardizeTablesOperation);
            Assert.IsTrue(result.IsStandardizeTablesMode);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables validate sets StandardizeTablesOperation to "validate".
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesValidate_SetsOperation()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "validate" });

            Assert.AreEqual("validate", result.StandardizeTablesOperation);
            Assert.IsTrue(result.IsStandardizeTablesMode);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables truncate sets StandardizeTablesOperation to "truncate".
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesTruncate_SetsOperation()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "truncate" });

            Assert.AreEqual("truncate", result.StandardizeTablesOperation);
            Assert.IsTrue(result.IsStandardizeTablesMode);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables parse-single with --table-id sets both properties.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesParseSingle_WithTableId_SetsProperties()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse-single", "--table-id", "12345" });

            Assert.AreEqual("parse-single", result.StandardizeTablesOperation);
            Assert.AreEqual(12345, result.StandardizeTableId);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables=parse (equals syntax) sets operation correctly.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesEqualsSyntax_SetsOperation()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables=parse" });

            Assert.AreEqual("parse", result.StandardizeTablesOperation);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// Operation values are normalized to lowercase.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesUppercase_NormalizesToLowercase()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "PARSE" });

            Assert.AreEqual("parse", result.StandardizeTablesOperation);
            Assert.IsFalse(result.HasErrors);
        }

        #endregion

        #region Invalid Operation Tests

        /**************************************************************/
        /// <summary>
        /// Invalid operation name produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesInvalidOperation_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "invalid-op" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("must be one of")));
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables without a value produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesNoValue_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("requires a value")));
        }

        #endregion

        #region IsStandardizeTablesMode Tests

        /**************************************************************/
        /// <summary>
        /// IsStandardizeTablesMode is false when no --standardize-tables is specified.
        /// </summary>
        [TestMethod]
        public void Parse_NoStandardizeTables_IsStandardizeTablesMode_False()
        {
            var result = CommandLineArgs.Parse(Array.Empty<string>());

            Assert.IsFalse(result.IsStandardizeTablesMode);
        }

        #endregion

        #region Batch Size Tests

        /**************************************************************/
        /// <summary>
        /// --batch-size with valid value sets BatchSize.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_ValidRange_SetsBatchSize()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--batch-size", "500" });

            Assert.AreEqual(500, result.BatchSize);
            Assert.IsFalse(result.HasErrors);
        }

        /**************************************************************/
        /// <summary>
        /// --batch-size above 50000 produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_TooLarge_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--batch-size", "60000" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("1 and 50000")));
        }

        /**************************************************************/
        /// <summary>
        /// --batch-size below 1 produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_TooSmall_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--batch-size", "0" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("1 and 50000")));
        }

        /**************************************************************/
        /// <summary>
        /// --batch-size with non-numeric value produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_NonNumeric_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--batch-size", "abc" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("numeric")));
        }

        /**************************************************************/
        /// <summary>
        /// --batch-size without --standardize-tables produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_WithoutStandardizeTables_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--batch-size", "500" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("requires --standardize-tables")));
        }

        /**************************************************************/
        /// <summary>
        /// --batch-size with truncate operation produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_BatchSize_WithTruncate_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "truncate", "--batch-size", "500" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("parse or validate")));
        }

        #endregion

        #region Table ID Tests

        /**************************************************************/
        /// <summary>
        /// --table-id without parse-single produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_TableId_WithoutParseSingle_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--table-id", "123" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("parse-single")));
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables parse-single without --table-id produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_ParseSingle_WithoutTableId_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse-single" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("requires --table-id")));
        }

        /**************************************************************/
        /// <summary>
        /// --table-id with non-numeric value produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_TableId_NonNumeric_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse-single", "--table-id", "abc" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("numeric")));
        }

        #endregion

        #region Mutual Exclusion Tests

        /**************************************************************/
        /// <summary>
        /// --standardize-tables with --folder produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesAndFolder_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--folder", "C:\\test" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("--folder")));
        }

        /**************************************************************/
        /// <summary>
        /// --standardize-tables with --orange-book produces an error.
        /// </summary>
        [TestMethod]
        public void Parse_StandardizeTablesAndOrangeBook_AddsError()
        {
            var result = CommandLineArgs.Parse(new[] { "--standardize-tables", "parse", "--orange-book", "C:\\test.zip" });

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("--orange-book")));
        }

        #endregion
    }
}
