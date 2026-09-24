# Runtime `Voxta.Model` dependency closure

This is the shipped closure for the runtime `Voxta.Model` migration. The
SignalR 10 / JSON 10 assemblies and `Voxta.Model.dll` in
`Runtime/Plugins/ThirdParty` were promoted atomically after the Unity
compatibility probe passed on 2026-09-23.

## Selection

The candidate is restored for `netstandard2.1` with the .NET 10.0.401 SDK and a
locked `packages.lock.json`. `Microsoft.AspNetCore.SignalR.Client` 10.0.12 was
selected because it is the SignalR patch in the same servicing family as
`System.Text.Json` 10.0.12 required by `Voxta.Model` 1.11.0-beta.1.

The restore inputs are stored in
`Documentation~/RuntimeModelDependencyProbe/`. Each package is downloaded from
`https://api.nuget.org/v3-flatcontainer/<lowercase-package-id>/<version>/<lowercase-package-id>.<version>.nupkg`.
The lock file contains NuGet's SHA-512 content hash for every package; the
inventory contains the selected assembly SHA-512 hashes and the license data.

## Selected runtime assemblies

Every listed package uses the shown `lib` asset. All Microsoft and System
packages use the MIT license. `Voxta.Model` uses the included Business Source
License 1.1 (`LICENSE.md`).

| Package | Version | Selected asset |
| --- | --- | --- |
| Microsoft.AspNetCore.Connections.Abstractions | 10.0.12 | lib/netstandard2.1/Microsoft.AspNetCore.Connections.Abstractions.dll |
| Microsoft.AspNetCore.Http.Connections.Client | 10.0.12 | lib/netstandard2.1/Microsoft.AspNetCore.Http.Connections.Client.dll |
| Microsoft.AspNetCore.Http.Connections.Common | 10.0.12 | lib/netstandard2.0/Microsoft.AspNetCore.Http.Connections.Common.dll |
| Microsoft.AspNetCore.SignalR.Client | 10.0.12 | lib/netstandard2.0/Microsoft.AspNetCore.SignalR.Client.dll |
| Microsoft.AspNetCore.SignalR.Client.Core | 10.0.12 | lib/netstandard2.1/Microsoft.AspNetCore.SignalR.Client.Core.dll |
| Microsoft.AspNetCore.SignalR.Common | 10.0.12 | lib/netstandard2.0/Microsoft.AspNetCore.SignalR.Common.dll |
| Microsoft.AspNetCore.SignalR.Protocols.Json | 10.0.12 | lib/netstandard2.0/Microsoft.AspNetCore.SignalR.Protocols.Json.dll |
| Microsoft.Bcl.AsyncInterfaces | 10.0.12 | lib/netstandard2.1/Microsoft.Bcl.AsyncInterfaces.dll |
| Microsoft.Bcl.TimeProvider | 10.0.12 | lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | lib/netstandard2.1/Microsoft.Extensions.DependencyInjection.dll |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | lib/netstandard2.1/Microsoft.Extensions.DependencyInjection.Abstractions.dll |
| Microsoft.Extensions.Features | 10.0.12 | lib/netstandard2.0/Microsoft.Extensions.Features.dll |
| Microsoft.Extensions.Logging | 10.0.12 | lib/netstandard2.1/Microsoft.Extensions.Logging.dll |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | lib/netstandard2.0/Microsoft.Extensions.Logging.Abstractions.dll |
| Microsoft.Extensions.Options | 10.0.12 | lib/netstandard2.1/Microsoft.Extensions.Options.dll |
| Microsoft.Extensions.Primitives | 10.0.12 | lib/netstandard2.0/Microsoft.Extensions.Primitives.dll |
| System.Diagnostics.DiagnosticSource | 10.0.12 | lib/netstandard2.0/System.Diagnostics.DiagnosticSource.dll |
| System.IO.Pipelines | 10.0.12 | lib/netstandard2.0/System.IO.Pipelines.dll |
| System.Net.ServerSentEvents | 10.0.12 | lib/netstandard2.0/System.Net.ServerSentEvents.dll |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll |
| System.Text.Encodings.Web | 10.0.12 | lib/netstandard2.0/System.Text.Encodings.Web.dll |
| System.Text.Json | 10.0.12 | lib/netstandard2.0/System.Text.Json.dll |
| System.Threading.Channels | 10.0.12 | lib/netstandard2.1/System.Threading.Channels.dll |
| Voxta.Model | 1.11.0-beta.1 | lib/netstandard2.1/Voxta.Model.dll |

`System.Buffers` 4.6.1, `System.ComponentModel.Annotations` 5.0.0,
`System.Memory` 4.6.3, and `System.Threading.Tasks.Extensions` 4.6.3 are
present in the lock graph but supply only `netstandard2.1` platform placeholders
or reference assets. They must not be copied into the candidate Unity plug-in
folder. `System.Numerics.Vectors` is absent from the target graph.

## Comparison with the shipped SignalR 8 closure

All existing SignalR, ASP.NET Core, `Microsoft.Extensions`, BCL, diagnostic,
pipeline, encoding, JSON, and channels assemblies are replaced by the versions
above. `Microsoft.Bcl.TimeProvider` moves from 8.0.1 to 10.0.12 and
`System.Runtime.CompilerServices.Unsafe` moves from 6.0.0 to 6.1.2.
`System.Net.ServerSentEvents` 10.0.12 and `Voxta.Model` are additions.
The shipped `System.Buffers`, `System.ComponentModel.Annotations`,
`System.Memory`, `System.Numerics.Vectors`, and
`System.Threading.Tasks.Extensions` DLLs are removals from the candidate
plug-in folder because the target framework supplies those references.

The existing DLL inventory uses package versions, which can differ from assembly
versions; promotion must compare package version, selected asset, package hash,
and assembly hash from the candidate inventory rather than assembly version
alone.

## Unity probe record

On 2026-09-23, Unity 2022.3.62f3 opened the isolated probe in batch mode and
ran `Voxta.CompatibilityProbe.Editor.CandidatePluginImportSettings.ConfigureAndVerify`.
The `ClientAuthenticateMessage` JSON 10 round trip passed. The generated plugin
metadata explicitly enables all 24 candidate assemblies for the Editor and
standalone Windows, Linux, and macOS, and disables `Any platform`.

On 2026-09-23, the configured probe scene built and ran as Windows x64
development players under both Mono and IL2CPP. Both players completed the
`ClientAuthenticateMessage` JSON round trip. The IL2CPP build ran UnityLinker
with the project's normal stripping settings and converted `Voxta.Model`,
`System.Text.Json`, `System.Text.Encodings.Web`, `System.IO.Pipelines`, and
`System.Runtime.CompilerServices.Unsafe`. Neither the build nor player logs
reported a candidate-assembly identity/load failure, linker failure, missing
method, reflection failure, or serialization warning. No preservation rule was
required for this exercised path.

The headless player logs include expected Null-graphics shader errors and a
development-debugger port warning; these are unrelated to the candidate
closure. Linux and macOS player validation remains for the later package/CI
stage. On 2026-09-23, the verified 24-assembly closure was promoted into
`Runtime/Plugins/ThirdParty`, including runtime-enabled `Voxta.Model.dll` and
the locked inventory/hash validator. The shipped third-party notices now
describe the SignalR 10 / System.Text.Json 10.0.12 closure.
