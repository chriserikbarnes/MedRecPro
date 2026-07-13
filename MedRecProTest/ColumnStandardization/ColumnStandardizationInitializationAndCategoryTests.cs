using System.Globalization;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ImportDbContext = MedRecProImportClass.Data.ApplicationDbContext;
using static MedRecProTest.ColumnStandardizationTestFixture;

namespace MedRecProTest
{
    public partial class ColumnStandardizationServiceTests
    {
        #region Initialization Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that <see cref="ColumnStandardizationService.InitializeAsync"/> loads
        /// drug names from the database and does not throw.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_LoadsDrugDictionary_Succeeds()
        {
            #region implementation

            // Arrange & Act
            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Assert — service initialized without exception
            // The fact that createInitializedServiceAsync completed is the assertion
            Assert.IsNotNull(service);

            // Cleanup
            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that calling InitializeAsync twice does not throw (idempotent).
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_CalledTwice_NoOps()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Act — second call should be a no-op
            await service.InitializeAsync();

            Assert.IsNotNull(service);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Standardize skips processing when not initialized,
        /// returning observations unchanged.
        /// </summary>
        [TestMethod]
        public void Standardize_NotInitialized_ReturnsUnchanged()
        {
            #region implementation

            // Arrange — create service but do NOT call InitializeAsync
            var mockLogger = new Mock<ILogger<ColumnStandardizationService>>();
            var dbOptions = new DbContextOptionsBuilder<ImportDbContext>()
                .UseInMemoryDatabase($"ColStd_NotInit_{Guid.NewGuid()}")
                .Options;
            var context = new ImportDbContext(dbOptions);
            var service = new ColumnStandardizationService(context, mockLogger.Object);

            var obs = createObservation("(N=267)", "Placebo");
            var observations = new List<ParsedObservation> { obs };

            // Act
            var result = service.Standardize(observations);

            // Assert — no correction applied, TreatmentArm unchanged
            Assert.AreEqual("(N=267)", result[0].TreatmentArm);
            Assert.IsNull(result[0].ValidationFlags);

            context.Dispose();

            #endregion
        }

        #endregion Initialization Tests

        #region Category Filtering Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that PK observations skip Phase 1 (arm correction) but Phases 2-4 still run.
        /// Phase 1 does NOT fire for PK, but Phase 2 pre-pass extracts standalone (N=267)
        /// and recovers the arm name from StudyContext.
        /// Phase 3 migrates Percentage→Percentage; Phase 4 nulls ParameterCategory (N/A for PK).
        /// </summary>
        [TestMethod]
        public async Task Standardize_PkCategory_Phase1Skipped_OtherPhasesRun()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo", category: "PK");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Phase 1 skipped — but Phase 2 pre-pass extracts N=267 from TreatmentArm
            Assert.AreEqual(267, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:TreatmentArm");
            // Phase 1 arm correction flags should NOT be present
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "Phase 1 arm correction flags should not fire for PK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that EFFICACY observations ARE processed by the standardization service.
        /// </summary>
        [TestMethod]
        public async Task Standardize_EfficacyCategory_IsProcessed()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo", category: "EFFICACY");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Rule 1 should fire: arm was N=, ctx was drug name
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(267, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that true Comparison statistic rows are not modified.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ComparisonStatisticRow_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Comparison", "Placebo");
            obs.PrimaryValueType = "RiskDifference";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Comparison", result[0].TreatmentArm);
            assertNoFlags(result[0]);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that EFFICACY p-value rows emitted as comparison statistics keep the
        /// comparison arm during column standardization.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ComparisonPValueRow_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Comparison", "Placebo", category: "EFFICACY");
            obs.ParameterName = "p-value";
            obs.RawValue = "0.001";
            obs.PrimaryValue = 0.001;
            obs.PrimaryValueType = "PValue";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Comparison", result[0].TreatmentArm);
            Assert.AreEqual("Placebo", result[0].StudyContext);
            Assert.AreEqual("PValue", result[0].PrimaryValueType);
            assertNoFlags(result[0]);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that expanded EFFICACY p-value phrase rows keep the comparison arm.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ComparisonPValuePhraseRow_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Comparison", "Placebo", category: "EFFICACY");
            obs.ParameterName = "P-value versus Placebo";
            obs.RawValue = "0.0164";
            obs.PrimaryValue = 0.0164;
            obs.PValue = 0.0164;
            obs.PrimaryValueType = "PValue";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Comparison", result[0].TreatmentArm);
            Assert.AreEqual("Placebo", result[0].StudyContext);
            Assert.AreEqual("PValue", result[0].PrimaryValueType);
            assertNoFlags(result[0]);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-stat rows mislabeled as Comparison still go through arm cleanup.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ComparisonNonStatisticRow_CanRecoverFromStudyContext()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Comparison", "Placebo");
            obs.PrimaryValueType = "Percentage";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:SWAP_ARM_CTX");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Category Filtering Tests
    }
}

