# Intro video: tiny skip button, skip needed two taps

Found: `TransportButtonLayout` only enlarged the skip/rewind buttons on a "touch layout", so the phone run kept a ~32 px skip icon.
Changed:
- `TransportButtonLayout.cs`: buttons are always at least 48 px on screen.
- `VideoRuntime.cs`: a tap or click anywhere off the rewind button skips at once (after a 0.6 s grace so the tap that opened the video can't skip it).

Not done: the black picture. The video is a baked sprite sequence (`SpriteFramePlayer`), and I can't render WebGL here to see why the playtest phone saw black. The stall watchdog already moves on if it never plays. J had answered "won't fix" on an earlier intro-video finding (f_f35ed5063d76); this one is a separate finding with no thread, so I limited the change to the skip controls.
Checked: read the code only. Unity compile not run here. No conflicts.
