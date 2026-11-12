# Magenta key-pose + clip judge (ff00ff)

Refs: concept `a_babf8d7bca383dae`, pose3 colours `a_0960962c0b655f36`.

## Poses

| Pose | Asset | Verdict | Note |
|---|---|---|---|
| IdleSigh | a_8618b4b2b6e01956 | PASS | Bored sigh, closed helm, launcher, magenta plate |
| Walk | a_bf18806edfb4186b | PASS | Planted stride, cloth trails |
| Jump | a_c690fa811c5eb99d | PASS | Tuck, cloth streams |
| Fall | a_9522880c1f4c8fe4 (redo j_39a4c3b0d5c2) | PASS | Closed helm, cloth up, legs trailing — not a dash |
| Land | a_9e728f3f1fd49c24 | PASS | Squash crouch, hand planted |
| Dash | a_9ffc4d582fa52f6d | PASS | Forward lean, cloth horizontal |
| Hurt | a_24ecc1208f5f4001 (redo j_c6bc1f8affbb) | PASS | Closed helm + gauntlet up (snap-back mild; was open-face FAIL) |
| DeathBurn | a_52d8c1e3e6c36e72 | PASS | Kneeling, burn FX |
| Rewind | a_8bbc37df7ee3739c | PASS* | Weak rewind read but closed helm + colours OK |
| Victory | a_d7e1767c4d7a101f | PASS | Shrug, closed helm |

## Clips (video.i2v → sheets)

| Clip | Job / asset | Verdict | Bake |
|---|---|---|---|
| Idle | j_9c94c9dfa221 / a_a892ae7a5724affe | PASS | Idle.png 49f @12 |
| Walk | j_572ddffeb47f / a_f1dc40489993ac30 | PASS foot plant | Walk.png 25f; player.json fps 32 (stride vs moveSpeed 3.8) |
| Jump | j_ef4cc3c96816 / a_2ee9545e850a5480 | PASS | Jump.png 17f |
| Land | j_b653144432ba / a_33ac26fdf284e778 | PASS | Land.png 17f; fps 42 (≤0.4 s) |
| Dash | j_5253d79bba29 / a_65d9aa4bd11c9791 | PASS | Dash.png 17f |
| DeathBurn | j_81f62dd13012 / a_a475397afeffb0dc | PASS | DeathBurn.png 25f; fps 25 (~1 s) |
| Rewind | j_56889e5f963c / a_d587c0e585446e2e | PASS | Rewind.png 17f (sheet only; no player.json state yet) |
| Victory | j_d02be17c8af8 / a_b38325fb433b2c75 | PASS | Victory.png 17f |

## Fall / Hurt motion

Budget already ≥ $0.75 after the 8 clips — **no Fall/Hurt i2v queued**. Interim sheets are 6-frame holds baked from the PASS poses (`clip_Fall_hold.mp4` / `clip_Hurt_hold.mp4`, local ffmpeg, $0).

**Closed 2026-11-12:** J answered "keep the stand-ins" — Fall/Hurt holds stay; no further GPU for this task. No `.hawkes/pending-gpu.txt`.
