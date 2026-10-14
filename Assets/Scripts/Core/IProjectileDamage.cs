namespace SickOfIt.Core
{
    /// <summary>
    /// A projectile whose damage can be scaled from its prefab value, so upgrades can strengthen the player's
    /// shots without Core depending on the combat code (implemented by DamageDealer).
    /// </summary>
    public interface IProjectileDamage
    {
        void SetDamageMultiplier(float multiplier);
    }
}
