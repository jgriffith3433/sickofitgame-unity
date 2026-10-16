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

            DockToCorner();
            UpdateUI();
        }

        /// <summary>
        /// Keeps the download status out of the menu: a small strip in the bottom-left corner with a short label
        /// (no asset names or MB counts), clear of the buttons.
        /// </summary>
        private void DockToCorner()
        {
            if (downloadUIRoot != null && downloadUIRoot.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(24f, 24f);
                rect.sizeDelta = new Vector2(260f, 44f);
            }

            if (statusText != null)
            {
                var r = statusText.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(260f, 24f);
                statusText.alignment = TextAlignmentOptions.TopLeft;
                statusText.fontSize = 16f;
                statusText.textWrappingMode = TextWrappingModes.NoWrap;
                statusText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (sizeText != null)
                sizeText.gameObject.SetActive(false);

            if (progressBarFill != null)
            {
                var r = progressBarFill.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(0f, -28f);
                r.sizeDelta = new Vector2(200f, 8f);
            }
        }

        private static string CleanStatus(BundleDownloadManager m)
        {
            if (m.IsFullyCached && !m.IsDownloading)
                return "";
            return $"Loading {Mathf.RoundToInt(m.Progress * 100f)}%";
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
                statusText.text = CleanStatus(manager);

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
