"""What the floor writers leave alone: the art pipeline's files (Docs/ArtPipeline.md).

Every writer in Tools/Levels rebuilds its entities and specs from its tables, and its tables hold only gameplay and a
placeholder look. Once Tools/Art/art.mjs has made a creature's sheets it writes them into the entity's visual.animator
and takes the placeholder tint off; a writer run after that must not put the stand-in back. So:

- `art/<id>.json` belongs to the art pipeline once it exists: a writer creates it (a first draft) and never rewrites it.
- An entity whose file already has a generated animator (a sheet with a `.sheet.json` by art.mjs or fx_sheets.py beside it)
  keeps that animator, its facesLeft and its untinted look, whatever the writer's table says about `visual`.
"""
import json
import os

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")


def generated_animator(visual):
    """True when every state of this animator plays a generated sheet (art.mjs, fx_sheets.py: its .sheet.json says so)."""
    animator = (visual or {}).get("animator") or {}
    states = animator.get("states") or []
    if not states:
        return False
    for st in states:
        sheet = os.path.join(ASSETS, st.get("spriteSheet", "").split("#")[0])
        side = os.path.splitext(sheet)[0] + ".sheet.json"
        try:
            with open(side, encoding="utf-8") as f:
                if not json.load(f).get("generatedBy"):
                    return False
        except (OSError, ValueError):
            return False
    return True


def owned_by_art(path):
    """An art spec that already exists is the art pipeline's: writers only create it."""
    return path.replace("\\", "/").startswith("art/")


def keep_art(full, data):
    """Returns data with the generated look of the entity file at `full` carried over (see the module doc)."""
    if not isinstance(data, dict) or not os.path.exists(full):
        return data
    try:
        with open(full, encoding="utf-8") as f:
            old = json.load(f)
    except (OSError, ValueError):
        return data
    old_visual = old.get("visual") or {}
    if not generated_animator(old_visual):
        return data
    visual = dict(data.get("visual") or {})
    visual["animator"] = old_visual["animator"]
    if "facesLeft" in old_visual:
        visual["facesLeft"] = old_visual["facesLeft"]
    if "color" in old_visual:
        visual["color"] = old_visual["color"]
    else:
        visual.pop("color", None)
    out = dict(data)
    out["visual"] = visual
    return out


def guard(full, data):
    """For a writer's save: None when the file must be left alone (an existing art spec), else the data to write."""
    rel = os.path.relpath(os.path.abspath(full), os.path.join(ASSETS, "GameConfig")).replace("\\", "/")
    if rel.startswith("art/") and os.path.exists(full):
        return None
    if rel.startswith("content/entities/"):
        return keep_art(full, data)
    return data
