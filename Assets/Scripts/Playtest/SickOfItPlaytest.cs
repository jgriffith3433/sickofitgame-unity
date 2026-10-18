using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.VideoFrameExtractor.Runtime;
using Object = UnityEngine.Object;
using PlaytestBot = SickOfIt.Gameplay.PlaytestBot;

namespace SickOfIt.Playtest
{
    /// <summary>
    /// Sick Of It's side of the Hawkes playtest contract v1 (C:/Projects/hawkesstudio/docs/playtest.md).
    /// Registers the scenarios at startup in every build (they cost nothing until one starts) and plays them with
    /// <see cref="PlaytestBot"/> through virtual devices (<see cref="PlaytestInput"/>): keys, sticks and mouse clicks
    /// on the real menus. In a WebGL build the browser starts them (window.hawkesPlaytest, or ?playtest=smoke);
    /// in the editor and the PlayMode playtests, call <c>HawkesPlaytest.StartScenario("level-3")</c>.
    ///
    /// Scenarios: smoke (menu, New Game, level 1 for ~30 s, rewind, pause), level-N (starts level N directly,
    /// a setup shortcut), full (New Game and every level in order through the real flow, ~10 min), menus (level
    /// select, videos, shop and settings open and close; settings turns Sound off and on and steps Music), site (the
    /// website pages around the game show "Powered by HAWKES GEN", the landing page links Hawkes Biomedical, and the
    /// game page itself has neither), layout (every screen's buttons on screen, apart and thumb-sized: main menu,
    /// level select, settings, the level HUD, pause and its settings, the death scene; meant for device "phone").
    /// cutscene (the scene content/scenes/cutsceneTest.json on level 1's stage, a setup shortcut: the hero walks there
    /// by himself and stops, turns, can't rewind, the camera zooms in and back, the screen fades to black and back, and
    /// he is pulled into a point, spinning and shrinking until he is gone; each beat is checked).
    /// video-2 (Video2 loaded directly and watched, not skipped: the hero's fall overlay appears at the burning door,
    /// spins and shrinks into the tunnel and is gone by ~14.5 s; R held mid-spin plays it backward; Escape skips).
    /// portal-audio (the prologue's portal scene sounds and the Meadow's lines played in order on the main menu).
    /// story-path (New Game through the story path: Video1, the Meadow, Video2, the Landing with its floor counter
    /// and the 999 beat, Video3 (Yooba), the Spill, then the level select with the story levels in order).
    /// Every scenario starts by pressing the page's "Tap to play" sound prompt (<see cref="PageSound"/>).
    /// </summary>
    public static class SickOfItPlaytest
    {
        public const string Product = "sickofit";

        /// <summary>
        /// The setup shortcut level-N scenarios (and the full run's fallback) use to open a level. Returns the load
        /// operation, or null. The PlayMode playtests replace it with the editor's scene loader.
        /// </summary>
        public static Func<int, AsyncOperation> LoadLevel = DefaultLoadLevel;

        static AsyncOperation DefaultLoadLevel(int number)
        {
            SceneTransitionManager.Instance.LoadLevel(number);
            return null;
        }

        /// <summary>Real seconds the game clock may stand still mid-level (not paused) before it counts as a softlock.</summary>
        public static float SoftlockSeconds = 15f;

        /// <summary>The level-N scenario's last result, for the PlayMode playtest report.</summary>
        public static string LastLevelOutcome { get; private set; }

        static Session active;

        /// <summary>The level the imp-death scenario plays: Level 1's "imps" phase brings four, one at a time first.</summary>
        public const int ImpLevel = 1;

        /// <summary>
        /// The magma-king scenario: the level that holds his fight (Level 11, the Slag Throne: Docs/Story.md "The Magma
        /// King's fight"), the animation states he must be seen in, and his lines (text, audio.json sound, who must voice it).
        /// </summary>
        public const string MagmaKingLevel = "Level11", MagmaKingId = "magmaKing";
        const float MagmaKingSeconds = 300f;
        static readonly string[] MagmaKingStates = { "Idle", "Throw", "SlamSeat", "Hurt", "Rise", "Enrage", "IdleStand", "Slam", "Breath", "HurtStand" };
        static readonly (string beat, string text, string sound, string voice)[] MagmaKingLines =
        {
            ("intro", "Mm-hm-hm. An audience, at last. Welcome to the Slag Throne, little spark.", "vo_mk_intro", "magmaKing"),
            ("hero intro", "He's not getting up, is he.", "vo_mk_hero_intro", "player"),
            ("phase 1", "Dance for me, little spark. Hell loves a show.", "vo_mk_phase1", "magmaKing"),
            ("hurt", "Mm. That one tickled.", "vo_mk_hurt1", "magmaKing"),
            ("stand", "...You made me STAND.", "vo_mk_enrage", "magmaKing"),
            ("guards", "Guards. Make some noise for our guest.", "vo_mk_guards", "magmaKing"),
            ("enrage", "Now. Let's give them a real show.", "vo_mk_phase2", "magmaKing"),
            ("hurt 2", "You're ruining... my show.", "vo_mk_hurt2", "magmaKing"),
            ("defeat", "The show... must go... on...", "vo_mk_defeat", "magmaKing"),
            ("fallen", "THE MAGMA KING HAS FALLEN!", "vo_mk_fallen", "announcer"),
            ("hero defeat", "He got up for me. I'm touched.", "vo_mk_hero_defeat", "player"),
        };

        /// <summary>
        /// A Warden's arena (a floor stage with a locked Static screen, Level 11): each attack and the warning glow that
        /// must come at least ArenaTelegraphSeconds before it, and the signals its fight emits in order (intro, the three
        /// phases, the enrage, the flood, the defeat, the Waystone). Docs/Story.md, "The Magma King's fight".
        /// </summary>
        static readonly (string attack, string warning, string what)[] ArenaTelegraphs =
        {
            ("magmaBoulder", "mkWarnHand", "boulder"),
            ("slamShockwave", "mkWarnFloor", "shockwave"),
            ("lavaFlood", "mkWarnFlood", "lava flood"),
            ("fireball", "mkWarnMouth", "fire breath"),
        };
        const float ArenaTelegraphSeconds = 1f;
        static readonly (string signal, string beat)[] ArenaSignals =
        {
            ("mkIntro", "intro: the throne rises"),
            ("mkFight", "phase 1: from the throne"),
            ("mkStand", "half health: he stands"),
            ("mkPhase2", "phase 2: standing, with his guard"),
            ("mkEnrage", "a third: the enrage"),
            ("mkFlood", "phase 3: the flood"),
            ("mkDefeat", "defeat"),
            ("waystoneLit", "the Waystone lights"),
        };

        /// <summary>The cutscene scenario: the scene it plays (content/scenes) and the level whose stage it plays on.</summary>
        public const string CutsceneScene = "cutsceneTest";
        public const int CutsceneLevel = 1;

        /// <summary>The climb scenario: the test floor it plays (content/scenes, a floor with a path) and the level whose stage it plays on.</summary>
        public const string ClimbScene = "climbTest";
        public const int ClimbLevel = 2;
        const float ClimbSeconds = 200f;

        /// <summary>The level whose level-N scenario also watches the portal scene at its end (Level1.json, phases portalBeam to portalClose) and Video2 after it.</summary>
        public const int PortalLevel = 1;

        /// <summary>The video-2 scenario: the video it watches and the timeline overlay it checks (the hero falling down the fire tunnel).</summary>
        public const int OverlayVideo = 2;
        public const string OverlayName = "heroFall";

        /// <summary>
        /// The story-path scenario: each story level in order and the video that must play before it (0 = none: the level
        /// starts straight after the Shaft that follows the one before). Docs/Story.md, "Level numbers".
        /// </summary>
        static readonly (int video, int level)[] StoryPath = { (1, 1), (2, 2), (3, 3), (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9), (0, 10), (0, 11) };
        /// <summary>The Landing (Level 2): the floors its counter must run through, and the milestone it must play.</summary>
        public const int LandingLevel = 2, LandingFirstFloor = 1000, LandingLastFloor = 991, LandingMilestone = 999;
        const float StoryPathSeconds = 2100f;
        /// <summary>
        /// The wastes-climb scenario: the stage it loads directly (The Loop) and the last one it plays through the real flow
        /// (The Heat Stairs, ending at the Magma King's door). Docs/Story.md, "Circle X stage outline".
        /// </summary>
        public const int ClimbFrom = 6, ClimbTo = 11;
        const float WastesClimbSeconds = 960f;
        /// <summary>The shaft scenario: the level after the plunge whose climb it watches (The Landing).</summary>
        public const int ShaftLevel = 2;
        const float ShaftSeconds = 480f;
        /// <summary>The music scenario: after the shaft scenario's run, the Warden's arena it loads directly (the Magma King).</summary>
        public const int MusicBossLevel = 11;
        const float MusicBossSeconds = 420f;
        const float YoobaSeconds = 300f;
        /// <summary>The waystone scenario: the stage the bot clears first (The Spill, for a Grudge), then dies on the next one.</summary>
        public const int WaystoneLevel = 3;
        const float WaystoneSeconds = 480f;
        /// <summary>
        /// The shortcut scenario (Docs/Story.md, "Shortcuts"): the stage with Hell's first shortcut (The Old Stair, Floors
        /// 880-871, the collapsed stairwell at its top), the floor it lets him out on, and the stage it leads to, past the
        /// ones it skips (Levels 15-17).
        /// </summary>
        public const int ShortcutLevel = 14, ShortcutToFloor = 841, ShortcutNextLevel = 18;
        const float ShortcutSeconds = 480f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Register()
        {
            HawkesPlaytest.Register(Product, Scenarios(), Bot, IsReady, StopActive);
            HawkesPlaytest.Finished -= OnFinished;
            HawkesPlaytest.Finished += OnFinished;
        }

        public static PlaytestScenario[] Scenarios()
        {
            var list = new List<PlaytestScenario>
            {
                new PlaytestScenario("smoke", "Boot, main menu, New Game, level 1 for 30 s with a rewind and a pause", 55, "smoke"),
            };
            var definition = GameDefinition.Instance;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var level = definition.levels[n - 1];
                string name = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : level?.levelName;
                float seconds = Mathf.Max(30f, level != null ? level.duration : 60f) + 30f;
                string what = $"Level {n} ({name}), started directly (setup shortcut)";
                if (n == PortalLevel)
                {
                    what += ", then its ending, the portal scene, checked beat by beat: after the Grudge pick the Overlord's beam is telegraphed by a charge glow and " +
                            "comes down as a burning column with an impact burst beside the hero, the ground tears open into a swirling fire portal " +
                            "(the one from Video2) that loops while he's there and flares shut to a scorch at the end, he walks to it by himself, the lines and the echo play with subtitles, " +
                            "he turns to leave and is pulled in spinning and shrinking, a slime is slurped in after him, and Video2 starts (Continue on " +
                            "the results); move, jump, fire and R are pressed during it and must do nothing, and he takes no damage";
                    seconds += 30f;
                }
                else if (IsArena(n))
                {
                    what += ": a Warden's arena (a locked screen), checked as it plays: the title banner (\"FLOORS N-M: PLACE\") with its voice, then the " +
                            "intro (input and rewind locked: the camera pans up to the throne as it rises out of the slag, the announcer's \"FLOOR 901! THE WARDEN " +
                            "OF THE WASTES!\" and \"THE MAGMA KING\", his voiced taunt, the hero's one-liner, the boss bar), the fight's three phases in order " +
                            "(from the throne; at half health he stands and calls his imp guard, the only phase with adds; at a third he enrages and floods the hall), " +
                            "every attack (boulder, shockwave, fire breath, lava flood) after its warning glow 1+ s ahead, no more than 3 enemies at once, every line " +
                            "with its voice; his defeat: slow motion, his Die clip (collapsing into slag), his last line, \"THE MAGMA KING HAS FALLEN!\", the floor " +
                            "counter ticking from 910 to 901 and the Waystone lighting there (\"WAYSTONE LIT: FLOOR 901\"), the Grudge pick; then Continue on the " +
                            "results, the Shaft's climb to 901 with that Waystone saved and lit and Circle IX's band waking up (voiced), and the level select";
                    seconds += 60f;
                }
                else if (name != null && name.StartsWith("Floors ", StringComparison.Ordinal))
                {
                    what += ": a stage of Hell, checked as it plays: its title banner (\"FLOORS N-M: PLACE\") and hero lines with their voices, the floor " +
                            "counter running from its first floor to its last, its carved sign at the start, at most one new enemy type and the first one alone, " +
                            "a breather of 3+ s with nothing attacking, no more than 3 enemies at once, a repeated floor shown on two signs where the stage has one " +
                            "(The Loop), 60-120 s, its opening lines (the title and any milestone, e.g. Floor 950 on Level 7) before the first enemy, every " +
                            "scripted line shown with its voice, and where the stage ends at a door (The Heat Stairs) the door standing at the top with a " +
                            "Warden's voice heard before it, the Grudge pick; then Continue on the results, the Shaft's climb to its last floor, and what comes next " +
                            "(the next stage, video or the level select)";
                    seconds += 40f;
                }
                list.Add(new PlaytestScenario("level-" + n, what, seconds, "full", "level"));
            }
            list.Add(new PlaytestScenario("imp-death", $"Level {ImpLevel}'s imps, started directly (setup shortcut): the bot kills them and each imp's Die clip " +
                "must play where it died; the first deaths run in slow motion so the clip can be seen", 90, "level"));
            list.Add(new PlaytestScenario("magma-king", $"The Magma King's fight in \"{MagmaKingLevel}\" (the Slag Throne, loaded directly, a setup shortcut; " +
                "the bot plays the fight through real input, up to 3 tries): his generated animations seen at game size, each a check event: Idle, " +
                "Throw (molten boulder) and SlamSeat (fist on the throne's arm, shockwave) from the throne, Hurt, then at half health Rise " +
                "(the only time he stands), IdleStand, Slam (ground-pound), Breath and HurtStand standing, Enrage at a third, and his Die clip playing where he fell; " +
                "his on-screen height; every line with its subtitle and his own voice (intro, phase 1, hurt, stand, guards, enrage, hurt, defeat), the " +
                "announcer's \"HAS FALLEN\" and the hero's replies", MagmaKingSeconds, "level"));
            list.Add(new PlaytestScenario("cutscene", $"The scene \"{CutsceneScene}\" on level {CutsceneLevel}'s stage (setup shortcut): with input and rewind locked " +
                "the hero walks to a point by himself and stops, turns left and right, R does nothing; the camera zooms in and back; the screen fades " +
                "to black and back; he is pulled into a point, spinning and shrinking until he disappears. Each beat is checked", 75, "level"));
            list.Add(new PlaytestScenario("climb", $"The floor toolkit on a test floor: the scene \"{ClimbScene}\" on level {ClimbLevel}'s stage (setup shortcut), " +
                "one floor of Hell played by the bot through real input. Its path turns from a climb up a lava fall to a run sideways over a lava river " +
                "and a chase, then up again, with the lava sea and the parallax world below it (far sea, spires, smoke, floating islands, embers). Each " +
                "beat is a check event: the lava sea and parallax, the camera climbing, running sideways and climbing again, a stone sinking while he " +
                "stands on it and rising back when he leaves, a crumbling stone falling, a raft drifting on the current and carrying him, the hero " +
                "hanging from a swinging chain and jumping off, the lava wave chasing him sideways, a rewind (hold R) bringing a sunk stone back up, " +
                "and the carved landing with the floor number, which ends the level", ClimbSeconds, "level"));
            list.Add(new PlaytestScenario("video-" + OverlayVideo, $"Video {OverlayVideo}, started directly (setup shortcut) and watched instead of skipped: " +
                $"the overlay \"{OverlayName}\" (the hero) appears as the camera goes through the burning door, spins 3+ turns and shrinks into the tunnel's " +
                "centre and is gone before the lava river (~14.5 s); holding R mid-spin plays it backward; then Escape skips the video. Each beat is checked", 75, "video"));
            list.Add(new PlaytestScenario("video-" + OverlayVideo + "-tunnel", $"Video {OverlayVideo}, started directly (setup shortcut) and watched with no input " +
                "from the burning door through the fire tunnel to the lava river (10.6-17.5 s): no COMING SOON caption in the tunnel (screen pixels in the box " +
                "it used to cover), the frame on screen matches the clock, the soundtrack matches the clock, and the video is as long as its soundtrack", 60, "video"));
            list.Add(new PlaytestScenario("full", "New Game, then every level in order through the real flow, as far as the bot gets", 600, "full"));
            list.Add(new PlaytestScenario("story-path", "New Game through the story path with the real flow: Video1, Level 1 (the Meadow and its portal " +
                $"scene), Video2, Level 2 (Floors {LandingFirstFloor}-{LandingLastFloor}: The Landing, no arcade level in between: its floor counter runs " +
                $"{LandingFirstFloor} to {LandingLastFloor} and the \"ONLY {LandingMilestone} TO GO\" beat plays), Video3 (Yooba, watched a few seconds), then the " +
                "Ember Wastes stages one after another with no video between them: Level 3 (Floors 990-981: The Spill), Level 4 (980-971: Imp Run), Level 5 " +
                "(970-961: The Slag Falls), Level 6 (960-951: The Loop), Level 7 (950-941: Halfway Basin, opening on the Floor 950 beat), Level 8 " +
                "(940-931: The Crust), Level 9 (930-921: The Sack Pile), Level 10 (920-911: The Heat Stairs, ending at the Magma King's door with " +
                "his voice heard ahead), Level 11 (910-901: The Slag Throne, the Magma King's arena, his Waystone at 901 and Circle IX waking on the Shaft), each with its title, voiced lines, floor counter, new enemy alone and breather checked " +
                "and the Shaft's climb after it; then the level select, whose cards list the story levels first and in order. Each step is a check event", StoryPathSeconds, "full"));
            list.Add(new PlaytestScenario("wastes-climb", $"Level {ClimbFrom} (The Loop) loaded directly (setup shortcut), then the real flow on through " +
                $"Level {ClimbTo}: results, Continue, the Shaft's climb, and the next stage with no video between, so Levels 7-11 play in order after Level 6 " +
                "and the floors run 960 to 901. Each stage gets the level-n stage checks (title, voiced lines, floors, sign, new enemy alone, breather, " +
                "enemies at once, length, Grudge). Level 7 must open on the Floor 950 beat (\"FLOOR 950: HALFWAY TO 900\" with its voice, before " +
                "the first enemy); Level 10 must end at the Magma King's door (\"THE SLAG THRONE\") with his voice heard from ahead before it; " +
                "Level 11 is his arena (the boss checks of level-11), and its Shaft climbs to 901 with the Waystone lit and Circle IX waking; " +
                "then the level select. Up to 3 tries per stage; each beat is a check event", WastesClimbSeconds, "full"));
            list.Add(new PlaytestScenario("shortcut", $"Hell's first shortcut (Docs/Story.md, \"Shortcuts\"): level {ShortcutLevel} (Floors 880-871: The Old Stair, " +
                "Circle IX) loaded directly with one Grudge in hand (setup shortcut: given if he has none). The bot plays it through real input to the " +
                "collapsed stairwell at the top, where the choice comes up: card 1 keeps climbing, card 2 is the stairwell with its cost shown (a Grudge, " +
                "named); the bot presses 2. Checked: the choice and its cost on screen, the Grudge paid, the hero's line and the announcer's reaction voiced, " +
                $"the floor counter jumping from 871 to {ShortcutToFloor} with no Grudge pick after; then the results, the Shaft (the marker climbs to 871, " +
                $"then jumps a dotted line up through the wall's crack to FLOOR {ShortcutToFloor}), Continue, and the next stage is level {ShortcutNextLevel} " +
                $"(Floors 840-831), skipping levels {ShortcutLevel + 1}-{ShortcutNextLevel - 1}; the bot plays it with the stage checks. Each beat is a check event", ShortcutSeconds, "full"));
            list.Add(new PlaytestScenario("shaft", $"The Shaft, Hell's map, through the real flow: Video {OverlayVideo} loaded directly (setup shortcut; the Shaft's save is " +
                "reset so this is the first plunge) and skipped, then the plunge from the Meadow past all 10 Circle bands to FLOOR 1,000 (Space pressed " +
                "mid-fall must not skip the first one), the slam, the announcer's \"nine hundred ninety-nine to go\" and the hero's \"...Great.\"; " +
                $"Continue starts level {ShaftLevel}; the bot plays it; after its results the hero's marker climbs and the counter ticks to the floor " +
                "reached with the floors left and the next Warden shown; Continue goes on. The layout is audited on both Shaft screens. Each beat is a check event",
                ShaftSeconds, "full"));
            list.Add(new PlaytestScenario("music", "Hell's soundtrack (Docs/Story.md, \"Music\"): the shaft scenario's run (Video 2 skipped, the first plunge, " +
                $"level {ShaftLevel}, the climb) listening to the music, then level {MusicBossLevel} loaded directly (setup shortcut) and fought: the plunge " +
                "plays its falling riser and slam, the Ember Wastes loop plays on the stage and the push music takes over in a generated push, the climb " +
                "plays its rising sting, the Magma King's fight starts on his theme and turns to the enrage variant after mkEnrage. Each music switch is a " +
                "music event, each beat a check event", ShaftSeconds + MusicBossSeconds, "full"));
            list.Add(new PlaytestScenario("yooba", "Yooba, the village slime: his hello on the Shaft after Video3 (setup shortcut: his save reset, the marker at " +
                "Floor 991; Video3 loaded directly and skipped after 6 s): his talking portrait (idle and talk loops), every hello line with its voice and the " +
                "hero's one-line reply, then Continue starts Level 3. Then three Shaft previews over the menu (setup shortcuts) where he reacts to the floor: " +
                "950, a death on the way, the Magma King's door. Then the shop with his portrait: welcome, a card clicked with 0 diamonds (can't afford), " +
                "the same card with enough (buy), Back (goodbye). Each beat is a check event", YoobaSeconds, "menus"));
            list.Add(new PlaytestScenario("yooba-shop", "Yooba's shop from the Shaft: the Shaft after Video3 (setup shortcut: Yooba's save reset, the marker " +
                "at Floor 991, his hello skipped), Visit Yooba beside Continue opens his shop over the Shaft with his animated portrait and voiced welcome, " +
                "slime-made goods; a card clicked with 0 diamonds (can't afford), then with its price (bought: diamonds spent, level saved); Back (goodbye) " +
                "returns to the Shaft and Continue starts Level 3 with the goods applied; the main menu's Visit Yooba shows them still bought; a climb " +
                "between floors (preview) shows Visit Yooba again. Each beat is a check event", 240, "menus"));
            list.Add(new PlaytestScenario("waystone", "Waystones and runs: a save where the Landing was cleared (setup shortcut: the Landing's Waystone, " +
                $"Yooba met, diamonds, one of Yooba's goods bought), then level {WaystoneLevel} (The Spill) loaded directly and played by the bot, its Grudge " +
                "picked with 1, the results, the Shaft's climb and Continue to the next stage, where the bot plays to lose (walks into the shots, no " +
                "dodging, firing or rewinding) until Hell kills it: the death screen (\"YOU OBVIOUSLY DIED\", the announcer gloating with a voice), then " +
                "the Shaft drops the hero's marker from the floor he died on back to the Waystone at FLOOR 1,000 with the Waystones marked on the map, " +
                "the first death's voiced line (\"Hell doesn't let you die. It lets you do it again.\") and the hero's answer, not skippable; the Grudges " +
                "are gone, the diamonds and Yooba's goods kept; Continue restarts the run at Level 2 (The Landing); then the main menu offers Continue " +
                "(the current run) and New Run (from the Waystone), and New Run starts Level 2 again. Each beat is a check event", WaystoneSeconds, "full"));
            list.Add(new PlaytestScenario("menus", "Tap to play, then level select, videos, Yooba's shop and settings (Sound off/on, Music -/+) each open and close, " +
                "then the Shaft (a preview over the menu, setup shortcut, Yooba met) opens, its layout is checked, Visit Yooba opens his shop over it, " +
                "Back returns to the Shaft and Continue closes it", 150, "menus"));
            list.Add(new PlaytestScenario("layout", "Phone layout check (run with device \"phone\", 390x844): on the main menu, level select, settings, " +
                "level 1's HUD, the pause menu and its settings, the Shaft (a preview over the menu, setup shortcut, with Visit Yooba beside Continue), Yooba's shop over it, and the death scene (loaded directly, a setup shortcut), no button overlaps another " +
                "or a HUD panel, none leaves the screen, and every button is thumb-sized", 120, "menus"));
            list.Add(new PlaytestScenario("portal-audio", "The prologue's portal scene, by ear, on the main menu: every Overlord line, hero take, " +
                "Hell's echo (three repeats, each deeper) and effect plays in scene order, then the Meadow's lines; any without a clip is a bug", 150, "menus", "audio"));
            list.Add(new PlaytestScenario("site", "The game's main menu (no credit on the game page), then the landing page, blog and sub pages each opened over it: " +
                "Powered by HAWKES GEN on every one, Hawkes Biomedical on the landing page; the lore (the Descent, its floor counter at 1,000 at the bottom, " +
                "the new lore pages) and the post about the engine, linked from the blog and Development; on each page no link or button overlaps " +
                "another or leaves the screen and the page is no wider than the screen (run it on device \"phone\" for the phone check)", 150, "menus"));
            return list.ToArray();
        }

        /// <summary>A Warden's arena: a stage with floors on a locked Static screen (Level 11, the Slag Throne).</summary>
        public static bool IsArena(int n)
        {
            var definition = GameDefinition.Instance;
            var level = definition != null && n > 0 && n <= definition.TotalLevels ? definition.GetLevel(n) : null;
            var content = GameContent.Current;
            return level != null && content != null && content.LevelScripts.TryGetValue(level.levelName ?? "", out var script) && script != null
                   && script.floors != null && script.floors.start > 0 && script.floors.end > 0
                   && script.world.camera == SickOfIt.Gameplay.Content.CameraMode.Static;
        }

        static bool IsReady()
        {
            if (GameDefinition.Instance == null)
                return false;
            // A test that loads levels itself starts from an empty scene.
            if (LoadLevel != DefaultLoadLevel)
                return true;
            return Object.FindAnyObjectByType<MainMenuManager>() != null || GameplayWorld.Instance != null
                   || Object.FindAnyObjectByType<MenuCanvas>() != null;
        }

        static void StopActive()
        {
            active?.Dispose();
            active = null;
        }

        static void OnFinished(PlaytestRun run)
        {
            if (active != null && active.Run == run)
            {
                active.Close();
                StopActive();
            }
        }

        static IEnumerator Bot(string id, PlaytestOptions options, PlaytestRun run)
        {
            StopActive();
            var session = active = new Session(run, options);
            yield return session.TapSoundPrompt();
            run.Note("waiting for the game to load");
            yield return run.Until(IsReady, 90f, "the game to load");
            if (run.Done)
                yield break;
            session.CheckContent();

            if (id == "smoke")
                yield return session.Smoke();
            else if (id == "full")
                yield return session.Full();
            else if (id == "story-path")
                yield return session.StoryPathRun();
            else if (id == "shaft")
                yield return session.ShaftRun();
            else if (id == "music")
                yield return session.ShaftRun(music: true);
            else if (id == "yooba")
                yield return session.YoobaRun();
            else if (id == "yooba-shop")
                yield return session.YoobaShopRun();
            else if (id == "waystone")
                yield return session.WaystoneRun();
            else if (id == "wastes-climb")
                yield return session.WastesClimb();
            else if (id == "shortcut")
                yield return session.ShortcutRun();
            else if (id == "menus")
                yield return session.Menus();
            else if (id == "site")
                yield return session.Site();
            else if (id == "layout")
                yield return session.Layout();
            else if (id == "imp-death")
                yield return session.ImpDeath();
            else if (id == "portal-audio")
                yield return session.PortalAudio();
            else if (id == "cutscene")
                yield return session.Cutscene();
            else if (id == "climb")
                yield return session.ClimbRun();
            else if (id == "magma-king")
                yield return session.MagmaKingRun();
            else if (id == "video-" + OverlayVideo)
                yield return session.VideoOverlay();
            else if (id == "video-" + OverlayVideo + "-tunnel")
                yield return session.VideoTunnel();
            else if (id.StartsWith("level-", StringComparison.Ordinal) && int.TryParse(id.Substring(6), out int n))
                yield return session.LevelScenario(n);
            else
            {
                run.Problem(PlaytestSeverity.Bug, "unknown scenario " + id);
                run.Finish(PlaytestOutcome.Error);
            }
        }

        /// <summary>One run: the virtual devices, the bot, and everything it watches (logs, telemetry, fps).</summary>
        sealed class Session
        {
            public readonly PlaytestRun Run;
            readonly PlaytestOptions options;
            readonly PlaytestInput input;
            readonly FpsSampler fps;
            readonly int diamondsAtStart;
            PlaytestBot bot;
            string lastOutcome;
            int deaths;
            int botRewinds;
            int impDeaths;
            MagmaWatch magma;
            bool impSlowMotion;
            float slowMotionUntil;

            /// <summary>imp-death: how many deaths play in slow motion, and how slow.</summary>
            const int ImpSlowMotionDeaths = 3;
            const float ImpSlowMotion = 0.3f;

            public Session(PlaytestRun run, PlaytestOptions options)
            {
                Run = run;
                this.options = options;
                input = PlaytestInput.Create();
                var go = new GameObject("PlaytestFps") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                fps = go.AddComponent<FpsSampler>();
                diamondsAtStart = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                Application.logMessageReceived += OnLog;
                GameplayTelemetry.Recorded += OnTelemetry;
                GameplayTelemetry.Finished += OnLevelLog;
                ShaftScreen.Reported += OnShaft;
                YoobaPortrait.Reported += OnYooba;
            }

            float Speed => Mathf.Clamp(options.speed, 0.25f, 4f);

            /// <summary>Writes the final metrics, then finishes (the report the browser reads is taken at Finish).</summary>
            void End(PlaytestOutcome outcome)
            {
                Close();
                Run.Finish(outcome);
            }

            /// <summary>Final metrics (also on a finish the package makes: stuck, error).</summary>
            public void Close()
            {
                if (fps != null && fps.Samples > 0)
                {
                    Run.Metric("fps", fps.Average);
                    Run.Metric("fpsWorst", fps.Worst);
                }
                Run.Metric("deaths", deaths);
                if (RogueliteManager.Instance != null)
                    Run.Metric("diamonds", RogueliteManager.Instance.GetPoints() - diamondsAtStart);
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
                TimelineOverlayPlayer.VisibilityChanged -= OnOverlayVisibility;
                GameplayTelemetry.Recorded -= OnTelemetry;
                GameplayTelemetry.Finished -= OnLevelLog;
                ShaftScreen.Reported -= OnShaft;
                YoobaPortrait.Reported -= OnYooba;
                if (bot != null)
                    Object.Destroy(bot);
                input.Dispose();
                if (fps != null)
                    Object.Destroy(fps.gameObject);
                Time.timeScale = 1f;
                SitePages.Close();
            }

            // ─── What the game can see ───────────────────────────────────

            public void CheckContent()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content != null)
                {
                    int warnings = 0;
                    foreach (var issue in content.Issues)
                    {
                        if (issue.Severity == SickOfIt.Json.JsonIssueSeverity.Error)
                            Run.Problem(PlaytestSeverity.Bug, "GameConfig error: " + issue);
                        else if (warnings++ < 10)
                            Run.Problem(PlaytestSeverity.Warning, "GameConfig warning: " + issue);
                    }
                }
                if (RemoteContent.State == RemoteContent.Status.Failed)
                    Run.Problem(PlaytestSeverity.Warning, "remote content: " + RemoteContent.Message);
            }

            static readonly Regex SoundId = new Regex("\"([^\"]+)\"");

            void OnLog(string message, string stack, LogType type)
            {
                if (Run.Done || type != LogType.Warning || !message.StartsWith("[Sfx]", StringComparison.Ordinal))
                    return;
                var match = SoundId.Match(message);
                string id = match.Success ? match.Groups[1].Value : message;
                if (message.Contains("has no clip"))
                    Run.Problem(PlaytestSeverity.Polish, $"missing sound: \"{id}\" has no clip (not generated)");
                else
                    Run.Problem(PlaytestSeverity.Warning, $"missing sound: \"{id}\" is not in audio.json");
            }

            void OnTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (Run.Done)
                    return;
                string level = "level " + log.World.LevelNumber;
                switch (e.Kind)
                {
                    case "phase":
                        Run.Event("phase", $"{level}: {e.Subject}");
                        if (stage != null && stage.Level == log.World.LevelNumber)
                            stage.Entered.Add(e.Subject);
                        break;
                    case "shortcut-offer":
                        Run.Event("shortcut-offer", $"{level}: {e.Subject} at FLOOR {e.Value}: {e.Detail}");
                        if (stage != null && stage.Level == log.World.LevelNumber)
                            stage.ShortcutOffer = e.Detail;
                        break;
                    case "shortcut-declined":
                        Run.Event("shortcut-declined", $"{level}: {e.Subject} at FLOOR {e.Value}: kept climbing");
                        if (stage != null && stage.Level == log.World.LevelNumber)
                            stage.ShortcutDeclined = e.Subject;
                        break;
                    case "shortcut":
                        Run.Event("shortcut-taken", $"{level}: {e.Subject}: {e.Detail}");
                        if (stage != null && stage.Level == log.World.LevelNumber)
                        {
                            stage.ShortcutTo = e.Value;
                            stage.ShortcutFrom = log.World.Shortcuts != null ? log.World.Shortcuts.TakenFromFloor : 0;
                        }
                        break;
                    case "section" when climb != null:
                        Run.Event("section", $"{level}: section {e.Value} \"{e.Subject}\" ({e.Detail})");
                        break;
                    case "floorkit" when climb != null:
                        Run.Event("floorkit", $"{level}: {e.Subject} ({e.Detail}) {e.Value}");
                        climb.Kit.Add(e.Subject.Split(':')[0]);
                        break;
                    case "error":
                        Run.Problem(PlaytestSeverity.Bug, $"{level} script error in {e.Subject}: {e.Detail}");
                        break;
                    case "upgrade":
                        Run.Event("upgrade", $"{level}: {e.Subject} -> {e.Value}");
                        if (portal != null && !portal.Started)
                            portal.Picked = true;
                        if (stage != null && stage.Level == log.World.LevelNumber)
                            stage.Picked = true;
                        break;
                    case "music":
                        OnMusic(log, e, level);
                        break;
                    case "milestone":
                        Run.Event("milestone", $"{level}: {e.Subject}");
                        story?.Milestones.Add((log.World.LevelNumber, e.Value));
                        break;
                    case "death" when e.Subject == MagmaKingId && magma != null:
                        CheckMagmaKingDeath(log.World.FindBySerial(e.Value));
                        break;
                    case "death" when e.Subject == "imp" && e.Detail == "Damage":
                        CheckImpDeath(log.World.FindBySerial(e.Value), level);
                        break;
                    case "floor":
                        Run.Metric("floor", e.Value);
                        if (story != null && log.World.LevelNumber == LandingLevel)
                            story.LandingFloors.Add(e.Value);
                        break;
                    case "cutscene" when scene != null || portal != null && portal.Started:
                        Run.Event("cutscene", $"{level}: {e.Subject}" + (string.IsNullOrEmpty(e.Detail) ? "" : " " + e.Detail));
                        break;
                    case "signal" when stage != null && stage.Arena && stage.Level == log.World.LevelNumber:
                    case "spawn" when stage != null && stage.Arena && stage.Level == log.World.LevelNumber:
                        ArenaTelemetry(stage, e);
                        break;
                    case "signal" when scene != null:
                        Run.Event("signal", $"{level}: {e.Subject}");
                        scene.Signals.Enqueue(e.Subject);
                        break;
                    case "signal" when portal != null && e.Subject.StartsWith("portal:", StringComparison.Ordinal):
                        Run.Event("signal", $"{level}: {e.Subject}");
                        portal.Signals.Enqueue(e.Subject);
                        break;
                    case "playerHit" when portal != null && portal.Started:
                        portal.Hits++;
                        Run.Event("hit", $"{level}: the hero was hit during the portal scene ({e.Subject})");
                        break;
                }
            }

            /// <summary>
            /// An imp was killed: its "Die" clip must be playing where it died (Entity.Die plays it before the death is
            /// recorded). In the imp-death scenario the first few play in slow motion so a recording shows the frames.
            /// </summary>
            void CheckImpDeath(Entity imp, string level)
            {
                impDeaths++;
                Run.Metric("impDeaths", impDeaths);
                if (imp == null || imp.Animation == null || !imp.Animation.TryGetClip(Entity.DieState, out var frames, out float fps)
                    || frames == null || frames.Length == 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"{level}: an imp died without a Die clip (no death frames, only the pop)");
                    return;
                }
                bool playing = false;
                foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (r.name == "Flipbook" && r.gameObject.activeInHierarchy && Array.IndexOf(frames, r.sprite) >= 0)
                    {
                        playing = true;
                        break;
                    }
                }
                float fade = imp.Def.visual.deathFade > 0f ? imp.Def.visual.deathFade : 0.25f;
                float seconds = frames.Length / Mathf.Max(0.01f, fps) + fade;
                string sheet = frames[0] != null && frames[0].texture != null ? frames[0].texture.name : "?";
                Run.Metric("impDieFrames", frames.Length);
                Run.Metric("impDieSeconds", seconds);
                Run.Event("imp-death", $"{level}: an imp died: Die clip {sheet}, {frames.Length} frames at {fps:0} fps " +
                                       $"({seconds:0.00} s with the fade){(playing ? "" : ", NOT playing")}");
                if (!playing)
                    Run.Problem(PlaytestSeverity.Bug, $"{level}: an imp died but its Die clip ({sheet}) isn't playing where it died");
                else if (impSlowMotion && impDeaths <= ImpSlowMotionDeaths && Time.timeScale > 0f)
                {
                    // Presentation pacing only (Time.timeScale), like the speed option: the simulation is untouched.
                    Time.timeScale = Speed * ImpSlowMotion;
                    slowMotionUntil = Time.realtimeSinceStartup + seconds / (Speed * ImpSlowMotion) + 0.2f;
                    Run.Event("slow-motion", $"imp death {impDeaths} at {ImpSlowMotion:0.0}x");
                }
            }

            void EndSlowMotion()
            {
                if (slowMotionUntil <= 0f)
                    return;
                slowMotionUntil = 0f;
                if (Time.timeScale > 0f)
                    Time.timeScale = Speed;
            }

            void OnLevelLog(GameplayTelemetry log, GameplayTelemetry.Summary s)
            {
                if (Run.Done)
                    return;
                string prefix = "level" + s.levelNumber;
                Run.Event("level-end", $"level {s.levelNumber}: {s.outcome} after {s.duration:0.0} s, {s.kills}/{s.spawned} kills, " +
                                       $"hp {s.playerHealthEnd}/{s.playerMaxHealth}, phase {s.lastPhase}");
                Run.Add("kills", s.kills);
                Run.Add("spawned", s.spawned);
                Run.Add("playerHits", s.playerHits);
                Run.Add("rewinds", s.rewinds);
                Run.Add("rewoundSeconds", s.rewoundSeconds);
                Run.Metric(prefix + ".seconds", s.duration);
                Run.Metric(prefix + ".kills", s.kills);
                Run.Metric(prefix + ".hp", s.playerHealthEnd);
                Run.Add(prefix + ".attempts");
                int total = 0;
                foreach (var pair in s.damageTakenBySource)
                {
                    Run.Add("damageTaken." + pair.Key, pair.Value);
                    total += pair.Value;
                }
                Run.Add("damageTaken", total);
                if (RogueliteManager.Instance != null)
                    Run.Metric("diamonds", RogueliteManager.Instance.GetPoints() - diamondsAtStart);
            }

            // ─── Scenarios ───────────────────────────────────────────────

            public IEnumerator LevelScenario(int n)
            {
                var definition = GameDefinition.Instance;
                if (n < 1 || n > definition.TotalLevels)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"there is no level {n}");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                LastLevelOutcome = null;
                Run.Event("shortcut", $"load level {n} directly");
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                // Level 1 ends in the portal scene: watch it beat by beat, then follow the game on into Video2.
                portal = n == PortalLevel ? new PortalWatch() : null;
                if (portal != null)
                    CheckPortalData(definition.GetLevel(n));
                // A floor stage: its title, lines, new enemy, breathers, then the Shaft's climb after it, each a check event.
                stageChecksFailed = 0;
                stage = portal == null && IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                // A Warden's arena also gets the magma-king watch: his animations, size, Die clip and lines.
                magma = stage != null && stage.Arena ? new MagmaWatch() : null;
                yield return PlayLevel(n, 0f, portal != null ? PortalDuring : stage != null ? StageDuring : (Func<float, IEnumerator>)null,
                    0f, stage != null ? 0.85f : 1f);
                LastLevelOutcome = lastOutcome;
                if (magma != null && !Run.Done)
                {
                    FinishMagma();
                    magma = null;
                }
                if (Run.Done)
                    yield break;
                bool portalFailed = false;
                if (portal != null)
                {
                    yield return PortalEnd();
                    portalFailed = portal.Failed > 0;
                    portal = null;
                    if (Run.Done)
                        yield break;
                }
                if (stage != null)
                {
                    FinishStage(lastOutcome);
                    // The editor's PlayMode playtests load levels themselves and stop at the results; the build follows the flow on.
                    if (lastOutcome == "completed" && LoadLevel == DefaultLoadLevel)
                        yield return StageAfter(n, StageCheck);
                    stage = null;
                    if (Run.Done)
                        yield break;
                }
                Run.Progress(1f);
                End(stageChecksFailed > 0 || portalFailed ? PlaytestOutcome.Done
                    : lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            // ─── Floor stages: what each level-n (and the story path) checks on a stage of Hell ─────

            /// <summary>
            /// What a floor stage showed while the bot played it: banners and subtitles (with their voices), the floor
            /// counter, its own carved sign, the stage's new enemy (Circle entries whose fromFloor is inside the stage)
            /// and whether it came alone, the longest stretch with nothing hostile alive, and the most enemies at once.
            /// </summary>
            sealed class StageWatch
            {
                public readonly int Level;
                public readonly Action<string, bool, string> Check;
                public readonly int Start, End;
                public readonly string Place, Sign, Door;
                public readonly List<int> Repeat = new List<int>();
                public readonly List<string> NewTypes = new List<string>();
                public readonly Dictionary<string, string> Sounds = new Dictionary<string, string>();
                /// <summary>Lines the first phase shows (the title, a milestone like Floor 950): they must come before the first enemy.</summary>
                public readonly List<string> Opening = new List<string>();
                /// <summary>When each banner or subtitle was first shown (game seconds), and who speaks each scripted line (audio.json voice).</summary>
                public readonly Dictionary<string, float> ShownAt = new Dictionary<string, float>();
                public readonly Dictionary<string, string> Speakers = new Dictionary<string, string>();
                public readonly List<int> Floors = new List<int>();
                public readonly List<(string text, bool voiced)> Banners = new List<(string, bool)>(), Lines = new List<(string, bool)>();
                public string LastBanner = "", LastLine = "";
                public int NewSerial = -1, OthersAtNewSpawn = -1, MaxEnemies, MostSameSign;
                public bool NewAlone = true, SignSeen, DoorSeen, Picked;
                /// <summary>Hell's shortcuts (Docs/Story.md, "Shortcuts"): the cost the choice showed, and where a taken one let him out (0 = not taken).</summary>
                public string ShortcutOffer = "", ShortcutDeclined = "";
                public int ShortcutTo, ShortcutFrom;
                /// <summary>The phase each scripted line belongs to, and the phases entered: a branch not taken (a shortcut's) has lines nobody hears.</summary>
                public readonly Dictionary<string, string> PhaseOf = new Dictionary<string, string>();
                public readonly HashSet<string> Entered = new HashSet<string>();
                public float FirstHostileAt = -1f, QuietSince = -1f, LongestQuiet, Clock, DoorSeenAt = -1f;

                // ─── A Warden's arena (IsArena): the boss, his fight's signals and his telegraphs ───
                public readonly bool Arena;
                /// <summary>When each of the fight's signals was first seen, and the boss's state machine as it went (game seconds).</summary>
                public readonly Dictionary<string, float> Signals = new Dictionary<string, float>();
                public readonly List<(string state, float at, float health)> BossStates = new List<(string, float, float)>();
                /// <summary>Last time each warning glow spawned; per attack: how many, the shortest lead, how many came with no warning.</summary>
                public readonly Dictionary<string, float> WarnedAt = new Dictionary<string, float>();
                public readonly Dictionary<string, (int count, float minLead, int unwarned)> Attacks = new Dictionary<string, (int, float, int)>();
                public readonly List<float> GuardSpawns = new List<float>();
                public string BossBar = "", LastBossState = "";
                public float BossBarAt = -1f, SlowMotionAt = -1f, BossFirstAt = -1f;

                public StageWatch(int level, Action<string, bool, string> check)
                {
                    Level = level;
                    Check = check;
                    Arena = IsArena(level);
                    var config = GameDefinition.Instance.GetLevel(level);
                    string display = config != null ? config.displayName ?? "" : "";
                    int colon = display.IndexOf(": ", StringComparison.Ordinal);
                    Place = colon >= 0 ? display.Substring(colon + 2) : display;
                    var content = GameContent.Current;
                    SickOfIt.Gameplay.Content.LevelScript script = null;
                    if (config != null && content != null)
                        content.LevelScripts.TryGetValue(config.levelName, out script);
                    if (script == null)
                        return;
                    Start = script.floors.start;
                    End = script.floors.end;
                    Sign = script.floors.sign ?? "";
                    Door = script.floors.door ?? "";
                    if (script.floors.repeat != null)
                        Repeat.AddRange(script.floors.repeat);
                    if (content.Circles.TryGetValue(script.floors.circle ?? "", out var circle))
                        foreach (var e in circle.enemies)
                            if (e.fromFloor <= Start && e.fromFloor >= End && !NewTypes.Contains(e.entity))
                                NewTypes.Add(e.entity);
                    // Which sound each on-screen line speaks with, so a shown line can be checked for its voice.
                    var actions = new List<SickOfIt.Gameplay.Content.GameAction>();
                    foreach (var rule in script.rules)
                        actions.AddRange(rule.@do);
                    foreach (var phase in script.phases)
                    {
                        actions.AddRange(phase.onEnter);
                        foreach (var rule in phase.rules)
                            actions.AddRange(rule.@do);
                    }
                    foreach (var phase in script.phases)
                    {
                        var own = new List<SickOfIt.Gameplay.Content.GameAction>(phase.onEnter);
                        foreach (var rule in phase.rules)
                            own.AddRange(rule.@do);
                        for (int i = 0; i < own.Count; i++)
                        {
                            if (own[i] is SickOfIt.Gameplay.Content.ShowTextAction line && !string.IsNullOrEmpty(line.text) && !PhaseOf.ContainsKey(line.text))
                                PhaseOf[line.text] = phase.name ?? "";
                            own[i]?.CollectChildren(own);
                        }
                    }
                    var spawned = new HashSet<string>();
                    var sequences = new HashSet<string>();
                    for (int i = 0; i < actions.Count; i++)
                    {
                        if (actions[i] is SickOfIt.Gameplay.Content.ShowTextAction show && !string.IsNullOrEmpty(show.text))
                            Sounds[show.text] = show.sound ?? "";
                        actions[i]?.CollectChildren(actions);
                        // An arena's lines are also the boss's own (his states, reactions and death) and his named sequences.
                        if (!Arena)
                            continue;
                        if (actions[i] is SickOfIt.Gameplay.Content.SpawnAction spawn && spawned.Add(spawn.entity)
                            && content.Entities.TryGetValue(spawn.entity, out var def) && def != null)
                            actions.AddRange(EntityActions(def));
                        if (actions[i] is SickOfIt.Gameplay.Content.RunSequenceAction run && !string.IsNullOrEmpty(run.sequence)
                            && sequences.Add(run.sequence) && content.Sequences.TryGetValue(run.sequence, out var sequence) && sequence != null)
                            actions.AddRange(sequence.steps);
                    }
                    if (script.phases.Count > 0)
                    {
                        var first = new List<SickOfIt.Gameplay.Content.GameAction>(script.phases[0].onEnter);
                        // An arena's first phase is the boss's intro: only its title must come before him.
                        if (!Arena)
                            foreach (var rule in script.phases[0].rules)
                                first.AddRange(rule.@do);
                        for (int i = 0; i < first.Count; i++)
                        {
                            if (first[i] is SickOfIt.Gameplay.Content.ShowTextAction show && !string.IsNullOrEmpty(show.text) && !Opening.Contains(show.text))
                                Opening.Add(show.text);
                            first[i]?.CollectChildren(first);
                        }
                    }
                    var audio = GameDefinition.Instance.audio;
                    if (audio != null)
                        foreach (var pair in Sounds)
                        {
                            var def = audio.sounds.Find(d => d != null && d.id == pair.Value);
                            Speakers[pair.Key] = def != null ? def.voice ?? "" : "";
                        }
                }

                static IEnumerable<SickOfIt.Gameplay.Content.GameAction> EntityActions(SickOfIt.Gameplay.Content.EntityDef def)
                {
                    foreach (var a in def.onSpawn) yield return a;
                    foreach (var a in def.onDeath) yield return a;
                    foreach (var ability in def.abilities)
                        if (ability is SickOfIt.Gameplay.Content.ReactAbility react)
                            foreach (var a in react.@do) yield return a;
                    foreach (var state in def.states)
                    {
                        foreach (var a in state.onEnter) yield return a;
                        foreach (var a in state.onExit) yield return a;
                        foreach (var ability in state.abilities)
                            if (ability is SickOfIt.Gameplay.Content.ReactAbility react)
                                foreach (var a in react.@do) yield return a;
                    }
                }
            }

            StageWatch stage;
            int stageChecksPassed, stageChecksFailed;

            static bool IsFloorStage(int n)
            {
                ShaftScreen.LevelFloors(n, out int start, out int end);
                return start > 0 && end > 0;
            }

            void StageCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    stageChecksPassed++;
                    Run.Event("check", $"stage {beat}: ok ({detail})");
                }
                else
                {
                    stageChecksFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"stage {beat}: {detail}");
                }
                Run.Metric("stage.passed", stageChecksPassed);
                Run.Metric("stage.failed", stageChecksFailed);
            }

            /// <summary>Called every frame of a floor stage (PlayLevel's 'during'): reads what is on screen, changes nothing.</summary>
            IEnumerator StageDuring(float now)
            {
                var s = stage;
                var world = GameplayWorld.Instance;
                if (s == null || world == null || world.Player == null)
                    return null;
                s.Clock = now;
                if (world.Stage != null && world.Stage.HasFloors && (s.Floors.Count == 0 || s.Floors[s.Floors.Count - 1] != world.Stage.Floor))
                    s.Floors.Add(world.Stage.Floor);

                var hud = world.Hud;
                if (hud != null)
                {
                    string banner = hud.BannerShown, line = hud.SubtitleShown;
                    if (banner.Length > 0 && banner != s.LastBanner)
                        s.Banners.Add((banner, SaidAs(s, "banner", banner)));
                    if (line.Length > 0 && line != s.LastLine)
                        s.Lines.Add((line, SaidAs(s, "line", line)));
                    s.LastBanner = banner;
                    s.LastLine = line;
                }

                // Enemies (hostile, with health) and anything hostile at all (shots, slag) for the breathers.
                int enemies = 0, hostile = 0;
                Entity fresh = null;
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || !world.Player.IsHostileTo(e))
                        continue;
                    hostile++;
                    if (e.MaxHealth <= 0)
                        continue;
                    enemies++;
                    if (s.NewSerial < 0 && s.NewTypes.Contains(e.DefId))
                        fresh = e;
                }
                s.MaxEnemies = Mathf.Max(s.MaxEnemies, enemies);
                if (fresh != null)
                {
                    s.NewSerial = fresh.Serial;
                    s.OthersAtNewSpawn = enemies - 1;
                    Run.Event("new-enemy", $"level {s.Level}: the first {fresh.DefId} arrives at {now:0.0} s with {enemies - 1} other enemies alive");
                }
                else if (s.NewSerial >= 0 && s.NewAlone)
                {
                    var first = world.FindBySerial(s.NewSerial);
                    if (first != null && first.IsAlive && enemies > 1)
                    {
                        s.NewAlone = false;
                        Run.Event("new-enemy", $"level {s.Level}: another enemy joined the first {first.DefId} at {now:0.0} s");
                    }
                }
                if (hostile > 0)
                {
                    if (s.FirstHostileAt < 0f)
                        s.FirstHostileAt = now;
                    if (s.QuietSince >= 0f && now - s.QuietSince >= 3f)
                        Run.Event("breather", $"level {s.Level}: {now - s.QuietSince:0.0} s with nothing attacking, until {now:0.0} s");
                    s.QuietSince = -1f;
                }
                else if (s.FirstHostileAt >= 0f)
                {
                    if (s.QuietSince < 0f)
                        s.QuietSince = now;
                    s.LongestQuiet = Mathf.Max(s.LongestQuiet, now - s.QuietSince);
                }

                if (s.Arena)
                    ArenaDuring(s, world, now);

                var track = world.Effects != null ? world.Effects.Track : null;
                if (track != null)
                {
                    var words = track.SignWords();
                    if (s.Sign.Length > 0 && !s.SignSeen && words.Contains(s.Sign))
                    {
                        s.SignSeen = true;
                        Run.Event("sign", $"level {s.Level}: \"{s.Sign}\" stands at the start");
                    }
                    if (s.Door.Length > 0 && !s.DoorSeen && words.Contains(s.Door))
                    {
                        s.DoorSeen = true;
                        s.DoorSeenAt = now;
                        Run.Event("door", $"level {s.Level}: the door \"{s.Door}\" stands at the top at {now:0.0} s (FLOOR {world.Stage?.Floor} on the counter)");
                    }
                    foreach (int f in s.Repeat)
                    {
                        int same = words.FindAll(w => w == f.ToString()).Count;
                        if (same > s.MostSameSign)
                        {
                            s.MostSameSign = same;
                            Run.Event("sign", $"level {s.Level}: {same} signs read {f} at once (FLOOR {world.Stage?.Floor} on the counter)");
                        }
                    }
                }
                return null;
            }

            /// <summary>
            /// A Warden's arena, every frame: the boss's state machine (with his health as it changes phase), the boss bar,
            /// and slow motion after his defeat. His animations and lines also go to the magma-king watch when it is on.
            /// </summary>
            void ArenaDuring(StageWatch s, GameplayWorld world, float now)
            {
                if (magma != null)
                    MagmaDuring(now);
                Entity boss = null;
                foreach (var e in world.Live)
                    if (e != null && e.IsAlive && e.Tags.Contains("boss"))
                    {
                        boss = e;
                        break;
                    }
                if (boss != null)
                {
                    if (s.BossFirstAt < 0f)
                        s.BossFirstAt = now;
                    string state = boss.StateName ?? "";
                    if (state.Length > 0 && state != s.LastBossState)
                    {
                        s.LastBossState = state;
                        float health = boss.MaxHealth > 0 ? (float)boss.Health / boss.MaxHealth : 0f;
                        s.BossStates.Add((state, now, health));
                        Run.Event("boss", $"level {s.Level} at {now:0.0} s: {boss.DefId} enters \"{state}\" at {health:P0} health");
                    }
                }
                string bar = world.Hud != null ? world.Hud.BossBarShown : "";
                if (bar.Length > 0 && s.BossBar.Length == 0)
                {
                    s.BossBar = bar;
                    s.BossBarAt = now;
                    Run.Event("boss", $"level {s.Level} at {now:0.0} s: the boss bar shows \"{bar}\"");
                }
                if (s.SlowMotionAt < 0f && s.Signals.ContainsKey("mkDefeat") && GameClock.Instance != null && GameClock.Instance.TimeScale < 0.9f)
                {
                    s.SlowMotionAt = now;
                    Run.Event("boss", $"level {s.Level} at {now:0.0} s: slow motion ({GameClock.Instance.TimeScale:0.00}x) as he falls");
                }
            }

            /// <summary>An arena's telemetry: the fight's signals, the warning glows and the attacks after them, his guard's spawns.</summary>
            void ArenaTelemetry(StageWatch s, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal")
                {
                    if (!s.Signals.ContainsKey(e.Subject))
                        s.Signals[e.Subject] = e.Time;
                    if (Array.Exists(ArenaSignals, x => x.signal == e.Subject))
                        Run.Event("signal", $"level {s.Level} at {e.Time:0.0} s: {e.Subject}");
                    return;
                }
                if (e.Kind != "spawn")
                    return;
                if (e.Subject == "impGuard")
                {
                    s.GuardSpawns.Add(e.Time);
                    Run.Event("adds", $"level {s.Level} at {e.Time:0.0} s: an imp guard comes down");
                    return;
                }
                foreach (var t in ArenaTelegraphs)
                {
                    if (e.Subject == t.warning)
                    {
                        // One volley spawns a row of glows at once: the first one is the warning's time.
                        if (!s.WarnedAt.TryGetValue(t.warning, out float last) || e.Time - last > 0.05f || e.Time < last)
                        {
                            s.WarnedAt[t.warning] = e.Time;
                            Run.Event("telegraph", $"level {s.Level} at {e.Time:0.0} s: {t.what} warning ({t.warning})");
                        }
                        return;
                    }
                    if (e.Subject != t.attack)
                        continue;
                    s.Attacks.TryGetValue(t.attack, out var seen);
                    bool warned = s.WarnedAt.TryGetValue(t.warning, out float at) && at <= e.Time;
                    float lead = warned ? e.Time - at : -1f;
                    // A volley (a fan of fire, the flood's column) is one attack: shots within 0.6 s of the last one belong to it.
                    if (seen.count > 0 && s.WarnedAt.ContainsKey(t.attack + "@") && e.Time - s.WarnedAt[t.attack + "@"] < 0.6f && e.Time >= s.WarnedAt[t.attack + "@"])
                        return;
                    s.WarnedAt[t.attack + "@"] = e.Time;
                    seen.count++;
                    if (!warned)
                        seen.unwarned++;
                    else
                        seen.minLead = seen.count == 1 || seen.minLead <= 0f ? lead : Mathf.Min(seen.minLead, lead);
                    s.Attacks[t.attack] = seen;
                    Run.Event("attack", $"level {s.Level} at {e.Time:0.0} s: {t.what} ({t.attack}), {(warned ? $"warned {lead:0.00} s ahead" : "NO WARNING")}");
                    return;
                }
            }

            /// <summary>A banner or subtitle just appeared: logs it with its voice (the script's sound for that text).</summary>
            bool SaidAs(StageWatch s, string kind, string text)
            {
                s.Sounds.TryGetValue(text, out string sound);
                s.Speakers.TryGetValue(text, out string speaker);
                bool voiced = !string.IsNullOrEmpty(sound) && Sfx.Has(sound);
                if (!s.ShownAt.ContainsKey(text))
                    s.ShownAt[text] = s.Clock;
                Run.Event(kind, $"level {s.Level} at {s.Clock:0.0} s: \"{text}\" ({(string.IsNullOrEmpty(sound) ? "no sound" : sound)}" +
                                $"{(string.IsNullOrEmpty(speaker) ? "" : ", " + speaker)}" +
                                $"{(string.IsNullOrEmpty(sound) ? "" : $", {Sfx.LengthOf(sound):0.0} s{(voiced ? "" : ", NO CLIP")}")})");
                return voiced;
            }

            /// <summary>The stage's checks once the bot is done with it (Docs/Story.md, "Rules for levels").</summary>
            void FinishStage(string outcome)
            {
                var s = stage;
                if (s == null)
                    return;
                bool completed = outcome == "completed";
                var check = s.Check;
                string prefix = "level" + s.Level + ".";

                string wantTitle = $"FLOORS {s.Start}-{s.End}: {s.Place.ToUpperInvariant()}";
                var title = s.Banners.Find(b => b.text == wantTitle);
                check(prefix + "title", title.text != null && title.voiced,
                    title.text != null ? $"\"{title.text}\" {(title.voiced ? "voiced by the announcer" : "shown WITHOUT its voice")}"
                        : $"no \"{wantTitle}\" banner (saw {string.Join(" | ", s.Banners.ConvertAll(b => b.text))})");
                int voicedLines = s.Lines.FindAll(l => l.voiced).Count;
                check(prefix + "heroLine", voicedLines >= 1,
                    $"{voicedLines} voiced subtitle line(s): {string.Join(" | ", s.Lines.ConvertAll(l => l.text + (l.voiced ? "" : " (no voice)")))}");

                string seen = s.Floors.Count > 0 ? string.Join(" ", s.Floors) : "none";
                Run.Event("floors", $"level {s.Level}: {seen}");
                int lowest = s.Floors.Count > 0 ? s.Floors[s.Floors.Count - 1] : int.MaxValue;
                // A shortcut taken on the stage ends it where the shortcut let him out, past the stage's own last floor.
                int wantEnd = s.ShortcutTo > 0 ? s.ShortcutTo : s.End;
                check(prefix + "floors", s.Floors.Count > 0 && s.Floors[0] == s.Start && (completed ? lowest == wantEnd : lowest >= Mathf.Min(s.End, wantEnd)),
                    $"the floor counter ran {seen} (want {s.Start} down to {s.End}{(s.ShortcutTo > 0 ? $", then the shortcut's jump to {s.ShortcutTo}" : "")}; level {outcome})");
                if (s.Sign.Length > 0)
                    check(prefix + "sign", s.SignSeen, s.SignSeen ? $"the stage's sign \"{s.Sign}\" stood at the start" : $"no \"{s.Sign}\" sign seen");

                if (s.NewTypes.Count == 0)
                    check(prefix + "newEnemy", true, "no new enemy type on this stage");
                else
                    check(prefix + "newEnemy", s.NewTypes.Count == 1 && s.NewSerial >= 0 && s.OthersAtNewSpawn == 0 && s.NewAlone,
                        s.NewTypes.Count > 1 ? $"{s.NewTypes.Count} new enemy types: {string.Join(", ", s.NewTypes)} (at most one per stage)"
                        : s.NewSerial < 0 ? $"the new enemy ({s.NewTypes[0]}) never appeared"
                        : $"the first {s.NewTypes[0]} arrived with {s.OthersAtNewSpawn} other enemies alive{(s.NewAlone ? " and stayed alone" : ", and another joined it")}");
                // A Warden's arena is one fight with the boss alive throughout: its pauses are the phase changes (FinishArena).
                if (!s.Arena)
                    check(prefix + "breather", s.LongestQuiet >= 3f, $"longest stretch with nothing attacking after the first push: {s.LongestQuiet:0.0} s (want 3+)");
                check(prefix + "maxEnemies", s.MaxEnemies <= 3, $"at most {s.MaxEnemies} enemies on screen at once (Circle X allows 3)");
                if (s.Opening.Count > 0)
                {
                    var late = s.Opening.FindAll(t => !s.ShownAt.TryGetValue(t, out float at) || s.FirstHostileAt >= 0f && at > s.FirstHostileAt);
                    check(prefix + "opening", late.Count == 0,
                        late.Count == 0 ? $"before the first enemy ({(s.FirstHostileAt >= 0f ? s.FirstHostileAt.ToString("0.0") + " s" : "none")}): " +
                                          string.Join(" | ", s.Opening.ConvertAll(t => $"\"{t}\" at {s.ShownAt[t]:0.0} s"))
                            : $"not shown before the first enemy at {s.FirstHostileAt:0.0} s: {string.Join(" | ", late)}");
                }
                if (s.Door.Length > 0)
                {
                    check(prefix + "door", s.DoorSeen, s.DoorSeen ? $"the door \"{s.Door}\" stood at the top ({s.DoorSeenAt:0.0} s)" : $"no \"{s.Door}\" door seen");
                    // Who waits behind it: lines from a voice other than the hero's and the announcer's, heard before the door.
                    var ahead = new List<string>();
                    foreach (var pair in s.ShownAt)
                        if (s.Speakers.TryGetValue(pair.Key, out string who) && who.Length > 0 && who != "player" && who != "announcer"
                            && (!s.DoorSeen || pair.Value <= s.DoorSeenAt) && s.Lines.Exists(l => l.text == pair.Key && l.voiced))
                            ahead.Add($"{who}: \"{pair.Key}\" at {pair.Value:0.0} s");
                    // Only a door someone waits behind (the stage has lines in another voice): a shortcut's stairwell has nobody.
                    bool someone = false;
                    foreach (var who in s.Speakers.Values)
                        someone |= who.Length > 0 && who != "player" && who != "announcer";
                    if (someone)
                        check(prefix + "voiceAhead", ahead.Count > 0,
                            ahead.Count > 0 ? string.Join(" | ", ahead) : "no voiced line from whoever waits behind the door before it");
                }
                if (s.Repeat.Count > 0)
                    check(prefix + "loop", s.MostSameSign >= 2, $"{s.MostSameSign} signs with the repeated floor ({string.Join(", ", s.Repeat)}) seen at once (want 2: Hell repeats it)");
                if (completed)
                {
                    if (s.Arena)
                        check(prefix + "length", s.Clock >= 60f && s.Clock <= 240f, $"the arena cleared in {s.Clock:0} s of game time (a boss fight: target 60-240)");
                    else
                        check(prefix + "length", s.Clock >= 60f && s.Clock <= 120f, $"cleared in {s.Clock:0} s of game time (target 60-120)");
                    // A shortcut is paid with a Grudge instead of picking one.
                    check(prefix + "grudge", s.Picked || s.ShortcutTo > 0, s.Picked ? "a Grudge was picked at the end"
                        : s.ShortcutTo > 0 ? "no Grudge pick: the shortcut cost one instead" : "no Grudge pick at the end");
                    var unheard = new List<string>();
                    foreach (var pair in s.Sounds)
                        if (pair.Value.Length > 0 && !s.Banners.Exists(b => b.text == pair.Key && b.voiced) && !s.Lines.Exists(l => l.text == pair.Key && l.voiced)
                            && !(s.PhaseOf.TryGetValue(pair.Key, out string phaseName) && s.Entered.Count > 0 && !s.Entered.Contains(phaseName)))
                            unheard.Add(pair.Key);
                    int scripted = s.Sounds.Count - unheard.Count;
                    check(prefix + "lines", unheard.Count == 0,
                        unheard.Count == 0 ? $"all {scripted} scripted lines shown with their voices"
                            : $"{unheard.Count} scripted line(s) not shown with their voice: {string.Join(" | ", unheard)}");
                }
                if (s.Arena)
                    FinishArena(s, completed);
            }

            /// <summary>
            /// A Warden's arena (Docs/Story.md, "The Magma King's fight"): the voiced intro and the boss bar, the three phases
            /// in order at their health, adds only in phase 2, every attack telegraphed 1+ s ahead, then his defeat in slow
            /// motion, his last line, the counter at the stage's last floor and the Waystone lighting there.
            /// </summary>
            void FinishArena(StageWatch s, bool completed)
            {
                var check = s.Check;
                string prefix = "level" + s.Level + ".";
                float At(string signal) => s.Signals.TryGetValue(signal, out float t) ? t : -1f;
                bool Voiced(string text) => s.Banners.Exists(b => b.text == text && b.voiced) || s.Lines.Exists(l => l.text == text && l.voiced);

                // The intro: the announcer calls it, he taunts, the hero answers, the boss bar comes up, then the fight.
                var introLines = new[] { "FLOOR 901! THE WARDEN OF THE WASTES!", "THE MAGMA KING",
                    "Mm-hm-hm. An audience, at last. Welcome to the Slag Throne, little spark.", "He's not getting up, is he." };
                var missing = Array.FindAll(introLines, t => !Voiced(t));
                float fight = At("mkFight");
                check(prefix + "intro", At("mkIntro") >= 0f && missing.Length == 0 && s.BossBar.Length > 0 && (fight < 0f || s.BossBarAt <= fight + 0.1f),
                    $"throne rises {(At("mkIntro") >= 0f ? $"at {At("mkIntro"):0.0} s" : "NEVER")}; " +
                    (missing.Length == 0 ? "the announcer, his taunt and the hero's line all voiced" : $"not voiced: {string.Join(" | ", missing)}") +
                    $"; boss bar {(s.BossBar.Length > 0 ? $"\"{s.BossBar}\" at {s.BossBarAt:0.0} s" : "NEVER shown")}; the fight starts at {(fight >= 0f ? fight.ToString("0.0") + " s" : "never")}");

                // The phases, in order, each at its share of his health.
                var order = new List<string>();
                float last = -1f;
                bool inOrder = true;
                foreach (var (signal, beat) in ArenaSignals)
                {
                    float t = At(signal);
                    if (t < 0f)
                    {
                        if (completed && signal != "mkFlood") inOrder = false;
                        order.Add($"{beat}: never");
                        continue;
                    }
                    if (t < last) inOrder = false;
                    last = t;
                    order.Add($"{beat} at {t:0.0} s");
                }
                check(prefix + "phases", completed ? inOrder : At("mkFight") >= 0f, string.Join("; ", order));
                float HealthAt(string prefixState)
                {
                    var hit = s.BossStates.Find(x => x.state.StartsWith(prefixState, StringComparison.Ordinal));
                    return hit.state != null ? hit.health : -1f;
                }
                float standAt = HealthAt("rising"), enrageAt = HealthAt("enraging");
                check(prefix + "enrage", completed ? standAt > 0f && standAt <= 0.5f && enrageAt > 0f && enrageAt <= 0.34f && At("mkEnrage") > At("mkStand") : true,
                    $"he stands at {(standAt >= 0f ? standAt.ToString("P0") : "never")} health (want half or less), enrages at {(enrageAt >= 0f ? enrageAt.ToString("P0") : "never")} (want a third or less)" +
                    $"; states: {string.Join(" > ", s.BossStates.ConvertAll(x => x.state))}");
                float p2 = At("mkPhase2"), p3 = At("mkEnrage");
                var stray = s.GuardSpawns.FindAll(t => p2 < 0f || t < p2 || p3 >= 0f && t > p3 + 0.05f);
                check(prefix + "adds", stray.Count == 0 && (!completed || s.GuardSpawns.Count > 0),
                    $"{s.GuardSpawns.Count} imp guard(s) came down, {stray.Count} outside phase 2 (phase 2 from {(p2 >= 0f ? p2.ToString("0.0") : "?")} s to {(p3 >= 0f ? p3.ToString("0.0") : "?")} s)");

                // Every attack after its warning.
                var attacks = new List<string>();
                bool telegraphed = true;
                foreach (var t in ArenaTelegraphs)
                {
                    if (!s.Attacks.TryGetValue(t.attack, out var a))
                    {
                        attacks.Add($"{t.what}: never");
                        if (completed) telegraphed = false;
                        continue;
                    }
                    if (a.unwarned > 0 || a.minLead < ArenaTelegraphSeconds) telegraphed = false;
                    attacks.Add($"{t.what}: {a.count}, shortest warning {a.minLead:0.00} s{(a.unwarned > 0 ? $", {a.unwarned} WITHOUT a warning" : "")}");
                }
                check(prefix + "telegraphs", telegraphed, string.Join("; ", attacks) + $" (want every one {ArenaTelegraphSeconds:0.0}+ s after its glow)");

                if (!completed)
                    return;
                // His defeat: slow motion, his last line, the announcer, the counter at the top floor, the Waystone.
                float defeat = At("mkDefeat");
                bool lastLine = Voiced("The show... must go... on...");
                check(prefix + "defeat", defeat >= 0f && s.SlowMotionAt >= 0f && lastLine && Voiced("THE MAGMA KING HAS FALLEN!"),
                    $"defeat at {(defeat >= 0f ? defeat.ToString("0.0") + " s" : "never")}; slow motion {(s.SlowMotionAt >= 0f ? $"at {s.SlowMotionAt:0.0} s" : "NOT seen")}; " +
                    $"his last line {(lastLine ? "voiced" : "NOT voiced")}; \"THE MAGMA KING HAS FALLEN!\" {(Voiced("THE MAGMA KING HAS FALLEN!") ? "voiced" : "NOT voiced")}");
                string waystone = $"WAYSTONE LIT: FLOOR {s.End}";
                bool top = s.Floors.Count > 0 && s.Floors[s.Floors.Count - 1] == s.End;
                check(prefix + "waystone", top && At("waystoneLit") > defeat && Voiced(waystone),
                    $"the counter ended on FLOOR {(s.Floors.Count > 0 ? s.Floors[s.Floors.Count - 1].ToString() : "?")} (want {s.End}); " +
                    $"\"{waystone}\" {(Voiced(waystone) ? "voiced" : "NOT voiced")} at {(At("waystoneLit") >= 0f ? At("waystoneLit").ToString("0.0") + " s" : "never")}");
            }

            /// <summary>After a cleared stage: Continue on the results, the Shaft's climb, then the next level (or video, or the level select).</summary>
            IEnumerator StageAfter(int n, Action<string, bool, string> check)
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                var left = GameplayWorld.Instance;
                yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                    30f, $"the level {n} results screen");
                if (Run.Done) yield break;
                if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                    yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                if (Run.Done) yield break;
                Run.Progress(0.9f);
                yield return WatchClimb(n, check);
                if (Run.Done) yield break;

                var config = GameDefinition.Instance.GetLevel(n);
                int nextLevel = config != null && config.nextVideoNumber <= 0 ? config.nextLevelNumber : 0;
                int nextVideo = config != null ? config.nextVideoNumber : 0;
                yield return Run.Until(() => ShaftScreen.Current == null && (Object.FindAnyObjectByType<VideoRuntime>() != null
                                                                             || Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                                                             || GameplayWorld.Instance != null && GameplayWorld.Instance != left),
                    60f, $"what comes after level {n}'s Shaft");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance != left ? GameplayWorld.Instance : null;
                bool video = Object.FindAnyObjectByType<VideoRuntime>() != null;
                if (nextLevel > 0)
                    check("level" + n + ".next", world != null && world.LevelNumber == nextLevel,
                        world != null ? $"level {world.LevelNumber} ({world.LevelName}) starts after the Shaft" : $"{SceneName()} came after the Shaft (want level {nextLevel})");
                else if (nextVideo > 0)
                    check("level" + n + ".next", video && PlayerPrefs.GetInt("CurrentVideoNumber", 0) == nextVideo,
                        video ? $"video {PlayerPrefs.GetInt("CurrentVideoNumber", 0)} plays after the Shaft" : $"{SceneName()} came after the Shaft (want video {nextVideo})");
                else
                    check("level" + n + ".next", Object.FindAnyObjectByType<LevelSelectionManager>() != null, $"{SceneName()} came after the Shaft (want the level select)");
                Run.Scene(world != null ? "Level " + world.LevelNumber : video ? "Video" : SceneName());
                yield return WaitReal(3f);
            }

            /// <summary>
            /// wastes-climb: Level ClimbFrom loaded directly, then each stage up to ClimbTo through the real flow (results,
            /// Continue, the Shaft, the next stage), each with the floor-stage checks; a stage must follow the one before
            /// with no video between, and the last one leads to the level select.
            /// </summary>
            public IEnumerator WastesClimb()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                float deadline = Time.realtimeSinceStartup + WastesClimbSeconds - 30f;
                stageChecksFailed = 0;
                Run.Event("shortcut", $"load level {ClimbFrom} directly");
                var load = LoadLevel(ClimbFrom);
                if (load != null)
                    yield return load;
                bool reachedEnd = true;
                GameplayWorld previous = null;
                for (int n = ClimbFrom; n <= ClimbTo; n++)
                {
                    if (n > ClimbFrom)
                    {
                        // Straight on from the Shaft: the next stage, not a video or the level select.
                        yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null || Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                                     || NewWorld(previous) != null && GameplayWorld.Instance.Player != null
                                                     && GameClock.Instance != null && GameClock.Instance.Time > 0f, 60f, $"level {n} to start");
                        if (Run.Done) yield break;
                        var next = NewWorld(previous);
                        StageCheck("level" + n + ".order", next != null && next.LevelNumber == n,
                            next != null ? $"level {next.LevelNumber} ({next.LevelName}) started straight after level {n - 1}'s Shaft"
                                : $"{SceneName()} came after level {n - 1}'s Shaft (want level {n})");
                        if (next == null || next.LevelNumber != n)
                        {
                            reachedEnd = false;
                            break;
                        }
                    }
                    int attempts = 0;
                    while (true)
                    {
                        attempts++;
                        float left = deadline - Time.realtimeSinceStartup;
                        if (left < 5f) { lastOutcome = "time up"; break; }
                        float from = (float)(n - ClimbFrom) / (ClimbTo - ClimbFrom + 1), to = (float)(n - ClimbFrom + 1) / (ClimbTo - ClimbFrom + 1);
                        stage = IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                        yield return PlayLevel(n, 0f, stage != null ? StageDuring : (Func<float, IEnumerator>)null, from, to, left, waitOutDeath: true);
                        if (Run.Done) yield break;
                        if (stage != null && (lastOutcome != "died" || attempts >= 3))
                            FinishStage(lastOutcome);
                        stage = null;
                        if (lastOutcome != "died" || attempts >= 3)
                            break;
                        Run.Event("retry", $"level {n}, attempt {attempts + 1}");
                        yield return ReachLevel(n);
                        if (Run.Done) yield break;
                    }
                    if (lastOutcome != "completed")
                    {
                        Run.Event("stopped", $"level {n}: {lastOutcome} after {attempts} attempt(s)");
                        reachedEnd = false;
                        break;
                    }
                    // The editor's PlayMode playtests load levels themselves and stop at the results.
                    if (LoadLevel != DefaultLoadLevel)
                        break;
                    previous = GameplayWorld.Instance;
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                    yield return WatchClimb(n, StageCheck);
                    if (Run.Done) yield break;
                }
                if (reachedEnd && LoadLevel == DefaultLoadLevel)
                {
                    yield return Run.Until(() => Object.FindAnyObjectByType<LevelSelectionManager>() != null || Object.FindAnyObjectByType<VideoRuntime>() != null
                                                 || NewWorld(previous) != null, 60f, $"what comes after level {ClimbTo}");
                    if (Run.Done) yield break;
                    bool select = Object.FindAnyObjectByType<LevelSelectionManager>() != null;
                    StageCheck("level" + ClimbTo + ".next", select, select ? $"level {ClimbTo} leads to the level select (Circle IX's stages are next to rebuild)"
                        : $"level {ClimbTo} led to {SceneName()} instead of the level select");
                    Run.Scene(select ? "Level Select" : SceneName());
                    yield return WaitReal(2f);
                }
                Run.Progress(1f);
                End(stageChecksFailed > 0 ? PlaytestOutcome.Done
                    : reachedEnd && lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            int shortcutPassed, shortcutFailed;

            void ShortcutCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    shortcutPassed++;
                    Run.Event("check", $"shortcut {beat}: ok ({detail})");
                }
                else
                {
                    shortcutFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"shortcut {beat}: {detail}");
                }
                Run.Metric("shortcut.passed", shortcutPassed);
                Run.Metric("shortcut.failed", shortcutFailed);
            }

            /// <summary>
            /// shortcut: Hell's first shortcut (Docs/Story.md, "Shortcuts"). Level ShortcutLevel loaded directly with a Grudge
            /// in hand; the bot (takeShortcuts) plays to the stairwell and presses 2 on its choice; the choice and its cost,
            /// the Grudge paid, the counter's jump, the Shaft's dotted jump and the next stage (ShortcutNextLevel, past the
            /// skipped ones) are checked, then that stage is played with the stage checks.
            /// </summary>
            public IEnumerator ShortcutRun()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                float deadline = Time.realtimeSinceStartup + ShortcutSeconds - 20f;
                stageChecksFailed = 0;
                int n = ShortcutLevel;
                ShaftScreen.LevelFloors(ShortcutNextLevel, out int nextStart, out int nextEnd);
                ShortcutCheck("levels", IsFloorStage(n) && nextStart == ShortcutToFloor - 1,
                    $"level {n} {(IsFloorStage(n) ? "is a floor stage" : "has NO floors")}; level {ShortcutNextLevel} starts on FLOOR {nextStart} (want {ShortcutToFloor - 1})");

                bool seenChoice = false;
                string costShown = "";
                int grudgesBefore = 0;
                takeShortcuts = true;
                try
                {
                    int attempts = 0;
                    while (true)
                    {
                        attempts++;
                        if (GrudgeCount() == 0)
                        {
                            foreach (var id in GameContent.Current.Upgrades.Keys)
                            {
                                UpgradeProgress.Add(id);
                                Run.Event("shortcut", $"Grudge {id} given, so the shortcut's cost can be paid");
                                break;
                            }
                        }
                        grudgesBefore = GrudgeCount();
                        Run.Event("before-shortcut", $"Grudges: {GrudgeList()}");
                        if (attempts == 1)
                        {
                            Run.Event("shortcut", $"load level {n} directly");
                            var load = LoadLevel(n);
                            if (load != null)
                                yield return load;
                        }
                        stage = new StageWatch(n, StageCheck);
                        yield return PlayLevel(n, 0f, now =>
                        {
                            var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                            if (!seenChoice && hud != null && hud.ShortcutOpen)
                            {
                                seenChoice = true;
                                costShown = hud.ShortcutCostShown;
                                int floor = GameplayWorld.Instance.Stage.Floor;
                                ShortcutCheck("choice", costShown.IndexOf("Grudge", StringComparison.OrdinalIgnoreCase) >= 0,
                                    $"the shortcut's choice is on screen at FLOOR {floor}, its cost shown: \"{costShown}\"");
                            }
                            return StageDuring(now);
                        }, 0.02f, 0.6f, Mathf.Max(30f, deadline - Time.realtimeSinceStartup - 200f), waitOutDeath: true);
                        if (Run.Done) yield break;
                        if (lastOutcome != "died" || attempts >= 3)
                            break;
                        stage = null;
                        Run.Event("retry", $"level {n}, attempt {attempts + 1}");
                        seenChoice = false;
                        yield return ReachLevel(n);
                        if (Run.Done) yield break;
                    }
                    var watched = stage;
                    if (stage != null)
                        FinishStage(lastOutcome);
                    stage = null;
                    if (lastOutcome != "completed" || watched == null)
                    {
                        ShortcutCheck("reached", false, $"level {n} ended: {lastOutcome}, before the shortcut could be taken");
                        End(lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Lost);
                        yield break;
                    }
                    if (!seenChoice)
                        ShortcutCheck("choice", false, "no shortcut choice came up on the stage");
                    ShortcutCheck("taken", watched.ShortcutTo == ShortcutToFloor,
                        watched.ShortcutTo > 0 ? $"taken: the shortcut lets him out on FLOOR {watched.ShortcutTo} (want {ShortcutToFloor}); offered as \"{watched.ShortcutOffer}\""
                            : watched.ShortcutDeclined.Length > 0 ? "the bot kept climbing instead" : "never taken");
                    int grudgesAfter = GrudgeCount();
                    ShortcutCheck("paid", grudgesAfter == grudgesBefore - 1,
                        $"Grudges {grudgesBefore} before, {grudgesAfter} after ({GrudgeList()}): the stairwell took one");
                    int last = watched.Floors.Count > 0 ? watched.Floors[watched.Floors.Count - 1] : 0;
                    ShortcutCheck("counterJump", watched.Floors.Contains(watched.End) && last == ShortcutToFloor
                                                 && !watched.Floors.Exists(f => f < watched.End && f > ShortcutToFloor),
                        $"the floor counter ran {string.Join(" ", watched.Floors)}: up to {watched.End}, then straight to {last} (want {ShortcutToFloor}, no floors between)");
                    ShortcutCheck("noPick", !watched.Picked, watched.Picked ? "a Grudge pick came after the shortcut (it costs one; there's no pick)" : "no Grudge pick after it: it cost one instead");
                    var reaction = new List<string>();
                    foreach (var pair in watched.ShownAt)
                        if (watched.PhaseOf.TryGetValue(pair.Key, out string phase) && phase == "stairwell" && watched.Speakers.TryGetValue(pair.Key, out string who))
                            reaction.Add($"{who}: \"{pair.Key}\"");
                    bool announcer = reaction.Exists(r => r.StartsWith("announcer", StringComparison.Ordinal));
                    bool hero = reaction.Exists(r => r.StartsWith("player", StringComparison.Ordinal));
                    ShortcutCheck("lines", announcer && hero, reaction.Count > 0 ? string.Join(" | ", reaction) : "no lines in the stairwell phase");

                    // The Shaft: the climb to the stage's top, then the dotted jump.
                    if (LoadLevel != DefaultLoadLevel)
                    {
                        End(shortcutFailed + stageChecksFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
                        yield break;
                    }
                    var previous = GameplayWorld.Instance;
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                    Run.Progress(0.65f);
                    yield return WatchClimb(n, ShortcutCheck);
                    if (Run.Done) yield break;
                    ShortcutCheck("saved", ShortcutProgress.All().Exists(t => t.ToFloor == ShortcutToFloor) && ShaftProgress.Floor == ShortcutToFloor,
                        $"the save: the Shaft's marker on FLOOR {ShaftProgress.Floor}, shortcuts taken: " +
                        string.Join(", ", ShortcutProgress.All().ConvertAll(t => $"{t.Id} {t.FromFloor}->{t.ToFloor}")));

                    // The next stage: the one after the skipped ones.
                    yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null || Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                                 || NewWorld(previous) != null && GameplayWorld.Instance.Player != null
                                                 && GameClock.Instance != null && GameClock.Instance.Time > 0f, 60f, $"level {ShortcutNextLevel} to start");
                    if (Run.Done) yield break;
                    var next = NewWorld(previous);
                    ShortcutCheck("nextLevel", next != null && next.LevelNumber == ShortcutNextLevel && WaystoneProgress.RunLevel == ShortcutNextLevel,
                        next != null ? $"level {next.LevelNumber} ({next.LevelName}, FLOOR {(next.Stage != null ? next.Stage.Floor : 0)}) started after the Shaft; the run is on level " +
                                       $"{WaystoneProgress.RunLevel} (want {ShortcutNextLevel}: levels {n + 1}-{ShortcutNextLevel - 1} skipped)"
                            : $"{SceneName()} came after the Shaft (want level {ShortcutNextLevel})");
                    if (next == null || next.LevelNumber != ShortcutNextLevel)
                    {
                        End(PlaytestOutcome.Done);
                        yield break;
                    }
                    Run.Scene("Level " + ShortcutNextLevel);
                    stage = new StageWatch(ShortcutNextLevel, StageCheck);
                    yield return PlayLevel(ShortcutNextLevel, 0f, StageDuring, 0.7f, 1f, Mathf.Max(30f, deadline - Time.realtimeSinceStartup), waitOutDeath: true);
                    if (Run.Done) yield break;
                    if (stage != null)
                        FinishStage(lastOutcome);
                    stage = null;
                }
                finally
                {
                    takeShortcuts = false;
                }
                Run.Progress(1f);
                End(shortcutFailed + stageChecksFailed > 0 ? PlaytestOutcome.Done
                    : lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            /// <summary>
            /// Plays Level 1 (where the imps arrive) until the bot has killed ImpSlowMotionDeaths imps and their Die
            /// clips have played, or the level ends. Every kill is checked by CheckImpDeath.
            /// </summary>
            public IEnumerator ImpDeath()
            {
                impSlowMotion = true;
                Run.Event("shortcut", $"load level {ImpLevel} directly");
                var load = LoadLevel(ImpLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(ImpLevel, 0f, until: () => impDeaths >= ImpSlowMotionDeaths && slowMotionUntil <= 0f);
                if (Run.Done)
                    yield break;
                Run.Metric("impDeaths", impDeaths);
                if (impDeaths == 0)
                    Run.Problem(PlaytestSeverity.Warning, $"imp-death: the bot killed no imp (level {lastOutcome}), so no Die clip was seen");
                Run.Progress(1f);
                End(impDeaths == 0 ? PlaytestOutcome.Done
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Won);
            }

            // ─── magma-king ───────────────────────────────────────────────

            /// <summary>What the magma-king scenario has seen of the Warden (across tries).</summary>
            sealed class MagmaWatch
            {
                public readonly Dictionary<string, float> States = new Dictionary<string, float>();
                public readonly Dictionary<string, string> Lines = new Dictionary<string, string>();
                public readonly HashSet<string> Passed = new HashSet<string>();
                public int Failed;
                public string LastState = "", LastLine = "", LastBanner = "";
                public float ScreenShare = -1f, Clock;
                public bool Seen, Art, DiePlayed;
                public string DieDetail = "he was never killed";
            }

            void MagmaCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    magma.Passed.Add(beat);
                    Run.Event("check", $"magma-king {beat}: ok ({detail})");
                }
                else
                {
                    magma.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"magma-king {beat}: {detail}");
                }
            }

            /// <summary>
            /// Plays the level with his fight (the fire rain first, then the boss phase) through real input, up to 3 tries,
            /// watching him (animation states, size on screen) and the lines; then checks what the fight must show.
            /// </summary>
            public IEnumerator MagmaKingRun()
            {
                var definition = GameDefinition.Instance;
                int n = definition.levels.FindIndex(l => l != null && l.levelName == MagmaKingLevel) + 1;
                if (n <= 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"magma-king: no level \"{MagmaKingLevel}\" in game.json");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                magma = new MagmaWatch();
                for (int attempt = 1; attempt <= 3 && !Run.Done; attempt++)
                {
                    Run.Event("shortcut", $"load level {n} ({MagmaKingLevel}) directly, try {attempt}");
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(n, 0f, MagmaDuring, (attempt - 1) / 3f, attempt / 3f);
                    if (Run.Done)
                        yield break;
                    if (lastOutcome == "completed")
                        break;
                }
                FinishMagma();
                Run.Progress(1f);
                // A failed check is a Bug problem (MagmaCheck); the outcome is how the fight went.
                End(lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Lost);
            }

            /// <summary>Every frame of the fight (PlayLevel's 'during'): reads what is on screen, changes nothing.</summary>
            IEnumerator MagmaDuring(float now)
            {
                var m = magma;
                var world = GameplayWorld.Instance;
                if (m == null || world == null)
                    return null;
                m.Clock = now;
                Entity king = null;
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.DefId == MagmaKingId)
                    {
                        king = e;
                        break;
                    }
                }
                if (king != null)
                {
                    if (!m.Seen)
                    {
                        m.Seen = true;
                        m.Art = king.Animation != null && king.Animation.HasClips && king.Sprite != null && king.Sprite.sprite != null;
                        Run.Event("boss", $"the Magma King appears at {now:0.0} s ({(m.Art ? "generated sheets" : "NO ART: placeholder")})");
                    }
                    string state = king.Animation != null ? king.Animation.CurrentState : "";
                    if (state != m.LastState && state.Length > 0)
                    {
                        m.LastState = state;
                        if (!m.States.ContainsKey(state))
                            m.States[state] = now;
                        Run.Event("anim", $"Magma King: {state} at {now:0.0} s");
                    }
                    if (m.ScreenShare < 0f && king.Sprite != null && king.Sprite.sprite != null && world.Camera != null && world.Camera.orthographic)
                    {
                        m.ScreenShare = king.Sprite.bounds.size.y / (2f * world.Camera.orthographicSize);
                        Run.Metric("magmaKing.screenShare", m.ScreenShare);
                        Run.Event("boss", $"the Magma King is {king.Sprite.bounds.size.y:0.00} units tall on screen, {m.ScreenShare:P0} of its height");
                    }
                }
                var hud = world.Hud;
                if (hud != null)
                {
                    string banner = hud.BannerShown, line = hud.SubtitleShown;
                    if (line.Length > 0 && line != m.LastLine)
                        SawMagmaLine(line, now);
                    if (banner.Length > 0 && banner != m.LastBanner)
                        SawMagmaLine(banner, now);
                    m.LastLine = line;
                    m.LastBanner = banner;
                }
                return null;
            }

            void SawMagmaLine(string text, float now)
            {
                foreach (var l in MagmaKingLines)
                {
                    if (l.text != text || magma.Lines.ContainsKey(l.beat))
                        continue;
                    var def = GameDefinition.Instance.audio?.sounds?.Find(x => x != null && x.id == l.sound);
                    string voice = def != null ? def.voice : "";
                    bool voiced = Sfx.Has(l.sound);
                    magma.Lines[l.beat] = voiced && voice == l.voice ? "" : $"{l.sound}: {(voiced ? "" : "no clip, ")}voice \"{voice}\" (want \"{l.voice}\")";
                    Run.Event("line", $"at {now:0.0} s, {voice}: \"{text}\" ({l.sound}, {Sfx.LengthOf(l.sound):0.0} s{(voiced ? "" : ", NO CLIP")})");
                }
            }

            /// <summary>He was killed: his "Die" clip (collapsing into slag, the throne cracking) must be playing where he fell.</summary>
            void CheckMagmaKingDeath(Entity king)
            {
                if (king == null || king.Animation == null || !king.Animation.TryGetClip(Entity.DieState, out var frames, out float fps) || frames.Length == 0)
                {
                    magma.DieDetail = "he died without a Die clip";
                    return;
                }
                foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (r.name == "Flipbook" && r.gameObject.activeInHierarchy && Array.IndexOf(frames, r.sprite) >= 0)
                    {
                        magma.DiePlayed = true;
                        break;
                    }
                }
                string sheet = frames[0] != null && frames[0].texture != null ? frames[0].texture.name : "?";
                magma.DieDetail = $"Die clip {sheet}, {frames.Length} frames at {fps:0} fps{(magma.DiePlayed ? "" : ", NOT playing where he fell")}";
                Run.Event("anim", $"Magma King: Die at {magma.Clock:0.0} s ({magma.DieDetail})");
            }

            void FinishMagma()
            {
                var m = magma;
                MagmaCheck("appears", m.Seen, m.Seen ? "the boss phase spawned him" : $"he never appeared (last outcome {lastOutcome})");
                MagmaCheck("art", m.Art, m.Art ? "drawn from his generated sheets" : "no generated sheets: he is a placeholder");
                MagmaCheck("size", m.ScreenShare >= 0.3f, $"{(m.ScreenShare < 0f ? "never measured" : m.ScreenShare.ToString("P0"))} of the screen's height (want 30%+)");
                foreach (string state in MagmaKingStates)
                    MagmaCheck("anim " + state, m.States.ContainsKey(state),
                        m.States.TryGetValue(state, out float at) ? $"first seen at {at:0.0} s" : "never played");
                MagmaCheck("anim Die", m.DiePlayed, m.DieDetail);
                foreach (var l in MagmaKingLines)
                    MagmaCheck("line " + l.beat, m.Lines.TryGetValue(l.beat, out string why) && why.Length == 0,
                        !m.Lines.ContainsKey(l.beat) ? $"\"{l.text}\" never shown" : why.Length == 0 ? $"\"{l.text}\" shown, voiced by {l.voice}" : why);
                Run.Metric("magmaKing.passed", m.Passed.Count);
                Run.Metric("magmaKing.failed", m.Failed);
            }

            /// <summary>
            /// The portal scene's sounds (audio.json ids; the scene itself is a later task), in the order the Prologue in
            /// Docs/Story.md plays them, then the other takes and the Meadow's lines. A sound and the next one: how long
            /// to wait after starting it (0 = its clip's length), so the portal's hum runs under the lines as it will in the scene.
            /// </summary>
            static readonly (string id, string text, float wait)[] PortalSounds =
            {
                ("overlord_beam_charge", "(the Overlord's beam charges)", 0f),
                ("vo_portal_overlord_stay", "Overlord: Mm. ... Stay.", 0f),
                ("overlord_beam_strike", "(the beam strikes the ground beside the hero)", 1.2f),
                ("ground_burn_open", "(the ground cracks and burns open)", 0f),
                ("portal_hum_loop", "(the portal hums)", 0.8f),
                ("vo_portal_hole_1", "Hero: That's a hole.", 0f),
                ("vo_portal_source_1", "Hero: So that's where they keep coming from.", 0f),
                ("vo_portal_hello_1", "Hero: Hello?", 0f),
                ("vo_portal_echo", "Hell: ...hello? ...hello? ...hello? (each repeat deeper)", 0f),
                ("vo_portal_nope_1", "Hero: Nope.", 0f),
                ("vo_portal_grabbed_1", "Hero: Oh, come ON-", 0f),
                ("portal_suck_whoosh", "(the portal sucks him in)", 0f),
                ("portal_swallow", "(whoosh + his yelp falling away)", 0f),
                ("yooba_slurp", "(Yooba is slurped in after him)", 0f),
                ("vo_portal_overlord_exhale", "Overlord: (a bored exhale) Mm.", 0f),
                ("vo_portal_hole_2", "Hero, take 2: Huh. That's a hole.", 0f),
                ("vo_portal_hole_3", "Hero, take 3: That is... a hole.", 0f),
                ("vo_portal_source_2", "Hero, take 2: Oh. So that's where they keep coming from.", 0f),
                ("vo_portal_source_3", "Hero, take 3: So that's where they keep coming from. Great.", 0f),
                ("vo_portal_hello_2", "Hero, take 2: Hellooo?", 0f),
                ("vo_portal_hello_3", "Hero, take 3: ...Hello?", 0f),
                ("vo_portal_nope_2", "Hero, take 2: Nope. Nope.", 0f),
                ("vo_portal_nope_3", "Hero, take 3: Yeah, no. Nope.", 0f),
                ("vo_portal_grabbed_2", "Hero, take 2: Oh, come on, come ON-", 0f),
                ("vo_portal_grabbed_3", "Hero, take 3: Wait wait wait- oh, come ON-", 0f),
                ("vo_meadow_title", "Announcer: Prologue! The Meadow!", 0f),
                ("vo_meadow_intro", "Hero: Nice day. Nothing could possibly go wrong.", 0f),
                ("vo_meadow_imps", "Hero: That's not a cloud. Those are not birds.", 0f),
                ("vo_meadow_laser", "Hero: Oh, he's lasering the village. Cool. Cool cool cool.", 0f),
                ("vo_meadow_leaving", "Hero: He's leaving. Through a hole in the sky. With our diamonds.", 0f),
                ("vo_meadow_clear", "Announcer: The Meadow... is TOAST!", 0f),
                ("vo_meadow_outro", "Hero: Fine. I'll go get them.", 0f),
            };

            /// <summary>
            /// portal-audio: plays every PortalSounds entry through Sfx (presentation only, nothing in the simulation), one
            /// event per sound with its clip length; a sound without a clip is a bug. The echo is vo_portal_echo, made by
            /// Tools/Audio/portal-mix.sh: the hero's "Hello?" three times at 0.84x, 0.68x and 0.52x speed and pitch.
            /// </summary>
            public IEnumerator PortalAudio()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Event("audio", $"sound {(GameSettings.SoundOn ? "on" : "off")}, page audio {PageSound.State()}, voices {GameSettings.VoiceVolume:0.##}, effects {GameSettings.SfxVolume:0.##}");
                int missing = 0;
                for (int i = 0; i < PortalSounds.Length && !Run.Done; i++)
                {
                    var (id, text, wait) = PortalSounds[i];
                    float length = Sfx.LengthOf(id);
                    Run.Progress((float)i / PortalSounds.Length);
                    if (!Sfx.Has(id))
                    {
                        missing++;
                        Run.Problem(PlaytestSeverity.Bug, $"portal-audio: \"{id}\" has no clip");
                        continue;
                    }
                    Run.Note(text);
                    Run.Event("sound", $"{id} ({length:0.0} s): {text}");
                    if (id == "vo_portal_echo")
                        Run.Event("echo", $"{id}: three repeats of the hero's Hello? at 0.84x, 0.68x and 0.52x pitch, {length:0.0} s");
                    Sfx.Play(id);
                    yield return WaitReal((wait > 0f ? wait : length) + 0.5f);
                }
                if (Run.Done) yield break;
                Run.Metric("portalSounds", PortalSounds.Length - missing);
                Run.Metric("portalSoundsMissing", missing);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            // ─── video-2: the hero's fall down the fire tunnel (a timeline overlay) ─────

            float overlayShownAt = -1f;
            float overlayHiddenAt = -1f;
            int overlayFailed;
            readonly HashSet<string> overlayPassed = new HashSet<string>();
            static readonly string[] OverlayBeats = { "appeared", "spun", "shrank", "gone", "rewound", "skipped" };

            void OnOverlayVisibility(string name, bool shown, float time)
            {
                if (name != OverlayName)
                    return;
                bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                Run.Event("overlay", $"{name} {(shown ? "shown" : "hidden")} at {time:0.00}s{(rewinding ? " (rewinding)" : "")}");
                if (rewinding)
                    return;
                if (shown && overlayShownAt < 0f)
                    overlayShownAt = time;
                if (!shown)
                    overlayHiddenAt = time;
            }

            void OverlayCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    overlayPassed.Add(beat);
                    Run.Event("check", $"video {beat}: ok ({detail})");
                }
                else
                {
                    overlayFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"video {beat}: {detail}");
                }
            }

            static float ClockTime => GameClock.Instance != null ? GameClock.Instance.Time : 0f;

            /// <summary>
            /// Loads the video directly (setup shortcut) and watches it instead of skipping: the overlay must appear at
            /// its first keyframe, spin and shrink toward the tunnel's centre and be gone after its last; R held
            /// mid-spin must turn it back; Escape must still skip the video.
            /// </summary>
            public IEnumerator VideoOverlay()
            {
                var definition = GameDefinition.Instance;
                var video = definition.TotalVideos >= OverlayVideo ? definition.GetVideo(OverlayVideo) : null;
                var overlay = video?.timeline?.overlays?.Find(o => o != null && o.name == OverlayName);
                if (overlay == null || overlay.keyframes.Count < 2)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"video-{OverlayVideo}: video {OverlayVideo} has no timeline overlay \"{OverlayName}\" with keyframes");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                float start = overlay.StartTime, end = overlay.EndTime;
                float firstRotation = overlay.keyframes[0].rotation;
                TimelineOverlayPlayer.VisibilityChanged -= OnOverlayVisibility;
                TimelineOverlayPlayer.VisibilityChanged += OnOverlayVisibility;

                Run.Event("shortcut", $"load video {OverlayVideo} directly; watch it (no Escape) to {end + 0.5f:0.0}s");
                SceneTransitionManager.Instance.LoadVideo(OverlayVideo);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {OverlayVideo} to start playing");
                if (Run.Done)
                    yield break;
                Run.Scene("Video");

                // Forward to just past the middle of the spin, watching how far he turned and how small he got.
                float maxTurned = 0f, biggest = 0f, smallest = float.MaxValue;
                Vector2 lastPosition = Vector2.zero;
                void Watch()
                {
                    if (!TimelineOverlayPlayer.TryGetPose(OverlayName, out bool visible, out var pose, out _) || !visible || pose == null)
                        return;
                    if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                        return;
                    maxTurned = Mathf.Max(maxTurned, Mathf.Abs(pose.rotation - firstRotation));
                    if (pose.alpha > 0.05f)
                    {
                        biggest = Mathf.Max(biggest, pose.scale);
                        smallest = Mathf.Min(smallest, pose.scale);
                        lastPosition = pose.position;
                    }
                }

                float rewindAt = Mathf.Lerp(start, end, 0.6f);
                Run.Note($"watching the video to {rewindAt:0.0}s");
                yield return Run.Until(() => { Watch(); return ClockTime >= rewindAt; }, rewindAt + 40f, $"video {OverlayVideo} to reach {rewindAt:0.0}s");
                if (Run.Done)
                    yield break;
                Run.Progress(0.4f);

                // Hold R: the clock runs backward and the spin with it.
                TimelineOverlayPlayer.TryGetPose(OverlayName, out _, out var before, out _);
                float clockBefore = ClockTime;
                float turnBefore = before != null ? Mathf.Abs(before.rotation - firstRotation) : 0f;
                float scaleBefore = before != null ? before.scale : 0f;
                input.SetKey(Key.R, true);
                Run.Event("input", $"hold R (rewind) at {clockBefore:0.00}s");
                float lowestClock = clockBefore, leastTurn = turnBefore, mostScale = scaleBefore;
                bool stayedVisible = true;
                float holdUntil = Time.realtimeSinceStartup + 0.45f;
                while (!Run.Done && Time.realtimeSinceStartup < holdUntil)
                {
                    yield return null;
                    lowestClock = Mathf.Min(lowestClock, ClockTime);
                    if (TimelineOverlayPlayer.TryGetPose(OverlayName, out bool visible, out var pose, out _) && pose != null)
                    {
                        stayedVisible &= visible;
                        leastTurn = Mathf.Min(leastTurn, Mathf.Abs(pose.rotation - firstRotation));
                        mostScale = Mathf.Max(mostScale, pose.scale);
                    }
                }
                input.SetKey(Key.R, false);
                yield return null;
                Run.Event("input", $"release R at {ClockTime:0.00}s");
                OverlayCheck("rewound", lowestClock < clockBefore - 0.3f && leastTurn < turnBefore - 30f && mostScale > scaleBefore,
                    $"clock went back from {clockBefore:0.00}s to {lowestClock:0.00}s; the hero turned back from {turnBefore / 360f:0.00} to " +
                    $"{leastTurn / 360f:0.00} turns and grew from {scaleBefore:0.00} to {mostScale:0.00}; on screen the whole time: {stayedVisible}");

                // Forward again, past the end of the fall.
                float watchUntil = end + 0.5f;
                Run.Note($"watching the video to {watchUntil:0.0}s");
                yield return Run.Until(() => { Watch(); return ClockTime >= watchUntil && (GameClock.Instance == null || !GameClock.Instance.IsRewinding); },
                    watchUntil + 40f, $"video {OverlayVideo} to reach {watchUntil:0.0}s");
                if (Run.Done)
                    yield break;
                Run.Progress(0.8f);

                TimelineOverlayPlayer.TryGetPose(OverlayName, out bool stillVisible, out _, out _);
                OverlayCheck("appeared", overlayShownAt >= 0f && Mathf.Abs(overlayShownAt - start) < 0.5f,
                    $"shown at {overlayShownAt:0.00}s (first keyframe {start:0.00}s, as the camera goes through the door)");
                OverlayCheck("spun", maxTurned >= 3f * 360f, $"turned {maxTurned / 360f:0.0} full turns");
                OverlayCheck("shrank", biggest > 0f && smallest < biggest * 0.15f && lastPosition.magnitude < 0.6f,
                    $"scale {biggest:0.00} down to {smallest:0.00}, last seen at ({lastPosition.x:0.00}, {lastPosition.y:0.00}) near the tunnel's centre");
                OverlayCheck("gone", !stillVisible && overlayHiddenAt >= end - 0.1f && overlayHiddenAt <= 14.8f,
                    $"hidden at {overlayHiddenAt:0.00}s (last keyframe {end:0.00}s), not on screen at {ClockTime:0.00}s");

                // Escape still skips the video.
                yield return Tap(Key.Escape);
                Run.Event("input", $"Escape (skip) at {ClockTime:0.00}s");
                float pressed = Time.realtimeSinceStartup;
                while (!Run.Done && Object.FindAnyObjectByType<VideoRuntime>() != null && ShaftScreen.Current == null && Time.realtimeSinceStartup - pressed < 20f)
                    yield return null;
                bool shaftUp = ShaftScreen.Current != null;
                bool left = Object.FindAnyObjectByType<VideoRuntime>() == null || shaftUp;
                OverlayCheck("skipped", left, !left ? "the video was still playing 20 s after Escape"
                    : shaftUp ? $"the video ended {Time.realtimeSinceStartup - pressed:0.0}s after Escape and the Shaft came up"
                    : $"the video closed {Time.realtimeSinceStartup - pressed:0.0}s after Escape ({SceneName()})");

                TimelineOverlayPlayer.VisibilityChanged -= OnOverlayVisibility;
                int passed = 0;
                foreach (var beat in OverlayBeats)
                    if (overlayPassed.Contains(beat)) passed++;
                Run.Metric("video.passed", passed);
                Run.Metric("video.failed", overlayFailed);
                Run.Progress(1f);
                End(passed == OverlayBeats.Length && overlayFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            // ─── video-2-tunnel: the fire tunnel with no caption, in sync, the same length ─────

            /// <summary>
            /// Loads the video directly (setup shortcut) and watches it with no input from the burning door through the
            /// tunnel to the lava river. Where the source used to have "COMING SOON" burned in (11.4-16.4 s), each mark
            /// captures the screen and counts caption-coloured pixels (near-black fill, pure-red outline) in the box the
            /// caption sat in; every mark also checks the frame on screen against the clock and the soundtrack's time.
            /// </summary>
            public IEnumerator VideoTunnel()
            {
                var definition = GameDefinition.Instance;
                var video = definition.TotalVideos >= OverlayVideo ? definition.GetVideo(OverlayVideo) : null;
                if (video == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"video-{OverlayVideo}-tunnel: there is no video {OverlayVideo}");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                Run.Event("shortcut", $"load video {OverlayVideo} directly; watch it (no input) to {TunnelMarks[TunnelMarks.Length - 1]:0.0}s");
                SceneTransitionManager.Instance.LoadVideo(OverlayVideo);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {OverlayVideo} to start playing");
                if (Run.Done)
                    yield break;
                Run.Scene("Video");

                SpriteFramePlayer frames = null;
                foreach (var p in Object.FindObjectsByType<SpriteFramePlayer>(FindObjectsSortMode.None))
                    if (p != null && p.IsReady && (frames == null || p.TotalFrames > frames.TotalFrames))
                        frames = p;
                AudioSource sound = null;
                foreach (var a in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (a != null && a.clip != null && a.clip == video.audio)
                        sound = a;
                if (frames == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"video {OverlayVideo}: no sprite frame player is ready");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                int failed = 0, passed = 0;
                void Check(string what, bool ok, string detail)
                {
                    if (ok) { passed++; Run.Event("check", $"video {what}: ok ({detail})"); }
                    else { failed++; Run.Problem(PlaytestSeverity.Bug, $"video {what}: {detail}"); }
                }

                float fps = frames.TotalFrames > 0 && frames.Duration > 0f ? frames.TotalFrames / frames.Duration : 0f;
                float soundLength = sound != null ? sound.clip.length : 0f;
                Run.Metric("video.seconds", frames.Duration);
                Run.Metric("video.frames", frames.TotalFrames);
                if (sound != null)
                    Check("length", Mathf.Abs(frames.Duration - soundLength) < 0.25f,
                        $"{frames.TotalFrames} frames at {fps:0.#} fps = {frames.Duration:0.00}s; soundtrack {soundLength:0.00}s");
                else
                    Run.Event("check", $"video length: {frames.Duration:0.00}s ({frames.TotalFrames} frames); no soundtrack found to compare");

                float lastAudio = -1f;
                foreach (float mark in TunnelMarks)
                {
                    yield return Run.Until(() => ClockTime >= mark, mark + 40f, $"video {OverlayVideo} to reach {mark:0.0}s");
                    if (Run.Done)
                        yield break;
                    yield return new WaitForEndOfFrame();
                    float clock = ClockTime;
                    int expected = Mathf.FloorToInt(clock * fps);
                    string shot = TunnelShot(clock);
                    Check($"frame at {clock:0.00}s", Mathf.Abs(frames.CurrentFrame - expected) <= 1,
                        $"{shot}: frame {frames.CurrentFrame}, clock says {expected}");

                    float caption = CaptionPixels(frames);
                    Run.Metric($"video.caption@{mark:0.0}", caption);
                    if (mark >= CaptionFrom && mark <= CaptionTo)
                        Check($"no caption at {clock:0.00}s", caption < 0.35f,
                            $"{caption:P0} of the old COMING SOON box is caption-coloured (the burned-in text was ~70%)");
                    else
                        Run.Event("frame", $"{shot} at {clock:0.00}s (caption box {caption:P0} dark/red)");

                    if (sound != null && sound.isPlaying && sound.time > lastAudio + 0.05f)
                        Check($"sound at {clock:0.00}s", Mathf.Abs(sound.time - clock) <= 0.15f, $"soundtrack at {sound.time:0.00}s");
                    else
                        Run.Event("check", $"video sound at {clock:0.00}s: soundtrack not running in this browser (audio {(sound != null ? sound.time : -1f):0.00}s); picture follows the clock");
                    if (sound != null)
                        lastAudio = sound.time;
                    Run.Progress(0.1f + 0.8f * mark / TunnelMarks[TunnelMarks.Length - 1]);
                }

                yield return Tap(Key.Escape);
                Run.Event("input", $"Escape (skip) at {ClockTime:0.00}s");
                Run.Metric("video.passed", passed);
                Run.Metric("video.failed", failed);
                Run.Progress(1f);
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            /// <summary>Clock times the tunnel scenario looks at: the door, the tunnel (where the caption was) and the lava river.</summary>
            static readonly float[] TunnelMarks = { 10.6f, 11.5f, 12.5f, 13.5f, 14.5f, 15.5f, 16.3f, 17.5f };
            const float CaptionFrom = 11.4f, CaptionTo = 16.4f;

            static string TunnelShot(float t) => t < 11.2f ? "burning door" : t < 16.45f ? "fire tunnel" : "lava river";

            /// <summary>
            /// Share of the screen pixels, inside the box Video2's burned-in caption covered (x 331-947, y 361-434 of a
            /// 1280x720 frame), that are caption-coloured: near-black fill or its pure-red outline. The fire tunnel has neither.
            /// </summary>
            static float CaptionPixels(SpriteFramePlayer frames)
            {
                var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
                if (camera == null || !frames.TryGetComponent(out SpriteRenderer renderer))
                    return 0f;
                var b = renderer.bounds;
                Vector3 World(float u, float v) => new Vector3(b.min.x + u * b.size.x, b.max.y - v * b.size.y, b.center.z);
                Vector3 lo = camera.WorldToScreenPoint(World(331f / 1280f, 434f / 720f));
                Vector3 hi = camera.WorldToScreenPoint(World(947f / 1280f, 361f / 720f));
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    int x0 = Mathf.Clamp(Mathf.RoundToInt(lo.x), 0, shot.width - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(hi.x), 0, shot.width - 1);
                    int y0 = Mathf.Clamp(Mathf.RoundToInt(lo.y), 0, shot.height - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(hi.y), 0, shot.height - 1);
                    if (x1 <= x0 || y1 <= y0)
                        return 0f;
                    var pixels = shot.GetPixels32();
                    int hits = 0, total = 0;
                    for (int y = y0; y <= y1; y += 2)
                    for (int x = x0; x <= x1; x += 2)
                    {
                        var c = pixels[y * shot.width + x];
                        total++;
                        if (c.r + c.g + c.b < 120 || (c.r > 150 && c.g < 70 && c.b < 70))
                            hits++;
                    }
                    return total > 0 ? (float)hits / total : 0f;
                }
                finally
                {
                    Object.Destroy(shot);
                }
            }

            // ─── level-1: the Meadow's ending, the portal scene ─────

            /// <summary>What the level-1 scenario has seen of the portal scene so far.</summary>
            sealed class PortalWatch
            {
                public readonly Queue<string> Signals = new Queue<string>();
                public readonly HashSet<string> Passed = new HashSet<string>();
                public readonly HashSet<string> LinesSeen = new HashSet<string>();
                public int Failed;
                public bool Picked;
                public bool Started;
                public bool Right;
                public PlayerHealth Health;
                public int HealthAtStart;
                public int Hits;
                public float StartX;
                public float GlowX;
                public float TelegraphAt = -1f;
                public float InputTryAt = -1f;
                public float LastAngle;
                public float Spun;
                public float BiggestScale;
                // The hero's scene clips (Curious, Shout, Grabbed): the one the scene just asked for, the distinct
                // sheet frames seen on screen for each, and frames where something else was showing.
                public string Anim;
                public readonly Dictionary<string, HashSet<string>> AnimFrames = new Dictionary<string, HashSet<string>>();
                public readonly Dictionary<string, int> AnimMisses = new Dictionary<string, int>();
                public string AnimOther;
                // The portal's and the beam's art (Assets/Art/Effects): the distinct sheet frames seen on screen per
                // "<entity>.<state>", how often the portal's Loop wrapped round, and entities drawn without art.
                public readonly Dictionary<string, HashSet<string>> FxFrames = new Dictionary<string, HashSet<string>>();
                public int LoopWraps;
                public int LastLoopFrame = -1;
                public readonly HashSet<string> FxNoArt = new HashSet<string>();
            }

            PortalWatch portal;

            /// <summary>The beats the scene must pass, in order.</summary>
            static readonly string[] PortalBeats =
            {
                "pickFirst", "locked", "telegraph", "beamCharge", "beamHit", "beamColumn", "impactBurst", "portalOpen", "inputIgnored", "portalTearOpen",
                "walkArrived", "curiousAnim", "shoutAnim", "lines", "turned", "grabbedAnim", "portalSwirl", "pulledIn", "yooba", "slurped", "portalFlare",
                "closed", "black", "untouched", "levelComplete", "video2",
            };

            /// <summary>The scene's spoken lines (Level1.json): the signal emitted with each, its subtitle and its voice.</summary>
            static readonly (string signal, string text, string sound)[] PortalLines =
            {
                ("portal:beamHit", "Mm. ... Stay.", "vo_portal_overlord_stay"),
                ("portal:line:hole", "That's a hole.", "vo_portal_hole_1"),
                ("portal:line:source", "So that's where they keep coming from.", "vo_portal_source_1"),
                ("portal:line:hello", "Hello?", "vo_portal_hello_1"),
                ("portal:line:echo", "...hello? ...hello? ...hello?", "vo_portal_echo"),
                ("portal:line:nope", "Nope.", "vo_portal_nope_1"),
                ("portal:line:grabbed", "Oh, come ON-", "vo_portal_grabbed_1"),
            };

            /// <summary>
            /// Level 1's loaded script must hold the portal scene (phase portalBeam, signal portal:locked). Without it the
            /// build is playing an old GameDefinition (the pre-scene Meadow cleared right after the Grudge pick).
            /// </summary>
            void CheckPortalData(SickOfIt.Data.Config.LevelConfig config)
            {
                string script = config != null ? config.script : null;
                bool ok = !string.IsNullOrEmpty(script) && script.Contains("\"portalBeam\"") && script.Contains("portal:locked");
                if (ok)
                {
                    Run.Event("check", "portal data: ok (Level 1's script has the portal scene)");
                    return;
                }
                portal.Failed++;
                Run.Problem(PlaytestSeverity.Bug, "portal data: the loaded Level 1 has no portal scene (no portalBeam phase): the build's " +
                                                  "GameDefinition is older than Assets/GameConfig/levels/Level1.json (look for \"Game config verified for the build\" in its log)");
            }

            void PortalCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    portal.Passed.Add(beat);
                    Run.Event("check", $"portal {beat}: ok ({detail})");
                }
                else
                {
                    portal.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"portal scene {beat}: {detail}");
                }
            }

            /// <summary>The hero showed <paramref name="state"/>'s own sheet frames, animating (several distinct frames), the whole time it was asked for.</summary>
            void AnimCheck(string beat, string state, string what, bool countMisses = true)
            {
                int frames = portal.AnimFrames.TryGetValue(state, out var seen) ? seen.Count : 0;
                portal.AnimMisses.TryGetValue(state, out int misses);
                Run.Metric($"portal.{state.ToLowerInvariant()}Frames", frames);
                bool ok = frames >= 6 && (!countMisses || misses <= 2);
                PortalCheck(beat, ok, $"{what}: {frames} different {state} frames on screen" +
                    (countMisses ? $", {misses} frames of something else{(misses > 0 ? $" (last: {portal.AnimOther})" : "")}" : ""));
            }

            static Entity FindLive(GameplayWorld world, string instanceId, string defId = null)
            {
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && (instanceId == null || e.InstanceId == instanceId) && (defId == null || e.DefId == defId))
                        return e;
                }
                return null;
            }

            /// <summary>Notes the sheet frame an effect entity shows this frame (its art, not a placeholder).</summary>
            void WatchFx(Entity e)
            {
                if (e == null)
                    return;
                var sprite = e.Sprite != null ? e.Sprite.sprite : null;
                if (e.Animation == null || !e.Animation.HasClips)
                {
                    if (portal.FxNoArt.Add(e.DefId))
                        Run.Problem(PlaytestSeverity.Bug, $"portal scene: {e.DefId} is drawn without its art (placeholder)");
                    return;
                }
                if (sprite == null)
                    return;
                string key = e.DefId + "." + e.Animation.CurrentState;
                if (!portal.FxFrames.TryGetValue(key, out var seen))
                {
                    portal.FxFrames[key] = seen = new HashSet<string>();
                    Run.Event("fx", $"{key} starts (sprite {sprite.name})");
                }
                seen.Add(sprite.name);
                if (key == "portal.Loop")
                {
                    int us = sprite.name.LastIndexOf('_');
                    if (us >= 0 && int.TryParse(sprite.name.Substring(us + 1), out int frame))
                    {
                        if (frame < portal.LastLoopFrame)
                            portal.LoopWraps++;
                        portal.LastLoopFrame = frame;
                    }
                }
            }

            int FxCount(string key)
            {
                int n = portal.FxFrames.TryGetValue(key, out var seen) ? seen.Count : 0;
                Run.Metric("portal.fx." + key, n);
                return n;
            }

            static int CountLive(GameplayWorld world, string defId)
            {
                int count = 0;
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.DefId == defId)
                        count++;
                }
                return count;
            }

            /// <summary>Runs every frame of level 1: from the scene's first signal, hands the hero to the scene and checks each beat.</summary>
            IEnumerator PortalDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || world.Player == null || portal == null)
                    return null;
                var player = world.Player;
                var controller = world.PlayerController;

                if (world.Scripted.Pulling)
                {
                    float angle = player.transform.eulerAngles.z;
                    portal.Spun += Mathf.Abs(Mathf.DeltaAngle(portal.LastAngle, angle));
                    portal.LastAngle = angle;
                    portal.BiggestScale = Mathf.Max(portal.BiggestScale, player.transform.localScale.y);
                }
                else
                {
                    portal.LastAngle = player.transform.eulerAngles.z;
                }

                if (portal.Anim != null && controller != null)
                {
                    if (!portal.AnimFrames.TryGetValue(portal.Anim, out var seen))
                        portal.AnimFrames[portal.Anim] = seen = new HashSet<string>();
                    if (controller.ShowsState(portal.Anim, out string sprite))
                    {
                        seen.Add(sprite);
                    }
                    else
                    {
                        portal.AnimMisses.TryGetValue(portal.Anim, out int misses);
                        portal.AnimMisses[portal.Anim] = misses + 1;
                        portal.AnimOther = sprite ?? "(no sprite)";
                    }
                }

                WatchFx(FindLive(world, "portal", "portal"));
                WatchFx(FindLive(world, null, "overlordBeamGlow"));
                WatchFx(FindLive(world, null, "overlordBeam"));
                WatchFx(FindLive(world, null, "overlordBeamImpact"));
                WatchFx(FindLive(world, null, "portalScorch"));

                while (portal.Signals.Count > 0)
                {
                    string signal = portal.Signals.Dequeue();
                    var hole = FindLive(world, "portal", "portal");
                    switch (signal)
                    {
                        case "portal:anim:curious":
                        case "portal:anim:shout":
                        case "portal:anim:grabbed":
                        {
                            string state = signal == "portal:anim:curious" ? "Curious" : signal == "portal:anim:shout" ? "Shout" : "Grabbed";
                            if (portal.Anim == "Curious")
                                AnimCheck("curiousAnim", "Curious", "peers into the portal");
                            portal.Anim = state;
                            Run.Event("anim", $"hero plays {state} (sprite {(controller != null && controller.ShowsState(state, out string sprite) ? sprite : "NOT a " + state + " frame")})");
                            break;
                        }
                        case "portal:line:echo":
                            // Shout has played out (1.5 s) and he's back to peering at the hole for the echo.
                            if (portal.Anim == "Shout")
                                AnimCheck("shoutAnim", "Shout", "cups a hand and calls down", countMisses: false);
                            portal.Anim = null;
                            break;
                        case "portal:line:nope":
                            portal.Anim = null;
                            break;
                        case "portal:locked":
                        {
                            portal.Started = true;
                            // The scene drives the hero now: the bot keeps its hands off the keys.
                            if (bot != null)
                                bot.enabled = false;
                            input.ReleaseAll();
                            portal.Right = player.Position.x < 0f;
                            portal.StartX = player.Position.x;
                            portal.Health = player.GetComponentInChildren<PlayerHealth>();
                            portal.HealthAtStart = portal.Health != null ? portal.Health.CurrentHealth : 0;
                            bool picker = world.Hud != null && world.Hud.PickerOpen;
                            PortalCheck("pickFirst", portal.Picked && !picker, $"the Grudge pick came first (picked={portal.Picked}, picker open={picker})");
                            bool locked = now < world.InputLockedUntil;
                            PortalCheck("locked", locked && world.RewindLocked,
                                $"input locked={locked}, rewind locked={world.RewindLocked}; hero at x={portal.StartX:0.00}, hp {portal.HealthAtStart}; no keys from the bot from here");
                            break;
                        }
                        case "portal:telegraph":
                        {
                            var glow = FindLive(world, null, "overlordBeamGlow");
                            float dx = glow != null ? glow.Position.x - player.Position.x : 0f;
                            portal.GlowX = glow != null ? glow.Position.x : float.NaN;
                            portal.TelegraphAt = now;
                            PortalCheck("telegraph", glow != null && Mathf.Abs(dx) >= 2f && Mathf.Abs(dx) <= 3f && dx > 0f == portal.Right,
                                glow != null ? $"red glow on the ground {dx:+0.00;-0.00} from the hero (x={portal.GlowX:0.00})" : "no overlordBeamGlow on the ground");
                            break;
                        }
                        case "portal:beamHit":
                        {
                            int beams = CountLive(world, "overlordBeam");
                            float warned = portal.TelegraphAt >= 0f ? now - portal.TelegraphAt : 0f;
                            int charge = FxCount("overlordBeamGlow.Charge");
                            PortalCheck("beamCharge", charge >= 6, $"the charge glow gathered on the ground through {charge} different frames");
                            PortalCheck("beamHit", beams >= 1 && warned >= 1f,
                                $"the beam came down ({beams} column) {warned:0.00} s after the glow");
                            break;
                        }
                        case "portal:open":
                        {
                            float dx = hole != null ? hole.Position.x - player.Position.x : 0f;
                            float fromGlow = hole != null ? Mathf.Abs(hole.Position.x - portal.GlowX) : 99f;
                            PortalCheck("portalOpen", hole != null && Mathf.Abs(dx) >= 2f && Mathf.Abs(dx) <= 3f && fromGlow < 0.3f,
                                hole != null ? $"portal open at x={hole.Position.x:0.00}, {dx:+0.00;-0.00} from the hero, {fromGlow:0.00} from where the beam hit" : "no portal entity");
                            portal.InputTryAt = now + 0.3f;
                            // The beam has had half a second on screen by now.
                            int column = FxCount("overlordBeam.Column");
                            PortalCheck("beamColumn", column >= 3, $"a burning beam column from the sky, {column} different frames");
                            int burst = FxCount("overlordBeamImpact.Burst");
                            PortalCheck("impactBurst", burst >= 4, $"an impact burst where it hit, {burst} different frames");
                            break;
                        }
                        case "portal:arrived":
                        {
                            int open = FxCount("portal.Open");
                            string state = hole != null && hole.Animation != null ? hole.Animation.CurrentState : "(none)";
                            PortalCheck("portalTearOpen", open >= 10 && state == "Loop",
                                $"the ground tore open through {open} different Open frames (scorch, crack, ring), now {state}");
                            float gap = hole != null ? Mathf.Abs(hole.Position.x - player.Position.x) : 99f;
                            float walked = Mathf.Abs(player.Position.x - portal.StartX);
                            bool locked = now < world.InputLockedUntil;
                            PortalCheck("walkArrived", hole != null && gap < 1.2f && walked > 0.8f && !world.Scripted.Walking && locked,
                                $"walked by himself {walked:0.00} to {gap:0.00} from the portal's centre, input locked={locked}");
                            break;
                        }
                        case "portal:curious":
                            Run.Event("portal", "\"?\" pops up over the hero");
                            break;
                        case "portal:turned":
                        {
                            bool away = controller != null && controller.FacingRight != portal.Right;
                            PortalCheck("turned", away, $"turned away from the portal (facing {(controller != null && controller.FacingRight ? "right" : "left")})");
                            break;
                        }
                        case "portal:pulledIn":
                        {
                            int swirl = FxCount("portal.Loop");
                            Run.Metric("portal.loopWraps", portal.LoopWraps);
                            string state = hole != null && hole.Animation != null ? hole.Animation.CurrentState : "(none)";
                            PortalCheck("portalSwirl", swirl >= 12 && portal.LoopWraps >= 3 && state == "Loop",
                                $"the fire swirled the whole time he was there: {swirl} different Loop frames, looped {portal.LoopWraps} times, still {state}");
                            if (portal.Anim == "Grabbed")
                                AnimCheck("grabbedAnim", "Grabbed", "flails and tumbles while he's pulled in");
                            portal.Anim = null;
                            float scale = player.transform.localScale.y;
                            float off = hole != null ? Vector2.Distance(player.Position, hole.Position) : 99f;
                            PortalCheck("pulledIn", world.Scripted.Hidden && scale < 0.01f && portal.Spun >= 600f && portal.BiggestScale > 0.05f && off < 0.3f,
                                $"spun {portal.Spun:0} degrees, shrank from {portal.BiggestScale:0.00} to {scale:0.00}, {off:0.00} from the portal, hidden={world.Scripted.Hidden}");
                            break;
                        }
                        case "portal:yooba":
                        {
                            var yooba = FindLive(world, "yooba", "yoobaProp");
                            float dx = yooba != null && hole != null ? Mathf.Abs(yooba.Position.x - hole.Position.x) : 0f;
                            PortalCheck("yooba", yooba != null && hole != null && dx > 2.5f,
                                yooba != null ? $"a green slime bounces in {dx:0.00} from the portal" : "no yoobaProp slime");
                            break;
                        }
                        case "portal:slurped":
                        {
                            bool gone = FindLive(world, "yooba") == null;
                            PortalCheck("slurped", gone && hole != null, $"the slime was slurped into the portal (gone={gone}, sound {(Sfx.Has("yooba_slurp") ? "yooba_slurp" : "MISSING")})");
                            break;
                        }
                        case "portal:closed":
                        {
                            bool scorch = FindLive(world, null, "portalScorch") != null;
                            int flare = FxCount("portal.Close");
                            int marks = FxCount("portalScorch.Scorch");
                            PortalCheck("portalFlare", flare >= 8 && marks >= 1, $"the portal flared shut through {flare} different Close frames to a scorch mark (scorch art={marks >= 1})");
                            PortalCheck("closed", hole == null && scorch, $"the portal closed (gone={hole == null}) to a scorch (scorch={scorch})");
                            break;
                        }
                        case "portal:black":
                        {
                            float alpha = world.Hud != null ? world.Hud.FadeAlpha : 0f;
                            PortalCheck("black", alpha > 0.99f, $"faded to black ({alpha:0.00})");
                            int hp = portal.Health != null ? portal.Health.CurrentHealth : 0;
                            PortalCheck("untouched", portal.Hits == 0 && hp == portal.HealthAtStart,
                                $"no damage during the scene: {portal.Hits} hits, hp {portal.HealthAtStart} -> {hp}");
                            break;
                        }
                    }

                    foreach (var line in PortalLines)
                    {
                        if (line.signal != signal)
                            continue;
                        string shown = world.Hud != null ? world.Hud.SubtitleShown : "";
                        bool voiced = Sfx.Has(line.sound);
                        Run.Event("line", $"\"{shown}\" ({line.sound}, {Sfx.LengthOf(line.sound):0.0} s{(voiced ? "" : ", NO CLIP")})");
                        if (shown == line.text && voiced)
                            portal.LinesSeen.Add(line.signal);
                        else
                            Run.Problem(PlaytestSeverity.Bug, $"portal scene line \"{line.text}\": subtitle \"{shown}\", voice {line.sound} {(voiced ? "ok" : "has no clip")}");
                        if (signal == "portal:line:hole")
                        {
                            bool facing = controller != null && hole != null && controller.FacingRight == hole.Position.x > player.Position.x;
                            if (!facing)
                                Run.Problem(PlaytestSeverity.Bug, "portal scene: the hero isn't facing the portal while he looks at it");
                        }
                        if (portal.LinesSeen.Count == PortalLines.Length)
                            PortalCheck("lines", true, $"all {PortalLines.Length} lines shown as subtitles with their voices, the echo included");
                    }
                }

                if (portal.InputTryAt >= 0f && now >= portal.InputTryAt)
                {
                    portal.InputTryAt = -1f;
                    return TryLockedInput();
                }
                return null;
            }

            /// <summary>
            /// Presses everything a player would during the scene (move away, jump, aim and fire, hold R): the hero must not
            /// move, jump or shoot, and the clock must not go backward.
            /// </summary>
            IEnumerator TryLockedInput()
            {
                var world = GameplayWorld.Instance;
                var controller = world != null ? world.PlayerController : null;
                if (world == null || controller == null || world.Player == null)
                    yield break;
                var player = world.Player;
                var clock = GameClock.Instance;
                Vector2 start = player.Position;
                float fired = controller.LastFireTime;
                float before = clock != null ? clock.Time : 0f;
                float drift = 0f, rise = 0f;
                bool rewound = false;
                Key away = portal.Right ? Key.A : Key.D;
                Run.Event("input", $"portal scene: hold {(portal.Right ? "A" : "D")}, tap Space, aim and fire with the right stick, then hold R for 0.8 s");

                void Watch()
                {
                    if (world == null || world.Player == null || world.Scripted.Walking || world.Scripted.Pulling)
                        return;
                    drift = Mathf.Max(drift, Mathf.Abs(world.Player.Position.x - start.x));
                    rise = Mathf.Max(rise, world.Player.Position.y - start.y);
                    if (clock != null && clock.IsRewinding)
                        rewound = true;
                }

                input.SetKey(away, true);
                input.SetAim(new Vector2(portal.Right ? -1f : 1f, 0.2f));
                yield return Tap(Key.Space);
                float end = Time.realtimeSinceStartup + 0.7f;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                {
                    Watch();
                    yield return null;
                }
                input.SetKey(away, false);
                input.SetAim(Vector2.zero);
                input.SetKey(Key.R, true);
                end = Time.realtimeSinceStartup + 0.8f;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                {
                    Watch();
                    yield return null;
                }
                input.SetKey(Key.R, false);
                if (Run.Done || world == null)
                    yield break;
                float after = clock != null ? clock.Time : 0f;
                bool shot = !Mathf.Approximately(controller.LastFireTime, fired);
                PortalCheck("inputIgnored", drift < 0.05f && rise < 0.05f && !shot && !rewound && after > before,
                    $"moved {drift:0.00}, rose {rise:0.00}, shot={shot}, rewound={rewound}, clock {before:0.0} -> {after:0.0} s");
            }

            /// <summary>After level 1: the scene's missing beats, then Continue on the results and Video2 must start.</summary>
            IEnumerator PortalEnd()
            {
                if (!portal.Started)
                {
                    if (lastOutcome == "completed")
                    {
                        // The scene sets the level's "cleared" counter: a clear without it is a skipped ending.
                        portal.Failed++;
                        Run.Problem(PlaytestSeverity.Bug, "portal scene: level 1 completed without the scene starting (no \"portal:locked\"); " +
                                                          "the Grudge pick must lead into it, never straight to the results");
                    }
                    else
                        Run.Problem(PlaytestSeverity.Warning, $"portal scene: the bot didn't reach it (level 1 {lastOutcome})");
                    yield break;
                }
                PortalCheck("levelComplete", lastOutcome == "completed", $"level 1 {lastOutcome} after the scene");
                if (lastOutcome == "completed")
                {
                    if (LoadLevel != DefaultLoadLevel)
                    {
                        // The PlayMode playtest loads scenes itself: the video after the level isn't part of it.
                        Run.Event("portal", "video 2 not followed (editor scene loader)");
                        portal.Passed.Add("video2");
                    }
                    else
                    {
                        var texts = GameDefinition.Instance.menuTheme.texts;
                        yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                            30f, "the level 1 results screen");
                        if (Run.Done) yield break;
                        if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                            yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                        if (Run.Done) yield break;
                        yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null, 30f, "video 2 to start");
                        if (Run.Done) yield break;
                        int video = PlayerPrefs.GetInt("CurrentVideoNumber", 0);
                        Run.Scene("Video " + video);
                        PortalCheck("video2", video == 2, $"video {video} started after the portal scene");
                        // Watch the start of the fall for the recording.
                        yield return WaitReal(4f);
                        if (Run.Done) yield break;
                    }
                }
                foreach (var beat in PortalBeats)
                {
                    if (!portal.Passed.Contains(beat))
                        Run.Problem(PlaytestSeverity.Bug, $"portal scene: never saw \"{beat}\" pass (level 1 {lastOutcome})");
                }
                int passed = 0;
                foreach (var beat in PortalBeats)
                    if (portal.Passed.Contains(beat)) passed++;
                Run.Metric("portal.passed", passed);
                Run.Metric("portal.failed", portal.Failed);
            }

            // ─── climb: the floor toolkit on a test floor ───────────────

            /// <summary>What the climb scenario has seen so far.</summary>
            sealed class ClimbWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public readonly HashSet<string> Kit = new HashSet<string>();
                public int Failed;
                public bool Started;
                public string Section = "";
                public Vector2 SectionCamera;
                public bool SawSideways;
                public readonly Dictionary<int, float> StoneSunk = new Dictionary<int, float>();
                public readonly Dictionary<int, float> CrumbleHome = new Dictionary<int, float>();
                public readonly Dictionary<int, float> RaftStart = new Dictionary<int, float>();
                public float RideStartX = float.NaN;
                public float RideRaftStart;
                public bool WasHeld;
                public float SurgeStart = float.NaN;
                public int RewindStone = -1;
                public float RewindSunk;
                public float RewindAskedAt = -1f;
                public bool ParallaxArmed;
                public long ParallaxKey;
                public Vector2 ParallaxCamera, ParallaxItem;
                public bool LandingSeen;
                public readonly HashSet<string> ArtMissing = new HashSet<string>();
            }

            ClimbWatch climb;

            /// <summary>What the climb scenario must see, in the order a good run shows them.</summary>
            static readonly string[] ClimbBeats =
            {
                "lava sea", "parallax", "climb up", "sinking stone", "rewind restores", "stone rises back", "crumbling stone", "run sideways",
                "raft drifts", "raft carries", "chain swing", "chase", "climb again", "landing number", "level ends",
            };

            /// <summary>The painted art of the climb (Docs/LevelDesign.md, "The art of the climb"): each piece on screen must be art, not its stand-in box.</summary>
            static readonly string[] ClimbArtBeats =
            {
                "art far sea", "art parallax", "art lava surface", "art ledge", "art sinkingStone", "art stone steams", "art crumblingStone",
                "art stone crumbles", "art raft", "art chain", "art chainLift", "art floorLanding",
            };

            /// <summary>The pieces whose painted art the climb checks (entity id → beat).</summary>
            static readonly Dictionary<string, string> ClimbArtPieces = new Dictionary<string, string>
            {
                { "ledge", "art ledge" }, { "sinkingStone", "art sinkingStone" }, { "crumblingStone", "art crumblingStone" },
                { "debrisRaft", "art raft" }, { "debrisRaftSign", "art raft" }, { "debrisRaftRoof", "art raft" }, { "debrisRaftHay", "art raft" },
                { "chainLift", "art chainLift" }, { "floorLanding", "art floorLanding" },
            };

            /// <summary>The art check of a piece on screen: painted (its sprite) or still the stand-in box (a problem, once).</summary>
            void ClimbArt(SickOfIt.Gameplay.Entity e, string beat)
            {
                if (climb.Passed.Contains(beat) || climb.ArtMissing.Contains(e.DefId))
                    return;
                bool standIn = e.transform.Find("Shape") != null;
                var sprite = e.Sprite != null ? e.Sprite.sprite : null;
                if (!standIn && sprite != null)
                {
                    ClimbCheck(beat, true, $"{Who(e)} is painted art ({sprite.name}, {sprite.bounds.size.x * Mathf.Abs(e.transform.lossyScale.x):0.0} units wide)");
                }
                else
                {
                    climb.ArtMissing.Add(e.DefId);
                    Run.Problem(PlaytestSeverity.Bug, $"climb {beat}: {Who(e)} is still its stand-in box (no art in its visual)");
                }
            }

            /// <summary>A frames check, reported once if it fails (it is looked at every frame).</summary>
            void ArtFrames(string beat, bool ok, string detail)
            {
                if (climb.ArtMissing.Contains(beat))
                    return;
                if (!ok)
                    climb.ArtMissing.Add(beat);
                ClimbCheck(beat, ok, detail);
            }

            static bool StripShown(SickOfIt.Gameplay.Entity e, string name)
            {
                var holder = e.transform.Find(name);
                if (holder == null)
                    return false;
                for (int i = 0; i < holder.childCount; i++)
                {
                    if (holder.GetChild(i).TryGetComponent<SpriteRenderer>(out var sr) && sr.enabled && sr.sprite != null)
                        return true;
                }
                return false;
            }

            public IEnumerator ClimbRun()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(ClimbScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"climb: there is no scene \"{ClimbScene}\" in content/scenes");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                climb = new ClimbWatch();
                Run.Event("shortcut", $"load level {ClimbLevel}'s stage with the test floor {ClimbScene} directly");
                LevelRuntime.PendingScene = ClimbScene;
                var load = LoadLevel(ClimbLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(ClimbLevel, ClimbSeconds, ClimbDuring, waitOutDeath: true);
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;

                ClimbCheck("level ends", lastOutcome == "completed", $"the level ended: {lastOutcome}");
                int passed = 0;
                foreach (var beat in ClimbBeats)
                {
                    if (climb.Passed.Contains(beat))
                        passed++;
                    else
                        Run.Problem(PlaytestSeverity.Bug, $"climb: never saw \"{beat}\" (level {lastOutcome})");
                }
                int painted = 0;
                foreach (var beat in ClimbArtBeats)
                {
                    if (climb.Passed.Contains(beat))
                        painted++;
                    else
                        Run.Problem(PlaytestSeverity.Bug, $"climb: never saw \"{beat}\": that piece is still drawn as a stand-in");
                }
                Run.Metric("climb.art", painted);
                passed += painted;
                Run.Metric("climb.passed", passed);
                Run.Metric("climb.failed", climb.Failed);
                Run.Progress(1f);
                bool ok = passed == ClimbBeats.Length + ClimbArtBeats.Length && climb.Failed == 0;
                climb = null;
                End(ok ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void ClimbCheck(string beat, bool ok, string detail)
            {
                if (climb == null || climb.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    climb.Passed.Add(beat);
                    Run.Event("check", $"climb {beat}: ok ({detail})");
                }
                else
                {
                    climb.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"climb {beat}: {detail}");
                }
            }

            static string Who(SickOfIt.Gameplay.Entity e) => string.IsNullOrEmpty(e.InstanceId) ? e.DefId + "#" + e.Serial : e.InstanceId;

            /// <summary>Runs every frame of the test floor: watches the camera's path, the toolkit's pieces and the bot, and checks each beat.</summary>
            IEnumerator ClimbDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || climb == null || world.Effects == null)
                    return null;
                var fx = world.Effects;
                var clock = GameClock.Instance;
                bool rewinding = clock != null && clock.IsRewinding;
                Vector2 cam = world.CameraCenter;
                var section = fx.CurrentSection;
                if (!climb.Started)
                {
                    climb.Started = true;
                    var names = new List<string>();
                    foreach (var sec in fx.Path)
                        names.Add($"{sec.name} ({sec.direction}{(sec.autoSpeed > 0f ? $", auto {sec.autoSpeed:0.#}/s" : "")})");
                    Run.Event("climb", fx.HasPath ? $"the floor's path: {string.Join(" → ", names)}" : "the test floor has no path");
                    if (!fx.HasPath)
                        ClimbCheck("climb up", false, "the scene has no world.path: the camera can't turn");
                }
                if (section == null || rewinding)
                    return null;

                // The lava sea and the world below it.
                var backdrop = fx.Backdrop;
                var sea = world.FindById("lavaSea");
                if (backdrop != null && sea != null && backdrop.LayerCount >= 3 && backdrop.ItemCount > 0)
                    ClimbCheck("lava sea", true, $"lava sea surface at y={sea.MainCollider.bounds.max.y:0.0} under the camera, the far sea's horizon at " +
                                                 $"y={backdrop.SeaTop:0.0}, {backdrop.LayerCount} parallax layers with {backdrop.ItemCount} shapes on screen");
                if (backdrop != null && backdrop.SeaPainted)
                    ClimbCheck("art far sea", true, $"the far sea is painted (its loop below the horizon at y={backdrop.SeaTop:0.0})");
                if (backdrop != null && backdrop.PaintedCount > 0)
                    ClimbCheck("art parallax", true, $"{backdrop.PaintedCount} painted spires, islands and smoke on screen in {backdrop.LayerCount} parallax layers");
                if (sea != null && StripShown(sea, "SurfaceArt"))
                    ClimbCheck("art lava surface", true, "the lava sea's surface is its painted loop");
                if (backdrop != null && !climb.Passed.Contains("parallax"))
                {
                    if (!climb.ParallaxArmed)
                    {
                        if (backdrop.TryGetItem(4, out var at, out var key) || backdrop.TryGetItem(2, out at, out key))
                        {
                            climb.ParallaxArmed = true;
                            climb.ParallaxKey = key;
                            climb.ParallaxCamera = cam;
                            climb.ParallaxItem = at;
                        }
                    }
                    else if (backdrop.TryGetItem(climb.ParallaxKey, out var at))
                    {
                        Vector2 camMoved = cam - climb.ParallaxCamera, itemMoved = at - climb.ParallaxItem;
                        if (camMoved.magnitude >= 3f)
                        {
                            // A far shape follows the camera only part of the way: on screen it falls behind, slower than the platforms.
                            float ratio = itemMoved.magnitude / camMoved.magnitude;
                            ClimbCheck("parallax", ratio > 0.3f && ratio < 0.97f,
                                $"camera moved {camMoved.magnitude:0.0}, a floating island moved {itemMoved.magnitude:0.0} with it (parallax {ratio:0.00}): it drifts by on screen slower than the platforms");
                        }
                    }
                    else
                    {
                        climb.ParallaxArmed = false;
                    }
                }

                // The path: up, sideways, up again.
                if (section.name != climb.Section)
                {
                    climb.Section = section.name;
                    climb.SectionCamera = cam;
                    Run.Event("climb", $"section \"{section.name}\" ({section.direction}) at t={now:0.0}, camera {cam.x:0.0},{cam.y:0.0}");
                }
                Vector2 moved = cam - climb.SectionCamera;
                if (section.direction == SickOfIt.Gameplay.Content.PathDirection.Up && moved.y >= 4f)
                {
                    if (!climb.SawSideways)
                        ClimbCheck("climb up", true, $"section \"{section.name}\": the camera climbed {moved.y:0.0} units straight up");
                    else
                        ClimbCheck("climb again", true, $"section \"{section.name}\": after the run the camera turned and climbed {moved.y:0.0} units up again");
                }
                if ((section.direction == SickOfIt.Gameplay.Content.PathDirection.Right || section.direction == SickOfIt.Gameplay.Content.PathDirection.Left)
                    && Mathf.Abs(moved.x) >= 4f && Mathf.Abs(moved.y) < 1f)
                {
                    climb.SawSideways = true;
                    ClimbCheck("run sideways", true, $"section \"{section.name}\": the camera turned and ran {Mathf.Abs(moved.x):0.0} units {section.direction.ToString().ToLowerInvariant()}");
                }

                var controller = world.PlayerController;
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive)
                        continue;
                    if (ClimbArtPieces.TryGetValue(e.DefId, out var artBeat) && e.MainCollider != null
                        && Mathf.Abs(e.Position.x - cam.x) < world.Camera.orthographicSize * world.Camera.aspect
                        && Mathf.Abs(e.Position.y - cam.y) < world.Camera.orthographicSize)
                        ClimbArt(e, artBeat);
                    if ((StripShown(e, "ChainArt")) && e.FindAbility<SickOfIt.Gameplay.Content.SwingChainAbility>() != null)
                        ClimbCheck("art chain", true, $"{Who(e)} swings on painted chain links");
                    var sink = e.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>();
                    if (sink != null && world.PlayerStandsOn(e) && sink.Sunk >= 0.3f && !e.transform.Find("Shape"))
                        ArtFrames("art stone steams", e.Animation.CurrentState == "Sink",
                            $"{Who(e)} going under plays its steaming frames (state {e.Animation.CurrentState})");
                    if (sink != null)
                    {
                        climb.StoneSunk.TryGetValue(e.Serial, out float most);
                        if (sink.Sunk > most)
                            climb.StoneSunk[e.Serial] = sink.Sunk;
                        bool on = world.PlayerStandsOn(e);
                        if (on && sink.Sunk >= 0.5f)
                            ClimbCheck("sinking stone", true, $"{Who(e)} sank {sink.Sunk:0.00} units while he stood on it");
                        if (!on && most >= 0.4f && sink.Sunk <= 0.02f && e.Serial != climb.RewindStone)
                            ClimbCheck("stone rises back", true, $"{Who(e)} sank {most:0.00} and bobbed back up once he stepped off");
                        // Rewind as a level tool: once a stone is well under, hold R and see it come back.
                        if (climb.RewindStone < 0 && on && sink.Sunk >= 0.6f && bot != null && bot.RewindNow(1.8f))
                        {
                            climb.RewindStone = e.Serial;
                            climb.RewindSunk = sink.Sunk;
                            climb.RewindAskedAt = Time.realtimeSinceStartup;
                            Run.Event("rewind", $"climb: holding R with {Who(e)} sunk {sink.Sunk:0.00} under him");
                        }
                        if (e.Serial == climb.RewindStone && climb.RewindAskedAt > 0f && !input.IsHeld(Key.R)
                            && Time.realtimeSinceStartup - climb.RewindAskedAt > 0.3f)
                        {
                            climb.RewindAskedAt = -1f;
                            ClimbCheck("rewind restores", sink.Sunk <= climb.RewindSunk - 0.4f,
                                $"{Who(e)} was {climb.RewindSunk:0.00} under; after the rewind it is {sink.Sunk:0.00} under");
                            if (!climb.Passed.Contains("rewind restores"))
                                climb.RewindStone = -2;
                        }
                    }
                    var crumble = e.FindAbility<SickOfIt.Gameplay.Content.CrumbleAbility>();
                    if (crumble != null)
                    {
                        if (!climb.CrumbleHome.ContainsKey(e.Serial) && !crumble.Falling)
                            climb.CrumbleHome[e.Serial] = e.Position.y;
                        if (crumble.Falling && !e.transform.Find("Shape"))
                            ArtFrames("art stone crumbles", e.Animation.CurrentState == "Fall",
                                $"{Who(e)} breaks up as it falls (state {e.Animation.CurrentState})");
                        if (crumble.Falling && climb.CrumbleHome.TryGetValue(e.Serial, out float home) && e.Position.y < home - 1.5f)
                            ClimbCheck("crumbling stone", true, $"{Who(e)} cracked under him, shook and fell {home - e.Position.y:0.0} units");
                    }
                    var raft = e.FindAbility<SickOfIt.Gameplay.Content.RaftAbility>();
                    if (raft != null)
                    {
                        if (!climb.RaftStart.ContainsKey(e.Serial))
                            climb.RaftStart[e.Serial] = e.Position.x;
                        if (Mathf.Abs(e.Position.x - climb.RaftStart[e.Serial]) >= 1.5f)
                            ClimbCheck("raft drifts", true, $"{Who(e)} drifted {Mathf.Abs(e.Position.x - climb.RaftStart[e.Serial]):0.0} units on the lava's current");
                        if (world.PlayerStandsOn(e))
                        {
                            if (float.IsNaN(climb.RideStartX))
                            {
                                climb.RideStartX = world.Player.Position.x;
                                climb.RideRaftStart = e.Position.x;
                            }
                            float rode = e.Position.x - climb.RideRaftStart;
                            if (Mathf.Abs(rode) >= 0.8f)
                                ClimbCheck("raft carries", true, $"he stood on {Who(e)} while it carried him {Mathf.Abs(rode):0.0} units");
                        }
                    }
                    var surge = e.FindAbility<SickOfIt.Gameplay.Content.SurgeAbility>();
                    if (surge != null)
                    {
                        if (float.IsNaN(climb.SurgeStart))
                            climb.SurgeStart = surge.X;
                        float chased = Mathf.Abs(surge.X - climb.SurgeStart);
                        if (chased >= 4f)
                            ClimbCheck("chase", true, $"the lava wave chased him {chased:0.0} units sideways (section \"{section.name}\", the hero {world.Player.Position.x - surge.X:0.0} ahead of it)");
                    }
                    var carved = e.FindAbility<SickOfIt.Gameplay.Content.CarvedAbility>();
                    if (carved != null && world.PlayerStandsOn(e) && !climb.LandingSeen)
                    {
                        climb.LandingSeen = true;
                        string floor = world.Stage != null ? world.Stage.Floor.ToString() : "";
                        ClimbCheck("landing number", carved.Carving == floor && floor.Length > 0, $"he stands on the landing carved \"{carved.Carving}\" (floor counter {floor})");
                    }
                }
                if (controller != null)
                {
                    if (controller.IsHeld)
                        climb.WasHeld = true;
                    else if (climb.WasHeld && climb.Kit.Contains("swingRelease"))
                        ClimbCheck("chain swing", true, "he grabbed the swinging chain, swung with it and jumped off");
                }
                return null;
            }

            // ─── cutscene: every scene action, checked beat by beat ─────

            /// <summary>What the cutscene scenario has seen so far.</summary>
            sealed class SceneWatch
            {
                public readonly Queue<string> Signals = new Queue<string>();
                public readonly HashSet<string> Passed = new HashSet<string>();
                public int Failed;
                public bool Done;
                public bool Started;
                public float StartX;
                public float HomeSize;
                public Vector3 HomeCamera;
                public float StopCheckAt = -1f;
                public float ArrivedX;
                public bool SawFacingLeft;
                public bool RewindTry;
                public float LastAngle;
                public float Spun;
                public float SmallestScale = float.MaxValue;
                public float BiggestScaleSeen;
            }

            SceneWatch scene;

            /// <summary>The beats the scene must reach, in order (signals it emits).</summary>
            static readonly string[] SceneBeats = { "walked", "faced", "rewindLocked", "zoomedIn", "zoomedBack", "fadedBlack", "fadedClear", "pulledIn" };

            public IEnumerator Cutscene()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(CutsceneScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"cutscene: there is no scene \"{CutsceneScene}\" in content/scenes");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                scene = new SceneWatch();
                Run.Event("shortcut", $"load level {CutsceneLevel}'s stage with the scene {CutsceneScene} directly");
                LevelRuntime.PendingScene = CutsceneScene;
                var load = LoadLevel(CutsceneLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(CutsceneLevel, 60f, SceneDuring, until: () => scene.Done);
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;

                foreach (var beat in SceneBeats)
                {
                    if (!scene.Passed.Contains(beat))
                        Run.Problem(PlaytestSeverity.Bug, $"cutscene: never saw \"{beat}\" pass (level {lastOutcome})");
                }
                int passed = 0;
                foreach (var beat in SceneBeats)
                    if (scene.Passed.Contains(beat)) passed++;
                Run.Metric("cutscene.passed", passed);
                Run.Metric("cutscene.failed", scene.Failed);
                Run.Progress(1f);
                bool ok = passed == SceneBeats.Length && scene.Failed == 0;
                scene = null;
                End(ok ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void SceneCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    scene.Passed.Add(beat);
                    Run.Event("check", $"cutscene {beat}: ok ({detail})");
                }
                else
                {
                    scene.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"cutscene {beat}: {detail}");
                }
            }

            /// <summary>Runs every frame of the scene: hands the hero to the scene, watches the pull, checks each beat.</summary>
            IEnumerator SceneDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || world.Player == null || scene == null)
                    return null;
                var player = world.Player;
                var controller = world.PlayerController;
                if (!scene.Started)
                {
                    scene.Started = true;
                    // The scene drives the hero: the bot keeps its hands off the keys (it would hold R and move).
                    if (bot != null)
                        bot.enabled = false;
                    input.ReleaseAll();
                    scene.StartX = player.Position.x;
                    scene.HomeSize = world.Effects.HomeSize;
                    scene.HomeCamera = world.Camera != null ? world.Camera.transform.position : Vector3.zero;
                    scene.LastAngle = player.transform.eulerAngles.z;
                    Run.Event("cutscene", $"hero starts at x={scene.StartX:0.00}; no keys pressed from here");
                }

                if (controller != null && !controller.FacingRight && world.Director != null && world.Director.PhaseName == "look")
                    scene.SawFacingLeft = true;
                if (world.Scripted.Pulling)
                {
                    float angle = player.transform.eulerAngles.z;
                    scene.Spun += Mathf.Abs(Mathf.DeltaAngle(scene.LastAngle, angle));
                    scene.LastAngle = angle;
                    float scale = player.transform.localScale.y;
                    scene.BiggestScaleSeen = Mathf.Max(scene.BiggestScaleSeen, scale);
                    scene.SmallestScale = Mathf.Min(scene.SmallestScale, scale);
                }
                else
                {
                    scene.LastAngle = player.transform.eulerAngles.z;
                }

                if (scene.StopCheckAt >= 0f && now >= scene.StopCheckAt)
                {
                    scene.StopCheckAt = -1f;
                    float x = player.Position.x;
                    float vx = player.Body != null ? player.Body.linearVelocity.x : 0f;
                    bool locked = now < world.InputLockedUntil;
                    SceneCheck("walked", x - scene.StartX > 2f && Mathf.Abs(x - world.Scripted.WalkTargetX) < 0.3f && Mathf.Abs(vx) < 0.05f
                                         && !world.Scripted.Walking && locked && world.RewindLocked,
                        $"walked by himself from x={scene.StartX:0.00} to x={x:0.00} (target {world.Scripted.WalkTargetX:0.00}), stopped (vx={vx:0.00}), " +
                        $"input locked={locked}, rewind locked={world.RewindLocked}");
                    scene.ArrivedX = x;
                }

                while (scene.Signals.Count > 0)
                {
                    string signal = scene.Signals.Dequeue();
                    switch (signal)
                    {
                        case "scene:arrived":
                            scene.StopCheckAt = now + 0.4f;
                            break;
                        case "scene:faced":
                        {
                            bool right = controller != null && controller.FacingRight;
                            float drift = Mathf.Abs(player.Position.x - scene.ArrivedX);
                            SceneCheck("faced", right && scene.SawFacingLeft && drift < 0.1f,
                                $"turned left then right (left seen={scene.SawFacingLeft}, now right={right}), stayed put (moved {drift:0.00})");
                            scene.RewindTry = true;
                            break;
                        }
                        case "scene:zoomed":
                        {
                            float size = world.Camera != null ? world.Camera.orthographicSize : 0f;
                            SceneCheck("zoomedIn", scene.HomeSize > 0f && size < scene.HomeSize * 0.7f,
                                $"camera size {size:0.00} (level's {scene.HomeSize:0.00}), centred near the hero");
                            break;
                        }
                        case "scene:unzoomed":
                        {
                            float size = world.Camera != null ? world.Camera.orthographicSize : 0f;
                            Vector3 at = world.Camera != null ? world.Camera.transform.position : Vector3.zero;
                            float off = Vector2.Distance(at, scene.HomeCamera);
                            SceneCheck("zoomedBack", Mathf.Abs(size - scene.HomeSize) < 0.02f && off < 0.3f,
                                $"camera size back to {size:0.00} (level's {scene.HomeSize:0.00}), {off:0.00} from its start");
                            break;
                        }
                        case "scene:black":
                        {
                            float alpha = world.Hud != null ? world.Hud.FadeAlpha : 0f;
                            SceneCheck("fadedBlack", alpha > 0.99f, $"screen covered {alpha:0.00}");
                            break;
                        }
                        case "scene:clear":
                        {
                            float alpha = world.Hud != null ? world.Hud.FadeAlpha : 1f;
                            SceneCheck("fadedClear", alpha < 0.01f, $"screen covered {alpha:0.00}");
                            break;
                        }
                        case "scene:gone":
                        {
                            float scale = player.transform.localScale.y;
                            float off = Vector2.Distance(player.Position, world.Scripted.PullPoint);
                            SceneCheck("pulledIn", world.Scripted.Hidden && scale < 0.01f && scene.Spun > 300f && scene.BiggestScaleSeen > 0.05f && off < 0.2f,
                                $"spun {scene.Spun:0} degrees, shrank from {scene.BiggestScaleSeen:0.00} to {scale:0.00}, {off:0.00} from the point, hidden={world.Scripted.Hidden}");
                            break;
                        }
                        case "scene:done":
                            scene.Done = true;
                            break;
                    }
                }

                if (scene.RewindTry)
                {
                    scene.RewindTry = false;
                    return TryLockedRewind();
                }
                return null;
            }

            /// <summary>Holds R like a player would while the scene has rewind locked: the clock must keep running forward.</summary>
            IEnumerator TryLockedRewind()
            {
                var clock = GameClock.Instance;
                var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
                float juice = budget != null ? budget.Juice : -1f;
                float before = clock != null ? clock.Time : 0f;
                Run.Event("rewind", "cutscene: hold R for 0.8 s while the scene has rewind locked");
                input.SetKey(Key.R, true);
                bool rewound = false;
                float end = Time.realtimeSinceStartup + 0.8f;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                {
                    if (clock != null && clock.IsRewinding)
                        rewound = true;
                    yield return null;
                }
                input.SetKey(Key.R, false);
                float after = clock != null ? clock.Time : 0f;
                SceneCheck("rewindLocked", !rewound && after > before && SickOfIt.Core.Rewind.RewindInput.Locked,
                    $"R held: rewound={rewound}, clock {before:0.0} -> {after:0.0} s, juice {juice:0.0} s, locked={SickOfIt.Core.Rewind.RewindInput.Locked}");
            }

            public IEnumerator Smoke()
            {
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                var texts = GameDefinition.Instance.menuTheme.texts;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Progress(0.1f);

                yield return ClickUntil(labels.newGame, () => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1), 30f);
                if (Run.Done) yield break;
                yield return ReachLevel(1);
                if (Run.Done) yield break;
                Run.Progress(0.3f);

                // Play about 30 s: rewind once at ~10 s, pause and unpause at ~20 s.
                bool rewound = false, paused = false;
                yield return PlayLevel(1, 30f, now =>
                {
                    if (!rewound && now > 10f && bot != null && bot.RewindNow(1.5f))
                    {
                        rewound = true;
                        Run.Event("rewind", "smoke: hold R for 1.5 s");
                    }
                    if (!paused && now > 20f)
                    {
                        paused = true;
                        return PauseAndResume();
                    }
                    return null;
                }, 0.3f, 0.9f);
                if (Run.Done) yield break;
                if (!rewound)
                    Run.Problem(PlaytestSeverity.Warning, "smoke: the bot never got to rewind (no juice, or the level ended first)");

                // Back out to the main menu through the pause menu.
                if (lastOutcome == "time up" && GameplayWorld.Instance != null)
                {
                    yield return Tap(Key.P);
                    yield return ClickUntil(texts.quitToMenu, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                }
                Run.Progress(1f);
                End(lastOutcome == "died" ? PlaytestOutcome.Lost : PlaytestOutcome.Done);
            }

            IEnumerator PauseAndResume()
            {
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null || hud.PickerOpen || hud.ResultsOpen)
                    yield break;
                yield return Tap(Key.P);
                yield return WaitReal(1f);
                hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null)
                    yield break;
                if (!hud.Paused)
                {
                    Run.Problem(PlaytestSeverity.Bug, "pressing P didn't pause the level");
                    yield break;
                }
                Run.Event("pause", "P");
                string done = GameDefinition.Instance.menuTheme.texts.done;
                yield return ClickUntil(GameDefinition.Instance.menuTheme.texts.settings, () => FindButton(done) != null, 10f);
                if (Run.Done) yield break;
                Run.Event("menu-open", "Settings (from pause)");
                yield return WaitReal(1f);
                yield return ClickUntil(done, () => FindButton(done) == null, 10f);
                if (Run.Done) yield break;
                Run.Event("menu-close", "Settings (from pause)");
                yield return WaitReal(0.5f);
                yield return Tap(Key.P);
                yield return WaitReal(0.5f);
                if (hud != null && hud.Paused)
                    Run.Problem(PlaytestSeverity.Bug, "pressing P again didn't resume the level");
                else
                    Run.Event("resume", "P");
            }

            public IEnumerator Full()
            {
                var definition = GameDefinition.Instance;
                var labels = definition.mainMenuConfig.labels;
                var texts = definition.menuTheme.texts;
                int levels = definition.TotalLevels;
                float deadline = Time.realtimeSinceStartup + 600f;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(labels.newGame, () => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1), 30f);
                if (Run.Done) yield break;

                int completed = 0;
                for (int n = 1; n <= levels; n++)
                {
                    int attempts = 0;
                    while (true)
                    {
                        yield return ReachLevel(n);
                        if (Run.Done) yield break;
                        attempts++;
                        float left = deadline - Time.realtimeSinceStartup;
                        if (left < 5f)
                        {
                            Run.Event("time-up", $"stopped at level {n}");
                            End(PlaytestOutcome.Done);
                            yield break;
                        }
                        float from = (float)(n - 1) / levels, to = (float)n / levels;
                        yield return PlayLevel(n, 0f, null, from, to, left, waitOutDeath: true);
                        if (Run.Done) yield break;
                        if (lastOutcome == "completed")
                            break;
                        if (lastOutcome == "died" && attempts < 3)
                        {
                            // The game's own way back: the death scene, then the level again.
                            Run.Event("retry", $"level {n}, attempt {attempts + 1}");
                            continue;
                        }
                        Run.Event("stopped", $"level {n}: {lastOutcome} after {attempts} attempt(s)");
                        Run.Metric("levelsCompleted", completed);
                        End(lastOutcome == "died" ? PlaytestOutcome.Lost
                            : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
                        yield break;
                    }
                    completed++;
                    Run.Metric("levelsCompleted", completed);
                    if (n == levels)
                        break;
                    // Results screen, then Continue (the game goes on to the next video or level).
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                }
                Run.Progress(1f);
                End(PlaytestOutcome.Won);
            }

            /// <summary>Gets from wherever the game went (video, death scene, menus) into level n.</summary>
            IEnumerator ReachLevel(int n)
            {
                float end = Time.realtimeSinceStartup + 90f;
                bool shortcut = false;
                bool dropped = false;
                float videoSeenAt = -1f;
                while (!Run.Done && !LevelRunning(n))
                {
                    var restarted = GameplayWorld.Instance;
                    if (dropped && restarted != null && restarted.LevelNumber != n && LevelRunning(restarted.LevelNumber))
                    {
                        // A death on a floor stage ended the run and it restarted at the Waystone (the waystone scenario
                        // checks that); this scenario retries level n, so it opens it directly.
                        dropped = false;
                        Run.Event("shortcut", $"the run restarted at the Waystone on level {restarted.LevelNumber}; loading level {n} directly to retry it");
                        restarted.Telemetry?.Finish("playtest left");
                        var retry = LoadLevel(n);
                        if (retry != null)
                            yield return retry;
                        yield return null;
                        continue;
                    }
                    if (Time.realtimeSinceStartup > end)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"never reached level {n} (stuck in {SceneName()})");
                        End(PlaytestOutcome.Stuck);
                        yield break;
                    }
                    if (ShaftScreen.Current != null)
                    {
                        // Hell's map between a video or level and the next: watch it, then Continue like a player.
                        if (ShaftScreen.Current.Kind == ShaftScreen.Mode.Drop)
                            dropped = true;
                        if (!ShaftScreen.Current.Continued)
                            yield return PassShaft();
                        else
                            yield return null;
                        continue;
                    }
                    bool inVideo = Object.FindAnyObjectByType<VideoRuntime>() != null;
                    bool inDeath = Object.FindAnyObjectByType<DeathRuntime>() != null;
                    if (inVideo || inDeath)
                    {
                        Run.Scene(inVideo ? "Video" : "Death");
                        if (videoSeenAt < 0f)
                            videoSeenAt = Time.realtimeSinceStartup;
                        // Watch a moment (for the recording), then skip like an impatient player.
                        if (Time.realtimeSinceStartup - videoSeenAt > 2f)
                        {
                            yield return Tap(Key.Escape);
                            Run.Event("skip", inVideo ? "video" : "death animation");
                            videoSeenAt = Time.realtimeSinceStartup + 3f;
                        }
                    }
                    else if (!shortcut && (Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                           || Object.FindAnyObjectByType<MainMenuManager>() != null))
                    {
                        // The flow ended in a menu (no next video): open the level directly.
                        shortcut = true;
                        Run.Event("shortcut", $"the game went to {SceneName()}; loading level {n} directly");
                        var load = LoadLevel(n);
                        if (load != null)
                            yield return load;
                    }
                    yield return null;
                }
            }

            // ─── story-path: New Game along the story, every video and level checked ─────

            /// <summary>What the story-path scenario has seen so far.</summary>
            sealed class StoryWatch
            {
                public readonly List<int> LandingFloors = new List<int>();
                public readonly List<(int level, int floor)> Milestones = new List<(int, int)>();
                public int Passed, Failed;
            }

            StoryWatch story;

            void StoryCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    story.Passed++;
                    Run.Event("check", $"story {beat}: ok ({detail})");
                }
                else
                {
                    story.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"story path {beat}: {detail}");
                }
                Run.Metric("story.passed", story.Passed);
                Run.Metric("story.failed", story.Failed);
            }

            public IEnumerator StoryPathRun()
            {
                var definition = GameDefinition.Instance;
                var labels = definition.mainMenuConfig.labels;
                var texts = definition.menuTheme.texts;
                story = new StoryWatch();
                float deadline = Time.realtimeSinceStartup + StoryPathSeconds - 60f;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(labels.newGame, () => Object.FindAnyObjectByType<VideoRuntime>() != null || GameplayWorld.Instance != null, 30f);
                if (Run.Done) yield break;

                bool reachedEnd = true;
                GameplayWorld previous = null; // the level just left: it lingers until the next scene loads
                for (int step = 0; step < StoryPath.Length; step++)
                {
                    var (video, n) = StoryPath[step];
                    var config = definition.GetLevel(n);

                    // The video before the level: it must be the one the story puts here, and no level may come first.
                    yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null || NewWorld(previous) != null,
                        60f, video > 0 ? $"video {video} to start" : $"level {n} to start");
                    if (Run.Done) yield break;
                    bool inVideo = Object.FindAnyObjectByType<VideoRuntime>() != null;
                    int playing = inVideo ? PlayerPrefs.GetInt("CurrentVideoNumber", 0) : 0;
                    string videoName = playing > 0 && definition.GetVideo(playing) != null ? definition.GetVideo(playing).displayName : "none";
                    if (video == 0)
                    {
                        // A stage that follows the last one directly: no video, straight from the Shaft into the level.
                        StoryCheck("level" + n + "Direct", !inVideo, inVideo ? $"video {playing} ({videoName}) played before level {n}"
                            : $"level {n} started straight after the Shaft");
                    }
                    else
                    {
                        StoryCheck("video" + video, inVideo && playing == video,
                            inVideo ? $"video {playing} ({videoName}) plays before level {n}" : $"level {GameplayWorld.Instance.LevelNumber} started instead of video {video}");
                    }
                    if (inVideo)
                    {
                        Run.Scene("Video " + playing);
                        // Watch a moment for the recording (Video3 longer: Yooba turns up), then skip like a player.
                        yield return WaitReal(video == 3 ? 8f : 2.5f);
                        if (Run.Done) yield break;
                        float skipUntil = Time.realtimeSinceStartup + 20f;
                        while (!Run.Done && Object.FindAnyObjectByType<VideoRuntime>() != null && ShaftScreen.Current == null && Time.realtimeSinceStartup < skipUntil)
                        {
                            yield return Tap(Key.Escape);
                            Run.Event("skip", $"video {playing}");
                            yield return WaitReal(2f);
                        }
                        if (Run.Done) yield break;
                        if (playing == definition.shaft.plungeAfterVideo)
                        {
                            yield return Run.Until(() => ShaftScreen.Current != null || NewWorld(previous) != null, 15f, "the Shaft after video " + playing);
                            if (Run.Done) yield break;
                            yield return WatchPlunge(StoryCheck);
                            if (Run.Done) yield break;
                        }
                        else if (ShaftScreen.Current != null)
                        {
                            yield return PassShaft();
                            if (Run.Done) yield break;
                        }
                    }

                    // The level after it: the story's level n, not an arcade level.
                    yield return Run.Until(() => NewWorld(previous) != null && GameplayWorld.Instance.Player != null
                                                 && GameClock.Instance != null && GameClock.Instance.Time > 0f, 60f, $"level {n} to start");
                    if (Run.Done) yield break;
                    var world = GameplayWorld.Instance;
                    StoryCheck("level" + n, world.LevelNumber == n && world.LevelName == config.levelName,
                        $"level {world.LevelNumber} ({world.LevelName}) started after {(video > 0 ? "video " + video : "the Shaft")}; the story wants level {n}, {config.displayName}");
                    if (world.LevelNumber != n)
                    {
                        reachedEnd = false;
                        break;
                    }

                    // Play it through, with up to 3 tries (a death goes back through the death scene to the level).
                    int attempts = 0;
                    while (true)
                    {
                        attempts++;
                        if (n == LandingLevel)
                        {
                            // The counter's first floor isn't a change, so it is read here (travel is locked at the start).
                            story.LandingFloors.Clear();
                            story.Milestones.Clear();
                            var landing = GameplayWorld.Instance;
                            if (landing != null && landing.Stage != null && landing.Stage.HasFloors)
                                story.LandingFloors.Add(landing.Stage.Floor);
                        }
                        float left = deadline - Time.realtimeSinceStartup;
                        if (left < 5f) { lastOutcome = "time up"; break; }
                        float from = (float)step / (StoryPath.Length + 1), to = (float)(step + 1) / (StoryPath.Length + 1);
                        // The Ember Wastes stages after the Landing get the stage checks (title, lines, floors, new enemy, breather).
                        stage = n > LandingLevel && IsFloorStage(n) ? new StageWatch(n, StoryCheck) : null;
                        yield return PlayLevel(n, 0f, stage != null ? StageDuring : (Func<float, IEnumerator>)null, from, to, left, waitOutDeath: true);
                        if (Run.Done) yield break;
                        if (stage != null && (lastOutcome == "completed" || lastOutcome != "died" || attempts >= 3))
                            FinishStage(lastOutcome);
                        stage = null;
                        if (lastOutcome == "completed" || lastOutcome != "died" || attempts >= 3)
                            break;
                        Run.Event("retry", $"level {n}, attempt {attempts + 1}");
                        yield return ReachLevel(n);
                        if (Run.Done) yield break;
                    }
                    if (n == LandingLevel)
                        CheckLanding(lastOutcome);
                    if (lastOutcome != "completed")
                    {
                        Run.Event("stopped", $"level {n}: {lastOutcome} after {attempts} attempt(s)");
                        reachedEnd = false;
                        break;
                    }

                    // A stage that leads straight on must name the next story level, or Continue goes elsewhere (the
                    // level select) and the wait for it below would stall. Old game data in the build does that: its
                    // Level 4 was the arcade "Everything, Everywhere" with no next level.
                    if (step + 1 < StoryPath.Length && StoryPath[step + 1].video == 0 && config.nextLevelNumber != StoryPath[step + 1].level)
                    {
                        int want = StoryPath[step + 1].level;
                        StoryCheck("level" + n + "Next", false, $"level {n} ({config.levelName}, {config.displayName}) leads to " +
                            (config.nextLevelNumber > 0 ? "level " + config.nextLevelNumber : config.nextVideoNumber > 0 ? "video " + config.nextVideoNumber : "the level select") +
                            $", not level {want}; old game data in this build? (a good build log has \"Game config verified for the build\")");
                        reachedEnd = false;
                        break;
                    }

                    // Results, then Continue: the game goes on by itself to the next video (or the level select).
                    previous = GameplayWorld.Instance;
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                    yield return WatchClimb(n, StoryCheck);
                    if (Run.Done) yield break;
                }

                if (reachedEnd)
                {
                    // The path ends after the last story level: no video, straight to the level select.
                    yield return Run.Until(() => Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                                 || Object.FindAnyObjectByType<VideoRuntime>() != null
                                                 || NewWorld(previous) != null, 60f, "the level select after the last story level");
                    if (Run.Done) yield break;
                    bool select = Object.FindAnyObjectByType<LevelSelectionManager>() != null;
                    StoryCheck("end", select, select ? $"level {StoryPath[StoryPath.Length - 1].level} leads to the level select"
                        : $"level {StoryPath[StoryPath.Length - 1].level} led to {SceneName()} instead of the level select");
                }
                if (Object.FindAnyObjectByType<LevelSelectionManager>() == null)
                {
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    yield return ClickUntil(labels.levelSelect, () => Object.FindAnyObjectByType<LevelSelectionManager>() != null, 20f);
                    if (Run.Done) yield break;
                }
                Run.Scene("Level Select");
                yield return WaitReal(2f);
                if (Run.Done) yield break;
                CheckLevelSelectOrder();
                yield return WaitReal(2f);
                Run.Progress(1f);
                End(story.Failed > 0 ? PlaytestOutcome.Done : reachedEnd ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost : PlaytestOutcome.Done);
            }

            /// <summary>The running level's world, unless it is still the one the bot just left (or there is none).</summary>
            static GameplayWorld NewWorld(GameplayWorld previous)
            {
                var world = GameplayWorld.Instance;
                return world != null && world != previous ? world : null;
            }

            /// <summary>The Landing's floor counter ran 1000 to 991 and the 999 milestone played on it.</summary>
            void CheckLanding(string outcome)
            {
                var floors = story.LandingFloors;
                string seen = floors.Count > 0 ? string.Join(" ", floors) : "none";
                Run.Event("floors", $"level {LandingLevel}: {seen}");
                int highest = int.MinValue, lowest = int.MaxValue;
                foreach (int f in floors)
                {
                    highest = Mathf.Max(highest, f);
                    lowest = Mathf.Min(lowest, f);
                }
                bool completed = outcome == "completed";
                StoryCheck("landingFloors", floors.Count > 0 && floors[0] == LandingFirstFloor && highest == LandingFirstFloor
                                            && (completed ? lowest == LandingLastFloor && floors[floors.Count - 1] == LandingLastFloor : lowest >= LandingLastFloor),
                    $"the floor counter ran {seen} (want {LandingFirstFloor} down to {LandingLastFloor}; level {outcome})");
                bool milestone = story.Milestones.Exists(m => m.level == LandingLevel && m.floor == LandingMilestone);
                StoryCheck("landing" + LandingMilestone, milestone || !completed && lowest > LandingMilestone,
                    milestone ? $"the floor {LandingMilestone} beat played" : $"no floor {LandingMilestone} beat (lowest floor {lowest})");
            }

            /// <summary>The level select's cards: the story levels come first, the Meadow then floors counting down, before any other level.</summary>
            void CheckLevelSelectOrder()
            {
                var titles = new List<string>();
                foreach (var canvas in Object.FindObjectsByType<MenuCanvas>(FindObjectsSortMode.None))
                {
                    if (canvas == null || canvas.name != "LevelSelectUI")
                        continue;
                    // Depth-first, so the cards come in their list order.
                    foreach (var text in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                    {
                        if (text.name == "Title" && text.GetComponentInParent<MenuButton>() != null)
                            titles.Add(Tags.Replace(text.text ?? "", "").Trim());
                    }
                }
                Run.Event("level-select", string.Join(" | ", titles));

                var definition = GameDefinition.Instance;
                var expected = new List<string>();
                foreach (var (_, level) in StoryPath)
                {
                    var config = definition.GetLevel(level);
                    expected.Add(config != null ? config.displayName : "?");
                }
                bool firstInOrder = titles.Count >= expected.Count;
                for (int i = 0; firstInOrder && i < expected.Count; i++)
                    firstInOrder = titles[i] == expected[i];
                StoryCheck("levelSelect", firstInOrder,
                    $"the first cards are {string.Join(" | ", titles.GetRange(0, Mathf.Min(titles.Count, expected.Count)))} (want {string.Join(" | ", expected)})");

                // Every story stage ("Floors N-M: ...") after the Meadow counts down, and none comes after an arcade level.
                var floorsTitle = new Regex(@"^Floors (\d+)-(\d+)");
                int last = int.MaxValue;
                bool arcadeSeen = false, ordered = true;
                string why = "story levels first, floors counting down";
                foreach (string title in titles)
                {
                    var match = floorsTitle.Match(title);
                    if (!match.Success)
                    {
                        if (title != expected[0])
                            arcadeSeen = true;
                        continue;
                    }
                    int start = int.Parse(match.Groups[1].Value);
                    if (arcadeSeen || start >= last)
                    {
                        ordered = false;
                        why = $"\"{title}\" is out of order";
                        break;
                    }
                    last = start;
                }
                StoryCheck("levelSelectStory", ordered, why);
            }

            // ─── The Shaft: Hell's map between videos and levels ──────────

            /// <summary>What one Shaft showed, from its open to its Continue.</summary>
            sealed class ShaftSeen
            {
                public ShaftScreen.Mode Kind;
                public int From, To, Shown, Bands, BandCount;
                public bool Skippable, Skipped, Slam;
                public string FloorsLeft = "", NextWarden = "", Circle = "", Wakes = "", WaystoneLit = "";
                public readonly List<string> Voices = new List<string>();
            }

            ShaftSeen shaftWatch;
            int shaftPassed, shaftFailed;

            void OnShaft(string kind, string detail)
            {
                if (Run.Done)
                    return;
                Run.Event("shaft", $"{kind}: {detail}");
                if (kind == "music")
                    shaftMusic.Add(detail);
                if (shaftWatch == null)
                    return;
                if (kind == "voice")
                    shaftWatch.Voices.Add(detail);
                else if (kind == "skip")
                    shaftWatch.Skipped = true;
                else if (kind == "slam")
                    shaftWatch.Slam = true;
                else if (kind == "band-wakes")
                    shaftWatch.Wakes = detail;
                else if (kind == "waystone-lit")
                    shaftWatch.WaystoneLit = detail;
            }

            void ShaftCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    shaftPassed++;
                    Run.Event("check", $"shaft {beat}: ok ({detail})");
                }
                else
                {
                    shaftFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"shaft {beat}: {detail}");
                }
                Run.Metric("shaft.passed", shaftPassed);
                Run.Metric("shaft.failed", shaftFailed);
            }

            /// <summary>
            /// Watches the Shaft on screen until its Continue is up, optionally checks its layout, then presses Continue
            /// with the virtual mouse. 'pressMidway' presses Space during the animation (the first Shaft must not skip).
            /// </summary>
            IEnumerator PassShaft(bool pressMidway = false, string audit = null)
            {
                var screen = ShaftScreen.Current;
                if (screen == null || screen.Continued)
                    yield break;
                Run.Scene("Shaft");
                var seen = shaftWatch = new ShaftSeen
                {
                    Kind = screen.Kind, From = screen.FromFloor, To = screen.ToFloor, Skippable = screen.Skippable, BandCount = screen.BandCount,
                };
                if (pressMidway)
                {
                    yield return WaitReal(1.6f);
                    if (Run.Done) yield break;
                    if (ShaftScreen.Current == screen && !screen.Ready)
                    {
                        yield return Tap(Key.Space);
                        Run.Event("input", $"Space during the Shaft at FLOOR {screen.ShownFloor}");
                    }
                }
                yield return Run.Until(() => ShaftScreen.Current != screen || screen.Ready, 75f, "the Shaft's Continue (Yooba may be talking)");
                if (Run.Done) yield break;
                if (ShaftScreen.Current == screen)
                {
                    seen.Shown = screen.ShownFloor;
                    seen.Bands = screen.BandsPassed;
                    seen.FloorsLeft = screen.FloorsLeftText;
                    seen.NextWarden = screen.NextWardenText;
                    seen.Circle = screen.CircleText;
                    Run.Event("shaft-shown", $"{seen.Kind} {seen.From}->{seen.To}: counter FLOOR {seen.Shown}, {seen.Circle}, {seen.FloorsLeft}, {seen.NextWarden}");
                    // A moment on the finished map for the recording.
                    yield return WaitReal(1.5f);
                    if (Run.Done) yield break;
                    if (!string.IsNullOrEmpty(audit))
                    {
                        yield return Audit(audit, 0, 1, 0.2f, progress: false);
                        if (Run.Done) yield break;
                    }
                    string continueText = GameDefinition.Instance.menuTheme.texts.continueText;
                    yield return ClickUntil(continueText, () => ShaftScreen.Current != screen || screen.Continued, 15f);
                }
                lastShaft = seen;
                shaftWatch = null;
            }

            /// <summary>The first plunge after the plunge video: past every Circle to the bottom, the slam and the welcome, not skippable.</summary>
            IEnumerator WatchPlunge(Action<string, bool, string> check, string audit = null)
            {
                var config = GameDefinition.Instance.shaft;
                var screen = ShaftScreen.Current;
                check("plungeOpens", screen != null && screen.Kind == ShaftScreen.Mode.Plunge && screen.FromFloor == 0 && screen.ToFloor == config.bottomFloor,
                    screen == null ? $"no Shaft after video {config.plungeAfterVideo}"
                        : $"{screen.Kind} from FLOOR {screen.FromFloor} to FLOOR {screen.ToFloor} (want the plunge from 0 to {config.bottomFloor})");
                if (screen == null)
                    yield break;
                ArtChecks(screen, check, "plunge");
                bool first = !screen.Skippable;
                yield return PassShaft(pressMidway: first, audit: audit);
                if (Run.Done) yield break;
                var seen = lastShaft;
                if (seen == null)
                    yield break;
                check("plungeBands", seen.BandCount >= 10 && seen.Bands == seen.BandCount,
                    $"fell past {seen.Bands} of {seen.BandCount} Circle bands (Meadow at the top, {seen.BandCount} Circles)");
                check("plungeBottom", seen.Shown == config.bottomFloor && seen.Slam,
                    $"the counter stopped at FLOOR {seen.Shown}{(seen.Slam ? " with the slam" : ", no slam")}");
                string welcome = seen.Voices.Find(v => v.StartsWith("announcer", StringComparison.Ordinal)) ?? "";
                check("plungeWelcome", welcome.IndexOf("NINE HUNDRED NINETY-NINE", StringComparison.OrdinalIgnoreCase) >= 0 && welcome.IndexOf("no clip", StringComparison.Ordinal) < 0,
                    welcome.Length > 0 ? "the announcer: " + welcome : "no announcer line at the bottom");
                string hero = seen.Voices.Find(v => v.StartsWith("hero", StringComparison.Ordinal)) ?? "";
                check("plungeHero", hero.Length > 0 && hero.IndexOf("no clip", StringComparison.Ordinal) < 0, hero.Length > 0 ? "the hero: " + hero : "no hero line");
                if (first)
                    check("plungeNotSkipped", !seen.Skipped, seen.Skipped ? "Space skipped the first plunge" : "Space mid-fall didn't skip the first plunge");
                check("plungeInfo", seen.FloorsLeft.Length > 0 && seen.NextWarden.Length > 0, $"{seen.FloorsLeft}; {seen.NextWarden}");
            }

            ShaftSeen lastShaft;

            /// <summary>
            /// The Shaft's paintings: a painted band for every Circle and the Meadow on top, every Warden drawn from its
            /// art (a dark silhouette until met, at least one still dark), and the hero's marker drawn from his art.
            /// </summary>
            static void ArtChecks(ShaftScreen screen, Action<string, bool, string> check, string prefix)
            {
                check(prefix + "PaintedBands", screen.BandCount >= 10 && screen.PaintedBands == screen.BandCount && screen.PaintedMeadow,
                    $"{screen.PaintedBands} of {screen.BandCount} Circle bands painted, the Meadow {(screen.PaintedMeadow ? "painted" : "a placeholder")}");
                check(prefix + "WardenSilhouettes", screen.WardenSilhouettes + screen.WardenPortraits == screen.BandCount && screen.WardenSilhouettes > 0,
                    $"{screen.WardenSilhouettes} Wardens as dark silhouettes, {screen.WardenPortraits} met (in colour), of {screen.BandCount}");
                check(prefix + "HeroMarker", screen.PaintedMarker, screen.PaintedMarker ? "the hero's marker is his helmet and rocket launcher" : "the hero's marker is a placeholder dot");
            }

            /// <summary>
            /// After level n's results: a level with a floor range brings up the Shaft, whose marker climbs from where it
            /// was to the level's last floor, with the floors left and the next Warden; a level without one goes straight on.
            /// </summary>
            IEnumerator WatchClimb(int n, Action<string, bool, string> check, string audit = null)
            {
                ShaftScreen.LevelFloors(n, out int start, out int end);
                yield return Run.Until(() => ShaftScreen.Current != null || Object.FindAnyObjectByType<VideoRuntime>() != null
                                             || Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                             || Object.FindAnyObjectByType<MainMenuManager>() != null, 20f, $"what comes after level {n}");
                if (Run.Done) yield break;
                var screen = ShaftScreen.Current;
                if (end <= 0)
                {
                    if (screen != null)
                        yield return PassShaft();
                    yield break;
                }
                var config = GameDefinition.Instance.shaft;
                check("climbOpens" + n, screen != null && screen.Kind == ShaftScreen.Mode.Climb && screen.ToFloor == end,
                    screen == null ? $"no Shaft after level {n} (floors {start}-{end})" : $"{screen.Kind} from FLOOR {screen.FromFloor} to FLOOR {screen.ToFloor} (level {n} ends on {end})");
                if (screen == null)
                    yield break;
                ArtChecks(screen, check, "climb" + n);
                // A shortcut taken on the level: after the climb the marker jumps a dotted line on up to where it let him out.
                int jumpTo = screen.ShortcutTo;
                int climbed = end;
                yield return PassShaft(audit: audit);
                if (Run.Done) yield break;
                var seen = lastShaft;
                if (seen == null)
                    yield break;
                if (jumpTo > 0)
                {
                    check("climbShortcut" + n, screen.ShortcutJumped && screen.ShortcutDots > 0 && seen.Shown == jumpTo,
                        $"after the climb to FLOOR {climbed} the marker {(screen.ShortcutJumped ? "jumped" : "did NOT jump")} the shortcut to FLOOR {seen.Shown} " +
                        $"(want {jumpTo}) along {screen.ShortcutDots} dots; saved marker FLOOR {ShaftProgress.Floor}");
                    end = jumpTo;
                }
                string left = Mathf.Max(0, end - config.topFloor).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                SickOfIt.Data.Config.ShaftBand warden = null;
                foreach (var band in config.bands)
                    if (band != null && band.lastFloor < end && (warden == null || band.lastFloor > warden.lastFloor))
                        warden = band;
                string distance = warden != null ? (end - warden.lastFloor).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "";
                check("climbCounter" + n, seen.From > seen.To && seen.Shown == end,
                    $"the marker climbed from FLOOR {seen.From} and the counter ticked to FLOOR {seen.Shown} (level {n} ends on {climbed}" +
                    (jumpTo > 0 ? $", the shortcut on to {jumpTo})" : ")"));
                check("climbFloorsLeft" + n, seen.FloorsLeft.Contains(left), $"\"{seen.FloorsLeft}\" (want {left} floors left)");
                check("climbWarden" + n, warden != null && seen.NextWarden.IndexOf(warden.warden, StringComparison.OrdinalIgnoreCase) >= 0 && seen.NextWarden.Contains(distance),
                    $"\"{seen.NextWarden}\" (want {(warden != null ? warden.warden : "?")}, {distance} floors up)");
                // A Warden's floor: the climb lights its Waystone (saved as the run's new start) and the Circle above wakes up.
                if (end != config.bottomFloor && RunFlow.IsWaystone(end))
                {
                    check("climbWaystone" + n, WaystoneProgress.Highest > 0 && WaystoneProgress.Highest <= end && seen.WaystoneLit.Length > 0,
                        $"highest Waystone saved: FLOOR {WaystoneProgress.Highest} (want {end}); on the map: {(seen.WaystoneLit.Length > 0 ? seen.WaystoneLit : "no waystone-lit beat")}");
                    bool voiced = seen.Voices.Exists(v => v.StartsWith("announcer: CIRCLE", StringComparison.Ordinal) && !v.Contains("no clip"));
                    check("climbWakes" + n, seen.Wakes.Length > 0 && (seen.Skipped || voiced),
                        seen.Wakes.Length > 0 ? $"{seen.Wakes}; the announcer {(voiced ? "calls it" : seen.Skipped ? "(skipped)" : "is NOT heard")}" : "no Circle woke up on the climb");
                }
            }

            /// <summary>
            /// The Shaft over the main menu (setup shortcut: a preview climb, nothing saved; Yooba marked as met so Visit
            /// Yooba stands beside Continue), its layout checked; then Visit Yooba opens his shop over it (layout checked
            /// too), Back returns to the Shaft, and Continue closes it.
            /// </summary>
            IEnumerator PreviewShaft(int step = 0, int steps = 0)
            {
                var yooba = GameDefinition.Instance.yooba;
                bool met = YoobaProgress.Met;
                try
                {
                    if (yooba != null && yooba.enabled && !met)
                    {
                        Run.Event("shortcut", "Yooba marked as met (so the Shaft shows Visit Yooba); put back afterwards");
                        YoobaProgress.Met = true;
                    }
                    Run.Event("shortcut", "open the Shaft over the main menu (preview: the marker climbs 1000 to 991)");
                    var screen = ShaftScreen.Preview(1000, 991);
                    yield return Run.Until(() => ShaftScreen.Current != screen || screen.Ready, 75f, "the Shaft preview's Continue");
                    if (Run.Done) yield break;
                    if (step > 0)
                        yield return Audit("Shaft", step, steps);
                    else
                        yield return Audit("Shaft", 0, 1, 1f, progress: false);
                    if (Run.Done) yield break;
                    if (ShaftScreen.Current == screen && screen.YoobaButton != null)
                    {
                        yield return VisitYooba(screen, ShopCheck, step > 0 ? "Yooba's shop (Shaft)" : null, step > 0 ? step + 1 : 0, steps);
                        if (Run.Done) yield break;
                        yield return LeaveYooba(screen, ShopCheck);
                        if (Run.Done) yield break;
                    }
                    else if (yooba != null && yooba.enabled)
                    {
                        ShopCheck("visitYoobaShown", false, "no Visit Yooba button on the Shaft with Yooba met");
                    }
                    string continueText = GameDefinition.Instance.menuTheme.texts.continueText;
                    yield return ClickUntil(continueText, () => ShaftScreen.Current == null, 15f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                    yield return WaitReal(0.8f);
                }
                finally
                {
                    if (YoobaProgress.Met != met)
                        YoobaProgress.Met = met;
                }
            }

            // ─── Yooba's shop over the Shaft ──────────────────────────────

            int shopPassed, shopFailed;

            void ShopCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    shopPassed++;
                    Run.Event("check", $"shop {beat}: ok ({detail})");
                }
                else
                {
                    shopFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"shop {beat}: {detail}");
                }
                Run.Metric("shop.passed", shopPassed);
                Run.Metric("shop.failed", shopFailed);
            }

            static string Where(MenuButton button)
            {
                if (button == null)
                    return "none";
                var rect = (RectTransform)(button.transform.parent != null ? button.transform.parent : button.transform);
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                return $"x {corners[0].x:0}..{corners[2].x:0}, y {corners[0].y:0}..{corners[2].y:0}";
            }

            /// <summary>
            /// Visit Yooba on a finished Shaft: the button beside Continue, clicked with the virtual mouse; his shop opens
            /// over the Shaft with his portrait and a voiced welcome; its title and goods are checked (and its layout, if 'audit').
            /// </summary>
            IEnumerator VisitYooba(ShaftScreen screen, Action<string, bool, string> check, string audit = null, int step = 0, int steps = 0)
            {
                var definition = GameDefinition.Instance;
                var button = screen.YoobaButton;
                var next = screen.ContinueButton;
                bool beside = false;
                if (button != null && next != null)
                {
                    var a = new Vector3[4];
                    var b = new Vector3[4];
                    ((RectTransform)button.transform.parent).GetWorldCorners(a);
                    ((RectTransform)next.transform.parent).GetWorldCorners(b);
                    // Same row (their heights overlap), side by side (no overlap across).
                    beside = a[0].y < b[1].y && b[0].y < a[1].y && (a[2].x <= b[0].x || b[2].x <= a[0].x);
                }
                string label = button != null && button.Label != null ? Tags.Replace(button.Label.text ?? "", "").Trim() : "";
                check("visitYoobaShown", button != null && button.isActiveAndEnabled && button.interactable && beside,
                    $"\"{label}\" at {Where(button)}, Continue at {Where(next)} (FLOOR {screen.ShownFloor}, {screen.Kind})");
                if (button == null)
                    yield break;
                Run.Scene("Shaft: Visit Yooba");
                yield return WaitReal(1f);
                if (Run.Done) yield break;
                int lines0 = yoobaLines.Count, clips0 = yoobaClips.Count;
                yield return ClickButton(button, label);
                yield return Run.Until(() => UpgradeShopManager.OverShaft != null, 10f, "Yooba's shop over the Shaft");
                if (Run.Done) yield break;
                var shop = UpgradeShopManager.OverShaft;
                check("shopOverShaft", shop != null && screen.ShopOpen && ShaftScreen.Current == screen,
                    shop != null ? "his shop opened over the Shaft (no scene load)" : "Visit Yooba opened nothing");
                if (shop == null)
                    yield break;
                Run.Scene("Yooba's shop (over the Shaft)");
                yield return Run.Until(() => UpgradeShopManager.OverShaft != shop || shop.Yooba != null && !shop.Yooba.Speaking && yoobaLines.Count > lines0, 25f, "Yooba's welcome");
                if (Run.Done) yield break;
                var portrait = shop.Yooba;
                check("shopPortrait", portrait != null && portrait.PaintedClips > 0,
                    portrait != null ? $"his animated portrait behind the counter, {portrait.PaintedClips} painted loops" : "no Yooba behind the counter");
                check("shopTalks", ClipSeenSince(clips0, "talk") || ClipSeenSince(clips0, "laugh"), "his mouth moved (talk or laugh loop) while he spoke");
                check("shopWelcome", ShopLineSince(lines0, "ShopWelcome"), FirstSince(lines0));
                var layout = definition.upgradeShopConfig.layout;
                check("shopTitle", layout.title.text.IndexOf("Yooba", StringComparison.OrdinalIgnoreCase) >= 0, $"title \"{layout.title.text}\"");
                var goods = new List<string>();
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && shop.CardButton(upgrade.id) != null)
                        goods.Add(upgrade.title);
                Run.Event("shop-goods", string.Join(", ", goods));
                check("shopGoods", goods.Count > 0, $"{goods.Count} goods on the counter: {string.Join(", ", goods)}");
                if (!string.IsNullOrEmpty(audit))
                    yield return Audit(audit, step, steps, 0.5f, progress: step > 0);
            }

            /// <summary>Back out of Yooba's shop: his goodbye, and the same Shaft again with Continue (and Visit Yooba) up.</summary>
            IEnumerator LeaveYooba(ShaftScreen screen, Action<string, bool, string> check)
            {
                var shop = UpgradeShopManager.OverShaft;
                if (shop == null)
                    yield break;
                int bye0 = yoobaLines.Count;
                yield return ClickButton(shop.BackButton, GameDefinition.Instance.upgradeShopConfig.layout.backText);
                yield return Run.Until(() => UpgradeShopManager.OverShaft == null, 15f, "Yooba's shop to close");
                if (Run.Done) yield break;
                yield return WaitReal(0.6f);
                if (Run.Done) yield break;
                check("shopLeave", ShopLineSince(bye0, "ShopLeave"), FirstSince(bye0));
                bool back = ShaftScreen.Current == screen && !screen.ShopOpen && !screen.Continued && screen.ContinueButton != null && screen.ContinueButton.isActiveAndEnabled;
                check("backToShaft", back, back ? $"back on the Shaft at FLOOR {screen.ShownFloor}, Continue up" : "the Shaft didn't come back after the shop");
                Run.Scene("Shaft (back from Yooba's)");
                yield return WaitReal(1.2f);
            }

            /// <summary>
            /// Yooba's shop from the Shaft, through the real flow (acceptance criteria of the shop task): his hello Shaft
            /// after Video3 (setup shortcut: his save reset, the marker at Floor 991, his hello skipped), Visit Yooba beside
            /// Continue opens his shop over the Shaft (portrait, voiced welcome, slime-made goods), a card with 0 diamonds
            /// (can't afford), the same card with its price in diamonds (bought: diamonds spent, level saved), Back (goodbye)
            /// returns to the Shaft, Continue starts Level 3 with the goods applied; then the main menu's Visit Yooba shows
            /// the goods still bought; then a climb between floors (preview) shows Visit Yooba again.
            /// </summary>
            public IEnumerator YoobaShopRun()
            {
                var definition = GameDefinition.Instance;
                var settings = definition.yooba;
                int video = settings.helloAfterVideo;
                var videoConfig = definition.GetVideo(video);
                int nextLevel = videoConfig != null ? videoConfig.nextLevel : 3;
                var roguelite = RogueliteManager.Instance;

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // 1. The Shaft after Video3 (Yooba's hello), skipped to its end.
                Run.Event("shortcut", $"reset Yooba's save, put the Shaft's marker at Floor 991 (the Landing cleared), diamonds to 0, load video {video} directly");
                YoobaProgress.Reset();
                ShaftProgress.Floor = 991;
                ShaftProgress.Plunged = true;
                roguelite.SetPoints(0);
                PlayerPrefs.SetInt("ReturnToLevel", nextLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(video);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {video} to start playing");
                if (Run.Done) yield break;
                Run.Scene("Video " + video);
                yield return WaitReal(3f);
                if (Run.Done) yield break;
                yield return Tap(Key.Escape);
                Run.Event("skip", "video " + video);
                yield return Run.Until(() => ShaftScreen.Current != null || GameplayWorld.Instance != null, 20f, "the Shaft after the video");
                if (Run.Done) yield break;
                var screen = ShaftScreen.Current;
                ShopCheck("shaft", screen != null, screen != null ? $"the Shaft at FLOOR {screen.ToFloor} ({screen.Kind})" : "no Shaft after the video");
                if (screen == null) { End(PlaytestOutcome.Lost); yield break; }
                Run.Scene("Shaft: Yooba's hello");
                yield return WaitReal(4f);
                if (Run.Done) yield break;
                if (!screen.Ready)
                {
                    yield return Tap(Key.Space);
                    Run.Event("input", "Space: skip the rest of the hello");
                }
                yield return Run.Until(() => ShaftScreen.Current != screen || screen.Ready, 60f, "the Shaft's Continue");
                if (Run.Done) yield break;
                yield return WaitReal(0.8f);
                if (Run.Done) yield break;
                Run.Progress(0.15f);

                // 2. Visit Yooba: his shop over the Shaft.
                yield return VisitYooba(screen, ShopCheck, "Yooba's shop (Shaft)");
                if (Run.Done) yield break;
                var shop = UpgradeShopManager.OverShaft;
                if (shop == null) { End(PlaytestOutcome.Lost); yield break; }
                Run.Progress(0.3f);

                // 3. Can't afford, then buy.
                SickOfIt.Data.Config.UpgradeDefinition pick = null;
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && upgrade.Effect == SickOfIt.Data.Config.ShopEffect.MaxHealth && roguelite.GetNextCost(upgrade) > 0) { pick = upgrade; break; }
                if (pick == null)
                    foreach (var upgrade in RogueliteManager.ShopCatalog)
                        if (upgrade != null && upgrade.Effect != SickOfIt.Data.Config.ShopEffect.None && roguelite.GetNextCost(upgrade) > 0) { pick = upgrade; break; }
                int owned = 0, maxHealth = 0;
                if (pick != null)
                {
                    int broke0 = yoobaLines.Count;
                    yield return ClickButton(shop.CardButton(pick.id), pick.title + " (0 diamonds)");
                    yield return Run.Until(() => yoobaLines.Count > broke0 && !shop.Yooba.Speaking, 20f, "Yooba's can't-afford line");
                    if (Run.Done) yield break;
                    ShopCheck("shopBroke", ShopLineSince(broke0, "ShopBroke") && roguelite.GetUpgradeLevel(pick.id) == 0,
                        FirstSince(broke0) + $"; {pick.title} still level {roguelite.GetUpgradeLevel(pick.id)}");

                    int cost = roguelite.GetNextCost(pick);
                    owned = roguelite.GetUpgradeLevel(pick.id);
                    Run.Event("shortcut", $"diamonds set to {cost + 3} so {pick.title} (♦ {cost}) can be bought");
                    roguelite.SetPoints(cost + 3);
                    shop.RefreshUI();
                    yield return WaitReal(0.6f);
                    if (Run.Done) yield break;
                    int buy0 = yoobaLines.Count;
                    yield return ClickButton(shop.CardButton(pick.id), pick.title + $" (♦ {cost})");
                    yield return Run.Until(() => yoobaLines.Count > buy0 && !shop.Yooba.Speaking, 20f, "Yooba's buy line");
                    if (Run.Done) yield break;
                    ShopCheck("shopBuy", ShopLineSince(buy0, "ShopBuy"), FirstSince(buy0));
                    ShopCheck("paidInDiamonds", roguelite.GetPoints() == 3 && roguelite.GetUpgradeLevel(pick.id) == owned + 1,
                        $"{pick.title} level {owned} -> {roguelite.GetUpgradeLevel(pick.id)}, diamonds {cost + 3} -> {roguelite.GetPoints()} (price ♦ {cost})");
                    owned = roguelite.GetUpgradeLevel(pick.id);
                    maxHealth = roguelite.GetMaxHealth();
                }
                else
                {
                    ShopCheck("shopCard", false, "nothing left to buy in Yooba's shop");
                }
                Run.Progress(0.45f);

                // 4. Back: his goodbye, the Shaft again, then Continue on to the next level.
                yield return LeaveYooba(screen, ShopCheck);
                if (Run.Done) yield break;
                string continueText = definition.menuTheme.texts.continueText;
                yield return ClickButton(screen.ContinueButton, continueText);
                yield return Run.Until(() => GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null, 60f, $"level {nextLevel} after the Shaft");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                ShopCheck("continueToLevel", world != null && world.LevelNumber == nextLevel, $"Continue started level {world?.LevelNumber} ({world?.LevelName}); want {nextLevel}");
                if (pick != null && world != null)
                {
                    bool applied = pick.Effect != SickOfIt.Data.Config.ShopEffect.MaxHealth || world.Player.MaxHealth == maxHealth;
                    ShopCheck("keptInLevel", roguelite.GetUpgradeLevel(pick.id) == owned && applied,
                        $"{pick.title} level {roguelite.GetUpgradeLevel(pick.id)} in level {world.LevelNumber}" +
                        (pick.Effect == SickOfIt.Data.Config.ShopEffect.MaxHealth ? $", the hero's max health {world.Player.MaxHealth} (want {maxHealth})" : ""));
                }
                Run.Scene("Level " + nextLevel);
                yield return WaitReal(4f);
                if (Run.Done) yield break;
                Run.Progress(0.6f);

                // 5. The main menu's Visit Yooba: the same shop, the goods still bought.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Yooba's shop (main menu)");
                var menuShop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(2f);
                if (Run.Done) yield break;
                if (pick != null)
                    ShopCheck("staysBought", roguelite.GetUpgradeLevel(pick.id) == owned && menuShop != null && menuShop.Yooba != null,
                        $"{pick.title} still level {roguelite.GetUpgradeLevel(pick.id)} from the main menu's Visit Yooba, Yooba {(menuShop != null && menuShop.Yooba != null ? "at the counter" : "missing")}");
                yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Main Menu");
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                Run.Progress(0.75f);

                // 6. Between floors: a climb (preview over the menu, setup shortcut) shows Visit Yooba beside Continue again.
                Run.Event("shortcut", "Shaft preview: the marker climbs 991 to 981 (the Spill cleared)");
                var climb = ShaftScreen.Preview(991, 981);
                Run.Scene("Shaft: between floors");
                yield return Run.Until(() => ShaftScreen.Current != climb || climb.Ready, 60f, "the climb's Continue");
                if (Run.Done) yield break;
                yield return VisitYooba(climb, ShopCheck);
                if (Run.Done) yield break;
                yield return LeaveYooba(climb, ShopCheck);
                if (Run.Done) yield break;
                yield return ClickUntil(continueText, () => ShaftScreen.Current == null, 15f);
                if (Run.Done) yield break;
                Run.Scene("Main Menu");
                yield return WaitReal(1.5f);
                Run.Progress(1f);
                End(shopFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }

            /// <summary>
            /// The Shaft through the real flow: Video2 (loaded directly, skipped) → the first plunge → Continue →
            /// The Landing played by the bot → its results → the climb → Continue → what comes next.
            /// </summary>
            // ─── music: what plays on the Shaft, a floor stage and the Magma King's fight ──────────

            sealed class MusicHeard
            {
                public int Level;
                public string Track, Why;
                public float Time;
                public bool AfterEnrage;
            }

            readonly List<MusicHeard> musicHeard = new List<MusicHeard>();
            readonly List<string> shaftMusic = new List<string>();
            int musicPassed, musicFailed;

            void MusicCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    musicPassed++;
                    Run.Event("check", $"music {beat}: ok ({detail})");
                }
                else
                {
                    musicFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"music {beat}: {detail}");
                }
                Run.Metric("music.passed", musicPassed);
                Run.Metric("music.failed", musicFailed);
            }

            /// <summary>StageMusic switched tracks (or started): the level's own, the Circle's push music, or a script's playMusic.</summary>
            void OnMusic(GameplayTelemetry log, GameplayTelemetry.Event e, string level)
            {
                bool enraged = false;
                foreach (var ev in log.Events)
                {
                    if (ev.Kind == "signal" && ev.Subject == "mkEnrage")
                    {
                        enraged = true;
                        break;
                    }
                }
                musicHeard.Add(new MusicHeard { Level = log.World.LevelNumber, Track = e.Subject, Why = e.Detail, Time = e.Time, AfterEnrage = enraged });
                Run.Event("music", $"{level}: {e.Subject} ({e.Detail}{(enraged ? ", after the enrage" : "")}) at {e.Time:0.0} s");
            }

            static string Tracks(List<MusicHeard> heard)
            {
                var parts = new List<string>();
                foreach (var m in heard)
                    parts.Add($"{m.Track} ({m.Why}{(m.AfterEnrage ? ", enraged" : "")}) at {m.Time:0.0} s");
                return parts.Count > 0 ? string.Join(", ", parts) : "no music";
            }

            void CheckShaftMusic(string beat, string sound, string what)
            {
                string heard = shaftMusic.Find(m => m.IndexOf(what, StringComparison.Ordinal) >= 0) ?? "";
                MusicCheck(beat, !string.IsNullOrEmpty(sound) && heard.Length > 0 && heard.IndexOf("no clip", StringComparison.Ordinal) < 0,
                    string.IsNullOrEmpty(sound) ? $"ui/shaft.json sets no sound for {what}" : heard.Length > 0 ? heard : $"{what} ({sound}) never played");
            }

            /// <summary>The Magma King's arena (loaded directly): his theme when the fight starts, the enrage variant after mkEnrage.</summary>
            IEnumerator MusicBoss()
            {
                float deadline = Time.realtimeSinceStartup + MusicBossSeconds - 20f;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Event("shortcut", $"load level {MusicBossLevel} (the Magma King's arena) directly");
                var load = LoadLevel(MusicBossLevel);
                if (load != null)
                    yield return load;
                int attempts = 0;
                while (true)
                {
                    attempts++;
                    float left = deadline - Time.realtimeSinceStartup;
                    if (left < 5f) { lastOutcome = "time up"; break; }
                    yield return PlayLevel(MusicBossLevel, 0f, null, 0.6f, 0.95f, left, waitOutDeath: true,
                        until: () => musicHeard.Exists(m => m.Level == MusicBossLevel && m.AfterEnrage && m.Why == "script"));
                    if (Run.Done) yield break;
                    if (musicHeard.Exists(m => m.Level == MusicBossLevel && m.AfterEnrage && m.Why == "script"))
                    {
                        // A moment of the enrage music for the recording.
                        yield return WaitReal(6f);
                        if (Run.Done) yield break;
                        break;
                    }
                    if (lastOutcome != "died" || attempts >= 3)
                        break;
                    Run.Event("retry", $"level {MusicBossLevel}, attempt {attempts + 1}");
                    yield return ReachLevel(MusicBossLevel);
                    if (Run.Done) yield break;
                }
                var heard = musicHeard.FindAll(m => m.Level == MusicBossLevel);
                var theme = heard.Find(m => m.Why == "script" && !m.AfterEnrage);
                var enrage = heard.Find(m => m.Why == "script" && m.AfterEnrage);
                MusicCheck("bossTheme", theme != null, $"level {MusicBossLevel}: {Tracks(heard)}");
                MusicCheck("bossEnrage", theme != null && enrage != null && enrage.Track != theme.Track,
                    enrage != null ? $"after mkEnrage: {enrage.Track}" : $"no enrage music ({lastOutcome}, {attempts} attempt(s)): {Tracks(heard)}");
            }

            public IEnumerator ShaftRun(bool music = false)
            {
                musicHeard.Clear();
                shaftMusic.Clear();
                var definition = GameDefinition.Instance;
                var config = definition.shaft;
                var texts = definition.menuTheme.texts;
                int video = config.plungeAfterVideo;
                float deadline = Time.realtimeSinceStartup + ShaftSeconds - 40f;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Event("shortcut", $"reset the Shaft's save (first plunge again), load video {video} directly, then level {ShaftLevel} after it");
                ShaftProgress.Reset();
                PlayerPrefs.SetInt("ReturnToLevel", ShaftLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(video);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {video} to start playing");
                if (Run.Done) yield break;
                Run.Scene("Video " + video);
                yield return WaitReal(2.5f);
                if (Run.Done) yield break;
                yield return Tap(Key.Escape);
                Run.Event("skip", "video " + video);
                yield return Run.Until(() => ShaftScreen.Current != null || GameplayWorld.Instance != null, 20f, "the Shaft after the video");
                if (Run.Done) yield break;
                Run.Progress(0.1f);

                // 1. The plunge, then Continue goes to the level.
                yield return WatchPlunge(ShaftCheck, $"Shaft plunge {(MenuScreen.Portrait ? "portrait" : "landscape")}");
                if (Run.Done) yield break;
                if (music)
                    CheckShaftMusic("plungeRiser", config.plungeMusic, "the plunge's riser and slam");
                yield return Run.Until(() => LevelRunning(ShaftLevel) || GameplayWorld.Instance != null && GameplayWorld.Instance.LevelNumber != ShaftLevel,
                    60f, $"level {ShaftLevel} after the Shaft's Continue");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                ShaftCheck("continueToLevel", world != null && world.LevelNumber == ShaftLevel,
                    $"Continue started level {world?.LevelNumber} ({world?.LevelName}); want level {ShaftLevel}");
                Run.Progress(0.25f);

                // 2. The level, up to 3 tries.
                int attempts = 0;
                while (true)
                {
                    attempts++;
                    float left = deadline - Time.realtimeSinceStartup;
                    if (left < 5f) { lastOutcome = "time up"; break; }
                    yield return PlayLevel(ShaftLevel, 0f, null, 0.25f, 0.8f, left, waitOutDeath: true);
                    if (Run.Done) yield break;
                    if (lastOutcome == "completed" || lastOutcome != "died" || attempts >= 3)
                        break;
                    Run.Event("retry", $"level {ShaftLevel}, attempt {attempts + 1}");
                    yield return ReachLevel(ShaftLevel);
                    if (Run.Done) yield break;
                }
                if (music)
                {
                    var stageMusic = musicHeard.FindAll(m => m.Level == ShaftLevel);
                    var loop = stageMusic.Find(m => m.Why == "start");
                    var push = stageMusic.Find(m => m.Why == "push");
                    MusicCheck("stageLoop", loop != null, $"level {ShaftLevel}: {Tracks(stageMusic)}");
                    MusicCheck("stagePush", push != null && loop != null && push.Track != loop.Track,
                        push != null ? $"a push changed it up to {push.Track} at {push.Time:0.0} s; back to the loop " +
                                       $"{stageMusic.FindAll(m => m.Why == "level").Count} time(s)" : $"no push music on level {ShaftLevel}: {Tracks(stageMusic)}");
                }

                if (lastOutcome == "completed")
                {
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {ShaftLevel} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;

                    // 3. The climb, then Continue goes on to whatever the story has next.
                    yield return WatchClimb(ShaftLevel, ShaftCheck, $"Shaft climb {(MenuScreen.Portrait ? "portrait" : "landscape")}");
                    if (Run.Done) yield break;
                    if (music)
                        CheckShaftMusic("climbSting", config.climbMusic, "the climb's rising sting");
                    yield return Run.Until(() => ShaftScreen.Current == null && (Object.FindAnyObjectByType<VideoRuntime>() != null || GameplayWorld.Instance == null
                                                                                 || GameplayWorld.Instance.LevelNumber != ShaftLevel), 60f, "the scene after the Shaft");
                    if (Run.Done) yield break;
                    bool video3 = Object.FindAnyObjectByType<VideoRuntime>() != null;
                    var after = definition.GetLevel(ShaftLevel);
                    int nextVideo = after != null ? after.nextVideoNumber : 0;
                    ShaftCheck("continueOn", nextVideo <= 0 || video3 && PlayerPrefs.GetInt("CurrentVideoNumber", 0) == nextVideo,
                        video3 ? $"Continue went on to video {PlayerPrefs.GetInt("CurrentVideoNumber", 0)}" : $"Continue went on to {SceneName()}");
                    Run.Scene(video3 ? "Video " + PlayerPrefs.GetInt("CurrentVideoNumber", 0) : SceneName());
                    yield return WaitReal(2f);
                }
                else
                {
                    // The bot couldn't clear the level: the climb is still shown, as a preview (setup shortcut), so it can be judged.
                    Run.Event("stopped", $"level {ShaftLevel}: {lastOutcome} after {attempts} attempt(s); showing the climb as a preview instead");
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    yield return PreviewShaft();
                    if (Run.Done) yield break;
                    if (music)
                        CheckShaftMusic("climbSting", config.climbMusic, "the climb's rising sting");
                }
                if (music)
                {
                    yield return MusicBoss();
                    if (Run.Done) yield break;
                    Run.Progress(1f);
                    End(shaftFailed > 0 || musicFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
                    yield break;
                }
                Run.Progress(1f);
                End(shaftFailed > 0 ? PlaytestOutcome.Done : lastOutcome == "completed" ? PlaytestOutcome.Won : PlaytestOutcome.Lost);
            }

            // ─── waystone: a death ends the run, the drop back to the Waystone, the menu's Continue / New Run ─────

            int waystonePassed, waystoneFailed;

            void WaystoneCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    waystonePassed++;
                    Run.Event("check", $"waystone {beat}: ok ({detail})");
                }
                else
                {
                    waystoneFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"waystone {beat}: {detail}");
                }
                Run.Metric("waystone.passed", waystonePassed);
                Run.Metric("waystone.failed", waystoneFailed);
            }

            static int GrudgeCount()
            {
                int count = 0;
                foreach (var pair in UpgradeProgress.Owned())
                    count += pair.Value;
                return count;
            }

            static string GrudgeList()
            {
                var parts = new List<string>();
                foreach (var pair in UpgradeProgress.Owned())
                    parts.Add($"{pair.Key} {pair.Value}");
                return parts.Count > 0 ? string.Join(", ", parts) : "none";
            }

            /// <summary>
            /// Waystones and runs (Docs/Story.md): the bot clears The Spill (a Grudge picked through real input), climbs on,
            /// then plays to lose on the next stage; the death screen; the Shaft's drop back to the Landing's Waystone with
            /// the first death's lines; Grudges gone, diamonds and Yooba's goods kept; the run restarting at the Landing;
            /// the main menu's Continue and New Run.
            /// </summary>
            public IEnumerator WaystoneRun()
            {
                var definition = GameDefinition.Instance;
                var labels = definition.mainMenuConfig.labels;
                var texts = definition.menuTheme.texts;
                var roguelite = RogueliteManager.Instance;
                var progress = GameProgressManager.Instance;
                int waystone = definition.shaft != null ? definition.shaft.bottomFloor : LandingFirstFloor;
                float deadline = Time.realtimeSinceStartup + WaystoneSeconds - 60f;

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // Setup: a save where the Landing was played, Yooba met, and something bought from him.
                roguelite.ResetAllRogueliteData();
                progress.InitializeNewPlayer();
                progress.SetLevelCompleted(1);
                progress.SetLevelCompleted(LandingLevel);
                ShaftProgress.Floor = LandingLastFloor;
                ShaftProgress.Plunged = true;
                YoobaProgress.Met = true;
                WaystoneProgress.Reach(waystone);
                SickOfIt.Data.Config.UpgradeDefinition goods = null;
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && upgrade.Effect != SickOfIt.Data.Config.ShopEffect.None && upgrade.Effect != SickOfIt.Data.Config.ShopEffect.MaxHealth
                        && roguelite.GetNextCost(upgrade) > 0) { goods = upgrade; break; }
                if (goods != null)
                {
                    roguelite.SetPoints(roguelite.GetNextCost(goods) + 40);
                    roguelite.TryPurchase(goods);
                }
                else
                    roguelite.SetPoints(40);
                Run.Event("shortcut", $"fresh save as if the Landing was cleared: the Waystone on FLOOR {waystone} reached, the Shaft's marker at " +
                                      $"{LandingLastFloor}, Yooba met, {(goods != null ? goods.title + " bought from Yooba, " : "")}{roguelite.GetPoints()} diamonds left");
                Run.Progress(0.03f);

                // 1. The Spill, played to its end: diamonds, its Grudge pick (the bot presses 1), the results, the climb.
                Run.Event("shortcut", $"load level {WaystoneLevel} directly");
                var load = LoadLevel(WaystoneLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(WaystoneLevel, 0f, null, 0.03f, 0.35f, Mathf.Max(30f, deadline - Time.realtimeSinceStartup - 150f), waitOutDeath: true);
                if (Run.Done) yield break;
                int deathLevel = WaystoneLevel;
                if (lastOutcome == "completed")
                {
                    var cleared = definition.GetLevel(WaystoneLevel);
                    int next = cleared != null && cleared.nextLevelNumber > 0 ? cleared.nextLevelNumber : WaystoneLevel + 1;
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {WaystoneLevel} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                    yield return ReachLevel(next);
                    if (Run.Done) yield break;
                    deathLevel = next;
                }
                else if (lastOutcome != "died")
                {
                    Run.Event("stopped", $"level {WaystoneLevel}: {lastOutcome}");
                    End(PlaytestOutcome.Stuck);
                    yield break;
                }
                WaystoneCheck("grudgePicked", GrudgeCount() > 0 || lastOutcome == "died", $"Grudges before the death: {GrudgeList()}");
                if (lastOutcome == "completed" && GrudgeCount() == 0)
                {
                    // The Spill was cleared without a pick: one Grudge is given so its loss can be seen.
                    foreach (var id in GameContent.Current.Upgrades.Keys)
                    {
                        UpgradeProgress.Add(id);
                        Run.Event("shortcut", $"Grudge {id} given (no pick was made on level {WaystoneLevel})");
                        break;
                    }
                }
                Run.Progress(0.4f);

                // 2. The next stage (or The Spill again, if the bot died on it): the bot plays to lose until Hell kills it.
                int grudges = GrudgeCount();
                string grudgeList = GrudgeList();
                int goodsLevel = goods != null ? roguelite.GetUpgradeLevel(goods.id) : 0;
                int diamonds = roguelite.GetPoints();
                if (lastOutcome != "died")
                {
                    reckless = true;
                    try
                    {
                        yield return PlayLevel(deathLevel, 0f, now =>
                        {
                            diamonds = roguelite.GetPoints();
                            return null;
                        }, 0.4f, 0.6f, Mathf.Max(30f, deadline - Time.realtimeSinceStartup - 90f), waitOutDeath: true);
                    }
                    finally
                    {
                        reckless = false;
                    }
                    if (Run.Done) yield break;
                }
                Run.Event("before-death", $"level {deathLevel}: Grudges {grudgeList}; {diamonds} diamonds; {(goods != null ? $"{goods.title} level {goodsLevel}" : "no goods")}");
                if (lastOutcome != "died")
                {
                    Run.Problem(PlaytestSeverity.Bug, $"waystone: the bot couldn't die on level {deathLevel} ({lastOutcome})");
                    End(PlaytestOutcome.Done);
                    yield break;
                }

                // 3. The death screen: "YOU OBVIOUSLY DIED" and the announcer gloating.
                yield return Run.Until(() => DeathRuntime.Current != null, 30f, "the death screen");
                if (Run.Done) yield break;
                Run.Scene("Death");
                var death = DeathRuntime.Current;
                yield return WaitReal(4f);
                if (Run.Done) yield break;
                if (death != null)
                {
                    WaystoneCheck("deathScreen", death.TitleText.IndexOf("Obviously", StringComparison.OrdinalIgnoreCase) >= 0, $"title \"{death.TitleText}\"");
                    var gloat = death.Gloat;
                    WaystoneCheck("deathGloat", gloat != null && Sfx.Has(gloat.sound),
                        gloat != null ? $"the announcer: \"{gloat.text}\" ({gloat.sound}{(Sfx.Has(gloat.sound) ? "" : ", no clip")})" : "no gloat line");
                    WaystoneCheck("runEnded", death.RunEnded && death.RunStop.Waystone == waystone && death.RunStop.NextLevel == LandingLevel,
                        death.RunEnded ? $"died on level {death.RunStop.Level} at FLOOR {death.RunStop.FromFloor}: back to the Waystone on FLOOR {death.RunStop.Waystone}, next run level {death.RunStop.NextLevel}"
                            : $"the death on level {deathLevel} didn't end the run");
                }
                yield return Tap(Key.Escape);
                Run.Event("skip", "death animation");
                Run.Progress(0.65f);

                // 4. The Shaft: the marker drops back to the Waystone; Grudges gone, diamonds and goods kept.
                yield return Run.Until(() => ShaftScreen.Current != null || GameplayWorld.Instance != null, 30f, "the Shaft after the death");
                if (Run.Done) yield break;
                var screen = ShaftScreen.Current;
                ShaftScreen.LevelFloors(deathLevel, out int start, out int end);
                WaystoneCheck("dropOpens", screen != null && screen.Kind == ShaftScreen.Mode.Drop && screen.ToFloor == waystone && screen.FromFloor <= start && screen.FromFloor >= end,
                    screen == null ? "no Shaft after the death screen (the level played again?)"
                        : $"{screen.Kind} from FLOOR {screen.FromFloor} to FLOOR {screen.ToFloor} (died on level {deathLevel}, floors {start}-{end}; the Waystone is {waystone})");
                if (screen == null) { End(PlaytestOutcome.Lost); yield break; }
                WaystoneCheck("waystonesOnShaft", screen.WaystonesShown >= 2 && screen.WaystonesLit >= 1,
                    $"{screen.WaystonesShown} Waystones marked on the map, {screen.WaystonesLit} lit (FLOORS {string.Join(", ", RunFlow.Waystones())})");
                WaystoneCheck("grudgesGone", GrudgeCount() == 0, $"Grudges {grudgeList} before the death, {GrudgeList()} now");
                WaystoneCheck("diamondsKept", roguelite.GetPoints() == diamonds, $"{diamonds} diamonds before the death, {roguelite.GetPoints()} now");
                if (goods != null)
                    WaystoneCheck("goodsKept", roguelite.GetUpgradeLevel(goods.id) == goodsLevel, $"{goods.title} level {goodsLevel} before, {roguelite.GetUpgradeLevel(goods.id)} now");
                bool first = screen.FirstDrop;
                yield return PassShaft(pressMidway: true, audit: $"Shaft drop {(MenuScreen.Portrait ? "portrait" : "landscape")}");
                if (Run.Done) yield break;
                var seen = lastShaft;
                if (seen != null)
                {
                    WaystoneCheck("dropLands", seen.Shown == waystone, $"the marker fell from FLOOR {seen.From} and the counter stopped at FLOOR {seen.Shown}");
                    string line = seen.Voices.Find(v => v.StartsWith("announcer", StringComparison.Ordinal)) ?? "";
                    WaystoneCheck("dropLine", first && line.IndexOf("DO IT AGAIN", StringComparison.OrdinalIgnoreCase) >= 0 && line.IndexOf("no clip", StringComparison.Ordinal) < 0,
                        line.Length > 0 ? (first ? "first death, " : "not the first death, ") + "the announcer: " + line : "no announcer line on the drop");
                    string hero = seen.Voices.Find(v => v.StartsWith("hero", StringComparison.Ordinal)) ?? "";
                    WaystoneCheck("dropHero", hero.Length > 0 && hero.IndexOf("no clip", StringComparison.Ordinal) < 0, hero.Length > 0 ? "the hero: " + hero : "no hero line");
                    WaystoneCheck("dropNotSkipped", !seen.Skipped, seen.Skipped ? "Space skipped the first drop" : "Space mid-drop didn't skip it");
                }
                Run.Progress(0.75f);

                // 5. Continue: the run restarts at the Waystone's level, the Landing, with no Grudges.
                yield return Run.Until(() => LevelRunning(LandingLevel) || GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null
                                             && GameplayWorld.Instance.LevelNumber != deathLevel, 60f, "the next run's first level");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                WaystoneCheck("runRestarts", world != null && world.LevelNumber == LandingLevel,
                    $"Continue started level {world?.LevelNumber} ({world?.LevelName}); the run restarts on level {LandingLevel} (FLOOR {waystone})");
                WaystoneCheck("noGrudgesInRun", GrudgeCount() == 0, $"Grudges in the new run: {GrudgeList()}");
                Run.Scene("Level " + LandingLevel + " (the next run)");
                yield return WaitReal(5f);
                if (Run.Done) yield break;
                Run.Progress(0.85f);

                // 6. The main menu: Continue (the current run) and New Run (from the Waystone).
                yield return ToMainMenu();
                if (Run.Done) yield break;
                string continueLabel = string.Format(labels.continueGame, LandingLevel);
                string newRunLabel = string.Format(System.Globalization.CultureInfo.InvariantCulture, labels.newRun,
                    waystone.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
                var continueButton = FindButton(continueLabel);
                var newRunButton = FindButton(newRunLabel);
                WaystoneCheck("menuContinue", continueButton != null, continueButton != null ? $"\"{continueLabel}\" on the main menu" : $"no \"{continueLabel}\" button");
                WaystoneCheck("menuNewRun", newRunButton != null, newRunButton != null ? $"\"{newRunLabel}\" on the main menu" : $"no \"{newRunLabel}\" button");
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                if (newRunButton != null)
                {
                    yield return ClickUntil(newRunLabel, () => Object.FindAnyObjectByType<MainMenuManager>() == null || GameplayWorld.Instance != null, 30f);
                    if (Run.Done) yield break;
                    yield return Run.Until(() => GameplayWorld.Instance != null && GameplayWorld.Instance.Player != null && ClockTime > 0f, 45f, "New Run's level");
                    if (Run.Done) yield break;
                    world = GameplayWorld.Instance;
                    WaystoneCheck("newRunStarts", world != null && world.LevelNumber == LandingLevel && GrudgeCount() == 0,
                        $"New Run started level {world?.LevelNumber} ({world?.LevelName}), Grudges {GrudgeList()}, {roguelite.GetPoints()} diamonds");
                    Run.Scene("Level " + LandingLevel + " (New Run)");
                    yield return WaitReal(4f);
                }
                Run.Progress(1f);
                End(waystoneFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }

            // ─── Yooba ────────────────────────────────────────────────────

            int yoobaPassed, yoobaFailed;
            readonly List<string> yoobaLines = new List<string>();
            readonly List<string> yoobaClips = new List<string>();

            void OnYooba(string kind, string detail)
            {
                if (Run.Done)
                    return;
                Run.Event("yooba", $"{kind}: {detail}");
                if (kind == "line")
                    yoobaLines.Add(detail);
                else if (kind == "clip")
                    yoobaClips.Add(detail);
            }

            void YoobaCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    yoobaPassed++;
                    Run.Event("check", $"yooba {beat}: ok ({detail})");
                }
                else
                {
                    yoobaFailed++;
                    Run.Problem(PlaytestSeverity.Bug, $"yooba {beat}: {detail}");
                }
                Run.Metric("yooba.passed", yoobaPassed);
                Run.Metric("yooba.failed", yoobaFailed);
            }

            /// <summary>Lines heard since 'from' (index into yoobaLines) by Yooba ("yooba (...") or the hero, and how many had no clip.</summary>
            (int yooba, int hero, int silent) LinesSince(int from)
            {
                int g = 0, h = 0, s = 0;
                for (int i = from; i < yoobaLines.Count; i++)
                {
                    if (yoobaLines[i].StartsWith("yooba", StringComparison.Ordinal)) g++;
                    else h++;
                    if (yoobaLines[i].Contains(", no clip")) s++;
                }
                return (g, h, s);
            }

            bool ClipSeenSince(int from, string clip)
            {
                for (int i = from; i < yoobaClips.Count; i++)
                    if (yoobaClips[i].StartsWith(clip + " ", StringComparison.Ordinal))
                        return true;
                return false;
            }

            /// <summary>Moves the virtual mouse onto a button and clicks it.</summary>
            IEnumerator ClickButton(MenuButton button, string what)
            {
                if (button == null)
                    yield break;
                input.MoveMouse(ScreenPoint(button));
                yield return null;
                yield return null;
                input.SetMouseButton(true);
                yield return null;
                yield return null;
                input.SetMouseButton(false);
                Run.Event("click", what);
            }

            /// <summary>
            /// Yooba (Docs/Story.md, "Yooba between floors"): his hello on the Shaft after Video3 through the real flow
            /// (setup shortcut: his save reset and the marker put at Floor 991, where the Landing ends), his lines between
            /// floors on Shaft previews over the main menu (950, a death, the Magma King's door: setup shortcuts), then
            /// the shop: welcome, can't afford, buy and goodbye, clicked with the virtual mouse.
            /// </summary>
            public IEnumerator YoobaRun()
            {
                var definition = GameDefinition.Instance;
                var settings = definition.yooba;
                string continueText = definition.menuTheme.texts.continueText;
                int video = settings.helloAfterVideo;
                var videoConfig = definition.GetVideo(video);
                int nextLevel = videoConfig != null ? videoConfig.nextLevel : 3;

                yield return ToMainMenu();
                if (Run.Done) yield break;

                // 1. The hello, after Video3, before the Spill.
                Run.Event("shortcut", $"reset Yooba's save, put the Shaft's marker at Floor 991 (the Landing cleared), load video {video} directly");
                YoobaProgress.Reset();
                ShaftProgress.Floor = 991;
                ShaftProgress.Plunged = true;
                PlayerPrefs.SetInt("ReturnToLevel", nextLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(video);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, $"video {video} to start playing");
                if (Run.Done) yield break;
                Run.Scene("Video " + video);
                yield return WaitReal(6f);
                if (Run.Done) yield break;
                yield return Tap(Key.Escape);
                Run.Event("skip", "video " + video);
                yield return Run.Until(() => ShaftScreen.Current != null || GameplayWorld.Instance != null, 20f, "the Shaft after the video");
                if (Run.Done) yield break;
                var screen = ShaftScreen.Current;
                YoobaCheck("helloShaft", screen != null && screen.Kind == ShaftScreen.Mode.Meet,
                    screen != null ? $"the Shaft opened as {screen.Kind} at FLOOR {screen.ToFloor}" : "no Shaft after the video");
                if (screen == null) { End(PlaytestOutcome.Lost); yield break; }
                Run.Scene("Shaft: Yooba's hello");
                Run.Progress(0.1f);
                int lines0 = yoobaLines.Count, clips0 = yoobaClips.Count;
                var portrait = screen.Yooba;
                YoobaCheck("portrait", portrait != null,
                    portrait != null ? $"portrait up, {portrait.PaintedClips} painted loops" : "no Yooba portrait on the hello Shaft");
                yield return Run.Until(() => ShaftScreen.Current != screen || screen.Ready, 90f, "Yooba's hello to finish");
                if (Run.Done) yield break;
                var heard = LinesSince(lines0);
                int hello = YoobaPortrait.HelloLines().Count;
                YoobaCheck("helloLines", heard.yooba >= hello && heard.yooba > 0, $"{heard.yooba} of {hello} hello lines, {heard.hero} hero replies");
                YoobaCheck("helloVoiced", heard.silent == 0, heard.silent == 0 ? "every line had its voice" : $"{heard.silent} line(s) without a clip");
                YoobaCheck("talking", ClipSeenSince(clips0, "talk"), "the talk loop played while he spoke");
                YoobaCheck("idle", ClipSeenSince(clips0, "idle"), "the idle loop played between lines");
                YoobaCheck("met", YoobaProgress.Met, "Yooba remembers meeting the hero");
                yield return Audit("Shaft (Yooba)", 0, 1, 1f, progress: false);
                if (Run.Done) yield break;
                yield return ClickUntil(continueText, () => ShaftScreen.Current == null, 30f);
                if (Run.Done) yield break;
                yield return Run.Until(() => GameplayWorld.Instance != null, 60f, $"level {nextLevel} after the hello");
                if (Run.Done) yield break;
                YoobaCheck("continueToLevel", GameplayWorld.Instance != null && GameplayWorld.Instance.LevelNumber == nextLevel,
                    $"Continue started level {GameplayWorld.Instance?.LevelNumber}; want {nextLevel}");
                yield return WaitReal(3f);
                if (Run.Done) yield break;
                Run.Progress(0.3f);

                // 2. Between floors: Shaft previews over the main menu (setup shortcuts), one reaction each.
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var climbs = new List<(int from, int to, bool died, string want, string label)>
                {
                    (951, 941, false, "Floor", "Floor 950 (Halfway Basin)"),
                    (981, 971, true, "Death", "a death on the way (Imp Run)"),
                    (921, 911, false, "NearWarden", "the Magma King's door (Heat Stairs)"),
                };
                int step = 0;
                foreach (var climb in climbs)
                {
                    step++;
                    Run.Event("shortcut", $"Shaft preview: the marker climbs {climb.from} to {climb.to}" + (climb.died ? ", the hero died on the way" : ""));
                    YoobaProgress.HeroDied = climb.died;
                    int before = yoobaLines.Count;
                    var preview = ShaftScreen.Preview(climb.from, climb.to);
                    Run.Scene("Shaft: " + climb.label);
                    yield return Run.Until(() => ShaftScreen.Current != preview || preview.Ready, 60f, "Yooba's line between floors");
                    if (Run.Done) yield break;
                    var said = LinesSince(before);
                    string first = before < yoobaLines.Count ? yoobaLines[before] : "";
                    YoobaCheck("betweenFloors." + climb.want, said.yooba > 0 && first.StartsWith("yooba (" + climb.want, StringComparison.Ordinal),
                        said.yooba > 0 ? $"{climb.label}: {first}" : $"{climb.label}: Yooba said nothing");
                    YoobaCheck("betweenFloorsVoiced." + climb.want, said.silent == 0, $"{said.yooba} Yooba line(s), {said.hero} hero repl(ies), {said.silent} without a clip");
                    yield return ClickUntil(continueText, () => ShaftScreen.Current == null, 15f);
                    if (Run.Done) yield break;
                    yield return WaitReal(0.8f);
                    Run.Progress(0.3f + 0.3f * step / climbs.Count);
                }
                YoobaProgress.HeroDied = false;

                // 3. The shop: Yooba's counter.
                var roguelite = RogueliteManager.Instance;
                Run.Event("shortcut", "diamonds set to 0 before the shop (so the first click can't afford anything)");
                roguelite.SetPoints(0);
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Shop");
                var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                int shop0 = yoobaLines.Count;
                yield return Run.Until(() => shop == null || shop.Yooba != null && !shop.Yooba.Speaking && yoobaLines.Count > shop0, 20f, "Yooba's welcome");
                if (Run.Done) yield break;
                YoobaCheck("shopPortrait", shop != null && shop.Yooba != null, "Yooba behind the shop counter");
                YoobaCheck("shopWelcome", ShopLineSince(shop0, "ShopWelcome"), FirstSince(shop0));
                yield return Audit("Shop (Yooba)", 0, 1, 1f, progress: false);
                if (Run.Done) yield break;

                SickOfIt.Data.Config.UpgradeDefinition pick = null;
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && upgrade.Effect != SickOfIt.Data.Config.ShopEffect.None && roguelite.GetNextCost(upgrade) > 0) { pick = upgrade; break; }
                if (shop != null && pick != null)
                {
                    int broke0 = yoobaLines.Count;
                    yield return ClickButton(shop.CardButton(pick.id), pick.title + " (0 diamonds)");
                    yield return Run.Until(() => yoobaLines.Count > broke0 && !shop.Yooba.Speaking, 20f, "Yooba's can't-afford line");
                    if (Run.Done) yield break;
                    YoobaCheck("shopBroke", ShopLineSince(broke0, "ShopBroke"), FirstSince(broke0));

                    int cost = roguelite.GetNextCost(pick);
                    Run.Event("shortcut", $"diamonds set to {cost} so {pick.title} can be bought");
                    roguelite.SetPoints(cost);
                    shop.RefreshUI();
                    yield return WaitReal(0.5f);
                    int buy0 = yoobaLines.Count;
                    yield return ClickButton(shop.CardButton(pick.id), pick.title + $" ({cost} diamonds)");
                    yield return Run.Until(() => yoobaLines.Count > buy0 && !shop.Yooba.Speaking, 20f, "Yooba's buy line");
                    if (Run.Done) yield break;
                    YoobaCheck("shopBuy", ShopLineSince(buy0, "ShopBuy"), FirstSince(buy0));
                }
                else
                {
                    YoobaCheck("shopCard", false, "no upgrade card to click in the shop");
                }
                int bye0 = yoobaLines.Count;
                yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                YoobaCheck("shopLeave", ShopLineSince(bye0, "ShopLeave"), FirstSince(bye0));
                Run.Scene("Main Menu");
                yield return WaitReal(4f);
                Run.Progress(1f);
                End(yoobaFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }

            bool ShopLineSince(int from, string moment)
            {
                for (int i = from; i < yoobaLines.Count; i++)
                    if (yoobaLines[i].StartsWith("yooba (" + moment, StringComparison.Ordinal) && !yoobaLines[i].Contains(", no clip"))
                        return true;
                return false;
            }

            string FirstSince(int from) => from < yoobaLines.Count ? yoobaLines[from] : "nothing said";

            public IEnumerator Menus()
            {
                var definition = GameDefinition.Instance;
                var main = definition.mainMenuConfig;
                var labels = main.labels;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                var screens = new List<(string label, bool shown, Func<bool> open, string back, string name)>
                {
                    (labels.levelSelect, main.showLevelSelectButton, () => Object.FindAnyObjectByType<LevelSelectionManager>() != null,
                        definition.levelSelectionConfig.layout.backText, "Level Select"),
                    (labels.videos, main.showVideosButton, () => Object.FindAnyObjectByType<VideoSelectionManager>() != null,
                        definition.videoSelectionConfig.layout.backText, "Video Select"),
                    (labels.shop, main.showUpgradeShopButton, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null,
                        definition.upgradeShopConfig.layout.backText, "Shop"),
                };
                int step = 0, steps = screens.Count + 1;
                foreach (var screen in screens)
                {
                    step++;
                    if (!screen.shown)
                    {
                        Run.Event("menu-skipped", screen.name + " (hidden by mainMenu.json)");
                        continue;
                    }
                    yield return ClickUntil(screen.label, screen.open, 20f);
                    if (Run.Done) yield break;
                    Run.Scene(screen.name);
                    Run.Event("menu-open", screen.name);
                    yield return WaitReal(1.5f);
                    if (CountButtons() < 2)
                        Run.Problem(PlaytestSeverity.Warning, $"{screen.name} shows {CountButtons()} button(s)");
                    yield return ClickUntil(screen.back, () => Object.FindAnyObjectByType<MainMenuManager>() != null && !screen.open(), 20f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                    Run.Event("menu-close", screen.name);
                    Run.Progress((float)step / steps);
                    yield return WaitReal(1f);
                }

                if (main.showSettingsButton)
                {
                    string done = definition.menuTheme.texts.done;
                    yield return ClickUntil(labels.settings, () => FindButton(done) != null, 15f);
                    if (Run.Done) yield break;
                    Run.Scene("Settings");
                    Run.Event("menu-open", "Settings");
                    yield return WaitReal(1f);
                    yield return SoundSettings();
                    if (Run.Done) yield break;
                    yield return ClickUntil(done, () => FindButton(done) == null, 15f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                    Run.Event("menu-close", "Settings");
                }
                yield return PreviewShaft();
                if (Run.Done) yield break;
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            /// <summary>
            /// The responsive layout on every main screen: each one is opened the way a player opens it (clicks and
            /// taps on the real buttons), then <see cref="MenuLayoutAudit"/> checks what is on screen. A level and the
            /// death scene are loaded directly (setup shortcuts). Each overlap, off-screen or too-small button is a bug.
            /// </summary>
            public IEnumerator Layout()
            {
                var definition = GameDefinition.Instance;
                var main = definition.mainMenuConfig;
                var labels = main.labels;
                var texts = definition.menuTheme.texts;
                string done = texts.done;
                Run.Metric("screenWidth", Screen.width);
                Run.Metric("screenHeight", Screen.height);
                Run.Event("layout-screen", $"{Screen.width}x{Screen.height} {(MenuScreen.Portrait ? "portrait" : "landscape")}, safe area {Screen.safeArea}, " +
                                           $"touch size {MenuScreen.MinTouchPixels(definition.menuTheme.responsive):0} px" +
                                           (MenuScreen.Portrait ? "" : " (landscape: run it with device \"phone\" for the portrait layout)"));
                const int steps = 9;
                int step = 0;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return Audit("Main Menu", ++step, steps);

                if (main.showLevelSelectButton)
                {
                    yield return ClickUntil(labels.levelSelect, () => Object.FindAnyObjectByType<LevelSelectionManager>() != null, 20f);
                    if (Run.Done) yield break;
                    yield return Audit("Level Select", ++step, steps);
                    yield return ClickUntil(definition.levelSelectionConfig.layout.backText,
                        () => Object.FindAnyObjectByType<MainMenuManager>() != null && Object.FindAnyObjectByType<LevelSelectionManager>() == null, 20f);
                    if (Run.Done) yield break;
                    yield return WaitReal(1f);
                }
                if (main.showSettingsButton)
                {
                    yield return ClickUntil(labels.settings, () => FindButton(done) != null, 15f);
                    if (Run.Done) yield break;
                    yield return Audit("Settings", ++step, steps);
                    yield return ClickUntil(done, () => FindButton(done) == null, 15f);
                    if (Run.Done) yield break;
                }
                yield return PreviewShaft(++step, steps);
                if (Run.Done) yield break;
                step++; // Yooba's shop over the Shaft (audited inside PreviewShaft)

                Run.Event("shortcut", "load level 1 directly");
                var load = LoadLevel(1);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(1), 45f, "level 1 to start");
                if (Run.Done) yield break;
                yield return Audit("Level 1 HUD", ++step, steps, 2.5f);

                // Pause with the HUD's own button (the one a thumb presses), then its settings.
                yield return ClickUntil("II", () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;
                yield return Audit("Pause", ++step, steps);
                yield return ClickUntil(texts.settings, () => FindButton(done) != null, 10f);
                if (Run.Done) yield break;
                yield return Audit("Pause Settings", ++step, steps);
                yield return ClickUntil(done, () => FindButton(done) == null, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(texts.resume, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;

                string death = definition.deaths.Count > 0 && !string.IsNullOrEmpty(definition.deaths[0].deathName) ? definition.deaths[0].deathName : "Death1";
                Run.Event("shortcut", $"load the death scene ({death}) directly");
                GameplayWorld.Instance?.Telemetry?.Finish("playtest left");
                Time.timeScale = 1f;
                SceneTransitionManager.Instance.LoadDeathScene(death);
                yield return Run.Until(() => Object.FindAnyObjectByType<DeathRuntime>() != null && FindButton(texts.rageQuit) != null, 45f, "the death scene");
                if (Run.Done) yield break;
                yield return Audit("Death", ++step, steps, 1.5f);
                yield return ClickUntil(texts.rageQuit, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 30f);
                if (Run.Done) yield break;
                Run.Scene("Main Menu");

                Run.Metric("layoutProblems", layoutProblems);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            int layoutProblems;

            /// <summary>Lets the screen settle (entrances), then checks its layout and reports each problem once per screen.</summary>
            IEnumerator Audit(string screen, int step, int steps, float settle = 1.5f, bool progress = true)
            {
                Run.Scene(screen);
                yield return WaitReal(settle);
                // Park the virtual mouse in a corner so no hovered button is mid-spring.
                input.MoveMouse(new Vector2(1f, 1f));
                yield return WaitReal(0.4f);
                if (Run.Done)
                    yield break;
                var result = MenuLayoutAudit.Check(GameDefinition.Instance.menuTheme.responsive);
                int buttons = 0;
                foreach (var box in result.Boxes)
                    if (box.Kind == MenuElementKind.Button)
                        buttons++;
                foreach (var problem in result.Problems)
                    Run.Problem(PlaytestSeverity.Bug, $"layout ({screen}, {Screen.width}x{Screen.height}): {problem}");
                foreach (var warning in result.Warnings)
                    Run.Problem(PlaytestSeverity.Polish, $"layout ({screen}, {Screen.width}x{Screen.height}): {warning}");
                layoutProblems += result.Problems.Count;
                Run.Metric("layout." + screen.Replace(" ", "") + ".problems", result.Problems.Count);
                Run.Event("layout", $"{screen}: {buttons} buttons and {result.Boxes.Count - buttons} panels/texts checked, " +
                                    (result.Problems.Count == 0 ? "no overlaps, all on screen and thumb-sized" : $"{result.Problems.Count} problem(s)"));
                if (progress)
                    Run.Progress((float)step / (steps + 1));
            }

            /// <summary>
            /// The website around the game: the game page has no credit (the game screen is unchanged), then each
            /// site page opens in a frame over the game, scrolled to its "Powered by HAWKES GEN" mark (ringed for the
            /// recording), and the landing page's Hawkes Biomedical link is checked too. The pages are opened by
            /// address, the way a visitor follows the site's links; outside a WebGL build there is no site to check.
            /// </summary>
            public IEnumerator Site()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                if (!SitePages.Available)
                {
                    Run.Event("site", "no website here (editor or a test): nothing to check");
                    Run.Progress(1f);
                    End(PlaytestOutcome.Done);
                    yield break;
                }

                int gameCredits = SitePages.GamePageCredits();
                Run.Event("game-page", gameCredits == 0 ? "the game page has no credit mark (unchanged)" : $"the game page has {gameCredits} credit element(s)");
                if (gameCredits > 0)
                    Run.Problem(PlaytestSeverity.Bug, $"the game page itself shows the Hawkes credit ({gameCredits} element(s)); it belongs on the site pages only");
                yield return WaitReal(1.5f);

                int passed = 0;
                for (int i = 0; i < SitePages.Pages.Length && !Run.Done; i++)
                {
                    var (path, name, landing, expect) = SitePages.Pages[i];
                    Run.Scene("Site: " + name);
                    SitePages.Open(path, expect);
                    string json = "";
                    yield return Run.Until(() => (json = SitePages.Result()).Length > 0, 25f, $"the {name} page to load");
                    if (Run.Done) break;
                    var check = JsonUtility.FromJson<SitePages.Check>(json);
                    bool ok = true;
                    if (!string.IsNullOrEmpty(check.error))
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {name} (/{path}): {check.error}");
                        ok = false;
                    }
                    else
                    {
                        ok &= Expect(name, path, "Powered by HAWKES GEN", "https://hawkesgen.com", check.gen, "powered by hawkes gen");
                        if (landing)
                            ok &= Expect(name, path, "Hawkes Biomedical", "https://hawkesbiomedical.com", check.parent, "hawkes biomedical");
                        ok &= SiteLayout(name, path, expect, check);
                    }
                    if (ok)
                        passed++;
                    Run.Progress((i + 1f) / (SitePages.Pages.Length + 1));
                    yield return WaitReal(2.5f); // on screen for the recording
                }
                SitePages.Close();
                if (Run.Done) yield break;
                Run.Metric("sitePagesChecked", SitePages.Pages.Length);
                Run.Metric("sitePagesWithCredit", passed);
                Run.Scene("Main Menu");
                Run.Event("site-done", $"{passed}/{SitePages.Pages.Length} site pages show the credit; back to the game");
                yield return WaitReal(1.5f);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            /// <summary>
            /// The page as laid out in the frame (the screen's size: a phone's on device "phone"): no link or button on
            /// another, none off the side, no sideways scrolling; the page's expected element (a new post's card); and
            /// the Descent's floor counter at 1,000 once scrolled to the bottom.
            /// </summary>
            bool SiteLayout(string page, string path, string expect, SitePages.Check check)
            {
                bool ok = true;
                var layout = check.layout;
                if (layout != null)
                {
                    Run.Event("site-layout", $"{page} at {layout.width}px: {(layout.overlaps?.Length ?? 0)} overlap(s), {(layout.offscreen?.Length ?? 0)} off screen, page {layout.pageWidth}px wide");
                    if (layout.overlaps != null && layout.overlaps.Length > 0)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) at {layout.width}px: links/buttons overlap: {string.Join("; ", layout.overlaps)}");
                        ok = false;
                    }
                    if (layout.offscreen != null && layout.offscreen.Length > 0)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) at {layout.width}px: off the side of the screen: {string.Join("; ", layout.offscreen)}");
                        ok = false;
                    }
                    if (layout.overflowX)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) is {layout.pageWidth}px wide on a {layout.width}px screen (scrolls sideways)");
                        ok = false;
                    }
                }
                if (!string.IsNullOrEmpty(expect))
                {
                    Run.Event("check", $"site {page}: {expect} {(check.expected ? "found" : "MISSING")}");
                    if (!check.expected)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) has no {expect}");
                        ok = false;
                    }
                }
                if (check.floor >= 0)
                {
                    Run.Event("check", $"site {page}: the floor counter reads {check.floor} at the bottom of the page");
                    if (check.floor != 1000)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}): scrolled to the bottom, the floor counter reads {check.floor}, not 1000");
                        ok = false;
                    }
                }
                return ok;
            }

            bool Expect(string page, string path, string what, string url, SitePages.Link link, string text)
            {
                if (link == null || string.IsNullOrEmpty(link.href))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) has no \"{what}\" link to {url}");
                    return false;
                }
                bool good = link.visible && (link.text ?? "").ToLowerInvariant().Contains(text);
                Run.Event("site-link", $"{page}: \"{link.text}\" -> {link.href}{(link.visible ? "" : " (not visible)")}");
                if (!good)
                    Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}): the {url} link reads \"{link.text}\"{(link.visible ? "" : " and isn't visible")}");
                return good;
            }

            /// <summary>
            /// Presses the page's "Tap to play" prompt, so the run isn't recorded behind it, and logs what the
            /// page says about audio. The page can only be pressed from script: a browser unlocks audio for a
            /// real tap only, so "audio" stays "suspended" unless the runner clicked the page itself.
            /// </summary>
            public IEnumerator TapSoundPrompt()
            {
                string before = PageSound.State();
                if (before == null)
                {
                    Run.Event("tap-prompt", "no page prompt here (editor or a page without hawkes-sound.js)");
                    yield break;
                }
                if (before.Length == 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, "the game page has no tap-to-play sound prompt (hawkes-sound.js not loaded)");
                    yield break;
                }
                bool tapped = PageSound.Tap();
                yield return WaitReal(0.5f);
                string after = PageSound.State();
                Run.Event("tap-prompt", (tapped ? "pressed the prompt: " : "the prompt was already gone: ") + after);
                Run.Metric("soundPrompt", tapped ? 1f : 0f);
                if (after != null && after.Contains("\"prompt\":\"shown\""))
                    Run.Problem(PlaytestSeverity.Bug, "the tap-to-play prompt stayed up after it was pressed: " + after);
            }

            /// <summary>In the open settings: Sound off and on again, Music one step down and up, checking each applies.</summary>
            IEnumerator SoundSettings()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                bool wasOn = GameSettings.SoundOn;
                float music = GameSettings.MusicVolume;

                yield return ClickIn(texts.sound, wasOn ? texts.on : texts.off, () => GameSettings.SoundOn != wasOn);
                if (Run.Done) yield break;
                bool muted = !GameSettings.SoundOn && AudioListener.volume == 0f;
                Run.Event("settings", $"Sound {(GameSettings.SoundOn ? "on" : "off")}, listener volume {AudioListener.volume:0.##}, {GameSettings.SoundKey} = {GameSettings.SoundJson}");
                if (wasOn && !muted)
                    Run.Problem(PlaytestSeverity.Bug, $"Sound Off didn't mute the game (listener volume {AudioListener.volume:0.##})");
                yield return WaitReal(0.5f);
                yield return ClickIn(texts.sound, GameSettings.SoundOn ? texts.on : texts.off, () => GameSettings.SoundOn == wasOn);
                if (Run.Done) yield break;
                Run.Event("settings", $"Sound {(GameSettings.SoundOn ? "on" : "off")}, listener volume {AudioListener.volume:0.##}");

                string step = music > 0.05f ? "-" : "+";
                yield return ClickIn(texts.musicVolume, step, () => !Mathf.Approximately(GameSettings.MusicVolume, music));
                if (Run.Done) yield break;
                Run.Event("settings", $"Music {music:0.#} -> {GameSettings.MusicVolume:0.#}, {GameSettings.SoundKey} = {GameSettings.SoundJson}");
                float stepped = GameSettings.MusicVolume;
                yield return WaitReal(0.5f);
                yield return ClickIn(texts.musicVolume, step == "-" ? "+" : "-", () => !Mathf.Approximately(GameSettings.MusicVolume, stepped));
                if (Run.Done) yield break;
                Run.Event("settings", $"Music back to {GameSettings.MusicVolume:0.#}");
                Run.Metric("soundSettings", 1f);
            }

            /// <summary>Clicks the button labeled 'text' in the settings row 'row' until 'effect' is true.</summary>
            IEnumerator ClickIn(string row, string text, Func<bool> effect)
            {
                MenuButton button = null;
                yield return Run.Until(() => (button = FindButtonInRow(row, text)) != null, 10f, $"the \"{text}\" button for {row}");
                if (Run.Done)
                    yield break;
                // The panel springs in when it opens, so a button's place moves for a moment: let it settle, then
                // aim at the button where it is now, and click again if the first click found it moving.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    yield return WaitReal(0.6f);
                    if (Run.Done)
                        yield break;
                    button = FindButtonInRow(row, text) ?? button;
                    input.MoveMouse(ScreenPoint(button));
                    yield return null;
                    yield return null;
                    input.SetMouseButton(true);
                    yield return null;
                    yield return null;
                    input.SetMouseButton(false);
                    Run.Event("click", $"{row}: {text}");
                    float end = Time.realtimeSinceStartup + 1.5f;
                    while (Time.realtimeSinceStartup < end && !Safe(effect))
                        yield return null;
                    if (Safe(effect))
                        yield break;
                }
                yield return Run.Until(effect, 3f, $"clicking {row} \"{text}\" to change the setting");
            }

            static MenuButton FindButtonInRow(string row, string text)
            {
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                {
                    if (button == null || !button.isActiveAndEnabled || button.Label == null)
                        continue;
                    // A settings row is named after its label; its buttons sit a level or two below it.
                    var parent = button.transform.parent;
                    for (int up = 0; up < 3 && parent != null && parent.name != row; up++)
                        parent = parent.parent;
                    if (parent == null || parent.name != row)
                        continue;
                    if (string.Equals(Tags.Replace(button.Label.text ?? "", "").Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                        return button;
                }
                return null;
            }

            // ─── Playing a level ─────────────────────────────────────────

            static bool LevelRunning(int n)
            {
                var world = GameplayWorld.Instance;
                return world != null && world.LevelNumber == n && world.Player != null
                       && GameClock.Instance != null && GameClock.Instance.Time > 0f;
            }

            /// <summary>
            /// Plays level n with the bot until it is completed, the player dies, the level's time runs out, or
            /// 'playSeconds' of game time pass (0 = no limit). Sets lastOutcome: completed, died, timeout, time up,
            /// softlock, scene changed. 'during' may return a coroutine to run mid-level (pause checks). With
            /// 'waitOutDeath' a death plays out like a player's: the bot may undo it with a rewind, else the game
            /// leaves for the death scene; otherwise the level ends at the fatal hit.
            /// </summary>
            IEnumerator PlayLevel(int n, float playSeconds, Func<float, IEnumerator> during = null,
                float progressFrom = 0f, float progressTo = 1f, float realBudget = float.MaxValue, bool waitOutDeath = false,
                Func<bool> until = null)
            {
                lastOutcome = "not started";
                Run.Scene("Level " + n);
                yield return Run.Until(() => LevelRunning(n), 45f, $"level {n} to start");
                if (Run.Done)
                    yield break;

                var world = GameplayWorld.Instance;
                var config = GameDefinition.Instance.GetLevel(n);
                if (bot != null)
                    Object.Destroy(bot);
                bot = PlaytestBot.Attach(world, (uint)(1000 * options.seed + n), input);
                if (bot != null && takeShortcuts)
                {
                    bot.takeShortcuts = true;
                    Run.Event("bot", $"level {n}: the bot takes Hell's shortcuts (presses 2 on a shortcut's choice)");
                }
                if (bot != null && reckless)
                {
                    bot.reckless = true;
                    bot.useRewind = false;
                    Run.Event("bot", $"level {n}: the bot plays to lose (walks into the shots, no dodging, firing or rewinding)");
                }
                Time.timeScale = Speed;
                Run.Event("level-start", $"level {n}: {config.levelName}");

                var health = world.Player.GetComponentInChildren<PlayerHealth>();
                float limit = Mathf.Max(30f, config.duration) + 10f;
                if (playSeconds > 0f)
                    limit = Mathf.Min(limit, playSeconds);
                float started = Time.realtimeSinceStartup;
                float realLimit = Mathf.Min(limit / Speed + 60f, realBudget);
                float lastClock = -1f, clockMovedAt = started, nextScan = 0f, shownProgress = -1f;
                string outcome = "timeout";
                bool dying = false;
                while (!Run.Done)
                {
                    if (world == null) { outcome = dying ? "died" : "scene changed"; break; }
                    var clock = GameClock.Instance;
                    float now = clock != null ? clock.Time : 0f;
                    if (world.Director != null && world.Director.Completed) { outcome = "completed"; break; }
                    if (slowMotionUntil > 0f && Time.realtimeSinceStartup >= slowMotionUntil)
                        EndSlowMotion();
                    if (until != null && until()) { outcome = "checked"; break; }
                    if (health != null && !health.IsAlive)
                    {
                        if (!dying)
                        {
                            dying = true;
                            deaths++;
                            Run.Metric("deaths", deaths);
                            Run.Event("death", $"level {n} at {now:0.0} s");
                        }
                        if (!waitOutDeath) { outcome = "died"; break; }
                    }
                    else if (dying)
                    {
                        dying = false;
                        Run.Event("death-undone", $"level {n}: rewound out of it");
                    }
                    if (now >= limit) { outcome = playSeconds > 0f && playSeconds < Mathf.Max(30f, config.duration) + 10f ? "time up" : "timeout"; break; }
                    if (Time.realtimeSinceStartup - started > realLimit) { outcome = "timeout"; break; }

                    // Softlock: the clock stands still while nothing (pause, picker, results, rewind) holds it.
                    var hud = world.Hud;
                    bool held = dying || hud != null && (hud.Paused || hud.PickerOpen || hud.ResultsOpen) || (clock != null && clock.IsRewinding);
                    if (held || Mathf.Abs(now - lastClock) > 0.0001f)
                    {
                        lastClock = now;
                        clockMovedAt = Time.realtimeSinceStartup;
                    }
                    else if (Time.realtimeSinceStartup - clockMovedAt > SoftlockSeconds)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"softlock: level {n}'s clock stopped at {now:0.0} s (phase \"{world.Director?.PhaseName}\")");
                        outcome = "softlock";
                        break;
                    }

                    if (Time.realtimeSinceStartup >= nextScan)
                    {
                        nextScan = Time.realtimeSinceStartup + 0.5f;
                        ScanPlaceholders(world);
                        Run.Note($"level {n} {world.Director?.PhaseName} t={now:0}");
                    }
                    float progress = Mathf.Lerp(progressFrom, progressTo, Mathf.Clamp01(now / limit));
                    if (progress - shownProgress >= 0.01f)
                    {
                        shownProgress = progress;
                        Run.Progress(progress);
                    }

                    var extra = during?.Invoke(now);
                    if (extra != null)
                        yield return extra;
                    else
                        yield return null;
                }

                if (bot != null)
                {
                    botRewinds += bot.RewindsStarted;
                    Run.Metric("botRewinds", botRewinds);
                    Object.Destroy(bot);
                }
                bot = null;
                input.ReleaseAll();
                slowMotionUntil = 0f;
                Time.timeScale = 1f;
                // Ends the level's log (the level may already have: complete, died). Writes Logs/Playtest in the editor.
                if (world != null && world.Telemetry != null)
                    world.Telemetry.Finish(outcome);
                lastOutcome = outcome;
            }

            /// <summary>PlayLevel's bot plays to lose (the waystone scenario's death through real input).</summary>
            bool reckless;
            /// <summary>PlayLevel's bot takes a shortcut when one is offered (the shortcut scenario).</summary>
            bool takeShortcuts;

            readonly HashSet<string> placeholderIds = new HashSet<string>();

            void ScanPlaceholders(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || placeholderIds.Contains(e.DefId) || e.transform.Find("Placeholder") == null)
                        continue;
                    placeholderIds.Add(e.DefId);
                    Run.Problem(PlaytestSeverity.Polish, $"placeholder art: \"{e.DefId}\" is drawn as a labeled circle (no visual.animator)");
                }
            }

            // ─── Menus and keys through the virtual devices ──────────────

            IEnumerator ToMainMenu()
            {
                if (Object.FindAnyObjectByType<MainMenuManager>() == null)
                {
                    Run.Event("shortcut", "load the main menu");
                    if (GameplayWorld.Instance != null)
                        GameplayWorld.Instance.Telemetry?.Finish("playtest left");
                    Time.timeScale = 1f;
                    SceneTransitionManager.Instance.LoadMainMenu();
                }
                yield return Run.Until(() => Object.FindAnyObjectByType<MainMenuManager>() != null && CountButtons() > 0, 45f, "the main menu");
                if (Run.Done)
                    yield break;
                Run.Scene("Main Menu");
                yield return WaitReal(1.5f);
            }

            IEnumerator Tap(Key key)
            {
                input.SetKey(key, true);
                yield return null;
                yield return null;
                input.SetKey(key, false);
                yield return null;
            }

            IEnumerator WaitReal(float seconds)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                    yield return null;
            }

            /// <summary>Clicks the button labeled 'text' with the virtual mouse until 'effect' is true (one retry).</summary>
            IEnumerator ClickUntil(string text, Func<bool> effect, float timeout)
            {
                for (int attempt = 0; attempt < 2 && !Run.Done; attempt++)
                {
                    MenuButton button = null;
                    if (attempt == 0)
                        yield return Run.Until(() => (button = FindButton(text)) != null && button.IsInteractable(), timeout, $"the \"{text}\" button");
                    else
                    {
                        // The first click may still be working (New Game plays the house explosion before the video
                        // loads, and the menu animates out): if the button is gone, wait for the effect, don't fail.
                        float gone = Time.realtimeSinceStartup + 2f;
                        while (!Run.Done && Time.realtimeSinceStartup < gone && !((button = FindButton(text)) != null && button.IsInteractable()))
                            yield return null;
                        if (button == null || !button.IsInteractable())
                            break;
                    }
                    if (Run.Done)
                        yield break;
                    yield return WaitReal(0.4f); // entrance animation
                    button = FindButton(text);
                    if (button != null)
                    {
                        input.MoveMouse(ScreenPoint(button));
                        yield return null;
                        yield return null;
                        input.SetMouseButton(true);
                        yield return null;
                        yield return null;
                        input.SetMouseButton(false);
                        Run.Event("click", text);
                    }
                    float end = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        if (Safe(effect))
                            yield break;
                        yield return null;
                    }
                }
                yield return Run.Until(effect, timeout, $"clicking \"{text}\" to do something");
            }

            static bool Safe(Func<bool> test)
            {
                try { return test(); }
                catch { return false; }
            }

            static Vector2 ScreenPoint(Component button)
            {
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                canvas = canvas != null ? canvas.rootCanvas : null;
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            }

            static readonly Regex Tags = new Regex("<[^>]*>");

            static MenuButton FindButton(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;
                MenuButton partial = null;
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                {
                    if (button == null || !button.isActiveAndEnabled || button.Label == null)
                        continue;
                    string label = Tags.Replace(button.Label.text ?? "", "").Trim();
                    if (string.Equals(label, text.Trim(), StringComparison.OrdinalIgnoreCase))
                        return button;
                    if (partial == null && label.IndexOf(text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        partial = button;
                }
                return partial;
            }

            static int CountButtons()
            {
                int count = 0;
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                    if (button != null && button.isActiveAndEnabled)
                        count++;
                return count;
            }

            static string SceneName()
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return scene.IsValid() ? scene.name : "?";
            }
        }

        /// <summary>
        /// The game page's tap-to-play sound prompt (Tools/Web/hawkes-sound.js, through Plugins/WebGL/HawkesSound.jslib).
        /// State() is the page's JSON, "" on a page without the prompt, null outside a WebGL build.
        /// </summary>
        static class PageSound
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")]
            static extern int HawkesSoundTap();

            [DllImport("__Internal")]
            static extern string HawkesSoundState();

            public static bool Tap() => HawkesSoundTap() != 0;
            public static string State() => HawkesSoundState() ?? "";
#else
            public static bool Tap() => false;
            public static string State() => null;
#endif
        }

        /// <summary>
        /// The website pages around the game (Plugins/WebGL/HawkesSite.jslib), opened in a frame over the game page.
        /// The credit on them is installed by the deploy (GameDeployWindow step 7c).
        /// </summary>
        static class SitePages
        {
            /// <summary>
            /// Paths from the site root, a name for the timeline, whether it's the landing page, and a CSS selector the
            /// page must have ("" for none). The lore pages and the engine post come from Tools/Web/site (deploy step 7d).
            /// </summary>
            public static readonly (string path, string name, bool landing, string expect)[] Pages =
            {
                ("", "Landing", true, ""),
                ("blog/index.html", "Blog", false, "a[href=\"posts/the-engine.html\"]"),
                ("blog/development.html", "Development", false, "a[href=\"posts/the-engine.html\"]"),
                ("blog/posts/the-engine.html", "Blog post: The Engine", false, "h1"),
                ("blog/lore.html", "Lore: The Descent", false, "[data-hell-floor]"),
                ("blog/lore/story/prologue.html", "Lore: The Prologue", false, ""),
                ("blog/lore/worlds/the-shaft.html", "Lore: The Shaft", false, ""),
                ("blog/lore/characters/hero.html", "Lore: The Hero", false, ""),
                ("blog/lore/characters/overlord.html", "Lore: The Overlord", false, ""),
                ("blog/lore/characters/yooba.html", "Lore: Yooba", false, ".yooba-portrait"),
                ("blog/lore/characters/magma-king.html", "Lore: The Magma King", false, ""),
                ("blog/lore/characters/imps.html", "Lore: Imps", false, ""),
                ("blog/lore/hazards/the-wastes.html", "Lore: The Wastes' Hazards", false, ""),
                ("blog/lore/systems/rules-of-hell.html", "Lore: The Rules of Hell", false, ""),
                ("blog/lore/worlds/cracked-road.html", "Lore: The Cracked Road", false, ".warden"),
                ("blog/posts/diamonds.html", "Blog post: Diamonds", false, ""),
                ("blog/posts/resend-saved-my-sanity.html", "Blog post without a footer", false, ""),
                ("offline.html", "Offline", false, ""),
            };

            [Serializable]
            public class Layout
            {
                public int width;
                public int pageWidth;
                public bool overflowX;
                public string[] overlaps;
                public string[] offscreen;
            }

            [Serializable]
            public class Link
            {
                public string href;
                public string text;
                public bool visible;
            }

            [Serializable]
            public class Check
            {
                public string path;
                public string url;
                public int status;
                public string error;
                public string title;
                public Link gen;
                public Link parent;
                public Layout layout;
                public bool expected = true;
                public int floor = -1;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")]
            static extern void HawkesSiteOpen(string path, string expect);

            [DllImport("__Internal")]
            static extern string HawkesSiteResult();

            [DllImport("__Internal")]
            static extern void HawkesSiteClose();

            [DllImport("__Internal")]
            static extern int HawkesSiteGamePageCredits();

            public static bool Available => true;
            public static void Open(string path, string expect) => HawkesSiteOpen(path, expect ?? "");
            public static string Result() => HawkesSiteResult() ?? "";
            public static void Close() => HawkesSiteClose();
            public static int GamePageCredits() => HawkesSiteGamePageCredits();
#else
            public static bool Available => false;
            public static void Open(string path, string expect) { }
            public static string Result() => "";
            public static void Close() { }
            public static int GamePageCredits() => 0;
#endif
        }

        /// <summary>Frames per second, sampled every second in real time.</summary>
        sealed class FpsSampler : MonoBehaviour
        {
            int frames;
            float since;
            double sum;
            public int Samples { get; private set; }
            public float Worst { get; private set; } = float.MaxValue;
            public float Average => Samples > 0 ? (float)(sum / Samples) : 0f;

            void Update()
            {
                frames++;
                since += Time.unscaledDeltaTime;
                if (since < 1f)
                    return;
                float value = frames / since;
                frames = 0;
                since = 0f;
                // The first seconds include loading; skip them.
                if (Time.realtimeSinceStartup < 5f)
                    return;
                sum += value;
                Samples++;
                Worst = Mathf.Min(Worst, value);
            }
        }
    }
}
