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
        public Vector2 position;
        public Vector2 scale = Vector2.one;
    }
}
