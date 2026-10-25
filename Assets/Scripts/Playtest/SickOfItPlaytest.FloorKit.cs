using System;
using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The grapple hook and the air-and-path floor kit (Docs/Story.md "The Hook", Docs/LevelDesign.md "The air and the paths"): the scene
        /// content/scenes/kitFloor.json on Level 2's stage (setup shortcut), crossed start to finish through real input: a swing over a gap, a pull up
        /// to a shelf, a gust ridden over a gap, a moving platform, a collapsing floor and a walk on the ceiling.
        /// </summary>
        public const string FloorKitId = "floor-kit";
        public const string FloorKitScene = "kitFloor";
        public const int FloorKitLevel = 2;
        const float FloorKitSeconds = 360f;

        sealed partial class Session
        {
            int kitPassed, kitFailed;

            struct KitSample
            {
                public float time;
                public Vector2 position;
                public int gravity;
                public bool attached;
            }

            struct KitEvent
            {
                public float time;
                public string subject;
            }

            readonly List<KitSample> kitSamples = new List<KitSample>();
            readonly List<KitEvent> kitEvents = new List<KitEvent>();
            bool kitTimedOut;

            void KitCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    kitPassed++;
                    Run.Event("check", $"floor-kit {beat}: ok ({detail})");
                }
                else
                {
                    kitFailed++;
                    Run.Event("check", $"floor-kit {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"floor-kit {beat}: {detail}");
                }
            }

            void KitTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind != "floorkit" && e.Kind != "grapple")
                    return;
                kitEvents.Add(new KitEvent { time = e.Time, subject = e.Kind + ":" + e.Subject });
                Run.Event(e.Kind, $"{e.Subject} at {e.Time:0.00} (value {e.Value})");
            }

            public IEnumerator FloorKitRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(FloorKitScene) || !content.Entities.ContainsKey("grappleRing"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"floor-kit: the scene \"{FloorKitScene}\" or the entity \"grappleRing\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // Setup shortcut: a new game's flags, so the Hook starts unfound.
                UnlockFlags.Reset();
                // The wall-jump re-arms itself from a saved Floor 996 clear (player.json unlockWhen level:6): a save left by the moves
                // scenarios made the hero cling to the kit's ledge corners. A new game's save has none of those clears.
                for (int n = 4; n <= 6; n++)
                    PlayerPrefs.DeleteKey($"Level_{n}_Completed");
                Run.Event("shortcut", $"flags reset (the Hook unfound); level {FloorKitLevel}'s stage with the scene {FloorKitScene} loaded directly");
                LevelRuntime.PendingScene = FloorKitScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(FloorKitLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(FloorKitLevel), 60f, "the kit floor to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                GameplayTelemetry.Recorded += KitTelemetry;
                try
                {
                    yield return FloorKitSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= KitTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("floorKit.passed", kitPassed);
                Run.Metric("floorKit.failed", kitFailed);
                Run.Progress(1f);
                End(kitFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            void KitNote()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                if (world == null || world.Player == null || world.PlayerController == null || clock == null || clock.IsRewinding)
                    return;
                var s = new KitSample
                {
                    time = clock.Time, position = world.Player.Position, gravity = world.PlayerController.GravitySign,
                    attached = world.Grapple != null && world.Grapple.Attached,
                };
                // A rewind rewrote the timeline: drop the samples of the future it undid.
                while (kitSamples.Count > 0 && kitSamples[kitSamples.Count - 1].time >= s.time)
                    kitSamples.RemoveAt(kitSamples.Count - 1);
                kitSamples.Add(s);
            }

            KitSample KitAt(float time)
            {
                KitSample best = default;
                float bestGap = float.MaxValue;
                foreach (var s in kitSamples)
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

            /// <summary>Waits 'seconds' of real time, noting the hero every frame.</summary>
            IEnumerator KitFor(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    KitNote();
                    yield return null;
                }
            }

            /// <summary>Waits until 'condition' holds (kitTimedOut tells whether it ran out of 'seconds' instead), noting the hero every frame.</summary>
            IEnumerator KitUntil(Func<bool> condition, float seconds)
            {
                kitTimedOut = false;
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done)
                {
                    KitNote();
                    if (condition())
                        yield break;
                    if (Time.realtimeSinceStartup >= until)
                    {
                        kitTimedOut = true;
                        yield break;
                    }
                    yield return null;
                }
            }

            /// <summary>Walks to x with A / D (real keys) and stops.</summary>
            IEnumerator KitWalkTo(float x, float tolerance, float seconds)
            {
                var hero = GameplayWorld.Instance.Player;
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Mathf.Abs(hero.Position.x - x) > tolerance && Time.realtimeSinceStartup < until)
                {
                    input.SetKey(Key.D, hero.Position.x < x);
                    input.SetKey(Key.A, hero.Position.x > x);
                    KitNote();
                    yield return null;
                }
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
            }

            /// <summary>Holds R until the game clock is back at 'target' (juice topped up: a setup shortcut).</summary>
            IEnumerator KitRewindTo(float target)
            {
                var clock = GameClock.Instance;
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(30f);
                Run.Event("rewind", $"hold R back to clock {target:0.00} (now {clock.Time:0.00})");
                input.SetKey(Key.R, true);
                float giveUp = Time.realtimeSinceStartup + 14f;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && clock.Time > target)
                    yield return null;
                input.SetKey(Key.R, false);
                yield return WaitReal(0.4f);
            }

            bool KitSeen(string subject, float after = -1f)
            {
                foreach (var e in kitEvents)
                    if (e.subject == subject && e.time >= after)
                        return true;
                return false;
            }

            float KitFirst(string subject, float after = -1f)
            {
                foreach (var e in kitEvents)
                    if (e.subject == subject && e.time >= after)
                        return e.time;
                return -1f;
            }

            IEnumerator FloorKitSteps()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                var hero = world.Player;
                var c = world.PlayerController;
                yield return KitUntil(() => world.FindById("swingRing") != null && c.IsGrounded, 10f);
                if (kitTimedOut)
                {
                    KitCheck("the kit floor spawns", false, "no ring or the hero never landed");
                    yield break;
                }

                // ── The Hook: found, and locked until then ──────────────────────────
                yield return KitUntil(() => UnlockFlags.Has("hook.found"), 8f);
                KitCheck("the Hook is found", UnlockFlags.Has("hook.found") && c.GrappleUnlocked, "the pickup on the start ledge set hook.found");
                UnlockFlags.Reset();
                yield return KitFor(0.1f);
                bool lockedOff = !c.GrappleUnlocked;
                yield return Tap(Key.E);
                yield return KitFor(0.4f);
                KitCheck("the hook is locked until found", lockedOff && !world.Grapple.Attached, $"flags cleared (setup shortcut): unlocked {c.GrappleUnlocked}, attached {world.Grapple.Attached}");
                UnlockFlags.Set("hook.found");
                Run.Progress(0.05f);

                // ── 1. The swing over the gap ───────────────────────────────────────
                var ring = world.FindById("swingRing");
                yield return KitWalkTo(-2.85f, 0.12f, 6f);
                yield return KitFor(0.3f);
                Vector2 ringAt = ring.Position;
                float startY = hero.Position.y;
                yield return Tap(Key.E);
                yield return KitUntil(() => world.Grapple.Attached, 1.5f);
                KitCheck("grapple attaches to the anchor", world.Grapple.Attached && world.Grapple.AnchorSerial == ring.Serial && !world.Grapple.Pulling,
                    $"E aimed up and ahead from x {hero.Position.x:0.00}: attached {world.Grapple.Attached}, rope {world.Grapple.RopeLength:0.00}");
                if (!world.Grapple.Attached)
                    yield break;
                input.SetKey(Key.D, true);
                yield return KitFor(0.5f);
                float tMid = clock.Time;
                var mid = KitAt(tMid);
                yield return KitFor(0.5f);
                yield return KitRewindTo(tMid);
                float miss = Vector2.Distance(hero.Position, mid.position);
                KitCheck("rewind restores the swing", miss < 0.6f && world.Grapple.Attached,
                    $"back at clock {clock.Time:0.00} (asked {tMid:0.00}): {miss:0.00} from where he was, still on the rope {world.Grapple.Attached}");
                input.SetKey(Key.D, true);

                float minY = hero.Position.y, maxSpeed = 0f;
                float giveUp = Time.realtimeSinceStartup + 7f;
                bool released = false;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp)
                {
                    KitNote();
                    minY = Mathf.Min(minY, hero.Position.y);
                    maxSpeed = Mathf.Max(maxSpeed, world.Grapple.Velocity.magnitude);
                    if (!world.Grapple.Attached)
                        break;
                    if (hero.Position.x >= ringAt.x + 1.6f && world.Grapple.Velocity.y > 0.5f)
                    {
                        yield return Tap(Key.Space);
                        released = true;
                        break;
                    }
                    yield return null;
                }
                yield return KitUntil(() => !world.Grapple.Attached, 1f);
                KitCheck("the hero swings", minY < startY - 0.6f && maxSpeed > 3f, $"dipped {startY - minY:0.00} under his start, top speed {maxSpeed:0.0}, released with Jump {released}");
                yield return KitUntil(() => c.IsGrounded && hero.Position.x > 3.2f, 5f);
                input.SetKey(Key.D, false);
                yield return KitFor(0.3f);
                KitCheck("the swing carries him across the gap", c.IsGrounded && hero.Position.x >= 3.4f && hero.Position.x <= 6.7f && hero.Position.y > -1.6f && hero.IsAlive,
                    $"landed at ({hero.Position.x:0.00}, {hero.Position.y:0.00}) on the far ledge (gap 6.0 wide, a jump carries ~2)");
                if (!c.IsGrounded || hero.Position.x < 3.4f)
                    yield break;
                Run.Progress(0.25f);

                // ── 2. The pull up to the shelf ─────────────────────────────────────
                yield return KitWalkTo(6.05f, 0.15f, 4f);
                yield return KitFor(0.3f);
                var pullHook = world.FindById("pullHook");
                yield return Tap(Key.E);
                yield return KitUntil(() => world.Grapple.Attached, 1.5f);
                KitCheck("a pull anchor reels him in", world.Grapple.Attached && world.Grapple.Pulling && pullHook != null && world.Grapple.AnchorSerial == pullHook.Serial,
                    $"attached {world.Grapple.Attached}, pulling {world.Grapple.Pulling}");
                float pullFrom = clock.Time;
                yield return KitUntil(() => !world.Grapple.Attached, 3f);
                yield return KitUntil(() => c.IsGrounded, 4f);
                yield return KitFor(0.3f);
                KitCheck("the pull hops him onto the shelf", c.IsGrounded && hero.Position.x >= 8.9f && hero.Position.x <= 13.2f && hero.Position.y > 0.5f && clock.Time - pullFrom < 6f,
                    $"on the shelf at ({hero.Position.x:0.00}, {hero.Position.y:0.00}) {clock.Time - pullFrom:0.0} s after the throw (the shelf is 2.2 above the ledge, a jump reaches 2.3 but not the 2.7 sideways)");
                if (!c.IsGrounded || hero.Position.y < 0.5f)
                    yield break;
                Run.Progress(0.4f);

                // ── 3. The gust ─────────────────────────────────────────────────────
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.IsGrounded && hero.Position.y < -1f && hero.Position.x >= 13.5f, 5f);
                input.SetKey(Key.D, false);
                yield return KitWalkTo(15.45f, 0.12f, 4f);
                yield return KitFor(0.3f);
                var gustEntity = world.FindById("gust");
                var gust = gustEntity != null ? gustEntity.FindAbility<WindAbility>() : null;
                if (gust == null)
                {
                    KitCheck("the gust exists", false, "no wind ability on the entity gust");
                    yield break;
                }
                // a hostile bolt flies into the gust
                yield return KitUntil(() => gust.Blowing(clock.Time) >= 0.97f, 8f);
                world.Emit("kit:bolt");
                yield return KitUntil(() => world.FindById("kitBolt") != null && world.FindById("kitBolt").Body != null, 1f);
                var bolt = world.FindById("kitBolt");
                float v0 = bolt != null && bolt.Body != null ? bolt.Body.linearVelocity.x : 0f;
                yield return KitFor(0.35f);
                bolt = world.FindById("kitBolt");
                float v1 = bolt != null && bolt.Body != null ? bolt.Body.linearVelocity.x : 0f;
                KitCheck("wind bends hostile shots", bolt != null && v1 > v0 + 0.8f, $"a bolt flew into the gust at {v0:0.0} and left its first third at {v1:0.0} (units per second along the wind)");

                // the hero's own shot, fired up and ahead from beside the zone
                yield return KitUntil(() => gust.Blowing(clock.Time) >= 0.97f, 8f);
                float firedBefore = c.LastFireTime;
                input.SetAim(new Vector2(1f, 1f));
                yield return KitUntil(() => c.LastFireTime > firedBefore, 1.5f);
                input.SetAim(Vector2.zero);
                yield return null;
                ProjectileLifetime mine = null;
                Rigidbody2D mineBody = null;
                float nearest = float.MaxValue;
                foreach (var shot in UnityEngine.Object.FindObjectsByType<ProjectileLifetime>(FindObjectsSortMode.None))
                {
                    if (shot == null || !shot.gameObject.activeInHierarchy || !shot.TryGetComponent<Rigidbody2D>(out var body))
                        continue;
                    float d = Vector2.Distance(body.position, hero.Position);
                    if (d < nearest)
                    {
                        nearest = d;
                        mine = shot;
                        mineBody = body;
                    }
                }
                if (mine != null && mineBody != null && Mathf.Abs(mineBody.linearVelocity.y) > 0.1f)
                {
                    float ratio0 = mineBody.linearVelocity.x / mineBody.linearVelocity.y;
                    yield return KitFor(0.3f);
                    if (mine != null && mine.gameObject.activeInHierarchy && Mathf.Abs(mineBody.linearVelocity.y) > 0.1f)
                    {
                        float ratio1 = mineBody.linearVelocity.x / mineBody.linearVelocity.y;
                        KitCheck("wind bends the hero's shots", ratio1 > ratio0 + 0.05f, $"his shot's sideways/up ratio went from {ratio0:0.00} to {ratio1:0.00} inside the gust");
                    }
                    else
                    {
                        Run.Event("check", "floor-kit wind bends the hero's shots: not measured (the shot was gone)");
                    }
                }
                else
                {
                    Run.Event("check", "floor-kit wind bends the hero's shots: not measured (no live shot found)");
                }

                // the crossing: a gap a plain jump can't make, jumped as a gust rises
                yield return KitUntil(() => { float b = gust.Blowing(clock.Time); return b >= 0.5f && gust.Blowing(clock.Time + 0.15f) > b; }, 8f);
                input.SetKey(Key.D, true);
                yield return KitUntil(() => hero.Position.x >= 16.15f, 2f);
                yield return Tap(Key.Space);
                float peakVx = 0f, startX = hero.Position.x;
                giveUp = Time.realtimeSinceStartup + 3f;
                yield return null;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp)
                {
                    KitNote();
                    if (hero.Body != null)
                        peakVx = Mathf.Max(peakVx, hero.Body.linearVelocity.x);
                    if (c.IsGrounded && hero.Position.x > 19f)
                        break;
                    yield return null;
                }
                input.SetKey(Key.D, false);
                yield return KitFor(0.3f);
                KitCheck("wind pushes the hero", peakVx > 3.3f, $"his sideways speed peaked at {peakVx:0.0} in the gust (walking is 2.0)");
                KitCheck("a gust carries him over the gap", c.IsGrounded && hero.Position.x >= 19.9f && hero.Position.x <= 23.1f && hero.IsAlive,
                    $"jumped from x {startX:0.00}, landed at ({hero.Position.x:0.00}, {hero.Position.y:0.00}) on the far ledge (gap 3.4 wide, a plain jump carries ~2)");
                if (!c.IsGrounded || hero.Position.x < 19.9f)
                    yield break;
                Run.Progress(0.55f);

                // ── 4. The moving platform ──────────────────────────────────────────
                var moverEntity = world.FindById("mover");
                var mover = moverEntity != null ? moverEntity.FindAbility<MoverAbility>() : null;
                if (mover == null)
                {
                    KitCheck("the mover exists", false, "no mover ability on the entity mover");
                    yield break;
                }
                yield return KitWalkTo(22.6f, 0.12f, 4f);
                yield return KitUntil(() => moverEntity.Position.x <= 24.5f, 12f);
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.GroundCollider != null && c.GroundCollider == moverEntity.MainCollider, 3f);
                input.SetKey(Key.D, false);
                bool boarded = c.GroundCollider == moverEntity.MainCollider;
                float boardX = hero.Position.x;
                KitCheck("he boards the mover", boarded, $"standing on it at x {hero.Position.x:0.00} while it is at {moverEntity.Position.x:0.00}");
                if (!boarded)
                    yield break;
                yield return KitUntil(() => moverEntity.Position.x >= 25.6f, 4f);
                float tRide = clock.Time;
                var rideSample = KitAt(tRide);
                Vector2 moverThen = moverEntity.Position;
                yield return KitFor(0.7f);
                yield return KitRewindTo(tRide);
                float riderMiss = Vector2.Distance(hero.Position, rideSample.position);
                float moverMiss = Vector2.Distance(moverEntity.Position, moverThen);
                KitCheck("rewind restores the mover and its rider", riderMiss < 0.6f && moverMiss < 0.4f,
                    $"back at clock {clock.Time:0.00} (asked {tRide:0.00}): hero {riderMiss:0.00} and mover {moverMiss:0.00} from where they were");
                yield return KitUntil(() => moverEntity.Position.x >= 27.3f, 6f);
                float carried = hero.Position.x - boardX;
                KitCheck("the mover carries him", c.GroundCollider == moverEntity.MainCollider && carried > 1.8f,
                    $"no key held: he rode {carried:0.00} along the path (the platform went 3.0)");
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.IsGrounded && hero.Position.x >= 28.7f, 4f);
                input.SetKey(Key.D, false);
                yield return KitFor(0.2f);
                if (hero.Position.x < 28.6f)
                {
                    KitCheck("the mover reaches the far ledge", false, $"at x {hero.Position.x:0.00}");
                    yield break;
                }
                Run.Progress(0.7f);

                // ── 5. The collapsing floor ─────────────────────────────────────────
                yield return KitWalkTo(31.2f, 0.12f, 4f);
                yield return KitFor(0.3f);
                float tPreFloor = clock.Time;
                var preFloor = KitAt(tPreFloor);
                kitEvents.Clear();
                input.SetKey(Key.D, true);
                yield return KitUntil(() => hero.Position.x >= 38.2f, 14f);
                input.SetKey(Key.D, false);
                bool firstFell = false;
                var stone0 = world.FindById("floor0");
                if (stone0 != null)
                    firstFell = stone0.FindAbility<CrumbleAbility>().Falling;
                yield return KitRewindTo(tPreFloor);
                bool allWhole = true;
                for (int i = 0; i < 5; i++)
                {
                    var stone = world.FindById("floor" + i);
                    if (stone == null || stone.FindAbility<CrumbleAbility>().Falling || Mathf.Abs(stone.Position.y - (-1.85f)) > 0.08f)
                        allWhole = false;
                }
                float floorMiss = Vector2.Distance(hero.Position, preFloor.position);
                KitCheck("rewind restores a collapsed floor", firstFell && allWhole && floorMiss < 0.8f,
                    $"the first stone had fallen {firstFell}; back at clock {clock.Time:0.00} every stone is whole {allWhole} and he is {floorMiss:0.00} from where he stood");
                kitEvents.Clear();
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.IsGrounded && hero.Position.x >= 43.2f, 16f);
                input.SetKey(Key.D, false);
                var cracks = new List<float>();
                for (int i = 0; i < 5; i++)
                    cracks.Add(KitFirst("floorkit:crack:floor" + i));
                bool inOrder = true;
                for (int i = 1; i < cracks.Count; i++)
                    if (cracks[i] < 0f || cracks[i] < cracks[i - 1] + 0.8f)
                        inOrder = false;
                KitCheck("a floor collapses in a wave he can outrun", inOrder && cracks[0] >= 0f && hero.IsAlive && hero.Position.x >= 42.7f,
                    $"cracks at {string.Join(", ", cracks.ConvertAll(t => t.ToString("0.0")))} s, he stands at x {hero.Position.x:0.00}");
                if (hero.Position.x < 42.7f)
                    yield break;
                yield return KitUntil(() => KitSeen("floorkit:rebuild:floor0"), 14f);
                var home0 = world.FindById("floor0");
                KitCheck("a collapsed floor rebuilds", !kitTimedOut && home0 != null && Mathf.Abs(home0.Position.y - (-1.85f)) < 0.08f,
                    $"the first stone is back at y {(home0 != null ? home0.Position.y : 0f):0.00} ({(kitTimedOut ? "never rebuilt" : "rebuild event seen")})");
                Run.Progress(0.85f);

                // ── 6. The ceiling ──────────────────────────────────────────────────
                yield return KitWalkTo(43.4f, 0.15f, 3f);
                yield return KitFor(0.3f);
                float tPreFlip = clock.Time;
                var preFlip = KitAt(tPreFlip);
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.GravitySign < 0 && c.IsGrounded && hero.Position.y > 1.2f, 6f);
                KitCheck("a gravity-flip zone turns the hero over", c.GravitySign < 0 && world.GravityFlipped && c.IsGrounded && hero.Position.y > 1.2f,
                    $"in the zone gravity {c.GravitySign}, he stands upside down at ({hero.Position.x:0.00}, {hero.Position.y:0.00}) under the ceiling");
                if (c.GravitySign >= 0)
                    yield break;
                yield return KitUntil(() => hero.Position.x >= 49f, 10f);
                input.SetKey(Key.D, false);
                yield return KitFor(0.3f);
                float tCeiling = clock.Time;
                var onCeiling = KitAt(tCeiling);
                input.SetKey(Key.D, true);
                yield return KitFor(1.0f);
                input.SetKey(Key.D, false);
                yield return KitRewindTo(tCeiling);
                miss = Vector2.Distance(hero.Position, onCeiling.position);
                yield return KitFor(0.5f);
                KitCheck("rewind restores the flip on the ceiling", c.GravitySign < 0 && world.GravityFlipped && c.IsGrounded && hero.Position.y > 1.2f && miss < 0.7f,
                    $"back at clock {clock.Time:0.00} (asked {tCeiling:0.00}): gravity {c.GravitySign}, {miss:0.00} from his place, still on the ceiling at y {hero.Position.y:0.00}");
                yield return KitRewindTo(tPreFlip);
                miss = Vector2.Distance(hero.Position, preFlip.position);
                yield return KitFor(0.5f);
                KitCheck("rewind before the zone puts the floor back", c.GravitySign > 0 && !world.GravityFlipped && c.IsGrounded && hero.Position.y < -1f && miss < 0.8f,
                    $"back at clock {clock.Time:0.00} (asked {tPreFlip:0.00}): gravity {c.GravitySign}, {miss:0.00} from where he stood, y {hero.Position.y:0.00}");
                input.SetKey(Key.D, true);
                yield return KitUntil(() => c.GravitySign < 0 && c.IsGrounded && hero.Position.y > 1.2f, 6f);
                yield return KitUntil(() => c.GravitySign > 0 && c.IsGrounded && hero.Position.y < -1f && hero.Position.x >= 55.3f, 22f);
                input.SetKey(Key.D, false);
                yield return KitFor(0.4f);
                KitCheck("the ceiling walk ends on the landing", !kitTimedOut && c.GravitySign > 0 && c.IsGrounded && hero.Position.x >= 55.3f && hero.IsAlive,
                    $"gravity back to {c.GravitySign}, standing on the landing at ({hero.Position.x:0.00}, {hero.Position.y:0.00}); the floor is crossed");
                Run.Metric("floorKit.finishedAt", clock.Time);
            }
        }
    }
}
