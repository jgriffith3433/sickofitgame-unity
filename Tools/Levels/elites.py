#!/usr/bin/env python3
"""
Elites and mid-bosses (Docs/Story.md "Elites and mid-bosses"): twelve named elite variants of existing enemies and four named mid-bosses
with phases, minions and hero lines, placed on authored floors at x5 (995, 955, 905, 895, 855, 805).

Writes: the 12 elite entities and 4 mid-boss entities (+ their warning/hit pairs), the voice lines in audio.json (clips are made through
Hawkes Gen: the audio entries point at Audio/Generated/<id>.mp3), the floor levels' rules and mid-boss phases, and the test arena
content/scenes/eliteArena.json. Idempotent: it rebuilds all of it from the tables below.

    python3 Tools/Levels/elites.py          # write
    python3 Tools/Levels/elites.py --check  # fail if the files differ from what the tables make (and check the placement)
"""
import copy
import json
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CFG = os.path.join(ROOT, "Assets", "GameConfig")
check_only = "--check" in sys.argv
changed = []


def load(path):
    with open(os.path.join(CFG, path), encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    full = os.path.join(CFG, path)
    if os.path.exists(full):
        with open(full, encoding="utf-8") as f:
            if f.read() == text:
                return
    changed.append(path)
    if not check_only:
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


HERO = "DzkD7I4tf5i3KSaB4cz5"
ANNOUNCER = "1xynpTfy7I462pPrU2kL"
VOICES = {}  # sound id -> (voice name, voice id, prompt)


def voice(sound_id, who, text):
    VOICES[sound_id] = ("player", HERO, "[deadpan] " + text) if who == "hero" else ("announcer", ANNOUNCER, "[shouting] " + text)


# ─── the twelve elites ─────────────────────────────────────────────────────────────────────────────────
# (id, base, name, hint, note, floors, mutate). The hint is the hero's line the first time one spawns in a save (hintOnce).

def ab(d, kind):
    return next(a for a in d["abilities"] if a["type"] == kind)


def tail_children(entity, count, spacing=0.6):
    return [{"type": "spawn", "entity": entity, "anchor": "Self", "count": count, "spacing": [spacing, 0.0], "facePlayer": False}]


def aura(radius, interval=1.5):
    return {"type": "aura", "radius": radius, "damagePerTick": 1, "interval": interval}


def m_quickfire(d):
    d["moveSpeed"] = 1.1
    d["abilities"].insert(2, {"type": "chase", "speed": 1.1, "stopDistance": 3.2, "horizontalOnly": True, "face": True})
    s = ab(d, "shoot")
    s.update(burst=2, burstInterval=0.28, interval=2.0, initialDelay=1.4, speed=4.0)


def m_corona(d):
    s = ab(d, "shoot")
    s["pattern"]["count"] = 14
    s["interval"] = 3.8
    d["abilities"].append(aura(1.1))


def m_ironhide(d):
    d["moveSpeed"] = 1.2
    ab(d, "frontShield").update(arc=150, leak=0.0)
    p = ab(d, "patrol")
    p.update(speed=1.2, distance=3.0)
    d["abilities"].append({"type": "shoot", "projectile": "projectileEnemy", "speed": 3.0, "aim": "Player", "muzzle": [0.09, 0.09375],
                           "interval": 3.6, "initialDelay": 2.2, "damage": 1})


def m_alpha(d):
    d["moveSpeed"] = 0.9
    ab(d, "chase")["speed"] = 0.9
    ab(d, "charge").update(interval=2.4, windup=0.6, speed=8.5)
    d["onDeath"] = tail_children("cinder", 3, 0.5)


def m_twinbolt(d):
    ab(d, "shoot").update(burst=2, burstInterval=0.45, interval=5.0, speed=9.5)


def m_twinlens(d):
    first = ab(d, "laserSweep")
    first["interval"] = 7.0
    second = copy.deepcopy(first)
    second.update(sweepDegrees=-70, initialDelay=4.8)
    d["abilities"].append(second)


def m_raider(d):
    t = ab(d, "teleport")
    t.update(interval=2.0, distance=3.0, behind=True, telegraph=0.7)
    ab(d, "shoot").update(burst=2, burstInterval=0.3, interval=2.4, speed=3.8)


def m_brood(d):
    d["moveSpeed"] = 1.3
    ab(d, "chase")["speed"] = 1.3
    d["onDeath"] = tail_children("demonWhelp", 2, 0.7)


def m_furnace(d):
    ab(d, "frontShield").update(arc=150, leak=0.0)
    ab(d, "shoot").update(interval=2.6)
    ab(d, "shoot")["pattern"].update(count=3, angle=22)
    d["abilities"].append(aura(1.2))


def m_triple(d):
    s = ab(d, "shoot")
    s["pattern"] = {"type": "spread", "count": 3, "angle": 18}
    s["interval"] = 4.0


def m_twice(d):
    ab(d, "revive").update(times=2, healthPercent=50, downSeconds=2.0)


def m_matron(d):
    ab(d, "frontShield").update(arc=120, leak=0.0)
    ab(d, "teleport")["interval"] = 3.6
    d["abilities"].append(aura(1.0))


ELITES = [
    # id, base, name, hint, note, health, scale multiplier, colour, fx colour, mutate
    ("quickfireImp", "imp", "Quickfire Imp", "Twice as fast, two shots. Move between them.", "Same imp. Double the hurry.", 4, 1.2, "#FF9A5A", "#FFC46A", m_quickfire),
    ("coronaImp", "haloImp", "Corona Imp", "Ring of embers, and it burns close. Keep your distance.", "A halo you can't stand next to.", 5, 1.25, "#FFEFA0", "#FFF2B0", m_corona),
    ("ironhideBulwark", "bulwarkImp", "Ironhide Bulwark", "Armored. Shots from the front are wasted. Get behind it.", "Plated all the way round, almost.", 9, 1.25, "#6C8294", "#8FD0FF", m_ironhide),
    ("alphaHound", "slagHound", "Alpha Hound", "Pack leader. It bursts into cinders. Shoot from range.", "It leaves a litter behind.", 6, 1.3, "#7A3A2A", "#FF8A3A", m_alpha),
    ("twinBoltDeadeye", "deadeyeImp", "Twin-Bolt Deadeye", "Two led bolts. Turn after the first.", "One bolt for where I am, one for where I went.", 4, 1.2, "#FFFFFF", "#BFE8FF", m_twinbolt),
    ("twinLensImp", "lensImp", "Twin Lens", "Two beams, one after the other. The red line comes first.", "Two lenses. Twice the squinting.", 5, 1.2, "#FF3A3A", "#FF4A4A", m_twinlens),
    ("phaseRaider", "blinkImp", "Phase Raider", "Blinks in behind you and fires twice. Leave when the mark shows.", "Never where I looked last.", 5, 1.2, "#D8A8FF", "#C58BFF", m_raider),
    ("broodWhelp", "demonWhelp", "Brood Whelp", "Pops into two more whelps. Count them.", "Born pregnant with whelps. Great.", 4, 1.5, "#FFB09A", "#FF8A3A", m_brood),
    ("furnacePlate", "plateDemon", "Furnace Plate", "Hot armor, and it burns near. Go around.", "A stove that walks.", 10, 1.2, "#C04A2A", "#FF8A3A", m_furnace),
    ("tripleLobber", "bounceLobber", "Triple Lobber", "Three lumps that bounce. Wait for them.", "Three times the bounce.", 5, 1.25, "#E8C898", "#FFD27A", m_triple),
    ("twiceRisen", "risenDemon", "Twice-Risen", "Gets up twice. Don't stop shooting.", "Fell over. Twice. Got up.", 6, 1.2, "#9A7A8A", "#FFA05A", m_twice),
    ("veilMatron", "veilDemon", "Veil Matron", "Shielded, blinks and burns. Hit it from the side.", "A curtain that bites.", 8, 1.25, "#A890E0", "#C8A8FF", m_matron),
]

for _id, _b, _n, _h, *_ in ELITES:
    voice(f"vo_hero_hint_{_id}", "hero", _h)


def build_elite(row):
    eid, base_id, name, hint, note, health, scale, color, fx, mutate = row
    base = load(f"content/entities/{base_id}.json")
    d = {"schemaVersion": 1, "displayName": name, "hint": hint, "note": note, "hintVoice": f"vo_hero_hint_{eid}",
         "onSpawn": [{"type": "hintOnce"}], "extends": base_id, "health": health}
    d["tags"] = sorted(set(base.get("tags", [])) | {"elite", eid})
    # the base file may extend another one for its scale: read the chain
    base_scale = 1.0
    cur = base
    while True:
        if "visual" in cur and "scale" in cur["visual"]:
            base_scale = cur["visual"]["scale"]
            break
        if "extends" not in cur:
            break
        cur = load(f"content/entities/{cur['extends']}.json")
    d["visual"] = {"scale": round(base_scale * scale, 3), "color": color, "fxColor": fx}
    d["healthBar"] = {"show": True, "color": [1.0, 0.82, 0.3, 1.0], "width": 0.6}
    d["loot"] = {"chance": 1.0, "count": 2, "value": 1}
    d["drops"] = [{"entity": "rewindOrb", "chance": 0.5}]
    d["abilities"] = copy.deepcopy(base["abilities"])
    if "moveSpeed" in base:
        d["moveSpeed"] = base["moveSpeed"]
    mutate(d)
    d["sounds"] = copy.deepcopy(base["sounds"])
    save(f"content/entities/{eid}.json", d)


# ─── the four mid-bosses ───────────────────────────────────────────────────────────────────────────────

GLOW = "mkWarnHand"


def shoot(projectile, muzzle, delay=0.2, aim="Down", speed=0.0, **extra):
    a = {"type": "shoot", "projectile": projectile, "speed": speed, "aim": aim, "muzzle": muzzle, "interval": 99, "initialDelay": delay}
    a.update(extra)
    return a


def fan(muzzle, count, angle):
    return [shoot(GLOW, muzzle, 0.0), shoot("ricochetEmber", muzzle, 1.2, "Player", 4.0, pattern={"type": "spread", "count": count, "angle": angle})]


def lead(muzzle, burst):
    return [shoot(GLOW, muzzle, 0.0), shoot("deadeyeBolt", muzzle, 1.2, "Predictive", 6.0, burst=burst, burstInterval=0.28)]


def lumps(muzzle, count):
    return [shoot(GLOW, muzzle, 0.0), shoot("bounceLump", muzzle, 1.2, "Player", 3.4, pattern={"type": "spread", "count": count, "angle": 26})]


def spots(warn, xs):
    return [{"type": "spawn", "entity": warn, "anchor": "Player", "at": [x, -0.3], "facePlayer": False} for x in xs]


SLOTS = [(-3.0, -2.2), (3.2, -2.0), (0.6, -2.5), (-1.6, -1.2), (4.4, -1.0), (-4.2, -1.4)]


def spawners(mix, interval=3.4, first=0.5):
    return [{"type": "spawner", "entity": e, "interval": interval, "initialDelay": round(first + i * 0.55, 2), "maxAlive": cap,
             "offset": list(SLOTS[i % len(SLOTS)])} for i, (e, cap) in enumerate(mix)]


def go(to, seconds):
    return {"to": to, "when": {"type": "after", "seconds": seconds}}


def below(to, fraction):
    return {"to": to, "when": {"type": "healthBelow", "fraction": fraction}}


def state(name, abilities=None, then=None, on_enter=None, guards=None):
    s = {"name": name}
    if on_enter:
        s["onEnter"] = on_enter
    if abilities:
        s["abilities"] = abilities
    s["transitions"] = (guards or []) + (then or [])
    return s


def cycle(prefix, steps, guards):
    out = []
    for i, (name, abilities, seconds, *rest) in enumerate(steps):
        nxt = prefix + steps[(i + 1) % len(steps)][0]
        out.append(state(prefix + name, copy.deepcopy(abilities), [go(nxt, seconds)], copy.deepcopy(rest[0]) if rest else None, copy.deepcopy(guards)))
    return out


def emit(signal):
    return {"type": "emit", "signal": signal}


HOVER = {"type": "hover", "speed": 2.4, "offset": [3.4, 2.3], "keepSide": True, "bobHeight": 0.2, "bobSpeed": 1.6}
MOUTH = [0.2, 0.3]

MIDBOSSES = {
    "baronSpillway": dict(
        pre="bs", floor=955, name="Baron Spillway", extends="impGuard", health=30, scale=0.8, color="#B8603A", fx="#FFB040",
        sounds="pileKeeper", warnColor="#FFB060", hitColor="#FF8A3A", burstSound="lava_burst",
        hint="Embers off the walls, marks glowing on the floor. Wait for the glow.",
        note="Keeps the Falls. Charges a toll on everything that falls.",
        title_text="BARON SPILLWAY! HE KEEPS THE FALLS!", title_say="Baron Spillway! He keeps the Falls!",
        intro="Halt. Everything that falls in these Falls pays the Baron.",
        hero_text="Baron. Embers bounce, marks glow first. I can read that.",
        wave_line="Lads! Collect the toll!", rage_line="Nobody. Gets. Past. The Falls.",
        down_line="The Falls... run... dry...", down_banner="BARON SPILLWAY HAS RUN DRY!", hero_end="He ran dry. Up I go.",
        wave=[("haloImp", 3), ("slagHound", 3), ("deadeyeImp", 3)],
        rage=[("haloImp", 5), ("ricochetImp", 5), ("whirlImp", 5), ("coronaImp", 5), ("slagHound", 5)],
        spots=[0.0], spots2=[-2.4, 0.0, 2.4], special=lambda m: [], rageSpecial=lambda m: lead(m, 3)),
    "kilnMatron": dict(
        pre="km", floor=905, name="The Kiln Matron", extends="impGuard", health=32, scale=0.85, color="#C890E8", fx="#FFA060",
        sounds="impForeman", warnColor="#FFC0F0", hitColor="#E070C0", burstSound="lava_burst",
        hint="She blinks in next to you, then the beam. Leave first.",
        note="Bakes the Heat Stairs. They are still hot.",
        title_text="THE KILN MATRON! SHE BAKES THE STAIRS!", title_say="The Kiln Matron! She bakes the stairs!",
        intro="Mind the step, dear. I only just baked it.",
        hero_text="She blinks in, then the beam. I leave first.",
        wave_line="Come, my embers. Supper's hot.", rage_line="Too RAW! Everything is too RAW!",
        down_line="Cold... at last...", down_banner="THE KILN MATRON HAS GONE COLD!", hero_end="Stairs are cooling. Nice of her.",
        wave=[("impGuard", 3), ("bulwarkImp", 3), ("lensImp", 3)],
        rage=[("impGuard", 5), ("twinLensImp", 5), ("phaseRaider", 5), ("bulwarkImp", 5), ("leadImp", 5)],
        spots=[0.0], spots2=[-2.4, 0.0, 2.4],
        special=lambda m: [{"type": "teleport", "to": "NearPlayer", "distance": 3.2, "interval": 99, "initialDelay": 1.6, "telegraph": 0.9,
                            "marker": "blinkMark", "flashColor": "#C58BFF", "sound": "banner_whoosh"}],
        rageSpecial=lambda m: [{"type": "laserSweep", "damage": 1, "interval": 99, "initialDelay": 0.8, "warning": 1.3, "sweepTime": 1.6,
                                "sweepDegrees": 80, "aimAtPlayer": True, "length": 10, "width": 0.4, "holdPosition": False,
                                "warningSound": "overlord_beam_charge", "beamSound": "overlord_beam_strike"}]),
    "motherFissure": dict(
        pre="mf", floor=895, name="Mother Fissure", extends="brimstoneDemon", health=32, scale=0.55, color="#C05030", fx="#FF6A1F",
        sounds="roadBreaker", warnColor="#FF9A6A", hitColor="#FF5A2A", burstSound="lava_burst",
        hint="Kill the whelps, they keep coming. Hit the mother.",
        note="The crack that breeds the Road's whelps.",
        title_text="MOTHER FISSURE! SHE BREEDS THE ROAD'S WHELPS!", title_say="Mother Fissure! She breeds the Road's whelps!",
        intro="Hsss. Come out, my little cracks. We have a guest.",
        hero_text="Whelps keep coming. I hit the mother.",
        wave_line="Out, out, out!", rage_line="NOBODY touches my brood!",
        down_line="My... cracks...", down_banner="MOTHER FISSURE HAS SEALED SHUT!", hero_end="Sealed. One crack fewer.",
        wave=[("demonWhelp", 3), ("fuseWhelp", 3), ("bounceLobber", 3)],
        rage=[("broodWhelp", 5), ("fuseWhelp", 5), ("tripleLobber", 5), ("demonWhelp", 5), ("plateDemon", 5)],
        spots=[0.0], spots2=[-2.4, 0.0, 2.4], special=lambda m: lumps(m, 3), rageSpecial=lambda m: lumps(m, 5)),
    "shardwright": dict(
        pre="sw", floor=855, name="The Shardwright", extends="brimstoneDemon", health=34, scale=0.5, color="#5A5870", fx="#C8C0E0",
        sounds="carver", warnColor="#E0D8F0", hitColor="#B8B0D8", burstSound="lava_burst",
        hint="Bolts aimed ahead of me, marks glowing on the floor. Stop being ahead.",
        note="The Carver's apprentice. Carves visitors.",
        title_text="THE SHARDWRIGHT! THE CARVER'S APPRENTICE!", title_say="The Shardwright! The Carver's apprentice!",
        intro="The master carves the rules. I carve the visitors.",
        hero_text="Bolts aimed ahead of me. So I stop being ahead.",
        wave_line="Chips, my chips. Fall!", rage_line="Cut. Him. DOWN.",
        down_line="Chipped... my... chisel...", down_banner="THE SHARDWRIGHT IS CARVED DOWN!", hero_end="Not a masterpiece.",
        wave=[("plateDemon", 3), ("slabSniper", 3), ("demonWhelp", 3)],
        rage=[("veilMatron", 5), ("slabSniper", 5), ("furnacePlate", 5), ("twiceRisen", 5), ("fuseWhelp", 5)],
        spots=[0.0], spots2=[-2.4, 0.0, 2.4],
        special=lambda m: [{"type": "teleport", "to": "NearPlayer", "distance": 3.4, "behind": True, "interval": 99, "initialDelay": 1.6, "telegraph": 0.9,
                            "marker": "blinkMark", "flashColor": "#C8C0E0", "sound": "banner_whoosh"}],
        rageSpecial=lambda m: lead(m, 5)),
}

for _id, _c in MIDBOSSES.items():
    voice(f"vo_mb_{_c['pre']}_title", "announcer", _c["title_say"])
    voice(f"vo_mb_{_c['pre']}_hero", "hero", _c["hero_text"])
    voice(f"vo_mb_{_c['pre']}_end", "hero", _c["hero_end"])


def build_midboss(eid, c):
    pre, name = c["pre"], c["name"]
    warn, hit = f"{pre}Warn", f"{pre}Hit"
    save(f"content/entities/{hit}.json", {"schemaVersion": 1, "extends": "fmLash", "visual": {"placeholderColor": c["hitColor"]}})
    save(f"content/entities/{warn}.json", {
        "schemaVersion": 1, "extends": "fmWarnWhip", "visual": {"color": c["warnColor"]},
        "onDeath": [{"type": "spawn", "entity": hit, "anchor": "Self", "facePlayer": False}, {"type": "playSound", "sound": c["burstSound"]}]})

    wave_types = [t for t, _ in c["wave"]]
    rage_types = [t for t, _ in c["rage"]]
    all_types = list(dict.fromkeys(wave_types + rage_types))
    g_wave = [below("waveEnter", 0.7), ]
    g_rage = [below("rageEnter", 0.35)]
    sp_wave = spawners(c["wave"])
    sp_rage = spawners(c["rage"], 3.0, 0.4)
    spot1, spot2 = spots(warn, c["spots"]), spots(warn, c["spots2"])

    def ab_(extra):
        return [copy.deepcopy(HOVER)] + extra

    states = [state("intro", None, [{"to": "p1Fan", "when": {"type": "signal", "name": pre + "Fight"}}])]
    p1 = [("Fan", ab_(fan(MOUTH, 3, 40) + c["special"](MOUTH)), 3.6),
          ("Spot", ab_([]), 1.8, spot1),
          ("Lead", ab_(lead(MOUTH, 2) + c["special"](MOUTH)), 3.4),
          ("Spot2", ab_([]), 1.8, spots(warn, [-1.8, 1.8]))]
    states += cycle("p1", p1, [below("waveEnter", 0.7)])
    states.append(state("waveEnter", ab_([]), [go("waveFan", 0.5)], [emit(pre + "Wave"), {"type": "playSound", "sound": "magma_rumble"}], g_rage))
    wv = [("Fan", ab_(fan(MOUTH, 3, 46) + c["special"](MOUTH) + sp_wave), 3.6),
          ("Spot", ab_(sp_wave), 1.8, spot2),
          ("Lead", ab_(lead(MOUTH, 3) + c["special"](MOUTH) + sp_wave), 3.4),
          ("Spot2", ab_(sp_wave), 1.8, spot1)]
    states += cycle("wave", wv, g_rage)
    kills = [{"type": "despawn", "target": {"entity": t}} for t in all_types if t in wave_types and t not in rage_types]
    states.append(state("rageEnter", ab_([]), [go("rageFan", 0.8)], [emit(pre + "Rage"), {"type": "playSound", "sound": "magma_rumble"}] + kills))
    rg = [("Fan", ab_(fan(MOUTH, 5, 64) + sp_rage), 3.4),
          ("Spot", ab_(sp_rage), 1.6, spot2),
          ("Special", ab_(c["rageSpecial"](MOUTH) + sp_rage), 5.0),
          ("Lead", ab_(lead(MOUTH, 4) + c["special"](MOUTH) + sp_rage), 3.4),
          ("Spot2", ab_(sp_rage), 1.6, spot2)]
    states += cycle("rage", rg, [])

    base = c["extends"]
    d = {"schemaVersion": 1, "displayName": name, "hint": c["hint"], "note": c["note"], "onSpawn": [], "extends": base, "team": "Enemy",
         "health": c["health"], "moveSpeed": 0, "activationRange": 0, "tags": ["boss", "midBoss", eid],
         "body": {"shape": "Circle", "radius": 0.6, "solid": False, "gravity": 0}, "healthBar": {"show": False},
         "visual": {"scale": c["scale"], "color": c["color"], "fxColor": c["fx"], "deathPunch": 0.35, "deathFlash": True, "deathBits": 30, "deathFade": 0.8},
         "loot": {"chance": 1, "count": 5, "value": 1}, "drops": [{"entity": "rewindOrb", "chance": 1.0}],
         "abilities": [{"type": "faceTarget"}, {"type": "contactDamage", "damage": 1}],
         "states": states,
         "onDeath": [{"type": "despawn", "target": {"entity": t}} for t in all_types],
         "sounds": {"hurtSound": c["sounds"] + "_hurt", "deathSound": c["sounds"] + "_die", "attackSound": c["sounds"] + "_attack"}}
    save(f"content/entities/{eid}.json", d)


# ─── placement ─────────────────────────────────────────────────────────────────────────────────────────
# floor -> [(section, elite, first delay)]: an `every` rule inside the section's phase (or a one-off rule for a floor without phases)

PLACEMENT = {
    995: [("lapThree", "quickfireImp"), ("lapFive", "coronaImp")],
    955: [("waitForIt", "ironhideBulwark"), ("threeAtOnce", "alphaHound")],
    905: [("collapse", "twinBoltDeadeye"), ("updraft", "twinLensImp"), ("landing", "phaseRaider")],
    895: [("TheHeave1", "broodWhelp"), ("UpAndDown3", "furnacePlate")],
    855: [("YouAreHere1", "tripleLobber"), ("TheSignKnows3", "twiceRisen")],
    805: [("RunAndJump3", "veilMatron"), ("TheBreakaway1", "broodWhelp")],
}
BOSS_SECTION = {955: "slippingThrough", 905: "ledgeRun", 895: "TheLastHeave5", 855: "StillHere5"}
BOSS_OF = {c["floor"]: (eid, c) for eid, c in MIDBOSSES.items()}
ELITE_NAMES = {row[0]: row[2] for row in ELITES}


def banner_color(floor):
    return "#FFD27A"


def midboss_phases(eid, c, hazard_ids, after):
    pre, floor = c["pre"], c["floor"]
    stops = [{"type": "stopHazard", "id": h} for h in sorted(hazard_ids)]
    hero = {"type": "showText", "text": c["hero_text"], "duration": 3.4, "style": "Subtitle", "sound": f"vo_mb_{pre}_hero"}
    enter = stops + [{"type": "despawn", "target": {"tag": "wave"}}, {"type": "despawn", "target": {"tag": "brute"}},
                     {"type": "lockTravel"}, {"type": "playerInvincible", "seconds": 16.0},
                     {"type": "runSequence", "steps": [
                         {"type": "lockInput"}, {"type": "lockRewind"},
                         {"type": "spawn", "entity": eid, "anchor": "Camera", "at": [4.2, 2.2], "id": "midboss"},
                         {"type": "emit", "signal": pre + "Intro"}, {"type": "cameraShake", "intensity": 0.14, "duration": 0.5},
                         {"type": "playSound", "sound": "magma_rumble"}, {"type": "wait", "seconds": 0.8},
                         {"type": "showText", "text": c["title_text"], "duration": 2.8, "style": "Banner", "sound": f"vo_mb_{pre}_title", "color": banner_color(floor)},
                         {"type": "wait", "seconds": 3.0},
                         {"type": "showText", "text": c["intro"], "duration": 3.0, "style": "Subtitle", "color": "#FFB040"},
                         {"type": "wait", "seconds": 3.2}, hero, {"type": "wait", "seconds": 3.6},
                         {"type": "bossBar", "target": {"id": "midboss"}, "label": c["name"].upper()},
                         {"type": "unlockInput"}, {"type": "unlockRewind"}, {"type": "emit", "signal": pre + "Fight"}]}]
    rules = [
        {"name": f"{eid}: falls", "when": {"type": "allDead", "target": {"id": "midboss"}}, "maxTimes": 1, "do": [{"type": "goToPhase", "phase": f"midBossDefeat{floor}"}]},
        {"name": f"{eid}: his wave", "when": {"type": "signal", "name": pre + "Wave"}, "maxTimes": 1,
         "do": [{"type": "cameraShake", "intensity": 0.18, "duration": 0.7},
                {"type": "showText", "text": c["wave_line"], "duration": 2.6, "style": "Subtitle", "color": "#FFB040"}]},
        {"name": f"{eid}: his rage", "when": {"type": "signal", "name": pre + "Rage"}, "maxTimes": 1,
         "do": [{"type": "cameraShake", "intensity": 0.3, "duration": 0.7},
                {"type": "showText", "text": c["rage_line"], "duration": 2.6, "style": "Subtitle", "color": "#FF8A3A"}]},
    ]
    defeat = [{"type": "runSequence", "steps": [
        {"type": "emit", "signal": pre + "Defeat"}, {"type": "hideBossBar"},
        {"type": "slowMotion", "scale": 0.35, "duration": 1.2}, {"type": "cameraShake", "intensity": 0.25, "duration": 0.8},
        {"type": "spawn", "entity": "diamond", "anchor": "Camera", "at": [0.4, -0.2], "count": 8, "spacing": [0.35, 0.0], "facePlayer": False},
        {"type": "showText", "text": c["down_line"], "duration": 2.4, "style": "Subtitle", "color": "#FFB040"},
        {"type": "wait", "seconds": 2.6},
        {"type": "showText", "text": c["down_banner"], "duration": 3.0, "style": "Banner", "color": banner_color(floor)},
        {"type": "wait", "seconds": 3.2},
        {"type": "showText", "text": c["hero_end"], "duration": 2.6, "style": "Subtitle", "sound": f"vo_mb_{pre}_end"},
        {"type": "wait", "seconds": 2.8},
        {"type": "setCounter", "name": "midboss", "value": 1.0}, {"type": "unlockTravel"}, {"type": "goToPhase", "phase": after}]}]
    return [{"name": f"midBoss{floor}", "onEnter": enter, "rules": rules},
            {"name": f"midBossDefeat{floor}", "onEnter": defeat},
            {"name": f"afterMidBoss{floor}"}]


def elite_rule(elite, section, first=6.0):
    return {"name": f"elite: {ELITE_NAMES[elite]}", "when": {"type": "every", "seconds": 24.0, "startAfter": first, "maxTimes": 3},
            "do": [{"type": "spawnWave", "entity": elite, "count": 1, "interval": 0.8, "anchor": "Camera", "areaCenter": [0.0, 2.2],
                    "areaSize": [14.0, 1.5], "tags": ["elite"]}]}


def build_floor(floor):
    name = f"Floor{floor}"
    lv = load(f"levels/{name}.json")
    s = lv["script"]
    phases = {p["name"]: p for p in s["phases"]}
    # take out what an earlier run wrote
    s["rules"] = [r for r in s["rules"] if not r["name"].startswith("elite:") and not r["name"].startswith("midboss:")]
    for p in s["phases"]:
        if "rules" in p:
            p["rules"] = [r for r in p["rules"] if not r["name"].startswith("elite:")]
    boss_names = {f"midBoss{floor}", f"midBossDefeat{floor}", f"afterMidBoss{floor}"}
    s["phases"] = [p for p in s["phases"] if p["name"] not in boss_names]
    s["counters"] = [c for c in s["counters"] if c["name"] != "midboss"]
    for section, elite in PLACEMENT[floor]:
        if section in phases and section != BOSS_SECTION.get(floor):
            phases[section].setdefault("rules", []).append(elite_rule(elite, section))
        else:
            s["rules"].append({"name": f"elite: {ELITE_NAMES[elite]}, {section}", "when": {"type": "sectionReached", "section": section}, "maxTimes": 1,
                               "do": [{"type": "spawnWave", "entity": elite, "count": 1, "interval": 0.8, "anchor": "Camera",
                                       "areaCenter": [0.0, 2.2], "areaSize": [14.0, 1.5], "tags": ["elite"]}]})

    if floor in BOSS_OF:
        eid, c = BOSS_OF[floor]
        section = BOSS_SECTION[floor]
        hazards = {a["id"] for p in s["phases"] for a in p.get("onEnter", []) if a.get("type") == "startHazard"}
        after = section if section in phases else f"afterMidBoss{floor}"
        s["phases"] += midboss_phases(eid, c, hazards, after)
        s["counters"].append({"name": "midboss", "value": 0.0})
        # reaching the boss section goes to the fight (replacing the setpiece's own goToPhase), the landing waits for the fight
        found = False
        for r in s["rules"]:
            if r["when"].get("type") == "sectionReached" and r["when"].get("section") == section and r["name"].startswith("to "):
                for a in r["do"]:
                    if a.get("type") == "goToPhase":
                        a["phase"] = f"midBoss{floor}"
                        found = True
        if not found:
            s["rules"].append({"name": f"midboss: {c['name']} stands on the way", "when": {"type": "sectionReached", "section": section}, "maxTimes": 1,
                               "do": [{"type": "goToPhase", "phase": f"midBoss{floor}"}]})
        landing = next(r for r in s["rules"] if r["name"] == "the landing")
        gate = {"type": "counter", "name": "midboss", "atLeast": 1.0}
        w = landing["when"]
        if w.get("type") == "allOf":
            w["triggers"] = [t for t in w["triggers"] if t.get("name") != "midboss"] + [gate]
        else:
            landing["when"] = {"type": "allOf", "triggers": [w, gate]}
        lv["duration"] = max(lv["duration"], 520.0)
    save(f"levels/{name}.json", lv)


# the "to <section>" rules for sections whose phase had hazards would keep running the old phase's hazards: handled by stopHazard on entry


# ─── the test arena ────────────────────────────────────────────────────────────────────────────────────

def build_arena():
    stations = [(row[0], row[2], "elite") for row in ELITES] + [(eid, c["name"], "boss") for eid, c in MIDBOSSES.items()]
    phases = []
    for i, (eid, name, kind) in enumerate(stations):
        nxt = stations[i + 1][0] if i + 1 < len(stations) else "done"
        clear = [{"type": "despawn", "target": {"tag": "station"}}, {"type": "despawn", "target": {"tag": "wave"}}]
        if kind == "boss":
            c = MIDBOSSES[eid]
            for t in dict.fromkeys([t for t, _ in c["wave"]] + [t for t, _ in c["rage"]]):
                clear.append({"type": "despawn", "target": {"entity": t}})
        hero_say = f"vo_hero_hint_{eid}" if kind == "elite" else f"vo_mb_{MIDBOSSES[eid]['pre']}_hero"
        hero_text = next(r[3] for r in ELITES if r[0] == eid) if kind == "elite" else MIDBOSSES[eid]["hero_text"]
        on_enter = clear + [{"type": "heal", "target": {"player": True}, "amount": 5},
                            {"type": "showText", "text": hero_text, "duration": 3.0, "style": "Subtitle", "sound": hero_say},
                            {"type": "spawn", "entity": eid, "anchor": "Camera", "at": [3.6, -1.6 if kind == "elite" else 1.8], "tags": ["station"],
                             "facePlayer": True, "id": "station"}
                            if kind == "elite" else
                            {"type": "spawn", "entity": eid, "anchor": "Camera", "at": [4.0, 2.0], "tags": ["station"], "id": "midboss"},
                            {"type": "emit", "signal": f"elite:{eid}"}]
        if kind == "boss":
            on_enter.append({"type": "emit", "signal": MIDBOSSES[eid]["pre"] + "Fight"})
        limit = 90 if kind == "boss" else 30
        phases.append({"name": eid, "onEnter": on_enter,
                       "rules": [{"name": f"{eid} beaten", "when": {"type": "allDead", "target": {"tag": "station"}}, "do": clear + [{"type": "goToPhase", "phase": nxt}]},
                                 {"name": f"{eid} took too long", "when": {"type": "after", "seconds": limit}, "do": clear + [{"type": "goToPhase", "phase": nxt}]}]})
    phases.append({"name": "done", "onEnter": [{"type": "emit", "signal": "elite:done"}]})
    save("content/scenes/eliteArena.json", {"schemaVersion": 1, "seed": 9031, "length": 1200,
                                            "objective": {"text": "Twelve elites and four mid-bosses, one at a time"}, "phases": phases})


# ─── audio.json ────────────────────────────────────────────────────────────────────────────────────────

def build_audio():
    a = load("audio.json")
    ids = {s["id"]: i for i, s in enumerate(a["sounds"])}
    for sid, (who, vid, prompt) in VOICES.items():
        entry = {"id": sid, "kind": "Voice", "prompt": prompt, "durationSeconds": 0, "promptInfluence": 0.3, "loop": False, "voice": who, "voiceId": "",
                 "clip": f"Audio/Generated/{sid}.mp3", "generatedFrom": f"voice|{vid}|eleven_v3|0.5|1|{prompt}", "volume": 1, "pitchVariation": 0,
                 "maxVoices": 1, "cooldown": 0.5}
        if sid in ids:
            a["sounds"][ids[sid]] = entry
        else:
            a["sounds"].append(entry)
    save("audio.json", a)



# ─── art recipes (specs only; not generated, GPU priced in the summary) ─────────────────────────────────

ART = {
    "quickfireImp": ("a wiry hell imp, faster and leaner than the others, ember-gold skin, two ember-claws raised, a trail of sparks behind its wings", 0.85),
    "coronaImp": ("a hell imp ringed by a floating halo of white-hot embers that scorches the air around it, pale gold skin", 0.9),
    "ironhideBulwark": ("a squat imp bolted into a full suit of riveted iron plate, shield-like pauldrons, only red eyes showing", 0.95),
    "alphaHound": ("a huge slag hound, pack leader, cracked basalt hide with a mane of molten slag, glowing cinders trapped in its ribs", 0.8),
    "twinBoltDeadeye": ("a white-skinned imp with two long thin crossbow-like bolt-spitters, one over each shoulder, a single huge eye", 0.85),
    "twinLensImp": ("an imp carrying two big red glass lenses on its arms, aimed apart, beams of red light leaking from the rims", 0.85),
    "phaseRaider": ("a violet imp half-faded into smoke at the edges, glowing violet eyes, two small embers in its hands", 0.85),
    "broodWhelp": ("a swollen brimstone whelp with a split, glowing belly where two smaller whelps can be seen curled inside", 0.55),
    "furnacePlate": ("a hulking basalt demon behind a glowing red-hot iron plate like a furnace door, heat haze around it", 1.0),
    "tripleLobber": ("a stout road-demon with three glowing lumps of stone balanced on its back and arms, ready to throw", 0.8),
    "twiceRisen": ("a scarred, twice-broken demon with chains of cooled lava wound round its limbs, one horn snapped, eyes burning", 0.9),
    "veilMatron": ("a tall veiled demon matron in layers of violet smoke-cloth, hot coals glowing through the weave, faceless", 1.0),
    "baronSpillway": ("an imp baron in a cape of cooled slag, a crown of dripping lava, a little lava fall pouring off one shoulder, smug", 1.5),
    "kilnMatron": ("an ancient imp crone with a kiln for a belly, open door glowing, soot-black apron of baked clay, long arms", 1.5),
    "motherFissure": ("a great cracked demon like a living fissure, glowing split down its chest, whelps crawling from the cracks", 1.5),
    "shardwright": ("a gaunt carver demon with a chisel and hammer, arms like black glass, shards of rock orbiting it, a cloak of chips", 1.5),
}


def build_art():
    for eid, (desc, height) in ART.items():
        spec = {"schemaVersion": 1, "entity": eid, "name": ELITE_NAMES.get(eid) or MIDBOSSES[eid]["name"], "description": desc, "height": height,
                "poses": {"stand": "standing ready, facing right, glowing with heat", "struck": "struck and flinching, sparks flying",
                          "dead": "collapsed and breaking apart in a burst of embers"},
                "clips": {"idle": {"from": "stand", "action": "breathes and shifts in place, glow pulsing slowly", "seconds": 2},
                          "attack": {"from": "stand", "action": "performs its signature attack toward the right, glow flaring", "seconds": 2.0},
                          "hurt": {"from": "stand", "action": "is struck, jerks back, then settles", "seconds": 1},
                          "die": {"from": "struck", "to": "dead", "action": "flares bright, breaks apart in a burst of embers and goes still", "seconds": 1.25}},
                "takes": {"pose": 3, "clip": 3}}
        save(f"art/{eid}.json", spec)


# ─── run ───────────────────────────────────────────────────────────────────────────────────────────────

for row in ELITES:
    build_elite(row)
for eid, c in MIDBOSSES.items():
    build_midboss(eid, c)
for floor in PLACEMENT:
    build_floor(floor)
build_arena()
build_audio()
build_art()

# placement check: ten+ elites on floors, four mid-bosses on floors
placed = {e for rows in PLACEMENT.values() for _, e in rows}
problems = []
if len(ELITES) < 10 or len(placed) < 10:
    problems.append(f"only {len(placed)} elites placed")
for eid in [r[0] for r in ELITES]:
    if eid not in placed and eid not in {t for c in MIDBOSSES.values() for t, _ in c["wave"] + c["rage"]}:
        problems.append(f"{eid} is on no floor")
for floor in BOSS_OF:
    s = load(f"levels/Floor{floor}.json")["script"]
    if not any(p["name"] == f"midBoss{floor}" for p in s["phases"]):
        problems.append(f"Floor {floor} has no mid-boss phase")
if problems:
    print("problems:", "; ".join(problems))
    sys.exit(1)
if check_only and changed:
    print("out of date:", ", ".join(changed))
    sys.exit(1)
print(("would change: " if check_only else "wrote: ") + (", ".join(changed) if changed else "nothing"))
