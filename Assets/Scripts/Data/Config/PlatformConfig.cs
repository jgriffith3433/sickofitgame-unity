using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    public enum PlatformType
    {
        Standard,
        Old,
        DropThrough
    }

    /// <summary>
    /// Defines one platform in a level.
    /// </summary>
    [Serializable]
    public class PlatformConfig
    {
        public PlatformType type = PlatformType.Standard;

        [Tooltip("Prefab to instantiate. If null, a basic platform is created.")]
        public GameObject prefab;

        public Vector2 position;
        public Vector2 scale = Vector2.one;

        [Tooltip("Width of auto-generated platform (only used when prefab is null)")]
        public float width = 3f;

        [Tooltip("If true, player can drop through by pressing down")]
        public bool allowDropThrough = true;
    }
}
