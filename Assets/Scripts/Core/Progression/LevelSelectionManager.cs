using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Level Selection scene: a card per level (thumbnail, name, locked/cleared state and the health
    /// the hero finished it with), built at runtime from levelSelectionConfig.layout and ui/theme.json.
    /// </summary>
    public class LevelSelectionManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        private MenuCanvas menu;
        private bool leaving;

        private void Start()
        {
            var gd = GameDefinition.Instance;
            if (gd == null)
            {
                Debug.LogError("LevelSelectionManager: GameDefinition not found in Resources!");
                return;
            }

            var config = gd.levelSelectionConfig ?? new SelectionSceneConfig();
            menu = MenuCanvas.Create("LevelSelectUI", gd.menuTheme, config.layout, gameObject);
            var list = menu.AddList(config.layout.list, menu.Theme.card.size);

            int highestReached = GameProgressManager.Instance.GetHighestLevelReached();
            for (int i = 1; i <= gd.TotalLevels; i++)
            {
                int levelNumber = i;
                var level = gd.GetLevel(levelNumber);
                bool completed = GameProgressManager.Instance.IsLevelCompleted(levelNumber);
                bool unlocked = levelNumber <= highestReached;
                int endHealth = RogueliteManager.Instance.GetLevelEndHealth(levelNumber);
                int maxHealth = RogueliteManager.Instance.GetLevelMaxHealth(levelNumber);

                var data = new MenuCardData
                {
                    Picture = level != null ? level.thumbnail : null,
                    PictureArea = true,
                    Badge = levelNumber.ToString(),
                    Caption = string.Format(config.captionFormat, levelNumber),
                    Title = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : $"Level {levelNumber}",
                    Status = completed
                        ? config.clearedText + (endHealth >= 0 ? "  " + Hearts(endHealth, maxHealth) : "")
                        : config.playText,
                    StatusColor = completed ? menu.Theme.positive : menu.Theme.accent,
                    LockedStamp = unlocked ? null : config.lockedText,
                };
                MenuCard.Add(menu, list, data, () => OnLevelClicked(levelNumber));
            }

            if (config.showCumulativeHealth)
            {
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int health = RogueliteManager.Instance.GetCumulativeHealth(gd.TotalLevels);
                menu.AddChip(string.Format(menu.Theme.healthFormat, health, maxHealth));
            }
            menu.AddBackButton(OnBackClicked);
        }

        /// <summary>Health as hearts, lost ones faded (falls back to "♥ 3/5" for big totals).</summary>
        public static string Hearts(int health, int max)
        {
            if (max <= 0 || max > 8)
                return $"♥ {health}/{max}";
            health = Mathf.Clamp(health, 0, max);
            return new string('♥', health) + "<alpha=#40>" + new string('♥', max - health) + "<alpha=#FF>";
        }

        private void OnLevelClicked(int levelNumber)
        {
            if (leaving) return;
            leaving = true;

            // For Level 1, check if it's the first time visiting
            if (levelNumber == 1 && !HasVisitedLevel(1))
            {
                // Mark as visited and play Video1 first
                MarkLevelAsVisited(1);
                PlayerPrefs.SetInt("ReturnToLevel", 1);
                PlayerPrefs.Save();

                if (BundleDownloadManager.Instance != null)
                    BundleDownloadManager.Instance.StartDownloadForLevel(1);

                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadVideo(1);
                else
                    SceneTransitionManager.Instance.LoadVideo(1);
            }
            else
            {
                if (BundleDownloadManager.Instance != null)
                    BundleDownloadManager.Instance.StartDownloadForLevel(levelNumber);

                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(levelNumber);
                else
                    SceneTransitionManager.Instance.LoadLevel(levelNumber);
            }
        }

        private bool HasVisitedLevel(int levelNumber)
        {
            return PlayerPrefs.GetInt($"Level_{levelNumber}_Visited", 0) == 1;
        }

        private void MarkLevelAsVisited(int levelNumber)
        {
            PlayerPrefs.SetInt($"Level_{levelNumber}_Visited", 1);
            PlayerPrefs.Save();
        }

        private void OnBackClicked()
        {
            if (leaving) return;
            leaving = true;
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }
}
