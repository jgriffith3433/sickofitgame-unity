using UnityEngine;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Unified animation event relay. Attach to the child GameObject with the Animator.
    /// Relays animation events to the parent EnemyBase subclass.
    /// 
    /// Works for ALL enemy types:
    ///   Fighter:  OnAttackFrame -> EnemyFighter.OnAttackFrame()
    ///   Sentinel: OnStandingAttackFrame -> EnemySentinel.OnStandingAttackFrame()
    ///             OnCrouchAttackFrame -> EnemySentinel.OnCrouchAttackFrame()
    ///   Shooter:  FireProjectile -> EnemyShooter.FireProjectile()
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private EnemyFighter _fighter;
        [SerializeField] private EnemySentinel _sentinel;
        [SerializeField] private EnemyShooter _shooter;

        private void Awake()
        {
            if (_fighter == null) _fighter = GetComponentInParent<EnemyFighter>();
            if (_sentinel == null) _sentinel = GetComponentInParent<EnemySentinel>();
            if (_shooter == null) _shooter = GetComponentInParent<EnemyShooter>();
        }

        // Fighter events
        public void OnAttackFrame() => _fighter?.OnAttackFrame();

        // Sentinel events
        public void OnStandingAttackFrame() => _sentinel?.OnStandingAttackFrame();
        public void OnCrouchAttackFrame() => _sentinel?.OnCrouchAttackFrame();

        // Shooter events
        public void FireProjectile() => _shooter?.FireProjectile();
    }
}
