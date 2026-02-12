using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple pause menu for levels (optional)
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private bool isPaused = false;

    private void Start()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        SetupButtons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    private void SetupButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);
        
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void Pause()
    {
        isPaused = true;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        LevelManager levelManager = FindObjectOfType<LevelManager>(true);
        if (levelManager != null)
            levelManager.RestartLevel();
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadMainMenu();
    }
}
