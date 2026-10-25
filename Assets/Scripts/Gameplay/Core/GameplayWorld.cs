using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>What the world needs from the level scene around it.</summary>
    public sealed class WorldSetup
    {
        public GameContent Content;
        /// <summary>The level script, or null for a level without one.</summary>
        public LevelScript Script;
        public int LevelNumber;
        public string LevelName = "";
        /// <summary>The scene (content/scenes) playing instead of the level's own script, or "".</summary>
        public string SceneId = "";
        public GameObject Player;
        public Camera Camera;
        public AudioSource Music;
        public ObjectPool PickupPool;
        public ILevelHost Host;
        public Transform BackgroundRoot;
        /// <summary>The scene the world belongs to (the level's scene). Levels load additively, so the active scene can still be the menu that is about to unload.</summary>
        public UnityEngine.SceneManagement.Scene Scene;
        /// <summary>The level's duration (progress bar length when the script doesn't set one).</summary>
        public float LevelDuration = 60f;
        /// <summary>Rewind limits; null or disabled = unlimited rewind.</summary>
        public SickOfIt.Data.Config.RewindBudgetConfig RewindBudget;
    }

    /// <summary>
    /// The per-level simulation: owns every gameplay entity, the level director, counters, and signals,
    /// and ticks them all from FixedUpdate in a fixed order (director, then entities by spawn order).
    /// Its state (and the director's) is saved for rewind.
    /// </summary>
    public sealed class GameplayWorld : MonoBehaviour, ISimState
    {
        public static GameplayWorld Instance { get; private set; }

        public GameContent Content { get; private set; }
        public LevelDirector Director { get; private set; }
        public Entity Player { get; private set; }
        public GameplayHud Hud { get; private set; }
        public WorldEffects Effects { get; private set; }
        /// <summary>The floor counter, distance travelled and milestones.</summary>
        public StageProgress Stage { get; private set; }
        /// <summary>Extra pickup magnet range from shop upgrades (world units), set once at level start.</summary>
        public float PickupMagnetBonus { get; set; }

        /// <summary>The level runner (restart, leave the level); null in tests.</summary>
        public ILevelHost Host { get; private set; }
        public GameplayTelemetry Telemetry { get; private set; }
        public UpgradeSystem Upgrades { get; private set; }
        /// <summary>Hell's shortcuts offered in this level (offerShortcut) and the one taken, if any.</summary>
        public ShortcutSystem Shortcuts { get; private set; }
        /// <summary>Expected level length in seconds, for the progress bar.</summary>
        public float LevelLength { get; private set; } = 60f;
        public ObjectPool PickupPool { get; private set; }
        public Camera Camera { get; private set; }
        public uint Seed { get; private set; }
        public int LevelNumber { get; private set; }
        public string LevelName { get; private set; } = "";
        public SpriteRenderer[] PlayerRenderers { get; private set; }
        /// <summary>The hero in a scripted scene: walks and pulls (walkPlayerTo, pullPlayerInto).</summary>
        public ScriptedPlayer Scripted { get; private set; }
        /// <summary>The player's controller, or null in tests without one.</summary>
        public PlayerController PlayerController => playerController;
        /// <summary>The aim guide's lines (null when player.json turns it off).</summary>
        public AimGuide AimGuide { get; private set; }
        /// <summary>The hero's weapons (content/weapons): held, equipped, ammo, the melee swing. Null without a player.</summary>
        public PlayerWeapons Weapons { get; private set; }
        /// <summary>The visible melee swing (fists).</summary>
        public MeleeSwingFx SwingFx { get; private set; }

        readonly List<Entity> live = new List<Entity>();
        readonly List<Entity> tickList = new List<Entity>();
        // Who is in live: every entity's LoadState registers it again on each rewind frame, and searching the list was O(n) each time.
        readonly HashSet<Entity> liveSet = new HashSet<Entity>();
        readonly Dictionary<Collider2D, Entity> byCollider = new Dictionary<Collider2D, Entity>();
        readonly Dictionary<string, ObjectPool> pools = new Dictionary<string, ObjectPool>(StringComparer.Ordinal);
        readonly Dictionary<GameObject, Entity> entityOf = new Dictionary<GameObject, Entity>();
        readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> granted = new HashSet<string>(StringComparer.Ordinal);
        Transform templateRoot;
        Transform poolRoot;
        int boundaryLayer = -1;
        PlayerController playerController;
        SpriteRenderer playerBody;
        SimHistory history;
        bool started;

        // ─── Saved state ──────────────────────────────────────────────
        int nextSerial = 1;
        readonly SortedDictionary<string, float> counters = new SortedDictionary<string, float>(StringComparer.Ordinal);
        readonly HashSet<string> signalsNow = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> signalsNext = new List<string>();
        float inputLockedUntil;
        bool rewindLocked;

        public float InputLockedUntil => inputLockedUntil;
        /// <summary>A script locked rewind (lockRewind): no control can start one until unlockRewind.</summary>
        public bool RewindLocked => rewindLocked;
        /// <summary>The scene playing instead of the level's script (a scripted scene), or "".</summary>
        public string SceneId { get; private set; } = "";
        public float Now => GameClock.Instance != null ? GameClock.Instance.Time : 0f;
        public IReadOnlyList<Entity> Live => live;

        // =====================================================================
        // SETUP
        // =====================================================================

        public static GameplayWorld Create(WorldSetup setup)
        {
            var go = new GameObject("GameplayWorld");
            if (setup.Scene.IsValid() && setup.Scene.isLoaded && go.scene != setup.Scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, setup.Scene);
            var world = go.AddComponent<GameplayWorld>();
            world.Initialize(setup);
            return world;
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SickOfIt.Core.Rewind.RewindInput.Locked = false;
            }
            Telemetry?.Finish("scene unloaded");
        }

        void Initialize(WorldSetup setup)
        {
            Content = setup.Content;
            Camera = setup.Camera != null ? setup.Camera : Camera.main;
            PickupPool = setup.PickupPool;
            LevelNumber = setup.LevelNumber;
            LevelName = setup.LevelName;
            SceneId = setup.SceneId ?? "";
            boundaryLayer = LayerMask.NameToLayer("Boundary");
            Seed = (uint)(setup.Script != null && setup.Script.seed != 0 ? setup.Script.seed : 1000 + setup.LevelNumber * 7919);

            templateRoot = new GameObject("EntityTemplates").transform;
            templateRoot.SetParent(transform, false);
            templateRoot.gameObject.SetActive(false);
            poolRoot = new GameObject("EntityPools").transform;
            poolRoot.SetParent(transform, false);

            Telemetry = new GameplayTelemetry(this);
            Effects = gameObject.AddComponent<WorldEffects>();
            Effects.Init(this, setup.Music, setup.Script?.world, setup.BackgroundRoot);
            Hud = GameplayHud.Create(this);

            if (setup.Player != null)
                WrapPlayer(setup.Player);
            Scripted = new ScriptedPlayer(this, playerController);
            SickOfIt.Core.Rewind.RewindInput.Locked = false;
            if (playerController != null && setup.Script != null && !Mathf.Approximately(setup.Script.world.playerGravity, 1f))
                playerController.SetGravityMultiplier(setup.Script.world.playerGravity);

            Host = setup.Host;
            Director = new LevelDirector(this, setup.Script ?? new LevelScript(), setup.Host);
            Stage = new StageProgress(this, setup.Script);
            if (setup.Script != null)
                Effects.BuildTrack(setup.Script, Stage.Circle);
            if (setup.Script != null && string.IsNullOrEmpty(SceneId))
                Ambience = CircleAmbience.Create(this, Stage.Circle);

            LevelLength = setup.Script != null && setup.Script.length > 0f ? setup.Script.length : Mathf.Max(1f, setup.LevelDuration);
            if (setup.RewindBudget != null && setup.RewindBudget.enabled)
            {
                var budget = gameObject.AddComponent<SickOfIt.Core.Rewind.RewindBudget>();
                budget.Configure(setup.RewindBudget.startSeconds, setup.RewindBudget.maxSeconds, setup.RewindBudget.costPerSecond);
                budget.ConfigureRewards(setup.RewindBudget.survivalSeconds, setup.RewindBudget.survivalBonus);
                cleanKillRefund = setup.RewindBudget.cleanKillRefund;
                cleanKillSeconds = setup.RewindBudget.cleanKillSeconds;
            }
            Upgrades = new UpgradeSystem(this, playerController, playerController != null ? playerController.GetComponentInChildren<PlayerHealth>() : null);
            Shortcuts = new ShortcutSystem(this, playerController != null ? playerController.GetComponentInChildren<PlayerHealth>() : null);
            if (playerController != null)
            {
                Weapons = new PlayerWeapons(this, playerController);
                playerController.Armory = Weapons;
                SwingFx = MeleeSwingFx.Create(this);
            }
            Upgrades.ApplyOwned();
            Upgrades.ApplyShop();
            Hud.BuildProgressBar();

            history = gameObject.AddComponent<SimHistory>();
            history.Bind(this);
        }

        void WrapPlayer(GameObject playerObject)
        {
            playerController = playerObject.GetComponentInChildren<PlayerController>();
            var health = playerObject.GetComponentInChildren<PlayerHealth>();
            var root = playerController != null ? playerController.gameObject : playerObject;
            if (!root.TryGetComponent<Entity>(out var player))
                player = root.AddComponent<Entity>();
            Player = player;
            Player.BeginAsPlayer(this, health, playerController);

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            var list = new List<SpriteRenderer>();
            foreach (var r in renderers)
            {
                if (r.gameObject.name == "Shadow")
                    continue;
                list.Add(r);
                if (playerBody == null)
                    playerBody = r;
            }
            PlayerRenderers = list.ToArray();
            if (health != null && !root.TryGetComponent<PlayerBlink>(out _))
                root.AddComponent<PlayerBlink>().Init(health, PlayerRenderers);
            if (health != null)
                health.onDamaged.AddListener(OnPlayerDamaged);
            AimGuide = AimGuide.Create(this, playerController, health);
        }

        void OnPlayerDamaged()
        {
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;
            AddCounter("playerHits", 1f);
            Telemetry.Record("playerHit", "player", Player != null ? Player.Health : 0);
        }

        /// <summary>Starts the level script. Called once playback begins (after videos and music are ready).</summary>
        public void Begin()
        {
            if (started)
                return;
            started = true;
            Telemetry.Record("levelStart", LevelName, LevelNumber);
            Stage.Start();
            Director.Start(Now);
            history.RecordNow();
        }

        // =====================================================================
        // TICK
        // =====================================================================

        void FixedUpdate()
        {
            var clock = GameClock.Instance;
            if (!started || clock == null || clock.IsRewinding || clock.IsPaused)
                return;

            float now = clock.Time;
            float dt = clock.DeltaTime;

            signalsNow.Clear();
            foreach (var s in signalsNext)
                signalsNow.Add(s);
            signalsNext.Clear();

            Director.Tick(now);

            carryThisTick = Vector2.zero;
            heldThisTick = false;
            tickList.Clear();
            tickList.AddRange(live);
            foreach (var e in tickList)
            {
                if (e != null && e.IsAlive && e.isActiveAndEnabled)
                    e.Tick(now, dt);
            }
            if (playerController != null)
            {
                // What he stands on carries him; nothing held him this tick, so he lets go.
                playerController.CarrierVelocity = carryThisTick;
                if (!heldThisTick && playerController.IsHeld)
                    playerController.ReleaseHold();
            }
            Scripted.Tick(now, dt);
            Weapons?.Tick(now, dt);
            if (Player != null && Player.isActiveAndEnabled)
                Player.Tick(now, dt);

            Effects.Tick(now, dt);
            Stage.Tick(now);
            Hud.Tick(now);
            history.RecordNow();
        }

        // =====================================================================
        // ENTITIES
        // =====================================================================

        public Entity Spawn(string entityId, Vector2 position, SpawnOptions options)
        {
            if (string.IsNullOrEmpty(entityId) || !Content.Entities.TryGetValue(entityId, out var def))
            {
                if (warned.Add("spawn:" + entityId))
                    Debug.LogError($"[GameplayWorld] Can't spawn unknown entity \"{entityId}\".");
                return null;
            }
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return null;

            options = options ?? new SpawnOptions();
            Director?.CollectSpawnModifiers(def, options);

            var pool = PoolFor(def);
            var go = pool.Get(new Vector3(position.x, position.y, 0f), Quaternion.identity);
            if (!entityOf.TryGetValue(go, out var entity))
            {
                entity = go.GetComponent<Entity>();
                entityOf[go] = entity;
            }

            int serial = nextSerial++;
            entity.Begin(this, def, options, serial, Seed);
            // Pools put spawns at the scene root of the active scene; keep them in the level's scene.
            if (go.transform.parent == null && go.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            pool.CaptureLaunchState(go);

            AddCounter("spawned", 1f);
            AddCounter("spawned:" + def.id, 1f);
            Telemetry.Record("spawn", def.id, serial);
            // The Codex counts each creature once, even when a rewind plays its spawn again (not in a scene).
            if (def.team == Team.Enemy && !string.IsNullOrEmpty(def.displayName) && string.IsNullOrEmpty(SceneId) && TryGrantOnce("codexMet:" + serial))
                SickOfIt.Data.CodexProgress.Meet(def.displayName);
            return entity;
        }

        ObjectPool PoolFor(EntityDef def)
        {
            if (pools.TryGetValue(def.id, out var pool))
                return pool;
            var template = EntityFactory.BuildTemplate(def, Content, templateRoot);
            var poolGo = new GameObject("Pool_" + def.id);
            poolGo.transform.SetParent(poolRoot, false);
            pool = poolGo.AddComponent<ObjectPool>();
            pool.Configure(template, 4, warnWhenGrowing: false);
            pools[def.id] = pool;
            return pool;
        }

        internal void Register(Entity entity)
        {
            if (entity.IsPlayer || !liveSet.Add(entity))
                return;
            int index = live.Count;
            while (index > 0 && live[index - 1].Serial > entity.Serial)
                index--;
            live.Insert(index, entity);
        }

        internal void Unregister(Entity entity)
        {
            if (liveSet.Remove(entity))
                live.Remove(entity);
        }

        internal void RegisterColliders(Collider2D[] colliders, Entity entity)
        {
            if (colliders == null) return;
            foreach (var c in colliders)
            {
                if (c != null)
                    byCollider[c] = entity;
            }
        }

        internal void UnregisterColliders(Collider2D[] colliders)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                    byCollider.Remove(c);
            }
        }

        public bool TryGetEntity(Collider2D collider, out Entity entity)
        {
            if (collider != null && byCollider.TryGetValue(collider, out entity) && entity != null)
                return true;
            entity = null;
            return false;
        }

        // ─── Floor platforms: standing on, carried by, held by ───────

        Vector2 carryThisTick;
        bool heldThisTick;

        /// <summary>
        /// The hero stands on top of this entity's body: his feet on its top edge, inside its width, not on his way up
        /// from a jump. Floor platforms (sinking stones, rafts, lifts) use it.
        /// </summary>
        public bool PlayerStandsOn(Entity platform)
        {
            if (platform == null || Player == null || !Player.IsAlive || playerController == null || playerController.IsHeld)
                return false;
            var feet = playerController.BodyCollider;
            var body = platform.MainCollider;
            if (feet == null || body == null || !body.enabled || playerController.IsDroppingThrough(body))
                return false;
            Bounds p = feet.bounds, b = body.bounds;
            if (p.max.x < b.min.x + 0.05f || p.min.x > b.max.x - 0.05f)
                return false;
            float gap = p.min.y - b.max.y;
            if (gap < -0.25f || gap > 0.12f)
                return false;
            float rising = Player.Body != null ? Player.Body.linearVelocity.y : 0f;
            return rising < 2.5f;
        }

        /// <summary>This tick, what he stands on moves him at 'velocity' (sideways; up and down come from the collision).</summary>
        public void CarryPlayer(Vector2 velocity) => carryThisTick = velocity;

        /// <summary>This tick, something holds him at 'position' moving at 'velocity' (a chain). Jump lets go.</summary>
        public void HoldPlayer(Vector2 position, Vector2 velocity)
        {
            if (playerController == null)
                return;
            heldThisTick = true;
            playerController.Hold(position, velocity);
        }

        /// <summary>
        /// This tick, a ladder holds him at 'position' moving at 'velocity' (up or down the rungs). The climb poses play, his guns
        /// fire at 'fireScale' of their rate, and Jump lets go sideways at 'jumpOffX'.
        /// </summary>
        public void HoldPlayerClimbing(Vector2 position, Vector2 velocity, float jumpOffX, float fireScale)
        {
            if (playerController == null)
                return;
            heldThisTick = true;
            playerController.HoldClimbing(position, velocity, jumpOffX, fireScale);
        }

        /// <summary>The live entity spawned with this instance id, or null.</summary>
        public Entity FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var e in live)
            {
                if (e.IsAlive && e.InstanceId == id)
                    return e;
            }
            return null;
        }

        public bool IsBoundary(Collider2D collider) => collider != null && collider.gameObject.layer == boundaryLayer;

        public Entity FindBySerial(int serial)
        {
            if (serial == 0)
                return Player;
            foreach (var e in live)
            {
                if (e.Serial == serial)
                    return e;
            }
            foreach (var pair in entityOf)
            {
                if (pair.Value != null && pair.Value.Serial == serial)
                    return pair.Value;
            }
            return null;
        }

        // ─── Selection ────────────────────────────────────────────────

        public static bool Matches(Entity e, TargetSelector selector, Entity self)
        {
            if (e == null) return false;
            if (selector.player) return e.IsPlayer;
            if (selector.self) return e == self;
            if (!string.IsNullOrEmpty(selector.id)) return e.InstanceId == selector.id;
            if (!string.IsNullOrEmpty(selector.tag)) return e.Tags.Contains(selector.tag);
            if (!string.IsNullOrEmpty(selector.entity)) return e.DefId == selector.entity;
            return selector.all && !e.IsPlayer;
        }

        public void ForEachTarget(TargetSelector selector, Entity self, Action<Entity> action)
        {
            if (selector == null)
                return;
            if (selector.player)
            {
                if (Player != null) action(Player);
                return;
            }
            if (selector.self)
            {
                if (self != null) action(self);
                return;
            }
            var query = new List<Entity>();
            foreach (var e in live)
            {
                if (e.IsAlive && Matches(e, selector, self))
                    query.Add(e);
            }
            var targets = query.ToArray();
            foreach (var e in targets)
                action(e);
        }

        public bool AnyMatches(TargetSelector selector, Entity self, Func<Entity, bool> predicate)
        {
            if (selector.player) return Player != null && predicate(Player);
            if (selector.self) return self != null && predicate(self);
            foreach (var e in live)
            {
                if (Matches(e, selector, self) && predicate(e))
                    return true;
            }
            return false;
        }

        public bool AnyAlive(TargetSelector selector, Entity self) => AnyMatches(selector, self, e => e.IsAlive);

        public int CountAlive(TargetSelector selector, Entity self)
        {
            int n = 0;
            foreach (var e in live)
            {
                if (e.IsAlive && Matches(e, selector, self))
                    n++;
            }
            return n;
        }

        public void ForEachWithin(Vector2 center, float radius, Action<Entity> action)
        {
            var query = new List<Entity>();
            foreach (var e in live)
            {
                if (e.IsAlive && Vector2.Distance(center, e.Position) <= radius + e.Radius)
                    query.Add(e);
            }
            var targets = query.ToArray();
            foreach (var e in targets)
                action(e);
            if (Player != null && Player.IsAlive && Vector2.Distance(center, Player.Position) <= radius + Player.Radius)
                action(Player);
        }

        public void ForEachHostileWithin(Entity source, Vector2 center, float radius, Action<Entity> action)
        {
            ForEachWithin(center, radius, e =>
            {
                if (e != source && source.IsHostileTo(e))
                    action(e);
            });
        }

        // ─── Notifications from entities ──────────────────────────────

        internal void NotifyDamaged(Entity entity, int amount, Entity source, string label = null)
        {
            Telemetry.Record("damage", entity.DefId, amount, source != null ? source.DefId : label ?? "");
            NoteHurt(entity);
            if (entity.IsPlayer)
                lastHeroHurtAt = Now;
        }

        // Rewind juice paid for a clean kill: no hurt on the hero for cleanKillSeconds. lastHeroHurtAt is saved, so a rewind forgets a hurt that hasn't happened yet.
        float cleanKillRefund;
        float cleanKillSeconds = 3f;
        float lastHeroHurtAt = -999f;

        void PayCleanKill(Entity entity)
        {
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            var def = entity.Def;
            if (budget == null || cleanKillRefund <= 0f || def == null || def.team != Team.Enemy || entity.IsPlayer)
                return;
            if (entity.FindAbility<ProjectileAbility>() != null || Now - lastHeroHurtAt < cleanKillSeconds)
                return;
            // Once per creature, even when a rewind replays the kill.
            if (TryGrantOnce("cleanKill:" + entity.Serial))
                budget.Reward(cleanKillRefund, "CLEAN KILL");
        }

        // ─── What bystanders notice (the witness trigger) ─────────────

        /// <summary>A hurt remembered for a moment, so an entity ticking just after it can still notice. Saved: a rewind forgets what hasn't happened yet.</summary>
        struct WitnessEvent
        {
            public float Time;
            public Vector2 At;
            public int Team;
            public int Serial;
            public bool Hero;
        }

        const int MaxWitnessEvents = 16;
        const float WitnessKeepSeconds = 2f;
        readonly List<WitnessEvent> witnessEvents = new List<WitnessEvent>();

        void NoteHurt(Entity entity)
        {
            float now = Now;
            for (int i = witnessEvents.Count - 1; i >= 0; i--)
            {
                if (now - witnessEvents[i].Time > WitnessKeepSeconds)
                    witnessEvents.RemoveAt(i);
            }
            if (witnessEvents.Count >= MaxWitnessEvents)
                witnessEvents.RemoveAt(0);
            witnessEvents.Add(new WitnessEvent { Time = now, At = entity.Position, Team = (int)entity.Team, Serial = entity.Serial, Hero = entity.IsPlayer });
        }

        /// <summary>
        /// Did <paramref name="watcher"/> just notice <paramref name="kind"/> within <paramref name="within"/> of itself? A hurt counts for
        /// <paramref name="window"/> seconds; a shot counts while it is that close. <paramref name="at"/> is where it happened.
        /// </summary>
        public bool TryWitness(WitnessKind kind, Entity watcher, float within, float window, out Vector2 at)
        {
            at = default;
            if (watcher == null || !watcher.IsAlive)
                return false;
            Vector2 here = watcher.Position;
            float limit = within * within;
            if (kind == WitnessKind.ShotPassed)
                return TryShotNear(watcher, here, limit, out at);

            float now = Now;
            for (int i = witnessEvents.Count - 1; i >= 0; i--)
            {
                var e = witnessEvents[i];
                if (now - e.Time > window || e.Time > now + 0.001f)
                    continue;
                bool match = kind == WitnessKind.HeroHurt ? e.Hero : !e.Hero && e.Team == (int)watcher.Team && e.Serial != watcher.Serial;
                if (match && (e.At - here).sqrMagnitude <= limit)
                {
                    at = e.At;
                    return true;
                }
            }
            return false;
        }

        bool TryShotNear(Entity watcher, Vector2 here, float limit, out Vector2 at)
        {
            at = default;
            // The hero's shots are pooled objects (legacy prefabs); he never shoots his own side.
            var pool = playerController != null ? playerController.ShotPool : null;
            if (pool != null && watcher.Team != Team.Player)
            {
                var shots = pool.ActiveObjects;
                for (int i = 0; i < shots.Count; i++)
                {
                    var shot = shots[i];
                    if (shot == null || !shot.activeInHierarchy)
                        continue;
                    Vector2 p = shot.transform.position;
                    if ((p - here).sqrMagnitude <= limit)
                    {
                        at = p;
                        return true;
                    }
                }
            }
            // Projectile entities (content/entities) of any team but the watcher's own.
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                if (e == null || e == watcher || e.IsPlayer || !e.IsAlive || e.Team == watcher.Team || e.FindAbility<ProjectileAbility>() == null)
                    continue;
                if ((e.Position - here).sqrMagnitude <= limit)
                {
                    at = e.Position;
                    return true;
                }
            }
            return false;
        }

        internal void NotifyDied(Entity entity, DeathCause cause)
        {
            if (cause == DeathCause.Damage)
            {
                AddCounter("kills", 1f);
                AddCounter("kills:" + entity.DefId, 1f);
                PayCleanKill(entity);
                var killedDef = entity.Def;
                if (killedDef != null && killedDef.team == Team.Enemy && !string.IsNullOrEmpty(killedDef.displayName) && string.IsNullOrEmpty(SceneId)
                    && TryGrantOnce("codexKill:" + entity.Serial))
                    SickOfIt.Data.CodexProgress.Kill(killedDef.displayName);
            }
            Hud.OnEntityDied(entity);
            Telemetry.Record("death", entity.DefId, entity.Serial, cause.ToString());
        }

        internal void NotifyStatus(Entity entity, string effect)
        {
            Telemetry.Record("status", entity.DefId, entity.Serial, effect);
        }

        internal void DropPoints(Vector2 at, int value)
        {
            if (PickupPool == null)
                return;
            var drop = PickupPool.Get(new Vector3(at.x, at.y, 0f), Quaternion.identity);
            if (drop.TryGetComponent<PointsPickup>(out var pickup))
            {
                pickup.SetPointsValue(value);
                pickup.SetSettledY(at.y);
            }
        }

        // =====================================================================
        // ACTIONS, COUNTERS, SIGNALS
        // =====================================================================

        public void RunActions(List<GameAction> actions, ActionContext context)
        {
            if (actions == null)
                return;
            foreach (var action in actions)
                RunAction(action, context);
        }

        /// <summary>Runs one action. A failing action is logged and recorded; it doesn't stop the level.</summary>
        public void RunAction(GameAction action, ActionContext context)
        {
            if (action == null || action is WaitAction)
                return;
            try
            {
                action.Run(context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Telemetry.Record("error", action.GetType().Name, 0, e.Message);
            }
        }

        public float GetCounter(string name) => counters.TryGetValue(name, out float v) ? v : 0f;

        public void SetCounter(string name, float value)
        {
            counters[name] = value;
        }

        public void AddCounter(string name, float amount) => SetCounter(name, GetCounter(name) + amount);

        public bool HasSignal(string name) => signalsNow.Contains(name);

        public void Emit(string signal)
        {
            signalsNext.Add(signal);
            Telemetry.Record("signal", signal, 0);
        }

        /// <summary>
        /// True the first time it is called with <paramref name="key"/> in this play of the level. Not rewound, so a reward
        /// (pickup, upgrade offer) replayed by a rewind is not paid twice.
        /// </summary>
        public bool TryGrantOnce(string key) => granted.Add(key);

        public void SetInputLocked(float until)
        {
            inputLockedUntil = until;
        }

        public void SetRewindLocked(bool locked)
        {
            if (rewindLocked != locked)
                Telemetry.Record("cutscene", locked ? "lockRewind" : "unlockRewind", 0);
            rewindLocked = locked;
            SickOfIt.Core.Rewind.RewindInput.Locked = locked;
        }

        /// <summary>Plays a named state of the player's Animator (playPlayerAnimation, or playAnimation on the player).</summary>
        public void PlayPlayerAnimation(string state, float crossFade = 0f)
        {
            if (playerController == null)
                return;
            if (playerController.PlayAnimatorState(state, crossFade))
                Telemetry.Record("cutscene", "playPlayerAnimation", 0, state);
            else if (warned.Add("playerAnim:" + state))
                Debug.LogWarning($"[GameplayWorld] The player's Animator has no state \"{state}\" (player.json animationConfig).");
        }

        // =====================================================================
        // GEOMETRY
        // =====================================================================

        /// <summary>The Circle's weather, depth layers and distant life (null without a Circle or without any).</summary>
        public CircleAmbience Ambience { get; private set; }

        public Vector2 CameraCenter => Camera != null ? (Vector2)Effects.CameraBase : Vector2.zero;

        /// <summary>The visible world rectangle.</summary>
        public Rect View
        {
            get
            {
                if (Camera == null || !Camera.orthographic)
                    return new Rect(-7f, -4f, 14f, 8f);
                float h = Camera.orthographicSize;
                float w = h * Camera.aspect;
                Vector2 c = CameraCenter;
                return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
            }
        }

        /// <summary>The hero's velocity, for aiming that leads him.</summary>
        public Vector2 PlayerVelocity => Player != null && Player.Body != null ? Player.Body.linearVelocity : Vector2.zero;

        /// <summary>Where shots aimed at the player should go: the upper body, not the feet.</summary>
        public Vector2 PlayerAimPoint
        {
            get
            {
                if (Player == null)
                    return Vector2.zero;
                if (playerBody != null)
                {
                    Bounds b = playerBody.bounds;
                    return new Vector2(b.center.x, b.center.y + b.extents.y * 0.35f);
                }
                return Player.Position;
            }
        }

        // =====================================================================
        // REWIND
        // =====================================================================

        public void SaveState(StateWriter w)
        {
            w.Int(nextSerial);
            w.Float(inputLockedUntil);
            w.Float(lastHeroHurtAt);
            w.Bool(rewindLocked);
            w.Int(counters.Count);
            foreach (var pair in counters)
            {
                w.String(pair.Key);
                w.Float(pair.Value);
            }
            w.Int(signalsNext.Count);
            foreach (var s in signalsNext)
                w.String(s);
            w.Int(witnessEvents.Count);
            foreach (var e in witnessEvents)
            {
                w.Float(e.Time);
                w.Float(e.At.x);
                w.Float(e.At.y);
                w.Int(e.Team);
                w.Int(e.Serial);
                w.Bool(e.Hero);
            }
            Director.SaveState(w);
            Hud.SaveState(w);
            Effects.SaveState(w);
            Stage.SaveState(w);
            Scripted.SaveState(w);
            Weapons?.SaveState(w);
        }

        public void LoadState(StateReader r)
        {
            nextSerial = r.Int();
            inputLockedUntil = r.Float();
            lastHeroHurtAt = r.Float();
            rewindLocked = r.Bool();
            SickOfIt.Core.Rewind.RewindInput.Locked = rewindLocked;
            counters.Clear();
            int counterCount = r.Int();
            for (int i = 0; i < counterCount; i++)
                counters[r.String()] = r.Float();
            signalsNext.Clear();
            int signalCount = r.Int();
            for (int i = 0; i < signalCount; i++)
                signalsNext.Add(r.String());
            witnessEvents.Clear();
            int witnessCount = r.Int();
            for (int i = 0; i < witnessCount; i++)
            {
                witnessEvents.Add(new WitnessEvent
                {
                    Time = r.Float(), At = new Vector2(r.Float(), r.Float()), Team = r.Int(), Serial = r.Int(), Hero = r.Bool(),
                });
            }
            Director.LoadState(r);
            Hud.LoadState(r);
            Effects.LoadState(r);
            Stage.LoadState(r);
            Scripted.LoadState(r);
            Weapons?.LoadState(r);
        }

        /// <summary>Saves the world's state at this moment (after a discrete event such as a swing or a weapon change), so a rewind to just after can't undo it.</summary>
        public void RecordNow() => history?.RecordNow();
    }
}
