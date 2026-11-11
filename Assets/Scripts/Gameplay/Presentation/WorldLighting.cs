using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Device;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Floor lighting (Docs/lighting.md): a multiply darkness layer, additive local lights (lava, torches, hero rim,
    /// bullet glows), and a camera grade pass. Phone tier (touch + short side) halves bloom, drops grain and caps lights
    /// at 8. Presentation only — never touches the simulation or rewind state.
    /// </summary>
    public sealed class WorldLighting : MonoBehaviour
    {
        const string TorchNeedle = "torch";
        const string LampNeedle = "lamp";
        const string LanternNeedle = "lantern";

        GameplayWorld world;
        LevelLightingProfile profile;
        LightingQuality quality;
        LevelGradePass grade;
        SpriteRenderer darkness;
        SpriteRenderer gradeWash;
        Material multiplyMat;
        Material glowMat;
        Sprite softDisc;
        Sprite solidQuad;
        Transform lightRoot;
        readonly List<LightSlot> slots = new List<LightSlot>();
        SpriteRenderer rim;
        Transform rimTransform;
        bool phoneTier;
        bool raid;
        float lightReach = 2f;
        int maxLights = 24;
        int lightCount;
        Color liveGrade;
        float liveStrength;

        /// <summary>Active profile id (meadow, floor1000, ...).</summary>
        public string ProfileId => profile != null ? profile.id : "";
        /// <summary>Dominant grade colour after raid lerp.</summary>
        public Color GradeColor => liveGrade;
        public float GradeStrength => liveStrength;
        public bool PhoneTier => phoneTier;
        public int MaxLights => maxLights;
        public int LightsDrawn => lightCount;
        public bool DarknessShown => darkness != null && darkness.enabled;
        public bool RimShown => rim != null && rim.enabled;
        public bool GradeReady => (grade != null && grade.Ready) || (gradeWash != null && gradeWash.enabled);
        public float BloomIntensity => grade != null ? grade.BloomIntensity : 0f;
        public bool GrainOn => grade != null && grade.GrainOn;
        public bool HalfResBloom => grade != null && grade.HalfResBloom;
        /// <summary>Phone quality asked for half-res bloom (true even if the grade shader is missing).</summary>
        public bool WantsHalfResBloom => quality != null && quality.bloomHalfRes;
        public float LightReach => lightReach;
        public static WorldLighting Current { get; private set; }

        struct LightSlot
        {
            public SpriteRenderer Renderer;
            public Transform Follow;
            public Vector2 Offset;
            public Color Color;
            public float Size;
            public float Alpha;
            public float Flicker;
            public float FlickerSpeed;
            public float Phase;
            public string Kind;
            public bool HeroRim;
        }

        public static WorldLighting Create(GameplayWorld owner)
        {
            if (owner == null)
                return null;
            var cfg = GameDefinition.Instance != null ? GameDefinition.Instance.lighting : null;
            if (cfg == null || !cfg.enabled)
                return null;
            var profile = ProfileFor(cfg, owner.LevelNumber);
            if (profile == null)
                return null;

            var go = new GameObject("WorldLighting");
            go.transform.SetParent(owner.transform, false);
            var lighting = go.AddComponent<WorldLighting>();
            lighting.Build(owner, cfg, profile);
            Current = lighting;
            return lighting;
        }

        public static LevelLightingProfile ProfileFor(LightingConfig cfg, int levelNumber)
        {
            if (cfg == null || cfg.profiles == null)
                return null;
            for (int i = 0; i < cfg.profiles.Count; i++)
            {
                var p = cfg.profiles[i];
                if (p == null || p.levels == null)
                    continue;
                for (int j = 0; j < p.levels.Count; j++)
                {
                    if (p.levels[j] == levelNumber)
                        return p;
                }
            }
            return null;
        }

        public static bool IsPhoneTier(LightingConfig cfg)
        {
            // Touch primary + short side (390x844 phones), or a playtest that forced the touch UI on a phone device.
            bool touch = TouchDevice.IsPrimary || GameplayHud.TouchUiForced;
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float limit = cfg != null && cfg.quality != null ? cfg.quality.phoneShortSide : 500f;
            if (!touch)
                return false;
            if (GameplayHud.TouchUiForced && shortSide <= limit)
                return true;
            return TouchDevice.IsPrimary && shortSide <= limit;
        }

        void Build(GameplayWorld owner, LightingConfig cfg, LevelLightingProfile prof)
        {
            world = owner;
            profile = prof;
            lightReach = Mathf.Max(0.5f, cfg.lightReach);
            phoneTier = IsPhoneTier(cfg);
            quality = phoneTier
                ? (cfg.quality != null ? cfg.quality.phone : new LightingQuality { bloomHalfRes = true, grain = false, maxLights = 8 })
                : (cfg.quality != null ? cfg.quality.desktop : new LightingQuality());
            maxLights = Mathf.Clamp(quality.maxLights, 1, 64);

            var glowShader = Resources.Load<Shader>("Feel/SpriteGlow");
            if (glowShader != null && glowShader.isSupported)
                glowMat = new Material(glowShader) { name = "WorldLightGlow (runtime)", hideFlags = HideFlags.DontSave };
            var mulShader = Resources.Load<Shader>("Feel/SpriteMultiply");
            if (mulShader != null && mulShader.isSupported)
                multiplyMat = new Material(mulShader) { name = "WorldDarkness (runtime)", hideFlags = HideFlags.DontSave };
            softDisc = MakeDisc();
            solidQuad = MakeSolid();

            lightRoot = new GameObject("Lights").transform;
            lightRoot.SetParent(transform, false);

            BuildDarkness();
            BuildGradeWash();
            if (world.Camera != null)
            {
                grade = LevelGradePass.Attach(world.Camera);
                if (grade != null)
                    grade.Apply(profile, quality);
            }
            ApplyGradeState(false);
        }

        void BuildGradeWash()
        {
            // Soft screen tint so the floor's grade still reads when the LevelGrade shader is missing (LFS not smudged).
            var go = new GameObject("GradeWash");
            go.transform.SetParent(transform, false);
            gradeWash = go.AddComponent<SpriteRenderer>();
            gradeWash.sprite = solidQuad != null ? solidQuad : softDisc;
            SpriteLayers.Apply(gradeWash, SpriteLayer.Player, 80);
        }

        void BuildDarkness()
        {
            var go = new GameObject("Darkness");
            go.transform.SetParent(transform, false);
            darkness = go.AddComponent<SpriteRenderer>();
            darkness.sprite = solidQuad != null ? solidQuad : softDisc;
            if (multiplyMat != null)
                darkness.sharedMaterial = multiplyMat;
            SpriteLayers.Apply(darkness, SpriteLayer.Player, -40);
            ApplyDarknessColor(profile.darknessAlpha);
        }

        void ApplyDarknessColor(float alpha)
        {
            if (darkness == null || profile == null)
                return;
            var d = profile.darkness;
            alpha = Mathf.Clamp01(alpha);
            if (multiplyMat != null)
            {
                // Multiply: lerp white→darkness by strength so local additive lights punch through.
                darkness.color = Color.Lerp(Color.white, new Color(d.r, d.g, d.b, 1f), alpha);
            }
            else
            {
                // Alpha-blend fallback when the multiply shader is missing (LFS not smudged).
                darkness.color = new Color(d.r, d.g, d.b, alpha * 0.85f);
            }
            darkness.enabled = alpha > 0.01f;
        }

        void ApplyGradeState(bool forceRaid)
        {
            if (profile == null)
                return;
            raid = forceRaid || (profile.id == "meadow" && AmbienceBed.Current != null
                && AmbienceBed.Current.IndexOf("burning", System.StringComparison.OrdinalIgnoreCase) >= 0);
            liveGrade = raid ? profile.raidGrade : profile.grade;
            liveStrength = raid ? profile.raidGradeStrength : profile.gradeStrength;
            if (grade != null && grade.Ready)
            {
                grade.Apply(profile, quality);
                grade.SetGrade(liveGrade, liveStrength);
            }
            if (gradeWash != null)
            {
                // When the camera pass is live, keep the wash faint; otherwise it carries the whole grade.
                float wash = grade != null && grade.Ready ? liveStrength * 0.2f : liveStrength * 0.55f;
                var c = liveGrade;
                c.a = Mathf.Clamp01(wash);
                gradeWash.color = c;
                gradeWash.enabled = wash > 0.02f;
            }
            ApplyDarknessColor(raid ? Mathf.Min(0.55f, profile.darknessAlpha + 0.12f) : profile.darknessAlpha);
        }

        void LateUpdate()
        {
            if (world == null || profile == null)
                return;
            // Grade stays put while rewinding so it doesn't pop; lights still follow restored places.
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            if (!rewinding)
            {
                bool wantRaid = profile.id == "meadow" && AmbienceBed.Current != null
                    && AmbienceBed.Current.IndexOf("burning", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (wantRaid != raid)
                    ApplyGradeState(wantRaid);
            }

            FitDarknessToCamera();
            RebuildLights();
            UpdateRim();
            DrawLights();
        }

        void FitDarknessToCamera()
        {
            if (darkness == null || world.Camera == null || !world.Camera.orthographic)
                return;
            var cam = world.Camera;
            float h = cam.orthographicSize * 2.2f;
            float w = h * cam.aspect;
            var t = darkness.transform;
            t.position = new Vector3(cam.transform.position.x, cam.transform.position.y, SpriteLayers.Depth(SpriteLayer.Player) + 0.4f);
            t.localScale = new Vector3(w, h, 1f);
            if (gradeWash != null)
            {
                var g = gradeWash.transform;
                g.position = new Vector3(cam.transform.position.x, cam.transform.position.y, SpriteLayers.Depth(SpriteLayer.Player) - 0.5f);
                g.localScale = new Vector3(w, h, 1f);
            }
        }

        void RebuildLights()
        {
            // Clear follow targets; slots are reused by DrawLights.
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                s.Follow = null;
                s.HeroRim = false;
                slots[i] = s;
            }
            int n = 0;

            // Hero rim counts as one light.
            if (world.Player != null && world.Player.IsAlive && n < maxLights)
            {
                EnsureSlot(n);
                var s = slots[n];
                s.Follow = world.Player.transform;
                s.Offset = Vector2.zero;
                s.Color = profile.rim;
                s.Size = lightReach * 1.1f;
                s.Alpha = profile.rim.a;
                s.Flicker = 0.05f;
                s.FlickerSpeed = 2.5f;
                s.Kind = "rim";
                s.HeroRim = true;
                slots[n++] = s;
            }

            if (world.Live != null)
            {
                foreach (var e in world.Live)
                {
                    if (n >= maxLights || e == null || !e.IsAlive)
                        continue;
                    string id = e.DefId ?? "";
                    bool lava = id.IndexOf("lava", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || (e.Tags != null && e.Tags.Contains("lava"));
                    bool torch = ContainsIgnore(id, TorchNeedle) || ContainsIgnore(id, LampNeedle) || ContainsIgnore(id, LanternNeedle)
                        || (e.Tags != null && (e.Tags.Contains("torch") || e.Tags.Contains("lamp")));
                    bool bullet = e.Team == Team.Enemy && e.FindAbility<ProjectileAbility>() != null;

                    if (lava)
                    {
                        EnsureSlot(n);
                        var s = slots[n];
                        s.Follow = e.transform;
                        s.Offset = Vector2.up * 0.4f;
                        s.Color = profile.lavaLight;
                        s.Size = lightReach * 2f;
                        s.Alpha = 0.55f;
                        s.Flicker = profile.lavaFlicker;
                        s.FlickerSpeed = 2.8f;
                        s.Kind = "lava";
                        slots[n++] = s;
                    }
                    else if (torch)
                    {
                        EnsureSlot(n);
                        var s = slots[n];
                        s.Follow = e.transform;
                        s.Offset = Vector2.up * 0.2f;
                        s.Color = profile.torchLight;
                        s.Size = lightReach * 1.6f;
                        s.Alpha = 0.5f;
                        s.Flicker = profile.torchFlicker;
                        s.FlickerSpeed = 7f;
                        s.Kind = "torch";
                        slots[n++] = s;
                    }
                    else if (bullet && n < maxLights)
                    {
                        EnsureSlot(n);
                        var s = slots[n];
                        s.Follow = e.transform;
                        s.Offset = Vector2.zero;
                        s.Color = profile.bulletLight;
                        s.Size = 0.7f;
                        s.Alpha = 0.4f;
                        s.Flicker = 0.1f;
                        s.FlickerSpeed = 14f;
                        s.Kind = "bullet";
                        slots[n++] = s;
                    }
                }
            }

            // Meadow warm pool when calm (no lava): a soft dusk lamp near the hero so the grade isn't flat.
            if (profile.id == "meadow" && !raid && world.Player != null && n < maxLights)
            {
                EnsureSlot(n);
                var s = slots[n];
                s.Follow = world.Player.transform;
                s.Offset = new Vector2(0f, 1.2f);
                s.Color = profile.torchLight;
                s.Size = lightReach * 2.4f;
                s.Alpha = 0.28f;
                s.Flicker = 0.08f;
                s.FlickerSpeed = 1.6f;
                s.Kind = "dusk";
                slots[n++] = s;
            }

            lightCount = n;
            for (int i = n; i < slots.Count; i++)
            {
                if (slots[i].Renderer != null)
                    slots[i].Renderer.enabled = false;
            }
        }

        static bool ContainsIgnore(string hay, string needle) =>
            !string.IsNullOrEmpty(hay) && hay.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        void EnsureSlot(int index)
        {
            while (slots.Count <= index)
            {
                var go = new GameObject("Light");
                go.transform.SetParent(lightRoot, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = softDisc;
                if (glowMat != null)
                    r.sharedMaterial = glowMat;
                SpriteLayers.Apply(r, SpriteLayer.Player, -30);
                r.enabled = false;
                slots.Add(new LightSlot
                {
                    Renderer = r,
                    Phase = (slots.Count * 0.73f) % 6.28f,
                });
            }
        }

        void DrawLights()
        {
            float time = Time.time;
            for (int i = 0; i < lightCount; i++)
            {
                var s = slots[i];
                if (s.Renderer == null || s.Follow == null)
                {
                    if (s.Renderer != null)
                        s.Renderer.enabled = false;
                    continue;
                }
                Vector2 at = (Vector2)s.Follow.position + s.Offset;
                float a = s.Alpha;
                if (s.Flicker > 0f)
                {
                    float w = 0.5f + 0.25f * Mathf.Sin(time * s.FlickerSpeed + s.Phase)
                        + 0.25f * Mathf.Sin(time * s.FlickerSpeed * 2.1f + s.Phase * 1.7f);
                    a *= 1f - s.Flicker * w;
                }
                var t = s.Renderer.transform;
                t.position = new Vector3(at.x, at.y, SpriteLayers.Depth(SpriteLayer.Player) + 0.35f);
                float size = Mathf.Max(0.05f, s.Size);
                t.localScale = new Vector3(size, size, 1f);
                var c = s.Color;
                c.a = Mathf.Clamp01(a);
                s.Renderer.color = c;
                s.Renderer.enabled = true;
                slots[i] = s;
            }
        }

        void UpdateRim()
        {
            // A slightly larger duplicate behind the hero so he separates from any backdrop (acceptance: rim light).
            var player = world != null ? world.Player : null;
            var body = player != null && player.IsAlive ? player.Sprite : null;
            if (body == null || body.sprite == null)
            {
                if (rim != null)
                    rim.enabled = false;
                return;
            }
            if (rim == null)
            {
                var go = new GameObject("HeroRim");
                rimTransform = go.transform;
                rimTransform.SetParent(transform, false);
                rim = go.AddComponent<SpriteRenderer>();
                if (glowMat != null)
                    rim.sharedMaterial = glowMat;
            }
            rim.enabled = true;
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            var c = profile.rim;
            c.a = Mathf.Clamp01(profile.rim.a * (raid ? 1.15f : 1f));
            rim.color = c;
            rimTransform.position = body.transform.position + new Vector3(0f, 0.02f, -0.02f);
            rimTransform.rotation = body.transform.rotation;
            rimTransform.localScale = body.transform.lossyScale * 1.18f;
        }

        static Sprite MakeDisc()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
                name = "WorldLightDisc",
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    float a = d * d * (3f - 2f * d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static Sprite MakeSolid()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave,
                name = "WorldDarknessQuad",
            };
            var white = new Color32(255, 255, 255, 255);
            tex.SetPixels32(new[] { white, white, white, white });
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (multiplyMat != null)
                Destroy(multiplyMat);
            if (glowMat != null)
                Destroy(glowMat);
            if (softDisc != null && softDisc.texture != null)
                Destroy(softDisc.texture);
            if (solidQuad != null && solidQuad.texture != null)
                Destroy(solidQuad.texture);
        }
    }
}
