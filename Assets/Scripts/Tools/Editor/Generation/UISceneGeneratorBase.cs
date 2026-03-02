#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Base class for UI scene generators with shared utilities.
    /// </summary>
    public static class UISceneGeneratorBase
    {
        /// <summary>
        /// Creates a new scene with standard UI infrastructure.
        /// </summary>
        public static void CreateBaseScene(string scenePath, UISceneConfig config, out GameObject canvasGO, out RectTransform safeArea)
        {
            // Create new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = config.cameraOrthoSize;
            camera.backgroundColor = config.cameraBackgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraGO.AddComponent<AudioListener>();
            cameraGO.tag = "MainCamera";

            // Create EventSystem
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // Create Canvas
            canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<GraphicRaycaster>();
            
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // Create SafeAreaPanel
            var safeAreaGO = new GameObject("SafeAreaPanel");
            safeAreaGO.transform.SetParent(canvasGO.transform, false);
            safeArea = safeAreaGO.AddComponent<RectTransform>();
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;

            // Create Background (if sprite provided)
            if (config.backgroundSprite != null)
            {
                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(canvasGO.transform, false);
                bgGO.transform.SetAsFirstSibling();
                var bgRect = bgGO.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImage = bgGO.AddComponent<Image>();
                bgImage.sprite = config.backgroundSprite;
                bgImage.preserveAspect = true;
            }

            // Create Directional Light (if enabled)
            if (config.useDirectionalLight)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = config.lightColor;
                light.intensity = config.lightIntensity;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>
        /// Creates a TextMeshProUGUI element.
        /// </summary>
        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment = TextAlignmentOptions.Center, float fontSize = 36f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.fontSize = fontSize;
            
            return tmp;
        }

        /// <summary>
        /// Creates a button container with VerticalLayoutGroup.
        /// </summary>
        public static RectTransform CreateButtonContainer(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400, 600);
            
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            return rect;
        }

        /// <summary>
        /// Creates a simple button with text.
        /// </summary>
        public static GameObject CreateSimpleButton(Transform parent, string name, string text, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Add MobileControllerFramework.Button
            var button = go.AddComponent<MobileControllerFramework.Button>();
            
            // Add CanvasGroup for alpha control
            go.AddComponent<CanvasGroup>();
            
            // Add text child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            
            return go;
        }

        /// <summary>
        /// Saves the current scene to the specified path.
        /// </summary>
        public static void SaveScene(string scenePath)
        {
            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(scenePath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            AssetDatabase.Refresh();
        }
    }
}
#endif
