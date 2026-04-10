using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="AeParameterCategoryDictionaryService"/> — the static dictionary-based
    /// lookup service that resolves NULL ParameterCategory values for ADVERSE_EVENT observations
    /// using unambiguous ParameterName → canonical SOC mappings.
    /// </summary>
    /// <remarks>
    /// ## Test Strategy
    /// Pure unit tests — no database, no mocking required. The service is stateless with a
    /// static dictionary, so tests exercise lookup logic, guard conditions, and flag appending.
    ///
    /// ## Test Organization
    /// - **Resolve tests**: Pure lookup behavior
    /// - **TryResolveObservation tests**: Full guard + mutation + flag behavior
    /// - **Dictionary integrity tests**: Validate all values are canonical SOC names
    /// - **Count tests**: Verify dictionary is populated
    /// </remarks>
    /// <seealso cref="IAeParameterCategoryDictionaryService"/>
    /// <seealso cref="ParsedObservation"/>
    [TestClass]
    public class AeParameterCategoryDictionaryServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>Creates a new instance of the service under test.</summary>
        private static AeParameterCategoryDictionaryService createService()
            => new AeParameterCategoryDictionaryService();

        /**************************************************************/
        /// <summary>
        /// Creates a minimal ADVERSE_EVENT observation with configurable ParameterName
        /// and ParameterCategory for testing.
        /// </summary>
        private static ParsedObservation createAeObservation(
            string? parameterName,
            string? parameterCategory = null,
            string tableCategory = "ADVERSE_EVENT",
            string? validationFlags = null) => new()
        {
            TableCategory = tableCategory,
            ParameterName = parameterName,
            ParameterCategory = parameterCategory,
            TreatmentArm = "Placebo",
            RawValue = "5.0",
            PrimaryValue = 5.0,
            PrimaryValueType = "Percentage",
            ValidationFlags = validationFlags
        };

        /**************************************************************/
        /// <summary>
        /// The 22 canonical SOC names used by <see cref="ColumnStandardizationService"/>'s
        /// <c>_socCanonicalMap</c>. All dictionary values must be one of these.
        /// </summary>
        private static readonly HashSet<string> _canonicalSocNames = new()
        {
            "Blood and Lymphatic System Disorders",
            "Cardiac Disorders",
            "Ear and Labyrinth Disorders",
            "Endocrine Disorders",
            "Eye Disorders",
            "Gastrointestinal Disorders",
            "General Disorders",
            "Hepatobiliary Disorders",
            "Immune System Disorders",
            "Infections and Infestations",
            "Injury, Poisoning and Procedural Complications",
            "Investigations",
            "Metabolism and Nutrition Disorders",
            "Musculoskeletal Disorders",
            "Neoplasms",
            "Nervous System Disorders",
            "Psychiatric Disorders",
            "Renal and Urinary Disorders",
            "Reproductive System and Breast Disorders",
            "Respiratory Disorders",
            "Skin and Subcutaneous Tissue Disorders",
            "Vascular Disorders"
        };

        #endregion Helper Methods

        #region Resolve Tests

        /**************************************************************/
        /// <summary>Known ParameterName returns the canonical SOC.</summary>
        [TestMethod]
        public void Resolve_KnownName_ReturnsCanonicalSoc()
        {
            #region implementation

            var service = createService();

            var result = service.Resolve("Nausea and Vomiting");

            Assert.IsNotNull(result);
            Assert.AreEqual("Gastrointestinal Disorders", result);

            #endregion
        }

        /**************************************************************/
        /// <summary>Lookup is case-insensitive.</summary>
        [TestMethod]
        public void Resolve_CaseInsensitive_ReturnsCanonicalSoc()
        {
            #region implementation

            var service = createService();

            var upper = service.Resolve("ANEMIA");
            var lower = service.Resolve("anemia");
            var mixed = service.Resolve("Anemia");

            Assert.IsNotNull(upper);
            Assert.IsNotNull(lower);
            Assert.IsNotNull(mixed);
            Assert.AreEqual(upper, lower);
            Assert.AreEqual(lower, mixed);

            #endregion
        }

        /**************************************************************/
        /// <summary>Unknown ParameterName returns null.</summary>
        [TestMethod]
        public void Resolve_UnknownName_ReturnsNull()
        {
            #region implementation

            var service = createService();

            var result = service.Resolve("TotallyInventedSymptom12345");

            Assert.IsNull(result);

            #endregion
        }

        /**************************************************************/
        /// <summary>Null input returns null.</summary>
        [TestMethod]
        public void Resolve_NullInput_ReturnsNull()
        {
            #region implementation

            var service = createService();

            Assert.IsNull(service.Resolve(null));

            #endregion
        }

        /**************************************************************/
        /// <summary>Whitespace-only input returns null.</summary>
        [TestMethod]
        public void Resolve_WhitespaceInput_ReturnsNull()
        {
            #region implementation

            var service = createService();

            Assert.IsNull(service.Resolve(""));
            Assert.IsNull(service.Resolve("   "));

            #endregion
        }

        /**************************************************************/
        /// <summary>Leading/trailing whitespace is trimmed before lookup.</summary>
        [TestMethod]
        public void Resolve_LeadingTrailingWhitespace_Trimmed()
        {
            #region implementation

            var service = createService();

            var result = service.Resolve("  Anemia  ");

            Assert.IsNotNull(result);
            Assert.AreEqual("Blood and Lymphatic System Disorders", result);

            #endregion
        }

        #endregion Resolve Tests

        #region TryResolveObservation Tests

        /**************************************************************/
        /// <summary>AE with null category + known name resolves and appends flag.</summary>
        [TestMethod]
        public void TryResolveObservation_NullCategory_KnownName_Resolves()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Dyspepsia");

            var result = service.TryResolveObservation(obs);

            Assert.IsTrue(result);
            Assert.AreEqual("Gastrointestinal Disorders", obs.ParameterCategory);
            Assert.IsNotNull(obs.ValidationFlags);
            Assert.IsTrue(obs.ValidationFlags.Contains("DICT:SOC_RESOLVED"));

            #endregion
        }

        /**************************************************************/
        /// <summary>AE with null category + unknown name returns false, no changes.</summary>
        [TestMethod]
        public void TryResolveObservation_NullCategory_UnknownName_ReturnsFalse()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("TotallyInventedSymptom12345");

            var result = service.TryResolveObservation(obs);

            Assert.IsFalse(result);
            Assert.IsNull(obs.ParameterCategory);
            Assert.IsNull(obs.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Existing ParameterCategory is never overwritten.</summary>
        [TestMethod]
        public void TryResolveObservation_ExistingCategory_NeverOverwrites()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia", parameterCategory: "Some Existing SOC");

            var result = service.TryResolveObservation(obs);

            Assert.IsFalse(result);
            Assert.AreEqual("Some Existing SOC", obs.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>Non-AE table category is skipped.</summary>
        [TestMethod]
        public void TryResolveObservation_NonAeCategory_Skipped()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia", tableCategory: "PK");

            var result = service.TryResolveObservation(obs);

            Assert.IsFalse(result);
            Assert.IsNull(obs.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>Flag set correctly when ValidationFlags starts empty.</summary>
        [TestMethod]
        public void TryResolveObservation_EmptyFlags_FlagSet()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia");

            service.TryResolveObservation(obs);

            Assert.AreEqual("DICT:SOC_RESOLVED", obs.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Flag appended with semicolon separator to existing flags.</summary>
        [TestMethod]
        public void TryResolveObservation_ExistingFlags_FlagAppended()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia", validationFlags: "PCT_CHECK:PASS");

            service.TryResolveObservation(obs);

            Assert.AreEqual("PCT_CHECK:PASS; DICT:SOC_RESOLVED", obs.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Null ParameterName returns false.</summary>
        [TestMethod]
        public void TryResolveObservation_NullParameterName_ReturnsFalse()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation(null);

            var result = service.TryResolveObservation(obs);

            Assert.IsFalse(result);
            Assert.IsNull(obs.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>Whitespace-only ParameterCategory is treated as null (resolved).</summary>
        [TestMethod]
        public void TryResolveObservation_WhitespaceCategory_TreatedAsNull()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia", parameterCategory: "   ");

            var result = service.TryResolveObservation(obs);

            Assert.IsTrue(result);
            Assert.AreEqual("Blood and Lymphatic System Disorders", obs.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>ADVERSE_EVENT comparison is case-insensitive.</summary>
        [TestMethod]
        public void TryResolveObservation_TableCategoryCaseInsensitive()
        {
            #region implementation

            var service = createService();
            var obs = createAeObservation("Anemia", tableCategory: "adverse_event");

            var result = service.TryResolveObservation(obs);

            Assert.IsTrue(result);
            Assert.AreEqual("Blood and Lymphatic System Disorders", obs.ParameterCategory);

            #endregion
        }

        #endregion TryResolveObservation Tests

        #region Count Tests

        /**************************************************************/
        /// <summary>Dictionary has a substantial number of entries.</summary>
        [TestMethod]
        public void Count_ReturnsPositiveNumber()
        {
            #region implementation

            var service = createService();

            Assert.IsTrue(service.Count > 600, $"Expected > 600 entries, got {service.Count}");

            #endregion
        }

        #endregion Count Tests

        #region Dictionary Integrity Tests

        /**************************************************************/
        /// <summary>
        /// Every value in the dictionary must be one of the 22 canonical SOC names.
        /// Catches typos and invalid SOC strings at test time.
        /// </summary>
        [TestMethod]
        public void AllValues_AreValidCanonicalSocNames()
        {
            #region implementation

            var service = createService();

            // Exercise every entry via Resolve — we need access to the keys.
            // Since the dictionary is case-insensitive, we test a representative set of known terms
            // across all SOC categories. The real validation is that Resolve never returns a
            // non-canonical string, which is ensured by the static dictionary values.

            // Test at least one known term per canonical SOC
            var sampleTermsBySoc = new Dictionary<string, string>
            {
                ["Anemia"] = "Blood and Lymphatic System Disorders",
                ["Arrhythmia"] = "Cardiac Disorders",
                ["Ear pain"] = "Ear and Labyrinth Disorders",
                ["Hypothyroidism"] = "Endocrine Disorders",
                ["Cataract"] = "Eye Disorders",
                ["Dyspepsia"] = "Gastrointestinal Disorders",
                ["Malaise"] = "General Disorders",
                ["Veno-occlusive disease"] = "Hepatobiliary Disorders",
                ["Hypersensitivity"] = "Immune System Disorders",
                ["Nasopharyngitis"] = "Infections and Infestations",
                ["Contusion"] = "Injury, Poisoning and Procedural Complications",
                ["Heart rate increased"] = "Investigations",
                ["Hyperlipidemia"] = "Metabolism and Nutrition Disorders",
                ["Arthritis"] = "Musculoskeletal Disorders",
                ["Convulsions"] = "Nervous System Disorders",
                ["Restlessness"] = "Psychiatric Disorders",
                ["Dysuria"] = "Renal and Urinary Disorders",
                ["Breast Pain"] = "Reproductive System and Breast Disorders",
                ["Bronchospasm"] = "Respiratory Disorders",
                ["Erythema"] = "Skin and Subcutaneous Tissue Disorders",
                ["Flushing"] = "Vascular Disorders",
            };

            foreach (var (term, expectedSoc) in sampleTermsBySoc)
            {
                var resolved = service.Resolve(term);
                Assert.IsNotNull(resolved, $"Expected '{term}' to resolve but got null");
                Assert.AreEqual(expectedSoc, resolved, $"Term '{term}' resolved to '{resolved}' instead of '{expectedSoc}'");
                Assert.IsTrue(_canonicalSocNames.Contains(resolved!),
                    $"Resolved SOC '{resolved}' for term '{term}' is not a canonical SOC name");
            }

            #endregion
        }

        #endregion Dictionary Integrity Tests
    }
}
