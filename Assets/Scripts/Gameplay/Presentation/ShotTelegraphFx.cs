using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Aimed-shot and spread telegraphs: a muzzle flash and a thin aim line for 0.3-0.5 s before a hostile volley,
    /// or a flash on the emitter for a spread. Presentation only; ShootAbility drives the timers and logs leads.
    /// </summary>
    public sealed class ShotTelegraphFx : MonoBehaviour
    {
        static readonly Color AimLineColor = new Color(1f, 0.35f, 0.95f, 0.85f);
        static readonly Color MuzzleFlashColor = new Color(0.55f, 1f, 1f, 0.95f);
        static readonly Color SpreadFlashColor = new Color(1f, 0.4f, 1f, 0.9f);
        static Sprite orbSprite;

        Entity entity;
        LineRenderer line;
        SpriteRenderer muzzle;
        SpriteRenderer emitterFlash;
        Vector2 aimDir = Vector2.right;
        Vector2 muzzleOffset;
        float until;
        float startedAt = -1f;
        bool spread;
        string subject;
        bool logged;

        public bool Active => until > 0f && entity != null && entity.IsAlive && GameClock.Instance != null && GameClock.Instance.Time < until;

        public static ShotTelegraphFx Attach(Entity owner)
        {
            if (owner == null)
                return null;
            if (!owner.TryGetComponent<ShotTelegraphFx>(out var fx))
            {
                fx = owner.gameObject.AddComponent<ShotTelegraphFx>();
                fx.entity = owner;
            }
            return fx;
        }

        /// <summary>Start an aimed-shot telegraph: thin line from the muzzle along 'direction' until 'fireAt'.</summary>
        public void BeginAim(Vector2 muzzleLocal, Vector2 direction, float fireAt, string who)
        {
            muzzleOffset = muzzleLocal;
            aimDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            startedAt = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            until = fireAt;
            spread = false;
            subject = who ?? (entity != null ? entity.DefId : "shot");
            logged = false;
            EnsureParts();
            if (line != null) line.enabled = true;
            if (muzzle != null) muzzle.enabled = true;
            if (emitterFlash != null) emitterFlash.enabled = false;
        }

        /// <summary>Start a spread/emitter flash telegraph (no aim line).</summary>
        public void BeginSpread(float fireAt, string who)
        {
            startedAt = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            until = fireAt;
            spread = true;
            subject = who ?? (entity != null ? entity.DefId : "spread");
            logged = false;
            EnsureParts();
            if (line != null) line.enabled = false;
            if (muzzle != null) muzzle.enabled = false;
            if (emitterFlash != null) emitterFlash.enabled = true;
        }

        /// <summary>Keep the same telegraph going until a later fireAt (does not reset the lead clock).</summary>
        public void ExtendUntil(float fireAt)
        {
            until = fireAt;
        }

        /// <summary>End the telegraph and log its lead (called when the volley actually fires).</summary>
        public float EndAndLog(string kind = null)
        {
            float lead = 0f;
            if (startedAt >= 0f && GameClock.Instance != null)
                lead = Mathf.Max(0f, GameClock.Instance.Time - startedAt);
            if (!logged && lead > 0.001f)
            {
                logged = true;
                TelegraphLog.Record(kind ?? (spread ? "spread" : "shot"), subject, lead, entity != null ? entity.World : null);
            }
            until = 0f;
            startedAt = -1f;
            if (line != null) line.enabled = false;
            if (muzzle != null) muzzle.enabled = false;
            if (emitterFlash != null) emitterFlash.enabled = false;
            return lead;
        }

        void LateUpdate()
        {
            if (entity == null || !entity.IsAlive)
            {
                Hide();
                return;
            }
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || until <= 0f || clock.Time >= until)
            {
                Hide();
                return;
            }
            EnsureParts();
            Vector2 origin = entity.Position + new Vector2(muzzleOffset.x * entity.Facing, muzzleOffset.y);
            if (spread)
            {
                if (emitterFlash != null)
                {
                    emitterFlash.enabled = true;
                    float pulse = 0.7f + 0.3f * Mathf.Sin(clock.Time * 28f);
                    emitterFlash.color = new Color(SpreadFlashColor.r, SpreadFlashColor.g, SpreadFlashColor.b, SpreadFlashColor.a * pulse);
                    emitterFlash.transform.position = new Vector3(entity.Position.x, entity.Position.y, entity.transform.position.z);
                    float s = 1.1f + 0.25f * pulse;
                    emitterFlash.transform.localScale = new Vector3(s, s, 1f);
                }
                return;
            }
            if (muzzle != null)
            {
                muzzle.enabled = true;
                float pulse = 0.65f + 0.35f * Mathf.Sin(clock.Time * 32f);
                muzzle.color = new Color(MuzzleFlashColor.r, MuzzleFlashColor.g, MuzzleFlashColor.b, MuzzleFlashColor.a * pulse);
                muzzle.transform.position = new Vector3(origin.x, origin.y, entity.transform.position.z);
                muzzle.transform.localScale = Vector3.one * (0.55f + 0.2f * pulse);
            }
            if (line != null)
            {
                line.enabled = true;
                const float len = 4.5f;
                Vector3 a = new Vector3(origin.x, origin.y, entity.transform.position.z);
                Vector3 b = a + new Vector3(aimDir.x, aimDir.y, 0f) * len;
                line.SetPosition(0, a);
                line.SetPosition(1, b);
                float alpha = 0.55f + 0.35f * Mathf.Sin(clock.Time * 22f);
                line.startColor = new Color(AimLineColor.r, AimLineColor.g, AimLineColor.b, AimLineColor.a * alpha);
                line.endColor = new Color(AimLineColor.r, AimLineColor.g, AimLineColor.b, 0f);
            }
        }

        void Hide()
        {
            if (line != null) line.enabled = false;
            if (muzzle != null) muzzle.enabled = false;
            if (emitterFlash != null) emitterFlash.enabled = false;
        }

        void EnsureParts()
        {
            if (line == null)
            {
                var go = new GameObject("AimTelegraph");
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = 0.045f;
                line.endWidth = 0.012f;
                line.numCapVertices = 2;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.sortingLayerName = SpriteLayer.Foreground1.ToString();
                line.sortingOrder = 80;
                line.enabled = false;
            }
            if (muzzle == null)
            {
                var go = new GameObject("MuzzleFlash");
                go.transform.SetParent(transform, false);
                muzzle = go.AddComponent<SpriteRenderer>();
                muzzle.sprite = OrbSprite();
                muzzle.sortingLayerName = SpriteLayer.Foreground1.ToString();
                muzzle.sortingOrder = 81;
                muzzle.enabled = false;
            }
            if (emitterFlash == null)
            {
                var go = new GameObject("EmitterFlash");
                go.transform.SetParent(transform, false);
                emitterFlash = go.AddComponent<SpriteRenderer>();
                emitterFlash.sprite = OrbSprite();
                emitterFlash.sortingLayerName = SpriteLayer.Foreground1.ToString();
                emitterFlash.sortingOrder = 79;
                emitterFlash.enabled = false;
            }
        }

        static Sprite OrbSprite()
        {
            if (orbSprite != null)
                return orbSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                if (d > 1f) px[y * n + x] = Color.clear;
                else if (d < 0.35f) px[y * n + x] = Color.white;
                else px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            orbSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return orbSprite;
        }
    }

    /// <summary>
    /// Hazard warning glow for lava rises, geysers, surges and sink tells: a cyan/magenta flash at least 0.5 s before hurt.
    /// Hooks the existing FloorKit phase telegraphs; presentation only.
    /// </summary>
    public sealed class HazardWarnFx : MonoBehaviour
    {
        static readonly Color WarnCyan = new Color(0.35f, 1f, 1f, 0.85f);
        static readonly Color WarnMagenta = new Color(1f, 0.35f, 0.95f, 0.85f);
        static Sprite orb;
        public static readonly Dictionary<int, float> LavaTellActive = new Dictionary<int, float>(16);

        Entity entity;
        SpriteRenderer glow;
        string kind;
        float warnStarted = -1f;
        bool logged;
        bool wasWarning;

        public static void Attach(Entity e)
        {
            if (e == null)
                return;
            bool hazard = e.FindAbility<LavaSeaAbility>() != null
                || e.FindAbility<GeyserAbility>() != null
                || e.FindAbility<SinkAbility>() != null
                || e.FindAbility<SurgeAbility>() != null;
            if (!hazard)
                return;
            if (!e.TryGetComponent<HazardWarnFx>(out _))
                e.gameObject.AddComponent<HazardWarnFx>().entity = e;
        }

        public static void NoteLavaTell(Entity owner, float until)
        {
            if (owner == null) return;
            LavaTellActive[owner.Serial] = until;
        }

        void LateUpdate()
        {
            if (entity == null || !entity.IsAlive)
                return;
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding)
            {
                if (glow != null) glow.enabled = false;
                return;
            }

            bool warning = false;
            string k = null;
            float expectedLead = 0.5f;

            var lava = entity.FindAbility<LavaSeaAbility>();
            if (lava != null && lava.IsWarning)
            {
                warning = true;
                k = "lava";
                expectedLead = lava.WarningLead;
            }
            var geyser = entity.FindAbility<GeyserAbility>();
            if (geyser != null && geyser.Phase == 1)
            {
                warning = true;
                k = "geyser";
                expectedLead = geyser.TellLead;
            }
            var sink = entity.FindAbility<SinkAbility>();
            if (sink != null && sink.IsTelling)
            {
                warning = true;
                k = "sink";
                expectedLead = Mathf.Max(0.5f, sink.loopTell > 0f ? sink.loopTell : 0.5f);
            }
            var surge = entity.FindAbility<SurgeAbility>();
            if (surge != null && surge.IsWarning)
            {
                warning = true;
                k = "surge";
                expectedLead = Mathf.Max(0.5f, surge.telegraph);
            }
            if (!warning && LavaTellActive.TryGetValue(entity.Serial, out float until) && clock.Time < until)
            {
                warning = true;
                k = k ?? "lava";
                expectedLead = Mathf.Max(0.5f, until - clock.Time);
            }

            if (warning && !wasWarning)
            {
                warnStarted = clock.Time;
                logged = false;
                kind = k;
            }
            if (!warning && wasWarning && !logged && warnStarted >= 0f)
            {
                float lead = clock.Time - warnStarted;
                if (lead >= 0.5f)
                    TelegraphLog.Record(kind ?? "hazard", entity.DefId, lead, entity.World);
                logged = true;
            }
            wasWarning = warning;

            if (!warning)
            {
                if (glow != null) glow.enabled = false;
                return;
            }

            EnsureGlow();
            glow.enabled = true;
            float pulse = 0.55f + 0.45f * Mathf.Sin(clock.Time * 16f);
            bool magenta = (entity.Serial & 1) == 0;
            var c = magenta ? WarnMagenta : WarnCyan;
            glow.color = new Color(c.r, c.g, c.b, c.a * pulse);
            var b = entity.MainCollider != null ? entity.MainCollider.bounds : new Bounds(entity.Position, Vector3.one);
            glow.transform.position = new Vector3(b.center.x, b.max.y + 0.15f, entity.transform.position.z);
            float w = Mathf.Max(0.8f, b.size.x * 0.9f);
            glow.transform.localScale = new Vector3(w, 0.45f + 0.1f * pulse, 1f);

            if (!logged && warnStarted >= 0f && clock.Time - warnStarted >= 0.5f)
            {
                logged = true;
                TelegraphLog.Record(kind ?? "hazard", entity.DefId, Mathf.Max(0.5f, expectedLead), entity.World);
            }
        }

        void EnsureGlow()
        {
            if (glow != null)
                return;
            var go = new GameObject("HazardWarn");
            go.transform.SetParent(transform, false);
            glow = go.AddComponent<SpriteRenderer>();
            glow.sprite = EffectAtlas.WhiteCore != null ? EffectAtlas.WhiteCore : Orb();
            glow.sortingLayerName = SpriteLayer.Foreground1.ToString();
            glow.sortingOrder = 70;
        }

        static Sprite Orb()
        {
            if (orb != null) return orb;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                px[y * n + x] = d > 1f ? Color.clear : new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * 0.85f));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            orb = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return orb;
        }
    }
}
