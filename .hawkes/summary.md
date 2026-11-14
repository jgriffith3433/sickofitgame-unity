# Summary

The Meadow and Floors 1000-997 now have their own tiles and 9 story props each, wired into the levels. **Nothing has been run in Unity or a browser** (none here): no screenshots, the C# is uncompiled, and the playtest has never been played. The PR needs the build server and beta's `story-props` run before the criteria count as met.

## What changed
- **Tilesets.** Floors 1000-997 each have a ledge entity of their own (`ledgeFloor1000`..`997`) with three drawn variants (`Assets/Art/Tiles/<set>/LedgeA|B|C`). The variant is picked by where the ledge stands: new `EntityDef.initialStateByPlace` and `Entity.PlaceHash`. The body is `ledge`'s, so colliders are unchanged. The Meadow's ground gets three sod strips and two ends (`sodStrip`, `sodEndL`, `sodEndR`), also picked by place.
- **Story props.** There are 9 per level (45 in all), in `Assets/Art/Props/<set>`, each an entity `prop<Name>` drawn behind the Player band (Background2, order 90). They stand exactly on their ledge top or the ground (0 px gap in the data; limit 2 px). 7 of 9 react in the Meadow, 4 on 1000, 3 each on 999, 998 and 997. A prop reacts to a shot or hostile ember passing, and/or to the hero close by. It plays its React clip with an existing sound, then stays changed (a sign that fell stays down) or rests for 3 s.
- **Writers, no hand edits.** `Tools/Art/story_art.py` writes the `.sheet.json` beside each PNG. `Tools/Levels/story_props.py` writes the entities and edits the five levels. Both have `--check` and both pass. `story_props.py` runs last, after the other floor writers; AGENTS.md says so.
- **Playtest `story-props`** (`.hawkes/scenario.txt`): `SickOfItPlaytest.StoryProps.cs`, registered in `SickOfItPlaytest.cs`. Per level it checks from the data: 8+ props, 2+ reacting, each on its surface within 2 px, 3+ tile variants. Live, it checks that props are drawn behind the hero and every hostile shot (the phone criterion, run it at 390x844 too), that they rest on the collider below, and that two react with their sound. If the bot's play doesn't set two off, a named setup shortcut spawns an ember beside two props.
- Docs: world-bible "Story props and per-level tilesets" (now built), Story.md "Hell's furniture", the AGENTS.md bullet.

## Not done
- **Meadow positions unverified.** I assumed the ground top is y -2.5 and a visible width of about +-5. Check the sod and props in the first screenshot.
- **Tiles are one ledge piece, not a full set.** There is no separate underside or corner art: ledges stretch with `scale`, so corners stretch too. The Meadow has strip variants and two end pieces.
- **Reaction sounds are existing clips** (`amb_*`, `thatch_burst`, `sack_spill`, `door_knock`, `sf_crumble`, `whip_crack`). Each prop's own sound is still to make.
- **Phone input.** The phone check sees props only through the bot, which plays through its virtual pad. I didn't drive aim and fire by touch.
- The Meadow's second phase may redraw its ground; I didn't check it.
- `Docs/GameplayReference.md` regenerates on the next import and will list `initialStateByPlace`.

## GPU
No Gen calls this session ($0). The art in the repo came from the earlier pass in this task (Gen project `org_eb0d5c82e8da8c2d.sickofit`), and I couldn't attribute that spend per job.

## Conflicts
- None with the other changes. I touched `SickOfItPlaytest.cs` (3 small edits), `AGENTS.md` (one bullet), `Docs/Story.md` and `docs/world-bible.md` (a section each), and `Tools/Levels/ambient_finds.py` (its `STATIC` list now includes the four new ledge ids).
- `companions.py --check` and `overlord_cameos.py --check` report out of date, as they do on the base commit (I checked).
- The PNGs are outside this repo's sparse checkout. I staged them with `git add --sparse` and they are LFS pointers in the commit.
