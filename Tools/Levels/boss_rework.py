#!/usr/bin/env python3
"""
The six bosses' rework (Docs/Story.md "The bosses' second look"): the Imp Foreman (991), the Pile Keeper (951), the Road Breaker (891), the Carver (851),
the Magma King (901) and the Spire Fiend (801).

Rewrites each boss entity's `states` (more phases, denser telegraphed patterns, minion waves tied to phases), raises its health, adds the hero's weakness
hint line to its level, and sets the level's duration. Idempotent: it rebuilds the states from the tables below, so edit the tables and run it again.

    python3 Tools/Levels/boss_rework.py          # write
    python3 Tools/Levels/boss_rework.py --check  # fail if the files differ from what the tables make

Pattern rules (the same telegraph the playtest checks): every attack is a glow first, 1+ s before it lands. A glow is a neutral entity (`mkWarnHand`, the
warn entity of the boss's own line / spot) and the shot a `shoot` with `initialDelay` 1.2 behind it.
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
    with open(full, encoding="utf-8") as f:
        if f.read() == text:
            return
    changed.append(path)
    if not check_only:
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


# ─── building blocks ───────────────────────────────────────────────────────────────────────────────

GLOW = "mkWarnHand"  # neutral, gold, 1.8 s, plays slag_drip: the glow in front of an aimed volley


def shoot(projectile, muzzle, delay=0.2, aim="Down", speed=0.0, anim=None, **extra):
    a = {"type": "shoot", "projectile": projectile, "speed": speed, "aim": aim, "muzzle": muzzle, "interval": 99, "initialDelay": delay}
    if anim:
        a["animTrigger"] = anim
    a.update(extra)
    return a


def line(warn, muzzle, count, spacing=1.0, anim="IsAttacking"):
    return shoot(warn, muzzle, 0.2, "Down", 0.0, anim, pattern={"type": "line", "count": count, "spacing": spacing})


def fan(muzzle, count, angle):
    """A glow at the muzzle, then 1.2 s later a fan of bouncing embers aimed at the hero (the `bounces` mechanic)."""
    return [shoot(GLOW, muzzle, 0.0),
            shoot("ricochetEmber", muzzle, 1.2, "Player", 4.0, pattern={"type": "spread", "count": count, "angle": angle})]


def lead(muzzle, burst):
    """A glow, then a burst of bolts thrown where the hero is going (`Aim.Predictive`)."""
    return [shoot(GLOW, muzzle, 0.0),
            shoot("deadeyeBolt", muzzle, 1.2, "Predictive", 6.0, burst=burst, burstInterval=0.28)]


def spot(entity):
    return {"type": "spawn", "entity": entity, "anchor": "Player", "at": [0.0, -0.3], "facePlayer": False}


def spawners(mix, slots, interval=3.2, first=0.5):
    """One spawner per minion type; they share the boss's cap (`maxAlive` is counted over everything the boss spawned).
    A state change restarts a spawner's clock, so each starts 0.55 s after the one before and tries again every `interval`."""
    out = []
    for i, (entity, cap) in enumerate(mix):
        out.append({"type": "spawner", "entity": entity, "interval": interval, "initialDelay": round(first + i * 0.55, 2),
                    "maxAlive": cap, "offset": list(slots[i % len(slots)])})
    return out


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


def cycle(prefix, steps, guards, extra=None, on_enter_of=None):
    """States prefix+name chained in a loop. steps: [(name, abilities, seconds, onEnter?)]; extra abilities go in every state."""
    out = []
    for i, step in enumerate(steps):
        name, abilities, seconds = step[0], step[1], step[2]
        on_enter = step[3] if len(step) > 3 else None
        nxt = prefix + steps[(i + 1) % len(steps)][0]
        out.append(state(prefix + name, copy.deepcopy((extra or []) + abilities), [go(nxt, seconds)], on_enter, copy.deepcopy(guards)))
    return out


def kills(types):
    return [{"type": "kill", "target": {"entity": t}} for t in types]


def emit(signal):
    return {"type": "emit", "signal": signal}


# ─── the four floor bosses (tier 1, the climb, tier 2) ─────────────────────────────────────────────────

T1 = [(2.6, 2.4), (3.8, 2.6), (5.2, 1.2), (1.4, 3.0), (4.4, 0.2), (6.2, 2.4), (2.8, 0.4), (5.6, 3.2)]
T2 = [(2.0, -1.2), (3.8, -2.4), (5.4, -1.0), (1.2, -3.2), (4.6, -3.6), (6.4, -2.0), (2.8, -0.2), (5.0, -4.4)]

FLOOR_BOSSES = {
    "impForeman": dict(
        line='The Foreman. The whip glows before it cracks. So I climb past it.', pre="fm", level="Floor991", health=105, name="The Foreman", warn="fmWarnWhip", spotWarn="fmSackDrip",
        hint="The whip glows before it cracks. Climb past it.", chain=True,
        surge=[("impHauler", 4), ("haloImp", 4), ("deadeyeImp", 4), ("slagHound", 4)],
        p2=[("haloImp", 6), ("ricochetImp", 6), ("deadeyeImp", 6), ("slagHound", 6), ("whirlImp", 6)],
        p3=[("haloImp", 8), ("ricochetImp", 8), ("leadImp", 8), ("slagHound", 8), ("blinkImp", 8), ("whirlImp", 8)]),
    "pileKeeper": dict(
        line='The Keeper. Guards first, then him. He keeps a pile. I keep my distance.', pre="pk", level="Floor951", health=100, name="The Pile Keeper", warn="pkWarnSpear", spotWarn="pkWarnSack",
        hint="Guards first, then the Keeper. He keeps a pile.",
        surge=[("impGuard", 4), ("bulwarkImp", 4), ("lensImp", 4), ("deadeyeImp", 4)],
        p2=[("impGuard", 6), ("bulwarkImp", 6), ("lensImp", 6), ("slabSniper", 6), ("leadImp", 6)],
        p3=[("impGuard", 8), ("bulwarkImp", 8), ("slabSniper", 8), ("leadImp", 8), ("ricochetImp", 8), ("blinkImp", 8)]),
    "roadBreaker": dict(
        line='The Road Breaker. The road glows before it bursts. So I move first.', pre="rb", level="Floor891", health=100, name="The Road Breaker", warn="rbWarnLine", spotWarn="rbWarnSpot",
        hint="The road glows before it bursts. Move first.",
        surge=[("demonWhelp", 4), ("fuseWhelp", 4), ("bounceLobber", 4), ("demonWhelp", 4)],
        p2=[("fuseWhelp", 6), ("bounceLobber", 6), ("demonWhelp", 6), ("plateDemon", 6), ("slabSniper", 6)],
        p3=[("fuseWhelp", 8), ("bounceLobber", 8), ("risenDemon", 8), ("veilDemon", 8), ("plateDemon", 8), ("slabSniper", 8)]),
    "carver": dict(
        line='The Carver. The shards fall after the glow. So I stay out of it.', pre="cv", level="Floor851", health=100, name="The Carver", warn="cvWarnLine", spotWarn="cvWarnSpot",
        hint="Shards fall after the glow. Stay out of it.",
        surge=[("demonWhelp", 4), ("bounceLobber", 4), ("fuseWhelp", 4), ("demonWhelp", 4)],
        p2=[("risenDemon", 6), ("bounceLobber", 6), ("fuseWhelp", 6), ("slabSniper", 6), ("veilDemon", 6)],
        p3=[("risenDemon", 8), ("veilDemon", 8), ("slabSniper", 8), ("plateDemon", 8), ("fuseWhelp", 8), ("demonWhelp", 8)]),
}


def build_floor_boss(entity_id, c):
    d = load(f"content/entities/{entity_id}.json")
    pre, warn, sw = c["pre"], c["warn"], c["spotWarn"]
    foreman = entity_id == "impForeman"
    d["health"] = c["health"]
    d["hint"] = c["hint"]
    d["hintVoice"] = f"vo_hero_hint_{entity_id}"
    found = [a for st in d["states"] for a in st.get("abilities", [])]
    hover = None
    chain = None
    if foreman:
        chain = copy.deepcopy(next(a for a in found if a["type"] == "swingChain"))
    else:
        hover = copy.deepcopy(next(a for a in found if a["type"] == "hover" and a["offset"][1] > 5))
    climb_signal = {"fm": "fmClimb", "pk": "pkSink", "rb": "rbBreak", "cv": "cvBreak"}[pre]
    tier2_signal = pre + "Tier2"
    all_types = []
    for mix in (c["surge"], c["p2"], c["p3"]):
        for t, _ in mix:
            if t not in all_types:
                all_types.append(t)
    surge_types = list(dict.fromkeys(t for t, _ in c["surge"]))

    ground = [-0.0, 0.0]
    m1 = [3.4, -0.9]   # tier 1 line origin
    m2 = [3.0, -2.2]   # tier 2 line origin
    mouth1, mouth2 = [0.2, 0.3], [0.2, 0.3]
    lash2 = [0.0, -2.75] if foreman else m2

    def tier2(extra):
        return ([copy.deepcopy(chain)] if foreman else [copy.deepcopy(hover)]) + extra

    # Foreman's tier-1 spot is the sack-drip rain, the others stand a glow where the hero is.
    def spot_state_ability():
        if foreman:
            return [shoot(sw, [0.0, -0.6], 0.2, "Down", 0.0, None, pattern={"type": "rain", "count": 3, "angleJitter": 0.0})]
        return []

    def spot_enter():
        return [] if foreman else [spot(sw)]

    states = [state("intro", None, [{"to": "p1Line", "when": {"type": "signal", "name": pre + "Fight"}}])]

    # Phase 1: the boss alone. Below 70% the surge; below 50% he climbs.
    p1_guards = [below("climb", 0.5), below("surgeEnter", 0.7)]
    steps1 = [("Line", [line(warn, m1, 5)], 3.0),
              ("Spot", spot_state_ability(), 1.6, spot_enter()),
              ("Spot2", spot_state_ability(), 2.2, spot_enter()),
              ("Line2", [line(warn, m1, 6, 0.9)], 3.0)]
    p1 = cycle("p1", steps1, p1_guards)
    states += p1

    # Phase 1b, the surge: the first wave, four at most.
    states.append(state("surgeEnter", None, [go("surgeFan", 0.4)], [emit(pre + "Surge"), {"type": "playSound", "sound": "whip_crack" if foreman else "lava_burst"}],
                        [below("climb", 0.5)]))
    surge_slots = T1
    sp = spawners(c["surge"], surge_slots)
    sg = [below("climb", 0.5)]
    steps_s = [("Fan", fan(mouth1, 3, 40) + sp, 3.6),
               ("Line", [line(warn, m1, 7, 0.85)] + sp, 3.0),
               ("Spot", spot_state_ability() + sp, 1.6, spot_enter()),
               ("Lead", lead(mouth1, 3) + sp, 3.6),
               ("Spot2", spot_state_ability() + sp, 2.0, spot_enter())]
    states += cycle("surge", steps_s, sg)

    # The climb: untouchable, the wave gone with the tier.
    climb_enter = [emit(climb_signal)]
    if foreman:
        climb_enter += [{"type": "applyStatus", "target": {"self": True}, "effect": "sceneUntouchable"}]
    climb_enter += kills(all_types)
    states.append(state("climb", [hover if hover else {"type": "hover", "speed": 3, "offset": [8.2, 8.4], "aroundSpawn": True, "keepSide": False, "bobHeight": 0}],
                        [{"to": "p2Enter", "when": {"type": "signal", "name": tier2_signal}}], climb_enter))

    # Phase 2, tier 2: five-wide waves, six at most. Below a quarter: phase 3.
    p2_enter_actions = [emit(pre + "Phase2")]
    if foreman:
        p2_enter_actions = [emit("fmPhase2"), {"type": "removeStatus", "target": {"self": True}, "effect": "sceneUntouchable"},
                            {"type": "showText", "text": "My sack! Nobody touches my sack! Up the chains!", "style": "Subtitle", "duration": 3.4,
                             "color": "#FFB040", "sound": "vo_fm_phase2"}]
    sp2 = spawners(c["p2"], T2)
    g2 = [below("p3Enter", 0.25)]
    states.append(state("p2Enter", tier2([]), [go("p2Line", 0.8)], p2_enter_actions, copy.deepcopy(g2)))
    steps2 = [("Line", [line(warn, lash2 if foreman else m2, 5)] + sp2, 3.0),
              ("Fan", fan(mouth2, 3, 44) + sp2, 3.6),
              ("Spot", (spot_state_ability() if foreman else []) + sp2, 2.2, spot_enter()),
              ("Lead", lead(mouth2, 4) + sp2, 3.6),
              ("Line2", [line(warn, lash2 if foreman else m2, 6, 0.9)] + sp2, 3.0),
              ("Spot2", (spot_state_ability() if foreman else []) + sp2, 2.0, spot_enter())]
    states += [s for s in cycle("p2", steps2, g2, extra=None) if s["name"] != "p2Enter"]
    # tier 2 states hover (or swing)
    for s in states:
        if s["name"].startswith("p2") and s["name"] != "p2Enter":
            s["abilities"] = tier2(s.get("abilities", []))

    # Phase 3: the rage. Eight at most, wider fans, bolts in fives, the glows two at a time.
    p3_enter = [emit(pre + "Phase3"), {"type": "playSound", "sound": "magma_rumble"}]
    sp3 = spawners(c["p3"], T2, interval=3.0, first=0.4)
    states.append(state("p3Enter", tier2([]), [go("p3Line", 0.8)], p3_enter))
    steps3 = [("Line", [line(warn, lash2 if foreman else m2, 7, 0.85)] + sp3, 2.6),
              ("Fan", fan(mouth2, 5, 64) + sp3, 3.2),
              ("Spot", (spot_state_ability() if foreman else []) + sp3, 1.6, spot_enter()),
              ("Lead", lead(mouth2, 5) + sp3, 3.2),
              ("Spot2", (spot_state_ability() if foreman else []) + sp3, 1.6, spot_enter()),
              ("Line2", [line(warn, lash2 if foreman else m2, 6, 0.9)] + fan(mouth2, 3, 30) + sp3, 3.2)]
    for s in cycle("p3", steps3, []):
        s["abilities"] = tier2(s.get("abilities", []))
        states.append(s)
    # The tier-2 attack rows use the Foreman's lash origin; the Foreman's sack drip needs a tier-2 origin of its own.
    if foreman:
        for s in states:
            for a in s.get("abilities", []):
                if a.get("type") == "shoot" and a.get("projectile") == sw:
                    a["muzzle"] = [0.0, -0.6]
                    a["pattern"] = {"type": "rain", "count": 3 if s["name"][:2] in ("p1", "p2") else 5, "angleJitter": 0.0}
    # Foreman: keep the original rewind-safe react at a quarter (fmFlap) as it is.
    d["states"] = states
    save(f"content/entities/{entity_id}.json", d)

    # The level: the hero's hint after the intro, the phases' camera shakes, a longer duration.
    lv = load(f"levels/{c['level']}.json")
    lv["duration"] = 520.0
    script = lv["script"]
    intro = next(p for p in script["phases"] if p["name"] == "intro")
    steps = intro["onEnter"][0]["steps"]
    hint_line = {"type": "showText", "text": c["line"], "duration": 3.8, "style": "Subtitle", "sound": f"vo_hero_hint_{entity_id}"}
    # after the fight signal: the hero sizes him up once the fight has begun
    idx = max(i for i, s in enumerate(steps) if s.get("type") == "emit" and s.get("signal") == pre + "Fight")
    steps[idx + 1:] = [{"type": "wait", "seconds": 1.2}, hint_line]
    rules = script["rules"]
    rules[:] = [r for r in rules if not r["name"].startswith("boss rework:")]
    for sig, label, intensity in ((pre + "Surge", "surge", 0.18), (pre + "Phase3", "rage", 0.3)):
        rules.append({"name": f"boss rework: {label}", "when": {"type": "signal", "name": sig}, "maxTimes": 1,
                      "do": [{"type": "cameraShake", "intensity": intensity, "duration": 0.7}]})
    save(f"levels/{c['level']}.json", lv)


# ─── the two Wardens (arenas) ──────────────────────────────────────────────────────────────────────────

WARDENS = {
    "magmaKing": dict(
        line="The Magma King. Dodge the breath. And don't wake him early.", pre="mk", level="Floor901", health=170, name="The Magma King",
        hint="Dodge the breath. Don't wake him early.", hint_after=7.0,
        wave=[("deadeyeImp", 3), ("haloImp", 3), ("ricochetImp", 3)],
        p2=[("impGuard", 6), ("bulwarkImp", 6), ("lensImp", 6), ("leadImp", 6), ("ricochetImp", 6)],
        p3=[("haloImp", 5), ("whirlImp", 5), ("slagHound", 5), ("deadeyeImp", 5)],
        last=[("haloImp", 8), ("whirlImp", 8), ("blinkImp", 8), ("lensImp", 8), ("slagHound", 8), ("leadImp", 8)],
        slots=[(1.9, 1.1), (4.2, -0.2), (3.0, 2.4), (5.6, 1.0), (2.4, -0.6), (6.4, 2.2), (4.8, 2.8), (1.4, 2.6)]),
    "spireFiend": dict(
        line='The Spire Fiend. He counts my steps. So I stay calm.', pre="sf", level="Floor801", health=170, name="The Spire Fiend",
        hint="Stay calm. He counts your steps.", hint_after=9.0,
        wave=[("bounceLobber", 3), ("fuseWhelp", 3), ("demonWhelp", 3)],
        p2=[("sfWhelp", 6), ("fuseWhelp", 6), ("bounceLobber", 6), ("veilDemon", 6), ("slabSniper", 6)],
        p3=[("veilDemon", 5), ("plateDemon", 5), ("bounceLobber", 5), ("fuseWhelp", 5)],
        last=[("risenDemon", 8), ("veilDemon", 8), ("slabSniper", 8), ("fuseWhelp", 8), ("demonWhelp", 8), ("plateDemon", 8)],
        slots=[(1.7, -0.9), (3.6, -1.6), (5.2, -0.4), (2.4, 0.9), (4.4, 1.4), (6.2, -1.8), (3.0, -2.6), (5.8, 1.0)]),
}


# Where the original cycles close (state -> (first state of the cycle, seconds)); the rework routes these through its own states.
ORIGINAL_TAILS = {
    "mk": {"p2Breath": ("p2Slam", 3.0), "p3Slam": ("p3Flood", 3.0)},
    "sf": {"p2Spot": ("p2Row", 2.0), "p3Row2": ("p3Field", 2.6)},
}


def build_warden(entity_id, c):
    d = load(f"content/entities/{entity_id}.json")
    pre = c["pre"]
    d["health"] = c["health"]
    d["hintVoice"] = f"vo_hero_hint_{entity_id}"
    d["hint"] = c["hint"]
    slots = c["slots"]
    wave = spawners(c["wave"], slots)
    p2 = spawners(c["p2"], slots)
    p3 = spawners(c["p3"], slots, 3.0)
    last = spawners(c["last"], slots, 2.8, 0.4)
    types2 = list(dict.fromkeys(t for t, _ in c["p2"]))

    # Back to the original states: drop the ones this script made and every way into them, and the spawners it put on the others.
    new = [s for s in d["states"] if not s["name"].startswith(pre)]
    for s in new:
        s["transitions"] = [t for t in s["transitions"] if not t["to"].startswith(pre)]
        if "abilities" in s:
            s["abilities"] = [a for a in s["abilities"] if a["type"] != "spawner"]
            if not s["abilities"]:
                del s["abilities"]
    by = {s["name"]: s for s in new}
    for name, (to, seconds) in ORIGINAL_TAILS[pre].items():
        by[name]["transitions"].append(go(to, seconds))

    if pre == "mk":
        p1_list, stand = ["p1Throw", "p1Slam", "p1Throw2"], "rising"
        phase2, p3_list, mouth = ["p2Slam", "p2Breath"], ["p3Flood", "p3Breath", "p3Slam"], [0.8, 0.9]
    else:
        p1_list, stand = ["p1Row", "p1Spear", "p1Spot"], "shedding"
        phase2, p3_list, mouth = ["p2Row", "p2Spear", "p2Spot"], ["p3Field", "p3Row", "p3Spot", "p3Row2"], [0.4, 0.8]
    enrage = "enraging"

    # Phase 1: alone. Below 75% the first wave, and a fan of bouncing embers and thrown bolts join the heavy attack.
    for n in p1_list:
        by[n]["transitions"].insert(1, below(f"{pre}Wave", 0.75))
    heavy = copy.deepcopy(by[p1_list[1]].get("abilities", []))
    guard = [below(stand, 0.5)]
    wave_states = [
        state(f"{pre}Wave", None, [go(f"{pre}WaveFan", 0.5)], [emit(pre + "Wave"), {"type": "playSound", "sound": "magma_rumble"}], guard),
        state(f"{pre}WaveFan", fan(mouth, 3, 44) + copy.deepcopy(wave), [go(f"{pre}WaveLead", 3.6)], None, guard),
        state(f"{pre}WaveLead", lead(mouth, 3) + copy.deepcopy(wave), [go(f"{pre}WaveHeavy", 3.4)], None, guard),
        state(f"{pre}WaveHeavy", copy.deepcopy(heavy) + copy.deepcopy(wave), [go(f"{pre}WaveFan", 3.2)], None, guard),
    ]
    # Phase 2 (standing / shed): the guard's wave, six at most; the cycle closes through a fan and a burst of bolts.
    for n in phase2:
        by[n]["abilities"] = by[n].get("abilities", []) + copy.deepcopy(p2)
    g2 = [below(enrage, 0.334)]
    p2_extra = [
        state(f"{pre}P2Fan", fan(mouth, 3, 50) + copy.deepcopy(p2), [go(f"{pre}P2Lead", 3.6)], None, g2),
        state(f"{pre}P2Lead", lead(mouth, 4) + copy.deepcopy(p2), [go(phase2[0], 3.6)], None, g2),
    ]
    tail2 = phase2[-1]
    by[tail2]["transitions"] = [t for t in by[tail2]["transitions"] if t["when"]["type"] == "healthBelow"] + [go(f"{pre}P2Fan", ORIGINAL_TAILS[pre][tail2][1])]
    # The enrage clears phase 2's wave (every type it brought).
    en = by[enrage]
    en["onEnter"] = [a for a in en["onEnter"] if a["type"] != "kill"] + kills(types2)
    # Phase 3 (enraged): a fresh wave of other types, five at most; the cycle closes through a wide fan.
    for n in p3_list:
        by[n]["abilities"] = by[n].get("abilities", []) + copy.deepcopy(p3)
        by[n]["transitions"].insert(0, below(f"{pre}Last", 0.15))
    p3_fan = state(f"{pre}P3Fan", fan(mouth, 5, 64) + copy.deepcopy(p3), [go(p3_list[0], 3.2)], None, [below(f"{pre}Last", 0.15)])
    tail3 = p3_list[-1]
    by[tail3]["transitions"] = [t for t in by[tail3]["transitions"] if t["when"]["type"] == "healthBelow"] + [go(f"{pre}P3Fan", ORIGINAL_TAILS[pre][tail3][1])]
    # Phase 4, the last stand: below 15% the show doesn't stop: eight at most, a wide fan, five bolts, the phase-3 heavy attack.
    heavy3 = [a for a in copy.deepcopy(by[p3_list[0]].get("abilities", [])) if a["type"] != "spawner"]
    last_states = [
        state(f"{pre}Last", None, [go(f"{pre}LastFan", 0.6)], [emit(pre + "Last"), {"type": "playSound", "sound": "magma_rumble"}]),
        state(f"{pre}LastFan", fan(mouth, 5, 70) + copy.deepcopy(last), [go(f"{pre}LastLead", 3.0)]),
        state(f"{pre}LastLead", lead(mouth, 5) + copy.deepcopy(last), [go(f"{pre}LastHeavy", 3.0)]),
        state(f"{pre}LastHeavy", heavy3 + copy.deepcopy(last), [go(f"{pre}LastFan", 3.2)]),
    ]

    d["states"] = new + wave_states + p2_extra + [p3_fan] + last_states
    save(f"content/entities/{entity_id}.json", d)

    lv = load(f"levels/{c['level']}.json")
    lv["duration"] = 600.0
    fight = next(p for p in lv["script"]["phases"] if p["name"] == "fight")
    fight["rules"] = [r for r in fight.get("rules", []) if r["name"] != "the hero sizes him up" and not r["name"].startswith("boss rework:")]
    fight["rules"].append({"name": "the hero sizes him up", "when": {"type": "after", "seconds": c["hint_after"]}, "maxTimes": 1,
                           "do": [{"type": "showText", "text": c["line"], "duration": 3.8, "style": "Subtitle",
                                   "sound": f"vo_hero_hint_{entity_id}"}]})
    for sig, label, intensity in ((pre + "Wave", "wave", 0.15), (pre + "Last", "last stand", 0.3)):
        fight["rules"].append({"name": f"boss rework: {label}", "when": {"type": "signal", "name": sig}, "maxTimes": 1,
                               "do": [{"type": "cameraShake", "intensity": intensity, "duration": 0.7}]})
    save(f"levels/{c['level']}.json", lv)


for eid, cfg in FLOOR_BOSSES.items():
    build_floor_boss(eid, cfg)
for eid, cfg in WARDENS.items():
    build_warden(eid, cfg)

if check_only and changed:
    print("out of date:", ", ".join(changed))
    sys.exit(1)
print(("would change: " if check_only else "wrote: ") + (", ".join(changed) if changed else "nothing"))
