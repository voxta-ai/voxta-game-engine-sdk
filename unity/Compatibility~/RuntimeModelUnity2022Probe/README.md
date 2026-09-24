# Runtime model Unity 2022.3 probe

This standalone Unity project contains the candidate SignalR 10 / JSON 10
closure restored by `Documentation~/RuntimeModelDependencyProbe`. It does not
reference or modify `../../Runtime/Plugins`.

Open it with Unity 2022.3.62f3, or run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -nographics -quit -projectPath '<repository>/unity/Compatibility~/RuntimeModelUnity2022Probe' -logFile -
```

`RuntimeModelClosureProbe` constructs a `ClientAuthenticateMessage`, serializes
and deserializes it with `VoxtaJsonSerializer.CreateSerializeOptions()`, and
logs the result. `CandidatePluginImportSettings` restricts every candidate DLL
to the Editor and standalone Windows, Linux, and macOS.

Use **Voxta > Compatibility Probe > Configure Probe Scene** to configure
`Assets/RuntimeModelClosureProbe.unity`, then invoke the static build methods
`Voxta.CompatibilityProbe.Editor.RuntimeModelClosureProbeBuild.BuildWindowsMono`
or `BuildWindowsIl2Cpp` from batch Unity. The development players log the
round-trip result and quit automatically. Before promotion, run the equivalent
player validation for the remaining supported platforms.
