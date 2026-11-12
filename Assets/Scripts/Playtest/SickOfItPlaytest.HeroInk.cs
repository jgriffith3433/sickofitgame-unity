using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Hero ink redraw acceptance (docs/art-bible.md): Idle, Walk, Jump, Fall, Land, Dash, Hurt and DeathBurn each
        /// show as their own state through real input on the moves bench; flat-ground Walk logs planted-foot screen drift
        /// (≤ 2 px per step); at 390x844 the hero is ≥ 48 px tall on screen. Named for Studio's review of the redraw.
        /// </summary>
        public const string HeroInkId = "hero-ink";
        const float HeroInkSeconds = 300f;
        const float HeroInkFootSlidePxLimit = 2f;
        const float HeroInkPhoneMinPx = 48f;

        sealed partial class Session
        {
            int inkPassed, inkFailed;
            float inkMaxFootSlidePx;
            float inkHeroScreenPx;
            readonly HashSet<string> inkStatesSeen = new HashSet<string>();

            void InkCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    inkPassed++;
                    Run.Event("check", $"hero-ink {beat}: ok ({detail})");
                }
                else
                {
                    inkFailed++;
                    Run.Event("check", $"hero-ink {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"hero-ink {beat}: {detail}");
                }
            }

            public IEnumerator HeroInkRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(MovesScene) || !content.Entities.ContainsKey("wallSlab"))
                {
                    Run.Problem(PlaytestSeverity.Bug, "hero-ink: moveTest bench or wallSlab is missing");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                bool phone = Screen.height >= 800 && Screen.width <= 500;
                if (phone)
                    GameplayHud.TouchUiForced = true;

                Application.onBeforeRender += AnimSample;
                try
                {
                    UnlockFlags.Reset();
                    RogueliteManager.Instance.ResetAllRogueliteData();
                    UnlockFlags.Set(HeroMoves.DashFlag);
                    UnlockFlags.Set(HeroMoves.WallJumpFlag);
                    WeaponProgress.Reset();
                    WeaponUnlocks.Reset();

                    yield return AnimOpenBench(MovesScene, MovesLevel, "hero-ink: inked set on moves bench");
                    if (Run.Done) yield break;

                    var world = GameplayWorld.Instance;
                    var pc = world.PlayerController;
                    var body = pc.GetComponent<Rigidbody2D>();
                    var cam = Camera.main != null ? Camera.main : world.Camera;

                    MeasureHeroScreenHeight(pc, cam);
                    InkCheck("phone height", !phone || inkHeroScreenPx >= HeroInkPhoneMinPx,
                        phone
                            ? $"hero {inkHeroScreenPx:0.0} px tall on {Screen.width}x{Screen.height} (need >= {HeroInkPhoneMinPx})"
                            : $"desktop {inkHeroScreenPx:0.0} px tall (phone check skipped at {Screen.width}x{Screen.height})");
                    Run.Metric("heroInk.screenPx", inkHeroScreenPx);
                    Run.Metric("heroInk.screenHeight", Screen.height);
                    Run.Metric("heroInk.screenWidth", Screen.width);

                    // Idle
                    var idle = AnimArm("Idle", () => AnimPlain(pc) && pc.IsGrounded && Mathf.Abs(body.linearVelocity.x) < 0.1f);
                    yield return AnimWait(1.2f, idle);
                    AnimJudgeInk(idle, "Idle", "standing still");

                    // Walk + foot-slide sample on flat ground
                    yield return MovesWalkTo(-6f);
                    var walk = AnimArm("Walk", () => AnimPlain(pc) && pc.IsGrounded && Mathf.Abs(body.linearVelocity.x) > 1f);
                    input.SetKey(Key.D, true);
                    yield return SampleFootSlide(pc, body, cam, 1.8f);
                    input.SetKey(Key.D, false);
                    AnimJudgeInk(walk, "Walk", "running flat");
                    InkCheck("foot slide", inkMaxFootSlidePx <= HeroInkFootSlidePxLimit,
                        $"max planted-foot screen drift {inkMaxFootSlidePx:0.00} px (limit {HeroInkFootSlidePxLimit})");
                    Run.Metric("heroInk.footSlidePx", inkMaxFootSlidePx);
                    if (inkMaxFootSlidePx > HeroInkFootSlidePxLimit)
                        Run.Problem(PlaytestSeverity.Bug, $"quality.foot-slide: planted foot drifted {inkMaxFootSlidePx:0.00} px on flat Walk");

                    // Match Walk fps to moveSpeed (docs/art-bible.md section 6)
                    float moveSpeed = GameDefinition.Instance != null && GameDefinition.Instance.player != null
                        ? GameDefinition.Instance.player.moveSpeed : 3.8f;
                    float walkFps = WalkFpsFromConfig();
                    int walkFrames = WalkFrameCountFromConfig();
                    float loopSec = walkFrames > 0 && walkFps > 0f ? walkFrames / walkFps : 0f;
                    float stride = loopSec > 0f ? moveSpeed * loopSec / 2f : 0f;
                    Run.Metric("heroInk.moveSpeed", moveSpeed);
                    Run.Metric("heroInk.walkFps", walkFps);
                    Run.Metric("heroInk.walkFrames", walkFrames);
                    Run.Metric("heroInk.strideUnits", stride);
                    Run.Event("stride", $"Walk {walkFrames} frames @ {walkFps:0.#} fps, moveSpeed {moveSpeed:0.##} -> stride {stride:0.###} u/step");

                    // Jump + Fall + Land
                    var jump = AnimArm("Jump", () => AnimPlain(pc) && !pc.IsGrounded && body.linearVelocity.y > 0.5f);
                    var fall = AnimArm("Fall", () => AnimPlain(pc) && !pc.IsGrounded && body.linearVelocity.y < -0.5f);
                    var land = AnimArm("Land", () =>
                        (pc.GripPose != null && pc.GripPose.IndexOf("Land", System.StringComparison.Ordinal) >= 0)
                        || pc.ShowsState("Land", out _));
                    input.SetKey(Key.Space, true);
                    yield return AnimWait(2.5f, jump, fall);
                    input.SetKey(Key.Space, false);
                    AnimJudgeInk(jump, "Jump", "going up");
                    AnimJudgeInk(fall, "Fall", "coming down");
                    yield return AnimWait(1.2f, land);
                    AnimJudgeInk(land, "Land", "squash on landing");
                    yield return Run.Until(() => pc.IsGrounded, 4f, "landed");

                    // Dash
                    var dash = AnimArm("Dash", () => pc.MovePose == "Dash" || pc.ShowsState("Dash", out _));
                    input.SetKey(Key.LeftShift, true);
                    yield return AnimWait(1.2f, dash);
                    input.SetKey(Key.LeftShift, false);
                    AnimJudgeInk(dash, "Dash", "dashing");

                    // Hurt then DeathBurn on Level 1 (moveTest has no hostiles)
                    Run.Progress(0.55f);
                    yield return InkHurtAndDeath();

                    InkCheck("eight states", inkStatesSeen.Count >= 8,
                        $"saw {inkStatesSeen.Count} distinct states: {string.Join(", ", inkStatesSeen)}");
                    Run.Metric("heroInk.states", inkStatesSeen.Count);
                    Run.Metric("heroInk.passed", inkPassed);
                    Run.Metric("heroInk.failed", inkFailed);
                    Run.Progress(1f);
                    End(inkFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
                }
                finally
                {
                    Application.onBeforeRender -= AnimSample;
                    GameplayHud.TouchUiForced = false;
                    input.ReleaseAll();
                    reckless = false;
                    animArmed.Clear();
                }
            }

            void AnimJudgeInk(AnimExpect expect, string state, string when)
            {
                AnimJudge(expect, when);
                if (expect.Seen)
                    inkStatesSeen.Add(state);
                // Mirror AnimJudge into hero-ink checks so Studio's criteria filter finds them.
                InkCheck(state, expect.Seen && !expect.SpriteMissed,
                    expect.Seen ? (expect.Detail ?? state) : $"{state} never shown ({when})");
            }

            void MeasureHeroScreenHeight(PlayerController pc, Camera cam)
            {
                inkHeroScreenPx = 0f;
                if (pc == null || cam == null) return;
                var sr = AnimRenderer(pc);
                if (sr == null || !sr.enabled) return;
                var b = sr.bounds;
                float y0 = cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
                float y1 = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y;
                inkHeroScreenPx = Mathf.Abs(y1 - y0);
                Run.Event("size", $"hero on screen {inkHeroScreenPx:0.0} px tall (bounds {b.size.y:0.00} world)");
            }

            /// <summary>
            /// While Walk plays on flat ground, sample the sprite feet (bounds bottom-centre) in screen space.
            /// Residual after subtracting the body's screen motion is foot slide; keep the max over the window.
            /// </summary>
            IEnumerator SampleFootSlide(PlayerController pc, Rigidbody2D body, Camera cam, float seconds)
            {
                inkMaxFootSlidePx = 0f;
                float until = Time.realtimeSinceStartup + seconds;
                bool havePrev = false;
                Vector2 prevFootScreen = Vector2.zero;
                Vector2 prevBodyScreen = Vector2.zero;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    if (pc != null && pc.IsGrounded && cam != null && Mathf.Abs(body.linearVelocity.x) > 1f)
                    {
                        var sr = AnimRenderer(pc);
                        if (sr != null)
                        {
                            var b = sr.bounds;
                            Vector2 foot = cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y, 0f));
                            Vector2 bodyScr = cam.WorldToScreenPoint(pc.transform.position);
                            if (havePrev)
                            {
                                float residual = Mathf.Abs((foot - prevFootScreen).x - (bodyScr - prevBodyScreen).x);
                                if (residual > inkMaxFootSlidePx)
                                    inkMaxFootSlidePx = residual;
                            }
                            prevFootScreen = foot;
                            prevBodyScreen = bodyScr;
                            havePrev = true;
                        }
                    }
                    yield return null;
                }
                Run.Event("foot-slide", $"max residual {inkMaxFootSlidePx:0.00} px over {seconds:0.#}s Walk");
            }

            IEnumerator InkHurtAndDeath()
            {
                // Level 1 through real input, reckless: first hit must show Hurt, then a Death* clip.
                Run.Event("shortcut", "level 1 loaded directly for Hurt + Death");
                var old = GameplayWorld.Instance;
                var load = LoadLevel(1);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(1), 60f, "level 1 to start");
                if (Run.Done) yield break;

                bool hurtSeen = false, deathSeen = false;
                string hurtDetail = null, deathDetail = null;
                reckless = true;
                try
                {
                    yield return PlayLevel(1, 0f, null, 0f, 1f, 180f, waitOutDeath: true, until: () =>
                    {
                        string on = HeroClipOnScreen(out int frame);
                        if (on == "Hurt" && !hurtSeen)
                        {
                            hurtSeen = true;
                            hurtDetail = $"Hurt frame {frame}";
                            Run.Event("hero-ink", hurtDetail);
                        }
                        if (on != null && on.StartsWith("Death", System.StringComparison.Ordinal))
                        {
                            deathSeen = true;
                            deathDetail = $"{on} frame {frame}";
                            Run.Event("hero-ink", deathDetail);
                            return true;
                        }
                        return false;
                    });
                }
                finally
                {
                    reckless = false;
                    input.ReleaseAll();
                }
                InkCheck("Hurt", hurtSeen, hurtSeen ? hurtDetail : "no Hurt clip on the first hit");
                if (hurtSeen) inkStatesSeen.Add("Hurt");
                InkCheck("DeathBurn", deathSeen, deathSeen ? deathDetail : "no Death* clip on death");
                if (deathSeen) inkStatesSeen.Add("DeathBurn");
            }

            static float WalkFpsFromConfig()
            {
                var states = GameDefinition.Instance?.player?.animationConfig?.states;
                if (states == null) return 15f;
                foreach (var s in states)
                    if (s != null && s.stateName == "Walk") return s.fps > 0 ? s.fps : 15f;
                return 15f;
            }

            static int WalkFrameCountFromConfig()
            {
                var states = GameDefinition.Instance?.player?.animationConfig?.states;
                if (states == null) return 12;
                foreach (var s in states)
                {
                    if (s == null || s.stateName != "Walk") continue;
                    if (s.frameCount > 0) return s.frameCount;
                    // Sheet not imported here: art bible default for the inked Walk.
                    return 12;
                }
                return 12;
            }
        }
    }
}
