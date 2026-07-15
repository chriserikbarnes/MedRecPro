using MedRecPro.Service;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace MedRecProTest.Unit.AI
{
    /**************************************************************/
    /// <summary>
    /// Deterministic HttpMessageHandler that serves queued responses in FIFO
    /// order and records every outgoing request (including its body) for
    /// assertion.
    /// </summary>
    /// <remarks>
    /// Used to fake the Anthropic Messages API for
    /// <see cref="ClaudeApiService"/> tests: enqueue an Anthropic-compatible
    /// envelope via <see cref="EnqueueClaudeText"/> for success paths or a raw
    /// error body via <see cref="EnqueueResponse"/> for failure paths. An
    /// unexpected request (empty queue) fails fast instead of hanging.
    /// </remarks>
    /// <seealso cref="ClaudeApiService"/>
    public sealed class QueuedHttpMessageHandler : HttpMessageHandler
    {
        #region implementation

        private readonly Queue<HttpResponseMessage> _responses = new();

        /**************************************************************/
        /// <summary>
        /// Gets the recorded requests with their serialized bodies, in send
        /// order.
        /// </summary>
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = new();

        /**************************************************************/
        /// <summary>
        /// Queues a successful Anthropic message envelope whose single text
        /// content block carries the supplied inner text.
        /// </summary>
        /// <param name="innerText">The text the Claude response parsers will see.</param>
        /// <seealso cref="BuildClaudeEnvelope"/>
        public void EnqueueClaudeText(string innerText)
        {
            #region implementation
            _responses.Enqueue(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(BuildClaudeEnvelope(innerText), Encoding.UTF8, "application/json")
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queues a raw response with the supplied status code and body (used
        /// for API error branches such as credit-limit detection).
        /// </summary>
        /// <param name="statusCode">HTTP status code to return.</param>
        /// <param name="body">Raw response body.</param>
        public void EnqueueResponse(HttpStatusCode statusCode, string body)
        {
            #region implementation
            _responses.Enqueue(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the exact Anthropic Messages API response envelope the
        /// production parser consumes (content[0].text).
        /// </summary>
        /// <param name="innerText">Inner text content.</param>
        /// <returns>Serialized envelope JSON.</returns>
        public static string BuildClaudeEnvelope(string innerText)
        {
            #region implementation
            return JsonConvert.SerializeObject(new
            {
                id = "msg_test",
                type = "message",
                role = "assistant",
                content = new[] { new { type = "text", text = innerText } },
                stop_reason = "end_turn",
                usage = new { input_tokens = 100, output_tokens = 50 }
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records the request and returns the next queued response.
        /// </summary>
        /// <param name="request">Outgoing request.</param>
        /// <param name="cancellationToken">Cancellation token (unused).</param>
        /// <returns>The next queued response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no response is queued.</exception>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            #region implementation
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add((request, body));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"QueuedHttpMessageHandler received an unexpected request to {request.RequestUri} with no queued response.");
            }

            return _responses.Dequeue();
            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configurable in-memory implementation of <see cref="IClaudeSkillService"/>
    /// returning fixture documents and recording which members were invoked.
    /// </summary>
    /// <remarks>
    /// Defaults are safe for every ClaudeApiService flow: skill selection is
    /// non-direct with a single "label" skill, and every document getter
    /// returns a labeled fixture string.
    /// </remarks>
    /// <seealso cref="IClaudeSkillService"/>
    /// <seealso cref="ClaudeApiService"/>
    public sealed class FakeClaudeSkillService : IClaudeSkillService
    {
        #region implementation

        /**************************************************************/
        /// <summary>Skill selection returned by <see cref="SelectSkillsAsync"/>.</summary>
        public SkillSelection SelectionToReturn { get; set; } = new SkillSelection
        {
            SelectedSkills = new List<string> { "label" },
            IsDirectResponse = false,
            Explanation = "fixture selection"
        };

        /**************************************************************/
        /// <summary>Content returned by <see cref="GetSkillContentAsync"/>.</summary>
        public string SkillContent { get; set; } = "=== FIXTURE SKILL CONTENT ===";

        /**************************************************************/
        /// <summary>Document returned by <see cref="GetFullSkillsDocumentAsync"/>.</summary>
        public string FullSkillsDocument { get; set; } = "=== FIXTURE FULL SKILLS DOCUMENT ===";

        /**************************************************************/
        /// <summary>Per-name skill bodies for <see cref="GetSkillByNameAsync"/>.</summary>
        public Dictionary<string, string> SkillsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>Names of the members invoked, in call order.</summary>
        public List<string> Calls { get; } = new();

        /**************************************************************/
        /// <summary>Returns a fixture manifest.</summary>
        public Task<string> GetSkillManifestAsync()
        {
            #region implementation
            Calls.Add(nameof(GetSkillManifestAsync));
            return Task.FromResult("=== FIXTURE MANIFEST ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured skill selection.</summary>
        /// <param name="userMessage">User message (recorded only).</param>
        /// <param name="systemContext">System context (ignored).</param>
        public Task<SkillSelection> SelectSkillsAsync(string userMessage, object? systemContext = null)
        {
            #region implementation
            Calls.Add(nameof(SelectSkillsAsync));
            return Task.FromResult(SelectionToReturn);
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured skill content.</summary>
        /// <param name="selection">Selection whose content is requested.</param>
        public Task<string> GetSkillContentAsync(SkillSelection selection)
        {
            #region implementation
            Calls.Add(nameof(GetSkillContentAsync));
            return Task.FromResult(SkillContent);
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured per-name skill body.</summary>
        /// <param name="skillName">Requested skill name.</param>
        public Task<string> GetSkillByNameAsync(string skillName)
        {
            #region implementation
            Calls.Add($"{nameof(GetSkillByNameAsync)}:{skillName}");
            return Task.FromResult(SkillsByName.TryGetValue(skillName, out var body)
                ? body
                : $"=== FIXTURE SKILL {skillName} ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured selection's skills.</summary>
        public Task<List<string>> GetAvailableSkillsAsync()
        {
            #region implementation
            Calls.Add(nameof(GetAvailableSkillsAsync));
            return Task.FromResult(new List<string>(SelectionToReturn.SelectedSkills));
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns a fixture capability contract document.</summary>
        public Task<string> GetCapabilityContractsAsync()
        {
            #region implementation
            Calls.Add(nameof(GetCapabilityContractsAsync));
            return Task.FromResult("=== FIXTURE CAPABILITIES ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns a fixture selectors document.</summary>
        public Task<string> GetSelectorsDocumentAsync()
        {
            #region implementation
            Calls.Add(nameof(GetSelectorsDocumentAsync));
            return Task.FromResult("=== FIXTURE SELECTORS ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns a fixture interface document.</summary>
        /// <param name="skillName">Requested skill name.</param>
        public Task<string> GetInterfaceDocumentAsync(string skillName)
        {
            #region implementation
            Calls.Add($"{nameof(GetInterfaceDocumentAsync)}:{skillName}");
            return Task.FromResult($"=== FIXTURE INTERFACE {skillName} ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns a fixture response-format document.</summary>
        public Task<string> GetResponseFormatDocumentAsync()
        {
            #region implementation
            Calls.Add(nameof(GetResponseFormatDocumentAsync));
            return Task.FromResult("=== FIXTURE RESPONSE FORMAT ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns a fixture synthesis-rules document.</summary>
        public Task<string> GetSynthesisRulesDocumentAsync()
        {
            #region implementation
            Calls.Add(nameof(GetSynthesisRulesDocumentAsync));
            return Task.FromResult("=== FIXTURE SYNTHESIS RULES ===");
            #endregion
        }

        /**************************************************************/
        /// <summary>Returns the configured full skills document.</summary>
        public Task<string> GetFullSkillsDocumentAsync()
        {
            #region implementation
            Calls.Add(nameof(GetFullSkillsDocumentAsync));
            return Task.FromResult(FullSkillsDocument);
            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Shared factory helpers for the external-service (AI/Azure) test
    /// harnesses.
    /// </summary>
    /// <seealso cref="QueuedHttpMessageHandler"/>
    /// <seealso cref="FakeClaudeSkillService"/>
    public static class ExternalServiceTestHarness
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Builds a real <see cref="IServiceScopeFactory"/> whose scopes
        /// resolve the supplied <see cref="IClaudeApiService"/> — the pattern
        /// <see cref="ClaudeSearchService"/> uses to reach the AI service.
        /// </summary>
        /// <param name="claudeApiService">Fake or mocked AI service.</param>
        /// <returns>A scope factory backed by a minimal service provider.</returns>
        /// <seealso cref="ClaudeSearchService"/>
        public static IServiceScopeFactory CreateScopeFactoryFor(IClaudeApiService claudeApiService)
        {
            #region implementation
            var services = new ServiceCollection();
            services.AddSingleton(claudeApiService);

            return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            #endregion
        }

        #endregion
    }
}
