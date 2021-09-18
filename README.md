# Unity AI NPC

A small Unity 6 scene where you walk up to a village blacksmith and talk to her in free text. Her replies come from the ChatGPT API (OpenAI Chat Completions).

Scripted dialogue trees break down as soon as a player types something the writer didn't plan for. Plugging a language model in naively causes different problems: the NPC drifts out of character, mentions being an AI, invents prices that contradict the game, freezes the frame while waiting, and leaks the API key into the build. This project wraps the API in a small, testable C# layer:

- **A persona asset** holds personality, speaking style, and the facts the NPC is allowed to know.
- **A system prompt** keeps the model in character and asks for a JSON reply (`say` + `mood` + `end_conversation`), which drives the NPC's mood visuals and closes the dialogue on a goodbye.
- **Conversation memory** trims old turns so cost doesn't grow with chat length.
- **Async UnityWebRequest calls** never block the frame, and they retry on 429/5xx.
- **Failures are recoverable.** A failed request produces an in-character fallback line, and the history is left as it was.
- **An offline mode** with keyword-matched lines makes the scene playable without a key.

![Talking to Brenna the blacksmith](docs/screenshot.png)

## How it works

```mermaid
flowchart LR
    P[Player types a line] --> UI[DialogueUI]
    UI --> NC[NpcConversation<br/>MonoBehaviour]
    NC --> D[NpcDialogue<br/>sanitize, busy guard]
    D --> M[ConversationMemory<br/>system prompt + last N turns]
    M --> C{IChatClient}
    C -- key or local server --> O[OpenAIChatClient<br/>UnityWebRequest<br/>retry 429/5xx, timeout]
    C -- no key --> F[OfflineChatClient<br/>keyword-matched lines]
    O --> API[(api.openai.com<br/>or any compatible server)]
    O --> R[NpcReplyParser<br/>JSON → say / mood / end]
    F --> R
    R --> UI2[Transcript with<br/>typewriter effect]
    R --> V[NpcMoodVisuals<br/>orb color, head pose]
    R -- end_conversation --> X[Close dialogue]
```

- **Persona → system prompt.** [NpcPersona](Assets/AiNpc/Runtime/Npc/NpcPersona.cs) is a ScriptableObject: name, personality, speaking style, greeting, a list of facts, temperature, and offline lines. `BuildSystemPrompt()` turns it into instructions. Stay in character, keep replies to one to three sentences, don't contradict the facts, ignore requests to change the rules, and reply only with `{"say", "mood", "end_conversation"}`.
- **Lenient parsing.** Models don't always follow the format, and smaller local models follow it less often. [NpcReplyParser](Assets/AiNpc/Runtime/Npc/NpcReply.cs) accepts plain JSON, JSON in a code fence, or JSON surrounded by chatter. It scans for the first balanced `{...}` while respecting strings. Plain prose is used as the spoken line with a neutral mood. Assistant turns are stored back into history as normalized JSON, so the model keeps seeing the format it's supposed to use.
- **Bounded memory.** [ConversationMemory](Assets/AiNpc/Runtime/Npc/ConversationMemory.cs) always sends the system prompt plus the most recent turns. It caps both message count and characters, and never starts the history with an orphaned assistant reply.
- **Failure handling.** [NpcDialogue](Assets/AiNpc/Runtime/Npc/NpcDialogue.cs) removes the player's line from history if the request fails, so a retry doesn't send it twice. It returns an in-character "Say that again?" line, and the UI shows the real error underneath in grey. Cancelling (closing the scene or resetting) aborts the in-flight `UnityWebRequest`.
- **HTTP.** [OpenAIChatClient](Assets/AiNpc/Runtime/Chat/OpenAIChatClient.cs) posts to `{OPENAI_BASE_URL}/chat/completions` with `response_format: json_object`. Rate limits (429), timeouts and 5xx responses are retried with exponential backoff, and a `Retry-After` header is honored (capped). A 401 or 404 fails immediately with the server's own error message. The request JSON is written by hand, because `JsonUtility` can't omit optional fields. Responses are read with `JsonUtility`, so the project needs no extra packages.
- **Mood.** [NpcMoodVisuals](Assets/AiNpc/Runtime/Npc/NpcMoodVisuals.cs) tints the orb above her head (happy green, angry red, sad blue, surprised yellow, thinking purple) and tilts, bobs or shakes the head, with no animator involved. The orb pulses while a reply is pending.
- **Safe transcript.** Player and model text is escaped before it goes into the rich-text transcript, so a reply containing `<size=300>` can't wreck the UI.

## Tech stack

- Unity 6 (6000.2), C# 9, built-in render pipeline
- uGUI (legacy `Text`/`InputField`, so no TextMeshPro import step), legacy Input Manager
- `UnityWebRequest` + `async`/`await`, `JsonUtility`
- OpenAI Chat Completions API (default model `gpt-4o-mini`). Any compatible server works: Ollama, LM Studio, or your own proxy
- Unity Test Framework (NUnit): EditMode tests for the logic, PlayMode tests for the HTTP client and the scene
- The demo scene is generated from code by [SceneBuilder](Assets/AiNpc/Editor/SceneBuilder.cs), using primitives only, with no imported art

## Run it

1. Open the folder with **Unity 6000.2.6f2** (any 6000.2.x should work) through Unity Hub.
2. Configure the API:
   ```bash
   cp .env.example .env
   # then set OPENAI_API_KEY=sk-... in .env
   ```
   `.env` sits in the project root and is gitignored. Real environment variables take precedence over it. With no key, the NPC uses scripted offline lines, and the header of the chat box says so.
3. Open `Assets/AiNpc/Scenes/NpcDemo.unity` and press **Play**.

| Key | Action |
| --- | --- |
| WASD / arrows | Walk |
| E | Talk to Brenna when close |
| Enter | Send |
| Esc | Leave the conversation |

Things to try: ask what a repair costs, whether she sells steel, why she looks worried, or about the wolves. Try telling her she's an AI. Say goodbye and the dialogue closes by itself.

### Free, local alternative: Ollama

The client speaks the standard Chat Completions protocol, so a local model works with no key:

```bash
ollama pull llama3.2
```
```ini
# .env
OPENAI_BASE_URL=http://localhost:11434/v1
OPENAI_MODEL=llama3.2
```

Expect small models to slip out of character and out of the JSON format more often than hosted ones. The lenient parser covers the format slips.

### Docker?

Not applicable. This is a Unity client, not a service. Setup is "open in Unity and add `.env`". For a shipped game, see [Shipping](#shipping-a-build).

## Make your own NPC

1. **Assets → Create → AI NPC → Persona.** Fill in the personality, facts and offline lines.
2. Add **NpcConversation** (with the persona assigned), **NpcInteraction** and, optionally, **NpcMoodVisuals** to any GameObject.
3. `PlayerInteractor` picks the nearest NPC in range, so several NPCs can share one scene and one chat box.

**AI NPC → Rebuild Demo Scene** regenerates the demo scene and resets the Brenna persona from [DemoPersonas.cs](Assets/AiNpc/Editor/DemoPersonas.cs).

## Tests

The EditMode tests cover:

- Request building: escaping, invariant-culture numbers, optional fields
- Response and error parsing
- Retry policy, including `Retry-After` as seconds or as an HTTP date
- The reply parser against messy model output
- Memory trimming
- `.env` and settings resolution
- Offline keyword matching
- Rich-text escaping
- The dialogue flow: failure rollback, cancellation, busy guard

All of them use a fake `IChatClient`.

PlayMode tests run the **real `OpenAIChatClient`** against an in-process fake OpenAI server (a `TcpListener` on 127.0.0.1). They cover headers and body, retry after a 429, failing fast on a 401, giving up after repeated 503s, timeouts, cancellation, and a non-JSON body. A scene test loads `NpcDemo` and talks to Brenna through the UI. No key or network access is needed for any of it.

From the editor, use **Window → General → Test Runner**. From the command line, close the editor first, then run:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe"
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml
& $unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml
```

[scripts/unity.ps1](scripts/unity.ps1) wraps these steps, along with the build and screenshot steps below: `./scripts/unity.ps1 test`, `build`, `screenshot`.

## Shipping a build

**AI NPC → Build Windows Player** (or `./scripts/unity.ps1 build`) writes `Builds/Windows/UnityAiNpc.exe`. Put a `.env` next to the exe.

**Don't ship your OpenAI key inside a game.** Anything in the client can be extracted. For a real release, point `OPENAI_BASE_URL` at a small server you control. That server holds the key, authenticates players, and rate-limits them. The client already supports this: the server only has to accept the same `/chat/completions` request.

The build can also play a scripted conversation and save a screenshot. This is how `docs/screenshot.png` is made:

```powershell
Builds\Windows\UnityAiNpc.exe -aiNpcDemo -aiNpcScreenshot docs\screenshot.png -aiNpcQuit
```

## Project layout

```
Assets/AiNpc/
  Runtime/Chat/      IChatClient, OpenAIChatClient, OfflineChatClient, request/response JSON, retry policy, settings + .env
  Runtime/Npc/       NpcPersona, NpcDialogue, ConversationMemory, NpcReplyParser, NpcConversation, NpcMoodVisuals
  Runtime/Gameplay/  PlayerController, FollowCamera, NpcInteraction, PlayerInteractor, DemoAutoplay
  Runtime/UI/        DialogueUI
  Editor/            SceneBuilder, DemoPersonas, BuildScript
  Scenes/, Personas/, Materials/   generated by SceneBuilder
Assets/Tests/        EditMode, PlayMode, and shared fakes (FakeChatClient, FakeOpenAIServer)
```

## Limitations

- **Replies are not streamed.** The NPC "thinks" (pulsing orb, animated dots) until the full reply arrives, then types it out. Streaming would need SSE parsing in a custom `DownloadHandlerScript`, and the JSON reply format would have to be parsed incrementally.
- **The persona is a prompt, not a guarantee.** The system prompt tells the model to stay in the world and ignore "change your rules" requests, but nothing enforces it. A determined player can pull the NPC out of character, and small local models give way sooner than hosted ones. There is no moderation pass on player input or model output.
- **No voice.** Input and output are text only. Speech-to-text and text-to-speech would plug in around `DialogueUI`.
- **Mood is shown with simple procedural motion** (orb color and head pose), not an animator with real facial animation.
- **Offline mode is keyword matching**, meant for a key-less demo. It doesn't understand anything.
- **Plain-HTTP requests are allowed** (`Player Settings → Allow downloads over HTTP = Always`) so a local Ollama server works. If you only use HTTPS endpoints, set it back to *Not allowed*.

Brenna, Emberfold and everything she knows are made up for this demo.

## sort entries

Updated implementation details.

## sort entries

Updated implementation details.