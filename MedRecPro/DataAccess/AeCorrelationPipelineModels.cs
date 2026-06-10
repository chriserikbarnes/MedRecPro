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
}
