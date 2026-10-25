using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;
using SickOfIt.Core.UI.Menus;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The codex scenario (Docs/Story.md, "The Codex"): the Codex screen from the pause menu and the main menu, met entries with
        /// their notes and tallies, unmet ones as silhouettes without spoilers, and the layout at the device's size.
        /// </summary>
        public const string CodexId = "codex";
        const int CodexLevel = 1;
        const float CodexSeconds = 360f;

        sealed partial class Session
        {
            int codexPassed, codexFailed;

            void CodexCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    codexPassed++;
                    Run.Event("check", $"codex {beat}: ok ({detail})");
                }
                else
                {
                    codexFailed++;
                    Run.Event("check", $"codex {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"codex {beat}: {detail}");
                }
            }

            static string CodexRowText(CodexRowView row) => string.Join(" / ", row.Texts);

            /// <summary>What the open page shows, for the timeline.</summary>
            void CodexDescribe(string where)
            {
                var view = CodexPanel.Current;
                if (view == null)
                    return;
                var sb = new StringBuilder();
                foreach (var row in view.Rows)
                    sb.Append(row.Silhouette ? "[silhouette] " : "[met] ").Append(CodexRowText(row)).Append(" | ");
                Run.Event("codex", $"{where}: tab {view.Tab}, page {view.Page + 1}/{view.Pages}, {view.Met}/{view.Total} met, {Screen.width}x{Screen.height}: {sb}");
            }

            /// <summary>Everything on the page is inside the panel, and the panel inside the screen.</summary>
            void CodexBounds(string where)
            {
                var view = CodexPanel.Current;
                if (view == null)
                    return;
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                var panel = CodexWorldRect(view.Panel);
                bool panelOn = screen.Contains(panel.min) && screen.Contains(panel.max);
                var outside = new List<string>();
                foreach (var row in view.Rows)
                {
                    var r = CodexWorldRect(row.Rect);
                    if (r.xMin < panel.xMin - 1f || r.xMax > panel.xMax + 1f || r.yMin < panel.yMin - 1f || r.yMax > panel.yMax + 1f)
                        outside.Add(row.Entry.Name + " (row)");
                    var rowRect = CodexWorldRect(row.Rect);
                    for (int i = 0; i < row.TextRects.Count; i++)
                    {
                        var t = CodexWorldRect(row.TextRects[i]);
                        if (t.xMin < rowRect.xMin - 1f || t.xMax > rowRect.xMax + 1f || t.yMin < rowRect.yMin - 1f || t.yMax > rowRect.yMax + 1f)
                            outside.Add(row.Entry.Name + " (text " + i + ")");
                    }
                }
                CodexCheck($"layout {where}", panelOn && outside.Count == 0,
                    $"panel {panel.width:0}x{panel.height:0} at {panel.x:0},{panel.y:0} on a {Screen.width}x{Screen.height} screen; " +
                    (outside.Count == 0 ? "every row and text inside" : "outside: " + string.Join(", ", outside)));
            }

            static Rect CodexWorldRect(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var canvas = rect.GetComponentInParent<Canvas>();
                canvas = canvas != null ? canvas.rootCanvas : null;
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }

            /// <summary>An unmet row says nothing but "???" and the unmet line, and its picture is solid black.</summary>
            void CodexSpoilers(string where, CodexConfig config, HashSet<string> secrets)
            {
                var view = CodexPanel.Current;
                if (view == null)
                    return;
                int silhouettes = 0;
                var leaks = new List<string>();
                foreach (var row in view.Rows)
                {
                    if (!row.Silhouette)
                        continue;
                    silhouettes++;
                    string text = CodexRowText(row);
                    bool black = row.ArtColor.r < 0.05f && row.ArtColor.g < 0.05f && row.ArtColor.b < 0.05f && row.ArtColor.a > 0.9f;
                    if (!black)
                        leaks.Add("picture not black: " + row.ArtColor);
                    foreach (string secret in secrets)
                        if (!string.IsNullOrEmpty(secret) && text.IndexOf(secret, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            leaks.Add("shows \"" + secret + "\"");
                    if (text != config.unknownName + " / " + config.unknownLine)
                        leaks.Add("reads \"" + text + "\"");
                }
                CodexCheck($"no spoilers {where}", leaks.Count == 0, $"{silhouettes} silhouettes on the page" + (leaks.Count == 0 ? "" : "; " + string.Join("; ", leaks)));
            }

            /// <summary>Every name, note and weakness of the unmet entries on a tab: none of it may be on screen.</summary>
            HashSet<string> CodexSecrets(CodexTab tab, GameContent content, CodexConfig config)
            {
                var secrets = new HashSet<string>();
                foreach (var entry in CodexBook.Entries(tab, content, config))
                {
                    if (entry.Met)
                        continue;
                    secrets.Add(entry.Name);
                    if (!string.IsNullOrEmpty(entry.Note)) secrets.Add(entry.Note);
                    if (!string.IsNullOrEmpty(entry.Line)) secrets.Add(entry.Line);
                }
                return secrets;
            }

            IEnumerator CodexOpenTab(CodexTab tab, string label)
            {
                yield return ClickUntil(label, () => CodexPanel.Current != null && CodexPanel.Current.Tab == tab, 10f);
                yield return WaitReal(0.4f);
            }

            /// <summary>Pages through a whole tab: spoilers, bounds and the layout audit on each page.</summary>
            IEnumerator CodexWalk(CodexTab tab, string label, GameContent content, CodexConfig config)
            {
                yield return CodexOpenTab(tab, label);
                if (Run.Done) yield break;
                var view = CodexPanel.Current;
                CodexCheck($"{tab} tab opens", view != null && view.Tab == tab, $"tab {(view != null ? view.Tab.ToString() : "none")}");
                if (view == null) yield break;
                var secrets = CodexSecrets(tab, content, config);
                int pages = view.Pages;
                for (int page = 0; page < pages && !Run.Done; page++)
                {
                    if (page > 0)
                    {
                        int target = page;
                        yield return ClickUntil(config.next, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                        yield return WaitReal(0.3f);
                    }
                    CodexDescribe($"{tab} page {page + 1}");
                    CodexSpoilers($"{tab} p{page + 1}", config, secrets);
                    CodexBounds($"{tab} p{page + 1}");
                    // The standard menu audit on the first and last page (overlaps, thumb size, on screen).
                    if (page == 0 || page == pages - 1)
                        yield return Audit($"Codex {tab} p{page + 1}", 0, 1, 0.3f, progress: false);
                }
                // Back to the first page for the next tab.
                for (int page = pages - 1; page > 0 && !Run.Done; page--)
                {
                    int target = page - 1;
                    yield return ClickUntil(config.previous, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                }
            }

            public IEnumerator CodexRun()
            {
                var gd = GameDefinition.Instance;
                var config = gd.codex;
                var content = GameContent.Current;
                var texts = gd.menuTheme.texts;
                if (config == null || !config.enabled || content == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "codex: the Codex is off or there is no content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                Run.Metric("screenWidth", Screen.width);
                Run.Metric("screenHeight", Screen.height);
                Run.Event("codex", $"screen {Screen.width}x{Screen.height} ({(SickOfIt.Core.UI.Menus.MenuScreen.Portrait ? "portrait" : "landscape")})");

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // 0. New Game forgets everything (setup shortcut for the starting state).
                CodexProgress.Meet("Imp");
                EnemyHints.Add("Imp");
                RogueliteManager.Instance.ResetAllRogueliteData();
                YoobaProgress.Reset();
                CodexProgress.Reload();
                CodexCheck("New Game empties the Codex", !CodexProgress.Seen("Imp") && CodexProgress.Of("Imp").Met == 0, $"imp seen: {CodexProgress.Seen("Imp")}");

                // 1. Open it on the main menu first: nothing met, everything a silhouette (no spoilers on a fresh save).
                yield return ClickUntil(config.buttonLabel, () => CodexPanel.IsOpen, 15f);
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                var view = CodexPanel.Current;
                CodexCheck("opens from the main menu", view != null, "the Codex button on the main menu");
                if (view == null) { End(PlaytestOutcome.Error); yield break; }
                CodexDescribe("fresh save");
                int freshMet = 0;
                foreach (var tab in new[] { CodexTab.Enemies, CodexTab.Bosses, CodexTab.Weapons })
                    foreach (var e in CodexBook.Entries(tab, content, config))
                        if (e.Met) freshMet++;
                CodexCheck("a fresh save has met no enemy, boss or extra weapon", freshMet <= 1, $"{freshMet} met (the rocket launcher is in his hands from the start)");
                CodexSpoilers("fresh Enemies", config, CodexSecrets(CodexTab.Enemies, content, config));
                yield return Audit("Codex (main menu)", 0, 1, 0.3f, progress: false);
                CodexBounds("fresh Enemies p1");
                yield return ClickUntil(gd.menuTheme.texts.done, () => !CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;

                // 2. Play Level 1 until the hero has met imps (real play; the bot).
                Run.Event("shortcut", $"load level {CodexLevel} directly");
                var old = GameplayWorld.Instance;
                var load = LoadLevel(CodexLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(CodexLevel), 60f, "level 1 to start");
                if (Run.Done) yield break;
                yield return PlayLevel(CodexLevel, 0f, until: () => GameplayWorld.Instance != null && GameClock.Instance != null && GameClock.Instance.Time >= 16f
                                                                      && CodexProgress.Of("Imp").Met >= 1);
                if (Run.Done) yield break;
                StopBot();
                var tally = CodexProgress.Of("Imp");
                CodexCheck("meeting imps is counted", tally.Met >= 1, $"Imp met {tally.Met}, killed {tally.Killed}");

                // 3. The pause menu's Codex button.
                yield return ClickUntil("II", () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(config.buttonLabel, () => CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                view = CodexPanel.Current;
                CodexCheck("opens from the pause menu", view != null, "the Codex button on the pause menu");
                if (view == null) { End(PlaytestOutcome.Error); yield break; }

                // 4. The Imp is listed with the hero's note, the weakness and the tally; the rest are silhouettes.
                CodexRowView impRow = null;
                foreach (var row in view.Rows)
                    if (row.Entry.Name == "Imp") impRow = row;
                if (impRow == null)
                {
                    // Alphabetical: the imp may be on a later page.
                    for (int page = 1; page < view.Pages && impRow == null && !Run.Done; page++)
                    {
                        int target = page;
                        yield return ClickUntil(config.next, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                        foreach (var row in CodexPanel.Current.Rows)
                            if (row.Entry.Name == "Imp") impRow = row;
                    }
                }
                if (Run.Done) yield break;
                string impText = impRow != null ? CodexRowText(impRow) : "";
                var impDef = content.Entities.TryGetValue("imp", out var d) ? d : null;
                CodexCheck("a met enemy is listed with its note", impRow != null && !impRow.Silhouette && impDef != null && impText.Contains(impDef.note),
                    impRow != null ? impText : "no Imp row");
                CodexCheck("with its weakness", impDef != null && impText.Contains(impDef.hint) && impText.Contains(config.weaknessPrefix.Trim()), impText);
                var shownTally = CodexProgress.Of("Imp");
                CodexCheck("with times met and killed", impText.Contains(string.Format(System.Globalization.CultureInfo.InvariantCulture, config.tallyFormat, shownTally.Met, shownTally.Killed)),
                    $"expected met {shownTally.Met} killed {shownTally.Killed}: {impText}");
                CodexCheck("its picture is not a silhouette", impRow != null && impRow.ArtColor.r > 0.5f && impRow.ArtColor.g > 0.5f, impRow != null ? impRow.ArtColor.ToString() : "-");

                // 5. Every tab, every page.
                yield return CodexWalk(CodexTab.Enemies, config.tabEnemies, content, config);
                yield return CodexWalk(CodexTab.Bosses, config.tabBosses, content, config);
                yield return CodexWalk(CodexTab.Weapons, config.tabWeapons, content, config);
                yield return CodexWalk(CodexTab.Characters, config.tabCharacters, content, config);
                if (Run.Done) yield break;
                bool heroMet = false, yoobaSilhouette = false;
                foreach (var row in CodexPanel.Current.Rows)
                {
                    if (row.Entry.Id == "hero") heroMet = !row.Silhouette;
                    if (row.Entry.Id == "yooba") yoobaSilhouette = row.Silhouette;
                }
                CodexCheck("characters: the hero is known, Yooba is a silhouette until met", heroMet && yoobaSilhouette, $"hero met {heroMet}, Yooba silhouette {yoobaSilhouette}");

                // 6. Close, resume.
                yield return ClickUntil(texts.done, () => !CodexPanel.IsOpen, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(texts.resume, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;

                // 7. The tallies persist: the main menu's Codex shows the same imp.
                int metBefore = CodexProgress.Of("Imp").Met;
                CodexProgress.Reload();
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(config.buttonLabel, () => CodexPanel.IsOpen, 15f);
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                bool persisted = false;
                string persistedText = "";
                for (int page = 0; page < CodexPanel.Current.Pages && !persisted && !Run.Done; page++)
                {
                    if (page > 0)
                    {
                        int target = page;
                        yield return ClickUntil(config.next, () => CodexPanel.Current != null && CodexPanel.Current.Page == target, 10f);
                    }
                    foreach (var row in CodexPanel.Current.Rows)
                        if (row.Entry.Name == "Imp" && !row.Silhouette)
                        {
                            persisted = row.Entry.TimesMet == metBefore && metBefore >= 1;
                            persistedText = CodexRowText(row);
                        }
                }
                CodexCheck("the tallies persist to the main menu", persisted, $"met before {metBefore}: {persistedText}");
                yield return Audit("Codex (main menu, met)", 0, 1, 0.3f, progress: false);
                yield return ClickUntil(texts.done, () => !CodexPanel.IsOpen, 10f);

                Run.Metric("codex.passed", codexPassed);
                Run.Metric("codex.failed", codexFailed);
                Run.Progress(1f);
                End(codexFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }
        }
    }
}
