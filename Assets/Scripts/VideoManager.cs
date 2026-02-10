using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manager for video scenes. Handles loading of SpriteFramePlayer and manages scene activation.
/// Also preloads next level during video playback for seamless transitions.
/// </summary>
public class VideoManager : MonoBehaviour
{
    [Header("Video Info")]
    [SerializeField] private int videoNumber = 1;
    
    [Header("Preload Next Content")]
    [SerializeField] private bool preloadDuringPlayback = true;
    [Tooltip("Preload the next level while video is playing")]
    [SerializeField] private bool preloadNextLevel = true;
    
    [Header("Loading System")]
    [Tooltip("Video sprite player that needs to finish loading")]
    [SerializeField] private SpriteFramePlayer videoPlayer;
    
    [Tooltip("GameObjects to activate once video assets are loaded (they should start deactivated)")]
    [SerializeField] private List<GameObject> objectsToActivateAfterLoading = new List<GameObject>();
    
    [Tooltip("GameObjects to deactivate once video assets are loaded (e.g., loading indicators)")]
    [SerializeField] private List<GameObject> objectsToDeactivateAfterLoading = new List<GameObject>();
    
    [Header("Loading Debug")]
    [SerializeField] private bool isLoading = false;
    [SerializeField] private bool videoLoaded = false;

    private void Awake()
    {
        // Start in loading state if we have a video player to wait for
        if (videoPlayer != null)
        {
            isLoading = true;
            StartCoroutine(WaitForVideoToLoad());
        }
        else
        {
            // No video player, activate everything immediately
            ActivateGameObjects();
        }
    }

    /// <summary>
    /// Wait for video assets to finish loading
    /// </summary>
    private IEnumerator WaitForVideoToLoad()
    {
        Debug.Log("VideoManager: Waiting for video to load...");
        
        // Wait until video is loaded
        while (videoPlayer != null && !videoPlayer.IsLoaded)
        {
            yield return null; // Wait one frame
        }
        
        videoLoaded = true;
        isLoading = false;
        Debug.Log("VideoManager: Video loaded!");
        
        // Deactivate loading indicators
        DeactivateGameObjects();
        
        // Activate game objects now that loading is complete
        ActivateGameObjects();
        
        // Play the video automatically
        if (videoPlayer != null)
        {
            videoPlayer.Play();
            Debug.Log("VideoManager: Started video playback");
            
            // Preload next content while video is playing
            if (preloadDuringPlayback)
            {
                StartCoroutine(PreloadNextContentDuringVideo());
            }
        }
    }

    /// <summary>
    /// Preload the next level/video during video playback
    /// This uses the video playback time to download assets in the background
    /// </summary>
    private IEnumerator PreloadNextContentDuringVideo()
    {
        // Wait a bit to let video start smoothly
        yield return new WaitForSeconds(1f);
        
        // If configured, preload the next level
        if (preloadNextLevel)
        {
            int nextLevel = videoNumber; // Video N leads to Level N
            string nextLevelLabel = $"Level{nextLevel}";
            
            Debug.Log($"VideoManager: Preloading {nextLevelLabel} during video playback...");
            PreloadManager.Instance.PreloadLabel(nextLevelLabel, (success) =>
            {
                if (success)
                {
                    Debug.Log($"VideoManager: Next level ready for seamless transition!");
                }
            });
        }
    }

    /// <summary>
    /// Activate all game objects that were waiting for loading to complete
    /// </summary>
    private void ActivateGameObjects()
    {
        if (objectsToActivateAfterLoading != null)
        {
            foreach (var obj in objectsToActivateAfterLoading)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    Debug.Log($"VideoManager: Activated {obj.name}");
                }
            }
        }
    }

    /// <summary>
    /// Deactivate loading indicators after loading is complete
    /// </summary>
    private void DeactivateGameObjects()
    {
        if (objectsToDeactivateAfterLoading != null)
        {
            foreach (var obj in objectsToDeactivateAfterLoading)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    Debug.Log($"VideoManager: Deactivated {obj.name}");
                }
            }
        }
    }
}
