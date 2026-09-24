# Voxta protocol generator

This .NET 10 tool is deliberately outside the Unity package. It reflects the
vendored `Voxta.Model` assembly described by `pinned-model.json` for contract
inspection. The Unity runtime now consumes `Voxta.Model.dll` directly and no
longer compiles generated DTO source. The assembly is checked into
`unity/Runtime/Plugins/ThirdParty` so CI does not check out or build the server
repository.

To generate a disposable comparison artifact, run:

```powershell
& C:\Users\chris\.dotnet\dotnet.exe run --project Tools/ProtocolGenerator -- --model-info Tools/ProtocolGenerator/pinned-model.json --output $env:TEMP\Voxta.Model.contract-reference.cs
```

`--check` compares generated content without writing when an explicit reference
artifact is supplied; it is no longer a Unity runtime drift check. The
pinned-model metadata verifies the vendored assembly's SHA-512 hash. The model
DLL is enabled for Unity runtime platforms with the locked SignalR 10 and
System.Text.Json 10.0.12 dependency closure.
