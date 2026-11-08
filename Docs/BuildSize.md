# What the WebGL build weighs, and why

Measured 2026-11-08 on beta's build v17 (`sites.hawkesgen.ai/.../sick-of-it-unity/b/web/17/`, commit e454871): **3,658 files,
1,267.4 MB**. Almost all of it is Addressables content that a player only downloads when a level, film or screen needs it;
the page itself loads the player (26.5 MB) and the "core" bundles.

| Part | Files | MB | What |
|---|---:|---:|---|
| `ServerData/WebGL` (desktop, DXT) | 1,804 | 848 | 590 texture bundles = 527 MB (sheets up to 4096, uncapped), the hero's grips 98, 1,063 sound bundles 78, film and background sheets |
| `ServerData/WebGL_ASTC` (phones) | 1,814 | 365 | textures capped at 2048 = 141 MB, and **1,101 bundles byte for byte the same as the desktop's** (sound, scenes, scripts: 98 MB) |
| `SickOfIt/Build` (the player) | 4 | 26.5 | data 16.1 MB, wasm 10.3 MB (gzip, `.unityweb` with the decompression fallback) |
| `SickOfIt/StreamingAssets` | 14 | 25.8 | the local bundles (main menu, deaths, scenes) and the catalog |

(MB here are 1,000,000 bytes.)

## What the deploy does about it (2026-11-08)

- **One texture cap for both variants: 2048** (`AddressablesTextureVariants.MaxTextureSize`). Phones had it already; the
  desktop's uncapped DXT sheets were 579 MB of bundles whose ASTC twins are 130 MB. A 1080p screen shows no more of a
  2048 sheet than a phone does. Estimated saving: ~430 MB. The player build runs under the same cap (`CapTextures`), so it
  reuses the DXT pass's imports instead of importing everything again at full size.
- **Shared bundles kept once.** After both passes, every ASTC bundle that is the same file as the DXT one (same name, same
  bytes) is deleted (`DropSharedCopies`) and its name goes to `ServerData/TextureVariantShared.txt`; the WebGL player build
  carries it as `Resources/TextureVariantShared.txt` (`TextureVariantSharedList`, removed after the build, gitignored) and
  `TextureVariantRedirect` leaves those bundles at their `WebGL` address on phones. Saving: ~98 MB and ~1,100 files.
- **Player settings pinned in code** (`WebGLPlayerSettings.Apply`, logged before -> after, put back after the build so
  ProjectSettings.asset never changes): gzip with the decompression fallback, fixed file names, engine code stripping,
  data caching (a second visit reads `SickOfIt.data` from the browser's cache), not a development build. Managed stripping,
  IL2CPP code generation and exception support are logged, not changed (the JSON content types are found by reflection;
  the size-optimised code generation can cost frame time).
- **A size report** with every build: `build-size.json` at the top of the output and `Build size:` lines in the log (each
  part's files and MB, against v17's 3,658 files / 1,267.4 MB).

Expected total: about **740 MB in ~2,560 files** (-42%). The real number is the next build's `Build size:` line.

## Why gzip and not Brotli

The builds host (`sites.hawkesgen.ai`) and both game pages (Hawkes Games' `/play/sickofit`, the template page) ask for
`SickOfIt.*.unityweb`, and the host sends those with no `Content-Encoding`, so Unity's loader decompresses them in
JavaScript. Measured on v17: Brotli would make the player 21.4 MB instead of 25.2 MB, but Unity's JavaScript Brotli
decoder is several times slower than its gzip one on a phone. Brotli pays only without the fallback: `SickOfIt.*.br`
served with `Content-Encoding: br` (the host already does that for `*.br`). That needs the hosted page
(`hawkesgenwebsite/public/play/sickofit/index.html`) to ask for `.br` names first; then flip
`WebGLPlayerSettings` to Brotli with no fallback.

## Audio and the films

- Every clip is already Vorbis 0.7 in the importer (`SoundGenerator.ConfigureImport`), but WebGL ignores the format and
  encodes AAC; long clips (music, soundtracks) are Compressed In Memory, which in WebGL is an `<audio>` element that streams
  (Unity's WebGL has no Streaming load type). Sound is 78 MB in 1,063 small bundles, each loaded on first play.
- The films (Video1-4, the deaths, the main menu's backgrounds) are baked sprite sheets (Docs/VideoPlayback.md), about
  60 MB a variant. Making Video1 one small URL-streamed file means a VideoPlayer path with rewind support; not done here.

## Checking it

- Playtest `phone-load` (`SickOfItPlaytest.PhoneLoad.cs`, device "phone", ~2 min): the page's bar and the boot's bar,
  the main menu within 20 s of the page opening (`load.menuSeconds`), what was downloaded by then (`download` events,
  `load.menuMB` when the host shows sizes), 30+ fps on the menu and over 30 s of Level 1.
- The boot (`GameDefinitionBoot`) logs how long the game data and the core took and shows "Climbing out of the lava... N%"
  over its bar.

## Core packed into shared bundles (phone-load, 30 s to the menu)

Phone playtest r_362280a11baf: menu at 30.4 s, 157 files / 142 bundles before it. Every "core" asset (what the boot loads)
was its own bundle, so the phone made ~140 requests with a handshake each. `PlayerDataManifest.PackCore` now moves every
core address into `PlayerData_Assets_Core_Art<n>` / `Core_Audio<n>` groups packed together (12 MB per bundle at most); a
level that reuses a core asset shares that bundle, already loaded. Expect ~142 bundles to become a handful. The compression
settings (gzip + decompression fallback) are untouched. Measure with the `phone-load` playtest (`bundles before the menu`).

## Before the menu: only the menu (2026-11-08)

"core" used to be everything not owned by a level, video, death or sound: it carried the Shaft's ten warden and band
paintings, Yooba's four sheets, the shop's icons and the select cards' thumbnails, all before the menu. Those are now the
scope `core.late` (`PlayerDataManifest.LatePrefixes`, `.thumbnail`): packed into their own `Core Late` bundles, started
2 s after the menu is up (`GameAssets.LoadLateInBackground`, from `GameDefinitionBoot`) and awaited by anything that reads
them (`SceneTransitionManager`: `Prepared` loads, level/video select and the shop; `GameAssets.EnsureLate`). The boot's bar
now covers the game data and the menu's own art and sound. Playtest `phone-load` checks `late core behind the menu`.
Add a path prefix to `LatePrefixes` to move more out of the boot; anything the menu itself draws must stay in "core".

## Core downloads beside the game data (2026-11-08, phone-load menu at 26.4 s)

Phone playtest r_889e1179cbd5: game data 14.8 s, then core art and sound after it. The core bundles' names are in the manifest
inside GameDefinition, so they used to start only once it was in. `PackCore` now labels every boot ("core") bundle
`soi.core` (`GameAssets.CoreLabel`); `GameDefinitionBoot.PrefetchCore` initialises Addressables and downloads that label while
the game data loads, releasing the handle at once (the download is cached, so `Acquire` then loads from the cache). A build
without the label skips it. The boot logs `Core prefetch ...` lines. Compression is untouched.
Not done: no Unity build ran here, so no new seconds were measured; the next phone-load run will say. Next candidates: time
Addressables init/catalog apart from the GameDefinition bundle (the 14.8 s is both), and split the 6 MB of content/level JSON out of GameDefinition.

## Game-data step split in two (2026-11-08)

The boot now records when Addressables' init and catalog finished (`GameDefinitionBoot.InitAt`, log `Addressables init (catalog) done`)
and the phone-load playtest reports it as `load.bootCatalogSeconds` next to `load.bootDataSeconds`, so the next run says whether the
~10-15 s is the catalog or the GameDefinition bundle (6 MB of content/level JSON). Fix that part next: if the catalog, shrink it
(fewer addresses per bundle); if the bundle, split `content` and the level scripts out of GameDefinition into a lazy bundle.

## Gameplay event sounds behind the menu (2026-11-08, phone-load menu at 26.0 s)

Phone playtest: game data 14.9 s (50 bundles, 65 files), core 0.8 s, menu 26.0 s. "core" still held every sound named in audio.json
`events` (voices, pickups, rewind, banners, stings...), none of which the menu plays. `PlayerDataManifest.ScopeOf` now keeps only the
`ui_*` and `player_*` events in "core" (the menu's clicks and its own hero) and puts the rest in `core.late`, which every level load
already awaits (`SceneTransitionManager`, `GameAssets.EnsureLate`). Not measured here (no Unity/build); the next `phone-load` run says.

## GameDefinition bundle in LZMA (2026-11-08, phone-load menu at 22.4 s, game data 11.4 s)

Phone playtest: game data 11.4 s (was 9.3), 65 files / 50 bundles before the menu. The earlier moves only shifted art and sound
between scopes; the game-data step is the catalog plus the GameDefinition bundle, whose ~6 MB is content/level-script JSON.
Bundles are not gzip'd by the host (no Content-Encoding), so that bundle was downloaded at LZ4 size. `PlayerDataSplit` now packs the
`PlayerData_GameDefinition` group with LZMA (text, one bundle). Not measured here (no Unity/build): the next `phone-load` run says;
if `load.bootDataSeconds` is still over ~6 s, the catalog is the rest (see `load.bootCatalogSeconds`) and the next step is splitting
`content` out of GameDefinition into a lazy bundle.

## GameDefinition bundle back to LZ4 (2026-11-08, menu 24.7 s after the LZMA change)

The LZMA pass above made it worse: game data 11.4 -> 12.0 s, player start 4.8 -> 6.3 s, menu 24.7 s. LZMA unpacks the whole bundle on the
single WebGL thread before the first read. `PlayerData_GameDefinition` is LZ4 again. Not measured here; the next `phone-load` run says.

## What the boot really loaded: the prefabs' sheets (2026-11-08, beta web 27, menu at 21.6 s)

The earlier passes moved art between scopes blind. This one read beta's published build (`ServerData/WebGL/*.bundle`, UnityPy on
`m_Dependencies`, and `SickOfIt.data.unityweb`). Before the menu, `LoadAssetAsync<GameDefinition>` also loaded the bundles
GameDefinition *depends on*: `player.playerPrefab` (Character.prefab, its controller and clips) and every legacy
`enemyTypes[].prefab`, whose clips point at the sprite sheets. Those sheets are **uncompressed RGBA32** (1819x2048 = 14.9 MB each
in memory: the capped size is not a multiple of 4, so DXT and ASTC both leave them raw; the DXT and ASTC bundles are byte for byte
the same), so a phone fetched and uploaded about 45 MB of hero and enemy art it does not draw in the menu:

| Before the menu (web 27) | Files | MB |
|---|---:|---:|
| the hero's Idle/Walk/Fall/Jump/Fire sheets (Fall 3.7, Fire 4.8, Jump 5.0, Walk 3.9, Idle ~4) | 5 | ~21 |
| the legacy enemies' sheets (level3enemy x6 17.4, fireball 2.2, fighter x3 1.0, demon2, ...) | ~11 | ~21 |
| GameDefinition's other dependencies: prefabs, fonts, controller textures, clips | 25 | 1.1 |
| core art and sound (the menu's own) | 2 | 4.8 |
| GameDefinition | 1 | 0.5 |
| two catalogs (local + remote, same 0.9 MB file) | 2 | 1.8 |

`PlayerDataManifest` now takes those prefab references out of the split GameDefinition (`AssetManifestEntry.Prefab`,
`PrefabLoadsWithItsScope`: `player.playerPrefab` -> scope `hero`; `enemyTypes.*`, `pickupPrefab`, `platformPrefab` -> `play`, and only
when the prefab's closure holds a taken sheet or a picture >= 512 KB). `GameAssets` loads them with the scope that plays them and puts
them back into the field, like a sheet. The remaining dependencies share one group, `PlayerData_Boot` (was one bundle per folder, 25).
Expected before the menu: ~25 files / ~8 MB less ~25 bundles (about 15 files / ~7 MB of bundles). Not measured here (no Unity or
build in this sandbox): the next `phone-load` run says; it now also checks `bundles before the menu` (<= 40) and `hero art waits for a level`.

Still in the player data (`SickOfIt.data.unityweb`, 22.8 MB): the MainMenu scene's own hero carries the same five sheets as raw RGBA32
(74 MB in memory) because the scene references them directly. Next candidate: give that hero a compressed or smaller copy, or load
his sheets through `GameAssets` like the level's. Fix the non-multiple-of-4 cap too (it also leaves most DXT bundles raw: the desktop
variant is 904 MB against ASTC's 267 MB).
