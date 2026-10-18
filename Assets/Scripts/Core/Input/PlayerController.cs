using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MobileControllerFramework;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// 2D top-down player controller with mobile and PC input support.
    /// Loads projectile prefab via Addressables for WebGL optimization.
    /// Replaces TopDownController — same logic, GameClock-aware firing,
    /// and ProjectileLifetime on spawned projectiles.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 2f;
        
        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 9f;
        [Tooltip("Base air jumps (before roguelite upgrades). Set to 0 for roguelite mode where jumps are purchased.")]
        [SerializeField] private int maxAirJumps = 0;
        [Tooltip("If true, maxAirJumps will be overridden by roguelite upgrade purchases.")]
        [SerializeField] private bool useRogueliteJumps = true;
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
        [Tooltip("Enable or disable shooting")]
        [SerializeField] private bool enableShooting = true;
        [Tooltip("Object pool for player projectiles (wired by generator).")]
        [SerializeField] private ObjectPool projectilePool;
        [Tooltip("Optional: Transform where projectiles spawn. If null, spawns at player position.")]
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float fireRate = 0.25f;
        [SerializeField] private float projectileLifetime = 5f;
        [SerializeField] private bool projectileFaceAim;
        [SerializeField] private float projectileSpriteAngle;
        
        [Header("Platform Interaction")]
        [Tooltip("Layer to ignore collision with when moving down (e.g., 'Platform' layer)")]
        [SerializeField] private LayerMask platformLayer;
        [Tooltip("How far down the player must press to drop through platforms (-1 = full down)")]
        [SerializeField] private float dropThroughThreshold = -0.5f;
        [Tooltip("Downward force applied when dropping through platforms (helps fall faster)")]
        [SerializeField] private float dropThroughForce = 5f;
        
        [Header("PC Input Actions")]
        [Tooltip("WASD/Arrow keys movement action")]
        [SerializeField] private InputActionReference moveAction;
        [Tooltip("Space key - bind to 'Player/Jump' action")]
        [SerializeField] private InputActionReference jumpAction;
        [Tooltip("Left mouse button - bind to 'Player/Fire' action")]
        [SerializeField] private InputActionReference fireAction;
        [Tooltip("Gamepad right stick / mouse delta - bind to 'Character/Look' action")]
        [SerializeField] private InputActionReference lookAction;
        
        [Header("Mobile Controls (wired by generator from MobileController prefab)")]
        [Tooltip("Move analog stick (left stick on MobileController).")]
        [SerializeField] private Analog moveAnalog;
        [Tooltip("Fire/aim analog stick (right stick on MobileController).")]
        [SerializeField] private Analog fireAnalog;
        [Tooltip("GameInputManager for jump events (lives on MobileController).")]
        [SerializeField] private GameInputManager gameInputManager;

        [Header("References")]
        [Tooltip("Child transform containing the graphics/visuals to rotate")]
        [SerializeField] private Transform graphicsTransform;
        [SerializeField] private Animator animator;
        [SerializeField] private Camera mainCamera;
        
        [Header("Rotation Settings")]
        [Tooltip("If true, use 2D sprite flip (localScale.x) instead of Y-axis rotation")]
        [SerializeField] private bool use2DFlip = true;
        [Tooltip("Speed of rotation when moving (smooth rotation to show front/back/sides)")]
        [SerializeField] private float normalRotationSpeed = 900f;
        [Tooltip("Speed of rotation when firing (fast snap to left/right)")]
        [SerializeField] private float firingRotationSpeed = 1800f;
        
        [Header("Shadow Settings")]
        [Tooltip("The shadow sprite transform that follows the character")]
        [SerializeField] private Transform shadowSprite;
        [Tooltip("Maximum height at which shadow is still visible (shadow scale = 0 at this height)")]
        [SerializeField] private float shadowMaxHeight = 10f;
        [Tooltip("Minimum scale of shadow when at max height (0 = invisible)")]
        [SerializeField] private float shadowMinScale = 0.2f;
        [Tooltip("How far down to raycast for ground detection")]
        [SerializeField] private float shadowRaycastDistance = 20f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] hurtSounds;
        [SerializeField] private AudioClip[] deathSounds;
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioClip landSound;
        [SerializeField] [Range(0f, 0.3f)] private float pitchVariation = 0.1f;
        
        #endregion
        
        #region Animator Parameter Hashes
        
        private static readonly int OnGroundHash = Animator.StringToHash("OnGround");
        private static readonly int VelocityXHash = Animator.StringToHash("Velocity_X");
        private static readonly int VelocityZHash = Animator.StringToHash("Velocity_Z");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int FiringHash = Animator.StringToHash("Firing");
        private static readonly int FireStateHash = Animator.StringToHash("Fire");
        // Which optional fire parameters the generated controller has (null until the animator is inspected).
        private bool? animatorHasFiring;
        private bool animatorHasFireTrigger;
        
        #endregion
        
        #region Private Fields
        
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D col;
        
        // Movement
        private Vector2 moveInput;
        private float currentYRotation;
        private float targetYRotation;
        
        // Jump
        private bool isGrounded;
        private int airJumpsRemaining;
        private bool jumpRequested;
        
        // Firing — uses GameClock time instead of Time.time
        private Vector2 fireDirection;
        private bool isFiring;
        // World point under the mouse while firing with it; shots aim from the muzzle at this point.
        private Vector2? mouseAimPoint;
        private float lastFireTime;

        // Gameplay hooks — set by status effects, level scripts, and the playtest bot
        /// <summary>Multiplies horizontal speed (slows, haste). 0 = can't move.</summary>
        public float MoveSpeedMultiplier { get; set; } = 1f;
        /// <summary>When true the player ignores all input (cutscenes, stuns).</summary>
        public bool InputLocked { get; set; }
        /// <summary>When set, input comes from here instead of the devices (the playtest bot).</summary>
        public IPlayerInputSource InputOverride { get; set; }
        /// <summary>Shots per second multiplier (2 = twice as fast). Set by upgrades.</summary>
        public float FireRateMultiplier { get; set; } = 1f;
        /// <summary>Multiplies the damage of each shot. Set by upgrades.</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Extra shots per volley, fanned out around the aim. Set by upgrades.</summary>
        public int ExtraShots { get; set; }
        /// <summary>Degrees between the shots of a volley.</summary>
        public float ShotSpreadDegrees { get; set; } = 10f;
        /// <summary>
        /// Level scripts walking the player (walkPlayerTo): horizontal input from -1 to 1 that replaces the devices'
        /// while set, even when InputLocked, so the Walk clip and the sprite flip play as if he walked himself.
        /// </summary>
        public float? ScriptedMove { get; set; }

        /// <summary>World units per second at full input (move speed times MoveSpeedMultiplier).</summary>
        public float CurrentMoveSpeed => moveSpeed * MoveSpeedMultiplier;

        /// <summary>Game-clock time of the last shot (read by the playtest: no shot may leave while input is locked).</summary>
        public float LastFireTime => lastFireTime;

        /// <summary>True while the sprite faces right (2D flip: negative X faces right).</summary>
        public bool FacingRight => use2DFlip
            ? graphicsTransform == null || graphicsTransform.localScale.x <= 0f
            : Mathf.Abs(Mathf.DeltaAngle(currentYRotation, 180f)) > 90f;

        /// <summary>Physics and collisions are off (pullPlayerInto): a level script moves the player's transform.</summary>
        public bool Suspended { get; private set; }

        private readonly System.Collections.Generic.Dictionary<GameObject, IProjectileDamage> shotDamage =
            new System.Collections.Generic.Dictionary<GameObject, IProjectileDamage>();

        public void SetGravityMultiplier(float multiplier)
        {
            if (rb != null)
                rb.gravityScale = gravityScale * multiplier;
        }

        /// <summary>Turns the player to face right (direction &gt; 0) or left (&lt; 0) now. Moving or firing turns him again.</summary>
        public void Face(float direction)
        {
            if (Mathf.Approximately(direction, 0f))
                return;
            bool right = direction > 0f;
            targetYRotation = currentYRotation = right ? 0f : 180f;
            if (graphicsTransform == null)
                return;
            if (use2DFlip)
            {
                Vector3 scale = graphicsTransform.localScale;
                scale.x = right ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                graphicsTransform.localScale = scale;
            }
            else
            {
                graphicsTransform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
            }
        }

        /// <summary>
        /// Switches physics and collisions off (true) so a script can move, spin and scale the player's transform,
        /// or back on (false). Nothing can hit him and he doesn't fall while suspended.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            if (Suspended == suspended)
                return;
            Suspended = suspended;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = !suspended;
            }
        }

        /// <summary>Stops sideways motion now (a scripted walk arriving), so he stops on the spot instead of sliding a step.</summary>
        public void StopHorizontal()
        {
            if (rb == null || !rb.simulated)
                return;
            Vector2 velocity = rb.linearVelocity;
            velocity.x = 0f;
            rb.linearVelocity = velocity;
        }

        /// <summary>
        /// Plays a state of the player's Animator by name (playPlayerAnimation). A player.json state the generated
        /// controller doesn't have yet plays from its sheet instead (<see cref="PlayerSceneClip"/>). False if neither has it.
        /// </summary>
        public bool PlayAnimatorState(string state, float crossFade = 0f)
        {
            if (animator == null || string.IsNullOrEmpty(state))
                return false;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
            {
                if (!TryGetComponent(out PlayerSceneClip clip))
                    clip = gameObject.AddComponent<PlayerSceneClip>();
                return clip.Play(animator, state);
            }
            if (TryGetComponent(out PlayerSceneClip playing))
                playing.Stop();
            if (crossFade > 0f)
                animator.CrossFadeInFixedTime(hash, crossFade, 0);
            else
                animator.Play(hash, 0, 0f);
            return true;
        }

        /// <summary>
        /// The sprite on screen is a frame of this player.json state (its sheet's sprites are named "State_0000", ...),
        /// whether the Animator or <see cref="PlayerSceneClip"/> drew it. For the playtest.
        /// </summary>
        public bool ShowsState(string state, out string spriteName)
        {
            spriteName = null;
            if (animator == null)
                return false;
            if (!animator.TryGetComponent(out SpriteRenderer renderer))
                renderer = animator.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                return false;
            spriteName = renderer.sprite.name;
            return spriteName.StartsWith(state + "_", System.StringComparison.Ordinal);
        }

        // Platform collision
        private int playerLayer;
        private bool isIgnoringPlatformCollision = false;
        private bool isDroppingThrough = false;
        private System.Collections.Generic.List<DropThroughPlatform> registeredPlatforms = new System.Collections.Generic.List<DropThroughPlatform>();
        
        // Input tracking
        private bool isUsingMobileInput = false;
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float mobileDropThreshold = -0.4f;
        
        // Shadow
        private Vector3 shadowInitialScale;
        private Vector3 shadowInitialLocalPosition;
        
        #endregion
        
        #region Unity Lifecycle
        
        /// <summary>
        /// Apply stats from PlayerConfig. Called early in Awake.
        /// Serialized fields act as defaults; config overrides them.
        /// </summary>
        private void ApplyPlayerConfig()
        {
            var gd = GameDefinition.Instance;
            if (gd == null) return;

            var cfg = gd.player;
            moveSpeed = cfg.moveSpeed;
            jumpForce = cfg.jumpForce;
            gravityScale = cfg.gravityScale;
            groundCheckDistance = cfg.groundCheckDistance;
            groundCheckWidth = cfg.groundCheckWidth;
            enableShooting = cfg.enableShooting;
            fireRate = cfg.fireRate;
            projectileSpeed = cfg.projectileSpeed;
            projectileLifetime = cfg.projectileLifetime;
            projectileFaceAim = cfg.projectileFaceAim;
            projectileSpriteAngle = cfg.projectileSpriteAngle;
            if (projectileSpawnPoint != null)
                projectileSpawnPoint.localPosition = new Vector3(cfg.projectileSpawnOffset.x, cfg.projectileSpawnOffset.y, projectileSpawnPoint.localPosition.z);

            // Rotation
            use2DFlip = cfg.use2DFlip;
            normalRotationSpeed = cfg.normalRotationSpeed;
            firingRotationSpeed = cfg.firingRotationSpeed;

            // Drop-through
            dropThroughForce = cfg.dropThroughForce;
            dropThroughThreshold = cfg.dropThroughThreshold;

            // Shadow
            shadowMaxHeight = cfg.shadowMaxHeight;
            shadowMinScale = cfg.shadowMinScale;
            shadowRaycastDistance = cfg.shadowRaycastDistance;

            // Input
            jumpDetectVelocityThreshold = cfg.jumpDetectVelocityThreshold;

            // Audio SFX
            if (cfg.hurtSounds != null && cfg.hurtSounds.Length > 0) hurtSounds = cfg.hurtSounds;
            if (cfg.deathSounds != null && cfg.deathSounds.Length > 0) deathSounds = cfg.deathSounds;
            if (cfg.jumpSound != null) jumpSound = cfg.jumpSound;
            if (cfg.fireSound != null) fireSound = cfg.fireSound;
            if (cfg.landSound != null) landSound = cfg.landSound;
            pitchVariation = cfg.pitchVariation;
        }

        private void Awake()
        {
            ApplyPlayerConfig();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (col == null) col = GetComponent<Collider2D>();
            playerLayer = gameObject.layer;
            
            if (rb != null)
            {
                rb.gravityScale = gravityScale;
            }
            
            // Initialize rotation based on mode
            if (use2DFlip)
            {
                // Negative X faces right. The source art faces the other way.
                currentYRotation = 0f;
                targetYRotation = 0f;
                if (graphicsTransform != null)
                {
                    Vector3 scale = graphicsTransform.localScale;
                    scale.x = -Mathf.Abs(scale.x);
                    graphicsTransform.localScale = scale;
                }
            }
            else
            {
                // 3D mode: Y rotation to face camera
                currentYRotation = 90f;
                targetYRotation = 90f;
                if (graphicsTransform != null)
                {
                    graphicsTransform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
                }
            }
            
            // Store initial shadow values
            if (shadowSprite != null)
            {
                shadowInitialScale = shadowSprite.localScale;
                shadowInitialLocalPosition = shadowSprite.localPosition;
            }
        }
        
        private void Start()
        {
            // Apply roguelite jump upgrades
            if (useRogueliteJumps)
            {
                int purchasedJumps = RogueliteManager.Instance.GetExtraJumpsPurchased();
                maxAirJumps = purchasedJumps;
            }

            if (moveAction?.action != null)
                moveAction.action.Enable();
            if (jumpAction?.action != null)
                jumpAction.action.Enable();
            if (fireAction?.action != null)
                fireAction.action.Enable();
            if (lookAction?.action != null)
                lookAction.action.Enable();
        }
        
        private void OnEnable()
        {
            // Re-enable input actions every time we come back on.
            if (moveAction?.action != null) moveAction.action.Enable();
            if (jumpAction?.action != null) jumpAction.action.Enable();
            if (fireAction?.action != null) fireAction.action.Enable();
            if (lookAction?.action != null) lookAction.action.Enable();

            // Subscribe to mobile control events
            if (moveAnalog != null)
                moveAnalog.StickExitEvent.AddListener(OnAnalogReleased);
            if (gameInputManager != null)
                gameInputManager.OnJumpPressed += OnJumpRequested;
        }

        private void Update()
        {
            // Freeze all player input/logic during rewind — RewindTimeline controls position
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            ReadMoveInput();
            ReadJumpInput();
            ReadFireInput();
            ApplyInputOverrides();
            CheckGrounded();
            HandlePlatformCollision();
            HandleJump();
            HandleFiring();
            UpdateRotationTarget();
            UpdateAnimator();
            UpdateShadow();
        }
        
        private void FixedUpdate()
        {
            // Don't apply movement during rewind
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            HandleMovement();
        }
        
        private void LateUpdate()
        {
            // Don't apply rotation during rewind — RewindableTransform handles it
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            // Apply rotation after Animator updates to prevent override
            ApplyRotation();
        }
        
        private void OnDisable()
        {
            // NOTE: Do NOT call Disable() on InputActionReferences here.
            // InputActionReference shares the same underlying InputAction across
            // all prefab instances.

            // Unsubscribe from mobile control events
            if (moveAnalog != null)
                moveAnalog.StickExitEvent.RemoveListener(OnAnalogReleased);
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
            
            // If ignoring platform collision, exclude platform layer from ground check
            LayerMask checkLayer = isIgnoringPlatformCollision ? (groundLayer & ~platformLayer) : groundLayer;
            isGrounded = Physics2D.OverlapBox(boxCenter, boxSize, 0f, checkLayer);
            
            if (isGrounded && !wasGrounded)
            {
                airJumpsRemaining = maxAirJumps;
                if (Cue(landSound, SickOfIt.Core.Audio.Sfx.Events.playerLand))
                    SickOfIt.Core.Fx.Fx.Dust(Feet, 1f);
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
            Vector2 mobileInput = Vector2.zero;
            Vector2 keyboardInput = Vector2.zero;
            bool usingMobileInput = false;
            
            // Poll mobile move input directly from analog stick
            if (moveAnalog != null && moveAnalog.gameObject.activeSelf)
            {
                mobileInput = moveAnalog.GetStickPosition();
                moveInput = mobileInput;
                usingMobileInput = true;
            }
            
            // Always check keyboard/gamepad input and use it if mobile input is idle
            if (moveAction?.action != null && moveAction.action.enabled)
            {
                keyboardInput = moveAction.action.ReadValue<Vector2>();
                if (keyboardInput.magnitude > inputDeadzone && moveInput.magnitude < inputDeadzone)
                {
                    moveInput = keyboardInput;
                    usingMobileInput = false;
                }
            }
            
            // Store whether we're using mobile input for platform threshold adjustment
            isUsingMobileInput = usingMobileInput;
        }
        
        private void ReadJumpInput()
        {
            if (jumpAction?.action != null && jumpAction.action.WasPressedThisFrame())
            {
                jumpRequested = true;
            }
        }
        
        private void ApplyInputOverrides()
        {
            if (InputOverride != null)
            {
                moveInput = InputOverride.Move;
                jumpRequested = InputOverride.ConsumeJump();
                fireDirection = InputOverride.FireDirection;
                isFiring = fireDirection.sqrMagnitude > inputDeadzone * inputDeadzone;
            }
            if (InputLocked || Suspended)
            {
                moveInput = Vector2.zero;
                jumpRequested = false;
                fireDirection = Vector2.zero;
                isFiring = false;
            }
            if (ScriptedMove.HasValue && !Suspended)
                moveInput = new Vector2(Mathf.Clamp(ScriptedMove.Value, -1f, 1f), 0f);
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
            mouseAimPoint = null;
            
            // Poll mobile fire input directly from analog stick
            if (fireAnalog != null && fireAnalog.gameObject.activeSelf)
            {
                Vector2 analogInput = fireAnalog.GetStickPosition();
                if (analogInput.magnitude > inputDeadzone)
                {
                    fireDirection = analogInput.normalized;
                    isFiring = true;
                }
            }
            
            // Check gamepad right stick (Look action) - only accept gamepad input, not mouse
            if (lookAction?.action != null && !isFiring)
            {
                Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
                if (lookInput.magnitude > inputDeadzone && 
                    lookAction.action.activeControl?.device is Gamepad)
                {
                    fireDirection = lookInput.normalized;
                    isFiring = true;
                }
            }
            
            // Attack is bound to F. Left click is the expected fire button, so poll it
            // directly as well as the action (the prefab still references Attack).
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool actionFire = fireAction?.action != null && fireAction.action.IsPressed();
            bool mouseFire = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if ((actionFire || mouseFire) && !pointerOverUi)
            {
                isFiring = true;
                fireDirection = GetMouseDirectionFromPlayer();
                mouseAimPoint = GetMouseWorldPoint();
            }
        }
        
        private Vector2 GetMouseDirectionFromPlayer()
        {
            if (mainCamera == null || Mouse.current == null)
                return Vector2.zero;
            
            return (GetMouseWorldPoint() - (Vector2)transform.position).normalized;
        }

        private Vector2 GetMouseWorldPoint()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            // Project onto the player's plane so a perspective camera aims correctly too.
            float depth = Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, depth));
        }

        /// <summary>Where shots leave the gun: the spawn point, mirrored to the side being aimed at.</summary>
        private Vector2 GetMuzzlePosition(Vector2 direction)
        {
            if (projectileSpawnPoint == null)
                return transform.position;
            Vector3 local = transform.InverseTransformPoint(projectileSpawnPoint.position);
            local.x = Mathf.Abs(local.x) * (direction.x < 0f ? -1f : 1f);
            return transform.TransformPoint(local);
        }
        
        #endregion
        
        #region Platform Collision
        
        private void HandlePlatformCollision()
        {
            // Use more lenient threshold for mobile analog sticks
            float threshold = isUsingMobileInput ? mobileDropThreshold : dropThroughThreshold;
            bool pressingDown = moveInput.y < threshold;
            
            // Only change state when transitioning
            if (pressingDown && !isDroppingThrough)
            {
                StartDroppingThrough();
            }
            else if (!pressingDown && isDroppingThrough)
            {
                StopDroppingThrough();
            }
        }
        
        private void StartDroppingThrough()
        {
            isDroppingThrough = true;
            
            // Disable all platform colliders
            foreach (var platform in registeredPlatforms)
            {
                if (platform != null)
                {
                    platform.DisablePlatform();
                }
            }
            
            int platformLayerIndex = GetLayerIndexFromMask(platformLayer);
            if (platformLayerIndex >= 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, platformLayerIndex, true);
                isIgnoringPlatformCollision = true;
            }
            
            // Apply downward force to help player fall through faster (only if grounded)
            if (isGrounded && rb != null && dropThroughForce > 0)
            {
                rb.AddForce(Vector2.down * dropThroughForce, ForceMode2D.Impulse);
            }
        }
        
        private void StopDroppingThrough()
        {
            isDroppingThrough = false;
            
            // Re-enable all platforms
            foreach (var platform in registeredPlatforms)
            {
                if (platform != null)
                {
                    platform.EnablePlatform();
                }
            }
            
            int platformLayerIndex = GetLayerIndexFromMask(platformLayer);
            if (platformLayerIndex >= 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, platformLayerIndex, false);
                isIgnoringPlatformCollision = false;
            }
        }
        
        private void OnAnalogReleased()
        {
            // When analog is released, immediately re-enable platforms
            if (isDroppingThrough)
            {
                StopDroppingThrough();
            }
        }
        
        private int GetLayerIndexFromMask(LayerMask mask)
        {
            int layerNumber = 0;
            int layer = mask.value;
            
            if (layer == 0)
                return -1;
            
            while (layer > 1)
            {
                layer = layer >> 1;
                layerNumber++;
            }
            
            return layer == 1 ? layerNumber : -1;
        }
        
        /// <summary>
        /// Called by DropThroughPlatform components to register themselves
        /// </summary>
        public void RegisterPlatform(DropThroughPlatform platform)
        {
            if (!registeredPlatforms.Contains(platform))
            {
                registeredPlatforms.Add(platform);
            }
        }
        
        /// <summary>
        /// Called by DropThroughPlatform components when destroyed
        /// </summary>
        public void UnregisterPlatform(DropThroughPlatform platform)
        {
            registeredPlatforms.Remove(platform);
        }

        /// <summary>Set at runtime by LevelRuntime after resolving references.</summary>
        public void SetProjectilePool(ObjectPool pool) => projectilePool = pool;
        
        #endregion
        
        #region Movement & Jump
        
        private void HandleMovement()
        {
            if (rb == null) return;
            
            if (Suspended) return;

            // A scripted walk drives the body directly; a lock stops it at once (moveInput is only read in Update).
            float horizontalInput = ScriptedMove.HasValue ? Mathf.Clamp(ScriptedMove.Value, -1f, 1f)
                : InputLocked ? 0f
                : Mathf.Clamp(moveInput.x, -1f, 1f);
            
            Vector2 velocity = rb.linearVelocity;
            velocity.x = horizontalInput * moveSpeed * MoveSpeedMultiplier;
            rb.linearVelocity = velocity;
        }
        
        private void UpdateRotationTarget()
        {
            float rotationSpeed = normalRotationSpeed;
            
            // Priority 1: Firing direction (fast rotation to pure left/right)
            if (isFiring && Mathf.Abs(fireDirection.x) > inputDeadzone)
            {
                rotationSpeed = firingRotationSpeed;
                if (fireDirection.x > 0)
                {
                    targetYRotation = 0f;  // Right (side view)
                }
                else if (fireDirection.x < 0)
                {
                    targetYRotation = 180f; // Left (side view)
                }
            }
            // Priority 2: Movement direction (4 cardinal directions only)
            else if (moveInput.magnitude > inputDeadzone)
            {
                float absX = Mathf.Abs(moveInput.x);
                float absY = Mathf.Abs(moveInput.y);
                
                if (absX >= absY)
                {
                    if (moveInput.x > 0)
                    {
                        targetYRotation = 0f;   // Right
                    }
                    else
                    {
                        targetYRotation = 180f; // Left
                    }
                }
                else
                {
                    if (moveInput.y > 0)
                    {
                        targetYRotation = 270f; // Up/Back
                    }
                    else
                    {
                        targetYRotation = 90f;  // Down/Front
                    }
                }
            }
            
            currentYRotation = Mathf.MoveTowardsAngle(currentYRotation, targetYRotation, 
                rotationSpeed * (GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime));
        }
        
        private void ApplyRotation()
        {
            if (use2DFlip)
            {
                // 2D mode: flip sprite using localScale.x
                if (graphicsTransform != null)
                {
                    Vector3 scale = graphicsTransform.localScale;
                    if (isFiring && Mathf.Abs(fireDirection.x) > inputDeadzone)
                    {
                        scale.x = fireDirection.x > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    }
                    else if (moveInput.magnitude > inputDeadzone && Mathf.Abs(moveInput.x) > inputDeadzone)
                    {
                        scale.x = moveInput.x > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    }
                    graphicsTransform.localScale = scale;
                }
            }
            else
            {
                // 3D mode: Y-axis rotation
                if (graphicsTransform != null)
                {
                    graphicsTransform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
                }
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
            
            if (Cue(jumpSound, SickOfIt.Core.Audio.Sfx.Events.playerJump))
                SickOfIt.Core.Fx.Fx.Dust(Feet, 0.6f);
            
            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }
        
        #endregion
        
        #region Projectile Firing
        
        private void HandleFiring()
        {
            if (!enableShooting) return;
            if (fireDirection.magnitude < inputDeadzone) return;
            
            // Use GameClock time for fire rate to survive rewind properly.
            // Rewinding to the start leaves lastFireTime ahead of the clock, which
            // would block firing until time catches up to the pre-rewind shot.
            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            float interval = fireRate / Mathf.Max(0.1f, FireRateMultiplier);
            if (currentTime < lastFireTime)
                lastFireTime = currentTime - interval;
            if (currentTime - lastFireTime < interval) return;
            if (projectilePool == null) return;

            FireProjectile(fireDirection);
        }

        private void FireProjectile(Vector2 direction)
        {
            if (projectilePool == null)
            {
                Debug.LogWarning("PlayerController: projectilePool not assigned!");
                return;
            }

            // Use GameClock time for fire timing
            lastFireTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;

            // Aim from the muzzle so the shot passes through the cursor, not parallel to it.
            Vector2 muzzle = GetMuzzlePosition(direction);
            if (mouseAimPoint.HasValue && (mouseAimPoint.Value - (Vector2)transform.position).sqrMagnitude > 0.25f)
            {
                Vector2 toCursor = mouseAimPoint.Value - muzzle;
                if (toCursor.sqrMagnitude > 0.0001f && Vector2.Dot(toCursor, direction) > 0f)
                    direction = toCursor.normalized;
            }

            // Extra shots fan out evenly around the aim.
            int shots = 1 + Mathf.Max(0, ExtraShots);
            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * ShotSpreadDegrees;
                SpawnShot(muzzle, Quaternion.Euler(0f, 0f, angle) * direction, i == 0);
            }
        }

        private void SpawnShot(Vector2 muzzle, Vector2 direction, bool firstOfVolley)
        {
            Vector3 spawnPosition = new Vector3(muzzle.x, muzzle.y, transform.position.z);
            Quaternion rotation = projectileFaceAim
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - projectileSpriteAngle)
                : Quaternion.identity;

            GameObject projectile = projectilePool.Get(spawnPosition, rotation);
            
            Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, col);
            }
            
            // Use pool-cached Rigidbody2D when available (avoids GetComponent per shot)
            Rigidbody2D projectileRb = projectilePool.GetCachedRb(projectile);
            if (projectileRb == null) projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }

            projectilePool.CaptureLaunchState(projectile);

            if (!shotDamage.TryGetValue(projectile, out var damage))
            {
                damage = projectile.GetComponent<IProjectileDamage>();
                shotDamage[projectile] = damage;
            }
            damage?.SetDamageMultiplier(DamageMultiplier);

            // Use pool-cached ProjectileLifetime when available
            var lifetime = projectilePool.GetCachedLifetime(projectile);
            if (lifetime == null) lifetime = projectile.GetComponent<ProjectileLifetime>();
            if (lifetime != null)
                lifetime.SetLifetime(projectileLifetime);

            if (!firstOfVolley)
                return;

            // Firing happens constantly, so its feedback stays quiet: a faint, small flash and a soft sound.
            if (Cue(fireSound, SickOfIt.Core.Audio.Sfx.Events.playerShoot))
                SickOfIt.Core.Fx.Fx.Flash(muzzle, new Color(1f, 0.85f, 0.45f, 0.35f), 0.26f, 0.06f);

            // Restart the fire clip on every shot so the muzzle flash lines up with the projectile.
            if (animator != null && animator.HasState(0, FireStateHash))
                animator.Play(FireStateHash, 0, 0f);
            else if (animator != null && AnimatorHasFireTrigger())
                animator.SetTrigger(FireHash);
        }
        
        #endregion
        
        #region Animation
        
        private bool AnimatorHasFiring()
        {
            if (!animatorHasFiring.HasValue && animator != null && animator.runtimeAnimatorController != null)
            {
                animatorHasFiring = false;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == FiringHash && parameter.type == AnimatorControllerParameterType.Bool)
                        animatorHasFiring = true;
                    if (parameter.nameHash == FireHash && parameter.type == AnimatorControllerParameterType.Trigger)
                        animatorHasFireTrigger = true;
                }
            }
            return animatorHasFiring == true;
        }

        private bool AnimatorHasFireTrigger()
        {
            AnimatorHasFiring();
            return animatorHasFireTrigger;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            
            animator.SetBool(OnGroundHash, isGrounded);
            if (AnimatorHasFiring())
                animator.SetBool(FiringHash, isFiring && enableShooting);
            animator.SetFloat(VelocityXHash, Mathf.Abs(moveInput.x));
            
            if (rb != null)
            {
                float verticalVelocity = rb.linearVelocity.y;
                animator.SetFloat(VelocityZHash, verticalVelocity);
            }
        }
        
        #endregion
        
        #region Shadow
        
        private void UpdateShadow()
        {
            if (shadowSprite == null) return;
            
            if (isGrounded)
            {
                shadowSprite.localPosition = shadowInitialLocalPosition;
                shadowSprite.localScale = shadowInitialScale;
                return;
            }
            
            LayerMask checkLayer = isIgnoringPlatformCollision ? (groundLayer & ~platformLayer) : groundLayer;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, shadowRaycastDistance, checkLayer);
            
            if (hit.collider != null)
            {
                float distanceToGround = hit.distance;
                
                Vector3 shadowWorldPosition = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                shadowSprite.position = shadowWorldPosition;
                
                float heightRatio = Mathf.Clamp01(distanceToGround / shadowMaxHeight);
                float scaleFactor = Mathf.Lerp(1f, shadowMinScale, heightRatio);
                shadowSprite.localScale = shadowInitialScale * scaleFactor;
            }
            else
            {
                shadowSprite.localScale = shadowInitialScale * shadowMinScale;
            }
        }
        
        #endregion

        #region Audio SFX

        /// <summary>
        /// Play a single sound effect via the player's AudioSource.
        /// Applies random pitch variation for variety.
        /// </summary>
        private void PlaySFX(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Play a random sound from an array of clips.
        /// </summary>
        public void PlayRandomSFX(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySFX(clips[Random.Range(0, clips.Length)]);
        }

        /// <summary>
        /// Called by PlayerHealth when the player is damaged: the player's own hurt clips, else audio.json's playerHurt.
        /// </summary>
        public void PlayHurtSound()
        {
            if (hurtSounds != null && hurtSounds.Length > 0)
                PlayRandomSFX(hurtSounds);
            else
                SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.playerHurt);
        }

        /// <summary>
        /// Called by PlayerHealth when the player dies: the player's own death clips, else audio.json's playerDie.
        /// </summary>
        public void PlayDeathSound()
        {
            if (deathSounds != null && deathSounds.Length > 0)
                PlayRandomSFX(deathSounds);
            else
                SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.playerDie);
        }

        /// <summary>
        /// Plays the player's own clip if one is set, else the audio.json sound. Returns false while time runs
        /// backward (a rewind replays landings and jumps; they shouldn't make noise or dust).
        /// </summary>
        private bool Cue(AudioClip clip, string soundId)
        {
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return false;
            if (clip != null)
                PlaySFX(clip);
            else
                SickOfIt.Core.Audio.Sfx.Play(soundId);
            return true;
        }

        private Vector2 Feet => col != null ? new Vector2(transform.position.x, col.bounds.min.y) : (Vector2)transform.position;

        #endregion
    }
}
