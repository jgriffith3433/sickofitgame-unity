"""The art inventory: every visible thing in the game and whether it has real art (Docs/ArtInventory.md).

    python Tools/Art/art_inventory.py           # rewrite Docs/ArtInventory.md
    python Tools/Art/art_inventory.py --check   # also fail (exit 1) while any row is a placeholder or a stand-in

No Unity, no GPU. It reads the content the game reads:

- content/entities: an entity drawn by a labeled circle or a coloured box (EntityFactory.AddPlaceholder) is a placeholder,
  unless one of its abilities paints it from a sheet (FloorKit art) or it is invisible on purpose (DECISIONS below). An entity
  whose animator plays a sheet made for another creature, tinted, is a stand-in. One that shares its parent's sheets untinted
  is the same creature (Docs/Story.md, rule 5). Sheets art.mjs made say so in their .sheet.json.
- art/hero.json: every hero clip's "wired" (own / standIn / false) and its sheet.
- content/weapons: every weapon's grip ("hands") and that each of its states has a sheet of its own.

Entities nothing references (retired) are listed apart and are not counted.
"""
import glob
import json
import os
import re
import sys

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CFG = os.path.join(REPO, "Assets", "GameConfig")
ASSETS = os.path.join(REPO, "Assets")
OUT = os.path.join(REPO, "Docs", "ArtInventory.md")

# Entities that are drawn on purpose by nothing, or by something other than their own sprite. Each says why.
DECISIONS = {
    "grappleHook": "invisible body; the grappleAnchor ability paints the anchor (art/props, AnchorRing)",
    "grappleRing": "invisible body; the grappleAnchor ability paints the anchor (art/props, AnchorRing)",
    "yoobaProp": "Yooba himself (the village slime) on Level 1: his slime sheet in his own green",
}
# Abilities that paint the entity from their own sheet (FloorKit / FloorKitAir / Path): the box is replaced by that art.
ART_FIELDS = ("art", "surfaceArt", "chainArt", "columnArt", "seaArt", "rungArt", "fieldArt")


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def merge(a, b):
    out = dict(a)
    for k, v in b.items():
        out[k] = merge(a[k], v) if isinstance(v, dict) and isinstance(a.get(k), dict) else v
    return out


def _circle_owns(circle, art):
    """A Circle owns its backdrop when some of its layers are named for it (RuneVoid for runeBridges, Road* for crackedRoad)."""
    stems = {"emberWastes": ("Island", "Spires", "FarSea"), "crackedRoad": ("Road", "Rift"), "runeBridges": ("Rune",)}.get(circle, ())
    return any(os.path.basename(a).startswith(stems) for a in art) if stems else True


def main():
    check = "--check" in sys.argv
    ents = {os.path.basename(f)[:-5]: load(f) for f in glob.glob(os.path.join(CFG, "content", "entities", "*.json"))}

    def resolve(i, seen=()):
        e = ents.get(i, {})
        if "extends" in e and e["extends"] not in seen and e["extends"] in ents:
            return merge(resolve(e["extends"], seen + (i,)), e)
        return e

    def chain(i):
        out = []
        while i in ents and i not in out:
            out.append(i)
            i = ents[i].get("extends")
        return out

    # what is referenced (levels, circles, scenes, sequences, other entities, weapons, game/player config)
    texts = []
    for pat in ("levels/*.json", "content/circles/*.json", "content/scenes/*.json", "content/sequences/*.json", "content/weapons/*.json", "game.json", "player.json"):
        for f in glob.glob(os.path.join(CFG, pat)):
            texts.append(open(f, encoding="utf-8").read())
    ent_texts = {i: json.dumps(e) for i, e in ents.items()}
    used = set()
    for i in ents:
        needle = f'"{i}"'
        if any(needle in t for t in texts):
            used.add(i)
    # entities referenced by used entities (spawned shots, glows, marks) and parents of used ones
    changed = True
    while changed:
        changed = False
        for i in list(used):
            for j in ents:
                if j not in used and f'"{j}"' in ent_texts[i] and ents[i].get("extends") != j:
                    used.add(j)
                    changed = True
    # parents that only lend their fields (abilities, art) to entities in use: listed, not counted
    bases = {p for i in used for p in chain(i)[1:]} - used

    def sheet_meta(sheet):
        side = os.path.splitext(os.path.join(ASSETS, sheet.split("#")[0]))[0] + ".sheet.json"
        try:
            return load(side)
        except (OSError, ValueError):
            return None

    specs = {os.path.basename(f)[:-5]: load(f) for f in glob.glob(os.path.join(CFG, "art", "*.json")) if not os.path.basename(f).startswith("_")}
    spec_of_entity = {}
    for sid, s in specs.items():
        if s.get("kind") != "hero":
            spec_of_entity[s.get("entity", sid)] = sid

    rows = []
    for i in sorted(ents):
        e = resolve(i)
        own = ents[i]
        v = e.get("visual") or {}
        an = v.get("animator") or {}
        sheets = sorted({st.get("spriteSheet", "") for st in an.get("states", []) if st.get("spriteSheet")})
        if v.get("sprite"):
            sheets.append(v["sprite"])
        tinted = "color" in (own.get("visual") or {}) and str(own["visual"]["color"]).upper() not in ("#FFFFFF", "#FFFFFFFF")
        defines_art = bool((own.get("visual") or {}).get("animator") or (own.get("visual") or {}).get("sprite"))
        ability_art = any(isinstance(a, dict) and any(isinstance(a.get(k), dict) and a[k].get("sheet") for k in ART_FIELDS)
                          for a in (e.get("abilities") or []) + [x for st in (e.get("states") or []) for x in (st.get("abilities") or [])])
        status, how = "", ""
        if i in DECISIONS:
            status, how = "ok", DECISIONS[i]
        elif ability_art:
            status, how = "ok", "painted by its ability from a sheet (FloorKit art)"
        elif not sheets:
            shape = v.get("placeholderShape", "Circle")
            col = str(v.get("placeholderColor", "#E64D4DFF"))
            if shape == "Box" and len(col) == 9 and col.endswith("00"):
                status, how = "ok", "invisible on purpose (a zone with a transparent box)"
            else:
                status, how = "PLACEHOLDER", f"drawn as a {'coloured box' if shape == 'Box' else 'labeled circle'}"
        else:
            metas = [sheet_meta(s) for s in sheets]
            generated = all(m and m.get("generatedBy") for m in metas)
            tintable = all(m and m.get("tintable") for m in metas)
            if defines_art:
                if tinted and not tintable and not (specs.get(spec_of_entity.get(i, ""), {}).get("keepTint")):
                    status, how = "STAND-IN", f"its own animator plays {', '.join(sorted({s.split('/')[-2] if '/' in s else s for s in sheets}))} tinted {own['visual']['color']}"
                else:
                    status, how = "ok", ("generated: " if generated else "painted: ") + ", ".join(sorted({os.path.dirname(s) or s for s in sheets}))
            else:
                parent = next((p for p in chain(i)[1:] if (ents[p].get("visual") or {}).get("animator") or (ents[p].get("visual") or {}).get("sprite")), "?")
                if tinted and not tintable:
                    status, how = "STAND-IN", f"wears {parent}'s art tinted {own['visual']['color']}"
                elif tinted:
                    status, how = "ok", f"{parent}'s effect sheet (made to be tinted), in {own['visual']['color']}"
                else:
                    status, how = "ok", f"the same creature as {parent} (its art, untinted)"
        if i in bases:
            status = "base only" if status != "ok" else "ok (base only)"
        elif i not in used:
            status = "retired" if status != "ok" else "ok (unused)"
        rows.append((i, e.get("team", ""), status, how, spec_of_entity.get(i, "")))

    # hero
    hero = specs.get("hero") or {}
    player = load(os.path.join(CFG, "player.json"))
    pstates = {s["stateName"]: s for s in player.get("animationConfig", {}).get("states", [])}
    hero_rows = []
    for name, c in (hero.get("clips") or {}).items():
        st = c.get("state")
        ps = pstates.get(st)
        sheet = ps["spriteSheet"].split("/")[-1] if ps else None
        exists = ps and os.path.exists(os.path.join(ASSETS, ps["spriteSheet"]))
        if c.get("wired") == "own" and sheet == f"{st}.png" and exists:
            status = "ok"
        elif ps and sheet != f"{st}.png":
            status = "STAND-IN"
        elif not ps:
            status = "MISSING"
        else:
            status = "STAND-IN"
        hero_rows.append((st, name, status, sheet or "-"))

    weapon_rows = []
    for f in sorted(glob.glob(os.path.join(CFG, "content", "weapons", "*.json"))):
        wid = os.path.basename(f)[:-5]
        w = load(f)
        h = w.get("hands") or {}
        if "look" in w:
            weapon_rows.append((wid, "PLACEHOLDER", "a block prop (look) is drawn over the sheets"))
            continue
        if not h:
            weapon_rows.append((wid, "PLACEHOLDER", "no grip of its own (hands)"))
            continue
        pfx = h.get("prefix", "")
        steps = len((w.get("melee") or {}).get("steps") or []) if w.get("kind") == "Melee" else 1
        names = [h.get("idle") or pfx + "Idle", h.get("run") or pfx + "Run", h.get("jump") or pfx + "Jump", h.get("fall") or pfx + "Fall",
                 h.get("land") or pfx + "Land", h.get("runAttack") or pfx + "RunAttack", h.get("airAttack") or pfx + "AirAttack",
                 h.get("equip") or "Equip" + pfx]
        names += h.get("attack") or ([pfx + "Attack"] if steps <= 1 else [f"{pfx}Attack{n}" for n in range(1, steps + 1)])
        bad = [n for n in names if n not in pstates or pstates[n]["spriteSheet"].split("/")[-1] != f"{n}.png"
               or not os.path.exists(os.path.join(ASSETS, pstates[n]["spriteSheet"]))]
        weapon_rows.append((wid, "ok" if not bad else "STAND-IN", f"{len(names)} states" + (f"; without a sheet of their own: {', '.join(bad)}" if bad else ", each its own sheet")))

    # Circles: each Circle's backdrop and floor art is its own (a Circle wearing another's backdrop is a stand-in).
    circle_rows = []
    circles = {os.path.basename(f)[:-5]: open(f, encoding="utf-8").read() for f in glob.glob(os.path.join(CFG, "content", "circles", "*.json"))}
    shared_ok = {"Art/Climb/Haze_band.png", "Art/Climb/Ceiling_band.png"}  # mist and the rock ceiling: every Circle's sky
    art_of = {c: set(re.findall(r'"(Art/[^"]+?\.png)', t)) - shared_ok for c, t in circles.items()}
    order = sorted(circles)
    for c in order:
        mine = art_of[c]
        earlier = set().union(*[art_of[o] for o in order if o != c and order.index(o) < order.index(c)] or [set()])
        borrowed = sorted(mine & set().union(*[art_of[o] for o in order if o != c] or [set()]))
        status = "STAND-IN" if mine and len(borrowed) == len(mine) and c not in ("emberWastes",) and mine <= earlier | set(borrowed) and _circle_owns(c, mine) is False else "ok"
        circle_rows.append((c, status, ", ".join(sorted(os.path.basename(x) for x in mine))))

    # UI: a card icon that is only a letter (Upgrades.cs: "a short tag shown on the card until real icon art exists")
    ui_rows = []
    for f in sorted(glob.glob(os.path.join(CFG, "content", "upgrades", "*.json"))):
        u = load(f)
        ui_rows.append((f"grudge {os.path.basename(f)[:-5]}", "ok" if u.get("iconArt") else ("PLACEHOLDER" if u.get("icon") else "ok (no badge)"),
                        u.get("iconArt") or (f'letter "{u.get("icon")}"' if u.get("icon") else "no badge")))
    for u in player.get("availableUpgrades", []):
        ui_rows.append((f"good {u.get('id')}", "ok" if u.get("iconArt") else ("PLACEHOLDER" if u.get("icon") else "ok (no badge)"),
                        u.get("iconArt") or (f'letter "{u.get("icon")}"' if u.get("icon") else "no badge")))

    # Circle scenery: props along a travel stage, ambience depth rows and far-off life (a built-in shape without a sprite).
    scenery_rows = []
    for c in sorted(circles):
        cd = json.loads(circles[c])
        groups = [("prop", cd.get("props") or []), ("depth", (cd.get("ambience") or {}).get("depth") or []),
                  ("life", (cd.get("ambience") or {}).get("life") or [])]
        for kind, items in groups:
            for it in items:
                spr = it.get("sprite")
                scenery_rows.append((f"{c} {kind} '{it.get('name', '')}'", "ok" if spr else "PLACEHOLDER", spr or f"built-in {it.get('shape', '')} shape"))

    # Weapons beyond the grip: the HUD chip's icon and a melee weapon's swing.
    for f in sorted(glob.glob(os.path.join(CFG, "content", "weapons", "*.json"))):
        w = load(f)
        wid = os.path.basename(f)[:-5]
        weapon_rows.append((f"{wid} HUD icon", "ok" if w.get("icon") else "PLACEHOLDER", w.get("icon") or "the chip shows only its name"))
        if w.get("kind") == "Melee":
            sw = (w.get("melee") or {}).get("swingArt")
            weapon_rows.append((f"{wid} swing", "ok" if sw else "PLACEHOLDER", sw or "a drawn crescent and disc (MeleeSwingFx)"))

    # Scenes, menus and the title: every picture and baked video a config names must exist.
    scene_rows = []
    for pat, kind in (("deaths/*.json", "death scene"), ("videos/*.json", "story video"), ("ui/*.json", "menu"), ("levels/*.json", "level backdrop")):
        for f in sorted(glob.glob(os.path.join(CFG, pat))):
            t = open(f, encoding="utf-8").read()
            refs = sorted(set(re.findall(r'"((?:Videos|Images|Art|Textures)/[^"#]+?\.(?:png|jpg|asset|mp4))', t)))
            if not refs:
                continue
            missing = [r for r in refs if not os.path.exists(os.path.join(ASSETS, r))]
            scene_rows.append((f"{kind} {os.path.basename(f)[:-5]}", "ok" if not missing else "MISSING", ", ".join(missing) if missing else f"{len(refs)} pictures/videos"))
    scene_rows = [r for r in scene_rows if r[1] != "ok"] + [r for r in scene_rows if r[1] == "ok"][:0] or scene_rows

    bad_ent = [r for r in rows if r[2] in ("PLACEHOLDER", "STAND-IN")]
    bad_hero = [r for r in hero_rows if r[2] != "ok"]
    bad_wpn = [r for r in weapon_rows if r[1] != "ok"]
    bad_circle = [r for r in circle_rows if r[1] != "ok"]
    bad_scenery = [r for r in scenery_rows if r[1] != "ok"]
    bad_scene = [r for r in scene_rows if r[1] != "ok"]
    bad_ui = [r for r in ui_rows if r[1] == "PLACEHOLDER"]
    lines = ["# Art inventory", "",
             "Every visible thing in the game and whether it has real art. **Written by `python Tools/Art/art_inventory.py`** (no Unity,",
             "no GPU; `--check` fails while a row is a placeholder or a stand-in). Don't edit the tables by hand: fix the art, then run it.",
             "How art is made: Docs/ArtPipeline.md (creatures `node Tools/Art/art.mjs <id>`, the hero `art.mjs hero`, effects and props",
             "`Tools/Art/fx_sheets.py`).", "",
             "## Totals", "",
             f"| | ok | placeholder / stand-in |", "|---|---|---|",
             f"| Entities in use | {sum(1 for r in rows if r[2] == 'ok')} | {len(bad_ent)} |",
             f"| Hero clips (art/hero.json) | {sum(1 for r in hero_rows if r[2] == 'ok')} | {len(bad_hero)} |",
             f"| Weapon grips | {sum(1 for r in weapon_rows if r[1] == 'ok')} | {len(bad_wpn)} |",
             f"| Circle backdrops | {len(circle_rows) - len(bad_circle)} | {len(bad_circle)} |",
             f"| Circle scenery (props, depth rows, far-off life) | {len(scenery_rows) - len(bad_scenery)} | {len(bad_scenery)} |",
             f"| Story videos, death scenes, menus, level backdrops (files exist) | {len(scene_rows) - len(bad_scene)} | {len(bad_scene)} |",
             f"| Card icons (Grudges, Yooba's goods) | {len(ui_rows) - len(bad_ui)} | {len(bad_ui)} |", ""]
    if os.path.exists(OUT):
        old = open(OUT, encoding="utf-8").read()
        m = re.search(r"## Before this pass\n(.*?)(?=\n## )", old, re.S)
        if m:
            lines += ["## Before this pass", m.group(1).rstrip(), ""]
    lines += ["## Still to do", ""]
    if not (bad_ent or bad_hero or bad_wpn or bad_circle or bad_ui or bad_scenery or bad_scene):
        lines += ["Nothing: every row below has real art.", ""]
    for r in bad_ent:
        lines.append(f"- entity `{r[0]}`: {r[2]}, {r[3]}")
    for r in bad_hero:
        lines.append(f"- hero `{r[0]}` ({r[1]}): {r[2]}, plays {r[3]}")
    for r in bad_wpn:
        lines.append(f"- weapon `{r[0]}`: {r[2]}")
    for r in bad_circle:
        lines.append(f"- Circle `{r[0]}`: wears another Circle's backdrop ({r[2]})")
    for r in bad_ui:
        lines.append(f"- {r[0]}: card icon is a {r[2]}")
    for r in bad_scenery:
        lines.append(f"- scenery {r[0]}: {r[2]}")
    for r in bad_scene:
        lines.append(f"- {r[0]}: missing {r[2]}")
    lines += ["", "## The hero", "", "| State | hero.json clip | Status | Sheet |", "|---|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} |" for r in sorted(hero_rows)]
    lines += ["", "Ledge grab and ledge climb have no clip: the game has no ledge mechanic (nothing in PlayerController grabs a ledge).", "",
              "## Weapons (grips)", "", "| Weapon | Status | States |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} |" for r in weapon_rows]
    lines += ["", "## Circles (backdrop layers and far art)", "", "| Circle | Status | Its art |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} |" for r in circle_rows]
    lines += ["", "## Circle scenery", "", "| Entry | Status | Art |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} |" for r in scenery_rows]
    lines += ["", "## Story videos, death scenes, menus and level backdrops", "", "| Config | Status | Pictures |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} |" for r in scene_rows]
    lines += ["", "## Drawn in code on purpose (not art to make)", "",
              "- The aim crosshair (AimCursor) and the aim guide's laser line (AimGuide): UI drawn in theme.json colours.",
              "- Accessibility outlines (AccessibilityVisuals): readability aids, off by default. No hitbox dot or ring is drawn on the hero.",
              "- Hit sparks, dust, flashes and glows (Core/Fx, Glow) and weather bits (AmbienceSprites weather): soft particles tinted per Circle.",
              "- Menu panels, buttons and chips (MenuGraphics): theme.json shapes around painted icons and pictures.",
              "- Ladders without art, anchors' rings (FloorKit) and a weapon swing without swingArt: fallbacks only; every ladder and melee weapon in the game names its art.",
              "- `Images/` legacy sheets still used untinted by their own creature: the Fireball (embers), the Diamond, the Drooler (fighter), the Lurker (sentinel), the old shooter (Demon2, a base no level spawns) and the title screen's house.", ""]
    lines += ["", "## Card icons", "", "| Card | Status | Icon |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} |" for r in ui_rows]
    lines += ["", "## Entities in use", "", "| Entity | Team | Status | How it is drawn | Art spec |", "|---|---|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} | {r[4]} |" for r in rows if not r[2].startswith(("retired", "ok (unused)", "base only", "ok (base only)"))]
    lines += ["", "## Not counted: bases other entities extend, and entities nothing spawns (retired)", "",
              "| Entity | Status | How it is drawn |", "|---|---|---|"]
    lines += [f"| {r[0]} | {r[2]} | {r[3]} |" for r in rows if r[2].startswith(("retired", "ok (unused)", "base only", "ok (base only)"))]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    total_bad = len(bad_ent) + len(bad_hero) + len(bad_wpn) + len(bad_circle) + len(bad_ui) + len(bad_scenery) + len(bad_scene)
    print(f"{OUT}: entities {len(rows) - len(bad_ent)} ok / {len(bad_ent)} to do; hero {len(hero_rows) - len(bad_hero)} / {len(bad_hero)}; weapons {len(weapon_rows) - len(bad_wpn)} / {len(bad_wpn)}; circles {len(bad_circle)} to do; icons {len(bad_ui)} to do; scenery {len(bad_scenery)} to do; scenes {len(bad_scene)} missing")
    if check and total_bad:
        sys.exit(1)


if __name__ == "__main__":
    main()
