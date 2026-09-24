using UnityEditor;
using UnityEditor.Callbacks;
using System;

namespace Voxta.CompatibilityProbe.Editor
{
    [InitializeOnLoad]
    public static class CandidatePluginImportSettings
    {
        static CandidatePluginImportSettings()
        {
            EditorApplication.delayCall += ConfigureAndVerify;
        }

        [MenuItem("Voxta/Compatibility Probe/Configure Candidate Plug-ins")]
        private static void Configure()
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets/Plugins/CandidateClosure" }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
                if (importer == null)
                    continue;

                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(true);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, true);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
                importer.SaveAndReimport();
            }
        }

        [MenuItem("Voxta/Compatibility Probe/Verify Model Serialization")]
        private static void VerifySerialization()
        {
            RuntimeModelClosureProbe.VerifySerialization();
        }

        public static void ConfigureAndVerify()
        {
            Configure();
            AssetDatabase.SaveAssets();
            VerifySerialization();
        }
    }
}
