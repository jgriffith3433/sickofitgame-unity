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

        [Tooltip("Pictures drawn over the scene and animated by keyframes, sampled from the clock every frame (rewind plays them backward)")]
        public List<TimelineOverlay> overlays = new List<TimelineOverlay>();

        /// <summary>True when there is anything to run: events or overlays.</summary>
        public bool HasContent => (events != null && events.Count > 0) || (overlays != null && overlays.Count > 0);

        // Legacy support — keep for existing timeline assets that use tracks
        [Header("Legacy Tracks (for existing Timeline assets)")]
        public List<TimelineTrackConfig> tracks = new List<TimelineTrackConfig>();
    }

    // =========================================================================
    // OVERLAYS — keyframed pictures on top of a video, death or level
    // =========================================================================

    /// <summary>How an overlay moves from one keyframe to the next.</summary>
    public enum OverlayEase
    {
        Linear,     // constant speed
        In,         // starts slow, ends fast (being sucked in)
        Out,        // starts fast, ends slow (settling)
        InOut,      // slow at both ends
        Hold,       // stays on this keyframe until the next one, then jumps
    }

    /// <summary>
    /// A picture drawn over the scene and animated by keyframes: "the hero tumbles down the fire tunnel,
    /// spinning and shrinking". Its pose is sampled from the clock time every frame (never tweened), so a
    /// rewind plays it backward and a seek lands on the right pose. Hidden before its first keyframe and
    /// after its last one.
    /// </summary>
    [Serializable]
    public class TimelineOverlay
    {
        [Tooltip("Name, for logs and the playtest (e.g. \"heroFall\")")]
        public string name;

        [Tooltip("A sliced sprite sheet: its sprites play in name order at fps (e.g. the hero's Fall sheet)")]
        public Texture2D spriteSheet;
        [Tooltip("Optional: only the sheet's sprites whose name contains this")]
        public string spriteNameFilter;
        [Tooltip("Or one still picture (used when spriteSheet is null)")]
        public Sprite sprite;
        [Tooltip("Frames per second of the sheet, counted from the first keyframe")]
        public float fps = 12f;
        [Tooltip("Loop the frames; off holds the last one")]
        public bool loop = true;

        [Tooltip("Draw band (sorting layer); the video itself is Background1")]
        public SpriteLayer sortLayer = SpriteLayer.Player;
        [Tooltip("Order inside the band")]
        public int sortOrder;
        [Tooltip("Tint multiplied with the picture; keyframe alpha multiplies its alpha")]
        public Color tint = Color.white;

        [Tooltip("The poses, in time order. Seconds on the scene's clock")]
        public List<OverlayKeyframe> keyframes = new List<OverlayKeyframe>();

        public float StartTime => keyframes != null && keyframes.Count > 0 ? keyframes[0].time : 0f;
        public float EndTime => keyframes != null && keyframes.Count > 0 ? keyframes[keyframes.Count - 1].time : 0f;

        /// <summary>
        /// The pose at clock time t, interpolated between the keyframes around it. False outside
        /// [first keyframe, last keyframe]: the overlay is hidden there. Keyframes must be in time order.
        /// </summary>
        public bool Sample(float t, out OverlayKeyframe pose)
        {
            pose = null;
            if (keyframes == null || keyframes.Count == 0 || t < StartTime || t > EndTime)
                return false;

            int next = 1;
            while (next < keyframes.Count && keyframes[next].time < t)
                next++;
            if (next >= keyframes.Count)
            {
                pose = keyframes[keyframes.Count - 1];
                return true;
            }

            var a = keyframes[next - 1];
            var b = keyframes[next];
            float span = b.time - a.time;
            float u = span > 0f ? Mathf.Clamp01((t - a.time) / span) : 1f;
            u = EaseProgress(a.ease, u);
            pose = new OverlayKeyframe
            {
                time = t,
                position = Vector2.LerpUnclamped(a.position, b.position, u),
                rotation = Mathf.LerpUnclamped(a.rotation, b.rotation, u),
                scale = Mathf.LerpUnclamped(a.scale, b.scale, u),
                alpha = Mathf.Lerp(a.alpha, b.alpha, u),
                ease = a.ease,
            };
            return true;
        }

        /// <summary>The sheet frame at clock time t, counted from the first keyframe at fps.</summary>
        public int FrameAt(float t, int frameCount)
        {
            if (frameCount <= 1)
                return 0;
            int frame = Mathf.FloorToInt(Mathf.Max(0f, t - StartTime) * Mathf.Max(0f, fps));
            return loop ? frame % frameCount : Mathf.Min(frame, frameCount - 1);
        }

        public static float EaseProgress(OverlayEase ease, float u)
        {
            u = Mathf.Clamp01(u);
            switch (ease)
            {
                case OverlayEase.In: return u * u;
                case OverlayEase.Out: return 1f - (1f - u) * (1f - u);
                case OverlayEase.InOut: return u * u * (3f - 2f * u);
                case OverlayEase.Hold: return u >= 1f ? 1f : 0f;
                default: return u;
            }
        }

        /// <summary>Problems with this overlay, for GameDefinition.Validate.</summary>
        public IEnumerable<string> Problems()
        {
            string label = string.IsNullOrEmpty(name) ? "an unnamed overlay" : $"overlay \"{name}\"";
            if (spriteSheet == null && sprite == null)
                yield return $"{label} has no spriteSheet or sprite";
            if (spriteSheet != null && fps <= 0f)
                yield return $"{label}: fps must be above 0";
            if (keyframes == null || keyframes.Count == 0)
            {
                yield return $"{label} has no keyframes";
                yield break;
            }
            for (int i = 0; i < keyframes.Count; i++)
            {
                var k = keyframes[i];
                if (k == null)
                {
                    yield return $"{label}: keyframe {i + 1} is empty";
                    continue;
                }
                if (i > 0 && keyframes[i - 1] != null && k.time < keyframes[i - 1].time)
                    yield return $"{label}: keyframe {i + 1} (time {k.time}) is before keyframe {i} (time {keyframes[i - 1].time}); keep them in time order";
                if (k.scale < 0f)
                    yield return $"{label}: keyframe {i + 1} has a negative scale";
                if (k.alpha < 0f || k.alpha > 1f)
                    yield return $"{label}: keyframe {i + 1} alpha {k.alpha} is outside 0-1";
            }
        }
    }

    /// <summary>One pose of an overlay at a moment of the scene.</summary>
    [Serializable]
    public class OverlayKeyframe
    {
        [Tooltip("Seconds from the scene's start")]
        public float time;
        [Tooltip("World position; the camera looks at (0, 0) and a video frame is 10.24 units wide")]
        public Vector2 position;
        [Tooltip("Degrees, counter-clockwise; not wrapped, so 1080 from 0 is three full turns (negative turns clockwise)")]
        public float rotation;
        [Tooltip("Size multiplier (1 = the sprite's own size)")]
        public float scale = 1f;
        [Range(0f, 1f)]
        [Tooltip("0 = invisible, 1 = solid")]
        public float alpha = 1f;
        [Tooltip("How the move from this keyframe to the next one speeds up and slows down")]
        public OverlayEase ease = OverlayEase.Linear;
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
