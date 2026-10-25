# Long bullet-hell floors: Floor 998 as the reference

**What was missing:** the playtest judged every floor on 60-120 s, 3 enemies and a 3 s breather, and the bot only dodged one shot at a time, so a 5-minute floor with dense volleys could not pass or be cleared.

**Changed**
- `Assets/GameConfig/levels/Floor998.json` (data only, no new C#/mechanics): The Spill is now ~280 s (`length` 280, `duration` 400). The opening river and procession (sink check, baker's cart and hay lines, first cinder alone) are unchanged. Five set pieces follow, one per path section, each a phase entered by `sectionReached` -> `goToPhase` and announced by `emit setpiece:<name>`: The Hay Wakes, Ember Rain, Sparks From the Sign, The Jam (rising lava + rain) and The Spire (finale). The camera keeps the time (`autoSpeed` sections), the landing needs the `finale` counter (`atTime` 250), so the length does not depend on the bot. Density ramp: `rampPerSecond` and shorter intervals per piece, a `heat` counter (`every` 20 s) whose `counter` rules apply `modifier` MoveSpeed x1.1 steps to cinders, and an extra aimed `spread` hazard in the last pieces.
- `Assets/Scripts/Playtest/SickOfItPlaytest.cs`: a floor whose script `length` >= 200 is "long": 240-360 s, up to 16 enemies at once, no 3 s breather, all-levels limit 420 s and a 6-min pace row. New checks `levelN.long.setPieces` (4+ `setpiece:` signals) and `levelN.long.density` (last piece >= 1.3x the first, at most one sharp drop), with a `setpiece` and a `density` event per piece for the timeline. Normal floors are untouched.
- `Assets/Scripts/Gameplay/Presentation/PlaytestBot.cs`: on long floors, off the path's footholds, a lane planner (stay / step left / step right over the next second, stays on its foothold) when 3+ shots and chasers are close. Short floors keep the old play.
- `Docs/LevelDesign.md`: "A long bullet-hell floor" (the recipe, 8 steps), the pace rule and Floor 998's page; `Docs/Story.md` rules note.

**Checked:** I could not run Unity here (no editor on this server), so none of this is compiled, imported or played. I checked the JSON against the reference doc's field names by hand. The bot's dodge, the exact lengths and densities are unverified: **beta's `level-4` run is the first real test**; if the bot dies, thin the piece it died in (the recipe says so) before touching a check.

**For J**
- Set-piece banners ("THE HAY WAKES!" etc.) are silent; announcer voice lines are a few cents via Hawkes Gen (follow-up). The title, "That was the baker's cart." and "The hay's following me now." keep their voices.
- Touch overlap: another agent (c_175c41ecde28) adds new mechanics; I did not touch their files. No merge conflicts.
- GPU: none used, nothing to price (no new art; cinder/spark/fireball art already exist).
