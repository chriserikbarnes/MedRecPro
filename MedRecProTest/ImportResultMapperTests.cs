using MedRecPro.Mappers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebSplZipImportResult = MedRecPro.Models.SplZipImportResult;
using ImportImportOperationStatus = MedRecProImportClass.Models.ImportOperationStatus;
using ImportSplFileImportResult = MedRecProImportClass.Models.SplFileImportResult;
using ImportSplZipImportResult = MedRecProImportClass.Models.SplZipImportResult;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for SPL import result boundary mapping.
    /// </summary>
    /// <remarks>
    /// Verifies that import-library result DTOs are copied into the web API result
    /// DTOs without assigning computed web properties directly.
    /// </remarks>
    /// <seealso cref="ImportResultMapper"/>
    /// <seealso cref="WebSplZipImportResult"/>
    [TestClass]
    public class ImportResultMapperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Maps a single successful ZIP result with one file result.
        /// </summary>
        /// <returns>A task representing the test operation.</returns>
        /// <remarks>
        /// Covers the normal success path used by import progress completion.
        /// </remarks>
        /// <seealso cref="ImportResultMapper.ToWebResults"/>
        [TestMethod]
        public void ToWebResults_SingleSuccessfulFile_CopiesStoredFields()
        {
            #region implementation
            var source = new List<ImportSplZipImportResult>
            {
                new ImportSplZipImportResult
                {
                    ZipFileName = "labels.zip",
                    FileResults = new List<ImportSplFileImportResult>
                    {
                        new ImportSplFileImportResult
                        {
                            FileName = "label.xml",
                            Success = true,
                            Message = "Imported successfully.",
                            DocumentsCreated = 1,
                            OrganizationsCreated = 2,
                            ProductsCreated = 3,
                            SectionsCreated = 4,
                            IngredientsCreated = 5,
                            ProductElementsCreated = 6
                        }
                    }
                }
            };

            var results = ImportResultMapper.ToWebResults(source);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("labels.zip", results[0].ZipFileName);
            Assert.AreEqual(1, results[0].FileResults.Count);
            Assert.AreEqual("label.xml", results[0].FileResults[0].FileName);
            Assert.IsTrue(results[0].FileResults[0].Success);
            Assert.AreEqual("Imported successfully.", results[0].FileResults[0].Message);
            Assert.AreEqual(1, results[0].FileResults[0].DocumentsCreated);
            Assert.AreEqual(2, results[0].FileResults[0].OrganizationsCreated);
            Assert.AreEqual(3, results[0].FileResults[0].ProductsCreated);
            Assert.AreEqual(4, results[0].FileResults[0].SectionsCreated);
            Assert.AreEqual(5, results[0].FileResults[0].IngredientsCreated);
            Assert.AreEqual(6, results[0].FileResults[0].ProductElementsCreated);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps a failed file result with multiple errors.
        /// </summary>
        /// <remarks>
        /// Ensures error details survive the import-library to web DTO boundary.
        /// </remarks>
        /// <seealso cref="ImportResultMapper.ToWebResults"/>
        [TestMethod]
        public void ToWebResults_FailedFileWithErrors_CopiesErrorMessages()
        {
            #region implementation
            var sourceErrors = new List<string> { "Missing document id.", "Invalid product code." };
            var source = new List<ImportSplZipImportResult>
            {
                new ImportSplZipImportResult
                {
                    ZipFileName = "failed.zip",
                    FileResults = new List<ImportSplFileImportResult>
                    {
                        new ImportSplFileImportResult
                        {
                            FileName = "failed.xml",
                            Success = false,
                            Message = "Imported with errors.",
                            Errors = sourceErrors
                        }
                    }
                }
            };

            var results = ImportResultMapper.ToWebResults(source);

            CollectionAssert.AreEqual(sourceErrors, results[0].FileResults[0].Errors);
            Assert.AreNotSame(sourceErrors, results[0].FileResults[0].Errors);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps null and empty result lists to empty web result lists.
        /// </summary>
        /// <remarks>
        /// Protects import progress callbacks from null result collections.
        /// </remarks>
        /// <seealso cref="ImportResultMapper.ToWebResults"/>
        [TestMethod]
        public void ToWebResults_NullOrEmptySource_ReturnsEmptyList()
        {
            #region implementation
            var nullResults = ImportResultMapper.ToWebResults(null);
            var emptyResults = ImportResultMapper.ToWebResults(new List<ImportSplZipImportResult>());

            Assert.AreEqual(0, nullResults.Count);
            Assert.AreEqual(0, emptyResults.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Leaves web computed ZIP result properties derived from mapped file results.
        /// </summary>
        /// <remarks>
        /// Verifies OverallSuccess, TotalFilesProcessed, and TotalFilesSucceeded still
        /// reflect the target FileResults collection after mapping.
        /// </remarks>
        /// <seealso cref="ImportResultMapper.ToWebResults"/>
        /// <seealso cref="WebSplZipImportResult"/>
        [TestMethod]
        public void ToWebResults_MixedFileResults_ComputesWebSummaryProperties()
        {
            #region implementation
            var source = new List<ImportSplZipImportResult>
            {
                new ImportSplZipImportResult
                {
                    ZipFileName = "mixed.zip",
                    FileResults = new List<ImportSplFileImportResult>
                    {
                        new ImportSplFileImportResult { FileName = "success.xml", Success = true },
                        new ImportSplFileImportResult { FileName = "failed.xml", Success = false }
                    }
                }
            };

            var result = ImportResultMapper.ToWebResults(source).Single();

            Assert.IsFalse(result.OverallSuccess);
            Assert.AreEqual(2, result.TotalFilesProcessed);
            Assert.AreEqual(1, result.TotalFilesSucceeded);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps an import-library operation status into the web API progress response.
        /// </summary>
        /// <remarks>
        /// Covers the Phase 5 import-boundary seam where runtime import status remains
        /// library-owned but polling responses stay web DTO compatible.
        /// </remarks>
        /// <seealso cref="ImportResultMapper.ToWebStatus"/>
        [TestMethod]
        public void ToWebStatus_ImportLibraryStatus_CopiesProgressAndMapsResults()
        {
            #region implementation
            var source = new ImportImportOperationStatus
            {
                OperationId = "operation-1",
                ProgressUrl = "/api/Label/import/progress/operation-1",
                Status = "Completed",
                PercentComplete = 100,
                CurrentFile = 1,
                TotalFiles = 1,
                Results = new List<ImportSplZipImportResult>
                {
                    new ImportSplZipImportResult
                    {
                        ZipFileName = "labels.zip",
                        FileResults = new List<ImportSplFileImportResult>
                        {
                            new ImportSplFileImportResult
                            {
                                FileName = "label.xml",
                                Success = true,
                                DocumentsCreated = 1
                            }
                        }
                    }
                }
            };

            var result = ImportResultMapper.ToWebStatus(source);

            Assert.AreEqual(source.OperationId, result.OperationId);
            Assert.AreEqual(source.ProgressUrl, result.ProgressUrl);
            Assert.AreEqual(source.Status, result.Status);
            Assert.AreEqual(source.PercentComplete, result.PercentComplete);
            Assert.AreEqual(source.CurrentFile, result.CurrentFile);
            Assert.AreEqual(source.TotalFiles, result.TotalFiles);
            Assert.AreEqual("labels.zip", result.Results?.Single().ZipFileName);
            Assert.AreEqual("label.xml", result.Results?.Single().FileResults.Single().FileName);
            Assert.AreEqual(1, result.Results?.Single().FileResults.Single().DocumentsCreated);
            #endregion
        }

        #endregion
    }
}
