#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Addressables;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Builds MainMenu.unity from MainMenuConfig.
    /// The Character is a new instance of the player prefab, so its animator
    /// controller is the one Generate Project just wrote onto that prefab.
    /// </summary>
    public static class MainMenuSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/MainMenu.unity";
        const string DefaultButtonPath = "Assets/nappin/MobileControllerFramework/Prefabs/Button/(Prb)ButtonOval.prefab";
        const string PreloadingIndicatorPath = "Assets/Prefabs/PreloadingIndicator.prefab";
        const string ButtonFontGuid = "8f586378b4e144a9851e7b34d9b748ee";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[MainMenuSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.mainMenuConfig ?? new MainMenuConfig();
            if (gd.player == null || gd.player.playerPrefab == null)
            {
                Debug.LogError("[MainMenuSceneGen] No player prefab. Generate the player prefab first.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var menu = new GameObject("MainMenu");
            var content = new GameObject("Content");
            content.transform.SetParent(menu.transform, false);

            var contentControllerGo = new GameObject("SceneContentController");
            contentControllerGo.transform.SetParent(menu.transform, false);
            var contentController = contentControllerGo.AddComponent<SceneContentController>();

            Camera sceneCamera = PlaceCamera(gd, config, content.transform);
            PlaceLight(config, content.transform);

            var backgroundPlayer = PlaceArt(menu.transform, content.transform, config);

            var character = PlaceCharacter(gd, config, content.transform, sceneCamera);
            PlaceHouseColliders(content.transform, config);
            PlaceScreenBounds(content.transform, config);

            var mobile = PlaceMobileController(gd, content.transform, sceneCamera);
            var buttons = PlaceButtons(config, mobile);
            PlaceDownloadUi(config, mobile);

            PlaceManagers(buttons, out var sceneLoading, out var downloadManager);
            WireMenu(mobile, config, buttons, downloadManager, sceneLoading, character);

            var waiter = menu.AddComponent<SpriteFrameLoadWaiter>();
            var waiterSo = new SerializedObject(waiter);
            waiterSo.FindProperty("sceneContentController").objectReferenceValue = contentController;
            var players = waiterSo.FindProperty("frameSequencePlayers");
            players.ClearArray();
            if (backgroundPlayer != null)
            {
                players.InsertArrayElementAtIndex(0);
                players.GetArrayElementAtIndex(0).objectReferenceValue = backgroundPlayer;
            }
            waiterSo.ApplyModifiedPropertiesWithoutUndo();

            var contentSo = new SerializedObject(contentController);
            var roots = contentSo.FindProperty("contentRoots");
            roots.arraySize = 1;
            roots.GetArrayElementAtIndex(0).objectReferenceValue = content;
            contentSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, OutputPath);
        }

        static Camera PlaceCamera(GameDefinition gd, MainMenuConfig config, Transform parent)
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

            float ortho = config.baseConfig != null ? config.baseConfig.cameraOrthoSize : 0f;
            var background = config.baseConfig != null ? config.baseConfig.cameraBackgroundColor : Color.black;
            GeneratorHelper.ConfigureSceneCamera(camera, ortho, background);
            return camera;
        }

        static void PlaceLight(MainMenuConfig config, Transform parent)
        {
            var lighting = config.baseConfig;
            if (lighting != null && !lighting.useDirectionalLight)
                return;

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = lighting != null ? lighting.lightColor : Color.white;
            light.intensity = lighting != null ? lighting.lightIntensity : 1f;
        }

        static SpriteFramePlayer PlaceArt(Transform menuRoot, Transform content, MainMenuConfig config)
        {
            SpriteFramePlayer background = null;
            if (config.placedSprites == null)
                return null;

            foreach (var sprite in config.placedSprites)
            {
                if (sprite == null)
                    continue;

                bool behindContent = sprite.layer == SpriteLayer.Background1 || sprite.layer == SpriteLayer.Background2;
                var placed = PlacedSpriteBuilder.Spawn(behindContent ? menuRoot : content, sprite);
                if (behindContent && background == null)
                    background = placed.GetComponent<SpriteFramePlayer>();

                if (sprite.name == "MainMenu_Explode")
                    GeneratorHelper.AddAudioSource(placed, playOnAwake: false, loop: false, spatialBlend: 0f);
            }

            return background;
        }

        static GameObject PlaceCharacter(GameDefinition gd, MainMenuConfig config, Transform parent, Camera camera)
        {
            if (!config.placeCharacter)
                return null;

            var character = (GameObject)PrefabUtility.InstantiatePrefab(gd.player.playerPrefab, parent);
            character.name = "Character";
            character.transform.localPosition = new Vector3(config.characterPosition.x, config.characterPosition.y, 0f);

            var controller = character.GetComponentInChildren<SickOfIt.Core.Input.PlayerController>(true);
            if (controller == null)
                return character;

            var so = new SerializedObject(controller);
            if (camera != null)
                so.FindProperty("mainCamera").objectReferenceValue = camera;
            so.FindProperty("enableShooting").boolValue = config.characterCanShoot;
            so.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }

        static void PlaceHouseColliders(Transform content, MainMenuConfig config)
        {
            if (config.houseColliders == null || config.houseColliders.Count == 0)
                return;

            Transform houseArt = null;
            if (!string.IsNullOrEmpty(config.houseSpriteName))
                houseArt = content.Find(config.houseSpriteName);
            if (houseArt == null)
                return;

            var body = new GameObject("House");
            body.transform.SetParent(houseArt, false);
            body.transform.localPosition = config.houseBodyPosition;
            body.transform.localScale = config.houseBodyScale;
            foreach (var box in config.houseColliders)
                CreateBox(body.transform, box);
        }

        static void PlaceScreenBounds(Transform content, MainMenuConfig config)
        {
            if (config.screenBounds == null || config.screenBounds.Count == 0)
                return;

            var bounds = new GameObject("Boundaries");
            bounds.transform.SetParent(content, false);
            foreach (var box in config.screenBounds)
                CreateBox(bounds.transform, box);
        }

        static void CreateBox(Transform parent, MenuBoxConfig box)
        {
            if (box == null)
                return;

            var go = new GameObject(string.IsNullOrEmpty(box.name) ? "Box" : box.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(box.position.x, box.position.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, box.rotation);
            GeneratorHelper.SetLayer(go, "Boundary");

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = box.size;
            collider.offset = box.offset;
        }

        static GameObject PlaceMobileController(GameDefinition gd, Transform parent, Camera camera)
        {
            if (gd.levelUIPrefab == null)
                return null;

            var mobile = (GameObject)PrefabUtility.InstantiatePrefab(gd.levelUIPrefab, parent);
            mobile.name = "MobileController";
            foreach (var canvas in mobile.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.worldCamera = camera;
            }
            return mobile;
        }

        static List<GameObject> PlaceButtons(MainMenuConfig config, GameObject mobile)
        {
            var created = new List<GameObject>();
            if (mobile == null)
                return created;

            var safe = FindNamed(mobile.transform, "SafeAreaPanel");
            if (safe == null)
                safe = mobile.transform;

            var menuButtons = new GameObject("MenuButtons", typeof(RectTransform));
            menuButtons.transform.SetParent(safe, false);
            var rect = menuButtons.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(config.buttonSize.x, 900f);

            var layout = menuButtons.AddComponent<VerticalLayoutGroup>();
            layout.spacing = config.buttonSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var buttonPrefab = config.buttonPrefab != null
                ? config.buttonPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultButtonPath);

            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showContinueButton, "(Prb)ButtonContinue", "Continue");
            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showNewGameButton, "(Prb)ButtonNewGame", "New Game");
            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showLevelSelectButton, "(Prb)ButtonLevelSelect", "Level Select");
            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showVideosButton, "(Prb)ButtonVideos", "Videos");
            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showUpgradeShopButton, "(Prb)ButtonShop", "Shop");
            AddButton(created, buttonPrefab, menuButtons.transform, config, config.showQuitButton, "(Prb)ButtonQuit", "Quit");

            var indicator = AssetDatabase.LoadAssetAtPath<GameObject>(PreloadingIndicatorPath);
            if (indicator != null)
                PrefabUtility.InstantiatePrefab(indicator, menuButtons.transform);

            return created;
        }

        static void AddButton(List<GameObject> created, GameObject prefab, Transform parent, MainMenuConfig config, bool show, string objectName, string label)
        {
            if (!show || prefab == null)
                return;

            var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            button.name = objectName;
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = config.buttonSize;
            }

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
                    child.sizeDelta = config.buttonSize;
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
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(ButtonFontGuid));
            if (font != null)
                tmp.font = font;

            created.Add(button);
        }

        static BundleDownloadUI PlaceDownloadUi(MainMenuConfig config, GameObject mobile)
        {
            if (!config.showBundleDownloadUI || mobile == null)
                return null;

            var safe = FindNamed(mobile.transform, "SafeAreaPanel");
            if (safe == null)
                safe = mobile.transform;

            var root = new GameObject("Bundle Download Panel", typeof(RectTransform));
            root.transform.SetParent(safe, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(200f, -100f);
            rect.sizeDelta = new Vector2(500f, 100f);
            root.AddComponent<CanvasGroup>();

            var status = CreateHudText(root.transform, "Status", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(480f, 36f), 22f);
            var size = CreateHudText(root.transform, "Size", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 28f), 18f);

            var barGo = new GameObject("Progress", typeof(RectTransform));
            barGo.transform.SetParent(root.transform, false);
            var barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 1f);
            barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -58f);
            barRect.sizeDelta = new Vector2(300f, 30f);
            var image = barGo.AddComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = 0f;
            image.raycastTarget = false;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var ui = mobile.AddComponent<BundleDownloadUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("progressBarFill").objectReferenceValue = image;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("sizeText").objectReferenceValue = size;
            so.FindProperty("downloadUIRoot").objectReferenceValue = root;
            so.FindProperty("hideWhenFullyCached").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        static TextMeshProUGUI CreateHudText(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(ButtonFontGuid));
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        static void PlaceManagers(List<GameObject> buttons, out SceneLoadingManager loading, out BundleDownloadManager download)
        {
            var managers = new GameObject("Game Managers");
            managers.AddComponent<GameProgressManager>();
            managers.AddComponent<SceneTransitionManager>();

            new GameObject("Roguelite Manager").AddComponent<RogueliteManager>();
            new GameObject("Addressables Operation Manager").AddComponent<AddressablesOperationManager>();

            var loadingGo = new GameObject("Scene Loading Manager");
            loading = loadingGo.AddComponent<SceneLoadingManager>();
            var loadingSo = new SerializedObject(loading);
            var hide = loadingSo.FindProperty("objectsToDeactivateOnLoad");
            hide.ClearArray();
            for (int i = 0; i < buttons.Count; i++)
            {
                hide.InsertArrayElementAtIndex(i);
                hide.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }
            loadingSo.ApplyModifiedPropertiesWithoutUndo();

            var downloadGo = new GameObject("Bundle Download Manager");
            download = downloadGo.AddComponent<BundleDownloadManager>();
            var downloadSo = new SerializedObject(download);
            var frames = downloadSo.FindProperty("spriteFramesDataAssets");
            frames.ClearArray();
            var assets = AssetDatabase.FindAssets("t:SpriteFramesData");
            for (int i = 0; i < assets.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<SpriteFramesData>(AssetDatabase.GUIDToAssetPath(assets[i]));
                if (data == null)
                    continue;
                int index = frames.arraySize;
                frames.InsertArrayElementAtIndex(index);
                frames.GetArrayElementAtIndex(index).objectReferenceValue = data;
            }
            downloadSo.FindProperty("autoStartOnAwake").boolValue = true;
            downloadSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireMenu(GameObject mobile, MainMenuConfig config, List<GameObject> buttons, BundleDownloadManager downloadManager, SceneLoadingManager loading, GameObject character)
        {
            if (mobile == null)
                return;

            var menu = mobile.AddComponent<MainMenuManager>();
            var so = new SerializedObject(menu);

            AssignButton(so, "continueButton", buttons, "(Prb)ButtonContinue");
            AssignButton(so, "newGameButton", buttons, "(Prb)ButtonNewGame");
            AssignButton(so, "levelSelectButton", buttons, "(Prb)ButtonLevelSelect");
            AssignButton(so, "videosButton", buttons, "(Prb)ButtonVideos");
            AssignButton(so, "upgradeShopButton", buttons, "(Prb)ButtonShop");
            AssignButton(so, "quitButton", buttons, "(Prb)ButtonQuit");

            var continueButton = FindButton(buttons, "(Prb)ButtonContinue");
            if (continueButton != null)
            {
                var label = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
                so.FindProperty("continueButtonText").objectReferenceValue = label;
            }

            TextMeshProUGUI health = null;
            TextMeshProUGUI points = null;
            foreach (var tmp in mobile.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "CumulativeHealth")
                    health = tmp;
                else if (tmp.gameObject.name == "Points")
                    points = tmp;
            }
            if (health != null)
                so.FindProperty("cumulativeHealthText").objectReferenceValue = health;
            if (points != null)
                so.FindProperty("pointsText").objectReferenceValue = points;

            so.FindProperty("sceneLoadingManager").objectReferenceValue = loading;
            so.FindProperty("bundleDownloadManager").objectReferenceValue = downloadManager;

            so.FindProperty("hideDelay").floatValue = config.hideDelay;
            so.FindProperty("transitionDelay").floatValue = config.transitionDelay;

            var house = FindInLoadedScene(config.houseSpriteName);
            var hideList = so.FindProperty("objectsToHideOnStart");
            hideList.ClearArray();
            if (house != null)
            {
                hideList.InsertArrayElementAtIndex(0);
                hideList.GetArrayElementAtIndex(0).objectReferenceValue = house;
            }

            var explode = FindInLoadedScene("MainMenu_Explode");
            so.FindProperty("explodeRoot").objectReferenceValue = explode;
            if (explode != null)
            {
                var player = explode.GetComponent<SpriteFramePlayer>();
                if (player != null)
                {
                    var playerSo = new SerializedObject(player);
                    so.FindProperty("explodeFrames").objectReferenceValue = playerSo.FindProperty("data").objectReferenceValue;
                }
            }
            so.FindProperty("explodeAudio").objectReferenceValue = config.explodeAudio;

            so.ApplyModifiedPropertiesWithoutUndo();

            if (character == null)
                return;

            var controller = character.GetComponentInChildren<SickOfIt.Core.Input.PlayerController>(true);
            if (controller == null || mobile == null)
                return;

            MobileControllerFramework.Analog move = null;
            MobileControllerFramework.Analog fire = null;
            foreach (var analog in mobile.GetComponentsInChildren<MobileControllerFramework.Analog>(true))
            {
                if (analog.gameObject.name.Contains("Move"))
                    move = analog;
                else if (analog.gameObject.name.Contains("Fire"))
                    fire = analog;
            }

            var pcSo = new SerializedObject(controller);
            if (move != null)
                pcSo.FindProperty("moveAnalog").objectReferenceValue = move;
            if (fire != null)
                pcSo.FindProperty("fireAnalog").objectReferenceValue = fire;
            var input = mobile.GetComponentInChildren<SickOfIt.Core.Input.GameInputManager>(true);
            if (input != null)
                pcSo.FindProperty("gameInputManager").objectReferenceValue = input;
            pcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignButton(SerializedObject so, string property, List<GameObject> buttons, string objectName)
        {
            var button = FindButton(buttons, objectName);
            if (button == null)
                return;
            so.FindProperty(property).objectReferenceValue = button.GetComponent<MobileControllerFramework.Button>();
        }

        static GameObject FindButton(List<GameObject> buttons, string objectName)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null && buttons[i].name == objectName)
                    return buttons[i];
            }
            return null;
        }

        static GameObject FindInLoadedScene(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                    return transforms[i].gameObject;
            }
            return null;
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
    }
}
#endif
