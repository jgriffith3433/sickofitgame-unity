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
    /// Generates a death scene from DeathConfig.
    /// 
    /// Scene structure:
    ///   - SceneRoot (SceneContentController)
    ///     - Content/
    ///       - DeathAnimation (SpriteRenderer + SpriteFramePlayer)
    ///       - Audio (AudioSource)
    ///       - Camera
    ///     - DeathRuntime
    /// 
    /// DeathRuntime is placed directly in the scene and drives playback from config.
    /// </summary>
    public static class DeathSceneGenerator
    {
        private const string SCENES_ROOT = "Assets/Scenes";

        public static bool GenerateDeathScene(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[DeathGen] Null GameDefinition!");
                return false;
            }

            Debug.Log("Generating Death.unity...");
            string scenePath = $"{SCENES_ROOT}/Death.unity";

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

            // Camera from prefab
            Camera sceneCamera = null;
            if (gd.deathCameraPrefab != null)
            {
                var cameraGo = (GameObject)PrefabUtility.InstantiatePrefab(gd.deathCameraPrefab);
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
                cam.orthographicSize = gd.cinematicCameraOrthoSize;
                cam.backgroundColor = gd.cinematicCameraBackground;
                cameraGo.AddComponent<AudioListener>();
                sceneCamera = cam;
            }

            // Place UI prefab (skip button) from GameDefinition
            GameObject ui = null;
            if (gd.deathUIPrefab != null)
            {
                ui = (GameObject)PrefabUtility.InstantiatePrefab(gd.deathUIPrefab);
                ui.name = "UI";
                ui.transform.SetParent(content.transform);
                WireCanvasCamera(ui, sceneCamera);
                Debug.Log("[DeathGen] Placed death UI prefab");
            }

            // Add DeathRuntime directly to the scene and wire references
            var deathRuntimeGo = new GameObject("DeathRuntime");
            deathRuntimeGo.transform.SetParent(sceneRoot.transform);
            var deathRuntime = deathRuntimeGo.AddComponent<DeathRuntime>();
            var drso = new SerializedObject(deathRuntime);
            drso.FindProperty("deathAnimationObject").objectReferenceValue = deathAnim;
            drso.FindProperty("audioSource").objectReferenceValue = audioSource;
            if (ui != null) drso.FindProperty("uiRoot").objectReferenceValue = ui;
            drso.ApplyModifiedPropertiesWithoutUndo();

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
