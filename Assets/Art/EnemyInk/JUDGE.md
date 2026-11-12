# EnemyInk concept + pose picks (Meadow + Floors 1000-997)

Judged against `docs/art-bible.md` (ink outline, two-tone cel, readable silhouette at 48 px, Cuphead wind-up glow).

## Concepts

| Entity | Winner asset | Why | Rejected |
|---|---|---|---|
| imp | `a_724b0a1aa9286113` → `imp_concept.png` | Clear wings/tail/ember silhouette; diamond sack on shoulder matches the raider | `a_f57efa2cd6571149` (pauldrons not in canon) |
| cinder | `a_b50aca679df11253` → `cinder_concept.png` | Clean fireball face silhouette on green | `a_dc74ee277f0e8671` (separate black body) |
| cinderThatch | `a_5761952c402e8f7d` → `cinderThatch_concept.png` | Burning straw ball + face reads at a glance | — |
| cinderBrute | `a_fa10a6a31fb63c15` → `cinderBrute_concept.png` | Hulking fireball + stubby arms + scowl | — |
| cinderSpark | `a_daf292daa5a8d55f` → `cinderSpark_concept.png` | Cleanup removed arcade cabinet; tiny flame + dots | interim crop replaced |
| impSack | `a_e5fe43ea46e8280d` → `impSack_concept.png` | Huge diamond sack is the signature shape | — |

## Poses (this resume)

| File | Asset | Verdict | Notes |
|---|---|---|---|
| `poses/imp_windup.png` | `a_098a9001c4e93309` | PASS | Wings/horns/tail held; cyan glow telegraph reads |
| `poses/imp_throw.png` | `a_a9233ca42a8a5d52` | PASS | Ember leaving the hand; silhouette held |
| `poses/imp_struck.png` | `a_1fcff1580aeca072` | PASS | Cracks + shriek + sack; clear hurt |
| `poses/imp_dead.png` | `a_4e736170e8f4533c` | PASS (weak) | Mid-pop more than ash pile; usable die end |
| `poses/cinder_windup.png` | `a_793f5de515d3f06e` | PASS | Thick cyan rim = Cuphead hold |
| `poses/cinder_lunge.png` | `a_788f6690b63e9131` | PASS | Flame streams back; directional charge |
| `poses/cinder_struck.png` | `a_51346eb5e0c2eb75` | PASS | X-eyes / impact sparks |
| `poses/cinder_popped.png` | `a_9b22c2434f802e6b` | PASS (weak) | Impact-heavy; still a death read |
| `poses/cinderThatch_windup.png` | `a_f5d9ad823922f967` | PASS | Straw ball + flame + face |
| `poses/cinderThatch_lunge.png` | `a_cf41d59e092d7d69` | FAIL | Became a bipedal strawman; lost ball silhouette — redo queued |
| `poses/cinderThatch_struck.png` | `a_f3cc0406fdb87edb` | PASS | Shock face on thatch ball |
| `poses/cinderThatch_dead.png` | `a_e43546ac7ae19592` | PASS | Burst shards + flame |
| `poses/cinderBrute_windup.png` | `a_185a7b72ffb7f4ae` | PASS | Hulking + fist glow |
| `poses/cinderBrute_struck.png` | `a_05d35b96f7332abd` | PASS | Roar + sparks; silhouette held |
| `poses/cinderBrute_dead.png` | `a_ab7420ab16c44500` | PASS | Back-burst smoke / sparks |
| `poses/impSack_windup.png` | `a_42e3c0689854060a` | PASS | Oversized sack + yellow loot glow |
| `poses/impSack_struck.png` | `a_d76efb345c16211a` | PASS | Diamonds spilling; shock face |
| `poses/impSack_dead.png` | `a_d15f9dd7a7feefe2` | PASS | Sack spilled + smoke; readable death |

Spark poses (windup / struck / dead) queued from cleaned concept after this commit.
Clips (`video.i2v`) for imp + cinder queued after `image.frame` (640 green) of their key poses.

## Resume 2026-11-12 (clips baked)

### Spark poses
| File | Asset | Verdict | Notes |
|---|---|---|---|
| `poses/cinderSpark_windup.png` | `a_40b1de0982220f96` | PASS | Tiny flame coil + cyan core glow |
| `poses/cinderSpark_struck.png` | `a_c9a8d0dba6e9283e` | PASS | Shock face + sparks |
| `poses/cinderSpark_dead.png` | `a_55e9993a64a5f4e4` | PASS | Fizzle + smoke |

### Thatch lunge redo
| File | Asset | Verdict | Notes |
|---|---|---|---|
| `poses/_rejected/cinderThatch_lunge_redo.png` | `a_2e65f902b4b0b18d` | FAIL | Still grew arms/legs; concept is limbless straw ball. Another redo queued `j_47f98911631c`. |

### Imp + cinder i2v → sheets
All 10 clips PASS (usable; wind-up glow present on mid frames). Baked with `art.mjs --build --no-unity` into `Assets/Art/Enemies/Imp|Cinder` including new `*_windup.png`. Animator graphs restored with `IsWinding` → Windup. Previews: `preview_imp.png`, `preview_cinder.png`.

## Resume 2026-11-12b (spark bake + variant held wind-ups)

### Thatch lunge redo `a_e2c151d1172b7b08`
| File | Verdict | Notes |
|---|---|---|
| `poses/cinderThatch_lunge.png` | PASS | Limbless straw ball; no biped arms/legs. Framed locally to `framed/cinderThatch_lunge.png`. |

### Framed stands / wind-ups
| File | Verdict | Notes |
|---|---|---|
| `framed/cinderThatch_stand.png` | PASS | Limbless ball |
| Gen `framed/cinderThatch_windup` (first) | FAIL | Grew thin legs — re-framed from PASS pose instead |
| `framed/cinderBrute_stand/windup` | PASS | Hulking fireball + stubby limbs |
| `framed/impSack_stand/windup` | PASS | Oversized diamond sack + glow |

### Spark i2v clips
| Clip | Asset | Verdict | Notes |
|---|---|---|---|
| idle | `a_1142c09bdf3624a9` | PASS | Tiny flame, face, green plate |
| windup | `a_5ad376e1d0bc9966` | FAIL | Mid frames had ~0 cyan; held still from framed windup pose used instead (Cuphead telegraph) |
| attack | `a_730dd29b0adb05ef` | PASS | Cyan into lunge |
| hurt | `a_2bb1d36bee4d310f` | PASS | Shock stretch |
| die | `a_f1698194fa166b89` | PASS | Pop / fizzle |

Baked `Assets/Art/Enemies/CinderSpark/*` Idle/Move/Windup/Attack/Hurt/Die. Preview: `preview_cinderSpark.png`.

### Variant held sheets (budget)
Thatch / brute / sack got Idle/Move/Windup/Attack from framed stands + wind-up poses (no GPU). Hurt/Die still prior sheets until i2v budget. Entities point Windup/Attack at the new sheets; `windup` ≥ 0.35.

## Resume 2026-11-12c (framed struck/dead + i2v queue, c_01cbd869e007)

### Free `image.frame` (PASS poses → 640 green)
| File | Asset | Notes |
|---|---|---|
| `framed/cinderThatch_windup.png` | `a_773bbd2466a05ef6` | From PASS pose `a_f5d9ad823922f967` (not the leggy Gen frame) |
| `framed/cinderThatch_lunge.png` | `a_268b14b1ea7b805e` | From limbless PASS `a_e2c151d1172b7b08` |
| `framed/cinderThatch_struck.png` | `a_354cacd856ccb667` | |
| `framed/cinderThatch_dead.png` | `a_9eb8506fb086b8e8` | |
| `framed/cinderBrute_struck.png` | `a_39c306ccbccc621a` | |
| `framed/cinderBrute_dead.png` | `a_2bc7550a97314f63` | |
| `framed/impSack_struck.png` | `a_c18649d1e0207897` | |
| `framed/impSack_dead.png` | `a_55a250844c98b1df` | |

### Queued `video.i2v` (wait_for_server; estimate **$0.70** of $0.86)
16 clips: thatch×5, brute×5, sack×5, spark windup cyan redo×1. Ids in `.hawkes/waiting-jobs.txt` / `framed/manifest.json`.
**Left for next budget:** `launcherThief` full ink (~$0.50). Fleet had no AWS keys when queued; Studio resumes when jobs finish.

## Resume 2026-11-12d (bake i2v batch — c_01cbd869e007)

Spent **~$0.55** of $0.86 on the 16 clips (Wan). Judged mid frames vs art bible + silhouette.

### cinderThatch
| Clip | Asset | Verdict | Notes |
|---|---|---|---|
| idle | `a_5d9e1efd5747b4d9` | PASS | Limbless straw ball |
| windup | `a_0785c49a5e2b2fb7` | FAIL | Grew bird legs; prior “PASS” pose `a_f5d9ad823922f967` also has legs. Held sheet remade from limbless idle frame (puffed + glow) |
| attack | `a_ee20ba9c5256acd6` | FAIL | Bird legs — held from framed limbless lunge `a_268b14b1ea7b805e` |
| hurt | `a_d5ecb74fc3451ff6` | PASS | Limbless |
| die | `a_6d8832dec05e0856` | PASS | Limbless burst |

### cinderBrute
All five PASS (hulking fireball + stubby fists): idle `a_6481258e34123a32`, windup `a_282a4df1fe22cbee`, attack `a_1f7728db2c5b4f41`, hurt `a_b13b269a7c3c4b06`, die `a_99cd3be4df736462`.

### impSack
All five PASS (oversized diamond sack): idle `a_111bc53f7c8af4ac`, windup `a_c95b48f2365249cd`, attack `a_c335a5b82ff86273`, hurt `a_2461b2943edd6eb9`, die `a_a175ddfbe3ecee67`.

### cinderSpark windup redo
| Clip | Asset | Verdict | Notes |
|---|---|---|---|
| windup | `a_4c5fed0fbca0470d` | PASS | Mid frames show cyan core glow (~1% cyanish pixels); replaced held sheet |

Baked with stub DoD + `SOI_ART_WORK` → `Assets/Art/Enemies/{CinderThatch,CinderBrute,ImpSack,CinderSpark}` + animator apply. Previews under `EnemyInk/previews/`.
**Still pending:** `launcherThief` (~$0.50); optional thatch windup/attack motion redo.
