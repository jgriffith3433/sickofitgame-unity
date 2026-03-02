namespace SickOfIt.Core
{
    /// <summary>
    /// Interface for components that need to know their owner pool.
    /// Replaces the fragile SendMessage("SetOwnerPool") pattern.
    /// 
    /// Implemented by: DamageDealer, PointsPickup, ProjectileLifetime
    /// Called by: ObjectPool.CreateInstance() during pool prewarm
    /// </summary>
    public interface IPoolReturnable
    {
        void SetOwnerPool(ObjectPool pool);
    }
}
