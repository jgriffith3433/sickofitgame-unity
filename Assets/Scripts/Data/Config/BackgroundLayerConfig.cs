using System;
using UnityEngine;

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
    /// Layers are rendered back-to-front (index 0 = furthest back).
    /// </summary>
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
        [Tooltip("Sorting order (higher = closer to camera)")]
        public int sortingOrder = -100;

        [Header("Parallax")]
        [Tooltip("Parallax multiplier (0 = static background, 1 = moves with camera)")]
        public float parallaxFactor = 0f;
        [Tooltip("If true, tile this layer horizontally for infinite scroll")]
        public bool tileHorizontally = false;

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
