using LLMChat.Data;
using LLMChat.I18n;
using StardewModdingAPI;

namespace LLMChat.Services;

public class LlmService
{
    private readonly ILlmProvider _provider;
    private readonly IMonitor _monitor;
    private readonly int _maxTokens;
    private int _dailyCallCount;
    private int _dailyCallLimit;

    public bool IsGenerating { get; private set; }

    public LlmService(ILlmProvider provider, IMonitor monitor, int maxTokens, int dailyCallLimit)
    {
        _provider = provider;
        _monitor = monitor;
        _maxTokens = maxTokens;
        _dailyCallLimit = dailyCallLimit;
    }

    public void ResetDailyCount()
    {
        _dailyCallCount = 0;
    }

    public bool CanMakeCall()
    {
        return _dailyCallLimit == 0 || _dailyCallCount < _dailyCallLimit;
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        List<ChatMessage> history,
        string userMessage,
        Action<string>? onToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanMakeCall())
        {
            return GetRateLimitMessage();
        }

        IsGenerating = true;
        try
        {
            _dailyCallCount++;

            string response;
            if (onToken != null)
            {
                response = await _provider.GenerateStreamingResponseAsync(
                    systemPrompt, history, userMessage, _maxTokens, onToken, cancellationToken
                );
            }
            else
            {
                response = await _provider.GenerateResponseAsync(
                    systemPrompt, history, userMessage, _maxTokens, cancellationToken
                );
            }

            return response;
        }
        catch (Exception ex)
        {
            _monitor.Log($"LLM call failed: {ex.Message}", LogLevel.Error);
            return GetErrorMessage(ex);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private static string GetRateLimitMessage()
    {
        return Strings.Get("llm.rate_limit");
    }

    private static string GetErrorMessage(Exception ex)
    {
        if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            return Strings.Get("llm.invalid_key");

        if (ex.Message.Contains("429") || ex.Message.Contains("rate"))
            return Strings.Get("llm.rate_limited");

        return string.Format(Strings.Get("llm.error"), ex.Message);
    }
}
