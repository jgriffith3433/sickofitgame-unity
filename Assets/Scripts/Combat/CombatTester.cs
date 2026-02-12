using UnityEngine;

/// <summary>
/// Simple test script to damage enemies/player with keyboard
/// Attach to any GameObject for testing
/// </summary>
public class CombatTester : MonoBehaviour
{
    [Header("Testing Controls")]
    [SerializeField] private KeyCode damagePlayerKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode healPlayerKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode damageNearestEnemyKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode killAllEnemiesKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode spawnWinTriggerKey = KeyCode.Alpha5;
    
    [Header("Settings")]
    [SerializeField] private int testDamageAmount = 1;
    [SerializeField] private int testHealAmount = 1;
    [SerializeField] private float enemySearchRadius = 100f;
    
    private void Update()
    {
        // Damage player
        if (Input.GetKeyDown(damagePlayerKey))
        {
            DamagePlayer();
        }
        
        // Heal player
        if (Input.GetKeyDown(healPlayerKey))
        {
            HealPlayer();
        }
        
        // Damage nearest enemy
        if (Input.GetKeyDown(damageNearestEnemyKey))
        {
            DamageNearestEnemy();
        }
        
        // Kill all enemies
        if (Input.GetKeyDown(killAllEnemiesKey))
        {
            KillAllEnemies();
        }
        
        // Force spawn win trigger
        if (Input.GetKeyDown(spawnWinTriggerKey))
        {
            ForceSpawnWinTrigger();
        }
    }
    
    private void DamagePlayer()
    {
        var allObjects = FindObjectsOfType<GameObject>(true);
        GameObject player = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (player != null)
        {
            HealthSystem health = player.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(testDamageAmount);
                Debug.Log($"[TEST] Damaged player for {testDamageAmount}. HP: {health.CurrentHealth}/{health.MaxHealth}");
            }
        }
    }
    
    private void HealPlayer()
    {
        var allObjects = FindObjectsOfType<GameObject>(true);
        GameObject player = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (player != null)
        {
            HealthSystem health = player.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.Heal(testHealAmount);
                Debug.Log($"[TEST] Healed player for {testHealAmount}. HP: {health.CurrentHealth}/{health.MaxHealth}");
            }
        }
    }
    
    private void DamageNearestEnemy()
    {
        var allObjects = FindObjectsOfType<GameObject>(true);
        GameObject player = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (player == null) return;
        
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject nearest = null;
        float nearestDistance = enemySearchRadius;
        
        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(player.transform.position, enemy.transform.position);
            if (dist < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = dist;
            }
        }
        
        if (nearest != null)
        {
            HealthSystem health = nearest.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(testDamageAmount);
                Debug.Log($"[TEST] Damaged {nearest.name} for {testDamageAmount}. HP: {health.CurrentHealth}/{health.MaxHealth}");
            }
        }
        else
        {
            Debug.Log("[TEST] No enemies found nearby.");
        }
    }
    
    private void KillAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int count = 0;
        
        foreach (GameObject enemy in enemies)
        {
            HealthSystem health = enemy.GetComponent<HealthSystem>();
            if (health != null && health.IsAlive)
            {
                health.TakeDamage(9999);
                count++;
            }
        }
        
        Debug.Log($"[TEST] Killed {count} enemies.");
    }
    
    private void ForceSpawnWinTrigger()
    {
        WinTriggerSpawner spawner = FindFirstObjectByType<WinTriggerSpawner>();
        if (spawner != null)
        {
            spawner.SpawnWinTrigger();
            Debug.Log("[TEST] Forced win trigger spawn.");
        }
        else
        {
            Debug.LogWarning("[TEST] No WinTriggerSpawner found in scene!");
        }
    }
    
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== COMBAT TESTER ===");
        GUILayout.Label($"{damagePlayerKey}: Damage Player");
        GUILayout.Label($"{healPlayerKey}: Heal Player");
        GUILayout.Label($"{damageNearestEnemyKey}: Damage Nearest Enemy");
        GUILayout.Label($"{killAllEnemiesKey}: Kill All Enemies");
        GUILayout.Label($"{spawnWinTriggerKey}: Spawn Win Trigger");
        GUILayout.EndArea();
    }
}
