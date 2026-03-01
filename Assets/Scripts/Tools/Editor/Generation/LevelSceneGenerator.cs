#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;

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
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, content.transform);
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

            // Camera
            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(sceneRoot.transform);
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cameraGo.AddComponent<AudioListener>();

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
    }
}
#endif
