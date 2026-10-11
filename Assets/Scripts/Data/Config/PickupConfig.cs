using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Configuration for point drop pickups (Diamond).
    /// PickupPrefabGenerator reads this to create the pickup prefab.
    /// EnemyBase reads these values at runtime for drop behavior.
    /// </summary>
    [Serializable]
    public class PickupConfig
    {
        [Header("Identity")]
        public string displayName = "Diamond";

        [Header("Visuals")]
        public Sprite sprite;
        public float spriteScale = 0.125f;
        public int sortingOrder = 5;

        [Header("Physics")]
        public float colliderRadius = 0.15f;
        public float popForce = 3f;
        public float gravity = 5f;
        public float spreadRange = 0.5f;
        [Tooltip("Time in seconds before the pickup is returned to pool")]
        public float lifetime = 10f;

        [Header("Bob Animation")]
        public float bobAmplitude = 0.1f;
        public float bobSpeed = 3f;

        [Header("Pool")]
        public int poolSize = 20;

        [Header("Layer / Tag")]
        public string layerName = "Pickup";
        public string tag = "Pickup";

        [Header("Audio - SFX")]
        [Tooltip("Sound played when the pickup is collected")]
        public AudioClip collectSound;
        [Tooltip("Sound played when the pickup spawns/pops out")]
        public AudioClip spawnSound;

        [Header("Animation (for generator)")]
        [Tooltip("Sprite sheet for pickup animation (e.g., diamond spin). If null, uses static sprite.")]
        public Texture2D spriteSheet;
        [Tooltip("Animation frames per second")]
        public int animationFps = 12;
        [Tooltip("Full animation config. If null but spriteSheet is set, creates a simple looping idle clip.")]
        public AnimationConfig animationConfig;
    }
}
