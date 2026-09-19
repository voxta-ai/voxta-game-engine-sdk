# Voxta Game Engine SDK — Design Document

**Status:** Draft for discussion
**Owner:** TBD (you!)
**Targets:** Unity first, Unreal Engine second
**Date:** 2026-08-17

---

## 1. Vision

A drop-in package for game engines that lets any game developer add a talking, listening, animated Voxta companion to their scene without writing networking code.

The developer's integration experience we are aiming for:

1. Install the package (UPM in Unity, plugin in Unreal).
2. Drop a `VoxtaCompanion` prefab/actor into the scene.
3. Enter a server URL (default `http://127.0.0.1:5384`).
4. Press Play and talk to a character.

**Deployment model:** the player runs their own Voxta server (local or self-hosted), exactly like the VirtaMate integration works today. The game connects to it. No cloud dependency, no API costs for the game developer, full local/private operation. Every game that ships with this SDK becomes a distribution channel for Voxta itself.

**Why Unity first:** existing team experience, and the server repo already contains a complete first-party .NET client (see §3) that Unity can build on directly. Unreal comes second and reuses everything from §4 (the protocol contract), re-implemented in C++.

---

## 2. What this is and is not

| | |
|---|---|
| **Is** | A client SDK. The engine is a *client* of a Voxta server, like the Talk web app or the VaM plugin. |
| **Is not** | A Voxta module. Modules (`src/modules/`) are pluggable AI services (TTS/STT/LLM) running inside the server. Nothing new is installed into Voxta. |
| **Is** | Its own repository and release artifact (like voxta-virtamate), maintained by us, versioned independently. |
| **Is not** | Part of the voxta-server tree. |

Server-side work is limited to: documenting and versioning the existing client protocol, and fixing rough edges the first integration exposes (at the source, in voxta-server PRs).

---

## 3. What already exists (protocol + client code map)

The scouting below is verified against the current codebase. File paths are relative to voxta-server.

### 3.1 Transport surfaces

A Voxta client uses three transports:

1. **SignalR hub at `/hub`** — all chat traffic. One hub method each way:
   - Client → server: `SendMessage(ClientMessage)` (`src/server/Voxta.Host.AspNetCore/SignalRHubs/VoxtaConnectionHub.cs`)
   - Server → client: `ReceiveMessage(ServerMessage)` (`src/common/Voxta.Model/Clients/IVoxtaClientMethods.cs`)
   - Messages are JSON-polymorphic with a `$type` discriminator: ~50 client message types, ~65 server message types, all in `src/common/Voxta.Model/WebsocketMessages/`.
2. **Raw WebSocket at `/ws/audio/input/stream?sessionId={guid}`** — microphone PCM upload for STT (`src/server/Voxta.Host.AspNetCore/Controllers/AudioController.cs`). First frame is a JSON `AudioInputSpecifications` (contentType, sampleRate, channels, bitsPerSample), then binary WAV frames, plus JSON `{"type":"silence","milliseconds":N}` markers. Server resamples to 16 kHz mono.
3. **REST under `/api`** — auth, chats, characters, scenarios, and TTS audio file downloads (`api/tts/...` in `SpeechController.cs`).

### 3.2 Auth

- Transport auth: API key as `Authorization: Bearer`, `?access_token=` query (used by the SignalR WebSocket), or cookie (`BearerAuthenticationMiddleware.cs`). Game clients use scope `role:app`.
- **Device flow exists and is the right fit for games:** `POST api/device/code` → show the code to the player → poll `api/device/poll` until a token arrives (`DeviceFlowController.cs`, client side `src/client/Voxta.Client/DeviceAuthorization.cs`). No embedded browser or password form needed in-game.
- After the socket connects, first message is `ClientAuthenticateMessage` (`Client` app name, `ClientVersion`, `Scope`, `Capabilities`). Server replies `ServerWelcomeMessage` (`welcome`) or `ServerAuthenticationRequiredMessage`.
- `ClientCapabilities` declares what the client can do: `AudioInput` (None = server records / WebSocketStream = client streams mic), `AudioOutput` (Url = download TTS clips, the default), `VisionCapture`, accepted audio content types.

### 3.3 Chat lifecycle (the runtime loop)

- Start/resume: `ClientStartChatMessage` (`startChat`: characterId/scenarioId/chatId), `ClientResumeChatMessage`. Server responds `chatStarting` → `chatStarted` (`ServerChatStartedMessage`: full message history + `Context`).
- Send input: `ClientSendMessage` (`send`: text, attachments, role). Interrupt: `ClientInterruptMessage`. Make a character say arbitrary text: `ClientCharacterSpeechRequestMessage`.
- Reply streaming: `replyGenerating` → `replyStart` → N × **`ServerReplyChunkMessage`** (`replyChunk`: text span + **`AudioUrl`** for that chunk + `AudioGapMs` + `Affect`) → `replyEnd`. Committed state arrives as `ServerUpdatedMessage`.
- **Speech playback is client-driven:** the client downloads each chunk's `AudioUrl`, plays it, and reports back `ClientSpeechPlaybackStartMessage` (with measured clip duration) per chunk and `ClientSpeechPlaybackCompleteMessage` once at the end. Server can order a stop via `ServerInterruptSpeechMessage`. The reference implementation of this whole state machine is **`src/client/Voxta.Client/SpeechPlayback.cs`** — port this, do not reinvent it.
- STT results: `speechRecognitionStart` / `speechRecognitionPartial` / `speechRecognitionEnd` over the hub, plus `ServerAudioFrameMessage` (RMS/VAD telemetry) for mic UI.

### 3.4 Actions, animations, scenario state (the game-facing part)

- `ServerActionMessage` (`action`) — action-inference result with `Value` + typed `Arguments`. This is what a game binds gameplay to.
- `ServerActionAppTriggerMessage` (`appTrigger`) — custom triggers targeted at external apps; acknowledge with `ClientAppTriggerCompleteMessage`.
- `ServerContextUpdatedMessage` (`contextUpdated`) — live scenario state: flags, variables, available actions/buttons, controls, characters. A game can render or bind to any of it.
- `ClientTriggerActionMessage`, `ClientUpdateContextMessage` — the game pushes world state and triggers back into the scenario.
- `ServerAnimationPlayMessage` (`animationPlay`: URL + fps + frame count) — server-driven animation playback.

### 3.5 The existing .NET client SDK (the head start)

`src/client/` contains a complete first-party client that a Unity SDK can build on:

- **`Voxta.Client`** — `VoxtaWebsocketsClient` (SignalR wrapper: connect, typed send, `OnMessageReceived`, auto re-auth on reconnect, retry policy), `VoxtaWebsocketsClientFactory` (wires the `$type` JSON serializer via `VoxtaJsonSerializer` + `InheritedPolymorphismResolver`), `WebsocketsUrlHelper` (http→ws), `SpeechPlayback` (playback state machine with `ISpeechAudioOutput`/`ISpeechAudioClip` abstractions — Unity implements these with `AudioSource`/`AudioClip`), full typed REST client (`VoxtaApiClient` + `Api/*`), `DeviceAuthorization` (device-flow login).
- **`Voxta.Providers.Host`** — higher-level session framework: `ProviderAppHandler` (the clearest end-to-end example of the runtime protocol flow), `RemoteChatSession`, `ProviderBase` (typed message dispatch, chunk accumulation into whole messages).
- Both reference **`Voxta.Model`** — the same assembly the server serializes. Wire compatibility is by construction.

The TypeScript mirror (`src/web/libs/voxta-client/`) is a second reference implementation of the same protocol.

---

## 4. Deliverable 1: the protocol contract (shared by all engines)

Before any engine code is polished, the protocol gets written down as a versioned document (in the SDK repo, later possibly moved to voxta-doc):

- Connection + auth handshake (device flow + API key paths)
- Capabilities negotiation and what each mode implies
- The chat lifecycle message sequences (as sequence diagrams)
- The speech playback contract (chunk download, start/complete reporting, interrupts, `AudioGapMs`)
- The mic streaming socket format
- The action/appTrigger/contextUpdated contract for gameplay binding
- The minimal REST surface a game needs

This document is what makes the Unreal port (and any future engine) cheap. It is written by extracting facts from `Voxta.Client`/`ProviderAppHandler` while building the Unity spike — every ambiguity found is either documented or fixed upstream in voxta-server.

**Server API stability note:** the moment external developers build against this contract, it becomes a public surface. `ServerWelcomeMessage` already carries `ApiVersion` — the SDK checks it on connect. Raising the "this is now a public API" question with acidbubbles happens before the SDK's first public release, not before the spike.

---

## 5. Deliverable 2: Voxta Unity SDK

### 5.1 Package shape

- Own repository in the Voxta GitHub org (working name: `voxta-unity`), UPM package `com.voxta.sdk`, installable via git URL initially, OpenUPM later.
- Runtime asmdef + Samples~ folder with a demo scene. Editor asmdef for inspector niceties.
- Unity 2022.3 LTS minimum (netstandard2.1), Mono and IL2CPP. Keeping the LTS floor is deliberate: minimum Unity version is the SDK's adoption ceiling for external devs (see R2 for the Unity 6.8 / direct Voxta.Model reference upgrade path).
- Ships a `link.xml` preserving SignalR client types against IL2CPP stripping (see R1) — integrators must never have to write this themselves.

### 5.2 Components (the public API)

| Component | Responsibility |
|---|---|
| `VoxtaClient` | Connection lifecycle: URL + token, connect/disconnect, auth handshake, reconnect, typed message send/subscribe. Wraps SignalR or a hand-rolled WebSocket transport (see risk R1). |
| `VoxtaCompanion` (MonoBehaviour, the prefab) | One character in the scene. Starts/resumes a chat, exposes UnityEvents: `OnReplyChunk(text)`, `OnMessageComplete`, `OnAction(name, args)`, `OnAppTrigger`, `OnContextUpdated`, `OnSpeechStart/End`. Inspector fields: server URL, character selection, capabilities. |
| `VoxtaSpeechPlayer` | Port of `SpeechPlayback.cs`. Downloads chunk `AudioUrl`s (UnityWebRequestMultimedia → `AudioClip`), plays through an `AudioSource` (3D-positional for free), reports playbackStart/Complete, honors interrupts and `AudioGapMs`. Optional lipsync hook: exposes current RMS/visemes for whoever consumes it. |
| `VoxtaMicrophone` | Unity `Microphone` capture → 16-bit PCM frames → `/ws/audio/input/stream`. Declares `AudioInput=WebSocketStream`. VAD telemetry surfaced from `ServerAudioFrameMessage`. |
| `VoxtaAuth` | Device-flow login UI helper: fetch code, display it, poll, persist token in `PlayerPrefs` (or a pluggable store). |
| `VoxtaActions` | Declarative binding: register game actions (name, description, arguments) into the chat context via `ClientUpdateContextMessage`, dispatch incoming `ServerActionMessage` to C# handlers/UnityEvents. This is the "AI can affect the game world" surface and the biggest selling point. |

Everything above compiles to plain C# events too, so non-UnityEvent codebases can subscribe directly.

### 5.3 What the sample scene shows

Empty scene + `VoxtaCompanion` prefab + a capsule with an audio source:
talk by voice or text box, hear the streamed reply spatially, watch a registered action ("wave", "come_here") fire a Debug/animation trigger. That scene *is* the marketing.

### 5.4 Explicitly out of scope for v1

- Avatar rendering/VRM (games have their own characters; VRM support can be a later optional add-on package)
- Embedding the Voxta server in the game
- Voxta Cloud auth flows
- Vision capture, image generation display, memory-book editing (later)

---

## 6. Deliverable 3: Unreal plugin (phase 2)

- C++ plugin, working name `VoxtaUnreal`. Same protocol document, re-implemented.
- SignalR consideration: since the server supports `skipNegotiation` + WebSockets transport (the TS client uses exactly that), the transport is a plain WebSocket with SignalR's JSON framing — implementable over Unreal's `FWebSocketsModule` without a full SignalR client library. Microsoft's `signalr-client-cpp` is a fallback.
- Polymorphic deserialization constraint (found by DJ in practice): Unreal's reflection system and built-in JSON converters resolve types strictly ahead-of-time — there is no mechanism to choose the allocation type from the `$type` discriminator mid-parse. The pattern is therefore two-phase: parse the frame into a generic `FJsonObject`, read `$type`, then dispatch to the matching UStruct via a `$type` → converter table. This works because SignalR frames arrive as complete JSON messages, never streamed mid-object.
- Consequence: Unreal needs its own ahead-of-time DTO model synced with `Voxta.Model`, exactly like the C# side. The R2 codegen covers this — it gains a second emitter target that generates the USTRUCT declarations and the `$type` dispatch table alongside the C# DTOs, so the Unreal model is generated and drift-checked by the same CI, not hand-synced.
- Public surface mirrors Unity: `UVoxtaClient` subsystem, `AVoxtaCompanion` actor component, Blueprint nodes/events for reply text, speech start/end, and actions.
- Not started until the Unity SDK has shipped its sample and the protocol doc is stable. Estimated at a fraction of the Unity effort because all discovery is done.

---

## 7. Milestones (each = one reviewable unit)

| # | Milestone | Definition of done |
|---|---|---|
| M1 | **Spike** | Console-free Unity scene connects to a local Voxta server, authenticates (hardcoded API key is fine), starts a chat, prints streamed reply text. No audio, no polish. R1 is already proven in DJ's current work, so M1's job is packaging that knowledge: a clean-project IL2CPP build passing with the SDK's shipped `link.xml`, plus the R2 decision (DTO codegen bootstrapped). |
| M2 | **Protocol doc v0** | The §4 document drafted from the spike + `Voxta.Client` sources. |
| M3 | **Speech** | `VoxtaSpeechPlayer` ported, full chunk playback handshake, interrupt works. Voice input via `VoxtaMicrophone`. |
| M4 | **Actions + Companion API** | `VoxtaActions` binding, `VoxtaCompanion` prefab, device-flow auth. |
| M5 | **Package + sample** | UPM packaging, sample scene, README/quickstart. First public tag. Raise API-stability question with acidbubbles before this ships. |
| M6 | **Unreal spike** | Phase 2 begins. |

---

## 8. Risks

- **R1 — SignalR + System.Text.Json under Unity/IL2CPP: MITIGATED (verified in practice).** DJ already runs `Microsoft.AspNetCore.SignalR.Client` + System.Text.Json under IL2CPP today. The one known gotcha is linker code stripping of SignalR types, which requires an explicit exception in `link.xml`. Design consequence: the SDK package ships its own `link.xml` with the required preservations so integrating developers never hit the stripping failure themselves. M1 shrinks accordingly: it validates the shipped `link.xml` on a clean IL2CPP build rather than exploring whether the stack works at all.
- **R2 — Voxta.Model reuse vs. vendoring.** Referencing the server's `Voxta.Model` DLL gives perfect wire fidelity, and Unity 6.8+ is expected to bring .NET 10-era runtime support that would allow referencing it directly as a managed DLL (per DJ, who currently maintains hand-synced DTOs and feels the drift pain). But 6.8 is unreleased (6.7 is in alpha), and more importantly a public SDK's minimum Unity version is an adoption ceiling — external game devs sit on LTS for years. Plan of record: **v1 vendors a netstandard2.1 DTO subset covering only the client contract, generated rather than hand-maintained** — a small codegen step reflects over the real `Voxta.Model` assembly and emits the DTOs, and a CI round-trip serialization test against a pinned server build makes drift fail loudly at build time instead of silently at runtime. This directly replaces the manual sync headache. When Unity 6.8+ ships and reaches mainstream adoption, direct `Voxta.Model` reference becomes an optional simplification to revisit — an upgrade path, not a launch dependency.
- **R3 — protocol drift: MANAGED.** Agreed approach: the SDK checks the API contract against `ApiVersion` from `ServerWelcomeMessage` on connect, plus SDK CI runs against a pinned server build. The remaining piece is the M5 conversation with acidbubbles about versioning guarantees once the contract goes public.
- **R4 — scope creep toward avatars: MANAGED.** Agreed: no built-in avatar in v1 (see §5.4). The SDK's identity is "brain + voice + actions for *your* characters," which is what game devs actually need.

---

## 9. Success criteria

- A developer with no Voxta knowledge gets a talking companion in a fresh Unity project in under 15 minutes using only the README.
- Zero networking code written by the integrating developer.
- The protocol document is sufficient for the Unreal port to be built without reading voxta-server source.
