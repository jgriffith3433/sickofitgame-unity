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
        public float placeRadius = 3f;

        [Header("References")]
        public GameObject swordIn;
        public GameObject swordOut;
        public GameObject ballObj;
        public GameObject dustPrt;
        public Analog analog;
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
        private GameObject ballRef;
        private Vector2 ballMousePosition;

        private bool canWalk = true;
        private bool moveBall = false;
        private bool isDashing = false;
        private bool swordShown = false;


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
                    placeBallAction.action.performed -= OnPlaceBall;
                    placeBallAction.action.performed += OnPlaceBall;
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
                placeBallAction.action.performed -= OnPlaceBall;
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

            if (moveBall)
                UpdateBallPosition();
        }


        private void FixedUpdate()
        {
            if (canWalk && !isDashing)
            {
                rb.linearVelocity = new Vector2(input.x * speed.x, input.y * speed.y);
            }
        }


        private void HandleInput()
        {
            input = Vector2Int.zero;
            keyboardInput = Vector2.zero;

            // Read from mobile widgets
            if (dpad != null && dpad.gameObject.activeSelf)
                input = dpad.GetDpadDirection();
            else if (arrowKeys != null && arrowKeys.gameObject.activeSelf)
                input = arrowKeys.GetDirection();
            
            // Read from New Input System (keyboard/gamepad)
            if (moveAction != null && moveAction.action != null && moveAction.action.enabled)
            {
                keyboardInput = moveAction.action.ReadValue<Vector2>();
            }
            
            // Combine inputs - use whichever has magnitude (simultaneous support)
            if (input == Vector2Int.zero && keyboardInput.magnitude > 0.1f)
            {
                input = new Vector2Int(
                    Mathf.RoundToInt(keyboardInput.x),
                    Mathf.RoundToInt(keyboardInput.y)
                );
            }
        }


        private void HandleFacing()
        {
            if (input.x > 0) transform.localScale = facingRight;
            else if (input.x < 0) transform.localScale = facingLeft;
        }


        private void HandleAnimation()
        {
            bool isMoving = input.x != 0 || input.y != 0;

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


        #region Ball

        public void InstantiateBall()
        {
            if (ballObj == null) return;

            ballRef = Instantiate(ballObj, transform.position, Quaternion.identity);
            moveBall = true;
        }


        private void UpdateBallPosition()
        {
            if (ballRef == null) return;

            // Use analog stick if available
            if (analog != null && analog.GetStickPosition().magnitude > 0.1f)
            {
                ballRef.transform.position = transform.position +
                                             (Vector3)analog.GetStickPosition() * placeRadius;
            }
            // Otherwise use mouse position for keyboard players
            else if (mainCamera != null && Mouse.current != null)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
                mouseWorldPos.z = 0;
                Vector3 direction = (mouseWorldPos - transform.position).normalized;
                float distance = Mathf.Min((mouseWorldPos - transform.position).magnitude, placeRadius);
                ballRef.transform.position = transform.position + direction * distance;
            }
        }


        public void PlaceBall()
        {
            ballRef = null;
            moveBall = false;
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

        private void OnPlaceBall(InputAction.CallbackContext context)
        {
            if (!moveBall)
            {
                InstantiateBall();
            }
            else
            {
                PlaceBall();
            }
        }

        #endregion
    }
}