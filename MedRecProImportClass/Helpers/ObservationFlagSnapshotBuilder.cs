using MedRecProImportClass.Models;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Identifies a single observation inside a source table/cell group for
    /// pre/post correction flag comparison.
    /// </summary>
    /// <remarks>
    /// Multiple observations may legitimately originate from the same source
    /// table cell. The zero-based <see cref="OccurrenceIndex"/> preserves the
    /// emitted observation order inside each
    /// <c>TextTableID + SourceRowSeq + SourceCellSeq</c> group so reporting
    /// snapshots never collapse distinct observations into one dictionary key.
    /// </remarks>
    /// <param name="TextTableID">Source table identifier for the observation.</param>
    /// <param name="SourceRowSeq">Source row sequence for the observation.</param>
    /// <param name="SourceCellSeq">Source cell sequence for the observation.</param>
    /// <param name="OccurrenceIndex">Zero-based occurrence within the same source table/row/cell group.</param>
    /// <example>
    /// <code>
    /// var key = new ObservationFlagKey(16359, 3, 4, 1);
    /// </code>
    /// </example>
    /// <seealso cref="ParsedObservation"/>
    public readonly record struct ObservationFlagKey(
        int? TextTableID,
        int? SourceRowSeq,
        int? SourceCellSeq,
        int OccurrenceIndex);

    /**************************************************************/
    /// <summary>
    /// Builds and resolves immutable <see cref="ParsedObservation.ValidationFlags"/>
    /// snapshots for Claude-correction report diffs.
    /// </summary>
    /// <remarks>
    /// The reporting harness uses these snapshots to compare the state immediately
    /// before Stage 3.5 with the post-Claude state. Keys include occurrence order
    /// so duplicate source cells such as <c>SourceRowSeq=3</c> and
    /// <c>SourceCellSeq=4</c> can be represented without dictionary collisions.
    /// </remarks>
    /// <seealso cref="ObservationFlagKey"/>
    /// <seealso cref="ParsedObservation"/>
    public static class ObservationFlagSnapshotBuilder
    {
        /**************************************************************/
        /// <summary>
        /// Captures each observation's current validation flags in observation order.
        /// </summary>
        /// <remarks>
        /// The returned dictionary is independent of the observation objects; later
        /// mutations to <see cref="ParsedObservation.ValidationFlags"/> do not change
        /// the captured string values.
        /// </remarks>
        /// <param name="observations">Observations to snapshot.</param>
        /// <returns>
        /// A dictionary keyed by source table/row/cell plus occurrence index, with
        /// values set to each observation's current validation flags.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="observations"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// var snapshot = ObservationFlagSnapshotBuilder.Capture(observations);
        /// </code>
        /// </example>
        /// <seealso cref="ResolveInObservationOrder"/>
        public static Dictionary<ObservationFlagKey, string?> Capture(
            IReadOnlyList<ParsedObservation> observations)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(observations);

            var snapshot = new Dictionary<ObservationFlagKey, string?>();
            var occurrenceCounts = new Dictionary<ObservationSourceKey, int>();

            foreach (var observation in observations)
            {
                var sourceKey = ObservationSourceKey.From(observation);
                var occurrenceIndex = occurrenceCounts.TryGetValue(sourceKey, out var count)
                    ? count
                    : 0;

                occurrenceCounts[sourceKey] = occurrenceIndex + 1;

                snapshot[new ObservationFlagKey(
                    sourceKey.TextTableID,
                    sourceKey.SourceRowSeq,
                    sourceKey.SourceCellSeq,
                    occurrenceIndex)] = observation.ValidationFlags;
            }

            return snapshot;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a snapshot back into the order of a current observation list.
        /// </summary>
        /// <remarks>
        /// This method rebuilds occurrence indexes from <paramref name="observations"/>
        /// using the same ordering rules as <see cref="Capture"/>. The result aligns
        /// index-for-index with the supplied observation list, which lets writers
        /// compare post-Claude observations without relying on non-unique row/cell keys.
        /// </remarks>
        /// <param name="observations">Current observations to align with the snapshot.</param>
        /// <param name="snapshot">Snapshot produced by <see cref="Capture"/>, or null when Claude was skipped.</param>
        /// <returns>
        /// A list with one entry per observation. Each value is the matching pre-Claude
        /// validation flags string, or null when no snapshot value exists.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="observations"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// var beforeFlags = ObservationFlagSnapshotBuilder.ResolveInObservationOrder(
        ///     observations,
        ///     snapshot);
        /// </code>
        /// </example>
        /// <seealso cref="Capture"/>
        public static IReadOnlyList<string?> ResolveInObservationOrder(
            IReadOnlyList<ParsedObservation> observations,
            IReadOnlyDictionary<ObservationFlagKey, string?>? snapshot)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(observations);

            var resolved = new List<string?>(observations.Count);
            var occurrenceCounts = new Dictionary<ObservationSourceKey, int>();

            foreach (var observation in observations)
            {
                var sourceKey = ObservationSourceKey.From(observation);
                var occurrenceIndex = occurrenceCounts.TryGetValue(sourceKey, out var count)
                    ? count
                    : 0;

                occurrenceCounts[sourceKey] = occurrenceIndex + 1;

                var flagKey = new ObservationFlagKey(
                    sourceKey.TextTableID,
                    sourceKey.SourceRowSeq,
                    sourceKey.SourceCellSeq,
                    occurrenceIndex);

                resolved.Add(snapshot != null && snapshot.TryGetValue(flagKey, out var beforeFlags)
                    ? beforeFlags
                    : null);
            }

            return resolved;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Internal source-cell grouping key used before occurrence indexes are assigned.
        /// </summary>
        /// <seealso cref="ObservationFlagKey"/>
        private readonly record struct ObservationSourceKey(
            int? TextTableID,
            int? SourceRowSeq,
            int? SourceCellSeq)
        {
            /**************************************************************/
            /// <summary>
            /// Creates a source grouping key from a parsed observation.
            /// </summary>
            /// <param name="observation">Observation whose source coordinates should be used.</param>
            /// <returns>A source grouping key for occurrence counting.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="observation"/> is null.
            /// </exception>
            /// <seealso cref="ParsedObservation"/>
            public static ObservationSourceKey From(ParsedObservation observation)
            {
                #region implementation

                ArgumentNullException.ThrowIfNull(observation);

                return new ObservationSourceKey(
                    observation.TextTableID,
                    observation.SourceRowSeq,
                    observation.SourceCellSeq);

                #endregion
            }
        }
    }
}
