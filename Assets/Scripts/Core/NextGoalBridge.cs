using System;
using UnityEngine;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Core
{
    /// <summary>
    /// Shared next-goal state and hooks. Gameplay Presentation cannot name <c>SickOfIt.Core.Progression</c>
    /// (Progression references Gameplay), so the HUD and analytics write here; Progression reads them and
    /// registers <see cref="BuildResultsCard"/>.
    /// </summary>
    public static class NextGoalBridge
    {
        /// <summary>Diamonds held when the level began, so the bar can show what this attempt added. -1 = unknown.</summary>
        public static int RunStartPoints = -1;

        /// <summary>What hit the hero last on a death that stood (entity id or hazard) and the boss he was fighting.</summary>
        public static string LastCause = "";
        public static string LastBoss = "";

        /// <summary>
        /// Builds the next-goal card on the results screen. Set by Progression's NextGoalCard
        /// (<c>RuntimeInitializeOnLoadMethod</c>); args are menu, parent, floor (0 = not on a floor stage).
        /// </summary>
        public static Action<MenuCanvas, Transform, int> BuildResultsCard;

        /// <summary>
        /// Builds Yooba's portrait on the results screen (menu, parent, landscape place, portrait place) and returns what makes him
        /// speak a verdict: (sound id, subtitle) → his voice with his talking loop. Set by Progression's ResultsYooba.
        /// </summary>
        public static Func<MenuCanvas, Transform, MenuPlacement, MenuPlacement, Action<string, string>> BuildResultsYooba;
    }
}
