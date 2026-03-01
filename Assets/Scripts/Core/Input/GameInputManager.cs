using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Lightweight jump-event hub that lives on the MobileController UI prefab.
    /// 
    /// GlobalTapJump broadcasts jump requests through this component;
    /// TopDownController subscribes to receive them.
    /// 
    /// Analog sticks and buttons are wired directly to their consumers
    /// (TopDownController, RewindController) at generation time — this
    /// component no longer holds those references.
    /// </summary>
    public class GameInputManager : MonoBehaviour
    {
        /// <summary>Raised when any jump input source detects a jump request.</summary>
        public event System.Action OnJumpPressed;

        /// <summary>
        /// Call this from any input detector (GlobalTapJump, UI buttons, etc.)
        /// to broadcast a jump request to all subscribers.
        /// </summary>
        public void RequestJump()
        {
            OnJumpPressed?.Invoke();
        }
    }
}
