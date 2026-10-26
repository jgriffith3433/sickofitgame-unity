using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The visible swing of a melee weapon: the weapon's painted swoosh (melee.swingArt, Tools/Art/fx/swoosh.json) sweeping in front
    /// of the hero, tinted with the weapon's color, drawn from the weapons' saved swing timers and the game clock, so a rewind plays
    /// a punch backward. The fist or the whip itself is in the hero's own attack clips (his grip's Punch/Whip states). A weapon
    /// without swing art falls back to a crescent made at runtime. Presentation only.
    /// </summary>
    public sealed class MeleeSwingFx : MonoBehaviour
    {
        const int Size = 128;
        const float ArcHalfDegrees = 60f;
        static Sprite crescent, fist;

        GameplayWorld world;
        SpriteRenderer arcRenderer;
        SpriteRenderer fistRenderer;

        /// <summary>True while a swing is on screen (the playtest checks that a punch is seen).</summary>
        public bool Visible { get; private set; }
        /// <summary>The most intense the swing drew since it was created (0 to 1), for the playtest.</summary>
        public float PeakAlpha { get; private set; }
        /// <summary>Where the fist is in the world this frame.</summary>
        public Vector2 FistPosition { get; private set; }

        public static MeleeSwingFx Create(GameplayWorld world)
        {
            var go = new GameObject("MeleeSwing");
            go.transform.SetParent(world.transform, false);
            var fx = go.AddComponent<MeleeSwingFx>();
            fx.world = world;
            fx.arcRenderer = fx.NewRenderer("Arc", Crescent, 9);
            fx.fistRenderer = fx.NewRenderer("Fist", Fist, 10);
            return fx;
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = SpriteLayer.Player.ToString();
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        void LateUpdate()
        {
            Visible = false;
            arcRenderer.enabled = false;
            fistRenderer.enabled = false;
            var weapons = world != null ? world.Weapons : null;
            var def = weapons?.CurrentDef;
            var step = weapons?.SwingStepDef;
            if (def == null || step == null || def.kind != WeaponKind.Melee || world.Player == null)
                return;
            float now = world.Now;
            if (!weapons.Swinging(now))
                return;
            var health = world.Player.GetComponentInChildren<PlayerHealth>();
            if (health != null && !health.IsAlive)
                return;

            float windup = weapons.SwingWindupProgress(now);
            float whole = weapons.SwingProgress(now);
            Vector2 dir = weapons.SwingDirection;
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float sign = dir.x >= 0f ? 1f : -1f;
            float half = Mathf.Max(10f, step.arc) * 0.5f;
            float eased = 1f - (1f - windup) * (1f - windup);
            float angle = baseAngle + sign * Mathf.Lerp(half, -half, eased);
            Vector2 origin = weapons.SwingOrigin;
            float z = world.Player.transform.position.z + SpriteLayers.Depth(SpriteLayer.Player);
            float reach = Mathf.Max(0.2f, step.reach);
            float scale = Mathf.Max(0.5f, step.size);
            // Bright while the blow is on its way, fading out through the recovery.
            float alpha = windup < 1f ? Mathf.Lerp(0.35f, 1f, windup) : Mathf.Clamp01(1f - (whole - 0.2f) * 1.3f);
            PeakAlpha = Mathf.Max(PeakAlpha, alpha);

            var color = def.color;
            arcRenderer.sprite = def.melee.swingArt != null ? def.melee.swingArt : Crescent;
            var t = arcRenderer.transform;
            t.position = new Vector3(origin.x, origin.y, z);
            t.rotation = Quaternion.Euler(0f, 0f, baseAngle);
            t.localScale = new Vector3(reach * (0.9f + 0.1f * scale), reach * sign * (0.9f + 0.1f * scale) * Mathf.Clamp(step.arc / 90f, 0.6f, 1.6f), 1f);
            color.a = alpha * 0.7f;
            arcRenderer.color = color;
            arcRenderer.enabled = true;

            float radians = angle * Mathf.Deg2Rad;
            FistPosition = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (reach * 0.88f);
            var ft = fistRenderer.transform;
            ft.position = new Vector3(FistPosition.x, FistPosition.y, z);
            ft.rotation = Quaternion.Euler(0f, 0f, angle);
            float fistSize = 0.34f * scale;
            ft.localScale = new Vector3(fistSize, fistSize, 1f);
            var fistColor = Color.Lerp(color, Color.white, 0.55f);
            fistColor.a = Mathf.Clamp01(alpha * 1.2f);
            fistRenderer.color = fistColor;
            // The flying disc only stands in when there is no painted swing: the hero's own clip shows the blow.
            fistRenderer.enabled = def.melee.swingArt == null;
            Visible = true;
        }

        /// <summary>A crescent facing +x, radius 1 world unit at scale 1 (pivot at the hero), thickest in the middle.</summary>
        static Sprite Crescent
        {
            get
            {
                if (crescent != null)
                    return crescent;
                var tex = NewTexture("MeleeCrescent");
                var pixels = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f - Size * 0.5f) / (Size * 0.5f);
                        float v = (y + 0.5f - Size * 0.5f) / (Size * 0.5f);
                        float r = Mathf.Sqrt(u * u + v * v);
                        float theta = Mathf.Abs(Mathf.Atan2(v, u)) * Mathf.Rad2Deg;
                        float a = 0f;
                        if (theta < ArcHalfDegrees && r < 1f)
                        {
                            float k = Mathf.Cos(theta / ArcHalfDegrees * Mathf.PI * 0.5f);
                            float inner = 1f - 0.34f * k;
                            float outerEdge = Mathf.SmoothStep(0f, 1f, (1f - r) / 0.05f);
                            float innerEdge = Mathf.SmoothStep(0f, 1f, (r - inner) / 0.12f);
                            a = outerEdge * innerEdge * k;
                        }
                        pixels[y * Size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                crescent = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size * 0.5f);
                crescent.hideFlags = HideFlags.DontSave;
                return crescent;
            }
        }

        /// <summary>A round fist: a soft disc with a dark knuckle line, 1 world unit across at scale 1.</summary>
        static Sprite Fist
        {
            get
            {
                if (fist != null)
                    return fist;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    name = "MeleeFist",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f - n * 0.5f) / (n * 0.5f);
                        float v = (y + 0.5f - n * 0.5f) / (n * 0.5f);
                        float r = Mathf.Sqrt(u * u + v * v);
                        float disc = Mathf.SmoothStep(0f, 1f, (1f - r) / 0.12f);
                        // Dark rim and one knuckle line across the front.
                        float shade = 1f - 0.45f * Mathf.SmoothStep(0.62f, 0.95f, r);
                        if (Mathf.Abs(u - 0.3f) < 0.05f && Mathf.Abs(v) < 0.55f)
                            shade *= 0.6f;
                        pixels[y * n + x] = new Color(shade, shade, shade, disc);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                fist = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                fist.hideFlags = HideFlags.DontSave;
                return fist;
            }
        }

        static Texture2D NewTexture(string name) => new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
    }
}
