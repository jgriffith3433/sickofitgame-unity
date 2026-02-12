using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

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
    private HashSet<string> preloadingScenes = new HashSet<string>();
    
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
        // If scene is preloading, wait for it to finish then activate
        if (preloadingScenes.Contains(sceneName))
        {
            StartCoroutine(WaitForPreloadThenActivate(sceneName, onComplete));
            return;
        }
        
        if (isLoading)
        {
            Debug.LogWarning($"Already loading a scene. Rejecting request for: {sceneName}");
            return;
        }

        StartCoroutine(LoadSceneAsync(sceneName, true, onComplete));
    }
    
    // REMOVED: ReloadCurrentScene() - Use Death scene flow instead
    // Trigger player death or call LevelManager.RestartLevel() to reload a level
    
    /// <summary>
    /// Preload a scene additively in the background without activating it
    /// </summary>
    public void PreloadScene(string sceneName, System.Action<bool> onComplete = null)
    {
        if (preloadingScenes.Contains(sceneName) || loadedScenes.Contains(sceneName))
        {
            // Scene already loaded
            onComplete?.Invoke(true);
            return;
        }

        StartCoroutine(PreloadSceneAsync(sceneName, onComplete));
    }

    public void LoadLevel(int levelNumber, System.Action onComplete = null)
    {
        LoadScene($"Level{levelNumber}", onComplete);
    }

    public void LoadVideo(int videoNumber, System.Action onComplete = null)
    {
        LoadScene($"Video{videoNumber}", onComplete);
    }
    
    public void PreloadLevel(int levelNumber, System.Action<bool> onComplete = null)
    {
        PreloadScene($"Level{levelNumber}", onComplete);
    }
    
    public void PreloadVideo(int videoNumber, System.Action<bool> onComplete = null)
    {
        PreloadScene($"Video{videoNumber}", onComplete);
    }

    private IEnumerator LoadSceneAsync(string sceneName, bool showLoadingUI, System.Action onComplete)
    {
        isLoading = true;
        bool shouldHideLoadingScreen = false;
        
        try
        {
            // Check if already preloaded
            bool wasPreloaded = loadedScenes.Contains(sceneName);
            
            if (!wasPreloaded)
            {
                // Scene not preloaded - load it now with loading UI
                if (showLoadingUI)
                {
                    ShowLoadingScreen();
                    shouldHideLoadingScreen = true;
                }
                
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
                    Debug.LogError($"Failed to load scene: {sceneName}");
                    if (shouldHideLoadingScreen) HideLoadingScreen();
                    onComplete?.Invoke();
                    yield break;
                }
                
                loadedScenes.Add(sceneName);
            }
            else
            {
                // Scene was preloaded - activate immediately (no loading UI needed)
            }

            // Activate the scene
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                
                // Show the scene's content
                ActivateSceneContent(scene);
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
            
            // Simple rule: Unload the previous scene (whatever we came from)
            if (!string.IsNullOrEmpty(currentGameplayScene) && currentGameplayScene != sceneName)
            {
                // Hide previous scene's content before unloading
                Scene previousScene = SceneManager.GetSceneByName(currentGameplayScene);
                if (previousScene.isLoaded)
                {
                    DeactivateSceneContent(previousScene);
                }
                
                // Unload in background
                StartCoroutine(UnloadSceneAsync(currentGameplayScene));
            }
            
            // Update current scene (Video, Death, and Level scenes all become "current")
            currentGameplayScene = sceneName;
            
            AddressablesOperationManager.Instance.SetCurrentScene(sceneName);
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
    
    private IEnumerator WaitForPreloadThenActivate(string sceneName, System.Action onComplete)
    {
        // Wait for preload to finish
        while (preloadingScenes.Contains(sceneName))
        {
            yield return null;
        }
        
        // Now activate the preloaded scene
        StartCoroutine(LoadSceneAsync(sceneName, false, onComplete));
    }
    
    private IEnumerator PreloadSceneAsync(string sceneName, System.Action<bool> onComplete)
    {
        if (loadedScenes.Contains(sceneName))
        {
            onComplete?.Invoke(true);
            yield break;
        }
        
        if (preloadingScenes.Contains(sceneName))
        {
            onComplete?.Invoke(true);
            yield break;
        }

        preloadingScenes.Add(sceneName);
        
        bool loadCompleted = false;
        bool loadSuccess = false;
        
        AddressablesOperationManager.Instance.LoadScene(sceneName, (success) =>
        {
            loadSuccess = success;
            loadCompleted = true;
        }, LoadSceneMode.Additive);
        
        while (!loadCompleted)
            yield return null;

        preloadingScenes.Remove(sceneName);
        
        if (loadSuccess)
        {
            loadedScenes.Add(sceneName);
        }
        else
        {
            Debug.LogError($"[AddressablesSceneLoader] Failed to preload scene '{sceneName}'");
        }
        
        onComplete?.Invoke(loadSuccess);
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
            // Use Addressables to unload
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

        loadedScenes.Remove(sceneName);
    }

    public void CacheSceneAssets(string sceneName)
    {
        PreloadManager.Instance.PreloadLabel(sceneName, null);
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
}