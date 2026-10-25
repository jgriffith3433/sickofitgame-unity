#!/usr/bin/env python3
"""Puts a finished lore film into the game's video slot (Docs/LoreVideos.md "Putting a film into the game"):
the film's mp4 -> Assets/Videos/<Video>.mp4, <Video>_Audio.mp3, <Video>_FirstFrame.png, a copy at the baker's source
(<root>/<Video>/Output/<Video>.mp4) and videos/<Video>.json's displayName and timeline.duration. The sprite sheets and
Addressables are then baked in the Unity editor (the command is printed). No Unity here; ffmpeg only.

Usage: prepare_lore_video.py <repo root> <film.mp4> <VideoN> "<display name>" [--fps 12] [--root <videoRoot>]"""
import json, os, shutil, subprocess, sys

def run(*a):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *a], check=True)

def main():
    args = [a for a in sys.argv[1:]]
    fps, root = "12", None
    for flag in ("--fps", "--root"):
        if flag in args:
            i = args.index(flag)
            v = args[i + 1]
            del args[i:i + 2]
            if flag == "--fps": fps = v
            else: root = v
    repo, film, video, title = args
    vdir = os.path.join(repo, "Assets", "Videos")
    seconds = float(subprocess.check_output(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", film]).decode())
    # the game's copy: small H.264 (the sheets are 512x284 cells; the mp4 is the source and the web download is the sheets)
    run("-i", film, "-vf", "scale=960:-2", "-c:v", "libx264", "-crf", "24", "-preset", "slow", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "128k", os.path.join(vdir, video + ".mp4"))
    run("-i", film, "-vn", "-c:a", "libmp3lame", "-b:a", "128k", "-ar", "44100", os.path.join(vdir, video + "_Audio.mp3"))
    run("-ss", "0.4", "-i", film, "-frames:v", "1", "-vf", "scale=1024:-2", os.path.join(vdir, video + "_FirstFrame.png"))
    if root:
        out = os.path.join(root, video, "Output")
        os.makedirs(out, exist_ok=True)
        shutil.copy(os.path.join(vdir, video + ".mp4"), os.path.join(out, video + ".mp4"))
    cfg = os.path.join(repo, "Assets", "GameConfig", "videos", video + ".json")
    d = json.load(open(cfg))
    d["displayName"] = title
    d["timeline"]["duration"] = round(seconds, 2)
    json.dump(d, open(cfg, "w"), indent=2, ensure_ascii=False)
    open(cfg, "a").write("\n")
    frames = int(seconds * float(fps)) + 1
    print(f"{video}: {seconds:.2f} s -> about {frames} frames at {fps} fps = {-(-frames // 28)} sheets of 4x7 cells (512x284)")
    print(f"Unity bake (editor closed): Unity.exe -batchmode -nographics -projectPath <project> -logFile - -executeMethod "
          f"SickOfIt.Tools.Editor.Generation.GenerateProjectWindow.RegenerateClipSheets -clip {video} -fps {fps}")

main()
