namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Shared interface for anything that can take damage.
    /// Implemented by PlayerHealth and EnemyHealth so that
    /// DamageDealer, InstantDeathTrigger, etc. don't need to know which type they hit.
    /// </summary>
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsAlive { get; }
        void TakeDamage(int damage);
    }
}
