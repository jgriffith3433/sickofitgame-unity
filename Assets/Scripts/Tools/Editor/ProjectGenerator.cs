#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;
using SickOfIt.Tools.Editor.Generation;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// THE ONLY PROJECT BUILDER YOU NEED.
    /// 
    /// Reads GameDefinition and generates 3 simple scenes:
    /// - Video.unity (VideoRuntime reads config to know which video to play)
    /// - Level.unity (LevelRuntime reads config to know which level to build)
    /// - Death.unity (DeathRuntime reads config to know which death to show)
    /// - Sets up Addressables  
    /// - Everything is config-driven from GameDefinition
    /// 
    /// ONE BUTTON. ONE SCRIPT. BOOM.
    /// </summary>
    public static class ProjectGenerator
    {
        private const string SCENES_ROOT = "Assets/Scenes";
        
        [MenuItem("SickOfIt/Generate Project")]
        public static void GenerateProject()
        {
            Debug.Log("=== GENERATING PROJECT FROM CONFIG ===");
            
            var gd = LoadGameDefinition();
            if (gd == null) return;
            
            // Clean up old scenes first
            CleanupOldScenes();
            
            // Delegate to individual scene generators
            // NOTE: Each NewScene(EmptyScene, Single) can unload the SO from native side,
            // so we reload GameDefinition before each generator call.
            try { VideoSceneGenerator.GenerateVideoScene(gd); }
            catch (System.Exception e) { Debug.LogError($"VideoSceneGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { LevelSceneGenerator.GenerateLevelScene(gd); }
            catch (System.Exception e) { Debug.LogError($"LevelSceneGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { DeathSceneGenerator.GenerateDeathScene(gd); }
            catch (System.Exception e) { Debug.LogError($"DeathSceneGenerator failed: {e}"); }
            
            // Refresh AssetDatabase so newly-saved scenes have valid GUIDs
            // before Addressables tries to register them
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Setup Addressables
            gd = LoadGameDefinition();
            SetupAddressables(gd);
            
            // Force refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("=== PROJECT GENERATION COMPLETE ===");
            Debug.Log($"✅ Generated: Video.unity, Level.unity, Death.unity");
            Debug.Log($"✅ Config from GameDefinition: {gd.levels.Count} levels, {gd.videos.Count} videos, {gd.deaths.Count} deaths");
            Debug.Log($"✅ Addressables configured");
            Debug.Log($"🎮 Game ready to play!");
            
            // Open MainMenu scene so it's ready to play
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        }
        
        private static void CleanupOldScenes()
        {
            Debug.Log("Cleaning up old scenes...");
            
            // Delete old scene folders and individual scenes
            string[] foldersToDelete = {
                "Assets/Scenes/Deaths",
                "Assets/Scenes/LevelS", 
                "Assets/Scenes/VideoS",
                "Assets/Scenes/Levels",
                "Assets/Scenes/Videos"
            };
            
            foreach (string folder in foldersToDelete)
            {
                if (Directory.Exists(folder))
                {
                    AssetDatabase.DeleteAsset(folder);
                    Debug.Log($"Deleted folder: {folder}");
                }
            }
            
            // Delete any existing individual scenes
            string[] scenePrefixes = { "Video", "Level", "Death", "Unified" };
            foreach (string prefix in scenePrefixes)
            {
                var sceneGuids = AssetDatabase.FindAssets($"{prefix} t:Scene", new[] { SCENES_ROOT });
                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string sceneName = Path.GetFileNameWithoutExtension(path);
                    
                    // Don't delete menu scenes
                    if (sceneName.Contains("Menu") || sceneName.Contains("Selection") || sceneName.Contains("Shop"))
                        continue;
                        
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Deleted old scene: {sceneName}");
                }
            }
        }
        
        // --- Commented out: now delegated to VideoSceneGenerator ---
        // private static void GenerateVideoScene(GameDefinition gd) { ... }
        
        // --- Commented out: now delegated to LevelSceneGenerator ---
        // private static void GenerateLevelScene(GameDefinition gd) { ... }
        
        // --- Commented out: now delegated to DeathSceneGenerator ---
        // private static void GenerateDeathScene(GameDefinition gd) { ... }
        
        private static void SetupAddressables(GameDefinition gd)
        {
            Debug.Log("Setting up Addressables...");
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    Debug.LogError("Could not create Addressable Asset Settings!");
                    return;
                }
            }
            
            // Create or get scenes group
            var group = GetOrCreateGroup(settings, "GameScenes");
            
            int entriesAdded = 0;
            
            // Add the 3 simple scenes
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Video.unity", "Video"))
                entriesAdded++;
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Level.unity", "Level"))
                entriesAdded++;
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Death.unity", "Death"))
                entriesAdded++;
            
            if (entriesAdded > 0)
            {
                EditorUtility.SetDirty(settings);
                Debug.Log($"✅ Added {entriesAdded} scenes to Addressables");
            }
        }
        
        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"Created Addressables group: {groupName}");
            }
            return group;
        }
        
        private static bool AddSceneToAddressables(AddressableAssetSettings settings, AddressableAssetGroup group, string scenePath, string address)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
                return false;
            }
            
            string guid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(guid))
                return false;
            
            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                // Already exists — ensure correct address and group
                if (entry.parentGroup != group)
                    settings.MoveEntry(entry, group);
                if (entry.address != address)
                    entry.SetAddress(address);
                return false; // Not newly added
            }
            
            // Add new entry
            entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(address);
            Debug.Log($"Added to Addressables: {address} -> {scenePath}");
            return true;
        }
        
        private static Camera PlaceCamera(GameObject cameraPrefab, string sceneType)
        {
            GameObject camGo;
            if (cameraPrefab != null)
            {
                camGo = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
                camGo.name = "Main Camera";
                camGo.tag = "MainCamera";
                Debug.Log($"✅ Placed {sceneType} camera prefab");
            }
            else
            {
                // Fallback: create a default camera so the scene is still usable
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 2.8f;
                cam.backgroundColor = Color.black;
                cam.transform.position = new Vector3(0, 0, -10f);
                camGo.AddComponent<AudioListener>();
                Debug.LogWarning($"⚠️ No {sceneType}CameraPrefab in GameDefinition — using default camera.");
            }
            return camGo.GetComponent<Camera>();
        }
        
        /// <summary>
        /// Find every Canvas on the UI prefab instance and assign its worldCamera.
        /// Handles Screen Space - Camera canvases.
        /// </summary>
        private static void WireCanvasCamera(GameObject uiRoot, Camera camera)
        {
            if (uiRoot == null || camera == null) return;
            
            foreach (var canvas in uiRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.worldCamera = camera;
                }
            }
        }

        /// <summary>
        /// Ensures a folder exists in the Asset database. Used by scene generators.
        /// </summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            
            AssetDatabase.CreateFolder(parent, folderName);
        }
        
        private static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("No GameDefinition found! Create one via: Assets → Create → Sick Of It → Game Definition");
                return null;
            }
            
            var gd = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Debug.Log($"Found GameDefinition: {gd.levels.Count} levels, {gd.videos.Count} videos, {gd.deaths.Count} deaths");
            return gd;
        }
    }
}
#endif