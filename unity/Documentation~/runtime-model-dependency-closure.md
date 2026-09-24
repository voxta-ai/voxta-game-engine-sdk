# Shipped runtime dependency closure

The Unity package vendors a complete managed dependency closure in
`Runtime/Plugins/ThirdParty`. Consumer projects do not restore NuGet packages:
Unity loads the selected DLLs from the package.

## Current closure

The package ships 24 assemblies:

- `Voxta.Model` **1.11.0-beta.1**, selected from
  `lib/netstandard2.1/Voxta.Model.dll`.
- SignalR, ASP.NET Core connection, Microsoft.Extensions, JSON, pipeline,
  channels, diagnostics, encoding, and time-provider packages at **10.0.12**.
- `System.Runtime.CompilerServices.Unsafe` **6.1.2**.

Each DLL is enabled for the Unity Editor and standalone Windows, Linux, and
macOS. The full package versions, NuGet source URLs, selected assets, package
content hashes, assembly hashes, and license information are the source of
truth in:

- `Runtime/Plugins/ThirdParty/RUNTIME-MODEL-DEPENDENCY-INVENTORY.json`
- `Runtime/Plugins/ThirdParty/THIRD-PARTY-INVENTORY.txt`
- `Runtime/Plugins/ThirdParty/licenses/`
- `Documentation~/RuntimeModelDependencyProbe/packages.lock.json`

`Tools/ModelDependencies/VoxtaModel/pinned-model.json` records the intentionally
selected `Voxta.Model` package, repository commit, and shipped assembly hash.

## Intentional exclusions

The lock graph includes `System.Buffers` 4.6.1,
`System.ComponentModel.Annotations` 5.0.0, `System.Memory` 4.6.3, and
`System.Threading.Tasks.Extensions` 4.6.3. For this `netstandard2.1` Unity
closure, they provide platform placeholders or reference assets. Unity supplies
the corresponding APIs, so those DLLs must not be copied into the package.

`System.Numerics.Vectors` is not present in the target graph and is not shipped.

## Verification

Run this from the repository root after changing the vendored closure:

```powershell
pwsh ./Tools/ModelDependencies/VoxtaModel/Validate-VoxtaModelDependencyClosure.ps1
```

The audit verifies that the shipped DLL names, assembly identities, versions,
hashes, lock entries, and license notices match the inventory. Add
`-VerifyPackageAssets` when the locked NuGet artifacts are available in the
local package cache; it also verifies each package content hash and selected
asset hash.

## Updating the closure

A newer `Voxta.Model` release is first exercised by the scheduled
`Voxta.Model update compatibility tests` workflow. The canary replaces only
`Voxta.Model.dll` in an isolated package and runs Unity tests and desktop
builds. It never changes the pinned production package.

A passing canary makes a version a candidate for review; it does not promote
it. To update the shipped closure, intentionally select the new package and
refresh the complete dependency set, inventory, lock data, plugin metadata,
and license notices together. Then run the audit above and the Unity package
validation workflow. This prevents mixed assembly versions and duplicate type
identities in consumer projects.
