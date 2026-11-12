# Feel VFX kit (Dead Cells bursts)

## Wrong / ask
Combat verbs only had procedural dots/sparks (`Fx.Spark`/`Burst`) and glow — no hand-made flipbook kit, no diamond arc to the HUD.

## Changed
- **`FeelVfx` / `VfxPool` / `DiamondFlyer`** (`Assets/Scripts/Gameplay/Presentation/FeelVfx.cs`): 12 flipbook kinds (muzzle, impact, hit, deathImp/Cinder/Sack, diamond, heroHurt/Death, dash, graze, rewindRipple), pooled (64 desktop / 32 phone, no Instantiate in play), procedural stand-in frames until Gen atlas lands at `Resources/Effects/FeelVfxAtlas`.
- Wired into `GameFeel`, projectile hit/solid impact, `StyleSystem` graze, `AddDiamondsAction` + legacy `PointsPickup`, `PlayerHealth` death, dash / rewind hooks.
- Diamonds arc to the HUD gem within **0.6 s**.
- Playtest **`vfx-kit`** (Level 1; also for phone 390x844): each verb, diamond fly, 50-effect fps ≥ 30, feel.hitStopMs.
- Story / art bible / world bible + `docs/metrics.json` + `Tools/Art/fx/feel-vfx.json` for the Gen bake.

## Gen / GPU
**$0 spent.** `image.generate` refused: org has **1 credit**, needs **5** per image. Fleet also reports AWS keys missing. Procedural stand-ins ship; `.hawkes/pending-gpu.txt` lists the 12 sheets (estimate ~$0.35 under the $0.59 budget once credits refill). No waiting-jobs (nothing queued).

## Checks
- **Compile fix (this pass):** `DiamondFlyer` had nested struct `Fly` and method `Fly` → CS0102; renamed the struct to `FlySlot` (`FeelVfx.cs:574`). Public `DiamondFlyer.Fly(...)` unchanged.
- Scenario named in `.hawkes/scenario.txt`: `vfx-kit`.
- Conflicts with c_43322668c9d9 (Floor 997 payoff) / c_4601f4040056 (OverlordTaunts): **none** (no shared files; that other fix is a different CS0103).

## Fun reviewer
**Verdict: living** on the feel axis (verbs answer with motion; Meadow already has life/sound). Gaps: Gen inked sheets still pending (stand-ins are readable but not art-bible final); phone fps must be proven on beta.

## Reviewer
**Verdict: revise later for Gen art only** — code/criteria wiring is in; acceptance “hand-made” finishes when pending-gpu atlas lands. Mergeable as a first part with stand-ins + playtest.
