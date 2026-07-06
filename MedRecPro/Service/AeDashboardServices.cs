using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Reads AE dashboard product catalog and picker data.
    /// </summary>
    /// <remarks>
    /// This interface gives controllers an injectable seam while the static
    /// <see cref="DtoLabelAccess"/> methods remain as compatibility entry points.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDrugSummaryDto"/>
    public interface IAeDashboardProductCatalogService
    {
        /**************************************************************/
        /// <summary>
        /// Gets AE dashboard product summaries.
        /// </summary>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="userId">Optional authenticated user identifier used to mark favorite products.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Product summary DTOs with derived dashboard fields.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        Task<List<AeDrugSummaryDto>> GetDrugSummariesAsync(
            string pkSecret,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null);

        /**************************************************************/
        /// <summary>
        /// Gets slim product picker catalog rows.
        /// </summary>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="userId">Optional authenticated user identifier used to mark favorite products.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Slim picker rows for the AE dashboard.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeProductCatalogAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        Task<List<AeProductCatalogItemDto>> GetProductCatalogAsync(
            string pkSecret,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null);

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard product count.
        /// </summary>
        /// <returns>The number of product rows available to the dashboard.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeProductCountAsync(ApplicationDbContext, ILogger)"/>
        Task<int> GetProductCountAsync();
    }

    /**************************************************************/
    /// <summary>
    /// Reads AE dashboard product-detail, signal, reverse-lookup, and interchange data.
    /// </summary>
    /// <remarks>
    /// Product-detail reads are separated from catalog reads because they share
    /// per-document signal loading and a user-independent cache boundary.
    /// </remarks>
    /// <seealso cref="AeDashboardProductDetailData"/>
    /// <seealso cref="AeRiskSignalDto"/>
    public interface IAeDashboardProductDetailService
    {
        /**************************************************************/
        /// <summary>
        /// Gets derived AE risk signals for one SPL document.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>Derived signal DTOs for the requested product.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        Task<List<AeRiskSignalDto>> GetRiskSignalsByDocumentAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true);

        /**************************************************************/
        /// <summary>
        /// Gets the reusable product-detail payload for product-level views.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>Product context plus derived signals, or null when absent.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeProductDetailDataAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        Task<AeDashboardProductDetailData?> GetProductDetailDataAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true);

        /**************************************************************/
        /// <summary>
        /// Gets the tiered triage view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A triage view DTO, or null when absent.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeTriageViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        Task<AeTriageViewDto?> GetTriageViewAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true);

        /**************************************************************/
        /// <summary>
        /// Gets the forest plot view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A forest plot DTO, or null when absent.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeForestPlotAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        Task<AeForestPlotDto?> GetForestPlotAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true);

        /**************************************************************/
        /// <summary>
        /// Gets the quadrant view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A quadrant DTO, or null when absent.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeQuadrantViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        Task<AeQuadrantViewDto?> GetQuadrantViewAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true);

        /**************************************************************/
        /// <summary>
        /// Gets reverse-lookup matches for one symptom term.
        /// </summary>
        /// <param name="symptom">Adverse-event term to look up.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="documentGuids">Optional product scope limiting candidate documents.</param>
        /// <returns>A ranked symptom-to-product reverse lookup result.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeReverseLookupAsync(ApplicationDbContext, string, string, ILogger, IEnumerable{Guid}?)"/>
        Task<AeReverseLookupResultDto> GetReverseLookupAsync(
            string symptom,
            string pkSecret,
            IEnumerable<Guid>? documentGuids = null);

        /**************************************************************/
        /// <summary>
        /// Gets an interchange comparison for two products.
        /// </summary>
        /// <param name="documentGuidA">SPL document identifier for product A.</param>
        /// <param name="documentGuidB">SPL document identifier for product B.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="differencesOnly">Whether similar rows should be removed.</param>
        /// <param name="sharedSignalsOnly">Whether rows without signals on both products should be removed.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <returns>An interchange comparison DTO, or null when either product is missing.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeInterchangeAsync(ApplicationDbContext, Guid, Guid, string, ILogger, bool, bool, AeComparatorMix?)"/>
        Task<AeInterchangeComparisonDto?> GetInterchangeAsync(
            Guid documentGuidA,
            Guid documentGuidB,
            string pkSecret,
            bool differencesOnly = false,
            bool sharedSignalsOnly = false,
            AeComparatorMix? comparator = null);
    }

    /**************************************************************/
    /// <summary>
    /// Reads and writes authenticated-user AE dashboard favorite state.
    /// </summary>
    /// <remarks>
    /// Favorite state is intentionally outside shared dashboard cache policy because
    /// it is scoped to one authenticated user.
    /// </remarks>
    /// <seealso cref="AspNetUserFavorite"/>
    public interface IAeDashboardFavoriteService
    {
        /**************************************************************/
        /// <summary>
        /// Gets favorited product summaries for one user.
        /// </summary>
        /// <param name="userId">Authenticated user identifier.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Favorite product summaries for the supplied user.</returns>
        /// <seealso cref="DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(ApplicationDbContext, long, string, ILogger, int?, int?)"/>
        Task<List<AeDrugSummaryDto>> GetFavoriteDrugSummariesAsync(
            long userId,
            string pkSecret,
            int? page = null,
            int? size = null);

        /**************************************************************/
        /// <summary>
        /// Adds or removes one AE dashboard product favorite.
        /// </summary>
        /// <param name="userId">Authenticated user identifier.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="isFavorite">True to add the favorite; false to remove it.</param>
        /// <returns>True when the product exists in the dashboard surface; otherwise false.</returns>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync(ApplicationDbContext, long, Guid, bool, ILogger)"/>
        Task<bool> SetProductFavoriteAsync(
            long userId,
            Guid documentGuid,
            bool isFavorite);
    }

    /**************************************************************/
    /// <summary>
    /// Reads AE dashboard pharmacologic-class-first correlation data.
    /// </summary>
    /// <remarks>
    /// This service owns the class-axis correlation API data path.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    public interface IAeDashboardClassCorrelationService
    {
        /**************************************************************/
        /// <summary>
        /// Gets the SOC by SOC correlation map for one pharmacologic class.
        /// </summary>
        Task<AeCorrelationMapDto?> GetCorrelationMapAsync(
            string pharmClassCode,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minDrugsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0);

        /**************************************************************/
        /// <summary>
        /// Gets pharmacologic classes that have AE rows for correlation.
        /// </summary>
        Task<AeCorrelationClassPickerPage> GetCorrelationClassesAsync(
            string pkSecret,
            string? classSearch = null,
            int? page = null,
            int? size = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            bool excludeCombos = false,
            int minEvents = 0,
            int minDrugsPerCell = 4,
            bool seriousSocOnly = false);

        /**************************************************************/
        /// <summary>
        /// Gets a sparse SOC by drug heatmap for one pharmacologic class.
        /// </summary>
        Task<AeCorrelationHeatmapDto?> GetCorrelationHeatmapAsync(
            string pharmClassCode,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0);

        /**************************************************************/
        /// <summary>
        /// Gets drug-pair detail for one class-first correlation cell.
        /// </summary>
        Task<AeCorrelationCellDetailDto?> GetCorrelationCellDetailAsync(
            string pharmClassCode,
            string socX,
            string socY,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minDrugsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0);
    }

    /**************************************************************/
    /// <summary>
    /// Reads AE dashboard MedDRA-system-first correlation data.
    /// </summary>
    /// <remarks>
    /// This service owns the system-axis correlation API data path.
    /// </remarks>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    public interface IAeDashboardSystemCorrelationService
    {
        /**************************************************************/
        /// <summary>
        /// Gets MedDRA systems available for system-first correlation.
        /// </summary>
        Task<AeSystemPickerPage> GetCorrelationSystemsAsync(
            string pkSecret,
            string? systemSearch = null,
            int? page = null,
            int? size = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            bool excludeCombos = false,
            int minEvents = 0,
            int minTermsPerCell = 4,
            string? classType = null);

        /**************************************************************/
        /// <summary>
        /// Gets a pharmacologic-class by class correlation map for selected systems.
        /// </summary>
        Task<AeSystemClassCorrelationMapDto?> GetSystemCorrelationMapAsync(
            IEnumerable<string> systems,
            string pkSecret,
            string? classSearch = null,
            int classPageNumber = 1,
            int classPageSize = 20,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minTermsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            bool includeFullMatrix = false,
            string? classType = null);

        /**************************************************************/
        /// <summary>
        /// Gets a system-scoped class by drug sparse heatmap.
        /// </summary>
        Task<AeSystemClassHeatmapDto?> GetSystemCorrelationHeatmapAsync(
            IEnumerable<string> systems,
            string pkSecret,
            string? classSearch = null,
            string? drugSearch = null,
            int? classPageNumber = null,
            int? classPageSize = null,
            int? drugPageNumber = null,
            int? drugPageSize = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            string? classType = null);

        /**************************************************************/
        /// <summary>
        /// Gets per-term pair detail for one system-first class correlation cell.
        /// </summary>
        Task<AeSystemClassCorrelationCellDetailDto?> GetSystemCorrelationCellDetailAsync(
            IEnumerable<string> systems,
            string classX,
            string classY,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minTermsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            int pageNumber = 1,
            int pageSize = 100,
            string? classType = null);
    }

    /**************************************************************/
    /// <summary>
    /// Generates and applies AE dashboard cache keys.
    /// </summary>
    /// <remarks>
    /// The generated key intentionally preserves the legacy
    /// <c>DtoLabelAccess</c> prefix so moved AE dashboard methods keep reading and
    /// writing the same managed cache entries.
    /// </remarks>
    /// <seealso cref="PerformanceHelper"/>
    public interface IAeDashboardCachePolicy
    {
        /**************************************************************/
        /// <summary>
        /// Generates the legacy AE dashboard cache key.
        /// </summary>
        /// <param name="viewName">The logical query or method name.</param>
        /// <param name="searchTerm">Optional search token.</param>
        /// <param name="page">Optional page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Base64-encoded cache key string.</returns>
        string GenerateKey(string viewName, string? searchTerm, int? page, int? size);

        /**************************************************************/
        /// <summary>
        /// Gets a cached AE dashboard value by key.
        /// </summary>
        /// <typeparam name="T">Expected cached value type.</typeparam>
        /// <param name="key">Cache key returned by <see cref="GenerateKey"/>.</param>
        /// <returns>The cached value, or default when absent.</returns>
        T? Get<T>(string key);

        /**************************************************************/
        /// <summary>
        /// Stores an AE dashboard value under a managed cache key.
        /// </summary>
        /// <param name="key">Cache key returned by <see cref="GenerateKey"/>.</param>
        /// <param name="value">Value to store.</param>
        /// <param name="duration">Cache duration in hours.</param>
        void Set(string key, object value, double duration = 1.0);
    }

    /**************************************************************/
    /// <summary>
    /// Encrypts and decrypts AE dashboard integer identifiers.
    /// </summary>
    /// <remarks>
    /// The mapper centralizes client-held encrypted ID behavior so ciphertext
    /// minted before service extraction remains decryptable afterward.
    /// </remarks>
    /// <seealso cref="StringCipher"/>
    public interface IAeDashboardEncryptedIdMapper
    {
        /**************************************************************/
        /// <summary>
        /// Encrypts a nullable integer identifier for DTO exposure.
        /// </summary>
        /// <param name="value">Identifier value to encrypt.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="fieldName">Field name used in error logs.</param>
        /// <returns>Encrypted identifier text, or null when absent or encryption fails.</returns>
        string? EncryptNullableInt(int? value, string pkSecret, ILogger logger, string fieldName);

        /**************************************************************/
        /// <summary>
        /// Decrypts a nullable integer identifier captured from a client-held token.
        /// </summary>
        /// <param name="encryptedValue">Encrypted identifier text.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="fieldName">Field name used in error logs.</param>
        /// <returns>The decrypted integer, or null when absent or invalid.</returns>
        int? DecryptNullableInt(string? encryptedValue, string pkSecret, ILogger logger, string fieldName);
    }

    /**************************************************************/
    /// <summary>
    /// Owns AE dashboard correlation validation and system-filter normalization.
    /// </summary>
    /// <remarks>
    /// Hard-coded validation and selected-system normalization live here so
    /// class-first and system-first services share one policy.
    /// </remarks>
    /// <seealso cref="AeCorrelationFilters"/>
    /// <seealso cref="AeSystemCorrelationFilters"/>
    public interface IAeDashboardCorrelationPolicy
    {
        /**************************************************************/
        /// <summary>
        /// Validates class-first correlation enum values.
        /// </summary>
        void ValidateCorrelationEnums(
            AeComparatorMix comparator,
            AeCorrelationAggregation aggregation,
            AeCorrelationMethod? method = null);

        /**************************************************************/
        /// <summary>
        /// Builds normalized system-first correlation filters.
        /// </summary>
        AeSystemCorrelationFilters BuildSystemCorrelationFilters(
            AeComparatorMix comparator,
            bool includeNonSignificant,
            bool excludeFragile,
            int minTermsPerCell,
            AeCorrelationMethod method,
            AeCorrelationAggregation aggregation,
            bool excludeCombos,
            int minEvents);

        /**************************************************************/
        /// <summary>
        /// Normalizes selected-system query values.
        /// </summary>
        List<string> NormalizeSystemInputs(IEnumerable<string>? systems);

        /**************************************************************/
        /// <summary>
        /// Resolves requested systems to canonical casing from surviving observations.
        /// </summary>
        List<string> CanonicalizeSelectedSystems(
            IReadOnlyList<string> requestedSystems,
            IReadOnlyList<AeSystemCorrelationObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Determines whether a MedDRA SOC should be treated as serious.
        /// </summary>
        bool IsSeriousCorrelationSoc(string? soc);
    }

    /**************************************************************/
    /// <summary>
    /// Default AE dashboard cache policy.
    /// </summary>
    /// <remarks>
    /// This implementation is stateless and safe to reuse as a singleton.
    /// </remarks>
    /// <seealso cref="IAeDashboardCachePolicy"/>
    public sealed class AeDashboardCachePolicy : IAeDashboardCachePolicy
    {
        /**************************************************************/
        /// <summary>
        /// Gets a reusable shared cache policy for static compatibility paths.
        /// </summary>
        public static AeDashboardCachePolicy Shared { get; } = new();

        /**************************************************************/
        /// <inheritdoc/>
        public string GenerateKey(string viewName, string? searchTerm, int? page, int? size)
        {
            #region implementation

            searchTerm = searchTerm?.Replace(" ", "_");
            var keyParts = $"{nameof(DtoLabelAccess)}.{viewName}_{searchTerm ?? "all"}_{page}_{size}";

            return keyParts.Base64Encode();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public T? Get<T>(string key)
        {
            #region implementation

            return PerformanceHelper.GetCache<T>(key);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public void Set(string key, object value, double duration = 1.0)
        {
            #region implementation

            PerformanceHelper.SetCacheManageKey(key, value, duration);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Default AE dashboard encrypted-ID mapper.
    /// </summary>
    /// <remarks>
    /// Encryption uses <see cref="StringCipher.EncryptionStrength.Fast"/> to
    /// preserve the current dashboard DTO token contract.
    /// </remarks>
    /// <seealso cref="IAeDashboardEncryptedIdMapper"/>
    public sealed class AeDashboardEncryptedIdMapper : IAeDashboardEncryptedIdMapper
    {
        /**************************************************************/
        /// <summary>
        /// Gets a reusable shared mapper for static compatibility paths.
        /// </summary>
        public static AeDashboardEncryptedIdMapper Shared { get; } = new();

        /**************************************************************/
        /// <inheritdoc/>
        public string? EncryptNullableInt(int? value, string pkSecret, ILogger logger, string fieldName)
        {
            #region implementation

            if (!value.HasValue)
            {
                return null;
            }

            try
            {
                return StringCipher.Encrypt(value.Value.ToString(), pkSecret, StringCipher.EncryptionStrength.Fast);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to encrypt AE dashboard identifier {FieldName} with value {Value}.", fieldName, value.Value);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public int? DecryptNullableInt(string? encryptedValue, string pkSecret, ILogger logger, string fieldName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                return null;
            }

            try
            {
                var decrypted = encryptedValue.Decrypt(pkSecret);
                return int.TryParse(decrypted, out var value) ? value : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decrypt AE dashboard identifier {FieldName}.", fieldName);
                return null;
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Default AE dashboard correlation policy.
    /// </summary>
    /// <remarks>
    /// This implementation is stateless and safe to reuse as a singleton.
    /// </remarks>
    /// <seealso cref="IAeDashboardCorrelationPolicy"/>
    public sealed class AeDashboardCorrelationPolicy : IAeDashboardCorrelationPolicy
    {
        /**************************************************************/
        /// <summary>
        /// Gets a reusable shared policy for static compatibility paths.
        /// </summary>
        public static AeDashboardCorrelationPolicy Shared { get; } = new();

        /**************************************************************/
        /// <inheritdoc/>
        public void ValidateCorrelationEnums(
            AeComparatorMix comparator,
            AeCorrelationAggregation aggregation,
            AeCorrelationMethod? method = null)
        {
            #region implementation

            if (!Enum.IsDefined(typeof(AeComparatorMix), comparator))
            {
                throw new ArgumentOutOfRangeException(nameof(comparator), comparator, "Unsupported comparator mix.");
            }

            if (!Enum.IsDefined(typeof(AeCorrelationAggregation), aggregation))
            {
                throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unsupported correlation aggregation.");
            }

            if (method.HasValue && !Enum.IsDefined(typeof(AeCorrelationMethod), method.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method.Value, "Unsupported correlation method.");
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public AeSystemCorrelationFilters BuildSystemCorrelationFilters(
            AeComparatorMix comparator,
            bool includeNonSignificant,
            bool excludeFragile,
            int minTermsPerCell,
            AeCorrelationMethod method,
            AeCorrelationAggregation aggregation,
            bool excludeCombos,
            int minEvents)
        {
            #region implementation

            ValidateCorrelationEnums(comparator, aggregation, method);
            if (minEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minEvents), minEvents, "Minimum events cannot be negative.");
            }

            return new AeSystemCorrelationFilters
            {
                Comparator = comparator,
                IncludeNonSignificant = includeNonSignificant,
                ExcludeFragile = excludeFragile,
                MinTermsPerCell = Math.Max(minTermsPerCell, 3),
                Method = method,
                Aggregation = aggregation,
                ExcludeCombos = excludeCombos,
                MinEvents = minEvents
            };

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public List<string> NormalizeSystemInputs(IEnumerable<string>? systems)
        {
            #region implementation

            if (systems == null)
            {
                return new List<string>();
            }

            return systems
                .Select(system => (system ?? string.Empty).Trim())
                .Where(system => !string.IsNullOrWhiteSpace(system))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public List<string> CanonicalizeSelectedSystems(
            IReadOnlyList<string> requestedSystems,
            IReadOnlyList<AeSystemCorrelationObservation> observations)
        {
            #region implementation

            var canonicalByKey = observations
                .GroupBy(observation => observation.SystemOrganClass, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key.ToLowerInvariant(),
                    group => group.First().SystemOrganClass,
                    StringComparer.OrdinalIgnoreCase);

            if (requestedSystems.Count == 0)
            {
                return canonicalByKey.Values
                    .OrderBy(system => system, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return requestedSystems
                .Select(system => canonicalByKey.TryGetValue(system.ToLowerInvariant(), out var canonical) ? canonical : null)
                .Where(system => !string.IsNullOrWhiteSpace(system))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public bool IsSeriousCorrelationSoc(string? soc)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(soc))
            {
                return false;
            }

            foreach (var token in AeDashboardMetadata.SocSerious)
            {
                var keyword = token
                    .Split(new[] { ' ', '&' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(keyword)
                    && soc.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Scoped product catalog service for AE dashboard reads.
    /// </summary>
    /// <remarks>
    /// This class is the injectable front door for catalog methods while
    /// <see cref="DtoLabelAccess"/> remains available to existing static callers.
    /// </remarks>
    /// <seealso cref="IAeDashboardProductCatalogService"/>
    public sealed class AeDashboardProductCatalogService : IAeDashboardProductCatalogService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardProductCatalogService"/> class.
        /// </summary>
        public AeDashboardProductCatalogService(
            ApplicationDbContext db,
            ILogger<AeDashboardProductCatalogService> logger)
            : this(db, (ILogger)logger)
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardProductCatalogService"/> class for compatibility callers.
        /// </summary>
        internal AeDashboardProductCatalogService(ApplicationDbContext db, ILogger logger)
        {
            #region implementation

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AeDrugSummaryDto>> GetDrugSummariesAsync(
            string pkSecret,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeDrugSummariesAsync(_db, pkSecret, _logger, productSearch, userId, page, size);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AeProductCatalogItemDto>> GetProductCatalogAsync(
            string pkSecret,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeProductCatalogAsync(_db, pkSecret, _logger, productSearch, userId, page, size);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<int> GetProductCountAsync()
        {
            #region implementation

            return DtoLabelAccess.GetAeProductCountAsync(_db, _logger);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Scoped product-detail service for AE dashboard reads.
    /// </summary>
    /// <seealso cref="IAeDashboardProductDetailService"/>
    public sealed class AeDashboardProductDetailService : IAeDashboardProductDetailService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardProductDetailService"/> class.
        /// </summary>
        public AeDashboardProductDetailService(
            ApplicationDbContext db,
            ILogger<AeDashboardProductDetailService> logger)
            : this(db, (ILogger)logger)
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardProductDetailService"/> class for compatibility callers.
        /// </summary>
        internal AeDashboardProductDetailService(ApplicationDbContext db, ILogger logger)
        {
            #region implementation

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AeRiskSignalDto>> GetRiskSignalsByDocumentAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            return DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(_db, documentGuid, pkSecret, _logger, comparator, includeFragile);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeDashboardProductDetailData?> GetProductDetailDataAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            return DtoLabelAccess.GetAeProductDetailDataAsync(_db, documentGuid, pkSecret, _logger, comparator, includeFragile);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeTriageViewDto?> GetTriageViewAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            return DtoLabelAccess.GetAeTriageViewAsync(_db, documentGuid, pkSecret, _logger, comparator, includeFragile);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeForestPlotDto?> GetForestPlotAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            return DtoLabelAccess.GetAeForestPlotAsync(_db, documentGuid, pkSecret, _logger, comparator, includeFragile);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeQuadrantViewDto?> GetQuadrantViewAsync(
            Guid documentGuid,
            string pkSecret,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            return DtoLabelAccess.GetAeQuadrantViewAsync(_db, documentGuid, pkSecret, _logger, comparator, includeFragile);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeReverseLookupResultDto> GetReverseLookupAsync(
            string symptom,
            string pkSecret,
            IEnumerable<Guid>? documentGuids = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeReverseLookupAsync(_db, symptom, pkSecret, _logger, documentGuids);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeInterchangeComparisonDto?> GetInterchangeAsync(
            Guid documentGuidA,
            Guid documentGuidB,
            string pkSecret,
            bool differencesOnly = false,
            bool sharedSignalsOnly = false,
            AeComparatorMix? comparator = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeInterchangeAsync(_db, documentGuidA, documentGuidB, pkSecret, _logger, differencesOnly, sharedSignalsOnly, comparator);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Scoped favorite service for AE dashboard reads and writes.
    /// </summary>
    /// <seealso cref="IAeDashboardFavoriteService"/>
    public sealed class AeDashboardFavoriteService : IAeDashboardFavoriteService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardFavoriteService"/> class.
        /// </summary>
        public AeDashboardFavoriteService(
            ApplicationDbContext db,
            ILogger<AeDashboardFavoriteService> logger)
            : this(db, (ILogger)logger)
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardFavoriteService"/> class for compatibility callers.
        /// </summary>
        internal AeDashboardFavoriteService(ApplicationDbContext db, ILogger logger)
        {
            #region implementation

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AeDrugSummaryDto>> GetFavoriteDrugSummariesAsync(
            long userId,
            string pkSecret,
            int? page = null,
            int? size = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(_db, userId, pkSecret, _logger, page, size);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<bool> SetProductFavoriteAsync(long userId, Guid documentGuid, bool isFavorite)
        {
            #region implementation

            return DtoLabelAccess.SetAeProductFavoriteAsync(_db, userId, documentGuid, isFavorite, _logger);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Scoped class-first correlation service for AE dashboard reads.
    /// </summary>
    /// <seealso cref="IAeDashboardClassCorrelationService"/>
    public sealed class AeDashboardClassCorrelationService : IAeDashboardClassCorrelationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardClassCorrelationService"/> class.
        /// </summary>
        public AeDashboardClassCorrelationService(
            ApplicationDbContext db,
            ILogger<AeDashboardClassCorrelationService> logger)
            : this(db, (ILogger)logger)
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardClassCorrelationService"/> class for compatibility callers.
        /// </summary>
        internal AeDashboardClassCorrelationService(ApplicationDbContext db, ILogger logger)
        {
            #region implementation

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeCorrelationMapDto?> GetCorrelationMapAsync(
            string pharmClassCode,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minDrugsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0)
        {
            #region implementation

            return DtoLabelAccess.GetAeCorrelationMapAsync(_db, pharmClassCode, pkSecret, _logger, comparator, includeNonSignificant, excludeFragile, minDrugsPerCell, method, aggregation, seriousSocOnly, excludeCombos, minEvents);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeCorrelationClassPickerPage> GetCorrelationClassesAsync(
            string pkSecret,
            string? classSearch = null,
            int? page = null,
            int? size = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            bool excludeCombos = false,
            int minEvents = 0,
            int minDrugsPerCell = 4,
            bool seriousSocOnly = false)
        {
            #region implementation

            return DtoLabelAccess.GetAeCorrelationClassesAsync(_db, pkSecret, _logger, classSearch, page, size, comparator, includeNonSignificant, excludeFragile, excludeCombos, minEvents, minDrugsPerCell, seriousSocOnly);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeCorrelationHeatmapDto?> GetCorrelationHeatmapAsync(
            string pharmClassCode,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0)
        {
            #region implementation

            return DtoLabelAccess.GetAeCorrelationHeatmapAsync(_db, pharmClassCode, pkSecret, _logger, comparator, includeNonSignificant, excludeFragile, aggregation, seriousSocOnly, excludeCombos, minEvents);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeCorrelationCellDetailDto?> GetCorrelationCellDetailAsync(
            string pharmClassCode,
            string socX,
            string socY,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minDrugsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0)
        {
            #region implementation

            return DtoLabelAccess.GetAeCorrelationCellDetailAsync(_db, pharmClassCode, socX, socY, pkSecret, _logger, comparator, includeNonSignificant, excludeFragile, minDrugsPerCell, method, aggregation, seriousSocOnly, excludeCombos, minEvents);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Scoped system-first correlation service for AE dashboard reads.
    /// </summary>
    /// <seealso cref="IAeDashboardSystemCorrelationService"/>
    public sealed class AeDashboardSystemCorrelationService : IAeDashboardSystemCorrelationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardSystemCorrelationService"/> class.
        /// </summary>
        public AeDashboardSystemCorrelationService(
            ApplicationDbContext db,
            ILogger<AeDashboardSystemCorrelationService> logger)
            : this(db, (ILogger)logger)
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AeDashboardSystemCorrelationService"/> class for compatibility callers.
        /// </summary>
        internal AeDashboardSystemCorrelationService(ApplicationDbContext db, ILogger logger)
        {
            #region implementation

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeSystemPickerPage> GetCorrelationSystemsAsync(
            string pkSecret,
            string? systemSearch = null,
            int? page = null,
            int? size = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            bool excludeCombos = false,
            int minEvents = 0,
            int minTermsPerCell = 4,
            string? classType = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeCorrelationSystemsAsync(_db, pkSecret, _logger, systemSearch, page, size, comparator, includeNonSignificant, excludeFragile, excludeCombos, minEvents, minTermsPerCell);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeSystemClassCorrelationMapDto?> GetSystemCorrelationMapAsync(
            IEnumerable<string> systems,
            string pkSecret,
            string? classSearch = null,
            int classPageNumber = 1,
            int classPageSize = 20,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minTermsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            bool includeFullMatrix = false,
            string? classType = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeSystemCorrelationMapAsync(_db, systems, pkSecret, _logger, classSearch, classPageNumber, classPageSize, comparator, includeNonSignificant, excludeFragile, minTermsPerCell, method, aggregation, excludeCombos, minEvents, includeFullMatrix, classType);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeSystemClassHeatmapDto?> GetSystemCorrelationHeatmapAsync(
            IEnumerable<string> systems,
            string pkSecret,
            string? classSearch = null,
            string? drugSearch = null,
            int? classPageNumber = null,
            int? classPageSize = null,
            int? drugPageNumber = null,
            int? drugPageSize = null,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            string? classType = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync(
                _db,
                systems,
                pkSecret,
                _logger,
                classSearch,
                drugSearch,
                classPageNumber ?? 1,
                classPageSize ?? 40,
                drugPageNumber ?? 1,
                drugPageSize ?? 50,
                comparator,
                includeNonSignificant,
                excludeFragile,
                aggregation,
                excludeCombos,
                minEvents,
                classType);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<AeSystemClassCorrelationCellDetailDto?> GetSystemCorrelationCellDetailAsync(
            IEnumerable<string> systems,
            string classX,
            string classY,
            string pkSecret,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            int minTermsPerCell = 4,
            AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool excludeCombos = false,
            int minEvents = 0,
            int pageNumber = 1,
            int pageSize = 100,
            string? classType = null)
        {
            #region implementation

            return DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync(_db, systems, classX, classY, pkSecret, _logger, comparator, includeNonSignificant, excludeFragile, minTermsPerCell, method, aggregation, excludeCombos, minEvents, pageNumber, pageSize);

            #endregion
        }
    }
}
