using UnityEngine;
using MobileControllerFramework;

/// <summary>
/// Manages the Level Selection scene. Dynamically creates or updates level buttons.
/// </summary>
public class LevelSelectionManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int totalLevels = 10; // Set this to your total number of levels
    
    [Header("UI References")]
    [SerializeField] private Transform levelButtonContainer; // Parent object for level buttons
    [SerializeField] private GameObject levelButtonPrefab; // Prefab for level buttons
    [SerializeField] private Button backButton;

    private void Start()
    {
        GenerateLevelButtons();
        
        if (backButton != null)
            backButton.TapEndEvent.AddListener(OnBackClicked);
    }

    private void GenerateLevelButtons()
    {
        if (levelButtonContainer == null || levelButtonPrefab == null)
        {
            Debug.LogError("LevelSelectionManager: Missing references! Please assign levelButtonContainer and levelButtonPrefab in the inspector.");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in levelButtonContainer)
        {
            Destroy(child.gameObject);
        }

        int highestReached = GameProgressManager.Instance.GetHighestLevelReached();

        // Create buttons for each level
        for (int i = 1; i <= totalLevels; i++)
        {
            int levelNumber = i; // Capture for lambda
            
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            LevelButton levelButton = buttonObj.GetComponent<LevelButton>();
            
            if (levelButton != null)
            {
                bool isCompleted = GameProgressManager.Instance.IsLevelCompleted(levelNumber);
                bool isUnlocked = levelNumber <= highestReached;
                
                levelButton.Setup(levelNumber, isCompleted, isUnlocked, () => OnLevelClicked(levelNumber));
            }
        }
    }

    private void OnLevelClicked(int levelNumber)
    {
        SceneTransitionManager.Instance.LoadLevel(levelNumber);
    }

    private void OnBackClicked()
    {
        SceneTransitionManager.Instance.LoadMainMenu();
    }
}
