# Basic Chat Integration

Import this sample through Package Manager, then open `BasicIntegration.unity`.
Select the **Voxta Companion** object and enter a local-server API key, character ID,
and optional scenario ID. Press Play, enter text, and select **Send**.

The imported sample includes the required IL2CPP linker configuration. If you use
`VoxtaCompanion` outside this sample, copy `link.xml` from this folder into your
project's `Assets` directory before creating an IL2CPP build. Unity does not apply
`link.xml` files stored inside UPM packages.

The sample is intentionally text-only for M1. It uses a `VoxtaCompanion` and a small
UGUI binding script; no microphone, speech playback, or device flow is included.
