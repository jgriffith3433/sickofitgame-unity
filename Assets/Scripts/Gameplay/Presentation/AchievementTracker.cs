using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Counts what the hero does for the achievements (Docs/Story.md, "Achievements"): it reads the level's telemetry
    /// (<see cref="GameplayTelemetry.Recorded"/>) and turns it into <see cref="Achievements"/> counters. Rewind plays the same
    /// future again, so every counted thing is keyed (a kill by its serial, a hit by its moment, a graze by its count) and a
    /// replay never pays twice; the counters themselves live in the save, so a death, a run or a rewind never takes one back.
    /// Counters (ui/codex.json names them): kills, kill:&lt;display name&gt;, species, bosses, killsOneFloor, deaths, diedToImp, hurt, pits,
    /// noHit, noHitBoss, levels, climbed, rewinds, rewindSeconds, deathUndone, grazes, rankS, ranks, grudges, shortcuts,
    /// shortcutsDeclined, secretRooms, gauntlets, weapons, weapon:&lt;id&gt;, weaponHeld:&lt;id&gt;, dashes, wallJumps, grapples, yoobaMet and (from the
    /// save's own code) shopBuys, shopSpent, shopGoods, diamondsEarned. Presentation only: it reads sim state, never changes it.
    /// </summary>
    public static class AchievementTracker
    {
        /// <summary>The floor counter starts here (Floor 1,000); a climb of n floors is 1,001 minus the floor.</summary>
        const int BottomFloor = 1000;

        sealed class Play
        {
            public GameplayWorld World;
            public readonly HashSet<string> Once = new HashSet<string>();
            public bool BossKilled;
            public float RewindFrom = -1f;
            public bool RewindFromDeath;
            public string LastHitBy = "";
        }

        static Play current;

        /// <summary>The counters this game feeds (the tracker above, the shop and the diamond purse in RogueliteManager).</summary>
        public static readonly string[] KnownStats =
        {
            "kills", "species", "bosses", "killsOneFloor", "deaths", "diedToImp", "hurt", "pits", "noHit", "noHitBoss", "levels", "climbed",
            "rewinds", "rewindSeconds", "deathUndone", "grazes", "rankS", "ranks", "grudges", "shortcuts", "shortcutsDeclined", "secretRooms",
            "gauntlets", "weapons", "dashes", "wallJumps", "grapples", "yoobaMet", "shopBuys", "shopSpent", "shopGoods", "diamondsEarned",
        };

        /// <summary>Counters named after a thing: kill:&lt;display name&gt;, weapon:&lt;id&gt;, weaponHeld:&lt;id&gt;.</summary>
        public static readonly string[] KnownStatPrefixes = { "kill:", "weapon:", "weaponHeld:" };

        public static bool IsKnownStat(string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return false;
            if (System.Array.IndexOf(KnownStats, stat) >= 0)
                return true;
            foreach (string prefix in KnownStatPrefixes)
                if (stat.StartsWith(prefix, System.StringComparison.Ordinal) && stat.Length > prefix.Length)
                    return true;
            return false;
        }

        /// <summary>Everything counted since the game started (the playtest reads it).</summary>
        public static int Counted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            current = null;
            Counted = 0;
            GameplayTelemetry.Recorded -= OnRecorded;
            GameplayTelemetry.Recorded += OnRecorded;
        }

        static Play For(GameplayWorld world)
        {
            if (current == null || current.World != world)
                current = new Play { World = world };
            return current;
        }

        static void Bump(string stat, int amount = 1)
        {
            Counted++;
            Achievements.Bump(stat, amount);
        }

        static void OnRecorded(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            var world = log != null ? log.World : null;
            if (world == null)
                return;
            // Scenes (cutscenes, test benches) are not play: nothing counts there.
            if (!string.IsNullOrEmpty(world.SceneId))
                return;
            if (e.Kind == "levelStart")
            {
                current = new Play { World = world };
                if (YoobaProgress.Met)
                    Achievements.SetMax("yoobaMet", 1);
                return;
            }
            var play = For(world);
            switch (e.Kind)
            {
                case "death":
                    Died(play, world, e);
                    break;
                case "damage":
                    if (IsPlayer(world, e.Subject))
                        play.LastHitBy = e.Detail ?? "";
                    break;
                case "playerHit":
                    if (play.Once.Add("hit:" + Mathf.RoundToInt(e.Time * 20f)))
                        Bump("hurt");
                    break;
                case "pit":
                    if (play.Once.Add("pit:" + Mathf.RoundToInt(e.Time * 20f)))
                        Bump("pits");
                    break;
                case "floor":
                    if (e.Value > 0)
                        Achievements.SetMax("climbed", BottomFloor + 1 - e.Value);
                    break;
                case "complete":
                    Completed(play, world);
                    break;
                case "rewind":
                    Rewind(play, world, e);
                    break;
                case "graze":
                    if (play.Once.Add("graze:" + e.Detail))
                        Bump("grazes");
                    break;
                case "rank":
                    if (play.Once.Add("rank:" + world.LevelNumber))
                    {
                        Bump("ranks");
                        if (e.Subject == "S")
                            Bump("rankS");
                    }
                    break;
                case "upgradePick":
                    if (play.Once.Add("pick:" + e.Detail + ":" + e.Subject))
                        Bump("grudges");
                    break;
                case "shortcut":
                    if (play.Once.Add("shortcut:" + e.Subject))
                        Bump("shortcuts");
                    break;
                case "shortcut-declined":
                    if (play.Once.Add("shortcut:" + e.Subject))
                        Bump("shortcutsDeclined");
                    break;
                case "weaponUnlocked":
                    if (e.Detail == "new")
                    {
                        Achievements.AddUnique("weapons", e.Subject);
                        Achievements.SetMax("weapon:" + e.Subject, 1);
                    }
                    break;
                case "weaponGiven":
                    Achievements.SetMax("weaponHeld:" + e.Subject, 1);
                    break;
                case "move":
                    if (e.Subject == "dash" && play.Once.Add("dash:" + Mathf.RoundToInt(e.Time * 20f)))
                        Bump("dashes");
                    else if (e.Subject == "wallJump" && play.Once.Add("wj:" + Mathf.RoundToInt(e.Time * 20f)))
                        Bump("wallJumps");
                    break;
                case "grapple":
                    if ((e.Subject == "attach" || e.Subject == "attachPull") && play.Once.Add("grapple:" + Mathf.RoundToInt(e.Time * 20f)))
                        Bump("grapples");
                    break;
            }
        }

        static bool IsPlayer(GameplayWorld world, string defId) => world.Player != null && world.Player.DefId == defId;

        static void Died(Play play, GameplayWorld world, GameplayTelemetry.Event e)
        {
            if (IsPlayer(world, e.Subject))
            {
                if (play.Once.Add("hero-died:" + Mathf.RoundToInt(e.Time * 20f)))
                {
                    Bump("deaths");
                    var killer = Def(play.LastHitBy);
                    if (killer != null && killer.displayName == "Imp")
                        Bump("diedToImp");
                }
                return;
            }
            if (e.Detail != "Damage" || !play.Once.Add("kill:" + e.Value))
                return;
            var def = Def(e.Subject);
            // Only creatures with a name count (shots, hazards and props die too).
            if (def == null || def.team != Team.Enemy || string.IsNullOrEmpty(def.displayName))
                return;
            Bump("kills");
            Bump("kill:" + def.displayName);
            Achievements.AddUnique("species", def.displayName);
            Achievements.SetMax("killsOneFloor", (int)world.GetCounter("kills"));
            if (def.tags != null && def.tags.Contains("boss"))
            {
                play.BossKilled = true;
                Achievements.AddUnique("bosses", def.displayName);
            }
        }

        static void Completed(Play play, GameplayWorld world)
        {
            if (!play.Once.Add("complete"))
                return;
            string name = world.LevelName ?? "";
            if (name.StartsWith("Secret"))
                Achievements.AddUnique("secretRooms", name);
            else if (name.StartsWith("Gauntlet"))
                Achievements.AddUnique("gauntlets", name);
            // The endless and daily floors are not a floor cleared.
            else if (name == "EndlessFloor" || name == "DailyFloor")
                return;
            Bump("levels");
            if ((int)world.GetCounter("playerHits") == 0)
            {
                Bump("noHit");
                if (play.BossKilled)
                    Bump("noHitBoss");
            }
        }

        static void Rewind(Play play, GameplayWorld world, GameplayTelemetry.Event e)
        {
            if (e.Subject == "start")
            {
                play.RewindFrom = world.Now;
                play.RewindFromDeath = world.Player != null && world.Player.Health <= 0;
                return;
            }
            if (play.RewindFrom < 0f)
                return;
            float back = Mathf.Max(0f, play.RewindFrom - world.Now);
            play.RewindFrom = -1f;
            Bump("rewinds");
            Bump("rewindSeconds", Mathf.RoundToInt(back));
            // Undoing a death: he was dead when the rewind started and is standing when it stops.
            if (play.RewindFromDeath && world.Player != null && world.Player.Health > 0)
                Bump("deathUndone");
        }

        static EntityDef Def(string id)
        {
            var content = GameContent.Current;
            if (content == null || string.IsNullOrEmpty(id))
                return null;
            content.Entities.TryGetValue(id, out var def);
            return def;
        }
    }
}
