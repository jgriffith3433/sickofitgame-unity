using System.Collections.Generic;
using Hawkes;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using UnityEngine;

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
    /// </summary>
    public class GhostLobby : MonoBehaviour
    {
        const string Project = "sickofit";
        const float FloorSpacing = 1000f;
        const float SnapDistance = 6f;
        static readonly string[] States = { "Idle", "Walk", "Jump", "Fall" };
        static readonly Color Tint = new Color(0.72f, 0.88f, 1f, 0.4f);

        /// <summary>Ghosts on your floor now.</summary>
        public static int Count { get; private set; }
        /// <summary>The lobby's status ("connecting", "online", "offline", "full"...), or "off".</summary>
        public static string Status => instance != null ? instance.lobby.Status : "off";

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
            instance.lobby.Join("lobby");
        }

        void Update()
        {
            var level = LevelRuntime.Current;
            var hero = PlayerController.Active;
            var heroSprite = hero != null ? hero.HeroRenderer : null;
            bool playing = level != null && heroSprite != null && heroSprite.enabled && heroSprite.gameObject.activeInHierarchy;
            int floor = playing ? level.LevelNumber : 0;

            if (lobby.Ready)
            {
                if (playing)
                {
                    var p = heroSprite.transform.position;
                    room.Pose(new Vector3(p.x, p.y, floor * FloorSpacing), hero.FacingRight ? 0f : 180f);
                }
                else
                {
                    room.Pose(new Vector3(0f, 0f, -FloorSpacing), 0f);
                }
            }
            Show(playing ? floor : 0, heroSprite);
        }

        void Show(int floor, SpriteRenderer hero)
        {
            int count = 0;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (floor > 0 && lobby.Ready)
            {
                foreach (var kv in room.Poses)
                {
                    var pose = kv.Value;
                    if (Mathf.RoundToInt(pose.position.z / FloorSpacing) != floor)
                        continue;
                    var target = new Vector3(pose.position.x, pose.position.y, hero.transform.position.z);
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
                    g.sr.color = Tint;
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
                bool here = floor > 0 && room.Poses.TryGetValue(kv.Key, out var pose) && Mathf.RoundToInt(pose.position.z / FloorSpacing) == floor;
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
            lobby?.Leave();
        }
    }
}
