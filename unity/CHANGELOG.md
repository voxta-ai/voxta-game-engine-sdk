# Changelog

## 0.1.0-pre.2

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

- Initial M1 text-chat spike package structure.
