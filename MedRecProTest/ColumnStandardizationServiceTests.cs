using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ImportDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="ColumnStandardizationService"/> — the Stage 3.25 deterministic
    /// column standardization service that corrects misclassified values across TreatmentArm,
    /// ArmN, DoseRegimen, StudyContext, and ParameterSubtype for ADVERSE_EVENT and EFFICACY
    /// table categories.
    /// </summary>
    /// <remarks>
    /// ## Test Strategy
    /// Uses SQLite shared-cache in-memory database via <see cref="DtoLabelAccessTestHelper"/>
    /// to seed drug names into vw_ProductsByIngredient, then exercises each of the 9 correction
    /// rules against representative misclassification examples from production data.
    ///
    /// ## Test Organization
    /// - **Initialization tests**: Dictionary loading from DB
    /// - **Classification tests**: Content type detection for each pattern
    /// - **Rule 1–9 tests**: One or more tests per correction rule
    /// - **Category filtering tests**: Verify non-target categories pass through unchanged
    /// - **Edge case tests**: Null values, empty strings, Comparison rows, already-correct data
    /// </remarks>
    /// <seealso cref="IColumnStandardizationService"/>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="TableParsingOrchestrator"/>
    [TestClass]
    public class ColumnStandardizationServiceTests
    {
        #region Test Constants

        private static readonly string[] _seedProductNames = new[]
        {
            "Mycophenolate Mofetil", "EVISTA", "Dofetilide", "Placebo",
            "Omeprazole", "Ranitidine", "Topiramate", "Doxazosin",
            "Paroxetine", "Enoxaparin", "Imiquimod", "Pregabalin",
            "Bortezomib", "Tocilizumab", "Paclitaxel", "Nalmefene",
            "Glycopyrrolate", "Losartan", "KANUMA", "EMPAVELI",
            "LYTGOBI", "Risperidone", "Cetirizine", "Diltiazem",
            "Warfarin", "VIAGRA", "Alogliptin", "Venlafaxine",
            "Metformin", "Progesterone", "Clarithromycin", "Amoxicillin",
            "MYCAPSSA"
        };

        private static readonly string[] _seedSubstanceNames = new[]
        {
            "mycophenolic acid", "raloxifene hydrochloride", "dofetilide",
            "omeprazole", "topiramate", "doxazosin mesylate",
            "azathioprine", "cyclosporine", "pregabalin"
        };

        #endregion Test Constants

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="ColumnStandardizationService"/> backed by a SQLite in-memory
        /// database seeded with drug names from <see cref="_seedProductNames"/> and
        /// <see cref="_seedSubstanceNames"/>.
        /// </summary>
        /// <returns>Tuple of (service, context, sentinel connection). Caller must dispose sentinel and context.</returns>
        private static async Task<(ColumnStandardizationService service, ImportDbContext context, SqliteConnection sentinel)>
            createInitializedServiceAsync()
        {
            #region implementation

            var (sentinel, connection) = createSharedMemoryDb();
            var context = createImportContext(connection);

            // Seed drug names into vw_ProductsByIngredient backing table
            seedDrugNames(connection);

            var mockLogger = new Mock<ILogger<ColumnStandardizationService>>();
            var service = new ColumnStandardizationService(context, mockLogger.Object);
            await service.InitializeAsync();

            return (service, context, sentinel);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a pair of SQLite shared-cache in-memory connections.
        /// The sentinel connection keeps the database alive; the service connection is for the context.
        /// </summary>
        /// <returns>Tuple of (sentinel, connection). Both must be disposed by caller.</returns>
        private static (SqliteConnection sentinel, SqliteConnection connection) createSharedMemoryDb()
        {
            #region implementation

            var dbName = $"file:colstd_{Guid.NewGuid():N}?mode=memory&cache=shared";
            var connStr = $"DataSource={dbName}";

            var sentinel = new SqliteConnection(connStr);
            sentinel.Open();

            var connection = new SqliteConnection(connStr);
            connection.Open();

            return (sentinel, connection);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ImportDbContext"/> backed by the given SQLite connection,
        /// with DDL patched for SQLite compatibility and the vw_ProductsByIngredient backing
        /// table created for drug name dictionary loading.
        /// </summary>
        /// <param name="connection">Open SQLite connection.</param>
        /// <returns>A configured <see cref="ImportDbContext"/>.</returns>
        private static ImportDbContext createImportContext(SqliteConnection connection)
        {
            #region implementation

            var options = new DbContextOptionsBuilder<ImportDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ImportDbContext(options);

            // Create the vw_ProductsByIngredient backing table manually
            // (EF Core ToView entities don't appear in GenerateCreateScript)
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""vw_ProductsByIngredient"" (
                    ""IngredientSubstanceID"" INTEGER,
                    ""UNII"" TEXT,
                    ""SubstanceName"" TEXT,
                    ""IngredientType"" TEXT,
                    ""IngredientID"" INTEGER,
                    ""IngredientClassCode"" TEXT,
                    ""QuantityNumerator"" REAL,
                    ""QuantityNumeratorUnit"" TEXT,
                    ""QuantityDenominator"" REAL,
                    ""StrengthDisplayName"" TEXT,
                    ""IngredientSequence"" INTEGER,
                    ""ActiveMoietyID"" INTEGER,
                    ""MoietyUNII"" TEXT,
                    ""MoietyName"" TEXT,
                    ""ProductID"" INTEGER,
                    ""ProductName"" TEXT,
                    ""DosageFormCode"" TEXT,
                    ""DosageFormName"" TEXT,
                    ""DocumentID"" INTEGER
                )";
            cmd.ExecuteNonQuery();

            return context;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds the vw_ProductsByIngredient backing table with test drug and substance names.
        /// </summary>
        /// <param name="connection">Open SQLite connection with the backing table already created.</param>
        private static void seedDrugNames(SqliteConnection connection)
        {
            #region implementation

            // Seed ProductName values
            foreach (var name in _seedProductNames)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO \"vw_ProductsByIngredient\" (\"ProductName\") VALUES ($name)";
                cmd.Parameters.AddWithValue("$name", name);
                cmd.ExecuteNonQuery();
            }

            // Seed SubstanceName values
            foreach (var name in _seedSubstanceNames)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO \"vw_ProductsByIngredient\" (\"SubstanceName\") VALUES ($name)";
                cmd.Parameters.AddWithValue("$name", name);
                cmd.ExecuteNonQuery();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal ADVERSE_EVENT <see cref="ParsedObservation"/> with the specified
        /// field values for testing correction rules.
        /// </summary>
        /// <param name="treatmentArm">TreatmentArm value to test.</param>
        /// <param name="studyContext">StudyContext value to test.</param>
        /// <param name="doseRegimen">DoseRegimen value (null by default).</param>
        /// <param name="armN">ArmN value (null by default).</param>
        /// <param name="parameterSubtype">ParameterSubtype value (null by default).</param>
        /// <param name="category">Table category (defaults to ADVERSE_EVENT).</param>
        /// <param name="productTitle">ProductTitle fallback (null by default).</param>
        /// <returns>A configured <see cref="ParsedObservation"/>.</returns>
        private static ParsedObservation createObservation(
            string? treatmentArm,
            string? studyContext = null,
            string? doseRegimen = null,
            int? armN = null,
            string? parameterSubtype = null,
            string category = "ADVERSE_EVENT",
            string? productTitle = null)
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = category,
                ParameterName = "Nausea",
                ParameterCategory = "Gastrointestinal disorders",
                TreatmentArm = treatmentArm,
                StudyContext = studyContext,
                DoseRegimen = doseRegimen,
                ArmN = armN,
                ParameterSubtype = parameterSubtype,
                ProductTitle = productTitle,
                TextTableID = 100,
                RawValue = "15.3",
                PrimaryValue = 15.3,
                PrimaryValueType = "Percentage"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that a specific COL_STD flag is present in the observation's ValidationFlags.
        /// </summary>
        /// <param name="obs">The observation to check.</param>
        /// <param name="expectedFlag">The expected flag string (e.g., "COL_STD:ARM_WAS_N").</param>
        private static void assertHasFlag(ParsedObservation obs, string expectedFlag)
        {
            #region implementation

            Assert.IsNotNull(obs.ValidationFlags,
                $"Expected ValidationFlags to contain '{expectedFlag}' but was null");
            Assert.IsTrue(obs.ValidationFlags.Contains(expectedFlag),
                $"Expected ValidationFlags to contain '{expectedFlag}' but was '{obs.ValidationFlags}'");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that no COL_STD flags are present in the observation's ValidationFlags.
        /// </summary>
        /// <param name="obs">The observation to check.</param>
        private static void assertNoFlags(ParsedObservation obs)
        {
            #region implementation

            if (obs.ValidationFlags != null)
            {
                Assert.IsFalse(obs.ValidationFlags.Contains("COL_STD"),
                    $"Expected no COL_STD flags but found '{obs.ValidationFlags}'");
            }

            #endregion
        }

        #endregion Helper Methods

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
        /// Verifies that Comparison rows are never modified.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ComparisonRow_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Comparison", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Comparison", result[0].TreatmentArm);
            assertNoFlags(result[0]);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Category Filtering Tests

        #region Rule 1 Tests — TreatmentArm is N= Value

        /**************************************************************/
        /// <summary>
        /// Pattern 2: TreatmentArm="(N=267)", StudyContext="Placebo"
        /// Expected: ArmN=267, TreatmentArm="Placebo", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsParenthesizedN_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(267, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 variant: TreatmentArm="(n=99)", StudyContext="Omeprazole 20 mg a.m."
        /// Expected: ArmN=99, TreatmentArm recovered from StudyContext.
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsLowercaseN_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n=99)", "Omeprazole");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(99, result[0].ArmN);
            Assert.AreEqual("Omeprazole", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 variant: TreatmentArm="N=677", StudyContext="Dofetilide"
        /// (no parentheses). Expected: ArmN=677, TreatmentArm="Dofetilide".
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsBareNEquals_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("N=677", "Dofetilide");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(677, result[0].ArmN);
            Assert.AreEqual("Dofetilide", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 with non-drug StudyContext: TreatmentArm="(N=267)", StudyContext="Some Unknown"
        /// Expected: ArmN=267, TreatmentArm cleared (no recovery possible).
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsN_CtxNotDrug_ArmCleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Some Unknown Context");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(267, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 1 Tests

        #region Rule 2 Tests — TreatmentArm is Format Hint

        /**************************************************************/
        /// <summary>
        /// Pattern 3: TreatmentArm="%", StudyContext="Dofetilide"
        /// Expected: TreatmentArm="Dofetilide", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsPercent_RecoveredFromCtx()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("%", "Dofetilide");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Dofetilide", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 3 variant: TreatmentArm="#", StudyContext is not a drug.
        /// Expected: TreatmentArm cleared, flag set.
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsHash_CtxNotDrug_ArmCleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("#", "Target Topiramate Tablets Dosage (mg per day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 3 variant: TreatmentArm="n(%)", StudyContext="Placebo".
        /// Expected: TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsNPct_RecoveredFromCtx()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("n(%)", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 2 Tests

        #region Rule 3 Tests — TreatmentArm is Severity Grade

        /**************************************************************/
        /// <summary>
        /// Pattern 4: TreatmentArm="Severe", StudyContext="Dosing Regimen"
        /// Expected: ParameterSubtype="Severe", TreatmentArm cleared (no drug in ctx).
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsSevere_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Severe", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Severe", result[0].ParameterSubtype);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4 variant: TreatmentArm="Grades 3/4", StudyContext="% of Patients"
        /// Expected: ParameterSubtype="Grades 3/4", TreatmentArm cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsGrades_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Grades 3/4", "% of Patients");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Grades 3/4", result[0].ParameterSubtype);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4 variant: TreatmentArm="Grades 1–4" (en-dash).
        /// Expected: ParameterSubtype="Grades 1–4".
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsGradesEnDash_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Grades 1\u20134", "% of Patients");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Grades 1\u20134", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4: TreatmentArm="Total", StudyContext="Dosing Regimen"
        /// Expected: ParameterSubtype="Total".
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsTotal_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Total", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Total", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 3 Tests

        #region Rule 4 Tests — TreatmentArm is Pure Dose

        /**************************************************************/
        /// <summary>
        /// Pattern 1: TreatmentArm="10 mg", StudyContext="Placebo"
        /// Expected: DoseRegimen="10 mg", TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_MovedToDoseRegimen()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("10 mg", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("10 mg", result[0].DoseRegimen);
            Assert.AreEqual(10m, result[0].Dose, "Dose extracted from DoseRegimen after arm-to-dose swap");
            Assert.AreEqual("mg", result[0].DoseUnit);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1 variant: TreatmentArm="500 mcg BID", StudyContext="Dofetilide Capsules Dose"
        /// Expected: DoseRegimen="500 mcg BID", TreatmentArm="Dofetilide Capsules" (from dose descriptor).
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_CtxIsDoseDescriptor_ExtractsDrug()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("500 mcg BID", "Dofetilide Capsules Dose");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("500 mcg BID", result[0].DoseRegimen);
            // "Dofetilide Capsules" extracted from dose descriptor; "Dofetilide" is a known drug
            Assert.IsNotNull(result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1 variant: TreatmentArm="1000 mg Once Daily", StudyContext="Placebo"
        /// Expected: DoseRegimen="1000 mg Once Daily", TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_CtxIsDrug_SwapsCorrectly()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("1000 mg Once Daily", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("1000 mg Once Daily", result[0].DoseRegimen);
            Assert.AreEqual(1000m, result[0].Dose, "Dose extracted from DoseRegimen after arm-to-dose swap");
            Assert.AreEqual("mg/d", result[0].DoseUnit, "Frequency promotion: 'mg' + 'Once Daily' → 'mg/d'");
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1: TreatmentArm="30 mg q12h subcutaneously", StudyContext="Dosing Regimen"
        /// Expected: DoseRegimen populated, arm recovered from ProductTitle.
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDoseWithSchedule_MovedToDoseRegimen()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("30 mg q12h subcutaneously", "Dosing Regimen",
                productTitle: "Enoxaparin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("30 mg q12h subcutaneously", result[0].DoseRegimen);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 4 Tests

        #region Rule 5 Tests — TreatmentArm is Bare Number

        /**************************************************************/
        /// <summary>
        /// Pattern 8: TreatmentArm="200", StudyContext="Target Topiramate Tablets Dosage (mg/day)"
        /// Expected: DoseRegimen="200 mg/day", TreatmentArm="Topiramate Tablets" or "Topiramate".
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber_CtxIsDoseDescriptor_Reconstructs()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("200", "Target Topiramate Tablets Dosage (mg/day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("200 mg/day", result[0].DoseRegimen);
            Assert.IsNotNull(result[0].TreatmentArm);
            // Should extract "Topiramate Tablets" or similar
            Assert.IsTrue(result[0].TreatmentArm!.Contains("Topiramate"),
                $"Expected TreatmentArm to contain 'Topiramate' but was '{result[0].TreatmentArm}'");
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_BARE_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 8 variant: TreatmentArm="600", StudyContext="Target Topiramate Tablets Dosage (mg per day)"
        /// Expected: DoseRegimen reconstructed, drug extracted.
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber600_Reconstructs()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("600", "Target Topiramate Tablets Dosage (mg per day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].DoseRegimen);
            Assert.IsTrue(result[0].DoseRegimen!.StartsWith("600"),
                $"Expected DoseRegimen to start with '600' but was '{result[0].DoseRegimen}'");
            assertHasFlag(result[0], "COL_STD:ARM_WAS_BARE_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bare number WITHOUT a dose descriptor context should NOT trigger Rule 5.
        /// TreatmentArm="200", StudyContext="Heart Study" — no correction expected
        /// (200 is ambiguous without a dose descriptor).
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber_CtxIsStudy_NoCorrection()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("200", "Heart Study");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // No Phase 1 rule matches: bare number without dose descriptor context
            Assert.AreEqual("200", result[0].TreatmentArm);
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for bare number without dose descriptor");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 5 Tests

        #region Rule 6 Tests — TreatmentArm is Drug+Dose Combined

        /**************************************************************/
        /// <summary>
        /// Pattern 5: TreatmentArm="Mycophenolate Mofetil 2g/day"
        /// Expected: TreatmentArm="Mycophenolate Mofetil", DoseRegimen="2g/day".
        /// </summary>
        [TestMethod]
        public async Task Rule6_ArmIsDrugPlusDose_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Mycophenolate Mofetil 2g/day", "Kidney Studies");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Mycophenolate Mofetil", result[0].TreatmentArm);
            Assert.AreEqual("2g/day", result[0].DoseRegimen);
            Assert.AreEqual(2m, result[0].Dose, "Dose extracted from split DoseRegimen");
            Assert.AreEqual("g", result[0].DoseUnit);
            Assert.AreEqual("Kidney Studies", result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:SPLIT_DRUG_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 5: DoseRegimen already populated — should NOT overwrite.
        /// TreatmentArm="Mycophenolate Mofetil 2g/day", DoseRegimen="existing dose"
        /// Expected: No change (preserves existing DoseRegimen).
        /// </summary>
        [TestMethod]
        public async Task Rule6_ArmIsDrugPlusDose_DoseExists_NoSplit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Mycophenolate Mofetil 2g/day", "Kidney Studies",
                doseRegimen: "existing dose");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Should not split because DoseRegimen already populated
            Assert.AreEqual("Mycophenolate Mofetil 2g/day", result[0].TreatmentArm);
            Assert.AreEqual("existing dose", result[0].DoseRegimen);
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:SPLIT_DRUG_DOSE") == true,
                "Should not split when DoseRegimen already populated");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 6 Tests

        #region Rule 7 Tests — StudyContext Contains Arm+N

        /**************************************************************/
        /// <summary>
        /// Pattern 9: StudyContext="Doxazosin N=339", TreatmentArm="Some non-drug text"
        /// Expected: ArmN=339, TreatmentArm="Doxazosin", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithN_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some non-drug text", "Doxazosin N=339");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(339, result[0].ArmN);
            Assert.AreEqual("Doxazosin", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="KANUMA N = 36" (with spaces around =)
        /// Expected: ArmN=36, StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithSpacedN_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Nausea", "KANUMA N = 36");
            // TreatmentArm is "Nausea" — not a drug, so Rule 7 can overwrite
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(36, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="Placebo N=300"
        /// Rule 1 fires first (arm="(N=267)" → ArmN=267, arm cleared).
        /// Then Rule 7 fires (ctx="Placebo N=300" → ArmN=300, TreatmentArm="Placebo").
        /// Final ArmN=300 (Rule 7 overwrites Rule 1's value).
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsPlaceboWithN_ExtractsArmAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo N=300");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Rule 1 fires: ArmN=267, arm cleared. Rule 7 fires: ArmN=300, arm="Placebo"
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(300, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="Control Arm (N=18) n (%)"
        /// Expected: ArmN=18, StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithNAndHint_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Nausea", "Control Arm (N=18) n (%)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(18, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 7 Tests

        #region Rule 8 Tests — StudyContext is Drug Name (Swap)

        /**************************************************************/
        /// <summary>
        /// Pattern 6: StudyContext="Alogliptin" (drug), TreatmentArm="Nausea" (not a drug)
        /// Expected: Swap — TreatmentArm="Alogliptin", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule8_CtxIsDrug_ArmIsNot_Swaps()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some Non-Drug Text", "Alogliptin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Alogliptin", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:SWAP_ARM_CTX");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Both arm and ctx are drug names — no swap should occur.
        /// TreatmentArm="Placebo", StudyContext="Alogliptin"
        /// Expected: No change (arm is already a drug).
        /// </summary>
        [TestMethod]
        public async Task Rule8_BothAreDrugs_NoSwap()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Alogliptin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual("Alogliptin", result[0].StudyContext);
            // No swap flag should be set
            if (result[0].ValidationFlags != null)
            {
                Assert.IsFalse(result[0].ValidationFlags.Contains("COL_STD:SWAP_ARM_CTX"),
                    "Should not swap when both are drug names");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 8 Tests

        #region Rule 9 Tests — StudyContext is Descriptor Hint

        /**************************************************************/
        /// <summary>
        /// Pattern 7: StudyContext="Reaction"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsReaction_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Reaction");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Incidence"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsIncidence_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("EVISTA", "Incidence");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("EVISTA", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Adverse Event"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsAdverseEvent_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Adverse Event");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Dosing Regimen"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsDosingRegimen_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Discontinuation"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsDiscontinuation_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Discontinuation");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 9 Tests

        #region Rule 10 Tests — TreatmentArm Has Trailing %

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="MYCAPSSA %", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="MYCAPSSA", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasTrailingPercent_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("MYCAPSSA %", "Heart Study");
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 29;
            obs.Unit = null;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("MYCAPSSA", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            Assert.AreEqual("%", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="PLACEBO %", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="PLACEBO", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_PlaceboWithPercent_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("PLACEBO %", null);
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 21;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("PLACEBO", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Drug n(%)", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="Drug", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasTrailingNPct_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo n(%)", null);
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 7;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="MYCAPSSA %", PrimaryValueType="Percentage" (already set by source).
        /// Expected: Arm stripped, PrimaryValueType migrated to "Percentage" by Phase 3.
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasPercent_AlreadyPercentage_StillStrips()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("MYCAPSSA %", null);
            obs.PrimaryValueType = "Percentage";
            obs.PrimaryValue = 29;
            obs.Unit = "%";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("MYCAPSSA", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Pimecrolimus Cream; 1%" — the % is part of a concentration, not a format hint.
        /// The trailing format hint pattern requires whitespace before %, so "1%" should NOT match.
        /// Expected: No change.
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmWithConcentrationPercent_NoChange()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Pimecrolimus Cream; 1%", "Some Study");
            obs.PrimaryValueType = "Numeric";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Pimecrolimus Cream; 1%", result[0].TreatmentArm);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            // Should not have Rule 10 flag
            if (result[0].ValidationFlags != null)
            {
                Assert.IsFalse(result[0].ValidationFlags.Contains("COL_STD:ARM_STRIP_PCT"),
                    "Should not strip % from concentration like '1%'");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 10 Tests

        #region Rule 11 Tests — TreatmentArm Has Bracketed [N=xxx]

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Placebo [N=459]"
        /// Expected: TreatmentArm="Placebo", ArmN=459.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsPlaceboWithBracketN_ExtractsArmAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=459]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(459, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="75 mg/day [N=77]"
        /// Expected: DoseRegimen="75 mg/day", ArmN=77, TreatmentArm resolved from drug dictionary.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsDoseWithBracketN_ExtractsDoseAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("75 mg/day [N=77]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("75 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(77, result[0].ArmN);
            // TreatmentArm should be resolved from drug dictionary via ProductTitle
            Assert.IsNotNull(result[0].TreatmentArm,
                "TreatmentArm should be resolved from drug dictionary");
            // "pregabalin" is in our seed substance names
            Assert.IsTrue(
                result[0].TreatmentArm!.Contains("pregabalin", StringComparison.OrdinalIgnoreCase),
                $"Expected TreatmentArm to contain 'pregabalin' but was '{result[0].TreatmentArm}'");
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="600 mg/day [N=369]"
        /// Expected: DoseRegimen="600 mg/day", ArmN=369.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIs600mgWithBracketN_ExtractsDoseAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("600 mg/day [N=369]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("600 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(369, result[0].ArmN);
            Assert.IsNotNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="All PGB [N=979]" — "All" prefix should be stripped,
        /// "PGB" is a known abbreviation → TreatmentArm="PGB".
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsAllPgbWithBracketN_StripsAllAndResolves()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("All PGB [N=979]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("PGB", result[0].TreatmentArm);
            Assert.AreEqual(979, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="150 mg/day [N=212]" — dose with drug recovery from dictionary.
        /// ProductTitle="Pregabalin Tablets" (seed name "Pregabalin" is in substance list).
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIs150mgWithBracketN_ResolvesDrugFromDictionary()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("150 mg/day [N=212]", null,
                productTitle: "Pregabalin Extended-Release Tablets");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("150 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(212, result[0].ArmN);
            // Should resolve "pregabalin" from the drug dictionary via ProductTitle
            Assert.IsNotNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple bracketed-N observations in a batch — consistent handling.
        /// Simulates the actual table from the screenshot.
        /// </summary>
        [TestMethod]
        public async Task Rule11_BatchOfBracketedN_AllCorrected()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var observations = new List<ParsedObservation>
            {
                createObservation("All PGB [N=979]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("Placebo [N=459]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("75 mg/day [N=77]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("150 mg/day [N=212]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("300 mg/day [N=321]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("600 mg/day [N=369]", null, productTitle: "LYRICA- pregabalin capsule"),
            };

            var result = service.Standardize(observations);

            // All PGB — drug abbreviation
            Assert.AreEqual("PGB", result[0].TreatmentArm);
            Assert.AreEqual(979, result[0].ArmN);

            // Placebo — drug name
            Assert.AreEqual("Placebo", result[1].TreatmentArm);
            Assert.AreEqual(459, result[1].ArmN);

            // Dose arms — should all have DoseRegimen populated and drug resolved
            for (int i = 2; i < 6; i++)
            {
                Assert.IsNotNull(result[i].DoseRegimen,
                    $"Observation {i}: DoseRegimen should be populated");
                Assert.IsNotNull(result[i].ArmN,
                    $"Observation {i}: ArmN should be populated");
                Assert.IsNotNull(result[i].TreatmentArm,
                    $"Observation {i}: TreatmentArm should be resolved from dictionary");
                assertHasFlag(result[i], "COL_STD:ARM_BRACKET_N");
            }

            // Verify specific doses
            Assert.AreEqual("75 mg/day", result[2].DoseRegimen);
            Assert.AreEqual("150 mg/day", result[3].DoseRegimen);
            Assert.AreEqual("300 mg/day", result[4].DoseRegimen);
            Assert.AreEqual("600 mg/day", result[5].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm without brackets should NOT trigger Rule 11.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmWithoutBrackets_NoChange()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Heart Study",
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].ArmN);
            if (result[0].ValidationFlags != null)
            {
                Assert.IsFalse(result[0].ValidationFlags.Contains("COL_STD:ARM_BRACKET_N"),
                    "Rule 11 should not fire without brackets");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 11 Tests

        #region Edge Case Tests

        /**************************************************************/
        /// <summary>
        /// Already-correct data should pass through without modification.
        /// TreatmentArm="EVISTA", StudyContext="Kidney Studies", ArmN=2557
        /// Expected: No changes, no flags.
        /// </summary>
        [TestMethod]
        public async Task Standardize_AlreadyCorrect_NoChanges()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("EVISTA", "Kidney Studies", armN: 2557);
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("EVISTA", result[0].TreatmentArm);
            Assert.AreEqual("Kidney Studies", result[0].StudyContext);
            Assert.AreEqual(2557, result[0].ArmN);
            // Phase 1 should not fire — arm is already a drug name with correct placement
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for already-correct data");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null TreatmentArm and StudyContext should not throw.
        /// </summary>
        [TestMethod]
        public async Task Standardize_NullFields_NoException()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, null);
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            // Phase 1 should not fire on null fields
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for null TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Empty observations list should not throw.
        /// </summary>
        [TestMethod]
        public async Task Standardize_EmptyList_ReturnsEmpty()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var result = service.Standardize(new List<ParsedObservation>());

            Assert.AreEqual(0, result.Count);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Known abbreviation "AZA" should be recognized as a drug name.
        /// TreatmentArm="Some text", StudyContext="AZA"
        /// Expected: Rule 8 swap — TreatmentArm="AZA".
        /// </summary>
        [TestMethod]
        public async Task Standardize_KnownAbbreviation_RecognizedAsDrug()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some non-drug text", "AZA");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AZA", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:SWAP_ARM_CTX");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple observations in a single batch — each should be corrected independently.
        /// </summary>
        [TestMethod]
        public async Task Standardize_MultipleMixedObservations_EachCorrected()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var observations = new List<ParsedObservation>
            {
                createObservation("(N=267)", "Placebo"),           // Rule 1
                createObservation("%", "Dofetilide"),              // Rule 2
                createObservation("Severe", "Dosing Regimen"),     // Rule 3
                createObservation("EVISTA", "Kidney Studies"),     // Already correct
                createObservation("10 mg", "Placebo"),             // Rule 4
            };

            var result = service.Standardize(observations);

            // Rule 1
            Assert.AreEqual(267, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);

            // Rule 2
            Assert.AreEqual("Dofetilide", result[1].TreatmentArm);

            // Rule 3
            Assert.AreEqual("Severe", result[2].ParameterSubtype);

            // Already correct — no Phase 1 flag
            Assert.AreEqual("EVISTA", result[3].TreatmentArm);
            Assert.IsFalse(result[3].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for already-correct EVISTA");

            // Rule 4
            Assert.AreEqual("10 mg", result[4].DoseRegimen);
            Assert.AreEqual("Placebo", result[4].TreatmentArm);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Existing ValidationFlags should be preserved and appended to, not overwritten.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ExistingFlags_Appended()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo");
            obs.ValidationFlags = "PCT_CHECK:PASS";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("PCT_CHECK:PASS"),
                "Original flags should be preserved");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("COL_STD:ARM_WAS_N"),
                "New flag should be appended");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// StudyContext that looks like a study name should NOT be cleared.
        /// StudyContext="Heart Study" — a legitimate study context.
        /// </summary>
        [TestMethod]
        public async Task Standardize_LegitimateStudyContext_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Heart Study");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Heart Study", result[0].StudyContext);
            // Phase 1 should not fire — study context is legitimate
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for legitimate study context");
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:CTX_") == true,
                "No Phase 1 context correction should fire for legitimate study context");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Edge Case Tests

        #region Phase 2 Tests — DoseRegimen Triage

        /**************************************************************/
        /// <summary>
        /// Phase 2a: PK sub-parameter in DoseRegimen → routes to ParameterSubtype.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_PkSubParam_RoutesToParameterSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "Cmax";
            obs.ParameterSubtype = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen, "DoseRegimen should be null after PK sub-param routing");
            Assert.IsNull(result[0].Dose, "Dose should be cleared when DoseRegimen is routed away");
            Assert.IsNull(result[0].DoseUnit, "DoseUnit should be cleared when DoseRegimen is routed away");
            Assert.AreEqual("Cmax", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_ROUTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Actual dose value in DoseRegimen is preserved.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ActualDose_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "50 mg once daily";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("50 mg once daily", result[0].DoseRegimen, "Actual dose should be preserved");
            Assert.AreEqual(50m, result[0].Dose, "Dose should be extracted from preserved DoseRegimen");
            Assert.AreEqual("mg/d", result[0].DoseUnit, "Frequency promotion: 'mg' + 'Once Daily' → 'mg/d'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Drug name in DoseRegimen for DDI → routes to ParameterSubtype.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_CoAdminDrug_RoutesToParameterSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.DoseRegimen = "Omeprazole";
            obs.ParameterSubtype = null;
            obs.PrimaryValueType = "Ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.IsNull(result[0].Dose, "Dose should be cleared when DoseRegimen is routed away");
            Assert.IsNull(result[0].DoseUnit, "DoseUnit should be cleared when DoseRegimen is routed away");
            Assert.AreEqual("Omeprazole", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:COADMIN_ROUTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Residual population in DoseRegimen → routes to Population.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ResidualPopulation_RoutesToPopulation()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "elderly";
            obs.Population = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.AreEqual("elderly", result[0].Population);
            assertHasFlag(result[0], "COL_STD:POPULATION_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Residual timepoint in DoseRegimen → routes to Timepoint.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ResidualTimepoint_RoutesToTimepoint()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "steady state";
            obs.Timepoint = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.AreEqual("steady state", result[0].Timepoint);
            assertHasFlag(result[0], "COL_STD:TIMEPOINT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — DoseRegimen Triage

        #region Phase 2 Tests — ParameterName Cleanup

        /**************************************************************/
        /// <summary>
        /// Phase 2b: Caption echo in ParameterName → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_CaptionEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterName = "Table 3. Pharmacokinetic Parameters";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:ROW_TYPE=CAPTION");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2b: Header echo "n" in ParameterName → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_HeaderEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterName = "n";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:ROW_TYPE=HEADER");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2b: HTML entities in ParameterName → decoded.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_HtmlEntities_Decoded()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.ParameterName = "ALT &gt; 3x ULN";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ALT > 3x ULN", result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:HTML_ENTITY_DECODED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — ParameterName Cleanup

        #region Phase 2 Tests — TreatmentArm Cleanup

        /**************************************************************/
        /// <summary>
        /// Phase 2c: Header echo "Number of Patients" in TreatmentArm → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_TreatmentArm_HeaderEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "PK");
            obs.TreatmentArm = "Number of Patients";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_HEADER");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2c: Generic arm label "Treatment" → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_TreatmentArm_GenericLabel_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "PK");
            obs.TreatmentArm = "Treatment";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_GENERIC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — TreatmentArm Cleanup

        #region Phase 2 Tests — Unit Scrub

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Known unit passes through unchanged.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_KnownUnit_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg/mL", result[0].Unit);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Long unit (> 30 chars) detected as header leak → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_HeaderLeak_LongString_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Drug Delivery Rate Including Infusion Therapy";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Unit containing header keyword → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_HeaderKeyword_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Dosage Regimen";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Variant unit spelling normalized to canonical form.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_VariantSpelling_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg h/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — Unit Scrub

        #region Phase 2 Tests — SOC Mapping

        /**************************************************************/
        /// <summary>
        /// Phase 2e: SOC variant normalized to canonical name.
        /// </summary>
        [TestMethod]
        public async Task Phase2_SOC_VariantNormalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.ParameterCategory = "gastrointestinal";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Gastrointestinal Disorders", result[0].ParameterCategory);
            assertHasFlag(result[0], "COL_STD:SOC_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2e: SOC mapping only applies to ADVERSE_EVENT category.
        /// Phase 4 nulls ParameterCategory for PK (N/A), so the value is null — but NOT
        /// because of SOC normalization. Verify no SOC_NORMALIZED flag is present.
        /// </summary>
        [TestMethod]
        public async Task Phase2_SOC_NonAeCategory_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterCategory = "gastrointestinal";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // ParameterCategory is null due to Phase 4 contract enforcement (N/A for PK),
            // NOT because of SOC normalization — verify no SOC_NORMALIZED flag
            Assert.IsNull(result[0].ParameterCategory, "Phase 4 should null ParameterCategory for PK");
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:SOC_NORMALIZED") == true,
                "SOC normalization should not apply to PK category");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — SOC Mapping

        #region Phase 3 Tests — PrimaryValueType Migration

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Mean" in PK context → "ArithmeticMean" (ArithmeticMean is the default for
        /// ALL categories; GeometricMean only when caption/header/footer explicitly says so).
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_MeanInPK_BecomesArithmeticMean()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Mean→ArithmeticMean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Mean" in AE context → "ArithmeticMean".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_MeanInAE_BecomesArithmeticMean()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Mean→ArithmeticMean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Percentage" is already canonical — no migration, no flag.
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_Percentage_RemainsPercentage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Percentage";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Numeric" in AE with Unit="%" → "Percentage".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_NumericAeWithPercent_BecomesPercentageCount()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Numeric";
            obs.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Numeric→Percentage");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Numeric" in DDI → "GeometricMeanRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_NumericInDDI_BecomesGeometricMeanRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Numeric";
            obs.Unit = "ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("GeometricMeanRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Numeric→GeometricMeanRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Ratio" in DDI → "GeometricMeanRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_RatioInDDI_BecomesGeometricMeanRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("GeometricMeanRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Ratio→GeometricMeanRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "RelativeRiskReduction" with "hazard" in caption → "HazardRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_RRR_WithHazardCaption_BecomesHazardRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "EFFICACY");
            obs.PrimaryValueType = "RelativeRiskReduction";
            obs.Caption = "Hazard Ratio for Overall Survival";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("HazardRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:RelativeRiskReduction→HazardRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 3 Tests — PrimaryValueType Migration

        #region Phase 4 Tests — Column Contract Enforcement

        /**************************************************************/
        /// <summary>
        /// Phase 4: N/A columns are nulled for PK (e.g., ParameterCategory).
        /// </summary>
        [TestMethod]
        public async Task Phase4_NullEnforcement_PK_ParameterCategoryNulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterCategory = "Some leftover category";
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterCategory, "ParameterCategory should be NULL for PK");
            assertHasFlag(result[0], "COL_STD:NULL_ParameterCategory");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: N/A columns are nulled for AE (Timepoint, Time, TimeUnit).
        /// </summary>
        [TestMethod]
        public async Task Phase4_NullEnforcement_AE_TimepointColumnsNulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Timepoint = "Week 24";
            obs.Time = 24.0;
            obs.TimeUnit = "weeks";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Timepoint, "Timepoint should be NULL for AE");
            Assert.IsNull(result[0].Time, "Time should be NULL for AE");
            Assert.IsNull(result[0].TimeUnit, "TimeUnit should be NULL for AE");
            assertHasFlag(result[0], "COL_STD:NULL_Timepoint");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Missing required column is flagged.
        /// </summary>
        [TestMethod]
        public async Task Phase4_MissingRequired_AE_MissingTreatmentArm_Flagged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            assertHasFlag(result[0], "COL_STD:MISSING_R_TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Default BoundType applied for DDI with bounds but no BoundType.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_DDI_Gets90CI()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Ratio";
            obs.LowerBound = 0.80;
            obs.UpperBound = 1.25;
            obs.BoundType = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("90CI", result[0].BoundType);
            assertHasFlag(result[0], "COL_STD:BOUND_TYPE_INFERRED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Default BoundType applied for EFFICACY with bounds → 95CI.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_Efficacy_Gets95CI()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "EFFICACY");
            obs.PrimaryValueType = "RelativeRiskReduction";
            obs.LowerBound = 0.45;
            obs.UpperBound = 0.88;
            obs.BoundType = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("95CI", result[0].BoundType);
            assertHasFlag(result[0], "COL_STD:BOUND_TYPE_INFERRED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Existing BoundType is NOT overwritten.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_ExistingBoundType_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.LowerBound = 10.0;
            obs.UpperBound = 20.0;
            obs.BoundType = "90CI";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("90CI", result[0].BoundType, "Existing BoundType should not be overwritten");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 4 Tests — Column Contract Enforcement

        #region Cross-Category Tests

        /**************************************************************/
        /// <summary>
        /// PK observations are now processed (not skipped).
        /// </summary>
        [TestMethod]
        public async Task CrossCategory_PK_NowProcessed()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";
            obs.ParameterCategory = "ShouldBeNulled";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Phase 3 should migrate Mean → ArithmeticMean for PK (per commit 1e80942)
            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            // Phase 4 should null ParameterCategory (N/A for PK)
            Assert.IsNull(result[0].ParameterCategory);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// DOSING observations are now processed.
        /// </summary>
        [TestMethod]
        public async Task CrossCategory_Dosing_NowProcessed()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "DOSING");
            obs.ParameterName = "Starting Dose";
            obs.PrimaryValueType = "Numeric";
            obs.DoseRegimen = "20 mg";
            obs.Population = "Adult";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Phase 3: Numeric stays as Numeric for DOSING (prescriptive)
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            // Phase 4: ArmN should be null (N/A for DOSING)
            Assert.IsNull(result[0].ArmN);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// SKIP category is still skipped by all phases.
        /// </summary>
        [TestMethod]
        public async Task CrossCategory_Skip_StillSkipped()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "SKIP");
            obs.PrimaryValueType = "Mean";
            obs.ParameterCategory = "ShouldNotBeNulled";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Nothing should change for SKIP
            Assert.AreEqual("Mean", result[0].PrimaryValueType);
            Assert.AreEqual("ShouldNotBeNulled", result[0].ParameterCategory);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Cross-Category Tests

        #region Phase 2 Pre-Pass: Inline N= Extraction

        /**************************************************************/
        /// <summary>
        /// Standalone (n=178) in non-AE TreatmentArm → ArmN=178, TreatmentArm nulled.
        /// Validates the gap where _bracketedNPattern (square brackets only) and
        /// _embeddedNPattern (trailing Name N=xxx) both miss parenthesized standalone N=.
        /// </summary>
        [TestMethod]
        public async Task InlineN_StandaloneParenN_NonAE_ExtractsArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n=178)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(178, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Standalone [N=60] in non-AE TreatmentArm → ArmN=60, TreatmentArm nulled.
        /// Uses PK category where ArmN is Optional (DOSING marks ArmN as NotApplicable,
        /// so Phase 4 would null it).
        /// </summary>
        [TestMethod]
        public async Task InlineN_StandaloneBracketN_NonAE_ExtractsArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("[N=60]", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(60, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// DoseRegimen with embedded (n=963) mid-string → ArmN=963, N stripped from DoseRegimen.
        /// </summary>
        [TestMethod]
        public async Task InlineN_DoseRegimenEmbeddedN_ExtractsAndCleans()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", doseRegimen: "23 mg/day Donezepil Hydrochloride (n=963) %", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(963, result[0].ArmN);
            Assert.IsNotNull(result[0].DoseRegimen);
            Assert.IsFalse(result[0].DoseRegimen!.Contains("963"),
                $"Expected N=963 stripped from DoseRegimen but was '{result[0].DoseRegimen}'");
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:DoseRegimen");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// RawValue containing N= must NOT be touched by the pre-pass (RawValue is excluded).
        /// </summary>
        [TestMethod]
        public async Task InlineN_RawValueWithN_Untouched()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.RawValue = "1.31 (±0.76) (n=25)";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("1.31 (±0.76) (n=25)", result[0].RawValue);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 1 AE row with "Placebo [N=459]" is handled by Phase 1 Rule 11.
        /// The pre-pass should be a no-op since Phase 1 already cleaned TreatmentArm.
        /// ArmN should still be 459 from Phase 1.
        /// </summary>
        [TestMethod]
        public async Task InlineN_Phase1AE_BracketN_HandledByPhase1()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=459]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(459, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When ArmN is already set from Phase 1, the pre-pass should strip N from
        /// other columns but NOT overwrite the existing ArmN value.
        /// </summary>
        [TestMethod]
        public async Task InlineN_ArmNAlreadySet_DoesNotOverwrite()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Phase 1 sets ArmN from TreatmentArm "(N=267)" for AE category
            var obs = createObservation("(N=267)", studyContext: "Placebo", category: "ADVERSE_EVENT");
            obs.DoseRegimen = "50 mg (n=100)";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // ArmN should be 267 from Phase 1 (TreatmentArm), NOT 100 from DoseRegimen
            Assert.AreEqual(267, result[0].ArmN);
            // DoseRegimen N should still be stripped
            if (result[0].DoseRegimen != null)
            {
                Assert.IsFalse(result[0].DoseRegimen!.Contains("100"),
                    $"Expected N=100 stripped from DoseRegimen but was '{result[0].DoseRegimen}'");
            }
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:DoseRegimen");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// StudyContext with embedded (N=50) → ArmN=50, N stripped from StudyContext.
        /// Validates that non-TreatmentArm columns are also scanned.
        /// </summary>
        [TestMethod]
        public async Task InlineN_StudyContextEmbeddedN_ExtractsAndCleans()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", studyContext: "Study 1 (N=50)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(50, result[0].ArmN);
            if (result[0].StudyContext != null)
            {
                Assert.IsFalse(result[0].StudyContext!.Contains("50"),
                    $"Expected N=50 stripped from StudyContext but was '{result[0].StudyContext}'");
            }
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:StudyContext");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Pre-Pass: Inline N= Extraction

        #region Issue 2: Comma-Formatted ArmN Extraction

        /**************************************************************/
        /// <summary>
        /// _nValuePattern matches comma-formatted N values: "(n = 8,506)" → ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task NValuePattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n = 8,506)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted N=8,506 to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _embeddedNPattern matches comma-formatted N values: "Placebo N=8,506" → ArmN=8506.
        /// Uses PK category to exercise the normalizeTreatmentArm path (non-Phase1 category).
        /// </summary>
        [TestMethod]
        public async Task EmbeddedNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo N=8,506", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted N=8,506 to parse as ArmN=8506");
            Assert.AreEqual("Placebo", result[0].TreatmentArm?.Trim(),
                "Expected TreatmentArm to be 'Placebo' after embedded N extraction");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _bracketedNPattern matches comma-formatted N values: "Placebo [N=8,102]" → ArmN=8102.
        /// </summary>
        [TestMethod]
        public async Task BracketedNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=8,102]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8102, result[0].ArmN,
                "Expected comma-formatted [N=8,102] to parse as ArmN=8102");
            Assert.AreEqual("Placebo", result[0].TreatmentArm?.Trim(),
                "Expected TreatmentArm to be 'Placebo' after bracket stripping");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _inlineNPattern matches comma-formatted N values embedded in text:
        /// "CE/MPA (n = 8,506)" → stripped + ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task InlineNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Use DoseRegimen to exercise the inline N pre-pass (Phase 2)
            var obs = createObservation("Placebo", doseRegimen: "CE/MPA (n = 8,506)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted inline (n = 8,506) to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _rawValueTrailingNPattern matches comma-formatted N values:
        /// "2.9 (22%) N=1,234" → ArmN=1234.
        /// </summary>
        [TestMethod]
        public async Task RawValueTrailingN_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.RawValue = "2.9 (22%) N=1,234";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(1234, result[0].ArmN,
                "Expected comma-formatted trailing N=1,234 to parse as ArmN=1234");
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:RawValue");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _standaloneBracketNPattern matches comma-formatted N values:
        /// "[N=8,506]" → ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task StandaloneBracketN_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Put standalone bracket N in DoseRegimen to exercise the inline N pre-pass
            var obs = createObservation("Placebo", doseRegimen: "[N=8,506]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted standalone [N=8,506] to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 2: Comma-Formatted ArmN Extraction

        #region Issue 1: Extract Units from ParameterSubtype

        /**************************************************************/
        /// <summary>
        /// Cmax(pg/mL) → ParameterSubtype="Cmax", Unit="pg/mL".
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_CmaxWithUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be 'Cmax' after unit extraction");
            Assert.AreEqual("pg/mL", result[0].Unit,
                "Expected Unit to be 'pg/mL'");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AUC120(pg·hr/mL) → ParameterSubtype="AUC120", Unit="pg·h/mL" (normalized hr→h).
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_AUC120WithHrVariant()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "AUC120(pg·hr/mL)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC120", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be 'AUC120' after unit extraction");
            Assert.AreEqual("pg·h/mL", result[0].Unit,
                "Expected Unit to be 'pg·h/mL' (normalized from pg·hr/mL)");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cmax(serum, mcg/mL) → ParameterSubtype="Cmax, serum", Unit="mcg/mL".
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_CmaxWithQualifierAndUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(serum, mcg/mL)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax, serum", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be 'Cmax, serum' after qualifier+unit extraction");
            Assert.AreEqual("mcg/mL", result[0].Unit,
                "Expected Unit to be 'mcg/mL'");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Serum AUC0-∞(mcg·hr/mL) → ParameterSubtype="Serum AUC0-∞", Unit="mcg·h/mL".
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_PrefixedAUC()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Serum AUC0-∞(mcg·hr/mL)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Serum AUC0-∞", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be 'Serum AUC0-∞'");
            Assert.AreEqual("mcg·h/mL", result[0].Unit,
                "Expected Unit to be 'mcg·h/mL'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AUC84 (no parentheses) → no change.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_NoParentheses()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "AUC84", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC84", result[0].ParameterSubtype,
                "Expected ParameterSubtype unchanged when no parentheses present");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tmax(hr) → ParameterSubtype="Tmax", Unit="h" (normalized).
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_TmaxHr()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Tmax(hr)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Tmax", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be 'Tmax'");
            Assert.AreEqual("h", result[0].Unit,
                "Expected Unit to be 'h' (normalized from 'hr')");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When Unit is already set, extraction should NOT overwrite it.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_DoesNotOverwriteExistingUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "PK");
            obs.Unit = "ng/mL";  // Pre-existing unit

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng/mL", result[0].Unit,
                "Expected existing Unit to be preserved, not overwritten");
            // ParameterSubtype should still be cleaned up
            Assert.AreEqual("Cmax", result[0].ParameterSubtype,
                "Expected ParameterSubtype to be cleaned even when Unit not set");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Non-PK category (ADVERSE_EVENT) → no extraction, ParameterSubtype unchanged.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_NonPKCategory_Skipped()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax(pg/mL)", result[0].ParameterSubtype,
                "Expected ParameterSubtype unchanged for ADVERSE_EVENT category");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 1: Extract Units from ParameterSubtype

        #region Issue 3: Post-Processing Stage 3.6

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction catches a unit in ParameterSubtype that was missed
        /// by the earlier Standardize phase (simulating Claude restoring it).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_ExtractsUnitMissedByEarlierStage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Simulate an observation that went through Standardize already (no unit extracted)
            // then Claude corrected ParameterSubtype to an extractable form
            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterSubtype = "Cmax(pg/mL)";
            obs.Unit = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("pg/mL", result[0].Unit,
                "Expected PostProcessExtraction to extract unit from ParameterSubtype");
            Assert.AreEqual("Cmax", result[0].ParameterSubtype,
                "Expected ParameterSubtype cleaned after unit extraction");
            assertHasFlag(result[0], "COL_STD:POST_PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction catches a comma-formatted ArmN value in TreatmentArm
        /// that was missed by the earlier Standardize phase (simulating Claude restoring it).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_ExtractsCommaArmNMissedByEarlierStage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Simulate an observation where Claude restored an N= value in DoseRegimen
            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.DoseRegimen = "Drug (n = 8,506)";
            obs.ArmN = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected PostProcessExtraction to extract comma-formatted ArmN");
            assertHasFlag(result[0], "COL_STD:POST_N_STRIPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType from "Count" to "Percentage"
        /// when TreatmentArm contains a "%" indicator and value is &lt;= 100.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_WhenTreatmentArmHasPercentHint()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Any Dose", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 2.5;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType from "Count" to "Percentage"
        /// when ParameterName contains "incidence".
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_WhenParameterNameHasIncidence()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterName = "Incidence of Nausea";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 24.4;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterName");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when SecondaryValueType is already set,
        /// indicating the parser already resolved the type pairing.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenSecondaryValueTypePresent()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Placebo", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 15.0;
            obs.SecondaryValueType = "Count";

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Count", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Count when SecondaryValueType is set");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when PrimaryValue exceeds 100.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenPrimaryValueOver100()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Any Dose", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 150.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Count", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Count when value > 100");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when PrimaryValueType is not "Count".
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenPrimaryValueTypeIsNotCount()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Placebo", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Mean";
            obs.PrimaryValue = 50.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Mean", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Mean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType when ParameterCategory
        /// contains "PROPORTION" (case-insensitive match).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_CaseInsensitiveKeyword()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterCategory = "PROPORTION of patients";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 88.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterCategory");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType when ParameterSubtype
        /// contains "rate of" (two-word keyword match).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_RateOfKeyword()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterSubtype = "Rate of occurrence";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 5.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterSubtype");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 3: Post-Processing Stage 3.6

        #region Issue 4: Confidence Provenance

        /**************************************************************/
        /// <summary>
        /// After Standardize, every processed observation should have a CONFIDENCE:PATTERN: flag
        /// with format CONFIDENCE:PATTERN:{score}:{reason}({correctionCount}).
        /// </summary>
        [TestMethod]
        public async Task Standardize_AppendsConfidencePatternFlag()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParseConfidence = 0.90;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags,
                "Expected ValidationFlags to not be null after standardization");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("CONFIDENCE:PATTERN:"),
                $"Expected CONFIDENCE:PATTERN: flag but got: '{result[0].ValidationFlags}'");

            // Verify format: CONFIDENCE:PATTERN:0.90:clean(0) or similar
            var flagParts = result[0].ValidationFlags.Split("; ")
                .FirstOrDefault(f => f.StartsWith("CONFIDENCE:PATTERN:"));
            Assert.IsNotNull(flagParts, "CONFIDENCE:PATTERN flag should be present");

            var segments = flagParts!.Split(':');
            Assert.AreEqual(4, segments.Length,
                $"Expected 4 colon-separated segments in CONFIDENCE:PATTERN flag but got: '{flagParts}'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 4: Confidence Provenance

        #region Phase 2: AE Dictionary SOC Resolution

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="ColumnStandardizationService"/> with an injected
        /// <see cref="AeParameterCategoryDictionaryService"/> for testing dictionary
        /// integration through the full Standardize pipeline.
        /// </summary>
        private static async Task<(ColumnStandardizationService service, ImportDbContext context, SqliteConnection sentinel)>
            createInitializedServiceWithDictionaryAsync()
        {
            #region implementation

            var (sentinel, connection) = createSharedMemoryDb();
            var context = createImportContext(connection);

            seedDrugNames(connection);

            var mockLogger = new Mock<ILogger<ColumnStandardizationService>>();
            var dictionary = new AeParameterCategoryDictionaryService();
            var service = new ColumnStandardizationService(context, mockLogger.Object, dictionary);
            await service.InitializeAsync();

            return (service, context, sentinel);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dictionary resolves NULL ParameterCategory through the full Standardize pipeline
        /// when ParameterName is a known AE term.
        /// </summary>
        [TestMethod]
        public async Task Standardize_AeDictionary_ResolvesNullCategory()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceWithDictionaryAsync();

            var observations = new List<ParsedObservation>
            {
                new()
                {
                    TextTableID = 1,
                    TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Dyspepsia",
                    ParameterCategory = null,
                    TreatmentArm = "Drug A",
                    ArmN = 100,
                    PrimaryValue = 12.0,
                    PrimaryValueType = "Percentage",
                    ParseConfidence = 1.0,
                    ParseRule = "n_pct",
                    SourceRowSeq = 3,
                    SourceCellSeq = 2
                }
            };

            var result = service.Standardize(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Gastrointestinal Disorders", result[0].ParameterCategory,
                "Dictionary should resolve NULL ParameterCategory for known AE term");
            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("DICT:SOC_RESOLVED"),
                $"Expected DICT:SOC_RESOLVED flag but got: '{result[0].ValidationFlags}'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dictionary does not overwrite an existing non-NULL ParameterCategory.
        /// </summary>
        [TestMethod]
        public async Task Standardize_AeDictionary_DoesNotOverwriteExistingCategory()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceWithDictionaryAsync();

            var observations = new List<ParsedObservation>
            {
                new()
                {
                    TextTableID = 1,
                    TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Anemia",
                    ParameterCategory = "Nervous System Disorders",
                    TreatmentArm = "Drug A",
                    ArmN = 100,
                    PrimaryValue = 5.0,
                    PrimaryValueType = "Percentage",
                    ParseConfidence = 1.0,
                    ParseRule = "n_pct",
                    SourceRowSeq = 3,
                    SourceCellSeq = 2
                }
            };

            var result = service.Standardize(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Nervous System Disorders", result[0].ParameterCategory,
                "Dictionary should not overwrite existing ParameterCategory");
            Assert.IsTrue(
                result[0].ValidationFlags == null ||
                !result[0].ValidationFlags.Contains("DICT:SOC_RESOLVED"),
                "DICT:SOC_RESOLVED flag should not be present when category was not changed");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2: AE Dictionary SOC Resolution
    }
}
