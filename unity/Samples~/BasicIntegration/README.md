# Basic Chat Integration

Import this sample through Package Manager, then open `BasicIntegration.unity`.
Select the **Voxta Companion** object and enter a local-server API key, character ID,
and optional scenario ID. Press Play, enter text, and select **Send**.

The imported sample includes the required IL2CPP linker configuration. If you use
`VoxtaCompanion` outside this sample, copy `link.xml` from this folder into your
project's `Assets` directory before creating an IL2CPP build. Unity does not apply
`link.xml` files stored inside UPM packages.

The companion includes `VoxtaSpeechPlayer` and an `AudioSource`. With **Local Server
Audio Output** disabled, it advertises `audioOutput: Url`, downloads reply audio, and
plays it through that source. On a local server, also configure **Audio Output** to use
the client capability; a server setting that prefers its own Audio Output service
overrides `audioOutput: Url` and plays sound outside Unity. No microphone or device
flow is included.

Samples already imported into `Assets/Samples/` are copies. Re-import the sample,
or add an enabled `VoxtaSpeechPlayer` alongside the `AudioSource` on your existing
**Voxta Companion** object, after updating the package.

The sample also includes an `AudioListener`. A Unity scene must have exactly one
enabled listener; remove this one if your camera already has an `AudioListener`.
