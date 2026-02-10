using UnityEngine;
using System.Collections;

/// <summary>
/// Handles scene loading using Addressables for optimized WebGL builds
/// Delegates to AddressablesOperationManager for deduplication
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

        // Delegate to AddressablesOperationManager
        bool loadCompleted = false;
        bool loadSuccess = false;
        
        AddressablesOperationManager.Instance.LoadScene(sceneName, (success) =>
        {
            loadSuccess = success;
            loadCompleted = true;
        });
        
        // Wait for scene to load
        while (!loadCompleted)
        {
            yield return null;
        }

        // Check for errors
        if (!loadSuccess)
        {
            Debug.LogError($"Failed to load scene: {sceneName}");
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
    /// Preload a scene's assets into RAM for instant loading later
    /// Good for preloading next level during gameplay
    /// </summary>
    public void CacheSceneAssets(string sceneName)
    {
        PreloadManager.Instance.PreloadLabel(sceneName, null);
    }

    /// <summary>
    /// Get the download size for a scene (0 = already cached)
    /// </summary>
    public void GetSceneDownloadSize(string sceneName, System.Action<long> onSizeRetrieved)
    {
        AddressablesOperationManager.Instance.GetLabelDownloadSize(sceneName, onSizeRetrieved);
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
        // Cleanup if needed
    }
}