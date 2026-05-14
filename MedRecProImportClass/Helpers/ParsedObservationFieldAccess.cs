using System.Globalization;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Reflection-free, name-based accessor for the core parsed-observation columns on
    /// <see cref="ParsedObservation"/>.
    /// </summary>
    /// <remarks>
    /// ## Supported Columns
    /// Column lookups are case-insensitive and normalize to canonical
    /// <see cref="ParsedObservation"/> property names before reading or writing.
    ///
    /// String columns: ParameterName, ParameterCategory, ParameterSubtype, TreatmentArm,
    /// StudyContext, DoseRegimen, DoseUnit, Population, Subpopulation, Timepoint,
    /// TimeUnit, PrimaryValueType, SecondaryValueType, BoundType, Unit.
    ///
    /// Numeric nullable columns: ArmN, Dose, Time, PrimaryValue, SecondaryValue,
    /// LowerBound, UpperBound.
    ///
    /// Unknown column names: <see cref="Get"/> and <see cref="GetAsString"/> return
    /// <c>null</c>; <see cref="Set"/> is a no-op; <see cref="SetFromString"/> returns
    /// <c>false</c>. This matches the prior fail-quiet behavior of the helpers being
    /// replaced.
    ///
    /// ## Why Not Reflection
    /// Standardization iterates column contracts for every observation. The fixed switch
    /// shape keeps this path cheap while still making the column list a single source of
    /// truth for standardization and Claude correction.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ValidationFlagExtensions"/>
    public static class ParsedObservationFieldAccess
    {
        #region Field Metadata

        /**************************************************************/
        /// <summary>
        /// Canonical column names accepted by the accessor.
        /// </summary>
        private static readonly Dictionary<string, string> _canonicalColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ParameterName"] = "ParameterName",
            ["ParameterCategory"] = "ParameterCategory",
            ["ParameterSubtype"] = "ParameterSubtype",
            ["TreatmentArm"] = "TreatmentArm",
            ["ArmN"] = "ArmN",
            ["StudyContext"] = "StudyContext",
            ["DoseRegimen"] = "DoseRegimen",
            ["Dose"] = "Dose",
            ["DoseUnit"] = "DoseUnit",
            ["Population"] = "Population",
            ["Subpopulation"] = "Subpopulation",
            ["Timepoint"] = "Timepoint",
            ["Time"] = "Time",
            ["TimeUnit"] = "TimeUnit",
            ["PrimaryValue"] = "PrimaryValue",
            ["PrimaryValueType"] = "PrimaryValueType",
            ["SecondaryValue"] = "SecondaryValue",
            ["SecondaryValueType"] = "SecondaryValueType",
            ["LowerBound"] = "LowerBound",
            ["UpperBound"] = "UpperBound",
            ["BoundType"] = "BoundType",
            ["Unit"] = "Unit"
        };

        #endregion Field Metadata

        /**************************************************************/
        /// <summary>
        /// Returns the typed boxed value of a column.
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name, case-insensitive.</param>
        /// <returns>Boxed column value, or <c>null</c> for unset values and unknown column names.</returns>
        /// <example>
        /// <code>
        /// var value = ParsedObservationFieldAccess.Get(obs, "ArmN");
        /// var name = ParsedObservationFieldAccess.Get(obs, "ParameterName");
        /// </code>
        /// </example>
        public static object? Get(ParsedObservation obs, string column)
        {
            #region implementation

            return normalizeColumn(column) switch
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
                "Subpopulation" => obs.Subpopulation,
                "Timepoint" => obs.Timepoint,
                "Time" => obs.Time,
                "TimeUnit" => obs.TimeUnit,
                "PrimaryValue" => obs.PrimaryValue,
                "PrimaryValueType" => obs.PrimaryValueType,
                "SecondaryValue" => obs.SecondaryValue,
                "SecondaryValueType" => obs.SecondaryValueType,
                "LowerBound" => obs.LowerBound,
                "UpperBound" => obs.UpperBound,
                "BoundType" => obs.BoundType,
                "Unit" => obs.Unit,
                _ => null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the column value rendered as a string.
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name, case-insensitive.</param>
        /// <returns>String view of the column value, or <c>null</c> when unset.</returns>
        /// <example>
        /// <code>
        /// ParsedObservationFieldAccess.GetAsString(obs, "ArmN");
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
                int i => i.ToString(CultureInfo.InvariantCulture),
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                double dbl => dbl.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets a column to the given typed value.
        /// </summary>
        /// <remarks>
        /// Casts via <c>as</c> for nullable types. Passing the wrong runtime type results
        /// in <c>null</c> being assigned, preserving the fail-quiet behavior of the
        /// predecessor helper.
        /// </remarks>
        /// <param name="obs">Observation to mutate.</param>
        /// <param name="column">Column name, case-insensitive.</param>
        /// <param name="value">Value to assign. Must match the column's underlying CLR type.</param>
        /// <example>
        /// <code>
        /// ParsedObservationFieldAccess.Set(obs, "Timepoint", null);
        /// ParsedObservationFieldAccess.Set(obs, "ArmN", 100);
        /// </code>
        /// </example>
        public static void Set(ParsedObservation obs, string column, object? value)
        {
            #region implementation

            switch (normalizeColumn(column))
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
                case "Subpopulation": obs.Subpopulation = value as string; break;
                case "Timepoint": obs.Timepoint = value as string; break;
                case "Time": obs.Time = value as double?; break;
                case "TimeUnit": obs.TimeUnit = value as string; break;
                case "PrimaryValue": obs.PrimaryValue = value as double?; break;
                case "PrimaryValueType": obs.PrimaryValueType = value as string; break;
                case "SecondaryValue": obs.SecondaryValue = value as double?; break;
                case "SecondaryValueType": obs.SecondaryValueType = value as string; break;
                case "LowerBound": obs.LowerBound = value as double?; break;
                case "UpperBound": obs.UpperBound = value as double?; break;
                case "BoundType": obs.BoundType = value as string; break;
                case "Unit": obs.Unit = value as string; break;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and sets a column from text.
        /// </summary>
        /// <remarks>
        /// Numeric columns are parsed with invariant culture. Unparseable numeric text
        /// clears the target numeric column, matching the prior Claude correction behavior.
        /// </remarks>
        /// <param name="obs">Observation to mutate.</param>
        /// <param name="column">Column name, case-insensitive.</param>
        /// <param name="value">Text value from a correction payload, or <c>null</c> to clear.</param>
        /// <returns><c>true</c> when the column is supported; otherwise <c>false</c>.</returns>
        /// <seealso cref="Set"/>
        public static bool SetFromString(ParsedObservation obs, string column, string? value)
        {
            #region implementation

            var canonical = normalizeColumn(column);
            if (canonical == null)
                return false;

            if (value == null)
            {
                Set(obs, canonical, null);
                return true;
            }

            switch (canonical)
            {
                case "ArmN":
                    obs.ArmN = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var armN)
                        ? armN
                        : null;
                    return true;

                case "Dose":
                    obs.Dose = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dose)
                        ? dose
                        : null;
                    return true;

                case "Time":
                case "PrimaryValue":
                case "SecondaryValue":
                case "LowerBound":
                case "UpperBound":
                    double? parsed = double.TryParse(
                        value,
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture,
                        out var doubleValue)
                            ? doubleValue
                            : null;

                    Set(obs, canonical, parsed);
                    return true;

                default:
                    Set(obs, canonical, value);
                    return true;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// True when the column has a non-null, non-whitespace value.
        /// </summary>
        /// <param name="obs">Observation to read from.</param>
        /// <param name="column">Column name, case-insensitive.</param>
        /// <returns><c>true</c> when populated; otherwise <c>false</c>.</returns>
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

        /**************************************************************/
        /// <summary>
        /// Resolves a caller-provided column name to the canonical name used by switch blocks.
        /// </summary>
        /// <param name="column">Caller-provided column name.</param>
        /// <returns>Canonical column name, or <c>null</c> when unsupported.</returns>
        private static string? normalizeColumn(string column)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(column))
                return null;

            return _canonicalColumns.TryGetValue(column.Trim(), out var canonical)
                ? canonical
                : null;

            #endregion
        }
    }
}
