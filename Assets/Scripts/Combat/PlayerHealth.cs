using UnityEngine;

/// <summary>
/// Player-specific health management with game over handling
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Death Scene Settings")]
    [Tooltip("Name of the death scene to load (e.g., 'Death1', 'DeathAnimation', etc.)")]
    [SerializeField] private string deathSceneName = "Death1";
    
    private HealthSystem healthSystem;
    private LevelManager levelManager;
    
    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        levelManager = FindObjectOfType<LevelManager>(true);
    }
    
    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.onDeath.AddListener(OnPlayerDeath);
        }
    }
    
    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.onDeath.RemoveListener(OnPlayerDeath);
        }
    }
    
    private void OnPlayerDeath()
    {
        // Disable player controls
        var controller = GetComponent<MobileControllerFramework.TopDownController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        // Play death scene which will reload the level after animation
        PlayDeathScene();
    }
    
    private void PlayDeathScene()
    {
        if (levelManager != null)
        {
            // Store which scene to reload after death animation
            UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            PlayerPrefs.SetString("DeathSceneToReload", currentScene.name);
            PlayerPrefs.Save();
            
            // Show loading UI before transitioning
            levelManager.ShowSceneTransitionUI();
            
            // Load the death scene
            SceneTransitionManager.Instance.LoadDeathScene(deathSceneName);
        }
    }
}
