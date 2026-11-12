using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The HUD's full-screen moments: the pause menu (Esc / P / Start, or the pause button), the end-of-level
    /// results with a grade, the "undo your death" countdown, and the red flash + shake + hit-stop when the player
    /// is hurt. All presentation: pausing stops the game clock, nothing here changes the simulation.
    /// </summary>
    public sealed partial class GameplayHud
    {
        MenuTexts Texts => theme.texts ?? (theme.texts = new MenuTexts());
        MenuPortraitScreens Screens => menu.Responsive.screens ?? (menu.Responsive.screens = new MenuPortraitScreens());

        // ─── Pause ────────────────────────────────────────────────────

        GameObject pauseRoot;
        MenuButton pauseButton;
        float timeScaleBeforePause = 1f;

        public bool Paused => pauseRoot != null;
        bool Blocked => PickerOpen || resultsRoot != null || deathRoot != null && deathRoot.activeSelf;

        void BuildPauseButton()
        {
            var place = Landscape.pause;
            var style = theme.backButton.Clone();
            style.size = place.size;
            style.surface = new MenuSurfaceStyle
            {
                shape = MenuShape.Circle,
                fill = theme.chip.fill,
                borderColor = theme.chip.borderColor,
                borderWidth = 5f,
                shadowColor = new Color(0f, 0f, 0f, 0.4f),
                shadowOffset = new Vector2(0f, -5f),
                padding = new Vector2(6f, 6f),
            };
            style.tilt = 0f;
            style.hoverRotation = 0f;
            pauseButton = menu.AddButtonAt(place.anchor, place.offset, "II", style, () => SetPaused(true));
            Lay(pauseButton.transform.parent, l => l.pause, MenuElementKind.Button);
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        int pauseChangedFrame = -1;

        void UpdatePauseKeys()
        {
            // One change per frame: Escape also reaches MenuCanvas.Back (resume / close settings) this frame.
            if (pauseChangedFrame == Time.frameCount)
                return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool toggle = (keyboard != null && keyboard.pKey.wasPressedThisFrame) || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
            if (Paused)
            {
                // Escape is handled by MenuCanvas.Back (closes settings, else resumes); P/Start resume unless in settings.
                if (toggle && settingsRoot == null)
                    SetPaused(false);
            }
            else if ((toggle || escape) && !Blocked)
            {
                SetPaused(true);
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused == Paused || (paused && Blocked))
                return;
            pauseChangedFrame = Time.frameCount;
            var clock = GameClock.Instance;
            if (paused)
            {
                // Pausing mid-rewind would leave the clock running backward; end the rewind first.
                SickOfIt.Core.Rewind.RewindInput.End();
                // A hit-freeze in progress gives Unity's clock back first, or the resume would restore the frozen scale.
                SickOfIt.Core.Fx.HitFreeze.Cancel();
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                clock?.Pause();
                Sfx.Play(Sfx.Events.pause);
                BuildPauseMenu();
                SickOfIt.Core.AnalyticsManager.Screen("pause");
            }
            else
            {
                // Hide at once: Destroy waits for the end of the frame, and the title must never outlive the resume.
                PanelAnim.CloseAndDestroy(pauseRoot);
                pauseRoot = null;
                if (settingsRoot != null)
                {
                    settingsRoot.SetActive(false);
                    Destroy(settingsRoot);
                }
                settingsRoot = null;
                menu.Back = null;
                Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
                clock?.Resume();
                Sfx.Play(Sfx.Events.uiBack);
                SickOfIt.Core.AnalyticsManager.Screen("level");
            }
        }

        GameObject settingsRoot;

        void BuildPauseMenu()
        {
            var overlay = Overlay.Create(menu, "Pause", 0.72f);
            pauseRoot = overlay.Root;
            menu.AddText(overlay.Inner, "Title", Texts.pauseTitle, theme.title, new Vector2(0.5f, 0.8f), Vector2.zero,
                new Vector2(1400f, 170f), out _, Mathf.Min(theme.title.fontSize, 130f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 1.2f, speed = 0.4f }, -3f);
            menu.AddText(overlay.Inner, "Subtitle", Texts.pauseSubtitle, theme.subtitle, new Vector2(0.5f, 0.68f), Vector2.zero,
                new Vector2(1400f, 70f), out _, 36f, null, theme.entrance);

            var list = menu.AddList(new MenuList
            {
                anchor = new Vector2(0.5f, 0.36f),
                size = new Vector2(700f, 520f),
                direction = MenuListDirection.Vertical,
                spacing = new Vector2(0f, 14f),
                portrait = Screens.pauseList,
            }, theme.button.size, overlay.Inner);
            menu.AddButton(list, Texts.resume, theme.button, () => SetPaused(false));
            if (world.Host != null)
                menu.AddButton(list, Texts.restart, theme.button, () => Leave(() => world.Host.GoToLevel(world.LevelNumber), "restarted"));
            menu.AddButton(list, Texts.settings, theme.button, () =>
            {
                settingsRoot = SettingsPanel.Show(menu, () => { settingsRoot = null; menu.Back = () => SetPaused(false); });
                SickOfIt.Core.AnalyticsManager.Screen("settings");
            });
            var codex = SickOfIt.Data.GameDefinition.Instance != null ? SickOfIt.Data.GameDefinition.Instance.codex : null;
            if (codex != null && codex.enabled && codex.showOnPauseMenu)
                menu.AddButton(list, codex.buttonLabel, theme.button, () =>
                {
                    // Shares settingsRoot: while it is up, P does not resume and a resume closes it.
                    settingsRoot = CodexPanel.Show(menu, () => { settingsRoot = null; menu.Back = () => SetPaused(false); }).Root;
                    SickOfIt.Core.AnalyticsManager.Screen("codex");
                });
            if (world.Host != null)
                menu.AddButton(list, Texts.quitToMenu, theme.button, () => Leave(() => world.Host.ReturnToMenu(), "quit"));
            menu.Back = () => SetPaused(false);
        }

        /// <summary>Unpauses (so the next scene doesn't start frozen) and leaves the level.</summary>
        void Leave(Action go, string outcome)
        {
            Time.timeScale = 1f;
            GameClock.Instance?.Resume();
            world.Telemetry?.Finish(outcome);
            PlayAnalytics.Left(world, outcome);
            go();
        }

        // ─── Results ──────────────────────────────────────────────────

        GameObject resultsRoot;
        /// <summary>The end-of-level results screen is up (waiting for Continue).</summary>
        public bool ResultsOpen => resultsRoot != null;
        /// <summary>The results rows as shown, "Time: 1:23 | Clean Kills: 4 | ...", for the playtest.</summary>
        public string ResultsSummary { get; private set; } = "";
        /// <summary>Clean kills on the results screen (kills with the hero unhurt).</summary>
        public int ResultsCleanKills { get; private set; }
        int pointsAtStart = -1;

        static readonly MenuGrade[] DefaultGrades =
        {
            new MenuGrade { grade = "S", minScore = 95, quip = "Flawless. Suspicious, even.", color = new Color(1f, 0.85f, 0.25f) },
            new MenuGrade { grade = "A", minScore = 80, quip = "Tuesday energy.", color = new Color(0.5f, 1f, 0.45f) },
            new MenuGrade { grade = "B", minScore = 60, quip = "Acceptable. Barely.", color = new Color(0.3f, 0.85f, 1f) },
            new MenuGrade { grade = "C", minScore = 40, quip = "Barely a dent in Hell.", color = new Color(1f, 0.6f, 0.3f) },
            new MenuGrade { grade = "D", minScore = 0, quip = "You survived. Technically.", color = new Color(1f, 0.35f, 0.3f) },
        };

        /// <summary>
        /// Shows the end-of-level results (time, kills, hits, rewinds, diamonds) and a grade, paused, until the
        /// player continues. Score: 100, minus 12 per hit taken and 4 per rewind.
        /// </summary>
        bool chapterCardShown;

        /// <summary>
        /// CHAPTER ONE card before the Grudge pick (Docs/Story.md). Continue offers a Grudge, then clears the floor;
        /// <see cref="ShowResults"/> then skips so the card is not shown twice.
        /// </summary>
        public void ShowChapterCard(ShowChapterCardAction action, ActionContext context)
        {
            if (resultsRoot != null || action == null)
                return;
            if (Paused)
                SetPaused(false);
            chapterCardShown = true;
            world.SetCounter("chapterOne", 1f);
            var summary = world.Telemetry.Summarize("chapter");
            int points = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
            int diamondsGained = pointsAtStart >= 0 ? Mathf.Max(0, points - pointsAtStart) : 0;
            int heroDeaths = Mathf.Max(0, Mathf.RoundToInt(world.GetCounter("heroDeaths")));

            Time.timeScale = 0f;
            GameClock.Instance?.Pause();
            PlayAnalytics.ResultsShown(world);
            UiSounds.LevelClear();
            string hookSound = string.IsNullOrEmpty(action.hookSound) ? Texts.chapterOneHookSound : action.hookSound;
            if (string.IsNullOrEmpty(hookSound))
                hookSound = "vo_ch1_hero_daylight";
            Sfx.Play(hookSound);
            string hookText = string.IsNullOrEmpty(action.hookText) ? Texts.chapterOneHook : action.hookText;
            world.Telemetry?.Record("signal", "chapter1:hook", 1, hookText);

            var overlay = Overlay.Create(menu, "ChapterCard", 0.78f);
            resultsRoot = overlay.Root;
            var inner = overlay.Inner;
            string title = string.IsNullOrEmpty(action.title)
                ? (string.IsNullOrEmpty(Texts.chapterOneTitle) ? "CHAPTER ONE" : Texts.chapterOneTitle)
                : action.title;
            menu.AddText(inner, "Title", title, theme.title, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1500f, 170f),
                out _, Mathf.Min(theme.title.fontSize, 130f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 1.2f, speed = 0.4f }, -3f);

            var stats = new List<(string label, string value, int amount, Func<int, string> format)>
            {
                (Texts.resultsTime, FormatTime(summary.duration), Mathf.Max(0, Mathf.FloorToInt(summary.duration)), s => FormatTime(s)),
                (Texts.resultsDeaths, heroDeaths.ToString(), heroDeaths, s => s.ToString()),
                (Texts.resultsRewinds, summary.rewinds.ToString(), summary.rewinds, s => s.ToString()),
                (Texts.resultsDiamonds, "♦ " + diamondsGained, diamondsGained, s => "♦ " + s),
            };
            ResultsSummary = string.Join(" | ", stats.Select(r => r.label + ": " + r.value));
            world.Telemetry?.Record("signal", "chapter1:cardShown", 1, ResultsSummary);

            var screens = Screens;
            for (int i = 0; i < stats.Count; i++)
            {
                float y = 0.66f - i * 0.085f;
                float tallY = screens.resultsStatsTop - i * screens.resultsStatsStep;
                var label = menu.AddText(inner, "Stat", stats[i].label, theme.heading, new Vector2(0.36f, y), Vector2.zero,
                    new Vector2(520f, 70f), out _, 44f, null, theme.entrance);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                MenuResponsive.Track((RectTransform)label.transform.parent, MenuPlacement.At(0.36f, y, 0f, 0f, 520f, 70f),
                    MenuPlacement.At(screens.resultsLabelX, tallY, 0f, 0f, 520f, 70f), MenuElementKind.Text, menu.Responsive);
                var value = menu.AddText(inner, "Value", stats[i].value, theme.heading, new Vector2(0.52f, y), Vector2.zero,
                    new Vector2(300f, 70f), out _, 48f, null, theme.entrance);
                MenuResponsive.Track((RectTransform)value.transform.parent, MenuPlacement.At(0.52f, y, 0f, 0f, 300f, 70f),
                    MenuPlacement.At(screens.resultsValueX, tallY, 0f, 0f, 300f, 70f), MenuElementKind.Text, menu.Responsive);
                value.alignment = TextAlignmentOptions.MidlineRight;
                value.enableVertexGradient = false;
                value.color = theme.accent;
                var ticker = NumberTicker.On(value, stats[i].format);
                ticker.Snap(0);
                ticker.CountTo(stats[i].amount, 0.7f, 0.55f + i * 0.14f);
            }

            var stamp = MenuGraphics.NewRect("Grade", inner);
            MenuGraphics.Place(stamp, new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(330f, 330f));
            MenuGraphics.Surface(stamp, new MenuSurfaceStyle
            {
                shape = MenuShape.Burst,
                fill = new Color(0.72f, 0.45f, 0.95f, 1f),
                borderColor = theme.title.outlineColor,
                borderWidth = 9f,
                shadowColor = theme.title.outlineColor,
                shadowOffset = new Vector2(8f, -10f),
            });
            var letter = MenuGraphics.Text(stamp, "Letter", "I", theme.title, menu.TitleFont, 170f);
            letter.enableVertexGradient = false;
            letter.color = new Color(1f, 0.97f, 0.88f, 1f);
            var stampMotion = menu.AddMotion(stamp.gameObject, new MenuMotion { type = MenuMotionType.Pulse, amount = 0.03f, speed = 1f },
                new MenuEntrance { type = MenuEntranceType.Pop, duration = 0.45f, delay = 0f }, 12f);
            MenuResponsive.Track(stamp, MenuPlacement.At(0.72f, 0.5f, 0f, 0f, 330f, 330f), screens.resultsGrade, MenuElementKind.Panel, menu.Responsive);
            stampMotion.EntranceDelay = 0.9f;

            var quip = menu.AddText(inner, "Quip", string.IsNullOrEmpty(hookText) ? "Daylight's still up there. Somewhere." : hookText,
                theme.subtitle, new Vector2(0.72f, 0.27f), Vector2.zero, new Vector2(620f, 100f),
                out var quipMotion, 36f, null, theme.entrance, null, 4f);
            MenuResponsive.Track((RectTransform)quip.transform.parent, MenuPlacement.At(0.72f, 0.27f, 0f, 0f, 620f, 100f), screens.resultsQuip,
                MenuElementKind.Text, menu.Responsive);
            quipMotion.EntranceDelay = 1.2f;

            var go = theme.button.Clone();
            go.size = new Vector2(420f, Mathf.Max(100f, go.size.y));
            menu.AddButtonAt(new Vector2(0.5f, 0.1f), Vector2.zero, Texts.continueText, go, () =>
            {
                if (!continuing)
                    StartCoroutine(ContinueChapterCard(context));
            }, inner, screens.resultsContinue);
            world.Telemetry?.Record("signal", "chapter1:continueReady", Mathf.RoundToInt(go.size.y), "continueH");
        }

        System.Collections.IEnumerator ContinueChapterCard(ActionContext context)
        {
            continuing = true;
            PanelAnim.CloseAndDestroy(resultsRoot);
            resultsRoot = null;
            continuing = false;
            Time.timeScale = 1f;
            GameClock.Instance?.Resume();
            // Grudge pick after the card, then clear — Continue leads on as today.
            var offer = new OfferUpgradesAction { title = "Pick a grudge", count = 3 };
            world.Upgrades.Offer(offer, context);
            float waitUntil = Time.unscaledTime + 90f;
            while (PickerOpen && Time.unscaledTime < waitUntil)
                yield return null;
            world.SetCounter("cleared", 1f);
        }

        public void ShowResults(Action onContinue)
        {
            if (resultsRoot != null)
                return;
            // Chapter card already celebrated this clear: go on without a second results screen.
            if (chapterCardShown && world.GetCounter("chapterOne") >= 1f)
            {
                onContinue?.Invoke();
                return;
            }
            if (Paused)
                SetPaused(false);
            var summary = world.Telemetry.Summarize("complete");
            int points = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
            int diamondsGained = pointsAtStart >= 0 ? Mathf.Max(0, points - pointsAtStart) : 0;
            bool chapterOne = world.GetCounter("chapterOne") >= 1f;
            // The floor's rank (S/A/B/C: par time, no hits, grazes, rewind use), saved per floor when it is the best.
            var rank = !chapterOne && world.Style != null && world.Style.Active ? world.Style.Rank(summary) : (FloorRank?)null;
            int cleanKills = Mathf.Clamp(Mathf.RoundToInt(world.GetCounter("cleanKills")), 0, summary.kills);
            world.Telemetry?.Record("cleanKills", "level " + world.LevelNumber, cleanKills, $"kills {summary.kills} time {summary.duration:0.0}");
            int score = Mathf.Clamp(100 - summary.playerHits * 12 - summary.rewinds * 4, 0, 100);
            var grades = theme.grades != null && theme.grades.Count > 0 ? theme.grades : DefaultGrades.ToList();
            var grade = rank.HasValue
                ? grades.FirstOrDefault(g => g.grade == rank.Value.Letter) ?? DefaultGrades.FirstOrDefault(g => g.grade == rank.Value.Letter) ?? grades.OrderBy(g => g.minScore).First()
                : grades.Where(g => score >= g.minScore).OrderByDescending(g => g.minScore).FirstOrDefault()
                        ?? grades.OrderBy(g => g.minScore).First();
            bool newBest = false;
            string bestLine = "";
            if (rank.HasValue)
            {
                newBest = StyleRanks.Record(world.LevelNumber, rank.Value.Letter, rank.Value.Points);
                StyleRanks.TryGet(world.LevelNumber, out var now);
                bestLine = newBest ? Texts.resultsNewBest : string.Format(Texts.resultsBest, now.Letter);
                world.Telemetry?.Record("rank", rank.Value.Letter, rank.Value.Points,
                    $"level {world.LevelNumber} par {rank.Value.Par:0} time {rank.Value.Seconds:0} hits {rank.Value.Hits} grazes {rank.Value.Grazes} kills {rank.Value.Kills} rewinds {summary.rewinds} best {(newBest ? "new" : now.Letter)} saved {(StyleRanks.TryGet(world.LevelNumber, out var saved) ? saved.Letter + saved.Points : "none")}");
            }

            Time.timeScale = 0f;
            GameClock.Instance?.Pause();
            PlayAnalytics.ResultsShown(world);
            UiSounds.LevelClear();
            if (chapterOne)
            {
                string hookSound = string.IsNullOrEmpty(Texts.chapterOneHookSound) ? "vo_ch1_hero_daylight" : Texts.chapterOneHookSound;
                Sfx.Play(hookSound);
                world.Telemetry?.Record("signal", "chapter1:hook", 1, Texts.chapterOneHook);
            }
            else
                Sfx.Play(Sfx.Events.levelClearVoice);
            resultsNewBestPending = newBest;

            var overlay = Overlay.Create(menu, "Results", 0.78f);
            resultsRoot = overlay.Root;
            var inner = overlay.Inner;
            string title = chapterOne
                ? (string.IsNullOrEmpty(Texts.chapterOneTitle) ? "CHAPTER ONE" : Texts.chapterOneTitle)
                : Texts.resultsTitle;
            menu.AddText(inner, "Title", title, theme.title, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1500f, 170f),
                out _, Mathf.Min(theme.title.fontSize, 130f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 1.2f, speed = 0.4f }, -3f);

            int heroDeaths = Mathf.Max(0, Mathf.RoundToInt(world.GetCounter("heroDeaths")));
            var stats = new List<(string label, string value, int amount, Func<int, string> format)>();
            if (chapterOne)
            {
                stats.Add((Texts.resultsTime, FormatTime(summary.duration), Mathf.Max(0, Mathf.FloorToInt(summary.duration)), s => FormatTime(s)));
                stats.Add((Texts.resultsDeaths, heroDeaths.ToString(), heroDeaths, s => s.ToString()));
                stats.Add((Texts.resultsRewinds, summary.rewinds.ToString(), summary.rewinds, s => s.ToString()));
                stats.Add((Texts.resultsDiamonds, "♦ " + diamondsGained, diamondsGained, s => "♦ " + s));
            }
            else
            {
                stats.Add((rank.HasValue ? string.Format(Texts.resultsTimePar, FormatTime(rank.Value.Par)) : Texts.resultsTime, FormatTime(summary.duration), Mathf.Max(0, Mathf.FloorToInt(summary.duration)), s => FormatTime(s)));
                stats.Add((Texts.resultsKills, summary.kills.ToString(), summary.kills, s => s.ToString()));
                stats.Add((Texts.resultsCleanKills, cleanKills.ToString(), cleanKills, s => s.ToString()));
                stats.Add((Texts.resultsHits, summary.playerHits.ToString(), summary.playerHits, s => s.ToString()));
                if (rank.HasValue)
                    stats.Add((Texts.resultsGrazes, rank.Value.Grazes.ToString(), rank.Value.Grazes, s => s.ToString()));
                stats.Add((Texts.resultsRewinds, summary.rewinds.ToString(), summary.rewinds, s => s.ToString()));
                stats.Add((Texts.resultsDiamonds, "♦ " + diamondsGained, diamondsGained, s => "♦ " + s));
            }
            ResultsSummary = string.Join(" | ", stats.Select(r => r.label + ": " + r.value));
            ResultsCleanKills = cleanKills;
            if (chapterOne)
                world.Telemetry?.Record("signal", "chapter1:cardShown", 1, ResultsSummary);
            // Landscape: the stats on the left, the grade beside them. Portrait: stats on top, grade under them.
            var screens = Screens;
            for (int i = 0; i < stats.Count; i++)
            {
                float y = 0.66f - i * 0.085f;
                float tallY = screens.resultsStatsTop - i * (stats.Count > 5 ? Mathf.Min(screens.resultsStatsStep, 0.046f) : screens.resultsStatsStep);
                var label = menu.AddText(inner, "Stat", stats[i].label, theme.heading, new Vector2(0.36f, y), Vector2.zero,
                    new Vector2(520f, 70f), out _, 44f, null, theme.entrance);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                MenuResponsive.Track((RectTransform)label.transform.parent, MenuPlacement.At(0.36f, y, 0f, 0f, 520f, 70f),
                    MenuPlacement.At(screens.resultsLabelX, tallY, 0f, 0f, 520f, 70f), MenuElementKind.Text, menu.Responsive);
                var value = menu.AddText(inner, "Value", stats[i].value, theme.heading, new Vector2(0.52f, y), Vector2.zero,
                    new Vector2(300f, 70f), out _, 48f, null, theme.entrance);
                MenuResponsive.Track((RectTransform)value.transform.parent, MenuPlacement.At(0.52f, y, 0f, 0f, 300f, 70f),
                    MenuPlacement.At(screens.resultsValueX, tallY, 0f, 0f, 300f, 70f), MenuElementKind.Text, menu.Responsive);
                value.alignment = TextAlignmentOptions.MidlineRight;
                value.enableVertexGradient = false;
                value.color = theme.accent;
                // The numbers count up one row after another once the screen is in.
                var ticker = NumberTicker.On(value, stats[i].format);
                ticker.Snap(0);
                ticker.CountTo(stats[i].amount, 0.7f, 0.55f + i * 0.14f);
            }

            // The grade (or Chapter One's "I" stamp): a stamped starburst that slams in after the stats.
            var stamp = MenuGraphics.NewRect("Grade", inner);
            MenuGraphics.Place(stamp, new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(330f, 330f));
            var stampColor = chapterOne ? new Color(0.72f, 0.45f, 0.95f, 1f) : grade.color;
            MenuGraphics.Surface(stamp, new MenuSurfaceStyle
            {
                shape = MenuShape.Burst,
                fill = stampColor,
                borderColor = theme.title.outlineColor,
                borderWidth = 9f,
                shadowColor = theme.title.outlineColor,
                shadowOffset = new Vector2(8f, -10f),
            });
            var letter = MenuGraphics.Text(stamp, "Letter", chapterOne ? "I" : grade.grade, theme.title, menu.TitleFont, 170f);
            letter.enableVertexGradient = false;
            letter.color = new Color(1f, 0.97f, 0.88f, 1f);
            var stampMotion = menu.AddMotion(stamp.gameObject, new MenuMotion { type = MenuMotionType.Pulse, amount = 0.03f, speed = 1f },
                new MenuEntrance { type = MenuEntranceType.Pop, duration = 0.45f, delay = 0f }, 12f);
            MenuResponsive.Track(stamp, MenuPlacement.At(0.72f, 0.5f, 0f, 0f, 330f, 330f), screens.resultsGrade, MenuElementKind.Panel, menu.Responsive);
            stampMotion.EntranceDelay = 0.9f;
            gradeSoundAt = Time.unscaledTime + 0.95f;
            gradeSting = chapterOne ? "" : grade.stingSound;
            string quipText = chapterOne
                ? (string.IsNullOrEmpty(Texts.chapterOneHook) ? "Daylight's still up there. Somewhere." : Texts.chapterOneHook)
                : grade.quip;
            var quip = menu.AddText(inner, "Quip", quipText, theme.subtitle, new Vector2(0.72f, 0.27f), Vector2.zero, new Vector2(620f, 100f),
                out var quipMotion, 36f, null, theme.entrance, null, 4f);
            MenuResponsive.Track((RectTransform)quip.transform.parent, MenuPlacement.At(0.72f, 0.27f, 0f, 0f, 620f, 100f), screens.resultsQuip,
                MenuElementKind.Text, menu.Responsive);
            quipMotion.EntranceDelay = 1.2f;

            // A no-hit floor: Hell's cabinet and its crowd say so (subtitle on the screen, then the voices); the best rank sits under the quip.
            string callout = "";
            if (rank.HasValue)
            {
                if (rank.Value.NoHit)
                    callout = world.Style.SayOnResults("perfect");
                else if (rank.Value.Letter == "S")
                    callout = world.Style.SayOnResults("rankS");
            }
            string under = rank.HasValue && rank.Value.NoHit ? Texts.resultsNoHit + (callout.Length > 0 ? "\n" + callout : "") : callout;
            if (bestLine.Length > 0)
                under = under.Length > 0 ? under + "\n" + bestLine : bestLine;
            if (under.Length > 0)
            {
                var extra = menu.AddText(inner, "Rank", under, theme.caption, new Vector2(0.72f, 0.185f), Vector2.zero, new Vector2(900f, 100f),
                    out var extraMotion, 30f, null, theme.entrance, null, 4f);
                MenuResponsive.Track((RectTransform)extra.transform.parent, MenuPlacement.At(0.72f, 0.185f, 0f, 0f, 900f, 100f),
                    MenuPlacement.At(0.5f, 0.155f, 0f, 0f, 1000f, 130f), MenuElementKind.Text, menu.Responsive);
                extraMotion.EntranceDelay = 1.5f;
            }

            // The next goal: what the diamonds are for, the bar filled with the ones just earned.
            // Built through Core's bridge — Gameplay cannot name SickOfIt.Core.Progression (circular asmdef).
            int goalFloor = world.Stage != null && world.Stage.HasFloors ? world.Stage.Floor : 0;
            NextGoalBridge.BuildResultsCard?.Invoke(menu, inner, goalFloor);

            var go = theme.button.Clone();
            // Phone acceptance: Continue at least 48 px tall (100 design units keeps it well above on 390x844).
            go.size = new Vector2(420f, Mathf.Max(100f, go.size.y));
            menu.AddButtonAt(new Vector2(0.5f, 0.1f), Vector2.zero, Texts.continueText, go, () =>
            {
                if (!continuing)
                    StartCoroutine(ContinueThroughCover(onContinue));
            }, inner, screens.resultsContinue);
            if (chapterOne)
                world.Telemetry?.Record("signal", "chapter1:continueReady", Mathf.RoundToInt(go.size.y), "continueH");
        }

        bool continuing;

        /// <summary>The screen transition between floors: the results fade out, the screen closes on the floor's number, then the next scene opens it.</summary>
        System.Collections.IEnumerator ContinueThroughCover(Action onContinue)
        {
            continuing = true;
            PanelAnim.CloseAndDestroy(resultsRoot);
            string label = world.Stage != null && world.Stage.HasFloors ? string.Format(Texts.floorFormat ?? "FLOOR {0}", world.Stage.Floor) : "";
            ScreenTransition.Cover(label);
            yield return new WaitForSecondsRealtime(ScreenTransition.CoverSeconds + 0.05f);
            resultsRoot = null;
            continuing = false;
            Time.timeScale = 1f;
            GameClock.Instance?.Resume();
            onContinue?.Invoke();
        }

        float gradeSoundAt = -1f;
        string gradeSting = "";
        bool resultsNewBestPending;

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        // ─── Death: "undo it" countdown ───────────────────────────────

        GameObject deathRoot;
        MenuBar deathBar;
        TextMeshProUGUI deathHint;
        PlayerHealth playerHealth;
        bool deathAnnounced;
        // What the hint shows now (the words and whether the colour is the rewind's), so a frame in the window only writes on a change.
        string deathHintShown;
        bool deathHintCanUndo;

        void BuildDeathOverlay()
        {
            deathRoot = MenuGraphics.NewImage(transform, "Death", new Color(0.55f, 0.05f, 0.08f, 0.55f)).gameObject;
            deathRoot.GetComponent<Image>().sprite = MenuGraphics.Vignette();
            var inner = MenuGraphics.NewRect("SafeArea", deathRoot.transform);
            MenuGraphics.Fill(inner);
            inner.gameObject.AddComponent<MenuSafeArea>();
            menu.AddText(inner, "Title", Texts.deathTitle, theme.title, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1500f, 180f),
                out _, Mathf.Min(theme.title.fontSize, 140f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Jitter, amount = 6f, speed = 1f }, -4f);
            deathHint = menu.AddText(inner, "Hint", "", theme.subtitle, new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(1400f, 80f),
                out _, 46f, null, theme.entrance, new MenuMotion { type = MenuMotionType.Pulse, amount = 0.05f, speed = 2f });

            var barSlot = MenuGraphics.NewRect("Countdown", inner);
            MenuGraphics.Place(barSlot, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(700f, 40f));
            MenuGraphics.Surface(barSlot, theme.chip);
            deathBar = MenuBar.Create(barSlot, "Bar");
            MenuGraphics.Fill((RectTransform)deathBar.transform, 9f);
            deathBar.Fill = style.rewind;
            deathBar.LowFill = style.lowHealthFill;
            deathBar.Lost = style.healthLost;
            deathBar.Track = style.barTrack;
            deathBar.MaxSegments = 0;
            deathHint.enableVertexGradient = false;
            PrewarmDeathOverlay();
            deathRoot.SetActive(false);
        }

        /// <summary>
        /// Builds every glyph and mesh the death overlay will need while it is still switched off. The first time it came on (the hit
        /// that leaves the hero at 0) its 140 pt title and hint had to rasterize their glyphs and build their meshes on that very
        /// frame, a stall of tens of milliseconds on a phone's CPU, right at the hit-freeze. The words are the same ones the
        /// overlay shows, so nothing is made at the hit.
        /// </summary>
        void PrewarmDeathOverlay()
        {
            string[] hints = { Texts.deathRewindTouch, Texts.deathRewind, Texts.deathNoJuice };
            foreach (var hint in hints)
            {
                if (string.IsNullOrEmpty(hint))
                    continue;
                MenuGraphics.SetText(deathHint, hint);
                foreach (var text in deathRoot.GetComponentsInChildren<TMP_Text>(true))
                    text.ForceMeshUpdate(true, true);
            }
            deathHintShown = null;
        }

        void UpdateDeath()
        {
            if (deathRoot == null)
                return;
            if (playerHealth == null && world != null && world.Player != null)
                playerHealth = world.Player.GetComponentInChildren<PlayerHealth>();
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            bool dying = playerHealth != null && playerHealth.DeathTimeLeft >= 0f && !rewinding;
            if (deathRoot.activeSelf != dying)
            {
                deathRoot.SetActive(dying);
                if (dying)
                {
                    deathRoot.transform.SetAsLastSibling();
                    if (Paused)
                        SetPaused(false);
                    if (!deathAnnounced)
                    {
                        UiSounds.DeathSting();
                        Sfx.Play(Sfx.Events.deathVoice);
                        deathAnnounced = true;
                        // Chapter One card counts every death window opened on this floor (undone ones too).
                        world?.AddCounter("heroDeaths", 1f);
                    }
                    Sfx.Play(Sfx.Events.deathWarning);
                }
            }
            if (!dying)
            {
                if (playerHealth != null && playerHealth.DeathTimeLeft < 0f)
                    deathAnnounced = false;
                return;
            }
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            bool canUndo = budget == null || budget.CanRewind;
            // Written only when it changes: every write rebuilt the hint's mesh and its shadow's (and found the shadow by name,
            // with a string made) on every frame of the 2.5 s window, which is where a phone's frames went.
            string hintText = canUndo ? (SickOfIt.Core.Device.TouchDevice.IsPrimary ? Texts.deathRewindTouch : Texts.deathRewind) : Texts.deathNoJuice;
            if (!string.Equals(hintText, deathHintShown) || canUndo != deathHintCanUndo)
            {
                deathHintShown = hintText;
                deathHintCanUndo = canUndo;
                MenuGraphics.SetText(deathHint, hintText);
                deathHint.color = canUndo ? style.rewind : theme.muted;
            }
            float window = Mathf.Max(0.01f, playerHealth.DeathWindow);
            deathBar.Set(Mathf.RoundToInt(playerHealth.DeathTimeLeft / window * 1000f), 1000);
        }

        // ─── Getting hurt: red flash, shake, hit-stop ─────────────────

        Image hurtVignette;
        float hurtAt = -99f;
        int lastSeenHealth = -1;

        void BuildHurtFeedback()
        {
            hurtVignette = MenuGraphics.NewImage(transform, "Hurt", Color.clear);
            hurtVignette.sprite = MenuGraphics.Vignette();
            hurtVignette.transform.SetSiblingIndex(1);
        }

        void UpdateHurtFeedback()
        {
            var player = world != null ? world.Player : null;
            int health = player != null ? player.Health : -1;
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            if (lastSeenHealth >= 0 && health >= 0 && health < lastSeenHealth && !rewinding)
            {
                hurtAt = Time.unscaledTime;
                world.Effects.Shake(0.22f, 0.25f);
                // A beat of hit-freeze sells the impact (game.json feel.freeze.heroHurtSeconds). Unity's clock, not the game
                // clock: the simulation's ticks stay the same, they just come a moment later.
                world.Feel?.HeroHurt();
            }
            lastSeenHealth = health;
            float age = Time.unscaledTime - hurtAt;
            float alpha = age < 0.45f ? (SickOfIt.Data.GameSettings.ReducedFlash ? 0.3f : 0.75f) * (1f - age / 0.45f) : 0f;
            hurtVignette.color = new Color(0.9f, 0.08f, 0.1f, alpha);
        }

        // ─── Wiring ───────────────────────────────────────────────────

        void BuildScreens()
        {
            BuildHurtFeedback();
            BuildPauseButton();
            BuildDeathOverlay();
        }

        void UpdateScreens()
        {
            if (pointsAtStart < 0 && RogueliteManager.Instance != null)
            {
                pointsAtStart = RogueliteManager.Instance.GetPoints();
                NextGoalBridge.RunStartPoints = pointsAtStart;
            }
            UpdateHurtFeedback();
            UpdateDeath();
            UpdatePauseKeys();
            if (gradeSoundAt > 0f && Time.unscaledTime >= gradeSoundAt)
            {
                gradeSoundAt = -1f;
                UiSounds.RankStamp();
                Sfx.Play(Sfx.Events.gradeReveal);
                Sfx.Play(gradeSting);
                if (resultsNewBestPending)
                {
                    resultsNewBestPending = false;
                    UiSounds.NewBest();
                }
            }
        }
    }
}
