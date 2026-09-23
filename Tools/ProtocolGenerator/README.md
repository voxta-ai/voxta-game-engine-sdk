# Voxta protocol generator

This .NET 10 tool is deliberately outside the Unity package. It reflects the
vendored `Voxta.Model` assembly described by `pinned-model.json` and writes
Unity-compatible DTO source to `unity/Runtime/Protocol/Generated/M1Messages.g.cs`.
The assembly is checked into `unity/Runtime/Plugins/Voxta.Model` so CI does not
check out or build the server repository.

To regenerate or check the output, run:

```powershell
& C:\Users\chris\.dotnet\dotnet.exe run --project Tools/ProtocolGenerator -- --model-info Tools/ProtocolGenerator/pinned-model.json --output unity/Runtime/Protocol/Generated/M1Messages.g.cs
```

`--check` compares generated content without writing and exits non-zero on
drift. The pinned-model metadata verifies the vendored assembly's SHA-512 hash.
The model DLL is disabled as a Unity plugin: it needs System.Text.Json 10 while
the runtime transport ships the Unity-proven 8.0.2 dependency closure.
