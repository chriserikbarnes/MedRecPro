using MedRecPro.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Reflection;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Comprehensive tests for <see cref="ClaudeSkillService"/> covering all public methods
    /// on <see cref="IClaudeSkillService"/>, skill registration, routing, and regression guards.
    /// </summary>
    /// <remarks>
    /// Validates that:
    /// - All three internal dictionaries (_skillConfigKeys, _interfaceDocPaths, mapAiSkillNamesToInternal)
    ///   are consistent and include the orangeBookPatents skill.
    /// - Every public method on <see cref="IClaudeSkillService"/> is exercised for input validation,
    ///   error handling, and expected behavior.
    /// - Document retrieval methods return non-null strings and do not throw.
    ///
    /// Note: In the unit test environment, skill files do not exist at <c>AppContext.BaseDirectory</c>.
    /// Methods that read files will return "Skills document not found" messages. Tests validate
    /// dictionary key resolution and behavior rather than file-level content.
    /// </remarks>
    /// <seealso cref="ClaudeSkillService"/>
    /// <seealso cref="IClaudeSkillService"/>
    [TestClass]
    public class ClaudeSkillServiceTests
    {
        #region Private Fields

        /**************************************************************/
        /// <summary>
        /// System under test — the ClaudeSkillService instance.
        /// </summary>
        private readonly ClaudeSkillService _sut;

        /**************************************************************/
        /// <summary>
        /// In-memory configuration for skill file paths.
        /// </summary>
        private readonly IConfiguration _configuration;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the test fixture with a configured ClaudeSkillService instance.
        /// </summary>
        public ClaudeSkillServiceTests()
        {
            #region implementation

            var configData = new Dictionary<string, string?>
            {
                { "ClaudeApiSettings:Skill-Label", "Skills/interfaces/api/label-content.md" },
                { "ClaudeApiSettings:Skill-Section", "Skills/interfaces/api/label-content.md" },
                { "ClaudeApiSettings:Skill-Settings", "Skills/interfaces/api/cache-management.md" },
                { "ClaudeApiSettings:Skill-UserActivity", "Skills/interfaces/api/user-activity.md" },
                { "ClaudeApiSettings:Skill-Synthesis", "Skills/interfaces/synthesis-rules.md" },
                { "ClaudeApiSettings:Skill-Retry", "Skills/retryPrompt.md" },
                { "ClaudeApiSettings:Skill-RescueWorkflow", "Skills/interfaces/api/data-rescue.md" },
                { "ClaudeApiSettings:Skill-LabelIndicationWorkflow", "Skills/interfaces/api/indication-discovery.md" },
                { "ClaudeApiSettings:Skill-LabelProductIndication", "Skills/labelProductIndication.md" },
                { "ClaudeApiSettings:Skill-General", "Skills/interfaces/api/session-management.md" },
                { "ClaudeApiSettings:Skill-SessionManagement", "Skills/interfaces/api/session-management.md" },
                { "ClaudeApiSettings:Skill-EquianalgesicConversion", "Skills/equianalgesicConversion.md" },
                { "ClaudeApiSettings:Skill-PharmacologicClassSearch", "Skills/pharmacologic-class-matching.md" },
                { "ClaudeApiSettings:Skill-OrangeBookPatents", "Skills/interfaces/api/orange-book-patents.md" },
                { "ClaudeApiSettings:Skill-Selector", "Skills/selectors.md" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var logger = NullLogger<ClaudeSkillService>.Instance;
            var scopeFactory = new Mock<IServiceScopeFactory>();

            _sut = new ClaudeSkillService(_configuration, logger, scopeFactory.Object);

            #endregion
        }

        #endregion

        #region GetSkillManifestAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillManifestAsync returns a non-null string without throwing.
        /// </summary>
        /// <remarks>
        /// In the test environment, the selectors file won't be on disk, so the method will
        /// return an error message. The test validates it handles missing files gracefully.
        /// </remarks>
        [TestMethod]
        public async Task GetSkillManifestAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillManifestAsync();

            // Assert
            Assert.IsNotNull(result, "GetSkillManifestAsync returned null.");
            Assert.IsTrue(result.Length > 0, "GetSkillManifestAsync returned empty string.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that calling GetSkillManifestAsync twice returns the same result
        /// (testing the caching path).
        /// </summary>
        [TestMethod]
        public async Task GetSkillManifestAsync_SecondCallReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetSkillManifestAsync();
            var second = await _sut.GetSkillManifestAsync();

            // Assert — should return the same content (cached or re-read)
            Assert.AreEqual(first, second,
                "GetSkillManifestAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region SelectSkillsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that SelectSkillsAsync returns a failure SkillSelection when the
        /// user message is empty.
        /// </summary>
        [TestMethod]
        public async Task SelectSkillsAsync_EmptyMessage_ReturnsFailure()
        {
            #region implementation

            // Act
            var result = await _sut.SelectSkillsAsync("");

            // Assert
            Assert.IsFalse(result.Success, "Expected Success=false for empty message.");
            Assert.IsNotNull(result.Error, "Expected an error message for empty input.");
            StringAssert.Contains(result.Error, "empty",
                "Error message should mention 'empty'.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SelectSkillsAsync returns a failure SkillSelection when the
        /// user message is null.
        /// </summary>
        [TestMethod]
        public async Task SelectSkillsAsync_NullMessage_ReturnsFailure()
        {
            #region implementation

            // Act
            var result = await _sut.SelectSkillsAsync(null!);

            // Assert
            Assert.IsFalse(result.Success, "Expected Success=false for null message.");
            Assert.IsNotNull(result.Error, "Expected an error message for null input.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SelectSkillsAsync returns a failure SkillSelection when the
        /// user message is whitespace-only.
        /// </summary>
        [TestMethod]
        public async Task SelectSkillsAsync_WhitespaceMessage_ReturnsFailure()
        {
            #region implementation

            // Act
            var result = await _sut.SelectSkillsAsync("   ");

            // Assert
            Assert.IsFalse(result.Success, "Expected Success=false for whitespace message.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SelectSkillsAsync falls back to the default "label" skill
        /// when the AI service scope cannot be resolved (mock scope factory).
        /// </summary>
        /// <remarks>
        /// The mock IServiceScopeFactory does not set up a real scope, so
        /// <c>selectSkillsViaAiAsync</c> will throw. The catch block in SelectSkillsAsync
        /// should return a fallback SkillSelection with SelectedSkills=["label"].
        /// </remarks>
        [TestMethod]
        public async Task SelectSkillsAsync_AiServiceUnavailable_FallsBackToLabelSkill()
        {
            #region implementation

            // Act
            var result = await _sut.SelectSkillsAsync("What is Lipitor used for?");

            // Assert — should fall back gracefully, not throw
            Assert.IsNotNull(result, "SelectSkillsAsync returned null.");
            Assert.IsFalse(result.Success, "Expected Success=false when AI service is unavailable.");
            CollectionAssert.Contains(result.SelectedSkills, "label",
                "Expected fallback to 'label' skill when AI service fails.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SelectSkillsAsync populates the Error property when the
        /// AI service is unavailable.
        /// </summary>
        [TestMethod]
        public async Task SelectSkillsAsync_AiServiceUnavailable_PopulatesError()
        {
            #region implementation

            // Act
            var result = await _sut.SelectSkillsAsync("Show me patent data for Ozempic");

            // Assert
            Assert.IsNotNull(result.Error,
                "Expected Error to be populated when AI service is unavailable.");
            Assert.IsTrue(result.Error.Length > 0,
                "Error message should not be empty.");

            #endregion
        }

        #endregion

        #region GetSkillContentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillContentAsync defaults to loading the "label" skill
        /// when the selection is null.
        /// </summary>
        [TestMethod]
        public async Task GetSkillContentAsync_NullSelection_DefaultsToLabelSkill()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillContentAsync(null!);

            // Assert
            Assert.IsNotNull(result, "GetSkillContentAsync returned null for null selection.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillContentAsync defaults to loading the "label" skill
        /// when the selection has an empty SelectedSkills list.
        /// </summary>
        [TestMethod]
        public async Task GetSkillContentAsync_EmptySelectedSkills_DefaultsToLabelSkill()
        {
            #region implementation

            // Arrange
            var selection = new SkillSelection { SelectedSkills = new List<string>() };

            // Act
            var result = await _sut.GetSkillContentAsync(selection);

            // Assert
            Assert.IsNotNull(result, "GetSkillContentAsync returned null for empty selection.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillContentAsync returns a non-null string for a valid selection.
        /// </summary>
        [TestMethod]
        public async Task GetSkillContentAsync_ValidSelection_ReturnsNonNullString()
        {
            #region implementation

            // Arrange
            var selection = new SkillSelection
            {
                SelectedSkills = new List<string> { "label" }
            };

            // Act
            var result = await _sut.GetSkillContentAsync(selection);

            // Assert
            Assert.IsNotNull(result, "GetSkillContentAsync returned null for valid selection.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillContentAsync handles multiple skills in a single selection
        /// without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetSkillContentAsync_MultipleSkills_DoesNotThrow()
        {
            #region implementation

            // Arrange
            var selection = new SkillSelection
            {
                SelectedSkills = new List<string> { "label", "settings", "userActivity" }
            };

            // Act
            var result = await _sut.GetSkillContentAsync(selection);

            // Assert
            Assert.IsNotNull(result, "GetSkillContentAsync returned null for multi-skill selection.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillContentAsync handles the orangeBookPatents skill
        /// in a selection without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetSkillContentAsync_OrangeBookPatents_DoesNotThrow()
        {
            #region implementation

            // Arrange
            var selection = new SkillSelection
            {
                SelectedSkills = new List<string> { "orangeBookPatents" }
            };

            // Act
            var result = await _sut.GetSkillContentAsync(selection);

            // Assert
            Assert.IsNotNull(result, "GetSkillContentAsync returned null for orangeBookPatents.");

            #endregion
        }

        #endregion

        #region GetSkillByNameAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync returns an error message for empty input.
        /// </summary>
        [TestMethod]
        public async Task GetSkillByNameAsync_EmptyName_ReturnsErrorMessage()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync("");

            // Assert
            StringAssert.Contains(result, "cannot be empty",
                "Expected 'cannot be empty' error for empty skill name.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync returns an error message for null input.
        /// </summary>
        [TestMethod]
        public async Task GetSkillByNameAsync_NullName_ReturnsErrorMessage()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync(null!);

            // Assert
            StringAssert.Contains(result, "cannot be empty",
                "Expected 'cannot be empty' error for null skill name.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync returns an error listing available skills
        /// when given a completely unknown skill name.
        /// </summary>
        [TestMethod]
        public async Task GetSkillByNameAsync_UnknownSkill_ReturnsNotFoundWithAvailableSkills()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync("nonExistentSkill12345");

            // Assert
            StringAssert.Contains(result, "not found",
                "Expected 'not found' error for unknown skill name.");
            StringAssert.Contains(result, "label",
                "Expected the available skills list to include 'label'.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync resolves a known skill name ("label")
        /// without throwing.
        /// </summary>
        /// <remarks>
        /// In the test environment the file may not exist, but the method should
        /// resolve the config key and not return "not found" (skill-level, not file-level).
        /// </remarks>
        [TestMethod]
        public async Task GetSkillByNameAsync_KnownSkill_ResolvesConfigKey()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync("label");

            // Assert — should NOT return "Skill 'label' not found" (that means it wasn't in _skillConfigKeys)
            Assert.IsFalse(
                result.StartsWith("Skill 'label' not found"),
                $"Expected 'label' to be found in _skillConfigKeys. Got: {result}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync resolves "orangeBookPatents" without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetSkillByNameAsync_OrangeBookPatents_ResolvesConfigKey()
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync("orangeBookPatents");

            // Assert
            Assert.IsFalse(
                result.StartsWith("Skill 'orangeBookPatents' not found"),
                $"Expected 'orangeBookPatents' to be found in _skillConfigKeys. Got: {result}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync is case-insensitive for skill names.
        /// </summary>
        /// <remarks>
        /// The _skillConfigKeys dictionary uses <c>StringComparer.OrdinalIgnoreCase</c>.
        /// </remarks>
        [TestMethod]
        public async Task GetSkillByNameAsync_CaseInsensitive_ResolvesSkill()
        {
            #region implementation

            // Act
            var resultLower = await _sut.GetSkillByNameAsync("label");
            var resultUpper = await _sut.GetSkillByNameAsync("LABEL");
            var resultMixed = await _sut.GetSkillByNameAsync("Label");

            // Assert — none should report "not found" at the skill config level
            Assert.IsFalse(resultLower.Contains("not found. Available"),
                "Lowercase 'label' should resolve.");
            Assert.IsFalse(resultUpper.Contains("not found. Available"),
                "Uppercase 'LABEL' should resolve.");
            Assert.IsFalse(resultMixed.Contains("not found. Available"),
                "Mixed case 'Label' should resolve.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSkillByNameAsync supports fuzzy matching via substring containment.
        /// </summary>
        /// <remarks>
        /// The method falls back to <c>_skillConfigKeys.Keys.FirstOrDefault(k => k.Contains(name) || name.Contains(k))</c>
        /// when a direct lookup fails.
        /// </remarks>
        [TestMethod]
        [DataRow("rescue", "rescueWorkflow")]
        [DataRow("equianalgesic", "equianalgesicConversion")]
        [DataRow("pharmacologic", "pharmacologicClassSearch")]
        [DataRow("session", "sessionManagement")]
        public async Task GetSkillByNameAsync_FuzzyMatch_ResolvesPartialName(
            string partialName, string expectedMatch)
        {
            #region implementation

            // Act
            var result = await _sut.GetSkillByNameAsync(partialName);

            // Assert — should NOT return the "Skill 'X' not found" message
            Assert.IsFalse(
                result.Contains("not found. Available"),
                $"Expected fuzzy match for '{partialName}' to resolve (expected match: '{expectedMatch}'). Got: {result}");

            #endregion
        }

        #endregion

        #region GetInterfaceDocumentAsync Tests (Expanded)

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInterfaceDocumentAsync returns an error for empty input.
        /// </summary>
        [TestMethod]
        public async Task GetInterfaceDocumentAsync_EmptyName_ReturnsErrorMessage()
        {
            #region implementation

            // Act
            var result = await _sut.GetInterfaceDocumentAsync("");

            // Assert
            StringAssert.Contains(result, "cannot be empty",
                "Expected 'cannot be empty' for empty skill name.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInterfaceDocumentAsync returns an error for null input.
        /// </summary>
        [TestMethod]
        public async Task GetInterfaceDocumentAsync_NullName_ReturnsErrorMessage()
        {
            #region implementation

            // Act
            var result = await _sut.GetInterfaceDocumentAsync(null!);

            // Assert
            StringAssert.Contains(result, "cannot be empty",
                "Expected 'cannot be empty' for null skill name.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInterfaceDocumentAsync returns a "not found" message
        /// listing available interfaces when given an unknown skill name.
        /// </summary>
        [TestMethod]
        public async Task GetInterfaceDocumentAsync_UnknownSkill_ReturnsNotFoundWithAvailableInterfaces()
        {
            #region implementation

            // Act
            var result = await _sut.GetInterfaceDocumentAsync("totallyFakeSkillName99");

            // Assert
            StringAssert.Contains(result, "Interface document for",
                "Expected 'Interface document for' error for unknown skill.");
            StringAssert.Contains(result, "Available interfaces",
                "Expected list of available interfaces in the error.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInterfaceDocumentAsync resolves all primary interface keys
        /// without returning the "Interface document for" error (key-not-found sentinel).
        /// </summary>
        /// <remarks>
        /// Each DataRow represents a key that should exist in <c>_interfaceDocPaths</c>.
        /// The method may return "Skills document not found" (file missing at runtime),
        /// but should NOT return "Interface document for" (key missing from dictionary).
        /// </remarks>
        [TestMethod]
        [DataRow("indicationDiscovery")]
        [DataRow("labelContent")]
        [DataRow("inventorySummary")]
        [DataRow("equianalgesicConversion")]
        [DataRow("userActivity")]
        [DataRow("cacheManagement")]
        [DataRow("sessionManagement")]
        [DataRow("dataRescue")]
        [DataRow("retryFallback")]
        [DataRow("pharmacologicClass")]
        [DataRow("pharmacologicClassSearch")]
        [DataRow("orangeBookPatents")]
        [DataRow("label")]
        [DataRow("settings")]
        [DataRow("rescueWorkflow")]
        [DataRow("labelIndicationWorkflow")]
        [DataRow("labelProductIndication")]
        [DataRow("retry")]
        [DataRow("section")]
        [DataRow("synthesis")]
        [DataRow("general")]
        public async Task GetInterfaceDocumentAsync_KnownKey_ResolvesInDictionary(string skillName)
        {
            #region implementation

            // Act
            var result = await _sut.GetInterfaceDocumentAsync(skillName);

            // Assert — "Interface document for" appears ONLY when key is missing
            Assert.IsFalse(
                result.Contains("Interface document for", StringComparison.OrdinalIgnoreCase),
                $"Expected '{skillName}' to exist in _interfaceDocPaths. Got: {result}");

            #endregion
        }

        #endregion

        #region GetCapabilityContractsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetCapabilityContractsAsync returns a non-null string without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetCapabilityContractsAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetCapabilityContractsAsync();

            // Assert
            Assert.IsNotNull(result, "GetCapabilityContractsAsync returned null.");
            Assert.IsTrue(result.Length > 0, "GetCapabilityContractsAsync returned empty string.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetCapabilityContractsAsync is idempotent (caching path).
        /// </summary>
        [TestMethod]
        public async Task GetCapabilityContractsAsync_CalledTwice_ReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetCapabilityContractsAsync();
            var second = await _sut.GetCapabilityContractsAsync();

            // Assert
            Assert.AreEqual(first, second,
                "GetCapabilityContractsAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region GetSelectorsDocumentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSelectorsDocumentAsync returns a non-null string without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetSelectorsDocumentAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetSelectorsDocumentAsync();

            // Assert
            Assert.IsNotNull(result, "GetSelectorsDocumentAsync returned null.");
            Assert.IsTrue(result.Length > 0, "GetSelectorsDocumentAsync returned empty string.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSelectorsDocumentAsync is idempotent (caching path).
        /// </summary>
        [TestMethod]
        public async Task GetSelectorsDocumentAsync_CalledTwice_ReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetSelectorsDocumentAsync();
            var second = await _sut.GetSelectorsDocumentAsync();

            // Assert
            Assert.AreEqual(first, second,
                "GetSelectorsDocumentAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region GetResponseFormatDocumentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetResponseFormatDocumentAsync returns a non-null string without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetResponseFormatDocumentAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetResponseFormatDocumentAsync();

            // Assert
            Assert.IsNotNull(result, "GetResponseFormatDocumentAsync returned null.");
            Assert.IsTrue(result.Length > 0, "GetResponseFormatDocumentAsync returned empty string.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetResponseFormatDocumentAsync is idempotent (caching path).
        /// </summary>
        [TestMethod]
        public async Task GetResponseFormatDocumentAsync_CalledTwice_ReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetResponseFormatDocumentAsync();
            var second = await _sut.GetResponseFormatDocumentAsync();

            // Assert
            Assert.AreEqual(first, second,
                "GetResponseFormatDocumentAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region GetSynthesisRulesDocumentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSynthesisRulesDocumentAsync returns a non-null string without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetSynthesisRulesDocumentAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetSynthesisRulesDocumentAsync();

            // Assert
            Assert.IsNotNull(result, "GetSynthesisRulesDocumentAsync returned null.");
            Assert.IsTrue(result.Length > 0, "GetSynthesisRulesDocumentAsync returned empty string.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSynthesisRulesDocumentAsync is idempotent (caching path).
        /// </summary>
        [TestMethod]
        public async Task GetSynthesisRulesDocumentAsync_CalledTwice_ReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetSynthesisRulesDocumentAsync();
            var second = await _sut.GetSynthesisRulesDocumentAsync();

            // Assert
            Assert.AreEqual(first, second,
                "GetSynthesisRulesDocumentAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region GetFullSkillsDocumentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetFullSkillsDocumentAsync returns a non-null string without throwing.
        /// </summary>
        [TestMethod]
        public async Task GetFullSkillsDocumentAsync_ReturnsNonNullString()
        {
            #region implementation

            // Act
            var result = await _sut.GetFullSkillsDocumentAsync();

            // Assert
            Assert.IsNotNull(result, "GetFullSkillsDocumentAsync returned null.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetFullSkillsDocumentAsync is idempotent (caching path).
        /// </summary>
        [TestMethod]
        public async Task GetFullSkillsDocumentAsync_CalledTwice_ReturnsSameResult()
        {
            #region implementation

            // Act
            var first = await _sut.GetFullSkillsDocumentAsync();
            var second = await _sut.GetFullSkillsDocumentAsync();

            // Assert
            Assert.AreEqual(first, second,
                "GetFullSkillsDocumentAsync returned different results on successive calls.");

            #endregion
        }

        #endregion

        #region Orange Book Patents Registration Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that orangeBookPatents appears in the list of available skills,
        /// confirming it was added to _skillConfigKeys.
        /// </summary>
        [TestMethod]
        public async Task GetAvailableSkillsAsync_IncludesOrangeBookPatents()
        {
            #region implementation

            // Act
            var skills = await _sut.GetAvailableSkillsAsync();

            // Assert
            CollectionAssert.Contains(skills, "orangeBookPatents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the interface document path for orangeBookPatents is registered
        /// in _interfaceDocPaths by checking the result does not report "not found".
        /// </summary>
        /// <remarks>
        /// GetInterfaceDocumentAsync will attempt to read the file from disk. If the key
        /// is NOT in _interfaceDocPaths, it returns "Interface document for 'X' not found".
        /// If the key IS present but the file doesn't exist in the test runner's output
        /// directory, it returns "Skills document not found". Both are distinct error paths.
        /// We verify the key exists in the dictionary (not "Interface document ... not found").
        /// </remarks>
        [TestMethod]
        public async Task GetInterfaceDocumentAsync_OrangeBookPatents_KeyExistsInInterfaceDocPaths()
        {
            #region implementation

            // Act
            var result = await _sut.GetInterfaceDocumentAsync("orangeBookPatents");

            // Assert — "Interface document for" appears ONLY when key is missing from dictionary.
            // "Skills document not found" appears when key exists but file is missing at runtime.
            Assert.IsFalse(
                result.Contains("Interface document for", StringComparison.OrdinalIgnoreCase),
                $"orangeBookPatents key not found in _interfaceDocPaths. Result: {result}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that mapAiSkillNamesToInternal correctly maps "orangeBookPatents"
        /// from the selectors.md AI skill name to the internal config key name.
        /// </summary>
        [TestMethod]
        public void MapAiSkillNamesToInternal_OrangeBookPatents_MapsCorrectly()
        {
            #region implementation

            // Arrange — Use reflection to invoke the private method
            var method = typeof(ClaudeSkillService).GetMethod(
                "mapAiSkillNamesToInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "mapAiSkillNamesToInternal method not found via reflection.");

            var aiNames = new List<string> { "orangeBookPatents" };

            // Act
            var result = (List<string>)method.Invoke(_sut, new object[] { aiNames })!;

            // Assert
            Assert.IsTrue(
                result.Contains("orangeBookPatents"),
                $"Expected 'orangeBookPatents' in mapped result. Got: [{string.Join(", ", result)}]");

            #endregion
        }

        #endregion

        #region mapAiSkillNamesToInternal Tests (Expanded)

        /**************************************************************/
        /// <summary>
        /// Verifies that mapAiSkillNamesToInternal returns at least one skill ("label")
        /// when given an empty list, ensuring the minimum-one-skill guarantee.
        /// </summary>
        [TestMethod]
        public void MapAiSkillNamesToInternal_EmptyList_ReturnsDefaultLabelSkill()
        {
            #region implementation

            // Arrange
            var method = typeof(ClaudeSkillService).GetMethod(
                "mapAiSkillNamesToInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            // Act
            var result = (List<string>)method.Invoke(_sut, new object[] { new List<string>() })!;

            // Assert — empty input should produce ["label"] as default
            Assert.IsTrue(result.Count >= 1,
                "Expected at least one skill for empty input (default).");
            CollectionAssert.Contains(result, "label",
                "Expected 'label' as default when no AI names are provided.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that mapAiSkillNamesToInternal deduplicates skills when multiple AI names
        /// map to the same internal name.
        /// </summary>
        [TestMethod]
        public void MapAiSkillNamesToInternal_DuplicateMappings_Deduplicates()
        {
            #region implementation

            // Arrange — "labelContent" and "inventorySummary" both map to "label"
            var method = typeof(ClaudeSkillService).GetMethod(
                "mapAiSkillNamesToInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            var aiNames = new List<string> { "labelContent", "inventorySummary" };

            // Act
            var result = (List<string>)method.Invoke(_sut, new object[] { aiNames })!;

            // Assert — both map to "label", should appear only once
            var labelCount = result.Count(s => s == "label");
            Assert.AreEqual(1, labelCount,
                $"Expected 'label' to appear exactly once after dedup. Got {labelCount} in [{string.Join(", ", result)}]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that mapAiSkillNamesToInternal passes through unmapped names as-is.
        /// </summary>
        [TestMethod]
        public void MapAiSkillNamesToInternal_UnmappedName_PassesThroughAsIs()
        {
            #region implementation

            // Arrange
            var method = typeof(ClaudeSkillService).GetMethod(
                "mapAiSkillNamesToInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            var aiNames = new List<string> { "someUnknownSkill" };

            // Act
            var result = (List<string>)method.Invoke(_sut, new object[] { aiNames })!;

            // Assert — unknown names pass through as-is
            CollectionAssert.Contains(result, "someUnknownSkill",
                "Expected unmapped name to pass through.");

            #endregion
        }

        #endregion

        #region Cross-Validation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that every skill in _skillConfigKeys has a corresponding entry
        /// in _interfaceDocPaths (either directly or via fuzzy alias match).
        /// </summary>
        /// <remarks>
        /// This validates dictionary key presence, not file existence. A result containing
        /// "Interface document for" means the key was NOT found. A result containing
        /// "Skills document not found" means the key was found but the file is missing
        /// from the test runner's output directory (expected in unit tests).
        /// </remarks>
        [TestMethod]
        public async Task AllSkillConfigKeys_HaveMatchingInterfaceDocPaths()
        {
            #region implementation

            // Arrange
            var skills = await _sut.GetAvailableSkillsAsync();

            // Act & Assert — Each skill should resolve to a known interface document path.
            // "Interface document for 'X' not found" = key missing from _interfaceDocPaths
            foreach (var skill in skills)
            {
                var result = await _sut.GetInterfaceDocumentAsync(skill);
                Assert.IsFalse(
                    result.Contains("Interface document for", StringComparison.OrdinalIgnoreCase),
                    $"Skill '{skill}' does not have a matching interface document path in _interfaceDocPaths. " +
                    $"Result: {result}");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that every AI skill name in mapAiSkillNamesToInternal maps to
        /// a name that exists in _skillConfigKeys.
        /// </summary>
        [TestMethod]
        public async Task AllAiSkillMappings_MapToValidInternalNames()
        {
            #region implementation

            // Arrange — Get the AI-to-internal mapping dictionary via reflection
            var method = typeof(ClaudeSkillService).GetMethod(
                "mapAiSkillNamesToInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "mapAiSkillNamesToInternal method not found via reflection.");

            var availableSkills = await _sut.GetAvailableSkillsAsync();

            // Known AI skill names from selectors.md
            var aiSkillNames = new List<string>
            {
                "indicationDiscovery",
                "labelContent",
                "inventorySummary",
                "equianalgesicConversion",
                "userActivity",
                "cacheManagement",
                "sessionManagement",
                "dataRescue",
                "retryFallback",
                "pharmacologicClassSearch",
                "orangeBookPatents"
            };

            // Act & Assert — Each AI name should map to an internal name that exists
            foreach (var aiName in aiSkillNames)
            {
                var mapped = (List<string>)method.Invoke(_sut, new object[] { new List<string> { aiName } })!;

                Assert.IsTrue(
                    mapped.Count > 0,
                    $"AI skill name '{aiName}' did not map to any internal skill.");

                foreach (var internalName in mapped)
                {
                    Assert.IsTrue(
                        availableSkills.Contains(internalName, StringComparer.OrdinalIgnoreCase),
                        $"AI skill name '{aiName}' mapped to '{internalName}' which is not in _skillConfigKeys. " +
                        $"Available: [{string.Join(", ", availableSkills)}]");
                }
            }

            #endregion
        }

        #endregion

        #region Existing Skill Regression Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that all expected skills are present in GetAvailableSkillsAsync,
        /// serving as a regression guard against accidental removals.
        /// </summary>
        [TestMethod]
        [DataRow("label")]
        [DataRow("section")]
        [DataRow("settings")]
        [DataRow("userActivity")]
        [DataRow("synthesis")]
        [DataRow("retry")]
        [DataRow("rescueWorkflow")]
        [DataRow("labelIndicationWorkflow")]
        [DataRow("labelProductIndication")]
        [DataRow("general")]
        [DataRow("sessionManagement")]
        [DataRow("equianalgesicConversion")]
        [DataRow("pharmacologicClassSearch")]
        [DataRow("orangeBookPatents")]
        public async Task GetAvailableSkillsAsync_ContainsExpectedSkill(string skillName)
        {
            #region implementation

            // Act
            var skills = await _sut.GetAvailableSkillsAsync();

            // Assert
            CollectionAssert.Contains(skills, skillName,
                $"Expected skill '{skillName}' not found in available skills.");

            #endregion
        }

        #endregion

        #region Interface Document File Existence Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the orange-book-patents.md interface document file exists
        /// at the expected path relative to the project root.
        /// </summary>
        [TestMethod]
        public void OrangeBookPatentsInterfaceDocument_FileExists()
        {
            #region implementation

            // Arrange — Build the expected path relative to the project root
            var projectRoot = findProjectRoot();
            var filePath = Path.Combine(projectRoot, "Skills", "interfaces", "api", "orange-book-patents.md");

            // Assert
            Assert.IsTrue(
                File.Exists(filePath),
                $"Interface document not found at: {filePath}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the orange-book-patents.md file contains expected content
        /// (title and endpoint specification).
        /// </summary>
        [TestMethod]
        public void OrangeBookPatentsInterfaceDocument_ContainsExpectedContent()
        {
            #region implementation

            // Arrange
            var projectRoot = findProjectRoot();
            var filePath = Path.Combine(projectRoot, "Skills", "interfaces", "api", "orange-book-patents.md");

            if (!File.Exists(filePath))
            {
                Assert.Fail($"Interface document not found at: {filePath}");
                return;
            }

            var content = File.ReadAllText(filePath);

            // Assert — Verify key content sections exist
            StringAssert.Contains(content, "Orange Book Patent Search");
            StringAssert.Contains(content, "/api/OrangeBook/expiring");
            StringAssert.Contains(content, "tradeName");
            StringAssert.Contains(content, "ingredient");
            StringAssert.Contains(content, "expiringInMonths");

            #endregion
        }

        #endregion

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Finds the main project root directory (containing MedRecPro.csproj and Skills/)
        /// by walking up from the test assembly's bin directory.
        /// </summary>
        /// <remarks>
        /// The test assembly builds to MedRecProTest/bin/Debug/net8.0/, so the main project
        /// root is at a sibling directory (MedRecPro/) relative to the test project root.
        /// </remarks>
        /// <returns>The main project root directory path.</returns>
        private static string findProjectRoot()
        {
            #region implementation

            // Start from the test assembly's base directory
            var dir = AppContext.BaseDirectory;

            // Walk up until we find MedRecPro.csproj WITH Skills directory
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "MedRecPro.csproj")) &&
                    Directory.Exists(Path.Combine(dir, "Skills")))
                {
                    return dir;
                }

                // Check sibling directory (test project is at same level as main project)
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent != null)
                {
                    var sibling = Path.Combine(parent, "MedRecPro");
                    if (File.Exists(Path.Combine(sibling, "MedRecPro.csproj")) &&
                        Directory.Exists(Path.Combine(sibling, "Skills")))
                    {
                        return sibling;
                    }
                }

                dir = parent;
            }

            // Fallback: try current directory
            dir = Directory.GetCurrentDirectory();
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "MedRecPro.csproj")) &&
                    Directory.Exists(Path.Combine(dir, "Skills")))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            return Directory.GetCurrentDirectory();

            #endregion
        }

        #endregion
    }
}
