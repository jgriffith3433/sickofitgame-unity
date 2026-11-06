using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The readability scenario (Docs/Story.md "Readability is fairness"): Level 1 and Floors 1000-997 (levels 1-5) are loaded directly (setup shortcut)
        /// and played by the bot through real input. Per level: (1) every hostile shot on screen wears the baseline rims (a level/Circle colour beside the
        /// shot, an ink one outside it) and a screen capture round it shows at least one of them against the background at a contrast ratio of 1.5 or
        /// more (`bullet` events with the ratio, rims and background); (2) at least two parallax layers are in the scene (parallax entries + the
        /// ambience's depth rows and weather kinds: `layers` events).
        /// </summary>
        public const string ReadabilityId = "readability";
        const float ReadabilitySeconds = 600f;
        const int ReadabilityFirstLevel = 1, ReadabilityLastLevel = 5;
        const float ReadabilityMinContrast = 1.5f;

        sealed partial class Session
        {
            int readPassed, readFailed;

            void ReadCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    readPassed++;
                    Run.Event("check", $"readability {beat}: ok ({detail})");
                }
                else
                {
                    readFailed++;
                    Run.Event("check", $"readability {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"readability {beat}: {detail}");
                }
            }

            static float Luminance(Color c)
            {
                float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
                return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
            }

            static float ContrastRatio(Color a, Color b)
            {
                float la = Luminance(a), lb = Luminance(b);
                return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
            }

            /// <summary>Captures the screen and measures every hostile shot's rim against the pixels round it.</summary>
            IEnumerator ReadabilitySample(int level, List<float> ratios, int[] shotsSeen)
            {
                yield return new WaitForEndOfFrame();
                var world = GameplayWorld.Instance;
                var cam = world != null ? world.Camera : null;
                if (cam == null)
                    yield break;
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    int measured = 0;
                    foreach (var e in world.Live)
                    {
                        if (measured >= 6)
                            break;
                        if (e == null || !e.IsAlive || e.Team != Team.Enemy || !e.TryGetComponent<BulletReadability>(out var r) || !r.RimShown || r.Body == null)
                            continue;
                        shotsSeen[0]++;
                        shotsSeen[1] += r.OuterShown ? 1 : 0;
                        var bounds = r.Body.bounds;
                        Vector3 c = cam.WorldToScreenPoint(bounds.center);
                        float radius = Mathf.Max(3f, Mathf.Abs(cam.WorldToScreenPoint(bounds.max).x - c.x)) * 1.7f + 3f;
                        if (c.x < radius || c.y < radius || c.x > shot.width - radius || c.y > shot.height - radius)
                            continue;
                        float rr = 0, gg = 0, bb = 0;
                        const int n = 8;
                        for (int i = 0; i < n; i++)
                        {
                            float a = i * Mathf.PI * 2f / n;
                            var px = shot.GetPixel(Mathf.RoundToInt(c.x + Mathf.Cos(a) * radius), Mathf.RoundToInt(c.y + Mathf.Sin(a) * radius));
                            rr += px.r; gg += px.g; bb += px.b;
                        }
                        var bg = new Color(rr / n, gg / n, bb / n, 1f);
                        var inner = r.RimColor;
                        var outer = r.OuterColor;
                        float ratio = Mathf.Max(ContrastRatio(new Color(inner.r, inner.g, inner.b, 1f), bg), ContrastRatio(new Color(outer.r, outer.g, outer.b, 1f), bg));
                        ratios.Add(ratio);
                        measured++;
                        Run.Event("bullet", $"level {level}: {e.name}: rims #{ColorUtility.ToHtmlStringRGB(inner)} / #{ColorUtility.ToHtmlStringRGB(outer)} against background #{ColorUtility.ToHtmlStringRGB(bg)}: contrast {ratio:0.0}:1");
                    }
                }
                finally
                {
                    Object.Destroy(shot);
                }
            }

            public IEnumerator ReadabilityRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                for (int level = ReadabilityFirstLevel; level <= ReadabilityLastLevel; level++)
                {
                    if (Run.Done) yield break;
                    Prepare(level, level == ReadabilityFirstLevel);
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} to start");
                    if (Run.Done) yield break;

                    var ratios = new List<float>();
                    var seen = new int[2];
                    int layers = 0;
                    int sampleAt = 0;
                    float[] marks = { 8f, 14f, 20f, 26f, 32f, 38f };
                    yield return PlayLevel(level, 0f, during: now =>
                    {
                        var world = GameplayWorld.Instance;
                        var amb = world != null ? world.Ambience : null;
                        var par = ParallaxController.Current;
                        int count = (par != null ? par.Entries.Count : 0) + (amb != null ? amb.DepthLayers : 0);
                        if (amb != null)
                            foreach (var k in amb.ActiveKinds)
                                count++;
                        layers = Mathf.Max(layers, count);
                        if (sampleAt < marks.Length && now >= marks[sampleAt])
                        {
                            sampleAt++;
                            return ReadabilitySample(level, ratios, seen);
                        }
                        return null;
                    }, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 40f);
                    input.ReleaseAll();
                    if (Run.Done) yield break;

                    Run.Event("layers", $"level {level}: {layers} parallax layers (entries + depth rows + weather kinds)");
                    ReadCheck($"parallax layers (level {level})", layers >= 2, $"{layers} layers");
                    ReadCheck($"baseline rims on hostile shots (level {level})", seen[0] == 0 || seen[1] == seen[0], $"{seen[0]} shots seen, {seen[1]} with both rims");
                    if (ratios.Count > 0)
                    {
                        ratios.Sort();
                        float median = ratios[ratios.Count / 2];
                        Run.Metric($"readability.level{level}.contrast", median);
                        ReadCheck($"bullets contrast with the background (level {level})", median >= ReadabilityMinContrast, $"median {median:0.0}:1 over {ratios.Count} shots, floor {ReadabilityMinContrast}:1");
                    }
                    else
                        Run.Event("check", $"readability bullets contrast (level {level}): no shot on screen to measure in 40 s (not counted)");
                    Run.Progress((level - ReadabilityFirstLevel + 1f) / (ReadabilityLastLevel - ReadabilityFirstLevel + 1f));
                }
                Run.Metric("readability.passed", readPassed);
                Run.Metric("readability.failed", readFailed);
                End(readFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
