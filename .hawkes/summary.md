# Fix: beta build failed on Floor701 import

**Wrong:** `Floor701.json` (Circle VIII's Bridge Wraith arena) had `"scale": [1.0, 1]` on 10 `spawn` actions. `SpawnAction` has no `scale` field, so the GameConfig import failed and stopped the deploy.

**Changed:**
- `Assets/GameConfig/levels/Floor701.json`: removed the 10 `scale` fields. They were identity values, so the level plays the same.
- `Tools/Levels/rune_lower.py`: the generator no longer writes them, so a re-run won't bring the error back.

**Checked:** a script found no other level with a `scale` on a `spawn` action. Unity isn't available here, so I couldn't run the import. The reported errors were all this one field. The cinematic plan (`Docs/LoreVideos.md`) is unchanged. GPU price estimate is in that doc. No GPU was started, and nothing was generated.

**For J:** run the import or the next beta build to confirm. Scenario `smoke` is only a sanity run. The plan itself is a document with no in-game content to play.
