# Beta build after "Opening and level beats"

**What was wrong:** the beta build failure was a billing limit ("This studio is out of credits: playtest.run needs 9 and 1 is left"), not a compile error. Nothing in the C# or JSON failed.

**What I checked (no code change needed):** the merged change is complete and consistent.
- `Docs/Story.md` "Why he is sick of it" holds the frame, the opening (Video1 after New Game on a fresh save, skippable with Escape or a tap) and the five-row beat table.
- Level 1 and Floors 1000, 999, 998, 997 each carry one distinct warm-grey (`#C9B8A6`) unvoiced Subtitle in their JSON; the five texts match the doc and the playtest's `BeatLines` word for word.
- `SickOfItPlaytest.Beats.cs` (`opening-beats`) is registered in `SickOfItPlaytest.cs` (scenario list and dispatch). Every helper it calls (`ToMainMenu`, `ClickNewGame`, `ClockTime`, `Tap`, `WaitReal`, `LoadLevel`, `End`) exists, and no member is defined twice.
- Unity compile not run here (no editor on this machine); the full compile and the playtest run on the build server.

**Added:** `.hawkes/scenario.txt` naming `opening-beats`, which the earlier attempt never wrote. It plays Video1 and skips it with Escape, then shows each of the five beats (`beat` events, `check beats ...`), ending with `beats none repeated`.

**Conflicts:** none. **GPU:** none used; nothing to price.

**For J:** the build only goes green again once credits are topped up (Settings > Plan and credits).
