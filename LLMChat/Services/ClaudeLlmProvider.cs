using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMChat.Data;
using StardewModdingAPI;

namespace LLMChat.Services;

public class ClaudeLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly IMonitor _monitor;

    public ClaudeLlmProvider(string apiKey, string modelId, IMonitor monitor)
    {
        _modelId = modelId;
        _monitor = monitor;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<ChatMessage> history,
        string userMessage,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = BuildRequest(systemPrompt, history, userMessage, maxTokens, stream: false);
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, JsonOptions);

            return result?.Content?.FirstOrDefault()?.Text ?? "";
        }
        catch (Exception ex)
        {
            _monitor.Log($"Claude API error: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    public async Task<string> GenerateStreamingResponseAsync(
        string systemPrompt,
        List<ChatMessage> history,
        string userMessage,
        int maxTokens,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = BuildRequest(systemPrompt, history, userMessage, maxTokens, stream: true);
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = httpContent
            };

            var response = await _httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var fullResponse = new StringBuilder();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                try
                {
                    var streamEvent = JsonSerializer.Deserialize<ClaudeStreamEvent>(data, JsonOptions);
                    if (streamEvent?.Type == "content_block_delta" && streamEvent.Delta?.Text is string text)
                    {
                        fullResponse.Append(text);
                        onToken(text);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed events
                }
            }

            return fullResponse.ToString();
        }
        catch (Exception ex)
        {
            _monitor.Log($"Claude streaming error: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    private ClaudeRequest BuildRequest(
        string systemPrompt, List<ChatMessage> history, string userMessage, int maxTokens, bool stream)
    {
        var messages = new List<ClaudeMessage>();

        foreach (var msg in history)
        {
            messages.Add(new ClaudeMessage { Role = msg.Role, Content = msg.Content });
        }

        messages.Add(new ClaudeMessage { Role = "user", Content = userMessage });

        return new ClaudeRequest
        {
            Model = _modelId,
            System = systemPrompt,
            Messages = messages,
            MaxTokens = maxTokens,
            Stream = stream
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // DTOs
    private class ClaudeRequest
    {
        public string Model { get; set; } = "";
        public string? System { get; set; }
        public List<ClaudeMessage> Messages { get; set; } = new();
        public int MaxTokens { get; set; }
        public bool? Stream { get; set; }
    }

    private class ClaudeMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private class ClaudeResponse
    {
        public List<ContentBlock>? Content { get; set; }
    }

    private class ContentBlock
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private class ClaudeStreamEvent
    {
        public string? Type { get; set; }
        public StreamDelta? Delta { get; set; }
    }

    private class StreamDelta
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }
}
