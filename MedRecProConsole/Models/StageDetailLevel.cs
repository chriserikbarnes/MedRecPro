namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Controls the level of per-batch diagnostic output during table standardization.
    /// Selected interactively at runtime or defaulted to <see cref="None"/> for CLI mode.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="Services.TableStandardizationService.ExecuteParseWithStagesAsync"/>
    /// to determine how much Stage 1→2→3→3.5 intermediate data to display after each batch.
    /// </remarks>
    /// <seealso cref="Services.TableStandardizationService"/>
    public enum StageDetailLevel
    {
        /**************************************************************/
        /// <summary>
        /// Progress bar only — no per-batch stage output. Fastest for large corpus runs.
        /// </summary>
        None,

        /**************************************************************/
        /// <summary>
        /// One summary line per batch: tables pivoted, standardized by category, skipped, observations.
        /// </summary>
        Concise,

        /**************************************************************/
        /// <summary>
        /// Per-table routing, pivoted table data, and parse details for every table.
        /// Very verbose — best for small batch sizes during debugging.
        /// </summary>
        Full
    }
}
