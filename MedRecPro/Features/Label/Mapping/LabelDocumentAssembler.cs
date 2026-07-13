using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.Service.LabelQuery.Implementation;

namespace MedRecPro.Features.Label.Mapping
{
    /**************************************************************/
    /// <summary>
    /// Selects the established document graph-loading strategy and returns assembled document DTOs.
    /// </summary>
    /// <remarks>
    /// This mapping boundary keeps feature-facing document assembly separate from the data-access
    /// implementation while preserving the legacy sequential default and the feature-flagged batch path.
    /// </remarks>
    /// <seealso cref="LabelQueryDataAccess"/>
    /// <seealso cref="DocumentDto"/>
    internal static class LabelDocumentAssembler
    {
        /**************************************************************/
        /// <summary>
        /// Builds complete document DTO graphs with the selected loading strategy.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documents">Document entities that form the roots of the returned graphs.</param>
        /// <param name="pkSecret">Secret used to encrypt entity identifiers in the returned DTOs.</param>
        /// <param name="logger">Logger used to record the chosen loading strategy.</param>
        /// <param name="useBatchLoading">Whether to use the batch graph-loading strategy.</param>
        /// <returns>The complete document DTO graphs in the input document order.</returns>
        /// <remarks>
        /// A null flag intentionally selects sequential loading to retain the legacy behavior.
        /// </remarks>
        /// <seealso cref="LabelQueryDataAccess.BuildSequentialDocumentDtosFromEntitiesAsync"/>
        /// <seealso cref="LabelQueryDataAccess.BuildBatchDocumentDtosFromEntitiesAsync"/>
        internal static Task<List<DocumentDto>> AssembleAsync(
            ApplicationDbContext db,
            List<global::MedRecPro.Models.Label.Document> documents,
            string pkSecret,
            ILogger logger,
            bool? useBatchLoading)
        {
            #region implementation

            var useBatch = useBatchLoading ?? false;
            logger.LogDebug(
                "Building document DTOs using {LoadingStrategy} loading strategy",
                useBatch ? "BATCH" : "SEQUENTIAL");

            return useBatch
                ? LabelQueryDataAccess.BuildBatchDocumentDtosFromEntitiesAsync(db, documents, pkSecret, logger)
                : LabelQueryDataAccess.BuildSequentialDocumentDtosFromEntitiesAsync(db, documents, pkSecret, logger);

            #endregion
        }
    }
}
