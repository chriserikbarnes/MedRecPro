using Newtonsoft.Json;

namespace MedRecPro.Models
{
    #region Claude API response models

    /**************************************************************/
    /// <summary>
    /// Represents the response from the Claude API after processing a completion request.
    /// This class maps to the JSON structure returned by Anthropic's Claude Messages API.
    /// </summary>
    /// <remarks>
    /// The response contains the AI-generated content along with metadata about the request,
    /// including the model used, token usage statistics, and stop reason information.
    ///
    /// This class is used internally by <see cref="Service.ClaudeApiService"/> to deserialize
    /// API responses for medical document analysis, interpretation, and synthesis operations.
    ///
    /// <para>
    /// <b>API Reference:</b> https://docs.anthropic.com/en/api/messages
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Deserializing a Claude API response
    /// var response = JsonConvert.DeserializeObject&lt;ClaudeApiResponse&gt;(jsonResponse);
    ///
    /// // Accessing the generated text
    /// var generatedText = response?.Content?.FirstOrDefault()?.Text;
    ///
    /// // Checking token usage
    /// var inputTokens = response?.Usage?.InputTokens ?? 0;
    /// var outputTokens = response?.Usage?.OutputTokens ?? 0;
    /// </code>
    /// </example>
    /// <seealso cref="ClaudeContent"/>
    /// <seealso cref="ClaudeUsage"/>
    /// <seealso cref="Service.IClaudeApiService"/>
    public class ClaudeApiResponse
    {
        /**************************************************************/
        /// <summary>
        /// Unique identifier for this API response.
        /// Format: "msg_" followed by a unique string.
        /// </summary>
        /// <remarks>
        /// This ID can be used for logging, debugging, and tracking specific API calls.
        /// </remarks>
        /// <example>
        /// "msg_01XFDUDYJgAACzvnptvVoYEL"
        /// </example>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /**************************************************************/
        /// <summary>
        /// The type of response object. Always "message" for successful completions.
        /// </summary>
        [JsonProperty("type")]
        public string? Type { get; set; }

        /**************************************************************/
        /// <summary>
        /// The role of the message author. Always "assistant" for Claude responses.
        /// </summary>
        [JsonProperty("role")]
        public string? Role { get; set; }

        /**************************************************************/
        /// <summary>
        /// The model identifier that processed the request.
        /// </summary>
        /// <remarks>
        /// Examples include "claude-3-opus-20240229", "claude-3-sonnet-20240229", etc.
        /// </remarks>
        /// <example>
        /// "claude-sonnet-4-20250514"
        /// </example>
        [JsonProperty("model")]
        public string? Model { get; set; }

        /**************************************************************/
        /// <summary>
        /// Array of content blocks containing the generated response.
        /// </summary>
        /// <remarks>
        /// Typically contains a single text content block for standard completions.
        /// May contain multiple blocks for responses with mixed content types.
        /// </remarks>
        /// <seealso cref="ClaudeContent"/>
        [JsonProperty("content")]
        public ClaudeContent[]? Content { get; set; }

        /**************************************************************/
        /// <summary>
        /// The reason why Claude stopped generating content.
        /// </summary>
        /// <remarks>
        /// Common values include:
        /// <list type="bullet">
        /// <item>"end_turn" - Natural completion of response</item>
        /// <item>"max_tokens" - Reached token limit</item>
        /// <item>"stop_sequence" - Hit a stop sequence</item>
        /// <item>"tool_use" - Model invoked a tool</item>
        /// </list>
        /// </remarks>
        [JsonProperty("stop_reason")]
        public string? StopReason { get; set; }

        /**************************************************************/
        /// <summary>
        /// The specific stop sequence that caused generation to stop, if applicable.
        /// </summary>
        /// <remarks>
        /// Only populated when <see cref="StopReason"/> is "stop_sequence".
        /// </remarks>
        [JsonProperty("stop_sequence")]
        public string? StopSequence { get; set; }

        /**************************************************************/
        /// <summary>
        /// Token usage statistics for the request and response.
        /// </summary>
        /// <seealso cref="ClaudeUsage"/>
        [JsonProperty("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents a content block within a Claude API response.
    /// Contains the actual generated text or other content types.
    /// </summary>
    /// <remarks>
    /// Each content block has a type and corresponding data. For text responses,
    /// the type is "text" and the <see cref="Text"/> property contains the generated content.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Accessing text content from a response
    /// foreach (var content in response.Content ?? Array.Empty&lt;ClaudeContent&gt;())
    /// {
    ///     if (content.Type == "text")
    ///     {
    ///         Console.WriteLine(content.Text);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ClaudeApiResponse"/>
    public class ClaudeContent
    {
        /**************************************************************/
        /// <summary>
        /// The type of content block.
        /// </summary>
        /// <remarks>
        /// Common values include:
        /// <list type="bullet">
        /// <item>"text" - Standard text content</item>
        /// <item>"tool_use" - Tool invocation content</item>
        /// </list>
        /// </remarks>
        [JsonProperty("type")]
        public string? Type { get; set; }

        /**************************************************************/
        /// <summary>
        /// The generated text content when <see cref="Type"/> is "text".
        /// </summary>
        /// <remarks>
        /// Contains the AI-generated response text for medical document analysis,
        /// interpretation results, or synthesis output.
        /// </remarks>
        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents token usage statistics from a Claude API request.
    /// Used for tracking API consumption, cost calculation, and monitoring.
    /// </summary>
    /// <remarks>
    /// Token counts are important for:
    /// <list type="bullet">
    /// <item>Cost estimation and billing</item>
    /// <item>Monitoring prompt efficiency</item>
    /// <item>Debugging context window usage</item>
    /// <item>Optimizing cache utilization</item>
    /// </list>
    ///
    /// <para>
    /// <b>Caching:</b> The cache-related properties indicate whether prompt caching
    /// was utilized, which can significantly reduce costs for repeated prompts.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Calculating total tokens used
    /// var totalInputTokens = usage.InputTokens +
    ///                        usage.CacheCreationInputTokens +
    ///                        usage.CacheReadInputTokens;
    /// var totalTokens = totalInputTokens + usage.OutputTokens;
    ///
    /// // Checking cache efficiency
    /// if (usage.CacheReadInputTokens > 0)
    /// {
    ///     Console.WriteLine($"Cache hit: {usage.CacheReadInputTokens} tokens read from cache");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ClaudeApiResponse"/>
    public class ClaudeUsage
    {
        /**************************************************************/
        /// <summary>
        /// Number of input tokens processed from the request prompt.
        /// </summary>
        /// <remarks>
        /// This count excludes tokens read from cache.
        /// Higher input token counts indicate longer prompts and higher costs.
        /// </remarks>
        [JsonProperty("input_tokens")]
        public int InputTokens { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of tokens written to the prompt cache.
        /// </summary>
        /// <remarks>
        /// Indicates new content added to the cache during this request.
        /// Cache creation has a small additional cost but enables
        /// significant savings on subsequent similar requests.
        /// </remarks>
        [JsonProperty("cache_creation_input_tokens")]
        public int CacheCreationInputTokens { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of tokens read from the prompt cache.
        /// </summary>
        /// <remarks>
        /// Higher values indicate effective cache utilization.
        /// Cached tokens are significantly cheaper than non-cached tokens.
        /// </remarks>
        [JsonProperty("cache_read_input_tokens")]
        public int CacheReadInputTokens { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of tokens generated in the response.
        /// </summary>
        /// <remarks>
        /// Output tokens typically cost more than input tokens.
        /// The count is affected by the max_tokens parameter in the request.
        /// </remarks>
        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }

        /**************************************************************/
        /// <summary>
        /// The service tier used for processing the request.
        /// </summary>
        /// <remarks>
        /// Indicates which processing tier handled the request,
        /// which may affect latency and cost.
        /// </remarks>
        [JsonProperty("service_tier")]
        public string? ServiceTier { get; set; }
    }

    #endregion
}
