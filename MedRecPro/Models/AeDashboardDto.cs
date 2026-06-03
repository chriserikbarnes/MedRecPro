using System.Collections.Generic;
using MedRecPro.Helpers;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Dashboard DTO for one materialized adverse-event risk signal.
    /// </summary>
    /// <remarks>
    /// This class is the API-safe counterpart to
    /// <see cref="LabelView.FlattenedAdverseEventRiskTable"/> for the AE dashboard
    /// prototypes. Integer source identifiers are exposed only as encrypted string
    /// fields; computed dashboard fields are declared here but intentionally
    /// populated by a later derivation service.
    /// </remarks>
    /// <example>
    /// <code>
    /// var signal = new AeRiskSignalDto
    /// {
    ///     ParameterName = "Headache",
    ///     RR = 1.82,
    ///     Significance = "elevated"
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
    /// <seealso cref="AeTriageViewDto"/>
    /// <seealso cref="AeForestPlotDto"/>
    /// <seealso cref="AeQuadrantPointDto"/>
    public class AeRiskSignalDto
    {
        #region Encrypted Source Identifier Properties

        /**************************************************************/
        /// <summary>
        /// Encrypted identifier for the source tmp_FlattenedAdverseEventRiskTable row.
        /// </summary>
        public string? EncryptedFlattenedAdverseEventRiskTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Source tmp_FlattenedAdverseEventRiskTable identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FlattenedAdverseEventRiskTableID =>
            !string.IsNullOrWhiteSpace(EncryptedFlattenedAdverseEventRiskTableID)
                ? Util.DecryptAndParseInt(EncryptedFlattenedAdverseEventRiskTableID)
                : null;

        /**************************************************************/
        /// <summary>
        /// Encrypted identifier for the source tmp_FlattenedAdverseEventTable row.
        /// </summary>
        public string? EncryptedFlattenedAdverseEventTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Source tmp_FlattenedAdverseEventTable identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FlattenedAdverseEventTableID =>
            !string.IsNullOrWhiteSpace(EncryptedFlattenedAdverseEventTableID)
                ? Util.DecryptAndParseInt(EncryptedFlattenedAdverseEventTableID)
                : null;

        /**************************************************************/
        /// <summary>
        /// Encrypted identifier for the source tmp_FlattenedStandardizedTable row.
        /// </summary>
        public string? EncryptedFlattenedStandardizedTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Source tmp_FlattenedStandardizedTable identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FlattenedStandardizedTableID =>
            !string.IsNullOrWhiteSpace(EncryptedFlattenedStandardizedTableID)
                ? Util.DecryptAndParseInt(EncryptedFlattenedStandardizedTableID)
                : null;

        #endregion Encrypted Source Identifier Properties

        #region Product and Signal Properties

        /**************************************************************/
        /// <summary>Canonical adverse-event term displayed by triage and lookup views.</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>Canonical adverse-event SOC/category displayed with the signal.</summary>
        public string? ParameterCategory { get; set; }

        /**************************************************************/
        /// <summary>Risk-signal classification persisted by the risk table.</summary>
        public string? Significance { get; set; }

        /**************************************************************/
        /// <summary>Number-needed interpretation persisted by the risk table.</summary>
        public string? NumberNeededType { get; set; }

        /**************************************************************/
        /// <summary>Plus-delimited active-ingredient UNIIs represented by the source row.</summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>Product display name resolved through pharmacologic-class context.</summary>
        public string? ProductName { get; set; }

        /**************************************************************/
        /// <summary>Source SPL document identifier carried by the source row.</summary>
        public Guid? DocumentGUID { get; set; }

        #endregion Product and Signal Properties

        #region Risk Estimate Properties

        /**************************************************************/
        /// <summary>Treatment-arm denominator used by the risk estimate.</summary>
        public int? ArmN { get; set; }

        /**************************************************************/
        /// <summary>Comparator-arm denominator used by the risk estimate.</summary>
        public int? ComparatorN { get; set; }

        /**************************************************************/
        /// <summary>Derived treatment-arm event count.</summary>
        public double? EventsTreatment { get; set; }

        /**************************************************************/
        /// <summary>Derived comparator-arm event count.</summary>
        public double? EventsComparator { get; set; }

        /**************************************************************/
        /// <summary>Relative Risk point estimate.</summary>
        public double? RR { get; set; }

        /**************************************************************/
        /// <summary>Lower bound of the 95% confidence interval for <see cref="RR"/>.</summary>
        public double? RRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Upper bound of the 95% confidence interval for <see cref="RR"/>.</summary>
        public double? RRUpperBound { get; set; }

        /**************************************************************/
        /// <summary>Natural log of <see cref="RR"/> materialized for log-scale charts.</summary>
        public double? LogRR { get; set; }

        /**************************************************************/
        /// <summary>Natural log of <see cref="RRLowerBound"/> materialized for log-scale charts.</summary>
        public double? LogRRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Natural log of <see cref="RRUpperBound"/> materialized for log-scale charts.</summary>
        public double? LogRRUpperBound { get; set; }

        /**************************************************************/
        /// <summary>Number-needed point estimate for significant elevated or protective intervals.</summary>
        public double? NumberNeeded { get; set; }

        /**************************************************************/
        /// <summary>Lower bound for number-needed estimates.</summary>
        public double? NumberNeededLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Upper bound for number-needed estimates.</summary>
        public double? NumberNeededUpperBound { get; set; }

        #endregion Risk Estimate Properties

        #region Provenance and Context Properties

        /**************************************************************/
        /// <summary>Flag indicating whether the selected comparator was placebo-like.</summary>
        public bool IsPlaceboControlled { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether the source row represents a combination product.</summary>
        public bool IsCombo { get; set; }

        /**************************************************************/
        /// <summary>Semicolon-delimited calculation and data-quality flags from Stage 5.</summary>
        public string? CalculationFlags { get; set; }

        /**************************************************************/
        /// <summary>Colspan-derived study context carried forward from table parsing.</summary>
        public string? StudyContext { get; set; }

        /**************************************************************/
        /// <summary>Caption-derived whole-table population context.</summary>
        public string? Population { get; set; }

        /**************************************************************/
        /// <summary>In-table subpopulation partition for the source AE row.</summary>
        public string? Subpopulation { get; set; }

        /**************************************************************/
        /// <summary>Treatment-arm dose copied from the Stage 5 AE row.</summary>
        public decimal? Dose { get; set; }

        /**************************************************************/
        /// <summary>Treatment-arm dose unit copied from the Stage 5 AE row.</summary>
        public string? DoseUnit { get; set; }

        #endregion Provenance and Context Properties

        #region Deferred Computation Properties

        /**************************************************************/
        /// <summary>
        /// Derived dashboard precision class.
        /// </summary>
        /// <remarks>
        /// Derived from confidence-interval width, event counts, and
        /// <see cref="CalculationFlags"/> by a later mapping service.
        /// </remarks>
        public AePrecisionClass? PrecisionClass { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived flag indicating whether the signal confidence interval excludes one.
        /// </summary>
        /// <remarks>Derived from <see cref="Significance"/> by a later mapping service.</remarks>
        public bool? IsSignificant { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived flag indicating whether the signal is protective.
        /// </summary>
        /// <remarks>Derived from <see cref="Significance"/> by a later mapping service.</remarks>
        public bool? IsProtective { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived typed significance classification for dashboard logic.
        /// </summary>
        /// <remarks>Derived from <see cref="Significance"/> by a later mapping service.</remarks>
        public AeRiskSignificance? RiskSignificance { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived typed number-needed interpretation.
        /// </summary>
        /// <remarks>Derived from <see cref="NumberNeededType"/> by a later mapping service.</remarks>
        public AeNumberNeededType? NumberNeededKind { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived data-quality flags parsed for dashboard filtering and display.
        /// </summary>
        /// <remarks>Derived from <see cref="CalculationFlags"/> by a later mapping service.</remarks>
        public List<AeDataQualityFlag> Flags { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Derived counseling tier used by the triage view.
        /// </summary>
        /// <remarks>
        /// Derived from precision, significance, protective direction, SOC, and
        /// number-needed values by a later mapping service.
        /// </remarks>
        public AeCounselingTier? CounselingTier { get; set; }

        #endregion Deferred Computation Properties
    }

    /**************************************************************/
    /// <summary>
    /// One active ingredient of a product, paired with its preferred
    /// (Established Pharmacologic Class, "[EPC]") pharmacologic class.
    /// </summary>
    /// <remarks>
    /// A combination product such as ADVAIR (salmeterol + fluticasone) yields one
    /// entry per active ingredient. The pharmacologic class is standardized on the
    /// "[EPC]" variant when one is available, falling back to whatever class the
    /// label provides. Built by <see cref="MedRecPro.DataAccess.AeDashboardDerivation.BuildActiveIngredients"/>
    /// from the per-(substance × class) strata that back <see cref="AeDrugSummaryDto"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var ingredient = new AeActiveIngredientDto
    /// {
    ///     SubstanceName = "salmeterol xinafoate",
    ///     PharmClassName = "beta2-Adrenergic Agonist [EPC]"
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="MedRecPro.DataAccess.AeDashboardDerivation"/>
    public class AeActiveIngredientDto
    {
        #region Ingredient Properties

        /**************************************************************/
        /// <summary>Active ingredient substance name (e.g. "salmeterol xinafoate").</summary>
        public string? SubstanceName { get; set; }

        /**************************************************************/
        /// <summary>UNII represented by this ingredient's source rows.</summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>Preferred ("[EPC]") pharmacologic class display name for the ingredient.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class code paired with <see cref="PharmClassName"/>.</summary>
        public string? PharmClassCode { get; set; }

        #endregion Ingredient Properties
    }

    /**************************************************************/
    /// <summary>
    /// Dashboard DTO for one aggregate adverse-event product summary row.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="LabelView.AeDrugSummary"/> for client-safe fields.
    /// Raw integer identifiers are replaced with encrypted string properties, while
    /// non-deterministic score fields are declared for later derivation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var summary = new AeDrugSummaryDto
    /// {
    ///     ProductName = "Example product",
    ///     SignificantElevatedCount = 3,
    ///     PlaceboCoverage = true
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="LabelView.AeDrugSummary"/>
    /// <seealso cref="AeInterchangeComparisonDto"/>
    public class AeDrugSummaryDto
    {
        #region Encrypted Identifier Properties

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe navigation.</summary>
        public string? EncryptedActiveMoietyID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Active moiety identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietyID =>
            !string.IsNullOrWhiteSpace(EncryptedActiveMoietyID)
                ? Util.DecryptAndParseInt(EncryptedActiveMoietyID)
                : null;

        /**************************************************************/
        /// <summary>Encrypted ingredient substance identifier for client-safe navigation.</summary>
        public string? EncryptedIngredientSubstanceID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ingredient substance identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            !string.IsNullOrWhiteSpace(EncryptedIngredientSubstanceID)
                ? Util.DecryptAndParseInt(EncryptedIngredientSubstanceID)
                : null;

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pharmacologic class identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            !string.IsNullOrWhiteSpace(EncryptedPharmacologicClassID)
                ? Util.DecryptAndParseInt(EncryptedPharmacologicClassID)
                : null;

        #endregion Encrypted Identifier Properties

        #region Product and Class Properties

        /**************************************************************/
        /// <summary>Source SPL document identifier represented by the summary row.</summary>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>Product display name used by dashboard selectors and headings.</summary>
        public string? ProductName { get; set; }

        /**************************************************************/
        /// <summary>Active ingredient substance name displayed as generic context.</summary>
        public string? SubstanceName { get; set; }

        /**************************************************************/
        /// <summary>Plus-delimited active-ingredient UNIIs represented by the summary row.</summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class code represented by the summary row.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name represented by the summary row.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>
        /// All active ingredients for the product, each paired with its preferred
        /// ("[EPC]") pharmacologic class.
        /// </summary>
        /// <remarks>
        /// Populated per document by <see cref="MedRecPro.DataAccess.AeDashboardDerivation.BuildActiveIngredients"/>
        /// so combination products list every ingredient rather than a single
        /// arbitrary stratum. Null or empty for rows built before aggregation.
        /// </remarks>
        public List<AeActiveIngredientDto>? ActiveIngredients { get; set; }

        #endregion Product and Class Properties

        #region Aggregate Count Properties

        /**************************************************************/
        /// <summary>Representative treatment-arm denominator across represented AE rows.</summary>
        public int? ArmN { get; set; }

        /**************************************************************/
        /// <summary>Representative comparator-arm denominator across represented AE rows.</summary>
        public int? ComparatorN { get; set; }

        /**************************************************************/
        /// <summary>Total number of represented materialized AE risk rows.</summary>
        public int RowCount { get; set; }

        /**************************************************************/
        /// <summary>Count of elevated or protective signals in the summary row.</summary>
        public int SignificantCount { get; set; }

        /**************************************************************/
        /// <summary>Count of protective significant signals in the summary row.</summary>
        public int SignificantProtectiveCount { get; set; }

        /**************************************************************/
        /// <summary>Count of elevated significant signals in the summary row.</summary>
        public int SignificantElevatedCount { get; set; }

        #endregion Aggregate Count Properties

        #region Dashboard Coverage Properties

        /**************************************************************/
        /// <summary>Flag indicating whether any represented row used a placebo-like comparator.</summary>
        public bool PlaceboCoverage { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether any represented row used an active comparator.</summary>
        public bool ActiveCoverage { get; set; }

        /**************************************************************/
        /// <summary>Fraction of represented rows with populated dose values.</summary>
        public double DoseCoverage { get; set; }

        /**************************************************************/
        /// <summary>Number of distinct SOC categories represented by the summary row.</summary>
        public int SocBreadth { get; set; }

        /**************************************************************/
        /// <summary>Total SOC denominator used by the dashboard coverage display.</summary>
        public int SocTotal { get; set; }

        /**************************************************************/
        /// <summary>Typed mono/combo/mixed product composition classification.</summary>
        public AeMonoComboMix? MonoComboMix { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether the current authenticated user has favorited this product.</summary>
        public bool IsFavorite { get; set; }

        #endregion Dashboard Coverage Properties

        #region Deferred Score Properties

        /**************************************************************/
        /// <summary>
        /// Derived chart-worthiness score used by the KPI strip.
        /// </summary>
        /// <remarks>
        /// Populated by a later dashboard derivation service from coverage,
        /// significant-signal density, precision, and dose availability.
        /// </remarks>
        public int? Score { get; set; }

        /**************************************************************/
        /// <summary>
        /// Derived explanation for <see cref="Score"/>.
        /// </summary>
        /// <remarks>Populated by a later dashboard derivation service.</remarks>
        public string? ScoreReason { get; set; }

        #endregion Deferred Score Properties
    }

    /**************************************************************/
    /// <summary>
    /// Slim product summary projection for the AE dashboard product picker.
    /// </summary>
    /// <remarks>
    /// Carries only the fields the picker renders, so the catalog payload stays
    /// small and a shared, cached, user-independent base can be served quickly.
    /// Produced from <see cref="AeDrugSummaryDto"/> after per-document aggregation,
    /// scoring, and favorite marking.
    /// </remarks>
    /// <example>
    /// <code>
    /// var item = new AeProductCatalogItemDto { ProductName = "ADVAIR HFA", Score = 58 };
    /// </code>
    /// </example>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="MedRecPro.DataAccess.DtoLabelAccess.GetAeProductCatalogAsync"/>
    public class AeProductCatalogItemDto
    {
        #region Catalog Properties

        /**************************************************************/
        /// <summary>Source SPL document identifier used as the picker key.</summary>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>Product display name shown in the picker.</summary>
        public string? ProductName { get; set; }

        /**************************************************************/
        /// <summary>Primary active ingredient substance name (first ingredient).</summary>
        public string? SubstanceName { get; set; }

        /**************************************************************/
        /// <summary>Plus-delimited active-ingredient UNIIs represented by the product.</summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>Primary preferred ("[EPC]") pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>All active ingredients paired with their preferred ("[EPC]") class.</summary>
        public List<AeActiveIngredientDto>? ActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>Typed mono/combo/mixed product composition classification.</summary>
        public AeMonoComboMix? MonoComboMix { get; set; }

        /**************************************************************/
        /// <summary>Derived chart-worthiness score used to rank picker rows.</summary>
        public int? Score { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether any represented row used a placebo-like comparator.</summary>
        public bool PlaceboCoverage { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether any represented row used an active comparator.</summary>
        public bool ActiveCoverage { get; set; }

        /**************************************************************/
        /// <summary>Flag indicating whether the current authenticated user has favorited this product.</summary>
        public bool IsFavorite { get; set; }

        #endregion Catalog Properties
    }

    /**************************************************************/
    /// <summary>
    /// Container DTO for one counseling tier in the AE triage dashboard.
    /// </summary>
    /// <remarks>
    /// Carries the display metadata and signals already assigned to a tier by the
    /// later derivation service.
    /// </remarks>
    /// <example>
    /// <code>
    /// var tier = new AeCounselingTierDto { Tier = AeCounselingTier.Counsel };
    /// </code>
    /// </example>
    /// <seealso cref="AeRiskSignalDto"/>
    public class AeCounselingTierDto
    {
        #region Tier Properties

        /**************************************************************/
        /// <summary>Counseling tier represented by this container.</summary>
        public AeCounselingTier Tier { get; set; }

        /**************************************************************/
        /// <summary>Display name for the counseling tier.</summary>
        public string? Name { get; set; }

        /**************************************************************/
        /// <summary>Display description for the counseling tier.</summary>
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>Signals assigned to this counseling tier.</summary>
        public List<AeRiskSignalDto> Signals { get; set; } = new();

        #endregion Tier Properties
    }

    /**************************************************************/
    /// <summary>
    /// Single-product triage view DTO for the AE dashboard flagship view.
    /// </summary>
    /// <remarks>
    /// Provides the product context plus counseling-tier containers. Tier
    /// assignment is intentionally outside this DTO.
    /// </remarks>
    /// <example>
    /// <code>
    /// var view = new AeTriageViewDto { Product = summary };
    /// </code>
    /// </example>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="AeCounselingTierDto"/>
    public class AeTriageViewDto
    {
        #region View Properties

        /**************************************************************/
        /// <summary>Product context for the triage view.</summary>
        public AeDrugSummaryDto? Product { get; set; }

        /**************************************************************/
        /// <summary>Counseling tiers shown by the triage view.</summary>
        public List<AeCounselingTierDto> Tiers { get; set; } = new();

        #endregion View Properties
    }

    /**************************************************************/
    /// <summary>
    /// Single-product forest plot DTO for log-scale RR visualization.
    /// </summary>
    /// <remarks>
    /// Axis ticks default to the prototype's primary log-scale tick set. The
    /// signal ordering and filtering are provided by later view assembly logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// var forest = new AeForestPlotDto { Signals = signals };
    /// </code>
    /// </example>
    /// <seealso cref="AeRiskSignalDto"/>
    public class AeForestPlotDto
    {
        #region Plot Properties

        /**************************************************************/
        /// <summary>Signals to render in the forest plot.</summary>
        public List<AeRiskSignalDto> Signals { get; set; } = new();

        /**************************************************************/
        /// <summary>Log-scale RR axis tick values.</summary>
        public double[] AxisTicks { get; set; } = new[] { 0.1, 0.25, 0.5, 1, 2, 4, 10 };

        #endregion Plot Properties
    }

    /**************************************************************/
    /// <summary>
    /// Point DTO for the AE risk-versus-precision quadrant view.
    /// </summary>
    /// <remarks>
    /// The coordinate and bubble-size values are deferred computations derived
    /// from RR confidence interval width, RR magnitude, and event counts.
    /// </remarks>
    /// <example>
    /// <code>
    /// var point = new AeQuadrantPointDto { Signal = signal };
    /// </code>
    /// </example>
    /// <seealso cref="AeRiskSignalDto"/>
    public class AeQuadrantPointDto
    {
        #region Signal Reference Properties

        /**************************************************************/
        /// <summary>Encrypted identifier of the signal represented by the point.</summary>
        public string? EncryptedFlattenedAdverseEventRiskTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Source tmp_FlattenedAdverseEventRiskTable identifier for server-side navigation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FlattenedAdverseEventRiskTableID =>
            !string.IsNullOrWhiteSpace(EncryptedFlattenedAdverseEventRiskTableID)
                ? Util.DecryptAndParseInt(EncryptedFlattenedAdverseEventRiskTableID)
                : null;

        /**************************************************************/
        /// <summary>Signal payload represented by the point.</summary>
        public AeRiskSignalDto? Signal { get; set; }

        #endregion Signal Reference Properties

        #region Deferred Coordinate Properties

        /**************************************************************/
        /// <summary>Deferred x-axis precision coordinate, bounded from zero to one.</summary>
        public double? PrecisionX { get; set; }

        /**************************************************************/
        /// <summary>Deferred y-axis effect-magnitude coordinate, bounded from zero to one.</summary>
        public double? MagnitudeY { get; set; }

        /**************************************************************/
        /// <summary>Deferred bubble size derived from treatment and comparator event counts.</summary>
        public double? BubbleSize { get; set; }

        /**************************************************************/
        /// <summary>Deferred visual direction classification for the quadrant point.</summary>
        public AeRiskSignificance? Direction { get; set; }

        #endregion Deferred Coordinate Properties
    }

    /**************************************************************/
    /// <summary>
    /// Single-product quadrant view DTO for AE risk and precision plotting.
    /// </summary>
    /// <remarks>
    /// Contains precomputed point DTOs. Coordinate derivation is outside this
    /// shape and belongs to the later dashboard derivation task.
    /// </remarks>
    /// <example>
    /// <code>
    /// var quadrant = new AeQuadrantViewDto { Points = points };
    /// </code>
    /// </example>
    /// <seealso cref="AeQuadrantPointDto"/>
    public class AeQuadrantViewDto
    {
        #region View Properties

        /**************************************************************/
        /// <summary>Points rendered by the quadrant view.</summary>
        public List<AeQuadrantPointDto> Points { get; set; } = new();

        #endregion View Properties
    }

    /**************************************************************/
    /// <summary>
    /// Reverse-lookup match DTO for one drug and signal pair.
    /// </summary>
    /// <remarks>
    /// Verdict assignment is a deferred computation based on signal direction,
    /// significance, and precision.
    /// </remarks>
    /// <example>
    /// <code>
    /// var match = new AeReverseLookupMatchDto { Drug = drug, Signal = signal };
    /// </code>
    /// </example>
    /// <seealso cref="AeReverseLookupResultDto"/>
    public class AeReverseLookupMatchDto
    {
        #region Match Properties

        /**************************************************************/
        /// <summary>Drug summary matched to the searched symptom.</summary>
        public AeDrugSummaryDto? Drug { get; set; }

        /**************************************************************/
        /// <summary>Signal matched to the searched symptom.</summary>
        public AeRiskSignalDto? Signal { get; set; }

        /**************************************************************/
        /// <summary>Deferred reverse-lookup verdict for the drug and signal pair.</summary>
        public AeReverseLookupVerdict? Verdict { get; set; }

        #endregion Match Properties
    }

    /**************************************************************/
    /// <summary>
    /// Reverse-lookup result DTO for a symptom-to-drug query.
    /// </summary>
    /// <remarks>
    /// The result contains matching drug/signal pairs plus an aggregate
    /// reassuring flag computed by later lookup assembly logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = new AeReverseLookupResultDto { Symptom = "Headache" };
    /// </code>
    /// </example>
    /// <seealso cref="AeReverseLookupMatchDto"/>
    public class AeReverseLookupResultDto
    {
        #region Result Properties

        /**************************************************************/
        /// <summary>Symptom or adverse-event term used as the lookup input.</summary>
        public string? Symptom { get; set; }

        /**************************************************************/
        /// <summary>Matched drug and signal pairs.</summary>
        public List<AeReverseLookupMatchDto> Matches { get; set; } = new();

        /**************************************************************/
        /// <summary>Flag indicating whether all matches were reassuring or low concern.</summary>
        public bool AllReassuring { get; set; }

        #endregion Result Properties
    }

    /**************************************************************/
    /// <summary>
    /// Interchange row DTO comparing one adverse-event term across two products.
    /// </summary>
    /// <remarks>
    /// Classification and delta label assignment are deferred computations based
    /// on the two product-specific signal records.
    /// </remarks>
    /// <example>
    /// <code>
    /// var row = new AeInterchangeRowDto { ParameterName = "Nausea" };
    /// </code>
    /// </example>
    /// <seealso cref="AeInterchangeComparisonDto"/>
    public class AeInterchangeRowDto
    {
        #region Row Properties

        /**************************************************************/
        /// <summary>Adverse-event term being compared.</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>SOC/category for the compared adverse-event term.</summary>
        public string? ParameterCategory { get; set; }

        /**************************************************************/
        /// <summary>Signal for product A, when present.</summary>
        public AeRiskSignalDto? SignalA { get; set; }

        /**************************************************************/
        /// <summary>Signal for product B, when present.</summary>
        public AeRiskSignalDto? SignalB { get; set; }

        /**************************************************************/
        /// <summary>Deferred interchange classification for the row.</summary>
        public AeInterchangeClass? Classification { get; set; }

        /**************************************************************/
        /// <summary>Deferred human-readable delta label for the row.</summary>
        public string? DeltaLabel { get; set; }

        #endregion Row Properties
    }

    /**************************************************************/
    /// <summary>
    /// Interchange comparison DTO for two AE dashboard products.
    /// </summary>
    /// <remarks>
    /// Contains compared product context, row-level differences, aggregate counts,
    /// and warnings for comparator or class mismatches.
    /// </remarks>
    /// <example>
    /// <code>
    /// var comparison = new AeInterchangeComparisonDto
    /// {
    ///     ProductA = productA,
    ///     ProductB = productB
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="AeInterchangeRowDto"/>
    public class AeInterchangeComparisonDto
    {
        #region Product Properties

        /**************************************************************/
        /// <summary>First product in the interchange comparison.</summary>
        public AeDrugSummaryDto? ProductA { get; set; }

        /**************************************************************/
        /// <summary>Second product in the interchange comparison.</summary>
        public AeDrugSummaryDto? ProductB { get; set; }

        #endregion Product Properties

        #region Row and Count Properties

        /**************************************************************/
        /// <summary>Compared adverse-event rows.</summary>
        public List<AeInterchangeRowDto> Rows { get; set; } = new();

        /**************************************************************/
        /// <summary>Count of compared signals present only on product A.</summary>
        public int OnlyACount { get; set; }

        /**************************************************************/
        /// <summary>Count of compared signals present only on product B.</summary>
        public int OnlyBCount { get; set; }

        /**************************************************************/
        /// <summary>Count of compared signals classified as similar.</summary>
        public int SimilarCount { get; set; }

        /**************************************************************/
        /// <summary>Count of compared signals worse on product A.</summary>
        public int AWorseCount { get; set; }

        /**************************************************************/
        /// <summary>Count of compared signals worse on product B.</summary>
        public int BWorseCount { get; set; }

        #endregion Row and Count Properties

        #region Warning Properties

        /**************************************************************/
        /// <summary>Warning text when products do not share a comparable pharmacologic class.</summary>
        public string? ClassMismatchWarning { get; set; }

        /**************************************************************/
        /// <summary>Warning text when products have different comparator coverage mixes.</summary>
        public string? ComparatorMismatchWarning { get; set; }

        #endregion Warning Properties
    }

    /**************************************************************/
    /// <summary>
    /// Static display metadata used by the AE dashboard DTO family.
    /// </summary>
    /// <remarks>
    /// Contains prototype display constants only. It does not parse flags, assign
    /// tiers, score products, compute coordinates, or map database entities.
    /// </remarks>
    /// <example>
    /// <code>
    /// var label = AeDashboardMetadata.TierNames[AeCounselingTier.Counsel];
    /// </code>
    /// </example>
    /// <seealso cref="AeCounselingTier"/>
    /// <seealso cref="AeDataQualityFlag"/>
    public static class AeDashboardMetadata
    {
        #region Display Constant Properties

        /**************************************************************/
        /// <summary>Total SOC denominator used by the dashboard coverage display.</summary>
        public const int SocTotal = 17;

        /**************************************************************/
        /// <summary>Display names for counseling tiers.</summary>
        public static IReadOnlyDictionary<AeCounselingTier, string> TierNames { get; } =
            new Dictionary<AeCounselingTier, string>
            {
                [AeCounselingTier.Counsel] = "Expect & counsel",
                [AeCounselingTier.Watch] = "Watch",
                [AeCounselingTier.Reassure] = "Reassure",
                [AeCounselingTier.Fragile] = "Low confidence - interpret with care"
            };

        /**************************************************************/
        /// <summary>Display descriptions for counseling tiers.</summary>
        public static IReadOnlyDictionary<AeCounselingTier, string> TierDescriptions { get; } =
            new Dictionary<AeCounselingTier, string>
            {
                [AeCounselingTier.Counsel] = "Common, tight-precision signals to mention to the patient up front.",
                [AeCounselingTier.Watch] = "Lower-probability signals in serious organ systems with red-flag instructions.",
                [AeCounselingTier.Reassure] = "Not significantly elevated, or significantly protective.",
                [AeCounselingTier.Fragile] = "Data-quality flags or extreme bounds. Do not drive counseling from these alone."
            };

        /**************************************************************/
        /// <summary>Display descriptions for parsed data-quality flags.</summary>
        public static IReadOnlyDictionary<AeDataQualityFlag, string> FlagText { get; } =
            new Dictionary<AeDataQualityFlag, string>
            {
                [AeDataQualityFlag.ZeroCellCorrected] = "Zero events in one arm; Haldane 0.5 correction applied.",
                [AeDataQualityFlag.SocRemap] = "MedDRA System Organ Class was remapped by Stage 5 processing.",
                [AeDataQualityFlag.WideCi] = "Confidence interval spans more than two orders of magnitude.",
                [AeDataQualityFlag.LowEventCount] = "Fewer than 10 total events."
            };

        /**************************************************************/
        /// <summary>SOC names treated as serious-organ-system context by tier derivation.</summary>
        public static IReadOnlySet<string> SocSerious { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cardiac",
                "Hepatobiliary",
                "Renal & Urinary",
                "Blood & Lymphatic",
                "Immune System",
                "Vascular",
                "Neoplasms"
            };

        #endregion Display Constant Properties
    }

    /**************************************************************/
    /// <summary>
    /// Precision class used by AE dashboard signal prioritization.
    /// </summary>
    /// <seealso cref="AeRiskSignalDto"/>
    public enum AePrecisionClass
    {
        Tight,
        Wide,
        Fragile
    }

    /**************************************************************/
    /// <summary>
    /// Typed number-needed interpretation used by AE dashboard signals.
    /// </summary>
    /// <seealso cref="AeRiskSignalDto"/>
    public enum AeNumberNeededType
    {
        None,
        NNH,
        NNT
    }

    /**************************************************************/
    /// <summary>
    /// Counseling tier used by the AE dashboard triage view.
    /// </summary>
    /// <seealso cref="AeCounselingTierDto"/>
    public enum AeCounselingTier
    {
        Counsel,
        Watch,
        Reassure,
        Fragile
    }

    /**************************************************************/
    /// <summary>
    /// Typed risk significance for dashboard direction and filtering.
    /// </summary>
    /// <seealso cref="AeRiskSignalDto"/>
    public enum AeRiskSignificance
    {
        NotSignificant,
        Elevated,
        Protective
    }

    /**************************************************************/
    /// <summary>
    /// Parsed Stage 5 data-quality flag used by dashboard display metadata.
    /// </summary>
    /// <seealso cref="AeDashboardMetadata"/>
    public enum AeDataQualityFlag
    {
        ZeroCellCorrected,
        SocRemap,
        WideCi,
        LowEventCount
    }

    /**************************************************************/
    /// <summary>
    /// Aggregate mono/combo mix used by product-level AE dashboard summaries.
    /// </summary>
    /// <seealso cref="AeDrugSummaryDto"/>
    public enum AeMonoComboMix
    {
        Mono,
        Combo,
        Mixed
    }

    /**************************************************************/
    /// <summary>
    /// Aggregate comparator mix used by dashboard filters and warnings.
    /// </summary>
    /// <seealso cref="AeInterchangeComparisonDto"/>
    public enum AeComparatorMix
    {
        Placebo,
        Active,
        Both
    }

    /**************************************************************/
    /// <summary>
    /// Interchange row classification for cross-product AE comparisons.
    /// </summary>
    /// <seealso cref="AeInterchangeRowDto"/>
    public enum AeInterchangeClass
    {
        OnlyA,
        OnlyB,
        Similar,
        AWorse,
        BWorse
    }

    /**************************************************************/
    /// <summary>
    /// Verdict assigned by reverse-lookup dashboard assembly.
    /// </summary>
    /// <seealso cref="AeReverseLookupMatchDto"/>
    public enum AeReverseLookupVerdict
    {
        PlausiblyCausal,
        Protective,
        NotSignificantlyElevated,
        LowConfidence
    }
}
