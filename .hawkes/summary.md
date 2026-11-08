**What was wrong:** two assembly-reference gaps, not code bugs.
- `SickOfIt.Tests.Editor.asmdef` did not reference `SickOfIt.Core.Progression`, where `NetRetry` (added in #268) lives, so `NetRetryTests.cs` hit CS0234.
- `SickOfIt.Playtest.asmdef` did not reference `nappin.MobileControllerFramework`, so `SickOfItPlaytest.IntroSkip.cs` (from #270/#271) hit CS0246 on `MobileControllerFramework.Button`.

**Changed:** one reference added to each of those two asmdef files. No code changed, so #268's behaviour is untouched.

**Not checked:** Unity compile not run here. Only the rebuild of beta can show it passes. I checked by reading that both namespaces exist and that other asmdefs (Core.Progression, Core) already reference the same assemblies, with no circular dependency (neither the Playtest nor the Tests assembly is referenced by them).
