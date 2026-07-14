using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Retrieves established complete Label document representations.
    /// </summary>
    /// <remarks>
    /// This service keeps repository selection and complete-graph retrieval below MVC while retaining
    /// the legacy dictionary-shaped payloads used by existing Label endpoints.
    /// </remarks>
    /// <seealso cref="Repository{T}"/>
    public interface ICompleteLabelService
    {
        /**************************************************************/
        /// <summary>Retrieves a complete Label document by document GUID.</summary>
        /// <param name="documentGuid">Document identifier to retrieve.</param>
        /// <returns>Complete document representations.</returns>
        Task<List<DocumentDto>> GetAsync(Guid documentGuid);

        /**************************************************************/
        /// <summary>Retrieves a page of complete Label documents.</summary>
        /// <param name="pageNumber">One-based page number.</param>
        /// <param name="pageSize">Number of documents per page.</param>
        /// <returns>Complete document representations.</returns>
        Task<List<DocumentDto>> GetAsync(int pageNumber, int pageSize);
    }

    /**************************************************************/
    /// <summary>
    /// Implements <see cref="ICompleteLabelService"/> through the existing document repository.
    /// </summary>
    /// <seealso cref="ICompleteLabelService"/>
    public sealed class CompleteLabelService : ICompleteLabelService
    {
        private readonly Repository<Label.Document> _documentRepository;

        /**************************************************************/
        /// <summary>Initializes a new instance of the service.</summary>
        /// <param name="documentRepository">Repository for complete Label document graphs.</param>
        public CompleteLabelService(Repository<Label.Document> documentRepository)
        {
            #region implementation

            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentDto>> GetAsync(Guid documentGuid)
        {
            #region implementation

            return _documentRepository.GetCompleteLabelsAsync(documentGuid);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<DocumentDto>> GetAsync(int pageNumber, int pageSize)
        {
            #region implementation

            return _documentRepository.GetCompleteLabelsAsync(pageNumber, pageSize);

            #endregion
        }
    }
}
