#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;

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
    ///     - VideoRuntime
    /// 
    /// VideoRuntime is placed directly in the scene and drives playback from config.
    /// </summary>
    public static class VideoSceneGenerator
    {
        private const string SCENES_ROOT = "Assets/Scenes";

        public static bool GenerateVideoScene(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[VideoGen] Null GameDefinition!");
                return false;
            }

            ////Debug.Log("Generating Video.unity...");
            string scenePath = $"{SCENES_ROOT}/Video.unity";

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
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;

            // Camera from prefab
            Camera sceneCamera = null;
            if (gd.videoCameraPrefab != null)
            {
                var cameraGo = (GameObject)PrefabUtility.InstantiatePrefab(gd.videoCameraPrefab);
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
                cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
                sceneCamera = cameraGo.GetComponent<Camera>();
            }

            GeneratorHelper.ConfigureSceneCamera(sceneCamera, gd.cinematicCameraOrthoSize, gd.cinematicCameraBackground);

            EnsureEventSystem();

            // Place UI prefab (skip button) from GameDefinition
            GameObject ui = null;
            if (gd.videoUIPrefab != null)
            {
                ui = (GameObject)PrefabUtility.InstantiatePrefab(gd.videoUIPrefab);
                ui.name = "UI";
                ui.transform.SetParent(content.transform);
                WireCanvasCamera(ui, sceneCamera);
                ////Debug.Log("[VideoGen] Placed video UI prefab");
            }

            // Add VideoRuntime directly to the scene and wire references
            var videoRuntimeGo = new GameObject("VideoRuntime");
            videoRuntimeGo.transform.SetParent(sceneRoot.transform);
            var videoRuntime = videoRuntimeGo.AddComponent<VideoRuntime>();
            var vrso = new SerializedObject(videoRuntime);
            vrso.FindProperty("videoPlayerObject").objectReferenceValue = videoPlayer;
            vrso.FindProperty("audioObject").objectReferenceValue = audioGo;
            if (ui != null) vrso.FindProperty("uiRoot").objectReferenceValue = ui;
            vrso.ApplyModifiedPropertiesWithoutUndo();

            // Wire content roots
            var so = new SerializedObject(contentController);
            var contentRoots = so.FindProperty("contentRoots");
            contentRoots.arraySize = 1;
            contentRoots.GetArrayElementAtIndex(0).objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);

            ////Debug.Log($"[VideoGen] Generated scene: {scenePath}");
            return true;
        }
        
        private static void EnsureEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
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
