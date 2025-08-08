
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace MedRecPro.Service
{
    public class ClaudeApiService : IClaudeApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ClaudeApiService> _logger;
        private readonly ClaudeApiSettings _settings;

        public ClaudeApiService(
            HttpClient httpClient,
            ILogger<ClaudeApiService> logger,
            IOptions<ClaudeApiSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;

            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<string> GenerateCompletionAsync(string prompt)
        {
            try
            {
                var request = new
                {
                    model = _settings.Model,
                    max_tokens = _settings.MaxTokens,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Claude API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<ClaudeApiResponse>(responseContent);

                return responseObj?.Content?.FirstOrDefault()?.Text ?? "No response generated";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API");
                throw;
            }
        }
    }

    public class ClaudeApiResponse
    {
        public ClaudeContent[]? Content { get; set; }
    }

    public class ClaudeContent
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }
}