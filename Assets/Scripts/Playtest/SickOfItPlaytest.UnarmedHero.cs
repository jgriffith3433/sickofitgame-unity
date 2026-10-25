using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The unarmed hero (art/hero.json unarmed* and punch1-3; player.json UnarmedIdle/Walk/Jump/Fall, Punch1-3): on the weapons bench the
        /// bot takes up the fists with Q (real input) and must show the unarmed Idle, Walk, Jump and Fall (never the launcher's), then punch an
        /// imp until all three combo steps have swung: each step's own Punch clip on screen at the swing and still on screen when the hit lands.
        /// Sheets are stand-ins until the real clips are generated; the state is read from <see cref="PlayerSceneClip"/>.
        /// </summary>
        public const string UnarmedHeroId = "unarmed-hero";
        const float UnarmedHeroSeconds = 120f;

        sealed partial class Session
        {
            int unarmedPassed, unarmedFailed;
            int unarmedStep = -1;
            float unarmedSwingAt;
            readonly HashSet<int> unarmedSwings = new HashSet<int>(), unarmedHitsOk = new HashSet<int>();

            void UnarmedCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    unarmedPassed++;
                    Run.Event("check", $"unarmed-hero {beat}: ok ({detail})");
                }
                else
                {
                    unarmedFailed++;
                    Run.Event("check", $"unarmed-hero {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"unarmed-hero {beat}: {detail}");
                }
            }

            void UnarmedTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                WeaponsTelemetry(log, e);
                if (e.Kind == "swing" && e.Subject == "fists")
                {
                    unarmedStep = e.Value;
                    unarmedSwingAt = e.Time;
                    string clip = HeroClipOnScreen(out int frame);
                    Run.Event("unarmed", $"swing step {e.Value + 1}: clip on screen {clip} frame {frame}");
                    // The pose is chosen in the controller's Update, a frame after the swing starts; checked at the hit.
                    unarmedSwings.Add(e.Value);
                }
                else if (e.Kind == "weaponHit" && e.Subject == "fists" && unarmedStep >= 0)
                {
                    string clip = HeroClipOnScreen(out int frame);
                    bool ok = clip == "Punch" + (unarmedStep + 1);
                    Run.Event("unarmed", $"hit of step {unarmedStep + 1} {e.Time - unarmedSwingAt:0.00} s after the swing: clip {clip} frame {frame}{(ok ? "" : " (WRONG)")}");
                    if (ok)
                        unarmedHitsOk.Add(unarmedStep);
                }
            }

            public IEnumerator UnarmedHeroRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(WeaponsScene) || !content.Weapons.ContainsKey("fists"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"unarmed-hero: the scene \"{WeaponsScene}\" or the weapon \"fists\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                SickOfIt.Data.WeaponProgress.Reset();
                SickOfIt.Data.WeaponUnlocks.Reset();
                Run.Event("shortcut", $"weapons reset; level {WeaponsLevel}'s stage with the scene {WeaponsScene} loaded directly");
                LevelRuntime.PendingScene = WeaponsScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(WeaponsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(WeaponsLevel), 60f, "the weapons bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                GameplayTelemetry.Recorded += UnarmedTelemetry;
                try
                {
                    yield return UnarmedHeroSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= UnarmedTelemetry;
                    input.ReleaseAll();
                    SickOfIt.Data.WeaponProgress.Reset();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("unarmedHero.passed", unarmedPassed);
                Run.Metric("unarmedHero.failed", unarmedFailed);
                Run.Progress(1f);
                End(unarmedFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator UnarmedHeroSteps()
            {
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var pc = world.PlayerController;
                var clock = GameClock.Instance;

                // The launcher first: his ordinary Idle, no unarmed state.
                yield return WaitReal(0.5f);
                UnarmedCheck("launcher in hand keeps Idle", pc.UnarmedPose == null && HeroClipOnScreen(out _) != "UnarmedIdle", $"unarmed pose {pc.UnarmedPose ?? "none"}");

                world.Emit("wpn:give");
                yield return Run.Until(() => weapons.Has("fists"), 6f, "fists given");
                yield return Tap(Key.Q);
                yield return WaitReal(0.5f);
                UnarmedCheck("fists equipped", weapons.CurrentId == "fists", $"equipped {weapons.CurrentId}");

                // Idle.
                yield return Run.Until(() => pc.IsGrounded, 6f, "the hero on the ground");
                yield return WaitReal(0.3f);
                string idle = HeroClipOnScreen(out _);
                UnarmedCheck("unarmed Idle", pc.UnarmedPose == "UnarmedIdle" && idle == "UnarmedIdle", $"pose {pc.UnarmedPose}, clip {idle}");

                // Walk.
                input.SetKey(Key.A, true);
                yield return WaitReal(0.4f);
                string walk = HeroClipOnScreen(out _);
                UnarmedCheck("unarmed Walk", pc.UnarmedPose == "UnarmedWalk" && walk == "UnarmedWalk", $"pose {pc.UnarmedPose}, clip {walk}");
                input.SetKey(Key.A, false);

                // Jump and Fall: hold Space and sample every frame until both have been on screen.
                bool jumpSeen = false, fallSeen = false;
                yield return WaitReal(0.3f);
                input.SetKey(Key.Space, true);
                float until = Time.realtimeSinceStartup + 3f;
                while (!Run.Done && Time.realtimeSinceStartup < until && !(jumpSeen && fallSeen))
                {
                    string c = HeroClipOnScreen(out _);
                    if (c == "UnarmedJump" && pc.UnarmedPose == "UnarmedJump") jumpSeen = true;
                    if (c == "UnarmedFall" && pc.UnarmedPose == "UnarmedFall") fallSeen = true;
                    yield return null;
                }
                input.SetKey(Key.Space, false);
                UnarmedCheck("unarmed Jump", jumpSeen, jumpSeen ? "UnarmedJump on screen going up" : "never seen");
                UnarmedCheck("unarmed Fall", fallSeen, fallSeen ? "UnarmedFall on screen coming down" : "never seen");
                yield return Run.Until(() => pc.IsGrounded, 6f, "landing");

                // Punches: aim at the imp until each combo step has swung and landed its hit under its own clip.
                var aim = new Vector2(1f, 0f);
                for (int round = 0; round < 6 && !Run.Done && unarmedHitsOk.Count < 3; round++)
                {
                    world.Emit("wpn:bag");
                    yield return Run.Until(() => FindBag(world) != null, 6f, "an imp to punch");
                    var bag = FindBag(world);
                    input.SetAim(aim);
                    float end = Time.realtimeSinceStartup + 6f;
                    while (!Run.Done && Time.realtimeSinceStartup < end && bag != null && bag.IsAlive)
                        yield return null;
                    input.SetAim(Vector2.zero);
                    yield return WaitReal(0.6f);
                }
                for (int step = 0; step < 3; step++)
                    UnarmedCheck($"Punch{step + 1} with its hit", unarmedSwings.Contains(step) && unarmedHitsOk.Contains(step),
                        $"step {step + 1} swung: {unarmedSwings.Contains(step)}; hit landed under Punch{step + 1}: {unarmedHitsOk.Contains(step)}");

                // Back to the launcher: the ordinary states again.
                yield return Tap(Key.Q);
                yield return WaitReal(0.6f);
                UnarmedCheck("launcher back", weapons.CurrentId == PlayerWeapons.Launcher && pc.UnarmedPose == null, $"equipped {weapons.CurrentId}, unarmed pose {pc.UnarmedPose ?? "none"}");
            }
        }
    }
}
