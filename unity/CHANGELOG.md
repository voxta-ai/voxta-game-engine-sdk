# Changelog

## 0.1.0-beta.2

- Fixed CS1705 compile errors on import in Unity 6.5 and later. Those editors
  compile against their own `System.Text.Json` 8, so the runtime closure moves
  from SignalR 10 / `System.Text.Json` 10.0.12 to SignalR 8.0.32 /
  `System.Text.Json` 8.0.6. `Voxta.Model` now references `System.Text.Json` 8.
- On Unity 6.5+, the package no longer loads its own copies of the assemblies
  the editor supplies. `System.Net.ServerSentEvents` is no longer shipped.
- **Breaking:** the `Voxta.Unity` namespaces are now `Voxta.GameEngine`
  (`Voxta.GameEngine`, `Voxta.GameEngine.Transport`), and the runtime assembly
  is now `Voxta.GameEngine.Runtime`. Update `using` directives and any
  assembly definition that references `Voxta.Unity.Runtime`.
- The saved device-flow token now uses the PlayerPrefs key
  `Voxta.GameEngine.AccessToken`, so existing installs authorize once more.
  The client reports itself to the server as `Voxta Game Engine SDK`.
- The SDK assemblies now compile only for the platforms the bundled plug-ins
  support: the Editor and Windows x64, macOS, and Linux x64 standalone players.
  Other build targets skip the SDK instead of failing to compile.
- Fixed authentication hanging after connecting: messages now keep their
  `$type` discriminator under SignalR 8.
- `VoxtaCompanion.Actions`, `Microphone`, and `SpeechPlayer` resolve on first
  access, so scripts can read them in their own `Awake`.
- Added `VoxtaCompanion.CharacterId` and `SetCharacterId`; the Basic
  Integration sample asks for a missing character ID in its panel.

## 0.1.0-beta.1

- Replaced the generated `Voxta.Unity.Protocol.Generated` protocol DTOs used by
  the public SDK API with `Voxta.Model` 1.11.0-beta.1 types. This is a source
  and binary compatibility break for applications that reference protocol
  types, callback delegates, or typed UnityEvents. See
  `Documentation~/runtime-model-public-api-migration.md` for migration guidance.
- The SDK now uses `VoxtaJsonSerializer.CreateSerializeOptions()` from
  `Voxta.Model`; protocol enums serialize as string values.
- The packaged runtime now includes the pinned `Voxta.Model` dependency and its
  SignalR 10 / `System.Text.Json` 10.0.12 closure.

## 0.1.0-pre.1

- Initial text-chat preview package.
