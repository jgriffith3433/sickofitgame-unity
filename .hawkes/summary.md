# Five JSON mechanics + a test bench

**What J asked:** bouncing shots, teleport, front shield, rotating telegraphed laser, predictive aim, as JSON, one test enemy each, documented, rewind-safe.

**Story first:** `Docs/Story.md` got a "Hell's tricks" section (why each exists in Hell's terms), roster rows for the five test enemies, and an engine-work entry. Tone: announcer banners, no office jokes, no slimes.

**Built (`Assets/Scripts/Gameplay/Content/Mechanics.cs` unless noted):**
- Bouncing shots: `projectile` gets `bounces`, `bounceKeep`, `bounceSound` (`Abilities.cs`). Test: `ricochetImp` throws `ricochetEmber`.
- `teleport` (NearPlayer / Point / Random; mark + flash telegraph, then blink). Test: `blinkImp` + `blinkMark`.
- `frontShield`: blocks damage from the side the entity faces; behind counts. `Entity.Damage` asks it. The hero's shots reach it through the new `IDirectionalDamageable` (`DamageDealer` passes the shot's position). Test: `bulwarkImp` (patrols, so its back turns).
- `laserSweep`: thin warning line, then a beam that sweeps. A real segment hitbox against the hero's collider, stopped by the first ground. Test: `beamLens`.
- `Aim.Predictive` (`Emission.LeadDirection`, solves the intercept). Test: `leadImp`.
- All runtime fields are in SaveState/LoadState. Drawing is read back from saved state each frame, so a rewind redraws it.
- Tests: `Assets/Tests/Editor/MechanicsTests.cs` (lead maths, shield side, beam/box, each enemy loads and uses its mechanic).
- Playtest scenario `mechanics` (`.hawkes/scenario.txt`): scene `content/scenes/mechanicsTest.json` on Level 1's stage, five stations. It logs `bounce`, `teleport` (telegraph then blink, 0.5+ s apart, moved 1.5+), `shieldBlock` then damage from behind, `predictive` (checked against the intercept maths), `laser` (warning, sweep, hero hit). Each beat is a `check` event.
- Docs: cookbook recipe, `GameplayReference.md` (hand-edited in the generator's format; regenerates on next import), AGENTS.md.

**NOT verified:** there is no Unity or dotnet on this server, so nothing was compiled or run. I read the code carefully, but the first Unity compile, the EditMode tests (incl. SaveStateCoversEveryRuntimeField), `rewind-perf` and the `mechanics` playtest still need to run. The rewind hot loop is untouched. The only added work is a `FindAbility` in `Entity.Damage`.

**Things to look at:**
- Shield only blocks (spends the shot); "reflect back" is not built.
- Bounces work on the level's own ground and walls, not on entity platforms or the screen-edge barriers (those aren't reported as solids).
- The `mechanics` bot doesn't dodge the beam on purpose, so it takes hits (the hitbox check). It heals 5 at each station. If the bot dies, the run ends early.
- Test enemies are tinted imps and the lens is the Fireball sheet (no new art). Sounds reuse existing audio.json ids.
- Conflicts: none.

**GPU estimate (not run, needs J's OK):** real art for the 4 imps (stand pose, 3 extra poses, 4 clips each) plus a lens: about 0.49 GPU h on 1 server, ~30 min, **$0.90** (≈$1.52 with the idle tail), via `Tools/Art/art.mjs`.
