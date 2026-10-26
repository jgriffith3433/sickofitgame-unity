using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>The floor's rank, worked out from its summary (Docs/Story.md, "Style").</summary>
    public struct FloorRank
    {
        public string Letter;
        public int Points;
        public float Seconds, Par;
        public bool NoHit;
        public int Hits, Grazes, Kills, Rewinds;
    }

    /// <summary>
    /// Grazes, the style meter and the floor rank. A hostile shot that goes by the hero within <c>style.grazeDistance</c>
    /// without hitting him is a graze once it has passed its closest point; each fills a meter that drains by itself,
    /// pays at its tiers (diamonds, rewind juice) and sets off the announcer's and the crowd's lines on streaks.
    /// Rewind: the meter, the streak, the tier, the graze count (a world counter) and the shots being watched are saved
    /// state, so a rewind takes back the credit it undoes; payouts and lines are keyed on the graze count through
    /// <see cref="GameplayWorld.TryGrantOnce"/>, so replaying the same grazes never pays or speaks twice.
    /// </summary>
    public sealed class StyleSystem
    {
        public const string GrazeCounter = "grazes";
        public const string BestStreakCounter = "styleBestStreak";

        readonly GameplayWorld world;
        readonly StyleConfig cfg;
        readonly Dictionary<EntityDef, bool> isShot = new Dictionary<EntityDef, bool>();
        // Shot serial -> the smallest gap seen while it was near; negative = settled (credited, or it hit): never credited again.
        readonly Dictionary<int, float> watched = new Dictionary<int, float>();
        readonly HashSet<int> liveSerials = new HashSet<int>();
        readonly List<int> drop = new List<int>();

        // ─── Saved state ──────────────────────────────────────────────
        float meter;
        float lastGainAt = -99f;
        float lastGrazeAt = -99f;
        int streak;
        int tier;
        int hitsSeen;

        // Not saved: only reports and pacing.
        int reportedGrazes;
        float lastLineAt = -99f;

        public StyleConfig Config => cfg;
        public float Meter => meter;
        public float Fraction => cfg.meterMax > 0f ? Mathf.Clamp01(meter / cfg.meterMax) : 0f;
        public int Streak => streak;
        public int Tier => tier;
        public int Grazes => (int)world.GetCounter(GrazeCounter);
        public string TierName => tier > 0 && tier <= cfg.tiers.Count ? cfg.tiers[tier - 1].name : "";
        public Color TierColor => tier > 0 && tier <= cfg.tiers.Count ? cfg.tiers[tier - 1].color : Color.white;
        public bool Active => cfg.enabled && world.Player != null;

        public StyleSystem(GameplayWorld world)
        {
            this.world = world;
            var def = GameDefinition.Instance;
            cfg = def != null && def.style != null ? def.style : new StyleConfig();
        }

        bool IsShot(Entity e)
        {
            var def = e.Def;
            if (def == null)
                return false;
            if (!isShot.TryGetValue(def, out bool yes))
                isShot[def] = yes = e.FindAbility<ProjectileAbility>() != null;
            return yes;
        }

        public void Tick(float now, float dt)
        {
            var player = world.Player;
            if (!cfg.enabled || player == null || !player.IsAlive || !string.IsNullOrEmpty(world.SceneId))
                return;

            // A rewind took credit back: say so once (the saved graze count is lower than the last one reported).
            int grazes = Grazes;
            if (grazes < reportedGrazes)
                world.Telemetry.Record("grazeUndone", "", reportedGrazes - grazes, grazes.ToString());
            reportedGrazes = grazes;

            int hits = (int)world.GetCounter("playerHits");
            if (hits > hitsSeen)
            {
                hitsSeen = hits;
                if (meter > 0f || streak > 0)
                    world.Telemetry.Record("styleLost", "", Mathf.RoundToInt(meter), $"streak {streak}");
                meter = 0f;
                streak = 0;
                tier = 0;
                watched.Clear();
            }
            else
            {
                hitsSeen = hits;
            }

            Watch(now, player);
            Decay(now, dt);
        }

        void Watch(float now, Entity player)
        {
            liveSerials.Clear();
            foreach (var e in world.Live)
            {
                if (e == null || !e.IsAlive || e.Team != Team.Enemy || !IsShot(e))
                    continue;
                int serial = e.Serial;
                liveSerials.Add(serial);
                float gap = e.GapTo(player);
                if (!watched.TryGetValue(serial, out float least))
                {
                    if (gap <= 0f)
                        watched[serial] = -1f;
                    else if (gap <= cfg.grazeDistance)
                        watched[serial] = gap;
                    continue;
                }
                if (least < 0f)
                    continue;
                if (gap <= 0f)
                {
                    watched[serial] = -1f;
                    continue;
                }
                if (gap < least)
                {
                    watched[serial] = gap;
                }
                else if (gap > least + 0.03f)
                {
                    watched[serial] = -1f;
                    Graze(now, e, least);
                }
            }
            if (watched.Count == 0)
                return;
            // Shots that are gone (or settled and gone) are forgotten; a spent shot can't graze again.
            drop.Clear();
            foreach (var pair in watched)
            {
                if (!liveSerials.Contains(pair.Key))
                    drop.Add(pair.Key);
            }
            foreach (int serial in drop)
                watched.Remove(serial);
        }

        void Graze(float now, Entity shot, float gap)
        {
            world.AddCounter(GrazeCounter, 1f);
            int count = Grazes;
            streak = now - lastGrazeAt <= cfg.streakWindow ? streak + 1 : 1;
            lastGrazeAt = now;
            lastGainAt = now;
            meter = Mathf.Min(cfg.meterMax, meter + cfg.grazeGain + cfg.streakBonus * Mathf.Min(streak - 1, cfg.streakBonusCap));
            if (streak > world.GetCounter(BestStreakCounter))
                world.SetCounter(BestStreakCounter, streak);

            SickOfIt.Core.Fx.Fx.Spark(shot.Position, tierColorOrWhite(), 0.35f);
            world.PlayerController?.NoteGraze();
            Sfx.Play(cfg.grazeSound, 1f, 1f + 0.05f * Mathf.Min(streak, 10));
            world.Hud?.StylePunch();
            world.Telemetry.Record("graze", shot.DefId, streak, $"gap {gap:0.00} meter {meter:0} count {count}");

            if (streak == 5 || streak == 10 || streak == 20)
                Say("streak" + streak, count);

            int reached = 0;
            for (int i = 0; i < cfg.tiers.Count; i++)
            {
                if (meter >= cfg.tiers[i].at)
                    reached = i + 1;
            }
            for (int t = tier + 1; t <= reached; t++)
                Payout(t, count);
            tier = reached;
        }

        Color tierColorOrWhite() => tier > 0 ? TierColor : new Color(1f, 0.95f, 0.82f);

        void Decay(float now, float dt)
        {
            if (streak > 0 && now - lastGrazeAt > cfg.streakWindow)
                streak = 0;
            if (meter <= 0f || now - lastGainAt <= cfg.decayDelay)
                return;
            meter = Mathf.Max(0f, meter - cfg.decayPerSecond * dt);
            int reached = 0;
            for (int i = 0; i < cfg.tiers.Count; i++)
            {
                if (meter >= cfg.tiers[i].at)
                    reached = i + 1;
            }
            tier = Mathf.Min(tier, reached);
        }

        void Payout(int t, int count)
        {
            var def = cfg.tiers[t - 1];
            // Keyed on the graze count: a rewind that replays this graze arrives at the same count and is not paid again.
            if (!world.TryGrantOnce($"style:{t}:{count}"))
                return;
            if (def.diamonds > 0 && RogueliteManager.Instance != null)
                RogueliteManager.Instance.AddPoints(def.diamonds);
            if (def.juice > 0f && RewindBudget.Instance != null)
                RewindBudget.Instance.Add(def.juice);
            world.Hud?.StyleFloat($"{def.name}!" + (def.diamonds > 0 ? $"  +{def.diamonds}♦" : "") + (def.juice > 0f ? $"  +{def.juice:0.#}s" : ""), def.color);
            world.Telemetry.Record("stylePayout", def.name, def.diamonds, $"juice {def.juice:0.##} tier {t}");
            if (t == 3)
                Say("tier3", count);
        }

        // ─── Lines ────────────────────────────────────────────────────

        /// <summary>
        /// The line for a moment (subtitle first, then the voice when its clip exists). At most one every
        /// <c>lineGapSeconds</c>, once per graze count; quiet while rewinding or when a script holds the hero.
        /// </summary>
        public bool Say(string on, int key)
        {
            var clock = GameClock.Instance;
            if (clock != null && clock.IsRewinding)
                return false;
            if (world.RewindLocked || world.Now < world.InputLockedUntil || Time.unscaledTime - lastLineAt < cfg.lineGapSeconds)
                return false;
            var line = cfg.lines.Find(l => l.on == on);
            if (line == null || line.sounds.Count == 0 || !world.TryGrantOnce($"styleLine:{on}:{key}"))
                return false;
            lastLineAt = Time.unscaledTime;
            string text = Sfx.SpokenText(line.sounds[0]);
            float shown = Mathf.Max(2.5f, Sfx.LengthOf(line.sounds[0]) + 0.5f);
            world.Hud?.ShowText(text.ToUpperInvariant(), TextStyle.Subtitle, line.color, world.Now + shown);
            foreach (var id in line.sounds)
                Sfx.Play(id);
            world.Telemetry.Record("styleLine", on, key, text);
            return true;
        }

        /// <summary>The line for the results screen: what is said and its words (no subtitle: the screen shows them).</summary>
        public string SayOnResults(string on)
        {
            var line = cfg.lines.Find(l => l.on == on);
            if (line == null || line.sounds.Count == 0)
                return "";
            foreach (var id in line.sounds)
                Sfx.Play(id);
            string text = Sfx.SpokenText(line.sounds[0]);
            world.Telemetry.Record("styleLine", on, 0, text);
            return text.ToUpperInvariant();
        }

        // ─── Rank ─────────────────────────────────────────────────────

        public FloorRank Rank(GameplayTelemetry.Summary summary)
        {
            float par = Mathf.Max(5f, world.LevelLength * cfg.parFactor);
            var r = new FloorRank
            {
                Seconds = summary.duration, Par = par, Hits = summary.playerHits, Kills = summary.kills, Rewinds = summary.rewinds,
                Grazes = Grazes, NoHit = summary.playerHits == 0,
            };
            int points = 0;
            if (r.Seconds <= par)
                points += cfg.parPoints;
            else if (r.Seconds <= par * 1.25f)
                points += Mathf.RoundToInt(cfg.parPoints * 0.66f);
            else if (r.Seconds <= par * 1.6f)
                points += Mathf.RoundToInt(cfg.parPoints * 0.33f);
            points += r.Hits == 0 ? cfg.noHitPoints : r.Hits == 1 ? Mathf.RoundToInt(cfg.noHitPoints * 0.66f) : r.Hits == 2 ? Mathf.RoundToInt(cfg.noHitPoints * 0.33f) : 0;
            points += Mathf.RoundToInt(cfg.grazePoints * Mathf.Clamp01(cfg.grazeGoal > 0 ? (float)r.Grazes / cfg.grazeGoal : 1f));
            points += r.Rewinds == 0 ? cfg.rewindPoints : r.Rewinds <= cfg.rewindsForHalf ? cfg.rewindPoints / 2 : 0;
            r.Points = points;
            r.Letter = "C";
            int best = int.MinValue;
            foreach (var rank in cfg.ranks)
            {
                if (points >= rank.minPoints && rank.minPoints > best)
                {
                    best = rank.minPoints;
                    r.Letter = rank.letter;
                }
            }
            return r;
        }

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Float(meter);
            w.Float(lastGainAt);
            w.Float(lastGrazeAt);
            w.Int(streak);
            w.Int(tier);
            w.Int(hitsSeen);
            w.Int(watched.Count);
            foreach (var pair in watched)
            {
                w.Int(pair.Key);
                w.Float(pair.Value);
            }
        }

        public void LoadState(StateReader r)
        {
            meter = r.Float();
            lastGainAt = r.Float();
            lastGrazeAt = r.Float();
            streak = r.Int();
            tier = r.Int();
            hitsSeen = r.Int();
            watched.Clear();
            int count = r.Int();
            for (int i = 0; i < count; i++)
                watched[r.Int()] = r.Float();
        }
    }
}
