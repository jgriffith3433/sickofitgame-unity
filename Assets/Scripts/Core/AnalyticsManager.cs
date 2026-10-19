using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Hawkes;
using UnityEngine;

namespace SickOfIt.Core
{
    /// <summary>
    /// The game's analytics: a thin layer over Hawkes Gen's Unity bridge (<see cref="HawkesAnalytics"/>, package
    /// com.hawkes.analytics; hawkesgen/docs/analytics.md), which loads Gen's SDK into the page and sends the events.
    /// It starts the bridge with the product id, tells the page which build this is (Resources/HawkesBuildInfo, written
    /// by the build), keeps a few per-session counts (deaths per level, for the death screen's feedback card) and shows
    /// the feedback card in the game's look. In the editor and outside WebGL nothing is sent; every call is still kept
    /// in <see cref="Recent"/> so tests and playtests can see what would have gone out.
    ///
    /// Who calls what (AGENTS.md "Analytics"): the level's events (level_start/level_end, death, rewind, Grudge
    /// choices, floors, bosses) come from the gameplay telemetry through SickOfIt.Gameplay.PlayAnalytics; runs and
    /// Waystones from RunFlow; videos from VideoRuntime; screens from the menus. Presentation only: nothing here
    /// touches the simulation, and no call is made per frame.
    /// </summary>
    public static class AnalyticsManager
    {
        public const string Product = "sickofit";
        public const string SdkUrl = "https://gen.hawkesgen.com/sdk/analytics@1.js";
        /// <summary>Written by the build (SickOfIt.Tools.Editor.AnalyticsBuildInfo) and removed after it.</summary>
        public const string BuildInfoResource = "HawkesBuildInfo";

        /// <summary>The feedback card in the game's colors (ui/theme.json: panel #1E1220, text #FFF4D6, the title's orange).</summary>
        const string CardTheme = "{\"background\":\"#1E1220\",\"color\":\"#FFF4D6\",\"accent\":\"#FF7A3D\",\"font\":\"'Arial Black', Impact, system-ui, sans-serif\",\"radius\":\"6px\"}";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SickOfItAnalytics_SetBuild(string version, string commit, string date);
        [DllImport("__Internal")] static extern void SickOfItAnalytics_Ask(string json);
        [DllImport("__Internal")] static extern int SickOfItAnalytics_AskOpen();
        [DllImport("__Internal")] static extern void SickOfItAnalytics_CloseAsk();
        [DllImport("__Internal")] static extern void SickOfItAnalytics_Debug();
        [DllImport("__Internal")] static extern string SickOfItAnalytics_DebugInfo();
        static readonly bool InPage = true;
#else
        static void SickOfItAnalytics_SetBuild(string version, string commit, string date) { }
        static void SickOfItAnalytics_Ask(string json) { }
        static int SickOfItAnalytics_AskOpen() => 0;
        static void SickOfItAnalytics_CloseAsk() { }
        static void SickOfItAnalytics_Debug() { }
        static string SickOfItAnalytics_DebugInfo() => "";
        static readonly bool InPage = false;
#endif

        /// <summary>True in a WebGL build, where events reach the page's SDK.</summary>
        public static bool Live => InPage;

        static bool started;
        static bool ready;
        static string screen = "";
        static readonly Dictionary<int, int> deathsByLevel = new Dictionary<int, int>();
        static readonly List<string> recent = new List<string>();

        /// <summary>The last 200 calls ("level_start {...}"), in the editor too (what a WebGL build would send).</summary>
        public static IReadOnlyList<string> Recent => recent;
        /// <summary>Every call as it is made: (event or method, props JSON).</summary>
        public static event Action<string, string> Sent;
        /// <summary>The screen last reported.</summary>
        public static string CurrentScreen => screen;

        [Serializable]
        class BuildInfo { public string version; public string commit; public string date; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            started = false;
            ready = false;
            screen = "";
            deathsByLevel.Clear();
            recent.Clear();
            Start();
        }

        static void Start()
        {
            if (started)
                return;
            started = true;
            try
            {
                var info = Resources.Load<TextAsset>(BuildInfoResource);
                var build = info != null ? JsonUtility.FromJson<BuildInfo>(info.text) : null;
                if (InPage)
                    SickOfItAnalytics_SetBuild(build != null && !string.IsNullOrEmpty(build.version) ? build.version : Application.version,
                        build?.commit ?? "", build?.date ?? "");
                var settings = ScriptableObject.CreateInstance<HawkesAnalyticsSettings>();
                settings.product = Product;
                settings.sdkUrl = SdkUrl;
                settings.captureExceptions = true;
                settings.logOutsideWebGL = false;
                HawkesAnalytics.Init(settings);
                Note("init", build != null ? $"{{\"version\":\"{build.version}\",\"commit\":\"{build.commit}\"}}" : "{}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HawkesAnalytics] could not start: " + e.Message);
            }
        }

        // ─── The standard events (hawkesgen/docs/analytics.md) ──────────────

        /// <summary>The player can play (the main menu is up, or a level started first: a playtest's setup shortcut).</summary>
        public static void Ready()
        {
            if (ready)
                return;
            ready = true;
            HawkesAnalytics.Ready();
            Note("game_ready", "{}");
        }

        /// <summary>What the game shows: menu, level_select, video_select, shop, settings, shaft, level, pause, results, death, video.</summary>
        public static void Screen(string name)
        {
            if (string.IsNullOrEmpty(name) || name == screen)
                return;
            screen = name;
            HawkesAnalytics.Screen(name);
            Note("screen", Json(new Dictionary<string, object> { { "name", name } }));
        }

        /// <summary>Copied onto every event from now on; a null value removes the key.</summary>
        public static void SetContext(Dictionary<string, object> context)
        {
            if (context == null || context.Count == 0)
                return;
            HawkesAnalytics.SetContext(context);
        }

        public static void SetContext(string key, object value) => HawkesAnalytics.SetContext(key, value);

        public static void LevelStart(int level, Dictionary<string, object> props)
        {
            Ready();
            HawkesAnalytics.LevelStart(level.ToString(), props);
            Note("level_start", Json(props));
        }

        /// <summary>result: complete, death, quit or restart. The SDK adds the duration and the deaths it counted.</summary>
        public static void LevelEnd(int level, string result, Dictionary<string, object> props)
        {
            HawkesAnalytics.LevelEnd(level.ToString(), result, props);
            Note("level_end", Json(props, "result", result));
        }

        /// <summary>A death that stood (not undone with a rewind): what killed him and where. Counted for the death screen.</summary>
        public static void Death(int level, string cause, Dictionary<string, object> props)
        {
            deathsByLevel[level] = DeathsIn(level) + 1;
            HawkesAnalytics.Death(string.IsNullOrEmpty(cause) ? "unknown" : cause, props);
            Note("death", Json(props, "cause", cause));
        }

        /// <summary>Deaths that stood on this level in this session.</summary>
        public static int DeathsIn(int level) => deathsByLevel.TryGetValue(level, out int n) ? n : 0;

        public static void Feature(string name, Dictionary<string, object> props = null)
        {
            HawkesAnalytics.Feature(name, props);
            Note("feature", Json(props, "name", name));
        }

        /// <summary>A choice ('node': grudge, shortcut:&lt;id&gt;), with what was offered in the props.</summary>
        public static void Choice(string node, string option, Dictionary<string, object> props = null)
        {
            var p = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
            p["node"] = node;
            p["option"] = option;
            HawkesAnalytics.Track("choice", p);
            Note("choice", Json(p));
        }

        /// <summary>action: play, complete or skip; at: seconds into it.</summary>
        public static void Video(string id, string action, float at)
        {
            HawkesAnalytics.Video(id, action, at);
            Note("video", Json(new Dictionary<string, object> { { "id", id }, { "action", action }, { "at", at } }));
        }

        /// <summary>Progress up Hell: 'step' only grows (floors climbed from the bottom), 'of' the whole climb.</summary>
        public static void Progress(int step, int of, string label, Dictionary<string, object> props = null)
        {
            var p = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
            p["step"] = step;
            if (of > 0) p["of"] = of;
            p["label"] = label;
            HawkesAnalytics.Track("progress", p);
            Note("progress", Json(p));
        }

        public static void RunStart(Dictionary<string, object> props)
        {
            HawkesAnalytics.Track("run_start", props);
            Note("run_start", Json(props));
        }

        /// <summary>result: win, lose (Hell sent him back to a Waystone) or quit (New Run over a run).</summary>
        public static void RunEnd(string result, Dictionary<string, object> props)
        {
            var p = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
            p["result"] = result;
            HawkesAnalytics.Track("run_end", p);
            Note("run_end", Json(p));
        }

        /// <summary>A custom event (^[a-z][a-z0-9_]{1,39}$): boss, shop_purchase, rage_quit, shaft... Prefer the standard ones.</summary>
        public static void Track(string name, Dictionary<string, object> props = null)
        {
            HawkesAnalytics.Track(name, props);
            Note(name, Json(props));
        }

        // ─── The feedback card ─────────────────────────────────────────────

        /// <summary>
        /// The SDK's feedback card (thumbs or stars, an optional comment) in the game's colors, at a pause (the death
        /// screen, the results): never mid-play. Each id shows once per session and not for 7 days after a close.
        /// </summary>
        public static void Ask(string id, string question, string context, bool stars, bool comment, string placeholder = null, string thanks = "Noted.")
        {
            var o = new Dictionary<string, object>
            {
                { "id", id }, { "question", question }, { "context", context }, { "kind", stars ? "stars" : "thumbs" },
                { "comment", comment }, { "thanks", thanks },
            };
            if (!string.IsNullOrEmpty(placeholder))
                o["placeholder"] = placeholder;
            string json = Json(o).TrimEnd('}') + ",\"theme\":" + CardTheme + "}";
            Note("ask", json);
            if (InPage)
            {
                try { SickOfItAnalytics_Ask(json); }
                catch (Exception e) { Debug.LogWarning("[HawkesAnalytics] ask: " + e.Message); }
            }
        }

        /// <summary>A feedback card is on the page (always false outside WebGL).</summary>
        public static bool AskOpen
        {
            get
            {
                if (!InPage) return false;
                try { return SickOfItAnalytics_AskOpen() != 0; }
                catch { return false; }
            }
        }

        /// <summary>Play goes on: an unanswered card goes away and the keyboard is the game's again.</summary>
        public static void CloseAsk()
        {
            if (!InPage) return;
            try { SickOfItAnalytics_CloseAsk(); }
            catch { }
        }

        // ─── The playtest's view ───────────────────────────────────────────

        /// <summary>Turns on the SDK's debug log (as ?hawkes-analytics-debug=1 does) so a playtest can read what went out.</summary>
        public static void EnableDebug()
        {
            if (!InPage) return;
            try { SickOfItAnalytics_Debug(); }
            catch { }
        }

        /// <summary>The SDK's debug log as JSON { sdk, config, events } ("" outside WebGL).</summary>
        public static string DebugInfo()
        {
            if (!InPage) return "";
            try { return SickOfItAnalytics_DebugInfo() ?? ""; }
            catch { return ""; }
        }

        public static void Flush() => HawkesAnalytics.Flush();

        static void Note(string name, string props)
        {
            string line = name + " " + props;
            recent.Add(line);
            if (recent.Count > 200)
                recent.RemoveAt(0);
            if (Sent == null)
                return;
            try { Sent(name, props); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ─── A small JSON writer for Recent (the bridge writes its own) ────

        static string Json(Dictionary<string, object> props, string key = null, object value = null)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            if (key != null)
            {
                Append(sb, key, value);
                first = false;
            }
            if (props != null)
            {
                foreach (var kv in props)
                {
                    if (kv.Key == key) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    Append(sb, kv.Key, kv.Value);
                }
            }
            return sb.Append('}').ToString();
        }

        static void Append(StringBuilder sb, string key, object value)
        {
            sb.Append('"').Append(key).Append("\":");
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int or long: sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)); break;
                default:
                    sb.Append('"');
                    foreach (char c in value.ToString())
                    {
                        if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                        else if (c < 0x20) sb.Append(' ');
                        else sb.Append(c);
                    }
                    sb.Append('"');
                    break;
            }
        }
    }
}
