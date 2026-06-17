using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Partial data-access surface for AE dashboard read models.
    /// </summary>
    /// <remarks>
    /// These methods query the materialized AE risk table and product summary
    /// view, map rows into API-safe DTOs with encrypted integer identifiers, and
    /// call <see cref="AeDashboardDerivation"/> for deterministic dashboard fields.
    ///
    /// Comments in this partial class document the flow from EF query construction
    /// to materialization, DTO mapping, encrypted identifier projection, favorite
    /// enrichment, and derivation. The goal is to make clear which operations are
    /// executed in SQL, which run in memory, and why cache boundaries are drawn
    /// where they are.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDashboardDerivation"/>
    public static partial class DtoLabelAccess
    {
        #region AE Dashboard Public Read Methods

        /**************************************************************/
        /// <summary>
        /// Gets AE dashboard product summaries for the product picker and catalog.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="userId">Optional authenticated user identifier used to mark favorite products.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Product summary DTOs with derived scores and optional favorite flags.</returns>
        /// <remarks>
        /// Only anonymous, unfiltered, unpaged catalog results are cached. Search,
        /// paging, and user-specific favorite enrichment are intentionally not shared
        /// through the global cache.
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = await DtoLabelAccess.GetAeDrugSummariesAsync(db, secret, logger, "aspirin", userId);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        public static async Task<List<AeDrugSummaryDto>> GetAeDrugSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null)
        {
            #region implementation

            var stopwatch = Stopwatch.StartNew();
            var catalogReady = await hasMaterializedProductCatalogRowsAsync(db);
            List<AeDrugSummaryDto> summaries;

            if (catalogReady)
            {
                // The materialized product catalog is already at one row per
                // DocumentGUID. Keep search, ordering, and paging provider-side so
                // picker requests avoid the former full-catalog materialization.
                summaries = await getMaterializedProductCatalogSummariesAsync(
                    db,
                    pkSecret,
                    logger,
                    productSearch,
                    page,
                    size);
            }
            else
            {
                // Backward-compatible safety net for environments where Stage 5 has
                // not created/refreshed the catalog yet. This keeps null-class
                // fallback behavior intact while the materialized table rolls out.
                var catalog = await getCachedAeProductCatalogAsync(db, pkSecret, logger);
                summaries = cloneSummaries(catalog);

                if (!string.IsNullOrWhiteSpace(productSearch))
                {
                    var term = productSearch.Trim();
                    summaries = summaries
                        .Where(summary => matchesProductSearch(summary, term))
                        .ToList();
                }

                summaries = applyProductSummaryPagination(summaries, page, size);
            }

            // Favorite flags are user-specific and therefore applied only to the
            // private clones produced above.
            if (userId.HasValue)
            {
                // Load the user's favorite document set once and mark DTOs in
                // memory to avoid per-row database checks.
                var favoriteDocumentGuids = await loadFavoriteDocumentGuidsAsync(db, userId.Value);
                markFavorites(summaries, favoriteDocumentGuids);
            }

            stopwatch.Stop();
            logger.LogDebug(
                "AE dashboard products query completed in {ElapsedMs} ms (catalogReady={CatalogReady}, search={Search}, page={Page}, size={Size}, rows={Rows}).",
                stopwatch.ElapsedMilliseconds,
                catalogReady,
                !string.IsNullOrWhiteSpace(productSearch),
                page,
                size,
                summaries.Count);

            // Return cloned, optionally favorite-enriched product summaries.
            return summaries;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the slim AE dashboard product catalog for the product picker.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="userId">Optional authenticated user identifier used to mark favorite products.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>Slim catalog items carrying only the fields the picker renders.</returns>
        /// <remarks>
        /// Reuses the same shared, cached, per-document pipeline as
        /// <see cref="GetAeDrugSummariesAsync"/> (cache → clone → search → page →
        /// favorites) and then projects each summary to a smaller
        /// <see cref="AeProductCatalogItemDto"/>, keeping the picker payload light.
        /// </remarks>
        /// <example>
        /// <code>
        /// var catalog = await DtoLabelAccess.GetAeProductCatalogAsync(db, secret, logger, "advair", userId);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeDrugSummariesAsync"/>
        /// <seealso cref="AeProductCatalogItemDto"/>
        public static async Task<List<AeProductCatalogItemDto>> GetAeProductCatalogAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            string? productSearch = null,
            long? userId = null,
            int? page = null,
            int? size = null)
        {
            #region implementation

            // Reuse the full cached/cloned/searched/paged/favorited pipeline so the
            // catalog and the legacy products endpoint share one cache and one
            // derivation, then project to the slim picker shape.
            var summaries = await GetAeDrugSummariesAsync(db, pkSecret, logger, productSearch, userId, page, size);

            return summaries
                .Select(toCatalogItem)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the count of distinct AE dashboard products in the materialized risk table.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>The number of distinct product names available to the dashboard.</returns>
        /// <remarks>
        /// This is the real inventory baseline shown in the product-picker count badge.
        /// It counts <c>DISTINCT ProductName</c> straight from
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable"/> and intentionally does
        /// not reuse the collapsed catalog pipeline (which groups combination products by
        /// document), so the number matches the canonical inventory query exactly:
        /// <code>
        /// SELECT COUNT(*) FROM (SELECT DISTINCT ProductName FROM tmp_FlattenedAdverseEventRiskTable) a
        /// </code>
        /// The query is translated to SQL and executed without tracking.
        /// </remarks>
        /// <example>
        /// <code>
        /// var total = await DtoLabelAccess.GetAeProductCountAsync(db, logger);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeProductCatalogAsync"/>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        public static async Task<int> GetAeProductCountAsync(
            ApplicationDbContext db,
            ILogger logger)
        {
            #region implementation

            try
            {
                if (await hasMaterializedProductCatalogRowsAsync(db))
                {
                    // The materialized catalog is already one row per picker-ready
                    // product document, so the badge can use a simple COUNT(*).
                    return await db.Set<LabelView.AeDashboardProductCatalog>()
                        .AsNoTracking()
                        .CountAsync();
                }

                // Fallback for pre-catalog environments. The projection + Distinct
                // + Count composes to a single COUNT over a DISTINCT subquery.
                return await db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                    .AsNoTracking()
                    .Select(risk => risk.ProductName)
                    .Distinct()
                    .CountAsync();
            }
            catch (Exception ex)
            {
                // Surface the failure to the controller, which translates it into a 500.
                logger.LogError(ex, "Error counting distinct AE dashboard products.");
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets derived AE risk signals for one SPL document.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>Derived signal DTOs for the requested product.</returns>
        /// <remarks>
        /// This is the shared per-product signal source used by triage, forest,
        /// quadrant, reverse-lookup, and interchange assembly methods.
        /// </remarks>
        /// <example>
        /// <code>
        /// var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        /// <seealso cref="AeRiskSignalDto"/>
        public static async Task<List<AeRiskSignalDto>> GetAeRiskSignalsByDocumentAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Start from the materialized risk table and constrain by SPL document
            // in SQL before any DTO projection happens. vw_AeRisk is
            // left-preserving, so raw signal rows may intentionally have null
            // product/pharmacologic-class metadata.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.DocumentGUID == documentGuid);

            // Apply optional comparator filtering while the query is still
            // translated to SQL.
            query = applyComparatorFilter(query, comparator);

            // Materialize rows in deterministic AE-term/RR order for stable chart
            // and test output.
            var entities = await query
                .OrderBy(signal => signal.ParameterName)
                .ThenByDescending(signal => signal.RR)
                .ToListAsync();

            // Map rows into API-safe DTOs before running pure derivation logic.
            var signals = buildAeRiskSignalDtos(entities, pkSecret, logger);

            // Derive typed significance, flags, precision, and counseling tier for
            // every signal in the returned list.
            AeDashboardDerivation.DeriveSignals(signals);

            // The caller can suppress fragile rows after derivation because the
            // fragile decision depends on parsed flags and confidence intervals.
            if (!includeFragile)
            {
                signals = signals
                    .Where(signal => signal.PrecisionClass != AePrecisionClass.Fragile)
                    .ToList();
            }

            // Return only signals that survived document, comparator, and optional
            // fragile filtering.
            return signals;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the reusable product-detail payload for product-level AE dashboard views.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>Product context plus derived signals, or null when the product is absent.</returns>
        /// <remarks>
        /// Product-level tabs need the same product header and filtered signal list.
        /// This helper centralizes that load and caches the user-independent payload
        /// for triage, forest, and quadrant assembly. Callers receive clones so later
        /// derivation and sorting cannot mutate cached DTO instances.
        /// </remarks>
        /// <example>
        /// <code>
        /// var detail = await DtoLabelAccess.GetAeProductDetailDataAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="AeDashboardProductDetailData"/>
        public static async Task<AeDashboardProductDetailData?> GetAeProductDetailDataAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            var stopwatch = Stopwatch.StartNew();
            var product = await getAeDrugSummaryByDocumentGuidAsync(db, documentGuid, pkSecret, logger);
            if (product == null)
            {
                logger.LogDebug(
                    "AE dashboard detail query found no product for {DocumentGuid} in {ElapsedMs} ms.",
                    documentGuid,
                    stopwatch.ElapsedMilliseconds);
                return null;
            }

            var versionToken = await getAeProductDetailVersionTokenAsync(db, documentGuid);
            var cacheKey = generateCacheKey(
                nameof(GetAeProductDetailDataAsync),
                $"{documentGuid:N}:{comparator?.ToString() ?? "all"}:{includeFragile}:{versionToken}",
                null,
                null);

            var cached = Cached.GetCache<AeDashboardProductDetailData>(cacheKey);
            if (cached != null)
            {
                stopwatch.Stop();
                logger.LogDebug(
                    "AE dashboard detail cache hit for {DocumentGuid} in {ElapsedMs} ms (signals={Signals}).",
                    documentGuid,
                    stopwatch.ElapsedMilliseconds,
                    cached.Signals.Count);
                return cloneProductDetailData(cached);
            }

            var signals = await GetAeRiskSignalsByDocumentAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);
            var payload = new AeDashboardProductDetailData
            {
                Product = product,
                Signals = signals
            };

            if (signals.Count > 0)
            {
                Cached.SetCacheManageKey(cacheKey, cloneProductDetailData(payload), 1.0);
            }

            stopwatch.Stop();
            logger.LogDebug(
                "AE dashboard detail cache miss for {DocumentGuid}; loaded {Signals} signals in {ElapsedMs} ms.",
                documentGuid,
                signals.Count,
                stopwatch.ElapsedMilliseconds);

            return cloneProductDetailData(payload);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the tiered AE dashboard triage view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A triage view DTO, or null when the product is absent from dashboard risk data.</returns>
        /// <remarks>
        /// The method keeps controller work thin by loading product context, loading
        /// signals, and delegating all tier assembly to <see cref="AeDashboardDerivation"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var triage = await DtoLabelAccess.GetAeTriageViewAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="AeTriageViewDto"/>
        public static async Task<AeTriageViewDto?> GetAeTriageViewAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Load product context and filtered signals through the shared detail
            // helper so product tabs reuse one user-independent cache boundary.
            var detail = await GetAeProductDetailDataAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // A missing product summary means the document is not dashboard-ready,
            // so the controller can translate null into an appropriate response.
            if (detail == null)
            {
                return null;
            }

            // Delegate tier assembly and sort decisions to the pure derivation
            // helper.
            return AeDashboardDerivation.BuildTriageView(detail.Product, detail.Signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard forest plot view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A forest plot DTO, or null when the product is absent from dashboard risk data.</returns>
        /// <remarks>
        /// Signals are sorted by descending RR after comparator and fragile filters are
        /// applied.
        /// </remarks>
        /// <example>
        /// <code>
        /// var forest = await DtoLabelAccess.GetAeForestPlotAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeForestPlotDto"/>
        public static async Task<AeForestPlotDto?> GetAeForestPlotAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Load product context and filtered signals through the shared detail
            // helper so tab requests can reuse the same cache entry.
            var detail = await GetAeProductDetailDataAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // Null tells the caller there is no forest plot for this document.
            if (detail == null)
            {
                return null;
            }

            // The derivation helper owns forest-plot sorting and payload assembly.
            return AeDashboardDerivation.BuildForestPlot(detail.Signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard quadrant view for one product.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Optional comparator coverage filter.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be returned.</param>
        /// <returns>A quadrant view DTO, or null when the product is absent from dashboard risk data.</returns>
        /// <remarks>
        /// Coordinates are derived after query filtering and are clamped from zero to
        /// one for direct chart use.
        /// </remarks>
        /// <example>
        /// <code>
        /// var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeQuadrantViewDto"/>
        public static async Task<AeQuadrantViewDto?> GetAeQuadrantViewAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger,
            AeComparatorMix? comparator = null,
            bool includeFragile = true)
        {
            #region implementation

            // Load product context and filtered signals through the shared detail
            // helper so product tabs reuse one user-independent cache boundary.
            var detail = await GetAeProductDetailDataAsync(db, documentGuid, pkSecret, logger, comparator, includeFragile);

            // Missing product summary maps to null rather than an empty quadrant.
            if (detail == null)
            {
                return null;
            }

            // The derivation helper converts each signal into clamped chart points.
            return AeDashboardDerivation.BuildQuadrantView(detail.Signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets reverse-lookup AE dashboard matches for one symptom term.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="symptom">Adverse-event term to look up.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="documentGuids">Optional product scope limiting candidate documents.</param>
        /// <returns>A ranked symptom-to-product reverse lookup result.</returns>
        /// <remarks>
        /// The lookup matches <see cref="AeRiskSignalDto.ParameterName"/> case-insensitively,
        /// optionally scopes to supplied documents, joins product summaries, and ranks
        /// causal-looking rows ahead of reassuring rows.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = await DtoLabelAccess.GetAeReverseLookupAsync(db, "Headache", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeReverseLookupResultDto"/>
        public static async Task<AeReverseLookupResultDto> GetAeReverseLookupAsync(
            ApplicationDbContext db,
            string symptom,
            string pkSecret,
            ILogger logger,
            IEnumerable<Guid>? documentGuids = null)
        {
            #region implementation

            // A blank symptom cannot be matched against AE terms, so return an
            // empty, reassuring result without querying the database.
            if (string.IsNullOrWhiteSpace(symptom))
            {
                return new AeReverseLookupResultDto { Symptom = symptom, AllReassuring = true };
            }

            // Normalize once for the SQL-side exact term comparison.
            var normalizedSymptom = symptom.Trim().ToLower();

            // Distinct optional scope prevents duplicate document filters from
            // inflating generated SQL parameters.
            var scopedDocuments = documentGuids?.Distinct().ToList();

            // Build the exact-term risk query against the persisted normalized key
            // so SQL Server can use the reverse-lookup index.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.ParameterNameNormalized == normalizedSymptom);

            // If a product scope was supplied, keep only risk rows from those
            // documents before materialization.
            if (scopedDocuments is { Count: > 0 })
            {
                query = query.Where(signal => signal.DocumentGUID.HasValue && scopedDocuments.Contains(signal.DocumentGUID.Value));
            }

            // Materialize matched risk rows, then map/encrypt them in memory.
            var signalEntities = await query.ToListAsync();

            // Build DTOs only after materialization because encrypted identifiers
            // and derivation helpers cannot be translated into SQL.
            var signals = buildAeRiskSignalDtos(signalEntities, pkSecret, logger);

            // Extract the matched document set so product summaries can be loaded
            // only for products that actually had the searched AE term.
            var matchedDocuments = signals
                .Where(signal => signal.DocumentGUID.HasValue)
                .Select(signal => signal.DocumentGUID!.Value)
                .Distinct()
                .ToList();

            // Load product cards for the matched documents and keep product scoring
            // consistent with the product picker.
            var products = await getProductSummariesByDocumentGuidsAsync(db, matchedDocuments, pkSecret, logger);

            // Delegate verdict calculation and ranking to the pure derivation layer.
            return AeDashboardDerivation.BuildReverseLookupResult(symptom, products, signals);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an AE dashboard interchange comparison for two products.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuidA">SPL document identifier for product A.</param>
        /// <param name="documentGuidB">SPL document identifier for product B.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="differencesOnly">Whether rows classified as similar should be removed.</param>
        /// <param name="sharedSignalsOnly">Whether rows without signals on both products should be removed.</param>
        /// <param name="comparator">Optional comparator filter. Omit or use <see cref="AeComparatorMix.Both"/> to include all comparator strata.</param>
        /// <returns>An interchange comparison DTO, or null when either product is missing.</returns>
        /// <remarks>
        /// The comparison unions adverse-event terms across both products and delegates
        /// row-level classification to <see cref="AeDashboardDerivation"/> after
        /// applying the same comparator scope used by forest and quadrant views.
        /// </remarks>
        /// <example>
        /// <code>
        /// var comparison = await DtoLabelAccess.GetAeInterchangeAsync(db, a, b, secret, logger, true, true, AeComparatorMix.Placebo);
        /// </code>
        /// </example>
        /// <seealso cref="AeInterchangeComparisonDto"/>
        public static async Task<AeInterchangeComparisonDto?> GetAeInterchangeAsync(
            ApplicationDbContext db,
            Guid documentGuidA,
            Guid documentGuidB,
            string pkSecret,
            ILogger logger,
            bool differencesOnly = false,
            bool sharedSignalsOnly = false,
            AeComparatorMix? comparator = null)
        {
            #region implementation

            // Fetch both product summaries together to avoid two separate summary
            // view queries and to guarantee the same mapping rules for both sides.
            var products = await getProductSummariesByDocumentGuidsAsync(
                db,
                new[] { documentGuidA, documentGuidB },
                pkSecret,
                logger);

            // Identify product A and product B by their requested document GUIDs
            // after the shared product query returns.
            var productA = products.FirstOrDefault(product => product.DocumentGUID == documentGuidA);

            // Product B is resolved independently so missing-side detection can
            // report a null comparison if either product is absent.
            var productB = products.FirstOrDefault(product => product.DocumentGUID == documentGuidB);

            // Interchange requires both products; missing either one means the
            // comparison cannot be assembled.
            if (productA == null || productB == null)
            {
                return null;
            }

            // Load each side's complete signal set before row-level comparison.
            var signalsA = await GetAeRiskSignalsByDocumentAsync(db, documentGuidA, pkSecret, logger, comparator);

            // Product B gets its own signal list so duplicate terms can be collapsed
            // separately by the interchange derivation helper.
            var signalsB = await GetAeRiskSignalsByDocumentAsync(db, documentGuidB, pkSecret, logger, comparator);

            // The derivation helper unions terms, classifies rows, and counts the
            // rendered comparison classes.
            return AeDashboardDerivation.BuildInterchangeComparison(
                productA,
                productB,
                signalsA,
                signalsB,
                differencesOnly,
                sharedSignalsOnly);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the SOC × SOC adverse-event correlation map for one pharmacologic class.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form).</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are retained before correlating.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before correlating.</param>
        /// <param name="minDrugsPerCell">Minimum drugs a cell needs for a coefficient (clamped to a floor of 3).</param>
        /// <param name="method">Correlation method; Spearman by default.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The correlation map, or null when the class has no usable rows.</returns>
        /// <remarks>
        /// The observation unit is a drug within the class, so each cell's sample size is the
        /// number of drugs with data in both SOCs — usually small. Correlating on LogRR with a
        /// single comparator, fragile-row exclusion, and a drugs-per-cell floor keeps the map
        /// from rendering confident color over noise.
        /// </remarks>
        /// <example>
        /// <code>
        /// var map = await DtoLabelAccess.GetAeCorrelationMapAsync(db, "N0000175076", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationMapDto"/>
        /// <seealso cref="AeDashboardDerivation.BuildCorrelationMap"/>
        public static async Task<AeCorrelationMapDto?> GetAeCorrelationMapAsync(
            ApplicationDbContext db,
            string pharmClassCode,
            string pkSecret,
            ILogger logger,
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

            // Reject undefined enum values up front so an out-of-range numeric value cannot
            // silently degrade (for example to "no comparator filter") in the pipeline below.
            validateCorrelationEnums(comparator, aggregation, method);

            // Carry the request knobs as one object that both filters input rows and is
            // echoed back to the client. The drugs-per-cell floor is clamped server-side.
            var filters = new AeCorrelationFilters
            {
                Comparator = comparator,
                IncludeNonSignificant = includeNonSignificant,
                ExcludeFragile = excludeFragile,
                MinDrugsPerCell = Math.Max(minDrugsPerCell, 3),
                Method = method,
                Aggregation = aggregation,
                SeriousSocOnly = seriousSocOnly,
                ExcludeCombos = excludeCombos,
                MinEvents = minEvents
            };

            // Build the drug-within-class observations; a missing class returns null (-> 404).
            var context = await buildCorrelationObservationsAsync(db, pharmClassCode, pkSecret, logger, filters);
            if (context == null)
            {
                return null;
            }

            // Hand the observations to the pure derivation layer for matrix assembly.
            return AeDashboardDerivation.BuildCorrelationMap(
                pharmClassCode,
                context.Value.PharmClassName,
                context.Value.EncryptedPharmacologicClassID,
                filters,
                context.Value.Observations,
                context.Value.Warnings);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the pharmacologic classes that have AE risk rows, for the correlation class picker.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="classSearch">Optional class code or name search text.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <param name="comparator">Comparator mix used to scope input rows; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are retained before map renderability checks.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before map renderability checks.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped before map renderability checks.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <param name="minDrugsPerCell">Minimum drugs an off-diagonal SOC pair needs to render (server floor 3).</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <returns>Pharmacologic class picker page ordered by map renderability and population, with total and chartable counts.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When enum or numeric filters are invalid.</exception>
        /// <remarks>
        /// Scoped to classes that actually have AE risk rows (the generic pharm-class summary
        /// is not AE-scoped). <see cref="AePharmClassPickerItemDto.HasRenderableMap"/> flags
        /// classes with at least one off-diagonal SOC pair meeting the current drugs-per-cell
        /// floor. Grouping and paging run in memory after filter-sensitive derivation.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classes = await DtoLabelAccess.GetAeCorrelationClassesAsync(db, secret, logger, "kinase");
        /// </code>
        /// </example>
        /// <seealso cref="AePharmClassPickerItemDto"/>
        /// <seealso cref="AeCorrelationClassPickerPage"/>
        public static async Task<AeCorrelationClassPickerPage> GetAeCorrelationClassesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
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

            // Lightweight observability for the in-memory grouping fan-out.
            var stopwatch = Stopwatch.StartNew();

            validateCorrelationEnums(comparator, AeCorrelationAggregation.MedianLogRr);
            if (minEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minEvents), minEvents, "Minimum events cannot be negative.");
            }

            // Only classed, categorized, document-backed AE rows can seed a correlation class.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.PharmClassCode != null
                    && signal.ParameterCategory != null
                    && signal.DocumentGUID.HasValue);

            // Comparator scoping must match the map endpoint before rows are materialized.
            query = applyComparatorFilter(query, comparator);

            // Optional provider-side search on class code or display name.
            if (!string.IsNullOrWhiteSpace(classSearch))
            {
                var pattern = $"%{classSearch.Trim()}%";
                query = query.Where(signal =>
                    EF.Functions.Like(signal.PharmClassCode!, pattern)
                    || EF.Functions.Like(signal.PharmClassName ?? string.Empty, pattern));
            }

            // Materialize the rows needed for the same derived filters used by the map pipeline.
            var entities = await query
                .OrderBy(signal => signal.PharmClassCode)
                .ThenBy(signal => signal.ParameterName)
                .ThenByDescending(signal => signal.RR)
                .ToListAsync();

            // Collapse multi-arm/duplicate strata without erasing pharmacologic-class fan-out.
            var collapsed = collapseToMostPoweredClassStratum(entities);
            var floor = Math.Max(minDrugsPerCell, 3);
            var pickerRows = new List<(string ClassCode, string? ClassName, int? ClassId, string DrugKey, string Soc)>();

            foreach (var entity in collapsed)
            {
                if (string.IsNullOrWhiteSpace(entity.PharmClassCode)
                    || string.IsNullOrWhiteSpace(entity.ParameterCategory)
                    || entity.RR is not double rr
                    || rr <= 0.0
                    || double.IsNaN(rr)
                    || double.IsInfinity(rr))
                {
                    continue;
                }

                var signal = AeDashboardDerivation.DeriveSignal(buildAeRiskSignalDto(entity, pkSecret, logger));
                var precision = signal.PrecisionClass ?? AePrecisionClass.Fragile;
                var riskSignificance = signal.RiskSignificance ?? AeRiskSignificance.NotSignificant;
                var events = (entity.EventsTreatment ?? 0.0) + (entity.EventsComparator ?? 0.0);

                if (excludeFragile && precision == AePrecisionClass.Fragile)
                {
                    continue;
                }

                if (!includeNonSignificant && riskSignificance == AeRiskSignificance.NotSignificant)
                {
                    continue;
                }

                if (excludeCombos && entity.IsCombo)
                {
                    continue;
                }

                if (minEvents > 0 && events < minEvents)
                {
                    continue;
                }

                if (seriousSocOnly && !isSeriousCorrelationSoc(entity.ParameterCategory))
                {
                    continue;
                }

                pickerRows.Add((
                    entity.PharmClassCode!,
                    entity.PharmClassName,
                    entity.PharmacologicClassID,
                    correlationDrugKey(entity.ActiveMoietyID, entity.DocumentGUID),
                    entity.ParameterCategory!));
            }

            // Group by class code in memory to count distinct drugs, SOCs, and usable SOC pairs.
            var items = pickerRows
                .GroupBy(row => row.ClassCode)
                .Select(group =>
                {
                    var drugSocLookup = group
                        .GroupBy(row => row.DrugKey, StringComparer.Ordinal)
                        .ToDictionary(
                            drugGroup => drugGroup.Key,
                            drugGroup => drugGroup
                                .Select(row => row.Soc)
                                .Where(soc => !string.IsNullOrWhiteSpace(soc))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase),
                            StringComparer.Ordinal);
                    var socs = group
                        .Select(row => row.Soc)
                        .Where(soc => !string.IsNullOrWhiteSpace(soc))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var totalOffDiagonalCellCount = socs.Count * (socs.Count - 1) / 2;
                    var usableMapCellCount = 0;
                    var maxPairCount = 0;

                    for (var i = 0; i < socs.Count; i++)
                    {
                        for (var j = i + 1; j < socs.Count; j++)
                        {
                            var leftSoc = socs[i];
                            var rightSoc = socs[j];
                            var pairCount = drugSocLookup.Values.Count(socSet =>
                                socSet.Contains(leftSoc) && socSet.Contains(rightSoc));

                            maxPairCount = Math.Max(maxPairCount, pairCount);
                            if (pairCount >= floor)
                            {
                                usableMapCellCount++;
                            }
                        }
                    }

                    var hasRenderableMap = usableMapCellCount > 0;
                    var renderabilityReason = hasRenderableMap
                        ? null
                        : totalOffDiagonalCellCount == 0
                            ? "Fewer than two SOCs remain under the active filters."
                            : $"No SOC pair meets the {floor}-drug floor.";
                    var className = group
                        .Select(row => row.ClassName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                    var classId = group
                        .Where(row => row.ClassId.HasValue)
                        .Select(row => row.ClassId)
                        .FirstOrDefault();

                    return new AePharmClassPickerItemDto
                    {
                        PharmClassCode = group.Key,
                        PharmClassName = className,
                        EncryptedPharmacologicClassID = encryptNullableInt(classId, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.PharmacologicClassID)),
                        DrugCount = drugSocLookup.Count,
                        SocCount = socs.Count,
                        TotalOffDiagonalCellCount = totalOffDiagonalCellCount,
                        UsableMapCellCount = usableMapCellCount,
                        MaxPairCount = maxPairCount,
                        HasRenderableMap = hasRenderableMap,
                        RenderabilityReason = renderabilityReason,
                        IsCorrelatable = hasRenderableMap
                    };
                })
                .OrderByDescending(item => item.HasRenderableMap)
                .ThenByDescending(item => item.UsableMapCellCount)
                .ThenByDescending(item => item.MaxPairCount)
                .ThenByDescending(item => item.DrugCount)
                .ThenByDescending(item => item.SocCount)
                .ThenBy(item => item.PharmClassName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.PharmClassCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            stopwatch.Stop();
            logger.LogDebug(
                "AE correlation class picker: {RowCount} rows collapsed to {CollapsedCount} strata and grouped into {ClassCount} classes (search: {HasSearch}, floor: {Floor}) in {ElapsedMs} ms.",
                entities.Count,
                collapsed.Count,
                items.Count,
                !string.IsNullOrWhiteSpace(classSearch),
                floor,
                stopwatch.ElapsedMilliseconds);

            var totalCount = items.Count;
            var chartableCount = items.Count(item => item.HasRenderableMap);

            // In-memory paging mirrors applyProductSummaryPagination.
            if (page.HasValue && size.HasValue && page.Value > 0 && size.Value > 0)
            {
                return new AeCorrelationClassPickerPage(
                    items
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value)
                    .ToList(),
                    totalCount,
                    chartableCount);
            }

            return new AeCorrelationClassPickerPage(items, totalCount, chartableCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the SOC × drug relative-risk heatmap for one pharmacologic class.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form).</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are retained.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The heatmap, or null when the class has no usable rows.</returns>
        /// <remarks>
        /// The honest small-n companion to <see cref="GetAeCorrelationMapAsync"/>; it stays
        /// meaningful when a class is too small to correlate.
        /// </remarks>
        /// <example>
        /// <code>
        /// var heatmap = await DtoLabelAccess.GetAeCorrelationHeatmapAsync(db, "N0000175076", secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationHeatmapDto"/>
        public static async Task<AeCorrelationHeatmapDto?> GetAeCorrelationHeatmapAsync(
            ApplicationDbContext db,
            string pharmClassCode,
            string pkSecret,
            ILogger logger,
            AeComparatorMix comparator = AeComparatorMix.Placebo,
            bool includeNonSignificant = true,
            bool excludeFragile = true,
            AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            bool seriousSocOnly = false,
            bool excludeCombos = false,
            int minEvents = 0)
        {
            #region implementation

            // Reject undefined enum values up front so an out-of-range numeric value cannot
            // silently degrade (for example to "no comparator filter") in the pipeline below.
            validateCorrelationEnums(comparator, aggregation);

            var filters = new AeCorrelationFilters
            {
                Comparator = comparator,
                IncludeNonSignificant = includeNonSignificant,
                ExcludeFragile = excludeFragile,
                Aggregation = aggregation,
                SeriousSocOnly = seriousSocOnly,
                ExcludeCombos = excludeCombos,
                MinEvents = minEvents
            };

            var context = await buildCorrelationObservationsAsync(db, pharmClassCode, pkSecret, logger, filters);
            if (context == null)
            {
                return null;
            }

            return AeDashboardDerivation.BuildCorrelationHeatmap(
                pharmClassCode,
                context.Value.PharmClassName,
                context.Value.EncryptedPharmacologicClassID,
                filters,
                context.Value.Observations,
                context.Value.Warnings);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the per-drug drill-down behind one SOC × SOC correlation cell.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form).</param>
        /// <param name="socX">Row SOC of the cell.</param>
        /// <param name="socY">Column SOC of the cell.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are retained.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="minDrugsPerCell">Minimum drugs for the map-safe coefficient (clamped to a floor of 3); the raw coefficient ignores it.</param>
        /// <param name="method">Correlation method used for the recomputed coefficient.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The cell drill-down, or null when the class has no usable rows.</returns>
        /// <remarks>
        /// Mirrors the triage/forest/quadrant drill pattern: it returns the per-drug paired
        /// observations behind one cell so the front end can explain "why is this cell 0.9?".
        /// </remarks>
        /// <example>
        /// <code>
        /// var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(db, "N0000175076", socX, socY, secret, logger);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationCellDetailDto"/>
        public static async Task<AeCorrelationCellDetailDto?> GetAeCorrelationCellDetailAsync(
            ApplicationDbContext db,
            string pharmClassCode,
            string socX,
            string socY,
            string pkSecret,
            ILogger logger,
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

            // Reject undefined enum values up front so an out-of-range numeric value cannot
            // silently degrade (for example to "no comparator filter") in the pipeline below.
            validateCorrelationEnums(comparator, aggregation, method);

            var filters = new AeCorrelationFilters
            {
                Comparator = comparator,
                IncludeNonSignificant = includeNonSignificant,
                ExcludeFragile = excludeFragile,
                // The cell drill-down applies the same clamped drugs-per-cell floor as the map
                // so a below-floor cell reads null in both payloads.
                MinDrugsPerCell = Math.Max(minDrugsPerCell, 3),
                Method = method,
                Aggregation = aggregation,
                SeriousSocOnly = seriousSocOnly,
                ExcludeCombos = excludeCombos,
                MinEvents = minEvents
            };

            var context = await buildCorrelationObservationsAsync(db, pharmClassCode, pkSecret, logger, filters);
            if (context == null)
            {
                return null;
            }

            return AeDashboardDerivation.BuildCorrelationCellDetail(
                pharmClassCode,
                context.Value.PharmClassName,
                socX,
                socY,
                filters,
                context.Value.Observations,
                context.Value.Warnings);

            #endregion
        }

        #endregion AE Dashboard Public Read Methods

        #region AE Dashboard Private Query Helpers

        /**************************************************************/
        /// <summary>
        /// Determines whether the materialized product catalog currently has rows.
        /// </summary>
        private static async Task<bool> hasMaterializedProductCatalogRowsAsync(ApplicationDbContext db)
        {
            #region implementation

            // A non-empty catalog means Stage 5 has produced the picker-ready
            // document grain and callers should avoid the legacy full-view cache.
            return await db.Set<LabelView.AeDashboardProductCatalog>()
                .AsNoTracking()
                .AnyAsync();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a lightweight data-version token for product-detail cache keys.
        /// </summary>
        private static async Task<string> getAeProductDetailVersionTokenAsync(
            ApplicationDbContext db,
            Guid documentGuid)
        {
            #region implementation

            var refreshedAt = await db.Set<LabelView.AeDashboardProductCatalog>()
                .AsNoTracking()
                .Where(summary => summary.DocumentGUID == documentGuid)
                .Select(summary => (DateTime?)summary.RefreshedAt)
                .FirstOrDefaultAsync();

            if (refreshedAt.HasValue)
            {
                return $"catalog:{refreshedAt.Value.Ticks}";
            }

            var riskToken = await db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.DocumentGUID == documentGuid)
                .GroupBy(signal => signal.DocumentGUID)
                .Select(group => new
                {
                    Count = group.Count(),
                    MaxId = group.Max(signal => signal.Id)
                })
                .FirstOrDefaultAsync();

            return riskToken == null
                ? "empty"
                : $"risk:{riskToken.Count}:{riskToken.MaxId}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets provider-filtered and provider-paged product summaries from the catalog table.
        /// </summary>
        private static async Task<List<AeDrugSummaryDto>> getMaterializedProductCatalogSummariesAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger,
            string? productSearch,
            int? page,
            int? size)
        {
            #region implementation

            var query = db.Set<LabelView.AeDashboardProductCatalog>()
                .AsNoTracking();

            query = applyProductCatalogSearch(query, productSearch);
            query = applyProductCatalogOrdering(query);
            query = applyProductCatalogPagination(query, page, size);

            var entities = await query.ToListAsync();
            return buildAeDashboardProductCatalogDtos(entities, pkSecret, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets catalog-backed product summaries for a document GUID set.
        /// </summary>
        private static async Task<List<AeDrugSummaryDto>> getMaterializedProductCatalogSummariesByDocumentGuidsAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> documentGuids,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var guidList = documentGuids.Distinct().ToList();
            if (guidList.Count == 0)
            {
                return new List<AeDrugSummaryDto>();
            }

            var entities = await db.Set<LabelView.AeDashboardProductCatalog>()
                .AsNoTracking()
                .Where(summary => guidList.Contains(summary.DocumentGUID))
                .ToListAsync();

            return buildAeDashboardProductCatalogDtos(entities, pkSecret, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets one catalog-backed product summary by document GUID.
        /// </summary>
        private static async Task<AeDrugSummaryDto?> getMaterializedProductCatalogSummaryAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var entity = await db.Set<LabelView.AeDashboardProductCatalog>()
                .AsNoTracking()
                .FirstOrDefaultAsync(summary => summary.DocumentGUID == documentGuid);

            return entity == null
                ? null
                : buildAeDashboardProductCatalogDto(entity, pkSecret, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies provider-side catalog search against the materialized search text.
        /// </summary>
        private static IQueryable<LabelView.AeDashboardProductCatalog> applyProductCatalogSearch(
            IQueryable<LabelView.AeDashboardProductCatalog> query,
            string? productSearch)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(productSearch))
            {
                return query;
            }

            var pattern = $"%{productSearch.Trim().ToLowerInvariant()}%";
            return query.Where(summary =>
                EF.Functions.Like(summary.SearchText ?? string.Empty, pattern)
                || EF.Functions.Like(summary.ProductName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PrimarySubstanceName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PrimaryUNII ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PrimaryPharmClassCode ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PrimaryPharmClassName ?? string.Empty, pattern));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies the deterministic materialized catalog order.
        /// </summary>
        private static IOrderedQueryable<LabelView.AeDashboardProductCatalog> applyProductCatalogOrdering(
            IQueryable<LabelView.AeDashboardProductCatalog> query)
        {
            #region implementation

            return query
                .OrderByDescending(summary => summary.SortSignificantElevatedCount)
                .ThenBy(summary => summary.SortProductName)
                .ThenBy(summary => summary.DocumentGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies provider-side paging to the materialized product catalog query.
        /// </summary>
        private static IQueryable<LabelView.AeDashboardProductCatalog> applyProductCatalogPagination(
            IQueryable<LabelView.AeDashboardProductCatalog> query,
            int? page,
            int? size)
        {
            #region implementation

            if (page.HasValue && size.HasValue && page.Value > 0 && size.Value > 0)
            {
                return query
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value);
            }

            return query;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds (or returns the cached) shared, user-independent AE product catalog.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>One derived, sorted summary per document, with standardized active ingredients.</returns>
        /// <remarks>
        /// The expensive work — querying <see cref="LabelView.AeDrugSummary"/>, merging
        /// risk-table fallback rows, collapsing per-(substance × class) strata into one
        /// row per document, building the active-ingredient list, scoring, and sorting —
        /// runs once and is cached under a managed key. Callers MUST clone the result
        /// before any per-user mutation (see <see cref="cloneSummaries"/>) because the
        /// cache hands back the live list and DTO instances.
        /// </remarks>
        /// <seealso cref="collapseToOneRowPerDocument"/>
        /// <seealso cref="cloneSummaries"/>
        private static async Task<List<AeDrugSummaryDto>> getCachedAeProductCatalogAsync(
            ApplicationDbContext db,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // A new version token keeps this per-document shape from colliding with
            // any older per-stratum cache entry.
            var cacheKey = generateCacheKey(nameof(getCachedAeProductCatalogAsync), "anonymous-catalog-by-document-v1", null, null);

            // Return the shared catalog when present. Callers clone before mutating.
            var cached = Cached.GetCache<List<AeDrugSummaryDto>>(cacheKey);
            if (cached != null)
            {
                logger.LogDebug("AE dashboard product catalog cache hit for {CacheKey} with {Count} rows.", cacheKey, cached.Count);
                return cached;
            }

            // Materialize every product-summary stratum (no search/page/user state).
            var entities = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .ToListAsync();
            var summaries = buildAeDrugSummaryDtos(entities, pkSecret, logger);

            // Add risk-table fallback summaries so null-class products (absent from
            // the summary view) remain discoverable and loadable.
            var representedDocumentGuids = summaries
                .Where(summary => summary.DocumentGUID.HasValue)
                .Select(summary => summary.DocumentGUID!.Value)
                .ToHashSet();
            var fallbackSummaries = await getRiskTableDrugSummariesAsync(
                db,
                documentGuids: null,
                excludedDocumentGuids: representedDocumentGuids,
                productSearch: null,
                pkSecret: pkSecret,
                logger: logger);
            summaries.AddRange(fallbackSummaries);

            // Collapse the per-(substance × class) strata into one row per document,
            // attaching the standardized active-ingredient list.
            var catalog = collapseToOneRowPerDocument(summaries);

            // Score after collapse so derivation operates on the representative row.
            AeDashboardDerivation.DeriveProducts(catalog);

            // Sort by most elevated findings first, then product name and GUID for
            // deterministic tie-breaking. Filtering/paging preserve this order.
            catalog = catalog
                .OrderByDescending(summary => summary.SignificantElevatedCount)
                .ThenBy(summary => summary.ProductName)
                .ThenBy(summary => summary.DocumentGUID)
                .ToList();

            // Cache only non-empty results to avoid pinning an empty catalog during
            // transient database or import states.
            if (catalog.Count > 0)
            {
                Cached.SetCacheManageKey(cacheKey, catalog, 1.0);
                logger.LogDebug("AE dashboard product catalog cache set for {CacheKey} with {Count} rows.", cacheKey, catalog.Count);
            }

            return catalog;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses per-(substance × class) summary strata into one row per document.
        /// </summary>
        /// <param name="strata">Mapped summary DTOs at the view's (substance × class) grain.</param>
        /// <returns>One representative summary per document, each with <see cref="AeDrugSummaryDto.ActiveIngredients"/>.</returns>
        /// <remarks>
        /// A combination product fans out to several strata in the summary view. This
        /// helper keeps a single deterministic representative per document (most AE
        /// rows, then most elevated signals, then class code, then ingredient id) for
        /// the count/score fields, attaches the standardized ingredient list across all
        /// strata, and mirrors the first ingredient's preferred ("[EPC]") class onto the
        /// flat fields for back-compatible consumers. Counts are NOT summed across
        /// strata — that would double-count the class fan-out.
        /// </remarks>
        /// <seealso cref="AeDashboardDerivation.BuildActiveIngredients"/>
        private static List<AeDrugSummaryDto> collapseToOneRowPerDocument(
            IEnumerable<AeDrugSummaryDto> strata)
        {
            #region implementation

            // Materialize once so the orphan and grouped passes do not re-enumerate a
            // lazy source.
            var rows = strata.ToList();
            var result = new List<AeDrugSummaryDto>();

            // Rows without a DocumentGUID cannot be grouped by document; pass them
            // through with a single-ingredient list so the shape stays consistent.
            foreach (var orphan in rows.Where(row => !row.DocumentGUID.HasValue))
            {
                orphan.ActiveIngredients = AeDashboardDerivation.BuildActiveIngredients(new[] { orphan });
                result.Add(orphan);
            }

            // One representative row per document carries the standardized ingredients.
            foreach (var group in rows
                .Where(row => row.DocumentGUID.HasValue)
                .GroupBy(row => row.DocumentGUID!.Value))
            {
                var documentRows = group.ToList();

                // Deterministic representative for the count/score fields.
                var representative = documentRows
                    .OrderByDescending(row => row.RowCount)
                    .ThenByDescending(row => row.SignificantElevatedCount)
                    .ThenBy(row => row.PharmClassCode, StringComparer.Ordinal)
                    .ThenBy(row => row.IngredientSubstanceID ?? int.MaxValue)
                    .First();

                // Standardized ingredient list across every stratum for the document.
                representative.ActiveIngredients = AeDashboardDerivation.BuildActiveIngredients(documentRows);

                // Mirror the first ingredient's EPC values onto the flat fields so the
                // header and any legacy consumer read the standardized class/substance.
                var primary = representative.ActiveIngredients.FirstOrDefault();
                if (primary != null)
                {
                    representative.SubstanceName = primary.SubstanceName ?? representative.SubstanceName;
                    representative.PharmClassName = primary.PharmClassName ?? representative.PharmClassName;
                    representative.PharmClassCode = primary.PharmClassCode ?? representative.PharmClassCode;
                }

                result.Add(representative);
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a deep-enough copy of one product summary DTO for per-request use.
        /// </summary>
        /// <param name="source">Cached summary instance to copy.</param>
        /// <returns>A new DTO whose scalar fields and ingredient list are independent of the source.</returns>
        /// <remarks>
        /// The product catalog cache returns the live list and DTO instances, so any
        /// per-user mutation (favorite marking) must occur on a copy. A fresh
        /// <see cref="AeDrugSummaryDto.ActiveIngredients"/> list is allocated so the
        /// shared cached list is never aliased.
        /// </remarks>
        private static AeDrugSummaryDto cloneSummary(AeDrugSummaryDto source)
        {
            #region implementation

            return new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = source.EncryptedActiveMoietyID,
                EncryptedIngredientSubstanceID = source.EncryptedIngredientSubstanceID,
                EncryptedPharmacologicClassID = source.EncryptedPharmacologicClassID,
                DocumentGUID = source.DocumentGUID,
                ProductName = source.ProductName,
                SubstanceName = source.SubstanceName,
                UNII = source.UNII,
                PharmClassCode = source.PharmClassCode,
                PharmClassName = source.PharmClassName,
                ActiveIngredients = source.ActiveIngredients?
                    .Select(ingredient => new AeActiveIngredientDto
                    {
                        SubstanceName = ingredient.SubstanceName,
                        UNII = ingredient.UNII,
                        PharmClassName = ingredient.PharmClassName,
                        PharmClassCode = ingredient.PharmClassCode
                    })
                    .ToList(),
                ArmN = source.ArmN,
                ComparatorN = source.ComparatorN,
                RowCount = source.RowCount,
                SignificantCount = source.SignificantCount,
                SignificantProtectiveCount = source.SignificantProtectiveCount,
                SignificantElevatedCount = source.SignificantElevatedCount,
                PlaceboCoverage = source.PlaceboCoverage,
                ActiveCoverage = source.ActiveCoverage,
                DoseCoverage = source.DoseCoverage,
                SocBreadth = source.SocBreadth,
                SocTotal = source.SocTotal,
                MonoComboMix = source.MonoComboMix,
                IsFavorite = source.IsFavorite,
                Score = source.Score,
                ScoreReason = source.ScoreReason
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Clones a sequence of product summary DTOs for per-request mutation.
        /// </summary>
        /// <param name="source">Cached summaries to copy.</param>
        /// <returns>A new list of independent summary copies.</returns>
        /// <seealso cref="cloneSummary"/>
        private static List<AeDrugSummaryDto> cloneSummaries(IEnumerable<AeDrugSummaryDto> source)
        {
            #region implementation

            return source.Select(cloneSummary).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an independent copy of a product-detail cache payload.
        /// </summary>
        private static AeDashboardProductDetailData cloneProductDetailData(AeDashboardProductDetailData source)
        {
            #region implementation

            return new AeDashboardProductDetailData
            {
                Product = cloneSummary(source.Product),
                Signals = source.Signals
                    .Select(cloneRiskSignal)
                    .ToList()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an independent copy of one risk-signal DTO.
        /// </summary>
        private static AeRiskSignalDto cloneRiskSignal(AeRiskSignalDto source)
        {
            #region implementation

            return new AeRiskSignalDto
            {
                EncryptedFlattenedAdverseEventRiskTableID = source.EncryptedFlattenedAdverseEventRiskTableID,
                EncryptedFlattenedAdverseEventTableID = source.EncryptedFlattenedAdverseEventTableID,
                EncryptedFlattenedStandardizedTableID = source.EncryptedFlattenedStandardizedTableID,
                ParameterName = source.ParameterName,
                ParameterCategory = source.ParameterCategory,
                Significance = source.Significance,
                NumberNeededType = source.NumberNeededType,
                UNII = source.UNII,
                ProductName = source.ProductName,
                DocumentGUID = source.DocumentGUID,
                ArmN = source.ArmN,
                ComparatorN = source.ComparatorN,
                EventsTreatment = source.EventsTreatment,
                EventsComparator = source.EventsComparator,
                RR = source.RR,
                RRLowerBound = source.RRLowerBound,
                RRUpperBound = source.RRUpperBound,
                LogRR = source.LogRR,
                LogRRLowerBound = source.LogRRLowerBound,
                LogRRUpperBound = source.LogRRUpperBound,
                NumberNeeded = source.NumberNeeded,
                NumberNeededLowerBound = source.NumberNeededLowerBound,
                NumberNeededUpperBound = source.NumberNeededUpperBound,
                IsPlaceboControlled = source.IsPlaceboControlled,
                IsCombo = source.IsCombo,
                CalculationFlags = source.CalculationFlags,
                StudyContext = source.StudyContext,
                Population = source.Population,
                Subpopulation = source.Subpopulation,
                Dose = source.Dose,
                DoseUnit = source.DoseUnit,
                PrecisionClass = source.PrecisionClass,
                IsSignificant = source.IsSignificant,
                IsProtective = source.IsProtective,
                RiskSignificance = source.RiskSignificance,
                NumberNeededKind = source.NumberNeededKind,
                Flags = source.Flags.ToList(),
                CounselingTier = source.CounselingTier
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a product summary matches a free-text search term.
        /// </summary>
        /// <param name="summary">Catalog summary to test.</param>
        /// <param name="term">Trimmed, case-insensitive search term.</param>
        /// <returns>True when product, substance, UNII, class, or any ingredient matches.</returns>
        /// <remarks>
        /// Replicates the former <see cref="applyProductSearch"/> SQL LIKE fields in
        /// memory over the cached catalog and additionally matches every aggregated
        /// active ingredient so combination ingredients stay searchable after collapse.
        /// </remarks>
        private static bool matchesProductSearch(AeDrugSummaryDto summary, string term)
        {
            #region implementation

            // Local helper keeps the per-field comparison terse and null-safe.
            bool containsTerm(string? value) =>
                !string.IsNullOrEmpty(value)
                && value.Contains(term, StringComparison.OrdinalIgnoreCase);

            if (containsTerm(summary.ProductName)
                || containsTerm(summary.SubstanceName)
                || containsTerm(summary.UNII)
                || containsTerm(summary.PharmClassCode)
                || containsTerm(summary.PharmClassName))
            {
                return true;
            }

            // Combination ingredients remain searchable after per-document collapse.
            if (summary.ActiveIngredients != null)
            {
                foreach (var ingredient in summary.ActiveIngredients)
                {
                    if (containsTerm(ingredient.SubstanceName)
                        || containsTerm(ingredient.UNII)
                        || containsTerm(ingredient.PharmClassName)
                        || containsTerm(ingredient.PharmClassCode))
                    {
                        return true;
                    }
                }
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies product, substance, UNII, and class search to the product summary query.
        /// </summary>
        private static IQueryable<LabelView.AeDrugSummary> applyProductSearch(
            IQueryable<LabelView.AeDrugSummary> query,
            string? productSearch)
        {
            #region implementation

            // Leave the original query untouched when no search term is supplied;
            // this keeps anonymous catalog caching and ordering behavior unchanged.
            if (string.IsNullOrWhiteSpace(productSearch))
            {
                return query;
            }

            // Wrap the trimmed term in SQL LIKE wildcards so one input can match
            // product names, substance names, UNII values, or class metadata.
            var pattern = $"%{productSearch.Trim()}%";

            // Keep this as an IQueryable expression so EF translates the search
            // predicates into SQL instead of filtering after materialization.
            return query.Where(summary =>
                EF.Functions.Like(summary.ProductName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.SubstanceName ?? string.Empty, pattern)
                || EF.Functions.Like(summary.UNII ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PharmClassCode ?? string.Empty, pattern)
                || EF.Functions.Like(summary.PharmClassName ?? string.Empty, pattern));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies product, substance, UNII, and class search to the risk-table fallback query.
        /// </summary>
        private static IQueryable<LabelView.FlattenedAdverseEventRiskTable> applyRiskProductSearch(
            IQueryable<LabelView.FlattenedAdverseEventRiskTable> query,
            string? productSearch)
        {
            #region implementation

            // The fallback search mirrors vw_AeDrugSummary search fields so a
            // null-class product can be found by product name, ingredient, UNII,
            // or whatever class metadata it does have.
            if (string.IsNullOrWhiteSpace(productSearch))
            {
                return query;
            }

            // Wrap the trimmed term in SQL LIKE wildcards for provider-side
            // filtering before fallback rows are aggregated in memory.
            var pattern = $"%{productSearch.Trim()}%";
            return query.Where(signal =>
                EF.Functions.Like(signal.ProductName ?? string.Empty, pattern)
                || EF.Functions.Like(signal.SubstanceName ?? string.Empty, pattern)
                || EF.Functions.Like(signal.UNII ?? string.Empty, pattern)
                || EF.Functions.Like(signal.PharmClassCode ?? string.Empty, pattern)
                || EF.Functions.Like(signal.PharmClassName ?? string.Empty, pattern));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies in-memory paging to merged product summary DTOs.
        /// </summary>
        private static List<AeDrugSummaryDto> applyProductSummaryPagination(
            IEnumerable<AeDrugSummaryDto> summaries,
            int? page,
            int? size)
        {
            #region implementation

            // Paging occurs after summary-view rows and fallback rows are merged;
            // invalid or missing page arguments keep the full ordered list.
            if (page.HasValue && size.HasValue && page.Value > 0 && size.Value > 0)
            {
                return summaries
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value)
                    .ToList();
            }

            return summaries.ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies comparator mix filtering to materialized AE risk rows.
        /// </summary>
        private static IQueryable<LabelView.FlattenedAdverseEventRiskTable> applyComparatorFilter(
            IQueryable<LabelView.FlattenedAdverseEventRiskTable> query,
            AeComparatorMix? comparator)
        {
            #region implementation

            // Convert the optional comparator enum into a SQL predicate. Only two values mean
            // "no comparator filter": an unspecified (null) comparator and the explicit Both mode
            // (whose mixed-estimand caveat is surfaced to clients as a warning). Any other,
            // undefined value throws rather than silently mixing placebo and active comparators.
            return comparator switch
            {
                AeComparatorMix.Placebo => query.Where(signal => signal.IsPlaceboControlled),
                AeComparatorMix.Active => query.Where(signal => !signal.IsPlaceboControlled),
                AeComparatorMix.Both => query,
                null => query,
                _ => throw new ArgumentOutOfRangeException(nameof(comparator), comparator, "Unsupported comparator mix.")
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the supplied correlation enum values are defined before they reach the pipeline.
        /// </summary>
        /// <param name="comparator">Comparator mix to validate.</param>
        /// <param name="aggregation">Within-SOC aggregation to validate.</param>
        /// <param name="method">Optional correlation method to validate (heatmap omits it).</param>
        /// <exception cref="ArgumentOutOfRangeException">When any value is outside its enum definition.</exception>
        /// <remarks>
        /// A second line of defense behind the controller's request validation: model binding can
        /// produce an undefined enum from an out-of-range numeric query value, and silently treating
        /// such a value as a default (for example "no comparator filter") would mix estimands. The
        /// controller rejects these with 400; this guard keeps direct/internal callers from
        /// reaching <see cref="applyComparatorFilter"/> or the derivation layer with bad input.
        /// </remarks>
        /// <seealso cref="applyComparatorFilter"/>
        private static void validateCorrelationEnums(
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
        /// <summary>
        /// Gets one aggregated AE product summary by document GUID.
        /// </summary>
        /// <remarks>
        /// Loads every (substance × pharmacologic-class) stratum for the document and
        /// collapses them into one summary with the standardized active-ingredient
        /// list, rather than picking an arbitrary single stratum. This is what gives
        /// the triage/forest/quadrant header every ingredient and the preferred
        /// ("[EPC]") class.
        /// </remarks>
        private static async Task<AeDrugSummaryDto?> getAeDrugSummaryByDocumentGuidAsync(
            ApplicationDbContext db,
            Guid documentGuid,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var catalogSummary = await getMaterializedProductCatalogSummaryAsync(db, documentGuid, pkSecret, logger);
            if (catalogSummary != null)
            {
                return catalogSummary;
            }

            // Load ALL summary strata for the document so combination products
            // aggregate every ingredient instead of one arbitrary class row.
            var entities = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .Where(summary => summary.DocumentGUID == documentGuid)
                .ToListAsync();

            if (entities.Count > 0)
            {
                // Map, collapse to a single per-document row with ActiveIngredients,
                // then derive score fields for the product-level view builder.
                var summaries = buildAeDrugSummaryDtos(entities, pkSecret, logger);
                var collapsed = collapseToOneRowPerDocument(summaries).FirstOrDefault();
                return collapsed != null ? AeDashboardDerivation.DeriveProduct(collapsed) : null;
            }

            // Missing summary rows can happen for labels whose AE rows have no
            // pharmacologic-class context, so fall back to the risk table before
            // treating the product as absent.
            var fallbackSummaries = await getRiskTableDrugSummariesAsync(
                db,
                documentGuids: new[] { documentGuid },
                excludedDocumentGuids: null,
                productSearch: null,
                pkSecret: pkSecret,
                logger: logger);

            // Collapse the fallback strata too so null-class products still expose a
            // standardized ingredient list to the header.
            var collapsedFallback = collapseToOneRowPerDocument(fallbackSummaries).FirstOrDefault();
            return collapsedFallback != null
                ? AeDashboardDerivation.DeriveProduct(collapsedFallback)
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets mapped AE product summaries by document GUID set.
        /// </summary>
        private static async Task<List<AeDrugSummaryDto>> getProductSummariesByDocumentGuidsAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> documentGuids,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // De-duplicate document GUID input so SQL contains the smallest useful
            // IN-list and the mapper sees each product at most once.
            var guidList = documentGuids.Distinct().ToList();

            // Avoid an unnecessary database round trip when no documents need
            // product summary rows.
            if (guidList.Count == 0)
            {
                return new List<AeDrugSummaryDto>();
            }

            var catalogSummaries = await getMaterializedProductCatalogSummariesByDocumentGuidsAsync(
                db,
                guidList,
                pkSecret,
                logger);
            var catalogDocumentGuids = catalogSummaries
                .Where(summary => summary.DocumentGUID.HasValue)
                .Select(summary => summary.DocumentGUID!.Value)
                .ToHashSet();
            var missingFromCatalog = guidList
                .Where(documentGuid => !catalogDocumentGuids.Contains(documentGuid))
                .ToList();

            if (catalogSummaries.Count > 0 && missingFromCatalog.Count == 0)
            {
                return catalogSummaries;
            }

            // Load all matching product summary rows as read-only EF data.
            var entities = await db.Set<LabelView.AeDrugSummary>()
                .AsNoTracking()
                .Where(summary => summary.DocumentGUID.HasValue && missingFromCatalog.Contains(summary.DocumentGUID.Value))
                .ToListAsync();

            // Convert rows to encrypted DTOs and calculate product score fields.
            var summaries = buildAeDrugSummaryDtos(entities, pkSecret, logger);
            var representedDocumentGuids = summaries
                .Where(summary => summary.DocumentGUID.HasValue)
                .Select(summary => summary.DocumentGUID!.Value)
                .ToHashSet();
            var missingDocumentGuids = guidList
                .Where(documentGuid => !representedDocumentGuids.Contains(documentGuid))
                .ToList();
            var fallbackSummaries = await getRiskTableDrugSummariesAsync(
                db,
                documentGuids: missingDocumentGuids,
                excludedDocumentGuids: null,
                productSearch: null,
                pkSecret: pkSecret,
                logger: logger);
            summaries.AddRange(fallbackSummaries);

            // Collapse the per-(substance × class) strata into one row per document so
            // a combination product yields a single summary with a standardized
            // ingredient list. Without this, callers keying by DocumentGUID (the
            // favorites ToDictionary) throw on duplicate keys. Mirrors
            // getCachedAeProductCatalogAsync and getAeDrugSummaryByDocumentGuidAsync,
            // which already collapse before scoring.
            var collapsed = collapseToOneRowPerDocument(summaries);
            var derivedFallbacks = AeDashboardDerivation.DeriveProducts(collapsed);
            catalogSummaries.AddRange(derivedFallbacks);
            return catalogSummaries;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds product summary DTOs directly from materialized risk rows.
        /// </summary>
        private static async Task<List<AeDrugSummaryDto>> getRiskTableDrugSummariesAsync(
            ApplicationDbContext db,
            IEnumerable<Guid>? documentGuids,
            IReadOnlySet<Guid>? excludedDocumentGuids,
            string? productSearch,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Start with concrete dashboard rows; rows without DocumentGUID cannot
            // back product-level dashboard navigation or favorites.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.DocumentGUID.HasValue);

            // Optional document scoping keeps product-level fallback queries small.
            var guidList = documentGuids?
                .Distinct()
                .ToList();
            if (guidList is { Count: > 0 })
            {
                query = query.Where(signal => signal.DocumentGUID.HasValue && guidList.Contains(signal.DocumentGUID.Value));
            }

            // Skip documents already represented by the summary view so fallback
            // rows supplement missing null-class products without duplicating the
            // ordinary summary catalog.
            var excludedGuidList = excludedDocumentGuids?
                .ToList();
            if (excludedGuidList is { Count: > 0 })
            {
                query = query.Where(signal => signal.DocumentGUID.HasValue && !excludedGuidList.Contains(signal.DocumentGUID.Value));
            }

            // Mirror product-summary search semantics for fallback-only products.
            query = applyRiskProductSearch(query, productSearch);

            // The aggregate projection includes encrypted IDs and enum parsing, so
            // materialize the filtered rows and aggregate in memory.
            var entities = await query.ToListAsync();
            return buildFallbackAeDrugSummaryDtos(entities, pkSecret, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads favorite document GUIDs for one authenticated user.
        /// </summary>
        private static async Task<HashSet<Guid>> loadFavoriteDocumentGuidsAsync(
            ApplicationDbContext db,
            long userId)
        {
            #region implementation

            // Load only the persisted document GUIDs because favorite marking only
            // needs set membership by document.
            var favoriteDocumentGuids = await db.AspNetUserFavorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .Select(favorite => favorite.DocumentGUID)
                .ToListAsync();

            // A HashSet makes per-product favorite checks O(1) during DTO marking.
            return favoriteDocumentGuids.ToHashSet();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Marks product summary DTOs as favorites using a loaded favorite set.
        /// </summary>
        private static void markFavorites(
            IEnumerable<AeDrugSummaryDto> summaries,
            IReadOnlySet<Guid> favoriteDocumentGuids)
        {
            #region implementation

            // Walk the already-materialized product DTOs and set IsFavorite based on
            // the user's favorite document set.
            foreach (var summary in summaries)
            {
                // Products without a DocumentGUID cannot be favorited because the
                // favorite table stores DocumentGUID as its product key.
                summary.IsFavorite = summary.DocumentGUID.HasValue
                    && favoriteDocumentGuids.Contains(summary.DocumentGUID.Value);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds drug-within-class correlation observations for one pharmacologic class.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pharmClassCode">Pharmacologic class code scoping the query.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="filters">Applied filters used both to scope SQL and drop input rows.</param>
        /// <returns>Surviving observations plus resolved class context and warnings, or null when nothing survives.</returns>
        /// <remarks>
        /// This is the single source for all four correlation read methods. It reuses
        /// <see cref="applyComparatorFilter"/>, <see cref="collapseToMostPoweredStratum"/>, and
        /// <see cref="buildAeRiskSignalDto"/>, then runs each surviving row through
        /// <see cref="AeDashboardDerivation.DeriveSignal"/> to get precision and significance.
        /// LogRR is computed in memory as <c>entity.LogRR ?? Math.Log(entity.RR)</c> because the
        /// persisted log column is null in seeded rows; rows with a non-positive RR are skipped.
        /// </remarks>
        /// <seealso cref="AeCorrelationObservation"/>
        /// <seealso cref="GetAeCorrelationMapAsync"/>
        private static async Task<(List<AeCorrelationObservation> Observations, string? PharmClassName, string? EncryptedPharmacologicClassID, List<string> Warnings)?> buildCorrelationObservationsAsync(
            ApplicationDbContext db,
            string pharmClassCode,
            string pkSecret,
            ILogger logger,
            AeCorrelationFilters filters)
        {
            #region implementation

            // Lightweight observability: large pharmacologic classes materialize every matching
            // risk row before collapse/filter, so log the row counts and elapsed time at Debug
            // to make an expensive class request diagnosable before any caching is considered.
            var stopwatch = Stopwatch.StartNew();

            // Scope to classed, categorized, document-backed AE rows in SQL.
            var query = db.Set<LabelView.FlattenedAdverseEventRiskTable>()
                .AsNoTracking()
                .Where(signal => signal.PharmClassCode == pharmClassCode
                    && signal.ParameterCategory != null
                    && signal.DocumentGUID.HasValue);

            // Apply the comparator mix while the query is still translated to SQL.
            query = applyComparatorFilter(query, filters.Comparator);

            // Deterministic order mirrors GetAeRiskSignalsByDocumentAsync for stable output.
            var entities = await query
                .OrderBy(signal => signal.ParameterName)
                .ThenByDescending(signal => signal.RR)
                .ToListAsync();
            if (entities.Count == 0)
            {
                return null;
            }

            // Collapse class fan-out and multi-arm duplication to one row per clinical stratum.
            var collapsed = collapseToMostPoweredStratum(entities);

            // Build one observation per surviving row, deriving precision/significance and
            // reading moiety/class/combo/events directly off the entity.
            var observations = new List<AeCorrelationObservation>();
            foreach (var entity in collapsed)
            {
                // Guard RR before log math; the persisted LogRR is null in seeded rows.
                if (entity.RR is not double rr || rr <= 0.0 || double.IsNaN(rr) || double.IsInfinity(rr))
                {
                    continue;
                }

                var logRr = entity.LogRR ?? Math.Log(rr);
                if (double.IsNaN(logRr) || double.IsInfinity(logRr))
                {
                    continue;
                }

                // DeriveSignal reuses ClassifyPrecision/ParseRiskSignificance off the mapped DTO.
                var signal = AeDashboardDerivation.DeriveSignal(buildAeRiskSignalDto(entity, pkSecret, logger));

                observations.Add(new AeCorrelationObservation
                {
                    DrugKey = correlationDrugKey(entity.ActiveMoietyID, entity.DocumentGUID),
                    EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                    DrugDisplayName = !string.IsNullOrWhiteSpace(entity.SubstanceName) ? entity.SubstanceName : entity.ProductName,
                    DocumentGUID = entity.DocumentGUID,
                    Soc = entity.ParameterCategory!,
                    LogRr = logRr,
                    Rr = rr,
                    Precision = signal.PrecisionClass ?? AePrecisionClass.Fragile,
                    RiskSignificance = signal.RiskSignificance ?? AeRiskSignificance.NotSignificant,
                    IsCombo = entity.IsCombo,
                    Events = (entity.EventsTreatment ?? 0.0) + (entity.EventsComparator ?? 0.0)
                });
            }

            // Apply the input-row filters that depend on derived or contextual fields.
            IEnumerable<AeCorrelationObservation> filtered = observations;
            if (filters.ExcludeFragile)
            {
                filtered = filtered.Where(observation => observation.Precision != AePrecisionClass.Fragile);
            }

            if (!filters.IncludeNonSignificant)
            {
                filtered = filtered.Where(observation => observation.RiskSignificance != AeRiskSignificance.NotSignificant);
            }

            if (filters.ExcludeCombos)
            {
                filtered = filtered.Where(observation => !observation.IsCombo);
            }

            if (filters.MinEvents > 0)
            {
                filtered = filtered.Where(observation => observation.Events >= filters.MinEvents);
            }

            if (filters.SeriousSocOnly)
            {
                filtered = filtered.Where(observation => isSeriousCorrelationSoc(observation.Soc));
            }

            var survivors = filtered.ToList();
            if (survivors.Count == 0)
            {
                return null;
            }

            // Class name and id are stable across the class; take the first populated values.
            var pharmClassName = entities
                .Select(entity => entity.PharmClassName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            var pharmacologicClassId = entities
                .Where(entity => entity.PharmacologicClassID.HasValue)
                .Select(entity => entity.PharmacologicClassID)
                .FirstOrDefault();
            var encryptedClassId = encryptNullableInt(
                pharmacologicClassId,
                pkSecret,
                logger,
                nameof(LabelView.FlattenedAdverseEventRiskTable.PharmacologicClassID));

            // Collect the honesty warnings shared by every correlation payload.
            var warnings = new List<string>();
            if (filters.Comparator == AeComparatorMix.Both)
            {
                warnings.Add("Cells may mix placebo-controlled and active-controlled estimates.");
            }

            if (!filters.ExcludeFragile && survivors.Any(observation => observation.Precision == AePrecisionClass.Fragile))
            {
                warnings.Add("Fragile or wide-confidence-interval rows are included; interpret affected cells with care.");
            }

            var distinctDrugs = survivors.Select(observation => observation.DrugKey).Distinct().Count();
            if (distinctDrugs < 2)
            {
                warnings.Add($"This class has {distinctDrugs} drug(s) with usable rows; a correlation is not computable, so use the heatmap.");
            }

            stopwatch.Stop();
            logger.LogDebug(
                "AE correlation observations for class {PharmClassCode} (comparator {Comparator}): {RowCount} rows before collapse, {SurvivorCount} survivors after filters, {DrugCount} drugs across {SocCount} SOCs in {ElapsedMs} ms.",
                pharmClassCode,
                filters.Comparator,
                entities.Count,
                survivors.Count,
                distinctDrugs,
                survivors.Select(observation => observation.Soc).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                stopwatch.ElapsedMilliseconds);

            return (survivors, pharmClassName, encryptedClassId, warnings);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the stable per-drug key used as the correlation observation unit.
        /// </summary>
        /// <param name="activeMoietyId">Active moiety identifier, when present.</param>
        /// <param name="documentGuid">Source document identifier used as a fallback key.</param>
        /// <returns>A deterministic key keyed on the moiety, or the document when the moiety is null.</returns>
        /// <remarks>
        /// Keying on active moiety collapses two SPL labels of one molecule into a single drug;
        /// the document-derived fallback keeps moiety-less rows distinct without a runtime-unstable
        /// hash. Shared by the observation builder and the class picker so both count drugs the same way.
        /// </remarks>
        /// <seealso cref="AeDashboardDerivation.StableDrugKey"/>
        private static string correlationDrugKey(int? activeMoietyId, Guid? documentGuid)
        {
            #region implementation

            if (activeMoietyId.HasValue)
            {
                return $"moiety:{activeMoietyId.Value}";
            }

            return documentGuid.HasValue
                ? AeDashboardDerivation.StableDrugKey(documentGuid.Value)
                : "unknown";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a SOC is treated as a serious organ system for the axis filter.
        /// </summary>
        /// <param name="soc">The SOC (ParameterCategory) to test.</param>
        /// <returns>True when the SOC approximately matches a serious-organ-system keyword.</returns>
        /// <remarks>
        /// TODO: replace this approximate keyword match against
        /// <see cref="AeDashboardMetadata.SocSerious"/> with a proper MedDRA SOC → seriousness map.
        /// The metadata tokens are abbreviations (e.g. "Cardiac") while ParameterCategory holds the
        /// full SOC names (e.g. "Cardiac Disorders"), so the first keyword of each token is matched
        /// as a substring.
        /// </remarks>
        private static bool isSeriousCorrelationSoc(string? soc)
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

        #endregion AE Dashboard Private Query Helpers

        #region AE Dashboard Private Mapping Helpers

        /**************************************************************/
        /// <summary>
        /// Projects an aggregated product summary into the slim picker catalog item.
        /// </summary>
        /// <param name="summary">A per-document, scored, favorite-marked summary DTO.</param>
        /// <returns>A slim <see cref="AeProductCatalogItemDto"/> carrying only picker fields.</returns>
        /// <remarks>
        /// The flat <see cref="AeDrugSummaryDto.SubstanceName"/> and
        /// <see cref="AeDrugSummaryDto.PharmClassName"/> are already standardized to the
        /// first ingredient's preferred ("[EPC]") values by
        /// <see cref="collapseToOneRowPerDocument"/>, so they are copied directly.
        /// </remarks>
        /// <seealso cref="AeProductCatalogItemDto"/>
        private static AeProductCatalogItemDto toCatalogItem(AeDrugSummaryDto summary)
        {
            #region implementation

            return new AeProductCatalogItemDto
            {
                DocumentGUID = summary.DocumentGUID,
                ProductName = summary.ProductName,
                SubstanceName = summary.SubstanceName,
                UNII = summary.UNII,
                PharmClassName = summary.PharmClassName,
                ActiveIngredients = summary.ActiveIngredients,
                MonoComboMix = summary.MonoComboMix,
                Score = summary.Score,
                PlaceboCoverage = summary.PlaceboCoverage,
                ActiveCoverage = summary.ActiveCoverage,
                IsFavorite = summary.IsFavorite
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds product summary DTOs from materialized catalog rows.
        /// </summary>
        private static List<AeDrugSummaryDto> buildAeDashboardProductCatalogDtos(
            IEnumerable<LabelView.AeDashboardProductCatalog> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities
                .Select(entity => buildAeDashboardProductCatalogDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one product summary DTO from a materialized catalog row.
        /// </summary>
        private static AeDrugSummaryDto buildAeDashboardProductCatalogDto(
            LabelView.AeDashboardProductCatalog entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var dto = new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(entity.IngredientSubstanceID, pkSecret, logger, nameof(entity.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(entity.PharmacologicClassID, pkSecret, logger, nameof(entity.PharmacologicClassID)),
                DocumentGUID = entity.DocumentGUID,
                ProductName = entity.ProductName,
                SubstanceName = entity.PrimarySubstanceName,
                UNII = entity.PrimaryUNII,
                PharmClassCode = entity.PrimaryPharmClassCode,
                PharmClassName = entity.PrimaryPharmClassName,
                ActiveIngredients = parseCatalogActiveIngredients(entity.ActiveIngredientsJson, logger),
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                RowCount = entity.RowCount,
                SignificantCount = entity.SignificantCount,
                SignificantProtectiveCount = entity.SignificantProtectiveCount,
                SignificantElevatedCount = entity.SignificantElevatedCount,
                PlaceboCoverage = entity.PlaceboCoverage,
                ActiveCoverage = entity.ActiveCoverage,
                DoseCoverage = entity.DoseCoverage,
                SocBreadth = entity.SocBreadth,
                SocTotal = entity.SocTotal > 0 ? entity.SocTotal : AeDashboardMetadata.SocTotal,
                MonoComboMix = parseMonoComboMix(entity.MonoComboMix),
                Score = entity.Score,
                ScoreReason = entity.ScoreReason
            };

            return dto.Score.HasValue && !string.IsNullOrWhiteSpace(dto.ScoreReason)
                ? dto
                : AeDashboardDerivation.DeriveProduct(dto);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses catalog ingredient JSON into dashboard ingredient DTOs.
        /// </summary>
        private static List<AeActiveIngredientDto>? parseCatalogActiveIngredients(
            string? activeIngredientsJson,
            ILogger logger)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(activeIngredientsJson))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<List<AeActiveIngredientDto>>(activeIngredientsJson);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unable to parse AE dashboard product catalog active ingredients JSON.");
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE product summary DTOs from EF rows.
        /// </summary>
        private static List<AeDrugSummaryDto> buildAeDrugSummaryDtos(
            IEnumerable<LabelView.AeDrugSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Map each EF product summary row through the single-row mapper so
            // encryption and default handling stay centralized.
            return entities
                .Select(entity => buildAeDrugSummaryDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds fallback AE product summary DTOs from risk-table rows.
        /// </summary>
        private static List<AeDrugSummaryDto> buildFallbackAeDrugSummaryDtos(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Match vw_AeDrugSummary grouping so fallback rows have the same
            // product/substance/class grain as refreshed summary-view rows.
            return entities
                .GroupBy(entity => (
                    DocumentGUID: entity.DocumentGUID,
                    ProductName: entity.ProductName,
                    SubstanceName: entity.SubstanceName,
                    UNII: entity.UNII,
                    PharmClassCode: entity.PharmClassCode,
                    PharmClassName: entity.PharmClassName,
                    ActiveMoietyID: entity.ActiveMoietyID,
                    IngredientSubstanceID: entity.IngredientSubstanceID,
                    PharmacologicClassID: entity.PharmacologicClassID))
                .Select(group => buildFallbackAeDrugSummaryDto(group, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one fallback AE product summary DTO from grouped risk-table rows.
        /// </summary>
        private static AeDrugSummaryDto buildFallbackAeDrugSummaryDto(
            IGrouping<(Guid? DocumentGUID, string? ProductName, string? SubstanceName, string? UNII, string? PharmClassCode, string? PharmClassName, int? ActiveMoietyID, int? IngredientSubstanceID, int? PharmacologicClassID), LabelView.FlattenedAdverseEventRiskTable> group,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Materialize the grouping once so aggregate helpers do not repeatedly
            // enumerate the same in-memory rows.
            var rows = group.ToList();
            var key = group.Key;
            var rowCount = rows.Count;

            // Fallback summaries intentionally mirror vw_AeDrugSummary aggregate
            // columns, while preserving null pharmacologic-class identifiers.
            return new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = encryptNullableInt(key.ActiveMoietyID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(key.IngredientSubstanceID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(key.PharmacologicClassID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.PharmacologicClassID)),
                DocumentGUID = key.DocumentGUID,
                ProductName = key.ProductName,
                SubstanceName = key.SubstanceName,
                UNII = key.UNII,
                PharmClassCode = key.PharmClassCode,
                PharmClassName = key.PharmClassName,
                ArmN = rows.Max(row => row.ArmN),
                ComparatorN = rows.Max(row => row.ComparatorN),
                RowCount = rowCount,
                SignificantCount = rows.Count(row => isSignificantAeSignal(row.Significance)),
                SignificantProtectiveCount = rows.Count(row => string.Equals(row.Significance, "protective", StringComparison.OrdinalIgnoreCase)),
                SignificantElevatedCount = rows.Count(row => string.Equals(row.Significance, "elevated", StringComparison.OrdinalIgnoreCase)),
                PlaceboCoverage = rows.Any(row => row.IsPlaceboControlled),
                ActiveCoverage = rows.Any(row => !row.IsPlaceboControlled),
                DoseCoverage = rowCount > 0
                    ? rows.Count(row => row.Dose.HasValue) / (double)rowCount
                    : 0.0,
                SocBreadth = rows
                    .Select(row => row.ParameterCategory)
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                SocTotal = AeDashboardMetadata.SocTotal,
                MonoComboMix = getMonoComboMix(rows)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE product summary DTO from an EF row.
        /// </summary>
        private static AeDrugSummaryDto buildAeDrugSummaryDto(
            LabelView.AeDrugSummary entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Build the client DTO directly from the summary view. Integer database
            // identifiers are encrypted before they leave the data-access layer.
            return new AeDrugSummaryDto
            {
                // Active moiety, ingredient, and pharmacologic-class IDs are masked
                // according to the existing DTO encrypted-ID convention.
                EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(entity.IngredientSubstanceID, pkSecret, logger, nameof(entity.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(entity.PharmacologicClassID, pkSecret, logger, nameof(entity.PharmacologicClassID)),

                // Document and product identity fields remain readable because they
                // are already public dashboard keys or display text.
                DocumentGUID = entity.DocumentGUID,
                ProductName = entity.ProductName,
                SubstanceName = entity.SubstanceName,
                UNII = entity.UNII,
                PharmClassCode = entity.PharmClassCode,
                PharmClassName = entity.PharmClassName,

                // Denominator and row-count fields are numeric metrics, not raw row
                // identifiers, so they stay visible in the client DTO.
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                RowCount = entity.RowCount,
                SignificantCount = entity.SignificantCount,
                SignificantProtectiveCount = entity.SignificantProtectiveCount,
                SignificantElevatedCount = entity.SignificantElevatedCount,

                // Coverage values are consumed by score derivation and product
                // picker filtering.
                PlaceboCoverage = entity.PlaceboCoverage,
                ActiveCoverage = entity.ActiveCoverage,
                DoseCoverage = (double)entity.DoseCoverage,
                SocBreadth = entity.SocBreadth,

                // Fall back to dashboard metadata when the view does not provide a
                // positive SOC total.
                SocTotal = entity.SocTotal > 0 ? entity.SocTotal : AeDashboardMetadata.SocTotal,

                // Convert the view's text classification into the dashboard enum.
                MonoComboMix = parseMonoComboMix(entity.MonoComboMix)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a raw significance value contributes to summary counts.
        /// </summary>
        private static bool isSignificantAeSignal(string? significance)
        {
            #region implementation

            // The summary view counts elevated and protective intervals as
            // significant; fallback aggregation keeps the same interpretation.
            return string.Equals(significance, "elevated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(significance, "protective", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Derives the mono/combo mix from materialized risk-table rows.
        /// </summary>
        private static AeMonoComboMix getMonoComboMix(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            // Match vw_AeDrugSummary: all combo rows produce Combo, all mono rows
            // produce Mono, and mixed source rows produce Mixed.
            var hasCombo = entities.Any(entity => entity.IsCombo);
            var hasMono = entities.Any(entity => !entity.IsCombo);

            return hasCombo && hasMono
                ? AeMonoComboMix.Mixed
                : hasCombo
                    ? AeMonoComboMix.Combo
                    : AeMonoComboMix.Mono;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE risk signal DTOs from EF rows.
        /// </summary>
        private static List<AeRiskSignalDto> buildAeRiskSignalDtos(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Collapse duplicate risk rows to one signal per viewer-visible clinical stratum
            // before mapping. This removes both the pharmacologic-class fan-out from
            // class-enriched vw_AeRisk rows (the product/substance context is stable, but a
            // substance can still map to N pharmacologic classes) and the multi-arm
            // duplication where the same term/dose/comparator is reported for both a pooled arm
            // and a smaller, unlabeled subgroup arm. Collapsing at the entity grain also avoids
            // encrypting throwaway rows.
            return collapseToMostPoweredStratum(entities)
                .Select(entity => buildAeRiskSignalDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses duplicate AE risk rows to one representative per clinical stratum.
        /// </summary>
        /// <param name="entities">Materialized risk-table rows for one or more documents.</param>
        /// <returns>One row per (document, term, SOC, dose, comparator, study/population context), keeping the most statistically powered arm.</returns>
        /// <remarks>
        /// Two sources of duplication are removed here. First, class-enriched
        /// <c>vw_AeRisk</c> rows can fan out because one product/substance can map to several
        /// pharmacologic classes, emitting identical copies of every AE statistic except the
        /// class columns. Left-preserved rows without class context pass through as a single
        /// raw risk signal. Second,
        /// the same adverse event at the same dose and comparator can be reported for both a
        /// pooled arm and a smaller subgroup arm that carries no
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable.Population"/> or
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable.Subpopulation"/> label, which
        /// renders as the same signal with conflicting numbers.
        ///
        /// The grouping key intentionally includes study context, population, and subpopulation
        /// so genuinely labeled strata stay distinct; only rows a reader cannot tell apart are
        /// merged. Within a group the representative is the most statistically powered arm
        /// (largest treatment denominator), preferring a significant, tighter-CI row on ties,
        /// with the source row identifier as a final deterministic tiebreaker.
        /// </remarks>
        /// <seealso cref="buildAeRiskSignalDtos"/>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        private static List<LabelView.FlattenedAdverseEventRiskTable> collapseToMostPoweredStratum(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            // Group by the clinical identity a dashboard reader actually sees. Class and moiety
            // identifiers are deliberately excluded so class-fan-out copies merge; study and
            // population context is included so legitimately labeled strata are preserved.
            return entities
                .GroupBy(entity => new
                {
                    entity.DocumentGUID,
                    entity.ParameterName,
                    entity.ParameterCategory,
                    entity.Dose,
                    entity.DoseUnit,
                    entity.IsPlaceboControlled,
                    entity.StudyContext,
                    entity.Population,
                    entity.Subpopulation
                })
                // Keep the most statistically powered arm as the stratum representative so the
                // pooled, tighter-CI estimate wins over a small unlabeled subgroup.
                .Select(group => group
                    .OrderByDescending(entity => entity.ArmN ?? 0)
                    .ThenByDescending(entity => entity.ComparatorN ?? 0)
                    .ThenBy(entity => string.Equals(entity.Significance, "not significant", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(entity => (entity.RRUpperBound ?? double.MaxValue) - (entity.RRLowerBound ?? 0.0))
                    .ThenBy(entity => entity.FlattenedAdverseEventTableId)
                    .First())
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses duplicate AE strata while preserving pharmacologic-class fan-out.
        /// </summary>
        /// <param name="entities">Materialized AE risk rows for one or more pharmacologic classes.</param>
        /// <returns>Representative rows with one most-powered row per visible class/clinical stratum.</returns>
        /// <remarks>
        /// The all-class picker needs the same multi-arm de-duplication as the map pipeline, but
        /// it must not use <see cref="collapseToMostPoweredStratum"/> because that helper
        /// deliberately merges identical rows across class fan-out. Including
        /// <see cref="LabelView.FlattenedAdverseEventRiskTable.PharmClassCode"/> keeps each class
        /// eligible for renderability calculations.
        /// </remarks>
        /// <seealso cref="collapseToMostPoweredStratum"/>
        /// <seealso cref="GetAeCorrelationClassesAsync"/>
        private static List<LabelView.FlattenedAdverseEventRiskTable> collapseToMostPoweredClassStratum(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            return entities
                .GroupBy(entity => new
                {
                    entity.PharmClassCode,
                    entity.DocumentGUID,
                    entity.ParameterName,
                    entity.ParameterCategory,
                    entity.Dose,
                    entity.DoseUnit,
                    entity.IsPlaceboControlled,
                    entity.StudyContext,
                    entity.Population,
                    entity.Subpopulation
                })
                .Select(group => group
                    .OrderByDescending(entity => entity.ArmN ?? 0)
                    .ThenByDescending(entity => entity.ComparatorN ?? 0)
                    .ThenBy(entity => string.Equals(entity.Significance, "not significant", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(entity => (entity.RRUpperBound ?? double.MaxValue) - (entity.RRLowerBound ?? 0.0))
                    .ThenBy(entity => entity.FlattenedAdverseEventTableId)
                    .First())
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE risk signal DTO from an EF row.
        /// </summary>
        private static AeRiskSignalDto buildAeRiskSignalDto(
            LabelView.FlattenedAdverseEventRiskTable entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Build the risk-signal DTO directly from the risk table row. Raw
            // integer table IDs are encrypted while statistical and context fields
            // remain readable for dashboard rendering.
            return new AeRiskSignalDto
            {
                // These encrypted identifiers let clients request detail/drill-in
                // later without exposing database integer keys.
                EncryptedFlattenedAdverseEventRiskTableID = encryptNullableInt(entity.Id, pkSecret, logger, nameof(entity.Id)),
                EncryptedFlattenedAdverseEventTableID = encryptNullableInt(entity.FlattenedAdverseEventTableId, pkSecret, logger, nameof(entity.FlattenedAdverseEventTableId)),
                EncryptedFlattenedStandardizedTableID = encryptNullableInt(entity.FlattenedStandardizedTableId, pkSecret, logger, nameof(entity.FlattenedStandardizedTableId)),

                // AE term and product context identify what the row means.
                ParameterName = entity.ParameterName,
                ParameterCategory = entity.ParameterCategory,
                Significance = entity.Significance,
                NumberNeededType = entity.NumberNeededType,
                UNII = entity.UNII,
                ProductName = entity.ProductName,
                DocumentGUID = entity.DocumentGUID,

                // Denominators and event counts support downstream precision,
                // number-needed, and bubble-size derivation.
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                EventsTreatment = entity.EventsTreatment,
                EventsComparator = entity.EventsComparator,

                // Relative-risk statistics are copied as calculated by Stage 5 so
                // derivation can classify direction and chart coordinates.
                RR = entity.RR,
                RRLowerBound = entity.RRLowerBound,
                RRUpperBound = entity.RRUpperBound,
                LogRR = entity.LogRR,
                LogRRLowerBound = entity.LogRRLowerBound,
                LogRRUpperBound = entity.LogRRUpperBound,

                // Number-needed fields are displayed after the derivation layer
                // classifies NNH versus NNT.
                NumberNeeded = entity.NumberNeeded,
                NumberNeededLowerBound = entity.NumberNeededLowerBound,
                NumberNeededUpperBound = entity.NumberNeededUpperBound,

                // Comparator and combo flags drive filtering and product summaries.
                IsPlaceboControlled = entity.IsPlaceboControlled,
                IsCombo = entity.IsCombo,
                CalculationFlags = entity.CalculationFlags,

                // Study, population, and dose context remain display metadata for
                // signal detail panels.
                StudyContext = entity.StudyContext,
                Population = entity.Population,
                Subpopulation = entity.Subpopulation,
                Dose = entity.Dose,
                DoseUnit = entity.DoseUnit
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a nullable integer identifier for client-safe DTO exposure.
        /// </summary>
        private static string? encryptNullableInt(
            int? value,
            string pkSecret,
            ILogger logger,
            string fieldName)
        {
            #region implementation

            // Null source IDs stay null on the DTO so callers can distinguish
            // absent relationships from encryption failures.
            if (!value.HasValue)
            {
                return null;
            }

            // Encryption can fail if the secret is invalid; isolate that failure to
            // the affected field and log the identifier name for diagnostics.
            try
            {
                // Fast strength matches the existing DTO ID masking convention.
                return StringCipher.Encrypt(value.Value.ToString(), pkSecret, StringCipher.EncryptionStrength.Fast);
            }
            // Encryption failures are caught per field so one bad ID does not fail
            // the entire AE dashboard payload.
            catch (Exception ex)
            {
                // Returning null prevents a bad identifier from breaking the whole
                // dashboard response while still preserving an error log.
                logger.LogError(ex, "Failed to encrypt AE dashboard identifier {FieldName} with value {Value}.", fieldName, value.Value);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the mono/combo text persisted by the product summary view.
        /// </summary>
        private static AeMonoComboMix? parseMonoComboMix(string? monoComboMix)
        {
            #region implementation

            // Normalize the view text before matching so casing and surrounding
            // whitespace do not change the enum result.
            return monoComboMix?.Trim().ToLowerInvariant() switch
            {
                "mono" => AeMonoComboMix.Mono,
                "combo" => AeMonoComboMix.Combo,
                "mixed" => AeMonoComboMix.Mixed,
                _ => null
            };

            #endregion
        }

        #endregion AE Dashboard Private Mapping Helpers
    }
}
