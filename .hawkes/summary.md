# Rook / Wick real art — blocked on Gen org credits

## What was wrong
c_8d29cd9df354 merged gameplay (Rook Shaft tally, Wick Floor 999 lamp walk, playtest `climber-locals`) with stand-in portrait blobs and placeholder lamp props. This pass was to replace them with Gen art within $0.88 GPU.

## What blocked Gen
- `image.generate` refused: **org credits need 5, 1 left** (same wall as the prior pass). GPU budget ($0.88) and fleet are separate; estimate for the full batch is **~$0.38 GPU / ~13 min**.
- Fleet status also reports AWS keys missing in hawkesgen/.env; brief said Studio boots a server — jobs still cannot start without org credits.
- No Rook/Wick concept assets in the Gen library to reuse.

## What changed (prep only)
- `Assets/GameConfig/art/rook.json` — poses/clips/animator for **scratch** and **talk** (Shaft portrait loops).
- `Assets/GameConfig/art/wick.json` — poses/clips/animator for **lightLamp** and **talk**.
- `.hawkes/pending-gpu.txt` — remaining Gen pieces + estimate.
- `.hawkes/scenario.txt` — `climber-locals` on phone.

## Conflicts
None (no merge this pass). Did not touch the other agent's rewind files.

## Checks
- Art specs JSON valid.
- Did not run Unity compile (no editor here).
- Did not start a browser / playtest (Studio owns that).
- Fun-reviewer (self-pass): **thin** — gameplay locals already on beta; without sheets/voices the Shaft Rook is still an amber blob and lamps are tinted placeholders. Gaps: Gen sheets, land pending voice clips, phone ≥64 px on real art.

## What's left
1. Top up Gen org credits (≥5, ideally ≥40 for 4× generate + edits + i2v).
2. Resume: queue concepts → poses → frame → i2v → sprite.sheet; `studio_gen_import` into `Assets/Art/Enemies/Rook|Wick` and `Assets/Art/Props/WickLamp/`; wire `ui/rook.json` sheets + entity animators; then `companions.py` LightLamp play.
3. Delete `.hawkes/pending-gpu.txt` when nothing remains.
4. Beta playtest `climber-locals` (phone 390x844).
