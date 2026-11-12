using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Hostile-shot readability (Docs/Story.md "Readability is fairness"): every enemy bullet wears a white-hot core,
    /// a dark ink outline, a cyan/magenta halo (a hue no Ember Wastes / Meadow backdrop uses) and a short fading trail.
    /// Accessibility toggles still add the heavier outlines and the one-colour high-contrast shots on top.
    /// Hitboxes are untouched; on a phone the drawn sprite is scaled so it stays at least 8 CSS px across.
    /// Presentation only.
    /// </summary>
    public sealed class BulletReadability : MonoBehaviour
    {
        const float MinPhonePx = 8f;
        const int TrailPoints = 6;

        static readonly Color HotColor = new Color(1f, 0.93f, 0.1f, 1f);
        static readonly Color InkRim = new Color(0.05f, 0.02f, 0.08f, 1f);
        static readonly Color LightRim = new Color(1f, 0.97f, 0.85f, 1f);
        static readonly Color CoreWhite = new Color(1f, 1f, 0.97f, 1f);
        static readonly Color HaloCyan = new Color(0.25f, 0.95f, 1f, 0.72f);
        static readonly Color HaloMagenta = new Color(1f, 0.3f, 0.95f, 0.72f);
        static readonly Color PlayerWarm = new Color(1f, 0.72f, 0.28f, 1f);
        static readonly Color PlayerCore = new Color(1f, 0.95f, 0.75f, 1f);
        static Sprite orbSprite;

        Entity entity;
        SpriteRenderer body, rim, outer, core, halo;
        Transform rimTransform, outerTransform, coreTransform, haloTransform;
        LineRenderer trail;
        readonly Vector3[] trailBuf = new Vector3[TrailPoints];
        int trailCount;
        float trailAge;
        bool playerShot;
        Color haloColor = HaloCyan;

        /// <summary>The rim is on screen (the playtest reads this).</summary>
        public bool RimShown => rim != null && rim.enabled;
        /// <summary>The baseline's two rims: the pale/level colour next to the shot and an ink one outside it (reads on bright and on dark).</summary>
        public bool OuterShown => outer != null && outer.enabled;
        /// <summary>White-hot core drawn over the body.</summary>
        public bool CoreShown => core != null && core.enabled;
        /// <summary>Coloured halo (cyan/magenta for enemies, none for the hero's warm shots).</summary>
        public bool HaloShown => halo != null && halo.enabled;
        /// <summary>Fading trail behind the shot.</summary>
        public bool TrailShown => trail != null && trail.enabled && trailCount >= 2;
        public Color RimColor => rim != null ? rim.color : default;
        public Color OuterColor => outer != null ? outer.color : default;
        public Color HaloColor => halo != null ? halo.color : default;
        public SpriteRenderer Body => body;
        /// <summary>On-screen diameter in CSS/screen pixels (the phone criterion reads this).</summary>
        public float ScreenPixels { get; private set; }
        /// <summary>The bullet is drawn in the high-contrast colour.</summary>
        public bool HighContrast { get; private set; }
        public bool IsPlayerShot => playerShot;

        public static void Attach(Entity shot)
        {
            if (shot == null || shot.FindAbility<ProjectileAbility>() == null)
                return;
            bool enemy = shot.Team == Team.Enemy;
            bool hero = shot.Team == Team.Player;
            if (!enemy && !hero)
                return;
            if (!shot.TryGetComponent<BulletReadability>(out var look))
            {
                look = shot.gameObject.AddComponent<BulletReadability>();
                look.entity = shot;
            }
            look.playerShot = hero;
            look.haloColor = (shot.Serial & 1) == 0 ? HaloCyan : HaloMagenta;
            // Magenta on Floor 997's dark slate (story light), cyan elsewhere so it never matches sulphur/furnace orange.
            if (shot.World != null && shot.World.Stage != null && shot.World.Stage.Floor == 997)
                look.haloColor = HaloMagenta;
        }

        void LateUpdate()
        {
            bool outlines = GameSettings.BulletOutlines, contrast = GameSettings.HighContrastBullets;
            HighContrast = contrast;
            if (entity == null || !entity.IsAlive)
            {
                HideExtras();
                return;
            }
            if (body == null)
            {
                body = entity.Sprite != null && entity.Sprite.sprite != null ? entity.Sprite : FindChildBody();
                if (body == null)
                    return;
            }

            if (playerShot)
            {
                DrawPlayerLook(contrast);
                return;
            }

            // The baseline (fairness): every hostile shot wears a dark outline, a coloured halo, a white core and a trail.
            bool baseline = !outlines && !contrast;
            if (baseline && entity.World == null)
                return;

            EnsureEnemyParts();
            if (contrast)
                body.color = new Color(HotColor.r, HotColor.g, HotColor.b, body.color.a);

            // Dark outline (outer) + pale/level rim (inner) — keep the old readability contract.
            rim.enabled = true;
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            rim.color = baseline ? entity.World.ShotRim : contrast || !outlines ? InkRim : LightRim;
            CopyPose(rimTransform, 1.28f);

            if (baseline || outlines)
            {
                outer.enabled = true;
                outer.sprite = body.sprite;
                outer.flipX = body.flipX;
                outer.flipY = body.flipY;
                outer.sortingLayerID = body.sortingLayerID;
                outer.sortingOrder = body.sortingOrder - 2;
                outer.color = new Color(InkRim.r, InkRim.g, InkRim.b, 0.92f);
                CopyPose(outerTransform, 1.58f);
            }
            else if (outer.enabled)
                outer.enabled = false;

            // Coloured halo behind the ink rim (cyan/magenta family).
            halo.enabled = true;
            halo.sprite = Orb();
            halo.sortingLayerID = body.sortingLayerID;
            halo.sortingOrder = body.sortingOrder - 3;
            float pulse = 0.75f + 0.25f * Mathf.Sin((entity.Now + entity.Serial * 0.13f) * 9f);
            halo.color = new Color(haloColor.r, haloColor.g, haloColor.b, haloColor.a * pulse);
            float haloScale = BodyWorldDiameter() * 2.4f / OrbWorldSize();
            haloTransform.position = body.bounds.center;
            haloTransform.rotation = Quaternion.identity;
            haloTransform.localScale = Vector3.one * haloScale;

            // White-hot core on top of the body.
            core.enabled = true;
            core.sprite = Orb();
            core.sortingLayerID = body.sortingLayerID;
            core.sortingOrder = body.sortingOrder + 1;
            core.color = CoreWhite;
            float coreScale = BodyWorldDiameter() * 0.45f / OrbWorldSize();
            coreTransform.position = body.bounds.center;
            coreTransform.rotation = Quaternion.identity;
            coreTransform.localScale = Vector3.one * coreScale;

            UpdateTrail();
            EnforceMinScreenSize();
        }

        void DrawPlayerLook(bool contrast)
        {
            EnsureEnemyParts();
            // Warm amber + soft white core; elongated silhouette so it never reads as an enemy orb.
            if (halo != null) halo.enabled = false;
            rim.enabled = true;
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            rim.color = new Color(0.45f, 0.18f, 0.05f, 0.95f);
            CopyPose(rimTransform, 1.35f);
            outer.enabled = true;
            outer.sprite = body.sprite;
            outer.flipX = body.flipX;
            outer.flipY = body.flipY;
            outer.sortingLayerID = body.sortingLayerID;
            outer.sortingOrder = body.sortingOrder - 2;
            outer.color = new Color(InkRim.r, InkRim.g, InkRim.b, 0.8f);
            CopyPose(outerTransform, 1.55f);
            core.enabled = true;
            core.sprite = Orb();
            core.sortingLayerID = body.sortingLayerID;
            core.sortingOrder = body.sortingOrder + 1;
            core.color = contrast ? HotColor : PlayerCore;
            float coreScale = BodyWorldDiameter() * 0.4f / OrbWorldSize();
            coreTransform.position = body.bounds.center;
            coreTransform.localScale = new Vector3(coreScale * 0.7f, coreScale * 1.35f, 1f);
            body.color = new Color(PlayerWarm.r, PlayerWarm.g, PlayerWarm.b, body.color.a);
            UpdateTrail();
            EnforceMinScreenSize();
        }

        void UpdateTrail()
        {
            if (trail == null)
                return;
            var clock = SickOfIt.Core.GameClock.Instance;
            if (clock != null && clock.IsRewinding)
            {
                trail.enabled = false;
                trailCount = 0;
                return;
            }
            Vector3 p = body.bounds.center;
            trailAge += Time.unscaledDeltaTime;
            if (trailCount == 0 || trailAge >= 0.03f)
            {
                trailAge = 0f;
                if (trailCount < TrailPoints)
                {
                    trailBuf[trailCount++] = p;
                }
                else
                {
                    for (int i = 1; i < TrailPoints; i++)
                        trailBuf[i - 1] = trailBuf[i];
                    trailBuf[TrailPoints - 1] = p;
                }
            }
            else if (trailCount > 0)
                trailBuf[trailCount - 1] = p;

            if (trailCount < 2)
            {
                trail.enabled = false;
                return;
            }
            trail.enabled = true;
            trail.positionCount = trailCount;
            for (int i = 0; i < trailCount; i++)
                trail.SetPosition(i, trailBuf[i]);
            Color head = playerShot ? PlayerWarm : haloColor;
            trail.startColor = new Color(head.r, head.g, head.b, 0f);
            trail.endColor = new Color(head.r, head.g, head.b, playerShot ? 0.55f : 0.7f);
            float w = Mathf.Max(0.04f, BodyWorldDiameter() * 0.35f);
            trail.startWidth = w * 0.2f;
            trail.endWidth = w;
        }

        void EnforceMinScreenSize()
        {
            var cam = entity.World != null ? entity.World.Camera : null;
            if (cam == null || body == null)
            {
                ScreenPixels = 0f;
                return;
            }
            float worldD = BodyWorldDiameter();
            Vector3 a = cam.WorldToScreenPoint(body.bounds.center);
            Vector3 b = cam.WorldToScreenPoint(body.bounds.center + Vector3.right * worldD);
            float px = Mathf.Abs(b.x - a.x);
            ScreenPixels = px;
            // Only grow the drawn layers, never the collider / body hitbox scale.
            if (px > 0.01f && px < MinPhonePx && SickOfIt.Core.Device.TouchDevice.IsPrimary)
            {
                float boost = MinPhonePx / px;
                if (halo != null && halo.enabled)
                    haloTransform.localScale *= boost;
                if (core != null && core.enabled)
                    coreTransform.localScale *= boost;
                if (rim != null && rim.enabled)
                    rimTransform.localScale *= Mathf.Lerp(1f, boost, 0.5f);
                if (outer != null && outer.enabled)
                    outerTransform.localScale *= Mathf.Lerp(1f, boost, 0.5f);
                ScreenPixels = MinPhonePx;
            }
        }

        void EnsureEnemyParts()
        {
            if (rim == null)
            {
                var go = new GameObject("Rim");
                rimTransform = go.transform;
                rimTransform.SetParent(transform, false);
                rim = go.AddComponent<SpriteRenderer>();
            }
            if (outer == null)
            {
                var og = new GameObject("RimOuter");
                outerTransform = og.transform;
                outerTransform.SetParent(transform, false);
                outer = og.AddComponent<SpriteRenderer>();
            }
            if (halo == null)
            {
                var hg = new GameObject("Halo");
                haloTransform = hg.transform;
                haloTransform.SetParent(transform, false);
                halo = hg.AddComponent<SpriteRenderer>();
            }
            if (core == null)
            {
                var cg = new GameObject("Core");
                coreTransform = cg.transform;
                coreTransform.SetParent(transform, false);
                core = cg.AddComponent<SpriteRenderer>();
            }
            if (trail == null)
            {
                var tg = new GameObject("Trail");
                tg.transform.SetParent(transform, false);
                trail = tg.AddComponent<LineRenderer>();
                trail.positionCount = 0;
                trail.useWorldSpace = true;
                trail.numCapVertices = 2;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.sortingLayerName = body != null ? body.sortingLayerName : SpriteLayer.Player.ToString();
                trail.sortingOrder = body != null ? body.sortingOrder - 4 : 0;
                trail.enabled = false;
            }
        }

        void CopyPose(Transform t, float scale)
        {
            t.localPosition = body.transform == transform ? Vector3.zero : body.transform.localPosition;
            t.localRotation = body.transform == transform ? Quaternion.identity : body.transform.localRotation;
            t.localScale = (body.transform == transform ? Vector3.one : body.transform.localScale) * scale;
        }

        float BodyWorldDiameter()
        {
            if (body == null) return 0.2f;
            var s = body.bounds.size;
            return Mathf.Max(0.05f, Mathf.Max(s.x, s.y));
        }

        static float OrbWorldSize() => 1f; // sprite pixels-per-unit = n, so bounds size is 1.

        void HideExtras()
        {
            if (rim != null) rim.enabled = false;
            if (outer != null) outer.enabled = false;
            if (core != null) core.enabled = false;
            if (halo != null) halo.enabled = false;
            if (trail != null) trail.enabled = false;
        }

        SpriteRenderer FindChildBody()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r != rim && r != outer && r != core && r != halo && r.sprite != null)
                    return r;
            }
            return null;
        }

        static Sprite Orb()
        {
            if (orbSprite != null)
                return orbSprite;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                if (d > 1f) px[y * n + x] = Color.clear;
                else if (d < 0.4f) px[y * n + x] = Color.white;
                else px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.15f - d));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            orbSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return orbSprite;
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
            // Gameplay cannot name SickOfIt.Core.Progression (Progression references Gameplay); GameplayWorld carries the level number.
            bool want = GameSettings.HitboxDotFor(GameplayWorld.Instance != null ? GameplayWorld.Instance.LevelNumber : 99, SickOfIt.Core.Device.TouchDevice.IsPrimary) && controller != null && controller.BodyCollider != null && controller.BodyCollider.enabled;
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
