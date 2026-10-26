using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The shelf behind Yooba's counter (Docs/Story.md, "Yooba's shop"; ui/yooba.json shelves): a row of jars, one slot per
    /// ware. A ware is on the shelf once its requires hold (a level cleared, the quest paid); until then the slot is an
    /// empty dusty gap, so the shop is seen to fill as the hero climbs. A ware that just arrived glows ("NEW") until
    /// Yooba has pointed it out. Presentation only; built from menu shapes, no art.
    /// </summary>
    public sealed class YoobaShelf
    {
        sealed class Slot
        {
            public YoobaWare ware;
            public Image jar;
            public TextMeshProUGUI label;
            public bool stocked;
        }

        readonly List<Slot> slots = new List<Slot>();
        RectTransform root;

        /// <summary>Ids of the wares on the shelf now, in shelf order.</summary>
        public List<string> StockedIds()
        {
            var ids = new List<string>();
            foreach (var slot in slots)
                if (slot.stocked)
                    ids.Add(slot.ware.id);
            return ids;
        }

        public int SlotCount => slots.Count;

        public static YoobaShelf Build(MenuCanvas menu, YoobaConfig config)
        {
            var shelf = new YoobaShelf();
            if (config.shelves.Count == 0)
                return shelf;
            var place = config.shelf;
            shelf.root = MenuGraphics.NewRect("YoobaShelf", menu.Root);
            MenuGraphics.Place(shelf.root, place.anchor, place.offset, place.size);
            MenuResponsive.Track(shelf.root, place, config.shelfPortrait, MenuElementKind.Text, menu.Responsive);
            var row = shelf.root.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            // The plank under the jars.
            var plank = MenuGraphics.NewImage(shelf.root, "Plank", new Color(0.32f, 0.2f, 0.1f, 0.9f));
            plank.raycastTarget = false;
            plank.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            plank.rectTransform.anchorMin = new Vector2(0f, 0f);
            plank.rectTransform.anchorMax = new Vector2(1f, 0.12f);
            plank.rectTransform.offsetMin = plank.rectTransform.offsetMax = Vector2.zero;
            foreach (var ware in config.shelves)
            {
                if (ware == null)
                    continue;
                var holder = MenuGraphics.NewRect("Ware_" + ware.id, shelf.root);
                var jar = MenuGraphics.NewImage(holder, "Jar", ware.color);
                MenuGraphics.Apply(jar, MenuShape.Rounded, 14f);
                jar.raycastTarget = false;
                MenuGraphics.Fill(jar.rectTransform);
                var text = MenuGraphics.Text(holder, "Label", ware.label, menu.Theme.caption, menu.Font, 17f);
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMin = 9f;
                text.fontSizeMax = 17f;
                text.enableVertexGradient = false;
                text.color = new Color(0.1f, 0.07f, 0.05f, 1f);
                shelf.slots.Add(new Slot { ware = ware, jar = jar, label = text });
            }
            return shelf;
        }

        /// <summary>Re-reads what is stocked: returns the wares that are on the shelf and that Yooba has not pointed out yet.</summary>
        public List<YoobaWare> Refresh()
        {
            var fresh = new List<YoobaWare>();
            foreach (var slot in slots)
            {
                bool now = Requirements.Met(slot.ware.requires);
                slot.stocked = now;
                bool isNew = now && !YoobaProgress.HasShownWare(slot.ware.id);
                if (now)
                {
                    slot.jar.color = slot.ware.color;
                    MenuGraphics.SetText(slot.label, isNew ? "NEW!\n" + slot.ware.label : slot.ware.label);
                    if (isNew)
                        fresh.Add(slot.ware);
                }
                else
                {
                    // An empty gap: a dusty outline where the jar will stand.
                    slot.jar.color = new Color(0.12f, 0.08f, 0.05f, 0.55f);
                    MenuGraphics.SetText(slot.label, "·");
                }
            }
            return fresh;
        }
    }
}
