using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Reflection;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for the indication search feature in <see cref="ClaudeSearchService"/>.
    /// Tests cover reference file parsing, keyword pre-filtering, AI response parsing,
    /// validation response parsing, and orchestrator edge cases.
    /// </summary>
    /// <remarks>
    /// Private methods are tested via reflection since they contain significant
    /// logic that benefits from isolated testing. Public methods are tested
    /// through the service interface where possible.
    /// </remarks>
    /// <seealso cref="IClaudeSearchService"/>
    [TestClass]
    public class ClaudeSearchServiceIndicationTests
    {
        #region Private Fields

        /**************************************************************/
        /// <summary>
        /// System Under Test — ClaudeSearchService instance with mocked dependencies.
        /// </summary>
        private readonly ClaudeSearchService _sut;

        /**************************************************************/
        /// <summary>
        /// Configuration provider with in-memory test settings.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for the service under test.
        /// </summary>
        private readonly ILogger<ClaudeSearchService> _logger;

        #endregion

        #region Test Data Constants

        /**************************************************************/
        /// <summary>
        /// Sample reference file content matching the labelProductIndication.md format.
        /// </summary>
        private const string SampleReferenceContent = @"# Pharmaceutical Indications Reference

This file contains FDA indications data organized by product.

## Data Dictionary

- **ProductName**: The branded and generic product name separated by commas
- **UNII**: Unique Ingredient Identifier (FDA standard)
- **IndicationsSummary**: Combined indication text

## Data

ProductNames|UNII|IndicationsSummary
---
Acebutolol Hydrochloride|B025Y34C54|
# Acebutolol Hydrochloride Summary
Hypertension Acebutolol HCl capsules are indicated for the management of hypertension in adults.
Ventricular Arrhythmias Acebutolol HCl capsules are indicated in the management of ventricular premature beats.
---
Acarbose|T58MSI464G|
# Acarbose Summary
Acarbose tablets, USP are indicated as an adjunct to diet and exercise to improve glycemic control in adults with type 2 diabetes mellitus.
---
ADMELOG,insulin lispro|GFX7QIS1II|
# Insulin Lispro Summary
ADMELOG is indicated to improve glycemic control in adult and pediatric patients with diabetes mellitus.
---
Lisinopril,Zestril|7Q3P4BS2FD|
# Lisinopril Summary
Lisinopril is indicated for the treatment of hypertension in adult and pediatric patients 6 years of age and older to lower blood pressure. Lowering blood pressure reduces the risk of fatal and nonfatal cardiovascular events.
Lisinopril is indicated for the treatment of heart failure as adjunctive therapy.
---
Amlodipine Besylate|1J444QC288|
# Amlodipine Summary
Amlodipine besylate tablets are indicated for the treatment of hypertension, to lower blood pressure.
Amlodipine besylate tablets are indicated for the treatment of coronary artery disease.";

        /**************************************************************/
        /// <summary>
        /// Sample valid AI match response JSON.
        /// </summary>
        private const string ValidMatchResponseJson = @"{
  ""success"": true,
  ""matchedIndications"": [
    {
      ""unii"": ""B025Y34C54"",
      ""productNames"": ""Acebutolol Hydrochloride"",
      ""relevanceReason"": ""Indicated for management of hypertension"",
      ""confidence"": ""high""
    },
    {
      ""unii"": ""7Q3P4BS2FD"",
      ""productNames"": ""Lisinopril, Zestril"",
      ""relevanceReason"": ""Indicated for treatment of hypertension"",
      ""confidence"": ""high""
    }
  ],
  ""explanation"": ""Matched drugs indicated for hypertension management"",
  ""confidence"": ""high""
}";

        /**************************************************************/
        /// <summary>
        /// Sample valid AI validation response JSON.
        /// </summary>
        private const string ValidValidationResponseJson = @"{
  ""success"": true,
  ""validatedMatches"": [
    {
      ""unii"": ""B025Y34C54"",
      ""productName"": ""Acebutolol Hydrochloride"",
      ""confirmed"": true,
      ""validationReason"": ""FDA label explicitly states indicated for hypertension management"",
      ""confidence"": ""high""
    },
    {
      ""unii"": ""7Q3P4BS2FD"",
      ""productName"": ""Lisinopril"",
      ""confirmed"": true,
      ""validationReason"": ""FDA label states indicated for treatment of hypertension"",
      ""confidence"": ""high""
    }
  ],
  ""explanation"": ""Both products confirmed as antihypertensives""
}";

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the test class with a ClaudeSearchService instance
        /// configured with mocked dependencies.
        /// </summary>
        public ClaudeSearchServiceIndicationTests()
        {
            #region implementation

            var configData = new Dictionary<string, string?>
            {
                { "ClaudeApiSettings:Skill-LabelProductIndication", "Skills/labelProductIndication.md" },
                { "ClaudeApiSettings:Prompt-IndicationMatching", "Skills/prompts/indication-matching-prompt.md" },
                { "ClaudeApiSettings:Prompt-IndicationValidation", "Skills/prompts/indication-validation-prompt.md" },
                { "Security:DB:PKSecret", "test-secret-key-12345" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            _logger = NullLogger<ClaudeSearchService>.Instance;
            var scopeFactory = new Mock<IServiceScopeFactory>();

            // Use reflection to create the service without ApplicationDbContext validation
            // since we can't easily create one for unit tests without a real database
            _sut = createTestServiceInstance(scopeFactory.Object);

            #endregion
        }

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a ClaudeSearchService instance for testing using a mock DbContext.
        /// </summary>
        /// <param name="scopeFactory">The service scope factory to use.</param>
        /// <returns>A configured ClaudeSearchService instance.</returns>
        private ClaudeSearchService createTestServiceInstance(IServiceScopeFactory scopeFactory)
        {
            #region implementation

            // Create a minimal in-memory DbContext
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<MedRecPro.Data.ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            var dbContext = new MedRecPro.Data.ApplicationDbContext(options);

            return new ClaudeSearchService(dbContext, _configuration, _logger, scopeFactory);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Invokes a private method on the service under test via reflection.
        /// </summary>
        /// <typeparam name="T">The return type of the method.</typeparam>
        /// <param name="methodName">Name of the private method.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>The method's return value.</returns>
        private T invokePrivateMethod<T>(string methodName, params object[] parameters)
        {
            #region implementation

            var method = typeof(ClaudeSearchService)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, $"Method '{methodName}' not found on ClaudeSearchService");

            var result = method.Invoke(_sut, parameters);
            return (T)result!;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of sample indication reference entries for testing.
        /// </summary>
        /// <returns>List of test entries.</returns>
        private List<IndicationReferenceEntry> buildTestCandidates()
        {
            #region implementation

            return new List<IndicationReferenceEntry>
            {
                new()
                {
                    ProductNames = new List<string> { "Acebutolol Hydrochloride" },
                    UNII = "B025Y34C54",
                    IndicationsSummary = "Hypertension Acebutolol HCl capsules are indicated for the management of hypertension in adults."
                },
                new()
                {
                    ProductNames = new List<string> { "Acarbose" },
                    UNII = "T58MSI464G",
                    IndicationsSummary = "Acarbose tablets are indicated to improve glycemic control in adults with type 2 diabetes mellitus."
                },
                new()
                {
                    ProductNames = new List<string> { "ADMELOG", "insulin lispro" },
                    UNII = "GFX7QIS1II",
                    IndicationsSummary = "ADMELOG is indicated to improve glycemic control in patients with diabetes mellitus."
                },
                new()
                {
                    ProductNames = new List<string> { "Lisinopril", "Zestril" },
                    UNII = "7Q3P4BS2FD",
                    IndicationsSummary = "Lisinopril is indicated for the treatment of hypertension in adult and pediatric patients. Also indicated for heart failure."
                },
                new()
                {
                    ProductNames = new List<string> { "Amlodipine Besylate" },
                    UNII = "1J444QC288",
                    IndicationsSummary = "Amlodipine is indicated for the treatment of hypertension and coronary artery disease."
                }
            };

            #endregion
        }

        #endregion

        #region Reference File Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that parsing a valid reference file returns the correct number of entries.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_ValidContent_ReturnsCorrectEntryCount()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", SampleReferenceContent);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(5, result.Count, "Should parse 5 entries from sample content");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the first entry's product names are parsed correctly.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_FirstEntry_ParsesProductNamesCorrectly()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", SampleReferenceContent);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count > 0, "Should have at least one entry");

            var first = result[0];
            Assert.AreEqual(1, first.ProductNames.Count, "First entry should have 1 product name");
            Assert.AreEqual("Acebutolol Hydrochloride", first.ProductNames[0], "First product name should match");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the UNII code is correctly extracted from entries.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_EntryWithUNII_ExtractsCorrectCode()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", SampleReferenceContent);

            // Assert
            var acebutolol = result.FirstOrDefault(e => e.UNII == "B025Y34C54");
            Assert.IsNotNull(acebutolol, "Should find entry with UNII B025Y34C54");

            var acarbose = result.FirstOrDefault(e => e.UNII == "T58MSI464G");
            Assert.IsNotNull(acarbose, "Should find entry with UNII T58MSI464G");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that empty content returns an empty list.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_EmptyContent_ReturnsEmptyList()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", "");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Empty content should yield no entries");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that malformed entries (missing UNII) are skipped gracefully.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_MalformedEntry_SkipsGracefully()
        {
            #region implementation

            // Arrange — content with an entry missing a valid UNII
            var malformedContent = @"## Data

ProductNames|UNII|IndicationsSummary
---
ValidProduct|ABC12XYZ99|
# Valid Summary
This is a valid indication.
---
MalformedProduct||
# No UNII
This entry has no UNII and should be skipped.
---
AnotherValid|DEF45GHI88|
# Another Summary
Another valid indication text.";

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", malformedContent);

            // Assert
            Assert.AreEqual(2, result.Count, "Should skip entry with missing UNII");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multi-line indication text is captured fully.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_MultiLineIndication_CapturesFullText()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", SampleReferenceContent);

            // Assert
            var lisinopril = result.FirstOrDefault(e => e.UNII == "7Q3P4BS2FD");
            Assert.IsNotNull(lisinopril, "Should find Lisinopril entry");
            Assert.IsTrue(lisinopril.IndicationsSummary.Contains("hypertension"),
                "Indication should contain hypertension");
            Assert.IsTrue(lisinopril.IndicationsSummary.Contains("heart failure"),
                "Indication should contain heart failure across multiple lines");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that comma-separated product names are parsed into a list.
        /// </summary>
        [TestMethod]
        public void parseIndicationReferenceFile_CommaDelimitedProducts_ParsesMultipleNames()
        {
            #region implementation

            // Arrange & Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "parseIndicationReferenceFile", SampleReferenceContent);

            // Assert
            var insulin = result.FirstOrDefault(e => e.UNII == "GFX7QIS1II");
            Assert.IsNotNull(insulin, "Should find insulin lispro entry");
            Assert.AreEqual(2, insulin.ProductNames.Count, "Should have 2 product names");
            CollectionAssert.Contains(insulin.ProductNames, "ADMELOG", "Should contain brand name");
            CollectionAssert.Contains(insulin.ProductNames, "insulin lispro", "Should contain generic name");

            #endregion
        }

        #endregion

        #region Keyword Pre-Filter Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a hypertension query finds blood pressure entries.
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_HypertensionQuery_FindsBloodPressureEntries()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "high blood pressure", candidates);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count > 0, "Should find entries for high blood pressure");

            // Should find hypertension drugs
            Assert.IsTrue(result.Any(e => e.UNII == "B025Y34C54"),
                "Should include Acebutolol (hypertension)");
            Assert.IsTrue(result.Any(e => e.UNII == "7Q3P4BS2FD"),
                "Should include Lisinopril (hypertension)");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a diabetes query finds glycemic entries.
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_DiabetesQuery_FindsGlycemicEntries()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "diabetes", candidates);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count > 0, "Should find entries for diabetes");
            Assert.IsTrue(result.Any(e => e.UNII == "T58MSI464G"),
                "Should include Acarbose (diabetes)");
            Assert.IsTrue(result.Any(e => e.UNII == "GFX7QIS1II"),
                "Should include insulin lispro (diabetes)");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty query returns an empty result.
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_EmptyQuery_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "", candidates);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Empty query should return no results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a query with no matching terms returns empty.
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_NoMatches_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "xyznonexistent", candidates);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Non-matching query should return no results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that results are capped at MaxCandidatesForClaude.
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_ExceedsMaxCandidates_TruncatesToLimit()
        {
            #region implementation

            // Arrange — create 100 entries that all match "indicated"
            var manyEntries = Enumerable.Range(0, 100)
                .Select(i => new IndicationReferenceEntry
                {
                    ProductNames = new List<string> { $"Product{i}" },
                    UNII = $"UNII{i:D10}",
                    IndicationsSummary = "This product is indicated for the treatment of hypertension and blood pressure management."
                })
                .ToList();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "hypertension", manyEntries);

            // Assert — MaxCandidatesForClaude is 50
            Assert.IsTrue(result.Count <= 50, $"Should be capped at 50, got {result.Count}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that product name matches work (not just indication text).
        /// </summary>
        [TestMethod]
        public void preFilterIndicationsByKeyword_DrugNameQuery_MatchesProductNames()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<List<IndicationReferenceEntry>>(
                "preFilterIndicationsByKeyword", "lisinopril", candidates);

            // Assert
            Assert.IsTrue(result.Count > 0, "Should find entry by product name");
            Assert.IsTrue(result.Any(e => e.UNII == "7Q3P4BS2FD"),
                "Should find Lisinopril by name");

            #endregion
        }

        #endregion

        #region AI Match Response Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid JSON AI response is parsed correctly.
        /// </summary>
        [TestMethod]
        public void parseIndicationMatchResponse_ValidJson_ReturnsCorrectMatches()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<IndicationMatchResult>(
                "parseIndicationMatchResponse", ValidMatchResponseJson, candidates);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Should succeed with valid JSON");
            Assert.AreEqual(2, result.MatchedIndications.Count, "Should have 2 matches");
            Assert.AreEqual("B025Y34C54", result.MatchedIndications[0].UNII, "First match should be Acebutolol");
            Assert.AreEqual("high", result.MatchedIndications[0].Confidence, "First match should be high confidence");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that fabricated UNIIs not in the candidate list are rejected.
        /// </summary>
        [TestMethod]
        public void parseIndicationMatchResponse_InvalidUnii_FilteredOut()
        {
            #region implementation

            // Arrange — response with a UNII not in candidates
            var responseWithFakeUnii = @"{
  ""success"": true,
  ""matchedIndications"": [
    {
      ""unii"": ""B025Y34C54"",
      ""productNames"": ""Acebutolol"",
      ""relevanceReason"": ""Valid match"",
      ""confidence"": ""high""
    },
    {
      ""unii"": ""FAKE_UNII_999"",
      ""productNames"": ""Fabricated Drug"",
      ""relevanceReason"": ""This should be rejected"",
      ""confidence"": ""high""
    }
  ],
  ""explanation"": ""Test""
}";
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<IndicationMatchResult>(
                "parseIndicationMatchResponse", responseWithFakeUnii, candidates);

            // Assert
            Assert.IsTrue(result.Success, "Should succeed (valid match exists)");
            Assert.AreEqual(1, result.MatchedIndications.Count, "Should filter out fabricated UNII");
            Assert.AreEqual("B025Y34C54", result.MatchedIndications[0].UNII, "Should keep valid UNII");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty response returns failure.
        /// </summary>
        [TestMethod]
        public void parseIndicationMatchResponse_EmptyResponse_ReturnsFalse()
        {
            #region implementation

            // Arrange
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<IndicationMatchResult>(
                "parseIndicationMatchResponse", "", candidates);

            // Assert
            Assert.IsFalse(result.Success, "Empty response should not be successful");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that JSON wrapped in markdown code blocks is extracted correctly.
        /// </summary>
        [TestMethod]
        public void parseIndicationMatchResponse_MarkdownCodeBlock_ExtractsJson()
        {
            #region implementation

            // Arrange
            var wrappedResponse = "Here are the matches:\n```json\n" + ValidMatchResponseJson + "\n```";
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<IndicationMatchResult>(
                "parseIndicationMatchResponse", wrappedResponse, candidates);

            // Assert
            Assert.IsTrue(result.Success, "Should extract JSON from markdown code block");
            Assert.AreEqual(2, result.MatchedIndications.Count, "Should parse both matches");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that malformed JSON triggers error handling.
        /// </summary>
        [TestMethod]
        public void parseIndicationMatchResponse_MalformedJson_ReturnsFallback()
        {
            #region implementation

            // Arrange
            var malformedJson = "{ this is not valid json at all }}}";
            var candidates = buildTestCandidates();

            // Act
            var result = invokePrivateMethod<IndicationMatchResult>(
                "parseIndicationMatchResponse", malformedJson, candidates);

            // Assert
            Assert.IsFalse(result.Success, "Malformed JSON should not succeed");
            Assert.IsNotNull(result.Error, "Should have an error message");

            #endregion
        }

        #endregion

        #region Validation Response Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid validation JSON response is parsed correctly.
        /// </summary>
        [TestMethod]
        public void parseIndicationValidationResponse_ValidJson_ReturnsCorrectVerdicts()
        {
            #region implementation

            // Act
            var result = invokePrivateMethod<IndicationValidationResult>(
                "parseIndicationValidationResponse", ValidValidationResponseJson);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Should succeed with valid JSON");
            Assert.AreEqual(2, result.ValidatedMatches.Count, "Should have 2 validated matches");
            Assert.IsTrue(result.ValidatedMatches[0].Confirmed, "First match should be confirmed");
            Assert.AreEqual("high", result.ValidatedMatches[0].Confidence, "First match should be high confidence");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rejected matches are parsed with Confirmed=false.
        /// </summary>
        [TestMethod]
        public void parseIndicationValidationResponse_RejectedMatch_ParsesCorrectly()
        {
            #region implementation

            // Arrange
            var mixedResponse = @"{
  ""success"": true,
  ""validatedMatches"": [
    {
      ""unii"": ""B025Y34C54"",
      ""productName"": ""Acebutolol"",
      ""confirmed"": true,
      ""validationReason"": ""Confirmed for hypertension"",
      ""confidence"": ""high""
    },
    {
      ""unii"": ""T58MSI464G"",
      ""productName"": ""Acarbose"",
      ""confirmed"": false,
      ""validationReason"": ""Acarbose is for diabetes, not hypertension"",
      ""confidence"": ""high""
    }
  ],
  ""explanation"": ""One confirmed, one rejected""
}";

            // Act
            var result = invokePrivateMethod<IndicationValidationResult>(
                "parseIndicationValidationResponse", mixedResponse);

            // Assert
            Assert.IsTrue(result.Success, "Should succeed");
            Assert.AreEqual(2, result.ValidatedMatches.Count, "Should have 2 entries");

            var confirmed = result.ValidatedMatches.Where(v => v.Confirmed).ToList();
            var rejected = result.ValidatedMatches.Where(v => !v.Confirmed).ToList();

            Assert.AreEqual(1, confirmed.Count, "Should have 1 confirmed");
            Assert.AreEqual(1, rejected.Count, "Should have 1 rejected");
            Assert.AreEqual("B025Y34C54", confirmed[0].UNII, "Acebutolol should be confirmed");
            Assert.AreEqual("T58MSI464G", rejected[0].UNII, "Acarbose should be rejected");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty validation response returns failure.
        /// </summary>
        [TestMethod]
        public void parseIndicationValidationResponse_EmptyResponse_ReturnsFalse()
        {
            #region implementation

            // Act
            var result = invokePrivateMethod<IndicationValidationResult>(
                "parseIndicationValidationResponse", "");

            // Assert
            Assert.IsFalse(result.Success, "Empty response should not succeed");

            #endregion
        }

        #endregion

        #region Orchestrator Edge Case Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty query returns an error result.
        /// </summary>
        [TestMethod]
        public async Task SearchByIndicationAsync_EmptyQuery_ReturnsError()
        {
            #region implementation

            // Act
            var result = await _sut.SearchByIndicationAsync("");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Empty query should fail");
            Assert.IsNotNull(result.Error, "Should have an error message");
            StringAssert.Contains(result.Error, "empty", "Error should mention empty query");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a whitespace-only query returns an error result.
        /// </summary>
        [TestMethod]
        public async Task SearchByIndicationAsync_WhitespaceQuery_ReturnsError()
        {
            #region implementation

            // Act
            var result = await _sut.SearchByIndicationAsync("   ");

            // Assert
            Assert.IsFalse(result.Success, "Whitespace query should fail");
            Assert.IsNotNull(result.Error, "Should have an error message");

            #endregion
        }

        #endregion

        #region DTO Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that IndicationSearchResult initializes with correct defaults.
        /// </summary>
        [TestMethod]
        public void IndicationSearchResult_DefaultValues_AreCorrect()
        {
            #region implementation

            // Act
            var result = new IndicationSearchResult();

            // Assert
            Assert.IsFalse(result.Success, "Default Success should be false");
            Assert.AreEqual(string.Empty, result.OriginalQuery, "Default OriginalQuery should be empty");
            Assert.IsNotNull(result.MatchedIndications, "MatchedIndications should not be null");
            Assert.AreEqual(0, result.MatchedIndications.Count, "MatchedIndications should be empty");
            Assert.IsNotNull(result.ProductsByIndication, "ProductsByIndication should not be null");
            Assert.AreEqual(0, result.ProductsByIndication.Count, "ProductsByIndication should be empty");
            Assert.AreEqual(0, result.TotalProductCount, "Default TotalProductCount should be 0");
            Assert.IsNotNull(result.LabelLinks, "LabelLinks should not be null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that IndicationMatch DTO properties are set correctly.
        /// </summary>
        [TestMethod]
        public void IndicationMatch_SetProperties_RetainsValues()
        {
            #region implementation

            // Act
            var match = new IndicationMatch
            {
                UNII = "ABC123",
                ProductNames = "TestDrug, GenericDrug",
                RelevanceReason = "Indicated for test condition",
                Confidence = "high"
            };

            // Assert
            Assert.AreEqual("ABC123", match.UNII);
            Assert.AreEqual("TestDrug, GenericDrug", match.ProductNames);
            Assert.AreEqual("Indicated for test condition", match.RelevanceReason);
            Assert.AreEqual("high", match.Confidence);

            #endregion
        }

        #endregion
    }
}
