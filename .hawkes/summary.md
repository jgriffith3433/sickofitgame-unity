# Place ambience Gen replace — still blocked on org credits

## What was wrong
Beta already ships the wiring from #398/#399: 7 Ambience-bus beds, 12 positional emitters, playtest `floor-ambience`, world-bible place beds. Clips on beta are still **synth stand-ins** (`generatedFrom: synth|Tools/Audio/synth_core_sfx.py`). Eyes-closed distinct floors need real Gen takes. This change was started to replace them (brief: GPU up, $0.28 budget, project `org_eb0d5c82e8da8c2d.sickofit`).

## What this pass tried
- Confirmed JSON wiring still OK: Level1 + Floors 1000–997 each have non-null `ambientSound` and ≥3 `ambientEmitters`; Shaft uses `ambience_shaft`; all 19 ids on the Ambience bus. `AmbientEmitters` phone cap = half of 8. Scenario `floor-ambience` already registered.
- `hawkesgen_estimate`: ElevenLabs 7 beds + 12 oneshots ≈ **$0 GPU / $0.14 API** (fits the $0.28 GPU budget); open path ≈ $0.19–0.22 GPU.
- Tried one `audio.sfx` ElevenLabs bed and one open `audio.sfx` / `sound_effect`: all **refused — org credits 1, need 8**.
- `hawkesgen_fleet`: `servers: []`, `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` missing in `hawkesgen/.env`.
- Per preference (m_f60905657340 / m_82768131e9d7): **did not requeue**, no fake job ids, kept `.hawkes/pending-gpu.txt`. No `waiting-jobs.txt` (nothing queued).

## Estimate when credits refill
- Prefer ElevenLabs (`provider: "elevenlabs"`, loop 30s beds): $0 GPU.
- Then: `studio_gen_import` each `a_…` → `Assets/Audio/Generated/Core/<id>.ogg` (keep existing `.meta` GUIDs from git), `audio.analyze` loop:true on each bed, mark `generatedFrom` off synth, delete this pending-gpu file.

## Checks this pass
- JSON wiring check (floor_ambience tables vs Level1/Floor1000–997/shaft/audio.json): OK; 19/19 still synth
- Full `--check` also flags missing clip files in this sparse worktree (Audio/ LFS not checked out); beta already has the stand-in oggs
- Unity compile not run here (no editor)
- Conflicts: none
- No browser / build / playtest started (Studio owns those)

## Fun reviewer (self-pass)
**Verdict: thin** on the sound axis until Gen clips land. Wired beds + breathing prop emitters give each place a distinct air in data, but synth stand-ins are not the Hollow Knight bar the criteria ask for. Gaps: (1) Gen beds with loop analyze, (2) Gen oneshots, (3) richer Meadow village life (out of scope).

## Reviewer (self-pass)
**Verdict: revise when credits return** — acceptance 1–2 and phone fps are wired; criterion 3 (`audio.analyze` loop) and “real Gen art” cannot pass until credits ≥8. Do not treat this merge as the Gen-replace done.
