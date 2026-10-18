using System;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // THE FLOOR TOOLKIT (Docs/LevelDesign.md, "The toolkit")
    //
    // Platforms and hazards of a floor of Hell. Each is an entity ability, so it is pooled, ticked in spawn order
    // and rewound like everything else: position and rotation come back through the pool's RewindableTransform,
    // and every runtime field below is saved in SaveState/LoadState. A stone that sank, a ledge that fell or a
    // lift that left comes back when the hero drags Hell's loop backward. Every motion is a function of the
    // game clock or of saved state, never of real time, so a rewind replays the same future.
    // =====================================================================

    /// <summary>Shared helpers for the floor pieces (presentation only: tints and child shapes).</summary>
    static class FloorKit
    {
        /// <summary>The drawn body: the box placeholder ("Shape") or the entity's own sprite.</summary>
        public static SpriteRenderer Look(Entity owner)
        {
            var shape = owner.transform.Find("Shape");
            if (shape != null && shape.TryGetComponent<SpriteRenderer>(out var sr))
                return sr;
            return owner.Sprite;
        }

        /// <summary>Tints the drawn body (cosmetic).</summary>
        public static void Tint(Entity owner, Color color)
        {
            var sr = Look(owner);
            if (sr != null)
                sr.color = color;
        }

        public static Color BaseColor(Entity owner) =>
            owner.Def.visual.placeholderShape == PlaceholderShape.Box ? owner.Def.visual.placeholderColor : owner.Def.visual.color;

        /// <summary>A child quad (white square) under the entity, made once per pooled instance.</summary>
        public static SpriteRenderer Child(Entity owner, string name, SpriteLayer layer, int order)
        {
            var t = owner.transform.Find(name);
            if (t != null && t.TryGetComponent<SpriteRenderer>(out var existing))
                return existing;
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EntityFactory.WhiteSquare;
            SpriteLayers.Apply(sr, layer, order);
            return sr;
        }

        /// <summary>Places a child quad by world center and world size, whatever the entity's own scale.</summary>
        public static void PlaceWorld(Entity owner, SpriteRenderer sr, Vector2 center, Vector2 size, float angle = 0f)
        {
            var t = sr.transform;
            t.position = new Vector3(center.x, center.y, t.position.z);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3 parent = owner.transform.lossyScale;
            t.localScale = new Vector3(size.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), size.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        }

        /// <summary>The top edge of the entity's body in world units.</summary>
        public static float Top(Entity owner)
        {
            var c = owner.MainCollider;
            return c != null ? c.bounds.max.y : owner.Position.y;
        }

        public static void Moved(Entity owner, Vector2 want, float dt)
        {
            if (owner.Body == null || dt <= 0f)
                return;
            owner.Move((want - owner.Body.position) / dt);
        }

        public static void Record(Entity owner, string what, int value = 0)
        {
            string who = string.IsNullOrEmpty(owner.InstanceId) ? owner.DefId + "#" + owner.Serial : owner.InstanceId;
            owner.World.Telemetry.Record("floorkit", what + ":" + who, value, owner.DefId);
        }
    }

    [Serializable, JsonType("sink")]
    [Doc("A sinking stone: a slab of cooled slag floating on the lava. While the hero stands on it, it sinks (hissing, dipping the moment he " +
         "lands); when he steps off it bobs back up. Rewind brings a sunk stone back.")]
    public sealed class SinkAbility : Ability
    {
        [Doc("Units per second it sinks while stood on.")] public float sinkSpeed = 1f;
        [Doc("How far it goes down before it stops (under the lava).")] public float depth = 1.5f;
        [Doc("Units per second it comes back up once he's off (0 = it stays down).")] public float riseSpeed = 0.7f;
        [Doc("The dip the moment he lands (the telegraph), in units.")] public float dip = 0.08f;
        [SoundRef, Doc("Played when he lands on it (a hiss).")] public string sound = "";
        [Doc("Signal emitted the first time he stands on it ('' = none).")] public string signal = "";

        Vector2 home;
        float sunk;
        bool stoodOn;
        bool signalled;

        public override void OnSpawn()
        {
            home = Owner.Position;
            sunk = 0f;
            stoodOn = false;
            signalled = false;
        }

        public override void Tick(float now, float dt)
        {
            bool on = Owner.World.PlayerStandsOn(Owner);
            if (on && !stoodOn)
            {
                sunk = Mathf.Max(sunk, dip);
                SickOfIt.Core.Audio.Sfx.Play(sound);
                FloorKit.Record(Owner, "sink");
                if (!signalled && !string.IsNullOrEmpty(signal))
                {
                    signalled = true;
                    Owner.World.Emit(signal);
                }
                Owner.RecordNow();
            }
            else if (!on && stoodOn)
            {
                FloorKit.Record(Owner, "sinkLeft", Mathf.RoundToInt(sunk * 100f));
            }
            stoodOn = on;
            sunk = on ? Mathf.Min(depth, sunk + sinkSpeed * dt) : Mathf.Max(0f, sunk - riseSpeed * dt);
            FloorKit.Moved(Owner, home + Vector2.down * sunk, dt);
            // Hot as it goes under (cosmetic).
            FloorKit.Tint(Owner, Color.Lerp(FloorKit.BaseColor(Owner), new Color(1f, 0.4f, 0.1f, 1f), Mathf.Clamp01(sunk / Mathf.Max(0.01f, depth)) * 0.7f));
        }

        /// <summary>How far down it is now (the playtest reads it).</summary>
        public float Sunk => sunk;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(sunk);
            writer.Bool(stoodOn);
            writer.Bool(signalled);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            sunk = reader.Float();
            stoodOn = reader.Bool();
            signalled = reader.Bool();
        }
    }

    [Serializable, JsonType("crumble")]
    [Doc("A crumbling stone: old slag that cracks the moment he lands (shaking and dusting for 'delay' seconds, the telegraph), then falls " +
         "into the lava. It doesn't come back, except by rewind.")]
    public sealed class CrumbleAbility : Ability
    {
        [Doc("Seconds it shakes before it falls (at least 1, so it's fair).")] public float delay = 1f;
        [Doc("How hard it shakes (units).")] public float shake = 0.05f;
        [Doc("Fall acceleration (units per second squared).")] public float gravity = 16f;
        [Doc("How far it falls before it is gone.")] public float fallDistance = 14f;
        [SoundRef, Doc("Played when it cracks.")] public string crackSound = "";
        [SoundRef, Doc("Played when it falls.")] public string fallSound = "";

        Vector2 home;
        float crackedAt = -1f;
        float fallSpeed;
        float fallen;
        bool falling;

        public override void OnSpawn()
        {
            home = Owner.Position;
            crackedAt = -1f;
            fallSpeed = 0f;
            fallen = 0f;
            falling = false;
        }

        public override void Tick(float now, float dt)
        {
            if (crackedAt < 0f && Owner.World.PlayerStandsOn(Owner))
            {
                crackedAt = now;
                SickOfIt.Core.Audio.Sfx.Play(crackSound);
                SickOfIt.Core.Fx.Fx.Dust(new Vector2(Owner.Position.x, FloorKit.Top(Owner)), 0.8f);
                FloorKit.Record(Owner, "crack");
                Owner.RecordNow();
            }
            Vector2 want = home;
            if (crackedAt >= 0f && !falling)
            {
                float t = now - crackedAt;
                // Shakes harder as it gives.
                want += new Vector2(Mathf.Sin(t * 55f) * shake * (0.5f + t / Mathf.Max(0.1f, delay)), 0f);
                FloorKit.Tint(Owner, Color.Lerp(FloorKit.BaseColor(Owner), new Color(0.9f, 0.75f, 0.6f, 1f), Mathf.PingPong(t * 6f, 1f) * 0.5f));
                if (t >= Mathf.Max(1f, delay))
                {
                    falling = true;
                    SickOfIt.Core.Audio.Sfx.Play(fallSound);
                    FloorKit.Record(Owner, "fall");
                    Owner.RecordNow();
                }
            }
            if (falling && fallen < fallDistance)
            {
                fallSpeed += gravity * dt;
                fallen = Mathf.Min(fallDistance, fallen + fallSpeed * dt);
            }
            if (falling)
                want = home + Vector2.down * fallen;
            if (crackedAt < 0f)
                FloorKit.Tint(Owner, FloorKit.BaseColor(Owner));
            FloorKit.Moved(Owner, want, dt);
        }

        public bool Falling => falling;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(crackedAt);
            writer.Float(fallSpeed);
            writer.Float(fallen);
            writer.Bool(falling);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            crackedAt = reader.Float();
            fallSpeed = reader.Float();
            fallen = reader.Float();
            falling = reader.Bool();
        }
    }

    [Serializable, JsonType("raft")]
    [Doc("A debris raft (a cart, a roof, a fence) floating on the lava: it bobs on its own rhythm, drifts with the lava's current " +
         "(back and forth over 'range', or on and on), rocks, tips toward an edge he stands on, and carries him along.")]
    public sealed class RaftAbility : Ability
    {
        [Doc("The lava current: drift velocity in units per second.")] public Vector2 current = new Vector2(0.8f, 0f);
        [Doc("Distance it drifts before the current turns it back (0 = it keeps going).")] public float range = 6f;
        [Doc("Bob height (units).")] public float bob = 0.1f;
        [Doc("Seconds per bob.")] public float bobPeriod = 2.4f;
        [Doc("Rocking, in degrees.")] public float rock = 3f;
        [Doc("Extra tilt toward the edge he stands on, in degrees (at the very edge).")] public float tip = 8f;
        [Doc("Where in its bob it starts (0-1), so rafts side by side don't move together.")] public float phase;

        Vector2 home;
        float travelled;
        float heading = 1f;
        float tilt;
        bool carried;

        public override void OnSpawn()
        {
            home = Owner.Position;
            travelled = 0f;
            heading = 1f;
            tilt = 0f;
            carried = false;
        }

        public override void Tick(float now, float dt)
        {
            float speed = current.magnitude;
            Vector2 axis = speed > 0.0001f ? current / speed : Vector2.right;
            travelled += speed * heading * dt;
            if (range > 0f)
            {
                if (travelled >= range) { travelled = range; heading = -1f; }
                else if (travelled <= 0f) { travelled = 0f; heading = 1f; }
            }
            float age = now - Owner.SpawnTime;
            float wave = Mathf.Sin((age / Mathf.Max(0.2f, bobPeriod) + phase) * Mathf.PI * 2f);
            Vector2 want = home + axis * travelled + Vector2.up * (wave * bob);

            bool on = Owner.World.PlayerStandsOn(Owner);
            float lean = 0f;
            if (on && Owner.MainCollider != null)
            {
                var b = Owner.MainCollider.bounds;
                float offset = (Owner.World.Player.Position.x - b.center.x) / Mathf.Max(0.1f, b.extents.x);
                lean = -Mathf.Clamp(offset, -1f, 1f) * tip;
            }
            tilt = Mathf.MoveTowards(tilt, lean + wave * rock, 40f * dt);
            if (Owner.Body != null)
                Owner.Body.MoveRotation(tilt);

            Vector2 velocity = axis * (speed * heading);
            if (on)
            {
                Owner.World.CarryPlayer(velocity);
                if (!carried)
                    FloorKit.Record(Owner, "raftBoard");
            }
            carried = on;
            FloorKit.Moved(Owner, want, dt);
        }

        public float Travelled => travelled;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(travelled);
            writer.Float(heading);
            writer.Float(tilt);
            writer.Bool(carried);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            travelled = reader.Float();
            heading = reader.Float();
            tilt = reader.Float();
            carried = reader.Bool();
        }
    }

    [Serializable, JsonType("conveyor")]
    [Doc("A conveyor slag flow: a crust of slag sliding along on the lava. Standing on it carries him at 'speed' (with it or against it); " +
         "its glowing seams crawl along so it reads before he steps on.")]
    public sealed class ConveyorAbility : Ability
    {
        [Doc("Units per second it carries him (negative = to the left).")] public float speed = 1f;
        [Doc("Colour of the crawling seams.")] public Color seamColor = new Color(1f, 0.5f, 0.15f, 1f);

        public override void Tick(float now, float dt)
        {
            if (Owner.World.PlayerStandsOn(Owner))
                Owner.World.CarryPlayer(new Vector2(speed, 0f));

            // Three seams crawling along the top (cosmetic, from the game clock so a rewind runs them back).
            var c = Owner.MainCollider;
            if (c == null)
                return;
            var b = c.bounds;
            for (int i = 0; i < 3; i++)
            {
                var seam = FloorKit.Child(Owner, "Seam" + i, Owner.Def.visual.layer, SpriteLayers.Platform + 1);
                seam.color = seamColor;
                float u = Mathf.Repeat(now * speed / Mathf.Max(0.1f, b.size.x) + i / 3f, 1f);
                FloorKit.PlaceWorld(Owner, seam, new Vector2(b.min.x + u * b.size.x, b.max.y - 0.06f), new Vector2(0.35f, 0.06f));
            }
        }
    }

    [Serializable, JsonType("swingChain")]
    [Doc("A swinging chain (or rope): the entity is the handle at the end, swinging from a pivot 'length' above where it spawned. The hero " +
         "grabs it by jumping into it, swings with it, and lets go with Jump, keeping the swing's speed. The chain rattles (drawn) as it goes.")]
    public sealed class SwingChainAbility : Ability
    {
        [Doc("Chain length (units): the pivot is this far above the spawn point.")] public float length = 2.6f;
        [Doc("How far it swings each way, in degrees.")] public float amplitude = 40f;
        [Doc("Seconds for a full swing there and back.")] public float period = 2.8f;
        [Doc("Where in its swing it starts (0-1).")] public float phase;
        [Doc("How close his body must come to the handle to grab it.")] public float grabRadius = 0.85f;
        [Doc("Where he hangs below the handle (units).")] public float hang = 0.55f;
        [Doc("Seconds after letting go before he can grab it again.")] public float regrab = 0.6f;
        public Color chainColor = new Color(0.32f, 0.26f, 0.24f, 1f);
        [SoundRef, Doc("Played when he grabs it.")] public string grabSound = "";

        Vector2 pivot;
        bool holding;
        float releasedAt = -99f;

        public override void OnSpawn()
        {
            pivot = Owner.Position + Vector2.up * length;
            holding = false;
            releasedAt = -99f;
        }

        public override void Tick(float now, float dt)
        {
            float w = Mathf.PI * 2f / Mathf.Max(0.2f, period);
            float arg = (now - Owner.SpawnTime) * w + phase * Mathf.PI * 2f;
            float amp = amplitude * Mathf.Deg2Rad;
            float angle = amp * Mathf.Sin(arg);
            float spin = amp * w * Mathf.Cos(arg);
            Vector2 handle = pivot + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * length;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (length * spin);
            FloorKit.Moved(Owner, handle, dt);

            var world = Owner.World;
            var controller = world.PlayerController;
            var player = world.Player;
            if (controller != null && player != null && player.IsAlive)
            {
                Vector2 body = handle + Vector2.down * hang;
                if (holding)
                {
                    if (controller.ConsumeJumpOff())
                    {
                        holding = false;
                        releasedAt = now;
                        FloorKit.Record(Owner, "swingRelease", Mathf.RoundToInt(velocity.x * 100f));
                        Owner.RecordNow();
                    }
                    else
                    {
                        world.HoldPlayer(body, velocity);
                    }
                }
                else if (now - releasedAt >= regrab && !controller.IsGrounded && !controller.IsHeld
                         && Vector2.Distance(world.PlayerAimPoint, handle) <= grabRadius + 0.35f)
                {
                    holding = true;
                    controller.ConsumeJumpOff();
                    world.HoldPlayer(body, velocity);
                    SickOfIt.Core.Audio.Sfx.Play(grabSound);
                    FloorKit.Record(Owner, "swingGrab");
                    Owner.RecordNow();
                }
            }
            else
            {
                holding = false;
            }

            // The chain from the pivot to the handle (cosmetic).
            var chain = FloorKit.Child(Owner, "Chain", Owner.Def.visual.layer, SpriteLayers.Platform);
            chain.color = chainColor;
            FloorKit.PlaceWorld(Owner, chain, (pivot + handle) * 0.5f, new Vector2(0.08f, length), angle * Mathf.Rad2Deg);
            var anchor = FloorKit.Child(Owner, "Pivot", Owner.Def.visual.layer, SpriteLayers.Platform);
            anchor.color = chainColor;
            FloorKit.PlaceWorld(Owner, anchor, pivot, new Vector2(0.3f, 0.2f));
        }

        public bool Holding => holding;
        public Vector2 Pivot => pivot;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(pivot.x);
            writer.Float(pivot.y);
            writer.Bool(holding);
            writer.Float(releasedAt);
        }

        public override void LoadState(StateReader reader)
        {
            pivot = new Vector2(reader.Float(), reader.Float());
            holding = reader.Bool();
            releasedAt = reader.Float();
        }
    }

    [Serializable, JsonType("chainLift")]
    [Doc("A chain lift: a platform the imps haul up and down a shaft. It waits, jolts and clanks for 'jolt' seconds (the telegraph), rides " +
         "'travel' units, waits, jolts and comes back. With startOnStand it waits at the bottom until he steps on it.")]
    public sealed class ChainLiftAbility : Ability
    {
        [Doc("Where it goes from where it spawned (units).")] public Vector2 travel = new Vector2(0f, 3f);
        [Doc("Units per second.")] public float speed = 1.2f;
        [Doc("Seconds it waits at each end.")] public float wait = 1.5f;
        [Doc("Seconds it jolts before it moves.")] public float jolt = 0.6f;
        [Doc("Wait at the bottom until he stands on it.")] public bool startOnStand = true;
        public Color chainColor = new Color(0.32f, 0.26f, 0.24f, 1f);
        [SoundRef, Doc("Played on each jolt (clank).")] public string joltSound = "";

        Vector2 home;
        float startedAt = -1f;
        int lastStage = -1;

        public override void OnSpawn()
        {
            home = Owner.Position;
            startedAt = startOnStand ? -1f : Owner.SpawnTime;
            lastStage = -1;
        }

        public override void Tick(float now, float dt)
        {
            if (startedAt < 0f && Owner.World.PlayerStandsOn(Owner))
            {
                startedAt = now;
                FloorKit.Record(Owner, "liftStart");
                Owner.RecordNow();
            }
            float distance = travel.magnitude;
            float move = distance / Mathf.Max(0.05f, speed);
            float j = Mathf.Max(0f, jolt);
            // One cycle: jolt, up, wait, jolt, down, wait.
            float cycle = 2f * (j + move + wait);
            float k = 0f, shake = 0f;
            int stage = -1;
            if (startedAt >= 0f)
            {
                float t = Mathf.Repeat(now - startedAt, cycle);
                if (t < j) { stage = 0; shake = 1f; }
                else if ((t -= j) < move) { stage = 1; k = t / move; }
                else if ((t -= move) < wait) { stage = 2; k = 1f; }
                else if ((t -= wait) < j) { stage = 3; k = 1f; shake = 1f; }
                else if ((t -= j) < move) { stage = 4; k = 1f - t / move; }
                else { stage = 5; k = 0f; }
            }
            if (stage != lastStage)
            {
                if (stage == 0 || stage == 3)
                    SickOfIt.Core.Audio.Sfx.Play(joltSound);
                lastStage = stage;
            }
            float e = k * k * (3f - 2f * k);
            Vector2 want = home + travel * e + new Vector2(0f, shake * Mathf.Sin(now * 70f) * 0.04f);
            FloorKit.Moved(Owner, want, dt);

            // The chain it hangs from, up past the top of its ride (cosmetic).
            var chain = FloorKit.Child(Owner, "Chain", Owner.Def.visual.layer, SpriteLayers.Platform - 1);
            chain.color = chainColor;
            float top = home.y + Mathf.Max(0f, travel.y) + 4f;
            float y = Owner.Position.y;
            FloorKit.PlaceWorld(Owner, chain, new Vector2(Owner.Position.x, (top + y) * 0.5f), new Vector2(0.07f, Mathf.Max(0.1f, top - y)));
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(startedAt);
            writer.Int(lastStage);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            startedAt = reader.Float();
            lastStage = reader.Int();
        }
    }

    [Serializable, JsonType("counterweight")]
    [Doc("One side of a counterweight: two platforms on one chain over a pulley. Stand on this one and it goes down while its partner " +
         "(the entity with instance id 'partner', also a counterweight) goes up the same distance, and the other way round.")]
    public sealed class CounterweightAbility : Ability
    {
        [Required, Doc("Instance id of the other side.")] public string partner = "";
        [Doc("How far each side moves (units).")] public float drop = 2.4f;
        [Doc("Units per second.")] public float speed = 1.2f;

        Vector2 home;
        float offset;

        public override void OnSpawn()
        {
            home = Owner.Position;
            offset = 0f;
        }

        public override void Tick(float now, float dt)
        {
            var world = Owner.World;
            var other = world.FindById(partner);
            float target = world.PlayerStandsOn(Owner) ? drop : other != null && world.PlayerStandsOn(other) ? -drop : offset;
            offset = Mathf.MoveTowards(offset, target, speed * dt);
            FloorKit.Moved(Owner, home + Vector2.down * offset, dt);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(offset);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            offset = reader.Float();
        }
    }

    [Serializable, JsonType("updraft")]
    [Doc("A heat updraft: a shimmering column of hot air over the lava (the entity's body is the column). Inside it he is carried up, " +
         "and a jump in it keeps rising. Ash floats up the column so it reads before he steps in.")]
    public sealed class UpdraftAbility : Ability
    {
        [Doc("Upward speed it carries him to (units per second).")] public float lift = 5f;
        [Doc("How fast he gets to that speed.")] public float acceleration = 30f;

        bool inside;

        public override void OnSpawn() => inside = false;

        public override void Tick(float now, float dt)
        {
            var world = Owner.World;
            var feet = world.PlayerController != null ? world.PlayerController.BodyCollider : null;
            var column = Owner.MainCollider;
            bool inColumn = feet != null && column != null && world.Player != null && world.Player.IsAlive
                              && !world.PlayerController.IsHeld && column.bounds.Intersects(feet.bounds);
            if (inColumn && world.Player.Body != null)
            {
                var v = world.Player.Body.linearVelocity;
                v.y = Mathf.MoveTowards(v.y, Mathf.Max(v.y, lift), acceleration * dt);
                world.Player.Body.linearVelocity = v;
                if (!inside)
                    FloorKit.Record(Owner, "updraft");
            }
            inside = inColumn;

            // Ash rising up the column (cosmetic, from the game clock).
            if (column == null)
                return;
            var b = column.bounds;
            for (int i = 0; i < 4; i++)
            {
                var ash = FloorKit.Child(Owner, "Ash" + i, SpriteLayer.Player, SpriteLayers.Platform + 2);
                ash.color = new Color(1f, 0.85f, 0.7f, 0.55f);
                float u = Mathf.Repeat(now * 0.45f + i * 0.25f, 1f);
                float x = b.center.x + Mathf.Sin(now * 2f + i * 1.7f) * b.extents.x * 0.6f;
                FloorKit.PlaceWorld(Owner, ash, new Vector2(x, b.min.y + u * b.size.y), new Vector2(0.07f, 0.07f));
            }
        }

        public override void SaveState(StateWriter writer) => writer.Bool(inside);
        public override void LoadState(StateReader reader) => inside = reader.Bool();
    }

    [Serializable, JsonType("scald")]
    [Doc("Lava that burns and throws him out: touching it hurts (with the usual hit invincibility) and launches him up out of it, so the " +
         "lava is a hot bath and a lost step, not a pit. Put it on the lava sea, rivers, falls and waves.")]
    public sealed class ScaldAbility : Ability
    {
        public int damage = 1;
        [Doc("Upward speed he is thrown out at.")] public float launch = 9f;
        [Doc("Seconds between burns while he's in it.")] public float interval = 0.6f;
        [EffectRef, Doc("Status applied on a burn ('' = none).")] public string effect = "";

        float lastAt = -99f;

        public override void OnSpawn() => lastAt = -99f;

        public override void OnContact(Entity other, bool entering)
        {
            if (other == null || !other.IsPlayer || !other.IsAlive || !Owner.IsAlive)
                return;
            // Out of the lava every time, burned at most every 'interval'.
            if (other.Body != null)
            {
                var v = other.Body.linearVelocity;
                v.y = Mathf.Max(v.y, launch);
                other.Body.linearVelocity = v;
            }
            float now = Owner.Now;
            if (now - lastAt < interval)
                return;
            lastAt = now;
            other.Damage(damage, Owner);
            if (!string.IsNullOrEmpty(effect))
                other.ApplyStatus(effect, Owner);
            FloorKit.Record(Owner, "scald", damage);
        }

        public override void SaveState(StateWriter writer) => writer.Float(lastAt);
        public override void LoadState(StateReader reader) => lastAt = reader.Float();
    }

    [Serializable, JsonType("lavaSea")]
    [Doc("The lava sea at the bottom of a floor: it stays under the camera sideways and rises when the script says. Its speed is the " +
         "counter 'speedCounter' (units per second, 0 = still) and it rises at most 'maxCounter' units above where it spawned, so it stops " +
         "under a breather ledge or the landing. Every new rise is telegraphed: it bubbles and rumbles for 'telegraph' seconds first. " +
         "Draws its glowing surface and the heat shimmer above it.")]
    public sealed class LavaSeaAbility : Ability
    {
        [Doc("Counter with its rise speed.")] public string speedCounter = "lavaSpeed";
        [Doc("Counter with how far it may rise (units above where it spawned).")] public string maxCounter = "lavaMax";
        [Doc("Seconds of bubbling and rumbling before a new rise starts (at least 1).")] public float telegraph = 2f;
        [Doc("Stay under the camera sideways (a sea, not a pool).")] public bool followCamera = true;
        [SoundRef, Doc("The rumble played when a rise is telegraphed.")] public string rumbleSound = "";
        public Color surfaceColor = new Color(1f, 0.8f, 0.35f, 1f);

        Vector2 home;
        float risen;
        float lastSpeed;
        float riseFrom = -1f;

        public override void OnSpawn()
        {
            home = Owner.Position;
            risen = 0f;
            lastSpeed = 0f;
            riseFrom = -1f;
        }

        public override void Tick(float now, float dt)
        {
            var world = Owner.World;
            float speed = Mathf.Max(0f, world.GetCounter(speedCounter));
            float max = world.GetCounter(maxCounter);
            if (speed > 0f && lastSpeed <= 0f)
            {
                // A new rise: bubbles and a rumble first.
                riseFrom = now + Mathf.Max(1f, telegraph);
                world.Effects.Shake(0.06f, Mathf.Max(1f, telegraph));
                SickOfIt.Core.Audio.Sfx.Play(rumbleSound);
                FloorKit.Record(Owner, "lavaWarn", Mathf.RoundToInt(speed * 100f));
                Owner.RecordNow();
            }
            lastSpeed = speed;
            bool bubbling = riseFrom >= 0f && now < riseFrom;
            if (speed > 0f && !bubbling && risen < max)
                risen = Mathf.Min(max, risen + speed * dt);

            float x = followCamera ? world.CameraCenter.x : home.x;
            FloorKit.Moved(Owner, new Vector2(x, home.y + risen), dt);
            DrawSurface(now, bubbling);
        }

        /// <summary>How far it has risen (the playtest reads it).</summary>
        public float Risen => risen;

        void DrawSurface(float now, bool bubbling)
        {
            var c = Owner.MainCollider;
            if (c == null)
                return;
            var b = c.bounds;
            var surface = FloorKit.Child(Owner, "Surface", Owner.Def.visual.layer, SpriteLayers.Projectile + 1);
            float pulse = bubbling ? 0.5f + 0.5f * Mathf.Sin(now * 18f) : 0.15f * Mathf.Sin(now * 3f);
            surface.color = Color.Lerp(surfaceColor, Color.white, Mathf.Max(0f, pulse) * 0.5f);
            FloorKit.PlaceWorld(Owner, surface, new Vector2(b.center.x, b.max.y - 0.04f), new Vector2(b.size.x, bubbling ? 0.16f : 0.09f));
            // Heat shimmer: faint bands wobbling over the surface (cosmetic).
            for (int i = 0; i < 3; i++)
            {
                var haze = FloorKit.Child(Owner, "Haze" + i, SpriteLayer.Foreground1, 0);
                haze.color = new Color(1f, 0.55f, 0.25f, 0.07f - i * 0.018f);
                float wobble = Mathf.Sin(now * (2.1f + i) + i * 1.3f) * 0.25f;
                FloorKit.PlaceWorld(Owner, haze, new Vector2(b.center.x + wobble, b.max.y + 0.35f + i * 0.45f), new Vector2(b.size.x, 0.35f + 0.08f * Mathf.Sin(now * 3f + i)));
            }
            // Bubbles along the surface while a rise is telegraphed.
            for (int i = 0; i < 6; i++)
            {
                var bubble = FloorKit.Child(Owner, "Bubble" + i, Owner.Def.visual.layer, SpriteLayers.Projectile + 2);
                bubble.enabled = bubbling;
                if (!bubbling)
                    continue;
                bubble.color = new Color(1f, 0.9f, 0.5f, 1f);
                float u = Mathf.Repeat(now * 0.7f + i * 0.37f, 1f);
                float x = b.min.x + Mathf.Repeat(i * 0.173f + Mathf.Floor(now * 0.7f + i * 0.37f) * 0.31f, 1f) * b.size.x;
                FloorKit.PlaceWorld(Owner, bubble, new Vector2(x, b.max.y + u * 0.35f), Vector2.one * (0.14f * (1f - u) + 0.04f));
            }
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(risen);
            writer.Float(lastSpeed);
            writer.Float(riseFrom);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            risen = reader.Float();
            lastSpeed = reader.Float();
            riseFrom = reader.Float();
        }
    }

    [Serializable, JsonType("surge")]
    [Doc("A chase: a wall of lava (or a collapsing bridge's fall) that comes after him sideways at 'speed', never further than 'behind' units " +
         "past the trailing edge of the screen, and sinks away once it reaches 'stopAt'. It rumbles for 'telegraph' seconds before it moves. " +
         "Pair it with a path section's autoSpeed.")]
    public sealed class SurgeAbility : Ability
    {
        [Doc("1 = comes from the left and runs right, -1 = the other way.")] public float direction = 1f;
        [Doc("Units per second.")] public float speed = 1.5f;
        [Doc("Most it lags behind the screen's trailing edge (it catches up).")] public float behind = 1f;
        [Doc("Seconds it rumbles before it moves.")] public float telegraph = 1.5f;
        [Doc("x (from the path's origin) where it stops and sinks. 0 = it never stops.")] public float stopAt;
        [Doc("Stay level with the camera (a wall of lava filling the screen's height).")] public bool followCameraY = true;
        [Doc("Stop and sink once the floor's path turns away from its run (the chase is over when the climb starts).")] public bool stopWhenPathTurns = true;

        float x;
        float sinking;
        bool started;

        public override void OnSpawn()
        {
            x = Owner.Position.x;
            sinking = 0f;
            started = false;
        }

        public override void Tick(float now, float dt)
        {
            var world = Owner.World;
            float dir = direction < 0f ? -1f : 1f;
            float age = now - Owner.SpawnTime;
            Rect view = world.View;
            float trailing = dir > 0f ? view.xMin : view.xMax;
            float halfWidth = Owner.MainCollider != null ? Owner.MainCollider.bounds.extents.x : 0.5f;
            float stop = world.Effects.PathOrigin.x + stopAt;
            bool stopped = stopAt != 0f && (dir > 0f ? x >= stop : x <= stop);
            var section = world.Effects.CurrentSection;
            if (stopWhenPathTurns && started && section != null && section.Axis.x * dir < 0.1f)
                stopped = true;
            if (age >= Mathf.Max(0f, telegraph) && !stopped)
            {
                if (!started)
                {
                    started = true;
                    FloorKit.Record(Owner, "surge");
                    Owner.RecordNow();
                }
                x += dir * speed * dt;
                // Never so far behind it stops being a threat: its front edge stays within 'behind' of the screen's edge.
                float front = x + dir * halfWidth;
                float least = trailing - dir * behind;
                if (dir * (front - least) < 0f)
                    x = least - dir * halfWidth;
            }
            if (stopped)
                sinking = Mathf.Min(20f, sinking + 2.5f * dt);
            float y = followCameraY ? world.CameraCenter.y : Owner.Position.y;
            FloorKit.Moved(Owner, new Vector2(x, y - sinking), dt);
        }

        public float X => x;

        public override void SaveState(StateWriter writer)
        {
            writer.Float(x);
            writer.Float(sinking);
            writer.Bool(started);
        }

        public override void LoadState(StateReader reader)
        {
            x = reader.Float();
            sinking = reader.Float();
            started = reader.Bool();
        }
    }

    [Serializable, JsonType("lavaFall")]
    [Doc("A lava fall: a curtain pouring down the wall on a beat. It pours for 'pour' seconds, thins and sputters 1 s before it stops, stays " +
         "off for 'off' seconds (pass now), gurgles 1 s before it pours again. Only hurts while pouring. Hell's loop: the beat never changes.")]
    public sealed class LavaFallAbility : Ability
    {
        public float pour = 3f;
        public float off = 2.5f;
        [Doc("Where in its beat it starts (seconds).")] public float offset;
        public Color color = new Color(1f, 0.45f, 0.1f, 0.9f);

        public override void Tick(float now, float dt)
        {
            float cycle = Mathf.Max(0.5f, pour) + Mathf.Max(0.5f, off);
            float t = Mathf.Repeat(now - Owner.SpawnTime + offset, cycle);
            bool pouring = t < pour;
            float thin = pouring ? Mathf.Clamp01((pour - t) / 1f) : 0f;          // 1 → 0 over the last second of the pour
            float gurgle = !pouring ? Mathf.Clamp01(1f - (cycle - t) / 1f) : 0f; // 0 → 1 over the last second off
            var c = Owner.MainCollider;
            if (c != null)
                c.enabled = pouring;
            var look = FloorKit.Look(Owner);
            if (look != null)
            {
                float a = pouring ? Mathf.Lerp(0.35f, 1f, thin) : gurgle * 0.3f;
                look.color = new Color(color.r, color.g, color.b, color.a * a);
                // The box placeholder narrows as it thins (the entity's own art only fades: scaling it would scale the body).
                if (look != Owner.Sprite)
                {
                    float width = pouring ? Mathf.Lerp(0.35f, 1f, thin) : 0.2f + gurgle * 0.2f;
                    var scale = look.transform.localScale;
                    look.transform.localScale = new Vector3(Owner.Def.body.size.x * width, scale.y, 1f);
                }
            }
        }
    }

    [Serializable, JsonType("carved")]
    [Doc("Words carved into a platform, glowing faintly: the floor's landing with its number (\"{floor}\" is the floor counter) and a word " +
         "or two the damned carved under it (KEEP MOVING).")]
    public sealed class CarvedAbility : Ability
    {
        [Doc("The big carving; \"{floor}\" is replaced by the floor counter.")] public string text = "{floor}";
        [Doc("A smaller line under it ('' = none).")] public string words = "";
        public Color color = new Color(1f, 0.72f, 0.4f, 1f);
        [Doc("Height of the big carving (world units).")] public float size = 1.1f;
        [Doc("Where it stands above the platform's top.")] public float above = 0.15f;

        public override void OnSpawn()
        {
            int floor = Owner.World.Stage != null ? Owner.World.Stage.Floor : 0;
            Write("Carving", text.Replace("{floor}", floor > 0 ? floor.ToString() : ""), size);
            Write("Words", words ?? "", size * 0.38f);
        }

        // Laid out every tick: the platform may have been stretched after it spawned, and it may sink or ride.
        public override void Tick(float now, float dt)
        {
            Layout("Carving", size, above + size * 0.5f);
            Layout("Words", size * 0.38f, above + size + size * 0.25f);
        }

        void Write(string name, string value, float height)
        {
            var t = Owner.transform.Find(name);
            TMPro.TextMeshPro mesh;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(Owner.transform, false);
                mesh = go.AddComponent<TMPro.TextMeshPro>();
                var theme = GameDefinition.Instance != null ? GameDefinition.Instance.menuTheme : null;
                mesh.font = SickOfIt.Core.UI.Menus.MenuGraphics.FontAsset(theme != null ? theme.font : null);
                mesh.alignment = TMPro.TextAlignmentOptions.Center;
                mesh.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                mesh.fontStyle = TMPro.FontStyles.Bold;
                mesh.sortingLayerID = SortingLayer.NameToID(SpriteLayer.Background2.ToString());
                mesh.sortingOrder = 903;
            }
            else
            {
                mesh = t.GetComponent<TMPro.TextMeshPro>();
            }
            if (mesh == null)
                return;
            mesh.gameObject.SetActive(!string.IsNullOrEmpty(value));
            mesh.text = value;
            mesh.color = color;
            mesh.enableAutoSizing = true;
            mesh.fontSizeMin = 0.5f;
            mesh.fontSizeMax = 12f * height;
            mesh.rectTransform.sizeDelta = new Vector2(Mathf.Max(2f, height * Mathf.Max(1, value.Length) * 0.8f), height);
            Layout(name, height, 0f);
        }

        void Layout(string name, float height, float y)
        {
            var t = Owner.transform.Find(name);
            if (t == null || !t.gameObject.activeSelf)
                return;
            Vector3 scale = Owner.transform.lossyScale;
            t.position = new Vector3(Owner.Position.x, FloorKit.Top(Owner) + y, 0f);
            t.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 1f);
        }

        /// <summary>The carving's text now (the playtest reads it).</summary>
        public string Carving
        {
            get
            {
                var t = Owner != null ? Owner.transform.Find("Carving") : null;
                var mesh = t != null ? t.GetComponent<TMPro.TextMeshPro>() : null;
                return mesh != null ? mesh.text : "";
            }
        }
    }
}
