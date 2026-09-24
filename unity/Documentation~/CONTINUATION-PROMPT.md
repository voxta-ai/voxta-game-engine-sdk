# Continuation Prompt: Complete runtime-model test hardening

Continue in `G:\voxta-projects\GitHub\voxta-game-engine-sdk\unity`. Preserve
all existing uncommitted work. Do not delete
`C:\Users\chris\AppData\Local\Temp\voxta-runtime-model-promotion-backup-20260923`.

The runtime, tests, and BasicIntegration source use `Voxta.Model` namespaces
and `VoxtaJsonSerializer.CreateSerializeOptions()`. `VoxtaSignalRTransport`
keeps raw `JsonElement` SignalR registration, ignores unknown `$type` frames,
and still reports malformed `action` frames. `M1Messages.g.cs` has been removed
from the runtime; do not restore generated DTO source.

Validation already passed: clean package validation found 69 PlayMode tests
(65 passed, 4 explicit live-server skips); sandbox validation found 66 passed
and 4 skipped. A live BasicIntegration run verified device authorization and
saved-token reuse, chat/text replies, `wave`, authenticated spatial reply
audio, and microphone recording. The package sample now includes runtime and
test asmdefs with explicit `Voxta.Model.dll` references; its test asmdef also
directly references `Voxta.Unity.Runtime` because assembly references are not
transitive for public return types.

Implement only Section 5 of
`Documentation~/RUNTIME-MODEL-DEPENDENCY-UPGRADE-PLAN.md`:

1. Audit the migrated runtime and sample tests, then check the first two
   Section 5 items only when model-based coverage demonstrably includes
   authentication, chat lifecycle, actions, device authorization, speech, and
   microphone frames.
2. Add focused wire-contract tests for SDK-emitted frames under
   `System.Text.Json` 10, including polymorphic `$type` output and string enum
   values.
3. Add a regression check that rejects accidental SignalR 8 or JSON 8 assets
   in the vendored closure.
4. Keep live-server and device-approval tests explicit and skipped by default;
   record the already completed live run only where it supports the checklist.
5. Run the relevant clean Unity validation and update only the Section 5
   checklist items backed by the results.

Do not change protocol behavior, update CI, tag a rollback point, or start
Section 6 work. Use
`Documentation~/runtime-model-type-migration-map.md` for model differences,
especially string enum serialization, `object?[]` app-trigger arguments, and
nested context-update entry types.
