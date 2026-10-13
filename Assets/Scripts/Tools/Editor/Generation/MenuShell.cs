#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
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
    /// Shared world for LevelSelection, VideoSelection, and UpgradeShop:
    /// land backdrop, camera, mobile UI, title, button column, and loading screen.
    /// </summary>
    public sealed class MenuShell
    {
        const string GroundSpriteGuid = "f9d4b0bbd6907574ca415143f3506041";
        const string ButtonFontGuid = "8f586378b4e144a9851e7b34d9b748ee";
        const string DefaultButtonPath = "Assets/nappin/MobileControllerFramework/Prefabs/Button/(Prb)ButtonOval.prefab";
        const string LoadingScreenPath = "Assets/Prefabs/LoadingScreen.prefab";

        public Camera Camera;
        public GameObject Mobile;
        public RectTransform SafeArea;
        public GameObject Panel;
        public RectTransform Container;
        public GameObject LoadingScreen;
        public SceneLoadingManager Loading;
        public TextMeshProUGUI Health;
        public TextMeshProUGUI Points;

        public static MenuShell Build(
            GameDefinition gd,
            UISceneConfig config,
            string panelName,
            string title,
            string containerName,
            float spacing,
            Vector2 buttonSize)
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

            foreach (var tmp in shell.Mobile.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "CumulativeHealth")
                    shell.Health = tmp;
                else if (tmp.gameObject.name == "Points" || tmp.gameObject.name == "PointText")
                    shell.Points = tmp;
            }

            shell.Panel = new GameObject(panelName, typeof(RectTransform));
            shell.Panel.transform.SetParent(shell.SafeArea, false);
            GeneratorHelper.SetLayer(shell.Panel, "UI");
            var panelRect = shell.Panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1920f, 761f);

            var titleText = CreateText(shell.Panel.transform, "Title", string.IsNullOrEmpty(title) ? "Select" : title, 48f);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(800f, 80f);

            var containerGo = new GameObject(containerName, typeof(RectTransform));
            containerGo.transform.SetParent(shell.Panel.transform, false);
            GeneratorHelper.SetLayer(containerGo, "UI");
            shell.Container = containerGo.GetComponent<RectTransform>();
            shell.Container.anchorMin = new Vector2(0.5f, 1f);
            shell.Container.anchorMax = new Vector2(0.5f, 1f);
            shell.Container.pivot = new Vector2(0.5f, 1f);
            shell.Container.anchoredPosition = new Vector2(0f, -129f);
            shell.Container.sizeDelta = new Vector2(Mathf.Max(buttonSize.x, 420f), 700f);

            var layout = containerGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

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
            var deactivate = loadingSo.FindProperty("objectsToDeactivateOnLoad");
            deactivate.ClearArray();
            deactivate.InsertArrayElementAtIndex(0);
            deactivate.GetArrayElementAtIndex(0).objectReferenceValue = shell.Panel;
            loadingSo.ApplyModifiedPropertiesWithoutUndo();

            return shell;
        }

        public static GameObject AddOvalButton(Transform parent, GameObject prefab, string objectName, string label, Vector2 size)
        {
            if (prefab == null)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultButtonPath);
            if (prefab == null)
                return null;

            var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            button.name = objectName;
            GeneratorHelper.SetLayer(button, "UI");
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = size;

            foreach (var child in button.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == rect)
                    continue;
                if (child.gameObject.name == "_iconImg")
                {
                    child.gameObject.SetActive(false);
                    continue;
                }
                if (child.gameObject.name == "_backgroundImg" || child.gameObject.name == "Content_Target")
                    child.sizeDelta = size;
            }

            var labelParent = button.transform.Find("Content_Target");
            if (labelParent == null)
                labelParent = button.transform;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(labelParent, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var font = LoadFont();
            if (font != null)
                tmp.font = font;

            return button;
        }

        public static void PlaceCornerButton(GameObject button, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect == null)
                return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            GeneratorHelper.SetLayer(go, "UI");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var font = LoadFont();
            if (font != null)
                tmp.font = font;
            var rect = tmp.rectTransform;
            rect.sizeDelta = new Vector2(420f, 50f);
            return tmp;
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

        static TMP_FontAsset LoadFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(ButtonFontGuid));
        }
    }
}
#endif
