#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates a death scene from DeathConfig.
    /// 
    /// Scene structure:
    ///   - SceneRoot (SceneContentController)
    ///     - Content/
    ///       - DeathAnimation (SpriteRenderer + SpriteFramePlayer)
    ///       - Audio (AudioSource)
    ///       - Camera
    /// 
    /// DeathRuntime auto-bootstraps and drives playback from config.
    /// </summary>
    public static class DeathSceneGenerator
    {
        private const string DEATHS_FOLDER = "Assets/Scenes/Deaths";

        public static bool GenerateDeathScene(DeathConfig config, GameDefinition gd)
        {
            if (config == null)
            {
                Debug.LogError("[DeathGen] Null DeathConfig!");
                return false;
            }

            string sceneName = config.deathName;
            string scenePath = $"{DEATHS_FOLDER}/{sceneName}.unity";

            GameBuilder.EnsureFolder(DEATHS_FOLDER);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Scene root
            var sceneRoot = new GameObject("SceneRoot");
            var contentController = sceneRoot.AddComponent<SickOfIt.Core.Flow.SceneContentController>();

            // Content parent
            var content = new GameObject("Content");
            content.transform.SetParent(sceneRoot.transform);

            // Death animation object
            var deathAnim = new GameObject("DeathAnimation");
            deathAnim.transform.SetParent(content.transform);
            var sr = deathAnim.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -10;
            // SpriteFramePlayer will be added at runtime by DeathRuntime

            // Audio
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(content.transform);
            var audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            if (config.audio != null)
                audioSource.clip = config.audio;

            // Camera
            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(sceneRoot.transform);
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            // Wire content roots
            var so = new SerializedObject(contentController);
            var contentRoots = so.FindProperty("contentRoots");
            contentRoots.arraySize = 1;
            contentRoots.GetArrayElementAtIndex(0).objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[DeathGen] Generated scene: {scenePath}");
            return true;
        }
    }
}
#endif
