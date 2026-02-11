using UnityEngine;

/// <summary>
/// Example script showing how to use Addressables for runtime asset loading
/// This replaces the old pattern of direct prefab references or Resources.Load
/// </summary>
public class AddressablesExamples : MonoBehaviour
{
    // ===== OLD WAY (Don't do this with Addressables) =====
    // [SerializeField] private GameObject enemyPrefab; // This creates dependencies!
    // void OldWay() { Instantiate(enemyPrefab); }

    // ===== NEW WAY (Addressables) =====
    // Reference by string address instead

    private void Start()
    {
        // Example: Load and spawn an enemy when level starts
        SpawnEnemyExample();
    }

    // ===== EXAMPLE 1: Spawn a Prefab =====
    void SpawnEnemyExample()
    {
        // Instead of: Instantiate(enemyPrefab);
        // Use this:
        
        AddressablesAssetLoader.Instance.InstantiatePrefab(
            address: "Enemy_Basic",  // The Addressable name/address
            onComplete: (enemyObject) =>
            {
                if (enemyObject != null)
                {
                    Debug.Log("Enemy spawned!");
                    // Do setup on the spawned enemy
                    enemyObject.transform.position = new Vector3(0, 0, 0);
                }
            }
        );
    }

    // ===== EXAMPLE 2: Spawn at Specific Position =====
    void SpawnAtPosition()
    {
        Vector3 spawnPos = new Vector3(10, 0, 10);
        Quaternion spawnRot = Quaternion.identity;

        AddressablesAssetLoader.Instance.InstantiatePrefabAtPosition(
            address: "Powerup_Health",
            position: spawnPos,
            rotation: spawnRot,
            onComplete: (obj) =>
            {
                if (obj != null)
                {
                    Debug.Log("Powerup spawned at specific position!");
                }
            }
        );
    }

    // ===== EXAMPLE 3: Load Asset Then Instantiate Multiple Times =====
    void SpawnMultipleEnemies()
    {
        // Load the prefab once, then instantiate it multiple times
        AddressablesAssetLoader.Instance.LoadAsset<GameObject>(
            address: "Enemy_Basic",
            onComplete: (prefab) =>
            {
                if (prefab != null)
                {
                    // Now instantiate it multiple times
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 pos = new Vector3(i * 2, 0, 0);
                        Instantiate(prefab, pos, Quaternion.identity);
                    }
                }
            }
        );
    }

    // ===== EXAMPLE 4: Load Scriptable Object Data =====
    void LoadGameData()
    {
        AddressablesAssetLoader.Instance.LoadAsset<ScriptableObject>(
            address: "LevelConfig_1",
            onComplete: (config) =>
            {
                if (config != null)
                {
                    Debug.Log("Level config loaded!");
                    // Use the data from scriptable object
                }
            }
        );
    }

    // ===== EXAMPLE 5: Load Multiple Assets by Label =====
    void LoadAllEnemyPrefabs()
    {
        // First, you need to assign labels to your assets in Addressables
        // (Select assets, set label to "Enemy" in Addressables window)
        
        AddressablesAssetLoader.Instance.LoadAssetsByLabel<GameObject>(
            label: "Enemy",
            onComplete: (enemyPrefabs) =>
            {
                if (enemyPrefabs != null)
                {
                    Debug.Log($"Loaded {enemyPrefabs.Count} enemy prefabs");
                    
                    // Spawn one of each type
                    foreach (var prefab in enemyPrefabs)
                    {
                        Instantiate(prefab);
                    }
                }
            }
        );
    }

    // ===== EXAMPLE 6: Preload Assets for Smooth Gameplay =====
    void PreloadNextWaveEnemies()
    {
        // Load these assets in the background before you need them
        AddressablesAssetLoader.Instance.PreloadAsset<GameObject>("Enemy_Boss");
        AddressablesAssetLoader.Instance.PreloadAsset<GameObject>("Enemy_Fast");
        
        // Later, when you need to spawn them, they'll load instantly
        // because they're already in memory
    }

    // ===== EXAMPLE 7: Preload by Label =====
    void PreloadAllPowerups()
    {
        // Preload all assets with the "Powerup" label
        AddressablesAssetLoader.Instance.PreloadAssetsByLabel("Powerup");
    }

    // ===== EXAMPLE 8: Memory Management =====
    void OnLevelComplete()
    {
        // Unity's Addressables handles cleanup automatically via GC
        // Downloaded bundles are cached on disk by the browser
        // No manual release needed - prevents accidental shared dependency unload (pink shaders)
        // Assets will be unloaded by Unity's GC when no longer referenced
        Debug.Log("Level complete - Unity will handle asset cleanup automatically");
    }

    // ===== EXAMPLE 9: Load Material at Runtime =====
    void ChangeMaterial()
    {
        AddressablesAssetLoader.Instance.LoadAsset<Material>(
            address: "Material_Gold",
            onComplete: (material) =>
            {
                if (material != null)
                {
                    GetComponent<Renderer>().material = material;
                }
            }
        );
    }

    // ===== EXAMPLE 10: Integration with Existing Spawner =====
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private string enemyAddress = "Enemy_Basic";
        [SerializeField] private float spawnInterval = 2f;

        private void Start()
        {
            // Preload the prefab
            AddressablesAssetLoader.Instance.PreloadAsset<GameObject>(enemyAddress);
            
            // Start spawning
            InvokeRepeating(nameof(SpawnEnemy), spawnInterval, spawnInterval);
        }

        void SpawnEnemy()
        {
            AddressablesAssetLoader.Instance.InstantiatePrefab(
                enemyAddress,
                (enemy) =>
                {
                    if (enemy != null)
                    {
                        enemy.transform.position = transform.position;
                    }
                }
            );
        }
    }

    // ===== EXAMPLE 11: Load Audio Clip =====
    void PlaySound()
    {
        AddressablesAssetLoader.Instance.LoadAsset<AudioClip>(
            address: "SFX_Explosion",
            onComplete: (clip) =>
            {
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, transform.position);
                }
            }
        );
    }

    // ===== EXAMPLE 12: Conditional Loading Based on Player Progress =====
    void LoadWeaponBasedOnLevel()
    {
        int playerLevel = GameProgressManager.Instance.GetCurrentLevel();
        
        string weaponAddress = playerLevel switch
        {
            1 => "Weapon_Pistol",
            2 => "Weapon_Shotgun",
            3 => "Weapon_Rifle",
            _ => "Weapon_Pistol"
        };

        AddressablesAssetLoader.Instance.InstantiatePrefab(
            weaponAddress,
            (weapon) =>
            {
                if (weapon != null)
                {
                    // Equip weapon
                    weapon.transform.SetParent(transform);
                }
            },
            parent: transform
        );
    }
}
