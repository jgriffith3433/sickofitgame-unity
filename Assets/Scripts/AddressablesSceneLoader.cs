using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using System.Collections;

/// <summary>
/// Handles scene loading using Addressables for optimized WebGL builds
/// Scenes are loaded on-demand and can be hosted remotely
/// </summary>
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

    [Header("Loading UI (Optional)")]
    [SerializeField] private GameObject loadingScreenPrefab;
    
    private GameObject currentLoadingScreen;
    private AsyncOperationHandle<SceneInstance> currentLoadOperation;
    private bool isLoading = false;

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

    /// <summary>
    /// Load a scene by its Addressable address (scene name)
    /// </summary>
    public void LoadScene(string sceneName, System.Action onComplete = null)
    {
        if (isLoading)
        {
            Debug.LogWarning($"Already loading a scene. Please wait.");
            return;
        }

        StartCoroutine(LoadSceneAsync(sceneName, onComplete));
    }

    /// <summary>
    /// Load a level by number (converts to scene name)
    /// </summary>
    public void LoadLevel(int levelNumber, System.Action onComplete = null)
    {
        string sceneName = $"Level{levelNumber}";
        LoadScene(sceneName, onComplete);
    }

    /// <summary>
    /// Load a video scene by number
    /// </summary>
    public void LoadVideo(int videoNumber, System.Action onComplete = null)
    {
        string sceneName = $"Video{videoNumber}";
        LoadScene(sceneName, onComplete);
    }

    private IEnumerator LoadSceneAsync(string sceneName, System.Action onComplete)
    {
        isLoading = true;

        // Show loading screen
        ShowLoadingScreen();

        // Note: Using LoadSceneMode.Single automatically unloads the previous scene
        // Manual unloading is not needed and causes "last scene" warnings

        // Load the new scene (replaces current scene automatically)
        currentLoadOperation = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        // Wait for scene to load with progress updates
        while (!currentLoadOperation.IsDone)
        {
            float progress = currentLoadOperation.PercentComplete;
            UpdateLoadingProgress(progress);
            yield return null;
        }

        // Check for errors
        if (currentLoadOperation.Status == AsyncOperationStatus.Failed)
        {
            Debug.LogError($"Failed to load scene: {sceneName}. Error: {currentLoadOperation.OperationException}");
        }
        else
        {
            Debug.Log($"Successfully loaded scene: {sceneName}");
        }

        // Hide loading screen
        HideLoadingScreen();

        isLoading = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Preload a scene into memory without activating it (for faster transitions)
    /// </summary>
    public void PreloadScene(string sceneName)
    {
        StartCoroutine(PreloadSceneAsync(sceneName));
    }

    private IEnumerator PreloadSceneAsync(string sceneName)
    {
        var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive, false);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"Preloaded scene: {sceneName}");
            // Keep reference to unload later if needed
        }
    }

    /// <summary>
    /// Download and cache a scene's assets without loading it
    /// Good for preloading next level during gameplay
    /// </summary>
    public void CacheSceneAssets(string sceneName)
    {
        StartCoroutine(CacheSceneAsync(sceneName));
    }

    private IEnumerator CacheSceneAsync(string sceneName)
    {
        var downloadSize = Addressables.GetDownloadSizeAsync(sceneName);
        yield return downloadSize;

        if (downloadSize.Result > 0)
        {
            Debug.Log($"Downloading {sceneName}: {downloadSize.Result / 1024f / 1024f:F2} MB");
            
            var downloadDeps = Addressables.DownloadDependenciesAsync(sceneName);
            
            while (!downloadDeps.IsDone)
            {
                float progress = downloadDeps.PercentComplete;
                Debug.Log($"Download progress: {progress * 100:F0}%");
                yield return null;
            }

            if (downloadDeps.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Successfully cached {sceneName}");
            }
            
            Addressables.Release(downloadDeps);
        }
        else
        {
            Debug.Log($"{sceneName} already cached");
        }

        Addressables.Release(downloadSize);
    }

    /// <summary>
    /// Get the download size for a scene (useful for showing download progress to player)
    /// </summary>
    public void GetSceneDownloadSize(string sceneName, System.Action<long> onSizeRetrieved)
    {
        StartCoroutine(GetDownloadSizeAsync(sceneName, onSizeRetrieved));
    }

    private IEnumerator GetDownloadSizeAsync(string sceneName, System.Action<long> callback)
    {
        var handle = Addressables.GetDownloadSizeAsync(sceneName);
        yield return handle;
        callback?.Invoke(handle.Result);
        Addressables.Release(handle);
    }

    // Loading screen management
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
            // Find and update progress bar/text if you have one
            var progressText = currentLoadingScreen.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (progressText != null)
            {
                progressText.text = $"Loading... {progress * 100:F0}%";
            }
        }
    }

    private void OnDestroy()
    {
        if (currentLoadOperation.IsValid())
        {
            Addressables.UnloadSceneAsync(currentLoadOperation);
        }
    }
}
