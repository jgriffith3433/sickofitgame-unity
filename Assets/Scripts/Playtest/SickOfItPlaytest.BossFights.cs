using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The boss-fights scenario (Docs/Story.md "The bosses' second look"): the six bosses (the Imp Foreman 991, the Pile Keeper 951, the Magma King 901,
        /// the Road Breaker 891, the Carver 851, the Spire Fiend 801) each loaded directly (setup shortcut) and fought by the bot through real input, with one
        /// more try after a death. Each fight gets the stage and arena checks and the rework's own (`bossFight.*`): 3+ phases from the fight's signals, 4-8 minions of
        /// mixed types alive at the peak (and the peak of every phase), the fight 120-240 s from its fight to its defeat signal, and the hero's voiced weakness hint.
        /// `boss-phase` and `boss-fight` events log the timeline.
        /// </summary>
        public const string BossFightsId = "boss-fights";
        const float BossFightsSeconds = 3600f;

        sealed partial class Session
        {
            public IEnumerator BossFightsRun()
            {
                float deadline = Time.realtimeSinceStartup + BossFightsSeconds / 2f - 180f;
                int played = 0;
                for (int i = 0; i < BossFights.Length && !Run.Done; i++)
                {
                    var def = BossFights[i];
                    int n = LevelNumberByName(def.Level);
                    if (n <= 0)
                    {
                        StageCheck($"bossFights.{def.Level}", false, $"{def.Level} ({def.Name}) is not a level in game.json");
                        continue;
                    }
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        Run.Event("budget", $"no time left: {def.Name} not reached");
                        continue;
                    }
                    float lo = i / (float)BossFights.Length, hi = (i + 1) / (float)BossFights.Length;
                    for (int attempt = 1; attempt <= 2 && !Run.Done; attempt++)
                    {
                        Run.Event("boss-fight", $"{def.Name}: level {n} ({def.Level}), try {attempt}");
                        Prepare(n, true);
                        var old = GameplayWorld.Instance;
                        var load = LoadLevel(n);
                        if (load != null)
                            yield return load;
                        yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(n), 60f, $"level {n} ({def.Name}) to start");
                        if (Run.Done) yield break;
                        stage = new StageWatch(n, StageCheck);
                        yield return PlayLevel(n, 0f, StageDuring, lo, hi, 720f, waitOutDeath: true);
                        if (Run.Done) yield break;
                        bool again = lastOutcome == "died" && attempt < 2;
                        if (!again)
                            FinishStage(lastOutcome);
                        stage = null;
                        if (!again)
                        {
                            played++;
                            break;
                        }
                        Run.Event("retry", $"{def.Name}, attempt {attempt + 1}");
                    }
                }
                Run.Metric("bossFights.played", played);
                Run.Metric("stage.passed", stageChecksPassed);
                Run.Metric("stage.failed", stageChecksFailed);
                Run.Progress(1f);
                End(stageChecksFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }
        }
    }
}
