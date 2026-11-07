using UnityEngine;
using System.Collections.Generic;
using MobileControllerFramework;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Jumps on the finger going DOWN, anywhere on the screen, and broadcasts it
    /// through GameInputManager. Presses come from the Input System's event stream
    /// (EnhancedTouch), not from polling in Update, so a tap that begins and ends
    /// inside one frame, or in a hitch, still jumps; there is no duration or movement gate.
    /// Excludes configurable UI regions (e.g., fire analog area) so normal controls aren't interrupted.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GlobalTapJump : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private GameInputManager gameInputManager;

        [Header("Exclusion Zones")]
        [Tooltip("Don't trigger jump if tap starts in these UI regions (e.g., fire analog area).")]
        [SerializeField] private List<RectTransform> excludedRegions = new List<RectTransform>();

        [Tooltip("Automatically detect and exclude TouchRegionAnalog components.")]
        [SerializeField] private bool autoExcludeTouchRegions = false;

        [SerializeField] private Canvas parentCanvas;

        private void Awake()
        {
            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                parentCanvas = FindFirstObjectByType<Canvas>();

            if (autoExcludeTouchRegions)
            {
                foreach (var region in FindObjectsByType<TouchRegionAnalog>(FindObjectsSortMode.None))
                {
                    RectTransform rect = region.GetComponent<RectTransform>();
                    if (rect != null && !excludedRegions.Contains(rect))
                    {
                        excludedRegions.Add(rect);
                    }
                }
            }
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ETouch.onFingerDown += OnFingerDown;
        }

        private void OnDisable()
        {
            ETouch.onFingerDown -= OnFingerDown;
            EnhancedTouchSupport.Disable();
        }

        private void OnFingerDown(Finger finger)
        {
            Vector2 position = finger.currentTouch.screenPosition;
            // A tap on a HUD button (pause, rewind) presses the button, it doesn't jump.
            if (IsPositionInExcludedRegion(position) || TouchStickZone.Contains(position) || SickOfIt.Core.UI.Menus.MenuHit.OnButton(position))
                return;
            // The controller drops it during a rewind.
            gameInputManager.RequestJump();
        }

        private bool IsPositionInExcludedRegion(Vector2 screenPosition)
        {
            Camera cam = parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : Camera.main;

            foreach (RectTransform rect in excludedRegions)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, cam))
                    return true;
            }

            return false;
        }

        public void AddExcludedRegion(RectTransform region)
        {
            if (!excludedRegions.Contains(region))
                excludedRegions.Add(region);
        }

        public void RemoveExcludedRegion(RectTransform region)
        {
            excludedRegions.Remove(region);
        }
    }
}
