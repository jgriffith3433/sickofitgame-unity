using System.Text.RegularExpressions;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Core.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The stamped title card that slams in when a floor starts ("FLOOR 1000" over the floor's name), with a stamp
    /// sound and one announcer quip, then fades out by <see cref="TotalSeconds"/> while play is already live (it never
    /// blocks input and sits in its own overlay canvas inside the safe area). Presentation only: real time, nothing
    /// saved, so a rewind never replays it.
    /// </summary>
    public sealed class FloorCard : MonoBehaviour
    {
        public const float TotalSeconds = 1.5f;
        const float SlamSeconds = 0.14f;
        const float FadeFrom = 1.05f;

        static readonly Regex Title = new Regex(@"^\s*Floor\s+([\d,]+)\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase);

        /// <summary>The card on screen right now (the playtest reads these).</summary>
        public static FloorCard Current { get; private set; }
        public static int Shown { get; private set; }
        public static int LastFloor { get; private set; }
        public static string LastName { get; private set; } = "";
        public static float LastShownAt { get; private set; } = -99f;
        /// <summary>The card's box in screen pixels (0 when none), for the safe-area check.</summary>
        public static Rect ScreenRect { get; private set; }

        float born;
        CanvasGroup group;
        RectTransform stamp;
        RectTransform box;
        bool stamped;

        /// <summary>Starts a card for a level's display name ("Floor 1000: The Landing"); anything else makes none.</summary>
        public static bool TryShow(string displayName, TMP_FontAsset font, string quipId)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;
            var m = Title.Match(displayName);
            if (!m.Success)
                return false;
            if (Current != null)
                Destroy(Current.gameObject);

            var go = new GameObject("FloorCard", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3900; // over the HUD, under the screen transition
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Wide screens scale on height, tall ones on width, so the card is as big as fits in either.
            scaler.matchWidthOrHeight = Screen.width >= Screen.height ? 1f : 0f;
            var card = go.AddComponent<FloorCard>();
            card.Build(m.Groups[1].Value, m.Groups[2].Value, font);
            Current = card;
            Shown++;
            LastFloor = int.TryParse(m.Groups[1].Value.Replace(",", ""), out var n) ? n : 0;
            LastName = m.Groups[2].Value;
            LastShownAt = Time.unscaledTime;
            card.quip = quipId;
            return true;
        }

        string quip;

        void Build(string number, string name, TMP_FontAsset font)
        {
            born = Time.unscaledTime;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(transform, false);
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            // The slab: a dark plate with an ember rim, a little above the middle, 88% of the safe width at most.
            stamp = MenuGraphics.NewRect("Stamp", safe);
            stamp.anchorMin = new Vector2(0.06f, 0.5f);
            stamp.anchorMax = new Vector2(0.94f, 0.5f);
            stamp.pivot = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = new Vector2(0f, 120f);
            stamp.sizeDelta = new Vector2(0f, 320f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
            box = stamp;
            var rim = MenuGraphics.NewImage(stamp, "Rim", new Color(1f, 0.45f, 0.12f, 0.95f));
            rim.raycastTarget = false;
            MenuGraphics.Fill(rim.rectTransform);
            var plate = MenuGraphics.NewImage(stamp, "Plate", new Color(0.07f, 0.03f, 0.05f, 0.92f));
            plate.raycastTarget = false;
            MenuGraphics.Fill(plate.rectTransform, 8f);

            var num = MakeText(stamp, "Number", "FLOOR " + number, font, 150f, new Color(1f, 0.82f, 0.4f, 1f), 0.5f, 1f);
            var nm = MakeText(stamp, "Name", name.ToUpperInvariant(), font, 74f, new Color(1f, 0.93f, 0.8f, 1f), 0f, 0.5f);
            var numRect = num.rectTransform;
            numRect.anchorMin = new Vector2(0f, 0.42f);
            numRect.anchorMax = new Vector2(1f, 1f);
            numRect.offsetMin = new Vector2(24f, 0f);
            numRect.offsetMax = new Vector2(-24f, -10f);
            var nameRect = nm.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.42f);
            nameRect.offsetMin = new Vector2(24f, 10f);
            nameRect.offsetMax = new Vector2(-24f, 0f);
            group.alpha = 0f;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color, float _a, float _b)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null)
                t.font = font;
            t.text = value;
            t.alignment = TextAlignmentOptions.Midline;
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = 24f;
            t.fontSize = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        void Update()
        {
            float age = Time.unscaledTime - born;
            if (age >= TotalSeconds)
            {
                if (Current == this)
                {
                    Current = null;
                    ScreenRect = Rect.zero;
                }
                Destroy(gameObject);
                return;
            }
            if (!stamped)
            {
                stamped = true;
                Sfx.Play("floor_stamp");
                if (!string.IsNullOrEmpty(quip))
                    Sfx.Play(quip);
            }
            // Slam: from large and tilted down to the plate's size in SlamSeconds, with a short overshoot.
            float p = Mathf.Clamp01(age / SlamSeconds);
            float scale = Mathf.Lerp(2.4f, 1f, 1f - (1f - p) * (1f - p)) + (age > SlamSeconds && age < SlamSeconds + 0.12f ? 0.04f * Mathf.Sin((age - SlamSeconds) / 0.12f * Mathf.PI) : 0f);
            stamp.localScale = new Vector3(scale, scale, 1f);
            float shake = age > SlamSeconds && age < SlamSeconds + 0.18f ? (1f - (age - SlamSeconds) / 0.18f) * 10f : 0f;
            stamp.anchoredPosition = new Vector2(Mathf.Sin(age * 90f) * shake, 120f + Mathf.Cos(age * 70f) * shake);
            float fadeIn = Mathf.Clamp01(age / 0.06f);
            float fadeOut = 1f - Mathf.Clamp01((age - FadeFrom) / (TotalSeconds - FadeFrom));
            group.alpha = fadeIn * fadeOut;
            var corners = new Vector3[4];
            box.GetWorldCorners(corners);
            ScreenRect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                ScreenRect = Rect.zero;
            }
        }
    }
}
