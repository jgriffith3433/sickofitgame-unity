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

        /// <summary>The piece has its painted art (no box placeholder).</summary>
        public static bool HasArt(Entity owner) => owner.transform.Find("Shape") == null;

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
        [Doc("Hell's loop (The Loop, 995): it also sinks by itself every 'loopPeriod' seconds of the game clock (0 = only when stood on). " +
             "The clock is the level's, not the stone's, so every stone with the same loop settings does the same thing at the same time, " +
             "lap after lap, and a rewind replays it exactly.")]
        public float loopPeriod;
        [Doc("Where in the loop it sinks (0-1 of loopPeriod): stones in a row with rising phases sink in order.")] public float loopPhase;
        [Doc("Seconds it shakes and hisses before it sinks by itself (at least 1: the telegraph).")] public float loopTell = 1f;
        [Doc("Seconds it stays under before it bobs back up.")] public float loopDown = 1.5f;

        Vector2 home;
        float sunk;
        bool stoodOn;
        bool signalled;
        int loopStage = -1;

        public override void OnSpawn()
        {
            home = Owner.Position;
            sunk = 0f;
            stoodOn = false;
            signalled = false;
            loopStage = -1;
        }

        /// <summary>The loop's stage now: 0 up, 1 telling (shaking), 2 going under / under; -1 no loop.</summary>
        int LoopStage(float now)
        {
            if (loopPeriod <= 0f)
                return -1;
            float period = Mathf.Max(loopPeriod, Mathf.Max(1f, loopTell) + loopDown + 0.5f);
            float t = Mathf.Repeat(now - loopPhase * period, period);
            float tell = Mathf.Max(1f, loopTell);
            return t < tell ? 1 : t < tell + Mathf.Max(0.2f, loopDown) ? 2 : 0;
        }

        public override void Tick(float now, float dt)
        {
            int stage = LoopStage(now);
            if (stage != loopStage)
            {
                if (stage == 1)
                    FloorKit.Record(Owner, "loopTell");
                else if (stage == 2)
                {
                    SickOfIt.Core.Audio.Sfx.Play(sound);
                    FloorKit.Record(Owner, "loopSink");
                }
                loopStage = stage;
                Owner.RecordNow();
            }
            if (stage == 2)
            {
                sunk = Mathf.Min(depth, sunk + sinkSpeed * 1.4f * dt);
                FloorKit.Moved(Owner, home + Vector2.down * sunk, dt);
                Owner.Animation.SetBool("Sinking", true);
                FloorKit.Tint(Owner, Color.Lerp(FloorKit.BaseColor(Owner), new Color(1f, 0.4f, 0.1f, 1f), FloorKit.HasArt(Owner) ? 0.35f : 0.7f));
                stoodOn = Owner.World.PlayerStandsOn(Owner);
                return;
            }
            if (stage == 1)
            {
                // The tell: it trembles and steams on the spot for a second before it goes.
                float shake = Mathf.Sin(now * 60f) * 0.05f;
                FloorKit.Moved(Owner, home + new Vector2(shake, -Mathf.Max(sunk, dip)), dt);
                Owner.Animation.SetBool("Sinking", true);
                stoodOn = Owner.World.PlayerStandsOn(Owner);
                return;
            }
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
            // Painted art steams while it is stood on and going under (animator bool "Sinking").
            Owner.Animation.SetBool("Sinking", on || sunk > dip);
            // Hot as it goes under (cosmetic; the art is only warmed a little, its steam frames do the rest).
            float heat = Mathf.Clamp01(sunk / Mathf.Max(0.01f, depth)) * (FloorKit.HasArt(Owner) ? 0.35f : 0.7f);
            FloorKit.Tint(Owner, Color.Lerp(FloorKit.BaseColor(Owner), new Color(1f, 0.4f, 0.1f, 1f), heat));
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
            writer.Int(loopStage);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            sunk = reader.Float();
            stoodOn = reader.Bool();
            signalled = reader.Bool();
            loopStage = reader.Int();
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
                FloorKit.Tint(Owner, Color.Lerp(FloorKit.BaseColor(Owner), new Color(0.9f, 0.75f, 0.6f, 1f), Mathf.PingPong(t * 6f, 1f) * (FloorKit.HasArt(Owner) ? 0.2f : 0.5f)));
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
            // Painted art: cracks the moment he lands, breaks up as it falls (animator bools "Cracked", "Falling").
            Owner.Animation.SetBool("Cracked", crackedAt >= 0f);
            Owner.Animation.SetBool("Falling", falling);
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
        [Doc("It bobs where it spawned until the hero first stands on it, then drifts (a ride that waits for him: 959's sign).")]
        public bool holdUntilBoarded;

        Vector2 home;
        float travelled;
        float heading = 1f;
        float tilt;
        bool carried;
        bool boarded;

        public override void OnSpawn()
        {
            home = Owner.Position;
            travelled = 0f;
            heading = 1f;
            tilt = 0f;
            carried = false;
            boarded = false;
        }

        public override void Tick(float now, float dt)
        {
            bool on = Owner.World.PlayerStandsOn(Owner);
            if (on)
                boarded = true;
            bool drifting = !holdUntilBoarded || boarded;
            float drift = current.magnitude;
            Vector2 axis = drift > 0.0001f ? current / drift : Vector2.right;
            float speed = drifting ? drift : 0f;
            travelled += speed * heading * dt;
            if (range > 0f)
            {
                if (travelled >= range) { travelled = range; heading = -1f; }
                else if (travelled <= 0f) { travelled = 0f; heading = 1f; }
            }
            float age = now - Owner.SpawnTime;
            float wave = Mathf.Sin((age / Mathf.Max(0.2f, bobPeriod) + phase) * Mathf.PI * 2f);
            Vector2 want = home + axis * travelled + Vector2.up * (wave * bob);

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
            writer.Bool(boarded);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            travelled = reader.Float();
            heading = reader.Float();
            tilt = reader.Float();
            carried = reader.Bool();
            boarded = reader.Bool();
        }
    }

    [Serializable, JsonType("conveyor")]
    [Doc("A conveyor slag flow: a crust of slag sliding along on the lava. Standing on it carries him at 'speed' (with it or against it); " +
         "its glowing seams crawl along so it reads before he steps on.")]
    public sealed class ConveyorAbility : Ability
    {
        [Doc("Units per second it carries him (negative = to the left).")] public float speed = 1f;
        [Doc("Colour of the crawling seams.")] public Color seamColor = new Color(1f, 0.5f, 0.15f, 1f);
        [Doc("Painted crust laid along the body (instead of the box); the seams crawl over it.")] public ArtLoopDef art = new ArtLoopDef();
        [Doc("Height of the painted crust (units).")] public float artHeight = 0.75f;
        [Doc("How far the painted top stands above the body's top (units; its glowing rim).")] public float artLip = 0.04f;

        public override void Tick(float now, float dt)
        {
            if (Owner.World.PlayerStandsOn(Owner))
                Owner.World.CarryPlayer(new Vector2(speed, 0f));

            // Three seams crawling along the top (cosmetic, from the game clock so a rewind runs them back).
            var c = Owner.MainCollider;
            if (c == null)
                return;
            var b = c.bounds;
            if (art.IsSet)
            {
                // Painted: tiles fitted to the body's length, so the crust ends where the foothold does.
                float y = b.max.y + artLip - artHeight * 0.5f;
                bool drawn = FloorArt.Strip(Owner.transform, "BeltArt", art, FloorArt.Frames(art, Owner.World),
                    new Vector2(b.min.x, y), new Vector2(b.max.x, y), artHeight, now, Owner.Def.visual.layer, SpriteLayers.Platform, true);
                var box = FloorKit.Look(Owner);
                if (drawn && box != null && box != Owner.Sprite)
                    box.enabled = false;
            }
            for (int i = 0; i < 3; i++)
            {
                var seam = FloorKit.Child(Owner, "Seam" + i, Owner.Def.visual.layer, SpriteLayers.Platform + 1);
                seam.color = seamColor;
                float u = Mathf.Repeat(now * speed / Mathf.Max(0.1f, b.size.x) + i / 3f, 1f);
                FloorKit.PlaceWorld(Owner, seam, new Vector2(b.min.x + u * b.size.x, b.max.y - 0.06f), new Vector2(0.35f, 0.06f));
            }
        }
    }

    [Serializable, JsonType("ladder")]
    [Doc("A ladder: the entity's body is the climbable zone (a tall, narrow box; stretch it with the spawn's scale). The hero grabs it by " +
         "pressing up (W, up arrow, the stick or the touch stick pushed up) while he overlaps it, or down while he stands on its top. Held, he has no " +
         "gravity, climbs and descends with up and down at 'climbSpeed', can shoot (guns at 'fireRate' of their rate), and lets go with Jump " +
         "(straight up, or out the way the stick points) or by climbing off its top (he steps out on whatever stands at its top) or its bottom. " +
         "Rewind brings back the climb: where he was on the rungs and whether he held on.")]
    public sealed class LadderAbility : Ability
    {
        [Doc("Units per second he climbs at full stick.")] public float climbSpeed = 2.6f;
        [Doc("How far beside the zone (units, each side) his body may be and still grab on.")] public float grabReach = 0.25f;
        [Doc("Sideways speed (units per second) a jump off leaves with, in the direction the stick points; 0 jumps straight up.")] public float jumpOffSpeed = 1.5f;
        [Doc("Fraction of the guns' rate while climbing (1 = as on the ground, 0.5 = half as fast). A melee weapon is unchanged.")] public float fireRate = 0.55f;
        [Doc("Seconds after letting go before he can grab it again.")] public float regrab = 0.35f;
        [Doc("Units per second he slides to the middle of the rungs when he grabs on.")] public float centerSpeed = 8f;
        [Doc("Spacing of the drawn rungs (units).")] public float rungSpacing = 0.45f;
        public Color railColor = new Color(0.45f, 0.3f, 0.2f, 1f);
        public Color rungColor = new Color(0.6f, 0.42f, 0.28f, 1f);
        [SoundRef, Doc("Played when he grabs on.")] public string grabSound = "";

        const int MaxRungs = 48;
        bool holding;
        float releasedAt = -99f;

        public override void OnSpawn()
        {
            holding = false;
            releasedAt = -99f;
        }

        public bool Holding => holding;

        public override void Tick(float now, float dt)
        {
            var zone = Owner.MainCollider;
            if (zone == null)
                return;
            Bounds lb = zone.bounds;
            Draw(lb);

            var world = Owner.World;
            var controller = world.PlayerController;
            var player = world.Player;
            if (controller == null || player == null || !player.IsAlive || controller.BodyCollider == null || controller.Suspended)
            {
                holding = false;
                return;
            }

            Bounds hb = controller.BodyCollider.bounds;
            float x = lb.center.x;
            float stick = controller.MoveY;

            if (holding)
            {
                if (controller.ConsumeJumpOff())
                {
                    holding = false;
                    releasedAt = now;
                    FloorKit.Record(Owner, "ladderJumpOff", Mathf.RoundToInt(hb.min.y * 100f));
                    Owner.RecordNow();
                    return;
                }
                float vy = stick * climbSpeed;
                Vector2 at = player.Position;
                float dx = Mathf.MoveTowards(at.x, x, centerSpeed * dt) - at.x;
                float dy = vy * dt;
                float feet = hb.min.y;
                string left = null;
                if (feet + dy >= lb.max.y)
                {
                    // Over the top: he steps out on what stands there (his feet a hair above it; gravity does the rest).
                    dy = lb.max.y + 0.02f - feet;
                    left = "ladderTop";
                }
                else if (feet + dy <= lb.min.y)
                {
                    dy = lb.min.y - feet;
                    left = "ladderBottom";
                }
                world.HoldPlayerClimbing(at + new Vector2(dx, dy), left != null ? Vector2.zero : new Vector2(0f, vy),
                    controller.MoveX * jumpOffSpeed, fireRate);
                if (left != null)
                {
                    holding = false;
                    releasedAt = now;
                    FloorKit.Record(Owner, left, Mathf.RoundToInt((feet + dy) * 100f));
                    Owner.RecordNow();
                }
                return;
            }

            if (now - releasedAt < regrab || controller.IsHeld)
                return;
            bool beside = Mathf.Abs(hb.center.x - x) <= lb.extents.x + grabReach;
            if (!beside)
                return;
            // Up while on the rungs' height (not already standing at the very top), or down while standing on the top.
            bool up = stick > 0.5f && hb.max.y > lb.min.y + 0.2f && hb.min.y < lb.max.y - 0.3f;
            bool down = stick < -0.5f && controller.IsGrounded && Mathf.Abs(hb.min.y - lb.max.y) < 0.25f;
            if (!up && !down)
                return;
            holding = true;
            controller.ConsumeJumpOff();
            // From the top he steps down onto the rungs (feet a little under the top), so the first push up doesn't just walk him off again.
            Vector2 start = player.Position;
            if (down)
                start.y -= hb.min.y - (lb.max.y - 0.35f);
            world.HoldPlayerClimbing(start, Vector2.zero, 0f, fireRate);
            SickOfIt.Core.Audio.Sfx.Play(grabSound);
            FloorKit.Record(Owner, down ? "ladderGrabTop" : "ladderGrab", Mathf.RoundToInt(hb.min.y * 100f));
            Owner.RecordNow();
        }

        // Two rails and rungs between them over the zone (cosmetic; the zone's box is invisible to the hero's feet).
        void Draw(Bounds b)
        {
            var rails = new[] { -1f, 1f };
            for (int i = 0; i < rails.Length; i++)
            {
                var rail = FloorKit.Child(Owner, "Rail" + i, Owner.Def.visual.layer, SpriteLayers.Platform);
                rail.color = railColor;
                FloorKit.PlaceWorld(Owner, rail, new Vector2(b.center.x + rails[i] * (b.extents.x - 0.06f), b.center.y), new Vector2(0.1f, b.size.y));
            }
            int count = Mathf.Min(MaxRungs, Mathf.Max(1, Mathf.FloorToInt(b.size.y / Mathf.Max(0.15f, rungSpacing))));
            for (int i = 0; i < MaxRungs; i++)
            {
                var rung = FloorKit.Child(Owner, "Rung" + i, Owner.Def.visual.layer, SpriteLayers.Platform);
                rung.enabled = i < count;
                if (i >= count)
                    continue;
                rung.color = rungColor;
                float y = b.min.y + (i + 0.5f) * (b.size.y / count);
                FloorKit.PlaceWorld(Owner, rung, new Vector2(b.center.x, y), new Vector2(Mathf.Max(0.1f, b.size.x - 0.12f), 0.07f));
            }
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Bool(holding);
            writer.Float(releasedAt);
        }

        public override void LoadState(StateReader reader)
        {
            holding = reader.Bool();
            releasedAt = reader.Float();
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
        [Doc("Painted chain links laid from the pivot to the handle (instead of the drawn line).")] public ArtLoopDef chainArt = new ArtLoopDef();
        [Doc("Width of the painted chain (units).")] public float chainWidth = 0.22f;
        [SoundRef, Doc("Played when he grabs it.")] public string grabSound = "";
        [Doc("Moves the pivot from straight above the spawn point (units): a boss whose swing hangs over a higher tier.")]
        public Vector2 pivotOffset;

        Vector2 pivot;
        bool holding;
        float releasedAt = -99f;

        public override void OnSpawn()
        {
            pivot = Owner.Position + Vector2.up * length + pivotOffset;
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
            chain.enabled = !FloorArt.Strip(Owner.transform, "ChainArt", chainArt, FloorArt.Frames(chainArt, world), pivot, handle, chainWidth, now,
                Owner.Def.visual.layer, SpriteLayers.Platform, true);
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
        [Doc("Painted chain it hangs from (instead of the drawn line).")] public ArtLoopDef chainArt = new ArtLoopDef();
        [Doc("Width of the painted chain (units).")] public float chainWidth = 0.22f;
        [Doc("Where the chain meets the platform, above its centre (units): the ring of its painted art.")] public float chainFrom = 0.2f;
        [SoundRef, Doc("Played on each jolt (clank).")] public string joltSound = "";
        [Doc("Imps cutting its chain (Chain Lift, 994): while a live enemy with this tag is within cutRange of the lift and it is running, " +
             "every cutEvery seconds the chain gives and the lift drops a notch (sparks, a jolt, the cutSound). Shoot the cutters and it holds. '' = none.")]
        public string cutterTag = "";
        public float cutRange = 6f;
        [Doc("Seconds between cuts (the first comes a full cutEvery after he steps on: the telegraph is the imp hacking at the chain).")] public float cutEvery = 3f;
        [Doc("How far each cut drops it (units).")] public float notch = 0.5f;
        [Doc("Most it can drop in all.")] public float maxCut = 2f;
        [SoundRef, Doc("Played when the chain gives.")] public string cutSound = "";
        [Doc("Signal emitted each time the chain gives ('' = none): the hero's line at the first jolt.")] public string cutSignal = "";

        Vector2 home;
        float startedAt = -1f;
        int lastStage = -1;
        float cut;
        float cutClock;

        public override void OnSpawn()
        {
            home = Owner.Position;
            startedAt = startOnStand ? -1f : Owner.SpawnTime;
            lastStage = -1;
            cut = 0f;
            cutClock = 0f;
        }

        bool CutterNear()
        {
            foreach (var e in Owner.World.Live)
                if (e != null && e.IsAlive && e.MaxHealth > 0 && e.Tags.Contains(cutterTag) && Vector2.Distance(e.Position, Owner.Position) <= cutRange)
                    return true;
            return false;
        }

        /// <summary>How far the imps' cuts have dropped it (the playtest reads it).</summary>
        public float Cut => cut;

        /// <summary>How far up its ride it is now, 0 at the bottom (the playtest bot reads it: it steps on a lift waiting below).</summary>
        public float Raised => travel.sqrMagnitude > 0f ? Vector2.Dot(Owner.Position - home, travel.normalized) : 0f;

        void TickCut(float dt)
        {
            if (string.IsNullOrEmpty(cutterTag) || startedAt < 0f || cut >= maxCut || !CutterNear())
                return;
            cutClock += dt;
            if (cutClock < Mathf.Max(1f, cutEvery))
                return;
            cutClock = 0f;
            cut = Mathf.Min(maxCut, cut + notch);
            SickOfIt.Core.Audio.Sfx.Play(cutSound);
            SickOfIt.Core.Fx.Fx.Dust(new Vector2(Owner.Position.x, FloorKit.Top(Owner)), 0.6f);
            if (Owner.World.View.Contains(Owner.Position))
                Owner.World.Effects.Shake(0.12f, 0.4f);
            FloorKit.Record(Owner, "liftCut", Mathf.RoundToInt(cut * 100f));
            Owner.RecordNow();
            if (!string.IsNullOrEmpty(cutSignal))
                Owner.World.Emit(cutSignal);
        }

        public override void Tick(float now, float dt)
        {
            if (startedAt < 0f && Owner.World.PlayerStandsOn(Owner))
            {
                startedAt = now;
                FloorKit.Record(Owner, "liftStart");
                Owner.RecordNow();
            }
            TickCut(dt);
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
            Vector2 want = home + travel * e + new Vector2(0f, shake * Mathf.Sin(now * 70f) * 0.04f - cut);
            FloorKit.Moved(Owner, want, dt);

            // The chain it hangs from, up past the top of its ride (cosmetic).
            var chain = FloorKit.Child(Owner, "Chain", Owner.Def.visual.layer, SpriteLayers.Platform - 1);
            chain.color = chainColor;
            float top = home.y + Mathf.Max(0f, travel.y) + 4f;
            float y = Owner.Position.y;
            FloorKit.PlaceWorld(Owner, chain, new Vector2(Owner.Position.x, (top + y) * 0.5f), new Vector2(0.07f, Mathf.Max(0.1f, top - y)));
            chain.enabled = !FloorArt.Strip(Owner.transform, "ChainArt", chainArt, FloorArt.Frames(chainArt, Owner.World),
                new Vector2(Owner.Position.x, top), new Vector2(Owner.Position.x, y + chainFrom), chainWidth, now,
                Owner.Def.visual.layer, SpriteLayers.Platform - 1, true);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x);
            writer.Float(home.y);
            writer.Float(startedAt);
            writer.Int(lastStage);
            writer.Float(cut);
            writer.Float(cutClock);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            startedAt = reader.Float();
            lastStage = reader.Int();
            cut = reader.Float();
            cutClock = reader.Float();
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
        [Doc("Painted chain going up from it to the pulley (none without art).")] public ArtLoopDef chainArt = new ArtLoopDef();
        [Doc("How far above where it spawned the pulley is (units).")] public float chainHeight = 4f;
        [Doc("Width of the painted chain (units).")] public float chainWidth = 0.2f;

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
            FloorArt.Strip(Owner.transform, "ChainArt", chainArt, FloorArt.Frames(chainArt, world),
                new Vector2(Owner.Position.x, home.y + chainHeight), new Vector2(Owner.Position.x, Owner.Position.y), chainWidth, now,
                Owner.Def.visual.layer, SpriteLayers.Platform - 1, true);
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
        [Doc("Painted shimmer laid up the column (instead of the box); art.scroll runs it upward.")] public ArtLoopDef art = new ArtLoopDef();
        [Doc("Width of the painted shimmer as a share of the body's width (it fades out at its sides).")] public float artWidth = 1.5f;

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
            if (art.IsSet)
            {
                // Laid from the foot up, so a positive scroll rises with the air.
                bool drawn = FloorArt.Strip(Owner.transform, "UpdraftArt", art, FloorArt.Frames(art, Owner.World),
                    new Vector2(b.center.x, b.min.y), new Vector2(b.center.x, b.max.y), b.size.x * artWidth, now,
                    Owner.Def.visual.layer, Owner.Def.visual.SortingOrder, true);
                var box = FloorKit.Look(Owner);
                if (drawn && box != null && box != Owner.Sprite)
                    box.enabled = false;
            }
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
        [Doc("Painted surface: a strip laid along the top (its glowing edge and the lava under it), instead of the drawn line.")]
        public ArtLoopDef surfaceArt = new ArtLoopDef();
        [Doc("Height of the painted surface strip (units).")] public float surfaceHeight = 1.2f;
        [Doc("Where the strip's centre sits from the top of the lava (units; negative = below, so its edge lines up with the top).")]
        public float surfaceOffset = -0.25f;

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
            float y = b.max.y + surfaceOffset;
            // The painted surface flares while a rise is telegraphed (the same tell as the drawn line).
            bool painted = FloorArt.Strip(Owner.transform, "SurfaceArt", surfaceArt, FloorArt.Frames(surfaceArt, Owner.World),
                new Vector2(b.min.x, y), new Vector2(b.max.x, y), surfaceHeight, now, Owner.Def.visual.layer,
                Mathf.Max(SpriteLayers.Projectile + 1, Owner.Def.visual.SortingOrder + 1), false);
            if (painted)
            {
                surface.color = new Color(1f, 0.95f, 0.7f, bubbling ? 0.35f + 0.35f * Mathf.Max(0f, pulse) : 0f);
            }
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
        [Doc("Painted wall laid down the body (instead of the box): vertical art with its crest painted on the right, turned to face the run.")]
        public ArtLoopDef art = new ArtLoopDef();
        [Doc("Width of the painted wall as a share of the body's width (the crest's spray reaches past the burning edge).")] public float artWidth = 1.15f;
        [Doc("How far the painted wall's centre sits ahead of the body's, toward its run (units): lines its crest up with the edge that burns.")]
        public float artAhead;

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
            bool stopped = sinking > 0f || (stopAt != 0f && (dir > 0f ? x >= stop : x <= stop));
            var section = world.Effects.CurrentSection;
            if (stopWhenPathTurns && started && section != null && (section.Axis.x * dir < 0.1f || HasLanding(section)))
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
            DrawArt(now, dir);
        }

        static bool HasLanding(PathSectionDef section)
        {
            if (section.spawns == null)
                return false;
            for (int i = 0; i < section.spawns.Count; i++)
            {
                var s = section.spawns[i];
                if (s != null && (s.id == "landing" || (s.entity != null && s.entity.StartsWith("floorLanding", StringComparison.Ordinal))))
                    return true;
            }
            return false;
        }

        void DrawArt(float now, float dir)
        {
            var c = Owner.MainCollider;
            if (!art.IsSet || c == null)
                return;
            var b = c.bounds;
            float cx = b.center.x + dir * artAhead;
            // Vertical art goes from 'from' (its top) to 'to': laid bottom to top it turns half round, so the crest faces left.
            Vector2 top = new Vector2(cx, b.max.y), bottom = new Vector2(cx, b.min.y);
            bool drawn = FloorArt.Strip(Owner.transform, "SurgeArt", art, FloorArt.Frames(art, Owner.World),
                dir > 0f ? top : bottom, dir > 0f ? bottom : top, b.size.x * artWidth, now, Owner.Def.visual.layer, Owner.Def.visual.SortingOrder, true);
            var box = FloorKit.Look(Owner);
            if (drawn && box != null && box != Owner.Sprite)
                box.enabled = false;
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
        [Doc("Painted curtain laid down the fall (instead of the box); it thins and fades on the same beat.")] public ArtLoopDef art = new ArtLoopDef();
        [Doc("Width of the painted curtain at full pour, as a share of the body's width (the art has a margin at its sides).")] public float artWidth = 1.6f;

        /// <summary>Seconds until it next pours (0 while it pours): the playtest bot reads it to time a pass, like a player counting the beat.</summary>
        public float UntilPour(float now)
        {
            float cycle = Mathf.Max(0.5f, pour) + Mathf.Max(0.5f, off);
            float t = Mathf.Repeat(now - Owner.SpawnTime + offset, cycle);
            return t < pour ? 0f : cycle - t;
        }

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
            Owner.Animation.SetBool("Pouring", pouring);
            if (c != null && art.IsSet)
            {
                // Painted: the curtain narrows as it thins and fades between pours, then gurgles back.
                var size = Owner.Def.body.size;
                Vector3 scale = Owner.transform.lossyScale;
                float top = Owner.Position.y + size.y * 0.5f * Mathf.Abs(scale.y), bottom = Owner.Position.y - size.y * 0.5f * Mathf.Abs(scale.y);
                float width = (pouring ? Mathf.Lerp(0.35f, 1f, thin) : 0.2f + gurgle * 0.2f) * size.x * Mathf.Abs(scale.x) * artWidth;
                float alpha = pouring ? Mathf.Lerp(0.6f, 1f, thin) : gurgle * 0.6f;
                bool drawn = FloorArt.Strip(Owner.transform, "FallArt", art, FloorArt.Frames(art, Owner.World),
                    new Vector2(Owner.Position.x, top), new Vector2(Owner.Position.x, bottom), width, now, Owner.Def.visual.layer, Owner.Def.visual.SortingOrder, true, alpha);
                var box = FloorKit.Look(Owner);
                if (drawn && box != null)
                {
                    box.enabled = false;
                    return;
                }
            }
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

    [Serializable, JsonType("geyser")]
    [Doc("A lava geyser: a basalt vent (the entity's own sprite, standing on the spot) that blows a column of lava straight up on a beat. " +
         "It rests for 'off' seconds, its crack glows and spits for 'tell' seconds (at least 1: animator bool \"Telling\", the 'tellSound'), " +
         "then the column blows for 'blow' seconds (animator bool \"Blowing\", the 'blowSound', a shake). Only the column hurts: give the " +
         "body a box over it (body.offset up by half its height) and a scald. Hell's loop: the beat never changes, so a rewind shows the order.")]
    public sealed class GeyserAbility : Ability
    {
        [Doc("Seconds it rests between blows (the tell is the last part of it).")] public float off = 3f;
        [Doc("Seconds the vent glows and spits before it blows (at least 1).")] public float tell = 1f;
        [Doc("Seconds the column blows.")] public float blow = 1.2f;
        [Doc("Where in its beat it starts (seconds; 0 = the start of a rest).")] public float offset;
        [Doc("Height of the column above the vent (units): match the body box.")] public float height = 3f;
        [Doc("Width of the drawn column (units).")] public float width = 0.8f;
        public Color color = new Color(1f, 0.55f, 0.15f, 0.95f);
        [Doc("Painted column laid up from the vent (instead of the drawn box).")] public ArtLoopDef columnArt = new ArtLoopDef();
        [SoundRef, Doc("Played as the vent starts to glow (a gurgle).")] public string tellSound = "";
        [SoundRef, Doc("Played as it blows.")] public string blowSound = "";
        [Doc("Camera shake as it blows, when the vent is on screen (0 = none).")] public float shake = 0.12f;
        [Doc("Beat on the level's clock instead of from its own spawn (Hell's loop: two vents with the same offset blow together, wherever they stand).")]
        public bool worldClock;

        int lastPhase = -1;

        public override void OnSpawn() => lastPhase = -1;

        /// <summary>0 resting, 1 telling, 2 blowing (the playtest reads it).</summary>
        public int Phase => Mathf.Max(0, lastPhase);

        public override void Tick(float now, float dt)
        {
            float t1 = Mathf.Max(1f, tell), rest = Mathf.Max(t1, off), up = Mathf.Max(0.3f, blow);
            float cycle = rest + up;
            float t = Mathf.Repeat(now - (worldClock ? 0f : Owner.SpawnTime) + offset, cycle);
            int phase = t < rest - t1 ? 0 : t < rest ? 1 : 2;
            if (phase != lastPhase)
            {
                if (lastPhase >= 0)
                {
                    if (phase == 1)
                    {
                        SickOfIt.Core.Audio.Sfx.Play(tellSound);
                        FloorKit.Record(Owner, "geyserTell");
                    }
                    else if (phase == 2)
                    {
                        SickOfIt.Core.Audio.Sfx.Play(blowSound);
                        FloorKit.Record(Owner, "geyser");
                        if (shake > 0f && Owner.World.View.Contains(Owner.Position))
                            Owner.World.Effects.Shake(shake, 0.4f);
                    }
                }
                lastPhase = phase;
                Owner.RecordNow();
            }
            var c = Owner.MainCollider;
            if (c != null)
                c.enabled = phase == 2;
            Owner.Animation.SetBool("Telling", phase == 1);
            Owner.Animation.SetBool("Blowing", phase == 2);

            // The column (cosmetic): rises fast as it blows, sinks back in its last 0.3 s; a stub of glow while it tells.
            float rise = phase == 2 ? Mathf.Clamp01((t - rest) / 0.15f) * Mathf.Clamp01((cycle - t) / 0.3f) : phase == 1 ? 0.12f : 0f;
            Vector2 foot = Owner.Position;
            Vector2 top = foot + Vector2.up * Mathf.Max(0.01f, height * rise);
            float alpha = phase == 2 ? 1f : phase == 1 ? 0.5f + 0.4f * Mathf.Sin((t - (rest - t1)) * 25f) : 0f;
            bool drawn = rise > 0f && FloorArt.Strip(Owner.transform, "ColumnArt", columnArt, FloorArt.Frames(columnArt, Owner.World),
                top, foot, width, now, Owner.Def.visual.layer, Owner.Def.visual.SortingOrder + 1, true, alpha);
            var art = Owner.transform.Find("ColumnArt");
            if (art != null && rise <= 0f)
                art.gameObject.SetActive(false);
            else if (art != null)
                art.gameObject.SetActive(true);
            var box = FloorKit.Child(Owner, "Column", Owner.Def.visual.layer, Owner.Def.visual.SortingOrder + 1);
            box.enabled = !drawn && rise > 0f;
            if (box.enabled)
            {
                box.color = new Color(color.r, color.g, color.b, color.a * alpha);
                FloorKit.PlaceWorld(Owner, box, (foot + top) * 0.5f, new Vector2(width * 0.7f, (top - foot).y));
            }
        }

        public override void SaveState(StateWriter writer) => writer.Int(lastPhase);
        public override void LoadState(StateReader reader) => lastPhase = reader.Int();
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
        [Doc("Where it stands above the platform's top (negative = down on the platform's face).")] public float above = 0.15f;
        [Doc("Carved into the platform's painted face: drawn in front of it (its own layer) instead of standing behind it.")] public bool onFace;

        public override void OnSpawn()
        {
            int floor = Owner.World.Stage != null ? Owner.World.Stage.Floor : 0;
            Write("Carving", text.Replace("{floor}", floor > 0 ? floor.ToString() : ""), size);
            Write("Words", words ?? "", size * 0.38f);
        }

        // Laid out every tick: the platform may have been stretched after it spawned, and it may sink or ride.
        public override void Tick(float now, float dt)
        {
            // The counter can move between the spawn (a section ahead) and his arrival.
            int floor = Owner.World.Stage != null ? Owner.World.Stage.Floor : 0;
            string carving = text.Replace("{floor}", floor > 0 ? floor.ToString() : "");
            if (carving != Carving)
                Write("Carving", carving, size);
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
                mesh.sortingLayerID = SortingLayer.NameToID((onFace ? Owner.Def.visual.layer : SpriteLayer.Background2).ToString());
                mesh.sortingOrder = onFace ? Owner.Def.visual.SortingOrder + 1 : 903;
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
