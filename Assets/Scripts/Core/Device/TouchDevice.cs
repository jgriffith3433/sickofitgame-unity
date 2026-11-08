using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SickOfIt.Core.Device
{
    /// <summary>
    /// Whether this player's main pointer is a finger, so the game shows its touch controls and "tap/drag" wording.
    /// Unity's <c>Application.isMobilePlatform</c> misses an iPad in Safari (it says it is a Mac) and touch-first tablets,
    /// which then got no move stick. In a WebGL build this also asks the page (<c>MobileDetection.jslib</c>): an
    /// iPhone/iPad/Android browser, an iPad that calls itself a Mac (touch points &gt; 1), or a coarse main pointer. A
    /// laptop with a touch screen and a mouse keeps the desktop controls. The page's <c>?touch=1</c> / <c>?touch=0</c>
    /// forces it either way, like the page's own guide. Asked once, then cached.
    /// </summary>
    public static class TouchDevice
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int SickOfItTouchPrimary();
#endif

        static int cached = -1;

        /// <summary>True on a phone or tablet (or with ?touch=1).</summary>
        public static bool IsPrimary
        {
            get
            {
                if (cached < 0)
                    cached = Detect() ? 1 : 0;
                return cached == 1;
            }
        }

        static bool Detect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return SickOfItTouchPrimary() != 0; }
            catch { return Application.isMobilePlatform; }
#else
            return Application.isMobilePlatform;
#endif
        }
    }
}
