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
        [Tooltip("Picture on the level select card.")]
        public Sprite thumbnail;

        [Header("Visuals")]
        [Tooltip("Background layers — SpriteFramesData ScriptableObjects (rendered in order, first = back). Legacy field; prefer backgroundLayers.")]
        public List<ScriptableObject> backgrounds = new List<ScriptableObject>();

        [Tooltip("Strongly-typed background layers with parallax, position, and static sprite support. If populated, takes priority over 'backgrounds' list.")]
        public List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();

        public Color ambientColor = Color.white;

        [Header("Audio")]
        public AudioClip music;
        [Tooltip("Start playback at this time in the audio clip")]
        public float musicStartTime;
        [Range(0f, 1f)]
        public float musicVolume = 1f;

        [Tooltip("The adaptive score: explore / fight / finale loops at one tempo (see ScoreConfig). Empty keeps the single track above.")]
        public ScoreConfig score = new ScoreConfig();

        [Header("Audio - Ambient SFX")]
        [Tooltip("Ambient/environmental sounds that loop during the level")]
        public AudioClip ambientSound;
        [Range(0f, 1f)]
        public float ambientVolume = 0.5f;
        [Tooltip("Positional one-shots on props (lava pop, chain rattle, ...): random gaps, pan by screen x.")]
        public List<AmbientEmitterConfig> ambientEmitters = new List<AmbientEmitterConfig>();
        [Tooltip("Sound played when the level is completed")]
        public AudioClip levelCompleteSound;

        [Header("Layout")]
        public List<PlatformConfig> platforms = new List<PlatformConfig>();

        [Tooltip("Props in the level: houses, trees, doors. Pick a sorting layer and they stack with the player.")]
        public List<PlacedSpriteConfig> placedSprites = new List<PlacedSpriteConfig>();

        [Header("Barriers")]
        [Tooltip("Generate invisible boundary colliders around the camera viewport")]
        public bool generateBarriers = true;
        [Tooltip("Thickness of each barrier collider (in world units)")]
        public float barrierThickness = 1f;
        [Tooltip("If true, also add a floor barrier at the bottom of the viewport")]
        public bool barrierOnBottom = true;
        [Tooltip("If true, also add a ceiling barrier at the top of the viewport")]
        public bool barrierOnTop = true;

        [Header("Enemies")]
        public List<EnemySpawnConfig> enemies = new List<EnemySpawnConfig>();

        [Header("Player")]
        [Tooltip("Where the player spawns in this level")]
        public Vector2 playerSpawnPosition = new Vector2(-4f, 0f);

        [Header("Win Zone")]
        [Tooltip("Center of the win zone in world space")]
        public Vector2 winZoneCenter = new Vector2(6.5f, 0f);
        [Tooltip("Half-size of the win zone (box check)")]
        public Vector2 winZoneHalfSize = new Vector2(0.5f, 4f);

        [Header("Flow")]
        public string deathConfigName = "Death1";
        public float duration = 60f;

        [Header("Completion")]
        [Tooltip("Delay in seconds after level completion before transitioning")]
        public float completionDelay = 2f;
        [Tooltip("Video number to play after completing this level (0 = go to level select)")]
        public int nextVideoNumber;
        [Tooltip("Level (position in game.json's levels) to play next, straight after the Shaft, when there is no next video " +
                 "(0 = go to level select). Story stages in a row: The Spill leads on to Imp Run.")]
        public int nextLevelNumber;
        [Tooltip("If true, completing this level returns to main menu instead of continuing")]
        public bool returnToMainMenuOnComplete;

        [Header("Timeline")]
        public TimelineConfig timeline = new TimelineConfig();

        [Header("Gameplay Script")]
        [SickOfIt.Json.RawJson]
        [Tooltip("Phases, rules, spawns, and the objective (see Docs/GameplayReference.md). Parsed at runtime. " +
                 "When set, it replaces the 'enemies' list and the built-in win rules; the timeline still runs.")]
        public string script;

        public bool HasScript => !string.IsNullOrEmpty(script);

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
