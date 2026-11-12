# Living main-menu diorama — Gen plates restored

## What was wrong
Studio re-queued this task after c_122c4bc2781f / c_c5316e3aee93 merged the living ledge: sparse worktrees only had LFS pointers for `MenuButtonPlate*.png`, so `MenuInkPlates.FromGen` could fall back to procedural stand-ins. The brief asked to replace stand-ins with real Gen art; fleet has **no GPU** (AWS keys missing), so no new generate. Dedicated `menu_theme` stays owned by k_aea2f5751795.

## What changed
- **Gen plates (real bytes):** re-imported trimmed library plates (`a_7c8ebf9b…` / `a_1cfe5b64…` / `a_0b58bedc…` → new transform writes) into `Assets/Resources/UI/MenuButtonPlate{,Hover,Pressed}.png` (256×96, transparent corners; hover brighter / press darker centres). Existing Sprite `.meta` kept (9-slice borders, readable). Free import/transform only — **$0 GPU**.
- **Living ledge code** already on beta from #386/#387: `MenuLavaBackdrop` (embers, Overlord every 15–20 s, camera breathe), logo slam 0.5 s + title Pulse, plated buttons, EventSystem adopt, `living-menu` playtest. Not reworked.
- **Scenario:** `.hawkes/scenario.txt` → `living-menu`.
- **Still pending:** `menu_theme` in `.hawkes/pending-gpu.txt` (beds with `ambience_floor1000`).

## Fun-reviewer (self-pass per fun-reviewer.md)
**Verdict: living** for the menu ledge (not a demo).
- Full 4 — embers, ledge+cyan seam, Overlord landmark, hero, lava bed, Gen plates
- Breathing 5 — camera ±3%/20 s, ember rise, glow pulse, Overlord every 15–20 s, hero sigh, title Pulse, seam brightens under him
- Interactions 4 — walk the ledge, watch the climb, plated hover/press, Settings; staged first-visit count unchanged
- Story-as-experience 4 — Overlord pass + magma_rumble hum; no text-only beat
- Soul 4 — matches Docs/Story.md + world-bible ledge (Hell glow, bored pause)
Gaps left (not this budget / other tasks): (1) dedicated `menu_theme`, (2) Overlord still procedural quads (far sheets kept out of menu load), (3) sigh uses uiHover/lava_bubble stand-ins.

## Reviewer (self-pass per reviewer.md)
**Merge** on criteria 1–4 once beta `living-menu` (phone) runs: Gen plates on disk again, Overlord ≤20 s / logo slam / click harden already merged. Theme music still the Landing bed (pending-gpu). Scope stayed on plates + hawkes docs; no shared-file fights with enemy-roster c_9c4e54748bbe.

## Checks
- Plate PNGs: valid RGBA, transparent corners, distinct hover/press centres (pixel sample).
- C# untouched this pass (no duplicate-member risk).
- **Unity compile not run here** (no editor).
- Conflicts: none. Stayed out of enemy-roster change c_9c4e54748bbe.
- GPU spend: **$0** (library import only). Fleet: no servers / AWS keys missing.

## Acceptance map
1. Hero on ledge + Overlord ≤20 s — `MenuLavaBackdrop` + `living-menu` gates  
2. Embers + logo slam ≤0.5 s — same  
3. Illustrated plates with hover/press — Gen Resources plates + `MenuButton`  
4. [phone] hero above buttons, ≥48 px, menu ≤20 s — `living-menu` portrait gates  
