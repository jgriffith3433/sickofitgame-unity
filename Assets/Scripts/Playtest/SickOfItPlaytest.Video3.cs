using System.Collections;
using Hawkes.Playtest;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The video3 scenario: Video3 "Hell's Ad Break" (Docs/LoreVideos.md) after Floor 1000, Escape skipping it, and its replay from video select.</summary>
        public const string Video3Id = "video3";
        const float Video3Seconds = 240f;
        const int Video3Number = 3;

        sealed partial class Session
        {
            int video3Failed;

            /// <summary>A video-select card: its button has no label of its own, so find it from its title text.</summary>
            static MenuButton FindCard(string title)
            {
                foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                {
                    if (text == null || !text.isActiveAndEnabled || Tags.Replace(text.text ?? "", "").Trim() != title)
                        continue;
                    var button = text.GetComponentInParent<MenuButton>();
                    if (button != null && button.isActiveAndEnabled)
                        return button;
                }
                return null;
            }

            void Video3Check(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"video3 {beat}: ok ({detail})"); return; }
                video3Failed++;
                Run.Event("check", $"video3 {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"video3 {beat}: {detail}");
            }

            /// <summary>
            /// Setup shortcuts: the Shaft's marker at Floor 999 and Yooba's save reset (a player who just cleared Floor 1000), the video
            /// loaded directly like the yooba scenario (the real flow after Level 2 is story-path's), and video 3 unlocked (what clearing
            /// Floor 1000 writes). Everything else is real input: the film must play (the clock runs, a mid-film check, no stall), Escape
            /// skips it into Yooba's hello on the Shaft, and the video-select card replays it, this time to its end and back to the main menu.
            /// </summary>
            public IEnumerator Video3Run()
            {
                var definition = GameDefinition.Instance;
                var video = definition.GetVideo(Video3Number);
                var landing = video != null ? definition.GetLevel(video.afterLevel) : null;
                Video3Check("config", video != null && video.afterLevel == 2 && video.nextLevel == 3 && landing != null && landing.nextVideoNumber == Video3Number,
                    video == null ? "no Video3 in game.json" : $"Video3 \"{video.displayName}\" after level {video.afterLevel}, next level {video.nextLevel}, level {video.afterLevel} points to video {(landing != null ? landing.nextVideoNumber : -1)}");
                if (video == null) { End(PlaytestOutcome.Error); yield break; }
                float length = video.timeline != null && video.timeline.duration > 0f ? video.timeline.duration : 30f;

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // 1. After Floor 1000: the film plays.
                Run.Event("shortcut", $"Yooba's save reset, the Shaft's marker at Floor 999, video {Video3Number} unlocked, load video {Video3Number} directly");
                YoobaProgress.Reset();
                ShaftProgress.Floor = 999;
                ShaftProgress.Plunged = true;
                GameProgressManager.Instance.UnlockVideo(Video3Number);
                PlayerPrefs.SetInt("ReturnToLevel", video.nextLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(Video3Number);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {Video3Number} to start playing");
                if (Run.Done) yield break;
                Run.Scene("Video 3: Hell's Ad Break");
                Video3Check("plays", PlayerPrefs.GetInt("CurrentVideoNumber", 0) == Video3Number, $"video {PlayerPrefs.GetInt("CurrentVideoNumber", 0)} is playing, clock {ClockTime:0.00}s, film length {length:0.0}s");
                float clock0 = ClockTime, real0 = Time.realtimeSinceStartup;
                yield return WaitReal(10f);
                if (Run.Done) yield break;
                float advanced = ClockTime - clock0, spent = Time.realtimeSinceStartup - real0;
                Video3Check("clock", advanced > spent * 0.7f && Object.FindAnyObjectByType<VideoRuntime>() != null,
                    $"the film's clock ran {advanced:0.0}s in {spent:0.0}s of real time (clock now {ClockTime:0.0}s)");
                Run.Metric("video3.clockAt10s", ClockTime);
                Run.Progress(0.25f);

                // 2. Escape skips it into Yooba's hello.
                float skippedAt = ClockTime;
                yield return Tap(Key.Escape);
                Run.Event("skip", "video " + Video3Number);
                yield return Run.Until(() => ShaftScreen.Current != null || GameplayWorld.Instance != null, 20f, "the Shaft after the video");
                if (Run.Done) yield break;
                var shaft = ShaftScreen.Current;
                Video3Check("skip", shaft != null && shaft.Kind == ShaftScreen.Mode.Meet && skippedAt < length - 3f,
                    shaft != null ? $"Escape at {skippedAt:0.0}s of {length:0.0}s opened the Shaft as {shaft.Kind} at FLOOR {shaft.ToFloor} (Yooba's hello)" : "no Shaft after Escape");
                Run.Scene("Shaft: Yooba's hello");
                yield return WaitReal(3f);
                if (Run.Done) yield break;
                Run.Progress(0.4f);

                // 3. Replay from video select, watched to its end.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var labels = definition.mainMenuConfig.labels;
                yield return ClickUntil(labels.videos, () => Object.FindAnyObjectByType<VideoSelectionManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Video Select");
                yield return WaitReal(1.5f);
                var card = FindCard(video.displayName);
                Video3Check("card", card != null, card != null ? $"video select lists \"{video.displayName}\" and it can be clicked" : $"no \"{video.displayName}\" card in video select");
                if (card == null) { End(PlaytestOutcome.Lost); yield break; }
                yield return ClickButton(card, "video select card " + video.displayName);
                if (Run.Done) yield break;
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null, 20f, "the replay's video to load");
                if (Run.Done) yield break;
                yield return Run.Until(() => ClockTime > 0.1f, 60f, "the replay to start playing");
                if (Run.Done) yield break;
                Run.Scene("Video 3 replay");
                Video3Check("replay", PlayerPrefs.GetInt("CurrentVideoNumber", 0) == Video3Number, $"video select started video {PlayerPrefs.GetInt("CurrentVideoNumber", 0)}");
                float furthest = 0f;
                yield return Run.Until(() =>
                {
                    if (Object.FindAnyObjectByType<VideoRuntime>() != null)
                        furthest = Mathf.Max(furthest, ClockTime);
                    return Object.FindAnyObjectByType<MainMenuManager>() != null && Object.FindAnyObjectByType<VideoRuntime>() == null;
                }, length + 40f, "the replay to end and the main menu to return");
                if (Run.Done) yield break;
                Video3Check("end", furthest >= length - 3f, $"the replay ran to {furthest:0.0}s of {length:0.0}s and returned to the main menu");
                Run.Metric("video3.replayReached", furthest);
                Run.Progress(1f);
                End(video3Failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
