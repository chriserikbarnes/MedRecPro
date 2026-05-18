using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    public partial class ColumnStandardizationService
    {
        /**************************************************************/
        /// <summary>
        /// Applies the ordered Phase 1 arm/context rule groups for AE and EFFICACY observations.
        /// </summary>
        /// <remarks>
        /// The pipeline owns execution order only. Individual rule bodies remain on
        /// <see cref="ColumnStandardizationService"/> so this refactor preserves the
        /// existing drug dictionary, content classifier, and validation-flag behavior.
        /// </remarks>
        /// <seealso cref="ColumnStandardizationService"/>
        private sealed class Phase1ArmContextPipeline
        {
            /**************************************************************/
            /// <summary>
            /// Parent standardization service that owns the Phase 1 rule implementations.
            /// </summary>
            /// <seealso cref="ColumnStandardizationService"/>
            private readonly ColumnStandardizationService _service;

            /**************************************************************/
            /// <summary>
            /// Rules 1-6 that should stop after the first successful arm correction.
            /// </summary>
            /// <seealso cref="FirstMatchPhase1Rule"/>
            private readonly FirstMatchPhase1Rule[] _firstMatchRules;

            /**************************************************************/
            /// <summary>
            /// Rules 7-10 that should run independently after the arm first-match group.
            /// </summary>
            /// <seealso cref="AlwaysRunPhase1Rule"/>
            private readonly AlwaysRunPhase1Rule[] _alwaysRunRules;

            /**************************************************************/
            /// <summary>
            /// Initializes the ordered Phase 1 rule pipeline.
            /// </summary>
            /// <remarks>
            /// Delegate arrays make the ordering explicit without moving the rule bodies
            /// during this narrow behavior-preserving extraction.
            /// </remarks>
            /// <param name="service">Parent service that owns the concrete rule methods.</param>
            public Phase1ArmContextPipeline(ColumnStandardizationService service)
            {
                #region implementation

                _service = service;
                _firstMatchRules = new FirstMatchPhase1Rule[]
                {
                    _service.applyRule1_ArmIsN,
                    _service.applyRule2_ArmIsFormatHint,
                    _service.applyRule3_ArmIsSeverity,
                    _service.applyRule4_ArmIsDose,
                    _service.applyRule5_ArmIsBareNumber,
                    (obs, armType, _) => _service.applyRule6_ArmIsDrugPlusDose(obs, armType)
                };
                _alwaysRunRules = new AlwaysRunPhase1Rule[]
                {
                    _service.applyRule7_CtxIsArmWithN,
                    _service.applyRule8_CtxIsDrugName,
                    _service.applyRule9_CtxIsDescriptor,
                    (obs, _) => applyRule10_ArmHasTrailingPercent(obs)
                };

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Applies the Phase 1 rule groups to a single parsed observation.
            /// </summary>
            /// <remarks>
            /// Rule 11 remains a pre-chain rule. Rules 1-6 are evaluated in priority
            /// order and stop at the first match. Rules 7-10 then run independently
            /// against the corrected observation and current context classification.
            /// </remarks>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <returns>The number of corrections applied.</returns>
            /// <seealso cref="ParsedObservation"/>
            public int Apply(ParsedObservation obs)
            {
                #region implementation

                int corrections = 0;

                if (_service.applyRule11_ArmHasBracketedN(obs))
                {
                    corrections++;
                }

                var armType = _service.classifyContent(obs.TreatmentArm);
                var ctxType = _service.classifyContent(obs.StudyContext);

                if (applyFirstMatchingArmRule(obs, armType, ctxType))
                {
                    corrections++;
                    ctxType = _service.classifyContent(obs.StudyContext);
                }

                corrections += applyAlwaysRunRules(obs, ctxType);
                return corrections;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Runs the Rule 1-6 first-match sub-chain.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <param name="armType">Current TreatmentArm content classification.</param>
            /// <param name="ctxType">Current StudyContext content classification.</param>
            /// <returns><c>true</c> when one first-match rule applied.</returns>
            /// <seealso cref="FirstMatchPhase1Rule"/>
            private bool applyFirstMatchingArmRule(ParsedObservation obs, ContentType armType, ContentType ctxType)
            {
                #region implementation

                foreach (var rule in _firstMatchRules)
                {
                    if (rule(obs, armType, ctxType))
                    {
                        return true;
                    }
                }

                return false;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Runs the Rule 7-10 always-run cleanup sub-chain.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <param name="ctxType">Current StudyContext content classification.</param>
            /// <returns>The number of always-run corrections applied.</returns>
            /// <seealso cref="AlwaysRunPhase1Rule"/>
            private int applyAlwaysRunRules(ParsedObservation obs, ContentType ctxType)
            {
                #region implementation

                int corrections = 0;
                foreach (var rule in _alwaysRunRules)
                {
                    if (rule(obs, ctxType))
                    {
                        corrections++;
                    }
                }

                return corrections;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Delegate shape for Rule 1-6 first-match arm correction rules.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <param name="armType">Current TreatmentArm content classification.</param>
            /// <param name="ctxType">Current StudyContext content classification.</param>
            /// <returns><c>true</c> when the rule applied.</returns>
            private delegate bool FirstMatchPhase1Rule(ParsedObservation obs, ContentType armType, ContentType ctxType);

            /**************************************************************/
            /// <summary>
            /// Delegate shape for Rule 7-10 always-run cleanup rules.
            /// </summary>
            /// <param name="obs">Observation to inspect and correct in place.</param>
            /// <param name="ctxType">Current StudyContext content classification.</param>
            /// <returns><c>true</c> when the rule applied.</returns>
            private delegate bool AlwaysRunPhase1Rule(ParsedObservation obs, ContentType ctxType);
        }
    }
}
