using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Flow;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// Simple per-scene component that defines what to preload for this level
    /// Add this to each Level scene and configure what you want to preload
    /// </summary>
    public class LevelPreloader : MonoBehaviour
    {
        [Header("What to Preload")]
        [Tooltip("Labels to download in background (e.g., Video1, Level2)")]
        [SerializeField] private List<string> labelsToPreload = new List<string>();

        [Header("Settings")]
        [SerializeField] private bool preloadOnStart = true;
        [SerializeField] private float delayBeforePreload = 0.5f; // Small delay to let scene settle

        [Header("WebGL Memory")]
        [Tooltip("On WebGL, wait for all SpriteFramePlayers in the scene to finish loading before preloading next assets")]
        [SerializeField] private bool waitForSceneAssetsOnWebGL = true;
        [Tooltip("Extra delay after scene assets load on WebGL (seconds)")]
        [SerializeField] private float webGLExtraDelay = 3f;

        private void Start()
        {
            if (preloadOnStart && labelsToPreload.Count > 0)
            {
#if UNITY_WEBGL
                if (waitForSceneAssetsOnWebGL)
                {
                    StartCoroutine(WaitForSceneAssetsThenPreload());
                    return;
                }
#endif
                Invoke(nameof(StartPreloading), delayBeforePreload);
            }
        }

#if UNITY_WEBGL
        /// <summary>
        /// On WebGL, wait for all IFrameSequencePlayer components in the scene to finish loading
        /// before preloading next-level assets. This prevents memory spikes from loading
        /// current level backgrounds and next level assets simultaneously.
        /// </summary>
        private IEnumerator WaitForSceneAssetsThenPreload()
        {
            // Wait for initial scene setup
            yield return new WaitForSeconds(delayBeforePreload);

            // Find all IFrameSequencePlayer implementations in the scene and wait for them to load.
            // We use MonoBehaviour search + interface cast to avoid a cross-assembly reference.
            var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            var players = new List<IFrameSequencePlayer>();
            foreach (var mb in allBehaviours)
            {
                if (mb is IFrameSequencePlayer fp)
                    players.Add(fp);
            }

            if (players.Count > 0)
            {
                Debug.Log($"[LevelPreloader] WebGL: Waiting for {players.Count} IFrameSequencePlayer(s) to load before preloading next assets...");

                bool allLoaded = false;
                while (!allLoaded)
                {
                    allLoaded = true;
                    foreach (var player in players)
                    {
                        if (player != null && !player.IsLoaded)
                        {
                            allLoaded = false;
                            break;
                        }
                    }
                    yield return null;
                }

                Debug.Log($"[LevelPreloader] WebGL: All IFrameSequencePlayers loaded. Waiting {webGLExtraDelay}s before preloading...");
                yield return new WaitForSeconds(webGLExtraDelay);
            }

            StartPreloading();
        }
#endif

        private void StartPreloading()
        {
            // Preloading labels

            foreach (string label in labelsToPreload)
            {
                PreloadManager.Instance.PreloadLabel(label, (success) => {
                    if (success)
                    {
                        // Successfully preloaded
                    }
                });
            }
        }

        /// <summary>
        /// Manually trigger preloading
        /// </summary>
        public void TriggerPreload()
        {
            StartPreloading();
        }
    }
}
