# Merge origin/beta into Rook/Wick climber-locals

## Conflict
- **File:** `docs/world-bible.md` (only conflict; everything else auto-merged).
- **Cause:** beta’s living-floors change (#404) and this branch both edited The Shaft under *What happens*.
- **Resolution:** kept **both** intents in that section:
  - *Ours:* Rook scratches chalk when the rewind count rises; ambient chalk scratch; wall fills with fives.
  - *Beta:* living air (wind streaks, debris every 10–15 s, chain silhouettes).
  - Merged lines include Rook’s tally behaviour **and** the living-air sentence; ambient and Changes list both Rook’s chalk and beta’s prior bullets.
- No binary / Unity YAML conflicts. Did not abort.

## Auto-merge (no conflict markers)
Beta’s ShaftAmbience attach in `ShaftScreen.cs` sits beside our Rook wiring; Floor 999 keeps Wick’s three-lamp walk and gains beta’s furnace ambience pack; `SickOfItPlaytest.cs` registers both `climber-locals` and `living-air`.

## Checks
- No `<<<<<<<` / `=======` / `>>>>>>>` left in `docs/world-bible.md`
- World bible still has Areas / Landmarks / Who lives here / Characters / What happens / Story beats; Rook + Wick character blocks intact
- `python3 Tools/Levels/companions.py --check` → would change: nothing
- Unity compile not run here (no editor in this worktree)

## Fun-reviewer (post-merge pass)
Verdict: **thin**. Shaft is fuller (Rook tally + living air), Floor 999 has Wick’s lamp walk + furnace air; still missing Gen voice/SFX clips and GPU character sheets (`pending-gpu.txt` / prior credits wall). Biggest gaps: land clips so lines are heard; ScratchMark / LightLamp art; phone ≥64 px check on real sheets.

## What’s left (unchanged by merge)
1. Gen credits → make Rook/Wick clips → clear pending
2. GPU art in `pending-gpu.txt` within $0.88 once free
3. Beta playtest `climber-locals` (phone 390x844 for size/captions)
