using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The card that drops from the top of the screen when an achievement unlocks (Docs/Story.md, "Achievements"): a heading,
    /// the achievement's name and the title it lets the hero wear. Its own overlay canvas above the game and the menus, in
    /// unscaled time (it shows over a pause, a results screen and a rewind alike), one at a time, a queue behind it. Presentation only.
    /// </summary>
    public sealed class AchievementToast : MonoBehaviour
    {
        const float DropSeconds = 0.35f;
        const float HoldSeconds = 3.6f;
        const float LiftSeconds = 0.3f;
        const float CardWidth = 920f;
        const float CardHeight = 150f;

        static AchievementToast instance;

        /// <summary>Toasts shown since the game started, and the last one's achievement id (the playtest reads them).</summary>
        public static int Shown { get; private set; }
        public static string LastId { get; private set; } = "";
        public static string LastName { get; private set; } = "";
        /// <summary>A toast is on screen right now.</summary>
        public static bool Visible => instance != null && instance.active != null;
        /// <summary>The card's text while it shows (heading, name, title lines).</summary>
        public static string VisibleText => Visible ? instance.heading.text + "|" + instance.nameText.text + "|" + instance.titleText.text : "";
        /// <summary>The card's rectangle in screen pixels (the playtest checks it stays on screen).</summary>
        public static Rect ScreenRect
        {
            get
            {
                if (!Visible)
                    return Rect.zero;
                var corners = new Vector3[4];
                instance.card.GetWorldCorners(corners);
                return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
            }
        }

        RectTransform card;
        CanvasGroup group;
        TextMeshProUGUI heading, nameText, titleText;
        readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
        AchievementDef active;
        float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            instance = null;
            Shown = 0;
            LastId = LastName = "";
            Achievements.Unlocked -= OnUnlocked;
            Achievements.Unlocked += OnUnlocked;
        }

        static void OnUnlocked(AchievementDef def)
        {
            if (instance == null)
                instance = Create();
            instance.queue.Enqueue(def);
        }

        static AchievementToast Create()
        {
            var go = new GameObject("AchievementToast", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var toast = go.AddComponent<AchievementToast>();
            toast.Build();
            return toast;
        }

        void Build()
        {
            card = MenuGraphics.NewRect("Card", transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var back = card.gameObject.AddComponent<Image>();
            back.color = new Color(0.08f, 0.04f, 0.1f, 0.94f);
            back.raycastTarget = false;
            var rim = MenuGraphics.NewImage(card, "Rim", new Color(1f, 0.72f, 0.2f, 1f));
            rim.raycastTarget = false;
            var rimRect = rim.rectTransform;
            rimRect.anchorMin = new Vector2(0f, 0f);
            rimRect.anchorMax = new Vector2(0f, 1f);
            rimRect.pivot = new Vector2(0f, 0.5f);
            rimRect.sizeDelta = new Vector2(12f, 0f);

            heading = Line("Heading", 0.72f, 30f, new Color(1f, 0.72f, 0.2f, 1f), FontStyles.Bold);
            nameText = Line("Name", 0.42f, 50f, Color.white, FontStyles.Bold);
            titleText = Line("Title", 0.12f, 30f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Italic);
        }

        TextMeshProUGUI Line(string name, float y, float size, Color color, FontStyles style)
        {
            var slot = MenuGraphics.NewRect(name, card);
            slot.anchorMin = new Vector2(0f, y - 0.18f);
            slot.anchorMax = new Vector2(1f, y + 0.18f);
            slot.offsetMin = new Vector2(40f, 0f);
            slot.offsetMax = new Vector2(-24f, 0f);
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(slot, false);
            MenuGraphics.Fill(t.rectTransform);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.5f;
            t.fontSizeMax = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (active == null)
            {
                if (queue.Count == 0)
                    return;
                Show(queue.Dequeue(), now);
            }

            float t = now - startedAt;
            float total = DropSeconds + HoldSeconds + LiftSeconds;
            if (t >= total)
            {
                active = null;
                group.alpha = 0f;
                return;
            }
            // Drops in from above the screen, holds, lifts away. Kept inside the safe area at the top.
            float rest = -24f - (Screen.height - Screen.safeArea.yMax) / Mathf.Max(0.01f, transform.lossyScale.y);
            float away = CardHeight + 40f;
            float y;
            if (t < DropSeconds)
                y = Mathf.Lerp(away, rest, Ease(t / DropSeconds));
            else if (t < DropSeconds + HoldSeconds)
                y = rest;
            else
                y = Mathf.Lerp(rest, away, Ease((t - DropSeconds - HoldSeconds) / LiftSeconds));
            card.anchoredPosition = new Vector2(0f, y);
            group.alpha = t < DropSeconds ? Mathf.Clamp01(t / (DropSeconds * 0.5f)) : t > DropSeconds + HoldSeconds ? 1f - (t - DropSeconds - HoldSeconds) / LiftSeconds : 1f;
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void Show(AchievementDef def, float now)
        {
            var config = GameDefinition.Instance != null && GameDefinition.Instance.codex != null ? GameDefinition.Instance.codex : new CodexConfig();
            active = def;
            startedAt = now;
            heading.text = config.toastHeading;
            nameText.text = def.name;
            titleText.text = string.IsNullOrEmpty(def.title) ? def.description : string.Format(CultureInfo.InvariantCulture, config.toastTitleFormat, def.title);
            Shown++;
            LastId = def.id;
            LastName = def.name;
            Sfx.Play(Sfx.Events.gradeReveal);
        }
    }
}
