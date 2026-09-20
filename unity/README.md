# Voxta Game Engine SDK

`com.voxta.game-engine-sdk` is the official Unity client package for a Voxta
server. M1 provides a text-chat transport spike for Unity 2022.3 LTS.

Install from a local path or git URL. The managed SignalR dependency closure is
included in the package; projects do not need a scoped NuGet registry.

The initial public runtime is intentionally text-only. Configure a server URL,
API key, character ID, and scenario ID through the M1 companion API once the
sample is complete.

For IL2CPP, import the Basic Chat Integration sample. Its `link.xml` is copied into
`Assets` and preserves SignalR's reflection-only members. When creating a scene
without the sample, copy `Samples~/BasicIntegration/link.xml` into the project's
`Assets` directory; Unity does not process linker files inside UPM packages.

See `Documentation~/UNITY-SDK-DEVELOPMENT-PLAN.md` for the active checklist.
