using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hawkes
{
    /// <summary>How to join a room. Give either TicketUrl (your server asks hawkesgen.com for tickets with your API key:
    /// POST /v1/net/rooms/ticket) or Project (a Hawkes Net project whose sites may ask without a key).</summary>
    [Serializable]
    public class HawkesNetOptions
    {
        /// <summary>Your server's ticket route. The page POSTs { room, name } to it and expects the answer it got from
        /// hawkesgen.com back as JSON.</summary>
        public string ticketUrl = "";
        /// <summary>Send cookies with the ticket request (your ticket route is on another site that signs players in).</summary>
        public bool ticketCredentials;
        /// <summary>A Hawkes Net project that hands out tickets to pages on its own sites (no key).</summary>
        public string project = "";
        public string name = "";
        /// <summary>"auto" (peer to peer while small, then the relay), "p2p" or "relay".</summary>
        public string transport = "auto";
        /// <summary>"player" or "spectator".</summary>
        public string role = "player";
        /// <summary>The Hawkes Net service, when not https://net.hawkesgen.com.</summary>
        public string endpoint = "";
        /// <summary>Where the SDK is loaded from.</summary>
        public string sdkUrl = "https://net.hawkesgen.com/sdk/hawkes-net@1.mjs";
        /// <summary>Leave the room when the tab is closed or hidden for long (the web default). Off for games that
        /// keep playing in the background.</summary>
        public bool leaveOnHide = true;
        public bool log = true;
    }

    /// <summary>A peer's avatar: where it is and which way it faces (yaw in degrees around Y, as in Unity).</summary>
    public struct HawkesNetPose
    {
        public Vector3 position;
        public float yaw;
    }

    /// <summary>A room you joined. Events arrive on the main thread, during Update.</summary>
    public class HawkesNetRoom
    {
        internal readonly int handle;
        internal HawkesNetRoom(int handle, string name) { this.handle = handle; Name = name; }

        public string Name { get; }
        /// <summary>Your peer id in the room (once Ready).</summary>
        public string Id { get; internal set; } = "";
        public string Host { get; internal set; } = "";
        public bool IsHost => Id != "" && Id == Host;
        /// <summary>"p2p" or "relay".</summary>
        public string Transport { get; internal set; } = "";
        /// <summary>"connecting", "online", "reconnecting", "closed".</summary>
        public string Status { get; internal set; } = "connecting";
        public bool Ready { get; internal set; }
        /// <summary>Everyone else in the room: peer id to name.</summary>
        public readonly Dictionary<string, string> Peers = new Dictionary<string, string>();
        /// <summary>Everyone else's newest avatar pose.</summary>
        public readonly Dictionary<string, HawkesNetPose> Poses = new Dictionary<string, HawkesNetPose>();

        public event Action<HawkesNetRoom> OnReady;
        /// <summary>A peer arrived (id, name).</summary>
        public event Action<string, string> OnPeerJoin;
        /// <summary>A peer left (id, reason).</summary>
        public event Action<string, string> OnPeerLeave;
        /// <summary>The host changed (new host id).</summary>
        public event Action<string> OnHost;
        /// <summary>The room moved between peer to peer and the relay (new transport, why). Nothing to do: it is told.</summary>
        public event Action<string, string> OnTransport;
        /// <summary>A problem (code, detail): "full", "capped", "relay-lost", "ticket"... The join failed if Ready is false.</summary>
        public event Action<string, string> OnProblem;
        /// <summary>Any message (kind, JSON data, from peer id).</summary>
        public event Action<string, string, string> OnMessage;

        readonly Dictionary<string, Action<string, string>> handlers = new Dictionary<string, Action<string, string>>();

        /// <summary>Messages of one kind: (JSON data, from peer id). Parse the data with JsonUtility.</summary>
        public void On(string kind, Action<string, string> handler)
        {
            handlers.TryGetValue(kind, out var h);
            handlers[kind] = h + handler;
        }

        /// <summary>Messages of one kind as a JsonUtility type.</summary>
        public void On<T>(string kind, Action<T, string> handler)
        {
            On(kind, (json, from) =>
            {
                T v;
                try { v = JsonUtility.FromJson<T>(json); }
                catch (Exception e) { Debug.LogWarning("[HawkesNet] " + kind + ": " + e.Message); return; }
                handler(v, from);
            });
        }

        /// <summary>Send to everyone. reliable: once and in order (default); false: may drop, for things sent often.
        /// to: one peer id. data: an object for JsonUtility, a JSON string, or null.</summary>
        public bool Send(string kind, object data = null, bool reliable = true, string to = "")
        {
            string json = data == null ? "" : data is string s ? s : JsonUtility.ToJson(data);
            return HawkesNet.Send(handle, kind, json, reliable, to ?? "");
        }

        /// <summary>Your avatar, as often as you like: the newest is sent about 15 times a second. Yaw in degrees.</summary>
        public void Pose(Vector3 position, float yaw) => HawkesNet.Pose(handle, position, yaw);

        /// <summary>The room's clock (milliseconds, the same for everyone in it).</summary>
        public double Now => HawkesNet.Now(handle);

        /// <summary>Ask the room to move to the relay now.</summary>
        public void Migrate() => HawkesNet.Migrate(handle);

        public void Leave() => HawkesNet.Leave(this);

        internal void Dispatch(HawkesNet.Event e)
        {
            switch (e.e)
            {
                case "ready":
                    Id = e.peer; Transport = e.transport; Host = e.host; Ready = true; Status = "online";
                    foreach (var p in HawkesNet.ParsePeers(e.data)) Peers[p.peer] = p.name;
                    OnReady?.Invoke(this);
                    break;
                case "join":
                    Peers[e.peer] = e.name;
                    OnPeerJoin?.Invoke(e.peer, e.name);
                    break;
                case "leave":
                    Peers.Remove(e.peer); Poses.Remove(e.peer);
                    OnPeerLeave?.Invoke(e.peer, e.reason);
                    break;
                case "host":
                    Host = e.peer;
                    OnHost?.Invoke(e.peer);
                    break;
                case "transport":
                    Transport = e.transport;
                    OnTransport?.Invoke(e.transport, e.reason);
                    break;
                case "status":
                    Status = e.status;
                    if (!string.IsNullOrEmpty(e.transport)) Transport = e.transport;
                    break;
                case "problem":
                case "error":
                    if (e.e == "error") Status = "closed";
                    OnProblem?.Invoke(e.reason, e.data);
                    break;
                case "msg":
                    OnMessage?.Invoke(e.kind, e.data, e.peer);
                    if (handlers.TryGetValue(e.kind, out var h)) h?.Invoke(e.data, e.peer);
                    break;
            }
        }

        internal void ReadPoses(string s)
        {
            Poses.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var row in s.Split(';'))
            {
                var f = row.Split(',');
                if (f.Length < 5) continue;
                Poses[f[0]] = new HawkesNetPose
                {
                    position = new Vector3(F(f[1]), F(f[2]), F(f[3])),
                    yaw = F(f[4]) * Mathf.Rad2Deg,
                };
            }
        }

        static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }

    /// <summary>
    /// Hawkes Net multiplayer for Unity WebGL (Hawkes Gen docs/net.md): the web SDK runs in the page and this is its C#
    /// face. HawkesNet.Join("lobby", new HawkesNetOptions { ticketUrl = "https://mygame.example/ticket", name = "Ada" })
    /// gives a room; handle OnReady, On("chat", ...), read Poses each frame, call Pose(...) with yours. Outside a WebGL
    /// build (the Editor included) Join reports a "webgl-only" problem and nothing is sent.
    /// </summary>
    public static class HawkesNet
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HawkesNet_Join(int handle, string room, string optionsJson, string receiver);
        [DllImport("__Internal")] static extern int HawkesNet_Send(int handle, string kind, string dataJson, bool reliable, string to);
        [DllImport("__Internal")] static extern void HawkesNet_Pose(int handle, float x, float y, float z, float yaw);
        [DllImport("__Internal")] static extern string HawkesNet_Poses(int handle);
        [DllImport("__Internal")] static extern double HawkesNet_Now(int handle);
        [DllImport("__Internal")] static extern void HawkesNet_Migrate(int handle);
        [DllImport("__Internal")] static extern void HawkesNet_Leave(int handle);
        static readonly bool Live = true;
#else
        static void HawkesNet_Join(int handle, string room, string optionsJson, string receiver) { }
        static int HawkesNet_Send(int handle, string kind, string dataJson, bool reliable, string to) => 0;
        static void HawkesNet_Pose(int handle, float x, float y, float z, float yaw) { }
        static string HawkesNet_Poses(int handle) => null;
        static double HawkesNet_Now(int handle) => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        static void HawkesNet_Migrate(int handle) { }
        static void HawkesNet_Leave(int handle) { }
        static readonly bool Live = false;
#endif

        const string ReceiverName = "HawkesNet";
        static readonly Dictionary<int, HawkesNetRoom> rooms = new Dictionary<int, HawkesNetRoom>();
        static int next;
        static Receiver receiver;

        /// <summary>Join a room (1 to 64 of letters, digits and / _ . -). The room is ready when OnReady fires.</summary>
        public static HawkesNetRoom Join(string room, HawkesNetOptions options)
        {
            options = options ?? new HawkesNetOptions();
            Ensure();
            var r = new HawkesNetRoom(++next, room);
            rooms[r.handle] = r;
            if (!Live)
            {
                receiver.Later(r, new Event { e = "error", reason = "webgl-only", data = "Hawkes Net runs in WebGL builds (the web SDK in the page)" });
                return r;
            }
            HawkesNet_Join(r.handle, room, JsonUtility.ToJson(options), ReceiverName);
            return r;
        }

        internal static bool Send(int h, string kind, string json, bool reliable, string to) => Live && HawkesNet_Send(h, kind, json, reliable, to) == 1;
        internal static void Pose(int h, Vector3 p, float yaw) { if (Live) HawkesNet_Pose(h, p.x, p.y, p.z, yaw * Mathf.Deg2Rad); } // radians on the wire
        internal static double Now(int h) => HawkesNet_Now(h);
        internal static void Migrate(int h) { if (Live) HawkesNet_Migrate(h); }
        internal static void Leave(HawkesNetRoom r)
        {
            rooms.Remove(r.handle);
            if (Live) HawkesNet_Leave(r.handle);
            r.Status = "closed";
        }

        static void Ensure()
        {
            if (receiver) return;
            var go = new GameObject(ReceiverName);
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            receiver = go.AddComponent<Receiver>();
        }

#pragma warning disable 0649 // filled by JsonUtility
        [Serializable]
        internal class Event
        {
            public int h;
            public string e = "", kind = "", data = "", peer = "", name = "", reason = "", transport = "", host = "", status = "";
            public bool reliable;
        }

        [Serializable] internal class PeerInfo { public string peer = "", name = ""; }
        [Serializable] class PeerList { public PeerInfo[] items; }
#pragma warning restore 0649

        internal static PeerInfo[] ParsePeers(string json)
        {
            if (string.IsNullOrEmpty(json)) return new PeerInfo[0];
            try { return JsonUtility.FromJson<PeerList>("{\"items\":" + json + "}").items ?? new PeerInfo[0]; }
            catch { return new PeerInfo[0]; }
        }

        /// <summary>Receives the page's SendMessage calls and reads the poses once a frame.</summary>
        internal class Receiver : MonoBehaviour
        {
            readonly List<KeyValuePair<HawkesNetRoom, Event>> later = new List<KeyValuePair<HawkesNetRoom, Event>>();
            internal void Later(HawkesNetRoom r, Event e) => later.Add(new KeyValuePair<HawkesNetRoom, Event>(r, e));

            // Called from the page: SendMessage('HawkesNet', 'OnNet', json).
            void OnNet(string json)
            {
                Event e;
                try { e = JsonUtility.FromJson<Event>(json); }
                catch (Exception x) { Debug.LogWarning("[HawkesNet] " + x.Message); return; }
                if (e == null || !rooms.TryGetValue(e.h, out var r)) return;
                try { r.Dispatch(e); }
                catch (Exception x) { Debug.LogException(x); }
            }

            void Update()
            {
                if (later.Count > 0)
                {
                    var now = later.ToArray();
                    later.Clear();
                    foreach (var kv in now)
                    {
                        try { kv.Key.Dispatch(kv.Value); }
                        catch (Exception x) { Debug.LogException(x); }
                    }
                }
                if (!Live) return;
                foreach (var r in rooms.Values)
                {
                    var s = HawkesNet_Poses(r.handle);
                    if (s != null) r.ReadPoses(s);
                }
            }
        }
    }
}
