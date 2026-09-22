# Continuation Prompt: Establish Unity package CI

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. Preserve all
existing uncommitted work.

M1-M4 and the completed M5 package/sample units are in place. BasicIntegration
uses device-flow authorization, persisted tokens, authenticated reply-audio
downloads, scrollable diagnostics, microphone voice input, spatial reply playback,
and a visible `wave` action callback. Its imported copy in
`G:\Unity\voxtaSDK-sandbox\Assets\Samples\...` is separate from `Samples~`.
The intended public Git URL installation has already been manually verified as
working, including running the sample and passing tests. Do not repeat or document
that validation in this task.

Implement only the next M5 unit: establish CI for generation drift, Unity tests,
package validation, and supported desktop player builds. First inspect the existing
repository layout, `Tools/ProtocolGenerator`, Unity project/test invocation options,
and any current CI conventions. Add the smallest maintainable workflow and supporting
scripts/configuration needed to run on pull requests and the default branch. It must
fail on generated-protocol drift, run the package's automated tests, validate a clean
UPM installation from the intended Git URL, import the Basic Chat Integration sample,
and build supported desktop Mono and IL2CPP players. Keep credentials and local-server
dependencies out of CI; live-server and device-approval tests must remain excluded or
explicitly skipped. Prefer pinned Unity and action versions, useful test/build
artifacts, and clear failure output.

Do not change runtime code, protocol or auth contracts, the prefab, sample scene,
quickstart documentation, device-flow behavior, server configuration, compatibility
policy, publication/release notes, or M6 work. Update only CI-related files and the
development plan. Mark only the M5 CI item once the workflow is syntactically valid
and its feasible local or dry-run checks pass; add a concise completion-record entry
that states what runs in CI and any unavoidable hosted-runner limitation.
