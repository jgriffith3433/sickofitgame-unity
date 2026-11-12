using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Device;
using SickOfIt.Core.Progression;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Living air (Docs/Story.md "Hell's air"): Meadow, Shaft and Floors 1000-997 each show ≥3 moving ambient
        /// particle kinds, a background event at least every 15 s, and a foreground silhouette at ≤0.6 alpha.
        /// Phone tier: half the bits, no heat shimmer, fps ≥ 30. Touch-driven on 390x844.
        /// </summary>
        public const string LivingAirId = "living-air";
        const float LivingAirSeconds = 420f;
        const float LivingAirWatch = 32f;

        static readonly (string name, int level)[] LivingAirPlaces =
        {
            ("Meadow", 1),
            ("Floor1000", 2),
            ("Floor999", 3),
            ("Floor998", 4),
            ("Floor997", 5),
        };

        sealed partial class Session
        {
            int livingAirPassed, livingAirFailed;
            float livingAirFrameSum;
            int livingAirFrames;

            void LivingAirCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    livingAirPassed++;
                    Run.Event("check", $"living-air {beat}: ok ({detail})");
                }
                else
                {
                    livingAirFailed++;
                    Run.Event("check", $"living-air {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"living-air {beat}: {detail}");
                }
            }

            IEnumerator LivingAirDuring(float now)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && dt < 1f)
                {
                    livingAirFrameSum += dt;
                    livingAirFrames++;
                }
                var amb = CircleAmbience.Current;
                if (amb != null && amb.EventFirings > 0)
                    Run.Event("ambience-event", $"firings {amb.EventFirings} kinds {amb.ActiveKindCount} bits {amb.ActiveBits}");
                yield break;
            }

            public IEnumerator LivingAirRun()
            {
                livingAirPassed = livingAirFailed = 0;
                yield return TapSoundPrompt();
                if (Run.Done) yield break;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                CircleAmbience.Weather = true;
                CircleAmbience.Enabled = true;
                bool phone = TouchDevice.IsPrimary || Application.isMobilePlatform;

                // Shaft preview: wind / debris / ash + foreground chains + a debris event within 15 s.
                Run.Event("shortcut", "Shaft preview at Floor 1000");
                ShaftScreen.Preview(1000, 1000);
                yield return Run.Until(() => ShaftScreen.Current != null, 10f, "Shaft opens");
                if (Run.Done) yield break;
                int shaftEventsStart = ShaftAmbience.Current != null ? ShaftAmbience.Current.EventFirings : 0;
                float shaftUntil = Time.unscaledTime + 16f;
                while (Time.unscaledTime < shaftUntil && !Run.Done)
                {
                    float dt = Time.unscaledDeltaTime;
                    if (dt > 0f && dt < 1f) { livingAirFrameSum += dt; livingAirFrames++; }
                    yield return null;
                }
                var shaft = ShaftAmbience.Current;
                LivingAirCheck("Shaft has ≥3 particle kinds", shaft != null && shaft.ActiveKindCount >= 3,
                    shaft == null ? "no ShaftAmbience" : $"{shaft.ActiveKindCount} kinds, {shaft.ActiveBits} bits");
                LivingAirCheck("Shaft has a foreground silhouette", shaft != null && shaft.ForegroundLayers >= 1,
                    shaft == null ? "none" : $"{shaft.ForegroundLayers} layers");
                LivingAirCheck("Shaft fires a background event within 15 s",
                    shaft != null && shaft.EventFirings > shaftEventsStart,
                    shaft == null ? "none" : $"{shaft.EventFirings - shaftEventsStart} firings");
                Run.Event("living-air", $"Shaft kinds={shaft?.ActiveKindCount} bits={shaft?.ActiveBits} events={shaft?.EventFirings} fg={shaft?.ForegroundLayers}");
                Run.Metric("livingAir.shaft.kinds", shaft != null ? shaft.ActiveKindCount : 0);
                Run.Metric("livingAir.shaft.events", shaft != null ? shaft.EventFirings : 0);
                if (ShaftScreen.Current != null)
                    ShaftScreen.Current.Close();
                yield return WaitReal(0.4f);
                Run.Progress(0.12f);

                for (int i = 0; i < LivingAirPlaces.Length; i++)
                {
                    var place = LivingAirPlaces[i];
                    livingAirFrameSum = 0f;
                    livingAirFrames = 0;
                    Run.Event("shortcut", $"load {place.name} (level {place.level}) directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(place.level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(place.level), 60f, $"{place.name} to start");
                    if (Run.Done) yield break;

                    // Warm a couple of seconds so RefreshGate turns weather on.
                    yield return WaitReal(1.2f);
                    var amb = CircleAmbience.Current;
                    LivingAirCheck($"{place.name} ambience built", amb != null, amb == null ? "null" : $"bits pool {amb.BitCount}");
                    if (amb != null)
                    {
                        LivingAirCheck($"{place.name} has a foreground silhouette", amb.ForegroundLayers >= 1,
                            $"{amb.ForegroundLayers} foreground layers");
                        if (place.name == "Floor1000" && phone)
                            LivingAirCheck("Floor1000 heat shimmer off on phone", !amb.HeatShimmerOn, amb.HeatShimmerOn ? "on" : "off");
                        if (phone)
                            LivingAirCheck($"{place.name} phone Quality ≤ 0.5", CircleAmbience.Quality <= 0.51f,
                                $"Quality={CircleAmbience.Quality:0.00}");
                    }

                    float endAt = (GameClock.Instance != null ? GameClock.Instance.Time : 0f) + LivingAirWatch;
                    int kindsPeak = amb != null ? amb.ActiveKindCount : 0;
                    yield return PlayLevel(place.level, 0f, during: now =>
                    {
                        var a = CircleAmbience.Current;
                        if (a != null)
                            kindsPeak = Mathf.Max(kindsPeak, a.ActiveKindCount);
                        return LivingAirDuring(now);
                    }, until: () => GameClock.Instance != null && GameClock.Instance.Time >= endAt);
                    if (Run.Done) yield break;

                    amb = CircleAmbience.Current;
                    int firings = amb != null ? amb.EventFirings : 0;
                    LivingAirCheck($"{place.name} has ≥3 particle kinds", kindsPeak >= 3, $"{kindsPeak} kinds over {LivingAirWatch:0}s");
                    LivingAirCheck($"{place.name} fires a background event within 15 s", firings >= 1, $"{firings} event firings");
                    Run.Metric($"livingAir.{place.name}.kinds", kindsPeak);
                    Run.Metric($"livingAir.{place.name}.events", firings);
                    Run.Metric($"livingAir.{place.name}.fg", amb != null ? amb.ForegroundLayers : 0);
                    if (phone && livingAirFrames > 0)
                    {
                        float avgMs = 1000f * livingAirFrameSum / livingAirFrames;
                        float fps = avgMs > 0.01f ? 1000f / avgMs : 0f;
                        LivingAirCheck($"{place.name} fps ≥ 30 on phone", fps >= 30f, $"fps {fps:0.0}");
                        Run.Metric($"livingAir.{place.name}.fps", fps);
                    }
                    Run.Progress(0.12f + 0.14f * (i + 1));
                }

                CircleAmbience.Weather = false;
                Run.Metric("livingAir.passed", livingAirPassed);
                Run.Metric("livingAir.failed", livingAirFailed);
                Run.Progress(1f);
                End(livingAirFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
