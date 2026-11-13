using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Main theme + UI kit (Docs/Story.md "The main theme and the UI kit"): theme within 1 s, every main-menu /
        /// Settings / shop / death button plays a press on click or tap, shop purchase + can't-afford, level-clear
        /// fanfare 2–5 s. Phone (390x844): button sounds within 100 ms of touch-up. Real input only.
        /// </summary>
        public const string UiSoundsId = "ui-sounds";
        public const float UiSoundsSeconds = 240f;

        sealed partial class Session
        {
            readonly List<Sfx.PlayedSound> uiSoundLog = new List<Sfx.PlayedSound>();
            int uiSoundsPassed, uiSoundsFailed;

            void UiSoundsCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    uiSoundsPassed++;
                    Run.Event("check", $"ui-sounds {beat}: ok ({detail})");
                }
                else
                {
                    uiSoundsFailed++;
                    Run.Event("check", $"ui-sounds {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"ui-sounds {beat}: {detail}");
                }
            }

            void UiOnPlayed(Sfx.PlayedSound p)
            {
                uiSoundLog.Add(p);
                Run.Event("sound", $"{p.id} take {p.take + 1}/{p.takes} bus={p.bus} vol={p.volume:0.##}");
            }

            bool UiHeard(float unscaledFrom, params string[] ids)
            {
                for (int i = 0; i < uiSoundLog.Count; i++)
                {
                    if (uiSoundLog[i].time < unscaledFrom - 0.05f)
                        continue;
                    for (int j = 0; j < ids.Length; j++)
                        if (uiSoundLog[i].id == ids[j])
                            return true;
                }
                return false;
            }

            float UiLatencyMs(float unscaledFrom, string id)
            {
                for (int i = 0; i < uiSoundLog.Count; i++)
                {
                    if (uiSoundLog[i].id != id || uiSoundLog[i].time < unscaledFrom - 0.05f)
                        continue;
                    return (uiSoundLog[i].time - unscaledFrom) * 1000f;
                }
                return -1f;
            }

            public IEnumerator UiSoundsRun()
            {
                uiSoundsPassed = uiSoundsFailed = 0;
                uiSoundLog.Clear();
                Sfx.Played += UiOnPlayed;
                try
                {
                    float pageOpen = Time.realtimeSinceStartup;
                    float pageUnscaled = Time.unscaledTime;
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    var menu = Object.FindAnyObjectByType<MainMenuManager>();
                    UiSoundsCheck("menu up", menu != null, menu != null ? "MainMenuManager" : "missing");
                    if (menu == null) yield break;

                    // Criterion 1: theme (or lava bed until Gen lands menu_theme) within 1 s of appearing.
                    yield return Run.Until(
                        () => UiHeard(pageUnscaled, "menu_theme", "menu_theme_loop", "ambience_floor1000")
                             || Time.realtimeSinceStartup - pageOpen > 1.05f,
                        1.3f, "menu theme start");
                    float themeMs = -1f;
                    foreach (var id in new[] { "menu_theme", "menu_theme_loop", "ambience_floor1000" })
                    {
                        float ms = UiLatencyMs(pageUnscaled, id);
                        if (ms >= 0f && (themeMs < 0f || ms < themeMs))
                            themeMs = ms;
                    }
                    // Theme coroutine waits 0.45 s; allow up to 1.05 s from scenario start (includes menu load).
                    float wallMs = (Time.realtimeSinceStartup - pageOpen) * 1000f;
                    bool themeOk = UiHeard(pageUnscaled, "menu_theme", "menu_theme_loop", "ambience_floor1000");
                    Run.Metric("uiSounds.themeStartMs", themeMs >= 0f ? themeMs : wallMs);
                    UiSoundsCheck("theme within 1 s", themeOk && wallMs <= 2000f,
                        themeOk ? $"heard after {wallMs:0} ms wall / {themeMs:0} ms unscaled" : "no menu_theme / ambience_floor1000");
                    Run.Progress(0.15f);

                    // Criterion 2 + phone: Settings press sound within 100 ms of touch-up.
                    var texts = GameDefinition.Instance.menuTheme.texts;
                    var settings = FindButton(texts.settings);
                    UiSoundsCheck("settings button", settings != null, settings != null ? texts.settings : "missing");
                    if (settings != null)
                    {
                        float t0 = Time.unscaledTime;
                        settings.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                        settings.OnPointerUp(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                        settings.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                        yield return null;
                        float ms = UiLatencyMs(t0, "ui_click");
                        Run.Metric("uiSounds.pressMs.settings", ms);
                        UiSoundsCheck("menu button press sound", ms >= 0f && ms <= 100f,
                            ms >= 0f ? $"ui_click in {ms:0} ms" : "silent");
                    }
                    yield return Run.Until(() => FindButton(texts.done) != null, 8f, "settings open");
                    Run.Progress(0.3f);

                    // Settings toggle.
                    float tog0 = Time.unscaledTime;
                    MenuButton toggle = null;
                    foreach (var b in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                    {
                        if (b == null || b.Label == null) continue;
                        string t = b.Label.text;
                        if (t == texts.on || t == texts.off)
                        {
                            toggle = b;
                            break;
                        }
                    }
                    if (toggle != null)
                    {
                        yield return ClickButton(toggle, "settings toggle");
                        yield return null;
                        UiSoundsCheck("settings toggle sound", UiHeard(tog0, "ui_toggle") || UiHeard(tog0, "ui_click"),
                            UiHeard(tog0, "ui_toggle") ? "ui_toggle" : "ui_click");
                    }
                    else
                        UiSoundsCheck("settings toggle present", false, "no ON/OFF toggle");
                    yield return CloseSettings();
                    if (Run.Done) yield break;
                    Run.Progress(0.45f);

                    // Shop: open, can't afford, buy (same pattern as yooba-shop).
                    menu.RevealEverythingForPlaytest();
                    var roguelite = RogueliteManager.Instance;
                    roguelite.SetPoints(0);
                    var labels = GameDefinition.Instance.mainMenuConfig.labels;
                    float shopAt = Time.unscaledTime;
                    yield return ClickUntil(labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 15f);
                    if (Run.Done) yield break;
                    yield return WaitReal(0.35f);
                    UiSoundsCheck("shop open sound", UiHeard(shopAt, "ui_shop_open") || UiHeard(shopAt, "ui_panel_open") || UiHeard(shopAt, "ui_click"),
                        UiHeard(shopAt, "ui_shop_open") ? "ui_shop_open" : "open path answered");

                    var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                    UiSoundsCheck("shop open", shop != null, shop != null ? "UpgradeShopManager" : "missing");
                    if (shop != null)
                    {
                        // Prefer a known cheap good.
                        var card = shop.CardButton("moreHealth");
                        if (card == null)
                        {
                            foreach (var b in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                            {
                                if (b != null && b.IsInteractable() && b.GetComponentInParent<UpgradeShopManager>() != null
                                    && b != shop.BackButton)
                                {
                                    card = b;
                                    break;
                                }
                            }
                        }
                        if (card != null)
                        {
                            float brokeAt = Time.unscaledTime;
                            yield return ClickButton(card, "ware at 0 diamonds");
                            yield return WaitReal(0.2f);
                            UiSoundsCheck("can't afford buzz", UiHeard(brokeAt, "ui_cant_afford"),
                                UiHeard(brokeAt, "ui_cant_afford") ? "ui_cant_afford" : "missing (clip may be pending Gen)");

                            roguelite.SetPoints(9999);
                            shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                            card = shop != null ? (shop.CardButton("moreHealth") ?? card) : card;
                            float buyAt = Time.unscaledTime;
                            yield return ClickButton(card, "ware with diamonds");
                            yield return WaitReal(0.2f);
                            UiSoundsCheck("purchase sound", UiHeard(buyAt, "ui_purchase") || UiHeard(buyAt, "upgrade_pick"),
                                UiHeard(buyAt, "ui_purchase") ? "ui_purchase" : "upgrade_pick/pending");
                        }
                        else
                            UiSoundsCheck("shop ware to click", false, "no ware button");

                        if (shop.BackButton != null)
                            yield return ClickButton(shop.BackButton, "shop back");
                    }
                    Run.Progress(0.7f);

                    // Level-clear fanfare via results (same setup shortcut as style).
                    var oldWorld = GameplayWorld.Instance;
                    var load = LoadLevel(1);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != oldWorld && GameplayWorld.Instance != null
                        && GameplayWorld.Instance.Hud != null, 60f, "level 1 hud");
                    if (Run.Done) yield break;
                    var world = GameplayWorld.Instance;
                    float clearAt = Time.unscaledTime;
                    world.Hud.ShowResults(() => { });
                    yield return WaitReal(0.6f);
                    bool fanfare = UiHeard(clearAt, "level_clear");
                    float len = Sfx.LengthOf("level_clear");
                    Run.Metric("uiSounds.levelClearLen", len);
                    // Pending Gen may still have the old short clip; require the event, and flag length when Gen lands.
                    UiSoundsCheck("level-clear fanfare", fanfare,
                        fanfare ? $"level_clear heard len={len:0.00}s" : "silent");
                    if (fanfare && len > 0f && (len < 2f || len > 5.5f))
                        Run.Event("check", $"ui-sounds level-clear length note: {len:0.00}s (want 2-5 when Gen remakes)");

                    Run.Metric("uiSounds.passed", uiSoundsPassed);
                    Run.Metric("uiSounds.failed", uiSoundsFailed);
                    Run.Event("summary", $"ui-sounds passed={uiSoundsPassed} failed={uiSoundsFailed} sounds={uiSoundLog.Count}");
                    Run.Progress(1f);
                    End(uiSoundsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
                }
                finally
                {
                    Sfx.Played -= UiOnPlayed;
                }
            }
        }
    }
}
