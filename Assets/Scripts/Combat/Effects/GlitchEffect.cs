using System.Collections;
using UnityEngine;
using SickOfIt.Combat.Health;

namespace SickOfIt.Combat.Effects
{
    /// <summary>
    /// Triggers a glitchy visual effect whenever the attached HealthSystem takes damage.
    /// Automatically finds all active SpriteRenderers in children each frame of the effect,
    /// so it works correctly with enemies that swap animator GameObjects (e.g. EnemyFighter).
    /// 
    /// Usage: Add this component alongside a HealthSystem. No other wiring needed.
    /// Can also be triggered manually via TriggerGlitch() on objects without a HealthSystem.
    /// </summary>
    public class GlitchEffect : MonoBehaviour
    {
        [Header("Flash Settings")]
        [Tooltip("Color to flash when hit")]
        [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [Tooltip("How many times the flash blinks")]
        [SerializeField] private int flashCount = 2;
        [Tooltip("Duration of each flash (on)")]
        [SerializeField] private float flashOnDuration = 0.08f;
        [Tooltip("Duration between flashes (off)")]
        [SerializeField] private float flashOffDuration = 0.06f;
        [Tooltip("How strongly the flash color overrides the original (0 = none, 1 = full)")]
        [SerializeField] [Range(0f, 1f)] private float flashIntensity = 0.5f;

        private HealthSystem healthSystem;
        private Coroutine activeGlitch;

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (healthSystem != null)
                healthSystem.onDamaged.AddListener(TriggerGlitch);
        }

        private void OnDisable()
        {
            if (healthSystem != null)
                healthSystem.onDamaged.RemoveListener(TriggerGlitch);

            // Restore visuals if we get disabled mid-glitch
            RestoreAllRenderers();
        }

        /// <summary>
        /// Trigger the glitch effect. Called automatically on damage, but can also be invoked manually.
        /// </summary>
        public void TriggerGlitch()
        {
            if (!isActiveAndEnabled)
                return;

            // Restart if already running
            if (activeGlitch != null)
                StopCoroutine(activeGlitch);

            activeGlitch = StartCoroutine(GlitchCoroutine());
        }

        private IEnumerator GlitchCoroutine()
        {
            for (int i = 0; i < flashCount; i++)
            {
                // Flash on — tint all active renderers toward the flash color
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(false);
                foreach (SpriteRenderer sr in renderers)
                {
                    if (sr == null) continue;
                    sr.color = Color.Lerp(Color.white, flashColor, flashIntensity);
                }

                yield return new WaitForSeconds(flashOnDuration);

                // Flash off — restore to normal
                RestoreAllRenderers();

                if (i < flashCount - 1)
                    yield return new WaitForSeconds(flashOffDuration);
            }

            // Final safety restore
            RestoreAllRenderers();
            activeGlitch = null;
        }

        /// <summary>
        /// Resets all active child SpriteRenderers to full white (alpha 1).
        /// This works because sprite renderers default to Color.white as their tint,
        /// and the actual sprite colors come from the sprite texture itself.
        /// </summary>
        private void RestoreAllRenderers()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(false);
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                sr.color = Color.white;
            }
        }
    }
}
