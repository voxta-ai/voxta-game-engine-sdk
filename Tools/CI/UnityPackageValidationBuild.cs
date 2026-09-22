using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Voxta.CI
{
    public static class UnityPackageValidationBuild
    {
        private const string PackageName = "com.voxta.game-engine-sdk";
        private const string ImportedSamplePath = "Assets/Samples/Voxta Game Engine SDK/Basic Chat Integration";
        private const string SampleScenePath = ImportedSamplePath + "/BasicIntegration.unity";

        public static void ImportBasicIntegrationSample()
        {
            var package = PackageInfo.FindForPackageName(PackageName);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new BuildFailedException($"UPM did not resolve {PackageName} from its Git dependency.");
            }

            var source = Path.Combine(package.resolvedPath, "Samples~", "BasicIntegration");
            if (!Directory.Exists(source))
            {
                throw new BuildFailedException($"The resolved package has no BasicIntegration sample: {source}");
            }

            if (Directory.Exists(ImportedSamplePath))
            {
                throw new BuildFailedException($"The sample import destination already exists: {ImportedSamplePath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ImportedSamplePath) ?? throw new InvalidOperationException());
            FileUtil.CopyFileOrDirectory(source, ImportedSamplePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!File.Exists(SampleScenePath))
            {
                throw new BuildFailedException($"The imported sample scene is missing: {SampleScenePath}");
            }
        }

        public static void BuildMonoWindows64()
        {
            BuildWindowsPlayer(ScriptingImplementation.Mono, "Windows64-Mono");
        }

        public static void BuildIl2CppWindows64()
        {
            BuildWindowsPlayer(ScriptingImplementation.IL2CPP, "Windows64-IL2CPP");
        }

        private static void BuildWindowsPlayer(ScriptingImplementation scriptingImplementation, string outputDirectory)
        {
            if (!File.Exists(SampleScenePath))
            {
                throw new BuildFailedException($"Import the BasicIntegration sample before building: {SampleScenePath}");
            }

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, scriptingImplementation);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SampleScenePath },
                locationPathName = Path.Combine("Builds", outputDirectory, "VoxtaBasicIntegration.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"{outputDirectory} build failed: {report.summary.result}. See the Unity log for details.");
            }

            Debug.Log($"{outputDirectory} build succeeded at {report.summary.outputPath}.");
        }
    }
}
