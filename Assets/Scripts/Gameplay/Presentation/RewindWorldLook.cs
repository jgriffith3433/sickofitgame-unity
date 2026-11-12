using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Device;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// World-space rewind look (Docs/Story.md "Rewind that feels amazing"): desaturate everything but the hero and
    /// bullets to ~20% colour, leave fading ghost copies of the hero's recent places, and draw short reverse streaks
    /// on every live bullet. Presentation only — nothing saved. Created beside <see cref="RewindFx"/>.
    /// </summary>
    public sealed class RewindWorldLook : MonoBehaviour
    {
        const float SampleHz = 18f;
        const float Saturation = 0.2f;
        const float GhostAlpha = 0.55f;
        const int MaxStreaks = 28;
        const int HistoryCap = 48;

        struct Pose
        {
            public Vector3 Pos;
            public Sprite Sprite;
            public bool FlipX;
            public Color Color;
        }

        struct DesatEntry
        {
            public SpriteRenderer Renderer;
            public Color Original;
            public bool Owned;
        }

        static RewindWorldLook instance;

        readonly Pose[] history = new Pose[HistoryCap];
        int historyCount, historyHead;
        float sampleAt;
        float intensity;
        float snapLeft;
        bool wasRewinding;

        SpriteRenderer[] ghosts;
        int ghostCap;
        LineRenderer[] streaks;
        int streakCap;

        readonly List<DesatEntry> desat = new List<DesatEntry>(128);
        readonly List<SpriteRenderer> scratch = new List<SpriteRenderer>(128);
        float refreshDesatAt;
        Transform ghostRoot, streakRoot;

        // Playtest probes.
        public static float Intensity { get; private set; }
        public static float Desaturation { get; private set; }
        public static int GhostVisible { get; private set; }
        public static int BulletStreaks { get; private set; }
        public static bool SnapBackActive { get; private set; }
        public static int GhostBudget { get; private set; }

        public static RewindWorldLook Ensure()
        {
            if (instance != null)
                return instance;
            var go = new GameObject("[RewindWorldLook]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RewindWorldLook>();
            instance.Build();
            return instance;
        }

        void Build()
        {
            // Cap is refreshed in LateUpdate so a phone playtest that forces touch after boot still gets 4 ghosts.
            ghostCap = 8;
            streakCap = MaxStreaks;
            GhostBudget = ghostCap;

            ghostRoot = new GameObject("Ghosts").transform;
            ghostRoot.SetParent(transform, false);
            ghosts = new SpriteRenderer[ghostCap];
            for (int i = 0; i < ghostCap; i++)
            {
                var g = new GameObject("Ghost" + i);
                g.transform.SetParent(ghostRoot, false);
                var sr = g.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, SpriteLayer.Player, SpriteLayers.Character - 1);
                sr.color = Color.clear;
                ghosts[i] = sr;
            }

            streakRoot = new GameObject("Streaks").transform;
            streakRoot.SetParent(transform, false);
            streaks = new LineRenderer[streakCap];
            for (int i = 0; i < streakCap; i++)
            {
                var s = new GameObject("Streak" + i);
                s.transform.SetParent(streakRoot, false);
                var lr = s.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.02f;
                lr.numCapVertices = 2;
                lr.material = new Material(Shader.Find("Sprites/Default")) { name = "RewindStreak", hideFlags = HideFlags.DontSave };
                lr.startColor = lr.endColor = Color.clear;
                lr.sortingLayerName = SpriteLayer.Player.ToString();
                lr.sortingOrder = SpriteLayers.Projectile + 2;
                lr.enabled = false;
                streaks[i] = lr;
            }
        }

        void LateUpdate()
        {
            int wantGhosts = TouchDevice.IsPrimary || GameplayHud.TouchUiForced ? 4 : 8;
            int wantStreaks = TouchDevice.IsPrimary || GameplayHud.TouchUiForced ? 14 : MaxStreaks;
            GhostBudget = wantGhosts;
            // Never grow past the pooled arrays; shrinking only limits how many we draw.
            ghostCap = Mathf.Min(wantGhosts, ghosts != null ? ghosts.Length : wantGhosts);
            streakCap = Mathf.Min(wantStreaks, streaks != null ? streaks.Length : wantStreaks);

            var clock = GameClock.Instance;
            bool beatHidden = RewindFx.BeatHidden;
            bool rewinding = clock != null && clock.IsRewinding && !beatHidden;
            float dt = Time.unscaledDeltaTime;

            if (rewinding)
                intensity = Mathf.MoveTowards(intensity, 1f, dt * 9f);
            else if (wasRewinding && intensity > 0.2f)
            {
                // Release: 0.2 s colour snap-back + ripple from the hero.
                snapLeft = 0.2f;
                FeelVfx.Ensure();
                var hero = PlayerController.Active;
                Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
                FeelVfx.Play(FeelVfx.Kind.RewindRipple, at, size: 2.6f, tint: new Color(1f, 0.82f, 0.28f, 0.95f));
                intensity = Mathf.MoveTowards(intensity, 0f, dt * 8f);
            }
            else
                intensity = Mathf.MoveTowards(intensity, 0f, dt * 5f);

            if (snapLeft > 0f)
                snapLeft -= dt;

            wasRewinding = rewinding;
            Intensity = intensity;
            SnapBackActive = snapLeft > 0f;
            Desaturation = intensity * (1f - Saturation);

            SampleHero(dt);
            ApplyDesat(intensity);
            DrawGhosts(intensity);
            DrawStreaks(intensity, rewinding);
        }

        void SampleHero(float dt)
        {
            sampleAt -= dt;
            if (sampleAt > 0f)
                return;
            sampleAt = 1f / SampleHz;
            var hero = PlayerController.Active;
            var world = GameplayWorld.Instance;
            if (hero == null || world == null)
                return;
            Sprite sprite = null;
            bool flip = false;
            Color col = Color.white;
            var renderers = world.PlayerRenderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r != null && r.enabled && r.sprite != null)
                    {
                        sprite = r.sprite;
                        flip = r.flipX;
                        col = r.color;
                        break;
                    }
                }
            }
            if (sprite == null)
                return;
            history[historyHead] = new Pose
            {
                Pos = hero.transform.position,
                Sprite = sprite,
                FlipX = flip,
                Color = col,
            };
            historyHead = (historyHead + 1) % HistoryCap;
            if (historyCount < HistoryCap)
                historyCount++;
        }

        void DrawGhosts(float k)
        {
            int shown = 0;
            if (k < 0.05f || historyCount < 2)
            {
                for (int i = 0; i < ghostCap; i++)
                    if (ghosts[i] != null)
                        ghosts[i].color = Color.clear;
                GhostVisible = 0;
                return;
            }

            // Spread ghosts through the recent pose ring (oldest → newest fades up).
            int steps = Mathf.Min(ghostCap, historyCount - 1);
            for (int i = 0; i < ghostCap; i++)
            {
                var g = ghosts[i];
                if (g == null)
                    continue;
                if (i >= steps)
                {
                    g.color = Color.clear;
                    continue;
                }
                // Index into history: further back for higher i.
                int back = 2 + i * Mathf.Max(1, (historyCount - 2) / Mathf.Max(1, steps));
                int idx = historyHead - 1 - back;
                while (idx < 0) idx += HistoryCap;
                var pose = history[idx % HistoryCap];
                if (pose.Sprite == null)
                {
                    g.color = Color.clear;
                    continue;
                }
                g.sprite = pose.Sprite;
                g.flipX = pose.FlipX;
                g.transform.position = pose.Pos;
                float fade = GhostAlpha * k * (1f - i / (float)Mathf.Max(1, steps));
                if (GameSettings.ReducedFlash)
                    fade *= 0.55f;
                var c = pose.Color;
                c.a = fade;
                // Keep ghosts slightly tinted teal-gold so they read as time copies, still full chroma.
                c = Color.Lerp(c, new Color(0.85f, 0.95f, 1f, c.a), 0.25f);
                g.color = c;
                shown++;
            }
            GhostVisible = shown;
        }

        void DrawStreaks(float k, bool rewinding)
        {
            int used = 0;
            if (k < 0.05f || !rewinding)
            {
                for (int i = 0; i < streakCap; i++)
                    if (streaks[i] != null)
                        streaks[i].enabled = false;
                BulletStreaks = 0;
                return;
            }

            var world = GameplayWorld.Instance;
            if (world == null)
            {
                BulletStreaks = 0;
                return;
            }

            var live = world.Live;
            for (int i = 0; i < live.Count && used < streakCap; i++)
            {
                var e = live[i];
                if (e == null || !e.IsAlive || e.FindAbility<ProjectileAbility>() == null)
                    continue;
                Vector2 vel = e.Body != null ? e.Body.linearVelocity : Vector2.zero;
                // Reverse direction: time pouring backwards.
                Vector2 dir = vel.sqrMagnitude > 0.01f ? -vel.normalized : Vector2.left;
                float len = Mathf.Clamp(0.35f + vel.magnitude * 0.04f, 0.35f, 1.1f);
                Vector2 a = e.Position;
                Vector2 b = a + dir * len;
                var lr = streaks[used++];
                lr.enabled = true;
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                bool heroShot = e.Team == Team.Player;
                Color tip = heroShot
                    ? new Color(1f, 0.85f, 0.35f, 0.95f * k)
                    : new Color(0.55f, 1f, 1f, 0.9f * k);
                // Bright head at the bullet, fading along the reverse path.
                lr.startColor = tip;
                lr.endColor = new Color(tip.r, tip.g, tip.b, 0.05f * k);
            }
            for (int i = used; i < streakCap; i++)
                streaks[i].enabled = false;
            BulletStreaks = used;
        }

        void ApplyDesat(float k)
        {
            if (k < 0.01f)
            {
                RestoreDesat();
                return;
            }
            if (Time.unscaledTime >= refreshDesatAt)
            {
                refreshDesatAt = Time.unscaledTime + 0.4f;
                RefreshDesatList();
            }

            float keep = Mathf.Lerp(1f, Saturation, k);
            if (GameSettings.ReducedFlash)
                keep = Mathf.Lerp(1f, 0.45f, k);

            for (int i = 0; i < desat.Count; i++)
            {
                var e = desat[i];
                if (e.Renderer == null)
                    continue;
                Color o = e.Original;
                float lum = o.r * 0.299f + o.g * 0.587f + o.b * 0.114f;
                var gray = new Color(lum, lum, lum, o.a);
                e.Renderer.color = Color.Lerp(gray, o, keep);
            }
        }

        void RefreshDesatList()
        {
            // Put current colours back before rebuilding so we don't bake a half-desat as "original".
            RestoreDesat();
            desat.Clear();
            scratch.Clear();
            var world = GameplayWorld.Instance;
            if (world == null)
                return;

            // Prefer the active level scene roots; fall back to all loaded sprites.
            var cam = world.Camera;
            if (cam != null)
            {
                var scene = cam.gameObject.scene;
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                        root.GetComponentsInChildren(true, scratch);
                }
            }
            if (scratch.Count == 0)
            {
                var all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                scratch.AddRange(all);
            }

            var heroRenderers = world.PlayerRenderers;
            for (int i = 0; i < scratch.Count; i++)
            {
                var r = scratch[i];
                if (r == null || !r.enabled || r.sprite == null)
                    continue;
                if (IsHeroOrBullet(r, heroRenderers, world))
                    continue;
                // Ghost copies we own: skip.
                if (r.transform.IsChildOf(ghostRoot))
                    continue;
                desat.Add(new DesatEntry { Renderer = r, Original = r.color, Owned = true });
            }
        }

        static bool IsHeroOrBullet(SpriteRenderer r, SpriteRenderer[] heroRenderers, GameplayWorld world)
        {
            if (heroRenderers != null)
            {
                for (int i = 0; i < heroRenderers.Length; i++)
                    if (heroRenderers[i] == r)
                        return true;
            }
            // Player sorting + character/projectile orders stay saturated.
            if (r.sortingLayerName == SpriteLayer.Player.ToString())
            {
                int o = r.sortingOrder;
                if (o == SpriteLayers.Character || o == SpriteLayers.Projectile
                    || o == SpriteLayers.Projectile + 1 || o == SpriteLayers.Projectile - 1)
                {
                    // Character order is also enemies — only keep the hero's body and true projectiles.
                    var ent = r.GetComponentInParent<Entity>();
                    if (ent != null)
                    {
                        if (ent.IsPlayer)
                            return true;
                        if (ent.FindAbility<ProjectileAbility>() != null)
                            return true;
                        return false;
                    }
                }
            }
            var e = r.GetComponentInParent<Entity>();
            if (e != null && e.FindAbility<ProjectileAbility>() != null)
                return true;
            if (e != null && e.IsPlayer)
                return true;
            return false;
        }

        void RestoreDesat()
        {
            for (int i = 0; i < desat.Count; i++)
            {
                var e = desat[i];
                if (e.Renderer != null)
                    e.Renderer.color = e.Original;
            }
            desat.Clear();
        }

        void OnDestroy()
        {
            RestoreDesat();
            if (instance == this)
                instance = null;
            Intensity = 0f;
            Desaturation = 0f;
            GhostVisible = 0;
            BulletStreaks = 0;
            SnapBackActive = false;
        }
    }
}
