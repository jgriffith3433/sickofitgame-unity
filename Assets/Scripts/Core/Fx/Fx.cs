using UnityEngine;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Fx
{
    /// <summary>
    /// Small cosmetic effects in the world: flashes, bursts of bits, dust puffs. Pooled sprites updated by one
    /// runner per scene. Visual only (never touches the simulation), so it doesn't need to rewind; it runs on
    /// game time, so pausing freezes it.
    /// </summary>
    public static class Fx
    {
        /// <summary>A circle that pops out and fades (muzzle flash, hit, death pop).</summary>
        public static void Flash(Vector2 at, Color color, float size, float life = 0.12f)
        {
            var runner = FxRunner.Get();
            if (runner != null)
                runner.Spawn(at, Vector2.zero, color, size * 0.4f, size, life, 0f, MenuShape.Circle);
        }

        /// <summary>Bits flying out in all directions and falling (death, explosion, splat).</summary>
        public static void Burst(Vector2 at, Color color, int count, float speed = 4f, float size = 0.12f, float life = 0.45f)
        {
            var runner = FxRunner.Get();
            if (runner == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (speed * Random.Range(0.45f, 1f));
                float s = size * Random.Range(0.6f, 1.2f);
                runner.Spawn(at, velocity, color, s, s * 0.2f, life * Random.Range(0.7f, 1.1f), 9f, MenuShape.Circle);
            }
        }

        /// <summary>A star-shaped spark (shots hitting, sparks off the gun).</summary>
        public static void Spark(Vector2 at, Color color, float size = 0.3f)
        {
            var runner = FxRunner.Get();
            if (runner != null)
                runner.Spawn(at, Vector2.zero, color, size, size * 0.2f, 0.1f, 0f, MenuShape.Burst, Random.Range(0f, 360f));
        }

        /// <summary>
        /// Plays sprite frames once where something was and then removes them (an enemy's "Die" clip after it is
        /// gone from the simulation). The last frame fades out over fadeTime.
        /// </summary>
        public static void Flipbook(Sprite[] frames, float fps, Vector3 position, Vector3 scale, bool flipX, Color tint, SpriteLayer layer,
            int sortingOrder, float fadeTime = 0.25f, float punch = 0f)
        {
            var runner = FxRunner.Get();
            if (runner != null && frames != null && frames.Length > 0)
                runner.PlayFlipbook(frames, fps, position, scale, flipX, tint, layer, sortingOrder, fadeTime, punch);
        }

        /// <summary>A puff of dust at the feet (jump, land).</summary>
        public static void Dust(Vector2 feet, float strength = 1f)
        {
            var runner = FxRunner.Get();
            if (runner == null)
                return;
            var color = new Color(0.92f, 0.86f, 0.72f, 0.75f);
            int count = Mathf.RoundToInt(4 + 4 * strength);
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var velocity = new Vector2(side * Random.Range(0.8f, 2.2f) * strength, Random.Range(0.2f, 0.9f));
                float s = Random.Range(0.08f, 0.16f) * (0.8f + strength * 0.4f);
                runner.Spawn(feet, velocity, color, s, s * 1.8f, Random.Range(0.25f, 0.4f), -1.5f, MenuShape.Circle);
            }
        }
    }

    /// <summary>Owns the pooled effect sprites for the current scene.</summary>
    public sealed class FxRunner : MonoBehaviour
    {
        const int Capacity = 256;

        static FxRunner instance;

        struct Bit
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public Color Color;
            public float StartSize, EndSize, Life, Age, Gravity;
            public bool Active;
        }

        sealed class Book
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public float Fps, Age, Fade, Punch;
            public Vector3 Scale, Position;
            public Color Tint;
        }

        readonly Bit[] bits = new Bit[Capacity];
        readonly System.Collections.Generic.List<Book> books = new System.Collections.Generic.List<Book>();
        int next;

        public static FxRunner Get()
        {
            if (instance != null)
                return instance;
            if (!Application.isPlaying)
                return null;
            instance = new GameObject("[Fx]").AddComponent<FxRunner>();
            return instance;
        }

        public void Spawn(Vector2 at, Vector2 velocity, Color color, float startSize, float endSize, float life, float gravity,
            MenuShape shape, float rotation = 0f)
        {
            ref var bit = ref bits[next];
            next = (next + 1) % Capacity;
            if (bit.Renderer == null)
            {
                var go = new GameObject("Bit");
                go.transform.SetParent(transform, false);
                bit.Renderer = go.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(bit.Renderer, SpriteLayer.Player, 400);
            }
            bit.Renderer.sprite = MenuGraphics.Shape(shape, 0f);
            bit.Renderer.transform.position = new Vector3(at.x, at.y, -0.4f);
            bit.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            bit.Renderer.gameObject.SetActive(true);
            bit.Velocity = velocity;
            bit.Color = color;
            bit.StartSize = startSize;
            bit.EndSize = endSize;
            bit.Life = Mathf.Max(0.02f, life);
            bit.Age = 0f;
            bit.Gravity = gravity;
            bit.Active = true;
            Apply(ref bit);
        }

        public void PlayFlipbook(Sprite[] frames, float fps, Vector3 position, Vector3 scale, bool flipX, Color tint, SpriteLayer layer, int sortingOrder,
            float fade, float punch = 0f)
        {
            Book book = null;
            foreach (var b in books)
            {
                if (!b.Renderer.gameObject.activeSelf)
                {
                    book = b;
                    break;
                }
            }
            if (book == null)
            {
                var go = new GameObject("Flipbook");
                go.transform.SetParent(transform, false);
                book = new Book { Renderer = go.AddComponent<SpriteRenderer>() };
                books.Add(book);
            }
            SpriteLayers.Apply(book.Renderer, layer, sortingOrder);
            book.Frames = frames;
            book.Fps = Mathf.Max(0.01f, fps);
            book.Age = 0f;
            book.Fade = Mathf.Max(0f, fade);
            book.Tint = tint;
            book.Punch = punch;
            book.Scale = scale;
            book.Position = position;
            book.Renderer.flipX = flipX;
            book.Renderer.sprite = frames[0];
            book.Renderer.color = tint;
            book.Renderer.transform.position = position;
            book.Renderer.transform.localScale = scale;
            book.Renderer.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var book in books)
            {
                if (!book.Renderer.gameObject.activeSelf)
                    continue;
                book.Age += dt;
                float length = book.Frames.Length / book.Fps;
                if (book.Age >= length + book.Fade)
                {
                    book.Renderer.gameObject.SetActive(false);
                    continue;
                }
                book.Renderer.sprite = book.Frames[Mathf.Min(book.Frames.Length - 1, Mathf.FloorToInt(book.Age * book.Fps))];
                var c = book.Tint;
                if (book.Age > length)
                    c.a *= 1f - (book.Age - length) / Mathf.Max(0.001f, book.Fade);
                book.Renderer.color = c;
                if (book.Punch > 0f)
                {
                    // Squash and stretch: a quick tall-and-thin pop, then it shrinks and drifts up as it fades.
                    const float popTime = 0.18f;
                    float sx = 1f, sy = 1f, rise = 0f;
                    if (book.Age < popTime)
                    {
                        float k = Mathf.Sin(book.Age / popTime * Mathf.PI) * book.Punch;
                        sx = 1f - 0.5f * k;
                        sy = 1f + k;
                    }
                    if (book.Age > length)
                    {
                        float f = Mathf.Clamp01((book.Age - length) / Mathf.Max(0.001f, book.Fade));
                        sx = sy = 1f - 0.35f * f;
                        rise = 0.25f * f * book.Scale.y;
                    }
                    book.Renderer.transform.localScale = new Vector3(book.Scale.x * sx, book.Scale.y * sy, book.Scale.z);
                    book.Renderer.transform.position = book.Position + new Vector3(0f, rise, 0f);
                }
            }
            for (int i = 0; i < bits.Length; i++)
            {
                ref var bit = ref bits[i];
                if (!bit.Active)
                    continue;
                bit.Age += dt;
                if (bit.Age >= bit.Life)
                {
                    bit.Active = false;
                    bit.Renderer.gameObject.SetActive(false);
                    continue;
                }
                bit.Velocity.y -= bit.Gravity * dt;
                bit.Velocity *= 1f - Mathf.Clamp01(2.5f * dt);
                bit.Renderer.transform.position += (Vector3)(bit.Velocity * dt);
                Apply(ref bit);
            }
        }

        static void Apply(ref Bit bit)
        {
            float t = bit.Age / bit.Life;
            // 'size' is in world units; shape sprites differ in size, so scale by the sprite's own width.
            float size = Mathf.Lerp(bit.StartSize, bit.EndSize, 1f - (1f - t) * (1f - t));
            float width = bit.Renderer.sprite != null ? Mathf.Max(0.01f, bit.Renderer.sprite.bounds.size.x) : 1f;
            float scale = size / width;
            bit.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            var c = bit.Color;
            c.a *= 1f - t * t;
            bit.Renderer.color = c;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
