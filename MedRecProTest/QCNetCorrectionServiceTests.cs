using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="QCNetCorrectionService"/> — the Stage 3.4 ML.NET-based correction
    /// service that applies trained Stage 1/2/3 classifiers to parsed observations and
    /// delegates downstream Claude forwarding to <see cref="IParseQualityService"/>.
    /// </summary>
    /// <remarks>
    /// ## Test Strategy
    /// No database or SQLite dependency — the ML service uses in-memory accumulation only.
    /// Tests exercise the three-stage classifier pipeline, training triggers, accumulator
    /// behavior, and flag formatting. Parse-quality behaviour itself is covered in
    /// <c>ParseQualityServiceTests</c>; these tests only verify that a registered
    /// <see cref="IParseQualityService"/> is invoked and emits an
    /// <c>QC_PARSE_QUALITY</c> flag.
    ///
    /// ## Stage 4 Retirement
    /// The former Stage 4 anomaly-scoring test regions (per-key, UnifiedGlobal, adaptive
    /// threshold, validation-guard, degrade-fallback) were deleted on 2026-04-24 along with
    /// the anomaly pipeline itself.
    /// </remarks>
    /// <seealso cref="IQCNetCorrectionService"/>
    /// <seealso cref="QCNetCorrectionSettings"/>
    [TestClass]
    public class QCNetCorrectionServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="QCNetCorrectionService"/> with default settings and mock logger.
        /// </summary>
        /// <param name="settings">Optional settings override.</param>
        /// <param name="includeQualityService">When true (default), injects a stub
        /// <see cref="IParseQualityService"/> so tests can assert on
        /// <c>QC_PARSE_QUALITY</c> emission without depending on the full rule engine.</param>
        /// <returns>Initialized service ready for testing.</returns>
        private static async Task<QCNetCorrectionService> createInitializedServiceAsync(
            QCNetCorrectionSettings? settings = null,
            bool includeQualityService = true)
        {
            #region implementation

            settings ??= new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            IParseQualityService? qualityService = includeQualityService
                ? new StubParseQualityService()
                : null;
            var service = new QCNetCorrectionService(
                mockLogger.Object,
                settings,
                trainingStore: null,
                parseQualityService: qualityService);
            await service.InitializeAsync();
            return service;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deterministic stub <see cref="IParseQualityService"/> used by tests — returns a
        /// constant score and an empty reason list regardless of input. Allows tests to assert
        /// that the service is wired without depending on the real rule engine's behavior.
        /// </summary>
        private sealed class StubParseQualityService : IParseQualityService
        {
            public ParseQualityScore Evaluate(ParsedObservation obs)
            {
                return new ParseQualityScore(Score: 0.9f, Reasons: new List<string>());
            }
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
            int sourceCellSeq = 1,
            string? secondaryValueType = null,
            string? unii = null)
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
                SecondaryValueType = secondaryValueType,
                UNII = unii,
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
        /// Produces rows across multiple categories with varied PrimaryValueTypes and SecondaryValueTypes.
        /// </summary>
        /// <param name="countPerCategory">Number of rows to generate per category.</param>
        /// <returns>List of diverse training observations.</returns>
        private static List<ParsedObservation> generateTrainingBatch(int countPerCategory = 25)
        {
            #region implementation

            var categories = new[]
            {
                ("PK", "Pharmacokinetic Parameters", "Pharmacokinetics", "34090-1", "ABC123"),
                ("ADVERSE_EVENT", "Adverse Events", "Adverse Reactions", "34084-4", "DEF456"),
                ("EFFICACY", "Clinical Efficacy", "Clinical Studies", "34092-7", "GHI789"),
                ("DRUG_INTERACTION", "Drug Interactions", "Drug Interactions", "34073-7", "JKL012")
            };

            var pvTypes = new[]
            {
                ("GeometricMean", "mcg/mL", (string?)null),
                ("ArithmeticMean", "mg/L", "SD"),
                ("Proportion", "%", "Count"),
                ("Count", null as string, (string?)null),
                ("Median", "h", "CV")
            };

            var observations = new List<ParsedObservation>();
            var rng = new Random(42);

            foreach (var (cat, caption, section, loinc, unii) in categories)
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
                        sourceCellSeq: 1,
                        secondaryValueType: pvt.Item3,
                        unii: unii
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
        /// Verifies that InitializeAsync sets the service to a ready state so ScoreAndCorrect
        /// emits a parse-quality flag on each observation rather than passing through.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_SetsInitialized_EmitsParseQuality()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var obs = new List<ParsedObservation> { createTestObservation() };

            var result = service.ScoreAndCorrect(obs);
            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"),
                $"Expected QC_PARSE_QUALITY flag after initialization but got: {result[0].ValidationFlags}");

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

            var settings = new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: new StubParseQualityService());

            await service.InitializeAsync();
            await service.InitializeAsync(); // Should not throw

            var obs = new List<ParsedObservation> { createTestObservation() };
            var result = service.ScoreAndCorrect(obs);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"));

            #endregion
        }

        #endregion InitializeAsync Tests

        #region Accumulator / Training Trigger Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that high-confidence rows are accumulated after ScoreAndCorrect and that
        /// subsequent batches do not crash the pipeline. With Stage 4 retired, we no longer
        /// have a cheap "model trained?" visible signal; the parse-quality flag emits regardless
        /// so we assert only that processing continues across batches without error.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_AccumulatesHighConfidenceRows_AndContinuesAcrossBatches()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 5,
                RetrainingBatchSize = 50
            };
            var service = await createInitializedServiceAsync(settings);

            var batch1 = generateTrainingBatch(25); // 100 high-confidence rows
            var result1 = service.ScoreAndCorrect(batch1);
            Assert.AreEqual(100, result1.Count);

            // Second batch should accept new observations without crashing, and each should
            // still get a parse-quality flag.
            var batch2 = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValueType: "GeometricMean", primaryValue: 100.0, unii: "ABC123")
            };
            var result2 = service.ScoreAndCorrect(batch2);
            Assert.IsTrue(result2[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that observations with low ParseConfidence still receive a
        /// parse-quality flag (the gate is independent of the training-accumulator filter).
        /// The BootstrapMinParseConfidence threshold only governs accumulator admission, not
        /// pipeline output.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_LowConfidence_StillEmitsParseQualityFlag()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                BootstrapMinParseConfidence = 0.85,
                MinTrainingRowsPerCategory = 5,
                RetrainingBatchSize = 10
            };
            var service = await createInitializedServiceAsync(settings);

            var lowConfBatch = Enumerable.Range(1, 20)
                .Select(i => createTestObservation(
                    category: "PK",
                    parseConfidence: 0.50,
                    primaryValue: i * 1.0,
                    sourceRowSeq: i))
                .ToList();
            var result = service.ScoreAndCorrect(lowConfBatch);

            foreach (var obs in result)
            {
                Assert.IsTrue(obs.ValidationFlags!.Contains("QC_PARSE_QUALITY:"),
                    $"Expected QC_PARSE_QUALITY on every observation regardless of confidence.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the service survives repeated invocations with accumulator growth
        /// past the RetrainingBatchSize trigger. Former test asserted Stage 4 numeric-score
        /// emergence after training; parse-quality gate removes the need for that signal.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_RetrainTrigger_DoesNotThrow()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                MinTrainingRowsPerCategory = 5,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            var batch1 = generateTrainingBatch(25);
            var result1 = service.ScoreAndCorrect(batch1);
            Assert.AreEqual(100, result1.Count);

            var batch2 = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValueType: "GeometricMean", primaryValue: 55.0, unii: "ABC123"),
                createTestObservation(category: "ADVERSE_EVENT", primaryValueType: "GeometricMean", primaryValue: 12.0, sourceRowSeq: 2, unii: "DEF456")
            };
            var result2 = service.ScoreAndCorrect(batch2);
            Assert.AreEqual(2, result2.Count);
            foreach (var obs in result2)
            {
                Assert.IsTrue(obs.ValidationFlags!.Contains("QC_PARSE_QUALITY:"));
            }

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
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC:CATEGORY_CORRECTED"));

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

            var settings = new QCNetCorrectionSettings
            {
                TableCategoryMinConfidence = 0.99f, // Very high bar — nothing should pass
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40
            };
            var service = await createInitializedServiceAsync(settings);

            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            var testObs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

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

            Assert.AreEqual("Cmax", result[0].DoseRegimen);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC:DOSEREGIMEN_ROUTED_TO"));

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
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC:DOSEREGIMEN_ROUTED_TO"));

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
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC:PVTYPE_DISAMBIGUATED"));

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
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC:PVTYPE_DISAMBIGUATED"));

            #endregion
        }

        #endregion Stage 3

        #region Integration Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that when Enabled=false, all observations pass through unmodified.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_Disabled_PassesThrough()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings { Enabled = false };
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

            var settings = new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            var service = new QCNetCorrectionService(mockLogger.Object, settings);
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
            Assert.IsTrue(result[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"));

            #endregion
        }

        #endregion Integration Tests

        #region Parse-Quality Gate Integration

        /**************************************************************/
        /// <summary>
        /// Verifies that the registered <see cref="IParseQualityService"/> is invoked during
        /// <c>ScoreAndCorrect</c> and emits the primary <c>QC_PARSE_QUALITY:{score}</c>
        /// flag on every observation.
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_Registered_EmitsFlagOnEveryObservation()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var batch = generateTrainingBatch(5);

            var result = service.ScoreAndCorrect(batch);

            foreach (var obs in result)
            {
                var flags = obs.ValidationFlags ?? string.Empty;
                Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:"),
                    $"row={obs.SourceRowSeq}: expected QC_PARSE_QUALITY on every observation");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when no quality service is registered, the service still processes
        /// observations without crashing — no parse-quality flag is emitted, but the rest of
        /// the pipeline (classifier stages, CONFIDENCE flag) runs as normal.
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_NotRegistered_PipelineStillRuns()
        {
            #region implementation

            var service = await createInitializedServiceAsync(includeQualityService: false);
            var obs = createTestObservation();

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsFalse(result[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"),
                "No parse-quality service registered — flag should not appear");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("CONFIDENCE:ML:"),
                "Pipeline should still emit the CONFIDENCE:ML provenance flag");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when the quality service reports penalty reasons, the companion
        /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{list}</c> flag is emitted alongside the
        /// numeric score flag.
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_ReasonsPopulated_EmitsReviewReasonsFlag()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            IParseQualityService qualityService = new ReasonReturningStubQualityService();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: qualityService);
            await service.InitializeAsync();

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:0.5000"),
                $"Expected numeric score flag but got: {flags}");
            Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:REVIEW_REASONS:PrimaryValueNull|ParameterNameNull"),
                $"Expected REVIEW_REASONS flag but got: {flags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stub that returns a non-empty reasons list so the REVIEW_REASONS flag emission
        /// can be asserted.
        /// </summary>
        private sealed class ReasonReturningStubQualityService : IParseQualityService
        {
            public ParseQualityScore Evaluate(ParsedObservation obs)
            {
                return new ParseQualityScore(
                    Score: 0.5f,
                    Reasons: new List<string> { "PrimaryValueNull", "ParameterNameNull" });
            }
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the threshold gate: when the quality service reports penalty reasons
        /// but the score is AT OR ABOVE <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/>,
        /// the REVIEW_REASONS flag is suppressed. The numeric score still emits for audit.
        /// Rationale: rows above threshold are not forwarded to Claude, so the reason list
        /// has no operational effect and would only pollute aggregate reason breakdowns.
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_ScoreAboveThreshold_ReviewReasonsSuppressed()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            var claudeSettings = new ClaudeApiCorrectionSettings { ClaudeReviewQualityThreshold = 0.75f };
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            // Stub returns score 0.9 (above 0.75 threshold) with a non-empty reasons list.
            IParseQualityService qualityService = new AboveThresholdReasonStubQualityService();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: qualityService,
                claudeSettings: claudeSettings);
            await service.InitializeAsync();

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:0.9000"),
                $"Numeric score flag must still emit; got: {flags}");
            Assert.IsFalse(flags.Contains("QC_PARSE_QUALITY:REVIEW_REASONS"),
                $"REVIEW_REASONS must be suppressed when score >= threshold; got: {flags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the threshold gate boundary: when the score exactly matches
        /// <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/>, the
        /// REVIEW_REASONS flag is suppressed (the gate is <c>score &lt; threshold</c>,
        /// strictly less).
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_ScoreExactlyAtThreshold_ReviewReasonsSuppressed()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            var claudeSettings = new ClaudeApiCorrectionSettings { ClaudeReviewQualityThreshold = 0.75f };
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            IParseQualityService qualityService = new ExactThresholdReasonStubQualityService();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: qualityService,
                claudeSettings: claudeSettings);
            await service.InitializeAsync();

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC_PARSE_QUALITY:REVIEW_REASONS"),
                $"REVIEW_REASONS must be suppressed when score == threshold; got: {flags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a custom Claude threshold is honored for reason emission. Raising
        /// the threshold to 0.90 means a score of 0.8 (which skipped Claude under the 0.75
        /// default) now emits REVIEW_REASONS because it IS below 0.90.
        /// </summary>
        [TestMethod]
        public async Task ParseQualityService_CustomThreshold_GatesReasonEmission()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            var claudeSettings = new ClaudeApiCorrectionSettings { ClaudeReviewQualityThreshold = 0.90f };
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            IParseQualityService qualityService = new AboveDefaultThresholdReasonStubQualityService();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: qualityService,
                claudeSettings: claudeSettings);
            await service.InitializeAsync();

            var obs = createTestObservation();
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:REVIEW_REASONS"),
                $"REVIEW_REASONS must emit when score < custom threshold (0.8 < 0.9); got: {flags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>Stub: score 0.9 (above default 0.75 threshold) with non-empty reasons.</summary>
        private sealed class AboveThresholdReasonStubQualityService : IParseQualityService
        {
            public ParseQualityScore Evaluate(ParsedObservation obs)
                => new(Score: 0.9f, Reasons: new List<string> { "SoftRepair:PVT_MIGRATED" });
        }

        /**************************************************************/
        /// <summary>Stub: score exactly 0.75 (boundary) with non-empty reasons.</summary>
        private sealed class ExactThresholdReasonStubQualityService : IParseQualityService
        {
            public ParseQualityScore Evaluate(ParsedObservation obs)
                => new(Score: 0.75f, Reasons: new List<string> { "SoftRepair:PVT_MIGRATED" });
        }

        /**************************************************************/
        /// <summary>Stub: score 0.8 (above default 0.75 threshold, below 0.9 custom threshold).</summary>
        private sealed class AboveDefaultThresholdReasonStubQualityService : IParseQualityService
        {
            public ParseQualityScore Evaluate(ParsedObservation obs)
                => new(Score: 0.8f, Reasons: new List<string> { "SoftRepair:PVT_MIGRATED" });
        }

        #endregion Parse-Quality Gate Integration

        #region QCTrainingStore Eviction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that when records exceed MaxAccumulatorRows, bootstrap records are evicted
        /// first and ground-truth records are preserved.
        /// </summary>
        [TestMethod]
        public async Task QCTrainingStore_Eviction_BootstrapEvictedFirst()
        {
            #region implementation

            var tempPath = Path.Combine(Path.GetTempPath(), $"ml-store-evict-{Guid.NewGuid()}.json");
            try
            {
                var settings = new QCNetCorrectionSettings
                {
                    TrainingStoreFilePath = tempPath,
                    MaxAccumulatorRows = 10
                };
                var mockLogger = new Mock<ILogger<QCTrainingStore>>();
                var store = new QCTrainingStore(mockLogger.Object, settings);
                await store.LoadAsync();

                // Add 5 ground-truth records
                var groundTruth = Enumerable.Range(1, 5)
                    .Select(i => new QCTrainingRecord
                    {
                        TableCategory = "PK",
                        PrimaryValue = i,
                        IsClaudeGroundTruth = true
                    })
                    .ToList();
                await store.AddRecordsAsync(groundTruth);

                // Add 10 bootstrap records (total = 15, max = 10)
                var bootstrap = Enumerable.Range(1, 10)
                    .Select(i => new QCTrainingRecord
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

        #endregion QCTrainingStore Eviction Tests

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

            var settings = new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            var service = new QCNetCorrectionService(
                mockLogger.Object, settings,
                trainingStore: null,
                parseQualityService: new StubParseQualityService());
            await service.InitializeAsync();

            var observations = Enumerable.Range(1, 10)
                .Select(i => createTestObservation(
                    category: "PK",
                    primaryValue: i * 10.0,
                    sourceRowSeq: i,
                    validationFlags: i <= 4
                        ? $"QC_PARSE_QUALITY:0.5000; AI_CORRECTED:PrimaryValueType"
                        : "QC_PARSE_QUALITY:0.9000"))
                .ToList();

            await service.FeedClaudeCorrectedBatchAsync(observations);

            // Feed a large training batch to trigger retrain — this verifies the
            // ground-truth records were added to the accumulator.
            var trainBatch = generateTrainingBatch(50);
            service.ScoreAndCorrect(trainBatch);

            // A second ScoreAndCorrect should work with the mixed accumulator.
            var testBatch = new List<ParsedObservation>
            {
                createTestObservation(category: "PK", primaryValue: 999.0)
            };
            var result = service.ScoreAndCorrect(testBatch);

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("QC_PARSE_QUALITY:"));

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

            var settings = new QCNetCorrectionSettings();
            var mockLogger = new Mock<ILogger<QCNetCorrectionService>>();
            var service = new QCNetCorrectionService(mockLogger.Object, settings);
            await service.InitializeAsync();

            var observations = Enumerable.Range(1, 5)
                .Select(i => createTestObservation(
                    category: "PK",
                    primaryValue: i * 10.0,
                    sourceRowSeq: i,
                    validationFlags: "QC_PARSE_QUALITY:0.9000"))
                .ToList();

            // Should not throw
            await service.FeedClaudeCorrectedBatchAsync(observations);

            #endregion
        }

        #endregion FeedClaudeCorrectedBatchAsync Tests

        #region Issue 4: Confidence Provenance

        /**************************************************************/
        /// <summary>
        /// After ScoreAndCorrect, every observation should have a CONFIDENCE:ML: provenance flag
        /// with format CONFIDENCE:ML:{score}:{correctionLabel}.
        /// </summary>
        [TestMethod]
        public async Task ScoreAndCorrect_AppendsConfidenceMlFlag()
        {
            #region implementation

            var service = await createInitializedServiceAsync();

            var obs = createTestObservation(
                category: "PK",
                parseConfidence: 0.85);

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags,
                "Expected ValidationFlags to not be null after ML pipeline");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("CONFIDENCE:ML:"),
                $"Expected CONFIDENCE:ML: flag but got: '{result[0].ValidationFlags}'");

            #endregion
        }

        #endregion Issue 4: Confidence Provenance

        #region R9 — Per-Stage Enable Toggles + Shadow Mode

        /**************************************************************/
        /// <summary>
        /// R9 — With <see cref="QCNetCorrectionSettings.EnableStage1TableCategoryCorrection"/>
        /// false AND <see cref="QCNetCorrectionSettings.EnableStage1ShadowMode"/> false,
        /// Stage 1 is fully silent: no <c>QC:CATEGORY_CORRECTED</c> flag, no
        /// <c>QC:CATEGORY_SHADOW</c> flag, and <c>TableCategory</c> is never mutated
        /// — even when a trained model would have produced a high-confidence prediction.
        /// </summary>
        [TestMethod]
        public async Task R9_Stage1_BothTogglesOff_NoFlagAndNoMutation()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage1TableCategoryCorrection = false,
                EnableStage1ShadowMode = false,
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40,
                TableCategoryMinConfidence = 0.01f  // Very low so predictions WOULD fire if not gated
            };
            var service = await createInitializedServiceAsync(settings);

            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            var testObs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            Assert.AreEqual("PK", result[0].TableCategory,
                "TableCategory must never be mutated when Stage 1 is fully disabled.");
            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:CATEGORY_CORRECTED"),
                "No CATEGORY_CORRECTED flag may be emitted when Stage 1 is disabled.");
            Assert.IsFalse(flags.Contains("QC:CATEGORY_SHADOW"),
                "No CATEGORY_SHADOW flag may be emitted when shadow mode is also disabled.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 — With Stage 1 correction disabled but shadow mode ON (the R9 default),
        /// the classifier still runs and emits <c>QC:CATEGORY_SHADOW</c> flags for
        /// predictions that WOULD have triggered a correction. <c>TableCategory</c>
        /// is never mutated.
        /// </summary>
        [TestMethod]
        public async Task R9_Stage1_ShadowModeOn_EmitsShadowFlagWithoutMutation()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage1TableCategoryCorrection = false,
                EnableStage1ShadowMode = true,
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40,
                TableCategoryMinConfidence = 0.01f
            };
            var service = await createInitializedServiceAsync(settings);

            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            var testObs = createTestObservation(
                category: "PK",
                caption: "Serious Adverse Events — Pooled Safety Analysis",
                sectionTitle: "Adverse Reactions",
                parentSectionCode: "34084-4");

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            Assert.AreEqual("PK", result[0].TableCategory,
                "Shadow mode must never mutate TableCategory.");

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:CATEGORY_CORRECTED"),
                "CATEGORY_CORRECTED must NOT be emitted when correction is disabled.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 — With Stage 1 correction explicitly enabled, the classical
        /// <c>QC:CATEGORY_CORRECTED</c> path runs and may mutate TableCategory.
        /// </summary>
        [TestMethod]
        public async Task R9_Stage1_CorrectionEnabled_CanEmitCorrectedFlag()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage1TableCategoryCorrection = true,
                MinTrainingRowsPerCategory = 10,
                RetrainingBatchSize = 40,
                TableCategoryMinConfidence = 0.99f
            };
            var service = await createInitializedServiceAsync(settings);

            var trainBatch = generateTrainingBatch(25);
            service.ScoreAndCorrect(trainBatch);

            var testObs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:CATEGORY_SHADOW"),
                "No SHADOW flag should be emitted when correction is actively enabled.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 — Disabling Stage 2 (DoseRegimen routing) stops
        /// <c>QC:DOSEREGIMEN_ROUTED</c> flag emission.
        /// </summary>
        [TestMethod]
        public async Task R9_Stage2_Disabled_NoDoseRegimenRoutedFlag()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage2DoseRegimenRouting = false
            };
            var service = await createInitializedServiceAsync(settings);

            var testObs = createTestObservation(
                category: "PK",
                doseRegimen: "Adult Healthy Subjects given 50 mg oral QD");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:DOSEREGIMEN_ROUTED"),
                "Stage 2 disabled — no DOSEREGIMEN_ROUTED flag should be emitted.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 — Disabling Stage 3 (PrimaryValueType disambiguation) stops
        /// <c>QC:PVTYPE_DISAMBIGUATED</c> flag emission.
        /// </summary>
        [TestMethod]
        public async Task R9_Stage3_Disabled_NoPvTypeDisambiguatedFlag()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage3PrimaryValueTypeDisambiguation = false
            };
            var service = await createInitializedServiceAsync(settings);

            var testObs = createTestObservation(category: "PK", primaryValueType: "Numeric");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:PVTYPE_DISAMBIGUATED"),
                "Stage 3 disabled — no PVTYPE_DISAMBIGUATED flag should be emitted.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 — Default settings (Stage 1 off with shadow on, Stages 2/3 on) produce no
        /// CATEGORY_CORRECTED flags but still produce the parse-quality flag. Regression
        /// guard on the R9 default posture after the 2026-04-24 Stage 4 retirement.
        /// </summary>
        [TestMethod]
        public async Task R9_DefaultSettings_Stage1Off_ParseQualityStillEmits()
        {
            #region implementation

            var service = await createInitializedServiceAsync();
            var testObs = createTestObservation(category: "PK");
            var result = service.ScoreAndCorrect(new List<ParsedObservation> { testObs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:CATEGORY_CORRECTED"),
                "R9 default: Stage 1 must not emit CATEGORY_CORRECTED without opt-in.");
            Assert.IsTrue(flags.Contains("QC_PARSE_QUALITY:"),
                "R9 default: parse-quality gate must still emit a flag on every observation.");

            #endregion
        }

        #endregion R9 — Per-Stage Enable Toggles + Shadow Mode

        #region PR #4 — Stage 2 shadow mode

        /**************************************************************/
        /// <summary>
        /// PR #4 — default settings keep Stage 2 in active-correction mode (shadow off).
        /// </summary>
        [TestMethod]
        public void Stage2_DefaultSettings_CorrectionOnShadowOff()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            Assert.IsTrue(settings.EnableStage2DoseRegimenRouting,
                "Master toggle must default to on (pre-existing behavior).");
            Assert.IsTrue(settings.EnableStage2DoseRegimenRoutingCorrection,
                "Correction must default to on.");
            Assert.IsFalse(settings.EnableStage2ShadowMode,
                "Shadow mode must default to off so operators only opt in deliberately.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #4 — when correction is disabled and shadow is off, Stage 2
        /// is a no-op. No flags are emitted, no mutation.
        /// </summary>
        [TestMethod]
        public async Task Stage2_BothOff_NoFlagsAndNoMutation()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage2DoseRegimenRouting = true,
                EnableStage2DoseRegimenRoutingCorrection = false,
                EnableStage2ShadowMode = false,
            };
            var service = await createInitializedServiceAsync(settings);

            var obs = createTestObservation(doseRegimen: "500 mg daily");
            var originalRegimen = obs.DoseRegimen;

            var result = service.ScoreAndCorrect(new List<ParsedObservation> { obs });

            var flags = result[0].ValidationFlags ?? string.Empty;
            Assert.IsFalse(flags.Contains("QC:DOSEREGIMEN_ROUTED_TO"),
                "No active routing when correction is disabled.");
            Assert.IsFalse(flags.Contains("QC:DOSEREGIMEN_SHADOW"),
                "No shadow emission when shadow is disabled.");
            Assert.AreEqual(originalRegimen, result[0].DoseRegimen,
                "DoseRegimen must not be mutated.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #4 — settings round-trip — flipping shadow on while correction is still on
        /// is allowed (shadow is only consulted when correction is off).
        /// </summary>
        [TestMethod]
        public void Stage2_ShadowOnWithCorrectionOn_SettingsCoexistWithoutError()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage2DoseRegimenRoutingCorrection = true,
                EnableStage2ShadowMode = true,
            };
            Assert.IsTrue(settings.EnableStage2DoseRegimenRoutingCorrection);
            Assert.IsTrue(settings.EnableStage2ShadowMode);

            #endregion
        }

        #endregion PR #4 — Stage 2 shadow mode

        #region PR #6 infrastructure — Stage 1 dual-write audit

        /**************************************************************/
        /// <summary>
        /// PR #6 — the dual-write audit flag defaults to off so no behavior
        /// changes until operators deliberately opt in during the re-enable window.
        /// </summary>
        [TestMethod]
        public void Stage1DualWriteAudit_DefaultsToOff()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings();
            Assert.IsFalse(settings.EnableStage1DualWriteAudit,
                "Dual-write audit should default to off — it's an opt-in verbosity knob.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #6 — the dual-write audit flag is meaningful only when BOTH
        /// correction and shadow are enabled.
        /// </summary>
        [TestMethod]
        public void Stage1DualWriteAudit_CoexistsWithShadowAndCorrectionFlags()
        {
            #region implementation

            var settings = new QCNetCorrectionSettings
            {
                EnableStage1TableCategoryCorrection = true,
                EnableStage1ShadowMode = true,
                EnableStage1DualWriteAudit = true,
            };

            Assert.IsTrue(settings.EnableStage1TableCategoryCorrection);
            Assert.IsTrue(settings.EnableStage1ShadowMode);
            Assert.IsTrue(settings.EnableStage1DualWriteAudit);

            #endregion
        }

        #endregion PR #6 infrastructure — Stage 1 dual-write audit
    }
}
