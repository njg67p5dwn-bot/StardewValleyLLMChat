using LLMChat.Services;
using StardewModdingAPI;

namespace LLMChat.Data;

public class ConversationStore
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly int _maxMessages;
    private readonly Dictionary<string, ConversationHistory> _histories = new();

    // Track which NPCs have untrimmed overflow (need summarization)
    private readonly Dictionary<string, List<ChatMessage>> _pendingSummarization = new();

    public ConversationStore(IModHelper helper, IMonitor monitor, int maxMessages)
    {
        _helper = helper;
        _monitor = monitor;
        _maxMessages = maxMessages;
    }

    public ConversationHistory GetHistory(string npcName)
    {
        if (!_histories.ContainsKey(npcName))
        {
            var loaded = _helper.Data.ReadJsonFile<ConversationHistory>(
                $"data/{Constants.SaveFolderName}/{npcName}.json"
            );
            _histories[npcName] = loaded ?? new ConversationHistory { NpcName = npcName };

            // Migrate legacy Summary field to RecentSummary
            var history = _histories[npcName];
            if (!string.IsNullOrEmpty(history.Summary) && string.IsNullOrEmpty(history.RecentSummary))
            {
                history.RecentSummary = history.Summary;
                history.Summary = "";
            }
        }
        return _histories[npcName];
    }

    public void AddMessage(string npcName, string role, string content, string gameDate)
    {
        var history = GetHistory(npcName);
        history.AddMessage(role, content, gameDate);
        history.LastInteraction = gameDate;

        // Trim and collect overflow for later summarization
        var trimmed = history.TrimToSize(_maxMessages);
        if (trimmed.Count > 0)
        {
            if (!_pendingSummarization.ContainsKey(npcName))
                _pendingSummarization[npcName] = new List<ChatMessage>();
            _pendingSummarization[npcName].AddRange(trimmed);
        }
    }

    /// <summary>
    /// Process pending summarizations for all NPCs. Call on day end / save.
    /// </summary>
    public async Task ProcessPendingSummarizationsAsync(LlmService llmService)
    {
        if (_pendingSummarization.Count == 0)
            return;

        // Take a snapshot and clear pending
        var pending = new Dictionary<string, List<ChatMessage>>(_pendingSummarization);
        _pendingSummarization.Clear();

        foreach (var (npcName, trimmedMessages) in pending)
        {
            var history = GetHistory(npcName);

            _monitor.Log($"Summarizing {trimmedMessages.Count} old messages for {npcName}...", LogLevel.Debug);

            var result = await llmService.SummarizeAsync(
                npcName,
                trimmedMessages,
                history.RecentSummary,
                history.KeyMemories
            );

            if (result.HasValue)
            {
                history.RecentSummary = result.Value.RecentSummary;
                foreach (var memory in result.Value.KeyMemories)
                {
                    history.AddKeyMemory(memory);
                }
                _monitor.Log($"Summary updated for {npcName}. Key memories: {history.KeyMemories.Count}", LogLevel.Debug);
            }
            else
            {
                _monitor.Log($"Summarization skipped for {npcName} (not enough messages or error).", LogLevel.Debug);
            }
        }
    }

    public void SaveAll()
    {
        foreach (var (npcName, history) in _histories)
        {
            _helper.Data.WriteJsonFile(
                $"data/{Constants.SaveFolderName}/{npcName}.json",
                history
            );
        }
    }

    public void Clear()
    {
        _histories.Clear();
        _pendingSummarization.Clear();
    }
}
