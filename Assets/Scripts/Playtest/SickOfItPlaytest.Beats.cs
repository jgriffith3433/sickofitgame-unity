using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core.Progression;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The opening-beats scenario: New Game goes straight into Level 1, then each of the first five levels shows its own beat line.</summary>
        public const string BeatsId = "opening-beats";
        const float BeatsSeconds = 300f;

        // Floors 1000-997: the hero's voiced line inside each floor's storyBeat (the warm-grey caption is gone; scenario `story` checks the rest).
        static readonly string[] BeatLines =
        {
            "Then the sky opened.",
            "Floor one thousand. Of course it's a thousand.",
            "You said that already.",
            "That was the baker's cart.",
            "Those are ours.",
        };

        sealed partial class Session
        {
            int beatsFailed;

            void BeatsCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"beats {beat}: ok ({detail})"); return; }
                beatsFailed++;
                Run.Event("check", $"beats {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"beats {beat}: {detail}");
            }

            static bool BeatOnScreen(string line)
            {
                foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                    if (text != null && text.isActiveAndEnabled && (text.text ?? "").Contains(line))
                        return true;
                return false;
            }

            IEnumerator WatchBeat(int index, int level)
            {
                string line = BeatLines[index];
                yield return Run.Until(() => GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null, 90f, $"level {level} to start");
                if (Run.Done) yield break;
                Run.Scene($"Level {level}");
                yield return Run.Until(() => BeatOnScreen(line), 25f, $"level {level}'s beat line");
                bool shown = BeatOnScreen(line);
                if (shown) Run.Event("beat", $"level {level}: \"{line}\"");
                BeatsCheck($"level {level}", shown, shown ? "its beat line is on screen" : "no beat line appeared");
                yield return WaitReal(2f);
            }

            public IEnumerator BeatsRun()
            {
                var seen = new HashSet<string>(BeatLines);
                BeatsCheck("distinct", seen.Count == BeatLines.Length, $"{seen.Count} different beat lines for {BeatLines.Length} levels");

                // 1. A fresh save: New Game goes straight into Level 1 (no film before play), within 20 s of the pick.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                float picked = Time.realtimeSinceStartup;
                yield return ClickNewGame(() => LevelRunning(1) || Object.FindAnyObjectByType<VideoRuntime>() != null);
                if (Run.Done) yield break;
                bool film = Object.FindAnyObjectByType<VideoRuntime>() != null;
                BeatsCheck("straight into play", !film, film ? "New Game opened a video before Level 1" : "New Game opened Level 1");
                if (film) yield return Tap(Key.Escape);
                yield return Run.Until(() => GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null, 60f, "Level 1 to start");
                if (Run.Done) yield break;
                float toPlay = Time.realtimeSinceStartup - picked;
                Run.Metric("beats.newGameToPlaySeconds", toPlay);
                BeatsCheck("playing fast", toPlay <= 20f, $"playing {toPlay:0.0} s after New Game");

                // 2. Level 1 through the real flow, then Floors 1000-997 loaded directly.
                yield return WatchBeat(0, 1);
                if (Run.Done) yield break;
                for (int i = 1; i < BeatLines.Length; i++)
                {
                    Run.Event("shortcut", $"load level {i + 1} directly");
                    Time.timeScale = 1f;
                    var load = LoadLevel(i + 1);
                    if (load != null) yield return load;
                    yield return WatchBeat(i, i + 1);
                    if (Run.Done) yield break;
                }
                BeatsCheck("none repeated", seen.Count == BeatLines.Length, "five levels, five different beats");
                Run.Progress(1f);
                End(beatsFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
