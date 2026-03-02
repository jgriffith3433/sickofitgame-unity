using UnityEngine;
using MobileControllerFramework;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Video Selection/Gallery scene where players can rewatch unlocked videos
    /// </summary>
    public class VideoSelectionManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Read from GameDefinition.TotalVideos at runtime")]
        [SerializeField] private int totalVideos = 2;

        [Header("UI References")]
        [SerializeField] private Transform videoButtonContainer; // Parent object for video buttons
        [SerializeField] private GameObject videoButtonPrefab; // Prefab for video buttons
        [SerializeField] private MobileControllerFramework.Button backButton;

        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        private void Start()
        {
            // Read total videos from config instead of hardcoded serialized field
            var gd = GameDefinition.Instance;
            if (gd != null) totalVideos = gd.TotalVideos;

            GenerateVideoButtons();

            if (backButton != null)
                backButton.TapEndEvent.AddListener(OnBackClicked);
        }

        private void GenerateVideoButtons()
        {
            if (videoButtonContainer == null || videoButtonPrefab == null)
            {
                Debug.LogError("VideoSelectionManager: Missing references! Please assign videoButtonContainer and videoButtonPrefab in the inspector.");
                return;
            }

            // Clear existing buttons
            foreach (Transform child in videoButtonContainer)
            {
                Destroy(child.gameObject);
            }

            // Create buttons for each video (starting from Video 1)
            for (int i = 1; i <= totalVideos; i++)
            {
                int videoNumber = i; // Capture for lambda

                GameObject buttonObj = Instantiate(videoButtonPrefab, videoButtonContainer);
                VideoButton videoButton = buttonObj.GetComponent<VideoButton>();

                if (videoButton != null)
                {
                    bool isUnlocked = GameProgressManager.Instance.IsVideoUnlocked(videoNumber);

                    // Read display name from VideoConfig if available
                    string videoName = $"Episode {videoNumber}";
                    var gd2 = GameDefinition.Instance;
                    if (gd2 != null)
                    {
                        VideoConfig vc = gd2.GetVideo(videoNumber);
                        if (vc != null && !string.IsNullOrEmpty(vc.displayName))
                            videoName = vc.displayName;
                    }

                    videoButton.Setup(videoNumber, videoName, isUnlocked, () => OnVideoClicked(videoNumber));
                }
            }
        }

        private void OnVideoClicked(int videoNumber)
        {
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
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }
}
