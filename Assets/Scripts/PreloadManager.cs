using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// User-facing API for preloading Addressable labels, scenes, and assets
/// This is a thin wrapper around AddressablesOperationManager for backward compatibility
/// Call from per-scene components (LevelPreloader, MainMenuPreloader, etc.)
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

    // All operations are now delegated to AddressablesOperationManager

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
        AddressablesOperationManager.Instance.DownloadLabel(label, onComplete);
    }

    /// <summary>
    /// Download AND load assets with a label into RAM
    /// Instant access but uses memory. Best for imminent content.
    /// </summary>
    public void PreloadLabel(string label, System.Action<bool> onComplete = null)
    {
        AddressablesOperationManager.Instance.LoadLabel(label, onComplete);
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
        AddressablesOperationManager.Instance.GetLabelDownloadSize(label, onComplete);
    }

    /// <summary>
    /// Release a label from memory (but keeps it cached on disk)
    /// </summary>
    public void ReleaseLabel(string label)
    {
        AddressablesOperationManager.Instance.ReleaseLabel(label);
    }

    /// <summary>
    /// Release all loaded labels from memory
    /// Call when transitioning between major sections (e.g., menu to game)
    /// </summary>
    public void ReleaseAll()
    {
        AddressablesOperationManager.Instance.ReleaseAll();
    }

    #endregion

    #region Internal Implementation

    private IEnumerator DownloadMultipleLabelsAsync(List<string> labels, System.Action<bool> onComplete)
    {
        bool allSucceeded = true;
        int remaining = labels.Count;
        
        foreach (string label in labels)
        {
            AddressablesOperationManager.Instance.DownloadLabel(label, (success) =>
            {
                if (!success) allSucceeded = false;
                remaining--;
            });
        }
        
        // Wait for all downloads to complete
        while (remaining > 0)
        {
            yield return null;
        }
        
        onComplete?.Invoke(allSucceeded);
    }

    private IEnumerator PreloadMultipleLabelsAsync(List<string> labels, System.Action<bool> onComplete)
    {
        bool allSucceeded = true;
        int remaining = labels.Count;
        
        foreach (string label in labels)
        {
            AddressablesOperationManager.Instance.LoadLabel(label, (success) =>
            {
                if (!success) allSucceeded = false;
                remaining--;
            });
        }
        
        // Wait for all loads to complete
        while (remaining > 0)
        {
            yield return null;
        }
        
        onComplete?.Invoke(allSucceeded);
    }

    private IEnumerator DownloadSceneAsync(string sceneName, System.Action<bool> onComplete)
    {
        bool completed = false;
        bool result = false;
        
        AddressablesOperationManager.Instance.DownloadLabel(sceneName, (success) =>
        {
            result = success;
            completed = true;
        });
        
        while (!completed)
        {
            yield return null;
        }
        
        onComplete?.Invoke(result);
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
