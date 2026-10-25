using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>When a Codex character counts as met.</summary>
    public enum CodexCharacterUnlock
    {
        [Tooltip("From the start: the hero has seen him already (the meadow).")] Always,
        [Tooltip("Once the hero has met Yooba (the Shaft after Video3).")] YoobaMet,
    }

    /// <summary>One character page of the Codex. Enemies, bosses and weapons come from content/; characters are listed here.</summary>
    [Serializable]
    public class CodexCharacter
    {
        public string id = "";
        public string displayName = "";
        [Tooltip("The hero's note: one short deadpan line.")]
        public string note = "";
        [Tooltip("What he is to Hell (the line where an enemy has its weakness).")]
        public string about = "";
        public CodexCharacterUnlock unlock = CodexCharacterUnlock.Always;
        [Tooltip("Sprite sheet whose first sprite is the picture (null = a drawn placeholder).")]
        public Texture2D sheet;
        [Tooltip("Only sprites whose names start with this.")]
        public string spriteNameFilter = "";
        [Tooltip("Placeholder / tint color while there is no art.")]
        public Color color = new Color(0.9f, 0.5f, 0.2f, 1f);
    }

    /// <summary>
    /// The Codex (Docs/Story.md, "The Codex"): everything the hero has seen, in four tabs, from the main menu and the pause
    /// menu (ui/codex.json). Presentation only. The words of the screen are here; the look is ui/theme.json.
    /// </summary>
    [Serializable]
    public class CodexConfig
    {
        public bool enabled = true;
        [Header("Words")]
        public string title = "Codex";
        public string buttonLabel = "Codex";
        public string tabEnemies = "Enemies";
        public string tabBosses = "Bosses";
        public string tabWeapons = "Weapons";
        public string tabCharacters = "Characters";
        [Tooltip("The name of an entry he has not met. No spoilers: nothing else about it is shown.")]
        public string unknownName = "???";
        public string unknownLine = "Not met yet.";
        public string weaknessPrefix = "Weakness: ";
        [Tooltip("{0} = times met, {1} = times killed.")]
        public string tallyFormat = "Met {0}  |  Killed {1}";
        [Tooltip("A weapon he holds / has not found.")]
        public string weaponFound = "Found";
        [Tooltip("{0} = page, {1} = pages, {2} = entries met, {3} = entries.")]
        public string pageFormat = "Page {0}/{1}  ({2}/{3} met)";
        public string previous = "<";
        public string next = ">";
        public string empty = "Nothing here yet.";

        [Header("Layout (in the menu's layout space: 1920x1080 landscape, 1080x1920 portrait)")]
        public MenuPlacement panel = MenuPlacement.At(0.5f, 0.5f, 0f, 0f, 1600f, 960f);
        public MenuPlacement panelPortrait = MenuPlacement.At(0.5f, 0.5f, 0f, 0f, 1032f, 1640f);
        [Tooltip("Entries per page, landscape and portrait.")]
        public int rowsLandscape = 4;
        public int rowsPortrait = 7;
        [Tooltip("The main menu / pause button's place in the list.")]
        public bool showOnMainMenu = true;
        public bool showOnPauseMenu = true;

        [Header("Characters")]
        public List<CodexCharacter> characters = new List<CodexCharacter>();
    }
}
