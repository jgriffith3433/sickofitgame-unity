using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Which main-menu buttons the player has earned (Docs/Story.md "The menu only shows what you have met"). A brand-new player sees
    /// Play, Settings and Rage Quit; the rest appear as the game shows them: Visit Yooba after the first diamonds, Pick a Level, Rewatch
    /// Stuff and the Codex after Floor 1000 is cleared, the Endless / Daily / New Game+ cards after the first Waystone. A reveal is saved
    /// for good ("Menu_Unlocked", not cleared by New Game, so a returning player never loses a button) and each newly shown button wears a
    /// NEW sticker until it is pressed ("Menu_Seen"). A save that already qualifies when this first runs gets everything with no stickers.
    /// </summary>
    public static class MenuReveal
    {
        public const string Shop = "shop";
        public const string LevelSelect = "levelSelect";
        public const string Videos = "videos";
        public const string Codex = "codex";
        public const string Endless = "endless";
        public const string Daily = "daily";
        public const string NewGamePlus = "newGamePlus";

        const string UnlockedKey = "Menu_Unlocked";
        const string SeenKey = "Menu_Seen";
        const string InitKey = "Menu_Init";

        /// <summary>The level whose clear opens the level pickers: Floor 1000, The Landing.</summary>
        public const int FloorThousandLevel = 2;

        /// <summary>The ids in reveal order.</summary>
        public static readonly string[] All = { Shop, LevelSelect, Videos, Codex, Endless, Daily, NewGamePlus };

        /// <summary>
        /// Nothing played yet: no first level started, no level cleared. Play (a New Game) is the one big button; after it the button is
        /// Continue, with New Game beside it.
        /// </summary>
        public static bool IsFirstRun
        {
            get
            {
                if (PlayerPrefs.HasKey("Level_1_Visited"))
                    return false;
                var progress = GameProgressManager.Instance;
                return progress == null || progress.GetHighestLevelReached() <= 1;
            }
        }

        /// <summary>Whether the save's progress now earns this button (the save's own facts, not the saved reveal).</summary>
        public static bool Qualifies(string id)
        {
            var progress = GameProgressManager.Instance;
            switch (id)
            {
                case Shop:
                    return RogueliteManager.Instance != null
                           && (RogueliteManager.Instance.GetPoints() > 0 || Achievements.Get("diamondsEarned") > 0 || YoobaProgress.Met
                               || OwnsAGood());
                case LevelSelect:
                case Videos:
                case Codex:
                    return progress != null && (progress.IsLevelCompleted(FloorThousandLevel) || progress.GetHighestLevelReached() > FloorThousandLevel);
                case Endless:
                case Daily:
                    return WaystoneProgress.Highest > 0;
                case NewGamePlus:
                    return WaystoneProgress.Highest > 0 || ReplayModes.NewGamePlusOpen;
                default:
                    return true;
            }
        }

        static bool OwnsAGood()
        {
            foreach (var upgrade in RogueliteManager.ShopCatalog)
                if (upgrade != null && RogueliteManager.Instance.GetUpgradeLevel(upgrade.id) > 0)
                    return true;
            return false;
        }

        /// <summary>Shown now: earned before (kept for good) or earned by the save as it stands. Saves what it finds.</summary>
        public static bool Unlocked(string id)
        {
            var kept = Set(UnlockedKey);
            if (kept.Contains(id))
                return true;
            if (!Qualifies(id))
                return false;
            kept.Add(id);
            Write(UnlockedKey, kept);
            return true;
        }

        /// <summary>Shown, and not pressed yet since it appeared: it wears the NEW sticker.</summary>
        public static bool IsNew(string id) => Unlocked(id) && !Set(SeenKey).Contains(id);

        /// <summary>The player pressed it (or saw it with a save that had it before): no more sticker.</summary>
        public static void Acknowledge(string id)
        {
            var seen = Set(SeenKey);
            if (seen.Add(id))
                Write(SeenKey, seen);
        }

        /// <summary>
        /// Call when the main menu opens. The first time ever, whatever the save already qualifies for is unlocked and seen (an existing
        /// player gets every button back, with no stickers); a brand-new save marks nothing, so what it earns later arrives with NEW.
        /// </summary>
        public static void BeginMenu()
        {
            if (PlayerPrefs.GetInt(InitKey, 0) == 1)
                return;
            foreach (var id in All)
                if (Unlocked(id))
                    Acknowledge(id);
            PlayerPrefs.SetInt(InitKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>The ids shown now (the playtest reads it).</summary>
        public static List<string> UnlockedIds()
        {
            var list = new List<string>();
            foreach (var id in All)
                if (Unlocked(id))
                    list.Add(id);
            return list;
        }

        /// <summary>Playtest setup: every button shown and seen (what a returning player's menu looks like).</summary>
        public static void UnlockAll()
        {
            var kept = Set(UnlockedKey);
            var seen = Set(SeenKey);
            foreach (var id in All)
            {
                kept.Add(id);
                seen.Add(id);
            }
            Write(UnlockedKey, kept);
            Write(SeenKey, seen);
            PlayerPrefs.SetInt(InitKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Playtest setup: back to a brand-new player's menu.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.DeleteKey(InitKey);
        }

        static HashSet<string> Set(string key) =>
            new HashSet<string>(PlayerPrefs.GetString(key, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        static void Write(string key, HashSet<string> set)
        {
            PlayerPrefs.SetString(key, string.Join(",", set));
            PlayerPrefs.Save();
        }
    }
}
