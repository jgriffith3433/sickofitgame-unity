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
            
            // Generate the 3 simple scenes
            GenerateVideoScene();
            GenerateLevelScene();
            GenerateDeathScene();
            
            // Setup Addressables
            SetupAddressables(gd);
            
            // Force refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("=== PROJECT GENERATION COMPLETE ===");
            Debug.Log($"✅ Generated: Video.unity, Level.unity, Death.unity");
            Debug.Log($"✅ Config from GameDefinition: {gd.levels.Count} levels, {gd.videos.Count} videos, {gd.deaths.Count} deaths");
            Debug.Log($"✅ Addressables configured");
            Debug.Log($"🎮 Game ready to play!");
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
        
        private static void GenerateVideoScene()
        {
            Debug.Log("Generating Video.unity...");
            string scenePath = $"{SCENES_ROOT}/Video.unity";
            
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Setup camera
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 0, -10f);
                camera.backgroundColor = Color.black;
                camera.gameObject.name = "Main Camera";
            }
            
            // Create VideoPlayer GameObject
            var videoPlayer = new GameObject("VideoPlayer");
            var spriteRenderer = videoPlayer.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 0;
            spriteRenderer.color = Color.white;
            videoPlayer.transform.position = Vector3.zero;
            
            // Create Audio GameObject
            var audioGo = new GameObject("Audio");
            var audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            
            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("✅ Created: Video.unity");
        }
        
        private static void GenerateLevelScene()
        {
            Debug.Log("Generating Level.unity...");
            string scenePath = $"{SCENES_ROOT}/Level.unity";
            
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Setup camera
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 0, -10f);
                camera.backgroundColor = new Color(0.1f, 0.1f, 0.2f);
                camera.gameObject.name = "Main Camera";
            }
            
            // Create basic level structure
            var levelRoot = new GameObject("Level");
            var spawnsRoot = new GameObject("Spawns");
            spawnsRoot.transform.SetParent(levelRoot.transform);
            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(spawnsRoot.transform);
            
            // Create player character (LevelRuntime expects this)
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            // Note: PlayerHealth component would be added by the game's player prefab system
            // For now just create the GameObject so LevelRuntime doesn't crash
            
            // Create music audio source
            var musicGo = new GameObject("Music");
            var audioSource = musicGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio
            
            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("✅ Created: Level.unity");
        }
        
        private static void GenerateDeathScene()
        {
            Debug.Log("Generating Death.unity...");
            string scenePath = $"{SCENES_ROOT}/Death.unity";
            
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Setup camera
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 0, -10f);
                camera.backgroundColor = Color.black;
                camera.gameObject.name = "Main Camera";
            }
            
            // Create death player
            var deathPlayer = new GameObject("DeathPlayer");
            var spriteRenderer = deathPlayer.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 0;
            spriteRenderer.color = Color.white;
            deathPlayer.transform.position = Vector3.zero;
            
            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("✅ Created: Death.unity");
        }
        
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