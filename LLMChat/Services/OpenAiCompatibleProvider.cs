using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMChat.Data;
using StardewModdingAPI;

namespace LLMChat.Services;

public class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly IMonitor _monitor;
    private readonly string _completionsPath;

    public OpenAiCompatibleProvider(string apiKey, string modelId, string baseUrl, IMonitor monitor)
    {
        _modelId = modelId;
        _monitor = monitor;

        // Parse the base URL - if it already contains /chat/completions, split it
        var uri = new Uri(baseUrl.TrimEnd('/'));
        var path = uri.AbsolutePath;

        if (path.Contains("/chat/completions"))
        {
            // User provided the full endpoint URL
            _completionsPath = path;
            var baseUri = new Uri($"{uri.Scheme}://{uri.Authority}");
            _httpClient = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(60) };
        }
        else if (path.Contains("/v1"))
        {
            // User provided base with /v1
            _completionsPath = path.TrimEnd('/') + "/chat/completions";
            var baseUri = new Uri($"{uri.Scheme}://{uri.Authority}");
            _httpClient = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(60) };
        }
        else
        {
            // User provided just the host
            _completionsPath = "/v1/chat/completions";
            _httpClient = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(60) };
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        _monitor.Log($"OpenAI-compatible provider initialized: {_httpClient.BaseAddress}{_completionsPath} model={modelId}", LogLevel.Info);
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
            _monitor.Log($"Sending request to {_completionsPath}", LogLevel.Debug);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_completionsPath, content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _monitor.Log($"API error {response.StatusCode}: {responseJson}", LogLevel.Error);
                throw new Exception($"API returned {response.StatusCode}: {responseJson}");
            }

            var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);
            var text = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
            _monitor.Log($"Got response: {text.Length} chars", LogLevel.Debug);
            return text;
        }
        catch (Exception ex)
        {
            _monitor.Log($"OpenAI-compatible API error: {ex.Message}", LogLevel.Error);
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
            _monitor.Log($"Sending streaming request to {_completionsPath}", LogLevel.Debug);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _completionsPath)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _monitor.Log($"Streaming API error {response.StatusCode}: {errorBody}", LogLevel.Error);
                throw new Exception($"API returned {response.StatusCode}: {errorBody}");
            }

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
                    var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, JsonOptions);
                    var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        fullResponse.Append(delta);
                        onToken(delta);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed chunks
                }
            }

            _monitor.Log($"Streaming complete: {fullResponse.Length} chars", LogLevel.Debug);
            return fullResponse.ToString();
        }
        catch (Exception ex)
        {
            _monitor.Log($"OpenAI-compatible streaming error: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    private ChatCompletionRequest BuildRequest(
        string systemPrompt, List<ChatMessage> history, string userMessage, int maxTokens, bool stream)
    {
        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var msg in history)
        {
            messages.Add(new OpenAiMessage { Role = msg.Role, Content = msg.Content });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = userMessage });

        return new ChatCompletionRequest
        {
            Model = _modelId,
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

    // Request/Response DTOs
    private class ChatCompletionRequest
    {
        public string Model { get; set; } = "";
        public List<OpenAiMessage> Messages { get; set; } = new();
        public int MaxTokens { get; set; }
        public bool Stream { get; set; }
    }

    private class OpenAiMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private class ChatCompletionResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        public OpenAiMessage? Message { get; set; }
        public DeltaMessage? Delta { get; set; }
    }

    private class ChatCompletionChunk
    {
        public List<Choice>? Choices { get; set; }
    }

    private class DeltaMessage
    {
        public string? Content { get; set; }
    }
}
