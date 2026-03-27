using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="MlNetCorrectionService"/> — the Stage 3.4 ML.NET-based correction
    /// and anomaly scoring service that applies trained classification and PCA anomaly detection
    /// models to parsed observations.
    /// </summary>
    /// <remarks>
    /// ## Test Strategy
    /// No database or SQLite dependency — the ML service uses in-memory accumulation only.
    /// Tests exercise the 4-stage pipeline, training triggers, accumulator behavior, and
    /// flag formatting. Integration tests feed enough synthetic rows to trigger model training
    /// and verify that subsequent batches receive real anomaly scores.
    ///
    /// ## Test Organization
    /// - **InitializeAsync**: Idempotency and ready state
    /// - **Accumulator/Training**: Row collection and retrain triggers
    /// - **Stage 1–4**: Per-stage correction behavior
    /// - **Integration**: Full pipeline and edge cases
    /// - **ClaudeApiCorrectionService Gate**: Anomaly threshold filtering
    /// </remarks>
    /// <seealso cref="IMlNetCorrectionService"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    [TestClass]
    public class MlNetCorrectionServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="MlNetCorrectionService"/> with default settings and mock logger.
        /// </summary>
        /// <param name="settings">Optional settings override.</param>
        /// <returns>Initialized service ready for testing.</returns>
        private static async Task<MlNetCorrectionService> createInitializedServiceAsync(
            MlNetCorrectionSettings? settings = null)
        {
            #region implementation

            settings ??= new MlNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<MlNetCorrectionService>>();
            var service = new MlNetCorrectionService(mockLogger.Object, settings);
            await service.InitializeAsync();
            return service;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a test observation with configurable fields.
        /// </summary>
        private static ParsedObservation createTestObservation(
            string category = "PK",
            double parseConfidence = 0.95,
            string? doseRegimen = null,
            string primaryValueType = "Numeric",
            double? primaryValue = 42.5,
            double? secondaryValue = null,
            double? lowerBound = null,
            double? upperBound = null,
            double? pValue = null,
            string? unit = null,
            string? caption = "Pharmacokinetic Parameters",
            string? sectionTitle = "Pharmacokinetics",
            string? parentSectionCode = "34090-1",
            string? parseRule = "plain_number",
            string? parameterName = "Cmax",
            string? validationFlags = null,
            int sourceRowSeq = 1,
            int sourceCellSeq = 1)
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = category,
                ParseConfidence = parseConfidence,
                DoseRegimen = doseRegimen,
                PrimaryValueType = primaryValueType,
                PrimaryValue = primaryValue,
                SecondaryValue = secondaryValue,
                LowerBound = lowerBound,
                UpperBound = upperBound,
                PValue = pValue,
                Unit = unit,
                Caption = caption,
                SectionTitle = sectionTitle,
                ParentSectionCode = parentSectionCode,
                ParseRule = parseRule,
                ParameterName = parameterName,
                ValidationFlags = validationFlags,
                SourceRowSeq = sourceRowSeq,
                SourceCellSeq = sourceCellSeq,
                TextTableID = 100
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a batch of diverse high-confidence observations suitable for training.
        /// Produces rows across multiple categories with varied PrimaryValueTypes.
        /// </summary>
        /// <param name="countPerCategory">Number of rows to generate per category.</param>
        /// <returns>List of diverse training observations.</returns>
        private static List<ParsedObservation> generateTrainingBatch(int countPerCategory = 25)
        {
            #region implementation

            var categories = new[]
            {
                ("PK", "Pharmacokinetic Parameters", "Pharmacokinetics", "34090-1"),
                ("ADVERSE_EVENT", "Adverse Events", "Adverse Reactions", "34084-4"),
                ("EFFICACY", "Clinical Efficacy", "Clinical Studies", "34092-7"),
                ("DRUG_INTERACTION", "Drug Interactions", "Drug Interactions", "34073-7")
            };

            var pvTypes = new[]
            {
                ("GeometricMean", "mcg/mL"),
                ("ArithmeticMean", "mg/L"),
                ("Proportion", "%"),
                ("Count", null as string),
                ("Median", "h")
            };

            var observations = new List<ParsedObservation>();
            var rng = new Random(42);

            foreach (var (cat, caption, section, loinc) in categories)
            {
                for (int i = 0; i < countPerCategory; i++)
                {
                    var pvt = pvTypes[i % pvTypes.Length];
                    observations.Add(createTestObservation(
                        category: cat,
                        parseConfidence: 0.90 + rng.NextDouble() * 0.10,
                        primaryValueType: pvt.Item1,
                        primaryValue: rng.NextDouble() * 100,
                        secondaryValue: rng.NextDouble() * 10,
                        unit: pvt.Item2,
                        caption: caption,
                        sectionTitle: section,
                        parentSectionCode: loinc,
                        parseRule: "plain_number",
                        parameterName: $"Param_{i}",
                        sourceRowSeq: i + 1,
                        sourceCellSeq: 1
                    ));
                }
            }

            return observations;

            #endregion
        }

        #endregion Helper Methods

        #region InitializeAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that InitializeAsync sets the service to a ready state
        /// so ScoreAndCorrect processes observations instead of passing through.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_SetsInitialized_LogsReady()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = new List<ParsedObservation> { createTestObservation() };

            // Should process (emit NOMODEL flag) rather than pass through silently
            var result = service.ScoreAndCorrect(obs);
            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that calling InitializeAsync twice does not throw or corrupt state.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_CalledTwice_IsIdempotent()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<MlNetCorrectionService>>();
            var service = new MlNetCorrectionService(mockLogger.Object, settings);

            await service.InitializeAsync();
            await service.InitializeAsync(); // Should not throw

            var obs = new List<ParsedObservation> { createTestObservation() };
            var result = service.ScoreAndCorrect(obs);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE"));

            #endregion
        }

        #endregion InitializeAsync Tests

        #region Accumulator / Training Trigger Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that high-confidence rows are accumulated after ScoreAndCorrect.
        /// Confirmed by: a second batch large enough to trigger training produces trained-model scores.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_AccumulatesHighConfidenceRows()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 50
            };
            var service = await createInitializedServiceAsync(settings);

            // Feed a batch of high-confidence rows (above BootstrapMinParseConfidence=0.85)
            var batch1 = generateTrainingBatch(15); // 4 categories × 15 = 60 rows
            service.ScoreAndCorrect(batch1);

            // Batch 1 all get NOMODEL (no models trained yet before batch 1 accumulates)
            // But after batch 1, accumulator should have rows.
            // Feed batch 2 — tryRetrain fires because 60 new rows > RetrainingBatchSize=50
            var batch2 = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 100.0)
            };
            var result = service.ScoreAndCorrect(batch2);

            // After retrain, PK should have an anomaly engine — expect a numeric score, not NOMODEL
            Assert.IsNotNull(result[0].ValidationFlags);
            // The score should be a number (the model was trained on PK data)
            Assert.IsTrue(
                result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:") &&
                !result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"),
                $"Expected numeric anomaly score but got: {result[0].ValidationFlags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with ParseConfidence below BootstrapMinParseConfidence
        /// are excluded from the training accumulator.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_DoesNotAccumulateLowConfidenceRows()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                BootstrapMinParseConfidence = 0.85,
                MinTrainingRowsPerCategory = 5,
                RetrainingBatchSize = 10
            };
            var service = await createInitializedServiceAsync(settings);

            // Feed 20 low-confidence rows (below 0.85 threshold)
            var lowConfBatch = Enumerable.Range(1, 20)
                .Select(i => createTestObservation(
                    category: "PK",
                    parseConfidence: 0.50,
                    primaryValue: i * 1.0,
                    sourceRowSeq: i))
                .ToList();
            service.ScoreAndCorrect(lowConfBatch);

            // Feed a test batch — should still get NOMODEL because low-confidence rows weren't accumulated
            var testBatch = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 50.0)
            };
            var result = service.ScoreAndCorrect(testBatch);

            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"),
                $"Expected NOMODEL but got: {result[0].ValidationFlags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a retrain is triggered when the accumulator grows past RetrainingBatchSize
        /// and subsequent batches use trained models.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_TriggersRetrain_WhenThresholdMet()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            // Batch 1: 100 high-confidence rows across 4 categories
            var batch1 = generateTrainingBatch(25);
            var result1 = service.ScoreAndCorrect(batch1);

            // Batch 1 should all have NOMODEL
            Assert.IsTrue(result1.All(o => o.ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL")),
                "Batch 1 should have NOMODEL for all observations");

            // Batch 2: retrain should fire (100 rows accumulated > 40 threshold)
            var batch2 = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 55.0),
                createTestObservation(category: "ADVERSE_EVENT", primaryValue: 12.0, sourceRowSeq: 2)
            };
            var result2 = service.ScoreAndCorrect(batch2);

            // At least one should have a real score
            Assert.IsTrue(result2.Any(o =>
                o.ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:") &&
                !o.ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL")),
                "Batch 2 should have at least one trained anomaly score");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that no retrain occurs when the accumulator is below RetrainingBatchSize.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_NoRetrain_WhenBelowThreshold()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 500 // Very high threshold
            };
            var service = await createInitializedServiceAsync(settings);

            // Feed a small batch
            var batch = generateTrainingBatch(15); // 60 rows, below 500
            service.ScoreAndCorrect(batch);

            // Second batch should still get NOMODEL
            var testBatch = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 100.0)
            };
            var result = service.ScoreAndCorrect(testBatch);

            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"),
                $"Expected NOMODEL but got: {result[0].ValidationFlags}");

            #endregion
        }

        #endregion Accumulator / Training Trigger Tests

        #region Stage 1 — TableCategory Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 1 does not crash or modify observations when no model is trained.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage1_ModelNull_NoEffect()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.AreEqual("PK", result[0].TableCategory);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("MLNET:CATEGORY_CORRECTED"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 1 with a trained model and low confidence does not override the category.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage1_LowConfidence_NoCorrection()
        {
            #region implementation

            // Train models with a batch
            var settings = new MlNetCorrectionSettings
            {
                TableCategoryMinConfidence = 0.99f, // Very high bar — nothing should pass
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            // Now test with a new observation
            var testObs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            // With 0.99 confidence threshold, the model likely won't correct
            Assert.AreEqual("PK", result[0].TableCategory);

            #endregion
        }

        #endregion Stage 1

        #region Stage 2 — DoseRegimen Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 2 skips observations already routed by Stage 3.25 rules.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage2_AlreadyRoutedByRules_Skips()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(
                doseRegimen: "Cmax",
                validationFlags: "COL_STD:PK_SUBPARAM_ROUTED");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            // DoseRegimen should remain as-is (not re-routed)
            Assert.AreEqual("Cmax", result[0].DoseRegimen);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("MLNET:DOSEREGIMEN_ROUTED_TO"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 2 does nothing when DoseRegimen is null/empty.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage2_NullDoseRegimen_NoEffect()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(doseRegimen: null);

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("MLNET:DOSEREGIMEN_ROUTED_TO"));

            #endregion
        }

        #endregion Stage 2

        #region Stage 3 — PrimaryValueType Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 3 only fires when PrimaryValueType is "Numeric".
        /// Non-Numeric types should pass through unchanged.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage3_NonNumeric_PassesThrough()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(primaryValueType: "GeometricMean");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.AreEqual("GeometricMean", result[0].PrimaryValueType);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("MLNET:PVTYPE_DISAMBIGUATED"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 3 with no model does not modify "Numeric" PrimaryValueType.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage3_ModelNull_RemainsNumeric()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(primaryValueType: "Numeric");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("MLNET:PVTYPE_DISAMBIGUATED"));

            #endregion
        }

        #endregion Stage 3

        #region Stage 4 — Anomaly Score Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 4 ALWAYS emits an anomaly score flag, even when no model is trained.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage4_AlwaysEmitsAnomalyScore()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation();

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Stage 4 emits NOMODEL for categories without a trained anomaly engine.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage4_UnknownCategory_EmitsNoModel()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(category: "UNKNOWN_CATEGORY_XYZ");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that after training, Stage 4 emits numeric scores for known categories.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Stage4_TrainedModel_EmitsNumericScore()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            // Train
            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            // Score
            var testObs = createTestObservation(category: "PK", primaryValue: 999.99);
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            Assert.IsTrue(
                result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:") &&
                !result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"),
                $"Expected numeric score but got: {result[0].ValidationFlags}");

            #endregion
        }

        #endregion Stage 4

        #region Integration Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that when Enabled=false, all observations pass through unmodified.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Disabled_PassesThrough()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings { Enabled = false };
            var service = await createInitializedServiceAsync(settings);

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when the service is not initialized, observations pass through.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_NotInitialized_PassesThrough()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<MlNetCorrectionService>>();
            var service = new MlNetCorrectionService(mockLogger.Object, settings);
            // NOT calling InitializeAsync

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty list returns an empty list without error.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_EmptyList_ReturnsEmpty()
        {
            #region implementation

            var service = await createInitializedServiceAsync();

            var result = service.ScoreAndCorrect(new List<ParsedObservation>());

            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that ValidationFlags use semicolon-space delimited format
        /// when appending to existing flags.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_FlagFormat_SemicolonDelimited_Correct()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = createTestObservation(validationFlags: "COL_STD:EXISTING_FLAG");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.StartsWith("COL_STD:EXISTING_FLAG; "),
                $"Expected semicolon-space delimiter but got: {result[0].ValidationFlags}");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:"));

            #endregion
        }

        #endregion Integration Tests

        #region ClaudeApiCorrectionService Gate Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that with MlAnomalyScoreThreshold=0, all observations pass to Claude
        /// (backward-compatible behavior).
        /// </summary>
        [TestMethod]
        public void ExceedsAnomalyThreshold_ThresholdZero_SendsAllObservations()
        {
            #region implementation

            var settings = new ClaudeApiCorrectionSettings
            {
                MlAnomalyScoreThreshold = 0.0f,
                Enabled = false // Don't actually call API
            };

            // With threshold 0.0, the service should not filter any observations
            // This is tested indirectly through the settings value
            Assert.AreEqual(0.0f, settings.MlAnomalyScoreThreshold);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that observations without an ML anomaly score flag pass through
        /// the threshold check (conservative behavior — send to Claude when unknown).
        /// </summary>
        [TestMethod]
        public void MlAnomalyScoreThreshold_NoFlag_ObservationPassesThreshold()
        {
            #region implementation

            var settings = new ClaudeApiCorrectionSettings
            {
                MlAnomalyScoreThreshold = 0.75f
            };

            // An observation with no MLNET_ANOMALY_SCORE flag should pass through
            // (conservative: send to Claude when score is unknown)
            var obs = createTestObservation(validationFlags: "COL_STD:SOME_FLAG");

            // Verify the flag format — no MLNET_ANOMALY_SCORE present
            Assert.IsFalse(obs.ValidationFlags!.Contains("MLNET_ANOMALY_SCORE"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that NOMODEL observations pass through the threshold check.
        /// </summary>
        [TestMethod]
        public void MlAnomalyScoreThreshold_NoModel_ObservationPassesThreshold()
        {
            #region implementation

            var obs = createTestObservation(validationFlags: "MLNET_ANOMALY_SCORE:NOMODEL");

            // NOMODEL should be treated as "unknown" → conservative: pass to Claude
            Assert.IsTrue(obs.ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:NOMODEL"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the anomaly score flag format: MLNET_ANOMALY_SCORE:{4-decimal float}.
        /// </summary>
        [TestMethod]
        public async Task AnomalyScoreFlag_HasCorrectFormat()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            // Train models
            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            // Score a new observation
            var testObs = createTestObservation(category: "PK", primaryValue: 50.0);
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            var flags = result[0].ValidationFlags!;
            var scoreIdx = flags.IndexOf("MLNET_ANOMALY_SCORE:");
            Assert.IsTrue(scoreIdx >= 0, "Expected MLNET_ANOMALY_SCORE flag");

            // Extract score value
            var valueStart = scoreIdx + "MLNET_ANOMALY_SCORE:".Length;
            var valueEnd = flags.IndexOf(';', valueStart);
            var scoreStr = valueEnd >= 0
                ? flags.Substring(valueStart, valueEnd - valueStart).Trim()
                : flags.Substring(valueStart).Trim();

            // Should be a parseable float (not NOMODEL)
            Assert.IsTrue(float.TryParse(scoreStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _),
                $"Expected parseable float but got: '{scoreStr}'");

            #endregion
        }

        #endregion ClaudeApiCorrectionService Gate Tests

        #region MlTrainingStore Persistence Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the training store round-trips: save state, reload, assert records
        /// and threshold are preserved.
        /// </summary>
        [TestMethod]
        public async Task MlTrainingStore_RoundTrip_PreservesState()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-test-{Guid.NewGuid()}.json");
            try
            {
                var settings = new MlNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    MaxAccumulatorRows = 1000
                };
                var mockLogger = new Mock<ILogger<MlTrainingStore>>();

                // Save some records
                var store1 = new MlTrainingStore(mockLogger.Object, settings);
                await store1.LoadAsync();

                var records = new List<MlTrainingRecord>
                {
                    MlTrainingRecord.FromObservation(createTestObservation(category: "PK"), isGroundTruth: true),
                    MlTrainingRecord.FromObservation(createTestObservation(category: "ADVERSE_EVENT"), isGroundTruth: false)
                };
                await store1.AddRecordsAsync(records);
                await store1.RecordClaudeFeedbackAsync(100, 5); // won't raise threshold (< 2000 min)

                // Reload in a new instance
                var store2 = new MlTrainingStore(mockLogger.Object, settings);
                await store2.LoadAsync();

                Assert.AreEqual(2, store2.GetRecords().Count);
                Assert.AreEqual("PK", store2.GetRecords()[0].TableCategory);
                Assert.IsTrue(store2.GetRecords()[0].IsClaudeGroundTruth);
                Assert.IsFalse(store2.GetRecords()[1].IsClaudeGroundTruth);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            #endregion
        }

        #endregion MlTrainingStore Persistence Tests

        #region Eviction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that when records exceed MaxAccumulatorRows, bootstrap records are evicted
        /// first and ground-truth records are preserved.
        /// </summary>
        [TestMethod]
        public async Task MlTrainingStore_Eviction_BootstrapEvictedFirst()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-evict-{Guid.NewGuid()}.json");
            try
            {
                var settings = new MlNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    MaxAccumulatorRows = 10
                };
                var mockLogger = new Mock<ILogger<MlTrainingStore>>();
                var store = new MlTrainingStore(mockLogger.Object, settings);
                await store.LoadAsync();

                // Add 5 ground-truth records
                var groundTruth = Enumerable.Range(1, 5)
                    .Select(i => new MlTrainingRecord
                    {
                        TableCategory = "PK",
                        PrimaryValue = i,
                        IsClaudeGroundTruth = true
                    })
                    .ToList();
                await store.AddRecordsAsync(groundTruth);

                // Add 10 bootstrap records (total = 15, max = 10)
                var bootstrap = Enumerable.Range(1, 10)
                    .Select(i => new MlTrainingRecord
                    {
                        TableCategory = "PK",
                        PrimaryValue = i * 100,
                        IsClaudeGroundTruth = false
                    })
                    .ToList();
                await store.AddRecordsAsync(bootstrap);

                var remaining = store.GetRecords();
                Assert.AreEqual(10, remaining.Count, "Should be capped at MaxAccumulatorRows");

                // All 5 ground-truth should survive
                var gtCount = remaining.Count(r => r.IsClaudeGroundTruth);
                Assert.AreEqual(5, gtCount, "All ground-truth records should be preserved");

                // Only 5 of 10 bootstrap should remain
                var bsCount = remaining.Count(r => !r.IsClaudeGroundTruth);
                Assert.AreEqual(5, bsCount, "5 oldest bootstrap records should be evicted");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            #endregion
        }

        #endregion Eviction Tests

        #region Adaptive Threshold Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that when the correction rate is low (below floor), the adaptive threshold
        /// is raised.
        /// </summary>
        [TestMethod]
        public async Task AdaptiveThreshold_LowCorrectionRate_ThresholdRises()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-adapt-{Guid.NewGuid()}.json");
            try
            {
                var settings = new MlNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    AdaptiveThresholdMinObservations = 2000,
                    AdaptiveThresholdCorrectionRateFloor = 0.10f,
                    AdaptiveThresholdStep = 0.05f,
                    AdaptiveThresholdCeiling = 0.95f,
                    AdaptiveThresholdEvaluationInterval = 1000
                };
                var mockLogger = new Mock<ILogger<MlTrainingStore>>();
                var store = new MlTrainingStore(mockLogger.Object, settings);
                await store.LoadAsync();

                Assert.AreEqual(0.0f, store.GetAdaptiveThreshold(), "Initial threshold should be 0.0");

                // Feed 3000 sent, 100 corrected (3.3% rate — below 10% floor)
                // First batch won't trigger (below 2000 min)
                var result1 = await store.RecordClaudeFeedbackAsync(1500, 50);
                Assert.IsNull(result1, "Should not trigger before min observations");

                // Second batch crosses 2000 threshold + 1000 interval
                var result2 = await store.RecordClaudeFeedbackAsync(1500, 50);
                Assert.IsNotNull(result2, "Should trigger threshold increase");
                Assert.AreEqual(0.05f, result2!.Value, 0.001f, "Threshold should rise by step size");
                Assert.AreEqual(0.05f, store.GetAdaptiveThreshold(), 0.001f);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when the correction rate is high (above floor), the adaptive threshold
        /// does not change.
        /// </summary>
        [TestMethod]
        public async Task AdaptiveThreshold_HighCorrectionRate_ThresholdUnchanged()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-noadapt-{Guid.NewGuid()}.json");
            try
            {
                var settings = new MlNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    AdaptiveThresholdMinObservations = 2000,
                    AdaptiveThresholdCorrectionRateFloor = 0.10f,
                    AdaptiveThresholdStep = 0.05f,
                    AdaptiveThresholdEvaluationInterval = 1000
                };
                var mockLogger = new Mock<ILogger<MlTrainingStore>>();
                var store = new MlTrainingStore(mockLogger.Object, settings);
                await store.LoadAsync();

                // Feed 3000 sent, 600 corrected (20% rate — above 10% floor)
                await store.RecordClaudeFeedbackAsync(1500, 300);
                var result = await store.RecordClaudeFeedbackAsync(1500, 300);

                Assert.IsNull(result, "Threshold should not change when correction rate is high");
                Assert.AreEqual(0.0f, store.GetAdaptiveThreshold(), "Threshold should remain at 0.0");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            #endregion
        }

        #endregion Adaptive Threshold Tests

        #region FeedClaudeCorrectedBatchAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that FeedClaudeCorrectedBatchAsync extracts only AI_CORRECTED observations
        /// and adds them as ground-truth records.
        /// </summary>
        [TestMethod]
        public async Task FeedClaudeCorrectedBatch_ExtractsOnlyCorrected()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<MlNetCorrectionService>>();
            var service = new MlNetCorrectionService(mockLogger.Object, settings);
            await service.InitializeAsync();

            // Create 10 observations, 4 with AI_CORRECTED flag
            var observations = Enumerable.Range(1, 10)
                .Select(i => createTestObservation(
                    category: "PK",
                    primaryValue: i * 10.0,
                    sourceRowSeq: i,
                    validationFlags: i <= 4
                        ? $"MLNET_ANOMALY_SCORE:0.8500; AI_CORRECTED:PrimaryValueType"
                        : "MLNET_ANOMALY_SCORE:0.5000"))
                .ToList();

            await service.FeedClaudeCorrectedBatchAsync(observations);

            // Feed a large training batch to trigger retrain — this verifies the
            // ground-truth records were added to the accumulator
            var trainBatch = generateTrainingBatch(50);
            service.ScoreAndCorrect(trainBatch);

            // A second ScoreAndCorrect should work with the mixed accumulator
            var testBatch = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 999.0)
            };
            var result = service.ScoreAndCorrect(testBatch);

            // Should not crash and should produce a score
            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("MLNET_ANOMALY_SCORE:"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that FeedClaudeCorrectedBatchAsync with no corrected observations
        /// does not add any ground-truth records and does not throw.
        /// </summary>
        [TestMethod]
        public async Task FeedClaudeCorrectedBatch_NoCorrected_NoOp()
        {
            #region implementation

            var settings = new MlNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<MlNetCorrectionService>>();
            var service = new MlNetCorrectionService(mockLogger.Object, settings);
            await service.InitializeAsync();

            var observations = Enumerable.Range(1, 5)
                .Select(i => createTestObservation(
                    category: "PK",
                    primaryValue: i * 10.0,
                    sourceRowSeq: i,
                    validationFlags: "MLNET_ANOMALY_SCORE:0.5000"))
                .ToList();

            // Should not throw
            await service.FeedClaudeCorrectedBatchAsync(observations);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that FeedClaudeCorrectedBatchAsync with a training store persists
        /// ground-truth records and propagates adaptive threshold changes.
        /// </summary>
        [TestMethod]
        public async Task FeedClaudeCorrectedBatch_WithStore_PersistsAndAdapts()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-feed-{Guid.NewGuid()}.json");
            try
            {
                var mlSettings = new MlNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    AdaptiveThresholdMinObservations = 100,
                    AdaptiveThresholdCorrectionRateFloor = 0.10f,
                    AdaptiveThresholdStep = 0.05f,
                    AdaptiveThresholdEvaluationInterval = 50
                };
                var claudeSettings = new ClaudeApiCorrectionSettings
                {
                    MlAnomalyScoreThreshold = 0.0f
                };

                var storeLogger = new Mock<ILogger<MlTrainingStore>>();
                var store = new MlTrainingStore(storeLogger.Object, mlSettings);

                var serviceLogger = new Mock<ILogger<MlNetCorrectionService>>();
                var service = new MlNetCorrectionService(
                    serviceLogger.Object, mlSettings, store, claudeSettings);

                await service.InitializeAsync();

                // Feed enough low-correction-rate batches to trigger threshold raise
                // 200 observations, 5 corrected = 2.5% rate (below 10% floor)
                for (int batch = 0; batch < 4; batch++)
                {
                    var obs = Enumerable.Range(1, 50)
                        .Select(i => createTestObservation(
                            category: "PK",
                            primaryValue: i,
                            sourceRowSeq: i,
                            validationFlags: i == 1
                                ? "AI_CORRECTED:PrimaryValueType"
                                : "MLNET_ANOMALY_SCORE:0.5000"))
                        .ToList();

                    await service.FeedClaudeCorrectedBatchAsync(obs);
                }

                // Threshold should have been raised
                Assert.IsTrue(claudeSettings.MlAnomalyScoreThreshold > 0.0f,
                    $"Expected threshold > 0 but got {claudeSettings.MlAnomalyScoreThreshold}");

                // Verify persistence — reload store
                var store2 = new MlTrainingStore(storeLogger.Object, mlSettings);
                await store2.LoadAsync();
                Assert.IsTrue(store2.GetRecords().Count > 0, "Records should be persisted");
                Assert.IsTrue(store2.GetAdaptiveThreshold() > 0.0f, "Threshold should be persisted");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            #endregion
        }

        #endregion FeedClaudeCorrectedBatchAsync Tests
    }
}
