using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// ScriptableObject that defines game-wide configuration settings
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfiguration", menuName = "SickOfIt/Game Configuration", order = 0)]
    public class GameConfiguration : ScriptableObject
    {
        [Header("Level Configuration")]
        [SerializeField]
        [Tooltip("Total number of levels in the game")]
        private int totalLevels = 2;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Enable verbose logging for game progression")]
        private bool debugMode = false;

        public int TotalLevels => totalLevels;
        public bool DebugMode => debugMode;

        private void OnValidate()
        {
            // Ensure total levels is at least 1
            if (totalLevels < 1)
                totalLevels = 1;
        }
    }
}
