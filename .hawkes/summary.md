# Living main-menu diorama — Gen plates + click harden

## What was wrong
PR #386 (c_c5316e3aee93) shipped the living ledge with Gen button plates as LFS, then Studio re-queued this pass to replace stand-ins and to keep Play/Continue answering (backlog f_9ee4fb228e7d). Sparse worktrees only had LFS pointers for `MenuButtonPlate*.png`; the dedicated `menu_theme` is still owned by k_aea2f5751795.

## What changed
- **Gen plates (real bytes, trimmed):** re-imported cutout + hover/press from library (`a_382b2cf…` → trim `a_7c8ebf9b…`; hover `a_1cfe5b64…`; pressed `a_0b58bedc…`) into `Assets/Resources/UI/MenuButtonPlate{,Hover,Pressed}.png` (256×96, transparent, distinct hover/press centres) with Sprite `.meta` (9-slice borders, readable). Free `image.transform` only — no new GPU generate.
- **Clicks / EventSystem:** `EventSystemGuard.Ensure` now **adopts** an existing Input System UI EventSystem (keeps its actions) instead of always spawning a blank module; re-enables the keeper’s modules; `RebuildMenu` calls Ensure again; `MenuMotionDriver` keeps `blocksRaycasts` on while open (only off during exit).
- **Playtest `living-menu`:** Ensure before the click check; gates Gen plates (`livingMenu.platesFromGen`). Named in `.hawkes/scenario.txt`.
- **Still pending:** `menu_theme` — listed in `.hawkes/pending-gpu.txt` (ambience_floor1000 bed until then). Fleet has no GPU (AWS keys missing); music estimate ~$0.18.

## Fun-reviewer (self-pass)
**Verdict: living** for the menu ledge (not a demo). Scores: Full 4 (embers, Overlord, hero, seam, bed), Breathing 5, Interactions 4 (walk / watch / plated press / settings), Story-as-experience 4 (silhouette + hum, no text beat), Soul 4 (matches Story/bible ledge). Gaps left: (1) dedicated menu theme, (2) Overlord still procedural quads, (3) sigh uses uiHover/lava_bubble stand-ins — out of this budget / other tasks.

## Reviewer (self-pass)
**Merge** on criteria 1–4 once beta `living-menu` (phone) runs: Gen plates on disk, Overlord ≤20 s already in #386, logo slam + click harden here. Theme music still the Landing bed (called out in pending-gpu).

## Checks
- Plate PNGs: RGBA, transparent corners, hover brighter / press darker (pixel sample).
- C# read for duplicate members / missing usings on changed files — clean.
- **Unity compile not run here** (no editor).
- Conflicts: none. Stayed out of c_9c4e54748bbe EnemyInk.

## GPU spend
$0 this pass (transform/import only). Prior plates already in Gen library from c_c5316e3aee93 (~$0.016).
