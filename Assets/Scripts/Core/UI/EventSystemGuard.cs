using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace SickOfIt.Core.UI
{
    /// <summary>
    /// Keeps exactly one EventSystem for the whole game. Scenes load additively and several of them (and the Camera
    /// prefab) carry their own EventSystem, so during every scene change two or three were active at once: Unity warns
    /// every frame and clicks can go to the wrong one. This keeps one persistent EventSystem and switches off (doesn't
    /// destroy, some live on cameras) every other one as scenes load. Code that needs UI input calls
    /// <see cref="Ensure"/> instead of creating its own.
    /// </summary>
    public static class EventSystemGuard
    {
        static EventSystem keeper;
        static float aggressiveUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // After a load, scene Awakes may re-enable a second EventSystem for a few frames: watch tightly for 2 s.
            aggressiveUntil = Time.unscaledTime + 2f;
            Ensure();
        }

        /// <summary>How many EventSystems are enabled and active in the hierarchy right now.</summary>
        public static int EnabledCount()
        {
            int n = 0;
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (es != null && es.enabled && es.gameObject.activeInHierarchy)
                    n++;
            return n;
        }

        /// <summary>Makes sure the one EventSystem exists and is the only enabled one; returns it.</summary>
        public static EventSystem Ensure()
        {
            if (!Application.isPlaying)
                return EventSystem.current;
            if (keeper == null || keeper.Equals(null))
            {
                keeper = null;
                // Prefer a scene / Camera-prefab EventSystem that already has an Input System UI module: its actions
                // stay wired. A brand-new empty module can miss pointer/touch events and leave Play / Continue dead
                // (backlog f_9ee4fb228e7d: smoke sits on the menu forever).
                foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (es == null || es.GetComponent<InputSystemUIInputModule>() == null)
                        continue;
                    keeper = es;
                    Object.DontDestroyOnLoad(es.gameObject);
                    if (es.GetComponent<Watcher>() == null)
                        es.gameObject.AddComponent<Watcher>();
                    break;
                }
                if (keeper == null)
                {
                    var go = new GameObject("[EventSystem]");
                    Object.DontDestroyOnLoad(go);
                    keeper = go.AddComponent<EventSystem>();
                    go.AddComponent<InputSystemUIInputModule>();
                    go.AddComponent<Watcher>();
                }
            }

            foreach (var other in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == null)
                    continue;
                // Legacy StandaloneInputModule fights the Input System module and can swallow clicks on WebGL.
                foreach (var legacy in other.GetComponents<StandaloneInputModule>())
                    if (legacy != null)
                        legacy.enabled = false;
                if (other == keeper)
                    continue;
                // Switch off even if already disabled so a late Awake that re-enables it loses its modules first.
                foreach (var module in other.GetComponents<BaseInputModule>())
                    module.enabled = false;
                other.enabled = false;
            }
            if (!keeper.enabled)
                keeper.enabled = true;
            if (!keeper.gameObject.activeSelf)
                keeper.gameObject.SetActive(true);
            foreach (var legacy in keeper.GetComponents<StandaloneInputModule>())
                if (legacy != null)
                    legacy.enabled = false;
            var ui = keeper.GetComponent<InputSystemUIInputModule>();
            if (ui != null && !ui.enabled)
                ui.enabled = true;
            foreach (var module in keeper.GetComponents<BaseInputModule>())
                if (module != null && !(module is StandaloneInputModule) && !module.enabled)
                    module.enabled = true;
            EventSystem.current = keeper;
            return keeper;
        }

        /// <summary>
        /// A scene's own EventSystem switches on in its Awake, before <c>sceneLoaded</c> fires, and one made later by a bridge
        /// or an additive scene never fires it at all: the guard looks again a few times a second, so a second one lives for
        /// a fraction of a second, never for a whole menu. Right after a load it checks every frame for 2 s.
        /// </summary>
        sealed class Watcher : MonoBehaviour
        {
            float next;

            void Update()
            {
                float now = Time.unscaledTime;
                float gap = now < aggressiveUntil ? 0f : 0.25f;
                if (now < next)
                    return;
                next = now + gap;
                Ensure();
            }
        }
    }
}
