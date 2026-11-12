# Enemy ink redraw — thatch/brute/sack/spark sheets baked

## Wrong
Prior merge shipped Imp/Cinder/CinderSpark plus held wind-ups for thatch/brute/sack; Hurt/Die and full motion for those three, and spark’s cyan wind-up, were still stand-ins / queued GPU.

## Judged (16 `video.i2v`, ~$0.55 of $0.86)
- **cinderBrute** all 5 PASS → motion sheets baked.
- **impSack** all 5 PASS → motion sheets baked.
- **cinderSpark windup** `a_4c5fed0fbca0470d` PASS (cyan mid-frames) → replaced held sheet; other spark clips re-baked from prior PASS assets.
- **cinderThatch** idle/hurt/die PASS (limbless). Windup + attack i2v FAIL (bird legs). Held Windup remade from limbless idle frame; Attack held from limbless lunge frame. Note: old “PASS” pose `a_f5d9ad823922f967` also has legs — do not reuse.

## Changed
- Sheets: `Assets/Art/Enemies/{CinderThatch,CinderBrute,ImpSack,CinderSpark}/*` (+ `.sheet.json`, kept `.meta` GUIDs).
- Entities: animator re-applied for those four (`Windup` + Idle/Move/Attack/Hurt/Die); contact `windup` 0.35 inherited / set.
- Docs: `docs/art-bible.md` §9, `EnemyInk/JUDGE.md`, `framed/manifest.json`, previews under `EnemyInk/previews/`.
- `.hawkes/pending-gpu.txt`: **launcherThief** (~$0.50, Floors 998/994) + optional thatch windup/attack motion redo.
- Playtest scenario stays `enemy-ink` (`{"device":"phone"}`).

## Checks
- Visual judge of mid-frames + baked thatch windup/attack (limbless) and spark windup (cyan).
- `art.mjs --build --no-unity` with stub DoD + Pillow venv (lesson m_a9b1750211f6).
- Phone height math at `levelCameraOrthoSize` 2.8: spark ~42 px; thatch/brute/sack well over 40.
- Unity compile not run here (no editor). Conflicts: none.

## Fun reviewer
**thin** — art/telegraph pass for existing Meadow foes, not a new area. Living-world score unchanged. Gaps: launcherThief ink; thatch windup/attack still held (readable, not full motion).

## Reviewer
**merge for this slice** — acceptance for thatch/brute/sack/spark Idle/Windup/Attack/Hurt/Die + ≥0.3 s wind-up met; launcherThief still open under pending-gpu (budget left ~$0.31 < ~$0.50).
