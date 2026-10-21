using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Yooba's shop, where the hero spends diamonds on permanent upgrades: its own scene from the main menu, and
    /// opened over the Shaft by its Visit Yooba button (<see cref="OpenOverShaft"/>; Back closes it and the Shaft
    /// carries on to the next level). A card per upgrade in the catalog (player.availableUpgrades, else
    /// upgradeShopConfig.upgrades), built at runtime from upgradeShopConfig.layout and ui/theme.json. Click a card to
    /// buy its next level. Yooba keeps the shop (Docs/Story.md, "Yooba between floors"): his portrait and lines
    /// (welcome, buy, can't afford, leave) from ui/yooba.json.
    /// </summary>
    public class UpgradeShopManager : MonoBehaviour
    {
        private MenuCanvas menu;
        private UpgradeShopConfig config;
        private TextMeshProUGUI pointsChip;
        private TextMeshProUGUI healthChip;
        private readonly List<(UpgradeDefinition def, MenuCardView view)> cards = new List<(UpgradeDefinition, MenuCardView)>();
        private bool leaving;
        private YoobaPortrait yooba;
        private TextMeshProUGUI yoobaSubtitle;
        private Coroutine yoobaTalk;
        private bool built;
        private MenuButton backButton;

        /// <summary>Back (for the playtest's virtual mouse).</summary>
        public MenuButton BackButton => backButton;
        private bool overlay;
        private Action closed;

        /// <summary>Yooba's shop open over the Shaft, or null.</summary>
        public static UpgradeShopManager OverShaft { get; private set; }

        /// <summary>Opened over the Shaft (Back closes it instead of loading the main menu).</summary>
        public bool IsOverlay => overlay;

        /// <summary>Yooba behind the counter, or null (off in ui/yooba.json).</summary>
        public YoobaPortrait Yooba => yooba;
        public string YoobaSubtitle => yoobaSubtitle != null && yoobaSubtitle.transform.parent.gameObject.activeSelf ? yoobaSubtitle.text : "";

        /// <summary>
        /// Yooba's shop over the Shaft (its Visit Yooba button): built at once on its own canvas above the Shaft's,
        /// with no scene load, so leaving puts the Shaft back where it was. <paramref name="onClosed"/> runs on Back.
        /// </summary>
        public static UpgradeShopManager OpenOverShaft(Action onClosed)
        {
            if (OverShaft != null)
                OverShaft.CloseOverlay(false);
            var go = new GameObject("YoobaShop");
            DontDestroyOnLoad(go);
            var shop = go.AddComponent<UpgradeShopManager>();
            shop.overlay = true;
            shop.closed = onClosed;
            OverShaft = shop;
            shop.Build();
            return shop;
        }

        private void Start() => Build();

        private void Build()
        {
            if (built)
                return;
            AnalyticsManager.Screen("shop");
            built = true;
            var gd = GameDefinition.Instance;
            config = gd != null ? gd.upgradeShopConfig : new UpgradeShopConfig();
            menu = MenuCanvas.Create("UpgradeShopUI", gd != null ? gd.menuTheme : null, config.layout, overlay ? null : gameObject);
            if (overlay)
            {
                // Above the Shaft (60), and gone with this object.
                menu.GetComponent<Canvas>().sortingOrder = 70;
                menu.transform.SetParent(transform, false);
            }
            var list = menu.AddList(config.layout.list, menu.Theme.card.size);
            var cardStyle = menu.Theme.card.Clone();
            cardStyle.tilt = 0f;

            foreach (var upgrade in RogueliteManager.ShopCatalog)
            {
                if (upgrade == null)
                    continue;
                var def = upgrade;
                var view = MenuCard.Add(menu, list, new MenuCardData
                {
                    Caption = "",
                    Title = def.title,
                    Description = def.description,
                    Status = "",
                }, () => Buy(def), cardStyle);
                cards.Add((def, view));
            }

            healthChip = menu.AddChip("");
            pointsChip = menu.AddChip("", menu.Theme.accent);

            backButton = menu.AddBackButton(OnBackClicked);
            // A testing aid: only in the editor and development builds.
            if (config.showDebugAddPoints && (Application.isEditor || Debug.isDebugBuild))
            {
                menu.AddButtonAt(new Vector2(1f, 0f), new Vector2(-170f, 80f), $"+{config.debugPointsAmount} ♦ (debug)",
                    menu.Theme.smallButton, OnAddPointsDebugClicked);
            }
            AddYooba(gd);
            RefreshUI();
        }

        private void AddYooba(GameDefinition gd)
        {
            var settings = gd != null ? gd.yooba : null;
            if (settings == null || !settings.enabled)
                return;
            yooba = YoobaPortrait.Create(menu, menu.Root, settings, settings.shopPortrait, settings.shopPortraitPortrait);
            var place = settings.shopSubtitle;
            yoobaSubtitle = menu.AddText(menu.Root, "YoobaSubtitle", "", menu.Theme.subtitle, place.anchor, place.offset, place.size, out _, 38f);
            yoobaSubtitle.enableAutoSizing = true;
            MenuResponsive.Track((RectTransform)yoobaSubtitle.transform.parent, place, settings.shopSubtitlePortrait, MenuElementKind.Text, menu.Responsive);
            yoobaSubtitle.transform.parent.gameObject.SetActive(false);
            YoobaSay(YoobaMoment.ShopWelcome, true);
        }

        /// <summary>Yooba says a shop line (unless he is still talking, or 'interrupt').</summary>
        private void YoobaSay(YoobaMoment moment, bool interrupt)
        {
            if (yooba == null || (!interrupt && yooba.Speaking))
                return;
            var line = YoobaPortrait.PickShop(moment);
            if (line == null)
                return;
            if (yoobaTalk != null)
                StopCoroutine(yoobaTalk);
            yoobaTalk = StartCoroutine(YoobaLineRoutine(line));
        }

        private System.Collections.IEnumerator YoobaLineRoutine(YoobaLine line)
        {
            yield return yooba.Say(line, ShowYoobaSubtitle);
            ShowYoobaSubtitle(null);
            yoobaTalk = null;
        }

        private void ShowYoobaSubtitle(string text)
        {
            if (yoobaSubtitle == null)
                return;
            yoobaSubtitle.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
            MenuGraphics.SetText(yoobaSubtitle, text ?? "");
        }

        /// <summary>The card of an upgrade (for the playtest's virtual mouse), or null.</summary>
        public MenuButton CardButton(string id)
        {
            foreach (var (def, view) in cards)
                if (def != null && def.id == id)
                    return view.Button;
            return null;
        }

        /// <summary>Refresh all UI elements to reflect current state.</summary>
        public void RefreshUI()
        {
            var roguelite = RogueliteManager.Instance;
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();
            MenuGraphics.SetText(pointsChip, string.Format(menu.Theme.pointsFormat, roguelite.GetPoints()));
            MenuGraphics.SetText(healthChip, string.Format(menu.Theme.healthFormat,
                roguelite.GetCumulativeHealth(totalLevels), roguelite.GetMaxHealth()));

            foreach (var (def, view) in cards)
            {
                if (def.Effect == ShopEffect.None)
                {
                    view.SetCaption("");
                    view.SetStatus(config.comingSoonText, menu.Theme.muted);
                    view.Button.interactable = false;
                    continue;
                }

                int owned = roguelite.GetUpgradeLevel(def.id);
                view.SetCaption(Pips(owned, def.maxLevel));
                int cost = roguelite.GetNextCost(def);
                if (cost < 0)
                {
                    view.SetStatus(config.maxedText, menu.Theme.positive);
                    view.Button.interactable = false;
                }
                else
                {
                    bool affordable = roguelite.CanAfford(def);
                    view.SetStatus(string.Format(config.buyFormat, cost), affordable ? menu.Theme.accent : menu.Theme.muted);
                    // With Yooba at the counter a card you can't afford still answers (he tells you so).
                    view.Button.interactable = affordable || yooba != null;
                }
            }
        }

        static string Pips(int owned, int max)
        {
            owned = Mathf.Clamp(owned, 0, max);
            return new string('●', owned) + "<alpha=#40>" + new string('●', Mathf.Max(0, max - owned)) + "<alpha=#FF>";
        }

        private void Buy(UpgradeDefinition def)
        {
            if (leaving || def.Effect == ShopEffect.None)
                return;
            int cost = RogueliteManager.Instance.GetNextCost(def);
            bool bought = RogueliteManager.Instance.TryPurchase(def);
            AnalyticsManager.Track("shop_purchase", new System.Collections.Generic.Dictionary<string, object>
            {
                { "item", def.id }, { "cost", cost }, { "bought", bought }, { "level", RogueliteManager.Instance.GetUpgradeLevel(def.id) },
                { "diamonds", RogueliteManager.Instance.GetPoints() }, { "over_shaft", overlay },
            });
            if (bought)
            {
                SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.upgradePick);
                YoobaSay(YoobaMoment.ShopBuy, true);
            }
            else
            {
                YoobaSay(YoobaMoment.ShopBroke, true);
            }
            RefreshUI();
        }

        private void OnBackClicked()
        {
            if (leaving) return;
            leaving = true;
            // His goodbye plays on (sounds outlive the scene); the menu loads at once.
            if (yooba != null)
            {
                var line = YoobaPortrait.PickShop(YoobaMoment.ShopLeave);
                if (line != null)
                {
                    SickOfIt.Core.Audio.Sfx.Play(line.sound);
                    YoobaPortrait.Announce("line", $"yooba ({line.moment}): {line.text} ({line.sound}{(SickOfIt.Core.Audio.Sfx.Has(line.sound) ? "" : ", no clip")})");
                }
            }
            if (overlay)
            {
                CloseOverlay(true);
                return;
            }
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        /// <summary>Takes the shop off the Shaft (and tells the Shaft, when 'notify').</summary>
        public void CloseOverlay(bool notify)
        {
            if (!overlay)
                return;
            var then = notify ? closed : null;
            closed = null;
            if (OverShaft == this)
                OverShaft = null;
            AnalyticsManager.Screen("shaft");
            if (this != null)
                Destroy(gameObject);
            then?.Invoke();
        }

        private void OnDestroy()
        {
            if (OverShaft == this)
                OverShaft = null;
        }

        private void OnAddPointsDebugClicked()
        {
            RogueliteManager.Instance.AddPoints(config.debugPointsAmount);
            RefreshUI();
        }
    }
}
