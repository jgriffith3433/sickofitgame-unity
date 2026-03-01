using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Defines a video/cutscene in the game.
    /// Video scenes play sprite-frame sequences with audio.
    /// </summary>
    [Serializable]
    public class VideoConfig
    {
        [Header("Identity")]
        public string videoName;
        public int videoNumber;
        public string displayName;

        [Header("Content")]
        [Tooltip("SpriteFramesData ScriptableObject for this video")]
        public ScriptableObject spriteFramesData;
        public AudioClip audio;

        [Header("Flow")]
        [Tooltip("Which level this video plays after (0 = intro/standalone)")]
        public int afterLevel;
        [Tooltip("Which level to load after video finishes")]
        public int nextLevel;
        [Tooltip("If true, returns to main menu instead of loading next level")]
        public bool returnToMainMenu;

        [Header("Timeline")]
        public TimelineConfig timeline;
    }
}
