using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// A positional ambience one-shot tied to a prop or landmark in a level (docs/world-bible.md place beds).
    /// Played by <see cref="SickOfIt.Core.Audio.AmbientEmitters"/> on the Ambience bus: random gaps, stereo pan by
    /// screen x, falloff by distance from the hero. Presentation only.
    /// </summary>
    [Serializable]
    public class AmbientEmitterConfig
    {
        [Tooltip("audio.json sound id (Effect, Ambience bus).")]
        public string sound = "";
        [Tooltip("Prop or landmark this emitter sits on (for logs and the playtest).")]
        public string prop = "";
        [Tooltip("World position of the prop.")]
        public Vector2 at = Vector2.zero;
        [Tooltip("Shortest gap between plays (seconds).")]
        public float minInterval = 4f;
        [Tooltip("Longest gap between plays (seconds).")]
        public float maxInterval = 12f;
        [Tooltip("Distance at which the one-shot falls silent.")]
        public float range = 10f;
        [Range(0f, 1f)]
        public float volume = 0.7f;
    }
}
