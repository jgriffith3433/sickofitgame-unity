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
    /// Yooba's talking portrait (Docs/Story.md, "Yooba between floors"; ui/yooba.json): his loops (idle, talk, laugh,
    /// cough) cut from their sprite sheets and played on unscaled time, in a rounded frame with his name. A loop
    /// without a sheet is drawn as a placeholder slime (green blob, two eyestalks, a mouth that opens while he talks).
    /// Lives in menus (the Shaft, the shop): presentation only.
    /// </summary>
    public sealed class YoobaPortrait : MonoBehaviour
    {
        /// <summary>What Yooba did, for the playtest and the log: (kind, detail), e.g. ("clip", "talk (painted)") or ("line", "...").</summary>
        public static event Action<string, string> Reported;

        /// <summary>The loop playing now (idle, talk, laugh, cough).</summary>
        public string Clip { get; private set; } = "";
        /// <summary>The loop on screen comes from its painted sheet (false = the placeholder slime).</summary>
        public bool Painted => frames.TryGetValue(Clip, out var list) && list.Count > 0;
        /// <summary>Loops with a painted sheet.</summary>
        public int PaintedClips => frames.Count;
        /// <summary>The idle behavior playing now (blink, look, hop, hum, sniff), or "".</summary>
        public string Behavior => behavior;
        /// <summary>How many idle behaviors he has done (the playtest reads it).</summary>
        public int BehaviorsDone { get; private set; }
        /// <summary>A line is being said right now.</summary>
        public bool Speaking { get; private set; }

        YoobaConfig config;
        readonly Dictionary<string, List<Sprite>> frames = new Dictionary<string, List<Sprite>>();
        readonly Dictionary<string, float> rates = new Dictionary<string, float>();
        RectTransform body;
        Image art;
        Image blob;
        Image mouth;
        Image pupilL, pupilR;
        float clipStarted;
        string behavior = "";
        float behaviorStart, behaviorLength, nextBehavior = -1f;
        static readonly string[] Behaviors = { "blink", "look", "hop", "hum", "sniff" };
        float clipEnds = -1f;

        /// <summary>Builds the portrait under 'parent', placed (and kept on screen) like any menu element.</summary>
        public static YoobaPortrait Create(MenuCanvas menu, Transform parent, YoobaConfig config, MenuPlacement landscape, MenuPlacement portrait)
        {
            var slot = MenuGraphics.NewRect("Yooba", parent != null ? parent : menu.Root);
            MenuGraphics.Place(slot, landscape.anchor, landscape.offset, landscape.size);
            MenuResponsive.Track(slot, landscape, portrait, MenuElementKind.Text, menu.Responsive);
            var view = slot.gameObject.AddComponent<YoobaPortrait>();
            view.Build(menu, config);
            return view;
        }

        void Build(MenuCanvas menu, YoobaConfig settings)
        {
            config = settings;
            var rect = (RectTransform)transform;
            var frame = MenuGraphics.NewImage(rect, "Frame", config.frame);
            MenuGraphics.Apply(frame, MenuShape.Rounded, 28f);
            frame.raycastTarget = false;
            var inner = MenuGraphics.NewImage(rect, "Inside", new Color(0.05f, 0.03f, 0.04f, 0.92f));
            MenuGraphics.Apply(inner, MenuShape.Rounded, 22f);
            inner.raycastTarget = false;
            inner.rectTransform.offsetMin = new Vector2(8f, 8f);
            inner.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var window = MenuGraphics.NewRect("Window", rect);
            MenuGraphics.Fill(window, 10f);
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

            // His name on a tag across the bottom of the frame.
            var tag = MenuGraphics.NewRect("Name", rect);
            tag.anchorMin = new Vector2(0.12f, 0f);
            tag.anchorMax = new Vector2(0.88f, 0f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.sizeDelta = new Vector2(0f, 46f);
            tag.anchoredPosition = new Vector2(0f, 4f);
            var back = MenuGraphics.NewImage(tag, "Back", config.frame);
            MenuGraphics.Apply(back, MenuShape.Pill, 0f);
            back.raycastTarget = false;
            var name = MenuGraphics.Text(tag, "Text", config.displayName, menu.Theme.caption, menu.Font, 28f);
            name.enableVertexGradient = false;
            name.color = Color.white;
            name.alignment = TextAlignmentOptions.Center;

            Play("idle");
            Report("open", $"portrait: {frames.Count} of {Math.Max(4, config.clips.Count)} loops painted ({string.Join(", ", frames.Keys)})");
        }

        /// <summary>A loop's sheet cut into its cells (left to right, top to bottom).</summary>
        void Cut(YoobaClip clip)
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
                sprite.name = $"yooba_{clip.name}_{i}";
                list.Add(sprite);
            }
            frames[clip.name] = list;
            rates[clip.name] = Mathf.Max(1f, clip.fps);
        }

        /// <summary>The slime from shapes, for loops that have no sheet yet: Video3's green blob, eyestalks, pink mouth.</summary>
        void BuildPlaceholder()
        {
            var holder = MenuGraphics.NewRect("Placeholder", body);
            MenuGraphics.Fill(holder);
            blob = Part(holder, "Blob", config.body, MenuShape.Rounded, new Vector2(0.18f, -0.1f), new Vector2(0.82f, 0.62f), 60f);
            Part(holder, "StalkL", config.body, MenuShape.Pill, new Vector2(0.33f, 0.55f), new Vector2(0.38f, 0.8f), 0f);
            Part(holder, "StalkR", config.body, MenuShape.Pill, new Vector2(0.62f, 0.55f), new Vector2(0.67f, 0.8f), 0f);
            Part(holder, "EyeL", config.body, MenuShape.Circle, new Vector2(0.24f, 0.72f), new Vector2(0.46f, 0.94f), 0f);
            Part(holder, "EyeR", config.body, MenuShape.Circle, new Vector2(0.54f, 0.72f), new Vector2(0.76f, 0.94f), 0f);
            var pink = new Color(0.93f, 0.68f, 0.78f, 1f);
            Part(holder, "BallL", pink, MenuShape.Circle, new Vector2(0.27f, 0.75f), new Vector2(0.43f, 0.91f), 0f);
            Part(holder, "BallR", pink, MenuShape.Circle, new Vector2(0.57f, 0.75f), new Vector2(0.73f, 0.91f), 0f);
            var pupil = new Color(0.1f, 0.3f, 0.1f, 1f);
            pupilL = Part(holder, "PupilL", pupil, MenuShape.Circle, new Vector2(0.32f, 0.8f), new Vector2(0.38f, 0.86f), 0f);
            pupilR = Part(holder, "PupilR", pupil, MenuShape.Circle, new Vector2(0.62f, 0.8f), new Vector2(0.68f, 0.86f), 0f);
            mouth = Part(holder, "Mouth", pink, MenuShape.Rounded, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.36f), 10f);
        }

        static Image Part(Transform parent, string name, Color color, MenuShape shape, Vector2 min, Vector2 max, float corner)
        {
            var rect = MenuGraphics.NewRect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(image, shape, corner);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Plays a loop; after 'seconds' (0 = for good) he goes back to idle.</summary>
        public void Play(string clip, float seconds = 0f)
        {
            if (string.IsNullOrEmpty(clip))
                clip = "idle";
            if (clip != "idle" && !frames.ContainsKey(clip) && frames.Count > 0 && !frames.ContainsKey("idle"))
                clip = "idle";
            Clip = clip;
            clipStarted = Time.unscaledTime;
            clipEnds = seconds > 0f ? Time.unscaledTime + seconds : -1f;
            bool painted = frames.ContainsKey(clip) || frames.ContainsKey("idle");
            art.enabled = painted;
            if (blob != null)
                blob.transform.parent.gameObject.SetActive(!painted);
            Report("clip", $"{clip} ({(frames.ContainsKey(clip) ? "painted" : painted ? "painted idle" : "placeholder")})");
        }

        /// <summary>Stops talking at once (the line was cut off): back to idle, ready for the next line.</summary>
        public void Silence()
        {
            Speaking = false;
            Play("idle");
        }

        /// <summary>
        /// Idle life (ui/yooba.json idleBehaviorSeconds): while he is quiet he blinks, looks about, hops, hums and sniffs
        /// by himself, so the counter is never a still picture. Returns the body's lift and tilt for this frame.
        /// </summary>
        void IdleBehaviors(out float lift, out float tilt, out float pupilScale, out float pupilShift)
        {
            lift = tilt = pupilShift = 0f;
            pupilScale = 1f;
            float now = Time.unscaledTime;
            bool free = !Speaking && Clip == "idle" && config.idleBehaviorSeconds > 0f;
            if (!free)
            {
                behavior = "";
                nextBehavior = -1f;
                return;
            }
            if (nextBehavior < 0f)
                nextBehavior = now + UnityEngine.Random.Range(0.4f, 1f) * config.idleBehaviorSeconds;
            if (behavior == "" && now >= nextBehavior)
            {
                behavior = Behaviors[UnityEngine.Random.Range(0, Behaviors.Length)];
                behaviorStart = now;
                behaviorLength = behavior == "blink" ? 0.35f : behavior == "hop" ? 0.7f : 1.4f;
                BehaviorsDone++;
                Report("idle", behavior);
            }
            if (behavior == "")
                return;
            float u = (now - behaviorStart) / behaviorLength;
            if (u >= 1f)
            {
                behavior = "";
                nextBehavior = now + UnityEngine.Random.Range(0.6f, 1.4f) * config.idleBehaviorSeconds;
                return;
            }
            switch (behavior)
            {
                case "blink": pupilScale = 1f - 0.9f * Mathf.Sin(u * Mathf.PI); break;
                case "look": pupilShift = Mathf.Sin(u * Mathf.PI * 2f) * 0.03f; tilt = Mathf.Sin(u * Mathf.PI * 2f) * 3f; break;
                case "hop": lift = Mathf.Sin(u * Mathf.PI) * 18f; break;
                case "hum": tilt = Mathf.Sin(u * Mathf.PI * 6f) * 4f; lift = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 6f)) * 4f; break;
                case "sniff": lift = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4f)) * 6f; pupilShift = 0.02f; break;
            }
        }

        void Update()
        {
            if (clipEnds > 0f && Time.unscaledTime >= clipEnds)
                Play("idle");
            float t = Time.unscaledTime - clipStarted;
            IdleBehaviors(out float lift, out float tilt, out float pupilScale, out float pupilShift);
            body.anchoredPosition = new Vector2(0f, lift);
            body.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (pupilL != null)
                PlacePupils(pupilScale, pupilShift);
            if (art.enabled)
            {
                var list = frames.TryGetValue(Clip, out var own) ? own : frames["idle"];
                float fps = rates.TryGetValue(frames.ContainsKey(Clip) ? Clip : "idle", out var rate) ? rate : 12f;
                art.sprite = list[Mathf.FloorToInt(t * fps) % list.Count];
                body.localScale = Vector3.one;
                return;
            }
            // The placeholder: a slow wobble; talking opens the mouth, laughing bounces, coughing heaves.
            float wobble = Mathf.Sin(Time.unscaledTime * 2.6f);
            float squash = Clip == "laugh" ? 0.06f * Mathf.Abs(Mathf.Sin(t * 14f)) : Clip == "cough" ? 0.1f * Mathf.Max(0f, Mathf.Sin(t * 7f)) : 0f;
            body.localScale = new Vector3(1f + 0.025f * wobble + squash, 1f - 0.025f * wobble - squash, 1f);
            float open = Clip == "talk" ? 0.5f + 0.5f * Mathf.Sin(t * 18f) : Clip == "laugh" ? 0.8f + 0.2f * Mathf.Sin(t * 22f) : Clip == "cough" ? Mathf.Max(0f, Mathf.Sin(t * 7f)) : 0f;
            var m = mouth.rectTransform;
            m.anchorMin = new Vector2(0.3f, 0.33f - 0.12f * open);
            m.anchorMax = new Vector2(0.7f, 0.36f);
        }

        void PlacePupils(float scale, float shift)
        {
            foreach (var (pupil, x) in new[] { (pupilL, 0.32f), (pupilR, 0.62f) })
            {
                var r = pupil.rectTransform;
                r.anchorMin = new Vector2(x + shift, 0.83f - 0.03f * scale);
                r.anchorMax = new Vector2(x + 0.06f + shift, 0.83f + 0.03f * scale);
            }
        }

        // ─── Saying lines ─────────────────────────────────────────────

        /// <summary>
        /// Says one line: his voice and subtitle with his loop, then the hero's one-liner back if it has one. Shows
        /// words through 'subtitle' (null = hide). Ends early when 'skipped' says so (the voice plays out).
        /// </summary>
        public IEnumerator Say(YoobaLine line, Action<string> subtitle, Func<bool> skipped = null)
        {
            if (line == null)
                yield break;
            Speaking = true;
            float length = Seconds(line.sound, line.text);
            Sfx.Play(line.sound);
            Play(string.IsNullOrEmpty(line.clip) ? "talk" : line.clip, length);
            subtitle?.Invoke($"{config.speaker}: {line.text}");
            Report("line", $"yooba ({line.moment}{(line.moment == YoobaMoment.Floor || line.moment == YoobaMoment.NearWarden ? " " + line.floor.ToString(CultureInfo.InvariantCulture) : "")}, {Clip}): {line.text} ({line.sound}{(Sfx.Has(line.sound) ? "" : ", no clip")})");
            yield return Wait(length + config.pause, skipped);
            if (!string.IsNullOrEmpty(line.reply) && !(skipped != null && skipped()))
            {
                float reply = Seconds(line.reply, line.replyText);
                Sfx.Play(line.reply);
                Play("idle");
                subtitle?.Invoke($"{config.heroSpeaker}: {line.replyText}");
                Report("line", $"hero: {line.replyText} ({line.reply}{(Sfx.Has(line.reply) ? "" : ", no clip")})");
                yield return Wait(reply + config.pause, skipped);
            }
            Play("idle");
            Speaking = false;
        }

        float Seconds(string sound, string text)
        {
            float clip = Sfx.LengthOf(sound);
            return clip > 0f ? clip : Mathf.Max(1.5f, (text ?? "").Length * config.secondsPerCharacter);
        }

        static IEnumerator Wait(float seconds, Func<bool> skipped)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !(skipped != null && skipped()))
                yield return null;
        }

        // ─── Which line ───────────────────────────────────────────────

        static YoobaConfig Config => GameDefinition.Instance != null ? GameDefinition.Instance.yooba : null;

        /// <summary>Yooba is on: configured, and (for the Shaft) he has met the hero.</summary>
        public static bool Enabled => Config != null && Config.enabled;

        /// <summary>His hello lines, in order.</summary>
        public static List<YoobaLine> HelloLines()
        {
            var list = new List<YoobaLine>();
            if (Enabled)
                foreach (var line in Config.lines)
                    if (line != null && line.moment == YoobaMoment.Hello)
                        list.Add(line);
            return list;
        }

        public static int GrudgesHeld()
        {
            int total = 0;
            foreach (var pair in UpgradeProgress.Owned())
                total += Mathf.Max(0, pair.Value);
            return total;
        }

        /// <summary>
        /// The line for a Shaft climb from 'from' to 'to' with the next Warden at 'wardenFloor' (0 = none): the first
        /// moment that matches, in order milestone floor, near the Warden, a death, a new Grudge, gossip. Inside a
        /// moment the first line not said yet wins; once all are said, a repeatable one (not 'once') comes back.
        /// </summary>
        public static YoobaLine PickBetweenFloors(int from, int to, int wardenFloor)
        {
            if (!Enabled)
                return null;
            int grudges = GrudgesHeld();
            int seen = YoobaProgress.GrudgesSeen;
            var order = new[] { YoobaMoment.Floor, YoobaMoment.NearWarden, YoobaMoment.Death, YoobaMoment.Grudge, YoobaMoment.Chatter };
            foreach (var moment in order)
            {
                if (moment == YoobaMoment.Death && !YoobaProgress.HeroDied)
                    continue;
                if (moment == YoobaMoment.Grudge && !(seen >= 0 && grudges > seen))
                    continue;
                var candidates = new List<YoobaLine>();
                foreach (var line in Config.lines)
                {
                    if (line == null || line.moment != moment)
                        continue;
                    if (moment == YoobaMoment.Floor && !(from > line.floor && to <= line.floor))
                        continue;
                    if (moment == YoobaMoment.NearWarden && !(wardenFloor > 0 && to > wardenFloor && to - wardenFloor <= line.floor))
                        continue;
                    candidates.Add(line);
                }
                // The closest call first (the door before the stairs).
                if (moment == YoobaMoment.NearWarden)
                    candidates.Sort((a, b) => a.floor.CompareTo(b.floor));
                var line0 = Choose(candidates);
                if (line0 != null)
                    return line0;
            }
            return null;
        }

        /// <summary>The lines of a moment that fit now: its item (when asked for), the deaths counted and its requires.</summary>
        public static List<YoobaLine> Candidates(YoobaMoment moment, string item = null)
        {
            var list = new List<YoobaLine>();
            if (!Enabled)
                return list;
            int deaths = YoobaProgress.Deaths;
            foreach (var line in Config.lines)
            {
                if (line == null || line.moment != moment)
                    continue;
                if (item != null && line.item != item)
                    continue;
                if (item == null && !string.IsNullOrEmpty(line.item))
                    continue;
                if (line.minDeaths > deaths || !Requirements.Met(line.requires))
                    continue;
                list.Add(line);
            }
            return list;
        }

        /// <summary>The first line of a moment not said yet that fits now (null = none left): for lines that come once, like a reaction to a find.</summary>
        public static YoobaLine PickNew(YoobaMoment moment, string item = null)
        {
            foreach (var line in Candidates(moment, item))
                if (!YoobaProgress.HasSaid(line.sound))
                    return line;
            return null;
        }

        /// <summary>
        /// Something to mutter while the hero looks at the shelf: a running gag the hero's climb has just unlocked (its requires
        /// hold, not said yet; the latest first), else a line not said yet, else any repeatable line, else null.
        /// </summary>
        public static YoobaLine PickIdle()
        {
            var all = Candidates(YoobaMoment.ShopIdle);
            var fresh = all.FindAll(l => !YoobaProgress.HasSaid(l.sound));
            for (int i = fresh.Count - 1; i >= 0; i--)
                if (fresh[i].requires.Count > 0)
                    return fresh[i];
            if (fresh.Count > 0)
                return fresh[0];
            var again = all.FindAll(l => !l.once);
            return again.Count > 0 ? again[UnityEngine.Random.Range(0, again.Count)] : null;
        }

        /// <summary>A shop line for the moment: the next one not said in this visit's rotation. 'item' picks the lines made for that good.</summary>
        public static YoobaLine PickShop(YoobaMoment moment, string item = null)
        {
            if (!Enabled)
                return null;
            var candidates = Candidates(moment, item);
            if (candidates.Count == 0 && item != null)
                candidates = Candidates(moment);
            if (candidates.Count == 0)
                return null;
            int key = (int)moment * 1000 + (item != null ? item.GetHashCode() & 0x3ff : 0);
            shopTurn.TryGetValue(key, out int turn);
            shopTurn[key] = turn + 1;
            return candidates[turn % candidates.Count];
        }

        static readonly Dictionary<int, int> shopTurn = new Dictionary<int, int>();

        static YoobaLine Choose(List<YoobaLine> candidates)
        {
            foreach (var line in candidates)
                if (!YoobaProgress.HasSaid(line.sound))
                    return line;
            var again = candidates.FindAll(l => !l.once);
            return again.Count > 0 ? again[UnityEngine.Random.Range(0, again.Count)] : null;
        }

        /// <summary>After a Shaft: the line is said and the news is old.</summary>
        /// <summary>Remembers that a shop line was said (the ones that come once).</summary>
        public static void Said(YoobaLine line)
        {
            if (line != null && line.once)
                YoobaProgress.MarkSaid(line.sound);
        }

        public static void Heard(YoobaLine line)
        {
            if (line != null)
                YoobaProgress.MarkSaid(line.sound);
            YoobaProgress.HeroDied = false;
            YoobaProgress.GrudgesSeen = GrudgesHeld();
        }

        void Report(string kind, string detail) => Announce(kind, detail);

        public static void Announce(string kind, string detail)
        {
            Debug.Log($"[Yooba] {kind}: {detail}");
            try
            {
                Reported?.Invoke(kind, detail);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
