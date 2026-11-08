Main menu list was still clipped at 1280x720 after #273: with 7 buttons (6 + "More >") the safe-area-shrunk list viewport (~537 units) cannot hold 7 buttons at the 52 px floor plus 36-unit top/bottom padding and 10-unit gaps, so the list overflowed and the first/last buttons were cut.
Change: `MenuListLayout.Apply` (MenuResponsive.cs) now, when even floor-height buttons don't fit, tightens the vertical padding (36 -> 9 units) and gaps (max 4) before giving up, so all seven fit at >= 52 px (the 46.7 px button was also under the 48 px target; now the floor holds). Overflow still anchors at the top.
Checked: reasoned from the screenshot numbers (7*~68 + 24 + 18 ≈ 515 < 537). Unity compile and the `layout` playtest not run here; please run `layout` at 1280x720 and 390x844.
No merge conflicts.
