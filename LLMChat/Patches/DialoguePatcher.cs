using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;
using LLMChat.I18n;

namespace LLMChat.Patches;

public static class DialoguePatcher
{
    private static IMonitor? _monitor;
    private static Action<NPC>? _onChatRequested;

    public static void Initialize(IMonitor monitor, Action<NPC> onChatRequested)
    {
        _monitor = monitor;
        _onChatRequested = onChatRequested;
    }

    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            postfix: new HarmonyMethod(typeof(DialoguePatcher), nameof(NPC_checkAction_Postfix))
        );
    }

    private static void NPC_checkAction_Postfix(NPC __instance, Farmer who, ref bool __result)
    {
        try
        {
            if (!__result || __instance == null)
                return;

            if (Game1.activeClickableMenu is DialogueBox)
            {
                var responses = new Response[]
                {
                    new Response("llmchat_chat", Strings.Get("dialogue.free_chat")),
                    new Response("llmchat_normal", Strings.Get("dialogue.normal"))
                };

                var npc = __instance;

                Game1.currentLocation.createQuestionDialogue(
                    string.Format(Strings.Get("dialogue.talk_to"), __instance.displayName),
                    responses,
                    (who, answer) =>
                    {
                        if (answer == "llmchat_chat")
                        {
                            _onChatRequested?.Invoke(npc);
                        }
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"Error in dialogue patch: {ex.Message}", LogLevel.Error);
        }
    }
}
