using StardewModdingAPI;

namespace LLMChat.Data;

public class ConversationStore
{
    private readonly IModHelper _helper;
    private readonly int _maxMessages;
    private readonly Dictionary<string, ConversationHistory> _histories = new();

    public ConversationStore(IModHelper helper, int maxMessages)
    {
        _helper = helper;
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
        }
        return _histories[npcName];
    }

    public void AddMessage(string npcName, string role, string content, string gameDate)
    {
        var history = GetHistory(npcName);
        history.AddMessage(role, content, gameDate);
        history.LastInteraction = gameDate;
        history.TrimToSize(_maxMessages);
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
    }
}
