# Runtime dependencies

The package vendors the locked `netstandard2.0` and `netstandard2.1` runtime
assemblies in `Runtime/Plugins/ThirdParty` and `Runtime/Plugins/Voxta`.
Local-path and Git UPM installs are self-contained; consuming projects do not
need an `org.nuget` scoped registry or NuGet manifest entries.

## Shipped closure

`Runtime/Plugins/ThirdParty` contains `Microsoft.AspNetCore.SignalR.Client`
8.0.32, `System.Text.Json` 8.0.6, and their required transitive assemblies.
These are MIT-licensed Microsoft components; see
`Runtime/Plugins/ThirdParty/Third-Party Notices.txt`. The selected assets are
recorded in the SDK repository under `Tools/ModelDependencies/VoxtaModel/Inventory`,
outside the package, so the records are not imported into your project.

The closure stays on the 8.x line because Unity 6.5 and later compile scripts
against their own `System.Text.Json` 8.0.0.0 and override any newer copy a
package ships. An assembly built against a higher version then fails with
CS1705. Unity 6.5+ also supplies `Microsoft.Bcl.AsyncInterfaces`,
`Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Logging.Abstractions`,
`System.Diagnostics.DiagnosticSource`,
`System.Runtime.CompilerServices.Unsafe`, `System.Text.Encodings.Web`, and
`System.Text.Json` at the versions SignalR 8 needs. Their vendored copies carry
the `!UNITY_6000_5_OR_NEWER` define constraint, so they load only in older
editors.

Run `Tools/ModelDependencies/VoxtaModel/Update-VoxtaModelDependencyClosure.ps1`
after changing the probe's package versions, then
`Validate-VoxtaModelDependencyClosure.ps1`.

The package does not copy System.Buffers, System.ComponentModel.Annotations,
System.Memory, System.Numerics.Vectors, or
System.Threading.Tasks.Extensions because Unity's .NET Standard 2.1 target
supplies those references.

## Voxta.Model

`Runtime/Plugins/Voxta/Voxta.Model.dll` is a first-party Voxta assembly, not a
third-party component. It is the `netstandard2.1` asset of a Voxta.Model
release whose `netstandard2.1` build references `System.Text.Json` 8, and each
update to it is chosen and tested by hand. As included in this package, it is
licensed under the Unity Asset Store End User License Agreement, like the rest
of the package's own content; licenses published with the Voxta.Model NuGet
package do not apply to this copy.

It is enabled for the Editor and standalone Windows, Linux, and macOS targets.