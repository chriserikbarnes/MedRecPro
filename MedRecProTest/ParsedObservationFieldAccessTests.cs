using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="ParsedObservationFieldAccess"/> — the shared accessor
    /// that replaced three near-duplicate switch helpers across
    /// <c>ColumnStandardizationService</c> and <c>RowValidationService</c>.
    /// </summary>
    /// <remarks>
    /// Every supported column is exercised by <see cref="RoundTrip_AllFifteenColumns_GetReturnsWhatSetWrote"/>;
    /// the typed-boxing tests verify that <see cref="ParsedObservationFieldAccess.Get"/> preserves
    /// <see cref="int"/>/<see cref="decimal"/>/<see cref="double"/> for the three numeric columns.
    /// </remarks>
    /// <seealso cref="ParsedObservationFieldAccess"/>
    /// <seealso cref="ParsedObservation"/>
    [TestClass]
    public class ParsedObservationFieldAccessTests
    {
        #region Get Tests

        /**************************************************************/
        /// <summary>
        /// String columns are returned as <see cref="string"/>.
        /// </summary>
        [TestMethod]
        public void Get_StringField_ReturnsString()
        {
            #region implementation

            var obs = new ParsedObservation { ParameterName = "Cmax" };
            var value = ParsedObservationFieldAccess.Get(obs, "ParameterName");

            Assert.IsInstanceOfType(value, typeof(string));
            Assert.AreEqual("Cmax", value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>ArmN</c> is returned as a boxed <see cref="int"/> (preserves the typed nullable).
        /// </summary>
        [TestMethod]
        public void Get_IntField_ReturnsBoxedInt()
        {
            #region implementation

            var obs = new ParsedObservation { ArmN = 100 };
            var value = ParsedObservationFieldAccess.Get(obs, "ArmN");

            Assert.IsInstanceOfType(value, typeof(int));
            Assert.AreEqual(100, value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>Dose</c> is returned as a boxed <see cref="decimal"/>.
        /// </summary>
        [TestMethod]
        public void Get_DecimalField_ReturnsBoxedDecimal()
        {
            #region implementation

            var obs = new ParsedObservation { Dose = 5.5m };
            var value = ParsedObservationFieldAccess.Get(obs, "Dose");

            Assert.IsInstanceOfType(value, typeof(decimal));
            Assert.AreEqual(5.5m, value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>Time</c> is returned as a boxed <see cref="double"/>.
        /// </summary>
        [TestMethod]
        public void Get_DoubleField_ReturnsBoxedDouble()
        {
            #region implementation

            var obs = new ParsedObservation { Time = 24.0 };
            var value = ParsedObservationFieldAccess.Get(obs, "Time");

            Assert.IsInstanceOfType(value, typeof(double));
            Assert.AreEqual(24.0, value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unset string columns return <c>null</c>.
        /// </summary>
        [TestMethod]
        public void Get_NullField_ReturnsNull()
        {
            #region implementation

            var obs = new ParsedObservation();
            Assert.IsNull(ParsedObservationFieldAccess.Get(obs, "ParameterName"));
            Assert.IsNull(ParsedObservationFieldAccess.Get(obs, "ArmN"));
            Assert.IsNull(ParsedObservationFieldAccess.Get(obs, "Dose"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unknown column names return <c>null</c> rather than throwing.
        /// </summary>
        [TestMethod]
        public void Get_UnknownField_ReturnsNull()
        {
            #region implementation

            var obs = new ParsedObservation { ParameterName = "X" };
            Assert.IsNull(ParsedObservationFieldAccess.Get(obs, "NonExistent"));

            #endregion
        }

        #endregion Get Tests

        #region GetAsString Tests

        /**************************************************************/
        /// <summary>
        /// <see cref="ParsedObservationFieldAccess.GetAsString"/> returns string columns unchanged.
        /// </summary>
        [TestMethod]
        public void GetAsString_StringField_ReturnsString()
        {
            #region implementation

            var obs = new ParsedObservation { TreatmentArm = "Placebo" };
            Assert.AreEqual("Placebo", ParsedObservationFieldAccess.GetAsString(obs, "TreatmentArm"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Numeric columns are formatted via <c>ToString()</c>.
        /// </summary>
        [TestMethod]
        public void GetAsString_IntField_FormatsViaToString()
        {
            #region implementation

            var obs = new ParsedObservation { ArmN = 100 };
            Assert.AreEqual("100", ParsedObservationFieldAccess.GetAsString(obs, "ArmN"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <see cref="ParsedObservationFieldAccess.GetAsString"/> returns <c>null</c> for unset columns.
        /// </summary>
        [TestMethod]
        public void GetAsString_NullField_ReturnsNull()
        {
            #region implementation

            var obs = new ParsedObservation();
            Assert.IsNull(ParsedObservationFieldAccess.GetAsString(obs, "ParameterName"));
            Assert.IsNull(ParsedObservationFieldAccess.GetAsString(obs, "ArmN"));

            #endregion
        }

        #endregion GetAsString Tests

        #region Set Tests

        /**************************************************************/
        /// <summary>
        /// <see cref="ParsedObservationFieldAccess.Set"/> assigns the value to the named string column.
        /// </summary>
        [TestMethod]
        public void Set_StringField_AssignsValue()
        {
            #region implementation

            var obs = new ParsedObservation();
            ParsedObservationFieldAccess.Set(obs, "ParameterName", "AUC");

            Assert.AreEqual("AUC", obs.ParameterName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <see cref="ParsedObservationFieldAccess.Set"/> assigns when the runtime type matches.
        /// </summary>
        [TestMethod]
        public void Set_IntField_AssignsWhenInt()
        {
            #region implementation

            var obs = new ParsedObservation();
            ParsedObservationFieldAccess.Set(obs, "ArmN", 42);

            Assert.AreEqual(42, obs.ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Type-mismatch on a numeric column results in <c>null</c> (matches <c>value as int?</c>
        /// fail-quiet semantics of the predecessor helper).
        /// </summary>
        [TestMethod]
        public void Set_IntField_NullWhenWrongType()
        {
            #region implementation

            var obs = new ParsedObservation { ArmN = 100 };
            ParsedObservationFieldAccess.Set(obs, "ArmN", "not-an-int");

            Assert.IsNull(obs.ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>Dose</c> accepts <see cref="decimal"/> assignment.
        /// </summary>
        [TestMethod]
        public void Set_DecimalField_AssignsValue()
        {
            #region implementation

            var obs = new ParsedObservation();
            ParsedObservationFieldAccess.Set(obs, "Dose", 1.25m);

            Assert.AreEqual(1.25m, obs.Dose);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Setting null clears any existing value (used by Phase 4 NULL enforcement in
        /// <c>ColumnStandardizationService</c>).
        /// </summary>
        [TestMethod]
        public void Set_NullValue_ClearsField()
        {
            #region implementation

            var obs = new ParsedObservation { Timepoint = "Week 12" };
            ParsedObservationFieldAccess.Set(obs, "Timepoint", null);

            Assert.IsNull(obs.Timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Setting an unknown column is a no-op (does not throw).
        /// </summary>
        [TestMethod]
        public void Set_UnknownField_NoThrow()
        {
            #region implementation

            var obs = new ParsedObservation { ParameterName = "X" };
            ParsedObservationFieldAccess.Set(obs, "Bogus", "value");

            Assert.AreEqual("X", obs.ParameterName, "Unknown-column Set should not affect known columns.");

            #endregion
        }

        #endregion Set Tests

        #region IsPopulated Tests

        /**************************************************************/
        /// <summary>
        /// Non-empty string column reports populated.
        /// </summary>
        [TestMethod]
        public void IsPopulated_NonEmptyString_True()
        {
            #region implementation

            var obs = new ParsedObservation { ParameterName = "Cmax" };
            Assert.IsTrue(ParsedObservationFieldAccess.IsPopulated(obs, "ParameterName"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace-only string column reports unpopulated.
        /// </summary>
        [TestMethod]
        public void IsPopulated_WhitespaceString_False()
        {
            #region implementation

            var obs = new ParsedObservation { ParameterName = "   " };
            Assert.IsFalse(ParsedObservationFieldAccess.IsPopulated(obs, "ParameterName"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unset string column reports unpopulated.
        /// </summary>
        [TestMethod]
        public void IsPopulated_NullField_False()
        {
            #region implementation

            var obs = new ParsedObservation();
            Assert.IsFalse(ParsedObservationFieldAccess.IsPopulated(obs, "ParameterName"));
            Assert.IsFalse(ParsedObservationFieldAccess.IsPopulated(obs, "ArmN"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Numeric column with zero value still reports populated (zero is data, not absence).
        /// </summary>
        [TestMethod]
        public void IsPopulated_PopulatedNumeric_True()
        {
            #region implementation

            var obs = new ParsedObservation { ArmN = 0, Dose = 0m, Time = 0.0 };
            Assert.IsTrue(ParsedObservationFieldAccess.IsPopulated(obs, "ArmN"));
            Assert.IsTrue(ParsedObservationFieldAccess.IsPopulated(obs, "Dose"));
            Assert.IsTrue(ParsedObservationFieldAccess.IsPopulated(obs, "Time"));

            #endregion
        }

        #endregion IsPopulated Tests

        #region Round Trip Tests

        /**************************************************************/
        /// <summary>
        /// Sets every supported column via <see cref="ParsedObservationFieldAccess.Set"/> and
        /// reads it back via <see cref="ParsedObservationFieldAccess.Get"/>; values must match.
        /// Provides parameterized coverage of all 15 supported column names.
        /// </summary>
        [TestMethod]
        public void RoundTrip_AllFifteenColumns_GetReturnsWhatSetWrote()
        {
            #region implementation

            var cases = new (string Column, object? Value)[]
            {
                ("ParameterName",     "Cmax"),
                ("ParameterCategory", "Investigations"),
                ("ParameterSubtype",  "Components of endpoint"),
                ("TreatmentArm",      "Placebo"),
                ("ArmN",              188),
                ("StudyContext",      "Treatment"),
                ("DoseRegimen",       "50 mg oral"),
                ("Dose",              50.0m),
                ("DoseUnit",          "mg"),
                ("Population",        "Adult Healthy Volunteers"),
                ("Timepoint",         "Week 12"),
                ("Time",              12.0),
                ("TimeUnit",          "weeks"),
                ("PrimaryValueType",  "Mean"),
                ("Unit",              "mcg/mL"),
            };

            foreach (var (column, value) in cases)
            {
                var obs = new ParsedObservation();
                ParsedObservationFieldAccess.Set(obs, column, value);
                var got = ParsedObservationFieldAccess.Get(obs, column);
                Assert.AreEqual(value, got, $"Round-trip failed for column '{column}'.");
            }

            #endregion
        }

        #endregion Round Trip Tests
    }
}
