using System;
using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Configuration for a single background layer in a level.
    /// Levels can have multiple stacked background layers (parallax-ready).
    /// 
    /// Each layer can be:
    /// 1. A SpriteFramesData (animated sprite sheet sequence for scrolling/animated BGs)
    /// 2. A static Sprite (simple image)
    /// 3. A solid Color (flat fill)
    /// 
    /// Each layer picks a SpriteLayer band. Order inside that band is sortingOrder.
    /// </summary>
    /// <summary>What a sequenced background layer's start and duration are measured in.</summary>
    public enum BackgroundSequenceKey
    {
        /// <summary>Seconds on the game clock.</summary>
        Time,
        /// <summary>World units travelled on a Travel stage.</summary>
        Distance,
        /// <summary>Floors climbed since the stage's first floor.</summary>
        Floors,
    }

    [Serializable]
    public class BackgroundLayerConfig
    {
        [Header("Identity")]
        [Tooltip("Name for this layer (for debugging/editing)")]
        public string name = "Background";

        [Header("Content (pick one)")]
        [Tooltip("Animated sprite frame sequence (for video-style or scrolling backgrounds)")]
        public ScriptableObject spriteFramesData;
        [Tooltip("Static sprite (for simple image backgrounds)")]
        public Sprite staticSprite;
        [Tooltip("Solid color fill (used when no sprite or frames assigned)")]
        public Color solidColor = Color.clear;

        [Header("Transform")]
        [Tooltip("Position offset from camera center")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("Scale multiplier")]
        public Vector2 scale = Vector2.one;
        [Tooltip("Draw band. Background1 is furthest back. Foreground bands cover the player.")]
        public SpriteLayer layer = SpriteLayer.Background1;
        [Tooltip("Order inside the chosen band. Higher draws in front of other sprites on that same band.")]
        public int sortingOrder;

        [Header("Parallax")]
        [Tooltip("Parallax multiplier (0 = static background, 1 = moves with camera)")]
        public float parallaxFactor = 0f;
        [Tooltip("If true, tile this layer horizontally for infinite scroll")]
        public bool tileHorizontally = false;

        [Header("Sequence")]
        [Tooltip("When this layer appears, in seconds on the game clock (the same clock as the music). Negative means it plays from the start, stacked with the other layers.")]
        public float sequenceStart = -1f;
        [Tooltip("How long this layer stays up. The clip loops or holds inside that window. 0 means until the next sequenced layer, or for the rest of the level if it is the last one.")]
        public float sequenceDuration;
        [Tooltip("Repeat the clip until the window ends. Off plays it once and holds the last frame.")]
        public bool sequenceLoop = true;
        [Tooltip("What sequenceStart and sequenceDuration measure: Time (seconds), Distance (world units travelled on a Travel stage) or Floors (floors climbed since the stage's first floor). The clip itself still plays on the game clock.")]
        public BackgroundSequenceKey sequenceBy = BackgroundSequenceKey.Time;

        [Header("Clip window")]
        [Tooltip("Play only part of the clip: start here (seconds into the clip). Lets one long video supply a backdrop, e.g. just its lava-river shot.")]
        public float clipStart;
        [Tooltip("Length of that part in seconds (0 = the whole clip). It loops inside this window when the layer loops.")]
        public float clipLength;

        /// <summary>True when this layer is a timed segment instead of a layer that plays for the whole level.</summary>
        public bool IsSequenced => sequenceStart >= 0f;

        /// <summary>
        /// Returns true if this layer has any renderable content.
        /// </summary>
        public bool HasContent => spriteFramesData != null || staticSprite != null || solidColor.a > 0;

        /// <summary>
        /// Returns true if this is an animated (SpriteFramesData) background.
        /// </summary>
        public bool IsAnimated => spriteFramesData != null;
    }
}
