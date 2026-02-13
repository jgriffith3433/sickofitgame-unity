using UnityEngine;
using System.Collections.Generic;
using SickOfIt.Combat.Health;

namespace SickOfIt.Combat.Enemies.Spawning
{
    /// <summary>
    /// Spawns enemies periodically until destroyed
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnInterval = 3f;
        [SerializeField] private int maxEnemies = 5; // Max alive at once
        [SerializeField] private float spawnRadius = 1f;

        [Header("Spawner Info")]
        [SerializeField] private int enemiesSpawned = 0;
        [SerializeField] private List<GameObject> aliveEnemies = new List<GameObject>();

        private float nextSpawnTime = 0f;
        private HealthSystem healthSystem;
        private bool isActive = true;

        // Static tracking for win condition
        private static List<EnemySpawner> allSpawners = new List<EnemySpawner>();

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
            allSpawners.Add(this);
        }

        private void OnEnable()
        {
            if (healthSystem != null)
            {
                healthSystem.onDeath.AddListener(OnSpawnerDestroyed);
            }
        }

        private void OnDisable()
        {
            if (healthSystem != null)
            {
                healthSystem.onDeath.RemoveListener(OnSpawnerDestroyed);
            }
        }

        private void OnDestroy()
        {
            allSpawners.Remove(this);
        }

        private void Update()
        {
            if (!isActive || enemyPrefab == null)
                return;

            // Clean up dead enemies from list
            aliveEnemies.RemoveAll(enemy => enemy == null);

            // Spawn new enemy if needed
            if (Time.time >= nextSpawnTime && aliveEnemies.Count < maxEnemies)
            {
                SpawnEnemy();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        private void SpawnEnemy()
        {
            // Random position within spawn radius
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0);

            // Spawn enemy
            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            aliveEnemies.Add(enemy);
            enemiesSpawned++;
        }

        private void OnSpawnerDestroyed()
        {
            isActive = false;

            // Clean up all spawned enemies
            foreach (GameObject enemy in aliveEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy);
                }
            }
            aliveEnemies.Clear();

            // Check if all spawners are destroyed
            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            // Check if all spawners are destroyed
            bool allDestroyed = true;
            foreach (var spawner in allSpawners)
            {
                if (spawner != null && spawner.healthSystem != null && spawner.healthSystem.IsAlive)
                {
                    allDestroyed = false;
                    break;
                }
            }

            if (allDestroyed)
            {
                // Spawn win trigger
                WinTriggerSpawner winSpawner = FindFirstObjectByType<WinTriggerSpawner>();
                if (winSpawner != null)
                {
                    winSpawner.SpawnWinTrigger();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw spawn radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }

        /// <summary>
        /// Get count of all active spawners in the level
        /// </summary>
        public static int GetActiveSpawnerCount()
        {
            int count = 0;
            foreach (var spawner in allSpawners)
            {
                if (spawner != null && spawner.healthSystem != null && spawner.healthSystem.IsAlive)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
