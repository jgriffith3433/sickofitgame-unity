# Video Playback

Every moving picture in the game (story videos, death scenes, level backgrounds, main-menu loops) is a
**sprite-sheet sequence**, not a Unity `VideoPlayer`. WebGL can't reliably decode, seek or rewind video, so each
clip is baked into compressed texture sheets, shipped as Addressables bundles, and drawn one frame at a time by
`SpriteFramePlayer`. That makes playback seekable and rewindable, keeps it in sync with the game clock, and lets
memory be capped per clip.

```
source mp4 ──ffmpeg──► jpg frames ──VideoSpriteSheetBaker──► 2048² sheets (28 frames each)
(C:\Projects\SickOfItGameVideos)                               + SpriteFramesData asset (layout, chunks)
                                                               + Addressables labels  <Clip>_Part1..N
                                                                        │
            Addressables build (DXT for desktop, ASTC for phones) ◄─────┘
                                                                        │
  BundleDownloadManager (prefetch to browser cache) ◄───────────────────┤
                                                                        ▼
  VideoRuntime / DeathRuntime / LevelRuntime / PlacedSpriteBuilder ──► SpriteFramePlayer
            (owns the clock and audio)                                  (loads chunks, draws frames)
```

## Files

| What | Where |
|---|---|
| Clip data (layout, fps, chunks) | `Assets/Videos/<Clip>.asset` (`SpriteFramesData`) |
| Sheets | `Assets/Videos/<Clip>_SpriteSheets/<Clip>_sheet_NNNN.png` |
| Source mp4s (outside the repo) | `C:\Projects\SickOfItGameVideos\<Clip>\Output\<Clip>.mp4` (`game.json` `sourceMedia.videoRoot`) |
| Player | `Assets/Scripts/VideoFrameExtractor/Runtime/SpriteFramePlayer.cs` |
| Data + layout rule | `Assets/Scripts/VideoFrameExtractor/Runtime/SpriteFramesData.cs` |
| Baker | `Assets/Scripts/Tools/Editor/Generation/VideoSpriteSheetBaker.cs` |
| Bake UI | **SickOfIt > Generate Project** (per-clip *Regenerate*, or *Regenerate All Sheets*) |
| DXT / ASTC builds | `Tools/Editor/AddressablesTextureVariants.cs`, runtime pick in `Core/Addressables/TextureVariantRedirect.cs` |

Configs point at clips by asset path: `videos/*.json` and `deaths/*.json` `spriteFramesData`, a level's
`backgroundLayers[].spriteFramesData`, and `ui/mainMenu.json` `placedSprites[].spriteFramesData`.

## Baking

`VideoSpriteSheetBaker.Regenerate` turns one mp4 into sheets:

1. **Extract**: ffmpeg writes jpg frames at the clip's `playbackFPS`. That one number is both the bake rate and
   the playback rate, so a clip always plays at the source's real speed. **Changing `playbackFPS` without
   re-baking changes the playback speed of the frames already on disk.**
2. **Lay out**: `SpriteFramesData.CalculateOptimalGrid` picks the grid. Cells are at least a quarter of the sheet
   wide, so a 16:9 clip packs **28 frames of 512×284 into a 4×7 grid on one 2048×2048 sheet**, whatever the
   source resolution. Rows run from the top of the sheet. A clip whose stored grid packs fewer frames per sheet
   than this rule gets re-laid out on its next bake.
3. **Pack and import**: sheets are written over the old files in place (texture GUIDs stay attached to their
   Addressables entries; extra old sheets are deleted). Import settings are no mipmaps, not readable, bilinear,
   compressed, **no alpha** (the frames are jpgs, so DXT1 instead of DXT5: half the memory).
4. **Chunk**: frames are grouped into chunks of 3 sheets (84 frames, 8.4 s at 10 fps). Each chunk is one Addressables
   label, `<Clip>_Part<N>`, in the clip's group (`Video1_Group`, `Level1_Group`, ...). A chunk is the unit the
   player loads and unloads.

**Regenerate All Sheets** bakes every video, death and background whose mp4 exists, with no dialogs. Batch mode
(editor closed):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Projects/sickofitgame-unity" -logFile - -executeMethod SickOfIt.Tools.Editor.Generation.GenerateProjectWindow.RegenerateAllSheets
```

Clips with **`skipBulkRegenerate`** set are left alone because their frames are hand-timed rather than "the mp4
at `playbackFPS`": `MainMenu_Explode` (21 frames timed to its 4.2 s sound; a real-speed bake would stretch it
to the 15.6 s source) and `Level4_Background1` (a single still frame). Bake those one at a time on purpose.

After any bake: rebuild Addressables (both texture variants), then the WebGL player.

### Current rates

| Clips | `playbackFPS` |
|---|---|
| Video1–3, Death1–3, Level1_Background1–4, Level2_Background1, Level3_Background1 | 10 |
| MainMenu_Background1, Level2/3_Background2, Video4, MainMenu_Explode | 5 |
| Level4_Background1 | 3 (one frame) |

These favor download size and memory over smoothness and sharpness.

## Playing

`SpriteFramePlayer` sits on a GameObject with a `SpriteRenderer`. Callers create it and call
`Initialize(data, onReady)`.

**Time to frame.** The player keeps a local time and maps it to `frame = time × playbackFPS`, wrapping when the
clip loops and clamping to the last frame when it doesn't. Time comes from one of two places:

- **Game clock** (`UseGameClock(true)`): every Update reads `GameClock.Instance.Time` minus the clip's
  `SetClockOrigin`. Rewinding the clock rewinds the picture for free. Levels, videos and deaths run this way.
- **Own clock**: `Time.deltaTime` while `isPlaying`. The main-menu loops and the explosion use this.

`SetTime` seeks, `SetFrame` jumps to a frame.

**Drawing a frame.** All sheets of a clip share one layout, so the player cuts **one sprite per cell** from the
first loaded sheet (`CreateCellSprites`). To show frame *f*, it picks the chunk and sheet holding *f*, assigns
that cell's sprite, and swaps the sheet texture in through a `MaterialPropertyBlock`. Nothing is allocated per
frame and only the frame's rectangle is drawn. If the chunk the cell sprites were cut from is evicted, they are
re-cut from another loaded chunk.

**World size.** Every frame is drawn `SpriteFramesData.FrameWorldWidth` = **10.24 units** wide, whatever
resolution it was baked at (`FramePixelsPerUnit = frameWidth / 10.24`). That is the camera's full width at
orthographic size 2.88, and what menu, video and death layouts are tuned for. Re-baking at another resolution
must not need camera or scale changes. `LevelRuntime` and `ExperienceRunner` also stretch level backgrounds to
fill the camera height.

**Memory: the sliding window.** The player holds only a few chunks:

| | Desktop / editor | WebGL |
|---|---|---|
| Preloaded before `IsReady` | 3 | 2 |
| Kept in memory | 5 | 2 |
| Loaded ahead | 2 | 1 |

On each chunk change it loads the window around the playhead and releases the rest. While the game clock is
rewinding, the window keeps the chunks **behind** the playhead instead, since that's where the picture is
heading. The chunk on screen is never evicted before its replacement has loaded, so the picture can't flash.

A loaded chunk is about 3 sheets × 2 MB (DXT1) ≈ 6 MB, so a playing clip costs about 12 MB on WebGL.

**Phones.** Phone browsers can't sample DXT; WebGL would expand each sheet to RGBA32 (16 MB). Addressables are
therefore built twice: DXT in `ServerData/WebGL`, ASTC 8×8 in `ServerData/WebGL_ASTC`. `TextureVariantRedirect`
points every bundle URL at the ASTC folder on browsers without S3TC. Add `?tex=astc` or `?tex=dxt` to the page
URL to force one.

## Who drives the player

| Caller | What it does |
|---|---|
| `VideoRuntime` | Story video. Game clock plus a `RewindableAudioSource` for the soundtrack; hold R or the ◄◄ button rewinds, Escape skips. `PlaybackStartGate.Hold` keeps the clock at 0 until frames and audio are loaded, then starts them together. |
| `DeathRuntime` | Same, for death scenes. |
| `LevelRuntime` | Background layers. A plain animated layer loops on the game clock. **Sequenced layers** (`sequenceStart` / `sequenceDuration` / `sequenceLoop`) play back to back as one timeline: each segment is preloaded with `PrepareForSequence` while the previous one is on screen, posed on the frame the cut lands on, and only shown once it has drawn (`HasDrawnFrame`). Rewinding across a cut works the same way in reverse. |
| `PlacedSpriteBuilder` / `MainMenuManager` | Main-menu loops and the explosion, on their own clock. |
| `BundleDownloadManager` | Doesn't play anything. It prefetches a level's chunks into the browser cache from the main menu, so the first play doesn't stall. |

## Overlays on top

A video's (or death's, or level's) `timeline.overlays` draws keyframed pictures over the clip: a sprite or a sheet's
frames, a sort layer, and keyframes `{ time, position, rotation, scale, alpha, ease }`. `ExperienceRunner` adds a
`TimelineOverlayPlayer`, which samples every overlay from `GameClock.Time` each frame, exactly like the clip itself,
so rewind, seeking and skipping need nothing extra. Recipe: GameplayCookbook, "Something moving over a video".
`timeline.tracks` are legacy and never play; `events` fire once when the clock crosses them.

## Recipes

- **Add a video, death or background**: put the mp4 at `<videoRoot>\<Clip>\Output\<Clip>.mp4`, reference the
  clip from its config (create the asset with the first bake), set `playbackFPS` (10 for motion, 5 for loops),
  click *Build Sheets* in Generate Project, then rebuild Addressables.
- **Make a clip smaller**: lower `playbackFPS` and re-bake. Don't shrink the grid per clip; the layout rule is
  global.
- **Make a clip sharper**: raise `minCellW` in `CalculateOptimalGrid` (affects every clip on its next bake;
  1024-wide cells are 6 per sheet, about 4.7× the memory per frame).
- **Something shows neighbouring frames or the wrong size**: the frame layout in the asset doesn't match the
  sheets on disk. Re-bake the clip; don't change the camera.
