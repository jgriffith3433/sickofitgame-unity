# Hawkes Net for Unity WebGL

Multiplayer for Unity WebGL games on Hawkes Net (Hawkes Gen `docs/net.md`): peer to peer while a room is small, the
relay when it grows, moved between them without anyone reconnecting.

```csharp
using Hawkes;

var room = HawkesNet.Join("lobby", new HawkesNetOptions {
    ticketUrl = "https://mygame.example/ticket",   // your server: POST /v1/net/rooms/ticket on hawkesgen.com with your API key
    name = "Ada",
});
room.OnReady += r => Debug.Log($"in {r.Name} as {r.Id} over {r.Transport}");
room.On<Chat>("chat", (msg, from) => Show(room.Peers[from], msg.text));
room.Send("chat", new Chat { text = "hello" });

void Update() {
    room.Pose(player.position, player.eulerAngles.y);           // yours, every frame is fine
    foreach (var kv in room.Poses) MoveAvatar(kv.Key, kv.Value.position, kv.Value.yaw);
}
```

- **Your ticket route** receives `{ room, name }` from the page and answers with what hawkesgen.com gave it:
  `POST https://hawkesgen.com/v1/net/rooms/ticket` with `Authorization: Bearer hg_...` and `{ "room", "name", "transport", "config" }`.
  Rooms are in your own project; usage is on your account.
- WebGL builds only: the web SDK runs in the page (loaded from `https://net.hawkesgen.com/sdk/hawkes-net@1.mjs`). In the
  Editor `Join` reports the problem `webgl-only`.
- Events come on the main thread. `Poses` is refreshed once a frame.
- Install: Package Manager > Add package from disk (`package.json`), or by path in `Packages/manifest.json`.
