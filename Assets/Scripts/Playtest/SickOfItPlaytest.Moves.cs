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
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The hero's moves (Docs/Story.md "The hero's moves"): the scene content/scenes/moveTest.json on Level 2's stage (setup shortcut).
        /// <c>hero-moves</c> plays both, <c>dash</c> and <c>wall-jump</c> one each.
        /// </summary>
        public const string HeroMovesId = "hero-moves";
        public const string DashId = "dash";
        public const string WallJumpId = "wall-jump";
        public const string MovesScene = "moveTest";
        public const int MovesLevel = 2;
        const float MovesSeconds = 220f;
        const string DashGood = "slimedLining";

        sealed partial class Session
        {
            int movesPassed, movesFailed;
            int dashCount, clingCount, kickCount;
            float firstClingSlide;

            /// <summary>One frame of the hero's moves: the clock, his place and what the controller says.</summary>
            struct MoveSample
            {
                public float time;
                public Vector2 position;
                public bool dashing, invulnerable, kicking;
                public float cooldown;
                public int wallSide, airDashes;
            }

            readonly List<MoveSample> moveSamples = new List<MoveSample>();
            bool moveSawDashPose, moveSawSlidePose, moveSawKickPose;
            float moveMinClingVy;

            void MoveCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    movesPassed++;
                    Run.Event("check", $"moves {beat}: ok ({detail})");
                }
                else
                {
                    movesFailed++;
                    Run.Event("check", $"moves {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"moves {beat}: {detail}");
                }
            }

            void OnMoveDashed(PlayerController who)
            {
                dashCount++;
                Run.Event("move", $"dash #{dashCount} at clock {GameClock.Instance.Time:0.00}");
            }

            void OnMoveClung(PlayerController who, int side)
            {
                clingCount++;
                Run.Event("move", $"wall cling #{clingCount} on the {(side > 0 ? "right" : "left")}");
            }

            void OnMoveKicked(PlayerController who, int side)
            {
                kickCount++;
                Run.Event("move", $"wall-jump #{kickCount} off the {(side > 0 ? "right" : "left")} wall at y {who.transform.position.y:0.00}");
            }

            void MoveTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "move")
                    Run.Event("move", $"telemetry {e.Subject} ({e.Detail}) value {e.Value}");
            }

            public IEnumerator HeroMovesRun(bool dash, bool wall)
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(MovesScene) || !content.Entities.ContainsKey("wallSlab"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"moves: the scene \"{MovesScene}\" or the entity \"wallSlab\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // Setup shortcut: a new game's save, so both moves start locked (no flags, no goods, no level cleared).
                UnlockFlags.Reset();
                RogueliteManager.Instance.ResetAllRogueliteData();
                for (int n = 4; n <= 6; n++)
                    PlayerPrefs.DeleteKey($"Level_{n}_Completed");
                Run.Event("shortcut", $"save reset (both moves locked); level {MovesLevel}'s stage with the scene {MovesScene} loaded directly");
                LevelRuntime.PendingScene = MovesScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(MovesLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(MovesLevel), 60f, "the moves bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                PlayerController.Dashed += OnMoveDashed;
                PlayerController.WallClung += OnMoveClung;
                PlayerController.WallJumped += OnMoveKicked;
                GameplayTelemetry.Recorded += MoveTelemetry;
                try
                {
                    yield return MovesSettle();
                    if (dash)
                        yield return DashSteps();
                    if (wall && !Run.Done)
                        yield return WallSteps();
                }
                finally
                {
                    PlayerController.Dashed -= OnMoveDashed;
                    PlayerController.WallClung -= OnMoveClung;
                    PlayerController.WallJumped -= OnMoveKicked;
                    GameplayTelemetry.Recorded -= MoveTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("moves.passed", movesPassed);
                Run.Metric("moves.failed", movesFailed);
                Run.Metric("moves.dashes", dashCount);
                Run.Metric("moves.wallJumps", kickCount);
                Run.Progress(1f);
                End(movesFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator MovesSettle()
            {
                var world = GameplayWorld.Instance;
                yield return Run.Until(() => world.Player != null && world.PlayerController != null && world.PlayerController.IsGrounded, 10f, "the hero to land on the bench");
                yield return MovesFor(0.4f);
            }

            /// <summary>Waits 'seconds' of real time, noting the hero's moves every frame.</summary>
            IEnumerator MovesFor(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    MoveNote();
                    yield return null;
                }
            }

            void MoveNote()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                if (world == null || world.PlayerController == null || clock.IsRewinding)
                    return;
                var c = world.PlayerController;
                var s = new MoveSample
                {
                    time = clock.Time, position = world.Player.Position, dashing = c.IsDashing, invulnerable = c.DashInvulnerable,
                    cooldown = c.DashCooldownLeft, wallSide = c.WallSide, airDashes = c.AirDashesLeft, kicking = c.IsWallKicking,
                };
                // A rewind rewrote the timeline: drop the samples of the future it undid.
                while (moveSamples.Count > 0 && moveSamples[moveSamples.Count - 1].time >= s.time)
                    moveSamples.RemoveAt(moveSamples.Count - 1);
                moveSamples.Add(s);
                moveSawDashPose |= c.MovePose == "Dash";
                moveSawSlidePose |= c.MovePose == "WallSlide";
                moveSawKickPose |= c.MovePose == "WallJump";
                if (c.WallSide != 0 && c.IsWallClinging)
                    moveMinClingVy = Mathf.Min(moveMinClingVy, world.PlayerController.GetComponent<Rigidbody2D>().linearVelocity.y);
            }

            MoveSample MoveAt(float time)
            {
                MoveSample best = default;
                float bestGap = float.MaxValue;
                foreach (var s in moveSamples)
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

            /// <summary>Holds R until the game clock is back at 'target' (juice topped up: a setup shortcut).</summary>
            IEnumerator MoveRewindTo(float target)
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
                // Only a couple of frames: the clock runs forward again once R is up, and a longer wait
                // lands the checks after the burst they ask about (0.34 s late).
                yield return null;
                yield return null;
            }

            /// <summary>Walks to world x with A/D (real input).</summary>
            IEnumerator MovesWalkTo(float x)
            {
                var hero = GameplayWorld.Instance.Player;
                float giveUp = Time.realtimeSinceStartup + 8f;
                while (!Run.Done && Mathf.Abs(hero.Position.x - x) > 0.15f && Time.realtimeSinceStartup < giveUp)
                {
                    input.SetKey(Key.D, hero.Position.x < x);
                    input.SetKey(Key.A, hero.Position.x > x);
                    MoveNote();
                    yield return null;
                }
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
                yield return MovesFor(0.25f);
            }

            /// <summary>Presses Shift with D or A held for the direction (real input) and releases both.</summary>
            IEnumerator PressDash(int direction)
            {
                input.SetKey(direction > 0 ? Key.D : Key.A, true);
                yield return null;
                input.SetKey(Key.LeftShift, true);
                yield return null;
                yield return null;
                input.SetKey(Key.LeftShift, false);
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
            }

            // ─── The dash ──────────────────────────────────────────────────────────────

            IEnumerator DashSteps()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                var hero = world.Player;
                var c = world.PlayerController;
                var roguelite = RogueliteManager.Instance;

                // 1. The dash is his from the first second (player.json moves.dash.startUnlocked); Yooba's good is only an upgrade (a second air dash).
                SickOfIt.Data.Config.UpgradeDefinition good = null;
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && upgrade.id == DashGood)
                        good = upgrade;
                MoveCheck("Yooba has the dash good", good != null && good.Effect == SickOfIt.Data.Config.ShopEffect.Dash, good != null ? $"{good.title}, {good.costsPerLevel[0]} diamonds" : "no good with id " + DashGood);
                if (good == null)
                    yield break;
                MoveCheck("the dash is his before buying anything", c.DashUnlocked && !c.DashUpgraded, $"unlocked {c.DashUnlocked}, upgraded {c.DashUpgraded}");
                yield return MovesWalkTo(-4.5f);
                int dashesBefore = dashCount;
                yield return PressDash(1);
                yield return MovesFor(0.4f);
                MoveCheck("Shift dashes with nothing bought", dashCount == dashesBefore + 1, $"dashes {dashCount - dashesBefore}");
                yield return MovesFor(1.0f);

                // 2. Unlock it the way a player does: clear Floor 998, earn the diamonds, buy it from Yooba (the diamonds and the clear are setup shortcuts).
                GameProgressManager.Instance.SetLevelCompleted(4);
                roguelite.AddPoints(good.costsPerLevel[0] + 10);
                MoveCheck("the dash good is on sale after Floor 998", good.IsForSale(0), "requires " + string.Join(",", good.requires));
                bool bought = roguelite.TryPurchase(good);
                MoveCheck("the purchase goes through", bought && roguelite.GetUpgradeLevel(DashGood) == 1, $"level {roguelite.GetUpgradeLevel(DashGood)}");
                world.Moves.Arm(roguelite);
                MoveCheck("the dash is his and upgraded", c.DashUnlocked && c.DashUpgraded && UnlockFlags.Has(HeroMoves.DashFlag), $"unlocked {c.DashUnlocked}, upgraded {c.DashUpgraded}, flag {UnlockFlags.Has(HeroMoves.DashFlag)}");
                Run.Progress(0.1f);

                // 3. A bolt with no dash hurts (the control).
                yield return MovesWalkTo(-4.5f);
                int health = hero.Health;
                world.Emit("mv:bolt");
                // The rule runs on the next world tick, so the bolt exists a frame or two after Emit: wait for it, then for it to land or go.
                yield return Run.Until(() => world.FindById("moveBolt") != null, 2f, "the bolt to spawn");
                yield return Run.Until(() => hero.Health < health || world.FindById("moveBolt") == null, 4f, "the bolt to land");
                yield return MovesFor(0.3f);
                MoveCheck("a bolt that is not dashed hurts", hero.Health < health, $"health {health} -> {hero.Health}");
                world.Emit("mv:heal");
                yield return MovesFor(1.6f);

                // 4. The dash goes through a bolt unharmed.
                health = hero.Health;
                dashesBefore = dashCount;
                moveSawDashPose = false;
                float startX = hero.Position.x;
                world.Emit("mv:bolt");
                yield return Run.Until(() => world.FindById("moveBolt") != null, 2f, "the bolt to spawn");
                var bolt = world.FindById("moveBolt");
                if (bolt == null)
                {
                    MoveCheck("the bolt spawns", false, "no live entity with id moveBolt");
                    yield break;
                }
                float giveUp = Time.realtimeSinceStartup + 3f;
                while (!Run.Done && bolt.IsAlive && bolt.Position.x - hero.Position.x > 1.7f && Time.realtimeSinceStartup < giveUp)
                {
                    MoveNote();
                    yield return null;
                }
                float gap = bolt.Position.x - hero.Position.x;
                yield return PressDash(1);
                float dashFrom = clock.Time;
                bool wasInvulnerable = false, wasDashing = false;
                float until = Time.realtimeSinceStartup + 0.6f;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    wasInvulnerable |= c.DashInvulnerable;
                    wasDashing |= c.IsDashing;
                    MoveNote();
                    yield return null;
                }
                float moved = hero.Position.x - startX;
                bool passed = bolt.IsAlive && bolt.Position.x < hero.Position.x;
                Run.Event("dash", $"bolt was {gap:0.00} ahead; he moved {moved:0.00}; bolt alive {bolt.IsAlive} at x {bolt.Position.x:0.00}, hero x {hero.Position.x:0.00}");
                MoveCheck("the dash starts", dashCount == dashesBefore + 1 && wasDashing, $"{dashCount - dashesBefore} dash(es), burst seen {wasDashing}");
                MoveCheck("he is untouchable during it", wasInvulnerable, $"DashInvulnerable seen {wasInvulnerable}");
                MoveCheck("he goes through the bolt unharmed", hero.Health == health && passed, $"health {health} -> {hero.Health}; the bolt flew on and is now behind him: {passed}");
                MoveCheck("the dash covers ground", moved > c.DashDistance * 0.7f, $"moved {moved:0.00} (the dash is {c.DashDistance:0.0})");
                MoveCheck("the Dash pose plays", moveSawDashPose, "MovePose Dash seen " + moveSawDashPose);
                Run.Progress(0.3f);

                // 5. The cooldown: a second press at once does nothing; after it the next dash goes.
                yield return MovesWalkTo(-5.5f);
                yield return PressDash(1);
                yield return MovesFor(0.15f);
                int afterFirst = dashCount;
                yield return PressDash(1);
                yield return MovesFor(0.3f);
                MoveCheck("a second press during the cooldown does nothing", dashCount == afterFirst && c.DashCooldownLeft > 0f, $"dashes {dashCount - afterFirst} extra, cooldown left {c.DashCooldownLeft:0.00}");
                yield return Run.Until(() => c.DashCooldownLeft <= 0f, 3f, "the dash cooldown to end");
                yield return MovesWalkTo(-5.5f);
                int beforeReady = dashCount;
                yield return PressDash(1);
                yield return MovesFor(0.3f);
                MoveCheck("the dash is ready again after the cooldown", dashCount == beforeReady + 1, $"{dashCount - beforeReady} dash(es)");
                yield return Run.Until(() => c.DashCooldownLeft <= 0f, 3f, "the dash cooldown to end");

                // 5b. Run-and-shoot: walking while firing shows the RunFire pose, standing still while firing does not.
                yield return MovesWalkTo(-6.0f);
                bool sawRunFire = false, sawRunFireStill = false;
                input.SetAim(Vector2.right);
                input.SetKey(Key.D, true);
                float runUntil = Time.realtimeSinceStartup + 0.8f;
                while (!Run.Done && Time.realtimeSinceStartup < runUntil)
                {
                    MoveNote();
                    sawRunFire |= c.IsRunFiring;
                    yield return null;
                }
                input.SetKey(Key.D, false);
                float stillUntil = Time.realtimeSinceStartup + 0.6f;
                while (!Run.Done && Time.realtimeSinceStartup < stillUntil)
                {
                    MoveNote();
                    if (Time.realtimeSinceStartup > stillUntil - 0.3f)
                        sawRunFireStill |= c.IsRunFiring;
                    yield return null;
                }
                input.SetAim(Vector2.zero);
                MoveCheck("firing while walking shows the RunFire pose", sawRunFire, $"RunFire seen walking {sawRunFire}");
                MoveCheck("firing standing still does not", !sawRunFireStill, $"RunFire seen standing {sawRunFireStill}");
                yield return MovesWalkTo(-6.0f);

                // 6. Rewind: back into the middle of a burst, and back to before it.
                yield return MovesWalkTo(-6.0f);
                yield return MovesFor(0.3f);
                float before = clock.Time;
                var beforeState = MoveAt(before);
                yield return PressDash(1);
                yield return MovesFor(0.8f);
                float mid = -1f;
                foreach (var s in moveSamples)
                    if (s.time > before && s.dashing && s.invulnerable && mid < 0f)
                        mid = s.time + 0.04f;
                if (mid < 0f)
                {
                    MoveCheck("a burst to rewind into was sampled", false, "no sampled frame of the dash");
                    yield break;
                }
                yield return MoveRewindTo(mid);
                var want = MoveAt(clock.Time);
                MoveCheck("rewind into the burst restores the dash", c.IsDashing == want.dashing && c.DashInvulnerable == want.invulnerable && want.dashing,
                    $"clock {clock.Time:0.00} (asked {mid:0.00}): dashing {c.IsDashing} (was {want.dashing}), untouchable {c.DashInvulnerable} (was {want.invulnerable})");
                // Mid-burst he moves ~16 u/s, so the nearest sample (frames are 20-40 ms apart, and the two frames
                // after R is released run forward) can sit a few frames' worth of travel away: allow that much.
                const float dashSpeed = 2.6f / 0.16f; // player.json moves.dash distance / duration
                float placeSlack = 0.5f + dashSpeed * (Mathf.Abs(want.time - clock.Time) + 0.04f);
                MoveCheck("rewind into the burst restores his place and the cooldown", Vector2.Distance(hero.Position, want.position) < placeSlack && Mathf.Abs(c.DashCooldownLeft - want.cooldown) < 0.15f,
                    $"{Vector2.Distance(hero.Position, want.position):0.00} from where he was (allowed {placeSlack:0.00}); cooldown {c.DashCooldownLeft:0.00} (was {want.cooldown:0.00})");
                yield return MovesFor(0.5f);
                yield return MoveRewindTo(before);
                want = MoveAt(clock.Time);
                MoveCheck("rewind to before the dash gives the dash back", !c.IsDashing && c.DashCooldownLeft <= 0.05f && !c.DashInvulnerable && Mathf.Abs(hero.Position.x - want.position.x) < 0.6f,
                    $"clock {clock.Time:0.00} (asked {before:0.00}): dashing {c.IsDashing}, cooldown {c.DashCooldownLeft:0.00}, {Mathf.Abs(hero.Position.x - want.position.x):0.00} from where he stood");
                int afterRewind = dashCount;
                yield return PressDash(1);
                yield return MovesFor(0.3f);
                MoveCheck("and he can dash again", dashCount == afterRewind + 1, $"{dashCount - afterRewind} dash(es)");
                MoveCheck("a rewind never takes the unlock away", c.DashUnlocked && UnlockFlags.Has(HeroMoves.DashFlag), $"unlocked {c.DashUnlocked}");
                Run.Progress(0.5f);
            }

            // ─── The wall-jump ─────────────────────────────────────────────────────────

            IEnumerator WallSteps()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                var hero = world.Player;
                var c = world.PlayerController;
                var body = c.GetComponent<Rigidbody2D>();

                // 1. Locked: a shaft around him, and pushing into the wall in the air does nothing.
                yield return MovesWalkTo(-4.5f);
                world.Emit("mv:walls");
                yield return MovesFor(0.6f);
                int clingsBefore = clingCount, kicksBefore = kickCount;
                input.SetKey(Key.D, true);
                yield return null;
                yield return Tap(Key.Space);
                yield return MovesFor(0.9f);
                input.SetKey(Key.D, false);
                MoveCheck("the wall does nothing while the wall-jump is locked", !c.WallJumpUnlocked && clingCount == clingsBefore && kickCount == kicksBefore,
                    $"unlocked {c.WallJumpUnlocked}, clings {clingCount - clingsBefore}, kicks {kickCount - kicksBefore}");
                yield return Run.Until(() => c.IsGrounded, 4f, "him to land");

                // 2. The story moment: Floor 996 (level 6) cleared gives it (a setup shortcut for the clear).
                GameProgressManager.Instance.SetLevelCompleted(5);
                world.Moves.Arm(RogueliteManager.Instance);
                MoveCheck("Floor 997 alone does not give it", !c.WallJumpUnlocked, $"unlocked {c.WallJumpUnlocked}");
                GameProgressManager.Instance.SetLevelCompleted(6);
                world.Moves.Arm(RogueliteManager.Instance);
                MoveCheck("clearing Floor 996 gives the wall-jump", c.WallJumpUnlocked && UnlockFlags.Has(HeroMoves.WallJumpFlag), $"unlocked {c.WallJumpUnlocked}, flag {UnlockFlags.Has(HeroMoves.WallJumpFlag)}");
                Run.Progress(0.6f);

                // 3. Climb the shaft with real input: jump at one wall, kick off it, cross to the other, kick again.
                yield return MovesWalkTo(-4.5f);
                yield return MovesFor(0.3f);
                float startY = hero.Position.y;
                float maxY = startY;
                int dir = 1, seenKicks = kickCount;
                int kicksFrom = kickCount, clingsFrom = clingCount;
                moveMinClingVy = 0f;
                moveSawSlidePose = moveSawKickPose = false;
                float lastPress = -1f, downAt = -1f;
                bool spaceDown = false;
                float giveUp = Time.realtimeSinceStartup + 40f;
                bool clungWhileFalling = false;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && hero.IsAlive && (maxY - startY < 6.0f || (kickCount - kicksFrom < 3 && maxY - startY < 7.0f)))
                {
                    input.SetKey(Key.D, dir > 0);
                    input.SetKey(Key.A, dir < 0);
                    float now = Time.realtimeSinceStartup;
                    bool want = (c.IsGrounded || c.WallSide != 0) && now - lastPress > 0.3f;
                    if (want && !spaceDown)
                    {
                        input.SetKey(Key.Space, true);
                        spaceDown = true;
                        downAt = now;
                        lastPress = now;
                    }
                    else if (spaceDown && now - downAt > 0.08f)
                    {
                        input.SetKey(Key.Space, false);
                        spaceDown = false;
                    }
                    if (kickCount > seenKicks)
                    {
                        seenKicks = kickCount;
                        dir = -dir;
                    }
                    clungWhileFalling |= c.IsWallClinging && body.linearVelocity.y < -0.2f;
                    maxY = Mathf.Max(maxY, hero.Position.y);
                    MoveNote();
                    yield return null;
                }
                input.SetKey(Key.Space, false);
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
                int kicks = kickCount - kicksFrom;
                float rose = maxY - startY;
                Run.Event("wall-jump", $"climbed {rose:0.00} units with {kicks} kick(s) and {clingCount - clingsFrom} cling(s)");
                MoveCheck("he catches the wall", clingCount > clingsFrom, $"{clingCount - clingsFrom} cling(s)");
                MoveCheck("he kicks off it", kicks >= 2, $"{kicks} kick(s)");
                // Each kick adds about 1.75 over the jump's 2.3: 3+ kicks reach 4.5, the 2 the shaft sometimes allows reach ~4.
                float needRise = kicks >= 3 ? 4.5f : 3.5f;
                MoveCheck("he climbs the shaft", rose >= needRise && hero.IsAlive, $"rose {rose:0.00} units with {kicks} kick(s), needs {needRise:0.0} (a jump alone reaches about 2.3)");
                MoveCheck("he slides slowly down the wall", moveMinClingVy >= -1.7f, $"fastest slide {moveMinClingVy:0.00} (limit about -1.3)");
                MoveCheck("the WallSlide and WallJump poses play", moveSawKickPose && (moveSawSlidePose || !clungWhileFalling), $"WallJump seen {moveSawKickPose}, WallSlide seen {moveSawSlidePose}");
                Run.Progress(0.85f);

                // 4. Rewind: back to a moment clinging to a wall, and back to before the first kick.
                float clingTime = -1f;
                for (int i = moveSamples.Count - 1; i >= 0; i--)
                {
                    if (moveSamples[i].wallSide != 0 && moveSamples[i].time < clock.Time - 0.5f)
                    {
                        clingTime = moveSamples[i].time;
                        break;
                    }
                }
                if (clingTime > 0f && hero.IsAlive)
                {
                    yield return MoveRewindTo(clingTime);
                    var want = MoveAt(clock.Time);
                    MoveCheck("rewind back to a cling restores the wall", Vector2.Distance(hero.Position, want.position) < 0.5f && c.WallSide == want.wallSide && want.wallSide != 0,
                        $"clock {clock.Time:0.00} (asked {clingTime:0.00}): wall side {c.WallSide} (was {want.wallSide}), {Vector2.Distance(hero.Position, want.position):0.00} from where he was");
                    int kicksAfterRewind = kickCount;
                    yield return PressWallKick(c.WallSide);
                    yield return MovesFor(0.3f);
                    MoveCheck("and he can kick off it again", kickCount > kicksAfterRewind, $"{kickCount - kicksAfterRewind} kick(s) after the rewind");
                }
                else
                {
                    MoveCheck("rewind back to a cling restores the wall", false, "no sampled cling to rewind to, or he died");
                }
                MoveCheck("a rewind never takes the unlock away", c.WallJumpUnlocked && UnlockFlags.Has(HeroMoves.WallJumpFlag), $"unlocked {c.WallJumpUnlocked}");
            }

            /// <summary>Jump with the stick held into the wall he hangs on (real input), which is what keeps him clinging until the kick.</summary>
            IEnumerator PressWallKick(int wallSide)
            {
                input.SetKey(wallSide > 0 ? Key.D : Key.A, true);
                yield return null;
                yield return Tap(Key.Space);
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
            }
        }
    }
}
