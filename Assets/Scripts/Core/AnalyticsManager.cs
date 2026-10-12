using UnityEngine;
using System.Runtime.InteropServices;

namespace SickOfIt.Core
{
    /// <summary>
    /// Manages analytics event tracking for the game using Umami Analytics.
    /// Uses two generic JavaScript interop functions — scales to any number of levels/videos.
    /// Event names match the existing website functions (e.g. "trackLevel1Start") for backward compat.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        private static AnalyticsManager _instance;
        private static bool _applicationIsQuitting;

        public static AnalyticsManager Instance
        {
            get
            {
                if (_applicationIsQuitting) return null;
                if (_instance == null)
                {
                    GameObject go = new GameObject("AnalyticsManager");
                    _instance = go.AddComponent<AnalyticsManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        // ============================================================
        // JavaScript Interop — two generic functions handle everything
        // ============================================================
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void trackEvent(string eventName);

        [DllImport("__Internal")]
        private static extern void trackEventWithData(string eventName, string data);
#endif

        // ============================================================
        // Internal helper — fires a named event via JS interop
        // ============================================================

        private void Track(string eventName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                trackEvent(eventName);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Analytics] Failed to track {eventName}: {e.Message}");
            }
#else
#endif
        }

        private void Track(string eventName, string data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                trackEventWithData(eventName, data);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Analytics] Failed to track {eventName}: {e.Message}");
            }
#else
#endif
        }

        // ============================================================
        // Public API — same signatures, now scales to any N
        // ============================================================

        /// <summary>Track when the game starts.</summary>
        public void TrackGameStart() => Track("trackGameStart");

        /// <summary>Track when a level starts. Works for any level number.</summary>
        public void TrackLevelStart(int levelNumber) => Track($"trackLevel{levelNumber}Start");

        /// <summary>Track when a level is completed. Works for any level number.</summary>
        public void TrackLevelComplete(int levelNumber) => Track($"trackLevel{levelNumber}Complete");

        /// <summary>Track when a video starts playing. Works for any video number.</summary>
        public void TrackVideoStart(int videoNumber) => Track($"trackVideo{videoNumber}Start");

        /// <summary>Track when a video completes. Works for any video number.</summary>
        public void TrackVideoComplete(int videoNumber) => Track($"trackVideo{videoNumber}Complete");

        /// <summary>Track when a video is skipped. Works for any video number.</summary>
        public void TrackVideoSkip(int videoNumber) => Track($"trackVideo{videoNumber}Skip");

        /// <summary>Track when the player dies with a cause string.</summary>
        public void TrackPlayerDeath(string cause = "unknown") => Track("trackPlayerDeath", cause);

        /// <summary>Track when the game is over.</summary>
        public void TrackGameOver() => Track("trackGameOver");
    }
}
