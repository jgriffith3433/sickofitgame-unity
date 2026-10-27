using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    public enum DeathCause
    {
        /// <summary>Killed by damage or a kill action: runs onDeath, drops loot, counts as a kill.</summary>
        Damage,
        /// <summary>Ended itself (exploded, expired, projectile hit): runs onDeath, no loot, not a kill.</summary>
        Self,
        /// <summary>Removed: nothing runs.</summary>
        Despawn,
    }

    /// <summary>How to spawn one entity.</summary>
    public sealed class SpawnOptions
    {
        /// <summary>Overrides the entity's team (projectiles take their shooter's).</summary>
        public Team? Team;
        /// <summary>+1 right, -1 left, 0 = decide from FacePlayer or velocity.</summary>
        public float Facing;
        public bool FacePlayer = true;
        public Vector2 Velocity;
        public string InstanceId = "";
        public readonly List<string> Tags = new List<string>();
        /// <summary>Overrides starting health (0 = keep).</summary>
        public int Health;
        public readonly List<(Stat stat, float multiply, float add)> Modifiers = new List<(Stat, float, float)>();
        /// <summary>Projectile damage per hit (0 = the projectile's own).</summary>
        public int DamageOverride;
        public float DamageMultiplier = 1f;
        public Entity Owner;

        public static SpawnOptions From(ActionContext context, string id, List<string> tags, int health, List<StatModifierDef> modifiers)
        {
            var options = new SpawnOptions { InstanceId = id ?? "", Health = health };
            if (tags != null)
                options.Tags.AddRange(tags);
            if (modifiers != null)
            {
                foreach (var m in modifiers)
                    options.Modifiers.Add((m.stat, m.MultiplyFor(context.Repeat), m.add));
            }
            return options;
        }
    }

    /// <summary>Fires one volley of projectile entities. Shared by the shoot ability and level hazards.</summary>
    public static class Emission
    {
        static readonly List<Shot> Shots = new List<Shot>();
        static readonly SinglePattern Single = new SinglePattern();

        public static Vector2 AimDirection(GameplayWorld world, Entity owner, Aim aim, Vector2 direction, Vector2 origin, IRandomSource random, float speed = 0f)
        {
            switch (aim)
            {
                case Aim.Player:
                {
                    Vector2 to = world.PlayerAimPoint - origin;
                    if (world.Player != null && to.sqrMagnitude > 0.0001f)
                        return to.normalized;
                    return new Vector2(owner != null ? owner.Facing : 1f, 0f);
                }
                case Aim.Predictive:
                {
                    if (world.Player == null)
                        return new Vector2(owner != null ? owner.Facing : 1f, 0f);
                    return LeadDirection(world.PlayerAimPoint, world.PlayerVelocity, origin, speed);
                }
                case Aim.Facing: return new Vector2(owner != null ? owner.Facing : 1f, 0f);
                case Aim.Direction: return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
                case Aim.Down: return Vector2.down;
                case Aim.Up: return Vector2.up;
                case Aim.Left: return Vector2.left;
                case Aim.Right: return Vector2.right;
                default:
                {
                    float a = random.Range(0f, Mathf.PI * 2f);
                    return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                }
            }
        }

        /// <summary>
        /// The direction that meets a target moving at a constant velocity: solves |target + velocity * t - origin| = speed * t
        /// for the smallest positive t. Without a solution (the target outruns the shot, or the shot has no speed) it aims
        /// straight at the target.
        /// </summary>
        public static Vector2 LeadDirection(Vector2 target, Vector2 velocity, Vector2 origin, float speed)
        {
            Vector2 to = target - origin;
            Vector2 straight = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.right;
            if (speed <= 0.0001f)
                return straight;
            float a = velocity.sqrMagnitude - speed * speed;
            float b = 2f * Vector2.Dot(to, velocity);
            float c = to.sqrMagnitude;
            float t;
            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) < 0.0001f)
                    return straight;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                    return straight;
                float root = Mathf.Sqrt(disc);
                float t1 = (-b - root) / (2f * a);
                float t2 = (-b + root) / (2f * a);
                t = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            }
            if (t <= 0f || t > MaxLeadSeconds)
                return straight;
            Vector2 meet = to + velocity * t;
            return meet.sqrMagnitude > 0.0001f ? meet.normalized : straight;
        }

        /// <summary>A shot never leads further ahead than this many seconds of flight.</summary>
        public const float MaxLeadSeconds = 2f;

        /// <summary>Speed x lifetime of a hostile projectile entity (no lifetime = far); 0 for anything else, so off-screen starts are only used where they are safe.</summary>
        static float ShotReach(GameplayWorld world, string projectile, float speed)
        {
            if (speed <= 0.0001f || string.IsNullOrEmpty(projectile) || !world.Content.Entities.TryGetValue(projectile, out var def) || def.team != Team.Enemy)
                return 0f;
            foreach (var ability in def.abilities)
            {
                if (ability is LifetimeAbility life)
                    return speed * life.seconds;
            }
            return 1000f;
        }

        public static void Fire(GameplayWorld world, Entity owner, string projectile, Pattern pattern, Aim aim, Vector2 direction,
            Vector2 origin, float speed, int damage, int volleyIndex, IRandomSource random)
        {
            float speedMultiplier = owner != null ? owner.Stat(Stat.ProjectileSpeed, 1f) : 1f;
            if (owner != null && owner.Team == Team.Enemy && world.Perks != null)
                speedMultiplier *= world.Perks.EnemyShotSpeed; // Sticky Air
            if (owner != null && owner.Team == Team.Enemy)
                speedMultiplier *= SickOfIt.Data.Difficulty.BulletSpeed;
            var input = new EmitInput
            {
                Origin = origin,
                Aim = AimDirection(world, owner, aim, direction, origin, random, speed * speedMultiplier),
                VolleyIndex = volleyIndex,
                View = world.View,
                Reach = ShotReach(world, projectile, speed * speedMultiplier),
                Random = random,
            };

            Shots.Clear();
            (pattern ?? Single).Emit(input, Shots);

            float damageMultiplier = owner != null ? owner.Stat(Stat.Damage, 1f) : 1f;
            // Copy: spawning can run actions that fire other volleys.
            var shots = Shots.ToArray();
            foreach (var shot in shots)
            {
                var options = new SpawnOptions
                {
                    Team = owner != null ? owner.Team : (Team?)null,
                    Velocity = shot.Direction * speed * speedMultiplier,
                    Facing = shot.Direction.x < 0f ? -1f : 1f,
                    FacePlayer = false,
                    DamageOverride = damage,
                    DamageMultiplier = damageMultiplier,
                    Owner = owner,
                };
                world.Spawn(projectile, shot.Position, options);
            }
        }
    }
}
