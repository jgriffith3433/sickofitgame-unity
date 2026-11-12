using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Rewind;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The floor arrival card: Floors 1000-997 each opened directly (a setup shortcut). Per floor: the stamped card shows with its
        /// number and name, the stamp sound starts, the card is gone within 1.5 s (+0.25 s slack) of showing while the hero's input is
        /// not locked, it sits inside the safe area, and the screen transition never holds a dark screen over 0.3 s once the level is in.
        /// Touch-driven on a phone-shaped screen only in that nothing here needs input (the card never blocks it). Check events `floor-card ...`.
        /// </summary>
        public const string FloorCardId = "floor-card";
        const float FloorCardSeconds = 240f;

        sealed partial class Session
        {
            void CardCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"floor-card {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"floor-card {beat}: {detail}");
            }

            public IEnumerator FloorCardRun()
            {
                var gd = GameDefinition.Instance;
                var stamps = new List<float>();
                System.Action<Sfx.PlayedSound> onPlayed = p => { if (p.id == "floor_stamp") stamps.Add(Time.realtimeSinceStartup); };
                Sfx.Played += onPlayed;
                int level = 2;
                for (int floor = 1000; floor >= 997 && !Run.Done; floor--, level++)
                {
                    var config = gd.GetLevel(level);
                    string shown = config != null ? config.displayName : "";
                    var old = GameplayWorld.Instance;
                    int cardsBefore = FloorCard.Shown;
                    stamps.Clear();
                    Run.Event("shortcut", $"load level {level} (Floor {floor}) directly");
                    var load = LoadLevel(level);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"Floor {floor} to start");
                    if (Run.Done) break;
                    yield return Run.Until(() => FloorCard.Shown > cardsBefore, 5f, $"Floor {floor}'s card");
                    bool made = FloorCard.Shown > cardsBefore;
                    float shownAt = Time.realtimeSinceStartup;
                    CardCheck($"{floor} card", made && FloorCard.LastFloor == floor, made ? $"\"FLOOR {FloorCard.LastFloor}\" over \"{FloorCard.LastName}\" (level says \"{shown}\")" : "no card");
                    bool inside = false;
                    for (int i = 0; i < 12 && FloorCard.Current != null; i++)
                    {
                        var r = FloorCard.ScreenRect;
                        var safe = Screen.safeArea;
                        // The slam starts large: judge it once settled (0.3 s in).
                        if (Time.realtimeSinceStartup - shownAt > 0.3f)
                            inside = r.width > 0f && r.xMin >= safe.xMin - 1f && r.xMax <= safe.xMax + 1f && r.yMin >= safe.yMin - 1f && r.yMax <= safe.yMax + 1f;
                        yield return WaitReal(0.1f);
                    }
                    CardCheck($"{floor} safe area", inside, $"card inside the {Screen.safeArea.width:0}x{Screen.safeArea.height:0} safe area on a {Screen.width}x{Screen.height} screen");
                    CardCheck($"{floor} stamp", stamps.Count > 0, $"{stamps.Count} stamp sound(s)");
                    yield return Run.Until(() => FloorCard.Current == null, 3f, "the card to go");
                    float gone = Time.realtimeSinceStartup - shownAt;
                    Run.Metric($"floorCard.floor{floor}.seconds", gone);
                    CardCheck($"{floor} gone", gone <= FloorCard.TotalSeconds + 0.25f, $"{gone:0.00}s on screen");
                    CardCheck($"{floor} input live", !RewindInput.Locked && ScreenTransition.Alpha < 0.3f, $"rewind locked {RewindInput.Locked}, dark {ScreenTransition.Alpha:0.00}");
                }
                Sfx.Played -= onPlayed;
                Run.Progress(1f);
                End(PlaytestOutcome.Won);
            }
        }
    }
}
