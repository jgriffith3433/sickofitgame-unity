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
        private YoobaShelf shelf;
        /// <summary>His opening lines (welcome and reactions) have all been said or cut off by a buy.</summary>
        public bool OpeningFinished { get; private set; }
        /// <summary>The ledger chip's text ("LOST THINGS 1/3"), or "" while he hasn't asked.</summary>
        public string QuestChipText => questChip != null && questChip.transform.parent.gameObject.activeSelf ? questChip.text : "";
        private TextMeshProUGUI questChip;
        private float quietSince;
        private YoobaConfig yoobaConfig;

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
        /// <summary>The wares on the shelf now (the playtest reads it).</summary>
        public List<string> ShelfWares() => shelf != null ? shelf.StockedIds() : new List<string>();
        public int ShelfSlots => shelf != null ? shelf.SlotCount : 0;
        /// <summary>The lost things found so far (flags set by the pickups on the floors).</summary>
        public int LostFound() => LostThingsFound();
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

            // Every good gets a card; those whose requires aren't met yet are switched off (RefreshUI), so a good
            // bought here that unlocks another (shop:<id>) shows up at once.
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
            AddShelfAndQuest(gd);
            if (yooba != null)
                openingTalk = StartCoroutine(OpeningLines());
            else
                OpeningFinished = true;
            quietSince = Time.unscaledTime;
        }

        // ─── The shop room: shelf, quest, reactions, idle muttering ───

        private void AddShelfAndQuest(GameDefinition gd)
        {
            yoobaConfig = gd != null ? gd.yooba : null;
            if (yoobaConfig == null || !yoobaConfig.enabled)
                return;
            shelf = YoobaShelf.Build(menu, yoobaConfig);
            shelf.Refresh();
            if (yoobaConfig.quest != null && yoobaConfig.quest.enabled && yoobaConfig.quest.things.Count > 0)
                questChip = menu.AddChip("", menu.Theme.muted);
            RefreshQuestChip();
        }

        private int LostThingsFound()
        {
            var quest = yoobaConfig != null ? yoobaConfig.quest : null;
            if (quest == null)
                return 0;
            int found = 0;
            foreach (var thing in quest.things)
                if (UnlockFlags.Has(thing.flag))
                    found++;
            return found;
        }

        private bool QuestComplete() =>
            yoobaConfig != null && yoobaConfig.quest != null && yoobaConfig.quest.enabled && yoobaConfig.quest.things.Count > 0
            && LostThingsFound() == yoobaConfig.quest.things.Count;

        private void RefreshQuestChip()
        {
            if (questChip == null)
                return;
            var quest = yoobaConfig.quest;
            bool asked = UnlockFlags.Has(quest.askedFlag) || LostThingsFound() > 0;
            questChip.transform.parent.gameObject.SetActive(asked);
            bool paid = UnlockFlags.Has(quest.paidFlag);
            MenuGraphics.SetText(questChip, paid ? quest.doneText : string.Format(quest.chipFormat, LostThingsFound(), quest.things.Count));
        }

        /// <summary>
        /// What Yooba says on opening the shop, in order: the welcome, then whatever he has to react to: a lost thing found, the
        /// quest paid out, a ware that just appeared, the quest asked, the deaths he has counted. Each reaction once.
        /// </summary>
        private System.Collections.IEnumerator OpeningLines()
        {
            var queue = new List<YoobaLine>();
            var quest = yoobaConfig.quest;
            bool questOn = quest != null && quest.enabled && quest.things.Count > 0;
            if (questOn)
            {
                foreach (var line in YoobaPortrait.Candidates(YoobaMoment.QuestFound))
                    if (!YoobaProgress.HasSaid(line.sound))
                        queue.Add(line);
                if (QuestComplete() && !UnlockFlags.Has(quest.paidFlag))
                {
                    UnlockFlags.Set(quest.paidFlag);
                    RogueliteManager.Instance.AddPoints(quest.rewardDiamonds);
                    var done = YoobaPortrait.PickShop(YoobaMoment.QuestDone);
                    if (done != null)
                        queue.Add(done);
                    YoobaPortrait.Announce("quest", $"paid {quest.rewardDiamonds} diamonds for {quest.title}");
                    RefreshUI();
                }
                else if (!UnlockFlags.Has(quest.askedFlag))
                {
                    var ask = YoobaPortrait.PickNew(YoobaMoment.QuestOffer);
                    if (ask != null)
                    {
                        UnlockFlags.Set(quest.askedFlag);
                        queue.Add(ask);
                    }
                }
                else if (!UnlockFlags.Has(quest.paidFlag) && LostThingsFound() < quest.things.Count)
                {
                    var nudge = YoobaPortrait.PickNew(YoobaMoment.QuestRemind);
                    if (nudge != null)
                        queue.Add(nudge);
                }
                RefreshQuestChip();
            }
            // Wares new on the shelf since his last word about them (one line each, the first two).
            int stocked = 0;
            foreach (var ware in shelf != null ? shelf.Refresh() : new List<YoobaWare>())
            {
                YoobaProgress.MarkWareShown(ware.id);
                var line = YoobaPortrait.PickShop(YoobaMoment.ShopStocked, ware.id);
                if (line != null && line.item == ware.id && stocked++ < 2)
                    queue.Add(line);
            }
            // The deaths he has counted: the line for the highest count reached (the lower ones are old news).
            var counted = YoobaPortrait.Candidates(YoobaMoment.ShopDeaths);
            if (counted.Count > 0 && !YoobaProgress.HasSaid(counted[counted.Count - 1].sound))
            {
                for (int i = 0; i < counted.Count - 1; i++)
                    YoobaProgress.MarkSaid(counted[i].sound);
                queue.Add(counted[counted.Count - 1]);
            }

            // Let the welcome finish first.
            yield return null;
            while (yooba != null && yooba.Speaking)
                yield return null;
            foreach (var line in queue)
            {
                if (yooba == null || leaving)
                    break;
                YoobaPortrait.Said(line);
                yield return yooba.Say(line, ShowYoobaSubtitle);
                ShowYoobaSubtitle(null);
                quietSince = Time.unscaledTime;
            }
            quietSince = Time.unscaledTime;
            OpeningFinished = true;
            openingTalk = null;
        }

        private Coroutine openingTalk;

        private void Update()
        {
            // Quiet behind the counter: after a while he mutters to himself (a running gag, gossip).
            if (yooba == null || yoobaConfig == null || yoobaConfig.idleLineSeconds <= 0f || leaving)
                return;
            if (yooba.Speaking || yoobaTalk != null || openingTalk != null)
            {
                quietSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - quietSince < yoobaConfig.idleLineSeconds)
                return;
            quietSince = Time.unscaledTime;
            var line = YoobaPortrait.PickIdle();
            if (line == null)
                return;
            YoobaPortrait.Said(line);
            if (yoobaTalk != null)
                StopCoroutine(yoobaTalk);
            yoobaTalk = StartCoroutine(YoobaLineRoutine(line));
        }

        private void AddYooba(GameDefinition gd)
        {
            var settings = gd != null ? gd.yooba : null;
            if (settings == null || !settings.enabled)
                return;
            yooba = YoobaPortrait.Create(menu, menu.Root, settings, settings.shopPortrait, settings.shopPortraitPortrait);
            yoobaConfig = settings;
            var place = settings.shopSubtitle;
            yoobaSubtitle = menu.AddText(menu.Root, "YoobaSubtitle", "", menu.Theme.subtitle, place.anchor, place.offset, place.size, out _, 38f);
            yoobaSubtitle.enableAutoSizing = true;
            MenuResponsive.Track((RectTransform)yoobaSubtitle.transform.parent, place, settings.shopSubtitlePortrait, MenuElementKind.Text, menu.Responsive);
            yoobaSubtitle.transform.parent.gameObject.SetActive(false);
            YoobaSay(YoobaMoment.ShopWelcome, true);
        }

        /// <summary>Yooba says a shop line (unless he is still talking, or 'interrupt').</summary>
        private void YoobaSay(YoobaMoment moment, bool interrupt, string item = null)
        {
            if (yooba == null || (!interrupt && yooba.Speaking))
                return;
            var line = YoobaPortrait.PickShop(moment, item);
            if (line == null)
                return;
            if (interrupt && openingTalk != null)
            {
                // A buy cuts his reactions short; the welcome that started them is not cut.
                StopCoroutine(openingTalk);
                openingTalk = null;
                OpeningFinished = true;
                yooba.Silence();
            }
            if (yoobaTalk != null)
            {
                StopCoroutine(yoobaTalk);
                yooba.Silence();
            }
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

        /// <summary>The card of an upgrade that is for sale (for the playtest's virtual mouse), or null.</summary>
        public MenuButton CardButton(string id)
        {
            foreach (var (def, view) in cards)
                if (def != null && def.id == id && view.Button.transform.parent.gameObject.activeSelf)
                    return view.Button;
            return null;
        }

        /// <summary>The ids of the goods on show, in order (the playtest reads it).</summary>
        public List<string> ShownIds()
        {
            var ids = new List<string>();
            foreach (var (def, view) in cards)
                if (def != null && view.Button.transform.parent.gameObject.activeSelf)
                    ids.Add(def.id);
            return ids;
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
                bool forSale = def.IsForSale(roguelite.GetUpgradeLevel(def.id));
                view.Button.transform.parent.gameObject.SetActive(forSale);
                if (!forSale)
                    continue;
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
                bool weaponGood = def.Effect == ShopEffect.KeepWeapons || def.Effect == ShopEffect.MeleeDamage || def.Effect == ShopEffect.MeleeReach;
                YoobaSay(weaponGood ? YoobaMoment.ShopBuyWeapon : YoobaMoment.ShopBuy, true, weaponGood ? null : def.id);
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
