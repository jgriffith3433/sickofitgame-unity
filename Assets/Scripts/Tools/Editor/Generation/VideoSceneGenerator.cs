#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates a video/cutscene scene from VideoConfig.
    /// 
    /// Scene structure:
    ///   - SceneRoot (SceneContentController)
    ///     - Content/
    ///       - VideoPlayer (SpriteFramePlayer — driven by GameClock)
    ///       - Audio (AudioSource)
    ///       - UI/ (skip button)
    ///       - Camera
    /// 
    /// VideoRuntime auto-bootstraps and drives playback from config.
    /// </summary>
    public static class VideoSceneGenerator
    {
        private const string VIDEOS_FOLDER = "Assets/Scenes/Videos";

        public static bool GenerateVideoScene(VideoConfig config, GameDefinition gd)
        {
            if (config == null)
            {
                Debug.LogError("[VideoGen] Null VideoConfig!");
                return false;
            }

            string sceneName = config.videoName;
            string scenePath = $"{VIDEOS_FOLDER}/{sceneName}.unity";

            GameBuilder.EnsureFolder(VIDEOS_FOLDER);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Scene root
            var sceneRoot = new GameObject("SceneRoot");
            var contentController = sceneRoot.AddComponent<SickOfIt.Core.Flow.SceneContentController>();

            // Content parent
            var content = new GameObject("Content");
            content.transform.SetParent(sceneRoot.transform);

            // Video playback object (SpriteRenderer + SpriteFramePlayer)
            var videoPlayer = new GameObject("VideoPlayer");
            videoPlayer.transform.SetParent(content.transform);
            var sr = videoPlayer.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -10;
            // SpriteFramePlayer will be added at runtime by VideoRuntime

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
            cam.orthographicSize = 2.8f;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            // Wire content roots
            var so = new SerializedObject(contentController);
            var contentRoots = so.FindProperty("contentRoots");
            contentRoots.arraySize = 1;
            contentRoots.GetArrayElementAtIndex(0).objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[VideoGen] Generated scene: {scenePath}");
            return true;
        }
    }
}
#endif
