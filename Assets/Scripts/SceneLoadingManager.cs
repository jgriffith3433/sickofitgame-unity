using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages UI state during scene transitions. Place this in menu scenes (MainMenu, LevelSelection, VideoSelection)
/// to show loading indicators and hide UI elements while scenes are loading.
/// </summary>
public class SceneLoadingManager : MonoBehaviour
{
    private static SceneLoadingManager _currentInstance;

    [Header("Loading UI")]
    [Tooltip("GameObjects to activate when scene loading starts (e.g., loading screen, spinner)")]
    [SerializeField] private List<GameObject> objectsToActivateOnLoad = new List<GameObject>();
    
    [Tooltip("GameObjects to deactivate when scene loading starts (e.g., menu buttons, UI panels)")]
    [SerializeField] private List<GameObject> objectsToDeactivateOnLoad = new List<GameObject>();

    [Header("Debug")]
    [SerializeField] private bool isLoading = false;

    private void OnEnable()
    {
        // Register this as the current scene's loading manager
        _currentInstance = this;
    }

    private void OnDisable()
    {
        // Unregister when scene is unloading
        if (_currentInstance == this)
        {
            _currentInstance = null;
        }
    }

    /// <summary>
    /// Call this before initiating any scene load to show loading UI
    /// </summary>
    public static void ShowLoadingUI()
    {
        if (_currentInstance != null)
        {
            _currentInstance.ActivateLoadingUI();
        }
    }

    /// <summary>
    /// Internal method to activate loading UI elements
    /// </summary>
    private void ActivateLoadingUI()
    {
        if (isLoading) return; // Already loading
        
        isLoading = true;
        Debug.Log("SceneLoadingManager: Showing loading UI");

        // Activate loading indicators
        if (objectsToActivateOnLoad != null)
        {
            foreach (var obj in objectsToActivateOnLoad)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }

        // Deactivate menu UI
        if (objectsToDeactivateOnLoad != null)
        {
            foreach (var obj in objectsToDeactivateOnLoad)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Helper method to show loading UI and then load a level
    /// </summary>
    public void LoadLevel(int levelNumber)
    {
        ShowLoadingUI();
        SceneTransitionManager.Instance.LoadLevel(levelNumber);
    }

    /// <summary>
    /// Helper method to show loading UI and then load a video
    /// </summary>
    public void LoadVideo(int videoNumber)
    {
        ShowLoadingUI();
        SceneTransitionManager.Instance.LoadVideo(videoNumber);
    }

    /// <summary>
    /// Helper method to show loading UI and then continue game
    /// </summary>
    public void ContinueGame()
    {
        ShowLoadingUI();
        SceneTransitionManager.Instance.ContinueGame();
    }

    /// <summary>
    /// Helper method to show loading UI and then start new game
    /// </summary>
    public void NewGame()
    {
        ShowLoadingUI();
        // Assuming new game starts at level 1 or video 1
        SceneTransitionManager.Instance.LoadLevel(1);
    }
}
