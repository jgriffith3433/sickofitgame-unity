**Wrong:** `MenuPolish.cs` (Readable feedback text, #251) used `MenuShape.Pill` in `HudFloaters.Make`, but `MenuShape` lives in `SickOfIt.Data.Config` and the file had no `using` for it (CS0103).
**Changed:** added `using SickOfIt.Data.Config;` to `Assets/Scripts/Core/UI/Menus/MenuPolish.cs`. Nothing else.
**Checked:** `MenuShape` is defined once (`Data/Config/MenuStyleConfig.cs`); no type declared in MenuPolish.cs shares a name with one in `Data/Config`, so the using adds no ambiguity. Unity compile not run here; only the beta rebuild will prove it. No conflicts. No GPU work.
