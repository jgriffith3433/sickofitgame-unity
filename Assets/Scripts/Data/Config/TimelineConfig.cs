using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    // =========================================================================
    // EVENT TYPES — every scripted thing that can happen in a level/video/death
    // =========================================================================

    public enum GameEventType
    {
        // Audio
        PlaySFX,            // Play a one-shot sound effect
        PlayMusic,          // Start/change background music
        StopMusic,          // Stop background music
        SetMusicVolume,     // Fade music volume

        // Visual
        CameraShake,        // Shake the camera
        CameraZoom,         // Zoom camera in/out
        CameraMove,         // Move camera to position
        ScreenFlash,        // Flash the screen a color
        SetAmbientColor,    // Change ambient/background color

        // Objects
        ActivateObject,     // Show a GameObject by path
        DeactivateObject,   // Hide a GameObject by path
        SpawnPrefab,        // Instantiate a prefab at position

        // Enemies
        SpawnEnemyWave,     // Spawn a group of enemies
        ActivateEnemy,      // Wake up a sleeping enemy

        // Sprite Frames (for background layers, video playback)
        PlaySpriteSequence, // Start a SpriteFramesData playback

        // Flow
        ShowText,           // Display text on screen (dialogue, titles)
        HideText,           // Hide text
        SlowMotion,         // Change time scale
        WaitForEnemiesDead, // Pause timeline until all enemies are dead

        // Player
        SetPlayerInvincible,    // Make player invincible for duration
        DisablePlayerInput,     // Lock player controls
        EnablePlayerInput,      // Unlock player controls

        // Custom
        UnityEvent,         // Fire a UnityEvent by name (for anything not covered)
    }

    // =========================================================================
    // GAME EVENT — one thing that happens at a specific time
    // =========================================================================

    /// <summary>
    /// A single scripted event in the game experience.
    /// "At time 2.5s, play an explosion sound at volume 0.8"
    /// "At time 10s, spawn 3 chasers from the right"
    /// "At time 0s, shake the camera for 0.3s"
    /// </summary>
    [Serializable]
    public class GameEvent
    {
        [Header("Timing")]
        [Tooltip("When this event fires (seconds from scene start)")]
        public float time;
        [Tooltip("Optional: how long this event lasts (for continuous effects)")]
        public float duration;

        [Header("Event")]
        public GameEventType type;
        [Tooltip("Human-readable label for this event")]
        public string label;

        // --- Audio ---
        [Header("Audio")]
        public AudioClip audioClip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Tooltip("Pitch variation for SFX (0 = none)")]
        [Range(0f, 0.3f)]
        public float pitchVariation;
        [Tooltip("World position for spatial audio (ignored if zero)")]
        public Vector2 audioPosition;

        // --- Camera ---
        [Header("Camera")]
        public float shakeIntensity = 0.3f;
        public float zoomTarget = 5f;
        public Vector2 cameraTargetPosition;
        public float cameraSpeed = 2f;

        // --- Visual ---
        [Header("Visual")]
        public Color color = Color.white;
        public float fadeSpeed = 1f;

        // --- Objects ---
        [Header("Objects")]
        [Tooltip("Path to target GameObject in hierarchy (e.g., 'Content/Backgrounds/Layer1')")]
        public string targetPath;
        public GameObject prefab;
        public Vector2 spawnPosition;
        public float spawnRotation;

        // --- Enemies ---
        [Header("Enemies")]
        public EnemyType enemyType;
        public int enemyCount = 1;
        [Tooltip("Positions for each enemy in the wave. If fewer than enemyCount, extras spawn at spawnPosition.")]
        public List<Vector2> enemyPositions = new List<Vector2>();

        // --- Sprite Frames ---
        [Header("Sprite Frames")]
        [Tooltip("SpriteFramesData ScriptableObject for sprite sequence playback")]
        public ScriptableObject spriteFramesData;

        // --- Text ---
        [Header("Text")]
        public string text;
        public float textSize = 24f;

        // --- Time ---
        [Header("Time Scale")]
        [Tooltip("Time scale for SlowMotion event (0.5 = half speed)")]
        public float timeScale = 1f;
    }

    // =========================================================================
    // TIMELINE CONFIG — the experience definition for a scene
    // =========================================================================

    /// <summary>
    /// Defines the scripted experience for a level, video, or death scene.
    /// A flat list of timed events, sorted by time.
    /// 
    /// This replaces hand-made Timeline assets for most use cases.
    /// The ExperienceRunner processes these events at runtime.
    /// 
    /// Example conversation:
    ///   "Add an explosion sound at 2 seconds in Level 1"
    ///   → GameEvent { time=2, type=PlaySFX, audioClip=explosion, volume=0.8 }
    ///   
    ///   "Shake the camera when the boss spawns at 30 seconds"  
    ///   → GameEvent { time=30, type=CameraShake, shakeIntensity=0.5, duration=0.5 }
    ///   
    ///   "Spawn 3 chasers from the right at 15 seconds"
    ///   → GameEvent { time=15, type=SpawnEnemyWave, enemyType=Chaser, enemyCount=3 }
    /// </summary>
    [Serializable]
    public class TimelineConfig
    {
        [Tooltip("Total duration of the experience (0 = no time limit, runs until events exhausted)")]
        public float duration;

        [Tooltip("All scripted events, processed in time order")]
        public List<GameEvent> events = new List<GameEvent>();

        // Legacy support — keep for existing timeline assets that use tracks
        [Header("Legacy Tracks (for existing Timeline assets)")]
        public List<TimelineTrackConfig> tracks = new List<TimelineTrackConfig>();
    }

    // =========================================================================
    // LEGACY TRACK SUPPORT — for existing hand-made Timeline assets
    // =========================================================================

    public enum TimelineTrackType
    {
        Audio,
        Activation,
        SpriteFrame
    }

    [Serializable]
    public class TimelineTrackConfig
    {
        public TimelineTrackType trackType;
        public string trackName;
        public List<TimelineClipConfig> clips = new List<TimelineClipConfig>();
    }

    [Serializable]
    public class TimelineClipConfig
    {
        [Header("Timing")]
        public float startTime;
        public float duration;

        [Header("Audio Track")]
        public AudioClip audioClip;

        [Header("Activation Track")]
        public string targetGameObjectPath;

        [Header("Sprite Frame Track")]
        public ScriptableObject spriteFramesData;
    }
}
