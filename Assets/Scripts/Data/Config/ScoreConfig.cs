using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// A floor's adaptive score (Docs/Story.md "Music"): three loops of one shared tempo and key that play in step and
    /// cross-fade on the bar by the floor's intensity. Empty = the floor plays its single track (LevelConfig.music).
    /// Written by Tools/Audio/adaptive_score.py from music_manifest.json (edit the table, not the levels).
    /// </summary>
    [Serializable]
    public class ScoreConfig
    {
        [Tooltip("Sparse, the floor's motif. Plays alone when nothing threatens the hero (also the level's music source).")]
        public AudioClip explore;
        [Tooltip("Full drums, distorted guitar, choir stabs: comes in when enemies and bullets are on screen.")]
        public AudioClip fight;
        [Tooltip("The set piece at the end of the floor. Same length, tempo and key as the other two.")]
        public AudioClip finale;
        [Tooltip("One second sting played when the hero dies, then the explore layer.")]
        public AudioClip sting;
        [Tooltip("Shared tempo of the three loops; a bar is 4 beats.")]
        public float bpm = 120f;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("The signal that starts the finale (e.g. floors:1000:finale).")]
        public string finaleSignal;
        [Tooltip("Seconds the fight layer holds after the last enemy of a wave is down.")]
        public float fightHoldSeconds = 1f;

        public bool Complete => explore != null && fight != null && finale != null && bpm > 1f;
    }
}
