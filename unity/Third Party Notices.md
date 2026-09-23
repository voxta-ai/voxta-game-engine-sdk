# Third-party notices

The M1 transport bundles the netstandard2.0 assemblies resolved by Voxy's
Unity NuGet installation for Microsoft.AspNetCore.SignalR.Client 8.0.2. They
are distributed under the MIT License.  The source package licenses are copied
next to their assemblies under `Runtime/Plugins/ThirdParty/licenses/`.

The complete, lock-resolved dependency inventory is maintained in
`Documentation~/third-party-dependencies.md`.

The package also carries `Voxta.Model 1.11.0-beta.1` as the pinned protocol
generator input. It is licensed under the Business Source License 1.1; its
license is copied to `Runtime/Plugins/Voxta.Model/LICENSE.md`. The DLL is
disabled in Unity and is not part of the runtime dependency closure.
