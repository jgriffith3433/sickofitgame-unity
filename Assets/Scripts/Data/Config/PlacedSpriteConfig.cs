using System;
using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// One sprite placed in a level or the main menu.
    /// Set the sorting layer and this draws on that band. Z is filled in from the layer
    /// so the Scene view shows the stack.
    /// </summary>
    [Serializable]
    public class PlacedSpriteConfig
    {
        public string name = "Sprite";

        [Tooltip("Static image. Leave empty when using a frame sequence.")]
        public Sprite sprite;

        [Tooltip("Animated sequence (SpriteFramesData). Used instead of the static sprite when set.")]
        public ScriptableObject spriteFramesData;

        public SpriteLayer layer = SpriteLayer.Background2;
        [Tooltip("Order inside the chosen band.")]
        public int sortingOrder;

        public Vector2 position;
        public Vector2 scale = Vector2.one;
        public bool flipX;

        [Tooltip("Play the frame sequence from GameClock. Menu art leaves this off and uses real time.")]
        public bool useGameClock = true;

        [Tooltip("Turn off for a transition that should stay hidden until something plays it.")]
        public bool startActive = true;

        [Tooltip("Levels always spawn these. The menu spawns one only when no object with this name is already in the scene.")]
        public bool spawnIfMissing = true;
    }
}
