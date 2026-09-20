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
