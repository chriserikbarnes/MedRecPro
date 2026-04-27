using MedRecProImportClass.Models;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Reflection-free, name-based accessor for the 15 observation-context columns on
    /// <see cref="ParsedObservation"/>. Single source of truth replacing three near-duplicate
    /// switch helpers that previously lived in <c>ColumnStandardizationService</c> and
    /// <c>RowValidationService</c>.
    /// </summary>
    /// <remarks>
    /// ## Supported Columns (case-sensitive, exact-name match)
    /// <list type="bullet">
    ///   <item><description>String columns (11): ParameterName, ParameterCategory, ParameterSubtype,
    ///   TreatmentArm, StudyContext, DoseRegimen, DoseUnit, Population, Timepoint, TimeUnit,
    ///   PrimaryValueType, Unit</description></item>
    ///   <item><description>Numeric nullables (3): ArmN (int?), Dose (decimal?), Time (double?)</description></item>
    /// </list>
    ///
    /// Unknown column names: <see cref="Get"/> and <see cref="GetAsString"/> return <c>null</c>;
    /// <see cref="Set"/> is a no-op. This matches the prior fail-quiet behavior of the helpers
    /// being replaced — callers are expected to use compile-time known column names.
    ///
    /// ## Why Not Reflection
    /// All three predecessor helpers used hand-written <c>switch</c> statements for performance —
    /// the standardization phase iterates the full contract for every observation. Reflection
    /// would add ~10x overhead for no readability benefit since the column set is fixed.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ValidationFlagExtensions"/>
    public static class ParsedObservationFieldAccess
    {
        /**************************************************************/
        /// <summary>
        /// Returns the typed boxed value of a column. Preserves <see cref="int"/>/<see cref="decimal"/>/<see cref="double"/>
        /// boxing for ArmN/Dose/Time so callers that need the original type can cast back.
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name (case-sensitive).</param>
        /// <returns>Boxed column value, or <c>null</c> for unset values and unknown column names.</returns>
        /// <example>
        /// <code>
        /// var value = ParsedObservationFieldAccess.Get(obs, "ArmN");      // returns boxed int? or null
        /// var name  = ParsedObservationFieldAccess.Get(obs, "ParameterName"); // returns string? or null
        /// </code>
        /// </example>
        public static object? Get(ParsedObservation obs, string column)
        {
            #region implementation

            return column switch
            {
                "ParameterName" => obs.ParameterName,
                "ParameterCategory" => obs.ParameterCategory,
                "ParameterSubtype" => obs.ParameterSubtype,
                "TreatmentArm" => obs.TreatmentArm,
                "ArmN" => obs.ArmN,
                "StudyContext" => obs.StudyContext,
                "DoseRegimen" => obs.DoseRegimen,
                "Dose" => obs.Dose,
                "DoseUnit" => obs.DoseUnit,
                "Population" => obs.Population,
                "Timepoint" => obs.Timepoint,
                "Time" => obs.Time,
                "TimeUnit" => obs.TimeUnit,
                "PrimaryValueType" => obs.PrimaryValueType,
                "Unit" => obs.Unit,
                _ => null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the column value rendered as a string. Numeric nullables (ArmN, Dose, Time)
        /// are formatted via <c>ToString()</c>. Returns <c>null</c> for unset values and unknown
        /// column names.
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name (case-sensitive).</param>
        /// <returns>String view of the column value, or <c>null</c> when unset.</returns>
        /// <example>
        /// <code>
        /// // For obs.ArmN = 100:
        /// ParsedObservationFieldAccess.GetAsString(obs, "ArmN"); // returns "100"
        /// </code>
        /// </example>
        public static string? GetAsString(ParsedObservation obs, string column)
        {
            #region implementation

            var value = Get(obs, column);
            return value switch
            {
                null => null,
                string s => s,
                int i => i.ToString(),
                decimal d => d.ToString(),
                double dbl => dbl.ToString(),
                _ => value.ToString()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets a column to the given value. Casts via <c>as</c> for nullable types — passing
        /// the wrong runtime type results in <c>null</c> being assigned (preserves the existing
        /// fail-quiet behavior of the predecessor helper). No-op for unknown column names.
        /// </summary>
        /// <param name="obs">Observation to mutate.</param>
        /// <param name="column">Column name (case-sensitive).</param>
        /// <param name="value">Value to assign — must match the column's underlying CLR type
        /// (<see cref="string"/>, <see cref="int"/>?, <see cref="decimal"/>?, or <see cref="double"/>?).
        /// Mismatched types silently become <c>null</c>.</param>
        /// <example>
        /// <code>
        /// ParsedObservationFieldAccess.Set(obs, "Timepoint", null);  // clears the field
        /// ParsedObservationFieldAccess.Set(obs, "ArmN", 100);        // assigns int? = 100
        /// </code>
        /// </example>
        public static void Set(ParsedObservation obs, string column, object? value)
        {
            #region implementation

            switch (column)
            {
                case "ParameterName": obs.ParameterName = value as string; break;
                case "ParameterCategory": obs.ParameterCategory = value as string; break;
                case "ParameterSubtype": obs.ParameterSubtype = value as string; break;
                case "TreatmentArm": obs.TreatmentArm = value as string; break;
                case "ArmN": obs.ArmN = value as int?; break;
                case "StudyContext": obs.StudyContext = value as string; break;
                case "DoseRegimen": obs.DoseRegimen = value as string; break;
                case "Dose": obs.Dose = value as decimal?; break;
                case "DoseUnit": obs.DoseUnit = value as string; break;
                case "Population": obs.Population = value as string; break;
                case "Timepoint": obs.Timepoint = value as string; break;
                case "Time": obs.Time = value as double?; break;
                case "TimeUnit": obs.TimeUnit = value as string; break;
                case "PrimaryValueType": obs.PrimaryValueType = value as string; break;
                case "Unit": obs.Unit = value as string; break;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// True when the column has a non-null, non-whitespace value. For string columns,
        /// matches <see cref="string.IsNullOrWhiteSpace"/>; for numeric columns, returns
        /// <c>true</c> for any populated value (including zero).
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name (case-sensitive).</param>
        /// <returns><c>true</c> when populated; <c>false</c> for unset, whitespace-only strings, or unknown columns.</returns>
        public static bool IsPopulated(ParsedObservation obs, string column)
        {
            #region implementation

            var value = Get(obs, column);
            return value switch
            {
                null => false,
                string s => !string.IsNullOrWhiteSpace(s),
                _ => true
            };

            #endregion
        }
    }
}
