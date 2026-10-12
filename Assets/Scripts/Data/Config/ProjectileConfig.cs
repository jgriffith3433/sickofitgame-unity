using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Configuration for a projectile type.
    /// ProjectilePrefabGenerator reads these to create projectile prefabs.
    /// </summary>
    [Serializable]
    public class ProjectileConfig
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this projectile type (e.g., 'Player', 'Enemy', 'Laser')")]
        public string id;
        [Tooltip("Display name for debugging")]
        public string displayName;

        [Header("Visual")]
        [Tooltip("Sprite to use for the projectile")]
        public Sprite sprite;
        [Tooltip("Scale multiplier for the sprite")]
        public float scale = 0.15f;
        [Tooltip("Color tint for the sprite")]
        public Color color = Color.white;
        [Tooltip("SpriteLayer name. Empty uses Player.")]
        public string sortingLayer = "Player";
        [Tooltip("Order inside that layer. 0 uses the projectile order on the Player layer.")]
        public int sortingOrder;

        [Header("Physics")]
        [Tooltip("Collider radius")]
        public float colliderRadius = 1f;
        [Tooltip("Is the collider a trigger?")]
        public bool isTrigger = true;

        [Header("Combat")]
        [Tooltip("Damage dealt on hit")]
        public int damage = 1;
        [Tooltip("Speed at which the projectile travels")]
        public float speed = 10f;
        [Tooltip("How long the projectile lives before returning to pool")]
        public float lifetime = 5f;

        [Header("Layer")]
        [Tooltip("Layer name for collision filtering")]
        public string layerName = "Default";

        [Header("Tags")]
        [Tooltip("Tag to assign to the projectile")]
        public string tag = "Untagged";

        [Header("Pool")]
        [Tooltip("Number of instances to pre-warm in the object pool")]
        public int poolSize = 10;

        [Header("Audio - SFX")]
        [Tooltip("Sound played when this projectile is fired")]
        public AudioClip fireSound;
        [Tooltip("Sound played when this projectile hits a target")]
        public AudioClip hitSound;
        [Tooltip("Sound played when this projectile expires/returns to pool")]
        public AudioClip expireSound;

        [Header("Animation (for generator)")]
        [Tooltip("Sprite sheet for projectile animation (e.g., fireball spin). If null, uses static sprite.")]
        public Texture2D spriteSheet;
        [Tooltip("Animation frames per second")]
        public int animationFps = 12;
        [Tooltip("Full animation config. If null but spriteSheet is set, creates a simple looping idle clip.")]
        public AnimationConfig animationConfig;
    }
}
