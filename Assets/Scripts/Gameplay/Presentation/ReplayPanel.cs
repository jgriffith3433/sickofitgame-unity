using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    public enum ReplayPanelKind { Endless, Daily, NewGamePlus }

    /// <summary>The open replay card: what it says and its buttons (the playtest reads these).</summary>
    public sealed class ReplayView
    {
        public GameObject Root;
        public RectTransform Panel;
        public ReplayPanelKind Kind;
        public string Title = "", Blurb = "", Body = "";
        public MenuButton Play, Back;
        public Action Close;
    }

    /// <summary>
    /// The card the main menu's Endless Floor, Daily Challenge and New Game+ buttons open (Docs/Story.md, "Again"): the mode's one-line pitch,
    /// what is saved (the best floor, today's twist and local table, the cycle) and Play / Back. Looks come from ui/theme.json, words from
    /// game.json replay.labels; shared with the playtest like the Codex.
    /// </summary>
    public static class ReplayPanel
    {
        /// <summary>The card on screen, or null.</summary>
        public static ReplayView Current { get; private set; }

        public static bool IsOpen => Current != null && Current.Root != null;

        /// <summary>The words of the card for a mode, without building any UI (the playtest and the tests read this too).</summary>
        public static void Words(ReplayPanelKind kind, out string title, out string blurb, out string body, out bool canPlay)
        {
            var gd = GameDefinition.Instance;
            var config = gd != null && gd.replay != null ? gd.replay : new ReplayConfig();
            var labels = config.labels ?? new ReplayLabels();
            var ci = CultureInfo.InvariantCulture;
            canPlay = true;
            switch (kind)
            {
                case ReplayPanelKind.Endless:
                {
                    title = labels.endless;
                    blurb = labels.endlessBlurb;
                    int best = ReplayModes.EndlessBest;
                    body = best > 0 ? string.Format(ci, labels.endlessBest, best) : labels.endlessNoBest;
                    break;
                }
                case ReplayPanelKind.Daily:
                {
                    title = labels.daily;
                    blurb = labels.dailyBlurb;
                    string day = ReplayModes.TodayKey;
                    var modifier = ReplayModes.ModifierFor(day);
                    var sb = new StringBuilder();
                    sb.Append(string.Format(ci, labels.dailyHeader, ReplayModes.PrettyDate(day), modifier != null ? modifier.name : "")).Append('\n');
                    var table = ReplayModes.DailyTable(day);
                    if (table.Count == 0)
                    {
                        sb.Append(labels.dailyEmpty);
                    }
                    else
                    {
                        for (int i = 0; i < table.Count && i < 5; i++)
                        {
                            var row = table[i];
                            sb.Append(string.Format(ci, labels.dailyRow, i + 1, row.Score.ToString("N0", ci), row.Floors, row.Cleared ? labels.cleared : labels.fell));
                            if (i < table.Count - 1 && i < 4)
                                sb.Append('\n');
                        }
                    }
                    body = sb.ToString();
                    break;
                }
                default:
                {
                    title = ReplayModes.Cycle > 0 ? string.Format(ci, labels.ngPlusLabel, ReplayModes.Cycle + 1) : labels.newGamePlus;
                    canPlay = ReplayModes.NewGamePlusOpen;
                    blurb = canPlay ? string.Format(ci, labels.ngPlusBlurb, ReplayModes.NextCycle + 1) : labels.ngPlusLocked;
                    body = ReplayModes.Cycle > 0 ? string.Format(ci, labels.ngPlusLabel, ReplayModes.Cycle + 1).ToUpperInvariant() : "";
                    break;
                }
            }
        }

        public static ReplayView Show(MenuCanvas menu, ReplayPanelKind kind, Action onPlay, Action onClosed)
        {
            var gd = GameDefinition.Instance;
            var labels = gd != null && gd.replay != null && gd.replay.labels != null ? gd.replay.labels : new ReplayLabels();
            var theme = menu.Theme;
            var previousBack = menu.Back;
            Words(kind, out string title, out string blurb, out string body, out bool canPlay);

            var overlay = Overlay.Create(menu, "Replay", 0.8f);
            var view = new ReplayView { Root = overlay.Root, Kind = kind, Title = title, Blurb = blurb, Body = body };
            Current = view;

            var panel = MenuGraphics.NewRect("Panel", overlay.Inner);
            view.Panel = panel;
            MenuGraphics.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 780f));
            MenuGraphics.Surface(panel, theme.card.surface);
            menu.AddMotion(panel.gameObject, null, theme.entrance, 1.5f);
            MenuResponsive.Track(panel, MenuPlacement.At(0.5f, 0.5f, 0f, 0f, 1100f, 780f), MenuPlacement.At(0.5f, 0.5f, 0f, 0f, 940f, 1000f),
                MenuElementKind.Container, menu.Responsive);

            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 40, 36);
            column.spacing = 18f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            AddLine(panel, "Title", title, theme.title, menu, 84f, 120f, true);
            AddLine(panel, "Blurb", blurb, theme.body, menu, 34f, 150f, false);
            var bodyText = AddLine(panel, "Body", body, theme.heading, menu, 38f, 300f, false);
            bodyText.color = theme.accent;

            var footer = MenuGraphics.NewRect("Footer", panel);
            footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 24f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            var buttonStyle = theme.button.Clone();
            buttonStyle.size = new Vector2(380f, 90f);

            void Close()
            {
                if (overlay.Root == null)
                    return;
                Sfx.Play(Sfx.Events.uiBack);
                menu.Back = previousBack;
                if (Current == view)
                    Current = null;
                Overlay.Close(overlay.Root);
                onClosed?.Invoke();
            }
            view.Close = Close;

            string playLabel = kind == ReplayPanelKind.NewGamePlus ? string.Format(CultureInfo.InvariantCulture, labels.ngPlusPlay, ReplayModes.NextCycle + 1) : labels.play;
            view.Play = menu.AddButton(footer, playLabel, buttonStyle, () =>
            {
                if (overlay.Root == null)
                    return;
                menu.Back = previousBack;
                if (Current == view)
                    Current = null;
                Overlay.Close(overlay.Root);
                onPlay?.Invoke();
            });
            view.Play.interactable = canPlay;
            view.Back = menu.AddButton(footer, labels.back, buttonStyle, Close);
            menu.Back = Close;
            return view;
        }

        static TextMeshProUGUI AddLine(RectTransform parent, string name, string value, MenuTextStyle style, MenuCanvas menu, float size, float height, bool upper)
        {
            var slot = MenuGraphics.NewRect(name, parent);
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = height;
            var text = MenuGraphics.Text(slot, "Text", upper ? (value ?? "").ToUpperInvariant() : value ?? "", style, menu.Font, size);
            return text;
        }
    }
}
