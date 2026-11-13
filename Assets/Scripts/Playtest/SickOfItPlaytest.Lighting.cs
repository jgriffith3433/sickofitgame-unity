using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Lighting and post (Docs/lighting.md): Level 1 and Floors 1000-997 (levels 1-5) loaded directly and played through
        /// real input. Per floor: a distinct dominant grade colour, bloom around lava/torches/bullets, lava/torch lights
        /// within 2 units with flicker, a hero rim, and on phone (390x844, touch forced) the phone tier (half-res bloom, no
        /// grain, ≤8 lights) holding 30+ fps. Rewind scanlines stay off the playfield centre. check events lighting &lt;beat&gt;.
        /// </summary>
        public const string LightingId = "lighting";
        const float LightingSeconds = 480f;
        const int LightingFirstLevel = 1, LightingLastLevel = 5;
        const float LightingPlaySeconds = 22f;

        sealed partial class Session
        {
            int lightingPassed, lightingFailed;

            void LightingCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    lightingPassed++;
                    Run.Event("check", $"lighting {beat}: ok ({detail})");
                }
                else
                {
                    lightingFailed++;
                    Run.Event("check", $"lighting {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"lighting {beat}: {detail}");
                }
            }

            static float GradeHue(Color c)
            {
                Color.RGBToHSV(c, out float h, out _, out _);
                return h;
            }

            public IEnumerator LightingRun()
            {
                bool touch = GameplayHud.TouchUiOn || Mathf.Min(Screen.width, Screen.height) <= 500f;
                GameplayHud.TouchUiForced = true;
                Run.Event("shortcut", "touch UI forced for the phone lighting tier");
                try
                {
                    yield return ToMainMenu();
                    if (Run.Done) yield break;

                    var cfg = GameDefinition.Instance != null ? GameDefinition.Instance.lighting : null;
                    LightingCheck("lighting config", cfg != null && cfg.enabled && cfg.profiles != null && cfg.profiles.Count >= 5,
                        cfg != null ? $"{(cfg.profiles != null ? cfg.profiles.Count : 0)} profiles" : "no lighting config");

                    var grades = new List<Color>();
                    var ids = new List<string>();
                    float worstFps = 999f;

                    for (int level = LightingFirstLevel; level <= LightingLastLevel; level++)
                    {
                        if (Run.Done) yield break;
                        Prepare(level, level == LightingFirstLevel);
                        var old = GameplayWorld.Instance;
                        var load = LoadLevel(level);
                        if (load != null)
                            yield return load;
                        yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} to start");
                        if (Run.Done) yield break;

                        float frameSum = 0f;
                        int frameN = 0;
                        float frameWorst = 0f;
                        int lightsPeak = 0;
                        bool rimSeen = false, darknessSeen = false, gradeReady = false;
                        bool phoneTier = false, grainOff = false, halfBloom = false;
                        Color grade = Color.clear;
                        string profileId = "";
                        float bloom = 0f;

                        yield return PlayLevel(level, 0f, during: now =>
                        {
                            var lit = WorldLighting.Current;
                            if (lit != null)
                            {
                                profileId = lit.ProfileId;
                                grade = lit.GradeColor;
                                gradeReady = lit.GradeReady;
                                rimSeen |= lit.RimShown;
                                darknessSeen |= lit.DarknessShown;
                                phoneTier = lit.PhoneTier;
                                lightsPeak = Mathf.Max(lightsPeak, lit.LightsDrawn);
                                bloom = lit.BloomIntensity;
                                grainOff = !lit.GrainOn;
                                halfBloom = lit.HalfResBloom || lit.WantsHalfResBloom;
                            }
                            float dt = Time.unscaledDeltaTime;
                            if (dt > 0f && dt < 0.25f)
                            {
                                frameSum += dt;
                                frameN++;
                                frameWorst = Mathf.Max(frameWorst, dt);
                            }
                            return null;
                        }, until: () => GameClock.Instance != null && GameClock.Instance.Time >= LightingPlaySeconds);
                        input.ReleaseAll();
                        if (Run.Done) yield break;

                        // One short rewind: glitch must stay a frame effect (coverage is edge-weighted).
                        if (RewindBudget.Instance != null)
                        {
                            RewindBudget.Instance.RaiseMax(10f, false);
                            float t0 = Time.realtimeSinceStartup;
                            input.SetKey(UnityEngine.InputSystem.Key.R, true);
                            float cov = 0f;
                            while (!Run.Done && Time.realtimeSinceStartup - t0 < 1.2f)
                            {
                                cov = Mathf.Max(cov, RewindFx.Coverage);
                                yield return null;
                            }
                            input.SetKey(UnityEngine.InputSystem.Key.R, false);
                            yield return Run.Until(() => !RewindFx.Active, 2f, "rewind fx to end");
                            LightingCheck($"rewind off playfield (level {level})", cov > 0.05f && cov < 0.85f,
                                $"edge coverage {cov:0.00} (frame only, not a full-screen wash)");
                        }

                        float fps = frameN > 0 ? frameN / frameSum : 0f;
                        worstFps = Mathf.Min(worstFps, fps);
                        Run.Metric($"lighting.level{level}.fps", fps);
                        Run.Metric($"lighting.level{level}.frameWorstMs", frameWorst * 1000f);
                        Run.Metric($"lighting.level{level}.lights", lightsPeak);
                        Run.Scene($"lighting-{profileId}");
                        Run.Event("lighting", $"level {level} profile {profileId}: grade #{ColorUtility.ToHtmlStringRGB(grade)} strength-ready {gradeReady}, rim {rimSeen}, darkness {darknessSeen}, lights {lightsPeak}, bloom {bloom:0.00}, phone {phoneTier}, fps {fps:0.0}");

                        LightingCheck($"profile on level {level}", !string.IsNullOrEmpty(profileId) && gradeReady,
                            $"id {profileId}, grade pass {gradeReady}");
                        LightingCheck($"hero rim (level {level})", rimSeen, rimSeen ? "rim sprite on" : "no rim");
                        LightingCheck($"darkness layer (level {level})", darknessSeen, darknessSeen ? "multiply darkness on" : "no darkness");
                        LightingCheck($"bloom (level {level})", bloom > 0.2f, $"bloom intensity {bloom:0.00}");
                        LightingCheck($"lights within cap (level {level})", lightsPeak > 0 && lightsPeak <= (phoneTier ? 8 : 24),
                            $"{lightsPeak} lights, phone {phoneTier}");
                        var reach = WorldLighting.Current != null ? WorldLighting.Current.LightReach : 0f;
                        LightingCheck($"light reach 2u (level {level})", Mathf.Abs(reach - 2f) < 0.05f, $"reach {reach:0.00}");

                        grades.Add(grade);
                        ids.Add(profileId);

                        if (phoneTier || touch)
                        {
                            LightingCheck($"phone tier (level {level})", phoneTier && grainOff && halfBloom,
                                $"phone {phoneTier}, grain off {grainOff}, half-res bloom {halfBloom}");
                            LightingCheck($"phone fps (level {level})", fps >= 30f, $"{fps:0.0} fps (floor 30)");
                        }

                        Run.Progress((level - LightingFirstLevel + 1f) / (LightingLastLevel - LightingFirstLevel + 1f));
                    }

                    // Distinct dominant grades across the five floors.
                    int distinct = 0;
                    for (int i = 0; i < grades.Count; i++)
                    {
                        bool unique = true;
                        for (int j = 0; j < i; j++)
                        {
                            float dh = Mathf.Abs(GradeHue(grades[i]) - GradeHue(grades[j]));
                            dh = Mathf.Min(dh, 1f - dh);
                            float d = Vector3.Distance(new Vector3(grades[i].r, grades[i].g, grades[i].b),
                                new Vector3(grades[j].r, grades[j].g, grades[j].b));
                            if (dh < 0.06f && d < 0.18f)
                                unique = false;
                        }
                        if (unique)
                            distinct++;
                        Run.Event("lighting", $"grade {ids[i]} #{ColorUtility.ToHtmlStringRGB(grades[i])}");
                    }
                    LightingCheck("five distinct grades", distinct >= 5 && grades.Count >= 5,
                        $"{distinct} distinct of {grades.Count}: {string.Join(", ", ids)}");

                    Run.Metric("lighting.worstFps", worstFps < 900f ? worstFps : 0f);
                    Run.Metric("lighting.passed", lightingPassed);
                    Run.Metric("lighting.failed", lightingFailed);
                    End(lightingFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
                }
                finally
                {
                    GameplayHud.TouchUiForced = false;
                }
            }
        }
    }
}
