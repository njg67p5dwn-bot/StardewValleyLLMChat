namespace LLMChat.Data;

public class ConversationHistory
{
    public string NpcName { get; set; } = "";
    public string LastInteraction { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();
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

    public void TrimToSize(int maxMessages)
    {
        if (Messages.Count > maxMessages)
        {
            Messages = Messages.Skip(Messages.Count - maxMessages).ToList();
        }
    }
}
