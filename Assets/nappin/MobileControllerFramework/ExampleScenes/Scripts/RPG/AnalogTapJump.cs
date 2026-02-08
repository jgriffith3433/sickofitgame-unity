using UnityEngine;
using UnityEngine.EventSystems;

namespace MobileControllerFramework
{
    /// <summary>
    /// Detects taps on an Analog stick and triggers jump on the player controller
    /// </summary>
    public class AnalogTapJump : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private TopDownController playerController;
        [Tooltip("Maximum time for a tap (seconds)")]
        [SerializeField] private float maxTapDuration = 0.2f;
        [Tooltip("Maximum stick movement to still count as a tap")]
        [SerializeField] private float maxTapMovement = 0.15f;
        
        private Analog analog;
        private float pointerDownTime;
        private bool isPointerDown;
        private float maxStickDistanceDuringPress;
        
        private void Awake()
        {
            analog = GetComponent<Analog>();
            
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<TopDownController>();
            }
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDownTime = Time.time;
            isPointerDown = true;
            maxStickDistanceDuringPress = 0f;
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPointerDown) return;
            
            float pressDuration = Time.time - pointerDownTime;
            isPointerDown = false;
            
            // Check if this was a tap (short duration + minimal stick movement)
            if (pressDuration <= maxTapDuration && maxStickDistanceDuringPress <= maxTapMovement)
            {
                // This was a tap, trigger jump
                if (playerController != null)
                {
                    playerController.OnJumpButtonPressed();
                }
            }
        }
        
        private void Update()
        {
            // Track maximum stick movement during press
            if (isPointerDown && analog != null)
            {
                float currentDistance = analog.GetStickPosition().magnitude;
                if (currentDistance > maxStickDistanceDuringPress)
                {
                    maxStickDistanceDuringPress = currentDistance;
                }
            }
        }
    }
}
