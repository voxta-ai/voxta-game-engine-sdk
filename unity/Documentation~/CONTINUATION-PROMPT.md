# Continuation Prompt: Continue Unity SDK M1 Implementation

Continue implementing the Voxta Unity Game Engine SDK directly in this repository. Persist until the active M1 work is complete or a concrete external blocker remains.

The UPM package root is `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. The package identity is fixed:

```json
"name": "com.voxta.game-engine-sdk",
"version": "0.1.0-pre.1"
```

Start by reading, in order:

1. `Documentation~/UNITY-SDK-DEVELOPMENT-PLAN.md`
2. `Documentation~/VOXTA-GAME-ENGINE-SDK-DESIGN.md`
3. `Runtime/Transport/VoxtaSignalRTransport.cs`
4. `Runtime/Protocol/Generated/M1Messages.g.cs`
5. `Documentation~/third-party-dependencies.md`
6. `G:\voxta-projects\GitHub\voxta-server\src\client\Voxta.Client\VoxtaWebsocketsClient.cs`
7. `G:\voxta-projects\GitHub\voxta-server\src\client\Voxta.Client\VoxtaWebsocketsClientFactory.cs`
8. `G:\voxta-projects\GitHub\voxta-server\src\client\Voxta.Client\WebsocketsUrlHelper.cs`

## Current implementation state

M1 package, generated-protocol, and transport-foundation tasks have been completed and recorded in `UNITY-SDK-DEVELOPMENT-PLAN.md`. Do not undo completed checkboxes; mark more only after their stated verification passes.

- Package foundation files, runtime/editor/test asmdefs, README, changelog, license, third-party notices, and `Runtime/link.xml` exist and compile in Unity 2022.3.62f3.
- `Runtime/Plugins/ThirdParty/` contains the 27-package Voxy lock-resolved netstandard2.0 SignalR/System.Text.Json closure, copied licenses, inventory, and Unity plugin metadata.
- Voxy declares SignalR 8.0.2 and System.Text.Json 8.0.0, but its lock graph resolves System.Text.Json to 8.0.2. The package intentionally vendors the resolved 8.0.2 assembly; see `third-party-dependencies.md`.
- `Runtime/Transport/` has canonical hub URL normalization, a main-thread dispatcher, and a SignalR transport configured for WebSockets only, skipped negotiation, bearer token access, automatic reconnect/re-authentication, and queued sends.
- `Microsoft.Bcl.AsyncInterfaces.dll` remains vendored because SignalR requires it, but must not be listed in `Runtime/Voxta.Unity.Runtime.asmdef` precompiled references: Unity's .NET Standard 2.1 supplies the duplicate `IAsyncDisposable` type. Unity compilation passed after removing that explicit reference.
- `Tools/ProtocolGenerator/` is a standalone .NET 10 tool. It validates the required M1 roots against the pinned `Voxta.Model` build at server commit `b533b287194af6fc418900d1384fb2fda2f7d286`, regenerates `Runtime/Protocol/Generated/M1Messages.g.cs`, and supports `--check` drift validation. It is never included in Unity runtime assemblies.
- `Runtime/VoxtaClient.cs` and `Runtime/VoxtaChatSession.cs` are initial implementations. They need full transport/session tests and review before their M1 plan tasks are checked.
- `Tests/Runtime/GeneratedProtocolTests.cs` covers generated-message discriminator serialization. All current SDK tests pass in Unity's Test Runner window. The batch runner compiled the test assembly but did not emit XML because Unity's licensing module could not refresh a token.
- A fresh Unity 2022.3.62f3 project at `G:\Unity\voxtaSDK-sandbox\M1FreshInstall` installed the package by local path using only the default Unity registry and produced `Voxta.Unity.Runtime.dll`.

## Immediate next work

1. Complete and test `VoxtaClient`: public disconnect, authentication/welcome state transitions, reconnect re-authentication, API-version rejection, typed send, and error propagation. Ensure every public networking callback reaches Unity's main thread.
2. Complete and test `VoxtaChatSession`: start chat, correlate `chatStarted`, send text, route streamed `replyChunk`, and finish on `replyEnd`.
3. Add transport unit tests for URL construction, dispatch, reconnect re-authentication, and main-thread delivery. Mark the client/session/transport-test plan tasks only after they pass in Unity Test Runner.
4. Implement the minimal text-only `VoxtaCompanion`, its C# events and UnityEvents, inspector fields, and `Samples~/BasicIntegration`; then validate a local-server text chat.
5. Add CI after the local workflow is stable: check out `voxta-server` at the SHA in `Tools/ProtocolGenerator/pinned-model.json`, build `Voxta.Model` with .NET 10, run generator `--check`, and run Unity tests. A scheduled workflow may propose a PR for a newer server pin, but merging remains a human compatibility decision.

## Constraints

- Unity baseline: 2022.3 LTS and .NET Standard 2.1.
- Never reference or package the current .NET 10 `Voxta.Client` or `Voxta.Model` assemblies. Use them only as behavior/protocol references.
- Voxy's client code is source reference only. Keep package-owned transport code under `Runtime/Transport`.
- Public networking callbacks must be marshalled to Unity's main thread.
- Keep M1 text-only. Do not begin speech, microphone, device flow, actions, or prefab polish before prerequisites are done.
- Preserve user changes, use `apply_patch` for edits, avoid destructive git commands, and check a plan item only after verification passes.
