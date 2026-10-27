using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// "Incoming warnings" (game.json "incomingWarnings"): a quiet tick at the screen's edge where a hostile shot or creature
    /// that is off screen is about to come in, so the hero can dodge instead of being surprised. Presentation only.
    /// </summary>
    [Serializable]
    public class IncomingWarningsConfig
    {
        [Tooltip("Show the warnings at all (the player's own switch is Settings > Accessibility > Incoming warnings).")]
        public bool enabled = true;
        [Tooltip("Only things that will enter the view within this many seconds get a marker.")]
        public float lookaheadSeconds = 1.5f;
        [Tooltip("At most this many markers at once, soonest arrival first.")]
        public int maxMarkers = 4;
        [Tooltip("Things that cross the same edge within this fraction of the edge's length of each other share one marker (a rain of fireballs is one band).")]
        public float mergeFraction = 0.3f;
        [Tooltip("Marker opacity when the thing is lookaheadSeconds away.")]
        public float farAlpha = 0.25f;
        [Tooltip("Marker opacity when it is nearSeconds away.")]
        public float nearAlpha = 0.7f;
        [Tooltip("From here on the marker is at full strength (seconds to arrival).")]
        public float nearSeconds = 0.3f;
        [Tooltip("Seconds a marker takes to fade in, and to fade out when its thing is on screen (game time).")]
        public float fadeInSeconds = 0.25f;
        public float fadeOutSeconds = 0.12f;
        [Tooltip("Marker depth (how far it reaches in from the edge) and length, as fractions of the screen's short side.")]
        public float depth = 0.035f;
        public float length = 0.08f;
        [Tooltip("How far in from the very edge the marker sits, as a fraction of the short side.")]
        public float inset = 0.008f;
        [Tooltip("Tint of a hostile shot (fireballs, embers).")]
        public Color shotColor = new Color(1f, 0.56f, 0.24f, 1f);
        [Tooltip("Tint of a creature.")]
        public Color creatureColor = new Color(0.86f, 0.42f, 0.62f, 1f);

        /// <summary>Everything wrong with the numbers (empty = fine); the importer's tests and the playtest read it.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (lookaheadSeconds < 0.5f || lookaheadSeconds > 5f)
                problems.Add($"incomingWarnings.lookaheadSeconds {lookaheadSeconds} must be 0.5-5.");
            if (maxMarkers < 1 || maxMarkers > 12)
                problems.Add($"incomingWarnings.maxMarkers {maxMarkers} must be 1-12.");
            if (mergeFraction < 0.02f || mergeFraction > 1f)
                problems.Add($"incomingWarnings.mergeFraction {mergeFraction} must be 0.02-1.");
            if (farAlpha < 0f || nearAlpha > 1f || farAlpha > nearAlpha)
                problems.Add($"incomingWarnings alpha: need 0 <= farAlpha ({farAlpha}) <= nearAlpha ({nearAlpha}) <= 1.");
            if (nearSeconds < 0f || nearSeconds >= lookaheadSeconds)
                problems.Add($"incomingWarnings.nearSeconds {nearSeconds} must be 0 or more and under lookaheadSeconds ({lookaheadSeconds}).");
            if (fadeInSeconds <= 0f || fadeOutSeconds <= 0f)
                problems.Add("incomingWarnings fade seconds must be above 0.");
            if (depth <= 0f || depth > 0.2f || length <= 0f || length > 0.5f || inset < 0f || inset > 0.1f)
                problems.Add("incomingWarnings depth (0-0.2), length (0-0.5) and inset (0-0.1) are fractions of the short side.");
            return problems;
        }
    }
}
