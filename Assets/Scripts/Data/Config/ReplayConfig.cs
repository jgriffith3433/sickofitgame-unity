using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>Hell's twist of the day: how the Daily Challenge's enemies and pushes differ (Docs/Story.md, "Again").</summary>
    [Serializable]
    public class ReplayModifier
    {
        public string id = "";
        public string name = "";
        [Tooltip("The announcer's line when the day's floor starts.")]
        public string line = "";
        [Tooltip("Multiplies every enemy's health.")]
        public float enemyHealth = 1f;
        [Tooltip("Multiplies how often enemies attack.")]
        public float enemyAttackSpeed = 1f;
        [Tooltip("Multiplies enemies' movement speed.")]
        public float enemyMoveSpeed = 1f;
        [Tooltip("Multiplies enemies' shot speed.")]
        public float enemyProjectileSpeed = 1f;
        [Tooltip("Multiplies enemies' size.")]
        public float enemySize = 1f;
        [Tooltip("Added to the most enemies a generated push has alive at once.")]
        public int extraAlive;
        [Tooltip("Added to the enemies in a generated push.")]
        public int extraPush;
        [Tooltip("Multiplies the quiet seconds between pushes.")]
        public float breatherScale = 1f;
    }

    /// <summary>The words of the three replay modes (main menu buttons and their panels).</summary>
    [Serializable]
    public class ReplayLabels
    {
        public string endless = "Endless Floor";
        public string daily = "Daily Challenge";
        [Tooltip("{0} = the cycle he starts next (2 after the first climb).")]
        public string newGamePlus = "New Game+";
        [Tooltip("{0} = floors climbed.")]
        public string endlessBest = "Best: {0} floors climbed";
        public string endlessNoBest = "No floor climbed yet.";
        [Tooltip("{0} = the date, {1} = the day's twist.")]
        public string dailyHeader = "{0}: {1}";
        public string dailyEmpty = "Nobody has played today's floor here yet.";
        [Tooltip("{0} = place, {1} = score, {2} = floors, {3} = FELL / CLEARED.")]
        public string dailyRow = "{0}. {1} pts  (Floor {2}, {3})";
        public string cleared = "CLEARED";
        public string fell = "FELL";
        public string play = "Play";
        public string back = "Back";
        [Tooltip("{0} = the cycle he starts.")]
        public string ngPlusPlay = "Again (Cycle {0})";
        public string endlessBlurb = "The floor never ends. Neither do they. The best floor you climb is kept.";
        public string dailyBlurb = "One floor a day, the same for everyone. Ten pushes. Today's twist is on the card.";
        [Tooltip("{0} = the cycle he starts next.")]
        public string ngPlusBlurb = "Hell takes the climb back and gives it again, harder. You keep your weapons, Yooba's goods, the diamonds and the Codex. Cycle {0}.";
        public string ngPlusLocked = "Beat the Magma King first.";
        [Tooltip("{0} = the cycle he is on.")]
        public string ngPlusLabel = "New Game+ {0}";
    }

    /// <summary>Endless Floor, the Daily Challenge and New Game Plus (game.json "replay"; Docs/Story.md, "Again").</summary>
    [Serializable]
    public class ReplayConfig
    {
        public bool enabled = true;
        [Tooltip("The level file (levelName) of Endless Floor.")]
        public string endlessLevel = "EndlessFloor";
        [Tooltip("The level file (levelName) of the Daily Challenge.")]
        public string dailyLevel = "DailyFloor";
        [Tooltip("The Waystone floor that must be lit before New Game+ is offered (901: the Magma King fell).")]
        public int ngPlusAfterWaystone = 901;
        [Tooltip("The highest New Game+ cycle.")]
        public int ngPlusMaxCycle = 9;
        [Tooltip("Per cycle: extra enemy health (0.35 = +35%).")]
        public float ngPlusHealth = 0.35f;
        [Tooltip("Per cycle: extra enemy attack speed.")]
        public float ngPlusAttackSpeed = 0.12f;
        [Tooltip("Per cycle: extra enemy movement speed.")]
        public float ngPlusMoveSpeed = 0.05f;
        [Tooltip("Every this many cycles a generated push gets one more enemy alive at once and in it.")]
        public int ngPlusCrowdEvery = 2;
        [Header("Daily score")]
        public int scorePerFloor = 100;
        public int scorePerKill = 10;
        public int scorePerGraze = 2;
        [Tooltip("Paid once if he finishes the day's floor.")]
        public int scoreCleared = 500;
        [Tooltip("Scores kept per day (the best ones).")]
        public int tableSize = 10;
        [Tooltip("Days of tables kept.")]
        public int daysKept = 14;
        [Header("Daily twists")]
        public List<ReplayModifier> modifiers = new List<ReplayModifier>();
        public ReplayLabels labels = new ReplayLabels();
    }
}
