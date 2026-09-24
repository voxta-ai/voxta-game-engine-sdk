# Runtime model type migration map

**Scope:** Section 4 of `RUNTIME-MODEL-DEPENDENCY-UPGRADE-PLAN.md` for the pinned `Voxta.Model` 1.11.0-beta.1 assembly.  This is a source inventory and API comparison only; it makes no runtime-code changes.

## Inventory

`Runtime/Protocol/Generated/M1Messages.g.cs` was the generated DTO and serializer implementation. It has been removed from the runtime; this map preserves its public type inventory and migration guidance for consumers moving to `Voxta.Model`.

| Area | Generated-type use |
| --- | --- |
| Runtime transport | `IVoxtaTransport`, `VoxtaSignalRTransport`: client/server base types, authentication DTO, serializer options, raw `ReceiveMessage` registration |
| Runtime client/session | `VoxtaClient`, `VoxtaChatSession`: public events and every outgoing chat/action/app-trigger DTO |
| Runtime components | `VoxtaActions`, `VoxtaMicrophone`, `VoxtaSpeechPlayer`, `VoxtaCompanion`: action/context definitions, capability enums, and incoming message events |
| Tests and sample | `GeneratedProtocolTests` plus client, action, speech tests and the BasicIntegration sample: DTO construction, polymorphic wire tests, fake transport contracts, and `JsonElement` assertions |
| Preservation | `Runtime/link.xml` preserves the runtime and JSON/SignalR assemblies, but does not yet preserve `Voxta.Model` metadata. |

There are no other generated-protocol source files and no typed SignalR `ReceiveMessage` registration. `VoxtaSignalRTransport` registers `connection.On<JsonElement>("ReceiveMessage", ...)` and then deserializes the raw JSON itself.

## Namespace map

Every type below keeps its simple name unless stated otherwise. The generated namespace is `Voxta.Unity.Protocol.Generated`.

| Generated type(s) used by the SDK | `Voxta.Model` namespace / type | API and behavior notes |
| --- | --- | --- |
| `ClientMessage`, `ClientChatSessionMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientMessage`, `.ClientChatSessionMessage` | Same base relationship and mutable `SessionId`; model applies `[JsonPolymorphic]` / `[JsonDerivedType]` to `ClientMessage`. |
| `ClientAuthenticateMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientAuthenticateMessage` | Same fields and `Scope` default (`role:app`); `Client`, `ClientVersion`, and `Capabilities` are `init` properties, and `Client` / `Capabilities` are JSON-required. |
| `ClientStartChatMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientStartChatMessage` | Existing `ChatId`, `CharacterId`, `ScenarioId`, `Ephemeral`, `Title`, and inherited `DisableServices` remain. Model adds multi-character roles, dependencies, chat-memory, and augmentation properties; most are `init` properties. |
| `ClientSendMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientSendMessage` | Existing text, session, continuation/reply flags, and role remain. Model adds attachments, inference flags, character/trigger fields, expiry, and constraints. `DoReply` is `init`; continue and role remain mutable. |
| `ClientInterruptMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientInterruptMessage` | Same empty chat-session message. |
| `ClientSpeechPlaybackStartMessage`, `ClientSpeechPlaybackCompleteMessage` | `Voxta.Model.WebsocketMessages.ClientMessages` counterparts | Same wire fields. Required IDs/ranges are `init` properties; `IsNarration` is mutable. |
| `ClientUpdateContextMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientUpdateContextMessage` | Existing context key, contexts, and actions remain. Model adds events, flags, role enables, and arbitrary variables; message-shaping fields are mostly `init`. |
| `ClientTriggerActionMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientTriggerActionMessage` | Same session/message/action/argument shape. `MessageId` and `Value` are JSON-required and mutable. |
| `ClientAppTriggerCompleteMessage` | `Voxta.Model.WebsocketMessages.ClientMessages.ClientAppTriggerCompleteMessage` | Same `TriggerId`; it is `init` in the model. |
| `ServerMessage`, `ServerChatSessionMessage` | `Voxta.Model.WebsocketMessages.ServerMessages.ServerMessage`, `.ServerChatSessionMessage` | Model applies the complete server discriminator list to `ServerMessage`; session IDs remain required. The model recognizes many newer server frames, so `VoxtaClient.MessageReceived` can receive additional concrete model subclasses. |
| `ServerWelcomeMessage` | `Voxta.Model.WebsocketMessages.ServerMessages.ServerWelcomeMessage` | Existing version/API/user fields remain. Model adds `Favorite` and `Assistant`; `User` is required. |
| `ServerAuthenticationRequiredMessage`, `ServerErrorMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Same names/discriminators. `ServerErrorMessage` adds code, service/category/resolution details and constructors. |
| `ServerChatStartingMessage`, `ServerChatStartedMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Existing chat IDs/titles/messages remain. `ServerChatStartedMessage.ChatId` is inherited from `ServerChatConfigurationMessage`, which also carries required services, participants, augmentations, and configuration fields. |
| `ServerReplyStartMessage`, `ServerReplyChunkMessage`, `ServerReplyEndMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Existing fields remain. Model adds `SpeechLineId` to chunks and message/conversation indexes to reply end; most inbound properties are `init`. |
| `ServerSpeechPlaybackStartMessage`, `ServerSpeechPlaybackCompleteMessage`, `ServerInterruptSpeechMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Same fields and discriminators. |
| `ServerContextUpdatedMessage` | `Voxta.Model.WebsocketMessages.ServerMessages.ServerContextUpdatedMessage` | Same top-level protocol intent. Its entry types are nested model classes: `.ContextKeyEntry`, `.FunctionKeyEntry`, `.ToolEntry`, `.ControlGroupEntry`, `.RoleEntry`. `ControlGroupEntry.Controls` changes from `JsonElement[]` to `ScenarioControlDefinition[]`; `Variables` changes from `Dictionary<string, JsonElement>` to `Dictionary<string, object?>`. |
| `ContextKeyEntry`, `FunctionKeyEntry`, `ToolEntry`, `ControlGroupEntry`, `RoleEntry`, `FunctionInvocation`, `FunctionTiming` | Nested types of `ServerContextUpdatedMessage` and their model enum counterparts | Preserve their fields and enum meanings. Use the nested model entry names in code and tests; no corresponding top-level model entry types exist. |
| `ServerAnimationPlayMessage`, `ServerChatSessionErrorMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Existing fields remain. The error now derives from model `ServerErrorMessage`, retaining `Retry` with its `true` default. |
| `ServerActionMessage` | `Voxta.Model.WebsocketMessages.ServerMessages.ServerActionMessage` | Same action payload; nullable annotations are stricter and model adds `TryGetArgument`. |
| `ServerActionAppTriggerMessage` | `Voxta.Model.WebsocketMessages.ServerMessages.ServerActionAppTriggerMessage` | Same IDs/name/sender/scenario role, but `Arguments` is `object?[]?` instead of `JsonElement[]`. With the default model options, JSON object slots deserialize as `JsonElement`; callers must pattern-match/cast before calling `GetString`, `GetInt32`, or `GetBoolean`. |
| `ServerRecordingRequestMessage`, `ServerSpeechRecognitionStartMessage`, `ServerSpeechRecognitionPartialMessage`, `ServerSpeechRecognitionEndMessage`, `ServerAudioFrameMessage` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Existing fields and wire names remain. Recognition end makes `Text`, `Reason`, `Words`, and `Repair` nullable and defaults `MisheardWordsSuppression` to `"None"`. |
| `TranscriptWordInfo`, `TranscriptRepairInfo` | `Voxta.Model.WebsocketMessages.ServerMessages` counterparts | Model exposes immutable positional records rather than mutable DTO classes. |
| `ClientCapabilities`, `AudioInputClientCapabilities`, `AudioOutputClientCapabilities`, `VisionCaptureClientCapabilities` | `Voxta.Model.Shared` counterparts | Existing enum values retain their ordinals; each capability enum adds `Disabled`. `ClientCapabilities.AcceptedAudioContentTypes` defaults to WAV **and** MPEG (generated layer was WAV only), and audio output defaults to `Url` (generated default was `None`). Model also adds audio folder, vision sources, helpers, and `Disabled`. The SDK must explicitly advertise only `audio/x-wav`: Unity has no native MPEG/MP3 playback path and audio conversion is outside this SDK's scope. |
| `ContextDefinition`, `ScenarioActionDefinition`, `FunctionArgumentDefinition`, `ActionInvocationArgument` | `Voxta.Model.Shared` counterparts | Existing fields remain. `ScenarioActionDefinition` now inherits `FunctionDefinition`, gaining timing/invocation/filter/effect/metadata fields. Several properties are now `init`; continue constructing them with object initializers. |
| `FunctionArgumentType`, `PromptCategories`, `PromptPositions`, `ChatMessageRole` | `Voxta.Model.Shared` counterparts | Existing numeric enum members and values match. `PromptPositions` is marked `[Flags]` in the model. |
| `FlagInfo`, `ChatParticipantInfo`, `ChatMessage` | `Voxta.Model.Shared` counterparts | Existing fields remain. Participants, chat messages, and flags have additional model metadata; model `ChatMessage` adds appearance, indexes, chat time, attachments, annotations, and tool calls. |

## Serialization and registration comparison

The generated `VoxtaJson.CreateOptions()` uses camel case, null omission, a numeric-only `ChatMessageRole` converter, and hand-written client/server converters. Its server converter recognizes 21 discriminators and throws for all other `$type` values.

`Voxta.Model.Serialization.VoxtaJsonSerializer.CreateSerializeOptions()` uses camel case, null omission, named-floating-point support, boolean-string and nullable-GUID converters, and `JsonStringEnumConverter`. The model's `ClientMessage` and `ServerMessage` classes carry the `$type` discriminator registrations through `JsonPolymorphic` / `JsonDerivedType`; the registered sets include every generated discriminator plus newer model messages. Consequently, code must use `VoxtaJsonSerializer.CreateSerializeOptions()` for both SignalR payload options and direct message serialization/deserialization.

This changes enum wire behavior: the generated serializer wrote `ChatMessageRole.User` as `3`, whereas the model serializer writes enum names as strings (for example, `"User"`). Section 5 must add wire-compatibility tests for each SDK-emitted frame before the generated layer is removed.

The existing raw SignalR registration remains appropriate:

1. keep `connection.On<JsonElement>("ReceiveMessage", HandleReceiveMessage)`;
2. deserialize the raw text as the model `ServerMessage` using model options;
3. retain the current `JsonException` boundary so an unknown future discriminator cannot stop the connection or later frames; and
4. retain the special error reporting for a malformed `action` frame, while deciding whether newly supported model messages should be surfaced or ignored by higher-level SDK components.

## Public API and IL2CPP implications

`VoxtaClient`, `VoxtaChatSession`, `VoxtaActions`, and `VoxtaCompanion` expose generated types in public events, methods, UnityEvents, and `VoxtaChatSessionException`. Replacing them with model types is a public namespace/type identity change, even where simple type names match. The release notes and package version must describe this migration.

Before removing the generated source, add `Voxta.Model` preservation to `Runtime/link.xml` and validate representative polymorphic deserialization in an IL2CPP player. The model's attribute-based discriminator metadata, including all concrete server types the SDK handles, must survive stripping.

## Required source edits identified by the inventory

- Replace every `using Voxta.Unity.Protocol.Generated` with the three model namespaces needed by that file: client messages, server messages, and/or shared types.
- Replace every `VoxtaJson.CreateOptions()` call with `VoxtaJsonSerializer.CreateSerializeOptions()`.
- Update test/sample app-trigger argument assertions from direct `JsonElement` calls to checked `JsonElement` casts/patterns, and do the same for `ServerContextUpdatedMessage.Variables` tests.
- Explicitly set Unity capability `AcceptedAudioContentTypes` to only `audio/x-wav`; do not rely on the model's expanded WAV-and-MPEG default. MPEG/MP3 playback and conversion are outside this SDK's scope.
- Retain the model-provided `$type` registrations rather than reproducing the generated discriminator switch.
