using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// UI bridge for BundleDownloadManager. Place this on a scene (e.g., Main Menu)
    /// alongside the UI elements. It reads state from the persistent BundleDownloadManager
    /// singleton and updates the local UI every frame.
    /// </summary>
    public class BundleDownloadUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Fill image for the overall download progress bar (Image.Type.Filled or horizontal)")]
        [SerializeField] private Image progressBarFill;

        [Tooltip("Text showing download status (e.g., 'Verifying cache (5/97)...')")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("Text showing remaining download size (e.g., '150 MB remaining')")]
        [SerializeField] private TextMeshProUGUI sizeText;

        [Tooltip("Root GameObject for the download UI. Hidden when everything is cached.")]
        [SerializeField] private GameObject downloadUIRoot;

        [Header("Settings")]
        [Tooltip("Hide the download UI when all bundles are already cached")]
        [SerializeField] private bool hideWhenFullyCached = true;

        private BundleDownloadManager manager;

        private void Start()
        {
            manager = BundleDownloadManager.Instance;

            if (manager == null)
            {
                Debug.LogWarning("[BundleDownloadUI] No BundleDownloadManager instance found. Hiding UI.");
                SetUIVisible(false);
                return;
            }

            // Initialize UI to current state immediately
            UpdateUI();
        }

        private void Update()
        {
            if (manager == null)
                return;

            UpdateUI();
        }

        private void UpdateUI()
        {
            // Progress bar
            if (progressBarFill != null)
                progressBarFill.fillAmount = manager.Progress;

            // Status text
            if (statusText != null)
                statusText.text = manager.StatusText;

            // Size text
            if (sizeText != null)
                sizeText.text = manager.SizeText;

            // Show/hide root
            if (downloadUIRoot != null)
            {
                bool shouldHide = hideWhenFullyCached && manager.IsFullyCached && !manager.IsDownloading;
                downloadUIRoot.SetActive(!shouldHide);
            }
        }

        private void SetUIVisible(bool visible)
        {
            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(visible);
        }
    }
}
