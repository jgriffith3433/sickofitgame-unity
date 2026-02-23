using UnityEngine;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Relays mid-clip animation events from the Animator GameObject to the parent EnemyFighter.
    /// Attach this to the same GameObject that has the Animator component.
    ///
    /// Animation events to add:
    ///   Attack clip - hit frame : OnAttackFrame
    /// </summary>
    public class AnimationEventRelayFighter : MonoBehaviour
    {
        private EnemyFighter _fighter;

        private void Awake()
        {
            _fighter = GetComponentInParent<EnemyFighter>();

            if (_fighter == null)
                Debug.LogError($"AnimationEventRelayFighter on '{gameObject.name}' could not find EnemyFighter in parent hierarchy.");
        }

        public void OnAttackFrame() => _fighter?.OnAttackFrame();
    }
}