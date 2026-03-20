namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a parsed treatment arm header from an SPL table in Stage 3 of the
    /// SPL Table Normalization pipeline. Extracted from header cell text matching the
    /// pattern <c>ArmName(N=SampleSize)FormatHint</c>.
    /// </summary>
    /// <remarks>
    /// ## Parsing Pattern
    /// The arm header regex <c>^(.+?)\s*\(N=(\d+)\)\s*(.*)$</c> extracts:
    /// - <see cref="Name"/>: Treatment arm name (e.g., "EVISTA", "Placebo", "Fluconazole")
    /// - <see cref="SampleSize"/>: N from (N=xxx) — used for PCT_CHECK validation
    /// - <see cref="FormatHint"/>: Format after N (e.g., "n(%)", "%", "") — drives type promotion
    ///
    /// ## Multi-Level Headers
    /// For multi-level headers, <see cref="StudyContext"/> is populated from the parent
    /// colspan header row (e.g., "Treatment", "Prevention").
    ///
    /// ## Usage
    /// <code>
    /// var arm = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");
    /// // arm.Name = "EVISTA", arm.SampleSize = 2557, arm.FormatHint = "n(%)"
    /// </code>
    /// </remarks>
    /// <seealso cref="ParsedValue"/>
    /// <seealso cref="ParsedObservation"/>
    public class ArmDefinition
    {
        /**************************************************************/
        /// <summary>
        /// Treatment arm name (e.g., "EVISTA", "Placebo", "Fluconazole", "Drug A").
        /// </summary>
        public string? Name { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sample size extracted from (N=xxx) in the header. Null if not present.
        /// Used for PCT_CHECK validation in value parsing.
        /// </summary>
        public int? SampleSize { get; set; }

        /**************************************************************/
        /// <summary>
        /// Format hint text after the N value (e.g., "n(%)", "%", "").
        /// Drives type promotion: bare numbers in columns with "%" or "n(%)" hints
        /// are promoted to Percentage in AE context.
        /// </summary>
        public string? FormatHint { get; set; }

        /**************************************************************/
        /// <summary>
        /// Resolved column index from the header. Maps this arm to data cell positions
        /// via <c>ResolvedColumnStart</c> on <see cref="ProcessedCell"/>.
        /// </summary>
        public int? ColumnIndex { get; set; }

        /**************************************************************/
        /// <summary>
        /// Study context from parent colspan header row in multi-level headers.
        /// Example: "Treatment", "Prevention", "Study 1". Null for single-level headers.
        /// </summary>
        public string? StudyContext { get; set; }
    }
}
