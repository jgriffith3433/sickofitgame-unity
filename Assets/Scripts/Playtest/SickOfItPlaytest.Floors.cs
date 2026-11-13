using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using UnityEngine.InputSystem;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The first four floors (Docs/LevelDesign.md "The first four floors: teach, then finale"): 1000 sinking stones, 999 rising lava, 998 the cinder, 997 the sack imps' chains.
        /// Each floor is opened directly (a setup shortcut) and played to its landing by the bot through real input (rewind allowed). Per floor: the teaching glow
        /// (`floors:&lt;n&gt;:glow`, a `teachGlow` entity on screen 0.5+ s, fully inside the playfield and clear of the touch stick, one screenshot via `Run.Scene`) and its cue sound
        /// (`:cue`) before the threat first acts; the finale (`:finale`, a `music` change after it, `:tint` with the screen flash drawn, ~15 s before the landing, one screenshot); the payoff stinger
        /// (`:payoff`) as the Shaft's pick opens; the deaths, the longest stretch the hero stood still (stuck spot, 5 s) and whether the finale was reached. On a portrait screen the touch UI is switched on
        /// (a named setup shortcut) so the stick and its zone are the real ones the glow must clear. Tools/Levels/first_floors.py writes the floors' teach, finale and payoff rules.
        /// </summary>
        public const string FloorsId = "floors";
        const float FloorsSeconds = 1500f;

        static readonly int[] FloorsList = { 1000, 999, 998, 997 };

        sealed partial class Session
        {
            readonly Dictionary<string, float> floorSignals = new Dictionary<string, float>();
            readonly List<float> floorMusic = new List<float>();

            void FloorsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal" && e.Subject.StartsWith("floors:"))
                {
                    if (!floorSignals.ContainsKey(e.Subject))
                        floorSignals[e.Subject] = e.Time;
                    Run.Event("floors", $"{e.Subject} at {e.Time:0.00}");
                    // A metric reaches the runner in the page's live state, an event only in the final report: when the page
                    // freezes the report is lost, so each cue, finale and payoff is also a metric the moment it happens.
                    // "floors:1000:finale" -> floors.floor1000.finale (the game second into the try it happened at).
                    var parts = e.Subject.Split(':');
                    if (parts.Length >= 3 && (parts[2] == "glow" || parts[2] == "cue" || parts[2] == "finale" || parts[2] == "tint" || parts[2] == "payoff" || parts[2] == "salvo"))
                        Run.Metric($"floors.floor{parts[1]}.{parts[2]}", Mathf.Max(e.Time, 0.01f));
                }
                else if (e.Kind == "music")
                {
                    floorMusic.Add(e.Time);
                    Run.Event("floors", $"music {e.Subject} ({e.Detail}) at {e.Time:0.00}");
                }
            }

            void FloorsCheck(string beat, bool ok, string detail, PlaytestSeverity severity = PlaytestSeverity.Bug)
            {
                Run.Event("check", $"floors {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(severity, $"floors {beat}: {detail}");
            }

            bool FloorSeen(string name) => floorSignals.ContainsKey(name);

            static Rect FloorsScreenRect(Camera cam, SpriteRenderer sprite)
            {
                var b = sprite.bounds;
                var a = cam.WorldToScreenPoint(b.min);
                var c = cam.WorldToScreenPoint(b.max);
                return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
            }

            /// <summary>Floor 1000's first salvo: the hit, the slowed time and the pulsing button, then R (a finger on the button on touch) undoes it.</summary>
            IEnumerator FloorsSalvoRewind(GameplayWorld w, float hitAt)
            {
                bool touch = GameplayHud.TouchUiOn;
                var rect = w.Hud != null ? w.Hud.RewindButtonScreenRect : default;
                float px = Mathf.Min(rect.width, rect.height);
                Run.Event("floors", $"floor 1000 salvo hit at {hitAt:0.0} s, time scale {Time.timeScale:0.00}, rewind button {rect.width:0}x{rect.height:0} px, touch {touch}");
                FloorsCheck("floor 1000 the first salvo hits within 30 s", hitAt <= 30f, $"at {hitAt:0.0} s");
                // 0.2 is the lesson's slow-motion (readable); the hurt freeze's 0.02 is a near stop, which is what the hold used to fall back to.
                FloorsCheck("floor 1000 the salvo slows time and pulses the rewind button", Time.timeScale >= 0.1f && Time.timeScale <= 0.3f && FirstRewindStage.Pulsing, $"time scale {Time.timeScale:0.000}, freeze active {SickOfIt.Core.Fx.HitFreeze.Active}");
                if (touch)
                    FloorsCheck("floor 1000 the rewind button is a 44+ px tap target", px >= 44f, $"{px:0} px");
                if (w.Hud != null)
                {
                    FloorsCheck("floor 1000 shows no dash or weapon chip", !w.Hud.DashButtonShown && !w.Hud.WeaponButtonShown, $"dash {w.Hud.DashButtonShown}, weapon {w.Hud.WeaponButtonShown}");
                    var hint = w.Hud.RewindHintScreenRect;
                    bool apart = hint.width <= 0f || !hint.Overlaps(rect);
                    FloorsCheck("floor 1000 hint and rewind button stay apart", apart, $"hint {hint}, button {rect}");
                }
                yield return WaitReal(0.5f);
                int id = -1;
                var at = rect.center;
                if (touch) id = input.TouchDown(at); else input.SetKey(Key.R, true);
                yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 4f, "the salvo rewind to start");
                bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                FloorsCheck("floor 1000 holding R (the button on touch) rewinds the salvo", rewinding, rewinding ? "rewinding" : "no rewind");
                yield return WaitReal(1.5f);
                if (touch) input.TouchUp(id, at); else input.SetKey(Key.R, false);
                yield return Run.Until(() => FirstRewindStage.Current == FirstRewindStage.Stage.Done, 6f, "the rewind to end");
                FloorsCheck("floor 1000 the rewind pays off: relief line, time back to normal", FirstRewindStage.Relieved > 0 && Time.timeScale > 0.9f, $"time scale {Time.timeScale:0.00}");
            }

            float floorsWorstFrame, floorsLiveAt;
            int floorsLiveDeaths = -1;

            /// <summary>
            /// Live readings for the runner's timeline (the page may freeze and never give its report back): the floor being played, its deaths
            /// so far, and the worst frame of the last two seconds (a stall shows as a long frame before a freeze, and the scene it was in).
            /// </summary>
            void FloorsLive(int floor, int floorDeaths)
            {
                floorsWorstFrame = Mathf.Max(floorsWorstFrame, Time.unscaledDeltaTime * 1000f);
                if (floorDeaths != floorsLiveDeaths)
                {
                    floorsLiveDeaths = floorDeaths;
                    Run.Metric($"floors.floor{floor}.deaths", floorDeaths);
                }
                if (Time.realtimeSinceStartup - floorsLiveAt < 2f)
                    return;
                floorsLiveAt = Time.realtimeSinceStartup;
                Run.Metric("floors.current", floor);
                Run.Metric("floors.worstFrameMs", floorsWorstFrame);
                Run.Metric("floors.timeScale", Time.timeScale);
                floorsWorstFrame = 0f;
            }

            public IEnumerator FloorsRun()
            {
                bool portrait = Screen.height > Screen.width;
                if (portrait)
                {
                    GameplayHud.TouchUiForced = true;
                    Run.Event("shortcut", "portrait screen: the touch UI on so the stick zone is the real one (the bot plays through its virtual pad)");
                }
                GameplayTelemetry.Recorded += FloorsTelemetry;
                int reached = 0;
                try
                {
                    for (int i = 0; i < FloorsList.Length && !Run.Done; i++)
                    {
                        int floor = FloorsList[i];
                        int n = LevelNumberOf("Floor" + floor);
                        if (n == 0)
                        {
                            FloorsCheck($"floor {floor} exists", false, "no such level in game.json");
                            continue;
                        }
                        string p = $"floors:{floor}:";
                        int deathsBefore = deaths;
                        floorsLiveDeaths = -1;
                        int sceneLoads = 0, mostLoadsToOpen = 0;
                        float stillMax = 0f, stillSince = -1f, stillSpot = 0f;
                        Vector2 stillAt = Vector2.zero;
                        bool shotTeach = false, shotFinale = false, glowOk = false, glowSeen = false, tintDrawn = false;
                        float glowAlive = 0f, glowFirst = -1f;
                        string glowWhere = "no glow entity seen";
                        for (int attempt = 1; attempt <= 2 && !FloorSeen(p + "payoff") && !Run.Done; attempt++)
                        {
                            floorSignals.Clear();
                            floorMusic.Clear();
                            shotTeach = shotFinale = false;
                            int loadsBefore = SickOfIt.Core.Addressables.AddressablesSceneLoader.LoadsStarted;
                            yield return OpenLevel(n, attempt);
                            // A retry is a few scene loads (the death scene, the level). More is the Level <-> Death reload loop
                            // that froze this scenario after the first retry.
                            int opened = SickOfIt.Core.Addressables.AddressablesSceneLoader.LoadsStarted - loadsBefore;
                            sceneLoads += opened;
                            mostLoadsToOpen = Mathf.Max(mostLoadsToOpen, opened);
                            Run.Event("floors", $"floor {floor} try {attempt}: {opened} scene load(s) to reach the level ({SickOfIt.Core.Addressables.AddressablesSceneLoader.LoadsJoined} joined, {SickOfIt.Core.Addressables.AddressablesSceneLoader.LoadsQueued} queued so far)");
                            if (Run.Done || !openedLevel)
                                break;
                            bool salvoWatched = false;
                            yield return PlayLevel(n, 260f, now =>
                            {
                                var w = GameplayWorld.Instance;
                                if (w == null || w.PlayerController == null)
                                    return null;
                                FloorsLive(floor, deaths - deathsBefore);
                                if (floor == 1000 && !salvoWatched && FirstRewindStage.Current == FirstRewindStage.Stage.Slow)
                                {
                                    salvoWatched = true;
                                    return FloorsSalvoRewind(w, now);
                                }
                                // Stuck: the longest stretch of game time he stays within half a unit (not while rewinding or on a locked beat).
                                Vector2 at = w.PlayerController.transform.position;
                                if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                                    stillSince = -1f;
                                else if (stillSince < 0f || Vector2.Distance(at, stillAt) > 0.5f)
                                {
                                    stillSince = now;
                                    stillAt = at;
                                }
                                else if (now - stillSince > stillMax && !FloorSeen(p + "payoff"))
                                {
                                    stillMax = now - stillSince;
                                    stillSpot = now;
                                }
                                var glow = w.FindById($"teach{floor}");
                                if (glow != null && glow.Sprite != null && w.Camera != null)
                                {
                                    glowSeen = true;
                                    if (glowFirst < 0f) glowFirst = now;
                                    glowAlive = Mathf.Max(glowAlive, now - glowFirst);
                                    var rect = FloorsScreenRect(w.Camera, glow.Sprite);
                                    var play = w.Camera.pixelRect;
                                    var zone = w.Hud != null ? w.Hud.TouchStickZoneScreenRect : default;
                                    var ring = w.Hud != null ? w.Hud.TouchStickScreenRect : default;
                                    bool inside = play.Contains(rect.min) && play.Contains(rect.max);
                                    bool clear = !(zone.width > 1f && zone.Overlaps(rect)) && !(ring.width > 1f && ring.Overlaps(rect));
                                    glowOk = inside && clear;
                                    glowWhere = $"glow {rect} playfield {play} stick zone {zone} ring {ring} ({Screen.width}x{Screen.height})";
                                    if (!shotTeach)
                                    {
                                        shotTeach = true;
                                        Run.Event("floors", $"floor {floor} teaching glow: {glowWhere}, inside {inside}, clear of the stick {clear}");
                                        Run.Scene($"floors {floor} teaching glow");
                                    }
                                }
                                if (!shotFinale && FloorSeen(p + "tint") && now >= floorSignals[p + "tint"] + 0.4f)
                                {
                                    shotFinale = true;
                                    tintDrawn = w.Hud != null && w.Hud.LastFlashAlpha > 0.01f;
                                    Run.Event("floors", $"floor {floor} finale: flash alpha {(w.Hud != null ? w.Hud.LastFlashAlpha : 0f):0.00}");
                                    Run.Scene($"floors {floor} finale");
                                }
                                return null;
                            }, (float)i / FloorsList.Length, (float)(i + 1) / FloorsList.Length, 330f, waitOutDeath: true,
                               until: () => FloorSeen(p + "payoff"));
                        }
                        if (Run.Done) break;
                        string label = $"floor {floor}";
                        int floorDeaths = deaths - deathsBefore;
                        bool done = FloorSeen(p + "payoff");
                        bool finale = FloorSeen(p + "finale");
                        if (finale) reached++;
                        Run.Metric($"floors.floor{floor}.deaths", floorDeaths);
                        Run.Metric($"floors.floor{floor}.stillSeconds", stillMax);
                        Run.Event("floors", $"{label}: {floorDeaths} death(s), longest standing still {stillMax:0.0} s (at {stillSpot:0.0}), finale reached {finale}, landing {done}, outcome {lastOutcome}");
                        FloorsCheck($"{label} teaches with a glow", FloorSeen(p + "glow") && glowSeen, glowSeen ? $"{glowAlive:0.0} s on screen" : "the teachGlow entity never appeared");
                        FloorsCheck($"{label} glow lasts 0.5+ s", glowAlive >= 0.5f || (glowSeen && FloorSeen(p + "cue")), $"{glowAlive:0.0} s seen by the bot (the entity lives 1.8 s)");
                        FloorsCheck($"{label} glow cue sound is logged before the threat", FloorSeen(p + "cue"), FloorSeen(p + "cue") ? $"at {floorSignals[p + "cue"]:0.0} s" : "no cue");
                        FloorsCheck($"{label} glow is inside the playfield and clear of the touch stick", glowSeen && glowOk, glowWhere);
                        FloorsCheck($"{label} finale is reached", finale, finale ? $"at {floorSignals[p + "finale"]:0.0} s" : "the finale section never started");
                        bool music = finale && floorMusic.Exists(t => t >= floorSignals[p + "finale"] - 0.05f);
                        FloorsCheck($"{label} finale changes the music", music, music ? "a music switch after the finale began" : "no music event after the finale began");
                        FloorsCheck($"{label} finale tints the whole screen", FloorSeen(p + "tint") && tintDrawn, $"flash drawn {tintDrawn}");
                        FloorsCheck($"{label} payoff stinger plays as the Shaft opens", done, done ? $"at {floorSignals[p + "payoff"]:0.0} s" : "no payoff signal");
                        if (finale && done)
                        {
                            float span = floorSignals[p + "payoff"] - floorSignals[p + "finale"];
                            Run.Metric($"floors.floor{floor}.finaleSeconds", span);
                            FloorsCheck($"{label} finale lasts 15-20 s (10-35 s allowed)", span >= 10f && span <= 35f, $"{span:0.0} s from the finale to the landing", PlaytestSeverity.Warning);
                        }
                        // Each floor's own payoff: its reward signal (diamonds / rewind / heart) fires with the landing, after the finale is over.
                        string rewardKey = null;
                        foreach (var k in floorSignals.Keys)
                            if (k.StartsWith(p + "reward:")) rewardKey = k;
                        FloorsCheck($"{label} pays its own reward at the landing", rewardKey != null && FloorSeen(p + "finaleOver") && floorSignals[rewardKey] >= floorSignals[p + "finaleOver"],
                            rewardKey != null ? $"{rewardKey.Substring(p.Length)} at {floorSignals[rewardKey]:0.0} s" : "no reward signal");
                        if (rewardKey != null) Run.Event("floors", $"floor {floor} reward {rewardKey.Substring(p.Length)}");
                        FloorsCheck($"{label} has no stuck spot over 5 s", stillMax <= 5f, $"longest still {stillMax:0.0} s");
                        // The death scene, the level and one replay of either at most: more is a Level <-> Death reload loop (the page hung after a retry).
                        FloorsCheck($"{label} every try opens in 4 scene loads or fewer (no Level/Death reload loop)", mostLoadsToOpen <= 4, $"{sceneLoads} scene load(s) over the tries, the most for one try {mostLoadsToOpen}");
                    }
                    Run.Metric("floors.finalesReached", reached);
                    FloorsCheck("every floor's finale is reached", reached == FloorsList.Length, $"{reached} of {FloorsList.Length}");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= FloorsTelemetry;
                    if (portrait)
                        GameplayHud.TouchUiForced = false;
                }
            }
        }
    }
}
