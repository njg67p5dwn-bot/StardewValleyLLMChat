using StardewValley;

namespace LLMChat.Data;

/// <summary>
/// Tracks game world state changes between days and generates
/// conversation topics for NPCs based on actual game events.
/// </summary>
public class WorldStateTracker
{
    private string _previousWeather = "";
    private string _previousSeason = "";
    private int _previousDay;
    private int _previousMoney;
    private readonly Dictionary<string, int> _previousFriendships = new();

    /// <summary>
    /// Snapshot current state at the start of each day.
    /// Call this in OnDayStarted AFTER reading the current values.
    /// </summary>
    public void OnDayStarted()
    {
        _previousSeason = Game1.currentSeason;
        _previousDay = Game1.dayOfMonth;
        _previousWeather = GetCurrentWeather();
        _previousMoney = Game1.player.Money;

        // Snapshot friendship points
        _previousFriendships.Clear();
        foreach (var (name, friendship) in Game1.player.friendshipData.Pairs)
        {
            _previousFriendships[name] = friendship.Points;
        }
    }

    /// <summary>
    /// Generate conversation topics relevant to a specific NPC.
    /// </summary>
    public List<string> GetTopicsForNpc(NPC npc)
    {
        var topics = new List<string>();
        var farmer = Game1.player;

        // 1. Weather change
        var currentWeather = GetCurrentWeather();
        if (!string.IsNullOrEmpty(_previousWeather) && _previousWeather != currentWeather)
            topics.Add($"The weather changed from {_previousWeather} to {currentWeather} today");

        // 2. New season
        if (Game1.dayOfMonth == 1)
            topics.Add($"Today is the first day of {Game1.currentSeason}! A new season has begun");

        // 3. Weekend (Fri/Sat = days that end in 5,6,12,13,19,20,26,27)
        var dayOfWeek = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth);
        if (dayOfWeek == "Fri" || dayOfWeek == "Sat")
            topics.Add("It's the weekend");

        // 4. Friendship changes with this NPC
        if (farmer.friendshipData.TryGetValue(npc.Name, out var friendship))
        {
            if (_previousFriendships.TryGetValue(npc.Name, out var prevPoints))
            {
                int prevHearts = prevPoints / 250;
                int currentHearts = friendship.Points / 250;
                if (currentHearts > prevHearts)
                    topics.Add($"Your friendship with the player recently grew to {currentHearts} hearts");
            }
        }

        // 5. Player gave gift to other NPCs (gossip)
        foreach (var (name, fr) in farmer.friendshipData.Pairs)
        {
            if (name == npc.Name) continue;
            if (_previousFriendships.TryGetValue(name, out var prev))
            {
                int delta = fr.Points - prev;
                if (delta >= 200) // Significant gift (loved item = 250+)
                    topics.Add($"The player recently gave a well-received gift to {name}");
            }
        }

        // 6. Farm animals
        var farm = Game1.getFarm();
        if (farm != null)
        {
            long animalCount = farm.animals.Length;
            if (animalCount > 0)
                topics.Add($"The player has {animalCount} farm animal(s)");
        }

        // 7. Money milestones
        var money = farmer.Money;
        if (money >= 1000000 && _previousMoney < 1000000)
            topics.Add("The player just became a millionaire!");
        else if (money >= 100000 && _previousMoney < 100000)
            topics.Add("The player's savings just passed 100,000g");

        // 8. Player skill levels (interesting topics)
        var farmingLevel = farmer.FarmingLevel;
        var miningLevel = farmer.MiningLevel;
        var fishingLevel = farmer.FishingLevel;
        var combatLevel = farmer.CombatLevel;
        if (farmingLevel >= 10)
            topics.Add("The player is a master farmer (level 10)");
        if (miningLevel >= 10)
            topics.Add("The player is a master miner (level 10)");
        if (fishingLevel >= 10)
            topics.Add("The player is a master angler (level 10)");
        if (combatLevel >= 10)
            topics.Add("The player is a master fighter (level 10)");

        // 9. Late-game year awareness
        if (Game1.year >= 3)
            topics.Add($"The player has been living in the valley for {Game1.year} years now");

        // 10. Night market (multi-day event)
        if (Game1.currentSeason == "winter" && Game1.dayOfMonth >= 15 && Game1.dayOfMonth <= 17)
            topics.Add("The Night Market is happening at the beach this evening");

        return topics;
    }

    private static string GetCurrentWeather()
    {
        if (Game1.isRaining) return Game1.isLightning ? "thunderstorm" : "rain";
        if (Game1.isSnowing) return "snow";
        return "sunny";
    }
}
