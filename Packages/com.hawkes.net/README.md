# Hawkes Net for Unity WebGL

Multiplayer for Unity WebGL games on Hawkes Net (Hawkes Gen `docs/net.md`): peer to peer while a room is small, the
relay when it grows, moved between them without anyone reconnecting. Three layers; most games use the second or third.

## 1. Shared rooms: `HawkesNetLobby` (the standard way)

One shared room per area ("lobby", "level-1"), everyone in it, kept joined. It names the room for where the page runs,
spills into the next shard when a room is full, joins again by itself when the connection is lost or refused, and gives
one status for the HUD. Subscribe once: its events carry on across rejoins and shards.

```csharp
using Hawkes;

var lobby = new HawkesNetLobby(new HawkesNetOptions { project = "mygame", name = "Ada" }, "mygame.com", "www.mygame.com");
lobby.Joined += room => Debug.Log($"in {room.Name} as {room.Id} over {room.Transport}");
lobby.PeerJoined += (id, name) => Spawn(id, name);
lobby.PeerLeft += (id, why) => Despawn(id);
lobby.Message += (kind, data, from) => { if (kind == "chat") Show(from, data); };
lobby.PoseChanged += (id, pose) => Move(id, pose.position, pose.yaw, pose.extra);
lobby.StatusChanged += status => chip.text = status;     // off, connecting, online, reconnecting, offline, full
lobby.Join("level-1");

void Update() {
    lobby.Room?.Pose(player.position, player.eulerAngles.y, "{\"hat\":\"crown\"}");   // every frame is fine
    if (pressedWave) lobby.Room?.SendText("emote", "wave");
}
```

Room names (the project's test prefix `t-` keeps beta and playtests apart from live: free, out of the counts):

| Where the page runs | Room |
|---|---|
| one of the live hosts you pass | `level-1`, then `level-1.2`, `level-1.3`... when full |
| anywhere else (beta sites, localhost) | `t-level-1` |
| a playtest (`?room=<id>`: Gen's `playtest.run` with `peers`) | `t-<id>/level-1` |

`Prefix` goes before the area (a protocol version, `"p1/"`, so pages that can't talk never meet). `NameFor(area)` gives the
full name, `HawkesNet.Count(project, rooms, done)` how many are in rooms you aren't in (Gen's public counts, for a
project with its own sites).

## 2. Drop-in avatars: `HawkesNetAvatars`

A component: set `Project`, `Area`, `LiveHosts`, `Local` (your player) and `RemotePrefab`. It joins through a lobby,
sends your pose, and keeps one prefab per other player, moved smoothly a little behind their own clock (`Delay`).
`Extra` rides with your pose; read `Remotes[id].Extra` on the others.

## 3. The room: `HawkesNet.Join`

```csharp
var room = HawkesNet.Join("lobby", new HawkesNetOptions {
    ticketUrl = "https://mygame.example/ticket",   // your server: POST /v1/net/rooms/ticket on hawkesgen.com with your API key
    name = "Ada",                                  // (or project = "mygame" for a page on that project's own sites)
});
room.OnReady += r => Debug.Log($"in {r.Name} as {r.Id} over {r.Transport}");
room.On<Chat>("chat", (msg, from) => Show(room.Peers[from], msg.text));
room.Send("chat", new Chat { text = "hello" });            // JSON (a JsonUtility object or a JSON string)
room.SendText("say", "plain text arrives as it was sent");
room.OnPose += (id, pose) => MoveAvatar(id, pose.position, pose.yaw);   // or read room.Poses
```

- **Poses** are the avatar channel: the relay sends near players every bundle, far ones 5 a second and the farthest once
  a second, so a big room stays cheap. `extra` is a small JSON object that rides with the pose; a key you stop sending is
  dropped for the others too. `HawkesNetPose.at` is when it was, on the room's clock (`room.Now`).
- **Messages**: reliable ones arrive once and in order (also across reconnects and the move to the relay); an unreliable
  one is replaced by a newer one of its kind still queued. `room.Order` and `PeerOrder` are the join order (the oldest is
  the host: `room.Host`).
- **Your ticket route** receives `{ room, name }` from the page and answers with what hawkesgen.com gave it:
  `POST https://hawkesgen.com/v1/net/rooms/ticket` with `Authorization: Bearer hg_...` and `{ "room", "name", "transport", "config" }`.
  Rooms are in your own project; usage is on your account. A Hawkes game uses its project's public door instead (`project`).
- WebGL builds only: the web SDK runs in the page (loaded from `https://net.hawkesgen.com/sdk/hawkes-net@1.mjs`). In the
  Editor `Join` reports the problem `webgl-only` (a lobby's status is then `offline`): keep an Editor stand-in of your own.
- Events come on the main thread. Changed poses are read once a frame.
- Install: by path in `Packages/manifest.json`, or embed a copy in the game's `Packages/` (what a game built on the cloud
  build server does: Sick Of It, Yooba Land). Copy it again when this one changes.
