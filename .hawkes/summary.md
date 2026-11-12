# Adaptive score cues: Meadow Gen-looped; floors wait on credits

## What was wrong
#375/#378 already shipped ScoreConfig, MusicDirector, `adaptive_score.py`, the 15 `audio.music` takes (project `org_eb0d5c82e8da8c2d.sickofit`), level `score` wiring, `.meta` (loadType 1 / Vorbis / streamed), and playtests `score-floors` / `adaptive-music` (accepts `score_*` with old tracks as fallback). What was still open: seamless `audio.music-loop` + `audio.analyze` on every cue.

## What changed
- Gen-looped the three Meadow cues (`j_2d285323c61f` → `a_c9b6aabc8de1aecb` explore, `j_1c2bf8602b82` → `a_1e2b8a4cc2bfd6ed` fight, `j_ff775b00c240` → `a_eded7a4eb46db363` finale; Gen check ok) and wrote them over `Assets/Audio/Music/score_meadow_{explore,fight,finale}.ogg` (kept existing GUIDs in `.meta`).
- Floors 1000-997 keep the #378 takes: Studio credits hit the floor after the Meadow batch (each music-loop needs 8 credits, 7 left). Listed in `.hawkes/pending-gpu.txt` with library asset ids.
- Scenario: `score-floors`. `python3 Tools/Audio/adaptive_score.py --check` OK. No level JSON edits this pass (already wired).
- Stayed out of enemy-roster redraw (`Floor998.json` / playtest files that agent owns).

## Fun-reviewer
**thin** — Meadow score can breathe; floors still on older takes; no footage yet for fight slam / phone layers. Gaps: Gen-loop floors 1000-997; prove fight≤2s + distinct finale; phone 390x844 via `score-floors`.

## Reviewer
**revise → shipping first part** — Meadow loops + pending list for the 12; scenario pointed at `score-floors`; hello-hole dirt not committed. Full brief done once credits allow the 12 loops + analyze.

## Checks
- `adaptive_score.py --check` OK; meadow clips 30s ogg Vorbis; metas loadType 1 unchanged.
- Unity compile not run here (no editor).
- Conflicts: none. GPU $0 this pass (music-loop is local; reuse of existing music takes). Credits spent: 3× music-loop.
- Leftover: 12× `audio.music-loop` + `audio.analyze` when Studio credits ≥8 each (~96).

## Acceptance map
1. Fight within 2s — `score-floors` / MusicDirector (needs beta playtest)
2. Distinct finale — wired `score.finale` per floor
3. Tape-stop 0.3s — MusicDirector + `score-floors`
4. [phone] — `score-floors` at 390x844 touch
