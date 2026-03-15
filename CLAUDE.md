# LLMChat - Stardew Valley NPC LLM Conversation Mod

## Project Overview

Stardew Valley mod that lets players have free-form conversations with NPCs via LLM APIs (Claude, OpenAI, OpenRouter, Ollama, etc.). Built with SMAPI 4.x, Harmony, and C# (.NET 6).

**Project location:** `/home/kim/StardewValleyLLMChat/LLMChat/`
**Game location:** `/home/kim/.steam/debian-installation/steamapps/common/Stardew Valley/`
**Mod deploy target:** `{Game}/Mods/LLMChat/` (auto-deployed on `dotnet build`)
**SMAPI log:** `/home/kim/.config/StardewValley/ErrorLogs/SMAPI-latest.txt`

## Build & Deploy

```bash
export DOTNET_ROOT=$HOME/.dotnet && export PATH=$PATH:$DOTNET_ROOT
cd /home/kim/StardewValleyLLMChat/LLMChat
dotnet build
```

Build automatically:
1. Compiles to `bin/Debug/net6.0/LLMChat.dll`
2. Copies mod files to `{Game}/Mods/LLMChat/` (via `Pathoschild.Stardew.ModBuildConfig`)
3. Generates release zip `LLMChat 1.0.0.zip`

## Architecture

```
LLMChat/
├── manifest.json                    # SMAPI mod manifest (ID: kim.LLMChat)
├── LLMChat.csproj                   # .NET 6, Harmony enabled, no external DLL deps
├── ModEntry.cs                      # Entry point - event registration, service wiring
├── ModConfig.cs                     # Config: Provider, ApiKey, ModelId, BaseUrl, etc.
├── Patches/
│   └── DialoguePatcher.cs           # Harmony postfix on NPC.checkAction
│                                    #   Injects [Free Chat] / [Normal Dialogue] options
├── UI/
│   └── ChatMenu.cs                  # Custom IClickableMenu with:
│                                    #   - Custom text input (Window.TextInput event, NOT TextBox)
│                                    #   - Pixel-based scrolling for long messages
│                                    #   - Streaming response display
│                                    #   - SDL2 IME interop for Korean input
├── Services/
│   ├── ILlmProvider.cs              # Provider interface (GenerateResponseAsync, streaming)
│   ├── ClaudeLlmProvider.cs         # Claude API via raw HttpClient (NOT Anthropic SDK)
│   ├── OpenAiCompatibleProvider.cs  # OpenAI-compatible API (OpenAI, OpenRouter, Ollama, etc.)
│   ├── LlmService.cs               # Business logic: rate limiting, error handling
│   └── SnakeCaseNamingPolicy.cs     # JSON snake_case naming (net6.0 compat)
├── Personalities/
│   ├── NpcPersonality.cs            # Data model for NPC personality JSON
│   └── PersonalityManager.cs        # Loads JSONs, builds dynamic system prompts
│                                    #   Injects game state: season, weather, friendship, etc.
├── Data/
│   ├── ChatMessage.cs               # Message model (Role, Content, GameDate)
│   ├── ConversationHistory.cs       # Per-NPC history with sliding window
│   └── ConversationStore.cs         # Save/load via SMAPI Data API per save folder
└── ContentPack/personalities/       # NPC personality JSON files
    ├── Abigail.json, Emily.json, Penny.json
    ├── Sam.json, Sebastian.json, Shane.json
    └── (28 more NPCs needed)
```

## Key Design Decisions & Lessons Learned

### 1. No Anthropic SDK - Use raw HttpClient only
The Anthropic NuGet package (v10+) depends on `Microsoft.Extensions.AI.Abstractions` etc., which SMAPI cannot resolve during DLL rewriting. **All API calls use `System.Net.Http.HttpClient` directly.** Both `ClaudeLlmProvider` and `OpenAiCompatibleProvider` are self-contained HTTP clients with no external DLL dependencies.

### 2. `BundleExtraAssemblies` in .csproj
Even though we dropped the Anthropic SDK, keep `<BundleExtraAssemblies>ThirdParty</BundleExtraAssemblies>` in the csproj in case future NuGet packages are added. This ensures third-party DLLs are copied to the mod folder.

### 3. OpenAI-compatible BaseUrl auto-detection
`OpenAiCompatibleProvider` constructor parses `BaseUrl` intelligently:
- `https://openrouter.ai/api/v1/chat/completions` → uses as-is (no double path)
- `https://api.openai.com/v1` → appends `/chat/completions`
- `https://api.openai.com` → appends `/v1/chat/completions`

The user's config had `BaseUrl` with the full path including `/chat/completions`, which caused a 404 before this fix.

### 4. Korean IME handling - The biggest challenge

**Problem:** Stardew Valley's built-in `TextBox` class has fundamental Korean IME issues:
- Last character stays in IME composition buffer, not in `TextBox.Text`
- Space/period during composition inserts before the composing character
  (typing "안녕하세요." → "안녕하세.요")
- Root cause: `TextBox` processes `KeyDown` events (for `.`) before `TextInput` events (for IME commit of `요`)

**Attempted solutions (in order):**
1. ❌ `TextBox` directly embedded - Korean IME composition broken
2. ❌ `Game1.showTextEntry(TextBox)` / `TextEntryMenu` - Same underlying TextBox issues, plus last character lost on Enter
3. ❌ `TextBox.OnEnterPressed` event - Fires before IME commits last character
4. ❌ Detecting `Game1.textEntry` closing in `update()` - Timing race with IME commit
5. ✅ **Current solution:** Custom text input via `Window.TextInput` event + SDL2 interop

**Current implementation (`ChatMenu.cs`):**
- Subscribes to `Game1.game1.Window.TextInput` event directly
- `TextInput` events arrive in correct IME order (commit first, then punctuation)
- Maintains own `_inputText` string buffer
- On send: calls `SDL_StopTextInput()` / `SDL_StartTextInput()` to force-commit last composing character, then waits 50ms
- SDL2 P/Invoke: `libSDL2-2.0.so.0` (Linux-specific, needs Windows equivalent `SDL2.dll` for cross-platform)
- Renders text and blinking cursor manually

### 5. Message rendering - Pixel scrolling
**Problem:** Original implementation used message-index-based scrolling with a `break` when message height exceeded area. Long NPC responses (common with LLMs) were entirely skipped.

**Fix:** Pixel-based scrolling with `_scrollY` offset. Messages render at `messageAreaY - _scrollY`, scissor rect clips overflow, `break` only when past visible area (not when content is tall).

### 6. Font rendering
- Game UI text (dialogue options, etc.): Use English to avoid font issues when game is in English mode
- NPC name in ChatMenu header: `SpriteText.drawString()` (game's bitmap font, supports all game languages)
- Chat messages: `Game1.smallFont` (SpriteFont, supports current game language characters)
- The game's font only contains glyphs for the currently selected language

### 7. System prompt assembly
`PersonalityManager.BuildSystemPrompt(NPC)` combines:
1. Static personality data from JSON (traits, speech style, backstory, relationships)
2. Dynamic game state: season, day, year, time, weather, NPC location
3. Player info: name, farm name, friendship hearts, dating/married status
4. Instructions: response language, max length, stay in character, no fourth-wall breaking

## Config (config.json)

Generated on first SMAPI launch. Located at `{Game}/Mods/LLMChat/config.json`.

```json
{
  "Provider": "openai-compatible",    // "claude", "openai", "openai-compatible", "ollama", "local"
  "ApiKey": "sk-...",
  "ModelId": "google/gemini-3.1-flash-lite-preview",
  "BaseUrl": "https://openrouter.ai/api/v1/chat/completions",
  "MaxTokens": 300,
  "ChatHotkey": "C",
  "DailyCallLimit": 50,
  "ConversationHistorySize": 20,
  "ResponseLanguage": "ko"
}
```

Provider routing in `ModEntry.CreateLlmProvider()`:
- `"claude"` → `ClaudeLlmProvider` (Anthropic API with `x-api-key` header)
- `"openai"`, `"openai-compatible"`, `"ollama"`, `"local"`, or anything else → `OpenAiCompatibleProvider`

## User's current setup
- Provider: OpenRouter (`https://openrouter.ai/api/v1/chat/completions`)
- Model: `google/gemini-3.1-flash-lite-preview`
- Game language: Korean
- Platform: Linux (Ubuntu, kernel 6.17)
- Stardew Valley 1.6.15, SMAPI 4.5.1

## Known Issues / TODO

### Must fix
- [x] Cross-platform SDL2 interop: Uses `NativeLibrary.Load()` with runtime OS detection for Windows (`SDL2.dll`), macOS (`libSDL2.dylib`), Linux (`libSDL2-2.0.so.0`)
### Should do
- [ ] Add personality JSONs for remaining ~28 NPCs
- [x] Generic Mod Config Menu (GMCM) integration for in-game settings
- [ ] Conversation summarization for long-term memory (use LLM to summarize old messages)
- [ ] Thread safety: `_displayMessages` list is modified from async callback and read from game thread

### Nice to have
- [ ] Multiplayer support (each player own API key, client-side calls)
- [ ] Content pack system so users can customize/share NPC personalities
- [ ] Streaming text animation (character-by-character display in chat)
- [ ] Scroll bar visual indicator
- [ ] Text input max length limit
