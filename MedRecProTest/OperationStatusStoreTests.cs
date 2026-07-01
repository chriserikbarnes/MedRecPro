using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests operation status storage contracts used by long-running API progress endpoints.
    /// </summary>
    /// <remarks>
    /// Focuses on the web import status type because the import progress endpoint reads
    /// through <see cref="IOperationStatusStore"/>.
    /// </remarks>
    /// <seealso cref="IOperationStatusStore"/>
    /// <seealso cref="ImportOperationStatus"/>
    [TestClass]
    public class OperationStatusStoreTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Stores and retrieves a web import operation status without type-cast errors.
        /// </summary>
        /// <remarks>
        /// Guards the import progress contract so the status store round trip uses
        /// <see cref="MedRecPro.Models.ImportOperationStatus"/> consistently.
        /// </remarks>
        /// <seealso cref="InMemoryOperationStatusStore"/>
        /// <seealso cref="ImportOperationStatus"/>
        [TestMethod]
        public void TryGet_WebImportStatus_ReturnsStoredStatus()
        {
            #region implementation
            var store = new InMemoryOperationStatusStore();
            var operationId = Guid.NewGuid().ToString();
            var expected = new ImportOperationStatus
            {
                OperationId = operationId,
                ProgressUrl = $"/api/Label/import/progress/{operationId}",
                Status = "Completed",
                PercentComplete = 100,
                CurrentFile = 2,
                TotalFiles = 2,
                Results = new List<SplZipImportResult>
                {
                    new SplZipImportResult
                    {
                        ZipFileName = "labels.zip",
                        FileResults = new List<SplFileImportResult>
                        {
                            new SplFileImportResult
                            {
                                FileName = "label.xml",
                                Success = true,
                                Message = "Imported successfully."
                            }
                        }
                    }
                }
            };

            store.Set(operationId, expected);
            var found = store.TryGet(operationId, out var actual);

            Assert.IsTrue(found);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.OperationId, actual.OperationId);
            Assert.AreEqual(expected.ProgressUrl, actual.ProgressUrl);
            Assert.AreEqual(expected.Status, actual.Status);
            Assert.AreEqual(expected.PercentComplete, actual.PercentComplete);
            Assert.AreEqual(expected.CurrentFile, actual.CurrentFile);
            Assert.AreEqual(expected.TotalFiles, actual.TotalFiles);
            Assert.AreEqual("labels.zip", actual.Results?.Single().ZipFileName);
            #endregion
        }

        #endregion
    }
}
