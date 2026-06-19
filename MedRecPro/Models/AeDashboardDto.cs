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
    /// Internal reusable AE dashboard detail payload for one product document.
    /// </summary>
    /// <remarks>
    /// Carries the product summary and derived signal list shared by the triage,
    /// forest plot, and quadrant views. The payload is intentionally not a new
    /// client contract; existing endpoint-specific DTOs are still assembled by
    /// the derivation layer.
    /// </remarks>
    /// <example>
    /// <code>
    /// var detail = new AeDashboardProductDetailData
    /// {
    ///     Product = new AeDrugSummaryDto { ProductName = "ASPIRIN" },
    ///     Signals = new List&lt;AeRiskSignalDto&gt;()
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="AeRiskSignalDto"/>
    public class AeDashboardProductDetailData
    {
        #region Detail Properties

        /**************************************************************/
        /// <summary>Product summary context for the requested document.</summary>
        public AeDrugSummaryDto Product { get; set; } = new();

        /**************************************************************/
        /// <summary>Derived adverse-event risk signals for the requested document.</summary>
        public List<AeRiskSignalDto> Signals { get; set; } = new();

        #endregion Detail Properties
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
                [AeCounselingTier.Watch] = "Lower-probability signals.",
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

    /**************************************************************/
    /// <summary>
    /// Correlation coefficient method used by the SOC × SOC correlation map.
    /// </summary>
    /// <remarks>
    /// Spearman (rank) is the default because it resists outliers and is more honest
    /// at the small per-cell sample sizes typical of a pharmacologic class. Pearson
    /// is opt-in for linear-on-log-scale relationships.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    public enum AeCorrelationMethod
    {
        Spearman,
        Pearson
    }

    /**************************************************************/
    /// <summary>
    /// Within-SOC per-drug aggregation used before correlating two SOCs.
    /// </summary>
    /// <remarks>
    /// A drug can have several adverse-event terms in one SOC; the terms are collapsed
    /// to a single LogRR value first. Median resists outlier terms (default); mean is
    /// opt-in.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    public enum AeCorrelationAggregation
    {
        MedianLogRr,
        MeanLogRr
    }

    /**************************************************************/
    /// <summary>
    /// Applied-filter echo shared by every correlation-map payload.
    /// </summary>
    /// <remarks>
    /// The same object is used to filter the input observations and is echoed back to
    /// the client so a rendered map can be reproduced exactly. Defaults are the strict,
    /// honesty-first settings: placebo comparator only, fragile rows excluded, a minimum
    /// drugs-per-cell floor, Spearman over median LogRR.
    /// </remarks>
    /// <example>
    /// <code>
    /// var filters = new AeCorrelationFilters { Comparator = AeComparatorMix.Placebo };
    /// </code>
    /// </example>
    /// <seealso cref="AeCorrelationMapDto"/>
    public sealed class AeCorrelationFilters
    {
        #region Filter Properties

        /**************************************************************/
        /// <summary>Comparator mix used to scope input rows; defaults to placebo-controlled only.</summary>
        public AeComparatorMix Comparator { get; set; } = AeComparatorMix.Placebo;

        /**************************************************************/
        /// <summary>Whether RR-non-significant input rows are retained before correlating.</summary>
        public bool IncludeNonSignificant { get; set; } = true;

        /**************************************************************/
        /// <summary>Whether fragile/wide-CI input rows are dropped before correlating.</summary>
        public bool ExcludeFragile { get; set; } = true;

        /**************************************************************/
        /// <summary>Minimum drugs a cell needs before a coefficient is returned (server floor 3).</summary>
        public int MinDrugsPerCell { get; set; } = 4;

        /**************************************************************/
        /// <summary>Correlation coefficient method.</summary>
        public AeCorrelationMethod Method { get; set; } = AeCorrelationMethod.Spearman;

        /**************************************************************/
        /// <summary>Within-SOC per-drug LogRR aggregation method.</summary>
        public AeCorrelationAggregation Aggregation { get; set; } = AeCorrelationAggregation.MedianLogRr;

        /**************************************************************/
        /// <summary>Whether the SOC axis is restricted to serious-organ-system categories.</summary>
        public bool SeriousSocOnly { get; set; } = false;

        /**************************************************************/
        /// <summary>Whether combination-product input rows are dropped before correlating.</summary>
        public bool ExcludeCombos { get; set; } = false;

        /**************************************************************/
        /// <summary>Minimum total (treatment + comparator) events an input row needs to count.</summary>
        public int MinEvents { get; set; } = 0;

        #endregion Filter Properties
    }

    /**************************************************************/
    /// <summary>
    /// One cell of the SOC × SOC correlation matrix.
    /// </summary>
    /// <remarks>
    /// Off-diagonal cells hold the correlation, across drugs in the class, of two SOCs'
    /// per-drug LogRR profiles. Thin cells below the drugs-per-cell floor return a null
    /// <see cref="Coefficient"/> with <see cref="InsufficientN"/> set and the real
    /// <see cref="PairCount"/>; diagonal cells return 1.0 and are flagged non-informative.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    public class AeCorrelationCellDto
    {
        #region Cell Properties

        /**************************************************************/
        /// <summary>Row index into the SOC axis.</summary>
        public int RowIndex { get; set; }

        /**************************************************************/
        /// <summary>Column index into the SOC axis.</summary>
        public int ColumnIndex { get; set; }

        /**************************************************************/
        /// <summary>Row SOC name.</summary>
        public string? RowSoc { get; set; }

        /**************************************************************/
        /// <summary>Column SOC name.</summary>
        public string? ColumnSoc { get; set; }

        /**************************************************************/
        /// <summary>Correlation coefficient, or null below the floor or with zero variance.</summary>
        public double? Coefficient { get; set; }

        /**************************************************************/
        /// <summary>Number of drugs present in both SOCs (the cell sample size).</summary>
        public int PairCount { get; set; }

        /**************************************************************/
        /// <summary>Two-sided p-value, when computable.</summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>Whether the coefficient is significant at p &lt; 0.05.</summary>
        public bool IsSignificant { get; set; }

        /**************************************************************/
        /// <summary>Whether any contributing observation was fragile.</summary>
        public bool IsFragile { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell fell below the minimum drugs-per-cell floor.</summary>
        public bool InsufficientN { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell is on the diagonal (non-informative 1.0).</summary>
        public bool IsDiagonal { get; set; }

        #endregion Cell Properties
    }

    /**************************************************************/
    /// <summary>
    /// Per-SOC summary row shown alongside the correlation matrix.
    /// </summary>
    /// <remarks>
    /// Gives the reader the marginal context a single cell cannot: how many drugs and
    /// fragile drugs are in the SOC, the central LogRR/RR, and the elevated/protective
    /// share of the SOC's terms.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    public class AeCorrelationSocSummaryDto
    {
        #region Summary Properties

        /**************************************************************/
        /// <summary>Index into the SOC axis.</summary>
        public int Index { get; set; }

        /**************************************************************/
        /// <summary>SOC name.</summary>
        public string? Soc { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs with data in this SOC.</summary>
        public int DrugCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs whose data in this SOC included a fragile term.</summary>
        public int FragileDrugCount { get; set; }

        /**************************************************************/
        /// <summary>Median per-drug LogRR across drugs in this SOC.</summary>
        public double? MedianLogRr { get; set; }

        /**************************************************************/
        /// <summary>Median per-drug RR (exp of <see cref="MedianLogRr"/>).</summary>
        public double? MedianRr { get; set; }

        /**************************************************************/
        /// <summary>Share of this SOC's terms classified elevated.</summary>
        public double ElevatedShare { get; set; }

        /**************************************************************/
        /// <summary>Share of this SOC's terms classified protective.</summary>
        public double ProtectiveShare { get; set; }

        #endregion Summary Properties
    }

    /**************************************************************/
    /// <summary>
    /// SOC × SOC correlation map scoped to one pharmacologic class.
    /// </summary>
    /// <remarks>
    /// ## Front-end rendering
    /// Render the matrix with a diverging, colorblind-safe scale centered at 0 (avoid
    /// red/green). Hatch or gray any cell where <see cref="AeCorrelationCellDto.InsufficientN"/>
    /// is true, and treat <see cref="AeCorrelationCellDto.IsDiagonal"/> cells as non-informative.
    /// The <see cref="Soc"/> array is the shared axis: index <c>i</c> is row and column <c>i</c>.
    /// Cells are the upper triangle including the diagonal; mirror them client-side. Always
    /// surface <see cref="Warnings"/> (small-n, mixed-comparator, fragile-included,
    /// pairwise-deletion-not-PSD) so the map reads honestly.
    /// </remarks>
    /// <seealso cref="AeCorrelationCellDto"/>
    /// <seealso cref="AeCorrelationSocSummaryDto"/>
    public class AeCorrelationMapDto
    {
        #region Class Context Properties

        /**************************************************************/
        /// <summary>Pharmacologic class code the map is scoped to.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            !string.IsNullOrWhiteSpace(EncryptedPharmacologicClassID)
                ? Util.DecryptAndParseInt(EncryptedPharmacologicClassID)
                : null;

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>Distinct drugs (observation units) in the class after filtering.</summary>
        public int DrugCount { get; set; }

        #endregion Class Context Properties

        #region Matrix Properties

        /**************************************************************/
        /// <summary>Ordered SOC axis; index i is both row i and column i.</summary>
        public List<string> Soc { get; set; } = new();

        /**************************************************************/
        /// <summary>Upper-triangle-including-diagonal correlation cells.</summary>
        public List<AeCorrelationCellDto> Cells { get; set; } = new();

        /**************************************************************/
        /// <summary>Per-SOC marginal summaries aligned to <see cref="Soc"/>.</summary>
        public List<AeCorrelationSocSummaryDto> SocSummaries { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for the front end to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Matrix Properties
    }

    /**************************************************************/
    /// <summary>
    /// One pharmacologic class option for the correlation-map class picker.
    /// </summary>
    /// <remarks>
    /// Scoped to classes that actually have AE risk rows after the active class-picker filters.
    /// <see cref="HasRenderableMap"/> means at least one off-diagonal SOC pair meets the active
    /// drugs-per-cell floor; <see cref="IsCorrelatable"/> is retained as a compatibility alias.
    /// The companion heatmap can still be useful when no SOC-pair map cells are renderable.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    /// <seealso cref="AeCorrelationCellDto"/>
    public class AePharmClassPickerItemDto
    {
        #region Picker Properties

        /**************************************************************/
        /// <summary>Pharmacologic class code used as the picker key.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            !string.IsNullOrWhiteSpace(EncryptedPharmacologicClassID)
                ? Util.DecryptAndParseInt(EncryptedPharmacologicClassID)
                : null;

        /**************************************************************/
        /// <summary>Distinct drugs (observation units) with AE rows in the class.</summary>
        public int DrugCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct SOC categories with AE rows in the class.</summary>
        public int SocCount { get; set; }

        /**************************************************************/
        /// <summary>Total possible off-diagonal SOC-pair cells for this class.</summary>
        /// <remarks>
        /// Counts the upper-triangle SOC pairs excluding the diagonal after class-picker
        /// filters. This is the denominator for map renderability, not a count of emitted DTO cells.
        /// </remarks>
        /// <seealso cref="AeCorrelationCellDto"/>
        public int TotalOffDiagonalCellCount { get; set; }

        /**************************************************************/
        /// <summary>Off-diagonal SOC-pair cells that meet the active drugs-per-cell floor.</summary>
        /// <remarks>
        /// A positive value means the SOC correlation map can display at least one usable,
        /// actionable off-diagonal cell for the current/default map floor.
        /// </remarks>
        /// <seealso cref="AeCorrelationMapDto"/>
        public int UsableMapCellCount { get; set; }

        /**************************************************************/
        /// <summary>Largest pairwise-complete drug count found across SOC pairs.</summary>
        /// <remarks>
        /// Helps clients explain classes that have SOC breadth but still fail the selected
        /// map floor, because the maximum pair count can be shown beside a no-map-cell badge.
        /// </remarks>
        /// <seealso cref="AeCorrelationCellDto"/>
        public int MaxPairCount { get; set; }

        /**************************************************************/
        /// <summary>Whether at least one off-diagonal SOC-pair cell can render on the map.</summary>
        /// <remarks>
        /// This is the preferred renderability flag for class-picker clients. It is based on
        /// pairwise-complete drug counts, not merely distinct drug and SOC totals.
        /// </remarks>
        /// <seealso cref="AeCorrelationMapDto"/>
        public bool HasRenderableMap { get; set; }

        /**************************************************************/
        /// <summary>Human-readable reason a class cannot render an off-diagonal map cell.</summary>
        /// <remarks>
        /// Null when <see cref="HasRenderableMap"/> is true. Intended for tooltip or title
        /// text so clients can label heatmap-only classes honestly.
        /// </remarks>
        /// <seealso cref="HasRenderableMap"/>
        public string? RenderabilityReason { get; set; }

        /**************************************************************/
        /// <summary>Compatibility alias for <see cref="HasRenderableMap"/>.</summary>
        /// <remarks>
        /// Older clients used this field when a two-drug/two-SOC heuristic was sufficient.
        /// New clients should read <see cref="HasRenderableMap"/> for SOC-pair map readiness.
        /// </remarks>
        /// <seealso cref="HasRenderableMap"/>
        public bool IsCorrelatable { get; set; }

        #endregion Picker Properties
    }

    /**************************************************************/
    /// <summary>
    /// One drug column of the SOC × drug RR heatmap.
    /// </summary>
    /// <seealso cref="AeCorrelationHeatmapDto"/>
    public class AeCorrelationHeatmapDrugDto
    {
        #region Drug Properties

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe navigation.</summary>
        public string? EncryptedActiveMoietyID { get; set; }

        /**************************************************************/
        /// <summary>Active moiety identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietyID =>
            !string.IsNullOrWhiteSpace(EncryptedActiveMoietyID)
                ? Util.DecryptAndParseInt(EncryptedActiveMoietyID)
                : null;

        /**************************************************************/
        /// <summary>Drug display name.</summary>
        public string? DrugDisplayName { get; set; }

        /**************************************************************/
        /// <summary>Source SPL document identifier for the drug column.</summary>
        public Guid? DocumentGUID { get; set; }

        #endregion Drug Properties
    }

    /**************************************************************/
    /// <summary>
    /// One populated cell of the SOC × drug RR heatmap.
    /// </summary>
    /// <remarks>
    /// Only populated (SOC, drug) pairs are emitted; the client fills the rest of the grid
    /// as empty. Precision and significance come from the drug's strongest-magnitude term in
    /// the SOC, and <see cref="TermCount"/> shows how many terms were aggregated.
    /// </remarks>
    /// <seealso cref="AeCorrelationHeatmapDto"/>
    public class AeCorrelationHeatmapCellDto
    {
        #region Cell Properties

        /**************************************************************/
        /// <summary>Row index into the SOC axis.</summary>
        public int SocIndex { get; set; }

        /**************************************************************/
        /// <summary>Column index into the drug axis.</summary>
        public int DrugIndex { get; set; }

        /**************************************************************/
        /// <summary>Aggregated LogRR for the (SOC, drug) cell.</summary>
        public double? LogRr { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR (exp of <see cref="LogRr"/>) for display.</summary>
        public double? Rr { get; set; }

        /**************************************************************/
        /// <summary>Representative precision class for the cell.</summary>
        public AePrecisionClass? Precision { get; set; }

        /**************************************************************/
        /// <summary>Representative RR significance for the cell.</summary>
        public AeRiskSignificance? Significance { get; set; }

        /**************************************************************/
        /// <summary>Number of adverse-event terms aggregated into the cell.</summary>
        public int TermCount { get; set; }

        #endregion Cell Properties
    }

    /**************************************************************/
    /// <summary>
    /// SOC × drug relative-risk heatmap; the honest small-n companion to the map.
    /// </summary>
    /// <remarks>
    /// Rows are SOCs, columns are drugs (deduplicated to active moiety), and each populated
    /// cell is an aggregated LogRR. This stays meaningful when a class is too small for a
    /// correlation map to be trustworthy.
    /// </remarks>
    /// <seealso cref="AeCorrelationMapDto"/>
    /// <seealso cref="AeCorrelationHeatmapCellDto"/>
    public class AeCorrelationHeatmapDto
    {
        #region Class Context Properties

        /**************************************************************/
        /// <summary>Pharmacologic class code the heatmap is scoped to.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            !string.IsNullOrWhiteSpace(EncryptedPharmacologicClassID)
                ? Util.DecryptAndParseInt(EncryptedPharmacologicClassID)
                : null;

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>Distinct drugs in the heatmap.</summary>
        public int DrugCount { get; set; }

        #endregion Class Context Properties

        #region Grid Properties

        /**************************************************************/
        /// <summary>Ordered SOC rows.</summary>
        public List<string> Soc { get; set; } = new();

        /**************************************************************/
        /// <summary>Ordered drug columns.</summary>
        public List<AeCorrelationHeatmapDrugDto> Drugs { get; set; } = new();

        /**************************************************************/
        /// <summary>Populated (SOC, drug) cells.</summary>
        public List<AeCorrelationHeatmapCellDto> Cells { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for the front end to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Grid Properties
    }

    /**************************************************************/
    /// <summary>
    /// One drug's paired (SOC X, SOC Y) LogRR behind a correlation cell.
    /// </summary>
    /// <remarks>
    /// These are the per-drug points the cell's coefficient is computed over — the answer to
    /// "why is this cell 0.9?". Encrypted moiety carries the drill-down provenance.
    /// </remarks>
    /// <seealso cref="AeCorrelationCellDetailDto"/>
    public class AeCorrelationDrugPairDto
    {
        #region Pair Properties

        /**************************************************************/
        /// <summary>Drug display name.</summary>
        public string? DrugDisplayName { get; set; }

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe provenance.</summary>
        public string? EncryptedActiveMoietyID { get; set; }

        /**************************************************************/
        /// <summary>Active moiety identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietyID =>
            !string.IsNullOrWhiteSpace(EncryptedActiveMoietyID)
                ? Util.DecryptAndParseInt(EncryptedActiveMoietyID)
                : null;

        /**************************************************************/
        /// <summary>Aggregated LogRR for the drug in SOC X.</summary>
        public double? LogRrX { get; set; }

        /**************************************************************/
        /// <summary>Aggregated LogRR for the drug in SOC Y.</summary>
        public double? LogRrY { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR for the drug in SOC X.</summary>
        public double? RrX { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR for the drug in SOC Y.</summary>
        public double? RrY { get; set; }

        /**************************************************************/
        /// <summary>Representative precision for the drug in SOC X.</summary>
        public AePrecisionClass? PrecisionX { get; set; }

        /**************************************************************/
        /// <summary>Representative precision for the drug in SOC Y.</summary>
        public AePrecisionClass? PrecisionY { get; set; }

        /**************************************************************/
        /// <summary>Adverse-event terms aggregated for the drug in SOC X.</summary>
        public int TermCountX { get; set; }

        /**************************************************************/
        /// <summary>Adverse-event terms aggregated for the drug in SOC Y.</summary>
        public int TermCountY { get; set; }

        #endregion Pair Properties
    }

    /**************************************************************/
    /// <summary>
    /// Drill-down detail behind one SOC × SOC correlation cell.
    /// </summary>
    /// <remarks>
    /// Returns the per-drug paired observations the cell was computed from, mirroring the
    /// triage/forest/quadrant drill pattern. The payload carries two coefficients so the
    /// drill-down stays honest the same way the map does:
    /// <list type="bullet">
    /// <item><see cref="Coefficient"/> is the <b>map-safe</b> value — it is null whenever
    /// <see cref="PairCount"/> is below <see cref="MinDrugsPerCell"/> (the same suppression
    /// the map applies), so a cell that reads null on the map also reads null here.</item>
    /// <item><see cref="RawCoefficient"/> is the <b>diagnostic</b> value — the unsuppressed
    /// pairwise-complete coefficient, available even below the floor for transparency.</item>
    /// </list>
    /// When the two differ, <see cref="InsufficientN"/> is the reason and a warning is added.
    /// <see cref="PValue"/>/<see cref="RawPValue"/> follow the same map-safe/raw split.
    /// </remarks>
    /// <seealso cref="AeCorrelationDrugPairDto"/>
    /// <seealso cref="AeCorrelationCellDto"/>
    public class AeCorrelationCellDetailDto
    {
        #region Cell Context Properties

        /**************************************************************/
        /// <summary>Pharmacologic class code the cell belongs to.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Row SOC of the cell.</summary>
        public string? SocX { get; set; }

        /**************************************************************/
        /// <summary>Column SOC of the cell.</summary>
        public string? SocY { get; set; }

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Map-safe correlation coefficient for the cell: null when
        /// <see cref="PairCount"/> is below <see cref="MinDrugsPerCell"/>, exactly as the
        /// map suppresses thin cells. Use <see cref="RawCoefficient"/> for the unsuppressed value.
        /// </summary>
        public double? Coefficient { get; set; }

        /**************************************************************/
        /// <summary>
        /// Unsuppressed diagnostic coefficient computed from the pairwise-complete drugs,
        /// available even below the drugs-per-cell floor. Null only for fewer than two pairs
        /// or zero variance (never a fabricated number or NaN).
        /// </summary>
        public double? RawCoefficient { get; set; }

        /**************************************************************/
        /// <summary>Map-safe two-sided p-value: null when the cell is below the floor or undefined.</summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Unsuppressed diagnostic two-sided p-value, available even below the floor. Null when
        /// statistically undefined (perfect ±1 correlation, fewer than three pairs, or zero
        /// variance) even if <see cref="RawCoefficient"/> exists; always null for diagonal cells.
        /// </summary>
        public double? RawPValue { get; set; }

        /**************************************************************/
        /// <summary>Whether the map-safe coefficient is significant at p &lt; 0.05.</summary>
        public bool IsSignificant { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell fell below <see cref="MinDrugsPerCell"/> (why <see cref="Coefficient"/> is null but <see cref="RawCoefficient"/> may not be).</summary>
        public bool InsufficientN { get; set; }

        /**************************************************************/
        /// <summary>Whether the requested SOCs are identical (a non-informative diagonal cell forced to 1.0).</summary>
        public bool IsDiagonal { get; set; }

        /**************************************************************/
        /// <summary>The clamped drugs-per-cell floor applied to derive <see cref="Coefficient"/> (hard minimum 3).</summary>
        public int MinDrugsPerCell { get; set; }

        /**************************************************************/
        /// <summary>Number of paired drugs behind the cell.</summary>
        public int PairCount { get; set; }

        #endregion Cell Context Properties

        #region Pair Properties

        /**************************************************************/
        /// <summary>Per-drug paired observations behind the cell.</summary>
        public List<AeCorrelationDrugPairDto> DrugPairs { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for the front end to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Pair Properties
    }

    /**************************************************************/
    /// <summary>
    /// Applied-filter echo for MedDRA-system-scoped class correlation payloads.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="AeCorrelationFilters"/> while naming the floor by the
    /// observation unit used in this inverse lane: shared selected-SOC terms.
    /// </remarks>
    /// <example>
    /// <code>
    /// var filters = new AeSystemCorrelationFilters { MinTermsPerCell = 4 };
    /// </code>
    /// </example>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    /// <seealso cref="AeSystemClassCorrelationCellDetailDto"/>
    public sealed class AeSystemCorrelationFilters
    {
        #region Filter Properties

        /**************************************************************/
        /// <summary>Comparator mix used to scope input rows; defaults to placebo-controlled only.</summary>
        public AeComparatorMix Comparator { get; set; } = AeComparatorMix.Placebo;

        /**************************************************************/
        /// <summary>Whether RR-non-significant input rows are retained before aggregation.</summary>
        public bool IncludeNonSignificant { get; set; } = true;

        /**************************************************************/
        /// <summary>Whether fragile/wide-CI input rows are dropped before aggregation.</summary>
        public bool ExcludeFragile { get; set; } = true;

        /**************************************************************/
        /// <summary>Minimum shared selected-SOC terms a class-pair cell needs for a map-safe coefficient.</summary>
        public int MinTermsPerCell { get; set; } = 4;

        /**************************************************************/
        /// <summary>Correlation coefficient method.</summary>
        public AeCorrelationMethod Method { get; set; } = AeCorrelationMethod.Spearman;

        /**************************************************************/
        /// <summary>Within-class aggregation method for LogRR values.</summary>
        public AeCorrelationAggregation Aggregation { get; set; } = AeCorrelationAggregation.MedianLogRr;

        /**************************************************************/
        /// <summary>Whether combination-product input rows are dropped before aggregation.</summary>
        public bool ExcludeCombos { get; set; } = false;

        /**************************************************************/
        /// <summary>Minimum total treatment plus comparator events an input row needs to count.</summary>
        public int MinEvents { get; set; } = 0;

        #endregion Filter Properties
    }

    /**************************************************************/
    /// <summary>
    /// Page metadata for paged correlation axes and detail rows.
    /// </summary>
    /// <remarks>
    /// Used in response bodies where headers cannot describe more than one independent
    /// axis, such as class-row and drug-column windows on a heatmap.
    /// </remarks>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    /// <seealso cref="AeSystemClassHeatmapDto"/>
    public class AeCorrelationAxisPageDto
    {
        #region Page Properties

        /**************************************************************/
        /// <summary>Current 1-based page number.</summary>
        public int PageNumber { get; set; }

        /**************************************************************/
        /// <summary>Number of items requested per page.</summary>
        public int PageSize { get; set; }

        /**************************************************************/
        /// <summary>Total items available before paging.</summary>
        public int TotalCount { get; set; }

        /**************************************************************/
        /// <summary>Total page count based on <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
        public int TotalPages { get; set; }

        /**************************************************************/
        /// <summary>Whether a prior page exists.</summary>
        public bool HasPreviousPage { get; set; }

        /**************************************************************/
        /// <summary>Whether a later page exists.</summary>
        public bool HasNextPage { get; set; }

        #endregion Page Properties
    }

    /**************************************************************/
    /// <summary>
    /// One MedDRA System Organ Class option for the inverse correlation picker.
    /// </summary>
    /// <remarks>
    /// The key is the canonical <see cref="AeRiskSignalDto.ParameterCategory"/> value already
    /// present in dashboard risk rows. Renderability is based on whether any class-pair
    /// term-profile cell can meet the active terms-per-cell floor.
    /// </remarks>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    public class AeMeddraSystemPickerItemDto
    {
        #region Picker Properties

        /**************************************************************/
        /// <summary>Canonical MedDRA System Organ Class display name.</summary>
        public string? SystemOrganClass { get; set; }

        /**************************************************************/
        /// <summary>Distinct pharmacologic classes with usable rows in this system.</summary>
        public int ClassCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs with usable rows in this system.</summary>
        public int DrugCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct adverse-event terms with usable rows in this system.</summary>
        public int TermCount { get; set; }

        /**************************************************************/
        /// <summary>Total possible off-diagonal class-pair cells for this system.</summary>
        public int TotalOffDiagonalCellCount { get; set; }

        /**************************************************************/
        /// <summary>Off-diagonal class-pair cells meeting the active terms-per-cell floor.</summary>
        public int UsableMapCellCount { get; set; }

        /**************************************************************/
        /// <summary>Largest shared-term count found across class pairs in this system.</summary>
        public int MaxPairCount { get; set; }

        /**************************************************************/
        /// <summary>Whether at least one off-diagonal class-pair cell can render on the map.</summary>
        public bool HasRenderableMap { get; set; }

        /**************************************************************/
        /// <summary>Human-readable reason the system cannot render an off-diagonal map cell.</summary>
        public string? RenderabilityReason { get; set; }

        #endregion Picker Properties
    }

    /**************************************************************/
    /// <summary>
    /// One pharmacologic class axis item in a MedDRA-system-scoped response.
    /// </summary>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    /// <seealso cref="AeSystemClassHeatmapDto"/>
    public class AeSystemClassAxisItemDto
    {
        #region Axis Properties

        /**************************************************************/
        /// <summary>Index of this class within the returned page window.</summary>
        public int Index { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class code used by cell-detail requests.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            !string.IsNullOrWhiteSpace(EncryptedPharmacologicClassID)
                ? Util.DecryptAndParseInt(EncryptedPharmacologicClassID)
                : null;

        /**************************************************************/
        /// <summary>Distinct selected-SOC terms contributing to this class axis item.</summary>
        public int TermCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs contributing to this class axis item.</summary>
        public int DrugCount { get; set; }

        /**************************************************************/
        /// <summary>Whether this class has at least one renderable off-diagonal class-pair cell.</summary>
        public bool HasRenderableMap { get; set; }

        #endregion Axis Properties
    }

    /**************************************************************/
    /// <summary>
    /// One class-pair cell in the system-scoped class correlation matrix.
    /// </summary>
    /// <remarks>
    /// Off-diagonal coefficients correlate selected-SOC adverse-event term profiles across
    /// pharmacologic classes. Diagonal cells are non-informative and forced to 1.0.
    /// </remarks>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    public class AeSystemClassCorrelationCellDto
    {
        #region Cell Properties

        /**************************************************************/
        /// <summary>Row index into the returned class axis window.</summary>
        public int RowIndex { get; set; }

        /**************************************************************/
        /// <summary>Column index into the returned class axis window.</summary>
        public int ColumnIndex { get; set; }

        /**************************************************************/
        /// <summary>Row pharmacologic class code.</summary>
        public string? RowClassCode { get; set; }

        /**************************************************************/
        /// <summary>Column pharmacologic class code.</summary>
        public string? ColumnClassCode { get; set; }

        /**************************************************************/
        /// <summary>Map-safe correlation coefficient, or null below the floor or when undefined.</summary>
        public double? Coefficient { get; set; }

        /**************************************************************/
        /// <summary>Number of shared selected-SOC terms behind the cell.</summary>
        public int PairCount { get; set; }

        /**************************************************************/
        /// <summary>Two-sided p-value, when computable and not suppressed by the floor.</summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>Whether the map-safe coefficient is significant at p &lt; 0.05.</summary>
        public bool IsSignificant { get; set; }

        /**************************************************************/
        /// <summary>Whether any contributing aggregate included fragile rows.</summary>
        public bool IsFragile { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell fell below the minimum terms-per-cell floor.</summary>
        public bool InsufficientN { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell is on the non-informative diagonal.</summary>
        public bool IsDiagonal { get; set; }

        #endregion Cell Properties
    }

    /**************************************************************/
    /// <summary>
    /// Per-class marginal summary aligned to a system-scoped class axis.
    /// </summary>
    /// <seealso cref="AeSystemClassCorrelationMapDto"/>
    public class AeSystemClassSummaryDto
    {
        #region Summary Properties

        /**************************************************************/
        /// <summary>Index into the returned class axis window.</summary>
        public int Index { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class code.</summary>
        public string? PharmClassCode { get; set; }

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs represented by this class under the selected systems.</summary>
        public int DrugCount { get; set; }

        /**************************************************************/
        /// <summary>Distinct selected-SOC terms represented by this class.</summary>
        public int TermCount { get; set; }

        /**************************************************************/
        /// <summary>Median class-term LogRR across selected systems.</summary>
        public double? MedianLogRr { get; set; }

        /**************************************************************/
        /// <summary>Median class-term RR across selected systems.</summary>
        public double? MedianRr { get; set; }

        /**************************************************************/
        /// <summary>Share of contributing rows classified elevated.</summary>
        public double ElevatedShare { get; set; }

        /**************************************************************/
        /// <summary>Share of contributing rows classified protective.</summary>
        public double ProtectiveShare { get; set; }

        #endregion Summary Properties
    }

    /**************************************************************/
    /// <summary>
    /// Pharmacologic-class x pharmacologic-class correlation map scoped to one selected MedDRA system.
    /// </summary>
    /// <remarks>
    /// Cells correlate selected-SOC term profiles across classes, not shared drugs. The class
    /// axis is pruned to renderable off-diagonal pairs and paged symmetrically unless
    /// <see cref="IncludesFullMatrix"/> is true. <see cref="Warnings"/> explains pairwise
    /// deletion, mixed comparator requests, fragile inclusion, below-floor cells, and pruning.
    /// </remarks>
    /// <seealso cref="AeSystemClassCorrelationCellDto"/>
    /// <seealso cref="AeSystemClassSummaryDto"/>
    public class AeSystemClassCorrelationMapDto
    {
        #region Context Properties

        /**************************************************************/
        /// <summary>Canonical selected MedDRA System Organ Class echoed from the data.</summary>
        public List<string> SelectedSystems { get; set; } = new();

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeSystemCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>Total selected-SOC classes after filters, renderability pruning, and before axis paging.</summary>
        public int ClassCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Whether the returned class axis and cells cover every filtered class instead of a paged matrix window.
        /// </summary>
        public bool IncludesFullMatrix { get; set; }

        /**************************************************************/
        /// <summary>Page metadata for the returned class axis window.</summary>
        public AeCorrelationAxisPageDto ClassPage { get; set; } = new();

        #endregion Context Properties

        #region Matrix Properties

        /**************************************************************/
        /// <summary>Returned pharmacologic class axis window.</summary>
        public List<AeSystemClassAxisItemDto> Classes { get; set; } = new();

        /**************************************************************/
        /// <summary>Upper-triangle-including-diagonal class-pair cells for the returned class window.</summary>
        public List<AeSystemClassCorrelationCellDto> Cells { get; set; } = new();

        /**************************************************************/
        /// <summary>Per-class marginal summaries aligned to <see cref="Classes"/>.</summary>
        public List<AeSystemClassSummaryDto> ClassSummaries { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for clients to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Matrix Properties
    }

    /**************************************************************/
    /// <summary>
    /// One drug column in a system-scoped class x drug heatmap.
    /// </summary>
    /// <seealso cref="AeSystemClassHeatmapDto"/>
    public class AeSystemClassHeatmapDrugDto
    {
        #region Drug Properties

        /**************************************************************/
        /// <summary>Index of this drug within the returned drug axis window.</summary>
        public int Index { get; set; }

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe navigation.</summary>
        public string? EncryptedActiveMoietyID { get; set; }

        /**************************************************************/
        /// <summary>Active moiety identifier for server-side navigation.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietyID =>
            !string.IsNullOrWhiteSpace(EncryptedActiveMoietyID)
                ? Util.DecryptAndParseInt(EncryptedActiveMoietyID)
                : null;

        /**************************************************************/
        /// <summary>Drug display name.</summary>
        public string? DrugDisplayName { get; set; }

        /**************************************************************/
        /// <summary>Source SPL document identifier for fallback provenance.</summary>
        public Guid? DocumentGUID { get; set; }

        #endregion Drug Properties
    }

    /**************************************************************/
    /// <summary>
    /// One populated cell of a system-scoped class x drug heatmap.
    /// </summary>
    /// <remarks>
    /// Empty class/drug intersections are absent from <see cref="AeSystemClassHeatmapDto.Cells"/>.
    /// </remarks>
    /// <seealso cref="AeSystemClassHeatmapDto"/>
    public class AeSystemClassHeatmapCellDto
    {
        #region Cell Properties

        /**************************************************************/
        /// <summary>Row index into the returned class axis window.</summary>
        public int ClassIndex { get; set; }

        /**************************************************************/
        /// <summary>Column index into the returned drug axis window.</summary>
        public int DrugIndex { get; set; }

        /**************************************************************/
        /// <summary>Aggregated LogRR for the selected systems, class, and drug.</summary>
        public double? LogRr { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR for display.</summary>
        public double? Rr { get; set; }

        /**************************************************************/
        /// <summary>Representative precision class for the cell.</summary>
        public AePrecisionClass? Precision { get; set; }

        /**************************************************************/
        /// <summary>Representative RR significance for the cell.</summary>
        public AeRiskSignificance? Significance { get; set; }

        /**************************************************************/
        /// <summary>Number of selected-SOC terms aggregated into the cell.</summary>
        public int TermCount { get; set; }

        #endregion Cell Properties
    }

    /**************************************************************/
    /// <summary>
    /// Pharmacologic-class x drug relative-risk heatmap scoped to selected MedDRA systems.
    /// </summary>
    /// <seealso cref="AeSystemClassHeatmapCellDto"/>
    /// <seealso cref="AeSystemClassHeatmapDrugDto"/>
    public class AeSystemClassHeatmapDto
    {
        #region Context Properties

        /**************************************************************/
        /// <summary>Canonical selected MedDRA System Organ Classes echoed from the data.</summary>
        public List<string> SelectedSystems { get; set; } = new();

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeSystemCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>Page metadata for the returned class rows.</summary>
        public AeCorrelationAxisPageDto ClassPage { get; set; } = new();

        /**************************************************************/
        /// <summary>Page metadata for the returned drug columns.</summary>
        public AeCorrelationAxisPageDto DrugPage { get; set; } = new();

        #endregion Context Properties

        #region Grid Properties

        /**************************************************************/
        /// <summary>Returned pharmacologic class rows.</summary>
        public List<AeSystemClassAxisItemDto> Classes { get; set; } = new();

        /**************************************************************/
        /// <summary>Returned drug columns.</summary>
        public List<AeSystemClassHeatmapDrugDto> Drugs { get; set; } = new();

        /**************************************************************/
        /// <summary>Populated class/drug cells only.</summary>
        public List<AeSystemClassHeatmapCellDto> Cells { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for clients to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Grid Properties
    }

    /**************************************************************/
    /// <summary>
    /// One shared selected-SOC term pair behind a system-scoped class-pair cell.
    /// </summary>
    /// <seealso cref="AeSystemClassCorrelationCellDetailDto"/>
    public class AeSystemClassTermPairDto
    {
        #region Pair Properties

        /**************************************************************/
        /// <summary>MedDRA System Organ Class for the shared term.</summary>
        public string? SystemOrganClass { get; set; }

        /**************************************************************/
        /// <summary>Canonical adverse-event term name.</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>Aggregated LogRR for class X.</summary>
        public double? LogRrX { get; set; }

        /**************************************************************/
        /// <summary>Aggregated LogRR for class Y.</summary>
        public double? LogRrY { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR for class X.</summary>
        public double? RrX { get; set; }

        /**************************************************************/
        /// <summary>Aggregated RR for class Y.</summary>
        public double? RrY { get; set; }

        /**************************************************************/
        /// <summary>Representative precision for class X.</summary>
        public AePrecisionClass? PrecisionX { get; set; }

        /**************************************************************/
        /// <summary>Representative precision for class Y.</summary>
        public AePrecisionClass? PrecisionY { get; set; }

        /**************************************************************/
        /// <summary>Representative significance for class X.</summary>
        public AeRiskSignificance? SignificanceX { get; set; }

        /**************************************************************/
        /// <summary>Representative significance for class Y.</summary>
        public AeRiskSignificance? SignificanceY { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs aggregated for class X on this term.</summary>
        public int DrugCountX { get; set; }

        /**************************************************************/
        /// <summary>Distinct drugs aggregated for class Y on this term.</summary>
        public int DrugCountY { get; set; }

        /**************************************************************/
        /// <summary>Term row count aggregated for class X.</summary>
        public int TermCountX { get; set; }

        /**************************************************************/
        /// <summary>Term row count aggregated for class Y.</summary>
        public int TermCountY { get; set; }

        #endregion Pair Properties
    }

    /**************************************************************/
    /// <summary>
    /// Drill-down detail behind one system-scoped class-pair correlation cell.
    /// </summary>
    /// <remarks>
    /// <see cref="Coefficient"/> is the map-safe value suppressed below
    /// <see cref="MinTermsPerCell"/>; <see cref="RawCoefficient"/> is diagnostic and
    /// unsuppressed for transparency.
    /// </remarks>
    /// <seealso cref="AeSystemClassTermPairDto"/>
    /// <seealso cref="AeSystemClassCorrelationCellDto"/>
    public class AeSystemClassCorrelationCellDetailDto
    {
        #region Cell Context Properties

        /**************************************************************/
        /// <summary>Canonical selected MedDRA System Organ Classes echoed from the data.</summary>
        public List<string> SelectedSystems { get; set; } = new();

        /**************************************************************/
        /// <summary>Row pharmacologic class.</summary>
        public AeSystemClassAxisItemDto? ClassX { get; set; }

        /**************************************************************/
        /// <summary>Column pharmacologic class.</summary>
        public AeSystemClassAxisItemDto? ClassY { get; set; }

        /**************************************************************/
        /// <summary>Applied filters echoed for reproducibility.</summary>
        public AeSystemCorrelationFilters AppliedFilters { get; set; } = new();

        /**************************************************************/
        /// <summary>Map-safe correlation coefficient for the cell.</summary>
        public double? Coefficient { get; set; }

        /**************************************************************/
        /// <summary>Unsuppressed diagnostic coefficient computed from shared terms.</summary>
        public double? RawCoefficient { get; set; }

        /**************************************************************/
        /// <summary>Map-safe two-sided p-value, when computable.</summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>Unsuppressed diagnostic two-sided p-value, when computable.</summary>
        public double? RawPValue { get; set; }

        /**************************************************************/
        /// <summary>Whether the map-safe coefficient is significant at p &lt; 0.05.</summary>
        public bool IsSignificant { get; set; }

        /**************************************************************/
        /// <summary>Number of shared selected-SOC terms behind the cell.</summary>
        public int PairCount { get; set; }

        /**************************************************************/
        /// <summary>Clamped terms-per-cell floor applied to <see cref="Coefficient"/>.</summary>
        public int MinTermsPerCell { get; set; }

        /**************************************************************/
        /// <summary>Whether the cell fell below <see cref="MinTermsPerCell"/>.</summary>
        public bool InsufficientN { get; set; }

        /**************************************************************/
        /// <summary>Whether the requested classes are identical.</summary>
        public bool IsDiagonal { get; set; }

        /**************************************************************/
        /// <summary>Page metadata for the returned term-pair rows.</summary>
        public AeCorrelationAxisPageDto TermPairPage { get; set; } = new();

        #endregion Cell Context Properties

        #region Pair Properties

        /**************************************************************/
        /// <summary>Shared selected-SOC adverse-event terms behind the coefficient.</summary>
        public List<AeSystemClassTermPairDto> TermPairs { get; set; } = new();

        /**************************************************************/
        /// <summary>Honesty warnings for clients to surface.</summary>
        public List<string> Warnings { get; set; } = new();

        #endregion Pair Properties
    }
}
