# Voxta Game Engine SDK for Unity

`com.voxta.game-engine-sdk` connects a Unity 2022.3 LTS project to a Voxta
server. The **Basic Chat Integration** sample is the fastest way to get a
talking companion: text chat, microphone input, Unity-owned spatial reply audio,
and a game-action callback are ready in one scene.

## Quickstart: a talking companion in 15 minutes

Before starting, run a Voxta server that has a character you can use. You need
that character's ID; the scenario ID is optional. The sample defaults to the
local server URL `http://127.0.0.1:5384`.

### 1. Install the package and import the sample

In Unity 2022.3 LTS or later:

1. Open **Window > Package Manager**.
2. Select **+ > Add package from git URL** and enter
   `https://github.com/voxta-ai/voxta-game-engine-sdk.git?path=/unity`.
   For a local checkout, use **Add package from disk** and select its
   `unity/package.json` instead.
3. Select **Voxta Game Engine SDK**, open **Samples**, and import **Basic Chat
   Integration**.
4. Open
   `Assets/Samples/Voxta Game Engine SDK/0.1.0-pre.2/Basic Chat Integration/BasicIntegration.unity`.

Importing the sample places its `link.xml` under `Assets`, which is needed for
IL2CPP builds. If you build a different scene with `VoxtaCompanion`, copy the
sample's `link.xml` into that project's `Assets` directory.

### 2. Configure the companion

Select **Voxta Companion** in the scene hierarchy. In the `VoxtaCompanion`
inspector, set:

| Field | Value |
| --- | --- |
| **Server URL** | Your Voxta server's HTTP or HTTPS base URL. Keep `http://127.0.0.1:5384` for the default local server. |
| **Character ID** | The required character GUID from Voxta. |
| **Scenario ID** | An optional scenario GUID. |
| **API Key** | Leave empty. The sample obtains and stores its own device-flow token. |
| **Local Server Audio Output** | Leave disabled so Unity receives reply-audio URLs and plays them itself. |

The companion is disabled in the saved scene on purpose. The sample enables it
only after authorization succeeds.

### 3. Prepare local-server voice routing

In the server profile or service configuration used by the character:

1. Configure and enable a **Text to Speech** service.
2. For **Audio Output**, enable **Use client capability**. This prevents a
   local server Audio Output service from taking ownership of speech. The Unity
   sample then receives audio URLs, downloads them with its approved token, and
   plays them through Unity.
3. To use voice input, configure and enable a **Speech To Text** service, then
   enable **Use client capability** for **Audio Input**. This lets the server
   request audio from Unity instead of listening through a server-side input
   service.

Those two client-capability settings matter on a local server: a configured
server Audio Output or Audio Input service that is preferred can override the
Unity path. Leave the sample's `VoxtaMicrophone` enabled and ensure Unity can
access a local microphone. It advertises streamed audio input only while that
component has an available device; the server starts and stops capture with its
own recording request. The diagnostics panel shows recognition and VAD events.

### 4. Authorize and talk

Start the server and press **Play**. The sample displays a verification URL and
short code for **Unity Basic Integration**. Open the URL, enter the code, and
approve it. Unity polls for the result, validates the token, saves it in
`PlayerPrefs`, then connects and starts the chat. Later runs validate and reuse
that token automatically.

Type a message and choose **Send** to use text chat. For voice, wait until the
server requests recording, then speak normally. The panel first shows
**Microphone streaming; waiting for speech recognition...**, then changes to
**Listening for your voice...** when recognition begins. It shows partial
recognition when available and sends a nonempty final transcript as the next
chat message. If text works but voice does not begin, check the server's Speech
To Text and Audio Input settings above, then look for microphone and recognition
messages in **Diagnostics**.

### 5. Hear spatial replies and try the game action

With **Local Server Audio Output** disabled, the sample's `VoxtaSpeechPlayer`
uses its attached `AudioSource`. Set that source's **Spatial Blend** slider to
**3D** (a value greater than zero) to enable Unity spatialization. The sample
sets it to 3D; the companion sits at `(2, 0, 3)` and the scene's separate
`AudioListener` is at the origin, so replies pan and attenuate as Unity spatial
audio. Keep exactly one enabled `AudioListener`; remove the sample listener if
your camera already has one.

The sample registers a `wave` action when the chat starts. Ask the character to
wave, for example, “Please wave at me.” When the server invokes the action, the
chat panel and diagnostics show `wave` and any `style` argument. In a game,
replace that sample callback with your animation or gameplay response. If your
server does not infer the action, make sure its action-inference capability is
enabled for the character's profile.

## Sample notes

The sample's [README](Samples~/BasicIntegration/README.md) records its scene
components and local-server behavior. Imported samples under `Assets/Samples`
are copies: re-import the sample after package updates rather than expecting the
copy to update with `Samples~`.

## Protocol model API migration in 0.1.0-pre.2

This prerelease replaces the generated protocol DTO namespace
`Voxta.Unity.Protocol.Generated` with types supplied by the bundled
`Voxta.Model` 1.11.0-beta.1 runtime dependency. Applications that use protocol
messages in callbacks, `UnityEvent` bindings, or calls to `VoxtaClient.Send`
must update their namespace imports and generic type arguments. See
[`Documentation~/runtime-model-public-api-migration.md`](Documentation~/runtime-model-public-api-migration.md)
for the complete public-surface mapping, source migration steps, and the few
payload-shape changes to handle.
