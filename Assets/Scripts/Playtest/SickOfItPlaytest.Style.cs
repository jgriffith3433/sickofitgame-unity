using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The style scenario (Docs/Story.md "Style"): Level 1 loaded directly and played by the bot through real input. The graze distance
        /// is widened (setup shortcut) so the imps' shots graze often. Checks: a graze sparks and fills the meter; a rewind removes the
        /// graze credit it undoes; the announcer's lines fire on a streak; then the floor's rank screen is opened (setup shortcut: the hits are
        /// zeroed so it is a no-hit floor) and must show a rank, say the no-hit line and save the best.
        /// </summary>
        public const string StyleId = "style";
        const int StyleLevel = 1;
        const float StyleSeconds = 300f;

        sealed partial class Session
        {
            int stylePassed, styleFailed;
            readonly List<float> grazeTimes = new List<float>();
            readonly List<string> styleLines = new List<string>();
            readonly List<string> stylePayouts = new List<string>();
            int styleUndone;
            string rankEvent = "";
            int rankPoints = -1;

            void StyleCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    stylePassed++;
                    Run.Event("check", $"style {beat}: ok ({detail})");
                }
                else
                {
                    styleFailed++;
                    Run.Event("check", $"style {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"style {beat}: {detail}");
                }
            }

            void StyleTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                switch (e.Kind)
                {
                    case "graze":
                        grazeTimes.Add(e.Time);
                        Run.Event("graze", $"streak {e.Value} at {e.Time:0.00}: {e.Detail}");
                        break;
                    case "grazeUndone":
                        styleUndone += e.Value;
                        Run.Event("graze-undone", $"{e.Value} graze(s) taken back by the rewind, {e.Detail} left");
                        break;
                    case "styleLine":
                        styleLines.Add(e.Subject);
                        Run.Event("style-line", $"{e.Subject}: \"{e.Detail}\"");
                        break;
                    case "stylePayout":
                        stylePayouts.Add(e.Subject);
                        Run.Event("style-payout", $"{e.Subject}: {e.Value} diamond(s), {e.Detail}");
                        break;
                    case "rank":
                        rankEvent = e.Subject;
                        rankPoints = e.Value;
                        Run.Event("rank", $"{e.Subject} ({e.Value} points): {e.Detail}");
                        break;
                }
            }

            public IEnumerator StyleRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                RogueliteManager.Instance.ResetAllRogueliteData();

                GameplayTelemetry.Recorded += StyleTelemetry;
                try
                {
                    Run.Event("shortcut", $"load level {StyleLevel} directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(StyleLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(StyleLevel), 60f, "level 1 to start");
                    if (Run.Done) yield break;
                    var world = GameplayWorld.Instance;
                    StyleCheck("the style system is on", world.Style != null && world.Style.Active, "world.Style");
                    if (world.Style == null)
                    {
                        End(PlaytestOutcome.Error);
                        yield break;
                    }
                    // Setup shortcut: the imps' shots graze from further away, and a streak lasts longer, so the bot meets every beat.
                    world.Style.Config.grazeDistance = 2.5f;
                    world.Style.Config.streakWindow = 8f;
                    Run.Event("shortcut", "grazeDistance 2.5, streakWindow 8 s (the bot's dodging still decides what grazes)");

                    bool rewound = false, sparkChecked = false;
                    yield return PlayLevel(StyleLevel, 0f, during: now =>
                    {
                        var w = GameplayWorld.Instance;
                        var hud = w != null ? w.Hud : null;
                        if (w == null || hud == null)
                            return null;
                        if (!sparkChecked && grazeTimes.Count >= 1)
                        {
                            sparkChecked = true;
                            StyleCheck("a graze sparks and fills the meter", hud.StylePunches >= 1 && w.Style.Meter > 0f && hud.StyleText.Length > 0,
                                $"{grazeTimes.Count} graze(s), the meter pops {hud.StylePunches}, meter {w.Style.Meter:0}, shows \"{hud.StyleText}\"");
                        }
                        if (!rewound && grazeTimes.Count >= 2 && now - grazeTimes[grazeTimes.Count - 1] < 0.8f && HasJuice())
                        {
                            rewound = true;
                            return StyleRewindCheck(w);
                        }
                        return null;
                    }, until: () => GameplayWorld.Instance != null && GameClock.Instance != null
                        && ((rewound && styleLines.Count >= 1 && GameClock.Instance.Time >= 20f) || GameClock.Instance.Time >= 120f));
                    if (Run.Done) yield break;

                    StyleCheck("grazes happened", grazeTimes.Count >= 3, $"{grazeTimes.Count} graze events");
                    StyleCheck("the rewind check ran", rewound, "a rewind right after a graze");
                    StyleCheck("an announcer line fired on a streak or tier", styleLines.Count >= 1, string.Join(", ", styleLines));
                    Run.Metric("style.grazes", grazeTimes.Count);
                    Run.Metric("style.payouts", stylePayouts.Count);

                    // The rank screen (setup shortcut: hits zeroed so the floor counts as a no-hit one).
                    world = GameplayWorld.Instance;
                    StopBot();
                    input.ReleaseAll();
                    Run.Event("shortcut", $"playerHits {world.GetCounter("playerHits")} zeroed, results opened with the floor as it stands");
                    world.SetCounter("playerHits", 0f);
                    int linesBefore = styleLines.Count;
                    world.Hud.ShowResults(() => { });
                    yield return WaitReal(2.5f);
                    StyleCheck("the results show a rank", world.Hud.ResultsOpen && rankEvent.Length == 1 && "SABC".Contains(rankEvent), $"rank {rankEvent}, {rankPoints} points");
                    bool saved = StyleRanks.TryGet(StyleLevel, out var best);
                    StyleCheck("the best rank is saved for the floor", saved && best.Letter == rankEvent && best.Points == rankPoints, saved ? $"{best.Letter} {best.Points}" : "nothing saved");
                    StyleCheck("the no-hit floor is called out", styleLines.Count > linesBefore && styleLines.Contains("perfect"), string.Join(", ", styleLines));
                    Run.Event("check", $"style screen: grazes on screen {world.Style.Grazes}");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= StyleTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("style.passed", stylePassed);
                Run.Metric("style.failed", styleFailed);
                Run.Progress(1f);
                End(styleFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            static bool HasJuice() => SickOfIt.Core.Rewind.RewindBudget.Instance == null || SickOfIt.Core.Rewind.RewindBudget.Instance.Juice > 1.6f;

            /// <summary>Holds R for 1.5 s right after a graze: the graze count must fall back to the grazes that happened before the moment he is back at.</summary>
            IEnumerator StyleRewindCheck(GameplayWorld w)
            {
                var before = new List<float>(grazeTimes);
                int grazesBefore = w.Style.Grazes;
                float meterBefore = w.Style.Meter;
                float timeBefore = GameClock.Instance.Time;
                int undoneBefore = styleUndone;
                yield return HoldRewind(1.5f);
                yield return WaitReal(0.4f);
                float timeAfter = GameClock.Instance.Time;
                int expected = 0;
                foreach (float t in before)
                {
                    if (t <= timeAfter + 0.001f)
                        expected++;
                }
                int now = w.Style.Grazes;
                StyleCheck("rewind removes the graze credit it undoes", timeAfter < timeBefore && now <= expected + 1 && now < grazesBefore,
                    $"time {timeBefore:0.00} -> {timeAfter:0.00}, grazes {grazesBefore} -> {now} (expected about {expected}), meter {meterBefore:0} -> {w.Style.Meter:0}");
                StyleCheck("the undone grazes are reported", styleUndone > undoneBefore, $"{styleUndone - undoneBefore} reported undone");
            }
        }
    }
}
