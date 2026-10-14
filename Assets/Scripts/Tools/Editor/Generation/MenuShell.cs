#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Device;
using SickOfIt.Core.Flow;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Shared world for LevelSelection, VideoSelection, and UpgradeShop: land backdrop, camera, mobile UI and
    /// loading screen. The menus themselves (title, cards, buttons) are built at runtime by each screen's manager
    /// from its config's "layout" and ui/theme.json, so they change without regenerating.
    /// </summary>
    public sealed class MenuShell
    {
        const string GroundSpriteGuid = "f9d4b0bbd6907574ca415143f3506041";
        const string LoadingScreenPath = "Assets/Prefabs/LoadingScreen.prefab";

        public Camera Camera;
        public GameObject Mobile;
        public RectTransform SafeArea;
        public GameObject LoadingScreen;
        public SceneLoadingManager Loading;

        public static MenuShell Build(GameDefinition gd, UISceneConfig config)
        {
            if (gd == null)
                return null;
            if (gd.levelUIPrefab == null)
            {
                Debug.LogError("[MenuShell] GameDefinition.levelUIPrefab is not set.");
                return null;
            }

            config = config ?? new UISceneConfig();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var shell = new MenuShell();
            var sceneRoot = new GameObject("[Scene]");

            var land = new GameObject("Land");
            land.transform.SetParent(sceneRoot.transform, false);
            var ground = new GameObject("Ground");
            ground.transform.SetParent(land.transform, false);
            ground.transform.localPosition = new Vector3(config.groundPosition.x, config.groundPosition.y, 0f);
            var renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = config.groundSprite != null
                ? config.groundSprite
                : LoadSprite(GroundSpriteGuid);

            shell.Camera = PlaceCamera(gd, config, sceneRoot.transform);
            PlaceLight(config, sceneRoot.transform);
            PlaceEventSystem();

            shell.Mobile = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelUIPrefab);
            shell.Mobile.name = "MobileController";
            foreach (var canvas in shell.Mobile.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.worldCamera = shell.Camera;
            }

            shell.SafeArea = FindNamed(shell.Mobile.transform, "SafeAreaPanel") as RectTransform;
            if (shell.SafeArea == null)
                shell.SafeArea = shell.Mobile.GetComponent<RectTransform>();

            var loadingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingScreenPath);
            if (loadingPrefab != null)
            {
                shell.LoadingScreen = (GameObject)PrefabUtility.InstantiatePrefab(loadingPrefab, shell.SafeArea);
                shell.LoadingScreen.name = "LoadingScreen";
                var loadingRect = shell.LoadingScreen.GetComponent<RectTransform>();
                if (loadingRect != null)
                {
                    loadingRect.anchorMin = Vector2.zero;
                    loadingRect.anchorMax = Vector2.one;
                    loadingRect.offsetMin = Vector2.zero;
                    loadingRect.offsetMax = Vector2.zero;
                }
                shell.LoadingScreen.SetActive(false);
            }

            var loadingGo = new GameObject("Scene Loading Manager");
            shell.Loading = loadingGo.AddComponent<SceneLoadingManager>();
            var loadingSo = new SerializedObject(shell.Loading);
            var activate = loadingSo.FindProperty("objectsToActivateOnLoad");
            activate.ClearArray();
            if (shell.LoadingScreen != null)
            {
                activate.InsertArrayElementAtIndex(0);
                activate.GetArrayElementAtIndex(0).objectReferenceValue = shell.LoadingScreen;
            }
            // The runtime menu registers itself to be hidden on load (SceneLoadingManager.HideOnLoad).
            loadingSo.FindProperty("objectsToDeactivateOnLoad").ClearArray();
            loadingSo.ApplyModifiedPropertiesWithoutUndo();

            return shell;
        }

        static Camera PlaceCamera(GameDefinition gd, UISceneConfig config, Transform parent)
        {
            Camera camera;
            if (gd.levelCameraPrefab != null)
            {
                var cameraGo = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelCameraPrefab, parent);
                cameraGo.name = "Camera";
                cameraGo.tag = "MainCamera";
                camera = cameraGo.GetComponent<Camera>();
            }
            else
            {
                var cameraGo = new GameObject("Camera");
                cameraGo.transform.SetParent(parent, false);
                cameraGo.tag = "MainCamera";
                camera = cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
            }

            camera.transform.localPosition = config.cameraPosition;
            GeneratorHelper.ConfigureSceneCamera(camera, config.cameraOrthoSize, config.cameraBackgroundColor);
            var aspect = camera.GetComponent<AspectRatioController>();
            if (aspect != null)
                aspect.letterboxColor = Color.black;
            return camera;
        }

        static void PlaceLight(UISceneConfig config, Transform parent)
        {
            if (!config.useDirectionalLight)
                return;

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = config.lightColor;
            light.intensity = config.lightIntensity;
        }

        static void PlaceEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                    return transforms[i];
            }
            return null;
        }

        static Sprite LoadSprite(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite found)
                    return found;
            }
            return null;
        }
    }
}
#endif
