using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>One line of Hell's announcer gloating over a death: the subtitle and its audio.json voice id.</summary>
    [Serializable]
    public class DeathLine
    {
        public string text = "";
        [Tooltip("audio.json sound id (voice announcer).")]
        public string sound = "";
    }

    /// <summary>
    /// Defines a death scene: Hell's announcer gloating over a death (Docs/Story.md, "Death and runs").
    /// Death scenes play a short animation/sequence, then the level again, or on a floor stage the Shaft's drop back
    /// to the Waystone and the next run.
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

        [Header("Flow")]
        [Tooltip("Delay in seconds before reloading the level after death animation")]
        public float delayBeforeReload = 1f;

        [Header("Words")]
        [Tooltip("The big title over the death animation.")]
        public string title = "You Obviously Died";
        [Tooltip("Hell's announcer gloating as it plays: one line per death, taken in turn.")]
        public List<DeathLine> gloats = new List<DeathLine>();

        [Header("Timeline")]
        public TimelineConfig timeline;
    }
}
