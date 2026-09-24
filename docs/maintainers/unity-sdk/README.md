# Unity SDK maintainer documentation

These documents are for contributors who change the SDK itself. They are not
shipped with the Unity package.

- [Design](design.md)
- [Development plan](unity-sdk-development-plan.md)
- [Runtime model upgrade plan](runtime-model-dependency-upgrade-plan.md)
- [Runtime dependency closure](runtime-model-dependency-closure.md)
- [Runtime model type migration map](runtime-model-type-migration-map.md)
- [Protocol contract](protocol-v0.md)

The locked dependency probe data lives in
`Tools/ModelDependencies/VoxtaModel/Probe/`. Run the closure audit from the
repository root before accepting a vendored dependency update:

```powershell
pwsh ./Tools/ModelDependencies/VoxtaModel/Validate-VoxtaModelDependencyClosure.ps1
```