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
            GenerateVideoScene(gd);
            GenerateLevelScene(gd);
            GenerateDeathScene(gd);
            
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
        
        private static void GenerateVideoScene(GameDefinition gd)
        {
            Debug.Log("Generating Video.unity...");
            string scenePath = $"{SCENES_ROOT}/Video.unity";
            
            // Create new scene (empty — camera comes from prefab)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Camera from prefab
            var sceneCamera = PlaceCamera(gd.videoCameraPrefab, "video");
            
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
            
            // Place UI prefab (skip button)
            GameObject ui = null;
            if (gd.videoUIPrefab != null)
            {
                ui = (GameObject)PrefabUtility.InstantiatePrefab(gd.videoUIPrefab);
                ui.name = "UI";
                WireCanvasCamera(ui, sceneCamera);
                Debug.Log("✅ Placed video UI prefab in Video scene");
            }
            else
            {
                Debug.LogWarning("⚠️ No videoUIPrefab in GameDefinition — Video scene has no skip button.");
            }
            
            // Add VideoRuntime directly to the scene and wire references
            var videoRuntimeGo = new GameObject("VideoRuntime");
            var videoRuntime = videoRuntimeGo.AddComponent<VideoRuntime>();
            var vso = new SerializedObject(videoRuntime);
            vso.FindProperty("videoPlayerObject").objectReferenceValue = videoPlayer;
            vso.FindProperty("audioObject").objectReferenceValue = audioGo;
            if (ui != null) vso.FindProperty("uiRoot").objectReferenceValue = ui;
            vso.ApplyModifiedPropertiesWithoutUndo();

            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("✅ Created: Video.unity");
        }
        
        private static void GenerateLevelScene(GameDefinition gd)
        {
            Debug.Log("Generating Level.unity...");
            string scenePath = $"{SCENES_ROOT}/Level.unity";
            
            // Create new scene (empty — camera comes from prefab)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Camera from prefab
            var sceneCamera = PlaceCamera(gd.levelCameraPrefab, "level");
            
            // Create basic level structure
            var levelRoot = new GameObject("Level");
            var spawnsRoot = new GameObject("Spawns");
            spawnsRoot.transform.SetParent(levelRoot.transform);
            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(spawnsRoot.transform);
            
            // Instantiate player Character prefab from GameDefinition config
            GameObject player = null;
            if (gd.player?.playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(gd.player.playerPrefab);
                player.name = "Character";
                player.transform.position = Vector3.zero;
                Debug.Log($"✅ Placed Character prefab in Level scene");
            }
            else
            {
                Debug.LogWarning("⚠️ No playerPrefab in GameDefinition.player — Level scene has no player. Assign it in GameDefinition and regenerate.");
            }
            
            // Place full UI prefab (MobileController)
            GameObject mobileController = null;
            if (gd.levelUIPrefab != null)
            {
                mobileController = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelUIPrefab);
                mobileController.name = "MobileController";
                WireCanvasCamera(mobileController, sceneCamera);
                Debug.Log("✅ Placed level UI prefab in Level scene");
            }
            else
            {
                Debug.LogWarning("⚠️ No levelUIPrefab in GameDefinition — Level scene has no mobile controls.");
            }
            
            // Create music audio source
            var musicGo = new GameObject("Music");
            var audioSource = musicGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio
            
            // Add LevelRuntime directly to the scene and wire references
            var levelRuntimeGo = new GameObject("LevelRuntime");
            var levelRuntime = levelRuntimeGo.AddComponent<LevelRuntime>();
            var lso = new SerializedObject(levelRuntime);
            if (player != null) lso.FindProperty("characterObject").objectReferenceValue = player;
            lso.FindProperty("playerSpawnPoint").objectReferenceValue = playerSpawn.transform;
            lso.FindProperty("musicSource").objectReferenceValue = audioSource;
            if (mobileController != null) lso.FindProperty("mobileControllerObject").objectReferenceValue = mobileController;
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Add StateRecorder + RewindController and wire their references
            var stateRecorder = levelRuntimeGo.AddComponent<StateRecorder>();
            var srso = new SerializedObject(stateRecorder);
            if (player != null)
            {
                srso.FindProperty("playerTransform").objectReferenceValue = player.transform;
                var rb = player.GetComponentInChildren<Rigidbody2D>();
                if (rb != null) srso.FindProperty("playerRb").objectReferenceValue = rb;
                var ph = player.GetComponentInChildren<SickOfIt.Combat.Health.PlayerHealth>();
                if (ph != null) srso.FindProperty("playerHealth").objectReferenceValue = ph;
            }
            if (sceneCamera != null) srso.FindProperty("mainCamera").objectReferenceValue = sceneCamera;
            srso.ApplyModifiedPropertiesWithoutUndo();

            var rewindController = levelRuntimeGo.AddComponent<RewindController>();
            var rcso = new SerializedObject(rewindController);
            rcso.FindProperty("musicSource").objectReferenceValue = audioSource;
            rcso.ApplyModifiedPropertiesWithoutUndo();

            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("✅ Created: Level.unity");
        }
        
        private static void GenerateDeathScene(GameDefinition gd)
        {
            Debug.Log("Generating Death.unity...");
            string scenePath = $"{SCENES_ROOT}/Death.unity";
            
            // Create new scene (empty — camera comes from prefab)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Camera from prefab
            var sceneCamera = PlaceCamera(gd.deathCameraPrefab, "death");
            
            // Create death player
            var deathPlayer = new GameObject("DeathPlayer");
            var spriteRenderer = deathPlayer.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 0;
            spriteRenderer.color = Color.white;
            deathPlayer.transform.position = Vector3.zero;
            
            // Place UI prefab (skip button)
            GameObject deathUi = null;
            if (gd.deathUIPrefab != null)
            {
                deathUi = (GameObject)PrefabUtility.InstantiatePrefab(gd.deathUIPrefab);
                deathUi.name = "UI";
                WireCanvasCamera(deathUi, sceneCamera);
                Debug.Log("✅ Placed death UI prefab in Death scene");
            }
            else
            {
                Debug.LogWarning("⚠️ No deathUIPrefab in GameDefinition — Death scene has no skip button.");
            }
            
            // Add DeathRuntime directly to the scene and wire references
            var deathRuntimeGo = new GameObject("DeathRuntime");
            var deathRuntime = deathRuntimeGo.AddComponent<DeathRuntime>();
            var dso = new SerializedObject(deathRuntime);
            dso.FindProperty("deathAnimationObject").objectReferenceValue = deathPlayer;
            if (deathUi != null) dso.FindProperty("uiRoot").objectReferenceValue = deathUi;
            dso.ApplyModifiedPropertiesWithoutUndo();

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