using UnityEngine;

/// <summary>
/// Base manager for individual level scenes. Add this to each level scene and call CompleteLevel() when the player wins.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int levelNumber = 1;
    
    [Header("Optional: Auto-play video on first visit")]
    [SerializeField] private bool playVideoOnFirstVisit = true;
    [SerializeField] private int videoToPlay = 0; // Which video to play (usually matches level number)

    [Header("Performance: Preload next level")]
    [SerializeField] private bool preloadNextLevel = true; // Preload next level during gameplay for instant transitions

    private bool hasPlayedVideo = false;

    private void Start()
    {
        // Preload the next level in the background for faster transitions (if using Addressables)
        if (preloadNextLevel)
        {
            SceneTransitionManager.Instance.PreloadNextLevel(levelNumber);
        }

        // Check if we should play the intro video for this level
        if (playVideoOnFirstVisit && !hasPlayedVideo)
        {
            if (!HasVisitedLevelBefore())
            {
                // Mark as visited before playing video so we don't loop
                MarkLevelAsVisited();
                
                // Play the video if it's unlocked
                if (GameProgressManager.Instance.IsVideoUnlocked(videoToPlay))
                {
                    PlayLevelVideo();
                    return; // Video manager will return to this level after video
                }
            }
        }
    }

    /// <summary>
    /// Call this method when the player completes the level's win condition
    /// </summary>
    public void CompleteLevel()
    {
        Debug.Log($"Level {levelNumber} completed!");
        
        // Mark level as complete
        GameProgressManager.Instance.SetLevelCompleted(levelNumber);
        
        // Unlock the NEXT level's video
        int nextLevelVideo = levelNumber + 1;
        GameProgressManager.Instance.UnlockVideo(nextLevelVideo);
        
        // You can add completion effects here (particles, sounds, etc.)
        
        // After a short delay, play next level's video OR go to next level
        Invoke(nameof(ProceedToNextLevel), 2f);
    }

    private void ProceedToNextLevel()
    {
        int nextLevel = levelNumber + 1;
        int nextLevelVideo = levelNumber + 1;
        
        // Play the next level's video (if it exists and hasn't been seen)
        if (GameProgressManager.Instance.IsVideoUnlocked(nextLevelVideo))
        {
            // Store which level to load after the video
            PlayerPrefs.SetInt("ReturnToLevel", nextLevel);
            PlayerPrefs.Save();
            
            SceneTransitionManager.Instance.LoadVideo(nextLevelVideo);
        }
        else
        {
            // No video, go directly to next level or level select
            SceneTransitionManager.Instance.LoadLevelSelection();
        }
    }

    public void ReturnToLevelSelect()
    {
        SceneTransitionManager.Instance.LoadLevelSelection();
    }

    public void ReturnToMainMenu()
    {
        SceneTransitionManager.Instance.LoadMainMenu();
    }

    public void RestartLevel()
    {
        SceneTransitionManager.Instance.LoadLevel(levelNumber);
    }

    private void PlayLevelVideo()
    {
        // Store which level to return to after video
        PlayerPrefs.SetInt("ReturnToLevel", levelNumber);
        PlayerPrefs.Save();
        
        SceneTransitionManager.Instance.LoadVideo(videoToPlay);
    }

    private bool HasVisitedLevelBefore()
    {
        return PlayerPrefs.GetInt($"Level_{levelNumber}_Visited", 0) == 1;
    }

    private void MarkLevelAsVisited()
    {
        PlayerPrefs.SetInt($"Level_{levelNumber}_Visited", 1);
        PlayerPrefs.Save();
    }
}
