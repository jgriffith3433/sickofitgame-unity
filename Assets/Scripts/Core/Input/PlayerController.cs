using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MobileControllerFramework;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;

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
        [Tooltip("How far down the player must press to drop through platforms (-1 = full down)")]
        [SerializeField] private float dropThroughThreshold = -0.75f;
        [Tooltip("Down only counts within this many degrees of straight down")]
        [SerializeField] private float dropThroughMaxAngle = 30f;
        [Tooltip("Seconds of continuous down on a one-way platform before he drops")]
        [SerializeField] private float dropThroughHoldSeconds = 0.18f;
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
        private static readonly int WalkStateHash = Animator.StringToHash("Walk");
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
        /// <summary>Asked once per volley for its damage scale (a crit is more than 1); set by the world (HeroPerks).</summary>
        public System.Func<float> VolleyScale { get; set; }
        /// <summary>Aim assist: (muzzle, aim direction) -> the direction nudged onto a nearby enemy (set by the world; null = none).</summary>
        public System.Func<Vector2, Vector2, Vector2> AimAssist { get; set; }
        /// <summary>Slippery Shots: his shots fly this much faster.</summary>
        public float ShotSpeedScale { get; set; } = 1f;
        private float shotCrit = 1f;
        /// <summary>Extra shots per volley, fanned out around the aim. Set by upgrades.</summary>
        public int ExtraShots { get; set; }
        /// <summary>Degrees between the shots of a volley.</summary>
        public float ShotSpreadDegrees { get; set; } = 10f;
        /// <summary>
        /// The hero's weapons (content/weapons), set by the gameplay world. While null he fires the gun from player.json.
        /// </summary>
        public IPlayerArmory Armory { get; set; }
        /// <summary>The equipped weapon's aim guide length as a multiple of player.json aimGuide.length (1 for the launcher).</summary>
        public float GuideLengthScale { get; private set; } = 1f;
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

        /// <summary>
        /// Velocity of what he stands on (a drifting raft, a slag conveyor), added to his own sideways speed. The gameplay
        /// world sets it every simulation tick (zero when he stands on nothing that moves).
        /// </summary>
        public Vector2 CarrierVelocity { get; set; }

        /// <summary>On the ground this frame (the ground check under his feet).</summary>
        public bool IsGrounded => isGrounded;

        /// <summary>His body's collider (standing checks for floor platforms).</summary>
        public Collider2D BodyCollider => col;
        /// <summary>What he stands on (null in the air).</summary>
        public Collider2D GroundCollider => isGrounded ? groundCollider : null;

        /// <summary>Hanging from something (a swinging chain): gravity is off and the holder moves him; Jump lets go.</summary>
        public bool IsHeld => held;

        private bool held;
        private Vector2 heldVelocity;
        private float gravityBeforeHold;
        private bool jumpedOffHold;
        // Sideways speed kept from a chain's swing after jumping off it, fading out (or gone on landing).
        private float swingMomentum;
        // Held on a ladder (HoldClimbing): the climb poses play, shots come slower, and a jump leaves sideways with the held stick.
        private bool climbing;
        private float heldJumpOffX;
        private float climbFireScale = 1f;
        private string climbPose;

        /// <summary>Climbing a ladder (HoldClimbing): gravity is off and the ladder moves him; Jump lets go.</summary>
        public bool IsClimbing => held && climbing;

        /// <summary>Which way the move stick is held up (+1) or down (-1), 0 when idle or locked (read by ladders; he grabs on with up).</summary>
        public float MoveY => InputLocked || Suspended || ScriptedMove.HasValue ? 0f : (Mathf.Abs(moveInput.y) > inputDeadzone ? moveInput.y : 0f);

        /// <summary>Sideways stick, -1..1 (read by ladders: a jump off leaves that way).</summary>
        public float MoveX => InputLocked || Suspended || ScriptedMove.HasValue ? 0f : Mathf.Clamp(moveInput.x, -1f, 1f);

        /// <summary>The climb pose playing now ("Climb", "ClimbIdle") or null; a playtest reads it.</summary>
        public string ClimbPose => climbPose;

        /// <summary>
        /// Holds him on a ladder at 'position' moving at 'velocity'. Like <see cref="Hold"/>, but the climb poses play,
        /// 'fireScale' slows the guns while he climbs (1 = same as on the ground) and a jump off leaves sideways at 'jumpOffX'.
        /// Call every tick while climbing.
        /// </summary>
        public void HoldClimbing(Vector2 position, Vector2 velocity, float jumpOffX, float fireScale)
        {
            if (rb == null || Suspended)
                return;
            Hold(position, velocity);
            climbing = true;
            heldJumpOffX = jumpOffX;
            climbFireScale = Mathf.Clamp(fireScale, 0.1f, 1f);
        }

        /// <summary>Holds him at 'position' moving at 'velocity' (a chain he grabbed). Call every tick while holding.</summary>
        public void Hold(Vector2 position, Vector2 velocity)
        {
            if (rb == null || Suspended)
                return;
            if (!held)
            {
                held = true;
                gravityBeforeHold = Mathf.Abs(rb.gravityScale) * gravitySign;
                rb.gravityScale = 0f;
                airJumpsRemaining = maxAirJumps;
            }
            heldVelocity = velocity;
            rb.position = position;
            rb.linearVelocity = velocity;
        }

        /// <summary>Lets go (the holder is gone, or a rewind went back before the grab). Keeps his current velocity.</summary>
        public void ReleaseHold()
        {
            if (!held)
                return;
            held = false;
            if (rb != null)
                rb.gravityScale = Mathf.Abs(gravityBeforeHold) * gravitySign;
            if (climbing)
                EndClimbPose();
            climbing = false;
            heldJumpOffX = 0f;
            climbFireScale = 1f;
        }

        // Back to the ordinary animator state once he leaves a ladder (the climb poses are scene-clip states).
        private void EndClimbPose()
        {
            climbPose = null;
            if (animator != null)
                PlayAnimatorState(isGrounded ? "Idle" : "Fall");
        }

        // Plays "Climb" going up the ladder, "ClimbDown" going down and "ClimbIdle" while he hangs still.
        private void UpdateClimbPose()
        {
            if (!IsClimbing)
                return;
            float vy = rb != null ? rb.linearVelocity.y : 0f;
            string pose = vy > 0.1f ? "Climb" : vy < -0.1f ? "ClimbDown" : "ClimbIdle";
            if (pose == climbPose)
                return;
            climbPose = pose;
            PlayAnimatorState(pose);
        }

        /// <summary>True once after he jumped off what held him (the holder lets go of him).</summary>
        public bool ConsumeJumpOff()
        {
            bool jumped = jumpedOffHold;
            jumpedOffHold = false;
            return jumped;
        }

        private readonly System.Collections.Generic.Dictionary<GameObject, IProjectileDamage> shotDamage =
            new System.Collections.Generic.Dictionary<GameObject, IProjectileDamage>();

        public void SetGravityMultiplier(float multiplier)
        {
            if (rb != null)
                rb.gravityScale = gravityScale * multiplier * gravitySign;
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
            if (suspended)
                ReleaseHold();
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

        // One-way platforms (DropThroughPlatform.IsOneWay): the pieces he is dropping through (his collisions with each
        // ignored until he is below it), and the ones he is rising up through (to report the landing on top).
        private struct DropThrough { public Collider2D platform; public float since; }
        private readonly System.Collections.Generic.List<DropThrough> droppingThrough = new System.Collections.Generic.List<DropThrough>();
        private readonly System.Collections.Generic.List<DropThrough> risingThrough = new System.Collections.Generic.List<DropThrough>();
        private readonly System.Collections.Generic.List<Collider2D> ownColliders = new System.Collections.Generic.List<Collider2D>();
        private Collider2D groundCollider;
        
        // Input tracking
        private bool isUsingMobileInput = false;
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float mobileDropThreshold = -0.75f;
        // Seconds down has been held on the one-way platform under him (a dead zone: a brush of down never drops him).
        private float dropHold;
        
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
            dropThroughMaxAngle = cfg.dropThroughMaxAngle;
            dropThroughHoldSeconds = cfg.dropThroughHoldSeconds;
            mobileDropThreshold = cfg.mobileDropThreshold;

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

            // Moves (dash, wall-jump)
            moves = cfg.moves ?? new HeroMovesConfig();
            airDashesLeft = Mathf.Max(0, moves.dash.airDashes);
            ResolveDashKeys();
            ResolveGrappleKeys();
        }

        private void Awake()
        {
            ApplyPlayerConfig();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (col == null) col = GetComponent<Collider2D>();
            
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
                    scale.y = Mathf.Abs(scale.y) * gravitySign;
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
            if (gameInputManager != null)
                gameInputManager.OnJumpPressed += OnJumpRequested;
        }

        private void Update()
        {
            // Freeze all player input/logic during rewind — RewindTimeline controls position
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
            {
                ClearDropThrough();
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            ReadMoveInput();
            ReadJumpInput();
            ReadFireInput();
            ReadDashInput();
            ReadGrappleInput();
            ApplyInputOverrides();
            CheckGrounded();
            HandlePlatformCollision();
            UpdateWall();
            HandleDash();
            HandleJump();
            HandleWeaponSwitch();
            HandleFiring();
            UpdateRotationTarget();
            UpdateMovePose();
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
            if (gameInputManager != null)
                gameInputManager.OnJumpPressed -= OnJumpRequested;
            ClearDropThrough();
        }
        
        #endregion
        
        #region Ground Check
        
        private void CheckGrounded()
        {
            // Lenient on uneven ground: the box spans most of his feet (a slope, a step or a ledge edge under only one side
            // still counts), and a jump pressed just before landing or just after walking off still works.
            float width = Mathf.Max(groundCheckWidth, col.bounds.size.x * 0.9f);
            Vector2 boxSize = new Vector2(width, groundCheckDistance);
            Vector2 boxCenter = (Vector2)transform.position + col.offset + (gravitySign < 0 ? Vector2.up : Vector2.down) * (col.bounds.extents.y + groundCheckDistance * 0.5f);

            bool wasGrounded = isGrounded;

            isGrounded = FindGround(boxCenter, boxSize, out groundCollider);
            if (isGrounded)
                lastGroundedTime = Time.time;

            if (isGrounded && !wasGrounded)
            {
                airJumpsRemaining = maxAirJumps;
                airDashesLeft = Mathf.Max(0, moves.dash.airDashes);
                if (Cue(landSound, SickOfIt.Core.Audio.Sfx.Events.playerLand))
                    SickOfIt.Core.Fx.Fx.Dust(Feet, 1f);
            }
        }
        
        private static readonly Collider2D[] footholdHits = new Collider2D[8];

        /// <summary>
        /// What he stands on: the level's ground and walls (groundLayer: Platform, Boundary), or a solid gameplay entity
        /// on the Default layer (the climb engine's stones, ledges, rafts, lifts). A one-way platform or an entity only
        /// counts when its top is under his feet, so rising up through one isn't standing on it; a piece he is dropping
        /// through never counts.
        /// </summary>
        private bool FindGround(Vector2 center, Vector2 size, out Collider2D ground)
        {
            ground = null;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer | 1, useTriggers = Physics2D.queriesHitTriggers };
            int n = Physics2D.OverlapBox(center, size, 0f, filter, footholdHits);
            float feetY = col.bounds.min.y;
            float headY = col.bounds.max.y;
            for (int i = 0; i < n; i++)
            {
                var hit = footholdHits[i];
                if (hit == null || !hit.enabled || hit.attachedRigidbody == rb || IsDroppingThrough(hit))
                    continue;
                bool levelGround = (groundLayer.value & (1 << hit.gameObject.layer)) != 0 && hit.gameObject.layer != 0;
                bool surfaceOnly = !levelGround || DropThroughPlatform.IsOneWay(hit);
                if (!levelGround && hit.isTrigger)
                    continue;
                // Only a surface under his feet is ground: a solid piece whose top is above them (beside him, or around him
                // as he passes) isn't, so he falls instead of standing on air.
                if (surfaceOnly && (gravitySign < 0 ? hit.bounds.min.y < headY - 0.12f : hit.bounds.max.y > feetY + 0.12f))
                    continue;
                // Solid ground wins over a one-way piece (it decides what a hold of down drops through).
                ground = hit;
                if (!DropThroughPlatform.IsOneWay(hit))
                    return true;
            }
            return ground != null;
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
            // Firing ignores what the pointer is over: a HUD button under the cursor still gets its click
            // from the EventSystem, and the shot goes out too.
            bool actionFire = fireAction?.action != null && fireAction.action.IsPressed();
            bool mouseFire = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (actionFire || mouseFire)
            {
                isFiring = true;
                fireDirection = GetMouseDirectionFromPlayer();
                mouseAimPoint = GetMouseWorldPoint();
            }
            TrackAimDevice(isFiring && !mouseAimPoint.HasValue);
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

        /// <summary>He dropped through a one-way platform (holding down on it) and is now below it.</summary>
        public static event System.Action<PlayerController, Collider2D> DroppedThrough;
        /// <summary>He jumped up through a one-way platform from below and landed on top of it.</summary>
        public static event System.Action<PlayerController, Collider2D> JumpedUpThrough;

        /// <summary>True while he is dropping through this one-way platform (his collisions with it are off).</summary>
        public bool IsDroppingThrough(Collider2D platform)
        {
            for (int i = 0; i < droppingThrough.Count; i++)
                if (droppingThrough[i].platform == platform)
                    return true;
            return false;
        }

        /// <summary>Standing on a one-way platform (a hold of down drops him through it).</summary>
        public bool OnOneWayPlatform => isGrounded && groundCollider != null && DropThroughPlatform.IsOneWay(groundCollider);

        /// <summary>
        /// Hold down (S/down arrow, the gamepad's stick, the move touch pulled down) on a one-way platform and he drops
        /// through it; the ground and walls never let him through. Also watches him rise up through one, so the landing
        /// on top can be reported.
        /// </summary>
        private void HandlePlatformCollision()
        {
            UpdateDropThrough();
            UpdateRiseThrough();

            float threshold = isUsingMobileInput ? mobileDropThreshold : dropThroughThreshold;
            // Deep and close to straight down, so a run with the stick a little low doesn't drop him...
            bool pressingDown = moveInput.y < threshold
                && Vector2.Angle(moveInput, Vector2.down) <= dropThroughMaxAngle;
            // ...and held for a moment, so a brush of down doesn't either. Letting go (or leaving the platform) starts over.
            if (pressingDown && OnOneWayPlatform && !held && !Suspended)
                dropHold += Time.deltaTime;
            else
                dropHold = 0f;
            if (dropHold >= dropThroughHoldSeconds && dropHold > 0f)
            {
                float heldFor = dropHold;
                dropHold = 0f;
                if (StartDroppingThrough())
                    LastDropHeldSeconds = heldFor;
            }
        }

        /// <summary>How long down was held before his last drop through a one-way platform (seconds).</summary>
        public float LastDropHeldSeconds { get; private set; }
        /// <summary>Seconds of continuous down a drop through a one-way platform needs (player.json dropThroughHoldSeconds).</summary>
        public float DropHoldSeconds => dropThroughHoldSeconds;

        private void CollectOwnColliders()
        {
            ownColliders.Clear();
            if (rb != null)
            {
                var attached = new Collider2D[8];
                int n = rb.GetAttachedColliders(attached);
                for (int i = 0; i < n; i++)
                    if (attached[i] != null && !attached[i].isTrigger)
                        ownColliders.Add(attached[i]);
            }
            if (ownColliders.Count == 0 && col != null)
                ownColliders.Add(col);
        }

        private void IgnorePlatform(Collider2D platform, bool ignore)
        {
            if (platform == null)
                return;
            if (ownColliders.Count == 0)
                CollectOwnColliders();
            foreach (var own in ownColliders)
                if (own != null)
                    Physics2D.IgnoreCollision(own, platform, ignore);
        }

        private bool StartDroppingThrough()
        {
            // Every one-way piece under his feet (he may stand across two), never the ground.
            var b = col.bounds;
            Vector2 center = new Vector2(b.center.x, b.min.y - groundCheckDistance * 0.5f);
            Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance + 0.1f);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer | 1, useTriggers = false };
            int n = Physics2D.OverlapBox(center, size, 0f, filter, footholdHits);
            bool dropped = false;
            for (int i = 0; i < n; i++)
            {
                var hit = footholdHits[i];
                if (hit == null || !hit.enabled || hit.attachedRigidbody == rb || !DropThroughPlatform.IsOneWay(hit) || IsDroppingThrough(hit) || hit.TryGetComponent<NoDropThrough>(out _))
                    continue;
                if (hit.bounds.max.y > b.min.y + 0.12f)
                    continue;
                IgnorePlatform(hit, true);
                droppingThrough.Add(new DropThrough { platform = hit, since = Time.time });
                dropped = true;
            }
            if (!dropped)
                return false;
            isGrounded = false;
            groundCollider = null;
            // Push him down so he clears the platform quickly.
            if (rb != null && dropThroughForce > 0)
                rb.AddForce(Vector2.down * dropThroughForce, ForceMode2D.Impulse);
            return true;
        }

        /// <summary>Gives each platform he dropped through back its collision once he is below it (or stopped falling past it).</summary>
        private void UpdateDropThrough()
        {
            if (droppingThrough.Count == 0)
                return;
            var b = col.bounds;
            for (int i = droppingThrough.Count - 1; i >= 0; i--)
            {
                var drop = droppingThrough[i];
                var platform = drop.platform;
                if (platform == null || !platform.enabled || !platform.gameObject.activeInHierarchy)
                {
                    IgnorePlatform(platform, false);
                    droppingThrough.RemoveAt(i);
                    continue;
                }
                bool below = b.max.y < platform.bounds.min.y - 0.02f;
                bool stuck = Time.time - drop.since > 1.5f && !col.Distance(platform).isOverlapped;
                if (!below && !stuck)
                    continue;
                IgnorePlatform(platform, false);
                droppingThrough.RemoveAt(i);
                if (below)
                    DroppedThrough?.Invoke(this, platform);
            }
        }

        /// <summary>Notes the one-way platforms he is jumping up through, and reports the landing on one of them.</summary>
        private void UpdateRiseThrough()
        {
            for (int i = risingThrough.Count - 1; i >= 0; i--)
            {
                var rise = risingThrough[i];
                if (rise.platform == null || Time.time - rise.since > 2f)
                    risingThrough.RemoveAt(i);
                else if (isGrounded && groundCollider == rise.platform)
                {
                    risingThrough.RemoveAt(i);
                    JumpedUpThrough?.Invoke(this, rise.platform);
                }
            }
            if (rb == null || rb.linearVelocity.y < 0.1f || isGrounded)
                return;
            var b = col.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer | 1, useTriggers = false };
            int n = Physics2D.OverlapBox(b.center, b.size, 0f, filter, footholdHits);
            for (int i = 0; i < n; i++)
            {
                var hit = footholdHits[i];
                // Inside it on the way up: his feet well under its top (not a lift lifting him).
                if (hit == null || hit.attachedRigidbody == rb || !DropThroughPlatform.IsOneWay(hit) || b.min.y > hit.bounds.max.y - 0.15f)
                    continue;
                bool known = false;
                for (int k = 0; k < risingThrough.Count; k++)
                {
                    if (risingThrough[k].platform == hit)
                    {
                        risingThrough[k] = new DropThrough { platform = hit, since = Time.time };
                        known = true;
                    }
                }
                if (!known)
                    risingThrough.Add(new DropThrough { platform = hit, since = Time.time });
            }
        }

        /// <summary>Every platform he was dropping through is solid for him again (a rewind puts him back on top of them).</summary>
        private void ClearDropThrough()
        {
            dropHold = 0f;
            foreach (var drop in droppingThrough)
                IgnorePlatform(drop.platform, false);
            droppingThrough.Clear();
            risingThrough.Clear();
        }
        
        /// <summary>Set at runtime by LevelRuntime after resolving references.</summary>
        public void SetProjectilePool(ObjectPool pool) => projectilePool = pool;
        /// <summary>The pool his shots come from (read only: the witness trigger notices them passing).</summary>
        public ObjectPool ShotPool => projectilePool;
        
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
            
            if (held)
            {
                // The holder sets his position each tick; keep moving with it in between.
                rb.linearVelocity = heldVelocity;
                return;
            }

            if (isGrounded)
                swingMomentum = 0f;
            else
                swingMomentum = Mathf.MoveTowards(swingMomentum, 0f, 3f * Time.fixedDeltaTime);

            Vector2 velocity = rb.linearVelocity;
            float now = MoveNow;
            if (now < dashEnd)
            {
                // The dash: a flat burst (gravity can't pull him down out of it), whatever the stick says.
                velocity.x = dashDir * moves.dash.distance / Mathf.Max(0.02f, moves.dash.duration) + CarrierVelocity.x;
                velocity.y = 0f;
                rb.linearVelocity = velocity;
                return;
            }
            velocity.x = horizontalInput * moveSpeed * MoveSpeedMultiplier + CarrierVelocity.x + swingMomentum + WindVelocity.x;
            if (now < kickUntil)
                velocity.x = kickDir * moves.wallJump.kickSpeed;
            if (wallSide != 0)
                velocity.y = Mathf.Max(velocity.y, -moves.wallJump.slideSpeed);
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
                    graphicsTransform.rotation = Quaternion.Euler(0f, currentYRotation, gravitySign < 0 ? 180f : 0f);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(0f, currentYRotation, gravitySign < 0 ? 180f : 0f);
                }
            }
        }
        
        private void HandleJump()
        {
            if (jumpRequested)
            {
                jumpRequested = false;
                lastJumpPressTime = Time.time;
            }
            // A press stays good for a moment (landing a hair late) ...
            if (Time.time - lastJumpPressTime > JumpBufferSeconds) return;

            if (held)
            {
                // Jumping off a chain: let go with its swing plus a jump (off a ladder: out the way the stick points).
                float jumpOffX = heldJumpOffX;
                ReleaseHold();
                lastJumpPressTime = float.NegativeInfinity;
                jumpedOffHold = true;
                swingMomentum = heldVelocity.x + jumpOffX;
                isGrounded = false;
                Jump();
                return;
            }

            if (TryWallKick())
                return;

            bool fresh = lastJumpPressTime == Time.time;
            // ... and just after leaving a surface he can still jump off it (not while already rising).
            bool coyote = Time.time - lastGroundedTime <= CoyoteSeconds && (rb == null || rb.linearVelocity.y * gravitySign <= 0.1f);
            if (isGrounded || coyote)
            {
                lastJumpPressTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                Jump();
                airJumpsRemaining = maxAirJumps;
            }
            else if (fresh && airJumpsRemaining > 0)
            {
                lastJumpPressTime = float.NegativeInfinity;
                Jump();
                airJumpsRemaining--;
            }
        }

        private const float CoyoteSeconds = 0.12f;
        private const float JumpBufferSeconds = 0.12f;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastJumpPressTime = float.NegativeInfinity;
        
        private void Jump()
        {
            if (rb == null) return;
            
            Vector2 velocity = rb.linearVelocity;
            velocity.y = jumpForce * gravitySign;
            rb.linearVelocity = velocity;
            
            if (Cue(jumpSound, SickOfIt.Core.Audio.Sfx.Events.playerJump))
                SickOfIt.Core.Fx.Fx.Dust(Feet, 0.6f);
            
            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }
        
        #endregion
        
        #region Grapple, wind and gravity (floor kit)

        // The hook is thrown here (a key, the gamepad) and flown by HeroGrapple, the world's side. Wind and the gravity flip are set by
        // floor pieces every simulation tick; none of it is rewound here (the pieces re-derive it from their saved state).
        private Key[] grappleKeys;
        private bool grappleRequested;
        private int gravitySign = 1;

        /// <summary>Gravity pulls down (+1) or, in a gravity-flip zone, up (-1).</summary>
        public int GravitySign => gravitySign;

        /// <summary>The wind pushing him now (set by wind zones every tick; the sideways part moves him whatever his own speed).</summary>
        public Vector2 WindVelocity { get; set; }

        /// <summary>A jump's upward speed (the hook's release adds a share of it).</summary>
        public float JumpSpeed => jumpForce;

        /// <summary>The hook is his: the story or a pickup unlocked it (player.json moves.grapple).</summary>
        public bool GrappleUnlocked
        {
            get
            {
                var g = moves.grapple;
                if (g == null || !g.enabled)
                    return false;
                string flag = string.IsNullOrEmpty(g.unlockFlag) ? "move:grapple" : g.unlockFlag;
                return UnlockFlags.Has(flag) || (g.unlockWhen != null && g.unlockWhen.Count > 0 && Requirements.Met(g.unlockWhen));
            }
        }

        /// <summary>Asks for the hook on the next frame (a HUD button, a test).</summary>
        public void RequestGrapple() => grappleRequested = true;

        /// <summary>True once per press of the hook key (the world consumes it).</summary>
        public bool ConsumeGrappleRequest()
        {
            bool asked = grappleRequested;
            grappleRequested = false;
            return asked && !InputLocked && !Suspended && !ScriptedMove.HasValue;
        }

        /// <summary>
        /// Where the hook is aimed: the right stick, else the mouse (once it has been used), else the move stick leaning up (sideways = up and
        /// that way), else up and the way he faces. Not normalised to a level: the hook takes the nearest anchor inside its cone.
        /// </summary>
        public Vector2 GrappleAim()
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.magnitude > 0.4f)
                    return stick.normalized;
            }
            if (aimingWithMouse && mainCamera != null && Mouse.current != null && MouseOnScreen())
            {
                Vector2 toMouse = GetMouseDirectionFromPlayer();
                if (toMouse.magnitude >= inputDeadzone)
                    return toMouse.normalized;
            }
            if (moveInput.magnitude > 0.4f)
                return new Vector2(moveInput.x, Mathf.Max(moveInput.y, 0.9f)).normalized;
            return new Vector2(FacingRight ? 1f : -1f, 1.1f).normalized;
        }

        /// <summary>Carries a speed sideways after the rope lets go (it fades in the air like a chain's swing).</summary>
        public void KeepMomentum(float x) => swingMomentum = x;

        /// <summary>The floor pieces set gravity for this tick: flipped zones pull him up (+1 / -1).</summary>
        public void SetGravitySign(int sign)
        {
            sign = sign < 0 ? -1 : 1;
            if (sign == gravitySign)
                return;
            gravitySign = sign;
            if (rb != null && !held)
                rb.gravityScale = Mathf.Abs(rb.gravityScale) * sign;
            gravityBeforeHold = Mathf.Abs(gravityBeforeHold) * sign;
            // Standing on the old floor means nothing now: the next ground check looks the new way.
            isGrounded = false;
            lastGroundedTime = float.NegativeInfinity;
        }

        private void ResolveGrappleKeys()
        {
            var list = new System.Collections.Generic.List<Key>();
            if (moves.grapple != null && moves.grapple.keys != null)
            {
                foreach (string name in moves.grapple.keys)
                    if (System.Enum.TryParse(name, out Key key))
                        list.Add(key);
            }
            grappleKeys = list.ToArray();
        }

        private void ReadGrappleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && grappleKeys != null)
            {
                foreach (var key in grappleKeys)
                {
                    if (keyboard[key].wasPressedThisFrame)
                        grappleRequested = true;
                }
            }
            var pad = Gamepad.current;
            if (pad != null && (pad.leftShoulder.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame))
                grappleRequested = true;
        }

        #endregion

        #region Hero Moves (dash, wall-jump)

        // player.json "moves". Locked until the story or Yooba's shop unlocks each one (SetMovesUnlocked).
        private HeroMovesConfig moves = new HeroMovesConfig();
        private bool dashUnlocked, wallJumpUnlocked;
        private bool dashRequested;
        private Key[] dashKeys;

        // State a rewind must put back (GetMoveState / SetMoveState, saved with the world). Times are game-clock seconds.
        private float dashEnd = -100f, dashInvulnUntil = -100f, dashReadyAt = -100f, dashDir = 1f;
        private int airDashesLeft;
        private int wallSide, lastWallSide;
        private float wallCoyoteUntil = -100f, kickUntil = -100f, kickDir;
        // Presentation only: the pose playing now.
        private string movePose;
        private float kickPoseUntil = -100f;
        private bool dashReadyPending;

        private static readonly RaycastHit2D[] wallHits = new RaycastHit2D[8];

        /// <summary>He dashed (the dash just started).</summary>
        public static event System.Action<PlayerController> Dashed;
        /// <summary>He caught a wall (the side: -1 left, +1 right).</summary>
        public static event System.Action<PlayerController, int> WallClung;
        /// <summary>He kicked off a wall (the side the wall was on).</summary>
        public static event System.Action<PlayerController, int> WallJumped;
        /// <summary>A move's first use in this save (for the hero's line): "dash" or "wallJump".</summary>
        public static event System.Action<PlayerController, string> MoveFirstUsed;

        /// <summary>What a rewind keeps of the moves (the controller's own flags are not recorded).</summary>
        public struct MoveState
        {
            public float dashEnd, dashInvulnUntil, dashReadyAt, dashDir, wallCoyoteUntil, kickUntil, kickDir;
            public int airDashesLeft, wallSide, lastWallSide;
        }

        public MoveState GetMoveState() => new MoveState
        {
            dashEnd = dashEnd, dashInvulnUntil = dashInvulnUntil, dashReadyAt = dashReadyAt, dashDir = dashDir,
            wallCoyoteUntil = wallCoyoteUntil, kickUntil = kickUntil, kickDir = kickDir,
            airDashesLeft = airDashesLeft, wallSide = wallSide, lastWallSide = lastWallSide,
        };

        public void SetMoveState(MoveState s)
        {
            dashEnd = s.dashEnd; dashInvulnUntil = s.dashInvulnUntil; dashReadyAt = s.dashReadyAt; dashDir = s.dashDir;
            wallCoyoteUntil = s.wallCoyoteUntil; kickUntil = s.kickUntil; kickDir = s.kickDir;
            airDashesLeft = s.airDashesLeft; wallSide = s.wallSide; lastWallSide = s.lastWallSide;
            dashRequested = false;
            dashReadyPending = s.dashReadyAt > MoveNow;
        }

        /// <summary>The world unlocks the moves (story flags and Yooba's goods) when a floor starts.</summary>
        public void SetMovesUnlocked(bool dash, bool wallJump)
        {
            dashUnlocked = dash;
            wallJumpUnlocked = wallJump;
        }

        public bool DashUnlocked => dashUnlocked && moves.dash.enabled;
        public bool WallJumpUnlocked => wallJumpUnlocked && moves.wallJump.enabled;
        private static float MoveNow => GameClock.Instance != null ? GameClock.Instance.Time : Time.time;

        /// <summary>The dash burst is under way.</summary>
        public bool IsDashing => MoveNow < dashEnd;
        /// <summary>Nothing can hurt him (the dash's grace, a hair longer than the burst).</summary>
        public bool DashInvulnerable => MoveNow < dashInvulnUntil;
        /// <summary>Seconds until the next dash is ready (0 = ready).</summary>
        public float DashCooldownLeft => Mathf.Max(0f, dashReadyAt - MoveNow);
        public int AirDashesLeft => airDashesLeft;
        /// <summary>Which side he clings to a wall on: -1 left, +1 right, 0 none.</summary>
        public int WallSide => wallSide;
        /// <summary>Clinging to a wall and sliding down it (not rising).</summary>
        public bool IsWallClinging => wallSide != 0 && rb != null && rb.linearVelocity.y <= 0.1f;
        /// <summary>Kicking off a wall: the kick's push is still on.</summary>
        public bool IsWallKicking => MoveNow < kickUntil;
        /// <summary>The move pose playing now ("Dash", "WallSlide", "WallJump") or null; a playtest reads it.</summary>
        public string MovePose => movePose;
        private bool runFirePlaying;
        /// <summary>Firing while walking on the ground: the RunFire clip is playing; a playtest reads it.</summary>
        public bool IsRunFiring => runFirePlaying && movePose == null && !IsClimbing;
        /// <summary>The dash's distance in world units (player.json moves.dash).</summary>
        public float DashDistance => moves.dash.distance;
        public float DashDuration => moves.dash.duration;

        /// <summary>Asks for a dash on the next frame (the HUD's touch button).</summary>
        public void RequestDash() => dashRequested = true;

        private void ResolveDashKeys()
        {
            var list = new System.Collections.Generic.List<Key>();
            if (moves.dash.keys != null)
            {
                foreach (string name in moves.dash.keys)
                    if (System.Enum.TryParse(name, out Key key))
                        list.Add(key);
            }
            dashKeys = list.ToArray();
        }

        private void ReadDashInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && dashKeys != null)
            {
                foreach (var key in dashKeys)
                {
                    if (keyboard[key].wasPressedThisFrame)
                        dashRequested = true;
                }
            }
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.leftStickButton.wasPressedThisFrame))
                dashRequested = true;
        }

        /// <summary>Starts a dash when one was asked for and he can: unlocked, ready, a charge in the air, not held or locked.</summary>
        private void HandleDash()
        {
            if (dashReadyPending && MoveNow >= dashReadyAt)
            {
                dashReadyPending = false;
                Cue(null, SickOfIt.Core.Audio.Sfx.Events.playerDashReady);
            }
            bool want = dashRequested;
            dashRequested = false;
            if (!want || !DashUnlocked || held || Suspended || InputLocked || ScriptedMove.HasValue || rb == null)
                return;
            var dash = moves.dash;
            float now = MoveNow;
            if (now < dashReadyAt || now < dashEnd)
                return;
            if (!isGrounded && airDashesLeft <= 0)
                return;
            float dir = Mathf.Abs(moveInput.x) > inputDeadzone ? Mathf.Sign(moveInput.x) : (FacingRight ? 1f : -1f);
            // Never into the wall he hangs on: away from it.
            if (wallSide != 0 && dir == wallSide)
                dir = -wallSide;
            if (!isGrounded)
                airDashesLeft--;
            dashDir = dir;
            dashEnd = now + dash.duration;
            dashInvulnUntil = now + Mathf.Max(dash.duration, dash.invulnerableSeconds);
            dashReadyAt = now + Mathf.Max(dash.cooldown, dash.duration);
            wallSide = 0;
            dashReadyPending = true;
            Face(dir);
            // Straight into it: the burst is flat, and he stops falling at once.
            rb.linearVelocity = new Vector2(dir * dash.distance / Mathf.Max(0.02f, dash.duration), 0f);
            if (Cue(null, SickOfIt.Core.Audio.Sfx.Events.playerDash))
            {
                SickOfIt.Core.Fx.Fx.Dust(Feet, 1.2f);
                SickOfIt.Core.Fx.Fx.Flash(transform.position, new Color(0.7f, 1f, 0.8f, 0.4f), 0.5f, 0.1f);
            }
            Dashed?.Invoke(this);
            FirstUse("dash");
        }

        // The hero's line the first time a move is used (kept in UnlockFlags, so a save hears it once).
        private void FirstUse(string move)
        {
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;
            MoveFirstUsed?.Invoke(this, move);
        }

        /// <summary>
        /// Looks for a wall on the side he pushes toward while airborne. Touching one lets him cling (slide slowly) and kick off;
        /// the kick's own push, a dash, a hold and the ground all mean no wall.
        /// </summary>
        private void UpdateWall()
        {
            int side = 0;
            float now = MoveNow;
            if (WallJumpUnlocked && !isGrounded && !held && !Suspended && col != null
                && now >= kickUntil && now >= dashEnd && Mathf.Abs(moveInput.x) > 0.3f)
            {
                int want = moveInput.x > 0f ? 1 : -1;
                if (TouchingWall(want))
                    side = want;
            }
            if (side != 0)
            {
                if (wallSide == 0)
                {
                    if (Cue(null, SickOfIt.Core.Audio.Sfx.Events.playerWallCling))
                        SickOfIt.Core.Fx.Fx.Dust(new Vector2(col.bounds.center.x + side * col.bounds.extents.x, col.bounds.center.y), 0.5f);
                    WallClung?.Invoke(this, side);
                    FirstUse("wallJump");
                }
                lastWallSide = side;
                wallCoyoteUntil = now + moves.wallJump.coyoteSeconds;
            }
            wallSide = side;
        }

        /// <summary>A solid wall within reach on this side (the level's walls and solid entities; never a one-way piece or a trigger).</summary>
        private bool TouchingWall(int side)
        {
            var b = col.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer | 1, useTriggers = false };
            Vector2 size = new Vector2(b.size.x, b.size.y * 0.7f);
            int n = Physics2D.BoxCast(b.center, size, 0f, new Vector2(side, 0f), filter, wallHits, moves.wallJump.wallCheckDistance);
            for (int i = 0; i < n; i++)
            {
                var hit = wallHits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.attachedRigidbody == rb || DropThroughPlatform.IsOneWay(hit.collider))
                    continue;
                if (Mathf.Abs(hit.normal.x) > 0.7f && hit.normal.x * side < 0f)
                    return true;
            }
            return false;
        }

        /// <summary>Jump while clinging (or a hair after losing the wall) kicks off it: up and away, with the dash charge back.</summary>
        private bool TryWallKick()
        {
            float now = MoveNow;
            if (isGrounded || !WallJumpUnlocked || rb == null || now < kickUntil)
                return false;
            if (wallSide == 0 && now >= wallCoyoteUntil)
                return false;
            int onSide = wallSide != 0 ? wallSide : lastWallSide;
            if (onSide == 0)
                return false;
            var kick = moves.wallJump;
            lastJumpPressTime = float.NegativeInfinity;
            kickDir = -onSide;
            kickUntil = now + kick.kickLockSeconds;
            kickPoseUntil = now + 0.35f;
            wallCoyoteUntil = -100f;
            wallSide = 0;
            swingMomentum = 0f;
            airDashesLeft = Mathf.Max(0, moves.dash.airDashes);
            rb.linearVelocity = new Vector2(kickDir * kick.kickSpeed, kick.kickUp * gravitySign);
            Face(kickDir);
            if (Cue(null, SickOfIt.Core.Audio.Sfx.Events.playerWallJump))
                SickOfIt.Core.Fx.Fx.Dust(new Vector2(col.bounds.center.x + onSide * col.bounds.extents.x, col.bounds.center.y), 0.8f);
            WallJumped?.Invoke(this, onSide);
            return true;
        }

        // The Dash, WallSlide and WallJump clips (player.json states; stand-in sheets until the Hawkes Gen clips are made) play over the Animator while the move lasts.
        private void UpdateMovePose()
        {
            if (animator == null || IsClimbing)
                return;
            float now = MoveNow;
            string pose = now < dashEnd ? "Dash" : now < kickPoseUntil ? "WallJump" : IsWallClinging ? "WallSlide" : null;
            if (pose == movePose)
                return;
            movePose = pose;
            if (pose != null)
            {
                if (!PlayAnimatorState(pose))
                    movePose = null;
            }
            else
            {
                PlayAnimatorState(isGrounded ? "Idle" : "Fall");
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
            WeaponStats weapon = default;
            bool armed = Armory != null && Armory.TryGetCurrent(out weapon);
            if (armed && !Armory.CanUse(currentTime))
            {
                if (Armory.Empty)
                    Armory.Dry(currentTime);
                return;
            }
            if (armed && weapon.melee)
            {
                SwingWeapon(currentTime);
                return;
            }

            float interval = (armed ? weapon.cooldown : fireRate) / Mathf.Max(0.1f, FireRateMultiplier * (IsClimbing ? climbFireScale : 1f));
            if (currentTime < lastFireTime)
                lastFireTime = currentTime - interval;
            if (currentTime - lastFireTime < interval) return;
            if (projectilePool == null) return;

            FireProjectile(fireDirection);
            if (armed)
                Armory.Fired(currentTime);
        }

        /// <summary>A melee weapon: swing toward the aim. The armory times the hit; the hero plays his fire pose.</summary>
        private void SwingWeapon(float currentTime)
        {
            Vector2 origin = AimVolley(fireDirection, mouseAimPoint, volley);
            Vector2 direction = volley.Count > 0 ? volley[0] : fireDirection.normalized;
            if (!Armory.Swing(currentTime, origin, direction, FireRateMultiplier, DamageMultiplier))
                return;
            lastFireTime = currentTime;
            VolleysFired++;
            lastVolley.Clear();
            lastVolley.Add(direction.normalized);
            PlayFireAnimation();
        }

        /// <summary>Switch weapon: Q or the mouse wheel on the keyboard, the gamepad's top button and left shoulder; the HUD's weapon button on touch.</summary>
        private void HandleWeaponSwitch()
        {
            if (Armory == null || InputLocked || Suspended || ScriptedMove.HasValue)
                return;
            if (GameClock.Instance != null && GameClock.Instance.IsPaused)
                return;
            int step = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
                step = 1;
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.buttonNorth.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)
                    step = 1;
                else if (pad.leftShoulder.wasPressedThisFrame)
                    step = -1;
            }
            var mouse = Mouse.current;
            if (step == 0 && mouse != null && InputOverride == null && MouseOnScreen()
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.5f) step = -1;
                else if (scroll < -0.5f) step = 1;
            }
            if (step != 0)
                Armory.Cycle(step, GameClock.Instance != null ? GameClock.Instance.Time : Time.time);
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

            // The shot's numbers come from the equipped weapon (the launcher's are player.json's).
            shotSpeed = projectileSpeed;
            shotLifetime = projectileLifetime;
            shotFaceAim = projectileFaceAim;
            shotSpriteAngle = projectileSpriteAngle;
            shotDamageScale = 1f;
            shotSound = null;
            shotEntity = null;
            if (Armory != null && Armory.TryGetCurrent(out var weapon) && !weapon.melee)
            {
                shotEntity = weapon.shotEntity;
                shotSpeed = weapon.projectileSpeed;
                shotLifetime = weapon.projectileLifetime;
                shotFaceAim = weapon.projectileFaceAim;
                shotSpriteAngle = weapon.projectileSpriteAngle;
                shotDamageScale = weapon.damageScale;
                shotSound = weapon.useSound;
            }

            shotSpeed *= ShotSpeedScale;
            shotCrit = VolleyScale != null ? VolleyScale() : 1f;
            Vector2 muzzle = AimVolley(direction, mouseAimPoint, volley);
            VolleysFired++;
            lastVolley.Clear();
            if (!string.IsNullOrEmpty(shotEntity))
            {
                // A weapon with a shot of its own (a bouncing bullet, a boomerang): the world spawns it as a rewindable entity.
                float now = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
                for (int i = 0; i < volley.Count; i++)
                    lastVolley.Add(volley[i].normalized);
                Armory.SpawnShots(muzzle, volley, shotSpeed, DamageMultiplier * shotDamageScale * shotCrit, now);
                if (Cue(fireSound, string.IsNullOrEmpty(shotSound) ? SickOfIt.Core.Audio.Sfx.Events.playerShoot : shotSound))
                    SickOfIt.Core.Fx.Fx.Flash(muzzle, new Color(1f, 0.85f, 0.45f, 0.35f), 0.26f, 0.06f);
                PlayFireAnimation();
                return;
            }
            for (int i = 0; i < volley.Count; i++)
                SpawnShot(muzzle, volley[i], i == 0);
        }

        private float shotSpeed, shotLifetime, shotSpriteAngle, shotDamageScale = 1f;
        private bool shotFaceAim;
        private string shotSound, shotEntity;

        private readonly System.Collections.Generic.List<Vector2> volley = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.List<Vector2> lastVolley = new System.Collections.Generic.List<Vector2>();

        /// <summary>Volleys fired since he spawned (the aim guide notices a new one).</summary>
        public int VolleysFired { get; private set; }

        /// <summary>The flight direction of each shot of the last volley, read back from the projectiles (the playtest compares the aim guide with it).</summary>
        public System.Collections.Generic.IReadOnlyList<Vector2> LastVolley => lastVolley;

        /// <summary>
        /// The one place a volley is aimed: where it leaves the gun and each shot's direction (in firing order),
        /// for an aim 'direction' and, with the mouse, the world point under it. Firing and the aim guide both use it,
        /// so the guide always shows the real shots.
        /// </summary>
        private Vector2 AimVolley(Vector2 direction, Vector2? aimPoint, System.Collections.Generic.List<Vector2> directions)
        {
            // Aim from the muzzle so the shot passes through the cursor, not parallel to it.
            Vector2 muzzle = GetMuzzlePosition(direction);
            if (aimPoint.HasValue && (aimPoint.Value - (Vector2)transform.position).sqrMagnitude > 0.25f)
            {
                Vector2 toCursor = aimPoint.Value - muzzle;
                if (toCursor.sqrMagnitude > 0.0001f && Vector2.Dot(toCursor, direction) > 0f)
                    direction = toCursor.normalized;
            }

            if (AimAssist != null)
                direction = AimAssist(muzzle, direction);

            // Extra shots fan out evenly around the aim.
            directions.Clear();
            int shots = 1;
            float spread = ShotSpreadDegrees;
            if (Armory != null && Armory.TryGetCurrent(out var held) && held.pellets > 1)
            {
                // A blast: the weapon's own pellets, fanned by its own spread.
                shots = held.pellets;
                spread = held.pelletSpread;
            }
            if (Armory == null || !Armory.TryGetCurrent(out var weapon) || weapon.multiShot)
                shots += Mathf.Max(0, ExtraShots);
            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * spread;
                directions.Add(Quaternion.Euler(0f, 0f, angle) * direction);
            }
            return muzzle;
        }

        /// <summary>
        /// Where his next volley would go right now, for the aim guide: the muzzle and one direction per shot (in
        /// firing order), from the same aim and the same AimVolley that fires. While firing that is the aim being
        /// fired; with the mouse it is where a click would fire. False when there is no aim or he can't fire
        /// (shooting off, input locked by a scene, walked or pulled by a script, a rewind). Reads input only.
        /// </summary>
        public bool TryGetAimGuide(System.Collections.Generic.List<Vector2> directions, out Vector2 muzzle)
        {
            muzzle = transform.position;
            directions.Clear();
            GuideLengthScale = 1f;
            if (!enableShooting || projectilePool == null || InputLocked || Suspended || ScriptedMove.HasValue)
                return false;
            // The guide follows the weapon: the launcher's line, fists' short reach line, nothing for a weapon without one.
            if (Armory != null && Armory.TryGetCurrent(out var equipped))
            {
                if (!equipped.aimGuide)
                    return false;
                GuideLengthScale = equipped.aimGuideLength > 0f ? equipped.aimGuideLength : 1f;
            }
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return false;
            Vector2 direction;
            Vector2? aimPoint;
            if (isFiring && fireDirection.magnitude >= inputDeadzone)
            {
                direction = fireDirection;
                aimPoint = mouseAimPoint;
            }
            else if (aimingWithMouse && InputOverride == null && !TouchAim && mainCamera != null && Mouse.current != null && MouseOnScreen())
            {
                // Not firing: where a click would fire (ReadFireInput's mouse path).
                direction = GetMouseDirectionFromPlayer();
                aimPoint = GetMouseWorldPoint();
                if (direction.magnitude < inputDeadzone)
                    return false;
            }
            else
            {
                return false;
            }
            muzzle = AimVolley(direction, aimPoint, directions);
            return true;
        }

        // The mouse moved or fired after the last stick aim: the guide follows the cursor between clicks.
        private bool aimingWithMouse;

        private void TrackAimDevice(bool aimedWithStick)
        {
            if (aimedWithStick)
            {
                aimingWithMouse = false;
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            Vector2 position = mouse.position.ReadValue();
            if ((position - lastMousePosition).sqrMagnitude > 1f || mouse.leftButton.isPressed)
                aimingWithMouse = true;
            lastMousePosition = position;
        }

        private Vector2 lastMousePosition = new Vector2(float.NaN, float.NaN);

        // The on-screen fire stick is up (a phone): touches aren't a hovering mouse.
        private bool TouchAim => fireAnalog != null && fireAnalog.gameObject.activeInHierarchy;

        private static bool MouseOnScreen()
        {
            Vector2 p = Mouse.current.position.ReadValue();
            return p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
        }

        private void SpawnShot(Vector2 muzzle, Vector2 direction, bool firstOfVolley)
        {
            Vector3 spawnPosition = new Vector3(muzzle.x, muzzle.y, transform.position.z);
            Quaternion rotation = shotFaceAim
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - shotSpriteAngle)
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
                projectileRb.linearVelocity = direction * shotSpeed;
            }
            lastVolley.Add(projectileRb != null && projectileRb.linearVelocity.sqrMagnitude > 0f ? projectileRb.linearVelocity.normalized : direction);

            projectilePool.CaptureLaunchState(projectile);

            if (!shotDamage.TryGetValue(projectile, out var damage))
            {
                damage = projectile.GetComponent<IProjectileDamage>();
                shotDamage[projectile] = damage;
            }
            damage?.SetDamageMultiplier(DamageMultiplier * shotDamageScale * shotCrit);

            // Use pool-cached ProjectileLifetime when available
            var lifetime = projectilePool.GetCachedLifetime(projectile);
            if (lifetime == null) lifetime = projectile.GetComponent<ProjectileLifetime>();
            if (lifetime != null)
                lifetime.SetLifetime(shotLifetime);

            if (!firstOfVolley)
                return;

            // Firing happens constantly, so its feedback stays quiet: a faint, small flash and a soft sound.
            if (Cue(fireSound, string.IsNullOrEmpty(shotSound) ? SickOfIt.Core.Audio.Sfx.Events.playerShoot : shotSound))
                SickOfIt.Core.Fx.Fx.Flash(muzzle, new Color(1f, 0.85f, 0.45f, 0.35f), 0.26f, 0.06f);

            PlayFireAnimation();
        }

        /// <summary>
        /// Restarts the fire clip on every shot or swing so the muzzle flash lines up with it.
        /// Walking on the ground, keep the walk clip going (no standing fire pose while his legs move).
        /// </summary>
        private void PlayFireAnimation()
        {
            if (animator != null && IsWalkingOnGround() && animator.HasState(0, WalkStateHash))
                return;
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

        private bool IsWalkingOnGround() => isGrounded && Mathf.Abs(moveInput.x) > 0.1f;

        private bool AnimatorHasFireTrigger()
        {
            AnimatorHasFiring();
            return animatorHasFireTrigger;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            if (IsClimbing)
            {
                UpdateClimbPose();
                return;
            }
            if (movePose != null)
            {
                runFirePlaying = false;
                return;
            }
            
            animator.SetBool(OnGroundHash, isGrounded);
            if (AnimatorHasFiring())
                animator.SetBool(FiringHash, isFiring && enableShooting);
            animator.SetFloat(VelocityXHash, Mathf.Abs(moveInput.x));
            // Fire was entered standing; once he walks, move on to the walk clip (Walk -> Idle handles stopping).
            bool runFire = isFiring && enableShooting && IsWalkingOnGround();
            if (runFire && !runFirePlaying)
            {
                runFirePlaying = PlayAnimatorState("RunFire");
                if (!runFirePlaying && animator.HasState(0, WalkStateHash)
                    && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == FireStateHash)
                    animator.Play(WalkStateHash, 0, 0f);
            }
            else if (!runFire && runFirePlaying)
            {
                // He stopped or stopped firing: back to the clip the animator's own parameters pick.
                runFirePlaying = false;
                PlayAnimatorState(isFiring && enableShooting ? "Fire" : IsWalkingOnGround() ? "Walk" : "Idle");
            }
            
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
            
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, shadowRaycastDistance, groundLayer);
            
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
