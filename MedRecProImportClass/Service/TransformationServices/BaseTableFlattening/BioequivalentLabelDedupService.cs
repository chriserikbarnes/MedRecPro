using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MedRecProImportClass.Data;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Default implementation of <see cref="IBioequivalentLabelDedupService"/>. Resolves each
    /// <c>DocumentGUID</c> to its SPL ApplicationNumber via <c>vw_ProductsByLabeler</c>,
    /// joins to <see cref="OrangeBook.Product"/> to obtain
    /// <c>(ApplType, Ingredient, DosageForm, Route, ApprovalDate)</c>, then picks one
    /// canonical label per bioequivalent group preferring NDA over ANDA.
    /// </summary>
    /// <remarks>
    /// ## Data Flow
    /// <list type="number">
    /// <item>Load <c>vw_ProductsByLabeler</c> rows for the input GUIDs (one round-trip).</item>
    /// <item>Parse each ApplicationNumber via <see cref="ApplicationNumberParser.TryParse"/>
    /// to obtain the Orange Book composite key (<c>ApplType</c>, <c>ApplNo</c>).</item>
    /// <item>Load <see cref="OrangeBook.Product"/> rows for the candidate
    /// (<c>ApplType</c>, <c>ApplNo</c>) set (one round-trip).</item>
    /// <item>Group by normalized (Ingredient | DosageForm | Route).</item>
    /// <item>Within each group, prefer NDA tier (if any) then pick the DocumentGUID with
    /// the most recent <c>LabelEffectiveDate</c>. Ties break on higher <c>VersionNumber</c>,
    /// then lower <c>DocumentGUID</c> ordinal.</item>
    /// </list>
    ///
    /// ## Database Access
    /// Two queries total, both <c>AsNoTracking</c>. No N+1. Works for the 250K+ label
    /// corpus in a single pass because both queries are keyed on small in-memory sets.
    /// </remarks>
    /// <seealso cref="IBioequivalentLabelDedupService"/>
    /// <seealso cref="ApplicationNumberParser"/>
    public class BioequivalentLabelDedupService : IBioequivalentLabelDedupService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Regex used to collapse runs of whitespace inside Ingredient /
        /// DosageForm / Route strings so "TABLET, FILM COATED" and
        /// "TABLET,  FILM COATED" normalize to the same group key.</summary>
        private static readonly Regex _internalWhitespace =
            new Regex(@"\s+", RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Database context used for the two read-only join queries.</summary>
        private readonly ApplicationDbContext _context;

        /**************************************************************/
        /// <summary>Logger for summary metrics.</summary>
        private readonly ILogger<BioequivalentLabelDedupService> _logger;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the dedup service with the required database context and logger.
        /// </summary>
        /// <param name="context">EF Core context used for <c>vw_ProductsByLabeler</c>
        /// and <see cref="OrangeBook.Product"/> queries.</param>
        /// <param name="logger">Logger for summary diagnostics.</param>
        public BioequivalentLabelDedupService(
            ApplicationDbContext context,
            ILogger<BioequivalentLabelDedupService> logger)
        {
            #region implementation

            _context = context;
            _logger = logger;

            #endregion
        }

        #endregion

        #region IBioequivalentLabelDedupService

        /**************************************************************/
        /// <inheritdoc />
        public async Task<BioequivalentDedupResult> DeduplicateAsync(
            IReadOnlyList<Guid> orderedDocumentGuids,
            BioequivalentDedupOptions? options = null,
            CancellationToken ct = default)
        {
            #region implementation

            options ??= new BioequivalentDedupOptions();

            if (orderedDocumentGuids is null || orderedDocumentGuids.Count == 0)
            {
                return emptyResult();
            }

            // Distinct input list guards against accidental duplicates from the caller
            // while preserving the first-seen order. Preserving order is part of the
            // public contract — ML anomaly-model training locality depends on it.
            var inputDistinct = orderedDocumentGuids
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();

            // Stage 1: resolve DocumentGUID → ApplicationNumber from vw_ProductsByLabeler.
            // One document maps to many products (strength variants), so project distinct
            // (DocumentGUID, ApplicationNumber, LabelEffectiveDate, VersionNumber) combinations.
            var labelerRows = await loadLabelerRowsAsync(inputDistinct, ct);

            // Stage 2: parse the application-number strings into Orange Book key pairs.
            var classified = classifyApplicationNumbers(labelerRows);

            // Stage 3: look up Orange Book metadata for the (ApplType, ApplNo) candidate set.
            var obLookup = await loadOrangeBookLookupAsync(classified, ct);

            // Stage 4: merge Orange Book data into each candidate and compute group keys.
            // A DocumentGUID may have multiple ApplicationNumbers (rare — near-duplicate
            // labels that reference two applications). We attach every viable candidate
            // record and let the ranking step pick the canonical one.
            var candidates = buildCandidates(classified, obLookup);

            var dropped = new List<DroppedDocument>();
            var keptSet = new HashSet<Guid>();

            // Unclassifiable: no candidate row at all, OR no row with an Orange Book match.
            var classifiableDocs = candidates
                .GroupBy(c => c.DocumentGuid)
                .Where(g => g.Any(c => c.HasOrangeBookMatch))
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var guid in inputDistinct)
            {
                if (classifiableDocs.Contains(guid))
                {
                    continue;
                }

                if (options.DropUnclassifiable)
                {
                    // Distinguish the three unclassifiable flavors for diagnostics.
                    string reason;
                    if (!labelerRows.ContainsKey(guid))
                    {
                        reason = "unclassifiable:no_application_number";
                    }
                    else if (!labelerRows[guid].Any(r => !string.IsNullOrWhiteSpace(r.ApplicationNumber)))
                    {
                        reason = "unclassifiable:no_application_number";
                    }
                    else if (!candidates.Where(c => c.DocumentGuid == guid).Any(c => c.ApplTypeParsed))
                    {
                        reason = "unclassifiable:unrecognized_prefix";
                    }
                    else
                    {
                        reason = "unclassifiable:no_orange_book_match";
                    }
                    dropped.Add(new DroppedDocument(guid, reason, GroupKey: null));
                }
                else
                {
                    keptSet.Add(guid);
                }
            }

            // Group the classifiable candidates by (Ingredient|DosageForm|Route).
            var classifiableCandidates = candidates.Where(c => c.HasOrangeBookMatch).ToList();

            // Guards against the degenerate "single document spans two OB groups" case:
            // attach the document to every group it has coverage for, then pick one
            // canonical per group. The union of canonicals is kept.
            var groupSelections = classifiableCandidates
                .GroupBy(c => c.GroupKey!)
                .ToList();

            var ndaGroupCount = 0;
            var andaGroupCount = 0;
            var canonicalByGroup = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var group in groupSelections)
            {
                // Partition into NDA and ANDA tiers. Candidates with unknown ApplType
                // are dropped at classification time; everything here is "N" or "A".
                var ndaTier = group.Where(c => c.IsNda).ToList();
                var andaTier = group.Where(c => c.IsAnda).ToList();

                // Choose which tier competes for the canonical slot.
                //  - PreferNdaOverAnda=true + any NDA → NDA tier only (skip ANDAs entirely).
                //  - Otherwise → rank all candidates together (so the winner is just
                //    the most-recent label regardless of application type).
                List<Candidate> chosenTier;
                if (options.PreferNdaOverAnda && ndaTier.Count > 0)
                {
                    chosenTier = ndaTier;
                }
                else
                {
                    chosenTier = group.ToList();
                }

                // Pick one canonical DocumentGUID using the MostRecentLabel strategy.
                // Order comparer: LabelEffectiveDate desc, VersionNumber desc,
                // DocumentGUID asc (ordinal). Deterministic under ties.
                var canonical = chosenTier
                    .OrderByDescending(c => c.LabelEffectiveDate ?? DateTime.MinValue)
                    .ThenByDescending(c => c.VersionNumber ?? 0)
                    .ThenBy(c => c.DocumentGuid)
                    .First();

                canonicalByGroup[group.Key] = canonical.DocumentGuid;
                keptSet.Add(canonical.DocumentGuid);

                // Metrics reflect where the winner came from, not just tier presence.
                if (canonical.IsNda) ndaGroupCount++;
                else andaGroupCount++;
            }

            // Any classifiable doc that did NOT win its group is a bioequivalent duplicate.
            foreach (var group in groupSelections)
            {
                var winner = canonicalByGroup[group.Key];
                foreach (var doc in group.Select(c => c.DocumentGuid).Distinct())
                {
                    if (doc != winner && !keptSet.Contains(doc))
                    {
                        dropped.Add(new DroppedDocument(
                            doc,
                            $"bioequivalent_duplicate:{group.Key}",
                            group.Key));
                    }
                }
            }

            // Preserve input order in the final KeptDocumentGuids list.
            var kept = inputDistinct.Where(keptSet.Contains).ToList();

            var unclassifiableCount = dropped.Count(d => d.Reason.StartsWith("unclassifiable", StringComparison.Ordinal));

            _logger.LogInformation(
                "Bioequivalent dedup: {Kept}/{Input} docs kept — {Groups} groups ({Nda} NDA-led, {Anda} ANDA-led), {Unclass} unclassifiable",
                kept.Count, inputDistinct.Count, groupSelections.Count, ndaGroupCount, andaGroupCount, unclassifiableCount);

            return new BioequivalentDedupResult
            {
                KeptDocumentGuids = kept,
                DroppedDocuments = dropped,
                GroupCount = groupSelections.Count,
                NdaGroupCount = ndaGroupCount,
                AndaGroupCount = andaGroupCount,
                UnclassifiableCount = unclassifiableCount
            };

            #endregion
        }

        #endregion

        #region private helpers

        /**************************************************************/
        /// <summary>
        /// Queries <c>vw_ProductsByLabeler</c> for the given DocumentGUIDs and returns
        /// a dictionary keyed by DocumentGUID. Collapses duplicate (DocumentGUID,
        /// ApplicationNumber) pairs that arise when a label covers multiple products.
        /// </summary>
        private async Task<Dictionary<Guid, List<LabelerRow>>> loadLabelerRowsAsync(
            IReadOnlyList<Guid> guids,
            CancellationToken ct)
        {
            #region implementation

            var rows = await _context.Set<LabelView.ProductsByLabeler>()
                .AsNoTracking()
                .Where(p => p.DocumentGUID != null && guids.Contains(p.DocumentGUID.Value))
                .Select(p => new LabelerRow
                {
                    DocumentGuid = p.DocumentGUID!.Value,
                    ApplicationNumber = p.ApplicationNumber,
                    LabelEffectiveDate = p.LabelEffectiveDate,
                    VersionNumber = p.VersionNumber
                })
                .ToListAsync(ct);

            // De-duplicate at (DocumentGUID, ApplicationNumber) level — one row per
            // application per document is sufficient for the downstream grouping.
            var byGuid = rows
                .GroupBy(r => r.DocumentGuid)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(r => (r.ApplicationNumber ?? string.Empty).Trim().ToUpperInvariant())
                          .Select(sub => sub.OrderByDescending(r => r.LabelEffectiveDate ?? DateTime.MinValue)
                                            .ThenByDescending(r => r.VersionNumber ?? 0)
                                            .First())
                          .ToList());

            return byGuid;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Walks the labeler rows, parses each ApplicationNumber, and returns a list
        /// of <see cref="ClassifiedRow"/> records. Rows whose ApplicationNumber fails
        /// to parse (null, blank, or non-NDA/ANDA) still flow through with
        /// <see cref="ClassifiedRow.ApplTypeParsed"/> = false so the caller can tell
        /// the difference between "no prefix" and "no Orange Book match".
        /// </summary>
        private static List<ClassifiedRow> classifyApplicationNumbers(
            Dictionary<Guid, List<LabelerRow>> labelerRows)
        {
            #region implementation

            var result = new List<ClassifiedRow>();
            foreach (var (guid, rows) in labelerRows)
            {
                foreach (var row in rows)
                {
                    var parsed = ApplicationNumberParser.TryParse(row.ApplicationNumber, out var applType, out var applNo);
                    result.Add(new ClassifiedRow
                    {
                        DocumentGuid = guid,
                        ApplicationNumber = row.ApplicationNumber,
                        LabelEffectiveDate = row.LabelEffectiveDate,
                        VersionNumber = row.VersionNumber,
                        ApplTypeParsed = parsed,
                        ApplType = parsed ? applType : null,
                        ApplNo = parsed ? applNo : null
                    });
                }
            }
            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads <see cref="OrangeBook.Product"/> rows for the distinct (ApplType, ApplNo)
        /// pairs in <paramref name="classified"/> and returns a lookup keyed on those pairs.
        /// The value is the first matching product (picking the highest ApprovalDate
        /// to resolve ambiguity when multiple product-number rows share a key —
        /// typically different strengths which all share Ingredient/DosageForm/Route).
        /// </summary>
        private async Task<Dictionary<(string Type, string No), OrangeBookInfo>> loadOrangeBookLookupAsync(
            List<ClassifiedRow> classified,
            CancellationToken ct)
        {
            #region implementation

            var keys = classified
                .Where(c => c.ApplTypeParsed)
                .Select(c => (Type: c.ApplType!, No: c.ApplNo!))
                .Distinct()
                .ToList();

            if (keys.Count == 0)
            {
                return new Dictionary<(string, string), OrangeBookInfo>();
            }

            // Translate the pair-list into parallel arrays because EF Core's
            // .Contains() translation works best on single-column lookups.
            var applTypes = keys.Select(k => k.Type).Distinct().ToList();
            var applNos = keys.Select(k => k.No).Distinct().ToList();

            // Over-fetch: filter by the union of types and numbers, then reduce in memory.
            // The per-key set is small (hundreds, not millions) so this is cheap.
            var rawRows = await _context.Set<OrangeBook.Product>()
                .AsNoTracking()
                .Where(p => p.ApplType != null && p.ApplNo != null
                    && applTypes.Contains(p.ApplType)
                    && applNos.Contains(p.ApplNo))
                .Select(p => new { p.ApplType, p.ApplNo, p.Ingredient, p.DosageForm, p.Route, p.ApprovalDate, p.IsRLD })
                .ToListAsync(ct);

            var keyedSet = new HashSet<(string, string)>(keys);

            var lookup = rawRows
                .Where(r => r.ApplType != null && r.ApplNo != null
                    && keyedSet.Contains((r.ApplType!, r.ApplNo!)))
                .GroupBy(r => (r.ApplType!, r.ApplNo!))
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Pick one representative metadata record per application.
                        // An application covers N products (strengths); they share
                        // Ingredient/DosageForm/Route, so any row is fine — we pick
                        // deterministically by (IsRLD desc, earliest ApprovalDate).
                        var rep = g
                            .OrderByDescending(r => r.IsRLD ?? false)
                            .ThenBy(r => r.ApprovalDate ?? DateTime.MaxValue)
                            .First();
                        return new OrangeBookInfo
                        {
                            Ingredient = rep.Ingredient,
                            DosageForm = rep.DosageForm,
                            Route = rep.Route
                        };
                    });

            return lookup;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Joins <paramref name="classified"/> with <paramref name="obLookup"/> to produce
        /// candidate records that carry both the selection signals (LabelEffectiveDate,
        /// VersionNumber) and the grouping signals (Ingredient, DosageForm, Route →
        /// GroupKey). Unmatched classified rows (no Orange Book entry) become
        /// candidates with <c>HasOrangeBookMatch = false</c> so the caller can report
        /// them as <c>unclassifiable:no_orange_book_match</c>.
        /// </summary>
        private static List<Candidate> buildCandidates(
            List<ClassifiedRow> classified,
            Dictionary<(string, string), OrangeBookInfo> obLookup)
        {
            #region implementation

            var result = new List<Candidate>(classified.Count);
            foreach (var row in classified)
            {
                if (!row.ApplTypeParsed)
                {
                    result.Add(new Candidate
                    {
                        DocumentGuid = row.DocumentGuid,
                        ApplTypeParsed = false,
                        LabelEffectiveDate = row.LabelEffectiveDate,
                        VersionNumber = row.VersionNumber
                    });
                    continue;
                }

                if (!obLookup.TryGetValue((row.ApplType!, row.ApplNo!), out var info))
                {
                    result.Add(new Candidate
                    {
                        DocumentGuid = row.DocumentGuid,
                        ApplTypeParsed = true,
                        ApplType = row.ApplType,
                        ApplNo = row.ApplNo,
                        LabelEffectiveDate = row.LabelEffectiveDate,
                        VersionNumber = row.VersionNumber,
                        HasOrangeBookMatch = false
                    });
                    continue;
                }

                result.Add(new Candidate
                {
                    DocumentGuid = row.DocumentGuid,
                    ApplTypeParsed = true,
                    ApplType = row.ApplType,
                    ApplNo = row.ApplNo,
                    LabelEffectiveDate = row.LabelEffectiveDate,
                    VersionNumber = row.VersionNumber,
                    HasOrangeBookMatch = true,
                    GroupKey = buildGroupKey(info)
                });
            }
            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the normalized group key for a single Orange Book product:
        /// <c>"{INGREDIENT}|{DOSAGE_FORM}|{ROUTE}"</c>. Whitespace is collapsed and
        /// case is upper-invariant so trivial formatting differences do not split groups.
        /// </summary>
        private static string buildGroupKey(OrangeBookInfo info)
        {
            #region implementation

            return $"{normalize(info.Ingredient)}|{normalize(info.DosageForm)}|{normalize(info.Route)}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes an Orange Book text column: trim, upper-invariant, collapse
        /// runs of whitespace to a single space. Null becomes the empty string.
        /// </summary>
        private static string normalize(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            var upper = value.Trim().ToUpperInvariant();
            return _internalWhitespace.Replace(upper, " ");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Empty-input short-circuit with a fully-populated result object so callers
        /// never need to null-check.
        /// </summary>
        private static BioequivalentDedupResult emptyResult()
        {
            #region implementation

            return new BioequivalentDedupResult
            {
                KeptDocumentGuids = Array.Empty<Guid>(),
                DroppedDocuments = Array.Empty<DroppedDocument>(),
                GroupCount = 0,
                NdaGroupCount = 0,
                AndaGroupCount = 0,
                UnclassifiableCount = 0
            };

            #endregion
        }

        #endregion

        #region private types

        /// <summary>Row projected from vw_ProductsByLabeler for a single document.</summary>
        private sealed class LabelerRow
        {
            public Guid DocumentGuid { get; init; }
            public string? ApplicationNumber { get; init; }
            public DateTime? LabelEffectiveDate { get; init; }
            public int? VersionNumber { get; init; }
        }

        /// <summary>Labeler row after ApplicationNumber parsing.</summary>
        private sealed class ClassifiedRow
        {
            public Guid DocumentGuid { get; init; }
            public string? ApplicationNumber { get; init; }
            public DateTime? LabelEffectiveDate { get; init; }
            public int? VersionNumber { get; init; }
            public bool ApplTypeParsed { get; init; }
            public string? ApplType { get; init; }
            public string? ApplNo { get; init; }
        }

        /// <summary>Orange Book metadata used for group-key construction.</summary>
        private sealed class OrangeBookInfo
        {
            public string? Ingredient { get; init; }
            public string? DosageForm { get; init; }
            public string? Route { get; init; }
        }

        /// <summary>
        /// Fully merged candidate record used during tier selection.
        /// </summary>
        private sealed class Candidate
        {
            public Guid DocumentGuid { get; init; }
            public bool ApplTypeParsed { get; init; }
            public string? ApplType { get; init; }
            public string? ApplNo { get; init; }
            public DateTime? LabelEffectiveDate { get; init; }
            public int? VersionNumber { get; init; }
            public bool HasOrangeBookMatch { get; init; }
            public string? GroupKey { get; init; }

            public bool IsNda => string.Equals(ApplType, ApplicationNumberParser.ApplTypeNda, StringComparison.Ordinal);
            public bool IsAnda => string.Equals(ApplType, ApplicationNumberParser.ApplTypeAnda, StringComparison.Ordinal);
        }

        #endregion
    }
}
