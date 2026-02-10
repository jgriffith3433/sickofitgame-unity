using UnityEngine;

/// <summary>
/// Add this to LevelSelection scene to handle video playback before loading levels
/// Place on the same GameObject as any UI handler for level buttons
/// </summary>
public class LevelSelectionVideoHandler : MonoBehaviour
{
    /// <summary>
    /// Call this when player clicks a level button
    /// It will check if video should be played first
    /// </summary>
    public void LoadLevelWithVideoCheck(int levelNumber)
    {
        // Check if player should see the video first
        if (ShouldPlayVideoBeforeLevel(levelNumber))
        {
            Debug.Log($"[LevelSelection] Playing Video{levelNumber} before Level{levelNumber}");
            PreloadVideoThenLevel(levelNumber);
        }
        else
        {
            Debug.Log($"[LevelSelection] Loading Level{levelNumber} directly (video already seen)");
            LoadLevelDirectly(levelNumber);
        }
    }

    /// <summary>
    /// Check if the player should see the video before entering the level
    /// </summary>
    private bool ShouldPlayVideoBeforeLevel(int levelNumber)
    {
        // Check if video is unlocked
        if (!GameProgressManager.Instance.IsVideoUnlocked(levelNumber))
            return false;
        
        // Check if player has already seen this video
        return !HasSeenVideoBefore(levelNumber);
    }

    private bool HasSeenVideoBefore(int videoNumber)
    {
        return PlayerPrefs.GetInt($"Video_{videoNumber}_Seen", 0) == 1;
    }

    /// <summary>
    /// Preload video, play it, then go to level
    /// </summary>
    private void PreloadVideoThenLevel(int levelNumber)
    {
        string videoLabel = $"Video{levelNumber}";
        
        PreloadManager.Instance.PreloadLabel(videoLabel, (success) =>
        {
            if (success)
            {
                // Set which level to load after video
                PlayerPrefs.SetInt("ReturnToLevel", levelNumber);
                PlayerPrefs.SetString("ReturnScene", "Level");
                PlayerPrefs.Save();
                
                // Load the video
                SceneTransitionManager.Instance.LoadVideo(levelNumber);
            }
            else
            {
                // Failed to download video, go to level directly
                Debug.LogWarning($"Failed to download video{levelNumber}, loading level directly");
                LoadLevelDirectly(levelNumber);
            }
        });
    }

    /// <summary>
    /// Load level directly without video
    /// </summary>
    private void LoadLevelDirectly(int levelNumber)
    {
        string levelLabel = $"Level{levelNumber}";
        
        PreloadManager.Instance.PreloadLabel(levelLabel, (success) =>
        {
            if (success)
            {
                SceneTransitionManager.Instance.LoadLevel(levelNumber);
            }
            else
            {
                Debug.LogError($"Failed to download Level{levelNumber}");
            }
        });
    }
}
