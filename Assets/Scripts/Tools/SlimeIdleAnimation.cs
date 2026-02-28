using UnityEngine;

namespace SickOfIt.Combat
{
    /// <summary>
    /// Simple code-based idle animation for slime using transform tweens.
    /// Much more reliable than AI-generated frame animation.
    /// </summary>
    public class SlimeIdleAnimation : MonoBehaviour
    {
        [Header("Breathing Animation")]
        [SerializeField] private float breathingSpeed = 1.5f;
        [SerializeField] private float breathingIntensity = 0.05f; // 5% scale change
        
        [Header("Wobble Animation")]
        [SerializeField] private float wobbleSpeed = 0.8f;
        [SerializeField] private float wobbleAngle = 3f; // degrees
        
        [Header("Vertical Float Animation")]
        [SerializeField] private float floatSpeed = 1.2f;
        [SerializeField] private float floatHeight = 0.1f;
        
        private Vector3 originalScale;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float timeOffset;
        
        private void Start()
        {
            originalScale = transform.localScale;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            
            // Random time offset so multiple slimes don't sync
            timeOffset = Random.Range(0f, Mathf.PI * 2f);
        }
        
        private void Update()
        {
            float time = Time.time + timeOffset;
            
            // Breathing (Y-scale pulsing)
            float breathingFactor = 1f + Mathf.Sin(time * breathingSpeed) * breathingIntensity;
            Vector3 newScale = originalScale;
            newScale.y *= breathingFactor;
            transform.localScale = newScale;
            
            // Wobble (slight rotation)
            float wobbleFactor = Mathf.Sin(time * wobbleSpeed) * wobbleAngle;
            transform.localRotation = originalRotation * Quaternion.Euler(0, 0, wobbleFactor);
            
            // Vertical floating
            float floatFactor = Mathf.Sin(time * floatSpeed) * floatHeight;
            Vector3 newPosition = originalPosition;
            newPosition.y += floatFactor;
            transform.localPosition = newPosition;
        }
        
        private void OnValidate()
        {
            // Clamp values to reasonable ranges
            breathingSpeed = Mathf.Clamp(breathingSpeed, 0.1f, 5f);
            breathingIntensity = Mathf.Clamp(breathingIntensity, 0f, 0.3f);
            wobbleSpeed = Mathf.Clamp(wobbleSpeed, 0.1f, 3f);
            wobbleAngle = Mathf.Clamp(wobbleAngle, 0f, 15f);
            floatSpeed = Mathf.Clamp(floatSpeed, 0.1f, 3f);
            floatHeight = Mathf.Clamp(floatHeight, 0f, 1f);
        }
    }
}