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
        /// <summary>The opening-beats scenario: Video1 plays and is skipped, then each of the first five levels shows its own beat line.</summary>
        public const string BeatsId = "opening-beats";
        const float BeatsSeconds = 300f;

        static readonly string[] BeatLines =
        {
            "Then the sky opened.",
            "The bottom of Hell. He is already sick of it.",
            "One floor up. Hell is counting along.",
            "They took the village. Now they take the gun.",
            "Imps haul his village's diamonds. Same sack, every loop.",
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

                // 1. A fresh save: New Game opens Video1, which Escape skips.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickNewGame(() => Object.FindAnyObjectByType<VideoRuntime>() != null);
                if (Run.Done) yield break;
                yield return Run.Until(() => ClockTime > 0.1f, 60f, "Video1 to start playing");
                if (Run.Done) yield break;
                Run.Scene("Opening: Video1");
                float before = ClockTime;
                yield return WaitReal(4f);
                BeatsCheck("opening plays", ClockTime > before + 1f, $"the opening's clock ran from {before:0.0}s to {ClockTime:0.0}s");
                yield return Tap(Key.Escape);
                Run.Event("skip", "the opening (Video1)");

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
