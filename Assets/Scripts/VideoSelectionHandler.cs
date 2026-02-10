using UnityEngine;

/// <summary>
/// Add this to VideoSelection scene to handle loading videos that return to VideoSelection
/// Place on the same GameObject as any UI handler for video buttons
/// </summary>
public class VideoSelectionHandler : MonoBehaviour
{
    /// <summary>
    /// Call this when player clicks a video button
    /// Video will return to VideoSelection after playback
    /// </summary>
    public void LoadVideoFromSelection(int videoNumber)
    {
        // Check if video is unlocked
        if (!GameProgressManager.Instance.IsVideoUnlocked(videoNumber))
        {
            Debug.LogWarning($"Video {videoNumber} is not unlocked yet!");
            return;
        }

        Debug.Log($"[VideoSelection] Loading Video{videoNumber}");
        
        // Set return destination to VideoSelection
        PlayerPrefs.SetInt("ReturnToLevel", 0); // Not used for VideoSelection return
        PlayerPrefs.SetString("ReturnScene", "VideoSelection");
        PlayerPrefs.Save();
        
        // Load the video
        SceneTransitionManager.Instance.LoadVideo(videoNumber);
    }
}
