using UnityEngine;
using TMPro;
using MobileControllerFramework;

/// <summary>
/// Manages the Main Menu scene
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button videosButton;
    [SerializeField] private Button quitButton;
    
    [Header("Optional UI Elements")]
    [SerializeField] private TextMeshProUGUI continueButtonText;

    private void Start()
    {
        // Initialize game progress for first-time players
        GameProgressManager.Instance.InitializeNewPlayer();
        
        SetupButtons();
        UpdateContinueButton();
    }

    private void SetupButtons()
    {
        if (continueButton != null)
            continueButton.TapEndEvent.AddListener(OnContinueClicked);
        
        if (newGameButton != null)
            newGameButton.TapEndEvent.AddListener(OnNewGameClicked);
        
        if (levelSelectButton != null)
            levelSelectButton.TapEndEvent.AddListener(OnLevelSelectClicked);
        
        if (videosButton != null)
            videosButton.TapEndEvent.AddListener(OnVideosClicked);
        
        if (quitButton != null)
            quitButton.TapEndEvent.AddListener(OnQuitClicked);
    }

    private void UpdateContinueButton()
    {
        if (continueButton != null)
        {
            int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
            
            // Update button text if available
            if (continueButtonText != null)
            {
                continueButtonText.text = $"Continue (Level {currentLevel})";
            }
        }
    }

    private void OnContinueClicked()
    {
        SceneTransitionManager.Instance.ContinueGame();
    }

    private void OnNewGameClicked()
    {
        // Reset all progress and start from level 1
        GameProgressManager.Instance.ResetAllProgress();
        GameProgressManager.Instance.InitializeNewPlayer();
        SceneTransitionManager.Instance.LoadLevel(1);
    }

    private void OnLevelSelectClicked()
    {
        SceneTransitionManager.Instance.LoadLevelSelection();
    }

    private void OnVideosClicked()
    {
        SceneTransitionManager.Instance.LoadVideoSelection();
    }

    private void OnQuitClicked()
    {
        SceneTransitionManager.Instance.QuitGame();
    }
}
