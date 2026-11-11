using System.Collections;
using SickOfIt.Data;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// A player build keeps GameDefinition (and every sprite sheet, clip and painting it references) out of the player
    /// data: the deploy moves it out of Resources into Addressables bundles (Tools/Editor PlayerDataSplit) and puts a
    /// Boot scene first. Here, with no GameDefinition in Resources, the Boot scene loads it from its bundle (with a
    /// progress bar), hands it to <see cref="GameDefinition.Provide"/> and opens the next scene, the main menu.
    /// The editor, tests and any build that still has it in Resources never get here.
    /// </summary>
    public sealed class GameDefinitionBoot : MonoBehaviour
    {
        const int QuietTries = 3;
        const float RetrySeconds = 2f;

        static AsyncOperationHandle<GameDefinition> handle;

        /// <summary>The bar's share for the game data bundle; the rest is the "core" art and sound every screen uses.</summary>
        const float DefinitionShare = 0.3f;

        /// <summary>When the boot began, the game data arrived and the core was in (real seconds since startup; 0 = not yet).</summary>
        public static float StartedAt { get; private set; }
        public static float DefinitionAt { get; private set; }
        public static float ReadyAt { get; private set; }
        /// <summary>Real seconds since startup when Addressables' init (runtime data and catalog) finished; 0 = not yet. Splits the game-data time into catalog and bundle.</summary>
        public static float InitAt { get; private set; }
        /// <summary>The bar's fill (0..1, never goes back) and how many times it moved: the playtest's "a bar with progress".</summary>
        public static float Progress { get; private set; }
        public static int ProgressSteps { get; private set; }

        float progress;
        string failure;
        GUIStyle labelStyle;

        void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (value <= progress + 0.0001f)
                return;
            progress = value;
            Progress = value;
            ProgressSteps++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Decide()
        {
            if (Application.isEditor)
                return;
            // Before any BeforeSceneLoad hook asks for it: in a split build Instance waits instead of logging an error.
            GameDefinition.Loading = Resources.Load<GameDefinition>("GameDefinition") == null;
        }

        // After every BeforeSceneLoad hook, so the catalog and bundle URL rewrites (TextureVariantRedirect,
        // CatalogCacheBuster) are installed before Addressables starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (!GameDefinition.Loading)
                return;
            var go = new GameObject("GameDefinitionBoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameDefinitionBoot>();
        }

        IEnumerator Start()
        {
            // Never gives up: a CDN's 503 or a dropped connection passes, and until the definition arrives nothing else
            // (the menu, the playtest bot registering) can start, so a stop here looks like a game that never boots.
            StartedAt = Time.realtimeSinceStartup;
            Debug.Log($"[GameDefinitionBoot] Loading the game data at {StartedAt:0.0}s since the player started.");
            // The menu's own bundles download beside the game data instead of after it (the manifest that names them is inside it).
            StartCoroutine(PrefetchCore());
            for (int attempt = 1; ; attempt++)
            {
                failure = null;
                handle = AddressablesAPI.LoadAssetAsync<GameDefinition>(GameDefinition.Address);
                while (!handle.IsDone)
                {
                    SetProgress(handle.GetDownloadStatus().Percent * DefinitionShare);
                    yield return null;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    // Never released: the game reads it until the page closes. Its art and sound are not in it: only what
                    // every screen uses ("core") loads now; the rest when a level, video or screen needs it (GameAssets).
                    DefinitionAt = Time.realtimeSinceStartup;
                    SetProgress(DefinitionShare);
                    Debug.Log($"[GameDefinitionBoot] Game data loaded from its bundles (try {attempt}) in {DefinitionAt - StartedAt:0.0}s.");
                    Debug.Log($"[GameDefinitionBoot] Core prefetch so far: {(prefetchDone ? $"done in {prefetchSeconds:0.0}s" : "still running")}.");
                    GameAssets.Init(handle.Result);
                    yield return GameAssets.Acquire(GameAssets.Core, new[] { GameAssets.Core }, p => SetProgress(DefinitionShare + p * (1f - DefinitionShare)));
                    SetProgress(1f);
                    ReadyAt = Time.realtimeSinceStartup;
                    Debug.Log($"[GameDefinitionBoot] Core art and sound in after {ReadyAt - DefinitionAt:0.0}s; the boot took {ReadyAt - StartedAt:0.0}s " +
                              $"({ReadyAt:0.0}s since the player started), the bar moved {ProgressSteps} times.");
                    GameDefinition.Provide(handle.Result);
                    int next = SceneManager.GetActiveScene().buildIndex + 1;
                    if (next < SceneManager.sceneCountInBuildSettings)
                        SceneManager.LoadScene(next);
                    // The select cards, Shaft and shop art come in behind the menu, not before it: MainMenuManager starts that
                    // once the menu is up (a timer here ticked through the menu scene's own load and fetched them before it).
                    Destroy(gameObject);
                    yield break;
                }

                Debug.LogError($"[GameDefinitionBoot] Couldn't load the game data (try {attempt}): {handle.OperationException?.Message}");
                if (handle.IsValid())
                    AddressablesAPI.Release(handle);
                if (attempt >= QuietTries)
                    failure = "Couldn't load the game yet. Check the connection; still trying.";
                yield return new WaitForSecondsRealtime(Mathf.Min(RetrySeconds * attempt, 15f));
            }
        }

        bool prefetchDone;
        float prefetchSeconds;

        /// <summary>
        /// Downloads the bundles labelled <see cref="GameAssets.CoreLabel"/> (PlayerDataManifest.PackCore) and
        /// <see cref="GameAssets.MenuLabel"/> (PlayerDataSplit.LabelMenuGroup) while the game data loads, so the browser has
        /// fetched the menu's art and sound — and the MainMenu scene's background/explosion sheets — by the time
        /// <see cref="GameAssets.Acquire"/> and the MainMenu scene ask for them (the download is cached; the handle is
        /// released at once so the heap doesn't keep a second copy). Any failure is ignored: Acquire / the scene download
        /// what is missing, as before.
        /// </summary>
        IEnumerator PrefetchCore()
        {
            float began = Time.realtimeSinceStartup;
            var init = AddressablesAPI.InitializeAsync(false);
            yield return init;
            InitAt = Time.realtimeSinceStartup;
            Debug.Log($"[GameDefinitionBoot] Addressables init (catalog) done after {InitAt - began:0.0}s.");
            var keys = new System.Collections.Generic.List<object>(2);
            if (init.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (string label in new[] { GameAssets.CoreLabel, GameAssets.MenuLabel })
                {
                    foreach (var locator in AddressablesAPI.ResourceLocators)
                    {
                        if (locator.Locate(label, typeof(object), out var locations) && locations != null && locations.Count > 0)
                        {
                            keys.Add(label);
                            break;
                        }
                    }
                }
            }
            if (init.IsValid())
                AddressablesAPI.Release(init);
            if (keys.Count > 0)
            {
                var download = AddressablesAPI.DownloadDependenciesAsync(keys, AddressablesAPI.MergeMode.Union, false);
                yield return download;
                if (download.IsValid())
                    AddressablesAPI.Release(download);
            }
            prefetchSeconds = Time.realtimeSinceStartup - began;
            prefetchDone = true;
            string which = keys.Count == 0 ? "skipped (no label in the catalog)"
                : "finished (" + string.Join(", ", keys) + ")";
            Debug.Log($"[GameDefinitionBoot] Core/menu prefetch {which} after {prefetchSeconds:0.0}s.");
        }

        Texture2D sky, fill;

        // The game's lava palette (Floor 1000's painted look): ash-black sky going to ember red, lava orange, hot yellow.
        static readonly Color Ash = new Color(0.05f, 0.02f, 0.02f);
        static readonly Color Ember = new Color(0.42f, 0.09f, 0.03f);
        static readonly Color Lava = new Color(1f, 0.45f, 0.1f);
        static readonly Color Hot = new Color(1f, 0.82f, 0.35f);

        static Texture2D Ramp(Color top, Color bottom)
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                t.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            if (sky != null) Destroy(sky);
            if (fill != null) Destroy(fill);
        }

        // Cosmetic only: nothing else is on screen while the bundles download. Drawn in the lava art style
        // (ash sky, lava sea along the bottom, rising embers, a rimmed bar filled with lava), sized for phones too.
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
                return;
            float w = Screen.width, h = Screen.height, t = Time.realtimeSinceStartup;
            if (sky == null) { sky = Ramp(Ash, Ember); fill = Ramp(Hot, Lava); }
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, w, h), sky);

            // The lava sea: a glowing band with a hot surface line that breathes.
            float seaY = h * 0.78f;
            GUI.color = new Color(Lava.r, Lava.g, Lava.b, 0.85f);
            GUI.DrawTexture(new Rect(0, seaY, w, h - seaY), Texture2D.whiteTexture);
            GUI.color = new Color(Hot.r, Hot.g, Hot.b, 0.7f + 0.3f * Mathf.Sin(t * 2.2f));
            GUI.DrawTexture(new Rect(0, seaY, w, Mathf.Max(3f, h * 0.006f)), Texture2D.whiteTexture);

            // Embers rising off the lava, positions from the clock only.
            float u = Mathf.Max(3f, Mathf.Min(w, h) * 0.008f);
            for (int i = 0; i < 28; i++)
            {
                float ph = (t * (0.05f + 0.03f * (i % 5)) + i * 0.137f) % 1f;
                float x = (Mathf.Sin(i * 12.9898f) * 0.5f + 0.5f) * w + Mathf.Sin(t * 0.9f + i) * u * 3f;
                float y = Mathf.Lerp(seaY, h * 0.08f, ph);
                GUI.color = new Color(Hot.r, Lava.g, 0.1f, (1f - ph) * 0.9f);
                float sz = u * (0.6f + (i % 3) * 0.4f);
                GUI.DrawTexture(new Rect(x, y, sz, sz), Texture2D.whiteTexture);
            }

            // The bar: dark basalt frame, ember-red trough, lava fill with a hot top edge.
            float barW = Mathf.Min(w * 0.7f, 520f), barH = Mathf.Max(16f, h * 0.026f), rim = Mathf.Max(3f, barH * 0.2f);
            var bar = new Rect((w - barW) / 2f, h * 0.6f, barW, barH);
            GUI.color = Ash;
            GUI.DrawTexture(new Rect(bar.x - rim, bar.y - rim, bar.width + rim * 2f, bar.height + rim * 2f), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.06f, 0.03f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float p = Mathf.Clamp01(progress);
            var filled = new Rect(bar.x, bar.y, bar.width * p, bar.height);
            GUI.color = new Color(Lava.r, Lava.g, Lava.b, 0.25f + 0.1f * Mathf.Sin(t * 4f));
            GUI.DrawTexture(new Rect(filled.x - rim, filled.y - rim, filled.width + rim * 2f, filled.height + rim * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(filled, fill);

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            labelStyle.fontSize = Mathf.RoundToInt(Mathf.Max(16f, Mathf.Min(w, h) * 0.04f));
            float lineH = labelStyle.fontSize * 1.6f;
            var words = new Rect(0f, bar.y - lineH * 1.5f, w, lineH);
            string text = $"Climbing out of the lava... {Mathf.RoundToInt(p * 100f)}%";
            GUI.color = Ash;
            GUI.Label(new Rect(words.x + 2f, words.y + 2f, words.width, words.height), text, labelStyle);
            GUI.color = new Color(1f, 0.82f, 0.6f);
            GUI.Label(words, text, labelStyle);
            if (failure != null)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, bar.yMax + barH * 2f, w, h * 0.1f), failure, labelStyle);
            }
        }
    }
}
