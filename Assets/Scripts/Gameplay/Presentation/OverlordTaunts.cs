using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The Overlord watches the climb (Docs/Story.md "The Overlord watches the climb", docs/world-bible.md): on Floors 1000-997 he shows up on the
    /// screen's upper edge, animated and voiced, and answers what the hero just did: the floor entered, a death (by imp, shot, hazard or fall), a
    /// rewind, a rewind that undoes a death, thirty seconds untouched, the finale. At most one taunt per 20 s (an undone death answers at once),
    /// and no line twice in a session (<see cref="Said"/>; the table is <see cref="OverlordTauntLines"/>). Presentation only: real time, the spoken
    /// set is not rewound (a replay never repeats a line), and nothing here changes the simulation but his own spawn.
    /// A rewind cannot spawn him (the world refuses spawns while time runs backward), so rewind lines are voice and caption at once, the death
    /// rewind shows <see cref="OverlordEscape"/>'s silhouette, and when the rewind ends he climbs out of the top (the Flee clip).
    /// </summary>
    public sealed class OverlordTaunts : MonoBehaviour
    {
        public const float Cooldown = 20f;
        const float StreakSeconds = 30f;
        static readonly Color Red = new Color(0.88f, 0.38f, 0.35f, 1f);
        /// <summary>Lines spoken this session (never again).</summary>
        public static readonly HashSet<string> Said = new HashSet<string>();
        /// <summary>Taunts shown per key, floors entered: the playtest reads these.</summary>
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public static string LastLine { get; private set; } = "";
        public static string LastKey { get; private set; } = "";
        public static float LastAt { get; private set; } = -1f;
        public static bool Visible { get; private set; }
        /// <summary>The animated figure's on-screen height in pixels, measured from his renderer when a taunt starts (0 before any).</summary>
        public static float SizePx { get; private set; }

        GameplayWorld world;
        float lastTauntAt = -999f, lastHitAt, startedAt;
        string lastCause = "";
        bool entered, dying, wasDying, fleeNext, finaleSaid, subscribed;
        Entity figure;
        float figureUntil;
        GameClock clock;

        public static OverlordTaunts Create(Transform hud, GameplayWorld world)
        {
            var go = new GameObject("OverlordTaunts");
            go.transform.SetParent(hud, false);
            var t = go.AddComponent<OverlordTaunts>();
            t.world = world;
            return t;
        }

        /// <summary>Floors 1000-997 only.</summary>
        bool Active
        {
            get
            {
                var stage = world != null ? world.Stage : null;
                return stage != null && stage.HasFloors && stage.Floors.start >= 997 && stage.Floors.start <= 1000 && string.IsNullOrEmpty(world.SceneId);
            }
        }

        void OnEnable()
        {
            if (subscribed) return;
            subscribed = true;
            GameplayTelemetry.Recorded += OnRecorded;
            startedAt = lastHitAt = Time.unscaledTime;
        }

        void OnDisable()
        {
            if (!subscribed) return;
            subscribed = false;
            GameplayTelemetry.Recorded -= OnRecorded;
            if (clock != null) { clock.OnRewindStarted -= OnRewindStarted; clock.OnRewindStopped -= OnRewindStopped; clock = null; }
            Visible = false;
        }

        void OnRecorded(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            if (world == null || log == null || log.World != world || !Active) return;
            if (e.Kind == "damage" && e.Subject == "player")
            {
                lastCause = (e.Detail ?? "").ToLowerInvariant();
                lastHitAt = Time.unscaledTime;
            }
            else if (e.Kind == "signal" && !finaleSaid && e.Subject != null && e.Subject.StartsWith("floors:") && e.Subject.EndsWith(":finale"))
            {
                finaleSaid = true;
                Say("finale", "Point", false);
            }
        }

        /// <summary>imp, bullet, hazard or fall, from the name of what hurt the hero last.</summary>
        public static string CauseKey(string source)
        {
            source = (source ?? "").ToLowerInvariant();
            if (source.Contains("pit") || source.Contains("fall") || source.Contains("drop")) return "deathFall";
            if (source.Contains("lava") || source.Contains("geyser") || source.Contains("burn") || source.Contains("magma") || source.Contains("spike") || source.Contains("hazard") || source.Contains("fire"))
                return "deathHazard";
            if (source.Contains("imp") || source.Contains("sack") || source.Contains("brute") || source.Contains("whelp")) return "deathImp";
            return "deathBullet";
        }

        void Update()
        {
            if (world == null || !Active) return;
            if (clock == null && GameClock.Instance != null)
            {
                clock = GameClock.Instance;
                clock.OnRewindStarted += OnRewindStarted;
                clock.OnRewindStopped += OnRewindStopped;
            }
            bool rewinding = clock != null && clock.IsRewinding;
            float now = Time.unscaledTime;

            if (figure != null && world.Now > figureUntil + 0.5f)
                Visible = false;

            if (rewinding) return;

            dying = HeroIsDying();
            if (dying && !wasDying)
                Say(CauseKey(lastCause), "Laugh", false);
            wasDying = dying;
            if (dying) { lastHitAt = now; return; }

            // The floor entered: one line per floor, a moment after the level opens.
            if (!entered && now - startedAt > 2.5f && world.Now > 1.5f)
            {
                entered = true;
                Say("enter" + world.Stage.Floors.start, "Idle", true);
            }
            if (now - lastHitAt >= StreakSeconds)
            {
                lastHitAt = now;
                Say("streak", "Point", false);
            }
        }

        void OnRewindStarted()
        {
            if (!Active) return;
            lastHitAt = Time.unscaledTime;
            if (wasDying)
            {
                // The death is being undone: he answers at once, whatever the cooldown, and his silhouette climbs away (OverlordEscape).
                Speak("undo", "undo", true);
            }
            else
            {
                Speak("rewind", "rewind", false);
            }
            fleeNext = true;
        }

        void OnRewindStopped()
        {
            if (!Active || !fleeNext) return;
            fleeNext = false;
            if (Time.unscaledTime - LastAt < 6f)
                Show("overlordTauntFlee", "Flee");
        }

        /// <summary>A taunt that shows him: line, caption and the figure with the named clip.</summary>
        void Say(string key, string clip, bool ignoreCooldown)
        {
            if (Speak(key, key, ignoreCooldown))
                Show("overlordTaunt", clip);
        }

        bool Speak(string key, string countKey, bool ignoreCooldown)
        {
            if (!OverlordTauntLines.Table.TryGetValue(key, out var ids)) return false;
            float now = Time.unscaledTime;
            if (!ignoreCooldown && now - lastTauntAt < Cooldown) return false;
            string line = null;
            foreach (var id in ids)
                if (!Said.Contains(id)) { line = id; break; }
            if (line == null) return false;
            Said.Add(line);
            lastTauntAt = now;
            LastLine = line; LastKey = key; LastAt = now;
            Counts[countKey] = (Counts.TryGetValue(countKey, out int n) ? n : 0) + 1;
            string text = Sfx.SpokenText(line);
            float len = Mathf.Max(2.6f, Sfx.LengthOf(line) + 0.5f);
            world.Hud.ShowText(text, TextStyle.Subtitle, Red, world.Now + len);
            if (Sfx.Has(line)) Sfx.Play(line);
            world.Telemetry.Record("taunt", key, world.Stage.Floors.start, line);
            return true;
        }

        void Show(string entityId, string clip)
        {
            if (world.RewindLocked || (clock != null && clock.IsRewinding)) return;
            var e = world.Spawn(entityId, world.CameraCenter + new Vector2(0.7f, 1.7f), new SpawnOptions { FacePlayer = false, InstanceId = entityId });
            if (e == null) return;
            figure = e;
            figureUntil = world.Now + (entityId == "overlordTauntFlee" ? 2.6f : 4f);
            if (e.Animation != null) e.Animation.Play(clip);
            Visible = true;
            var sr = e.GetComponentInChildren<SpriteRenderer>();
            var cam = world.Camera;
            if (sr != null && cam != null)
            {
                var b = sr.bounds;
                float a = cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
                float c = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y;
                SizePx = Mathf.Abs(c - a);
            }
        }

        static bool HeroIsDying()
        {
            var hero = PlayerController.Active;
            if (hero == null) return false;
            var health = hero.GetComponentInChildren<PlayerHealth>();
            if (health == null) health = hero.GetComponentInParent<PlayerHealth>();
            return health != null && health.DeathTimeLeft >= 0f;
        }
    }
}
