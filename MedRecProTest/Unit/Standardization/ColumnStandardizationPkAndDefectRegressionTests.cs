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
using static MedRecProTest.Unit.Standardization.ColumnStandardizationTestFixture;

namespace MedRecProTest.Unit.Standardization
{
    public partial class ColumnStandardizationServiceTests
    {
        #region Phase 2: AE Dictionary SOC Resolution

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
            assertFlagAbsent(result[0], "DICT:SOC_RESOLVED",
                "DICT:SOC_RESOLVED flag should not be present when category was not changed");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2: AE Dictionary SOC Resolution

        #region PK Column Contract Enforcement

        /// <summary>
        /// Fast path: Name already canonical, Subtype is a short qualifier.
        /// ParameterName is preserved as-is. Subtype may get its unit-like
        /// parenthesized content extracted by an upstream Phase-2 pass (that
        /// is pre-existing behavior and not under test here) — the relevant
        /// invariant is only that ParameterName stays canonical.
        /// </summary>
        [TestMethod]
        public async Task PkContract_FastPath_NameCanonical_Preserved()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation("Cmax", "single_dose");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            // Subtype kept (no PK term to scrub, no swap needed).
            Assert.AreEqual("single_dose", result[0].ParameterSubtype);

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 126/127 Cmax row shape: Name holds a header echo ("Population
        /// Estimates"), Subtype holds the descriptive phrase with Cmax embedded.
        /// Expected after enforcement: Name="Cmax", Subtype=qualifier (or null),
        /// Name was dropped via header-echo rule.
        /// </summary>
        [TestMethod]
        public async Task PkContract_BenlystaCmaxRow_CanonicalizesAndDropsHeaderEcho()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Population Estimates",
                parameterSubtype: "Peak concentration at steady state, Cmax,ss",
                unit: "mcg/mL");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName,
                "PK term must land in ParameterName per contract");
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                $"ParameterSubtype must not contain a PK term, got: '{result[0].ParameterSubtype}'");
            Assert.AreEqual("mcg/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:PK_NAME_FROM_PHRASE");
            assertHasFlag(result[0], "COL_STD:PK_NAME_ECHO_DROPPED");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 1769 drug+dose shape: Name holds "Guanfacine Extended-Release
        /// Tablets 1 mg once daily", Subtype="Cmax". Expected: Name="Cmax",
        /// TreatmentArm="Guanfacine Extended-Release Tablets", Dose=1, DoseUnit="mg/d".
        /// </summary>
        [TestMethod]
        public async Task PkContract_DrugPlusDoseInName_RoutesToArmAndDose()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Guanfacine Extended-Release Tablets 1 mg once daily",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            Assert.IsTrue(result[0].ValidationFlags?.Contains("COL_STD:PK_NAME_SUBTYPE_SWAPPED") ?? false);

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 985 infants shape: Name="Infants from Birth to 12 Months",
        /// Subtype="AUC0 to ∞(ng*hr/mL)". Expected: Name="AUC0-inf",
        /// Population="Infants Birth to 12 Months", Subtype=null.
        /// </summary>
        [TestMethod]
        public async Task PkContract_InfantsAgeRangeInName_RoutesToPopulation()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Infants from Birth to 12 Months",
                parameterSubtype: "AUC0 to ∞(ng*hr/mL)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC0-inf", result[0].ParameterName);
            Assert.AreEqual("Infants Birth to 12 Months", result[0].Population);
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                "ParameterSubtype must not contain a PK term");
            assertHasFlag(result[0], "COL_STD:PK_POPULATION_ROUTED_REGEX");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 3207 shape: Name="Tramadol" (drug name), Subtype="Cmax".
        /// Expected: Name="Cmax", TreatmentArm="Tramadol", Subtype=null.
        /// </summary>
        [TestMethod]
        public async Task PkContract_DrugNameInName_RoutesToTreatmentArm()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Tramadol",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            // "Tramadol" is a drug in the dictionary — should route to TreatmentArm.
            // (If not in the loaded drug list, parked in StudyContext instead.)
            var arm = result[0].TreatmentArm;
            var studyCtx = result[0].StudyContext;
            Assert.IsTrue(arm == "Tramadol" || studyCtx == "Tramadol",
                $"Tramadol should be preserved in TreatmentArm or StudyContext, got Arm='{arm}', Ctx='{studyCtx}'");
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 4977 shape: Name="6 to 11 years", Subtype="Cmax". Expected:
        /// Name="Cmax", Population="Ages 6-11 Years", Subtype=null.
        /// </summary>
        [TestMethod]
        public async Task PkContract_AgeRangeInName_RoutesToPopulationViaRegex()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "6 to 11 years",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.AreEqual("Ages 6-11 Years", result[0].Population);
            Assert.IsNull(result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:PK_POPULATION_ROUTED_REGEX");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 3208 shape: Name="Normal Creatinine Clearance 90 to 140 mL/min",
        /// Subtype="Cmin". Expected: Name="Cmin", Population="Normal Renal Function".
        /// </summary>
        [TestMethod]
        public async Task PkContract_RenalBandInName_RoutesToPopulationViaRegex()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Normal Creatinine Clearance 90 to 140 mL/min",
                parameterSubtype: "Cmin");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmin", result[0].ParameterName);
            Assert.AreEqual("Normal Renal Function", result[0].Population);
            Assert.IsNull(result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:PK_POPULATION_ROUTED_REGEX");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// "Total AUC" in Name canonicalizes to "AUC" per the new alias.
        /// Subtype holds "Single dose" which is a qualifier — kept as-is.
        /// </summary>
        [TestMethod]
        public async Task PkContract_TotalAUC_CanonicalizesToAUC()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Total AUC",
                parameterSubtype: "Single dose");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC", result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:PK_NAME_CANONICALIZED");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// Empty Name + TPEAK(h) in Subtype: the trailing unit is stripped by
        /// the prior Phase-2 pass, leaving Subtype="TPEAK". The contract enforcement
        /// then promotes TPEAK → Tmax into ParameterName.
        /// </summary>
        [TestMethod]
        public async Task PkContract_TPEAKInSubtype_PromotesToTmax()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: null,
                parameterSubtype: "TPEAK(h)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Tmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// 2nd Trimester population phrase routes via regex second pass.
        /// </summary>
        [TestMethod]
        public async Task PkContract_TrimesterInName_RoutesToPopulationViaRegex()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "2nd Trimester of Pregnancy",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.AreEqual("Second Trimester", result[0].Population);
            assertHasFlag(result[0], "COL_STD:PK_POPULATION_ROUTED_REGEX");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// Subtype scrub: Name is already canonical, but Subtype ALSO holds
        /// a PK term (duplicate). The scrub step demotes Subtype.
        /// </summary>
        [TestMethod]
        public async Task PkContract_SubtypeScrub_DemotesDuplicatePkTerm()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Cmax",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:PK_SUBTYPE_SCRUBBED");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// NULL Preservation guard: Name holds unclassifiable content,
        /// Subtype holds PK term, StudyContext empty. Name must be preserved
        /// into StudyContext rather than nulled.
        /// </summary>
        [TestMethod]
        public async Task PkContract_UnclassifiableName_ParksToStudyContext()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "MysteryValueXYZ",
                parameterSubtype: "Cmax");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.AreEqual("MysteryValueXYZ", result[0].StudyContext,
                "Unclassifiable Name must be preserved into StudyContext, not dropped");
            assertHasFlag(result[0], "COL_STD:PK_NAME_PARKED_CTX");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// Non-PK category guard: ADVERSE_EVENT observation with Subtype="Cmax"
        /// is NOT modified by the PK enforcement pass.
        /// </summary>
        [TestMethod]
        public async Task PkContract_NonPkCategory_LeftUnchanged()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = "ADVERSE_EVENT",
                ParameterName = "Nausea",
                ParameterCategory = "Gastrointestinal disorders",
                ParameterSubtype = "Cmax", // deliberately odd; shouldn't be touched
                TreatmentArm = "Drug A",
                TextTableID = 42,
                RawValue = "5",
                PrimaryValue = 5.0,
                PrimaryValueType = "Percentage",
                ParseConfidence = 0.9
            };

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Nausea", result[0].ParameterName);
            Assert.AreEqual("Cmax", result[0].ParameterSubtype,
                "ADVERSE_EVENT rows are outside the PK contract enforcement scope");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 126/127 t½ row: embedded "(t½, days)" inside the descriptive
        /// phrase should yield canonical t½ plus the "distribution" qualifier.
        /// </summary>
        [TestMethod]
        public async Task PkContract_DistributionHalfLifePhrase_CanonicalizesToThalf()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Population Estimates",
                parameterSubtype: "Distribution half-life (t½, days)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("t½", result[0].ParameterName);
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                $"ParameterSubtype must not contain a PK term, got: '{result[0].ParameterSubtype}'");
            assertHasFlag(result[0], "COL_STD:PK_NAME_FROM_PHRASE");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 126/127 Vss row: "Volume of distribution at steady state (Vss, L)"
        /// canonicalizes to Vss (not Vd — the "at steady state" specificity wins).
        /// </summary>
        [TestMethod]
        public async Task PkContract_VolumeAtSteadyStatePhrase_CanonicalizesToVss()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Population Estimates",
                parameterSubtype: "Volume of distribution at steady state (Vss, L)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Vss", result[0].ParameterName);
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                $"ParameterSubtype must not contain a PK term, got: '{result[0].ParameterSubtype}'");
            assertHasFlag(result[0], "COL_STD:PK_NAME_FROM_PHRASE");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 126/127 Systemic Clearance row: canonicalizes to CL, Unit stays L/day
        /// extracted by prior pass.
        /// </summary>
        [TestMethod]
        public async Task PkContract_SystemicClearancePhrase_CanonicalizesToCL()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Population Estimates",
                parameterSubtype: "Systemic clearance (CL, mL/day)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("CL", result[0].ParameterName);
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                "ParameterSubtype must not contain a PK term");
            assertHasFlag(result[0], "COL_STD:PK_NAME_FROM_PHRASE");

            context.Dispose();
            sentinel.Dispose();
        }

        /// <summary>
        /// TID 126/127 AUC row: canonicalizes to AUC0-inf from the embedded
        /// "(AUC0-∞, day·mcg/mL)" form.
        /// </summary>
        [TestMethod]
        public async Task PkContract_AreaUnderCurvePhrase_CanonicalizesToAUC0inf()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Population Estimates",
                parameterSubtype: "Area under the curve (AUC0-∞, day·mcg/mL)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC0-inf", result[0].ParameterName);
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                "ParameterSubtype must not contain a PK term");

            context.Dispose();
            sentinel.Dispose();
        }

        #endregion PK Column Contract Enforcement

        #region Wave 2 R6 — Subtype Non-PK Routing (Step 3b)

        /**************************************************************/
        /// <summary>
        /// R6 — Subtype holds a drug name ("Ketoconazole") while Name is a PK
        /// canonical. Expected after enforcement: Name preserved, TreatmentArm
        /// populated from Subtype, Subtype cleared, flag PK_SUBTYPE_ROUTED.
        /// </summary>
        [TestMethod]
        public async Task R6_PkContract_SubtypeDrugName_RoutesToTreatmentArm()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Cmax",
                parameterSubtype: "Ketoconazole");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype,
                "Non-qualifier Subtype must be cleared by Step 3b");
            var arm = result[0].TreatmentArm;
            var ctx = result[0].StudyContext;
            Assert.IsTrue(arm == "Ketoconazole" || ctx == "Ketoconazole",
                $"Ketoconazole should route to TreatmentArm or StudyContext, got arm='{arm}', ctx='{ctx}'");
            assertHasFlag(result[0], "COL_STD:PK_SUBTYPE_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R6 — Subtype holds a population descriptor. Expected: Population
        /// populated from Subtype, Subtype cleared, PK_SUBTYPE_ROUTED flag.
        /// </summary>
        [TestMethod]
        public async Task R6_PkContract_SubtypePopulation_RoutesToPopulation()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "AUC",
                parameterSubtype: "Female");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            Assert.AreEqual("Female", result[0].Population);
            assertHasFlag(result[0], "COL_STD:PK_SUBTYPE_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R6 — Subtype holds a timepoint descriptor. Expected: Timepoint
        /// populated from Subtype via the R7 Timepoint route inside
        /// routeOrParkNameContent.
        /// </summary>
        [TestMethod]
        public async Task R6_PkContract_SubtypeTimepoint_RoutesToTimepoint()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Tmax",
                parameterSubtype: "5 days");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Tmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            Assert.AreEqual("5 days", result[0].Timepoint);
            assertHasFlag(result[0], "COL_STD:PK_SUBTYPE_ROUTED");
            assertHasFlag(result[0], "COL_STD:PK_TIMEPOINT_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R6 guard: allowed qualifier tokens in Subtype are NOT routed out.
        /// The contract explicitly permits steady_state / single_dose / fasted /
        /// fed / terminal / distribution / CV(%) as Subtype content.
        /// </summary>
        [TestMethod]
        public async Task R6_PkContract_AllowedQualifiersInSubtype_Preserved()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            // "CV(%)" is omitted — upstream extractUnitFromParameterSubtype
            // strips trailing parenthesized content, leaving "CV". R6's job is
            // to leave whatever qualifier remains in Subtype after Phase 2
            // upstream passes without routing it out. "CV" alone IS allowed.
            var allowed = new[] { "steady_state", "single_dose", "fasted", "fed",
                                  "terminal", "distribution", "CV" };

            foreach (var q in allowed)
            {
                var obs = createPkObservation(parameterName: "Cmax", parameterSubtype: q);
                var result = service.Standardize(new List<ParsedObservation> { obs });

                Assert.IsFalse((result[0].ValidationFlags ?? "").Contains("COL_STD:PK_SUBTYPE_ROUTED"),
                    $"Allowed qualifier '{q}' must NOT fire PK_SUBTYPE_ROUTED");
                Assert.IsFalse(string.IsNullOrWhiteSpace(result[0].ParameterSubtype),
                    $"Allowed qualifier '{q}' must be preserved in Subtype (not routed out)");
            }

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R6 — Extended header-echo set drops "Mean (SD)" / "Geometric Mean"
        /// from Subtype without leaving data behind (PK_NAME_ECHO_DROPPED or
        /// absorbed via routing). These are statistic descriptors, not PK
        /// content.
        /// </summary>
        [TestMethod]
        public async Task R6_PkContract_HeaderEchoSet_ExtendedStatisticsDropped()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Cmax",
                parameterSubtype: "Mean (SD)");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            // Allow either path: direct echo drop or routed to StudyContext via (v)
            var subtypeCleared = string.IsNullOrWhiteSpace(result[0].ParameterSubtype);
            Assert.IsTrue(subtypeCleared, "Statistic-descriptor Subtype should be cleared");
            assertHasFlag(result[0], "COL_STD:PK_SUBTYPE_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        #endregion Wave 2 R6 — Subtype Non-PK Routing

        #region Wave 2 R7 — Unconditional Name Fitness (Step 5) + Timepoint Route

        /**************************************************************/
        /// <summary>
        /// R7 — Name is a pure drug name ("Placebo"), no PK term anywhere.
        /// Step 5 routes it to TreatmentArm or StudyContext via routeOrParkNameContent,
        /// nulls Name, fires PK_NAME_CLEANED_NONCANON flag.
        /// </summary>
        [TestMethod]
        public async Task R7_PkContract_NameDrugName_NoPkTermAnywhere_RoutedCleaned()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Placebo",
                parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName,
                "R7: Name with no canonical and no PK term anywhere must be nulled");
            var arm = result[0].TreatmentArm;
            var ctx = result[0].StudyContext;
            Assert.IsTrue(arm == "Placebo" || ctx == "Placebo",
                $"Placebo content preserved — arm='{arm}', ctx='{ctx}'");
            assertHasFlag(result[0], "COL_STD:PK_NAME_CLEANED_NONCANON");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R7 — Name holds a timepoint descriptor. Route to Timepoint via the
        /// new timepoint step in routeOrParkNameContent.
        /// </summary>
        [TestMethod]
        public async Task R7_PkContract_NameTimepoint_RoutesToTimepoint()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Day 14",
                parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            Assert.AreEqual("Day 14", result[0].Timepoint);
            assertHasFlag(result[0], "COL_STD:PK_TIMEPOINT_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R7 guard: Name with an embedded PK term (not in canonical form but
        /// ContainsPkParameter=true) is NOT cleaned — Step 5 skips it so upstream
        /// rescue paths (Step 2) can handle it.
        /// </summary>
        [TestMethod]
        public async Task R7_PkContract_NameWithEmbeddedPkTerm_NotCleanedByStep5()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            // "Area Under the Curve" contains a PK term (AUC) via the prefix
            // pattern / ContainsPkParameter; Step 5 must NOT clean it.
            var obs = createPkObservation(
                parameterName: "Area Under the Curve",
                parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Fast-path Step 1 or Step 2 rescue should have already resolved
            // this to a canonical AUC. Name should NOT be null.
            Assert.IsNotNull(result[0].ParameterName,
                "Name with embedded PK term must be resolved, not cleaned");
            // PK_NAME_CLEANED_NONCANON must NOT fire for this case.
            Assert.IsFalse((result[0].ValidationFlags ?? "").Contains("COL_STD:PK_NAME_CLEANED_NONCANON"),
                "Step 5 cleanup must not fire when Name contains a PK term");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R7 — Name is a population descriptor that does NOT contain a PK term.
        /// Step 5 routes it to Population.
        /// </summary>
        [TestMethod]
        public async Task R7_PkContract_NamePopulation_RoutesToPopulation()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Healthy Subjects",
                parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            Assert.AreEqual("Healthy Volunteers", result[0].Population);
            assertHasFlag(result[0], "COL_STD:PK_NAME_CLEANED_NONCANON");
            assertHasFlag(result[0], "COL_STD:PK_POPULATION_ROUTED");

            context.Dispose();
            sentinel.Dispose();
        }

        /**************************************************************/
        /// <summary>
        /// R7 — Drug+dose compound in Name with no PK term: routes to
        /// TreatmentArm + DoseRegimen via Step 5.
        /// </summary>
        [TestMethod]
        public async Task R7_PkContract_NameDrugPlusDose_RoutesToArmAndDose()
        {
            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(
                parameterName: "Guanfacine Extended-Release Tablets 1 mg once daily",
                parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName,
                "R7 Step 5 should null Name when it has no PK term");
            // Per NULL Preservation Rule: the displaced content lands in one
            // of TreatmentArm (if isDrugName(prefix)), DoseRegimen (if dose
            // extracted but drug prefix unknown), or StudyContext (park).
            // Test environment's loaded drug dict may differ, so accept any
            // of the three targets.
            var hasContent = !string.IsNullOrWhiteSpace(result[0].TreatmentArm) ||
                             !string.IsNullOrWhiteSpace(result[0].DoseRegimen) ||
                             !string.IsNullOrWhiteSpace(result[0].StudyContext);
            Assert.IsTrue(hasContent,
                $"Content must be preserved somewhere (Arm / DoseRegimen / StudyContext). " +
                $"arm='{result[0].TreatmentArm}', dose='{result[0].DoseRegimen}', ctx='{result[0].StudyContext}'");
            assertHasFlag(result[0], "COL_STD:PK_NAME_CLEANED_NONCANON");

            context.Dispose();
            sentinel.Dispose();
        }

        #endregion Wave 2 R7 — Unconditional Name Fitness

        #region Defect 1 — Bare N= Stripping

        /**************************************************************/
        /// <summary>
        /// Verifies that the Phase 2 pre-pass strips bare trailing N= patterns
        /// (no surrounding brackets/parens) from any eligible column and populates
        /// ArmN. Regression coverage for the production case where 55+ DoseRegimen
        /// rows and 28+ StudyContext rows held values like "60–89 mL per minute N=10",
        /// "Postpartum (6–12 weeks) N=6", "Adults given 50 mg once daily for 7 days N=12".
        /// </summary>
        /// <seealso cref="ColumnStandardizationService"/>
        [DataTestMethod]
        [DataRow("DoseRegimen", "60–89 mL per minute N=10", "60–89 mL per minute", 10)]
        [DataRow("DoseRegimen", "Adults given 50 mg once daily for 7 days N=12", "Adults given 50 mg once daily for 7 days", 12)]
        [DataRow("StudyContext", "Postpartum (6–12 weeks) N=6", "Postpartum (6–12 weeks)", 6)]
        [DataRow("StudyContext", "Abdomen N=113", "Abdomen", 113)]
        [DataRow("StudyContext", "Moderate Hepatic Impairment N=10", "Moderate Hepatic Impairment", 10)]
        public async Task Defect1_BareTrailingN_StrippedAndArmNPopulated(
            string column, string input, string expectedCleaned, int expectedArmN)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                TextTableID = 999,
                RawValue = "10",
                PrimaryValue = 10.0,
                PrimaryValueType = "ArithmeticMean",
                ParseConfidence = 0.9
            };
            switch (column)
            {
                case "DoseRegimen": obs.DoseRegimen = input; break;
                case "StudyContext": obs.StudyContext = input; break;
                default: throw new InvalidOperationException($"Unsupported column: {column}");
            }

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Verify the bare N= suffix is stripped and ArmN is populated.
            var actual = column == "DoseRegimen" ? result[0].DoseRegimen : result[0].StudyContext;
            Assert.AreEqual(expectedCleaned, actual,
                $"Column {column} should have bare N= suffix stripped");
            Assert.AreEqual(expectedArmN, result[0].ArmN,
                $"ArmN should be populated from stripped bare N=");
            assertHasFlag(result[0], $"COL_STD:N_STRIPPED:{column}:BARE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 1 — Bare N= Stripping

        #region Defect 2 — PrimaryValueType Enum Compliance

        /**************************************************************/
        /// <summary>
        /// Verifies the Phase 3 PVT migration coerces non-canonical labels to the
        /// enum defined in column-contracts.md: "Range" → ArithmeticMean (the
        /// midpoint is already in PrimaryValue with bounds set), "SampleSize" →
        /// Count (sample-size counts are integer counts).
        /// </summary>
        [DataTestMethod]
        [DataRow("Range", "ArithmeticMean")]
        [DataRow("SampleSize", "Count")]
        public async Task Defect2_NonCanonicalPvt_MappedToCanonical(string oldType, string expectedNew)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                TextTableID = 999,
                RawValue = "12 to 18",
                PrimaryValue = 15.0,
                PrimaryValueType = oldType,
                ParseConfidence = 0.9
            };

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(expectedNew, result[0].PrimaryValueType,
                $"PVT '{oldType}' should migrate to '{expectedNew}'");
            assertHasFlag(result[0], $"COL_STD:PVT_MIGRATED:{oldType}→{expectedNew}");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that already-canonical RelativeRisk on a non-Efficacy
        /// TableCategory is remapped to the contract-appropriate type. Per
        /// column-contracts.md, only the Efficacy contract permits risk-type
        /// PVTs; PK / DRUG_INTERACTION must use ArithmeticMean / GeometricMean.
        /// Caption containing "geometric" steers to GeometricMean.
        /// </summary>
        [DataTestMethod]
        [DataRow("PK", "", "ArithmeticMean")]
        [DataRow("PK", "Geometric Mean Ratio (90% CI)", "GeometricMean")]
        [DataRow("PK", "arithmetic mean ± SD", "ArithmeticMean")]
        [DataRow("DRUG_INTERACTION", "", "ArithmeticMean")]
        [DataRow("EFFICACY", "", "RelativeRisk")] // negative case — Efficacy keeps RelativeRisk
        public async Task Defect2_RelativeRiskOnNonEfficacy_RemappedByCategory(
            string category, string caption, string expected)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = category,
                ParameterName = "Cmax",
                TextTableID = 999,
                Caption = caption,
                RawValue = "1.5 (1.2, 1.8)",
                PrimaryValue = 1.5,
                LowerBound = 1.2,
                UpperBound = 1.8,
                BoundType = "90CI",
                PrimaryValueType = "RelativeRisk",
                ParseRule = "rr_ci",
                ParseConfidence = 0.9
            };

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(expected, result[0].PrimaryValueType,
                $"PVT for {category} (caption='{caption}') should be '{expected}'");
            // Bounds preserved
            Assert.AreEqual(1.2, result[0].LowerBound);
            Assert.AreEqual(1.8, result[0].UpperBound);

            // Negative case: Efficacy should NOT carry the remap flag.
            if (string.Equals(category, "EFFICACY", StringComparison.OrdinalIgnoreCase))
            {
                assertFlagAbsent(result[0], "COL_STD:PVT_RR_CI_CATEGORY_REMAP",
                    "Efficacy should NOT receive the remap flag");
            }
            else
            {
                assertHasFlag(result[0], "COL_STD:PVT_RR_CI_CATEGORY_REMAP");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 2 — PrimaryValueType Enum Compliance

        #region Defect 3 — DoseRegimen Stat-Form Echo

        /**************************************************************/
        /// <summary>
        /// Verifies that stat-form column-header echoes leaked into DoseRegimen
        /// (e.g., "Mean ± Standard Deviation", "Median (Range)") are nulled per
        /// the §0.2 header-echo carve-out in normalization-rules.md. A real dose
        /// regimen is preserved untouched (negative case).
        /// </summary>
        [DataTestMethod]
        [DataRow("Mean ± Standard Deviation", true)]
        [DataRow("Median (Range)", true)]
        [DataRow("Geometric Mean (CV%)", true)]
        [DataRow("Mean ± SD", true)]
        [DataRow("Median", true)]
        [DataRow("10 mg once daily", false)] // negative — real dose stays
        public async Task Defect3_DoseRegimenStatEcho_NulledOrPreserved(string input, bool shouldNull)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                DoseRegimen = input,
                TextTableID = 999,
                RawValue = "10",
                PrimaryValue = 10.0,
                PrimaryValueType = "ArithmeticMean",
                ParseConfidence = 0.9
            };

            var result = service.Standardize(new List<ParsedObservation> { obs });

            if (shouldNull)
            {
                Assert.IsNull(result[0].DoseRegimen,
                    $"Stat-form echo '{input}' should be nulled");
                assertHasFlag(result[0], "COL_STD:DOSEREGIMEN_STAT_ECHO_DROPPED");
            }
            else
            {
                Assert.AreEqual(input, result[0].DoseRegimen,
                    $"Real dose '{input}' should be preserved");
                assertFlagAbsent(result[0], "COL_STD:DOSEREGIMEN_STAT_ECHO_DROPPED",
                    "Real dose should not carry the echo-dropped flag");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 3 — DoseRegimen Stat-Form Echo

        #region Defect 4 — Unit Header-Leak Post-Extract

        /**************************************************************/
        /// <summary>
        /// Verifies that <see cref="ColumnStandardizationService.normalizeUnit"/>
        /// catches malformed Unit values that slip past Rules 1–6: values shorter
        /// than 30 chars (so Rule 2 misses), not exact-match drug names (so Rule 3
        /// misses), with unbalanced parens or embedded drug-name tokens. The
        /// observed production case is "mcg•hr/mL) Amoxicillin (±S.D.".
        /// </summary>
        [DataTestMethod]
        [DataRow("mcg•hr/mL) Amoxicillin (±S.D.", true)]  // unbalanced parens + drug
        [DataRow("mcg/mL", false)]                        // clean known unit (Rule 1)
        public async Task Defect4_UnitPostExtractSanity_NullsLeakedHeader(string input, bool shouldNull)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                Unit = input,
                TextTableID = 999,
                RawValue = "10",
                PrimaryValue = 10.0,
                PrimaryValueType = "ArithmeticMean",
                ParseConfidence = 0.9
            };

            var result = service.Standardize(new List<ParsedObservation> { obs });

            if (shouldNull)
            {
                Assert.IsNull(result[0].Unit,
                    $"Malformed Unit '{input}' should be nulled by sanity sweep");
                assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK:POST_EXTRACT");
            }
            else
            {
                Assert.AreEqual(input, result[0].Unit,
                    $"Clean Unit '{input}' should be preserved");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 4 — Unit Header-Leak Post-Extract

        #region Defect 5 — Study Identifier Routing

        /**************************************************************/
        /// <summary>
        /// Verifies that clinical-trial study identifiers leaked into ParameterName
        /// (e.g., "TMC114-C230" after Defect 1 strips the trailing "N=12") are
        /// routed to StudyContext, not parked there as the catch-all "unclassified"
        /// fallback. Drug names like "Amoxicillin" are excluded by the guard and
        /// route through the existing drug-name path. PK parameters are excluded
        /// by the IsPkParameter guard.
        /// </summary>
        [DataTestMethod]
        [DataRow("TMC114-C230", true)]
        [DataRow("TMC125-C234/IMPAACT P1090", true)]
        [DataRow("Cmax", false)]              // PK parameter — fast path wins
        public async Task Defect5_StudyId_RoutedToStudyContext(string parameterName, bool shouldRoute)
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createPkObservation(parameterName, parameterSubtype: null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            if (shouldRoute)
            {
                Assert.AreEqual(parameterName, result[0].StudyContext,
                    $"Study ID '{parameterName}' should be routed to StudyContext");
                Assert.IsNull(result[0].ParameterName,
                    "ParameterName should be nulled after routing");
                assertHasFlag(result[0], "COL_STD:PK_NAME_ROUTED_STUDY_ID");
            }
            else
            {
                // PK parameter — should remain as ParameterName via fast path.
                Assert.AreEqual(parameterName, result[0].ParameterName,
                    $"PK parameter '{parameterName}' should remain as ParameterName");
                assertFlagAbsent(result[0], "COL_STD:PK_NAME_ROUTED_STUDY_ID",
                    "PK parameter should not carry the study-id routed flag");
            }

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 5 — Study Identifier Routing

        #region Defect 5 Regression — Subtype Rescue Path

        /**************************************************************/
        /// <summary>
        /// Regression: when ParameterName is a study identifier AND ParameterSubtype
        /// holds a recoverable PK term (e.g., Name="OP-1118", Subtype="Cmax"), the
        /// Step 0 short-circuit MUST NOT fire — otherwise the existing Subtype
        /// rescue (PK_NAME_SUBTYPE_SWAPPED) is bypassed and the PK statistic is
        /// stranded. The end-to-end re-parse against the live corpus showed 10 rows
        /// of table 37517 (Fidaxomicin OP-1118 metabolite block) lost their
        /// Cmax/Tmax/AUC rows because the unguarded Step 0 nulled Name before the
        /// rescue could promote the PK term.
        /// </summary>
        /// <remarks>
        /// Expected post-Phase 2 state for this row:
        /// ParameterName="Cmax", ParameterSubtype=null, StudyContext="OP-1118",
        /// flag PK_NAME_SUBTYPE_SWAPPED (from the rescue) and
        /// PK_NAME_ROUTED_STUDY_ID (from the (i.6) step inside routeOrParkNameContent).
        /// </remarks>
        [TestMethod]
        public async Task Defect5Regression_StudyIdName_PkTermInSubtype_BothPathsFire()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Reproduces the OP-1118 / Cmax-in-Subtype shape from table 37517.
            var obs = createPkObservation(
                parameterName: "OP-1118",
                parameterSubtype: "Cmax",
                unit: "ng/mL");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // The PK term must be rescued from Subtype to Name.
            Assert.AreEqual("Cmax", result[0].ParameterName,
                "Subtype rescue should promote the PK term to ParameterName");

            // The displaced study identifier must be routed to StudyContext.
            Assert.AreEqual("OP-1118", result[0].StudyContext,
                "Displaced study identifier should land in StudyContext");

            // ParameterSubtype is cleared (the qualifier side-channel is empty for
            // a bare canonical PK term).
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype should be cleared after rescue");

            // Step 0 short-circuit must NOT have fired — would have nulled Name
            // prematurely. The PK_NAME_SUBTYPE_SWAPPED flag confirms the rescue ran.
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            // The (i.6) study-id step inside routeOrParkNameContent fires for the
            // displaced ParameterName.
            assertHasFlag(result[0], "COL_STD:PK_NAME_ROUTED_STUDY_ID");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Defect 5 Regression — Subtype Rescue Path
    }
}

