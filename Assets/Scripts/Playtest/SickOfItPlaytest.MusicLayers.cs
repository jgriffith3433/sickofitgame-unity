using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hawkes.Playtest;
using SickOfIt.Core;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The music-layers scenario (Docs/Story.md "Music", the intensity layer): Floor 997 (a long bullet-hell floor in the Ember Wastes, loaded
        /// directly: a setup shortcut) played by the bot. The Circle's intensity layer (stand-in loop) must be silent on the empty floor, come in as
        /// enemies and shots crowd the screen, and fade out again once it empties. `musicLayer` telemetry gives its level (0/25/50/75/100) and the crowd.
        /// </summary>
        public const string MusicLayersId = "music-layers";
        const int MusicLayersLevel = 5;
        const float MusicLayersSeconds = 300f;

        sealed partial class Session
        {
            int layerPassed, layerFailed;
            readonly List<(float time, int level, float crowd)> layerEvents = new List<(float, int, float)>();
            string layerClipName = "";

            void LayerCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    layerPassed++;
                    Run.Event("check", $"music-layers {beat}: ok ({detail})");
                }
                else
                {
                    layerFailed++;
                    Run.Event("check", $"music-layers {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"music-layers {beat}: {detail}");
                }
            }

            void LayerTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind != "musicLayer")
                    return;
                float crowd = 0f;
                if (e.Detail.StartsWith("crowd "))
                    float.TryParse(e.Detail.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out crowd);
                layerClipName = e.Subject;
                layerEvents.Add((e.Time, e.Value, crowd));
                Run.Event("musicLayer", $"{e.Subject}: {e.Value}% at {e.Time:0.0} s, crowd {crowd:0.0}");
            }

            public IEnumerator MusicLayersRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                GameplayTelemetry.Recorded += LayerTelemetry;
                try
                {
                    Run.Event("shortcut", $"load level {MusicLayersLevel} directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(MusicLayersLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(MusicLayersLevel), 60f, "the floor to start");
                    if (Run.Done) yield break;
                    yield return PlayLevel(MusicLayersLevel, 0f, until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= 150f);
                    if (Run.Done) yield break;

                    LayerCheck("the Circle has an intensity layer", layerEvents.Count > 0, $"{layerEvents.Count} musicLayer events, clip \"{layerClipName}\"");
                    if (layerEvents.Count > 0)
                    {
                        LayerCheck("it starts silent on the empty floor", layerEvents[0].level == 0, $"first level {layerEvents[0].level}% at crowd {layerEvents[0].crowd:0.0}");
                        int peak = 0, peakAt = 0;
                        float peakCrowd = 0f, quietCrowd = float.MaxValue;
                        for (int i = 0; i < layerEvents.Count; i++)
                        {
                            if (layerEvents[i].level > peak)
                            {
                                peak = layerEvents[i].level;
                                peakAt = i;
                                peakCrowd = layerEvents[i].crowd;
                            }
                        }
                        LayerCheck("it fades in as the floor crowds", peak >= 50, $"peak {peak}% at crowd {peakCrowd:0.0}");
                        // Whenever it was at its peak or more than a half, the crowd had been above the Circle's low mark.
                        bool crowded = true;
                        foreach (var ev in layerEvents)
                        {
                            if (ev.level >= 75 && ev.crowd < 3f)
                                crowded = false;
                        }
                        LayerCheck("it is loud only while crowded", crowded, "every level of 75% or more came with a crowd of 3 or more");
                        int low = 100;
                        for (int i = peakAt + 1; i < layerEvents.Count; i++)
                        {
                            if (layerEvents[i].level < low)
                            {
                                low = layerEvents[i].level;
                                quietCrowd = layerEvents[i].crowd;
                            }
                        }
                        LayerCheck("it fades out as the floor empties", peak >= 50 && low <= peak - 50, $"after the peak ({peak}%) it fell to {(low == 100 ? "nothing" : low + "%")} at crowd {(quietCrowd == float.MaxValue ? 0f : quietCrowd):0.0}");
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= LayerTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("musicLayers.passed", layerPassed);
                Run.Metric("musicLayers.failed", layerFailed);
                Run.Metric("musicLayers.events", layerEvents.Count);
                Run.Progress(1f);
                End(layerFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
