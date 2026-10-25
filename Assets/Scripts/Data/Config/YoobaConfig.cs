using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>When Yooba says a line (Docs/Story.md, "Yooba between floors").</summary>
    public enum YoobaMoment
    {
        /// <summary>The Shaft after the hello video (Video3): his first meeting with the hero, every hello line in order.</summary>
        Hello,
        /// <summary>A Shaft climb that passed 'floor' (from above it to it or past it).</summary>
        Floor,
        /// <summary>A Shaft climb that ended within 'floor' floors below the next Warden's floor.</summary>
        NearWarden,
        /// <summary>The hero died since the last Shaft.</summary>
        Death,
        /// <summary>A Grudge was picked since the last Shaft.</summary>
        Grudge,
        /// <summary>Nothing else to say: gossip.</summary>
        Chatter,
        /// <summary>The shop opens.</summary>
        ShopWelcome,
        /// <summary>Something was bought.</summary>
        ShopBuy,
        /// <summary>Not enough diamonds.</summary>
        ShopBroke,
        /// <summary>The hero leaves the shop.</summary>
        ShopLeave,
        /// <summary>Something for a weapon was bought (a weapon upgrade, Keep Weapons): Yooba has never heard of weapons.</summary>
        ShopBuyWeapon,
        /// <summary>The shop opens and the hero has died before: Yooba reacts to the count ('minDeaths').</summary>
        ShopDeaths,
        /// <summary>A ware has just appeared on a shelf since the last visit ('item' = the ware's id).</summary>
        ShopStocked,
        /// <summary>He mutters to himself behind the counter while the hero looks (running gags, gossip). Repeats are allowed when 'once' is off.</summary>
        ShopIdle,
        /// <summary>The Lost Things quest: he asks (once the hero has climbed far enough).</summary>
        QuestOffer,
        /// <summary>The hero has found a lost thing ('requires' names its flag); said once on the next visit.</summary>
        QuestFound,
        /// <summary>The quest is asked but not finished: a nudge.</summary>
        QuestRemind,
        /// <summary>All three found: he pays the reward.</summary>
        QuestDone,
    }

    /// <summary>One of Yooba's portrait loops: a sprite sheet cut into equal cells, read left to right, top to bottom.</summary>
    [Serializable]
    public class YoobaClip
    {
        [Tooltip("idle, talk, laugh or cough.")]
        public string name = "idle";
        [Tooltip("The sheet (keyed, transparent background). Empty = the placeholder slime drawn from shapes.")]
        public Sprite sheet;
        public int columns = 4;
        public int rows = 4;
        [Tooltip("Frames used (from the first cell). 0 = every cell.")]
        public int frames = 0;
        public float fps = 12f;
    }

    /// <summary>A line Yooba says (a Voice sound in audio.json with voice "yooba") and an optional one-liner back from the hero.</summary>
    [Serializable]
    public class YoobaLine
    {
        public YoobaMoment moment = YoobaMoment.Chatter;
        [Tooltip("Floor: the floor the climb must pass. NearWarden: how many floors below the Warden.")]
        public int floor;
        [Tooltip("Sound id in audio.json.")]
        public string sound = "";
        [Tooltip("The subtitle.")]
        public string text = "";
        [Tooltip("The portrait loop while he says it: talk, laugh or cough.")]
        public string clip = "talk";
        [Tooltip("The hero's answer (sound id, voice player). Empty = none.")]
        public string reply = "";
        public string replyText = "";
        [Tooltip("Between floors: a line already said is skipped while another of its moment is still new.")]
        public bool once = true;
        [Tooltip("ShopBuy / ShopStocked: only for this good or ware (an upgrade id from player.json, a ware id from 'shelves'). Empty = any.")]
        public string item = "";
        [Tooltip("Shop: only once the hero has died at least this many times in all (Yooba keeps count).")]
        public int minDeaths;
        [Tooltip("Only while all of these hold: 'flag:id', 'level:N' (level cleared), 'shop:id:level', 'grudge:id', 'weapon:id' (see Requirements).")]
        public List<string> requires = new List<string>();
    }

    /// <summary>A ware on Yooba's shelf: a labelled jar that is on the shelf only once its 'requires' holds (a level cleared, a quest done).</summary>
    [Serializable]
    public class YoobaWare
    {
        public string id = "";
        [Tooltip("Short capital words on the jar.")]
        public string label = "";
        [Tooltip("What the hero reads on the shelf's tag when it is new.")]
        public string description = "";
        public Color color = new Color(0.6f, 0.85f, 0.4f, 1f);
        [Tooltip("Stocked once all of these hold (e.g. 'level:11': Floor 991 cleared).")]
        public List<string> requires = new List<string>();
    }

    /// <summary>One lost thing of the quest: a pickup on a floor sets 'flag'.</summary>
    [Serializable]
    public class YoobaLostThing
    {
        [Tooltip("The unlock flag the pickup's setFlag action writes (flag:<this> in 'requires').")]
        public string flag = "";
        public string label = "";
    }

    /// <summary>The Lost Things quest (Docs/Story.md, "Yooba's shop"): three things of the village's that fell out of the portal; found on floors, paid at the counter.</summary>
    [Serializable]
    public class YoobaQuest
    {
        public bool enabled = true;
        public string title = "LOST THINGS";
        public List<YoobaLostThing> things = new List<YoobaLostThing>();
        [Tooltip("Diamonds paid when all are found.")]
        public int rewardDiamonds = 60;
        [Tooltip("Flag written when the reward has been paid (never twice).")]
        public string paidFlag = "lost.paid";
        [Tooltip("Flag written when he has asked.")]
        public string askedFlag = "lost.asked";
        [Tooltip("Shown on the ledger chip. {0} found, {1} total.")]
        public string chipFormat = "LOST THINGS {0}/{1}";
        public string doneText = "LOST THINGS: DONE";
    }

    /// <summary>
    /// Yooba, the village's slime and the hero's one friend in Hell (Docs/Story.md, "Yooba between floors"): his
    /// talking portrait and lines on the Shaft and in the shop (ui/yooba.json). Presentation only.
    /// </summary>
    [Serializable]
    public class YoobaConfig
    {
        [Tooltip("Off = no Yooba on the Shaft or in the shop.")]
        public bool enabled = true;
        [Tooltip("The video he turns up in: the Shaft after it is his hello.")]
        public int helloAfterVideo = 3;
        [Tooltip("The name over his portrait.")]
        public string displayName = "YOOBA";
        [Tooltip("Subtitle speaker labels.")]
        public string speaker = "Yooba";
        public string heroSpeaker = "You";
        [Tooltip("Seconds between his line and the hero's answer, and after the last one.")]
        public float pause = 0.35f;
        [Tooltip("A line without a clip yet is shown this long per character (at least 1.5 s).")]
        public float secondsPerCharacter = 0.06f;
        public Color frame = new Color(0.16f, 0.42f, 0.14f, 1f);
        public Color body = new Color(0.17f, 0.62f, 0.2f, 1f);

        [Header("Portrait loops (idle, talk, laugh, cough)")]
        public List<YoobaClip> clips = new List<YoobaClip>();

        [Header("Lines")]
        public List<YoobaLine> lines = new List<YoobaLine>();

        [Header("The shop room")]
        [Tooltip("The shelf behind the counter: wares appear as the hero climbs.")]
        public List<YoobaWare> shelves = new List<YoobaWare>();
        public YoobaQuest quest = new YoobaQuest();
        [Tooltip("He sniffs, blinks, hops and hums by himself every few seconds (seconds between; 0 = off).")]
        public float idleBehaviorSeconds = 4f;
        [Tooltip("Seconds of quiet in the shop before he mutters a ShopIdle line (0 = off).")]
        public float idleLineSeconds = 20f;
        public MenuPlacement shelf = MenuPlacement.At(0.5f, 0f, 0f, 36f, 1300f, 64f);
        public MenuPlacement shelfPortrait = MenuPlacement.At(0.5f, 0f, 0f, 190f, 1000f, 64f);

        [Header("Placement")]
        [Tooltip("His portrait on the Shaft (left of the shaft window).")]
        public MenuPlacement shaftPortrait = MenuPlacement.At(0f, 0f, 150f, 200f, 260f, 260f);
        public MenuPlacement shaftPortraitPortrait = MenuPlacement.At(1f, 1f, -150f, -840f, 220f, 220f);
        [Tooltip("His portrait in the shop, and what he says there.")]
        public MenuPlacement shopPortrait = MenuPlacement.At(0f, 1f, 160f, -140f, 220f, 220f);
        public MenuPlacement shopPortraitPortrait = MenuPlacement.At(0f, 1f, 130f, -130f, 200f, 200f);
        public MenuPlacement shopSubtitle = MenuPlacement.At(0.5f, 0f, 0f, 40f, 1300f, 70f);
        public MenuPlacement shopSubtitlePortrait = MenuPlacement.At(0.5f, 0f, 150f, 90f, 740f, 90f);
    }
}
