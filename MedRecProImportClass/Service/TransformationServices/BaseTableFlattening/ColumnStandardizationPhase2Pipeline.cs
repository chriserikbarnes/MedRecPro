using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    public partial class ColumnStandardizationService
    {
        /**************************************************************/
        /// <summary>
        /// Applies the ordered Phase 2 content-normalization passes for every
        /// table category.
        /// </summary>
        /// <remarks>
        /// The pipeline owns execution order only. Individual pass bodies remain on
        /// <see cref="ColumnStandardizationService"/> so this refactor preserves the
        /// existing drug dictionary, PK dictionaries, AE dictionary, and validation
        /// flag behavior.
        /// </remarks>
        /// <seealso cref="ColumnStandardizationService"/>
        private sealed class Phase2ContentNormalizationPipeline
        {
            /**************************************************************/
            /// <summary>
            /// Parent standardization service that owns the Phase 2 pass implementations.
            /// </summary>
            /// <seealso cref="ColumnStandardizationService"/>
            private readonly ColumnStandardizationService _service;

            /**************************************************************/
            /// <summary>
            /// Ordered list of Phase 2 passes that should run exactly once per observation.
            /// </summary>
            /// <seealso cref="Phase2Pass"/>
            private readonly Phase2Pass[] _passes;

            /**************************************************************/
            /// <summary>
            /// Initializes the ordered Phase 2 content-normalization pipeline.
            /// </summary>
            /// <remarks>
            /// Delegate ordering is intentionally explicit: later passes depend on
            /// earlier column movements, especially inline-N stripping before
            /// DoseRegimen triage, subtype unit extraction before PK canonicalization,
            /// and dose scanning after all other column movements.
            /// </remarks>
            /// <param name="service">Parent service that owns the concrete pass methods.</param>
            public Phase2ContentNormalizationPipeline(ColumnStandardizationService service)
            {
                #region implementation

                _service = service;
                _passes = new Phase2Pass[]
                {
                    _service.normalizeInlineNValues,
                    _service.normalizeDoseRegimen,
                    _service.normalizeParameterName,
                    _service.normalizeTreatmentArm,
                    _service.extractUnitFromParameterSubtype,
                    _service.applyPkCanonicalization,
                    _service.normalizeUnit,
                    _service.normalizeParameterCategory,
                    applyAeDictionaryResolution,
                    DoseExtractor.ScanAllColumnsForDose
                };

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Applies every Phase 2 content-normalization pass to one observation.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <returns>The number of Phase 2 corrections applied.</returns>
            /// <seealso cref="ParsedObservation"/>
            public int Apply(ParsedObservation obs)
            {
                #region implementation

                int corrections = 0;

                foreach (var pass in _passes)
                {
                    if (pass(obs))
                    {
                        corrections++;
                    }
                }

                return corrections;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Fills missing AE system-organ-class categories from the optional
            /// dictionary after existing non-null categories have been normalized.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <returns><c>true</c> when the dictionary resolved a missing category.</returns>
            /// <seealso cref="IAeParameterCategoryDictionaryService"/>
            private bool applyAeDictionaryResolution(ParsedObservation obs)
            {
                #region implementation

                return _service._aeDictionary != null &&
                       _service._aeDictionary.TryResolveObservation(obs);

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Delegate shape for an ordered Phase 2 content-normalization pass.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <returns><c>true</c> when the pass applied a correction.</returns>
            private delegate bool Phase2Pass(ParsedObservation obs);
        }
    }
}
