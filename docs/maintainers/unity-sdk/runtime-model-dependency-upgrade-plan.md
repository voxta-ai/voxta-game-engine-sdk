# Runtime `Voxta.Model` Dependency Upgrade Plan

**Status:** Proposed  
**Package root:** `unity/`  
**Decision:** Replace the generated `Runtime/Protocol/Generated/M1Messages.g.cs` DTOs with the pinned `Voxta.Model` runtime assembly after a compatible managed-dependency upgrade.  
**Unity baseline:** Unity 2022.3 LTS / .NET Standard 2.1

## Goal and completion criteria

The public runtime uses `Voxta.Model.dll` directly for protocol messages and JSON serialization. The package ships one coherent, pinned dependency closure based on SignalR 10 and `System.Text.Json` 10.0.12. It compiles and runs in supported Unity Mono and IL2CPP players without relying on NuGet restore in consuming projects.

- [x] `Voxta.Model.dll` is enabled for supported Unity platforms and referenced by `Voxta.Unity.Runtime.asmdef`.
- [x] No generated protocol DTO source is compiled or needed at runtime. The generated source was removed after clean package validation rebuilt `Voxta.Unity.Runtime.dll` and `Voxta.Unity.Runtime.Tests.dll` successfully on 2026-09-23.
- [x] SignalR connections, device authorization, chat, action, microphone, and reply audio retain their current behavior. Verified in the BasicIntegration live-server run on 2026-09-23.
- [x] Runtime and package-validation tests pass. The clean package-validation project reported 65 passed and 4 explicit live-server tests skipped; the sandbox validation reported 66 passed and 4 skipped after compiling the imported sample against `Voxta.Model`.
- [x] Windows Mono and Linux IL2CPP player builds pass in CI. The hosted Unity package workflow passed both desktop builds on 2026-09-24.
- [x] A live-server compatibility run confirms authentication, text, actions, audio playback, and microphone streaming. BasicIntegration verified device authorization and saved-token reuse, chat startup and text replies, the `wave` callback, authenticated spatial reply audio, and microphone capture/recording on 2026-09-23.

## Boundaries

- [x] Keep the vendored model package version explicitly pinned; do not dynamically restore NuGet packages in Unity projects or consumer CI.
- [x] Remove the obsolete protocol generator after the runtime migration. The former generator source and its generated DTO output were deleted; the model pin and closure audit remain under `Tools/ModelDependencies/VoxtaModel/`.
- [ ] Do not mix assemblies from SignalR 8 and SignalR 10 in the shipped package.
- [ ] Do not replace only `System.Text.Json.dll`; every assembly in the transitive closure must be selected and tested together.
- [ ] Keep the existing non-blocking NuGet-version notice. A newer model version remains an intentional update, not an automatic runtime upgrade.

## 1. Establish the target dependency closure

- [x] Select an exact SignalR 10 patch version compatible with `System.Text.Json` 10.0.12. SignalR 10.0.12 was selected to match the model-required JSON 10.0.12 servicing release.
- [x] Restore the target closure in a temporary .NET Standard 2.1 probe project using `Voxta.Model` 1.11.0-beta.1 and the selected SignalR version. The locked probe is in `Tools/ModelDependencies/VoxtaModel/Probe/`.
- [x] Record the exact package IDs, package versions, source URLs, SHA-512 hashes, licenses, and selected `lib/netstandard2.0` or `lib/netstandard2.1` assets. The machine-readable inventory is in `Compatibility~/RuntimeModelUnity2022Probe/Assets/Plugins/CandidateClosure/candidate-assembly-inventory.json`; the review document is `runtime-model-dependency-closure.md`.
- [x] Include at minimum: SignalR client/core/common/JSON protocol, HTTP connections client/common, connections abstractions, Microsoft.Extensions dependency injection/logging/options/primitives/features, `Microsoft.Bcl.TimeProvider`, `System.Text.Json`, `System.Text.Encodings.Web`, `System.IO.Pipelines`, `System.Threading.Channels`, diagnostic source, and unsafe/runtime support dependencies selected by restore.
- [x] Compare the target closure against the currently vendored third-party files and identify removals, additions, and version replacements. The candidate contains 24 DLLs, adds `System.Net.ServerSentEvents` and `Voxta.Model`, and omits platform-provided legacy support DLLs.
- [x] Update `Third Party Notices.md` and `Documentation~/third-party-dependencies.md` with the final inventory and license notices.

## 2. Prove Unity compatibility before replacing the runtime

- [x] Create an isolated Unity 2022.3 test project that imports only the candidate managed closure and `Voxta.Model.dll`. It is in `Compatibility~/RuntimeModelUnity2022Probe/` and uses Unity 2022.3.62f3.
- [x] Enable the candidate DLLs only for the Unity platforms the SDK supports; ensure editor, standalone Mono, and IL2CPP import settings are explicit. All 24 candidate DLLs disable `Any platform` and enable Editor plus standalone Windows, Linux, and macOS.
- [x] Compile a small assembly that constructs representative model messages and serializes/deserializes them with the target `System.Text.Json` assembly. The Editor verification serializes and deserializes `ClientAuthenticateMessage` with `VoxtaJsonSerializer.CreateSerializeOptions()`.
- [x] Run that probe in the Unity Editor and a development Mono player. Editor batch verification and the Windows x64 Mono player passed on 2026-09-23.
- [x] Build and run an IL2CPP player; resolve stripping/linker failures with narrowly scoped preservation rules. The Windows x64 IL2CPP player passed with the project's normal stripping settings; no preservation rule was required for the exercised model serialization path.
- [x] Inspect Unity logs for duplicate assembly identities, missing methods, unsupported APIs, reflection failures, and serialization warnings. No candidate-assembly, linker, reflection, or serialization issue was reported by either player. Headless runs report the expected Null graphics-device shader messages and a development-debugger port warning; neither affects the probe.
- [x] Record the Unity version, target platforms, and outcome in the completion record.

### Section 2 completion record

| Date | Unity | Target | Backend | Result |
| --- | --- | --- | --- | --- |
| 2026-09-23 | 2022.3.62f3 | Windows x64 development player | Mono | Built and ran successfully. `ClientAuthenticateMessage` serialized and deserialized with `VoxtaJsonSerializer.CreateSerializeOptions()`. |
| 2026-09-23 | 2022.3.62f3 | Windows x64 development player | IL2CPP | Built, linked, and ran successfully with stripping enabled. The same JSON round trip passed; no `link.xml` preservation was needed for this path. |

The probe stores its build scene at `Compatibility~/RuntimeModelUnity2022Probe/Assets/RuntimeModelClosureProbe.unity` and has Editor menu/build entry points for the two Windows development players. Linux and macOS remain platform validation work for the later package/CI stage.

## 3. Vendor the selected closure reproducibly

- [x] Download the selected package assets outside the Unity runtime and verify each artifact against its recorded hash.
- [x] Replace the managed dependency closure in `Runtime/Plugins/ThirdParty/` and the pinned `Voxta.Model.dll` in `Runtime/Plugins/Voxta/` as one atomic update.
- [x] Move `Voxta.Model.dll` from its disabled generator-only import configuration to the runtime plugin configuration.
- [x] Update plugin `.meta` files so required runtime DLLs are enabled and generator-only tooling assets remain excluded from Unity as appropriate.
- [x] Add or update a repeatable maintenance script that validates the vendored filenames, versions, and hashes without restoring them at consumer build time.
- [x] Add an upstream-model compatibility canary. The scheduled `voxta-model-update-compatibility-tests.yml` workflow tests a newer `Voxta.Model` DLL in an isolated package without changing the production pin.

## 4. Replace generated DTO use with model types

- [x] Inventory every public and internal reference to `M1Messages.g.cs`, generated serializer options, converters, discriminators, and message-registration code. See `runtime-model-type-migration-map.md`.
- [x] Map each generated message type to its `Voxta.Model` equivalent, including namespaces, property names, constructors, enums, and nullable/default behavior. See `runtime-model-type-migration-map.md`.
- [x] Update `VoxtaClient`, `VoxtaChatSession`, `VoxtaActions`, `VoxtaAuth`, `VoxtaMicrophone`, `VoxtaSpeechPlayer`, and transport code to use model types directly.
- [x] Replace generated polymorphic `$type` dispatch with the model-supported serialization configuration, preserving safe handling of unknown future messages.
- [x] Preserve current public SDK API behavior where feasible; document and version any unavoidable public type changes. `0.1.0-pre.2` documents the generated-namespace-to-`Voxta.Model` type-identity migration in `../../../unity/Documentation~/runtime-model-public-api-migration.md` and the package changelog.
- [x] Update `link.xml` to preserve model message types and serialization metadata needed by IL2CPP.
- [x] Remove `M1Messages.g.cs` and generated-runtime assembly references only after all replacement code compiles and tests pass. Removed `Runtime/Protocol/Generated/M1Messages.g.cs` and its Unity metadata; clean package validation rebuilt the runtime and test assemblies successfully, and the preceding validation suite had 65 passes with 4 explicit live-server skips.

## 5. Rebuild tests around the model assembly

- [x] Rewrite generated-protocol tests as model compatibility tests: discriminator values, representative deserialization, enum handling, optional fields, and unknown-message behavior. `ModelProtocolTests` uses `Voxta.Model` and `VoxtaJsonSerializer.CreateSerializeOptions()`; `TransportTests` verifies unknown frames are ignored while malformed `action` frames report errors.
- [x] Retain coverage for authentication, chat lifecycle, actions, device authorization, speech lifecycle, and microphone protocol frames using model types. Runtime tests cover the device-code and token flow, model chat/action/speech messages, microphone stream endpoint and PCM frames; BasicIntegration covers the model `wave` definition. The 2026-09-23 live run additionally verified the explicit device-approval, text/action, reply-audio, and microphone paths.
- [x] Add regression tests that verify `System.Text.Json` 10 serialization sends the same wire frames required by the pinned server contract. `ModelProtocolTests.SdkEmittedFramesUsePinnedDiscriminatorsAndStringEnums` covers every SignalR client frame emitted by the SDK, including polymorphic `$type` output and string enum values.
- [x] Add a test that fails when an accidental SignalR 8 / JSON 8 DLL is introduced alongside the target closure. `Tools/ModelDependencies/VoxtaModel/Validate-VoxtaModelDependencyClosure.ps1` rejects assembly identities at major version 8 and requires the inventory's SignalR/JSON assets to be major version 10.
- [x] Keep live-server and device-approval tests explicit; run them before declaring the migration complete. The four live-server tests remain explicit/skipped by default; the 2026-09-23 BasicIntegration run completed device approval and saved-token reuse, chat/text, `wave`, authenticated spatial reply audio, and microphone recording.

## 6. Validate package delivery and release readiness

- [x] Run the clean Unity package-validation project with the target DLL closure. Unity 2022.3.62f3 PlayMode validation discovered 69 tests: 65 passed, 0 failed, and 4 explicit live-server tests were skipped.
- [x] Run CI package tests, Windows Mono build, and Linux IL2CPP build. The hosted workflow passed all three validations on 2026-09-24; it runs for pull requests, manual dispatch, and pushed tags.
- [x] Verify build output contains exactly one intended `System.Text.Json` assembly and the expected SignalR/model assemblies. The Unity package CI workflow validates the Windows Mono player against the pinned assembly hashes after its build.
- [x] Run the BasicIntegration sample against a compatible Voxta server: device flow, saved token reuse, text reply, `wave` action, authenticated reply-audio download, spatial playback, and microphone input. Verified in the sandbox live run on 2026-09-23.
- [x] Review binary/package size and platform support changes for release notes. The managed closure changes from 27 DLLs / 2,461,632 bytes to 24 DLLs / 2,768,792 bytes (+307,160 bytes); it supports the Editor and standalone Windows, Linux, and macOS targets.
- [x] Update package documentation to state that `Voxta.Model` is now a runtime dependency and identify the pinned version. `README.md` and `Documentation~/third-party-dependencies.md` identify the vendored `Voxta.Model` 1.11.0-beta.1 and its locked closure.
- [x] Create a dedicated release note describing the dependency upgrade and any public API migration guidance. See `Documentation~/release-notes/0.1.0-pre.2-runtime-model-upgrade.md`.

## Rollback point

- [ ] Keep the last known-good SignalR 8 / generated-DTO package revision tagged before enabling the runtime model assembly.
- [ ] If a Unity platform probe or live-server validation fails, revert the dependency closure and retain the current generated DTO runtime while the incompatibility is investigated.
