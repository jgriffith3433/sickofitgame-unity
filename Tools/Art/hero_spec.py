# Writes Assets/GameConfig/art/hero.json: every grip's animation set and the shared clips (Docs/ArtPipeline.md "Grips").
# Edit this, not hero.json: python Tools/Art/hero_spec.py (from the repo root), then node Tools/Art/art.mjs hero.
import json

KNIGHT = ("the hero: an armoured knight in battered dark bronze plate armour with brass trim, a closed helm with two glowing orange eye slits, "
          "tattered maroon cloth hanging from the belt, heavy gauntlets and greaves; broad, tired, unimpressed posture; his gun from the start is a huge "
          "rocket launcher with glowing orange chambers; exactly the look of his existing Idle sprite (SickOfItGamePlayer/Idle)")
BLACK = "Plain flat pure black background (#000000), no floor, no shadow, no scenery, even lighting."
KEEP = ("Keep the knight exactly the same as image 1: the same battered bronze plate armour, helm with glowing orange eye slits, tattered maroon cloth, "
        "the same view angle, the same size and the same position in the frame, feet on the same ground line. The whole body and the whole weapon visible. "
        + BLACK)
WSTYLE = ("Dark fantasy video game weapon art: painterly, detailed, gritty, warm rim light from hellfire, readable silhouette. A single object alone, centered, "
          "the whole thing visible, plenty of empty space around it. Plain flat pure green background (#00ff00), no floor, no shadow, no hands.")
CLIPNEG = "extra weapons, two weapons, rocket launcher on his back"
CALM = CLIPNEG + ", muzzle flash, fire from the weapon, flames from the barrel, shooting, explosion"
# Wan Animate copies the launcher out of the motion video: bare-handed clips also say no weapon at all.
BARE = CALM + ", rocket launcher, gun, pistol, any weapon in his hands, projectile, missile, rocket"

# grip id -> (state prefix, weapon concept, carry prompt, what he does when he attacks, kind)
GRIPS = {
    "shotgun": ("Shotgun",
        "A short heavy shotgun forged from a slag barrel: a thick dark iron barrel with glowing orange cracks, a stubby wooden stock bound with riveted iron bands, seen from the side, pointing right.",
        "Image 2 is a shotgun. Remove the rocket launcher completely and put the shotgun from image 2 in its place: the same raised hand holds the shotgun resting on his shoulder, the barrel pointing out to the left side of the picture the way the launcher pointed, the wooden stock behind his shoulder.",
        "fires the shotgun from his shoulder toward the left, a big blast of orange flame and sparks from the barrel, the recoil rocking him back", "gun"),
    "spitfire": ("Spitfire",
        "A live fire newt: a small chubby orange salamander lizard with glowing ember-red spots and a glowing mouth, a worn leather strap buckled round its belly as a handle, seen from the side facing right, grumpy.",
        "Image 2 is a fire newt lizard. Remove the rocket launcher completely: the same raised hand now holds the orange fire newt from image 2 up beside his shoulder by the leather strap round its belly, its glowing mouth pointing to the left side of the picture.",
        "squeezes the fire newt and it spits a fast stream of glowing embers toward the left", "gun"),
    "ricochetPistol": ("Pistol",
        "A pale steel pistol built by someone who hates straight lines: a long bent barrel, angular plates, a bright glowing blue crystal eye set in the muzzle and another in the side, seen from the side pointing right.",
        "Image 2 is a pale steel pistol. Remove the rocket launcher completely: the same raised hand now holds the pistol from image 2 up beside his shoulder, muzzle pointing up and a little to the left, ready.",
        "levels the pale pistol toward the left and fires it, a flash of blue light at the muzzle, the recoil kicking his hand up", "gun"),
    "slagLadle": ("Ladle",
        "A long iron foundry ladle: a deep round iron bowl brimming with glowing molten orange slag, a long iron handle wrapped in scorched rags, seen from the side, bowl to the right.",
        "Image 2 is a long iron ladle full of glowing slag. Remove the rocket launcher completely: the same raised hand now holds the long ladle from image 2 resting on his shoulder, the bowl of glowing slag out on the left side of the picture where the launcher's muzzle was.",
        "swings the ladle forward toward the left and flings a glob of glowing molten slag out of its bowl, then hauls it back onto his shoulder", "gun"),
    "boomerangHalo": ("Halo",
        "A thick heavy golden halo ring, glowing warm gold light, dented and chipped from a long fall, a few tiny sparks, seen at a slight angle.",
        "Image 2 is a golden halo ring. Remove the rocket launcher completely: the same raised hand now holds the golden halo from image 2 up beside his shoulder like a discus ready to throw, its warm gold glow lighting his armour.",
        "throws the golden halo toward the left like a discus, a gold trail behind it, and catches it again as it comes spinning back", "throw"),
    "chainWhip": ("Whip",
        "A chain whip: a short wooden grip wrapped in black leather with an iron pommel, a long length of heavy dark iron chain links ending in a hooked iron barb, laid out in a loose S curve.",
        "Image 2 is a chain whip. Remove the rocket launcher completely: the same raised hand now holds the wooden handle of the chain whip from image 2 up beside his shoulder, the heavy iron chain hanging down over his arm and dangling to the ground on the left side of the picture.",
        None, "whip"),
    "fists": ("Unarmed", None,
        "Remove the rocket launcher completely: the same raised hand is now an empty armoured fist held up beside his shoulder in a boxer's guard, the other hand a fist at his hip.",
        None, "fists"),
}
WEAPON_WORD = {"shotgun": "shotgun", "spitfire": "fire newt", "ricochetPistol": "pistol", "slagLadle": "slag ladle", "boomerangHalo": "golden halo",
               "chainWhip": "chain whip", "fists": "bare armoured fists", "rocketLauncher": "rocket launcher"}

poses = {
    "stand": "standing with the rocket launcher on his shoulder, bored under the helm (Idle's first frame)",
    "unarmed": {"from": "stand", "action": "standing with empty hands, nothing on his shoulder or back",
                "prompt": "Image 1 is an armoured knight carrying a rocket launcher on his shoulder. Remove the rocket launcher completely: nothing on his shoulder or back, both armoured gauntlets empty, hanging relaxed at his sides with the fists loosely closed. " + KEEP},
}
clips = {}


def pose(name, base, action, prompt=None, refs=None, tool=None):
    p = {"from": base, "action": action}
    if prompt:
        p["prompt"] = prompt
    if refs:
        p["refs"] = refs
    if tool:
        p["tool"] = tool
        p.pop("from")
    poses[name] = p


NOWEAPON = "His hands hold nothing: no sword, no axe, no spear, no weapon of any kind. Nothing else in the picture: no wall, no floor, no ground, no props. "


def edit(base_desc, change, bare=False):
    return f"Image 1 is an armoured knight {base_desc}. Show the exact same knight {change}. " + (NOWEAPON if bare else "") + KEEP


# Clips Wan Animate spoiled by copying the launcher (or its muzzle fire) out of the driving video:
# POSEMOTION take the motion as pose maps (video.pose, then video.motion: no launcher pixels reach the model);
# NOMOTION are made from the pose alone (first-last-frame image-to-video), fine for standing still.
POSEMOTION = {"fistsRun", "fistsJump", "fistsFall", "climbUp", "climbDown"}
NOMOTION = {"shotgunIdle", "shotgunFall", "ricochetPistolIdle"}


def clip(name, state, frm, action, seconds=1.0, to=None, loop=False, motion=None, motion_frames=None, fps=None, wired="own", neg=CLIPNEG, play_fps=None, start=None, end=None):
    if name in NOMOTION:
        motion = None
    c = {"from": frm, "action": action, "seconds": seconds, "state": state, "loop": loop, "wired": wired}
    if to:
        c["to"] = to
    if motion:
        c["motion"] = motion
        c["fps"] = 12
        c["playFps"] = 12
        if motion_frames:
            c["motionFrames"] = motion_frames
        if name in POSEMOTION:
            c["motionVia"] = "pose"
    else:
        c["fps"] = fps or 16
        c["playFps"] = play_fps or (fps or 16)
    if neg:
        c["negative"] = neg
    if start:
        c["start"] = start
    if end:
        c["end"] = end
    clips[name] = c


# ---- the launcher (the Animator draws Idle/Walk/Jump/Fall/Fire: these are its extra clips)
pose("launcherLand", "stand", "landing from a fall in a deep crouch, knees bent hard, the launcher still on his shoulder",
     edit("carrying a rocket launcher on his shoulder", "landing from a fall in a deep crouch, knees bent hard, one hand braced, the launcher still on his shoulder"))
pose("launcherAir", "stand", "in mid-air with his knees tucked up, firing the launcher toward the left",
     edit("carrying a rocket launcher on his shoulder", "in mid-air with his knees tucked up under him, the rocket launcher levelled on his shoulder pointing to the left, a burst of flame at its muzzle"))
clip("land", "Land", "launcherLand", "straightens up out of the landing crouch, the launcher settling on his shoulder", 0.6, to="stand")
clip("airFire", "AirFire", "launcherAir", "hangs in the air with his knees tucked, firing the launcher toward the left again and again, flame and smoke bursting from its muzzle", 1.0, loop=True)
clip("runFire", "RunFire", "stand", "walks steadily in place, the rocket launcher on his shoulder firing toward the left as he goes, flame bursting from its muzzle with each shot", motion="player:Walk", loop=True)
clip("equipLauncher", "EquipLauncher", "unarmed", "reaches back, swings the rocket launcher up onto his shoulder and checks it", 1.0, to="stand")
clip("launcherAway", "LauncherTaken", "stand", "the rocket launcher is yanked off his shoulder out of the picture to the left; he looks down at his empty hands, unimpressed", 1.5, to="unarmed")

# ---- the other grips
for g, (pfx, concept, carry, attack, kind) in GRIPS.items():
    word = WEAPON_WORD[g]
    carryp = f"carry{pfx}"
    if concept:
        poses[f"weapon{pfx}"] = {"canvas": "00ff00", "action": f"the {word} alone", "prompt": "Paint " + concept[0].lower() + concept[1:] + " " + WSTYLE}
        pose(carryp, "stand", f"holding the {word} the way he held the launcher", "Image 1 is an armoured knight carrying a rocket launcher on his shoulder. " + carry + " " + KEEP, refs=[f"weapon{pfx}"])
    else:
        pose(carryp, "stand", "empty fists up in a boxer's guard", "Image 1 is an armoured knight carrying a rocket launcher on his shoulder. " + carry + " " + KEEP)
    holding = f"holding a {word}" if g != "fists" else "with his empty armoured fists up"
    pose(f"land{pfx}", carryp, f"landing in a deep crouch, {holding}",
         edit(holding, f"landing from a fall in a deep crouch, knees bent hard, still {holding} the same way"))
    pose(f"air{pfx}", carryp, f"in mid-air with his knees tucked, {holding}",
         edit(holding, f"in mid-air with his knees tucked up under him, still {holding}"))
    names = {"idle": pfx + "Idle", "run": pfx + ("Walk" if g == "fists" else "Run"), "jump": pfx + "Jump", "fall": pfx + "Fall"}
    walking = "walks steadily in place" if g != "fists" else "walks steadily in place, fists up"
    clip(f"{g}Idle", names["idle"], carryp, f"stands bored, shifting his weight, {holding}", motion="player:Idle", loop=True, neg=BARE if g == "fists" else CALM)
    clip(f"{g}Run", names["run"], carryp, f"{walking}, {holding}", motion="player:Walk", loop=True, neg=BARE if g == "fists" else CALM)
    clip(f"{g}Jump", names["jump"], carryp, f"jumps up high and lands, {holding}", motion="player:Jump", loop=False, neg=BARE if g == "fists" else CALM)
    clip(f"{g}Fall", names["fall"], carryp, f"falls through the air flailing, {holding}", motion="player:Fall", loop=True, neg=BARE if g == "fists" else CALM)
    clip(f"{g}Land", pfx + "Land", f"land{pfx}", f"straightens up out of the landing crouch, {holding}", 0.6, to=carryp)
    clip(f"{g}Equip", "Equip" + ("Fists" if g == "fists" else pfx), "unarmed",
         "cracks his gauntleted knuckles and raises his fists into a guard" if g == "fists" else f"reaches back and pulls out the {word}, checks it and takes it up the way he carries it",
         1.0, to=carryp)
    if kind == "gun":
        clip(f"{g}Attack", pfx + "Attack", carryp, attack, motion="player:Fire", loop=True)
        clip(f"{g}RunAttack", pfx + "RunAttack", carryp, f"walks steadily in place and {attack}", motion="player:Walk", loop=True)
        clip(f"{g}AirAttack", pfx + "AirAttack", f"air{pfx}", f"hangs in the air with his knees tucked and {attack}", 1.0, loop=True)
    elif kind == "throw":
        pose("throwHalo", carryp, "his arm swung out toward the left, the golden halo just leaving his hand",
             edit(holding, "with his throwing arm swung out toward the left, the golden halo just leaving his open hand and flying away to the left, a gold trail"))
        clip(f"{g}Attack", pfx + "Attack", carryp, attack, 0.75, to=carryp, loop=True)
        clip(f"{g}RunAttack", pfx + "RunAttack", carryp, f"walks steadily in place and {attack}", motion="player:Walk", loop=True)
        clip(f"{g}AirAttack", pfx + "AirAttack", f"air{pfx}", f"hangs in the air with his knees tucked and {attack}", 0.75, to=f"air{pfx}", loop=True)
    elif kind == "fists":
        for name, state, act in [("jab", "Punch1", "throwing a quick straight jab toward the left side of the picture, his lead arm fully extended"),
                                 ("cross", "Punch2", "throwing a hard cross with his other fist toward the left, shoulders turned into it"),
                                 ("hook", "Punch3", "swinging a big wide round punch with his bare gauntleted fist toward the left, his whole body twisted into the blow, armour seams flaring")]:
            pose(f"{name}Fists", carryp, act, edit(holding, act, bare=True))
            clip(f"punch{state[-1]}", state, carryp, act.replace("throwing", "throws").replace("swinging", "swings"), 0.6, to=f"{name}Fists")
        pose("runPunch", carryp, "mid-stride, lunging forward to the left with a straight punch",
             edit(holding, "mid-stride, lunging forward to the left with a straight punch, his rear leg pushing off", bare=True))
        pose("airPunch", f"air{pfx}", "in mid-air, knees tucked, swinging a punch toward the left",
             edit("in mid-air with his fists up", "in mid-air with his knees tucked, swinging a big punch toward the left", bare=True))
        clip("fistsRunAttack", "UnarmedRunAttack", carryp, "lunges forward a step and throws a straight punch toward the left", 0.6, to="runPunch")
        clip("fistsAirAttack", "UnarmedAirAttack", "airUnarmed", "swings a big punch toward the left in mid-air", 0.6, to="airPunch")
    elif kind == "whip":
        for name, state, act in [("lash", "WhipLash", "lashing the chain whip out toward the left, the heavy chain stretched out long in a straight line to the left"),
                                 ("snap", "WhipSnap", "snapping the chain whip back up over his head, the chain looping in a wide arc above him"),
                                 ("crack", "WhipCrack", "cracking the chain whip down hard toward the left with both hands, the chain whipping out in a long straight line, sparks at its barbed tip")]:
            pose(f"{name}Whip", carryp, act, edit(holding, act))
            clip(f"whip{name.title()}", state, carryp, act.replace("lashing", "lashes").replace("snapping", "snaps").replace("cracking", "cracks"), 0.7, to=f"{name}Whip")
        pose("runLash", carryp, "mid-stride, lashing the chain whip out toward the left",
             edit(holding, "mid-stride, lashing the chain whip out toward the left, the chain stretched out long in a line"))
        pose("airLash", f"air{pfx}", "in mid-air, knees tucked, lashing the chain whip out toward the left",
             edit("in mid-air holding a chain whip", "in mid-air with his knees tucked, lashing the chain whip out toward the left, the chain stretched out long"))
        clip("whipRunAttack", "WhipRunAttack", carryp, "steps forward and lashes the chain whip out toward the left", 0.7, to="runLash")
        clip("whipAirAttack", "WhipAirAttack", "airWhip", "lashes the chain whip out toward the left in mid-air", 0.7, to="airLash")

# ---- shared clips: no weapon in his hands (one look for every grip)
U = "with empty hands"
shared = [
    ("dashLean", "leaning hard forward mid-sprint toward the left, one leg stretched back, the maroon cloth streaming behind"),
    ("wallCling", "pressed flat against an invisible wall on the left side of the picture (the wall itself is not drawn), one gauntlet and one boot flat against it, his helm turned back over his shoulder"),
    ("wallKick", "kicking off an invisible wall (not drawn) with one boot, his armoured body arched out and up toward the right, arms spread"),
    ("grappleAim", "throwing his arm forward and up toward the upper left, a hook on a rope flying from his gauntlet"),
    ("grappleHang", "hanging by one gauntlet from a rope rising above him, his legs swinging, bored"),
    ("grazeSpin", "twisting smugly aside, helm tilted, as if something just missed him by an inch"),
    ("hurtRecoil", "jolted backward by a hit, helm snapped back, arms flung up, sparks bursting off his breastplate"),
    ("deathBurnt", "lying flat on his back, his armour scorched black and smoking, a puff of grey smoke rising"),
    ("deathSquashed", "squashed flat on the ground under his own armour, only his helm and gauntlets sticking out"),
    ("launchedSpin", "launched up into the air spinning, one gauntlet raised in a small wave goodbye"),
    ("shrugLying", "lying down on his side on the ground, propped on one elbow, giving up, a small shrug"),
    ("victoryPose", "standing with his arms crossed, a small tired nod of the helm, one gauntlet raised in the smallest fist pump"),
]
for name, act in shared:
    poses[name] = {"from": "unarmed", "action": act, "prompt": edit(U, act, bare=True)}
clip("dash", "Dash", "unarmed", "bursts into a low fast dash toward the left, the maroon cloth streaming behind", 0.5, to="dashLean")
clip("wallSlide", "WallSlide", "wallCling", "slides slowly down the wall, gauntlet scraping, sparks under his boot", 1.0, loop=True)
clip("wallJump", "WallJump", "wallCling", "kicks off the wall and springs up and away from it", 0.6, to="wallKick")
clip("grappleThrow", "GrappleThrow", "unarmed", "throws the hook on its rope up toward the upper left", 0.5, to="grappleAim")
clip("grappleSwing", "GrappleSwing", "grappleHang", "swings on the rope, legs swinging, bored", 1.2, loop=True)
clip("graze", "Graze", "unarmed", "twists smugly aside as something just misses him, then settles", 0.6, to="grazeSpin")
clip("hurt", "Hurt", "unarmed", "is hit, jolts backward with sparks bursting off his breastplate, then braces", 0.6, to="hurtRecoil")
clip("deathBurn", "DeathBurn", "unarmed", "lights up like a firework, a burst of flame and smoke, and falls flat on his back, smoking", 1.5, to="deathBurnt")
clip("deathSquash", "DeathSquash", "unarmed", "looks up, sighs, and is flattened by something heavy from above", 1.25, to="deathSquashed")
clip("deathSpin", "DeathSpin", "unarmed", "is launched spinning up into the air and waves goodbye at the top", 1.5, to="launchedSpin")
clip("deathShrug", "DeathShrug", "unarmed", "shrugs, lies down on his side and gives up", 1.5, to="shrugLying")
clip("victory", "Victory", "unarmed", "crosses his arms, nods once and gives the smallest fist pump: fine", 1.25, to="victoryPose")
clip("climbUp", "Climb", "unarmed", "climbs a ladder hand over hand, no ladder drawn", motion="player:Climb", motion_frames=53, loop=True, neg=BARE)
clip("climbIdle", "ClimbIdle", "unarmed", "hangs on a ladder, breathing, no ladder drawn", motion="player:ClimbIdle", motion_frames=49, loop=True, neg=BARE)
clip("climbDown", "ClimbDown", "unarmed", "climbs down a ladder hand under hand, no ladder drawn", motion="player:ClimbDown", motion_frames=33, loop=True, neg=BARE)

spec = {
    "schemaVersion": 1, "kind": "hero", "entity": "player", "name": "Hero",
    "description": KNIGHT,
    "height": 0.85, "reference": "player:Idle", "pixels": 340, "cellSize": [604, 340],
    "notes": ("Hero clips (Docs/ArtPipeline.md, 'Hero clips'). The hero is the ARMOURED KNIGHT (SickOfItGamePlayer/Idle), never a man in a jacket. "
              "Every grip (content/weapons hands) has its own set: a carry pose made from Idle's first frame with the weapon's concept (pose weapon<Grip>, "
              "image.generate) as image 2, then Idle/Run/Jump/Fall/Attack copied from his launcher videos' motion (video.animate, 'motion': 'player:<State>'), "
              "so every set has the launcher's timing, size and ground line; the rest are first-last-frame clips (video.i2v). Shared moves (dash, walls, the hook, "
              "graze, hurt, deaths, victory, ladders) show him with empty hands, one look for every grip. 1280x720 frames baked to 604x340 cells at 100 PPU "
              "(Idle's size). 'wired' says what player.json does: 'own', 'standIn' or false. Ledge grab/climb: no such mechanic in the game yet."),
    "takes": {"stand": 1, "pose": 3, "clip": 1},
    "poses": poses, "clips": clips,
}
# Curious, Shout and Grabbed (Level 1's portal scene, with the launcher) were made before as clips of their own; their sheets stay.
for name, state in [("curious", "Curious"), ("shout", "Shout"), ("grabbed", "Grabbed")]:
    spec["clips"][name] = {"video": f"player:{state}", "state": state, "wired": "own", "loop": state != "Shout", "action": "made before (Level 1's portal scene)"}

open("Assets/GameConfig/art/hero.json", "w", encoding="utf-8", newline="\n").write(json.dumps(spec, indent=2, ensure_ascii=False) + "\n")
print(len(poses), "poses", len(clips), "clips")
