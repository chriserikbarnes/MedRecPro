using MedRecPro.Models;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// One drug-within-class observation feeding the SOC correlation pipeline.
    /// </summary>
    /// <remarks>
    /// ## Pipeline-only model — not a client contract
    /// This record is backend derivation plumbing: it is produced by
    /// <see cref="DtoLabelAccess"/> from materialized risk rows and consumed by
    /// <see cref="AeDashboardDerivation"/>. It is **never serialized to clients** and is
    /// intentionally kept out of <c>Models/AeDashboardDto.cs</c> so the Swagger-facing
    /// response contract stays free of backend plumbing. It is public only because the
    /// public derivation methods that accept it (for example
    /// <see cref="AeDashboardDerivation.BuildCorrelationMap"/>) and the focused pure tests
    /// reference it directly; do not add it to any controller response type.
    ///
    /// The observation unit is a drug (distinct active moiety) within the pharmacologic
    /// class; a stable document-derived key is used when the active moiety is null so two
    /// SPL labels of one molecule do not double-count. <see cref="LogRr"/> is computed in
    /// memory because the persisted log column is null for seeded rows.
    /// </remarks>
    /// <seealso cref="AeDashboardDerivation.BuildCorrelationMap"/>
    /// <seealso cref="AeCorrelationAggregate"/>
    public sealed record AeCorrelationObservation
    {
        #region Observation Properties

        /**************************************************************/
        /// <summary>Stable per-drug key (active moiety, or a document-derived fallback).</summary>
        public string DrugKey { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe drill-down provenance.</summary>
        public string? EncryptedActiveMoietyID { get; init; }

        /**************************************************************/
        /// <summary>Drug display name (substance, falling back to product).</summary>
        public string? DrugDisplayName { get; init; }

        /**************************************************************/
        /// <summary>Source SPL document identifier for the observation.</summary>
        public Guid? DocumentGUID { get; init; }

        /**************************************************************/
        /// <summary>MedDRA System Organ Class (ParameterCategory) of the observation.</summary>
        public string Soc { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Natural-log relative risk for the observation.</summary>
        public double LogRr { get; init; }

        /**************************************************************/
        /// <summary>Raw relative risk for the observation, retained for display.</summary>
        public double? Rr { get; init; }

        /**************************************************************/
        /// <summary>Derived precision class for the observation.</summary>
        public AePrecisionClass Precision { get; init; }

        /**************************************************************/
        /// <summary>Derived typed RR significance for the observation.</summary>
        public AeRiskSignificance RiskSignificance { get; init; }

        /**************************************************************/
        /// <summary>Whether the observation came from a combination-product row.</summary>
        public bool IsCombo { get; init; }

        /**************************************************************/
        /// <summary>Total (treatment + comparator) event count for the observation.</summary>
        public double Events { get; init; }

        #endregion Observation Properties
    }

    /**************************************************************/
    /// <summary>
    /// Aggregated per-(drug, SOC) value used by correlation and heatmap assembly.
    /// </summary>
    /// <remarks>
    /// ## Pipeline-only model — not a client contract
    /// This record is backend derivation plumbing produced by
    /// <see cref="AeDashboardDerivation.AggregatePerDrugSoc"/> and consumed only within the
    /// derivation layer; it is **never serialized to clients** and is intentionally kept out
    /// of <c>Models/AeDashboardDto.cs</c>. <see cref="Value"/> is the median or mean LogRR
    /// across a drug's terms in one SOC; <see cref="Precision"/> and
    /// <see cref="Significance"/> come from the strongest-magnitude term for display;
    /// <see cref="AnyFragile"/> is true when any contributing term is fragile, which a
    /// correlation cell surfaces honestly.
    /// </remarks>
    /// <seealso cref="AeDashboardDerivation.AggregatePerDrugSoc"/>
    /// <seealso cref="AeCorrelationObservation"/>
    public sealed record AeCorrelationAggregate(
        double Value,
        bool AnyFragile,
        int Count,
        AePrecisionClass Precision,
        AeRiskSignificance Significance);

    /**************************************************************/
    /// <summary>
    /// One in-memory page of class-picker rows plus matching class-count metadata.
    /// </summary>
    /// <remarks>
    /// This is a data-access transport record for controller pagination headers. It is not
    /// serialized directly; the public endpoint still returns the existing picker-item array.
    /// <paramref name="ChartableCount"/> counts the matching classes that can render at least
    /// one off-diagonal SOC map cell under the active filters.
    /// </remarks>
    /// <seealso cref="AePharmClassPickerItemDto"/>
    public sealed record AeCorrelationClassPickerPage(
        List<AePharmClassPickerItemDto> Items,
        int TotalCount,
        int ChartableCount);

    /**************************************************************/
    /// <summary>
    /// One class-within-system observation feeding the inverse correlation pipeline.
    /// </summary>
    /// <remarks>
    /// ## Pipeline-only model - not a client contract
    /// This record is backend derivation plumbing for the MedDRA-system-first lane. It is
    /// produced by <see cref="DtoLabelAccess"/> from materialized risk rows and consumed by
    /// <see cref="AeDashboardDerivation"/>. It is public for pure tests only and must not be
    /// serialized directly by controllers.
    /// </remarks>
    /// <seealso cref="AeDashboardDerivation.BuildSystemClassCorrelationMap"/>
    /// <seealso cref="AeSystemCorrelationAggregate"/>
    public sealed record AeSystemCorrelationObservation
    {
        #region Observation Properties

        /**************************************************************/
        /// <summary>Pharmacologic class code used as the class axis key.</summary>
        public string ClassCode { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Pharmacologic class display name.</summary>
        public string? ClassName { get; init; }

        /**************************************************************/
        /// <summary>Encrypted pharmacologic class identifier for client-safe navigation.</summary>
        public string? EncryptedPharmacologicClassID { get; init; }

        /**************************************************************/
        /// <summary>Canonical MedDRA System Organ Class (ParameterCategory).</summary>
        public string SystemOrganClass { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Stable key combining SOC and adverse-event term.</summary>
        public string TermKey { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Canonical adverse-event term display name.</summary>
        public string ParameterName { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Stable per-drug key shared with the class-first correlation lane.</summary>
        public string DrugKey { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Encrypted active moiety identifier for client-safe drill-down provenance.</summary>
        public string? EncryptedActiveMoietyID { get; init; }

        /**************************************************************/
        /// <summary>Drug display name (substance, falling back to product).</summary>
        public string? DrugDisplayName { get; init; }

        /**************************************************************/
        /// <summary>Source SPL document identifier for fallback provenance.</summary>
        public Guid? DocumentGUID { get; init; }

        /**************************************************************/
        /// <summary>Natural-log relative risk for the observation.</summary>
        public double LogRr { get; init; }

        /**************************************************************/
        /// <summary>Raw relative risk for display.</summary>
        public double? Rr { get; init; }

        /**************************************************************/
        /// <summary>Derived precision class for the observation.</summary>
        public AePrecisionClass Precision { get; init; }

        /**************************************************************/
        /// <summary>Derived RR significance for the observation.</summary>
        public AeRiskSignificance RiskSignificance { get; init; }

        /**************************************************************/
        /// <summary>Whether the observation came from a combination-product row.</summary>
        public bool IsCombo { get; init; }

        /**************************************************************/
        /// <summary>Total treatment plus comparator event count for the observation.</summary>
        public double Events { get; init; }

        #endregion Observation Properties
    }

    /**************************************************************/
    /// <summary>
    /// Aggregated class/term or class/drug value used by the system-first derivation lane.
    /// </summary>
    /// <remarks>
    /// The key lives in the dictionaries that hold this record, while the record carries the
    /// display and honesty metadata needed by map, heatmap, and cell-detail assembly.
    /// </remarks>
    /// <seealso cref="AeSystemCorrelationObservation"/>
    public sealed record AeSystemCorrelationAggregate(
        double Value,
        bool AnyFragile,
        int Count,
        int DrugCount,
        int TermCount,
        AePrecisionClass Precision,
        AeRiskSignificance Significance,
        string SystemOrganClass,
        string ParameterName,
        string? DrugDisplayName,
        string? EncryptedActiveMoietyID,
        Guid? DocumentGUID);

    /**************************************************************/
    /// <summary>
    /// One in-memory page of MedDRA-system picker rows plus matching renderability metadata.
    /// </summary>
    /// <remarks>
    /// This is a data-access transport record for controller pagination headers. It is not
    /// serialized directly; the public endpoint returns <see cref="AeMeddraSystemPickerItemDto"/>
    /// items and emits <paramref name="ChartableCount"/> through <c>X-Chartable-Count</c>.
    /// </remarks>
    /// <seealso cref="AeMeddraSystemPickerItemDto"/>
    public sealed record AeSystemPickerPage(
        List<AeMeddraSystemPickerItemDto> Items,
        int TotalCount,
        int ChartableCount);
}
