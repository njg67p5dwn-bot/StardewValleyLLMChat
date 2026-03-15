namespace LLMChat.I18n;

public static class Strings
{
    private static string _lang = "ko";

    public static void SetLanguage(string langCode)
    {
        _lang = langCode.ToLower();
    }

    public static string Get(string key) => (_lang, key) switch
    {
        // DialoguePatcher
        ("ko", "dialogue.talk_to") => "{0}에게 말 걸기.",
        ("ko", "dialogue.free_chat") => "[자유 대화]",
        ("ko", "dialogue.normal") => "[일반 대화]",

        // ModEntry HUD messages
        ("ko", "hud.no_npc_nearby") => "근처에 대화할 수 있는 주민이 없어요.",
        ("ko", "hud.no_api_key") => "API 키가 설정되지 않았습니다. config.json을 확인해주세요.",
        ("ko", "hud.api_key_missing_log") => "API key not configured! Edit config.json in the mod folder to set your API key.",

        // ChatMenu
        ("ko", "chat.type_here") => "여기에 입력하세요...",
        ("ko", "chat.thinking") => "{0}이(가) 생각 중",
        ("ko", "chat.cancelled") => "(취소됨.)",

        // LlmService
        ("ko", "llm.rate_limit") => "(오늘은 대화를 너무 많이 했어요... 내일 다시 말을 걸어주세요.)",
        ("ko", "llm.invalid_key") => "(오류: 잘못된 API 키입니다. config.json을 확인하세요.)",
        ("ko", "llm.rate_limited") => "(요청이 너무 많습니다. 잠시 후 다시 시도해주세요...)",
        ("ko", "llm.error") => "(오류: {0})",

        // English
        ("en", "dialogue.talk_to") => "Talk to {0}.",
        ("en", "dialogue.free_chat") => "[Free Chat]",
        ("en", "dialogue.normal") => "[Normal Dialogue]",

        ("en", "hud.no_npc_nearby") => "No villager nearby to chat with.",
        ("en", "hud.no_api_key") => "API key not set. Check config.json.",
        ("en", "hud.api_key_missing_log") => "API key not configured! Edit config.json in the mod folder to set your API key.",

        ("en", "chat.type_here") => "Type here...",
        ("en", "chat.thinking") => "{0} is thinking",
        ("en", "chat.cancelled") => "(Cancelled.)",

        ("en", "llm.rate_limit") => "(Too many conversations today... talk to me again tomorrow.)",
        ("en", "llm.invalid_key") => "(Error: Invalid API key. Check config.json.)",
        ("en", "llm.rate_limited") => "(Rate limited. Please wait a moment...)",
        ("en", "llm.error") => "(Error: {0})",

        // Japanese
        ("ja", "dialogue.talk_to") => "{0}に話しかける。",
        ("ja", "dialogue.free_chat") => "[フリーチャット]",
        ("ja", "dialogue.normal") => "[通常会話]",

        ("ja", "hud.no_npc_nearby") => "近くに話せる住民がいません。",
        ("ja", "hud.no_api_key") => "APIキーが設定されていません。config.jsonを確認してください。",
        ("ja", "hud.api_key_missing_log") => "API key not configured! Edit config.json in the mod folder to set your API key.",

        ("ja", "chat.type_here") => "ここに入力...",
        ("ja", "chat.thinking") => "{0}が考え中",
        ("ja", "chat.cancelled") => "(キャンセル。)",

        ("ja", "llm.rate_limit") => "(今日は会話が多すぎます...明日また話しかけてください。)",
        ("ja", "llm.invalid_key") => "(エラー: 無効なAPIキーです。config.jsonを確認してください。)",
        ("ja", "llm.rate_limited") => "(リクエスト制限中。少々お待ちください...)",
        ("ja", "llm.error") => "(エラー: {0})",

        // Chinese
        ("zh", "dialogue.talk_to") => "和{0}说话。",
        ("zh", "dialogue.free_chat") => "[自由聊天]",
        ("zh", "dialogue.normal") => "[普通对话]",

        ("zh", "hud.no_npc_nearby") => "附近没有可以聊天的村民。",
        ("zh", "hud.no_api_key") => "未设置API密钥。请检查config.json。",
        ("zh", "hud.api_key_missing_log") => "API key not configured! Edit config.json in the mod folder to set your API key.",

        ("zh", "chat.type_here") => "在此输入...",
        ("zh", "chat.thinking") => "{0}正在思考",
        ("zh", "chat.cancelled") => "(已取消。)",

        ("zh", "llm.rate_limit") => "(今天对话太多了...明天再来找我吧。)",
        ("zh", "llm.invalid_key") => "(错误: 无效的API密钥。请检查config.json。)",
        ("zh", "llm.rate_limited") => "(请求过多。请稍等...)",
        ("zh", "llm.error") => "(错误: {0})",

        // Fallback to English for unknown language
        (_, "dialogue.talk_to") => "Talk to {0}.",
        (_, "dialogue.free_chat") => "[Free Chat]",
        (_, "dialogue.normal") => "[Normal Dialogue]",
        (_, "hud.no_npc_nearby") => "No villager nearby to chat with.",
        (_, "hud.no_api_key") => "API key not set. Check config.json.",
        (_, "hud.api_key_missing_log") => "API key not configured! Edit config.json in the mod folder to set your API key.",
        (_, "chat.type_here") => "Type here...",
        (_, "chat.thinking") => "{0} is thinking",
        (_, "chat.cancelled") => "(Cancelled.)",
        (_, "llm.rate_limit") => "(Too many conversations today... talk to me again tomorrow.)",
        (_, "llm.invalid_key") => "(Error: Invalid API key. Check config.json.)",
        (_, "llm.rate_limited") => "(Rate limited. Please wait a moment...)",
        (_, "llm.error") => "(Error: {0})",

        _ => key
    };
}
