using System;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SickOfIt.Core.Progression
{
    public enum GoalKind { Ware, Waystone, Daily, Summit }

    /// <summary>What the next goal card shows: a label that counts from <see cref="From"/> to <see cref="To"/> out of <see cref="Max"/>.</summary>
    public sealed class GoalInfo
    {
        public GoalKind Kind;
        public string Id = "";
        public int From, To, Max = 1;
        public Func<int, string> Format;
        /// <summary>The label with the value at the end of the count (what the player is left reading).</summary>
        public string FinalText => Format != null ? Format(To) : "";
    }

    /// <summary>
    /// "What am I close to": one card on the death scene and the results screen (Docs/Story.md, "Every death tells you what is next").
    /// Chosen in order: the cheapest Yooba good not yet bought (a bar of diamonds that fills with this attempt's), else the next
    /// Waystone (floors to go), else today's Daily if unplayed, else the climb's top. Presentation only: it reads saves, changes nothing.
    /// </summary>
    public static class NextGoal
    {
        /// <summary>Diamonds held when the level began (set by the HUD via <see cref="NextGoalBridge"/>), so the bar can show what this attempt added. -1 = unknown.</summary>
        public static int RunStartPoints
        {
            get => NextGoalBridge.RunStartPoints;
            set => NextGoalBridge.RunStartPoints = value;
        }

        /// <summary>What hit the hero last on a death that stood (an entity id or a hazard name) and the boss he was fighting, set by PlayAnalytics via <see cref="NextGoalBridge"/>.</summary>
        public static string LastCause
        {
            get => NextGoalBridge.LastCause;
            set => NextGoalBridge.LastCause = value;
        }

        public static string LastBoss
        {
            get => NextGoalBridge.LastBoss;
            set => NextGoalBridge.LastBoss = value;
        }

        public static int DiamondsThisRun()
        {
            var roguelite = RogueliteManager.Instance;
            if (roguelite == null || RunStartPoints < 0)
                return 0;
            return Mathf.Max(0, roguelite.GetPoints() - RunStartPoints);
        }

        /// <summary>The goal to show now. <paramref name="floor"/> is the floor he is on (0 = not on a floor stage).</summary>
        public static GoalInfo Pick(int floor)
        {
            return CheapestWare() ?? NextWaystone(floor) ?? DailyUnplayed() ?? Summit(floor);
        }

        static GoalInfo CheapestWare()
        {
            var roguelite = RogueliteManager.Instance;
            if (roguelite == null)
                return null;
            UpgradeDefinition best = null;
            int bestCost = int.MaxValue;
            foreach (var upgrade in roguelite.VisibleCatalog())
            {
                if (roguelite.GetUpgradeLevel(upgrade.id) > 0)
                    continue;
                int cost = roguelite.GetNextCost(upgrade);
                if (cost > 0 && cost < bestCost)
                {
                    best = upgrade;
                    bestCost = cost;
                }
            }
            if (best == null)
                return null;
            int points = roguelite.GetPoints();
            int gained = DiamondsThisRun();
            string title = best.title;
            string effect = FirstSentence(best.description, 34);
            string tail = string.IsNullOrEmpty(effect) ? title : title + ", " + effect;
            int cost2 = bestCost;
            return new GoalInfo
            {
                Kind = GoalKind.Ware,
                Id = best.id,
                From = Mathf.Min(Mathf.Max(0, points - gained), cost2),
                To = Mathf.Min(points, cost2),
                Max = cost2,
                Format = v => v >= cost2 ? $"{cost2} / {cost2} diamonds: {tail}. Go buy it!" : $"{v} / {cost2} diamonds: {tail}",
            };
        }

        static GoalInfo NextWaystone(int floor)
        {
            var list = RunFlow.Waystones();
            if (list == null || list.Count == 0)
                return null;
            int highest = WaystoneProgress.Highest;
            int basis = highest > 0 ? highest : list[0];
            int next = 0;
            foreach (int w in list)
                if (w < basis) { next = w; break; }
            if (next <= 0)
                return null;
            int span = Mathf.Max(1, basis - next);
            int here = floor > 0 ? floor : basis;
            int climbed = Mathf.Clamp(basis - here, 0, span);
            return new GoalInfo
            {
                Kind = GoalKind.Waystone,
                Id = "waystone" + next,
                From = 0,
                To = climbed,
                Max = span,
                Format = v =>
                {
                    int left = span - v;
                    return $"Floor {next}: {left} {(left == 1 ? "floor" : "floors")} to the next Waystone";
                },
            };
        }

        static GoalInfo DailyUnplayed()
        {
            var gd = GameDefinition.Instance;
            if (gd == null || gd.replay == null || !gd.replay.enabled)
                return null;
            if (ReplayModes.DailyTable(ReplayModes.TodayKey).Count > 0)
                return null;
            return new GoalInfo { Kind = GoalKind.Daily, Id = "daily", From = 0, To = 0, Max = 1, Format = v => "Today's Daily Challenge: not played yet" };
        }

        static GoalInfo Summit(int floor)
        {
            var shaft = GameDefinition.Instance != null ? GameDefinition.Instance.shaft : null;
            int bottom = shaft != null ? Mathf.Max(2, shaft.bottomFloor) : 1000;
            int top = shaft != null ? Mathf.Max(1, shaft.topFloor) : 1;
            int span = Mathf.Max(1, bottom - top);
            int here = floor > 0 ? floor : bottom;
            int climbed = Mathf.Clamp(bottom - here, 0, span);
            return new GoalInfo
            {
                Kind = GoalKind.Summit,
                Id = "summit",
                From = 0,
                To = climbed,
                Max = span,
                Format = v => $"Floor {top}: {span - v} floors to the top",
            };
        }

        /// <summary>The first sentence of a good's description (its effect), cut to a short line.</summary>
        public static string FirstSentence(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int stop = text.IndexOfAny(new[] { '.', '!', '?' });
            string s = (stop > 0 ? text.Substring(0, stop) : text).Trim();
            if (s.Length > max)
            {
                s = s.Substring(0, max);
                int space = s.LastIndexOf(' ');
                if (space > 8)
                    s = s.Substring(0, space);
                s = s.TrimEnd(',', ';', ':') + "…";
            }
            return s;
        }
    }

    /// <summary>The card: a panel with the goal's words over a bar that fills as the number counts up (NumberTicker) with a soft tick.</summary>
    public sealed class NextGoalCard : MonoBehaviour
    {
        /// <summary>The card on screen now (for the playtest), or null.</summary>
        public static NextGoalCard Current { get; private set; }

        public GoalInfo Goal { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public RectTransform Panel { get; private set; }
        public RectTransform Caption { get; private set; }
        public TextMeshProUGUI CaptionText { get; private set; }
        /// <summary>The bar's fill, 0-1, as drawn.</summary>
        public float FillFraction { get; private set; }
        public int Ticks { get; private set; }
        public bool Counting => ticker != null && ticker.Counting;
        public int Shown => ticker != null ? ticker.Shown : 0;

        RectTransform fill;
        NumberTicker ticker;
        int lastShown = int.MinValue;
        float lastTickAt = -1f;

        /// <summary>Builds the card (and an empty caption row under it) in <paramref name="parent"/>.</summary>
        public static NextGoalCard Build(MenuCanvas menu, Transform parent, GoalInfo goal, MenuPlacement landscape, MenuPlacement portrait,
            MenuPlacement captionLandscape, MenuPlacement captionPortrait, float countDelay)
        {
            var theme = menu.Theme;
            var slot = MenuGraphics.NewRect("NextGoal", parent);
            MenuGraphics.Place(slot, landscape.anchor, landscape.offset, landscape.size);
            var back = slot.gameObject.AddComponent<Image>();
            back.color = new Color(0.07f, 0.03f, 0.04f, 0.82f);
            back.raycastTarget = false;
            var card = slot.gameObject.AddComponent<NextGoalCard>();
            card.Goal = goal;
            card.Panel = slot;
            MenuResponsive.Track(slot, landscape, portrait, MenuElementKind.Panel, menu.Responsive);

            var labelSlot = MenuGraphics.NewRect("Label", slot);
            labelSlot.anchorMin = new Vector2(0.04f, 0.42f);
            labelSlot.anchorMax = new Vector2(0.96f, 0.94f);
            labelSlot.offsetMin = labelSlot.offsetMax = Vector2.zero;
            card.Label = MenuGraphics.Text(labelSlot, "Text", goal.Format(goal.From), theme.caption, menu.Font, 40f);
            card.Label.enableAutoSizing = true;
            card.Label.fontSizeMin = 14f;
            card.Label.fontSizeMax = 40f;
            card.Label.textWrappingMode = TextWrappingModes.NoWrap;
            card.Label.alignment = TextAlignmentOptions.Center;

            var track = MenuGraphics.NewRect("Track", slot);
            track.anchorMin = new Vector2(0.04f, 0.12f);
            track.anchorMax = new Vector2(0.96f, 0.34f);
            track.offsetMin = track.offsetMax = Vector2.zero;
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.7f);
            trackImage.raycastTarget = false;
            card.fill = MenuGraphics.NewRect("Fill", track);
            card.fill.anchorMin = Vector2.zero;
            card.fill.anchorMax = new Vector2(0f, 1f);
            card.fill.offsetMin = card.fill.offsetMax = Vector2.zero;
            var fillImage = card.fill.gameObject.AddComponent<Image>();
            fillImage.color = theme.accent;
            fillImage.raycastTarget = false;

            if (captionLandscape != null)
            {
                // The caption row for the death reaction: one short line, empty until the voice plays.
                var captionSlot = MenuGraphics.NewRect("Reaction", parent);
                MenuGraphics.Place(captionSlot, captionLandscape.anchor, captionLandscape.offset, captionLandscape.size);
                card.CaptionText = MenuGraphics.Text(captionSlot, "Text", "", theme.subtitle, menu.Font, 44f);
                card.CaptionText.enableAutoSizing = true;
                card.CaptionText.fontSizeMin = 16f;
                card.CaptionText.textWrappingMode = TextWrappingModes.NoWrap;
                card.Caption = captionSlot;
                MenuResponsive.Track(captionSlot, captionLandscape, captionPortrait, MenuElementKind.Text, menu.Responsive);
            }

            card.ticker = NumberTicker.On(card.Label, goal.Format);
            card.ticker.Snap(goal.From);
            card.ticker.CountTo(goal.To, 0.9f, countDelay);
            Current = card;
            Debug.Log($"[NextGoal] {goal.Kind}: \"{goal.FinalText}\" (counts {goal.From} to {goal.To} of {goal.Max})");
            return card;
        }

        /// <summary>
        /// The death scene's card and the reaction under it: the bar counts up the diamonds this attempt earned (after the title lands),
        /// and a voiced line about how he died is said a moment after the announcer's gloat. Landscape: above the buttons; portrait: under them.
        /// </summary>
        public static NextGoalCard BuildForDeath(MenuCanvas menu, MonoBehaviour owner, int floor, int totalDeaths)
        {
            var goal = NextGoal.Pick(floor);
            // An empty bar (nothing gathered this attempt) is clutter on the death screen: show the card only with real data in it.
            if (goal != null && goal.To <= 0)
                goal = null;
            if (goal == null)
            {
                string quiet = DeathReaction.Pick(NextGoal.LastCause, NextGoal.LastBoss, totalDeaths);
                Debug.Log("[NextGoal] none on the death screen (empty bar hidden)");
                if (quiet != null && owner != null)
                    owner.StartCoroutine(DeathReaction.Say(null, quiet, 2.4f));
                return null;
            }
            var card = Build(menu, menu.Root, goal,
                MenuPlacement.At(0.5f, 0.3f, 0f, 0f, 1100f, 170f), MenuPlacement.At(0.5f, 1f, 0f, -1120f, 980f, 170f),
                MenuPlacement.At(0.5f, 0.6f, 0f, 0f, 1500f, 80f), MenuPlacement.At(0.5f, 1f, 0f, -1300f, 1000f, 100f), 0.6f);
            string line = DeathReaction.Pick(NextGoal.LastCause, NextGoal.LastBoss, totalDeaths);
            Debug.Log($"[Death] cause \"{NextGoal.LastCause}\" boss \"{NextGoal.LastBoss}\" group {DeathReaction.GroupOf(NextGoal.LastCause, NextGoal.LastBoss)} deaths {totalDeaths}: reaction {(line ?? "none (no clip made yet)")}");
            if (line != null && owner != null)
                owner.StartCoroutine(DeathReaction.Say(card, line, 2.4f));
            return card;
        }

        /// <summary>
        /// The results screen's next-goal card (same pick as death). Called from GameplayHud through
        /// <see cref="NextGoalBridge.BuildResultsCard"/> because Gameplay cannot name this assembly.
        /// </summary>
        public static NextGoalCard BuildForResults(MenuCanvas menu, Transform parent, int floor)
        {
            var goal = NextGoal.Pick(floor);
            if (goal == null || menu == null || parent == null)
                return null;
            return Build(menu, parent, goal, MenuPlacement.At(0.2f, 0.13f, 0f, 0f, 540f, 140f),
                MenuPlacement.At(0.5f, 0.268f, 0f, 0f, 900f, 130f), null, null, 1.4f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterResultsCard() =>
            NextGoalBridge.BuildResultsCard = (menu, parent, floor) => BuildForResults(menu, parent, floor);

        void Update()
        {
            if (ticker == null || Goal == null)
                return;
            int shown = ticker.Shown;
            FillFraction = Mathf.Clamp01(shown / (float)Mathf.Max(1, Goal.Max));
            if (fill != null)
                fill.anchorMax = new Vector2(FillFraction, 1f);
            if (shown != lastShown)
            {
                // A soft tick while the count moves, not on the first frame (nothing counted yet).
                if (lastShown != int.MinValue && Time.unscaledTime - lastTickAt >= 0.06f)
                {
                    Sfx.Play("ui_hover", 0.7f, 1f + 0.4f * FillFraction);
                    lastTickAt = Time.unscaledTime;
                    Ticks++;
                }
                lastShown = shown;
            }
        }

        /// <summary>Puts the one short line under the card (the reaction's subtitle).</summary>
        public void SetCaption(string text)
        {
            if (CaptionText != null)
                CaptionText.text = text ?? "";
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }
    }

    /// <summary>
    /// The voiced reaction to how he just died (Hades' way): keyed on the cause (an entity id or hazard from the last damage event, or the
    /// boss), the death count, never the same line within 5 minutes. Lines are audio.json voices; one with no clip made yet is skipped
    /// (the wanted ones are listed in Tools/Audio/pending_clips.json), never shown as text alone.
    /// </summary>
    public static class DeathReaction
    {
        public const float RepeatSeconds = 300f;
        /// <summary>Yooba's counted lines (existing): said when the total of deaths reaches the number.</summary>
        static readonly KeyValuePair<int, string>[] Milestones =
        {
            new KeyValuePair<int, string>(1, "vo_yooba_deaths_1"),
            new KeyValuePair<int, string>(5, "vo_yooba_deaths_5"),
            new KeyValuePair<int, string>(15, "vo_yooba_deaths_15"),
            new KeyValuePair<int, string>(40, "vo_yooba_deaths_40"),
        };

        /// <summary>Cause group: substrings of the cause (lower case), and the announcer lines for it.</summary>
        sealed class Group
        {
            public string Name;
            public string[] Tokens;
            public string[] Lines;
        }

        static readonly Group[] Groups =
        {
            new Group { Name = "lava", Tokens = new[] { "lava", "pit", "magma", "geyser", "burn", "fire" }, Lines = new[] { "vo_dr_lava_a", "vo_dr_lava_b" } },
            new Group { Name = "bite", Tokens = new[] { "hound", "whelp", "brute", "beetle", "tortoise", "chaser", "charger", "demon", "bomber", "wraith" }, Lines = new[] { "vo_dr_bite_a", "vo_dr_bite_b" } },
            new Group { Name = "bolt", Tokens = new[] { "imp", "bolt", "lens", "ember", "laser", "sniper", "lobber", "deadeye", "shot", "spark" }, Lines = new[] { "vo_dr_bolt_a", "vo_dr_bolt_b" } },
        };
        static readonly string[] BossLines = { "vo_dr_boss_a", "vo_dr_boss_b" };
        static readonly string[] OtherLines = { "vo_dr_other_a", "vo_dr_other_b" };

        static readonly Dictionary<string, float> saidAt = new Dictionary<string, float>();

        /// <summary>Which group a cause falls in ("boss", "lava", "bite", "bolt" or "other").</summary>
        public static string GroupOf(string cause, string boss)
        {
            if (!string.IsNullOrEmpty(boss))
                return "boss";
            string c = (cause ?? "").ToLowerInvariant();
            if (c.Length > 0)
                foreach (var g in Groups)
                    foreach (string token in g.Tokens)
                        if (c.Contains(token))
                            return g.Name;
            return "other";
        }

        static string[] LinesOf(string group)
        {
            if (group == "boss") return BossLines;
            foreach (var g in Groups)
                if (g.Name == group)
                    return g.Lines;
            return OtherLines;
        }

        static bool Fresh(string id) => !saidAt.TryGetValue(id, out float at) || Time.realtimeSinceStartup - at >= RepeatSeconds;

        /// <summary>
        /// The line to say for this death (not said in the last 5 minutes, with a clip), or null. A death count that is one of Yooba's
        /// counted numbers comes first; then the cause's lines; then the general ones.
        /// </summary>
        public static string Pick(string cause, string boss, int totalDeaths, bool requireClip = true)
        {
            foreach (var m in Milestones)
                if (m.Key == totalDeaths && Usable(m.Value, requireClip))
                    return m.Value;
            string group = GroupOf(cause, boss);
            foreach (string id in LinesOf(group))
                if (Usable(id, requireClip))
                    return id;
            if (group != "other")
                foreach (string id in OtherLines)
                    if (Usable(id, requireClip))
                        return id;
            return null;
        }

        static bool Usable(string id, bool requireClip) => Fresh(id) && (!requireClip || Sfx.Has(id));

        /// <summary>Says it (voice and one short subtitle under the card) after <paramref name="delay"/> real seconds.</summary>
        public static IEnumerator Say(NextGoalCard card, string id, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            saidAt[id] = Time.realtimeSinceStartup;
            Sfx.Play(id);
            LastSaid = id;
            string text = ShortLine(Sfx.SpokenText(id));
            if (card != null)
                card.SetCaption(text);
            Debug.Log($"[Death] reaction {id}: \"{text}\"");
        }

        /// <summary>The line the last death said (for the playtest).</summary>
        public static string LastSaid { get; private set; } = "";

        public static void ForgetAll()
        {
            saidAt.Clear();
            LastSaid = "";
        }

        /// <summary>One short line of a long one: its first sentence, cut to about 60 characters.</summary>
        public static string ShortLine(string text) => NextGoal.FirstSentence(text, 60);
    }
}
