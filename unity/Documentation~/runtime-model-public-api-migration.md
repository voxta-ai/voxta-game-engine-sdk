# Runtime model public API migration

## Release scope

`0.1.0-pre.2` replaces the SDK's generated protocol assembly types in
`Voxta.Unity.Protocol.Generated` with the corresponding types from the pinned
`Voxta.Model` `1.11.0-beta.1` runtime assembly. This is a source and binary
compatibility break for application code that mentions a protocol type, even
when the simple type name did not change. Update every affected `using`, fully
qualified name, generic argument, and serialized UnityEvent binding.

The SDK now serializes protocol messages with
`Voxta.Model.Serialization.VoxtaJsonSerializer.CreateSerializeOptions()`.
The generated `VoxtaJson.CreateOptions()` type is no longer part of the
runtime path.

## Namespace replacements

| Previous namespace | Replacement namespace | Types exposed through the SDK |
| --- | --- | --- |
| `Voxta.Unity.Protocol.Generated` | `Voxta.Model.WebsocketMessages.ClientMessages` | `ClientMessage`, `ClientChatSessionMessage`, `ClientAuthenticateMessage`, `ClientStartChatMessage`, `ClientSendMessage`, `ClientInterruptMessage`, `ClientSpeechPlaybackStartMessage`, `ClientSpeechPlaybackCompleteMessage`, `ClientUpdateContextMessage`, `ClientTriggerActionMessage`, `ClientAppTriggerCompleteMessage` |
| `Voxta.Unity.Protocol.Generated` | `Voxta.Model.WebsocketMessages.ServerMessages` | `ServerMessage`, `ServerChatSessionMessage`, `ServerWelcomeMessage`, `ServerAuthenticationRequiredMessage`, `ServerErrorMessage`, `ServerChatStartingMessage`, `ServerChatStartedMessage`, `ServerReplyStartMessage`, `ServerReplyChunkMessage`, `ServerReplyEndMessage`, `ServerSpeechPlaybackStartMessage`, `ServerSpeechPlaybackCompleteMessage`, `ServerInterruptSpeechMessage`, `ServerContextUpdatedMessage`, `ServerAnimationPlayMessage`, `ServerChatSessionErrorMessage`, `ServerActionMessage`, `ServerActionAppTriggerMessage`, `ServerRecordingRequestMessage`, `ServerSpeechRecognitionStartMessage`, `ServerSpeechRecognitionPartialMessage`, `ServerSpeechRecognitionEndMessage`, `ServerAudioFrameMessage`, `TranscriptWordInfo`, `TranscriptRepairInfo` |
| `Voxta.Unity.Protocol.Generated` | `Voxta.Model.Shared` | `ClientCapabilities`, audio capability enums, `ActionInvocationArgument`, `ContextDefinition`, `ScenarioActionDefinition`, `FunctionArgumentDefinition`, `FunctionArgumentType`, `PromptCategories`, `PromptPositions`, `ChatMessageRole`, `ChatMessage`, `ChatParticipantInfo`, `FlagInfo` |

The complete generated-to-model DTO mapping, including types that are not in a
public SDK signature but can appear in model payloads, is maintained in
[`runtime-model-type-migration-map.md`](runtime-model-type-migration-map.md).

## Affected SDK surface

| SDK surface | Model types now used |
| --- | --- |
| `VoxtaClient` constructor and `Send` method | `ClientCapabilities`, `ClientMessage` |
| `VoxtaClient.WelcomeReceived` and `MessageReceived` | `ServerWelcomeMessage`, `ServerMessage` |
| `VoxtaChatSession` events | `ServerChatStartedMessage`, `ServerReplyChunkMessage`, `ServerReplyEndMessage`, `ServerActionMessage`, `ServerActionAppTriggerMessage`, `ServerContextUpdatedMessage`, `ServerAnimationPlayMessage`, `ServerChatSessionErrorMessage` |
| `VoxtaChatSession.TriggerAction` | `ActionInvocationArgument[]` |
| `VoxtaActions`, `VoxtaActionUnityEvent`, and registered handlers | `ServerActionMessage` |
| `VoxtaMicrophone` events | `ServerSpeechRecognitionStartMessage`, `ServerSpeechRecognitionPartialMessage`, `ServerSpeechRecognitionEndMessage`, `ServerAudioFrameMessage` |
| `VoxtaSpeechPlayer.SpeechStarted` | `ServerReplyChunkMessage` |
| `VoxtaCompanion` C# events and typed UnityEvents | all corresponding server message types listed above |
| `VoxtaChatSessionException.ServerMessage` | `ServerChatSessionErrorMessage` |

## Consumer migration

Replace the generated namespace import with the model namespaces required by
the code. A typical component that sends messages and consumes chat events now
starts with:

```csharp
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
```

Update callback, delegate, and UnityEvent type arguments in the same way. For
example, `Action<Voxta.Unity.Protocol.Generated.ServerActionMessage>` becomes
`Action<Voxta.Model.WebsocketMessages.ServerMessages.ServerActionMessage>`.
Existing serialized UnityEvent persistent calls whose argument type was a
generated DTO must be reselected after the package update because Unity stores
the assembly-qualified type identity.

The model has stricter nullability and uses `init` for many message properties.
Continue to construct messages with object initializers; code that mutates one
of those properties after construction must set it in the initializer instead.

Two payload-shape changes need explicit consumer handling:

- `ServerActionAppTriggerMessage.Arguments` is `object?[]?`. With the SDK's
  default model options, object-valued JSON slots are `JsonElement`; check or
  cast before calling `GetString`, `GetInt32`, or similar methods.
- `ServerContextUpdatedMessage` entry classes are nested beneath
  `ServerContextUpdatedMessage`, `Variables` is `Dictionary<string, object?>`,
  and `ControlGroupEntry.Controls` is `ScenarioControlDefinition[]`. Replace
  former top-level entry type references with their nested model names.

The model writes enums as strings. Code that serializes an SDK message must use
`VoxtaJsonSerializer.CreateSerializeOptions()` and expect, for example,
`ChatMessageRole.User` to produce `"User"` rather than `3`.

`ClientCapabilities` adds values and has broader model defaults. The SDK itself
continues to advertise only `audio/x-wav`; applications that construct their
own capabilities should set `AcceptedAudioContentTypes` explicitly when they
require the same Unity audio contract.
