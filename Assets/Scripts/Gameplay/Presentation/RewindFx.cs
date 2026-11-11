using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// What rewinding looks and sounds like (Docs/Story.md "Rewind that feels amazing"): while R is held the picture goes
    /// to tape: scrolling scanlines, a cyan/magenta colour split at the edges, a tracking bar sweeping up the screen and
    /// speed streaks trailing off to the side; and a reversed tape whirr loops with its pitch climbing the longer the
    /// rewind runs, fading out when it ends. Scanlines and glitch overlays stay on the letterbox / screen frame and off
    /// the playfield (Docs/lighting.md). All presentation: real time, no simulation state, nothing saved, no
    /// allocation per frame (a fixed set of Images and one AudioSource). Reduced Flash turns off the jitter and halves
    /// the strength.
    /// </summary>
    public sealed class RewindFx : MonoBehaviour
    {
        const string LoopSound = "rewind_start";
        const int Streaks = 10;
        const float ScanPeriod = 6f;
        const float PitchStart = 0.8f, PitchEnd = 1.9f, PitchRamp = 4.5f;

        RectTransform root;
        RectTransform scanRect;
        Image scan, fringeCyan, fringeMagenta, band;
        readonly Image[] streak = new Image[Streaks];
        readonly float[] streakSeed = new float[Streaks];
        AudioSource source;
        float reportedPitch;
        AudioClip reversed;
        float intensity;
        float rewindingFor;
        float volume;

        // What the playtest reads: the effect's state this frame.
        public static float Intensity { get; private set; }
        public static bool Active => Intensity > 0.02f;
        public static bool SoundPlaying { get; private set; }
        public static float Pitch { get; private set; }
        public static float SoundVolume { get; private set; }
        public static int VisibleLayers { get; private set; }
        /// <summary>How much of the screen the effect tints on the last rewinding frame, 0..1 (scanlines + fringes + wash).</summary>
        public static float Coverage { get; private set; }
        public static bool ReversedClip { get; private set; }

        public static RewindFx Create(Transform hud, Color tint, int siblingIndex)
        {
            var go = new GameObject("RewindFx", typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var fx = go.AddComponent<RewindFx>();
            fx.Build(tint);
            go.transform.SetSiblingIndex(siblingIndex);
            return fx;
        }

        void Build(Color tint)
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Scanlines: a tiled one-in-six dark row, taller than the screen so it can scroll by whole periods.
            scan = MenuGraphics.NewImage(root, "Scanlines", Color.clear);
            scan.sprite = ScanSprite();
            scan.type = Image.Type.Tiled;
            scanRect = scan.rectTransform;
            scanRect.offsetMin = new Vector2(0f, -ScanPeriod * 2f);
            scanRect.offsetMax = new Vector2(0f, ScanPeriod * 2f);

            // The colour split: the vignette twice, one cyan nudged right, one magenta nudged left.
            fringeCyan = MenuGraphics.NewImage(root, "FringeCyan", Color.clear);
            fringeCyan.sprite = MenuGraphics.Vignette();
            fringeMagenta = MenuGraphics.NewImage(root, "FringeMagenta", Color.clear);
            fringeMagenta.sprite = MenuGraphics.Vignette();

            // A tracking bar, like a tape's: a soft bright band sweeping upward.
            band = MenuGraphics.NewImage(root, "TrackingBar", Color.clear);
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.sizeDelta = new Vector2(0f, 46f);

            for (int i = 0; i < Streaks; i++)
            {
                var s = MenuGraphics.NewImage(root, "Streak" + i, Color.clear);
                var r = s.rectTransform;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 0f);
                r.pivot = new Vector2(0f, 0.5f);
                streak[i] = s;
                streakSeed[i] = Hash(i * 7.31f);
            }
            root.gameObject.SetActive(false);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            source.ignoreListenerPause = false;
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

        void Update()
        {
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            float dt = Time.unscaledDeltaTime;
            intensity = Mathf.MoveTowards(intensity, rewinding ? 1f : 0f, dt * (rewinding ? 9f : 5f));
            rewindingFor = rewinding ? rewindingFor + dt : 0f;
            Intensity = intensity;

            bool shown = intensity > 0.001f;
            if (root.gameObject.activeSelf != shown)
                root.gameObject.SetActive(shown);
            if (shown)
                Draw(dt);
            else
            {
                VisibleLayers = 0;
                Coverage = 0f;
            }
            Sound(rewinding, dt);
        }

        void Draw(float dt)
        {
            bool calm = GameSettings.ReducedFlash;
            float k = intensity * (calm ? 0.5f : 1f);
            float t = Time.unscaledTime;
            float jitter = calm ? 0f : 1f;
            float h = root.rect.height;
            float w = root.rect.width;
            // Playfield in HUD space: the camera's viewport, or a centre band when the screen is already 16:9.
            // Scanlines / fringes / streaks stay in the frame outside it so the play area stays readable (critic).
            GetPlayfieldPad(w, h, out float padL, out float padR, out float padB, out float padT);

            // Scanlines only in the letterbox / edge frame (top and bottom strips).
            float bandH = Mathf.Max(padT, padB, h * 0.08f);
            var c = scan.color; c.a = 0.22f * k; scan.color = c;
            scanRect.anchorMin = new Vector2(0f, 0f);
            scanRect.anchorMax = new Vector2(1f, 1f);
            scanRect.offsetMin = new Vector2(0f, h - bandH - ScanPeriod * 2f);
            scanRect.offsetMax = new Vector2(0f, bandH + ScanPeriod * 2f);
            // Hide the middle of the tiled scan by shrinking it to the top strip only.
            scanRect.anchorMin = new Vector2(0f, 1f);
            scanRect.anchorMax = new Vector2(1f, 1f);
            scanRect.pivot = new Vector2(0.5f, 1f);
            scanRect.sizeDelta = new Vector2(0f, bandH + ScanPeriod);
            scanRect.anchoredPosition = new Vector2(0f, Mathf.Repeat(t * 40f, ScanPeriod));

            // Colour split: vignette fringes sit on the outer frame, punched away from the playfield centre.
            float shift = 16f + 8f * Mathf.Sin(t * 9f) * jitter;
            fringeCyan.color = new Color(0.1f, 0.95f, 1f, 0.45f * k);
            fringeMagenta.color = new Color(1f, 0.15f, 0.8f, 0.4f * k);
            // Keep the vignette, but inset so the hot centre of the playfield is clear.
            fringeCyan.rectTransform.offsetMin = new Vector2(padL * 0.35f + shift, padB * 0.35f);
            fringeCyan.rectTransform.offsetMax = new Vector2(-(padR * 0.35f) + shift, -(padT * 0.35f));
            fringeMagenta.rectTransform.offsetMin = new Vector2(padL * 0.35f - shift, padB * 0.35f);
            fringeMagenta.rectTransform.offsetMax = new Vector2(-(padR * 0.35f) - shift, -(padT * 0.35f));
            // Soften the middle by lowering fringe alpha when pads are thin (full-bleed playfield).
            if (padL + padR + padT + padB < w * 0.05f)
            {
                fringeCyan.color = new Color(0.1f, 0.95f, 1f, 0.22f * k);
                fringeMagenta.color = new Color(1f, 0.15f, 0.8f, 0.2f * k);
            }

            // Tracking bar rides the top or bottom letterbox, never through the playfield middle.
            float rise = Mathf.Repeat(t / 1.4f, 1f);
            bool topBand = rise > 0.5f;
            band.color = new Color(0.7f, 1f, 1f, 0.16f * k);
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, topBand ? 1f : 0f);
            bandRect.anchorMax = new Vector2(1f, topBand ? 1f : 0f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.sizeDelta = new Vector2(0f, Mathf.Min(46f, bandH));
            bandRect.anchoredPosition = new Vector2(0f, topBand ? -bandH * rise * 2f : bandH * (1f - rise) * 2f);

            // Speed streaks only along the top and bottom edges (and side letterbox).
            for (int i = 0; i < Streaks; i++)
            {
                float life = Mathf.Repeat(t * (0.9f + streakSeed[i]) + streakSeed[i] * 9f, 1f);
                int cycle = Mathf.FloorToInt(t * (0.9f + streakSeed[i]) + streakSeed[i] * 9f);
                bool top = (i & 1) == 0;
                float y = top
                    ? h - Hash(i * 3.7f + cycle * 1.9f) * bandH
                    : Hash(i * 3.7f + cycle * 1.9f) * bandH;
                float len = w * (0.12f + 0.28f * Hash(i + cycle * 5.1f));
                float x = Mathf.Lerp(-len, w, life);
                var r = streak[i].rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, y);
                r.sizeDelta = new Vector2(len, 2f + 3f * streakSeed[i]);
                float fade = Mathf.Sin(life * Mathf.PI);
                streak[i].color = new Color(0.75f, 1f, 1f, 0.5f * fade * k * (calm ? 0.6f : 1f));
            }
            VisibleLayers = 3 + Streaks;
            // Coverage is the tinted *frame* share (letterbox / edge), not the playfield centre.
            float area = Mathf.Max(1f, w * h);
            float edge = padL * h + padR * h + padT * w + padB * w;
            Coverage = Mathf.Clamp01(k) * Mathf.Clamp01(Mathf.Max(edge / area, 0.12f));
        }

        /// <summary>Letterbox / pillarbox padding in HUD pixels; falls back to an 8% frame when the playfield is full-bleed.</summary>
        static void GetPlayfieldPad(float w, float h, out float padL, out float padR, out float padB, out float padT)
        {
            padL = padR = padB = padT = 0f;
            var cam = Camera.main;
            Rect view = cam != null ? cam.rect : new Rect(0f, 0f, 1f, 1f);
            if (cam != null && cam.TryGetComponent(out SickOfIt.Core.Device.AspectRatioController aspect))
                view = aspect.LastViewport;
            padL = view.xMin * w;
            padR = (1f - view.xMax) * w;
            padB = view.yMin * h;
            padT = (1f - view.yMax) * h;
            if (padL + padR + padB + padT < 1f)
            {
                // Full-bleed: keep a thin outer frame so glitches never sit on the hero.
                padL = padR = w * 0.06f;
                padB = padT = h * 0.08f;
            }
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
                    // Studio's audio probe: the rewind whoosh starts (its pitch then climbs with the scrub).
                    SickOfIt.Core.Audio.AudioProbeBridge.Report(LoopSound, SickOfIt.Data.Config.Bus.Sfx, PitchStart, 0.55f);
                }
                target = 0.55f * GameSettings.SfxVolume * SickOfIt.Core.Audio.AudioBuses.Gain(SickOfIt.Data.Config.Bus.Sfx);
            }
            volume = Mathf.MoveTowards(volume, target, dt * (rewinding ? 6f : 8f));
            if (source.isPlaying)
            {
                // The pitch climbs the longer it runs (a rewind winding up) and drops back with the stop.
                float climb = 1f - Mathf.Exp(-rewindingFor / PitchRamp);
                source.pitch = Mathf.Lerp(PitchStart, PitchEnd, climb);
                // The probe hears the scrub's pitch as it climbs, one report per 0.3 step (never per frame).
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
            if (reversed != null && ReversedClip)
                Destroy(reversed);
            ReversedClip = false;
        }
    }
}
