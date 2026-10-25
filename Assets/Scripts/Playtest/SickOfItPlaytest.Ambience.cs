using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The ambience scenario (Docs/GameplayCookbook.md "Weather and life"): every Circle that has an `ambience` gets its first travelling
        /// floor loaded directly (a setup shortcut) and played by the bot. Parallax: every ParallaxController entry moves at its own factor of
        /// the camera's travel (measured over a stretch where the camera moved). Each Circle has its own weather and life (kinds seen, a
        /// silhouette set no other Circle shares). Frame time: the same floor with the ambience on, off, on again; on must stay within budget.
        /// </summary>
        public const string AmbienceId = "ambience";
        const float AmbienceSeconds = 420f;
        // Game-time windows of one floor: warm-up, ambience on, off (baseline), on again.
        const float AmbWarm = 3f, AmbOnEnd = 15f, AmbOffEnd = 25f, AmbEnd = 35f, AmbLifeLimit = 70f;
        // Budget: on may cost at most this much over off, relative and absolute (a WebGL frame at 60 fps is 16.7 ms).
        const float AmbBudgetRatio = 1.2f, AmbBudgetMs = 2f, AmbWorstMs = 120f;

        sealed partial class Session
        {
            int ambPassed, ambFailed;

            void AmbCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    ambPassed++;
                    Run.Event("check", $"ambience {beat}: ok ({detail})");
                }
                else
                {
                    ambFailed++;
                    Run.Event("check", $"ambience {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"ambience {beat}: {detail}");
                }
            }

            sealed class AmbFrames
            {
                public double Sum;
                public int Count;
                public float Worst;
                public void Add(float seconds)
                {
                    Sum += seconds;
                    Count++;
                    if (seconds > Worst)
                        Worst = seconds;
                }
                public float AverageMs => Count > 0 ? (float)(Sum / Count) * 1000f : 0f;
            }

            /// <summary>The first level of a Circle that travels (so the camera moves and the parallax can be measured).</summary>
            static int AmbienceLevelOf(string circleId)
            {
                var definition = GameDefinition.Instance;
                var content = GameContent.Current;
                if (definition == null || content == null)
                    return 0;
                for (int n = 1; n <= definition.TotalLevels; n++)
                {
                    var config = definition.GetLevel(n);
                    if (config != null && config.levelName != null && content.LevelScripts.TryGetValue(config.levelName, out var script) && script != null
                        && script.floors != null && script.floors.circle == circleId && script.world.camera == CameraMode.Travel)
                        return n;
                }
                return 0;
            }

            static string AmbienceSignature(AmbienceDef def)
            {
                var kinds = new SortedSet<string>();
                foreach (var w in def.weather)
                    if (w != null)
                        kinds.Add("w:" + w.kind);
                foreach (var l in def.life)
                    if (l != null)
                        kinds.Add("l:" + l.behavior + "/" + l.shape);
                foreach (var d in def.depth)
                    if (d != null)
                        kinds.Add("d:" + d.shape);
                return string.Join(",", kinds);
            }

            public IEnumerator AmbienceRun()
            {
                var content = GameContent.Current;
                if (content == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "ambience: no game content loaded");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                var circles = new List<KeyValuePair<string, CircleDef>>();
                foreach (var pair in content.Circles)
                    if (pair.Value != null && pair.Value.ambience != null && !pair.Value.ambience.IsEmpty)
                        circles.Add(pair);
                circles.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                AmbCheck("circles with an ambience", circles.Count >= 3, string.Join(", ", circles.ConvertAll(c => c.Key)));

                // Each Circle's own air: no two share the same set of weather, life and silhouettes.
                var signatures = new Dictionary<string, string>();
                foreach (var pair in circles)
                {
                    string sig = AmbienceSignature(pair.Value.ambience);
                    signatures.TryGetValue(sig, out string other);
                    AmbCheck("own weather and life: " + pair.Key, other == null, other == null ? sig : "same as " + other);
                    signatures[sig] = pair.Key;
                }

                yield return ToMainMenu();
                if (Run.Done) yield break;

                int played = 0;
                float onTotal = 0f, offTotal = 0f;
                foreach (var pair in circles)
                {
                    if (Run.Done) yield break;
                    string id = pair.Key;
                    var def = pair.Value.ambience;
                    int level = AmbienceLevelOf(id);
                    if (level <= 0)
                    {
                        AmbCheck("a travelling floor in " + id, false, "no level uses this Circle with a travelling camera");
                        continue;
                    }

                    Prepare(level, true);
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} ({id}) to start");
                    if (Run.Done) yield break;

                    var on1 = new AmbFrames();
                    var off = new AmbFrames();
                    var on2 = new AmbFrames();
                    var start = new Dictionary<string, Vector2>();
                    var end = new Dictionary<string, Vector2>();
                    Vector2 camStart = default, camEnd = default;
                    bool startTaken = false, endTaken = false, disabled = false;
                    int maxBits = 0, maxKinds = 0, maxLife = 0;
                    var kindsSeen = new HashSet<WeatherKind>();
                    var ambience = GameplayWorld.Instance != null ? GameplayWorld.Instance.Ambience : null;
                    AmbCheck("built on the floor: " + id, ambience != null, ambience != null ? $"level {level}, {ambience.BitCount} bits, {ambience.DepthLayers} depth rows with {ambience.DepthShapes} shapes" : $"level {level} has no ambience");
                    if (ambience == null)
                        continue;

                    CircleAmbience.Enabled = true;
                    try
                    {
                        yield return PlayLevel(level, 0f, during: now =>
                        {
                            var world = GameplayWorld.Instance;
                            var amb = world != null ? world.Ambience : null;
                            var parallax = ParallaxController.Current;
                            float dt = Time.unscaledDeltaTime;
                            if (now >= AmbOnEnd && now < AmbOffEnd && CircleAmbience.Enabled)
                            {
                                CircleAmbience.Enabled = false;
                                disabled = true;
                                Run.Event("ambience", $"{id}: switched off at {now:0.0} s (the baseline)");
                            }
                            else if (now >= AmbOffEnd && !CircleAmbience.Enabled)
                            {
                                CircleAmbience.Enabled = true;
                                Run.Event("ambience", $"{id}: switched on again at {now:0.0} s");
                            }
                            if (now >= AmbWarm && dt > 0f && dt < 1f)
                            {
                                if (now < AmbOnEnd) on1.Add(dt);
                                else if (now < AmbOffEnd) off.Add(dt);
                                else on2.Add(dt);
                            }
                            if (amb != null && CircleAmbience.Enabled)
                            {
                                maxBits = Mathf.Max(maxBits, amb.ActiveBits);
                                maxLife = Mathf.Max(maxLife, amb.ActiveLife);
                                int kinds = 0;
                                foreach (var k in amb.ActiveKinds)
                                {
                                    kinds++;
                                    kindsSeen.Add(k);
                                }
                                maxKinds = Mathf.Max(maxKinds, kinds);
                            }
                            // The parallax: where every entry and the camera stand 5 s in, and 8 s later.
                            if (parallax != null && !startTaken && now >= 5f)
                            {
                                startTaken = true;
                                camStart = parallax.CameraPosition;
                                foreach (var e in parallax.Entries)
                                    start[e.Name] = e.Position;
                            }
                            else if (parallax != null && startTaken && !endTaken && now >= 13f)
                            {
                                endTaken = true;
                                camEnd = parallax.CameraPosition;
                                foreach (var e in parallax.Entries)
                                    end[e.Name] = e.Position;
                            }
                            return null;
                        }, until: () => GameClock.Instance != null && GameClock.Instance.Time >= AmbEnd
                                        && ((ambience != null && ambience.LifeAppearances > 0) || GameClock.Instance.Time >= AmbLifeLimit));
                    }
                    finally
                    {
                        CircleAmbience.Enabled = true;
                        input.ReleaseAll();
                    }
                    if (Run.Done) yield break;

                    // 1. Parallax layers move at their own factors.
                    var parallaxNow = ParallaxController.Current;
                    Vector2 camMoved = camEnd - camStart;
                    AmbCheck($"the camera travelled ({id})", startTaken && endTaken && camMoved.magnitude >= 3f, $"moved ({camMoved.x:0.0}, {camMoved.y:0.0}) in 8 s");
                    int measured = 0, wrong = 0;
                    var detail = new StringBuilder();
                    if (parallaxNow != null && endTaken)
                    {
                        foreach (var e in parallaxNow.Entries)
                        {
                            if (!start.TryGetValue(e.Name, out var a) || !end.TryGetValue(e.Name, out var b))
                                continue;
                            // A clamped layer that follows less than 1 is held to the screen by its edge; the rest must keep their factor.
                            bool clampedX = !float.IsInfinity(e.Slack.x), clampedY = !float.IsInfinity(e.Slack.y);
                            Vector2 moved = b - a;
                            float resX = moved.x - e.Factor.x * camMoved.x, resY = moved.y - e.Factor.y * camMoved.y;
                            if (e.Period.x > 0f) resX = Mathf.Repeat(resX + e.Period.x * 0.5f, e.Period.x) - e.Period.x * 0.5f;
                            if (e.Period.y > 0f) resY = Mathf.Repeat(resY + e.Period.y * 0.5f, e.Period.y) - e.Period.y * 0.5f;
                            bool ok = (clampedX || Mathf.Abs(resX) < 0.15f) && (clampedY || Mathf.Abs(resY) < 0.15f);
                            measured++;
                            if (!ok)
                                wrong++;
                            detail.Append($"{e.Name} f=({e.Factor.x:0.00},{e.Factor.y:0.00}) moved ({moved.x:0.0},{moved.y:0.0}){(ok ? "" : " WRONG")}; ");
                            Run.Event("parallax", $"{id}: {e.Name}: factor ({e.Factor.x:0.00}, {e.Factor.y:0.00}), camera moved ({camMoved.x:0.0}, {camMoved.y:0.0}), layer moved ({moved.x:0.0}, {moved.y:0.0}), off by ({resX:0.00}, {resY:0.00})");
                        }
                    }
                    AmbCheck($"parallax layers move at their own factors ({id})", measured >= def.depth.Count && measured > 0 && wrong == 0, $"{measured} measured, {wrong} wrong; {detail}");

                    // 2. Weather and life.
                    int expectedKinds = 0;
                    var configured = new HashSet<WeatherKind>();
                    foreach (var w in def.weather)
                        if (w != null && configured.Add(w.kind) && w.fromFloor <= 0)
                            expectedKinds++;
                    AmbCheck($"weather in the air ({id})", maxBits > 0 && kindsSeen.Count >= Mathf.Min(2, configured.Count) && kindsSeen.Count >= expectedKinds,
                        $"{maxBits} bits at most, kinds {string.Join("+", kindsSeen)} of {string.Join("+", configured)}");
                    AmbCheck($"weather within its cap ({id})", maxBits <= CircleAmbience.MaxBits, $"{maxBits} bits of at most {CircleAmbience.MaxBits}");
                    AmbCheck($"depth layers drawn ({id})", ambience != null && ambience.DepthLayers == def.depth.Count && ambience.DepthShapes > 0,
                        $"{(ambience != null ? ambience.DepthLayers : 0)} rows of {def.depth.Count}, {(ambience != null ? ambience.DepthShapes : 0)} shapes");
                    AmbCheck($"background life seen ({id})", ambience != null && ambience.LifeAppearances > 0,
                        $"{(ambience != null ? ambience.LifeAppearances : 0)} appearances ({(ambience != null ? string.Join("; ", ambience.LifeSeen) : "")}), {maxLife} on screen at most");
                    Run.Event("ambience", $"{id}: level {level}, bits {maxBits}, kinds {string.Join("+", kindsSeen)}, life {(ambience != null ? ambience.LifeAppearances : 0)}");

                    // 3. Frame time within budget: ambience on (before and after the baseline) against off.
                    float onMs = (on1.AverageMs * on1.Count + on2.AverageMs * on2.Count) / Mathf.Max(1, on1.Count + on2.Count);
                    float offMs = off.AverageMs;
                    float worstMs = Mathf.Max(on1.Worst, on2.Worst) * 1000f;
                    Run.Metric($"ambience.{id}.frameOnMs", onMs);
                    Run.Metric($"ambience.{id}.frameOffMs", offMs);
                    Run.Metric($"ambience.{id}.frameWorstMs", worstMs);
                    Run.Metric($"ambience.{id}.bits", maxBits);
                    onTotal += onMs;
                    offTotal += offMs;
                    played++;
                    AmbCheck($"frame time within budget ({id})", disabled && off.Count > 30 && on1.Count + on2.Count > 60 && onMs <= offMs * AmbBudgetRatio + AmbBudgetMs && worstMs < AmbWorstMs,
                        $"ambience on {onMs:0.00} ms/frame ({on1.Count + on2.Count} frames), off {offMs:0.00} ms ({off.Count} frames), worst {worstMs:0} ms; budget off x{AmbBudgetRatio} + {AmbBudgetMs} ms");
                }

                if (Run.Done)
                    yield break;
                AmbCheck("every Circle played", played == circles.Count, $"{played} of {circles.Count}");
                if (played > 0)
                {
                    Run.Metric("ambience.frameOnMs", onTotal / played);
                    Run.Metric("ambience.frameOffMs", offTotal / played);
                }
                Run.Metric("ambience.passed", ambPassed);
                Run.Metric("ambience.failed", ambFailed);
                Run.Progress(1f);
                End(ambFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
