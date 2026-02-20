using UnityEngine;

namespace SickOfIt.Core.Progression
{
    public class LevelWinTriggerGoBackToMainMenu : MonoBehaviour
    {
        private LevelManager _levelManager;
        private bool hasTriggered;

        private void Awake()
        {
            // Find LevelManager in the scene
            _levelManager = FindFirstObjectByType<LevelManager>();

            if (_levelManager == null)
            {
                Debug.LogError("LevelWinTriggerGoBackToMainMenu: No LevelManager found in scene!");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasTriggered) return;

            if (other.CompareTag("Player") && _levelManager != null)
            {
                hasTriggered = true;
                _levelManager.CompleteLevelAndReturnToMainMenu();
            }
        }
    }
}
