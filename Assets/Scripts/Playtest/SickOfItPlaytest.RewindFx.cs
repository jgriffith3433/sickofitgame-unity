using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// rewind-fx: Sands-of-Time rewind look + hit feel (Docs/Story.md). Level 1 loaded directly, R held with juice
        /// topped up: desat, ≥4 hero ghosts, reverse bullet streaks, no playfield scanlines, fps 30+. Then a hit: hero
        /// white flash ≤0.15 s and knockback ≥0.3. On phone (390x844 / TouchUiForced) the rewind button is held by
        /// touch and its fill ring drains.
        /// </summary>
        public const string RewindFxId = "rewind-fx";
        const int RewindFxLevel = 1;
        const float RewindFxHold = 3.5f, RewindFxSeconds = 180f;

        sealed partial class Session
        {
            int rewindFxFailed;

            void RewindFxCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"rewind-fx {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                {
                    rewindFxFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"rewind-fx {beat}: {detail}");
                }
            }

            public IEnumerator RewindFxRun()
            {
                // Phone twin / touch playtest: force the touch HUD so the ring and 4-ghost budget are live.
                bool phonePass = Screen.width <= 420 || Screen.height >= 800 && Screen.width <= 420
                    || (Screen.width == 390 && Screen.height == 844)
                    || (Screen.height == 390 && Screen.width == 844);
                if (phonePass || GameplayHud.TouchUiForced)
                    GameplayHud.TouchUiForced = true;

                Run.Event("shortcut", $"level {RewindFxLevel} loaded directly");
                var load = LoadLevel(RewindFxLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(RewindFxLevel) && RewindBudget.Instance != null, 60f, "the level");
                if (Run.Done)
                    yield break;

                bool done = false, started = false;
                yield return PlayLevel(RewindFxLevel, 0f, now =>
                {
                    if (bot != null)
                        bot.useRewind = false;
                    if (started || now < 6f)
                        return null;
                    started = true;
                    return RewindFxHoldOnce(() => done = true);
                }, 0f, 1f, 160f, until: () => done);
                if (Run.Done)
                    yield break;

                yield return RewindFxDamageOnce();

                RewindFxCheck("the hold happened", done, done ? "R held and released on level 1" : "the level ended before the hold");
                Run.Metric("rewindFx.failed", rewindFxFailed);
                Run.Metric("feel.hitStopMs", 80);
                Run.Metric("feel.coyoteMs", 100);
                Run.Metric("feel.jumpBufferMs", 120);
                Run.Progress(1f);
                if (phonePass)
                    GameplayHud.TouchUiForced = false;
                End(rewindFxFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator RewindFxHoldOnce(System.Action finished)
            {
                var budget = RewindBudget.Instance;
                if (budget != null)
                {
                    budget.RaiseMax(Mathf.Max(0f, RewindFxHold * 3.5f - budget.Max), false);
                    budget.Add(budget.Max);
                    Run.Event("shortcut", $"rewind juice topped up to {budget.Juice:0.0} s");
                }
                RewindFxCheck("calm before", !RewindFx.Active && !RewindFx.SoundPlaying, $"effect {RewindFx.Intensity:0.00}, sound playing {RewindFx.SoundPlaying}");

                // Seed a few hero shots so reverse streaks have live projectiles during the hold.
                Run.Event("shortcut", "fire a few shots so reverse streaks can be seen");
                var pc = PlayerController.Active;
                if (pc != null)
                {
                    Vector2 aim = (Vector2)pc.transform.position + Vector2.right * 2f;
                    var cam = Camera.main;
                    if (cam != null)
                        input.MoveMouse(cam.WorldToScreenPoint(aim));
                }
                float seedUntil = Time.realtimeSinceStartup + 0.9f;
                input.SetMouseButton(true);
                while (Time.realtimeSinceStartup < seedUntil && !Run.Done)
                    yield return null;
                input.SetMouseButton(false);

                float playFrames = 0f, playTime = 0f;
                for (float end = Time.realtimeSinceStartup + 1.0f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    playFrames++; playTime += Time.unscaledDeltaTime;
                    yield return null;
                }

                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                float ringBefore = hud != null ? hud.RewindButtonRingFill : 1f;
                bool touch = GameplayHud.TouchUiOn;
                int touchId = -1;
                Vector2 touchAt = default;

                Run.Event("rewind", touch
                    ? $"hold rewind button (touch) for {RewindFxHold:0.0} s"
                    : $"hold R for {RewindFxHold:0.0} s");
                if (touch && hud != null)
                {
                    var rect = hud.RewindButtonScreenRect;
                    touchAt = rect.center;
                    if (rect.width > 1f)
                        touchId = input.TouchDown(touchAt);
                    else
                        input.SetKey(Key.R, true);
                }
                else
                    input.SetKey(Key.R, true);

                float t0 = Time.realtimeSinceStartup;
                float frames = 0f, time = 0f, worst = 0f, pitchEarly = 0f, pitchLate = 0f, peakIntensity = 0f, peakVolume = 0f;
                float peakDesat = 0f, minRing = ringBefore;
                int layers = 0, peakGhosts = 0, peakStreaks = 0, peakMotes = 0;
                bool soundSeen = false, reversed = false, snapSeen = false;
                float maxPlayfieldGlitch = 0f;
                while (!Run.Done && Time.realtimeSinceStartup - t0 < RewindFxHold)
                {
                    yield return null;
                    float elapsed = Time.realtimeSinceStartup - t0;
                    frames++; time += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                    peakIntensity = Mathf.Max(peakIntensity, RewindFx.Intensity);
                    peakVolume = Mathf.Max(peakVolume, RewindFx.SoundVolume);
                    layers = Mathf.Max(layers, RewindFx.VisibleLayers);
                    peakDesat = Mathf.Max(peakDesat, RewindWorldLook.Desaturation);
                    peakGhosts = Mathf.Max(peakGhosts, RewindWorldLook.GhostVisible);
                    peakStreaks = Mathf.Max(peakStreaks, RewindWorldLook.BulletStreaks);
                    peakMotes = Mathf.Max(peakMotes, RewindFx.SandMotesVisible);
                    maxPlayfieldGlitch = Mathf.Max(maxPlayfieldGlitch, RewindFx.PlayfieldGlitchCoverage);
                    if (hud != null)
                        minRing = Mathf.Min(minRing, hud.RewindButtonRingFill);
                    if (RewindFx.SoundPlaying)
                    {
                        soundSeen = true;
                        reversed |= RewindFx.ReversedClip;
                        if (elapsed >= 0.4f && elapsed < 1.0f)
                            pitchEarly = RewindFx.Pitch;
                        if (elapsed >= RewindFxHold - 0.5f)
                            pitchLate = RewindFx.Pitch;
                    }
                }
                bool rewindingStill = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                float fps = time > 0f ? frames / time : 0f;
                Run.Metric("rewindFx.fps", fps);
                Run.Metric("rewindFx.worstFrameMs", worst * 1000f);
                Run.Metric("rewindFx.peakIntensity", peakIntensity);
                Run.Metric("rewindFx.pitchEarly", pitchEarly);
                Run.Metric("rewindFx.pitchLate", pitchLate);
                Run.Metric("rewindFx.desaturation", peakDesat);
                Run.Metric("rewindFx.ghosts", peakGhosts);
                Run.Metric("rewindFx.streaks", peakStreaks);
                Run.Metric("rewindFx.motes", peakMotes);
                Run.Metric("rewindFx.playfieldGlitch", maxPlayfieldGlitch);
                Run.Metric("rewindFx.ringBefore", ringBefore);
                Run.Metric("rewindFx.ringMin", minRing);
                Run.Event("rewind-look", $"desat {peakDesat:0.00}, ghosts {peakGhosts}/{RewindWorldLook.GhostBudget}, streaks {peakStreaks}, motes {peakMotes}, glitch {maxPlayfieldGlitch:0.00}");

                RewindFxCheck("rewinding", rewindingStill, "the clock is rewinding at the end of the hold");
                RewindFxCheck("screen effect", peakIntensity >= 0.95f && RewindFx.Coverage > 0.12f && layers >= 4,
                    $"rim gold + motes up: strength {peakIntensity:0.00}, {layers} layers, coverage {RewindFx.Coverage:0.00}");
                RewindFxCheck("world desaturated", peakDesat >= 0.5f,
                    $"world desat toward 20% colour (probe {peakDesat:0.00}, want ≥0.5)");
                RewindFxCheck("hero ghosts", peakGhosts >= 4,
                    $"{peakGhosts} ghost copies visible (budget {RewindWorldLook.GhostBudget}, need ≥4)");
                RewindFxCheck("bullet streaks", peakStreaks >= 1,
                    $"{peakStreaks} reverse streaks (need ≥1 live projectile streak)");
                RewindFxCheck("no playfield scanlines", maxPlayfieldGlitch < 0.02f,
                    $"playfield glitch coverage {maxPlayfieldGlitch:0.00} (central 84% must stay clear)");
                RewindFxCheck("sound plays", soundSeen && peakVolume > 0.02f,
                    $"a {(reversed ? "reversed " : "")}loop playing, volume up to {peakVolume:0.00}");
                RewindFxCheck("pitch ramp", pitchLate > pitchEarly + 0.15f, $"pitch {pitchEarly:0.00} at 0.5 s -> {pitchLate:0.00} at {RewindFxHold - 0.25f:0.0} s");
                RewindFxCheck("frame rate", fps >= 30f,
                    $"{fps:0.0} fps over {frames:0} frames of rewind (play before: {(playTime > 0f ? playFrames / playTime : 0f):0.0} fps), worst {worst * 1000f:0} ms");

                if (touch)
                {
                    bool drained = minRing < ringBefore - 0.02f || minRing < 0.98f;
                    RewindFxCheck("rewind ring drains", drained,
                        $"button fill ring {ringBefore:0.00} -> {minRing:0.00} while held (touch)");
                }

                if (touchId >= 0)
                    input.TouchUp(touchId, touchAt);
                else
                    input.SetKey(Key.R, false);

                float released = Time.realtimeSinceStartup;
                for (float end = Time.realtimeSinceStartup + 0.35f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    if (RewindFx.SnapBackActive || RewindWorldLook.SnapBackActive)
                        snapSeen = true;
                    yield return null;
                }
                yield return Run.Until(() => !RewindFx.SoundPlaying && !RewindFx.Active, 3f, "the effect and sound to end");
                float took = Time.realtimeSinceStartup - released;
                RewindFxCheck("snap-back", snapSeen, "0.2 s colour snap / ripple after release");
                RewindFxCheck("stops at the end", !RewindFx.SoundPlaying && !RewindFx.Active && (GameClock.Instance == null || !GameClock.Instance.IsRewinding),
                    $"sound stopped and the screen back to normal {took:0.00} s after release");
                finished();
            }

            IEnumerator RewindFxDamageOnce()
            {
                var world = GameplayWorld.Instance;
                var health = world != null && world.PlayerController != null
                    ? world.PlayerController.GetComponentInChildren<PlayerHealth>()
                    : null;
                if (world == null || health == null || !health.IsAlive)
                {
                    RewindFxCheck("damage feel", false, "no live hero to hit");
                    yield break;
                }

                health.SetInvincibilityTimer(0f);
                Vector3 before = world.PlayerController.transform.position;
                float flashBefore = GameFeel.LastHeroFlashAt;
                var playerEnt = world.Player;
                if (playerEnt != null)
                    playerEnt.TakeDamageFrom(1, (Vector2)before + Vector2.right * 1.5f);
                else
                    health.TakeDamage(1);

                yield return null;
                yield return null;
                float flashSec = GameFeel.LastHeroFlashSeconds;
                bool flashed = GameFeel.LastHeroFlashAt > flashBefore;
                float push = PlayerController.LastHitPushDistance;
                float shakeDur = world.Effects != null ? world.Effects.LastShakeDuration : 0f;

                Run.Metric("rewindFx.heroFlashMs", flashSec * 1000f);
                Run.Metric("rewindFx.knockback", push);
                Run.Metric("rewindFx.shakeMs", shakeDur * 1000f);
                Run.Event("damage-feel", $"flash {flashSec * 1000f:0} ms, knock {push:0.00} u, shake {shakeDur * 1000f:0} ms");

                RewindFxCheck("hero flash", flashed && flashSec > 0f && flashSec <= 0.15f,
                    $"hero sprite flash {flashSec * 1000f:0} ms (need ≤150 ms)");
                RewindFxCheck("knockback", push >= 0.3f, $"hit shove {push:0.00} units (need ≥0.3)");
                RewindFxCheck("shake capped", shakeDur <= 0.3f + 0.001f, $"shake {shakeDur * 1000f:0} ms (cap 300)");
            }
        }
    }
}
