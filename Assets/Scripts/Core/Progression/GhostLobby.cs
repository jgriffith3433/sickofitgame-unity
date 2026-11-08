using System.Collections;
using System.Collections.Generic;
using Hawkes;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The ghost lobby (Docs/Story.md "Other climbers"): everyone playing sits in one Hawkes Net room, and everyone on
    /// your floor climbs beside you as a faint ghost of the hero. A player sends only where his hero is drawn and which
    /// way he faces (a pose; the floor rides in z, so the relay sends other floors rarely); each ghost's Idle, Walk, Jump
    /// or Fall is read from how it moves, from player.json's own sheets. Presentation only: ghosts have no colliders,
    /// never touch the simulation and ignore rewind (they are other people, now). WebGL builds only; `?ghosts=0` turns
    /// them off. The room is Hawkes Net's standard shared room (HawkesNetLobby, Gen docs/net.md "Unity: the standard layer"):
    /// "lobby" on sickofitgame.com, "t-lobby" anywhere else (beta, previews, localhost), "t-&lt;id&gt;/lobby" in a two-player
    /// playtest (?room=), "lobby.2"... when one is full; it joins again by itself when lost or refused.
    /// The main menu is a band of its own (z = 0; a level is its number x 1000, nowhere is -1000): the menu's hero sends his
    /// meadow pose there, so everyone on the menu sees the others' ghosts wandering the meadow (each spread along it by a
    /// stable offset from its id, so idle ones don't all stand inside the hero), and the menu counts who is climbing.
    /// </summary>
    public class GhostLobby : MonoBehaviour
    {
        const string Project = "sickofit";
        const float FloorSpacing = 1000f;
        const float SnapDistance = 6f;
        /// <summary>The z band of the main menu (z / FloorSpacing): 0. A level is its number (1 and up); -1 (z = -1000) is
        /// nowhere (between screens, other menus, and what older clients send off a level), never drawn.</summary>
        const int MenuBand = 0;
        const int Nowhere = -1;
        /// <summary>Menu ghosts stand this far right of where the other hero really is, picked by id (0.9 to 4.3), so the
        /// ghosts of players idling at the menu's spawn spread over the meadow instead of standing inside the hero.</summary>
        const float MenuSpreadMin = 0.9f, MenuSpreadRange = 3.4f, MenuEdge = 0.5f;
        static readonly string[] States = { "Idle", "Walk", "Jump", "Fall" };
        static readonly Color Tint = new Color(0.72f, 0.88f, 1f, 0.4f);
        /// <summary>A player whose page is in the background (Hawkes Net's away: a hidden tab, a minimised window) is
        /// fainter still until they're back; their ghost stands where they left it.</summary>
        static readonly Color AwayTint = new Color(0.72f, 0.88f, 1f, 0.15f);

        /// <summary>Ghosts drawn now (on your floor, or on the menu's meadow).</summary>
        public static int Count { get; private set; }
        /// <summary>Other players on a level now (any floor), for the menu's "climbing now" chip.</summary>
        public static int Climbing { get; private set; }
        /// <summary>Other players on the main menu now.</summary>
        public static int OnMenu { get; private set; }
        /// <summary>In the lobby's room now (WebGL, not turned off).</summary>
        public static bool Online => instance != null && instance.lobby != null && instance.lobby.Ready;
        /// <summary>Set by MainMenuManager while the main menu is up: its hero sends the menu band.</summary>
        public static bool MenuOpen
        {
            get => menuOpen;
            // The menu is an online feature: opening it asks a halted connection to try again.
            set { menuOpen = value; if (value) Wake(); }
        }
        static bool menuOpen;
        /// <summary>"connecting", "online", "offline" (waiting to retry), "halted" (the server said stop: asked again on the
        /// next scene load or online feature), or "off" (no lobby: not WebGL, or ?ghosts=0).</summary>
        public static string Status => instance != null ? instance.status : "off";
        /// <summary>True when there is a lobby that isn't connected: online features show their quiet offline state.</summary>
        public static bool Offline => instance != null && (instance.status == "offline" || instance.status == "halted");
        /// <summary>Playtest only: pretend every connection attempt fails with this problem ("502", "410"); null = real.</summary>
        public static string ForcedFailure { get; set; }
        /// <summary>Playtest only: the server goes down now (the open connection is dropped) with this problem, or null: it is back
        /// and a waiting or halted connection tries again at once.</summary>
        public static void SimulateOutage(string problem)
        {
            ForcedFailure = problem;
            var g = instance;
            if (g == null || g.lobby == null)
                return;
            if (problem == null)
            {
                g.lastWake = -100f;
                if (g.status == "halted") Wake();
                return;
            }
            if (g.status == "online" || g.status == "connecting")
            {
                g.quiet = true;
                try { g.lobby.Leave(); }
                finally { g.quiet = false; }
                g.retry.Succeeded();
                g.Failed(problem);
            }
        }
        /// <summary>Attempts made (real or forced) and warnings logged, for the playtest.</summary>
        public static int Attempts { get; private set; }
        public static int Warnings { get; private set; }
        /// <summary>The seconds the next retry waits (0 when none is scheduled).</summary>
        public static float NextRetryIn => instance != null && instance.retryAt > 0f ? Mathf.Max(0f, instance.retryAt - Time.realtimeSinceStartup) : 0f;

        static GhostLobby instance;

        class Ghost
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Vector3 shown;
            public Vector3 velocity;
            public float animTime;
            public string state;
        }

        HawkesNetLobby lobby;
        readonly NetRetry retry = new NetRetry();
        string status = "connecting";
        float retryAt = -1f, lastWake = -100f;
        bool quiet; // our own Leave/Join: not a failure
        Coroutine timer;
        const float WakeGap = 10f;
        HawkesNetRoom room => lobby.Room;
        readonly Dictionary<string, Ghost> ghosts = new Dictionary<string, Ghost>();
        readonly List<string> drop = new List<string>();
        Dictionary<string, (List<Sprite> frames, float fps)> clips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || instance != null)
                return;
            if (HawkesNetLobby.Query("ghosts") == "0")
                return;
            var go = new GameObject("GhostLobby");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GhostLobby>();
            // A playtest on the live site still plays apart from the players (the test rooms).
            var hosts = HawkesNetLobby.Query("playtest").Length > 0 ? new string[0] : new[] { "sickofitgame.com", "www.sickofitgame.com" };
            instance.lobby = new HawkesNetLobby(new HawkesNetOptions { project = Project, name = "ghost", transport = "auto", log = false }, hosts);
            instance.lobby.Left += instance.OnLeft;
            instance.lobby.Joined += instance.OnJoined;
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
            instance.Connect();
        }

        // ---- the connection: one attempt, and what a failure means (NetRetry). Single-player never waits on any of it.

        void Connect()
        {
            timer = null;
            retryAt = -1f;
            Attempts++;
            status = "connecting";
            if (!string.IsNullOrEmpty(ForcedFailure))
            {
                Failed(ForcedFailure);
                return;
            }
            quiet = true;
            try { lobby.Join("lobby"); }
            finally { quiet = false; }
        }

        void OnJoined(HawkesNetRoom r)
        {
            retry.Succeeded();
            status = "online";
            retryAt = -1f;
        }

        void OnLeft(HawkesNetRoom r)
        {
            if (quiet || instance != this)
                return;
            string problem = lobby.LastProblem;
            if (problem.Contains("full"))
                return; // the lobby moves to the next shard by itself
            // Take the connection over from the package's own timer: this class decides when to try again.
            quiet = true;
            try { lobby.Leave(); }
            finally { quiet = false; }
            Failed(problem);
        }

        void Failed(string problem)
        {
            float wait = retry.Failed(problem);
            if (wait < 0f)
            {
                status = "halted";
                Warn("Hawkes Net: " + (problem.Length > 0 ? problem : "unreachable") + ". Playing offline; will ask again on the next scene or online screen.");
                return;
            }
            status = "offline";
            retryAt = Time.realtimeSinceStartup + wait;
            Warn("Hawkes Net: " + (problem.Length > 0 ? problem : "unreachable") + ". Playing offline; retrying in " + wait.ToString("0") + " s.");
            if (timer != null)
                StopCoroutine(timer);
            timer = StartCoroutine(RetryAfter(wait)); // a timer set by the failure, not a poll
        }

        IEnumerator RetryAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Connect();
        }

        static void Warn(string text)
        {
            Warnings++;
            Debug.LogWarning(text);
        }

        /// <summary>Ask a halted (or waiting) connection to try now: called when a scene loads or an online feature opens.
        /// Only a halted connection tries (a waiting one has its timer), at most once per 10 s.</summary>
        public static void Wake()
        {
            var g = instance;
            if (g == null || g.status != "halted" || Time.realtimeSinceStartup - g.lastWake < WakeGap)
                return;
            g.lastWake = Time.realtimeSinceStartup;
            g.retry.Succeeded();
            g.Connect();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Wake();

        void Update()
        {
            var level = LevelRuntime.Current;
            var hero = PlayerController.Active;
            var heroSprite = hero != null ? hero.HeroRenderer : null;
            bool heroShown = heroSprite != null && heroSprite.enabled && heroSprite.gameObject.activeInHierarchy;
            bool playing = level != null && heroShown;
            // A level is its number; the main menu (with its hero up) is the menu band; anything else is nowhere.
            int band = playing ? (level.LevelNumber >= 1 ? level.LevelNumber : Nowhere)
                : MenuOpen && level == null && heroShown ? MenuBand : Nowhere;

            if (lobby.Ready)
            {
                if (band != Nowhere)
                {
                    var p = heroSprite.transform.position;
                    room.Pose(new Vector3(p.x, p.y, band * FloorSpacing), hero.FacingRight ? 0f : 180f);
                }
                else
                {
                    room.Pose(new Vector3(0f, 0f, -FloorSpacing), 0f);
                }
            }
            Show(band, heroSprite);
        }

        static int BandOf(HawkesNetPose pose) => Mathf.RoundToInt(pose.position.z / FloorSpacing);

        void Show(int band, SpriteRenderer hero)
        {
            int count = 0, climbing = 0, onMenu = 0;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (lobby.Ready)
            {
                foreach (var kv in room.Poses)
                {
                    if (!room.Peers.ContainsKey(kv.Key))
                        continue;
                    int b = BandOf(kv.Value);
                    if (b >= 1)
                        climbing++;
                    else if (b == MenuBand)
                        onMenu++;
                }
            }
            if (band != Nowhere && hero != null && lobby.Ready)
            {
                var cam = band == MenuBand ? Camera.main : null;
                foreach (var kv in room.Poses)
                {
                    var pose = kv.Value;
                    if (BandOf(pose) != band)
                        continue;
                    var target = new Vector3(pose.position.x, pose.position.y, hero.transform.position.z);
                    if (band == MenuBand)
                        target.x = OnMeadow(target.x + MenuSpreadMin + MenuSpreadRange * Spread(kv.Key), cam);
                    if (!ghosts.TryGetValue(kv.Key, out var g) || g.go == null)
                        ghosts[kv.Key] = g = Make(target);
                    if (!g.go.activeSelf)
                    {
                        g.go.SetActive(true);
                        g.shown = target;
                    }
                    var before = g.shown;
                    g.shown = (target - g.shown).sqrMagnitude > SnapDistance * SnapDistance ? target : Vector3.Lerp(g.shown, target, 1f - Mathf.Exp(-14f * dt));
                    g.velocity = Vector3.Lerp(g.velocity, (g.shown - before) / dt, 0.25f);
                    Draw(g, hero, Mathf.Abs(Mathf.DeltaAngle(pose.yaw, 0f)) < 90f, dt);
                    g.sr.color = room.Away.Contains(kv.Key) ? AwayTint : Tint;
                    g.sr.sortingLayerID = hero.sortingLayerID;
                    g.sr.sortingOrder = hero.sortingOrder - 1;
                    g.go.name = "Ghost " + kv.Key;
                    count++;
                }
            }

            drop.Clear();
            foreach (var kv in ghosts)
            {
                var g = kv.Value;
                if (g.go == null || room == null || !room.Peers.ContainsKey(kv.Key))
                {
                    drop.Add(kv.Key);
                    continue;
                }
                bool here = band != Nowhere && hero != null && room.Poses.TryGetValue(kv.Key, out var pose) && BandOf(pose) == band;
                if (!here && g.go.activeSelf)
                    g.go.SetActive(false);
            }
            foreach (var id in drop)
            {
                if (ghosts[id].go != null)
                    Destroy(ghosts[id].go);
                ghosts.Remove(id);
            }
            Count = count;
            Climbing = climbing;
            OnMenu = onMenu;
        }

        /// <summary>0..1, the same for an id every frame (FNV-1a), so a menu ghost keeps its place on the meadow.</summary>
        static float Spread(string id)
        {
            uint h = 2166136261;
            foreach (char c in id)
                h = (h ^ c) * 16777619;
            return (h % 1000) / 999f;
        }

        /// <summary>Keeps a menu ghost's x inside the camera's view (the menu's width differs per screen).</summary>
        static float OnMeadow(float x, Camera cam)
        {
            if (cam == null || !cam.orthographic)
                return x;
            float half = cam.orthographicSize * cam.aspect;
            float left = cam.transform.position.x - half + MenuEdge, right = cam.transform.position.x + half - MenuEdge;
            return right > left ? Mathf.Clamp(x, left, right) : x;
        }

        Ghost Make(Vector3 at)
        {
            var go = new GameObject("Ghost");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            return new Ghost { go = go, sr = go.AddComponent<SpriteRenderer>(), shown = at, state = "Idle" };
        }

        void Draw(Ghost g, SpriteRenderer hero, bool facingRight, float dt)
        {
            g.go.transform.position = g.shown;
            // The sheets face left: facing right is a negative X scale, as on the hero (PlayerController.FacingRight).
            var size = hero.transform.lossyScale;
            g.go.transform.localScale = new Vector3(facingRight ? -Mathf.Abs(size.x) : Mathf.Abs(size.x), Mathf.Abs(size.y), 1f);

            var v = g.velocity;
            string state = Mathf.Abs(v.y) > 1.5f ? (v.y > 0f ? "Jump" : "Fall") : Mathf.Abs(v.x) > 0.6f ? "Walk" : "Idle";
            if (state != g.state)
            {
                g.state = state;
                g.animTime = 0f;
            }
            g.animTime += dt;
            var clip = Clip(state);
            if (clip.frames == null || clip.frames.Count == 0)
                clip = Clip("Idle");
            if (clip.frames == null || clip.frames.Count == 0)
            {
                g.sr.sprite = hero.sprite;
                return;
            }
            bool loops = state == "Idle" || state == "Walk";
            int i = Mathf.FloorToInt(g.animTime * clip.fps);
            g.sr.sprite = clip.frames[loops ? i % clip.frames.Count : Mathf.Min(i, clip.frames.Count - 1)];
        }

        (List<Sprite> frames, float fps) Clip(string state)
        {
            if (clips == null)
            {
                var gd = GameDefinition.Instance;
                var all = gd != null && gd.player != null && gd.player.animationConfig != null ? gd.player.animationConfig.states : null;
                if (all == null)
                    return (null, 0f);
                clips = new Dictionary<string, (List<Sprite>, float)>();
                foreach (var name in States)
                {
                    AnimationStateConfig config = null;
                    foreach (var s in all)
                    {
                        if (s != null && s.stateName == name)
                            config = s;
                    }
                    if (config == null || config.spriteSheet == null)
                        continue;
                    var kept = new List<Sprite>();
                    foreach (var sprite in gd.SpritesOf(config.spriteSheet))
                    {
                        if (sprite != null && (string.IsNullOrEmpty(config.spriteNameFilter) || sprite.name.Contains(config.spriteNameFilter)))
                            kept.Add(sprite);
                    }
                    clips[name] = (config.SelectFrames(kept), Mathf.Max(1, config.fps) * (config.speed > 0f ? config.speed : 1f));
                }
            }
            return clips.TryGetValue(state, out var c) ? c : (null, 0f);
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            quiet = true;
            lobby?.Leave();
        }
    }
}
