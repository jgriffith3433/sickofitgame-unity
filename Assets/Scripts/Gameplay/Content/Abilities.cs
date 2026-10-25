using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>
    /// A piece of entity behavior. The fields are configuration from JSON; each spawned entity gets its own copy,
    /// so private fields hold that entity's runtime state. Every private field that changes during play must be
    /// written in SaveState and read in LoadState, or a rewind won't restore it (a test checks this).
    /// </summary>
    [Serializable, JsonPolymorphic]
    [Doc("A piece of entity behavior: movement, attacks, reactions. Combine several on one entity.")]
    public abstract class Ability
    {
        [NonSerialized] protected Entity Owner;

        public Ability Instantiate(Entity owner)
        {
            var copy = (Ability)MemberwiseClone();
            copy.Owner = owner;
            copy.OnCreate();
            return copy;
        }

        /// <summary>Called once per copy. Make fresh copies of any config objects that keep state (triggers).</summary>
        protected virtual void OnCreate() { }

        /// <summary>The entity was spawned (or reused from a pool). Reset runtime state here.</summary>
        public virtual void OnSpawn() { }
        /// <summary>The state this ability belongs to became active (also called at spawn for always-on abilities).</summary>
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }
        public virtual void Tick(float now, float dt) { }
        public virtual void OnAnimationEvent(string eventName) { }
        public virtual void OnContact(Entity other, bool entering) { }
        public virtual void OnSolidContact(Collider2D collider) { }

        public virtual void SaveState(StateWriter writer) { }
        public virtual void LoadState(StateReader reader) { }

        protected Entity Player => Owner.World.Player;

        protected float DistanceToPlayer()
        {
            var player = Player;
            return player == null || !player.IsAlive ? float.PositiveInfinity : Vector2.Distance(Owner.Position, player.Position);
        }
    }

    // =====================================================================
    // MOVEMENT
    // =====================================================================

    [Serializable, JsonType("chase")]
    [Doc("Moves toward the player, stopping at 'stopDistance'.")]
    public sealed class ChaseAbility : Ability
    {
        [Doc("Speed; 0 = the entity's moveSpeed.")] public float speed;
        public float stopDistance = 0.5f;
        [Doc("Only move left and right.")] public bool horizontalOnly;
        [Doc("Only chase when the player is closer than this (0 = always).")] public float range;
        [Doc("Turn to face the way it moves.")] public bool face = true;
        [Doc("Animator bool set true while moving.")] public string animBool = "";

        public override void Tick(float now, float dt)
        {
            bool moving = false;
            var player = Player;
            if (player != null && player.IsAlive)
            {
                Vector2 to = player.Position - Owner.Position;
                if (horizontalOnly)
                    to.y = 0f;
                float distance = to.magnitude;
                if (!Owner.IsHeld && distance > stopDistance && (range <= 0f || distance <= range))
                {
                    Owner.Move(to / distance * (speed > 0f ? Owner.Stat(Stat.MoveSpeed, speed) : Owner.MoveSpeed));
                    if (face)
                        Owner.FaceTowards(player.Position.x);
                    moving = true;
                }
            }
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, moving);
        }
    }

    [Serializable, JsonType("patrol")]
    [Doc("Walks back and forth 'distance' either side of where it spawned, pausing at each end.")]
    public sealed class PatrolAbility : Ability
    {
        public float speed;
        public float distance = 2f;
        public float pause = 0.5f;
        public string animBool = "";

        float originX;
        float direction = 1f;
        float pausedUntil;

        public override void OnSpawn()
        {
            originX = Owner.Position.x;
            direction = Owner.Facing;
            pausedUntil = 0f;
        }

        public override void Tick(float now, float dt)
        {
            bool moving = now >= pausedUntil && !Owner.IsHeld;
            if (moving)
            {
                float offset = Owner.Position.x - originX;
                if ((direction > 0f && offset >= distance) || (direction < 0f && offset <= -distance))
                {
                    direction = -direction;
                    pausedUntil = now + pause;
                    moving = false;
                }
                else
                {
                    Owner.Move(new Vector2(direction * (speed > 0f ? Owner.Stat(Stat.MoveSpeed, speed) : Owner.MoveSpeed), 0f));
                    Owner.SetFacing(direction);
                }
            }
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, moving);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(originX);
            writer.Float(direction);
            writer.Float(pausedUntil);
        }

        public override void LoadState(StateReader reader)
        {
            originX = reader.Float();
            direction = reader.Float();
            pausedUntil = reader.Float();
        }
    }

    [Serializable, JsonType("hover")]
    [Doc("Flies toward a point 'offset' from the player (or from where it spawned), bobbing up and down. Good for flying enemies and bosses.")]
    public sealed class HoverAbility : Ability
    {
        public float speed;
        public Vector2 offset = new Vector2(0f, 1.5f);
        [Doc("Hover around the spawn point instead of the player.")] public bool aroundSpawn;
        [Doc("Mirror offset.x to stay on the side of the player it is already on.")] public bool keepSide = true;
        public float bobHeight = 0.15f;
        public float bobSpeed = 2f;

        Vector2 spawnPoint;

        public override void OnSpawn()
        {
            spawnPoint = Owner.Position;
        }

        public override void Tick(float now, float dt)
        {
            Vector2 anchor;
            var player = Player;
            Vector2 o = offset;
            if (aroundSpawn || player == null)
            {
                anchor = spawnPoint;
            }
            else
            {
                anchor = player.Position;
                if (keepSide && Owner.Position.x < player.Position.x)
                    o.x = -Mathf.Abs(o.x);
                else if (keepSide)
                    o.x = Mathf.Abs(o.x);
                Owner.FaceTowards(player.Position.x);
            }
            Vector2 target = anchor + o + Vector2.up * Mathf.Sin((now - Owner.SpawnTime) * bobSpeed) * bobHeight;
            Vector2 to = target - Owner.Position;
            float maxSpeed = speed > 0f ? Owner.Stat(Stat.MoveSpeed, speed) : Owner.MoveSpeed;
            float distance = to.magnitude;
            if (distance > 0.001f && dt > 0f)
                Owner.Move(to / distance * Mathf.Min(maxSpeed, distance / dt));
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(spawnPoint.x);
            writer.Float(spawnPoint.y);
        }

        public override void LoadState(StateReader reader)
        {
            spawnPoint = new Vector2(reader.Float(), reader.Float());
        }
    }

    [Serializable, JsonType("wander")]
    [Doc("Picks random points within 'radius' of where it spawned and walks to them, pausing between.")]
    public sealed class WanderAbility : Ability
    {
        public float speed;
        public float radius = 2f;
        public bool horizontalOnly = true;
        public float pauseMin = 0.5f;
        public float pauseMax = 1.5f;
        public string animBool = "";

        Vector2 home;
        Vector2 target;
        float pausedUntil;

        public override void OnSpawn()
        {
            home = Owner.Position;
            target = home;
            pausedUntil = 0f;
        }

        public override void Tick(float now, float dt)
        {
            bool moving = false;
            if (now >= pausedUntil && !Owner.IsHeld)
            {
                Vector2 to = target - Owner.Position;
                if (horizontalOnly)
                    to.y = 0f;
                if (to.magnitude < 0.05f)
                {
                    pausedUntil = now + Owner.Random.Range(pauseMin, pauseMax);
                    target = home + new Vector2(Owner.Random.Range(-radius, radius), horizontalOnly ? 0f : Owner.Random.Range(-radius, radius));
                }
                else
                {
                    Owner.Move(to.normalized * (speed > 0f ? Owner.Stat(Stat.MoveSpeed, speed) : Owner.MoveSpeed));
                    Owner.FaceTowards(target.x);
                    moving = true;
                }
            }
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, moving);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(home.x); writer.Float(home.y);
            writer.Float(target.x); writer.Float(target.y);
            writer.Float(pausedUntil);
        }

        public override void LoadState(StateReader reader)
        {
            home = new Vector2(reader.Float(), reader.Float());
            target = new Vector2(reader.Float(), reader.Float());
            pausedUntil = reader.Float();
        }
    }

    [Serializable, JsonType("move")]
    [Doc("Keeps moving in a direction (or the direction it was launched in), optionally curving toward the player.")]
    public sealed class MoveAbility : Ability
    {
        [Doc("Speed; 0 = keep the launch speed.")] public float speed;
        [Doc("Direction; zero = keep the launch direction.")] public Vector2 direction;
        [Doc("Degrees per second it turns toward the player (homing). 0 = straight.")] public float homing;
        [Doc("Speed added per second.")] public float acceleration;

        Vector2 velocity;

        public override void OnSpawn()
        {
            velocity = Vector2.zero;
        }

        public override void Tick(float now, float dt)
        {
            if (velocity == Vector2.zero)
            {
                Vector2 launch = Owner.Body.linearVelocity;
                Vector2 dir = direction != Vector2.zero ? direction.normalized : launch.sqrMagnitude > 0.0001f ? launch.normalized : new Vector2(Owner.Facing, 0f);
                float s = speed > 0f ? Owner.Stat(Stat.MoveSpeed, speed) : launch.magnitude;
                velocity = dir * s;
            }
            if (homing > 0f && Player != null && Player.IsAlive)
            {
                Vector2 want = (Owner.World.PlayerAimPoint - Owner.Position).normalized;
                float angle = Vector2.SignedAngle(velocity, want);
                float turn = Mathf.Clamp(angle, -homing * dt, homing * dt);
                float r = turn * Mathf.Deg2Rad;
                velocity = new Vector2(velocity.x * Mathf.Cos(r) - velocity.y * Mathf.Sin(r), velocity.x * Mathf.Sin(r) + velocity.y * Mathf.Cos(r));
            }
            if (acceleration != 0f && velocity.sqrMagnitude > 0.0001f)
                velocity += velocity.normalized * acceleration * dt;
            Owner.SetVelocity(velocity);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(velocity.x);
            writer.Float(velocity.y);
        }

        public override void LoadState(StateReader reader)
        {
            velocity = new Vector2(reader.Float(), reader.Float());
        }
    }

    [Serializable, JsonType("charge")]
    [Doc("Every 'interval' seconds (when the player is within 'range'), stops for 'windup' seconds, then dashes at the player's position at 'speed' for 'duration' seconds.")]
    public sealed class ChargeAbility : Ability
    {
        public float interval = 3f;
        public float initialDelay = 1f;
        public float windup = 0.5f;
        public float duration = 0.5f;
        public float speed = 8f;
        public float range = 8f;
        [Doc("Animator bool set while winding up.")] public string windupBool = "";
        [Doc("Animator bool set while dashing.")] public string chargeBool = "";

        float nextAt;
        float windupEndsAt = -1f;
        float chargeEndsAt = -1f;
        Vector2 chargeDirection;

        public override void OnSpawn()
        {
            nextAt = Owner.SpawnTime + initialDelay;
            windupEndsAt = -1f;
            chargeEndsAt = -1f;
        }

        /// <summary>The first charge comes initialDelay after spawning, or after entering the state that has this ability.</summary>
        public override void OnActivate() => nextAt = Owner.Now + initialDelay;

        public override void Tick(float now, float dt)
        {
            if (windupEndsAt < 0f && chargeEndsAt < 0f && now >= nextAt && DistanceToPlayer() <= range)
            {
                windupEndsAt = now + windup;
                Owner.HoldPosition(windupEndsAt);
                chargeDirection = Owner.World.PlayerAimPoint - Owner.Position;
                chargeDirection = chargeDirection.sqrMagnitude > 0.0001f ? chargeDirection.normalized : new Vector2(Owner.Facing, 0f);
                Owner.FaceTowards(Owner.Position.x + chargeDirection.x);
                Owner.RecordNow();
            }
            if (windupEndsAt >= 0f && now >= windupEndsAt)
            {
                windupEndsAt = -1f;
                chargeEndsAt = now + duration;
                Owner.OverrideMove(chargeDirection * Owner.Stat(Stat.MoveSpeed, speed), chargeEndsAt);
                Owner.RecordNow();
            }
            if (chargeEndsAt >= 0f && now >= chargeEndsAt)
            {
                chargeEndsAt = -1f;
                nextAt = now + interval / Mathf.Max(0.01f, Owner.Stat(Stat.AttackSpeed, 1f));
            }
            if (!string.IsNullOrEmpty(windupBool))
                Owner.Animation.SetBool(windupBool, windupEndsAt >= 0f);
            if (!string.IsNullOrEmpty(chargeBool))
                Owner.Animation.SetBool(chargeBool, chargeEndsAt >= 0f);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(nextAt);
            writer.Float(windupEndsAt);
            writer.Float(chargeEndsAt);
            writer.Float(chargeDirection.x);
            writer.Float(chargeDirection.y);
        }

        public override void LoadState(StateReader reader)
        {
            nextAt = reader.Float();
            windupEndsAt = reader.Float();
            chargeEndsAt = reader.Float();
            chargeDirection = new Vector2(reader.Float(), reader.Float());
        }
    }

    [Serializable, JsonType("faceTarget")]
    [Doc("Turns to face the player.")]
    public sealed class FaceTargetAbility : Ability
    {
        public override void Tick(float now, float dt)
        {
            var player = Player;
            if (player != null)
                Owner.FaceTowards(player.Position.x);
        }
    }

    [Serializable, JsonType("spin")]
    [Doc("Rotates continuously (cosmetic).")]
    public sealed class SpinAbility : Ability
    {
        public float degreesPerSecond = 180f;

        public override void Tick(float now, float dt)
        {
            Owner.transform.Rotate(0f, 0f, degreesPerSecond * dt);
        }
    }

    // =====================================================================
    // COMBAT
    // =====================================================================

    [Serializable, JsonType("contactDamage")]
    [Doc("Hurts hostile things it touches.")]
    public sealed class ContactDamageAbility : Ability
    {
        public int damage = 1;
        [Doc("Seconds between hits while touching. 0 = only when contact starts.")] public float interval;
        [EffectRef, Doc("Status effect applied on hit.")] public string effect = "";
        [Doc("Push the target away with this impulse.")] public float knockback;
        [Doc("Burst on touching something hostile: hits it, then dies (running onDeath, e.g. to splatter; no loot, not a kill).")] public bool dieOnHit;

        float lastHitAt = -999f;

        public override void OnSpawn() => lastHitAt = -999f;

        public override void OnContact(Entity other, bool entering)
        {
            if (!Owner.IsAlive || !other.IsAlive || !Owner.IsHostileTo(other))
                return;
            float now = Owner.Now;
            if (!entering && (interval <= 0f || now - lastHitAt < interval))
                return;
            lastHitAt = now;
            int amount = Mathf.RoundToInt(damage * Owner.Stat(Stat.Damage, 1f));
            if (other.Damage(amount, Owner))
                other.Knockback((other.Position - Owner.Position).normalized * knockback);
            if (!string.IsNullOrEmpty(effect))
                other.ApplyStatus(effect, Owner);
            if (dieOnHit)
                Owner.Die(DeathCause.Self);
        }

        public override void SaveState(StateWriter writer) => writer.Float(lastHitAt);
        public override void LoadState(StateReader reader) => lastHitAt = reader.Float();
    }

    [Serializable, JsonType("shoot")]
    [Doc("Fires projectile entities in a pattern. With 'interval' it attacks on a timer; with 'fireOnEvent' the shot happens on that animation frame (the timer then just starts the animation).")]
    public sealed class ShootAbility : Ability
    {
        [Required, EntityRef, Doc("Entity id of the projectile.")] public string projectile = "";
        [Doc("Volley shape. Default: one shot.")] public Pattern pattern;
        public float speed = 5f;
        public Aim aim = Aim.Player;
        public Vector2 direction = Vector2.right;
        [Doc("Where shots start, relative to the entity in world units; x is mirrored when facing left.")] public Vector2 muzzle;
        [Doc("Seconds between attacks. 0 = only when 'fireOnEvent' happens.")] public float interval = 2f;
        [Doc("Seconds before the first attack, counted from spawning or from entering the state that has this ability.")] public float initialDelay = 1f;
        [Doc("Volleys per attack.")] public int burst = 1;
        public float burstInterval = 0.1f;
        [Doc("Only attack when the player is closer than this (0 = any distance).")] public float range;
        [Doc("Animation event that releases the shot.")] public string fireOnEvent = "";
        [Doc("Animator parameter pulsed when an attack starts (a trigger, or a bool set for one tick).")] public string animTrigger = "";
        [Doc("Damage per shot; 0 = the projectile's own.")] public int damage;

        float nextAttackAt;
        int burstLeft;
        float nextVolleyAt;
        int volleyIndex;

        public override void OnSpawn()
        {
            nextAttackAt = Owner.SpawnTime + initialDelay;
            burstLeft = 0;
            nextVolleyAt = 0f;
            volleyIndex = 0;
        }

        /// <summary>The first attack comes initialDelay after spawning, or after entering the state that has this ability.</summary>
        public override void OnActivate() => nextAttackAt = Owner.Now + initialDelay;

        public override void Tick(float now, float dt)
        {
            if (interval > 0f && now >= nextAttackAt)
            {
                bool inRange = range <= 0f || DistanceToPlayer() <= range;
                if (inRange)
                {
                    float density = Owner.Team == Team.Enemy ? SickOfIt.Data.Difficulty.Density : 1f;
                    nextAttackAt = now + interval / Mathf.Max(0.01f, Owner.Stat(Stat.AttackSpeed, 1f) * density);
                    if (!string.IsNullOrEmpty(animTrigger))
                        Owner.Animation.Pulse(animTrigger);
                    if (string.IsNullOrEmpty(fireOnEvent))
                        StartBurst(now);
                    Owner.RecordNow();
                }
            }
            while (burstLeft > 0 && now >= nextVolleyAt)
            {
                FireVolley();
                burstLeft--;
                nextVolleyAt += burstInterval;
            }
        }

        public override void OnAnimationEvent(string eventName)
        {
            if (!string.IsNullOrEmpty(fireOnEvent) && eventName == fireOnEvent)
                StartBurst(Owner.Now);
        }

        void StartBurst(float now)
        {
            Owner.PlayAttackSound();
            burstLeft = Mathf.Max(1, burst);
            nextVolleyAt = now;
            FireVolley();
            burstLeft--;
            nextVolleyAt += burstInterval;
            Owner.RecordNow();
        }

        void FireVolley()
        {
            Vector2 origin = Owner.Position + new Vector2(muzzle.x * Owner.Facing, muzzle.y);
            Emission.Fire(Owner.World, Owner, projectile, pattern, aim, direction, origin, speed, damage, volleyIndex++, Owner.Random);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(nextAttackAt);
            writer.Int(burstLeft);
            writer.Float(nextVolleyAt);
            writer.Int(volleyIndex);
        }

        public override void LoadState(StateReader reader)
        {
            nextAttackAt = reader.Float();
            burstLeft = reader.Int();
            nextVolleyAt = reader.Float();
            volleyIndex = reader.Int();
        }
    }

    [Serializable, JsonType("melee")]
    [Doc("A close-range hit. With autoAttack it attacks whenever the player is in range and the cooldown is ready; the hit lands on 'hitEvent' (an animation event) or immediately.")]
    public sealed class MeleeAbility : Ability
    {
        public float range = 1.5f;
        public int damage = 1;
        [Doc("Seconds between attacks. 0 = keep attacking while the player is in range.")] public float cooldown = 1f;
        [Doc("Seconds an attack lasts (it stands still meanwhile).")] public float window = 0.35f;
        [Doc("Start attacks by itself. False = only hit on 'hitEvent' (another ability or the animator drives the attack).")] public bool autoAttack = true;
        [Doc("Animation event that deals the hit.")] public string hitEvent = "";
        [Doc("Animator bool set while attacking.")] public string animBool = "";
        [Doc("Animator parameter pulsed when an attack starts.")] public string animTrigger = "";
        [EffectRef] public string effect = "";
        public float knockback;

        float readyAt;
        float attackUntil;

        public override void OnSpawn()
        {
            readyAt = 0f;
            attackUntil = 0f;
        }

        public override void Tick(float now, float dt)
        {
            if (!autoAttack)
                return;

            bool inRange = DistanceToPlayer() <= range;
            bool attacking;
            if (cooldown <= 0f)
            {
                attacking = inRange;
            }
            else
            {
                if (inRange && now >= readyAt)
                {
                    attackUntil = now + Mathf.Min(window, cooldown);
                    readyAt = now + cooldown / Mathf.Max(0.01f, Owner.Stat(Stat.AttackSpeed, 1f));
                    if (!string.IsNullOrEmpty(animTrigger))
                        Owner.Animation.Pulse(animTrigger);
                    if (string.IsNullOrEmpty(hitEvent))
                        Hit();
                    Owner.RecordNow();
                }
                attacking = now < attackUntil;
            }

            if (attacking)
                Owner.HoldPosition(now + 0.0001f);
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, attacking);
        }

        public override void OnAnimationEvent(string eventName)
        {
            if (!string.IsNullOrEmpty(hitEvent) && eventName == hitEvent)
                Hit();
        }

        void Hit()
        {
            var player = Player;
            if (player == null || !player.IsAlive || DistanceToPlayer() > range || !Owner.IsHostileTo(player))
                return;
            if (player.Damage(Mathf.RoundToInt(damage * Owner.Stat(Stat.Damage, 1f)), Owner))
                player.Knockback((player.Position - Owner.Position).normalized * knockback);
            if (!string.IsNullOrEmpty(effect))
                player.ApplyStatus(effect, Owner);
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(readyAt);
            writer.Float(attackUntil);
        }

        public override void LoadState(StateReader reader)
        {
            readyAt = reader.Float();
            attackUntil = reader.Float();
        }
    }

    [Serializable, JsonType("explodeNear")]
    [Doc("When the player comes within 'triggerRadius', it arms; after 'fuse' seconds it explodes, hurting everything hostile within 'blastRadius', then dies (running onDeath, e.g. to leave sludge).")]
    public sealed class ExplodeNearAbility : Ability
    {
        public float triggerRadius = 1f;
        public float fuse = 0.6f;
        public float blastRadius = 1.2f;
        public int damage = 2;
        [Doc("Stand still while the fuse burns.")] public bool stopWhenArmed = true;
        [Doc("Flash while the fuse burns.")] public bool flash = true;
        [EffectRef] public string effect = "";
        [Doc("Animator bool set while armed.")] public string animBool = "";

        float armedAt = -1f;

        public override void OnSpawn() => armedAt = -1f;

        public override void Tick(float now, float dt)
        {
            if (armedAt < 0f)
            {
                if (DistanceToPlayer() <= triggerRadius)
                {
                    armedAt = now;
                    Owner.RecordNow();
                }
            }
            else
            {
                if (stopWhenArmed)
                    Owner.HoldPosition(now + 0.0001f);
                if (flash)
                    Owner.FlashFor(0.05f, Mathf.Repeat((now - armedAt) * 8f, 1f) < 0.5f ? Color.white : Color.red);
                if (now - armedAt >= fuse)
                {
                    Explode();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(animBool))
                Owner.Animation.SetBool(animBool, armedAt >= 0f);
        }

        void Explode()
        {
            armedAt = -1f;
            int amount = Mathf.RoundToInt(damage * Owner.Stat(Stat.Damage, 1f));
            Owner.World.ForEachHostileWithin(Owner, Owner.Position, blastRadius, target =>
            {
                target.Damage(amount, Owner, ignoreInvincibility: false);
                if (!string.IsNullOrEmpty(effect))
                    target.ApplyStatus(effect, Owner);
            });
            Owner.World.Effects.Shake(0.15f, 0.2f);
            SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.explosion);
            SickOfIt.Core.Fx.Fx.Flash(Owner.Position, new Color(1f, 0.8f, 0.4f, 0.9f), blastRadius * 2.2f, 0.18f);
            SickOfIt.Core.Fx.Fx.Burst(Owner.Position, new Color(1f, 0.55f, 0.2f, 1f), 16, 6f, 0.18f, 0.5f);
            Owner.World.Feel?.Explosion(Owner.Position, blastRadius, new Color(1f, 0.6f, 0.25f, 1f));
            Owner.Die(DeathCause.Self);
        }

        public override void SaveState(StateWriter writer) => writer.Float(armedAt);
        public override void LoadState(StateReader reader) => armedAt = reader.Float();
    }

    [Serializable, JsonType("aura")]
    [Doc("Every 'interval' seconds, hurts (and/or applies a status to) everything within 'radius'. Ignores hit invincibility, so it works as damage over time (sludge, fire, poison clouds).")]
    public sealed class AuraAbility : Ability
    {
        public float radius = 0.5f;
        public int damagePerTick = 1;
        public float interval = 0.5f;
        [EffectRef] public string effect = "";
        [Doc("Who it affects.")] public AuraTargets affects = AuraTargets.Hostile;

        float nextTickAt;

        public override void OnSpawn() => nextTickAt = Owner.SpawnTime + interval;

        public override void Tick(float now, float dt)
        {
            if (now < nextTickAt)
                return;
            nextTickAt = now + Mathf.Max(0.05f, interval);
            int amount = Mathf.RoundToInt(damagePerTick * Owner.Stat(Stat.Damage, 1f));
            Owner.World.ForEachWithin(Owner.Position, radius, target =>
            {
                if (target == Owner || !target.IsAlive)
                    return;
                bool hostile = Owner.IsHostileTo(target);
                if (affects == AuraTargets.Hostile && !hostile) return;
                if (affects == AuraTargets.Friendly && hostile) return;
                if (amount > 0)
                    target.Damage(amount, Owner, ignoreInvincibility: true);
                if (!string.IsNullOrEmpty(effect))
                    target.ApplyStatus(effect, Owner);
            });
        }

        public override void SaveState(StateWriter writer) => writer.Float(nextTickAt);
        public override void LoadState(StateReader reader) => nextTickAt = reader.Float();
    }

    public enum AuraTargets
    {
        Hostile,
        Friendly,
        All,
    }

    [Serializable, JsonType("projectile")]
    [Doc("Makes it a projectile: hurts the first hostile thing it touches, then disappears.")]
    public sealed class ProjectileAbility : Ability
    {
        public int damage = 1;
        [Doc("Extra targets it can pass through.")] public int pierce;
        [Doc("Disappear when hitting ground, walls, or platforms (after its last bounce, if it has any).")] public bool dieOnSolid;
        [Doc("Bounces off ground, walls and platforms this many times, mirroring its velocity about the surface, before 'dieOnSolid' applies. 0 = none. Needs a body that moves by velocity (not Dynamic).")] public int bounces;
        [Doc("Speed kept after each bounce (1 = all of it).")] public float bounceKeep = 1f;
        [SoundRef, Doc("Sound id (audio.json) played at each bounce; empty = none.")] public string bounceSound = "";
        [Doc("Passes through hostile things that have no health (enemy fireballs, hazards) instead of hitting them: the hero's own shots use it so they can't cancel a bullet.")] public bool onlyHitLiving;
        [EffectRef] public string effect = "";
        public float knockback;
        public AudioClip hitSound;

        int hits;
        int bouncesLeft;

        public int BouncesLeft => bouncesLeft;

        public override void OnSpawn()
        {
            hits = 0;
            bouncesLeft = bounces;
        }

        public override void OnContact(Entity other, bool entering)
        {
            if (!entering || !Owner.IsAlive || !other.IsAlive || !Owner.IsHostileTo(other))
                return;
            if (onlyHitLiving && other.MaxHealth <= 0)
                return;
            // The hero's dash goes through shots untouched: the shot flies on.
            if (other.Phased)
                return;
            int amount = Mathf.RoundToInt((Owner.DamageOverride > 0 ? Owner.DamageOverride : damage) * Owner.DamageMultiplier);
            if (other.Damage(amount, Owner))
                other.Knockback(Owner.Body.linearVelocity.normalized * knockback);
            if (!string.IsNullOrEmpty(effect))
                other.ApplyStatus(effect, Owner);
            if (hitSound != null)
                AudioSource.PlayClipAtPoint(hitSound, Owner.Position);
            hits++;
            if (hits > pierce)
                Owner.Die(DeathCause.Self);
        }

        public override void OnSolidContact(Collider2D collider)
        {
            if (!Owner.IsAlive)
                return;
            if (bouncesLeft > 0 && Owner.Body != null && TryBounce(collider))
                return;
            if (dieOnSolid)
                Owner.Die(DeathCause.Self);
        }

        /// <summary>Mirrors the velocity about the surface it touched. False if it was already moving away from it.</summary>
        bool TryBounce(Collider2D collider)
        {
            Vector2 position = Owner.Position;
            Vector2 velocity = Owner.Body.linearVelocity;
            Vector2 normal = position - collider.ClosestPoint(position);
            if (normal.sqrMagnitude < 0.000001f)
            {
                // Its centre is inside the solid: leave by the nearest face of the solid's box.
                Bounds box = collider.bounds;
                float left = position.x - box.min.x, right = box.max.x - position.x;
                float below = position.y - box.min.y, above = box.max.y - position.y;
                float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(below, above));
                normal = nearest == left ? Vector2.left : nearest == right ? Vector2.right : nearest == below ? Vector2.down : Vector2.up;
            }
            normal.Normalize();
            float into = Vector2.Dot(velocity, normal);
            if (into >= 0f)
                return false;
            Vector2 mirrored = (velocity - 2f * into * normal) * bounceKeep;
            Owner.Body.linearVelocity = mirrored;
            Owner.Body.position = position + normal * 0.03f;
            bouncesLeft--;
            if (!string.IsNullOrEmpty(bounceSound))
                SickOfIt.Core.Audio.Sfx.Play(bounceSound);
            SickOfIt.Core.Fx.Fx.Spark(position, Owner.FxColor, 0.25f);
            Owner.World.Telemetry?.Record("bounce", Owner.DefId, bouncesLeft, $"{normal.x:0.#},{normal.y:0.#}");
            Owner.RecordNow();
            return true;
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Int(hits);
            writer.Int(bouncesLeft);
        }

        public override void LoadState(StateReader reader)
        {
            hits = reader.Int();
            bouncesLeft = reader.Int();
        }
    }

    [Serializable, JsonType("lifetime")]
    [Doc("Dies after this many seconds (running onDeath, so a fireball can leave something behind).")]
    public sealed class LifetimeAbility : Ability
    {
        public float seconds = 5f;

        public override void Tick(float now, float dt)
        {
            if (now - Owner.SpawnTime >= seconds)
                Owner.Die(DeathCause.Self);
        }
    }

    [Serializable, JsonType("spawner")]
    [Doc("Spawns other entities every 'interval' seconds, keeping at most 'maxAlive' of them.")]
    public sealed class SpawnerAbility : Ability
    {
        [Required, EntityRef] public string entity = "";
        public float interval = 3f;
        public float initialDelay = 1f;
        public int maxAlive = 3;
        public Vector2 offset;
        [Doc("Animator parameter pulsed when spawning.")] public string animTrigger = "";

        float nextAt;

        public override void OnSpawn() => nextAt = Owner.SpawnTime + initialDelay;
        public override void OnActivate() => nextAt = Owner.Now + initialDelay;

        public override void Tick(float now, float dt)
        {
            if (now < nextAt)
                return;
            nextAt = now + interval;
            string tag = Owner.ChildTag;
            if (Owner.World.CountAlive(new TargetSelector { tag = tag }, Owner) >= maxAlive)
                return;
            if (!string.IsNullOrEmpty(animTrigger))
                Owner.Animation.Pulse(animTrigger);
            var options = new SpawnOptions { Team = Owner.Team, Facing = Owner.Facing };
            options.Tags.Add(tag);
            Owner.World.Spawn(entity, Owner.Position + new Vector2(offset.x * Owner.Facing, offset.y), options);
            Owner.RecordNow();
        }

        public override void SaveState(StateWriter writer) => writer.Float(nextAt);
        public override void LoadState(StateReader reader) => nextAt = reader.Float();
    }

    [Serializable, JsonType("animParam")]
    [Doc("Keeps an animator bool equal to a condition, e.g. \"IsCrouching\" while the player is far away.")]
    public sealed class AnimParamAbility : Ability
    {
        [Required] public string param = "";
        public Trigger when;

        Trigger condition;

        protected override void OnCreate() => condition = when?.Fresh();

        public override void OnActivate()
        {
            condition?.Reset(Owner.Triggers);
        }

        public override void Tick(float now, float dt)
        {
            Owner.Animation.SetBool(param, condition != null && condition.Check(Owner.Triggers));
        }

        public override void SaveState(StateWriter writer) => condition?.SaveState(writer);
        public override void LoadState(StateReader reader) => condition?.LoadState(reader);
    }

    [Serializable, JsonType("react")]
    [Doc("Runs actions when a trigger fires, e.g. enrage below half health. 'maxTimes' 0 = every time.")]
    public sealed class ReactAbility : Ability
    {
        public Trigger when;
        [Doc("Actions to run (self = this entity).")] public List<GameAction> @do = new List<GameAction>();
        public int maxTimes = 1;

        Trigger condition;
        bool wasTrue;
        int fired;

        protected override void OnCreate() => condition = when?.Fresh();

        public override void OnSpawn()
        {
            wasTrue = false;
            fired = 0;
        }

        public override void OnActivate()
        {
            condition?.Reset(Owner.Triggers);
            wasTrue = false;
        }

        public override void Tick(float now, float dt)
        {
            if (condition == null || (maxTimes > 0 && fired >= maxTimes))
                return;
            bool value = condition.Check(Owner.Triggers);
            bool fire = condition.EdgeTriggered ? value && !wasTrue : value;
            wasTrue = value;
            if (!fire)
                return;
            fired++;
            Owner.RunActions(@do, fired - 1);
            Owner.RecordNow();
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Bool(wasTrue);
            writer.Int(fired);
            condition?.SaveState(writer);
        }

        public override void LoadState(StateReader reader)
        {
            wasTrue = reader.Bool();
            fired = reader.Int();
            condition?.LoadState(reader);
        }
    }

    [Serializable, JsonType("onAnimationEvent")]
    [Doc("Runs actions when an animation event happens.")]
    public sealed class OnAnimationEventAbility : Ability
    {
        [Required, Doc("Event name.")] public string @event = "";
        public List<GameAction> @do = new List<GameAction>();

        public override void OnAnimationEvent(string eventName)
        {
            if (eventName == @event)
                Owner.RunActions(@do, 0);
        }
    }
}
