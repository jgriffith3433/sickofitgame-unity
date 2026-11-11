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

## The late core waits for the menu, not the boot (2026-11-08, attempt 3)

Beta 7afae6d, phone twin: menu at 20.75 s (player 4.8 + boot start + game data 9.9 + core 0.4 + the MainMenu scene's load); the same build on
desktop (the run a change's check plays: 1280x720, DXT) 24.5 s with game data 13.3 s, and 54-64 files / 39-49 bundles before the menu.
`GameDefinitionBoot` started the late core (Shaft, Yooba, shop, select cards, gameplay event sounds) with a 2 s timer *at the moment it handed the
game data over*, and the MainMenu scene then takes ~3.4 s to load, so those downloads (counted "before the menu") and their texture uploads on the
one WebGL thread ran through the menu scene's own load. `MainMenuManager.Start` now starts it (1.5 s after the menu is up, `GameAssets.LateStartedAt`);
the boot no longer does. The phone-load playtest checks `late core starts after the menu` and logs `load.lateStartAfterMenuSeconds`.
Also new: `BundleFetchLog` (in `CatalogCacheBuster`'s transform) counts the bundle URLs asked for until the menu by group name; phone-load reports it as
a `download` event ("bundle requests before the menu by group") and `load.menuBundleRequests`, because the 39-49 bundles are far more than core (2-3) +
Boot (1) + GameDefinition (1) should be: the next run names the rest.
Not measured here (no Unity or build in this sandbox). Studio's change check plays `phone-load` on desktop unless the scenario line carries a device, so
a desktop figure (DXT, slower) is what that check sees: judge the phone figure from `phone-load@local:phone`.
Next candidates, in order: whatever `bundle requests before the menu by group` shows beyond core/Boot/GameDefinition; move the MainMenu scene's hero
sheets (raw RGBA32, 22.8 MB of player data) out of the scene (needs Unity to regenerate the scene); move the 6 MB of content/level JSON out of GameDefinition.

## Video, death and level groups no longer ride with GameDefinition (2026-11-08, phone-load: 42 bundles / 142 requests before the menu)

Delaying `BundleDownloadManager` (#301) did not move the count, because nothing downloaded them on purpose: `LoadAssetAsync<GameDefinition>` loads the
bundles GameDefinition *depends on*, and it held direct references to assets that live in the clips' own Addressables groups (`Level1-3_Group`,
`Death1-3_Group`, `Video1-3_Group`): each video's and death's `audio` and `thumbnail`, and the `SpriteFramesData` of every video, death and
background layer. `PlayerDataSplit.Movable` and `PlayerDataManifest.Takeable` both skipped anything "already addressable", so those references stayed hard.
Now (deploy only, the split's copy):
- `PlayerDataManifest.Takeable(.., out foreignAddress)`: a Texture2D/Sprite/AudioClip reference to an asset in another group is emptied and put in the manifest
  under the address it already has (not moved, so its labels and addresses stay); it loads with its scope (`video/<name>`, `death/<name>`, the thumbnails in `core.late`).
- `PlayerDataSplit`: a `SpriteFramesData` in another group (a few KB, its frames load by label) is moved into the `PlayerData_Boot` group for the build, recorded in
  `Temp/GameDefinition.movedEntries.txt` and put back by `End`/`Recover`.
Not measured here (no Unity or build in this sandbox): the next `phone-load@local:phone` says. If bundles from those groups still show in
`bundle requests before the menu by group`, the next suspect is a scene (MainMenu/Boot) reference, not GameDefinition.

## The "76 playerdata requests" were one bundle; catalog and request queue (2026-11-09, beta c74a7ee, phone menu 20.47 s)

Phone playtest r_7a4d29a65c20: menu 20.47 s = player start 5.1 + game data 9.0 (catalog 2.05) + core 0.4 + the rest; `load.menuBundleRequests` 94 with
`playerdata` x76. Read from the published build (`catalog.bin` and the bundles of web/54, UnityPy), not guessed:

- Before the menu the `PlayerData_Assets_*` groups are exactly two bundles: `core_art1` (4.5 MB: the menu hero's Idle sheet, one 1819x2048
  texture and its ~90 sprite locations) and `core_audio1` (13 player/ui sounds, 0.2 MB); `core_late` ones start after the menu. The 76 was
  `InternalIdTransformFunc` running once per sprite location that depends on that bundle, and `BundleFetchLog` cut names at the first
  `_assets_`, so every `PlayerData_Assets_*` group read as "playerdata". Nothing is fetched 76 times and there is nothing left to pack together.
  `BundleFetchLog` now counts different bundles per group (`playerdata_assets_core_art1 x1 (76 calls)`).
- The player's own catalog and the remote one are the same file (same hash, `cmp` equal), yet `m_DisableCatalogUpdateOnStart` was false: init
  fetched the remote hash and the 0.9 MB remote catalog and parsed a second one before the game data could start. The catalog is already
  Addressables' binary format (`catalog.bin`, no JSON/compressed variant to switch to). `PlayerDataSplit.Begin` now sets
  `DisableCatalogUpdateOnStartup` for the player build.
- `m_maxConcurrentWebRequests` was 3 (Addressables resets the queue to it on init; `BundleDownloadManager` raises it only after the menu).
  The boot asks for 4 bundles of game data and 2 of core together, so the 4.5 MB hero sheet held a slot the game data waited on. Begin now
  sets 6 (`PlayerDataSplit.BootWebRequests`).
- Both settings are put back by `End`/`Recover` (`Temp/Addressables.bootSettings.txt`); the committed settings asset (LFS) is never left changed.
- Moves: `load.bootCatalogSeconds` and `load.bootDataSeconds` (expect about 0.5-1.5 s off the 9.0), `load.menuSeconds`. Not measured here (no Unity).
- Left: the 7 s between catalog and game data is mostly the 0.8 MB boot bundle (the legacy enemies' controllers and clips GameDefinition depends
  on) and the 6 MB of JSON being read; and `mainmenu_group` (the menu's background and explosion sheets) is fetched only as the menu scene
  loads (about 3.4 s): prefetching it beside the game data is the next step.

## MainMenu sheets prefetch beside the game data (2026-11-10)

Phone-load still spent ~3.4 s after the boot on `SceneManager.LoadScene(MainMenu)` while `mainmenu_group` (background + explosion
sheets) downloaded. `PlayerDataSplit.LabelMenuGroup` now labels every entry of `MainMenu_Group` with `soi.menu` for the player build
only (guids in `Temp/Addressables.menuLabel.txt`, cleared by `End`/`Recover`), and `GameDefinitionBoot.PrefetchCore` downloads
`soi.core` and `soi.menu` together while GameDefinition loads. The MainMenu scene then finds those bundles cached. Not measured here
(no Unity/build); the next `phone-load@local:phone` reports `load.menuSeconds`.

## Start to play inside 3 s (the menu's warm-up, the press, the glint)

- **Warm behind the menu, every visit** (`GameAssets.LoadLateInBackground`, `WarmBehindMenu`): once the menu is up the hero's and Continue's/Level 1's bundles are
  downloaded one at a time at `Application.backgroundLoadingPriority = Low` with two frames between bundles. Phones download only (cache, no heap); desktops also
  `Acquire` the hero and Continue's level (#297), so the press hands the same handles on. `GameAssets.WarmActive` is true meanwhile.
- **The press** (`MainMenuManager.TransitionThenExecute`): `GameAssets.CancelWarm()` (back to normal priority), `SceneTransitionManager.PreloadLevel(level)` starts the
  level's art during the house explosion, which is cut at `ExplodeCapSeconds` (1.4 s) so the level never waits for the whole clip.
- **Never a bare LOADING bar** (`ScreenTransition`): if assets are still coming in, the cover shows the hero walking along a track (his place is the progress) under
  "HERE WE GO AGAIN"; no digits, no "LOADING".
- **No percentage on the menu** (`BundleDownloadUI`): a small breathing ember diamond while the warm-up or a download runs, gone when it is done
  (`BundleDownloadUI.GlintVisible/GlintShownAt/GlintGoneAt`).
- **Playtest `phone-load`** step 4d: three New Game presses, each after 5 s on the menu, timed to Level 1 accepting input (`load.pressToPlaySeconds`, median checked
  <= 3 s), the menu scanned for "%"/LOADING text, the transition checked for the themed words (`check` events `phone-load press to play`, `themed transition`,
  `menu shows no percentage`). Keep core vs core.late as they are: the warm-up never touches the menu's 20 s budget (it starts after the menu is up).

## The menu hero leaves the player data (2026-11-11, phone twin: 33.5 MB before gameplay, limit 20)

Phone first-five (390x844, 4x CPU, slow-4g): `SickOfIt.data.unityweb` 22.8 MB + `SickOfIt.wasm.unityweb` 10.5 MB + framework 0.1 MB = 33.5 MB, and
167 s to gameplay (33.5 MB at slow-4g's ~1.6 Mbps is exactly that: the clock stops once the player itself is in). No Addressables bundle is in the
top five, so the earlier menu passes (core, late core, prefetch) were never the problem. The 22.8 MB is the **MainMenu scene's own hero**: MainMenu.unity is
a build-settings scene (Video, Level and Death are Addressables scenes), and its Character prefab instance references the controller, clips and five 3624x4080
hero sheets, which a scene always carries in the player: as raw RGBA32 (the 2048 cap gives 1819x2048 and 1819 is not a multiple of 4, so neither DXT nor ASTC
applies), ~74 MB in memory. Two changes, both in the deploy, nothing hand-edited:

1. **The scene's hero is taken out for the player build** (`PlayerDataSplit.StripMenuHero`, called by `Begin`; `RestoreMenuScene` in `End`/`Recover`
   copies `Temp/MainMenu.beforeSplit.unity` back byte for byte, so the committed scene still has him and the editor never changes). It opens MainMenu.unity
   through the editor API, destroys the outermost prefab instance of every `PlayerController` and saves; the log says how many and whether the scene still
   depends on `Assets/Art/Sprites/Player/*.png` (a WARNING if it does). `MainMenuManager.SpawnMenuHero` places him at runtime when the scene has none:
   *at once* a `MenuHeroStandIn` (his Idle sheet from `player.spriteSheet`, scope `core`, scaled and sorted like the prefab's Graphics child, no physics),
   then, when the scope `hero.menu` (`GameAssets.MenuHero`: only the `player.playerPrefab` entry, written by `PlayerDataManifest`; its bundle brings the
   controller, clips and the five sheets, not the hero's whole scope) is in, the real prefab at the same place, wired like `MainMenuSceneGenerator.PlaceCharacter`
   (`PlayerController.BindMenuHero`: the menu camera, `characterCanShoot`, the MobileController's sticks and input manager). The portrait camera follows whichever
   stands there. `MainMenuManager` owns the load (GameAssets owner `menuHero`, released in `OnDestroy`; a level's own load holds the same bundles).
2. **The hero sheets import at 2040 px** (`HeroSheetImport`, an `AssetPostprocessor` on `Assets/Art/Sprites/Player/`: no sheet or .meta changes, the sheets are
   LFS): 3624x4080 becomes 1812x2040, both multiples of 4, so desktop bundles get DXT and phones ASTC (about a quarter of the size; the 4-5 MB raw bundles and the
   14.9 MB RGBA32 in memory shrink) at the same on-screen size.

Expected for the phone twin: player data ~3-5 MB (what the hero's raw sheets left of the 22.8), wasm 10.5, framework 0.1: **~14-16 MB before gameplay (limit 20)**.
Not measured here (no Unity or build in this sandbox): beta's next `first-five@local:phone` says (`phone.mbBeforeGameplay`). The 20 s limit is a different matter: the
10.5 MB wasm alone is ~52 s at slow-4g, so seconds-to-gameplay needs a smaller wasm (code stripping, fewer assemblies), not this change.
Checks: `phone-load` (`hero on the menu`: a sprite drawn inside the camera's view within 20 s of the buttons; `playable hero on the menu`: the real controller is
there by the end of the warm-up; metrics `load.menuHeroSeconds`, `load.menuHeroPlayableSeconds`) and `first-five` (`heroOnMenu`), so taking him out can never
remove him unseen. If the build log says the scene still depends on hero sheets, the strip failed: read the line before it.
