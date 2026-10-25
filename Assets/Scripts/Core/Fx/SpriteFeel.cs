using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Fx
{
    /// <summary>
    /// game.json "feel", with its defaults when there is no definition (tests).
    /// </summary>
    public static class Feel
    {
        static readonly FeelConfig Defaults = new FeelConfig();

        public static FeelConfig Config
        {
            get
            {
                var definition = GameDefinition.Instance;
                return definition != null && definition.feel != null ? definition.feel : Defaults;
            }
        }

        /// <summary>The playtest's switch for its frame-time baseline: off, no flash, squash, kick or glow is drawn (the freeze still runs).</summary>
        public static bool Drawn = true;
    }

    /// <summary>
    /// A sprite's hit flash, squash and recoil (game.json feel.hit, feel.fire), drawn by the SickOfIt/SpriteFeel shader
    /// (Resources/Feel): while one plays, the renderer wears that material and a property block moves its vertices (squash and
    /// offset in the sprite's own space) and blends it toward the flash color. The transform, the collider and the body never
    /// move, so the simulation can't see it. Real time, so a pause or a hit-freeze doesn't strand a white sprite.
    /// </summary>
    public static class SpriteFeel
    {
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int SquashId = Shader.PropertyToID("_Squash");

        static Material material;
        static bool loaded;

        /// <summary>The shader is in the build and runs here.</summary>
        public static bool Available
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    var shader = Resources.Load<Shader>("Feel/SpriteFeel");
                    if (shader != null && shader.isSupported)
                        material = new Material(shader) { name = "SpriteFeel (runtime)", hideFlags = HideFlags.DontSave };
                    else
                        Debug.LogWarning("[SpriteFeel] Resources/Feel/SpriteFeel shader missing or unsupported: hits won't flash or squash.");
                }
                return material != null;
            }
        }

        /// <summary>Hits, squashes and kicks started (for the playtest).</summary>
        public static int Started { get; private set; }
        /// <summary>Sprites wearing the effect right now.</summary>
        public static int Playing => SpriteFeelRunner.Count;
        /// <summary>The largest squash (fraction) and offset (world units) drawn since the counter was last reset.</summary>
        public static float MaxSquashSeen, MaxOffsetSeen;

        /// <summary>
        /// Plays a flash (0..1 toward 'flash', 'flashSeconds' long) and a springy squash ('squash': how much wider (x) and taller (y)
        /// it gets at the peak, negative = narrower/shorter) with a recoil 'offset' (world units) that eases back over 'seconds'.
        /// </summary>
        public static void Play(SpriteRenderer renderer, Color flash, float flashAmount, float flashSeconds, Vector2 squash, Vector2 offset, float seconds)
        {
            if (renderer == null || !Feel.Drawn || !Application.isPlaying || !Available)
                return;
            var runner = SpriteFeelRunner.Get();
            if (runner == null)
                return;
            runner.Begin(renderer, material, flash, Mathf.Clamp01(flashAmount), Mathf.Max(0f, flashSeconds), squash, offset, Mathf.Max(0.01f, seconds));
            Started++;
        }

        internal static void Apply(SpriteRenderer renderer, MaterialPropertyBlock block, Color flash, float amount, Vector2 scale, Vector2 localOffset)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(FlashColorId, flash);
            block.SetFloat(FlashAmountId, amount);
            block.SetVector(SquashId, new Vector4(scale.x, scale.y, localOffset.x, localOffset.y));
            renderer.SetPropertyBlock(block);
        }
    }

    /// <summary>Runs the sprite effects for the current scene.</summary>
    public sealed class SpriteFeelRunner : MonoBehaviour
    {
        sealed class Entry
        {
            public SpriteRenderer Renderer;
            public Material Original;
            public Color Flash;
            public float FlashAmount, FlashSeconds, Seconds, StartedAt;
            public Vector2 Squash, Offset;
        }

        static SpriteFeelRunner instance;
        public static int Count => instance != null ? instance.entries.Count : 0;

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<SpriteRenderer, Entry> byRenderer = new Dictionary<SpriteRenderer, Entry>();
        readonly Stack<Entry> spare = new Stack<Entry>();
        MaterialPropertyBlock block;

        public static SpriteFeelRunner Get()
        {
            if (instance != null)
                return instance;
            if (!Application.isPlaying)
                return null;
            instance = new GameObject("[SpriteFeel]").AddComponent<SpriteFeelRunner>();
            return instance;
        }

        public void Begin(SpriteRenderer renderer, Material feel, Color flash, float flashAmount, float flashSeconds, Vector2 squash, Vector2 offset, float seconds)
        {
            if (block == null)
                block = new MaterialPropertyBlock();
            if (!byRenderer.TryGetValue(renderer, out var entry))
            {
                entry = spare.Count > 0 ? spare.Pop() : new Entry();
                entry.Renderer = renderer;
                entry.Original = renderer.sharedMaterial;
                entries.Add(entry);
                byRenderer[renderer] = entry;
                renderer.sharedMaterial = feel;
            }
            else
            {
                // A new hit on a sprite still springing: the stronger of the two flashes, a fresh squash.
                if (flashAmount < entry.FlashAmount * 0.5f && Time.unscaledTime - entry.StartedAt < entry.FlashSeconds)
                {
                    flash = entry.Flash;
                    flashAmount = entry.FlashAmount;
                    flashSeconds = entry.FlashSeconds;
                }
            }
            entry.Flash = flash;
            entry.FlashAmount = flashAmount;
            entry.FlashSeconds = flashSeconds;
            entry.Squash = squash;
            entry.Offset = offset;
            entry.Seconds = Mathf.Max(seconds, flashSeconds);
            entry.StartedAt = Time.unscaledTime;
            Step(entry, 0f);
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                float age = now - e.StartedAt;
                if (e.Renderer == null || !e.Renderer.gameObject.activeInHierarchy || age >= e.Seconds)
                {
                    End(i);
                    continue;
                }
                Step(e, age);
            }
        }

        void Step(Entry e, float age)
        {
            // The flash: full for its first half, then fading out.
            float amount = 0f;
            if (e.FlashSeconds > 0f && age < e.FlashSeconds)
            {
                float f = age / e.FlashSeconds;
                amount = e.FlashAmount * (f < 0.5f ? 1f : 1f - (f - 0.5f) * 2f);
            }
            // A spring: the full squash at once, a smaller stretch the other way, then still.
            float u = Mathf.Clamp01(age / e.Seconds);
            float spring = (1f - u) * Mathf.Cos(u * Mathf.PI * 1.5f);
            Vector2 scale = new Vector2(1f + e.Squash.x * spring, 1f + e.Squash.y * spring);
            float back = (1f - u) * (1f - u);
            Vector2 world = e.Offset * back;
            Vector2 local = world.sqrMagnitude > 0f ? (Vector2)e.Renderer.transform.InverseTransformVector(world) : Vector2.zero;
            SpriteFeel.Apply(e.Renderer, block, e.Flash, amount, scale, local);
            float squash = Mathf.Max(Mathf.Abs(scale.x - 1f), Mathf.Abs(scale.y - 1f));
            if (squash > SpriteFeel.MaxSquashSeen)
                SpriteFeel.MaxSquashSeen = squash;
            if (world.magnitude > SpriteFeel.MaxOffsetSeen)
                SpriteFeel.MaxOffsetSeen = world.magnitude;
        }

        void End(int index)
        {
            var e = entries[index];
            if (e.Renderer != null)
            {
                // Neutral values first (the original material ignores them), then the renderer's own material back.
                SpriteFeel.Apply(e.Renderer, block, e.Flash, 0f, Vector2.one, Vector2.zero);
                e.Renderer.sharedMaterial = e.Original;
                byRenderer.Remove(e.Renderer);
            }
            else
            {
                // Destroyed with the scene: drop whatever key is gone.
                RemoveDestroyedKeys();
            }
            int last = entries.Count - 1;
            entries[index] = entries[last];
            entries.RemoveAt(last);
            e.Renderer = null;
            e.Original = null;
            spare.Push(e);
        }

        readonly List<SpriteRenderer> deadKeys = new List<SpriteRenderer>();

        void RemoveDestroyedKeys()
        {
            deadKeys.Clear();
            foreach (var key in byRenderer.Keys)
            {
                if (key == null)
                    deadKeys.Add(key);
            }
            foreach (var key in deadKeys)
                byRenderer.Remove(key);
        }

        void OnDestroy()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.Renderer != null)
                    e.Renderer.sharedMaterial = e.Original;
            }
            entries.Clear();
            byRenderer.Clear();
            if (instance == this)
                instance = null;
        }
    }
}
