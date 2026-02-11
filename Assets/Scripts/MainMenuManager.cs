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
    
    [Header("Scene Loading")]
    [SerializeField] private SceneLoadingManager sceneLoadingManager;

    private void Awake()
    {
        // Preload common shaders/materials to prevent pink shader bug
        PreloadManager.Instance.PreloadLabel("Common");
    }

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
        if (sceneLoadingManager != null)
            sceneLoadingManager.ContinueGame();
        else
            SceneTransitionManager.Instance.ContinueGame();
    }

    private void OnNewGameClicked()
    {
        Debug.Log("[MOBILE DEBUG] New Game button clicked!");
        
        // Reset all progress and start from level 1
        GameProgressManager.Instance.ResetAllProgress();
        GameProgressManager.Instance.InitializeNewPlayer();
        
        // Mark Level1 as visited to prevent auto-play loop
        PlayerPrefs.SetInt("Level_1_Visited", 1);
        
        // Play Video1 first, then load Level1
        PlayerPrefs.SetInt("ReturnToLevel", 1);
        PlayerPrefs.Save();
        
        Debug.Log("[MOBILE DEBUG] Attempting to load Video1...");
        Debug.Log($"[MOBILE DEBUG] SceneLoadingManager present: {sceneLoadingManager != null}");
        
        if (sceneLoadingManager != null)
            sceneLoadingManager.LoadVideo(1);
        else
            SceneTransitionManager.Instance.LoadVideo(1);
    }

    private void OnLevelSelectClicked()
    {
        SceneLoadingManager.ShowLoadingUI();
        SceneTransitionManager.Instance.LoadLevelSelection();
    }

    private void OnVideosClicked()
    {
        SceneLoadingManager.ShowLoadingUI();
        SceneTransitionManager.Instance.LoadVideoSelection();
    }

    private void OnQuitClicked()
    {
        SceneTransitionManager.Instance.QuitGame();
    }
}
