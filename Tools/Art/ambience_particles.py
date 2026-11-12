#!/usr/bin/env python3
"""Bake Gen ambient particle sprites into Resources/Ambience and (optionally) wire WeatherDef.sprite.

Expected files (Hawkes Gen image.generate + image.cutout, project org_eb0d5c82e8da8c2d.sickofit):
  particle_pollen, particle_spark, particle_steam, particle_dust, particle_soul, particle_debris

Usage:
  python3 Tools/Art/ambience_particles.py --check
  python3 Tools/Art/ambience_particles.py --meta   # write .meta next to each PNG that exists
  python3 Tools/Art/ambience_particles.py --wire   # set WeatherDef.sprite on Meadow + Floors 1000-997

AmbienceSprites loads Resources/Ambience/particle_* at runtime (white/tintable). GameConfig sprite paths
point at the same files under Assets/Art/Ambience when --wire is used (copies or identical LFS blobs).
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RES = os.path.join(ROOT, "Assets", "Resources", "Ambience")
ART = os.path.join(ROOT, "Assets", "Art", "Ambience")
PARTICLES = ("pollen", "spark", "steam", "dust", "soul", "debris")
KIND_TO_FILE = {
    "Pollen": "pollen",
    "Sparks": "spark",
    "Steam": "steam",
    "Dust": "dust",
    "Soul": "soul",
    "Debris": "debris",
    "Ash": "dust",
    "Cinders": "spark",
    "Grit": "dust",
}
LEVELS = (
    "Assets/GameConfig/levels/Level1.json",
    "Assets/GameConfig/levels/Floor1000.json",
    "Assets/GameConfig/levels/Floor999.json",
    "Assets/GameConfig/levels/Floor998.json",
    "Assets/GameConfig/levels/Floor997.json",
)

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
  isReadable: 0
  sRGBTexture: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteMode: 1
  spritePixelsToUnits: 64
  spritePivot: {{x: 0.5, y: 0.5}}
  textureType: 8
  wrapU: 1
  wrapV: 1
  filterMode: 1
  maxTextureSize: 256
  textureCompression: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid_for(path: str) -> str:
    meta = path + ".meta"
    if os.path.exists(meta):
        m = re.search(r"^guid: (\w+)", open(meta, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return hashlib.md5(("sickofit-ambience:" + os.path.relpath(path, ROOT)).encode()).hexdigest()


def write_meta(png: str) -> None:
    text = META_TEMPLATE.format(guid=guid_for(png))
    with open(png + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def write_folder_meta(folder: str) -> None:
    folder_meta = folder + ".meta"
    if os.path.exists(folder_meta):
        return
    guid = hashlib.md5(("sickofit-dir:" + folder).encode()).hexdigest()
    with open(folder_meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )


def present() -> list[str]:
    found = []
    for name in PARTICLES:
        for folder in (RES, ART):
            p = os.path.join(folder, f"particle_{name}.png")
            if os.path.isfile(p) and os.path.getsize(p) > 200:
                found.append(name)
                break
    return found


def check() -> int:
    have = present()
    missing = [n for n in PARTICLES if n not in have]
    print(f"{len(have)}/{len(PARTICLES)} ambient particle sprites present")
    for n in have:
        print(f"  ok  particle_{n}.png")
    for n in missing:
        print(f"  MISSING particle_{n}.png — Gen image.generate + cutout → Resources/Ambience/")
    return 1 if missing else 0


def meta() -> int:
    os.makedirs(RES, exist_ok=True)
    os.makedirs(ART, exist_ok=True)
    write_folder_meta(RES)
    write_folder_meta(ART)
    n = 0
    for name in PARTICLES:
        for folder in (RES, ART):
            png = os.path.join(folder, f"particle_{name}.png")
            if os.path.isfile(png):
                write_meta(png)
                n += 1
                print(f"meta {png}")
    return 0 if n else 1


def wire() -> int:
    """Point each weather kind's sprite at Art/Ambience/particle_<id>.png when the file exists."""
    have = set(present())
    if not have:
        print("no particle PNGs to wire", file=sys.stderr)
        return 1
    changed = 0
    for rel in LEVELS:
        path = os.path.join(ROOT, rel)
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        weather = (((data.get("script") or {}).get("ambience") or {}).get("weather")) or []
        for w in weather:
            kind = w.get("kind")
            key = KIND_TO_FILE.get(kind)
            if not key or key not in have:
                continue
            want = f"Art/Ambience/particle_{key}.png"
            art = os.path.join(ART, f"particle_{key}.png")
            res = os.path.join(RES, f"particle_{key}.png")
            if not os.path.isfile(art) and os.path.isfile(res):
                os.makedirs(ART, exist_ok=True)
                shutil.copy2(res, art)
                write_meta(art)
            if w.get("sprite") != want:
                w["sprite"] = want
                changed += 1
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write("\n")
        print(f"wired {rel}")
    print(f"{changed} weather sprite fields set")
    return 0


def main(argv: list[str]) -> int:
    if "--check" in argv:
        return check()
    if "--meta" in argv:
        return meta()
    if "--wire" in argv:
        return wire()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
