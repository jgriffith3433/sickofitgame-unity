using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MobileControllerFramework
{
    /// <summary>
    /// Controller for Yooba character - 3D model in a 2D game with Kirby-like jump mechanics
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
        [SerializeField] private int maxAirJumps = 3; // Kirby-style air jumps
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
        
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;
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
        
        // Input dead zone
        private const float InputDeadzone = 0.1f;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            rb.gravityScale = gravityScale;
            
            if (mainCamera == null)
                mainCamera = Camera.main;
        }
        
        private void OnEnable()
        {
            EnableInputActions();
        }
        
        private void OnDisable()
        {
            DisableInputActions();
            ResetAnimatorState();
        }
        
        private void Update()
        {
            ReadMovementInput();
            ReadFireInput();
            CheckGroundedState();
            UpdateFacingDirection();
            HandleFiring();
            UpdateAnimator();
        }
        
        private void FixedUpdate()
        {
            // Ensure gravity scale is synced (allows runtime adjustment in inspector)
            if (rb.gravityScale != gravityScale)
            {
                rb.gravityScale = gravityScale;
            }
            
            ApplyMovement();
            ApplyJump();
        }
        
        #endregion
        
        #region Input System Setup
        
        private void EnableInputActions()
        {
            if (moveAction?.action != null)
            {
                moveAction.action.Enable();
            }
            
            if (jumpAction?.action != null)
            {
                jumpAction.action.performed -= OnJumpPerformed;
                jumpAction.action.performed += OnJumpPerformed;
                jumpAction.action.Enable();
            }
            
            if (fireAction?.action != null)
            {
                fireAction.action.started -= OnFireStarted;
                fireAction.action.canceled -= OnFireCanceled;
                fireAction.action.started += OnFireStarted;
                fireAction.action.canceled += OnFireCanceled;
                fireAction.action.Enable();
            }
        }
        
        private void DisableInputActions()
        {
            if (moveAction?.action != null)
            {
                moveAction.action.Disable();
            }
            
            if (jumpAction?.action != null)
            {
                jumpAction.action.performed -= OnJumpPerformed;
                jumpAction.action.Disable();
            }
            
            if (fireAction?.action != null)
            {
                fireAction.action.started -= OnFireStarted;
                fireAction.action.canceled -= OnFireCanceled;
                fireAction.action.Disable();
            }
        }
        
        #endregion
        
        #region Input Reading
        
        private void ReadMovementInput()
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
            
            // PC keyboard/gamepad input (can work simultaneously with mobile)
            if (moveAction?.action != null && moveAction.action.enabled)
            {
                Vector2 keyboardInput = moveAction.action.ReadValue<Vector2>();
                if (keyboardInput.magnitude > InputDeadzone && moveInput.magnitude < InputDeadzone)
                {
                    moveInput = keyboardInput;
                }
            }
        }
        
        private void ReadFireInput()
        {
            fireDirection = Vector2.zero;
            
            // Mobile fire analog stick
            if (fireAnalog != null && fireAnalog.gameObject.activeSelf)
            {
                Vector2 analogInput = fireAnalog.GetStickPosition();
                if (analogInput.magnitude > InputDeadzone)
                {
                    fireDirection = analogInput.normalized;
                }
            }
            
            // PC mouse aiming (when fire button is held)
            if (isFiring && fireDirection.magnitude < InputDeadzone)
            {
                fireDirection = GetMouseDirectionFromPlayer();
            }
        }
        
        private Vector2 GetMouseDirectionFromPlayer()
        {
            if (mainCamera == null || Mouse.current == null)
                return Vector2.zero;
            
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = transform.position.z;
            
            Vector2 direction = (mouseWorldPos - transform.position).normalized;
            return direction.magnitude > InputDeadzone ? direction : Vector2.zero;
        }
        
        #endregion
        
        #region Movement & Physics
        
        private void ApplyMovement()
        {
            // Apply horizontal movement, preserve vertical velocity
            Vector2 velocity = rb.linearVelocity;
            velocity.x = moveInput.x * moveSpeed;
            rb.linearVelocity = velocity;
        }
        
        private void CheckGroundedState()
        {
            // Calculate check position at bottom of collider
            Vector2 checkPosition = new Vector2(
                col.bounds.center.x,
                col.bounds.min.y - groundCheckDistance
            );
            
            // Create smaller box for ground check
            Vector2 boxSize = new Vector2(
                col.bounds.size.x * groundCheckWidth,
                groundCheckDistance * 2f
            );
            
            // Check for ground using overlap box (more reliable than boxcast)
            Collider2D groundHit = Physics2D.OverlapBox(
                checkPosition,
                boxSize,
                0f,
                groundLayer
            );
            
            bool wasGrounded = isGrounded;
            isGrounded = groundHit != null;
            
            // Reset air jumps when landing
            if (isGrounded && !wasGrounded)
            {
                airJumpsRemaining = maxAirJumps;
            }
        }
        
        private void UpdateFacingDirection()
        {
            // Prioritize fire direction when actively firing
            if (fireDirection.magnitude > InputDeadzone && Mathf.Abs(fireDirection.x) > InputDeadzone)
            {
                // Face the direction we're firing
                transform.localScale = fireDirection.x > 0 ? facingRight : facingLeft;
            }
            // Otherwise face movement direction
            else if (Mathf.Abs(moveInput.x) > InputDeadzone)
            {
                // Flip character based on horizontal movement
                transform.localScale = moveInput.x > 0 ? facingRight : facingLeft;
            }
        }
        
        #endregion
        
        #region Jump
        
        public void RequestJump()
        {
            if (isGrounded || airJumpsRemaining > 0)
            {
                jumpRequested = true;
            }
        }
        
        private void ApplyJump()
        {
            if (!jumpRequested) return;
            
            jumpRequested = false;
            
            if (isGrounded)
            {
                // Ground jump
                PerformJump();
                airJumpsRemaining = maxAirJumps;
            }
            else if (airJumpsRemaining > 0)
            {
                // Air jump (Kirby-style)
                PerformJump();
                airJumpsRemaining--;
            }
        }
        
        private void PerformJump()
        {
            // Reset vertical velocity for consistent jump height
            Vector2 velocity = rb.linearVelocity;
            velocity.y = 0f;
            rb.linearVelocity = velocity;
            
            // Apply jump force
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            
            // Trigger animator
            if (animator != null && rb.linearVelocity.y >= jumpDetectVelocityThreshold)
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
            
            FireProjectile(fireDirection);
        }
        
        private void FireProjectile(Vector2 direction)
        {
            if (projectilePrefab == null) return;
            
            lastFireTime = Time.time;
            
            // Spawn projectile at spawn point (or player position if not set)
            Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            
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
            
            animator.SetBool(OnGroundHash, isGrounded);
            animator.SetFloat(VelocityXHash, Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat(VelocityZHash, Mathf.Abs(rb.linearVelocity.y));
        }
        
        private void ResetAnimatorState()
        {
            if (animator == null) return;
            
            animator.SetBool(OnGroundHash, true);
            animator.SetFloat(VelocityXHash, 0f);
            animator.SetFloat(VelocityZHash, 0f);
        }
        
        #endregion
        
        #region Input Callbacks
        
        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            RequestJump();
        }
        
        private void OnFireStarted(InputAction.CallbackContext context)
        {
            if (!IsPointerOverUI())
            {
                isFiring = true;
            }
        }
        
        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            isFiring = false;
        }
        
        #endregion
        
        #region Utility
        
        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
        
        #endregion
        
        #region Public API (for UI buttons)
        
        /// <summary>
        /// Public method for UI button to trigger jump
        /// </summary>
        public void OnJumpButtonPressed()
        {
            RequestJump();
        }
        
        /// <summary>
        /// Public method for UI button to initiate firing
        /// </summary>
        public void OnFireButtonPressed()
        {
            if (projectilePrefab == null) return;
            
            // Fire in facing direction when using UI button
            Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            if (Time.time - lastFireTime >= fireRate)
            {
                FireProjectile(direction);
            }
        }
        
        #endregion
        
        #region Debug Visualization
        
        private void OnDrawGizmos()
        {
            if (col == null) return;
            
            // Visualize ground check box
            Vector2 checkPosition = new Vector2(
                col.bounds.center.x,
                col.bounds.min.y - groundCheckDistance
            );
            
            Vector2 boxSize = new Vector2(
                col.bounds.size.x * groundCheckWidth,
                groundCheckDistance * 2f
            );
            
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(checkPosition, boxSize);
        }
        
        #endregion
    }
}