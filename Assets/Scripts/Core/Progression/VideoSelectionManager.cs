using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Video Selection/Gallery scene where players can rewatch unlocked videos: a card per video,
    /// built at runtime from videoSelectionConfig.layout and ui/theme.json.
    /// </summary>
    public class VideoSelectionManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        private MenuCanvas menu;
        private bool leaving;

        private void Start()
        {
            var gd = GameDefinition.Instance;
            var config = gd != null ? gd.videoSelectionConfig : new SelectionSceneConfig();
            menu = MenuCanvas.Create("VideoSelectUI", gd != null ? gd.menuTheme : null, config.layout);
            var list = menu.AddList(config.layout.list, menu.Theme.card.size);

            int totalVideos = gd != null ? gd.TotalVideos : 0;
            for (int i = 1; i <= totalVideos; i++)
            {
                int videoNumber = i;
                var video = gd.GetVideo(videoNumber);
                bool unlocked = GameProgressManager.Instance.IsVideoUnlocked(videoNumber);
                var data = new MenuCardData
                {
                    Picture = video != null ? video.thumbnail : null,
                    PictureArea = true,
                    Badge = videoNumber.ToString(),
                    Caption = string.Format(config.captionFormat, videoNumber),
                    Title = video != null && !string.IsNullOrEmpty(video.displayName) ? video.displayName : $"Episode {videoNumber}",
                    Status = config.playText,
                    LockedStamp = unlocked ? null : config.lockedText,
                };
                MenuCard.Add(menu, list, data, () => OnVideoClicked(videoNumber));
            }

            menu.AddBackButton(OnBackClicked);
        }

        private void OnVideoClicked(int videoNumber)
        {
            if (leaving) return;
            leaving = true;

            // When watching from gallery, return to main menu after video completes
            PlayerPrefs.SetInt("ReturnToLevel", -1);
            PlayerPrefs.Save();

            if (sceneLoadingManager != null)
                sceneLoadingManager.LoadVideo(videoNumber);
            else
                SceneTransitionManager.Instance.LoadVideo(videoNumber);
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
