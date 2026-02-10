using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages preloading of Addressable labels, scenes, and assets
/// Supports both downloading (to disk cache) and loading (into RAM) for fast access
/// This is a utility singleton - call it from per-scene components (LevelPreloader, MainMenuPreloader, etc.)
/// </summary>
public class PreloadManager : MonoBehaviour
{
    private static PreloadManager _instance;
    public static PreloadManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PreloadManager");
                _instance = go.AddComponent<PreloadManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private List<string> currentlyPreloaded = new List<string>();

    // Track what's currently loaded in memory
    private Dictionary<string, AsyncOperationHandle> loadedLabels = new Dictionary<string, AsyncOperationHandle>();
    private Dictionary<string, AsyncOperationHandle> loadedAssets = new Dictionary<string, AsyncOperationHandle>();
    private HashSet<string> downloadedLabels = new HashSet<string>();

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

    #region Public API

    /// <summary>
    /// Download assets with a label to disk cache (doesn't load into RAM)
    /// Fast for subsequent loads. Best for next level preloading during gameplay.
    /// </summary>
    public void DownloadLabel(string label, System.Action<bool> onComplete = null)
    {
        StartCoroutine(DownloadLabelAsync(label, onComplete));
    }

    /// <summary>
    /// Download AND load assets with a label into RAM
    /// Instant access but uses memory. Best for imminent content.
    /// </summary>
    public void PreloadLabel(string label, System.Action<bool> onComplete = null)
    {
        StartCoroutine(PreloadLabelAsync(label, onComplete));
    }

    /// <summary>
    /// Download multiple labels at once
    /// </summary>
    public void DownloadLabels(List<string> labels, System.Action<bool> onComplete = null)
    {
        StartCoroutine(DownloadMultipleLabelsAsync(labels, onComplete));
    }

    /// <summary>
    /// Preload multiple labels into RAM
    /// </summary>
    public void PreloadLabels(List<string> labels, System.Action<bool> onComplete = null)
    {
        StartCoroutine(PreloadMultipleLabelsAsync(labels, onComplete));
    }

    /// <summary>
    /// Download a specific scene and all its dependencies
    /// </summary>
    public void DownloadScene(string sceneName, System.Action<bool> onComplete = null)
    {
        StartCoroutine(DownloadSceneAsync(sceneName, onComplete));
    }

    /// <summary>
    /// Get download size for a label
    /// </summary>
    public void GetLabelDownloadSize(string label, System.Action<long> onComplete)
    {
        StartCoroutine(GetLabelSizeAsync(label, onComplete));
    }

    /// <summary>
    /// Release a label from memory (but keeps it cached on disk)
    /// </summary>
    public void ReleaseLabel(string label)
    {
        if (loadedLabels.ContainsKey(label))
        {
            var handle = loadedLabels[label];
            if (handle.IsValid())
            {
                Addressables.Release(handle);
                Log($"Released label from memory: {label}");
            }
            loadedLabels.Remove(label);
            currentlyPreloaded.Remove(label);
        }
    }

    /// <summary>
    /// Release all loaded labels from memory
    /// Call when transitioning between major sections (e.g., menu to game)
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in loadedLabels)
        {
            if (kvp.Value.IsValid())
            {
                Addressables.Release(kvp.Value);
            }
        }
        loadedLabels.Clear();
        
        foreach (var kvp in loadedAssets)
        {
            if (kvp.Value.IsValid())
            {
                Addressables.Release(kvp.Value);
            }
        }
        loadedAssets.Clear();
        
        currentlyPreloaded.Clear();
        Log("Released all preloaded content from memory");
    }

    #endregion

    #region Internal Implementation

    private IEnumerator DownloadLabelAsync(string label, System.Action<bool> onComplete)
    {
        // Check if already downloaded
        var sizeHandle = Addressables.GetDownloadSizeAsync(label);
        yield return sizeHandle;

        long downloadSize = sizeHandle.Result;
        Addressables.Release(sizeHandle);

        if (downloadSize == 0)
        {
            Log($"Label '{label}' already cached on disk");
            downloadedLabels.Add(label);
            onComplete?.Invoke(true);
            yield break;
        }

        // Download the label
        Log($"Downloading label '{label}': {FormatBytes(downloadSize)}");
        
        var downloadHandle = Addressables.DownloadDependenciesAsync(label);
        
        while (!downloadHandle.IsDone)
        {
            float progress = downloadHandle.PercentComplete;
            Log($"Download progress '{label}': {progress * 100:F0}%");
            yield return null;
        }

        if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Log($"Successfully downloaded label: {label}");
            downloadedLabels.Add(label);
            Addressables.Release(downloadHandle);
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogError($"Failed to download label '{label}': {downloadHandle.OperationException}");
            Addressables.Release(downloadHandle);
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator PreloadLabelAsync(string label, System.Action<bool> onComplete)
    {
        // Check if already loaded in memory
        if (loadedLabels.ContainsKey(label))
        {
            Log($"Label '{label}' already loaded in memory");
            onComplete?.Invoke(true);
            yield break;
        }

        // First download to disk cache if not already there
        bool downloaded = false;
        yield return DownloadLabelAsync(label, (success) => downloaded = success);
        
        if (!downloaded)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // Now load into memory
        Log($"Loading label '{label}' into memory...");
        
        var loadHandle = Addressables.LoadAssetsAsync<Object>(label, null);
        yield return loadHandle;

        if (loadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            loadedLabels[label] = loadHandle;
            currentlyPreloaded.Add(label);
            Log($"Successfully preloaded label '{label}' ({loadHandle.Result.Count} assets in RAM)");
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogError($"Failed to preload label '{label}': {loadHandle.OperationException}");
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator DownloadMultipleLabelsAsync(List<string> labels, System.Action<bool> onComplete)
    {
        bool allSucceeded = true;
        
        foreach (string label in labels)
        {
            bool success = false;
            yield return DownloadLabelAsync(label, (result) => success = result);
            if (!success) allSucceeded = false;
        }
        
        onComplete?.Invoke(allSucceeded);
    }

    private IEnumerator PreloadMultipleLabelsAsync(List<string> labels, System.Action<bool> onComplete)
    {
        bool allSucceeded = true;
        
        foreach (string label in labels)
        {
            bool success = false;
            yield return PreloadLabelAsync(label, (result) => success = result);
            if (!success) allSucceeded = false;
        }
        
        onComplete?.Invoke(allSucceeded);
    }

    private IEnumerator DownloadSceneAsync(string sceneName, System.Action<bool> onComplete)
    {
        // Check size first
        var sizeHandle = Addressables.GetDownloadSizeAsync(sceneName);
        yield return sizeHandle;

        long downloadSize = sizeHandle.Result;
        Addressables.Release(sizeHandle);

        if (downloadSize == 0)
        {
            Log($"Scene '{sceneName}' already cached");
            onComplete?.Invoke(true);
            yield break;
        }

        Log($"Downloading scene '{sceneName}': {FormatBytes(downloadSize)}");
        
        var downloadHandle = Addressables.DownloadDependenciesAsync(sceneName);
        
        while (!downloadHandle.IsDone)
        {
            yield return null;
        }

        if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Log($"Successfully downloaded scene: {sceneName}");
            Addressables.Release(downloadHandle);
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogError($"Failed to download scene '{sceneName}': {downloadHandle.OperationException}");
            Addressables.Release(downloadHandle);
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator GetLabelSizeAsync(string label, System.Action<long> onComplete)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(label);
        yield return sizeHandle;
        
        onComplete?.Invoke(sizeHandle.Result);
        Addressables.Release(sizeHandle);
    }

    #endregion

    #region Utility

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PreloadManager] {message}");
        }
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F2} KB";
        return $"{bytes / 1024f / 1024f:F2} MB";
    }

    #endregion

    private void OnDestroy()
    {
        ReleaseAll();
    }
}

/// <summary>
/// Configuration for what to preload when entering a specific scene
/// </summary>
[System.Serializable]
public class ScenePreloadConfig
{
    [Tooltip("The scene this config applies to (e.g., 'Level1', 'MainMenu')")]
    public string sceneName;
    
    [Header("Download Only (Disk Cache)")]
    [Tooltip("Labels to download in background (fast loading later, no RAM usage now)")]
    public List<string> labelsToDownload = new List<string>();
    
    [Tooltip("Specific scenes to download (e.g., next level)")]
    public List<string> scenesToDownload = new List<string>();
    
    [Header("Preload (Load into RAM)")]
    [Tooltip("Labels to load into memory (instant access, uses RAM)")]
    public List<string> labelsToPreload = new List<string>();
}
