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

        // ─── Pause ────────────────────────────────────────────────────

        GameObject pauseRoot;
        MenuButton pauseButton;
        float timeScaleBeforePause = 1f;

        public bool Paused => pauseRoot != null;
        bool Blocked => PickerOpen || resultsRoot != null || deathRoot != null && deathRoot.activeSelf;

        void BuildPauseButton()
        {
            var style = theme.backButton.Clone();
            style.size = new Vector2(88f, 88f);
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
            pauseButton = menu.AddButtonAt(new Vector2(1f, 1f), new Vector2(-80f, -78f), "II", style, () => SetPaused(true));
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
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                clock?.Pause();
                Sfx.Play(Sfx.Events.pause);
                BuildPauseMenu();
            }
            else
            {
                Destroy(pauseRoot);
                pauseRoot = null;
                if (settingsRoot != null)
                    Destroy(settingsRoot);
                settingsRoot = null;
                menu.Back = null;
                Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
                clock?.Resume();
                Sfx.Play(Sfx.Events.uiBack);
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
            }, theme.button.size, overlay.Inner);
            menu.AddButton(list, Texts.resume, theme.button, () => SetPaused(false));
            if (world.Host != null)
                menu.AddButton(list, Texts.restart, theme.button, () => Leave(() => world.Host.GoToLevel(world.LevelNumber), "restarted"));
            menu.AddButton(list, Texts.settings, theme.button, () =>
            {
                settingsRoot = SettingsPanel.Show(menu, () => { settingsRoot = null; menu.Back = () => SetPaused(false); });
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
            go();
        }

        // ─── Results ──────────────────────────────────────────────────

        GameObject resultsRoot;
        /// <summary>The end-of-level results screen is up (waiting for Continue).</summary>
        public bool ResultsOpen => resultsRoot != null;
        int pointsAtStart = -1;

        static readonly MenuGrade[] DefaultGrades =
        {
            new MenuGrade { grade = "S", minScore = 95, quip = "Flawless. Suspicious, even.", color = new Color(1f, 0.85f, 0.25f) },
            new MenuGrade { grade = "A", minScore = 80, quip = "Tuesday energy.", color = new Color(0.5f, 1f, 0.45f) },
            new MenuGrade { grade = "B", minScore = 60, quip = "Acceptable. Barely.", color = new Color(0.3f, 0.85f, 1f) },
            new MenuGrade { grade = "C", minScore = 40, quip = "Monday energy.", color = new Color(1f, 0.6f, 0.3f) },
            new MenuGrade { grade = "D", minScore = 0, quip = "You survived. Technically.", color = new Color(1f, 0.35f, 0.3f) },
        };

        /// <summary>
        /// Shows the end-of-level results (time, kills, hits, rewinds, diamonds) and a grade, paused, until the
        /// player continues. Score: 100, minus 12 per hit taken and 4 per rewind.
        /// </summary>
        public void ShowResults(Action onContinue)
        {
            if (resultsRoot != null)
                return;
            if (Paused)
                SetPaused(false);
            var summary = world.Telemetry.Summarize("complete");
            int points = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
            int diamondsGained = pointsAtStart >= 0 ? Mathf.Max(0, points - pointsAtStart) : 0;
            int score = Mathf.Clamp(100 - summary.playerHits * 12 - summary.rewinds * 4, 0, 100);
            var grades = theme.grades != null && theme.grades.Count > 0 ? theme.grades : DefaultGrades.ToList();
            var grade = grades.Where(g => score >= g.minScore).OrderByDescending(g => g.minScore).FirstOrDefault()
                        ?? grades.OrderBy(g => g.minScore).First();

            Time.timeScale = 0f;
            GameClock.Instance?.Pause();
            Sfx.Play(Sfx.Events.levelClear);
            Sfx.Play(Sfx.Events.levelClearVoice);

            var overlay = Overlay.Create(menu, "Results", 0.78f);
            resultsRoot = overlay.Root;
            var inner = overlay.Inner;
            menu.AddText(inner, "Title", Texts.resultsTitle, theme.title, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1500f, 170f),
                out _, Mathf.Min(theme.title.fontSize, 130f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 1.2f, speed = 0.4f }, -3f);

            var stats = new List<(string label, string value)>
            {
                (Texts.resultsTime, FormatTime(summary.duration)),
                (Texts.resultsKills, summary.kills.ToString()),
                (Texts.resultsHits, summary.playerHits.ToString()),
                (Texts.resultsRewinds, summary.rewinds.ToString()),
                (Texts.resultsDiamonds, "♦ " + diamondsGained),
            };
            for (int i = 0; i < stats.Count; i++)
            {
                float y = 0.66f - i * 0.085f;
                var label = menu.AddText(inner, "Stat", stats[i].label, theme.heading, new Vector2(0.36f, y), Vector2.zero,
                    new Vector2(520f, 70f), out _, 44f, null, theme.entrance);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var value = menu.AddText(inner, "Value", stats[i].value, theme.heading, new Vector2(0.52f, y), Vector2.zero,
                    new Vector2(300f, 70f), out _, 48f, null, theme.entrance);
                value.alignment = TextAlignmentOptions.MidlineRight;
                value.enableVertexGradient = false;
                value.color = theme.accent;
            }

            // The grade: a stamped starburst that slams in after the stats.
            var stamp = MenuGraphics.NewRect("Grade", inner);
            MenuGraphics.Place(stamp, new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(330f, 330f));
            MenuGraphics.Surface(stamp, new MenuSurfaceStyle
            {
                shape = MenuShape.Burst,
                fill = grade.color,
                borderColor = theme.title.outlineColor,
                borderWidth = 9f,
                shadowColor = theme.title.outlineColor,
                shadowOffset = new Vector2(8f, -10f),
            });
            var letter = MenuGraphics.Text(stamp, "Letter", grade.grade, theme.title, menu.TitleFont, 170f);
            letter.enableVertexGradient = false;
            letter.color = new Color(1f, 0.97f, 0.88f, 1f);
            var stampMotion = menu.AddMotion(stamp.gameObject, new MenuMotion { type = MenuMotionType.Pulse, amount = 0.03f, speed = 1f },
                new MenuEntrance { type = MenuEntranceType.Pop, duration = 0.45f, delay = 0f }, 12f);
            stampMotion.EntranceDelay = 0.9f;
            gradeSoundAt = Time.unscaledTime + 0.95f;
            menu.AddText(inner, "Quip", grade.quip, theme.subtitle, new Vector2(0.72f, 0.27f), Vector2.zero, new Vector2(620f, 80f),
                out var quipMotion, 36f, null, theme.entrance, null, 4f);
            quipMotion.EntranceDelay = 1.2f;

            var go = theme.button.Clone();
            go.size = new Vector2(420f, 100f);
            menu.AddButtonAt(new Vector2(0.5f, 0.1f), Vector2.zero, Texts.continueText, go, () =>
            {
                Destroy(resultsRoot);
                resultsRoot = null;
                Time.timeScale = 1f;
                GameClock.Instance?.Resume();
                onContinue?.Invoke();
            }, inner);
        }

        float gradeSoundAt = -1f;

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
            deathRoot.SetActive(false);
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
                        Sfx.Play(Sfx.Events.deathVoice);
                        deathAnnounced = true;
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
            MenuGraphics.SetText(deathHint, canUndo ? (Application.isMobilePlatform ? Texts.deathRewindTouch : Texts.deathRewind) : Texts.deathNoJuice);
            deathHint.enableVertexGradient = false;
            deathHint.color = canUndo ? style.rewind : theme.muted;
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
                // A beat of hit-stop sells the impact.
                world.Effects.SlowMotion(0.08f, 0.07f);
            }
            lastSeenHealth = health;
            float age = Time.unscaledTime - hurtAt;
            float alpha = age < 0.45f ? 0.75f * (1f - age / 0.45f) : 0f;
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
                pointsAtStart = RogueliteManager.Instance.GetPoints();
            UpdateHurtFeedback();
            UpdateDeath();
            UpdatePauseKeys();
            if (gradeSoundAt > 0f && Time.unscaledTime >= gradeSoundAt)
            {
                gradeSoundAt = -1f;
                Sfx.Play(Sfx.Events.gradeReveal);
            }
        }
    }
}
