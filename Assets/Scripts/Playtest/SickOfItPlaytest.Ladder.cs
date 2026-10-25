using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The ladder scenario: the scene content/scenes/ladderTest.json on Level 2's stage (setup shortcut).</summary>
        public const string LadderId = "ladder";
        public const string LadderScene = "ladderTest";
        public const int LadderLevel = 2;
        const float LadderSeconds = 150f;

        sealed partial class Session
        {
            int ladderPassed, ladderFailed;

            /// <summary>One frame of the hero: the game clock, where he is, whether he holds the ladder.</summary>
            struct LadderSample
            {
                public float time;
                public Vector2 position;
                public bool climbing;
            }

            readonly List<LadderSample> ladderSamples = new List<LadderSample>();
            readonly List<float> ladderShots = new List<float>();
            float ladderLastShot = -1f;
            bool ladderSawMovePose, ladderSawIdlePose;

            void LadderCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    ladderPassed++;
                    Run.Event("check", $"ladder {beat}: ok ({detail})");
                }
                else
                {
                    ladderFailed++;
                    Run.Event("check", $"ladder {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"ladder {beat}: {detail}");
                }
            }

            void LadderTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "floorkit" && e.Subject.StartsWith("ladder"))
                    Run.Event("ladder", $"{e.Subject} at {e.Time:0.00} (value {e.Value})");
            }

            public IEnumerator LadderRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(LadderScene) || !content.Entities.ContainsKey("ladder"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"ladder: the scene \"{LadderScene}\" or the entity \"ladder\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                Run.Event("shortcut", $"level {LadderLevel}'s stage with the test floor {LadderScene} loaded directly");
                LevelRuntime.PendingScene = LadderScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(LadderLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(LadderLevel), 60f, "the ladder floor to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                GameplayTelemetry.Recorded += LadderTelemetry;
                try
                {
                    yield return LadderSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= LadderTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("ladder.passed", ladderPassed);
                Run.Metric("ladder.failed", ladderFailed);
                Run.Progress(1f);
                End(ladderFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>Waits 'seconds' of real time, noting the hero every frame (the clock, his place, his hold) and every shot he fires.</summary>
            IEnumerator LadderFor(float seconds)
            {
                var world = GameplayWorld.Instance;
                var controller = world.PlayerController;
                var clock = GameClock.Instance;
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
            }

            void LadderNote(GameplayWorld world, PlayerController controller, GameClock clock)
            {
                if (clock.IsRewinding)
                    return;
                var s = new LadderSample { time = clock.Time, position = world.Player.Position, climbing = controller.IsClimbing };
                // A rewind rewrote the timeline: drop the samples of the future it undid.
                while (ladderSamples.Count > 0 && ladderSamples[ladderSamples.Count - 1].time >= s.time)
                    ladderSamples.RemoveAt(ladderSamples.Count - 1);
                ladderSamples.Add(s);
                if (controller.IsClimbing)
                {
                    ladderSawMovePose |= controller.ClimbPose == "Climb";
                    ladderSawIdlePose |= controller.ClimbPose == "ClimbIdle";
                }
                if (controller.LastFireTime > ladderLastShot)
                {
                    ladderLastShot = controller.LastFireTime;
                    ladderShots.Add(ladderLastShot);
                }
            }

            /// <summary>The recorded sample nearest a clock time (what a rewind to that time must put back).</summary>
            LadderSample LadderAt(float time)
            {
                LadderSample best = default;
                float bestGap = float.MaxValue;
                foreach (var s in ladderSamples)
                {
                    float gap = Mathf.Abs(s.time - time);
                    if (gap < bestGap)
                    {
                        bestGap = gap;
                        best = s;
                    }
                }
                return best;
            }

            /// <summary>Holds R until the game clock is back at 'target' (juice topped up: a setup shortcut). Returns the clock's time.</summary>
            IEnumerator LadderRewindTo(float target)
            {
                var clock = GameClock.Instance;
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(10f);
                Run.Event("rewind", $"hold R back to clock {target:0.00} (now {clock.Time:0.00})");
                input.SetKey(Key.R, true);
                float giveUp = Time.realtimeSinceStartup + 8f;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && clock.Time > target)
                    yield return null;
                input.SetKey(Key.R, false);
                yield return WaitReal(0.35f);
            }

            /// <summary>The smallest gap between consecutive shots in 'shots' from index 'from' (0 when fewer than two).</summary>
            static float LadderMinGap(List<float> shots, int from)
            {
                float min = float.MaxValue;
                for (int i = from + 1; i < shots.Count; i++)
                    min = Mathf.Min(min, shots[i] - shots[i - 1]);
                return min == float.MaxValue ? 0f : min;
            }

            IEnumerator LadderSteps()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                var hero = world.Player;
                var controller = world.PlayerController;
                yield return Run.Until(() => world.FindById("ladder1") != null && controller.IsGrounded, 8f, "the ladder floor to settle");
                var ladder = world.FindById("ladder1");
                if (ladder == null)
                {
                    LadderCheck("the ladder spawns", false, "no live entity with id ladder1");
                    yield break;
                }
                var zone = ladder.MainCollider.bounds;
                float bottom = zone.min.y, top = zone.max.y, ladderX = zone.center.x;
                Run.Event("ladder", $"ladder1 at x={ladderX:0.00}, rungs from y={bottom:0.00} to y={top:0.00}");
                LadderCheck("the ladder spawns", top - bottom > 3f, $"a {top - bottom:0.0} tall ladder");
                yield return LadderFor(0.3f);

                // 1. Walk to its foot with D (real input).
                input.SetKey(Key.D, hero.Position.x < ladderX);
                input.SetKey(Key.A, hero.Position.x > ladderX);
                float giveUp = Time.realtimeSinceStartup + 6f;
                while (!Run.Done && Mathf.Abs(hero.Position.x - ladderX) > 0.2f && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
                yield return LadderFor(0.3f);
                LadderCheck("stands at the foot", controller.IsGrounded && !controller.IsClimbing && Mathf.Abs(hero.Position.x - ladderX) < 0.6f,
                    $"hero x {hero.Position.x:0.00}, ladder x {ladderX:0.00}, grounded {controller.IsGrounded}");

                // 2. Shooting on the ground, for the rate to compare with the climb's.
                int groundFrom = ladderShots.Count;
                input.SetAim(Vector2.right);
                yield return LadderFor(1.6f);
                input.SetAim(Vector2.zero);
                float groundGap = LadderMinGap(ladderShots, groundFrom);
                Run.Event("ladder", $"on the ground: {ladderShots.Count - groundFrom} shot(s), shortest gap {groundGap:0.00} s");
                Run.Progress(0.15f);

                // 3. Up grabs on.
                input.SetKey(Key.W, true);
                giveUp = Time.realtimeSinceStartup + 2f;
                while (!Run.Done && !controller.IsClimbing && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                input.SetKey(Key.W, false);
                LadderCheck("up grabs on", controller.IsClimbing, controller.IsClimbing ? "pressing W at the foot grabbed the ladder" : "W did not grab the ladder");
                if (!controller.IsClimbing)
                    yield break;

                // 4. Held still: no gravity.
                yield return LadderFor(0.2f);
                float restY = hero.Position.y;
                yield return LadderFor(0.7f);
                LadderCheck("no gravity on the rungs", controller.IsClimbing && Mathf.Abs(hero.Position.y - restY) < 0.05f,
                    $"hanging still for 0.7 s: y {restY:0.00} -> {hero.Position.y:0.00}, pose {controller.ClimbPose}");

                // 5. Up climbs.
                float y0 = hero.Position.y;
                input.SetKey(Key.W, true);
                yield return LadderFor(0.7f);
                input.SetKey(Key.W, false);
                float climbedUp = hero.Position.y - y0;
                LadderCheck("climbs up", controller.IsClimbing && climbedUp > 1.0f, $"W for 0.7 s: +{climbedUp:0.00}");
                LadderCheck("sticks to the middle", Mathf.Abs(hero.Position.x - ladderX) < 0.1f, $"hero x {hero.Position.x:0.00}, ladder x {ladderX:0.00}");

                // 6. Down descends.
                y0 = hero.Position.y;
                input.SetKey(Key.S, true);
                yield return LadderFor(0.4f);
                input.SetKey(Key.S, false);
                float climbedDown = y0 - hero.Position.y;
                LadderCheck("climbs down", controller.IsClimbing && climbedDown > 0.6f, $"S for 0.4 s: -{climbedDown:0.00}");
                LadderCheck("climb poses play", ladderSawMovePose && ladderSawIdlePose, $"Climb seen {ladderSawMovePose}, ClimbIdle seen {ladderSawIdlePose}");
                Run.Progress(0.4f);

                // 7. Shooting while climbing is slower.
                int climbFrom = ladderShots.Count;
                input.SetAim(Vector2.right);
                yield return LadderFor(1.6f);
                input.SetAim(Vector2.zero);
                float climbGap = LadderMinGap(ladderShots, climbFrom);
                int climbShots = ladderShots.Count - climbFrom;
                Run.Event("ladder", $"on the ladder: {climbShots} shot(s), shortest gap {climbGap:0.00} s");
                LadderCheck("shoots while climbing", climbShots >= 1 && controller.IsClimbing, $"{climbShots} shot(s) fired and still on the ladder");
                LadderCheck("shoots slower while climbing", groundGap <= 0f || climbShots < 2 || climbGap > groundGap * 1.4f,
                    $"shortest gap {climbGap:0.00} s on the ladder vs {groundGap:0.00} s on the ground");

                // 8. Rewind during the climb: back to a moment halfway up the rungs.
                input.SetKey(Key.W, true);
                giveUp = Time.realtimeSinceStartup + 4f;
                while (!Run.Done && hero.Position.y < bottom + 2.3f && controller.IsClimbing && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                input.SetKey(Key.W, false);
                yield return LadderFor(0.2f);
                float midTime = clock.Time;
                input.SetKey(Key.W, true);
                yield return LadderFor(0.5f);
                input.SetKey(Key.W, false);
                yield return LadderFor(0.2f);
                var want = LadderAt(midTime);
                yield return LadderRewindTo(midTime);
                float miss = Vector2.Distance(hero.Position, want.position);
                LadderCheck("rewind in the climb restores his place", miss < 0.3f, $"back at clock {clock.Time:0.00} (asked {midTime:0.00}): {miss:0.00} from where he was ({want.position.x:0.00}, {want.position.y:0.00})");
                LadderCheck("rewind in the climb restores the hold", want.climbing && controller.IsClimbing, $"was climbing {want.climbing}, climbing now {controller.IsClimbing}");
                float backY = hero.Position.y;
                yield return LadderFor(0.6f);
                LadderCheck("he keeps hanging after the rewind", controller.IsClimbing && Mathf.Abs(hero.Position.y - backY) < 0.08f, $"0.6 s later y {backY:0.00} -> {hero.Position.y:0.00}");
                Run.Progress(0.6f);

                // 9. Jump off.
                input.SetKey(Key.W, true);
                yield return LadderFor(0.3f);
                input.SetKey(Key.W, false);
                float jumpFrom = hero.Position.y;
                yield return Tap(Key.Space);
                float peak = jumpFrom;
                float airUntil = Time.realtimeSinceStartup + 0.5f;
                while (!Run.Done && Time.realtimeSinceStartup < airUntil)
                {
                    LadderNote(world, controller, clock);
                    peak = Mathf.Max(peak, hero.Position.y);
                    yield return null;
                }
                LadderCheck("jump lets go", !controller.IsClimbing && peak > jumpFrom + 0.3f, $"Space on the rungs: climbing {controller.IsClimbing}, rose {peak - jumpFrom:0.00}");
                giveUp = Time.realtimeSinceStartup + 4f;
                while (!Run.Done && !controller.IsGrounded && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                yield return LadderFor(0.4f);
                LadderCheck("lands after the jump", controller.IsGrounded && hero.IsAlive, $"grounded {controller.IsGrounded} at ({hero.Position.x:0.00}, {hero.Position.y:0.00})");
                Run.Progress(0.75f);

                // 10. Back on: down from the top of the ladder if he landed on the shelf, up from its foot if not. Then climb off the top.
                bool onShelf = controller.BodyCollider.bounds.min.y > top - 0.3f;
                input.SetKey(onShelf ? Key.S : Key.W, true);
                giveUp = Time.realtimeSinceStartup + 2f;
                while (!Run.Done && !controller.IsClimbing && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                input.SetKey(Key.S, false);
                input.SetKey(Key.W, false);
                if (!controller.IsClimbing && !onShelf)
                {
                    // He landed beside it: step to it first.
                    input.SetKey(hero.Position.x < ladderX ? Key.D : Key.A, true);
                    giveUp = Time.realtimeSinceStartup + 3f;
                    while (!Run.Done && Mathf.Abs(hero.Position.x - ladderX) > 0.3f && Time.realtimeSinceStartup < giveUp)
                        yield return null;
                    input.SetKey(Key.D, false);
                    input.SetKey(Key.A, false);
                    input.SetKey(Key.W, true);
                    giveUp = Time.realtimeSinceStartup + 2f;
                    while (!Run.Done && !controller.IsClimbing && Time.realtimeSinceStartup < giveUp)
                    {
                        LadderNote(world, controller, clock);
                        yield return null;
                    }
                    input.SetKey(Key.W, false);
                }
                LadderCheck("grabs on again", controller.IsClimbing, onShelf ? "S on the top of the ladder took hold" : "W at the foot took hold");
                if (!controller.IsClimbing)
                    yield break;
                input.SetKey(Key.W, true);
                giveUp = Time.realtimeSinceStartup + 8f;
                while (!Run.Done && controller.IsClimbing && Time.realtimeSinceStartup < giveUp)
                {
                    LadderNote(world, controller, clock);
                    yield return null;
                }
                input.SetKey(Key.W, false);
                yield return LadderFor(0.5f);
                float feet = controller.BodyCollider.bounds.min.y;
                LadderCheck("exits at the top", !controller.IsClimbing && controller.IsGrounded && Mathf.Abs(feet - top) < 0.2f,
                    $"off the top: climbing {controller.IsClimbing}, grounded {controller.IsGrounded}, feet y {feet:0.00} (top {top:0.00})");
                Run.Progress(0.85f);

                // 11. Rewind after the climb: first to standing on the shelf, then back into the climb itself.
                float standTime = clock.Time;
                yield return LadderFor(1.0f);
                var standing = LadderAt(standTime);
                yield return LadderRewindTo(standTime);
                miss = Vector2.Distance(hero.Position, standing.position);
                LadderCheck("rewind after the climb restores his place", miss < 0.3f && !controller.IsClimbing && controller.IsGrounded,
                    $"back at clock {clock.Time:0.00} (asked {standTime:0.00}): {miss:0.00} from where he stood, climbing {controller.IsClimbing}, grounded {controller.IsGrounded}");

                float lastClimb = -1f;
                foreach (var s in ladderSamples)
                {
                    if (s.climbing && s.time < standTime - 0.2f)
                        lastClimb = s.time;
                }
                if (lastClimb > 0f)
                {
                    float target = lastClimb - 0.1f;
                    var inClimb = LadderAt(target);
                    yield return LadderRewindTo(target);
                    miss = Vector2.Distance(hero.Position, inClimb.position);
                    LadderCheck("rewind back into the climb restores the hold", miss < 0.3f && controller.IsClimbing == inClimb.climbing && inClimb.climbing,
                        $"back at clock {clock.Time:0.00} (asked {target:0.00}): {miss:0.00} from the rung he was on, climbing {controller.IsClimbing} (was {inClimb.climbing})");
                    float rungY = hero.Position.y;
                    yield return LadderFor(0.6f);
                    LadderCheck("and he hangs there", controller.IsClimbing && Mathf.Abs(hero.Position.y - rungY) < 0.08f, $"0.6 s later y {rungY:0.00} -> {hero.Position.y:0.00}");
                }
                else
                {
                    LadderCheck("rewind back into the climb restores the hold", false, "no sampled climbing moment before the exit to rewind to");
                }
            }
        }
    }
}
