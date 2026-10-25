using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The replay-modes scenario (Docs/Story.md "Again", cookbook "Replay modes"): Endless Floor, the Daily Challenge and New Game+, each opened
        /// from the main menu with real clicks. Endless: climbs floors and the best is saved and shown on the card. Daily: a fixed date (a setup
        /// shortcut) played twice gives the same seed, twist and generated enemies, and both plays are in the local table. New Game+: with a weapon
        /// unlocked and the Magma King's Waystone lit (setup shortcuts) it starts the next cycle at the Landing with the weapon still in his hands
        /// and a harder Hell.
        /// </summary>
        public const string ReplayModesId = "replay-modes";
        const float ReplayModesSeconds = 1500f;
        const string ReplayTestDate = "20261025";
        const string ReplayTestWeapon = "spitfire";

        /// <summary>The enemies the Ember Wastes' generator sends (content/circles/emberWastes.json): the picks the Daily must repeat.</summary>
        static readonly HashSet<string> ReplayGenerated = new HashSet<string> { "imp", "cinder", "impSack", "cinderThatch", "impGuard" };

        sealed partial class Session
        {
            int replayPassed, replayFailed;
            readonly List<string> replaySpawns = new List<string>();
            ReplayRun replayLast;

            void ReplayCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    replayPassed++;
                    Run.Event("check", $"replay-modes {beat}: ok ({detail})");
                }
                else
                {
                    replayFailed++;
                    Run.Event("check", $"replay-modes {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"replay-modes {beat}: {detail}");
                }
            }

            void ReplayTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "replay")
                    Run.Event("replay", $"{e.Subject} {e.Value} {e.Detail}".Trim());
                else if (e.Kind == "spawn" && ReplayGenerated.Contains(e.Subject ?? "") && replaySpawns.Count < 6)
                    replaySpawns.Add(e.Subject);
            }

            /// <summary>Opens a main menu card by its button and waits for it.</summary>
            IEnumerator ReplayOpenCard(string buttonLabel)
            {
                yield return ClickUntil(buttonLabel, () => ReplayPanel.IsOpen, 20f);
                if (Run.Done)
                    yield break;
                yield return WaitReal(0.6f);
            }

            /// <summary>Plays a replay level until 'done' says so (or it ends): the run is kept in replayLast.</summary>
            IEnumerator ReplayPlay(int level, System.Func<ReplayRun, bool> done, float gameSeconds, float lo, float hi)
            {
                replayLast = null;
                yield return PlayLevel(level, gameSeconds, null, lo, hi, 600f, false, () =>
                {
                    var world = GameplayWorld.Instance;
                    if (world == null || world.Replay == null)
                        return false;
                    replayLast = world.Replay;
                    return done(world.Replay);
                });
            }

            public IEnumerator ReplayModesRun()
            {
                var definition = GameDefinition.Instance;
                int endless = ReplayModes.LevelNumber(ReplayModes.EndlessLevelName), daily = ReplayModes.LevelNumber(ReplayModes.DailyLevelName);
                ReplayCheck("levels", endless > 0 && daily > 0 && definition != null && definition.replay != null && definition.replay.modifiers.Count >= 3,
                    $"Endless Floor is level {endless}, the Daily is level {daily}, {definition?.replay?.modifiers.Count ?? 0} daily twists");
                if (endless <= 0 || daily <= 0)
                {
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                var labels = definition.replay.labels;
                GameplayTelemetry.Recorded += ReplayTelemetry;
                try
                {
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    ReplayModes.ResetAll();
                    ReplayModes.DateOverride = ReplayTestDate;
                    Run.Event("shortcut", $"the replay save cleared; the Daily's date fixed at {ReplayTestDate}");

                    // ─── 1. Endless Floor: climbs, saves a best, shows it ───────────────────────────────────────
                    ReplayCheck("endless button", FindButton(labels.endless) != null, $"\"{labels.endless}\" on the main menu");
                    yield return ReplayOpenCard(labels.endless);
                    if (Run.Done) yield break;
                    ReplayCheck("endless card", ReplayPanel.IsOpen && ReplayPanel.Current.Kind == ReplayPanelKind.Endless && ReplayPanel.Current.Body == labels.endlessNoBest,
                        $"card \"{ReplayPanel.Current?.Title}\": \"{ReplayPanel.Current?.Body}\"");
                    yield return ClickUntil(labels.play, () => GameplayWorld.Instance != null && GameplayWorld.Instance.LevelNumber == endless, 40f);
                    if (Run.Done) yield break;

                    int bestFloors = 0;
                    for (int attempt = 1; attempt <= 3 && !Run.Done; attempt++)
                    {
                        if (attempt > 1)
                        {
                            Run.Event("retry", $"Endless Floor, attempt {attempt}");
                            var load = LoadLevel(endless);
                            if (load != null)
                                yield return load;
                        }
                        yield return ReplayPlay(endless, run => run.Floors >= 3, 300f, 0f, 0.3f);
                        if (Run.Done) yield break;
                        int floors = replayLast != null ? replayLast.Floors : 0;
                        bestFloors = Mathf.Max(bestFloors, floors);
                        if (bestFloors >= 3)
                            break;
                    }
                    var endlessWorld = GameplayWorld.Instance;
                    ReplayCheck("endless climbs", bestFloors >= 3,
                        $"{bestFloors} floors climbed" + (endlessWorld != null && endlessWorld.Stage.HasFloors ? $", the counter reads {endlessWorld.Stage.Floor}" : ""));
                    ReplayCheck("endless saves a best", ReplayModes.EndlessBest >= bestFloors && bestFloors > 0,
                        $"saved best {ReplayModes.EndlessBest} floors (climbed {bestFloors})");
                    Run.Metric("replay.endless.floors", bestFloors);
                    Run.Metric("replay.endless.best", ReplayModes.EndlessBest);
                    StopBot();
                    input.ReleaseAll();

                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    yield return ReplayOpenCard(labels.endless);
                    if (Run.Done) yield break;
                    string wantBest = string.Format(CultureInfo.InvariantCulture, labels.endlessBest, ReplayModes.EndlessBest);
                    ReplayCheck("endless shows the best", ReplayPanel.IsOpen && ReplayPanel.Current.Body == wantBest, $"card says \"{ReplayPanel.Current?.Body}\" (want \"{wantBest}\")");
                    yield return ClickUntil(labels.back, () => !ReplayPanel.IsOpen, 10f);
                    if (Run.Done) yield break;

                    // ─── 2. Daily Challenge: the same for a date, a local table ─────────────────────────────────
                    ReplayCheck("daily button", FindButton(labels.daily) != null, $"\"{labels.daily}\" on the main menu");
                    var twist = ReplayModes.ModifierFor(ReplayTestDate);
                    ReplayCheck("daily twist", twist != null && ReplayModes.ModifierFor(ReplayTestDate) == twist && ReplayModes.DailySeed(ReplayTestDate) == ReplayModes.DailySeed(ReplayTestDate)
                        && ReplayModes.DailySeed(ReplayTestDate) != ReplayModes.DailySeed("20261026"),
                        $"{ReplayTestDate}: seed {ReplayModes.DailySeed(ReplayTestDate)}, twist {twist?.name}; the next day's seed {ReplayModes.DailySeed("20261026")}");
                    yield return ReplayOpenCard(labels.daily);
                    if (Run.Done) yield break;
                    ReplayCheck("daily card", ReplayPanel.IsOpen && ReplayPanel.Current.Kind == ReplayPanelKind.Daily && ReplayPanel.Current.Body.Contains(ReplayModes.PrettyDate(ReplayTestDate))
                        && ReplayPanel.Current.Body.Contains(labels.dailyEmpty), $"card body \"{ReplayPanel.Current?.Body?.Replace('\n', '|')}\"");

                    var firstSpawns = new List<string>();
                    uint firstSeed = 0;
                    string firstTwist = "";
                    for (int play = 1; play <= 2 && !Run.Done; play++)
                    {
                        replaySpawns.Clear();
                        if (play == 1)
                        {
                            yield return ClickUntil(labels.play, () => GameplayWorld.Instance != null && GameplayWorld.Instance.LevelNumber == daily, 40f);
                        }
                        else
                        {
                            var load = LoadLevel(daily);
                            if (load != null)
                                yield return load;
                        }
                        if (Run.Done) yield break;
                        for (int attempt = 1; attempt <= 3 && !Run.Done; attempt++)
                        {
                            if (attempt > 1)
                            {
                                Run.Event("retry", $"the Daily, play {play}, attempt {attempt}");
                                replaySpawns.Clear();
                                var load = LoadLevel(daily);
                                if (load != null)
                                    yield return load;
                            }
                            yield return ReplayPlay(daily, run => run.Floors >= 3 && replaySpawns.Count >= 6, 240f, 0.3f, 0.6f);
                            if (Run.Done) yield break;
                            if (replayLast != null && replayLast.Floors >= 3)
                                break;
                        }
                        var world = GameplayWorld.Instance;
                        uint seed = world != null ? world.Seed : 0;
                        string modifier = replayLast != null && replayLast.Modifier != null ? replayLast.Modifier.id : "";
                        Run.Event("daily", $"play {play}: seed {seed}, twist {modifier}, floors {replayLast?.Floors}, score {replayLast?.Score}, first picks {string.Join(",", replaySpawns)}");
                        if (play == 1)
                        {
                            firstSpawns = new List<string>(replaySpawns);
                            firstSeed = seed;
                            firstTwist = modifier;
                        }
                        else
                        {
                            ReplayCheck("daily is the same for the date", seed == firstSeed && seed == ReplayModes.DailySeed(ReplayTestDate) && modifier == firstTwist && modifier == twist.id,
                                $"seed {firstSeed} / {seed}, twist {firstTwist} / {modifier}");
                            int n = Mathf.Min(firstSpawns.Count, replaySpawns.Count);
                            bool same = n >= 4;
                            for (int i = 0; i < n; i++)
                                same &= firstSpawns[i] == replaySpawns[i];
                            ReplayCheck("daily sends the same enemies", same, $"play 1: {string.Join(",", firstSpawns)}; play 2: {string.Join(",", replaySpawns)}");
                        }
                        StopBot();
                        input.ReleaseAll();
                    }

                    var table = ReplayModes.DailyTable(ReplayTestDate);
                    ReplayCheck("daily keeps a local table", table.Count >= 2 && table[0].Score >= table[1].Score && table[0].Date == ReplayTestDate,
                        $"{table.Count} scores for {ReplayTestDate}: " + string.Join(", ", table.ConvertAll(t => $"{t.Score} pts (Floor {t.Floors})")));
                    Run.Metric("replay.daily.scores", table.Count);
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    yield return ReplayOpenCard(labels.daily);
                    if (Run.Done) yield break;
                    ReplayCheck("daily card shows the table", ReplayPanel.IsOpen && ReplayPanel.Current.Body.Contains("1. ") && ReplayPanel.Current.Body.Contains("2. "),
                        $"card body \"{ReplayPanel.Current?.Body?.Replace('\n', '|')}\"");
                    yield return ClickUntil(labels.back, () => !ReplayPanel.IsOpen, 10f);
                    if (Run.Done) yield break;
                    ReplayModes.DateOverride = "";

                    // ─── 3. New Game+: keeps the weapon, raises Hell ────────────────────────────────────────────
                    WeaponUnlocks.Add(ReplayTestWeapon);
                    WaystoneProgress.Reach(1000);
                    WaystoneProgress.Reach(901);
                    Run.Event("shortcut", $"{ReplayTestWeapon} unlocked and the Waystones on FLOOR 1,000 and 901 lit, so New Game+ is offered");
                    int cycleBefore = ReplayModes.Cycle;
                    int landing = RunFlow.LevelFrom(RunFlow.Waystones()[0]);
                    var old = Object.FindAnyObjectByType<MainMenuManager>();
                    SickOfIt.Core.Flow.SceneTransitionManager.Instance.LoadMainMenu();
                    yield return Run.Until(() => { var m = Object.FindAnyObjectByType<MainMenuManager>(); return m != null && m != old && CountButtons() > 0; }, 45f, "the main menu again");
                    if (Run.Done) yield break;
                    yield return WaitReal(1.5f);
                    ReplayCheck("new game plus is offered", FindButton(labels.newGamePlus) != null, $"\"{labels.newGamePlus}\" on the main menu (cycle {cycleBefore})");
                    yield return ReplayOpenCard(labels.newGamePlus);
                    if (Run.Done) yield break;
                    ReplayCheck("new game plus card", ReplayPanel.IsOpen && ReplayPanel.Current.Kind == ReplayPanelKind.NewGamePlus && ReplayPanel.Current.Play.IsInteractable(),
                        $"card \"{ReplayPanel.Current?.Title}\": {ReplayPanel.Current?.Blurb}");
                    string again = string.Format(CultureInfo.InvariantCulture, labels.ngPlusPlay, cycleBefore + 2);
                    yield return ClickUntil(again, () => GameplayWorld.Instance != null && GameplayWorld.Instance.LevelNumber == landing, 60f);
                    if (Run.Done) yield break;
                    yield return Run.Until(() => LevelRunning(landing), 60f, "the New Game+ level to start");
                    if (Run.Done) yield break;

                    var ngWorld = GameplayWorld.Instance;
                    ReplayCheck("new game plus starts the next cycle at the Landing", ReplayModes.Cycle == cycleBefore + 1 && ngWorld.LevelNumber == landing
                        && ngWorld.Stage.HasFloors && ngWorld.Stage.Floor >= 999 && (WaystoneProgress.Highest == 1000 || WaystoneProgress.Highest == 0),
                        $"cycle {cycleBefore} -> {ReplayModes.Cycle}, level {ngWorld.LevelNumber}, floor {ngWorld.Stage.Floor}, Waystone {WaystoneProgress.Highest}");
                    ReplayCheck("new game plus keeps the weapon", WeaponUnlocks.Has(ReplayTestWeapon) && ngWorld.Weapons != null && ngWorld.Weapons.Has(ReplayTestWeapon),
                        $"{ReplayTestWeapon} unlocked {WeaponUnlocks.Has(ReplayTestWeapon)}, in his hands {(ngWorld.Weapons != null && ngWorld.Weapons.Has(ReplayTestWeapon))}; held {(ngWorld.Weapons != null ? string.Join(",", ngWorld.Weapons.Held) : "-")}");
                    var scale = ngWorld.Scale;
                    ReplayCheck("new game plus raises the difficulty", scale.Active && scale.Health > 1.2f && scale.AttackSpeed > 1.05f,
                        $"enemy health x{scale.Health:0.00}, attack speed x{scale.AttackSpeed:0.00}, speed x{scale.MoveSpeed:0.00}, +{scale.ExtraAlive} alive/push");

                    // The first enemy of the climb is built with it: its health is more than its file says.
                    bool seen = false, harder = false;
                    string seenDetail = "no enemy appeared in 90 s";
                    yield return PlayLevel(landing, 90f, null, 0.6f, 1f, 300f, false, () =>
                    {
                        var w = GameplayWorld.Instance;
                        if (w == null)
                            return false;
                        foreach (var e in w.Live)
                        {
                            if (e == null || !e.IsAlive || e.Def == null || e.Def.team != SickOfIt.Gameplay.Content.Team.Enemy || e.Def.health <= 1)
                                continue;
                            seen = true;
                            harder = e.MaxHealth > e.Def.health;
                            seenDetail = $"{e.DefId}: {e.MaxHealth} health (its file says {e.Def.health})";
                            return true;
                        }
                        return false;
                    });
                    if (Run.Done) yield break;
                    ReplayCheck("new game plus enemies are tougher", seen && harder, seenDetail);
                    StopBot();
                    input.ReleaseAll();
                    ReplayModes.ResetCycle();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= ReplayTelemetry;
                    ReplayModes.DateOverride = "";
                }
                Run.Metric("replay.passed", replayPassed);
                Run.Metric("replay.failed", replayFailed);
                Run.Progress(1f);
                End(replayFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }
        }
    }
}
