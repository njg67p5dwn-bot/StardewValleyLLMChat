namespace LLMChat.Personalities;

public class NpcPersonality
{
    public string Name { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public List<string> Traits { get; set; } = new();
    public string SpeechStyle { get; set; } = "";
    public Dictionary<string, string> Relationships { get; set; } = new();
    public string Backstory { get; set; } = "";
    public List<string> Likes { get; set; } = new();
    public List<string> Dislikes { get; set; } = new();
    public int MaxResponseLength { get; set; } = 150;
}
