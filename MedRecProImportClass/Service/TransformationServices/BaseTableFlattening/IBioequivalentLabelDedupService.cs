using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Filters an ordered list of SPL <c>DocumentGUID</c>s down to a single canonical
    /// label per bioequivalent-drug group so the table-parsing pipeline does not
    /// process duplicate copies of the same innovator information.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Runs once between Stage 0 (document discovery —
    /// <see cref="ITableCellContextService.GetDocumentGuidsOrderedByUniiAsync"/>) and
    /// Stage 1 (cell fetch). Does not touch any cell-level data; only reshapes the
    /// document set.
    ///
    /// ## Why This Exists
    /// Multiple ANDAs (generics) and their repackager/relabel labels all reference
    /// the same innovator NDA, so they carry identical table content. Parsing all of
    /// them inflates aggregate counts (e.g., a single published PK value appearing
    /// 40+ times for a widely-genericized drug). This service collapses each
    /// bioequivalent group to one canonical <c>DocumentGUID</c>, preferring the
    /// innovator NDA when present and otherwise picking the ANDA label with the most
    /// recent <c>LabelEffectiveDate</c>.
    ///
    /// ## Grouping Key
    /// Products are grouped by a normalized tuple of (Ingredient, DosageForm, Route)
    /// drawn from <see cref="MedRecProImportClass.Models.OrangeBook.Product"/>.
    /// A single application (NDA or ANDA) typically covers all strengths of the same
    /// drug product under one label, so strength is intentionally excluded.
    ///
    /// ## Ordering
    /// The output preserves the input walk order (UNII-ordered from Stage 0) so the
    /// ML anomaly-model key accumulator keeps same-UNII documents adjacent in the
    /// batch sequence.
    /// </remarks>
    /// <seealso cref="BioequivalentDedupOptions"/>
    /// <seealso cref="BioequivalentDedupResult"/>
    /// <seealso cref="ITableCellContextService.GetDocumentGuidsOrderedByUniiAsync"/>
    /// <seealso cref="ITableParsingOrchestrator"/>
    public interface IBioequivalentLabelDedupService
    {
        /**************************************************************/
        /// <summary>
        /// Reduces <paramref name="orderedDocumentGuids"/> to the canonical label per
        /// bioequivalent group and returns the filtered list plus diagnostic metrics.
        /// </summary>
        /// <param name="orderedDocumentGuids">UNII-ordered document identifiers from
        /// Stage 0. The original order is preserved in the kept subset so downstream
        /// batching stays aligned with the ML training-data locality contract.</param>
        /// <param name="options">Optional knobs that control classification and
        /// selection. <c>null</c> applies defaults (drop unclassifiable, prefer NDA,
        /// most-recent-LabelEffectiveDate ANDA selection).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="BioequivalentDedupResult"/> with the kept GUIDs, the
        /// dropped GUIDs and their reasons, and aggregate group counts.</returns>
        Task<BioequivalentDedupResult> DeduplicateAsync(
            IReadOnlyList<Guid> orderedDocumentGuids,
            BioequivalentDedupOptions? options = null,
            CancellationToken ct = default);
    }

    /**************************************************************/
    /// <summary>
    /// Configuration knobs for
    /// <see cref="IBioequivalentLabelDedupService.DeduplicateAsync"/>. All properties
    /// default to the production behavior agreed in the plan review.
    /// </summary>
    /// <remarks>
    /// Defaults:
    /// <list type="bullet">
    ///   <item><c>DropUnclassifiable = true</c> — documents without a resolvable
    ///   Orange Book (ApplType, ApplNo) are dropped to keep the aggregate signal clean.</item>
    ///   <item><c>PreferNdaOverAnda = true</c> — when a group contains both an NDA
    ///   and ANDAs, only the NDA candidates compete for the canonical slot.</item>
    ///   <item><c>AndaSelection = MostRecentLabel</c> — ties are broken by
    ///   <c>VersionNumber DESC</c>, then <c>DocumentGUID ASC</c>.</item>
    /// </list>
    /// </remarks>
    public sealed class BioequivalentDedupOptions
    {
        /// <summary>
        /// When true (default) drop DocumentGUIDs that cannot be classified as NDA or
        /// ANDA via the Orange Book join. When false, keep them with no dedup applied.
        /// </summary>
        public bool DropUnclassifiable { get; init; } = true;

        /// <summary>
        /// When true (default) and a group contains both NDAs and ANDAs, the canonical
        /// label is chosen from the NDA partition only.
        /// </summary>
        public bool PreferNdaOverAnda { get; init; } = true;

        /// <summary>
        /// Selection strategy used within the chosen tier (NDA or ANDA). The v1
        /// implementation supports only <see cref="AndaSelectionStrategy.MostRecentLabel"/>;
        /// retained as an enum for future expansion.
        /// </summary>
        public AndaSelectionStrategy AndaSelection { get; init; } = AndaSelectionStrategy.MostRecentLabel;
    }

    /**************************************************************/
    /// <summary>
    /// Strategies for choosing a single canonical label within an NDA or ANDA tier
    /// once the bioequivalent group has been identified.
    /// </summary>
    public enum AndaSelectionStrategy
    {
        /**************************************************************/
        /// <summary>
        /// Pick the candidate with the most recent
        /// <see cref="MedRecProImportClass.Models.LabelView.ProductsByLabeler.LabelEffectiveDate"/>.
        /// Ties are broken by higher <c>VersionNumber</c>, then lower <c>DocumentGUID</c>.
        /// </summary>
        MostRecentLabel
    }

    /**************************************************************/
    /// <summary>
    /// Result object returned by
    /// <see cref="IBioequivalentLabelDedupService.DeduplicateAsync"/>. Carries the
    /// filtered document list, audit trail of dropped documents, and aggregate
    /// counts suitable for log output.
    /// </summary>
    public sealed class BioequivalentDedupResult
    {
        /// <summary>
        /// Canonical DocumentGUIDs in the same relative order as the input list.
        /// </summary>
        public IReadOnlyList<Guid> KeptDocumentGuids { get; init; } = Array.Empty<Guid>();

        /// <summary>
        /// Documents removed by the filter, each paired with a human-readable reason.
        /// Useful for run-time diagnostics and unit-test assertions.
        /// </summary>
        public IReadOnlyList<DroppedDocument> DroppedDocuments { get; init; } = Array.Empty<DroppedDocument>();

        /// <summary>
        /// Number of distinct bioequivalent groups identified in the input set.
        /// </summary>
        public int GroupCount { get; init; }

        /// <summary>
        /// Number of groups whose canonical label was chosen from the NDA partition.
        /// </summary>
        public int NdaGroupCount { get; init; }

        /// <summary>
        /// Number of groups whose canonical label was chosen from the ANDA partition
        /// (i.e., no NDA was present in the group).
        /// </summary>
        public int AndaGroupCount { get; init; }

        /// <summary>
        /// Number of input DocumentGUIDs that could not be classified (no
        /// ApplicationNumber or no Orange Book match).
        /// </summary>
        public int UnclassifiableCount { get; init; }
    }

    /**************************************************************/
    /// <summary>
    /// A single DocumentGUID that was removed from the input set, paired with the
    /// reason and (when available) the bioequivalent group key it belonged to.
    /// </summary>
    /// <param name="DocumentGuid">The removed document identifier.</param>
    /// <param name="Reason">Human-readable cause — one of
    /// <c>bioequivalent_duplicate:{group}</c>, <c>unclassifiable:no_application_number</c>,
    /// <c>unclassifiable:unrecognized_prefix</c>, or
    /// <c>unclassifiable:no_orange_book_match</c>.</param>
    /// <param name="GroupKey">The normalized (Ingredient|DosageForm|Route) key when
    /// the document was classified, or <c>null</c> for unclassifiable documents.</param>
    public sealed record DroppedDocument(Guid DocumentGuid, string Reason, string? GroupKey);
}
