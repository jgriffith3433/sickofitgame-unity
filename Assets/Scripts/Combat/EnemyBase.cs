using UnityEngine;

/// <summary>
/// Base class for all enemy types
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected int contactDamage = 1;
    
    [Header("Detection")]
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected LayerMask playerLayer;
    
    protected Transform player;
    protected Rigidbody2D rb;
    protected HealthSystem healthSystem;
    protected SpriteRenderer spriteRenderer;
    
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Setup rigidbody for 2D movement
        rb.gravityScale = 0; // Top-down style, no gravity
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        
        // Find player (including inactive)
        var allObjects = FindObjectsOfType<GameObject>(true);
        var playerObj = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }
    
    protected virtual void Update()
    {
        if (player == null || !healthSystem.IsAlive)
            return;
        
        // Check if player is in range
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= detectionRange)
        {
            UpdateBehavior();
        }
    }
    
    /// <summary>
    /// Override this for specific enemy behavior
    /// </summary>
    protected abstract void UpdateBehavior();
    
    /// <summary>
    /// Deal damage to player on contact
    /// </summary>
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            HealthSystem playerHealth = collision.gameObject.GetComponent<HealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
            }
        }
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
