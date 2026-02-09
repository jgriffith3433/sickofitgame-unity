using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MobileControllerFramework;

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

    [Header("Loading System")]
    [Tooltip("Background sprite players that need to finish loading")]
    [SerializeField] private List<SpriteFramePlayer> backgroundPlayers = new List<SpriteFramePlayer>();
    
    [Tooltip("Music cycler that needs to finish loading")]
    [SerializeField] private MusicCycler musicCycler;
    
    [Tooltip("GameObjects to activate once all assets are loaded (they should start deactivated)")]
    [SerializeField] private List<GameObject> objectsToActivateAfterLoading = new List<GameObject>();
    
    [Tooltip("GameObjects to deactivate once all assets are loaded (e.g., loading indicators)")]
    [SerializeField] private List<GameObject> objectsToDeactivateAfterLoading = new List<GameObject>();
    
    [Header("Scene Transition Loading")]
    [Tooltip("GameObjects to activate when transitioning to next scene (e.g., 'Loading...' UI)")]
    [SerializeField] private List<GameObject> objectsToActivateOnSceneTransition = new List<GameObject>();
    
    [Tooltip("GameObjects to deactivate when transitioning to next scene (e.g., player controls, game UI)")]
    [SerializeField] private List<GameObject> objectsToDeactivateOnSceneTransition = new List<GameObject>();
    
    [Header("Loading Debug")]
    [SerializeField] private bool isLoading = false;
    [SerializeField] private bool allAssetsLoaded = false;

    private bool hasPlayedVideo = false;

    private void Awake()
    {
        // Start in loading state if we have assets to wait for
        if (HasAssetsToLoad())
        {
            isLoading = true;
            StartCoroutine(WaitForAssetsToLoad());
        }
        else
        {
            // No assets to load, activate everything immediately
            ActivateGameObjects();
        }
    }

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

    /// <summary>
    /// Clla this method when the player completes the level but wants to return to the main menu instead of proceeding
    /// </summary>
    public void CompleteLevelAndReturnToMainMenu()
    {
        Debug.Log($"Level {levelNumber} completed! Returning to main menu...");
        
        // Mark level as complete
        GameProgressManager.Instance.SetLevelCompleted(levelNumber);
        
        // Unlock the NEXT level's video
        int nextLevelVideo = levelNumber + 1;
        GameProgressManager.Instance.UnlockVideo(nextLevelVideo);
        
        // You can add completion effects here (particles, sounds, etc.)
        
        // After a short delay, return to main menu
        Invoke(nameof(ReturnToMainMenu), 2f);
    }

    private void ProceedToNextLevel()
    {
        int nextLevel = levelNumber + 1;
        int nextLevelVideo = levelNumber + 1;
        
        // Show loading UI before transitioning
        ShowSceneTransitionUI();
        
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
        ShowSceneTransitionUI();
        SceneTransitionManager.Instance.LoadLevelSelection();
    }

    public void ReturnToMainMenu()
    {
        ShowSceneTransitionUI();
        SceneTransitionManager.Instance.LoadMainMenu();
    }

    public void RestartLevel()
    {
        ShowSceneTransitionUI();
        SceneTransitionManager.Instance.LoadLevel(levelNumber);
    }

    private void PlayLevelVideo()
    {
        // Store which level to return to after video
        PlayerPrefs.SetInt("ReturnToLevel", levelNumber);
        PlayerPrefs.Save();
        
        ShowSceneTransitionUI();
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

    /// <summary>
    /// Check if there are any assets that need to be loaded
    /// </summary>
    private bool HasAssetsToLoad()
    {
        return (backgroundPlayers != null && backgroundPlayers.Count > 0) || musicCycler != null;
    }

    /// <summary>
    /// Wait for all addressable assets to finish loading
    /// </summary>
    private IEnumerator WaitForAssetsToLoad()
    {
        Debug.Log("LevelManager: Waiting for assets to load...");
        
        // Wait until all assets are loaded
        while (!AreAllAssetsLoaded())
        {
            yield return null; // Wait one frame
        }
        
        allAssetsLoaded = true;
        isLoading = false;
        Debug.Log("LevelManager: All assets loaded!");
        
        // Deactivate loading indicators
        DeactivateGameObjects();
        
        // Activate game objects now that loading is complete
        ActivateGameObjects();
    }

    /// <summary>
    /// Check if all registered assets have finished loading
    /// </summary>
    private bool AreAllAssetsLoaded()
    {
        // Check all background players
        if (backgroundPlayers != null)
        {
            foreach (var player in backgroundPlayers)
            {
                if (player != null && !player.IsLoaded)
                {
                    return false; // Still waiting for this one
                }
            }
        }
        
        // Check music cycler
        if (musicCycler != null && !musicCycler.IsLoaded)
        {
            return false;
        }
        
        return true; // Everything is loaded
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
                    Debug.Log($"Activated: {obj.name}");
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
                    Debug.Log($"Deactivated: {obj.name}");
                }
            }
        }
    }

    /// <summary>
    /// Show loading UI when transitioning to another scene
    /// </summary>
    private void ShowSceneTransitionUI()
    {
        Debug.Log("LevelManager: Showing scene transition loading UI");
        
        // Activate loading indicators
        if (objectsToActivateOnSceneTransition != null)
        {
            foreach (var obj in objectsToActivateOnSceneTransition)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    Debug.Log($"Activated for transition: {obj.name}");
                }
            }
        }
        
        // Deactivate game UI/controls
        if (objectsToDeactivateOnSceneTransition != null)
        {
            foreach (var obj in objectsToDeactivateOnSceneTransition)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    Debug.Log($"Deactivated for transition: {obj.name}");
                }
            }
        }
    }
}
