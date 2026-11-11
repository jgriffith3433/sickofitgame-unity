using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The feel scenario (Docs/Story.md "How sick of it are you?", the quick hero): Level 1 loaded directly (setup shortcut), real input.
        /// Reports the time to cross the screen holding D (2.5-3.2 s), input-to-motion in ms (&lt; 100), the safe time after a hit on
        /// Normal (1.0-1.5 s, then he can be hit again), the dash from the first second (Shift, and a right-half swipe on a touchscreen
        /// that leaves the stick alone) passing through an ember unharmed, and the hitbox dot on the hero. Metrics feel.*.
        /// </summary>
        public const string FeelId = "feel";
        const int FeelLevel = 1;
        const float FeelSeconds = 150f;

        sealed partial class Session
        {
            int feelPassed, feelFailed;
            int feelDashes;

            void FeelCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    feelPassed++;
                    Run.Event("check", $"feel {beat}: ok ({detail})");
                }
                else
                {
                    feelFailed++;
                    Run.Event("check", $"feel {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"feel {beat}: {detail}");
                }
            }

            void FeelOnDashed(PlayerController who) => feelDashes++;

            public IEnumerator FeelRun()
            {
                Difficulty.Current = DifficultyLevel.Normal;
                Run.Event("shortcut", $"level {FeelLevel} loaded directly");
                var load = LoadLevel(FeelLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(FeelLevel) && GameplayWorld.Instance != null && GameplayWorld.Instance.PlayerController != null, 60f, "the level");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var hero = world.PlayerController;
                var health = hero.GetComponent<PlayerHealth>();
                yield return Run.Until(() => hero.IsGrounded, 20f, "the hero on the ground");
                yield return WaitReal(0.5f);
                Run.Progress(0.05f);
                PlayerController.Dashed += FeelOnDashed;
                try
                {
                    // 1. The hitbox dot is on in Level 1 without touching Settings.
                    var dot = hero.GetComponentInChildren<HitboxDot>();
                    yield return null;
                    FeelCheck("hitbox dot shows in Level 1", dot != null && dot.Shown, dot != null ? "dot " + (dot.Shown ? "on" : "off") : "no HitboxDot on the hero");

                    // 2. Input to motion: D pressed on a frame, the first frame the hero's place has moved.
                    float x0 = hero.transform.position.x;
                    float t0 = Time.realtimeSinceStartup;
                    input.SetKey(Key.D, true);
                    float motionMs = -1f;
                    while (!Run.Done && Time.realtimeSinceStartup - t0 < 1f)
                    {
                        yield return null;
                        if (Mathf.Abs(hero.transform.position.x - x0) > 0.004f)
                        {
                            motionMs = (Time.realtimeSinceStartup - t0) * 1000f;
                            break;
                        }
                    }
                    Run.Metric("feel.inputToMotionMs", motionMs);
                    FeelCheck("input to motion under 100 ms", motionMs > 0f && motionMs < 100f, $"{motionMs:0} ms");

                    // 3. Crossing the screen holding D: the time to cover one screen width.
                    var cam = Camera.main;
                    float width = cam != null && cam.orthographic ? cam.orthographicSize * 2f * cam.aspect : 10f;
                    float startX = x0;
                    float startT = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
                    float cross = -1f;
                    float giveUp = Time.realtimeSinceStartup + 8f;
                    while (!Run.Done && Time.realtimeSinceStartup < giveUp)
                    {
                        if (hero.transform.position.x - startX >= width)
                        {
                            cross = (GameClock.Instance != null ? GameClock.Instance.Time : Time.time) - startT;
                            break;
                        }
                        yield return null;
                    }
                    input.SetKey(Key.D, false);
                    Run.Metric("feel.screenWidthUnits", width);
                    Run.Metric("feel.crossSeconds", cross);
                    Run.Event("feel", $"crossed {width:0.00} units in {cross:0.00} s ({(cross > 0f ? width / cross : 0f):0.00} u/s incl. the ramp)");
                    FeelCheck("crossing the screen takes 2.5-3.2 s", cross >= 2.5f && cross <= 3.2f, $"{cross:0.00} s for {width:0.0} units");
                    yield return WaitReal(0.4f);
                    yield return Run.Until(() => hero.IsGrounded, 10f, "the hero on the ground");

                    // 4. A hit stings, it does not grant 5 s: the safe time after it, then he can be hit again.
                    if (health != null)
                    {
                        health.SetHealth(health.MaxHealth);
                        health.SetInvincibilityTimer(0f);
                        yield return null;
                        int before = health.CurrentHealth;
                        health.TakeDamage(1);
                        float hitAt = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
                        bool hurt = health.CurrentHealth < before;
                        float grace = -1f;
                        float limit = Time.realtimeSinceStartup + 4f;
                        while (!Run.Done && Time.realtimeSinceStartup < limit)
                        {
                            yield return null;
                            if (!health.IsInvincible)
                            {
                                grace = (GameClock.Instance != null ? GameClock.Instance.Time : Time.time) - hitAt;
                                break;
                            }
                        }
                        Run.Metric("feel.invulnerableSeconds", grace);
                        FeelCheck("the hit lands", hurt, $"hearts {before} -> {health.CurrentHealth + (hurt ? 0 : 0)}");
                        FeelCheck("post-hit invulnerable 1.0-1.5 s on Normal", grace >= 1.0f && grace <= 1.5f, $"{grace:0.00} s");
                        int again = health.CurrentHealth;
                        health.TakeDamage(1);
                        FeelCheck("he can be hit again after it", health.CurrentHealth < again, $"hearts {again} -> {health.CurrentHealth}");
                        health.SetHealth(health.MaxHealth);
                        health.SetInvincibilityTimer(0f);
                    }
                    Run.Progress(0.5f);

                    // 5. The dash from the first second: Shift through an ember, then a right-half swipe through another.
                    FeelCheck("the dash is unlocked in Level 1", hero.DashUnlocked, "DashUnlocked " + hero.DashUnlocked);
                    yield return FeelDashThrough(world, hero, health, "Shift", () => FeelPressShift());
                    yield return WaitReal(1.2f);
                    yield return FeelDashThrough(world, hero, health, "right-half swipe", () => FeelSwipe(hero));
                    Run.Metric("feel.hapticsWanted", SickOfIt.Gameplay.Haptics.Wanted);
                }
                finally
                {
                    PlayerController.Dashed -= FeelOnDashed;
                    input.SetKey(Key.D, false);
                    input.SetKey(Key.LeftShift, false);
                }
                Run.Metric("feel.passed", feelPassed);
                Run.Metric("feel.failed", feelFailed);
                Run.Progress(1f);
                End(feelFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator FeelPressShift()
            {
                input.SetKey(Key.LeftShift, true);
                yield return null;
                yield return null;
                input.SetKey(Key.LeftShift, false);
            }

            IEnumerator FeelSwipe(PlayerController hero)
            {
                float shortSide = Mathf.Min(Screen.width, Screen.height);
                var start = new Vector2(Screen.width * 0.75f, Screen.height * 0.45f);
                int id = input.TouchDown(start);
                yield return null;
                input.TouchMove(id, start + new Vector2(shortSide * 0.04f, 0f));
                yield return null;
                input.TouchMove(id, start + new Vector2(shortSide * 0.16f, 0f));
                yield return null;
                yield return null;
                bool stickIdle = Mathf.Abs(hero.TouchMove) < 0.001f;
                input.TouchUp(id, start + new Vector2(shortSide * 0.16f, 0f));
                FeelCheck("the swipe leaves the move stick alone", stickIdle, $"TouchMove {hero.TouchMove:0.00}");
            }

            IEnumerator FeelDashThrough(GameplayWorld world, PlayerController hero, PlayerHealth health, string how, System.Func<IEnumerator> press)
            {
                if (health == null)
                    yield break;
                health.SetHealth(health.MaxHealth);
                health.SetInvincibilityTimer(0f);
                yield return null;
                int hp = health.CurrentHealth;
                int dashesBefore = feelDashes;
                var pos = (Vector2)hero.transform.position;
                var opt = new SickOfIt.Gameplay.SpawnOptions { Velocity = new Vector2(-8f, 0f), Team = Team.Enemy, FacePlayer = false };
                var ember = world.Spawn("demonEmber", pos + new Vector2(3.2f, 0.05f), opt);
                if (ember == null)
                {
                    FeelCheck($"{how}: an ember spawns", false, "world.Spawn(demonEmber) returned null");
                    yield break;
                }
                float giveUp = Time.realtimeSinceStartup + 3f;
                while (!Run.Done && ember.IsAlive && ember.Position.x - hero.transform.position.x > 1.6f && Time.realtimeSinceStartup < giveUp)
                    yield return null;
                yield return press();
                float until = Time.realtimeSinceStartup + 0.6f;
                bool invulnerable = false;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    invulnerable |= hero.DashInvulnerable;
                    yield return null;
                }
                bool passed = ember.IsAlive && ember.Position.x < hero.transform.position.x;
                Run.Event("dash", $"{how}: dashes {feelDashes - dashesBefore}, ember at x {ember.Position.x:0.00}, hero x {hero.transform.position.x:0.00}, hearts {hp} -> {health.CurrentHealth}");
                FeelCheck($"{how} dashes", feelDashes == dashesBefore + 1 && invulnerable, $"{feelDashes - dashesBefore} dash(es), untouchable seen {invulnerable}");
                FeelCheck($"{how}: through the ember without damage", health.CurrentHealth == hp && passed, $"hearts {hp} -> {health.CurrentHealth}, ember behind him {passed}");
            }
        }
    }
}
