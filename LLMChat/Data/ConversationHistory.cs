namespace LLMChat.Data;

public class ConversationHistory
{
    public string NpcName { get; set; } = "";
    public string LastInteraction { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Key facts/events that should be remembered permanently (append-only).
    /// Example: "봄에 처음 만남", "자수정 선물 받음", "광산 탐험 약속"
    /// </summary>
    public List<string> KeyMemories { get; set; } = new();

    /// <summary>
    /// Rolling summary of recent conversations that were trimmed.
    /// Gets re-summarized each cycle.
    /// </summary>
    public string RecentSummary { get; set; } = "";

    // Legacy field kept for save file compatibility
    public string Summary { get; set; } = "";

    public void AddMessage(string role, string content, string gameDate)
    {
        Messages.Add(new ChatMessage
        {
            Role = role,
            Content = content,
            GameDate = gameDate
        });
    }

    /// <summary>
    /// Returns messages that would be trimmed, then trims.
    /// </summary>
    public List<ChatMessage> TrimToSize(int maxMessages)
    {
        if (Messages.Count <= maxMessages)
            return new List<ChatMessage>();

        var trimmed = Messages.Take(Messages.Count - maxMessages).ToList();
        Messages = Messages.Skip(Messages.Count - maxMessages).ToList();
        return trimmed;
    }

    public void AddKeyMemory(string memory)
    {
        // Avoid duplicates
        if (!KeyMemories.Contains(memory))
            KeyMemories.Add(memory);

        // Cap at 20 entries - remove oldest when full
        while (KeyMemories.Count > 20)
            KeyMemories.RemoveAt(0);
    }
}
