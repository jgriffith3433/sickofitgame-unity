using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The recurring cast (Docs/Story.md "The Climbers", "The Overlord on the far ledge", "Bystanders"): checked as data (each has a voice with its own Hawkes Gen
        /// voice id, every line they say is a sound with a clip, each has an animator with an idle and a reaction, art that is not the shared damned-soul sheet),
        /// then one climber scene each (Rook 996, Wick 999, Pointer 900) played by the bot through real input.
        /// </summary>
        public const string CastId = "cast";
        const float CastSeconds = 900f;

        static readonly string[] CastVoices = { "rook", "wick", "pointer", "overlord", "yooba", "soul", "hawker", "heckler", "player" };
        static readonly string[] CastEntities =
        {
            "rook", "wick", "pointer", "overlordFar997", "overlordFar958", "overlordFar806",
            "bystanderSoul", "hawkerSoul", "hecklerSoul", "cheerSoul", "giverSoul", "hidingSoul",
        };
        static readonly (int floor, string who)[] CastScenes = { (996, "rook"), (999, "wick"), (900, "pointer") };

        sealed partial class Session
        {
            void CastCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"cast {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"cast {beat}: {detail}");
            }

            public IEnumerator CastRun()
            {
                var def = GameDefinition.Instance;
                var content = GameContent.Current;
                var audio = def != null ? def.audio : null;
                if (audio == null || content == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "cast: no game definition or content loaded");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. Voices: each speaks with a voice id of its own.
                var ids = new Dictionary<string, string>();
                foreach (string name in CastVoices)
                {
                    var voice = audio.voices.Find(v => v.name == name);
                    string id = voice != null ? voice.voiceId : "";
                    string clash = null;
                    foreach (var pair in ids)
                        if (!string.IsNullOrEmpty(id) && pair.Value == id)
                            clash = pair.Key;
                    ids[name] = id;
                    CastCheck($"voice {name}", voice != null && !string.IsNullOrEmpty(id) && clash == null,
                        voice == null ? "no such voice in audio.json" : $"voice id {id}" + (clash != null ? $", the SAME as {clash}" : ", distinct"));
                }

                // 2. Lines: every sound of a cast voice has a clip.
                var perVoice = new Dictionary<string, int>();
                var silent = new List<string>();
                foreach (var s in audio.sounds)
                {
                    if (s.kind != SoundKind.Voice || System.Array.IndexOf(CastVoices, s.voice) < 0)
                        continue;
                    perVoice[s.voice] = (perVoice.TryGetValue(s.voice, out int n) ? n : 0) + 1;
                    if (s.clip == null)
                        silent.Add(s.id);
                }
                foreach (string name in CastVoices)
                    Run.Metric($"cast.lines.{name}", perVoice.TryGetValue(name, out int n) ? n : 0);
                CastCheck("every cast line has a clip", silent.Count == 0, silent.Count == 0 ? "all voiced" : "no clip: " + string.Join(", ", silent.GetRange(0, Mathf.Min(8, silent.Count))));

                // 3. Art: an animator with an idle (no placeholder circle) and, for the climbers and bystanders, a reaction state.
                foreach (string id in CastEntities)
                {
                    if (!content.Entities.TryGetValue(id, out var e))
                    {
                        CastCheck($"art {id}", false, "there is no such entity");
                        continue;
                    }
                    var states = e.visual != null && e.visual.animator != null ? e.visual.animator.states : null;
                    bool idle = false, react = false;
                    if (states != null)
                        foreach (var st in states)
                        {
                            idle |= st.stateName == "Idle";
                            react |= st.stateName == "Hiccup" || st.stateName == "React" || st.stateName == "Talk";
                        }
                    bool needsReact = !id.StartsWith("overlordFar");
                    CastCheck($"art {id}", idle && (react || !needsReact), $"idle {idle}, reaction {react}, states {(states != null ? states.Count : 0)}");
                }

                // 4. The climbers, played: each appears, speaks and answers in the footage.
                for (int i = 0; i < CastScenes.Length && !Run.Done; i++)
                {
                    var (floor, who) = CastScenes[i];
                    yield return CompanionsOne(floor, who, i, CastScenes.Length);
                }
            }

            IEnumerator CompanionsOne(int floor, string who, int index, int total)
            {
                GameplayTelemetry.Recorded += CompanionTelemetry;
                try
                {
                    yield return CompanionPlay(index, floor, who, "", false, total);
                    bool ok = companionSignals.Contains($"cmp:{who}:{floor}:done");
                    CastCheck($"{who} on floor {floor} appears, speaks and walks on", ok, ok ? "scene played to its end" : "the scene did not finish");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= CompanionTelemetry;
                }
            }
        }
    }
}
