using LLMChat.Data;
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

    public string BuildSystemPrompt(NPC npc, ConversationHistory? history = null, List<string>? topics = null)
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

        // Birthday
        var birthdayInfo = GetBirthdayInfo(npc);
        if (birthdayInfo != null)
            prompt.AppendLine($"- {birthdayInfo}");

        // Festival
        var festivalInfo = GetFestivalInfo();
        if (festivalInfo != null)
            prompt.AppendLine($"- {festivalInfo}");

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

        // Conversation topics (reactive world events)
        if (topics != null && topics.Count > 0)
        {
            prompt.AppendLine("## Things happening around the valley (use as natural conversation topics if relevant)");
            foreach (var topic in topics)
                prompt.AppendLine($"- {topic}");
            prompt.AppendLine();
        }

        // Long-term memory
        if (history != null)
        {
            if (history.KeyMemories.Count > 0)
            {
                prompt.AppendLine("## Key Memories (important past events with this player)");
                foreach (var memory in history.KeyMemories)
                    prompt.AppendLine($"- {memory}");
                prompt.AppendLine();
            }

            if (!string.IsNullOrEmpty(history.RecentSummary))
            {
                prompt.AppendLine("## Recent Conversation Summary");
                prompt.AppendLine(history.RecentSummary);
                prompt.AppendLine();
            }
        }

        // Instructions
        prompt.AppendLine("## Instructions");
        prompt.AppendLine($"- Respond in {GetLanguageName(_responseLanguage)}.");
        prompt.AppendLine($"- Keep responses under {personality?.MaxResponseLength ?? 150} words.");
        prompt.AppendLine("- Stay fully in character. Never break the fourth wall.");
        prompt.AppendLine("- Never mention being an AI, language model, or chatbot.");
        prompt.AppendLine("- React naturally to the current season, weather, time, and your relationship with the player.");
        prompt.AppendLine("- Reference your known likes, dislikes, and relationships when relevant.");
        prompt.AppendLine("- Start every response with an emotion tag: [neutral], [happy], [sad], [angry], [surprised], or [special]. Choose the emotion that best matches how you feel. The player will not see this tag.");

        return prompt.ToString();
    }

    private static string? GetBirthdayInfo(NPC npc)
    {
        var bSeason = npc.Birthday_Season;
        var bDay = npc.Birthday_Day;

        if (string.IsNullOrEmpty(bSeason) || bDay <= 0)
            return null;

        if (bSeason == Game1.currentSeason && bDay == Game1.dayOfMonth)
            return $"Today is YOUR birthday! ({bSeason} {bDay})";

        if (bSeason == Game1.currentSeason && bDay > Game1.dayOfMonth)
            return $"Your birthday is in {bDay - Game1.dayOfMonth} days ({bSeason} {bDay})";

        return $"Your birthday: {bSeason} {bDay}";
    }

    private static string? GetFestivalInfo()
    {
        var season = Game1.currentSeason;
        var day = Game1.dayOfMonth;
        var key = $"{season}{day}";

        var festival = key switch
        {
            "spring13" => "Egg Festival",
            "spring24" => "Flower Dance",
            "summer11" => "Luau",
            "summer28" => "Dance of the Moonlight Jellies",
            "fall16" => "Stardew Valley Fair",
            "fall27" => "Spirit's Eve",
            "winter8" => "Festival of Ice",
            "winter15" or "winter16" or "winter17" => "Night Market",
            "winter25" => "Feast of the Winter Star",
            _ => null
        };

        return festival != null ? $"Festival today: {festival}" : null;
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
