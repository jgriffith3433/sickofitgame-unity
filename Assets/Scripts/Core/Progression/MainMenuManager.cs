using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Main Menu scene. The title, buttons, stickers and stats are built at runtime from
    /// mainMenuConfig.layout/labels and ui/theme.json (see <see cref="MenuCanvas"/>).
    /// No PlayableDirectors — transitions use simple coroutine-based fades.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        [Header("Bundle Download Manager")]
        [SerializeField] private BundleDownloadManager bundleDownloadManager;

        [Header("Transition")]
        [Tooltip("How long to wait before hiding the objects (in seconds)")]
        [SerializeField] private float hideDelay = 1.5f;
        [Tooltip("GameObjects to hide after Continue/New Game is pressed")]
        [SerializeField] private List<GameObject> objectsToHideOnStart;
        [Tooltip("Optional: idle animation objects to deactivate on transition")]
        [SerializeField] private List<GameObject> idleObjects;
        [Tooltip("Optional: transition animation objects to activate on transition")]
        [SerializeField] private List<GameObject> transitionObjects;
        [Tooltip("Delay before executing scene load after transition starts")]
        [SerializeField] private float transitionDelay = 1f;

        [Header("Explode Transition")]
        [Tooltip("House-explosion video. Played to the end before the next scene loads.")]
        [SerializeField] private GameObject explodeRoot;
        [SerializeField] private SpriteFramesData explodeFrames;
        [SerializeField] private AudioClip explodeAudio;

        private bool isTransitioning = false;
        private MenuCanvas menu;
        private AudioSource menuBed;
        private float menuOpenedAt = -1f;
        private float logoSlamDoneAt = -1f;
        private int heroSighs;

        /// <summary>Unscaled time the menu finished building (playtest).</summary>
        public float MenuOpenedAt => menuOpenedAt;

        /// <summary>Unscaled time the title slam finished (≤ opened + 0.5 s), or -1.</summary>
        public float LogoSlamDoneAt => logoSlamDoneAt;

        /// <summary>How many bored sighs the menu hero has played (playtest).</summary>
        public int HeroSighs => heroSighs;

        /// <summary>True once every visible main-menu button carries an illustrated plate (playtest).</summary>
        public bool ButtonsHavePlates { get; private set; }

        /// <summary>Main-menu-only plated button style (never written back into theme.json).</summary>
        MenuButtonStyle platedButton;

        private void Start()
        {
            // Analytics: the game is playable once the main menu is up.
            AnalyticsManager.Ready();
            AnalyticsManager.Screen("menu");
            AnalyticsManager.SetContext(new System.Collections.Generic.Dictionary<string, object>
                { { "level", null }, { "level_name", null }, { "floor", null }, { "phase", null }, { "boss", null }, { "video", null } });
            GameProgressManager.Instance.InitializeNewPlayer();
            // However the player came back, nothing of a level, video or screen stays loaded (GameAssets).
            SceneTransitionManager.ReleaseAll();

            if (explodeRoot != null)
                explodeRoot.SetActive(false);

            ApplyMenuConfig();
            BuildMenu();
            // Exactly one EventSystem: additive scenes and the Camera prefab each carry one; two alive makes clicks miss
            // Play / Continue (backlog f_9ee4fb228e7d). Ensure after the menu canvas builds, and prefer the scene's
            // already-wired Input System module (EventSystemGuard adopts it) so pointer/touch keep working.
            EventSystemGuard.Ensure();
            menuOpenedAt = Time.unscaledTime;
            // Title slam is theme.titleEntrance (0.5 s Pop); mark done when that duration elapses.
            float slam = 0.5f;
            var theme = GameDefinition.Instance != null ? GameDefinition.Instance.menuTheme : null;
            if (theme != null && theme.titleEntrance != null && theme.titleEntrance.duration > 0f)
                slam = Mathf.Min(0.5f, theme.titleEntrance.duration);
            logoSlamDoneAt = menuOpenedAt + slam;
            MenuLavaBackdrop.OverlordAppeared -= OnOverlordAppeared;
            MenuLavaBackdrop.OverlordAppeared += OnOverlordAppeared;
            StartCoroutine(HeroSighLoop());
            StartCoroutine(StartMenuBedAfterButtons());
            SetupBundleDownloadManager();
            // The ghost lobby (Docs/Story.md "Other climbers"): the menu's hero is a ghost on everyone else's menu.
            GhostLobby.MenuOpen = true;
            SickOfIt.Core.Addressables.BundleFetchLog.Freeze();
            // The Shaft, Yooba, shop and select-card art arrive behind the menu, never during its load (phone-load, GameDefinitionBoot).
            SickOfIt.Core.Addressables.GameAssets.LoadLateInBackground(LateCoreDelaySeconds);
        }

        private const float LateCoreDelaySeconds = 1.5f;

        private void OnDestroy()
        {
            MenuLavaBackdrop.OverlordAppeared -= OnOverlordAppeared;
            GhostLobby.MenuOpen = false;
            if (menuBed != null)
                menuBed.Stop();
            // A level's own load (started at the press) holds the same bundles, so this only lets go of the menu's share.
            SickOfIt.Core.Addressables.GameAssets.Release(MenuHeroOwner);
        }

        /// <summary>World X of the menu hero's feet, if any (MenuLavaBackdrop seam brightens under him).</summary>
        public static bool ActiveHeroX(out float x)
        {
            x = 0f;
            var menu = Object.FindAnyObjectByType<MainMenuManager>();
            if (menu == null) return false;
            var hero = menu.MenuHero != null ? menu.MenuHero : FindCharacter();
            if (hero == null) return false;
            x = hero.position.x;
            return true;
        }

        void OnOverlordAppeared()
        {
            // Fun-reviewer: the pass is a beat — he glances up with a sigh when the silhouette crosses.
            var hero = MenuHero != null ? MenuHero : FindCharacter();
            if (hero == null) return;
            heroSighs++;
            StartCoroutine(SighPulse(hero));
            Sfx.Play("lava_bubble"); // soft breath stand-in until a dedicated sigh clip lands
        }

        /// <summary>
        /// How many EventSystems are enabled right now (playtest: must be 1 while the menu is up).
        /// </summary>
        public static int EnabledEventSystemCount()
        {
            int n = 0;
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (es != null && es.enabled && es.gameObject.activeInHierarchy)
                    n++;
            return n;
        }

        IEnumerator HeroSighLoop()
        {
            // Bored idle on the ledge: every 10–16 s a quiet sigh (animation pulse + a soft UI-adjacent breath).
            // No new voice clip required: the stand-in / prefab keeps Idle, and we nudge scale so the sigh reads on camera.
            var wait = new WaitForSecondsRealtime(1f);
            float next = Time.unscaledTime + 8f;
            while (!isTransitioning)
            {
                yield return wait;
                if (Time.unscaledTime < next)
                    continue;
                next = Time.unscaledTime + 10f + UnityEngine.Random.Range(0f, 6f);
                var hero = MenuHero != null ? MenuHero : FindCharacter();
                if (hero == null)
                    continue;
                heroSighs++;
                StartCoroutine(SighPulse(hero));
                // Soft breath: reuse uiHover as a quiet stand-in if no dedicated sigh clip is wired yet.
                Sfx.Play(Sfx.Events.uiHover);
            }
        }

        static IEnumerator SighPulse(Transform hero)
        {
            if (hero == null) yield break;
            var graphics = hero.Find("Graphics");
            var t = graphics != null ? graphics : hero;
            var baseScale = t.localScale;
            float start = Time.unscaledTime;
            const float dur = 0.55f;
            while (Time.unscaledTime - start < dur && t != null)
            {
                float u = (Time.unscaledTime - start) / dur;
                float s = 1f + 0.04f * Mathf.Sin(u * Mathf.PI);
                t.localScale = new Vector3(baseScale.x * s, baseScale.y * (2f - s), baseScale.z);
                yield return null;
            }
            if (t != null)
                t.localScale = baseScale;
        }

        IEnumerator StartMenuBedAfterButtons()
        {
            // Bed and theme start after the buttons are up so the phone's 20 s load isn't blocked by music decode.
            yield return new WaitForSecondsRealtime(LateCoreDelaySeconds);
            if (isTransitioning || this == null)
                yield break;
            if (menuBed == null)
            {
                menuBed = gameObject.AddComponent<AudioSource>();
                menuBed.playOnAwake = false;
                menuBed.loop = true;
                menuBed.spatialBlend = 0f;
            }
            // Dedicated main theme (k_aea2f5751795) when landed as audio.json "menu_theme"; else the Landing's lava
            // bed so the diorama is never silent. Only call PlayLoop for an id that exists (avoids a [Sfx] warning).
            if (Sfx.ClipOf("menu_theme") != null || SoundExists("menu_theme"))
                Sfx.PlayLoop(menuBed, "menu_theme", 0.55f);
            else
                Sfx.PlayLoop(menuBed, "ambience_floor1000", 0.45f);
        }

        static bool SoundExists(string id)
        {
            var cfg = GameDefinition.Instance != null ? GameDefinition.Instance.audio : null;
            if (cfg == null || cfg.sounds == null || string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; i < cfg.sounds.Count; i++)
                if (cfg.sounds[i] != null && cfg.sounds[i].id == id)
                    return true;
            return false;
        }

        /// <summary>Deep-enough copy so plated sprites never leak into the shared theme (Export would otherwise rewrite theme.json).</summary>
        static MenuButtonStyle CloneButtonStyle(MenuButtonStyle src)
        {
            if (src == null) return null;
            var copy = src.Clone();
            if (src.surface != null)
            {
                copy.surface = new MenuSurfaceStyle
                {
                    shape = src.surface.shape,
                    sprite = src.surface.sprite,
                    cornerRadius = src.surface.cornerRadius,
                    fill = src.surface.fill,
                    borderColor = src.surface.borderColor,
                    borderWidth = src.surface.borderWidth,
                    shadowColor = src.surface.shadowColor,
                    shadowOffset = src.surface.shadowOffset,
                    padding = src.surface.padding,
                };
            }
            copy.hoverSprite = src.hoverSprite;
            copy.pressedSprite = src.pressedSprite;
            return copy;
        }

        // "N climbing now": other players on a level, from the ghost lobby (hidden offline, in the Editor and at 0).
        private TMPro.TextMeshProUGUI climbingChip;
        private int climbingShown = -1;
        private float climbingCheckAt;

        private void Update()
        {
            if (menu == null || Time.unscaledTime < climbingCheckAt)
                return;
            climbingCheckAt = Time.unscaledTime + 0.5f;
            bool offline = GhostLobby.Offline;
            int climbing = GhostLobby.Online ? GhostLobby.Climbing : 0;
            int key = offline ? -2 : climbing;
            if (key == climbingShown)
                return;
            climbingShown = key;
            if (!offline && climbing <= 0 && climbingChip == null)
                return;
            var labels = GameDefinition.Instance != null && GameDefinition.Instance.mainMenuConfig != null
                ? GameDefinition.Instance.mainMenuConfig.labels : null;
            string text = offline
                ? (labels != null && !string.IsNullOrEmpty(labels.climbingOffline) ? labels.climbingOffline : new MainMenuLabels().climbingOffline)
                : string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    labels != null && !string.IsNullOrEmpty(labels.climbingNow) ? labels.climbingNow : new MainMenuLabels().climbingNow,
                    climbing.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
            if (climbingChip == null)
                climbingChip = menu.AddChip(text);
            climbingChip.text = text;
            var chip = climbingChip.transform.parent;
            if (chip != null)
                chip.gameObject.SetActive(offline || climbing > 0);
        }

        private void ApplyMenuConfig()
        {
            var config = GameDefinition.Instance != null ? GameDefinition.Instance.mainMenuConfig : null;
            if (config == null)
                return;

            hideDelay = config.hideDelay;
            transitionDelay = config.transitionDelay;
            PlacedSpriteBuilder.Bind(config.placedSprites);

            // The first screen is the lava world (black, orange, cyan), not the old meadow: the meadow video and the house are
            // hidden, and the house's explosion with them (Play goes straight to the load, which has its own lava cover).
            var camera0 = Camera.main;
            MenuLavaBackdrop.Build(camera0 != null ? camera0.transform.parent : null);
            if (explodeRoot != null)
                explodeRoot.SetActive(false);
            explodeRoot = null;

            Transform character = null;
            if (config.placeCharacter)
                character = PlaceCharacter(config);
            else if (config.portraitFollowCharacter)
                character = FindCharacter();

            // Portrait phones: the menu fills the screen around the hero instead of a 16:9 strip between black bars.
            SickOfIt.Core.Device.AspectRatioController.CoverPortrait(Camera.main, config.portraitWidthShare, config.portraitFocusX,
                config.portraitFollowCharacter ? character : null);

            // The player build's scene has no hero (PlayerDataSplit takes him out so his raw sheets stay out of SickOfIt.data):
            // he walks in from the Addressables bundles as soon as they are in, and the portrait camera starts following him then.
            if (character == null && config.placeCharacter)
                StartCoroutine(SpawnMenuHero(config));
        }

        /// <summary>Who holds the menu hero's bundles (GameAssets owner); let go when the menu scene goes.</summary>
        private const string MenuHeroOwner = "menuHero";

        /// <summary>The hero the menu spawned (null while he is still on his way, or when the scene carries him).</summary>
        public Transform MenuHero { get; private set; }

        /// <summary>
        /// The hero's renderer on the menu, whoever placed him (the scene, the stand-in or the spawned prefab); null while he is not there.
        /// The phone-load playtest checks he is on screen, so removing him from the player build can't go unseen.
        /// </summary>
        public SpriteRenderer MenuHeroRenderer()
        {
            var hero = MenuHero != null ? MenuHero : FindCharacter();
            return hero != null ? hero.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        /// <summary>
        /// Puts the hero on the meadow the way MainMenuSceneGenerator.PlaceCharacter does: an instance of player.playerPrefab under the
        /// scene's content root, at characterPosition, with the menu's camera and shooting switch. In a player build the prefab is
        /// not in GameDefinition: only its own bundles (Character.prefab, its controller, clips and the Idle/Walk/Jump/Fall/Fire
        /// sheets: scope "hero.menu") load here, never the hero's whole scope, which waits for a level. They are ~20 MB, so until they
        /// are in he stands there as his Idle sheet from the core bundles (a <see cref="MenuHeroStandIn"/>): the menu never opens on an
        /// empty meadow, however slow the line.
        /// </summary>
        private IEnumerator SpawnMenuHero(MainMenuConfig config)
        {
            var gd = GameDefinition.Instance;
            if (gd == null || gd.player == null)
                yield break;
            var camera = Camera.main;
            var parent = camera != null ? camera.transform.parent : null; // the scene's "Content" root, where the camera sits too
            var position = new Vector3(config.characterPosition.x, config.characterPosition.y, 0f);

            GameObject standIn = null;
            if (gd.player.playerPrefab == null)
            {
                standIn = MenuHeroStandIn.Create(gd.player, parent, position);
                if (standIn != null)
                {
                    MenuHero = standIn.transform;
                    SickOfIt.Core.Device.AspectRatioController.CoverPortrait(camera, config.portraitWidthShare, config.portraitFocusX,
                        config.portraitFollowCharacter ? MenuHero : null);
                }
                if (SickOfIt.Core.Addressables.GameAssets.Knows(SickOfIt.Core.Addressables.GameAssets.MenuHero))
                    yield return SickOfIt.Core.Addressables.GameAssets.Acquire(MenuHeroOwner, new[] { SickOfIt.Core.Addressables.GameAssets.MenuHero });
            }
            var prefab = gd.player.playerPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("[MainMenu] No hero prefab to place on the menu" + (standIn != null ? "; the stand-in stays." : "."));
                yield break;
            }

            var hero = Instantiate(prefab, parent, false);
            hero.name = "Character";
            hero.transform.localPosition = position;
            var controller = hero.GetComponentInChildren<SickOfIt.Core.Input.PlayerController>(true);
            if (controller != null)
                controller.BindMenuHero(camera, config.characterCanShoot, gameObject);
            if (standIn != null)
                Destroy(standIn);
            MenuHero = hero.transform;
            SickOfIt.Core.Device.AspectRatioController.CoverPortrait(camera, config.portraitWidthShare, config.portraitFocusX,
                config.portraitFollowCharacter ? MenuHero : null);
        }

        /// <summary>
        /// The menu's hero before his prefab has downloaded: a SpriteRenderer set up like the prefab's Graphics child (scale, layer,
        /// facing) playing the Idle sheet from the "core" scope (player.spriteSheet), which the boot already loaded. No physics,
        /// no input, nothing saved; replaced by the real hero in place.
        /// </summary>
        private sealed class MenuHeroStandIn : MonoBehaviour
        {
            private Sprite[] frames;
            private SpriteRenderer sr;
            private float fps = 12f;

            public static GameObject Create(PlayerConfig player, Transform parent, Vector3 localPosition)
            {
                var gd = GameDefinition.Instance;
                var frames = player.spriteSheet != null && gd != null ? gd.SpritesOf(player.spriteSheet) : null;
                if (frames == null || frames.Length == 0)
                    return null;
                var root = new GameObject("Character");
                root.transform.SetParent(parent, false);
                root.transform.localPosition = localPosition;
                var graphics = new GameObject("Graphics");
                graphics.transform.SetParent(root.transform, false);
                float scale = player.spriteScale > 0f ? player.spriteScale : 0.25f;
                graphics.transform.localScale = new Vector3(scale, scale, scale);
                var renderer = graphics.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.flipX = true; // the prefab mirrors its Graphics (the sheets face the other way); flipX, not a negative scale
                SpriteLayers.Apply(renderer, SpriteLayer.Player, SpriteLayers.Character);
                var standIn = root.AddComponent<MenuHeroStandIn>();
                standIn.frames = frames;
                standIn.sr = renderer;
                var states = player.animationConfig != null ? player.animationConfig.states : null;
                if (states != null)
                {
                    foreach (var state in states)
                    {
                        if (state != null && state.stateName == "Idle" && state.fps > 0)
                            standIn.fps = state.fps;
                    }
                }
                return root;
            }

            private void Update()
            {
                if (sr != null && frames != null && frames.Length > 0)
                    sr.sprite = frames[Mathf.FloorToInt(Time.unscaledTime * fps) % frames.Length];
            }
        }

        private static Transform FindCharacter()
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null && transforms[i].name == "Character")
                    return transforms[i];
            return null;
        }

        private static Transform PlaceCharacter(MainMenuConfig config)
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null || transforms[i].name != "Character")
                    continue;
                var position = transforms[i].localPosition;
                position.x = config.characterPosition.x;
                position.y = config.characterPosition.y;
                transforms[i].localPosition = position;
                return transforms[i];
            }
            return null;
        }

        private void BuildMenu()
        {
            var gd = GameDefinition.Instance;
            var config = gd != null ? gd.mainMenuConfig : new MainMenuConfig();
            var labels = config.labels ?? new MainMenuLabels();
            // Illustrated lava plates on the main buttons only (a clone — never mutate theme.json's shared button).
            platedButton = null;
            if (gd != null && gd.menuTheme != null && gd.menuTheme.button != null)
            {
                platedButton = CloneButtonStyle(gd.menuTheme.button);
                MenuInkPlates.ApplyTo(platedButton);
            }
            menu = MenuCanvas.Create("MainMenuUI", gd != null ? gd.menuTheme : null, config.layout, gameObject);
            var list = menu.AddList(config.layout.list, menu.Theme.button.size);
            entries.Clear();

            // The menu only shows what the player has met (MenuReveal): a brand-new save sees Play, Settings and Rage Quit; the rest
            // arrives with the progress that earns it, each with a one-time NEW sticker.
            MenuReveal.BeginMenu();
            // Continue: the current run, where it left off. New Run: a fresh run from the highest Waystone reached
            // (Docs/Story.md, "Waystones and runs"). New Game: everything from the Meadow again. First run: just Play.
            int currentLevel = RunFlow.ContinueLevel();
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();
            bool firstRun = MenuReveal.IsFirstRun;
            if (firstRun)
            {
                Add(list, labels.play, OnNewGameClicked);
            }
            else
            {
                if (config.showContinueButton && currentLevel <= totalLevels)
                    Add(list, string.Format(labels.continueGame, currentLevel), OnContinueClicked);
                int waystone = WaystoneProgress.Highest;
                if (waystone > 0 && RunFlow.LevelFrom(waystone) > 0)
                    Add(list, string.Format(System.Globalization.CultureInfo.InvariantCulture, labels.newRun,
                        waystone.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), OnNewRunClicked);
                if (config.showNewGameButton)
                    Add(list, labels.newGame, OnNewGameClicked);
            }
            // The replay modes (Docs/Story.md, "Again"): each opens a card with its best / table / cycle and a Play button.
            var replay = gd != null ? gd.replay : null;
            if (replay != null && replay.enabled)
            {
                var replayLabels = replay.labels ?? new ReplayLabels();
                if (ReplayModes.NewGamePlusOpen && MenuReveal.Unlocked(MenuReveal.NewGamePlus))
                    Add(list, replayLabels.newGamePlus,
                        () => OpenReplay(SickOfIt.Gameplay.ReplayPanelKind.NewGamePlus, "new_game_plus"), MenuReveal.NewGamePlus);
                if (MenuReveal.Unlocked(MenuReveal.Endless))
                    Add(list, replayLabels.endless, () => OpenReplay(SickOfIt.Gameplay.ReplayPanelKind.Endless, "endless"), MenuReveal.Endless);
                if (MenuReveal.Unlocked(MenuReveal.Daily))
                    Add(list, replayLabels.daily, () => OpenReplay(SickOfIt.Gameplay.ReplayPanelKind.Daily, "daily"), MenuReveal.Daily);
            }
            if (config.showLevelSelectButton && MenuReveal.Unlocked(MenuReveal.LevelSelect))
                Add(list, labels.levelSelect, OnLevelSelectClicked, MenuReveal.LevelSelect);
            if (config.showVideosButton && MenuReveal.Unlocked(MenuReveal.Videos))
                Add(list, labels.videos, OnVideosClicked, MenuReveal.Videos);
            if (config.showUpgradeShopButton && MenuReveal.Unlocked(MenuReveal.Shop))
                Add(list, labels.shop, OnUpgradeShopClicked, MenuReveal.Shop);
            var codex = gd != null ? gd.codex : null;
            if (codex != null && codex.enabled && codex.showOnMainMenu && MenuReveal.Unlocked(MenuReveal.Codex))
                Add(list, codex.buttonLabel, () =>
                {
                    Picked("codex");
                    SickOfIt.Gameplay.CodexPanel.Show(menu, () => AnalyticsManager.Screen("menu"));
                    AnalyticsManager.Screen("codex");
                }, MenuReveal.Codex);
            if (config.showSettingsButton)
                Add(list, labels.settings, () =>
                {
                    Picked("settings");
                    SettingsPanel.Show(menu, () => AnalyticsManager.Screen("menu"));
                    AnalyticsManager.Screen("settings");
                });
            // Right after a rage quit from a death, the quit button dares you to do it again (even in a browser,
            // where it can only ask you to close the tab). Otherwise quitting is hidden in browsers.
            bool rageQuitAgain = PlayerPrefs.GetInt(DeathRuntime.RageQuitKey, 0) == 1;
            if (rageQuitAgain)
            {
                PlayerPrefs.DeleteKey(DeathRuntime.RageQuitKey);
                PlayerPrefs.Save();
            }
            if (config.showQuitButton && (rageQuitAgain || Application.platform != RuntimePlatform.WebGLPlayer))
                Add(list, rageQuitAgain ? labels.rageQuitAgain : labels.quit, OnQuitClicked);

            BuildPages(list);
            ButtonsHavePlates = MenuInkPlates.Ready;
            if (ButtonsHavePlates)
            {
                foreach (var button in menu.GetComponentsInChildren<MenuButton>(true))
                {
                    if (button != null && !button.HasIllustratedPlate)
                    {
                        ButtonsHavePlates = false;
                        break;
                    }
                }
            }

            if (config.showCumulativeHealth)
            {
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int health = RogueliteManager.Instance.GetCumulativeHealth(totalLevels);
                menu.AddChip(string.Format(menu.Theme.healthFormat, health, maxHealth));
            }
            if (config.showPointsDisplay)
                menu.AddChip(string.Format(menu.Theme.pointsFormat, RogueliteManager.Instance.GetPoints()));
        }

        /// <summary>
        /// Playtest setup shortcut: every button earned and seen (an old save's menu), the menu rebuilt. Scenarios that predate the
        /// staged menu use it to reach the shop or the pickers without playing for them; the staging has its own scenario.
        /// </summary>
        public void RevealEverythingForPlaytest()
        {
            MenuReveal.UnlockAll();
            RebuildMenu();
        }

        /// <summary>Playtest setup: builds the menu again from the save as it stands now (what opening the main menu would show).</summary>
        public void RebuildForPlaytest() => RebuildMenu();

        private void RebuildMenu()
        {
            if (menu != null)
                Destroy(menu.gameObject);
            menu = null;
            climbingChip = null;
            climbingShown = -1;
            stickers.Clear();
            BuildMenu();
            // A rebuild can leave a second EventSystem from an overlay; clicks must still hit Play / Continue.
            EventSystemGuard.Ensure();
        }

        /// <summary>Analytics: which main menu button was pressed.</summary>
        private static void Picked(string option) => AnalyticsManager.Choice("main_menu", option);

        // The buttons are queued and then split into pages (like the Codex's tabs) so the list never scrolls.
        private const int ButtonsPerPage = 6;
        private const string MoreLabel = "More >";
        private const string FirstPageLabel = "< Back";
        private sealed class Entry
        {
            public string Text;
            public Action OnClick;
            /// <summary>The <see cref="MenuReveal"/> id a button wears a NEW sticker for, or null.</summary>
            public string RevealId;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<List<GameObject>> pageSlots = new List<List<GameObject>>();
        private readonly List<string> pageLabels = new List<string>();

        private void Add(RectTransform list, string text, Action onClick, string revealId = null)
        {
            entries.Add(new Entry { Text = text, OnClick = onClick, RevealId = revealId });
        }

        /// <summary>The main menu's buttons on show, by their words (the playtest reads it; a button on another page counts).</summary>
        public List<string> ButtonLabels()
        {
            var labels = new List<string>();
            foreach (var entry in entries)
                labels.Add(entry.Text);
            return labels;
        }

        /// <summary>Ids of the buttons wearing a NEW sticker now.</summary>
        public List<string> NewStickerIds()
        {
            var ids = new List<string>();
            foreach (var pair in stickers)
                if (pair.Value != null)
                    ids.Add(pair.Key);
            return ids;
        }

        private readonly Dictionary<string, RectTransform> stickers = new Dictionary<string, RectTransform>();

        private void BuildPages(RectTransform list)
        {
            pageSlots.Clear();
            pageLabels.Clear();
            int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)ButtonsPerPage));
            for (int p = 0; p < pages; p++)
            {
                var slots = new List<GameObject>();
                for (int i = p * ButtonsPerPage; i < Mathf.Min(entries.Count, (p + 1) * ButtonsPerPage); i++)
                {
                    var entry = entries[i];
                    var press = entry.OnClick;
                    var revealId = entry.RevealId;
                    var button = menu.AddButton(list, entry.Text, platedButton ?? menu.Theme.button, () =>
                    {
                        if (revealId != null)
                        {
                            MenuReveal.Acknowledge(revealId);
                            stickers.Remove(revealId);
                        }
                        press();
                    });
                    slots.Add(button.transform.parent.gameObject);
                    pageLabels.Add(entry.Text + "|" + p);
                    if (revealId != null && MenuReveal.IsNew(revealId))
                        stickers[revealId] = MenuMarks.AddNewSticker(menu, button);
                }
                if (pages > 1)
                {
                    int target = (p + 1) % pages;
                    var nav = menu.AddButton(list, p + 1 < pages ? MoreLabel : FirstPageLabel, platedButton ?? menu.Theme.button, () => ShowPage(target));
                    slots.Add(nav.transform.parent.gameObject);
                }
                pageSlots.Add(slots);
            }
            ShowPage(0);
        }

        private void ShowPage(int index)
        {
            for (int p = 0; p < pageSlots.Count; p++)
                foreach (var slot in pageSlots[p])
                    if (slot != null) slot.SetActive(p == index);
        }

        /// <summary>Shows the page holding the button with this label (the playtest looks for buttons by their words).</summary>
        public void ShowPageHolding(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            foreach (var entry in pageLabels)
            {
                int bar = entry.LastIndexOf('|');
                if (string.Equals(entry.Substring(0, bar), label.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ShowPage(int.Parse(entry.Substring(bar + 1)));
                    return;
                }
            }
        }

        private void OnContinueClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            int level = RunFlow.ContinueLevel();
            Picked("continue");
            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(level);
                else
                    SceneTransitionManager.Instance.LoadLevel(level);
            }, level));
        }

        /// <summary>A new run from the highest Waystone: the current run's Grudges go; diamonds and Yooba's goods stay.</summary>
        private void OnNewRunClicked()
        {
            if (isTransitioning) return;
            int level = RunFlow.NewRun();
            if (level <= 0) return;
            isTransitioning = true;
            Picked("new_run");

            if (bundleDownloadManager != null)
                bundleDownloadManager.StartDownloadForLevel(level);
            else if (BundleDownloadManager.Instance != null)
                BundleDownloadManager.Instance.StartDownloadForLevel(level);

            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(level);
                else
                    SceneTransitionManager.Instance.LoadLevel(level);
            }, level));
        }

        /// <summary>Endless Floor, the Daily Challenge or New Game+: the card first (best, today's table, the cycle), then Play.</summary>
        private void OpenReplay(SickOfIt.Gameplay.ReplayPanelKind kind, string option)
        {
            if (isTransitioning) return;
            Picked(option);
            SickOfIt.Gameplay.ReplayPanel.Show(menu, kind, () => StartReplay(kind), () => AnalyticsManager.Screen("menu"));
            AnalyticsManager.Screen("replay");
        }

        private void StartReplay(SickOfIt.Gameplay.ReplayPanelKind kind)
        {
            if (isTransitioning) return;
            int level;
            switch (kind)
            {
                case SickOfIt.Gameplay.ReplayPanelKind.Endless: level = ReplayModes.LevelNumber(ReplayModes.EndlessLevelName); break;
                case SickOfIt.Gameplay.ReplayPanelKind.Daily: level = ReplayModes.LevelNumber(ReplayModes.DailyLevelName); break;
                default: level = RunFlow.NewGamePlus(); break;
            }
            if (level <= 0)
            {
                Debug.LogWarning($"[Replay] {kind}: no level to start.");
                return;
            }
            isTransitioning = true;
            Picked("play_" + kind.ToString().ToLowerInvariant());

            if (bundleDownloadManager != null)
                bundleDownloadManager.StartDownloadForLevel(level);
            else if (BundleDownloadManager.Instance != null)
                BundleDownloadManager.Instance.StartDownloadForLevel(level);

            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(level);
                else
                    SceneTransitionManager.Instance.LoadLevel(level);
            }, level));
        }

        private void OnNewGameClicked()
        {
            if (isTransitioning) return;
            // First five minutes: straight into play at Normal (Docs/Story.md "First five minutes"). Pick Your Hell lives in Settings.
            StartNewGame();
        }

        private void StartNewGame()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            Picked("new_game");
            RunFlow.Abandon();
            RogueliteManager.Instance.ResetAllRogueliteData();
            GameProgressManager.Instance.InitializeNewPlayer();
            FtueProgress.BeginNewGame();
            // The climb starts on tap: Floor 1000 (level 2) is the first thing played; the Meadow stays in Pick a Level.
            PlayerPrefs.SetInt("Level_1_Visited", 1);
            PlayerPrefs.SetInt("HighestLevelReached", 2);
            PlayerPrefs.SetInt("ReturnToLevel", 2);
            PlayerPrefs.Save();

            if (bundleDownloadManager != null)
                bundleDownloadManager.StartDownloadForLevel(2);
            else if (BundleDownloadManager.Instance != null)
                BundleDownloadManager.Instance.StartDownloadForLevel(2);

            // Straight into play (quality rule: the hook in the first 30 s is something happening, not a 60 s film):
            // the house explodes, then Level 1, whose opening is the intro's story told in the level (the sky opens over
            // the meadow, the imps drop in). Video1 is unlocked by InitializeNewPlayer and stays in Videos.
            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(2);
                else
                    SceneTransitionManager.Instance.LoadLevel(2);
            }, 2));
        }

        /// <summary>
        /// Play the house explosion through to the end, then execute the scene load.
        /// </summary>
        private IEnumerator TransitionThenExecute(Action onComplete, int level = 0)
        {
            if (menu != null)
                menu.PlayExit(0.35f);

            // The press: the low-priority warm-up stops and the level's art and sound come in at full speed while the house
            // explodes (the handles are the ones the level loads with, so nothing loads twice).
            // Press to level is a few seconds, not an explosion plus a load.
            SickOfIt.Core.Addressables.GameAssets.CancelWarm();
            if (level > 0 && SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.PreloadLevel(level);

            if (explodeRoot != null)
                explodeRoot.SetActive(true);

            SpriteFramePlayer player = null;
            if (explodeRoot != null && explodeFrames != null)
            {
                player = explodeRoot.GetComponent<SpriteFramePlayer>();
                if (player == null)
                    player = explodeRoot.AddComponent<SpriteFramePlayer>();

                var renderer = explodeRoot.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    SpriteLayers.Apply(renderer, SpriteLayer.Foreground2);

                player.UseGameClock(false);
                player.Initialize(explodeFrames, () => player.Play());
                player.SetLoop(false);
            }

            // The hero and the house stay on screen until the explosion can play: its frames can take seconds to
            // load on WebGL, and hiding them first left an empty meadow (and no hero) while the level loaded.
            if (player != null)
                yield return WaitForExplodeReady(player);

            if (idleObjects != null)
            {
                foreach (var obj in idleObjects)
                    if (obj != null) obj.SetActive(false);
            }

            if (transitionObjects != null)
            {
                foreach (var obj in transitionObjects)
                    if (obj != null) obj.SetActive(true);
            }

            if (objectsToHideOnStart != null)
            {
                foreach (var obj in objectsToHideOnStart)
                    if (obj != null) obj.SetActive(false);
            }

            var source = explodeRoot != null ? explodeRoot.GetComponent<AudioSource>() : null;
            if (source != null && explodeAudio != null)
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.loop = false;
                source.clip = explodeAudio;
                source.volume = 1f;
                if (explodeAudio.loadState != AudioDataLoadState.Loaded)
                    explodeAudio.LoadAudioData();
                source.Play();
            }

            if (player != null)
                yield return PlayExplodeUntilDone(player);
            else if (transitionDelay > 0)
                yield return new WaitForSeconds(transitionDelay);

            onComplete?.Invoke();
        }

        /// <summary>The most of the house explosion played before the level opens (Start to play inside 3 s: Downwell's drop).</summary>
        public const float ExplodeCapSeconds = 1.4f;

        private IEnumerator WaitForExplodeReady(SpriteFramePlayer player)
        {
            float waited = 0f;
            while (player != null && !player.IsReady && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator PlayExplodeUntilDone(SpriteFramePlayer player)
        {
            yield return WaitForExplodeReady(player);

            float duration = player != null ? player.Duration : 0f;
            if (explodeAudio != null)
                duration = Mathf.Max(duration, explodeAudio.length);
            if (duration <= 0f)
                duration = 4f;

            // The level is coming in behind it: the blast is cut to its first moments so Start never waits on the whole clip.
            duration = Mathf.Min(duration, ExplodeCapSeconds);
            float played = 0f;
            while (played < duration + 0.15f)
            {
                played += Time.deltaTime;
                bool framesDone = player != null && player.IsReady && player.TotalFrames > 0 && player.Progress >= 0.99f;
                bool audioDone = explodeAudio == null || played >= explodeAudio.length - 0.05f;
                if (framesDone && audioDone)
                    break;
                yield return null;
            }
        }

        private void OnLevelSelectClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("level_select");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        private void OnVideosClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("videos");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadVideoSelection();
        }

        private void OnQuitClicked()
        {
            Picked("quit");
            SceneTransitionManager.Instance.QuitGame();
            if (Application.platform == RuntimePlatform.WebGLPlayer && menu != null && !quitHintShown)
            {
                // Browsers usually refuse to let a page close its own tab.
                quitHintShown = true;
                var labels = GameDefinition.Instance != null ? GameDefinition.Instance.mainMenuConfig.labels : new MainMenuLabels();
                menu.AddText(menu.Root, "QuitHint", labels.browserQuitHint, menu.Theme.subtitle, new Vector2(0.5f, 0.08f), Vector2.zero,
                    new Vector2(1500f, 70f), out _, 34f, null, menu.Theme.entrance);
            }
        }

        private bool quitHintShown;

        private void OnUpgradeShopClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("shop");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadUpgradeShop();
        }

        private void SetupBundleDownloadManager()
        {
            if (bundleDownloadManager == null)
                bundleDownloadManager = FindObjectOfType<BundleDownloadManager>();

            if (bundleDownloadManager != null)
            {
                if (bundleDownloadManager.IsDownloading)
                    DisableAllButtons();
                bundleDownloadManager.OnDownloadComplete.AddListener(OnBundleDownloadComplete);
            }
        }

        private void OnBundleDownloadComplete() => EnableAllButtons();

        private void DisableAllButtons()
        {
            if (menu != null)
                menu.SetInteractable(false);
        }

        private void EnableAllButtons()
        {
            if (menu != null && !isTransitioning)
                menu.SetInteractable(true);
        }
    }
}
