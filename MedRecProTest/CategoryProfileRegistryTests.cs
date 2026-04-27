using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="CategoryProfileRegistry"/> — the consolidated per-category
    /// profile lookup that replaces six parallel category-keyed dictionaries.
    /// </summary>
    /// <remarks>
    /// Verifies key-form acceptance (underscore vs documentation), profile completeness for all
    /// eight known categories plus OTHER, and consistency between the embedded
    /// <see cref="CategoryContract"/> and the canonical <see cref="ColumnContractRegistry"/>.
    /// </remarks>
    /// <seealso cref="CategoryProfileRegistry"/>
    /// <seealso cref="CategoryProfile"/>
    [TestClass]
    public class CategoryProfileRegistryTests
    {
        #region Key Form Tests

        /**************************************************************/
        /// <summary>
        /// Underscore-uppercase form returns a populated profile.
        /// </summary>
        [TestMethod]
        public void Get_KnownCategory_UnderscoreForm_ReturnsProfile()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("ADVERSE_EVENT");

            Assert.AreNotSame(CategoryProfile.Empty, profile);
            Assert.IsTrue(profile.UsesArmCoverage);
            Assert.AreEqual("95CI", profile.DefaultBoundType);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Documentation form returns the same profile as underscore form.
        /// </summary>
        [TestMethod]
        public void Get_KnownCategory_DocForm_ReturnsProfile()
        {
            #region implementation

            var fromUnderscore = CategoryProfileRegistry.Get("ADVERSE_EVENT");
            var fromDoc = CategoryProfileRegistry.Get("AdverseEvent");

            Assert.AreSame(fromUnderscore, fromDoc, "Both key forms should resolve to the same profile instance.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lowercase input still resolves.
        /// </summary>
        [TestMethod]
        public void Get_LowercaseInput_StillResolves()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("adverse_event");
            Assert.AreSame(CategoryProfileRegistry.Get("AdverseEvent"), profile);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unknown categories return <see cref="CategoryProfile.Empty"/>.
        /// </summary>
        [TestMethod]
        public void Get_Unknown_ReturnsEmpty()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("BOGUS_CATEGORY");
            Assert.AreSame(CategoryProfile.Empty, profile);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null and whitespace input return <see cref="CategoryProfile.Empty"/>.
        /// </summary>
        [TestMethod]
        public void Get_NullOrEmpty_ReturnsEmpty()
        {
            #region implementation

            Assert.AreSame(CategoryProfile.Empty, CategoryProfileRegistry.Get(null));
            Assert.AreSame(CategoryProfile.Empty, CategoryProfileRegistry.Get(""));
            Assert.AreSame(CategoryProfile.Empty, CategoryProfileRegistry.Get("   "));

            #endregion
        }

        #endregion Key Form Tests

        #region Contract Consistency Tests

        /**************************************************************/
        /// <summary>
        /// Each profile's embedded <see cref="CategoryContract"/> matches the contract
        /// returned by a fresh <see cref="ColumnContractRegistry"/> instance — proves the
        /// profile delegates to the canonical R/E/O/N source rather than duplicating it.
        /// </summary>
        [TestMethod]
        public void Profile_AllEightCategories_ContractMatchesColumnContractRegistry()
        {
            #region implementation

            var contractRegistry = new ColumnContractRegistry();
            var categories = new[] { "AdverseEvent", "PK", "DrugInteraction", "Efficacy", "Dosing", "BMD", "TissueDistribution", "TextDescriptive" };

            foreach (var category in categories)
            {
                var profile = CategoryProfileRegistry.Get(category);
                var canonical = contractRegistry.GetContract(category);

                CollectionAssert.AreEquivalent(canonical.Required.ToList(), profile.Contract.Required.ToList(),
                    $"Required mismatch for {category}");
                CollectionAssert.AreEquivalent(canonical.Expected.ToList(), profile.Contract.Expected.ToList(),
                    $"Expected mismatch for {category}");
                CollectionAssert.AreEquivalent(canonical.Optional.ToList(), profile.Contract.Optional.ToList(),
                    $"Optional mismatch for {category}");
                CollectionAssert.AreEquivalent(canonical.NullExpected.ToList(), profile.Contract.NullExpected.ToList(),
                    $"NullExpected mismatch for {category}");
            }

            #endregion
        }

        #endregion Contract Consistency Tests

        #region DefaultBoundType Tests

        /**************************************************************/
        [TestMethod]
        public void Profile_PK_DefaultBoundType_Is_90CI()
        {
            Assert.AreEqual("90CI", CategoryProfileRegistry.Get("PK").DefaultBoundType);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_DefaultBoundType_Is_95CI()
        {
            Assert.AreEqual("95CI", CategoryProfileRegistry.Get("ADVERSE_EVENT").DefaultBoundType);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_DrugInteraction_DefaultBoundType_Is_90CI()
        {
            Assert.AreEqual("90CI", CategoryProfileRegistry.Get("DRUG_INTERACTION").DefaultBoundType);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_BMD_DefaultBoundType_Is_95CI()
        {
            Assert.AreEqual("95CI", CategoryProfileRegistry.Get("BMD").DefaultBoundType);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_TissueDistribution_DefaultBoundType_IsNull()
        {
            Assert.IsNull(CategoryProfileRegistry.Get("TISSUE_DISTRIBUTION").DefaultBoundType);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_Dosing_DefaultBoundType_IsNull()
        {
            Assert.IsNull(CategoryProfileRegistry.Get("DOSING").DefaultBoundType);
        }

        #endregion DefaultBoundType Tests

        #region UsesArmCoverage / UsesTimeConsistency Tests

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_UsesArmCoverage_True()
        {
            Assert.IsTrue(CategoryProfileRegistry.Get("ADVERSE_EVENT").UsesArmCoverage);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_Efficacy_UsesArmCoverage_True()
        {
            Assert.IsTrue(CategoryProfileRegistry.Get("EFFICACY").UsesArmCoverage);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_PK_UsesArmCoverage_False()
        {
            Assert.IsFalse(CategoryProfileRegistry.Get("PK").UsesArmCoverage);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_PK_UsesTimeConsistency_True()
        {
            Assert.IsTrue(CategoryProfileRegistry.Get("PK").UsesTimeConsistency);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_BMD_UsesTimeConsistency_True()
        {
            Assert.IsTrue(CategoryProfileRegistry.Get("BMD").UsesTimeConsistency);
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_UsesTimeConsistency_False()
        {
            Assert.IsFalse(CategoryProfileRegistry.Get("ADVERSE_EVENT").UsesTimeConsistency);
        }

        #endregion UsesArmCoverage / UsesTimeConsistency Tests

        #region RowRequiredFields Tests

        /**************************************************************/
        [TestMethod]
        public void Profile_PK_RowRequiredFields_HasParameterNameAndDoseRegimen()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("PK");

            CollectionAssert.AreEqual(new[] { "ParameterName", "DoseRegimen" }, profile.RowRequiredFields.ToArray());

            #endregion
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_RowRequiredFields_HasParameterNameAndTreatmentArm()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("ADVERSE_EVENT");

            CollectionAssert.AreEqual(new[] { "ParameterName", "TreatmentArm" }, profile.RowRequiredFields.ToArray());

            #endregion
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_Other_RowRequiredFields_HasParameterName()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("OTHER");

            CollectionAssert.AreEqual(new[] { "ParameterName" }, profile.RowRequiredFields.ToArray());

            #endregion
        }

        #endregion RowRequiredFields Tests

        #region AllowedValueTypes / CompletenessFields Tests

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_AllowedValueTypes_ContainsPercentage()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("ADVERSE_EVENT");
            Assert.IsTrue(profile.AllowedValueTypes.Contains("Percentage"));
            Assert.IsTrue(profile.AllowedValueTypes.Contains("Count"));
            Assert.IsTrue(profile.AllowedValueTypes.Contains("RelativeRiskReduction"));

            #endregion
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_PK_AllowedValueTypes_DoesNotContainPercentage()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("PK");
            Assert.IsFalse(profile.AllowedValueTypes.Contains("Percentage"));
            Assert.IsTrue(profile.AllowedValueTypes.Contains("Mean"));
            Assert.IsTrue(profile.AllowedValueTypes.Contains("GeometricMeanRatio"));

            #endregion
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_AE_CompletenessFields_MatchesLegacyList()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("ADVERSE_EVENT");
            CollectionAssert.AreEqual(
                new[] { "ParameterName", "TreatmentArm", "ArmN", "PrimaryValueType", "Unit" },
                profile.CompletenessFields.ToArray());

            #endregion
        }

        /**************************************************************/
        [TestMethod]
        public void Profile_DrugInteraction_CompletenessFields_Empty()
        {
            #region implementation

            var profile = CategoryProfileRegistry.Get("DRUG_INTERACTION");
            Assert.AreEqual(0, profile.CompletenessFields.Count);

            #endregion
        }

        #endregion AllowedValueTypes / CompletenessFields Tests

        #region Migration Guard Tests

        /**************************************************************/
        /// <summary>
        /// Locks every (UsesArmCoverage, UsesTimeConsistency, DefaultBoundType) tuple to its
        /// migrated value. Intentionally exhaustive — once <c>TableValidationService</c> and
        /// <c>ColumnStandardizationService</c> consume the registry directly, drift between
        /// the registry and any duplicate field would only show up at runtime; this guard
        /// turns it into a compile-time-fast unit-test failure instead.
        /// </summary>
        /// <seealso cref="MedRecProImportClass.Service.TransformationServices.TableValidationService"/>
        /// <seealso cref="MedRecProImportClass.Service.TransformationServices.ColumnStandardizationService"/>
        [TestMethod]
        public void Profile_AllCategories_FlagsAndBoundType_MatchMigratedValues()
        {
            #region implementation

            // (category, usesArmCoverage, usesTimeConsistency, defaultBoundType)
            var expectations = new (string Category, bool Arm, bool Time, string? Bound)[]
            {
                ("ADVERSE_EVENT",      true,  false, "95CI"),
                ("EFFICACY",           true,  false, "95CI"),
                ("PK",                 false, true,  "90CI"),
                ("BMD",                false, true,  "95CI"),
                ("DRUG_INTERACTION",   false, false, "90CI"),
                ("DOSING",             false, false, null),
                ("TISSUE_DISTRIBUTION",false, false, null),
                ("TEXT_DESCRIPTIVE",   false, false, null),
                ("OTHER",              false, false, null),
            };

            foreach (var (category, arm, time, bound) in expectations)
            {
                var profile = CategoryProfileRegistry.Get(category);
                Assert.AreEqual(arm,   profile.UsesArmCoverage,     $"UsesArmCoverage mismatch for {category}");
                Assert.AreEqual(time,  profile.UsesTimeConsistency, $"UsesTimeConsistency mismatch for {category}");
                Assert.AreEqual(bound, profile.DefaultBoundType,    $"DefaultBoundType mismatch for {category}");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AllowedValueTypes uses the default (case-sensitive) ordinal comparer. This is a
        /// behavior contract — RowValidationService relies on case-sensitive matching to surface
        /// casing drift in parser-emitted PrimaryValueType values via UNEXPECTED_VALUE_TYPE.
        /// </summary>
        /// <seealso cref="MedRecProImportClass.Service.TransformationServices.RowValidationService"/>
        [TestMethod]
        public void Profile_AllowedValueTypes_IsCaseSensitive()
        {
            #region implementation

            var ae = CategoryProfileRegistry.Get("ADVERSE_EVENT");

            Assert.IsTrue(ae.AllowedValueTypes.Contains("Percentage"),
                "Canonical PascalCase 'Percentage' must be present.");
            Assert.IsFalse(ae.AllowedValueTypes.Contains("percentage"),
                "Lowercase 'percentage' must NOT match — case-sensitive comparer required.");
            Assert.IsFalse(ae.AllowedValueTypes.Contains("PERCENTAGE"),
                "Uppercase 'PERCENTAGE' must NOT match — case-sensitive comparer required.");

            #endregion
        }

        #endregion Migration Guard Tests
    }
}
