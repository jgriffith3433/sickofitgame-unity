#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates a level scene from LevelConfig.
    /// 
    /// The generated scene is minimal — just the structural skeleton.
    /// LevelRuntime does the actual spawning at runtime from config.
    /// 
    /// Scene structure:
    ///   - SceneRoot (SceneContentController, SpriteFrameLoadWaiter)
    ///     - Backgrounds/ (PlayableDirectors for sprite frame playback)
    ///     - Player (Character prefab instance)
    ///     - Level (ground collider, boundaries)
    ///     - UI/ (mobile controls, HUD)
    ///     - LevelCompletion (PlayableDirector for end sequence)
    /// </summary>
    public static class LevelSceneGenerator
    {
        private const string LEVELS_FOLDER = "Assets/Scenes/Levels";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Character.prefab";

        public static bool GenerateLevelScene(LevelConfig config, GameDefinition gd)
        {
            if (config == null)
            {
                Debug.LogError("[LevelGen] Null LevelConfig!");
                return false;
            }

            string sceneName = config.levelName;
            string scenePath = $"{LEVELS_FOLDER}/{sceneName}.unity";

            GameBuilder.EnsureFolder(LEVELS_FOLDER);

            // Create or open scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create scene root
            var sceneRoot = new GameObject("SceneRoot");

            // Add SceneContentController for addressable loading
            var contentController = sceneRoot.AddComponent<SickOfIt.Core.Flow.SceneContentController>();

            // Create content parent (everything under here gets hidden/shown by SceneContentController)
            var content = new GameObject("Content");
            content.transform.SetParent(sceneRoot.transform);

            // Backgrounds container
            var backgrounds = new GameObject("Backgrounds");
            backgrounds.transform.SetParent(content.transform);

            // Player spawn point
            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(content.transform);
            playerSpawn.transform.position = new Vector3(-4f, 0f, 0f);

            // Try to instantiate player prefab
            GameObject player = null;
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, content.transform);
                player.name = "Character";
                player.transform.position = new Vector3(-4f, 0f, 0f);
            }

            // Ground / boundaries
            var level = new GameObject("Level");
            level.transform.SetParent(content.transform);

            // Ground collider
            var ground = new GameObject("Ground");
            ground.transform.SetParent(level.transform);
            ground.transform.position = new Vector3(0f, -3.5f, 0f);
            var groundCollider = ground.AddComponent<BoxCollider2D>();
            groundCollider.size = new Vector2(config.levelBounds.x * 2, 1f);
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) ground.layer = groundLayer;

            // Left wall
            var leftWall = new GameObject("LeftWall");
            leftWall.transform.SetParent(level.transform);
            leftWall.transform.position = new Vector3(-config.levelBounds.x, 0f, 0f);
            var leftCollider = leftWall.AddComponent<BoxCollider2D>();
            leftCollider.size = new Vector2(1f, config.levelBounds.y * 2);

            // Right wall
            var rightWall = new GameObject("RightWall");
            rightWall.transform.SetParent(level.transform);
            rightWall.transform.position = new Vector3(config.levelBounds.x, 0f, 0f);
            var rightCollider = rightWall.AddComponent<BoxCollider2D>();
            rightCollider.size = new Vector2(1f, config.levelBounds.y * 2);

            // Level completion trigger (right side)
            var winTrigger = new GameObject("WinTrigger");
            winTrigger.transform.SetParent(content.transform);
            winTrigger.transform.position = new Vector3(config.levelBounds.x - 0.5f, 0f, 0f);
            var winCollider = winTrigger.AddComponent<BoxCollider2D>();
            winCollider.isTrigger = true;
            winCollider.size = new Vector2(1f, config.levelBounds.y * 2);
            winTrigger.AddComponent<SickOfIt.Core.Progression.LevelWinTrigger>();

            // Music AudioSource
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(content.transform);
            var audioSource = musicGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            if (config.music != null)
                audioSource.clip = config.music;

            // Camera from prefab
            Camera sceneCamera = null;
            if (gd.levelCameraPrefab != null)
            {
                var cameraGo = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelCameraPrefab);
                cameraGo.name = "Main Camera";
                cameraGo.tag = "MainCamera";
                cameraGo.transform.SetParent(sceneRoot.transform);
                sceneCamera = cameraGo.GetComponent<Camera>();
            }
            else
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.transform.SetParent(sceneRoot.transform);
                cameraGo.tag = "MainCamera";
                var cam = cameraGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 2.8f;
                cameraGo.AddComponent<AudioListener>();
                sceneCamera = cam;
            }

            // Place full UI prefab (MobileController) from GameDefinition
            GameObject mobileController = null;
            if (gd.levelUIPrefab != null)
            {
                mobileController = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelUIPrefab);
                mobileController.name = "MobileController";
                mobileController.transform.SetParent(content.transform);
                WireCanvasCamera(mobileController, sceneCamera);
                Debug.Log("[LevelGen] Placed level UI prefab");
            }

            // Add LevelRuntime directly to the scene and wire references
            var levelRuntimeGo = new GameObject("LevelRuntime");
            levelRuntimeGo.transform.SetParent(sceneRoot.transform);
            var levelRuntime = levelRuntimeGo.AddComponent<LevelRuntime>();
            var lrso = new SerializedObject(levelRuntime);
            if (player != null) lrso.FindProperty("characterObject").objectReferenceValue = player;
            lrso.FindProperty("playerSpawnPoint").objectReferenceValue = playerSpawn.transform;
            lrso.FindProperty("musicSource").objectReferenceValue = audioSource;
            if (mobileController != null) lrso.FindProperty("mobileControllerObject").objectReferenceValue = mobileController;
            lrso.ApplyModifiedPropertiesWithoutUndo();

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

            // Set the contentRoots via SerializedObject
            var so = new SerializedObject(contentController);
            var contentRoots = so.FindProperty("contentRoots");
            contentRoots.arraySize = 1;
            contentRoots.GetArrayElementAtIndex(0).objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[LevelGen] Generated scene: {scenePath} " +
                $"(bounds: {config.levelBounds}, enemies: {config.EnemyCount})");

            return true;
        }
        
        private static void WireCanvasCamera(GameObject uiRoot, Camera camera)
        {
            if (uiRoot == null || camera == null) return;
            foreach (var canvas in uiRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.worldCamera = camera;
            }
        }
    }
}
#endif
