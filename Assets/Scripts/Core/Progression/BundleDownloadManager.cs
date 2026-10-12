using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Settings")]
        [Tooltip("Start downloading automatically on Awake")]
        [SerializeField] private bool autoStartOnAwake = true;

        [Tooltip("Maximum concurrent download size checks to avoid overwhelming the server")]
        [SerializeField] private int maxConcurrentChecks = 3;

        private bool showDebugLogs = false;

        // Singleton
        private static BundleDownloadManager _instance;
        public static BundleDownloadManager Instance => _instance;

        // State
        private List<string> allLabels = new List<string>();
        private bool isDownloading = false;
        private int totalLabels = 0;
        private int completedLabels = 0;
        private long totalDownloadBytes = 0;
        private long completedDownloadBytes = 0;
        private string currentLabel = "";

        /// <summary>Event fired when all downloads complete (success or failure).</summary>
        public UnityEvent OnDownloadComplete = new UnityEvent();

        /// <summary>True when all bundles are cached and no download is needed.</summary>
        public bool IsFullyCached { get; private set; }

        /// <summary>True while a download is in progress.</summary>
        public bool IsDownloading => isDownloading;

        /// <summary>Overall progress from 0 to 1.</summary>
        public float Progress { get; private set; }

        /// <summary>Current status text describing what the manager is doing.</summary>
        public string StatusText { get; private set; } = "";

        /// <summary>Formatted string for remaining download size, or empty if not applicable.</summary>
        public string SizeText { get; private set; } = "";

        /// <summary>Total bytes that need downloading (may include cached content due to WebGL revalidation).</summary>
        public long TotalDownloadBytes => totalDownloadBytes;

        /// <summary>Bytes completed so far.</summary>
        public long CompletedDownloadBytes => completedDownloadBytes;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Already running from a previous scene - destroy this duplicate
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            BuildLabelList();

            if (autoStartOnAwake)
                StartCoroutine(CheckAndDownload());
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
            completedLabels = 0;
            completedDownloadBytes = 0;
            totalDownloadBytes = 0;
            Progress = 0f;
            isDownloading = true;
            currentLabel = "Verifying cache...";
            UpdateStatus();

            // Phase 1: Check download sizes in batches to avoid overwhelming the server
            // Batch checking is much faster than sequential while respecting server limits
            Log($"Phase 1: Verifying cached content (max {maxConcurrentChecks} concurrent checks)...");
            var labelsToDownload = new List<string>();
            var labelSizes = new Dictionary<string, long>();
            var sizeResults = new Dictionary<string, long>();
            int checksCompleted = 0;
            int checksStarted = 0;
            int activeChecks = 0;

            // Process labels in batches
            while (checksCompleted < allLabels.Count)
            {
                // Launch new checks up to the concurrent limit
                while (activeChecks < maxConcurrentChecks && checksStarted < allLabels.Count)
                {
                    int index = checksStarted;
                    string capturedLabel = allLabels[index];
                    checksStarted++;
                    activeChecks++;

                    AddressablesOperationManager.Instance.GetLabelDownloadSize(capturedLabel, downloadSize =>
                    {
                        sizeResults[capturedLabel] = downloadSize;
                        checksCompleted++;
                        activeChecks--;
                    });
                }

                // Update progress
                float checkProgress = checksCompleted / (float)allLabels.Count;
                Progress = checkProgress * 0.1f; // Reserve 10% for checking phase
                currentLabel = $"Verifying cache ({checksCompleted}/{allLabels.Count})...";
                UpdateStatus();
                
                yield return null;
            }

            // Process results
            foreach (var kvp in sizeResults)
            {
                if (kvp.Value > 0)
                {
                    labelsToDownload.Add(kvp.Key);
                    labelSizes[kvp.Key] = kvp.Value;
                    totalDownloadBytes += kvp.Value;
                    Log($"  '{kvp.Key}': {FormatBytes(kvp.Value)} needs download");
                }
            }

            // Check if everything is already cached
            if (labelsToDownload.Count == 0)
            {
                Log("All bundles already cached!");
                IsFullyCached = true;
                Progress = 1f;
                isDownloading = false;

                UpdateStatus();
                OnDownloadComplete?.Invoke();
                yield break;
            }

            Log($"Phase 2: Preparing {labelsToDownload.Count} bundles ({FormatBytes(totalDownloadBytes)} total)");
            IsFullyCached = false;
            UpdateStatus();

            // Phase 2: Download each bundle sequentially.
            // MUST be sequential on WebGL - concurrent downloads of bundles that share the same
            // underlying asset bundle (e.g., 14 Level3 labels all map to one 106 MB bundle) cause
            // Unity to decompress the same bundle multiple times simultaneously, causing OOM.
            // Sequential also means after the first label downloads the shared bundle, subsequent
            // labels in the same group see download size = 0 and skip instantly.
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
                        UpdateStatus();
                    }
                );

                yield return new WaitUntil(() => downloadComplete);

                if (downloadSuccess)
                {
                    completedLabels++;
                    completedDownloadBytes += labelSize;
                    Progress = completedDownloadBytes / (float)totalDownloadBytes;
                    Log($"  Prepared '{label}' ({completedLabels}/{labelsToDownload.Count})");
                }
                else
                {
                    Debug.LogError($"[BundleDownloadManager] Failed to download '{label}'. Continuing with remaining bundles.");
                }

                UpdateStatus();
            }

            // Done
            IsFullyCached = completedLabels == labelsToDownload.Count;
            Progress = 1f;
            isDownloading = false;

            if (IsFullyCached)
            {
                Log($"All downloads complete! ({FormatBytes(totalDownloadBytes)} total)");
            }
            else
            {
                Log($"Downloads finished with errors. {completedLabels}/{labelsToDownload.Count} succeeded.");
            }

            UpdateStatus();
            OnDownloadComplete?.Invoke();
        }

        /// <summary>
        /// Update the StatusText and SizeText properties based on current state.
        /// </summary>
        private void UpdateStatus()
        {
            // Status text
            if (IsFullyCached)
                StatusText = "All content ready!";
            else if (isDownloading)
            {
                if (string.IsNullOrEmpty(currentLabel))
                    StatusText = "Verifying cache...";
                else if (currentLabel.Contains("Verifying") || currentLabel.Contains("Preparing"))
                    StatusText = currentLabel;
                else
                    StatusText = $"{(Progress * 100f):F0}% - Preparing {currentLabel}...";
            }
            else
                StatusText = "";

            // Size text
            if (isDownloading && totalDownloadBytes > 0)
            {
                long remaining = totalDownloadBytes - completedDownloadBytes;
                SizeText = $"{FormatBytes(remaining)} remaining";
            }
            else
            {
                SizeText = "";
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
        }

        [ContextMenu("List All Labels (Dry Run)")]
        private void ListAllLabels()
        {
            BuildLabelList();
        }

        [ContextMenu("Force Re-check Cache")]
        private void ForceRecheckCache()
        {
            if (Application.isPlaying)
            {
                StartDownload();
            }
            else
            {
                Debug.LogWarning("[BundleDownloadManager] Force re-check only works in play mode");
            }
        }
#endif
    }
}
