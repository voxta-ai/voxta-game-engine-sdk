using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Voxta.CI
{
    public static class UnityPackageValidationBuild
    {
        private const string PackageName = "com.voxta.game-engine-sdk";
        private const string ImportedSamplePath = "Assets/Samples/Voxta Game Engine SDK/Basic Chat Integration";
        private const string SampleScenePath = ImportedSamplePath + "/BasicIntegration.unity";
        private const string PlayModeResultsPath = "../../artifacts/test-results/playmode-results.xml";

        private static TestRunnerApi? _testRunnerApi;
        private static PlayModeTestCallbacks? _playModeTestCallbacks;

        public static void ValidatePackageForTests()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new BuildFailedException($"UPM did not resolve {PackageName} from its Git dependency.");
            }
        }

        public static void RunPlayModeTests()
        {
            ValidatePackageForTests();

            try
            {
                _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                _playModeTestCallbacks = new PlayModeTestCallbacks();
                _testRunnerApi.RegisterCallbacks(_playModeTestCallbacks);
                Debug.Log("Starting package Play Mode tests.");
                _testRunnerApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ImportBasicIntegrationSample()
        {
            ValidatePackageForTests();
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName)!;

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
            BuildDesktopPlayer(
                BuildTarget.StandaloneWindows64,
                ScriptingImplementation.Mono2x,
                "Windows64-Mono",
                "VoxtaBasicIntegration.exe");
        }

        public static void BuildIl2CppLinux64()
        {
            BuildDesktopPlayer(
                BuildTarget.StandaloneLinux64,
                ScriptingImplementation.IL2CPP,
                "Linux64-IL2CPP",
                "VoxtaBasicIntegration.x86_64");
        }

        private static void BuildDesktopPlayer(
            BuildTarget buildTarget,
            ScriptingImplementation scriptingImplementation,
            string outputDirectory,
            string executableName)
        {
            if (!File.Exists(SampleScenePath))
            {
                throw new BuildFailedException($"Import the BasicIntegration sample before building: {SampleScenePath}");
            }

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, scriptingImplementation);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SampleScenePath },
                locationPathName = Path.Combine("Builds", outputDirectory, executableName),
                target = buildTarget,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"{outputDirectory} build failed: {report.summary.result}. See the Unity log for details.");
            }

            Debug.Log($"{outputDirectory} build succeeded at {report.summary.outputPath}.");
        }

        private sealed class PlayModeTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"Running {testsToRun.TestCaseCount} package Play Mode tests.");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    var resultsDirectory = Path.GetDirectoryName(PlayModeResultsPath);
                    if (!string.IsNullOrWhiteSpace(resultsDirectory))
                    {
                        Directory.CreateDirectory(resultsDirectory);
                    }

                    TestRunnerApi.SaveResultToFile(result, PlayModeResultsPath);
                    Debug.Log(
                        $"Package Play Mode tests completed: total={result.TestCaseCount}, " +
                        $"passed={result.PassCount}, failed={result.FailCount}, " +
                        $"skipped={result.SkipCount}, inconclusive={result.InconclusiveCount}.");
                    EditorApplication.Exit(result.FailCount == 0 ? 0 : 1);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorApplication.Exit(1);
                }
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.ResultState != "Passed")
                {
                    Debug.LogError($"Test failed: {result.Test.FullName}\n{result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
