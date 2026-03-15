using StardewModdingAPI;
using StardewValley;

namespace LLMChat.Personalities;

public class PersonalityManager
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, NpcPersonality> _personalities = new();
    private readonly string _responseLanguage;

    public PersonalityManager(IModHelper helper, IMonitor monitor, string responseLanguage)
    {
        _helper = helper;
        _monitor = monitor;
        _responseLanguage = responseLanguage;
    }

    public void LoadPersonalities()
    {
        _personalities.Clear();

        var files = Directory.GetFiles(
            Path.Combine(_helper.DirectoryPath, "ContentPack", "personalities"),
            "*.json"
        );

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var personality = System.Text.Json.JsonSerializer.Deserialize<NpcPersonality>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (personality != null)
                {
                    _personalities[personality.Name] = personality;
                    _monitor.Log($"Loaded personality for {personality.Name}", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to load personality from {file}: {ex.Message}", LogLevel.Error);
            }
        }

        _monitor.Log($"Loaded {_personalities.Count} NPC personalities", LogLevel.Info);
    }

    public NpcPersonality? GetPersonality(string npcName)
    {
        return _personalities.GetValueOrDefault(npcName);
    }

    public string BuildSystemPrompt(NPC npc)
    {
        var personality = GetPersonality(npc.Name);
        var prompt = new System.Text.StringBuilder();

        if (personality != null)
        {
            prompt.AppendLine(personality.SystemPrompt);
            prompt.AppendLine();
            prompt.AppendLine($"## Character Traits");
            prompt.AppendLine(string.Join(", ", personality.Traits));
            prompt.AppendLine();
            prompt.AppendLine($"## Speech Style");
            prompt.AppendLine(personality.SpeechStyle);
            prompt.AppendLine();
            prompt.AppendLine($"## Backstory");
            prompt.AppendLine(personality.Backstory);
            prompt.AppendLine();

            if (personality.Relationships.Count > 0)
            {
                prompt.AppendLine($"## Relationships");
                foreach (var (name, relation) in personality.Relationships)
                    prompt.AppendLine($"- {name}: {relation}");
                prompt.AppendLine();
            }

            if (personality.Likes.Count > 0)
                prompt.AppendLine($"## Likes: {string.Join(", ", personality.Likes)}");
            if (personality.Dislikes.Count > 0)
                prompt.AppendLine($"## Dislikes: {string.Join(", ", personality.Dislikes)}");
            prompt.AppendLine();
        }
        else
        {
            prompt.AppendLine($"You are {npc.Name} from Stardew Valley. Stay in character based on what is known about this character in the game.");
            prompt.AppendLine();
        }

        // Dynamic game state
        prompt.AppendLine("## Current Game State");
        prompt.AppendLine($"- Season: {Game1.currentSeason}");
        prompt.AppendLine($"- Day: {Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}, {Game1.dayOfMonth}");
        prompt.AppendLine($"- Year: {Game1.year}");
        prompt.AppendLine($"- Time: {Game1.timeOfDay}");
        prompt.AppendLine($"- Weather: {GetWeatherDescription()}");
        prompt.AppendLine($"- Location: {npc.currentLocation?.Name ?? "unknown"}");

        // Friendship
        var farmer = Game1.player;
        if (farmer.friendshipData.TryGetValue(npc.Name, out var friendship))
        {
            int hearts = friendship.Points / 250;
            prompt.AppendLine($"- Friendship with player: {hearts} hearts");
            if (friendship.IsDating())
                prompt.AppendLine("- Currently dating the player");
            if (friendship.IsMarried())
                prompt.AppendLine("- Married to the player");
        }
        else
        {
            prompt.AppendLine("- Friendship with player: just met");
        }

        prompt.AppendLine($"- Player name: {farmer.Name}");
        prompt.AppendLine($"- Farm name: {farmer.farmName.Value}");
        prompt.AppendLine();

        // Instructions
        prompt.AppendLine("## Instructions");
        prompt.AppendLine($"- Respond in {GetLanguageName(_responseLanguage)}.");
        prompt.AppendLine($"- Keep responses under {personality?.MaxResponseLength ?? 150} words.");
        prompt.AppendLine("- Stay fully in character. Never break the fourth wall.");
        prompt.AppendLine("- Never mention being an AI, language model, or chatbot.");
        prompt.AppendLine("- React naturally to the current season, weather, time, and your relationship with the player.");
        prompt.AppendLine("- Reference your known likes, dislikes, and relationships when relevant.");

        return prompt.ToString();
    }

    private static string GetWeatherDescription()
    {
        if (Game1.isRaining) return Game1.isLightning ? "thunderstorm" : "rainy";
        if (Game1.isSnowing) return "snowy";
        return "sunny";
    }

    private static string GetLanguageName(string code) => code switch
    {
        "ko" => "Korean (한국어)",
        "en" => "English",
        "ja" => "Japanese (日本語)",
        "zh" => "Chinese (中文)",
        _ => code
    };
}
