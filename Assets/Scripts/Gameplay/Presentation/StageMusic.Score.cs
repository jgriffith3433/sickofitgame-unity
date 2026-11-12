using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;
using Volume = SickOfIt.Core.Audio.MusicVolume;

namespace SickOfIt.Gameplay
{
    /// <summary>Carries a floor's ScoreConfig from LevelRuntime to the StageMusic that plays it (they share the music source's object).</summary>
    public sealed class ScoreCues : MonoBehaviour
    {
        public ScoreConfig Config { get; private set; }

        public static void Attach(GameObject host, ScoreConfig config)
        {
            if (!host.TryGetComponent(out ScoreCues cues))
                cues = host.AddComponent<ScoreCues>();
            cues.Config = config;
        }
    }

    /// <summary>
    /// The MusicDirector (Docs/Story.md "Music", "The adaptive score"): a floor with a ScoreConfig plays three loops of one tempo
    /// and key in step: explore (the level's music source), fight and finale. They are kept on the same playhead and only their
    /// gains move: the intensity (enemies alive, hostile shots on screen, the hero's hearts) picks fight over explore, the floor's
    /// finale signal picks finale over both. A change lands on a beat (into the fight) or a bar (out of it, into the finale) and
    /// cross-fades in 0.35 s. A rewind tape-stops it (pitch down over 0.3 s), a death cuts to a 1 s sting then explore.
    /// Presentation only; the one saved bit is whether the finale has begun (rewound with the world).
    /// </summary>
    public sealed partial class StageMusic
    {
        public enum ScoreState { Explore, Fight, Finale }

        const float ScoreFadeSeconds = 0.35f;
        const float TapeStopSeconds = 0.3f;
        const float TapeStopPitch = 0.4f;
        const float StingSeconds = 1f;
        const float ScoreDriftSeconds = 0.06f;

        ScoreConfig score;
        AudioSource fightSrc, finaleSrc, stingSrc;
        float exploreGain = 1f, fightGain, finaleGain;
        ScoreState scoreState;
        ScoreState scorePending;
        float scorePendingAt = -1f;
        float fightHoldUntil = -1f;
        float stingUntil = -1f;
        float scoreCrowdAt;
        int scoreEnemies, scoreBullets;
        float scoreIntensity;
        bool finaleFlag;           // saved
        bool scoreSyncNeeded;
        float tapeElapsed = -1f;
        bool telemetryHooked;

        public bool ScoreActive => score != null;
        public ScoreState CurrentScoreState => scoreState;
        /// <summary>0..1: how hard the floor is pressing the hero right now.</summary>
        public float ScoreIntensity => scoreIntensity;
        public bool TapeStopping => tapeElapsed >= 0f;

        void InitScore(AudioSource source)
        {
            if (!source.TryGetComponent(out ScoreCues cues) || cues.Config == null || !cues.Config.Complete)
                return;
            score = cues.Config;
            if (!telemetryHooked)
            {
                telemetryHooked = true;
                GameplayTelemetry.Recorded += OnScoreTelemetry;
            }
        }

        void ReleaseScore()
        {
            if (telemetryHooked)
                GameplayTelemetry.Recorded -= OnScoreTelemetry;
            telemetryHooked = false;
        }

        void OnScoreTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            if (score == null || log == null || log.World != world)
                return;
            if (e.Kind == "signal" && !string.IsNullOrEmpty(score.finaleSignal) && e.Subject == score.finaleSignal)
                finaleFlag = true;
            else if (e.Kind == "death" && world.Player != null && e.Value == world.Player.Serial && stingUntil < 0f)
                BeginSting();
        }

        AudioSource MakeScoreSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(main.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.loop = clip != null && name != "MusicSting";
            src.clip = clip;
            return src;
        }

        void BeginSting()
        {
            if (stingSrc == null && score.sting != null)
                stingSrc = MakeScoreSource("MusicSting", score.sting);
            if (stingSrc != null)
            {
                Volume.Set(stingSrc, score.volume);
                stingSrc.Play();
            }
            stingUntil = Time.unscaledTime + StingSeconds;
            scoreState = ScoreState.Explore;
            scorePendingAt = -1f;
            fightHoldUntil = -1f;
            world?.Telemetry?.Record("musicState", "sting", -1, "death");
        }

        void ScoreCount()
        {
            int was = scoreEnemies;
            scoreEnemies = scoreBullets = 0;
            var live = world != null ? world.Live : null;
            if (live == null)
                return;
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                if (e == null || !e.IsAlive || e.IsPlayer || e.Team != Team.Enemy)
                    continue;
                if (e.FindAbility<ProjectileAbility>() != null)
                    scoreBullets++;
                else
                    scoreEnemies++;
            }
            if ((was > 0) != (scoreEnemies > 0))
                world?.Telemetry?.Record("musicWave", scoreEnemies > 0 ? "start" : "end", scoreEnemies, "enemies");
            var hero = world.Player;
            float hurt = hero != null && hero.MaxHealth > 0 ? 1f - Mathf.Clamp01((float)hero.Health / hero.MaxHealth) : 0f;
            scoreIntensity = Mathf.Clamp01(scoreEnemies / 6f + scoreBullets / 24f + hurt * 0.25f);
        }

        ScoreState ScoreWanted()
        {
            if (finaleFlag)
                return ScoreState.Finale;
            bool fighting = scoreEnemies > 0 || scoreBullets >= 2;
            if (fighting)
                fightHoldUntil = Time.unscaledTime + Mathf.Max(0f, score.fightHoldSeconds);
            return Time.unscaledTime < fightHoldUntil ? ScoreState.Fight : ScoreState.Explore;
        }

        float SecondsToNext(float grid)
        {
            float t = main.time;
            float into = t - Mathf.Floor(t / grid) * grid;
            return Mathf.Max(0.02f, grid - into);
        }

        void StartScoreSources()
        {
            if (fightSrc == null)
                fightSrc = MakeScoreSource("MusicFight", score.fight);
            if (finaleSrc == null)
                finaleSrc = MakeScoreSource("MusicFinale", score.finale);
            bool resync = StartSilent(fightSrc);
            resync |= StartSilent(finaleSrc);
            if (resync)
                SyncScoreSources(true);
        }

        static bool StartSilent(AudioSource src)
        {
            if (src.isPlaying)
                return false;
            Volume.Set(src, 0f);
            src.pitch = 1f;
            src.Play();
            return true;
        }

        void SyncScoreSources(bool force)
        {
            float at = main.time;
            Resync(fightSrc, at, force);
            Resync(finaleSrc, at, force);
        }

        void Resync(AudioSource src, float at, bool force)
        {
            if (src == null || src.clip == null || !src.isPlaying)
                return;
            float len = src.clip.length;
            float diff = Mathf.Abs(src.time - at % len);
            diff = Mathf.Min(diff, len - diff);
            if (force || diff > ScoreDriftSeconds)
                src.time = Mathf.Min(at % len, len - 0.05f);
        }

        /// <summary>Called every frame by Update when not rewinding.</summary>
        void TickScore()
        {
            if (!main.isPlaying)
                return;
            if (!started)
            {
                started = true;
                Report(main.clip, "score");
            }
            if (tapeElapsed >= 0f)
            {
                // The rewind ended: the three loops come back on one playhead.
                tapeElapsed = -1f;
                scoreSyncNeeded = true;
            }
            StartScoreSources();
            if (scoreSyncNeeded)
            {
                scoreSyncNeeded = false;
                fightSrc.pitch = finaleSrc.pitch = 1f;
                SyncScoreSources(true);
                // The state the world is in now is re-read, so the right loop is the audible one at once.
                scoreCrowdAt = 0f;
                scoreState = ScoreWanted();
                scorePendingAt = -1f;
                exploreGain = scoreState == ScoreState.Explore ? 1f : 0f;
                fightGain = scoreState == ScoreState.Fight ? 1f : 0f;
                finaleGain = scoreState == ScoreState.Finale ? 1f : 0f;
            }
            else
            {
                SyncScoreSources(false);
            }

            if (Time.unscaledTime >= scoreCrowdAt)
            {
                scoreCrowdAt = Time.unscaledTime + CrowdEvery;
                ScoreCount();
            }

            bool inSting = stingUntil >= 0f && Time.unscaledTime < stingUntil;
            if (stingUntil >= 0f && !inSting)
                stingUntil = -1f;

            ScoreState want = inSting ? ScoreState.Explore : ScoreWanted();
            if (want == scoreState)
            {
                scorePendingAt = -1f;
            }
            else if (scorePendingAt < 0f || scorePending != want)
            {
                scorePending = want;
                // Into the fight on the next beat (the wave is on us), anywhere else on the next bar.
                float beat = 60f / score.bpm;
                float grid = want == ScoreState.Fight ? beat : beat * 4f;
                scorePendingAt = Time.unscaledTime + SecondsToNext(grid);
            }
            if (scorePendingAt >= 0f && Time.unscaledTime >= scorePendingAt)
            {
                scoreState = scorePending;
                scorePendingAt = -1f;
                world?.Telemetry?.Record("musicState", scoreState.ToString().ToLowerInvariant(), (int)scoreState, "intensity " + scoreIntensity.ToString("0.00"));
                world?.Telemetry?.Record("music", ClipOf(scoreState).name, 0, "score " + scoreState.ToString().ToLowerInvariant());
            }

            // Cross-fade the gains: the three always sum to about one.
            float step = Time.unscaledDeltaTime / ScoreFadeSeconds;
            float fightLevel = Mathf.Lerp(0.8f, 1f, scoreIntensity);
            exploreGain = Mathf.MoveTowards(exploreGain, inSting ? 0f : scoreState == ScoreState.Explore ? 1f : 0f, step);
            fightGain = Mathf.MoveTowards(fightGain, scoreState == ScoreState.Fight && !inSting ? fightLevel : 0f, step);
            finaleGain = Mathf.MoveTowards(finaleGain, scoreState == ScoreState.Finale && !inSting ? 1f : 0f, step);
            Volume.Set(main, score.volume * exploreGain);
            Volume.Set(fightSrc, score.volume * fightGain);
            Volume.Set(finaleSrc, score.volume * finaleGain);
        }

        AudioClip ClipOf(ScoreState s) => s == ScoreState.Fight ? score.fight : s == ScoreState.Finale ? score.finale : score.explore;

        void TapeStop(AudioSource src, float gain, float pitch, float k)
        {
            if (!src.isPlaying)
                return;
            src.pitch = pitch;
            Volume.Set(src, score.volume * gain * (1f - k));
            if (k >= 1f)
                src.Stop();
        }

        /// <summary>The rewind is on: the layers tape-stop (pitch down, fading) over 0.3 s and wait for the end.</summary>
        void TickScoreRewind()
        {
            if (fightSrc == null)
                return;
            if (tapeElapsed < 0f)
            {
                tapeElapsed = 0f;
                stingUntil = -1f;
                if (stingSrc != null)
                    stingSrc.Stop();
                world?.Telemetry?.Record("musicTapeStop", "score", 1, "pitch down over " + TapeStopSeconds.ToString("0.0") + " s");
            }
            tapeElapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tapeElapsed / TapeStopSeconds);
            float pitch = Mathf.Lerp(1f, TapeStopPitch, k);
            TapeStop(fightSrc, fightGain, pitch, k);
            TapeStop(finaleSrc, finaleGain, pitch, k);
            scoreSyncNeeded = true;
        }
    }
}
