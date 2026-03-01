#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Master builder: generates all level/video/death scenes from GameDefinition.
    /// This is the "compile" step — config → scenes → addressables.
    /// 
    /// Usage:
    ///   Tools → Claw → Build All Scenes From Config
    ///   OR: mcporter call unity execute_menu_item menuPath="Tools/Claw/Build All Scenes From Config"
    /// </summary>
    public static class GameBuilder
    {
        private const string SCENES_ROOT = "Assets/Scenes";
        private const string LEVELS_FOLDER = "Assets/Scenes/Levels";
        private const string VIDEOS_FOLDER = "Assets/Scenes/Videos";
        private const string DEATHS_FOLDER = "Assets/Scenes/Deaths";

        [MenuItem("Tools/Claw/Build All Scenes From Config")]
        public static void BuildAllScenes()
        {
            Debug.LogWarning("[DEPRECATED] This old GameBuilder creates individual scenes. Use 'Tools/Claw/Build Unified Project' instead!");
            Debug.LogWarning("The new unified approach uses 3 template scenes + config-driven content.");
            
            // Redirect to new project generator
            if (EditorUtility.DisplayDialog("Deprecated Builder", 
                "This old GameBuilder is deprecated.\n\nUse the new Project Generator instead?\n\nIt uses 3 simple scenes + config-driven approach.", 
                "Use Project Generator", "Continue with Old"))
            {
                ProjectGenerator.GenerateProject();
                return;
            }
            
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int levelsBuilt = 0, videosBuilt = 0, deathsBuilt = 0;

            // Build levels
            foreach (var level in gd.levels)
            {
                if (LevelSceneGenerator.GenerateLevelScene(level, gd))
                    levelsBuilt++;
            }

            // Build videos
            foreach (var video in gd.videos)
            {
                if (VideoSceneGenerator.GenerateVideoScene(video, gd))
                    videosBuilt++;
            }

            // Build deaths
            foreach (var death in gd.deaths)
            {
                if (DeathSceneGenerator.GenerateDeathScene(death, gd))
                    deathsBuilt++;
            }

            // Update addressable groups
            AddressableGroupGenerator.UpdateAddressableGroups(gd);

            Debug.Log($"[GameBuilder] Build complete: {levelsBuilt} levels, {videosBuilt} videos, {deathsBuilt} deaths");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Claw/Build Level Scenes Only")]
        public static void BuildLevelScenesOnly()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int built = 0;
            foreach (var level in gd.levels)
            {
                if (LevelSceneGenerator.GenerateLevelScene(level, gd))
                    built++;
            }

            AddressableGroupGenerator.UpdateAddressableGroups(gd);
            Debug.Log($"[GameBuilder] Built {built} level scenes");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Claw/Build Video Scenes Only")]
        public static void BuildVideoScenesOnly()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int built = 0;
            foreach (var video in gd.videos)
            {
                if (VideoSceneGenerator.GenerateVideoScene(video, gd))
                    built++;
            }

            AddressableGroupGenerator.UpdateAddressableGroups(gd);
            Debug.Log($"[GameBuilder] Built {built} video scenes");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Claw/Build Death Scenes Only")]
        public static void BuildDeathScenesOnly()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int built = 0;
            foreach (var death in gd.deaths)
            {
                if (DeathSceneGenerator.GenerateDeathScene(death, gd))
                    built++;
            }

            AddressableGroupGenerator.UpdateAddressableGroups(gd);
            Debug.Log($"[GameBuilder] Built {built} death scenes");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("[GameBuilder] No GameDefinition asset found!");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Ensure a folder exists, create if not.
        /// </summary>
        public static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
