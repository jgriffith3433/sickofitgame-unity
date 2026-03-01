using UnityEngine;
using UnityEngine.Events;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Self-contained player health component.
    /// All health values come from RogueliteManager — no serialized max health.
    /// Implements IDamageable so DamageDealer/InstantDeathTrigger work generically.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Death Scene Settings")]
        [Tooltip("Name of the death scene to load (e.g., 'Death1', 'DeathAnimation', etc.)")]
        [SerializeField] private string deathSceneName = "Death1";

        [Header("Roguelite Settings")]
        [Tooltip("The level number this player is in. Set by LevelRuntime.")]
        [SerializeField] private int currentLevelNumber = 1;

        [Header("Invincibility")]
        [SerializeField] private bool hasInvincibilityFrames = true;
        [SerializeField] private float invincibilityDuration = 0.5f;
        private float invincibilityTimer;

        [Header("Events")]
        public UnityEvent<int> onHealthChanged;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;

        // Cached ref — set once in Awake, no GetComponent in Die()
        private SickOfIt.Core.Input.TopDownController controller;
        private int maxHealth;
        private int currentHealth;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsInvincible => invincibilityTimer > 0;

        /// <summary>
        /// Set current health directly (used by rewind system).
        /// </summary>
        public void SetHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            onHealthChanged?.Invoke(currentHealth);
        }

        private void Awake()
        {
            controller = GetComponentInParent<SickOfIt.Core.Input.TopDownController>();
        }

        private void Update()
        {
            if (invincibilityTimer > 0)
                invincibilityTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Set the death scene for this level. Called by LevelRuntime.
        /// </summary>
        public void SetDeathSceneName(string name)
        {
            if (!string.IsNullOrEmpty(name))
                deathSceneName = name;
        }

        /// <summary>
        /// Set the level number and apply roguelite health values.
        /// Called by LevelRuntime during level initialisation.
        /// </summary>
        public void InitializeForLevel(int levelNumber)
        {
            currentLevelNumber = levelNumber;
            maxHealth = RogueliteManager.Instance.GetMaxHealth();
            currentHealth = Mathf.Clamp(
                RogueliteManager.Instance.GetStartingHealthForLevel(currentLevelNumber),
                0, maxHealth);
            invincibilityTimer = 0f;
            onHealthChanged?.Invoke(currentHealth);
            Debug.Log($"[PlayerHealth] Level {currentLevelNumber}: Max={maxHealth}, Starting={currentHealth}");
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || IsInvincible)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            onDamaged?.Invoke();
            onHealthChanged?.Invoke(currentHealth);

            if (hasInvincibilityFrames)
                invincibilityTimer = invincibilityDuration;

            if (currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            onDeath?.Invoke();

            SickOfIt.Core.AnalyticsManager.Instance?.TrackPlayerDeath("combat");

            if (controller != null)
                controller.enabled = false;

            PlayDeathScene();
        }

        private void PlayDeathScene()
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            PlayerPrefs.SetString("DeathSceneToReload", currentScene.name);
            PlayerPrefs.Save();
            SceneTransitionManager.Instance.LoadDeathScene(deathSceneName);
        }
    }
}
