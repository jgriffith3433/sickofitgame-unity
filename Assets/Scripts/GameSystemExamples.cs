using UnityEngine;

/// <summary>
/// Example script showing how to integrate the game systems with your gameplay code
/// This is just a reference - adapt these patterns to your actual game code
/// </summary>
public class GameSystemExamples : MonoBehaviour
{
    // ===== EXAMPLE 1: Completing a Level =====
    // Call this when the player achieves the win condition in your level
    public void OnPlayerWins()
    {
        // This is the main method you'll use in your levels
        LevelManager levelManager = FindObjectOfType<LevelManager>(true);
        if (levelManager != null)
        {
            levelManager.CompleteLevel();
        }
    }

    // ===== EXAMPLE 2: Checking if a Level is Completed =====
    // Useful for displaying progress or customizing gameplay
    public void CheckLevelStatus()
    {
        bool level1Complete = GameProgressManager.Instance.IsLevelCompleted(1);
        if (level1Complete)
        {
            Debug.Log("Player has already beaten Level 1!");
            // Maybe unlock a special skin or bonus content
        }
    }

    // ===== EXAMPLE 3: Manual Scene Loading =====
    // If you need to load scenes from your own code
    public void LoadSpecificLevel()
    {
        SceneTransitionManager.Instance.LoadLevel(3); // Load Level 3
    }

    public void GoToMainMenu()
    {
        SceneTransitionManager.Instance.LoadMainMenu();
    }

    // ===== EXAMPLE 4: Checking Player Progress =====
    public void DisplayPlayerProgress()
    {
        int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
        int highestReached = GameProgressManager.Instance.GetHighestLevelReached();
        
        Debug.Log($"Current Level: {currentLevel}");
        Debug.Log($"Highest Level Reached: {highestReached}");
        
        // Check individual levels
        for (int i = 1; i <= 10; i++)
        {
            if (GameProgressManager.Instance.IsLevelCompleted(i))
            {
                Debug.Log($"Level {i}: ✓ Completed");
            }
        }
    }

    // ===== EXAMPLE 5: Manually Unlocking Content =====
    // Useful for bonus unlocks or cheat codes
    public void UnlockBonusContent()
    {
        // Unlock a specific video
        GameProgressManager.Instance.UnlockVideo(99);
        
        // Mark a level as complete
        GameProgressManager.Instance.SetLevelCompleted(5);
    }

    // ===== EXAMPLE 6: Reset Progress (Debug/Testing) =====
    // Great for testing the new player experience
    public void ResetGame()
    {
        GameProgressManager.Instance.ResetAllProgress();
        Debug.Log("All progress reset!");
    }

    // ===== EXAMPLE 7: Integration with Your Existing Gameplay =====
    // Example of how to integrate with your existing level code
    private int objectivesCompleted = 0;
    private int totalObjectives = 5;

    public void OnObjectiveCompleted()
    {
        objectivesCompleted++;
        
        if (objectivesCompleted >= totalObjectives)
        {
            // All objectives done, player wins!
            OnPlayerWins();
        }
    }

    // ===== EXAMPLE 8: Custom Win Screen =====
    // If you want a custom win screen before returning to level select
    public void ShowWinScreenThenComplete()
    {
        // Show your win UI, play animations, etc.
        // Then after a delay or button press:
        FindObjectOfType<LevelManager>().CompleteLevel();
    }

    // ===== EXAMPLE 9: Conditional Video Playback =====
    // Play different videos based on player performance or choices
    public void PlayConditionalVideo(int videoNumber)
    {
        if (GameProgressManager.Instance.IsVideoUnlocked(videoNumber))
        {
            SceneTransitionManager.Instance.LoadVideo(videoNumber);
        }
        else
        {
            Debug.Log("Video not yet unlocked!");
        }
    }

    // ===== EXAMPLE 10: Integration with UI =====
    // Update UI based on progression
    public void UpdateProgressUI(UnityEngine.UI.Text progressText)
    {
        int current = GameProgressManager.Instance.GetCurrentLevel();
        progressText.text = $"Current Level: {current}";
    }
}
