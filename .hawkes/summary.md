# Place ambience Gen replace — still blocked on credits

## What this pass tried
Resume of #398 (`c_3e1d6d914cbd`): replace synth stand-in beds/oneshots with Hawkes Gen `audio.sfx`. Wiring, playtest `floor-ambience`, world-bible place beds and emitters are already on beta.

## Blocker (unchanged Gen wall)
- Tried one `audio.sfx` (ElevenLabs, loop 30s, project `org_eb0d5c82e8da8c2d.sickofit`): **refused — org credits 1, need 8**.
- `hawkesgen_fleet`: `servers: []`, `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` missing in `hawkesgen/.env`.
- Per preference (m_f60905657340 / m_82768131e9d7): **did not requeue**, no fake job ids, kept `.hawkes/pending-gpu.txt`.

## Estimate when credits refill (within $0.28 GPU budget)
- ElevenLabs path: **$0 GPU, ~$0.14 API** for 7 beds + 12 oneshots (preferred while fleet keys are missing).
- Open `audio.sfx` variants (2 batches): **~$0.19 GPU**.
- Then: `studio_gen_import` each `a_…` → `Assets/Audio/Generated/Core/<id>.ogg` (keep existing `.meta` GUIDs), `audio.analyze` loop:true on each bed, mark `generatedFrom` in audio.json / leave pending_clips empty.

## Checks this pass
- `python3 -I Tools/Audio/floor_ambience.py --check` — pass
- Unity compile not run here (no editor)
- Conflicts: none
- No browser / build / playtest started (Studio owns those)

## Fun reviewer (self-pass)
**Verdict: thin** on the sound axis until Gen clips land. Wired beds + breathing prop emitters give each place a distinct air in data, but synth stand-ins are not the Hollow Knight bar the criteria ask for. Gaps: (1) Gen beds, (2) Gen oneshots + loop analyze, (3) Meadow visual village life (out of scope).

## Reviewer (self-pass)
**Verdict: hold / revise when credits return** — acceptance criteria 1–2 and phone fps are wired; criterion 3 (`audio.analyze` loop) and “real Gen art” cannot pass until credits ≥8. Do not merge as “done.”
