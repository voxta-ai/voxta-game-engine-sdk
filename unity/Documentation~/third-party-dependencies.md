# M1 managed dependency closure

The package vendors the Voxy-proven, Unity NuGet lock-resolved `netstandard2.0`
assemblies in `Runtime/Plugins/ThirdParty`. This makes local-path and git UPM
installs self-contained: consuming projects do not need the `org.nuget` scoped
registry or any NuGet manifest entries.

Voxy declares `Microsoft.AspNetCore.SignalR.Client` 8.0.2 and
`System.Text.Json` 8.0.0. Its lock file resolves `System.Text.Json` to 8.0.2,
which is required by the SignalR 8.0.2 graph. The package therefore ships that
resolved 8.0.2 assembly rather than an incompatible lower direct declaration.

The copied assembly inventory and upstream license for each package live beside
the binaries. `THIRD-PARTY-INVENTORY.txt` records package name, resolved version,
and source package-cache path used to create this closure.

## Pinned protocol-model input

`Runtime/Plugins/Voxta.Model/Voxta.Model.dll` is the `netstandard2.1` assembly
from [Voxta.Model 1.11.0-beta.1](https://www.nuget.org/packages/Voxta.Model/1.11.0-beta.1).
It is an input to `Tools/ProtocolGenerator`, not a Unity runtime assembly. Its
plugin metadata disables it on every Unity platform because it depends on
`System.Text.Json` 10.0.12, while the runtime transport's tested dependency
closure uses 8.0.2. `Tools/ProtocolGenerator/pinned-model.json` records its
repository commit and SHA-512 hash; the generator verifies the hash before
writing or checking generated protocol code.
