using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    public enum EnemyType
    {
        Chaser,
        Fighter,
        Sentinel,
        Shooter
    }

    public enum ShootDirection
    {
        AtPlayer,
        Left,
        Right
    }

    /// <summary>
    /// Defines one enemy spawn in a level.
    /// Type maps to an EnemyTypeConfig which holds the prefab and default stats.
    /// Override fields (when > 0) replace the template defaults.
    /// </summary>
    [Serializable]
    public class EnemySpawnConfig
    {
        [Header("Identity")]
        public EnemyType type;

        [Header("Placement")]
        public Vector2 position;
        public float spawnTime;
        public bool activeAtStart = true;

        [Header("Stat Overrides (0 = use template default)")]
        public int healthOverride;
        public float speedOverride;
        public float detectionRangeOverride;

        [Header("Shooter Overrides")]
        public float fireRateOverride;
        public float initialAttackDelay = 5f;
        public ShootDirection shootDirection = ShootDirection.AtPlayer;
    }
}
