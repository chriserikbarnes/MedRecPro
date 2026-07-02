using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises the six planned <see cref="ClaudeApiService"/> public methods
    /// (plus <c>GenerateCleanMarkdownAsync</c>) with deterministic fakes:
    /// a <see cref="QueuedHttpMessageHandler"/> serving synthetic Anthropic
    /// envelopes, a <see cref="FakeClaudeSkillService"/>, a real
    /// <see cref="ConversationStore"/>, and an EF InMemory context.
    /// </summary>
    /// <remarks>
    /// The service funnels every AI call through a single HTTP POST to the
    /// Anthropic Messages endpoint, so the queued handler drives all flows and
    /// its recorded requests prove which paths made (or skipped) HTTP calls.
    /// </remarks>
    /// <seealso cref="ClaudeApiService"/>
    /// <seealso cref="QueuedHttpMessageHandler"/>
    /// <seealso cref="FakeClaudeSkillService"/>
    [TestClass]
    public class ClaudeApiServicePublicSurfaceTests
    {
        #region implementation

        /// <summary>
        /// Interpretation JSON matching the wire contract the parser expects.
        /// </summary>
        private const string InterpretationJson =
            "{\"success\":true,\"endpoints\":[{\"method\":\"GET\",\"path\":\"/api/Label/labeler/search\"," +
            "\"queryParameters\":{\"labelerNameSearch\":\"Pfizer\"}}],\"explanation\":\"Searching labelers\"," +
            "\"requiresAuthentication\":false,\"isDirectResponse\":false}";

        #region GetSystemContextAsync

        /**************************************************************/
        /// <summary>
        /// Verifies the system context aggregates feature flags, demo-mode
        /// messaging, entity counts, and the hardcoded section/view catalogs.
        /// </summary>
        /// <seealso cref="ClaudeApiService.GetSystemContextAsync"/>
        [TestMethod]
        public async Task GetSystemContextAsync_WithSeededContext_ReturnsFeatureFlagsCountsViewsAndSections()
        {
            #region implementation
            // Arrange - two documents, one product, demo mode 120 minutes.
            var harness = new ApiHarness(new Dictionary<string, string?>
            {
                ["DemoModeSettings:Enabled"] = "true",
                ["DemoModeSettings:ResetIntervalMinutes"] = "120",
                ["FeatureFlags:SplImportEnabled"] = "true",
                ["FeatureFlags:ComparisonAnalysisEnabled"] = "false"
            });
            harness.Db.Set<Label.Document>().Add(new Label.Document { DocumentGUID = Guid.NewGuid() });
            harness.Db.Set<Label.Document>().Add(new Label.Document { DocumentGUID = Guid.NewGuid() });
            harness.Db.Set<Label.Product>().Add(new Label.Product { ProductName = "ASPIRIN" });
            harness.Db.SaveChanges();

            // Act
            var authenticated = await harness.Service.GetSystemContextAsync(isAuthenticated: true, userId: "user-1");
            var anonymous = await harness.Service.GetSystemContextAsync(isAuthenticated: false, userId: null);

            // Assert - counts and flags.
            Assert.AreEqual(2, authenticated.DocumentCount);
            Assert.AreEqual(1, authenticated.ProductCount);
            Assert.IsFalse(authenticated.IsDatabaseEmpty);
            Assert.IsTrue(authenticated.IsDemoMode);
            StringAssert.Contains(authenticated.DemoModeMessage, "every 2 hours");
            Assert.IsTrue(authenticated.ImportEnabled, "Import stays enabled for authenticated users.");
            Assert.IsFalse(authenticated.ComparisonAnalysisEnabled);

            // Catalogs are hardcoded and non-empty.
            CollectionAssert.Contains(authenticated.AvailableSections, "PharmacologicClass");
            CollectionAssert.Contains(authenticated.AvailableViews, "ndc");

            // Import gate: unauthenticated users never see import enabled.
            Assert.IsFalse(anonymous.ImportEnabled);
            Assert.AreEqual("user-1", authenticated.UserId);
            #endregion
        }

        #endregion

        #region InterpretRequestAsync

        /**************************************************************/
        /// <summary>
        /// Verifies a null request is rejected.
        /// </summary>
        /// <seealso cref="ClaudeApiService.InterpretRequestAsync"/>
        [TestMethod]
        public async Task InterpretRequestAsync_NullRequest_ThrowsArgumentNullException()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => harness.Service.InterpretRequestAsync(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies an empty user message returns the validation failure shape
        /// without calling the AI.
        /// </summary>
        /// <seealso cref="ClaudeApiService.InterpretRequestAsync"/>
        [TestMethod]
        public async Task InterpretRequestAsync_EmptyMessage_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();

            // Act
            var result = await harness.Service.InterpretRequestAsync(new AiAgentRequest { UserMessage = "   " });

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("User message cannot be empty.", result.Error);
            Assert.AreEqual(0, harness.Handler.Requests.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the full interpretation flow: skill selection, one HTTP
        /// call, JSON parsing into endpoints, and conversation persistence.
        /// </summary>
        /// <seealso cref="ClaudeApiService.InterpretRequestAsync"/>
        [TestMethod]
        public async Task InterpretRequestAsync_FakeClaudeJson_ReturnsParsedInterpretationAndStoresMessages()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            harness.Handler.EnqueueClaudeText(InterpretationJson);

            // Act
            var result = await harness.Service.InterpretRequestAsync(
                new AiAgentRequest { UserMessage = "find pfizer labels" });

            // Assert - parsed interpretation.
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Endpoints.Count);
            Assert.AreEqual("/api/Label/labeler/search", result.Endpoints[0].Path);
            Assert.AreEqual("GET", result.Endpoints[0].Method);
            Assert.AreEqual("Searching labelers", result.Explanation);
            Assert.IsFalse(string.IsNullOrEmpty(result.ConversationId));

            // Conversation history captured user + assistant turns.
            var messages = harness.Store.GetMessages(result.ConversationId!);
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("user", messages[0].Role);
            Assert.AreEqual("find pfizer labels", messages[0].Content);
            Assert.AreEqual("assistant", messages[1].Role);

            // Exactly one HTTP call using the configured model.
            Assert.AreEqual(1, harness.Handler.Requests.Count);
            StringAssert.Contains(harness.Handler.Requests[0].Body, "\"model\":\"claude-test\"");
            StringAssert.Contains(harness.Handler.Requests[0].Body, "find pfizer labels");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a direct-response skill selection short-circuits before
        /// any HTTP call.
        /// </summary>
        /// <seealso cref="ClaudeApiService.InterpretRequestAsync"/>
        [TestMethod]
        public async Task InterpretRequestAsync_DirectResponseSelection_ShortCircuitsWithoutHttp()
        {
            #region implementation
            // Arrange - skill service answers directly.
            var harness = new ApiHarness();
            harness.Skills.SelectionToReturn = new SkillSelection
            {
                SelectedSkills = new List<string>(),
                IsDirectResponse = true,
                DirectResponse = "Here is a direct answer.",
                Explanation = "Answered without endpoints"
            };

            // Act
            var result = await harness.Service.InterpretRequestAsync(
                new AiAgentRequest { UserMessage = "what can you do?" });

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsDirectResponse);
            Assert.AreEqual("Here is a direct answer.", result.DirectResponse);
            Assert.AreEqual(0, harness.Handler.Requests.Count, "Direct responses must not call the AI endpoint.");

            // The assistant turn is still recorded.
            var messages = harness.Store.GetMessages(result.ConversationId!);
            Assert.AreEqual("assistant", messages[^1].Role);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the credit-exhaustion branch: an API error body containing
        /// the credit substring maps to the token-limiter error message.
        /// </summary>
        /// <seealso cref="ClaudeApiService.InterpretRequestAsync"/>
        [TestMethod]
        public async Task InterpretRequestAsync_CreditExhausted_ReturnsTokenLimiterError()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            harness.Handler.EnqueueResponse(HttpStatusCode.BadRequest,
                "{\"error\":{\"message\":\"Your credit balance is too low to access the API\"}}");

            // Act
            var result = await harness.Service.InterpretRequestAsync(
                new AiAgentRequest { UserMessage = "find labels" });

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("The AI token limiter has been exhausted. Please try again once it has reset.", result.Error);
            Assert.IsNotNull(result.Suggestions);
            Assert.IsFalse(string.IsNullOrEmpty(result.ConversationId),
                "The store-issued conversation id survives into the error result.");
            #endregion
        }

        #endregion

        #region SynthesizeResultsAsync

        /**************************************************************/
        /// <summary>
        /// Verifies a null synthesis request is rejected.
        /// </summary>
        /// <seealso cref="ClaudeApiService.SynthesizeResultsAsync"/>
        [TestMethod]
        public async Task SynthesizeResultsAsync_NullRequest_ThrowsArgumentNullException()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => harness.Service.SynthesizeResultsAsync(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies an empty original query returns the incomplete-response
        /// shape without HTTP.
        /// </summary>
        /// <seealso cref="ClaudeApiService.SynthesizeResultsAsync"/>
        [TestMethod]
        public async Task SynthesizeResultsAsync_EmptyQuery_ReturnsIncompleteResponse()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();

            // Act
            var result = await harness.Service.SynthesizeResultsAsync(new AiSynthesisRequest { OriginalQuery = " " });

            // Assert
            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual("Unable to synthesize results without the original query.", result.Response);
            Assert.AreEqual(0, harness.Handler.Requests.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the synthesis flow parses the AI's JSON into the synthesis
        /// DTO and carries the conversation ID through.
        /// </summary>
        /// <seealso cref="ClaudeApiService.SynthesizeResultsAsync"/>
        [TestMethod]
        public async Task SynthesizeResultsAsync_FakeClaudeText_ReturnsParsedSynthesis()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            var conversation = harness.Store.Create("user-1");
            harness.Handler.EnqueueClaudeText("{\"response\":\"Here is your answer\",\"isComplete\":true}");

            var request = new AiSynthesisRequest
            {
                OriginalQuery = "compare aspirin labels",
                ConversationId = conversation.ConversationId,
                ExecutedEndpoints = new List<AiEndpointResult>
                {
                    new AiEndpointResult
                    {
                        Specification = new AiEndpointSpecification { Method = "GET", Path = "/api/Label/document/search" },
                        StatusCode = 200,
                        Result = new { title = "ASPIRIN" },
                        ExecutionTimeMs = 5
                    }
                }
            };

            // Act
            var result = await harness.Service.SynthesizeResultsAsync(request);

            // Assert
            Assert.AreEqual("Here is your answer", result.Response);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(conversation.ConversationId, result.ConversationId);
            Assert.AreEqual(1, harness.Handler.Requests.Count);

            // Synthesis never appends conversation messages.
            Assert.AreEqual(0, harness.Store.GetMessages(conversation.ConversationId).Count);
            #endregion
        }

        #endregion

        #region GetSkillsDocumentAsync / SelectSkillsViaAiAsync

        /**************************************************************/
        /// <summary>
        /// Verifies GetSkillsDocumentAsync is a pure delegation to the skill
        /// service.
        /// </summary>
        /// <seealso cref="ClaudeApiService.GetSkillsDocumentAsync"/>
        [TestMethod]
        public async Task GetSkillsDocumentAsync_DelegatesToSkillService()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            harness.Skills.FullSkillsDocument = "=== DELEGATED SKILLS DOC ===";

            // Act
            var document = await harness.Service.GetSkillsDocumentAsync();

            // Assert
            Assert.AreEqual("=== DELEGATED SKILLS DOC ===", document);
            CollectionAssert.Contains(harness.Skills.Calls, nameof(FakeClaudeSkillService.GetFullSkillsDocumentAsync));
            Assert.AreEqual(0, harness.Handler.Requests.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies blank user message or selectors document each return the
        /// documented validation failure.
        /// </summary>
        /// <seealso cref="ClaudeApiService.SelectSkillsViaAiAsync"/>
        [TestMethod]
        public async Task SelectSkillsViaAiAsync_EmptyMessageOrSelectors_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();

            // Act
            var noMessage = await harness.Service.SelectSkillsViaAiAsync("  ", "selectors doc");
            var noSelectors = await harness.Service.SelectSkillsViaAiAsync("find labels", "  ");

            // Assert
            Assert.IsFalse(noMessage.Success);
            Assert.AreEqual("User message cannot be empty.", noMessage.Error);
            Assert.IsFalse(noSelectors.Success);
            Assert.AreEqual("Selectors document cannot be empty.", noSelectors.Error);
            Assert.AreEqual(0, harness.Handler.Requests.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the skill-selection flow parses the AI JSON and force-sets
        /// Success to true on any successful parse (documented behavior).
        /// </summary>
        /// <seealso cref="ClaudeApiService.SelectSkillsViaAiAsync"/>
        [TestMethod]
        public async Task SelectSkillsViaAiAsync_FakeClaudeJson_ReturnsSelectedSkills()
        {
            #region implementation
            // Arrange - JSON deliberately says success:false to pin the
            // parser's force-set-true behavior.
            var harness = new ApiHarness();
            harness.Handler.EnqueueClaudeText(
                "{\"success\":false,\"selectedSkills\":[\"label\",\"indication\"],\"explanation\":\"picked\"," +
                "\"isDirectResponse\":false,\"directResponse\":null}");

            // Act
            var result = await harness.Service.SelectSkillsViaAiAsync("find beta blockers", "=== SELECTORS ===");

            // Assert
            Assert.IsTrue(result.Success, "A parseable selection is always reported successful.");
            CollectionAssert.AreEqual(new[] { "label", "indication" }, result.SelectedSkills);
            Assert.AreEqual("picked", result.Explanation);
            Assert.AreEqual(1, harness.Handler.Requests.Count);
            #endregion
        }

        #endregion

        #region RetryInterpretationAsync

        /**************************************************************/
        /// <summary>
        /// Verifies null request and missing failed-results are rejected.
        /// </summary>
        /// <seealso cref="ClaudeApiService.RetryInterpretationAsync"/>
        [TestMethod]
        public async Task RetryInterpretationAsync_InvalidArguments_Throws()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            var failed = new List<AiEndpointResult> { new AiEndpointResult { StatusCode = 404 } };

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => harness.Service.RetryInterpretationAsync(null!, failed, 1));
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => harness.Service.RetryInterpretationAsync(
                    new AiAgentRequest { UserMessage = "query" }, new List<AiEndpointResult>(), 1));
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => harness.Service.RetryInterpretationAsync(
                    new AiAgentRequest { UserMessage = "query" }, null!, 1));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies attempts beyond the maximum return the apologetic direct
        /// fallback without any HTTP or skill-service work.
        /// </summary>
        /// <seealso cref="ClaudeApiService.RetryInterpretationAsync"/>
        [TestMethod]
        public async Task RetryInterpretationAsync_AttemptBeyondMax_ReturnsDirectFallbackResponse()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            var failed = new List<AiEndpointResult>
            {
                new AiEndpointResult
                {
                    Specification = new AiEndpointSpecification { Method = "GET", Path = "/api/Label/section/x" },
                    StatusCode = 404
                }
            };

            // Act - attempt 4 exceeds the max of 3.
            var result = await harness.Service.RetryInterpretationAsync(
                new AiAgentRequest { UserMessage = "find sections" }, failed, attemptNumber: 4);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsDirectResponse);
            StringAssert.StartsWith(result.DirectResponse, "I apologize");
            StringAssert.Contains(result.DirectResponse, "**What I tried:**");
            StringAssert.Contains(result.DirectResponse, "/api/Label/section/x");
            Assert.AreEqual(0, harness.Handler.Requests.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a normal retry attempt builds the retry prompt, calls the
        /// AI once, and stamps the attempt number on the parsed result.
        /// </summary>
        /// <seealso cref="ClaudeApiService.RetryInterpretationAsync"/>
        [TestMethod]
        public async Task RetryInterpretationAsync_FakeClaudeRetry_ReturnsRetryInterpretation()
        {
            #region implementation
            // Arrange
            var harness = new ApiHarness();
            harness.Skills.SkillsByName["retry"] = "=== RETRY GUIDANCE ===";
            harness.Handler.EnqueueClaudeText(InterpretationJson);
            var failed = new List<AiEndpointResult>
            {
                new AiEndpointResult
                {
                    Specification = new AiEndpointSpecification { Method = "GET", Path = "/api/Label/old-path" },
                    StatusCode = 500,
                    Error = "server error"
                }
            };

            // Act
            var result = await harness.Service.RetryInterpretationAsync(
                new AiAgentRequest { UserMessage = "find labels again" }, failed, attemptNumber: 2);

            // Assert
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.RetryAttempt);
            Assert.AreEqual(1, result.Endpoints.Count);
            Assert.AreEqual(1, harness.Handler.Requests.Count);

            // The retry prompt embeds the failed endpoint and retry context.
            StringAssert.Contains(harness.Handler.Requests[0].Body, "/api/Label/old-path");
            StringAssert.Contains(harness.Handler.Requests[0].Body, "retry attempt 2 of 3");
            #endregion
        }

        #endregion

        #region GenerateCleanMarkdownAsync (surface-guard extra)

        /**************************************************************/
        /// <summary>
        /// Verifies markdown cleanup returns the AI's cleaned text on success
        /// and falls back to the raw markdown on API failure.
        /// </summary>
        /// <seealso cref="ClaudeApiService.GenerateCleanMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateCleanMarkdownAsync_SuccessAndFailure_ReturnCleanedOrRawMarkdown()
        {
            #region implementation
            // Arrange - success path.
            var harness = new ApiHarness();
            harness.Handler.EnqueueClaudeText("# ASPIRIN\n\nClean content");

            // Act
            var cleaned = await harness.Service.GenerateCleanMarkdownAsync("<raw>ugly</raw>", "ASPIRIN");

            // Assert
            Assert.AreEqual("# ASPIRIN\n\nClean content", cleaned);

            // Arrange - failure path swallows the error and returns the input.
            harness.Handler.EnqueueResponse(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

            // Act
            var fallback = await harness.Service.GenerateCleanMarkdownAsync("<raw>ugly</raw>", "ASPIRIN");

            // Assert
            Assert.AreEqual("<raw>ugly</raw>", fallback);
            #endregion
        }

        #endregion

        #region Harness

        /**************************************************************/
        /// <summary>
        /// Bundles the queued HTTP handler, fake skill service, conversation
        /// store, InMemory context, and the service under test.
        /// </summary>
        /// <seealso cref="ClaudeApiService"/>
        private sealed class ApiHarness
        {
            #region implementation

            /**************************************************************/
            /// <summary>Queued Anthropic response handler.</summary>
            public QueuedHttpMessageHandler Handler { get; } = new();

            /**************************************************************/
            /// <summary>Configurable skill service fake.</summary>
            public FakeClaudeSkillService Skills { get; } = new();

            /**************************************************************/
            /// <summary>Real in-memory conversation store.</summary>
            public ConversationStore Store { get; } = new(NullLogger<ConversationStore>.Instance);

            /**************************************************************/
            /// <summary>EF InMemory application context (unique per harness).</summary>
            public ApplicationDbContext Db { get; }

            /**************************************************************/
            /// <summary>Service under test.</summary>
            public ClaudeApiService Service { get; }

            /**************************************************************/
            /// <summary>
            /// Builds the service with deterministic settings and optional
            /// configuration overrides.
            /// </summary>
            /// <param name="configOverrides">Optional configuration values.</param>
            public ApiHarness(Dictionary<string, string?>? configOverrides = null)
            {
                #region implementation
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configOverrides ?? new Dictionary<string, string?>())
                    .Build();

                var settings = Options.Create(new ClaudeApiSettings
                {
                    ApiKey = "test-api-key",
                    Model = "claude-test",
                    MaxTokens = 1000,
                    Temperature = 0.0
                });

                Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"ClaudeApiTests_{Guid.NewGuid():N}")
                    .Options);

                Service = new ClaudeApiService(
                    new HttpClient(Handler),
                    NullLogger<ClaudeApiService>.Instance,
                    settings,
                    Db,
                    configuration,
                    Store,
                    Skills);
                #endregion
            }

            #endregion
        }

        #endregion

        #endregion
    }
}
