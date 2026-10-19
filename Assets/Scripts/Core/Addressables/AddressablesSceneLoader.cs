using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Flow;

namespace SickOfIt.Core.Addressables
{
    public class AddressablesSceneLoader : MonoBehaviour
    {
        private static AddressablesSceneLoader _instance;
        public static AddressablesSceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("AddressablesSceneLoader");
                    _instance = go.AddComponent<AddressablesSceneLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Loading UI")]
        [SerializeField] private GameObject loadingScreenPrefab;

        [Header("Scene Tracking")]
        [SerializeField] private List<string> loadedScenes = new List<string>();
        [SerializeField] private string currentGameplayScene = "";

        private GameObject currentLoadingScreen;
        private bool isLoading = false;

        // Base scenes that should be unloaded when entering gameplay
        private static readonly string[] BASE_SCENES = { "MainMenu", "LevelSelection", "VideoSelection" };

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

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        /// <summary>
        /// Called when any scene is unloaded. Handles two cases:
        /// 1. Force-unload: SceneManager.LoadScene(Single) destroyed a preloaded additive scene.
        ///    The Addressables handle is still in activeSceneHandles, so we release it.
        /// 2. Intentional unload: Our UnloadSceneAsync already released the handle via
        ///    AddressablesOperationManager, so activeSceneHandles no longer contains it.
        ///    We just clean up our local tracking.
        /// </summary>
        private void OnSceneUnloaded(Scene unloadedScene)
        {
            string sceneName = unloadedScene.name;

            if (loadedScenes.Remove(sceneName))
            {
                // If the Addressables handle is still tracked, this was a force-unload.
                // Our intentional UnloadSceneAsync path removes the handle before the scene
                // finishes unloading, so it won't be present here for intentional unloads.
                if (AddressablesOperationManager.Instance.IsSceneLoadedThroughAddressables(sceneName))
                {
                    AddressablesOperationManager.Instance.ReleaseSceneHandle(sceneName);
                }
            }

            // Clear currentGameplayScene if it was destroyed out from under us
            if (currentGameplayScene == sceneName)
            {
                currentGameplayScene = "";
            }
        }

        private void Start()
        {
            // Initialize currentGameplayScene if empty (for editor testing)
            if (string.IsNullOrEmpty(currentGameplayScene))
            {
                Scene activeScene = SceneManager.GetActiveScene();
                // Only set for gameplay scenes, not base menu scenes
                if (activeScene.isLoaded && !System.Array.Exists(BASE_SCENES, s => s == activeScene.name))
                {
                    currentGameplayScene = activeScene.name;
                    if (!loadedScenes.Contains(activeScene.name))
                    {
                        loadedScenes.Add(activeScene.name);
                    }
                }
            }
        }

        /// <summary>
        /// Load and activate a scene, unloading the previous gameplay scene
        /// </summary>
        public void LoadScene(string sceneName, System.Action onComplete = null)
        {
            if (isLoading)
            {
                Debug.LogWarning($"Already loading a scene. Rejecting request for: {sceneName}");
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneName, true, onComplete));
        }

        public void LoadLevel(int levelNumber, System.Action onComplete = null)
        {
            LoadScene($"Level{levelNumber}", onComplete);
        }

        public void LoadVideo(int videoNumber, System.Action onComplete = null)
        {
            LoadScene($"Video{videoNumber}", onComplete);
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool showLoadingUI, System.Action onComplete)
        {
            isLoading = true;
            bool shouldHideLoadingScreen = false;

            try
            {
                // Load scene with loading UI
                if (showLoadingUI)
                {
                    ShowLoadingScreen();
                    shouldHideLoadingScreen = true;
                }

                // ---------------------------------------------------------------
                // PRE-RELEASE: Free old scene's textures BEFORE loading the new
                // scene.  On WebGL the new scene's bundle + old scene's textures
                // can exceed the 2 GB WASM limit if both are in memory at the same
                // time.  The loading screen is already visible, so the visual
                // impact of releasing textures early is hidden.
                // ---------------------------------------------------------------
                // A reload (Restart: "Level" while "Level" is current) must also unload the old copy. Treated as "no
                // previous scene", it loaded a second "Level" next to the first, GetSceneByName found the old one, and
                // the old level went on running ("[SceneContent] Already active, skipping").
                // The same goes for a copy this loader didn't load (opened in the editor, loaded by a test).
                if (string.IsNullOrEmpty(currentGameplayScene) && SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    currentGameplayScene = sceneName;
                    if (!loadedScenes.Contains(sceneName))
                        loadedScenes.Add(sceneName);
                }
                bool reload = !string.IsNullOrEmpty(currentGameplayScene) && currentGameplayScene == sceneName;
                bool hadPreviousScene = !string.IsNullOrEmpty(currentGameplayScene);
                if (hadPreviousScene)
                {
                    Scene previousScene = SceneManager.GetSceneByName(currentGameplayScene);
                    if (previousScene.isLoaded)
                    {
                        // CRITICAL: Release all IFrameSequencePlayer resources BEFORE new scene load.
                        // SpriteFramePlayer creates Sprite objects that reference Addressable Texture2Ds.
                        // If we rely on OnDestroy() → Destroy(sprite), destruction is deferred to end of frame,
                        // meaning sprites still hold texture references when Resources.UnloadUnusedAssets() runs.
                        // ReleaseAllResources() uses DestroyImmediate to sever references immediately.
                        ReleaseFrameSequencePlayerResources(previousScene);

                        // Stop and destroy all PlayableDirector playable graphs so that
                        // PlayableBehaviour native allocations (mixer state, etc.) are freed
                        // before the new scene loads.
                        DestroyPlayableDirectorGraphs(previousScene);

                        // Hide previous scene's content (behind loading screen anyway)
                        DeactivateSceneContent(previousScene);
                    }

                    // Release tracked asset handles (audio clips, prefabs, etc.) for the outgoing scene.
                    AddressablesOperationManager.Instance.ReleaseAllSceneAssetHandles(currentGameplayScene);
                    AddressablesOperationManager.Instance.ReleaseAllSceneAssetHandles("");

                    // Cancel in-flight label-load tracking coroutines on AddressablesOperationManager.
                    // Their chunk handles were already released by ReleaseAllChunks, but the
                    // coroutine state-machines still hold stale AsyncOperationHandle copies
                    // that can prevent the GC from collecting the underlying operation objects.
                    AddressablesOperationManager.Instance.CancelAllLabelLoads();

                    // --- First cleanup pass ---
                    yield return Resources.UnloadUnusedAssets();
                    System.GC.Collect();

                    // Yield a frame so the engine can process deferred native deallocations
                    // (texture upload buffers, GL resource cleanup, bundle unload finalization).
                    yield return null;

                    // --- Second cleanup pass ---
                    // Catches assets that only became unused after the first pass freed their
                    // dependants (e.g., materials no longer referencing destroyed textures).
                    yield return Resources.UnloadUnusedAssets();
                    System.GC.Collect();
                }

                // A reload unloads the old copy first, so only one scene by that name is ever loaded. An empty holder
                // scene stays loaded meanwhile (Unity can't unload the last loaded scene).
                Scene holder = default;
                if (reload)
                {
                    holder = SceneManager.CreateScene($"Reloading {sceneName} {Time.frameCount}");
                    SceneManager.SetActiveScene(holder);
                    yield return StartCoroutine(UnloadSceneAsync(sceneName));
                    yield return Resources.UnloadUnusedAssets();
                    System.GC.Collect();
                }

                // ---------------------------------------------------------------
                // LOAD: Load the new scene additively (old scene is still loaded
                // but its heavy texture assets have been freed above).
                // ---------------------------------------------------------------
                bool loadCompleted = false;
                bool loadSuccess = false;
                float currentProgress = 0f;

                AddressablesOperationManager.Instance.LoadScene(sceneName, (success) =>
                {
                    loadSuccess = success;
                    loadCompleted = true;
                }, LoadSceneMode.Additive);

                while (!loadCompleted)
                {
                    float progress = AddressablesOperationManager.Instance.GetCurrentSceneLoadProgress(sceneName);
                    if (progress != currentProgress)
                    {
                        currentProgress = progress;
                        if (showLoadingUI) UpdateLoadingProgress(currentProgress);
                    }
                    yield return null;
                }

                if (!loadSuccess)
                {
                    Debug.LogError($"Failed to load scene: {sceneName}. Going to the level select.");
                    if (shouldHideLoadingScreen) HideLoadingScreen();
                    // The previous scene is already switched off: without a fallback the player is left on a dead
                    // screen. The level select is in the base build (no Addressables) and replaces everything.
                    currentGameplayScene = "";
                    SceneManager.LoadScene(SceneTransitionManager.LEVEL_SELECTION_SCENE);
                    onComplete?.Invoke();
                    yield break;
                }

                loadedScenes.Add(sceneName);

                // Get scene reference and set it as active for lighting/physics
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }

                // Unload base menu scenes when entering gameplay
                foreach (string baseSceneName in BASE_SCENES)
                {
                    Scene baseScene = SceneManager.GetSceneByName(baseSceneName);
                    if (baseScene.isLoaded && baseScene.name != sceneName)
                    {
                        SceneManager.UnloadSceneAsync(baseScene);
                    }
                }

                // ---------------------------------------------------------------
                // UNLOAD: Now fully unload the previous scene (its resources were
                // already released above, so this just removes the scene object).
                // ---------------------------------------------------------------
                if (holder.IsValid() && holder.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(holder);

                if (hadPreviousScene && !reload)
                {
                    // Wait for unload to fully complete
                    yield return StartCoroutine(UnloadSceneAsync(currentGameplayScene));

                    // Final cleanup pass — catch anything the scene unload freed
                    yield return Resources.UnloadUnusedAssets();
                    System.GC.Collect();
                }

                // Activate new scene content (after old scene memory is freed).
                // This triggers SpriteFramePlayer.Awake() which starts loading texture chunks.
                if (scene.isLoaded)
                {
                    ActivateSceneContent(scene);
                }

                // Update current scene (Video, Death, and Level scenes all become "current")
                currentGameplayScene = sceneName;

                AddressablesOperationManager.Instance.CompleteSceneTransition();

                if (shouldHideLoadingScreen) HideLoadingScreen();
                onComplete?.Invoke();
            }
            finally
            {
                // Always reset the loading flag, even if an exception occurs
                isLoading = false;
            }
        }

        private IEnumerator UnloadSceneAsync(string sceneName)
        {
            if (!loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[AddressablesSceneLoader] Scene '{sceneName}' not tracked as loaded");
                yield break;
            }

            // Check if scene was loaded through Addressables or directly (editor testing)
            bool isAddressablesLoaded = AddressablesOperationManager.Instance.IsSceneLoadedThroughAddressables(sceneName);

            if (isAddressablesLoaded)
            {
                // Addressables unload: removes the handle from activeSceneHandles first,
                // then unloads the scene. When OnSceneUnloaded fires, it sees no handle
                // and just cleans up loadedScenes without trying to release again.
                bool unloadCompleted = false;
                AddressablesOperationManager.Instance.UnloadScene(sceneName, (success) =>
                {
                    unloadCompleted = true;
                });

                while (!unloadCompleted)
                    yield return null;
            }
            else
            {
                // Scene was loaded directly (e.g., in editor) - use Unity's SceneManager
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            // OnSceneUnloaded handles loadedScenes cleanup, but remove here too as a safety net
            loadedScenes.Remove(sceneName);
        }

        public void GetSceneDownloadSize(string sceneName, System.Action<long> onSizeRetrieved)
        {
            AddressablesOperationManager.Instance.GetLabelDownloadSize(sceneName, onSizeRetrieved);
        }

        private void ShowLoadingScreen()
        {
            if (loadingScreenPrefab != null && currentLoadingScreen == null)
            {
                currentLoadingScreen = Instantiate(loadingScreenPrefab);
                DontDestroyOnLoad(currentLoadingScreen);
            }
        }

        private void HideLoadingScreen()
        {
            if (currentLoadingScreen != null)
            {
                Destroy(currentLoadingScreen);
                currentLoadingScreen = null;
            }
        }

        private void UpdateLoadingProgress(float progress)
        {
            if (currentLoadingScreen != null)
            {
                var progressText = currentLoadingScreen.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (progressText != null)
                    progressText.text = $"Loading... {progress * 100:F0}%";
            }
        }

        private void ActivateSceneContent(Scene scene)
        {
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                var controller = rootObj.GetComponentInChildren<SceneContentController>(true);
                if (controller != null)
                {
                    controller.ActivateScene();
                    return;
                }
            }
            Debug.LogWarning($"[AddressablesSceneLoader] No SceneContentController found in scene '{scene.name}'");
        }

        private void DeactivateSceneContent(Scene scene)
        {
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                var controller = rootObj.GetComponentInChildren<SceneContentController>(true);
                if (controller != null)
                {
                    controller.DeactivateScene();
                    return;
                }
            }
        }

        /// <summary>
        /// Find all IFrameSequencePlayer instances in the given scene and release their resources
        /// BEFORE the scene is unloaded. This ensures DestroyImmediate is used for sprites so
        /// texture references are severed immediately, allowing Resources.UnloadUnusedAssets() to
        /// actually reclaim the texture memory on WebGL.
        /// </summary>
        private void ReleaseFrameSequencePlayerResources(Scene scene)
        {
            int released = 0;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                // Search all MonoBehaviours including inactive ones
                var behaviours = rootObj.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is IFrameSequencePlayer player)
                    {
                        player.ReleaseAllResources();
                        released++;
                    }
                }
            }
        }

        /// <summary>
        /// Stop all PlayableDirectors in the scene and destroy their playable graphs.
        /// This frees native mixer/playable allocations immediately rather than waiting
        /// for scene unload, reducing peak WASM memory during the transition.
        /// </summary>
        private void DestroyPlayableDirectorGraphs(Scene scene)
        {
            int destroyed = 0;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                var directors = rootObj.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
                foreach (var director in directors)
                {
                    if (director != null && director.playableGraph.IsValid())
                    {
                        director.Stop();
                        director.playableGraph.Destroy();
                        destroyed++;
                    }
                }
            }
        }
    }
}
