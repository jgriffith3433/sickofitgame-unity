using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Where character mp4s live, and how they are packed into sprite sheets.
    /// Sick Of It → Regenerate Player/Enemy Sprites reads this.
    /// </summary>
    [Serializable]
    public class CharacterArtConfig
    {
        public string playerSourceRoot = @"C:\Projects\SickOfItGamePlayer";
        public string enemySourceRoot = @"C:\Projects\SickOfItGameEnemies";

        [Tooltip("Largest sprite sheet edge, in pixels.")]
        public int maxSheetSize = 4096;

        [Tooltip("Pixels per world unit. Higher makes the sprite smaller before spriteScale is applied.")]
        public float pixelsPerUnit = 100f;

        [Tooltip("Edge pixels at or below this brightness are treated as the black plate and cleared.")]
        [Range(0, 80)]
        public int clearCutoff = 28;
    }
}
