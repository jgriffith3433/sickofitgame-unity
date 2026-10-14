using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using SickOfIt.Data;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Lets gameplay change without a rebuild: if GameDefinition.remoteContentUrl is set, the game downloads that
    /// content bundle at startup (built with SickOfIt > Game Config > Build Remote Content Bundle) and uses it
    /// instead of the content built into the game. A bundle that fails to download or has any error is ignored,
    /// so a bad upload can't break the game. Bundles can use only assets the build already contains.
    /// </summary>
    public sealed class RemoteContent : MonoBehaviour
    {
        public enum Status { Off, Loading, Loaded, Failed }

        const float TimeoutSeconds = 10f;

        public static Status State { get; private set; } = Status.Off;
        public static string Message { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor)
                return; // the editor always plays the local config
            var definition = GameDefinition.Instance;
            if (definition == null || string.IsNullOrEmpty(definition.remoteContentUrl))
                return;
            var go = new GameObject("RemoteContent");
            DontDestroyOnLoad(go);
            go.AddComponent<RemoteContent>().StartCoroutine(Fetch(definition.remoteContentUrl));
        }

        static IEnumerator Fetch(string url)
        {
            State = Status.Loading;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)TimeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail($"Couldn't download remote content from {url}: {request.error}. Using built-in content.");
                    yield break;
                }
                Apply(request.downloadHandler.text);
            }
        }

        /// <summary>Validates a bundle and switches to it if it has no errors. Returns true if it was used.</summary>
        public static bool Apply(string bundleJson)
        {
            var candidate = GameContent.FromDefinition(GameDefinition.Instance, bundleJson);
            ContentValidator.Validate(candidate, candidate.Issues);
            if (candidate.Issues.HasErrors)
            {
                Fail($"Remote content has {candidate.Issues.ErrorCount} error(s); using built-in content. First: {candidate.Issues.Find(i => i.Severity == SickOfIt.Json.JsonIssueSeverity.Error)}");
                return false;
            }
            GameContent.UseRemoteBundle(bundleJson);
            State = Status.Loaded;
            Message = $"Using remote content ({candidate.Entities.Count} entities, {candidate.LevelScripts.Count} level scripts).";
            Debug.Log("[RemoteContent] " + Message);
            return true;
        }

        static void Fail(string message)
        {
            State = Status.Failed;
            Message = message;
            Debug.LogWarning("[RemoteContent] " + message);
        }
    }
}
