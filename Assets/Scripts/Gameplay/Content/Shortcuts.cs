using System;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // SHORTCUTS (Docs/Story.md, "Shortcuts")
    // =====================================================================

    [Serializable, JsonType("offerShortcut")]
    [Doc("A shortcut's choice point: pauses the game and offers two cards, keep climbing (card 1) or take the shortcut (card 2) " +
         "with its cost shown (a Grudge). Offered once per play, even across rewinds; the choice is final. Taken, the Grudge is paid " +
         "and the stage leads on to 'nextLevel' instead of its nextLevelNumber, and the Shaft shows a dotted jump to 'toFloor'. " +
         "Branch on it with the shortcutChosen trigger (the script moves the counter itself, e.g. setFloor 'toFloor'). " +
         "Don't say how far it goes: the player isn't told.")]
    public sealed class OfferShortcutAction : GameAction
    {
        [Required, Doc("Names this shortcut (shortcutChosen, the save, the Shaft).")] public string id = "";
        [Doc("The panel's title.")] public string prompt = "A shortcut?";
        [Doc("The shortcut card's title.")] public string title = "Take the shortcut";
        [Doc("The shortcut card's text: what it is, never how far it goes.")] public string description = "";
        [Doc("The keep-climbing card's title and text.")] public string stayTitle = "Keep climbing";
        public string stayDescription = "The long way. Pick a Grudge at the top.";
        [Doc("Grudges it costs (taken from the ones he has, the strongest first). 0 = free.")] public int costGrudges = 1;
        [Doc("The cost line on the card. {0} = the Grudge it will take.")] public string costText = "Costs a Grudge: {0}";
        [Doc("Stamped across the card when he has no Grudge to pay with.")] public string cantPayText = "NO GRUDGE TO GIVE";
        [Doc("The floor it lets him out on (the counter is set by the script; this is for the Shaft and the save).")] public int toFloor;
        [Doc("The level it leads on to (game.json position), skipping the ones between.")] public int nextLevel;
        public Color color = new Color(1f, 0.55f, 0.2f, 1f);

        public override void Run(ActionContext context) => context.World.Shortcuts.Offer(this, context);
    }

    [Serializable, JsonType("shortcutChosen")]
    [Doc("A shortcut offered by offerShortcut has been chosen: taken (taken = true) or passed by (taken = false). " +
         "The choice is made once and kept through rewinds, so a replay takes the same branch.")]
    public sealed class ShortcutChosenTrigger : Trigger
    {
        [Required] public string shortcut = "";
        public bool taken = true;

        public override bool Check(TriggerContext context)
        {
            var choice = context.World.Shortcuts.Choice(shortcut);
            return choice.HasValue && choice.Value == taken;
        }
    }
}
