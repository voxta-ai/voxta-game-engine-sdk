# Continuation Prompt: Implement M4 action registration and context publication

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. M1–M3
are complete. M4 device authorization is complete: `VoxtaAuth` and
`PlayerPrefsTokenStore` were added, `VoxtaAuthTests` passed 5/5 in Play Mode,
and the full browser-approved device flow was verified against the local server.
M1 generator-drift CI remains intentionally unchecked. Preserve all changes.

Implement only the next M4 unit: `VoxtaActions` registration and
`ClientUpdateContextMessage` publication. Before editing, read the development
plan, `protocol-v0.md`, current Unity client/session/companion code, and the
pinned server’s action REST/WebSocket contracts plus canonical client behavior.

Keep Unity 2022.3/.NET Standard 2.1 compatibility, generate protocol types
only for verified contracts, dispatch public Unity callbacks on the main
thread, add focused tests, and verify against the local server. Record verified
discoveries in `protocol-v0.md` and check only the completed M4 action/context
item after evidence. Do not implement action dispatch, app triggers, companion
prefab work, M5, server assemblies, or CI. Use `apply_patch` for edits.
