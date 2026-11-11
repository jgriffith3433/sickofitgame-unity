using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Input;
using SickOfIt.Core.UI.Menus;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The chase, seen in play (Docs/Story.md "The Overlord on the far ledge", docs/world-bible.md "The chase"): when the hero
    /// rewinds past a death (the rewind starts while the undo window is open), the Overlord's silhouette, who was standing
    /// over the spot where he fell, hauls himself up and out of the top of the screen, red eyes last. He is always one step
    /// ahead, and rewinding is how the hero catches him at it. All presentation: real time, nothing saved, two Images and
    /// one one-shot sound; a rewind that does not start from a death shows nothing.
    /// </summary>
    public sealed class OverlordEscape : MonoBehaviour
    {
        const float Rise = 2.4f;
        static readonly Color Ink = new Color(0.04f, 0.02f, 0.07f, 0.9f);

        RectTransform root, body;
        Image figure, eyeL, eyeR;
        bool wasRewinding, dyingBefore;
        float t = -1f;

        /// <summary>Death-rewinds that showed him (the playtest reads these).</summary>
        public static int Escapes { get; private set; }
        public static bool Visible { get; private set; }
        /// <summary>0 at the bottom of his climb, 1 gone off the top; -1 when not showing.</summary>
        public static float Progress { get; private set; } = -1f;
        /// <summary>The on-screen size of his silhouette in pixels while shown (width, height).</summary>
        public static Vector2 SizePx { get; private set; }

        public static OverlordEscape Create(Transform hud, int siblingIndex)
        {
            var go = new GameObject("OverlordEscape", typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var fx = go.AddComponent<OverlordEscape>();
            fx.Build();
            go.transform.SetSiblingIndex(siblingIndex);
            return fx;
        }

        void Build()
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            figure = MenuGraphics.NewImage(root, "Silhouette", Ink);
            figure.sprite = FigureSprite();
            figure.preserveAspect = true;
            body = figure.rectTransform;
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0f);
            body.pivot = new Vector2(0.5f, 0f);
            eyeL = MenuGraphics.NewImage(body, "EyeL", new Color(1f, 0.15f, 0.1f, 1f));
            eyeR = MenuGraphics.NewImage(body, "EyeR", new Color(1f, 0.15f, 0.1f, 1f));
            foreach (var e in new[] { eyeL, eyeR })
            {
                var r = e.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.78f);
            }
            root.gameObject.SetActive(false);
        }

        static Sprite sprite;
        /// <summary>A titan from behind: two long horns, a head, broad shoulders and a cloak dropping off the bottom.</summary>
        static Sprite FigureSprite()
        {
            if (sprite != null)
                return sprite;
            const int w = 96, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "OverlordEscape" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = (x - w * 0.5f) / (w * 0.5f), fy = y / (float)h; // fx -1..1, fy 0 bottom..1 top
                    bool on;
                    float headY = 0.74f;
                    float head = (fx * fx) / 0.07f + (fy - headY) * (fy - headY) / 0.012f;
                    on = head < 1f;
                    // shoulders and cloak: widest at the shoulders (fy 0.6), narrowing toward the neck
                    float shoulder = fy < 0.62f ? 0.95f - 0.05f * (0.62f - fy) : Mathf.Lerp(0.95f, 0.22f, (fy - 0.62f) / 0.1f);
                    if (fy < 0.72f && Mathf.Abs(fx) < Mathf.Max(0.22f, shoulder)) on = true;
                    // horns: two curved spikes rising from the head
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float hy = (fy - 0.82f) / 0.18f;
                        if (hy > 0f && hy < 1f)
                        {
                            float cx = side * (0.28f + 0.22f * hy * hy);
                            if (Mathf.Abs(fx - cx) < 0.09f * (1f - hy)) on = true;
                        }
                    }
                    px[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        }

        void Update()
        {
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            if (rewinding && !wasRewinding && t < 0f && dyingBefore)
            {
                t = 0f;
                Escapes++;
                Sfx.Play("overlord_beam_charge", 0.35f, 1.5f);
            }
            wasRewinding = rewinding;
            // Read while time runs forward: the death window is cancelled as the rewind restores his hearts.
            if (!rewinding)
                dyingBefore = HeroIsDying();

            if (t < 0f)
            {
                if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
                Visible = false;
                Progress = -1f;
                return;
            }

            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Rise);
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            Visible = true;
            Progress = p;

            float h = root.rect.height, w = root.rect.width;
            float size = Mathf.Min(h * 0.55f, w * 0.9f);
            body.sizeDelta = new Vector2(size * 0.75f, size);
            SizePx = body.sizeDelta;
            // He starts half in view at the bottom third and climbs, shrinking a little with the distance, out of the top.
            float eased = p * p * (3f - 2f * p);
            body.anchoredPosition = new Vector2(Mathf.Sin(p * 5f) * size * 0.04f, Mathf.Lerp(h * 0.12f, h * 1.05f, eased));
            float fade = Mathf.Clamp01((1f - p) * 4f) * Mathf.Clamp01(t * 6f);
            var c = Ink; c.a *= fade; figure.color = c;
            float blink = p < 0.25f ? 0f : 1f; // the eyes open as he turns away
            foreach (var e in new[] { eyeL, eyeR })
            {
                var r = e.rectTransform;
                r.sizeDelta = new Vector2(size * 0.07f, size * 0.03f);
                r.anchoredPosition = new Vector2((e == eyeL ? -1f : 1f) * size * 0.06f, 0f);
                e.color = new Color(1f, 0.15f, 0.1f, blink * fade);
            }
            if (p >= 1f)
                t = -1f;
        }

        static bool HeroIsDying()
        {
            var hero = PlayerController.Active;
            if (hero == null) return false;
            var health = hero.GetComponentInChildren<PlayerHealth>() ?? hero.GetComponentInParent<PlayerHealth>();
            return health != null && health.DeathTimeLeft >= 0f;
        }

        void OnDestroy()
        {
            Visible = false;
            Progress = -1f;
        }
    }
}
