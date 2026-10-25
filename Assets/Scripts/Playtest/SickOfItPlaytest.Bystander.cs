using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The bystander scenario: the scene content/scenes/bystanderTest.json on Level 1's stage (setup shortcut).</summary>
        public const string BystanderId = "bystander";
        public const string BystanderScene = "bystanderTest";
        public const int BystanderLevel = 1;
        const float BystanderSeconds = 150f;

        sealed partial class Session
        {
            int bystanderPassed, bystanderFailed;
            readonly Dictionary<string, float> bystanderSignals = new Dictionary<string, float>();
            readonly List<string> bystanderActions = new List<string>();

            void BystanderCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    bystanderPassed++;
                    Run.Event("check", $"bystander {beat}: ok ({detail})");
                }
                else
                {
                    bystanderFailed++;
                    Run.Event("check", $"bystander {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"bystander {beat}: {detail}");
                }
            }

            void BystanderTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal" && e.Subject.StartsWith("soul:"))
                {
                    if (!bystanderSignals.ContainsKey(e.Subject))
                        Run.Event("bystander", $"signal {e.Subject} at {e.Time:0.00}");
                    bystanderSignals[e.Subject] = e.Time;
                }
                else if (e.Kind == "signal" && e.Subject.StartsWith("bys:"))
                {
                    bystanderSignals[e.Subject] = e.Time;
                }
                else if (e.Kind == "cutscene" && (e.Subject == "walkEntityTo" || e.Subject == "faceEntity" || e.Subject == "playEntityAnimation"
                         || e.Subject == "entityWalkArrived" || e.Subject == "entityWalkBlocked"))
                {
                    bystanderActions.Add(e.Subject);
                    Run.Event("bystander", $"{e.Subject} {e.Detail} (value {e.Value}) at {e.Time:0.00}");
                }
            }

            static Entity BystanderFind(GameplayWorld world, string id)
            {
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.InstanceId == id)
                        return e;
                }
                return null;
            }

            public IEnumerator BystanderRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(BystanderScene) || !content.Entities.ContainsKey("bystanderSoul"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"bystander: the scene \"{BystanderScene}\" or the entity \"bystanderSoul\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                Run.Event("shortcut", $"level {BystanderLevel}'s stage with the scene {BystanderScene} loaded directly");
                LevelRuntime.PendingScene = BystanderScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(BystanderLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(BystanderLevel), 60f, "the bystander bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                GameplayTelemetry.Recorded += BystanderTelemetry;
                try
                {
                    yield return BystanderSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= BystanderTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("bystander.passed", bystanderPassed);
                Run.Metric("bystander.failed", bystanderFailed);
                Run.Progress(1f);
                End(bystanderFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            bool BystanderSaw(string signal) => bystanderSignals.ContainsKey(signal);

            IEnumerator BystanderSteps()
            {
                var world = GameplayWorld.Instance;
                var clock = GameClock.Instance;
                var hud = world.Hud;
                var hero = world.Player;
                yield return Run.Until(() => BystanderFind(world, "soul") != null, 6f, "the damned soul to appear");
                var soul = BystanderFind(world, "soul");
                if (soul == null)
                {
                    BystanderCheck("the soul spawns", false, "no live entity with id soul");
                    yield break;
                }
                yield return WaitReal(0.5f);
                BystanderCheck("the soul stands behind him, looking away", soul.Position.x - hero.Position.x > 5f && soul.Facing > 0f && !soul.ScriptWalking,
                    $"soul {soul.Position.x - hero.Position.x:0.0} right of the hero, facing {soul.Facing:0}");

                // 1. A scene walks a non-hero entity (walkEntityTo), and it faces the way it goes.
                float startX = soul.Position.x;
                world.Emit("bys:walk");
                yield return Run.Until(() => soul.ScriptWalking, 3f, "the soul to start walking");
                bool walkingSeen = soul.ScriptWalking;
                yield return Run.Until(() => BystanderSaw("soul:walked") || !soul.ScriptWalking, 8f, "the soul to arrive");
                yield return WaitReal(0.3f);
                float walked = startX - soul.Position.x;
                BystanderCheck("walkEntityTo walks a non-hero entity", walkingSeen && walked > 0.8f && bystanderActions.Contains("entityWalkArrived"),
                    $"walked {walked:0.00} toward the hero, to {soul.Position.x - hero.Position.x:0.0} from him; walking seen {walkingSeen}");
                BystanderCheck("it faced the way it walked", soul.Facing < 0f, $"facing {soul.Facing:0} after walking left");
                Run.Progress(0.15f);

                // 2. faceEntity turns it away (his back to the hero).
                float xTurn = soul.Position.x;
                world.Emit("bys:turn");
                yield return WaitReal(0.5f);
                BystanderCheck("faceEntity turns a non-hero entity", soul.Facing > 0f && bystanderActions.Contains("faceEntity"), $"facing {soul.Facing:0} (was -1)");
                float turnedAt = clock.Time;

                // 3. He walks up (real input); inside 4.5 units the soul turns to him and speaks.
                input.SetKey(Key.D, true);
                yield return Run.Until(() => BystanderSaw("soul:noticed") || soul.Position.x - hero.Position.x < 3f, 8f, "the soul to notice the hero");
                input.SetKey(Key.D, false);
                yield return WaitReal(0.4f);
                BystanderCheck("it turns to the hero", BystanderSaw("soul:noticed") && soul.Facing < 0f,
                    $"noticed {BystanderSaw("soul:noticed")}, facing {soul.Facing:0}, {soul.Position.x - hero.Position.x:0.0} from him");
                string line = hud.SubtitleShown;
                BystanderCheck("it speaks a subtitle", line.Contains("visitor"), $"subtitle \"{line}\"");
                Run.Progress(0.35f);

                // 4. playEntityAnimation on a non-hero entity.
                world.Emit("bys:anim");
                yield return WaitReal(0.3f);
                BystanderCheck("playEntityAnimation animates a non-hero entity", bystanderActions.Contains("playEntityAnimation") && soul.Animation.CurrentState == "Hiccup",
                    $"animation state {soul.Animation.CurrentState}");
                yield return WaitReal(1.5f);

                // 5. Shots pass: an imp appears behind the soul and he shoots at it along the ground (real input).
                int healthBefore = hero.Health;
                world.Emit("bys:imp");
                yield return WaitReal(0.5f);
                float soulX = soul.Position.x;
                input.SetAim(new Vector2(1f, 0f));
                yield return Run.Until(() => BystanderSaw("soul:flinchShot"), 10f, "the soul to flinch from a shot");
                float flinchTime = clock.Time;
                yield return WaitReal(0.25f);
                float flinchX = soul.Position.x;
                BystanderCheck("it flinches when a shot passes", BystanderSaw("soul:flinchShot") && flinchX > soulX + 0.1f,
                    $"stepped back {flinchX - soulX:0.00} (from {soulX - hero.Position.x:0.0} to {flinchX - hero.Position.x:0.0} from him)");
                yield return Run.Until(() => BystanderSaw("soul:settled"), 6f, "the soul to settle back");
                BystanderCheck("then settles back", BystanderSaw("soul:settled") && soul.Position.x < flinchX, $"x {soul.Position.x - hero.Position.x:0.0} from the hero");
                Run.Progress(0.6f);

                // 6. The hero is hurt near it: the imp's shots do it; a fallback hurt after 15 s keeps the beat from stalling.
                float waitHurt = Time.realtimeSinceStartup + 15f;
                while (!Run.Done && Time.realtimeSinceStartup < waitHurt && !BystanderSaw("soul:flinchHurt"))
                    yield return null;
                input.SetAim(Vector2.zero);
                if (!BystanderSaw("soul:flinchHurt"))
                {
                    Run.Event("bystander", "the imp did not hit him in 15 s: a scene rule hurts him (bys:hurt)");
                    world.Emit("bys:hurt");
                    yield return Run.Until(() => BystanderSaw("soul:flinchHurt"), 4f, "the soul to see the hero hurt");
                }
                yield return WaitReal(0.4f);
                BystanderCheck("the hero was hurt", hero.Health < healthBefore, $"health {healthBefore} -> {hero.Health}");
                BystanderCheck("it reacts when the hero is hurt nearby", BystanderSaw("soul:flinchHurt") && BystanderSaw("soul:spoke"), "flinched and spoke");
                Run.Event("bystander", $"subtitle now: \"{hud.SubtitleShown}\"");
                Run.Progress(0.8f);

                // 7. Rewind (hold R) to the moment he turned it away: its facing and place come back, and it hasn't noticed him yet.
                yield return WaitReal(0.5f);
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(30f);
                Run.Event("rewind", $"hold R back to before the soul noticed him (it was turned away at {turnedAt:0.0}, now {clock.Time:0.0})");
                input.SetKey(Key.R, true);
                float giveUp = Time.realtimeSinceStartup + 20f;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && clock.Time > turnedAt + 0.2f)
                    yield return null;
                input.SetKey(Key.R, false);
                yield return WaitReal(0.4f);
                BystanderCheck("rewind restores the bystander", clock.Time < turnedAt + 0.5f && soul.Facing > 0f && !soul.ScriptWalking && Mathf.Abs(soul.Position.x - xTurn) < 0.5f,
                    $"clock {clock.Time:0.0} (turned away at {turnedAt:0.0}): facing {soul.Facing:0}, x {soul.Position.x:0.00} (was {xTurn:0.00}), walking {soul.ScriptWalking}");
            }
        }
    }
}
