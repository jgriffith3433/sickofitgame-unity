using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A runtime instance of an <see cref="EntityDef"/>: an enemy, boss, projectile, or hazard (or, in its
    /// "player" form, the player wrapped so abilities and scripts can target, damage, and apply statuses to it).
    ///
    /// Entities are pooled. GameplayWorld spawns them and ticks them in spawn order from FixedUpdate, so
    /// the simulation runs in a fixed, deterministic order. All state that drives decisions is saved
    /// through <see cref="SimHistory"/>; position and velocity are saved by RewindableTransform and
    /// RewindableRigidbody2D, and active/inactive by RewindableGameObject.
    /// </summary>
    public sealed class Entity : MonoBehaviour, ISimState, IRandomSource, IDamageable, IPoolReturnable
    {
        sealed class StateRuntime
        {
            public StateDef Def;
            public Ability[] Abilities;
            public Trigger[] Transitions;
        }

        struct ModifierInstance
        {
            public Stat Stat;
            public float Multiply;
            public float Add;
            public float Until;
        }

        struct StatusInstance
        {
            public StatusEffectDef Def;
            public float Until;
            public float NextTick;
            public int Stacks;
        }

        // ─── Identity ─────────────────────────────────────────────────

        public GameplayWorld World { get; private set; }
        public EntityDef Def { get; private set; }
        public string DefId => Def != null ? Def.id : IsPlayer ? "player" : "";
        public string InstanceId { get; private set; } = "";
        public readonly List<string> Tags = new List<string>();
        public Team Team { get; private set; }
        public int Serial { get; private set; }
        public bool IsPlayer { get; private set; }
        /// <summary>Tag given to entities this one spawns with a spawner ability.</summary>
        public string ChildTag => "child:" + Serial;

        // ─── Components ───────────────────────────────────────────────

        public Rigidbody2D Body { get; private set; }
        public SpriteRenderer Sprite { get; private set; }
        public EntityAnimation Animation { get; private set; }

        /// <summary>Animator names the engine drives itself: a trigger pulsed on every hit that doesn't kill, and the
        /// state played as the death effect.</summary>
        public const string HurtParameter = "Hurt", DieState = "Die";

        SimHistory history;
        AudioSource audioSource;
        AudioSource loopSource;
        ObjectPool pool;
        Collider2D[] colliders;
        EntityHealthBar healthBar;
        PlayerHealth playerHealth;
        PlayerController playerController;

        // ─── Behavior ─────────────────────────────────────────────────

        Ability[] always = Array.Empty<Ability>();
        StateRuntime[] states = Array.Empty<StateRuntime>();
        readonly TriggerContext triggers = new TriggerContext();
        public TriggerContext Triggers => triggers;

        // ─── Simulation state (saved) ─────────────────────────────────

        bool alive;
        int health;
        int maxHealth;
        float invulnerableUntil;
        float spawnTime;
        float facing = 1f;
        float holdUntil;
        float overrideUntil;
        Vector2 overrideVelocity;
        float flashUntil;
        Color flashColor = Color.white;
        DeterministicRandom random;
        int damageOverride;
        float damageMultiplier = 1f;
        int stateIndex = -1;
        float stateEnteredAt;
        readonly List<ModifierInstance> modifiers = new List<ModifierInstance>();
        readonly List<StatusInstance> statuses = new List<StatusInstance>();

        // ─── Per-tick (not saved) ─────────────────────────────────────

        Vector2 moveIntent;
        bool hasMoveIntent;
        Vector2 velocityIntent;
        bool hasVelocityIntent;
        bool dirty;
        string damageLabel;
        Color baseColor = Color.white;
        float radius = 0.5f;
        bool touchingPlayer;

        // ─── Public state ─────────────────────────────────────────────

        public bool IsAlive => alive && (!IsPlayer || (playerHealth != null && playerHealth.IsAlive));
        public int Health => IsPlayer ? (playerHealth != null ? playerHealth.CurrentHealth : 0) : health;
        public int MaxHealth => IsPlayer ? (playerHealth != null ? playerHealth.MaxHealth : 0) : maxHealth;
        public int CurrentHealth => Health;
        public Vector2 Position => transform.position;
        /// <summary>Its first ability of type T (always-on first, then the states'), or null. For the playtest and tools.</summary>
        public T FindAbility<T>() where T : Ability
        {
            foreach (var a in always)
                if (a is T t) return t;
            foreach (var s in states)
                foreach (var a in s.Abilities)
                    if (a is T t) return t;
            return null;
        }

        /// <summary>Its first collider (the body), for standing checks.</summary>
        public Collider2D MainCollider => colliders != null && colliders.Length > 0 ? colliders[0] : null;
        /// <summary>+1 facing right, -1 facing left.</summary>
        public float Facing => facing;
        public float SpawnTime => spawnTime;
        public float StateEnteredAt => stateEnteredAt;
        public string StateName => stateIndex >= 0 && stateIndex < states.Length ? states[stateIndex].Def.name : "";
        public int DamageOverride => damageOverride;
        public float DamageMultiplier => damageMultiplier;
        /// <summary>Approximate body radius in world units, for area checks.</summary>
        public float Radius => radius;
        public float Now => GameClock.Instance != null ? GameClock.Instance.Time : 0f;
        public bool IsStunned => HasStatus(s => s.stun);
        /// <summary>Standing still for an attack or wind-up (movement requests are ignored).</summary>
        public bool IsHeld => Now < holdUntil || IsStunned;
        public float MoveSpeed => IsStunned ? 0f : Stat(Content.Stat.MoveSpeed, Def != null ? Def.moveSpeed : 0f);
        public IRandomSource Random => this;

        // =====================================================================
        // SETUP
        // =====================================================================

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Sprite = GetComponent<SpriteRenderer>();
            audioSource = GetComponent<AudioSource>();
            history = GetComponent<SimHistory>();
            healthBar = GetComponentInChildren<EntityHealthBar>(true);
            colliders = GetComponents<Collider2D>();
            if (history != null)
                history.Bind(this, () => Def != null);
        }

        public void SetOwnerPool(ObjectPool owner) => pool = owner;

        void OnEnable()
        {
            if (alive && World != null)
            {
                World.Register(this);
                StartLoopSound();
            }
        }

        void OnDisable()
        {
            StopLoopSound();
            World?.Unregister(this);
        }

        void OnDestroy()
        {
            if (World != null && colliders != null)
                World.UnregisterColliders(colliders);
        }

        /// <summary>Builds the per-instance ability copies. Called once per pooled instance.</summary>
        void Build(EntityDef def, GameplayWorld world)
        {
            if (Def == def)
                return;
            Def = def;
            World = world;
            world.RegisterColliders(colliders, this);

            var list = new List<Ability>();
            foreach (var a in def.abilities)
            {
                if (a != null)
                    list.Add(a.Instantiate(this));
            }
            always = list.ToArray();

            states = new StateRuntime[def.states.Count];
            for (int i = 0; i < def.states.Count; i++)
            {
                var s = def.states[i];
                var abilities = new List<Ability>();
                foreach (var a in s.abilities)
                {
                    if (a != null)
                        abilities.Add(a.Instantiate(this));
                }
                var transitions = new Trigger[s.transitions.Count];
                for (int t = 0; t < transitions.Length; t++)
                    transitions[t] = s.transitions[t].when?.Fresh();
                states[i] = new StateRuntime { Def = s, Abilities = abilities.ToArray(), Transitions = transitions };
            }

            Animation = new EntityAnimation(def.visual.animator, world.Content.Sheets, Sprite, OnAnimationEvent);
            baseColor = def.visual.color;
        }

        /// <summary>Starts a new life from the pool. The world assigns the serial and seeds the random stream.</summary>
        public void Begin(GameplayWorld world, EntityDef def, SpawnOptions options, int serial, uint seed)
        {
            Build(def, world);

            Serial = serial;
            InstanceId = options.InstanceId ?? "";
            Tags.Clear();
            Tags.AddRange(def.tags);
            Tags.AddRange(options.Tags);
            Team = options.Team ?? def.team;
            random = DeterministicRandom.Derive(seed, (uint)serial);
            spawnTime = Now;
            damageOverride = options.DamageOverride;
            damageMultiplier = options.DamageMultiplier;
            invulnerableUntil = 0f;
            holdUntil = 0f;
            overrideUntil = 0f;
            flashUntil = 0f;
            stateIndex = -1;
            touchingPlayer = false;
            modifiers.Clear();
            statuses.Clear();
            foreach (var m in options.Modifiers)
                modifiers.Add(new ModifierInstance { Stat = m.stat, Multiply = m.multiply, Add = m.add, Until = float.PositiveInfinity });

            float size = Stat(Content.Stat.Size, 1f);
            float scale = def.visual.scale * size;
            transform.localScale = new Vector3(scale, scale, 1f);
            radius = (def.body.shape == ShapeKind.Circle ? def.body.radius : Mathf.Max(def.body.size.x, def.body.size.y) * 0.5f) * scale;

            maxHealth = options.Health > 0 ? options.Health : Mathf.Max(0, Mathf.RoundToInt(def.health * Stat(Content.Stat.MaxHealth, 1f)));
            health = maxHealth;

            if (options.Facing != 0f)
                facing = Mathf.Sign(options.Facing);
            else if (options.FacePlayer && world.Player != null)
                facing = world.Player.Position.x < transform.position.x ? -1f : 1f;
            else
                facing = 1f;

            if (Body != null)
            {
                Body.linearVelocity = options.Velocity;
                Body.angularVelocity = 0f;
            }
            if (def.body.rotateToVelocity && options.Velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(options.Velocity.y, options.Velocity.x) * Mathf.Rad2Deg);
            else
                transform.rotation = Quaternion.identity;

            Animation.Reset();
            alive = true;

            UpdateTriggerContext();
            foreach (var a in always)
            {
                a.OnSpawn();
                a.OnActivate();
            }
            foreach (var s in states)
            {
                foreach (var a in s.Abilities)
                    a.OnSpawn();
            }
            if (states.Length > 0)
                EnterState(InitialState());

            ApplyLook();
            world.Register(this);

            PlayRandom(def.sounds.spawn);
            StartLoopSound();
            if (def.onSpawn.Count > 0)
                RunActions(def.onSpawn, 0);
            dirty = true;
        }

        int InitialState()
        {
            if (Def.initialStates.Count > 0)
            {
                string pick = Def.initialStates[random.Range(0, Def.initialStates.Count)];
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Def.name == pick)
                        return i;
                }
            }
            return 0;
        }

        /// <summary>Wraps the player so scripts and abilities can treat it like an entity.</summary>
        public void BeginAsPlayer(GameplayWorld world, PlayerHealth health, PlayerController controller)
        {
            World = world;
            IsPlayer = true;
            Team = Team.Player;
            Serial = 0;
            InstanceId = "player";
            playerHealth = health;
            playerController = controller;
            alive = true;
            spawnTime = Now;
            random = DeterministicRandom.Derive(world.Seed, 0);
            colliders = GetComponentsInChildren<Collider2D>(true);
            world.RegisterColliders(colliders, this);
            Body = GetComponent<Rigidbody2D>();
            history = GetComponent<SimHistory>();
            if (history == null)
                history = gameObject.AddComponent<SimHistory>();
            history.Bind(this);
            radius = 0.25f;
        }

        // =====================================================================
        // TICK
        // =====================================================================

        void UpdateTriggerContext()
        {
            triggers.World = World;
            triggers.Self = this;
            triggers.Director = World.Director;
            triggers.Now = Now;
            triggers.ScopeStart = stateIndex >= 0 ? stateEnteredAt : spawnTime;
            triggers.Random = this;
        }

        public void Tick(float now, float dt)
        {
            if (!alive)
                return;

            UpdateTriggerContext();
            TickStatuses(now);
            if (!alive)
                return;
            ExpireModifiers(now);

            if (IsPlayer)
            {
                ApplyPlayerHooks(now);
                Flush();
                return;
            }

            hasMoveIntent = false;
            hasVelocityIntent = false;

            bool active = Def.activationRange <= 0f || World.Player == null
                || Vector2.Distance(Position, World.Player.Position) <= Def.activationRange;
            if (active && !IsStunned)
            {
                foreach (var a in always)
                {
                    a.Tick(now, dt);
                    if (!alive) return;
                }
                if (stateIndex >= 0)
                {
                    var state = states[stateIndex];
                    foreach (var a in state.Abilities)
                    {
                        a.Tick(now, dt);
                        if (!alive) return;
                    }
                    CheckTransitions(state);
                }
            }

            ApplyMovement(now, dt);
            Animation.Tick(dt);
            if (!alive)
                return;
            CheckPlayerContact();
            if (!alive)
                return;
            ApplyLook();
            Flush();
        }

        /// <summary>
        /// Enemies and the player are on layers that don't collide (so enemies can't shove the player around),
        /// which means physics never reports them touching. For those, check the overlap here and report it the
        /// same way (contact damage, kamikaze bursts, pickups).
        /// </summary>
        void CheckPlayerContact()
        {
            var player = World.Player;
            bool touching = false;
            if (player != null && player.IsAlive && player.colliders != null
                && Physics2D.GetIgnoreLayerCollision(gameObject.layer, player.gameObject.layer))
                touching = Overlaps(player);
            bool entering = touching && !touchingPlayer;
            if (touching != touchingPlayer)
                dirty = true;
            touchingPlayer = touching;
            if (touching)
                Dispatch(a => a.OnContact(player, entering));
        }

        bool Overlaps(Entity other)
        {
            foreach (var mine in colliders)
            {
                if (mine == null || !mine.enabled)
                    continue;
                foreach (var theirs in other.colliders)
                {
                    if (theirs != null && theirs.enabled && !theirs.isTrigger && theirs.gameObject.activeInHierarchy
                        && mine.Distance(theirs).isOverlapped)
                        return true;
                }
            }
            return false;
        }

        void Flush()
        {
            if (!dirty)
                return;
            dirty = false;
            history?.RecordNow();
        }

        void CheckTransitions(StateRuntime state)
        {
            UpdateTriggerContext();
            for (int i = 0; i < state.Transitions.Length; i++)
            {
                var trigger = state.Transitions[i];
                if (trigger != null && trigger.Check(triggers))
                {
                    SetState(state.Def.transitions[i].to);
                    return;
                }
            }
        }

        public void SetState(string name)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].Def.name == name)
                {
                    EnterState(i);
                    return;
                }
            }
            Debug.LogWarning($"[Entity] {DefId} has no state \"{name}\".");
        }

        void EnterState(int index)
        {
            if (stateIndex >= 0 && stateIndex < states.Length)
            {
                var old = states[stateIndex];
                foreach (var a in old.Abilities)
                    a.OnDeactivate();
                if (old.Def.onExit.Count > 0)
                    RunActions(old.Def.onExit, 0);
            }

            stateIndex = index;
            stateEnteredAt = Now;
            UpdateTriggerContext();
            var state = states[index];
            foreach (var t in state.Transitions)
                t?.Reset(triggers);
            foreach (var a in state.Abilities)
                a.OnActivate();
            if (!string.IsNullOrEmpty(state.Def.animation))
                Animation.Play(state.Def.animation);
            if (state.Def.onEnter.Count > 0)
                RunActions(state.Def.onEnter, 0);
            dirty = true;
        }

        // =====================================================================
        // MOVEMENT
        // =====================================================================

        /// <summary>Movement abilities call this each tick with the velocity they want.</summary>
        public void Move(Vector2 velocity)
        {
            moveIntent = velocity;
            hasMoveIntent = true;
        }

        /// <summary>Sets the body's velocity directly (projectiles, homing).</summary>
        public void SetVelocity(Vector2 velocity)
        {
            velocityIntent = velocity;
            hasVelocityIntent = true;
        }

        /// <summary>Stand still until this time (attacks, wind-ups).</summary>
        public void HoldPosition(float until)
        {
            holdUntil = Mathf.Max(holdUntil, until);
        }

        /// <summary>Move at this velocity until this time, ignoring movement abilities (dashes).</summary>
        public void OverrideMove(Vector2 velocity, float until)
        {
            overrideVelocity = velocity;
            overrideUntil = until;
            dirty = true;
        }

        void ApplyMovement(float now, float dt)
        {
            if (Body == null)
                return;

            bool dynamicBody = Body.bodyType == RigidbodyType2D.Dynamic;
            Vector2 velocity;
            bool apply;
            if (IsStunned || now < holdUntil)
            {
                velocity = Vector2.zero;
                apply = hasMoveIntent || hasVelocityIntent || now < holdUntil;
            }
            else if (now < overrideUntil)
            {
                velocity = overrideVelocity;
                apply = true;
            }
            else if (hasVelocityIntent)
            {
                velocity = velocityIntent;
                apply = true;
                Body.linearVelocity = velocity;
                if (Def.body.rotateToVelocity && velocity.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
                return;
            }
            else
            {
                velocity = moveIntent;
                apply = hasMoveIntent;
            }

            if (!apply)
            {
                if (Def.body.rotateToVelocity && Body.linearVelocity.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Body.linearVelocity.y, Body.linearVelocity.x) * Mathf.Rad2Deg);
                return;
            }

            if (dynamicBody)
            {
                Vector2 v = Body.linearVelocity;
                v.x = velocity.x;
                if (Body.gravityScale == 0f)
                    v.y = velocity.y;
                Body.linearVelocity = v;
            }
            else
            {
                Body.MovePosition(Body.position + velocity * dt);
            }
        }

        /// <summary>Different teams hurt each other; Neutral hurts and is hurt by no one.</summary>
        public bool IsHostileTo(Entity other)
        {
            return other != null && other != this && Team != Team.Neutral && other.Team != Team.Neutral && Team != other.Team;
        }

        public void FaceTowards(float x)
        {
            float dx = x - transform.position.x;
            if (Mathf.Abs(dx) > 0.0001f)
                SetFacing(dx);
        }

        public void SetFacing(float sign)
        {
            float f = sign < 0f ? -1f : 1f;
            if (f != facing)
            {
                facing = f;
                dirty = true;
            }
        }

        public void Knockback(Vector2 impulse)
        {
            if (impulse.sqrMagnitude < 0.0001f || Body == null || Body.bodyType != RigidbodyType2D.Dynamic)
                return;
            Body.AddForce(impulse, ForceMode2D.Impulse);
        }

        // =====================================================================
        // HEALTH
        // =====================================================================

        public void TakeDamage(int damage) => Damage(damage, null);

        /// <summary>Hurts this entity. Returns true if any damage was dealt.</summary>
        public bool Damage(int amount, Entity source, bool ignoreInvincibility = false)
        {
            if (!IsAlive || IsRewinding || amount <= 0)
                return false;
            if (HasStatus(s => s.invulnerable))
                return false;

            int final = Mathf.Max(0, Mathf.RoundToInt(amount * Stat(Content.Stat.DamageTaken, 1f)));
            if (final <= 0)
                return false;

            if (IsPlayer)
            {
                if (playerHealth == null || (!ignoreInvincibility && playerHealth.IsInvincible))
                    return false;
                // Record first: a killing hit ends the level (and its log) inside TakeDamage.
                World.NotifyDamaged(this, final, source, damageLabel);
                playerHealth.TakeDamage(final, ignoreInvincibility);
                return true;
            }

            if (Def.health <= 0 && maxHealth <= 0)
                return false;
            if (!ignoreInvincibility && Now < invulnerableUntil)
                return false;

            health = Mathf.Max(0, health - final);
            if (Def.invincibilityTime > 0f)
                invulnerableUntil = Now + Def.invincibilityTime;
            if (Def.visual.hurtFlash)
                FlashFor(0.1f, Color.white);
            if (maxHealth > 0 && (float)health / maxHealth <= Def.sounds.hurtThreshold)
            {
                if (Def.sounds.hurt.Count > 0)
                    PlayRandom(Def.sounds.hurt);
                else if (health > 0)
                    SickOfIt.Core.Audio.Sfx.Play(SoundOr(Def.sounds.hurtSound, SickOfIt.Core.Audio.Sfx.Events.enemyHurt));
            }
            if (health > 0 && Team == Team.Enemy)
                SickOfIt.Core.Fx.Fx.Spark(Position, FxColor, Mathf.Clamp(radius * 1.6f, 0.2f, 0.8f));
            // Art made by the pipeline (Docs/ArtPipeline.md) has a "Hurt" trigger; no-op for animators without one.
            if (health > 0)
                Animation?.Pulse(HurtParameter);
            World.NotifyDamaged(this, final, source, damageLabel);
            dirty = true;

            if (health <= 0)
                Die(DeathCause.Damage);
            return true;
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
                return;
            if (IsPlayer)
            {
                playerHealth?.SetHealth(Mathf.Min(playerHealth.MaxHealth, playerHealth.CurrentHealth + amount));
                return;
            }
            health = Mathf.Min(maxHealth, health + amount);
            dirty = true;
        }

        public void MakeInvincible(float seconds)
        {
            if (IsPlayer)
                playerHealth?.SetInvincibilityTimer(seconds);
            else
                invulnerableUntil = Mathf.Max(invulnerableUntil, Now + seconds);
            dirty = true;
        }

        public void Die(DeathCause cause)
        {
            if (!alive || IsRewinding)
                return;
            if (IsPlayer)
            {
                if (cause != DeathCause.Despawn)
                    playerHealth?.TakeDamage(playerHealth.CurrentHealth, true);
                return;
            }

            alive = false;
            StopLoopSound();
            Vector2 at = Position;

            if (cause != DeathCause.Despawn)
            {
                if (Def.onDeath.Count > 0)
                    RunActions(Def.onDeath, 0);
                if (Def.sounds.death.Count > 0)
                {
                    var clip = Def.sounds.death[random.Range(0, Def.sounds.death.Count)];
                    if (clip != null)
                        AudioSource.PlayClipAtPoint(clip, at, Def.sounds.volume);
                }
                else if (maxHealth > 0)
                {
                    SickOfIt.Core.Audio.Sfx.Play(SoundOr(Def.sounds.deathSound, SickOfIt.Core.Audio.Sfx.Events.enemyDie));
                }
                // An animator with a "Die" state plays it where the entity was (presentation only: the entity is
                // already gone from the simulation). Otherwise enemies pop into bits; things without health just vanish.
                if (Animation != null && Animation.TryGetClip(DieState, out var dieFrames, out float dieFps))
                {
                    SickOfIt.Core.Fx.Fx.Flipbook(dieFrames, dieFps, transform.position, transform.lossyScale, Sprite != null && Sprite.flipX,
                        Sprite != null ? Sprite.color : Color.white, Def.visual.layer, Def.visual.SortingOrder,
                        Def.visual.deathFade > 0f ? Def.visual.deathFade : 0.25f, Def.visual.deathPunch);
                    if (maxHealth > 0 && Def.visual.deathPop)
                    {
                        if (Def.visual.deathFlash)
                            SickOfIt.Core.Fx.Fx.Flash(at, Color.Lerp(FxColor, Color.white, 0.6f), Mathf.Clamp(radius * 3f, 0.5f, 2.5f), 0.16f);
                        SickOfIt.Core.Fx.Fx.Burst(at, FxColor, Def.visual.deathBits > 0 ? Def.visual.deathBits : 6, 2f + radius * 2f,
                            Mathf.Clamp(radius * 0.3f, 0.05f, 0.2f));
                    }
                }
                else if (maxHealth > 0 && Def.visual.deathPop)
                {
                    float size = Mathf.Clamp(radius * 3f, 0.5f, 2.5f);
                    SickOfIt.Core.Fx.Fx.Flash(at, Color.white, size, 0.14f);
                    SickOfIt.Core.Fx.Fx.Burst(at, FxColor, Mathf.Clamp(Mathf.RoundToInt(6 + radius * 10f), 6, 18), 3f + radius * 4f,
                        Mathf.Clamp(radius * 0.45f, 0.07f, 0.3f));
                }
            }
            if (cause == DeathCause.Damage)
            {
                DropLoot(at);
                DropEntities(at);
            }
            World.NotifyDied(this, cause);
            history?.RecordNow();

            if (pool != null)
                pool.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        void DropEntities(Vector2 at)
        {
            foreach (var drop in Def.drops)
            {
                for (int i = 0; i < Mathf.Max(1, drop.count); i++)
                {
                    if (random.Value() >= drop.chance)
                        continue;
                    var offset = new Vector2(random.Range(-0.2f, 0.2f), random.Range(0f, 0.2f));
                    World.Spawn(drop.entity, at + offset, new SpawnOptions { FacePlayer = false });
                }
            }
        }

        void DropLoot(Vector2 at)
        {
            var loot = Def.loot;
            if (loot.chance <= 0f)
                return;
            string pickup = string.IsNullOrEmpty(loot.entity) ? "diamond" : loot.entity;
            bool asEntity = World.Content.Entities.ContainsKey(pickup);
            if (!asEntity && World.PickupPool == null)
                return;
            for (int i = 0; i < loot.count; i++)
            {
                if (random.Value() > loot.chance)
                    continue;
                if (!asEntity)
                {
                    World.DropPoints(at, loot.value);
                    continue;
                }
                for (int d = 0; d < Mathf.Max(1, loot.value); d++)
                {
                    var offset = new Vector2(random.Range(-0.3f, 0.3f), random.Range(0f, 0.25f));
                    World.Spawn(pickup, at + offset, new SpawnOptions { FacePlayer = false });
                }
            }
        }

        // =====================================================================
        // STATS, MODIFIERS, STATUSES
        // =====================================================================

        public float Stat(Stat stat, float baseValue)
        {
            float multiply = 1f;
            float add = 0f;
            float now = Now;
            foreach (var m in modifiers)
            {
                if (m.Stat == stat && now < m.Until)
                {
                    multiply *= m.Multiply;
                    add += m.Add;
                }
            }
            foreach (var s in statuses)
            {
                foreach (var m in s.Def.modifiers)
                {
                    if (m.stat != stat) continue;
                    for (int k = 0; k < s.Stacks; k++)
                    {
                        multiply *= m.multiply;
                        add += m.add;
                    }
                }
            }
            return baseValue * multiply + add;
        }

        public void AddModifier(Stat stat, float multiply, float add, float until)
        {
            modifiers.Add(new ModifierInstance { Stat = stat, Multiply = multiply, Add = add, Until = until });
            dirty = true;
        }

        void ExpireModifiers(float now)
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (now >= modifiers[i].Until)
                {
                    modifiers.RemoveAt(i);
                    dirty = true;
                }
            }
        }

        public void ApplyStatus(string effectId, Entity source)
        {
            if (!IsAlive || IsRewinding)
                return;
            if (!World.Content.Effects.TryGetValue(effectId, out var def))
            {
                Debug.LogWarning($"[Entity] Unknown status effect \"{effectId}\".");
                return;
            }

            float now = Now;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Def != def)
                    continue;
                var s = statuses[i];
                switch (def.stacking)
                {
                    case Stacking.Ignore:
                        return;
                    case Stacking.Stack:
                        s.Stacks = Mathf.Min(def.maxStacks, s.Stacks + 1);
                        s.Until = now + def.duration;
                        break;
                    default:
                        s.Until = now + def.duration;
                        break;
                }
                statuses[i] = s;
                dirty = true;
                return;
            }

            statuses.Add(new StatusInstance { Def = def, Until = now + def.duration, NextTick = now + def.tickInterval, Stacks = 1 });
            World.NotifyStatus(this, effectId);
            dirty = true;
        }

        public void RemoveStatus(string effectId)
        {
            int removed = statuses.RemoveAll(s => s.Def.id == effectId);
            if (removed > 0)
                dirty = true;
        }

        bool HasStatus(Func<StatusEffectDef, bool> predicate)
        {
            foreach (var s in statuses)
            {
                if (predicate(s.Def))
                    return true;
            }
            return false;
        }

        void TickStatuses(float now)
        {
            for (int i = statuses.Count - 1; i >= 0 && i < statuses.Count; i--)
            {
                var s = statuses[i];
                if (s.Def.damagePerTick > 0 && s.Def.tickInterval > 0f)
                {
                    while (now >= s.NextTick && s.NextTick < s.Until)
                    {
                        s.NextTick += s.Def.tickInterval;
                        statuses[i] = s;
                        damageLabel = "status:" + s.Def.id;
                        Damage(s.Def.damagePerTick * s.Stacks, null, ignoreInvincibility: true);
                        damageLabel = null;
                        if (!alive)
                            return;
                        dirty = true;
                    }
                }
                if (i < statuses.Count && now >= statuses[i].Until)
                {
                    statuses.RemoveAt(i);
                    dirty = true;
                }
            }
        }

        void ApplyPlayerHooks(float now)
        {
            if (playerController == null)
                return;
            float speed = IsStunned ? 0f : Stat(Content.Stat.MoveSpeed, 1f);
            playerController.MoveSpeedMultiplier = Mathf.Max(0f, speed);
            playerController.FireRateMultiplier = Mathf.Max(0.1f, Stat(Content.Stat.AttackSpeed, 1f));
            playerController.DamageMultiplier = Mathf.Max(0f, Stat(Content.Stat.Damage, 1f));
            playerController.InputLocked = IsStunned || now < World.InputLockedUntil;
            var tint = StatusTint();
            var renderers = World.PlayerRenderers;
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null)
                        r.color = tint.a > 0f ? Color.Lerp(Color.white, new Color(tint.r, tint.g, tint.b, 1f), tint.a) : Color.white;
                }
            }
        }

        Color StatusTint()
        {
            Color tint = new Color(1f, 1f, 1f, 0f);
            foreach (var s in statuses)
            {
                if (s.Def.tint.a > tint.a)
                    tint = s.Def.tint;
            }
            return tint;
        }

        // =====================================================================
        // EVENTS
        // =====================================================================

        /// <summary>
        /// Sends an event to every active ability: the always-on ones, then the current state's. Stops if one of
        /// them kills the entity. (Ticking has its own loop, to avoid a closure per frame.)
        /// </summary>
        void Dispatch(Action<Ability> send)
        {
            foreach (var a in always)
            {
                send(a);
                if (!alive) return;
            }
            if (stateIndex < 0)
                return;
            foreach (var a in states[stateIndex].Abilities)
            {
                send(a);
                if (!alive) return;
            }
        }

        void OnAnimationEvent(string eventName)
        {
            if (alive && !IsRewinding)
                Dispatch(a => a.OnAnimationEvent(eventName));
        }

        void Contact(Collider2D other, bool entering, bool otherIsTrigger)
        {
            if (!alive || IsPlayer || IsRewinding || World == null)
                return;
            if (World.TryGetEntity(other, out var entity))
            {
                if (entity != this)
                    Dispatch(a => a.OnContact(entity, entering));
                return;
            }
            if (entering && !otherIsTrigger && !World.IsBoundary(other))
                Dispatch(a => a.OnSolidContact(other));
        }

        void OnTriggerEnter2D(Collider2D other) => Contact(other, true, other.isTrigger);
        void OnTriggerStay2D(Collider2D other) => Contact(other, false, other.isTrigger);
        void OnCollisionEnter2D(Collision2D collision) => Contact(collision.collider, true, false);
        void OnCollisionStay2D(Collision2D collision) => Contact(collision.collider, false, false);

        public void RunActions(List<GameAction> actions, int repeat)
        {
            if (actions == null || actions.Count == 0)
                return;
            World.RunActions(actions, new ActionContext
            {
                World = World,
                Self = this,
                Director = World.Director,
                Repeat = repeat,
                Scope = null,
                Random = this,
                Now = Now,
            });
        }

        /// <summary>Ask for this tick's state to be recorded, so a rewind can't land between a decision and its effect.</summary>
        public void RecordNow() => dirty = true;

        // =====================================================================
        // LOOK
        // =====================================================================

        public void FlashFor(float seconds, Color color)
        {
            flashUntil = Now + seconds;
            flashColor = color;
        }

        void ApplyLook()
        {
            if (Sprite == null || Def == null)
                return;
            Sprite.flipX = (facing < 0f) != Def.visual.facesLeft;
            var tint = StatusTint();
            Color color = baseColor;
            if (tint.a > 0f)
                color = Color.Lerp(color, new Color(tint.r, tint.g, tint.b, color.a), tint.a);
            if (Now < flashUntil)
                color = flashColor;
            Sprite.color = color;
            if (healthBar != null)
                healthBar.Show(maxHealth > 0 ? (float)health / maxHealth : 0f, transform.lossyScale.x);
        }

        Color FxColor => Def.visual.fxColor.a > 0f ? Def.visual.fxColor : new Color(1f, 0.95f, 0.82f, 1f);

        /// <summary>The entity's own sound id; for enemies with none, the game-wide fallback.</summary>
        string SoundOr(string own, string enemyDefault)
        {
            if (!string.IsNullOrEmpty(own))
                return own;
            return Team == Team.Enemy ? enemyDefault : null;
        }

        void PlayRandom(List<AudioClip> clips)
        {
            if (clips == null || clips.Count == 0 || audioSource == null)
                return;
            var clip = clips[random.Range(0, clips.Count)];
            if (clip == null)
                return;
            audioSource.pitch = 1f + random.Range(-Def.sounds.pitchVariation, Def.sounds.pitchVariation);
            audioSource.PlayOneShot(clip, Def.sounds.volume);
        }

        /// <summary>
        /// sounds.loopSound plays on its own source while the entity is alive and active (presentation only); it stops
        /// when the entity dies, despawns or is switched off (pool return, a rewind back to before it spawned).
        /// </summary>
        void StartLoopSound()
        {
            if (Def == null || string.IsNullOrEmpty(Def.sounds.loopSound) || !alive)
                return;
            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.spatialBlend = 0f;
            }
            if (!loopSource.isPlaying)
                SickOfIt.Core.Audio.Sfx.PlayLoop(loopSource, Def.sounds.loopSound, Def.sounds.volume);
        }

        void StopLoopSound()
        {
            if (loopSource != null && loopSource.isPlaying)
                loopSource.Stop();
        }

        static bool IsRewinding => GameClock.Instance != null && GameClock.Instance.IsRewinding;

        // =====================================================================
        // IRandomSource
        // =====================================================================

        public float Value() => random.Value();
        public float Range(float min, float max) => random.Range(min, max);
        public int Range(int min, int max) => random.Range(min, max);

        // =====================================================================
        // REWIND
        // =====================================================================

        public void SaveState(StateWriter w)
        {
            w.Bool(alive);
            w.Int(health);
            w.Int(maxHealth);
            w.Float(invulnerableUntil);
            w.Float(spawnTime);
            w.Float(facing);
            w.Float(holdUntil);
            w.Float(overrideUntil);
            w.Float(overrideVelocity.x);
            w.Float(overrideVelocity.y);
            w.Float(flashUntil);
            w.UInt(random.State);
            w.Int(damageOverride);
            w.Float(damageMultiplier);
            w.Int(stateIndex);
            w.Float(stateEnteredAt);

            w.Int(modifiers.Count);
            foreach (var m in modifiers)
            {
                w.Int((int)m.Stat);
                w.Float(m.Multiply);
                w.Float(m.Add);
                w.Float(m.Until);
            }
            w.Int(statuses.Count);
            foreach (var s in statuses)
            {
                w.String(s.Def.id);
                w.Float(s.Until);
                w.Float(s.NextTick);
                w.Int(s.Stacks);
            }

            if (IsPlayer)
                return;

            w.Bool(touchingPlayer);
            Animation.SaveState(w);
            foreach (var a in always)
                a.SaveState(w);
            foreach (var s in states)
            {
                foreach (var a in s.Abilities)
                    a.SaveState(w);
                foreach (var t in s.Transitions)
                    t?.SaveState(w);
            }
        }

        public void LoadState(StateReader r)
        {
            alive = r.Bool();
            health = r.Int();
            maxHealth = r.Int();
            invulnerableUntil = r.Float();
            spawnTime = r.Float();
            facing = r.Float();
            holdUntil = r.Float();
            overrideUntil = r.Float();
            overrideVelocity = new Vector2(r.Float(), r.Float());
            flashUntil = r.Float();
            random.State = r.UInt();
            damageOverride = r.Int();
            damageMultiplier = r.Float();
            stateIndex = r.Int();
            stateEnteredAt = r.Float();

            modifiers.Clear();
            int modifierCount = r.Int();
            for (int i = 0; i < modifierCount; i++)
                modifiers.Add(new ModifierInstance { Stat = (Stat)r.Int(), Multiply = r.Float(), Add = r.Float(), Until = r.Float() });

            statuses.Clear();
            int statusCount = r.Int();
            for (int i = 0; i < statusCount; i++)
            {
                string id = r.String();
                float until = r.Float();
                float next = r.Float();
                int stacks = r.Int();
                if (World.Content.Effects.TryGetValue(id, out var def))
                    statuses.Add(new StatusInstance { Def = def, Until = until, NextTick = next, Stacks = stacks });
            }

            if (IsPlayer)
                return;

            touchingPlayer = r.Bool();
            Animation.LoadState(r);
            foreach (var a in always)
                a.LoadState(r);
            foreach (var s in states)
            {
                foreach (var a in s.Abilities)
                    a.LoadState(r);
                foreach (var t in s.Transitions)
                    t?.LoadState(r);
            }

            if (alive && isActiveAndEnabled)
                World.Register(this);
            ApplyLook();
        }
    }
}
