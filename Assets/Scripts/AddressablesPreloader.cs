using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEPRECATED: Use PreloadManager or LevelPreloader instead
/// This is an OLD utility that has been replaced by the new preloading system
/// Preloads shared Addressables assets (from default group or by label) on startup
/// Attach this to MainMenu scene to cache common assets used across levels/videos
/// </summary>
public class AddressablesPreloader : MonoBehaviour
{
    [Header("Preload Settings")]
    [Tooltip("Label to preload (e.g., 'default', 'shared', 'common'). Leave empty to skip label-based preloading.")]
    [SerializeField] private string preloadLabel = "default";
    
    [Tooltip("Specific asset addresses to preload (optional). NOTE: Individual asset preloading not supported in new system.")]
    [SerializeField] private List<string> specificAssets = new List<string>();
    
    [Tooltip("Start preloading automatically on Start()")]
    [SerializeField] private bool preloadOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isPreloading = false;
    private bool preloadComplete = false;

    private void Start()
    {
        Debug.LogWarning("AddressablesPreloader is DEPRECATED! Use PreloadManager or LevelPreloader instead.");
        
        if (preloadOnStart)
        {
            PreloadAssets();
        }
    }

    /// <summary>
    /// Preload assets by label (now delegates to AddressablesOperationManager)
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

        // Preload by label using new system
        if (!string.IsNullOrEmpty(preloadLabel))
        {
            if (showDebugLogs)
                Debug.Log($"AddressablesPreloader: Delegating to AddressablesOperationManager for label '{preloadLabel}'");
            
            AddressablesOperationManager.Instance.LoadLabel(preloadLabel, (success) =>
            {
                if (success)
                {
                    if (showDebugLogs)
                        Debug.Log($"AddressablesPreloader: Successfully preloaded label '{preloadLabel}'");
                }
                else
                {
                    Debug.LogError($"AddressablesPreloader: Failed to preload label '{preloadLabel}'");
                }
                
                isPreloading = false;
                preloadComplete = true;
            });
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("AddressablesPreloader: No label to preload.");
            isPreloading = false;
            preloadComplete = true;
        }
        
        // Specific assets no longer supported
        if (specificAssets.Count > 0)
        {
            Debug.LogWarning("AddressablesPreloader: Individual asset preloading is no longer supported. Use labels instead.");
        }
    }

    public bool IsPreloadComplete()
    {
        return preloadComplete;
    }
}
