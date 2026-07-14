using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Coordinates AI-assisted Label search operations below the MVC boundary.
    /// </summary>
    /// <remarks>
    /// The service owns optional Claude availability, encrypted user-context construction, and
    /// fallback logging so classification actions only translate the established HTTP outcomes.
    /// </remarks>
    /// <seealso cref="IClaudeSearchService"/>
    public interface ILabelAiSearchService
    {
        Task<PharmacologicClassSearchResult?> SearchByPharmacologicClassAsync(string query, int maxProductsPerClass, bool isAuthenticated, long? userId, CancellationToken cancellationToken);
        Task<List<PharmacologicClassSummaryDto>?> GetCachedClassSummariesAsync(CancellationToken cancellationToken);
        Task<ProductExtractionResult> ExtractProductAsync(string description, CancellationToken cancellationToken);
        Task<IndicationSearchResult?> SearchByIndicationAsync(string query, int maxProductsPerIndication, bool isAuthenticated, long? userId, CancellationToken cancellationToken);
    }

    /**************************************************************/
    /// <summary>
    /// Implements AI-assisted Label searches with the established graceful-fallback behavior.
    /// </summary>
    /// <seealso cref="ILabelAiSearchService"/>
    public sealed class LabelAiSearchService : ILabelAiSearchService
    {
        private readonly IClaudeApiService _claudeApiService;
        private readonly IClaudeSearchService? _claudeSearchService;
        private readonly IPrimaryKeyCipher _primaryKeyCipher;
        private readonly ILogger<LabelAiSearchService> _logger;

        /**************************************************************/
        /// <summary>Initializes a new instance of the AI Label search service.</summary>
        /// <param name="claudeApiService">Service that creates Claude system context.</param>
        /// <param name="primaryKeyCipher">Cipher for optional authenticated-user context.</param>
        /// <param name="logger">Logger for fallback diagnostics.</param>
        /// <param name="claudeSearchService">Optional Claude search capability.</param>
        public LabelAiSearchService(IClaudeApiService claudeApiService, IPrimaryKeyCipher primaryKeyCipher, ILogger<LabelAiSearchService> logger, IClaudeSearchService? claudeSearchService = null)
        {
            #region implementation

            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));
            _primaryKeyCipher = primaryKeyCipher ?? throw new ArgumentNullException(nameof(primaryKeyCipher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _claudeSearchService = claudeSearchService;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<PharmacologicClassSearchResult?> SearchByPharmacologicClassAsync(string query, int maxProductsPerClass, bool isAuthenticated, long? userId, CancellationToken cancellationToken)
        {
            #region implementation

            if (_claudeSearchService is null)
            {
                return null;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = await createSystemContextAsync(isAuthenticated, userId, cancellationToken);
                var result = await _claudeSearchService.SearchByUserQueryAsync(query, context, maxProductsPerClass);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI pharmacologic class search failed, falling back to database search: {Query}", query);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<List<PharmacologicClassSummaryDto>?> GetCachedClassSummariesAsync(CancellationToken cancellationToken)
        {
            #region implementation

            if (_claudeSearchService is null)
            {
                return null;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await _claudeSearchService.GetAllClassSummariesAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI cache retrieval failed, falling back to database query");
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<ProductExtractionResult> ExtractProductAsync(string description, CancellationToken cancellationToken)
        {
            #region implementation

            if (_claudeSearchService is null)
            {
                return new ProductExtractionResult
                {
                    Success = false,
                    Error = "Product extraction service not available"
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await _claudeSearchService.ExtractProductFromDescriptionAsync(description);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<IndicationSearchResult?> SearchByIndicationAsync(string query, int maxProductsPerIndication, bool isAuthenticated, long? userId, CancellationToken cancellationToken)
        {
            #region implementation

            if (_claudeSearchService is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var context = await createSystemContextAsync(isAuthenticated, userId, cancellationToken);
            return await _claudeSearchService.SearchByIndicationAsync(query, context, maxProductsPerIndication);

            #endregion
        }

        /**************************************************************/
        /// <summary>Creates the established Claude system context for a request user.</summary>
        /// <param name="isAuthenticated">Whether the originating request is authenticated.</param>
        /// <param name="userId">Current numeric user identifier when available.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>System context supplied to the Claude search service.</returns>
        private Task<AiSystemContext> createSystemContextAsync(bool isAuthenticated, long? userId, CancellationToken cancellationToken)
        {
            #region implementation

            cancellationToken.ThrowIfCancellationRequested();
            var encryptedUserId = isAuthenticated && userId.HasValue
                ? _primaryKeyCipher.Encrypt(userId.Value)
                : null;
            return _claudeApiService.GetSystemContextAsync(isAuthenticated, encryptedUserId);

            #endregion
        }
    }
}
