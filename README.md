# LLM Chat - Stardew Valley NPC Conversation Mod

LLM API를 이용해 스타듀밸리 NPC와 자유롭게 대화할 수 있는 모드입니다.

NPC에게 말을 걸면 `[Free Chat]` 옵션이 추가되며, 선택하면 채팅 UI가 열립니다. 각 NPC는 고유한 성격 설정을 가지고 있으며, 현재 게임 상태(계절, 날씨, 호감도 등)가 대화에 반영됩니다.

## 주요 기능

- **34명 전체 NPC 지원** — 결혼 가능 NPC 12명 + 비결혼 NPC 22명 모두 고유한 성격 설정 보유
- **감정 기반 동적 초상화** — LLM 응답에 따라 NPC 표정이 실시간으로 변화 (neutral, happy, sad, angry, surprised, special)
- **장기 기억 시스템** — 3단계 계층형 메모리로 NPC가 과거 대화를 기억
  - **Key Memories**: 영구 보존되는 핵심 사실 (최대 20개)
  - **Recent Summary**: 최근 대화 요약 (하루 끝 자동 생성)
  - **Messages**: 최근 원본 메시지 (슬라이딩 윈도우)
- **마을 이벤트** — 매 게임일마다 LLM이 1~2개의 마을 이벤트를 생성, NPC끼리 서로의 이벤트를 언급 (당사자 1인칭 / 제3자 가십)
- **반응형 월드 스테이트** — 날씨 변화, 계절, 호감도, 선물, 소득, 스킬 레벨업, 농장 동물 등 게임 상태 변화를 대화 주제로 반영
- **생일 & 축제 인식** — NPC 생일과 9개 축제를 시스템 프롬프트에 반영
- **한국어 IME 완벽 지원** — SDL2 인터럽을 통한 커스텀 텍스트 입력 (조합형 한글 문제 해결)
- **GMCM 인게임 설정** — Generic Mod Config Menu로 게임 내에서 모든 설정 변경 가능
- **디버그 로깅** — LLM 입출력 전문을 파일로 저장하여 디버깅 지원

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

### 인게임 설정 (GMCM)

[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)가 설치되어 있으면 게임 내에서 모든 설정을 변경할 수 있습니다.

- **타이틀 화면** — 하단의 톱니바퀴 아이콘 클릭
- **게임 중** — ESC → 옵션 탭 → 하단 스크롤 → LLM Chat 설정

GMCM 없이도 모드는 정상 동작하며, 아래 `config.json`을 직접 수정해도 됩니다.

### config.json

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
  "ResponseLanguage": "ko",
  "EnableVillageEvents": true,
  "DebugLogging": false
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
  "ResponseLanguage": "ko",
  "EnableVillageEvents": true,
  "DebugLogging": false
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
  "ResponseLanguage": "ko",
  "EnableVillageEvents": true,
  "DebugLogging": false
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
  "ResponseLanguage": "ko",
  "EnableVillageEvents": true,
  "DebugLogging": false
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
| `EnableVillageEvents` | 매일 LLM 생성 마을 이벤트 활성화 | `true` |
| `DebugLogging` | LLM 입출력 전문 파일 저장 (`Mods/LLMChat/debug_logs/`) | `false` |

## 사용법

1. SMAPI로 게임 실행
2. NPC에게 다가가서 말 걸기
3. `[Free Chat]` 선택
4. 채팅 UI에서 자유롭게 대화

## SMAPI 디버그 명령어

SMAPI 콘솔에서 사용 가능한 디버그 명령어:

| 명령어 | 설명 |
|--------|------|
| `llm_settime <시간>` | 게임 내 시간 설정 (예: `llm_settime 1200`) |
| `llm_money <금액>` | 소지금 설정 |
| `llm_events [NPC이름]` | 오늘의 마을 이벤트 확인 (NPC 지정 시 해당 NPC 이벤트만) |

## 프로젝트 구조

```
LLMChat/
├── manifest.json                    # SMAPI 모드 매니페스트
├── LLMChat.csproj                   # .NET 6 프로젝트 (Harmony 활성화)
├── ModEntry.cs                      # 진입점 - 이벤트 등록, 서비스 연결, GMCM
├── ModConfig.cs                     # 설정 모델
├── IGenericModConfigMenuApi.cs      # GMCM API 인터페이스
├── Patches/
│   └── DialoguePatcher.cs           # Harmony 패치 (대화 옵션 주입)
├── UI/
│   └── ChatMenu.cs                  # 채팅 UI (한국어 IME, SDL2 인터럽, 감정 초상화)
├── Services/
│   ├── ILlmProvider.cs              # 프로바이더 인터페이스
│   ├── ClaudeLlmProvider.cs         # Claude API 클라이언트 (raw HttpClient)
│   ├── OpenAiCompatibleProvider.cs  # OpenAI 호환 API 클라이언트
│   ├── LlmService.cs               # 비즈니스 로직 (채팅, 요약, 이벤트 생성)
│   └── SnakeCaseNamingPolicy.cs     # JSON snake_case 네이밍
├── Personalities/
│   ├── NpcPersonality.cs            # NPC 성격 데이터 모델
│   └── PersonalityManager.cs        # NPC 성격 + 게임 상태 → 시스템 프롬프트
├── Data/
│   ├── ChatMessage.cs               # 메시지 모델
│   ├── ConversationHistory.cs       # NPC별 대화 기록 + 계층형 메모리
│   ├── ConversationStore.cs         # 대화 기록 저장/로드, 요약 트리거
│   ├── VillageEvent.cs              # 마을 이벤트 데이터 모델
│   ├── VillageEventManager.cs       # LLM 마을 이벤트 생성/관리
│   └── WorldStateTracker.cs         # 게임 상태 변화 추적 → 대화 주제
└── ContentPack/personalities/       # NPC 성격 JSON (34명)
```

## 라이선스

MIT
