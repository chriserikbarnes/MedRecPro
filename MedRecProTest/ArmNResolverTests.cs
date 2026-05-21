using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using MedRecProImportClass.Service.TransformationServices.SampleSize;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for AE ArmN assignment policy.
    /// </summary>
    /// <remarks>
    /// Resolver tests are deliberately database-free so precedence and rejection
    /// behavior can be validated independently of parser and Stage 5 wiring.
    /// </remarks>
    /// <seealso cref="ArmNResolver"/>
    /// <seealso cref="ParsedObservation"/>
    [TestClass]
    public class ArmNResolverTests
    {
        /**************************************************************/
        /// <summary>
        /// Header-derived N populates ArmN when no scoped override is present.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_HeaderN_WinsWithinSameScope()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug", SampleSize = 188 };
            var parsed = new ParsedValue { ParseRule = "n_pct" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed);

            Assert.AreEqual(188, result.ArmN);
            Assert.AreEqual(ArmNResolver.FromHeaderFlag, result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// A scoped body metadata row populates later rows for the same arm.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_BodyMetadataRow_PopulatesLaterRowsForSameArm()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug", SampleSize = null };
            var parsed = new ParsedValue { ParseRule = "percentage" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed, scopedMetadataN: 102);

            Assert.AreEqual(102, result.ArmN);
            Assert.AreEqual(ArmNResolver.FromMetadataRowFlag, result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cell fractions provide denominator evidence for the same observation only.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_FractionDenominator_PopulatesSameObservationOnly()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug" };
            var parsed = new ParsedValue { SampleSize = 103, ParseRule = "frac_pct" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed);

            Assert.AreEqual(103, result.ArmN);
            Assert.AreEqual(ArmNResolver.FromFractionDenominatorFlag, result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Range-like values do not assign ArmN.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_RangeOnly_DoesNotAssignArmN()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug" };
            var parsed = new ParsedValue { ParseRule = "range_to" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed);

            Assert.IsNull(result.ArmN);
            Assert.IsNull(result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Existing higher-precedence contextual N is not overwritten by a conflicting cell N.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_ExistingHigherPrecedenceN_NotOverwritten()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug", SampleSize = 200 };
            var parsed = new ParsedValue { SampleSize = 103, ParseRule = "fraction_count" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed);

            Assert.AreEqual(200, result.ArmN);
            Assert.AreEqual(ArmNResolver.RejectedConflictingNFlag, result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Count-percent inference is labeled distinctly from fraction and inline suffix evidence.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_CountPercentInference_UsesInferenceValidationFlag()
        {
            #region implementation

            var arm = new ArmDefinition { Name = "Drug" };
            var parsed = new ParsedValue { SampleSize = 100, ParseRule = "count_percent_inference" };

            var result = ArmNResolver.ResolveForAeObservation(arm, parsed);

            Assert.AreEqual(100, result.ArmN);
            Assert.AreEqual(ArmNResolver.FromCountPercentInferenceFlag, result.ValidationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parser and Stage 5 conflicting-N diagnostics share one canonical string.
        /// </summary>
        [TestMethod]
        public void ArmNResolver_ConflictingNConstants_RemainSharedAcrossParserAndStage5()
        {
            #region implementation

            Assert.AreEqual(SampleSizeParser.ConflictingNDiagnostic, ArmNResolver.RejectedConflictingNFlag);
            Assert.AreEqual(AeDenormalizationConstants.ArmNRejectedConflictingNFlag, ArmNResolver.RejectedConflictingNFlag);

            #endregion
        }
    }
}
