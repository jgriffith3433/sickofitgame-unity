using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Rook on every Shaft stop (Docs/Story.md "The Climbers"): chalk tally wall of the hero's real rewind count
    /// (Achievements stat "rewinds", bundles of five) and one voiced line per visit by band. Presentation only.
    /// </summary>
    public sealed class RookShaft : MonoBehaviour
    {
        const string LastLineKey = "Rook_Shaft_LastLine";
        const string ShownCountKey = "Rook_Shaft_ShownCount";

        /// <summary>What Rook did, for the playtest: (kind, detail).</summary>
        public static event Action<string, string> Reported;

        public string Clip { get; private set; } = "";
        public bool Painted => frames.TryGetValue(Clip, out var list) && list.Count > 0;
        public bool Speaking { get; private set; }
        /// <summary>Rewind count drawn on the wall right now.</summary>
        public int ShownRewinds { get; private set; }
        /// <summary>How many tally strokes are drawn (groups of five use a slash).</summary>
        public int MarkCount { get; private set; }
        /// <summary>The line he just said (sound id), or "".</summary>
        public string LastSaid { get; private set; } = "";
        /// <summary>Portrait height in canvas pixels (phone criterion: ≥ 64).</summary>
        public float PortraitHeight => rect != null ? rect.rect.height : 0f;
        /// <summary>Caption on screen now, or "".</summary>
        public string CaptionText => caption != null && caption.gameObject.activeInHierarchy ? caption.text : "";

        RookConfig config;
        RectTransform rect;
        RectTransform body;
        RectTransform wall;
        Image art;
        Image blob;
        readonly List<Image> marks = new List<Image>();
        readonly Dictionary<string, List<Sprite>> frames = new Dictionary<string, List<Sprite>>();
        readonly Dictionary<string, float> rates = new Dictionary<string, float>();
        TextMeshProUGUI caption;
        TextMeshProUGUI countLabel;
        float clipStarted;
        float clipEnds = -1f;
        int drawnMarks;
        int targetMarks;
        float nextScratch = -1f;

        public static RookShaft Create(MenuCanvas menu, Transform parent, RookConfig config, MenuPlacement landscape, MenuPlacement portrait)
        {
            var slot = MenuGraphics.NewRect("Rook", parent != null ? parent : menu.Root);
            MenuGraphics.Place(slot, landscape.anchor, landscape.offset, landscape.size);
            MenuResponsive.Track(slot, landscape, portrait, MenuElementKind.Text, menu.Responsive);
            var view = slot.gameObject.AddComponent<RookShaft>();
            view.Build(menu, config);
            return view;
        }

        void Build(MenuCanvas menu, RookConfig settings)
        {
            config = settings;
            rect = (RectTransform)transform;

            // Tally wall behind him (readable at phone size: thick chalk strokes).
            wall = MenuGraphics.NewRect("TallyWall", rect);
            wall.anchorMin = new Vector2(0.02f, 0.08f);
            wall.anchorMax = new Vector2(0.42f, 0.92f);
            wall.offsetMin = wall.offsetMax = Vector2.zero;
            var wallImg = wall.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(wallImg, MenuShape.Rounded, 10f);
            wallImg.color = config.wall;
            wallImg.raycastTarget = false;

            countLabel = MenuGraphics.Text(wall, "Count", "0", menu.Theme.caption, menu.Font, 22f);
            countLabel.enableVertexGradient = false;
            countLabel.color = config.chalk;
            countLabel.alignment = TextAlignmentOptions.Center;
            var cl = countLabel.rectTransform;
            cl.anchorMin = new Vector2(0.05f, 0.82f);
            cl.anchorMax = new Vector2(0.95f, 0.98f);
            cl.offsetMin = cl.offsetMax = Vector2.zero;

            var frame = MenuGraphics.NewImage(rect, "Frame", config.frame);
            MenuGraphics.Apply(frame, MenuShape.Rounded, 22f);
            frame.raycastTarget = false;
            frame.rectTransform.anchorMin = new Vector2(0.38f, 0.05f);
            frame.rectTransform.anchorMax = new Vector2(0.98f, 0.95f);
            frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;

            var inner = MenuGraphics.NewImage(frame.rectTransform, "Inside", new Color(0.05f, 0.03f, 0.04f, 0.92f));
            MenuGraphics.Apply(inner, MenuShape.Rounded, 16f);
            inner.raycastTarget = false;
            inner.rectTransform.offsetMin = new Vector2(6f, 6f);
            inner.rectTransform.offsetMax = new Vector2(-6f, -6f);

            var window = MenuGraphics.NewRect("Window", frame.rectTransform);
            MenuGraphics.Fill(window, 8f);
            window.gameObject.AddComponent<RectMask2D>();
            body = MenuGraphics.NewRect("Body", window);
            MenuGraphics.Fill(body);
            body.pivot = new Vector2(0.5f, 0f);

            foreach (var clip in config.clips)
                Cut(clip);
            BuildPlaceholder();
            var artRect = MenuGraphics.NewRect("Art", body);
            MenuGraphics.Fill(artRect);
            art = artRect.gameObject.AddComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;
            art.enabled = false;

            var tag = MenuGraphics.NewRect("Name", frame.rectTransform);
            tag.anchorMin = new Vector2(0.1f, 0f);
            tag.anchorMax = new Vector2(0.9f, 0f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.sizeDelta = new Vector2(0f, 40f);
            tag.anchoredPosition = new Vector2(0f, 4f);
            var back = MenuGraphics.NewImage(tag, "Back", config.frame);
            MenuGraphics.Apply(back, MenuShape.Pill, 0f);
            back.raycastTarget = false;
            var name = MenuGraphics.Text(tag, "Text", config.displayName, menu.Theme.caption, menu.Font, 26f);
            name.enableVertexGradient = false;
            name.color = Color.white;
            name.alignment = TextAlignmentOptions.Center;

            caption = menu.AddText(menu.Root, "RookCaption", "", menu.Theme.subtitle, config.subtitle.anchor, config.subtitle.offset, config.subtitle.size, out _, 36f);
            caption.fontStyle &= ~FontStyles.UpperCase;
            caption.color = new Color(0.95f, 0.66f, 0.23f, 1f);
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 18f;
            MenuResponsive.Track((RectTransform)caption.transform.parent, config.subtitle, config.subtitlePortrait, MenuElementKind.Text, menu.Responsive);
            caption.transform.parent.gameObject.SetActive(false);

            Play("idle");
            SyncWall(Achievements.Get("rewinds"), animate: false);
            Report("open", $"portrait: {frames.Count} loops painted, wall {ShownRewinds} rewinds / {MarkCount} marks, height {PortraitHeight:0}px");
        }

        void Cut(RookClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.name) || clip.sheet == null || clip.sheet.texture == null)
                return;
            int columns = Mathf.Max(1, clip.columns), rows = Mathf.Max(1, clip.rows);
            int count = clip.frames > 0 ? Mathf.Min(clip.frames, columns * rows) : columns * rows;
            var source = clip.sheet.textureRect;
            float w = source.width / columns, h = source.height / rows;
            var list = new List<Sprite>(count);
            for (int i = 0; i < count; i++)
            {
                int col = i % columns, row = i / columns;
                var cell = new Rect(source.x + col * w, source.y + source.height - (row + 1) * h, w, h);
                var sprite = Sprite.Create(clip.sheet.texture, cell, new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = $"rook_{clip.name}_{i}";
                list.Add(sprite);
            }
            frames[clip.name] = list;
            rates[clip.name] = Mathf.Max(1f, clip.fps);
        }

        void BuildPlaceholder()
        {
            var holder = MenuGraphics.NewRect("Placeholder", body);
            MenuGraphics.Fill(holder);
            blob = Part(holder, "Coat", config.body, MenuShape.Rounded, new Vector2(0.22f, 0.05f), new Vector2(0.78f, 0.72f), 28f);
            Part(holder, "Head", config.body, MenuShape.Circle, new Vector2(0.32f, 0.68f), new Vector2(0.68f, 0.98f), 0f);
            Part(holder, "Scarf", new Color(0.9f, 0.25f, 0.15f, 1f), MenuShape.Pill, new Vector2(0.28f, 0.55f), new Vector2(0.72f, 0.66f), 0f);
            Part(holder, "Medal", new Color(0.85f, 0.7f, 0.2f, 1f), MenuShape.Circle, new Vector2(0.44f, 0.42f), new Vector2(0.56f, 0.54f), 0f);
        }

        static Image Part(RectTransform parent, string name, Color color, MenuShape shape, Vector2 min, Vector2 max, float radius)
        {
            var img = MenuGraphics.NewImage(parent, name, color);
            MenuGraphics.Apply(img, shape, radius);
            img.raycastTarget = false;
            img.rectTransform.anchorMin = min;
            img.rectTransform.anchorMax = max;
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            return img;
        }

        void Play(string name, float seconds = -1f)
        {
            Clip = string.IsNullOrEmpty(name) ? "idle" : name;
            clipStarted = Time.unscaledTime;
            clipEnds = seconds > 0f ? clipStarted + seconds : -1f;
            bool has = frames.TryGetValue(Clip, out var list) && list.Count > 0;
            art.enabled = has;
            if (blob != null)
                blob.transform.parent.gameObject.SetActive(!has);
        }

        /// <summary>Draws (or grows) the chalk marks for 'rewinds'. New marks scratch in with sound when animate.</summary>
        public void SyncWall(int rewinds, bool animate)
        {
            ShownRewinds = Mathf.Max(0, rewinds);
            targetMarks = ShownRewinds;
            if (countLabel != null)
                countLabel.text = ShownRewinds.ToString(CultureInfo.InvariantCulture);
            if (!animate)
            {
                drawnMarks = targetMarks;
                RebuildMarks(drawnMarks);
                PlayerPrefs.SetInt(ShownCountKey, ShownRewinds);
                MarkCount = drawnMarks;
                return;
            }
            int was = PlayerPrefs.GetInt(ShownCountKey, 0);
            if (ShownRewinds > was)
            {
                drawnMarks = was;
                RebuildMarks(drawnMarks);
                nextScratch = Time.unscaledTime + 0.15f;
                Play("scratch", 1.2f);
            }
            else
            {
                drawnMarks = targetMarks;
                RebuildMarks(drawnMarks);
            }
            MarkCount = drawnMarks;
            PlayerPrefs.SetInt(ShownCountKey, ShownRewinds);
        }

        void RebuildMarks(int count)
        {
            foreach (var m in marks)
                if (m != null)
                    Destroy(m.gameObject);
            marks.Clear();
            // Up to 40 strokes visible (8 bundles); beyond that the number still climbs.
            int show = Mathf.Min(count, 40);
            for (int i = 0; i < show; i++)
            {
                int bundle = i / 5;
                int inBundle = i % 5;
                bool slash = inBundle == 4;
                var mark = MenuGraphics.NewImage(wall, slash ? "Slash" : "Stroke", config.chalk);
                mark.raycastTarget = false;
                var r = mark.rectTransform;
                float col = 0.12f + (bundle % 4) * 0.22f;
                float row = 0.72f - (bundle / 4) * 0.28f;
                if (slash)
                {
                    r.anchorMin = new Vector2(col - 0.02f, row - 0.12f);
                    r.anchorMax = new Vector2(col + 0.16f, row + 0.02f);
                    r.localRotation = Quaternion.Euler(0f, 0f, -55f);
                }
                else
                {
                    r.anchorMin = new Vector2(col + inBundle * 0.035f, row - 0.14f);
                    r.anchorMax = new Vector2(col + inBundle * 0.035f + 0.02f, row + 0.02f);
                }
                r.offsetMin = r.offsetMax = Vector2.zero;
                // Thick enough to read at 390x844: at least 4 px once laid out.
                marks.Add(mark);
            }
            MarkCount = count;
        }

        void Update()
        {
            if (clipEnds > 0f && Time.unscaledTime >= clipEnds && !Speaking)
                Play("idle");
            float t = Time.unscaledTime - clipStarted;
            if (art != null && art.enabled && frames.TryGetValue(Clip, out var list) && list.Count > 0)
            {
                float fps = rates.TryGetValue(Clip, out var rate) ? rate : 10f;
                art.sprite = list[Mathf.FloorToInt(t * fps) % list.Count];
            }
            else if (blob != null)
            {
                float wobble = Mathf.Sin(Time.unscaledTime * 2.4f);
                body.localScale = new Vector3(1f + 0.02f * wobble, 1f - 0.02f * wobble, 1f);
            }

            if (drawnMarks < targetMarks && nextScratch > 0f && Time.unscaledTime >= nextScratch)
            {
                drawnMarks++;
                RebuildMarks(drawnMarks);
                if (!string.IsNullOrEmpty(config.scratchSound))
                    Sfx.Play(config.scratchSound);
                Report("scratch", $"mark {drawnMarks} of {targetMarks}");
                nextScratch = drawnMarks < targetMarks ? Time.unscaledTime + 0.12f : -1f;
                if (drawnMarks >= targetMarks)
                    Play("idle");
            }
        }

        /// <summary>Picks one line for the current rewind count, never the same sound twice in a row.</summary>
        public RookLine PickLine(int rewinds)
        {
            var band = new List<RookLine>();
            foreach (var line in config.lines)
            {
                if (line == null) continue;
                if (rewinds < line.minRewinds) continue;
                if (line.maxRewinds >= 0 && rewinds > line.maxRewinds) continue;
                band.Add(line);
            }
            if (band.Count == 0)
                return null;
            string last = PlayerPrefs.GetString(LastLineKey, "");
            var choices = new List<RookLine>();
            foreach (var line in band)
                if (line.sound != last)
                    choices.Add(line);
            if (choices.Count == 0)
                choices.AddRange(band);
            // Stable pick from the count so a visit is deterministic, then avoid the last.
            int pick = (rewinds * 17 + band.Count * 3) % choices.Count;
            return choices[pick];
        }

        /// <summary>Says one Shaft line: voice + caption, portrait talk/scratch. Skip ends early.</summary>
        public IEnumerator SayVisit(Func<bool> skipped = null)
        {
            int rewinds = Achievements.Get("rewinds");
            SyncWall(rewinds, animate: true);
            var line = PickLine(rewinds);
            if (line == null)
            {
                Report("line", "none for band");
                yield break;
            }
            Speaking = true;
            LastSaid = line.sound ?? "";
            PlayerPrefs.SetString(LastLineKey, LastSaid);
            float length = Seconds(line.sound, line.text);
            Sfx.Play(line.sound);
            Play(string.IsNullOrEmpty(line.clip) ? "talk" : line.clip, length);
            if (caption != null)
            {
                caption.text = $"{config.speaker}: {line.text}";
                caption.transform.parent.gameObject.SetActive(true);
            }
            Report("line", $"rook band {BandOf(rewinds)} ({rewinds}): {line.text} ({line.sound}{(Sfx.Has(line.sound) ? "" : ", no clip")})");
            yield return Wait(length + config.pause, skipped);
            if (caption != null)
                caption.transform.parent.gameObject.SetActive(false);
            Play("idle");
            Speaking = false;
        }

        static string BandOf(int n)
        {
            if (n <= 0) return "0";
            if (n <= 5) return "1-5";
            if (n <= 20) return "6-20";
            return "20+";
        }

        float Seconds(string sound, string text)
        {
            float clip = Sfx.LengthOf(sound);
            return clip > 0f ? clip : Mathf.Max(1.4f, (text ?? "").Length * config.secondsPerCharacter);
        }

        static IEnumerator Wait(float seconds, Func<bool> skipped)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && (skipped == null || !skipped()))
                yield return null;
        }

        static void Report(string kind, string detail) => Reported?.Invoke(kind, detail);

        void OnDestroy()
        {
            if (caption != null && caption.transform.parent != null)
                Destroy(caption.transform.parent.gameObject);
        }
    }
}
