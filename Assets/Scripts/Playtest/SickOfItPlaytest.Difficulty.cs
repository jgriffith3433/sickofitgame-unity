using System;
using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.VideoFrameExtractor.Runtime;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The difficulty scenario (Docs/Story.md "How sick of it are you?"): New Game asks for Assist, Normal, Hard or Hell Loop with real clicks;
        /// the settings' Accessibility page toggles are clicked and saved; the bot clears Level 1 on Assist and on Normal (enemy shot speed
        /// measured on each: Assist's are slower), then plays reckless on Hell Loop with every accessibility option on (a hit lands for twice
        /// Normal's damage, shots have a rim and one hot colour, the hitbox dot shows, shake and flash are reduced, aim assist bends a shot onto an enemy).
        /// </summary>
        public const string DifficultyId = "difficulty";
        const float DifficultySeconds = 900f;

        sealed partial class Session
        {
            int diffPassed, diffFailed;

            void DiffCheck(string beat, bool ok, string detail)
            {
                if (ok) { diffPassed++; Run.Event("check", $"difficulty {beat}: ok ({detail})"); }
                else
                {
                    diffFailed++;
                    Run.Event("check", $"difficulty {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"difficulty {beat}: {detail}");
                }
            }

            /// <summary>Clicks New Game, then the named difficulty on the card that opens, until effect() is true.</summary>
            IEnumerator PickNewGame(string difficultyName, Func<bool> effect)
            {
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                yield return ClickUntil(labels.newGame, () => FindButton(difficultyName) != null || effect(), 30f);
                if (Run.Done || effect()) yield break;
                yield return ClickUntil(difficultyName, effect, 30f);
            }

            /// <summary>Every New Game click in the other scenarios goes through the card as Normal.</summary>
            IEnumerator ClickNewGame(Func<bool> effect) => PickNewGame(Difficulty.Of(DifficultyLevel.Normal).name, effect);

            readonly Dictionary<string, float> shotSpeedSum = new Dictionary<string, float>();
            readonly Dictionary<string, int> shotSpeedCount = new Dictionary<string, int>();
            readonly HashSet<int> shotsSeen = new HashSet<int>();
            readonly List<int> heroHits = new List<int>();
            bool heroHitSeen;

            void DiffTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "damage" && e.Subject == "player")
                {
                    heroHits.Add(e.Value);
                    heroHitSeen = true;
                    Run.Event("hero-hit", $"{e.Value} heart(s) from {e.Detail} at {e.Time:0.0}");
                }
            }

            void SampleShots(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || e.Team != Team.Enemy || e.Body == null || !shotsSeen.Add(e.Serial))
                        continue;
                    if (e.FindAbility<ProjectileAbility>() == null)
                        continue;
                    float speed = e.Body.linearVelocity.magnitude;
                    if (speed < 0.1f)
                        continue;
                    shotSpeedSum.TryGetValue(e.DefId, out float sum);
                    shotSpeedCount.TryGetValue(e.DefId, out int n);
                    shotSpeedSum[e.DefId] = sum + speed;
                    shotSpeedCount[e.DefId] = n + 1;
                }
            }

            void ResetSamples()
            {
                shotSpeedSum.Clear(); shotSpeedCount.Clear(); shotsSeen.Clear(); heroHits.Clear(); heroHitSeen = false;
            }

            Dictionary<string, float> AverageShotSpeeds()
            {
                var result = new Dictionary<string, float>();
                foreach (var pair in shotSpeedCount)
                {
                    if (pair.Value >= 3)
                        result[pair.Key] = shotSpeedSum[pair.Key] / pair.Value;
                }
                return result;
            }

            IEnumerator PlayOnDifficulty(DifficultyLevel level, Func<bool> until = null, bool expectClear = true)
            {
                string name = Difficulty.Of(level).name;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return PickNewGame(name, () => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1));
                if (Run.Done) yield break;
                DiffCheck($"{name} is saved by the card", Difficulty.Current == level, $"Difficulty.Current = {Difficulty.Current}");
                yield return ReachLevel(1);
                if (Run.Done) yield break;
                ResetSamples();
                GameplayTelemetry.Recorded += DiffTelemetry;
                try
                {
                    yield return PlayLevel(1, 0f, during: now =>
                    {
                        var w = GameplayWorld.Instance;
                        if (w != null)
                            SampleShots(w);
                        return null;
                    }, until: until);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= DiffTelemetry;
                }
                if (Run.Done) yield break;
                var speeds = AverageShotSpeeds();
                foreach (var pair in speeds)
                    Run.Event("shot-speed", $"{name}: {pair.Key} {pair.Value:0.00} u/s over {shotSpeedCount[pair.Key]} shots");
                if (expectClear)
                {
                    DiffCheck($"the bot clears Level 1 on {name}", lastOutcome == "completed", $"outcome \"{lastOutcome}\"; hits taken {heroHits.Count}");
                    Run.Metric($"difficulty.{name}.cleared", lastOutcome == "completed" ? 1 : 0);
                }
                Run.Metric($"difficulty.{name}.hits", heroHits.Count);
            }

            public IEnumerator DifficultyRun()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                var labels = GameDefinition.Instance.mainMenuConfig.labels;

                // 1. The card at New Game offers all four, Back returns to the menu.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(labels.newGame, () => FindButton("Hell Loop") != null, 15f);
                if (Run.Done) yield break;
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    DiffCheck($"the card offers {Difficulty.Of(level).name}", FindButton(Difficulty.Of(level).name) != null, "a button on the New Game card");
                yield return ClickUntil(texts.difficultyBack, () => FindButton("Hell Loop") == null, 10f);
                if (Run.Done) yield break;
                DiffCheck("Back closes the card without starting", FindButton(labels.newGame) != null && LevelRunning(1) == false, "main menu still up");

                // The numbers behind the cards.
                var assist = Difficulty.Of(DifficultyLevel.Assist);
                var normal = Difficulty.Of(DifficultyLevel.Normal);
                var hard = Difficulty.Of(DifficultyLevel.Hard);
                var hell = Difficulty.Of(DifficultyLevel.HellLoop);
                DiffCheck("Normal changes nothing", Mathf.Approximately(normal.bulletSpeed, 1f) && Mathf.Approximately(normal.damageTaken, 1f) && Mathf.Approximately(normal.juice, 1f) && Mathf.Approximately(normal.density, 1f), "all 1.0");
                DiffCheck("the ladder runs Assist < Normal < Hard < Hell Loop", assist.bulletSpeed < normal.bulletSpeed && normal.bulletSpeed < hard.bulletSpeed && hard.bulletSpeed < hell.bulletSpeed
                    && assist.damageTaken < normal.damageTaken && normal.damageTaken < hard.damageTaken && hard.damageTaken < hell.damageTaken
                    && assist.juice > normal.juice && normal.juice > hard.juice && hard.juice > hell.juice, "bullet speed and damage rise, rewind juice falls");

                // 2. Accessibility toggles, clicked and saved.
                yield return ClickUntil(texts.settings, () => FindButton(texts.accessibility) != null, 15f);
                if (Run.Done) yield break;
                yield return ClickUntil(texts.accessibility, () => FindButtonInRow(texts.aimAssist, "Off") != null, 10f);
                if (Run.Done) yield break;
                var rows = new (string row, Func<bool> get, string prefs)[]
                {
                    (texts.bulletOutlines, () => GameSettings.BulletOutlines, "Settings_BulletOutlines"),
                    (texts.highContrastBullets, () => GameSettings.HighContrastBullets, "Settings_HighContrast"),
                    (texts.reducedFlash, () => GameSettings.ReducedFlash, "Settings_ReducedFlash"),
                    (texts.reducedShake, () => GameSettings.ReducedShake, "Settings_ReducedShake"),
                    (texts.hitboxDot, () => GameSettings.HitboxDot, "Settings_HitboxDot"),
                    (texts.aimAssist, () => GameSettings.AimAssist, "Settings_AimAssist"),
                };
                foreach (var r in rows)
                {
                    var button = FindButtonInRow(r.row, "Off");
                    if (button == null)
                    {
                        DiffCheck($"{r.row} can be toggled", false, "no Off button in its row");
                        continue;
                    }
                    yield return ClickButton(button, r.row);
                    yield return WaitReal(0.2f);
                    DiffCheck($"{r.row} turns on and is saved", r.get() && PlayerPrefs.GetFloat(r.prefs, 0f) > 0.5f, $"setting {r.get()}, PlayerPrefs {PlayerPrefs.GetFloat(r.prefs, -1f)}");
                }
                // Escape closes the Accessibility page, then Settings (each overlay's Back).
                yield return Tap(Key.Escape);
                yield return WaitReal(0.6f);
                yield return Tap(Key.Escape);
                yield return WaitReal(0.6f);
                DiffCheck("the settings close", FindButton(texts.done) == null, "no Done button left on screen");

                // 3. Assist, then Normal: the bot clears Level 1 on both. Accessibility on Assist: aim assist comes on with the pick.
                GameSettings.AimAssist = false;
                GameSettings.BulletOutlines = false; GameSettings.HighContrastBullets = false; GameSettings.ReducedFlash = false;
                GameSettings.ReducedShake = false; GameSettings.HitboxDot = false;
                yield return PlayOnDifficulty(DifficultyLevel.Assist);
                if (Run.Done) yield break;
                DiffCheck("Assist turns aim assist on", GameSettings.AimAssist, $"AimAssist {GameSettings.AimAssist}");
                var assistSpeeds = AverageShotSpeeds();
                int assistHits = heroHits.Count;
                GameSettings.AimAssist = false;

                yield return PlayOnDifficulty(DifficultyLevel.Normal);
                if (Run.Done) yield break;
                var normalSpeeds = AverageShotSpeeds();
                int compared = 0;
                foreach (var pair in normalSpeeds)
                {
                    if (!assistSpeeds.TryGetValue(pair.Key, out float slow))
                        continue;
                    compared++;
                    float ratio = slow / pair.Value;
                    DiffCheck($"Assist's {pair.Key} flies slower than Normal's", Mathf.Abs(ratio - assist.bulletSpeed) < 0.08f,
                        $"{slow:0.00} vs {pair.Value:0.00} u/s = x{ratio:0.00} (want x{assist.bulletSpeed:0.00})");
                    Run.Metric($"difficulty.speedRatio.{pair.Key}", ratio);
                }
                DiffCheck("a shot type was measured on both", compared > 0, $"{compared} shot types seen 3+ times on both");
                int normalBase = heroHits.Count > 0 ? heroHits[0] : 1;

                // 4. Hell Loop with every accessibility option on, reckless until the first hit.
                GameSettings.BulletOutlines = true; GameSettings.HighContrastBullets = true; GameSettings.ReducedFlash = true;
                GameSettings.ReducedShake = true; GameSettings.HitboxDot = true; GameSettings.AimAssist = true; GameSettings.ScreenShake = true;
                reckless = true;
                bool checkedVisuals = false;
                int hellHits = 0;
                yield return PlayOnDifficulty(DifficultyLevel.HellLoop, until: () =>
                {
                    var w = GameplayWorld.Instance;
                    if (w == null || w.Player == null)
                        return false;
                    if (!checkedVisuals)
                        checkedVisuals = DiffVisuals(w);
                    return heroHitSeen && checkedVisuals;
                }, expectClear: false);
                reckless = false;
                hellHits = heroHits.Count;
                if (Run.Done) yield break;
                DiffCheck("Hell Loop's hit lands for twice Normal's", hellHits > 0 && heroHits[0] == normalBase * 2, $"first hit {(hellHits > 0 ? heroHits[0] : 0)} vs Normal's {normalBase} (x2)");
                DiffCheck("Hell Loop's shots are faster than Normal's", AverageShotSpeedRatio(normalSpeeds) > 1.2f, $"x{AverageShotSpeedRatio(normalSpeeds):0.00} (want x{hell.bulletSpeed:0.00})");
                Run.Event("difficulty", $"hits taken: Assist {assistHits}, Normal {normalBase} hearts for the first");

                // Put the player's settings back.
                GameSettings.BulletOutlines = false; GameSettings.HighContrastBullets = false; GameSettings.ReducedFlash = false;
                GameSettings.ReducedShake = false; GameSettings.HitboxDot = false; GameSettings.AimAssist = false;
                Difficulty.Current = DifficultyLevel.Normal;
                input.ReleaseAll();
                Run.Metric("difficulty.passed", diffPassed);
                Run.Metric("difficulty.failed", diffFailed);
                Run.Progress(1f);
                End(diffFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            float AverageShotSpeedRatio(Dictionary<string, float> normalSpeeds)
            {
                float best = 0f;
                foreach (var pair in AverageShotSpeeds())
                {
                    if (normalSpeeds.TryGetValue(pair.Key, out float n) && n > 0f)
                        best = Mathf.Max(best, pair.Value / n);
                }
                return best;
            }

            bool DiffVisuals(GameplayWorld w)
            {
                BulletReadability shot = null;
                Entity target = null;
                foreach (var e in w.Live)
                {
                    if (e == null || !e.IsAlive || e.Team != Team.Enemy)
                        continue;
                    if (shot == null && e.FindAbility<ProjectileAbility>() != null && e.TryGetComponent<BulletReadability>(out var r))
                        shot = r;
                    else if (target == null && e.MaxHealth > 0)
                        target = e;
                }
                if (shot == null || target == null || w.Hud == null)
                    return false;
                DiffCheck("bullets have a rim", shot.RimShown, "enemy shot's Rim renderer on");
                DiffCheck("bullets are one high-contrast colour", shot.HighContrast, "BulletReadability.HighContrast");
                var dot = w.Player.GetComponentInChildren<HitboxDot>();
                DiffCheck("the hitbox dot shows", dot != null && dot.Shown, dot != null ? "dot on" : "no HitboxDot on the hero");
                w.Effects.Shake(1f, 0.05f);
                DiffCheck("reduced shake softens a shake", w.Effects.LastShakeIntensity < 0.5f, $"asked 1.0, got {w.Effects.LastShakeIntensity:0.00}");
                w.Hud.Flash(Color.white, 0.05f);
                DiffCheck("reduced flash dims a flash", w.Hud.LastFlashAlpha < 0.5f, $"asked 1.0, got {w.Hud.LastFlashAlpha:0.00}");
                Vector2 from = w.Player.Position;
                Vector2 toEnemy = target.Position - from;
                if (toEnemy.magnitude < GameplayWorld.AssistRange)
                {
                    Vector2 off = Quaternion.Euler(0f, 0f, 5f) * toEnemy.normalized;
                    Vector2 bent = w.AssistAim(from, off);
                    float before = Vector2.Angle(off, toEnemy), after = Vector2.Angle(bent, toEnemy);
                    DiffCheck("aim assist bends a near-miss onto the enemy", after < before * 0.5f, $"{before:0.0} deg off, {after:0.0} deg after");
                }
                else
                    Run.Event("check", "difficulty aim assist: enemy out of range, not measured");
                return true;
            }
        }
    }
}
