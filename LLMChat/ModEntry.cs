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

        // Initialize conversation store
        _conversationStore = new ConversationStore(helper, _config.ConversationHistorySize);

        // Initialize LLM service
        var provider = CreateLlmProvider();
        _llmService = new LlmService(provider, Monitor, _config.MaxTokens, _config.DailyCallLimit);

        // Apply Harmony patches
        var harmony = new Harmony(ModManifest.UniqueID);
        DialoguePatcher.Initialize(Monitor, OnChatRequested);
        DialoguePatcher.Apply(harmony);

        // Register events
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        // Debug console commands
        helper.ConsoleCommands.Add("llm_settime", "Set game time (e.g., llm_settime 800)", OnSetTime);

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

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _conversationStore.Clear();
        Monitor.Log("Conversation history loaded for current save.", LogLevel.Debug);
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        _conversationStore.SaveAll();
        Monitor.Log("Conversation history saved.", LogLevel.Debug);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _llmService.ResetDailyCount();
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

        Game1.activeClickableMenu = new ChatMenu(npc, _llmService, _personalityManager, _conversationStore);
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
