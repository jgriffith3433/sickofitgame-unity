using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls visibility of scene content for additive scene loading.
/// Add this to each Level/Video scene. Put all scene content under a root GameObject
/// and add that root to the contentRoots list.
/// </summary>
public class SceneContentController : MonoBehaviour
{
    [Header("Scene Content")]
    [Tooltip("Root GameObjects containing all scene content - will be hidden when scene is preloaded")]
    [SerializeField] private List<GameObject> contentRoots = new List<GameObject>();
    
    [Header("Loading UI")]
    [Tooltip("GameObjects to show while waiting for scene to activate (optional)")]
    [SerializeField] private List<GameObject> loadingIndicators = new List<GameObject>();
    
    [Header("Debug")]
    [SerializeField] private bool isActive = false;

    private void Awake()
    {
        // Start with content hidden (scene might be preloaded before being shown)
        HideContent();
        ShowLoadingIndicators();
    }

    /// <summary>
    /// Call this when the scene should become visible and active
    /// </summary>
    public void ActivateScene()
    {
        if (isActive) return;
        
        isActive = true;
        ShowContent();
        HideLoadingIndicators();
        
        Debug.Log($"[SceneContent] Activated scene: {gameObject.scene.name}");
    }

    /// <summary>
    /// Call this when transitioning away from this scene
    /// </summary>
    public void DeactivateScene()
    {
        if (!isActive) return;
        
        isActive = false;
        HideContent();
        ShowLoadingIndicators();
        
        Debug.Log($"[SceneContent] Deactivated scene: {gameObject.scene.name}");
    }

    private void ShowContent()
    {
        foreach (var root in contentRoots)
        {
            if (root != null)
                root.SetActive(true);
        }
    }

    private void HideContent()
    {
        foreach (var root in contentRoots)
        {
            if (root != null)
                root.SetActive(false);
        }
    }

    private void ShowLoadingIndicators()
    {
        foreach (var indicator in loadingIndicators)
        {
            if (indicator != null)
                indicator.SetActive(true);
        }
    }

    private void HideLoadingIndicators()
    {
        foreach (var indicator in loadingIndicators)
        {
            if (indicator != null)
                indicator.SetActive(false);
        }
    }
}
