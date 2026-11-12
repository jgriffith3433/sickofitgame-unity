using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Playtest/debug log of every shot and hazard telegraph's lead time (Docs/Story.md "Readability is fairness").
    /// Presentation only. The playtest turns the overlay on; players never see it on a public page.
    /// </summary>
    public static class TelegraphLog
    {
        public struct Entry
        {
            public string Kind;
            public string Subject;
            public float LeadSeconds;
            public float At;
            public int Level;
        }

        static readonly List<Entry> entries = new List<Entry>(128);
        static readonly List<Entry> recent = new List<Entry>(16);
        static GameObject overlayRoot;
        static TextMeshProUGUI overlayText;
        static bool overlayOn;

        /// <summary>Every lead recorded since the level opened (the playtest reads this).</summary>
        public static IReadOnlyList<Entry> Entries => entries;
        /// <summary>Last few leads drawn on the overlay.</summary>
        public static IReadOnlyList<Entry> Recent => recent;
        public static bool OverlayEnabled
        {
            get => overlayOn;
            set
            {
                overlayOn = value;
                if (!value)
                    HideOverlay();
            }
        }

        public static void Clear()
        {
            entries.Clear();
            recent.Clear();
            RefreshOverlay();
        }

        /// <summary>Log one telegraph. Kind is "shot", "spread", "lava", "geyser", "sink", "hazard". Lead is seconds of warning before it can hurt or fire.</summary>
        public static void Record(string kind, string subject, float leadSeconds, GameplayWorld world = null)
        {
            if (leadSeconds < 0f)
                return;
            int level = world != null ? world.LevelNumber : (GameplayWorld.Instance != null ? GameplayWorld.Instance.LevelNumber : 0);
            float at = GameClock.Instance != null ? GameClock.Instance.Time : Time.unscaledTime;
            var e = new Entry { Kind = kind ?? "", Subject = subject ?? "", LeadSeconds = leadSeconds, At = at, Level = level };
            entries.Add(e);
            recent.Add(e);
            while (recent.Count > 8)
                recent.RemoveAt(0);
            world?.Telemetry?.Record("telegraph", subject, Mathf.RoundToInt(leadSeconds * 1000f), $"{kind} lead {leadSeconds:0.00}s");
            if (overlayOn)
                EnsureOverlay();
            RefreshOverlay();
        }

        static void EnsureOverlay()
        {
            if (overlayRoot != null)
                return;
            overlayRoot = new GameObject("TelegraphDebugOverlay");
            Object.DontDestroyOnLoad(overlayRoot);
            var canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            overlayRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayRoot.AddComponent<GraphicRaycaster>();
            var go = new GameObject("Log");
            go.transform.SetParent(overlayRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(12f, -12f);
            rt.sizeDelta = new Vector2(420f, 220f);
            overlayText = go.AddComponent<TextMeshProUGUI>();
            overlayText.fontSize = 16f;
            overlayText.color = new Color(0.85f, 1f, 0.95f, 0.92f);
            overlayText.alignment = TextAlignmentOptions.TopLeft;
            overlayText.enableWordWrapping = true;
            overlayText.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.02f, 0.05f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        static void HideOverlay()
        {
            if (overlayRoot != null)
                Object.Destroy(overlayRoot);
            overlayRoot = null;
            overlayText = null;
        }

        static void RefreshOverlay()
        {
            if (!overlayOn || overlayText == null)
                return;
            if (recent.Count == 0)
            {
                overlayText.text = "telegraph lead times\n(waiting…)";
                return;
            }
            var sb = new System.Text.StringBuilder(256);
            sb.Append("telegraph lead times\n");
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                var e = recent[i];
                sb.Append(e.Kind).Append(' ').Append(e.Subject).Append(' ')
                    .Append(e.LeadSeconds.ToString("0.00")).Append("s L").Append(e.Level).Append('\n');
            }
            overlayText.text = sb.ToString();
        }
    }
}
