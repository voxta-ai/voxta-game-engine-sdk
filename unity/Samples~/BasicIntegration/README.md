# Basic Chat Integration

Import this sample through Package Manager, then open `BasicIntegration.unity`.
Select the **Voxta Companion** object and enter a character ID and optional scenario
ID. Leave its API Key empty. Its Server URL defaults to the local server at
`http://127.0.0.1:5384`; change it before Play if your local server uses a different
HTTP or HTTPS address.

Start the local Voxta server, press Play, then use the displayed verification URL and
code to approve **Unity Basic Integration**. The sample polls until approval, validates
the returned token, saves it in PlayerPrefs, and then connects automatically. Later
runs validate and reuse that saved authorization. Enter text and select **Send** once
the chat connects.

The imported sample includes the required IL2CPP linker configuration. If you use
`VoxtaCompanion` outside this sample, copy `link.xml` from this folder into your
project's `Assets` directory before creating an IL2CPP build. Unity does not apply
`link.xml` files stored inside UPM packages.

The companion includes `VoxtaSpeechPlayer` and an `AudioSource`. With **Local Server
Audio Output** disabled, it advertises `audioOutput: Url`, downloads reply audio, and
plays it through that source. On a local server, also configure **Audio Output** to use
the client capability; a server setting that prefers its own Audio Output service
overrides `audioOutput: Url` and plays sound outside Unity.

The sample also includes an enabled `VoxtaMicrophone`. When a local microphone is
available, the companion advertises `audioInput: WebSocketStream`; the server opens
capture with a `recordingRequest`, and the sample diagnostics show recognition and
audio-frame (VAD) events. Configure the local server to use the client audio-input
capability and an STT service. Disable or remove `VoxtaMicrophone` to advertise
`audioInput: None`.

Samples already imported into `Assets/Samples/` are copies. Re-import the sample,
or add an enabled `VoxtaSpeechPlayer` alongside the `AudioSource` on your existing
**Voxta Companion** object, after updating the package.

The sample also includes an `AudioListener`. A Unity scene must have exactly one
enabled listener; remove this one if your camera already has an `AudioListener`.
