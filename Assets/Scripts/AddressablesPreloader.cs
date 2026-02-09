using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

/// <summary>
/// Preloads shared Addressables assets (from default group or by label) on startup
/// Attach this to MainMenu scene to cache common assets used across levels/videos
/// </summary>
public class AddressablesPreloader : MonoBehaviour
{
    [Header("Preload Settings")]
    [Tooltip("Label to preload (e.g., 'default', 'shared', 'common'). Leave empty to skip label-based preloading.")]
    [SerializeField] private string preloadLabel = "default";
    
    [Tooltip("Specific asset addresses to preload (optional).")]
    [SerializeField] private List<string> specificAssets = new List<string>();
    
    [Tooltip("Start preloading automatically on Start()")]
    [SerializeField] private bool preloadOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private List<AsyncOperationHandle> preloadHandles = new List<AsyncOperationHandle>();
    private bool isPreloading = false;
    private bool preloadComplete = false;

    private void Start()
    {
        if (preloadOnStart)
        {
            PreloadAssets();
        }
    }

    /// <summary>
    /// Preload assets by label and/or specific addresses
    /// </summary>
    public void PreloadAssets()
    {
        if (isPreloading)
        {
            if (showDebugLogs)
                Debug.LogWarning("AddressablesPreloader: Already preloading!");
            return;
        }

        isPreloading = true;

        // Preload by label
        if (!string.IsNullOrEmpty(preloadLabel))
        {
            PreloadByLabel(preloadLabel);
        }

        // Preload specific assets
        foreach (string address in specificAssets)
        {
            if (!string.IsNullOrEmpty(address))
            {
                PreloadByAddress(address);
            }
        }

        if (preloadHandles.Count == 0)
        {
            if (showDebugLogs)
                Debug.Log("AddressablesPreloader: No assets to preload.");
            isPreloading = false;
            preloadComplete = true;
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"AddressablesPreloader: Started preloading {preloadHandles.Count} asset(s)...");
        }
    }

    private void PreloadByLabel(string label)
    {
        var handle = Addressables.LoadAssetsAsync<object>(label, null);
        handle.Completed += OnLabelPreloadComplete;
        preloadHandles.Add(handle);
    }

    private void PreloadByAddress(string address)
    {
        var handle = Addressables.LoadAssetAsync<object>(address);
        handle.Completed += OnAssetPreloadComplete;
        preloadHandles.Add(handle);
    }

    private void OnLabelPreloadComplete(AsyncOperationHandle<IList<object>> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            if (showDebugLogs)
                Debug.Log($"AddressablesPreloader: Successfully preloaded label '{preloadLabel}' ({handle.Result.Count} asset(s))");
        }
        else
        {
            Debug.LogError($"AddressablesPreloader: Failed to preload label '{preloadLabel}': {handle.OperationException}");
        }

        CheckPreloadComplete();
    }

    private void OnAssetPreloadComplete(AsyncOperationHandle<object> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            if (showDebugLogs)
                Debug.Log($"AddressablesPreloader: Successfully preloaded asset");
        }
        else
        {
            Debug.LogError($"AddressablesPreloader: Failed to preload asset: {handle.OperationException}");
        }

        CheckPreloadComplete();
    }

    private void CheckPreloadComplete()
    {
        // Check if all handles are complete
        bool allComplete = true;
        foreach (var handle in preloadHandles)
        {
            if (!handle.IsDone)
            {
                allComplete = false;
                break;
            }
        }

        if (allComplete)
        {
            isPreloading = false;
            preloadComplete = true;
            if (showDebugLogs)
                Debug.Log("AddressablesPreloader: All assets preloaded successfully!");
        }
    }

    /// <summary>
    /// Check if preloading is complete
    /// </summary>
    public bool IsPreloadComplete()
    {
        return preloadComplete;
    }

    /// <summary>
    /// Release all preloaded assets (call this when quitting or restarting)
    /// </summary>
    public void ReleasePreloadedAssets()
    {
        foreach (var handle in preloadHandles)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        preloadHandles.Clear();
        preloadComplete = false;
        
        if (showDebugLogs)
            Debug.Log("AddressablesPreloader: Released all preloaded assets.");
    }

    private void OnDestroy()
    {
        // Optional: Release on destroy (usually you want to keep them cached)
        // ReleasePreloadedAssets();
    }
}
