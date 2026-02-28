using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Defines a death scene.
    /// Death scenes play a short animation/sequence then offer retry.
    /// </summary>
    [Serializable]
    public class DeathConfig
    {
        [Header("Identity")]
        public string deathName;
        public string displayName;

        [Header("Content")]
        [Tooltip("SpriteFramesData ScriptableObject for this death scene")]
        public ScriptableObject spriteFramesData;
        public AudioClip audio;

        [Header("Timeline")]
        public TimelineConfig timeline;
    }
}
