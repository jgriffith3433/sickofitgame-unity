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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>Makes sure the one EventSystem exists and is the only enabled one; returns it.</summary>
        public static EventSystem Ensure()
        {
            if (!Application.isPlaying)
                return EventSystem.current;
            if (keeper == null)
            {
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
                if (other == keeper)
                    continue;
                // Switch off even if already disabled so a late Awake that re-enables it loses its modules first.
                foreach (var module in other.GetComponents<BaseInputModule>())
                    module.enabled = false;
                other.enabled = false;
            }
            if (!keeper.enabled)
                keeper.enabled = true;
            foreach (var module in keeper.GetComponents<BaseInputModule>())
                if (module != null && !module.enabled)
                    module.enabled = true;
            EventSystem.current = keeper;
            return keeper;
        }

        /// <summary>
        /// A scene's own EventSystem switches on in its Awake, before <c>sceneLoaded</c> fires, and one made later by a bridge
        /// or an additive scene never fires it at all: the guard looks again a few times a second, so a second one lives for
        /// a fraction of a second, never for a whole menu.
        /// </summary>
        sealed class Watcher : MonoBehaviour
        {
            float next;

            void Update()
            {
                if (Time.unscaledTime < next)
                    return;
                next = Time.unscaledTime + 0.25f;
                Ensure();
            }
        }
    }
}
