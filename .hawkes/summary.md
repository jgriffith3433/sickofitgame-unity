# Intro video black / two-tap skip

**Premise differs from the brief:** the intro is not a Unity `VideoPlayer`/`VideoClip`; it is baked sprite sheets (`SpriteFramePlayer`) plus an mp3 (AGENTS.md "Videos ... are baked sprite-sheet sequences"). So the URL/StreamingAssets approach does not apply. The black screen is `PlaybackStartGate`, which holds the clock at 0 for up to 8 s (sheets) + 10 s (audio) while the sheets/clip load on demand.

**Changes**
- `VideoRuntime.cs`: two one-shot timers (no polling): picture not ready after 1.75 s, or playback not started after 4 s -> go straight to the next scene. 1.75 s (not the brief's 3 s) so black never exceeds acceptance criterion 1's 2 s. Any first tap / click / key (except R, the rewind button) / gamepad A or Start skips at once, from frame 0.2 s (also while the gate holds). Pressing the rewind button still starts a hold.
- `TransportButtonLayout.cs`: skip/rewind buttons are always >= 56 px on screen (was only on a touch layout).
- New playtest `intro-skip` (`SickOfItPlaytest.IntroSkip.cs`, in `.hawkes/scenario.txt`): loads Video1 (setup shortcut), checks black time <= 2 s, button heights, and one real mouse click on empty screen skips within 1 s.

**Checks:** Unity compile not run here; C# read for CS errors only. Scenario not run.
**Look at:** a slow phone may now skip the intro when its sheets take > 1.75 s (deliberate, per "no black wait"). Memory notes an earlier "won't fix" from J on intro video fixes; this brief postdates it.
No GPU used; nothing to price. No merge conflicts.
