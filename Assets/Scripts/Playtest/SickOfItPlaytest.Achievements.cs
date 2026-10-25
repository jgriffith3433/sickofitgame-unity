using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The achievements scenario (Docs/Story.md, "Achievements"): the list in ui/codex.json is sound (40+, unique, every stat
        /// counted by the game, no office words); New Game empties it; Level 1 is played by the bot until the first unlock, whose toast
        /// must be on screen with the achievement's name; the pause menu's Codex shows the Achievements tab with the unlocked one first,
        /// its title, and the others with their progress; a click on the unlocked row wears its title; the save persists across a reload
        /// and the main menu, and New Game empties it again.
        /// </summary>
        public const string AchievementsId = "achievements";
        const int AchievementsLevel = 1;
        const float AchievementsSeconds = 300f;

        sealed partial class Session
        {
            int achPassed, achFailed;

            void AchCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    achPassed++;
                    Run.Event("check", $"achievements {beat}: ok ({detail})");
                }
                else
                {
                    achFailed++;
                    Run.Event("check", $"achievements {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"achievements {beat}: {detail}");
                }
            }

            void AchList(CodexConfig config)
            {
                var list = config.achievements;
                AchCheck("at least 40 achievements", list.Count >= 40, $"{list.Count} in ui/codex.json");
                var ids = new HashSet<string>();
                var names = new HashSet<string>();
                var bad = new List<string>();
                var banned = new[] { "meeting", "inbox", "monday", "coffee", "office", "deadline", "e-mail", "email", "paycheck", "overtime", "manager" };
                foreach (var a in list)
                {
                    if (string.IsNullOrEmpty(a.id) || !ids.Add(a.id)) bad.Add("id " + a.id);
                    if (string.IsNullOrEmpty(a.name) || !names.Add(a.name)) bad.Add("name " + a.name);
                    if (string.IsNullOrEmpty(a.description) || string.IsNullOrEmpty(a.title)) bad.Add("words " + a.id);
                    if (a.target < 1) bad.Add("target " + a.id);
                    if (!AchievementTracker.IsKnownStat(a.stat)) bad.Add("stat " + a.id + ":" + a.stat);
                    if (a.secret && string.IsNullOrEmpty(a.hint)) bad.Add("hint " + a.id);
                    string text = (a.name + " " + a.description + " " + a.title + " " + a.hint).ToLowerInvariant();
                    foreach (string word in banned)
                        if (text.Contains(word)) bad.Add("word '" + word + "' in " + a.id);
                }
                AchCheck("every achievement is sound", bad.Count == 0, bad.Count == 0 ? "ids and names unique, words set, stats known, tone clean" : string.Join("; ", bad));
                var stats = new HashSet<string>();
                foreach (var a in list) stats.Add(a.stat);
                Run.Metric("achievements.total", list.Count);
                Run.Metric("achievements.stats", stats.Count);
            }

            IEnumerator AchOpenTab(CodexConfig config)
            {
                yield return ClickUntil(config.tabAchievements, () => CodexPanel.Current != null && CodexPanel.Current.Tab == CodexTab.Achievements, 10f);
                yield return WaitReal(0.5f);
            }

            public IEnumerator AchievementsRun()
            {
                var gd = GameDefinition.Instance;
                var config = gd.codex;
                var texts = gd.menuTheme.texts;
                if (config == null || !config.enabled || config.achievements == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "achievements: the Codex is off or lists no achievements");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                Run.Event("achievements", $"screen {Screen.width}x{Screen.height}");
                AchList(config);

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // 1. New Game forgets everything (setup shortcut for the starting state).
                Achievements.Bump("kills", 3);
                RogueliteManager.Instance.ResetAllRogueliteData();
                Achievements.Reload();
                AchCheck("New Game empties the list", Achievements.UnlockedCount == 0 && Achievements.Get("kills") == 0 && string.IsNullOrEmpty(Achievements.WornId),
                    $"{Achievements.UnlockedCount} unlocked, kills {Achievements.Get("kills")}");

                // 2. Level 1 played by the bot until something unlocks (the first kill).
                Run.Event("shortcut", $"load level {AchievementsLevel} directly");
                var old = GameplayWorld.Instance;
                var load = LoadLevel(AchievementsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(AchievementsLevel), 60f, "level 1 to start");
                if (Run.Done) yield break;
                int shownBefore = AchievementToast.Shown;
                yield return PlayLevel(AchievementsLevel, 0f, until: () => AchievementToast.Shown > shownBefore && AchievementToast.Visible);
                if (Run.Done) yield break;
                // Let the card finish dropping in, then look at it.
                yield return WaitReal(0.7f);
                bool visible = AchievementToast.Visible;
                var card = AchievementToast.ScreenRect;
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                var def = Achievements.Find(AchievementToast.LastId);
                AchCheck("a toast shows on unlock", AchievementToast.Shown > shownBefore && visible && def != null,
                    $"{AchievementToast.Shown - shownBefore} shown: {AchievementToast.VisibleText}");
                AchCheck("the toast is on screen", visible && card.width > 0f && screen.Contains(card.min) && screen.Contains(card.max - Vector2.one),
                    $"card {card.width:0}x{card.height:0} at {card.x:0},{card.y:0} on a {Screen.width}x{Screen.height} screen");
                AchCheck("the toast names the achievement", def != null && AchievementToast.VisibleText.Contains(def.name) && AchievementToast.VisibleText.Contains(config.toastHeading),
                    AchievementToast.VisibleText);
                AchCheck("the unlock is saved", def != null && Achievements.IsUnlocked(def.id), def != null ? def.id : "none");
                Run.Event("achievements", $"unlocked {Achievements.UnlockedCount}: {AchievementToast.LastName}; counted {AchievementTracker.Counted}");

                // Play on a little so more counters move (kills, rewinds, grazes ...), then stop.
                yield return PlayLevel(AchievementsLevel, 0f, until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= 20f);
                if (Run.Done) yield break;
                StopBot();
                Run.Metric("achievements.unlockedInPlay", Achievements.UnlockedCount);
                Run.Metric("achievements.kills", Achievements.Get("kills"));
                Run.Metric("achievements.toasts", AchievementToast.Shown);

                // 3. The pause menu's Codex, Achievements tab.
                yield return ClickUntil("II", () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(config.buttonLabel, () => CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                yield return AchOpenTab(config);
                if (Run.Done) yield break;
                var view = CodexPanel.Current;
                AchCheck("the Achievements tab opens", view != null && view.Tab == CodexTab.Achievements, view != null ? view.Tab.ToString() : "no Codex");
                if (view == null) { End(PlaytestOutcome.Error); yield break; }
                CodexDescribe("achievements");
                CodexBounds("achievements p1");
                AchCheck("the list counts every achievement", view.Total == config.achievements.Count, $"{view.Total} rows for {config.achievements.Count} achievements");
                AchCheck("the list counts the unlocked", view.Met == Achievements.UnlockedCount && view.Met >= 1, $"{view.Met} unlocked in the list, {Achievements.UnlockedCount} saved");
                var first = view.Rows.Count > 0 ? view.Rows[0] : null;
                string firstText = first != null ? CodexRowText(first) : "";
                AchCheck("an unlocked achievement is listed first with its title", first != null && def != null && Achievements.IsUnlocked(first.Entry.Id)
                    && firstText.Contains(first.Entry.Name) && firstText.Contains(string.Format(System.Globalization.CultureInfo.InvariantCulture, config.titleLineFormat, first.Entry.Title)), firstText);
                bool progressShown = false;
                string progressText = "";
                foreach (var row in view.Rows)
                {
                    if (row.Entry.IsAchievement && !Achievements.IsUnlocked(row.Entry.Id) && row.Entry.Met)
                    {
                        string expect = string.Format(System.Globalization.CultureInfo.InvariantCulture, config.progressFormat, row.Entry.Progress, row.Entry.Target);
                        progressText = CodexRowText(row);
                        progressShown = progressText.Contains(expect);
                        break;
                    }
                }
                AchCheck("a locked one shows its progress", progressShown, progressText);
                yield return Audit("Codex Achievements p1", 0, 1, 0.3f, progress: false);

                // Page through the whole list.
                int pages = view.Pages;
                for (int page = 1; page < pages && !Run.Done; page++)
                {
                    int target = page;
                    yield return ClickUntil(config.next, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                    yield return WaitReal(0.2f);
                    CodexBounds($"achievements p{page + 1}");
                }
                for (int page = pages - 1; page > 0 && !Run.Done; page--)
                {
                    int target = page - 1;
                    yield return ClickUntil(config.previous, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                }
                if (Run.Done) yield break;

                // 4. A click on the unlocked row wears its title.
                // The page was just rebuilt by the '<' click: let the layout place the rows before aiming at one.
                yield return WaitReal(0.6f);
                first = CodexPanel.Current.Rows.Count > 0 ? CodexPanel.Current.Rows[0] : null;
                if (first != null)
                {
                    Vector2 rowPoint = ScreenPoint(first.Rect);
                    Run.Event("achievements", $"click row {first.Entry.Id} at {rowPoint.x:0},{rowPoint.y:0} on page {CodexPanel.Current.Page + 1}");
                    input.MoveMouse(rowPoint);
                    yield return null;
                    yield return null;
                    input.SetMouseButton(true);
                    yield return null;
                    yield return null;
                    input.SetMouseButton(false);
                    yield return WaitReal(0.5f);
                }
                string wornTitle = Achievements.WornTitle;
                AchCheck("a click wears the title", first != null && wornTitle == first.Entry.Title && !string.IsNullOrEmpty(wornTitle), $"wearing \"{wornTitle}\"");
                string header = CodexPanel.Current != null && CodexPanel.Current.Wearing != null ? CodexPanel.Current.Wearing.text : "";
                AchCheck("the header shows it", header.Contains(wornTitle) && !string.IsNullOrEmpty(wornTitle), header);

                yield return ClickUntil(texts.done, () => !CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(texts.resume, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;

                // 5. The save persists: reload it from PlayerPrefs, leave for the main menu, open the Codex there.
                int unlockedBefore = Achievements.UnlockedCount;
                int killsBefore = Achievements.Get("kills");
                string wornBefore = Achievements.WornId;
                Achievements.Flush();
                Achievements.Reload();
                AchCheck("the save persists", Achievements.UnlockedCount == unlockedBefore && Achievements.Get("kills") == killsBefore && Achievements.WornId == wornBefore,
                    $"{Achievements.UnlockedCount}/{unlockedBefore} unlocked, kills {Achievements.Get("kills")}/{killsBefore}, title {Achievements.WornId}");
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(config.buttonLabel, () => CodexPanel.IsOpen, 15f);
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                yield return AchOpenTab(config);
                view = CodexPanel.Current;
                AchCheck("the main menu's Codex lists them still", view != null && view.Tab == CodexTab.Achievements && view.Met == unlockedBefore, view != null ? $"{view.Met} unlocked" : "no Codex");
                yield return Audit("Codex Achievements (main menu)", 0, 1, 0.3f, progress: false);
                yield return ClickUntil(texts.done, () => !CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;

                // 6. New Game empties it.
                RogueliteManager.Instance.ResetAllRogueliteData();
                Achievements.Reload();
                AchCheck("New Game resets the achievements", Achievements.UnlockedCount == 0 && Achievements.Get("kills") == 0 && string.IsNullOrEmpty(Achievements.WornId),
                    $"{Achievements.UnlockedCount} unlocked, kills {Achievements.Get("kills")}, title \"{Achievements.WornId}\"");

                Run.Metric("achievements.passed", achPassed);
                Run.Metric("achievements.failed", achFailed);
                Run.Progress(1f);
                End(achFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
