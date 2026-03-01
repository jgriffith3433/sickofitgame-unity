using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Trigger zone that completes the level when the player enters it.
    /// Uses LevelRuntime (config-driven).
    /// </summary>
    public class LevelWinTrigger : MonoBehaviour
    {
        private LevelRuntime _levelRuntime;
        private bool hasTriggered;

        private void Awake()
        {
            _levelRuntime = FindFirstObjectByType<LevelRuntime>();
            if (_levelRuntime == null)
                Debug.LogError("[LevelWinTrigger] No LevelRuntime found in scene!");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasTriggered) return;
            if (other.CompareTag("Player") && _levelRuntime != null)
            {
                hasTriggered = true;
                _levelRuntime.CompleteLevel();
            }
        }
    }
}
