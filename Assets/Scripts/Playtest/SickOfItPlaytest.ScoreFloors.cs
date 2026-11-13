using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The adaptive score (Docs/Story.md "Music", "The adaptive score"): the Meadow and Floors 1000-997 (levels 1-5), each loaded directly
        /// (a setup shortcut) and played by the bot through real input. Per floor: at least 2 layer changes (`musicState` events), the fight layer
        /// in within 2 s of the first enemy and out within 4 s of the wave's last, the finale's own cue (not the fight's) once the floor's finale
        /// signal fires, and a held R tape-stopping the score (`musicTapeStop`) within 0.3 s with the score back after. Runs only on floors whose
        /// `score` is wired (Tools/Audio/adaptive_score.py --wire); an unwired floor is reported, not failed, so the scenario can ship before the clips.
        /// Phone: run it at 390x844 touch; the bot plays by the touch stick there.
        /// </summary>
        public const string ScoreFloorsId = "score-floors";

        sealed partial class Session
        {
            readonly List<(float game, string state, float real)> scoreStates = new List<(float, string, float)>();
            readonly List<float> scoreTape = new List<float>();
            readonly List<float> scoreEnemyAt = new List<float>();
            readonly List<float> scoreWaveEnd = new List<float>();
            int scorePassed, scoreFailed;

            void ScoreCheck(string beat, bool ok, string detail)
            {
                if (ok) { scorePassed++; Run.Event("check", $"score-floors {beat}: ok ({detail})"); }
                else
                {
                    scoreFailed++;
                    Run.Event("check", $"score-floors {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"score-floors {beat}: {detail}");
                }
            }

            void ScoreTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "musicState")
                {
                    scoreStates.Add((e.Time, e.Subject, Time.realtimeSinceStartup));
                    Run.Event("musicLayer", $"score {e.Subject} at {e.Time:0.0} s ({e.Detail})");
                }
                else if (e.Kind == "musicTapeStop")
                {
                    scoreTape.Add(Time.realtimeSinceStartup);
                    Run.Event("musicTapeStop", e.Detail);
                }
                else if (e.Kind == "musicWave")
                    (e.Subject == "start" ? scoreEnemyAt : scoreWaveEnd).Add(e.Time);
            }

            public IEnumerator ScoreFloorsRun()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                GameplayTelemetry.Recorded += ScoreTelemetry;
                try
                {
                    // Touch only for the first start: the sound prompt tap unlocks the music on a phone.
                    yield return TapSoundPrompt();
                    for (int level = 1; level <= 5; level++)
                    {
                        scoreStates.Clear(); scoreTape.Clear(); scoreEnemyAt.Clear(); scoreWaveEnd.Clear();
                        var old = GameplayWorld.Instance;
                        Time.timeScale = 1f;
                        var load = LoadLevel(level);
                        if (load != null) yield return load;
                        yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"level {level} to start");
                        if (Run.Done) yield break;
                        var stage = Object.FindAnyObjectByType<StageMusic>();
                        if (stage == null || !stage.ScoreActive)
                        {
                            Run.Event("check", $"score-floors level {level}: score not wired yet (clips pending), skipped");
                            continue;
                        }
                        yield return PlayLevel(level, 0f, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 40f);
                        if (Run.Done) yield break;
                        // A rewind: pitch down at once, the score back after.
                        float pressed = Time.realtimeSinceStartup;
                        int tapes = scoreTape.Count;
                        input.SetKey(UnityEngine.InputSystem.Key.R, true);
                        yield return Run.Until(() => GameClock.Instance != null && GameClock.Instance.IsRewinding, 5f, "the rewind to start");
                        yield return WaitReal(0.5f);
                        ScoreCheck($"level {level} rewind tape-stops", scoreTape.Count > tapes && scoreTape[tapes] - pressed <= 0.3f + 0.15f,
                            scoreTape.Count > tapes ? $"{scoreTape[tapes] - pressed:0.00} s after R" : "no musicTapeStop event");
                        input.SetKey(UnityEngine.InputSystem.Key.R, false);
                        yield return Run.Until(() => GameClock.Instance != null && !GameClock.Instance.IsRewinding, 5f, "the rewind to end");
                        yield return WaitReal(1f);
                        ScoreCheck($"level {level} score resumes", stage.ScoreActive && !stage.TapeStopping, "the tape-stop was lifted");
                        yield return PlayLevel(level, 0f, until: () => GameClock.Instance != null && GameClock.Instance.Time >= 110f || stage.CurrentScoreState == StageMusic.ScoreState.Finale);
                        if (Run.Done) yield break;
                        ScoreCheck($"level {level} layer changes", scoreStates.Count >= 2, $"{scoreStates.Count} changes");
                        // The finale swap must fire on every floor: its own `musicState finale` event, after the fight layer.
                        ScoreCheck($"level {level} finale swap fires", scoreStates.Exists(s => s.state == "finale") || stage.CurrentScoreState == StageMusic.ScoreState.Finale,
                            scoreStates.Exists(s => s.state == "finale") ? "musicState finale logged" : $"no finale cue by {GameClock.Instance.Time:0} s (state {stage.CurrentScoreState})");
                        int fight = scoreStates.FindIndex(s => s.state == "fight");
                        if (fight >= 0 && scoreEnemyAt.Count > 0)
                        {
                            float lead = scoreStates[fight].game - scoreEnemyAt[0];
                            ScoreCheck($"level {level} fight within 2 s of the first wave", lead <= 2.2f, $"{lead:0.0} s after the first enemy");
                        }
                        int drop = fight >= 0 && scoreWaveEnd.Count > 0 ? scoreStates.FindIndex(fight + 1, s => s.state == "explore") : -1;
                        if (drop >= 0)
                        {
                            float after = scoreStates[drop].game - scoreWaveEnd[0];
                            ScoreCheck($"level {level} fight drops within 4 s of the last enemy", after <= 4f, $"{after:0.0} s after the wave's last enemy");
                        }
                        input.ReleaseAll();
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= ScoreTelemetry;
                    input.ReleaseAll();
                }
                if (Run.Done) yield break;
                Run.Metric("scoreFloors.passed", scorePassed);
                Run.Metric("scoreFloors.failed", scoreFailed);
                Run.Progress(1f);
                End(scoreFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
