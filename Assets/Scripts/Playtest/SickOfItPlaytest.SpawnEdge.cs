using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The spawn-edge scenario: rain, cinders and generated enemies appear outside the view (floors opened directly: a setup shortcut).</summary>
        public const string SpawnEdgeId = "spawn-edge";
        const float SpawnEdgeSeconds = 420f;

        sealed partial class Session
        {
            sealed class EdgeTrack
            {
                public Entity Entity;
                public string Id;
                public float SpawnedAt;
                public bool Seen;
            }

            int spawnEdgePassed, spawnEdgeFailed;

            void SpawnEdgeCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    spawnEdgePassed++;
                    Run.Event("check", $"spawn-edge {beat}: ok ({detail})");
                }
                else
                {
                    spawnEdgeFailed++;
                    Run.Event("check", $"spawn-edge {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"spawn-edge {beat}: {detail}");
                }
            }

            /// <summary>Plays one floor and watches every hostile spawn of the given ids (and generated ones): where it was put against the view, and how long it took to come into view.</summary>
            IEnumerator SpawnEdgeLevel(string levelName, float seconds, params string[] timed)
            {
                int n = LevelNumberOf(levelName);
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(n), 60f, levelName);
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var cam = world.Camera;
                string shape = cam != null && cam.aspect < 1f ? "portrait" : "landscape";
                Run.Event("spawn-edge", $"{levelName}: {shape}, aspect {(cam != null ? cam.aspect : 0f):0.00}, view {world.View.width:0.0}x{world.View.height:0.0}");

                var tracked = new List<EdgeTrack>();
                int spawns = 0, visibleAtSpawn = 0, beyondBarrier = 0, entered = 0, tooQuick = 0;
                float shortest = float.MaxValue;
                var timedSet = new HashSet<string>(timed);
                System.Action<Entity, Vector2, Rect> onSpawn = (entity, at, view) =>
                {
                    var def = entity.Def;
                    if (def == null || def.team != Team.Enemy || GameClock.Instance == null || GameClock.Instance.IsRewinding)
                        return;
                    if (!timedSet.Contains(def.id) && !entity.Tags.Contains(LevelDirector.GeneratedTag))
                        return;
                    spawns++;
                    if (view.Contains(at))
                    {
                        visibleAtSpawn++;
                        Run.Event("spawn-edge", $"VISIBLE at spawn: {def.id} at ({at.x:0.0},{at.y:0.0}), view x {view.xMin:0.0}..{view.xMax:0.0} y {view.yMin:0.0}..{view.yMax:0.0}");
                    }
                    // A walker (dynamic body) put beyond a Travel edge barrier (it stands 0.5 outside the view) could never get in.
                    if (def.body != null && def.body.kind == BodyKind.Dynamic && (at.x < view.xMin - 0.5f || at.x > view.xMax + 0.5f))
                    {
                        beyondBarrier++;
                        Run.Event("spawn-edge", $"WALKER BEYOND THE BARRIER: {def.id} at x {at.x:0.0}, view x {view.xMin:0.0}..{view.xMax:0.0}");
                    }
                    tracked.Add(new EdgeTrack { Entity = entity, Id = def.id, SpawnedAt = GameClock.Instance.Time });
                };
                GameplayWorld.Spawned += onSpawn;
                bool Poll()
                {
                    var view = world.View;
                    foreach (var t in tracked)
                    {
                        if (t.Seen || t.Entity == null || !t.Entity.IsAlive)
                            continue;
                        if (!view.Contains(t.Entity.Position))
                            continue;
                        t.Seen = true;
                        entered++;
                        float took = GameClock.Instance.Time - t.SpawnedAt;
                        shortest = Mathf.Min(shortest, took);
                        if (took < 1f)
                            tooQuick++;
                        if (timedSet.Contains(t.Id))
                            Run.Event("spawn-edge", $"{t.Id} reached the view after {took:0.00} s");
                    }
                    tracked.RemoveAll(t => t.Seen || t.Entity == null || !t.Entity.IsAlive);
                    return false;
                }
                yield return PlayLevel(n, seconds, null, 0f, 1f, seconds * 3f + 60f, until: Poll);
                GameplayWorld.Spawned -= onSpawn;
                if (Run.Done)
                    yield break;
                Run.Metric($"spawnEdge.{levelName}.spawns", spawns);
                Run.Metric($"spawnEdge.{levelName}.shortestToView", shortest == float.MaxValue ? -1f : shortest);
                SpawnEdgeCheck($"{levelName} ({shape}) something spawned", spawns > 0, $"{spawns} hostile spawn(s) watched");
                SpawnEdgeCheck($"{levelName} ({shape}) never visible at spawn", visibleAtSpawn == 0, $"{visibleAtSpawn} of {spawns} appeared inside the view");
                SpawnEdgeCheck($"{levelName} ({shape}) takes 1+ s to reach the view", tooQuick == 0, $"{entered} entered the view, shortest {(shortest == float.MaxValue ? 0f : shortest):0.00} s, {tooQuick} under 1 s");
                SpawnEdgeCheck($"{levelName} ({shape}) no walker behind the barriers", beyondBarrier == 0, $"{beyondBarrier} walker(s) spawned past the Travel edges");
            }

            public IEnumerator SpawnEdgeRun()
            {
                // Floor 998: the fire rain (fireball) and the cinders; Level 13: the generator's enemies of Circle IX.
                yield return SpawnEdgeLevel("Floor998", 60f, "fireball", "cinder");
                if (Run.Done)
                    yield break;
                yield return SpawnEdgeLevel("Level13", 90f);
                if (Run.Done)
                    yield break;
                Run.Metric("spawnEdge.passed", spawnEdgePassed);
                Run.Metric("spawnEdge.failed", spawnEdgeFailed);
                Run.Progress(1f);
                End(spawnEdgeFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
