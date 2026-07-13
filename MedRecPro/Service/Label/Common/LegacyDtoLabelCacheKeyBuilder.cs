using MedRecPro.Helpers;

namespace MedRecPro.Service.LabelQuery.Common
{
    /**************************************************************/
    /// <summary>
    /// Builds cache keys that preserve the legacy DtoLabelAccess cache namespace.
    /// </summary>
    /// <remarks>
    /// This pure builder keeps key text stable while feature services replace the
    /// static facade. It deliberately uses the literal legacy prefix rather than
    /// the declaring service name, because existing cache entries are part of the
    /// compatibility contract.
    /// </remarks>
    /// <seealso cref="MedRecPro.DataAccess.DtoLabelAccess"/>
    internal sealed class LegacyDtoLabelCacheKeyBuilder
    {
        #region implementation

        private const string LegacyPrefix = "DtoLabelAccess";

        /**************************************************************/
        /// <summary>
        /// Builds the encoded legacy key used by standard search and paging queries.
        /// </summary>
        /// <param name="memberName">The legacy method or view member name.</param>
        /// <param name="searchTerm">Optional caller-supplied search text.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>The Base64-encoded legacy cache key.</returns>
        /// <seealso cref="BuildDocumentPageKey"/>
        internal string BuildQueryKey(string memberName, string? searchTerm, int? page, int? size)
        {
            #region implementation

            ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

            var normalizedSearchTerm = searchTerm?.Replace(" ", "_");
            return $"{LegacyPrefix}.{memberName}_{normalizedSearchTerm ?? "all"}_{page}_{size}".Base64Encode();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the encoded legacy key for a paged document graph request.
        /// </summary>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <param name="useBatchLoading">Whether the batch graph loader is active.</param>
        /// <returns>The Base64-encoded legacy cache key.</returns>
        /// <seealso cref="BuildDocumentGuidKey"/>
        internal string BuildDocumentPageKey(int? page, int? size, bool? useBatchLoading)
        {
            #region implementation

            var loadingMode = useBatchLoading == true ? "batch" : "sequential";
            return $"{LegacyPrefix}.BuildDocumentsAsync_{page}_{size}_{loadingMode}".Base64Encode();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the encoded legacy key for one document graph request.
        /// </summary>
        /// <param name="documentGuid">The requested document identifier.</param>
        /// <param name="useBatchLoading">Whether the batch graph loader is active.</param>
        /// <returns>The Base64-encoded legacy cache key.</returns>
        /// <seealso cref="BuildDocumentPageKey"/>
        internal string BuildDocumentGuidKey(Guid documentGuid, bool? useBatchLoading)
        {
            #region implementation

            var loadingMode = useBatchLoading == true ? "batch" : "sequential";
            return $"{LegacyPrefix}.BuildDocumentsAsync.{documentGuid}_{loadingMode}".Base64Encode();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the encoded legacy Orange Book search key.
        /// </summary>
        /// <param name="expiringInMonths">Optional patent-expiration horizon.</param>
        /// <param name="documentGuid">Optional document identifier filter.</param>
        /// <param name="applicationNumber">Optional application number filter.</param>
        /// <param name="ingredient">Optional ingredient filter.</param>
        /// <param name="tradeName">Optional trade-name filter.</param>
        /// <param name="patentNo">Optional patent number filter.</param>
        /// <param name="patentExpireDate">Optional exact expiration-date filter.</param>
        /// <param name="hasPediatricFlag">Optional pediatric flag filter.</param>
        /// <param name="hasWithdrawnCommercialReasonFlag">Optional withdrawal-reason filter.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>The Base64-encoded legacy cache key.</returns>
        /// <seealso cref="MedRecPro.DataAccess.DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        internal string BuildOrangeBookSearchKey(
            int? expiringInMonths,
            Guid? documentGuid,
            string? applicationNumber,
            string? ingredient,
            string? tradeName,
            string? patentNo,
            DateOnly? patentExpireDate,
            bool? hasPediatricFlag,
            bool? hasWithdrawnCommercialReasonFlag,
            int? page,
            int? size)
        {
            #region implementation

            var searchKey = string.Join("-",
                expiringInMonths?.ToString() ?? string.Empty,
                documentGuid?.ToString() ?? string.Empty,
                applicationNumber ?? string.Empty,
                ingredient ?? string.Empty,
                tradeName ?? string.Empty,
                patentNo ?? string.Empty,
                patentExpireDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                hasPediatricFlag?.ToString() ?? string.Empty,
                hasWithdrawnCommercialReasonFlag?.ToString() ?? string.Empty);

            return BuildQueryKey("SearchOrangeBookPatentsAsync", searchKey, page, size);

            #endregion
        }

        #endregion
    }
}
