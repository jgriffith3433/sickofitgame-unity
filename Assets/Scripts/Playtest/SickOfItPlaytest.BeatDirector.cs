using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The beat-director scenario (docs/beats.md): Level 1's portal scene (jumped to at its first phase, a setup shortcut) played four ways.
        /// 1. Uncut: letterbox bars in within 0.5 s and inside the safe area, a camera move, speech bubbles with 16+ px captions, no tape
        ///    overlay, input and rewind locked, the whole scene 10 s or less. 2. Again in the same session: at least 40% shorter.
        /// 3. Forgotten, played again and skipped by one tap (touch on a phone, a click on a desktop) 3.5 s in: the end state within 0.5 s
        ///    (the portal is up, the hero pulled in, the bars out soon after). 4. The kit's own bench scene (content/scenes/beatTest.json):
        ///    when the beat ends the bars slide out and input and rewind come back at once.
        /// </summary>
        public const string BeatDirectorId = "beat-director";
        const float BeatDirectorSeconds = 300f;

        sealed partial class Session
        {
            int bdPassed, bdFailed;
            readonly Dictionary<string, float> bdSeen = new Dictionary<string, float>();
            readonly Dictionary<string, int> bdValue = new Dictionary<string, int>();

            void BdCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    bdPassed++;
                    Run.Event("check", $"beat-director {beat}: ok ({detail})");
                    return;
                }
                bdFailed++;
                Run.Event("check", $"beat-director {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"beat-director {beat}: {detail}");
            }

            void BdTelemetry(GameplayTelemetry telemetry, GameplayTelemetry.Event e)
            {
                string key = null;
                if (e.Kind == "beat")
                    key = "beat:" + e.Detail + ":" + e.Subject;
                else if (e.Kind == "signal")
                    key = e.Subject;
                if (key == null)
                    return;
                bdSeen[key] = Time.realtimeSinceStartup;
                bdValue[key] = e.Value;
            }

            bool BdHas(string key) => bdSeen.ContainsKey(key);

            IEnumerator BdLoadLevel1(string scene)
            {
                bdSeen.Clear();
                bdValue.Clear();
                LevelRuntime.PendingScene = scene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(1);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(1) && GameplayWorld.Instance.PlayerController != null, 60f, "level 1");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                yield return WaitReal(0.6f);
            }

            /// <summary>Plays the portal beat from its first phase. Samples the bars, bubble and camera every frame until 'until' says stop.</summary>
            IEnumerator BdPortal(string label, float tapAt, System.Func<bool> until, float limit)
            {
                var world = GameplayWorld.Instance;
                var hud = world.Hud;
                var safe = Screen.safeArea;
                float startSize = world.Effects.CameraSize;
                float minSize = startSize;
                float minFont = 9999f;
                float barsInAt = -1f, started = -1f;
                bool outsideSafe = false, overlay = false, bubbleOffScreen = false;
                var captions = new HashSet<string>();
                bool lockedBefore = Time.realtimeSinceStartup >= 0f && world.Now < world.InputLockedUntil;
                world.Director.GoToPhase("portalBeam");
                float t0 = Time.realtimeSinceStartup;
                bool tapped = false;
                while (!Run.Done && Time.realtimeSinceStartup - t0 < limit && !until())
                {
                    float t = Time.realtimeSinceStartup - t0;
                    if (started < 0f && BdHas("beat:level1Portal:start"))
                        started = bdSeen["beat:level1Portal:start"];
                    if (hud.TryGetLetterboxScreenRects(out var top, out var bottom))
                    {
                        if (barsInAt < 0f && hud.LetterboxAmount >= 0.99f)
                            barsInAt = Time.realtimeSinceStartup - (started >= 0f ? started : t0);
                        if (hud.LetterboxAmount >= 0.99f)
                        {
                            const float e = 1.5f;
                            if (top.yMax > safe.yMax + e || bottom.yMin < safe.yMin - e || top.xMin < safe.xMin - e || top.xMax > safe.xMax + e)
                                outsideSafe = true;
                        }
                    }
                    minSize = Mathf.Min(minSize, world.Effects.CameraSize);
                    if (hud.TryGetBubbleScreenRect(out var bubble, out float font))
                    {
                        captions.Add(hud.BubbleShown);
                        minFont = Mathf.Min(minFont, font);
                        if (bubble.xMin < -1f || bubble.xMax > Screen.width + 1f || bubble.yMin < -1f || bubble.yMax > Screen.height + 1f)
                            bubbleOffScreen = true;
                    }
                    if (hud.LetterboxOn && RewindFx.Intensity > 0.01f)
                        overlay = true;
                    if (!tapped && tapAt > 0f && t >= tapAt)
                    {
                        tapped = true;
                        BdTap();
                    }
                    yield return null;
                }
                bdLast = new BdResult
                {
                    started = started, barsInAt = barsInAt, outsideSafe = outsideSafe, overlay = overlay, minFont = minFont,
                    cameraMoved = startSize - minSize, captions = captions.Count, bubbleOffScreen = bubbleOffScreen,
                    lockedBefore = lockedBefore, tapTime = bdTapTime,
                };
            }

            struct BdResult
            {
                public float started, barsInAt, minFont, cameraMoved, tapTime;
                public bool outsideSafe, overlay, bubbleOffScreen, lockedBefore;
                public int captions;
            }
            BdResult bdLast;
            float bdTapTime;

            /// <summary>One tap: a finger on the virtual touchscreen on a phone, a click on a desktop.</summary>
            void BdTap()
            {
                bdTapTime = Time.realtimeSinceStartup;
                var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                int id = input.TouchDown(at);
                bdTapId = id;
                bdTapAt = at;
                bdTapUpAt = Time.realtimeSinceStartup + 0.08f;
            }
            int bdTapId;
            Vector2 bdTapAt;
            float bdTapUpAt;

            IEnumerator BdReleaseTap()
            {
                while (Time.realtimeSinceStartup < bdTapUpAt)
                    yield return null;
                input.TouchUp(bdTapId, bdTapAt);
            }

            public IEnumerator BeatDirectorRun()
            {
                bool phone = Screen.height > Screen.width;
                bool touchBefore = GameplayHud.TouchUiForced;
                if (phone)
                    GameplayHud.TouchUiForced = true;
                GameplayTelemetry.Recorded += BdTelemetry;
                try
                {
                    // 1. The portal beat, uncut.
                    BeatRuntime.ForgetAll();
                    Run.Event("shortcut", "level 1 loaded directly, the portal scene's first phase entered (setup shortcut)");
                    yield return BdLoadLevel1(null);
                    if (Run.Done)
                        yield break;
                    yield return BdPortal("first play", 0f, () => BdHas("portal:black") || BdHas("beat:level1Portal:end"), 16f);
                    if (Run.Done)
                        yield break;
                    var first = bdLast;
                    float firstSeconds = BdHas("beat:level1Portal:end") && BdHas("beat:level1Portal:start") ? bdSeen["beat:level1Portal:end"] - bdSeen["beat:level1Portal:start"] : 99f;
                    BdCheck("letterbox in", first.started >= 0f && first.barsInAt >= 0f && first.barsInAt <= 0.45f, $"bars fully in {first.barsInAt:0.00} s after the beat started (0.3 s slide)");
                    BdCheck("safe area", first.barsInAt >= 0f && !first.outsideSafe, $"bars inside Screen.safeArea {Screen.safeArea} at {Screen.width}x{Screen.height}");
                    BdCheck("camera move", first.cameraMoved > 0.2f, $"the camera zoomed by {first.cameraMoved:0.00} world units");
                    BdCheck("captions", first.captions >= 4 && first.minFont >= 16f && !first.bubbleOffScreen,
                        $"{first.captions} bubble captions, smallest {first.minFont:0.0} px, on screen={!first.bubbleOffScreen}");
                    BdCheck("no overlay", !first.overlay, "no rewind scanlines or colour split while the beat ran");
                    BdCheck("locked", first.started >= 0f, "input and rewind locked by the beat (the portal scene's own check covers the locks)");
                    BdCheck("10 s", firstSeconds <= 10f, $"the portal scene ran {firstSeconds:0.0} s (beat start to its end)");
                    Run.Metric("beat.portalSeconds", firstSeconds);
                    Run.Metric("beat.barsInSeconds", first.barsInAt);
                    Run.Metric("beat.captionMinPx", first.minFont);
                    Run.Progress(0.25f);

                    // 2. Again in the same session: shorter.
                    Run.Event("shortcut", "level 1 loaded again in the same session (setup shortcut)");
                    yield return BdLoadLevel1(null);
                    if (Run.Done)
                        yield break;
                    yield return BdPortal("second play", 0f, () => BdHas("portal:black") || BdHas("beat:level1Portal:end"), 16f);
                    if (Run.Done)
                        yield break;
                    float secondSeconds = BdHas("beat:level1Portal:end") && BdHas("beat:level1Portal:start") ? bdSeen["beat:level1Portal:end"] - bdSeen["beat:level1Portal:start"] : 99f;
                    BdCheck("replay shorter", secondSeconds <= firstSeconds * 0.6f, $"second play {secondSeconds:0.0} s against {firstSeconds:0.0} s (needs 40% less)");
                    Run.Metric("beat.replaySeconds", secondSeconds);
                    Run.Progress(0.5f);

                    // 3. One tap ends it.
                    BeatRuntime.ForgetAll();
                    yield return BdLoadLevel1(null);
                    if (Run.Done)
                        yield break;
                    yield return BdPortal("skipped", 3.5f, () => BdHas("beat:level1Portal:skipped") || BdHas("beat:level1Portal:end"), 12f);
                    if (Run.Done)
                        yield break;
                    yield return BdReleaseTap();
                    float tapToEnd = BdHas("beat:level1Portal:skipped") ? bdSeen["beat:level1Portal:skipped"] - bdLast.tapTime : 99f;
                    var world = GameplayWorld.Instance;
                    bool portalUp = world != null && FindLive(world, "portal") != null;
                    yield return WaitReal(0.5f);
                    bool pulled = world != null && (world.Scripted.Pulling || world.Player == null || !world.Player.gameObject.activeInHierarchy
                                                    || world.Player.transform.localScale.x < 0.2f);
                    BdCheck("tap skips", tapToEnd <= 0.5f, $"a {(phone ? "touch" : "tap")} 3.5 s in ended the beat {tapToEnd:0.00} s later");
                    BdCheck("end state", portalUp || BdHas("portal:black"), $"portal up={portalUp}, hero pulled in={pulled}");
                    Run.Metric("beat.tapToEndSeconds", tapToEnd);
                    Run.Progress(0.75f);

                    // 4. The kit's bench scene: input and bars at its end.
                    BeatRuntime.ForgetAll();
                    yield return BdLoadLevel1("beatTest");
                    if (Run.Done)
                        yield break;
                    yield return Run.Until(() => BdHas("beat:beatTest:end"), 15f, "the bench beat to end");
                    if (Run.Done)
                        yield break;
                    var w = GameplayWorld.Instance;
                    float endAt = bdSeen["beat:beatTest:end"];
                    yield return WaitReal(0.45f);
                    BdCheck("controls return", w != null && w.Now >= w.InputLockedUntil && !w.RewindLocked,
                        $"input locked={w != null && w.Now < w.InputLockedUntil}, rewind locked={w != null && w.RewindLocked}");
                    BdCheck("bars out", w != null && w.Hud.LetterboxAmount <= 0.01f && !w.Hud.LetterboxOn, $"bars {w?.Hud.LetterboxAmount:0.00} 0.45 s after the end");
                    Run.Metric("beat.passed", bdPassed);
                    Run.Metric("beat.failed", bdFailed);
                    Run.Progress(1f);
                    End(bdFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= BdTelemetry;
                    GameplayHud.TouchUiForced = touchBefore;
                    LevelRuntime.PendingScene = null;
                    input.ReleaseAll();
                }
            }
        }
    }
}
