using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Inked HUD (Docs/Story.md "The HUD and menus"): illustrated vessels, reel, gem and dial in two corners;
        /// crack ≥0.3 s on a hit, reel spins while rewinding, gem pulses within 0.2 s of a diamond; on phone 390x844
        /// the clusters stay in the safe area, cover ≤12% of the playfield, and the rewind button is ≥72 px.
        /// </summary>
        public const string HudInkId = "hud-ink";
        const float HudInkSeconds = 180f;

        sealed partial class Session
        {
            int hudInkPassed, hudInkFailed;

            void HudInkCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    hudInkPassed++;
                    Run.Event("check", $"hud-ink {beat}: ok ({detail})");
                }
                else
                {
                    hudInkFailed++;
                    Run.Event("check", $"hud-ink {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"hud-ink {beat}: {detail}");
                }
            }

            public IEnumerator HudInkRun()
            {
                hudInkPassed = hudInkFailed = 0;
                bool touchBefore = GameplayHud.TouchUiForced;
                // Phone criterion: force touch UI so the rewind button is the big bottom-right control.
                GameplayHud.TouchUiForced = true;
                Run.Metric("screenWidth", Screen.width);
                Run.Metric("screenHeight", Screen.height);
                Run.Event("hud-ink", $"screen {Screen.width}x{Screen.height}, safe {Screen.safeArea}, touch UI on");

                try
                {
                    Run.Event("shortcut", "load Floor 1000 (level 2) directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(2);
                    if (load != null) yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(2), 60f, "Floor 1000 to start");
                    if (Run.Done) yield break;
                    yield return WaitReal(1.2f);

                    var world = GameplayWorld.Instance;
                    var hud = world != null ? world.Hud : null;
                    HudInkCheck("ink HUD built", hud != null && hud.InkHudActive, hud == null ? "no HUD" : "clusters up");
                    if (hud == null || !hud.InkHudActive)
                    {
                        End(PlaytestOutcome.Error);
                        yield break;
                    }

                    var safe = Screen.safeArea;
                    var left = hud.LeftClusterScreenRect;
                    var right = hud.RightClusterScreenRect;
                    var health = hud.HealthClusterScreenRect;
                    var reel = hud.ReelScreenRect;
                    var gem = hud.GemScreenRect;
                    var dial = hud.DialScreenRect;
                    var rewind = hud.RewindButtonScreenRect;

                    bool InSafe(Rect r) => r.width > 1f && r.xMin >= safe.xMin - 2f && r.xMax <= safe.xMax + 2f
                                           && r.yMin >= safe.yMin - 2f && r.yMax <= safe.yMax + 2f;
                    HudInkCheck("hearts illustrated", health.width > 8f && health.height > 8f, $"health {health}");
                    HudInkCheck("rewind reel illustrated", reel.width > 8f, $"reel {reel}");
                    HudInkCheck("diamonds illustrated", gem.width > 8f, $"gem {gem}");
                    HudInkCheck("floor dial illustrated", dial.width > 8f || !world.Stage.HasFloors, $"dial {dial}");
                    HudInkCheck("left cluster top-left", left.xMin < Screen.width * 0.35f && left.yMax > Screen.height * 0.7f, $"left {left}");
                    HudInkCheck("right cluster top-right", right.xMax > Screen.width * 0.65f && right.yMax > Screen.height * 0.7f, $"right {right}");
                    HudInkCheck("clusters in safe area", InSafe(left) && InSafe(right), $"safe {safe}");

                    float cover = hud.InkHudCoverageOfPlayfield();
                    Run.Metric("hudInk.coverage", cover);
                    HudInkCheck("HUD covers <=12% of playfield", cover <= 0.12f + 0.005f, $"{cover * 100f:0.0}%");

                    float rewindPx = Mathf.Min(rewind.width, rewind.height);
                    Run.Metric("hudInk.rewindButtonPx", rewindPx);
                    HudInkCheck("rewind button >=72 px", rewindPx + 0.5f >= GameplayHud.MinRewindButtonPx, $"{rewindPx:0} px at {rewind}");

                    // Crack: take a heart through real damage.
                    var hero = world.Player;
                    var healthComp = hero != null ? hero.GetComponentInChildren<PlayerHealth>() : null;
                    if (healthComp != null)
                    {
                        healthComp.SetHealth(Mathf.Max(2, healthComp.MaxHealth));
                        healthComp.SetInvincibilityTimer(0f);
                        yield return null;
                        int before = healthComp.CurrentHealth;
                        healthComp.TakeDamage(1);
                        float crackStart = Time.unscaledTime;
                        bool sawCrack = false;
                        float crackHeld = 0f;
                        float limit = Time.realtimeSinceStartup + 1.5f;
                        while (!Run.Done && Time.realtimeSinceStartup < limit)
                        {
                            yield return null;
                            if (hud.VesselCracking)
                            {
                                sawCrack = true;
                                crackHeld = Time.unscaledTime - crackStart;
                            }
                            else if (sawCrack)
                                break;
                        }
                        Run.Metric("hudInk.crackSeconds", crackHeld);
                        HudInkCheck("heart crack >=0.3 s", sawCrack && crackHeld >= 0.3f - 0.02f,
                            $"cracked {sawCrack}, held {crackHeld:0.00}s (hearts {before}->{healthComp.CurrentHealth})");
                        healthComp.SetHealth(healthComp.MaxHealth);
                        healthComp.SetInvincibilityTimer(0f);
                    }
                    else
                        HudInkCheck("heart crack >=0.3 s", false, "no PlayerHealth");

                    // Gem pulse: add diamonds.
                    var roguelite = RogueliteManager.Instance;
                    if (roguelite != null)
                    {
                        int pts = roguelite.GetPoints();
                        roguelite.AddPoints(3);
                        float pulseStart = Time.unscaledTime;
                        yield return null;
                        yield return null;
                        float age = hud.GemPulseAge;
                        Run.Metric("hudInk.gemPulseAge", age);
                        HudInkCheck("diamond gem pulses within 0.2 s", age <= 0.2f + 0.05f, $"pulse age {age:0.00}s after +3 (was {pts})");
                    }
                    else
                        HudInkCheck("diamond gem pulses within 0.2 s", false, "no RogueliteManager");

                    // Reel spins while rewinding: top up juice and hold R.
                    var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
                    if (budget != null)
                        budget.Reward(8f, "playtest");
                    input.SetKey(Key.R, true);
                    yield return WaitReal(0.35f);
                    float spin = Mathf.Abs(hud.ReelSpinSpeed);
                    float fill = hud.ReelFillAmount;
                    Run.Metric("hudInk.reelSpin", spin);
                    Run.Metric("hudInk.reelFill", fill);
                    HudInkCheck("reel spins while rewinding", spin > 10f, $"spin {spin:0}/s fill {fill:0.00}");
                    input.SetKey(Key.R, false);
                    yield return WaitReal(0.25f);
                    HudInkCheck("reel stops after rewind", Mathf.Abs(hud.ReelSpinSpeed) < 1f, $"spin {hud.ReelSpinSpeed:0}/s");

                    Run.Metric("hudInk.bakedArt", HudInkAtlas.HasBakedArt ? 1 : 0);
                    Run.Event("hud-ink", HudInkAtlas.HasBakedArt ? "Gen atlas loaded" : "procedural stand-ins (Gen atlas pending)");
                    Run.Metric("hudInk.passed", hudInkPassed);
                    Run.Metric("hudInk.failed", hudInkFailed);
                    Run.Progress(1f);
                    End(hudInkFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
                }
                finally
                {
                    input.SetKey(Key.R, false);
                    GameplayHud.TouchUiForced = touchBefore;
                }
            }
        }
    }
}
