using UnityEngine;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Relays animation events from a child object (with Animator) to the parent EnemyFighter.
    /// Attach this to the same GameObject that has the Animator component.
    /// </summary>
    public class AnimationEventRelayFighter : MonoBehaviour
    {
        private EnemyFighter _fighter;

        private void Awake()
        {
            _fighter = GetComponentInParent<EnemyFighter>();
            
            if (_fighter == null)
            {
                Debug.LogError($"AnimationEventRelayFighter on '{gameObject.name}' could not find EnemyFighter in parent hierarchy.");
            }
        }

        /// <summary>
        /// Called by animation event on the attack animation frame where damage should be dealt.
        /// Add this as an Animation Event on the frame where the attack should hit.
        /// </summary>
        public void OnAttackFrame()
        {
            if (_fighter != null)
            {
                _fighter.OnAttackFrame();
            }
        }
    }
}
