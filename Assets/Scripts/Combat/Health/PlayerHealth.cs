using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Input;
using SickOfIt.Data;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Player-specific health management with game over handling.
    /// Integrates with RogueliteManager for cumulative health.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Death Scene Settings")]
        [Tooltip("Name of the death scene to load (e.g., 'Death1', 'DeathAnimation', etc.)")]
        [SerializeField] private string deathSceneName = "Death1";

        [Header("Roguelite Settings")]
        [Tooltip("The level number this player is in (used for cumulative health). Set by LevelManager.")]
        [SerializeField] private int currentLevelNumber = 1;

        private HealthSystem healthSystem;

        public HealthSystem HealthSystem => healthSystem;

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (healthSystem != null)
            {
                healthSystem.onDeath.AddListener(OnPlayerDeath);
            }
        }

        private void OnDisable()
        {
            if (healthSystem != null)
            {
                healthSystem.onDeath.RemoveListener(OnPlayerDeath);
            }
        }

        /// <summary>
        /// Set the level number and apply cumulative health from RogueliteManager.
        /// Called by LevelManager during level initialization.
        /// </summary>
        public void InitializeForLevel(int levelNumber)
        {
            currentLevelNumber = levelNumber;
            ApplyCumulativeHealth();
        }

        /// <summary>
        /// Apply the cumulative health from previous levels.
        /// </summary>
        private void ApplyCumulativeHealth()
        {
            if (healthSystem == null) return;

            int startingHealth = RogueliteManager.Instance.GetStartingHealthForLevel(currentLevelNumber);
            healthSystem.SetCurrentHealth(startingHealth);
            Debug.Log($"[PlayerHealth] Level {currentLevelNumber}: Starting with {startingHealth}/{healthSystem.MaxHealth} health (cumulative).");
        }

        private void OnPlayerDeath()
        {
            // Track player death event
            SickOfIt.Core.AnalyticsManager.Instance.TrackPlayerDeath("combat");

            // Disable player controls
            var controller = GetComponentInParent<TopDownController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            // Play death scene which will reload the level after animation
            PlayDeathScene();
        }

        private void PlayDeathScene()
        {
            // Store which scene to reload after death animation
            UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            PlayerPrefs.SetString("DeathSceneToReload", currentScene.name);
            PlayerPrefs.Save();

            // Load the death scene
            SceneTransitionManager.Instance.LoadDeathScene(deathSceneName);
        }
    }
}
