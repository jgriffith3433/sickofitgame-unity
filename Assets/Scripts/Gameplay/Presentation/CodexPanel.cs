using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    public enum CodexTab { Enemies, Bosses, Weapons, Characters }

    /// <summary>One page of the Codex: what to show about a creature, a weapon or a character, and whether the hero has met it.</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public string Name;
        public string Note;
        /// <summary>The weakness (enemies, bosses), what the weapon does, or what the character is to Hell.</summary>
        public string Line;
        public Sprite Art;
        public Color Tint = Color.white;
        public bool Met;
        public bool IsWeapon;
        /// <summary>An enemy or a boss: its line is a weakness and it has a tally.</summary>
        public bool Creature;
        public int TimesMet;
        public int TimesKilled;
    }

    /// <summary>
    /// What the Codex lists, built from the content (entities with a displayName, weapons) and ui/codex.json (characters),
    /// and whether the hero has met each (<see cref="CodexProgress"/>, <see cref="WeaponProgress"/> unlock flags).
    /// </summary>
    public static class CodexBook
    {
        public static List<CodexEntry> Entries(CodexTab tab, GameContent content, CodexConfig config)
        {
            switch (tab)
            {
                case CodexTab.Enemies: return Creatures(content, boss: false);
                case CodexTab.Bosses: return Creatures(content, boss: true);
                case CodexTab.Weapons: return Weapons(content);
                default: return Characters(config);
            }
        }

        static bool IsBoss(EntityDef def) => def.tags != null && def.tags.Contains("boss");

        static List<CodexEntry> Creatures(GameContent content, bool boss)
        {
            var list = new List<CodexEntry>();
            if (content == null)
                return list;
            var ids = new List<string>(content.Entities.Keys);
            ids.Sort(StringComparer.Ordinal);
            var byName = new Dictionary<string, CodexEntry>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                var def = content.Entities[id];
                if (def == null || def.team != Team.Enemy || def.health <= 0 || string.IsNullOrEmpty(def.displayName) || IsBoss(def) != boss)
                    continue;
                if (!byName.TryGetValue(def.displayName, out var entry))
                {
                    entry = new CodexEntry { Id = id, Name = def.displayName, Note = def.note ?? "", Line = def.hint ?? "", Creature = true };
                    byName[def.displayName] = entry;
                    list.Add(entry);
                }
                // Types that share a name share an entry: the first one with a note, a hint or art fills it in.
                if (string.IsNullOrEmpty(entry.Note) && !string.IsNullOrEmpty(def.note))
                    entry.Note = def.note;
                if (string.IsNullOrEmpty(entry.Line) && !string.IsNullOrEmpty(def.hint))
                    entry.Line = def.hint;
                if (entry.Art == null)
                    entry.Art = ArtOf(def);
            }
            foreach (var entry in list)
            {
                var tally = CodexProgress.Of(entry.Name);
                entry.Met = CodexProgress.Seen(entry.Name);
                entry.TimesMet = Mathf.Max(tally.Met, entry.Met ? 1 : 0);
                entry.TimesKilled = tally.Killed;
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        static List<CodexEntry> Weapons(GameContent content)
        {
            var list = new List<CodexEntry>();
            if (content == null)
                return list;
            var ids = new List<string>(content.Weapons.Keys);
            ids.Sort((a, b) =>
            {
                // The launcher and the fists first, then the ones found on floors.
                int ra = a == "rocketLauncher" ? 0 : a == "fists" ? 1 : 2;
                int rb = b == "rocketLauncher" ? 0 : b == "fists" ? 1 : 2;
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a, b, StringComparison.Ordinal);
            });
            foreach (string id in ids)
            {
                var def = content.Weapons[id];
                if (def == null || string.IsNullOrEmpty(def.displayName))
                    continue;
                Sprite art = null;
                if (!string.IsNullOrEmpty(def.pickup) && content.Entities.TryGetValue(def.pickup, out var pickup))
                    art = ArtOf(pickup);
                list.Add(new CodexEntry
                {
                    Id = id,
                    Name = def.displayName,
                    Note = def.heroText ?? "",
                    Line = def.description ?? "",
                    Art = art,
                    Tint = def.color,
                    IsWeapon = true,
                    Met = id == "rocketLauncher" || UnlockFlags.Has("weapon:" + id),
                });
            }
            return list;
        }

        static List<CodexEntry> Characters(CodexConfig config)
        {
            var list = new List<CodexEntry>();
            if (config == null)
                return list;
            foreach (var c in config.characters)
            {
                if (c == null || string.IsNullOrEmpty(c.displayName))
                    continue;
                list.Add(new CodexEntry
                {
                    Id = c.id,
                    Name = c.displayName,
                    Note = c.note,
                    Line = c.about,
                    Art = FirstSprite(c.sheet, c.spriteNameFilter),
                    Tint = c.color,
                    Met = c.unlock == CodexCharacterUnlock.Always || (c.unlock == CodexCharacterUnlock.YoobaMet && YoobaProgress.Met),
                });
            }
            return list;
        }

        /// <summary>The first frame of an entity's first animation state, or its still sprite.</summary>
        public static Sprite ArtOf(EntityDef def)
        {
            if (def == null || def.visual == null)
                return null;
            var animator = def.visual.animator;
            if (animator != null && !animator.IsEmpty)
            {
                AnimStateDef chosen = animator.states[0];
                foreach (var state in animator.states)
                {
                    if (state != null && state.stateName == animator.defaultStateName)
                    {
                        chosen = state;
                        break;
                    }
                }
                var sprite = FirstSprite(chosen.spriteSheet, chosen.spriteNameFilter);
                if (sprite != null)
                    return sprite;
            }
            return def.visual.sprite;
        }

        static Sprite FirstSprite(Texture2D sheet, string filter)
        {
            var definition = GameDefinition.Instance;
            if (sheet == null || definition == null)
                return null;
            Sprite best = null;
            foreach (var sprite in definition.SpritesOf(sheet))
            {
                if (sprite == null || (!string.IsNullOrEmpty(filter) && !sprite.name.StartsWith(filter, StringComparison.Ordinal)))
                    continue;
                if (best == null || string.Compare(sprite.name, best.name, StringComparison.Ordinal) < 0)
                    best = sprite;
            }
            return best;
        }
    }

    /// <summary>A row on the open Codex page, as drawn (the playtest reads these).</summary>
    public sealed class CodexRowView
    {
        public CodexEntry Entry;
        public RectTransform Rect;
        public RectTransform Art;
        public bool Silhouette;
        public bool ShowsArt;
        /// <summary>The picture's tint: solid black for a silhouette.</summary>
        public Color ArtColor;
        public readonly List<string> Texts = new List<string>();
        public readonly List<RectTransform> TextRects = new List<RectTransform>();
    }

    /// <summary>The open Codex screen: its tabs, page and rows.</summary>
    public sealed class CodexView
    {
        public GameObject Root;
        public RectTransform Panel;
        public CodexTab Tab;
        public int Page;
        public int Pages;
        public int RowsPerPage;
        public int Met;
        public int Total;
        public readonly List<CodexRowView> Rows = new List<CodexRowView>();
        public readonly List<MenuButton> Tabs = new List<MenuButton>();
        public MenuButton Previous, Next, Done;
        public Action<CodexTab> SelectTab;
        public Action Close;
    }

    /// <summary>
    /// The Codex overlay (Docs/Story.md, "The Codex"), shared by the main menu and the pause menu like the settings panel:
    /// four tabs, a few rows per page with the creature's picture (a black silhouette until met), name, the hero's note, the
    /// weakness and the times met and killed. Looks come from ui/theme.json; the layout space is the menus' (1920x1080,
    /// 1080x1920 portrait) and the page size follows the screen's shape.
    /// </summary>
    public static class CodexPanel
    {
        /// <summary>The Codex on screen, or null.</summary>
        public static CodexView Current { get; private set; }

        public static bool IsOpen => Current != null && Current.Root != null;

        public static CodexView Show(MenuCanvas menu, Action onClosed)
        {
            var gd = GameDefinition.Instance;
            var config = gd != null && gd.codex != null ? gd.codex : new CodexConfig();
            var theme = menu.Theme;
            var previousBack = menu.Back;
            var content = GameContent.Current;
            CodexProgress.Flush();

            var overlay = Overlay.Create(menu, "Codex", 0.8f);
            var view = new CodexView { Root = overlay.Root };
            Current = view;

            var panel = MenuGraphics.NewRect("Panel", overlay.Inner);
            view.Panel = panel;
            MenuGraphics.Place(panel, config.panel.anchor, config.panel.offset, config.panel.size);
            MenuGraphics.Surface(panel, theme.card.surface);
            menu.AddMotion(panel.gameObject, null, theme.entrance, 1.5f);
            MenuResponsive.Track(panel, config.panel, config.panelPortrait, MenuElementKind.Container, menu.Responsive);

            menu.AddText(panel, "Title", config.title, theme.title, new Vector2(0.5f, 1f), new Vector2(0f, -62f),
                new Vector2(900f, 100f), out _, Mathf.Min(theme.title.fontSize, 84f), menu.TitleFont, null, null, -2f);

            // Tabs: one row under the title.
            var tabs = MenuGraphics.NewRect("Tabs", panel);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(40f, -250f);
            tabs.offsetMax = new Vector2(-40f, -122f);
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 12f;
            tabLayout.childAlignment = TextAnchor.UpperCenter;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;

            var tabStyle = theme.smallButton.Clone();
            tabStyle.size = new Vector2(210f, 70f);
            string[] tabNames = { config.tabEnemies, config.tabBosses, config.tabWeapons, config.tabCharacters };

            // The rows between the tabs and the footer.
            var rows = MenuGraphics.NewRect("Rows", panel);
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.offsetMin = new Vector2(40f, 176f);
            rows.offsetMax = new Vector2(-40f, -256f);
            var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 8f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = rowsLayout.childForceExpandHeight = true;

            // Footer: previous, page, next, done.
            var footer = MenuGraphics.NewRect("Footer", panel);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = new Vector2(40f, 30f);
            footer.offsetMax = new Vector2(-40f, 160f);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            var pager = theme.smallButton.Clone();
            pager.size = new Vector2(110f, 70f);
            var done = theme.button.Clone();
            done.size = new Vector2(260f, 80f);

            TextMeshProUGUI pageText = null;
            List<CodexEntry> entries = null;
            bool lastPortrait = MenuScreen.Portrait;
            var tab = CodexTab.Enemies;

            void Refresh()
            {
                bool portrait = MenuScreen.Portrait;
                lastPortrait = portrait;
                view.RowsPerPage = Mathf.Max(1, portrait ? config.rowsPortrait : config.rowsLandscape);
                entries = CodexBook.Entries(tab, content, config);
                view.Total = entries.Count;
                view.Met = 0;
                foreach (var e in entries)
                    if (e.Met)
                        view.Met++;
                view.Pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)view.RowsPerPage));
                view.Page = Mathf.Clamp(view.Page, 0, view.Pages - 1);
                view.Tab = tab;

                for (int i = rows.childCount - 1; i >= 0; i--)
                {
                    var old = rows.GetChild(i).gameObject;
                    old.SetActive(false);
                    UnityEngine.Object.Destroy(old);
                }
                view.Rows.Clear();

                int first = view.Page * view.RowsPerPage;
                for (int i = 0; i < view.RowsPerPage; i++)
                {
                    int index = first + i;
                    if (index < entries.Count)
                        view.Rows.Add(BuildRow(menu, rows, entries[index], config, portrait));
                    else if (entries.Count == 0 && i == 0)
                        BuildEmpty(menu, rows, config.empty);
                    else
                        MenuGraphics.NewRect("Spare", rows).gameObject.AddComponent<LayoutElement>();
                }

                MenuGraphics.SetText(pageText, string.Format(CultureInfo.InvariantCulture, config.pageFormat, view.Page + 1, view.Pages, view.Met, view.Total));
                for (int i = 0; i < view.Tabs.Count; i++)
                {
                    var label = view.Tabs[i].Label;
                    bool selected = i == (int)tab;
                    label.color = selected ? theme.accent : Color.white;
                    label.fontStyle = selected ? label.fontStyle | FontStyles.Underline : label.fontStyle & ~FontStyles.Underline;
                }
                if (view.Previous != null) view.Previous.interactable = view.Page > 0;
                if (view.Next != null) view.Next.interactable = view.Page < view.Pages - 1;
            }

            void Select(CodexTab picked)
            {
                tab = picked;
                view.Page = 0;
                Refresh();
            }
            view.SelectTab = Select;

            for (int i = 0; i < tabNames.Length; i++)
            {
                var picked = (CodexTab)i;
                view.Tabs.Add(menu.AddButton(tabs, tabNames[i], tabStyle, () => Select(picked)));
            }

            view.Previous = menu.AddButton(footer, config.previous, pager, () => { view.Page--; Refresh(); });
            var pageSlot = MenuGraphics.NewRect("Page", footer);
            var pageElement = pageSlot.gameObject.AddComponent<LayoutElement>();
            pageElement.preferredWidth = pageElement.minWidth = 420f;
            pageElement.preferredHeight = 70f;
            pageText = MenuGraphics.Text(pageSlot, "Text", "", theme.heading, menu.Font, 32f);
            view.Next = menu.AddButton(footer, config.next, pager, () => { view.Page++; Refresh(); });

            void Close()
            {
                if (overlay.Root == null)
                    return;
                Sfx.Play(Sfx.Events.uiBack);
                menu.Back = previousBack;
                if (Current == view)
                    Current = null;
                UnityEngine.Object.Destroy(overlay.Root);
                onClosed?.Invoke();
            }
            view.Close = Close;
            view.Done = menu.AddButton(footer, (theme.texts ?? new MenuTexts()).done, done, Close);
            menu.Back = Close;

            // The page size follows the screen's shape (a phone turned round), so rebuild when it changes.
            overlay.Root.AddComponent<CodexShapeWatch>().Init(() => lastPortrait != MenuScreen.Portrait, Refresh);

            Refresh();
            return view;
        }

        static void BuildEmpty(MenuCanvas menu, RectTransform rows, string text)
        {
            var slot = MenuGraphics.NewRect("Empty", rows);
            var t = MenuGraphics.Text(slot, "Text", text, menu.Theme.body, menu.Font, 32f);
            t.color = menu.Theme.muted;
        }

        static CodexRowView BuildRow(MenuCanvas menu, RectTransform rows, CodexEntry entry, CodexConfig config, bool portrait)
        {
            var theme = menu.Theme;
            var row = MenuGraphics.NewRect(entry.Met ? "Row" : "RowUnknown", rows);
            var view = new CodexRowView { Entry = entry, Rect = row, Silhouette = !entry.Met };
            MenuGraphics.Surface(row, theme.card.surface, entry.Met ? new Color(0.13f, 0.08f, 0.18f, 0.9f) : new Color(0.05f, 0.03f, 0.07f, 0.8f));
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(16, 20, 10, 10);
            line.spacing = 20f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = true;
            line.childForceExpandHeight = true;

            // The picture. Unmet: the same shape in solid black, or a black question mark tile. No colour, no name.
            var art = MenuGraphics.NewRect("Art", row);
            view.Art = art;
            var artElement = art.gameObject.AddComponent<LayoutElement>();
            artElement.preferredWidth = artElement.minWidth = 108f;
            artElement.flexibleWidth = 0f;
            var image = art.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (entry.Art != null)
            {
                image.sprite = entry.Art;
                image.preserveAspect = true;
                image.color = entry.Met ? Color.white : Color.black;
                view.ShowsArt = true;
            }
            else
            {
                MenuGraphics.Apply(image, MenuShape.Rounded, 18f);
                image.color = entry.Met ? entry.Tint : Color.black;
                string glyph = entry.Met ? InitialOf(entry.Name) : "?";
                var g = MenuGraphics.Text(art, "Glyph", glyph, theme.heading, menu.Font, 56f);
                g.color = entry.Met ? Color.white : theme.muted;
            }

            view.ArtColor = image.color;

            // The words.
            var column = MenuGraphics.NewRect("Words", row);
            var columnElement = column.gameObject.AddComponent<LayoutElement>();
            columnElement.flexibleWidth = 1f;
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 0f;
            stack.childAlignment = TextAnchor.MiddleLeft;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = true;

            float nameSize = portrait ? 40f : 36f;
            float bodySize = portrait ? 30f : 27f;

            // Name on the left, the tally on the right.
            var head = MenuGraphics.NewRect("Head", column);
            head.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1.3f;
            var headLine = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            headLine.spacing = 12f;
            headLine.childAlignment = TextAnchor.MiddleLeft;
            headLine.childControlWidth = headLine.childControlHeight = true;
            headLine.childForceExpandWidth = headLine.childForceExpandHeight = true;
            var name = Cell(menu, view, head, "Name", entry.Met ? entry.Name : config.unknownName, theme.heading, nameSize, TextAlignmentOptions.MidlineLeft, 1f);
            if (!entry.Met)
                name.color = theme.muted;
            if (entry.Met)
            {
                string tally = entry.IsWeapon
                    ? config.weaponFound
                    : string.Format(CultureInfo.InvariantCulture, config.tallyFormat, entry.TimesMet, entry.TimesKilled);
                var t = Cell(menu, view, head, "Tally", tally, theme.body, bodySize, TextAlignmentOptions.MidlineRight, 0f, portrait ? 330f : 420f);
                t.color = theme.accent;
            }

            if (entry.Met)
            {
                Cell(menu, view, column, "Note", entry.Note, theme.body, bodySize, TextAlignmentOptions.MidlineLeft, 1f);
                string second = string.IsNullOrEmpty(entry.Line) ? "" : entry.Creature ? config.weaknessPrefix + entry.Line : entry.Line;
                var w = Cell(menu, view, column, "Line", second, theme.body, bodySize, TextAlignmentOptions.MidlineLeft, 1f);
                w.color = theme.muted;
            }
            else
            {
                var u = Cell(menu, view, column, "Unknown", config.unknownLine, theme.body, bodySize, TextAlignmentOptions.MidlineLeft, 1f);
                u.color = theme.muted;
            }
            return view;
        }

        static string InitialOf(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        static TextMeshProUGUI Cell(MenuCanvas menu, CodexRowView view, RectTransform parent, string name, string value, MenuTextStyle style, float size,
            TextAlignmentOptions alignment, float flexibleWidth, float preferredWidth = 0f)
        {
            var slot = MenuGraphics.NewRect(name, parent);
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 1f;
            if (preferredWidth > 0f)
                element.preferredWidth = element.minWidth = preferredWidth;
            var text = MenuGraphics.Text(slot, "Text", value, style, menu.Font, size);
            text.alignment = alignment;
            view.Texts.Add(value);
            view.TextRects.Add(slot);
            return text;
        }
    }

    /// <summary>Calls back when the screen turns between landscape and portrait, so the Codex can re-page itself.</summary>
    public sealed class CodexShapeWatch : MonoBehaviour
    {
        Func<bool> changed;
        Action rebuild;

        public void Init(Func<bool> changed, Action rebuild)
        {
            this.changed = changed;
            this.rebuild = rebuild;
        }

        void Update()
        {
            if (changed != null && changed())
                rebuild();
        }
    }
}
