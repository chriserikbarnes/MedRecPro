using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for AE dashboard derivation helpers.
    /// </summary>
    /// <remarks>
    /// These tests exercise pure derivation behavior without a DbContext so signal
    /// scoring, tiering, quadrant, reverse lookup, and interchange rules stay
    /// deterministic.
    /// </remarks>
    /// <seealso cref="AeDashboardDerivation"/>
    [TestClass]
    public class AeDashboardDerivationTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies that DeriveSignal maps significance, number-needed, and delimited flags.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.DeriveSignal(AeRiskSignalDto, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void DeriveSignal_ElevatedProtectiveNotSignificantAndFlags_MapsExpectedValues()
        {
            #region implementation

            var elevated = newSignal(significance: "elevated", numberNeededType: "NNH", flags: "ZERO_CELL_CORRECTED,UNKNOWN;SOC_REMAP");
            var protective = newSignal(significance: "protective", numberNeededType: "NNT");
            var notSignificant = newSignal(significance: "not_significant", numberNeededType: null);

            AeDashboardDerivation.DeriveSignal(elevated);
            AeDashboardDerivation.DeriveSignal(protective);
            AeDashboardDerivation.DeriveSignal(notSignificant);

            Assert.AreEqual(AeRiskSignificance.Elevated, elevated.RiskSignificance);
            Assert.IsTrue(elevated.IsSignificant);
            Assert.AreEqual(AeNumberNeededType.NNH, elevated.NumberNeededKind);
            CollectionAssert.Contains(elevated.Flags, AeDataQualityFlag.ZeroCellCorrected);
            CollectionAssert.Contains(elevated.Flags, AeDataQualityFlag.SocRemap);
            Assert.AreEqual(AeRiskSignificance.Protective, protective.RiskSignificance);
            Assert.IsTrue(protective.IsProtective);
            Assert.AreEqual(AeNumberNeededType.NNT, protective.NumberNeededKind);
            Assert.AreEqual(AeRiskSignificance.NotSignificant, notSignificant.RiskSignificance);
            Assert.AreEqual(AeNumberNeededType.None, notSignificant.NumberNeededKind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that ClassifyPrecision covers tight, wide, fragile, and SocRemap cases.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.ClassifyPrecision(AeRiskSignalDto, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void ClassifyPrecision_TightWideFragileAndSocRemap_ReturnsExpectedClasses()
        {
            #region implementation

            var tight = newSignal(eventsTreatment: 30, eventsComparator: 20, rrLower: 1.5, rrUpper: 2.0);
            var wideByCi = newSignal(eventsTreatment: 30, eventsComparator: 20, rrLower: 0.5, rrUpper: 5.0);
            var wideByEventCount = newSignal(eventsTreatment: 12, eventsComparator: 10, rrLower: 1.5, rrUpper: 2.0);
            var fragileByFlag = newSignal(flags: "LOW_EVENT_COUNT", eventsTreatment: 30, eventsComparator: 20);
            var fragileByInvalidCi = newSignal(eventsTreatment: 30, eventsComparator: 20, rrLower: null, rrUpper: 2.0);
            var socRemapOnly = newSignal(flags: "SOC_REMAP", eventsTreatment: 30, eventsComparator: 20, rrLower: 1.5, rrUpper: 2.0);

            Assert.AreEqual(AePrecisionClass.Tight, AeDashboardDerivation.ClassifyPrecision(tight));
            Assert.AreEqual(AePrecisionClass.Wide, AeDashboardDerivation.ClassifyPrecision(wideByCi));
            Assert.AreEqual(AePrecisionClass.Wide, AeDashboardDerivation.ClassifyPrecision(wideByEventCount));
            Assert.AreEqual(AePrecisionClass.Fragile, AeDashboardDerivation.ClassifyPrecision(fragileByFlag));
            Assert.AreEqual(AePrecisionClass.Fragile, AeDashboardDerivation.ClassifyPrecision(fragileByInvalidCi));
            Assert.AreEqual(AePrecisionClass.Tight, AeDashboardDerivation.ClassifyPrecision(socRemapOnly));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that ClassifyCounselingTier covers Counsel, Watch, Reassure, and Fragile.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.ClassifyCounselingTier(AeRiskSignalDto)"/>
        [TestMethod]
        public void ClassifyCounselingTier_PrimaryDecisionBranches_ReturnExpectedTiers()
        {
            #region implementation

            var counsel = AeDashboardDerivation.DeriveSignal(newSignal(significance: "elevated", numberNeeded: 25));
            var watch = AeDashboardDerivation.DeriveSignal(newSignal(significance: "elevated", category: "Cardiac", numberNeeded: 75));
            var reassure = AeDashboardDerivation.DeriveSignal(newSignal(significance: "not_significant"));
            var fragile = AeDashboardDerivation.DeriveSignal(newSignal(significance: "elevated", flags: "ZERO_CELL_CORRECTED"));

            Assert.AreEqual(AeCounselingTier.Counsel, AeDashboardDerivation.ClassifyCounselingTier(counsel));
            Assert.AreEqual(AeCounselingTier.Watch, AeDashboardDerivation.ClassifyCounselingTier(watch));
            Assert.AreEqual(AeCounselingTier.Reassure, AeDashboardDerivation.ClassifyCounselingTier(reassure));
            Assert.AreEqual(AeCounselingTier.Fragile, AeDashboardDerivation.ClassifyCounselingTier(fragile));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that DeriveProduct applies the score formula and reason text.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.DeriveProduct(AeDrugSummaryDto, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void DeriveProduct_HighAndLowCoverageProducts_ProducesExpectedScoreRange()
        {
            #region implementation

            var high = AeDashboardDerivation.DeriveProduct(newProduct(
                rowCount: 40,
                significantElevatedCount: 40,
                placeboCoverage: true,
                activeCoverage: true,
                doseCoverage: 1.0,
                socBreadth: 17));
            var low = AeDashboardDerivation.DeriveProduct(newProduct(
                rowCount: 5,
                significantElevatedCount: 0,
                placeboCoverage: false,
                activeCoverage: false,
                doseCoverage: 0.0,
                socBreadth: 1));

            Assert.AreEqual(100, high.Score);
            Assert.IsTrue(high.ScoreReason!.Contains("Top contributors"));
            Assert.IsTrue(low.Score < 15);
            Assert.IsTrue(low.ScoreReason!.Contains("no placebo coverage"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildTriageView and BuildForestPlot assemble sorted dashboard containers.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildTriageView(AeDrugSummaryDto, IEnumerable{AeRiskSignalDto}, AeDashboardDerivationSettings?)"/>
        /// <seealso cref="AeDashboardDerivation.BuildForestPlot(IEnumerable{AeRiskSignalDto}, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void BuildTriageViewAndBuildForestPlot_DeriveAndSortSignals()
        {
            #region implementation

            var signals = new[]
            {
                newSignal(parameterName: "Nausea", rr: 2.0, numberNeeded: 25),
                newSignal(parameterName: "Headache", rr: 5.0, numberNeeded: 10),
                newSignal(parameterName: "Rash", significance: "not_significant", rr: 1.1)
            };

            var triage = AeDashboardDerivation.BuildTriageView(newProduct(), signals);
            var forest = AeDashboardDerivation.BuildForestPlot(signals);

            Assert.AreEqual(4, triage.Tiers.Count);
            Assert.AreEqual("Headache", triage.Tiers.First(tier => tier.Tier == AeCounselingTier.Counsel).Signals.First().ParameterName);
            Assert.AreEqual("Headache", forest.Signals.First().ParameterName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildQuadrantView clamps coordinates and derives direction.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildQuadrantView(IEnumerable{AeRiskSignalDto}, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void BuildQuadrantView_ExtremeValues_ClampsCoordinates()
        {
            #region implementation

            var quadrant = AeDashboardDerivation.BuildQuadrantView(new[]
            {
                newSignal(parameterName: "Extreme", rr: 1000, rrLower: 0.01, rrUpper: 10000, eventsTreatment: 400, eventsComparator: 300)
            });

            var point = quadrant.Points.Single();
            Assert.IsTrue(point.PrecisionX >= 0 && point.PrecisionX <= 1);
            Assert.IsTrue(point.MagnitudeY >= 0 && point.MagnitudeY <= 1);
            Assert.IsTrue(point.BubbleSize > 8);
            Assert.AreEqual(AeRiskSignificance.Elevated, point.Direction);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildReverseLookupResult ranks causal rows first and fragile rows last.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildReverseLookupResult(string, IEnumerable{AeDrugSummaryDto}, IEnumerable{AeRiskSignalDto}, AeDashboardDerivationSettings?)"/>
        /// <seealso cref="AeDashboardDerivation.ClassifyReverseLookupVerdict(AeRiskSignalDto)"/>
        [TestMethod]
        public void BuildReverseLookupResult_CausalProtectiveAndFragileRows_RanksAndSetsAllReassuring()
        {
            #region implementation

            var docA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var docB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var docC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var products = new[]
            {
                newProduct(documentGuid: docA, productName: "A"),
                newProduct(documentGuid: docB, productName: "B"),
                newProduct(documentGuid: docC, productName: "C")
            };
            var signals = new[]
            {
                newSignal(documentGuid: docB, parameterName: "Nausea", significance: "protective", rr: 0.4, numberNeeded: 30),
                newSignal(documentGuid: docC, parameterName: "Nausea", flags: "LOW_EVENT_COUNT", rr: 2.0, numberNeeded: 5),
                newSignal(documentGuid: docA, parameterName: "Nausea", significance: "elevated", rr: 2.5, numberNeeded: 10)
            };

            var result = AeDashboardDerivation.BuildReverseLookupResult("nausea", products, signals);

            Assert.IsFalse(result.AllReassuring);
            Assert.AreEqual(AeReverseLookupVerdict.PlausiblyCausal, result.Matches.First().Verdict);
            Assert.AreEqual(AeReverseLookupVerdict.LowConfidence, result.Matches.Last().Verdict);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildInterchangeComparison handles only-A, only-B, similar, A-worse, and B-worse rows.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildInterchangeComparison(AeDrugSummaryDto, AeDrugSummaryDto, IEnumerable{AeRiskSignalDto}, IEnumerable{AeRiskSignalDto}, bool, AeDashboardDerivationSettings?)"/>
        [TestMethod]
        public void BuildInterchangeComparison_AllClassificationBranchesAndDifferencesOnly_ReturnsExpectedCounts()
        {
            #region implementation

            var productA = newProduct(productName: "A", pharmClassCode: "A", placeboCoverage: true, activeCoverage: false);
            var productB = newProduct(productName: "B", pharmClassCode: "B", placeboCoverage: false, activeCoverage: true);
            var signalsA = new[]
            {
                newSignal(parameterName: "Headache", rr: 3.0),
                newSignal(parameterName: "Nausea", rr: 2.0),
                newSignal(parameterName: "Rash", significance: "not_significant", rr: 1.1),
                newSignal(parameterName: "Dizziness", rr: 1.2)
            };
            var signalsB = new[]
            {
                newSignal(parameterName: "Headache", rr: 1.5),
                newSignal(parameterName: "Rash", significance: "not_significant", rr: 0.9),
                newSignal(parameterName: "Dizziness", rr: 3.0),
                newSignal(parameterName: "Cough", rr: 2.0)
            };

            var comparison = AeDashboardDerivation.BuildInterchangeComparison(productA, productB, signalsA, signalsB);
            var differences = AeDashboardDerivation.BuildInterchangeComparison(productA, productB, signalsA, signalsB, true);

            Assert.AreEqual(1, comparison.OnlyACount);
            Assert.AreEqual(1, comparison.OnlyBCount);
            Assert.AreEqual(1, comparison.SimilarCount);
            Assert.AreEqual(1, comparison.AWorseCount);
            Assert.AreEqual(1, comparison.BWorseCount);
            Assert.IsNotNull(comparison.ClassMismatchWarning);
            Assert.IsNotNull(comparison.ComparatorMismatchWarning);
            Assert.IsFalse(differences.Rows.Any(row => row.Classification == AeInterchangeClass.Similar));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a default product summary for derivation tests.
        /// </summary>
        private static AeDrugSummaryDto newProduct(
            Guid? documentGuid = null,
            string productName = "Product",
            string pharmClassCode = "CLASS",
            int rowCount = 40,
            int significantElevatedCount = 4,
            bool placeboCoverage = true,
            bool activeCoverage = true,
            double doseCoverage = 0.75,
            int socBreadth = 10)
        {
            #region implementation

            return new AeDrugSummaryDto
            {
                DocumentGUID = documentGuid ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductName = productName,
                PharmClassCode = pharmClassCode,
                RowCount = rowCount,
                SignificantElevatedCount = significantElevatedCount,
                PlaceboCoverage = placeboCoverage,
                ActiveCoverage = activeCoverage,
                DoseCoverage = doseCoverage,
                SocBreadth = socBreadth,
                SocTotal = 17
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildActiveIngredients standardizes a single ingredient on its
        /// EPC class even when MoA strata are present.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildActiveIngredients(System.Collections.Generic.IEnumerable{AeDrugSummaryDto})"/>
        [TestMethod]
        public void BuildActiveIngredients_PrefersEpcOverMoa()
        {
            #region implementation

            var strata = new[]
            {
                stratum("salmeterol xinafoate", "Adrenergic beta2-Agonists [MoA]"),
                stratum("salmeterol xinafoate", "beta2-Adrenergic Agonist [EPC]")
            };

            var ingredients = AeDashboardDerivation.BuildActiveIngredients(strata);

            Assert.AreEqual(1, ingredients.Count);
            Assert.AreEqual("salmeterol xinafoate", ingredients[0].SubstanceName);
            Assert.AreEqual("beta2-Adrenergic Agonist [EPC]", ingredients[0].PharmClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a combination product lists every active ingredient, each on its
        /// EPC class, in deterministic (name-fallback) order.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildActiveIngredients(System.Collections.Generic.IEnumerable{AeDrugSummaryDto})"/>
        [TestMethod]
        public void BuildActiveIngredients_CombinationProduct_ListsEachIngredientOnEpc()
        {
            #region implementation

            var strata = new[]
            {
                stratum("salmeterol xinafoate", "Adrenergic beta2-Agonists [MoA]"),
                stratum("salmeterol xinafoate", "beta2-Adrenergic Agonist [EPC]"),
                stratum("fluticasone propionate", "Corticosteroid Hormone Receptor Agonists [MoA]"),
                stratum("fluticasone propionate", "Corticosteroid [EPC]")
            };

            var ingredients = AeDashboardDerivation.BuildActiveIngredients(strata);

            // Two ingredients, ordered by ordinal substance name when ids are absent.
            Assert.AreEqual(2, ingredients.Count);
            Assert.AreEqual("fluticasone propionate", ingredients[0].SubstanceName);
            Assert.AreEqual("Corticosteroid [EPC]", ingredients[0].PharmClassName);
            Assert.AreEqual("salmeterol xinafoate", ingredients[1].SubstanceName);
            Assert.AreEqual("beta2-Adrenergic Agonist [EPC]", ingredients[1].PharmClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildActiveIngredients falls back to the available class when no
        /// EPC variant exists, and tolerates a null class.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.BuildActiveIngredients(System.Collections.Generic.IEnumerable{AeDrugSummaryDto})"/>
        [TestMethod]
        public void BuildActiveIngredients_NoEpcVariantOrNullClass_FallsBack()
        {
            #region implementation

            var noEpc = AeDashboardDerivation.BuildActiveIngredients(new[]
            {
                stratum("drugx", "Some Receptor Antagonists [MoA]")
            });
            Assert.AreEqual(1, noEpc.Count);
            Assert.AreEqual("Some Receptor Antagonists [MoA]", noEpc[0].PharmClassName);

            var nullClass = AeDashboardDerivation.BuildActiveIngredients(new[]
            {
                stratum("rufinamide", null)
            });
            Assert.AreEqual(1, nullClass.Count);
            Assert.AreEqual("rufinamide", nullClass[0].SubstanceName);
            Assert.IsNull(nullClass[0].PharmClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates one product-summary stratum DTO for BuildActiveIngredients tests.
        /// </summary>
        /// <param name="substanceName">Active ingredient substance name.</param>
        /// <param name="pharmClassName">Pharmacologic class display name (may be null).</param>
        /// <returns>An <see cref="AeDrugSummaryDto"/> at the view's (substance × class) grain.</returns>
        private static AeDrugSummaryDto stratum(string substanceName, string? pharmClassName)
        {
            #region implementation

            return new AeDrugSummaryDto
            {
                DocumentGUID = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductName = "TEST PRODUCT",
                SubstanceName = substanceName,
                UNII = "TESTUNII",
                PharmClassName = pharmClassName,
                PharmClassCode = pharmClassName == null ? null : "CODE"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a default risk signal for derivation tests.
        /// </summary>
        private static AeRiskSignalDto newSignal(
            Guid? documentGuid = null,
            string parameterName = "Headache",
            string category = "Nervous System",
            string significance = "elevated",
            string? numberNeededType = "NNH",
            string? flags = null,
            double? eventsTreatment = 30,
            double? eventsComparator = 20,
            double? rr = 2.0,
            double? rrLower = 1.5,
            double? rrUpper = 2.0,
            double? numberNeeded = 25)
        {
            #region implementation

            return new AeRiskSignalDto
            {
                DocumentGUID = documentGuid ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ParameterName = parameterName,
                ParameterCategory = category,
                Significance = significance,
                NumberNeededType = numberNeededType,
                CalculationFlags = flags,
                EventsTreatment = eventsTreatment,
                EventsComparator = eventsComparator,
                RR = rr,
                RRLowerBound = rrLower,
                RRUpperBound = rrUpper,
                NumberNeeded = numberNeeded
            };

            #endregion
        }
    }
}
