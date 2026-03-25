namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a parsed treatment arm header from an SPL table in Stage 3 of the
    /// SPL Table Normalization pipeline. Extracted from header cell text matching the
    /// pattern <c>ArmName(N=SampleSize)FormatHint</c>.
    /// </summary>
    /// <remarks>
    /// ## Parsing Patterns
    /// Two arm header patterns are supported (tried in order):
    ///
    /// 1. Parenthesized: <c>^(.+?)\s*\([Nn]\s*=\s*(\d+)\)\s*(.*)$</c>
    ///    - Matches: "EVISTA(N=2557)n(%)", "Paroxetine (n = 421) %", "Drug (N=100) %"
    ///
    /// 2. No-parentheses: <c>^(.+?)\s+[Nn]\s*=\s*(\d+)\s*(.*)$</c>
    ///    - Matches: "Placebo n = 51 %", "Drug N=188 n(%)"
    ///
    /// Both patterns extract:
    /// - <see cref="Name"/>: Treatment arm name (e.g., "EVISTA", "Placebo", "Fluconazole")
    /// - <see cref="SampleSize"/>: N from N=xxx — used for PCT_CHECK validation
    /// - <see cref="FormatHint"/>: Format after N (e.g., "n(%)", "%", "") — drives type promotion
    ///
    /// ## Multi-Level Headers
    /// For multi-level headers, <see cref="StudyContext"/> is populated from the parent
    /// colspan header row (e.g., "Treatment", "Prevention").
    ///
    /// ## Usage
    /// <code>
    /// var arm1 = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");
    /// // arm1.Name = "EVISTA", arm1.SampleSize = 2557, arm1.FormatHint = "n(%)"
    ///
    /// var arm2 = ValueParser.ParseArmHeader("Paroxetine (n = 421) %");
    /// // arm2.Name = "Paroxetine", arm2.SampleSize = 421, arm2.FormatHint = "%"
    ///
    /// var arm3 = ValueParser.ParseArmHeader("Placebo n = 51 %");
    /// // arm3.Name = "Placebo", arm3.SampleSize = 51, arm3.FormatHint = "%"
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

        /**************************************************************/
        /// <summary>
        /// Dose regimen extracted from body-row header enrichment (e.g., "10 mg", "20 mg once daily").
        /// Null for arms without dose-specific data. Propagated to <see cref="ParsedObservation.DoseRegimen"/>.
        /// </summary>
        /// <seealso cref="ParsedObservation"/>
        public string? DoseRegimen { get; set; }
    }
}
