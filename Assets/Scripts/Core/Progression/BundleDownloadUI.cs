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
            GlintVisible = false;
            GlintShownAt = GlintGoneAt = 0f;

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

        // The menu says nothing about loading in numbers: a faint ember glint breathes in the corner while the next level is
        // fetched behind the menu, and is gone when that is done. (The old "Loading N%" text and bar are never shown.)
        private Image glint;
        private float glintAlpha;

        /// <summary>The glint is showing (alpha above a tenth), and when it first showed and went (real seconds since start; 0 = not yet). The playtest reads them.</summary>
        public static bool GlintVisible { get; private set; }
        public static float GlintShownAt { get; private set; }
        public static float GlintGoneAt { get; private set; }

        private void EnsureGlint()
        {
            if (glint != null || downloadUIRoot == null)
                return;
            var go = new GameObject("Glint", typeof(RectTransform));
            go.transform.SetParent(downloadUIRoot.transform, false);
            glint = go.AddComponent<Image>();
            glint.color = new Color(1f, 0.5f, 0.15f, 0f);
            glint.raycastTarget = false;
            var r = glint.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(14f, 14f);
            r.localRotation = Quaternion.Euler(0f, 0f, 45f); // a diamond: the game's own shape
        }

        private void Update()
        {
            if (manager == null)
                return;

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (statusText != null)
                statusText.gameObject.SetActive(false);
            if (progressBarFill != null)
                progressBarFill.gameObject.SetActive(false);

            bool busy = manager.IsDownloading || SickOfIt.Core.Addressables.GameAssets.WarmActive;
            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(busy || glintAlpha > 0.01f);
            EnsureGlint();
            glintAlpha = Mathf.MoveTowards(glintAlpha, busy ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (glint != null)
            {
                float breathe = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                glint.color = new Color(1f, 0.5f, 0.15f, 0.7f * glintAlpha * breathe);
            }
            bool visible = glintAlpha > 0.1f;
            if (visible && !GlintVisible && GlintShownAt <= 0f)
                GlintShownAt = Time.realtimeSinceStartup;
            if (!visible && GlintVisible)
                GlintGoneAt = Time.realtimeSinceStartup;
            GlintVisible = visible;
        }

        private void SetUIVisible(bool visible)
        {
            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(visible);
        }
    }
}
