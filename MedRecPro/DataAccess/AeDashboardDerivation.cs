using MedRecPro.Models;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Provides deterministic AE dashboard derivation and assembly helpers.
    /// </summary>
    /// <remarks>
    /// This class contains pure functions only. It does not access EF Core,
    /// configuration providers, loggers, caches, clocks, or mutable static state.
    /// Data-access methods pass mapped DTOs through these helpers so controllers
    /// do not duplicate scoring, tiering, quadrant, reverse-lookup, or interchange
    /// logic.
    ///
    /// The comments in this file intentionally document each local data-shaping
    /// step because these helpers are the handoff point between persisted Stage 5
    /// values and the dashboard's display contract. Future maintainers should be
    /// able to trace how raw view columns become typed enums, ranking values,
    /// warning labels, chart coordinates, and user-facing explanations.
    /// </remarks>
    /// <example>
    /// <code>
    /// var derived = AeDashboardDerivation.DeriveSignal(signal);
    /// var triage = AeDashboardDerivation.BuildTriageView(product, signals);
    /// </code>
    /// </example>
    /// <seealso cref="AeRiskSignalDto"/>
    /// <seealso cref="AeDrugSummaryDto"/>
    public static class AeDashboardDerivation
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Populates derived fields on one AE dashboard signal.
        /// </summary>
        /// <param name="signal">Signal DTO to enrich.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>The same <see cref="AeRiskSignalDto"/> instance after enrichment.</returns>
        /// <remarks>
        /// The derivation maps persisted significance and calculation flags into typed
        /// dashboard fields, then classifies precision and counseling tier.
        /// </remarks>
        /// <example>
        /// <code>
        /// var signal = AeDashboardDerivation.DeriveSignal(rawSignal);
        /// </code>
        /// </example>
        /// <seealso cref="AeRiskSignalDto"/>
        /// <seealso cref="AeDashboardDerivationSettings"/>
        public static AeRiskSignalDto DeriveSignal(
            AeRiskSignalDto signal,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Use the dashboard defaults whenever the caller does not provide an
            // override, keeping all thresholds centralized in the settings object.
            settings ??= AeDashboardDerivationSettings.Default;

            // Convert the persisted free-text significance into the typed enum
            // that the rest of the dashboard logic can compare safely.
            signal.RiskSignificance = ParseRiskSignificance(signal.Significance);

            // Treat elevated and protective rows as statistically significant,
            // while leaving "not significant" rows available for reassurance UI.
            signal.IsSignificant = signal.RiskSignificance == AeRiskSignificance.Elevated
                || signal.RiskSignificance == AeRiskSignificance.Protective;

            // Protective rows are significant, but they should sort and render
            // differently from elevated risk rows.
            signal.IsProtective = signal.RiskSignificance == AeRiskSignificance.Protective;

            // Convert the persisted NNH/NNT label into a typed value before tier
            // and reverse-lookup logic read the number-needed fields.
            signal.NumberNeededKind = ParseNumberNeededType(signal.NumberNeededType);

            // Normalize Stage 5 calculation diagnostics into the compact dashboard
            // flag list used by precision and display decisions.
            signal.Flags = ParseFlags(signal.CalculationFlags);

            // Precision must be calculated before counseling tier because fragile
            // evidence overrides otherwise causal-looking direction.
            signal.PrecisionClass = ClassifyPrecision(signal, settings);

            // Counseling tier is the final single-signal rollup consumed by the
            // triage cards.
            signal.CounselingTier = ClassifyCounselingTier(signal);

            // Return the same instance so callers can keep object identity when
            // they pass lists that are already referenced elsewhere.
            return signal;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Populates derived fields on a sequence of AE dashboard signals.
        /// </summary>
        /// <param name="signals">Signal DTOs to enrich.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A materialized list of enriched signal DTOs.</returns>
        /// <remarks>
        /// This helper preserves input object identity and order while ensuring every
        /// signal has typed flags, significance, precision, and counseling tier.
        /// </remarks>
        /// <example>
        /// <code>
        /// var signals = AeDashboardDerivation.DeriveSignals(rawSignals);
        /// </code>
        /// </example>
        /// <seealso cref="DeriveSignal(AeRiskSignalDto, AeDashboardDerivationSettings?)"/>
        public static List<AeRiskSignalDto> DeriveSignals(
            IEnumerable<AeRiskSignalDto> signals,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Materialize immediately so every caller receives a stable list of
            // enriched signals rather than a deferred query that could re-run.
            return signals
                .Select(signal => DeriveSignal(signal, settings))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Populates product-level AE dashboard score fields.
        /// </summary>
        /// <param name="product">Product summary DTO to enrich.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>The same <see cref="AeDrugSummaryDto"/> instance after enrichment.</returns>
        /// <remarks>
        /// The score is a bounded, deterministic chart-worthiness indicator based on
        /// comparator coverage, elevated-signal density, dose coverage, SOC breadth,
        /// and row volume.
        /// </remarks>
        /// <example>
        /// <code>
        /// var product = AeDashboardDerivation.DeriveProduct(summary);
        /// </code>
        /// </example>
        /// <seealso cref="AeDrugSummaryDto"/>
        /// <seealso cref="AeDashboardScoreWeights"/>
        public static AeDrugSummaryDto DeriveProduct(
            AeDrugSummaryDto product,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Use the same threshold object as per-signal derivation unless the
            // caller intentionally provides a scenario-specific override.
            settings ??= AeDashboardDerivationSettings.Default;

            // Guard every denominator used by the score so empty or malformed
            // source rows cannot produce divide-by-zero values.
            var rowCount = Math.Max(product.RowCount, 1);

            // SOC total is independently guarded because some labels may have row
            // counts but incomplete SOC aggregation.
            var socTotal = Math.Max(product.SocTotal, 1);

            // Row target is guarded so configuration cannot accidentally make row
            // volume division invalid.
            var rowTarget = Math.Max(settings.ScoreRowCountTarget, 1);

            // Convert boolean comparator coverage into fractional score inputs.
            var placeboCoverage = product.PlaceboCoverage ? 1.0 : 0.0;

            // Active-comparator coverage is tracked separately because it has a
            // smaller score weight but matters for interchange confidence.
            var activeCoverage = product.ActiveCoverage ? 1.0 : 0.0;

            // Significant elevated density rewards products with actionable risk
            // findings while capping the contribution at full credit.
            var elevatedDensity = Math.Min((double)product.SignificantElevatedCount / rowCount, 1.0);

            // Dose coverage and SOC breadth may come from aggregate SQL ratios, so
            // clamp them before they are multiplied into the weighted score.
            var doseCoverage = clamp(product.DoseCoverage, 0.0, 1.0);

            // SOC breadth is normalized by the guarded SOC total to create a
            // fractional coverage measure.
            var socBreadth = clamp((double)product.SocBreadth / socTotal, 0.0, 1.0);

            // Row volume is capped so large labels do not dominate the chart
            // worthiness score solely because they have many AE rows.
            var rowVolume = Math.Min((double)product.RowCount / rowTarget, 1.0);

            // Combine normalized score inputs with configured weights and scale
            // the result onto the 0-100 dashboard display range.
            var score = 100.0 * (
                settings.ScoreWeights.PlaceboCoverage * placeboCoverage
                + settings.ScoreWeights.ActiveCoverage * activeCoverage
                + settings.ScoreWeights.SignificantElevatedDensity * elevatedDensity
                + settings.ScoreWeights.DoseCoverage * doseCoverage
                + settings.ScoreWeights.SocBreadth * socBreadth
                + settings.ScoreWeights.RowVolume * rowVolume);

            // Round away from zero so .5 values consistently appear as the next
            // visible whole-number score on product picker cards.
            product.Score = (int)Math.Round(score, MidpointRounding.AwayFromZero);

            // Persist a short explanation beside the score so UI and tests can
            // explain which source dimensions drove the value.
            product.ScoreReason = buildScoreReason(
                product,
                settings,
                placeboCoverage,
                activeCoverage,
                elevatedDensity,
                doseCoverage,
                socBreadth,
                rowVolume);

            // Return the same DTO instance after adding derived score fields.
            return product;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Populates product-level AE dashboard score fields for a sequence.
        /// </summary>
        /// <param name="products">Product summary DTOs to enrich.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A materialized list of enriched product DTOs.</returns>
        /// <remarks>
        /// This helper is used by product picker and favorites data-access methods
        /// after query projection and favorite enrichment.
        /// </remarks>
        /// <example>
        /// <code>
        /// var products = AeDashboardDerivation.DeriveProducts(rawProducts);
        /// </code>
        /// </example>
        /// <seealso cref="DeriveProduct(AeDrugSummaryDto, AeDashboardDerivationSettings?)"/>
        public static List<AeDrugSummaryDto> DeriveProducts(
            IEnumerable<AeDrugSummaryDto> products,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Product lists are materialized after each DTO has received score and
            // score-reason values.
            return products
                .Select(product => DeriveProduct(product, settings))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the standardized active-ingredient list for one product from its
        /// per-(substance × pharmacologic-class) summary strata.
        /// </summary>
        /// <param name="documentStrata">All <see cref="AeDrugSummaryDto"/> strata that share one DocumentGUID.</param>
        /// <returns>One ingredient per distinct substance, each paired with its preferred ("[EPC]") class, in deterministic order.</returns>
        /// <remarks>
        /// The product summary view emits one row per (substance × class), so a
        /// combination product fans out to several strata. This helper collapses
        /// those strata to one entry per ingredient, standardizing on the
        /// Established Pharmacologic Class ("[EPC]") when available and falling back
        /// to whatever class the label carries. Ordering is by ingredient substance
        /// identifier (nulls last) then substance name so results are stable across
        /// cache refreshes. Pure function — no EF, cache, or mutable state.
        /// </remarks>
        /// <example>
        /// <code>
        /// var ingredients = AeDashboardDerivation.BuildActiveIngredients(advairStrata);
        /// // [ salmeterol xinafoate · beta2-Adrenergic Agonist [EPC],
        /// //   fluticasone propionate · Corticosteroid [EPC] ]
        /// </code>
        /// </example>
        /// <seealso cref="AeActiveIngredientDto"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        public static List<AeActiveIngredientDto> BuildActiveIngredients(
            IEnumerable<AeDrugSummaryDto> documentStrata)
        {
            #region implementation

            // Defensively handle a null sequence so callers can pass query results
            // directly without pre-checking.
            if (documentStrata == null)
            {
                return new List<AeActiveIngredientDto>();
            }

            // Group the document's strata by ingredient identity. IngredientSubstanceID
            // is the stable key; fall back to a case-folded substance name so combo
            // ingredients without a resolved id still separate into distinct rows.
            var groups = documentStrata
                .Where(stratum => stratum != null)
                .GroupBy(stratum => stratum.IngredientSubstanceID.HasValue
                    ? $"id:{stratum.IngredientSubstanceID.Value}"
                    : $"nm:{(stratum.SubstanceName ?? string.Empty).Trim().ToLowerInvariant()}");

            // Carry sort keys alongside each ingredient so ordering stays deterministic.
            var ordered = new List<(AeActiveIngredientDto Ingredient, int? SortId, string SortName)>();

            foreach (var group in groups)
            {
                var rows = group.ToList();

                // Standardize on the EPC class: exact "[EPC]" suffix first, then any
                // "[EPC]" mention, then the first available class as a last resort.
                var classRow = rows.FirstOrDefault(row => endsWithEpc(row.PharmClassName))
                    ?? rows.FirstOrDefault(row => containsEpc(row.PharmClassName))
                    ?? rows[0];

                // Prefer the first populated substance name / UNII across the strata.
                var substanceName = rows
                    .Select(row => row.SubstanceName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                var unii = rows
                    .Select(row => row.UNII)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                // The lowest non-null ingredient id orders this group; null means the
                // name-fallback group, which sorts after all id-backed ingredients.
                var sortId = rows
                    .Where(row => row.IngredientSubstanceID.HasValue)
                    .Select(row => row.IngredientSubstanceID)
                    .DefaultIfEmpty(null)
                    .Min();

                ordered.Add((
                    new AeActiveIngredientDto
                    {
                        SubstanceName = substanceName,
                        UNII = unii,
                        PharmClassName = classRow.PharmClassName,
                        PharmClassCode = classRow.PharmClassCode
                    },
                    sortId,
                    substanceName ?? string.Empty));
            }

            // Id-backed ingredients first (ascending id), then name-fallback groups by
            // ordinal substance name.
            return ordered
                .OrderBy(entry => entry.SortId.HasValue ? 0 : 1)
                .ThenBy(entry => entry.SortId ?? int.MaxValue)
                .ThenBy(entry => entry.SortName, StringComparer.Ordinal)
                .Select(entry => entry.Ingredient)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the tiered AE triage DTO for a product.
        /// </summary>
        /// <param name="product">Product summary context.</param>
        /// <param name="signals">Product-specific AE signals.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A triage view DTO with all counseling tiers present.</returns>
        /// <remarks>
        /// Signals are derived before tiering. Each tier is sorted deterministically by
        /// clinical priority and adverse-event term.
        /// </remarks>
        /// <example>
        /// <code>
        /// var view = AeDashboardDerivation.BuildTriageView(product, signals);
        /// </code>
        /// </example>
        /// <seealso cref="AeTriageViewDto"/>
        /// <seealso cref="AeCounselingTierDto"/>
        public static AeTriageViewDto BuildTriageView(
            AeDrugSummaryDto product,
            IEnumerable<AeRiskSignalDto> signals,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Derive signals once before tier filtering so each tier uses the same
            // precision, significance, and counseling classification decisions.
            var derivedSignals = DeriveSignals(signals, settings);

            // Derive the product score from the summary view first so the product picker
            // and this detail header report the same chart-worthiness value.
            var derivedProduct = DeriveProduct(product, settings);

            // Reconcile the headline counts with the de-duplicated signal set so the KPI
            // strip matches the rendered tiers. The signal list is already collapsed to one
            // row per clinical stratum upstream in the data-access mapper, so trusting the
            // summary view's pre-collapse aggregate here would over-count the visible rows.
            derivedProduct.RowCount = derivedSignals.Count;
            derivedProduct.SignificantCount = derivedSignals.Count(signal => signal.IsSignificant == true);
            derivedProduct.SignificantElevatedCount = derivedSignals.Count(signal => signal.RiskSignificance == AeRiskSignificance.Elevated);
            derivedProduct.SignificantProtectiveCount = derivedSignals.Count(signal => signal.RiskSignificance == AeRiskSignificance.Protective);

            // Use an explicit tier order so empty tiers still render in the same
            // clinical flow instead of depending on whatever rows are present.
            var tierOrder = new[]
            {
                AeCounselingTier.Counsel,
                AeCounselingTier.Watch,
                AeCounselingTier.Reassure,
                AeCounselingTier.Fragile
            };

            // Build the full triage payload: one derived product context plus a
            // stable list of tier buckets and deterministically sorted signals.
            return new AeTriageViewDto
            {
                Product = derivedProduct,
                Tiers = tierOrder.Select(tier =>
                {
                    // Rows for this tier, which are clustered so every occurrence of one
                    // adverse-event term renders together instead of scattering by NNH (the
                    // same term can recur across study contexts, doses, and populations).
                    var tierSignals = derivedSignals
                        .Where(signal => signal.CounselingTier == tier)
                        .ToList();

                    // Each term's lowest NNH is its cluster sort key, so the most actionable
                    // effect (smallest number-needed-to-harm) leads the tier.
                    var termLowestNumberNeeded = tierSignals
                        .GroupBy(signal => signal.ParameterName ?? string.Empty)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Min(signal => signal.NumberNeeded ?? double.MaxValue));

                    return new AeCounselingTierDto
                    {
                        Tier = tier,
                        Name = AeDashboardMetadata.TierNames[tier],
                        Description = AeDashboardMetadata.TierDescriptions[tier],
                        Signals = tierSignals
                            // Cluster order: most-concerning effect first, keyed by its lowest NNH.
                            .OrderBy(signal => termLowestNumberNeeded[signal.ParameterName ?? string.Empty])
                            // Keep every row of one effect contiguous; break cluster ties by name.
                            .ThenBy(signal => signal.ParameterName)
                            // Within a cluster, most-concerning row first; larger RR breaks NNH ties.
                            .ThenBy(signal => signal.NumberNeeded ?? double.MaxValue)
                            .ThenByDescending(signal => signal.RR ?? 0.0)
                            .ToList()
                    };
                }).ToList()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the forest plot DTO from product-specific AE signals.
        /// </summary>
        /// <param name="signals">Signals to include in the plot.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A forest plot DTO sorted by descending relative risk.</returns>
        /// <remarks>
        /// The DTO keeps the static log-scale axis ticks defined by
        /// <see cref="AeForestPlotDto"/> and only supplies the sorted signal payload.
        /// </remarks>
        /// <example>
        /// <code>
        /// var plot = AeDashboardDerivation.BuildForestPlot(signals);
        /// </code>
        /// </example>
        /// <seealso cref="AeForestPlotDto"/>
        public static AeForestPlotDto BuildForestPlot(
            IEnumerable<AeRiskSignalDto> signals,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Forest plots are signal-only views; derive first, then sort largest
            // relative risk first so the riskiest rows appear at the top.
            return new AeForestPlotDto
            {
                Signals = DeriveSignals(signals, settings)
                    .OrderByDescending(signal => signal.RR ?? 0.0)
                    .ThenBy(signal => signal.ParameterName)
                    .ToList()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the quadrant DTO from product-specific AE signals.
        /// </summary>
        /// <param name="signals">Signals to convert into quadrant points.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A quadrant view DTO with coordinates clamped from zero to one.</returns>
        /// <remarks>
        /// Coordinates are derived from confidence-interval width and RR magnitude.
        /// Bubble size is derived from treatment and comparator event counts.
        /// </remarks>
        /// <example>
        /// <code>
        /// var quadrant = AeDashboardDerivation.BuildQuadrantView(signals);
        /// </code>
        /// </example>
        /// <seealso cref="AeQuadrantViewDto"/>
        /// <seealso cref="AeQuadrantPointDto"/>
        public static AeQuadrantViewDto BuildQuadrantView(
            IEnumerable<AeRiskSignalDto> signals,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Convert each derived signal into chart coordinates before ordering
            // points by risk magnitude for deterministic rendering and testing.
            var points = DeriveSignals(signals, settings)
                .Select(signal => buildQuadrantPoint(signal))
                .OrderByDescending(point => point.Signal?.RR ?? 0.0)
                .ThenBy(point => point.Signal?.ParameterName)
                .ToList();

            // The DTO keeps chart metadata elsewhere; this method supplies only the
            // point payload.
            return new AeQuadrantViewDto { Points = points };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a reverse-lookup result for one adverse-event term.
        /// </summary>
        /// <param name="symptom">Adverse-event term being searched.</param>
        /// <param name="products">Candidate product summaries.</param>
        /// <param name="signals">Candidate AE signals.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>A ranked reverse-lookup result DTO.</returns>
        /// <remarks>
        /// Matching is case-insensitive on <see cref="AeRiskSignalDto.ParameterName"/>.
        /// Ranking favors elevated significant matches with lower NNH, then protective
        /// matches, then not-significant matches, with fragile matches last.
        /// </remarks>
        /// <example>
        /// <code>
        /// var lookup = AeDashboardDerivation.BuildReverseLookupResult("Nausea", products, signals);
        /// </code>
        /// </example>
        /// <seealso cref="AeReverseLookupResultDto"/>
        /// <seealso cref="AeReverseLookupMatchDto"/>
        public static AeReverseLookupResultDto BuildReverseLookupResult(
            string symptom,
            IEnumerable<AeDrugSummaryDto> products,
            IEnumerable<AeRiskSignalDto> signals,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Trim the searched symptom once so matching treats leading/trailing
            // user input whitespace as non-semantic.
            var normalizedSymptom = symptom.Trim();

            // Build a product lookup keyed by SPL document so each matched signal
            // can attach its already-derived product card context.
            var productByDocument = products
                .Where(product => product.DocumentGUID.HasValue)
                .GroupBy(product => product.DocumentGUID!.Value)
                .ToDictionary(group => group.Key, group => DeriveProduct(group.First(), settings));

            // Filter signal rows to the requested AE term, discard rows whose
            // product context is unavailable, then rank the remaining matches.
            var matches = DeriveSignals(signals, settings)
                .Where(signal => string.Equals(
                    signal.ParameterName?.Trim(),
                    normalizedSymptom,
                    StringComparison.OrdinalIgnoreCase))
                .Where(signal => signal.DocumentGUID.HasValue && productByDocument.ContainsKey(signal.DocumentGUID.Value))
                .Select(signal => new AeReverseLookupMatchDto
                {
                    Drug = productByDocument[signal.DocumentGUID!.Value],
                    Signal = signal,
                    Verdict = ClassifyReverseLookupVerdict(signal)
                })
                .OrderBy(match => reverseLookupRank(match))
                .ThenBy(match => match.Signal?.NumberNeeded ?? double.MaxValue)
                .ThenByDescending(match => match.Signal?.RR ?? 0.0)
                .ThenBy(match => match.Drug?.ProductName)
                .ToList();

            // AllReassuring is true only when no matched row looks plausibly causal;
            // empty match sets are therefore naturally reassuring.
            return new AeReverseLookupResultDto
            {
                Symptom = symptom,
                Matches = matches,
                AllReassuring = !matches.Any(match => match.Verdict == AeReverseLookupVerdict.PlausiblyCausal)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds an interchange comparison DTO for two dashboard products.
        /// </summary>
        /// <param name="productA">First product summary.</param>
        /// <param name="productB">Second product summary.</param>
        /// <param name="signalsA">Signals for the first product.</param>
        /// <param name="signalsB">Signals for the second product.</param>
        /// <param name="differencesOnly">Whether to remove rows classified as similar.</param>
        /// <param name="sharedSignalsOnly">Whether to remove rows where only one product has the signal.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>An interchange comparison DTO with row counts and warnings.</returns>
        /// <remarks>
        /// Shared adverse-event terms are compared on log10 RR distance. Rows present
        /// on only one product are classified directly.
        /// </remarks>
        /// <example>
        /// <code>
        /// var comparison = AeDashboardDerivation.BuildInterchangeComparison(a, b, signalsA, signalsB);
        /// </code>
        /// </example>
        /// <seealso cref="AeInterchangeComparisonDto"/>
        /// <seealso cref="AeInterchangeRowDto"/>
        public static AeInterchangeComparisonDto BuildInterchangeComparison(
            AeDrugSummaryDto productA,
            AeDrugSummaryDto productB,
            IEnumerable<AeRiskSignalDto> signalsA,
            IEnumerable<AeRiskSignalDto> signalsB,
            bool differencesOnly = false,
            bool sharedSignalsOnly = false,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Derive both products up front so the comparison header exposes the
            // same scoring fields as the standalone product picker.
            productA = DeriveProduct(productA, settings);
            productB = DeriveProduct(productB, settings);

            // Collapse product A duplicate AE terms to one representative signal so
            // the comparison has one row per adverse-event term.
            var signalLookupA = DeriveSignals(signalsA, settings)
                .Where(signal => !string.IsNullOrWhiteSpace(signal.ParameterName))
                .GroupBy(signal => signal.ParameterName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => chooseRepresentativeSignal(group), StringComparer.OrdinalIgnoreCase);

            // Build the same normalized lookup for product B, using identical
            // grouping rules so term matching is symmetric.
            var signalLookupB = DeriveSignals(signalsB, settings)
                .Where(signal => !string.IsNullOrWhiteSpace(signal.ParameterName))
                .GroupBy(signal => signal.ParameterName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => chooseRepresentativeSignal(group), StringComparer.OrdinalIgnoreCase);

            // Union both key sets so rows that appear on only one product are still
            // visible and can be classified as OnlyA or OnlyB.
            var terms = signalLookupA.Keys
                .Union(signalLookupB.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(term => term, StringComparer.OrdinalIgnoreCase);

            // Build the row list after classification, then apply independent UI
            // filters so similar shared rows can survive when only shared rows are requested.
            var rows = terms
                .Select(term => buildInterchangeRow(term, signalLookupA, signalLookupB))
                .Where(row => !differencesOnly || row.Classification != AeInterchangeClass.Similar)
                .Where(row => !sharedSignalsOnly || (row.SignalA != null && row.SignalB != null))
                .ToList();

            // Count the rendered rows by classification so the dashboard can show
            // comparison summary chips without recomputing on the client.
            return new AeInterchangeComparisonDto
            {
                ProductA = productA,
                ProductB = productB,
                Rows = rows,
                OnlyACount = rows.Count(row => row.Classification == AeInterchangeClass.OnlyA),
                OnlyBCount = rows.Count(row => row.Classification == AeInterchangeClass.OnlyB),
                SimilarCount = rows.Count(row => row.Classification == AeInterchangeClass.Similar),
                AWorseCount = rows.Count(row => row.Classification == AeInterchangeClass.AWorse),
                BWorseCount = rows.Count(row => row.Classification == AeInterchangeClass.BWorse),
                ClassMismatchWarning = buildClassMismatchWarning(productA, productB),
                ComparatorMismatchWarning = buildComparatorMismatchWarning(productA, productB)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses persisted AE risk significance text into a typed dashboard value.
        /// </summary>
        /// <param name="significance">Persisted significance text.</param>
        /// <returns>The typed risk significance value.</returns>
        /// <remarks>
        /// Unknown, blank, or not-significant values return
        /// <see cref="AeRiskSignificance.NotSignificant"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var significance = AeDashboardDerivation.ParseRiskSignificance("elevated");
        /// </code>
        /// </example>
        /// <seealso cref="AeRiskSignificance"/>
        public static AeRiskSignificance ParseRiskSignificance(string? significance)
        {
            #region implementation

            // Blank or missing source text means there is no risk direction to
            // communicate, so default to the reassuring/not-significant state.
            if (string.IsNullOrWhiteSpace(significance))
            {
                return AeRiskSignificance.NotSignificant;
            }

            // Stage 5 and SQL view text may vary slightly, so match on a stable
            // substring rather than requiring one exact literal.
            if (significance.Contains("protect", StringComparison.OrdinalIgnoreCase))
            {
                return AeRiskSignificance.Protective;
            }

            // Elevated/increased wording both indicate a harmful significant
            // direction in the dashboard.
            if (significance.Contains("elevat", StringComparison.OrdinalIgnoreCase)
                || significance.Contains("increas", StringComparison.OrdinalIgnoreCase))
            {
                return AeRiskSignificance.Elevated;
            }

            // Unknown non-blank text is treated conservatively as not significant
            // instead of inventing a new display bucket.
            return AeRiskSignificance.NotSignificant;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses persisted number-needed text into a typed dashboard value.
        /// </summary>
        /// <param name="numberNeededType">Persisted number-needed type.</param>
        /// <returns>The typed number-needed interpretation.</returns>
        /// <remarks>
        /// Unknown or blank values return <see cref="AeNumberNeededType.None"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var kind = AeDashboardDerivation.ParseNumberNeededType("NNH");
        /// </code>
        /// </example>
        /// <seealso cref="AeNumberNeededType"/>
        public static AeNumberNeededType ParseNumberNeededType(string? numberNeededType)
        {
            #region implementation

            // NNH means elevated harm and is displayed differently from NNT.
            if (string.Equals(numberNeededType, "NNH", StringComparison.OrdinalIgnoreCase))
            {
                return AeNumberNeededType.NNH;
            }

            // NNT means a protective benefit row.
            if (string.Equals(numberNeededType, "NNT", StringComparison.OrdinalIgnoreCase))
            {
                return AeNumberNeededType.NNT;
            }

            // Missing or unrecognized labels have no number-needed interpretation.
            return AeNumberNeededType.None;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses Stage 5 calculation flags into dashboard data-quality flags.
        /// </summary>
        /// <param name="calculationFlags">Delimited calculation flags from the risk row.</param>
        /// <returns>Recognized dashboard data-quality flags.</returns>
        /// <remarks>
        /// Both semicolon and comma delimiters are supported. Unknown flags are ignored
        /// so new Stage 5 diagnostics do not break dashboard rendering.
        /// </remarks>
        /// <example>
        /// <code>
        /// var flags = AeDashboardDerivation.ParseFlags("ZERO_CELL_CORRECTED;SOC_REMAP");
        /// </code>
        /// </example>
        /// <seealso cref="AeDataQualityFlag"/>
        public static List<AeDataQualityFlag> ParseFlags(string? calculationFlags)
        {
            #region implementation

            // Empty diagnostic text means no recognized quality caveats should be
            // shown on the dashboard row.
            if (string.IsNullOrWhiteSpace(calculationFlags))
            {
                return new List<AeDataQualityFlag>();
            }

            // Split, normalize, map, and de-duplicate known flag tokens while
            // ignoring future Stage 5 tokens that the dashboard does not yet use.
            return calculationFlags
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(normalizeFlag)
                .Select(flag => flag switch
                {
                    "zerocellcorrected" => AeDataQualityFlag.ZeroCellCorrected,
                    "socremap" => AeDataQualityFlag.SocRemap,
                    "wideci" => AeDataQualityFlag.WideCi,
                    "loweventcount" => AeDataQualityFlag.LowEventCount,
                    _ => (AeDataQualityFlag?)null
                })
                .Where(flag => flag.HasValue)
                .Select(flag => flag!.Value)
                .Distinct()
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies AE signal precision from interval width, events, and flags.
        /// </summary>
        /// <param name="signal">Signal DTO to classify.</param>
        /// <param name="settings">Optional derivation settings. Defaults mirror FeatureFlags:AeDashboard.</param>
        /// <returns>The precision class.</returns>
        /// <remarks>
        /// SocRemap alone remains a display caveat and does not force fragile
        /// precision. WideCi, LowEventCount, and ZeroCellCorrected do.
        /// </remarks>
        /// <example>
        /// <code>
        /// var precision = AeDashboardDerivation.ClassifyPrecision(signal);
        /// </code>
        /// </example>
        /// <seealso cref="AePrecisionClass"/>
        public static AePrecisionClass ClassifyPrecision(
            AeRiskSignalDto signal,
            AeDashboardDerivationSettings? settings = null)
        {
            #region implementation

            // Resolve settings first because the same thresholds drive fragile,
            // wide, and tight precision outcomes.
            settings ??= AeDashboardDerivationSettings.Default;

            // Prefer already-derived flags when available; otherwise parse the raw
            // calculation flag text from the risk-table row.
            var flags = signal.Flags.Count > 0 ? signal.Flags : ParseFlags(signal.CalculationFlags);

            // Total observed events are used as a proxy for statistical stability.
            var totalEvents = (signal.EventsTreatment ?? 0.0) + (signal.EventsComparator ?? 0.0);

            // Certain Stage 5 diagnostics force fragile precision because they
            // indicate corrected or sparse evidence.
            var hasFragileFlag = flags.Contains(AeDataQualityFlag.WideCi)
                || flags.Contains(AeDataQualityFlag.LowEventCount)
                || flags.Contains(AeDataQualityFlag.ZeroCellCorrected);

            // Fragile precision wins when explicit flags, low event counts, or
            // unusable confidence bounds make the RR too weak for normal tiering.
            if (hasFragileFlag
                || totalEvents < settings.PrecisionFragileEventCount
                || !hasPositiveInterval(signal))
            {
                return AePrecisionClass.Fragile;
            }

            // Confidence interval width is measured on the log scale because RR is
            // multiplicative; equal ratios above and below 1.0 should be symmetric.
            var logCiWidth = Math.Log10(signal.RRUpperBound!.Value) - Math.Log10(signal.RRLowerBound!.Value);

            // Wide precision still has usable direction, but should be visually
            // caveated when the interval is broad or the event count is modest.
            if (logCiWidth >= settings.PrecisionLogCiWideThreshold
                || totalEvents < settings.PrecisionAdequateEventCount)
            {
                return AePrecisionClass.Wide;
            }

            // Tight is reserved for rows with adequate event support and a narrower
            // confidence interval.
            return AePrecisionClass.Tight;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a signal into the dashboard counseling tier.
        /// </summary>
        /// <param name="signal">Derived or raw signal DTO to classify.</param>
        /// <returns>The counseling tier.</returns>
        /// <remarks>
        /// Raw signals are derived first when the required typed fields are absent.
        /// </remarks>
        /// <example>
        /// <code>
        /// var tier = AeDashboardDerivation.ClassifyCounselingTier(signal);
        /// </code>
        /// </example>
        /// <seealso cref="AeCounselingTier"/>
        public static AeCounselingTier ClassifyCounselingTier(AeRiskSignalDto signal)
        {
            #region implementation

            // Populate precision on raw DTOs so callers can use this method either
            // before or after full signal derivation.
            if (!signal.PrecisionClass.HasValue)
            {
                signal.PrecisionClass = ClassifyPrecision(signal);
            }

            // Populate significance on raw DTOs for the same defensive reason.
            if (!signal.RiskSignificance.HasValue)
            {
                signal.RiskSignificance = ParseRiskSignificance(signal.Significance);
            }

            // These nullable booleans are derived from the typed significance value
            // only when a prior derivation step has not already set them.
            signal.IsSignificant ??= signal.RiskSignificance == AeRiskSignificance.Elevated
                || signal.RiskSignificance == AeRiskSignificance.Protective;
            signal.IsProtective ??= signal.RiskSignificance == AeRiskSignificance.Protective;

            // Fragile rows are isolated first so weak evidence does not accidentally
            // land in a counsel, watch, or reassure bucket.
            if (signal.PrecisionClass == AePrecisionClass.Fragile)
            {
                return AeCounselingTier.Fragile;
            }

            // Non-significant and protective rows are reassuring rather than
            // counseling alerts.
            if (signal.IsSignificant != true || signal.IsProtective == true)
            {
                return AeCounselingTier.Reassure;
            }

            // Tight elevated rows with low NNH are the clearest counseling targets.
            if (signal.PrecisionClass == AePrecisionClass.Tight
                && signal.NumberNeeded.HasValue
                && signal.NumberNeeded.Value <= 50.0)
            {
                return AeCounselingTier.Counsel;
            }

            // Serious SOC rows or less intense elevated findings still deserve a
            // watch tier even when they do not meet the strongest counsel rule.
            if (isSeriousSoc(signal.ParameterCategory)
                || (signal.NumberNeeded.HasValue && signal.NumberNeeded.Value > 50.0))
            {
                return AeCounselingTier.Watch;
            }

            // Remaining elevated, non-fragile rows should still surface as counsel
            // because they are significant and not protective.
            return AeCounselingTier.Counsel;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a reverse-lookup signal verdict.
        /// </summary>
        /// <param name="signal">Derived or raw signal DTO to classify.</param>
        /// <returns>The reverse-lookup verdict.</returns>
        /// <remarks>
        /// Fragile precision wins over signal direction so low-confidence rows do not
        /// appear as causal or protective in the picker.
        /// </remarks>
        /// <example>
        /// <code>
        /// var verdict = AeDashboardDerivation.ClassifyReverseLookupVerdict(signal);
        /// </code>
        /// </example>
        /// <seealso cref="AeReverseLookupVerdict"/>
        public static AeReverseLookupVerdict ClassifyReverseLookupVerdict(AeRiskSignalDto signal)
        {
            #region implementation

            // If callers passed a raw DTO, derive the fields this verdict depends on
            // before applying the reverse-lookup labels.
            if (!signal.PrecisionClass.HasValue || !signal.RiskSignificance.HasValue)
            {
                DeriveSignal(signal);
            }

            // Low-confidence evidence should not be presented as causal or
            // protective, even when the source significance text has a direction.
            if (signal.PrecisionClass == AePrecisionClass.Fragile)
            {
                return AeReverseLookupVerdict.LowConfidence;
            }

            // Elevated, non-fragile rows are the reverse lookup's causal-looking
            // matches.
            if (signal.RiskSignificance == AeRiskSignificance.Elevated)
            {
                return AeReverseLookupVerdict.PlausiblyCausal;
            }

            // Protective, non-fragile rows are explicitly labeled as protective.
            if (signal.RiskSignificance == AeRiskSignificance.Protective)
            {
                return AeReverseLookupVerdict.Protective;
            }

            // Anything else is present but not significantly elevated for the
            // searched term.
            return AeReverseLookupVerdict.NotSignificantlyElevated;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the SOC × SOC correlation map for one pharmacologic class.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code the map is scoped to.</param>
        /// <param name="pharmClassName">Pharmacologic class display name.</param>
        /// <param name="encryptedPharmacologicClassId">Encrypted class identifier for client-safe navigation.</param>
        /// <param name="filters">Applied filters, echoed onto the payload.</param>
        /// <param name="observations">Drug-within-class observations to correlate.</param>
        /// <param name="warnings">Base honesty warnings collected during observation assembly.</param>
        /// <returns>A correlation map DTO with an ordered SOC axis, upper-triangle cells, and per-SOC summaries.</returns>
        /// <remarks>
        /// Each off-diagonal cell is the correlation, across drugs present in both SOCs, of
        /// the two SOCs' per-drug aggregated LogRR. Cells below <c>max(MinDrugsPerCell, 3)</c>
        /// return a null coefficient and <see cref="AeCorrelationCellDto.InsufficientN"/>; the
        /// diagonal is forced to 1.0. Pure function — no EF, cache, or mutable state.
        /// </remarks>
        /// <example>
        /// <code>
        /// var map = AeDashboardDerivation.BuildCorrelationMap(code, name, encId, filters, observations, warnings);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationMapDto"/>
        /// <seealso cref="ComputeCorrelation(IReadOnlyList{double}, IReadOnlyList{double}, AeCorrelationMethod)"/>
        public static AeCorrelationMapDto BuildCorrelationMap(
            string pharmClassCode,
            string? pharmClassName,
            string? encryptedPharmacologicClassId,
            AeCorrelationFilters filters,
            IReadOnlyList<AeCorrelationObservation> observations,
            IReadOnlyList<string> warnings)
        {
            #region implementation

            // Aggregate each drug's terms to one LogRR per SOC, then pivot to drug -> SOC.
            var aggregates = AggregatePerDrugSoc(observations, filters.Aggregation);
            var socAxis = orderedSocAxis(observations);
            var perDrug = pivotByDrug(aggregates);

            // The drugs-per-cell floor is clamped to a hard minimum of three so a coefficient
            // is never returned for a mechanically ±1 pair of two drugs.
            var floor = Math.Max(filters.MinDrugsPerCell, 3);

            // Build only the upper triangle including the diagonal; the client mirrors it.
            var cells = new List<AeCorrelationCellDto>();
            for (var i = 0; i < socAxis.Count; i++)
            {
                for (var j = i; j < socAxis.Count; j++)
                {
                    cells.Add(i == j
                        ? buildDiagonalCell(i, socAxis[i], perDrug)
                        : buildOffDiagonalCell(i, j, socAxis, perDrug, filters.Method, floor));
                }
            }

            // Per-SOC marginal context the matrix cells cannot show on their own.
            var socSummaries = socAxis
                .Select((soc, index) => buildSocSummary(index, soc, observations, perDrug))
                .ToList();

            // Carry forward the assembly warnings and add the correlation-specific honesty notes.
            var mapWarnings = new List<string>(warnings)
            {
                "Cells use pairwise-complete drugs; the matrix is not guaranteed positive semi-definite, so do not cluster or run PCA on it without repair."
            };
            if (cells.Any(cell => !cell.IsDiagonal && cell.InsufficientN))
            {
                mapWarnings.Add($"Some cells fall below the minimum of {floor} drugs and are returned as null.");
            }

            return new AeCorrelationMapDto
            {
                PharmClassCode = pharmClassCode,
                PharmClassName = pharmClassName,
                EncryptedPharmacologicClassID = encryptedPharmacologicClassId,
                AppliedFilters = filters,
                DrugCount = perDrug.Count,
                Soc = socAxis,
                Cells = cells,
                SocSummaries = socSummaries,
                Warnings = mapWarnings
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the SOC × drug relative-risk heatmap for one pharmacologic class.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code the heatmap is scoped to.</param>
        /// <param name="pharmClassName">Pharmacologic class display name.</param>
        /// <param name="encryptedPharmacologicClassId">Encrypted class identifier for client-safe navigation.</param>
        /// <param name="filters">Applied filters, echoed onto the payload.</param>
        /// <param name="observations">Drug-within-class observations to aggregate.</param>
        /// <param name="warnings">Base honesty warnings collected during observation assembly.</param>
        /// <returns>A heatmap DTO with SOC rows, drug columns, and populated aggregated cells.</returns>
        /// <remarks>
        /// The honest small-n companion to <see cref="BuildCorrelationMap"/>: it stays meaningful
        /// when a class is too small to correlate. Only populated (SOC, drug) cells are emitted.
        /// Pure function — no EF, cache, or mutable state.
        /// </remarks>
        /// <example>
        /// <code>
        /// var heatmap = AeDashboardDerivation.BuildCorrelationHeatmap(code, name, encId, filters, observations, warnings);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationHeatmapDto"/>
        public static AeCorrelationHeatmapDto BuildCorrelationHeatmap(
            string pharmClassCode,
            string? pharmClassName,
            string? encryptedPharmacologicClassId,
            AeCorrelationFilters filters,
            IReadOnlyList<AeCorrelationObservation> observations,
            IReadOnlyList<string> warnings)
        {
            #region implementation

            var aggregates = AggregatePerDrugSoc(observations, filters.Aggregation);
            var socAxis = orderedSocAxis(observations);
            var socIndex = indexLookup(socAxis);

            // Drug columns are deduplicated to one per drug key and ordered for stable rendering.
            var drugReps = observations
                .GroupBy(observation => observation.DrugKey)
                .Select(group => group.First())
                .OrderBy(observation => observation.DrugDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(observation => observation.DrugKey, StringComparer.Ordinal)
                .ToList();
            var drugIndex = drugReps
                .Select((observation, index) => (observation.DrugKey, index))
                .ToDictionary(entry => entry.DrugKey, entry => entry.index);

            // Emit one cell per populated (SOC, drug) aggregate; the client fills the gaps.
            var cells = new List<AeCorrelationHeatmapCellDto>();
            foreach (var aggregate in aggregates)
            {
                if (!socIndex.TryGetValue(aggregate.Key.Soc, out var socColumn)
                    || !drugIndex.TryGetValue(aggregate.Key.DrugKey, out var drugColumn))
                {
                    continue;
                }

                cells.Add(new AeCorrelationHeatmapCellDto
                {
                    SocIndex = socColumn,
                    DrugIndex = drugColumn,
                    LogRr = aggregate.Value.Value,
                    Rr = Math.Exp(aggregate.Value.Value),
                    Precision = aggregate.Value.Precision,
                    Significance = aggregate.Value.Significance,
                    TermCount = aggregate.Value.Count
                });
            }

            return new AeCorrelationHeatmapDto
            {
                PharmClassCode = pharmClassCode,
                PharmClassName = pharmClassName,
                EncryptedPharmacologicClassID = encryptedPharmacologicClassId,
                AppliedFilters = filters,
                DrugCount = drugReps.Count,
                Soc = socAxis,
                Drugs = drugReps
                    .Select(observation => new AeCorrelationHeatmapDrugDto
                    {
                        EncryptedActiveMoietyID = observation.EncryptedActiveMoietyID,
                        DrugDisplayName = observation.DrugDisplayName,
                        DocumentGUID = observation.DocumentGUID
                    })
                    .ToList(),
                Cells = cells
                    .OrderBy(cell => cell.SocIndex)
                    .ThenBy(cell => cell.DrugIndex)
                    .ToList(),
                Warnings = new List<string>(warnings)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the per-drug drill-down behind one SOC × SOC correlation cell.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code the cell belongs to.</param>
        /// <param name="pharmClassName">Pharmacologic class display name.</param>
        /// <param name="socX">Requested row SOC (matched case-insensitively).</param>
        /// <param name="socY">Requested column SOC (matched case-insensitively).</param>
        /// <param name="filters">Applied filters, echoed onto the payload.</param>
        /// <param name="observations">Drug-within-class observations to pair.</param>
        /// <param name="warnings">Base honesty warnings collected during observation assembly.</param>
        /// <returns>A cell-detail DTO with the per-drug paired LogRR observations and recomputed coefficient.</returns>
        /// <remarks>
        /// Includes only drugs present in both SOCs (the pairwise-complete contributors), so the
        /// pair count matches the map cell. Pure function — no EF, cache, or mutable state.
        /// </remarks>
        /// <example>
        /// <code>
        /// var detail = AeDashboardDerivation.BuildCorrelationCellDetail(code, name, socX, socY, filters, observations, warnings);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationCellDetailDto"/>
        public static AeCorrelationCellDetailDto BuildCorrelationCellDetail(
            string pharmClassCode,
            string? pharmClassName,
            string socX,
            string socY,
            AeCorrelationFilters filters,
            IReadOnlyList<AeCorrelationObservation> observations,
            IReadOnlyList<string> warnings)
        {
            #region implementation

            var detailWarnings = new List<string>(warnings);
            var aggregates = AggregatePerDrugSoc(observations, filters.Aggregation);

            // Resolve the requested SOCs to their canonical casing from the data.
            var canonicalX = observations
                .Select(observation => observation.Soc)
                .FirstOrDefault(soc => string.Equals(soc, socX, StringComparison.OrdinalIgnoreCase));
            var canonicalY = observations
                .Select(observation => observation.Soc)
                .FirstOrDefault(soc => string.Equals(soc, socY, StringComparison.OrdinalIgnoreCase));

            var detail = new AeCorrelationCellDetailDto
            {
                PharmClassCode = pharmClassCode,
                PharmClassName = pharmClassName,
                SocX = canonicalX ?? socX,
                SocY = canonicalY ?? socY,
                AppliedFilters = filters,
                Warnings = detailWarnings
            };

            // A cell only exists when both SOCs are present in the filtered data.
            if (canonicalX == null || canonicalY == null)
            {
                detailWarnings.Add("One or both requested SOCs have no rows in this class after filtering.");
                return detail;
            }

            var perDrug = pivotByDrug(aggregates);
            var drugReps = observations
                .GroupBy(observation => observation.DrugKey)
                .ToDictionary(group => group.Key, group => group.First());

            // Pair each contributing drug's SOC-X and SOC-Y aggregates.
            var pairs = new List<AeCorrelationDrugPairDto>();
            var xs = new List<double>();
            var ys = new List<double>();
            foreach (var (drugKey, perSoc) in perDrug)
            {
                if (!perSoc.TryGetValue(canonicalX, out var aggregateX)
                    || !perSoc.TryGetValue(canonicalY, out var aggregateY))
                {
                    continue;
                }

                var representative = drugReps[drugKey];
                xs.Add(aggregateX.Value);
                ys.Add(aggregateY.Value);
                pairs.Add(new AeCorrelationDrugPairDto
                {
                    DrugDisplayName = representative.DrugDisplayName,
                    EncryptedActiveMoietyID = representative.EncryptedActiveMoietyID,
                    LogRrX = aggregateX.Value,
                    LogRrY = aggregateY.Value,
                    RrX = Math.Exp(aggregateX.Value),
                    RrY = Math.Exp(aggregateY.Value),
                    PrecisionX = aggregateX.Precision,
                    PrecisionY = aggregateY.Precision,
                    TermCountX = aggregateX.Count,
                    TermCountY = aggregateY.Count
                });
            }

            // The drill-down shows every contributing pair, so it returns the raw coefficient
            // (null only for fewer than two pairs or zero variance) rather than applying the
            // map's drugs-per-cell floor; the reader can see the small n directly.
            var (coefficient, pairCount, _) = ComputeCorrelation(xs, ys, filters.Method);
            detail.DrugPairs = pairs
                .OrderBy(pair => pair.DrugDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.LogRrX)
                .ToList();
            detail.Coefficient = coefficient;
            detail.PairCount = pairCount;
            return detail;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Aggregates each drug's adverse-event terms to one value per SOC.
        /// </summary>
        /// <param name="observations">Drug-within-class observations.</param>
        /// <param name="aggregation">Median (default) or mean of the terms' LogRR.</param>
        /// <returns>A map from (drug key, SOC) to the aggregated value, fragility, term count, and representative direction.</returns>
        /// <remarks>
        /// A drug can report several terms in one SOC; this collapses them to a single number
        /// before correlation so each drug contributes one point per SOC. Pure function.
        /// </remarks>
        /// <example>
        /// <code>
        /// var aggregates = AeDashboardDerivation.AggregatePerDrugSoc(observations, AeCorrelationAggregation.MedianLogRr);
        /// </code>
        /// </example>
        /// <seealso cref="AeCorrelationAggregate"/>
        public static Dictionary<(string DrugKey, string Soc), AeCorrelationAggregate> AggregatePerDrugSoc(
            IReadOnlyList<AeCorrelationObservation> observations,
            AeCorrelationAggregation aggregation)
        {
            #region implementation

            // Fold SOC casing to one canonical (first-seen) form before grouping. The tuple
            // key compares strings ordinally, while the downstream pivot, axis, and index are
            // all case-insensitive — without this fold, one drug with two casings of the same
            // SOC would split into two aggregates and one would silently overwrite the other.
            var canonicalSocs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var observation in observations)
            {
                canonicalSocs.TryAdd(observation.Soc, observation.Soc);
            }

            return observations
                .GroupBy(observation => (observation.DrugKey, Soc: canonicalSocs[observation.Soc]))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        // Collapse the term-level LogRR values for this drug/SOC pair.
                        var logs = group.Select(observation => observation.LogRr).ToList();
                        var value = aggregation == AeCorrelationAggregation.MeanLogRr
                            ? logs.Average()
                            : median(logs);

                        // The strongest-magnitude term supplies the representative direction
                        // and precision shown by the heatmap and drill-down.
                        var representative = group
                            .OrderByDescending(observation => Math.Abs(observation.LogRr))
                            .ThenByDescending(observation => observation.Events)
                            .First();

                        return new AeCorrelationAggregate(
                            value,
                            group.Any(observation => observation.Precision == AePrecisionClass.Fragile),
                            group.Count(),
                            representative.Precision,
                            representative.RiskSignificance);
                    });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes a correlation coefficient, pair count, and two-sided p-value.
        /// </summary>
        /// <param name="xs">First paired vector.</param>
        /// <param name="ys">Second paired vector.</param>
        /// <param name="method">Spearman (rank) or Pearson.</param>
        /// <returns>The coefficient (null for n &lt; 2 or zero variance), the pair count, and the p-value (null for n &lt; 3 or |r| = 1).</returns>
        /// <remarks>
        /// Spearman is Pearson over tie-averaged ranks. The coefficient is clamped to [-1, 1].
        /// The p-value uses the t-statistic <c>r·sqrt((n-2)/(1-r²))</c> against Student's t with
        /// <c>n - 2</c> degrees of freedom. Pure function.
        /// </remarks>
        /// <example>
        /// <code>
        /// var (coefficient, pairCount, pValue) = AeDashboardDerivation.ComputeCorrelation(xs, ys, AeCorrelationMethod.Spearman);
        /// </code>
        /// </example>
        /// <seealso cref="StudentTTwoSidedP(double, int)"/>
        public static (double? Coefficient, int PairCount, double? PValue) ComputeCorrelation(
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            AeCorrelationMethod method)
        {
            #region implementation

            // A correlation needs at least two paired points; fewer is undefined.
            var n = Math.Min(xs.Count, ys.Count);
            if (n < 2)
            {
                return (null, n, null);
            }

            // Spearman ranks both vectors first; Pearson uses the raw values.
            var a = method == AeCorrelationMethod.Spearman ? ranks(xs) : xs.Take(n).ToArray();
            var b = method == AeCorrelationMethod.Spearman ? ranks(ys) : ys.Take(n).ToArray();

            // Zero variance on either side leaves the coefficient undefined (never NaN).
            var r = pearson(a, b);
            if (!r.HasValue)
            {
                return (null, n, null);
            }

            var coefficient = clamp(r.Value, -1.0, 1.0);

            // A p-value is only defined with at least three points and a non-perfect coefficient.
            double? pValue = null;
            if (n >= 3 && Math.Abs(coefficient) < 1.0)
            {
                var t = coefficient * Math.Sqrt((n - 2) / (1.0 - coefficient * coefficient));
                pValue = StudentTTwoSidedP(t, n - 2);
            }

            return (coefficient, n, pValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the two-sided p-value of a t-statistic under Student's t distribution.
        /// </summary>
        /// <param name="t">The t-statistic.</param>
        /// <param name="df">Degrees of freedom (must be positive).</param>
        /// <returns>The two-sided p-value, or NaN for non-positive degrees of freedom or non-finite input.</returns>
        /// <remarks>
        /// Evaluates the regularized incomplete beta function
        /// <c>I_x(df/2, 1/2)</c> with <c>x = df / (df + t²)</c> using a deterministic
        /// continued-fraction expansion. Pure function.
        /// </remarks>
        /// <example>
        /// <code>
        /// var p = AeDashboardDerivation.StudentTTwoSidedP(2.0, 8);
        /// </code>
        /// </example>
        /// <seealso cref="ComputeCorrelation(IReadOnlyList{double}, IReadOnlyList{double}, AeCorrelationMethod)"/>
        public static double StudentTTwoSidedP(double t, int df)
        {
            #region implementation

            // Degrees of freedom must be positive and the statistic finite for a defined p-value.
            if (df <= 0 || double.IsNaN(t) || double.IsInfinity(t))
            {
                return double.NaN;
            }

            // x maps the t-statistic onto the incomplete beta's domain; larger |t| -> smaller x -> smaller p.
            var x = df / (df + t * t);
            return betai(df / 2.0, 0.5, x);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a deterministic per-drug key from a document identifier.
        /// </summary>
        /// <param name="documentGuid">Source SPL document identifier.</param>
        /// <returns>A stable string key derived from the GUID.</returns>
        /// <remarks>
        /// Used as the observation unit when a row has no active moiety. The GUID's fixed
        /// hexadecimal form is stable across runtimes, unlike <see cref="object.GetHashCode"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var key = AeDashboardDerivation.StableDrugKey(documentGuid);
        /// </code>
        /// </example>
        public static string StableDrugKey(Guid documentGuid)
        {
            #region implementation

            return $"doc:{documentGuid:N}";

            #endregion
        }

        #endregion public methods

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Determines whether a pharmacologic class name is an Established
        /// Pharmacologic Class, identified by a trailing "[EPC]" suffix.
        /// </summary>
        /// <param name="pharmClassName">Pharmacologic class display name to test.</param>
        /// <returns>True when the name ends with "[EPC]" (case-insensitive).</returns>
        private static bool endsWithEpc(string? pharmClassName)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(pharmClassName)
                && pharmClassName.TrimEnd().EndsWith("[EPC]", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a pharmacologic class name mentions an "[EPC]" tag
        /// anywhere, used as a tolerant fallback to <see cref="endsWithEpc"/>.
        /// </summary>
        /// <param name="pharmClassName">Pharmacologic class display name to test.</param>
        /// <returns>True when the name contains "[EPC]" (case-insensitive).</returns>
        private static bool containsEpc(string? pharmClassName)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(pharmClassName)
                && pharmClassName.Contains("[EPC]", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a deterministic explanation for a product score.
        /// </summary>
        private static string buildScoreReason(
            AeDrugSummaryDto product,
            AeDashboardDerivationSettings settings,
            double placeboCoverage,
            double activeCoverage,
            double elevatedDensity,
            double doseCoverage,
            double socBreadth,
            double rowVolume)
        {
            #region implementation

            // Pair every score component with its weighted contribution so the
            // reason text can identify the strongest positive drivers.
            var contributors = new List<(string Label, double Value)>
            {
                ("placebo coverage", settings.ScoreWeights.PlaceboCoverage * placeboCoverage),
                ("active comparator coverage", settings.ScoreWeights.ActiveCoverage * activeCoverage),
                ("elevated signal density", settings.ScoreWeights.SignificantElevatedDensity * elevatedDensity),
                ("dose coverage", settings.ScoreWeights.DoseCoverage * doseCoverage),
                ("SOC breadth", settings.ScoreWeights.SocBreadth * socBreadth),
                ("row volume", settings.ScoreWeights.RowVolume * rowVolume)
            };

            // Pick at most two non-zero contributors to keep the score explanation
            // readable in card-sized UI.
            var top = contributors
                .Where(item => item.Value > 0.0)
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Label)
                .Take(2)
                .Select(item => item.Label)
                .ToList();

            // Collect human-readable limiters in priority order; later text takes
            // the first two so the message stays compact.
            var limiters = new List<string>();

            // Missing placebo rows limits interpretability against the safest
            // comparator baseline.
            if (!product.PlaceboCoverage)
            {
                limiters.Add("no placebo coverage");
            }

            // Missing active-comparator rows limits interchange-style comparisons.
            if (!product.ActiveCoverage)
            {
                limiters.Add("no active comparator coverage");
            }

            // Low dose coverage means the dashboard has less dose-response context.
            if (product.DoseCoverage < 0.5)
            {
                limiters.Add("limited dose coverage");
            }

            // Narrow SOC breadth means the product has fewer body-system categories
            // represented in the risk table.
            if (product.SocBreadth < Math.Max(product.SocTotal, 1) / 2.0)
            {
                limiters.Add("limited SOC breadth");
            }

            // Low row volume means fewer observations support the product summary.
            if (product.RowCount < settings.ScoreRowCountTarget)
            {
                limiters.Add("limited row volume");
            }

            // Build fallback text so every product has a complete explanation even
            // when no contributor or limiter is present.
            var topText = top.Count > 0 ? string.Join(", ", top) : "no major contributors";

            // Keep limiter text equally defensive so the final sentence is always
            // complete.
            var limiterText = limiters.Count > 0 ? string.Join(", ", limiters.Take(2)) : "no major limiters";

            // Return a deterministic sentence used directly by dashboard cards.
            return $"Top contributors: {topText}. Limiters: {limiterText}.";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates one quadrant point from a derived signal.
        /// </summary>
        private static AeQuadrantPointDto buildQuadrantPoint(AeRiskSignalDto signal)
        {
            #region implementation

            // Wider confidence intervals reduce precision on the X axis; missing or
            // invalid bounds use the widest supported value.
            var logCiWidth = hasPositiveInterval(signal)
                ? Math.Log10(signal.RRUpperBound!.Value) - Math.Log10(signal.RRLowerBound!.Value)
                : 3.0;

            // RR defaults to neutral 1.0 when missing or invalid so log10 math
            // remains safe and the point centers vertically.
            var rr = signal.RR.HasValue && signal.RR.Value > 0.0 ? signal.RR.Value : 1.0;

            // Bubble size reflects observed evidence volume but never goes below
            // zero when event columns are missing.
            var totalEvents = Math.Max(0.0, (signal.EventsTreatment ?? 0.0) + (signal.EventsComparator ?? 0.0));

            // Direction is derived from the already-computed signal flags so chart
            // styling matches triage and reverse-lookup semantics.
            var direction = signal.IsSignificant == true
                ? signal.IsProtective == true ? AeRiskSignificance.Protective : AeRiskSignificance.Elevated
                : AeRiskSignificance.NotSignificant;

            // Clamp coordinates into the chart's expected 0-1 range and keep the
            // encrypted row ID attached for downstream selection or drill-in.
            return new AeQuadrantPointDto
            {
                EncryptedFlattenedAdverseEventRiskTableID = signal.EncryptedFlattenedAdverseEventRiskTableID,
                Signal = signal,
                PrecisionX = clamp(1.0 - logCiWidth / 3.0, 0.0, 1.0),
                MagnitudeY = clamp((Math.Log10(rr) + 1.5) / 3.0, 0.0, 1.0),
                BubbleSize = 8.0 + Math.Sqrt(totalEvents) * 1.6,
                Direction = direction
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one interchange row for a normalized adverse-event term.
        /// </summary>
        private static AeInterchangeRowDto buildInterchangeRow(
            string term,
            IReadOnlyDictionary<string, AeRiskSignalDto> signalLookupA,
            IReadOnlyDictionary<string, AeRiskSignalDto> signalLookupB)
        {
            #region implementation

            // Try both product lookups because a term may exist on either side or
            // both sides of the comparison.
            signalLookupA.TryGetValue(term, out var signalA);
            signalLookupB.TryGetValue(term, out var signalB);

            // Classification is separated from DTO assembly so counts and labels
            // can share the same interpretation.
            var classification = classifyInterchange(signalA, signalB);

            // Prefer the original source term casing from whichever signal exists,
            // falling back to the normalized dictionary key only if both are absent.
            return new AeInterchangeRowDto
            {
                ParameterName = signalA?.ParameterName ?? signalB?.ParameterName ?? term,
                ParameterCategory = signalA?.ParameterCategory ?? signalB?.ParameterCategory,
                SignalA = signalA,
                SignalB = signalB,
                Classification = classification,
                DeltaLabel = buildDeltaLabel(classification, signalA, signalB)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies an interchange row from two optional signals.
        /// </summary>
        private static AeInterchangeClass classifyInterchange(
            AeRiskSignalDto? signalA,
            AeRiskSignalDto? signalB)
        {
            #region implementation

            // A term found only on product A is a product-A-only difference.
            if (signalA != null && signalB == null)
            {
                return AeInterchangeClass.OnlyA;
            }

            // A term found only on product B is a product-B-only difference.
            if (signalA == null && signalB != null)
            {
                return AeInterchangeClass.OnlyB;
            }

            // This null-safety branch should rarely run after the only-side checks,
            // but keeps the helper stable if called directly with two nulls.
            if (signalA == null || signalB == null)
            {
                return AeInterchangeClass.Similar;
            }

            // If neither row is significant, or either RR cannot support log math,
            // the row is not a meaningful comparative difference.
            var neitherSignificant = signalA.IsSignificant != true && signalB.IsSignificant != true;

            // The actual early-exit branch combines significance and RR validity
            // because either condition makes the products similar for this view.
            if (neitherSignificant || !hasPositiveValue(signalA.RR) || !hasPositiveValue(signalB.RR))
            {
                return AeInterchangeClass.Similar;
            }

            // Log-scale distance compares multiplicative risk differences rather
            // than raw RR subtraction.
            var logDiff = Math.Abs(Math.Log10(signalA.RR!.Value) - Math.Log10(signalB.RR!.Value));

            // Treat tiny log-scale gaps as clinically similar for this dashboard
            // comparison.
            if (logDiff < 0.15)
            {
                return AeInterchangeClass.Similar;
            }

            // The larger RR is labeled worse when both products have comparable
            // significant evidence for the same adverse-event term.
            return signalA.RR > signalB.RR
                ? AeInterchangeClass.AWorse
                : AeInterchangeClass.BWorse;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds display text for an interchange row classification.
        /// </summary>
        private static string buildDeltaLabel(
            AeInterchangeClass classification,
            AeRiskSignalDto? signalA,
            AeRiskSignalDto? signalB)
        {
            #region implementation

            // Map internal comparison classes to concise display labels; signal
            // parameters are retained in the signature for future label expansion.
            return classification switch
            {
                AeInterchangeClass.OnlyA => "Only product A has this signal",
                AeInterchangeClass.OnlyB => "Only product B has this signal",
                AeInterchangeClass.AWorse => "Higher RR on product A",
                AeInterchangeClass.BWorse => "Higher RR on product B",
                _ => "Similar AE profile"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Chooses a representative signal from duplicate terms.
        /// </summary>
        private static AeRiskSignalDto chooseRepresentativeSignal(IEnumerable<AeRiskSignalDto> signals)
        {
            #region implementation

            // Prefer non-fragile, significant, low-number-needed, high-RR rows so a
            // duplicate AE term is represented by the most actionable signal.
            return signals
                .OrderBy(signal => signal.PrecisionClass == AePrecisionClass.Fragile ? 1 : 0)
                .ThenByDescending(signal => signal.IsSignificant == true)
                .ThenBy(signal => signal.NumberNeeded ?? double.MaxValue)
                .ThenByDescending(signal => signal.RR ?? 0.0)
                .First();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a pharmacologic class mismatch warning when product classes differ.
        /// </summary>
        private static string? buildClassMismatchWarning(AeDrugSummaryDto productA, AeDrugSummaryDto productB)
        {
            #region implementation

            // Prefer class code because it is more stable than display text, then
            // fall back to the human-readable class name.
            var classA = !string.IsNullOrWhiteSpace(productA.PharmClassCode)
                ? productA.PharmClassCode
                : productA.PharmClassName;

            // Product B uses the same code-first fallback so both sides compare
            // equivalent class representations.
            var classB = !string.IsNullOrWhiteSpace(productB.PharmClassCode)
                ? productB.PharmClassCode
                : productB.PharmClassName;

            // Emit a warning only when both products have class information and the
            // normalized values differ.
            if (!string.IsNullOrWhiteSpace(classA)
                && !string.IsNullOrWhiteSpace(classB)
                && !string.Equals(classA, classB, StringComparison.OrdinalIgnoreCase))
            {
                return "Products have different pharmacologic classes.";
            }

            // No warning is returned when either class is missing or both match.
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a comparator coverage mismatch warning when products differ.
        /// </summary>
        private static string? buildComparatorMismatchWarning(AeDrugSummaryDto productA, AeDrugSummaryDto productB)
        {
            #region implementation

            // Comparator coverage affects interpretability, so surface a warning
            // when one product has placebo or active coverage that the other lacks.
            if (productA.PlaceboCoverage != productB.PlaceboCoverage
                || productA.ActiveCoverage != productB.ActiveCoverage)
            {
                return "Products have different comparator coverage mixes.";
            }

            // Matching comparator coverage requires no warning text.
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes reverse lookup ordering priority.
        /// </summary>
        private static int reverseLookupRank(AeReverseLookupMatchDto match)
        {
            #region implementation

            // Lower ranks sort earlier in reverse lookup results; causal-looking
            // matches therefore appear before protective or neutral matches.
            return match.Verdict switch
            {
                AeReverseLookupVerdict.PlausiblyCausal => 0,
                AeReverseLookupVerdict.Protective => 1,
                AeReverseLookupVerdict.NotSignificantlyElevated => 2,
                _ => 4
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether a signal has positive confidence interval bounds.
        /// </summary>
        private static bool hasPositiveInterval(AeRiskSignalDto signal)
        {
            #region implementation

            // Both lower and upper RR bounds must be positive before any log10
            // precision math can run safely.
            return hasPositiveValue(signal.RRLowerBound) && hasPositiveValue(signal.RRUpperBound);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether a nullable number is positive.
        /// </summary>
        private static bool hasPositiveValue(double? value)
        {
            #region implementation

            // Positive finite values are the only safe inputs for RR log operations.
            return value.HasValue && value.Value > 0.0 && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether the supplied SOC is treated as serious dashboard context.
        /// </summary>
        private static bool isSeriousSoc(string? parameterCategory)
        {
            #region implementation

            // A blank SOC cannot be treated as serious; otherwise trim before
            // matching the metadata list.
            return !string.IsNullOrWhiteSpace(parameterCategory)
                && AeDashboardMetadata.SocSerious.Contains(parameterCategory.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes a Stage 5 flag token for tolerant parsing.
        /// </summary>
        private static string normalizeFlag(string flag)
        {
            #region implementation

            // Remove punctuation and casing differences so Stage 5 flag text can be
            // matched even if delimiters or naming style drift.
            return flag
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bounds a number to the supplied inclusive range.
        /// </summary>
        private static double clamp(double value, double min, double max)
        {
            #region implementation

            // Clamp by applying the lower bound first and then the upper bound.
            return Math.Min(Math.Max(value, min), max);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the ordered, distinct SOC axis from correlation observations.
        /// </summary>
        private static List<string> orderedSocAxis(IReadOnlyList<AeCorrelationObservation> observations)
        {
            #region implementation

            return observations
                .Select(observation => observation.Soc)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(soc => soc, StringComparer.OrdinalIgnoreCase)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pivots per-(drug, SOC) aggregates into a drug -> SOC -> aggregate map.
        /// </summary>
        private static Dictionary<string, Dictionary<string, AeCorrelationAggregate>> pivotByDrug(
            IReadOnlyDictionary<(string DrugKey, string Soc), AeCorrelationAggregate> aggregates)
        {
            #region implementation

            var perDrug = new Dictionary<string, Dictionary<string, AeCorrelationAggregate>>(StringComparer.Ordinal);
            foreach (var aggregate in aggregates)
            {
                if (!perDrug.TryGetValue(aggregate.Key.DrugKey, out var perSoc))
                {
                    perSoc = new Dictionary<string, AeCorrelationAggregate>(StringComparer.OrdinalIgnoreCase);
                    perDrug[aggregate.Key.DrugKey] = perSoc;
                }

                perSoc[aggregate.Key.Soc] = aggregate.Value;
            }

            return perDrug;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a case-insensitive name -> axis-index lookup.
        /// </summary>
        private static Dictionary<string, int> indexLookup(IReadOnlyList<string> axis)
        {
            #region implementation

            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < axis.Count; i++)
            {
                if (!lookup.ContainsKey(axis[i]))
                {
                    lookup[axis[i]] = i;
                }
            }

            return lookup;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a non-informative diagonal correlation cell.
        /// </summary>
        private static AeCorrelationCellDto buildDiagonalCell(
            int index,
            string soc,
            IReadOnlyDictionary<string, Dictionary<string, AeCorrelationAggregate>> perDrug)
        {
            #region implementation

            // Diagonal n is the number of drugs with any data in this SOC.
            var drugsInSoc = perDrug.Values.Where(perSoc => perSoc.ContainsKey(soc)).ToList();

            return new AeCorrelationCellDto
            {
                RowIndex = index,
                ColumnIndex = index,
                RowSoc = soc,
                ColumnSoc = soc,
                Coefficient = 1.0,
                PairCount = drugsInSoc.Count,
                PValue = null,
                IsSignificant = false,
                IsFragile = drugsInSoc.Any(perSoc => perSoc[soc].AnyFragile),
                InsufficientN = false,
                IsDiagonal = true
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one off-diagonal correlation cell from the drugs present in both SOCs.
        /// </summary>
        private static AeCorrelationCellDto buildOffDiagonalCell(
            int rowIndex,
            int columnIndex,
            IReadOnlyList<string> socAxis,
            IReadOnlyDictionary<string, Dictionary<string, AeCorrelationAggregate>> perDrug,
            AeCorrelationMethod method,
            int floor)
        {
            #region implementation

            var rowSoc = socAxis[rowIndex];
            var columnSoc = socAxis[columnIndex];

            // Pairwise-complete deletion: only drugs with data in both SOCs contribute.
            var xs = new List<double>();
            var ys = new List<double>();
            var anyFragile = false;
            foreach (var perSoc in perDrug.Values)
            {
                if (perSoc.TryGetValue(rowSoc, out var rowAggregate)
                    && perSoc.TryGetValue(columnSoc, out var columnAggregate))
                {
                    xs.Add(rowAggregate.Value);
                    ys.Add(columnAggregate.Value);
                    anyFragile |= rowAggregate.AnyFragile || columnAggregate.AnyFragile;
                }
            }

            var (coefficient, pairCount, pValue) = ComputeCorrelation(xs, ys, method);

            // Thin cells return null rather than a confident number over noise.
            var insufficient = pairCount < floor;
            var cellCoefficient = insufficient ? null : coefficient;
            var cellPValue = insufficient ? null : pValue;

            return new AeCorrelationCellDto
            {
                RowIndex = rowIndex,
                ColumnIndex = columnIndex,
                RowSoc = rowSoc,
                ColumnSoc = columnSoc,
                Coefficient = cellCoefficient,
                PairCount = pairCount,
                PValue = cellPValue,
                IsSignificant = cellCoefficient.HasValue && cellPValue.HasValue && cellPValue.Value < 0.05,
                IsFragile = anyFragile,
                InsufficientN = insufficient,
                IsDiagonal = false
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the per-SOC marginal summary shown beside the matrix.
        /// </summary>
        private static AeCorrelationSocSummaryDto buildSocSummary(
            int index,
            string soc,
            IReadOnlyList<AeCorrelationObservation> observations,
            IReadOnlyDictionary<string, Dictionary<string, AeCorrelationAggregate>> perDrug)
        {
            #region implementation

            // Drug-level values in this SOC are the same inputs the correlation uses.
            var drugAggregates = perDrug.Values
                .Where(perSoc => perSoc.ContainsKey(soc))
                .Select(perSoc => perSoc[soc])
                .ToList();
            var medianLogRr = drugAggregates.Count > 0
                ? median(drugAggregates.Select(aggregate => aggregate.Value))
                : (double?)null;

            // Direction shares are computed over the SOC's term-level observations.
            var socObservations = observations
                .Where(observation => string.Equals(observation.Soc, soc, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new AeCorrelationSocSummaryDto
            {
                Index = index,
                Soc = soc,
                DrugCount = drugAggregates.Count,
                FragileDrugCount = drugAggregates.Count(aggregate => aggregate.AnyFragile),
                MedianLogRr = medianLogRr,
                MedianRr = medianLogRr.HasValue ? Math.Exp(medianLogRr.Value) : null,
                ElevatedShare = socObservations.Count > 0
                    ? socObservations.Count(observation => observation.RiskSignificance == AeRiskSignificance.Elevated) / (double)socObservations.Count
                    : 0.0,
                ProtectiveShare = socObservations.Count > 0
                    ? socObservations.Count(observation => observation.RiskSignificance == AeRiskSignificance.Protective) / (double)socObservations.Count
                    : 0.0
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the median of a sequence, or zero for an empty sequence.
        /// </summary>
        private static double median(IEnumerable<double> values)
        {
            #region implementation

            var sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return 0.0;
            }

            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2.0;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes tie-averaged ranks for a vector (1-based).
        /// </summary>
        private static double[] ranks(IReadOnlyList<double> values)
        {
            #region implementation

            var n = values.Count;
            var order = Enumerable.Range(0, n).OrderBy(i => values[i]).ToArray();
            var result = new double[n];

            // Walk the sorted order, assigning the average rank to each run of ties.
            var i = 0;
            while (i < n)
            {
                var j = i;
                while (j + 1 < n && values[order[j + 1]] == values[order[i]])
                {
                    j++;
                }

                var averageRank = (i + j) / 2.0 + 1.0;
                for (var k = i; k <= j; k++)
                {
                    result[order[k]] = averageRank;
                }

                i = j + 1;
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the Pearson correlation, or null when either vector has zero variance.
        /// </summary>
        private static double? pearson(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            #region implementation

            var n = Math.Min(a.Count, b.Count);
            if (n < 2)
            {
                return null;
            }

            // First pass: means.
            double sumA = 0.0, sumB = 0.0;
            for (var k = 0; k < n; k++)
            {
                sumA += a[k];
                sumB += b[k];
            }

            var meanA = sumA / n;
            var meanB = sumB / n;

            // Second pass: covariance and per-vector variance.
            double covariance = 0.0, varianceA = 0.0, varianceB = 0.0;
            for (var k = 0; k < n; k++)
            {
                var deltaA = a[k] - meanA;
                var deltaB = b[k] - meanB;
                covariance += deltaA * deltaB;
                varianceA += deltaA * deltaA;
                varianceB += deltaB * deltaB;
            }

            // A constant vector has no direction to correlate with.
            if (varianceA <= 1e-12 || varianceB <= 1e-12)
            {
                return null;
            }

            return covariance / Math.Sqrt(varianceA * varianceB);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Evaluates the regularized incomplete beta function I_x(a, b).
        /// </summary>
        private static double betai(double a, double b, double x)
        {
            #region implementation

            if (x <= 0.0)
            {
                return 0.0;
            }

            if (x >= 1.0)
            {
                return 1.0;
            }

            // Factor in front of the continued fraction, evaluated in log space for stability.
            var front = Math.Exp(gammaln(a + b) - gammaln(a) - gammaln(b)
                + a * Math.Log(x) + b * Math.Log(1.0 - x));

            // Use the continued fraction on whichever side converges fastest.
            return x < (a + 1.0) / (a + b + 2.0)
                ? front * betacf(a, b, x) / a
                : 1.0 - front * betacf(b, a, 1.0 - x) / b;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Evaluates the continued fraction used by <see cref="betai"/> (Lentz's method).
        /// </summary>
        private static double betacf(double a, double b, double x)
        {
            #region implementation

            const int maxIterations = 200;
            const double epsilon = 3.0e-12;
            const double tiny = 1.0e-300;

            var qab = a + b;
            var qap = a + 1.0;
            var qam = a - 1.0;
            var c = 1.0;
            var d = 1.0 - qab * x / qap;
            if (Math.Abs(d) < tiny)
            {
                d = tiny;
            }

            d = 1.0 / d;
            var h = d;

            for (var m = 1; m <= maxIterations; m++)
            {
                var m2 = 2 * m;

                // Even step.
                var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < tiny)
                {
                    d = tiny;
                }

                c = 1.0 + aa / c;
                if (Math.Abs(c) < tiny)
                {
                    c = tiny;
                }

                d = 1.0 / d;
                h *= d * c;

                // Odd step.
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < tiny)
                {
                    d = tiny;
                }

                c = 1.0 + aa / c;
                if (Math.Abs(c) < tiny)
                {
                    c = tiny;
                }

                d = 1.0 / d;
                var delta = d * c;
                h *= delta;

                if (Math.Abs(delta - 1.0) < epsilon)
                {
                    break;
                }
            }

            return h;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Evaluates the natural log of the gamma function (Lanczos approximation).
        /// </summary>
        private static double gammaln(double x)
        {
            #region implementation

            double[] coefficients =
            {
                76.18009172947146,
                -86.50532032941677,
                24.01409824083091,
                -1.231739572450155,
                0.1208650973866179e-2,
                -0.5395239384953e-5
            };

            var y = x;
            var tmp = x + 5.5;
            tmp -= (x + 0.5) * Math.Log(tmp);

            var series = 1.000000000190015;
            for (var j = 0; j < coefficients.Length; j++)
            {
                y += 1.0;
                series += coefficients[j] / y;
            }

            return -tmp + Math.Log(2.5066282746310005 * series / x);

            #endregion
        }

        #endregion private methods
    }

    /**************************************************************/
    /// <summary>
    /// Settings used by AE dashboard derivation helpers.
    /// </summary>
    /// <remarks>
    /// Defaults mirror the FeatureFlags:AeDashboard appsettings section so data
    /// access can remain controller-ready without hard-coded threshold references
    /// scattered through query methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// var settings = AeDashboardDerivationSettings.Default;
    /// </code>
    /// </example>
    /// <seealso cref="AeDashboardDerivation"/>
    public sealed class AeDashboardDerivationSettings
    {
        #region properties

        /**************************************************************/
        /// <summary>Gets the default dashboard derivation settings.</summary>
        public static AeDashboardDerivationSettings Default => new();

        /**************************************************************/
        /// <summary>Gets or sets the log10 confidence interval width threshold for wide precision.</summary>
        public double PrecisionLogCiWideThreshold { get; set; } = 0.5;

        /**************************************************************/
        /// <summary>Gets or sets the event-count threshold below which precision is wide.</summary>
        public int PrecisionAdequateEventCount { get; set; } = 30;

        /**************************************************************/
        /// <summary>Gets or sets the event-count threshold below which precision is fragile.</summary>
        public int PrecisionFragileEventCount { get; set; } = 10;

        /**************************************************************/
        /// <summary>Gets or sets the score weights used by product score derivation.</summary>
        public AeDashboardScoreWeights ScoreWeights { get; set; } = new();

        /**************************************************************/
        /// <summary>Gets or sets the row-count target that gives full score credit for row volume.</summary>
        public int ScoreRowCountTarget { get; set; } = 40;

        /**************************************************************/
        /// <summary>Gets or sets the favorite ordering mode used by favorite data access.</summary>
        public string FavoriteOrdering { get; set; } = "CreatedAtDescending";

        /**************************************************************/
        /// <summary>Gets or sets the favorite delete mode used by favorite data access.</summary>
        public string FavoriteDeleteMode { get; set; } = "HardDelete";

        #endregion properties
    }

    /**************************************************************/
    /// <summary>
    /// Score weights used by AE dashboard product score derivation.
    /// </summary>
    /// <remarks>
    /// Values are interpreted as fractions of the total product score and mirror
    /// the FeatureFlags:AeDashboard:ScoreWeights defaults.
    /// </remarks>
    /// <example>
    /// <code>
    /// var weights = new AeDashboardScoreWeights { PlaceboCoverage = 0.25 };
    /// </code>
    /// </example>
    /// <seealso cref="AeDashboardDerivationSettings"/>
    public sealed class AeDashboardScoreWeights
    {
        #region properties

        /**************************************************************/
        /// <summary>Gets or sets the placebo comparator coverage score weight.</summary>
        public double PlaceboCoverage { get; set; } = 0.25;

        /**************************************************************/
        /// <summary>Gets or sets the active comparator coverage score weight.</summary>
        public double ActiveCoverage { get; set; } = 0.05;

        /**************************************************************/
        /// <summary>Gets or sets the significant elevated signal density score weight.</summary>
        public double SignificantElevatedDensity { get; set; } = 0.25;

        /**************************************************************/
        /// <summary>Gets or sets the dose coverage score weight.</summary>
        public double DoseCoverage { get; set; } = 0.15;

        /**************************************************************/
        /// <summary>Gets or sets the SOC breadth score weight.</summary>
        public double SocBreadth { get; set; } = 0.20;

        /**************************************************************/
        /// <summary>Gets or sets the row volume score weight.</summary>
        public double RowVolume { get; set; } = 0.10;

        #endregion properties
    }
}
