using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero talks back (Docs/Story.md "The hero talks back"): eleven moments in play (the first graze, a graze streak of 5, a kill streak of 5,
    /// down to one heart, healed, a rewind that undoes a death, a dash through a bullet, eight idle seconds, a new floor, the Overlord sighted, and
    /// a buy at Yooba's) answer with a voiced, deadpan line and a one-line caption in a bubble above him for 1.5 s (<see cref="GameplayHud.ShowBark"/>).
    /// The lines are <see cref="HeroBarkLines"/> (written by Tools/Audio/hero_barks.py).
    /// Rules: <b>priority</b> (events landing in the same frame: the highest wins), a <b>15 s global cooldown</b> between barks (the shop's too),
    /// <b>no line twice per session</b> (<see cref="Said"/>), and <b>never over another character's line</b> (<see cref="Sfx.VoiceBusy"/>: a bark
    /// waits a few seconds for the voice to finish when it has patience, else it is dropped; an undone death queues behind it so the answer still lands at
    /// once). Barks are never spoken in scenes, beats, locked input or while dying. Presentation only: real time, nothing here is saved or rewound, so a
    /// replayed future never speaks again; the caption starts on the frame the voice really starts.
    /// </summary>
    public sealed class HeroBarks : MonoBehaviour
    {
        public const float Cooldown = 15f;
        public const float CaptionSeconds = 1.5f;
        const float IdleSeconds = 8f;
        const float ArrivalDelay = 3.2f;
        static readonly Color CaptionColor = new Color(0.97f, 0.92f, 0.82f, 1f);

        /// <summary>Lines spoken this session (never again).</summary>
        public static readonly HashSet<string> Said = new HashSet<string>();
        /// <summary>Barks spoken per event key (the playtest reads these).</summary>
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        /// <summary>Every bark of the session, in order: the event key, the line id and the real time it was decided.</summary>
        public static readonly List<(string key, string line, float at)> Log = new List<(string, string, float)>();
        public static string LastLine { get; private set; } = "";
        public static string LastKey { get; private set; } = "";
        /// <summary>Real time the last bark was decided (-999 before any).</summary>
        public static float LastAt { get; private set; } = -999f;
        /// <summary>Real time the last bark's caption/voice started.</summary>
        public static float LastShownAt { get; private set; } = -999f;
        /// <summary>The caption words of the last bark.</summary>
        public static string LastCaption { get; private set; } = "";

        GameplayWorld world;
        GameClock clock;
        bool subscribed, armedHooked;
        readonly HashSet<string> fired = new HashSet<string>();
        readonly HashSet<int> killed = new HashSet<int>();
        readonly List<string> raised = new List<string>(4);
        string pendingKey;
        float pendingUntil;
        string armedLine, armedText;
        float armedAt;
        int killsSinceHit;
        int lastHealth = -1;
        float healthGraceUntil, startedAt;
        bool wasDying, dashHandled, arrivalDone;
        Vector2 idleAnchor;
        float idleSince = -1f, lastActionAt;
        PlayerHealth health;

        public static HeroBarks Create(Transform hud, GameplayWorld world)
        {
            var go = new GameObject("HeroBarks");
            go.transform.SetParent(hud, false);
            var b = go.AddComponent<HeroBarks>();
            b.world = world;
            return b;
        }

        void OnEnable()
        {
            if (subscribed) return;
            subscribed = true;
            startedAt = Time.unscaledTime;
            GameplayTelemetry.Recorded += OnRecorded;
        }

        void OnDisable()
        {
            if (!subscribed) return;
            subscribed = false;
            GameplayTelemetry.Recorded -= OnRecorded;
            if (clock != null) { clock.OnRewindStarted -= OnRewindStarted; clock.OnRewindStopped -= OnRewindStopped; clock = null; }
            Unarm();
        }

        /// <summary>Barks belong to the played floors: not scenes, test benches or the replay modes' scripted setups.</summary>
        bool Active => world != null && string.IsNullOrEmpty(world.SceneId);

        // ─── Events ───────────────────────────────────────────────────

        void OnRecorded(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            if (world == null || log == null || log.World != world || !Active) return;
            switch (e.Kind)
            {
                case "graze":
                {
                    // Detail ends with "count N": the level's graze count; a rewind's replay records the same one again.
                    int count = CountOf(e.Detail);
                    if (e.Value == 5 && fired.Add("graze5:" + e.Detail)) Raise("graze5");
                    else if (count == 1 && fired.Add("firstGraze")) Raise("firstGraze");
                    break;
                }
                case "playerHit":
                    killsSinceHit = 0;
                    lastActionAt = Time.unscaledTime;
                    break;
                case "death":
                    if (e.Detail == "Damage" && killed.Add(e.Value) && IsNamedEnemy(e.Subject) && ++killsSinceHit >= 5)
                    {
                        killsSinceHit = 0;
                        Raise("kill5");
                    }
                    break;
                case "move":
                    if (e.Subject == "dash") { dashHandled = false; lastActionAt = Time.unscaledTime; }
                    break;
                case "weaponShot":
                case "swing":
                    lastActionAt = Time.unscaledTime;
                    break;
                case "taunt":
                    if (fired.Add("overlord")) Raise("overlord");
                    break;
                case "spawn":
                    if (e.Subject != null && e.Subject.StartsWith("overlordFar") && fired.Add("overlord")) Raise("overlord");
                    break;
                case "floor":
                    if (e.Value > 0 && arrivalDone && world.Now > ArrivalDelay && fired.Add("floor:" + e.Value)) Raise("floor");
                    break;
            }
        }

        static int CountOf(string detail)
        {
            int at = (detail ?? "").LastIndexOf("count ", System.StringComparison.Ordinal);
            return at >= 0 && int.TryParse(detail.Substring(at + 6), out int n) ? n : -1;
        }

        static bool IsNamedEnemy(string defId)
        {
            var content = GameContent.Current;
            if (content == null || string.IsNullOrEmpty(defId) || !content.Entities.TryGetValue(defId, out var def))
                return false;
            return def.team == Team.Enemy && !string.IsNullOrEmpty(def.displayName);
        }

        void OnRewindStarted()
        {
            if (!Active) return;
            lastActionAt = Time.unscaledTime;
            // A rewind that saves him: he was dying when it started.
            if (wasDying) Raise("undo");
        }

        void OnRewindStopped()
        {
            lastHealth = -1;
            healthGraceUntil = Time.unscaledTime + 0.6f;
            idleSince = -1f;
        }

        // ─── Per frame ────────────────────────────────────────────────

        void Update()
        {
            if (world == null || !Active) return;
            if (clock == null && GameClock.Instance != null)
            {
                clock = GameClock.Instance;
                clock.OnRewindStarted += OnRewindStarted;
                clock.OnRewindStopped += OnRewindStopped;
            }
            if (Time.timeScale <= 0f) return;
            float now = Time.unscaledTime;
            bool rewinding = clock != null && clock.IsRewinding;

            if (armedLine != null && now - armedAt > 4f)
                ShowCaption(armedText);

            if (!rewinding)
                Detect(now);
            Resolve(now);
        }

        void Detect(float now)
        {
            var hero = PlayerController.Active;
            if (hero == null) return;
            if (health == null)
            {
                health = hero.GetComponentInChildren<PlayerHealth>();
                if (health == null) health = hero.GetComponentInParent<PlayerHealth>();
            }
            bool dying = health != null && health.DeathTimeLeft >= 0f;
            wasDying = dying;
            if (dying) { idleSince = -1f; return; }

            // Hearts: down to one, and healed.
            if (health != null && now - startedAt > 1.5f && now >= healthGraceUntil)
            {
                int cur = health.CurrentHealth;
                if (lastHealth >= 0)
                {
                    if (cur == 1 && lastHealth > 1 && health.MaxHealth > 1) Raise("lastHeart");
                    else if (cur > lastHealth && lastHealth > 0) Raise("healed");
                }
                lastHealth = cur;
            }

            // A dash through a bullet: a hostile shot overlaps him while he is phased.
            if (hero.DashInvulnerable && !dashHandled && world.Player != null)
            {
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || e.Team != Team.Enemy || e.FindAbility<ProjectileAbility>() == null) continue;
                    if (e.GapTo(world.Player) <= 0.3f)
                    {
                        dashHandled = true;
                        Raise("dashThrough");
                        break;
                    }
                }
            }
            else if (!hero.DashInvulnerable)
            {
                dashHandled = false;
            }

            // The floor arrived at (floors only; the Meadow has none).
            if (!arrivalDone && world.Now > ArrivalDelay)
            {
                arrivalDone = true;
                if (world.Stage != null && world.Stage.HasFloors)
                {
                    fired.Add("floor:" + world.Stage.Floors.start);
                    Raise("floor");
                }
            }

            // Eight quiet seconds: he hasn't moved, shot or dashed.
            Vector2 at = hero.transform.position;
            if (idleSince < 0f || (at - idleAnchor).sqrMagnitude > 0.04f || lastActionAt > idleSince)
            {
                idleAnchor = at;
                idleSince = Mathf.Max(now, lastActionAt);
            }
            else if (now - idleSince >= IdleSeconds && health != null && health.IsAlive)
            {
                idleSince = now;
                Raise("idle");
            }
        }

        // ─── Rules ────────────────────────────────────────────────────

        void Raise(string key)
        {
            if (!HeroBarkLines.Table.ContainsKey(key)) return;
            raised.Add(key);
        }

        /// <summary>How long an event waits for another voice to finish (seconds); 0 drops it.</summary>
        static float Patience(string key)
        {
            switch (key)
            {
                case "overlord": return 8f;
                case "floor": return 6f;
                case "graze5": case "kill5": case "lastHeart": case "healed": case "dashThrough": return 1f;
                default: return 0f;
            }
        }

        bool Allowed(string key)
        {
            if (world.RewindLocked || world.Now < world.InputLockedUntil) return false;
            if (world.Hud != null && world.Hud.LetterboxOn) return false;
            if (key != "undo" && health != null && health.DeathTimeLeft >= 0f) return false;
            return true;
        }

        void Resolve(float now)
        {
            string key = null;
            if (raised.Count > 0)
            {
                // Events in one frame: the highest priority wins, the rest are dropped.
                int best = -1;
                foreach (var k in raised)
                {
                    int p = HeroBarkLines.Table[k].Priority;
                    if (p > best) { best = p; key = k; }
                }
                raised.Clear();
                pendingKey = null;
            }
            else if (pendingKey != null)
            {
                if (now > pendingUntil) { pendingKey = null; return; }
                key = pendingKey;
            }
            if (key == null) return;

            if (!Allowed(key) || now - LastAt < Cooldown) { pendingKey = null; return; }
            // Never over another character's line: wait for it (if the event has patience), else drop. An undone death queues behind it.
            if (Sfx.VoiceBusy && key != "undo")
            {
                float patience = Patience(key);
                if (patience > 0f)
                {
                    if (pendingKey != key) pendingUntil = now + patience;
                    pendingKey = key;
                }
                return;
            }
            pendingKey = null;
            Speak(key, now);
        }

        void Speak(string key, float now)
        {
            string line = Pick(key);
            if (line == null) return;
            Claim(key, line, now);
            string text = Sfx.SpokenText(line);
            if (world.Telemetry != null)
                world.Telemetry.Record("bark", key, world.Stage != null && world.Stage.HasFloors ? world.Stage.Floor : 0, line);
            if (Sfx.Has(line))
            {
                // The caption starts when the voice does (a queued line waits its turn), so the two never part.
                armedLine = line;
                armedText = text;
                armedAt = now;
                if (!armedHooked) { Sfx.Played += OnSoundPlayed; armedHooked = true; }
                Sfx.Play(line);
            }
            else
            {
                ShowCaption(text);
            }
        }

        /// <summary>The first line of the event nobody has said this session, picked at random among them (null when all are said).</summary>
        static string Pick(string key)
        {
            if (!HeroBarkLines.Table.TryGetValue(key, out var bark)) return null;
            var left = new List<string>(bark.Lines.Length);
            foreach (var id in bark.Lines)
                if (!Said.Contains(id)) left.Add(id);
            return left.Count == 0 ? null : left[Random.Range(0, left.Count)];
        }

        static void Claim(string key, string line, float now)
        {
            Said.Add(line);
            LastAt = now;
            LastKey = key;
            LastLine = line;
            Counts[key] = (Counts.TryGetValue(key, out int n) ? n : 0) + 1;
            Log.Add((key, line, now));
        }

        void OnSoundPlayed(Sfx.PlayedSound s)
        {
            if (armedLine != null && s.id == armedLine)
                ShowCaption(armedText);
        }

        void ShowCaption(string text)
        {
            Unarm();
            LastShownAt = Time.unscaledTime;
            LastCaption = text;
            if (world != null && world.Hud != null)
                world.Hud.ShowBark(text, CaptionColor, Time.unscaledTime + CaptionSeconds);
            if (world != null && world.Telemetry != null)
                world.Telemetry.Record("barkSpoken", LastKey, 0, LastLine);
        }

        void Unarm()
        {
            armedLine = null;
            if (armedHooked) { Sfx.Played -= OnSoundPlayed; armedHooked = false; }
        }

        /// <summary>Forgets the session (the playtest's start: a new session says everything again).</summary>
        public static void ResetSession()
        {
            Said.Clear();
            Counts.Clear();
            Log.Clear();
            LastLine = LastKey = LastCaption = "";
            LastAt = LastShownAt = -999f;
        }

        // ─── Yooba's shop ─────────────────────────────────────────────

        /// <summary>
        /// A buy at Yooba's: the hero answers with a voiced line (not over Yooba's: the caller waits until <see cref="Sfx.VoiceBusy"/> is false).
        /// Returns the caption's words, or null when the cooldown, the lines or a busy voice say no. The caller shows the caption for
        /// <see cref="CaptionSeconds"/> where the shop shows Yooba's subtitle.
        /// </summary>
        public static string SpeakInShop()
        {
            float now = Time.unscaledTime;
            if (now - LastAt < Cooldown || Sfx.VoiceBusy) return null;
            string line = Pick("shop");
            if (line == null) return null;
            Claim("shop", line, now);
            LastShownAt = now;
            LastCaption = Sfx.SpokenText(line);
            if (Sfx.Has(line)) Sfx.Play(line);
            return LastCaption;
        }
    }
}
