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

    /// <summary>
    /// Something that cares which side a hit comes from (a front shield). DamageDealer uses it when the target has it,
    /// passing where the shot is.
    /// </summary>
    public interface IDirectionalDamageable : IDamageable
    {
        void TakeDamageFrom(int damage, UnityEngine.Vector2 point);
    }
}
