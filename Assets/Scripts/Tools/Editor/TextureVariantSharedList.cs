using System;
using System.IO;
using SickOfIt.Core.Addressables;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Puts the list of bundles both texture variants share (ServerData/TextureVariantShared.txt, written by
    /// <see cref="AddressablesTextureVariants.Build"/>) into the WebGL player as Assets/Resources/TextureVariantShared.txt,
    /// where TextureVariantRedirect reads it at startup, and removes it after the build (never committed: .gitignore).
    /// Without the list a phone would ask WebGL_ASTC for bundles that live only in WebGL.
    /// </summary>
    sealed class TextureVariantSharedList : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Resources/" + TextureVariantRedirect.SharedListName + ".txt";

        public int callbackOrder => -890;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;
            string source;
            try
            {
                source = AddressablesTextureVariants.SharedListPath;
            }
            catch (Exception e)
            {
                Debug.Log("[TextureVariant] No shared bundle list (no Addressables settings): " + e.Message);
                return;
            }
            if (!File.Exists(source))
            {
                Debug.Log("[TextureVariant] No shared bundle list at " + source + ": phones fetch every bundle from WebGL_ASTC.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.Copy(source, AssetPath, true);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            int count = File.ReadAllLines(AssetPath).Length;
            Debug.Log($"[TextureVariant] The player carries {count} shared bundle names ({AssetPath}).");
        }

        public void OnPostprocessBuild(BuildReport report) => Remove();

        /// <summary>Removes the file (after the build, and on the next editor start if a failed build left it).</summary>
        [InitializeOnLoadMethod]
        static void Remove()
        {
            if (!File.Exists(AssetPath) || BuildPipeline.isBuildingPlayer)
                return;
            AssetDatabase.DeleteAsset(AssetPath);
        }
    }
}
