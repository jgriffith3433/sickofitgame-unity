using System.Collections;
using System.Text;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The ui-polish scenario (Docs/Story.md "The HUD and menus"): the HUD and every popup animate on open and close, the boss bar
        /// has an intro (the name lands, the bar fills), pickups and tickers give feedback, the results count up, a screen transition
        /// sits between floors, and the layout holds at the device's size (run it on a 1920x1080 and a 1080x1920 device).
        /// </summary>
        public const string UiPolishId = "ui-polish";
        const int UiPolishBossLevel = 31;
        const float UiPolishSeconds = 720f;

        sealed partial class Session
        {
            int uiPassed, uiFailed;

            void UiCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    uiPassed++;
                    Run.Event("check", $"ui-polish {beat}: ok ({detail})");
                }
                else
                {
                    uiFailed++;
                    Run.Event("check", $"ui-polish {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"ui-polish {beat}: {detail}");
                }
            }

            static bool UiInside(Rect r, Rect area) => r.width > 0f && r.xMin >= area.xMin - 2f && r.xMax <= area.xMax + 2f
                                                         && r.yMin >= area.yMin - 2f && r.yMax <= area.yMax + 2f;

            IEnumerator UiLoad(int level)
            {
                Run.Event("shortcut", $"load level {level} directly");
                var old = GameplayWorld.Instance;
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} to start");
            }

            public IEnumerator UiPolishRun()
            {
                var gd = GameDefinition.Instance;
                var texts = gd.menuTheme.texts;
                var codex = gd.codex;
                var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
                Run.Metric("screenWidth", Screen.width);
                Run.Metric("screenHeight", Screen.height);
                Run.Event("ui-polish", $"screen {Screen.width}x{Screen.height} ({(MenuScreen.Portrait ? "portrait" : "landscape")}), safe area {Screen.safeArea}");

                // 1. A menu popup animates open and closed: the main menu's Codex (real clicks).
                yield return ToMainMenu();
                if (Run.Done) yield break;
                if (codex != null && codex.enabled)
                {
                    int opened = PanelAnim.Opened, closed = PanelAnim.Closed;
                    yield return ClickUntil(codex.buttonLabel, () => CodexPanel.IsOpen, 15f);
                    if (Run.Done) yield break;
                    UiCheck("menu popup opens animated", PanelAnim.Opened > opened && PanelAnim.LastOpened == "Codex", $"{PanelAnim.Opened - opened} panel(s) opened, last \"{PanelAnim.LastOpened}\"");
                    yield return WaitReal(PanelAnim.OpenSeconds + 0.3f);
                    UiCheck("popup settled", PanelAnim.Animating == 0, $"{PanelAnim.Animating} still animating");
                    yield return ClickUntil(texts.done, () => !CodexPanel.IsOpen, 10f);
                    if (Run.Done) yield break;
                    UiCheck("menu popup closes animated", PanelAnim.Closed > closed, $"{PanelAnim.Closed - closed} panel(s) closed with a fade");
                    yield return WaitReal(PanelAnim.CloseSeconds + 0.3f);
                }

                // 2. Level 1: HUD pickups and tickers, pause open/close, layout.
                yield return UiLoad(1);
                if (Run.Done) yield break;
                yield return PlayLevel(1, 6f);
                if (Run.Done) yield break;
                StopBot();
                var hud = GameplayWorld.Instance.Hud;
                int spawned = HudFloaters.Spawned, retargets = NumberTicker.Retargets;
                Run.Event("shortcut", "RogueliteManager.AddPoints(7): a diamond pickup, without waiting for a drop");
                RogueliteManager.Instance.AddPoints(7);
                yield return WaitReal(0.15f);
                UiCheck("pickup feedback: a +7 rises from the diamonds", HudFloaters.Spawned > spawned && HudFloaters.Last == "+7", $"floaters {HudFloaters.Spawned - spawned}, last \"{HudFloaters.Last}\"");
                UiCheck("the diamond counter ticks up", NumberTicker.Retargets > retargets, $"{NumberTicker.Retargets - retargets} counter(s) retargeted, {NumberTicker.Active} counting");
                yield return WaitReal(1.4f);
                UiCheck("the ticker settles", NumberTicker.Active == 0, $"{NumberTicker.Active} still counting");

                int popOpened = PanelAnim.Opened, popClosed = PanelAnim.Closed;
                yield return ClickUntil("II", () => hud.Paused, 10f);
                if (Run.Done) yield break;
                UiCheck("pause opens animated", PanelAnim.Opened > popOpened && PanelAnim.LastOpened == "Pause", $"last opened \"{PanelAnim.LastOpened}\"");
                yield return Audit("Pause (ui-polish)", 0, 1, 0.5f, progress: false);
                yield return ClickUntil(texts.resume, () => !hud.Paused, 10f);
                if (Run.Done) yield break;
                UiCheck("pause closes animated", PanelAnim.Closed > popClosed && PanelAnim.LastClosed == "Pause", $"last closed \"{PanelAnim.LastClosed}\"");
                yield return Audit("Level 1 HUD (ui-polish)", 0, 1, 1.5f, progress: false);

                // 3. The boss intro: the name lands and the bar fills from empty. Level 31 (the Slag Throne) loaded directly.
                yield return UiLoad(UiPolishBossLevel);
                if (Run.Done) yield break;
                hud = GameplayWorld.Instance.Hud;
                int intros = GameplayHud.BossIntros;
                yield return PlayLevel(UiPolishBossLevel, 0f, until: () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null
                                                                             && GameplayWorld.Instance.Hud.BossBarShown.Length > 0);
                if (Run.Done) yield break;
                hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null || hud.BossBarShown.Length == 0)
                {
                    UiCheck("the boss bar appears", false, $"no boss bar ({lastOutcome})");
                }
                else
                {
                    var safe = Screen.safeArea;
                    float firstFill = hud.BossBarFill, maxFill = firstFill;
                    string name = hud.BossBarShown;
                    var rect = hud.BossBarScreenRect;
                    var trace = new StringBuilder();
                    float t0 = Time.realtimeSinceStartup;
                    while (!Run.Done && Time.realtimeSinceStartup - t0 < GameplayHud.BossIntroSeconds + 0.5f)
                    {
                        maxFill = Mathf.Max(maxFill, hud.BossBarFill);
                        if (trace.Length < 200)
                            trace.Append($"{hud.BossIntroAge:0.0}s:{hud.BossBarFill:0.00} ");
                        yield return null;
                    }
                    if (Run.Done) yield break;
                    float finalFill = hud.BossBarFill;
                    UiCheck("boss intro shows a name", name.Length > 0, $"\"{name}\"");
                    UiCheck("boss bar intro fills from empty", firstFill < 0.5f && finalFill > firstFill - 0.001f && maxFill >= 0.9f,
                        $"started at {firstFill:0.00}, reached {maxFill:0.00}, now {finalFill:0.00}; {trace}");
                    UiCheck("boss bar is on screen and inside the safe area", UiInside(rect, screenRect) && UiInside(rect, safe), $"{rect} on {screenRect.width}x{screenRect.height}, safe {safe}");
                    UiCheck("a boss intro was played", GameplayHud.BossIntros > intros, $"{GameplayHud.BossIntros - intros} intro(s)");
                    Run.Event("boss-intro", $"\"{name}\" bar {firstFill:0.00} -> {maxFill:0.00} at {Screen.width}x{Screen.height}");
                    yield return Audit("Boss bar (ui-polish)", 0, 1, 0.3f, progress: false);
                }
                StopBot();

                // 4. A floor stage: the sweep on each new floor. Level 2 (Floor 1000), real play.
                yield return UiLoad(2);
                if (Run.Done) yield break;
                int sweeps = ScreenTransition.Sweeps;
                yield return PlayLevel(2, 120f, until: () => ScreenTransition.Sweeps > sweeps);
                if (Run.Done) yield break;
                UiCheck("a transition sweeps the screen when the floor changes", ScreenTransition.Sweeps > sweeps, $"{ScreenTransition.Sweeps - sweeps} sweep(s)");
                StopBot();

                // 5. The results and the transition out of the floor: Level 1 played to the end.
                yield return UiLoad(1);
                if (Run.Done) yield break;
                yield return PlayLevel(1, 0f);
                if (Run.Done) yield break;
                StopBot();
                hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null)
                {
                    UiCheck("results appear", false, $"the level ended {lastOutcome}");
                }
                else
                {
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen, 30f, "the results screen");
                    if (Run.Done) yield break;
                    UiCheck("results appear", GameplayWorld.Instance != null && GameplayWorld.Instance.Hud.ResultsOpen, "the results screen is up");
                    yield return WaitReal(0.9f);
                    UiCheck("results numbers count up", NumberTicker.Active > 0 || NumberTicker.Retargets > retargets + 1, $"{NumberTicker.Active} counting");
                    yield return Audit("Results (ui-polish)", 0, 1, 3.5f, progress: false);
                    int covers = ScreenTransition.Covers;
                    yield return ClickUntil(texts.continueText, () => ScreenTransition.Covers > covers, 15f);
                    if (Run.Done) yield break;
                    float peak = 0f;
                    float tc = Time.realtimeSinceStartup;
                    while (!Run.Done && Time.realtimeSinceStartup - tc < 1.5f)
                    {
                        peak = Mathf.Max(peak, ScreenTransition.Alpha);
                        yield return null;
                    }
                    UiCheck("the screen transition covers the screen between floors", peak > 0.9f, $"peak cover {peak:0.00}, label \"{ScreenTransition.LastLabel}\"");
                    yield return Run.Until(() => !ScreenTransition.Covered && ScreenTransition.Alpha < 0.05f, 30f, "the screen to open again");
                    UiCheck("the next scene opens the transition", !ScreenTransition.Covered, $"cover {ScreenTransition.Alpha:0.00}, {ScreenTransition.Reveals} reveal(s)");
                }

                Run.Metric("uiPolish.passed", uiPassed);
                Run.Metric("uiPolish.failed", uiFailed);
                Run.Metric("uiPolish.layoutProblems", layoutProblems);
                Run.Progress(1f);
                End(uiFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
