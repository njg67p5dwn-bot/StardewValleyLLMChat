using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace LLMChat;

public class ModConfig
{
    /// <summary>LLM provider to use: "claude", "openai", "local"</summary>
    public string Provider { get; set; } = "claude";

    /// <summary>API key for the LLM provider</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Model ID to use (e.g., "claude-haiku-4-5-20251001", "gpt-4o-mini", "llama3")</summary>
    public string ModelId { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Base URL for OpenAI-compatible API (e.g., "http://localhost:11434/v1" for Ollama, "https://api.openai.com" for OpenAI)</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>Maximum tokens per response</summary>
    public int MaxTokens { get; set; } = 300;

    /// <summary>Hotkey to open chat when near an NPC</summary>
    public KeybindList ChatHotkey { get; set; } = KeybindList.Parse("C");

    /// <summary>Maximum API calls per in-game day (0 = unlimited)</summary>
    public int DailyCallLimit { get; set; } = 50;

    /// <summary>Number of conversation messages to keep in context</summary>
    public int ConversationHistorySize { get; set; } = 20;

    /// <summary>Language for NPC responses</summary>
    public string ResponseLanguage { get; set; } = "ko";
}
