# Living main-menu diorama — clicks + ledge life

## What was wrong
Studio re-queued after #389 because `.hawkes/pending-gpu.txt` listed `menu_theme` (owned by **k_aea2f5751795**, not this task). Gen button plates, painted ledge, Overlord pass, hero mutter/sigh and `living-menu` already landed on beta. PM also linked **f_9ee4fb228e7d** (smoke stuck on Play/Continue; duplicate EventSystem warning). Fun-reviewer still scored the ledge **thin**.

## What changed
- **No pending-gpu** for `menu_theme` (other task). Bed stays `ambience_floor1000` until that lands. Plates already Gen on beta (`MenuInkPlates.FromGen`).
- **Clicks / EventSystem (f_9ee4fb228e7d):** `EventSystemGuard` disables `StandaloneInputModule`, watches aggressively for 2 s after every scene load, exposes `EnabledCount`. `MainMenuManager.EnsureClicksReady` re-Ensures after two frames and forces menu buttons interactable. `living-menu` gates clicks-ready + one EventSystem held 2 s, then a real Play click.
- **Ledge life (fun-reviewer gaps):** damned soul sit-in (asymmetric, looks up / faces hero), occasional lava bursts aimed near the hero, Overlord sheds an ember mid-climb that lands with a splash + seam flash.
- Story + `docs/world-bible.md` ledge updated. Scenario: `living-menu`.

## Fun-reviewer
After life pass: still **thin** on painted Overlord / `menu_theme` / rewind-on-menu toy (out of this task / owned elsewhere). Full↑ Breathing↑ Interactions↑ vs prior; remaining gaps called out in try/learned for later polish.

## Reviewer
**Merge** on criteria 1–4 + click harden; do not block on `menu_theme`.

## Checks
- C# read for duplicate members / missing usings on changed files — clean.
- **Unity compile not run here** (no editor).
- Conflicts: none. Stayed out of enemy-roster redraw (c_01cbd869e007 / k_f8a8bfcb37fe).
- GPU spend: $0 (reuse library plates + procedural life).

## Acceptance map
1. Hero + Overlord ≤20 s — existing + ember drop / soul look-up
2. Embers + logo slam ≤0.5 s — existing; lava bursts add life
3. Illustrated plates hover/press — Gen plates already on beta
4. [phone] hero above buttons, ≥48 px, ≤20 s — existing `living-menu` gates
