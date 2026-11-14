using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using SickOfIt.Core.Audio;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Portraits
{
    /// <summary>The four expressions every portrait is painted in (Docs/Story.md "Portraits").</summary>
    public static class PortraitMood
    {
        public const string Neutral = "neutral", Smug = "smug", Laughing = "laughing", Shocked = "shocked";
        public static readonly string[] All = { Neutral, Smug, Laughing, Shocked };

        /// <summary>Maps the words a writer might use onto the four painted moods.</summary>
        public static string Normalize(string mood)
        {
            switch ((mood ?? "").Trim().ToLowerInvariant())
            {
                case "smug": case "angry": case "sly": case "annoyed": case "sneer": case "cross": return Smug;
                case "laughing": case "laugh": case "happy": case "amused": case "cackle": return Laughing;
                case "shocked": case "surprised": case "scared": case "afraid": case "gasp": return Shocked;
                default: return Neutral;
            }
        }

        static readonly Regex Tags = new Regex(@"\[([^\]]*)\]", RegexOptions.Compiled);

        /// <summary>A line's mood: its own 'mood' field, else read from its delivery tags and words.</summary>
        public static string Of(SoundDef sound)
        {
            if (sound == null)
                return Neutral;
            if (!string.IsNullOrEmpty(sound.mood))
                return Normalize(sound.mood);
            return Infer(sound.prompt);
        }

        /// <summary>The same reading the audio.json writer script makes, for lines left without a mood.</summary>
        public static string Infer(string prompt)
        {
            string text = (prompt ?? "").ToLowerInvariant();
            var tags = new System.Text.StringBuilder();
            foreach (Match m in Tags.Matches(text))
                tags.Append(m.Groups[1].Value).Append(' ');
            string t = tags.ToString();
            string words = Tags.Replace(text, " ");
            if (t.Contains("laugh") || t.Contains("chuckl") || t.Contains("giggl") || t.Contains("cackl") || t.Contains("snicker") || words.Contains("hahaha") || words.Contains("ha ha"))
                return Laughing;
            if (t.Contains("gasp") || t.Contains("shock") || t.Contains("surpris") || t.Contains("scream") || t.Contains("panic") || t.Contains("yell") || words.Contains("?!") || words.Contains("!?"))
                return Shocked;
            if (t.Contains("smug") || t.Contains("angry") || t.Contains("snort") || t.Contains("scoff") || t.Contains("sneer") || t.Contains("annoy") || t.Contains("furious") || t.Contains("sarcastic") || t.Contains("proud"))
                return Smug;
            return Neutral;
        }
    }

    /// <summary>
    /// The voiced line speaking right now, for the portraits (the overlay and Yooba's shop frame): who, what mood, and how open the
    /// mouth is. The mouth follows the clip's own loudness (its samples read once into a 50 Hz envelope); a clip whose samples
    /// can't be read (streamed, or not decoded yet) flaps on a syllable-like rhythm instead. It closes the moment the line ends.
    /// Presentation only: unscaled time, nothing saved.
    /// </summary>
    public static class PortraitVoice
    {
        /// <summary>Seconds per envelope window.</summary>
        public const float Window = 0.02f;
        const float OpenAbove = 0.22f, CloseBelow = 0.12f;
        /// <summary>A line is "over" this long after its clip's end at the latest; the mouth is shut well inside the 0.2 s the check allows.</summary>
        const float EndGrace = 0.04f;

        public static SoundDef Current { get; private set; }
        public static string Voice => Current != null ? Current.voice : "";
        public static string Mood { get; private set; } = PortraitMood.Neutral;
        public static float StartedAt { get; private set; } = -999f;
        public static float Length { get; private set; }
        /// <summary>The loudness read from the clip (false = the rhythm stand-in).</summary>
        public static bool FromClip { get; private set; }
        public static int LinesStarted { get; private set; }
        /// <summary>Playtest and log: (what, detail).</summary>
        public static event Action<SoundDef> Started;

        static float[] envelope;
        static readonly Dictionary<AudioClip, float[]> cache = new Dictionary<AudioClip, float[]>();
        static bool open;
        static int openFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Current = null;
            StartedAt = -999f;
            LinesStarted = 0;
            cache.Clear();
            Sfx.VoiceStarted -= OnVoiceStarted;
            Sfx.VoiceStarted += OnVoiceStarted;
        }

        static void OnVoiceStarted(SoundDef sound)
        {
            Current = sound;
            Mood = PortraitMood.Of(sound);
            StartedAt = Time.unscaledTime;
            var clip = sound.clip;
            Length = clip != null && clip.length > 0f ? clip.length : 2f;
            envelope = clip != null ? EnvelopeOf(clip) : null;
            FromClip = envelope != null;
            open = false;
            openFrame = -1;
            LinesStarted++;
            Started?.Invoke(sound);
        }

        /// <summary>A line is being spoken: inside its clip's length and the voice source is still playing it.</summary>
        public static bool Speaking
        {
            get
            {
                if (Current == null)
                    return false;
                float t = Time.unscaledTime - StartedAt;
                if (t >= Length + EndGrace)
                    return false;
                return t < 0.15f || Sfx.VoicePlaying;
            }
        }

        /// <summary>Seconds since the line ended (0 while it is spoken; large when none was).</summary>
        public static float SinceEnd => Speaking ? 0f : Mathf.Max(0f, Time.unscaledTime - (StartedAt + Length));

        /// <summary>The loudness now, 0 to 1 (0 when no line is spoken).</summary>
        public static float Level()
        {
            if (!Speaking)
                return 0f;
            float t = Time.unscaledTime - StartedAt;
            if (envelope != null && envelope.Length > 0)
            {
                float f = t / Window;
                int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, envelope.Length - 1);
                int j = Mathf.Min(i + 1, envelope.Length - 1);
                return Mathf.Lerp(envelope[i], envelope[j], Mathf.Clamp01(f - i));
            }
            // A stand-in rhythm: syllables about 4.5 a second, phrases about 1.2 s, a pause between them.
            float h = Current != null ? (Current.id.GetHashCode() & 0xFF) / 255f * 6.28f : 0f;
            float syllable = 0.5f + 0.5f * Mathf.Sin(t * 28f + h);
            float phrase = 0.5f + 0.5f * Mathf.Sin(t * 5.2f + h * 0.5f);
            return Mathf.Clamp01(syllable * (0.35f + 0.9f * phrase));
        }

        /// <summary>
        /// The mouth is open now: the loudness crossed its upper mark (and stays open until it drops under the lower one), so it flaps with
        /// the voice rather than shimmering. Shut at once when the line is over. Cached per frame: any number of portraits may ask.
        /// </summary>
        public static bool MouthOpen
        {
            get
            {
                if (openFrame == Time.frameCount)
                    return open;
                openFrame = Time.frameCount;
                if (!Speaking)
                    return open = false;
                float level = Level();
                if (open && level < CloseBelow)
                    open = false;
                else if (!open && level > OpenAbove)
                    open = true;
                return open;
            }
        }

        /// <summary>Reads a clip's loudness once into windows of <see cref="Window"/> seconds, scaled so the loud parts reach 1 (null when its samples can't be read).</summary>
        static float[] EnvelopeOf(AudioClip clip)
        {
            if (cache.TryGetValue(clip, out var known))
                return known;
            float[] env = null;
            try
            {
                if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0)
                {
                    int channels = Mathf.Max(1, clip.channels);
                    var data = new float[clip.samples * channels];
                    if (clip.GetData(data, 0))
                    {
                        int per = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * Window));
                        int count = Mathf.Max(1, clip.samples / per);
                        env = new float[count];
                        float peak = 0f;
                        for (int w = 0; w < count; w++)
                        {
                            double sum = 0;
                            int from = w * per;
                            int to = Mathf.Min(clip.samples, from + per);
                            for (int s = from; s < to; s++)
                            {
                                float v = 0f;
                                for (int c = 0; c < channels; c++)
                                    v += data[s * channels + c];
                                v /= channels;
                                sum += v * v;
                            }
                            float rms = Mathf.Sqrt((float)(sum / Math.Max(1, to - from)));
                            env[w] = rms;
                            peak = Mathf.Max(peak, rms);
                        }
                        if (peak < 1e-4f)
                            env = null;
                        else
                        {
                            // Scale by a high mark, not the peak, so one pop doesn't leave the rest of the line shut.
                            var sorted = (float[])env.Clone();
                            Array.Sort(sorted);
                            float loud = Mathf.Max(1e-4f, sorted[Mathf.Min(sorted.Length - 1, Mathf.FloorToInt(sorted.Length * 0.9f))]);
                            for (int w = 0; w < count; w++)
                                env[w] = Mathf.Clamp01(env[w] / loud * 0.8f);
                        }
                    }
                }
            }
            catch (Exception)
            {
                env = null;
            }
            if (cache.Count > 48)
                cache.Clear();
            cache[clip] = env;
            return env;
        }
    }

    /// <summary>Finds a character's portrait set in ui/portraits.json and picks a painting.</summary>
    public static class PortraitLibrary
    {
        public static PortraitsConfig Config => GameDefinition.Instance != null ? GameDefinition.Instance.portraits : null;

        /// <summary>The character whose lines are spoken by this audio.json voice, or null (the announcer, the bosses, souls).</summary>
        public static PortraitCharacter ForVoice(string voice)
        {
            var cfg = Config;
            if (cfg == null || !cfg.enabled || string.IsNullOrEmpty(voice))
                return null;
            foreach (var c in cfg.characters)
                if (c != null && c.voice == voice)
                    return c;
            return null;
        }

        public static PortraitCharacter ById(string id)
        {
            var cfg = Config;
            if (cfg == null)
                return null;
            foreach (var c in cfg.characters)
                if (c != null && c.id == id)
                    return c;
            return null;
        }

        /// <summary>The painting for a mood and mouth: a mood not painted falls back to neutral, a missing open frame to the closed one. Null = nothing painted.</summary>
        public static Sprite Pick(PortraitCharacter c, string mood, bool open)
        {
            if (c == null)
                return null;
            PortraitFrame frame = null, neutral = null;
            foreach (var f in c.frames)
            {
                if (f == null)
                    continue;
                if (PortraitMood.Normalize(f.mood) == mood)
                    frame = f;
                if (PortraitMood.Normalize(f.mood) == PortraitMood.Neutral)
                    neutral = f;
            }
            frame = frame ?? neutral;
            if (frame == null)
                return null;
            return open && frame.open != null ? frame.open : frame.closed != null ? frame.closed : neutral != null ? neutral.closed : null;
        }

        /// <summary>How many different moods this character has painted frames for.</summary>
        public static int PaintedMoods(PortraitCharacter c)
        {
            var set = new HashSet<string>();
            if (c != null)
                foreach (var f in c.frames)
                    if (f != null && f.closed != null)
                        set.Add(PortraitMood.Normalize(f.mood));
            return set.Count;
        }
    }
}
