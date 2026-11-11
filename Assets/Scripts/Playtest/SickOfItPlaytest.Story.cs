using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The story scenario (Docs/Story.md "Arrival beats"): Level 1, then Floors 1000-997 loaded directly (setup shortcut), each played for its
        /// first seconds through touch (the move stick: taps and drags, no keys or mouse). Every beat must show a <c>storyBeat</c> event, a voiced
        /// line, a stinger and a shake / hero reaction, keep input locked at most 1.5 s, leave the hero under the thumb afterwards, keep its caption to
        /// one line clear of the hero, and not replay on a retry. Reports <c>quality.textOnlyBeats</c> (captions with no voice and no event).
        /// </summary>
        public const string StoryId = "story";
        const float StorySeconds = 420f;
        const float StoryWatchSeconds = 9f;
        const float StoryMaxLockSeconds = 1.5f;
        const int StoryCaptionMaxChars = 60;

        static readonly int[] StoryLevels = { 1, 2, 3, 4, 5 };
        static readonly string[] StoryNames = { "Level 1", "Floor 1000", "Floor 999", "Floor 998", "Floor 997" };

        sealed partial class Session
        {
            int storyFailed, storyTextOnly;

            void BeatCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"story {beat}: ok ({detail})"); return; }
                storyFailed++;
                Run.Event("check", $"story {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"story {beat}: {detail}");
            }

            sealed class BeatWatch
            {
                public readonly List<string> Beats = new List<string>();
                public readonly List<string> Skipped = new List<string>();
                public readonly List<string> Reactions = new List<string>();
                public readonly List<string> Shakes = new List<string>();
                public readonly List<string> Sounds = new List<string>();
                public readonly List<string> Captions = new List<string>();
                public int VoicedCaptions, SilentCaptions;
                public float LongestLock;
                public bool CaptionOneLine = true, CaptionClear = true;
                public string CaptionDetail = "";
            }

            /// <summary>Plays a level's first seconds with the stick held right, logging what the telemetry and the HUD show.</summary>
            IEnumerator StoryWatchLevel(int level, string name, BeatWatch watch, bool drive)
            {
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(level) && GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null && GameplayWorld.Instance.Hud != null,
                    90f, $"{name} to start");
                if (Run.Done) yield break;
                Run.Scene(name);
                var world = GameplayWorld.Instance;
                var hud = world.Hud;
                var hero = world.PlayerController;
                Time.timeScale = Speed;

                void OnRecorded(GameplayTelemetry t, GameplayTelemetry.Event e)
                {
                    if (t.World != world) return;
                    switch (e.Kind)
                    {
                        case "storyBeat": watch.Beats.Add($"{e.Subject} [{e.Detail}]"); break;
                        case "storyBeatSkipped": watch.Skipped.Add(e.Subject); break;
                        case "cutscene":
                            if (e.Subject == "playPlayerAnimation" && e.Detail != "Idle") watch.Reactions.Add(e.Detail);
                            else if (e.Subject == "cameraShake") watch.Shakes.Add(e.Detail);
                            break;
                        case "caption":
                            if (e.Detail.StartsWith("Subtitle")) { watch.Captions.Add(e.Subject); if (e.Value == 1) watch.VoicedCaptions++; else watch.SilentCaptions++; }
                            break;
                    }
                }
                GameplayTelemetry.Recorded += OnRecorded;

                int finger = -1;
                Vector2 touchAt = default;
                float radius = 40f;
                if (drive && hud.TouchStickShown)
                {
                    var zone = hud.TouchStickZoneScreenRect;
                    touchAt = new Vector2(Mathf.Clamp(zone.center.x + zone.width * 0.15f, zone.xMin + 8f, zone.xMax - 8f),
                        Mathf.Clamp(zone.center.y + zone.height * 0.1f, zone.yMin + 8f, zone.yMax - 8f));
                    radius = Mathf.Max(24f, hud.TouchStickScreenRect.width * 0.5f);
                    finger = input.TouchDown(touchAt);
                    input.TouchMove(finger, touchAt + new Vector2(radius * 0.9f, 0f));
                }

                float start = Time.realtimeSinceStartup, lockStart = -1f;
                string lastCaption = "";
                while (Time.realtimeSinceStartup - start < StoryWatchSeconds && !Run.Done)
                {
                    bool locked = world.Now < world.InputLockedUntil;
                    if (locked && lockStart < 0f) lockStart = Time.realtimeSinceStartup;
                    if (!locked && lockStart >= 0f) { watch.LongestLock = Mathf.Max(watch.LongestLock, Time.realtimeSinceStartup - lockStart); lockStart = -1f; }
                    if (hud.TryGetSubtitleScreenRect(out var captionRect, out int lines) && hud.SubtitleShown != lastCaption)
                    {
                        lastCaption = hud.SubtitleShown;
                        bool oneLine = lines <= 1 && lastCaption.Length <= StoryCaptionMaxChars;
                        bool clear = true;
                        var cam = world.Camera;
                        var sr = hero != null ? hero.HeroRenderer : null;
                        if (cam != null && sr != null)
                        {
                            var b = sr.bounds;
                            var lo = cam.WorldToScreenPoint(b.min);
                            var hi = cam.WorldToScreenPoint(b.max);
                            var heroRect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
                            clear = !heroRect.Overlaps(captionRect);
                            watch.CaptionDetail += $"\"{lastCaption}\" {lines} line(s), caption {captionRect}, hero {heroRect}, screen {Screen.width}x{Screen.height}; ";
                        }
                        watch.CaptionOneLine &= oneLine;
                        watch.CaptionClear &= clear;
                        Run.Event("beat", $"{name}: caption \"{lastCaption}\" on screen ({lines} line, clear of hero: {clear})");
                    }
                    yield return null;
                }
                if (lockStart >= 0f) watch.LongestLock = Mathf.Max(watch.LongestLock, Time.realtimeSinceStartup - lockStart);
                if (finger >= 0)
                    input.TouchUp(finger, touchAt + new Vector2(radius * 0.9f, 0f));
                GameplayTelemetry.Recorded -= OnRecorded;
            }

            public IEnumerator StoryRun()
            {
                // Setup shortcuts: a save that has not lived any beat, the touch UI on (the phone's input), the audio unlocked.
                StoryBeats.Reset();
                GameplayHud.TouchUiForced = true;
                GameplayHud.ResetTouchHints();
                Run.Event("shortcut", "story beats cleared, touch UI on, levels loaded directly");

                for (int i = 0; i < StoryLevels.Length; i++)
                {
                    int level = StoryLevels[i];
                    string name = StoryNames[i];
                    var watch = new BeatWatch();
                    var world0 = GameplayWorld.Instance;
                    yield return StoryWatchLevel(level, name, watch, drive: true);
                    if (Run.Done) yield break;

                    bool hasBeat = watch.Beats.Count > 0;
                    bool isFloor = level >= 2;
                    Run.Event("beat", $"{name}: storyBeat {(hasBeat ? string.Join(", ", watch.Beats) : "none")}; reactions {string.Join("/", watch.Reactions)}; shakes {watch.Shakes.Count}; captions {watch.Captions.Count} ({watch.VoicedCaptions} voiced)");

                    if (isFloor)
                    {
                        BeatCheck($"{name} plays a beat", hasBeat, hasBeat ? watch.Beats[0] : "no storyBeat event");
                        BeatCheck($"{name} says a voiced line", hasBeat && watch.VoicedCaptions >= 1, $"{watch.VoicedCaptions} voiced caption(s)");
                        BeatCheck($"{name} shakes the camera and the hero reacts", watch.Reactions.Count >= 1, $"reactions {string.Join("/", watch.Reactions)}");
                        BeatCheck($"{name} keeps the caption to one short line", watch.CaptionOneLine, watch.CaptionDetail);
                        BeatCheck($"{name} caption is clear of the hero", watch.CaptionClear, watch.CaptionDetail);
                        BeatCheck($"{name} locks input at most {StoryMaxLockSeconds} s", watch.LongestLock <= StoryMaxLockSeconds, $"longest lock {watch.LongestLock:0.00} s");
                    }
                    // A caption is text only when it has no voice under it and no beat event around it.
                    int textOnly = watch.SilentCaptions > 0 && !hasBeat ? watch.SilentCaptions : 0;
                    storyTextOnly += textOnly;
                    Run.Metric($"story.{name.Replace(' ', '_')}.textOnlyCaptions", textOnly);
                    Run.Metric($"story.{name.Replace(' ', '_')}.longestLockSeconds", watch.LongestLock);

                    // The hero is under the thumb once the beat is over: the stick moves him.
                    var world = GameplayWorld.Instance;
                    if (world != null && world.PlayerController != null && world.Hud != null && world.Hud.TouchStickShown)
                    {
                        var hero = world.PlayerController;
                        var hud = world.Hud;
                        yield return Run.Until(() => hero.IsGrounded, 10f, "the hero on the ground");
                        var zone = hud.TouchStickZoneScreenRect;
                        var at = new Vector2(Mathf.Clamp(zone.center.x - zone.width * 0.15f, zone.xMin + 8f, zone.xMax - 8f), Mathf.Clamp(zone.center.y, zone.yMin + 8f, zone.yMax - 8f));
                        float r = Mathf.Max(24f, hud.TouchStickScreenRect.width * 0.5f);
                        float x0 = hero.transform.position.x;
                        int f = input.TouchDown(at);
                        yield return null;
                        input.TouchMove(f, at + new Vector2(-r * 0.9f, 0f));
                        yield return WaitReal(0.9f);
                        input.TouchUp(f, at + new Vector2(-r * 0.9f, 0f));
                        float x1 = hero.transform.position.x;
                        BeatCheck($"{name} hero stays under touch control after the beat", world.Now >= world.InputLockedUntil && Mathf.Abs(x1 - x0) > 0.3f, $"x {x0:0.00} -> {x1:0.00}");
                    }
                    Run.Progress((i + 1f) / (StoryLevels.Length + 1f));
                }

                // A quick retry of the floors does not replay the beats: Floor 1000 again.
                var retry = new BeatWatch();
                yield return StoryWatchLevel(StoryLevels[1], "Floor 1000 (retry)", retry, drive: false);
                if (Run.Done) yield break;
                BeatCheck("retry does not replay the beat", retry.Beats.Count == 0 && retry.Skipped.Count >= 1,
                    $"beats {retry.Beats.Count}, skipped {retry.Skipped.Count}, captions {retry.Captions.Count}");

                Run.Metric("quality.textOnlyBeats", storyTextOnly);
                BeatCheck("no text-only beats (Level 1 and Floors 1000-997)", storyTextOnly == 0, $"{storyTextOnly} caption(s) with no voice and no event");
                GameplayHud.TouchUiForced = false;
                Run.Progress(1f);
                End(storyFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
