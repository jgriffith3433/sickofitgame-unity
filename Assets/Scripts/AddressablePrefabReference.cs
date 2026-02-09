using UnityEngine;

/// <summary>
/// Drop-in replacement for direct prefab references
/// Add this component to any GameObject that spawns prefabs
/// Automatically handles Addressables without changing your existing spawning code
/// </summary>
public class AddressablePrefabReference : MonoBehaviour
{
    [Header("Addressable Settings")]
    [SerializeField] private string addressableAddress;
    [Tooltip("Set to true to preload this prefab when the scene starts")]
    [SerializeField] private bool preloadOnStart = false;

    private GameObject cachedPrefab;
    private bool isLoading = false;

    private void Start()
    {
        if (preloadOnStart)
        {
            PreloadPrefab();
        }
    }

    /// <summary>
    /// Preload the prefab into memory (call this early for faster spawns later)
    /// </summary>
    public void PreloadPrefab()
    {
        if (cachedPrefab != null || isLoading) return;

        isLoading = true;
        AddressablesAssetLoader.Instance.LoadAsset<GameObject>(addressableAddress, (prefab) =>
        {
            cachedPrefab = prefab;
            isLoading = false;
            if (prefab != null)
            {
                Debug.Log($"Preloaded prefab: {addressableAddress}");
            }
        });
    }

    /// <summary>
    /// Instantiate the prefab (loads if not already loaded)
    /// </summary>
    public void Instantiate(System.Action<GameObject> onComplete, Transform parent = null)
    {
        if (cachedPrefab != null)
        {
            // Already loaded, spawn instantly
            GameObject instance = GameObject.Instantiate(cachedPrefab, parent);
            onComplete?.Invoke(instance);
        }
        else
        {
            // Load then spawn
            AddressablesAssetLoader.Instance.LoadAsset<GameObject>(addressableAddress, (prefab) =>
            {
                if (prefab != null)
                {
                    cachedPrefab = prefab;
                    GameObject instance = GameObject.Instantiate(prefab, parent);
                    onComplete?.Invoke(instance);
                }
                else
                {
                    Debug.LogError($"Failed to load prefab: {addressableAddress}");
                    onComplete?.Invoke(null);
                }
            });
        }
    }

    /// <summary>
    /// Instantiate at specific position and rotation
    /// </summary>
    public void InstantiateAt(Vector3 position, Quaternion rotation, System.Action<GameObject> onComplete, Transform parent = null)
    {
        Instantiate((obj) =>
        {
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            onComplete?.Invoke(obj);
        }, parent);
    }

    /// <summary>
    /// Get the cached prefab (returns null if not loaded yet)
    /// </summary>
    public GameObject GetPrefab()
    {
        return cachedPrefab;
    }

    /// <summary>
    /// Check if prefab is loaded and ready
    /// </summary>
    public bool IsLoaded()
    {
        return cachedPrefab != null;
    }
}

// ===== EXAMPLE USAGE =====
// 
// Before (Old Way):
// public class OldSpawner : MonoBehaviour
// {
//     [SerializeField] private GameObject enemyPrefab;
//     
//     void SpawnEnemy()
//     {
//         Instantiate(enemyPrefab);
//     }
// }
//
// After (New Way):
// public class NewSpawner : MonoBehaviour
// {
//     [SerializeField] private AddressablePrefabReference enemyPrefab;
//     
//     void SpawnEnemy()
//     {
//         enemyPrefab.Instantiate((enemy) => 
//         {
//             // Enemy spawned!
//         });
//     }
// }
//
// Setup in Inspector:
// 1. Create empty child GameObject under your spawner
// 2. Add AddressablePrefabReference component
// 3. Set "Addressable Address" to your prefab's address (e.g., "Enemy_Basic")
// 4. Check "Preload On Start" if you want instant spawning
// 5. Drag this GameObject to your spawner's enemyPrefab field
