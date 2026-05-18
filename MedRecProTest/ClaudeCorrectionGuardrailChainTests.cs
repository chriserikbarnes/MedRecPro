using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Characterization tests for the Claude correction guardrail chain.
    /// </summary>
    /// <remarks>
    /// These tests pin the exact rejection reason tokens before the guardrail logic is
    /// consumed by <see cref="ClaudeApiCorrectionService"/>. The service tests cover
    /// end-to-end flag formatting; this class keeps each branch cheap and deterministic.
    /// </remarks>
    /// <seealso cref="CorrectionGuardrailChain"/>
    /// <seealso cref="ClaudeApiCorrectionService"/>
    [TestClass]
    public class ClaudeCorrectionGuardrailChainTests
    {
        #region Helpers

        /**************************************************************/
        /// <summary>
        /// Creates the guardrail chain under test.
        /// </summary>
        /// <param name="settings">Optional settings override.</param>
        /// <returns>Initialized guardrail chain.</returns>
        private static CorrectionGuardrailChain createChain(ClaudeApiCorrectionSettings? settings = null)
        {
            #region implementation

            return new CorrectionGuardrailChain(
                settings ?? new ClaudeApiCorrectionSettings(),
                new PlaceboArmClassifier());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal observation for guardrail validation.
        /// </summary>
        /// <returns>Parsed observation with stable row/cell identity.</returns>
        private static ParsedObservation createObservation()
        {
            #region implementation

            return new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 2,
                TextTableID = 1,
                ParameterName = "Headache",
                TreatmentArm = "Active Drug",
                PrimaryValueType = "Numeric",
                RawValue = "10"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a correction context with optional header text.
        /// </summary>
        /// <param name="headers">Header cells to attach to the context.</param>
        /// <returns>Correction context containing header and percent-column metadata.</returns>
        private static ClaudeCorrectionContext createContext(params string[] headers)
        {
            #region implementation

            if (headers.Length == 0)
                return ClaudeCorrectionContext.FromTable(null);

            var table = new ReconstructedTable
            {
                TextTableID = 1,
                TotalColumnCount = headers.Length,
                Rows = new List<ReconstructedRow>
                {
                    new()
                    {
                        RowGroupType = "Header",
                        Classification = RowClassification.ExplicitHeader,
                        Cells = headers.Select((header, index) => new ProcessedCell
                        {
                            SequenceNumber = index + 1,
                            ResolvedColumnStart = index,
                            ResolvedColumnEnd = index + 1,
                            CleanedText = header
                        }).ToList()
                    }
                }
            };

            return ClaudeCorrectionContext.FromTable(table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that a guardrail result rejected with the expected reason token.
        /// </summary>
        /// <param name="result">Guardrail result.</param>
        /// <param name="expectedReason">Expected rejection reason.</param>
        private static void assertRejected(CorrectionGuardrailResult result, string expectedReason)
        {
            #region implementation

            Assert.IsFalse(result.IsAccepted, "Expected the guardrail chain to reject the correction.");
            Assert.AreEqual(expectedReason, result.RejectionReason);

            #endregion
        }

        #endregion

        #region Rejection Branches

        /**************************************************************/
        /// <summary>
        /// Protected fields reject before more specific field guardrails run.
        /// </summary>
        [TestMethod]
        public void Validate_ProtectedField_ReturnsExactReason()
        {
            #region implementation

            var settings = new ClaudeApiCorrectionSettings();
            settings.ProtectedFields.Add("ParameterName");
            var chain = createChain(settings);

            var result = chain.Validate(
                createObservation(),
                "ParameterName",
                "Migraine",
                createContext());

            assertRejected(result, "ProtectedField");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm rewrites that change placebo-class semantics are rejected.
        /// </summary>
        [TestMethod]
        public void Validate_TreatmentArmPlaceboClassFlip_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();
            obs.TreatmentArm = "Active Drug";

            var result = chain.Validate(obs, "TreatmentArm", "Placebo", createContext());

            assertRejected(result, "PlaceboClassFlip");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Protected short arm values cannot be nulled.
        /// </summary>
        [TestMethod]
        public void Validate_TreatmentArmNull_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();
            obs.TreatmentArm = "BSC";

            var result = chain.Validate(obs, "TreatmentArm", null, createContext());

            assertRejected(result, "TreatmentArmNull");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Body-system labels are rejected as TreatmentArm values.
        /// </summary>
        [TestMethod]
        public void Validate_TreatmentArmBodySystem_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();

            var result = chain.Validate(obs, "TreatmentArm", "Ocular", createContext());

            assertRejected(result, "TreatmentArmBodySystem");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Header-token echoes are rejected as TreatmentArm values.
        /// </summary>
        [TestMethod]
        public void Validate_TreatmentArmHeaderToken_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();

            var result = chain.Validate(
                obs,
                "TreatmentArm",
                "Incidence (discontinuation)",
                createContext("Adverse Event", "Incidence (discontinuation)"));

            assertRejected(result, "TreatmentArmHeaderToken");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ParameterName corrections that strictly add tokens are rejected.
        /// </summary>
        [TestMethod]
        public void Validate_ParameterNameSuperset_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();
            obs.ParameterName = "Abdominal pain";

            var result = chain.Validate(obs, "ParameterName", "Abdominal pain Dyspepsia", createContext());

            assertRejected(result, "ParameterNameSuperset");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Percent-column rows cannot be demoted from Percentage to Count.
        /// </summary>
        [TestMethod]
        public void Validate_PercentColumnTypeDemotion_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();
            obs.PrimaryValueType = "Percentage";

            var result = chain.Validate(
                obs,
                "PrimaryValueType",
                "Count",
                createContext("Event", "(N=74) %"));

            assertRejected(result, "PercentColumnTypeDemotion");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Text rows cannot receive a percent unit.
        /// </summary>
        [TestMethod]
        public void Validate_TextRowUnitPercent_ReturnsExactReason()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();
            obs.PrimaryValueType = "Text";

            var result = chain.Validate(obs, "Unit", "%", createContext());

            assertRejected(result, "TextRowUnitPercent");

            #endregion
        }

        #endregion

        #region Accepted Branch

        /**************************************************************/
        /// <summary>
        /// Safe corrections pass through the full guardrail chain.
        /// </summary>
        [TestMethod]
        public void Validate_AcceptedCorrection_ReturnsAcceptedResult()
        {
            #region implementation

            var chain = createChain();
            var obs = createObservation();

            var result = chain.Validate(obs, "ParameterName", "Migraine", createContext());

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(string.Empty, result.RejectionReason);

            #endregion
        }

        #endregion
    }
}
