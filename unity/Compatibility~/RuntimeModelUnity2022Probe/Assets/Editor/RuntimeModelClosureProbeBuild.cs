using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Voxta.CompatibilityProbe.Editor
{
    public static class RuntimeModelClosureProbeBuild
    {
        private const string ScenePath = "Assets/RuntimeModelClosureProbe.unity";

        public static void BuildWindowsMono()
        {
            BuildWindows("Mono", ScriptingImplementation.Mono2x, "Builds/WindowsMono/RuntimeModelClosureProbe.exe");
        }

        public static void BuildWindowsIl2Cpp()
        {
            BuildWindows("IL2CPP", ScriptingImplementation.IL2CPP, "Builds/WindowsIL2CPP/RuntimeModelClosureProbe.exe");
        }

        private static void BuildWindows(string backendName, ScriptingImplementation backend, string locationPathName)
        {
            EnsureProbeScene();
            var previousBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            try
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, backend);
                EditorUserBuildSettings.development = true;
                EditorUserBuildSettings.allowDebugging = true;

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = locationPathName,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"{backendName} probe build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

                Debug.Log($"{backendName} probe build passed: {Path.GetFullPath(locationPathName)}");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, previousBackend);
            }
        }

        [MenuItem("Voxta/Compatibility Probe/Configure Probe Scene")]
        public static void EnsureProbeScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var gameObject = new GameObject("Runtime Model Closure Probe");
                gameObject.AddComponent<RuntimeModelClosureProbe>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Runtime model closure probe scene configured: {ScenePath}");
        }
    }
}
