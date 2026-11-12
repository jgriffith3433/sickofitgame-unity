# Enemy ink redraw — framed variants + i2v queued (~$0.70)

## Wrong
Prior merge (#388 / c_9c4e54748bbe) shipped Imp/Cinder/CinderSpark sheets and held still wind-ups for thatch/brute/sack because GPU budget ran out. Hurt/Die for variants and full motion for those three were still stand-ins; spark wind-up lacked cyan mid-frames.

## This pass (non-GPU + queue)
- Free `image.frame` of PASS poses → `Assets/Art/EnemyInk/framed/` for thatch (stand/windup/lunge/struck/dead), brute (struck/dead + existing stand/windup), sack (struck/dead + stand/windup), spark stand/windup. Metas added.
- Queued **16 `video.i2v`** jobs (one Wan batch, `wait_for_server`): thatch×5, brute×5, sack×5, spark windup cyan redo×1. Estimate **$0.70** of **$0.86**. Ids in `.hawkes/waiting-jobs.txt` (already running on RunPod when queued).
- Updated `docs/art-bible.md` §9 status, `JUDGE.md`, `framed/manifest.json`.
- Playtest scenario stays `enemy-ink` (phone device) in `.hawkes/scenario.txt`.
- **Left:** `launcherThief` full ink (~$0.50) in `.hawkes/pending-gpu.txt` (over remaining budget after this batch).

## On resume (Studio brings job results)
Judge each clip against art bible + silhouette; bake with `SOI_ART_WORK=... node Tools/Art/art.mjs <id> --build --apply --no-unity`; replace sheets under `Assets/Art/Enemies/{CinderThatch,CinderBrute,ImpSack,CinderSpark}`; clear pending lines that land; PR screenshots of lineup beside the hero.

## Checks
- `hawkesgen_estimate` for thatch+brute+sack+spark = $0.70; framing cost $0.
- `hawkesgen_jobs`: batch queued/running (RunPod).
- Unity compile not run here (no editor on agent; JSON/docs/PNG only this pass).
- Conflicts: none.

## Fun reviewer
**thin** (1–2 lines): this pass is art pipeline + wind-up telegraph for existing Meadow foes, not a new area. Living-world score unchanged; gaps are unfinished GPU sheets (queued) and launcherThief ink.

## Reviewer
**revise until bake lands** — acceptance needs Idle/Windup/Attack/Hurt/Die motion sheets; held frames + queued i2v are the correct mid-pass. Do not open PR as done until waiting-jobs bake.
