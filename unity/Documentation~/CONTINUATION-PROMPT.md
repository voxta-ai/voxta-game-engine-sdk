# Continuation Prompt: Expand BasicIntegration voice, spatial audio, and action sample

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. Preserve all
existing uncommitted work.

M1-M4 and the first M5 unit are complete. BasicIntegration now uses device-flow
authorization, persists the approved token, authenticates reply-audio downloads,
and has a scrollable IMGUI diagnostics panel. Its imported copy in
`G:\Unity\voxtaSDK-sandbox\Assets\Samples\...` is separate from `Samples~`; keep
the sandbox copy synchronized when validating the sample.

Implement only the next M5 unit: expand BasicIntegration to clearly demonstrate
the existing microphone voice path, spatial `AudioSource` playback, and one
registered `VoxtaActions` game action with visible invocation feedback. Inspect
the existing companion, microphone, speech-player, actions APIs, scene, sample
README, and Play Mode test conventions first. Retain text chat, device
authorization, diagnostics, and explicit live-server tests.

Do not change protocol messages, auth contracts, the reusable prefab, quickstart
documentation, CI, build-target work, or later M5 items. Add focused Play Mode
coverage where it does not require a live server, validate in the existing
sandbox, update only directly affected sample documentation, and mark only this
M5 plan item after its stated verification passes.
