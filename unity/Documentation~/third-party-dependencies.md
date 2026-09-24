# M1 managed dependency closure

The package vendors the locked `netstandard2.0` and `netstandard2.1` runtime
assemblies in `Runtime/Plugins/ThirdParty`. This makes local-path and git UPM
installs self-contained: consuming projects do not need the `org.nuget` scoped
registry or any NuGet manifest entries.

`Documentation~/RuntimeModelDependencyProbe/packages.lock.json` pins
`Microsoft.AspNetCore.SignalR.Client` 10.0.12, `System.Text.Json` 10.0.12,
and `Voxta.Model` 1.11.0-beta.1. The package ships the 24 selected assets
recorded in `Runtime/Plugins/ThirdParty/RUNTIME-MODEL-DEPENDENCY-INVENTORY.json`.
The closure intentionally omits System.Buffers, System.ComponentModel.Annotations,
System.Memory, System.Numerics.Vectors, and System.Threading.Tasks.Extensions,
because the Unity .NET Standard 2.1 target supplies those references.

The copied assembly inventory, selected assets, NuGet SHA-512 package hashes,
and assembly SHA-512 hashes live beside the binaries. Run
`pwsh ./Tools/ValidateRuntimeModelDependencyClosure.ps1` from the repository
root to validate the vendored closure. Add `-VerifyPackageAssets` when the
locked NuGet artifacts are available in the package cache to verify those
artifacts and their selected assets too.

## Pinned protocol model

`Voxta.Model.dll` is the `netstandard2.1` assembly from
[Voxta.Model 1.11.0-beta.1](https://www.nuget.org/packages/Voxta.Model/1.11.0-beta.1).
It is enabled for the Editor and standalone Windows, Linux, and macOS targets
with the rest of the runtime closure. `Tools/ProtocolGenerator/pinned-model.json`
continues to record the generator input's repository commit and SHA-512 hash.
