#!/usr/bin/env python3
"""
The Shaft's paintings (Docs/Story.md, "The Shaft"; ui/shaft.json): puts picked Hawkes Gen images into
Assets/Art/Shaft as UI sprites (with a full .png.meta, so no Unity is needed) and points ui/shaft.json at them.

    python3 Tools/Art/shaft_art.py                      # every slot in Tools/Art/shaft_art.json that has a source
    python3 Tools/Art/shaft_art.py --only band.X,marker

Tools/Art/shaft_art.json lists the slots and where each picture comes from ("source": a file, e.g. a Hawkes Gen
library path or a job output saved with hawkesgen_job save_to) and the Gen job that made it ("job", for the record):
  band.<numeral>    a Circle's band (floor-backdrop preset, 768x960, seamless): copied as a PNG
  meadow            the Meadow at the top with the burned hole: copied
  warden.<numeral>  the Warden (enemy-concept preset, on flat green): green keyed out, trimmed; the game tints it
                    dark until the Warden is met
  marker            the hero's marker (his helmet and launcher): copied (already cut out)
Needs ffmpeg; standard library only otherwise. A sprite that already has a .meta keeps its guid.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Art", "Shaft")
SHAFT = os.path.join(ROOT, "Assets", "GameConfig", "ui", "shaft.json")
SLOTS = os.path.join(ROOT, "Tools", "Art", "shaft_art.json")
TEMPLATE = os.path.join(ROOT, "Assets", "Images", "UI", "IconRewind.png.meta")

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        m = re.search(r"^guid: (\w+)", open(meta, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return hashlib.md5(("sickofit-shaft:" + os.path.relpath(path, ROOT)).encode()).hexdigest()


def write_meta(png, max_size=1024):
    """A single-sprite UI texture: IconRewind's importer settings with this file's guid and size cap."""
    text = open(TEMPLATE, encoding="utf-8").read()
    text = re.sub(r"^guid: \w+", "guid: " + guid_for(png), text, count=1, flags=re.M)
    text = re.sub(r"maxTextureSize: \d+", f"maxTextureSize: {max_size}", text)
    with open(png + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def ffmpeg(*args):
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", *args], check=True)


def make(slot, source, dest):
    if slot.startswith("warden."):
        # Flat green out (with a little despill), trimmed to the figure, at most 512 high.
        ffmpeg("-i", source, "-vf",
               "colorkey=0x00ff00:0.32:0.12,despill=type=green,"
               "scale=-2:512:flags=lanczos,format=rgba", dest)
    elif slot.startswith("band."):
        ffmpeg("-i", source, "-vf", "scale=768:960:flags=lanczos", dest)
    else:
        if os.path.abspath(source) != os.path.abspath(dest):
            shutil.copyfile(source, dest)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", default="", help="comma list of slots")
    args = parser.parse_args()
    only = {s for s in args.only.split(",") if s}
    slots = json.load(open(SLOTS, encoding="utf-8"))["slots"]
    os.makedirs(OUT, exist_ok=True)
    if not os.path.exists(OUT + ".meta"):
        with open(OUT + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(OUT)))

    shaft = json.load(open(SHAFT, encoding="utf-8"))
    bands = {b["numeral"]: b for b in shaft["bands"]}
    done = 0
    for slot, spec in slots.items():
        if only and slot not in only:
            continue
        source = spec.get("source") or ""
        name = slot.replace(".", "_") + ".png"
        dest = os.path.join(OUT, name)
        if source:
            source = os.path.join(ROOT, source) if not os.path.isabs(source) else source
            if not os.path.exists(source):
                print(f"{slot}: source {source} is missing", file=sys.stderr)
                continue
            make(slot, source, dest)
        if not os.path.exists(dest):
            continue
        write_meta(dest)
        asset = "Art/Shaft/" + name
        kind, _, numeral = slot.partition(".")
        if kind == "band":
            bands[numeral]["art"] = asset
        elif kind == "warden":
            bands[numeral]["wardenArt"] = asset
        elif kind == "meadow":
            shaft["meadowArt"] = asset
        elif kind == "marker":
            shaft["markerArt"] = asset
        done += 1
        print(f"{slot}: {asset}")

    with open(SHAFT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(shaft, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"{done} sprites in Assets/Art/Shaft, ui/shaft.json updated")


if __name__ == "__main__":
    main()
