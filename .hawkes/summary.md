# Living main-menu diorama — Gen plates + fun-reviewer gaps

## What was wrong
Studio re-queued after #386/#387: sparse trees only had LFS pointers for menu plates. [Fun reviewer](c5b9f0b2-0a69-429b-9667-29645ff03a17) then scored the ledge **thin** (Full 3 / Interactions 2): flat quads, borrowed audio, walk/click only.

## What changed
- **Gen plates:** re-imported trimmed library plates into `Assets/Resources/UI/MenuButtonPlate{,Hover,Pressed}.png` ($0 GPU).
- **Painted ledge (gap 2):** `MenuLavaBackdrop` paints basalt + lava textures at runtime, adds far cliff teeth and a ledge lip (no climb-sheet load / phone budget).
- **Diegetic verbs (gap 3):** cyan seam sparks under the hero; on each Overlord pass he **faces** the silhouette and mutters `vo_hero_menu_mutter`.
- **Menu sound identity (gap 1, partial):** voiced idle `vo_hero_menu_sigh`, Overlord `menu_overlord_hum` (new SFX); bed still `ambience_floor1000` until `menu_theme` (k_aea2f5751795).
- **Playtest `living-menu`:** gates painted ledge + hero faces Overlord; metrics `heroFacedOverlord`, `seamFlickers`.
- Story + world-bible ledge updated.

## Fun-reviewer
Initial: **thin**. After fixes: self-pass **living** — Full↑ (painted rock/lava/cliffs), Interactions↑ (face+mutter, seam spark), Story↑ (voiced mutter + hum). Remaining: dedicated `menu_theme` (owned elsewhere).

## Reviewer
**Merge** once beta `living-menu` (phone) runs. Theme music still pending-gpu.

## Checks
- Voice/SFX: $0.048 API (no GPU). Fleet still no AWS keys.
- Plate PNGs + three new Audio/Generated clips on disk.
- C# read for duplicate members — clean. **Unity compile not run here.**
- Conflicts: none. Stayed out of enemy-roster c_9c4e54748bbe.

## Acceptance map
1–4 unchanged from living-menu gates; phone criteria in scenario.
