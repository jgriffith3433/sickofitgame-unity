using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MobileControllerFramework
{
    /// <summary>
    /// Controller for Yooba character - ADDRESSABLES VERSION
    /// Modified to load projectile prefab via Addressables for WebGL optimization
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TopDownController : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        
        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 15f;
        [SerializeField] private int maxAirJumps = 3;
        [Tooltip("How fast the character falls (higher = faster). Try 3-5 for faster falling.")]
        [SerializeField] private float gravityScale = 1.8f;
        [Tooltip("Distance below collider to check for ground")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [Tooltip("Width of ground check box (smaller than collider)")]
        [SerializeField] private float groundCheckWidth = 0.8f;
        [Tooltip("Layers that count as ground (exclude player layer!)")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [Tooltip("Minimum upward velocity needed to trigger jump animation (prevents false triggers)")]
        [SerializeField] private float jumpDetectVelocityThreshold = 0.3f;
        
        [Header("Projectile Settings - ADDRESSABLES")]
        // OLD: [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Addressable address of the projectile prefab (e.g., 'Projectile_Player')")]
        [SerializeField] private string projectileAddress = "Projectile_Player";
        [Tooltip("Optional: Transform where projectiles spawn. If null, spawns at player position.")]
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float fireRate = 0.25f;
        [SerializeField] private float projectileLifetime = 5f;
        
        [Header("Mobile Input")]
        [Tooltip("Analog stick for movement. Add AnalogTapJump component for tap-to-jump.")]
        [SerializeField] private Analog moveAnalog;
        [Tooltip("Analog stick for aiming/firing. Movement beyond threshold automatically fires.")]
        [SerializeField] private Analog fireAnalog;
        [SerializeField] private Dpad dpad;
        [SerializeField] private ArrowKeys arrowKeys;
        
        [Header("PC Input Actions")]
        [Tooltip("WASD/Arrow keys movement action")]
        [SerializeField] private InputActionReference moveAction;
        [Tooltip("Space key - bind to 'Player/Jump' action")]
        [SerializeField] private InputActionReference jumpAction;
        [Tooltip("Left mouse button - bind to 'Player/Fire' action")]
        [SerializeField] private InputActionReference fireAction;
        
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Camera mainCamera;
        
        #endregion
        
        #region Animator Parameter Hashes
        
        private static readonly int OnGroundHash = Animator.StringToHash("OnGround");
        private static readonly int VelocityXHash = Animator.StringToHash("Velocity_X");
        private static readonly int VelocityZHash = Animator.StringToHash("Velocity_Z");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        
        #endregion
        
        #region Private Fields
        
        private Rigidbody2D rb;
        private Collider2D col;
        
        // Movement
        private Vector2 moveInput;
        private Vector3 facingRight = Vector3.one;
        private Vector3 facingLeft = new Vector3(-1, 1, 1);
        
        // Jump
        private bool isGrounded;
        private int airJumpsRemaining;
        private bool jumpRequested;
        
        // Firing
        private Vector2 fireDirection;
        private bool isFiring;
        private float lastFireTime;
        
        // Addressables
        private GameObject cachedProjectilePrefab; // Cached loaded prefab
        private bool isProjectileReady = false;
        
        // Input dead zone
        private const float InputDeadzone = 0.1f;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            
            if (rb != null)
            {
                rb.gravityScale = gravityScale;
            }
            
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            // PRELOAD PROJECTILE PREFAB - This is critical for smooth gameplay!
            PreloadProjectilePrefab();
        }
        
        /// <summary>
        /// Preload the projectile prefab so firing is instant
        /// </summary>
        private void PreloadProjectilePrefab()
        {
            if (string.IsNullOrEmpty(projectileAddress))
            {
                Debug.LogError("TopDownController: projectileAddress is not set!");
                return;
            }
            
            AddressablesAssetLoader.Instance.LoadAsset<GameObject>(projectileAddress, (prefab) =>
            {
                if (prefab != null)
                {
                    cachedProjectilePrefab = prefab;
                    isProjectileReady = true;
                    Debug.Log($"Projectile prefab preloaded: {projectileAddress}");
                }
                else
                {
                    Debug.LogError($"Failed to load projectile prefab: {projectileAddress}");
                }
            });
        }
        
        private void Start()
        {
            // Enable input actions
            if (moveAction?.action != null)
                moveAction.action.Enable();
            if (jumpAction?.action != null)
                jumpAction.action.Enable();
            if (fireAction?.action != null)
                fireAction.action.Enable();
        }
        
        private void Update()
        {
            ReadMoveInput();
            ReadJumpInput();
            ReadFireInput();
            CheckGrounded();
            HandleJump();
            HandleFiring();
            UpdateAnimator();
        }
        
        private void FixedUpdate()
        {
            HandleMovement();
        }
        
        private void OnDisable()
        {
            // Disable input actions
            if (moveAction?.action != null)
                moveAction.action.Disable();
            if (jumpAction?.action != null)
                jumpAction.action.Disable();
            if (fireAction?.action != null)
                fireAction.action.Disable();
        }
        
        #endregion
        
        #region Ground Check
        
        private void CheckGrounded()
        {
            // Cast a box downward from the collider
            Vector2 boxSize = new Vector2(groundCheckWidth, groundCheckDistance);
            Vector2 boxCenter = (Vector2)transform.position + col.offset + Vector2.down * (col.bounds.extents.y + groundCheckDistance * 0.5f);
            
            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer);
            
            // Reset air jumps when landing
            if (isGrounded && !wasGrounded)
            {
                airJumpsRemaining = maxAirJumps;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw ground check box
            if (col != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Vector2 boxSize = new Vector2(groundCheckWidth, groundCheckDistance);
                Vector2 boxCenter = (Vector2)transform.position + col.offset + Vector2.down * (col.bounds.extents.y + groundCheckDistance * 0.5f);
                Gizmos.DrawWireCube(boxCenter, boxSize);
            }
        }
        
        #endregion
        
        #region Input Reading
        
        private void ReadMoveInput()
        {
            moveInput = Vector2.zero;
            
            // Mobile analog stick
            if (moveAnalog != null && moveAnalog.gameObject.activeSelf)
            {
                moveInput = moveAnalog.GetStickPosition();
            }
            // Mobile D-pad
            else if (dpad != null && dpad.gameObject.activeSelf)
            {
                moveInput = dpad.GetDpadDirection();
            }
            // Arrow keys
            else if (arrowKeys != null && arrowKeys.gameObject.activeSelf)
            {
                moveInput = arrowKeys.GetDirection();
            }
            
            // PC keyboard/gamepad input
            if (moveAction?.action != null && moveAction.action.enabled)
            {
                Vector2 keyboardInput = moveAction.action.ReadValue<Vector2>();
                if (keyboardInput.magnitude > InputDeadzone && moveInput.magnitude < InputDeadzone)
                {
                    moveInput = keyboardInput;
                }
            }
        }
        
        private void ReadJumpInput()
        {
            // GlobalTapJump calls OnJumpButtonPressed() directly, so we don't poll here
            // Just check PC jump input
            if (jumpAction?.action != null && jumpAction.action.WasPressedThisFrame())
            {
                jumpRequested = true;
            }
        }
        
        /// <summary>
        /// Called by GlobalTapJump when a tap-to-jump is detected
        /// </summary>
        public void OnJumpButtonPressed()
        {
            jumpRequested = true;
        }
        
        private void ReadFireInput()
        {
            fireDirection = Vector2.zero;
            isFiring = false;
            
            // Mobile fire analog stick
            if (fireAnalog != null && fireAnalog.gameObject.activeSelf)
            {
                Vector2 analogInput = fireAnalog.GetStickPosition();
                if (analogInput.magnitude > InputDeadzone)
                {
                    fireDirection = analogInput.normalized;
                    isFiring = true;
                }
            }
            
            // PC fire input
            if (fireAction?.action != null && fireAction.action.IsPressed())
            {
                isFiring = true;
                fireDirection = GetMouseDirectionFromPlayer();
            }
        }
        
        private Vector2 GetMouseDirectionFromPlayer()
        {
            if (mainCamera == null || Mouse.current == null)
                return Vector2.zero;
            
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldMousePos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));
            Vector2 direction = ((Vector2)worldMousePos - (Vector2)transform.position).normalized;
            
            return direction;
        }
        
        #endregion
        
        #region Movement & Jump
        
        private void HandleMovement()
        {
            if (rb == null) return;
            
            // Apply horizontal movement
            Vector2 velocity = rb.linearVelocity;
            velocity.x = moveInput.x * moveSpeed;
            rb.linearVelocity = velocity;
            
            // Flip sprite based on shooting direction (priority) or movement
            if (isFiring && fireDirection.magnitude > InputDeadzone)
            {
                // Face the direction we're shooting
                if (fireDirection.x > 0)
                {
                    transform.localScale = facingRight;
                }
                else if (fireDirection.x < 0)
                {
                    transform.localScale = facingLeft;
                }
            }
            else if (moveInput.x > InputDeadzone)
            {
                transform.localScale = facingRight;
            }
            else if (moveInput.x < -InputDeadzone)
            {
                transform.localScale = facingLeft;
            }
        }
        
        private void HandleJump()
        {
            if (!jumpRequested) return;
            jumpRequested = false;
            
            // Ground jump
            if (isGrounded)
            {
                Jump();
                airJumpsRemaining = maxAirJumps;
            }
            // Air jump (Kirby-style)
            else if (airJumpsRemaining > 0)
            {
                Jump();
                airJumpsRemaining--;
            }
        }
        
        private void Jump()
        {
            if (rb == null) return;
            
            // Set vertical velocity to jump force
            Vector2 velocity = rb.linearVelocity;
            velocity.y = jumpForce;
            rb.linearVelocity = velocity;
            
            // Trigger jump animation
            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }
        
        #endregion
        
        #region Projectile Firing - ADDRESSABLES VERSION
        
        private void HandleFiring()
        {
            if (fireDirection.magnitude < InputDeadzone) return;
            if (Time.time - lastFireTime < fireRate) return;
            if (!isProjectileReady) return; // Don't fire if projectile not loaded yet
            
            FireProjectile(fireDirection);
        }
        
        private void FireProjectile(Vector2 direction)
        {
            if (cachedProjectilePrefab == null)
            {
                Debug.LogWarning("Projectile prefab not loaded yet!");
                return;
            }
            
            lastFireTime = Time.time;
            
            // Spawn projectile at spawn point (or player position if not set)
            Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            
            // MODIFIED: Instantiate from cached prefab instead of direct reference
            GameObject projectile = Instantiate(cachedProjectilePrefab, spawnPosition, Quaternion.identity);
            
            // Ignore collision with player
            Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, col);
            }
            
            // Apply velocity
            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }
            
            // Trigger fire animation
            if (animator != null)
            {
                animator.SetTrigger(FireHash);
            }
            
            // Clean up after lifetime
            Destroy(projectile, projectileLifetime);
        }
        
        #endregion
        
        #region Animation
        
        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            // Update ground state
            animator.SetBool(OnGroundHash, isGrounded);
            
            // Update velocity (for blend tree)
            animator.SetFloat(VelocityXHash, Mathf.Abs(moveInput.x));
            
            // Update vertical velocity for jump detection
            if (rb != null)
            {
                float verticalVelocity = rb.linearVelocity.y;
                animator.SetFloat(VelocityZHash, verticalVelocity);
            }
        }
        
        #endregion
    }
}
