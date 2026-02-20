using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using SickOfIt.Core.Addressables;
using SickOfIt.VideoFrameExtractor.Runtime;
using UnityEngine.Events;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Pre-downloads Addressable bundles to browser cache without loading assets into memory.
    /// Place on the Main Menu to show download progress and cache game assets in the background.
    /// Uses Addressables.GetDownloadSizeAsync to detect what's already cached and
    /// Addressables.DownloadDependenciesAsync to cache the rest.
    /// </summary>
    public class BundleDownloadManager : MonoBehaviour
    {
        [Header("Content to Download")]
        [Tooltip("All SpriteFramesData assets whose chunk bundles should be pre-cached. " +
                 "Each chunk label will be checked and downloaded individually.")]
        [SerializeField] private List<SpriteFramesData> spriteFramesDataAssets = new List<SpriteFramesData>();

        [Tooltip("Additional Addressable labels to pre-cache (scenes, audio, prefabs, etc.)")]
        [SerializeField] private List<string> additionalLabels = new List<string>();

        [Header("UI References")]
        [Tooltip("Fill image for the overall download progress bar (Image.Type.Filled or horizontal)")]
        [SerializeField] private Image progressBarFill;

        [Tooltip("Text showing download status (e.g., '45% - Downloading Video3_Part5...')")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("Text showing total download size (e.g., '150 MB remaining')")]
        [SerializeField] private TextMeshProUGUI sizeText;

        [Tooltip("Root GameObject for the download UI. Hidden when everything is cached.")]
        [SerializeField] private GameObject downloadUIRoot;

        [Header("Settings")]
        [Tooltip("Start downloading automatically on Awake")]
        [SerializeField] private bool autoStartOnAwake = true;

        [Tooltip("Hide the download UI when all bundles are already cached")]
        [SerializeField] private bool hideWhenFullyCached = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // State
        private List<string> allLabels = new List<string>();
        private bool isDownloading = false;
        private int totalLabels = 0;
        private int completedLabels = 0;
        private long totalDownloadBytes = 0;
        private long completedDownloadBytes = 0;
        private string currentLabel = "";
        private Coroutine uiUpdateCoroutine = null;

        /// <summary>Event fired when all downloads complete (success or failure).</summary>
        public UnityEvent OnDownloadComplete = new UnityEvent();

        /// <summary>True when all bundles are cached and no download is needed.</summary>
        public bool IsFullyCached { get; private set; }

        /// <summary>True while a download is in progress.</summary>
        public bool IsDownloading => isDownloading;

        /// <summary>Overall progress from 0 to 1.</summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            BuildLabelList();
            InitializeUI();

            if (autoStartOnAwake)
                StartCoroutine(CheckAndDownload());
        }

        /// <summary>
        /// Initialize UI to default state
        /// </summary>
        private void InitializeUI()
        {
            Progress = 0f;
            
            if (progressBarFill != null)
                progressBarFill.fillAmount = 0f;

            if (statusText != null)
                statusText.text = "Checking downloads...";

            if (sizeText != null)
                sizeText.text = "";

            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(true);
        }

        /// <summary>
        /// Collect all unique labels from the SpriteFramesData chunks and additional labels.
        /// </summary>
        private void BuildLabelList()
        {
            allLabels.Clear();
            var seen = new HashSet<string>();

            foreach (var data in spriteFramesDataAssets)
            {
                if (data == null) continue;

                foreach (var chunk in data.chunks)
                {
                    if (!string.IsNullOrEmpty(chunk.addressableLabel) && seen.Add(chunk.addressableLabel))
                        allLabels.Add(chunk.addressableLabel);
                }

                // Also include audio if present
                if (!string.IsNullOrEmpty(data.audioAddress) && seen.Add(data.audioAddress))
                    allLabels.Add(data.audioAddress);
            }

            foreach (var label in additionalLabels)
            {
                if (!string.IsNullOrEmpty(label) && seen.Add(label))
                    allLabels.Add(label);
            }

            totalLabels = allLabels.Count;
            Log($"Built label list: {totalLabels} labels to check");
        }

        /// <summary>
        /// Public entry point to manually start the check + download process.
        /// </summary>
        public void StartDownload()
        {
            if (!isDownloading)
                StartCoroutine(CheckAndDownload());
        }

        /// <summary>
        /// Check which bundles need downloading, then download them sequentially.
        /// Sequential downloading prevents memory spikes from multiple concurrent decompression operations.
        /// </summary>
        private IEnumerator CheckAndDownload()
        {
            isDownloading = true;
            completedLabels = 0;
            completedDownloadBytes = 0;
            totalDownloadBytes = 0;
            Progress = 0f;
            
            // Ensure UI is visible and reset
            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(true);
            
            UpdateUI();

            // Phase 1: Check download sizes for all labels to determine what's cached
            Log("Phase 1: Checking download sizes...");
            var labelsToDownload = new List<string>();
            var labelSizes = new Dictionary<string, long>();

            foreach (var label in allLabels)
            {
                bool sizeChecked = false;
                long size = 0;

                AddressablesOperationManager.Instance.GetLabelDownloadSize(label, downloadSize =>
                {
                    size = downloadSize;
                    sizeChecked = true;
                });

                yield return new WaitUntil(() => sizeChecked);

                if (size > 0)
                {
                    labelsToDownload.Add(label);
                    labelSizes[label] = size;
                    totalDownloadBytes += size;
                    Log($"  '{label}': {FormatBytes(size)} needs download");
                }
            }

            // Check if everything is already cached
            if (labelsToDownload.Count == 0)
            {
                Log("All bundles already cached!");
                IsFullyCached = true;
                Progress = 1f;
                isDownloading = false;

                if (hideWhenFullyCached && downloadUIRoot != null)
                    downloadUIRoot.SetActive(false);

                UpdateUI();
                OnDownloadComplete?.Invoke();
                yield break;
            }

            Log($"Phase 2: Downloading {labelsToDownload.Count} bundles ({FormatBytes(totalDownloadBytes)} total)");
            IsFullyCached = false;

            if (downloadUIRoot != null)
                downloadUIRoot.SetActive(true);

            UpdateUI();
            
            // Start continuous UI updates
            if (uiUpdateCoroutine != null)
                StopCoroutine(uiUpdateCoroutine);
            uiUpdateCoroutine = StartCoroutine(ContinuousUIUpdate());

            // Phase 2: Download each bundle sequentially
            foreach (var label in labelsToDownload)
            {
                currentLabel = label;
                long labelSize = labelSizes[label];
                bool downloadComplete = false;
                bool downloadSuccess = false;

                AddressablesOperationManager.Instance.DownloadDependencies(
                    label,
                    success =>
                    {
                        downloadSuccess = success;
                        downloadComplete = true;
                    },
                    progress =>
                    {
                        // Calculate overall progress: completed bytes + current label partial progress
                        long currentLabelProgress = (long)(progress * labelSize);
                        float overall = (completedDownloadBytes + currentLabelProgress) / (float)totalDownloadBytes;
                        Progress = Mathf.Clamp01(overall);
                        UpdateUI();
                    }
                );

                yield return new WaitUntil(() => downloadComplete);

                if (downloadSuccess)
                {
                    completedLabels++;
                    completedDownloadBytes += labelSize;
                    Progress = completedDownloadBytes / (float)totalDownloadBytes;
                    Log($"  Downloaded '{label}' ({completedLabels}/{labelsToDownload.Count})");
                }
                else
                {
                    Debug.LogError($"[BundleDownloadManager] Failed to download '{label}'. Continuing with remaining bundles.");
                }

                UpdateUI();
            }

            // Done
            IsFullyCached = completedLabels == labelsToDownload.Count;
            Progress = 1f;
            isDownloading = false;
            
            // Stop continuous UI updates
            if (uiUpdateCoroutine != null)
            {
                StopCoroutine(uiUpdateCoroutine);
                uiUpdateCoroutine = null;
            }

            if (IsFullyCached)
            {
                Log($"All downloads complete! ({FormatBytes(totalDownloadBytes)} total)");

                if (hideWhenFullyCached && downloadUIRoot != null)
                    downloadUIRoot.SetActive(false);
            }
            else
            {
                Log($"Downloads finished with errors. {completedLabels}/{labelsToDownload.Count} succeeded.");
            }

            UpdateUI();
            OnDownloadComplete?.Invoke();
        }

        /// <summary>
        /// Continuously update UI while downloading to ensure smooth progress display
        /// </summary>
        private IEnumerator ContinuousUIUpdate()
        {
            while (isDownloading)
            {
                UpdateUI();
                yield return null; // Update every frame
            }
        }

        private void UpdateUI()
        {
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = Progress;
                Canvas.ForceUpdateCanvases();
            }

            if (statusText != null)
            {
                if (IsFullyCached)
                    statusText.text = "All content downloaded!";
                else if (isDownloading)
                {
                    if (string.IsNullOrEmpty(currentLabel))
                        statusText.text = "Checking bundles...";
                    else
                        statusText.text = $"{(Progress * 100f):F0}% - Downloading {currentLabel}...";
                }
                else
                    statusText.text = "";
            }

            if (sizeText != null)
            {
                if (isDownloading && totalDownloadBytes > 0)
                {
                    long remaining = totalDownloadBytes - completedDownloadBytes;
                    sizeText.text = $"{FormatBytes(remaining)} remaining";
                }
                else if (IsFullyCached)
                {
                    sizeText.text = "";
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:F1} KB";
            return $"{bytes / 1024f / 1024f:F1} MB";
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[BundleDownloadManager] {message}");
        }

#if UNITY_EDITOR
        [ContextMenu("Find All SpriteFramesData Assets")]
        private void FindAllSpriteFramesDataAssets()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:SpriteFramesData");
            spriteFramesDataAssets.Clear();

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SpriteFramesData>(path);
                if (asset != null)
                    spriteFramesDataAssets.Add(asset);
            }

            UnityEditor.EditorUtility.SetDirty(this);

            // Build and log all labels for verification
            BuildLabelList();
            Debug.Log($"[BundleDownloadManager] Found {spriteFramesDataAssets.Count} SpriteFramesData assets → {allLabels.Count} labels total");
            for (int i = 0; i < allLabels.Count; i++)
                Debug.Log($"  [{i + 1}] {allLabels[i]}");
        }

        [ContextMenu("List All Labels (Dry Run)")]
        private void ListAllLabels()
        {
            BuildLabelList();
            Debug.Log($"[BundleDownloadManager] === {allLabels.Count} labels would be checked ===");
            for (int i = 0; i < allLabels.Count; i++)
                Debug.Log($"  [{i + 1}] {allLabels[i]}");
        }
#endif
    }
}
