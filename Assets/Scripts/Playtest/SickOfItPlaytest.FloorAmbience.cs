using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Device;
using SickOfIt.Core.Progression;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Place ambience (docs/world-bible.md): the Meadow, the Shaft and Floors 1000-997 each play their own Ambience-bus
        /// bed from the first second, with ≥3 positional one-shots per minute panned by screen x. Phone: after the first
        /// touch, fps ≥ 30. Touch-driven on 390x844.
        /// </summary>
        public const string FloorAmbienceId = "floor-ambience";
        const float FloorAmbienceSeconds = 480f;
        const float FloorAmbienceListen = 50f;

        static readonly (string name, string bedKey, int level)[] FloorAmbiencePlaces =
        {
            ("Meadow", "meadow", 1),
            ("Floor1000", "floor1000", 2),
            ("Floor999", "floor999", 3),
            ("Floor998", "floor998", 4),
            ("Floor997", "floor997", 5),
        };

        sealed partial class Session
        {
            int floorAmbPassed, floorAmbFailed;
            readonly List<Sfx.PlayedSound> floorAmbLog = new List<Sfx.PlayedSound>();
            int floorAmbLastFired;
            readonly List<(string id, float pan)> floorAmbPans = new List<(string, float)>();
            float floorAmbFrameSum;
            int floorAmbFrames;

            void FloorAmbCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    floorAmbPassed++;
                    Run.Event("check", $"floor-ambience {beat}: ok ({detail})");
                }
                else
                {
                    floorAmbFailed++;
                    Run.Event("check", $"floor-ambience {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"floor-ambience {beat}: {detail}");
                }
            }

            void FloorAmbOnPlayed(Sfx.PlayedSound p)
            {
                floorAmbLog.Add(p);
                if (p.bus == Bus.Ambience)
                    Run.Event("sound", $"{p.id} volume {p.volume.ToString("0.00", CultureInfo.InvariantCulture)} bus Ambience");
            }

            IEnumerator FloorAmbDuring(float now)
            {
                if (AmbientEmitters.FiredTotal > floorAmbLastFired)
                {
                    floorAmbLastFired = AmbientEmitters.FiredTotal;
                    floorAmbPans.Add((AmbientEmitters.LastId, AmbientEmitters.LastPan));
                    Run.Event("emitter", $"{AmbientEmitters.LastId} pan {AmbientEmitters.LastPan.ToString("0.00", CultureInfo.InvariantCulture)}");
                }
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && dt < 1f)
                {
                    floorAmbFrameSum += dt;
                    floorAmbFrames++;
                }
                yield break;
            }

            public IEnumerator FloorAmbienceRun()
            {
                yield return TapSoundPrompt();
                if (Run.Done) yield break;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                string page = PageSound.State();
                if (page != null)
                {
                    bool gone = page.Contains("\"prompt\":\"gone\"") && !page.Contains("\"taps\":0");
                    FloorAmbCheck("plays after the first touch", gone && !page.Contains("\"audio\":\"none\""), page);
                }
                else
                    Run.Event("tap-prompt", "editor/standalone: page touch check skipped");

                Sfx.Played += FloorAmbOnPlayed;
                try
                {
                    Run.Event("shortcut", "Shaft preview at Floor 1000");
                    var shaft = ShaftScreen.Preview(1000, 1000);
                    yield return Run.Until(() => ShaftScreen.Current != null, 10f, "Shaft opens");
                    if (Run.Done) yield break;
                    yield return WaitReal(1.2f);
                    bool shaftBed = AmbienceBed.Playing && AmbienceBed.Current != null
                        && AmbienceBed.Current.IndexOf("shaft", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    FloorAmbCheck("Shaft bed from the first second", shaftBed, $"{AmbienceBed.Current}, vol {AmbienceBed.Volume:0.000}");
                    Run.Metric("floorAmbience.shaftBed", shaftBed ? 1 : 0);
                    if (ShaftScreen.Current != null)
                        ShaftScreen.Current.Close();
                    yield return WaitReal(0.4f);

                    for (int i = 0; i < FloorAmbiencePlaces.Length; i++)
                    {
                        var place = FloorAmbiencePlaces[i];
                        Run.Progress((i + 1) / (float)(FloorAmbiencePlaces.Length + 1));
                        int firedBefore = AmbientEmitters.FiredTotal;
                        floorAmbLastFired = firedBefore;
                        floorAmbPans.Clear();
                        floorAmbFrameSum = 0f;
                        floorAmbFrames = 0;

                        Run.Event("shortcut", $"load {place.name} (level {place.level}) directly");
                        var old = GameplayWorld.Instance;
                        var load = LoadLevel(place.level);
                        if (load != null)
                            yield return load;
                        yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(place.level), 60f, $"{place.name} to start");
                        if (Run.Done) yield break;

                        yield return Run.Until(() => AmbienceBed.Playing && AmbienceBed.Current != null
                            && AmbienceBed.Current.IndexOf(place.bedKey, System.StringComparison.OrdinalIgnoreCase) >= 0, 2.5f, $"{place.name} bed");
                        if (Run.Done) yield break;

                        bool bedOk = AmbienceBed.Playing && AmbienceBed.Volume > 0.01f
                            && AmbienceBed.Current != null
                            && AmbienceBed.Current.IndexOf(place.bedKey, System.StringComparison.OrdinalIgnoreCase) >= 0;
                        FloorAmbCheck($"{place.name} bed from the first second", bedOk, $"{AmbienceBed.Current}, vol {AmbienceBed.Volume:0.000}");
                        Run.Event("ambience", $"{place.name} bed {AmbienceBed.Current}");
                        FloorAmbCheck($"{place.name} has emitters armed", AmbientEmitters.ArmedCount >= 3,
                            $"{AmbientEmitters.ArmedCount} armed, cap {AmbientEmitters.Cap}");

                        float endAt = (GameClock.Instance != null ? GameClock.Instance.Time : 0f) + FloorAmbienceListen;
                        yield return PlayLevel(place.level, 0f, during: FloorAmbDuring,
                            until: () => GameClock.Instance != null && GameClock.Instance.Time >= endAt);
                        if (Run.Done) yield break;

                        int fired = AmbientEmitters.FiredTotal - firedBefore;
                        float perMin = fired * (60f / Mathf.Max(1f, FloorAmbienceListen));
                        FloorAmbCheck($"{place.name} ≥3 positional one-shots per minute", perMin >= 3f,
                            $"{fired} fires in {FloorAmbienceListen:0}s ≈ {perMin:0.0}/min");
                        Run.Metric($"floorAmbience.{place.name}.perMin", perMin);
                        Run.Metric($"floorAmbience.{place.name}.fired", fired);

                        bool anyPan = floorAmbPans.Exists(p => Mathf.Abs(p.pan) > 0.04f);
                        FloorAmbCheck($"{place.name} pans toward the prop's side", fired == 0 || anyPan,
                            fired == 0 ? "no fires" : $"pans {string.Join(", ", floorAmbPans.ConvertAll(p => $"{p.id}:{p.pan:0.00}"))}");

                        if (TouchDevice.IsPrimary && floorAmbFrames > 0)
                        {
                            float avgMs = 1000f * floorAmbFrameSum / floorAmbFrames;
                            float fps = avgMs > 0.01f ? 1000f / avgMs : 0f;
                            FloorAmbCheck($"{place.name} fps ≥ 30 on phone", fps >= 30f, $"fps {fps:0.0}");
                            Run.Metric($"floorAmbience.{place.name}.fps", fps);
                        }

                        if (i == 0)
                        {
                            Run.Metric("floorAmbience.cap", AmbientEmitters.Cap);
                            Run.Metric("floorAmbience.phone", TouchDevice.IsPrimary ? 1 : 0);
                        }
                    }
                }
                finally
                {
                    Sfx.Played -= FloorAmbOnPlayed;
                    input.ReleaseAll();
                    AmbientEmitters.Stop();
                }

                if (Run.Done) yield break;
                Run.Metric("floorAmbience.passed", floorAmbPassed);
                Run.Metric("floorAmbience.failed", floorAmbFailed);
                Run.Progress(1f);
                End(floorAmbFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
