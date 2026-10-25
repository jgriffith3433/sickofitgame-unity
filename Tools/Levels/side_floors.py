#!/usr/bin/env python3
"""
Side floors: Hell's secret floors and gauntlets (Docs/Story.md "Secret floors and gauntlets", Docs/LevelDesign.md section 7,
cookbook "A secret floor").

A side floor is a short level a hero reaches through a *door in the wall* on a floor of the played route: an `offerShortcut`
at the last foothold before the host floor's landing. Taken, the host floor ends there (no Grudge pick: the door cost
something), the side floor plays on the floor the host would have ended on, and its `nextLevelNumber` is the host's, so the
climb goes on exactly where it was. Nothing on the route is skipped; only the host's last few footholds and its Grudge pick.

Run from the repo root; it is idempotent:

  python3 Tools/Levels/side_floors.py            write levels, content, patch hosts and game.json
  python3 Tools/Levels/side_floors.py --check    only check the data (entities, phases, positions); exit 1 on a problem

To add one: append a row to ROOMS (a room builder and its door), run it, add the story to Docs/Story.md. Everything below is
data written from those tables; never hand-edit Secret*.json / Gauntlet*.json or the "SIDE DOOR" rules in the hosts.
"""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONFIG = os.path.join(ROOT, "Assets", "GameConfig")
LEVELS = os.path.join(CONFIG, "levels")
ENTITIES = os.path.join(CONFIG, "content", "entities")

GOLD = "#FFD27A"
HOT = "#FF9A5C"
GREY = "#D2DAE4"
DOOR_PROMPT = "A door in the wall"      # PlaytestBot.SideDoorPrompt: the bot only opens these when asked
RULE_PREFIX = "SIDE DOOR"


# ───────────────────────── action helpers ─────────────────────────

def banner(text, seconds=2.8, color=GOLD):
    return {"type": "showText", "text": text, "duration": seconds, "style": "Banner", "color": color}

def hero(text, seconds=3.0):
    return {"type": "showText", "text": text, "duration": seconds, "style": "Subtitle"}

def voice(text, seconds=3.0, color=GREY):
    return {"type": "showText", "text": text, "duration": seconds, "style": "Subtitle", "color": color}

def wait(seconds):
    return {"type": "wait", "seconds": seconds}

def emit(signal):
    return {"type": "emit", "signal": signal}

def goto(phase):
    return {"type": "goToPhase", "phase": phase}

def seq(*steps):
    return {"type": "runSequence", "steps": list(steps)}

def pop(text, color=GOLD, y=1.0):
    return {"type": "popText", "text": text, "color": color, "at": [0.0, y], "anchor": "Player", "size": 0.95}

def wave(entity, count, points, tag="wave"):
    return {"type": "spawnWave", "entity": entity, "count": count, "points": points, "anchor": "Camera", "interval": 0.0, "tags": [tag]}

def when_dead(tag, target_phase, name):
    return {"name": name, "when": {"type": "allDead", "target": {"tag": tag}}, "maxTimes": 1, "do": [goto(target_phase)]}

def stragglers(tag, seconds):
    """No soft-lock: whatever still floats about leaves, which also ends the wave."""
    return {"name": "stragglers leave (no soft-lock)", "when": {"type": "after", "seconds": seconds}, "maxTimes": 1,
            "do": [{"type": "despawn", "target": {"tag": tag}}]}

def outro(kind, closing):
    """Every side floor ends the same way: the announcer, the hero's last word, the level's counter."""
    return seq(emit("side:reward:" + kind), wait(0.5),
               banner("SECRET FLOOR FOUND! NOBODY TOLD HIM!" if kind != "gauntlet" else "GAUNTLET CLEARED! THE CROWD IS HOARSE!", 2.8),
               wait(3.0), hero(closing, 2.8), wait(3.0), emit("side:cleared"), {"type": "setCounter", "name": "cleared", "value": 1})


# ───────────────────────── the rooms ─────────────────────────
# Each builder returns the phases of one side floor, the first being "enter". Lines are short, in the hero's deadpan or the
# announcer's boom; never office or work jokes. Rewards are paid by the room's own script (and once per play).

def enter(title, first, then):
    return {"name": "enter", "onEnter": [seq(emit("side:enter"), banner(title, 3.0), wait(3.2), hero(first, 3.0), wait(3.2), goto(then))]}

def tally_closet(floor):
    return [
        enter(f"FLOOR {floor} AND A HALF: THE TALLY CLOSET", "Not on the map. Not on any map.", "keepers"),
        {"name": "keepers", "onEnter": [emit("side:guard"), wave("imp", 2, [[-2.6, 0.9], [2.8, 1.2]], "guard")],
         "rules": [when_dead("guard", "scrap", "the keepers drop"), stragglers("guard", 50.0)]},
        {"name": "scrap", "onEnter": [seq(
            hero("Scratches on the wall. In fives. Thousands of them.", 3.2), wait(3.4),
            hero("Every group crossed out. A big hand. Bigger than anybody's.", 3.4), wait(3.6),
            hero("Last one's fresh. It says: AGAIN.", 3.0), wait(3.2),
            {"type": "setFlag", "flag": "lore.tally"}, pop("LORE: THE TALLY"),
            hero("He counts them too. Great. Now I can't even hate him properly.", 3.6), wait(3.8),
            outro("lore", "Back to the stairs. Counting."))]},
    ]

def armory(floor):
    return [
        enter(f"FLOOR {floor} AND A HALF: THE ARMORY NOBODY LOCKED", "A rack. Nothing on it but one ladle. Suspicious.", "guards"),
        {"name": "guards", "onEnter": [emit("side:guard"), wave("impGuard", 2, [[-2.8, 1.0], [2.8, 1.0]], "guard")],
         "rules": [when_dead("guard", "rack", "the guards drop"), stragglers("guard", 55.0)]},
        {"name": "rack", "onEnter": [
            seq(banner("SOMETHING IS ON THE RACK!", 2.2),
                {"type": "spawn", "entity": "slagLadlePickup", "anchor": "Player", "at": [1.8, 0.4], "facePlayer": False, "id": "ladle"})],
         "rules": [
             {"name": "he takes the ladle", "when": {"type": "counter", "name": "secretFound", "atLeast": 1}, "maxTimes": 1,
              "do": [seq(wait(1.5), outro("weapon", "I'm armed with a soup utensil. It's fine."))]},
             {"name": "the magnet is slow: it comes to him", "when": {"type": "after", "seconds": 25.0}, "maxTimes": 1,
              "do": [{"type": "unlockWeapon", "weapon": "slagLadle", "equip": True}, {"type": "setCounter", "name": "secretFound", "value": 1},
                     {"type": "despawn", "target": {"id": "ladle"}}]},
         ]},
    ]

def held_breath(floor):
    return [
        enter(f"FLOOR {floor} AND A HALF: THE HELD BREATH", "Steam. Thick. Hot. Don't breathe, don't blink.", "hold"),
        {"name": "hold", "onEnter": [
            emit("side:guard"), banner("HOLD IT! HOLD IT!", 2.2),
            {"type": "startHazard", "id": "steam", "projectile": "fireball", "pattern": {"type": "rain", "count": 1}, "speed": 3.2,
             "interval": 1.5, "initialDelay": 1.0, "rampPerSecond": 0.012}],
         "rules": [
             {"name": "cinders find him", "when": {"type": "every", "seconds": 7.0, "startAfter": 3.0, "maxTimes": 3}, "do": [
                 {"type": "spawnWave", "entity": "cinder", "count": 2, "interval": 0.8, "anchor": "Camera", "areaCenter": [0.0, 2.0],
                  "areaSize": [8.0, 1.2], "tags": ["wave"]}]},
             {"name": "twenty seconds, no breath", "when": {"type": "after", "seconds": 22.0}, "maxTimes": 1, "do": [goto("breath")]},
         ]},
        {"name": "breath", "onEnter": [
            {"type": "stopHazard", "id": "steam"}, {"type": "despawn", "target": {"tag": "wave"}},
            seq(banner("HE DIDN'T BREATHE! HE DID NOT BREATHE!", 2.6, HOT), wait(2.8),
                hero("Twenty seconds. I've held worse. Mostly opinions.", 3.0), wait(3.2),
                {"type": "grantUpgrade", "upgrade": "heldBreath"}, pop("HELD BREATH: +2 HEALTH, +2s REWIND", "#7FE8FF"), wait(2.0),
                outro("upgrade", "Hold on to that. ...The breath, I mean."))]},
    ]

def applause_room(floor):
    crowd = [{"type": "spawn", "entity": "cheerSoul", "anchor": "Camera", "at": [x, -2.5], "facePlayer": False} for x in (-4.2, -2.2, 2.0, 4.0)]
    return [
        enter(f"FLOOR {floor} AND A HALF: THE APPLAUSE ROOM", "A crowd. Of course there's a crowd.", "act"),
        {"name": "act", "onEnter": crowd + [seq(
            banner("LADIES, DEMONS AND OTHER DAMNED! THE HERO!", 3.0, HOT), wait(3.2),
            voice("HE IS STANDING! HE IS STANDING SO CONFIDENTLY!", 3.0), wait(3.2),
            hero("I'm just standing here.", 2.4), wait(2.8),
            banner("AND NOW, THE ACT: ONE IMP! WILL HE SHOOT IT?!", 3.0, HOT), wait(2.4),
            emit("side:guard"), wave("imp", 1, [[2.4, 0.9]], "guard"))],
         "rules": [when_dead("guard", "tips", "the act ends"), stragglers("guard", 45.0)]},
        {"name": "tips", "onEnter": [seq(
            banner("HE SHOT IT! THE CROWD HAS NEVER SEEN ANYTHING LIKE IT!", 3.2, HOT), wait(3.4),
            hero("It's an imp.", 2.0), wait(2.4),
            voice("TIPS FOR THE ARTIST!", 2.4, GOLD), wait(1.0),
            {"type": "addDiamonds", "amount": 20}, pop("TIPS: +20 DIAMONDS"), wait(2.6),
            outro("diamonds", "Best floor in Hell. Don't tell anyone."))]},
    ]

def rest_stop(floor):
    return [
        enter(f"FLOOR {floor} AND A HALF: THE REST STOP", "A bench. In Hell. Somebody planned this.", "sit"),
        {"name": "sit", "onEnter": [
            {"type": "spawn", "entity": "giverSoul", "anchor": "Player", "at": [2.2, 0.0], "facePlayer": False},
            {"type": "spawn", "entity": "damnedSoul", "anchor": "Camera", "at": [-2.0, -2.4], "facePlayer": False},
            seq(banner("HE'S SITTING. THE CROWD DOESN'T KNOW WHAT TO DO.", 3.2, HOT), wait(3.6),
                voice("Sit. ...Sit.", 2.2), wait(2.8),
                hero("He's right, though. I could sit.", 2.6), wait(3.0),
                {"type": "heal", "target": {"player": True}, "amount": 9},
                {"type": "addRewind", "seconds": 10.0, "text": "REWIND REFILLED"}, pop("REST: FULL HEALTH, REWIND REFILLED", "#9EFFE6"), wait(3.0),
                hero("Ten seconds of nothing. Hell hates this.", 2.8), wait(3.2),
                outro("refill", "Feet up. Feet down. Stairs."))]},
    ]

def guest_book(floor):
    return [
        enter(f"FLOOR {floor} AND A HALF: THE GUEST BOOK", "A book on a stand. Open. Heavy.", "guests"),
        {"name": "guests", "onEnter": [emit("side:guard"), voice("Mm-hm-hm. Do sign in, little spark.", 3.0, "#FF8A3D"),
                                       wave("cinder", 3, [[-3.0, 1.2], [0.0, 1.8], [3.0, 1.2]], "guard")],
         "rules": [when_dead("guard", "book", "the guests are seen out"), stragglers("guard", 50.0)]},
        {"name": "book", "onEnter": [seq(
            hero("Names. Hundreds. Each one: CAME. SAW. LEFT.", 3.2), wait(3.4),
            hero("Every one of them is in the same handwriting. He wrote them himself.", 3.6), wait(3.8),
            hero("Last line's wet. It says: LITTLE SPARK. He wrote mine before I got here.", 4.0), wait(4.2),
            {"type": "setFlag", "flag": "lore.guestbook"}, pop("LORE: THE GUEST BOOK"),
            hero("Fine. I'll sign it. Just not his way.", 3.0), wait(3.4),
            outro("lore", "CAME. SAW. LEFT. ...Stayed up."))]},
    ]

def gauntlet(waves, title_line, first, refill_lines):
    """Waves chained with a rewind refill between them (Docs/Story.md). 'waves' is a list of lists of (entity, count, points)."""
    def build(floor):
        phases = [enter(title_line.replace("{floor}", str(floor)), first, "wave1")]
        for i, parts in enumerate(waves, 1):
            last = i == len(waves)
            nxt = "reward" if last else f"refill{i}"
            on_enter = [emit(f"side:wave:{i}"), banner(f"WAVE {i}!" if not last else f"LAST WAVE! WAVE {i}!", 2.0, HOT)]
            on_enter += [wave(e, c, pts, "wave") for e, c, pts in parts]
            phases.append({"name": f"wave{i}", "onEnter": on_enter, "rules": [when_dead("wave", nxt, f"wave {i} is down"), stragglers("wave", 55.0)]})
            if not last:
                phases.append({"name": f"refill{i}", "onEnter": [seq(
                    emit(f"side:refill:{i}"), banner(f"WAVE {i} DOWN! REWIND REFILLED!", 2.4), {"type": "addRewind", "seconds": 10.0, "text": "REWIND REFILLED"},
                    voice(refill_lines[(i - 1) % len(refill_lines)], 2.4), wait(3.4), goto(f"wave{i + 1}"))]})
        return phases
    return build

CAGE_WAVES = [
    [("imp", 3, [[-3.2, 1.0], [0.0, 1.8], [3.2, 1.0]])],
    [("impGuard", 2, [[-3.0, 1.1], [3.0, 1.1]])],
    [("cinder", 3, [[-3.4, 1.4], [0.0, 2.0], [3.4, 1.4]]), ("imp", 1, [[0.0, 0.8]])],
    [("impGuard", 2, [[-3.0, 1.1], [3.0, 1.1]]), ("imp", 2, [[-1.4, 1.8], [1.4, 1.8]])],
]
ENCORE_WAVES = [
    [("imp", 2, [[-2.8, 1.0], [2.8, 1.0]])],
    [("imp", 3, [[-3.2, 1.0], [0.0, 1.8], [3.2, 1.0]])],
    [("imp", 2, [[-3.0, 1.4], [3.0, 1.4]]), ("impGuard", 1, [[0.0, 1.0]])],
    [("cinder", 3, [[-3.4, 1.4], [0.0, 2.0], [3.4, 1.4]]), ("imp", 1, [[0.0, 0.8]])],
    [("impGuard", 3, [[-3.4, 1.0], [0.0, 1.8], [3.4, 1.0]]), ("imp", 1, [[0.0, 0.8]])],
]

def with_reward(build, kind, reward_steps, closing):
    def go(floor):
        phases = build(floor)
        phases.append({"name": "reward", "onEnter": [seq(banner("THE CAGE OPENS!" if kind == "cage" else "ENCORE! ENCORE! THAT'S ALL OF IT!", 2.6, HOT), wait(2.8),
                                                         *reward_steps, wait(2.4), outro("gauntlet", closing))]})
        return phases
    return go


# (key, level file, level number = position in game.json, display title, the host floor, the floor the side floor is on, builder, ...)
# 'door' = what the host's door offers. Costs are one of: diamonds N | grudge | risk text.
ROOMS = [
    dict(key="tallyCloset", file="Secret1", number=89, title="The Tally Closet", host=906, kind="lore", build=tally_closet,
         door=dict(prompt="a closet", title="Open the closet", text="A seam in the stone, hand-sized. Hell won't say what's behind it.",
                   cost=("diamonds", 5), see="That wall has a seam. Walls shouldn't have seams.", take="Five diamonds. For a closet. Sure.",
                   announcer="A SECRET DOOR?! HELL HAS BEEN KEEPING SECRETS!", leave="Nope. Nothing good is behind a seam.")),
    dict(key="armoryDoor", file="Secret2", number=90, title="The Armory Nobody Locked", host=910, kind="weapon", build=armory,
         door=dict(prompt="the armory", title="Pick the lock", text="Something shiny behind a vent. Somebody left it. Somebody's watching it.",
                   cost=("grudge",), see="A vent with a latch. Somebody wants it found.", take="Fine. One less thing to hate.",
                   announcer="HE'S PICKING A LOCK ON THE HEAT STAIRS! THE KING IS NOT AMUSED!", leave="Nah. Hell would love that.")),
    dict(key="heldBreath", file="Secret3", number=91, title="The Held Breath", host=909, kind="upgrade", build=held_breath,
         door=dict(prompt="the steam room", title="Go in holding your breath", text="A steam vent as wide as a door. Hell won't say what's in it.",
                   cost=("diamonds", 15), see="That vent's too wide to be a vent.", take="Deep breath. Fifteen diamonds' worth.",
                   announcer="INTO THE STEAM?! HE'S HOLDING HIS BREATH FOR A LIVING!", leave="No. I like breathing.")),
    dict(key="applauseRoom", file="Secret4", number=92, title="The Applause Room", host=907, kind="funny", build=applause_room,
         door=dict(prompt="a noise behind the wall", title="Follow the clapping", text="Someone is clapping behind the wall. Slowly. At him.",
                   cost=("risk", "Risk: Hell is watching."), see="Is that... clapping? Behind the wall?", take="Fine. I want to see who's clapping.",
                   announcer="AN AUDIENCE?! HE HAS FOUND THE AUDIENCE!", leave="It's a trap. Applause is always a trap.")),
    dict(key="restStop", file="Secret5", number=93, title="The Rest Stop", host=905, kind="refill", build=rest_stop,
         door=dict(prompt="a bench", title="Sit on the bench", text="A bench in a crack in the wall. A bench. In Hell. It costs, of course.",
                   cost=("diamonds", 10), see="A bench in the wall. A bench. In Hell.", take="Ten diamonds to sit. Hell has prices.",
                   announcer="HE'S TAKING A SEAT! THE CROWD DEMANDS HE STAND!", leave="Sitting is for people with time.")),
    dict(key="guestBook", file="Secret6", number=94, title="The Guest Book", host=903, kind="lore", build=guest_book,
         door=dict(prompt="a book on a stand", title="Sign the book", text="A book on a stand in a gap in the wall. It's open. It's waiting.",
                   cost=("grudge",), see="A book on a stand. In a gap. Waiting.", take="Fine. One less thing to hate.",
                   announcer="HE'S SIGNING THE KING'S GUEST BOOK! THE KING'S GLOW BRIGHTENS!", leave="Not signing anything.")),
    dict(key="cageMatch", file="Gauntlet1", number=95, title="The Cage Match", host=908, kind="gauntlet",
         build=with_reward(gauntlet(CAGE_WAVES, "FLOOR {floor} AND A HALF: THE CAGE MATCH", "A cage. Round. Sand on the floor. Of course.",
                                    ["The crowd loves it.", "Again! ...Again!", "More! ...More!"]),
                           "cage", [{"type": "addDiamonds", "amount": 12}, {"type": "setFlag", "flag": "gauntlet.cage"}, pop("PURSE: +12 DIAMONDS")],
                           "Twelve diamonds. For that. Fine."),
         door=dict(prompt="a cage", title="Climb into the cage", text="A cage in the wall, sand on its floor. Four waves. Hell won't say more.",
                   cost=("risk", "Risk: four waves, no way out."), see="A cage in the wall. With sand in it.", take="Fine. How bad can four waves be.",
                   announcer="A CAGE MATCH! HE'S CLIMBING IN! THE CROWD IS ON ITS FEET!", leave="I'm not here to fight for sport.")),
    dict(key="encore", file="Gauntlet2", number=96, title="The Encore", host=904, kind="gauntlet",
         build=with_reward(gauntlet(ENCORE_WAVES, "FLOOR {floor} AND A HALF: THE ENCORE", "A stage. Footlights. A curtain with a hole in it.",
                                    ["Encore!", "Again! Again!", "Same again! ...Same again!", "Once more with feeling!"]),
                           "encore", [{"type": "setFlag", "flag": "gauntlet.encore"}, pop("A GRUDGE FOR THE ENCORE", "#FFB84D")],
                           "That's it? ...It's never it."),
         door=dict(prompt="a curtain", title="Step through the curtain", text="A curtain, painted on the wall. Footlights. Five waves, the same again.",
                   cost=("diamonds", 10), see="A curtain on the wall. Painted. Moving.", take="Ten diamonds to be shot at. Sure.",
                   announcer="AN ENCORE?! HE HASN'T EVEN DONE THE FIRST SHOW!", leave="One show is plenty.")),
]

# The door's zone: the last foothold before each host's landing (its ledge + 1.15 up), measured from the host's own path.
HOST_DOORS = {906: [22.4, 3.7], 910: [2.8, 11.2], 909: [5.4, 6.7], 907: [16.2, 6.6], 905: [23.7, 9.8], 903: [10.8, 11.8],
              908: [18.2, 4.8], 904: [8.4, 6.2]}
DOOR_SIZE = [2.8, 2.6]


# ───────────────────────── writing ─────────────────────────

def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)

def dump(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

def host_info(host):
    d = load(os.path.join(LEVELS, f"Floor{host}.json"))
    return d, d["nextLevelNumber"], d["script"]["floors"]["end"]

def side_level(room):
    _, host_next, floor = host_info(room["host"])
    base = load(os.path.join(LEVELS, "Floor901.json"))
    script = {
        "length": 90 if room["kind"] != "gauntlet" else 120,
        "floors": {"start": floor, "end": floor, "unitsPerFloor": 0.0, "circle": "emberWastes", "signs": False},
        "objective": {"complete": {"type": "counter", "name": "cleared", "atLeast": 1}, "text": room["title"]},
        "counters": [{"name": "lavaSpeed", "value": 0.0}, {"name": "lavaMax", "value": 0.0}, {"name": "secretFound", "value": 0.0}],
        "phases": room["build"](floor),
    }
    level = {k: v for k, v in base.items() if k != "script"}
    level["levelName"] = room["file"]
    level["displayName"] = f"Floor {floor} and a half: {room['title']}"
    level["levelNumber"] = room["number"]
    level["musicVolume"] = 0.7
    level["deathConfigName"] = "Death1"
    level["duration"] = 240.0 if room["kind"] != "gauntlet" else 300.0
    level["nextLevelNumber"] = host_next
    level["script"] = script
    return level

def door_rules(room):
    host_floor = room["host"]
    _, _, end = host_info(host_floor)
    d, key = room["door"], room["key"]
    cost = d["cost"]
    offer = {"type": "offerShortcut", "id": key, "prompt": f"{DOOR_PROMPT}: {d['prompt']}", "title": d["title"], "description": d["text"],
             "stayTitle": "Leave it", "stayDescription": "Keep climbing. Pick a Grudge at the top.", "toFloor": end, "nextLevel": room["number"],
             "color": "#FFB84D"}
    if cost[0] == "grudge":
        offer.update(costGrudges=1, costText="Costs a Grudge: {0}", cantPayText="NO GRUDGE TO GIVE")
    elif cost[0] == "diamonds":
        offer.update(costGrudges=0, costDiamonds=cost[1], costText="Costs {0} diamonds", cantPayText="NOT ENOUGH DIAMONDS")
    else:
        offer.update(costGrudges=0, costText=cost[1], cantPayText="")
    zone = HOST_DOORS[host_floor]
    return [
        {"name": f"{RULE_PREFIX}: {room['title']}", "when": {"type": "playerInZone", "center": zone, "size": DOOR_SIZE, "anchor": "World"},
         "maxTimes": 1, "do": [seq(wait(0.3), hero(d["see"], 2.6), wait(2.8), offer)]},
        {"name": f"{RULE_PREFIX}: {room['title']}, taken", "when": {"type": "shortcutChosen", "shortcut": key, "taken": True}, "maxTimes": 1, "do": [seq(
            {"type": "lockInput"}, {"type": "lockRewind"}, {"type": "playerInvincible", "seconds": 30.0},
            hero(d["take"], 2.6), wait(2.4), banner(d["announcer"], 3.0, "#FFB84D"), wait(2.8),
            {"type": "fade", "to": "Black", "duration": 0.8}, wait(1.0), {"type": "cameraShake", "intensity": 0.18, "duration": 0.8},
            {"type": "setFloor", "floor": end, "skipMilestones": True}, {"type": "setCounter", "name": "lavaSpeed", "value": 0.0}, wait(0.8),
            {"type": "fade", "to": "Clear", "duration": 0.8}, wait(1.0), {"type": "setCounter", "name": "cleared", "value": 1})]},
        {"name": f"{RULE_PREFIX}: {room['title']}, left", "when": {"type": "shortcutChosen", "shortcut": key, "taken": False}, "maxTimes": 1,
         "do": [hero(d["leave"], 2.4)]},
    ]

def patch_host(host_floor, rules):
    path = os.path.join(LEVELS, f"Floor{host_floor}.json")
    data = load(path)
    kept = [r for r in data["script"]["rules"] if not r["name"].startswith(RULE_PREFIX)]
    data["script"]["rules"] = kept + rules
    dump(path, data)

def write_content():
    dump(os.path.join(CONFIG, "content", "upgrades", "heldBreath.json"), {
        "schemaVersion": 1, "title": "Held Breath",
        "description": "You held it the whole way. +2 max health and 2s more rewind. Found, never offered.",
        "maxLevel": 1, "weight": 0.0, "maxHealthPerLevel": 2, "rewindSecondsPerLevel": 2.0, "color": "#7FE8FF"})
    dump(os.path.join(CONFIG, "content", "weapons", "slagLadle.json"), {
        "schemaVersion": 1, "displayName": "Slag Ladle",
        "description": "Somebody was stirring something with this. A slow glob of slag for two hits' worth of damage, 10 scoops that come back slowly.",
        "color": "#FF8A3D", "kind": "Projectile", "cooldown": 0.42, "switchDelay": 0.2,
        "aimGuide": {"show": True, "length": 0.8},
        "ammo": {"capacity": 10, "start": 10, "perShot": 1, "regenPerSecond": 0.9},
        "projectile": {"speed": 7.5, "lifetime": 1.9, "faceAim": True, "spriteAngle": 225.0, "damageScale": 2.2, "multiShot": True},
        "sounds": {"empty": "wpn_empty_shotgun", "use": "shotgun_blast", "hit": "", "equip": "wpn_equip_shotgun"},
        "pickup": "slagLadlePickup", "banner": "SECRET WEAPON: SLAG LADLE",
        "heroText": "A ladle. Somebody's cooking. I don't ask what."})
    pick = load(os.path.join(ENTITIES, "spitfirePickup.json"))
    pick["visual"].update(placeholderColor="#FF8A3D", fxColor="#FF8A3D")
    pick["abilities"][0]["do"] = [{"type": "unlockWeapon", "weapon": "slagLadle", "equip": True}, {"type": "setCounter", "name": "secretFound", "value": 1}]
    dump(os.path.join(ENTITIES, "slagLadlePickup.json"), pick)

def add_to_game_json():
    path = os.path.join(CONFIG, "game.json")
    text = open(path, encoding="utf-8").read()
    names = [r["file"] for r in ROOMS]
    data = json.loads(text)
    missing = [n for n in names if n not in data["levels"]]
    if not missing:
        return
    # A text edit keeps the file's one-line list (the diff stays one line, and other agents' edits merge).
    marker = '"Floor792"]'
    assert marker in text, "game.json's level list no longer ends with Floor792: add the side floors at its end by hand"
    text = text.replace(marker, '"Floor792", ' + ", ".join(f'"{n}"' for n in missing) + "]", 1)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)

def check():
    problems = []
    entities = {f[:-5] for f in os.listdir(ENTITIES) if f.endswith(".json")}
    game = load(os.path.join(CONFIG, "game.json"))["levels"]
    numbers = set()
    keys = set()
    rewards = {}
    for room in ROOMS:
        name = room["file"]
        if name not in game:
            problems.append(f"{name}: not in game.json")
        elif game.index(name) + 1 != room["number"]:
            problems.append(f"{name}: game.json position {game.index(name) + 1} != number {room['number']}")
        if room["number"] in numbers or room["key"] in keys:
            problems.append(f"{name}: duplicate number or key")
        numbers.add(room["number"]); keys.add(room["key"])
        rewards.setdefault(room["kind"], []).append(name)
        path = os.path.join(LEVELS, name + ".json")
        if not os.path.exists(path):
            problems.append(f"{name}: not written"); continue
        level = load(path)
        phases = {p["name"] for p in level["script"]["phases"]}
        if "enter" not in phases:
            problems.append(f"{name}: no enter phase")

        def walk(o):
            if isinstance(o, dict):
                if o.get("type") in ("spawn", "spawnWave") and o["entity"] not in entities:
                    problems.append(f"{name}: unknown entity {o['entity']}")
                if o.get("type") == "goToPhase" and o["phase"] not in phases:
                    problems.append(f"{name}: goToPhase {o['phase']} has no phase")
                if o.get("type") == "spawnWave" and "points" in o and len(o["points"]) != o["count"]:
                    problems.append(f"{name}: wave of {o['entity']} has {len(o['points'])} points for {o['count']}")
                for v in o.values(): walk(v)
            elif isinstance(o, list):
                for v in o: walk(v)
        walk(level["script"])
        host = load(os.path.join(LEVELS, f"Floor{room['host']}.json"))
        if level["nextLevelNumber"] != host["nextLevelNumber"]:
            problems.append(f"{name}: leads to {level['nextLevelNumber']} but host Floor{room['host']} leads to {host['nextLevelNumber']}")
        if level["script"]["floors"]["start"] != host["script"]["floors"]["end"]:
            problems.append(f"{name}: not on the floor its host ends on")
        if not any(r["name"].startswith(RULE_PREFIX) for r in host["script"]["rules"]):
            problems.append(f"{name}: host Floor{room['host']} has no door")
        door = next((r for r in host["script"]["rules"] if r["name"] == f"{RULE_PREFIX}: {room['title']}"), None)
        if door and f'"nextLevel": {room["number"]},' not in json.dumps(door, indent=0).replace("\n", " "):
            problems.append(f"{name}: host Floor{room['host']}'s door leads to another level")
    hosts = [r["host"] for r in ROOMS]
    if len(set(hosts)) != len(hosts):
        problems.append("two doors on one host floor")
    kinds = [k for k, v in rewards.items() if k != "lore" and len(v) > 1 and k != "gauntlet"]
    if kinds:
        problems.append(f"two rooms pay the same kind of reward: {kinds}")
    return problems


def main():
    if "--check" not in sys.argv:
        write_content()
        add_to_game_json()
        for room in ROOMS:
            dump(os.path.join(LEVELS, room["file"] + ".json"), side_level(room))
        for room in ROOMS:
            patch_host(room["host"], door_rules(room))
    problems = check()
    for p in problems:
        print("PROBLEM:", p)
    print(f"{len(ROOMS)} side floors, {len(problems)} problem(s)")
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
