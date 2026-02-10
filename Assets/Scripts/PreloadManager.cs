using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// User-facing API for preloading Addressable content (labels and scenes)
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
    /// Preload assets with a label into RAM for instant access
    /// This downloads the bundle (if needed) and loads assets into memory
    /// </summary>
    public void PreloadLabel(string label, System.Action<bool> onComplete = null)
    {
        AddressablesOperationManager.Instance.LoadLabel(label, onComplete);
    }

    /// <summary>
    /// Preload multiple labels into RAM
    /// </summary>
    public void PreloadLabels(List<string> labels, System.Action<bool> onComplete = null)
    {
        StartCoroutine(PreloadMultipleLabelsAsync(labels, onComplete));
    }

    /// <summary>
    /// Get download size for a label (0 = already cached)
    /// </summary>
    public void GetLabelDownloadSize(string label, System.Action<long> onComplete)
    {
        AddressablesOperationManager.Instance.GetLabelDownloadSize(label, onComplete);
    }

    /// <summary>
    /// Release a label from memory
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

    #endregion

    private void OnDestroy()
    {
        ReleaseAll();
    }
}
