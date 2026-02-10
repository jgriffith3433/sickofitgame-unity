using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

/// <summary>
/// Manages loading prefabs and assets at runtime using Addressables
/// Use this instead of Resources.Load or direct prefab references for WebGL optimization
/// </summary>
public class AddressablesAssetLoader : MonoBehaviour
{
    private static AddressablesAssetLoader _instance;
    public static AddressablesAssetLoader Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AddressablesAssetLoader");
                _instance = go.AddComponent<AddressablesAssetLoader>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Cache of loaded assets to avoid reloading
    private Dictionary<string, AsyncOperationHandle> loadedAssets = new Dictionary<string, AsyncOperationHandle>();

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

    #region Load and Instantiate

    /// <summary>
    /// Load and instantiate a prefab by its Addressable address
    /// </summary>
    /// <param name="address">Addressable address/name of the prefab</param>
    /// <param name="onComplete">Callback with instantiated GameObject</param>
    /// <param name="parent">Optional parent transform</param>
    public void InstantiatePrefab(string address, System.Action<GameObject> onComplete, Transform parent = null)
    {
        StartCoroutine(InstantiatePrefabAsync(address, onComplete, parent));
    }

    private System.Collections.IEnumerator InstantiatePrefabAsync(string address, System.Action<GameObject> onComplete, Transform parent)
    {
        var handle = Addressables.InstantiateAsync(address, parent);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            onComplete?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogError($"Failed to instantiate prefab: {address}. Error: {handle.OperationException}");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>
    /// Load and instantiate a prefab at a specific position and rotation
    /// </summary>
    public void InstantiatePrefabAtPosition(string address, Vector3 position, Quaternion rotation, 
        System.Action<GameObject> onComplete, Transform parent = null)
    {
        StartCoroutine(InstantiatePrefabAtPositionAsync(address, position, rotation, onComplete, parent));
    }

    private System.Collections.IEnumerator InstantiatePrefabAtPositionAsync(string address, Vector3 position, 
        Quaternion rotation, System.Action<GameObject> onComplete, Transform parent)
    {
        var handle = Addressables.InstantiateAsync(address, position, rotation, parent);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            onComplete?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogError($"Failed to instantiate prefab: {address}. Error: {handle.OperationException}");
            onComplete?.Invoke(null);
        }
    }

    #endregion

    #region Load Assets (No Instantiation)

    /// <summary>
    /// Load an asset (prefab, scriptable object, material, etc.) without instantiating
    /// Useful when you want to instantiate multiple times or access data
    /// </summary>
    public void LoadAsset<T>(string address, System.Action<T> onComplete) where T : Object
    {
        StartCoroutine(LoadAssetAsync(address, onComplete));
    }

    private System.Collections.IEnumerator LoadAssetAsync<T>(string address, System.Action<T> onComplete) where T : Object
    {
        // Delegate to AddressablesOperationManager for centralized tracking and deduplication
        bool callbackInvoked = false;
        T result = null;
        
        AddressablesOperationManager.Instance.LoadAsset<T>(address, (asset) =>
        {
            result = asset;
            callbackInvoked = true;
        });
        
        // Wait for callback
        while (!callbackInvoked)
        {
            yield return null;
        }
        
        // Update local cache reference
        if (result != null)
        {
            // Note: The manager handles the actual caching
            if (!loadedAssets.ContainsKey(address))
            {
                loadedAssets[address] = default; // Placeholder for compatibility
            }
        }
        
        onComplete?.Invoke(result);
    }

    /// <summary>
    /// Load a prefab and instantiate it manually (gives you more control)
    /// </summary>
    public void LoadPrefabThenInstantiate(string address, System.Action<GameObject> onComplete, Transform parent = null)
    {
        LoadAsset<GameObject>(address, (prefab) =>
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, parent);
                onComplete?.Invoke(instance);
            }
            else
            {
                onComplete?.Invoke(null);
            }
        });
    }

    #endregion

    #region Load Multiple Assets

    /// <summary>
    /// Load multiple assets by label (e.g., all enemies, all powerups)
    /// </summary>
    public void LoadAssetsByLabel<T>(string label, System.Action<IList<T>> onComplete) where T : Object
    {
        StartCoroutine(LoadAssetsByLabelAsync(label, onComplete));
    }

    private System.Collections.IEnumerator LoadAssetsByLabelAsync<T>(string label, System.Action<IList<T>> onComplete) where T : Object
    {
        // Delegate to AddressablesOperationManager for centralized tracking and deduplication
        // The manager handles:
        // - Preventing duplicate concurrent loads
        // - Type-specific loading (critical for Sprites)
        // - Caching results
        // - Queueing callbacks if already loading
        
        bool callbackInvoked = false;
        IList<T> result = null;
        
        AddressablesOperationManager.Instance.LoadLabel<T>(label, (assets) =>
        {
            result = assets;
            callbackInvoked = true;
        });
        
        // Wait for callback from manager
        while (!callbackInvoked)
        {
            yield return null;
        }
        
        onComplete?.Invoke(result);
    }

    #endregion

    #region Preload Assets

    /// <summary>
    /// Preload an asset into memory without using it yet (for faster access later)
    /// </summary>
    public void PreloadAsset<T>(string address) where T : Object
    {
        if (!loadedAssets.ContainsKey(address))
        {
            StartCoroutine(PreloadAssetAsync<T>(address));
        }
    }

    private System.Collections.IEnumerator PreloadAssetAsync<T>(string address) where T : Object
    {
        // Delegate to AddressablesOperationManager for centralized tracking
        AddressablesOperationManager.Instance.LoadAsset<T>(address, (asset) =>
        {
            if (asset != null)
            {
                Debug.Log($"Preloaded asset: {address}");
            }
            else
            {
                Debug.LogError($"Failed to preload asset: {address}");
            }
        });
        
        // Wait for load to complete
        while (AddressablesOperationManager.Instance.IsLoading(address))
        {
            yield return null;
        }
    }

    /// <summary>
    /// Preload multiple assets by label
    /// </summary>
    public void PreloadAssetsByLabel(string label)
    {
        AddressablesOperationManager.Instance.LoadLabel(label, (success) =>
        {
            if (success)
            {
                Debug.Log($"Preloaded assets with label: {label}");
            }
            else
            {
                Debug.LogError($"Failed to preload assets with label: {label}");
            }
        });
    }

    #endregion

    #region Release/Unload

    /// <summary>
    /// Release a loaded asset from memory
    /// </summary>
    public void ReleaseAsset(string address)
    {
        if (loadedAssets.ContainsKey(address))
        {
            var handle = loadedAssets[address];
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            loadedAssets.Remove(address);
        }
    }

    /// <summary>
    /// Release all loaded assets
    /// Call this when transitioning between major sections (e.g., leaving a level)
    /// </summary>
    public void ReleaseAllAssets()
    {
        foreach (var kvp in loadedAssets)
        {
            if (kvp.Value.IsValid())
            {
                Addressables.Release(kvp.Value);
            }
        }
        loadedAssets.Clear();
        Debug.Log("Released all loaded assets");
    }

    #endregion

    private void OnDestroy()
    {
        ReleaseAllAssets();
    }
}
