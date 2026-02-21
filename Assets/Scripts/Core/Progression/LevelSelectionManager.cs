using UnityEngine;
using TMPro;
using MobileControllerFramework;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Level Selection scene. Dynamically creates or updates level buttons.
    /// Displays per-level health info and cumulative health for the roguelite system.
    /// </summary>
    public class LevelSelectionManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private GameConfiguration gameConfiguration;

        [Header("UI References")]
        [SerializeField] private Transform levelButtonContainer; // Parent object for level buttons
        [SerializeField] private GameObject levelButtonPrefab; // Prefab for level buttons
        [SerializeField] private MobileControllerFramework.Button backButton;

        [Header("Roguelite UI")]
        [Tooltip("Displays cumulative health on the level selection screen.")]
        [SerializeField] private TextMeshProUGUI cumulativeHealthText;

        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        private void Start()
        {
            GenerateLevelButtons();
            UpdateCumulativeHealthDisplay();

            if (backButton != null)
                backButton.TapEndEvent.AddListener(OnBackClicked);
        }

        private void UpdateCumulativeHealthDisplay()
        {
            if (cumulativeHealthText != null && gameConfiguration != null)
            {
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int cumulativeHealth = RogueliteManager.Instance.GetCumulativeHealth(gameConfiguration.TotalLevels);
                cumulativeHealthText.text = $"Yooba's Health: {cumulativeHealth} / {maxHealth}";
            }
        }

        private void GenerateLevelButtons()
        {
            if (levelButtonContainer == null || levelButtonPrefab == null)
            {
                Debug.LogError("LevelSelectionManager: Missing references! Please assign levelButtonContainer and levelButtonPrefab in the inspector.");
                return;
            }

            if (gameConfiguration == null)
            {
                Debug.LogError("LevelSelectionManager: GameConfiguration is not assigned!");
                return;
            }

            // Clear existing buttons
            foreach (Transform child in levelButtonContainer)
            {
                Destroy(child.gameObject);
            }

            int highestReached = GameProgressManager.Instance.GetHighestLevelReached();

            // Create buttons for each level
            for (int i = 1; i <= gameConfiguration.TotalLevels; i++)
            {
                int levelNumber = i; // Capture for lambda

                GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
                LevelButton levelButton = buttonObj.GetComponent<LevelButton>();

                if (levelButton != null)
                {
                    bool isCompleted = GameProgressManager.Instance.IsLevelCompleted(levelNumber);
                    bool isUnlocked = levelNumber <= highestReached;

                    // Get roguelite health data for this level
                    int endHealth = RogueliteManager.Instance.GetLevelEndHealth(levelNumber);
                    int maxHealth = RogueliteManager.Instance.GetLevelMaxHealth(levelNumber);

                    levelButton.Setup(levelNumber, isCompleted, isUnlocked, () => OnLevelClicked(levelNumber), endHealth, maxHealth);
                }
            }
        }

        private void OnLevelClicked(int levelNumber)
        {
            // For Level 1, check if it's the first time visiting
            if (levelNumber == 1 && !HasVisitedLevel(1))
            {
                // Mark as visited and play Video1 first
                MarkLevelAsVisited(1);
                PlayerPrefs.SetInt("ReturnToLevel", 1);
                PlayerPrefs.Save();

                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadVideo(1);
                else
                    SceneTransitionManager.Instance.LoadVideo(1);
            }
            else
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(levelNumber);
                else
                    SceneTransitionManager.Instance.LoadLevel(levelNumber);
            }
        }

        private bool HasVisitedLevel(int levelNumber)
        {
            return PlayerPrefs.GetInt($"Level_{levelNumber}_Visited", 0) == 1;
        }

        private void MarkLevelAsVisited(int levelNumber)
        {
            PlayerPrefs.SetInt($"Level_{levelNumber}_Visited", 1);
            PlayerPrefs.Save();
        }

        private void OnBackClicked()
        {
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }
}
