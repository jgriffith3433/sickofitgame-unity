using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles scene loading and transitions
/// Supports both traditional scene loading and Addressables for optimized WebGL builds
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    [Header("Loading Mode")]
    [SerializeField] private bool useAddressables = true; // Set to true for optimized WebGL builds
    private static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SceneTransitionManager");
                _instance = go.AddComponent<SceneTransitionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Scene name constants (you can modify these to match your actual scene names)
    public const string MAIN_MENU_SCENE = "MainMenu";
    public const string LEVEL_SELECTION_SCENE = "LevelSelection";
    public const string VIDEO_SELECTION_SCENE = "VideoSelection";

    public void LoadMainMenu()
    {
        // Always load MainMenu normally (not via Addressables) as it's part of the base build
        SceneManager.LoadScene(MAIN_MENU_SCENE);
    }

    public void LoadLevel(int levelNumber)
    {
        string sceneName = $"Level{levelNumber}";
        if (useAddressables)
            AddressablesSceneLoader.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    public void LoadLevelSelection()
    {
        // Always load LevelSelection normally (not via Addressables) as it's part of the base build
        SceneManager.LoadScene(LEVEL_SELECTION_SCENE);
    }

    public void LoadVideoSelection()
    {
        // Always load VideoSelection normally (not via Addressables) as it's part of the base build
        SceneManager.LoadScene(VIDEO_SELECTION_SCENE);
    }

    public void LoadVideo(int videoNumber)
    {
        string sceneName = $"Video{videoNumber}";
        if (useAddressables)
            AddressablesSceneLoader.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    public void ContinueGame()
    {
        int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
        LoadLevel(currentLevel);
    }

    /// <summary>
    /// Preload the next level's assets during gameplay (only works with Addressables)
    /// Call this while player is playing to cache the next level
    /// </summary>
    public void PreloadNextLevel(int currentLevel)
    {
        if (useAddressables)
        {
            int nextLevel = currentLevel + 1;
            string nextSceneName = $"Level{nextLevel}";
            AddressablesSceneLoader.Instance.CacheSceneAssets(nextSceneName);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
