using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="ClaudeApiCorrectionService"/> public methods.
    /// Uses mock <see cref="HttpMessageHandler"/> to intercept API calls and return
    /// controlled responses without hitting the live Anthropic API.
    /// </summary>
    /// <seealso cref="IClaudeApiCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionSettings"/>
    [TestClass]
    public class ClaudeApiCorrectionServiceTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Creates a minimal ParsedObservation for testing.
        /// </summary>
        private static ParsedObservation createTestObservation(
            int rowSeq,
            int cellSeq,
            string paramName,
            string rawValue,
            int? tableId = 1)
        {
            #region implementation

            return new ParsedObservation
            {
                SourceRowSeq = rowSeq,
                SourceCellSeq = cellSeq,
                ParameterName = paramName,
                RawValue = rawValue,
                TextTableID = tableId,
                PrimaryValueType = "Numeric",
                SecondaryValueType = null,
                TreatmentArm = "Placebo",
                Caption = "Test Table",
                TableCategory = "ADVERSE_EVENT",
                ParseConfidence = 0.9,
                ParseRule = "plain_number",
                ValidationFlags = null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mock HttpMessageHandler that returns the specified response.
        /// </summary>
        private static Mock<HttpMessageHandler> createMockHandler(
            string responseJson,
            HttpStatusCode status = HttpStatusCode.OK)
        {
            #region implementation

            var apiResponse = new
            {
                id = "msg_test",
                type = "message",
                role = "assistant",
                content = new[]
                {
                    new { type = "text", text = responseJson }
                },
                stop_reason = "end_turn",
                usage = new { input_tokens = 100, output_tokens = 50 }
            };

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(
                        JsonConvert.SerializeObject(apiResponse),
                        Encoding.UTF8,
                        "application/json")
                });

            return mockHandler;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mock HttpMessageHandler that throws a TaskCanceledException (timeout).
        /// </summary>
        private static Mock<HttpMessageHandler> createTimeoutHandler()
        {
            #region implementation

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out"));

            return mockHandler;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the service under test with the given handler and optional settings.
        /// </summary>
        private static ClaudeApiCorrectionService createService(
            HttpMessageHandler handler,
            ClaudeApiCorrectionSettings? settings = null)
        {
            #region implementation

            settings ??= new ClaudeApiCorrectionSettings
            {
                ApiKey = "test-api-key",
                Enabled = true,
                MaxObservationsPerRequest = 50,
                DelayBetweenRequestsMs = 0,
                Model = "claude-haiku-4-5-20251001",
                MaxTokens = 2000,
                Temperature = 0.0
            };

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.anthropic.com/")
            };

            var options = Options.Create(settings);
            var logger = NullLogger<ClaudeApiCorrectionService>.Instance;

            return new ClaudeApiCorrectionService(httpClient, options, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the number of times SendAsync was called on the mock handler.
        /// </summary>
        private static void verifySendAsyncCallCount(Mock<HttpMessageHandler> mockHandler, int expectedCount)
        {
            #region implementation

            mockHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(expectedCount),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            #endregion
        }

        #endregion

        #region CorrectBatchAsync — Empty/Disabled Tests

        /**************************************************************/
        /// <summary>
        /// Empty input returns empty output without making any API call.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_EmptyList_ReturnsEmptyList()
        {
            #region implementation

            var mockHandler = createMockHandler("[]");
            var service = createService(mockHandler.Object);

            var result = await service.CorrectBatchAsync(new List<ParsedObservation>());

            Assert.AreEqual(0, result.Count);
            verifySendAsyncCallCount(mockHandler, 0);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When Enabled=false, observations pass through unmodified with no API call.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_DisabledSetting_ReturnsOriginalUnchanged()
        {
            #region implementation

            var mockHandler = createMockHandler("[]");
            var settings = new ClaudeApiCorrectionSettings
            {
                ApiKey = "test-api-key",
                Enabled = false,
                DelayBetweenRequestsMs = 0
            };
            var service = createService(mockHandler.Object, settings);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Headache", result[0].ParameterName);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            verifySendAsyncCallCount(mockHandler, 0);

            #endregion
        }

        #endregion

        #region CorrectBatchAsync — Successful Correction Tests

        /**************************************************************/
        /// <summary>
        /// Valid corrections from API are applied to matching observations.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_ValidCorrections_AppliesChanges()
        {
            #region implementation

            var corrections = new[]
            {
                new { sourceRowSeq = 1, sourceCellSeq = 1, field = "PrimaryValueType", oldValue = "Numeric", newValue = "Percentage", reason = "AE table with % format hint" }
            };

            var mockHandler = createMockHandler(JsonConvert.SerializeObject(corrections));
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AI_CORRECTED flag is appended to ValidationFlags when correction is applied.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_ValidCorrections_AppendsValidationFlag()
        {
            #region implementation

            var corrections = new[]
            {
                new { sourceRowSeq = 1, sourceCellSeq = 1, field = "PrimaryValueType", oldValue = "Numeric", newValue = "Percentage", reason = "test" }
            };

            var mockHandler = createMockHandler(JsonConvert.SerializeObject(corrections));
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            // Pre-set an existing validation flag
            observations[0].ValidationFlags = "PCT_CHECK:PASS";

            var result = await service.CorrectBatchAsync(observations);

            Assert.IsTrue(result[0].ValidationFlags!.Contains("AI_CORRECTED:PrimaryValueType"));
            Assert.IsTrue(result[0].ValidationFlags!.Contains("PCT_CHECK:PASS"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// API returns empty corrections array — observations unchanged.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_NoCorrectionsReturned_ReturnsOriginalUnchanged()
        {
            #region implementation

            var mockHandler = createMockHandler("[]");
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual("Headache", result[0].ParameterName);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple corrections for the same row are all applied.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_MultipleCorrectionsPerRow_AllApplied()
        {
            #region implementation

            var corrections = new[]
            {
                new { sourceRowSeq = 1, sourceCellSeq = 1, field = "PrimaryValueType", oldValue = "Numeric", newValue = "Percentage", reason = "test1" },
                new { sourceRowSeq = 1, sourceCellSeq = 1, field = "TreatmentArm", oldValue = "Placebo", newValue = "EVISTA", reason = "test2" }
            };

            var mockHandler = createMockHandler(JsonConvert.SerializeObject(corrections));
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            Assert.AreEqual("EVISTA", result[0].TreatmentArm);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("AI_CORRECTED:PrimaryValueType"));
            Assert.IsTrue(result[0].ValidationFlags!.Contains("AI_CORRECTED:TreatmentArm"));

            #endregion
        }

        #endregion

        #region CorrectBatchAsync — Failure Handling Tests

        /**************************************************************/
        /// <summary>
        /// HTTP 500 response — returns original observations unchanged.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_ApiFailure_ReturnsOriginalUnchanged()
        {
            #region implementation

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal Server Error")
                });

            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Request timeout — returns original observations unchanged.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_ApiTimeout_ReturnsOriginalUnchanged()
        {
            #region implementation

            var mockHandler = createTimeoutHandler();
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// API returns invalid JSON — returns original observations unchanged.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_InvalidJson_ReturnsOriginalUnchanged()
        {
            #region implementation

            var mockHandler = createMockHandler("this is not valid json at all");
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);

            #endregion
        }

        #endregion

        #region CorrectBatchAsync — Grouping and Splitting Tests

        /**************************************************************/
        /// <summary>
        /// Observations from different TextTableIDs produce separate API requests.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_GroupsByTextTableID_SendsSeparateRequests()
        {
            #region implementation

            var mockHandler = createMockHandler("[]");
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2", tableId: 100),
                createTestObservation(2, 1, "Nausea", "3.1", tableId: 100),
                createTestObservation(1, 1, "Cmax", "12.5", tableId: 200)
            };

            await service.CorrectBatchAsync(observations);

            // Two table groups = two API calls
            verifySendAsyncCallCount(mockHandler, 2);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Large table exceeding MaxObservationsPerRequest is split into sub-batches.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_LargeTableSplit_RespectsMaxObservationsPerRequest()
        {
            #region implementation

            var mockHandler = createMockHandler("[]");
            var settings = new ClaudeApiCorrectionSettings
            {
                ApiKey = "test-api-key",
                Enabled = true,
                MaxObservationsPerRequest = 3,
                DelayBetweenRequestsMs = 0
            };
            var service = createService(mockHandler.Object, settings);

            // 8 observations in same table → ceil(8/3) = 3 API calls
            var observations = new List<ParsedObservation>();
            for (int i = 1; i <= 8; i++)
            {
                observations.Add(createTestObservation(i, 1, $"Param{i}", $"{i}.0", tableId: 1));
            }

            await service.CorrectBatchAsync(observations);

            verifySendAsyncCallCount(mockHandler, 3);

            #endregion
        }

        #endregion

        #region CorrectBatchAsync — Invalid Correction Handling Tests

        /**************************************************************/
        /// <summary>
        /// Correction referencing a non-existent row is silently ignored.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_CorrectionForNonexistentRow_Ignored()
        {
            #region implementation

            var corrections = new[]
            {
                new { sourceRowSeq = 999, sourceCellSeq = 999, field = "PrimaryValueType", oldValue = "Numeric", newValue = "Percentage", reason = "ghost row" }
            };

            var mockHandler = createMockHandler(JsonConvert.SerializeObject(corrections));
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };

            var result = await service.CorrectBatchAsync(observations);

            // Original unchanged — the correction was for a non-existent row
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Correction referencing an invalid field name is silently ignored.
        /// </summary>
        [TestMethod]
        public async Task CorrectBatchAsync_CorrectionForInvalidField_Ignored()
        {
            #region implementation

            var corrections = new[]
            {
                new { sourceRowSeq = 1, sourceCellSeq = 1, field = "PrimaryValue", oldValue = "5.2", newValue = "99.9", reason = "not a correctable field" }
            };

            var mockHandler = createMockHandler(JsonConvert.SerializeObject(corrections));
            var service = createService(mockHandler.Object);

            var observations = new List<ParsedObservation>
            {
                createTestObservation(1, 1, "Headache", "5.2")
            };
            observations[0].PrimaryValue = 5.2;

            var result = await service.CorrectBatchAsync(observations);

            // PrimaryValue is NOT in the correctable fields set
            Assert.AreEqual(5.2, result[0].PrimaryValue);
            Assert.IsNull(result[0].ValidationFlags);

            #endregion
        }

        #endregion
    }
}
