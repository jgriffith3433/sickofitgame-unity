using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The enemy-hints scenario (Docs/GameplayCookbook.md "The hero sizes them up"): Level 1's imps. New Game forgets what he has
        /// seen; the first imp gets his hint once; a rewind past it, a second imp and a new run do not repeat it; a scene stays silent.
        /// </summary>
        public const string EnemyHintsId = "enemy-hints";
        const int EnemyHintsLevel = 1;
        const float EnemyHintsSeconds = 240f;

        sealed partial class Session
        {
            int hintsPassed, hintsFailed;
            readonly Dictionary<string, int> hintEvents = new Dictionary<string, int>();
            readonly HashSet<int> hintImpSerials = new HashSet<int>();

            void HintsCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    hintsPassed++;
                    Run.Event("check", $"enemy-hints {beat}: ok ({detail})");
                }
                else
                {
                    hintsFailed++;
                    Run.Event("check", $"enemy-hints {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"enemy-hints {beat}: {detail}");
                }
            }

            void HintsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind != "hint")
                    return;
                hintEvents.TryGetValue(e.Subject, out int n);
                hintEvents[e.Subject] = n + 1;
                Run.Event("hint", $"{e.Subject} at {e.Time:0.00}: \"{e.Detail}\"");
            }

            int HintCount(string name) => hintEvents.TryGetValue(name, out int n) ? n : 0;

            void CountImps(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.Def != null && e.Def.displayName == "Imp")
                        hintImpSerials.Add(e.Serial);
                }
            }

            public IEnumerator EnemyHintsRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Entities.TryGetValue("imp", out var impDef) || string.IsNullOrEmpty(impDef.displayName) || string.IsNullOrEmpty(impDef.hint))
                {
                    Run.Problem(PlaytestSeverity.Bug, "enemy-hints: the imp has no displayName and hint in the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // Every creature that is a type of its own has a name and a hint (hazards and bosses are the exceptions).
                int named = 0;
                var missing = new List<string>();
                foreach (var pair in content.Entities)
                {
                    var def = pair.Value;
                    if (!string.IsNullOrEmpty(def.displayName))
                    {
                        named++;
                        if (string.IsNullOrEmpty(def.hint))
                            missing.Add(pair.Key);
                    }
                }
                HintsCheck("enemy types have a name and a hint", named >= 10 && missing.Count == 0, $"{named} entities named; without a hint: {string.Join(", ", missing)}");

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // New Game forgets.
                EnemyHints.Add("Imp");
                RogueliteManager.Instance.ResetAllRogueliteData();
                HintsCheck("New Game forgets what he has seen", EnemyHints.All().Count == 0, $"{EnemyHints.All().Count} types remembered after the reset");

                GameplayTelemetry.Recorded += HintsTelemetry;
                try
                {
                    // 1. The first imp, a rewind, more imps.
                    Run.Event("shortcut", $"load level {EnemyHintsLevel} directly");
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(EnemyHintsLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(EnemyHintsLevel), 60f, "level 1 to start");
                    if (Run.Done) yield break;
                    int hintsBeforeRewind = -1;
                    bool rewound = false;
                    int impsAtRewind = 0;
                    yield return PlayLevel(EnemyHintsLevel, 0f, during: now =>
                    {
                        var w = GameplayWorld.Instance;
                        if (w != null)
                            CountImps(w);
                        if (!rewound && now >= 10f && HintCount("Imp") > 0)
                        {
                            rewound = true;
                            hintsBeforeRewind = HintCount("Imp");
                            impsAtRewind = hintImpSerials.Count;
                            return HoldRewind(2f);
                        }
                        return null;
                    }, until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= 24f && rewound);
                    if (Run.Done) yield break;
                    HintsCheck("the first imp gets his hint", HintCount("Imp") >= 1, $"{HintCount("Imp")} hint events for Imp");
                    HintsCheck("it is said once", HintCount("Imp") == 1, $"{HintCount("Imp")} hints, {hintImpSerials.Count} imps seen");
                    HintsCheck("a second imp does not repeat it", hintImpSerials.Count >= 2 && HintCount("Imp") == 1, $"{hintImpSerials.Count} imps spawned, {HintCount("Imp")} hint");
                    HintsCheck("a rewind does not repeat it", rewound && HintCount("Imp") == hintsBeforeRewind, $"{hintsBeforeRewind} before the rewind, {HintCount("Imp")} after (imps {impsAtRewind} then {hintImpSerials.Count})");
                    HintsCheck("it is remembered in the save", EnemyHints.Has("Imp"), string.Join(", ", EnemyHints.All()));
                    foreach (var pair in hintEvents)
                        HintsCheck("no type repeats: " + pair.Key, pair.Value == 1, $"{pair.Value} hints");

                    // 2. A new run (the level loaded again, as after a death): nothing is said again.
                    int before = HintCount("Imp");
                    old = GameplayWorld.Instance;
                    load = LoadLevel(EnemyHintsLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(EnemyHintsLevel), 60f, "level 1 to start again");
                    if (Run.Done) yield break;
                    hintImpSerials.Clear();
                    yield return PlayLevel(EnemyHintsLevel, 0f, during: now =>
                    {
                        var w = GameplayWorld.Instance;
                        if (w != null)
                            CountImps(w);
                        return null;
                    }, until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= 10f);
                    if (Run.Done) yield break;
                    HintsCheck("a new run does not repeat it", hintImpSerials.Count >= 1 && HintCount("Imp") == before, $"{hintImpSerials.Count} imps in the new run, hints {before} -> {HintCount("Imp")}");

                    // 3. A scene stays silent, and the type is not used up.
                    EnemyHints.Reset();
                    int sceneBefore = HintCount("Imp");
                    LevelRuntime.PendingScene = BystanderScene;
                    old = GameplayWorld.Instance;
                    load = LoadLevel(EnemyHintsLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(EnemyHintsLevel), 60f, "the bench to start");
                    LevelRuntime.PendingScene = null;
                    if (Run.Done) yield break;
                    StopBot();
                    var sceneWorld = GameplayWorld.Instance;
                    yield return WaitReal(1f);
                    sceneWorld.Emit("bys:imp");
                    yield return Run.Until(() => BystanderFind(sceneWorld, "shooter") != null, 6f, "the scene's imp to appear");
                    yield return WaitReal(1.5f);
                    HintsCheck("a scripted scene is silent", BystanderFind(sceneWorld, "shooter") != null && HintCount("Imp") == sceneBefore && !EnemyHints.Has("Imp"),
                        $"scene \"{sceneWorld.SceneId}\", imp spawned, hints {sceneBefore} -> {HintCount("Imp")}, remembered: {EnemyHints.Has("Imp")}");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= HintsTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("enemyHints.passed", hintsPassed);
                Run.Metric("enemyHints.failed", hintsFailed);
                Run.Progress(1f);
                End(hintsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
