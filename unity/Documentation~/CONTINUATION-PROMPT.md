# Continuation Prompt: Implement M3 microphone input

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. M1, M2,
and the M3 speech-playback unit are complete; M1 generator-drift CI remains
intentionally unchecked. Preserve all existing changes.

Implement only the remaining M3 microphone work. Before editing, read the
development plan, `protocol-v0.md`, current session/transport code, and the
pinned server's audio-input WebSocket endpoint, startup-frame DTO, audio-input
capabilities, speech-recognition messages, and VAD/audio-frame messages.

Add `VoxtaMicrophone` for Unity 2022.3/.NET Standard 2.1: microphone capture,
PCM16 conversion, audio-input WebSocket startup frame, audio and silence-marker
streaming to `/ws/audio/input/stream`, and main-thread public callbacks/events
for recognition, VAD, and audio frames. Advertise `audioInput: WebSocketStream`
only while the path is active and configured. Integrate it with
`VoxtaCompanion` and the BasicIntegration sample without regressing the verified
Unity speech playback path.

Expand the generated protocol subset only for verified contracts. Add focused
automated tests where practical, then perform a live local-server Play-mode
microphone run. Update `protocol-v0.md` with verified discoveries and check M3
microphone items only after that evidence. Do not begin device auth, actions,
M4, package .NET 10 server assemblies, or add CI. Use `apply_patch` for edits.
