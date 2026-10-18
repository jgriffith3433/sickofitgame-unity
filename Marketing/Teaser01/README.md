# Sick Of It: Teaser 01 ("I'm tired... of sleeping...")

A 45-second YouTube teaser, 1920x1080, 30 fps, H.264 + AAC 48 kHz (J, 2026-10-18). Made from the game's own art
through Hawkes Gen; how it fits the story is in `Docs/Story.md`, "Teasers". No video files live in git: every piece
is a Gen library asset (`https://studio.hawkesgen.com/gen/v1/files/<asset id>`).

**The cut: `FINAL_ASSET`** (poster `POSTER_ASSET`, captions `VTT_ASSET`; Gen job `COMPOSE_JOB`).

## Shot list

| Time (s) | Shot | Picture | Sound |
|---|---|---|---|
| 0.0-17.5 | **The village** | The main menu's meadow, from its exact menu framing; a slow push and pan (keyed camera, monotone cubic) left to the hero's hut, the neighbours' hut coming into view, then into the dark doorway. Pollen drifting. Fade to black 15.6-17.5. | Farm Life (OST) from the top, the meadow bed under it. |
| 17.5-18.3 | Black | | Farm Life carries on, dipping. |
| 18.3-34.0 | **Inside the hut** | Fade in on the hero asleep on his straw bed, helmet on, arms folded, rocket launcher on the wall, a sunny afternoon through the window. Slow push to his helmet. Wan's loop (candle, grass, dust) played forward and back, his chest lifted on each breath, dust turning in the sunbeam. | Farm Life soft; three helmet snores (19.8, 22.5, 24.8), one on each breath. |
| 28.6-33.3 | **The line** | "I'm tired..." (28.7): his visor's eyes crack open, a dim orange. "...of sleeping..." (32.2): they blaze, the room warms, the camera pushes in. They flare to white. | The hero's voice (`player`), Farm Life ducked under it. |
| 34.0-38.2 | **Boom** | Cut on a white flash and a shake to the same meadow after the Overlord: his hut gone, ash falling, embers rising. | The boom; Farm Life stops dead. |
| 38.2-45.0 | **Title** | The meadow goes ember-dark; **SICK OF IT** slams in (theme.json's title look: #FFF0A0 to #FF7A3D, #1E1220 outline and shadow), "1,000 floors of Hell." (39.8), sickofitgame.com (41.0). Fade out 44-45. | A second, quieter boom on the slam; Farm Life comes back softly and fades out. |

## Assets (Hawkes Gen library)

| Piece | Asset | Made by |
|---|---|---|
| Hero's line, the take used | `a_f9ff15aa45dc4ace` | `audio.voice`, voice `player` (DzkD7I4tf5i3KSaB4cz5, eleven_v3), job `j_7ed740852a4a`; other takes `a_82b91b65273f3b62`, `a_1c9134a2d3540dce`, `a_6719c8d105fa38ef` |
| Helmet snores (used) | `a_7af49da37f1a2d94` | `audio.sfx`, job `j_319d293fe637` (other take `a_725dd0a11f9c602c`) |
| Meadow bed (20 s loop) | `a_3a9633efc410fd82` | `audio.sfx`, job `j_60461c33f425` |
| Boom with embers / title boom | `a_8c848401d25e5f26` / `a_7986f437c3a92491` | `audio.sfx`, job `j_cd49ba1bbfac` |
| Interior painting (take used) | `a_d4966297a69ccee1` | `image.edit` on the menu frame + the hero's Idle frame, job `j_9b6933d79e8b` (other takes `a_2a33dcabf0478c07`, `a_bce578a19c53434a`, `a_1b342c790816cd72`) |
| Interior plate (glint painted out) | `blob:26b4583edabdfeb249a6901fd634afceb679f7bcea6faddb7b3f62302d0f3eae` | `scripts/sleepfix.py` |
| Sleeping loop, Wan (take used) | `a_babd7cedc74a11fe` | `video.i2v` 832x464, 81 frames, start = end = the plate, job `j_5359b690c490` (other take `a_7a3b6a3e6d7126cf`) |
| Sleeping loop, 32 fps | `a_ec4c6302e027a353` | `video.interpolate` x2, job `j_1efc246e8dee` |
| Sleeping loop, 4x (rendered from) | `a_a198f748556b7103` | `video.upscale`, job `j_f452d0ae051e` |
| Exterior outpaint (take used) | `a_ab22660494cb56d9` | `image.edit` on the menu frame padded 400 px left, job `j_663bf819b453` (other take `a_324101cf9c237dd5`) |
| Exterior plate (outpaint left + the real menu frame) | `blob:93a4a3214880ad4edf5dbc84f5fdf4f9dc735464baae971a2ccea568bcf7ca6b` | `scripts/composite_plate.py` |
| Exterior plate, 4x (rendered from) | `a_11de2523ec727a91` | `image.upscale` resize 1, job `j_6cbe88d4c53e` |
| The village after, 2x | `a_ac337a4ff7c17a32` | `image.upscale` of the website's `images/QuietVillageAfter.jpg`, job `j_7c51e43833f8` |
| Interior still, 2x (not used) | `a_28b53185d691f51e` | `image.upscale`, job `j_9ed87113105d` |
| Talk's first exterior draft (superseded) | `a_dcb77385283876c1` | its `exterior.sh`: `blob:475379255bdbb3133167c528f1a6a007d241858357fc52e2d47fa7de0c17d639` |

Sources outside Gen: the main menu loop (`SickOfItGameVideos/MainMenu_Background/Output/MainMenu_Background.mp4`,
first frame; the loop is effectively still), Farm Life (`sickofitgamewebsite/music/Farm Life.mp3`), the village
after (`sickofitgamewebsite/images/QuietVillageAfter.jpg`), LiberationSans Bold (the game's TMP font).

GPU spent: about $0.61 (Talk's interior takes $0.14; this pass $0.47: outpaint $0.12, Wan loop $0.24, interpolate,
upscales $0.11). Voice and sound effects (ElevenLabs, Talk's pass): $0.68.

## Remaking it

Needs ffmpeg and Python 3 with Pillow and numpy (`python3 -m venv v && v/bin/pip install pillow numpy`). Fetch the
assets above into a folder (`curl -H "Authorization: Bearer $KEY" $HAWKESGEN_URL/v1/files/<id>`); blobs are the
plates the scripts below make, so they need no download.

```bash
S=Marketing/Teaser01/scripts
# plates
ffmpeg -i MainMenu_Background.mp4 -frames:v 1 menu.png
ffmpeg -i menu.png -vf "pad=1680:720:400:0:0x808080" menu_padded.png      # -> image.edit (prompt in the job)
python $S/composite_plate.py a_ab22660494cb56d9.png menu.png plate_ext.png # -> image.upscale resize 1
python $S/sleepfix.py a_d4966297a69ccee1.png sleep.png                    # -> video.i2v, interpolate, upscale
# shots (raw frames piped to x264)
enc() { ffmpeg -y -f rawvideo -pix_fmt rgb24 -s 1920x1080 -r 30 -i - -c:v libx264 -crf 14 -pix_fmt yuv420p "$1"; }
python $S/render_exterior.py a_11de2523ec727a91.png | enc exterior.mp4                    # 17.5 s
python $S/render_interior.py a_a198f748556b7103.mp4 sleep.png | enc interior.mp4          # 15.7 s
python $S/render_end.py a_ac337a4ff7c17a32.png | enc end.mp4                              # 11.0 s
# sound and the cut
bash $S/mix.sh <folder with the a_*.mp3 assets> "Farm Life.mp3" mix.wav
bash $S/assemble.sh exterior.mp4 interior.mp4 end.mp4 mix.wav teaser01.mp4
```

Then `video.compose` (free) with the cut as its one clip, no extra audio, `fps` 30, 1920x1080, and the caption
below, to put it in the library with a poster:
`{"clips":[{"clip":0}],"audio":[],"captions":[{"text":"I'm tired... of sleeping...","start":28.7,"end":33.3}]}`.

Any of the renderers previews single frames: `FRAMES=0,90,180 python render_exterior.py ...` writes PNGs instead.

Timings live at the top of each script and in `mix.sh` (`SNORE`, `LINE`, `BOOM`, `TITLE`): move the line, and move
`LINE` in `render_interior.py` with it (the eyes follow it).
