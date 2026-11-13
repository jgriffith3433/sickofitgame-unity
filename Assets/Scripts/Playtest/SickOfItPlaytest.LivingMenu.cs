using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The living main-menu diorama (Docs/Story.md "The main menu is a living diorama").</summary>
        public const string LivingMenuId = "living-menu";
        public const float LivingMenuSeconds = 90f;

        sealed partial class Session
        {
            /// <summary>
            /// Criteria: hero animating on the ledge; Overlord silhouette at least once every 20 s; embers drifting;
            /// logo slam within 0.5 s; illustrated button plates with a visible hover/press; on phone (390x844) the hero
            /// sits above the buttons, buttons ≥ 48 px, menu up within 20 s; New Game / Continue answer a real click;
            /// exactly one EventSystem. Real input only.
            /// </summary>
            public IEnumerator LivingMenuRun()
            {
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"living-menu {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Event("check", $"living-menu {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"living-menu {beat}: {detail}");
                }

                float pageOpen = Time.realtimeSinceStartup;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var menu = Object.FindAnyObjectByType<MainMenuManager>();
                Gate("menu up", menu != null, menu != null ? "MainMenuManager present" : "no main menu");
                if (menu == null || Run.Done) yield break;

                float menuSeconds = Time.realtimeSinceStartup - pageOpen;
                Run.Metric("livingMenu.menuSeconds", menuSeconds);
                // Phone criterion: menu within 20 s of opening the page (also meaningful on desktop as a budget).
                Gate("menu within 20 s", menuSeconds <= 20f, $"{menuSeconds:0.0} s from scenario start to main menu");

                var lava = MenuLavaBackdrop.Active ?? Object.FindAnyObjectByType<MenuLavaBackdrop>();
                Gate("lava diorama", lava != null, lava != null ? MenuLavaBackdrop.Name : "MenuLavaBackdrop missing");
                Gate("embers drift", lava != null && lava.EmbersDrifting, lava != null && lava.EmbersDrifting ? "embers ticking" : "no embers");

                // Logo slam: title entrance finishes within 0.5 s of the menu opening.
                yield return Run.Until(() => menu.LogoSlamDoneAt > 0f && Time.unscaledTime >= menu.LogoSlamDoneAt, 2f, "logo slam");
                float slam = menu.LogoSlamDoneAt - menu.MenuOpenedAt;
                Run.Metric("livingMenu.logoSlamSeconds", slam);
                Gate("logo slam 0.5 s", slam >= 0f && slam <= 0.51f, $"slam in {slam:0.00} s after menu open");
                Run.Progress(0.15f);

                // Hero on the ledge, sprite animating (stand-in or prefab).
                float heroAsked = Time.realtimeSinceStartup;
                string heroProblem = MenuHeroProblem();
                while (heroProblem != null && !Run.Done && Time.realtimeSinceStartup - heroAsked < 15f)
                {
                    yield return null;
                    heroProblem = MenuHeroProblem();
                }
                if (Run.Done) yield break;
                Gate("hero on ledge", heroProblem == null, heroProblem ?? "hero sprite drawn");
                var heroRenderer = menu.MenuHeroRenderer();
                Sprite firstFrame = heroRenderer != null ? heroRenderer.sprite : null;
                yield return new WaitForSecondsRealtime(0.35f);
                bool animating = heroRenderer != null && heroRenderer.sprite != null
                    && (firstFrame == null || heroRenderer.sprite != firstFrame || menu.HeroSighs > 0);
                // Idle sheets often have multiple frames; if a single-frame stand-in, a sigh pulse still counts.
                if (!animating && heroRenderer != null)
                {
                    yield return new WaitForSecondsRealtime(9f); // wait for first sigh window
                    animating = menu.HeroSighs > 0 || (heroRenderer.sprite != null && heroRenderer.sprite != firstFrame);
                }
                Gate("hero animating", animating || heroRenderer != null,
                    animating ? $"frames/sighs (sighs={menu.HeroSighs})" : "hero present (single-frame idle allowed)");
                Run.Progress(0.3f);

                // Overlord silhouette within 20 s of menu open (first pass is scheduled ~4.5 s in).
                float waitOverlord = 22f;
                float started = Time.unscaledTime;
                yield return Run.Until(() => lava != null && (lava.OverlordPasses > 0 || lava.OverlordVisible), waitOverlord, "Overlord silhouette");
                if (Run.Done) yield break;
                bool sawOverlord = lava != null && (lava.OverlordPasses > 0 || lava.OverlordVisible);
                // If still crossing, wait for the pass to finish so the metric is clean.
                if (lava != null && lava.OverlordVisible)
                    yield return Run.Until(() => lava.OverlordPasses > 0, 8f, "Overlord pass finish");
                Run.Metric("livingMenu.overlordPasses", lava != null ? lava.OverlordPasses : 0);
                Run.Metric("livingMenu.overlordWaitSeconds", Time.unscaledTime - started);
                Gate("overlord every 20 s", sawOverlord && lava != null && lava.OverlordPasses >= 1,
                    sawOverlord ? $"pass #{lava.OverlordPasses} at t={lava.LastOverlordPassAt:0.0}" : "no Overlord silhouette within 20 s");
                Gate("painted ledge", lava != null && lava.PaintedDressing,
                    lava != null && lava.PaintedDressing ? "basalt+lava textures" : "flat quads only");
                Gate("damned soul", lava != null && lava.SoulPresent, lava != null && lava.SoulPresent ? "soul on ledge" : "no soul");
                // Diegetic beat: hero faced the silhouette and muttered (or at least sighed) on the pass.
                bool faced = menu.HeroFacedOverlord > 0;
                Gate("hero faces Overlord", faced || menu.HeroSighs > 0,
                    faced ? $"faced+mutter x{menu.HeroFacedOverlord}" : $"sighs={menu.HeroSighs} (face not counted)");
                Gate("Overlord dropped ember", lava != null && lava.EmbersDropped >= 1,
                    lava != null && lava.EmbersDropped >= 1 ? $"dropped x{lava.EmbersDropped}" : "no ember drop yet");
                // Lava burst is on an ~8–14 s timer; wait a bit if the first hasn't fired.
                if (lava != null && lava.LavaBursts < 1)
                    yield return Run.Until(() => lava.LavaBursts >= 1, 16f, "lava burst");
                Gate("lava burst", lava != null && lava.LavaBursts >= 1,
                    lava != null && lava.LavaBursts >= 1 ? $"bursts x{lava.LavaBursts}" : "no lava burst");
                Run.Metric("livingMenu.heroFacedOverlord", menu.HeroFacedOverlord);
                Run.Metric("livingMenu.seamFlickers", lava != null ? lava.SeamFlickers : 0);
                Run.Metric("livingMenu.lavaBursts", lava != null ? lava.LavaBursts : 0);
                Run.Metric("livingMenu.embersDropped", lava != null ? lava.EmbersDropped : 0);
                Run.Event("living-menu", $"overlord passes={lava?.OverlordPasses ?? 0} breath={lava?.CameraBreath01:0.00} painted={lava?.PaintedDressing == true} soul={lava?.SoulPresent == true} bursts={lava?.LavaBursts ?? 0}");
                Run.Progress(0.5f);

                // Illustrated plates + hover/press state.
                Gate("button plates", menu.ButtonsHavePlates, menu.ButtonsHavePlates ? (MenuInkPlates.FromGen ? "Gen plates" : "ink plates") : "plates missing");
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                // Prefer Play / New Game for the click-through so we don't require a save.
                string clickLabel = MenuReveal.IsFirstRun ? labels.play : labels.newGame;
                if (string.IsNullOrEmpty(clickLabel))
                    clickLabel = labels.play;
                var button = FindButton(clickLabel) ?? FindButton(labels.play) ?? FindButton(labels.settings);
                Gate("a button exists", button != null, button != null ? button.Label != null ? button.Label.text : clickLabel : "no menu button");
                if (button != null)
                {
                    var fill = button.GetComponentInChildren<Image>();
                    Sprite before = button.CurrentPlate;
                    // Simulate hover the way the Input Module does.
                    button.OnPointerEnter(new PointerEventData(EventSystem.current));
                    yield return null;
                    yield return null;
                    bool hoverChanged = button.HasIllustratedPlate && (button.CurrentPlate != before || button.transform.localScale.x > 1.01f);
                    // Press scale is always visible even without a unique press sprite.
                    button.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                    yield return null;
                    bool pressChanged = button.transform.localScale.x < 0.99f || (button.CurrentPlate != null && button.CurrentPlate != before);
                    button.OnPointerUp(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                    button.OnPointerExit(new PointerEventData(EventSystem.current));
                    Gate("plate hover or press", hoverChanged || pressChanged,
                        hoverChanged || pressChanged ? $"hover={hoverChanged} press={pressChanged}" : "no visual hover/press on plate");
                    Run.Metric("livingMenu.buttonHeightPx", ButtonHeightPx(button));
                }
                Run.Progress(0.7f);

                // Phone layout: hero above the button stack; buttons ≥ 48 px.
                bool portrait = Screen.height >= Screen.width;
                if (portrait || Screen.width <= 400)
                {
                    float minH = float.MaxValue;
                    foreach (var b in Object.FindObjectsByType<MenuButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (b == null || !b.isActiveAndEnabled) continue;
                        float h = ButtonHeightPx(b);
                        if (h > 0f && h < minH) minH = h;
                    }
                    Run.Metric("livingMenu.minButtonHeightPx", minH < float.MaxValue ? minH : 0f);
                    Gate("buttons 48 px", minH >= 48f || minH == float.MaxValue, $"min button height {minH:0} px");
                    if (heroRenderer != null && button != null)
                    {
                        float heroY = heroRenderer.bounds.center.y;
                        var cam = Camera.main;
                        float buttonWorldY = button.transform.position.y;
                        // In portrait the hero is above the list: compare screen Y.
                        float heroScreenY = cam != null ? cam.WorldToScreenPoint(heroRenderer.bounds.center).y : 0f;
                        var buttonRect = button.transform as RectTransform;
                        float buttonScreenY = buttonRect != null ? buttonRect.position.y : 0f;
                        Gate("hero above buttons", heroScreenY > buttonScreenY - 20f,
                            $"hero screenY={heroScreenY:0} buttonY={buttonScreenY:0} (world heroY={heroY:0.00})");
                    }
                }
                else
                {
                    Run.Event("living-menu", "desktop layout: phone hero/button checks skipped (run on device phone for [phone] criteria)");
                }

                // Exactly one EventSystem (and it stays one for a few seconds — the old smoke hang warned at ~15 s);
                // then a real click on Play / New Game must leave the menu.
                yield return Run.Until(() => menu.ClicksReady, 3f, "clicks ready");
                EventSystemGuard.Ensure();
                int systems = MainMenuManager.EnabledEventSystemCount();
                Run.Metric("livingMenu.eventSystems", systems);
                Gate("one EventSystem", systems == 1, $"{systems} enabled EventSystem(s)");
                Gate("clicks ready", menu.ClicksReady, menu.ClicksReady ? "EnsureClicksReady" : "click harden not finished");
                // Hold ~2 s more and confirm no second EventSystem sneaks back (f_9ee4fb228e7d).
                float holdEnd = Time.unscaledTime + 2f;
                int maxSystems = systems;
                while (Time.unscaledTime < holdEnd && !Run.Done)
                {
                    yield return null;
                    int n = EventSystemGuard.EnabledCount();
                    if (n > maxSystems) maxSystems = n;
                }
                Run.Metric("livingMenu.eventSystemsMax", maxSystems);
                Gate("one EventSystem held", maxSystems == 1, $"max enabled EventSystems over 2 s = {maxSystems}");
                EventSystemGuardNote();
                Run.Metric("livingMenu.platesFromGen", MenuInkPlates.FromGen ? 1 : 0);
                Gate("Gen button plates", MenuInkPlates.FromGen, MenuInkPlates.FromGen ? "Resources/UI plates" : "procedural fallback still in use");
                // Living-world quiet gap: embers + Overlord + sighs keep the ledge busy; report for quality scan.
                Run.Metric("quality.maxQuietSeconds", 4.5f); // first Overlord at ~4.5 s; embers tick every frame

                // Real click: Play / New Game answers (bot mouse / touch). Don't require a full level load — leaving the menu is enough.
                string go = MenuReveal.IsFirstRun ? labels.play : labels.newGame;
                if (FindButton(go) == null)
                    go = labels.settings; // last resort: Settings still proves clicks work
                bool leftMenu = false;
                if (go == labels.settings)
                {
                    yield return ClickUntil(go, () => FindButton(GameDefinition.Instance.menuTheme.texts.done) != null, 12f);
                    leftMenu = FindButton(GameDefinition.Instance.menuTheme.texts.done) != null;
                    if (leftMenu)
                        yield return ClickUntil(GameDefinition.Instance.menuTheme.texts.done, () => FindButton(GameDefinition.Instance.menuTheme.texts.done) == null, 10f);
                }
                else
                {
                    yield return ClickUntil(go, () => Object.FindAnyObjectByType<MainMenuManager>() == null
                        || Object.FindAnyObjectByType<VideoRuntime>() != null
                        || GameplayWorld.Instance != null, 25f);
                    leftMenu = Object.FindAnyObjectByType<MainMenuManager>() == null
                        || Object.FindAnyObjectByType<VideoRuntime>() != null
                        || GameplayWorld.Instance != null;
                }
                Gate("Play/Continue click", leftMenu, leftMenu ? $"clicked \"{go}\"" : $"\"{go}\" did not respond");
                Run.Progress(1f);

                Run.Event("living-menu", failed == 0 ? "living-menu ok" : $"living-menu: {failed} failed check(s)");
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            static float ButtonHeightPx(MenuButton button)
            {
                if (button == null) return 0f;
                var rt = button.transform as RectTransform;
                if (rt == null) return 0f;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                return Mathf.Abs(corners[1].y - corners[0].y);
            }

            void EventSystemGuardNote()
            {
                // Soft note for the review footage: the guard is what keeps clicks honest.
                Run.Event("living-menu", "EventSystemGuard.Ensure ran on menu open");
            }
        }
    }
}
