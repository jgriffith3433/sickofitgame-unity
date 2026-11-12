using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// What rewinding looks and sounds like (Docs/Story.md "Rewind that feels amazing"): gold time-ripple and optional
    /// scanlines only on the outer ~8% of the screen, sand/ember motes streaming backwards, and a 0.2 s colour snap-back
    /// on release. World desat, hero ghosts and bullet streaks live in <see cref="RewindWorldLook"/>. A reversed tape
    /// whirr loops with its pitch climbing. All presentation: real time, no sim state, no per-frame allocation beyond
    /// the fixed Image / particle pool. Reduced Flash halves it and stops the wobble.
    /// </summary>
    public sealed class RewindFx : MonoBehaviour
    {
        const string LoopSound = "rewind_start";
        const int Motes = 18;
        const float ScanPeriod = 6f;
        const float OuterFrac = 0.08f;
        const float PitchStart = 0.8f, PitchEnd = 1.9f, PitchRamp = 4.5f;
        static readonly Color Gold = new Color(1f, 0.78f, 0.22f, 1f);

        RectTransform root;
        RectTransform scanTop, scanBot, scanLeft, scanRight;
        Image scanTopImg, scanBotImg, scanLeftImg, scanRightImg;
        Image goldTop, goldBot, goldLeft, goldRight;
        readonly Image[] mote = new Image[Motes];
        readonly float[] moteSeed = new float[Motes];
        AudioSource source;
        float reportedPitch;
        AudioClip reversed;
        float intensity;
        float rewindingFor;
        float volume;
        float snapLeft;
        bool wasRewinding;

        // What the playtest reads: the effect's state this frame.
        public static float Intensity { get; private set; }
        public static bool Active => Intensity > 0.02f || SnapBackActive;
        public static bool SoundPlaying { get; private set; }
        public static float Pitch { get; private set; }
        public static float SoundVolume { get; private set; }
        public static int VisibleLayers { get; private set; }
        /// <summary>How much of the *outer frame* the effect tints, 0..1 (never the central playfield).</summary>
        public static float Coverage { get; private set; }
        /// <summary>Fraction of the screen covered by scan/glitch overlays that sit inside the central 84% (should stay ~0).</summary>
        public static float PlayfieldGlitchCoverage { get; private set; }
        public static bool ReversedClip { get; private set; }
        public static bool SnapBackActive { get; private set; }
        public static int SandMotesVisible { get; private set; }

        public static RewindFx Create(Transform hud, Color tint, int siblingIndex)
        {
            RewindWorldLook.Ensure();
            var go = new GameObject("RewindFx", typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var fx = go.AddComponent<RewindFx>();
            fx.Build(tint);
            go.transform.SetSiblingIndex(siblingIndex);
            return fx;
        }

        void Build(Color tint)
        {
            _ = tint; // HUD wash colour still comes from GameplayHud.rewindTint; kept for call-site compat.
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Four scanline strips + four gold ripple strips — never a full-screen overlay on the playfield.
            scanTopImg = EdgeScan(root, "ScanTop", out scanTop);
            scanBotImg = EdgeScan(root, "ScanBot", out scanBot);
            scanLeftImg = EdgeScan(root, "ScanLeft", out scanLeft);
            scanRightImg = EdgeScan(root, "ScanRight", out scanRight);
            goldTop = MenuGraphics.NewImage(root, "GoldTop", Color.clear);
            goldBot = MenuGraphics.NewImage(root, "GoldBot", Color.clear);
            goldLeft = MenuGraphics.NewImage(root, "GoldLeft", Color.clear);
            goldRight = MenuGraphics.NewImage(root, "GoldRight", Color.clear);

            for (int i = 0; i < Motes; i++)
            {
                var s = MenuGraphics.NewImage(root, "Mote" + i, Color.clear);
                var r = s.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(6f, 6f);
                mote[i] = s;
                moteSeed[i] = Hash(i * 11.17f);
            }
            root.gameObject.SetActive(false);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            source.ignoreListenerPause = false;
        }

        static Image EdgeScan(RectTransform parent, string name, out RectTransform rect)
        {
            var img = MenuGraphics.NewImage(parent, name, Color.clear);
            img.sprite = ScanSprite();
            img.type = Image.Type.Tiled;
            rect = img.rectTransform;
            return img;
        }

        static float Hash(float x) => Mathf.Repeat(Mathf.Sin(x * 12.9898f) * 43758.5453f, 1f);

        static Sprite scanSprite;
        static Sprite ScanSprite()
        {
            if (scanSprite != null)
                return scanSprite;
            int h = (int)ScanPeriod;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "RewindScan" };
            for (int y = 0; y < h; y++)
                tex.SetPixel(0, y, y < 2 ? new Color(0f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 0f));
            tex.Apply();
            return scanSprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>The loop's clip: rewind_start played backwards (a reverse whoosh), or the clip itself when it can't be read.</summary>
        AudioClip LoopClip()
        {
            if (reversed != null)
                return reversed;
            var clip = Sfx.ClipOf(LoopSound);
            if (clip == null)
                return null;
            reversed = clip;
            try
            {
                var data = new float[clip.samples * clip.channels];
                if (clip.GetData(data, 0))
                {
                    for (int f = 0, last = clip.samples - 1; f < clip.samples / 2; f++)
                        for (int c = 0; c < clip.channels; c++)
                        {
                            int a = f * clip.channels + c, b = (last - f) * clip.channels + c;
                            (data[a], data[b]) = (data[b], data[a]);
                        }
                    var copy = AudioClip.Create("rewind_loop_reversed", clip.samples, clip.channels, clip.frequency, false);
                    if (copy.SetData(data, 0))
                    {
                        reversed = copy;
                        ReversedClip = true;
                    }
                }
            }
            catch (System.Exception)
            {
                // A clip kept compressed in memory can't be read: the forward clip loops instead.
            }
            return reversed;
        }

        /// <summary>True while a beat runs: no scanlines or gold ripple over the play area.</summary>
        public static bool BeatHidden;

        void Update()
        {
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding && !BeatHidden;
            float dt = Time.unscaledDeltaTime;

            if (rewinding)
            {
                intensity = Mathf.MoveTowards(intensity, 1f, dt * 9f);
                rewindingFor += dt;
            }
            else
            {
                if (wasRewinding && intensity > 0.15f)
                    snapLeft = 0.2f;
                intensity = Mathf.MoveTowards(intensity, 0f, dt * (snapLeft > 0f ? 12f : 5f));
                rewindingFor = 0f;
            }
            if (snapLeft > 0f)
                snapLeft -= dt;

            wasRewinding = rewinding;
            Intensity = intensity;
            SnapBackActive = snapLeft > 0f;

            bool shown = intensity > 0.001f || snapLeft > 0f;
            if (root.gameObject.activeSelf != shown)
                root.gameObject.SetActive(shown);
            if (shown)
                Draw(dt);
            else
            {
                VisibleLayers = 0;
                Coverage = 0f;
                PlayfieldGlitchCoverage = 0f;
                SandMotesVisible = 0;
            }
            Sound(rewinding, dt);
        }

        void Draw(float dt)
        {
            bool calm = GameSettings.ReducedFlash;
            float k = intensity * (calm ? 0.5f : 1f);
            // Snap-back flash: brief gold wash on the rim as colour returns.
            float snap = snapLeft > 0f ? (snapLeft / 0.2f) : 0f;
            float t = Time.unscaledTime;
            float h = root.rect.height;
            float w = root.rect.width;
            // Critic: scan/glitch/gold only on the outer 8% of the *screen* (central 84% stays clear).
            float top = h * OuterFrac;
            float bot = h * OuterFrac;
            float left = w * OuterFrac;
            float right = w * OuterFrac;

            PlaceScanStrip(scanTop, scanTopImg, 0f, 1f, 1f, 1f, 0f, top, t, k, horizontal: true);
            PlaceScanStrip(scanBot, scanBotImg, 0f, 0f, 1f, 0f, 0f, bot, t, k, horizontal: true);
            PlaceScanStrip(scanLeft, scanLeftImg, 0f, 0f, 0f, 1f, left, 0f, t, k * 0.6f, horizontal: false);
            PlaceScanStrip(scanRight, scanRightImg, 1f, 0f, 1f, 1f, right, 0f, t, k * 0.6f, horizontal: false);

            // Gold time-ripple: four edge fills only (same pad as scan), never a full-screen vignette.
            float pulse = 0.55f + 0.2f * Mathf.Sin(t * 7f) * (calm ? 0f : 1f);
            float goldA = (0.55f * k + 0.45f * snap) * pulse;
            PlaceGoldStrip(goldTop, 0f, 1f, 1f, 1f, top, goldA, topEdge: true);
            PlaceGoldStrip(goldBot, 0f, 0f, 1f, 0f, bot, goldA * 0.9f, topEdge: false);
            PlaceGoldStrip(goldLeft, 0f, 0f, 0f, 1f, left, goldA * 0.75f, leftEdge: true);
            PlaceGoldStrip(goldRight, 1f, 0f, 1f, 1f, right, goldA * 0.75f, leftEdge: false);

            // Sand / ember motes streaming backwards (right → left while rewinding), kept in the outer bands.
            int motesOn = 0;
            int moteBudget = calm ? Motes / 2 : Motes;
            float playfieldGlitch = 0f;
            for (int i = 0; i < Motes; i++)
            {
                if (i >= moteBudget || k < 0.05f)
                {
                    mote[i].color = Color.clear;
                    continue;
                }
                float life = Mathf.Repeat(t * (0.55f + moteSeed[i] * 0.7f) + moteSeed[i] * 5f, 1f);
                float x = Mathf.Lerp(w + 20f, -20f, life);
                bool topBand = (i & 1) == 0;
                float y = topBand
                    ? h - Hash(i * 2.3f) * top
                    : Hash(i * 4.1f) * bot;
                var r = mote[i].rectTransform;
                r.anchoredPosition = new Vector2(x, y);
                float size = 3f + 5f * moteSeed[i];
                r.sizeDelta = new Vector2(size, size * (0.4f + 0.6f * moteSeed[i]));
                float fade = Mathf.Sin(life * Mathf.PI) * k;
                bool ember = moteSeed[i] > 0.55f;
                mote[i].color = ember
                    ? new Color(1f, 0.45f + 0.3f * moteSeed[i], 0.12f, 0.7f * fade)
                    : new Color(0.92f, 0.82f, 0.55f, 0.55f * fade);
                if (fade > 0.05f)
                    motesOn++;
                // Motes that land inside the central 84% count as playfield glitch (should stay ~0).
                if (fade > 0.05f && x > w * OuterFrac && x < w * (1f - OuterFrac)
                    && y > h * OuterFrac && y < h * (1f - OuterFrac))
                    playfieldGlitch += (size * size) / Mathf.Max(1f, w * h);
            }
            SandMotesVisible = motesOn;

            // Rim sizes are fixed at OuterFrac; any mote inside the centre already added above.
            // Also count if a gold/scan strip depth somehow exceeds the outer band.
            float maxRimFrac = Mathf.Max(Mathf.Max(top, bot) / Mathf.Max(1f, h), Mathf.Max(left, right) / Mathf.Max(1f, w));
            if (maxRimFrac > OuterFrac + 0.002f)
                playfieldGlitch += (maxRimFrac - OuterFrac);

            VisibleLayers = 4 + 4 + motesOn
                + (RewindWorldLook.GhostVisible > 0 ? 1 : 0)
                + (RewindWorldLook.BulletStreaks > 0 ? 1 : 0);
            float area = Mathf.Max(1f, w * h);
            float edge = top * w + bot * w + left * h + right * h;
            Coverage = Mathf.Clamp01(k) * Mathf.Clamp01(Mathf.Max(edge / area, OuterFrac * 2f));
            PlayfieldGlitchCoverage = Mathf.Clamp01(playfieldGlitch);
        }

        static void PlaceGoldStrip(Image img, float ax0, float ay0, float ax1, float ay1, float depth, float alpha,
            bool topEdge = false, bool leftEdge = false)
        {
            if (img == null)
                return;
            var rect = img.rectTransform;
            rect.anchorMin = new Vector2(ax0, ay0);
            rect.anchorMax = new Vector2(ax1, ay1);
            if (ax0 != ax1)
            {
                // Horizontal strip (top/bot).
                rect.pivot = new Vector2(0.5f, topEdge ? 1f : 0f);
                rect.sizeDelta = new Vector2(0f, depth);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.pivot = new Vector2(leftEdge ? 0f : 1f, 0.5f);
                rect.sizeDelta = new Vector2(depth, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
            img.color = new Color(Gold.r, Gold.g, Gold.b, alpha);
        }

        static void PlaceScanStrip(RectTransform rect, Image img, float ax0, float ay0, float ax1, float ay1,
            float sizeA, float sizeB, float t, float k, bool horizontal)
        {
            if (rect == null || img == null)
                return;
            rect.anchorMin = new Vector2(ax0, ay0);
            rect.anchorMax = new Vector2(ax1, ay1);
            rect.pivot = new Vector2((ax0 + ax1) * 0.5f, (ay0 + ay1) * 0.5f);
            if (horizontal)
            {
                rect.sizeDelta = new Vector2(0f, sizeB);
                rect.anchoredPosition = new Vector2(0f, ay0 > 0.5f ? -sizeB * 0.5f : sizeB * 0.5f);
                // Scroll the tiled scan along the strip.
                rect.anchoredPosition += new Vector2(0f, Mathf.Repeat(t * 40f, ScanPeriod) - ScanPeriod * 0.5f);
            }
            else
            {
                rect.sizeDelta = new Vector2(sizeA, 0f);
                rect.anchoredPosition = new Vector2(ax0 > 0.5f ? -sizeA * 0.5f : sizeA * 0.5f, 0f);
            }
            var c = img.color;
            c.a = 0.2f * k;
            img.color = c;
        }

        void Sound(bool rewinding, float dt)
        {
            float target = 0f;
            if (rewinding)
            {
                var clip = LoopClip();
                if (clip != null && !source.isPlaying)
                {
                    source.clip = clip;
                    source.loop = true;
                    source.volume = 0f;
                    source.pitch = PitchStart;
                    reportedPitch = PitchStart;
                    source.Play();
                    SickOfIt.Core.Audio.AudioProbeBridge.Report(LoopSound, SickOfIt.Data.Config.Bus.Sfx, PitchStart, 0.55f);
                }
                target = 0.55f * GameSettings.SfxVolume * SickOfIt.Core.Audio.AudioBuses.Gain(SickOfIt.Data.Config.Bus.Sfx);
            }
            volume = Mathf.MoveTowards(volume, target, dt * (rewinding ? 6f : 8f));
            if (source.isPlaying)
            {
                float climb = 1f - Mathf.Exp(-rewindingFor / PitchRamp);
                source.pitch = Mathf.Lerp(PitchStart, PitchEnd, climb);
                if (rewinding && source.pitch >= reportedPitch + 0.3f)
                {
                    reportedPitch = source.pitch;
                    SickOfIt.Core.Audio.AudioProbeBridge.Report(LoopSound, SickOfIt.Data.Config.Bus.Sfx, reportedPitch, 0.55f);
                }
                source.volume = volume;
                if (!rewinding && volume <= 0.001f)
                    source.Stop();
            }
            SoundPlaying = source.isPlaying;
            Pitch = source.isPlaying ? source.pitch : 0f;
            SoundVolume = source.isPlaying ? source.volume : 0f;
        }

        void OnDestroy()
        {
            Intensity = 0f;
            SoundPlaying = false;
            Pitch = 0f;
            SoundVolume = 0f;
            VisibleLayers = 0;
            Coverage = 0f;
            PlayfieldGlitchCoverage = 0f;
            SnapBackActive = false;
            SandMotesVisible = 0;
            if (reversed != null && ReversedClip)
                Destroy(reversed);
            ReversedClip = false;
        }
    }
}
