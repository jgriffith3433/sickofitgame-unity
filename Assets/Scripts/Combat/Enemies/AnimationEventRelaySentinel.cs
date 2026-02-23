using UnityEngine;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Relays mid-clip animation events from the Animator GameObject to the parent EnemySentinel.
    /// Attach this to the same GameObject that has the Animator component.
    ///
    /// Animation events to add:
    ///   StandingAttack clip  – damage frame : OnStandingAttackFrame
    ///   CrouchingAttack clip – fire frame   : OnCrouchAttackFrame
    ///
    /// State completion (end-of-clip) is handled automatically by AnimationStateNotifier
    /// (a StateMachineBehaviour) — no last-frame events needed.
    /// </summary>
    public class AnimationEventRelaySentinel : MonoBehaviour
    {
        private EnemySentinel _sentinel;

        private void Awake()
        {
            _sentinel = GetComponentInParent<EnemySentinel>();

            if (_sentinel == null)
                Debug.LogError($"AnimationEventRelaySentinel on '{gameObject.name}' could not find EnemySentinel in parent hierarchy.");
        }

        public void OnStandingAttackFrame() => _sentinel?.OnStandingAttackFrame();
        public void OnCrouchAttackFrame()   => _sentinel?.OnCrouchAttackFrame();
    }
}
