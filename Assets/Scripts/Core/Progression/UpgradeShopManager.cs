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
    /// Manages the Upgrade Shop scene where players spend diamonds on permanent upgrades. A card per upgrade in the
    /// catalog (player.availableUpgrades, else upgradeShopConfig.upgrades), built at runtime from
    /// upgradeShopConfig.layout and ui/theme.json. Click a card to buy its next level.
    /// </summary>
    public class UpgradeShopManager : MonoBehaviour
    {
        private MenuCanvas menu;
        private UpgradeShopConfig config;
        private TextMeshProUGUI pointsChip;
        private TextMeshProUGUI healthChip;
        private readonly List<(UpgradeDefinition def, MenuCardView view)> cards = new List<(UpgradeDefinition, MenuCardView)>();
        private bool leaving;

        private void Start()
        {
            var gd = GameDefinition.Instance;
            config = gd != null ? gd.upgradeShopConfig : new UpgradeShopConfig();
            menu = MenuCanvas.Create("UpgradeShopUI", gd != null ? gd.menuTheme : null, config.layout, gameObject);
            var list = menu.AddList(config.layout.list, menu.Theme.card.size);

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
                }, () => Buy(def));
                cards.Add((def, view));
            }

            healthChip = menu.AddChip("");
            pointsChip = menu.AddChip("", menu.Theme.accent);

            menu.AddBackButton(OnBackClicked);
            // A testing aid: only in the editor and development builds.
            if (config.showDebugAddPoints && (Application.isEditor || Debug.isDebugBuild))
            {
                menu.AddButtonAt(new Vector2(1f, 0f), new Vector2(-170f, 80f), $"+{config.debugPointsAmount} ♦ (debug)",
                    menu.Theme.smallButton, OnAddPointsDebugClicked);
            }
            RefreshUI();
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
                    view.Button.interactable = affordable;
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
            if (RogueliteManager.Instance.TryPurchase(def))
                SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.upgradePick);
            RefreshUI();
        }

        private void OnBackClicked()
        {
            if (leaving) return;
            leaving = true;
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        private void OnAddPointsDebugClicked()
        {
            RogueliteManager.Instance.AddPoints(config.debugPointsAmount);
            RefreshUI();
        }
    }
}
