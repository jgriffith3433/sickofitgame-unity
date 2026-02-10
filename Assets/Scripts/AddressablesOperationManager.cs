using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central manager for ALL Addressables operations in the game.
/// Prevents duplicate concurrent downloads/loads of the same asset.
/// All systems (PreloadManager, SceneLoader, etc.) should use this.
/// </summary>
public class AddressablesOperationManager : MonoBehaviour
{
    private static AddressablesOperationManager _instance;
    public static AddressablesOperationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AddressablesOperationManager");
                _instance = go.AddComponent<AddressablesOperationManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private List<string> activeOperations = new List<string>();

    // Track in-progress operations
    private Dictionary<string, bool> inProgressDownloads = new Dictionary<string, bool>();
    private Dictionary<string, List<System.Action<bool>>> pendingDownloadCallbacks = new Dictionary<string, List<System.Action<bool>>>();
    
    private Dictionary<string, bool> inProgressLabelLoads = new Dictionary<string, bool>();
    private Dictionary<string, List<System.Action<bool>>> pendingLabelLoadCallbacks = new Dictionary<string, List<System.Action<bool>>>();
    
    private Dictionary<string, bool> inProgressSceneLoads = new Dictionary<string, bool>();
    private Dictionary<string, List<System.Action<bool>>> pendingSceneLoadCallbacks = new Dictionary<string, List<System.Action<bool>>>();

    // Track completed loads
    private Dictionary<string, AsyncOperationHandle> loadedLabels = new Dictionary<string, AsyncOperationHandle>();
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
    /// Download a label to disk cache (doesn't load into RAM)
    /// If already downloading, queues the callback.
    /// </summary>
    public void DownloadLabel(string label, System.Action<bool> onComplete = null)
    {
        StartCoroutine(DownloadLabelAsync(label, onComplete));
    }

    /// <summary>
    /// Load a label into RAM (downloads first if needed)
    /// If already loading, queues the callback.
    /// </summary>
    public void LoadLabel(string label, System.Action<bool> onComplete = null)
    {
        StartCoroutine(LoadLabelAsync(label, onComplete));
    }

    /// <summary>
    /// Load a scene using Addressables
    /// If already loading, queues the callback.
    /// </summary>
    public void LoadScene(string sceneName, System.Action<bool> onComplete = null)
    {
        StartCoroutine(LoadSceneAsync(sceneName, onComplete));
    }

    /// <summary>
    /// Get download size for a label
    /// </summary>
    public void GetLabelDownloadSize(string label, System.Action<long> onComplete)
    {
        StartCoroutine(GetDownloadSizeAsync(label, onComplete));
    }

    /// <summary>
    /// Check if a label is currently being downloaded
    /// </summary>
    public bool IsDownloading(string label)
    {
        return inProgressDownloads.ContainsKey(label) && inProgressDownloads[label];
    }

    /// <summary>
    /// Check if a label is currently being loaded
    /// </summary>
    public bool IsLoading(string label)
    {
        return inProgressLabelLoads.ContainsKey(label) && inProgressLabelLoads[label];
    }

    /// <summary>
    /// Check if a scene is currently being loaded
    /// </summary>
    public bool IsLoadingScene(string sceneName)
    {
        return inProgressSceneLoads.ContainsKey(sceneName) && inProgressSceneLoads[sceneName];
    }

    /// <summary>
    /// Check if a label is already loaded in memory
    /// </summary>
    public bool IsLabelLoaded(string label)
    {
        return loadedLabels.ContainsKey(label);
    }

    /// <summary>
    /// Release a label from memory (keeps it cached on disk)
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
        }
    }

    /// <summary>
    /// Release all loaded labels from memory
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
        Log("Released all loaded content from memory");
    }

    #endregion

    #region Internal Implementation

    private IEnumerator DownloadLabelAsync(string label, System.Action<bool> onComplete)
    {
        // Check if already downloading - queue the callback
        if (inProgressDownloads.ContainsKey(label) && inProgressDownloads[label])
        {
            Log($"[DEDUP] Label '{label}' download already in progress, queuing callback...");
            if (!pendingDownloadCallbacks.ContainsKey(label))
            {
                pendingDownloadCallbacks[label] = new List<System.Action<bool>>();
            }
            pendingDownloadCallbacks[label].Add(onComplete);
            yield break;
        }
        
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

        // Mark as in-progress
        inProgressDownloads[label] = true;
        activeOperations.Add($"Download: {label}");
        
        // Download the label
        Log($"Downloading label '{label}': {FormatBytes(downloadSize)}");
        
        var downloadHandle = Addressables.DownloadDependenciesAsync(label);
        
        while (!downloadHandle.IsDone)
        {
            float progress = downloadHandle.PercentComplete;
            yield return null;
        }

        bool success = downloadHandle.Status == AsyncOperationStatus.Succeeded;
        
        if (success)
        {
            Log($"Successfully downloaded label: {label}");
            downloadedLabels.Add(label);
        }
        else
        {
            Debug.LogError($"Failed to download label '{label}': {downloadHandle.OperationException}");
        }
        
        Addressables.Release(downloadHandle);
        
        // Mark as complete
        inProgressDownloads[label] = false;
        activeOperations.Remove($"Download: {label}");
        
        // Call the original callback
        onComplete?.Invoke(success);
        
        // Call all queued callbacks
        if (pendingDownloadCallbacks.ContainsKey(label))
        {
            Log($"[DEDUP] Calling {pendingDownloadCallbacks[label].Count} queued download callbacks for '{label}'");
            foreach (var callback in pendingDownloadCallbacks[label])
            {
                callback?.Invoke(success);
            }
            pendingDownloadCallbacks.Remove(label);
        }
    }

    private IEnumerator LoadLabelAsync(string label, System.Action<bool> onComplete)
    {
        // Check if already loading - queue the callback
        if (inProgressLabelLoads.ContainsKey(label) && inProgressLabelLoads[label])
        {
            Log($"[DEDUP] Label '{label}' load already in progress, queuing callback...");
            if (!pendingLabelLoadCallbacks.ContainsKey(label))
            {
                pendingLabelLoadCallbacks[label] = new List<System.Action<bool>>();
            }
            pendingLabelLoadCallbacks[label].Add(onComplete);
            yield break;
        }
        
        // Check if already loaded in memory
        if (loadedLabels.ContainsKey(label))
        {
            Log($"Label '{label}' already loaded in memory");
            onComplete?.Invoke(true);
            yield break;
        }

        // Mark as in-progress
        inProgressLabelLoads[label] = true;
        activeOperations.Add($"Load: {label}");

        // First download to disk cache if not already there
        bool downloaded = false;
        yield return DownloadLabelAsync(label, (success) => downloaded = success);
        
        if (!downloaded)
        {
            inProgressLabelLoads[label] = false;
            activeOperations.Remove($"Load: {label}");
            onComplete?.Invoke(false);
            
            // Call queued callbacks
            if (pendingLabelLoadCallbacks.ContainsKey(label))
            {
                foreach (var callback in pendingLabelLoadCallbacks[label])
                {
                    callback?.Invoke(false);
                }
                pendingLabelLoadCallbacks.Remove(label);
            }
            yield break;
        }

        // Now load into memory
        Log($"Loading label '{label}' into memory...");
        
        var loadHandle = Addressables.LoadAssetsAsync<Object>(label, null);
        yield return loadHandle;

        bool success = loadHandle.Status == AsyncOperationStatus.Succeeded;
        
        if (success)
        {
            loadedLabels[label] = loadHandle;
            Log($"Successfully loaded label '{label}' ({loadHandle.Result.Count} assets in RAM)");
        }
        else
        {
            Debug.LogError($"Failed to load label '{label}': {loadHandle.OperationException}");
        }
        
        // Mark as complete
        inProgressLabelLoads[label] = false;
        activeOperations.Remove($"Load: {label}");
        
        // Call the original callback
        onComplete?.Invoke(success);
        
        // Call all queued callbacks
        if (pendingLabelLoadCallbacks.ContainsKey(label))
        {
            Log($"[DEDUP] Calling {pendingLabelLoadCallbacks[label].Count} queued load callbacks for '{label}'");
            foreach (var callback in pendingLabelLoadCallbacks[label])
            {
                callback?.Invoke(success);
            }
            pendingLabelLoadCallbacks.Remove(label);
        }
    }

    private IEnumerator LoadSceneAsync(string sceneName, System.Action<bool> onComplete)
    {
        // Check if already loading - queue the callback
        if (inProgressSceneLoads.ContainsKey(sceneName) && inProgressSceneLoads[sceneName])
        {
            Log($"[DEDUP] Scene '{sceneName}' load already in progress, queuing callback...");
            if (!pendingSceneLoadCallbacks.ContainsKey(sceneName))
            {
                pendingSceneLoadCallbacks[sceneName] = new List<System.Action<bool>>();
            }
            pendingSceneLoadCallbacks[sceneName].Add(onComplete);
            yield break;
        }

        // Mark as in-progress
        inProgressSceneLoads[sceneName] = true;
        activeOperations.Add($"Scene: {sceneName}");
        
        Log($"Loading scene '{sceneName}'...");
        
        var sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        
        while (!sceneHandle.IsDone)
        {
            yield return null;
        }

        bool success = sceneHandle.Status == AsyncOperationStatus.Succeeded;
        
        if (success)
        {
            Log($"Successfully loaded scene: {sceneName}");
        }
        else
        {
            Debug.LogError($"Failed to load scene '{sceneName}': {sceneHandle.OperationException}");
        }
        
        // Mark as complete
        inProgressSceneLoads[sceneName] = false;
        activeOperations.Remove($"Scene: {sceneName}");
        
        // Call the original callback
        onComplete?.Invoke(success);
        
        // Call all queued callbacks
        if (pendingSceneLoadCallbacks.ContainsKey(sceneName))
        {
            Log($"[DEDUP] Calling {pendingSceneLoadCallbacks[sceneName].Count} queued scene load callbacks for '{sceneName}'");
            foreach (var callback in pendingSceneLoadCallbacks[sceneName])
            {
                callback?.Invoke(success);
            }
            pendingSceneLoadCallbacks.Remove(sceneName);
        }
    }

    private IEnumerator GetDownloadSizeAsync(string label, System.Action<long> onComplete)
    {
        var handle = Addressables.GetDownloadSizeAsync(label);
        yield return handle;
        onComplete?.Invoke(handle.Result);
        Addressables.Release(handle);
    }

    #endregion

    #region Utilities

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[AddressablesOp] {message}");
        }
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F2} KB";
        return $"{bytes / 1024f / 1024f:F2} MB";
    }

    #endregion
}
