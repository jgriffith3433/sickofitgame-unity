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

        readonly List<Entity> live = new List<Entity>();
        readonly List<Entity> tickList = new List<Entity>();
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

            LevelLength = setup.Script != null && setup.Script.length > 0f ? setup.Script.length : Mathf.Max(1f, setup.LevelDuration);
            if (setup.RewindBudget != null && setup.RewindBudget.enabled)
            {
                var budget = gameObject.AddComponent<SickOfIt.Core.Rewind.RewindBudget>();
                budget.Configure(setup.RewindBudget.startSeconds, setup.RewindBudget.maxSeconds, setup.RewindBudget.costPerSecond);
            }
            Upgrades = new UpgradeSystem(this, playerController, playerController != null ? playerController.GetComponentInChildren<PlayerHealth>() : null);
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

            tickList.Clear();
            tickList.AddRange(live);
            foreach (var e in tickList)
            {
                if (e != null && e.IsAlive && e.isActiveAndEnabled)
                    e.Tick(now, dt);
            }
            Scripted.Tick(now, dt);
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
            if (entity.IsPlayer || live.Contains(entity))
                return;
            int index = live.Count;
            while (index > 0 && live[index - 1].Serial > entity.Serial)
                index--;
            live.Insert(index, entity);
        }

        internal void Unregister(Entity entity)
        {
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
        }

        internal void NotifyDied(Entity entity, DeathCause cause)
        {
            if (cause == DeathCause.Damage)
            {
                AddCounter("kills", 1f);
                AddCounter("kills:" + entity.DefId, 1f);
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
            Director.SaveState(w);
            Hud.SaveState(w);
            Effects.SaveState(w);
            Stage.SaveState(w);
            Scripted.SaveState(w);
        }

        public void LoadState(StateReader r)
        {
            nextSerial = r.Int();
            inputLockedUntil = r.Float();
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
            Director.LoadState(r);
            Hud.LoadState(r);
            Effects.LoadState(r);
            Stage.LoadState(r);
            Scripted.LoadState(r);
        }
    }
}
