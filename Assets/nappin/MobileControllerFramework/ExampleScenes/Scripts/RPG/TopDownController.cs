using UnityEngine;
using UnityEngine.InputSystem;


namespace MobileControllerFramework
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TopDownController : MonoBehaviour
    {
        [Header("Movement")]
        public Vector2 speed = Vector2.one;
        public float dashSpeed = 25f;
        public float dashDuration = 0.2f; // how long the dash lasts
        
        [Header("Shooting")]
        public float ballSpeed = 10f;
        public float fireRate = 0.25f; // Time between shots

        [Header("References")]
        public GameObject swordIn;
        public GameObject swordOut;
        public GameObject ballObj;
        public GameObject dustPrt;
        public Analog analog;
        public Analog moveAnalog; // Use analog stick for smooth movement
        public Dpad dpad;
        public ArrowKeys arrowKeys;
        public Animator anim;

        [Header("Input System (Optional)")]
        public InputActionReference moveAction;
        public InputActionReference dashLeftAction;
        public InputActionReference dashRightAction;
        public InputActionReference attackAction;
        public InputActionReference placeBallAction;
        public Camera mainCamera;

        private Vector3 facingRight = Vector3.one;
        private Vector3 facingLeft = new Vector3(-1, 1, 1);

        private Rigidbody2D rb;
        private Vector2Int input;
        private Vector2 keyboardInput;
        private Vector2 analogInput; // Smooth analog input
        private Vector2 shootDirection; // Direction to shoot

        private bool canWalk = true;
        private bool isDashing = false;
        private bool swordShown = false;
        private bool isFiring = false; // PC fire button held
        private float lastFireTime = 0f;


        /**/


        #region Setup

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            // Enable Input System actions with safe null checks
            try
            {
                if (moveAction != null && moveAction.action != null)
                    moveAction.action.Enable();
                
                if (dashLeftAction != null && dashLeftAction.action != null)
                {
                    dashLeftAction.action.performed -= OnDashLeft; // Remove first to prevent duplicates
                    dashLeftAction.action.performed += OnDashLeft;
                    dashLeftAction.action.Enable();
                }
                
                if (dashRightAction != null && dashRightAction.action != null)
                {
                    dashRightAction.action.performed -= OnDashRight;
                    dashRightAction.action.performed += OnDashRight;
                    dashRightAction.action.Enable();
                }
                
                if (attackAction != null && attackAction.action != null)
                {
                    attackAction.action.performed -= OnAttackPressed;
                    attackAction.action.canceled -= OnAttackReleased;
                    attackAction.action.performed += OnAttackPressed;
                    attackAction.action.canceled += OnAttackReleased;
                    attackAction.action.Enable();
                }
                
                if (placeBallAction != null && placeBallAction.action != null)
                {
                    placeBallAction.action.started -= OnPlaceBallStarted;
                    placeBallAction.action.canceled -= OnPlaceBallCanceled;
                    placeBallAction.action.started += OnPlaceBallStarted;
                    placeBallAction.action.canceled += OnPlaceBallCanceled;
                    placeBallAction.action.Enable();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to enable input actions: {e.Message}");
            }
        }

        private void OnDisable()
        {
            // Disable Input System actions
            if (moveAction != null && moveAction.action != null)
                moveAction.action.Disable();
            
            if (dashLeftAction != null && dashLeftAction.action != null)
            {
                dashLeftAction.action.performed -= OnDashLeft;
                dashLeftAction.action.Disable();
            }
            
            if (dashRightAction != null && dashRightAction.action != null)
            {
                dashRightAction.action.performed -= OnDashRight;
                dashRightAction.action.Disable();
            }
            
            if (attackAction != null && attackAction.action != null)
            {
                attackAction.action.performed -= OnAttackPressed;
                attackAction.action.canceled -= OnAttackReleased;
                attackAction.action.Disable();
            }
            
            if (placeBallAction != null && placeBallAction.action != null)
            {
                placeBallAction.action.started -= OnPlaceBallStarted;
                placeBallAction.action.canceled -= OnPlaceBallCanceled;
                placeBallAction.action.Disable();
            }
        }

        #endregion


        #region Update & Movement

        private void Update()
        {
            HandleInput();
            HandleFacing();
            HandleAnimation();
            HandleShooting();
        }


        private void FixedUpdate()
        {
            if (canWalk && !isDashing)
            {
                // Use analog input if available (smooth movement), otherwise use discrete input
                Vector2 movement = analogInput.magnitude > 0.1f 
                    ? new Vector2(analogInput.x * speed.x, analogInput.y * speed.y)
                    : new Vector2(input.x * speed.x, input.y * speed.y);
                
                rb.linearVelocity = movement;
            }
        }


        private void HandleInput()
        {
            input = Vector2Int.zero;
            keyboardInput = Vector2.zero;
            analogInput = Vector2.zero;

            // Read from move analog (smooth movement)
            if (moveAnalog != null && moveAnalog.gameObject.activeSelf)
            {
                analogInput = moveAnalog.GetStickPosition();
            }
            // Otherwise read from dpad/arrow keys (discrete movement)
            else if (dpad != null && dpad.gameObject.activeSelf)
            {
                input = dpad.GetDpadDirection();
            }
            else if (arrowKeys != null && arrowKeys.gameObject.activeSelf)
            {
                input = arrowKeys.GetDirection();
            }
            
            // Read from New Input System (keyboard/gamepad)
            if (moveAction != null && moveAction.action != null && moveAction.action.enabled)
            {
                keyboardInput = moveAction.action.ReadValue<Vector2>();
            }
            
            // Combine inputs - use whichever has magnitude (simultaneous support)
            if (analogInput.magnitude < 0.1f && input == Vector2Int.zero && keyboardInput.magnitude > 0.1f)
            {
                input = new Vector2Int(
                    Mathf.RoundToInt(keyboardInput.x),
                    Mathf.RoundToInt(keyboardInput.y)
                );
            }
        }


        private void HandleFacing()
        {
            // Check analog input first, then fall back to discrete input
            float horizontalInput = analogInput.magnitude > 0.1f ? analogInput.x : input.x;
            
            if (horizontalInput > 0.1f) transform.localScale = facingRight;
            else if (horizontalInput < -0.1f) transform.localScale = facingLeft;
        }


        private void HandleAnimation()
        {
            // Check both analog and discrete input
            bool isMoving = analogInput.magnitude > 0.1f || input.x != 0 || input.y != 0;

            if (anim != null)
                anim.Play(isMoving ? "Walk" : "Idle");

            if (dustPrt != null)
                dustPrt.SetActive(isMoving);
        }

        #endregion


        #region Dash

        public void DashLeft()
        {
            if (!canWalk || isDashing) return;
            StartCoroutine(Dash(Vector2.left, facingLeft));
        }


        public void DashRight()
        {
            if (!canWalk || isDashing) return;
            StartCoroutine(Dash(Vector2.right, facingRight));
        }


        private System.Collections.IEnumerator Dash(Vector2 direction, Vector3 facing)
        {
            isDashing = true;
            canWalk = false;

            // Face the right way
            transform.localScale = facing;

            // Reset velocity for consistent dash
            rb.linearVelocity = Vector2.zero;

            // Apply dash force
            rb.AddForce(direction * dashSpeed, ForceMode2D.Impulse);

            // Dash duration
            yield return new WaitForSeconds(dashDuration);

            // Stop dash movement smoothly
            rb.linearVelocity = Vector2.zero;

            isDashing = false;
            canWalk = true;
        }

        #endregion


        #region Sword

        public void ShowSword()
        {
            if (swordIn != null) swordIn.SetActive(false);
            if (swordOut != null) swordOut.SetActive(true);
        }


        public void HideSword()
        {
            if (swordIn != null) swordIn.SetActive(true);
            if (swordOut != null) swordOut.SetActive(false);
        }

        #endregion


        #region Shooting

        private void HandleShooting()
        {
            shootDirection = Vector2.zero;

            // Get shoot direction from analog stick (mobile) - only if it has actual input
            if (analog != null && analog != moveAnalog && analog.gameObject.activeSelf)
            {
                Vector2 analogStickPos = analog.GetStickPosition();
                if (analogStickPos.magnitude > 0.1f)
                {
                    shootDirection = analogStickPos;
                }
            }
            
            // If no analog input, use mouse position when PC fire button is held
            if (shootDirection.magnitude < 0.1f && isFiring && mainCamera != null && Mouse.current != null)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
                mouseWorldPos.z = 0;
                shootDirection = (mouseWorldPos - transform.position).normalized;
            }

            // Fire if there's a direction
            if (shootDirection.magnitude > 0.1f)
            {
                FireBall(shootDirection);
            }
        }


        private void FireBall(Vector2 direction)
        {
            if (ballObj == null) return;
            if (Time.time - lastFireTime < fireRate) return; // Rate limiting

            lastFireTime = Time.time;

            // Spawn ball at player position
            GameObject ball = Instantiate(ballObj, transform.position, Quaternion.identity);
            
            // Add velocity to the ball
            Rigidbody2D ballRb = ball.GetComponent<Rigidbody2D>();
            if (ballRb != null)
            {
                ballRb.linearVelocity = direction.normalized * ballSpeed;
            }

            // Optional: Destroy ball after a few seconds
            Destroy(ball, 5f);
        }


        public void InstantiateBall()
        {
            // Legacy method - now just fires in facing direction
            Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            FireBall(direction);
        }


        public void PlaceBall()
        {
            // Legacy method for UI button compatibility
            InstantiateBall();
        }

        #endregion


        #region Input System Callbacks

        private void OnDashLeft(InputAction.CallbackContext context)
        {
            DashLeft();
        }

        private void OnDashRight(InputAction.CallbackContext context)
        {
            DashRight();
        }

        private void OnAttackPressed(InputAction.CallbackContext context)
        {
            if (!swordShown)
            {
                ShowSword();
                swordShown = true;
            }
            else
            {
                HideSword();
                swordShown = false;
            }
        }

        private void OnAttackReleased(InputAction.CallbackContext context)
        {
            // Optional: Auto-hide sword on release
            // HideSword();
            // swordShown = false;
        }

        private void OnPlaceBallStarted(InputAction.CallbackContext context)
        {
            // Start firing toward mouse
            isFiring = true;
        }

        private void OnPlaceBallCanceled(InputAction.CallbackContext context)
        {
            // Stop firing
            isFiring = false;
        }

        #endregion
    }
}