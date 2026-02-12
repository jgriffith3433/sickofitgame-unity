using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Enemy that shoots projectiles at the player
/// </summary>
public class EnemyShooter : EnemyBase
{
    [Header("Shooter Settings")]
    [SerializeField] private string projectileAddress = "Projectile_Enemy";
    [SerializeField] private float fireRate = 2f; // Shots per second
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private bool rotateToFacePlayer = false;
    
    private float nextFireTime = 0f;
    private GameObject cachedProjectilePrefab;
    private bool isProjectileReady = false;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Load projectile prefab
        LoadProjectilePrefab();
        
        // Create fire point if not assigned
        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.zero;
            firePoint = firePointObj.transform;
        }
    }
    
    private void LoadProjectilePrefab()
    {
        Addressables.LoadAssetAsync<GameObject>(projectileAddress).Completed += (AsyncOperationHandle<GameObject> handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                cachedProjectilePrefab = handle.Result;
                isProjectileReady = true;
            }
            else
            {
                Debug.LogError($"Failed to load projectile: {projectileAddress}");
            }
        };
    }
    
    protected override void UpdateBehavior()
    {
        if (player == null || !isProjectileReady)
            return;
        
        // Rotate to face player
        if (rotateToFacePlayer)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        
        // Shoot at player
        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }
    
    private void Shoot()
    {
        if (cachedProjectilePrefab == null || firePoint == null)
            return;
        
        // Calculate direction to player
        Vector2 direction = (player.position - firePoint.position).normalized;
        
        // Spawn projectile
        GameObject projectile = Instantiate(cachedProjectilePrefab, firePoint.position, Quaternion.identity);
        
        // Set projectile velocity
        Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
        if (projectileRb != null)
        {
            projectileRb.linearVelocity = direction * projectileSpeed;
        }
        
        // Make sure it can damage player
        DamageDealer damageDealer = projectile.GetComponent<DamageDealer>();
        if (damageDealer != null)
        {
            damageDealer.SetTargetTags("Player");
        }
        
        // Auto-destroy after a while
        Destroy(projectile, 5f);
    }
}
