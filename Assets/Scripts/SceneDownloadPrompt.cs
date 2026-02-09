using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional: Shows download progress and size for scenes before loading
/// Useful for levels that are large downloads
/// </summary>
public class SceneDownloadPrompt : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI sizeText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private string pendingSceneName;
    private System.Action onConfirmed;
    private System.Action onCancelled;

    private void Awake()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    /// <summary>
    /// Show a prompt asking user if they want to download the scene
    /// Good for mobile data or slow connections
    /// </summary>
    public void ShowDownloadPrompt(string sceneName, System.Action onConfirm, System.Action onCancel = null)
    {
        pendingSceneName = sceneName;
        onConfirmed = onConfirm;
        onCancelled = onCancel;

        if (promptPanel != null)
            promptPanel.SetActive(true);

        // Get download size
        AddressablesSceneLoader.Instance.GetSceneDownloadSize(sceneName, OnSizeRetrieved);
    }

    private void OnSizeRetrieved(long sizeBytes)
    {
        if (sizeBytes > 0)
        {
            float sizeMB = sizeBytes / 1024f / 1024f;
            
            if (sizeText != null)
            {
                if (sizeMB < 1f)
                    sizeText.text = $"Download Size: {sizeBytes / 1024f:F0} KB";
                else
                    sizeText.text = $"Download Size: {sizeMB:F1} MB";
            }

            if (statusText != null)
                statusText.text = "This level needs to be downloaded. Continue?";
        }
        else
        {
            // Already cached
            if (sizeText != null)
                sizeText.text = "Cached ✓";

            if (statusText != null)
                statusText.text = "Level is ready to play!";

            // Auto-confirm if already downloaded
            Invoke(nameof(OnConfirmClicked), 0.5f);
        }
    }

    private void OnConfirmClicked()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        onConfirmed?.Invoke();
    }

    private void OnCancelClicked()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        onCancelled?.Invoke();
    }

    /// <summary>
    /// Download the scene with progress feedback (without loading it)
    /// </summary>
    public void PredownloadWithProgress(string sceneName, System.Action onComplete)
    {
        StartCoroutine(PredownloadCoroutine(sceneName, onComplete));
    }

    private System.Collections.IEnumerator PredownloadCoroutine(string sceneName, System.Action onComplete)
    {
        if (promptPanel != null)
            promptPanel.SetActive(true);

        if (statusText != null)
            statusText.text = "Downloading...";

        // Start caching
        AddressablesSceneLoader.Instance.CacheSceneAssets(sceneName);

        // Simulate progress (actual progress from Addressables is more complex)
        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * 0.2f; // Adjust speed as needed
            
            if (progressBar != null)
                progressBar.value = progress;

            if (statusText != null)
                statusText.text = $"Downloading... {progress * 100f:F0}%";

            yield return null;
        }

        if (statusText != null)
            statusText.text = "Download Complete!";

        yield return new WaitForSeconds(0.5f);

        if (promptPanel != null)
            promptPanel.SetActive(false);

        onComplete?.Invoke();
    }
}
