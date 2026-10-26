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

        float progress;
        string failure;

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
            for (int attempt = 1; ; attempt++)
            {
                failure = null;
                handle = AddressablesAPI.LoadAssetAsync<GameDefinition>(GameDefinition.Address);
                while (!handle.IsDone)
                {
                    progress = handle.GetDownloadStatus().Percent;
                    yield return null;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    // Never released: the game reads it until the page closes. Its art and sound are not in it: only what
                    // every screen uses ("core") loads now; the rest when a level, video or screen needs it (GameAssets).
                    Debug.Log($"[GameDefinitionBoot] Game data loaded from its bundles (try {attempt}).");
                    GameAssets.Init(handle.Result);
                    yield return GameAssets.Acquire(GameAssets.Core, new[] { GameAssets.Core }, p => progress = p);
                    GameDefinition.Provide(handle.Result);
                    int next = SceneManager.GetActiveScene().buildIndex + 1;
                    if (next < SceneManager.sceneCountInBuildSettings)
                        SceneManager.LoadScene(next);
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

        // Cosmetic only: nothing else is on screen while the bundles download.
        void OnGUI()
        {
            float w = Screen.width, h = Screen.height;
            float barW = Mathf.Min(w * 0.6f, 480f), barH = Mathf.Max(6f, h * 0.012f);
            var bar = new Rect((w - barW) / 2f, h * 0.6f, barW, barH);
            GUI.color = new Color(0.25f, 0.06f, 0.03f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.45f, 0.1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height), Texture2D.whiteTexture);
            if (failure != null)
            {
                GUI.color = Color.white;
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Mathf.Max(14f, h * 0.025f)) };
                GUI.Label(new Rect(0f, bar.yMax + barH * 2f, w, h * 0.1f), failure, style);
            }
        }
    }
}
