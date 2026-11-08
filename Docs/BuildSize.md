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
