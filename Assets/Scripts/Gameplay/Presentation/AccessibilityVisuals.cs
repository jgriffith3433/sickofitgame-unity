using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Hostile-shot readability (settings > Accessibility): an ink or light rim behind every enemy shot (bullet outlines) and a single
    /// hot colour for all of them (high-contrast bullets). Added once to each pooled shot; presentation only, it reads the setting
    /// every frame and never touches the simulation.
    /// </summary>
    public sealed class BulletReadability : MonoBehaviour
    {
        static readonly Color HotColor = new Color(1f, 0.93f, 0.1f, 1f);
        static readonly Color InkRim = new Color(0.05f, 0.02f, 0.08f, 1f);
        static readonly Color LightRim = new Color(1f, 0.97f, 0.85f, 1f);

        Entity entity;
        SpriteRenderer body, rim, outer;
        Transform rimTransform, outerTransform;

        /// <summary>The rim is on screen (the playtest reads this).</summary>
        public bool RimShown => rim != null && rim.enabled;
        /// <summary>The baseline's two rims: the pale/level colour next to the shot and an ink one outside it (reads on bright and on dark).</summary>
        public bool OuterShown => outer != null && outer.enabled;
        public Color RimColor => rim != null ? rim.color : default;
        public Color OuterColor => outer != null ? outer.color : default;
        public SpriteRenderer Body => body;
        /// <summary>The bullet is drawn in the high-contrast colour.</summary>
        public bool HighContrast { get; private set; }

        public static void Attach(Entity shot)
        {
            if (shot.Team != Team.Enemy || shot.FindAbility<ProjectileAbility>() == null)
                return;
            if (!shot.TryGetComponent<BulletReadability>(out _))
                shot.gameObject.AddComponent<BulletReadability>().entity = shot;
        }

        void LateUpdate()
        {
            bool outlines = GameSettings.BulletOutlines, contrast = GameSettings.HighContrastBullets;
            HighContrast = contrast;
            // The baseline (fairness): every hostile shot wears a thin rim in its level's colour, so it reads against any background.
            bool baseline = !outlines && !contrast;
            if (baseline && (entity == null || entity.World == null))
                return;
            if (body == null)
            {
                body = entity != null && entity.Sprite != null && entity.Sprite.sprite != null ? entity.Sprite : FindChildBody();
                if (body == null)
                    return;
            }
            if (rim == null)
            {
                var go = new GameObject("Rim");
                rimTransform = go.transform;
                rimTransform.SetParent(transform, false);
                rim = go.AddComponent<SpriteRenderer>();
            }
            if (contrast)
                body.color = new Color(HotColor.r, HotColor.g, HotColor.b, body.color.a);
            rim.enabled = true;
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            rim.color = baseline ? entity.World.ShotRim : contrast || !outlines ? InkRim : LightRim;
            rimTransform.localPosition = body.transform == transform ? Vector3.zero : body.transform.localPosition;
            rimTransform.localRotation = body.transform == transform ? Quaternion.identity : body.transform.localRotation;
            rimTransform.localScale = (body.transform == transform ? Vector3.one : body.transform.localScale) * (baseline ? 1.25f : 1.4f);
            if (baseline)
            {
                if (outer == null)
                {
                    var og = new GameObject("RimOuter");
                    outerTransform = og.transform;
                    outerTransform.SetParent(transform, false);
                    outer = og.AddComponent<SpriteRenderer>();
                }
                outer.enabled = true;
                outer.sprite = body.sprite;
                outer.flipX = body.flipX;
                outer.flipY = body.flipY;
                outer.sortingLayerID = body.sortingLayerID;
                outer.sortingOrder = body.sortingOrder - 2;
                outer.color = new Color(InkRim.r, InkRim.g, InkRim.b, 0.85f);
                outerTransform.localPosition = rimTransform.localPosition;
                outerTransform.localRotation = rimTransform.localRotation;
                outerTransform.localScale = (body.transform == transform ? Vector3.one : body.transform.localScale) * 1.55f;
            }
            else if (outer != null && outer.enabled)
                outer.enabled = false;
        }

        SpriteRenderer FindChildBody()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r != rim && r != outer && r.sprite != null)
                    return r;
            }
            return null;
        }
    }

    /// <summary>
    /// The hero's hitbox (settings > Accessibility): a ring at the size of his body collider, which is what shots hit, with a dot in the
    /// middle to steer by. Drawn above the hero. Presentation only.
    /// </summary>
    public sealed class HitboxDot : MonoBehaviour
    {
        static Sprite sprite;

        PlayerController controller;
        SpriteRenderer renderer2d;

        public bool Shown => renderer2d != null && renderer2d.enabled;

        public static HitboxDot Attach(PlayerController controller)
        {
            if (controller == null)
                return null;
            if (controller.TryGetComponent<HitboxDot>(out var existing))
                return existing;
            var dot = controller.gameObject.AddComponent<HitboxDot>();
            dot.controller = controller;
            return dot;
        }

        void Awake()
        {
            var go = new GameObject("HitboxDot");
            go.transform.SetParent(transform, false);
            renderer2d = go.AddComponent<SpriteRenderer>();
            renderer2d.sprite = MakeSprite();
            renderer2d.sortingLayerName = SpriteLayer.Player.ToString();
            renderer2d.sortingOrder = 400;
            renderer2d.enabled = false;
        }

        void LateUpdate()
        {
            bool want = GameSettings.HitboxDot && controller != null && controller.BodyCollider != null && controller.BodyCollider.enabled;
            if (renderer2d.enabled != want)
                renderer2d.enabled = want;
            if (!want)
                return;
            var bounds = controller.BodyCollider.bounds;
            float diameter = Mathf.Min(bounds.size.x, bounds.size.y);
            var t = renderer2d.transform;
            t.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            t.rotation = Quaternion.identity;
            float unit = renderer2d.sprite.bounds.size.x;
            t.localScale = Vector3.one * (diameter / unit) / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        }

        static Sprite MakeSprite()
        {
            if (sprite != null)
                return sprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    Color c = Color.clear;
                    if (d < 0.2f) c = new Color(1f, 1f, 1f, 1f);                       // the dot
                    else if (d < 0.3f) c = new Color(0.05f, 0.02f, 0.08f, 0.9f);       // its ink rim
                    else if (d > 0.9f && d < 1f) c = new Color(1f, 1f, 1f, 0.85f);      // the body ring
                    else if (d > 0.82f && d <= 0.9f) c = new Color(0.05f, 0.02f, 0.08f, 0.6f);
                    pixels[y * n + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return sprite;
        }
    }
}
