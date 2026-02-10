using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// Manages video playback in video scenes
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerManager : MonoBehaviour
{
    [Header("Video Settings")]
    [SerializeField] private int videoNumber = 0;
    [SerializeField] private bool returnToLevelAfterVideo = false; // If true, returns to the level that triggered this video
    
    [Header("UI References")]
    [SerializeField] private Button skipButton;
    [SerializeField] private GameObject skipButtonObject; // To show/hide skip option
    
    private VideoPlayer videoPlayer;
    private bool hasFinished = false;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    private void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.Play();
        }

        // Setup skip button
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        // Check if we should enable skipping (e.g., if player has already seen this video)
        if (skipButtonObject != null)
        {
            bool hasSeenBefore = HasSeenVideoBefore();
            skipButtonObject.SetActive(hasSeenBefore);
        }
        
        MarkVideoAsSeen();
    }

    private void Update()
    {
        // Allow ESC or Space to skip video
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
        {
            OnSkipClicked();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (!hasFinished)
        {
            hasFinished = true;
            ReturnToGame();
        }
    }

    private void OnSkipClicked()
    {
        if (!hasFinished)
        {
            hasFinished = true;
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
            ReturnToGame();
        }
    }

    private void ReturnToGame()
    {
        // Check if there's a specific level to return to
        if (PlayerPrefs.HasKey("ReturnToLevel"))
        {
            int levelToReturn = PlayerPrefs.GetInt("ReturnToLevel");
            string returnScene = PlayerPrefs.GetString("ReturnScene", "Level");
            
            PlayerPrefs.DeleteKey("ReturnToLevel");
            PlayerPrefs.DeleteKey("ReturnScene");
            PlayerPrefs.Save();
            
            // Determine where to return based on ReturnScene
            if (returnScene == "Level")
            {
                SceneTransitionManager.Instance.LoadLevel(levelToReturn);
            }
            else if (returnScene == "MainMenu")
            {
                SceneTransitionManager.Instance.LoadMainMenu();
            }
            else if (returnScene == "LevelSelection")
            {
                SceneTransitionManager.Instance.LoadLevelSelection();
            }
            else if (returnScene == "VideoSelection")
            {
                SceneTransitionManager.Instance.LoadVideoSelection();
            }
            else
            {
                // Fallback
                SceneTransitionManager.Instance.LoadLevel(levelToReturn);
            }
        }
        else if (returnToLevelAfterVideo)
        {
            // Legacy path: no specific level set but returnToLevel flag is true
            // This shouldn't happen in normal flow, return to main menu as fallback
            SceneTransitionManager.Instance.LoadMainMenu();
        }
        else
        {
            // Return to main menu or video selection
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }

    private bool HasSeenVideoBefore()
    {
        return PlayerPrefs.GetInt($"Video_{videoNumber}_Seen", 0) == 1;
    }

    private void MarkVideoAsSeen()
    {
        PlayerPrefs.SetInt($"Video_{videoNumber}_Seen", 1);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
