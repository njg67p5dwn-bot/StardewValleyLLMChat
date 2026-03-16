using System.Text.Json;
using LLMChat.Personalities;
using LLMChat.Services;
using StardewModdingAPI;
using StardewValley;

namespace LLMChat.Data;

public class VillageEventManager
{
    private readonly PersonalityManager _personalityManager;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    /// <summary>Rolling history of recent days' events (max 3 days)</summary>
    private readonly List<VillageEventDay> _recentDays = new();

    /// <summary>Today's generated events (null = not yet generated)</summary>
    private List<VillageEvent>? _todayEvents;

    /// <summary>True while LLM generation is in progress</summary>
    public bool IsGenerating { get; private set; }

    private static readonly string[] AllNpcNames =
    {
        "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf",
        "Elliott", "Emily", "Evelyn", "George", "Gus", "Haley",
        "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah",
        "Leo", "Lewis", "Linus", "Marnie", "Maru", "Pam",
        "Penny", "Pierre", "Robin", "Sam", "Sandy", "Sebastian",
        "Shane", "Vincent", "Willy", "Wizard"
    };

    public VillageEventManager(PersonalityManager personalityManager, IModHelper helper, IMonitor monitor)
    {
        _personalityManager = personalityManager;
        _helper = helper;
        _monitor = monitor;
    }

    /// <summary>
    /// Kick off async event generation at the start of the day.
    /// </summary>
    public async void OnDayStarted(LlmService llmService)
    {
        _todayEvents = null;

        try
        {
            await GenerateTodayEventsAsync(llmService);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Village event generation failed: {ex.Message}", LogLevel.Warn);
            _todayEvents = new List<VillageEvent>();
        }
    }

    /// <summary>
    /// Get today's events relevant to a specific NPC.
    /// Returns (involvedEvents, gossipEvents).
    /// </summary>
    public (List<(VillageEvent Event, string Perspective)> Involved, List<VillageEvent> Gossip) GetEventsForNpc(string npcName)
    {
        var involved = new List<(VillageEvent, string)>();
        var gossip = new List<VillageEvent>();

        if (_todayEvents == null)
            return (involved, gossip);

        foreach (var ev in _todayEvents)
        {
            if (ev.InvolvedNpcs.Contains(npcName, StringComparer.OrdinalIgnoreCase))
            {
                var perspective = ev.Perspectives.GetValueOrDefault(npcName, ev.Description);
                involved.Add((ev, perspective));
            }
            else if (ev.GossipNpcs.Contains(npcName, StringComparer.OrdinalIgnoreCase))
            {
                gossip.Add(ev);
            }
        }

        return (involved, gossip);
    }

    /// <summary>
    /// Save today's events into rolling history. Call before game save.
    /// </summary>
    public void SaveToHistory()
    {
        if (_todayEvents == null || _todayEvents.Count == 0)
            return;

        var today = new VillageEventDay
        {
            GameDate = $"{Game1.currentSeason} {Game1.dayOfMonth}, Year {Game1.year}",
            Events = _todayEvents
        };

        _recentDays.Add(today);

        // Keep only last 3 days
        while (_recentDays.Count > 3)
            _recentDays.RemoveAt(0);

        // Persist to file
        _helper.Data.WriteJsonFile(
            $"data/{Constants.SaveFolderName}/village_events.json",
            _recentDays
        );
    }

    /// <summary>
    /// Load event history when save is loaded.
    /// </summary>
    public void OnSaveLoaded()
    {
        _todayEvents = null;
        _recentDays.Clear();

        var loaded = _helper.Data.ReadJsonFile<List<VillageEventDay>>(
            $"data/{Constants.SaveFolderName}/village_events.json"
        );
        if (loaded != null)
            _recentDays.AddRange(loaded);
    }

    private async Task GenerateTodayEventsAsync(LlmService llmService)
    {
        if (!llmService.CanMakeCall())
        {
            _monitor.Log("Skipping village event generation (daily limit reached)", LogLevel.Debug);
            _todayEvents = new List<VillageEvent>();
            return;
        }

        IsGenerating = true;
        try
        {
            var prompt = BuildEventGenerationPrompt();
            var response = await llmService.GenerateEventAsync(prompt);
            _todayEvents = ParseEventResponse(response);
            _monitor.Log($"Generated {_todayEvents.Count} village event(s) for today", LogLevel.Debug);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private string BuildEventGenerationPrompt()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You are a Stardew Valley village event generator.");
        sb.AppendLine("Generate 1-2 small daily events happening in Pelican Town today.");
        sb.AppendLine();

        // Game context
        sb.AppendLine("## Current State");
        sb.AppendLine($"- Season: {Game1.currentSeason}, Day {Game1.dayOfMonth}, Year {Game1.year}");
        sb.AppendLine($"- Weather: {(Game1.isRaining ? (Game1.isLightning ? "thunderstorm" : "rainy") : Game1.isSnowing ? "snowy" : "sunny")}");
        sb.AppendLine($"- Day of week: {Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}");
        sb.AppendLine();

        // NPC relationships (condensed)
        sb.AppendLine("## Key NPC Relationships");
        sb.AppendLine("- Pierre & Caroline: married, sometimes argue about Pierre's work obsession");
        sb.AppendLine("- Pierre & Abigail: father-daughter (Pierre secretly doubts paternity)");
        sb.AppendLine("- Demetrius & Robin: married, Demetrius is Maru's father, Sebastian's stepfather");
        sb.AppendLine("- Sebastian & Maru: half-siblings, tension due to Demetrius favoring Maru");
        sb.AppendLine("- Sam & Sebastian: best friends, band together");
        sb.AppendLine("- Sam & Vincent: brothers (Jodi is their mother, Kent is father)");
        sb.AppendLine("- Shane & Marnie: nephew-aunt, Shane lives with Marnie");
        sb.AppendLine("- Shane & Jas: Shane is Jas's godfather");
        sb.AppendLine("- Jas & Vincent: childhood friends, Penny tutors them");
        sb.AppendLine("- Penny & Pam: mother-daughter, Penny worries about Pam's drinking");
        sb.AppendLine("- Lewis & Marnie: secret relationship");
        sb.AppendLine("- Clint: blacksmith, has a crush on Emily");
        sb.AppendLine("- Emily & Haley: sisters, very different personalities");
        sb.AppendLine("- Evelyn & George: elderly married couple, Alex's grandparents");
        sb.AppendLine("- Elliott: writer living in beach cabin");
        sb.AppendLine("- Leah: artist living in cottage south of town");
        sb.AppendLine("- Harvey: town doctor, anxious personality");
        sb.AppendLine("- Gus: runs the Stardrop Saloon");
        sb.AppendLine("- Willy: fisherman at the docks");
        sb.AppendLine("- Linus: lives in a tent on the mountain, forager");
        sb.AppendLine("- Wizard: mysterious, lives in tower south of forest");
        sb.AppendLine();

        // Event categories
        sb.AppendLine("## Event Categories (pick from these naturally)");
        sb.AppendLine("- Family dynamics (arguments, bonding, concern)");
        sb.AppendLine("- Romance/relationships (secret dates, crushes, jealousy)");
        sb.AppendLine("- Business/work (store competition, fishing, cooking, crafting)");
        sb.AppendLine("- Daily life (cooking mishaps, lost items, animals, weather reactions)");
        sb.AppendLine("- Community (town gossip, helping neighbors, small celebrations)");
        sb.AppendLine("- Mystery/unusual (strange sounds, odd sightings, wizard stuff)");
        sb.AppendLine();

        // Recent events for continuity
        if (_recentDays.Count > 0)
        {
            sb.AppendLine("## Recent Events (do NOT repeat these, but you may reference or follow up on them)");
            foreach (var day in _recentDays)
            {
                sb.AppendLine($"[{day.GameDate}]");
                foreach (var ev in day.Events)
                    sb.AppendLine($"  - {ev.Description} (involved: {string.Join(", ", ev.InvolvedNpcs)})");
            }
            sb.AppendLine();
        }

        // Output format
        sb.AppendLine("## Output Format");
        sb.AppendLine("Output EXACTLY in this format for each event (1-2 events total):");
        sb.AppendLine();
        sb.AppendLine("===EVENT===");
        sb.AppendLine("DESCRIPTION: [1-2 sentence description of what happened]");
        sb.AppendLine("INVOLVED: [comma-separated NPC names who are directly part of this]");
        sb.AppendLine("PERSPECTIVE_NpcName: [how this NPC feels/would talk about it, 1 sentence]");
        sb.AppendLine("(repeat PERSPECTIVE_ for each involved NPC)");
        sb.AppendLine("GOSSIP: [comma-separated NPC names who would hear about this as gossip]");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Events should feel grounded in Stardew Valley lore");
        sb.AppendLine("- Use exact NPC names from the relationships list above");
        sb.AppendLine("- Each event should involve 2-3 NPCs directly, with 2-4 gossip NPCs");
        sb.AppendLine("- Keep events small and daily-life scale (not catastrophes)");
        sb.AppendLine("- Consider the current season and weather naturally");

        return sb.ToString();
    }

    private List<VillageEvent> ParseEventResponse(string response)
    {
        var events = new List<VillageEvent>();
        var gameDate = $"{Game1.currentSeason} {Game1.dayOfMonth}, Year {Game1.year}";

        var eventBlocks = response.Split("===EVENT===", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in eventBlocks)
        {
            var ev = new VillageEvent { GameDate = gameDate };
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    ev.Description = line.Substring("DESCRIPTION:".Length).Trim();
                }
                else if (line.StartsWith("INVOLVED:", StringComparison.OrdinalIgnoreCase))
                {
                    ev.InvolvedNpcs = ParseNpcList(line.Substring("INVOLVED:".Length));
                }
                else if (line.StartsWith("PERSPECTIVE_", StringComparison.OrdinalIgnoreCase))
                {
                    var colonIdx = line.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var npcName = line.Substring("PERSPECTIVE_".Length, colonIdx - "PERSPECTIVE_".Length).Trim();
                        var perspective = line.Substring(colonIdx + 1).Trim();
                        // Normalize NPC name casing
                        var matched = MatchNpcName(npcName);
                        if (matched != null)
                            ev.Perspectives[matched] = perspective;
                    }
                }
                else if (line.StartsWith("GOSSIP:", StringComparison.OrdinalIgnoreCase))
                {
                    ev.GossipNpcs = ParseNpcList(line.Substring("GOSSIP:".Length));
                }
            }

            // Only add if we got meaningful data
            if (!string.IsNullOrEmpty(ev.Description) && ev.InvolvedNpcs.Count > 0)
                events.Add(ev);
        }

        return events;
    }

    private List<string> ParseNpcList(string raw)
    {
        var result = new List<string>();
        foreach (var name in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var matched = MatchNpcName(name.Trim());
            if (matched != null)
                result.Add(matched);
        }
        return result;
    }

    /// <summary>
    /// Case-insensitive match against known NPC names.
    /// </summary>
    private static string? MatchNpcName(string input)
    {
        return AllNpcNames.FirstOrDefault(n =>
            string.Equals(n, input, StringComparison.OrdinalIgnoreCase));
    }
}
