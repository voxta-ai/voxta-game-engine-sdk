# Runtime dependencies

The package vendors the locked `netstandard2.0` and `netstandard2.1` runtime
assemblies in `Runtime/Plugins/ThirdParty` and `Runtime/Plugins/Voxta`.
Local-path and Git UPM installs are self-contained; consuming projects do not
need an `org.nuget` scoped registry or NuGet manifest entries.

## Shipped closure

`Runtime/Plugins/ThirdParty` contains `Microsoft.AspNetCore.SignalR.Client`
10.0.12, `System.Text.Json` 10.0.12, and their required transitive assemblies.
The selected assets are recorded in
`Runtime/Plugins/ThirdParty/RUNTIME-MODEL-DEPENDENCY-INVENTORY.json`.

The package does not copy System.Buffers, System.ComponentModel.Annotations,
System.Memory, System.Numerics.Vectors, or
System.Threading.Tasks.Extensions because Unity's .NET Standard 2.1 target
supplies those references.

## Voxta.Model

`Runtime/Plugins/Voxta/Voxta.Model.dll` is the `netstandard2.1` assembly from
[Voxta.Model 1.11.0-beta.1](https://www.nuget.org/packages/Voxta.Model/1.11.0-beta.1).
Its selected asset is recorded in
`Runtime/Plugins/Voxta/VOXTA-MODEL-INVENTORY.json`. It is enabled for the
Editor and standalone Windows, Linux, and macOS targets.