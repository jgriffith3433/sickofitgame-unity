using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Complete definition of a game level.
    /// Everything needed to generate and run a level lives here.
    /// LevelRuntime reads this at load time and spawns all content dynamically.
    /// </summary>
    [Serializable]
    public class LevelConfig
    {
        [Header("Identity")]
        public string levelName;
        public string displayName;
        public int levelNumber;

        [Header("Visuals")]
        [Tooltip("Background layers — SpriteFramesData ScriptableObjects (rendered in order, first = back)")]
        public List<ScriptableObject> backgrounds = new List<ScriptableObject>();
        public Color ambientColor = Color.white;

        [Header("Audio")]
        public AudioClip music;
        [Tooltip("Start playback at this time in the audio clip")]
        public float musicStartTime;
        [Range(0f, 1f)]
        public float musicVolume = 1f;

        [Header("Layout")]
        public Vector2 levelBounds = new Vector2(10f, 6f);
        public List<PlatformConfig> platforms = new List<PlatformConfig>();

        [Header("Enemies")]
        public List<EnemySpawnConfig> enemies = new List<EnemySpawnConfig>();

        [Header("Flow")]
        public string deathConfigName = "Death1";
        public float duration = 60f;
        public bool hasTwoStageSequence;
        public float firstSequenceDuration;

        [Header("Completion")]
        [Tooltip("Video number to play after completing this level (0 = go to level select)")]
        public int nextVideoNumber;

        [Header("Timeline")]
        public TimelineConfig timeline = new TimelineConfig();

        // --- Convenience ---

        public int EnemyCount => enemies?.Count ?? 0;

        public int EnemyCountOfType(EnemyType type)
        {
            int count = 0;
            if (enemies == null) return 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].type == type) count++;
            }
            return count;
        }
    }
}
