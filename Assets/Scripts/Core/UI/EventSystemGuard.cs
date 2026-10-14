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
                var go = new GameObject("[EventSystem]");
                Object.DontDestroyOnLoad(go);
                keeper = go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
            }

            foreach (var other in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == keeper || !other.enabled)
                    continue;
                foreach (var module in other.GetComponents<BaseInputModule>())
                    module.enabled = false;
                other.enabled = false;
            }
            if (!keeper.enabled)
                keeper.enabled = true;
            EventSystem.current = keeper;
            return keeper;
        }
    }
}
