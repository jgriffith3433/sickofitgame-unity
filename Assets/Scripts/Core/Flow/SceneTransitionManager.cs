using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Runtime.InteropServices;
using SickOfIt.Core.Addressables;
using SickOfIt.Data;

namespace SickOfIt.Core.Flow
{
    /// <summary>
    /// Handles scene loading and transitions
    /// Supports both traditional scene loading and Addressables for optimized WebGL builds
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CloseWindow();
#endif

        [Header("Loading Mode")]
        [SerializeField] private bool useAddressables = true; // Set to true for optimized WebGL builds
        private static SceneTransitionManager _instance;
        public static SceneTransitionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SceneTransitionManager");
                    _instance = go.AddComponent<SceneTransitionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Scene name constants (you can modify these to match your actual scene names)
        public const string MAIN_MENU_SCENE = "MainMenu";
        public const string LEVEL_SELECTION_SCENE = "LevelSelection";
        public const string VIDEO_SELECTION_SCENE = "VideoSelection";
        public const string UPGRADE_SHOP_SCENE = "UpgradeShop";

        public void LoadMainMenu()
        {
            // Back at the menu nothing of a level, video or death is needed (GameAssets keeps only "core").
            loadSerial++; // a level, video or death still on its way is replaced by the menu
            ReleaseAll();
            // Always load MainMenu normally (not via Addressables) as it's part of the base build
            SceneManager.LoadScene(MAIN_MENU_SCENE);
        }

        /// <summary>Lets go of every level, hero, video, death and screen asset (the main menu).</summary>
        public static void ReleaseAll()
        {
            foreach (string owner in new[] { "level", "hero", "video", "death", "screen:shaft", "screen:yooba", "screen:codex" })
                GameAssets.Release(owner);
        }

        /// <summary>Again! after a death: the same level, its assets still held, so no cover and no cleanup passes in front of play.</summary>
        public void RetryLevel(int levelNumber) => LoadLevel(levelNumber, true);

        public void LoadLevel(int levelNumber) => LoadLevel(levelNumber, false);

        const string LevelWarmOwner = "levelNext";
        int warmedLevel = -1;

        /// <summary>
        /// Past a floor's midpoint (the phase event, no polling): brings the next floor's bundles in behind the play, on phones only
        /// (elsewhere they come with the load). Held by its own owner so the current floor keeps its art; <see cref="LoadLevel"/> takes
        /// it over (the level owner's acquire finds the bundles in) and lets this hold go, which releases nothing the new floor uses.
        /// </summary>
        public void WarmNextLevel(int levelNumber)
        {
            if (!useAddressables || !GameAssets.Managed || levelNumber <= 0 || warmedLevel == levelNumber)
                return;
            if (!SickOfIt.Core.Device.TouchDevice.IsPrimary)
                return;
            warmedLevel = levelNumber;
            Debug.Log($"[Warm] level {levelNumber}'s bundles start in behind the play");
            StartCoroutine(GameAssets.Acquire(LevelWarmOwner, LevelScopes(levelNumber)));
        }

        /// <summary>The warmed floor's bundles are all in (for the playtest).</summary>
        public bool NextLevelWarm(int levelNumber) => warmedLevel == levelNumber && AllLoaded(LevelScopes(levelNumber));

        void LoadLevel(int levelNumber, bool fast)
        {
            // Store level number for LevelRuntime to read on scene load
            PlayerPrefs.SetInt("CurrentLevelNumber", levelNumber);
            PlayerPrefs.Save();

            string sceneName = "Level";

            // Set current scene for memory management
            if (useAddressables)
            {
                AddressablesOperationManager.Instance.SetCurrentScene(sceneName);
            }

            if (useAddressables)
            {
                // The level's art and sound (and the hero's) first; a video's or death's go once they are in.
                var level = GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(levelNumber) : null;
                var wanted = new System.Collections.Generic.List<string>();
                if (level != null)
                    wanted.Add(GameAssets.Level(level.levelName));
                if (!string.IsNullOrEmpty(GameAssets.PendingLevelScene))
                    wanted.Add(GameAssets.Scene(GameAssets.PendingLevelScene));
                Prepared(sceneName, "hero", new[] { GameAssets.Hero }, "level", wanted.ToArray(), new[] { "video", "death", LevelWarmOwner }, fast);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
            warmedLevel = -1;
        }

        Coroutine preload;
        bool preloadCancelled;

        public static string[] LevelScopes(int levelNumber)
        {
            var level = GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(levelNumber) : null;
            var wanted = new System.Collections.Generic.List<string>();
            if (level != null)
                wanted.Add(GameAssets.Level(level.levelName));
            if (!string.IsNullOrEmpty(GameAssets.PendingLevelScene))
                wanted.Add(GameAssets.Scene(GameAssets.PendingLevelScene));
            return wanted.ToArray();
        }

        /// <summary>The hero's and the level's bundles are in, so <see cref="LoadLevel"/> needs no loading screen.</summary>
        public bool LevelAssetsReady(int levelNumber)
        {
            if (!GameAssets.Managed) return true;
            // LoadLevel shows a loading cover while the late core is out, so it counts as part of "ready".
            if (!GameAssets.LateReady) return false;
            if (!GameAssets.IsLoaded(GameAssets.Hero)) return false;
            foreach (string scope in LevelScopes(levelNumber))
            {
                if (!GameAssets.IsLoaded(scope)) return false;
            }
            return true;
        }

        /// <summary>
        /// Starts bringing a level's bundles in behind whatever is on screen (the intro), so leaving it only has to
        /// activate the level. No cover, no bar. Safe to call twice; <see cref="CancelPreload"/> stops following it.
        /// </summary>
        public void PreloadLevel(int levelNumber)
        {
            if (!GameAssets.Managed || preload != null || LevelAssetsReady(levelNumber)) return;
            preloadCancelled = false;
            preload = StartCoroutine(Preloading(levelNumber));
        }

        public void CancelPreload() => preloadCancelled = true;

        IEnumerator Preloading(int levelNumber)
        {
            var late = GameAssets.EnsureLate();
            while (!preloadCancelled && late.MoveNext()) yield return late.Current;
            var hero = GameAssets.Acquire("hero", new[] { GameAssets.Hero });
            while (!preloadCancelled && hero.MoveNext()) yield return hero.Current;
            var level = GameAssets.Acquire("level", LevelScopes(levelNumber));
            while (!preloadCancelled && level.MoveNext()) yield return level.Current;
            preload = null;
        }

        /// <summary>
        /// Loads an Addressables scene once its assets are in (GameAssets): each (owner, scopes) acquired, then the owners in
        /// <paramref name="release"/> let go. A build without managed assets loads it at once.
        /// </summary>
        void Prepared(string sceneName, string firstOwner, string[] firstScopes, string secondOwner, string[] secondScopes, string[] release, bool fast = false)
        {
            // Single flight: this request is now the newest, so any older one still bringing its assets in stops (it would
            // release owners and ask the loader for a second scene a moment after this one: the Level <-> Death reload loop).
            int serial = ++loadSerial;
            if (!GameAssets.Managed)
            {
                AddressablesSceneLoader.Instance.LoadScene(sceneName, null, fast);
                return;
            }
            StartCoroutine(PrepareThenLoad(sceneName, serial, firstOwner, firstScopes, secondOwner, secondScopes, release, fast));
        }

        /// <summary>The newest scene request's number. A request whose number is no longer this one has been replaced and does nothing more.</summary>
        int loadSerial;

        bool Superseded(int serial) => serial != loadSerial;

        /// <summary>True while the Shaft screen is up (set by ShaftScreen, which lives in the Progression assembly that depends on Core).</summary>
        public static bool ShaftCovering;

        /// <summary>The loading cover and bar, except under the Shaft's drop: that animation is the cover then (it closes when the scene is in).</summary>
        static void Cover(float progress)
        {
            if (ShaftCovering)
                return;
            SickOfIt.Core.UI.Menus.ScreenTransition.Loading(progress);
        }

        static bool AllLoaded(string[] scopes)
        {
            if (scopes == null) return true;
            foreach (string scope in scopes)
            {
                if (!GameAssets.IsLoaded(scope)) return false;
            }
            return true;
        }

        IEnumerator PrepareThenLoad(string sceneName, int serial, string firstOwner, string[] firstScopes, string secondOwner, string[] secondScopes, string[] release, bool fast = false)
        {
            // The screen goes black while the assets come in, with a bar: the first owner's share is the first half
            // (the hero, usually in already), the second's the rest.
            bool both = firstOwner != null && secondOwner != null;
            // The menu's low-priority warm-up stops asking; this takes over at full speed (the bundles it fetched are in the cache).
            GameAssets.CancelWarm();
            // The art of the screens after the menu normally came in behind it long ago; if not, it is first.
            if (!GameAssets.LateReady)
            {
                Cover(0f);
                yield return GameAssets.EnsureLate(p => Cover(p * 0.3f));
                if (Superseded(serial)) yield break;
            }
            // Everything already in (the intro preloaded it): no cover, no bar.
            bool inAlready = GameAssets.Managed && AllLoaded(firstScopes) && AllLoaded(secondScopes);
            if (inAlready)
            {
                if (firstOwner != null) yield return GameAssets.Acquire(firstOwner, firstScopes);
                if (secondOwner != null) yield return GameAssets.Acquire(secondOwner, secondScopes);
                if (Superseded(serial)) yield break;
                foreach (string owner in release)
                    GameAssets.Release(owner);
                AddressablesSceneLoader.Instance.LoadScene(sceneName, null, fast);
                yield break;
            }
            Cover(0f);
            if (firstOwner != null)
            {
                yield return GameAssets.Acquire(firstOwner, firstScopes, p => Cover(both ? p * 0.3f : p));
                if (Superseded(serial)) yield break;
            }
            if (secondOwner != null)
            {
                yield return GameAssets.Acquire(secondOwner, secondScopes, p => Cover(both ? 0.3f + p * 0.7f : p));
                if (Superseded(serial)) yield break;
            }
            Cover(1f);
            foreach (string owner in release)
                GameAssets.Release(owner);
            AddressablesSceneLoader.Instance.LoadScene(sceneName);
        }

        /// <summary>Loads a base-build screen that reads the late core's art (select cards, shop) once it is in.</summary>
        void LoadAfterLate(string sceneName)
        {
            loadSerial++; // a level, video or death still on its way is replaced by this screen
            if (GameAssets.LateReady)
                SceneManager.LoadScene(sceneName);
            else
                StartCoroutine(LateThenLoad(sceneName, loadSerial));
        }

        IEnumerator LateThenLoad(string sceneName, int serial)
        {
            Cover(0f);
            yield return GameAssets.EnsureLate(p => Cover(p));
            if (Superseded(serial)) yield break;
            Cover(1f);
            SceneManager.LoadScene(sceneName);
        }

        public void LoadLevelSelection()
        {
            // Always load LevelSelection normally (not via Addressables) as it's part of the base build
            LoadAfterLate(LEVEL_SELECTION_SCENE);
        }

        public void LoadVideoSelection()
        {
            // Always load VideoSelection normally (not via Addressables) as it's part of the base build
            LoadAfterLate(VIDEO_SELECTION_SCENE);
        }

        public void LoadUpgradeShop()
        {
            // Always load UpgradeShop normally (not via Addressables) as it's part of the base build
            LoadAfterLate(UPGRADE_SHOP_SCENE);
        }

        const string VideoPreloadOwner = "videoPreload";

        /// <summary>
        /// Starts bringing in a video's art now, while something else is on screen (New Game's house explosion), so the
        /// intro doesn't open on a black loading cover on a phone. <see cref="LoadVideo"/> takes it over (its own acquire
        /// finds the bundles in or arriving) and lets this hold go. Nothing to do in the editor or without a manifest.
        /// </summary>
        public void PreloadVideo(int videoNumber)
        {
            if (!useAddressables || !GameAssets.Managed)
                return;
            var video = GameDefinition.Instance != null ? GameDefinition.Instance.GetVideo(videoNumber) : null;
            if (video == null)
                return;
            StartCoroutine(GameAssets.Acquire(VideoPreloadOwner, new[] { GameAssets.Video(video.videoName) }));
        }

        public void LoadVideo(int videoNumber)
        {
            // Store video number for VideoRuntime to read on scene load
            PlayerPrefs.SetInt("CurrentVideoNumber", videoNumber);
            PlayerPrefs.Save();

            string sceneName = "Video";

            // Set current scene for memory management
            if (useAddressables)
            {
                AddressablesOperationManager.Instance.SetCurrentScene(sceneName);
            }

            if (useAddressables)
            {
                // The video's own; the level and death before it go (the hero stays for the next level).
                var video = GameDefinition.Instance != null ? GameDefinition.Instance.GetVideo(videoNumber) : null;
                Prepared(sceneName, "video", video != null ? new[] { GameAssets.Video(video.videoName) } : new string[0], null, null, new[] { "level", "death", VideoPreloadOwner });
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        public void LoadDeathScene(string deathSceneName)
        {
            // Store death config name for DeathRuntime to read
            PlayerPrefs.SetString("CurrentDeathConfig", deathSceneName);
            PlayerPrefs.Save();

            string sceneName = "Death";
            
            // Set current scene for memory management
            if (useAddressables)
            {
                AddressablesOperationManager.Instance.SetCurrentScene(sceneName);
            }

            if (useAddressables)
            {
                // The death's own; the level stays (the Shaft and the next try come after).
                Prepared(sceneName, "death", new[] { GameAssets.Death(deathSceneName) }, null, null, new string[0]);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        public void ContinueGame()
        {
            int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
            LoadLevel(currentLevel);
        }

        public void QuitGame()
        {
            StartCoroutine(QuitGameCoroutine());
        }

        private IEnumerator QuitGameCoroutine()
        {
            // Exit fullscreen before quitting
            Screen.fullScreen = false;

            // Wait briefly for fullscreen transition
            yield return new WaitForSeconds(0.5f);

#if UNITY_WEBGL && !UNITY_EDITOR
            // Close browser window/tab for WebGL builds using modern JavaScript interop
            CloseWindow();
#endif

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
