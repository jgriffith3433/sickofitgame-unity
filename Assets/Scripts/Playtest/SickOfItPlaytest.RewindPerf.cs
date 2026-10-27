using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// rewind-perf on a slow device: the same run with every frame made <see cref="SlowCpuFactor"/> times longer
        /// (a spin at the end of the frame, standing in for the browser's 4x CPU throttle, which the playtest can't set).
        /// Rewinding must stay within 1.5x of playing, allocate nothing, and put objects back where they were.
        /// </summary>
        public const string RewindPerfSlowId = "rewind-perf-slow";
        public const float SlowCpuFactor = 4f;

        /// <summary>Spins at the end of each frame for (factor - 1) times the frame's own work.</summary>
        [DefaultExecutionOrder(32000)]
        sealed class CpuThrottle : MonoBehaviour
        {
            public float Factor = 1f;
            float spinMs, ownMs = 8f;

            void LateUpdate()
            {
                if (Factor <= 1f)
                    return;
                // What the frame cost without the spin we added last time (smoothed: a hitch must not feed itself).
                float own = Mathf.Max(0f, Time.unscaledDeltaTime * 1000f - spinMs);
                ownMs = Mathf.Lerp(ownMs, Mathf.Min(own, 40f), 0.1f);
                spinMs = Mathf.Min(120f, ownMs * (Factor - 1f));
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed.TotalMilliseconds < spinMs) { }
            }
        }

        sealed partial class Session
        {
            CpuThrottle rewindThrottle;
            float rewindPerfCpuFactor = 1f;

            // Where things stood in the last seconds of play, to compare with where a rewind puts them back.
            readonly List<(float t, Vector2 hero)> rewindHeroSamples = new List<(float, Vector2)>();
            readonly List<(float t, Dictionary<Entity, Vector2> places)> rewindPlaceSamples = new List<(float, Dictionary<Entity, Vector2>)>();
            float rewindPlacesAt = -1f;

            void StartRewindThrottle(float factor)
            {
                rewindPerfCpuFactor = factor;
                if (factor <= 1f)
                    return;
                var go = new GameObject("PlaytestCpuThrottle") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                rewindThrottle = go.AddComponent<CpuThrottle>();
                rewindThrottle.Factor = factor;
                Run.Event("rewind-perf", $"CPU throttled {factor:0}x: every frame is held to {factor:0}x its own work");
            }

            void StopRewindThrottle()
            {
                if (rewindThrottle != null)
                    Object.Destroy(rewindThrottle.gameObject);
                rewindThrottle = null;
            }

            /// <summary>Called every frame the level plays (not rewinding): remembers the hero's place each frame, the others' every 0.25 s.</summary>
            void SampleRewindPlaces(GameplayWorld world)
            {
                var clock = SickOfIt.Core.GameClock.Instance;
                if (world == null || world.Player == null || clock == null || clock.IsRewinding)
                    return;
                float t = clock.Time;
                rewindHeroSamples.Add((t, world.Player.Position));
                if (rewindHeroSamples.Count > 1500)
                    rewindHeroSamples.RemoveRange(0, 500);
                if (t - rewindPlacesAt < 0.25f && t >= rewindPlacesAt)
                    return;
                rewindPlacesAt = t;
                var places = new Dictionary<Entity, Vector2>();
                foreach (var e in world.Live)
                    if (e != null && e != world.Player && e.IsAlive)
                        places[e] = e.Position;
                rewindPlaceSamples.Add((t, places));
                if (rewindPlaceSamples.Count > 120)
                    rewindPlaceSamples.RemoveRange(0, 40);
            }

            /// <summary>After a rewind: the hero and the entities still alive stand where they stood at that game time.</summary>
            void CheckRewindPlaces(GameplayWorld world, int rewindNo)
            {
                var clock = SickOfIt.Core.GameClock.Instance;
                if (world == null || world.Player == null || clock == null || rewindHeroSamples.Count == 0)
                    return;
                float t = clock.Time;
                (float t, Vector2 hero) best = rewindHeroSamples[0];
                foreach (var s in rewindHeroSamples)
                    if (Mathf.Abs(s.t - t) < Mathf.Abs(best.t - t))
                        best = s;
                float heroOff = Vector2.Distance(best.hero, world.Player.Position);
                RewindPerfCheck($"same place hero #{rewindNo}", Mathf.Abs(best.t - t) <= 0.2f && heroOff <= 0.6f,
                    $"back at game time {t:0.00} s the hero is {heroOff:0.00} units from where he stood at {best.t:0.00} s");

                (float t, Dictionary<Entity, Vector2> places)? bucket = null;
                foreach (var s in rewindPlaceSamples)
                    if (bucket == null || Mathf.Abs(s.t - t) < Mathf.Abs(bucket.Value.t - t))
                        bucket = s;
                if (bucket == null || Mathf.Abs(bucket.Value.t - t) > 0.2f)
                {
                    Run.Event("rewind-perf", $"rewind {rewindNo}: no sample of the others near {t:0.00} s (it went back past the recorded seconds)");
                    return;
                }
                int compared = 0, near = 0;
                float worst = 0f;
                foreach (var pair in bucket.Value.places)
                {
                    if (pair.Key == null || !pair.Key.IsAlive)
                        continue;
                    compared++;
                    float off = Vector2.Distance(pair.Value, pair.Key.Position);
                    if (off > worst) worst = off;
                    if (off <= 1.5f) near++;
                }
                if (compared > 0)
                    RewindPerfCheck($"same places #{rewindNo}", near >= compared * 0.85f,
                        $"{near} of {compared} entities within 1.5 units of where they stood at {bucket.Value.t:0.00} s (worst {worst:0.00})");
            }

            /// <summary>The allocation and slow-device checks, after the frame-time ones.</summary>
            void CheckRewindPerfSlow(RewindPerf.Stats forward, RewindPerf.Stats rewinding)
            {
                if (rewinding.Frames > 0)
                {
                    Run.Metric("rewindPerf.rewind.workAllocBytes", rewinding.WorkAllocBytes);
                    Run.Metric("rewindPerf.rewind.frameAllocBytes", rewinding.FrameAllocBytes);
                    Run.Metric("rewindPerf.rewind.workAllocPerFrame", rewinding.AverageWorkAllocBytes);
                    Run.Metric("rewindPerf.forward.workAllocPerFrame", forward.AverageWorkAllocBytes);
                    RewindPerfCheck("no allocation", rewinding.WorkAllocBytes <= 1024,
                        $"rewinding allocated {rewinding.WorkAllocBytes} B in the timeline over {rewinding.Frames} frames ({rewinding.WorstWorkAllocBytes} B at most in one), " +
                        $"{rewinding.FrameAllocBytes} B in the frames altogether (playing: {forward.AverageFrameAllocBytes:0} B a frame)");
                }
                var timeline = RewindTimeline.Instance;
                if (timeline != null)
                    Run.Event("rewind-perf", $"records every {timeline.EffectiveRecordInterval:0.00} s ({(timeline.LowEnd ? "slow-device interval" : "normal interval")})");
                if (rewindPerfCpuFactor > 1f && rewinding.Frames > 0 && forward.Frames > 0)
                {
                    double allowed = forward.AverageMs * 1.5;
                    Run.Metric("frameTime.throttled.playingAvgMs", (float)forward.AverageMs);
                    Run.Metric("frameTime.throttled.rewindingAvgMs", (float)rewinding.AverageMs);
                    RewindPerfCheck($"throttled {rewindPerfCpuFactor:0}x", rewinding.AverageMs <= allowed,
                        $"at {rewindPerfCpuFactor:0}x CPU rewinding averages {rewinding.AverageMs:0.0} ms a frame, playing {forward.AverageMs:0.0} ms (allowed 1.5x = {allowed:0.0}); " +
                        $"ratio {rewinding.AverageMs / forward.AverageMs:0.00}");
                }
            }
        }
    }
}
