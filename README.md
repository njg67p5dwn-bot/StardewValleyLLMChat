# LLM Chat - Stardew Valley NPC Conversation Mod

LLM API를 이용해 스타듀밸리 NPC와 자유롭게 대화할 수 있는 모드입니다.

NPC에게 말을 걸면 `[Free Chat]` 옵션이 추가되며, 선택하면 채팅 UI가 열립니다. 각 NPC는 고유한 성격 설정을 가지고 있으며, 현재 게임 상태(계절, 날씨, 호감도 등)가 대화에 반영됩니다.

## 지원 LLM 프로바이더

- **Claude** (Anthropic API)
- **OpenAI** (GPT-4o, GPT-4o-mini 등)
- **OpenRouter** (Gemini, Llama 등 다양한 모델)
- **Ollama** (로컬 LLM)
- 기타 OpenAI 호환 API

## 요구 사항

- [Stardew Valley](https://www.stardewvalley.net/) 1.6+
- [SMAPI](https://smapi.io/) 4.0+
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- LLM API 키 (Claude, OpenAI, OpenRouter 등)

## 설치 및 빌드

```bash
git clone https://github.com/njg67p5dwn-bot/StardewValleyLLMChat.git
cd StardewValleyLLMChat/LLMChat
dotnet build
```

빌드 시 자동으로 `{게임 폴더}/Mods/LLMChat/`에 배포됩니다.

## 설정

SMAPI로 게임을 한 번 실행하면 `{게임 폴더}/Mods/LLMChat/config.json`이 자동 생성됩니다. 아래 예시를 참고하여 수정하세요.

### Claude 사용 시

```json
{
  "Provider": "claude",
  "ApiKey": "sk-ant-...",
  "ModelId": "claude-haiku-4-5-20251001",
  "BaseUrl": "https://api.openai.com",
  "MaxTokens": 300,
  "ChatHotkey": "C",
  "DailyCallLimit": 50,
  "ConversationHistorySize": 20,
  "ResponseLanguage": "ko"
}
```

### OpenAI 사용 시

```json
{
  "Provider": "openai",
  "ApiKey": "sk-...",
  "ModelId": "gpt-4o-mini",
  "BaseUrl": "https://api.openai.com",
  "MaxTokens": 300,
  "ChatHotkey": "C",
  "DailyCallLimit": 50,
  "ConversationHistorySize": 20,
  "ResponseLanguage": "ko"
}
```

### OpenRouter 사용 시

```json
{
  "Provider": "openai-compatible",
  "ApiKey": "sk-or-...",
  "ModelId": "google/gemini-3.1-flash-lite-preview",
  "BaseUrl": "https://openrouter.ai/api/v1/chat/completions",
  "MaxTokens": 300,
  "ChatHotkey": "C",
  "DailyCallLimit": 50,
  "ConversationHistorySize": 20,
  "ResponseLanguage": "ko"
}
```

### Ollama (로컬) 사용 시

```json
{
  "Provider": "ollama",
  "ApiKey": "",
  "ModelId": "llama3",
  "BaseUrl": "http://localhost:11434/v1",
  "MaxTokens": 300,
  "ChatHotkey": "C",
  "DailyCallLimit": 0,
  "ConversationHistorySize": 20,
  "ResponseLanguage": "ko"
}
```

### 설정 항목

| 항목 | 설명 | 기본값 |
|------|------|--------|
| `Provider` | LLM 프로바이더 (`claude`, `openai`, `openai-compatible`, `ollama`, `local`) | `claude` |
| `ApiKey` | API 키 | `""` |
| `ModelId` | 모델 ID | `claude-haiku-4-5-20251001` |
| `BaseUrl` | API 엔드포인트 URL | `https://api.openai.com` |
| `MaxTokens` | 응답 최대 토큰 수 | `300` |
| `ChatHotkey` | NPC 근처에서 채팅 여는 키 | `C` |
| `DailyCallLimit` | 게임 내 하루 API 호출 제한 (0 = 무제한) | `50` |
| `ConversationHistorySize` | 대화 기록 유지 개수 | `20` |
| `ResponseLanguage` | NPC 응답 언어 (`ko`, `en` 등) | `ko` |

## 사용법

1. SMAPI로 게임 실행
2. NPC에게 다가가서 말 걸기
3. `[Free Chat]` 선택
4. 채팅 UI에서 자유롭게 대화

## 프로젝트 구조

```
LLMChat/
├── manifest.json              # SMAPI 모드 매니페스트
├── LLMChat.csproj             # .NET 6 프로젝트
├── ModEntry.cs                # 진입점
├── ModConfig.cs               # 설정 모델
├── Patches/
│   └── DialoguePatcher.cs     # Harmony 패치 (대화 옵션 주입)
├── UI/
│   └── ChatMenu.cs            # 채팅 UI (한국어 IME 지원)
├── Services/
│   ├── ILlmProvider.cs        # 프로바이더 인터페이스
│   ├── ClaudeLlmProvider.cs   # Claude API 클라이언트
│   ├── OpenAiCompatibleProvider.cs  # OpenAI 호환 API 클라이언트
│   └── LlmService.cs         # 비즈니스 로직
├── Personalities/
│   └── PersonalityManager.cs  # NPC 성격 + 게임 상태 → 시스템 프롬프트
├── Data/
│   └── ConversationStore.cs   # 대화 기록 저장/로드
└── ContentPack/personalities/  # NPC 성격 JSON 파일
```

## 라이선스

MIT
