# Lighter, faster load on phones (first part)

**What makes the build 1.27 GB.** I measured beta's build v17 file by file (3,658 files, 1,267.4 MB):
- `ServerData/WebGL` (desktop): 1,804 files, 848 MB. 527 MB of it is texture bundles at full size (sheets up to 4096). Phones already capped theirs at 2048.
- `ServerData/WebGL_ASTC` (phones): 1,814 files, 365 MB. 1,101 of those files (98 MB: every sound, the scenes, the scripts) were exact copies of the desktop ones.
- The player itself is small: 26.5 MB (data 16.1, wasm 10.3), already gzip with the decompression fallback. The local bundles add 25.8 MB.
Players never download all of it. Each bundle loads when a level, film or screen needs it.

**What I changed.**
- **One texture cap for both:** desktop textures are now at most 2048, like the phones' (`AddressablesTextureVariants.MaxTextureSize`). Estimated saving: ~430 MB. The player build keeps the same cap (`CapTextures`), so it doesn't import every texture again.
- **Shared bundles stored once:** after both passes, the phone folder drops every bundle that is byte for byte the same as the desktop one. The player carries their names (`TextureVariantSharedList` puts a list in Resources during the build, then removes it), and `TextureVariantRedirect` loads those from `WebGL` on phones. Saves 98 MB and 1,101 files.
- **Player settings pinned in code** (`WebGLPlayerSettings`): gzip with the decompression fallback, fixed file names, engine code stripping, data caching (a second visit reads the data file from the browser cache), not a development build. Each value is logged before and after, and the project's own values are put back after the build, so ProjectSettings.asset (LFS) isn't touched.
- **Why gzip and not Brotli:** both pages ask for `.unityweb` files and the builds host sends them without Content-Encoding, so Unity's loader decompresses them in JavaScript. Brotli would save only 3.9 MB, and its JS decoder is much slower on a phone.
- **Size report:** every build writes `build-size.json` and "Build size:" log lines (files and MB per part, compared with v17).
- **Loading bar:** the boot's bar now fills once from 0 to 100% across the game data and the core art and sound (it used to fill, empty and fill again). It says "Climbing out of the lava... N%", and the boot logs its timings. The page's own bar still covers the download.
- **Playtest `phone-load`** (run on device "phone"). No input until the menu opens by itself. It checks that the page's bar filled and went away, that the boot bar moved to 100%, and that the main menu was up within 20 s of the page opening (page clock from a new `SickOfItLoad.jslib`). It also logs what was downloaded by then, then checks 30+ fps on the menu and over 30 s of Level 1.

**Build size, before and after (for the PR):**
| | Files | MB |
|---|---:|---:|
| Before (v17, measured) | 3,658 | 1,267.4 |
| After (estimate from v17's files) | ~2,560 | ~740 |
| Cut | desktop textures over 2048: ~430 MB; duplicate phone bundles: 98 MB / 1,101 files | |
The next build prints the real number in its "Build size:" lines and `build-size.json`.

**How I checked it.** I measured the live v17 build over HTTP: both Addressables catalogs, a HEAD request for every bundle, the player files decompressed and re-compressed to compare gzip and Brotli. I ran a baseline phone smoke on beta (r_1cccbcb6b928: done, 0 problems, 56 fps). I read every changed C# file for duplicate members, missing usings and changed signatures. The new names (PageLoad, PageLoadInfo, SampleFps, LoadCheck, PhoneLoad*, WebGLPlayerSettings, TextureVariantSharedList, CapTextures, DropSharedCopies) are unique. **Unity compile not run here:** there is no Unity on this machine, and the checkout is sparse (ProjectSettings and jslibs are LFS or unfetched blobs). No conflicts.

**What J should look at.**
- Desktop sheets are now at most 2048, the same as phones have had. A 1440p+ monitor may show slightly softer backgrounds. If that's not OK, raise `MaxTextureSize` for desktop only.
- `stripEngineCode` is now on. If it was off before, a missing engine module would show as a runtime error on beta (Addressables' link.xml should keep what the bundles use).
- The jslib's `.meta` is new (WebGL only).

**Not done yet (next parts).**
- **Video1 as one small URL-streamed file:** the films are baked sprite sheets (~60 MB per variant) that rewind plays backwards. A VideoPlayer path is its own change.
- **Brotli without the fallback:** the Hawkes Games page (`hawkesgenwebsite/public/play/sickofit/index.html`) needs to ask for `.br` names first. The host already serves `*.br` with `Content-Encoding: br`. Then flip `WebGLPlayerSettings`.
- **A smaller "core" scope at boot**, if `phone-load` shows the menu over 20 s: its `load` and `download` events say which part is slow.
- Voice clips at a lower WebGL quality (sound is 78 MB in 1,063 bundles, each loaded on first play).
- Managed stripping above its current level, after `Assets/link.xml` is checked in a WebGL build (the JSON content types are found by reflection).

**GPU estimate:** none. Nothing here needs GPU generation ($0).
