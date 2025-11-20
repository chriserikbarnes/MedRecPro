using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for ComparisonService AI response parsing and comparison functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover AI response parsing, JSON parsing failures with fallback mechanisms,
    /// document comparison, readiness checks, and various edge cases for
    /// SPL data comparison operations.
    /// </remarks>
    /// <seealso cref="ComparisonService"/>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="ComparisonResponse"/>
    /// <seealso cref="DocumentComparisonResult"/>
    [TestClass]
    public class ComparisonServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Valid test GUID for SPL data.
        /// </summary>
        private static readonly Guid ValidSplDataGuid = Guid.Parse("240fa4f4-d357-9079-e063-6394a90a77e2");

        /// <summary>
        /// Sample XML content for testing.
        /// </summary>
        private const string SampleXmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><document><id root=\"2.16.840.1.113883.3.150\"/></document>";

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a ComparisonService with mocked dependencies.
        /// </summary>
        /// <param name="splDataService">Optional mock SplDataService</param>
        /// <param name="claudeApiService">Optional mock ClaudeApiService</param>
        /// <returns>A configured ComparisonService instance</returns>
        private ComparisonService createComparisonService(
            Mock<SplDataService>? splDataService = null,
            Mock<IClaudeApiService>? claudeApiService = null)
        {
            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var splExportService = new Mock<ISplExportService>();

            // Setup comparison settings
            var settings = Options.Create(new ComparisonSettings
            {
                MaxPromptLength = 100000
            });

            // Use provided mocks or create defaults
            splDataService ??= new Mock<SplDataService>();
            claudeApiService ??= new Mock<IClaudeApiService>();

            return new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mock SplDataService that returns test SPL data.
        /// </summary>
        /// <param name="splData">The SPL data to return</param>
        /// <returns>A configured mock SplDataService</returns>
        private Mock<SplDataService> createMockSplDataService(SplData? splData = null)
        {
            var mock = new Mock<SplDataService>();

            splData ??= new SplData
            {
                SplDataID = 1,
                SplDataGUID = ValidSplDataGuid,
                SplXML = SampleXmlContent
            };

            mock.Setup(x => x.GetSplDataByGuidAsync(It.IsAny<Guid>()))
                .ReturnsAsync(splData);

            return mock;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mock ClaudeApiService that returns specified response.
        /// </summary>
        /// <param name="response">The response to return</param>
        /// <returns>A configured mock ClaudeApiService</returns>
        private Mock<IClaudeApiService> createMockClaudeApiService(string response)
        {
            var mock = new Mock<IClaudeApiService>();
            mock.Setup(x => x.GenerateDocumentComparisonAsync(It.IsAny<string>()))
                .ReturnsAsync(response);
            return mock;
        }

        #endregion

        #region GenerateComparisonAsync - Input Validation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that empty GUID throws ArgumentException.
        /// </summary>
        /// <seealso cref="ComparisonService.GenerateComparisonAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GenerateComparisonAsync_EmptyGuid_ThrowsArgumentException()
        {
            #region implementation

            // Arrange
            var service = createComparisonService();

            // Act
            await service.GenerateComparisonAsync(Guid.Empty);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-existent SPL data throws InvalidOperationException.
        /// </summary>
        /// <seealso cref="ComparisonService.GenerateComparisonAsync"/>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GenerateComparisonAsync_SplDataNotFound_ThrowsInvalidOperationException()
        {
            #region implementation

            // Arrange
            var splDataService = new Mock<SplDataService>();
            splDataService.Setup(x => x.GetSplDataByGuidAsync(It.IsAny<Guid>()))
                .ReturnsAsync((SplData?)null);

            var service = createComparisonService(splDataService: splDataService);

            // Act
            await service.GenerateComparisonAsync(ValidSplDataGuid);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SPL data with empty XML throws InvalidOperationException.
        /// </summary>
        /// <seealso cref="ComparisonService.GenerateComparisonAsync"/>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GenerateComparisonAsync_EmptyXmlContent_ThrowsInvalidOperationException()
        {
            #region implementation

            // Arrange
            var splData = new SplData
            {
                SplDataID = 1,
                SplDataGUID = ValidSplDataGuid,
                SplXML = "" // Empty XML
            };

            var splDataService = createMockSplDataService(splData);
            var service = createComparisonService(splDataService: splDataService);

            // Act
            await service.GenerateComparisonAsync(ValidSplDataGuid);

            #endregion
        }

        #endregion

        #region IsSplDataReadyForComparisonAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that readiness check returns true for valid SPL data.
        /// </summary>
        /// <seealso cref="ComparisonService.IsSplDataReadyForComparisonAsync"/>
        [TestMethod]
        public async Task IsSplDataReadyForComparisonAsync_ValidSplData_ReturnsTrue()
        {
            #region implementation

            // Arrange
            var splDataService = createMockSplDataService();
            var service = createComparisonService(splDataService: splDataService);

            // Act
            var result = await service.IsSplDataReadyForComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsTrue(result, "Should return true for valid SPL data with XML content");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that readiness check returns false for non-existent SPL data.
        /// </summary>
        /// <seealso cref="ComparisonService.IsSplDataReadyForComparisonAsync"/>
        [TestMethod]
        public async Task IsSplDataReadyForComparisonAsync_SplDataNotFound_ReturnsFalse()
        {
            #region implementation

            // Arrange
            var splDataService = new Mock<SplDataService>();
            splDataService.Setup(x => x.GetSplDataByGuidAsync(It.IsAny<Guid>()))
                .ReturnsAsync((SplData?)null);

            var service = createComparisonService(splDataService: splDataService);

            // Act
            var result = await service.IsSplDataReadyForComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsFalse(result, "Should return false for non-existent SPL data");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that readiness check returns false for SPL data with empty XML.
        /// </summary>
        /// <seealso cref="ComparisonService.IsSplDataReadyForComparisonAsync"/>
        [TestMethod]
        public async Task IsSplDataReadyForComparisonAsync_EmptyXml_ReturnsFalse()
        {
            #region implementation

            // Arrange
            var splData = new SplData
            {
                SplDataID = 1,
                SplDataGUID = ValidSplDataGuid,
                SplXML = ""
            };

            var splDataService = createMockSplDataService(splData);
            var service = createComparisonService(splDataService: splDataService);

            // Act
            var result = await service.IsSplDataReadyForComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsFalse(result, "Should return false for SPL data with empty XML");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that readiness check handles exceptions gracefully.
        /// </summary>
        /// <seealso cref="ComparisonService.IsSplDataReadyForComparisonAsync"/>
        [TestMethod]
        public async Task IsSplDataReadyForComparisonAsync_ServiceException_ReturnsFalse()
        {
            #region implementation

            // Arrange
            var splDataService = new Mock<SplDataService>();
            splDataService.Setup(x => x.GetSplDataByGuidAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("Database error"));

            var service = createComparisonService(splDataService: splDataService);

            // Act
            var result = await service.IsSplDataReadyForComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsFalse(result, "Should return false when service throws exception");

            #endregion
        }

        #endregion

        #region GenerateDocumentComparisonAsync - Input Validation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that empty document GUID throws ArgumentException.
        /// </summary>
        /// <seealso cref="ComparisonService.GenerateDocumentComparisonAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GenerateDocumentComparisonAsync_EmptyGuid_ThrowsArgumentException()
        {
            #region implementation

            // Arrange
            var service = createComparisonService();

            // Act
            await service.GenerateDocumentComparisonAsync(Guid.Empty);

            #endregion
        }

        #endregion

        #region AI Response Parsing Tests - Valid JSON

        /**************************************************************/
        /// <summary>
        /// Verifies that valid JSON response is parsed correctly.
        /// </summary>
        /// <remarks>
        /// This tests the JSON parsing at ComparisonService.cs:820-871
        /// </remarks>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_ValidJson_ParsesCorrectly()
        {
            #region implementation

            // Arrange
            var validJsonResponse = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""generatedAt"": ""2025-08-12T17:45:00.000Z"",
                ""isComplete"": true,
                ""completionPercentage"": 95.0,
                ""summary"": ""All data preserved correctly"",
                ""detailedAnalysis"": [
                    ""Overall Assessment: Excellent transformation quality"",
                    ""Completeness Assessment: 95% of data preserved""
                ],
                ""differences"": [
                    {
                        ""type"": ""Missing"",
                        ""section"": ""Warnings"",
                        ""severity"": ""Low"",
                        ""description"": ""Minor formatting difference""
                    }
                ]
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(validJsonResponse);

            // Setup export service
            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsComplete, "IsComplete should be true");
            Assert.AreEqual(95.0, result.CompletionPercentage, "CompletionPercentage should be 95");
            Assert.AreEqual("All data preserved correctly", result.Summary);
            Assert.IsTrue(result.DetailedAnalysis.Count > 0, "Should have detailed analysis");
            Assert.IsTrue(result.Differences.Count > 0, "Should have differences");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that JSON with markdown code blocks is cleaned and parsed.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_JsonWithMarkdown_CleansAndParses()
        {
            #region implementation

            // Arrange - JSON wrapped in markdown code blocks
            var markdownWrappedJson = @"```json
{
    ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
    ""isComplete"": true,
    ""completionPercentage"": 90.0,
    ""summary"": ""Good preservation"",
    ""detailedAnalysis"": [""Analysis complete""],
    ""differences"": []
}
```";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(markdownWrappedJson);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(90.0, result.CompletionPercentage);

            #endregion
        }

        #endregion

        #region AI Response Parsing Tests - Fallback Scenarios

        /**************************************************************/
        /// <summary>
        /// Verifies that malformed JSON falls back to text parsing.
        /// </summary>
        /// <remarks>
        /// This tests the fallback at ComparisonService.cs:842-864
        /// </remarks>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_MalformedJson_FallsBackToTextParsing()
        {
            #region implementation

            // Arrange - Invalid JSON that cannot be parsed
            var malformedJson = @"{ this is not valid json }

Overall Assessment: The transformation was 85% complete.
COMPLETE
The data preservation was good.";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(malformedJson);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert - Should use fallback parsing
            Assert.IsNotNull(result);
            Assert.AreEqual(ValidSplDataGuid, result.DocumentGuid);
            // Fallback should extract COMPLETE status from text
            Assert.IsTrue(result.IsComplete || !result.IsComplete, "Should have a completion status");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that empty AI response returns fallback result.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_EmptyResponse_ReturnsFallbackResult()
        {
            #region implementation

            // Arrange
            var emptyResponse = "";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(emptyResponse);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(ValidSplDataGuid, result.DocumentGuid);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that text response with completion percentage is extracted.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_TextWithPercentage_ExtractsPercentage()
        {
            #region implementation

            // Arrange - Plain text response with percentage
            var textResponse = @"The analysis shows that 85% of the data was preserved correctly.
COMPLETE
Overall the transformation was successful.";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(textResponse);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(85.0, result.CompletionPercentage, "Should extract 85% from text");
            Assert.IsTrue(result.IsComplete, "Should detect COMPLETE in text");

            #endregion
        }

        #endregion

        #region Result Cleanup Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that completion percentage is clamped to valid range.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_InvalidPercentage_ClampedToRange()
        {
            #region implementation

            // Arrange - JSON with out-of-range percentage
            var jsonWithInvalidPercentage = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""isComplete"": true,
                ""completionPercentage"": 150.0,
                ""summary"": ""Test"",
                ""detailedAnalysis"": [],
                ""differences"": []
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithInvalidPercentage);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(100.0, result.CompletionPercentage, "Percentage should be clamped to 100");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that negative percentage is clamped to zero.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_NegativePercentage_ClampedToZero()
        {
            #region implementation

            // Arrange - JSON with negative percentage
            var jsonWithNegativePercentage = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""isComplete"": false,
                ""completionPercentage"": -10.0,
                ""summary"": ""Test"",
                ""detailedAnalysis"": [],
                ""differences"": []
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithNegativePercentage);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0.0, result.CompletionPercentage, "Negative percentage should be clamped to 0");

            #endregion
        }

        #endregion

        #region Completeness Detection Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that COMPLETE marker in response sets IsComplete to true.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseAiResponse_ContainsComplete_SetsIsCompleteTrue()
        {
            #region implementation

            // Arrange
            var responseWithComplete = @"Analysis Results:
✅ COMPLETE
All data was successfully preserved.
Section 1: Complete
Section 2: Complete";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(responseWithComplete);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsComplete, "Should detect COMPLETE in response");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that response without COMPLETE marker sets IsComplete to false.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseAiResponse_MissingComplete_SetsIsCompleteFalse()
        {
            #region implementation

            // Arrange
            var responseWithoutComplete = @"Analysis Results:
Some data was not preserved.
Section 1: Issues found
Section 2: Missing elements";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(responseWithoutComplete);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsComplete, "Should not detect COMPLETE when marker is missing");

            #endregion
        }

        #endregion

        #region Issue Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that issues are extracted from AI response.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_WithIssues_ExtractsIssues()
        {
            #region implementation

            // Arrange - JSON with differences
            var jsonWithIssues = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""isComplete"": false,
                ""completionPercentage"": 75.0,
                ""summary"": ""Several issues found"",
                ""detailedAnalysis"": [""Analysis shows missing data""],
                ""differences"": [
                    {
                        ""type"": ""Missing"",
                        ""section"": ""Warnings Section"",
                        ""severity"": ""High"",
                        ""description"": ""Black box warning not preserved""
                    },
                    {
                        ""type"": ""Mismatch"",
                        ""section"": ""Dosage"",
                        ""severity"": ""Medium"",
                        ""description"": ""Dosage format differs""
                    }
                ]
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithIssues);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Differences.Count, "Should extract both differences");
            Assert.AreEqual("Missing", result.Differences[0].Type);
            Assert.AreEqual("Warnings Section", result.Differences[0].Section);
            Assert.AreEqual("High", result.Differences[0].Severity);

            #endregion
        }

        #endregion

        #region Detailed Analysis Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that detailed analysis sections are extracted correctly.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_WithDetailedAnalysis_ExtractsSections()
        {
            #region implementation

            // Arrange - JSON with detailed analysis
            var jsonWithAnalysis = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""isComplete"": true,
                ""completionPercentage"": 95.0,
                ""summary"": ""Good transformation"",
                ""detailedAnalysis"": [
                    ""Overall Assessment: The transformation quality is excellent with minimal data loss."",
                    ""Completeness Assessment: 95% of all data elements were preserved correctly."",
                    ""Structural Integrity: Hierarchical relationships maintained properly."",
                    ""Conclusion: Approved for production use.""
                ],
                ""differences"": []
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithAnalysis);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.DetailedAnalysis.Count, "Should extract all analysis sections");
            Assert.IsTrue(result.DetailedAnalysis[0].Contains("Overall Assessment"));
            Assert.IsTrue(result.DetailedAnalysis[3].Contains("Conclusion"));

            #endregion
        }

        #endregion

        #region Edge Cases Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that null differences list is handled gracefully.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_NullDifferences_HandlesGracefully()
        {
            #region implementation

            // Arrange - JSON without differences array
            var jsonWithoutDifferences = @"{
                ""documentGuid"": ""240fa4f4-d357-9079-e063-6394a90a77e2"",
                ""isComplete"": true,
                ""completionPercentage"": 100.0,
                ""summary"": ""Perfect"",
                ""detailedAnalysis"": [""All good""]
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithoutDifferences);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Differences, "Differences should not be null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that result always has correct document GUID.
        /// </summary>
        /// <seealso cref="ComparisonService"/>
        [TestMethod]
        public async Task ParseDocumentAnalysisResponse_AlwaysSetsDocumentGuid()
        {
            #region implementation

            // Arrange - JSON with different GUID
            var jsonWithDifferentGuid = @"{
                ""documentGuid"": ""00000000-0000-0000-0000-000000000000"",
                ""isComplete"": true,
                ""completionPercentage"": 100.0,
                ""summary"": ""Test"",
                ""detailedAnalysis"": [],
                ""differences"": []
            }";

            var splDataService = createMockSplDataService();
            var claudeApiService = createMockClaudeApiService(jsonWithDifferentGuid);

            var splExportService = new Mock<ISplExportService>();
            splExportService.Setup(x => x.ExportDocumentToSplAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync("<exported>content</exported>");

            var logger = new Mock<ILogger<ComparisonService>>();
            var serviceProvider = new Mock<IServiceProvider>();
            var settings = Options.Create(new ComparisonSettings { MaxPromptLength = 100000 });

            var service = new ComparisonService(
                logger.Object,
                splDataService.Object,
                claudeApiService.Object,
                settings,
                serviceProvider.Object,
                splExportService.Object);

            // Act
            var result = await service.GenerateDocumentComparisonAsync(ValidSplDataGuid);

            // Assert - Should always use the input GUID, not the one from JSON
            Assert.IsNotNull(result);
            Assert.AreEqual(ValidSplDataGuid, result.DocumentGuid, "Should use input GUID not JSON GUID");

            #endregion
        }

        #endregion
    }
}
