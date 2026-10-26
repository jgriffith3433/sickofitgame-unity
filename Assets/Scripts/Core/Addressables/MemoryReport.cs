using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// What the page holds, in one line: the WebAssembly heap (it never shrinks, and a phone's browser reloads the page
    /// past its limit, iOS Safari first), the JavaScript heap where the browser tells (Chrome; decoded audio lives there),
    /// texture memory, loaded AssetBundles and what GameAssets holds (bundles, decoded audio). Logged as "[Memory] ..." a
    /// moment after every scene loads and after every GameAssets load, so a playtest's console shows how it climbs; shown on
    /// screen with dev=1 (DevKeys). Presentation only.
    /// </summary>
    public static class MemoryReport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SoiMemoryHeapMB();
        [DllImport("__Internal")] static extern int SoiMemoryJsMB();
#else
        static int SoiMemoryHeapMB() => -1;
        static int SoiMemoryJsMB() => -1;
#endif

        /// <summary>The WebAssembly heap in MB (-1 outside WebGL).</summary>
        public static int HeapMB => SoiMemoryHeapMB();
        /// <summary>The JavaScript heap in MB (-1 where the browser doesn't say).</summary>
        public static int JsMB => SoiMemoryJsMB();
        /// <summary>Highest heap seen this session (MB).</summary>
        public static int PeakHeapMB { get; private set; }

        const float Mb = 1024f * 1024f;

        public static string Line()
        {
            int heap = HeapMB;
            if (heap > PeakHeapMB)
                PeakHeapMB = heap;
            int js = JsMB;
            int bundles = AssetBundle.GetAllLoadedAssetBundles().Count();
            return $"heap {(heap >= 0 ? heap + " MB" : "?")} (peak {PeakHeapMB}) · js {(js >= 0 ? js + " MB" : "?")} · textures {Texture.currentTextureMemory / Mb:0} MB · " +
                   $"bundles {bundles} (art+sound {GameAssets.LoadedCount}, audio {GameAssets.AudioBytes / Mb:0} MB)";
        }

        public static void Log(string when) => Debug.Log($"[Memory] {when}: {Line()}");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Watch()
        {
            if (Application.isEditor)
                return;
            SceneManager.sceneLoaded += (scene, mode) => Delayed.Run(1.5f, () => Log("scene " + scene.name));
        }

        /// <summary>Runs an action after a moment of real time (a hidden object that outlives scenes).</summary>
        sealed class Delayed : MonoBehaviour
        {
            static Delayed instance;

            public static void Run(float seconds, System.Action action)
            {
                if (instance == null)
                {
                    var go = new GameObject("MemoryReport") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Delayed>();
                }
                instance.StartCoroutine(After(seconds, action));
            }

            static System.Collections.IEnumerator After(float seconds, System.Action action)
            {
                yield return new WaitForSecondsRealtime(seconds);
                action();
            }
        }
    }
}
