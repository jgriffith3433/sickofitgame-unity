using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A log of what happened in one play of a level (spawns, damage, deaths, phases, rules, rewinds, the
    /// outcome) plus a summary. In the editor and in development builds it is written to Logs/Playtest/ when
    /// the level ends, so balance changes can be checked from data.
    /// </summary>
    public sealed class GameplayTelemetry
    {
        public struct Event
        {
            public float Time;
            public string Kind;
            public string Subject;
            public int Value;
            public string Detail;
        }

        /// <summary>Write logs to disk (editor and development builds by default).</summary>
        public static bool WriteFiles = Application.isEditor || Debug.isDebugBuild;
        /// <summary>Where logs go; the playtest runner overrides this.</summary>
        public static string Folder = "Logs/Playtest";
        /// <summary>The most recent finished summary (the playtest runner reads it).</summary>
        public static Summary LastSummary { get; private set; }
        /// <summary>Every event as it is recorded (the Hawkes playtest listens to report phases, deaths and errors).</summary>
        public static event Action<GameplayTelemetry, Event> Recorded;
        /// <summary>A level's log ended (any outcome), with its summary.</summary>
        public static event Action<GameplayTelemetry, Summary> Finished;

        public GameplayWorld World => world;

        readonly GameplayWorld world;
        readonly List<Event> events = new List<Event>();
        readonly DateTime started = DateTime.Now;
        bool finished;
        int rewinds;
        float rewoundSeconds;
        float rewindStartedAt;

        public IReadOnlyList<Event> Events => events;

        public GameplayTelemetry(GameplayWorld world)
        {
            this.world = world;
            var clock = GameClock.Instance;
            if (clock != null)
            {
                clock.OnRewindStarted += OnRewindStarted;
                clock.OnRewindStopped += OnRewindStopped;
            }
        }

        void OnRewindStarted()
        {
            rewinds++;
            rewindStartedAt = world.Now;
            Record("rewind", "start", 0);
        }

        void OnRewindStopped()
        {
            rewoundSeconds += Mathf.Max(0f, rewindStartedAt - world.Now);
            Record("rewind", "stop", 0);
        }

        public void Record(string kind, string subject, int value, string detail = "")
        {
            if (finished)
                return;
            var e = new Event { Time = world.Now, Kind = kind, Subject = subject ?? "", Value = value, Detail = detail ?? "" };
            events.Add(e);
            if (Recorded != null)
            {
                try { Recorded(this, e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        [Serializable]
        public sealed class Summary
        {
            public string level;
            public int levelNumber;
            public string outcome;
            public float duration;
            public int kills;
            public int spawned;
            public int playerHits;
            public int playerHealthEnd;
            public int playerMaxHealth;
            public int rewinds;
            public float rewoundSeconds;
            public int errors;
            public string lastPhase;
            /// <summary>Floor counter at the end (0 = the stage has no floors) and distance travelled (travel stages).</summary>
            public int floorEnd;
            public float distance;
            public Dictionary<string, int> killsByEntity = new Dictionary<string, int>();
            public Dictionary<string, int> damageTakenBySource = new Dictionary<string, int>();
            public List<string> phases = new List<string>();
        }

        public Summary Summarize(string outcome)
        {
            var s = new Summary
            {
                level = world.LevelName,
                levelNumber = world.LevelNumber,
                outcome = outcome,
                duration = world.Now,
                kills = (int)world.GetCounter("kills"),
                spawned = (int)world.GetCounter("spawned"),
                playerHits = (int)world.GetCounter("playerHits"),
                playerHealthEnd = world.Player != null ? world.Player.Health : 0,
                playerMaxHealth = world.Player != null ? world.Player.MaxHealth : 0,
                rewinds = rewinds,
                rewoundSeconds = rewoundSeconds,
                lastPhase = world.Director != null ? world.Director.PhaseName : "",
                floorEnd = world.Stage != null ? world.Stage.Floor : 0,
                distance = world.Stage != null ? world.Stage.Distance : 0f,
            };
            foreach (var e in events)
            {
                if (e.Kind == "error")
                    s.errors++;
                else if (e.Kind == "phase")
                    s.phases.Add(e.Subject);
                else if (e.Kind == "death" && e.Detail == "Damage")
                    s.killsByEntity[e.Subject] = (s.killsByEntity.TryGetValue(e.Subject, out int k) ? k : 0) + 1;
                else if (e.Kind == "damage" && e.Subject == "player")
                {
                    string source = string.IsNullOrEmpty(e.Detail) ? "(other)" : e.Detail;
                    s.damageTakenBySource[source] = (s.damageTakenBySource.TryGetValue(source, out int d) ? d : 0) + e.Value;
                }
            }
            return s;
        }

        /// <summary>Ends the log (complete, failed, died, quit) and writes it.</summary>
        public void Finish(string outcome)
        {
            if (finished)
                return;
            Record("end", outcome, 0);
            finished = true;
            LastSummary = Summarize(outcome);
            try { Finished?.Invoke(this, LastSummary); }
            catch (Exception e) { Debug.LogException(e); }
            if (!WriteFiles)
                return;

            try
            {
                Directory.CreateDirectory(Folder);
                string stamp = started.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string name = $"{(string.IsNullOrEmpty(world.LevelName) ? "level" : world.LevelName)}_{stamp}";
                var sb = new StringBuilder();
                foreach (var e in events)
                {
                    sb.Append("{\"t\":").Append(e.Time.ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(",\"kind\":").Append(JsonConvert.ToString(e.Kind))
                      .Append(",\"subject\":").Append(JsonConvert.ToString(e.Subject))
                      .Append(",\"value\":").Append(e.Value);
                    if (!string.IsNullOrEmpty(e.Detail))
                        sb.Append(",\"detail\":").Append(JsonConvert.ToString(e.Detail));
                    sb.Append("}\n");
                }
                File.WriteAllText(Path.Combine(Folder, name + ".jsonl"), sb.ToString());
                File.WriteAllText(Path.Combine(Folder, name + ".summary.json"), JsonConvert.SerializeObject(LastSummary, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameplayTelemetry] Could not write the playtest log: " + e.Message);
            }
        }
    }
}
