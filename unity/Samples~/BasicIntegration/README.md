# Basic Chat Integration

Follow the package [quickstart](../../README.md) to install and import this
sample, configure its companion, prepare a local server, approve device access,
and test text, voice, spatial playback, and the `wave` action.

Open `BasicIntegration.unity`, then select **Voxta Companion**. Enter the
required character ID and optional scenario ID, and change the default Server
URL (`http://127.0.0.1:5384`) only when your server uses a different HTTP or
HTTPS address. Leave API Key empty: Play Mode displays a device-approval URL and
code, then stores the approved token in `PlayerPrefs` for later runs.

The companion is disabled until that approval completes. It includes:

- `VoxtaSpeechPlayer` and a 3D `AudioSource` at `(2, 0, 3)`, with a separate
  `AudioListener` at the origin. Leave **Local Server Audio Output** disabled
  and configure the local server's **Audio Output** to **Use client capability**
  so replies download and play through Unity instead of on the server machine.
- An enabled `VoxtaMicrophone`. With an available Unity microphone it advertises
  streamed client audio. Configure an enabled server **Speech To Text** service
  and set **Audio Input** to **Use client capability**; the server sends the
  recording request and the sample displays recognition and VAD diagnostics.
- `VoxtaActions`, which registers `wave`. Ask the character to wave to see the
  callback and optional `style` argument in the chat panel and diagnostics.

A Unity scene must have exactly one enabled `AudioListener`; remove this
sample's listener if a camera provides one. The imported sample includes the
required IL2CPP linker configuration. For a different scene using
`VoxtaCompanion`, copy this folder's `link.xml` into the consuming project's
`Assets` directory because Unity does not process linker files inside UPM
packages.

Samples already imported into `Assets/Samples/` are copies. Re-import this
sample after updating the package.
