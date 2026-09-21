# Continuation Prompt: Implement M4 device authorization

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. M1, M2,
and M3 are complete: Unity speech playback and microphone streaming were
verified in Play Mode, and all 37 Play Mode tests pass. M1 generator-drift CI
remains intentionally unchecked. Preserve all existing changes.

Implement only the first M4 unit: `VoxtaAuth` device-code request, polling,
cancellation, token validation, and a token-store abstraction with a
`PlayerPrefs` implementation. Before editing, read the development plan,
`protocol-v0.md`, current client/session code, and the pinned server's device
authorization REST contracts and canonical client behavior.

Keep the Unity runtime on 2022.3/.NET Standard 2.1, use generated protocol
types only for verified contracts, keep public Unity callbacks on the main
thread, add focused tests, and verify the flow against the local server. Record
verified discoveries in `protocol-v0.md` and check only the completed M4
device-auth items after evidence. Do not begin actions, companion prefab work,
M5, package .NET 10 server assemblies, or CI. Use `apply_patch` for edits.
