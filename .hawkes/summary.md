Beta's build error (CS0102 `uiPassed` duplicated in `SickOfItPlaytest.Session`) was already fixed on this branch's base by #423 (UiSounds uses `uiSoundsPassed/Failed`; `uiPassed` now lives only in UiPolish.cs). I grepped the Playtest partials for other duplicated member names and found none. Related build fixes #421 and #422 are also in HEAD.
The clarity change itself (hearts + rewind prompt only on Floor 1000, dash and launcher chips later) landed in #354 and is kept as is. I changed no code.
Unity compile not run here; the build server must confirm. No GPU work, so no estimate.
