using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The aim guide: a short, thin, see-through laser line from the hero's muzzle along each shot his next volley
    /// fires (one per shot with a spread Grudge, the extra ones fainter). The directions come from
    /// PlayerController.TryGetAimGuide, which aims through the same AimVolley that fires, so the line is where the
    /// bullet goes. Drawn under everything in the gameplay band (player.json aimGuide.sortingOrder), so it never hides a
    /// bullet or a hazard; hidden while a scene has the hero (input locked, walked, pulled), during a rewind, the pause,
    /// the Grudge picker and the results, and once he is dead. Presentation only: it reads input, never changes it.
    /// </summary>
    public sealed class AimGuide : MonoBehaviour
    {
        const int TextureWidth = 64, TextureHeight = 4;
        static Sprite lineSprite;

        GameplayWorld world;
        PlayerController controller;
        PlayerHealth health;
        AimGuideConfig config;
        readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();
        readonly List<Vector2> aim = new List<Vector2>();
        readonly List<Vector2> drawn = new List<Vector2>();
        readonly List<Vector2> drawnBefore = new List<Vector2>();
        int volleysSeen;

        /// <summary>
        /// A volley left the gun this frame: the guide's lines as drawn on that frame (empty if it was hidden), the
        /// frame before, and the shots' flight directions read back from the projectiles. The playtest compares them.
        /// </summary>
        public static event System.Action<AimGuide, IReadOnlyList<Vector2>, IReadOnlyList<Vector2>, IReadOnlyList<Vector2>> VolleyFired;

        /// <summary>True while at least one line is on screen.</summary>
        public bool Visible { get; private set; }
        /// <summary>Where the lines start (the muzzle) this frame.</summary>
        public Vector2 Muzzle { get; private set; }
        /// <summary>The direction each line points, read back from the drawn lines, in firing order (empty while hidden).</summary>
        public IReadOnlyList<Vector2> Directions => drawn;
        /// <summary>World length of a line this frame.</summary>
        public float Length { get; private set; }
        /// <summary>Sorting layer and order the lines draw at (the playtest compares them with bullets and hazards).</summary>
        public string SortingLayerName => SpriteLayer.Player.ToString();
        public int SortingOrder => config != null ? config.sortingOrder : -1;

        public static AimGuide Create(GameplayWorld world, PlayerController controller, PlayerHealth health)
        {
            var cfg = GameDefinition.Instance != null ? GameDefinition.Instance.player.aimGuide : null;
            if (controller == null || cfg == null || !cfg.enabled)
                return null;
            var go = new GameObject("AimGuide");
            // A child of the world, so it lives in the level's scene and goes with it.
            go.transform.SetParent(world.transform, false);
            var guide = go.AddComponent<AimGuide>();
            guide.world = world;
            guide.controller = controller;
            guide.health = health;
            guide.config = cfg;
            return guide;
        }

        void LateUpdate()
        {
            drawnBefore.Clear();
            drawnBefore.AddRange(drawn);
            Draw();
            if (controller != null && controller.VolleysFired != volleysSeen)
            {
                volleysSeen = controller.VolleysFired;
                VolleyFired?.Invoke(this, drawn, drawnBefore, controller.LastVolley);
            }
        }

        void Draw()
        {
            drawn.Clear();
            // Stored in a bool, the && chain no longer proves muzzle is set (CS0165): give it a value first.
            Vector2 muzzle = default;
            bool show = ShouldShow() && controller.TryGetAimGuide(aim, out muzzle) && aim.Count > 0;
            Visible = show;
            if (!show)
            {
                for (int i = 0; i < lines.Count; i++)
                    lines[i].enabled = false;
                return;
            }

            Muzzle = muzzle;
            float shortSide = ShortSide();
            Length = config.length * shortSide;
            float width = Mathf.Max(0.005f, config.width * shortSide);
            var sprite = LineSprite;
            Vector2 native = sprite.bounds.size;
            int centre = aim.Count % 2 == 1 ? aim.Count / 2 : -1;
            float z = controller.transform.position.z + SpriteLayers.Depth(SpriteLayer.Player);
            while (lines.Count < aim.Count)
                lines.Add(NewLine(lines.Count));
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (i >= aim.Count)
                {
                    line.enabled = false;
                    continue;
                }
                Vector2 d = aim[i].normalized;
                var t = line.transform;
                t.position = new Vector3(muzzle.x, muzzle.y, z);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(Length / native.x, width / native.y, 1f);
                var c = config.color;
                c.a = aim.Count == 1 || i == centre ? config.alpha : config.extraShotAlpha;
                line.color = c;
                line.sortingOrder = config.sortingOrder;
                line.enabled = true;
                drawn.Add(t.right);
            }
        }

        bool ShouldShow()
        {
            if (world == null || controller == null || !controller.isActiveAndEnabled)
                return false;
            if (health != null && !health.IsAlive)
                return false;
            var clock = GameClock.Instance;
            if (clock != null && (clock.IsRewinding || clock.IsPaused))
                return false;
            var hud = world.Hud;
            return hud == null || !(hud.Paused || hud.PickerOpen || hud.ResultsOpen);
        }

        float ShortSide()
        {
            var cam = world.Camera != null ? world.Camera : Camera.main;
            if (cam == null || !cam.orthographic)
                return 10f;
            float height = cam.orthographicSize * 2f;
            return Mathf.Min(height, height * cam.aspect);
        }

        SpriteRenderer NewLine(int index)
        {
            var go = new GameObject("Line" + index);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LineSprite;
            sr.sortingLayerName = SpriteLayer.Player.ToString();
            sr.sortingOrder = config.sortingOrder;
            sr.enabled = false;
            return sr;
        }

        /// <summary>One unit long from its pivot at the muzzle end: solid for the first half, then fading out to the tip.</summary>
        static Sprite LineSprite
        {
            get
            {
                if (lineSprite != null)
                    return lineSprite;
                var tex = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
                {
                    name = "AimGuideLine",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color[TextureWidth * TextureHeight];
                for (int x = 0; x < TextureWidth; x++)
                {
                    float u = (x + 0.5f) / TextureWidth;
                    float a = u < 0.5f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.5f) / 0.5f);
                    for (int y = 0; y < TextureHeight; y++)
                    {
                        // Soft top and bottom rows so the thin line doesn't shimmer.
                        float edge = y == 0 || y == TextureHeight - 1 ? 0.5f : 1f;
                        pixels[y * TextureWidth + x] = new Color(1f, 1f, 1f, a * edge);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                lineSprite = Sprite.Create(tex, new Rect(0, 0, TextureWidth, TextureHeight), new Vector2(0f, 0.5f), TextureWidth);
                lineSprite.hideFlags = HideFlags.DontSave;
                return lineSprite;
            }
        }
    }
}
