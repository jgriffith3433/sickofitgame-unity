using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Addressables;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;
using UnityEngine.Events;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Pre-downloads Addressable bundles to browser cache without loading assets into memory.
    /// Place on the Main Menu to show download progress and cache game assets in the background.
    ///
    /// Pipeline: labels are resolved to their unique physical bundles in one catalog pass,
    /// every bundle's cache state is checked at once, and the uncached bundles are then
    /// downloaded several at a time (bounded by count and by total bytes in flight).
    /// Android and iOS use a higher count and byte budget than WebGL.
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

        [Tooltip("Download the main menu plus the level the player is on, instead of every bundle.")]
        [SerializeField] private bool downloadForCurrentLevel = true;

        [Tooltip("How many bundles download at the same time on WebGL and desktop. Browsers allow about " +
                 "6 connections per host. Bundles are deduplicated first, so two in-flight downloads never share one.")]
        [SerializeField] private int maxConcurrentDownloads = 6;

        [Tooltip("Cap on the combined size (MB) of bundles in flight at once. Bounds peak WebGL heap " +
                 "usage during pre-caching. A single bundle larger than this still downloads alone. 0 = no cap.")]
        [SerializeField] private int maxInFlightMegabytes = 256;

        [Tooltip("Concurrent downloads on Android and iOS. Native players are not limited to a browser's " +
                 "6 connections per host, and these downloads only fill the cache — they are not decompressed into memory.")]
        [SerializeField] private int mobileMaxConcurrentDownloads = 8;

        [Tooltip("Combined size (MB) of bundles in flight on Android and iOS. 0 = no cap.")]
        [SerializeField] private int mobileMaxInFlightMegabytes = 512;

        private bool showDebugLogs = false;

        // Singleton
        private static BundleDownloadManager _instance;
        public static BundleDownloadManager Instance => _instance;

        private Coroutine downloadRoutine;
        private int activeLevel = -1;

        // State
        private List<string> allLabels = new List<string>();
        private bool isDownloading = false;
        private int totalLabels = 0;
        private int totalBundles = 0;
        private int completedBundles = 0;
        private long totalDownloadBytes = 0;
        private long completedDownloadBytes = 0;
        private string currentLabel = "";

        private struct ActiveDownload
        {
            public RemoteBundle Bundle;
            public AsyncOperationHandle Handle;
        }

        private readonly List<ActiveDownload> activeDownloads = new List<ActiveDownload>();

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

        /// <summary>Total bytes that need downloading.</summary>
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

            int level = downloadForCurrentLevel ? CurrentLevelNumber() : 0;
            if (autoStartOnAwake)
                BeginDownload(level);
            else
            {
                activeLevel = level;
                BuildLabelList(level);
            }
        }

        /// <summary>
        /// Download the main menu bundles plus everything the given level needs
        /// (its intro video, backgrounds, death scene, and the video after it).
        /// Passing 0 downloads the full list.
        /// </summary>
        public void StartDownloadForLevel(int levelNumber)
        {
            if (!downloadForCurrentLevel)
                levelNumber = 0;

            if (levelNumber == activeLevel && (isDownloading || IsFullyCached))
                return;

            BeginDownload(levelNumber);
        }

        private void BeginDownload(int levelNumber)
        {
            if (downloadRoutine != null)
                StopCoroutine(downloadRoutine);
            CancelActiveDownloads();

            activeLevel = levelNumber;
            IsFullyCached = false;
            isDownloading = false;
            BuildLabelList(levelNumber);
            downloadRoutine = StartCoroutine(CheckAndDownload());
        }

        private static int CurrentLevelNumber()
        {
            if (GameProgressManager.Instance == null)
                return 1;
            return GameProgressManager.Instance.GetCurrentLevel();
        }

        /// <summary>
        /// Collect chunk labels for the main menu and, when a level is given, that level only.
        /// </summary>
        private void BuildLabelList(int levelNumber)
        {
            allLabels.Clear();
            var seen = new HashSet<string>();
            HashSet<SpriteFramesData> needed = null;
            HashSet<string> neededNames = null;
            if (downloadForCurrentLevel && levelNumber > 0)
            {
                needed = FramesForLevel(levelNumber);
                neededNames = new HashSet<string>();
                foreach (var frames in needed)
                {
                    if (frames != null)
                        neededNames.Add(frames.name);
                }
            }

            foreach (var data in spriteFramesDataAssets)
            {
                if (data == null) continue;
                if (needed != null && !needed.Contains(data) && !neededNames.Contains(data.name) && !IsMainMenuFrames(data))
                    continue;

                int chunkLimit = ChunkLimit(data, levelNumber);
                int added = 0;
                foreach (var chunk in data.chunks)
                {
                    if (chunkLimit >= 0 && added >= chunkLimit)
                        break;
                    if (!string.IsNullOrEmpty(chunk.addressableLabel) && seen.Add(chunk.addressableLabel))
                        allLabels.Add(chunk.addressableLabel);
                    added++;
                }
            }

            foreach (var label in additionalLabels)
            {
                if (!string.IsNullOrEmpty(label) && seen.Add(label))
                    allLabels.Add(label);
            }

            totalLabels = allLabels.Count;
            Log($"Built label list: {totalLabels} labels for level {levelNumber}");
        }

        /// <summary>
        /// How many leading chunks of this clip the level actually plays.
        /// A one-shot sequence shorter than the packed clip does not need the rest.
        /// A loop, or a layer that plays the whole level, needs every chunk.
        /// Returns -1 when there is no limit.
        /// </summary>
        private int ChunkLimit(SpriteFramesData data, int levelNumber)
        {
            if (!downloadForCurrentLevel || levelNumber <= 0 || data == null)
                return -1;

            GameDefinition def = GameDefinition.Instance;
            LevelConfig level = def != null ? def.GetLevel(levelNumber) : null;
            if (level == null || level.backgroundLayers == null)
                return -1;

            bool referenced = false;
            int lastFrame = -1;
            for (int i = 0; i < level.backgroundLayers.Count; i++)
            {
                BackgroundLayerConfig layer = level.backgroundLayers[i];
                if (layer == null || layer.spriteFramesData == null)
                    continue;
                bool same = layer.spriteFramesData == data || layer.spriteFramesData.name == data.name;
                if (!same)
                    continue;

                referenced = true;
                if (!layer.IsSequenced || layer.sequenceLoop || layer.sequenceDuration <= 0f)
                    return -1;

                float fps = data.playbackFPS > 0f ? data.playbackFPS : 30f;
                int frame = Mathf.CeilToInt(layer.sequenceDuration * fps) - 1;
                if (frame > lastFrame)
                    lastFrame = frame;
            }

            if (!referenced || lastFrame < 0 || data.chunks == null)
                return -1;

            int needed = 0;
            for (int i = 0; i < data.chunks.Count; i++)
            {
                needed++;
                if (data.chunks[i].endFrame >= lastFrame)
                    break;
            }

            // SpriteFramePlayer preloads the first three chunks before it reports ready.
            int preload = Mathf.Min(3, data.chunks.Count);
            return Mathf.Max(needed, preload);
        }

        private static bool IsMainMenuFrames(SpriteFramesData data)
        {
            return data.name.StartsWith("MainMenu");
        }

        /// <summary>
        /// Frame assets the player needs while on this level: the video that leads
        /// into it, the level itself, its death scene, and the video that follows it.
        /// </summary>
        private static HashSet<SpriteFramesData> FramesForLevel(int levelNumber)
        {
            var set = new HashSet<SpriteFramesData>();
            GameDefinition def = GameDefinition.Instance;
            if (def == null || levelNumber < 1 || levelNumber > def.TotalLevels)
                return set;

            LevelConfig level = def.GetLevel(levelNumber);
            if (level == null)
                return set;

            if (level.backgrounds != null)
            {
                for (int i = 0; i < level.backgrounds.Count; i++)
                    AddFrames(set, level.backgrounds[i]);
            }

            if (level.backgroundLayers != null)
            {
                for (int i = 0; i < level.backgroundLayers.Count; i++)
                {
                    if (level.backgroundLayers[i] != null)
                        AddFrames(set, level.backgroundLayers[i].spriteFramesData);
                }
            }

            AddTimeline(set, level.timeline);

            if (!string.IsNullOrEmpty(level.deathConfigName))
            {
                DeathConfig death = def.GetDeath(level.deathConfigName);
                if (death != null)
                {
                    AddFrames(set, death.spriteFramesData);
                    AddTimeline(set, death.timeline);
                }
            }

            for (int i = 0; i < def.TotalVideos; i++)
            {
                VideoConfig video = def.GetVideo(i + 1);
                if (video == null)
                    continue;
                bool leadsIntoLevel = video.nextLevel == levelNumber;
                bool followsLevel = video.afterLevel == levelNumber || video.videoNumber == level.nextVideoNumber;
                if (!leadsIntoLevel && !followsLevel)
                    continue;
                AddFrames(set, video.spriteFramesData);
                AddTimeline(set, video.timeline);
            }

            return set;
        }

        private static void AddFrames(HashSet<SpriteFramesData> set, ScriptableObject source)
        {
            if (source is SpriteFramesData frames)
                set.Add(frames);
        }

        private static void AddTimeline(HashSet<SpriteFramesData> set, TimelineConfig timeline)
        {
            if (timeline == null)
                return;

            if (timeline.events != null)
            {
                for (int i = 0; i < timeline.events.Count; i++)
                {
                    if (timeline.events[i] != null)
                        AddFrames(set, timeline.events[i].spriteFramesData);
                }
            }

            if (timeline.tracks == null)
                return;

            for (int i = 0; i < timeline.tracks.Count; i++)
            {
                TimelineTrackConfig track = timeline.tracks[i];
                if (track == null || track.clips == null)
                    continue;
                for (int c = 0; c < track.clips.Count; c++)
                {
                    if (track.clips[c] != null)
                        AddFrames(set, track.clips[c].spriteFramesData);
                }
            }
        }

        /// <summary>
        /// Public entry point to manually start the check + download process.
        /// </summary>
        public void StartDownload()
        {
            if (!isDownloading)
                BeginDownload(downloadForCurrentLevel ? CurrentLevelNumber() : 0);
        }

        /// <summary>
        /// Resolve labels to unique bundles, find the ones not yet cached, and download them
        /// in parallel.
        ///
        /// Working at the bundle level (instead of the label level) is what makes parallelism
        /// safe: labels that share a physical bundle collapse to one entry, so Unity is never
        /// asked to fetch/decompress the same bundle twice at once. Peak memory is bounded by
        /// the platform's concurrency and in-flight byte budget.
        /// </summary>
        private IEnumerator CheckAndDownload()
        {
            completedBundles = 0;
            totalBundles = 0;
            completedDownloadBytes = 0;
            totalDownloadBytes = 0;
            Progress = 0f;
            isDownloading = true;
            currentLabel = "Verifying cache...";
            UpdateStatus();

            var ops = AddressablesOperationManager.Instance;

            // Phase 1: one catalog pass resolves every label to its unique bundles.
            Log($"Phase 1: Resolving {allLabels.Count} labels to bundles...");
            List<RemoteBundle> bundles = null;
            yield return ops.ResolveBundles(allLabels, result => bundles = result);
            bundles ??= new List<RemoteBundle>();

            // Phase 2: ask the cache about every bundle at once.
            Log($"Phase 2: Checking cache for {bundles.Count} bundles...");
            HashSet<string> cachedUrls = null;
            yield return ops.CheckBundlesCached(bundles, result => cachedUrls = result);
            cachedUrls ??= new HashSet<string>();

            var pending = new List<RemoteBundle>(bundles.Count);
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                if (cachedUrls.Contains(bundle.Url))
                    continue;
                pending.Add(bundle);
                totalDownloadBytes += bundle.Size;
                Log($"  '{bundle.BundleName}': {FormatBytes(bundle.Size)} needs download ({bundle.Labels.Count} label(s))");
            }
            totalBundles = pending.Count;

            // Addressables serialises every web request through one queue, and initialization
            // resets it to the project setting (3). Raise it after that init — ResolveBundles
            // has already waited for it — or the parallel downloads get throttled back down.
            // Leave it raised afterwards so scene and chunk loads keep the wider pipe.
            int concurrency = EffectiveConcurrency;
            WebRequestQueue.SetMaxConcurrentRequests(concurrency);

            if (pending.Count == 0)
            {
                Log("All bundles already cached!");
                FinishDownload(failed: 0);
                yield break;
            }

            // Largest first + first-fit below keeps the pipe full: big bundles start early,
            // small ones fill whatever budget is left alongside them.
            pending.Sort((a, b) => b.Size.CompareTo(a.Size));

            long budget = EffectiveBudgetBytes;
            long inFlightBytes = 0;
            int failed = 0;

            Log($"Phase 3: Downloading {pending.Count} bundles ({FormatBytes(totalDownloadBytes)}) " +
                $"with up to {concurrency} in flight, {FormatBytes(budget == long.MaxValue ? 0 : budget)} budget");
            IsFullyCached = false;
            UpdateStatus();

            while (pending.Count > 0 || activeDownloads.Count > 0)
            {
                // Launch: scan the pending list for anything that fits in the remaining budget.
                for (int i = 0; i < pending.Count && activeDownloads.Count < concurrency;)
                {
                    var bundle = pending[i];
                    bool fits = activeDownloads.Count == 0 || inFlightBytes + bundle.Size <= budget;
                    if (!fits)
                    {
                        i++;
                        continue;
                    }

                    pending.RemoveAt(i);
                    var handle = ops.DownloadBundle(bundle);
                    activeDownloads.Add(new ActiveDownload { Bundle = bundle, Handle = handle });
                    inFlightBytes += bundle.Size;
                }

                // Poll: harvest finished downloads, sum partial progress of the rest.
                long partialBytes = 0;
                for (int i = activeDownloads.Count - 1; i >= 0; i--)
                {
                    var active = activeDownloads[i];
                    var handle = active.Handle;

                    if (!handle.IsValid())
                    {
                        activeDownloads.RemoveAt(i);
                        inFlightBytes -= active.Bundle.Size;
                        failed++;
                        Debug.LogError($"[BundleDownloadManager] Download handle for '{active.Bundle.BundleName}' became invalid.");
                        continue;
                    }

                    if (handle.IsDone)
                    {
                        bool success = handle.Status == AsyncOperationStatus.Succeeded;
                        if (!success)
                            Debug.LogError($"[BundleDownloadManager] Failed to download '{active.Bundle.BundleName}': {handle.OperationException}. Continuing with remaining bundles.");

                        AddressablesAPI.Release(handle);
                        activeDownloads.RemoveAt(i);
                        inFlightBytes -= active.Bundle.Size;

                        if (success)
                        {
                            completedBundles++;
                            completedDownloadBytes += active.Bundle.Size;
                            Log($"  Prepared '{active.Bundle.BundleName}' ({completedBundles}/{totalBundles})");
                        }
                        else
                        {
                            failed++;
                        }
                        continue;
                    }

                    partialBytes += DownloadedBytes(active);
                }

                if (totalDownloadBytes > 0)
                    Progress = Mathf.Clamp01((completedDownloadBytes + partialBytes) / (float)totalDownloadBytes);
                else
                    Progress = totalBundles > 0 ? (completedBundles + failed) / (float)totalBundles : 1f;

                currentLabel = DescribeActiveDownloads();
                UpdateStatus();
                yield return null;
            }

            FinishDownload(failed);
        }

        /// <summary>
        /// Android and iOS use their own knobs. A phone has no browser connection cap, and a
        /// download-only request writes the bundle straight to the Unity cache instead of
        /// decompressing it, so more of them can be in flight than on WebGL.
        /// </summary>
        private bool IsMobilePlayer =>
            Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;

        private int EffectiveConcurrency =>
            Mathf.Max(1, IsMobilePlayer ? mobileMaxConcurrentDownloads : maxConcurrentDownloads);

        private long EffectiveBudgetBytes
        {
            get
            {
                int megabytes = IsMobilePlayer ? mobileMaxInFlightMegabytes : maxInFlightMegabytes;
                return megabytes > 0 ? megabytes * 1024L * 1024L : long.MaxValue;
            }
        }

        private void FinishDownload(int failed)
        {
            IsFullyCached = failed == 0;
            Progress = 1f;
            isDownloading = false;
            currentLabel = "";

            if (IsFullyCached)
                Log($"All downloads complete! ({FormatBytes(totalDownloadBytes)} total, {completedBundles} bundle(s))");
            else
                Log($"Downloads finished with errors. {completedBundles}/{totalBundles} succeeded.");

            UpdateStatus();
            OnDownloadComplete?.Invoke();
        }

        /// <summary>Bytes received so far for an in-flight bundle, clamped to its catalog size.</summary>
        private static long DownloadedBytes(ActiveDownload active)
        {
            var status = active.Handle.GetDownloadStatus();
            long bytes = status.TotalBytes > 0
                ? status.DownloadedBytes
                : (long)(active.Handle.PercentComplete * active.Bundle.Size);
            return active.Bundle.Size > 0 ? System.Math.Min(bytes, active.Bundle.Size) : 0;
        }

        private string DescribeActiveDownloads()
        {
            if (activeDownloads.Count == 0)
                return "";

            var first = activeDownloads[0].Bundle;
            string name = first.Labels.Count > 0 ? first.Labels[0] : first.BundleName;
            return activeDownloads.Count > 1 ? $"{name} (+{activeDownloads.Count - 1} more)" : name;
        }

        /// <summary>Release every in-flight download handle (aborts the web requests).</summary>
        private void CancelActiveDownloads()
        {
            for (int i = 0; i < activeDownloads.Count; i++)
            {
                var handle = activeDownloads[i].Handle;
                if (handle.IsValid())
                    AddressablesAPI.Release(handle);
            }
            activeDownloads.Clear();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                CancelActiveDownloads();
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
            BuildLabelList(0);
        }

        [ContextMenu("List All Labels (Dry Run)")]
        private void ListAllLabels()
        {
            BuildLabelList(0);
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
