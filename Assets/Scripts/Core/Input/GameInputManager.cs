using UnityEngine;
using MobileControllerFramework;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Centralized input event bus AND mobile-control hub.
    /// Lives on the MobileController UI prefab so the Analog references
    /// can be wired in the Inspector once.
    /// 
    /// Consumers (TopDownController, etc.) find this at runtime with
    /// FindAnyObjectByType and subscribe / poll as needed.
    /// </summary>
    public class GameInputManager : MonoBehaviour
    {
        // ── Mobile Controls ──────────────────────────────────────────
        [Header("Mobile Controls")]
        [Tooltip("Analog stick for movement (left stick on MobileController).")]
        [SerializeField] private Analog moveAnalog;
        [Tooltip("Analog stick for aiming/firing (right stick on MobileController).")]
        [SerializeField] private Analog fireAnalog;

        // ── Events ───────────────────────────────────────────────────
        /// <summary>Raised when any jump input source detects a jump request.</summary>
        public event System.Action OnJumpPressed;
        /// <summary>Raised when the move analog stick is released.</summary>
        public event System.Action OnMoveAnalogReleased;

        // ── Public Accessors ─────────────────────────────────────────
        public Analog MoveAnalog => moveAnalog;
        public Analog FireAnalog => fireAnalog;

        private void OnEnable()
        {
            if (moveAnalog != null)
                moveAnalog.StickExitEvent.AddListener(HandleMoveAnalogReleased);
        }

        private void OnDisable()
        {
            if (moveAnalog != null)
                moveAnalog.StickExitEvent.RemoveListener(HandleMoveAnalogReleased);
        }

        // ── Input Polling ────────────────────────────────────────────

        /// <summary>
        /// Returns the current move-stick position, or Vector2.zero if
        /// no move analog is assigned or it is inactive.
        /// </summary>
        public Vector2 GetMoveInput()
        {
            if (moveAnalog != null && moveAnalog.gameObject.activeSelf)
                return moveAnalog.GetStickPosition();
            return Vector2.zero;
        }

        /// <summary>
        /// Returns the current fire-stick position, or Vector2.zero if
        /// no fire analog is assigned or it is inactive.
        /// </summary>
        public Vector2 GetFireInput()
        {
            if (fireAnalog != null && fireAnalog.gameObject.activeSelf)
                return fireAnalog.GetStickPosition();
            return Vector2.zero;
        }

        /// <summary>True when a move analog is present and active.</summary>
        public bool HasMoveAnalog =>
            moveAnalog != null && moveAnalog.gameObject.activeSelf;

        /// <summary>True when a fire analog is present and active.</summary>
        public bool HasFireAnalog =>
            fireAnalog != null && fireAnalog.gameObject.activeSelf;

        // ── Broadcast Helpers ────────────────────────────────────────

        /// <summary>
        /// Call this from any input detector (GlobalTapJump, UI buttons, etc.)
        /// to broadcast a jump request to all subscribers.
        /// </summary>
        public void RequestJump()
        {
            OnJumpPressed?.Invoke();
        }

        private void HandleMoveAnalogReleased()
        {
            OnMoveAnalogReleased?.Invoke();
        }
    }
}
