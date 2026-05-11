using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="ObservationFlagSnapshotBuilder"/>.
    /// </summary>
    /// <remarks>
    /// These tests guard the public snapshot contract used by Claude correction reporting.
    /// The key behavior is occurrence-aware resolution for observations that share the same
    /// source table, row, and cell coordinates.
    /// </remarks>
    /// <seealso cref="ObservationFlagSnapshotBuilder"/>
    /// <seealso cref="ObservationFlagKey"/>
    [TestClass]
    public class ObservationFlagSnapshotBuilderTests
    {
        /**************************************************************/
        /// <summary>
        /// Builds a <see cref="ParsedObservation"/> with only the fields needed by snapshot
        /// capture and resolution tests.
        /// </summary>
        /// <param name="textTableId">The source table identifier.</param>
        /// <param name="rowSeq">The source row sequence.</param>
        /// <param name="cellSeq">The source cell sequence.</param>
        /// <param name="flags">The validation flags to snapshot.</param>
        /// <returns>A minimal parsed observation.</returns>
        private static ParsedObservation buildObservation(
            int? textTableId = 16359,
            int? rowSeq = 3,
            int? cellSeq = 4,
            string? flags = null)
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = textTableId,
                SourceRowSeq = rowSeq,
                SourceCellSeq = cellSeq,
                ValidationFlags = flags
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unique source coordinates receive occurrence index zero.
        /// </summary>
        [TestMethod]
        public void Capture_UniqueObservations_UsesOccurrenceZero()
        {
            #region implementation

            var observations = new[]
            {
                buildObservation(textTableId: 101, rowSeq: 1, cellSeq: 1, flags: "FIRST"),
                buildObservation(textTableId: 101, rowSeq: 1, cellSeq: 2, flags: "SECOND")
            };

            var snapshot = ObservationFlagSnapshotBuilder.Capture(observations);

            Assert.AreEqual("FIRST", snapshot[new ObservationFlagKey(101, 1, 1, 0)]);
            Assert.AreEqual("SECOND", snapshot[new ObservationFlagKey(101, 1, 2, 0)]);
            Assert.IsFalse(snapshot.ContainsKey(new ObservationFlagKey(101, 1, 1, 1)));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Duplicate source coordinates receive deterministic occurrence indexes instead of
        /// colliding on the old row/cell-only scalar key.
        /// </summary>
        [TestMethod]
        public void Capture_DuplicateSourceCells_IncrementsOccurrenceIndex()
        {
            #region implementation

            var observations = new[]
            {
                buildObservation(textTableId: 16359, rowSeq: 3, cellSeq: 4, flags: "BEFORE_FIRST"),
                buildObservation(textTableId: 16359, rowSeq: 3, cellSeq: 4, flags: "BEFORE_SECOND")
            };

            var snapshot = ObservationFlagSnapshotBuilder.Capture(observations);

            Assert.AreEqual("BEFORE_FIRST", snapshot[new ObservationFlagKey(16359, 3, 4, 0)]);
            Assert.AreEqual("BEFORE_SECOND", snapshot[new ObservationFlagKey(16359, 3, 4, 1)]);
            Assert.AreEqual(2, snapshot.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null table, row, and cell coordinates remain valid key components and still receive
        /// deterministic occurrence indexes.
        /// </summary>
        [TestMethod]
        public void Capture_NullSourceCoordinates_DeterministicOccurrenceIndexes()
        {
            #region implementation

            var observations = new[]
            {
                buildObservation(textTableId: null, rowSeq: null, cellSeq: null, flags: "NULL_FIRST"),
                buildObservation(textTableId: null, rowSeq: null, cellSeq: null, flags: "NULL_SECOND")
            };

            var snapshot = ObservationFlagSnapshotBuilder.Capture(observations);

            Assert.AreEqual("NULL_FIRST", snapshot[new ObservationFlagKey(null, null, null, 0)]);
            Assert.AreEqual("NULL_SECOND", snapshot[new ObservationFlagKey(null, null, null, 1)]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captured values remain immutable string snapshots even when the source observations
        /// are mutated after capture by later pipeline stages.
        /// </summary>
        [TestMethod]
        public void Capture_LaterObservationMutation_DoesNotChangeSnapshot()
        {
            #region implementation

            var observation = buildObservation(flags: "BEFORE");
            var snapshot = ObservationFlagSnapshotBuilder.Capture(new[] { observation });

            observation.ValidationFlags = "AFTER";

            Assert.AreEqual("BEFORE", snapshot[new ObservationFlagKey(16359, 3, 4, 0)]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Duplicate source cells resolve in current observation order using the same occurrence
        /// assignment used during capture.
        /// </summary>
        [TestMethod]
        public void ResolveInObservationOrder_DuplicateSourceCells_ResolvesInOrder()
        {
            #region implementation

            var beforeObservations = new[]
            {
                buildObservation(flags: "BEFORE_FIRST"),
                buildObservation(flags: "BEFORE_SECOND")
            };
            var snapshot = ObservationFlagSnapshotBuilder.Capture(beforeObservations);
            var afterObservations = new[]
            {
                buildObservation(flags: "AFTER_FIRST"),
                buildObservation(flags: "AFTER_SECOND")
            };

            var resolved = ObservationFlagSnapshotBuilder.ResolveInObservationOrder(
                afterObservations,
                snapshot);

            Assert.AreEqual(2, resolved.Count);
            Assert.AreEqual("BEFORE_FIRST", resolved[0]);
            Assert.AreEqual("BEFORE_SECOND", resolved[1]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Missing snapshot entries resolve to null so writers can keep producing output when
        /// a table has no matching pre-Claude value.
        /// </summary>
        [TestMethod]
        public void ResolveInObservationOrder_MissingSnapshotEntry_ReturnsNull()
        {
            #region implementation

            var observations = new[]
            {
                buildObservation(textTableId: 200, rowSeq: 1, cellSeq: 1, flags: "AFTER")
            };
            var snapshot = new Dictionary<ObservationFlagKey, string?>
            {
                [new ObservationFlagKey(999, 9, 9, 0)] = "UNRELATED"
            };

            var resolved = ObservationFlagSnapshotBuilder.ResolveInObservationOrder(
                observations,
                snapshot);

            Assert.AreEqual(1, resolved.Count);
            Assert.IsNull(resolved[0]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// A null snapshot represents a no-Claude or skipped-Claude run and resolves to one
        /// null before-flag value for every observation.
        /// </summary>
        [TestMethod]
        public void ResolveInObservationOrder_NullSnapshot_ReturnsAllNulls()
        {
            #region implementation

            var observations = new[]
            {
                buildObservation(flags: "AFTER_FIRST"),
                buildObservation(rowSeq: 4, cellSeq: 2, flags: "AFTER_SECOND")
            };

            var resolved = ObservationFlagSnapshotBuilder.ResolveInObservationOrder(
                observations,
                snapshot: null);

            Assert.AreEqual(2, resolved.Count);
            Assert.IsTrue(resolved.All(value => value == null));

            #endregion
        }
    }
}
