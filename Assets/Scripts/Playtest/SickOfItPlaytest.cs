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
using RewindPerf = SickOfIt.Core.Rewind.RewindPerf;

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
    /// video3 (Video3 Hell's Ad Break loaded directly after Floor 1000 plays with its clock running, Escape skips it into Yooba's hello, and the Videos card replays it to its end).
    /// video-2 (Video2 loaded directly and watched, not skipped: the hero's fall overlay appears at the burning door,
    /// spins and shrinks into the tunnel and is gone by ~14.5 s; R held mid-spin plays it backward; Escape skips).
    /// portal-audio (the prologue's portal scene sounds and the Meadow's lines played in order on the main menu).
    /// story-path (New Game through the story path: Video1, the Meadow, Video2, the Landing with its floor counter
    /// and the 999 beat, Video3 (Hell's Ad Break), the Spill, then the level select with the story levels in order).
    /// all-levels (every level in game.json, each opened directly with a time limit, a table of cleared / died / stuck;
    /// at most ~4 min per level, no new level after 38 min, all done by 43 (the runner encodes the video after), the rest "not reached (time budget)"),
    /// off-path (the same over the levels after the path: the old 10-floor stages and the arcade levels) and
    /// floors-A-B (the same over one stretch of the path, e.g. floors-1000-991).
    /// Every scenario starts by pressing the page's "Tap to play" sound prompt (<see cref="PageSound"/>).
    /// </summary>
    public static partial class SickOfItPlaytest
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

        /// <summary>Real seconds with no progress (same cell, phase and path section) after which a level ends as "stalled" with its place named.</summary>
        public static float StallSeconds = 240f;
        /// <summary>A hit/rewind loop at one spot ends the level as stuck after this long (real seconds).</summary>
        public static float LoopSeconds = 60f;

        /// <summary>The level-N scenario's last result, for the PlayMode playtest report.</summary>
        public static string LastLevelOutcome { get; private set; }

        static Session active;

        /// <summary>The analytics scenario's level: the Meadow, where a death just plays the level again (no run to end).</summary>
        public const int AnalyticsLevel = 1;

        /// <summary>
        /// The rewind-perf scenario: a busy Circle X floor (the Magma King's arena: boulders, shockwaves, fireballs and his
        /// imp guards), played forward, then rewound for a few seconds, <see cref="RewindPerfRewinds"/> times, with
        /// RewindPerf's frame times compared.
        /// </summary>
        public const string RewindPerfId = "rewind-perf", RewindPerfLevel = MagmaKingLevel;
        public const int RewindPerfRewinds = 3;
        /// <summary>The daily budget run: rewind-perf, then a sample of the climb with the balance report (Level 1, Floors 1000-998).</summary>
        public const string DailyId = "daily";
        public const float DailySeconds = 1500f;
        public static List<int> DailyLevels() => ClimbStartLevels().GetRange(0, Math.Min(4, ClimbStartLevels().Count));
        /// <summary>Real seconds R is held per rewind (the juice is topped up first, a setup shortcut) and game seconds played before each.</summary>
        public const float RewindPerfHold = 3f, RewindPerfPlay = 12f;

        /// <summary>The level the imp-death scenario plays: Level 1's "imps" phase brings four, one at a time first.</summary>
        public const int ImpLevel = 1;

        /// <summary>The aim-guide scenario: the level it plays (imps to shoot at once), and the spread Grudge it gives for the multi-shot half.</summary>
        public const string AimGuideScenario = "aim-guide";
        public const int AimGuideLevel = 1;
        public const string AimGuideSpread = "spreadShot";
        public const int AimGuideSpreadLevels = 2;
        const int AimGuideVolleys = 12;
        const float AimGuideToleranceDegrees = 0.5f;

        /// <summary>
        /// A Warden's fight (Docs/Story.md, "The Magma King's fight", "The Spire Fiend's fight"): the level that holds it, the
        /// boss entity, the animation states he must be seen in, his lines (text, audio.json sound, who must voice it), each
        /// attack and the warning glow(s) that must come at least ArenaTelegraphSeconds before it (several warnings: any of
        /// them, comma-separated), the signals the fight emits in order, the boss states at half health and at the enrage,
        /// the adds allowed only in phase 2, and the intro and defeat lines the arena checks want voiced.
        /// </summary>
        public sealed class WardenFight
        {
            public string Level, Id, Name, Scenario;
            public string[] States;
            public (string beat, string text, string sound, string voice)[] Lines;
            public (string attack, string warning, string what)[] Telegraphs;
            public (string signal, string beat)[] Signals;
            /// <summary>Signals: the intro, the fight, half health, phase 2, the enrage, a phase-3 attack that may not come before he falls, the defeat.</summary>
            public string Intro, Fight, Half, Phase2, Enrage, Optional, Defeat;
            /// <summary>The boss's state machine at half health and at the enrage (their names start with these).</summary>
            public string HalfState, EnrageState;
            public string Adds, AddsWhat;
            public string[] IntroLines;
            public string LastLine, Fallen;
            public float Seconds = 300f;
        }

        /// <summary>The magma-king scenario: Floor 901, the Slag Throne (Docs/Story.md "The Magma King's fight").</summary>
        public const string MagmaKingLevel = "Floor901", MagmaKingId = "magmaKing";
        public static readonly WardenFight MagmaKing = new WardenFight
        {
            Level = MagmaKingLevel, Id = MagmaKingId, Name = "Magma King", Scenario = "magma-king",
            States = new[] { "Idle", "Throw", "SlamSeat", "Hurt", "Rise", "Enrage", "IdleStand", "Slam", "Breath", "HurtStand" },
            Lines = new[]
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
            },
            Telegraphs = new[]
            {
                ("magmaBoulder", "mkWarnHand", "boulder"),
                ("slamShockwave", "mkWarnFloor", "shockwave"),
                ("lavaFlood", "mkWarnFlood", "lava flood"),
                ("fireball", "mkWarnMouth", "fire breath"),
            },
            Signals = new[]
            {
                ("mkIntro", "intro: the throne rises"),
                ("mkFight", "phase 1: from the throne"),
                ("mkStand", "half health: he stands"),
                ("mkPhase2", "phase 2: standing, with his guard"),
                ("mkEnrage", "a third: the enrage"),
                ("mkFlood", "phase 3: the flood"),
                ("mkDefeat", "defeat"),
                ("waystoneLit", "the Waystone lights"),
            },
            Intro = "mkIntro", Fight = "mkFight", Half = "mkStand", Phase2 = "mkPhase2", Enrage = "mkEnrage", Optional = "mkFlood", Defeat = "mkDefeat",
            HalfState = "rising", EnrageState = "enraging",
            Adds = "impGuard", AddsWhat = "imp guard",
            IntroLines = new[] { "FLOOR 901! THE WARDEN OF THE WASTES!", "THE MAGMA KING",
                "Mm-hm-hm. An audience, at last. Welcome to the Slag Throne, little spark.", "He's not getting up, is he." },
            LastLine = "The show... must go... on...", Fallen = "THE MAGMA KING HAS FALLEN!",
        };

        /// <summary>The spire-fiend scenario: Floor 801, the Spire's Heart (Docs/Story.md "The Spire Fiend's fight").</summary>
        public const string SpireFiendLevel = "Floor801", SpireFiendId = "spireFiend";
        public static readonly WardenFight SpireFiend = new WardenFight
        {
            Level = SpireFiendLevel, Id = SpireFiendId, Name = "Spire Fiend", Scenario = "spire-fiend", Seconds = 420f,
            States = new[] { "Rise", "Idle", "Slam", "Throw", "Hurt", "Enrage", "IdleEnraged", "SlamEnraged" },
            Lines = new[]
            {
                ("intro", "Hmmmm. There you are. I heard every step. A hundred floors of them.", "vo_sf_intro", "spireFiend"),
                ("hero intro", "He's been listening. Course he has.", "vo_sf_hero_intro", "player"),
                ("phase 1", "Climb, then. Nothing climbs higher than me.", "vo_sf_phase1", "spireFiend"),
                ("hurt", "Hm. You chipped me.", "vo_sf_hurt1", "spireFiend"),
                ("shed", "You don't climb MY spire.", "vo_sf_shed", "spireFiend"),
                ("whelps", "Up. Out of the cracks.", "vo_sf_whelps", "spireFiend"),
                ("enrage", "ENOUGH. Everything here comes DOWN.", "vo_sf_enrage", "spireFiend"),
                ("needle field", "Floor's all spikes. ...Let's try that again.", "vo_sf_hero_field", "player"),
                ("hurt 2", "Stop. Climbing.", "vo_sf_hurt2", "spireFiend"),
                ("defeat", "Nothing... higher... than... me...", "vo_sf_defeat", "spireFiend"),
                ("fallen", "THE SPIRE FIEND HAS FALLEN!", "vo_sf_fallen", "announcer"),
                ("hero defeat", "He never got off that rock. I almost respect it.", "vo_sf_hero_defeat", "player"),
            },
            Telegraphs = new[]
            {
                ("sfSpear", "sfWarnHand", "spire spear"),
                ("sfNeedle", "sfWarnRow,sfWarnSpot,sfWarnField", "needles"),
                ("shard", "spireShard", "falling shard"),
            },
            Signals = new[]
            {
                ("sfIntro", "intro: he rises out of his spire"),
                ("sfFight", "phase 1: rooted"),
                ("sfShed", "half health: the spire sheds"),
                ("sfPhase2", "phase 2: shards and whelps"),
                ("sfEnrage", "a third: the enrage"),
                ("sfField", "phase 3: the needle field"),
                ("sfDefeat", "defeat"),
                ("waystoneLit", "the Waystone lights"),
            },
            Intro = "sfIntro", Fight = "sfFight", Half = "sfShed", Phase2 = "sfPhase2", Enrage = "sfEnrage", Optional = "sfField", Defeat = "sfDefeat",
            HalfState = "shedding", EnrageState = "enraging",
            Adds = "sfWhelp", AddsWhat = "whelp",
            IntroLines = new[] { "FLOOR 801! THE WARDEN OF THE ROAD!", "THE SPIRE FIEND",
                "Hmmmm. There you are. I heard every step. A hundred floors of them.", "He's been listening. Course he has." },
            LastLine = "Nothing... higher... than... me...", Fallen = "THE SPIRE FIEND HAS FALLEN!",
        };

        /// <summary>The bridge-wraith scenario: Floor 701, the Great Span (Docs/Story.md "The Bridge Wraith's fight"). No art is generated yet (specs priced, not approved), so no animation states are asked for.</summary>
        public const string BridgeWraithLevel = "Floor701", BridgeWraithId = "bridgeWraith";
        public static readonly WardenFight BridgeWraith = new WardenFight
        {
            Level = BridgeWraithLevel, Id = BridgeWraithId, Name = "Bridge Wraith", Scenario = "bridge-wraith", Seconds = 420f,
            States = new string[0],
            Lines = new[]
            {
                ("intro", "Oh. Oh, hello. Someone needs to cross. ...Sorry. It has been so long.", "vo_br_intro", "bridgeWraith"),
                ("hero intro", "She's already sorry. That's worse.", "vo_br_hero_intro", "player"),
                ("phase 1", "Mind the gap. ...There is not one. Sorry.", "vo_br_phase1", "bridgeWraith"),
                ("hurt", "Oh! That was the lantern. Sorry. Sorry.", "vo_br_hurt1", "bridgeWraith"),
                ("snuff", "I will put it out. It is the only way to do it right.", "vo_br_snuff", "bridgeWraith"),
                ("lights", "Come along. Come along, little lights.", "vo_br_flies", "bridgeWraith"),
                ("enrage", "Unwritten. Unwritten. Please, do not rush me. I will lose my place.", "vo_br_enrage", "bridgeWraith"),
                ("unwriting", "The bridge is unwriting itself. Behind me. Of course it is.", "vo_br_hero_field", "player"),
                ("hurt 2", "Please. I will write it wrong.", "vo_br_hurt2", "bridgeWraith"),
                ("defeat", "Oh. ...Thank you for crossing.", "vo_br_defeat", "bridgeWraith"),
                ("fallen", "THE BRIDGE WRAITH HAS FALLEN!", "vo_br_fallen", "announcer"),
                ("hero defeat", "She said thank you. Hell, first time.", "vo_br_hero_defeat", "player"),
            },
            Telegraphs = new[]
            {
                ("brLampBolt", "brWarnLamp", "lantern fan"),
                ("brMarkBolt", "brWarnLamp", "marked burst"),
                ("brRuneColumn", "brWarnRune", "falling rune"),
                ("brWallHit", "brWarnWall", "rune wall"),
            },
            Signals = new[]
            {
                ("brIntro", "intro: she rises over the Span"),
                ("brFight", "phase 1: The Lantern"),
                ("brSnuff", "half health: Snuffed, the Span goes dark"),
                ("brPhase2", "phase 2: her little lights and afterimages"),
                ("brEnrage", "a third: Unwritten"),
                ("brUnwrite", "phase 3: the walls of runes"),
                ("brDefeat", "defeat"),
                ("waystoneLit", "the Waystone lights"),
            },
            Intro = "brIntro", Fight = "brFight", Half = "brSnuff", Phase2 = "brPhase2", Enrage = "brEnrage", Optional = "brUnwrite", Defeat = "brDefeat",
            HalfState = "snuffed", EnrageState = "unwriting",
            Adds = "lanternfly", AddsWhat = "lanternfly",
            IntroLines = new[] { "FLOOR 701! THE WARDEN OF THE RUNE BRIDGES!", "THE BRIDGE WRAITH",
                "Oh. Oh, hello. Someone needs to cross. ...Sorry. It has been so long.", "She's already sorry. That's worse." },
            LastLine = "Oh. ...Thank you for crossing.", Fallen = "THE BRIDGE WRAITH HAS FALLEN!",
        };

        public static readonly WardenFight[] Wardens = { MagmaKing, SpireFiend, BridgeWraith };

        /// <summary>What a Floor 801 arena run checks (level-61 and spire-fiend).</summary>
        const string SpireFiendArena = "the title banner (\"FLOOR 801: THE SPIRE'S HEART\") with its voice, then the intro (input and rewind " +
            "locked, the hum, the camera on the lone spire as he rises out of it, the announcer's \"FLOOR 801! THE WARDEN OF THE ROAD!\" and " +
            "\"THE SPIRE FIEND\", his whispered greeting, the hero's one-liner, the boss bar), the fight's three phases in order from its signals " +
            "(sfIntro, sfFight; at half health the spire sheds (sfShed) and phase 2 brings shards and whelps (sfPhase2), the only phase with adds; " +
            "at a third the enrage (sfEnrage), phase 3's needle field (sfField); sfDefeat, waystoneLit), his health at the shedding (half or less) and " +
            "the enrage (a third or less), every attack after its warning glow 1+ s ahead (spire spear after the glow at his shoulder, needles after " +
            "their glowing cracks, shards after the glowing seam), no more than 4 enemies at once, every line with its voice; his defeat: slow motion, " +
            "his last line, \"THE SPIRE FIEND HAS FALLEN!\", the counter on 801 and \"WAYSTONE LIT: FLOOR 801\", the Grudge pick";

        /// <summary>The Warden whose fight is level n (by its file name), or null.</summary>
        public static WardenFight WardenOf(int n)
        {
            var definition = GameDefinition.Instance;
            var level = definition != null && n > 0 && n <= definition.TotalLevels ? definition.GetLevel(n) : null;
            return level != null ? Array.Find(Wardens, w => w.Level == level.levelName) : null;
        }

        const float ArenaTelegraphSeconds = 1f;

        /// <summary>The cutscene scenario: the scene it plays (content/scenes) and the level whose stage it plays on.</summary>
        public const string CutsceneScene = "cutsceneTest";
        public const string GrudgeScene = "grudgeTest";
        public const int CutsceneLevel = 1;

        /// <summary>The climb scenario: the test floor it plays (content/scenes, a floor with a path) and the level whose stage it plays on.</summary>
        public const string ClimbScene = "climbTest";
        public const int ClimbLevel = 2;
        const float ClimbSeconds = 200f;

        /// <summary>The one-way scenario: the test floor it plays (content/scenes) and the level whose stage it plays on.</summary>
        public const string OneWayScene = "oneWayTest";
        public const int OneWayLevel = 2;
        const float OneWaySeconds = 90f;

        /// <summary>The mechanics scenario: Hell's tricks on a test bench (content/scenes) played on this level's stage.</summary>
        public const string MechanicsScene = "mechanicsTest";
        public const int MechanicsLevel = 1;
        const float MechanicsSeconds = 150f;

        /// <summary>The level whose level-N scenario also watches the portal scene at its end (Level1.json, phases portalBeam to portalClose) and the first-run Shaft plunge (Video2 stays in Rewatch).</summary>
        public const int PortalLevel = 1;

        /// <summary>The video-2 scenario: the video it watches and the timeline overlay it checks (the hero falling down the fire tunnel).</summary>
        public const int OverlayVideo = 2;
        public const string OverlayName = "heroFall";

        /// <summary>
        /// The story-path scenario: each story level in order and the video that must play before it (0 = none: the level
        /// starts straight after the Shaft that follows the one before). Docs/Story.md, "Level numbers".
        /// </summary>
        static readonly (int video, int level)[] StoryPath =
            // First run: no Video1 before Level 1, no Video2 before Floor 1000 (Shaft plunge instead; Docs/Story.md "First five minutes").
            { (0, 1), (0, 2), (3, 3), (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9), (0, 10), (0, 11), (0, 12), (0, 13), (0, 14), (0, 15), (0, 16), (0, 17),
              (0, 18), (0, 19), (0, 20), (0, 21), (0, 22), (0, 23), (0, 24), (0, 25), (0, 26), (0, 27), (0, 28), (0, 29), (0, 30), (0, 31) };
        /// <summary>
        /// Circle X's played route (Docs/LevelDesign.md, "The route through Circle X"): its shortcuts by level, the floor each lets him
        /// out on, and the level it leads to (the Sack Lift on Floor 991 to Floor 960, the King's Chimney on Floor 951 to Floor 910).
        /// The story path and the circle-x-route scenario take both; the levels they skip are the long way's stand-ins, off the path.
        /// </summary>
        static readonly (int level, string id, int toFloor, int next)[] RouteShortcuts =
            { (11, "sackLift", 960, 12), (21, "kingsChimney", 910, 22), (41, "sparedDemon", 860, 42), (51, "hollowSpire", 810, 52) };
        /// <summary>The Sack Lift's fare (Floor991.json offerShortcut costDiamonds): the route scenarios top the diamonds up to it (a setup shortcut).</summary>
        const int SackLiftFare = 20;
        /// <summary>circle-x-route: Floor 991 loaded directly, then the real flow through Floor 901.</summary>
        public const int RouteFrom = 11, RouteTo = 31;
        const float RouteSeconds = 3900f;
        /// <summary>circle-x-route's first leg: Floor 991 the long way (the Sack Lift declined) up to the start of Floor 990's stage.</summary>
        const float LongWaySeconds = 420f;
        /// <summary>Floor 991: the Sack Lift's choice (or the floor's end) comes within this many game seconds of the Foreman's defeat (~9 s as built).</summary>
        const float ForemanExitSeconds = 10.5f;
        /// <summary>
        /// The Landing (Level 2, Floor 1000, one level = one floor): the floors its counter must run through (1000, ticking to 999
        /// on its landing), and the milestone beat, which plays as Floor 999 (MilestoneLevel) opens.
        /// </summary>
        public const int LandingLevel = 2, LandingFirstFloor = 1000, LandingLastFloor = 999, LandingMilestone = 999, MilestoneLevel = 3;
        /// <summary>
        /// The landing-stretch scenario: the first floors built one level per floor (Docs/LevelDesign.md, Floors 1000-996), Level 2
        /// loaded directly, then the real flow through Level 6, each with the stage checks and its own new idea checked, the Shaft
        /// climbing one floor after each; Video3 and Yooba's hello come after Floor 1000.
        /// </summary>
        public const int StretchFrom = 2, StretchTo = 6;
        const float StretchSeconds = 900f;
        /// <summary>
        /// The foreman-stretch scenario: Floors 995-991 (Docs/LevelDesign.md), Level 7 (The Loop) loaded directly, then the real flow
        /// through Level 11, the Imp Foreman's climbing arena (the first floor boss), each floor with its own idea checked.
        /// </summary>
        public const int ForemanFrom = 7, ForemanTo = 11;
        const float ForemanSeconds = 1080f;
        /// <summary>
        /// The long-way scenario: the old 10-floor stages over Floors 990-951 (Docs/Story.md, "Level numbers": the long way's
        /// stand-ins), level 68 (Level3.json, The Spill) loaded directly, then the real flow through level 70 (The Slag Falls),
        /// which leads on to Floor 960; then level 71 (Level6.json, The Loop: level select only) loaded directly and played.
        /// </summary>
        public const int LongWayFrom = 68, LongWayTo = 70, LongWayLoop = 71;
        const float LongWayStagesSeconds = 1200f;
        /// <summary>A floor boss's telegraphs (Floor 991, the Imp Foreman): each attack and the glow that must come 1+ s before it, on its spot.</summary>
        static readonly (string attack, string warning, string what)[] FloorBossTelegraphs =
        {
            ("fmLash", "fmWarnWhip", "whip crack"),
            ("fmEmber", "fmSackDrip", "sack embers"),
        };
        /// <summary>The Foreman's fight in order (signals): the intro, phase 1 on tier 1, the lava taking tier 1, phase 2 on tier 2, the defeat and the pile.</summary>
        static readonly (string signal, string beat)[] FloorBossSignals =
        {
            ("fmIntro", "intro: the Foreman on his sack"),
            ("fmFight", "phase 1: tier 1, the whip and the rain of embers, alone"),
            ("fmSurge", "phase 1b: below 70%, the first wave of minions"),
            ("fmClimb", "half health: the lava takes tier 1, he climbs"),
            ("fmPhase2", "phase 2: tier 2, swinging on the lift's chain, a second wave"),
            ("fmPhase3", "phase 3: a quarter left, the rage and the biggest wave"),
            ("fmDefeat", "defeat"),
            ("fmPile", "the diamond pile"),
        };
        /// <summary>
        /// The six bosses' rework (Docs/Story.md "The bosses' second look"): at least 3 phases, 4-8 minions of mixed types alive at the peak, a fight of
        /// 2-4 minutes (from its fight signal to its defeat signal), the hero's weakness hint spoken. Level = the level file, Phases = the signals that
        /// open a combat phase, in order. The boss + 8 minions is the most that may be on screen.
        /// </summary>
        public sealed class BossFightDef
        {
            public string Level, Name, Hint, Fight, Defeat;
            public string[] Phases;
        }

        public static readonly BossFightDef[] BossFights =
        {
            new BossFightDef { Level = "Floor991", Name = "Imp Foreman", Hint = "vo_hero_hint_impForeman", Fight = "fmFight", Defeat = "fmDefeat", Phases = new[] { "fmFight", "fmSurge", "fmPhase2", "fmPhase3" } },
            new BossFightDef { Level = "Floor951", Name = "Pile Keeper", Hint = "vo_hero_hint_pileKeeper", Fight = "pkFight", Defeat = "pkDefeat", Phases = new[] { "pkFight", "pkSurge", "pkPhase2", "pkPhase3" } },
            new BossFightDef { Level = "Floor901", Name = "Magma King", Hint = "vo_hero_hint_magmaKing", Fight = "mkFight", Defeat = "mkDefeat", Phases = new[] { "mkFight", "mkWave", "mkPhase2", "mkEnrage", "mkLast" } },
            new BossFightDef { Level = "Floor891", Name = "Road Breaker", Hint = "vo_hero_hint_roadBreaker", Fight = "rbFight", Defeat = "rbDefeat", Phases = new[] { "rbFight", "rbSurge", "rbPhase2", "rbPhase3" } },
            new BossFightDef { Level = "Floor851", Name = "Carver", Hint = "vo_hero_hint_carver", Fight = "cvFight", Defeat = "cvDefeat", Phases = new[] { "cvFight", "cvSurge", "cvPhase2", "cvPhase3" } },
            new BossFightDef { Level = "Floor801", Name = "Spire Fiend", Hint = "vo_hero_hint_spireFiend", Fight = "sfFight", Defeat = "sfDefeat", Phases = new[] { "sfFight", "sfWave", "sfPhase2", "sfEnrage", "sfLast" } },
            new BossFightDef { Level = "Floor751", Name = "Lamplighter", Hint = "vo_hero_hint_lamplighter", Fight = "llFight", Defeat = "llDefeat", Phases = new[] { "llFight", "llTier2", "llBlindSignal", "llLast" } },
            new BossFightDef { Level = "Floor701", Name = "Bridge Wraith", Hint = "vo_hero_hint_bridgeWraith", Fight = "brFight", Defeat = "brDefeat", Phases = new[] { "brFight", "brWave", "brPhase2", "brEnrage", "brLast" } },
        };

        /// <summary>The boss and up to 8 minions; the fight alone 120-240 s; the whole level (intro, fight, defeat) up to BossLevelMax.</summary>
        const int BossFightEnemies = 9;
        const float BossFightMin = 120f, BossFightMax = 240f, BossLevelMax = 330f;

        static BossFightDef BossFightOf(int n)
        {
            var definition = GameDefinition.Instance;
            var level = definition != null && n > 0 && n <= definition.TotalLevels ? definition.GetLevel(n) : null;
            return level != null ? Array.Find(BossFights, b => b.Level == level.levelName) : null;
        }

        const float StoryPathSeconds = 4200f;
        /// <summary>
        /// The wastes-climb scenario: the floor it loads directly (Floor 910, the foot of the Heat Stairs) and the last one it plays through the real flow
        /// (Floor 901, the Magma King's arena). Docs/Story.md, "Level numbers".
        /// </summary>
        public const int ClimbFrom = 22, ClimbTo = 31;
        const float WastesClimbSeconds = 1800f;
        /// <summary>The shaft scenario: the level after the plunge whose climb it watches (The Landing).</summary>
        public const int ShaftLevel = 2;
        const float ShaftSeconds = 480f;
        /// <summary>The music scenario: after the shaft scenario's run, the Warden's arena it loads directly (the Magma King).</summary>
        public const int MusicBossLevel = 31;
        const float MusicBossSeconds = 420f;
        const float YoobaSeconds = 300f;
        /// <summary>The waystone scenario: the stage the bot clears first (Floor 999, for a Grudge), then dies on the next one (Floor 998, The Spill).</summary>
        public const int WaystoneLevel = 3;
        const float WaystoneSeconds = 480f;
        /// <summary>
        /// The shortcut scenario (Docs/Story.md, "Shortcuts"): the stage with Hell's first shortcut (The Old Stair, Floors
        /// 880-871, the collapsed stairwell at its top), the floor it lets him out on, and the stage it leads to, past the
        /// ones it skips (Levels 19-21).
        /// </summary>
        public const int ShortcutLevel = 63, ShortcutToFloor = 841, ShortcutNextLevel = 67;
        /// <summary>
        /// The cracked-road-route scenario (Docs/LevelDesign.md section 6): Floor 901 (the Magma King) loaded directly, then the real
        /// flow through Circle IX's played route to Floor 802 (the Spire Fiend's door) and Floor 801 (his arena, his Waystone), taking the
        /// Spared Demon and the Hollow Spire.
        /// </summary>
        public const int CrackedFrom = 31, CrackedTo = 61;
        const float CrackedSeconds = 4900f;
        const float ShortcutSeconds = 480f;

        /// <summary>
        /// The all-levels scenario (and its floors-A-B stretches): every level on the path in game.json's order, each opened
        /// directly (a setup shortcut, so videos are skipped and a level the bot can't clear never stops the run), played with
        /// a time limit in game seconds (a floor, a floor boss's or Warden's floor), once more after a death, and reported
        /// per level: cleared, died (where, by what) or stuck (phase, place, path section, the bot's ClimbTarget).
        /// </summary>
        public const string AllLevelsId = "all-levels", StretchPrefix = "floors-";

        /// <summary>
        /// climb-start: the all-levels run over the start of the climb, Level 1 (the Meadow) and the Landing stretch (Floors 1000-991),
        /// each level with a check event (cleared, or how it wasn't, and how long it took).
        /// </summary>
        public const string ClimbStartId = "climb-start";
        const int ClimbStartTopFloor = 1000, ClimbStartLastFloor = 991;

        /// <summary>The climb-start levels: Level 1 and the path's floors from ClimbStartTopFloor down to ClimbStartLastFloor.</summary>
        public static List<int> ClimbStartLevels() =>
            PathLevels().FindAll(n => n == 1 || PathFloor(n) <= ClimbStartTopFloor && PathFloor(n) >= ClimbStartLastFloor);
        const float PathFloorSeconds = 160f, PathBossSeconds = 250f;
        /// <summary>A long bullet-hell floor (Docs/LevelDesign.md, "A long bullet-hell floor"): its script's expected length is at least
        /// LongFloorLength seconds. It runs 240-360 s, so all-levels gives it PathLongFloorSeconds and its pace row 6 minutes.</summary>
        const float LongFloorLength = 200f, PathLongFloorSeconds = 420f, LongFloorMin = 240f, LongFloorMax = 360f;
        const int LongFloorEnemies = 16, LongFloorMinPieces = 4;
        const float MidFloorLength = 100f;
        /// <summary>Floor 998's background boss (the ember rain's source) and the time a hero who just runs may take to its landing.</summary>
        const string SpillBossId = "motherCinder";
        const float SpillClearSeconds = 150f;
        /// <summary>The bar for Circle IX's long floors (their generator estimate opens at 4.5-7 and ends at 12-20, Circle X's at 1.4-5.4 and 12.7-13.8): the average density over a floor and the last piece's.</summary>
        const float CircleXAverageDensity = 5.5f, CircleXLastDensity = 8.5f;

        static bool IsLongFloor(int n)
        {
            var config = GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(n) : null;
            var content = GameContent.Current;
            return config != null && content != null && content.LevelScripts.TryGetValue(config.levelName, out var script)
                   && script != null && script.length >= LongFloorLength;
        }
        const int PathTries = 2;

        /// <summary>
        /// The runner cancels any run after 60 minutes ("stuck: ran for over 60 min"), and a full all-levels run takes hours. So
        /// a run starts no new level after PathRunBudget real seconds (from the scenario's start), lets the level in play finish
        /// by PathRunHardEnd, and lists every level it didn't get to as "not reached (time budget)" in the table.
        /// The runner's 60 minutes also cover what it does after the scenario ends: encoding the recording took 5-8 minutes for a
        /// 52-minute run (r_0d9606d1c310 ended its table at 51.7 min and was cancelled while uploading), so the scenario ends by
        /// PathRunHardEnd = 43 min, leaving the runner RunnerAfterMinutes and a few minutes to spare.
        /// </summary>
        const float PathRunBudget = 38f * 60f, PathRunHardEnd = 43f * 60f;
        const float RunnerLimitMinutes = 60f, RunnerAfterMinutes = 9f;
        const string NotReached = "not reached (time budget)";

        /// <summary>
        /// The most real seconds one level may take in all-levels, both tries and their loads together: its game-time limit at
        /// the run's speed plus 45 s, at least 4 minutes (a floor boss's about 5). Past it the level is stuck and the next starts.
        /// </summary>
        static float PathLevelCap(int n, float speed) => Mathf.Max(240f, PathLevelSeconds(n) / speed + 45f);
        static readonly Regex FloorLevelName = new Regex(@"^Floor(\d+)$");

        /// <summary>The floor a one-floor level ("FloorN" in game.json) is on, or 0.</summary>
        public static int PathFloor(int n)
        {
            var definition = GameDefinition.Instance;
            var level = definition != null && n > 0 && n <= definition.TotalLevels ? definition.GetLevel(n) : null;
            var match = level != null && level.levelName != null ? FloorLevelName.Match(level.levelName) : null;
            return match != null && match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        /// <summary>
        /// The path: game.json's levels from the start, Level 1 (the Meadow) then each floor ("FloorN", one level = one floor),
        /// up to the first level that isn't a floor (the long way's old 10-floor stages and the arcade levels after them).
        /// The shortcuts' skipped floors are not in the list, so the path takes them.
        /// </summary>
        public static List<int> PathLevels()
        {
            var list = new List<int>();
            var definition = GameDefinition.Instance;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                if (n > 1 && PathFloor(n) <= 0)
                    break;
                list.Add(n);
            }
            return list;
        }

        public const string LadderFloorsId = "ladder-floors";

        /// <summary>The levels with a ladder on their floor: a spawn in the script's path of an entity that has the ladder ability, in game.json's order.</summary>
        public static List<int> LadderFloorLevels()
        {
            var list = new List<int>();
            var definition = GameDefinition.Instance;
            var content = SickOfIt.Gameplay.GameContent.Current;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels && content != null; n++)
            {
                var config = definition.GetLevel(n);
                if (config == null || config.levelName == null || !content.LevelScripts.TryGetValue(config.levelName, out var script)
                    || script == null || script.world == null || script.world.path == null)
                    continue;
                bool ladder = false;
                foreach (var section in script.world.path)
                    foreach (var spawn in section.spawns)
                        if (spawn.entity != null && content.Entities.TryGetValue(spawn.entity, out var def) && def.abilities.Exists(a => a is SickOfIt.Gameplay.Content.LadderAbility))
                            ladder = true;
                if (ladder)
                    list.Add(n);
            }
            return list;
        }

        /// <summary>
        /// bridge-descent: Circle VIII's lower half (Docs/Story.md "The Rune Bridges' lower half"): a sample of Floors 751-702 (the Lamplighter, the halfway
        /// floor, then the floors that bring the afterimage, the hound and the sleepwalker, and the last crossing), each opened directly, then the Bridge
        /// Wraith's Floor 701 played like level-N: her fight, the Waystone and the Shaft waking Circle VII. The whole lower half runs in rune-bridges-3 and on.
        /// </summary>
        public const string BridgeDescentId = "bridge-descent";
        /// <summary>Where bridge-descent starts without an "at": the hound's floor, so the run (three floors, then the Wraith) fits Studio's 10 minutes.</summary>
        public const string BridgeDescentDefaultAt = "Floor735";

        /// <summary>Scenarios a beta check plays must finish in 10 minutes (docs/playtest.md): no new floor after 6 min of run time, the last one over by 8.</summary>
        const float ShortRunBudget = 6f * 60f, ShortRunHardEnd = 8f * 60f;

        public static readonly string[] BridgeDescentFloors = { "Floor751", "Floor750", "Floor745", "Floor735", "Floor725", "Floor705" };

        /// <summary>The level number (1-based position in game.json) of a level file's name, or 0.</summary>
        public static int LevelNumberOf(string levelName)
        {
            var definition = GameDefinition.Instance;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var config = definition.GetLevel(n);
                if (config != null && config.levelName == levelName)
                    return n;
            }
            return 0;
        }

        public static List<int> BridgeDescentLevels()
        {
            var list = new List<int>();
            foreach (string name in BridgeDescentFloors)
            {
                int n = LevelNumberOf(name);
                if (n > 0)
                    list.Add(n);
            }
            return list;
        }

        public const string RuneBridgesId = "rune-bridges";

        /// <summary>The levels of Circle VIII (the Rune Bridges): floors whose script says circle "runeBridges", in game.json's order.</summary>
        public static List<int> RuneBridgeLevels(int part = 0)
        {
            var list = new List<int>();
            var definition = GameDefinition.Instance;
            var content = SickOfIt.Gameplay.GameContent.Current;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var config = definition.GetLevel(n);
                if (config != null && content != null && config.levelName != null && content.LevelScripts.TryGetValue(config.levelName, out var script)
                    && script != null && script.floors != null && script.floors.circle == "runeBridges")
                    list.Add(n);
            }
            // Parts of five floors (about 5 minutes each, two tries): the run's time budget covers five, not nine.
            if (part > 0)
                list = list.GetRange(Mathf.Min(list.Count, (part - 1) * RuneBridgesPerPart), Mathf.Max(0, Mathf.Min(RuneBridgesPerPart, list.Count - (part - 1) * RuneBridgesPerPart)));
            return list;
        }

        public const int RuneBridgesPerPart = 5;

        public const string CrackedHeatId = "cracked-heat";
        public const int CrackedHeatPerPart = 5;

        /// <summary>
        /// Circle IX's heat pass (Docs/LevelDesign.md "A long bullet-hell floor", point 10): the floors whose script says circle "crackedRoad" and is
        /// a long floor (Floors 900-892, 860-852 and 810-802; the floor bosses are not), in game.json's order, in parts of five floors (about 5 minutes each).
        /// </summary>
        public static List<int> CrackedHeatLevels(int part = 0)
        {
            var list = new List<int>();
            var definition = GameDefinition.Instance;
            var content = SickOfIt.Gameplay.GameContent.Current;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var config = definition.GetLevel(n);
                if (config != null && content != null && config.levelName != null && content.LevelScripts.TryGetValue(config.levelName, out var script)
                    && script != null && script.floors != null && script.floors.circle == "crackedRoad" && script.length >= LongFloorLength)
                    list.Add(n);
            }
            if (part > 0)
                list = list.GetRange(Mathf.Min(list.Count, (part - 1) * CrackedHeatPerPart), Mathf.Max(0, Mathf.Min(CrackedHeatPerPart, list.Count - (part - 1) * CrackedHeatPerPart)));
            return list;
        }

        /// <summary>Every level in game.json's order, 1 to the last: the path, then the long way's old stages and the arcade levels.</summary>
        public static List<int> AllGameLevels()
        {
            var list = new List<int>();
            int levels = GameDefinition.Instance != null ? GameDefinition.Instance.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
                if (!IsSideFloor(n))
                    list.Add(n);
            return list;
        }

        /// <summary>
        /// off-path: the all-levels run over the levels after the path (the long way's old 10-floor stages, Circle IX's and
        /// Circle X's, and the arcade levels), each with a check event.
        /// </summary>
        public const string OffPathId = "off-path";

        /// <summary>The levels in game.json after the path's last floor.</summary>
        public static List<int> OffPathLevels()
        {
            var path = PathLevels();
            int last = path.Count > 0 ? path[path.Count - 1] : 0;
            return AllGameLevels().FindAll(n => n > last);
        }

        /// <summary>
        /// The path's floors in stretches, each ending on a floor boss or Warden (a floor ending in 1) or where a shortcut
        /// jumps: 1000-991, 960-951, 910-901, 900-891, ... The floors-A-B scenarios play one each.
        /// </summary>
        public static List<(int from, int to)> PathStretches()
        {
            var list = new List<(int, int)>();
            int start = -1, lastFloor = 0, lastLevel = 0;
            foreach (int n in PathLevels())
            {
                int floor = PathFloor(n);
                if (floor <= 0)
                    continue;
                if (start > 0 && (floor != lastFloor - 1 || lastFloor % 10 == 1))
                {
                    list.Add((start, lastLevel));
                    start = -1;
                }
                if (start < 0)
                    start = n;
                lastFloor = floor;
                lastLevel = n;
            }
            if (start > 0)
                list.Add((start, lastLevel));
            return list;
        }

        static string StretchId((int from, int to) stretch) => StretchPrefix + PathFloor(stretch.from) + "-" + PathFloor(stretch.to);

        /// <summary>
        /// The time limit all-levels gives level n, in game seconds: the level's own (duration + 10), at most PathFloorSeconds (a
        /// boss's floor PathBossSeconds). A level off the path (an old 10-floor stage, an arcade level) gets its duration + 30, at
        /// most PathBossSeconds: Everything, Everywhere runs 180 s and Fire Rain ends on a boss.
        /// </summary>
        static float PathLevelSeconds(int n)
        {
            var level = GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(n) : null;
            float own = Mathf.Max(30f, level != null ? level.duration : 60f) + 10f;
            int floor = PathFloor(n);
            if (n > 1 && floor <= 0)
                return Mathf.Min(own + 20f, PathBossSeconds);
            if (floor > 0 && IsLongFloor(n))
                return Mathf.Min(own, PathLongFloorSeconds);
            return Mathf.Min(own, floor > 0 && floor % 10 == 1 ? PathBossSeconds : PathFloorSeconds);
        }

        /// <summary>
        /// Real seconds a run of these levels may take: each level's limit, a second try for some, and the loads; never more than
        /// the time budget's hard end (the run stops starting levels at PathRunBudget and ends with its table).
        /// </summary>
        static float PathRunSeconds(List<int> levels)
        {
            float seconds = 60f;
            foreach (int n in levels)
                seconds += PathLevelSeconds(n) * 1.4f + 30f;
            return Mathf.Min(seconds, PathRunHardEnd);
        }

        static string PathRange(List<int> levels) =>
            levels.Count == 0 ? "none" : $"levels {levels[0]}-{levels[levels.Count - 1]}" +
                                         (PathFloor(levels[levels.Count - 1]) > 0 ? $" (to Floor {PathFloor(levels[levels.Count - 1])})" : "");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Register() => GameDefinition.WhenReady(RegisterNow);

        // The scenario list comes from the levels, so it waits for a player build's GameDefinition bundle.
        static void RegisterNow()
        {
            PlaytestScenario[] scenarios;
            try { scenarios = Scenarios(); }
            catch (Exception e)
            {
                // A throw while the list is built (it reads every level) must not leave the page with no contract at all: the runner would skip every scenario.
                Debug.LogException(e);
                scenarios = new[]
                {
                    new PlaytestScenario("smoke", "Boot, main menu, New Game, level 1 for 30 s with a rewind and a pause", 55, "smoke"),
                    new PlaytestScenario(FloorsId, "The first four floors (Floors 1000-997): teach, finale, payoff, deaths per floor (fallback entry)", FloorsSeconds, "level"),
                };
            }
            HawkesPlaytest.Register(Product, scenarios, Bot, IsReady, StopActive);
            WatchStartPoint();
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
                string what = $"Level {n} ({name}), started directly (setup shortcut) with the save a player arriving there has (the start-at save)";
                if (n == PortalLevel)
                {
                    what += ", then its ending, the portal scene, checked beat by beat: after TOAST (no Grudge; that waits until Floor 1000) the Overlord's beam is telegraphed by a charge glow and " +
                            "comes down as a burning column with an impact burst on the background's fire hole (where the footage's laser hit, wherever the hero stands), the ground tears open into a swirling fire portal " +
                            "(the one from Video2) right on top of the background's fire (screen captures before and after), that loops while he's there and flares shut to a scorch at the end, " +
                            "he walks to it by himself from his side, the lines and the echo play with subtitles, " +
                            "he turns to leave and is pulled in spinning and shrinking, a slime is slurped in after him, and Video2 starts (Continue on " +
                            "the results); move, jump, fire and R are pressed during it and must do nothing, and he takes no damage";
                    seconds += 30f;
                }
                else if (IsArena(n) && WardenOf(n) == SpireFiend)
                {
                    what += ": a Warden's arena (a locked screen), checked as it plays: " + SpireFiendArena + "; then Continue on the results, the Shaft's " +
                            "climb to 801 with that Waystone saved and lit and Circle VIII's band waking up (voiced), and the level select";
                    seconds += 120f;
                }
                else if (IsArena(n) && WardenOf(n) == BridgeWraith)
                {
                    what += ": a Warden's arena (a locked screen), checked as it plays: the title banner (\"FLOOR 701: THE GREAT SPAN\") with its voice, the voiced intro " +
                            "(\"FLOOR 701! THE WARDEN OF THE RUNE BRIDGES!\", \"THE BRIDGE WRAITH\", her whisper, the hero's reply, the boss bar), her phases in order from the signals " +
                            "(brIntro, brFight, brSnuff at half health, brPhase2 with the lanternflies, the only adds, brEnrage at a third, brUnwrite, brDefeat, waystoneLit), 4+ minions at the peak, " +
                            "every attack after its glow 1+ s ahead, the hero's hint line, her defeat (\"THANK YOU\" on the runes, \"THE BRIDGE WRAITH HAS FALLEN!\"), the Waystone lit at 701; then Continue, " +
                            "the Shaft's climb to 701 with that Waystone saved and lit and Circle VII, the Gloomwood, waking up (voiced)";
                    seconds += 120f;
                }
                else if (IsArena(n))
                {
                    what += ": a Warden's arena (a locked screen), checked as it plays: the title banner (\"FLOORS N-M: PLACE\") with its voice, then the " +
                            "intro (input and rewind locked: the camera pans up to the throne as it rises out of the slag, the announcer's \"FLOOR 901! THE WARDEN " +
                            "OF THE WASTES!\" and \"THE MAGMA KING\", his voiced taunt, the hero's one-liner, the boss bar), the fight's three phases in order " +
                            "(from the throne; at half health he stands and calls his imp guard, the only phase with adds; at a third he enrages and floods the hall), " +
                            "every attack (boulder, shockwave, fire breath, lava flood) after its warning glow 1+ s ahead, no more than 3 enemies at once, every line " +
                            "with its voice; his defeat: slow motion, his Die clip (collapsing into slag), his last line, \"THE MAGMA KING HAS FALLEN!\", the floor " +
                            "counter on 901 and the Waystone lighting there (\"WAYSTONE LIT: FLOOR 901\"), the Grudge pick; then Continue on the " +
                            "results, the Shaft's climb to 901 with that Waystone saved and lit and Circle IX's band waking up (voiced), and the level select";
                    seconds += 60f;
                }
                else if (name != null && name.StartsWith("Floor ", StringComparison.Ordinal))
                {
                    what += ": one floor of Hell (one level = one floor), checked as it plays: its title banner (\"FLOOR N: PLACE\") and hero lines " +
                            "with their voices, the floor counter on its floor and ticking to the next on the landing, the floor's own new idea (1000 sinking " +
                            "stones, 999 the rising lava and its wave, 998 the village's wreckage with cinders, 997 imps swinging with diamond sacks and an " +
                            "empty chain ridden, 996 lava falls and geysers), at most one new enemy type and the first one alone, a 3+ s breather where it " +
                            "has enemies, no more than 3 enemies at once, 60-120 s, every scripted line with its voice, the Grudge pick (a long bullet-hell floor, " +
                            "Floor 998: 240-360 s, up to 16 enemies and shots at once, 4+ set pieces announced by `setpiece:` signals and a rising density); then Continue on " +
                            "the results, the Shaft climbing one floor, and what comes next";
                    seconds += 40f;
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
            list.Add(new PlaytestScenario("restart", $"Restart from the pause menu: level {LandingLevel} (Floor 1000, loaded directly, a setup shortcut) " +
                "recorded 1 s in, the bot plays a few seconds, P, Restart Level; the restarted level 1 s in must match the fresh one: one Level scene " +
                "and a new world, the clock, the hero's place, his hearts, the floor counter and distance, and the first moving pieces (ids, states, " +
                "places). Each beat is a check event", 90, "level"));
            list.Add(new PlaytestScenario(DailyId, "The daily budget run: first rewind-perf on the Magma King's arena (three rewinds; rewinding no worse than 1.5x play, no frame over 100 ms, " +
                $"playing averages under {FramePlayAverageMs:0} ms and rewinding under {FrameRewindAverageMs:0} ms a frame), then a sample of the climb (Level 1 and Floors 1000-998) played by the bot " +
                "with the per-floor balance report: `balance` events and balance.level<n>.* metrics for clear time, deaths, diamonds, rewind use, grazes, hits taken and frame time, the same table " +
                $"in the report's log, and a `frame-time` check per floor (average under {FramePlayAverageMs:0} ms, under {FrameSpikeShare * 100:0}% of frames over {FrameSpikeMs:0} ms). " +
                "Every all-levels run (and its parts) ends with the same balance report", DailySeconds, "level"));
            list.Add(new PlaytestScenario(RewindPerfId, $"Rewind's frame rate on a busy floor: \"{RewindPerfLevel}\" (Circle X's top, the Magma King's arena, " +
                $"loaded directly, a setup shortcut; a death loads it again) played by the bot for {RewindPerfPlay:0} s, then R held for {RewindPerfHold:0} s " +
                $"(the juice topped up first, a setup shortcut; the bot's own rewinds off), {RewindPerfRewinds} times. The average and worst frame time while " +
                "playing and while rewinding, the timeline's own work, how many rewindables and how long their history, and the game time the clock takes on " +
                "the frames right after a rewind (clamped, so lag can't kill the hero). Checks: rewinding averages no worse than 1.5x normal play (or +5 ms), " +
                "no rewind frame over 100 ms, the post-rewind step clamped. rewind-perf and check events, rewindPerf.* metrics", 240, "level"));
            list.Add(new PlaytestScenario(RewindPerfSlowId, $"Rewind on a slow device: rewind-perf ({RewindPerfLevel}) with every frame held to {SlowCpuFactor:0}x its own work " +
                "(a spin standing in for the browser's CPU throttle). Rewinding must average no more than 1.5x a playing frame, the timeline must allocate nothing while rewinding " +
                "(RewindPerf's allocation counters), and after each rewind the hero and the entities still alive stand where they stood at that game time. The record interval in use is " +
                "reported (10 Hz on a slow device). rewind-perf and check events, rewindPerf.* and frameTime.throttled.* metrics", 480, "level"));
            list.Add(new PlaytestScenario(AimGuideScenario, $"The aim guide (player.json aimGuide): level {AimGuideLevel} (the Meadow, loaded directly, a setup " +
                "shortcut) played by the bot through real input; every volley logs the guide's line directions on the frame it fired and the shots' flight " +
                "directions read back from the projectiles, and they must match (aim events for the first volleys of each part and any mismatch, a check per part). Then the gamepad stick held: one " +
                "line along it, short and thin, drawn under every bullet, hazard and enemy; held through a rewind (hold R) and the pause: hidden for both; " +
                $"the mouse: hovering shows where a click goes, clicking fires along it. Then with {AimGuideSpreadLevels} levels of the spread Grudge " +
                $"\"{AimGuideSpread}\" (setup shortcut) the level again: one fainter line per shot, each matching its shot. Then the bot plays to lose with " +
                $"the stick held once he is dead (hidden), and the scene \"{CutsceneScene}\" with the stick held while it has the hero (hidden)", 420, "level"));
            list.Add(new PlaytestScenario(WeaponsId, $"The weapons (content/weapons, Docs/GameplayCookbook.md \"Weapons\"): the scene \"{WeaponsScene}\" on level {WeaponsLevel}'s stage (setup shortcut), " +
                "played through real input. The rocket launcher is the default: the HUD names it, it fires at player.json's rate along the aim, and its aim guide shows. A level action (giveWeapon) " +
                "puts fists in his hands; Q switches to them (keyboard), the HUD shows FISTS and the aim guide shortens to their reach; he punches an imp that appears in front of him: " +
                "the swing is drawn (a crescent and a fist), the combo hits and the imp dies. Q back to the launcher, then hold R back to before that switch: the rewind brings the fists back. " +
                "The HUD's weapon button is clicked with the mouse (a touch tap takes the same path) and switches; a second level action (takeWeapon) takes the fists away and the HUD " +
                "is back to the launcher. Each beat is a check event; weapon events log every equip, swing and hit", WeaponsSeconds, "level"));
            list.Add(new PlaytestScenario(WeaponDropsId, "The weapon drops (Docs/Story.md \"Weapons\"), real input, each floor opened directly with the save a hero arriving there has (setup shortcuts; nothing unlocked at the start): " +
                "Floor 998, where a thief imp comes out of the hay and takes the rocket launcher (weaponTaken, the banner and his line, fists equipped, the fists landing hits); " +
                "Floors 997-995 fought with fists only (brawls of cinders and brutes); Floor 994's end, where the thief throws the launcher back (a pickup: banner and line); then each weapon found on its floor with a first-pickup banner " +
                "and the hero's line: the Spitfire (993), the Ricochet Pistol (992), the Chain-Whip (the Imp Foreman, 991), the Shotgun (954) and the Boomerang Halo (the Pile Keeper, 951). " +
                "Then a real death on Floor 999 (the bot plays to lose): the run ends, and the next run still holds the launcher and all five unlocked weapons (a weapon given for the run only, fists, is gone). " +
                $"Last, the bench (the scene \"{WeaponArenaScene}\" on level {WeaponsLevel}'s stage): the Spitfire streams at several times the launcher's rate on ammo, the Ricochet Pistol's bullet bounces off the ground, " +
                "a dummy 2.6 away is out of the fists' reach and in the Chain-Whip's, the Shotgun fires five pellets and hurts point blank but not across the room, and the Boomerang Halo flies out, comes back and hits both ways. " +
                "Each beat is a check event; weapon, banner and line events log the timeline", WeaponDropsSeconds, "level"));
            list.Add(new PlaytestScenario(CodexId, $"The Codex (Docs/Story.md \"The Codex\"): New Game forgets everything; level {CodexLevel} is played by the bot until it has met imps; " +
                "the pause menu's Codex button (real clicks) opens the screen: the Enemies tab lists the imp with the hero's note, its weakness and times met/killed, every unmet entry is a black silhouette " +
                "called ??? with no name, note or weakness (no spoilers); the four tabs are clicked in turn, every page of each is paged through and laid out inside the panel and the screen " +
                "(MenuLayoutAudit, no overlaps; run it on a 1920x1080 and a 1080x1920 device); closed and resumed, the main menu's Codex button shows the same tallies (they persist). " +
                "Each beat is a check event; codex events log what each page shows", CodexSeconds, "level"));
            list.Add(new PlaytestScenario(AchievementsId, "Achievements (Docs/Story.md \"Achievements\"): the list in ui/codex.json has 40+ sound entries; New Game empties it; level 1 is played by the bot until its first unlock, " +
                "whose toast must be on screen with the achievement's name; the pause menu's Codex shows the Achievements tab (real clicks): the unlocked one first with its title, the locked ones with their progress, " +
                "every page inside the panel (MenuLayoutAudit; run it on a 1920x1080 and a 1080x1920 device); a click on the unlocked row wears its title; the save persists across a reload and the main menu, and New Game resets it. " +
                "Each beat is a check event", AchievementsSeconds, "level"));
            list.Add(new PlaytestScenario(UiPolishId, "HUD and menu polish (Docs/Story.md \"The HUD and menus\"): the main menu's Codex popup opens and closes with a fade (real clicks); Level 1's HUD gets a diamond pickup " +
                "(a +7 floats up from the counter and the counter ticks up to its total), the pause menu opens and closes animated and its layout is audited; Level 31's boss bar is introduced (the name lands, the bar " +
                "fills from empty, inside the screen and safe area, no overlaps); Floor 1000 sweeps the screen when its floor counter ticks over; Level 1 is played to its results, whose numbers count up, and " +
                "Continue closes the screen on a black transition that the next scene opens. Run it on a 1920x1080 and a 1080x1920 device. Each beat is a check event; ui-polish and boss-intro events log the timeline", UiPolishSeconds, "level"));
            list.Add(new PlaytestScenario(HudInkId, "Inked HUD widgets (Docs/Story.md \"The HUD and menus\"): Floor 1000 loaded directly with touch UI on; hearts are ember vessels, rewind juice a spinning reel, diamonds a pulsing gem, floor a brass dial, grouped in two safe-area corners; a hit cracks a vessel ≥0.3 s, holding R spins the reel, a diamond pickup pulses the gem within 0.2 s; at 390x844 the HUD covers ≤12% of the playfield and the rewind button is ≥72 px. check events hud-ink <beat>; hudInk.* metrics. Run on device \"phone\"", HudInkSeconds, "level"));
            list.Add(new PlaytestScenario(BossFightsId, "The six bosses' rework (Docs/Story.md \"The bosses' second look\"): the Imp Foreman (Floor 991), the Pile Keeper (951), the Magma King (901), the Road Breaker (891), " +
                "the Carver (851) and the Spire Fiend (801), each loaded directly (setup shortcut) and fought by the bot through real input (one more try after a death). Each gets the stage and arena checks and " +
                "`bossFight.*` checks: 3+ phases opened (the fight's phase signals, in order), 4-8 minions of mixed types alive at the peak (and each phase's peak, `boss-phase` events), the fight 120-240 s from its " +
                "fight to its defeat signal, and the hero's voiced weakness hint line; the boss's telegraphs 1+ s after their glow. `boss-fight` events log the timeline, metrics boss.<level>.phases / peakMinions / fightSeconds. ~50 min", BossFightsSeconds, "level"));
            list.Add(new PlaytestScenario(ReadabilityId, "Art direction and bullet readability (Docs/Story.md \"Readability is fairness\"): Level 1 and Floors 1000-997 loaded directly (setup shortcut) and played by the bot through real input for 40 s each. " +
                "Every hostile shot wears the baseline rims and, measured against the pixels round it in a screen capture, reads at 1.5:1 or better (`bullet` events with the ratio); each level has at least 2 parallax layers (`layers` events). Each beat is a check event", ReadabilitySeconds, "level"));
            list.Add(new PlaytestScenario(BulletLookId, "Enemy bullet look and telegraphs (Docs/Story.md \"Readability is fairness\"): Level 1 and Floors 1000-997 loaded directly (setup shortcut) and played by the bot through real input for 40 s each. " +
                "Every hostile shot shows a white-hot core, dark outline, cyan/magenta halo and a short trail (`bullet` events); aimed shots telegraph ≥ 0.3 s and hazards ≥ 0.5 s (`telegraph` events, debug overlay); " +
                "on a 390x844 phone enemy bullets are ≥ 8 px across. Screenshots per floor. Each beat is a check event", BulletLookSeconds, "level"));
            list.Add(new PlaytestScenario(LightingId, "Lighting and post (Docs/lighting.md; Docs/Story.md \"Every floor has its own light\"): Level 1 and Floors 1000-997 loaded directly (setup shortcut) and played through real input with touch UI forced. " +
                "Each floor shows its own dominant grade colour and bloom, lava/torch lights within 2 units with flicker, a hero rim, and on phone (390x844) the phone tier (half-res bloom, no grain, ≤8 lights) at 30+ fps. " +
                "Rewind scanlines stay on the frame, off the playfield. `lighting` events and check events lighting <beat>; metrics lighting.levelN.fps / lights", LightingSeconds, "level"));
            list.Add(new PlaytestScenario(AmbienceId, "A Circle's air (Docs/GameplayCookbook.md \"Weather and life\"): each Circle with an ambience (the Ember Wastes, the Cracked Road, the Rune Bridges) gets its first " +
                "travelling floor loaded directly (setup shortcut) and played by the bot through real input. The parallax layers move at their own factors (every layer's travel measured against the camera's, " +
                "`parallax` events); each Circle has its own weather (ash, sparks, cinders, runes, grit: kinds and bits drawn) and its own background life (flyers, walkers, a far-off chase: appearances), " +
                "and no two Circles share a set; the frame time is measured with the ambience on, off for 10 s (the baseline) and on again, and must stay within budget (on <= off x1.2 + 2 ms, no frame over 120 ms; " +
                "ambience.<circle>.frameOnMs / frameOffMs metrics). Each beat is a check event", AmbienceSeconds, "level"));
            list.Add(new PlaytestScenario(MusicLayersId, $"The intensity music layer (Docs/Story.md \"Music\"): Floor 997 (level {MusicLayersLevel}) loaded directly (setup shortcut) and played by the bot through real input for 150 s of game time. " +
                "The Circle's layer (a stand-in loop until the real stems are made, nothing is generated) must be silent on the empty floor, fade in as enemies and shots crowd the screen and fade out again as it empties. " +
                "`musicLayer` events log its level (0-100%) and the crowd; each beat is a check event", MusicLayersSeconds, "level"));
            list.Add(new PlaytestScenario(ScoreFloorsId, "The adaptive score (Docs/Story.md \"Music\"): the Meadow and Floors 1000-997 each loaded directly and played by the bot; at least 2 score layer changes per floor, fight layer within 2 s of the first wave, the floor's own finale cue, a held R tape-stopping the score within 0.3 s and the score back after (`musicState`, `musicTapeStop` events). Floors without wired clips are skipped. Run it at 390x844 touch too.", 900f, "level"));
            list.Add(new PlaytestScenario(AdaptiveMusicId, "Adaptive music (Docs/Story.md \"Music\"): touch only, a virtual touchscreen taps the sound prompt and New Game; Level 1 plays its own composed Meadow track (not the background video's audio) within 1 s of opening, calm and then the raid's track when the sky opens. " +
                $"Then Floor 997 (level {AdaptiveMusicFloorLevel}, loaded directly: a setup shortcut) played by the bot: the Circle's loop, the intensity layer rising with the crowd and falling when it clears, and a held R playing the bent (slowed, low-passed) variant with the normal track back within 0.5 s of letting go. " +
                "Then Floor 901: the Magma King's theme takes over as the arena locks. `music`, `musicLayer` and `musicRewind` events; each beat is a check event", AdaptiveMusicSeconds, "level"));
            list.Add(new PlaytestScenario(UnarmedHeroId, $"The unarmed hero (art/hero.json unarmed*/punch1-3; player.json UnarmedIdle/Walk/Jump/Fall, Punch1-3): on the weapons bench (level {WeaponsLevel}, setup shortcut) the bot takes up the fists with Q through real input. With fists equipped Idle, Walk, Jump and Fall must show the unarmed states, never the launcher's; then it punches an imp until all three combo steps have swung, each hit landing under its own Punch clip (`unarmed` events with the clip and frame at the swing and the hit). Sheets are stand-ins until the real clips are generated. Each beat is a check event", UnarmedHeroSeconds, "level"));
            list.Add(new PlaytestScenario(WeaponLooksId, $"Weapon swaps and looks (art/hero.json launcherAway/equipLauncher/equipFists; weapon JSON `look`): on the bench \"{WeaponLooksScene}\" (level {WeaponsLevel}, setup shortcut) the bot cycles the launcher, fists, Spitfire, Ricochet Pistol, Chain Whip, Shotgun, Boomerang Halo and Slag Ladle with Q and fires each through real input: every weapon must show its own prop in his hands while held and a flash when fired (`weapon-look` events with the look; eight different looks). Taking up the fists must play EquipFists, the launcher taken from his hands (Floor 998's theft) LauncherTaken, and the launcher coming back EquipLauncher, each on screen with the prop hidden meanwhile (`weapon-swap` events). Clips are stand-in sheets until the real ones are generated. Each beat is a check event", WeaponLooksSeconds, "level"));
            list.Add(new PlaytestScenario(HeroClipsId, $"The hero's own clips (art/hero.json; player.json Hurt, DeathBurn/Squash/Spin/Shrug, Victory): the bot plays level {HeroClipsDeathLevel} to lose four times through real input. Every first hit must show the Hurt clip (`hero-clip` events), every death its own clip, the four in turn, and the death scene the same one; then the bot clears level {HeroClipsClearLevel} and the clear must show Victory. Sheets are stand-ins until the real clips are generated. Each beat is a check event", HeroClipsSeconds, "level"));
            list.Add(new PlaytestScenario(HeroAnimId, "Every hero state on its trigger (player.json animationConfig), through real input: the moves bench (Idle, Walk, Jump, Fall, Fire, RunFire, Dash, WallSlide, WallJump), the weapons bench (the fists' UnarmedIdle/Walk/Jump/Fall, Punch1-3, EquipFists, EquipLauncher), the weapon-looks bench (LauncherTaken, the launcher back), then four deaths and a floor clear (Hurt, DeathBurn/Squash/Spin/Shrug, Victory). Each state is checked on the frame the controller says it plays: the sprite on screen must be a frame of it (PlayerController.ShowsState; a stand-in sheet is named in the event). Every sprite is measured on screen and each clip is compared with Idle's size: any that differs is an `animSize` DIFFERS event, a `hero-anim size` check and a Warning. Each beat is a check event", HeroAnimSeconds, "level"));
            list.Add(new PlaytestScenario(StyleId, $"Style (Docs/Story.md \"Style\"): level {StyleLevel} loaded directly and played by the bot through real input; the graze distance is widened (setup shortcut). " +
                "A graze sparks (the meter pops and fills), a rewind right after one takes the credit back (graze count and meter fall; `graze-undone` event), the announcer and crowd lines fire on a streak (`style-line`), " +
                "tiers pay (`style-payout`); then the results screen is opened with the hits zeroed (setup shortcut): a rank S/A/B/C (`rank` event), saved as the floor's best, and the no-hit line said. Each beat is a check event", StyleSeconds, "level"));
            list.Add(new PlaytestScenario(DifficultyId, "Difficulty and accessibility (Docs/Story.md \"How sick of it are you?\"): New Game starts at Normal with no card; Settings > Pick Your Hell offers Assist, Normal, Hard and Hell Loop (real clicks; Back returns); " +
                "the settings' Accessibility page toggles bullet outlines, high-contrast bullets, reduced flash, reduced shake, the hitbox dot and aim assist and each is saved in PlayerPrefs; the bot clears Level 1 on Assist and on Normal " +
                "(enemy shot speed measured on both: Assist's are slower, `shot-speed` events); then plays reckless on Hell Loop with every option on: the first hit lands for twice Normal's damage, shots have a rim and one colour, " +
                "the dot shows, shake and flash are dimmed, aim assist bends a near-miss onto an enemy. Each beat is a check event", DifficultySeconds, "level"));
            list.Add(new PlaytestScenario(EnemyHintsId, $"The hero sizes up each enemy type once (Docs/GameplayCookbook.md \"The hero sizes them up\"): every named enemy has a hint; New Game forgets; " +
                $"Level {EnemyHintsLevel}'s first imp gets his subtitle (name and weakness), then a rewind past it and the next imps do not repeat it; a new run (the level loaded again) does not; " +
                "a scripted scene (the bystander bench) spawns an imp in silence and does not use the type up. Each beat is a check event; hint events log the subtitles", EnemyHintsSeconds, "level"));
            list.Add(new PlaytestScenario(LadderId, $"Ladders (Docs/GameplayCookbook.md \"Ladders\"): the scene \"{LadderScene}\" on level {LadderLevel}'s stage (setup shortcut), " +
                "played through real input, no bot. A ladder stands on a ledge and rises to a shelf. The hero walks to its foot (D) and fires on the ground, then W grabs on; " +
                "held still he does not fall (no gravity), W climbs up and S down (he centres on the rungs, the Climb and ClimbIdle poses play), and he shoots while climbing, " +
                "slower than on the ground. Hold R back to a moment halfway up: his place and his hold are restored and he keeps hanging. Space lets go (a jump) and he lands; " +
                "he takes hold again (S from the ladder's top, or W from its foot) and climbs off the top onto the shelf. Then hold R back to standing on the shelf, and back again " +
                "into the climb itself: the exact place and the hold come back. Each beat is a check event; ladder events log the grabs, jumps and exits", LadderSeconds, "level"));
            list.Add(new PlaytestScenario(FloorKitId, $"The grapple hook and the air-and-path floor kit (Docs/GameplayCookbook.md \"The Hook and the air kit\"): the scene \"{FloorKitScene}\" on level {FloorKitLevel}'s stage (setup shortcut: flags cleared), " +
                "crossed from the start ledge to the landing through real input, no bot, no teleporting. The Hook is picked up (locked until then: E does nothing); E throws it up and ahead: it attaches to a ring over a 6-wide gap (`grapple` events, the rope, the swing's dip and top speed), " +
                "hold R back mid-swing (his place and the rope come back), Space lets go at the swing's rise and he lands on the far ledge. A pull anchor reels him in and hops him onto a shelf. A wind zone blows in gusts: a hostile bolt flown into it and his own shot are bent along the wind, " +
                "his sideways speed peaks above walking speed, and a jump timed to a rising gust crosses a 3.4 gap a plain jump (~2) cannot. A moving platform on a path carries him (no key held), hold R back mid-ride (platform and rider return). " +
                "A collapsing floor of five stones cracks one after another (`floorkit` crack events spaced 0.8+ s) in a wave he outruns; hold R back to before it (every stone whole again); it rebuilds by itself later. A gravity-flip zone turns him over: " +
                "he falls up to a ceiling and walks it upside down; hold R back on the ceiling (gravity, place and ceiling restored) and back to before the zone (gravity back down, the floor); then he crosses to the landing. Each beat is a check event; `grapple` and `floorkit` events log every hook, crack, rebuild and flip", FloorKitSeconds, "level"));
            string movesHow = $"the scene \"{MovesScene}\" on level {MovesLevel}'s stage (setup shortcut: a new game's save, so both moves start locked), played through real input, no bot. ";
            string dashHow = "DASH: Yooba's Slimed Lining is hidden until Floor 998 is cleared; Shift does nothing while it is locked; with Floor 998 cleared and the diamonds given (setup shortcuts) it is bought through the shop's own purchase. " +
                "A bolt that is not dashed hurts him; a bolt dashed into (Shift, the dash starts, `DashInvulnerable`, the Dash pose) passes through him: health unchanged and the bolt flies on behind him. " +
                "A second press in the cooldown does nothing, the dash is back after it. Hold R back into the middle of a burst (dashing, untouchable, place and cooldown restored) and to before the dash (the dash is given back and goes again); the unlock stays. ";
            string wallHow = "WALL-JUMP: a shaft of two walls rises around him. Locked, pushing into the wall in the air does nothing. Floor 997 cleared alone does not give it; clearing Floor 996 (setup shortcut) does, and sets the `move:wallJump` flag. " +
                "Then jump at one wall, cling (a slow slide, the WallSlide pose), Space kicks off it (WallJump pose), cross, kick again: he climbs 4.5+ units with 3+ kicks, more than a jump reaches. Hold R back to a moment clinging: the wall side and his place return and the kick works again. ";
            list.Add(new PlaytestScenario(HeroMovesId, "The hero's moves (Docs/GameplayCookbook.md \"The hero's moves\"): " + movesHow + dashHow + wallHow + "Each beat is a check event; `move` events log every dash, cling and kick", MovesSeconds, "level"));
            list.Add(new PlaytestScenario(DashId, "The hero's dash (Docs/GameplayCookbook.md \"The hero's moves\"): " + movesHow + dashHow + "Each beat is a check event; `move` events log every dash", MovesSeconds, "level"));
            list.Add(new PlaytestScenario(WallJumpId, "The hero's wall-jump (Docs/GameplayCookbook.md \"The hero's moves\"): " + movesHow + wallHow + "Each beat is a check event; `move` events log every cling and kick", MovesSeconds, "level"));
            var bystanderFloors = BystanderFloorLevels();
            if (bystanderFloors.Count > 0)
                list.Add(new PlaytestScenario(BystanderFloorsId, $"Bystanders on the floors ({bystanderFloors.Count} floors, {PathRange(bystanderFloors)}; Docs/Story.md \"Bystanders on the floors\"): each opened directly (a setup shortcut) and played by the bot through real input. " +
                    "Damned souls ahead of him (a hawker, a heckler, a hand-over soul, a hiding soul, a cheering soul) turn, speak a voiced subtitle, flinch from his shots and step back; a hawker gives a free sample, a giver hands him a loop shard, a heckler is passed by a stray shot. " +
                    "A `bystander-floor` event per reaction (signal by:<kind>:<what>), and the closing check counts the floors that reacted, the kinds and the three scripted hero interactions", PathRunSeconds(bystanderFloors), "full", "levels"));
            list.Add(new PlaytestScenario(OverlordCameosId, "The Overlord on the far ledge (Docs/Story.md \"The Overlord on the far ledge\"): eight floors of Circles X and IX (997, 994, 958, 953, 906, 896, 857, 806), each opened directly (a setup shortcut) and played by the bot through real input until its scene ends. On a far ledge the Overlord taunts, drops an ember, rides a lift out of sight, lasers the floor, takes a diamond, walks his ledge, sends two whelps, lets a diamond fall; each with his voiced line and the hero's voiced reply, and `overlord` events for every beat (signal ovl:<floor>:<beat>). On Floor 896 a real hold of R while he walks drags him back and he says \"Again\" (the clock must drop). One check event per floor, then the rewind and the eight distinct floors", OverlordCameosSeconds, "level"));
            list.Add(new PlaytestScenario(CastId, "The recurring cast (Rook, Wick, Pointer, the Overlord's far glimpses, Yooba, the bystander souls): checked as data (each voice has its own Hawkes Gen voice id, every cast line has a clip, each character has an animator with an idle and a reaction), then Rook on Floor 996, Wick on 999 and Pointer on 900 played by the bot through real input until their scenes end (voiced lines, hero answers). `check` events cast <beat>", CastSeconds, "level"));
            list.Add(new PlaytestScenario(StopFloorsId, "Stop floors (Docs/Story.md \"Stop floors\"): Circle X's 957 The Hot Bath, 959 Hell's Graffiti and 952 The Waystone Shrine, Circle IX's 897 Rook's Camp, 893 The Stuck Imp, 856 The Sleeping Demon, 853 The Damned Parade and 809 The Loop Room, each opened directly (a setup shortcut) and played by the bot through real input to its landing. No lava rises, no hazard starts and nothing can hurt him for 20+ s (`calm` 20 s after `enter`); each has a discovery (the bath's soul and heal, six carvings and a diamond, the shrine's rest-or-fill card, Rook's question answered by where he stands, an imp that throws the same failed ember, a demon that stays asleep while he tiptoes past for diamonds, a fair that repeats three times until a soul turns to watch him, a room where a soul replays his jump after a rewind). The stuck imp is rewound with R after its third throw and must throw the same throw again at the same game time; in the loop room R is held after his jump and the soul must jump it back. `stop-floor` events (signal stop:<key>:<beat>), a check per floor: played, discovery, quiet, 20 s, no rising lava, and its own beat", StopFloorsSeconds, "level"));
            list.Add(new PlaytestScenario(FloorsId, "The first four floors (Docs/LevelDesign.md \"The first four floors: teach, then finale\"): Floors 1000-997, each opened directly (a setup shortcut) and played to its landing by the bot through real input, rewind allowed. Per floor: the threat's teaching glow (a teachGlow on screen, fully inside the playfield and clear of the touch stick, a screenshot) and cue sound before it acts, the finale (music change, screen-wide tint, a screenshot), the payoff stinger as the Shaft opens, deaths, the longest stand-still (stuck spot, 5 s) and whether the finale was reached. `floors` events and checks, `floors.floorN.*` metrics; run it at 390x844 too (the touch UI on)", FloorsSeconds, "level"));
            list.Add(new PlaytestScenario(CompanionsId, "The Climbers (Docs/Story.md \"The Climbers\"): Rook the rival (Floors 996, 992, 955, 899, 858, 808), Wick the lamplighter (999, 993, 960, 902) and Pointer the fingerpost (900, 895, 860, 810), fourteen floors each opened twice directly (a setup shortcut) and played by the bot through real input until the scene ends: the climber appears ahead, speaks subtitles, then BRANCHES on the hero (hearts, diamonds, deaths, Grudges, the flags earlier scenes wrote), pays a small reward and walks on. The first run has a plain save, the second the save that makes the scene take its other branch (diamonds, deaths, flags set as setup shortcuts; for hearts the hero is hurt down to one heart); the two must differ. `companion` events (signal cmp:<who>:<floor>:<beat>, with branch:<label> and reward:<kind>), a check per floor and per climber (3+ encounters, 6+ branches). The floors run in the climb's order (progress only goes forward); under 10 min: no floor is begun after 5 min of run time, `at` (a floor like \"902\") starts later in the climb", CompanionsSeconds, "level"));
            list.Add(new PlaytestScenario(SpawnEdgeId, "Hazards and enemies appear just outside the view (Docs/GameplayCookbook.md \"Spawning off screen\"): Floor 998 (rain and cinders) and a generated Circle floor (Level 13) opened directly (a setup shortcut) and played by the bot through real input. Every hostile spawn is checked on the frame it exists against the camera's view (landscape or portrait, whichever the device is): never inside it; each fireball and cinder is followed until it first enters the view and must take 1+ s; a walker never sits beyond the Travel barriers. `spawn-edge` events, `check` events spawn-edge <beat>", SpawnEdgeSeconds, "level"));
            list.Add(new PlaytestScenario(IncomingId, "Incoming warnings (Docs/Story.md \"Incoming warnings\"): Floor 1000 opened directly (a setup shortcut), a known fireball put in from each edge (landscape or portrait, whichever the device is) and watched: its quiet marker must show on the right edge 1+ s before the fireball is in view, stay between 0.25 and 0.7 opacity, and be gone the moment it is. Fireballs that move away or pass beside the view get none; ten along the top are one band, never over the cap of 4; no marker over any HUD button; 400 updates allocate nothing; a pause freezes the marker; a rewind clears them and they come back matching the live fireballs; Settings > Accessibility > Incoming warnings off shows none. Then Floor 998's rain played by the bot: at most 4 markers, one band on the top, no flashing, every drop warned 1+ s ahead. `incoming` events, `check` events incoming <beat>, `incoming.*` metrics", IncomingSeconds, "level"));
            list.Add(new PlaytestScenario(FeelId, "The quick hero (feel): Level 1 loaded directly (setup shortcut), real input, Normal difficulty. Reports the hitbox dot on the hero, input-to-motion in ms (under 100), the time to cross one screen width holding D (2.5-3.2 s), the safe time after a hit (1.0-1.5 s, then he can be hit again), and the dash from the first second: Shift, then a right-half swipe on the virtual touchscreen (the move stick stays idle), each through an ember with no damage. `check` events feel <beat>, feel.* metrics (inputToMotionMs, crossSeconds, invulnerableSeconds)", FeelSeconds, "level"));
            list.Add(new PlaytestScenario(AudioId, "The sound of Level 1 (Docs/SoundAudit.md \"The audio scenario\"): after the page's tap-to-play (the menu must load within 20 s), Level 1 is loaded directly (setup shortcuts: the graze distance widened, a diamond dropped at his feet) and played by the bot for 60 s of game time through real input, with a jump, a dash and a rewind pressed at set moments (on a phone: a tap, a right-half swipe, R held). Every sound that starts is a `sound` event (id, take n/m, pitch, volume, bus), every voice line a `duck` event. Checks: a sound for every shot, hit, kill, graze, pickup, jump, land, dash and rewind; no take twice in a row; effects within 6% of pitch and voices unmoved; at most 4 copies of one effect at once; music at least 6 dB lower during each voice line and back after; Level 1 and then Floor 1000 each log an ambient bed (the Meadow's turns to its burning one); the master limiter is on the page. Run it at 390x844 too. Check events audio <beat>, audio.* metrics", AudioSeconds, "level", "audio"));
            list.Add(new PlaytestScenario(BeatDirectorId, "The beat director (docs/beats.md): Level 1's portal scene entered at its first phase (setup shortcut) and played four ways. Uncut: letterbox bars in within 0.3 s and inside the safe area, a camera move, speech-bubble captions 16+ px, no scanline overlay, 10 s or less. Played again in the same session: at least 40% shorter. Forgotten and played again, one tap (a virtual finger on a phone, a click on a desktop) 3.5 s in ends it within 0.5 s with the portal up. Then the bench scene beatTest: the bars slide out and input and rewind come back when the beat ends. Run it at 390x844 too. `check` events beat-director <beat>, beat.* metrics", BeatDirectorSeconds, "level"));
            list.Add(new PlaytestScenario(TapJumpId, $"Touch jump on the finger going down: level {TapJumpLevel} loaded directly (setup shortcut), no bot. A virtual touchscreen taps the middle of the screen " +
                $"{TapJumpTaps} times (30-120 ms down; every tenth tap is down and up inside one frame; every twentieth has the frame stall 150 ms with the finger down): each tap must make exactly one jump. " +
                "Then a tap while R rewinds must not be latched and must not jump when the rewind ends. Check events, tapJump.* metrics", TapJumpSeconds, "level"));
            list.Add(new PlaytestScenario(TouchControlsId, "Touch controls: a desktop run shows no touch UI; with the touch UI on (setup shortcut) level 2 shows a move stick (portrait: control strip below the playfield with a faint ~30% idle outline; landscape: floats under the thumb and fades while untouched), first-run hints, a 48+ px rewind button; a virtual finger drags the stick and the hero walks, a second finger holds rewind while the first still drags, a flick up jumps, and the hints go once used and are remembered. In the editor/standalone also resized to phone portrait and landscape. Check events touch-controls <beat>, touch.* metrics", TouchControlsSeconds, "level"));
            list.Add(new PlaytestScenario(MenuTouchLayoutId, "Main-menu stickers and phone touch controls together: the main menu's NOW WITH REWIND / NOW WITH GHOSTS stickers audited clear of each other and the buttons at 1920x1080, 1280x720, 1024x768, phone portrait and landscape (window resize in the editor/standalone; WebGL keeps the runner's device size), then the touch-controls scenario (stick fade/float, move, rewind, jump). check events for main menu sticker / touch-controls", MenuTouchLayoutSeconds, "menus", "level"));
            list.Add(new PlaytestScenario(AgainPhoneId, "Death to play without a load (run on device \"phone\", 390x844, touch): the bot plays level 3 to lose ten times; each Again! is a finger tap. Median of the first three taps to play under 1 s, no loading cover, no frame over 250 ms, the next-floor warm owner never held after an arrival. check events again-phone <beat>; metrics againPhone.*", AgainPhoneSeconds, "level"));
            list.Add(new PlaytestScenario(DeathRestartId, "Flow between attempts: the Landing's Waystone reached (setup shortcut), level 3 loaded directly, the bot plays to lose four times. Each death screen shows a quip under the title (varied: never the same twice running, 2+ different over the four) and an Again! button; pressing it with the mouse starts the next attempt in under 1 s of real time. Then level 3 is cleared by the bot: the results screen lists Time (m:ss) and Clean Kills. check events death-restart <beat>; metrics deathRestart.*", DeathRestartSeconds, "level"));
            list.Add(new PlaytestScenario(DeathReturnId, "The death screen's Return offer (Docs/Story.md \"Waystones and runs\"): the Landing's Waystone reached (setup shortcut), level 3 loaded directly with 25 diamonds and a Grudge, " +
                "the bot plays to lose; the screen offers \"Return for 10 diamonds\" and the button is clicked: level 3 again, 15 diamonds, a moment of safety, the run and Grudges kept. Then dying with 15 and skipping with Escape " +
                "(the Shaft drops him, nothing spent, run ended, Grudges gone), then dying with 5 (no offer, the drop). check events death-return <beat>", DeathReturnSeconds, "level"));
            list.Add(new PlaytestScenario(FirstRewindId, "Level 1's first minute as the tutorial: a shot crossing within 8 s, 4+ enemies by 25 s, neighbours moving, the meadow bed, the staged unavoidable volley (time to ~20%, pulsing rewind button 64+ px on a 390x844 touch phone, the hero's voiced 'Hold R.'), the press rewinding, a relief line, never two captions. check events first-rewind <beat>", FirstRewindSeconds, "level"));
            list.Add(new PlaytestScenario(OverlordChaseId, $"The chase seen in play (docs/world-bible.md \"The chase\"): level {OverlordChaseLevel} loaded directly (setup shortcut), the bot plays to lose, then rewinds the death (R, or a finger on the HUD button on touch). The Overlord's silhouette must climb out of the top of the screen, readable, and be gone after. check events overlord-chase <beat>; metrics overlordChase.*", OverlordChaseSeconds, "level"));
            list.Add(new PlaytestScenario(RewindFxId, $"Rewind that feels amazing (RewindFx, Docs/Story.md): level {RewindFxLevel} loaded directly (setup shortcut), played by the bot, then R held {RewindFxHold:0.0} s with the juice topped up (setup shortcut). While held: the screen effect (scanlines, colour split, tracking bar, streaks) is up, a reversed whirr loops with its pitch climbing, and the frame rate stays 30+ fps; on release the sound stops and the screen returns. check events rewind-fx <beat>; metrics rewindFx.*", RewindFxSeconds, "level"));
            list.Add(new PlaytestScenario(RewindJuiceId, "Longer, more rewarding rewind and readable HUD feedback (Docs/GameplayCookbook.md \"The arcade loop\", Docs/Story.md \"The HUD and menus\"): Level 1 loaded directly with the shop reset (setup shortcut): " +
                "the new game's juice must be 10 s. Then Yooba's rewind goods are bought through the shop's own purchase (diamonds given first, setup shortcut: Loop Jar x5, Bottomless Jar x2, Quick Unwind x3) " +
                "and Level 1 loads again: cap 24 s, rewind 75% faster. The bot plays through real input until a clean kill pays juice back: \"CLEAN KILL +0.5s\" pops in larger over 0.3 s, holds full opacity >= 0.6 s, " +
                "and is captured alongside the REWIND readout over Level 1's bright sky/grass; three successive rewards verify stack slots with zero overlap; then Floor 955 (dark floor, setup shortcut) plays through real input " +
                "to a clean kill and captures the popup and REWIND readout over dark slag (at 1280x720 and 390x844; WebGL uses the desktop and phone runner devices). check, capture, juice events", RewindJuiceSeconds, "level"));
            list.Add(new PlaytestScenario(SideFloorsId, "Secret floors and gauntlets (Docs/Story.md \"Secret floors and gauntlets\", cookbook \"A secret floor\"): the eight side floors, each reached " +
                "through real input from the door in the wall on its host floor (the Heat Stairs: Floors 910-903; the host is opened directly, a setup shortcut, with diamonds and a Grudge in hand " +
                "so any door's cost can be paid) and played to the end. Per floor, `check` events: the door was offered (`side-door` event, its cost on the card) and taken, the cost was paid, " +
                "the run now leads to the side floor, the side floor is cleared (`side-floor` events: side:enter, side:guard, side:reward:<kind>, side:cleared), what it paid is in the save " +
                "(a lore flag, the Slag Ladle unlocked for good, the hidden Held Breath, 20 diamonds, a full health and rewind refill, a gauntlet's purse or flag) and the run leads on to the floor after " +
                "the host. The two gauntlets also chain 4 and 5 waves (side:wave:N) with a rewind refill (side:refill:N) between them, the juice full at each wave's start. The last check: every reward is different. " +
                "Under 10 min: no room is begun after 3.5 min of run time (the rest are logged as not reached); `at` (a door key, a host floor like \"906\", or a side floor's level file) starts later in the table. The `gauntlets` scenario is the last two alone", SideFloorsSeconds, "level", "secret"));
            list.Add(new PlaytestScenario(ReplayModesId, "Replay modes (Docs/Story.md \"Again\", cookbook \"Replay modes\"): Endless Floor, the Daily Challenge and New Game+, each opened from the main menu with real clicks " +
                "(a card with the mode's best / table / cycle, then Play). Endless Floor: the bot climbs 3+ floors (the counter goes down for each push fought off) and the best is saved " +
                "(`replay` events, `check` events `replay-modes endless ...`) and shown on the card afterwards. The Daily Challenge on a fixed date (a setup shortcut) is played twice: the same seed, " +
                "the same twist of the day and the same first generated enemies both times, and both plays are in the date's local table, shown on the card with their places. " +
                "New Game+ (an unlocked weapon and the Waystone on 901, setup shortcuts) starts the next cycle at the Landing with the weapon still in his hands, enemy health and fire rate scaled " +
                "(`replay ngplus` event) and the first enemy tougher than its file. ~20 min", ReplayModesSeconds, "level"));
            list.Add(new PlaytestScenario(GauntletsId, "The two gauntlet floors alone (the Cage Match behind Floor 908's cage, the Encore behind Floor 904's curtain): the secret-floors run over just " +
                "these. Each is reached by its door in the wall, then 4 / 5 waves are cleared by the bot with a rewind refill between them (`side:wave:N`, `side:refill:N` events; the juice is full at " +
                "each wave's start) and the purse is paid", GauntletsSeconds, "level", "secret"));
            list.Add(new PlaytestScenario(BystanderId, $"A bystander in a scene (Docs/GameplayCookbook.md \"Bystanders\"): the scene \"{BystanderScene}\" on level {BystanderLevel}'s stage (setup shortcut), " +
                "played through real input. A neutral damned soul stands behind the hero looking away. A scene walks it (walkEntityTo: it walks toward him and faces the way it goes), " +
                "turns it away (faceEntity) and plays its Hiccup animation (playEntityAnimation). He walks up (D); inside 4.5 units it turns to him and speaks a subtitle. An imp appears " +
                "behind it and he shoots along the ground: the shots pass the soul and it flinches (the witness trigger, ShotPassed), then settles back; the imp's shots hurt him " +
                "and the soul flinches and speaks again (HeroHurt; a scene rule hurts him if the imp hasn't in 15 s). Last, hold R back to before it noticed him: its facing and place " +
                "are restored. Each beat is a check event; bystander events log the actions and signals", BystanderSeconds, "level"));
            list.Add(new PlaytestScenario("analytics", $"Hawkes Analytics (AGENTS.md \"Analytics\"): the SDK's debug log on, then Level {AnalyticsLevel} " +
                "(the Meadow, loaded directly) played until Hell kills the hero three times (the bot holds R once early on for a rewind, then plays to " +
                "lose): each death screen, and on the third one the feedback card (\"Too hard? Not fun?\", thumbs and a comment) over it, held while it's " +
                "open, then Escape goes on; then what went out: level_start, death (with its cause), feature rewind and level_end, each sent with the build " +
                "version and the env (beta on the beta site). Each beat is a check event, and every event the game sends is an analytics event", 420, "level"));
            var descent = BridgeDescentLevels();
            if (descent.Count > 0 && LevelNumberOf(BridgeWraithLevel) > 0)
                list.Add(new PlaytestScenario(BridgeDescentId, "Circle VIII's lower half (Docs/Story.md \"The Rune Bridges' lower half\"): " + PathRange(descent) + " (the Lamplighter on Floor 751, the halfway floor, " +
                    "the afterimage on 745, the rune hound on 735, the sleepwalker on 725, the last crossings), each opened directly (a setup shortcut) and played by the bot through real input, " +
                    "once more after a death, the Lamplighter's floor with its boss checks (3+ phases, 4+ minions, the hero's hint); then Floor 701 as level-N: the Bridge Wraith's Great Span, " +
                    "her phases in order (The Lantern, Snuffed, Unwritten), the minions, every attack after its glow, the hero's hint line, her voice, the Waystone at 701 and the Shaft waking Circle VII (voiced). " +
                    "The whole lower half runs as rune-bridges-3 and on.", PathRunSeconds(descent) + BridgeWraith.Seconds + 240f, "full", "levels"));
            list.Add(new PlaytestScenario(SpireFiend.Scenario, $"The Spire Fiend's fight, Circle IX's Warden (Docs/Story.md \"The Spire Fiend's fight\"): " +
                $"\"{SpireFiendLevel}\" (Floor 801, the Spire's Heart) loaded directly (a setup shortcut), the bot fighting through real input, up to 3 tries. " +
                "His generated animations seen at game size, each a check event: Rise (unfolding out of his spire), Idle, Slam (claws into the floor), " +
                "Throw (a spire off his back), Hurt, Enrage, IdleEnraged, SlamEnraged and his Die clip where he fell; every line with its subtitle and " +
                "his own voice (spireFiend). On the last try the arena checks: " + SpireFiendArena + ". A won fight goes on: Continue on the results, " +
                "the Shaft's climb to 801 with the Waystone saved and lit and Circle VIII, the Rune Bridges, waking up (voiced), then the level select",
                SpireFiend.Seconds + 120f, "level"));
            list.Add(new PlaytestScenario("magma-king", $"The Magma King's fight in \"{MagmaKingLevel}\" (the Slag Throne, loaded directly, a setup shortcut; " +
                "the bot plays the fight through real input, up to 3 tries): his generated animations seen at game size, each a check event: Idle, " +
                "Throw (molten boulder) and SlamSeat (fist on the throne's arm, shockwave) from the throne, Hurt, then at half health Rise " +
                "(the only time he stands), IdleStand, Slam (ground-pound), Breath and HurtStand standing, Enrage at a third, and his Die clip playing where he fell; " +
                "his on-screen height; every line with its subtitle and his own voice (intro, phase 1, hurt, stand, guards, enrage, hurt, defeat), the " +
                "announcer's \"HAS FALLEN\" and the hero's replies", MagmaKing.Seconds, "level"));
            list.Add(new PlaytestScenario("shop-gate", "Yooba's gated goods and the Not Today Grudge: an old save (setup shortcut: one Tummy-Dipped Shots bought the old way, " +
                "no unlock flags saved) keeps its purchase; the shop hides the gated good (Double-Dipped Shots) until Tummy-Dipped Shots is level 2, bought with real " +
                "clicks, then shows it; the shop's prices are reported (diamonds to max everything, with and without the gated good). Then the scene " +
                $"\"{GrudgeScene}\" on level {CutsceneLevel}'s stage offers a Grudge pick that must list Not Today (picked with 1: the hero's hit is followed by " +
                "a longer invincibility). Each beat is a check event, `shop.*` metrics carry the prices and `level<n>.diamonds` the diamonds each level paid", 150, "menus"));
            list.Add(new PlaytestScenario(ShopGoodsId, "Yooba's new goods and the new Grudges: the shop lists 20+ new goods (a card tag each) priced on the retuned curve, every level is bought with the shop's own purchase (setup shortcut: the purse), " +
                "Level 1 starts with their perks in the hero's hands (totals, full bubbles, plates, bombs and gasps, slower enemy shots, a cheaper rewind), the 8+ new Grudges are granted one by one and each adds its perk, " +
                "then the bot plays and the perks that fire (crit, bubble pop, plate, slip, fat diamond, burp, bomb, gasp) are logged as `perk` events. Each beat is a check event", ShopGoodsSeconds, "level"));
            list.Add(new PlaytestScenario(ShopBalanceId, "Yooba's health line: a fresh save with the line's price (setup shortcut), ten Goo Plasters bought with real clicks (+2 hearts each, ~430 diamonds, the purse ends at 0), Level 1 starts with 25 hearts and the HUD health bar is one smooth bar with its count, wide enough to read (run it on 1920x1080 and a 390x844 phone). The other goods' prices are logged. Each beat is a check event", 120, "menus"));
            list.Add(new PlaytestScenario(StagedMenuId, "The staged main menu and Yooba's tabbed counter: a brand-new save's menu has at most three buttons (Play, Settings, Rage Quit where a page can quit), the first diamonds bring Visit Yooba with a NEW sticker that goes when pressed, Pick a Level, Rewatch Stuff and the Codex wait for Floor 1000 and the replay cards for the first Waystone; in the shop every card leads with its effect in numbers (the joke name and quip second), the wares sit on Body / Rewind / Loot tabs with six cards a page, the ones the hero can buy are marked, Yooba points at one glowing ware with a voiced line on the first visit, and the layout is audited per tab at the runner's size (1280x720 and 390x844 in the editor); then a returning save shows every button it had, with no stickers, and New Game's reset takes none away (setup shortcuts: the fresh save, the first diamonds, the returning save). Each beat is a check event", 240, "menus"));
            list.Add(new PlaytestScenario(ShopWeaponsId, "Yooba's weapon goods: the shop hides Slimed Knuckles, Stretchy Wraps and Keep Weapons while no weapon is unlocked, shows them once the fists are, Keep Weapons and Slimed Knuckles bought with real clicks make Yooba accuse the hero of stealing from him (a ShopBuyWeapon line, voiced), and a run ending keeps the fists only with Keep Weapons bought (setup shortcuts: the unlock, the run end). Each beat is a check event", 120, "menus"));
            list.Add(new PlaytestScenario(YoobaAliveId, "Yooba's living shop (Docs/Story.md, \"Yooba's shop\"), clicked with the virtual mouse: the shelf starts with one jar and nine gaps, " +
                "fills as floors are cleared (setup shortcuts: levels marked cleared, deaths counted) and Yooba points out each new ware, asks for the Lost Things quest, reacts to the deaths " +
                "and to a Goo Plaster bought, fidgets (blink, look, hop, hum, sniff) and mutters to himself when it is quiet; with the three lost things found (setup shortcut: the flags " +
                "their pickups on Floors 997, 995 and 992 write) he pays the reward once and the Thank-You Jar joins the shelf. At least 20 reactive lines. Each beat is a check event", 300, "menus"));
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
            list.Add(new PlaytestScenario("one-way", $"One-way platforms on a test floor: the scene \"{OneWayScene}\" on level {OneWayLevel}'s stage (setup shortcut), " +
                "played by the bot through real input. First the drop's dead zone: a quick tap of S on a platform and a run across platforms with " +
                "the stick a little low must not drop him (a drop needs a deliberate hold, player.json dropThroughHoldSeconds). A climb where each ledge and stone hangs right over the last: the hero jumps up through each " +
                "from below and lands on top. Then a run where the way on is down: he holds S on the shelf and drops through it onto the ledge under " +
                "it, then holds the gamepad's left stick down on a sinking stone and drops through that; on the carved landing (solid ground) he holds " +
                "down and stays on it. Each beat is a check event, every pass a one-way event", OneWaySeconds, "level"));
            list.Add(new PlaytestScenario("mechanics", $"Hell's tricks on a test bench: the scene \"{MechanicsScene}\" on level {MechanicsLevel}'s stage (setup shortcut), " +
                "played by the bot through real input, five stations one after another. A ricochet imp's ember bounces off the ground (bounce events with the bounces left); " +
                "a blink imp shows its spot (a mark and a flash) for 0.5+ s and then appears there (teleport events); a bulwark imp with a front shield blocks shots " +
                "from the front (shieldBlock events, no damage) and takes them from behind until it dies; a lead imp's shots go where the moving hero will be " +
                "(checked against the intercept maths); the Overlord's lens shows a thin warning line, then sweeps a beam that hurts the hero where it crosses him. " +
                "Each beat is a check event", MechanicsSeconds, "level"));
            list.Add(new PlaytestScenario(BestiaryId, $"The Ember Wastes' ten new enemies (Docs/Story.md \"The Wastes' bestiary\"): checked as data (each has a name, a hint, a hint voice and a place in the Circle's enemy table; no two share an attack), then the arena \"{BestiaryScene}\" on level {BestiaryLevel}'s stage (setup shortcut) played by the bot through real input, one creature per station: the Halo imp's ring of embers, the Whirl imp's spiral, the Seam imp blinking behind the hero after a mark, the Slag tortoise's front shield blocking shots, the Magma geode splitting in two, the Mine beetle laying mines, the Lens imp's red line then sweep, the Pyre caller calling cinders, the Slag hound crouching then lunging, the Deadeye imp's fast led bolt. One check event per creature; bestiary events log what each did", BestiarySeconds, "level"));
            list.Add(new PlaytestScenario(RoadBestiaryId, "The Cracked Road's ten new enemies (Docs/Story.md \"The Cracked Road's bestiary\"): checked as data (name, hint, hint voice, sounds, a place in crackedRoad's enemy table, no two sharing an attack), then the arena \""+"roadBestiary"+"\" on level 1's stage (setup shortcut) played by the bot through real input, one creature per station: the Plate demon's shield, the Rift bomber's blink and bomb, the Bounce lobber's bouncing lump, the Chained pair sharing a blow, the Risen demon and the Bastion brute getting up once, the Loop wraith passing shots until the bot holds R, the Veil demon's shield and blink, the Slab sniper's fast bolt, the Fuse whelp. One check event per creature", RoadBestiarySeconds, "level"));
            list.Add(new PlaytestScenario(ElitesId, $"Elite variants and mid-bosses (Docs/Story.md \"Elites and mid-bosses\"): checked as data (twelve elites with a name, a hint, a hero voice line with a clip and the elite tag; four mid-bosses with phases), then the arena \"{EliteScene}\" on level 1's stage (setup shortcut) played by the bot through real input, one at a time: every elite and mid-boss appears and the mid-bosses' wave and rage phases are seen; then the four floors with a mid-boss (955, 905, 895, 855) played from their start with the hero's hints forgotten: the floor's elites come onto it and the hero introduces each (hint events), the mid-boss's intro, fight, wave, rage and defeat signals come in order. One check event each; elite, hint and midboss events log what happened", ElitesSeconds, "level"));
            list.Add(new PlaytestScenario("video-" + OverlayVideo, $"Video {OverlayVideo}, started directly (setup shortcut) and watched instead of skipped: " +
                $"the overlay \"{OverlayName}\" (the hero) appears as the camera goes through the burning door, spins 3+ turns and shrinks into the tunnel's " +
                "centre and is gone before the lava river (~14.5 s); holding R mid-spin plays it backward; then Escape skips the video. Each beat is checked", 75, "video"));
            list.Add(new PlaytestScenario("video-" + OverlayVideo + "-tunnel", $"Video {OverlayVideo}, started directly (setup shortcut) and watched with no input " +
                "from the burning door through the fire tunnel to the lava river (10.6-17.5 s): no COMING SOON caption in the tunnel (screen pixels in the box " +
                "it used to cover), the frame on screen matches the clock, the soundtrack matches the clock, and the video is as long as its soundtrack", 60, "video"));
            list.Add(new PlaytestScenario("full", "New Game, then every level in order through the real flow, as far as the bot gets", 600, "full"));
            var path = PathLevels();
            const string pathHow = "each level opened directly (a setup shortcut: no videos, no Shaft, and a level the bot can't clear never stops the run) " +
                                   "and played by the bot through real input with a time limit (a floor 160 s of game time, a floor boss's or Warden's floor 250 s, " +
                                   "a level off the path its duration + 30 s, at most 250 s), " +
                                   "once more after a death, both tries capped at about 4 minutes of real time (a floor boss's 5); no new level starts after " +
                                   "38 minutes of run time and the run ends by 43 (the runner's 60 minutes include encoding the video afterwards), and every level not reached is in the table as NOT REACHED (TIME BUDGET), with budget events. Each level ends with a level-result event: CLEARED, DIED (where: phase, floor, the hero's place, " +
                                   "path section; by what: the last thing that hurt him) or STUCK (the stuck event's phase, place, path section and the bot's " +
                                   "ClimbTarget); the run ends with a summary event and the per-level table (table events, and the same table in the report's log)";
            var every = AllGameLevels();
            var offPath = OffPathLevels();
            list.Add(new PlaytestScenario(AllLevelsId, $"Every level in game.json, in its order ({PathRange(every)}): the path ({PathRange(path)}: the Meadow, " +
                "then each floor of the played route, past the shortcuts' skipped floors), then the levels after it " +
                $"({PathRange(offPath)}: the long way's old 10-floor stages and the arcade levels), also in parts that run side by side ({ChunkPrefix}1-{Chunks().Count}); " +
                "\"at\" (a floor or level) starts it there: " + pathHow, PathRunSeconds(every), "full", "levels"));
            list.Add(new PlaytestScenario(OffPathId, $"The levels in game.json after the path ({PathRange(offPath)}: Circle IX's long way, Levels13-18, " +
                "Circle X's old stages, Level3-10, and the arcade levels), the all-levels run over just these with a check event per level " +
                "(cleared, or died/stuck and where, with its time): " + pathHow, PathRunSeconds(offPath), "full", "levels"));
            for (int part = 1; part * CrackedHeatPerPart - CrackedHeatPerPart < CrackedHeatLevels().Count; part++)
            {
                var heatLevels = CrackedHeatLevels(part);
                list.Add(new PlaytestScenario(part == 1 ? CrackedHeatId : CrackedHeatId + "-" + part, "Circle IX's heat pass (Floors 900-892, 860-852 and 810-802, long bullet-hell floors, part " + part + ": " +
                    PathRange(heatLevels) + "): each floor opened directly (a setup shortcut) and played by the bot through real input, once more after a death. A check event per floor " +
                    "(cleared or how not, its time: a long floor runs 4-6 minutes), a `setpiece` event per set piece and a `density` event per piece (enemies and shots alive), the ladder " +
                    "climbs (`ladder` events), and the `long.hotter` check: the floor's average density against Circle X's long floors (a `heat` event with both): " + pathHow,
                    PathRunSeconds(heatLevels), "full", "levels"));
            }
            for (int part = 1; part * RuneBridgesPerPart - RuneBridgesPerPart < RuneBridgeLevels().Count; part++)
            {
            var runeLevels = RuneBridgeLevels(part);
            list.Add(new PlaytestScenario(part == 1 ? RuneBridgesId : RuneBridgesId + "-" + part, "Circle VIII, the Rune Bridges (Floors 800 on, one level per floor): each floor opened directly " +
                "(a setup shortcut) and played by the bot through real input, once more after a death. Floor 800 is the safe intro: its spans wait for the hero and " +
                "no enemy appears; the rest teach the beat, the out-of-step rows and the Watchers' gaze. A check event per floor (cleared or how not, its time: " +
                "a long floor runs 4-6 minutes, its set pieces and density) and the rune checks (every kind of span hummed, lit and went out: `rune` events " +
                "and the `rune` check): " + pathHow, PathRunSeconds(runeLevels), "full", "levels"));
            }
            var ladderLevels = LadderFloorLevels();
            if (ladderLevels.Count > 0)
                list.Add(new PlaytestScenario(LadderFloorsId, $"The floors with a ladder ({PathRange(ladderLevels)}: Floors 999 and 998 today): each opened directly (a setup shortcut) and played by the bot " +
                    "through real input, once more after a death. The bot walks to the ladder's foot and holds up to climb it while the lava rises (999) and the ember rain and cinders fall (998). " +
                    "A `ladder` event per grab and exit, a check event per floor: the ladder was climbed to its top (grabbed, left by the top), and cleared or how not, with its time: " + pathHow,
                    PathRunSeconds(ladderLevels), "full", "levels"));
            var climbStart = ClimbStartLevels();
            list.Add(new PlaytestScenario(ClimbStartId, $"The start of the climb ({PathRange(climbStart)}): Level 1 (the Meadow), then Floors " +
                $"{ClimbStartTopFloor}-{ClimbStartLastFloor} one level per floor, the all-levels run over just these with a check event per level " +
                "(cleared, or died/stuck and where, with its time; a floor should take 1-2 minutes), and the foothold and lava-over-foothold events " +
                "of every floor: " + pathHow, PathRunSeconds(climbStart), "full", "levels"));
            foreach (var stretch in PathStretches())
            {
                var stretchLevels = path.FindAll(n => n >= stretch.from && n <= stretch.to);
                list.Add(new PlaytestScenario(StretchId(stretch), $"Floors {PathFloor(stretch.from)}-{PathFloor(stretch.to)} of the path " +
                    $"(levels {stretch.from}-{stretch.to}), the all-levels run over just this stretch with a check event per level (cleared or how " +
                    "not, its time and the floor's pace: 1-2 minutes, a floor boss's up to 3) and the foothold and lava-over-foothold events: " + pathHow, PathRunSeconds(stretchLevels), "full", "levels"));
            }
            AddStartScenarios(list, pathHow);
            list.Add(new PlaytestScenario("story-path", "New Game through the story path with the real flow: Video1, Level 1 (the Meadow and its portal " +
                $"scene), Video2, Level 2 (Floor {LandingFirstFloor}: The Landing, no arcade level in between: its floor counter on {LandingFirstFloor}, " +
                $"ticking to {LandingLastFloor} on its landing), Video3 (Hell's Ad Break, watched 13 s so Yooba's spot plays, then skipped), then Circle X's played route one floor per level " +
                "with no video between them: Levels 3-11 (Floors 999-991, the Imp Foreman), where the bot takes the Sack Lift (its diamond fare topped up " +
                "first, a setup shortcut) to Level 12, Levels 12-21 (Floors 960-951, the Pile Keeper), where it takes the King's Chimney to Level 22, Levels " +
                "22-30 (Floors 910-902, the Heat Stairs up to the Magma King's door) and Level 31 (Floor 901, the Slag Throne: his arena, his Waystone at 901 " +
                "and Circle IX waking on the Shaft); the old 10-floor stages (Levels 68-75) are never played. Each level with its title, voiced lines, floor " +
                "counter, new enemy alone and breather checked and the Shaft's climb (or a shortcut's dotted jump) after it; then the level select, whose " +
                "cards list the story levels first and in order. Each step is a check event", StoryPathSeconds, "full"));
            list.Add(new PlaytestScenario("wastes-climb", $"The Heat Stairs, one level per floor (Docs/LevelDesign.md): level {ClimbFrom} (Floor 910) loaded " +
                $"directly (setup shortcut), then the real flow on through level {ClimbTo}: results, Continue, the Shaft climbing one floor, and the next floor " +
                "with no video between, Floors 910-901. Each floor gets the stage checks (title voiced, hero line, counter, new enemy alone, at most 3 enemies, " +
                $"length, Grudge) and Level {ClimbTo - 1} (Floor 902, The Door) ends at the Magma King's door with his knock heard from the other side; " +
                $"Level {ClimbTo} is his arena (the boss checks of a Warden's arena), its Shaft standing on 901 with the Waystone lit and Circle IX waking; " +
                "then the level select. Up to 3 tries per floor; each beat is a check event", WastesClimbSeconds, "full"));
            list.Add(new PlaytestScenario("landing-stretch", $"The first floors of Hell, one level per floor (Docs/LevelDesign.md): level {StretchFrom} (Floor 1000, " +
                $"The Landing) loaded directly (setup shortcut), then the real flow on through level {StretchTo} (Floor 996): results, Continue, the Shaft " +
                "climbing exactly one floor (1000 to 999, ... 996 to 995), Video3 (Hell's Ad Break) and Yooba's hello after Floor 1000 (skipped like a player), and the next " +
                "floor. Each floor gets the stage checks (title \"FLOOR N: PLACE\" voiced, hero line, counter ticking to the next floor on the landing, new " +
                "enemy alone, breather, at most 3 enemies, 60-120 s, every line voiced, the Grudge pick) and its own idea: 1000 sinking stones taking his " +
                "weight; 999 the lava rising behind him with the \"ONLY 999 TO GO\" beat and its wave chasing him sideways; 998 the village's wreckage " +
                "sinking under him and the cinders; 997 sack imps swinging on the hauling chains and an empty chain ridden; 996 lava falls and geysers " +
                "blowing on their beat. Up to 3 tries per floor; then Floor 996 must lead on to the next level. Each beat is a check event",
                StretchSeconds, "full"));
            list.Add(new PlaytestScenario("foreman-stretch", $"Floors 995-991, one level per floor (Docs/LevelDesign.md): level {ForemanFrom} (Floor 995, " +
                $"The Loop) loaded directly (setup shortcut), then the real flow on through level {ForemanTo} (Floor 991, the Imp Foreman): results, Continue, " +
                "the Shaft climbing exactly one floor after each, and the next floor. Each floor gets the stage checks (title \"FLOOR N: PLACE\" voiced, hero " +
                "line, counter ticking to the next floor on the landing, new enemy alone, at most 3 enemies, every line voiced, the Grudge pick) and its own " +
                "idea: 995 the same stones sinking by themselves in the same order lap after lap (each loop stone sinks twice or more, the second lap called " +
                "out, \"It's doing that on purpose.\"); 994 two chain lifts ridden while imps hack at the chains (the lift drops a notch); 993 the imps that " +
                "throw, the camera holding on the hollows until both are down, the sleeper falling off its own ledge; 992 crumbling stones and the collapsing " +
                "bridge; 991 the first floor boss on a climbing arena: the voiced intro (announcer, the Foreman, the hero), the boss bar, phase 1 on tier 1 " +
                "(whip cracks after a 1 s glow on the floor, sack imps whistled onto the swings), at half health the lava taking tier 1 and the run along the " +
                "gantry, a chain lift to tier 2, phase 2 (he swings on the lift's chain dropping embers, each after a 1 s glow on its spot), his wings for show, " +
                "the defeat in slow motion with a pile of diamonds, the announcer's call; 60-180 s. Up to 3 tries per floor; then Floor 991 leads on to the next level. " +
                "Each beat is a check event", ForemanSeconds, "full"));
            list.Add(new PlaytestScenario("long-way", $"The old 10-floor stages over Floors 990-951, the long way's stand-ins (Docs/Story.md, \"Level numbers\"): " +
                $"level {LongWayFrom} (Floors 990-981: The Spill) loaded directly (setup shortcut), then the real flow on through level {LongWayTo} " +
                "(Floors 980-971: Imp Run, Floors 970-961: The Slag Falls): results, Continue, the Shaft climbing the stage's ten floors, and the next stage " +
                $"with no video between; level {LongWayTo} must lead on to Floor 960. Then level {LongWayLoop} (Floors 960-951: The Loop, played only from " +
                "the level select) loaded directly (setup shortcut) and played the same way, its Shaft climbing to 951. Each stage gets the stage checks: " +
                "its title banner (\"FLOORS N-M: PLACE\") with the announcer's voice, the hero's lines with his voice, the floor counter from its first " +
                "floor to its last, its carved sign, at most one new enemy type and the first one alone, a 3+ s breather, at most 3 enemies at once, " +
                "The Loop's repeated floor on two signs, the Grudge pick. Up to 3 tries per stage; each beat is a check event", LongWayStagesSeconds, "full"));
            list.Add(new PlaytestScenario("circle-x-route", $"Circle X's played route (Docs/LevelDesign.md, \"The route through Circle X\"): first the long " +
                $"way: level {RouteFrom} (Floor 991, the Imp Foreman) loaded directly, the bot pressing 1 on the Sack Lift's choice: the choice within " +
                $"{ForemanExitSeconds:0.#} s of his defeat (else it stops there), kept climbing, the counter on 990, the Grudge pick, the Shaft and the long way's " +
                "stage starting on FLOOR 990 (the Shaft's save put back after). Then level " + RouteFrom + " loaded directly again with the diamonds for the " +
                $"Sack Lift's fare (setup shortcut: topped up to {SackLiftFare}), then " +
                $"the real flow on through level {RouteTo} (Floor 901), the bot taking every shortcut (it presses 2). Checked: after Floor 991's defeat the Sack " +
                "Lift's choice with its fare on the card (\"FARE: 20 DIAMONDS\"), the fare paid, the hero's line and the announcer's reaction, the counter " +
                "jumping from 990 to 960 with no Grudge pick, the Shaft's marker climbing to 990 and jumping a dotted line up to FLOOR 960, and Floor 960 " +
                "next; then Floors 960-952 one floor each, Floor 951's Pile Keeper (voiced intro, boss bar, his spear and sack attacks after a 1 s glow, the " +
                "mound sinking at half health, his guards only on tier 2, the defeat with a pile of diamonds), the King's Chimney on his landing (its risk on " +
                "the card), the jump from 950 to 910 on the counter and the Shaft, Floor 910 played harder for it (the King's \"Came up my chimney\" line), " +
                "Floors 909-902 one floor each up to the Magma King's door, and Floor 901, his arena, whose defeat lights the Waystone at 901 and wakes " +
                "Circle IX on the Shaft; the old 10-floor stages are never played. Up to 3 tries per floor; each beat is a check event", RouteSeconds, "full"));
            list.Add(new PlaytestScenario("cracked-road-route", $"Circle IX's played route, one level per floor (Docs/LevelDesign.md section 6): level {CrackedFrom} " +
                "(Floor 901, the Magma King) loaded directly (setup shortcut) and fought, then the real flow on: results, Continue, the Shaft, and level 32, " +
                "Floor 900 on the Cracked Road, next. Then Floors 900-891 one floor each (the Brimstone Demon arriving alone on 900, each floor's own " +
                "pieces and monsters, at most 4 enemies, 60-120 s, the floor number carved on each landing), Floor 891's Road Breaker (voiced intro, boss bar, " +
                "his slams and kicked lumps after a 1 s glow, the road giving way at half health, whelps only on tier 2), the Spared Demon on his landing " +
                "(a Grudge paid, the counter jumping from 890 to 860, the Shaft's dotted jump), Floors 860-852, Floor 851's Carver, the Hollow Spire (its risk " +
                "on the card, 850 to 810), Floor 810 played harder for it, Floors 809-803 and Floor 802 up to the crack in the great spire, then Floor 801, " +
                $"the Spire Fiend's arena (the Warden's arena checks; his defeat lights the Waystone at 801 and wakes Circle VIII on the Shaft), then the " +
                $"level select. Up to 3 tries per floor; each beat is a check event", CrackedSeconds, "full"));
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
                "Floor 999; Video3 loaded directly and skipped after 6 s): his talking portrait (idle and talk loops), every hello line with its voice and the " +
                "hero's one-line reply, then Continue starts Level 3. Then three Shaft previews over the menu (setup shortcuts) where he reacts to the floor: " +
                "950, a death on the way, the Magma King's door. Then the shop with his portrait: welcome, a card clicked with 0 diamonds (can't afford), " +
                "the same card with enough (buy), Back (goodbye). Each beat is a check event", YoobaSeconds, "menus"));
            list.Add(new PlaytestScenario(NetOutageId, "The Hawkes Net server is down (setup shortcut: every attempt fails with 502, then 410): at most one short warning per retry and no errors, " +
                "retries on a growing timer, the menu's quiet offline chip, Level 1 played through, a 410 stops the retries, then the server returns and the game is online again with no reload. Each beat is a check event", NetOutageSeconds, "menus"));
            list.Add(new PlaytestScenario(FloorCardId, "Floors 1000-997 each opened directly (setup shortcut): the stamped floor card shows its number and name, the stamp sound plays, it sits inside the safe area, is gone within 1.5 s and never locks input or leaves a dark screen. Run it at 390x844 touch too. Each beat is a check event", FloorCardSeconds, "level"));
            list.Add(new PlaytestScenario(IntroSkipId, "The intro film (Video1, loaded directly: setup shortcut): it plays or the game moves on within 2 s (no black wait), " +
                "the on-screen buttons are 48+ px tall, and one click on empty screen skips it within 1 s. Each beat is a check event", IntroSkipSeconds, "menus"));
            list.Add(new PlaytestScenario(FirstFiveId, "First five minutes (Docs/Story.md \"First five minutes\"): fresh save, New Game with no difficulty card, play from what is on " +
                "screen for 5 minutes; reports seconds from page open to menu / first move / first hit / first rewind, the longest locked or non-interactive stretch " +
                "(portal, Shaft, results) with its name, and choice screens before play. One tap skips the portal and the Shaft plunge; Video2 stays unlocked for Rewatch. " +
                "Run desktop and at 390x844. check events first-five <beat>; firstFive.* metrics", FirstFiveSeconds, "smoke"));
            list.Add(new PlaytestScenario(PhoneLoadId, "How fast and light the game opens (Docs/BuildSize.md; run it on device \"phone\"): no input until the main menu " +
                "opens by itself; the page's loading bar fills to 100% and goes away, the boot's bar moves with its percentage to 100%, the main menu is up within " +
                $"{PhoneLoadMenuBudget:0} s of the page opening, what was downloaded by then (files, bundles, MB), then {PhoneLoadMinFps:0}+ fps on the menu and over " +
                $"{PhoneLoadPlaySeconds:0} s of Level 1 (loaded directly: setup shortcut) played by the bot. check, load and download events; load.* metrics", PhoneLoadSeconds, "smoke"));
            list.Add(new PlaytestScenario(Video3Id, "Video3, \"Hell's Ad Break\" (Docs/LoreVideos.md), the film after Floor 1000: its config (after level 2, next level 3), the film loaded directly " +
                "(setup shortcut: the Shaft's marker at Floor 999, Yooba's save reset, the video unlocked) must play with its clock running for 10 s, Escape must skip it into " +
                "Yooba's hello on the Shaft, and the main menu's Videos card must list it and replay it to its end and back to the main menu. Each beat is a check event", Video3Seconds, "menus"));
            list.Add(new PlaytestScenario(BeatsId, "The opening and the level beats (Docs/Story.md \"Why he is sick of it\"): New Game on a fresh save plays Video1, Escape skips it, " +
                "Level 1 starts and shows its beat line; Floors 1000-997 are loaded directly (setup shortcut) and each shows its own beat. Every beat is a `beat` event, " +
                "no two alike; `check` events `beats <beat>`", BeatsSeconds, "menus"));
            list.Add(new PlaytestScenario(StoryId, "Arrival beats (Docs/Story.md \"Arrival beats\"; play it on device \"phone\", 390x844 touch): Level 1, then Floors 1000-997 loaded directly (setup shortcut, " +
                "the beats cleared, touch UI on), each played through the move stick. Each floor must show a storyBeat event (a stinger, a shake, a hero reaction), a voiced hero line, a caption of one short line " +
                "clear of the hero, input locked at most 1.5 s and the hero under the thumb afterwards; Floor 1000 loaded again must not replay its beat. Reports quality.textOnlyBeats. `check`, `beat` events", StorySeconds, "menus"));
            list.Add(new PlaytestScenario("yooba-shop", "Yooba's shop from the Shaft: the Shaft after Video3 (setup shortcut: Yooba's save reset, the marker " +
                "at Floor 999, his hello skipped), Visit Yooba beside Continue opens his shop over the Shaft with his animated portrait and voiced welcome, " +
                "slime-made goods; a card clicked with 0 diamonds (can't afford), then with its price (bought: diamonds spent, level saved); Back (goodbye) " +
                "returns to the Shaft and Continue starts Level 3 with the goods applied; the main menu's Visit Yooba shows them still bought; a climb " +
                "between floors (preview) shows Visit Yooba again. Each beat is a check event", 240, "menus"));
            list.Add(new PlaytestScenario("yooba-greetings", "Yooba's greetings (acceptance: voiced, no repeats within 5 minutes, the portrait bobs, one-line caption): the Shaft after Video3 (setup shortcut), " +
                "then ten back-to-back Visit Yooba / Back round trips. Every welcome and goodbye is voiced (a clip), no welcome or goodbye line repeats while another of its pool is unused or inside 300 s, " +
                "the portrait's lift swings while he speaks and his caption has no line break; `check`, `shop-greeting` events", 360, "menus"));
            list.Add(new PlaytestScenario("waystone", "Waystones and runs: a save where the Landing was cleared (setup shortcut: the Landing's Waystone, " +
                $"Yooba met, diamonds, one of Yooba's goods bought), then level {WaystoneLevel} (Floor 999) loaded directly and played by the bot, its Grudge " +
                "picked with 1, the results, the Shaft's climb and Continue to the next stage, where the bot plays to lose (walks into the shots, no " +
                "dodging, firing or rewinding) until Hell kills it: the death screen (\"YOU OBVIOUSLY DIED\", the announcer gloating with a voice), then " +
                "the Shaft drops the hero's marker from the floor he died on back to the Waystone at FLOOR 1,000 with the Waystones marked on the map, " +
                "the first death's voiced line (\"Hell doesn't let you die. It lets you do it again.\") and the hero's answer, not skippable; the Grudges " +
                "are gone, the diamonds and Yooba's goods kept; Continue restarts the run at Level 2 (The Landing); then the main menu offers Continue " +
                "(the current run) and New Run (from the Waystone), and New Run starts Level 2 again. Each beat is a check event", WaystoneSeconds, "full"));
            list.Add(new PlaytestScenario("menus", "Tap to play, then level select, videos, Yooba's shop and settings (Sound off/on, Music -/+) each open and close, " +
                "then the Shaft (a preview over the menu, setup shortcut, Yooba met) opens, its layout is checked, Visit Yooba opens his shop over it, " +
                "Back returns to the Shaft and Continue closes it", 150, "menus"));
            list.Add(new PlaytestScenario("layout", "Phone layout check (run with device \"phone\", 390x844): on the main menu (in the editor and standalone also resized through 16:9, 1280x720, 1024x768, a tall narrow window, phone portrait and phone landscape; a WebGL page is never resized by the game, the runner's device sets its size: tagline whole, stickers clear, buttons 48 px), level select, settings, " +
                "level 1's HUD, the pause menu and its settings, the Shaft (a preview over the menu, setup shortcut, with Visit Yooba beside Continue), Yooba's shop over it " +
                "(also resized to 1280x720 and 390x844: pip strips clear of each card's name, last row fully visible after a scroll to the bottom, no Decoration/Subtitle overlaps), " +
                "and the death scene (loaded directly, a setup shortcut), no button overlaps another or a HUD panel, none leaves the screen, and every button is thumb-sized", 180, "menus"));
            list.Add(new PlaytestScenario("portal-audio", "The prologue's portal scene, by ear, on the main menu: every Overlord line, hero take, " +
                "Hell's echo (three repeats, each deeper) and effect plays in scene order, then the Meadow's lines; any without a clip is a bug", 150, "menus", "audio"));
            list.Add(new PlaytestScenario("sound-audit", "The sound audit on the main menu: every sound in audio.json has a clip (a missing one is a bug, " +
                "listed by id), every enemy's hurt, death and attack sound, every weapon's equip and empty sound, every enemy's hint line and Yooba's shop lines are heard once, " +
                "in the voice that speaks them; `sound` and `check` events, metrics soundAudit.*", 240, "menus", "audio"));
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
            // A start point (AGENTS.md "Playtests", "Start points"): the contract's optional "at", for the runs that take one.
            string at = RequestedAt(id, options);
            if (at != null)
                run.Event("at", $"start point asked for: \"{at}\"");

            if (TryStartRun(session, id, at, out var started))
                yield return started;
            else if (id == "smoke")
                yield return session.Smoke();
            else if (id == "full")
                yield return session.Full();
            else if (id == AllLevelsId)
                yield return session.AllLevels(AllGameLevels(), "every level in game.json", at: at);
            else if (id == OffPathId)
                yield return session.AllLevels(OffPathLevels(), "the levels after the path", checkEvery: true, at: at);
            else if (id == CrackedHeatId || id.StartsWith(CrackedHeatId + "-", StringComparison.Ordinal))
                yield return session.AllLevels(CrackedHeatLevels(id == CrackedHeatId ? 1 : int.Parse(id.Substring(CrackedHeatId.Length + 1))), "Circle IX, the heat pass", checkEvery: true, at: at, ladders: true);
            else if (id == RuneBridgesId || id.StartsWith(RuneBridgesId + "-", StringComparison.Ordinal))
                yield return session.AllLevels(RuneBridgeLevels(id == RuneBridgesId ? 1 : int.Parse(id.Substring(RuneBridgesId.Length + 1))), "Circle VIII, the Rune Bridges", checkEvery: true, at: at,
                    budget: ShortRunBudget, hardEndSeconds: ShortRunHardEnd);
            else if (id == LadderFloorsId)
                yield return session.AllLevels(LadderFloorLevels(), "the floors with a ladder", checkEvery: true, at: at, ladders: true);
            else if (id == ClimbStartId)
                yield return session.AllLevels(ClimbStartLevels(), $"Level 1 and Floors {ClimbStartTopFloor}-{ClimbStartLastFloor}", checkEvery: true, at: at);
            else if (id.StartsWith(StretchPrefix, StringComparison.Ordinal) && PathStretches().Exists(s => StretchId(s) == id))
            {
                var stretch = PathStretches().Find(s => StretchId(s) == id);
                yield return session.AllLevels(PathLevels().FindAll(n => n >= stretch.from && n <= stretch.to), $"floors {PathFloor(stretch.from)}-{PathFloor(stretch.to)}", checkEvery: true, at: at);
            }
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
            else if (id == "yooba-greetings")
                yield return session.YoobaGreetingsRun();
            else if (id == NetOutageId)
                yield return session.NetOutageRun();
            else if (id == IntroSkipId)
                yield return session.IntroSkipRun();
            else if (id == FloorCardId)
                yield return session.FloorCardRun();
            else if (id == FirstFiveId)
                yield return session.FirstFiveRun();
            else if (id == PhoneLoadId)
                yield return session.PhoneLoadRun();
            else if (id == Video3Id)
                yield return session.Video3Run();
            else if (id == BeatsId)
                yield return session.BeatsRun();
            else if (id == StoryId)
                yield return session.StoryRun();
            else if (id == "waystone")
                yield return session.WaystoneRun();
            else if (id == "wastes-climb")
                yield return session.ClimbFromStart(ClimbFrom, ClimbTo, WastesClimbSeconds, at);
            else if (id == "landing-stretch")
                yield return session.ClimbFromStart(StretchFrom, StretchTo, StretchSeconds, at);
            else if (id == "foreman-stretch")
                yield return session.ClimbFromStart(ForemanFrom, ForemanTo, ForemanSeconds, at);
            else if (id == "long-way")
                yield return session.LongWayStages();
            else if (id == "shortcut")
                yield return session.ShortcutRun();
            else if (id == "circle-x-route")
                yield return session.CircleXRoute();
            else if (id == "cracked-road-route")
                yield return session.CrackedRoadRoute();
            else if (id == "menus")
                yield return session.Menus();
            else if (id == "site")
                yield return session.Site();
            else if (id == "layout")
                yield return session.Layout();
            else if (id == "imp-death")
                yield return session.ImpDeath();
            else if (id == "analytics")
                yield return session.AnalyticsRun();
            else if (id == AimGuideScenario)
                yield return session.AimGuideRun();
            else if (id == WeaponsId)
                yield return session.WeaponsRun();
            else if (id == WeaponDropsId)
                yield return session.WeaponDropsRun();
            else if (id == DeathReturnId)
                yield return session.DeathReturnRun();
            else if (id == DeathRestartId)
                yield return session.DeathRestartRun();
            else if (id == AgainPhoneId)
                yield return session.AgainPhoneRun();
            else if (id == RewindJuiceId)
                yield return session.RewindJuiceRun();
            else if (id == FirstRewindId)
                yield return session.FirstRewindRun();
            else if (id == OverlordChaseId)
                yield return session.OverlordChaseRun();
            else if (id == RewindFxId)
                yield return session.RewindFxRun();
            else if (id == SpawnEdgeId)
                yield return session.SpawnEdgeRun();
            else if (id == IncomingId)
                yield return session.IncomingRun();
            else if (id == FeelId)
                yield return session.FeelRun();
            else if (id == AudioId)
                yield return session.AudioRun();
            else if (id == BeatDirectorId)
                yield return session.BeatDirectorRun();
            else if (id == TapJumpId)
                yield return session.TapJumpRun();
            else if (id == TouchControlsId)
                yield return session.TouchControlsRun();
            else if (id == MenuTouchLayoutId)
                yield return session.MenuTouchLayoutRun();
            else if (id == SideFloorsId || id == GauntletsId)
                yield return session.SideFloorsRun(id == GauntletsId, at);
            else if (id == ReplayModesId)
                yield return session.ReplayModesRun();
            else if (id == BystanderId)
                yield return session.BystanderRun();
            else if (id == OverlordCameosId)
                yield return session.OverlordCameosRun();
            else if (id == CastId)
                yield return session.CastRun();
            else if (id == StopFloorsId)
                yield return session.StopFloorsRun();
            else if (id == FloorsId)
                yield return session.FloorsRun();
            else if (id == CompanionsId)
                yield return session.CompanionsRun(at);
            else if (id == BystanderFloorsId)
                yield return session.BystanderFloorsRun();
            else if (id == LadderId)
            {
                session.Prepare(LadderLevel, true);
                yield return session.LadderRun();
            }
            else if (id == FloorKitId)
            {
                session.Prepare(FloorKitLevel, true);
                yield return session.FloorKitRun();
            }
            else if (id == HeroMovesId || id == DashId || id == WallJumpId)
            {
                session.Prepare(MovesLevel, true);
                yield return session.HeroMovesRun(id != WallJumpId, id != DashId);
            }
            else if (id == MusicLayersId)
                yield return session.MusicLayersRun();
            else if (id == ScoreFloorsId)
                yield return session.ScoreFloorsRun();
            else if (id == AdaptiveMusicId)
                yield return session.AdaptiveMusicRun();
            else if (id == UnarmedHeroId)
                yield return session.UnarmedHeroRun();
            else if (id == WeaponLooksId)
                yield return session.WeaponLooksRun();
            else if (id == HeroClipsId)
                yield return session.HeroClipsRun();
            else if (id == HeroAnimId)
                yield return session.HeroAnimRun();
            else if (id == StyleId)
                yield return session.StyleRun();
            else if (id == EnemyHintsId)
                yield return session.EnemyHintsRun();
            else if (id == DifficultyId)
                yield return session.DifficultyRun();
            else if (id == ReadabilityId)
                yield return session.ReadabilityRun();
            else if (id == BulletLookId)
                yield return session.BulletLookRun();
            else if (id == LightingId)
                yield return session.LightingRun();
            else if (id == AmbienceId)
                yield return session.AmbienceRun();
            else if (id == BossFightsId)
                yield return session.BossFightsRun();
            else if (id == CodexId)
                yield return session.CodexRun();
            else if (id == AchievementsId)
                yield return session.AchievementsRun();
            else if (id == UiPolishId)
                yield return session.UiPolishRun();
            else if (id == HudInkId)
                yield return session.HudInkRun();
            else if (id == "restart")
                yield return session.RestartRun();
            else if (id == RewindPerfId)
                yield return session.RewindPerfRun();
            else if (id == RewindPerfSlowId)
                yield return session.RewindPerfRun(cpuFactor: SlowCpuFactor);
            else if (id == DailyId)
            {
                yield return session.RewindPerfRun(finish: false);
                if (!session.Run.Done)
                    yield return session.AllLevels(DailyLevels(), $"the daily sample ({PathRange(DailyLevels())})", checkEvery: true);
            }
            else if (id == "portal-audio")
                yield return session.PortalAudio();
            else if (id == "sound-audit")
                yield return session.SoundAudit();
            else if (id == "cutscene")
                yield return session.Cutscene();
            else if (id == "shop-gate")
                yield return session.ShopGate();
            else if (id == ShopGoodsId)
                yield return session.ShopGoodsRun();
            else if (id == ShopBalanceId)
                yield return session.ShopBalance();
            else if (id == ShopWeaponsId)
                yield return session.ShopWeapons();
            else if (id == StagedMenuId)
                yield return session.StagedMenu();
            else if (id == YoobaAliveId)
                yield return session.YoobaAlive();
            else if (id == "climb")
            {
                session.Prepare(ClimbLevel, true);
                yield return session.ClimbRun();
            }
            else if (id == "one-way")
            {
                session.Prepare(OneWayLevel, true);
                yield return session.OneWayRun();
            }
            else if (id == "mechanics")
            {
                session.Prepare(MechanicsLevel, true);
                yield return session.MechanicsRun();
            }
            else if (id == BestiaryId)
            {
                session.Prepare(BestiaryLevel, true);
                yield return session.BestiaryRun();
            }
            else if (id == RoadBestiaryId)
            {
                session.Prepare(BestiaryLevel, true);
                yield return session.RoadBestiaryRun();
            }
            else if (id == ElitesId)
            {
                session.Prepare(BestiaryLevel, true);
                yield return session.ElitesRun();
            }
            else if (id == MagmaKing.Scenario)
                yield return session.WardenRun(MagmaKing, false);
            else if (id == SpireFiend.Scenario)
                yield return session.WardenRun(SpireFiend, true);
            else if (id == BridgeDescentId)
                yield return session.AllLevels(BridgeDescentLevels(), "Circle VIII's lower half", checkEvery: true, at: at ?? BridgeDescentDefaultAt, thenLevel: LevelNumberOf(BridgeWraithLevel),
                    budget: ShortRunBudget * 0.5f, hardEndSeconds: ShortRunHardEnd * 0.6f);
            else if (id == BridgeWraith.Scenario)
            {
                int wraithLevel = LevelNumberOf(BridgeWraithLevel);
                session.Prepare(wraithLevel, true);
                yield return session.LevelScenario(wraithLevel);
            }
            else if (id == "video-" + OverlayVideo)
                yield return session.VideoOverlay();
            else if (id == "video-" + OverlayVideo + "-tunnel")
                yield return session.VideoTunnel();
            else if (id.StartsWith("level-", StringComparison.Ordinal) && int.TryParse(id.Substring(6), out int n))
            {
                // Its own floor, with the save it expects (the start-at save): level-3 starts on Floor 999 as a player's run would.
                if (n >= 1 && n <= GameDefinition.Instance.TotalLevels)
                    session.Prepare(n, true);
                yield return session.LevelScenario(n);
            }
            else
            {
                run.Problem(PlaytestSeverity.Bug, "unknown scenario " + id);
                run.Finish(PlaytestOutcome.Error);
            }
        }

        /// <summary>One run: the virtual devices, the bot, and everything it watches (logs, telemetry, fps).</summary>
        sealed partial class Session
        {
            public readonly PlaytestRun Run;
            readonly PlaytestOptions options;
            readonly PlaytestInput input;
            readonly FpsSampler fps;
            readonly int diamondsAtStart;
            int diamondsAtLevelStart;
            string lastGrudgeOffer;
            PlaytestBot bot;
            string lastOutcome;
            int deaths;
            int botRewinds;
            int impDeaths;
            MagmaWatch magma;
            bool impSlowMotion;
            float slowMotionUntil;
            bool rewindPerfSummaryLogged;

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
                RewindPerf.Reset();
                Application.logMessageReceived += OnLog;
                GameplayTelemetry.Recorded += OnTelemetry;
                GameplayTelemetry.Finished += OnLevelLog;
                ShaftScreen.Reported += OnShaft;
                YoobaPortrait.Reported += OnYooba;
                RewindPerf.Reported += OnRewindPerf;
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
                Run.Metric("shop.totalPrice", RogueliteManager.TotalPrice(true));
                Run.Metric("shop.basePrice", RogueliteManager.TotalPrice(false));
                ReportRewindPerf();
            }

            void ReportRewindPerf()
            {
                if (RewindPerf.Rewinds <= 0)
                    return;
                var forward = RewindPerf.Forward;
                var rewinding = RewindPerf.Rewinding;
                var after = RewindPerf.AfterRewind;
                Run.Metric("rewindPerf.rewinds", RewindPerf.Rewinds);
                Run.Metric("rewindPerf.forward.avgMs", forward.AverageMs);
                Run.Metric("rewindPerf.forward.worstMs", forward.WorstMs);
                Run.Metric("rewindPerf.forward.workMs", forward.AverageWorkMs);
                Run.Metric("rewindPerf.rewind.avgMs", rewinding.AverageMs);
                Run.Metric("rewindPerf.rewind.worstMs", rewinding.WorstMs);
                Run.Metric("rewindPerf.rewind.workMs", rewinding.AverageWorkMs);
                Run.Metric("rewindPerf.rewind.worstWorkMs", rewinding.WorstWorkMs);
                Run.Metric("rewindPerf.after.avgMs", after.AverageMs);
                Run.Metric("rewindPerf.after.worstMs", after.WorstMs);
                Run.Metric("rewindPerf.after.worstStep", RewindPerf.WorstStepAfterRewind);
                Run.Metric("rewindPerf.rewindables", RewindPerf.Rewindables);
                Run.Metric("rewindPerf.activeRewindables", RewindPerf.ActiveRewindables);
                Run.Metric("rewindPerf.longestHistory", RewindPerf.LongestHistory);
                if (!rewindPerfSummaryLogged)
                {
                    rewindPerfSummaryLogged = true;
                    Run.Event("rewind-perf", $"playing: {forward}");
                    Run.Event("rewind-perf", $"rewinding: {rewinding}");
                    Run.Event("rewind-perf", $"just after a rewind: {after}, longest clock step {RewindPerf.WorstStepAfterRewind * 1000f:0.0} ms");
                    Run.Event("rewind-perf", $"history window {GameDefinition.Instance.rewindMaxHistorySeconds:0} s every {GameDefinition.Instance.rewindRecordInterval:0.00} s, juice start {GameDefinition.Instance.rewindBudget.startSeconds:0} s");
                    Run.Event("rewind-perf", $"{RewindPerf.Rewindables} rewindables ({RewindPerf.ActiveRewindables} on) at the last rewind, longest history " +
                                             $"{RewindPerf.LongestHistory} snapshots");
                }
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
                TimelineOverlayPlayer.VisibilityChanged -= OnOverlayVisibility;
                GameplayTelemetry.Recorded -= OnTelemetry;
                GameplayTelemetry.Finished -= OnLevelLog;
                ShaftScreen.Reported -= OnShaft;
                YoobaPortrait.Reported -= OnYooba;
                RewindPerf.Reported -= OnRewindPerf;
                StopRewindThrottle();
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

            // Ladder grabs per level and the levels where a ladder was climbed off at its top (the ladder-floors checks).
            readonly Dictionary<int, int> ladderKit = new Dictionary<int, int>();
            readonly HashSet<int> ladderTops = new HashSet<int>();

            void OnTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (Run.Done)
                    return;
                if (e.Kind == "damage" && e.Subject == "player" && !string.IsNullOrEmpty(e.Detail))
                    lastHitBy = e.Detail;
                if (e.Kind == "levelStart" && RogueliteManager.Instance != null)
                    diamondsAtLevelStart = RogueliteManager.Instance.GetPoints();
                if (e.Kind == "upgradeOffer")
                    lastGrudgeOffer = e.Subject ?? "";
                string level = "level " + log.World.LevelNumber;
                BalanceOnTelemetry(e, log.World.LevelNumber);
                if (e.Kind == "floorkit" && e.Subject != null && e.Subject.StartsWith("ladder", StringComparison.Ordinal))
                {
                    ladderKit.TryGetValue(log.World.LevelNumber, out var had);
                    ladderKit[log.World.LevelNumber] = had + (e.Subject.StartsWith("ladderGrab", StringComparison.Ordinal) ? 1 : 0);
                    if (e.Subject == "ladderTop")
                        ladderTops.Add(log.World.LevelNumber);
                    Run.Event("ladder", $"{level}: {e.Subject} at {e.Time:0.0} s ({e.Detail})");
                }
                if (stage != null && stage.OneFloor && stage.Level == log.World.LevelNumber)
                {
                    bool scald = e.Kind == "damage" && e.Subject == "player" && e.Detail != null && e.Detail.StartsWith("lava", StringComparison.Ordinal);
                    if (scald || e.Kind == "pit")
                    {
                        // Floor 1000 on beta: the camera had climbed past a sinking stone, PitCatch burned him under it and its
                        // throw carried him past the slope's footholds (start > stone1 > bank). Log each burn with where it was.
                        var w = log.World;
                        Vector2 p = w.Player != null ? w.Player.Position : Vector2.zero;
                        string last = stage.StoodOn.Count > 0 ? stage.StoodOn[stage.StoodOn.Count - 1] : "nothing";
                        string what = scald ? $"scalded by {e.Detail}" : "fell out of the bottom of the view (PitCatch)";
                        string burn = $"{what} at {e.Time:0.0} s at ({p.x:0.0}, {p.y:0.0})" +
                                      (w.Effects != null && w.Effects.HasPath ? $", path section \"{w.Effects.CurrentSection?.name}\"" : "") +
                                      $", last foothold {last}";
                        stage.Burns.Add(burn);
                        if (!scald)
                            stage.PitBurns++;
                        Run.Event("burn", $"{level}: {burn}");
                    }
                    if (e.Kind == "floorkit")
                    {
                        string key = e.Subject.Split(':')[0] + "@" + e.Detail;
                        stage.Kit.TryGetValue(key, out int had);
                        stage.Kit[key] = had + 1;
                        if (e.Subject.StartsWith("geyser:", StringComparison.Ordinal) || e.Subject.StartsWith("swingGrab:", StringComparison.Ordinal))
                            Run.Event("floorkit", $"{level}: {e.Subject} ({e.Detail})");
                        // Rune spans (Circle VIII): the first hum, lit and out of each kind of span on the floor.
                        if (had == 0 && e.Subject.StartsWith("rune", StringComparison.Ordinal))
                            Run.Event("rune", $"{level} at {e.Time:0.0} s: {key}");
                    }
                    else if (e.Kind == "spawn")
                    {
                        stage.Spawned.TryGetValue(e.Subject, out int had);
                        stage.Spawned[e.Subject] = had + 1;
                        if (!stage.SpawnTimes.TryGetValue(e.Subject, out var times))
                            stage.SpawnTimes[e.Subject] = times = new List<float>();
                        times.Add(e.Time);
                        if (e.Subject == SpillBossId && had == 0)
                            Run.Event("boss", $"{level} at {e.Time:0.0} s: {SpillBossId} looms in the background (the ember rain's source)");
                        else if (e.Subject == "fireball" && stage.Spawned.TryGetValue(SpillBossId, out _) && !stage.SpillRainSeen)
                        {
                            stage.SpillRainSeen = true;
                            Run.Event("rain", $"{level} at {e.Time:0.0} s: the first fireball falls with {SpillBossId} on screen");
                        }
                        foreach (var t in FloorBossTelegraphs)
                            if (e.Subject == t.attack)
                                Run.Event("attack", $"{level} at {e.Time:0.0} s: {t.what} ({t.attack})");
                    }
                    else if (e.Kind == "signal")
                    {
                        if (!stage.Signals.ContainsKey(e.Subject))
                            stage.Signals[e.Subject] = e.Time;
                        if (stage.Long && e.Subject.StartsWith("setpiece:", StringComparison.Ordinal))
                        {
                            if (!stage.Pieces.Contains(e.Subject))
                                Run.Event("setpiece", $"{level} at {e.Time:0.0} s: set piece {stage.Pieces.Count + 1}, {e.Subject.Substring(9)}");
                            stage.PieceBegan(e.Subject, e.Time);
                        }
                        if (Array.Exists(FloorBossSignals, x => x.signal == e.Subject) || e.Subject == "loopRepeat" || e.Subject == "liftCut" || e.Subject == "sleeperFell")
                            Run.Event("signal", $"{level} at {e.Time:0.0} s: {e.Subject}");
                    }
                }
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
                    case "death" when magma != null && e.Subject == magma.W.Id:
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
                    case "bounce" when mech != null:
                    case "shieldBlock" when mech != null:
                    case "laser" when mech != null:
                    case "teleport" when mech != null:
                    case "damage" when mech != null:
                    case "signal" when mech != null:
                        MechanicsTelemetry(log, e);
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
                // The economy (Docs/Story.md "Yooba's prices"): diamonds this level paid, and what the whole shop costs.
                int levelDiamonds = RogueliteManager.Instance != null ? Mathf.Max(0, RogueliteManager.Instance.GetPoints() - diamondsAtLevelStart) : 0;
                Run.Event("level-end", $"level {s.levelNumber}: {s.outcome} after {s.duration:0.0} s, {s.kills}/{s.spawned} kills, " +
                                       $"hp {s.playerHealthEnd}/{s.playerMaxHealth}, phase {s.lastPhase}, diamonds {levelDiamonds} " +
                                       $"(shop: {RogueliteManager.TotalPrice(true)} to max everything)");
                // A level left before it was played (all-levels: the Waystone's restart opens Level 2 after every death, and
                // the bot loads its next level over it at once) is not a try: it overwrote level2.seconds with 0 and added a
                // level2.attempt per death elsewhere (beta run r_b7a410dd5b39: 15 attempts, 0 s).
                if (s.outcome == "playtest left" && s.duration < 1f)
                    return;
                BalanceOnLevelEnd(s, levelDiamonds);
                Run.Add("kills", s.kills);
                Run.Add("spawned", s.spawned);
                Run.Add("playerHits", s.playerHits);
                Run.Add("rewinds", s.rewinds);
                Run.Add("rewoundSeconds", s.rewoundSeconds);
                Run.Metric(prefix + ".seconds", s.duration);
                Run.Metric(prefix + ".diamonds", levelDiamonds);
                Run.Metric("shop.totalPrice", RogueliteManager.TotalPrice(true));
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

            public IEnumerator LevelScenario(int n, bool alreadyOpen = false)
            {
                var definition = GameDefinition.Instance;
                if (n < 1 || n > definition.TotalLevels)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"there is no level {n}");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                LastLevelOutcome = null;
                if (!alreadyOpen)
                {
                    Run.Event("shortcut", $"load level {n} directly");
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                }
                // Level 1 ends in the portal scene: watch it beat by beat, then follow the game on into the Shaft plunge (Video2 skipped on a first run).
                portal = n == PortalLevel ? new PortalWatch() : null;
                if (portal != null)
                    CheckPortalData(definition.GetLevel(n));
                // A floor stage: its title, lines, new enemy, breathers, then the Shaft's climb after it, each a check event.
                stageChecksFailed = 0;
                stage = portal == null && IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                // A Warden's arena also gets the magma-king watch: his animations, size, Die clip and lines.
                magma = stage != null && stage.Arena ? new MagmaWatch(stage.Warden) : null;
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
                /// <summary>A boss fight: the most minions (hostile enemies but the boss) alive at once, and every change of that count with its game time.</summary>
                public int PeakMinions, LastMinions = -1;
                public readonly List<(float at, int count)> MinionSamples = new List<(float, int)>();
                public bool NewAlone = true, SignSeen, DoorSeen, Picked;
                /// <summary>Hell's shortcuts (Docs/Story.md, "Shortcuts"): the cost the choice showed, and where a taken one let him out (0 = not taken).</summary>
                public string ShortcutOffer = "", ShortcutDeclined = "";
                public int ShortcutTo, ShortcutFrom;
                /// <summary>The phase each scripted line belongs to, and the phases entered: a branch not taken (a shortcut's) has lines nobody hears.</summary>
                public readonly Dictionary<string, string> PhaseOf = new Dictionary<string, string>();
                public readonly HashSet<string> Entered = new HashSet<string>();
                public float FirstHostileAt = -1f, QuietSince = -1f, LongestQuiet, Clock, DoorSeenAt = -1f;

                // ─── A long bullet-hell floor (script length >= LongFloorLength): its set pieces and how dense each one got ───
                /// <summary>A long floor: 240-360 s, many enemies and shots, judged on its set pieces and a rising density.</summary>
                public bool Long;
                /// <summary>A floor that declares its own length (`script.length` 100-199: Floor 998 and the Circle X long floors, now ~150-195 s with the path as the clock): judged like a long floor on pace, enemies and breathers, but within its own length.</summary>
                public bool Mid;
                public bool SpillRainSeen;
                public float Declared;
                /// <summary>The set pieces in the order they began (script signals "setpiece:<name>"), when, and per piece: seconds
                /// spent, hostile-seconds (enemies and shots alive, summed over time) and the most alive at once.</summary>
                public readonly List<string> Pieces = new List<string>();
                public readonly Dictionary<string, float> PieceAt = new Dictionary<string, float>(), PieceSeconds = new Dictionary<string, float>(), PieceLoad = new Dictionary<string, float>();
                public readonly Dictionary<string, int> PiecePeak = new Dictionary<string, int>();
                public float LastSampleAt = -1f;
                public int MostHostile;

                public void PieceBegan(string name, float at)
                {
                    if (Pieces.Contains(name))
                        return;
                    Pieces.Add(name);
                    PieceAt[name] = at;
                    PieceSeconds[name] = 0f;
                    PieceLoad[name] = 0f;
                    PiecePeak[name] = 0;
                }

                /// <summary>Density of piece i: the average number of enemies and shots alive while it ran.</summary>
                public float Density(int i) => PieceSeconds[Pieces[i]] > 0.5f ? PieceLoad[Pieces[i]] / PieceSeconds[Pieces[i]] : 0f;

                // ─── A Warden's arena (IsArena): the boss, his fight's signals and his telegraphs ───
                public readonly bool Arena;
                /// <summary>Whose fight the arena is (Wardens, by the level's file).</summary>
                public readonly WardenFight Warden;
                /// <summary>When each of the fight's signals was first seen, and the boss's state machine as it went (game seconds).</summary>
                public readonly Dictionary<string, float> Signals = new Dictionary<string, float>();
                public readonly List<(string state, float at, float health)> BossStates = new List<(string, float, float)>();
                /// <summary>Last time each warning glow spawned; per attack: how many, the shortest lead, how many came with no warning.</summary>
                public readonly Dictionary<string, float> WarnedAt = new Dictionary<string, float>();
                public readonly Dictionary<string, (int count, float minLead, int unwarned)> Attacks = new Dictionary<string, (int, float, int)>();
                public readonly List<float> GuardSpawns = new List<float>();
                public string BossBar = "", LastBossState = "";
                public float BossBarAt = -1f, SlowMotionAt = -1f, BossFirstAt = -1f;

                // ─── One level = one floor (Docs/LevelDesign.md): what the floor's own pieces did ───
                /// <summary>A floor level: floors.end is the floor above floors.start.</summary>
                public bool OneFloor => Start > 0 && (Start - End == 1 || Start == End);
                /// <summary>Floor-kit events ("sink@sinkingStone", "swingGrab@swingChain", "geyser@lavaGeyser") and spawns by entity id.</summary>
                public readonly Dictionary<string, int> Kit = new Dictionary<string, int>(), Spawned = new Dictionary<string, int>();
                /// <summary>When each entity id spawned (game seconds): a floor boss's telegraphs are measured glow to attack.</summary>
                public readonly Dictionary<string, List<float>> SpawnTimes = new Dictionary<string, List<float>>();
                /// <summary>A floor boss (every 10th floor, the floors ending in 1): longer, and the boss checks.</summary>
                public bool FloorBoss => OneFloor && Start % 10 == 1;
                /// <summary>The lava sea's surface (lowest and highest seen) and when the wave first chased him.</summary>
                public float LavaLow = float.MaxValue, LavaHigh = float.MinValue;
                // The HUD bar's path fraction on the last frame before the director completed (-1: no path to measure).
                public float PathProgressLast = -1f;
                /// <summary>The footholds he stood on, in order (instance id, else entity id), and the ones a still lava (not risen,
                /// the foothold not sinking) covered: their top inside the lava's box, under its drawn surface and its scald.</summary>
                public readonly List<string> StoodOn = new List<string>();
                /// <summary>"foothold" events sent (capped at MaxFootholdEvents: a loop between two stones sent one every second).</summary>
                public int FootholdEvents;
                public const int MaxFootholdEvents = 60;
                public readonly Dictionary<string, string> UnderLava = new Dictionary<string, string>();
                /// <summary>Every burn on the floor (a lava's scald, or PitCatch after he dropped out of the bottom of the view): what,
                /// when, where, and the foothold he last stood on. A burn's throw carries him past footholds.</summary>
                public readonly List<string> Burns = new List<string>();
                public int PitBurns;

                /// <summary>
                /// StoodOn for a message, short: a stretch that repeats (stone2 > stone3 > stone2 > stone3 ...) as "(stone2 > stone3) x40",
                /// and past 40 entries the first and last 20 with how many were left out between.
                /// </summary>
                public string Footholds()
                {
                    var parts = new List<string>();
                    int i = 0;
                    while (i < StoodOn.Count)
                    {
                        int bestLength = 0, bestTimes = 1;
                        for (int length = 1; length <= 4 && i + length * 2 <= StoodOn.Count; length++)
                        {
                            int times = 1;
                            while (i + length * (times + 1) <= StoodOn.Count && SameRun(i, i + length * times, length))
                                times++;
                            if (times >= 2 && times * length > bestTimes * Math.Max(1, bestLength))
                            {
                                bestLength = length;
                                bestTimes = times;
                            }
                        }
                        if (bestLength == 0)
                        {
                            parts.Add(StoodOn[i]);
                            i++;
                            continue;
                        }
                        string unit = string.Join(" > ", StoodOn.GetRange(i, bestLength));
                        parts.Add((bestLength > 1 ? $"({unit})" : unit) + $" x{bestTimes}");
                        i += bestLength * bestTimes;
                    }
                    if (parts.Count > 40)
                    {
                        var ends = parts.GetRange(0, 20);
                        ends.Add($"... {parts.Count - 40} more ...");
                        ends.AddRange(parts.GetRange(parts.Count - 20, 20));
                        parts = ends;
                    }
                    return string.Join(" > ", parts);
                }

                /// <summary>Burns for a message: the first 10, then how many more.</summary>
                public string BurnList() =>
                    Burns.Count <= 10 ? string.Join(" | ", Burns) : string.Join(" | ", Burns.GetRange(0, 10)) + $" | ... {Burns.Count - 10} more";

                bool SameRun(int a, int b, int length)
                {
                    for (int k = 0; k < length; k++)
                        if (StoodOn[a + k] != StoodOn[b + k])
                            return false;
                    return true;
                }

                public int KitCount(string what, string entityPrefix = "")
                {
                    int n = 0;
                    foreach (var pair in Kit)
                    {
                        int at = pair.Key.IndexOf('@');
                        if (at > 0 && pair.Key.Substring(0, at) == what && pair.Key.Substring(at + 1).StartsWith(entityPrefix, StringComparison.Ordinal))
                            n += pair.Value;
                    }
                    return n;
                }

                public int SpawnCount(string entityPrefix)
                {
                    int n = 0;
                    foreach (var pair in Spawned)
                        if (pair.Key.StartsWith(entityPrefix, StringComparison.Ordinal))
                            n += pair.Value;
                    return n;
                }

                public StageWatch(int level, Action<string, bool, string> check)
                {
                    Level = level;
                    Check = check;
                    Arena = IsArena(level);
                    Warden = Arena ? WardenOf(level) ?? MagmaKing : null;
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
                    Long = script.length >= LongFloorLength;
                    Declared = script.length;
                    Mid = !Long && script.length >= MidFloorLength;
                    Sign = script.floors.sign ?? "";
                    Door = script.floors.door ?? "";
                    if (script.floors.repeat != null)
                        Repeat.AddRange(script.floors.repeat);
                    // A floor level's new enemy is the one the Circle introduces on that floor (the floor above is the next level's).
                    int lastFloor = Start - End == 1 ? Start : End;
                    if (content.Circles.TryGetValue(script.floors.circle ?? "", out var circle))
                        foreach (var e in circle.enemies)
                            if (e.fromFloor <= Start && e.fromFloor >= lastFloor && !NewTypes.Contains(e.entity))
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
                // A side floor (secret floor, gauntlet) has no stage checks: the secret-floors scenario judges it.
                return start > 0 && end > 0 && !IsSideFloor(n);
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
                    {
                        s.Banners.Add((banner, SaidAs(s, "banner", banner)));
                        // The 999 milestone is Floor 999's own title now (one level = one floor), not a Circle milestone passed.
                        if (story != null && world.Stage != null && world.Stage.Floor == LandingMilestone
                            && banner.IndexOf($"ONLY {LandingMilestone} TO GO", StringComparison.Ordinal) >= 0)
                        {
                            Run.Event("milestone", $"level {s.Level}: {banner}");
                            story.Milestones.Add((s.Level, LandingMilestone));
                        }
                    }
                    if (line.Length > 0 && line != s.LastLine)
                        s.Lines.Add((line, SaidAs(s, "line", line)));
                    s.LastBanner = banner;
                    s.LastLine = line;
                }

                // Enemies (hostile, with health) and anything hostile at all (shots, slag) for the breathers.
                int enemies = 0, hostile = 0, minions = 0;
                Entity fresh = null;
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || !world.Player.IsHostileTo(e))
                        continue;
                    hostile++;
                    if (e.MaxHealth <= 0)
                        continue;
                    enemies++;
                    if (!e.Tags.Contains("boss"))
                        minions++;
                    // Its variants count too (a sack imp on a swing is tagged impSack).
                    if (s.NewSerial < 0 && s.NewTypes.Exists(t => t == e.DefId || e.Tags.Contains(t)))
                        fresh = e;
                }
                s.MaxEnemies = Mathf.Max(s.MaxEnemies, enemies);
                if ((s.FloorBoss || s.Arena) && minions != s.LastMinions)
                {
                    s.LastMinions = minions;
                    s.MinionSamples.Add((now, minions));
                    s.PeakMinions = Mathf.Max(s.PeakMinions, minions);
                }
                if (s.Long)
                {
                    s.MostHostile = Mathf.Max(s.MostHostile, hostile);
                    if (s.Pieces.Count > 0 && s.LastSampleAt >= 0f && now > s.LastSampleAt)
                    {
                        string piece = s.Pieces[s.Pieces.Count - 1];
                        float dt = Mathf.Min(now - s.LastSampleAt, 0.5f);
                        s.PieceSeconds[piece] += dt;
                        s.PieceLoad[piece] += hostile * dt;
                        s.PiecePeak[piece] = Mathf.Max(s.PiecePeak[piece], hostile);
                    }
                    s.LastSampleAt = now;
                }
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
                if (s.FloorBoss && world.Hud != null && world.Hud.BossBarShown.Length > 0 && s.BossBar.Length == 0)
                {
                    s.BossBar = world.Hud.BossBarShown;
                    s.BossBarAt = now;
                    Run.Event("boss", $"level {s.Level} at {now:0.0} s: the boss bar shows \"{s.BossBar}\"");
                }
                if (s.FloorBoss && s.SlowMotionAt < 0f && s.Signals.ContainsKey("fmDefeat") && GameClock.Instance != null && GameClock.Instance.TimeScale < 0.9f)
                {
                    s.SlowMotionAt = now;
                    Run.Event("boss", $"level {s.Level} at {now:0.0} s: slow motion ({GameClock.Instance.TimeScale:0.00}x) as the Foreman falls");
                }
                if (s.OneFloor)
                {
                    var sea = world.FindById("lavaSea");
                    if (sea != null && sea.IsAlive && sea.MainCollider != null)
                    {
                        float top = sea.MainCollider.bounds.max.y;
                        s.LavaLow = Mathf.Min(s.LavaLow, top);
                        s.LavaHigh = Mathf.Max(s.LavaHigh, top);
                    }
                    FootholdsDuring(s, world, now);
                    if (world.Effects != null && world.Director != null && !world.Director.Completed)
                        s.PathProgressLast = world.Effects.PathProgress;
                }

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
            /// A floor level, every frame: which foothold he stands on (a "foothold" event each time it changes, so the timeline
            /// shows the way he climbed), and any foothold a lava that hasn't risen covers (a river laid over a stone hides the
            /// stone under its surface and burns him on it: Floor 1000's stone5, beta). A sinking or falling piece going under is
            /// the floor working, not a layout bug.
            /// </summary>
            void FootholdsDuring(StageWatch s, GameplayWorld world, float now)
            {
                Entity on = null;
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || e.IsPlayer || !e.Tags.Contains("foothold") || e.MainCollider == null || !e.MainCollider.enabled)
                        continue;
                    if (on == null && world.Player != null && world.Player.IsAlive && world.PlayerStandsOn(e))
                        on = e;
                    string name = e.InstanceId.Length > 0 ? e.InstanceId : e.DefId;
                    if (s.UnderLava.ContainsKey(name))
                        continue;
                    var sink = e.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>();
                    var crumble = e.FindAbility<SickOfIt.Gameplay.Content.CrumbleAbility>();
                    if (sink != null && sink.Sunk > 0.05f || crumble != null && crumble.Falling)
                        continue;
                    var b = e.MainCollider.bounds;
                    foreach (var lava in world.Live)
                    {
                        if (lava == null || !lava.IsAlive || !lava.Tags.Contains("lava") || lava.MainCollider == null)
                            continue;
                        var sea = lava.FindAbility<SickOfIt.Gameplay.Content.LavaSeaAbility>();
                        if (sea != null && sea.Risen > 0.01f)
                            continue;
                        var l = lava.MainCollider.bounds;
                        float overlap = Mathf.Min(b.max.x, l.max.x) - Mathf.Max(b.min.x, l.min.x);
                        if (overlap > 0.1f && b.max.y < l.max.y + 0.05f && b.max.y > l.min.y)
                        {
                            string lavaName = lava.InstanceId.Length > 0 ? lava.InstanceId : lava.DefId;
                            s.UnderLava[name] = $"{name} (top {b.max.y:0.00}, x {b.min.x:0.0}-{b.max.x:0.0}) under {lavaName} (top {l.max.y:0.00}, x {l.min.x:0.0}-{l.max.x:0.0})";
                            Run.Event("lava-over-foothold", $"level {s.Level} at {now:0.0} s: {s.UnderLava[name]}");
                            break;
                        }
                    }
                }
                if (on == null)
                    return;
                string stood = on.InstanceId.Length > 0 ? on.InstanceId : on.DefId;
                if (s.StoodOn.Count == 0 || s.StoodOn[s.StoodOn.Count - 1] != stood)
                {
                    s.StoodOn.Add(stood);
                    if (++s.FootholdEvents > StageWatch.MaxFootholdEvents)
                    {
                        if (s.FootholdEvents == StageWatch.MaxFootholdEvents + 1)
                            Run.Event("foothold", $"level {s.Level} at {now:0.0} s: {StageWatch.MaxFootholdEvents} footholds logged, the rest only in the stage's checks");
                        return;
                    }
                    var p = world.Player.Position;
                    Run.Event("foothold", $"level {s.Level} at {now:0.0} s: on {stood} at ({p.x:0.0}, {p.y:0.0})" +
                                          (world.Effects != null && world.Effects.HasPath ? $", path section \"{world.Effects.CurrentSection?.name}\"" : ""));
                }
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
                if (s.SlowMotionAt < 0f && s.Signals.ContainsKey(s.Warden.Defeat) && GameClock.Instance != null && GameClock.Instance.TimeScale < 0.9f)
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
                    if (Array.Exists(s.Warden.Signals, x => x.signal == e.Subject))
                        Run.Event("signal", $"level {s.Level} at {e.Time:0.0} s: {e.Subject}");
                    return;
                }
                if (e.Kind != "spawn")
                    return;
                if (e.Subject == s.Warden.Adds)
                {
                    s.GuardSpawns.Add(e.Time);
                    Run.Event("adds", $"level {s.Level} at {e.Time:0.0} s: a {s.Warden.AddsWhat} comes");
                    return;
                }
                foreach (var t in s.Warden.Telegraphs)
                {
                    if (Array.IndexOf(t.warning.Split(','), e.Subject) >= 0)
                    {
                        // One volley spawns a row of glows at once: the first one is the warning's time.
                        if (!s.WarnedAt.TryGetValue(t.warning, out float last) || e.Time - last > 0.05f || e.Time < last)
                        {
                            s.WarnedAt[t.warning] = e.Time;
                            Run.Event("telegraph", $"level {s.Level} at {e.Time:0.0} s: {t.what} warning ({e.Subject})");
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

                string wantTitle = s.OneFloor ? $"FLOOR {s.Start}: {s.Place.ToUpperInvariant()}" : $"FLOORS {s.Start}-{s.End}: {s.Place.ToUpperInvariant()}";
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

                if (s.NewTypes.Count == 0 || s.FloorBoss || s.Arena)
                    check(prefix + "newEnemy", true, s.NewTypes.Count == 0 ? "no new enemy type on this stage" : "a boss fight brings minions of mixed types (judged by boss.* checks)");
                else
                    check(prefix + "newEnemy", s.NewTypes.Count == 1 && s.NewSerial >= 0 && s.OthersAtNewSpawn == 0 && s.NewAlone,
                        s.NewTypes.Count > 1 ? $"{s.NewTypes.Count} new enemy types: {string.Join(", ", s.NewTypes)} (at most one per stage)"
                        : s.NewSerial < 0 ? $"the new enemy ({s.NewTypes[0]}) never appeared"
                        : $"the first {s.NewTypes[0]} arrived with {s.OthersAtNewSpawn} other enemies alive{(s.NewAlone ? " and stayed alone" : ", and another joined it")}");
                // A Warden's arena is one fight with the boss alive throughout: its pauses are the phase changes (FinishArena).
                if (s.Long || s.Mid)
                    check(prefix + "breather", true, $"a long bullet-hell floor is judged on its set pieces and rising density instead (longest quiet stretch {s.LongestQuiet:0.0} s)");
                else if (!s.Arena)
                    check(prefix + "breather", s.LongestQuiet >= 3f || s.OneFloor && s.FirstHostileAt < 0f,
                        s.FirstHostileAt < 0f ? "nothing attacked on this floor (its lava and stones are the floor)"
                            : $"longest stretch with nothing attacking after the first push: {s.LongestQuiet:0.0} s (want 3+)");
                // Docs/LevelDesign.md, "The pace": 3 on screen in Circle X, 4 in Circles IX-VIII.
                int allowed = s.Long || s.Mid ? LongFloorEnemies : s.FloorBoss || s.Arena ? BossFightEnemies : s.Start > 0 && s.Start <= 900 ? 4 : 3;
                check(prefix + "maxEnemies", s.MaxEnemies <= allowed, $"at most {s.MaxEnemies} enemies on screen at once ({(s.Long || s.Mid ? "a long bullet-hell floor" : s.FloorBoss || s.Arena ? "a boss fight: the boss and up to 8 minions" : allowed == 3 ? "Circle X" : "Circle IX")} allows {allowed})");
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
                        check(prefix + "length", s.Clock >= BossFightMin && s.Clock <= BossLevelMax, $"the arena cleared in {s.Clock:0} s of game time (a boss fight: the fight itself 120-240 s, the level with its intro and defeat {BossFightMin:0}-{BossLevelMax:0})");
                    else if (s.FloorBoss)
                        check(prefix + "length", s.Clock >= BossFightMin && s.Clock <= BossLevelMax, $"the floor boss cleared in {s.Clock:0} s of game time (the fight itself 120-240 s, the level with its intro and defeat {BossFightMin:0}-{BossLevelMax:0})");
                    else if (s.Long)
                        check(prefix + "length", s.Clock >= LongFloorMin && s.Clock <= LongFloorMax, $"a long bullet-hell floor cleared in {s.Clock:0} s of game time (target {LongFloorMin:0}-{LongFloorMax:0})");
                    else if (s.Mid)
                        check(prefix + "length", s.Clock >= 60f && s.Clock <= s.Declared * 1.1f, $"a floor that runs by its path cleared in {s.Clock:0} s of game time (its script says {s.Declared:0}; the camera is the clock, no timer holds the landing)");
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
                // Rune spans (Circle VIII): on a floor built of them every rune hummed, lit and went out at least once: the beat ran.
                int runesLit = s.KitCount("runeLit");
                if (runesLit + s.KitCount("runeHum") > 0)
                {
                    int hums = s.KitCount("runeHum"), outs = s.KitCount("runeOut");
                    check(prefix + "rune", hums > 0 && runesLit > 0 && outs > 0,
                        $"rune spans: {hums} hums, {runesLit} lit, {outs} went out (the beat needs all three)");
                }
                if (s.Arena)
                    FinishArena(s, completed);
                if (s.FloorBoss || s.Arena)
                    FinishBossFight(s, completed);
                if (s.Long)
                    FinishLongFloor(s, completed);
                if (s.OneFloor)
                    FinishFloorIdea(s, completed);
                if (s.Spawned.ContainsKey(SpillBossId) || s.Start == 998)
                {
                    bool boss = s.Spawned.TryGetValue(SpillBossId, out int bosses) && bosses > 0;
                    check(prefix + "background boss", boss, boss ? $"{SpillBossId} was on screen in the background" : $"{SpillBossId} never appeared");
                    check(prefix + "boss rain", boss && s.SpillRainSeen, s.SpillRainSeen ? "fireballs rained while it hung over the floor" : "no fireball fell while the boss was on screen");
                    if (completed)
                        check(prefix + "landing time", s.Clock <= SpillClearSeconds, $"the landing reached in {s.Clock:0} s of game time with no timer holding it (want {SpillClearSeconds:0} or less)");
                }
            }

            /// <summary>
            /// A long bullet-hell floor (Docs/LevelDesign.md, "A long bullet-hell floor"): at least LongFloorMinPieces set pieces, each
            /// announced by the script's `emit` of "setpiece:&lt;name&gt;", and the load (enemies and shots alive at once) rising from the
            /// first piece to the last. Every piece logs its density in a `density` event so the timeline shows the ramp.
            /// </summary>
            void FinishLongFloor(StageWatch s, bool completed)
            {
                string prefix = "level" + s.Level + ".long";
                var rows = new List<string>();
                for (int i = 0; i < s.Pieces.Count; i++)
                {
                    string piece = s.Pieces[i];
                    rows.Add($"{piece.Substring(9)} at {s.PieceAt[piece]:0} s: {s.Density(i):0.0} alive on average, {s.PiecePeak[piece]} at most");
                    Run.Event("density", $"level {s.Level} set piece {i + 1} ({piece.Substring(9)}): {s.Density(i):0.0} enemies and shots alive on average over {s.PieceSeconds[piece]:0} s, peak {s.PiecePeak[piece]}");
                }
                int want = completed ? LongFloorMinPieces : 1;
                s.Check(prefix + ".setPieces", s.Pieces.Count >= want,
                    $"{s.Pieces.Count} distinct set pieces (want {LongFloorMinPieces}+): {(rows.Count > 0 ? string.Join(" | ", rows) : "none")}");
                if (s.Pieces.Count >= 2)
                {
                    // Rising density: the last piece is busier than the first, and the ramp never falls by more than a third from one piece to the next.
                    int last = s.Pieces.Count - 1;
                    bool rising = s.Density(last) > s.Density(0) * 1.3f;
                    int drops = 0;
                    for (int i = 1; i <= last; i++)
                        if (s.Density(i) < s.Density(i - 1) * 0.67f)
                            drops++;
                    s.Check(prefix + ".density", !completed || rising && drops <= 1,
                        $"density {s.Density(0):0.0} on the first piece, {s.Density(last):0.0} on the last ({(rising ? "rising" : "NOT rising: want 1.3x")}, {drops} sharp drop(s), at most 1); " +
                        $"the most alive at once was {s.MostHostile}");
                    // Circle IX is hotter than Circle X (Docs/LevelDesign.md point 10): Circle X's long floors measure a little under this floor (the bot's average over a floor)
                    // (their estimate in long_floors.py: 1.4-5.4 on the first piece, 12.7-13.8 on the last); Circle IX's must average more and end hotter.
                    if (s.Start > 800 && s.Start <= 900)
                    {
                        float sum = 0f;
                        for (int i = 0; i <= last; i++)
                            sum += s.Density(i);
                        float average = sum / (last + 1);
                        Run.Event("heat", $"level {s.Level} (Floor {s.Start}): {average:0.0} alive on average over {last + 1} set pieces, {s.Density(last):0.0} on the last; Circle X's long floors average {CircleXAverageDensity:0.0} and end near {CircleXLastDensity:0.0}");
                        Run.Metric("level" + s.Level + ".heat", average);
                        s.Check(prefix + ".hotter", !completed || average >= CircleXAverageDensity && s.Density(last) >= CircleXLastDensity,
                            $"hotter than Circle X: {average:0.0} alive on average (want {CircleXAverageDensity:0.0}+) and {s.Density(last):0.0} on the last piece (want {CircleXLastDensity:0.0}+)");
                    }
                }
            }

            /// <summary>
            /// A floor's own new idea (Docs/LevelDesign.md, the floor's page), from what its pieces reported: the landing-stretch
            /// floors 1000-996. Floors without a page here only get the stage checks.
            /// </summary>
            void FinishFloorIdea(StageWatch s, bool completed)
            {
                string prefix = "level" + s.Level + ".idea";
                // The HUD's timeline is the path walked: full when the landing triggers (it snaps to full on completion).
                if (completed && s.PathProgressLast >= 0f)
                    s.Check("level" + s.Level + ".progressBar", s.PathProgressLast >= 0.97f,
                        $"the progress bar read {s.PathProgressLast:P0} on the frame before the landing completed (want 97% or more)");
                var kitSeen = new List<string>();
                foreach (var pair in s.Kit)
                    kitSeen.Add($"{pair.Key} x{pair.Value}");
                string kit = kitSeen.Count > 0 ? string.Join(", ", kitSeen) : "nothing";
                float rose = s.LavaHigh > s.LavaLow ? s.LavaHigh - s.LavaLow : 0f;
                switch (s.Start)
                {
                    case 1000:
                    {
                        int sinks = s.KitCount("sink", "sinkingStone");
                        s.Check(prefix, sinks >= 3 || !completed && sinks >= 1,
                            $"sinking stones took his weight {sinks} time(s) (want 3+: the floor teaches them); floor kit: {kit}");
                        // The jump off the slope's last stone onto the bank: the drift's lava river once lay over stone5, so he
                        // stood in the lava, drawn under it and burned, and never made the bank (beta's stuck event).
                        s.Check(prefix + ".lavaClear", s.UnderLava.Count == 0,
                            s.UnderLava.Count == 0 ? "no foothold under the lava (its sea and the drift's river lie below every stone, ledge and raft)"
                                : $"lava over {s.UnderLava.Count} foothold(s): {string.Join(" | ", s.UnderLava.Values)}");
                        int last = s.StoodOn.IndexOf("stone5");
                        int bank = last >= 0 ? s.StoodOn.IndexOf("bank", last) : -1;
                        s.Check(prefix + ".bankJump", bank > last && last >= 0,
                            last < 0 ? $"never stood on stone5 (the slope's last stone); footholds: {s.Footholds()}"
                                : bank < 0 ? $"stood on stone5 but never made the bank ledge; footholds: {s.Footholds()}"
                                : "jumped from stone5 onto the bank ledge");
                        // The climbs' camera keeps the stone he stands on in view as it sinks (lead 0.4): beta's run was burned out of
                        // the bottom of the view on the slope and thrown past stones 2-5, so it never stood on stone5.
                        s.Check(prefix + ".inView", s.PitBurns == 0,
                            s.PitBurns == 0 ? "never dropped out of the bottom of the view (every stone stayed on screen as it sank)"
                                : $"dropped out of the view {s.PitBurns} time(s): {s.BurnList()}");
                        bool landed = s.StoodOn.Contains("landing");
                        s.Check(prefix + ".cleared", completed && landed,
                            $"{(landed ? "reached the landing" : "never reached the landing")}, level {(completed ? "complete" : "not complete")} with no stuck event; " +
                            $"footholds: {s.Footholds()}; burns: {(s.Burns.Count == 0 ? "none" : s.BurnList())}");
                        break;
                    }
                    case 999:
                    {
                        bool beat = s.Banners.Exists(b => b.text.Contains("ONLY 999 TO GO") && b.voiced);
                        bool rising = s.Banners.Exists(b => b.text.StartsWith("THE LAVA'S RISING", StringComparison.Ordinal) && b.voiced);
                        int wave = s.SpawnCount("lavaSurge");
                        s.Check(prefix, beat && rising && rose >= 4f && (wave > 0 || !completed),
                            $"the 999 beat {(beat ? "voiced" : "NOT voiced")}; \"THE LAVA'S RISING\" {(rising ? "voiced" : "NOT voiced")}; the lava rose {rose:0.0} units " +
                            $"(want 4+); the wave {(wave > 0 ? "chased him" : "never came")}");
                        break;
                    }
                    case 998:
                    {
                        int wrecks = s.KitCount("sink", "wreck");
                        int cinders = s.SpawnCount("cinder");
                        s.Check(prefix, wrecks >= 3 && cinders >= 1,
                            $"the village's wreckage sank under him {wrecks} time(s) (want 3+), {cinders} cinder(s) came; floor kit: {kit}");
                        break;
                    }
                    case 997:
                    {
                        int imps = s.SpawnCount("impSwing");
                        int grabs = s.KitCount("swingGrab", "swingChain");
                        int spilled = s.SpawnCount("diamond");
                        s.Check(prefix, imps >= 3 && grabs >= 1,
                            $"{imps} sack imp(s) swung on the hauling chains (want 3+), he rode an empty chain {grabs} time(s) (want 1+), " +
                            $"{spilled} diamond(s) spilled; floor kit: {kit}");
                        break;
                    }
                    case 995:
                    {
                        // Hell's loop: the same stones sink by themselves in the same order, lap after lap.
                        var laps = new List<string>();
                        int repeated = 0;
                        for (int i = 1; i <= 5; i++)
                        {
                            int n = s.KitCount("loopSink", "loopStone" + i);
                            laps.Add($"stone {i} x{n}");
                            if (n >= 2) repeated++;
                        }
                        bool called = s.Signals.ContainsKey("loopRepeat");
                        bool purpose = s.Lines.Exists(l => l.text == "It's doing that on purpose." && l.voiced);
                        s.Check(prefix, repeated >= 4 && called && (purpose || !completed),
                            $"loop stones sank by themselves: {string.Join(", ", laps)} ({repeated} of 5 sank 2+ times, want 4+: the pattern repeats); " +
                            $"the second lap {(called ? "called out" : "NOT reached")}; \"It's doing that on purpose.\" {(purpose ? "voiced" : "NOT voiced")}; " +
                            $"he stood on stones {s.KitCount("sink", "loopStone")} time(s); floor kit: {kit}");
                        break;
                    }
                    case 994:
                    {
                        int lifts = s.KitCount("liftStart", "chainLift994");
                        int cuts = s.KitCount("liftCut", "chainLift994");
                        int cutters = s.SpawnCount("impCutter");
                        s.Check(prefix, lifts >= 2 && cutters >= 3 || !completed && lifts >= 1,
                            $"he rode {lifts} chain lift(s) (want 2), {cutters} imp(s) came to cut the chains (want 3), the chain gave {cuts} time(s); floor kit: {kit}");
                        break;
                    }
                    case 993:
                    {
                        s.Spawned.TryGetValue("imp", out int imps);
                        bool sleeper = s.Signals.ContainsKey("sleeperFell");
                        int lift = s.KitCount("liftStart", "chainLift993");
                        s.Check(prefix, imps >= 3 && (sleeper && lift >= 1 || !completed),
                            $"{imps} throwing imp(s) came (want 3: one alone, then two in the hollows), the sleeper {(sleeper ? "fell off its own ledge" : "never woke")}, " +
                            $"the lift ridden {lift} time(s); floor kit: {kit}");
                        break;
                    }
                    case 992:
                    {
                        int cracks = s.KitCount("crack", "crumblingStone");
                        int falls = s.KitCount("fall", "crumblingStone");
                        int swings = s.KitCount("swingGrab", "swingChain");
                        s.Check(prefix, cracks >= 5 && falls >= 3 && (swings >= 1 || !completed),
                            $"crumbling stones cracked under him {cracks} time(s) (want 5+) and {falls} fell (want 3+: the wall and the bridge behind him), " +
                            $"the chain over the gap ridden {swings} time(s); floor kit: {kit}");
                        break;
                    }
                    case 991:
                        FinishFloorBoss(s, completed);
                        break;
                    case 996:
                    {
                        int blows = s.KitCount("geyser", "lavaGeyser");
                        int falls = s.SpawnCount("lavaFallScenery");
                        s.Check(prefix, blows >= 3 && falls >= 1,
                            $"geysers blew {blows} time(s) (want 3+, each after its 1 s tell), {falls} lava fall(s) poured behind the run; floor kit: {kit}");
                        break;
                    }
                }
            }

            /// <summary>
            /// A floor boss on its climbing arena (Docs/LevelDesign.md, "Floor bosses": Floor 991, the Imp Foreman): the voiced intro
            /// (announcer, boss, hero) and the boss bar, the fight's beats in order, every attack 1+ s after its glow, the defeat in
            /// slow motion with a pile of diamonds and the announcer's call.
            /// </summary>
            void FinishFloorBoss(StageWatch s, bool completed)
            {
                string prefix = "level" + s.Level + ".boss.";
                float At(string signal) => s.Signals.TryGetValue(signal, out float t) ? t : -1f;
                bool Voiced(string text) => s.Banners.Exists(b => b.text == text && b.voiced) || s.Lines.Exists(l => l.text == text && l.voiced);
                var intro = new[] { "FLOOR 991: THE IMP FOREMAN", "Who's this? Get back in the sack!", "That's the biggest sack yet. Ours too, I bet." };
                var missing = Array.FindAll(intro, t => !Voiced(t));
                s.Check(prefix + "intro", missing.Length == 0 && s.BossBar.Length > 0 && At("fmIntro") >= 0f,
                    (missing.Length == 0 ? "the announcer, the Foreman and the hero all voiced" : $"not voiced: {string.Join(" | ", missing)}") +
                    $"; boss bar {(s.BossBar.Length > 0 ? $"\"{s.BossBar}\" at {s.BossBarAt:0.0} s" : "NEVER shown")}; the fight starts at {(At("fmFight") >= 0f ? At("fmFight").ToString("0.0") + " s" : "never")}");
                var order = new List<string>();
                float last = -1f;
                bool inOrder = true;
                foreach (var (signal, beat) in FloorBossSignals)
                {
                    float t = At(signal);
                    if (t < 0f) { if (completed) inOrder = false; order.Add($"{beat}: never"); continue; }
                    if (t < last) inOrder = false;
                    last = t;
                    order.Add($"{beat} at {t:0.0} s");
                }
                s.Check(prefix + "phases", completed ? inOrder : At("fmFight") >= 0f, string.Join("; ", order));
                var attacks = new List<string>();
                bool telegraphed = true;
                foreach (var t in FloorBossTelegraphs)
                {
                    s.SpawnTimes.TryGetValue(t.attack, out var hits);
                    s.SpawnTimes.TryGetValue(t.warning, out var glows);
                    if (hits == null || hits.Count == 0) { attacks.Add($"{t.what}: never"); if (completed) telegraphed = false; continue; }
                    float shortest = float.MaxValue;
                    int unwarned = 0;
                    foreach (float h in hits)
                    {
                        float best = -1f;
                        if (glows != null)
                            foreach (float g in glows)
                                if (g <= h + 0.001f && g > best) best = g;
                        // Every glow becomes its attack where it hung: the lead is the glow's life (1.1 s), never less than 1.
                        if (best < 0f) unwarned++;
                        else shortest = Mathf.Min(shortest, h - best);
                    }
                    if (unwarned > 0 || shortest < 1f) telegraphed = false;
                    attacks.Add($"{t.what}: {hits.Count}, shortest warning {(shortest < float.MaxValue ? shortest.ToString("0.00") : "-")} s{(unwarned > 0 ? $", {unwarned} WITHOUT a glow" : "")}");
                }
                s.Check(prefix + "telegraphs", telegraphed, string.Join("; ", attacks) + " (want every one 1.0+ s after its glow on the spot)");
                int haulers = s.SpawnCount("impHauler");
                s.Check(prefix + "adds", s.MaxEnemies <= BossFightEnemies && (s.PeakMinions >= 4 || !completed),
                    $"{haulers} sack imp(s) whistled onto the swings; up to {s.PeakMinions} minions of mixed types alive at once, at most {s.MaxEnemies} enemies counting him (allowed {BossFightEnemies})");
                if (!completed)
                    return;
                float defeat = At("fmDefeat");
                int pile = 0;
                if (s.SpawnTimes.TryGetValue("diamond", out var diamonds))
                    foreach (float d in diamonds)
                        if (defeat >= 0f && d >= defeat - 0.05f) pile++;
                const string down = "THE FOREMAN IS DOWN! HIS SACK, HOWEVER, IS UP FOR GRABS!";
                s.Check(prefix + "defeat", defeat >= 0f && s.SlowMotionAt >= 0f && pile >= 20 && Voiced(down) && Voiced("My... sack..."),
                    $"defeat at {(defeat >= 0f ? defeat.ToString("0.0") + " s" : "never")}; slow motion {(s.SlowMotionAt >= 0f ? $"at {s.SlowMotionAt:0.0} s" : "NOT seen")}; " +
                    $"{pile} diamonds in the pile (want 20+); his last line {(Voiced("My... sack...") ? "voiced" : "NOT voiced")}; the announcer's call {(Voiced(down) ? "voiced" : "NOT voiced")}");
            }

            /// <summary>
            /// The boss rework's criteria (BossFights): 3+ phases from the fight's signals, 4-8 minions of mixed types alive at the peak (and
            /// the peak of each phase), the fight from its first to its defeat signal 120-240 s, and the hero's voiced weakness hint.
            /// </summary>
            void FinishBossFight(StageWatch s, bool completed)
            {
                var def = BossFightOf(s.Level);
                if (def == null)
                    return;
                string prefix = $"level{s.Level}.bossFight.";
                float At(string signal) => s.Signals.TryGetValue(signal, out float t) ? t : -1f;
                var seen = new List<(string signal, float at)>();
                foreach (var signal in def.Phases)
                    if (At(signal) >= 0f)
                        seen.Add((signal, At(signal)));
                bool ordered = true;
                for (int i = 1; i < seen.Count; i++)
                    ordered &= seen[i].at >= seen[i - 1].at;
                s.Check(prefix + "phases", ordered && (seen.Count >= 3 || !completed),
                    $"{def.Name}: {seen.Count} phases opened (want 3+): {string.Join(", ", seen.ConvertAll(x => $"{x.signal} at {x.at:0.0} s"))}");
                // The peak of minions alive in each phase: the count carried over from before the phase, then every change inside it.
                var parts = new List<string>();
                for (int i = 0; i < seen.Count; i++)
                {
                    float from = seen[i].at, to = i + 1 < seen.Count ? seen[i + 1].at : float.MaxValue;
                    int carried = 0, peak = 0;
                    foreach (var sample in s.MinionSamples)
                    {
                        if (sample.at <= from)
                            carried = sample.count;
                        else if (sample.at < to)
                            peak = Mathf.Max(peak, sample.count);
                    }
                    parts.Add($"{seen[i].signal}: {Mathf.Max(peak, carried)}");
                    Run.Event("boss-phase", $"level {s.Level} ({def.Name}) {seen[i].signal} at {from:0.0} s: up to {Mathf.Max(peak, carried)} minions alive");
                }
                s.Check(prefix + "minions", s.PeakMinions <= 8 && (s.PeakMinions >= 4 || !completed),
                    $"{s.PeakMinions} minions alive at the peak (want 4-8); per phase: {string.Join(", ", parts)}");
                float fight = At(def.Fight), defeat = At(def.Defeat);
                float length = fight >= 0f && defeat >= fight ? defeat - fight : -1f;
                s.Check(prefix + "length", !completed || length >= BossFightMin && length <= BossFightMax,
                    length >= 0f ? $"the fight lasted {length:0} s from {def.Fight} to {def.Defeat} (want {BossFightMin:0}-{BossFightMax:0})"
                        : $"the fight has no end yet ({def.Fight} {(fight >= 0f ? "seen" : "never seen")}, {def.Defeat} {(defeat >= 0f ? "seen" : "never seen")})");
                bool hinted = s.Lines.Exists(l => l.voiced && s.Sounds.TryGetValue(l.text, out string sound) && sound == def.Hint);
                s.Check(prefix + "hint", hinted || !completed, hinted ? $"the hero said the weakness hint ({def.Hint})" : $"the hero never said {def.Hint} on a shown, voiced subtitle");
                Run.Metric($"boss.{def.Level}.phases", seen.Count);
                Run.Metric($"boss.{def.Level}.peakMinions", s.PeakMinions);
                Run.Metric($"boss.{def.Level}.fightSeconds", Mathf.Round(length));
                Run.Event("boss-fight", $"level {s.Level} ({def.Name}): {seen.Count} phases, up to {s.PeakMinions} minions, fight {(length >= 0f ? length.ToString("0") + " s" : "not over")}, hint {(hinted ? "said" : "NOT said")}");
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
                var w = s.Warden;
                var introLines = w.IntroLines;
                var missing = Array.FindAll(introLines, t => !Voiced(t));
                float fight = At(w.Fight);
                check(prefix + "intro", At(w.Intro) >= 0f && missing.Length == 0 && s.BossBar.Length > 0 && (fight < 0f || s.BossBarAt <= fight + 0.1f),
                    $"{w.Name} appears {(At(w.Intro) >= 0f ? $"at {At(w.Intro):0.0} s" : "NEVER")}; " +
                    (missing.Length == 0 ? "the announcer, his taunt and the hero's line all voiced" : $"not voiced: {string.Join(" | ", missing)}") +
                    $"; boss bar {(s.BossBar.Length > 0 ? $"\"{s.BossBar}\" at {s.BossBarAt:0.0} s" : "NEVER shown")}; the fight starts at {(fight >= 0f ? fight.ToString("0.0") + " s" : "never")}");

                // The phases, in order, each at its share of his health.
                var order = new List<string>();
                float last = -1f;
                bool inOrder = true;
                foreach (var (signal, beat) in w.Signals)
                {
                    float t = At(signal);
                    if (t < 0f)
                    {
                        if (completed && signal != w.Optional) inOrder = false;
                        order.Add($"{beat}: never");
                        continue;
                    }
                    if (t < last) inOrder = false;
                    last = t;
                    order.Add($"{beat} at {t:0.0} s");
                }
                check(prefix + "phases", completed ? inOrder : At(w.Fight) >= 0f, string.Join("; ", order));
                float HealthAt(string prefixState)
                {
                    var hit = s.BossStates.Find(x => x.state.StartsWith(prefixState, StringComparison.Ordinal));
                    return hit.state != null ? hit.health : -1f;
                }
                float standAt = HealthAt(w.HalfState), enrageAt = HealthAt(w.EnrageState);
                check(prefix + "enrage", completed ? standAt > 0f && standAt <= 0.5f && enrageAt > 0f && enrageAt <= 0.34f && At(w.Enrage) > At(w.Half) : true,
                    $"half health ({w.HalfState}) at {(standAt >= 0f ? standAt.ToString("P0") : "never")} health (want half or less), enrages at {(enrageAt >= 0f ? enrageAt.ToString("P0") : "never")} (want a third or less)" +
                    $"; states: {string.Join(" > ", s.BossStates.ConvertAll(x => x.state))}");
                float p2 = At(w.Phase2), p3 = At(w.Enrage);
                var stray = s.GuardSpawns.FindAll(t => p2 < 0f || t < p2 || p3 >= 0f && t > p3 + 0.05f);
                check(prefix + "adds", stray.Count == 0 && (!completed || s.GuardSpawns.Count > 0),
                    $"{s.GuardSpawns.Count} {w.AddsWhat}(s) came, {stray.Count} outside phase 2 (phase 2 from {(p2 >= 0f ? p2.ToString("0.0") : "?")} s to {(p3 >= 0f ? p3.ToString("0.0") : "?")} s)");

                // Every attack after its warning.
                var attacks = new List<string>();
                bool telegraphed = true;
                foreach (var t in w.Telegraphs)
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
                float defeat = At(w.Defeat);
                bool lastLine = Voiced(w.LastLine);
                check(prefix + "defeat", defeat >= 0f && s.SlowMotionAt >= 0f && lastLine && Voiced(w.Fallen),
                    $"defeat at {(defeat >= 0f ? defeat.ToString("0.0") + " s" : "never")}; slow motion {(s.SlowMotionAt >= 0f ? $"at {s.SlowMotionAt:0.0} s" : "NOT seen")}; " +
                    $"his last line {(lastLine ? "voiced" : "NOT voiced")}; \"{w.Fallen}\" {(Voiced(w.Fallen) ? "voiced" : "NOT voiced")}");
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
                var config = GameDefinition.Instance.GetLevel(n);
                int nextVideo = config != null ? config.nextVideoNumber : 0;
                // Level 1's Video2 is skipped on the story path; the Shaft plunge (WatchClimb) leads to Floor 1000.
                bool skipVideoToPlunge = n == 1 && nextVideo > 0
                    && GameDefinition.Instance.shaft != null && nextVideo == GameDefinition.Instance.shaft.plungeAfterVideo;
                int nextLevel = config != null && (config.nextVideoNumber <= 0 || skipVideoToPlunge) ? (skipVideoToPlunge ? n + 1 : config.nextLevelNumber) : 0;
                yield return WatchClimb(n, check);
                if (Run.Done) yield break;
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
            /// wastes-climb (and landing-stretch): level 'first' loaded directly, then each level up to 'last' through the real
            /// flow (results, Continue, the Shaft, a video and Yooba's hello where the story puts one, the next level), each with
            /// the floor-stage checks; a one-floor level's Shaft must climb exactly one floor; the last one leads to the level
            /// select, or to the level it names (the old stages after Floor 996).
            /// </summary>
            public IEnumerator WastesClimb(int first = ClimbFrom, int last = ClimbTo, float seconds = WastesClimbSeconds, bool keepChecks = false,
                bool finish = true)
            {
                var definition = GameDefinition.Instance;
                var texts = definition.menuTheme.texts;
                float deadline = Time.realtimeSinceStartup + seconds - 30f;
                if (!keepChecks)
                    stageChecksFailed = 0;
                Run.Event("shortcut", $"load level {first} directly");
                var load = LoadLevel(first);
                if (load != null)
                    yield return load;
                bool reachedEnd = true;
                GameplayWorld previous = null;
                for (int n = first; n <= last; n++)
                {
                    tookShortcutTo = 0;
                    if (n > first)
                    {
                        // A video the story puts here (Video3 after Floor 1000). Level 1's Video2 is skipped on a first run.
                        var before = definition.GetLevel(n - 1);
                        bool expectVideo = before != null && before.nextVideoNumber > 0
                            && !(before.nextVideoNumber == definition.shaft?.plungeAfterVideo && n - 1 == 1);
                        if (expectVideo)
                        {
                            yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null || NewWorld(previous) != null, 60f, $"video {before.nextVideoNumber} to start");
                            if (Run.Done) yield break;
                            bool inVideo = Object.FindAnyObjectByType<VideoRuntime>() != null;
                            int playing = inVideo ? PlayerPrefs.GetInt("CurrentVideoNumber", 0) : 0;
                            StageCheck("level" + (n - 1) + ".video", inVideo && playing == before.nextVideoNumber,
                                inVideo ? $"video {playing} plays after level {n - 1}" : $"no video after level {n - 1} (want video {before.nextVideoNumber})");
                            if (inVideo)
                            {
                                Run.Scene("Video " + playing);
                                yield return WaitReal(playing == 3 ? 13f : 4f); // Video3's Yooba spot starts at 11.5 s
                                if (Run.Done) yield break;
                                float skipUntil = Time.realtimeSinceStartup + 20f;
                                while (!Run.Done && Object.FindAnyObjectByType<VideoRuntime>() != null && ShaftScreen.Current == null && Time.realtimeSinceStartup < skipUntil)
                                {
                                    yield return SkipTap($"video {playing}");
                                    yield return WaitReal(2f);
                                }
                                if (Run.Done) yield break;
                                yield return Run.Until(() => ShaftScreen.Current != null || NewWorld(previous) != null, 15f, "Yooba's hello on the Shaft after video " + playing);
                                if (Run.Done) yield break;
                                if (ShaftScreen.Current != null)
                                {
                                    yield return PassShaft();
                                    if (Run.Done) yield break;
                                }
                            }
                        }
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
                        float from = (float)(n - first) / (last - first + 1), to = (float)(n - first + 1) / (last - first + 1);
                        if (takeShortcuts && n == RouteShortcuts[0].level)
                            TopUpFare();
                        stage = IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                        yield return PlayLevel(n, 0f, stage != null ? StageDuring : (Func<float, IEnumerator>)null, from, to, left, waitOutDeath: true,
                            until: n == RouteShortcuts[0].level ? ForemanExitWatch(n) : null);
                        if (Run.Done) yield break;
                        if (stage != null && (lastOutcome != "died" || attempts >= 3))
                        {
                            tookShortcutTo = stage.ShortcutTo;
                            RouteChecks(n, stage);
                            FinishStage(lastOutcome);
                        }
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
                    lastShaft = null;
                    yield return WatchClimb(n, StageCheck);
                    if (Run.Done) yield break;
                    // One level = one floor: the marker climbs one floor (Docs/Story.md, "The Shaft").
                    ShaftScreen.LevelFloors(n, out int floorStart, out int floorEnd);
                    if (floorStart - floorEnd == 1 && tookShortcutTo <= 0)
                        StageCheck("level" + n + ".climbOneFloor", lastShaft != null && lastShaft.From == floorStart && lastShaft.Shown == floorEnd,
                            lastShaft != null ? $"the Shaft's marker climbed from FLOOR {lastShaft.From} to FLOOR {lastShaft.Shown} (want {floorStart} to {floorEnd}: one floor)"
                                : "no Shaft climb seen");
                }
                if (reachedEnd && LoadLevel == DefaultLoadLevel)
                {
                    var lastConfig = definition.GetLevel(last);
                    int after = lastConfig != null && lastConfig.nextVideoNumber <= 0 ? lastConfig.nextLevelNumber : 0;
                    yield return Run.Until(() => Object.FindAnyObjectByType<LevelSelectionManager>() != null || Object.FindAnyObjectByType<VideoRuntime>() != null
                                                 || NewWorld(previous) != null, 60f, $"what comes after level {last}");
                    if (Run.Done) yield break;
                    if (after > 0)
                    {
                        var world = NewWorld(previous);
                        StageCheck("level" + last + ".next", world != null && world.LevelNumber == after,
                            world != null ? $"level {world.LevelNumber} ({world.LevelName}) starts after level {last}'s Shaft (want level {after})"
                                : $"level {last} led to {SceneName()} instead of level {after}");
                        Run.Scene(world != null ? "Level " + world.LevelNumber : SceneName());
                        yield return WaitReal(4f);
                    }
                    else
                    {
                        bool select = Object.FindAnyObjectByType<LevelSelectionManager>() != null;
                        StageCheck("level" + last + ".next", select, select ? $"level {last} leads to the level select (the next floor isn't built yet)"
                            : $"level {last} led to {SceneName()} instead of the level select");
                        Run.Scene(select ? "Level Select" : SceneName());
                        yield return WaitReal(2f);
                    }
                }
                longWayReachedEnd = reachedEnd;
                if (!finish)
                    yield break;
                Run.Progress(1f);
                End(stageChecksFailed > 0 ? PlaytestOutcome.Done
                    : reachedEnd && lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            /// <summary>Whether the last WastesClimb got through its last level (long-way reads it between its two legs).</summary>
            bool longWayReachedEnd;

            /// <summary>
            /// long-way: the old stages over Floors 990-951 in order. Levels 68-70 through the real flow (70 leads on to Floor 960),
            /// then level 71 (The Loop, level select only) loaded directly, each with the stage checks and its Shaft climb.
            /// </summary>
            public IEnumerator LongWayStages()
            {
                float started = Time.realtimeSinceStartup;
                yield return WastesClimb(LongWayFrom, LongWayTo, LongWayStagesSeconds * 0.72f, finish: false);
                if (Run.Done) yield break;
                bool firstLeg = longWayReachedEnd && lastOutcome == "completed";
                if (!firstLeg)
                    Run.Event("stopped", $"the long way stopped before level {LongWayTo}'s end ({lastOutcome}); playing level {LongWayLoop} anyway");
                if (GameplayWorld.Instance != null)
                    GameplayWorld.Instance.Telemetry?.Finish("playtest left");
                Run.Event("shortcut", $"level {LongWayLoop} (The Loop) is played only from the level select: loading it directly");
                yield return WastesClimb(LongWayLoop, LongWayLoop, LongWayStagesSeconds - (Time.realtimeSinceStartup - started), keepChecks: true, finish: false);
                if (Run.Done) yield break;
                bool loop = longWayReachedEnd && lastOutcome == "completed";
                Run.Progress(1f);
                End(stageChecksFailed > 0 ? PlaytestOutcome.Done
                    : firstLeg && loop ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            int shortcutPassed, shortcutFailed;
            /// <summary>The floor a shortcut taken on the level just played let him out on (0 = none).</summary>
            int tookShortcutTo;

            /// <summary>Setup shortcut: the diamonds topped up to the Sack Lift's fare, so the route can take it whatever the bot picked up.</summary>
            void TopUpFare()
            {
                var roguelite = RogueliteManager.Instance;
                if (roguelite == null || roguelite.GetPoints() >= SackLiftFare)
                    return;
                int add = SackLiftFare - roguelite.GetPoints();
                roguelite.AddPoints(add);
                Run.Event("shortcut", $"{add} diamonds given, so the Sack Lift's fare ({SackLiftFare}) can be paid");
            }

            /// <summary>
            /// Floor 991 (level n): from the Foreman's defeat the Sack Lift's choice must be up (or made, or the floor on its way
            /// out) within ForemanExitSeconds of game time. If not, a bug, and PlayLevel stops ("checked") instead of idling to its
            /// timeout (beta v159 stood on the pile for 230 s). A rewind back into the fight starts the watch over.
            /// </summary>
            Func<bool> ForemanExitWatch(int n)
            {
                float defeatAt = -1f;
                bool reported = false;
                return () =>
                {
                    var world = GameplayWorld.Instance;
                    if (reported || world == null || world.Director == null || world.LevelNumber != n || GameClock.Instance == null)
                        return false;
                    float now = GameClock.Instance.Time;
                    string phase = world.Director.PhaseName;
                    if (defeatAt < 0f)
                    {
                        if (phase == "defeat")
                            defeatAt = now;
                        return false;
                    }
                    bool choiceUp = world.Hud != null && world.Hud.ShortcutOpen;
                    bool chosen = world.Shortcuts != null && world.Shortcuts.Choice(RouteShortcuts[0].id).HasValue;
                    bool leaving = phase == "sackLift" || phase == "longWay" || world.Director.Completed;
                    if (choiceUp || chosen || leaving)
                    {
                        reported = true;
                        StageCheck("level" + n + ".exit", true, (choiceUp ? "the Sack Lift's choice is up" : chosen ? "the Sack Lift chosen" : $"the floor is ending (phase \"{phase}\")") +
                                                                $" {now - defeatAt:0.0} s after the Foreman's defeat (want within {ForemanExitSeconds:0.#} s)");
                        return false;
                    }
                    if (phase != "defeat")
                    {
                        defeatAt = -1f;
                        return false;
                    }
                    if (now - defeatAt <= ForemanExitSeconds)
                        return false;
                    reported = true;
                    var hero = world.Player != null ? world.Player.Position : Vector2.zero;
                    StageCheck("level" + n + ".exit", false, $"{now - defeatAt:0.0} s after the Foreman's defeat no Sack Lift choice came up and the floor didn't end " +
                                                             $"(phase \"{phase}\", FLOOR {(world.Stage != null ? world.Stage.Floor : 0)}, the hero at ({hero.x:0.0}, {hero.y:0.0})): stopped here");
                    return true;
                };
            }

            /// <summary>
            /// circle-x-route, the long way first: Floor 991 loaded directly with the fare in hand and the bot pressing 1 on the Sack
            /// Lift's choice. Checked: the choice came up with its fare, declined, the counter on 990, the Grudge pick, then the Shaft's
            /// climb to 990 and the long way's first stage (Floor 991's nextLevelNumber) starting on FLOOR 990. The Shaft's marker
            /// and the run's level are put back after (setup shortcut), so the paid run climbs from 991 as a player's would.
            /// </summary>
            IEnumerator SackLiftDeclined(float seconds)
            {
                var definition = GameDefinition.Instance;
                var texts = definition.menuTheme.texts;
                int n = RouteShortcuts[0].level;
                string prefix = "level" + n + ".longWay.";
                int shaftFloor = ShaftProgress.Floor, runLevel = WaystoneProgress.RunLevel;
                float deadline = Time.realtimeSinceStartup + seconds;
                takeShortcuts = false;
                Run.Event("shortcut", $"load level {n} directly: the long way first (the bot keeps climbing past the Sack Lift)");
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                StageWatch watched = null;
                int attempts = 0;
                while (true)
                {
                    attempts++;
                    TopUpFare();
                    stage = new StageWatch(n, StageCheck);
                    yield return PlayLevel(n, 0f, StageDuring, 0f, 0.08f, Mathf.Max(30f, deadline - Time.realtimeSinceStartup), waitOutDeath: true,
                        until: ForemanExitWatch(n));
                    if (Run.Done) yield break;
                    if (lastOutcome != "died" || attempts >= 3)
                    {
                        watched = stage;
                        if (stage != null)
                            FinishStage(lastOutcome);
                        stage = null;
                        break;
                    }
                    stage = null;
                    Run.Event("retry", $"level {n} (the long way), attempt {attempts + 1}");
                    yield return ReachLevel(n);
                    if (Run.Done) yield break;
                }

                bool completed = lastOutcome == "completed";
                int lastFloor = watched != null && watched.Floors.Count > 0 ? watched.Floors[watched.Floors.Count - 1] : 0;
                StageCheck(prefix + "offered", watched != null && watched.ShortcutOffer.Length > 0,
                    watched != null && watched.ShortcutOffer.Length > 0 ? $"the Sack Lift offered: {watched.ShortcutOffer}" : $"no Sack Lift choice came up (level {n}: {lastOutcome})");
                StageCheck(prefix + "declined", watched != null && watched.ShortcutDeclined == RouteShortcuts[0].id && watched.ShortcutTo == 0,
                    watched == null ? "not played" : watched.ShortcutTo > 0 ? $"taken to FLOOR {watched.ShortcutTo} (the bot pressed 1: want kept climbing)"
                        : watched.ShortcutDeclined.Length > 0 ? "kept climbing" : "never chosen");
                StageCheck(prefix + "counter", watched != null && lastFloor == watched.End && !watched.Floors.Exists(f => f < watched.End),
                    watched != null ? $"the floor counter ran {string.Join(" ", watched.Floors)} (want it to end on {watched.End})" : "not played");
                StageCheck(prefix + "pick", watched != null && watched.Picked, watched != null && watched.Picked ? "a Grudge pick at the top" : "no Grudge pick after keeping on");
                if (completed && LoadLevel == DefaultLoadLevel)
                {
                    var previous = GameplayWorld.Instance;
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                    yield return WatchClimb(n, StageCheck);
                    if (Run.Done) yield break;
                    int after = definition.GetLevel(n).nextLevelNumber;
                    yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null || Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                                 || NewWorld(previous) != null && GameplayWorld.Instance.Player != null
                                                 && GameClock.Instance != null && GameClock.Instance.Time > 0f, 60f, $"level {after} to start (the long way)");
                    if (Run.Done) yield break;
                    var next = NewWorld(previous);
                    int floor = next != null && next.Stage != null ? next.Stage.Floor : 0;
                    StageCheck(prefix + "next", next != null && next.LevelNumber == after && floor == lastFloor,
                        next != null ? $"level {next.LevelNumber} ({next.LevelName}) starts on FLOOR {floor} after the Shaft (want level {after} on FLOOR {lastFloor})"
                            : $"{SceneName()} came after the Shaft (want level {after})");
                    Run.Scene(next != null ? "Level " + next.LevelNumber : SceneName());
                    yield return WaitReal(3f);
                    if (Run.Done) yield break;
                    if (next != null && next.Telemetry != null)
                        next.Telemetry.Finish("playtest left");
                }
                ShaftProgress.Floor = shaftFloor;
                WaystoneProgress.RunLevel = runLevel;
                Run.Event("shortcut", $"the Shaft's marker put back on FLOOR {shaftFloor} and the run on level {runLevel}: now the Sack Lift, paid");
            }

            /// <summary>
            /// Circle X's route (takeShortcuts): a level with a shortcut must have offered it with its cost, been left through it (the
            /// counter on its toFloor, no Grudge pick, the fare paid), and the floor after the King's Chimney played harder for it.
            /// </summary>
            void RouteChecks(int n, StageWatch watched)
            {
                if (!takeShortcuts || lastOutcome != "completed")
                    return;
                foreach (var (level, id, toFloor, next) in RouteShortcuts)
                {
                    if (level != n)
                        continue;
                    string prefix = "level" + n + ".shortcut.";
                    StageCheck(prefix + "offered", watched.ShortcutOffer.Length > 0, watched.ShortcutOffer.Length > 0 ? $"{id} offered: {watched.ShortcutOffer}" : $"no {id} choice came up");
                    StageCheck(prefix + "taken", watched.ShortcutTo == toFloor, watched.ShortcutTo > 0 ? $"{id} taken: out on FLOOR {watched.ShortcutTo} (want {toFloor})"
                        : watched.ShortcutDeclined.Length > 0 ? "the bot kept climbing instead" : "never taken");
                    int last = watched.Floors.Count > 0 ? watched.Floors[watched.Floors.Count - 1] : 0;
                    StageCheck(prefix + "counterJump", last == toFloor && !watched.Floors.Exists(f => f < watched.End && f > toFloor),
                        $"the floor counter ran {string.Join(" ", watched.Floors)}: up to {watched.End}, then straight to {toFloor}");
                    StageCheck(prefix + "noPick", !watched.Picked, watched.Picked ? "a Grudge pick came after the shortcut" : "no Grudge pick after it");
                    var shortcuts = GameplayWorld.Instance != null ? GameplayWorld.Instance.Shortcuts : null;
                    if (id == "sackLift")
                        StageCheck(prefix + "farePaid", shortcuts != null && shortcuts.Paid.IndexOf("diamonds", StringComparison.OrdinalIgnoreCase) >= 0,
                            $"paid: \"{(shortcuts != null ? shortcuts.Paid : "?")}\" (want {SackLiftFare} diamonds); offered as \"{watched.ShortcutOffer}\"");
                }
                if (n == RouteShortcuts[1].next)
                    StageCheck("level" + n + ".chimneyRisk", watched.Lines.Exists(l => l.text.StartsWith("Came up my chimney", StringComparison.Ordinal)),
                        "after the King's Chimney the King says so on Floor 910: " + string.Join(" | ", watched.Lines.ConvertAll(l => l.text)));
            }

            /// <summary>
            /// circle-x-route: Floor 991 played the long way first (the Sack Lift declined, on to Floor 990), then loaded directly
            /// again (the fare topped up) and the real flow to Floor 901, taking both shortcuts.
            /// </summary>
            public IEnumerator CircleXRoute()
            {
                float started = Time.realtimeSinceStartup;
                stageChecksFailed = 0;
                yield return SackLiftDeclined(LongWaySeconds);
                if (Run.Done) yield break;
                takeShortcuts = true;
                try
                {
                    yield return WastesClimb(RouteFrom, RouteTo, RouteSeconds - (Time.realtimeSinceStartup - started), keepChecks: true);
                }
                finally
                {
                    takeShortcuts = false;
                }
            }

            /// <summary>
            /// cracked-road-route: Floor 901 loaded directly (the Magma King, his Waystone), then the real flow through Circle IX's played
            /// route to Floor 801 (the Spire Fiend, his Waystone), taking the Spared Demon (891 to 860) and the Hollow Spire (851 to 810).
            /// </summary>
            public IEnumerator CrackedRoadRoute()
            {
                stageChecksFailed = 0;
                takeShortcuts = true;
                try
                {
                    yield return WastesClimb(CrackedFrom, CrackedTo, CrackedSeconds, keepChecks: true);
                }
                finally
                {
                    takeShortcuts = false;
                }
            }

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

            // ─── restart: the pause menu's Restart puts the level back to its start ─────

            /// <summary>Where a level stands one second in: the hero, the clock, his hearts, the floor and its first moving pieces.</summary>
            sealed class LevelSnapshot
            {
                public float Clock;
                public Vector2 Hero;
                public int Health;
                public int Floor;
                public float Distance;
                public readonly List<(string id, string state, Vector2 at)> Pieces = new List<(string, string, Vector2)>();
            }

            const float RestartSnapshotAt = 1f, RestartTolerance = 0.35f;
            const int RestartPieces = 6;

            int restartPassed, restartFailed;

            void RestartCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    restartPassed++;
                    Run.Event("check", $"restart {beat}: ok ({detail})");
                }
                else
                {
                    restartFailed++;
                    Run.Event("check", $"restart {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"restart {beat}: {detail}");
                }
            }

            static int LevelScenesLoaded()
            {
                int n = 0;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene.name == "Level")
                        n++;
                }
                return n;
            }

            /// <summary>Waits until level n's clock reaches RestartSnapshotAt (no bot, no input), then records it.</summary>
            IEnumerator SnapshotLevelStart(int n, Action<LevelSnapshot> done)
            {
                if (bot != null)
                    Object.Destroy(bot);
                bot = null;
                input.ReleaseAll();
                Time.timeScale = 1f;
                yield return Run.Until(() => LevelRunning(n) && GameClock.Instance.Time >= RestartSnapshotAt, 60f, $"level {n} to run {RestartSnapshotAt:0} s");
                if (Run.Done)
                    yield break;
                var world = GameplayWorld.Instance;
                var health = world.Player.GetComponentInChildren<PlayerHealth>();
                var shot = new LevelSnapshot
                {
                    Clock = GameClock.Instance.Time,
                    Hero = world.Player.transform.position,
                    Health = health != null ? health.CurrentHealth : -1,
                    Floor = world.Stage != null ? world.Stage.Floor : 0,
                    Distance = world.GetCounter(StageProgress.DistanceCounter),
                };
                var pieces = new List<Entity>();
                foreach (var e in world.Live)
                    if (e != null && !e.IsPlayer)
                        pieces.Add(e);
                pieces.Sort((a, b) => a.Serial.CompareTo(b.Serial));
                for (int i = 0; i < pieces.Count && i < RestartPieces; i++)
                    shot.Pieces.Add((pieces[i].DefId, pieces[i].StateName, pieces[i].transform.position));
                Run.Event("snapshot", $"level {n} at {shot.Clock:0.00} s: hero ({shot.Hero.x:0.00}, {shot.Hero.y:0.00}), {shot.Health} hearts, floor {shot.Floor}, " +
                                      $"distance {shot.Distance:0.0}, {world.Live.Count} entities; first: " +
                                      string.Join(", ", shot.Pieces.ConvertAll(p => $"{p.id}[{p.state}]@({p.at.x:0.0},{p.at.y:0.0})")));
                done(shot);
            }

            /// <summary>
            /// Level 2 (Floor 1000) loaded directly, recorded 1 s in; the bot plays a few seconds; P, Restart; the restarted
            /// level, recorded 1 s in, must match the fresh one (one Level scene, a new world, hero, clock, hearts, floor, pieces).
            /// </summary>
            public IEnumerator RestartRun()
            {
                int n = LandingLevel;
                Run.Event("shortcut", $"load level {n} directly");
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                LevelSnapshot fresh = null, restarted = null;
                yield return SnapshotLevelStart(n, s => fresh = s);
                if (Run.Done) yield break;
                var firstWorld = GameplayWorld.Instance;
                Run.Progress(0.2f);

                // Play a few seconds so the hero, the clock and the floor move away from the start.
                yield return PlayLevel(n, RestartSnapshotAt + 7f, progressFrom: 0.2f, progressTo: 0.5f);
                if (Run.Done) yield break;
                if (GameplayWorld.Instance != firstWorld || lastOutcome != "time up")
                {
                    RestartCheck("played", false, $"the level didn't keep running for the restart ({lastOutcome})");
                    End(PlaytestOutcome.Done);
                    yield break;
                }
                var player = firstWorld.Player;
                Vector2 before = player.transform.position;
                Run.Event("played", $"at {GameClock.Instance.Time:0.0} s: hero ({before.x:0.00}, {before.y:0.00}), floor {firstWorld.Stage?.Floor}");
                if (bot != null)
                    Object.Destroy(bot);
                bot = null;
                input.ReleaseAll();

                yield return Tap(Key.P);
                yield return WaitReal(0.5f);
                RestartCheck("paused", firstWorld != null && firstWorld.Hud != null && firstWorld.Hud.Paused, "P opened the pause menu");
                yield return ClickUntil(GameDefinition.Instance.menuTheme.texts.restart,
                    () => GameplayWorld.Instance != null && GameplayWorld.Instance != firstWorld, 45f);
                if (Run.Done) yield break;
                Run.Progress(0.7f);

                yield return SnapshotLevelStart(n, s => restarted = s);
                if (Run.Done) yield break;
                var worlds = Object.FindObjectsByType<GameplayWorld>(FindObjectsSortMode.None);
                RestartCheck("reloaded", firstWorld == null && worlds.Length == 1 && LevelScenesLoaded() == 1,
                    $"old world {(firstWorld == null ? "gone" : "STILL ALIVE")}, {worlds.Length} world(s), {LevelScenesLoaded()} Level scene(s) loaded");
                RestartCheck("clock", Mathf.Abs(restarted.Clock - fresh.Clock) < 0.25f,
                    $"clock {restarted.Clock:0.00} s after the restart, {fresh.Clock:0.00} s on a fresh load");
                RestartCheck("hero", Vector2.Distance(restarted.Hero, fresh.Hero) < RestartTolerance,
                    $"hero at ({restarted.Hero.x:0.00}, {restarted.Hero.y:0.00}), fresh ({fresh.Hero.x:0.00}, {fresh.Hero.y:0.00}); before the restart ({before.x:0.00}, {before.y:0.00})");
                RestartCheck("hearts", restarted.Health == fresh.Health, $"{restarted.Health} hearts, {fresh.Health} on a fresh load");
                RestartCheck("floor", restarted.Floor == fresh.Floor && Mathf.Abs(restarted.Distance - fresh.Distance) < RestartTolerance,
                    $"floor {restarted.Floor}, distance {restarted.Distance:0.0}; fresh floor {fresh.Floor}, distance {fresh.Distance:0.0}");
                bool same = restarted.Pieces.Count == fresh.Pieces.Count;
                for (int i = 0; same && i < fresh.Pieces.Count; i++)
                    same = restarted.Pieces[i].id == fresh.Pieces[i].id && restarted.Pieces[i].state == fresh.Pieces[i].state
                           && Vector2.Distance(restarted.Pieces[i].at, fresh.Pieces[i].at) < RestartTolerance;
                RestartCheck("pieces", same, $"the first {fresh.Pieces.Count} moving pieces (ids, states, places) {(same ? "match" : "DIFFER from")} a fresh load");

                Run.Metric("restart.passed", restartPassed);
                Run.Metric("restart.failed", restartFailed);
                Run.Progress(1f);
                End(restartFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            // ─── rewind-perf: frame times while rewinding a busy floor vs playing it ─────

            int rewindPerfPassed, rewindPerfFailed;

            void RewindPerfCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    rewindPerfPassed++;
                    Run.Event("check", $"rewind-perf {beat}: ok ({detail})");
                }
                else
                {
                    rewindPerfFailed++;
                    Run.Event("check", $"rewind-perf {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"rewind-perf {beat}: {detail}");
                }
            }

            void OnRewindPerf(string line) => Run.Event("rewind-perf", line);

            public IEnumerator RewindPerfRun(bool finish = true, float cpuFactor = 1f)
            {
                StartRewindThrottle(cpuFactor);
                var definition = GameDefinition.Instance;
                int n = definition.levels.FindIndex(l => l != null && l.levelName == RewindPerfLevel) + 1;
                if (n <= 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"rewind-perf: no level \"{RewindPerfLevel}\" in game.json");
                    StopRewindThrottle();
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                int rewinds = 0, peakLive = 0;
                bool measuring = false;
                for (int attempt = 1; attempt <= 3 && rewinds < RewindPerfRewinds && !Run.Done; attempt++)
                {
                    Run.Event("shortcut", $"load level {n} ({RewindPerfLevel}) directly, try {attempt}");
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                    float playedSince = -1f;
                    bool holding = false;
                    yield return PlayLevel(n, 0f, now =>
                    {
                        var world = GameplayWorld.Instance;
                        if (bot != null)
                            bot.useRewind = false;
                        if (world != null && world.Live.Count > peakLive)
                            peakLive = world.Live.Count;
                        SampleRewindPlaces(world);
                        if (holding)
                            return null;
                        // Measure from 3 s in: the level's own loading frames aren't play.
                        if (!measuring && now >= 3f)
                        {
                            measuring = true;
                            RewindPerf.Reset();
                            Run.Event("rewind-perf", $"measuring from {now:0.0} s");
                        }
                        if (playedSince < 0f)
                            playedSince = Time.realtimeSinceStartup;
                        if (!measuring || Time.realtimeSinceStartup - playedSince < RewindPerfPlay / Speed)
                            return null;
                        return RewindPerfHoldOnce(world, rewound => { holding = false; playedSince = Time.realtimeSinceStartup; if (rewound) rewinds++; }, () => holding = true);
                    }, (attempt - 1) / 3f, attempt / 3f, 200f, until: () => rewinds >= RewindPerfRewinds && !holding);
                    if (Run.Done)
                        yield break;
                    if (rewinds < RewindPerfRewinds)
                        Run.Event("rewind-perf", $"try {attempt} ended ({lastOutcome}) after {rewinds} rewind(s)");
                }

                var forward = RewindPerf.Forward;
                var rewinding = RewindPerf.Rewinding;
                var after = RewindPerf.AfterRewind;
                StopRewindThrottle();
                ReportRewindPerf();
                CheckRewindPerfSlow(forward, rewinding);
                Run.Metric("rewindPerf.peakEntities", peakLive);
                Run.Event("rewind-perf", $"up to {peakLive} live entities");

                RewindPerfCheck("rewinds", rewinds >= RewindPerfRewinds, $"{rewinds} of {RewindPerfRewinds} rewinds held on level {n}");
                if (rewinding.Frames > 0 && forward.Frames > 0)
                {
                    double allowed = cpuFactor > 1f ? forward.AverageMs * 1.5 : Math.Max(forward.AverageMs * 1.5, forward.AverageMs + 5.0);
                    RewindPerfCheck("smooth", rewinding.AverageMs <= allowed,
                        $"rewinding averages {rewinding.AverageMs:0.0} ms a frame, playing {forward.AverageMs:0.0} ms (allowed {allowed:0.0})");
                    RewindPerfCheck("no spikes", rewinding.WorstMs <= 100.0,
                        $"worst rewind frame {rewinding.WorstMs:0.0} ms (worst playing frame {forward.WorstMs:0.0} ms)");
                    // The daily budgets in absolute terms: a busy floor must play and rewind at a playable rate whatever the ratio.
                    RewindPerfCheck("frame budget playing", forward.AverageMs <= FramePlayAverageMs,
                        $"playing averages {forward.AverageMs:0.0} ms a frame (budget {FramePlayAverageMs:0} ms)");
                    RewindPerfCheck("frame budget rewinding", rewinding.AverageMs <= FrameRewindAverageMs,
                        $"rewinding averages {rewinding.AverageMs:0.0} ms a frame (budget {FrameRewindAverageMs:0} ms)");
                    Run.Metric("frameTime.playingAvgMs", (float)forward.AverageMs);
                    Run.Metric("frameTime.rewindingAvgMs", (float)rewinding.AverageMs);
                }
                if (after.Frames > 0)
                    RewindPerfCheck("clamped after", RewindPerf.WorstStepAfterRewind <= GameClock.PostRewindMaxFrame * 1.05f + 0.0005f,
                        $"the clock moved at most {RewindPerf.WorstStepAfterRewind * 1000f:0.0} ms of game time on a frame right after a rewind " +
                        $"(clamp {GameClock.PostRewindMaxFrame * 1000f:0.0} ms), the frames themselves up to {after.WorstMs:0.0} ms");

                Run.Metric("rewindPerf.passed", rewindPerfPassed);
                Run.Metric("rewindPerf.failed", rewindPerfFailed);
                if (!finish)
                    yield break;
                Run.Progress(1f);
                End(rewindPerfFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>Tops up the juice (a setup shortcut: it isn't rewound state) and holds R for RewindPerfHold real seconds.</summary>
            IEnumerator RewindPerfHoldOnce(GameplayWorld world, Action<bool> done, Action started)
            {
                started();
                var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
                if (budget != null)
                {
                    float need = RewindPerfHold * 3.5f;
                    if (budget.Max < need)
                        budget.RaiseMax(need - budget.Max, false);
                    budget.Add(budget.Max);
                    Run.Event("shortcut", $"rewind juice topped up to {budget.Juice:0.0} s");
                }
                float before = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
                yield return HoldRewind(RewindPerfHold);
                float back = before - (GameClock.Instance != null ? GameClock.Instance.Time : 0f);
                Run.Event("rewind", $"held R {RewindPerfHold:0.0} s: {back:0.0} s of game time back, {(world != null ? world.Live.Count : 0)} live entities" +
                                    (back < 0.5f ? " (no rewind: locked by the scene or refused; tried again later)" : ""));
                if (back >= 0.5f)
                    CheckRewindPlaces(world, RewindPerf.Rewinds);
                done(back >= 0.5f);
            }

            // ─── aim-guide: the laser line from the muzzle matches the shots ─────

            int aimPassed, aimFailed;
            string aimPhase = "";
            int aimPhaseVolleys, aimPhaseBad, aimPhaseLogged;
            int aimExpectShots;
            float aimWorstDelta;

            void AimCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    aimPassed++;
                    Run.Event("check", $"aim-guide {beat}: ok ({detail})");
                }
                else
                {
                    aimFailed++;
                    Run.Event("check", $"aim-guide {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"aim-guide {beat}: {detail}");
                }
            }

            static float Degrees(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            static string Angles(IReadOnlyList<Vector2> list)
            {
                var parts = new List<string>();
                for (int i = 0; i < list.Count; i++)
                    parts.Add($"{Degrees(list[i]):0.0}°");
                return "[" + string.Join(", ", parts) + "]";
            }

            /// <summary>A volley left the gun: the guide's lines on that frame against the shots' flight directions.</summary>
            void OnAimVolley(AimGuide guide, IReadOnlyList<Vector2> lines, IReadOnlyList<Vector2> before, IReadOnlyList<Vector2> shots)
            {
                if (string.IsNullOrEmpty(aimPhase))
                    return;
                aimPhaseVolleys++;
                float worst = 0f;
                bool ok = lines.Count == shots.Count && shots.Count == aimExpectShots;
                for (int i = 0; ok && i < shots.Count; i++)
                    worst = Mathf.Max(worst, Vector2.Angle(lines[i], shots[i]));
                ok &= worst <= AimGuideToleranceDegrees;
                if (ok)
                    aimWorstDelta = Mathf.Max(aimWorstDelta, worst);
                else
                    aimPhaseBad++;
                float drift = 0f;
                for (int i = 0; i < shots.Count && i < before.Count; i++)
                    drift = Mathf.Max(drift, Vector2.Angle(before[i], shots[i]));
                if (!ok || aimPhaseLogged < 6)
                {
                    aimPhaseLogged++;
                    Run.Event("aim", $"{aimPhase} volley {aimPhaseVolleys}: guide {lines.Count} line(s) {Angles(lines)}, " +
                                     $"shots {shots.Count} {Angles(shots)}, want {aimExpectShots}; Δ {worst:0.00}° " +
                                     $"({(ok ? "match" : "MISMATCH")}; the frame before {(before.Count > 0 ? $"Δ {drift:0.0}°" : "no line")})");
                }
            }

            void BeginAimPhase(string phase, int shots)
            {
                aimPhase = phase;
                aimExpectShots = shots;
                aimPhaseVolleys = aimPhaseBad = aimPhaseLogged = 0;
                aimWorstDelta = 0f;
            }

            void EndAimPhase(string beat)
            {
                AimCheck(beat, aimPhaseVolleys >= 3 && aimPhaseBad == 0,
                    $"{aimPhase}: {aimPhaseVolleys} volley(s) of {aimExpectShots} shot(s), {aimPhaseBad} not matching their guide lines, " +
                    $"worst match Δ {aimWorstDelta:0.00}° (tolerance {AimGuideToleranceDegrees}°)");
                aimPhase = "";
            }

            void StopBot()
            {
                if (bot != null)
                    Object.Destroy(bot);
                bot = null;
                input.ReleaseAll();
                Time.timeScale = 1f;
            }

            /// <summary>Level n loaded directly, the bot playing until enough volleys were compared (up to 3 tries).</summary>
            IEnumerator AimGuideVolleys(int n, string phase, string beat)
            {
                for (int attempt = 1; attempt <= 3 && !Run.Done; attempt++)
                {
                    Run.Event("shortcut", $"load level {n} directly ({phase}, try {attempt})");
                    StopBot();
                    var old = GameplayWorld.Instance;
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                    // The same level may be running already: wait for the new world.
                    yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(n), 60f, $"level {n} to start");
                    if (Run.Done)
                        yield break;
                    var controller = GameplayWorld.Instance.PlayerController;
                    BeginAimPhase(phase, 1 + Mathf.Max(0, controller.ExtraShots));
                    Run.Event("aim", $"{phase}: {controller.ExtraShots} extra shot(s), {controller.ShotSpreadDegrees:0.#}° apart");
                    yield return PlayLevel(n, 90f, until: () => aimPhaseVolleys >= SickOfItPlaytest.AimGuideVolleys);
                    if (Run.Done)
                        yield break;
                    if (aimPhaseVolleys >= SickOfItPlaytest.AimGuideVolleys || attempt == 3)
                    {
                        EndAimPhase(beat);
                        yield break;
                    }
                    Run.Event("aim", $"{phase}: only {aimPhaseVolleys} volley(s) before the level stopped ({lastOutcome}); again");
                }
            }

            /// <summary>The stick held: the guide shows along it, short, thin and under the gameplay; one line per shot.</summary>
            IEnumerator AimGuideStick(string phase, int shots)
            {
                var world = GameplayWorld.Instance;
                var guide = world != null ? world.AimGuide : null;
                if (guide == null)
                {
                    AimCheck(phase + " stick", false, "the level has no aim guide (player.json aimGuide.enabled off?)");
                    yield break;
                }
                StopBot();
                Vector2 stick = new Vector2(0.8f, 0.6f);
                aimPhase = phase + " stick";
                aimExpectShots = shots;
                aimPhaseVolleys = aimPhaseBad = aimPhaseLogged = 0;
                aimWorstDelta = 0f;
                input.SetAim(stick);
                yield return WaitReal(0.6f);
                if (Run.Done) yield break;
                var dirs = guide.Directions;
                int centre = dirs.Count % 2 == 1 ? dirs.Count / 2 : -1;
                float along = centre >= 0 ? Vector2.Angle(dirs[centre], stick) : float.NaN;
                AimCheck(phase + " stick shows", guide.Visible && dirs.Count == shots && (centre < 0 || along <= AimGuideToleranceDegrees),
                    $"stick at {Degrees(stick):0.0}°: {(guide.Visible ? "shown" : "HIDDEN")}, {dirs.Count} line(s) {Angles(dirs)} (want {shots})" +
                    (centre >= 0 ? $", the middle one Δ {along:0.00}° from the stick" : ""));
                yield return WaitReal(1.2f);
                if (Run.Done) yield break;
                EndAimPhase(phase + " stick shots");
                aimPhase = "";

                // Short and thin: a fraction of the screen.
                var cam = world.Camera != null ? world.Camera : Camera.main;
                float shortSide = cam != null && cam.orthographic ? Mathf.Min(2f * cam.orthographicSize, 2f * cam.orthographicSize * cam.aspect) : 10f;
                float fraction = guide.Length / shortSide;
                AimCheck(phase + " short", fraction > 0.03f && fraction <= 0.3f,
                    $"each line {guide.Length:0.00} units, {fraction:P0} of the screen's short side");

                // Under every bullet, hazard, enemy and pickup in the gameplay band.
                int lowest = Math.Min(SpriteLayers.Projectile, SpriteLayers.Character);
                string lowestWhat = "bullets and characters";
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || e.IsPlayer)
                        continue;
                    foreach (var r in e.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (r.sortingLayerName == guide.SortingLayerName && r.sortingOrder < lowest)
                        {
                            lowest = r.sortingOrder;
                            lowestWhat = e.DefId;
                        }
                    }
                }
                AimCheck(phase + " under", guide.SortingLayerName == SpriteLayer.Player.ToString() && guide.SortingOrder < lowest,
                    $"lines on {guide.SortingLayerName} order {guide.SortingOrder}; the lowest bullet, hazard or enemy there is order {lowest} ({lowestWhat})");

                if (shots > 1 && dirs.Count == shots)
                {
                    var cfg = GameDefinition.Instance.player.aimGuide;
                    AimCheck(phase + " faint", cfg.extraShotAlpha <= cfg.alpha && cfg.alpha <= 0.6f,
                        $"main line alpha {cfg.alpha:0.00}, each extra shot's {cfg.extraShotAlpha:0.00}");
                }
            }

            /// <summary>The stick held through a rewind and the pause: hidden for both, back after.</summary>
            IEnumerator AimGuideHidden()
            {
                var world = GameplayWorld.Instance;
                var guide = world.AimGuide;
                var clock = GameClock.Instance;
                input.SetAim(new Vector2(0.8f, 0.6f));
                yield return WaitReal(0.4f);
                Run.Event("rewind", "hold R for 1.5 s with the stick held");
                input.SetKey(Key.R, true);
                int rewindFrames = 0, shownWhileRewinding = 0;
                float end = Time.realtimeSinceStartup + 1.5f;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                {
                    yield return new WaitForEndOfFrame();
                    if (clock != null && clock.IsRewinding)
                    {
                        rewindFrames++;
                        if (guide.Visible)
                            shownWhileRewinding++;
                    }
                }
                input.SetKey(Key.R, false);
                AimCheck("rewind hidden", rewindFrames > 0 && shownWhileRewinding == 0,
                    rewindFrames == 0 ? "R held 1.5 s but no rewind ran (out of juice?)" : $"{rewindFrames} rewinding frames, the guide shown on {shownWhileRewinding}");
                yield return WaitReal(0.6f);
                if (Run.Done) yield break;
                AimCheck("after rewind", guide.Visible, $"stick still held after the rewind: guide {(guide.Visible ? "back" : "STILL HIDDEN")}");

                yield return Tap(Key.P);
                yield return WaitReal(0.4f);
                bool paused = world.Hud != null && world.Hud.Paused;
                AimCheck("pause hidden", paused && !guide.Visible, $"pause {(paused ? "open" : "NOT OPEN")}, guide {(guide.Visible ? "SHOWN" : "hidden")}");
                yield return Tap(Key.P);
                yield return WaitReal(0.4f);
                input.SetAim(Vector2.zero);
                yield return WaitReal(0.2f);
            }

            /// <summary>The mouse: hovering shows the line toward the cursor, a click fires along it.</summary>
            IEnumerator AimGuideMouse(string phase, int shots)
            {
                var world = GameplayWorld.Instance;
                var guide = world.AimGuide;
                var cam = world.Camera != null ? world.Camera : Camera.main;
                if (cam == null)
                    yield break;
                input.SetAim(Vector2.zero);
                Vector2 hero = cam.WorldToScreenPoint(world.Player.transform.position);
                Vector2 at = hero + new Vector2(-Screen.height * 0.18f, Screen.height * 0.14f);
                at.x = Mathf.Clamp(at.x, 20f, Screen.width - 20f);
                at.y = Mathf.Clamp(at.y, 20f, Screen.height - 20f);
                input.MoveMouse(at - new Vector2(4f, 0f));
                yield return null;
                input.MoveMouse(at);
                yield return WaitReal(0.4f);
                if (Run.Done) yield break;
                var dirs = guide.Directions;
                float depth = Mathf.Abs(world.Player.transform.position.z - cam.transform.position.z);
                Vector2 cursor = cam.ScreenToWorldPoint(new Vector3(at.x, at.y, depth));
                int centre = dirs.Count % 2 == 1 ? dirs.Count / 2 : -1;
                float toCursor = centre >= 0 ? Vector2.Angle(dirs[centre], cursor - guide.Muzzle) : float.NaN;
                AimCheck(phase + " mouse hover", guide.Visible && dirs.Count == shots && (centre < 0 || toCursor <= 1f),
                    $"cursor at ({cursor.x:0.00}, {cursor.y:0.00}), not clicking: guide {(guide.Visible ? "shown" : "HIDDEN")}, {dirs.Count} line(s) {Angles(dirs)}" +
                    (centre >= 0 ? $", the middle one Δ {toCursor:0.00}° from the muzzle-to-cursor line" : ""));
                aimPhase = phase + " mouse";
                aimExpectShots = shots;
                aimPhaseVolleys = aimPhaseBad = aimPhaseLogged = 0;
                aimWorstDelta = 0f;
                input.SetMouseButton(true);
                yield return WaitReal(1f);
                input.SetMouseButton(false);
                EndAimPhase(phase + " mouse shots");
                yield return WaitReal(0.2f);
            }

            /// <summary>
            /// The aim guide (player.json aimGuide): its lines must be where the shots go, with and without a spread
            /// Grudge, and hidden when he can't fire (a rewind, the pause, death, a scene that has him).
            /// </summary>
            public IEnumerator AimGuideRun()
            {
                int n = AimGuideLevel;
                int ownedSpread = UpgradeProgress.LevelOf(AimGuideSpread);
                AimGuide.VolleyFired += OnAimVolley;
                try
                {
                    // Setup shortcut: no spread Grudge for the first half.
                    while (UpgradeProgress.Remove(AimGuideSpread)) { }
                    Run.Event("shortcut", $"spread Grudge \"{AimGuideSpread}\" removed for the single-shot half (owned {ownedSpread})");
                    yield return AimGuideVolleys(n, "single", "single shot");
                    if (Run.Done) yield break;
                    int shots = 1 + Mathf.Max(0, GameplayWorld.Instance.PlayerController.ExtraShots);
                    yield return AimGuideStick("single", shots);
                    if (Run.Done) yield break;
                    yield return AimGuideHidden();
                    if (Run.Done) yield break;
                    yield return AimGuideMouse("single", shots);
                    if (Run.Done) yield break;
                    Run.Progress(0.4f);

                    for (int i = 0; i < AimGuideSpreadLevels; i++)
                        UpgradeProgress.Add(AimGuideSpread);
                    Run.Event("shortcut", $"spread Grudge \"{AimGuideSpread}\" given {AimGuideSpreadLevels} levels");
                    yield return AimGuideVolleys(n, "spread", "spread shots");
                    if (Run.Done) yield break;
                    shots = 1 + Mathf.Max(0, GameplayWorld.Instance.PlayerController.ExtraShots);
                    AimCheck("spread upgrade", shots == 1 + AimGuideSpreadLevels, $"{shots} shots per volley with {AimGuideSpreadLevels} levels of {AimGuideSpread}");
                    yield return AimGuideStick("spread", shots);
                    if (Run.Done) yield break;
                    yield return AimGuideMouse("spread", shots);
                    if (Run.Done) yield break;
                    Run.Progress(0.65f);

                    // Death: the bot plays to lose; once he is dead the stick is held and the guide must stay hidden.
                    // A coroutine runs before LateUpdate, so Visible is last frame's: a stretch's first frame isn't counted.
                    int deadFrames = 0, shownDead = 0;
                    bool wasDead = false;
                    var health = GameplayWorld.Instance.Player.GetComponentInChildren<PlayerHealth>();
                    reckless = true;
                    try
                    {
                        yield return PlayLevel(n, 150f, now =>
                        {
                            bool dead = health != null && !health.IsAlive;
                            bool counted = dead && wasDead;
                            wasDead = dead;
                            if (!dead)
                                return null;
                            if (bot != null && bot.enabled)
                                bot.enabled = false;
                            input.SetAim(new Vector2(0.8f, 0.6f));
                            var g = GameplayWorld.Instance != null ? GameplayWorld.Instance.AimGuide : null;
                            if (!counted)
                                return null;
                            deadFrames++;
                            if (g != null && g.Visible)
                                shownDead++;
                            return null;
                        }, waitOutDeath: true, until: () => deadFrames >= 90);
                    }
                    finally { reckless = false; }
                    if (Run.Done) yield break;
                    AimCheck("death hidden", deadFrames > 0 && shownDead == 0,
                        deadFrames == 0 ? $"the hero never died ({lastOutcome})" : $"{deadFrames} frames dead with the stick held, the guide shown on {shownDead}");
                    StopBot();
                    Run.Progress(0.8f);

                    // A scene that has the hero (input locked, walked, pulled): the stick held, the guide hidden.
                    int lockedFrames = 0, shownLocked = 0;
                    bool wasLocked = false;
                    LevelRuntime.PendingScene = CutsceneScene;
                    Run.Event("shortcut", $"load level {CutsceneLevel}'s stage with the scene {CutsceneScene} directly");
                    var oldWorld = GameplayWorld.Instance;
                    var load = LoadLevel(CutsceneLevel);
                    if (load != null)
                        yield return load;
                    yield return Run.Until(() => GameplayWorld.Instance != oldWorld && LevelRunning(CutsceneLevel), 60f, $"level {CutsceneLevel} with the scene to start");
                    if (Run.Done) yield break;
                    yield return PlayLevel(CutsceneLevel, 40f, now =>
                    {
                        if (bot != null && bot.enabled)
                            bot.enabled = false;
                        input.SetAim(new Vector2(0.8f, 0.6f));
                        var world = GameplayWorld.Instance;
                        var c = world != null ? world.PlayerController : null;
                        bool locked = c != null && (c.InputLocked || c.ScriptedMove.HasValue || c.Suspended);
                        bool count = locked && wasLocked;
                        wasLocked = locked;
                        if (count)
                        {
                            lockedFrames++;
                            if (world.AimGuide != null && world.AimGuide.Visible)
                                shownLocked++;
                        }
                        return null;
                    }, until: () => lockedFrames >= 180);
                    LevelRuntime.PendingScene = null;
                    if (Run.Done) yield break;
                    AimCheck("scene hidden", lockedFrames > 0 && shownLocked == 0,
                        lockedFrames == 0 ? $"the scene never took the hero ({lastOutcome})" : $"{lockedFrames} frames the scene had the hero with the stick held, the guide shown on {shownLocked}");
                }
                finally
                {
                    AimGuide.VolleyFired -= OnAimVolley;
                    aimPhase = "";
                    LevelRuntime.PendingScene = null;
                    while (UpgradeProgress.Remove(AimGuideSpread)) { }
                    for (int i = 0; i < ownedSpread; i++)
                        UpgradeProgress.Add(AimGuideSpread);
                }
                Run.Metric("aimGuide.passed", aimPassed);
                Run.Metric("aimGuide.failed", aimFailed);
                Run.Progress(1f);
                End(aimFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            // ─── analytics: the events going out, and the death screen's feedback card ─────

            int analyticsPassed, analyticsFailed;

            void AnalyticsCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    analyticsPassed++;
                    Run.Event("check", $"analytics {beat}: ok ({detail})");
                }
                else
                {
                    analyticsFailed++;
                    Run.Event("check", $"analytics {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"analytics {beat}: {detail}");
                }
            }

            void OnAnalytics(string name, string props) => Run.Event("analytics", name + " " + props);

            static int CountSent(string name, string contains = null)
            {
                int n = 0;
                foreach (var line in AnalyticsManager.Recent)
                    if (line.StartsWith(name + " ", StringComparison.Ordinal) && (contains == null || line.IndexOf(contains, StringComparison.Ordinal) >= 0))
                        n++;
                return n;
            }

            IEnumerator HoldRewind(float seconds)
            {
                Run.Event("rewind", $"hold R for {seconds:0.0} s");
                input.SetKey(Key.R, true);
                float end = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                    yield return null;
                input.SetKey(Key.R, false);
                yield return null;
            }

            public IEnumerator AnalyticsRun()
            {
                AnalyticsManager.Sent += OnAnalytics;
                try
                {
                    AnalyticsManager.EnableDebug();
                    Run.Event("analytics", AnalyticsManager.Live ? "the SDK's debug log is on (as ?hawkes-analytics-debug=1)" : "not WebGL: nothing is sent, the game's own record of the calls is checked");
                    yield return ToMainMenu();
                    if (Run.Done) yield break;
                    AnalyticsCheck("ready", CountSent("game_ready") > 0 && AnalyticsManager.CurrentScreen == "menu", $"game_ready sent, screen \"{AnalyticsManager.CurrentScreen}\"");

                    int n = AnalyticsLevel;
                    Run.Event("shortcut", $"load level {n} directly");
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                    for (int death = 1; death <= DeathRuntime.AskAfterDeaths && !Run.Done; death++)
                    {
                        bool rewound = death > 1;
                        reckless = true;
                        try
                        {
                            // The first life holds R once (a rewind), then the bot plays to lose.
                            yield return PlayLevel(n, 0f, now =>
                            {
                                if (rewound || now < 3f)
                                    return null;
                                rewound = true;
                                return HoldRewind(1.2f);
                            }, (death - 1) / 3f * 0.8f, death / 3f * 0.8f, 200f, waitOutDeath: true);
                        }
                        finally
                        {
                            reckless = false;
                        }
                        if (Run.Done) yield break;
                        if (lastOutcome != "died")
                        {
                            AnalyticsCheck("death" + death, false, $"the bot couldn't die on level {n} ({lastOutcome})");
                            End(PlaytestOutcome.Done);
                            yield break;
                        }
                        yield return Run.Until(() => DeathRuntime.Current != null, 30f, "the death screen");
                        if (Run.Done) yield break;
                        Run.Scene("Death");
                        var screen = DeathRuntime.Current;
                        yield return WaitReal(2.5f);
                        if (Run.Done) yield break;
                        bool third = death >= DeathRuntime.AskAfterDeaths;
                        AnalyticsCheck("death" + death, screen != null && screen.DeathsOnLevel == death && CountSent("death") >= death,
                            $"death screen {death}: {(screen != null ? screen.DeathsOnLevel : -1)} deaths counted on level {n}, {CountSent("death")} death events");
                        AnalyticsCheck(third ? "feedbackPrompt" : "noPromptYet" + death, screen != null && screen.AskedFeedback == third && CountSent("ask") == (third ? 1 : 0)
                                && (!third || !AnalyticsManager.Live || AnalyticsManager.AskOpen),
                            third ? $"the card \"Too hard? Not fun?\" is {(AnalyticsManager.Live ? (AnalyticsManager.AskOpen ? "on the page" : "NOT on the page") : "asked for (not WebGL: no page)")}"
                                : $"no card before the third death ({CountSent("ask")} asked)");
                        if (third)
                        {
                            // Long enough to see it in the recording; the death screen holds while it's open.
                            yield return WaitReal(6f);
                            if (Run.Done) yield break;
                            bool held = DeathRuntime.Current == screen && screen != null;
                            AnalyticsCheck("promptHolds", held, held ? "the death screen waits while the card is open" : "the death screen left with the card up");
                        }
                        yield return Tap(Key.Escape);
                        Run.Event("skip", "death animation");
                        if (third)
                        {
                            yield return WaitReal(2f);
                            yield return Tap(Key.Escape);
                            Run.Event("skip", "the feedback card (Escape goes on without an answer)");
                        }
                        yield return Run.Until(() => LevelRunning(n), 45f, $"level {n} again after the death screen");
                        if (Run.Done) yield break;
                    }

                    // What went out.
                    AnalyticsCheck("levelStart", CountSent("level_start") >= DeathRuntime.AskAfterDeaths + 1, $"{CountSent("level_start")} level_start");
                    AnalyticsCheck("deathCause", CountSent("death", "\"cause\":\"unknown\"") < CountSent("death"), $"{CountSent("death")} death events, {CountSent("death", "\"cause\":\"unknown\"")} without a cause");
                    AnalyticsCheck("rewind", CountSent("feature", "\"name\":\"rewind\"") >= 1, $"{CountSent("feature", "\"name\":\"rewind\"")} rewind features");
                    AnalyticsCheck("levelEnd", CountSent("level_end", "\"result\":\"death\"") >= DeathRuntime.AskAfterDeaths, $"{CountSent("level_end", "\"result\":\"death\"")} level_end (death)");
                    if (AnalyticsManager.Live)
                    {
                        AnalyticsManager.Flush();
                        yield return WaitReal(1.5f);
                        string json = AnalyticsManager.DebugInfo();
                        Run.Event("analytics", "debug log: " + (json.Length > 600 ? json.Substring(0, 600) + "..." : json));
                        try
                        {
                            var info = Newtonsoft.Json.Linq.JObject.Parse(string.IsNullOrEmpty(json) ? "{}" : json);
                            bool sdk = info.Value<bool?>("sdk") == true;
                            var config = info["config"] as Newtonsoft.Json.Linq.JObject;
                            string version = config?["build"]?["version"]?.ToString() ?? "";
                            string commit = config?["build"]?["commit"]?.ToString() ?? "";
                            string env = config?["env"]?.ToString() ?? "";
                            string product = config?["product"]?.ToString() ?? "";
                            AnalyticsCheck("sdk", sdk && product == AnalyticsManager.Product, $"window.hawkesAnalytics {(sdk ? "loaded" : "missing")}, product \"{product}\"");
                            AnalyticsCheck("build", version.Length > 0, $"build {version}{(commit.Length > 0 ? " (" + commit + ")" : "")}, env {env}");
                            Run.Metric("analytics.env." + (env.Length > 0 ? env : "none"), 1);
                            var events = info["events"] as Newtonsoft.Json.Linq.JArray;
                            foreach (var name in new[] { "level_start", "death", "feature", "level_end", "feedback" })
                            {
                                int seen = 0, sent = 0;
                                if (events != null)
                                    foreach (var e in events)
                                        if (e["name"]?.ToString() == name && (name != "feature" || e["props"]?["name"]?.ToString() == "rewind"))
                                        {
                                            seen++;
                                            if (e.Value<bool?>("sent") == true) sent++;
                                        }
                                if (name == "feedback")
                                    Run.Event("analytics", $"{seen} feedback answers (the bot doesn't answer the card)");
                                else
                                    AnalyticsCheck("sent." + name, sent > 0, $"{seen} {(name == "feature" ? "rewind" : name)} in the debug log, {sent} sent");
                            }
                        }
                        catch (Exception e)
                        {
                            AnalyticsCheck("debugLog", false, "the SDK's debug log couldn't be read: " + e.Message);
                        }
                    }
                    Run.Metric("analytics.checksFailed", analyticsFailed);
                    Run.Progress(1f);
                    End(analyticsFailed == 0 ? PlaytestOutcome.Done : PlaytestOutcome.Lost);
                }
                finally
                {
                    AnalyticsManager.Sent -= OnAnalytics;
                }
            }

            // ─── magma-king, spire-fiend: a Warden seen up close ─────────────────

            /// <summary>What the magma-king / spire-fiend scenario (or a Warden's level-n) has seen of the Warden (across tries).</summary>
            sealed class MagmaWatch
            {
                public readonly WardenFight W;
                public MagmaWatch(WardenFight w) => W = w;
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
                    Run.Event("check", $"{magma.W.Scenario} {beat}: ok ({detail})");
                }
                else
                {
                    magma.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"{magma.W.Scenario} {beat}: {detail}");
                }
            }

            /// <summary>
            /// Plays the level with a Warden's fight through real input, up to 3 tries, watching him (animation states, size on
            /// screen) and the lines; then checks what the fight must show. With arenaChecks (spire-fiend) every try is also
            /// watched as a Warden's arena (the voiced intro, the phases in order, the telegraphs, the adds, the defeat and the
            /// Waystone: FinishArena), and a won fight goes on through the results to the Shaft's climb (the Waystone saved and
            /// lit, the Circle above waking) and what comes next.
            /// </summary>
            public IEnumerator WardenRun(WardenFight w, bool arenaChecks)
            {
                var definition = GameDefinition.Instance;
                int n = definition.levels.FindIndex(l => l != null && l.levelName == w.Level) + 1;
                if (n <= 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"{w.Scenario}: no level \"{w.Level}\" in game.json");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                magma = new MagmaWatch(w);
                stageChecksFailed = 0;
                for (int attempt = 1; attempt <= 3 && !Run.Done; attempt++)
                {
                    Run.Event("shortcut", $"load level {n} ({w.Level}) directly, try {attempt}");
                    var load = LoadLevel(n);
                    if (load != null)
                        yield return load;
                    stage = arenaChecks && IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                    float share = arenaChecks ? 0.85f : 1f;
                    yield return PlayLevel(n, 0f, stage != null ? StageDuring : MagmaDuring, share * (attempt - 1) / 3f, share * attempt / 3f);
                    if (Run.Done)
                        yield break;
                    if (lastOutcome == "completed")
                        break;
                    if (stage != null && attempt < 3)
                    {
                        Run.Event("retry", $"level {n}: {lastOutcome} on try {attempt}; the arena is checked on the last try");
                        stage = null;
                    }
                }
                FinishMagma();
                var won = lastOutcome;
                if (stage != null)
                {
                    FinishStage(won);
                    if (won == "completed" && LoadLevel == DefaultLoadLevel)
                        yield return StageAfter(n, StageCheck);
                    stage = null;
                    if (Run.Done)
                        yield break;
                }
                magma = null;
                Run.Progress(1f);
                // A failed check is a Bug problem (MagmaCheck, StageCheck); the outcome is how the fight went.
                End(won == "completed" ? PlaytestOutcome.Won
                    : won == "softlock" ? PlaytestOutcome.Stuck
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
                    if (e != null && e.IsAlive && e.DefId == m.W.Id)
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
                        Run.Event("boss", $"the {m.W.Name} appears at {now:0.0} s ({(m.Art ? "generated sheets" : "NO ART: placeholder")})");
                    }
                    string state = king.Animation != null ? king.Animation.CurrentState : "";
                    if (state != m.LastState && state.Length > 0)
                    {
                        m.LastState = state;
                        if (!m.States.ContainsKey(state))
                            m.States[state] = now;
                        Run.Event("anim", $"{m.W.Name}: {state} at {now:0.0} s");
                    }
                    if (m.ScreenShare < 0f && king.Sprite != null && king.Sprite.sprite != null && world.Camera != null && world.Camera.orthographic)
                    {
                        m.ScreenShare = king.Sprite.bounds.size.y / (2f * world.Camera.orthographicSize);
                        Run.Metric(m.W.Id + ".screenShare", m.ScreenShare);
                        Run.Event("boss", $"the {m.W.Name} is {king.Sprite.bounds.size.y:0.00} units tall on screen, {m.ScreenShare:P0} of its height");
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
                foreach (var l in magma.W.Lines)
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
                Run.Event("anim", $"{magma.W.Name}: Die at {magma.Clock:0.0} s ({magma.DieDetail})");
            }

            void FinishMagma()
            {
                var m = magma;
                MagmaCheck("appears", m.Seen, m.Seen ? "the boss phase spawned him" : $"he never appeared (last outcome {lastOutcome})");
                MagmaCheck("art", m.Art, m.Art ? "drawn from his generated sheets" : "no generated sheets: he is a placeholder");
                MagmaCheck("size", m.ScreenShare >= 0.3f, $"{(m.ScreenShare < 0f ? "never measured" : m.ScreenShare.ToString("P0"))} of the screen's height (want 30%+)");
                foreach (string state in m.W.States)
                    MagmaCheck("anim " + state, m.States.ContainsKey(state),
                        m.States.TryGetValue(state, out float at) ? $"first seen at {at:0.0} s" : "never played");
                MagmaCheck("anim Die", m.DiePlayed, m.DieDetail);
                foreach (var l in m.W.Lines)
                    MagmaCheck("line " + l.beat, m.Lines.TryGetValue(l.beat, out string why) && why.Length == 0,
                        !m.Lines.ContainsKey(l.beat) ? $"\"{l.text}\" never shown" : why.Length == 0 ? $"\"{l.text}\" shown, voiced by {l.voice}" : why);
                Run.Metric(m.W.Id + ".passed", m.Passed.Count);
                Run.Metric(m.W.Id + ".failed", m.Failed);
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

            /// <summary>
            /// sound-audit: every sound in audio.json must have a clip (the same rule as Tools/Audio/sound_audit.py --check and
            /// the SoundClipTests EditMode test), then one of each kind is played through Sfx: an enemy's hurt, death and attack,
            /// a weapon's equip and empty click, a hero hint and a Yooba shop line. Presentation only.
            /// </summary>
            public IEnumerator SoundAudit()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                var sounds = GameDefinition.Instance != null && GameDefinition.Instance.audio != null ? GameDefinition.Instance.audio.sounds : null;
                if (sounds == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "sound-audit: no audio config loaded");
                    End(PlaytestOutcome.Done);
                    yield break;
                }
                int missing = 0;
                foreach (var sound in sounds)
                {
                    if (sound == null || Sfx.Has(sound.id))
                        continue;
                    missing++;
                    Run.Problem(PlaytestSeverity.Bug, $"sound-audit: \"{sound.id}\" has no clip");
                }
                Run.Metric("soundAudit.sounds", sounds.Count);
                Run.Metric("soundAudit.missing", missing);
                Run.Event("check", $"every sound has a clip: {(missing == 0 ? "ok" : "FAILED")} ({sounds.Count - missing} of {sounds.Count})");
                var samples = new[]
                {
                    "imp_hurt", "imp_die", "imp_attack", "brimstoneDemon_attack", "watcher_die", "magmaKing_hurt", "spireFiend_die",
                    "wpn_equip_shotgun", "wpn_empty_shotgun", "wpn_equip_fists", "vo_hero_hint_imp", "vo_hero_hint_watcher",
                    "vo_yooba_buy_plaster", "vo_yooba_quest_offer", "vo_mk_defeat",
                };
                for (int i = 0; i < samples.Length && !Run.Done; i++)
                {
                    string id = samples[i];
                    Run.Progress((float)i / samples.Length);
                    if (!Sfx.Has(id))
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"sound-audit: \"{id}\" has no clip");
                        continue;
                    }
                    float length = Sfx.LengthOf(id);
                    Run.Event("sound", $"{id} ({length:0.0} s)");
                    Sfx.Play(id);
                    yield return WaitReal(length + 0.4f);
                }
                if (Run.Done) yield break;
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

            /// <summary>The distinct sheet frames seen for one clip.</summary>
            sealed class FrameSet : HashSet<string> { }

            /// <summary>Frame sets by clip name.</summary>
            sealed class FrameBook : Dictionary<string, FrameSet> { }

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
                // The background's fire hole: where Level 1's footage puts it (Spot), and what the screen showed there
                // before the beam (the background's own fire) and with the portal open.
                public Vector2 Spot;
                public bool SpotKnown;
                public float BgFireX = float.NaN;
                public float BgFireDensity;
                public bool CaptureBefore;
                public bool CaptureAfter;
                public float TelegraphAt = -1f;
                public float InputTryAt = -1f;
                public float LastAngle;
                public float Spun;
                public float BiggestScale;
                // The hero's scene clips (Curious, Shout, Grabbed): the one the scene just asked for, the distinct
                // sheet frames seen on screen for each, and frames where something else was showing.
                public string Anim;
                public readonly FrameBook AnimFrames = new FrameBook();
                public readonly Dictionary<string, int> AnimMisses = new Dictionary<string, int>();
                public string AnimOther;
                // The portal's and the beam's art (Assets/Art/Effects): the distinct sheet frames seen on screen per
                // "<entity>.<state>", how often the portal's Loop wrapped round, and entities drawn without art.
                public readonly FrameBook FxFrames = new FrameBook();
                public int LoopWraps;
                public int LastLoopFrame = -1;
                public readonly HashSet<string> FxNoArt = new HashSet<string>();
            }

            PortalWatch portal;

            /// <summary>The beats the scene must pass, in order.</summary>
            static readonly string[] PortalBeats =
            {
                "pickFirst", "locked", "bgFire", "telegraph", "beamCharge", "beamHit", "beamColumn", "impactBurst", "portalOpen", "onSpot", "overFire", "inputIgnored", "portalTearOpen",
                "walkArrived", "curiousAnim", "shoutAnim", "lines", "grabbedAnim", "portalSwirl", "pulledIn", "yooba", "slurped", "portalFlare",
                "closed", "black", "untouched", "levelComplete", "video2",
            };

            /// <summary>The scene's spoken lines (Level1.json): the signal emitted with each, its subtitle and its voice.</summary>
            static readonly (string signal, string text, string sound)[] PortalLines =
            {
                ("portal:beamHit", "Mm. ... Stay.", "vo_portal_overlord_stay"),
                ("portal:line:hole", "That's a hole.", "vo_portal_hole_1"),
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
                    portal.FxFrames[key] = seen = new FrameSet();
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

            /// <summary>
            /// The fire hole in Level 1's background (Level1_Background3's laser strike, Level1_Background4's aftermath fire),
            /// as a fraction of the 1920x1080 footage from its top left: the ring's centre on the ground line.
            /// </summary>
            const float BgFireU = 930f / 1920f, BgFireV = 1025f / 1080f;
            /// <summary>How far a portal effect may sit from the background's fire hole (world units).</summary>
            const float SpotTolerance = 0.15f;

            /// <summary>The background's fire hole in world space, from the background layer on screen (placed and scaled on the camera).</summary>
            static bool BackgroundFireSpot(out Vector2 spot)
            {
                spot = default;
                foreach (var frames in Object.FindObjectsByType<SpriteFramePlayer>(FindObjectsSortMode.None))
                {
                    if (!frames.name.StartsWith("Level1_Background", StringComparison.Ordinal) ||
                        !frames.TryGetComponent(out SpriteRenderer renderer) || !renderer.enabled || renderer.sprite == null)
                        continue;
                    var b = renderer.bounds;
                    spot = new Vector2(b.min.x + BgFireU * b.size.x, b.max.y - BgFireV * b.size.y);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// A screen capture of the box around <paramref name="around"/> (x +-1, from 0.1 to 0.9 above it, in world units; the
            /// HUD's progress bar along the screen's bottom left out): how much of it is fire-orange (per square world unit) and
            /// where that fire is (world x). The background's own fire is a small flame; the game's portal is a big burning ring.
            /// </summary>
            static bool ScanFire(Vector2 around, out float density, out float fireX, out int pixels)
            {
                density = 0f;
                fireX = float.NaN;
                pixels = 0;
                var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
                if (camera == null || !camera.orthographic)
                    return false;
                Vector3 lo = camera.WorldToScreenPoint(new Vector3(around.x - 1f, around.y + 0.1f, 0f));
                Vector3 hi = camera.WorldToScreenPoint(new Vector3(around.x + 1f, around.y + 0.9f, 0f));
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    int bar = Mathf.CeilToInt(shot.height * 75f / 1080f);
                    int x0 = Mathf.Clamp(Mathf.RoundToInt(lo.x), 0, shot.width - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(hi.x), 0, shot.width - 1);
                    int y0 = Mathf.Clamp(Mathf.RoundToInt(lo.y), bar, shot.height - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(hi.y), 0, shot.height - 1);
                    if (x1 <= x0 || y1 <= y0)
                        return false;
                    var colors = shot.GetPixels32();
                    long sumX = 0;
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var c = colors[y * shot.width + x];
                        if (c.r > 200 && c.g > 70 && c.g < 190 && c.b < 110 && c.r - c.g > 50)
                        {
                            pixels++;
                            sumX += x;
                        }
                    }
                    float perUnit = shot.height / (2f * camera.orthographicSize);
                    density = pixels / (perUnit * perUnit);
                    if (pixels > 0)
                        fireX = camera.ScreenToWorldPoint(new Vector3((float)sumX / pixels, 0f, -camera.transform.position.z)).x;
                    return true;
                }
                finally
                {
                    Object.Destroy(shot);
                }
            }

            /// <summary>Before the beam: the background's own fire must be on screen where the footage puts it.</summary>
            IEnumerator CaptureBackgroundFire()
            {
                yield return new WaitForEndOfFrame();
                if (portal == null || !portal.SpotKnown)
                    yield break;
                if (!ScanFire(portal.Spot, out float density, out float fireX, out int pixels))
                {
                    PortalCheck("bgFire", false, "no screen capture of the background's fire hole (camera not found or the box is off screen)");
                    yield break;
                }
                portal.BgFireX = fireX;
                portal.BgFireDensity = density;
                Run.Metric("portal.bgFireX", float.IsNaN(fireX) ? -99f : fireX);
                Run.Metric("portal.bgFireDensity", density);
                PortalCheck("bgFire", pixels >= 20 && Mathf.Abs(fireX - portal.Spot.x) < 0.25f,
                    pixels > 0 ? $"screen capture: the background's fire burns at world x={fireX:0.00} ({pixels} fire pixels, {density:0} per unit²), the footage's spot x={portal.Spot.x:0.00}"
                               : $"screen capture: no fire on the background around world x={portal.Spot.x:0.00} (Level1_Background4 not on screen?)");
            }

            /// <summary>The portal open, the camera settled: the capture must show the game's portal over the background's fire.</summary>
            IEnumerator CapturePortalOverFire()
            {
                yield return new WaitForEndOfFrame();
                var world = GameplayWorld.Instance;
                var hole = world != null ? FindLive(world, "portal", "portal") : null;
                if (portal == null || hole == null)
                    yield break;
                Vector2 at = float.IsNaN(portal.BgFireX) ? portal.Spot : new Vector2(portal.BgFireX, portal.Spot.y);
                bool scanned = ScanFire(at, out float density, out float fireX, out int pixels);
                var art = hole.Sprite != null ? hole.Sprite.bounds : new Bounds((Vector3)hole.Position, Vector3.zero);
                bool covers = art.min.x < at.x - 0.3f && art.max.x > at.x + 0.3f && art.min.y <= at.y && art.max.y > at.y + 0.3f;
                float grew = portal.BgFireDensity > 0f ? density / portal.BgFireDensity : 0f;
                Run.Metric("portal.overFireDensity", density);
                PortalCheck("overFire", scanned && covers && grew >= 3f && Mathf.Abs(fireX - at.x) < 0.25f,
                    $"screen capture with the portal open: {pixels} fire pixels ({density:0} per unit², {grew:0.0}x the background's own fire) centred at world x={fireX:0.00}, " +
                    $"the background's fire at x={at.x:0.00}; the portal's art spans x {art.min.x:0.00}..{art.max.x:0.00}, y {art.min.y:0.00}..{art.max.y:0.00}");
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
                        portal.AnimFrames[portal.Anim] = seen = new FrameSet();
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
                            portal.SpotKnown = BackgroundFireSpot(out portal.Spot);
                            if (!portal.SpotKnown)
                                Run.Problem(PlaytestSeverity.Bug, "portal scene: no Level1_Background layer on screen to find the fire hole on");
                            portal.Right = player.Position.x < portal.Spot.x;
                            portal.StartX = player.Position.x;
                            portal.CaptureBefore = portal.SpotKnown;
                            Run.Event("portal", $"the background's fire hole is at world ({portal.Spot.x:0.00}, {portal.Spot.y:0.00}); the hero is {(portal.Right ? "left" : "right")} of it");
                            portal.Health = player.GetComponentInChildren<PlayerHealth>();
                            portal.HealthAtStart = portal.Health != null ? portal.Health.CurrentHealth : 0;
                            bool picker = world.Hud != null && world.Hud.PickerOpen;
                            // First run: the Grudge pick moved to after Floor 1000 (Docs/Story.md "First five minutes").
                            PortalCheck("pickFirst", !portal.Picked && !picker, $"no Grudge before the portal (picked={portal.Picked}, picker open={picker})");
                            bool locked = now < world.InputLockedUntil;
                            PortalCheck("locked", locked && world.RewindLocked,
                                $"input locked={locked}, rewind locked={world.RewindLocked}; hero at x={portal.StartX:0.00}, hp {portal.HealthAtStart}; no keys from the bot from here");
                            break;
                        }
                        case "portal:telegraph":
                        {
                            var glow = FindLive(world, null, "overlordBeamGlow");
                            Vector2 off = glow != null ? glow.Position - portal.Spot : Vector2.zero;
                            portal.GlowX = glow != null ? glow.Position.x : float.NaN;
                            portal.TelegraphAt = now;
                            PortalCheck("telegraph", glow != null && portal.SpotKnown && Mathf.Abs(off.x) <= SpotTolerance && Mathf.Abs(off.y) <= SpotTolerance,
                                glow != null ? $"red glow on the background's fire hole: x={portal.GlowX:0.00}, {off.x:+0.00;-0.00}/{off.y:+0.00;-0.00} from it, the hero at x={player.Position.x:0.00}" : "no overlordBeamGlow on the ground");
                            break;
                        }
                        case "portal:beamHit":
                        {
                            int beams = CountLive(world, "overlordBeam");
                            var column = FindLive(world, null, "overlordBeam");
                            var impact = FindLive(world, null, "overlordBeamImpact");
                            float beamOff = column != null ? Vector2.Distance(column.Position, portal.Spot) : 99f;
                            float impactOff = impact != null ? Vector2.Distance(impact.Position, portal.Spot) : 99f;
                            float warned = portal.TelegraphAt >= 0f ? now - portal.TelegraphAt : 0f;
                            int charge = FxCount("overlordBeamGlow.Charge");
                            PortalCheck("beamCharge", charge >= 6, $"the charge glow gathered on the ground through {charge} different frames");
                            PortalCheck("beamHit", beams >= 1 && warned >= 1f && beamOff <= SpotTolerance && impactOff <= SpotTolerance,
                                $"the beam came down ({beams} column) {warned:0.00} s after the glow, on the background's fire hole (column {beamOff:0.00} and burst {impactOff:0.00} from it)");
                            break;
                        }
                        case "portal:open":
                        {
                            float fromGlow = hole != null ? Mathf.Abs(hole.Position.x - portal.GlowX) : 99f;
                            PortalCheck("portalOpen", hole != null && fromGlow < 0.05f,
                                hole != null ? $"portal open at x={hole.Position.x:0.00}, {fromGlow:0.00} from where the beam hit, the hero at x={player.Position.x:0.00}" : "no portal entity");
                            Vector2 fromSpot = hole != null ? hole.Position - portal.Spot : Vector2.zero;
                            if (hole != null)
                            {
                                Run.Metric("portal.worldX", hole.Position.x);
                                Run.Metric("portal.worldY", hole.Position.y);
                            }
                            PortalCheck("onSpot", hole != null && portal.SpotKnown && Mathf.Abs(fromSpot.x) <= SpotTolerance && Mathf.Abs(fromSpot.y) <= SpotTolerance,
                                hole != null ? $"portal at world x={hole.Position.x:0.00}, y={hole.Position.y:0.00}; the background's fire hole at x={portal.Spot.x:0.00}, y={portal.Spot.y:0.00} " +
                                               $"({fromSpot.x:+0.00;-0.00}/{fromSpot.y:+0.00;-0.00})" : "no portal entity");
                            portal.InputTryAt = now + 0.3f;
                            // The beam has had half a second on screen by now.
                            int column = FxCount("overlordBeam.Column");
                            PortalCheck("beamColumn", column >= 3, $"a burning beam column from the sky, {column} different frames");
                            int burst = FxCount("overlordBeamImpact.Burst");
                            PortalCheck("impactBurst", burst >= 4, $"an impact burst where it hit, {burst} different frames");
                            break;
                        }
                        case "portal:walk":
                            // The zoom and the shakes are over: look at the screen.
                            portal.CaptureAfter = true;
                            break;
                        case "portal:arrived":
                        {
                            int open = FxCount("portal.Open");
                            string state = hole != null && hole.Animation != null ? hole.Animation.CurrentState : "(none)";
                            PortalCheck("portalTearOpen", open >= 10 && state == "Loop",
                                $"the ground tore open through {open} different Open frames (scorch, crack, ring), now {state}");
                            float gap = hole != null ? Mathf.Abs(hole.Position.x - player.Position.x) : 99f;
                            float walked = Mathf.Abs(player.Position.x - portal.StartX);
                            bool locked = now < world.InputLockedUntil;
                            bool ownSide = hole != null && player.Position.x < hole.Position.x == portal.Right;
                            PortalCheck("walkArrived", hole != null && gap > 0.5f && gap < 1.2f && ownSide && !world.Scripted.Walking && locked,
                                $"walked by himself {walked:0.00} from x={portal.StartX:0.00} to {gap:0.00} from the portal's centre, on his side ({(portal.Right ? "left" : "right")}: {ownSide}), input locked={locked}");
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
                            float dx = yooba != null && hole != null ? yooba.Position.x - hole.Position.x : 0f;
                            bool farSide = dx > 0f == portal.Right;
                            PortalCheck("yooba", yooba != null && hole != null && Mathf.Abs(dx) > 2.5f && farSide,
                                yooba != null ? $"a green slime bounces in {dx:+0.00;-0.00} from the portal, from the far side={farSide}" : "no yoobaProp slime");
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
                            var mark = FindLive(world, null, "portalScorch");
                            bool scorch = mark != null;
                            float markOff = scorch ? Vector2.Distance(mark.Position, portal.Spot) : 99f;
                            int flare = FxCount("portal.Close");
                            int marks = FxCount("portalScorch.Scorch");
                            PortalCheck("portalFlare", flare >= 8 && marks >= 1, $"the portal flared shut through {flare} different Close frames to a scorch mark (scorch art={marks >= 1})");
                            PortalCheck("closed", hole == null && scorch && markOff <= SpotTolerance,
                                $"the portal closed (gone={hole == null}) to a scorch (scorch={scorch}) {markOff:0.00} from the background's fire hole");
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
                        string shown = world.Hud != null ? world.Hud.BubbleShown : "";
                        if (string.IsNullOrEmpty(shown) && world.Hud != null)
                            shown = world.Hud.SubtitleShown;
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

                if (portal.CaptureBefore)
                {
                    portal.CaptureBefore = false;
                    return CaptureBackgroundFire();
                }
                if (portal.CaptureAfter)
                {
                    portal.CaptureAfter = false;
                    return CapturePortalOverFire();
                }
                if (portal.InputTryAt >= 0f && now >= portal.InputTryAt)
                {
                    portal.InputTryAt = -1f;
                    // The beat kit (Docs/beats.md) ends a scene on any press, so pressing keys here would skip it:
                    // the lock itself is checked at portal:locked, the skip by the beat-director scenario.
                    Run.Event("input", "portal scene: input stays locked and rewind refused (any press would skip it; scenario beat-director plays that)");
                    PortalCheck("inputIgnored", world != null && world.RewindLocked && now < world.InputLockedUntil, "input and rewind locked during the beat");
                    return null;
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
                                                          "TOAST must lead into it, never straight to the results");
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
                        // The PlayMode playtest loads scenes itself: the Shaft / Video2 after the level isn't part of it.
                        Run.Event("portal", "first-run Shaft plunge not followed (editor scene loader)");
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
                        // First run skips Video2 and plunges on the Shaft (Video2 stays in Rewatch Stuff).
                        yield return Run.Until(() => ShaftScreen.Current != null || Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(2),
                            30f, "the Shaft plunge or Floor 1000 after Level 1");
                        if (Run.Done) yield break;
                        var shaft = ShaftScreen.Current;
                        var video = Object.FindAnyObjectByType<VideoRuntime>();
                        bool firstRunPlunge = shaft != null && shaft.Kind == ShaftScreen.Mode.Plunge;
                        bool floor1000 = LevelRunning(2);
                        int videoNum = PlayerPrefs.GetInt("CurrentVideoNumber", 0);
                        if (firstRunPlunge)
                            Run.Scene("Shaft plunge (first run, Video2 skipped)");
                        else if (video != null)
                            Run.Scene("Video " + videoNum);
                        else
                            Run.Scene("Floor 1000");
                        PortalCheck("video2", firstRunPlunge || floor1000 || videoNum == 2,
                            firstRunPlunge ? "first-run Shaft plunge after the portal (Video2 unlocked for Rewatch)"
                            : floor1000 ? "Floor 1000 after Level 1"
                            : $"video {videoNum} started after the portal scene");
                        // Watch a beat of the plunge / fall for the recording.
                        yield return WaitReal(3f);
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

            // ─── one-way: jump up through platforms, hold down to drop through ───

            /// <summary>What the one-way scenario has seen so far.</summary>
            sealed class OneWayWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public int Failed;
                public int JumpsThrough, Drops;
                public float HoldOnGroundSince = -1f;
                public bool DroppedThroughGround;
                // The dead zone: a brush of S (BrushSeconds) and a run with the stick a little low must not drop him.
                public bool Armed;
                public int BrushTries;
                public float LastDropAt = -1f;
                public bool WasLowRunning;
                public Collider2D LowRunPiece;
            }

            /// <summary>The one-way scenario's brush of down: well under player.json dropThroughHoldSeconds.</summary>
            const float BrushSeconds = 0.07f;
            /// <summary>Seconds of running across one-way platforms with the stick low before "low run stays on" passes.</summary>
            const float LowRunNeeded = 0.4f;

            OneWayWatch oneWay;

            /// <summary>What the one-way scenario must see (acceptance: up through, down through on keyboard and gamepad, the ground holds).</summary>
            static readonly string[] OneWayBeats =
            {
                "brush stays on", "low run stays on", "jump up through", "drop through keyboard", "drop through gamepad", "drop through a stone",
                "bot drops for its path", "ground holds", "level ends",
            };

            public IEnumerator OneWayRun()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(OneWayScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"one-way: there is no scene \"{OneWayScene}\" in content/scenes");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                oneWay = new OneWayWatch();
                Run.Event("shortcut", $"load level {OneWayLevel}'s stage with the test floor {OneWayScene} directly");
                Run.Event("one-way", "touch: the move touch pulled down feeds the same hold-down as S and the stick (player.json mobileDropThreshold, " +
                                     "the same dead zone and hold); the bot has no touchscreen, so try that one on a phone");
                SickOfIt.Core.Input.PlayerController.JumpedUpThrough += OnJumpedUpThrough;
                SickOfIt.Core.Input.PlayerController.DroppedThrough += OnDroppedThrough;
                LevelRuntime.PendingScene = OneWayScene;
                var load = LoadLevel(OneWayLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(OneWayLevel, OneWaySeconds, OneWayDuring, waitOutDeath: true);
                LevelRuntime.PendingScene = null;
                SickOfIt.Core.Input.PlayerController.JumpedUpThrough -= OnJumpedUpThrough;
                SickOfIt.Core.Input.PlayerController.DroppedThrough -= OnDroppedThrough;
                if (Run.Done)
                {
                    oneWay = null;
                    yield break;
                }

                OneWayCheck("level ends", lastOutcome == "completed", $"the level ended: {lastOutcome}");
                int passed = 0;
                foreach (var beat in OneWayBeats)
                {
                    if (oneWay.Passed.Contains(beat))
                        passed++;
                    else
                        Run.Problem(PlaytestSeverity.Bug, $"one-way: never saw \"{beat}\" (level {lastOutcome})");
                }
                Run.Metric("oneWay.jumpsThrough", oneWay.JumpsThrough);
                Run.Metric("oneWay.drops", oneWay.Drops);
                Run.Metric("oneWay.passed", passed);
                Run.Metric("oneWay.failed", oneWay.Failed);
                Run.Progress(1f);
                bool ok = passed == OneWayBeats.Length && oneWay.Failed == 0;
                oneWay = null;
                End(ok ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void OneWayCheck(string beat, bool ok, string detail)
            {
                if (oneWay == null || oneWay.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    oneWay.Passed.Add(beat);
                    Run.Event("check", $"one-way {beat}: ok ({detail})");
                }
                else
                {
                    oneWay.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"one-way {beat}: {detail}");
                }
            }

            static string PieceName(Collider2D piece)
            {
                if (piece == null)
                    return "a platform";
                return piece.TryGetComponent<SickOfIt.Gameplay.Entity>(out var e) ? Who(e) : piece.name;
            }

            void OnJumpedUpThrough(SickOfIt.Core.Input.PlayerController controller, Collider2D piece)
            {
                if (oneWay == null)
                    return;
                oneWay.JumpsThrough++;
                var feet = controller.BodyCollider != null ? controller.BodyCollider.bounds.min.y : controller.transform.position.y;
                Run.Event("one-way", $"jumped up through {PieceName(piece)} from below and landed on top (feet at y={feet:0.00}, its top y={piece.bounds.max.y:0.00})");
                OneWayCheck("jump up through", true, $"the hero jumped up through {PieceName(piece)} from below and stands on top of it");
            }

            void OnDroppedThrough(SickOfIt.Core.Input.PlayerController controller, Collider2D piece)
            {
                if (oneWay == null)
                    return;
                oneWay.Drops++;
                oneWay.LastDropAt = Time.time;
                float heldFor = controller.LastDropHeldSeconds, need = controller.DropHoldSeconds;
                string device = bot != null && bot.DropsStarted > 0 ? bot.LastDropDevice : "?";
                bool landing = piece != null && piece.TryGetComponent<SickOfIt.Gameplay.Entity>(out var e) && e.Tags.Contains("landing");
                bool stone = piece != null && piece.TryGetComponent<SickOfIt.Gameplay.Entity>(out var s) && s.FindAbility<SickOfIt.Gameplay.Content.SinkAbility>() != null;
                Run.Event("one-way", $"held down ({(device == "keyboard" ? "S" : device == "gamepad" ? "gamepad left stick" : device)}) on {PieceName(piece)} " +
                                     $"for {heldFor:0.00} s (a drop needs {need:0.00} s) and dropped through it");
                bool heldLongEnough = heldFor >= need - 0.001f;
                if (landing)
                {
                    oneWay.DroppedThroughGround = true;
                    OneWayCheck("ground holds", false, $"holding down on the landing dropped him through it: the ground must stay solid");
                    return;
                }
                if (device == "keyboard")
                    OneWayCheck("drop through keyboard", heldLongEnough, $"holding S on {PieceName(piece)} for {heldFor:0.00} s (a drop needs {need:0.00} s) dropped him through it");
                if (device == "gamepad")
                    OneWayCheck("drop through gamepad", heldLongEnough, $"holding the gamepad's left stick down on {PieceName(piece)} for {heldFor:0.00} s (a drop needs {need:0.00} s) dropped him through it");
                if (stone)
                    OneWayCheck("drop through a stone", true, $"he dropped through the climb stone {PieceName(piece)}");
                var target = bot != null ? bot.LastDropTarget : null;
                if (target != null && target.MainCollider != null && target.MainCollider.bounds.max.y < piece.bounds.min.y)
                    OneWayCheck("bot drops for its path", true, $"the bot's next foothold, {Who(target)}, was under {PieceName(piece)}, so it dropped through");
            }

            /// <summary>Runs every frame of the one-way test floor: on the landing (solid ground) the bot holds down and he must stay on it.</summary>
            IEnumerator OneWayDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || oneWay == null || bot == null)
                    return null;
                var hero = world.PlayerController;
                if (hero != null)
                    OneWayDeadZone(hero);
                var landing = world.FindById("landing");
                bool onLanding = landing != null && world.PlayerStandsOn(landing);
                if (oneWay.HoldOnGroundSince < 0f)
                {
                    if (onLanding)
                    {
                        oneWay.HoldOnGroundSince = now;
                        bot.holdDown = true;
                        Run.Event("one-way", "on the landing (solid ground): the bot holds S down");
                    }
                }
                else if (!oneWay.Passed.Contains("ground holds") && !oneWay.DroppedThroughGround && now - oneWay.HoldOnGroundSince >= 1.5f)
                {
                    var controller = world.PlayerController;
                    bool grounded = controller != null && controller.IsGrounded;
                    OneWayCheck("ground holds", onLanding && grounded,
                        onLanding && grounded ? "1.5 s of holding down on the landing and he still stands on it"
                                              : "holding down on the landing he left it: the ground must stay solid");
                    bot.holdDown = false;
                }
                return null;
            }

            /// <summary>
            /// The drop-through dead zone, through real input: the bot taps S for a moment on the first one-way platform it
            /// stands on (he must stay on top), and runs across one-way platforms with the gamepad's stick a little low (he
            /// must not drop either). A drop starts only after player.json dropThroughHoldSeconds of deliberate down.
            /// </summary>
            void OneWayDeadZone(SickOfIt.Core.Input.PlayerController controller)
            {
                if (!oneWay.Armed)
                {
                    oneWay.Armed = true;
                    oneWay.BrushTries = 1;
                    bot.brushDown = BrushSeconds;
                    bot.lowRun = true;
                    Run.Event("one-way", $"dead zone: the bot will tap S for {BrushSeconds:0.00} s on a platform (a drop needs {controller.DropHoldSeconds:0.00} s held) " +
                                         $"and run across platforms with the stick at ({PlaytestBot.LowRunStick.x:0.0}, {PlaytestBot.LowRunStick.y:0.0})");
                }

                // The brush: judged a moment after S is let go.
                if (!oneWay.Passed.Contains("brush stays on") && bot.BrushEndedAt >= 0f && bot.BrushEndedAt >= bot.BrushStartedAt
                    && Time.time >= bot.BrushEndedAt + 0.1f && bot.brushDown <= 0f)
                {
                    bool dropped = oneWay.LastDropAt >= bot.BrushStartedAt || (bot.BrushedOn != null && controller.IsDroppingThrough(bot.BrushedOn));
                    bool stillOn = bot.BrushedOn != null && controller.GroundCollider == bot.BrushedOn;
                    float tapped = bot.BrushEndedAt - bot.BrushStartedAt;
                    if (dropped)
                        OneWayCheck("brush stays on", false, $"a {tapped:0.00} s tap of S on {PieceName(bot.BrushedOn)} dropped him through it (a drop needs {controller.DropHoldSeconds:0.00} s held)");
                    else if (stillOn)
                        OneWayCheck("brush stays on", true, $"tapped S for {tapped:0.00} s on {PieceName(bot.BrushedOn)} and he still stands on it (a drop needs {controller.DropHoldSeconds:0.00} s held)");
                    else if (oneWay.BrushTries < 4)
                    {
                        oneWay.BrushTries++;
                        bot.brushDown = BrushSeconds;
                        Run.Event("one-way", $"dead zone: he left {PieceName(bot.BrushedOn)} during the tap without dropping; tapping again on the next platform");
                    }
                    else
                        OneWayCheck("brush stays on", false, $"after {oneWay.BrushTries} taps he never stayed on a platform long enough to judge one");
                }

                // The low run: a drop while running with the stick low is a failure.
                if (oneWay.WasLowRunning && oneWay.LowRunPiece != null && controller.IsDroppingThrough(oneWay.LowRunPiece))
                    OneWayCheck("low run stays on", false, $"running across {PieceName(oneWay.LowRunPiece)} with the stick a little low dropped him through it");
                oneWay.WasLowRunning = bot.LowRunning;
                oneWay.LowRunPiece = bot.LowRunning ? controller.GroundCollider : null;
                if (bot.lowRun && bot.LowRunSeconds >= LowRunNeeded && !oneWay.Passed.Contains("low run stays on"))
                {
                    OneWayCheck("low run stays on", true, $"{bot.LowRunSeconds:0.00} s running across platforms with the stick at " +
                                                          $"({PlaytestBot.LowRunStick.x:0.0}, {PlaytestBot.LowRunStick.y:0.0}) and he never dropped");
                    bot.lowRun = false;
                }
            }

            // ─── mechanics: Hell's tricks on a test bench ───────────────

            /// <summary>What the mechanics scenario has seen so far.</summary>
            sealed class MechWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public int Failed;
                public bool Done;
                public int Bounces;
                public int Blocked;
                public int HurtFromBehind;
                public int LaserHits;
                public int BeamCrossings;
                public bool CrossingThisSweep;
                public float TelegraphAt = -1f;
                public float WarningAt = -1f;
                public int Blinks;
                public int LeadShots;
                public int LeadMoving;
                public int LeadGood;
                public readonly HashSet<int> LeadSeen = new HashSet<int>();
            }

            MechWatch mech;

            /// <summary>What the mechanics scenario must see, in the order a good run shows them.</summary>
            static readonly string[] MechBeats =
            {
                "bounce", "teleport telegraph", "teleport blink", "teleport moved", "shield blocks the front", "shield hurt from behind",
                "predictive aim", "laser warning", "laser sweep", "laser hits the hero", "stations done",
            };

            public IEnumerator MechanicsRun()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(MechanicsScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"mechanics: there is no scene \"{MechanicsScene}\" in content/scenes");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                mech = new MechWatch();
                Run.Event("shortcut", $"load level {MechanicsLevel}'s stage with the test bench {MechanicsScene} directly");
                LevelRuntime.PendingScene = MechanicsScene;
                var load = LoadLevel(MechanicsLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(MechanicsLevel, MechanicsSeconds, MechanicsDuring, waitOutDeath: true, until: () => mech.Done);
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                {
                    mech = null;
                    yield break;
                }

                // The lead imp's shots are judged as a group: the hero has to be moving for a lead to show.
                if (mech.LeadMoving == 0)
                    Run.Problem(PlaytestSeverity.Polish, $"mechanics predictive aim: the hero never moved while the lead imp fired ({mech.LeadShots} shots), so a lead could not show");
                else
                    MechCheck("predictive aim", mech.LeadGood > 0, mech.LeadGood > 0
                        ? $"{mech.LeadGood} of {mech.LeadMoving} shots at the moving hero went to where he would be"
                        : $"none of {mech.LeadMoving} shots at the moving hero went to where he would be");
                if (mech.LaserHits == 0 && mech.BeamCrossings > 0)
                    MechCheck("laser hits the hero", false, $"the beam crossed the hero {mech.BeamCrossings} times and never hurt him");
                else if (mech.LaserHits == 0)
                    Run.Problem(PlaytestSeverity.Polish, "mechanics laser: the beam never crossed the hero (he was out of its way), so its hitbox was not tried");
                MechCheck("stations done", mech.Done, mech.Done ? "all five stations ran" : $"the bench did not finish ({lastOutcome})");

                int passed = 0;
                foreach (var beat in MechBeats)
                {
                    if (mech.Passed.Contains(beat))
                        passed++;
                    else
                        Run.Problem(PlaytestSeverity.Bug, $"mechanics: never saw \"{beat}\" (level {lastOutcome})");
                }
                Run.Metric("mechanics.bounces", mech.Bounces);
                Run.Metric("mechanics.shieldBlocks", mech.Blocked);
                Run.Metric("mechanics.hitsFromBehind", mech.HurtFromBehind);
                Run.Metric("mechanics.blinks", mech.Blinks);
                Run.Metric("mechanics.laserHits", mech.LaserHits);
                Run.Metric("mechanics.passed", passed);
                Run.Metric("mechanics.failed", mech.Failed);
                Run.Progress(1f);
                bool ok = passed == MechBeats.Length && mech.Failed == 0;
                mech = null;
                End(ok ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void MechCheck(string beat, bool ok, string detail)
            {
                if (mech == null || mech.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    mech.Passed.Add(beat);
                    Run.Event("check", $"mechanics {beat}: ok ({detail})");
                }
                else
                {
                    mech.Failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"mechanics {beat}: {detail}");
                }
            }

            static readonly Regex MechBlink = new Regex(@"blink (-?[\d.]+),(-?[\d.]+) -> (-?[\d.]+),(-?[\d.]+)");

            /// <summary>The mechanics' own telemetry: bounces, blocks, blinks, laser phases, and the damage they let through.</summary>
            void MechanicsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                string level = "level " + log.World.LevelNumber;
                switch (e.Kind)
                {
                    case "bounce":
                        mech.Bounces++;
                        Run.Event("bounce", $"{level} at {e.Time:0.0} s: {e.Subject} bounced off a surface (normal {e.Detail}), {e.Value} bounces left");
                        MechCheck("bounce", true, $"{e.Subject} bounced off a surface with {e.Value} bounces left");
                        break;
                    case "shieldBlock":
                        mech.Blocked++;
                        Run.Event("shield", $"{level} at {e.Time:0.0} s: {e.Subject}'s front shield blocked {e.Value} damage ({e.Detail})");
                        MechCheck("shield blocks the front", true, $"a hit on {e.Subject}'s front did no damage ({e.Detail})");
                        break;
                    case "damage" when e.Subject == "bulwarkImp":
                        mech.HurtFromBehind++;
                        Run.Event("shield", $"{level} at {e.Time:0.0} s: {e.Subject} took {e.Value} damage (a hit its shield does not cover)");
                        MechCheck("shield hurt from behind", mech.Blocked > 0, mech.Blocked > 0
                            ? $"after {mech.Blocked} blocked hits, a hit from behind did {e.Value} damage"
                            : "it took damage before its shield ever blocked a hit");
                        break;
                    case "damage" when e.Subject == "player" && e.Detail == "beamLens":
                        mech.LaserHits++;
                        Run.Event("laser", $"{level} at {e.Time:0.0} s: the beam hurt the hero ({e.Value})");
                        MechCheck("laser hits the hero", true, "the sweeping beam crossed the hero and hurt him");
                        break;
                    case "teleport":
                        if (e.Detail.StartsWith("telegraph", StringComparison.Ordinal))
                        {
                            mech.TelegraphAt = e.Time;
                            Run.Event("teleport", $"{level} at {e.Time:0.0} s: {e.Subject} shows its spot ({e.Detail})");
                            MechCheck("teleport telegraph", true, $"{e.Subject} marked its spot before blinking");
                        }
                        else
                        {
                            mech.Blinks++;
                            float gap = e.Time - mech.TelegraphAt;
                            Run.Event("teleport", $"{level} at {e.Time:0.0} s: {e.Subject} blinked ({e.Detail}), {gap:0.00} s after the telegraph");
                            MechCheck("teleport blink", mech.TelegraphAt >= 0f && gap >= 0.5f, $"{gap:0.00} s between the telegraph and the blink (needs 0.5+)");
                            var m = MechBlink.Match(e.Detail ?? "");
                            if (m.Success)
                            {
                                var c = System.Globalization.CultureInfo.InvariantCulture;
                                var from = new Vector2(float.Parse(m.Groups[1].Value, c), float.Parse(m.Groups[2].Value, c));
                                var to = new Vector2(float.Parse(m.Groups[3].Value, c), float.Parse(m.Groups[4].Value, c));
                                MechCheck("teleport moved", Vector2.Distance(from, to) >= 1.5f, $"it jumped {Vector2.Distance(from, to):0.0} units");
                            }
                        }
                        break;
                    case "laser":
                        if (e.Detail.StartsWith("warning", StringComparison.Ordinal))
                        {
                            mech.WarningAt = e.Time;
                            mech.CrossingThisSweep = false;
                            Run.Event("laser", $"{level} at {e.Time:0.0} s: the lens shows its warning line ({e.Detail})");
                            MechCheck("laser warning", true, "a thin warning line came first");
                        }
                        else if (e.Detail.StartsWith("sweep", StringComparison.Ordinal))
                        {
                            float lead = e.Time - mech.WarningAt;
                            Run.Event("laser", $"{level} at {e.Time:0.0} s: the beam sweeps ({e.Detail}), {lead:0.00} s after its warning");
                            MechCheck("laser sweep", mech.WarningAt >= 0f && lead >= 0.8f, $"the beam fired {lead:0.00} s after the warning (needs 0.8+)");
                        }
                        else
                            Run.Event("laser", $"{level} at {e.Time:0.0} s: the sweep ends");
                        break;
                    case "signal" when e.Subject.StartsWith("mech:", StringComparison.Ordinal):
                        Run.Event("station", $"{level} at {e.Time:0.0} s: {e.Subject.Substring(5)}");
                        if (e.Subject == "mech:done")
                            mech.Done = true;
                        break;
                }
            }

            /// <summary>Every frame of the bench: the lead imp's shots against the intercept maths, and the beam against the hero.</summary>
            IEnumerator MechanicsDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || mech == null || world.Player == null)
                    return null;
                Vector2 heroVelocity = world.PlayerVelocity;
                foreach (var e in world.Live)
                {
                    if (!e.IsAlive)
                        continue;
                    if (e.DefId == "beamLens")
                    {
                        var laser = e.FindAbility<SickOfIt.Gameplay.Content.LaserSweepAbility>();
                        if (laser != null && laser.IsSweeping && !mech.CrossingThisSweep && laser.Crosses(world.Player))
                        {
                            mech.CrossingThisSweep = true;
                            mech.BeamCrossings++;
                            Run.Event("laser", $"the beam is on the hero at {now:0.0} s (hitbox crosses his collider)");
                        }
                    }
                    else if (e.DefId == "projectileEnemy" && mech.LeadSeen.Add(e.Serial) && e.Body != null)
                    {
                        SickOfIt.Gameplay.Entity shooter = null;
                        foreach (var other in world.Live)
                        {
                            if (other.IsAlive && other.DefId == "leadImp" && Vector2.Distance(other.Position, e.Position) < 1.5f)
                            {
                                shooter = other;
                                break;
                            }
                        }
                        Vector2 v = e.Body.linearVelocity;
                        if (shooter == null || v.sqrMagnitude < 0.01f)
                            continue;
                        Vector2 target = world.PlayerAimPoint;
                        Vector2 actual = v.normalized;
                        Vector2 straight = (target - e.Position).normalized;
                        Vector2 ideal = Emission.LeadDirection(target, heroVelocity, e.Position, v.magnitude);
                        float leadAngle = Vector2.Angle(actual, straight);
                        float offIdeal = Vector2.Angle(actual, ideal);
                        mech.LeadShots++;
                        bool moving = heroVelocity.magnitude > 0.5f;
                        if (moving)
                        {
                            mech.LeadMoving++;
                            if (leadAngle >= 1.5f && offIdeal <= 8f)
                                mech.LeadGood++;
                        }
                        Run.Event("predictive", $"the lead imp fired at {now:0.0} s: hero speed {heroVelocity.magnitude:0.0}, " +
                                                $"the shot leads {leadAngle:0.0} degrees off a straight aim and is {offIdeal:0.0} degrees off the intercept");
                    }
                }
                return null;
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
                public int AirborneOnFoothold;
                public int PhantomGround;
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
                "art stone crumbles", "art raft", "art chain", "art chainLift", "art floorLanding", "art lavaSurge", "art heatUpdraft",
                "art slagConveyor",
            };

            /// <summary>The pieces whose painted art the climb checks (entity id → beat).</summary>
            static readonly Dictionary<string, string> ClimbArtPieces = new Dictionary<string, string>
            {
                { "ledge", "art ledge" }, { "sinkingStone", "art sinkingStone" }, { "crumblingStone", "art crumblingStone" },
                { "debrisRaft", "art raft" }, { "debrisRaftSign", "art raft" }, { "debrisRaftRoof", "art raft" }, { "debrisRaftHay", "art raft" },
                { "chainLift", "art chainLift" }, { "floorLanding", "art floorLanding" },
            };

            /// <summary>The pieces painted as strips laid over their body (entity id → the strip and its beat): the box must be hidden under it.</summary>
            static readonly Dictionary<string, (string strip, string beat)> ClimbStripPieces = new Dictionary<string, (string, string)>
            {
                { "lavaSurge", ("SurgeArt", "art lavaSurge") }, { "heatUpdraft", ("UpdraftArt", "art heatUpdraft") },
                { "heatUpdraftTall", ("UpdraftArt", "art heatUpdraft") }, { "slagConveyor", ("BeltArt", "art slagConveyor") },
                { "slagConveyorDown", ("BeltArt", "art slagConveyor") },
            };

            /// <summary>The art check of a strip-painted piece on screen: its strip drawn and the box hidden, or a problem (once).</summary>
            void ClimbStripArt(SickOfIt.Gameplay.Entity e, string strip, string beat)
            {
                if (climb.Passed.Contains(beat) || climb.ArtMissing.Contains(e.DefId))
                    return;
                var shape = e.transform.Find("Shape");
                bool boxShown = shape != null && shape.TryGetComponent<SpriteRenderer>(out var box) && box.enabled;
                if (StripShown(e, strip) && !boxShown)
                {
                    ClimbCheck(beat, true, $"{Who(e)} is painted art (strip {strip}, {e.MainCollider.bounds.size.x:0.0}x{e.MainCollider.bounds.size.y:0.0} units)");
                }
                else
                {
                    climb.ArtMissing.Add(e.DefId);
                    Run.Problem(PlaytestSeverity.Bug, $"climb {beat}: {Who(e)} is still its stand-in box (no painted {strip})");
                }
            }

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
                if (controller != null && world.Player != null && world.Player.IsAlive && world.Player.Body != null && !controller.IsHeld)
                {
                    // He falls when nothing solid is under him: standing still "grounded" over no piece is a hidden collider.
                    bool onPiece = false;
                    foreach (var e in world.Live)
                        if (e != null && e.IsAlive && e.MainCollider != null && !e.MainCollider.isTrigger && world.PlayerStandsOn(e))
                            onPiece = true;
                    float vy = world.Player.Body.linearVelocity.y;
                    if (!onPiece && controller.IsGrounded && Mathf.Abs(vy) < 0.05f)
                        climb.PhantomGround++;
                    else
                        climb.PhantomGround = 0;
                    if (climb.PhantomGround >= 10)
                    {
                        ClimbCheck("falls off edges", false, "standing still grounded with no stone, ledge or raft under him: a hidden collider is holding him up");
                        climb.PhantomGround = 0;
                    }
                    else if (!onPiece && !controller.IsGrounded && vy < -2f)
                        ClimbCheck("falls off edges", true, "with nothing solid under him he fell (the ground check said airborne)");
                }
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive)
                        continue;
                    if (ClimbArtPieces.TryGetValue(e.DefId, out var artBeat) && e.MainCollider != null
                        && Mathf.Abs(e.Position.x - cam.x) < world.Camera.orthographicSize * world.Camera.aspect
                        && Mathf.Abs(e.Position.y - cam.y) < world.Camera.orthographicSize)
                        ClimbArt(e, artBeat);
                    if (ClimbStripPieces.TryGetValue(e.DefId, out var stripArt) && e.MainCollider != null)
                    {
                        // On screen by its body (the wave's centre can sit past the screen's edge while its crest is in view).
                        var eb = e.MainCollider.bounds;
                        float hw = world.Camera.orthographicSize * world.Camera.aspect, hh = world.Camera.orthographicSize;
                        if (eb.max.x > cam.x - hw && eb.min.x < cam.x + hw && eb.max.y > cam.y - hh && eb.min.y < cam.y + hh)
                            ClimbStripArt(e, stripArt.strip, stripArt.beat);
                    }
                    if ((StripShown(e, "ChainArt")) && e.FindAbility<SickOfIt.Gameplay.Content.SwingChainAbility>() != null)
                        ClimbCheck("art chain", true, $"{Who(e)} swings on painted chain links");
                    // Jump needs the ground check to see a foothold: resting on a stone or ledge must read as grounded.
                    if (controller != null && e.Tags.Contains("foothold") && world.PlayerStandsOn(e)
                        && world.Player.Body != null && Mathf.Abs(world.Player.Body.linearVelocity.y) < 0.05f)
                    {
                        climb.AirborneOnFoothold = controller.IsGrounded ? 0 : climb.AirborneOnFoothold + 1;
                        if (controller.IsGrounded)
                            ClimbCheck("jump grounded", true, $"standing on {Who(e)} counts as grounded, so Space can jump");
                        else if (climb.AirborneOnFoothold >= 10)
                            ClimbCheck("jump grounded", false, $"standing still on {Who(e)} but the ground check says airborne, so Space does nothing");
                    }
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

                yield return ClickNewGame(() => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1));
                if (Run.Done) yield break;
                yield return ReachLevel(1);
                if (Run.Done) yield break;
                Run.Progress(0.3f);

                // Play about 30 s: rewind once at ~10 s, pause and unpause at ~20 s.
                RewindPerf.Reset();
                rewindPerfSummaryLogged = false;
                bool rewound = false, paused = false;
                yield return PlayLevel(1, 30f, now =>
                {
                    if (!rewound && now > 10f && bot != null)
                    {
                        var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
                        if (budget != null && budget.Juice < 1.8f)
                            budget.Add(2f - budget.Juice);
                        if (bot.RewindNow(1.5f))
                        {
                            rewound = true;
                            Run.Event("rewind", "smoke: hold R for 1.5 s");
                        }
                    }
                    if (!paused && now > 20f)
                    {
                        paused = true;
                        return PauseAndResume();
                    }
                    return null;
                }, 0.3f, 0.9f);
                if (Run.Done) yield break;
                ReportRewindPerf();
                if (!rewound && RewindPerf.Rewinds <= 0)
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
                CursorCheck("playing", AimCursor.Showing || AimCursor.PointerOverUi, "the aim crosshair while the level plays");
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
                CursorCheck("paused", !AimCursor.Showing, "the arrow on the pause menu");
                string done =GameDefinition.Instance.menuTheme.texts.done;
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
                {
                    Run.Event("resume", "P");
                    // The mouse is left where the bot clicked Done; over a HUD button the arrow is right.
                    bool overUi = AimCursor.PointerOverUi;
                    CursorCheck("resumed", AimCursor.Showing || overUi, overUi ? "the arrow over a HUD button" : "the aim crosshair back after the pause");
                }
            }

            void CursorCheck(string beat, bool ok, string detail)
            {
                if (ok)
                    Run.Event("check", $"cursor {beat}: ok ({detail})");
                else
                    Run.Problem(PlaytestSeverity.Bug, $"cursor {beat}: want {detail}");
            }

            // ─── all-levels: every level on the path, one after another, each reported ─────

            /// <summary>One row of the all-levels table.</summary>
            sealed class PathResult
            {
                public int Level, Floor, Tries;
                public string Title = "", Result = "", Where = "", Before = "";
                public float Seconds;

                public string Name => "Level " + Level;

                public string Line() =>
                    $"{Name} {Title}: {Result.ToUpperInvariant()} in {Seconds:0} s ({Tries} {(Tries == 1 ? "try" : "tries")})" +
                    (Where.Length > 0 ? " " + Where : "") + (Before.Length > 0 ? $" [first try: {Before}]" : "");

                public string Row() =>
                    $"| {Name} | {Title} | {Result.ToUpperInvariant()} | {(Tries > 0 ? $"{Seconds:0} s" : "-")} | {Tries} | {(Where.Length > 0 ? Where : "-")}{(Before.Length > 0 ? $" (first try: {Before})" : "")} |";
            }

            /// <summary>Set by OpenLevel: the level asked for is running in a world of its own.</summary>
            bool openedLevel;
            // The last Progress value sent: OpenLevel repeats it while it waits (the package ends a run after 60 s without one).
            float lastProgressSent;

            /// <summary>When the scenario started (real time): all-levels' time budget counts from here, boot included.</summary>
            readonly float sessionStarted = Time.realtimeSinceStartup;

            float RunMinutes() => (Time.realtimeSinceStartup - sessionStarted) / 60f;

            /// <summary>
            /// all-levels and floors-A-B: each level opened directly in order (setup shortcut), played with the bot, once more after a
            /// death, and reported: cleared, died (where and by what) or stuck (where). A level the bot can't clear goes in the table
            /// and the next one starts; the run ends with the table.
            /// </summary>
            public IEnumerator AllLevels(List<int> levels, string what, bool checkEvery = false, string at = null, bool ladders = false, int thenLevel = 0, float budget = 0f, float hardEndSeconds = 0f)
            {
                var results = new List<PathResult>();
                takeShortcuts = false;
                reckless = false;
                preparedFirst = 0;
                if (levels.Count == 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, "no levels on the path (game.json's levels)");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                if (at != null)
                {
                    // Started later in the run (a start point): from that level on.
                    if (!StartAtIn(levels, at, out levels, out string where))
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"{what}: start point \"{at}\": {where}");
                        End(PlaytestOutcome.Error);
                        yield break;
                    }
                    Run.Event("start-at", $"{what} from {where}");
                    what += $" from {where}";
                }
                Run.Event("all-levels", $"{what}: {levels.Count} levels, {PathRange(levels)}; each opened directly (setup shortcut), played once more after a death");
                float runBudget = budget > 0f ? budget : PathRunBudget, runHardEnd = hardEndSeconds > 0f ? hardEndSeconds : PathRunHardEnd;
                float budgetEnd = sessionStarted + runBudget, hardEnd = sessionStarted + runHardEnd;
                Run.Event("budget", $"time budget: no new level after {runBudget / 60f:0.#} min of run time (now {RunMinutes():0.0} min in), " +
                                    $"the last one ends by {runHardEnd / 60f:0.#} min; each level capped at {PathLevelCap(1, Speed) / 60f:0} min of real time " +
                                    "(a floor boss's a little more), both tries and the loads; a stuck or a timeout moves on to the next level; " +
                                    $"the runner stops at {RunnerLimitMinutes:0} min, encoding the video included (up to ~{RunnerAfterMinutes:0} min after the table)");
                int reached = 0;
                for (int i = 0; i < levels.Count && !Run.Done; i++)
                {
                    int n = levels[i];
                    if (Time.realtimeSinceStartup >= budgetEnd)
                    {
                        Run.Event("budget", $"time budget reached at {RunMinutes():0.0} min: stopping before level {n}; {levels.Count - i} of {levels.Count} levels not reached");
                        break;
                    }
                    reached = i + 1;
                    var config = GameDefinition.Instance.GetLevel(n);
                    var row = new PathResult { Level = n, Floor = PathFloor(n), Title = config != null ? config.displayName ?? config.levelName ?? "" : "" };
                    float limit = PathLevelSeconds(n);
                    float cap = PathLevelCap(n, Speed);
                    float levelEnd = Mathf.Min(Time.realtimeSinceStartup + cap, hardEnd);
                    for (int attempt = 1; attempt <= PathTries && !Run.Done; attempt++)
                    {
                        // A second try only fits if a real share of the level's cap (or the run's) is left; else the first try's row stands.
                        if (attempt > 1 && levelEnd - Time.realtimeSinceStartup < 45f)
                        {
                            Run.Event("budget", $"level {n}: no second try, its {(levelEnd >= hardEnd ? "run's" : "level's")} time cap is used up ({RunMinutes():0.0} min in)");
                            break;
                        }
                        row.Tries = attempt;
                        yield return OpenLevel(n, attempt);
                        if (Run.Done) yield break;
                        if (!openedLevel)
                        {
                            row.Result = "stuck";
                            row.Where = $"never started (the game stayed in {SceneName()})";
                            Run.Problem(PlaytestSeverity.Bug, $"all-levels: level {n} never started when loaded directly (in {SceneName()})");
                            break;
                        }
                        float started = Time.realtimeSinceStartup;
                        // A Warden's arena in a stretch run (checkEvery) gets the arena checks: the fight's signals in order (how far
                        // he got, the last phase included), every attack's lead over its warning glow, the boss bar and the defeat.
                        stage = checkEvery && IsArena(n) && IsFloorStage(n) ? new StageWatch(n, StageCheck) : null;
                        float realBudget = Mathf.Max(5f, levelEnd - Time.realtimeSinceStartup);
                        yield return PlayLevel(n, limit, stage != null ? StageDuring : (Func<float, IEnumerator>)null, (float)i / levels.Count, (float)(i + 1) / levels.Count,
                            realBudget, waitOutDeath: true);
                        if (Run.Done) yield break;
                        bool capped = Time.realtimeSinceStartup - started >= realBudget - 0.5f;
                        row.Seconds = (Time.realtimeSinceStartup - started) * Speed;
                        if (stage != null)
                        {
                            if (lastOutcome == "completed" || lastOutcome != "died" || attempt == PathTries)
                                FinishStage(lastOutcome);
                            stage = null;
                        }
                        if (lastOutcome == "completed")
                        {
                            row.Result = "cleared";
                            row.Where = "";
                            break;
                        }
                        if (lastOutcome == "died")
                        {
                            row.Result = "died";
                            row.Where = lastDeath ?? "(where not seen)";
                            if (attempt < PathTries)
                            {
                                row.Before = "died " + row.Where;
                                Run.Event("retry", $"level {n}, try {attempt + 1}: died {row.Where}");
                                continue;
                            }
                            break;
                        }
                        row.Result = "stuck";
                        bool timedOut = lastOutcome == "time up" || lastOutcome == "timeout";
                        string why = !timedOut ? lastOutcome
                            : capped ? $"the level's time cap ran out ({cap / 60f:0.#} min real{(levelEnd >= hardEnd ? ", cut short by the run's budget" : "")})"
                            : $"no end after {limit:0} s";
                        row.Where = $"{why}, {lastWhere ?? "the hero not seen"}";
                        break;
                    }
                    if (row.Result.Length == 0)
                    {
                        // Never a blank row in the table.
                        row.Result = "stuck";
                        row.Where = "no result before the level's time cap";
                    }
                    results.Add(row);
                    Run.Event("level-result", row.Line());
                    // One event per floor by its outcome, so a part's timeline reads cleared / died / stuck at a glance.
                    Run.Event(row.Result == "cleared" ? "cleared" : row.Result == "died" ? "died" : "stuck",
                        (row.Floor > 0 ? $"Floor {row.Floor} ({row.Name})" : row.Name) + $" {row.Title}: {row.Result} in {row.Seconds:0} s" +
                        (row.Where.Length > 0 ? ", " + row.Where : ""));
                    Run.Metric($"level{n}.cleared", row.Result == "cleared" ? 1 : 0);
                    Run.Metric("path.cleared", results.FindAll(r => r.Result == "cleared").Count);
                    Run.Metric("path.died", results.FindAll(r => r.Result == "died").Count);
                    Run.Metric("path.stuck", results.FindAll(r => r.Result == "stuck").Count);
                    Run.Metric("path.played", results.Count);
                }
                if (Run.Done) yield break;

                // The levels the time budget left out: a row each, so the table always lists the whole run.
                for (int i = reached; i < levels.Count; i++)
                {
                    int n = levels[i];
                    var config = GameDefinition.Instance.GetLevel(n);
                    results.Add(new PathResult
                    {
                        Level = n, Floor = PathFloor(n), Result = NotReached,
                        Title = config != null ? config.displayName ?? config.levelName ?? "" : ""
                    });
                }
                int played = reached, notReached = levels.Count - reached;
                Run.Metric("path.played", played);
                Run.Metric("path.notReached", notReached);
                Run.Metric("path.budgetMinutes", Mathf.Round(RunMinutes() * 10f) / 10f);
                Run.Event("budget", $"time budget used: {RunMinutes():0.0} of {runBudget / 60f:0.#} min (hard end {runHardEnd / 60f:0.#}; " +
                                    $"{RunnerLimitMinutes - RunMinutes():0.0} min left of the runner's {RunnerLimitMinutes:0} for the video); levels covered: {played} of {levels.Count}" +
                                    (played > 0 ? $" ({PathRange(levels.GetRange(0, played))})" : "") +
                                    (notReached > 0 ? $"; not reached: {notReached} ({PathRange(levels.GetRange(played, notReached))})" : "; every level reached"));

                // The table: a summary line, one event per row, and the same table (markdown) in the report's log.
                int cleared = results.FindAll(r => r.Result == "cleared").Count, died = results.FindAll(r => r.Result == "died").Count,
                    stuck = results.FindAll(r => r.Result == "stuck").Count;
                var first = results.Find(r => r.Result != "cleared" && r.Result != NotReached);
                string summary = $"{what}: {cleared} of {played} played cleared, {died} died, {stuck} stuck" +
                                 (notReached > 0 ? $", {notReached} of {levels.Count} not reached (time budget)" : "") +
                                 (first != null ? $"; the first not cleared is {first.Name} {first.Title} ({first.Result})" : played > 0 ? "; every level played cleared" : "");
                Run.Event("summary", summary);
                Run.Log("all-levels " + summary);
                Run.Log("| Level | Name | Result | Time | Tries | Where / by what |");
                Run.Log("|---|---|---|---|---|---|");
                foreach (var row in results)
                {
                    Run.Event("table", row.Row());
                    Run.Log(row.Row());
                }
                foreach (var row in results)
                {
                    if (row.Result == "stuck")
                        Run.Problem(PlaytestSeverity.Warning, $"all-levels: {row.Name} {row.Title} stuck: {row.Where}");
                }
                // Criterion: the bot clears the Meadow and the Landing (Floor 1000) when they're in the run; climb-start and the
                // floors-A-B stretches check every level, and a floor's pace: 1-2 minutes (a floor boss's up to 3), Grudge pick included.
                foreach (var row in results)
                {
                    if (!checkEvery && row.Level != 1 && row.Floor != LandingFirstFloor)
                        continue;
                    float most = row.Floor > 0 && IsLongFloor(row.Level) ? LongFloorMax : row.Floor > 0 && row.Floor % 10 == 1 ? 180f : 120f;
                    string pace = row.Result != "cleared" || row.Floor == 0 ? "" :
                        row.Seconds < 60f ? ", pace short (under 1 min)" : row.Seconds > most ? $", pace long (over {most / 60f:0} min)" : ", pace ok";
                    if (row.Result == NotReached)
                    {
                        Run.Event("check", $"all-levels {row.Name} {row.Title}: {NotReached}");
                        continue;
                    }
                    Run.Event("check", $"all-levels {row.Name} {row.Title}: {(row.Result == "cleared" ? "ok (cleared)" : "not cleared (" + row.Result + (row.Where.Length > 0 ? ": " + row.Where : "") + ")")}" +
                                       $" in {row.Seconds:0} s, {row.Tries} {(row.Tries == 1 ? "try" : "tries")}{pace}");
                }
                if (ladders)
                {
                    foreach (var row in results)
                    {
                        if (row.Result == NotReached)
                            continue;
                        ladderKit.TryGetValue(row.Level, out int grabs);
                        bool top = ladderTops.Contains(row.Level);
                        Run.Event("check", $"ladder floor {row.Floor} ({row.Name}): {(grabs > 0 && top ? "ok" : "not climbed")}: grabbed {grabs} time(s), " +
                                           $"{(top ? "stepped off the top" : "never reached the top")}; {(row.Result == "cleared" ? "cleared" : "not cleared (" + row.Result + ")")}");
                        Run.Metric($"ladder{row.Floor}.grabs", grabs);
                    }
                }
                BalanceReport(what, levels);
                Run.Progress(1f);
                if (thenLevel > 0 && !Run.Done)
                {
                    // Then one level played like level-N (a Warden's arena and the Shaft after it), which ends the run.
                    Run.Event("all-levels", $"{what}: the floors are done, now level {thenLevel} as a scenario of its own");
                    // Opened like every floor above (OpenLevel passes the Shaft, a death or a video the last floor left the game in, and
                    // retries a load the scene loader rejected while it was busy); a bare LoadLevel from there never started it (level 149).
                    yield return OpenLevel(thenLevel, 1);
                    if (Run.Done) yield break;
                    if (!openedLevel)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"{what}: level {thenLevel} never started when loaded directly (in {SceneName()})");
                        End(PlaytestOutcome.Stuck);
                        yield break;
                    }
                    yield return LevelScenario(thenLevel, alreadyOpen: true);
                    yield break;
                }
                End(cleared == results.Count ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            /// <summary>
            /// Opens level n directly (setup shortcut) from wherever the game is: the last level (its results, or standing where
            /// it timed out), the death scene (skipped like a player, Escape) and the Shaft after it (Continue), a video or a menu.
            /// Sets openedLevel once level n runs in a new world; gives up 45 s after its last load (the row says so and the run goes
            /// on), inside the package's 60 s without an event that would end the whole run.
            /// </summary>
            IEnumerator OpenLevel(int n, int attempt)
            {
                openedLevel = false;
                // Just after a death the level is gone before the death scene is up: let the game get there first.
                float settle = Time.realtimeSinceStartup + 8f;
                while (!Run.Done && GameplayWorld.Instance == null && ShaftScreen.Current == null && Time.realtimeSinceStartup < settle
                       && Object.FindAnyObjectByType<DeathRuntime>() == null && Object.FindAnyObjectByType<VideoRuntime>() == null
                       && Object.FindAnyObjectByType<MainMenuManager>() == null && Object.FindAnyObjectByType<LevelSelectionManager>() == null)
                    yield return null;
                var before = GameplayWorld.Instance;
                float end = Time.realtimeSinceStartup + 90f, loadAt = -99f, skipAt = -1f, beatAt = Time.realtimeSinceStartup;
                GameplayWorld loadedFrom = null;
                int loads = 0;
                while (!Run.Done)
                {
                    var world = GameplayWorld.Instance;
                    if (world != null && world != before && LevelRunning(n))
                    {
                        openedLevel = true;
                        yield break;
                    }
                    if (Time.realtimeSinceStartup > end || (loads >= 3 && Time.realtimeSinceStartup - loadAt > 45f))
                        yield break;
                    if (Time.realtimeSinceStartup - beatAt >= 10f)
                    {
                        // A slow load, the Shaft or a death scene can take over a minute: say the bot is alive.
                        beatAt = Time.realtimeSinceStartup;
                        Run.Progress(lastProgressSent);
                    }
                    yield return ResumeStrayPause(n);
                    if (ShaftScreen.Current != null)
                    {
                        // The Shaft after a death (the drop to the Waystone): Continue like a player, then open the level.
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
                        if (skipAt < 0f)
                            skipAt = Time.realtimeSinceStartup + 2f;
                        if (Time.realtimeSinceStartup >= skipAt)
                        {
                            yield return SkipTap(inVideo ? "video" : "death animation");
                            skipAt = Time.realtimeSinceStartup + 3f;
                        }
                        yield return null;
                        continue;
                    }
                    skipAt = -1f;
                    // A load in flight: wait for it, unless some other level has started since (the run restarted at a Waystone).
                    bool otherStarted = world != null && world != loadedFrom && world.LevelNumber != n && LevelRunning(world.LevelNumber);
                    bool waiting = loads > 0 && Time.realtimeSinceStartup - loadAt < 15f && !otherStarted;
                    if (!waiting && loads < 3)
                    {
                        if (loads == 0)
                            PrepareOpen(n);
                        loads++;
                        loadedFrom = world;
                        loadAt = Time.realtimeSinceStartup;
                        Run.Event("shortcut", $"load level {n} directly" + (attempt > 1 ? $" (try {attempt})" : "") + (loads > 1 ? $" (load {loads})" : ""));
                        if (world != null)
                            world.Telemetry?.Finish("playtest left");
                        input.ReleaseAll();
                        Time.timeScale = 1f;
                        var load = LoadLevel(n);
                        if (load != null)
                            yield return load;
                    }
                    yield return null;
                }
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
                yield return ClickNewGame(() => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1));
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
                    yield return ResumeStrayPause(n);
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
                            yield return SkipTap(inVideo ? "video" : "death animation");
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
                // Circle X's route takes its shortcuts (the Sack Lift, the King's Chimney): the levels they skip are off the story path.
                takeShortcuts = true;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickNewGame(() => Object.FindAnyObjectByType<VideoRuntime>() != null || GameplayWorld.Instance != null);
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
                        // Watch a moment for the recording (Video3 longer: Yooba's sponsor spot starts at 11.5 s), then skip like a player.
                        yield return WaitReal(video == 3 ? 13f : 2.5f);
                        if (Run.Done) yield break;
                        float skipUntil = Time.realtimeSinceStartup + 20f;
                        while (!Run.Done && Object.FindAnyObjectByType<VideoRuntime>() != null && ShaftScreen.Current == null && Time.realtimeSinceStartup < skipUntil)
                        {
                            yield return SkipTap($"video {playing}");
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
                        // The floors and stages of Hell, the Landing on, get the stage checks (title, lines, floors, new enemy, breather).
                        stage = n >= LandingLevel && IsFloorStage(n) ? new StageWatch(n, StoryCheck) : null;
                        yield return PlayLevel(n, 0f, stage != null ? StageDuring : (Func<float, IEnumerator>)null, from, to, left, waitOutDeath: true,
                            until: n == RouteShortcuts[0].level ? ForemanExitWatch(n) : null);
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
                    if (n == MilestoneLevel)
                        CheckMilestone(lastOutcome);
                    if (lastOutcome != "completed")
                    {
                        Run.Event("stopped", $"level {n}: {lastOutcome} after {attempts} attempt(s)");
                        reachedEnd = false;
                        break;
                    }

                    // A stage that leads straight on must name the next story level, or Continue goes elsewhere (the
                    // level select) and the wait for it below would stall. Old game data in the build does that: its
                    // Level 4 was the arcade "Everything, Everywhere" with no next level.
                    bool viaShortcut = Array.Exists(RouteShortcuts, r => r.level == n && r.next == (step + 1 < StoryPath.Length ? StoryPath[step + 1].level : -1));
                    if (n == RouteShortcuts[0].level)
                        TopUpFare();
                    if (step + 1 < StoryPath.Length && StoryPath[step + 1].video == 0 && config.nextLevelNumber != StoryPath[step + 1].level && !viaShortcut)
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
                    // Circle IX's floors follow the Magma King now (Floor 900); cracked-road-route plays them.
                    int lastStory = StoryPath[StoryPath.Length - 1].level;
                    var lastStoryConfig = GameDefinition.Instance.GetLevel(lastStory);
                    int afterStory = lastStoryConfig != null && lastStoryConfig.nextVideoNumber <= 0 ? lastStoryConfig.nextLevelNumber : 0;
                    var afterWorld = NewWorld(previous);
                    bool select = Object.FindAnyObjectByType<LevelSelectionManager>() != null;
                    bool onward = afterStory > 0 && afterWorld != null && afterWorld.LevelNumber == afterStory;
                    StoryCheck("end", afterStory > 0 ? onward : select,
                        onward ? $"level {lastStory} leads on to level {afterStory} ({afterWorld.LevelName}); cracked-road-route plays on from there"
                        : select ? $"level {lastStory} leads to the level select{(afterStory > 0 ? $" (want level {afterStory})" : "")}"
                        : $"level {lastStory} led to {SceneName()} instead of {(afterStory > 0 ? "level " + afterStory : "the level select")}");
                    if (onward)
                        yield return ToMainMenu();
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

            /// <summary>The Landing's floor counter read 1000 and ticked to 999 on its landing.</summary>
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
            }

            /// <summary>The 999 milestone: Floor 999 (level MilestoneLevel) opens on "ONLY 999 TO GO".</summary>
            void CheckMilestone(string outcome)
            {
                bool milestone = story.Milestones.Exists(m => m.level == MilestoneLevel && m.floor == LandingMilestone);
                StoryCheck("landing" + LandingMilestone, milestone,
                    milestone ? $"the floor {LandingMilestone} beat played as Floor {LandingMilestone} opened" : $"no floor {LandingMilestone} beat on level {MilestoneLevel} (level {outcome})");
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

                // Every floor ("Floor N: ...") and story stage ("Floors N-M: ...") after the Meadow counts down, and none comes after an arcade level.
                var floorsTitle = new Regex(@"^Floors? (\d+)(?:-(\d+))?:");
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
                check("climbCounter" + n, (seen.From > seen.To || start == end) && seen.Shown == end,
                    $"the marker climbed from FLOOR {seen.From} and the counter ticked to FLOOR {seen.Shown} (level {n} ends on {climbed}" +
                    (jumpTo > 0 ? $", the shortcut on to {jumpTo})" : ")"));
                check("climbFloorsLeft" + n, seen.FloorsLeft.Contains(left), $"\"{seen.FloorsLeft}\" (want {left} floors left)");
                check("climbWarden" + n, warden != null && seen.NextWarden.IndexOf(warden.warden, StringComparison.OrdinalIgnoreCase) >= 0 && seen.NextWarden.Contains(distance),
                    $"\"{seen.NextWarden}\" (want {(warden != null ? warden.warden : "?")}, {distance} floors up)");
                // A Warden's floor: the climb lights its Waystone (saved as the run's new start) and the Circle above wakes up.
                if (end != config.bottomFloor && start == end && RunFlow.IsWaystone(end))
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
                    Run.Event("shortcut", "open the Shaft over the main menu (preview: the marker climbs 1000 to 999)");
                    var screen = ShaftScreen.Preview(1000, 999);
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
                {
                    yield return Audit(audit, step, steps, 0.5f, progress: step > 0);
                    if (Run.Done) yield break;
                    // Layout scenario: the shop at desktop and phone sizes, top and scrolled-to-bottom (acceptance criteria).
                    if (step > 0)
                        yield return ShopLayoutSizes();
                }
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

            /// <summary>Ten shop visits back to back: every greeting and goodbye voiced, none repeated inside the pool's window, the portrait bobbing while he speaks.</summary>
            public IEnumerator YoobaGreetingsRun()
            {
                var definition = GameDefinition.Instance;
                int video = definition.yooba.helloAfterVideo;
                var videoConfig = definition.GetVideo(video);
                int nextLevel = videoConfig != null ? videoConfig.nextLevel : 3;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Event("shortcut", "reset Yooba's save, marker at Floor 999, load the Shaft through video " + video);
                YoobaProgress.Reset();
                ShaftProgress.Floor = 999;
                ShaftProgress.Plunged = true;
                RogueliteManager.Instance.SetPoints(0);
                PlayerPrefs.SetInt("ReturnToLevel", nextLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(video);
                yield return Run.Until(() => Object.FindAnyObjectByType<VideoRuntime>() != null && ClockTime > 0.1f, 60f, "the video to start");
                if (Run.Done) yield break;
                yield return WaitReal(3f);
                yield return Tap(Key.Escape);
                yield return Run.Until(() => ShaftScreen.Current != null, 20f, "the Shaft");
                if (Run.Done) yield break;
                var screen = ShaftScreen.Current;
                yield return WaitReal(4f);
                if (!screen.Ready)
                    yield return Tap(Key.Space);
                yield return Run.Until(() => ShaftScreen.Current != screen || screen.Ready, 60f, "the Shaft's Continue");
                if (Run.Done) yield break;
                yield return WaitReal(0.8f);

                var welcomeSeen = new List<string>();
                var byeSeen = new List<string>();
                int repeats = 0, unvoiced = 0;
                float bobMin = float.MaxValue, bobMax = float.MinValue;
                bool oneLine = true;
                for (int visit = 1; visit <= 10 && !Run.Done; visit++)
                {
                    int at = yoobaLines.Count;
                    var button = screen.YoobaButton;
                    yield return ClickButton(button, "Visit Yooba " + visit);
                    yield return Run.Until(() => UpgradeShopManager.OverShaft != null, 10f, "Yooba's shop");
                    if (Run.Done) yield break;
                    var shop = UpgradeShopManager.OverShaft;
                    float t0 = Time.realtimeSinceStartup;
                    while (shop != null && shop.Yooba != null && !shop.Yooba.Speaking && Time.realtimeSinceStartup - t0 < 3f)
                        yield return null;
                    while (shop != null && shop.Yooba != null && shop.Yooba.Speaking && Time.realtimeSinceStartup - t0 < 25f)
                    {
                        bobMin = Mathf.Min(bobMin, shop.Yooba.Lift);
                        bobMax = Mathf.Max(bobMax, shop.Yooba.Lift);
                        yield return null;
                    }
                    int welcomeEnd = yoobaLines.Count;
                    for (int i = at; i < welcomeEnd; i++)
                    {
                        if (!yoobaLines[i].StartsWith("yooba (ShopWelcome", StringComparison.Ordinal))
                            continue;
                        var m = System.Text.RegularExpressions.Regex.Match(yoobaLines[i], @"\((vo_[A-Za-z0-9_]+)(, no clip)?\)\s*$");
                        string id = m.Success ? m.Groups[1].Value : yoobaLines[i];
                        if (!m.Success || m.Groups[2].Success) unvoiced++;
                        if (welcomeSeen.Contains(id)) repeats++;
                        welcomeSeen.Add(id);
                        if (yoobaLines[i].Contains("\n")) oneLine = false;
                        Run.Event("shop-greeting", $"visit {visit}: {id}");
                    }
                    int bye0 = yoobaLines.Count;
                    yield return LeaveYooba(screen, ShopCheck);
                    for (int i = bye0; i < yoobaLines.Count; i++)
                        if (yoobaLines[i].StartsWith("yooba (ShopLeave", StringComparison.Ordinal))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(yoobaLines[i], @"\((vo_[A-Za-z0-9_]+)(, no clip)?\)\s*$");
                            if (!m.Success || m.Groups[2].Success) unvoiced++;
                            byeSeen.Add(m.Success ? m.Groups[1].Value : yoobaLines[i]);
                        }
                }
                ShopCheck("greetingsVoiced", welcomeSeen.Count >= 10 && unvoiced == 0, $"{welcomeSeen.Count} welcomes, {unvoiced} without a clip");
                ShopCheck("greetingsNoRepeat", repeats == 0, $"{repeats} repeated welcomes in 10 visits ({string.Join(", ", welcomeSeen)})");
                ShopCheck("portraitBobs", bobMax - bobMin >= 2f, $"lift ranged {bobMin:0.0}..{bobMax:0.0} while he spoke");
                ShopCheck("captionOneLine", oneLine, "no line break in his captions");
                Run.Metric("shop.greetingRepeats", repeats);
                Run.Progress(1f);
                End(shopFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }

            /// <summary>
            /// Yooba's shop from the Shaft, through the real flow (acceptance criteria of the shop task): his hello Shaft
            /// after Video3 (setup shortcut: his save reset, the marker at Floor 999, his hello skipped), Visit Yooba beside
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
                Run.Event("shortcut", $"reset Yooba's save, put the Shaft's marker at Floor 999 (the Landing cleared), diamonds to 0, load video {video} directly");
                YoobaProgress.Reset();
                ShaftProgress.Floor = 999;
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
                Run.Event("shortcut", "Shaft preview: the marker climbs 999 to 998 (Floor 999 cleared)");
                var climb = ShaftScreen.Preview(999, 998);
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
                    // Floor 1000 has no push and no push music (Docs/LevelDesign.md, its page); the push music plays from Floor 999's lava on.
                    ShaftScreen.LevelFloors(ShaftLevel, out int shaftStart, out int shaftEnd);
                    if (push == null && shaftStart - shaftEnd == 1)
                        MusicCheck("stagePush", loop != null, $"level {ShaftLevel} is one floor with no push (the Landing): {Tracks(stageMusic)}");
                    else
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
            /// (setup shortcut: his save reset and the marker put at Floor 999, where the Landing ends), his lines between
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
                Run.Event("shortcut", $"reset Yooba's save, put the Shaft's marker at Floor 999 (the Landing cleared), load video {video} directly");
                YoobaProgress.Reset();
                ShaftProgress.Floor = 999;
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

            /// <summary>
            /// Yooba's gate and the Not Today Grudge (Docs/Story.md, "Yooba's prices"; AGENTS.md): an old save (setup
            /// shortcut: raw PlayerPrefs as a build before the gate wrote them, no unlock flags) keeps its goods; the gated
            /// Double-Dipped Shots is missing from the counter until Tummy-Dipped Shots is level 2 (bought with real
            /// clicks), then shown; the prices are reported. Then the scene grudgeTest offers a Grudge pick that must list Not Today.
            /// </summary>
            public IEnumerator ShopGate()
            {
                var roguelite = RogueliteManager.Instance;
                var definition = GameDefinition.Instance;
                const string gated = "doubleDip", needs = "spicyFireballs";
                int failed = 0;
                void Gate(string beat, bool ok, string detail)
                {
                    if (ok) { Run.Event("check", $"shop-gate {beat}: ok ({detail})"); return; }
                    failed++;
                    Run.Problem(PlaytestSeverity.Bug, $"shop-gate {beat}: {detail}");
                }

                yield return ToMainMenu();
                if (Run.Done) yield break;

                Run.Event("shortcut", $"an old save: {needs} bought once the way a build before the gate stored it, no unlock flags, 500 diamonds");
                UnlockFlags.Reset();
                PlayerPrefs.SetInt("Rogue_Shop_" + needs, 1);
                PlayerPrefs.DeleteKey("Rogue_Shop_" + gated);
                roguelite.SetPoints(500);
                yield return ClickUntil(definition.mainMenuConfig.labels.shop, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Scene("Shop (gate)");
                var shop = Object.FindAnyObjectByType<UpgradeShopManager>();
                yield return WaitReal(2f);
                if (Run.Done || shop == null) yield break;

                int basePrice = RogueliteManager.TotalPrice(false), allPrice = RogueliteManager.TotalPrice(true);
                Run.Event("economy", $"Yooba's whole counter: {basePrice} diamonds for every level of every good, {allPrice} with the gated ones (was 655)");
                Run.Metric("shop.basePrice", basePrice);
                Run.Metric("shop.totalPrice", allPrice);
                Gate("oldSaveKept", roguelite.GetUpgradeLevel(needs) == 1 && shop.ShownIds().Contains(needs),
                    $"{needs} still level {roguelite.GetUpgradeLevel(needs)} and on the counter");
                Gate("hiddenUntilUnlocked", !shop.ShownIds().Contains(gated) && shop.CardButton(gated) == null,
                    $"on the counter: {string.Join(", ", shop.ShownIds())}");
                yield return Audit("Shop (gated good hidden)", 0, 2, 1f, progress: false);
                if (Run.Done) yield break;

                int cost = 0;
                foreach (var upgrade in RogueliteManager.ShopCatalog)
                    if (upgrade != null && upgrade.id == needs) cost = roguelite.GetNextCost(upgrade);
                yield return ClickButton(shop.CardButton(needs), $"{needs} level 2 (♦ {cost})");
                yield return WaitReal(1.5f);
                if (Run.Done) yield break;
                Gate("unlocked", roguelite.GetUpgradeLevel(needs) == 2 && shop.ShownIds().Contains(gated) && shop.CardButton(gated) != null,
                    $"{needs} level {roguelite.GetUpgradeLevel(needs)}; on the counter: {string.Join(", ", shop.ShownIds())}");
                yield return Audit("Shop (gated good shown)", 1, 2, 1f, progress: false);
                if (Run.Done) yield break;
                yield return ClickUntil(definition.upgradeShopConfig.layout.backText, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                if (Run.Done) yield break;
                Run.Progress(0.5f);

                // The Grudge pick: the scene offers three Grudges, Not Today one of them.
                var content = SickOfIt.Gameplay.GameContent.Current;
                Gate("grudgeInPool", content != null && content.Upgrades.TryGetValue("notToday", out var def) && def.weight > 0f && def.requires.Count == 0,
                    "Not Today is in content/upgrades, weighted into every pick, ungated");
                lastGrudgeOffer = null;
                UpgradeProgress.Reset();
                LevelRuntime.PendingScene = GrudgeScene;
                Run.Event("shortcut", $"load level {CutsceneLevel}'s stage with the scene {GrudgeScene} directly");
                var load = LoadLevel(CutsceneLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(CutsceneLevel, 40f, until: () => lastGrudgeOffer != null && GameplayWorld.Instance != null
                    && GameplayWorld.Instance.Hud != null && !GameplayWorld.Instance.Hud.PickerOpen);
                LevelRuntime.PendingScene = null;
                if (Run.Done) yield break;
                Gate("notTodayInPicker", lastGrudgeOffer != null && Array.IndexOf(lastGrudgeOffer.Split(','), "notToday") >= 0, $"the Grudge pick offered: {lastGrudgeOffer ?? "nothing"}");
                UpgradeProgress.Reset();
                roguelite.SetPoints(0);
                Run.Progress(1f);
                End(failed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
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
                yield return MainMenuSizes();
                if (Run.Done) yield break;

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
                DeathScreenChecks();
                // The same screen at phone portrait (a window resize, a setup shortcut): the criterion is stated at 390x844.
                int dw = Screen.width, dh = Screen.height;
                if (CanResizeWindow && (dw != 390 || dh != 844))
                {
                    Screen.SetResolution(390, 844, false);
                    yield return Run.Until(() => Screen.width == 390 && Screen.height == 844, 6f, "the window to resize to 390x844");
                    if (Run.Done) yield break;
                    Run.Event("layout-size", $"death screen at phone portrait: {Screen.width}x{Screen.height}");
                    yield return Audit("Death phone portrait", 0, 0, 1.2f, false);
                    if (Run.Done) yield break;
                    DeathScreenChecks();
                    Screen.SetResolution(dw, dh, false);
                    yield return Run.Until(() => Screen.width == dw && Screen.height == dh, 6f, "the window to return to its size");
                    yield return WaitReal(0.5f);
                }
                yield return ClickUntil(texts.rageQuit, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 30f);
                if (Run.Done) yield break;
                Run.Scene("Main Menu");

                Run.Metric("layoutProblems", layoutProblems);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            int layoutProblems;

            /// <summary>
            /// Screen.SetResolution is for standalone windows. In a WebGL page it resizes the canvas away from the browser
            /// window (v-playtest: the game shrank to 2/3, pinned bottom-left, black bars above and right, and never came back),
            /// so the layout scenario only resizes in the editor and standalone players.
            /// </summary>
            static bool CanResizeWindow => Application.platform != RuntimePlatform.WebGLPlayer;

            /// <summary>
            /// The main menu at 16:9, 1280x720, 1024x768, a tall narrow window, phone portrait and phone landscape (a window
            /// resize, a setup shortcut: the runner's device is one size). Each size is audited: tagline whole, stickers clear
            /// of buttons and each other, buttons 48 px tall at phone size. The original size is restored afterwards.
            /// </summary>
            IEnumerator MainMenuSizes()
            {
                if (!CanResizeWindow)
                {
                    Run.Event("layout-size", $"main menu kept at {Screen.width}x{Screen.height}: a WebGL page's size is the browser window's, set by the runner's device");
                    yield break;
                }
                int w0 = Screen.width, h0 = Screen.height;
                var sizes = new (string name, int w, int h)[]
                {
                    ("16:9 desktop", 1920, 1080), ("small desktop", 1280, 720), ("narrow desktop", 1024, 768),
                    ("tall narrow", 760, 900), ("phone portrait", 390, 844), ("phone landscape", 844, 390),
                };
                foreach (var size in sizes)
                {
                    Screen.SetResolution(size.w, size.h, false);
                    yield return Run.Until(() => Screen.width == size.w && Screen.height == size.h, 6f, $"the window to resize to {size.w}x{size.h}");
                    if (Run.Done) yield break;
                    Run.Event("layout-size", $"main menu at {size.name}: {Screen.width}x{Screen.height}");
                    yield return Audit("Main Menu " + size.name, 0, 0, 1.2f, false);
                    if (Run.Done) yield break;
                }
                Screen.SetResolution(w0, h0, false);
                yield return Run.Until(() => Screen.width == w0 && Screen.height == h0, 6f, "the window to return to its size");
                yield return WaitReal(0.5f);
            }

            /// <summary>The Death screen's buttons (Return, Again!, Rage Quit) sit below its title and quip with no overlap, and are thumb-tall.</summary>
            void DeathScreenChecks()
            {
                string size = $"{Screen.width}x{Screen.height}";
                var result = MenuLayoutAudit.Check(GameDefinition.Instance.menuTheme.responsive);
                MenuLayoutAudit.Box title = null, quip = null;
                foreach (var box in result.Boxes)
                {
                    if (box.Transform.name == "Title") title = box;
                    else if (box.Transform.name == "Quip") quip = box;
                }
                bool portrait = Screen.height > Screen.width;
                foreach (var box in result.Boxes)
                {
                    if (box.Kind != MenuElementKind.Button) continue;
                    bool ours = box.Name.Contains("Again") || box.Name.Contains("Rage Quit") || box.Name.Contains("Return");
                    if (!ours) continue;
                    // Screen y grows upward: "below" means the button's top is under the text's bottom.
                    bool belowTitle = title == null || !portrait || box.Screen.yMax <= title.Screen.yMin + 1f;
                    bool belowQuip = quip == null || !portrait || box.Screen.yMax <= quip.Screen.yMin + 1f;
                    bool clear = (title == null || !box.Screen.Overlaps(title.Screen)) && (quip == null || !box.Screen.Overlaps(quip.Screen));
                    bool ok = belowTitle && belowQuip && clear;
                    Run.Event("check", $"death screen {size}: {box.Name} {(ok ? "below the title and quip, no overlap" : "OVERLAPS OR SITS ABOVE THE TITLE/QUIP")} ({box.Screen.height:0} px tall)");
                    if (!ok)
                        Run.Problem(PlaytestSeverity.Bug, $"death screen {size}: {box.Name} overlaps or sits above the title/quip");
                }
                if (title == null) Run.Problem(PlaytestSeverity.Bug, $"death screen {size}: the title is not on screen");
            }

            /// <summary>The main menu's tagline in full, the stickers clear of every button, and the buttons thumb-tall (criteria of the menu re-layout).</summary>
            void MainMenuDressingChecks(MenuLayoutAudit.Result result)
            {
                string size = $"{Screen.width}x{Screen.height}";
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                float minHeight = float.MaxValue;
                foreach (var box in result.Boxes)
                    if (box.Kind == MenuElementKind.Button)
                        minHeight = Mathf.Min(minHeight, box.Screen.height);
                bool tagline = false, stickers = false;
                foreach (var box in result.Boxes)
                {
                    if (box.Transform.name == "Subtitle")
                    {
                        tagline = true;
                        bool inside = box.Screen.xMin >= -2f && box.Screen.yMin >= -2f && box.Screen.xMax <= screen.xMax + 2f && box.Screen.yMax <= screen.yMax + 2f;
                        bool covered = false;
                        foreach (var other in result.Boxes)
                            if (other.Kind == MenuElementKind.Button && other.Screen.Overlaps(box.Screen))
                                covered = true;
                        Run.Event("check", $"main menu tagline {size}: {(inside && !covered ? "fully visible, no button over it" : "COVERED OR OFF SCREEN")} ({box.Screen})");
                        if (!inside || covered)
                            Run.Problem(PlaytestSeverity.Bug, $"main menu tagline {size} is covered or off screen");
                    }
                    else if (box.Transform.name == "Decoration")
                    {
                        stickers = true;
                        bool covered = false, crowded = false;
                        foreach (var other in result.Boxes)
                        {
                            if (other == box) continue;
                            if (other.Kind == MenuElementKind.Button && other.Screen.Overlaps(box.Screen))
                                covered = true;
                            string n = other.Transform.name;
                            if ((n == "Decoration" || n == "Subtitle" || n == "Title") && other.Screen.Overlaps(box.Screen))
                                crowded = true;
                        }
                        Run.Event("check", $"main menu sticker {size}: {(covered ? "OVERLAPPED BY A BUTTON" : crowded ? "OVERLAPS THE TAGLINE, TITLE OR ANOTHER STICKER" : "clear of every button, the tagline, the title and the other sticker")} ({box.Screen})");
                        if (covered)
                            Run.Problem(PlaytestSeverity.Bug, $"main menu sticker {size} is overlapped by a button");
                        if (crowded)
                            Run.Problem(PlaytestSeverity.Bug, $"main menu sticker {size} overlaps the tagline, the title or another sticker");
                    }
                }
                if (!tagline) Run.Problem(PlaytestSeverity.Bug, $"main menu tagline {size} is not on screen");
                if (!stickers) Run.Problem(PlaytestSeverity.Bug, $"main menu stickers {size} are not on screen");
                Run.Metric("mainMenu.minButtonHeightPx", minHeight < float.MaxValue ? minHeight : 0f);
                bool tall = minHeight >= 48f;
                Run.Event("check", $"main menu buttons {size}: shortest {minHeight:0} px ({(tall ? "at least 48 px" : "UNDER 48 px")})");
                if (!tall && Mathf.Min(Screen.width, Screen.height) <= 900)
                    Run.Problem(PlaytestSeverity.Bug, $"main menu buttons {size}: shortest is {minHeight:0} px, under 48");
            }

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
                if (screen.StartsWith("Main Menu"))
                    MainMenuDressingChecks(result);
                Run.Metric("layout." + screen.Replace(" ", "") + ".problems", result.Problems.Count);
                if (screen == "Level 1 HUD" && Camera.main != null
                    && Camera.main.TryGetComponent(out SickOfIt.Core.Device.AspectRatioController aspect))
                {
                    var vp = aspect.LastViewport;
                    bool portraitScreen = Screen.height > Screen.width;
                    float top = vp.yMax * Screen.height;
                    bool ok = vp.width > 0.999f && (portraitScreen
                        ? vp.yMax >= Screen.safeArea.yMax / Screen.height - 0.001f && vp.height < 0.9f
                        : vp.height > 0.999f);
                    Run.Event("layout", $"{screen}: playfield viewport x{vp.x:0.##} y{vp.y:0.##} w{vp.width:0.##} h{vp.height:0.##} " +
                                        $"({(portraitScreen ? "portrait: full width, top of the screen" : "landscape: fills the screen")})");
                    if (ok) Run.Event("check", $"playfield viewport {Screen.width}x{Screen.height}: ok (full width, no bars above; portrait anchored to the top)");
                    else Run.Problem(PlaytestSeverity.Bug, $"playfield viewport {Screen.width}x{Screen.height}: {vp} (top {top:0}px) does not match the layout rule");
                }
                if (screen.StartsWith("Main Menu") && Screen.height > Screen.width && Camera.main != null
                    && Camera.main.TryGetComponent(out SickOfIt.Core.Device.AspectRatioController menuAspect))
                {
                    var vp = menuAspect.LastViewport;
                    bool full = vp.width > 0.999f && vp.height > 0.999f && menuAspect.PortraitCoverActive;
                    Run.Event("check", $"portrait menu {Screen.width}x{Screen.height}: " +
                                       (full ? "fills the screen around the hero, no black bars" : $"LETTERBOXED ({vp})"));
                    if (!full)
                        Run.Problem(PlaytestSeverity.Bug, $"portrait menu {Screen.width}x{Screen.height} is letterboxed: {vp}");
                }
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
                        ok &= Expect(name, path, "Powered by HAWKES GEN", "https://hawkesgen.ai", check.gen, "powered by hawkes gen");
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
                lastHitBy = "";
                lastDeath = null;
                lastWhere = null;
                Run.Scene("Level " + n);
                float startBy = Time.realtimeSinceStartup + 45f;
                while (!Run.Done && !LevelRunning(n) && Time.realtimeSinceStartup < startBy)
                {
                    yield return ResumeStrayPause(n);
                    yield return null;
                }
                yield return Run.Until(() => LevelRunning(n), 1f, $"level {n} to start");
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
                    bot.takeSideDoors = takeSideDoors;
                    Run.Event("bot", $"level {n}: the bot takes Hell's shortcuts (presses 2 on a shortcut's choice{(takeSideDoors ? ", the doors in the wall too" : "")})");
                }
                if (bot != null && reckless)
                {
                    bot.reckless = true;
                    bot.useRewind = false;
                    Run.Event("bot", $"level {n}: the bot plays to lose (walks into the shots, no dodging, firing or rewinding)");
                }
                Time.timeScale = Speed;
                Run.Event("level-start", $"level {n}: {config.levelName}");
                BalanceBegin(n);

                var health = world.Player.GetComponentInChildren<PlayerHealth>();
                float limit = Mathf.Max(30f, config.duration) + 10f;
                if (playSeconds > 0f)
                    limit = Mathf.Min(limit, playSeconds);
                float started = Time.realtimeSinceStartup;
                float realLimit = Mathf.Min(limit / Speed + 60f, realBudget);
                float lastClock = -1f, clockMovedAt = started, nextScan = 0f, shownProgress = -1f, heartbeatClock = -1f, heartbeatAt = started;
                string stallMarker = null;
                float stallSince = started;
                // A hit/rewind loop at one spot: deaths and rewinds keep the marker below changing, so this one ignores them. The same
                // 6-unit column of the same phase for LoopSeconds (real) with 3+ deaths or rewinds in it is a loop, not progress.
                string loopMarker = null;
                float loopSince = started;
                int loopCount0 = 0;
                string outcome = "timeout";
                bool dying = false;
                while (!Run.Done)
                {
                    if (world == null) { outcome = dying ? "died" : "scene changed"; break; }
                    var clock = GameClock.Instance;
                    float now = clock != null ? clock.Time : 0f;
                    BalanceFrame(now);
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
                            lastDeath = $"at {now:0} s, {HeroWhere(world)}, killed by {(lastHitBy == "pit" ? "pit (fell out of the bottom of the view)" : lastHitBy.Length > 0 ? lastHitBy : "?")}";
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

                    // No progress for StallSeconds (real): the hero in the same 2-unit cell, the same phase and path section, no death: the
                    // run says where it stands and the level ends as "stalled" (the scenario goes on or ends as stuck, never waits out the runner's 60 min).
                    if (!held && world.Player != null)
                    {
                        var at = world.Player.Position;
                        var section = world.Effects != null && world.Effects.HasPath ? world.Effects.CurrentSection : null;
                        string marker = $"{world.Director?.PhaseName}|{section?.name}|{Mathf.FloorToInt(at.x / 2f)},{Mathf.FloorToInt(at.y / 2f)}|{deaths}";
                        if (marker != stallMarker)
                        {
                            stallMarker = marker;
                            stallSince = Time.realtimeSinceStartup;
                        }
                        else if (Time.realtimeSinceStartup - stallSince > StallSeconds)
                        {
                            lastWhere = HeroWhere(world);
                            Run.Event("stuck", $"level {n}: no progress for {StallSeconds / 60f:0} min, {lastWhere}");
                            Run.Problem(PlaytestSeverity.Bug, $"stuck: level {n} made no progress for {StallSeconds / 60f:0} min, {lastWhere}");
                            outcome = "stalled";
                            break;
                        }
                    }

                    if (!held && world.Player != null)
                    {
                        string loopNow = $"{world.Director?.PhaseName}|{Mathf.FloorToInt(world.Player.Position.x / 6f)}";
                        int loopCount = deaths + (bot != null ? bot.RewindsStarted : 0);
                        if (loopNow != loopMarker)
                        {
                            loopMarker = loopNow;
                            loopSince = Time.realtimeSinceStartup;
                            loopCount0 = loopCount;
                        }
                        else if (Time.realtimeSinceStartup - loopSince > LoopSeconds && loopCount - loopCount0 >= 3)
                        {
                            lastWhere = HeroWhere(world);
                            Run.Event("stuck", $"level {n}: hit and rewind loop at x {world.Player.Position.x:0.0} for {LoopSeconds:0} s ({loopCount - loopCount0} rewinds/deaths), {lastWhere}");
                            Run.Problem(PlaytestSeverity.Bug, $"stuck: level {n} died or rewound {loopCount - loopCount0} times in {LoopSeconds:0} s at x {world.Player.Position.x:0.0}, {lastWhere}");
                            outcome = "stalled";
                            break;
                        }
                    }

                    if (Time.realtimeSinceStartup >= nextScan)
                    {
                        nextScan = Time.realtimeSinceStartup + 0.5f;
                        ScanPlaceholders(world);
                        Run.Note($"level {n} {world.Director?.PhaseName} t={now:0}");
                    }
                    // Progress only goes forward: a retried level (or one that restarts after a death) must not pull the bar back.
                    float progress = Mathf.Max(lastProgressSent, Mathf.Lerp(progressFrom, progressTo, Mathf.Clamp01(now / limit)));
                    // The package ends the whole run as stuck after 60 s with no Progress, Event or Add. A run of many levels
                    // (all-levels: 79) moves the bar less than 0.01 per level, so a quiet floor went silent for a minute and beta's
                    // all-levels run died on Level 5. While the game clock moves, the bot is playing: report it every 5 s. A clock
                    // that stands still is this loop's own softlock check (15 s); a held one (pause, picker) still gets the 60 s.
                    if (progress - shownProgress >= 0.01f
                        || (Time.realtimeSinceStartup - heartbeatAt >= 5f && Mathf.Abs(now - heartbeatClock) > 0.0001f)
                        || Time.realtimeSinceStartup - heartbeatAt >= 30f)
                    {
                        shownProgress = progress;
                        lastProgressSent = progress;
                        heartbeatClock = now;
                        heartbeatAt = Time.realtimeSinceStartup;
                        Run.Progress(progress);
                    }

                    var extra = during?.Invoke(now);
                    if (extra != null)
                        yield return extra;
                    else
                        yield return null;
                }

                // A level that didn't end: where the hero was, so the report shows which climb he couldn't make.
                if (outcome != "completed" && outcome != "died" && outcome != "stalled" && world != null && world.Player != null)
                {
                    lastWhere = HeroWhere(world);
                    if (outcome == "timeout" || outcome == "time up")
                        Run.Event("stuck", $"level {n} {outcome} in {lastWhere}");
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

            /// <summary>
            /// Where the hero is, for a stuck or death line: the phase, the floor, his place, the path section and the foothold
            /// the bot was heading for (its ClimbTarget), and how often it gave up on one it couldn't reach.
            /// </summary>
            string HeroWhere(GameplayWorld world)
            {
                var section = world.Effects != null && world.Effects.HasPath ? world.Effects.CurrentSection : null;
                var aim = bot != null ? bot.ClimbTarget : null;
                int floor = world.Stage != null && world.Stage.HasFloors ? world.Stage.Floor : 0;
                var at = world.Player != null ? world.Player.Position : Vector2.zero;
                return $"phase \"{world.Director?.PhaseName}\"" + (floor > 0 ? $", FLOOR {floor}" : "") + $": the hero at ({at.x:0.0}, {at.y:0.0})" +
                       (section != null ? $", path section \"{section.name}\"" : "") +
                       (aim != null ? $", heading for {aim.name} at ({aim.Position.x:0.0}, {aim.Position.y:0.0})" : "") +
                       (bot != null && bot.GaveUpOn > 0 ? $", gave up on {bot.GaveUpOn} foothold(s) out of reach" : "") +
                       (bot != null && bot.Unwedged > 0 ? $", dropped off {bot.Unwedged} corner(s) he hung on" : "");
            }

            /// <summary>What last hurt the hero in the level being played (a "damage" telemetry source), what killed him and where, and where he was when it didn't end.</summary>
            string lastHitBy = "", lastDeath, lastWhere;

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

            /// <summary>
            /// Escape to skip a video or the death animation, only while no level is loaded: the video or death scene
            /// lingers a moment after the next level loads, and Escape there pauses the level at 0 s, where it waits
            /// for good (beta: the retry of Floor 1000 stuck on the pause menu).
            /// </summary>
            IEnumerator SkipTap(string what)
            {
                var world = GameplayWorld.Instance;
                if (world != null && world.Player != null)
                    yield break;
                yield return Tap(Key.Escape);
                Run.Event("skip", what);
            }

            /// <summary>
            /// A level paused before it started (a skip press that landed on it) is resumed with P like a player,
            /// so waiting for the level never stands on the pause menu.
            /// </summary>
            IEnumerator ResumeStrayPause(int n)
            {
                var world = GameplayWorld.Instance;
                var hud = world != null ? world.Hud : null;
                if (world == null || world.LevelNumber != n || hud == null || !hud.Paused || hud.ResultsOpen || hud.PickerOpen)
                    yield break;
                Run.Event("resume", $"level {n} was paused before it started (a skip press landed on it): P to resume");
                yield return Tap(Key.P);
                yield return WaitReal(0.5f);
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
                text = StagedMenuLabel(text);
                for (int attempt = 0; attempt < 2 && !Run.Done; attempt++)
                {
                    MenuButton button = null;
                    if (attempt == 0)
                        yield return Run.Until(() =>
                        {
                            // The main menu is in pages: turn to the one holding this button.
                            if (FindButton(text) == null)
                                Object.FindAnyObjectByType<MainMenuManager>()?.ShowPageHolding(text);
                            RevealStagedButton(text);
                            return (button = FindButton(text)) != null && button.IsInteractable();
                        }, timeout, $"the \"{text}\" button");
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
