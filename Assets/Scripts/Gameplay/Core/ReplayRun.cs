using System.Globalization;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// One play of Endless Floor or the Daily Challenge (Docs/Story.md, "Again"). It keeps the score in the save as the hero climbs: the best
    /// Endless floor, and the Daily's line in the day's local table. Presentation and saves only: nothing here touches the simulation, so a rewind
    /// can't change the floor (the generator's own seeded stream does that); a rewind that takes a climb back leaves the best where it was.
    /// </summary>
    public sealed class ReplayRun
    {
        readonly GameplayWorld world;
        readonly ReplayConfig config;
        bool begun, announcedBest;

        public ReplayKind Kind { get; }
        /// <summary>The day the Daily is for (yyyyMMdd); the seed and the twist come from it.</summary>
        public string DateKey { get; }
        public ReplayModifier Modifier { get; }
        public ReplayScale Scale { get; }
        public long Attempt { get; }
        /// <summary>The floor the play started on.</summary>
        public int StartFloor { get; private set; }
        /// <summary>The best Endless floor before this play (0 = none).</summary>
        public int PreviousBest { get; }
        /// <summary>Floors climbed so far this play (the most, a rewind doesn't lower it).</summary>
        public int Floors { get; private set; }
        public int Score { get; private set; }
        public bool Cleared { get; private set; }
        /// <summary>The Daily's place in the day's table after the last save (0 = not in it).</summary>
        public int Place { get; private set; }

        public ReplayRun(GameplayWorld world, ReplayKind kind)
        {
            this.world = world;
            Kind = kind;
            config = GameDefinition.Instance != null && GameDefinition.Instance.replay != null ? GameDefinition.Instance.replay : new ReplayConfig();
            Attempt = System.DateTime.UtcNow.Ticks;
            PreviousBest = ReplayModes.EndlessBest;
            if (kind == ReplayKind.Daily)
            {
                DateKey = ReplayModes.TodayKey;
                Modifier = ReplayModes.ModifierFor(DateKey);
                Scale = ReplayModes.ScaleForModifier(Modifier);
            }
            else
            {
                DateKey = "";
                Scale = ReplayScale.None;
            }
        }

        /// <summary>The level starts: the announcer opens the show.</summary>
        public void Begin(float now)
        {
            if (begun)
                return;
            begun = true;
            StartFloor = world.Stage.HasFloors ? world.Stage.Floor : 0;
            var labels = config.labels;
            if (Kind == ReplayKind.Daily)
            {
                string pretty = ReplayModes.PrettyDate(DateKey);
                string twist = Modifier != null ? Modifier.name : "";
                world.Hud.ShowText(string.Format(CultureInfo.InvariantCulture, labels.dailyHeader, pretty, twist).ToUpperInvariant(), TextStyle.Banner,
                    new Color(1f, 0.82f, 0.48f), now + 3f);
                if (Modifier != null && !string.IsNullOrEmpty(Modifier.line))
                    world.Hud.ShowText(Modifier.line, TextStyle.Subtitle, new Color(0.82f, 0.85f, 0.9f), now + 5f);
                world.Telemetry.Record("replay", "daily", (int)(ReplayModes.DailySeed(DateKey) & 0x7FFFFFFF), (Modifier != null ? Modifier.id : "none") + " " + DateKey);
            }
            else
            {
                string best = PreviousBest > 0 ? string.Format(CultureInfo.InvariantCulture, labels.endlessBest, PreviousBest) : labels.endlessNoBest;
                world.Hud.ShowText(best.ToUpperInvariant(), TextStyle.Banner, new Color(1f, 0.82f, 0.48f), now + 3f);
                world.Telemetry.Record("replay", "endless", PreviousBest, "best before: " + PreviousBest + " floors");
            }
        }

        /// <summary>The generator took the hero up one floor (a push fought off). Saves the score.</summary>
        public void Climbed()
        {
            if (!world.Stage.HasFloors)
                return;
            int floors = Mathf.Max(0, StartFloor - world.Stage.Floor);
            if (floors <= Floors)
                return;
            Floors = floors;
            if (Kind == ReplayKind.Endless)
            {
                bool best = ReplayModes.RecordEndless(Floors);
                world.Telemetry.Record("replay", "endless-floor", Floors, best ? "new best" : "");
                if (best && PreviousBest > 0 && !announcedBest)
                {
                    announcedBest = true;
                    world.Hud.ShowText("NEW BEST! FLOOR " + world.Stage.Floor.ToString("N0", CultureInfo.InvariantCulture), TextStyle.Banner,
                        new Color(1f, 0.55f, 0.2f), world.Now + 2.5f);
                }
            }
            else
            {
                Save();
            }
        }

        /// <summary>The day's floor was finished (the generator's last push is over).</summary>
        public void Finished()
        {
            if (Kind != ReplayKind.Daily)
                return;
            Cleared = true;
            Save();
            world.Hud.ShowText($"{config.labels.cleared}! {Score.ToString("N0", CultureInfo.InvariantCulture)} PTS" + (Place > 0 ? $", PLACE {Place}" : ""),
                TextStyle.Banner, new Color(1f, 0.82f, 0.48f), world.Now + 4f);
        }

        void Save()
        {
            Score = ReplayModes.DailyScoreOf(Floors, Mathf.RoundToInt(world.GetCounter("kills")), Mathf.RoundToInt(world.GetCounter("grazes")), Cleared);
            Place = ReplayModes.RecordDaily(DateKey, Attempt, Score, Floors, Cleared);
            world.Telemetry.Record("replay", "daily-score", Score, $"floors {Floors}, place {Place}, {(Cleared ? "cleared" : "climbing")}");
        }
    }
}
