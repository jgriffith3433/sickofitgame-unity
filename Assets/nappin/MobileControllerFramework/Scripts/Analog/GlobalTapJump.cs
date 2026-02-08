using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace MobileControllerFramework
{
    /// <summary>
    /// Detects tap gestures anywhere on the screen and triggers jump.
    /// Works even when tapping on analog controls or UI elements.
    /// Useful for mobile games where you want jump to be accessible from anywhere.
    /// </summary>
    public class GlobalTapJump : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TopDownController playerController;
        
        [Header("Tap Detection")]
        [Tooltip("Maximum duration for a tap (seconds). Longer presses won't trigger jump.")]
        [SerializeField] private float maxTapDuration = 0.2f;
        
        [Tooltip("Maximum movement distance (screen pixels) to still count as a tap.")]
        [SerializeField] private float maxTapMovement = 30f;
        
        [Header("Exclusion Zones")]
        [Tooltip("Don't trigger jump if tap starts in these UI regions (e.g., fire analog area).")]
        [SerializeField] private List<RectTransform> excludedRegions = new List<RectTransform>();
        
        [Tooltip("Automatically detect and exclude TouchRegionAnalog components. WARNING: Excludes ALL touch regions - usually you only want to exclude fire region!")]
        [SerializeField] private bool autoExcludeTouchRegions = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        private Dictionary<int, TouchData> activeTouches = new Dictionary<int, TouchData>();
        private Canvas parentCanvas;
        
        private class TouchData
        {
            public float startTime;
            public Vector2 startPosition;
            public bool isInExcludedRegion;
        }
        
        private void Awake()
        {
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<TopDownController>();
                if (playerController == null)
                {
                    Debug.LogError("[GlobalTapJump] TopDownController not found!");
                    enabled = false;
                    return;
                }
            }
            
            // Find parent canvas for position calculations
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindFirstObjectByType<Canvas>();
            }
            
            // Auto-detect TouchRegionAnalog components to exclude
            if (autoExcludeTouchRegions)
            {
                TouchRegionAnalog[] touchRegions = FindObjectsByType<TouchRegionAnalog>(FindObjectsSortMode.None);
                foreach (var region in touchRegions)
                {
                    RectTransform rect = region.GetComponent<RectTransform>();
                    if (rect != null && !excludedRegions.Contains(rect))
                    {
                        excludedRegions.Add(rect);
                        if (debugMode)
                        {
                            Debug.Log($"[GlobalTapJump] Auto-excluded region: {region.gameObject.name}");
                        }
                    }
                }
            }
        }
        
        private void Update()
        {
            ProcessTouches();
        }
        
        private void ProcessTouches()
        {
            // Handle touch input (mobile)
            if (Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    ProcessPointerInput(touch.fingerId, touch.position, touch.phase);
                }
            }
            // Handle mouse input (PC testing)
            else if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                TouchPhase phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began : TouchPhase.Ended;
                ProcessPointerInput(0, Input.mousePosition, phase);
            }
        }
        
        private void ProcessPointerInput(int pointerId, Vector2 position, TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began:
                    OnTouchBegan(pointerId, position);
                    break;
                    
                case TouchPhase.Ended:
                    OnTouchEnded(pointerId, position);
                    break;
                    
                case TouchPhase.Canceled:
                    OnTouchCanceled(pointerId);
                    break;
            }
        }
        
        private void OnTouchBegan(int pointerId, Vector2 position)
        {
            // Check if touch is in excluded region
            bool inExcludedRegion = IsPositionInExcludedRegion(position);
            
            activeTouches[pointerId] = new TouchData
            {
                startTime = Time.time,
                startPosition = position,
                isInExcludedRegion = inExcludedRegion
            };
            
            if (debugMode)
            {
                Debug.Log($"[GlobalTapJump] Touch {pointerId} began. Excluded: {inExcludedRegion}");
            }
        }
        
        private void OnTouchEnded(int pointerId, Vector2 position)
        {
            if (!activeTouches.TryGetValue(pointerId, out TouchData touchData))
                return;
            
            // Calculate tap metrics
            float duration = Time.time - touchData.startTime;
            float movement = Vector2.Distance(position, touchData.startPosition);
            
            // Check if this is a valid tap
            bool isTap = duration <= maxTapDuration && movement <= maxTapMovement;
            bool shouldTriggerJump = isTap && !touchData.isInExcludedRegion;
            
            if (debugMode)
            {
                Debug.Log($"[GlobalTapJump] Touch {pointerId} ended. Duration: {duration:F3}s, Movement: {movement:F1}px, IsTap: {isTap}, TriggerJump: {shouldTriggerJump}");
            }
            
            if (shouldTriggerJump)
            {
                playerController.OnJumpButtonPressed();
                
                if (debugMode)
                {
                    Debug.Log("[GlobalTapJump] Jump triggered!");
                }
            }
            
            activeTouches.Remove(pointerId);
        }
        
        private void OnTouchCanceled(int pointerId)
        {
            activeTouches.Remove(pointerId);
            
            if (debugMode)
            {
                Debug.Log($"[GlobalTapJump] Touch {pointerId} canceled.");
            }
        }
        
        private bool IsPositionInExcludedRegion(Vector2 screenPosition)
        {
            foreach (RectTransform excludedRect in excludedRegions)
            {
                if (excludedRect == null || !excludedRect.gameObject.activeInHierarchy)
                    continue;
                
                if (RectTransformUtility.RectangleContainsScreenPoint(
                    excludedRect,
                    screenPosition,
                    parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Manually add a region to exclude from tap jump detection.
        /// </summary>
        public void AddExcludedRegion(RectTransform region)
        {
            if (!excludedRegions.Contains(region))
            {
                excludedRegions.Add(region);
            }
        }
        
        /// <summary>
        /// Remove a region from exclusion list.
        /// </summary>
        public void RemoveExcludedRegion(RectTransform region)
        {
            excludedRegions.Remove(region);
        }
    }
}
