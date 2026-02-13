using UnityEngine;
using UnityEngine.InputSystem;
using MobileControllerFramework;
using SickOfIt.Core.Addressables;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// 2D top-down player controller with mobile and PC input support.
    /// Loads projectile prefab via Addressables for WebGL optimization.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TopDownController : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 2f;
        
        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 15f;
        [SerializeField] private int maxAirJumps = 3;
        [Tooltip("How fast the character falls (higher = faster). Try 3-5 for faster falling.")]
        [SerializeField] private float gravityScale = 1.8f;
        [Tooltip("Distance below collider to check for ground")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [Tooltip("Width of ground check box (smaller than collider)")]
        [SerializeField] private float groundCheckWidth = 0.1f;
        [Tooltip("Layers that count as ground (exclude player layer!)")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [Tooltip("Minimum upward velocity needed to trigger jump animation (prevents false triggers)")]
        [SerializeField] private float jumpDetectVelocityThreshold = 0.3f;
        
        [Header("Projectile Settings")]
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
        [SerializeField] private GameInputManager gameInputManager;
        
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
        private GameObject cachedProjectilePrefab;
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
            
            PreloadProjectilePrefab();
        }
        
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
                }
                else
                {
                    Debug.LogError($"Failed to load projectile prefab: {projectileAddress}");
                }
            });
        }
        
        private void Start()
        {
            if (moveAction?.action != null)
                moveAction.action.Enable();
            if (jumpAction?.action != null)
                jumpAction.action.Enable();
            if (fireAction?.action != null)
                fireAction.action.Enable();

            if (gameInputManager != null)
                gameInputManager.OnJumpPressed += OnJumpRequested;
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
            if (moveAction?.action != null)
                moveAction.action.Disable();
            if (jumpAction?.action != null)
                jumpAction.action.Disable();
            if (fireAction?.action != null)
                fireAction.action.Disable();

            if (gameInputManager != null)
                gameInputManager.OnJumpPressed -= OnJumpRequested;
        }
        
        #endregion
        
        #region Ground Check
        
        private void CheckGrounded()
        {
            Vector2 boxSize = new Vector2(groundCheckWidth, groundCheckDistance);
            Vector2 boxCenter = (Vector2)transform.position + col.offset + Vector2.down * (col.bounds.extents.y + groundCheckDistance * 0.5f);
            
            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer);
            
            if (isGrounded && !wasGrounded)
            {
                airJumpsRemaining = maxAirJumps;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
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
            
            if (moveAnalog != null && moveAnalog.gameObject.activeSelf)
            {
                moveInput = moveAnalog.GetStickPosition();
            }
            else if (dpad != null && dpad.gameObject.activeSelf)
            {
                moveInput = dpad.GetDpadDirection();
            }
            else if (arrowKeys != null && arrowKeys.gameObject.activeSelf)
            {
                moveInput = arrowKeys.GetDirection();
            }
            
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
            if (jumpAction?.action != null && jumpAction.action.WasPressedThisFrame())
            {
                jumpRequested = true;
            }
        }
        
        /// <summary>
        /// Called by GameInputManager when any input source requests a jump.
        /// </summary>
        private void OnJumpRequested()
        {
            jumpRequested = true;
        }
        
        private void ReadFireInput()
        {
            fireDirection = Vector2.zero;
            isFiring = false;
            
            if (fireAnalog != null && fireAnalog.gameObject.activeSelf)
            {
                Vector2 analogInput = fireAnalog.GetStickPosition();
                if (analogInput.magnitude > InputDeadzone)
                {
                    fireDirection = analogInput.normalized;
                    isFiring = true;
                }
            }
            
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
            
            Vector2 velocity = rb.linearVelocity;
            velocity.x = moveInput.x * moveSpeed;
            rb.linearVelocity = velocity;
            
            if (isFiring && fireDirection.magnitude > InputDeadzone)
            {
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
            
            if (isGrounded)
            {
                Jump();
                airJumpsRemaining = maxAirJumps;
            }
            else if (airJumpsRemaining > 0)
            {
                Jump();
                airJumpsRemaining--;
            }
        }
        
        private void Jump()
        {
            if (rb == null) return;
            
            Vector2 velocity = rb.linearVelocity;
            velocity.y = jumpForce;
            rb.linearVelocity = velocity;
            
            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }
        
        #endregion
        
        #region Projectile Firing
        
        private void HandleFiring()
        {
            if (fireDirection.magnitude < InputDeadzone) return;
            if (Time.time - lastFireTime < fireRate) return;
            if (!isProjectileReady) return;
            
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
            
            Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            
            GameObject projectile = Instantiate(cachedProjectilePrefab, spawnPosition, Quaternion.identity);
            
            Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, col);
            }
            
            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }
            
            if (animator != null)
            {
                animator.SetTrigger(FireHash);
            }
            
            Destroy(projectile, projectileLifetime);
        }
        
        #endregion
        
        #region Animation
        
        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            animator.SetBool(OnGroundHash, isGrounded);
            animator.SetFloat(VelocityXHash, Mathf.Abs(moveInput.x));
            
            if (rb != null)
            {
                float verticalVelocity = rb.linearVelocity.y;
                animator.SetFloat(VelocityZHash, verticalVelocity);
            }
        }
        
        #endregion
    }
}
