# Voxta protocol generator

This .NET 10 tool is deliberately outside the Unity package. It reflects the
pinned `Voxta.Model` assembly and writes Unity-compatible M1 DTO source to
`unity/Runtime/Protocol/Generated/M1Messages.g.cs`.

Build the pinned model at the commit in `pinned-model.json`, then run:

```powershell
& C:\Users\chris\.dotnet\dotnet.exe run --project Tools/ProtocolGenerator -- --model <path-to-Voxta.Model.dll> --output unity/Runtime/Protocol/Generated/M1Messages.g.cs
```

`--check` compares generated content without writing and exits non-zero on
drift. The generator assembly is never copied to, or referenced by, Unity.
