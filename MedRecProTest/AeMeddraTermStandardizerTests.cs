using System.Text.Json;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests for the Stage 5 AE MedDRA term standardizer.
    /// </summary>
    /// <remarks>
    /// These tests protect the visualization-quality contract: known AE names own
    /// their SOC, raw categories normalize into the official 27 SOC vocabulary, and
    /// structural threshold rows are excluded before comparator grouping.
    /// </remarks>
    /// <seealso cref="AeMeddraTermStandardizer"/>
    [TestClass]
    public class AeMeddraTermStandardizerTests
    {
        #region Constants

        /**************************************************************/
        /// <summary>Local JSONL curation artifact supplied with the implementation plan.</summary>
        private const string JsonlPath = @"C:\Users\chris\Downloads\name-category_ae.jsonl";

        #endregion Constants

        #region Official SOC Guard

        /**************************************************************/
        /// <summary>The official SOC guard contains the MedDRA 27 SOC target set.</summary>
        [TestMethod]
        public void OfficialSocSet_ContainsTwentySevenMeddraSocs()
        {
            #region implementation

            Assert.AreEqual(27, AeMeddraTermStandardizer.OfficialSocCount);
            Assert.IsTrue(AeMeddraTermStandardizer.IsOfficialSoc("General Disorders and Administration Site Conditions"));
            Assert.IsTrue(AeMeddraTermStandardizer.IsOfficialSoc("Musculoskeletal and Connective Tissue Disorders"));
            Assert.IsTrue(AeMeddraTermStandardizer.IsOfficialSoc("Respiratory, Thoracic and Mediastinal Disorders"));
            Assert.IsTrue(AeMeddraTermStandardizer.IsOfficialSoc("Neoplasms Benign, Malignant and Unspecified (Incl Cysts and Polyps)"));

            #endregion
        }

        #endregion Official SOC Guard

        #region Category Resolution

        /**************************************************************/
        /// <summary>Raw JSONL and legacy repo category aliases normalize to official SOCs.</summary>
        /// <param name="rawCategory">Raw category value.</param>
        /// <param name="expectedSoc">Expected official SOC.</param>
        [DataTestMethod]
        [DataRow("Ocular", "Eye Disorders")]
        [DataRow("Vision Disorders", "Eye Disorders")]
        [DataRow("Digestive", "Gastrointestinal Disorders")]
        [DataRow("Gastrointestinal System", "Gastrointestinal Disorders")]
        [DataRow("Chemistry", "Investigations")]
        [DataRow("Laboratory Investigations", "Investigations")]
        [DataRow("Hematology", "Blood and Lymphatic System Disorders")]
        [DataRow("Respiratory Disorders", "Respiratory, Thoracic and Mediastinal Disorders")]
        [DataRow("Musculoskeletal Disorders", "Musculoskeletal and Connective Tissue Disorders")]
        [DataRow("General Disorders", "General Disorders and Administration Site Conditions")]
        public void ResolveRawCategory_KnownAliases_MapToOfficialSoc(string rawCategory, string expectedSoc)
        {
            #region implementation

            Assert.AreEqual(expectedSoc, AeMeddraTermStandardizer.ResolveRawCategoryForTesting(rawCategory));

            #endregion
        }

        /**************************************************************/
        /// <summary>Known table headers and regimen leakage are explicit non-category evidence.</summary>
        /// <param name="rawCategory">Raw category value.</param>
        [DataTestMethod]
        [DataRow("Adverse Reactions")]
        [DataRow("Preferred Term")]
        [DataRow("Grade 2")]
        [DataRow("% Overall")]
        [DataRow("Rate (episodes/patient-year)")]
        [DataRow("With Metformin (30 Weeks)")]
        public void IsExplicitlyUnmappableCategory_KnownHeaders_ReturnsTrue(string rawCategory)
        {
            #region implementation

            Assert.IsTrue(AeMeddraTermStandardizer.IsExplicitlyUnmappableCategory(rawCategory));
            Assert.IsNull(AeMeddraTermStandardizer.ResolveRawCategoryForTesting(rawCategory));

            #endregion
        }

        #endregion Category Resolution

        #region Name Authority

        /**************************************************************/
        /// <summary>Known AE terms override conflicting raw category headers.</summary>
        /// <param name="name">Raw ParameterName.</param>
        /// <param name="rawCategory">Conflicting raw category.</param>
        /// <param name="expectedSoc">Expected official SOC from name evidence.</param>
        [DataTestMethod]
        [DataRow("Nausea", "Chemistry", "Gastrointestinal Disorders")]
        [DataRow("Headache", "Psychiatric Disorders", "Nervous System Disorders")]
        [DataRow("Conjunctivitis", "General Disorders", "Eye Disorders")]
        public void Standardize_NameSocOverridesConflictingCategory(string name, string rawCategory, string expectedSoc)
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow(name, rawCategory);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual(expectedSoc, row.ParameterCategory);
            Assert.IsTrue(result.Flags.Any(f => f.StartsWith("AE_STD:SOC_ALIGNED:", StringComparison.Ordinal)));
            CollectionAssert.Contains(
                result.Flags.ToList(),
                $"AE_STD:SOC_ALIGNED:{rawCategory}->{expectedSoc}");

            #endregion
        }

        /**************************************************************/
        /// <summary>Null categories are rescued when the AE name is known.</summary>
        [TestMethod]
        public void Standardize_NullCategory_ResolvesFromName()
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow("Blood glucose increased", null);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual("Blood Glucose Increased", row.ParameterName);
            Assert.AreEqual("Investigations", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME:<null>->Investigations");

            #endregion
        }

        /**************************************************************/
        /// <summary>Title case keeps protected medical acronyms intact.</summary>
        [TestMethod]
        public void Standardize_TitleCase_PreservesMedicalAcronyms()
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow("alt increased", null);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual("ALT Increased", row.ParameterName);
            Assert.AreEqual("Investigations", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:NAME_NORMALIZED");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:NAME_NORMALIZED:alt increased->ALT Increased");

            #endregion
        }

        /**************************************************************/
        /// <summary>Category-only standardization records the raw old value and official new value.</summary>
        [TestMethod]
        public void Standardize_CategoryOnlyChange_LogsOldAndNewValues()
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow("Unknown term", "Ocular");

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual("Unknown Term", row.ParameterName);
            Assert.AreEqual("Eye Disorders", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_CATEGORY");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_CATEGORY:Ocular->Eye Disorders");

            #endregion
        }

        /**************************************************************/
        /// <summary>One-direction weight increase variants canonicalize to the MedDRA investigation term.</summary>
        /// <param name="rawName">Raw ParameterName value.</param>
        [DataTestMethod]
        [DataRow("Weight increase")]
        [DataRow("Increased weight")]
        [DataRow("Weight, increased")]
        [DataRow("Weight gain")]
        [DataRow("Weight Gain")]
        [DataRow("Weight gain/increased")]
        [DataRow("Investigations Weight increased")]
        public void Standardize_WeightIncreaseVariants_MapToWeightIncreasedInvestigations(string rawName)
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow(rawName, null);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual("Weight Increased", row.ParameterName);
            Assert.AreEqual("Investigations", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:NAME_NORMALIZED");
            CollectionAssert.Contains(result.Flags.ToList(), $"AE_STD:NAME_NORMALIZED:{rawName}->Weight Increased");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME:<null>->Investigations");

            #endregion
        }

        /**************************************************************/
        /// <summary>One-direction weight decrease variants canonicalize to the MedDRA investigation term.</summary>
        /// <param name="rawName">Raw ParameterName value.</param>
        [DataTestMethod]
        [DataRow("Weight decrease")]
        [DataRow("Decreased weight")]
        [DataRow("Decreased Weight*")]
        [DataRow("Weight Loss")]
        [DataRow("Weight loss")]
        [DataRow("Weight decreased*")]
        [DataRow("Lost >5 lbs")]
        public void Standardize_WeightDecreaseVariants_MapToWeightDecreasedInvestigations(string rawName)
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow(rawName, null);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual("Weight Decreased", row.ParameterName);
            Assert.AreEqual("Investigations", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:NAME_NORMALIZED");
            CollectionAssert.Contains(result.Flags.ToList(), $"AE_STD:NAME_NORMALIZED:{rawName}->Weight Decreased");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_FROM_NAME:<null>->Investigations");

            #endregion
        }

        /**************************************************************/
        /// <summary>Weight change terms override metabolism aliases and audit the SOC correction.</summary>
        /// <param name="rawName">Raw ParameterName value.</param>
        /// <param name="rawCategory">Raw ParameterCategory value.</param>
        /// <param name="expectedName">Expected canonical ParameterName.</param>
        [DataTestMethod]
        [DataRow("Weight gain", "Metabolic/Nutritional", "Weight Increased")]
        [DataRow("Weight loss", "Metabolic and Nutritional Disorders", "Weight Decreased")]
        public void Standardize_WeightChangeMetabolismCategory_AlignsToInvestigations(string rawName, string rawCategory, string expectedName)
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow(rawName, rawCategory);

            var result = standardizer.Standardize(row);

            Assert.IsFalse(result.IsExcluded);
            Assert.AreEqual(expectedName, row.ParameterName);
            Assert.AreEqual("Investigations", row.ParameterCategory);
            CollectionAssert.Contains(result.Flags.ToList(), $"AE_STD:NAME_NORMALIZED:{rawName}->{expectedName}");
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:SOC_ALIGNED");
            CollectionAssert.Contains(result.Flags.ToList(), $"AE_STD:SOC_ALIGNED:{rawCategory}->Investigations");

            #endregion
        }

        #endregion Name Authority

        #region Exclusions

        /**************************************************************/
        /// <summary>Threshold-only names are excluded, while real AE terms containing thresholds remain.</summary>
        /// <param name="name">Candidate name.</param>
        /// <param name="expectedExcluded">Expected exclusion result.</param>
        [DataTestMethod]
        [DataRow("> 5 x ULN", true)]
        [DataRow("< 500/mm", true)]
        [DataRow("25,000 to 49,999/mm", true)]
        [DataRow("ALT (>5 x ULN)", false)]
        [DataRow("AST (>5 x ULN)", false)]
        [DataRow("Granulocytopenia (<750 cells/mm)", false)]
        [DataRow("Thrombocytopenia (platelets <50,000/mm)", false)]
        public void IsExcludedFromVisualization_ThresholdBoundaries_AreAnchored(string name, bool expectedExcluded)
        {
            #region implementation

            Assert.AreEqual(expectedExcluded, AeMeddraTermStandardizer.IsExcludedFromVisualization(name));

            #endregion
        }

        /**************************************************************/
        /// <summary>Bidirectional weight gain/loss text is excluded instead of forced to one direction.</summary>
        [TestMethod]
        public void Standardize_WeightGainLoss_IsExcludedAsAmbiguous()
        {
            #region implementation

            var standardizer = new AeMeddraTermStandardizer();
            var row = createRow("Weight gain/loss", null);

            var result = standardizer.Standardize(row);

            Assert.IsTrue(result.IsExcluded);
            Assert.IsTrue(AeMeddraTermStandardizer.IsExcludedFromVisualization("Weight gain/loss"));
            CollectionAssert.Contains(result.Flags.ToList(), "AE_STD:EXCLUDED_NON_AE");

            #endregion
        }

        #endregion Exclusions

        #region JSONL Coverage

        /**************************************************************/
        /// <summary>Every raw JSONL category is mapped to an official SOC or explicit non-SOC category.</summary>
        [TestMethod]
        public void JsonlCategories_AllMapOrAreExplicitlyUnmappable()
        {
            #region implementation

            if (!File.Exists(JsonlPath))
                return;

            var unmappedCategories = readJsonlParameters()
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(c => AeMeddraTermStandardizer.ResolveRawCategoryForTesting(c) is null &&
                            !AeMeddraTermStandardizer.IsExplicitlyUnmappableCategory(c))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.AreEqual(
                0,
                unmappedCategories.Count,
                "Unmapped raw categories: " + string.Join(" | ", unmappedCategories.Take(25)));

            #endregion
        }

        /**************************************************************/
        /// <summary>Every JSONL null-category name resolves by name or is explicitly excluded.</summary>
        [TestMethod]
        public void JsonlNullCategoryNames_AllResolveByNameOrAreExcluded()
        {
            #region implementation

            if (!File.Exists(JsonlPath))
                return;

            var standardizer = new AeMeddraTermStandardizer();
            var unresolvedNames = new List<string>();

            foreach (var name in readJsonlParameters()
                         .Where(p => string.IsNullOrWhiteSpace(p.Category))
                         .Select(p => p.Name)
                         .Where(n => !string.IsNullOrWhiteSpace(n))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var row = createRow(name!, null);
                var result = standardizer.Standardize(row);
                if (!result.IsExcluded && !AeMeddraTermStandardizer.IsOfficialSoc(row.ParameterCategory))
                    unresolvedNames.Add(name!);
            }

            unresolvedNames = unresolvedNames
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.AreEqual(
                0,
                unresolvedNames.Count,
                "Unresolved null-category names: " + string.Join(" | ", unresolvedNames.Take(25)));

            #endregion
        }

        #endregion JSONL Coverage

        #region Helpers

        /**************************************************************/
        /// <summary>Creates a minimal source row for standardizer tests.</summary>
        /// <param name="name">ParameterName value.</param>
        /// <param name="category">ParameterCategory value.</param>
        /// <returns>Source row.</returns>
        private static LabelView.FlattenedStandardizedTable createRow(string? name, string? category)
        {
            #region implementation

            return new LabelView.FlattenedStandardizedTable
            {
                Id = 1,
                DocumentGUID = Guid.NewGuid(),
                TableCategory = "ADVERSE_EVENT",
                ParameterName = name,
                ParameterCategory = category,
                TreatmentArm = "Drug A",
                ArmN = 100,
                PrimaryValue = 1,
                PrimaryValueType = "Count"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>Reads the supplied JSONL curation artifact into lightweight name/category pairs.</summary>
        /// <returns>Distinct parameter pairs from the JSONL artifact.</returns>
        private static IEnumerable<JsonlAeParameter> readJsonlParameters()
        {
            #region implementation

            foreach (var line in File.ReadLines(JsonlPath))
            {
                using var document = JsonDocument.Parse(line);
                foreach (var parameter in document.RootElement
                             .GetProperty("adverseEventParameters")
                             .EnumerateArray())
                {
                    yield return new JsonlAeParameter(
                        parameter.GetProperty("ParameterName").GetString(),
                        parameter.TryGetProperty("ParameterCategory", out var categoryElement) &&
                        categoryElement.ValueKind != JsonValueKind.Null
                            ? categoryElement.GetString()
                            : null);
                }
            }

            #endregion
        }

        #endregion Helpers
    }

    /**************************************************************/
    /// <summary>
    /// Lightweight JSONL parameter projection used by standardizer tests.
    /// </summary>
    /// <param name="Name">ParameterName value from JSONL.</param>
    /// <param name="Category">ParameterCategory value from JSONL.</param>
    /// <seealso cref="AeMeddraTermStandardizerTests"/>
    internal sealed record JsonlAeParameter(string? Name, string? Category);
}
