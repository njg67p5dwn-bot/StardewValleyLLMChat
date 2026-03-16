namespace LLMChat.Data;

public class VillageEvent
{
    /// <summary>What happened (1-2 sentences)</summary>
    public string Description { get; set; } = "";

    /// <summary>NPCs directly involved — they'll talk about it in 1st person</summary>
    public List<string> InvolvedNpcs { get; set; } = new();

    /// <summary>Per-NPC perspective (how they feel about it)</summary>
    public Dictionary<string, string> Perspectives { get; set; } = new();

    /// <summary>NPCs who heard about it as gossip — 3rd person</summary>
    public List<string> GossipNpcs { get; set; } = new();

    /// <summary>Game date when the event occurred (e.g., "spring 5, Year 1")</summary>
    public string GameDate { get; set; } = "";
}

public class VillageEventDay
{
    public string GameDate { get; set; } = "";
    public List<VillageEvent> Events { get; set; } = new();
}
