# Speaker portraits: the paintings are in and wired

**What a player gets.** Whenever the hero, the Overlord, Yooba, Rook or Wick speaks a voiced line, their painted portrait slides in (0.12 s), wears the line's mood, flaps its mouth with the clip's loudness, shuts within 0.2 s of the line ending and slides away. The code was committed earlier (`SpeakerPortraits`, `PortraitVoice`, 604 `mood` fields in `audio.json`, Yooba's shop frame, playtest `portraits`). This pass adds the art.

**This pass**
- Judged all 20 mouth-open frames (Gen jobs `j_ab44c910e9fb` ... `j_21aced7a7652`). Every one keeps the face, pose and outfit of its closed frame, the mouth is clearly open, and there is no stray text or bubble. No redo was needed.
- Cut out all 40 frames (20 closed, 20 open) with `image.remove-background`, then trimmed each closed/open pair to one shared box so the flap doesn't shift the bust. Each is 512x512 and the bust sits on the card's bottom edge. They are `Assets/Art/Portraits/<char>_<mood>[_open].png` with `.meta` files (single sprite, no mipmaps, fresh GUIDs).
- `Assets/GameConfig/ui/portraits.json`: every character now has its four frames (neutral, smug, laughing, shocked), each with closed and open sprites. The stand-in bust stays only as a fallback in code.
- AGENTS.md "Speaker portraits" notes where the paintings live.

**GPU spend (estimate, from the jobs' own costs): about $0.58 of $0.75.** The masters and closed edits cost about $0.27. The 20 open edits cost about $0.23, most of it two model loads at about $0.07 each. The 40 cutouts cost about $0.08 (the first $0.008, then $0.0018 each).

**Not run here (no Unity or browser in this session)**
- The C# and the JSON import have not been compiled or imported: Studio's build does the config import. Check `Logs/GameConfig.log` for the 40 sprite paths if the build complains.
- The `portraits` playtest is in `.hawkes/scenario.txt`. Run it at 390x844 touch. It checks size >= 72 px (117 px at that size), slide <= 0.15 s, clear of the hero and touch stick, mouth flaps, shut within 0.2 s and 4 expressions per character.
- I judged the art by eye on contact sheets only. Check the cards in the game: the Overlord's and Rook's laughing frames fill the whole width of the card, and Wick is a small figure (he is trimmed tight, but he is drawn small).

**Caveats**
- Hero and Overlord masters are `image.edit`s of Gen concept assets (the repo has no in-game sheet PNGs: LFS isn't checked out). Rook, Wick and Yooba were generated from the art-spec descriptions.
- `.hawkes/pending-gpu.txt` now holds only another change's backdrop list ("Gen out of credits..."). It isn't mine and I left it. The portrait line is removed because nothing is left to paint here.
- Merge conflicts: none. Other files touched: only `AGENTS.md` (one line).
