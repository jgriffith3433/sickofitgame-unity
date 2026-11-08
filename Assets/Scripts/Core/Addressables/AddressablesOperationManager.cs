using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Collections;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;
using UObject = UnityEngine.Object;
using BoolCallbackList = System.Collections.Generic.List<System.Action<bool>>;
using HandleList = System.Collections.Generic.List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle>;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// One remote asset bundle that one or more Addressable labels resolve to.
    /// Produced by <see cref="AddressablesOperationManager.ResolveBundles"/>; bundles are
    /// deduplicated by URL so each physical bundle appears exactly once no matter how
    /// many labels reference it.
    /// </summary>
    public sealed class RemoteBundle
    {
        /// <summary>Fully transformed download URL. Unique key for the bundle.</summary>
        public string Url;
        /// <summary>Bundle file name from the catalog (falls back to the location's primary key).</summary>
        public string BundleName;
        /// <summary>Compressed size in bytes as recorded in the catalog. 0 if unknown.</summary>
        public long Size;
        /// <summary>Catalog location; pass to DownloadDependenciesAsync to fetch just this bundle.</summary>
        public IResourceLocation Location;
        /// <summary>Every label that resolved to this bundle.</summary>
        public readonly List<string> Labels = new List<string>();
    }

    /// <summary>
    /// Coordinates Addressables operations to prevent duplicate concurrent loads.
    /// Unity's Addressables system handles caching - we only provide deduplication.
    /// </summary>
    public class AddressablesOperationManager : MonoBehaviour
    {
        private static AddressablesOperationManager _instance;
        public static AddressablesOperationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("AddressablesOperationManager");
                    _instance = go.AddComponent<AddressablesOperationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Debug")]
        private bool showDebugLogs = false;
        [SerializeField] private List<string> activeOperations = new List<string>();

        [Header("Load Settings")]
        [Tooltip("Timeout for loading labels (seconds). Increase for large video assets.")]
        [SerializeField] private float labelLoadTimeout = 180f; // Increased from 90s for large video frames

        [Tooltip("Timeout for loading scenes (seconds).")]
        [SerializeField] private float sceneLoadTimeout = 120f;

        [Header("Scene Tracking")]
        [SerializeField] private string currentSceneName = "";
        [SerializeField] private string previousSceneName = "";

        private Dictionary<string, bool> inProgressLabelLoads = new Dictionary<string, bool>();
        private Dictionary<string, BoolCallbackList> pendingLabelLoadCallbacks = new Dictionary<string, BoolCallbackList>();

        private Dictionary<string, bool> inProgressTypedLabelLoads = new Dictionary<string, bool>();
        private Dictionary<string, bool> inProgressAssetLoads = new Dictionary<string, bool>();

        private Dictionary<string, bool> inProgressSceneLoads = new Dictionary<string, bool>();
        private Dictionary<string, BoolCallbackList> pendingSceneLoadCallbacks = new Dictionary<string, BoolCallbackList>();

        private Dictionary<string, AsyncOperationHandle<SceneInstance>> activeSceneHandles = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();

        // Per-scene tracking of asset handles (audio, prefabs, etc.) that callers don't own.
        // Released during scene transitions to prevent memory leaks on WebGL.
        private Dictionary<string, HandleList> sceneAssetHandles = new Dictionary<string, HandleList>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Loading Operations

        public void LoadLabel(string label, System.Action<bool> onComplete = null)
        {
            StartCoroutine(LoadLabelAsync(label, onComplete));
        }

        public void LoadLabel<T>(string label, System.Action<IList<T>> onComplete) where T : UObject
        {
            StartCoroutine(LoadLabelAsync<T>(label, onComplete));
        }

        public void LoadLabelWithHandle<T>(string label, System.Action<AsyncOperationHandle<IList<T>>, IList<T>> onComplete) where T : UObject
        {
            StartCoroutine(LoadLabelWithHandleAsync<T>(label, onComplete));
        }

        /// <summary>
        /// Same as LoadLabelWithHandle but also outputs the raw AsyncOperationHandle
        /// immediately so the caller can release it to cancel an in-flight load.
        /// </summary>
        public void LoadLabelWithHandle<T>(string label, System.Action<AsyncOperationHandle<IList<T>>, IList<T>> onComplete, out AsyncOperationHandle<IList<T>> immediateHandle) where T : UObject
        {
            try
            {
                immediateHandle = AddressablesAPI.LoadAssetsAsync<T>(label, null);
            }
            catch (InvalidKeyException)
            {
                Log($"No addressable locations for '{label}'. Skipping <{typeof(T).Name}> load.");
                immediateHandle = default;
                onComplete?.Invoke(default, null);
                return;
            }

            StartCoroutine(LoadLabelWithHandleTrackingAsync<T>(label, immediateHandle, onComplete));
        }

        public void LoadAsset<T>(string address, System.Action<T> onComplete) where T : UObject
        {
            StartCoroutine(LoadAssetAsync<T>(address, onComplete));
        }

        public void LoadScene(string sceneName, System.Action<bool> onComplete = null, UnityEngine.SceneManagement.LoadSceneMode mode = UnityEngine.SceneManagement.LoadSceneMode.Additive)
        {
            StartCoroutine(LoadSceneAsync(sceneName, mode, onComplete));
        }

        public void UnloadScene(string sceneName, System.Action<bool> onComplete = null)
        {
            StartCoroutine(UnloadSceneAsync(sceneName, onComplete));
        }

        public void GetLabelDownloadSize(string label, System.Action<long> onComplete)
        {
            StartCoroutine(GetDownloadSizeAsync(label, onComplete));
        }

        #endregion

        #region Status & Info

        public bool IsSceneLoadedThroughAddressables(string sceneName)
        {
            return activeSceneHandles.ContainsKey(sceneName);
        }

        /// <summary>
        /// Release an orphaned scene handle (e.g., when a scene was forcefully unloaded
        /// by SceneManager.LoadScene in Single mode). This prevents stale handles from
        /// blocking future loads of the same scene.
        /// </summary>
        public void ReleaseSceneHandle(string sceneName)
        {
            if (activeSceneHandles.ContainsKey(sceneName))
            {
                var handle = activeSceneHandles[sceneName];
                activeSceneHandles.Remove(sceneName);
                if (handle.IsValid())
                {
                    try { AddressablesAPI.Release(handle); }
                    catch (System.Exception e)
                    {
                        // Handle may already be invalid if scene was force-unloaded
                        Log($"[SCENE] Warning releasing handle for '{sceneName}': {e.Message}");
                    }
                }
                Log($"[SCENE] Released orphaned handle for '{sceneName}'");
            }
            // Also clean up any in-progress tracking
            inProgressSceneLoads.Remove(sceneName);
        }

        /// <summary>
        /// Track an asset handle under the current scene for cleanup during scene transitions.
        /// Used for handles from LoadAsset/LoadLabel where the caller receives the asset
        /// but doesn't own the handle (and therefore can't release it).
        /// </summary>
        private void TrackAssetHandleForCurrentScene(AsyncOperationHandle handle)
        {
            string scene = currentSceneName ?? "";
            if (!sceneAssetHandles.ContainsKey(scene))
                sceneAssetHandles[scene] = new HandleList();
            sceneAssetHandles[scene].Add(handle);
        }

        /// <summary>
        /// Release all tracked asset handles for a given scene. Call this during scene transitions
        /// to free audio clips, prefabs, and other assets that were loaded via LoadAsset/LoadLabel.
        /// </summary>
        public void ReleaseAllSceneAssetHandles(string sceneName)
        {
            string key = sceneName ?? "";
            if (!sceneAssetHandles.ContainsKey(key))
                return;

            var handles = sceneAssetHandles[key];
            int released = 0;
            foreach (var handle in handles)
            {
                if (handle.IsValid())
                {
                    try { AddressablesAPI.Release(handle); }
                    catch (System.Exception e)
                    {
                        Log($"Warning releasing tracked handle for scene '{key}': {e.Message}");
                    }
                    released++;
                }
            }
            sceneAssetHandles.Remove(key);
            if (released > 0)
                Log($"Released {released} tracked asset handle(s) for scene '{key}'");
        }

        public bool IsLoading(string label)
        {
            return (inProgressLabelLoads.ContainsKey(label) && inProgressLabelLoads[label]) ||
                   (inProgressTypedLabelLoads.ContainsKey(label) && inProgressTypedLabelLoads[label]) ||
                   (inProgressAssetLoads.ContainsKey(label) && inProgressAssetLoads[label]);
        }

        public bool IsLoadingScene(string sceneName)
        {
            return inProgressSceneLoads.ContainsKey(sceneName) && inProgressSceneLoads[sceneName];
        }

        public float GetCurrentSceneLoadProgress(string sceneName)
        {
            if (activeSceneHandles.ContainsKey(sceneName))
            {
                var handle = activeSceneHandles[sceneName];
                if (handle.IsValid()) return handle.PercentComplete;
            }
            return 0f;
        }

        public List<string> GetActiveOperations() => new List<string>(activeOperations);

        public int GetActiveOperationsCount() => activeOperations.Count;

        public void GetMemoryInfo(out long totalMemoryMB, out long usedMemoryMB, out long monoUsedMB, out long monoHeapMB)
        {
            totalMemoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            usedMemoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            monoUsedMB = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            monoHeapMB = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
        }

        #endregion

        #region Scene Tracking

        public void SetCurrentScene(string sceneName)
        {
            if (currentSceneName == sceneName) return;
            Log($"[SCENE] {currentSceneName} -> {sceneName}");
            previousSceneName = currentSceneName;
            currentSceneName = sceneName;
        }

        public void CompleteSceneTransition()
        {
            Log($"[SCENE] Transition complete: {currentSceneName}");
        }

        /// <summary>
        /// Cancel all in-progress label/typed-label/asset load coroutines.
        /// Called during scene transitions AFTER IFrameSequencePlayer resources
        /// have been released, to stop lingering tracking coroutines that hold
        /// stale AsyncOperationHandle copies in their state machines.
        /// Scene load coroutines are NOT affected (they run on AddressablesSceneLoader).
        /// </summary>
        public void CancelAllLabelLoads()
        {
            // Stop ALL coroutines on this MonoBehaviour. This is safe because
            // this is only called during the pre-release phase BEFORE the new
            // scene load starts (scene load coroutines are on AddressablesSceneLoader).
            StopAllCoroutines();

            int cancelled = inProgressTypedLabelLoads.Count + inProgressLabelLoads.Count + inProgressAssetLoads.Count;
            inProgressLabelLoads.Clear();
            inProgressTypedLabelLoads.Clear();
            inProgressAssetLoads.Clear();
            pendingLabelLoadCallbacks.Clear();
            activeOperations.Clear();

            if (cancelled > 0)
                Log($"[SCENE] Cancelled {cancelled} in-progress label load coroutine(s) for scene transition");
        }

        #endregion

        #region Implementation

        private IEnumerator LoadLabelAsync(string label, System.Action<bool> onComplete)
        {
            if (inProgressLabelLoads.ContainsKey(label) && inProgressLabelLoads[label])
            {
                Log($"[DEDUP] Label '{label}' already loading, queuing callback");
                if (!pendingLabelLoadCallbacks.ContainsKey(label))
                    pendingLabelLoadCallbacks[label] = new BoolCallbackList();
                pendingLabelLoadCallbacks[label].Add(onComplete);
                yield break;
            }

            inProgressLabelLoads[label] = true;
            activeOperations.Add($"Load: {label}");
            Log($"Loading label '{label}'...");

            AsyncOperationHandle<IList<UObject>> loadHandle;
            try
            {
                loadHandle = AddressablesAPI.LoadAssetsAsync<UObject>(label, null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Label '{label}' not found in Addressables catalog: {e.Message}");
                inProgressLabelLoads[label] = false;
                activeOperations.Remove($"Load: {label}");
                onComplete?.Invoke(false);
                if (pendingLabelLoadCallbacks.ContainsKey(label))
                {
                    foreach (var callback in pendingLabelLoadCallbacks[label])
                        callback?.Invoke(false);
                    pendingLabelLoadCallbacks.Remove(label);
                }
                yield break;
            }

            float elapsed = 0f;
            float lastProgress = 0f;

            while (!loadHandle.IsDone)
            {
                if (loadHandle.PercentComplete > lastProgress + 0.1f)
                {
                    lastProgress = loadHandle.PercentComplete;
                    Log($"Label '{label}' progress: {(loadHandle.PercentComplete * 100f):F1}%");
                }

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > labelLoadTimeout)
                {
                    Debug.LogError($"Label '{label}' load timeout after {labelLoadTimeout}s! Consider increasing labelLoadTimeout in AddressablesOperationManager.");
                    break;
                }

                yield return null;
            }

            bool success = loadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded label '{label}' ({loadHandle.Result.Count} assets)");
            else Debug.LogError($"Failed to load label '{label}': {loadHandle.OperationException}");

            inProgressLabelLoads[label] = false;
            activeOperations.Remove($"Load: {label}");
            onComplete?.Invoke(success);

            if (pendingLabelLoadCallbacks.ContainsKey(label))
            {
                Log($"[DEDUP] Calling {pendingLabelLoadCallbacks[label].Count} queued callbacks for '{label}'");
                foreach (var callback in pendingLabelLoadCallbacks[label])
                    callback?.Invoke(success);
                pendingLabelLoadCallbacks.Remove(label);
            }

            // Release the handle to free memory. On WebGL, the asset bundles are already
            // cached in the browser cache (UnityCache), so subsequent loads will hit the
            // local cache. Keeping the handle alive pins assets in WASM memory unnecessarily.
            if (loadHandle.IsValid())
            {
                AddressablesAPI.Release(loadHandle);
                Log($"Released label handle for '{label}' to free memory");
            }
        }

        private IEnumerator LoadLabelAsync<T>(string label, System.Action<IList<T>> onComplete) where T : UObject
        {
            string key = $"{label}+{typeof(T).Name}";

            if (inProgressTypedLabelLoads.ContainsKey(key) && inProgressTypedLabelLoads[key])
            {
                Log($"[DEDUP] Typed label '{label}<{typeof(T).Name}>' already loading");
                yield return new WaitUntil(() => !inProgressTypedLabelLoads.ContainsKey(key) || !inProgressTypedLabelLoads[key]);
            }

            inProgressTypedLabelLoads[key] = true;
            activeOperations.Add($"LoadTyped: {label}<{typeof(T).Name}>");
            Log($"Loading typed label '{label}' as <{typeof(T).Name}>...");

            var loadHandle = AddressablesAPI.LoadAssetsAsync<T>(label, null);

            float elapsed = 0f;
            float lastProgress = 0f;

            while (!loadHandle.IsDone)
            {
                if (loadHandle.PercentComplete > lastProgress + 0.1f)
                {
                    lastProgress = loadHandle.PercentComplete;
                    Log($"Label<{typeof(T).Name}> '{label}' progress: {(loadHandle.PercentComplete * 100f):F1}%");
                }

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > labelLoadTimeout)
                {
                    Debug.LogError($"Label<{typeof(T).Name}> '{label}' load timeout after {labelLoadTimeout}s!");
                    break;
                }

                yield return null;
            }

            bool success = loadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded typed label '{label}<{typeof(T).Name}>' ({loadHandle.Result.Count} assets)");
            else Debug.LogError($"Failed to load typed label '{label}<{typeof(T).Name}>': {loadHandle.OperationException}");

            inProgressTypedLabelLoads[key] = false;
            activeOperations.Remove($"LoadTyped: {label}<{typeof(T).Name}>");

            // Track handle for per-scene cleanup (caller doesn't receive the handle)
            if (loadHandle.IsValid())
                TrackAssetHandleForCurrentScene(loadHandle);

            onComplete?.Invoke(success ? loadHandle.Result : null);
        }

        private IEnumerator LoadLabelWithHandleAsync<T>(string label, System.Action<AsyncOperationHandle<IList<T>>, IList<T>> onComplete) where T : UObject
        {
            string key = $"{label}+{typeof(T).Name}";

            if (inProgressTypedLabelLoads.ContainsKey(key) && inProgressTypedLabelLoads[key])
            {
                Log($"[DEDUP] Typed label '{label}<{typeof(T).Name}>' already loading with handle");
                yield return new WaitUntil(() => !inProgressTypedLabelLoads.ContainsKey(key) || !inProgressTypedLabelLoads[key]);
            }

            inProgressTypedLabelLoads[key] = true;
            activeOperations.Add($"LoadTypedWithHandle: {label}<{typeof(T).Name}>");
            Log($"Loading typed label '{label}' as <{typeof(T).Name}> (returning handle)...");

            var loadHandle = AddressablesAPI.LoadAssetsAsync<T>(label, null);

            yield return TrackLoadProgressAsync<T>(label, loadHandle);

            bool success = loadHandle.IsValid() && loadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded typed label '{label}<{typeof(T).Name}>' ({loadHandle.Result.Count} assets) - returning handle");
            else if (loadHandle.IsValid()) Debug.LogError($"Failed to load typed label '{label}<{typeof(T).Name}>': {loadHandle.OperationException}");

            inProgressTypedLabelLoads[key] = false;
            activeOperations.Remove($"LoadTypedWithHandle: {label}<{typeof(T).Name}>");

            // Return both handle and result
            onComplete?.Invoke(loadHandle, success ? loadHandle.Result : null);
        }

        /// <summary>
        /// Variant that tracks a pre-created handle (used by the out-handle overload).
        /// </summary>
        private IEnumerator LoadLabelWithHandleTrackingAsync<T>(string label, AsyncOperationHandle<IList<T>> loadHandle, System.Action<AsyncOperationHandle<IList<T>>, IList<T>> onComplete) where T : UObject
        {
            string key = $"{label}+{typeof(T).Name}";

            inProgressTypedLabelLoads[key] = true;
            activeOperations.Add($"LoadTypedWithHandle: {label}<{typeof(T).Name}>");
            Log($"Loading typed label '{label}' as <{typeof(T).Name}> (returning handle)...");

            yield return TrackLoadProgressAsync<T>(label, loadHandle);

            bool success = loadHandle.IsValid() && loadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded typed label '{label}<{typeof(T).Name}>' ({loadHandle.Result.Count} assets) - returning handle");
            else if (loadHandle.IsValid() && loadHandle.OperationException is InvalidKeyException)
                Log($"No addressable locations for '{label}'. Nothing loaded.");
            else if (loadHandle.IsValid()) Debug.LogError($"Failed to load typed label '{label}<{typeof(T).Name}>': {loadHandle.OperationException}");

            inProgressTypedLabelLoads[key] = false;
            activeOperations.Remove($"LoadTypedWithHandle: {label}<{typeof(T).Name}>");

            // Handle may have been released externally (caller cancelled) — guard
            if (loadHandle.IsValid())
                onComplete?.Invoke(loadHandle, success ? loadHandle.Result : null);
        }

        /// <summary>
        /// Shared progress-tracking loop for label loads.
        /// </summary>
        private IEnumerator TrackLoadProgressAsync<T>(string label, AsyncOperationHandle<IList<T>> loadHandle) where T : UObject
        {
            float elapsed = 0f;
            float lastProgress = 0f;

            while (loadHandle.IsValid() && !loadHandle.IsDone)
            {
                if (loadHandle.PercentComplete > lastProgress + 0.1f)
                {
                    lastProgress = loadHandle.PercentComplete;
                    Log($"Label<{typeof(T).Name}> '{label}' progress: {(loadHandle.PercentComplete * 100f):F1}%");
                }

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > labelLoadTimeout)
                {
                    Debug.LogError($"Label<{typeof(T).Name}> '{label}' load timeout after {labelLoadTimeout}s!");
                    break;
                }

                yield return null;
            }
        }

        private IEnumerator LoadAssetAsync<T>(string address, System.Action<T> onComplete) where T : UObject
        {
            string key = $"{address}+{typeof(T).Name}";

            if (inProgressAssetLoads.ContainsKey(key) && inProgressAssetLoads[key])
            {
                Log($"[DEDUP] Asset '{address}<{typeof(T).Name}>' already loading");
                yield return new WaitUntil(() => !inProgressAssetLoads.ContainsKey(key) || !inProgressAssetLoads[key]);
            }

            inProgressAssetLoads[key] = true;
            activeOperations.Add($"LoadAsset: {address}<{typeof(T).Name}>");
            Log($"Loading asset '{address}' as <{typeof(T).Name}>...");

            var loadHandle = AddressablesAPI.LoadAssetAsync<T>(address);
            yield return loadHandle;

            bool success = loadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded asset '{address}<{typeof(T).Name}>'");
            else Debug.LogError($"Failed to load asset '{address}<{typeof(T).Name}>': {loadHandle.OperationException}");

            inProgressAssetLoads[key] = false;
            activeOperations.Remove($"LoadAsset: {address}<{typeof(T).Name}>");

            // Track handle for per-scene cleanup (caller doesn't receive the handle,
            // so we must release it when the scene transitions to prevent memory leaks)
            if (loadHandle.IsValid())
                TrackAssetHandleForCurrentScene(loadHandle);

            onComplete?.Invoke(success ? loadHandle.Result : default(T));
        }

        private IEnumerator LoadSceneAsync(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, System.Action<bool> onComplete)
        {
            if (inProgressSceneLoads.ContainsKey(sceneName) && inProgressSceneLoads[sceneName])
            {
                Log($"[DEDUP] Scene '{sceneName}' already loading, queuing callback");
                if (!pendingSceneLoadCallbacks.ContainsKey(sceneName))
                    pendingSceneLoadCallbacks[sceneName] = new BoolCallbackList();
                pendingSceneLoadCallbacks[sceneName].Add(onComplete);
                yield break;
            }

            inProgressSceneLoads[sceneName] = true;
            activeOperations.Add($"Scene: {sceneName}");
            Log($"Loading scene '{sceneName}' ({mode})...");

            var sceneHandle = AddressablesAPI.LoadSceneAsync(sceneName, mode);
            activeSceneHandles[sceneName] = sceneHandle;

            float lastProgress = 0f;
            float elapsed = 0f;

            while (!sceneHandle.IsDone)
            {
                if (sceneHandle.PercentComplete > lastProgress + 0.1f)
                {
                    lastProgress = sceneHandle.PercentComplete;
                    Log($"Scene '{sceneName}' progress: {(sceneHandle.PercentComplete * 100f):F1}%");
                }

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > sceneLoadTimeout)
                {
                    Debug.LogError($"Scene '{sceneName}' load timeout after {sceneLoadTimeout}s!");
                    Debug.LogError($"Status: {sceneHandle.Status}, Progress: {sceneHandle.PercentComplete}");
                    if (sceneHandle.OperationException != null)
                        Debug.LogError($"Exception: {sceneHandle.OperationException}");
                    break;
                }

                yield return null;
            }

            bool success = sceneHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Loaded scene '{sceneName}'");
            else Debug.LogError($"Failed to load scene '{sceneName}': {sceneHandle.OperationException}");

            inProgressSceneLoads[sceneName] = false;
            activeOperations.Remove($"Scene: {sceneName}");
            // DON'T remove handle - keep it for unloading later
            onComplete?.Invoke(success);

            if (pendingSceneLoadCallbacks.ContainsKey(sceneName))
            {
                Log($"[DEDUP] Calling {pendingSceneLoadCallbacks[sceneName].Count} queued scene callbacks for '{sceneName}'");
                foreach (var callback in pendingSceneLoadCallbacks[sceneName])
                    callback?.Invoke(success);
                pendingSceneLoadCallbacks.Remove(sceneName);
            }
        }

        private IEnumerator UnloadSceneAsync(string sceneName, System.Action<bool> onComplete)
        {
            if (!activeSceneHandles.ContainsKey(sceneName))
            {
                Debug.LogWarning($"Cannot unload scene '{sceneName}' - not tracked as loaded");
                onComplete?.Invoke(false);
                yield break;
            }

            Log($"Unloading scene '{sceneName}'...");
            var sceneHandle = activeSceneHandles[sceneName];

            // Validate handle before attempting unload
            if (!sceneHandle.IsValid())
            {
                Debug.LogWarning($"Scene handle for '{sceneName}' is invalid - may have been unloaded already");
                activeSceneHandles.Remove(sceneName);
                onComplete?.Invoke(true); // Consider this success since scene is already gone
                yield break;
            }

            activeSceneHandles.Remove(sceneName);

            var unloadHandle = AddressablesAPI.UnloadSceneAsync(sceneHandle);

            // Check if the unload operation handle is valid before waiting on it
            if (!unloadHandle.IsValid())
            {
                Debug.LogWarning($"Unload handle for scene '{sceneName}' is invalid - scene may have been unloaded already");
                onComplete?.Invoke(true); // Consider this success since scene is gone
                yield break;
            }

            yield return unloadHandle;

            // Check again if handle became invalid during the wait (race condition)
            if (!unloadHandle.IsValid())
            {
                Debug.LogWarning($"Unload handle for scene '{sceneName}' became invalid during unload - scene may have been unloaded already");
                onComplete?.Invoke(true); // Consider success since scene is gone
                yield break;
            }

            bool success = unloadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success) Log($"Unloaded scene '{sceneName}'");
            else Debug.LogError($"Failed to unload scene '{sceneName}': {unloadHandle.OperationException}");

            onComplete?.Invoke(success);
        }

        private IEnumerator GetDownloadSizeAsync(string label, System.Action<long> onComplete)
        {
            // Labels listed on SpriteFramesData but missing from a built group (Video4_Part1
            // is an empty group) throw InvalidKeyException inside GetDownloadSizeAsync.
            var locations = AddressablesAPI.LoadResourceLocationsAsync(label);
            yield return locations;
            bool hasLocations = locations.Status == AsyncOperationStatus.Succeeded
                && locations.Result != null
                && locations.Result.Count > 0;
            if (locations.IsValid())
                AddressablesAPI.Release(locations);

            if (!hasLocations)
            {
                Log($"No addressable locations for '{label}'. Treating download size as 0.");
                onComplete?.Invoke(0);
                yield break;
            }

            // GetDownloadSizeAsync uses Caching.IsVersionCached, which does not exist on
            // WebGL, so it reports the full bundle size even when UnityCache already has it.
            bool cachedInBrowser = false;
            yield return WebGlBundlesAreCached(label, hit => cachedInBrowser = hit);
            if (cachedInBrowser)
            {
                Log($"'{label}' is in the browser cache. Download size 0.");
                onComplete?.Invoke(0);
                yield break;
            }

            var handle = AddressablesAPI.GetDownloadSizeAsync(label);
            yield return handle;
            long size = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : 0;
            if (handle.IsValid())
                AddressablesAPI.Release(handle);
            onComplete?.Invoke(size);
        }

        /// <summary>
        /// Download and cache the bundle(s) for a label WITHOUT loading assets into memory.
        /// On WebGL this populates the browser cache so subsequent LoadAssetsAsync calls are instant.
        /// Returns download size of 0 if already cached.
        /// </summary>
        public void DownloadDependencies(string label, System.Action<bool> onComplete, System.Action<float> onProgress = null)
        {
            StartCoroutine(DownloadDependenciesAsync(label, onComplete, onProgress));
        }

        private IEnumerator DownloadDependenciesAsync(string label, System.Action<bool> onComplete, System.Action<float> onProgress)
        {
            // First check if we even need to download. Missing labels have no locations.
            var locations = AddressablesAPI.LoadResourceLocationsAsync(label);
            yield return locations;
            bool hasLocations = locations.Status == AsyncOperationStatus.Succeeded
                && locations.Result != null
                && locations.Result.Count > 0;
            if (locations.IsValid())
                AddressablesAPI.Release(locations);

            if (!hasLocations)
            {
                Log($"[Download] '{label}' has no addressable locations");
                onProgress?.Invoke(1f);
                onComplete?.Invoke(true);
                yield break;
            }

            bool cachedInBrowser = false;
            yield return WebGlBundlesAreCached(label, hit => cachedInBrowser = hit);
            if (cachedInBrowser)
            {
                Log($"[Download] '{label}' already in the browser cache");
                onProgress?.Invoke(1f);
                onComplete?.Invoke(true);
                yield break;
            }

            var sizeHandle = AddressablesAPI.GetDownloadSizeAsync(label);
            yield return sizeHandle;
            long downloadSize = sizeHandle.Status == AsyncOperationStatus.Succeeded ? sizeHandle.Result : 0;
            if (sizeHandle.IsValid())
                AddressablesAPI.Release(sizeHandle);

            if (downloadSize == 0)
            {
                Log($"[Download] '{label}' already cached (0 bytes to download)");
                onProgress?.Invoke(1f);
                onComplete?.Invoke(true);
                yield break;
            }

            Log($"[Download] Downloading dependencies for '{label}' ({downloadSize / 1024f / 1024f:F2} MB)...");
            activeOperations.Add($"Download: {label}");

            var downloadHandle = AddressablesAPI.DownloadDependenciesAsync(label);

            while (!downloadHandle.IsDone)
            {
                onProgress?.Invoke(downloadHandle.PercentComplete);
                yield return null;
            }

            bool success = downloadHandle.Status == AsyncOperationStatus.Succeeded;
            if (success)
                Log($"[Download] '{label}' cached successfully");
            else
                Debug.LogError($"[Download] Failed to cache '{label}': {downloadHandle.OperationException}");

            AddressablesAPI.Release(downloadHandle);
            activeOperations.Remove($"Download: {label}");
            onProgress?.Invoke(1f);
            onComplete?.Invoke(success);
        }

        #region Bundle-level pre-caching

        /// <summary>
        /// Resolve a set of labels to the unique asset bundles behind them, in a single
        /// catalog pass. Every label lookup is issued at once and the results are merged
        /// by bundle URL, so a bundle shared by many labels is returned exactly once.
        ///
        /// Returned as an IEnumerator (rather than started here) so the caller can drive it
        /// on its own MonoBehaviour and survive <see cref="CancelAllLabelLoads"/>.
        /// </summary>
        public IEnumerator ResolveBundles(IList<string> labels, System.Action<List<RemoteBundle>> onComplete)
        {
            var handles = new List<KeyValuePair<string, AsyncOperationHandle<IList<IResourceLocation>>>>(labels.Count);

            // Fire every lookup at once. These are catalog queries, not downloads.
            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                if (string.IsNullOrEmpty(label)) continue;
                try
                {
                    var handle = AddressablesAPI.LoadResourceLocationsAsync(label);
                    handles.Add(new KeyValuePair<string, AsyncOperationHandle<IList<IResourceLocation>>>(label, handle));
                }
                catch (System.Exception e)
                {
                    Log($"[Bundles] Could not resolve '{label}': {e.Message}");
                }
            }

            // Wait for all of them (waiting in order is fine - they're all already running).
            for (int i = 0; i < handles.Count; i++)
            {
                var handle = handles[i].Value;
                while (handle.IsValid() && !handle.IsDone)
                    yield return null;
            }

            var byUrl = new Dictionary<string, RemoteBundle>();
            var ordered = new List<RemoteBundle>();
            var pending = new Stack<IResourceLocation>();
            var seen = new HashSet<string>();

            for (int i = 0; i < handles.Count; i++)
            {
                string label = handles[i].Key;
                var handle = handles[i].Value;

                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    seen.Clear();
                    for (int l = 0; l < handle.Result.Count; l++)
                        pending.Push(handle.Result[l]);

                    // Walk each location and its dependency tree, collecting bundle locations.
                    while (pending.Count > 0)
                    {
                        var loc = pending.Pop();
                        if (loc == null || string.IsNullOrEmpty(loc.InternalId) || !seen.Add(loc.InternalId))
                            continue;

                        if (loc.ResourceType == typeof(IAssetBundleResource))
                            RegisterBundle(loc, label, byUrl, ordered);

                        if (loc.HasDependencies && loc.Dependencies != null)
                        {
                            for (int d = 0; d < loc.Dependencies.Count; d++)
                                pending.Push(loc.Dependencies[d]);
                        }
                    }
                }
                else
                {
                    Log($"[Bundles] No addressable locations for '{label}'");
                }

                if (handle.IsValid())
                    AddressablesAPI.Release(handle);
            }

            Log($"[Bundles] {labels.Count} label(s) -> {ordered.Count} unique bundle(s)");
            onComplete?.Invoke(ordered);
        }

        private void RegisterBundle(IResourceLocation loc, string label, Dictionary<string, RemoteBundle> byUrl, List<RemoteBundle> ordered)
        {
            BundleFetchLog.BeginResolve();
            string url;
            try { url = AddressablesAPI.ResourceManager.TransformInternalId(loc); }
            finally { BundleFetchLog.EndResolve(); }
            if (string.IsNullOrEmpty(url)) url = loc.InternalId;

            if (!byUrl.TryGetValue(url, out var bundle))
            {
                var options = loc.Data as AssetBundleRequestOptions;
                bundle = new RemoteBundle
                {
                    Url = url,
                    Location = loc,
                    BundleName = !string.IsNullOrEmpty(options?.BundleName) ? options.BundleName : loc.PrimaryKey,
                    Size = options != null ? options.BundleSize : 0,
                };
                byUrl[url] = bundle;
                ordered.Add(bundle);
            }

            if (!bundle.Labels.Contains(label))
                bundle.Labels.Add(label);
        }

        /// <summary>
        /// Determine which of the given bundles are already cached locally. All checks are
        /// issued concurrently. On WebGL this asks the browser's UnityCache directly (the
        /// only source of truth there); elsewhere it uses the catalog's size computation,
        /// which returns 0 for local bundles and for bundles already in Unity's cache.
        /// </summary>
        public IEnumerator CheckBundlesCached(IList<RemoteBundle> bundles, System.Action<HashSet<string>> onCachedUrls)
        {
            var cached = new HashSet<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
            int pending = bundles.Count;
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                RequestBundleCacheCheck(bundle.Url, hit =>
                {
                    if (hit) cached.Add(bundle.Url);
                    pending--;
                });
            }

            while (pending > 0)
                yield return null;
#else
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                var sizeData = bundle.Location?.Data as ILocationSizeData;
                long remaining = sizeData != null
                    ? sizeData.ComputeSize(bundle.Location, AddressablesAPI.ResourceManager)
                    : 0;
                if (remaining == 0)
                    cached.Add(bundle.Url);
            }
#endif
            onCachedUrls?.Invoke(cached);
            yield break;
        }

        /// <summary>
        /// Start downloading a single bundle into the local cache without loading it into
        /// memory. The caller owns the returned handle and must release it when done; poll
        /// <c>handle.GetDownloadStatus()</c> for byte-level progress.
        /// </summary>
        public AsyncOperationHandle DownloadBundle(RemoteBundle bundle)
        {
            string opName = $"Download: {bundle.BundleName}";
            activeOperations.Add(opName);
            Log($"[Download] {bundle.BundleName} ({bundle.Size / 1024f / 1024f:F2} MB)...");

            var handle = AddressablesAPI.DownloadDependenciesAsync(
                new List<IResourceLocation>(1) { bundle.Location }, false);
            handle.Completed += _ => activeOperations.Remove(opName);
            return handle;
        }

        #endregion

#if UNITY_WEBGL && !UNITY_EDITOR
        private delegate void BundleCacheCallback(int requestId, int isCached);

        private static int nextCacheRequestId = 1;
        private static readonly Dictionary<int, System.Action<bool>> pendingCacheChecks =
            new Dictionary<int, System.Action<bool>>();

        [DllImport("__Internal")]
        private static extern void WebGL_IsBundleCached(int requestId, string url, BundleCacheCallback callback);

        [MonoPInvokeCallback(typeof(BundleCacheCallback))]
        private static void OnBundleCacheResult(int requestId, int isCached)
        {
            if (!pendingCacheChecks.TryGetValue(requestId, out var handler))
                return;
            pendingCacheChecks.Remove(requestId);
            handler(isCached != 0);
        }

        private static void RequestBundleCacheCheck(string url, System.Action<bool> onComplete)
        {
            int requestId = nextCacheRequestId++;
            pendingCacheChecks[requestId] = onComplete;
            WebGL_IsBundleCached(requestId, url, OnBundleCacheResult);
        }
#endif

        /// <summary>
        /// True when every bundle for the label is already in Unity's browser cache.
        /// Outside WebGL this is always false; Caching.IsVersionCached handles that case.
        /// </summary>
        private IEnumerator WebGlBundlesAreCached(string label, System.Action<bool> onComplete)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var locations = AddressablesAPI.LoadResourceLocationsAsync(label);
            yield return locations;

            var urls = new List<string>();
            if (locations.Status == AsyncOperationStatus.Succeeded && locations.Result != null)
            {
                var seen = new HashSet<string>();
                var pending = new Stack<IResourceLocation>();
                for (int i = 0; i < locations.Result.Count; i++)
                    pending.Push(locations.Result[i]);

                while (pending.Count > 0)
                {
                    var loc = pending.Pop();
                    if (loc == null || string.IsNullOrEmpty(loc.InternalId) || !seen.Add(loc.InternalId))
                        continue;

                    string id = AddressablesAPI.ResourceManager.TransformInternalId(loc);
                    if (!string.IsNullOrEmpty(id) && id.Contains(".bundle"))
                        urls.Add(id);

                    if (loc.HasDependencies && loc.Dependencies != null)
                    {
                        for (int i = 0; i < loc.Dependencies.Count; i++)
                            pending.Push(loc.Dependencies[i]);
                    }
                }
            }

            if (locations.IsValid())
                AddressablesAPI.Release(locations);

            if (urls.Count == 0)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            int[] progress = new int[2];
            progress[0] = urls.Count;
            for (int i = 0; i < urls.Count; i++)
            {
                RequestBundleCacheCheck(urls[i], hit =>
                {
                    if (hit)
                        progress[1]++;
                    progress[0]--;
                });
            }

            while (progress[0] > 0)
                yield return null;

            onComplete?.Invoke(progress[1] == urls.Count);
#else
            onComplete?.Invoke(false);
            yield break;
#endif
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[AddressablesOp] {message}");
        }

        #endregion
    }
}
