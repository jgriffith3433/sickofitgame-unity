using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using TMPro;
using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The deepest climb ever made, kept for good (PlayerPrefs, not cleared by New Game or a run's end), and the line the death
    /// screen always shows: "BEST: FLOOR n" over what comes next (Docs/Story.md, "Every death tells you what is next").
    /// Presentation and a save only: it reads the run's stop and the goal pick, changes no simulation state.
    /// </summary>
    public static class BestFloorRecord
    {
        const string Key = "Best_Floor";

        /// <summary>The highest floor number's lowest value reached so far (floor 1000 is the bottom, smaller is higher); 0 = none yet.</summary>
        public static int Best => PlayerPrefs.GetInt(Key, 0);

        /// <summary>Records a floor reached (the climb counts down). True when it is a new best.</summary>
        public static bool Note(int floor)
        {
            if (floor <= 0)
                return false;
            int best = Best;
            if (best > 0 && floor >= best)
                return false;
            PlayerPrefs.SetInt(Key, floor);
            PlayerPrefs.Save();
            return best > 0;
        }

        /// <summary>The two lines: the record, and the next unlock (the next good, Waystone or the top).</summary>
        public static string Describe(int floor, bool newBest)
        {
            int best = Best;
            string record = best > 0 ? $"BEST: FLOOR {best}" : "BEST: FLOOR " + floor;
            if (newBest)
                record += "  (NEW!)";
            var goal = NextGoal.Pick(floor);
            string next = goal != null ? goal.Format(goal.To) : "";
            return string.IsNullOrEmpty(next) ? record : record + "\nNEXT: " + next;
        }

        /// <summary>Builds the record under the quip (landscape and portrait placements), always shown.</summary>
        public static TextMeshProUGUI Build(MenuCanvas menu, int floor, bool newBest)
        {
            var theme = menu.Theme;
            var text = menu.AddText(menu.Root, "BestFloor", Describe(floor, newBest), theme.caption,
                new Vector2(0.5f, 0.665f), Vector2.zero, new Vector2(1500f, 110f), out _, 40f, menu.Font, null, null, 0f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 40f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            MenuResponsive.Track((RectTransform)text.transform.parent,
                MenuPlacement.At(0.5f, 0.665f, 0f, 0f, 1500f, 110f), MenuPlacement.At(0.5f, 1f, 0f, -440f, 1000f, 110f),
                MenuElementKind.Text, menu.Responsive);
            Debug.Log($"[Death] best floor record: {text.text.Replace('\n', '|')}");
            return text;
        }
    }
}
