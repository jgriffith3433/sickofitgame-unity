using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>What the hero's equipped weapon asks of PlayerController, read from content/weapons by the gameplay world.</summary>
    public struct WeaponStats
    {
        public string id;
        public string displayName;
        /// <summary>A melee swing instead of a projectile volley.</summary>
        public bool melee;
        /// <summary>Seconds between shots (projectile weapons); player.json fireRate for the launcher.</summary>
        public float cooldown;
        public float projectileSpeed;
        public float projectileLifetime;
        public bool projectileFaceAim;
        public float projectileSpriteAngle;
        /// <summary>Multiplies the damage of each shot.</summary>
        public float damageScale;
        /// <summary>Upgrades' extra shots (spread Grudge) apply to this weapon.</summary>
        public bool multiShot;
        /// <summary>Sound id (audio.json) for a shot; empty = the playerShoot event.</summary>
        public string useSound;
        /// <summary>The aim guide shows for this weapon.</summary>
        public bool aimGuide;
        /// <summary>Length of the aim guide as a multiple of player.json aimGuide.length.</summary>
        public float aimGuideLength;
    }

    /// <summary>
    /// The hero's weapons as PlayerController sees them: which one is equipped, whether it can be used now, and what
    /// using it does. Implemented by the gameplay world's PlayerWeapons (state saved for rewind); without one the
    /// controller fires its player.json gun.
    /// </summary>
    public interface IPlayerArmory
    {
        /// <summary>The equipped weapon, or false when the hero has none (the controller's own gun).</summary>
        bool TryGetCurrent(out WeaponStats stats);
        /// <summary>True when the equipped weapon can be used at 'now': not switching, not mid-swing, ammo left.</summary>
        bool CanUse(float now);
        /// <summary>The equipped weapon is out of ammo.</summary>
        bool Empty { get; }
        /// <summary>He pulled the trigger on an empty weapon (a dry click, rate-limited).</summary>
        void Dry(float now);
        /// <summary>A projectile volley left the gun at 'now': spend the ammo.</summary>
        void Fired(float now);
        /// <summary>
        /// Starts a melee swing from 'origin' along 'direction'. False when it can't start. 'attackSpeed' divides the
        /// swing's times and 'damage' multiplies its damage.
        /// </summary>
        bool Swing(float now, Vector2 origin, Vector2 direction, float attackSpeed, float damage);
        /// <summary>Equips the next (+1) or previous (-1) weapon he holds. True if the weapon changed.</summary>
        bool Cycle(int step, float now);
    }
}
