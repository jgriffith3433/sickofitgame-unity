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
    /// upgradeShopConfig.layout and ui/theme.json. Click a card to buy its next level. Gloob keeps the shop
    /// (Docs/Story.md, "Gloob between floors"): his portrait and lines (welcome, buy, can't afford, leave) from ui/gloob.json.
    /// </summary>
    public class UpgradeShopManager : MonoBehaviour
    {
        private MenuCanvas menu;
        private UpgradeShopConfig config;
        private TextMeshProUGUI pointsChip;
        private TextMeshProUGUI healthChip;
        private readonly List<(UpgradeDefinition def, MenuCardView view)> cards = new List<(UpgradeDefinition, MenuCardView)>();
        private bool leaving;
        private GloobPortrait gloob;
        private TextMeshProUGUI gloobSubtitle;
        private Coroutine gloobTalk;

        /// <summary>Gloob behind the counter, or null (off in ui/gloob.json).</summary>
        public GloobPortrait Gloob => gloob;
        public string GloobSubtitle => gloobSubtitle != null && gloobSubtitle.transform.parent.gameObject.activeSelf ? gloobSubtitle.text : "";

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
            AddGloob(gd);
            RefreshUI();
        }

        private void AddGloob(GameDefinition gd)
        {
            var settings = gd != null ? gd.gloob : null;
            if (settings == null || !settings.enabled)
                return;
            gloob = GloobPortrait.Create(menu, menu.Root, settings, settings.shopPortrait, settings.shopPortraitPortrait);
            var place = settings.shopSubtitle;
            gloobSubtitle = menu.AddText(menu.Root, "GloobSubtitle", "", menu.Theme.subtitle, place.anchor, place.offset, place.size, out _, 38f);
            gloobSubtitle.enableAutoSizing = true;
            MenuResponsive.Track((RectTransform)gloobSubtitle.transform.parent, place, settings.shopSubtitlePortrait, MenuElementKind.Text, menu.Responsive);
            gloobSubtitle.transform.parent.gameObject.SetActive(false);
            GloobSay(GloobMoment.ShopWelcome, true);
        }

        /// <summary>Gloob says a shop line (unless he is still talking, or 'interrupt').</summary>
        private void GloobSay(GloobMoment moment, bool interrupt)
        {
            if (gloob == null || (!interrupt && gloob.Speaking))
                return;
            var line = GloobPortrait.PickShop(moment);
            if (line == null)
                return;
            if (gloobTalk != null)
                StopCoroutine(gloobTalk);
            gloobTalk = StartCoroutine(GloobLineRoutine(line));
        }

        private System.Collections.IEnumerator GloobLineRoutine(GloobLine line)
        {
            yield return gloob.Say(line, ShowGloobSubtitle);
            ShowGloobSubtitle(null);
            gloobTalk = null;
        }

        private void ShowGloobSubtitle(string text)
        {
            if (gloobSubtitle == null)
                return;
            gloobSubtitle.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
            MenuGraphics.SetText(gloobSubtitle, text ?? "");
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
                    // With Gloob at the counter a card you can't afford still answers (he tells you so).
                    view.Button.interactable = affordable || gloob != null;
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
            {
                SickOfIt.Core.Audio.Sfx.Play(SickOfIt.Core.Audio.Sfx.Events.upgradePick);
                GloobSay(GloobMoment.ShopBuy, true);
            }
            else
            {
                GloobSay(GloobMoment.ShopBroke, true);
            }
            RefreshUI();
        }

        private void OnBackClicked()
        {
            if (leaving) return;
            leaving = true;
            // His goodbye plays on (sounds outlive the scene); the menu loads at once.
            if (gloob != null)
            {
                var line = GloobPortrait.PickShop(GloobMoment.ShopLeave);
                if (line != null)
                {
                    SickOfIt.Core.Audio.Sfx.Play(line.sound);
                    GloobPortrait.Announce("line", $"gloob ({line.moment}): {line.text} ({line.sound}{(SickOfIt.Core.Audio.Sfx.Has(line.sound) ? "" : ", no clip")})");
                }
            }
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
