# Try it
1. Beta's rebuild should import the game config cleanly: its log has `Game config verified for the build ... fingerprint`.
2. Play Floor 1000 on beta, desktop and phone (390x844):
   - only the hearts and the rewind prompt on the HUD, with no dash chip and no weapon chip;
   - take a hit: the "Hold R to undo!" / "Hold ◄◄ to undo!" hint sits clear of the rewind button.
3. Playtest `floors` logs `check` events "floor 1000 shows no dash or weapon chip" and "floor 1000 hint and rewind button stay apart".
4. Jump, land, graze a shot, hit an imp: the same sounds as before (the clips didn't change).
