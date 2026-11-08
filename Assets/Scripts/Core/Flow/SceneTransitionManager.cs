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

        public void LoadLevel(int levelNumber)
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
                Prepared(sceneName, "hero", new[] { GameAssets.Hero }, "level", wanted.ToArray(), new[] { "video", "death" });
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        Coroutine preload;
        bool preloadCancelled;

        static string[] LevelScopes(int levelNumber)
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
        void Prepared(string sceneName, string firstOwner, string[] firstScopes, string secondOwner, string[] secondScopes, string[] release)
        {
            if (!GameAssets.Managed)
            {
                AddressablesSceneLoader.Instance.LoadScene(sceneName);
                return;
            }
            StartCoroutine(PrepareThenLoad(sceneName, firstOwner, firstScopes, secondOwner, secondScopes, release));
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

        IEnumerator PrepareThenLoad(string sceneName, string firstOwner, string[] firstScopes, string secondOwner, string[] secondScopes, string[] release)
        {
            // The screen goes black while the assets come in, with a bar: the first owner's share is the first half
            // (the hero, usually in already), the second's the rest.
            bool both = firstOwner != null && secondOwner != null;
            // The art of the screens after the menu normally came in behind it long ago; if not, it is first.
            if (!GameAssets.LateReady)
            {
                SickOfIt.Core.UI.Menus.ScreenTransition.Loading(0f);
                yield return GameAssets.EnsureLate(p => SickOfIt.Core.UI.Menus.ScreenTransition.Loading(p * 0.3f));
            }
            // Everything already in (the intro preloaded it): no cover, no bar.
            bool inAlready = GameAssets.Managed && AllLoaded(firstScopes) && AllLoaded(secondScopes);
            if (inAlready)
            {
                if (firstOwner != null) yield return GameAssets.Acquire(firstOwner, firstScopes);
                if (secondOwner != null) yield return GameAssets.Acquire(secondOwner, secondScopes);
                foreach (string owner in release)
                    GameAssets.Release(owner);
                AddressablesSceneLoader.Instance.LoadScene(sceneName);
                yield break;
            }
            SickOfIt.Core.UI.Menus.ScreenTransition.Loading(0f);
            if (firstOwner != null)
                yield return GameAssets.Acquire(firstOwner, firstScopes, p => SickOfIt.Core.UI.Menus.ScreenTransition.Loading(both ? p * 0.3f : p));
            if (secondOwner != null)
                yield return GameAssets.Acquire(secondOwner, secondScopes, p => SickOfIt.Core.UI.Menus.ScreenTransition.Loading(both ? 0.3f + p * 0.7f : p));
            SickOfIt.Core.UI.Menus.ScreenTransition.Loading(1f);
            foreach (string owner in release)
                GameAssets.Release(owner);
            AddressablesSceneLoader.Instance.LoadScene(sceneName);
        }

        /// <summary>Loads a base-build screen that reads the late core's art (select cards, shop) once it is in.</summary>
        void LoadAfterLate(string sceneName)
        {
            if (GameAssets.LateReady)
                SceneManager.LoadScene(sceneName);
            else
                StartCoroutine(LateThenLoad(sceneName));
        }

        IEnumerator LateThenLoad(string sceneName)
        {
            SickOfIt.Core.UI.Menus.ScreenTransition.Loading(0f);
            yield return GameAssets.EnsureLate(p => SickOfIt.Core.UI.Menus.ScreenTransition.Loading(p));
            SickOfIt.Core.UI.Menus.ScreenTransition.Loading(1f);
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
                Prepared(sceneName, "video", video != null ? new[] { GameAssets.Video(video.videoName) } : new string[0], null, null, new[] { "level", "death" });
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
