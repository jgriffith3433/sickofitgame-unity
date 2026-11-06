using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// rewind-fx: what rewinding looks and sounds like (RewindFx). Level 1 loaded directly (setup shortcut), played by
        /// the bot, then R held for a few real seconds with the juice topped up (setup shortcut): the screen effect comes up,
        /// a sound loops with its pitch climbing, the frame rate holds 30+, and on release the effect fades and the sound stops.
        /// </summary>
        public const string RewindFxId = "rewind-fx";
        const int RewindFxLevel = 1;
        const float RewindFxHold = 3.5f, RewindFxSeconds = 150f;

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
                }, 0f, 1f, 150f, until: () => done);
                if (Run.Done)
                    yield break;
                RewindFxCheck("the hold happened", done, done ? "R held and released on level 1" : "the level ended before the hold");
                Run.Metric("rewindFx.failed", rewindFxFailed);
                Run.Progress(1f);
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

                // Frame times of normal play just before, for the comparison.
                float playFrames = 0f, playTime = 0f;
                for (float end = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < end && !Run.Done;)
                {
                    playFrames++; playTime += Time.unscaledDeltaTime;
                    yield return null;
                }

                Run.Event("rewind", $"hold R for {RewindFxHold:0.0} s");
                input.SetKey(Key.R, true);
                float t0 = Time.realtimeSinceStartup;
                float frames = 0f, time = 0f, worst = 0f, pitchEarly = 0f, pitchLate = 0f, peakIntensity = 0f, peakVolume = 0f;
                int layers = 0;
                bool soundSeen = false, reversed = false;
                while (!Run.Done && Time.realtimeSinceStartup - t0 < RewindFxHold)
                {
                    yield return null;
                    float elapsed = Time.realtimeSinceStartup - t0;
                    // Skip the first 0.3 s of the fade-in for the frame rate: those frames also start the rewind itself.
                    frames++; time += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                    peakIntensity = Mathf.Max(peakIntensity, RewindFx.Intensity);
                    peakVolume = Mathf.Max(peakVolume, RewindFx.SoundVolume);
                    layers = Mathf.Max(layers, RewindFx.VisibleLayers);
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
                RewindFxCheck("rewinding", rewindingStill, "the clock is rewinding at the end of the hold");
                RewindFxCheck("screen effect", peakIntensity >= 0.95f && RewindFx.Coverage > 0.2f && layers >= 4,
                    $"scanlines, colour split, tracking bar and streaks up: strength {peakIntensity:0.00}, {layers} layers, coverage {RewindFx.Coverage:0.00}");
                RewindFxCheck("sound plays", soundSeen && peakVolume > 0.02f,
                    $"a {(reversed ? "reversed " : "")}loop playing, volume up to {peakVolume:0.00} (Effects setting {SickOfIt.Data.GameSettings.SfxVolume:0.00})");
                RewindFxCheck("pitch ramp", pitchLate > pitchEarly + 0.15f, $"pitch {pitchEarly:0.00} at 0.5 s -> {pitchLate:0.00} at {RewindFxHold - 0.25f:0.0} s");
                RewindFxCheck("frame rate", fps >= 30f,
                    $"{fps:0.0} fps over {frames:0} frames of rewind with the effect (play before: {(playTime > 0f ? playFrames / playTime : 0f):0.0} fps), worst frame {worst * 1000f:0} ms");

                input.SetKey(Key.R, false);
                float released = Time.realtimeSinceStartup;
                yield return null;
                yield return Run.Until(() => !RewindFx.SoundPlaying && !RewindFx.Active, 3f, "the effect and sound to end");
                float took = Time.realtimeSinceStartup - released;
                RewindFxCheck("stops at the end", !RewindFx.SoundPlaying && !RewindFx.Active && (GameClock.Instance == null || !GameClock.Instance.IsRewinding),
                    $"sound stopped and the screen back to normal {took:0.00} s after R let go");
                finished();
            }
        }
    }
}
