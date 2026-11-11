using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The adaptive-music scenario (Docs/Story.md "Music"; every floor its own music, rising with the crowd, bending on a rewind).
        /// 1. Touch only (a virtual touchscreen: the sound prompt, then New Game tapped, no mouse or keyboard): Level 1 plays its own composed
        ///    Meadow track (not Videos/Level1_Background1_Audio.mp3), within 1 s of the level starting, calm until the sky opens and then the raid's track.
        /// 2. Floor 997 (the Ember Wastes, loaded directly: a setup shortcut) played by the bot: the Circle's loop plays, the intensity layer comes in
        ///    with the crowd and goes out again, and a held R (the rewind) plays the bent variant of the track (`musicRewind` events) and the
        ///    normal track is back within 0.5 s of letting go.
        /// 3. Floor 901 (the Magma King, loaded directly): his theme takes over the Wastes' loop as the arena locks (`music` events).
        /// </summary>
        public const string AdaptiveMusicId = "adaptive-music";
        const int AdaptiveMusicFloorLevel = 5;
        const float AdaptiveMusicSeconds = 600f;

        sealed partial class Session
        {
            int adaptivePassed, adaptiveFailed;
            readonly List<(float real, float game, string clip, string why)> adaptiveMusic = new List<(float, float, string, string)>();
            readonly List<(float real, int on)> adaptiveRewind = new List<(float, int)>();
            readonly List<(float game, int level, float crowd)> adaptiveLayer = new List<(float, int, float)>();

            void AdaptiveCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    adaptivePassed++;
                    Run.Event("check", $"adaptive-music {beat}: ok ({detail})");
                }
                else
                {
                    adaptiveFailed++;
                    Run.Event("check", $"adaptive-music {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"adaptive-music {beat}: {detail}");
                }
            }

            void AdaptiveTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "music")
                {
                    adaptiveMusic.Add((Time.realtimeSinceStartup, e.Time, e.Subject, e.Detail));
                    Run.Event("music", $"{e.Subject} ({e.Detail}) at {e.Time:0.0} s");
                }
                else if (e.Kind == "musicRewind")
                {
                    adaptiveRewind.Add((Time.realtimeSinceStartup, e.Value));
                    Run.Event("musicRewind", $"{e.Subject} variant {(e.Value == 1 ? "on" : "off")} at {e.Time:0.0} s");
                }
                else if (e.Kind == "musicLayer")
                {
                    float crowd = 0f;
                    if (e.Detail != null && e.Detail.StartsWith("crowd "))
                        float.TryParse(e.Detail.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out crowd);
                    adaptiveLayer.Add((e.Time, e.Value, crowd));
                    Run.Event("musicLayer", $"{e.Subject}: {e.Value}% at {e.Time:0.0} s, crowd {crowd:0.0}");
                }
            }

            int AdaptiveFirst(int from, string clip, string why)
            {
                for (int i = from; i < adaptiveMusic.Count; i++)
                    if (adaptiveMusic[i].clip == clip && (why == null || adaptiveMusic[i].why == why))
                        return i;
                return -1;
            }

            IEnumerator TouchTapButton(string text, float timeout)
            {
                MenuButton button = null;
                yield return Run.Until(() =>
                {
                    if (FindButton(text) == null)
                        Object.FindAnyObjectByType<MainMenuManager>()?.ShowPageHolding(text);
                    return (button = FindButton(text)) != null && button.IsInteractable();
                }, timeout, $"the \"{text}\" button");
                if (Run.Done)
                    yield break;
                yield return WaitReal(0.4f);
                button = FindButton(text);
                if (button == null)
                    yield break;
                var at = ScreenPoint(button);
                int finger = input.TouchDown(at);
                yield return WaitReal(0.08f);
                input.TouchUp(finger, at);
                Run.Event("tap", text);
            }

            public IEnumerator AdaptiveMusicRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                GameplayTelemetry.Recorded += AdaptiveTelemetry;
                try
                {
                    // 1. Level 1 by touch.
                    yield return TapSoundPrompt();
                    var labels = GameDefinition.Instance.mainMenuConfig.labels;
                    float tapped = Time.realtimeSinceStartup;
                    yield return TouchTapButton(NewGameLabel(labels), 30f);
                    if (Run.Done) yield break;
                    yield return Run.Until(() => LevelRunning(1) || Object.FindAnyObjectByType<VideoRuntime>() != null, 60f, "Level 1 or its film after the tap");
                    if (Run.Done) yield break;
                    if (Object.FindAnyObjectByType<VideoRuntime>() != null)
                    {
                        // A tap skips the film.
                        var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                        int finger = input.TouchDown(at);
                        yield return WaitReal(0.08f);
                        input.TouchUp(finger, at);
                    }
                    yield return Run.Until(() => LevelRunning(1), 60f, "Level 1 to play");
                    if (Run.Done) yield break;
                    float playing = Time.realtimeSinceStartup;
                    yield return Run.Until(() => adaptiveMusic.Count > 0, 5f, "the level's music to start");
                    int start = adaptiveMusic.Count > 0 ? 0 : -1;
                    string first = start >= 0 ? adaptiveMusic[0].clip : "(none)";
                    AdaptiveCheck("Level 1 plays the Meadow track", first == "meadow_calm", $"the level's first music is \"{first}\" (not Level1_Background1_Audio)");
                    AdaptiveCheck("Level 1's background video audio is not the music", first != "Level1_Background1_Audio", $"music \"{first}\"");
                    if (start >= 0)
                    {
                        float afterPlay = adaptiveMusic[0].real - playing;
                        float afterTap = adaptiveMusic[0].real - tapped;
                        Run.Metric("adaptiveMusic.level1MusicAfterPlayStartSeconds", afterPlay);
                        Run.Metric("adaptiveMusic.level1MusicAfterNewGameTapSeconds", afterTap);
                        AdaptiveCheck("floor music starts within 1 s of the level opening", afterPlay <= 1f, $"{afterPlay:0.00} s after the level began ({afterTap:0.0} s after the New Game tap, loading included)");
                    }
                    // Calm first, then the raid: the sky opens and the music turns.
                    yield return Run.Until(() => AdaptiveFirst(0, "wastes_push", "script") >= 0, 30f, "the music to turn to the raid");
                    int raid = AdaptiveFirst(0, "wastes_push", "script");
                    if (raid >= 0 && start >= 0)
                    {
                        float calm = adaptiveMusic[raid].game - adaptiveMusic[0].game;
                        AdaptiveCheck("calm opening, then the raid", calm >= 4f, $"the Meadow track played {calm:0.0} s before the raid's track took over");
                    }
                    else
                        AdaptiveCheck("calm opening, then the raid", false, "the raid's track never started");
                    input.ReleaseAll();
                    Run.Progress(0.2f);

                    // 2. A Circle X floor: loop, intensity layer, the rewind variant.
                    Run.Event("shortcut", $"load level {AdaptiveMusicFloorLevel} directly");
                    adaptiveMusic.Clear();
                    adaptiveLayer.Clear();
                    var old = GameplayWorld.Instance;
                    Time.timeScale = 1f;
                    var load = LoadLevel(AdaptiveMusicFloorLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(AdaptiveMusicFloorLevel), 60f, "the floor to start");
                    if (Run.Done) yield break;
                    yield return PlayLevel(AdaptiveMusicFloorLevel, 0f, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 25f);
                    if (Run.Done) yield break;
                    string loop = adaptiveMusic.Count > 0 ? adaptiveMusic[0].clip : "(none)";
                    AdaptiveCheck("the Circle's loop plays on the floor", loop == "wastes_loop", $"first music \"{loop}\"");

                    var stage = Object.FindAnyObjectByType<StageMusic>();
                    AdaptiveCheck("the stage music exists", stage != null, stage != null ? "StageMusic found" : "no StageMusic");
                    if (stage != null)
                    {
                        int before = adaptiveRewind.Count;
                        float pressed = Time.realtimeSinceStartup;
                        input.SetKey(UnityEngine.InputSystem.Key.R, true);
                        yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 5f, "the rewind to start");
                        yield return Run.Until(() => stage.RewindVariantPlaying, 1f, "the rewind variant of the music");
                        bool variant = stage.RewindVariantPlaying;
                        float variantAfter = adaptiveRewind.Count > before ? adaptiveRewind[before].real - pressed : -1f;
                        AdaptiveCheck("a rewind plays the bent variant", variant && adaptiveRewind.Count > before && adaptiveRewind[before].on == 1, $"variant {(variant ? "playing" : "silent")} {variantAfter:0.00} s after R");
                        yield return WaitReal(1.5f);
                        AdaptiveCheck("the variant keeps playing through the rewind", stage.RewindVariantPlaying, "still bent 1.5 s in");
                        float released = Time.realtimeSinceStartup;
                        input.SetKey(UnityEngine.InputSystem.Key.R, false);
                        yield return Run.Until(() => GameClock.Instance != null && !GameClock.Instance.IsRewinding, 5f, "the rewind to end");
                        float back = -1f;
                        float limit = Time.realtimeSinceStartup + 2f;
                        while (!Run.Done && Time.realtimeSinceStartup < limit)
                        {
                            if (stage.NormalTrackAudible && !stage.RewindVariantPlaying)
                            {
                                back = Time.realtimeSinceStartup - released;
                                break;
                            }
                            yield return null;
                        }
                        Run.Metric("adaptiveMusic.normalBackSeconds", back);
                        AdaptiveCheck("the normal track is back within 0.5 s of letting go", back >= 0f && back <= 0.5f, back >= 0f ? $"{back:0.00} s" : "it never came back");
                        AdaptiveCheck("the variant event stopped", adaptiveRewind.Count > before + 1 && adaptiveRewind[adaptiveRewind.Count - 1].on == 0, "variant off event");
                    }
                    if (Run.Done) yield break;
                    Run.Progress(0.45f);

                    // The intensity layer over the next minute of play.
                    yield return PlayLevel(AdaptiveMusicFloorLevel, 0f, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 110f);
                    if (Run.Done) yield break;
                    int peak = 0, low = 100;
                    float peakCrowd = 0f;
                    int peakAt = -1;
                    for (int i = 0; i < adaptiveLayer.Count; i++)
                        if (adaptiveLayer[i].level > peak)
                        {
                            peak = adaptiveLayer[i].level;
                            peakCrowd = adaptiveLayer[i].crowd;
                            peakAt = i;
                        }
                    for (int i = peakAt + 1; i >= 1 && i < adaptiveLayer.Count; i++)
                        low = Mathf.Min(low, adaptiveLayer[i].level);
                    AdaptiveCheck("the intensity layer comes in when the screen is busy", peak >= 50 && peakCrowd >= 3f, $"peak {peak}% at crowd {peakCrowd:0.0}");
                    AdaptiveCheck("and goes out when it clears", peak >= 50 && low <= peak - 50, $"it fell to {(low == 100 ? "nothing seen" : low + "%")} after the peak");
                    input.ReleaseAll();
                    Run.Progress(0.7f);

                    // 3. The Magma King's arena.
                    Run.Event("shortcut", $"load level {MusicBossLevel} directly");
                    adaptiveMusic.Clear();
                    old = GameplayWorld.Instance;
                    Time.timeScale = 1f;
                    load = LoadLevel(MusicBossLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(MusicBossLevel), 60f, "the Slag Throne to start");
                    if (Run.Done) yield break;
                    yield return Run.Until(() => AdaptiveFirst(0, "magma_king_theme", "script") >= 0, 90f, "the Magma King's theme");
                    int theme = AdaptiveFirst(0, "magma_king_theme", "script");
                    AdaptiveCheck("the arena plays his theme", theme >= 0, theme >= 0 ? $"theme at {adaptiveMusic[theme].game:0.0} s into the arena" : "never started");
                    if (theme >= 0)
                    {
                        AdaptiveCheck("it takes over from the first beat of the lock", adaptiveMusic[theme].game <= 12f, $"{adaptiveMusic[theme].game:0.0} s in (the throne rises, his intro hit lands, the theme comes in)");
                        Run.Metric("adaptiveMusic.bossThemeSeconds", adaptiveMusic[theme].game);
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= AdaptiveTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("adaptiveMusic.passed", adaptivePassed);
                Run.Metric("adaptiveMusic.failed", adaptiveFailed);
                Run.Progress(1f);
                End(adaptiveFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
