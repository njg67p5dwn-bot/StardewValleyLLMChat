using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using LLMChat.Data;
using LLMChat.I18n;
using LLMChat.Patches;
using LLMChat.Personalities;
using LLMChat.Services;
using LLMChat.UI;

namespace LLMChat;

public class ModEntry : Mod
{
    private ModConfig _config = null!;
    private PersonalityManager _personalityManager = null!;
    private ConversationStore _conversationStore = null!;
    private LlmService _llmService = null!;
    private WorldStateTracker _worldStateTracker = null!;
    private VillageEventManager? _villageEventManager;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();

        // Set UI language from config
        Strings.SetLanguage(_config.ResponseLanguage);

        // Validate config
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            Monitor.Log(
                Strings.Get("hud.api_key_missing_log"),
                LogLevel.Warn
            );
        }

        // Initialize personality manager
        _personalityManager = new PersonalityManager(helper, Monitor, _config.ResponseLanguage);
        _personalityManager.LoadPersonalities();

        // Initialize world state tracker
        _worldStateTracker = new WorldStateTracker();

        // Initialize conversation store
        _conversationStore = new ConversationStore(helper, Monitor, _config.ConversationHistorySize);

        // Initialize LLM service
        var provider = CreateLlmProvider();
        _llmService = new LlmService(provider, Monitor, _config.MaxTokens, _config.DailyCallLimit,
            helper.DirectoryPath, _config.DebugLogging);

        // Initialize village event manager (if enabled)
        if (_config.EnableVillageEvents)
            _villageEventManager = new VillageEventManager(_personalityManager, helper, Monitor);

        // Apply Harmony patches
        var harmony = new Harmony(ModManifest.UniqueID);
        DialoguePatcher.Initialize(Monitor, OnChatRequested);
        DialoguePatcher.Apply(harmony);

        // Register events
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        // Debug console commands
        helper.ConsoleCommands.Add("llm_settime", "Set game time (e.g., llm_settime 800)", OnSetTime);
        helper.ConsoleCommands.Add("llm_money", "Set player money (e.g., llm_money 1000000)", OnSetMoney);
        helper.ConsoleCommands.Add("llm_events", "Show today's village events", OnShowEvents);

        Monitor.Log("LLM Chat mod loaded!", LogLevel.Info);
    }

    private ILlmProvider CreateLlmProvider()
    {
        return _config.Provider.ToLower() switch
        {
            "claude" => new ClaudeLlmProvider(_config.ApiKey, _config.ModelId, Monitor),
            "openai" or "openai-compatible" or "ollama" or "local" =>
                new OpenAiCompatibleProvider(_config.ApiKey, _config.ModelId, _config.BaseUrl, Monitor),
            _ => new OpenAiCompatibleProvider(_config.ApiKey, _config.ModelId, _config.BaseUrl, Monitor)
        };
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;

        configMenu.Register(
            mod: ModManifest,
            reset: () => _config = new ModConfig(),
            save: () =>
            {
                Helper.WriteConfig(_config);
                // Reinitialize services with new config
                Strings.SetLanguage(_config.ResponseLanguage);
                var provider = CreateLlmProvider();
                _llmService = new LlmService(provider, Monitor, _config.MaxTokens, _config.DailyCallLimit,
                    Helper.DirectoryPath, _config.DebugLogging);
            }
        );

        // === Main page ===
        configMenu.AddPageLink(mod: ModManifest, pageId: "api", text: () => "API Settings >",
            tooltip: () => "Provider, API Key, Model ID, Base URL");

        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "Provider",
            tooltip: () => "LLM provider",
            getValue: () => _config.Provider,
            setValue: value => _config.Provider = value,
            allowedValues: new[] { "claude", "openai", "openai-compatible", "ollama", "local" }
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Max Tokens",
            tooltip: () => "Maximum tokens per NPC response",
            getValue: () => _config.MaxTokens,
            setValue: value => _config.MaxTokens = value,
            min: 50,
            max: 2000,
            interval: 50
        );

        configMenu.AddKeybindList(
            mod: ModManifest,
            name: () => "Chat Hotkey",
            tooltip: () => "Key to open chat when near an NPC",
            getValue: () => _config.ChatHotkey,
            setValue: value => _config.ChatHotkey = value
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Daily Call Limit",
            tooltip: () => "Maximum API calls per in-game day (0 = unlimited)",
            getValue: () => _config.DailyCallLimit,
            setValue: value => _config.DailyCallLimit = value,
            min: 0,
            max: 200,
            interval: 5
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "History Size",
            tooltip: () => "Number of messages to keep in conversation context",
            getValue: () => _config.ConversationHistorySize,
            setValue: value => _config.ConversationHistorySize = value,
            min: 5,
            max: 100,
            interval: 5
        );

        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => "Village Events",
            tooltip: () => "Generate daily village events via LLM (costs 1 API call per game day)",
            getValue: () => _config.EnableVillageEvents,
            setValue: value => _config.EnableVillageEvents = value
        );

        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "Response Language",
            tooltip: () => "Language for NPC responses",
            getValue: () => _config.ResponseLanguage,
            setValue: value => _config.ResponseLanguage = value,
            allowedValues: new[] { "ko", "en", "ja", "zh", "es", "fr", "de", "pt", "ru" }
        );

        // === API Settings page ===
        configMenu.AddPage(mod: ModManifest, pageId: "api", pageTitle: () => "API Settings");

        configMenu.AddParagraph(mod: ModManifest,
            text: () => $"Current: {_config.ApiKey[..Math.Min(8, _config.ApiKey.Length)]}...");
        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "API Key",
            tooltip: () => "API key for the LLM provider",
            getValue: () => _config.ApiKey,
            setValue: value => _config.ApiKey = value
        );

        configMenu.AddParagraph(mod: ModManifest,
            text: () => $"Current: {_config.ModelId}");
        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "Model ID",
            tooltip: () => "e.g., claude-haiku-4-5-20251001, gpt-4o-mini, llama3",
            getValue: () => _config.ModelId,
            setValue: value => _config.ModelId = value
        );

        configMenu.AddParagraph(mod: ModManifest,
            text: () => $"Current: {_config.BaseUrl}");
        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "Base URL",
            tooltip: () => "e.g., https://api.openai.com, https://openrouter.ai/api/v1/chat/completions",
            getValue: () => _config.BaseUrl,
            setValue: value => _config.BaseUrl = value
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _conversationStore.Clear();
        _villageEventManager?.OnSaveLoaded();
        Monitor.Log("Conversation history loaded for current save.", LogLevel.Debug);
    }

    private async void OnSaving(object? sender, SavingEventArgs e)
    {
        // Process pending summarizations before saving
        try
        {
            await _conversationStore.ProcessPendingSummarizationsAsync(_llmService);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Summarization error during save: {ex.Message}", LogLevel.Warn);
        }

        _villageEventManager?.SaveToHistory();
        _conversationStore.SaveAll();
        Monitor.Log("Conversation history saved.", LogLevel.Debug);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _llmService.ResetDailyCount();
        _worldStateTracker.OnDayStarted();
        _villageEventManager?.OnDayStarted(_llmService);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        // Only handle when no menu is open and player is free
        if (!Context.IsPlayerFree)
            return;

        if (!_config.ChatHotkey.JustPressed())
            return;

        // Find nearest NPC
        var farmer = Game1.player;
        var nearbyNpc = FindNearbyNpc(farmer);

        if (nearbyNpc != null)
        {
            OpenChatMenu(nearbyNpc);
        }
        else
        {
            Game1.addHUDMessage(new HUDMessage(Strings.Get("hud.no_npc_nearby"), HUDMessage.error_type));
        }
    }

    private void OnChatRequested(NPC npc)
    {
        OpenChatMenu(npc);
    }

    private void OpenChatMenu(NPC npc)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            Game1.addHUDMessage(new HUDMessage(Strings.Get("hud.no_api_key"), HUDMessage.error_type));
            return;
        }

        Game1.activeClickableMenu = new ChatMenu(npc, _llmService, _personalityManager, _conversationStore, _worldStateTracker, _villageEventManager);
    }

    private void OnSetTime(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log("Save must be loaded first.", LogLevel.Warn);
            return;
        }

        if (args.Length == 0 || !int.TryParse(args[0], out int time))
        {
            Monitor.Log("Usage: llm_settime 800  (sets time to 8:00 AM)", LogLevel.Info);
            return;
        }

        Game1.timeOfDay = time;
        Monitor.Log($"Time set to {time}.", LogLevel.Info);
    }

    private void OnSetMoney(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log("Save must be loaded first.", LogLevel.Warn);
            return;
        }

        if (args.Length == 0 || !int.TryParse(args[0], out int amount))
        {
            Monitor.Log("Usage: llm_money 1000000", LogLevel.Info);
            return;
        }

        Game1.player.Money = amount;
        Monitor.Log($"Money set to {amount}g.", LogLevel.Info);
    }

    private void OnShowEvents(string command, string[] args)
    {
        if (_villageEventManager == null)
        {
            Monitor.Log("Village events are disabled. Set EnableVillageEvents: true in config.", LogLevel.Alert);
            return;
        }

        if (_villageEventManager.IsGenerating)
        {
            Monitor.Log("Village events are still being generated...", LogLevel.Alert);
            return;
        }

        // Show events for a specific NPC, or all events
        var npcFilter = args.Length > 0 ? args[0] : null;

        if (npcFilter != null)
        {
            var (involved, gossip) = _villageEventManager.GetEventsForNpc(npcFilter);
            if (involved.Count == 0 && gossip.Count == 0)
            {
                Monitor.Log($"No events for {npcFilter} today.", LogLevel.Alert);
                return;
            }
            Monitor.Log($"=== Events for {npcFilter} ===", LogLevel.Alert);
            foreach (var (ev, perspective) in involved)
                Monitor.Log($"  [Personal] {perspective}", LogLevel.Alert);
            foreach (var ev in gossip)
                Monitor.Log($"  [Gossip] {ev.Description}", LogLevel.Alert);
        }
        else
        {
            // Show all NPCs that have some village event relevance
            var allNpcs = new[] {
                "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Elliott",
                "Emily", "Evelyn", "George", "Gus", "Haley", "Harvey",
                "Jas", "Jodi", "Kent", "Leah", "Lewis", "Linus",
                "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin",
                "Sam", "Sandy", "Sebastian", "Shane", "Vincent", "Willy", "Wizard"
            };

            bool anyEvents = false;
            foreach (var npc in allNpcs)
            {
                var (involved, gossip) = _villageEventManager.GetEventsForNpc(npc);
                if (involved.Count == 0 && gossip.Count == 0) continue;

                anyEvents = true;
                Monitor.Log($"--- {npc} ---", LogLevel.Alert);
                foreach (var (ev, perspective) in involved)
                    Monitor.Log($"  [Personal] {perspective}", LogLevel.Alert);
                foreach (var ev in gossip)
                    Monitor.Log($"  [Gossip] {ev.Description}", LogLevel.Alert);
            }

            if (!anyEvents)
                Monitor.Log("No village events today. (Try sleeping to trigger next day's events)", LogLevel.Alert);
        }
    }

    private static NPC? FindNearbyNpc(Farmer farmer)
    {
        var location = farmer.currentLocation;
        if (location == null) return null;

        NPC? closest = null;
        float closestDist = float.MaxValue;
        const float maxDistance = 128f; // ~2 tiles

        foreach (var npc in location.characters)
        {
            if (!npc.IsVillager) continue;

            var dist = Microsoft.Xna.Framework.Vector2.Distance(
                farmer.Position, npc.Position
            );

            if (dist < maxDistance && dist < closestDist)
            {
                closest = npc;
                closestDist = dist;
            }
        }

        return closest;
    }
}
