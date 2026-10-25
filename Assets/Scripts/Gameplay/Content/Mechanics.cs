using System;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // Four abilities that are built from geometry: a front shield, a blink, a rotating laser. (Bouncing shots live on
    // 'projectile', predictive aim is Aim.Predictive.) All of their state is saved for rewind; what they draw is
    // read back from that state every frame, so a rewind redraws it too.
    // =====================================================================

    /// <summary>Geometry the mechanics share: what counts as solid, and a plain white sprite for the things they draw.</summary>
    public static class MechanicGeometry
    {
        static readonly RaycastHit2D[] RayHits = new RaycastHit2D[16];
        static readonly Collider2D[] OverlapHits = new Collider2D[16];
        static Sprite whiteSprite;

        /// <summary>Ground, walls and platforms: not a trigger, not an entity, not the screen boundary.</summary>
        public static bool IsSolid(GameplayWorld world, Collider2D collider)
        {
            return collider != null && !collider.isTrigger && !world.TryGetEntity(collider, out _) && !world.IsBoundary(collider);
        }

        /// <summary>The distance to the first solid along a ray, or 'distance' when nothing is in the way.</summary>
        public static float RaycastSolid(GameplayWorld world, Vector2 origin, Vector2 direction, float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.Raycast(origin, direction, filter, RayHits, distance);
            for (int i = 0; i < count; i++)
            {
                if (IsSolid(world, RayHits[i].collider))
                    return RayHits[i].distance;
            }
            return distance;
        }

        /// <summary>True if a circle at this point overlaps ground, a wall or a platform.</summary>
        public static bool OverlapsSolid(GameplayWorld world, Vector2 point, float radius)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapCircle(point, radius, filter, OverlapHits);
            for (int i = 0; i < count; i++)
            {
                if (IsSolid(world, OverlapHits[i]))
                    return true;
            }
            return false;
        }

        /// <summary>Does the segment a-b touch the box? (Slab test.)</summary>
        public static bool SegmentHitsBounds(Vector2 a, Vector2 b, Bounds box)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float origin = axis == 0 ? a.x : a.y;
                float delta = axis == 0 ? d.x : d.y;
                float min = axis == 0 ? box.min.x : box.min.y;
                float max = axis == 0 ? box.max.x : box.max.y;
                if (Mathf.Abs(delta) < 0.00001f)
                {
                    if (origin < min || origin > max)
                        return false;
                    continue;
                }
                float ta = (min - origin) / delta;
                float tb = (max - origin) / delta;
                if (ta > tb)
                {
                    float swap = ta;
                    ta = tb;
                    tb = swap;
                }
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1)
                    return false;
            }
            return true;
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    // 4 pixels per unit and 4 pixels wide: exactly 1 x 1 world unit, centred.
                    whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                }
                return whiteSprite;
            }
        }
    }

    /// <summary>A child object that draws one ability's state each frame (LateUpdate, so it follows a rewind too).</summary>
    public sealed class MechanicView : MonoBehaviour
    {
        public Action Draw;
        public SpriteRenderer Renderer;

        void LateUpdate() => Draw?.Invoke();

        /// <summary>A white bar on a child of the entity, drawn on its sorting layer just above it.</summary>
        public static MechanicView Create(Entity owner, string name, int orderAbove)
        {
            Transform parent = owner.transform;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MechanicView>();
            view.Renderer = go.AddComponent<SpriteRenderer>();
            view.Renderer.sprite = MechanicGeometry.WhiteSprite;
            if (owner.Sprite != null)
            {
                view.Renderer.sortingLayerID = owner.Sprite.sortingLayerID;
                view.Renderer.sortingOrder = owner.Sprite.sortingOrder + orderAbove;
            }
            else
                view.Renderer.sortingOrder = orderAbove;
            view.Renderer.enabled = false;
            return view;
        }

        /// <summary>Places the bar in world space: from 'from' along 'angle' degrees for 'length', 'thickness' wide.</summary>
        public void PlaceBar(Vector2 start, float angle, float length, float thickness, Color color)
        {
            var t = transform;
            Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            float sx = Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f;
            float sy = Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 mid = start + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (length * 0.5f);
            t.position = new Vector3(mid.x, mid.y, t.parent != null ? t.parent.position.z : 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(length * Mathf.Abs(sx), thickness * Mathf.Abs(sy), 1f);
            Renderer.color = color;
            Renderer.enabled = true;
        }

        public void Hide()
        {
            if (Renderer != null)
                Renderer.enabled = false;
        }
    }

    // =====================================================================
    // FRONT SHIELD
    // =====================================================================

    [Serializable, JsonType("frontShield")]
    [Doc("A shield on the side it faces: damage that comes from the front is blocked (the shot is spent, a spark and the 'blockSound' say so); damage from behind counts. Which way is front is the entity's facing, so make it face with 'faceTarget', 'patrol' or 'move'. 'arc' is how wide the shield is.")]
    public sealed class FrontShieldAbility : Ability
    {
        [Doc("Half the shield's width in degrees, around the way the entity faces. 90 = the whole front half; 45 = a narrow shield that a shot from above or below gets past.")] public float arc = 90f;
        [Doc("Share of the damage that gets through a block, 0 to 1. 0 = a clean block.")] public float leak;
        [SoundRef, Doc("Sound id (audio.json) played on a block; empty = none.")] public string blockSound = "";
        [Doc("Draw the shield as a bar in front of the body.")] public bool show = true;
        public Color color = new Color(0.55f, 0.85f, 1f, 0.85f);
        [Doc("Height of the drawn bar in world units; 0 = twice the body's radius.")] public float height;

        float blockedAt = -10f;
        int blocks;
        MechanicView view;

        public int Blocks => blocks;
        public float LastBlockAt => blockedAt;

        public override void OnSpawn()
        {
            blockedAt = -10f;
            blocks = 0;
        }

        /// <summary>Is a hit from this point on the shield's side?</summary>
        public bool CoversPoint(Vector2 start) => Covers(Owner.Position, Owner.Facing, start, arc);

        /// <summary>A shield at 'position' facing left (-1) or right (1), 'arc' degrees either side: is a hit from 'from' on its side?</summary>
        public static bool Covers(Vector2 position, float facing, Vector2 start, float arc)
        {
            Vector2 to = start - position;
            if (to.sqrMagnitude < 0.0001f)
                return true;
            return Vector2.Angle(new Vector2(facing < 0f ? -1f : 1f, 0f), to) < arc;
        }

        /// <summary>
        /// Called by Entity.Damage with where the hit came from. Returns true if the shield took the whole hit;
        /// 'amount' is replaced by what leaks through when it did not.
        /// </summary>
        public bool TryBlock(Vector2 start, ref int amount)
        {
            if (!CoversPoint(start))
                return false;
            blockedAt = Owner.Now;
            blocks++;
            Owner.RecordNow();
            SickOfIt.Core.Fx.Fx.Spark(Owner.Position + new Vector2(Owner.Facing * Owner.Radius, 0f), color, 0.35f);
            if (!string.IsNullOrEmpty(blockSound))
                SickOfIt.Core.Audio.Sfx.Play(blockSound);
            Owner.World.Telemetry?.Record("shieldBlock", Owner.DefId, amount, $"health {Owner.Health}");
            int leaked = Mathf.RoundToInt(amount * Mathf.Clamp01(leak));
            if (leaked <= 0)
                return true;
            amount = leaked;
            return false;
        }

        public override void Tick(float now, float dt)
        {
            if (!show)
                return;
            if (view == null)
            {
                view = MechanicView.Create(Owner, "FrontShield", 1);
                view.Draw = Draw;
            }
        }

        void Draw()
        {
            if (view == null)
                return;
            if (Owner == null || !Owner.IsAlive || !show)
            {
                view.Hide();
                return;
            }
            float size = height > 0f ? height : Owner.Radius * 2f;
            float flash = Mathf.Clamp01(1f - (Owner.Now - blockedAt) / 0.2f);
            var c = Color.Lerp(color, Color.white, flash);
            Vector2 foot = Owner.Position + new Vector2(Owner.Facing * (Owner.Radius + 0.12f), -size * 0.5f);
            view.PlaceBar(foot, 90f, size, 0.1f + 0.08f * flash, c);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(blockedAt);
            writer.Int(blocks);
        }

        public override void LoadState(StateReader reader)
        {
            blockedAt = reader.Float();
            blocks = reader.Int();
        }
    }

    // =====================================================================
    // TELEPORT
    // =====================================================================

    [Serializable, JsonType("teleport")]
    [Doc("Blinks to another spot. Every 'interval' seconds it picks the spot, shows it for 'telegraph' seconds (the body flashes and the optional 'marker' entity appears there), then vanishes from where it was and appears at the spot. 'to' says where: beside the player, a fixed point, or anywhere in view.")]
    public sealed class TeleportAbility : Ability
    {
        [Doc("Where the blink goes.")] public TeleportTarget to = TeleportTarget.NearPlayer;
        [Doc("NearPlayer: how far to the side of the player it lands. Random: ignored.")] public float distance = 3f;
        [Doc("NearPlayer: land behind the player (the side he is not facing) instead of a random side.")] public bool behind;
        [Doc("Point: the world position to blink to.")] public Vector2 point;
        public float interval = 4f;
        [Doc("Seconds before the first blink, from spawning or from entering the state that has this ability.")] public float initialDelay = 2f;
        [Doc("Seconds the spot is shown before the blink. The entity stands still meanwhile.")] public float telegraph = 0.8f;
        [Doc("Only blink when the player is closer than this (0 = any distance).")] public float range;
        [EntityRef, Doc("Entity id spawned on the spot during the telegraph (a glowing mark with a 'lifetime'); empty = none.")] public string marker = "";
        [Doc("Colour the body flashes during the telegraph.")] public Color flashColor = new Color(0.8f, 0.5f, 1f, 1f);
        [SoundRef, Doc("Sound id (audio.json) played where it vanishes and where it appears; empty = none.")] public string sound = "";
        [Doc("Keep this far inside the edge of the view when it lands.")] public float edgeMargin = 1f;

        int phase;
        float nextAt;
        float blinkAt;
        Vector2 destination;
        int blinks;

        public int Blinks => blinks;
        public bool Telegraphing => phase == 1;
        public Vector2 Destination => destination;

        public override void OnSpawn()
        {
            nextAt = Owner.SpawnTime + initialDelay;
            phase = 0;
            blinkAt = 0f;
            destination = Vector2.zero;
            blinks = 0;
        }

        public override void OnActivate()
        {
            nextAt = Owner.Now + initialDelay;
            phase = 0;
        }

        public override void Tick(float now, float dt)
        {
            if (phase == 0)
            {
                if (now < nextAt)
                    return;
                bool inRange = range <= 0f || DistanceToPlayer() <= range;
                if (!inRange)
                    return;
                if (!ChooseDestination())
                {
                    nextAt = now + 1f;
                    return;
                }
                phase = 1;
                blinkAt = now + telegraph;
                Owner.HoldPosition(blinkAt);
                if (!string.IsNullOrEmpty(marker))
                    Owner.World.Spawn(marker, destination, new SpawnOptions { FacePlayer = false });
                Owner.World.Telemetry?.Record("teleport", Owner.DefId, 0, $"telegraph {destination.x:0.0},{destination.y:0.0}");
                Owner.RecordNow();
            }
            if (phase == 1)
            {
                Owner.FlashFor(0.1f, flashColor);
                if (now < blinkAt)
                    return;
                Vector2 start = Owner.Position;
                SickOfIt.Core.Fx.Fx.Burst(start, flashColor, 8, 3f, 0.1f);
                Owner.TeleportTo(destination);
                SickOfIt.Core.Fx.Fx.Burst(destination, flashColor, 8, 3f, 0.1f);
                var player = Player;
                if (player != null && player.IsAlive)
                    Owner.FaceTowards(player.Position.x);
                if (!string.IsNullOrEmpty(sound))
                    SickOfIt.Core.Audio.Sfx.Play(sound);
                blinks++;
                phase = 0;
                nextAt = now + interval / Mathf.Max(0.01f, Owner.Stat(Stat.AttackSpeed, 1f));
                Owner.World.Telemetry?.Record("teleport", Owner.DefId, blinks, $"blink {start.x:0.0},{start.y:0.0} -> {destination.x:0.0},{destination.y:0.0}");
                Owner.RecordNow();
            }
        }

        bool ChooseDestination()
        {
            float radius = Mathf.Max(0.1f, Owner.Radius);
            Rect view = Owner.World.View;
            float minX = view.xMin + edgeMargin, maxX = view.xMax - edgeMargin;
            if (maxX < minX)
                minX = maxX = view.center.x;
            switch (to)
            {
                case TeleportTarget.Point:
                    destination = point;
                    return true;
                case TeleportTarget.Random:
                {
                    for (int tries = 0; tries < 8; tries++)
                    {
                        var candidate = new Vector2(Owner.Random.Range(minX, maxX),
                            Owner.Random.Range(view.yMin + edgeMargin, Mathf.Max(view.yMin + edgeMargin, view.yMax - edgeMargin)));
                        if (!MechanicGeometry.OverlapsSolid(Owner.World, candidate, radius * 0.9f))
                        {
                            destination = candidate;
                            return true;
                        }
                    }
                    return false;
                }
                default:
                {
                    var player = Player;
                    if (player == null || !player.IsAlive)
                        return false;
                    float side = Owner.Random.Value() < 0.5f ? -1f : 1f;
                    if (behind)
                        side = -player.Facing;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        float x = Mathf.Clamp(player.Position.x + side * distance, minX, maxX);
                        Vector2 candidate = new Vector2(x, player.Position.y);
                        // Stand on whatever is under that spot; hang at the hero's height if there is nothing.
                        float drop = MechanicGeometry.RaycastSolid(Owner.World, new Vector2(x, player.Position.y + 1.5f), Vector2.down, 6f);
                        if (drop < 6f)
                            candidate.y = player.Position.y + 1.5f - drop + radius;
                        if (!MechanicGeometry.OverlapsSolid(Owner.World, candidate, radius * 0.9f))
                        {
                            destination = candidate;
                            return true;
                        }
                        side = -side;
                    }
                    return false;
                }
            }
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(phase);
            writer.Float(nextAt);
            writer.Float(blinkAt);
            writer.Float(destination.x);
            writer.Float(destination.y);
            writer.Int(blinks);
        }

        public override void LoadState(StateReader reader)
        {
            phase = reader.Int();
            nextAt = reader.Float();
            blinkAt = reader.Float();
            destination = new Vector2(reader.Float(), reader.Float());
            blinks = reader.Int();
        }
    }

    [Doc("Where a teleport lands.")]
    public enum TeleportTarget
    {
        [Doc("Beside the player, 'distance' away (behind him with 'behind').")] NearPlayer,
        [Doc("The 'point' field.")] Point,
        [Doc("Anywhere in view that is clear of ground.")] Random,
    }

    // =====================================================================
    // ROTATING LASER
    // =====================================================================

    [Serializable, JsonType("laserSweep")]
    [Doc("A laser that sweeps. Every 'interval' seconds it shows a thin warning line where the beam will start, for 'warning' seconds, then fires a beam that turns 'sweepDegrees' over 'sweepTime' seconds. The beam is a real hitbox from the muzzle to its end (ground stops it) and hurts the player on contact. By default the sweep is centred on where the player stood when the warning began, so moving is the answer.")]
    public sealed class LaserSweepAbility : Ability
    {
        public int damage = 1;
        [Doc("Where the beam starts, relative to the entity in world units; x is mirrored when facing left.")] public Vector2 muzzle;
        public float interval = 6f;
        [Doc("Seconds before the first laser, from spawning or from entering the state that has this ability.")] public float initialDelay = 2f;
        [Doc("Seconds the thin warning line is shown before the beam fires.")] public float warning = 1.2f;
        [Doc("Seconds the beam takes to sweep.")] public float sweepTime = 1.6f;
        [Doc("Degrees the beam turns. Negative = the other way round (counter-clockwise is positive).")] public float sweepDegrees = 90f;
        [Doc("The warning line (and the beam's first position) aim at the player and the sweep is centred on that. False: the sweep starts at 'startAngle'.")] public bool aimAtPlayer = true;
        [Doc("Degrees, 0 = right, 90 = up. Used when 'aimAtPlayer' is off.")] public float startAngle;
        public float length = 14f;
        [Doc("Beam width in world units.")] public float width = 0.5f;
        [Doc("Seconds between two hits on the player in one sweep.")] public float hitInterval = 0.5f;
        [Doc("Only start a laser when the player is closer than this (0 = any distance).")] public float range;
        [Doc("Stand still while the laser warns and sweeps.")] public bool holdPosition = true;
        public Color warningColor = new Color(1f, 0.35f, 0.25f, 0.55f);
        public Color beamColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        [SoundRef, Doc("Sound id (audio.json) played when the warning starts; empty = none.")] public string warningSound = "";
        [SoundRef, Doc("Sound id (audio.json) played when the beam fires; empty = none.")] public string beamSound = "";
        [Doc("Animator bool set while the laser warns and sweeps (the entity's Attack clip).")] public string animBool = "";

        const int Idle = 0, Warning = 1, Sweeping = 2;
        int phase;
        float nextAt;
        float phaseStartedAt;
        float baseAngle;
        float lastHitAt = -10f;
        int sweeps;
        int hits;
        MechanicView view;

        public bool IsWarning => phase == Warning;
        public bool IsSweeping => phase == Sweeping;
        public int Sweeps => sweeps;
        public int Hits => hits;
        /// <summary>The beam's angle now in degrees (the warning line's while warning).</summary>
        public float Angle(float now)
        {
            if (phase != Sweeping || sweepTime <= 0.0001f)
                return baseAngle;
            return baseAngle + sweepDegrees * Mathf.Clamp01((now - phaseStartedAt) / sweepTime);
        }

        public override void OnSpawn()
        {
            nextAt = Owner.SpawnTime + initialDelay;
            phase = Idle;
            phaseStartedAt = 0f;
            baseAngle = 0f;
            lastHitAt = -10f;
            sweeps = 0;
            hits = 0;
        }

        public override void OnActivate()
        {
            nextAt = Owner.Now + initialDelay;
            phase = Idle;
        }

        public override void OnDeactivate() => phase = Idle;

        Vector2 Origin => Owner.Position + new Vector2(muzzle.x * Owner.Facing, muzzle.y);

        public override void Tick(float now, float dt)
        {
            if (view == null)
            {
                view = MechanicView.Create(Owner, "LaserSweep", 2);
                view.Draw = Draw;
            }
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, phase != Idle);

            if (phase == Idle)
            {
                if (now < nextAt || (range > 0f && DistanceToPlayer() > range))
                    return;
                Vector2 origin = Origin;
                if (aimAtPlayer && Player != null && Player.IsAlive)
                {
                    Vector2 to = Owner.World.PlayerAimPoint - origin;
                    float aim = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                    baseAngle = aim - sweepDegrees * 0.5f;
                }
                else
                {
                    baseAngle = startAngle;
                }
                phase = Warning;
                phaseStartedAt = now;
                if (holdPosition)
                    Owner.HoldPosition(now + warning + sweepTime);
                if (!string.IsNullOrEmpty(warningSound))
                    SickOfIt.Core.Audio.Sfx.Play(warningSound);
                Owner.World.Telemetry?.Record("laser", Owner.DefId, sweeps, $"warning {baseAngle:0}");
                Owner.RecordNow();
                return;
            }

            if (phase == Warning && now >= phaseStartedAt + warning)
            {
                phase = Sweeping;
                phaseStartedAt = now;
                lastHitAt = -10f;
                if (!string.IsNullOrEmpty(beamSound))
                    SickOfIt.Core.Audio.Sfx.Play(beamSound);
                Owner.World.Telemetry?.Record("laser", Owner.DefId, sweeps, $"sweep {baseAngle:0} to {baseAngle + sweepDegrees:0}");
                Owner.RecordNow();
            }

            if (phase == Sweeping)
            {
                BeamEnds(now, out Vector2 start, out Vector2 end);
                var player = Player;
                if (player != null && player.IsAlive && now - lastHitAt >= hitInterval && Touches(player, start, end))
                {
                    if (player.Damage(Mathf.RoundToInt(damage * Owner.Stat(Stat.Damage, 1f)), Owner))
                    {
                        lastHitAt = now;
                        hits++;
                        Owner.RecordNow();
                    }
                }
                if (now >= phaseStartedAt + sweepTime)
                {
                    phase = Idle;
                    sweeps++;
                    nextAt = now + interval / Mathf.Max(0.01f, Owner.Stat(Stat.AttackSpeed, 1f));
                    Owner.World.Telemetry?.Record("laser", Owner.DefId, sweeps, "end");
                    Owner.RecordNow();
                }
            }
        }

        /// <summary>The beam now, while it is sweeping (the playtest checks it against the hero).</summary>
        public bool TryGetBeam(out Vector2 start, out Vector2 end)
        {
            if (phase != Sweeping)
            {
                start = end = Vector2.zero;
                return false;
            }
            BeamEnds(Owner.Now, out start, out end);
            return true;
        }

        /// <summary>The box a hit on this body is tested with: the player's collider grown by the beam's width.</summary>
        Bounds HitBox(Entity player)
        {
            var collider = player.MainCollider;
            Bounds box = collider != null ? collider.bounds : new Bounds(player.Position, new Vector3(0.6f, 1.2f, 1f));
            box.Expand(new Vector3(width, width, 0f));
            return box;
        }

        public bool Crosses(Entity player)
        {
            return TryGetBeam(out var start, out var end) && MechanicGeometry.SegmentHitsBounds(start, end, HitBox(player));
        }

        /// <summary>The beam as a segment: from the muzzle along the current angle, cut short by the first solid.</summary>
        void BeamEnds(float now, out Vector2 start, out Vector2 end)
        {
            start = Origin;
            float rad = Angle(now) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float reach = MechanicGeometry.RaycastSolid(Owner.World, start, dir, length);
            end = start + dir * reach;
        }

        bool Touches(Entity player, Vector2 start, Vector2 end) => MechanicGeometry.SegmentHitsBounds(start, end, HitBox(player));

        void Draw()
        {
            if (view == null)
                return;
            if (Owner == null || !Owner.IsAlive || phase == Idle)
            {
                view.Hide();
                return;
            }
            float now = Owner.Now;
            BeamEnds(now, out Vector2 start, out Vector2 end);
            float reach = Vector2.Distance(start, end);
            float angle = Angle(now);
            if (phase == Warning)
            {
                float t = (now - phaseStartedAt) / Mathf.Max(0.01f, warning);
                var c = warningColor;
                c.a *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
                view.PlaceBar(start, angle, reach, Mathf.Max(0.04f, width * 0.15f), c);
            }
            else
            {
                view.PlaceBar(start, angle, reach, width, beamColor);
            }
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(phase);
            writer.Float(nextAt);
            writer.Float(phaseStartedAt);
            writer.Float(baseAngle);
            writer.Float(lastHitAt);
            writer.Int(sweeps);
            writer.Int(hits);
        }

        public override void LoadState(StateReader reader)
        {
            phase = reader.Int();
            nextAt = reader.Float();
            phaseStartedAt = reader.Float();
            baseAngle = reader.Float();
            lastHitAt = reader.Float();
            sweeps = reader.Int();
            hits = reader.Int();
        }
    }
}
