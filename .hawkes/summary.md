# Hawkes Net outage: degrade quietly, retry on a backoff timer

**Wrong:** the ghost lobby relied on the package's own rejoin (per-frame time check, no jitter, no stop on 410) and logged on every failure, so a 502/410 server meant endless retries and noisy console.

**Changed** (the package `com.hawkes.net` is pinned by git URL and the server is untouched):
- `Core/Progression/NetRetry.cs` (new): pure policy. 2, 4, 8 ... 60 s, +-25% jitter; 410 or the 3rd 4xx in a row (not 408/429) stops for the session.
- `Core/Progression/GhostLobby.cs`: takes over from the package's retry timer on `Left`; one short `LogWarning` per failure (no stack, no error); retry is a coroutine scheduled from the failure; `Wake()` retries a halted connection on the next scene load or when the main menu opens (max once / 10 s); reconnects by itself when the server returns. `Status`, `Offline`, `Attempts`, `Warnings`, `SimulateOutage` for the playtest. Single-player never waits on any of it.
- `MainMenuManager.cs`, `MenuStyleConfig.cs`, `ui/mainMenu.json`: quiet "Other climbers: offline" chip while offline/halted.
- Playtest `net-outage` (`SickOfItPlaytest.NetOutage.cs`, in `.hawkes/scenario.txt`): server down (502): warnings <= attempts, growing wait, chip, no errors; Level 1 played through; 410 halts; server back, online again with no reload. Connection checks run in WebGL only (no lobby elsewhere).
- `Assets/Tests/Editor/NetRetryTests.cs`; AGENTS.md bullet.

**Checked:** Unity compile and tests not run here (no editor on this machine); C# read through by hand. Scenario not played.
**Look at:** the 502 outage is simulated by a playtest hook, so the real server's 502/410 behaviour (browser-native WebSocket error lines in the console come from the browser/page SDK and can't be silenced from C#) is judged on beta. No merge conflicts.
**GPU:** none needed; estimate $0.
