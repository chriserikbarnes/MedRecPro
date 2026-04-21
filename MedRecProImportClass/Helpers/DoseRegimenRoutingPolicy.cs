using MedRecProImportClass.Models;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Shared primitives for DoseRegimen routing across the transformation pipeline.
    /// Centralizes the routing-flag constants, flag inspection helpers, and the
    /// <see cref="ApplyRoute"/> mutation used by both rule-based normalization and
    /// ML-driven correction.
    /// </summary>
    /// <remarks>
    /// The regex decision trees that select <em>which</em> target to route to intentionally
    /// stay in their owning services
    /// (<c>ColumnStandardizationService.normalizeDoseRegimen</c> and
    /// <c>MlNetCorrectionService.labelDoseRegimenRoutingFromRecord</c>) because the two
    /// services use deliberately different vocabularies — rule-based normalization is
    /// broader (anchored patterns with pediatric/neonatal/kg-range content) while ML
    /// label synthesis is narrower (word-boundary matches on a canonical keyword set)
    /// to keep training data clean. This helper exposes only the shared primitives:
    /// routing-flag constants, the flag → target back-mapping used during ML label
    /// synthesis, the presence checks used by both the "skip-if-already-routed" ML
    /// guard and the training-label synthesizer, and the <see cref="ApplyRoute"/>
    /// mutation that writes the target column and nulls the source columns.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    public static class DoseRegimenRoutingPolicy
    {
        #region Flag constants

        /**************************************************************/
        /// <summary>PK sub-parameter content routed to <see cref="ParsedObservation.ParameterSubtype"/>.</summary>
        public const string FlagPkSubparamRouted = "COL_STD:PK_SUBPARAM_ROUTED";

        /**************************************************************/
        /// <summary>Co-administered drug name routed to <see cref="ParsedObservation.ParameterSubtype"/>.</summary>
        public const string FlagCoAdminRouted = "COL_STD:COADMIN_ROUTED";

        /**************************************************************/
        /// <summary>Residual population content extracted to <see cref="ParsedObservation.Population"/>.</summary>
        public const string FlagPopulationExtracted = "COL_STD:POPULATION_EXTRACTED";

        /**************************************************************/
        /// <summary>Residual timepoint content extracted to <see cref="ParsedObservation.Timepoint"/>.</summary>
        public const string FlagTimepointExtracted = "COL_STD:TIMEPOINT_EXTRACTED";

        /**************************************************************/
        /// <summary>
        /// Legacy skip-guard prefix retained for <c>applyDoseRegimenRouting</c>. A row carrying this
        /// substring is treated as already routed and will not be re-routed by ML.
        /// </summary>
        public const string FlagDoseRegimenRoutedToPrefix = "COL_STD:DOSEREGIMEN_ROUTED_TO";

        #endregion Flag constants

        #region Target label constants

        /**************************************************************/
        /// <summary>Training and prediction label for ParameterSubtype routing target.</summary>
        public const string TargetLabelParameterSubtype = "ParameterSubtype";

        /**************************************************************/
        /// <summary>Training and prediction label for Population routing target.</summary>
        public const string TargetLabelPopulation = "Population";

        /**************************************************************/
        /// <summary>Training and prediction label for Timepoint routing target.</summary>
        public const string TargetLabelTimepoint = "Timepoint";

        /**************************************************************/
        /// <summary>Training and prediction label indicating the value should remain in DoseRegimen.</summary>
        public const string TargetLabelKeep = "Keep";

        #endregion Target label constants

        /**************************************************************/
        /// <summary>Typed routing target used by <see cref="ApplyRoute"/> and flag-to-target mapping.</summary>
        public enum RouteTarget
        {
            /// <summary>No target column — used by header-echo clearing where the source is nulled but no destination is written.</summary>
            None,

            /// <summary><see cref="ParsedObservation.ParameterSubtype"/>.</summary>
            ParameterSubtype,

            /// <summary><see cref="ParsedObservation.Population"/>.</summary>
            Population,

            /// <summary><see cref="ParsedObservation.Timepoint"/>.</summary>
            Timepoint
        }

        /**************************************************************/
        /// <summary>
        /// Returns true if any of the four routing-flag sentinels is present in the supplied flags string.
        /// Used by ML training label synthesis to decide whether to infer a routing target from flags.
        /// </summary>
        /// <param name="validationFlags">Semicolon-delimited flag string (may be null).</param>
        /// <returns>True when at least one routing-flag sentinel is present.</returns>
        public static bool HasRoutingFlag(string? validationFlags)
        {
            #region implementation

            if (string.IsNullOrEmpty(validationFlags))
                return false;

            return validationFlags.Contains(FlagPkSubparamRouted)
                || validationFlags.Contains(FlagCoAdminRouted)
                || validationFlags.Contains(FlagPopulationExtracted)
                || validationFlags.Contains(FlagTimepointExtracted);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true if the row has already been routed out of DoseRegimen — either by
        /// the four extraction flags or by the legacy <see cref="FlagDoseRegimenRoutedToPrefix"/>
        /// substring. Used by the ML Stage 2 skip-guard.
        /// </summary>
        /// <param name="validationFlags">Semicolon-delimited flag string (may be null).</param>
        /// <returns>True when any routing sentinel is present.</returns>
        public static bool IsAlreadyRouted(string? validationFlags)
        {
            #region implementation

            if (string.IsNullOrEmpty(validationFlags))
                return false;

            return validationFlags.Contains(FlagDoseRegimenRoutedToPrefix)
                || HasRoutingFlag(validationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps an existing routing flag back to its routing target. Used by ML Stage 2
        /// label synthesis when the row has already been routed by rules.
        /// </summary>
        /// <param name="validationFlags">Semicolon-delimited flag string (may be null).</param>
        /// <returns>The inferred target, or <see cref="RouteTarget.None"/> when no flag matches.</returns>
        public static RouteTarget RouteTargetFromFlags(string? validationFlags)
        {
            #region implementation

            if (string.IsNullOrEmpty(validationFlags))
                return RouteTarget.None;

            // PK sub-parameter and co-admin both route to ParameterSubtype.
            if (validationFlags.Contains(FlagPkSubparamRouted) ||
                validationFlags.Contains(FlagCoAdminRouted))
                return RouteTarget.ParameterSubtype;

            if (validationFlags.Contains(FlagPopulationExtracted))
                return RouteTarget.Population;

            if (validationFlags.Contains(FlagTimepointExtracted))
                return RouteTarget.Timepoint;

            return RouteTarget.None;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an ML-engine predicted target label (case-insensitive) into <see cref="RouteTarget"/>.
        /// Any unrecognized label (including <see cref="TargetLabelKeep"/> and null) yields
        /// <see cref="RouteTarget.None"/>.
        /// </summary>
        /// <param name="target">Predicted label.</param>
        /// <returns>Matching enum value or <see cref="RouteTarget.None"/>.</returns>
        public static RouteTarget ParseTarget(string? target)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(target))
                return RouteTarget.None;

            return target.Trim().ToLowerInvariant() switch
            {
                "parametersubtype" => RouteTarget.ParameterSubtype,
                "population" => RouteTarget.Population,
                "timepoint" => RouteTarget.Timepoint,
                _ => RouteTarget.None
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps a <see cref="RouteTarget"/> to its string label for training/prediction use.
        /// Returns null for <see cref="RouteTarget.None"/>.
        /// </summary>
        /// <param name="target">Typed target.</param>
        /// <returns>Label string or null.</returns>
        public static string? TargetLabel(RouteTarget target)
        {
            #region implementation

            return target switch
            {
                RouteTarget.ParameterSubtype => TargetLabelParameterSubtype,
                RouteTarget.Population => TargetLabelPopulation,
                RouteTarget.Timepoint => TargetLabelTimepoint,
                _ => null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies the route mutation: when <paramref name="target"/> is a real column and the
        /// corresponding field on <paramref name="obs"/> is empty, copies the effective source
        /// value into that field. Then, regardless of target, nulls
        /// <see cref="ParsedObservation.DoseRegimen"/>, <see cref="ParsedObservation.Dose"/>, and
        /// <see cref="ParsedObservation.DoseUnit"/>.
        /// </summary>
        /// <remarks>
        /// The effective source value is <paramref name="sourceValue"/> when supplied, otherwise
        /// the current <see cref="ParsedObservation.DoseRegimen"/>. This lets rule-based callers
        /// (which typically work against a pre-trimmed local variable) assign the trimmed value
        /// while ML callers (which route after prediction) assign the raw DoseRegimen value —
        /// preserving the pre-existing behavior of both paths. Callers passing
        /// <see cref="RouteTarget.None"/> get the source-clear behavior without any target write —
        /// used by the "Co-administered Drug" header-echo branch.
        /// </remarks>
        /// <param name="obs">Observation to mutate.</param>
        /// <param name="target">Destination column, or <see cref="RouteTarget.None"/> for source-only clearing.</param>
        /// <param name="sourceValue">Optional explicit value to assign to the target column. Defaults to <see cref="ParsedObservation.DoseRegimen"/>.</param>
        public static void ApplyRoute(ParsedObservation obs, RouteTarget target, string? sourceValue = null)
        {
            #region implementation

            var value = sourceValue ?? obs.DoseRegimen;

            switch (target)
            {
                case RouteTarget.ParameterSubtype:
                    if (string.IsNullOrEmpty(obs.ParameterSubtype))
                        obs.ParameterSubtype = value;
                    break;
                case RouteTarget.Population:
                    if (string.IsNullOrEmpty(obs.Population))
                        obs.Population = value;
                    break;
                case RouteTarget.Timepoint:
                    if (string.IsNullOrEmpty(obs.Timepoint))
                        obs.Timepoint = value;
                    break;
                case RouteTarget.None:
                default:
                    // No target column written — only source fields are cleared below.
                    break;
            }

            obs.DoseRegimen = null;
            obs.Dose = null;
            obs.DoseUnit = null;

            #endregion
        }
    }
}
