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

    /// <summary>
    /// Summarize old messages into a rolling summary and extract key memories.
    /// Returns (recentSummary, keyMemories).
    /// </summary>
    public async Task<(string RecentSummary, List<string> KeyMemories)?> SummarizeAsync(
        string npcName,
        List<ChatMessage> oldMessages,
        string existingRecentSummary,
        List<string> existingKeyMemories,
        CancellationToken cancellationToken = default)
    {
        if (oldMessages.Count < 4) // At least 2 exchanges to be worth summarizing
            return null;

        try
        {
            var conversationText = string.Join("\n",
                oldMessages.Select(m => $"{m.Role}: {m.Content}"));

            var existingContext = "";
            if (!string.IsNullOrEmpty(existingRecentSummary))
                existingContext += $"\n\n[Previous summary]\n{existingRecentSummary}";
            if (existingKeyMemories.Count > 0)
                existingContext += $"\n\n[Existing key memories]\n{string.Join("\n", existingKeyMemories.Select(m => $"- {m}"))}";

            var systemPrompt = $@"You are a memory manager for {npcName} (an NPC in Stardew Valley).
Analyze the following conversation between {npcName} and the player.{existingContext}

You must output EXACTLY in this format (keep the markers):

===SUMMARY===
Write a 2-3 sentence summary of the recent conversations. Focus on what was discussed, emotional tone, and any developments in the relationship.

===KEY_MEMORIES===
List only NEW important facts, events, promises, or relationship milestones that should be remembered long-term. One per line, starting with ""- "". Keep each entry short (under 15 words).
Do NOT repeat items already in [Existing key memories].
If there are no new key memories, write: - (none)

Respond in the same language the conversation is in.";

            var response = await _provider.GenerateResponseAsync(
                systemPrompt,
                new List<ChatMessage>(),
                conversationText,
                500,
                cancellationToken
            );

            return ParseSummarizationResponse(response);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Summarization failed for {npcName}: {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    private static (string RecentSummary, List<string> KeyMemories) ParseSummarizationResponse(string response)
    {
        var summary = "";
        var keyMemories = new List<string>();

        var summaryIdx = response.IndexOf("===SUMMARY===", StringComparison.Ordinal);
        var keyMemIdx = response.IndexOf("===KEY_MEMORIES===", StringComparison.Ordinal);

        if (summaryIdx >= 0 && keyMemIdx >= 0)
        {
            summary = response
                .Substring(summaryIdx + "===SUMMARY===".Length, keyMemIdx - summaryIdx - "===SUMMARY===".Length)
                .Trim();

            var keyMemSection = response.Substring(keyMemIdx + "===KEY_MEMORIES===".Length).Trim();
            foreach (var line in keyMemSection.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimStart('-', ' ', '*').Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed != "(none)")
                    keyMemories.Add(trimmed);
            }
        }
        else
        {
            // Fallback: treat entire response as summary
            summary = response.Trim();
        }

        return (summary, keyMemories);
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
