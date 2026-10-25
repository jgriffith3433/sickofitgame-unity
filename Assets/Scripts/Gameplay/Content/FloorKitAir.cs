using System;
using UnityEngine;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // THE AIR AND THE PATHS (Docs/LevelDesign.md "The air and the paths", Docs/Story.md "The Hook")
    //
    // The floor kit's second batch: grapple anchors, moving platforms on paths, wind zones and gravity-flip zones (collapsing floors are
    // the crumble ability's 'group'). Like the first batch, each is an entity ability: pooled, ticked in spawn order, every runtime field
    // saved in SaveState/LoadState, every motion a function of the game clock or of saved state. A mover that left, a gust, a
    // ceiling he walked on all come back when the hero drags Hell's loop backward.
    // =====================================================================

    public enum GrappleMode
    {
        [Doc("The hook holds a rope: he swings on it.")] Swing,
        [Doc("The hook reels him straight in and hops him up onto the ledge it sits on.")] Pull,
    }

    public enum MoverMode
    {
        [Doc("Along the waypoints and back the same way.")] PingPong,
        [Doc("Along the waypoints and, from the last, straight back to the start; round and round.")] Loop,
    }

    [Serializable, JsonType("grappleAnchor")]
    [Doc("A grapple anchor: a ring in the wall, a hanging hook, a stump of chain (give the entity the tag \"anchor\"). The hero's hook takes the anchor " +
         "nearest his aim inside the cone and the range (player.json moves.grapple). A Swing anchor holds a rope of the length he hooked it at; a Pull " +
         "anchor reels him in. It glows when the hook would take it, and the rope is drawn while he hangs on it. The entity's own motion (a mover " +
         "carrying it) moves the anchor.")]
    public sealed class GrappleAnchorAbility : Ability
    {
        [Doc("Swing on a rope, or be pulled to the anchor.")] public GrappleMode mode = GrappleMode.Swing;
        [Doc("Reach of this anchor (units from his chest); 0 = the hook's own range. Never more than the hook's.")] public float reach;
        [Doc("Longest rope on this anchor (units); 0 = the hook's own.")] public float ropeMax;
        public Color ringColor = new Color(0.55f, 0.85f, 0.95f, 1f);
        public Color pullColor = new Color(1f, 0.65f, 0.25f, 1f);
        public Color ropeColor = new Color(0.78f, 0.7f, 0.55f, 1f);
        [SoundRef, Doc("Played when the hook takes hold.")] public string hookSound = "";

        public override void Tick(float now, float dt)
        {
            var world = Owner.World;
            var grapple = world.Grapple;
            var controller = world.PlayerController;
            Vector2 at = Owner.Position;
            Color tint = mode == GrappleMode.Pull ? pullColor : ringColor;
            bool held = grapple != null && grapple.AnchorSerial == Owner.Serial;
            bool aimed = false;
            if (!held && grapple != null && controller != null && world.Player != null && world.Player.IsAlive && controller.GrappleUnlocked)
                aimed = grapple.FindAnchor(controller.GrappleAim()) == Owner;

            // A diamond-shaped ring and a core (cosmetic): brighter when it is the one the hook would take, pulsing while held.
            float pulse = held ? 1f + 0.25f * Mathf.Sin(now * 14f) : aimed ? 1.15f : 1f;
            var ring = FloorKit.Child(Owner, "AnchorRing", Owner.Def.visual.layer, SpriteLayers.Platform + 3);
            ring.color = new Color(tint.r, tint.g, tint.b, aimed || held ? 1f : 0.7f);
            FloorKit.PlaceWorld(Owner, ring, at, new Vector2(0.46f, 0.46f) * pulse, 45f);
            var core = FloorKit.Child(Owner, "AnchorCore", Owner.Def.visual.layer, SpriteLayers.Platform + 4);
            core.color = new Color(0.12f, 0.09f, 0.08f, 1f);
            FloorKit.PlaceWorld(Owner, core, at, new Vector2(0.28f, 0.28f) * pulse, 45f);

            // The rope, from his hand to the ring.
            var rope = FloorKit.Child(Owner, "AnchorRope", Owner.Def.visual.layer, SpriteLayers.Platform + 2);
            rope.enabled = held && world.Player != null;
            if (rope.enabled)
            {
                Vector2 hand = world.Player.Position + Vector2.up * 0.5f;
                Vector2 span = at - hand;
                float length = Mathf.Max(0.05f, span.magnitude);
                rope.color = ropeColor;
                FloorKit.PlaceWorld(Owner, rope, (hand + at) * 0.5f, new Vector2(0.07f, length), Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg - 90f);
            }
        }
    }

    [Serializable, JsonType("mover")]
    [Doc("A moving platform on a path: it spawns at the first point and travels the 'waypoints' (offsets from where it spawned) at 'speed', waiting " +
         "'pause' seconds at each. Standing on it carries the hero (sideways with it; up and down through the collision). 'holdUntilBoarded' keeps it at " +
         "its first point until he steps on. Its place on the path is saved, so a rewind puts it back exactly where it was.")]
    public sealed class MoverAbility : Ability
    {
        [Doc("Points after the start, as offsets from the spawn point. At least one.")] public Vector2[] waypoints = { new Vector2(4f, 0f) };
        [Doc("Units per second.")] public float speed = 2f;
        [Doc("Seconds it waits at each point.")] public float pause = 0.6f;
        public MoverMode mode = MoverMode.PingPong;
        [Doc("It waits at the first point until the hero first stands on it.")] public bool holdUntilBoarded;
        [SoundRef, Doc("Played each time it sets off from a point.")] public string startSound = "";

        Vector2 home;
        int from;
        int dir = 1;
        float along;
        float pauseLeft;
        bool boarded;
        bool carried;

        public override void OnSpawn()
        {
            home = Owner.Position;
            from = 0;
            dir = 1;
            along = 0f;
            pauseLeft = 0f;
            boarded = false;
            carried = false;
        }

        int Count => (waypoints != null ? waypoints.Length : 0) + 1;

        Vector2 Point(int i) => i <= 0 || waypoints == null ? home : home + waypoints[Mathf.Min(i, waypoints.Length) - 1];

        int Next(int i)
        {
            int n = Count;
            if (mode == MoverMode.Loop)
                return (i + 1) % n;
            return Mathf.Clamp(i + dir, 0, n - 1);
        }

        public override void Tick(float now, float dt)
        {
            bool on = Owner.World.PlayerStandsOn(Owner);
            if (on)
                boarded = true;
            if (Count > 1 && (!holdUntilBoarded || boarded))
                Advance(dt);

            int to = Next(from);
            Vector2 a = Point(from), b = Point(to);
            float length = Vector2.Distance(a, b);
            Vector2 want = length > 0.0001f ? Vector2.Lerp(a, b, Mathf.Clamp01(along / length)) : a;
            Vector2 velocity = dt > 0f && Owner.Body != null ? (want - Owner.Body.position) / dt : Vector2.zero;
            if (on)
            {
                Owner.World.CarryPlayer(new Vector2(velocity.x, 0f));
                if (!carried)
                    FloorKit.Record(Owner, "moverBoard");
            }
            carried = on;
            FloorKit.Moved(Owner, want, dt);
        }

        void Advance(float dt)
        {
            if (pauseLeft > 0f)
            {
                pauseLeft = Mathf.Max(0f, pauseLeft - dt);
                return;
            }
            float move = Mathf.Max(0f, speed) * dt;
            int guard = 8;
            while (move > 0f && guard-- > 0)
            {
                int to = Next(from);
                float length = Vector2.Distance(Point(from), Point(to));
                float left = length - along;
                if (move < left)
                {
                    along += move;
                    return;
                }
                move -= left;
                from = to;
                along = 0f;
                if (mode == MoverMode.PingPong)
                {
                    if (from >= Count - 1)
                        dir = -1;
                    else if (from <= 0)
                        dir = 1;
                }
                if (pause > 0f)
                {
                    pauseLeft = pause;
                    SickOfIt.Core.Audio.Sfx.Play(startSound);
                    FloorKit.Record(Owner, "moverStop", from);
                    return;
                }
            }
        }

        /// <summary>Where it is on its path: the waypoint it left and how far along to the next (a playtest reads it).</summary>
        public int From => from;
        public float Along => along;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Int(from);
            writer.Int(dir);
            writer.Float(along);
            writer.Float(pauseLeft);
            writer.Bool(boarded);
            writer.Bool(carried);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            from = reader.Int();
            dir = reader.Int();
            along = reader.Float();
            pauseLeft = reader.Float();
            boarded = reader.Bool();
            carried = reader.Bool();
        }
    }

    [Serializable, JsonType("wind")]
    [Doc("A wind zone (the entity's body is the zone): inside it the hero's sideways speed gains 'push'.x (on the ground too, so walking into it is slower), " +
         "'push'.y lifts him (or presses him down) like an updraft, and every shot in the zone, his and Hell's, is bent along the wind at 'shotPush' units " +
         "per second squared. 'gustPeriod' makes it blow in gusts (on for 'gustDuty' of each period, ramping in and out) so a crossing can be timed; " +
         "streaks run with the wind, bright while it blows. Gusts follow the game clock, so a rewind replays them.")]
    public sealed class WindAbility : Ability
    {
        [Doc("The wind: units per second of sideways drift added to the hero (x), and the upward speed it lifts him to (y; negative presses down).")]
        public Vector2 push = new Vector2(3f, 0f);
        [Doc("How hard the zone bends shots, units per second squared along the wind's direction.")] public float shotPush = 9f;
        [Doc("How fast a vertical wind gets him up to speed.")] public float acceleration = 30f;
        [Doc("Seconds from one gust to the next; 0 = a steady wind.")] public float gustPeriod;
        [Doc("The share of each period it blows (0-1).")] public float gustDuty = 0.6f;
        [Doc("Seconds a gust takes to rise and die down.")] public float gustRamp = 0.35f;
        [Doc("Where in its period it starts (0-1), so zones side by side don't gust together.")] public float phase;
        public Color streakColor = new Color(0.8f, 0.92f, 1f, 1f);

        bool inside;

        static readonly Collider2D[] hits = new Collider2D[64];
        static ContactFilter2D everything;
        static bool filterMade;

        public override void OnSpawn() => inside = false;

        /// <summary>How hard it blows right now, 0-1 (1 in a steady wind).</summary>
        public float Blowing(float now)
        {
            if (gustPeriod <= 0.01f)
                return 1f;
            float t = Mathf.Repeat((now - Owner.SpawnTime) / gustPeriod + phase, 1f);
            float duty = Mathf.Clamp01(gustDuty);
            if (t >= duty)
                return 0f;
            float ramp = Mathf.Max(0.01f, gustRamp / gustPeriod);
            return Mathf.Clamp01(Mathf.Min(t, duty - t) / ramp);
        }

        public override void Tick(float now, float dt)
        {
            var zone = Owner.MainCollider;
            if (zone == null)
                return;
            var world = Owner.World;
            Bounds b = zone.bounds;
            float blow = Blowing(now);
            var feet = world.PlayerController != null ? world.PlayerController.BodyCollider : null;
            bool inZone = feet != null && world.Player != null && world.Player.IsAlive && b.Intersects(feet.bounds);
            if (inZone && blow > 0f && !world.PlayerController.IsHeld)
            {
                world.WindPlayer(new Vector2(push.x * blow, 0f));
                var body = world.Player.Body;
                if (body != null && Mathf.Abs(push.y) > 0.01f)
                {
                    var v = body.linearVelocity;
                    float target = push.y * blow;
                    v.y = target > 0f ? Mathf.MoveTowards(v.y, Mathf.Max(v.y, target), acceleration * dt)
                                      : Mathf.MoveTowards(v.y, Mathf.Min(v.y, target), acceleration * dt);
                    body.linearVelocity = v;
                }
            }
            if (inZone && blow > 0.5f && !inside)
                FloorKit.Record(Owner, "windEnter");
            inside = inZone && blow > 0.5f;

            if (blow > 0f && push.sqrMagnitude > 0.0001f)
                BendShots(world, b, push.normalized * (shotPush * blow * dt));
            Draw(now, b, blow);
        }

        void BendShots(GameplayWorld world, Bounds b, Vector2 dv)
        {
            if (!filterMade)
            {
                everything = new ContactFilter2D();
                everything.NoFilter();
                filterMade = true;
            }
            int n = Physics2D.OverlapBox(b.center, b.size, 0f, everything, hits);
            var heroBody = world.Player != null ? world.Player.Body : null;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c == null)
                    continue;
                var rb = c.attachedRigidbody;
                if (rb == null || rb == heroBody || rb == Owner.Body || rb.bodyType == RigidbodyType2D.Static)
                    continue;
                bool shot;
                if (world.TryGetEntity(c, out var e))
                    shot = !e.IsPlayer && e != Owner && e.FindAbility<ProjectileAbility>() != null;
                else
                    shot = c.TryGetComponent<ProjectileLifetime>(out _);
                if (shot)
                    rb.linearVelocity += dv;
            }
        }

        // Streaks running with the wind (cosmetic, from the game clock).
        void Draw(float now, Bounds b, float blow)
        {
            Vector2 dir = push.sqrMagnitude > 0.0001f ? push.normalized : Vector2.right;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float run = Mathf.Abs(Vector2.Dot(b.size, new Vector2(Mathf.Abs(dir.x), Mathf.Abs(dir.y))));
            float across = Mathf.Abs(Vector2.Dot(b.size, new Vector2(Mathf.Abs(side.x), Mathf.Abs(side.y))));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 8; i++)
            {
                var streak = FloorKit.Child(Owner, "Streak" + i, SpriteLayer.Player, SpriteLayers.Platform + 2);
                float lane = (i * 0.618034f) % 1f - 0.5f;
                float u = Mathf.Repeat(now * (0.35f + 0.05f * (i % 3)) * (0.4f + blow) + i * 0.137f, 1f);
                Vector2 at = b.center + dir * ((u - 0.5f) * run) + side * (lane * across * 0.9f);
                float fade = Mathf.Sin(u * Mathf.PI);
                streak.color = new Color(streakColor.r, streakColor.g, streakColor.b, (0.15f + 0.5f * blow) * fade);
                FloorKit.PlaceWorld(Owner, streak, at, new Vector2(0.7f, 0.035f), angle);
            }
        }

        public bool Inside => inside;

        public override void SaveState(StateWriter writer) => writer.Bool(inside);
        public override void LoadState(StateReader reader) => inside = reader.Bool();
    }

    [Serializable, JsonType("gravityFlip")]
    [Doc("A gravity-flip zone (the entity's body is the zone): while the hero is inside it, and for 'linger' seconds after, gravity pulls him UP: he falls " +
         "to the ceiling, walks on it upside down, jumps down from it. Needs a ceiling (a ledge above) or he falls out of the top of the view. Which side " +
         "is 'down' is saved with the world and every zone's own timer is saved, so a rewind puts him back on the floor or the ceiling he stood on. " +
         "Arrows drift the way gravity pulls so it reads before he steps in.")]
    public sealed class GravityFlipAbility : Ability
    {
        [Doc("Seconds the flip lasts after he leaves the zone, so he can cross to the ceiling's far end.")] public float linger = 0.4f;
        public Color tint = new Color(0.7f, 0.45f, 1f, 1f);

        bool active;
        float leftAt = -99f;

        public override void OnSpawn()
        {
            active = false;
            leftAt = -99f;
        }

        public bool Active => active;

        public override void Tick(float now, float dt)
        {
            var zone = Owner.MainCollider;
            var world = Owner.World;
            var feet = world.PlayerController != null ? world.PlayerController.BodyCollider : null;
            if (zone == null)
                return;
            Bounds b = zone.bounds;
            bool inZone = feet != null && world.Player != null && world.Player.IsAlive && b.Intersects(feet.bounds);
            if (inZone)
                leftAt = now;
            bool on = inZone || (now - leftAt < linger && leftAt > -90f);
            if (on)
                world.FlipPlayerGravity();
            if (on != active)
            {
                active = on;
                FloorKit.Record(Owner, on ? "flipOn" : "flipOff");
                Owner.RecordNow();
            }
            Draw(now, b);
        }

        // Arrows drifting up the zone while it pulls up, a faint tint (cosmetic).
        void Draw(float now, Bounds b)
        {
            for (int i = 0; i < 5; i++)
            {
                var arrow = FloorKit.Child(Owner, "Arrow" + i, SpriteLayer.Player, SpriteLayers.Platform + 2);
                float u = Mathf.Repeat(now * 0.3f + i * 0.2f, 1f);
                float x = b.min.x + b.size.x * ((i + 0.5f) / 5f);
                arrow.color = new Color(tint.r, tint.g, tint.b, 0.55f * Mathf.Sin(u * Mathf.PI) * (active ? 1f : 0.6f));
                FloorKit.PlaceWorld(Owner, arrow, new Vector2(x, b.min.y + u * b.size.y), new Vector2(0.22f, 0.22f), 45f);
            }
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Bool(active);
            writer.Float(leftAt);
        }

        public override void LoadState(StateReader reader)
        {
            active = reader.Bool();
            leftAt = reader.Float();
        }
    }
}
