using UnityEngine;
using System.Collections.Generic;

namespace SickOfIt.Core.Flow
{
    /// <summary>
    /// Manages UI state during scene transitions. Place this in menu scenes (MainMenu, LevelSelection, VideoSelection)
    /// to show loading indicators and hide UI elements while scenes are loading.
    /// </summary>
    public class SceneLoadingManager : MonoBehaviour
    {
        private static SceneLoadingManager _currentInstance;

        [Header("Loading UI")]
        [Tooltip("GameObjects to activate when scene loading starts (e.g., loading screen, spinner)")]
        [SerializeField] private List<GameObject> objectsToActivateOnLoad = new List<GameObject>();

        [Tooltip("GameObjects to deactivate when scene loading starts (e.g., menu buttons, UI panels)")]
        [SerializeField] private List<GameObject> objectsToDeactivateOnLoad = new List<GameObject>();

        private bool isLoading;

        private void OnEnable()
        {
            _currentInstance = this;
        }

        private void OnDisable()
        {
            if (_currentInstance == this)
                _currentInstance = null;
        }

        /// <summary>
        /// Call this before initiating any scene load to show loading UI.
        /// </summary>
        public static void ShowLoadingUI()
        {
            if (_currentInstance != null)
                _currentInstance.ActivateLoadingUI();
        }

        private void ActivateLoadingUI()
        {
            if (isLoading) return;
            isLoading = true;

            if (objectsToActivateOnLoad != null)
            {
                foreach (var obj in objectsToActivateOnLoad)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }

            if (objectsToDeactivateOnLoad != null)
            {
                foreach (var obj in objectsToDeactivateOnLoad)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }

        public void LoadLevel(int levelNumber)
        {
            ShowLoadingUI();
            SceneTransitionManager.Instance.LoadLevel(levelNumber);
        }

        public void LoadVideo(int videoNumber)
        {
            ShowLoadingUI();
            SceneTransitionManager.Instance.LoadVideo(videoNumber);
        }

        public void ContinueGame()
        {
            ShowLoadingUI();
            SceneTransitionManager.Instance.ContinueGame();
        }

        public void NewGame()
        {
            ShowLoadingUI();
            SceneTransitionManager.Instance.LoadLevel(1);
        }
    }
}
