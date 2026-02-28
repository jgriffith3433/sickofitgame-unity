using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    public enum TimelineTrackType
    {
        Audio,
        Activation,
        SpriteFrame
    }

    /// <summary>
    /// Defines the timeline for a level, video, or death scene.
    /// The runtime TimelineRunner builds a PlayableDirector from this config.
    /// </summary>
    [Serializable]
    public class TimelineConfig
    {
        public float duration = 60f;
        public List<TimelineTrackConfig> tracks = new List<TimelineTrackConfig>();
    }

    /// <summary>
    /// One track in a timeline.
    /// </summary>
    [Serializable]
    public class TimelineTrackConfig
    {
        public TimelineTrackType trackType;
        public string trackName;
        public List<TimelineClipConfig> clips = new List<TimelineClipConfig>();
    }

    /// <summary>
    /// One clip on a timeline track.
    /// Which fields are used depends on the parent track type.
    /// </summary>
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
        [Tooltip("Reference to SpriteFramesData ScriptableObject")]
        public ScriptableObject spriteFramesData;
    }
}
