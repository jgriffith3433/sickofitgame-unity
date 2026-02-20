using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Plays sprite frame sequences loaded via Addressables with chunked loading for WebGL optimization.
    /// Requires a SpriteFramesData ScriptableObject to define chunks and playback settings.
    /// Uses a sliding window to keep current + next chunk in RAM while unloading previous chunks.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFramePlayer : MonoBehaviour, IFrameSequencePlayer
    {
        #region Serialized Fields

        [Header("Data")]
        [Tooltip("SpriteFramesData asset defining chunks and playback settings (REQUIRED)")]
        [SerializeField] private SpriteFramesData spriteFramesData;
        [Tooltip("Preload all chunks immediately on Awake (recommended for smooth playback)")]
        [SerializeField] private bool preloadOnAwake = true;

        [Header("WebGL Memory")]
        [Tooltip("On WebGL, limit how many chunks are preloaded upfront. 0 = no limit (preload all). " +
                 "Remaining chunks load via the sliding window during playback.")]
        [SerializeField] private int webGLMaxPreloadChunks = 3;

        [Tooltip("How many chunks ahead to preload during playback (sliding window lookahead)")]
        [SerializeField] private int chunksAheadToPreload = 2;

        [Header("Playback")]
        [Tooltip("Target playback FPS")]
        public float playbackFPS = 30f;
        public bool playOnLoad = true;
        [SerializeField] private bool loop = true;

        [Header("Audio")]
        [Tooltip("Volume for audio playback (0-1)")]
        [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

        [Header("Callbacks")]
        [Tooltip("Called when playback finishes (non-looping videos only)")]
        public System.Action onPlaybackComplete;

        #endregion

        #region Debug Info (Inspector)

        [Header("Debug")]
        [SerializeField] private List<Sprite> frames = new List<Sprite>();
        [SerializeField] private int currentFrameIndex;
        [SerializeField] private bool isPlaying;
        [SerializeField] private bool isLoaded;
        [SerializeField] private bool isAudioLoaded;
        [SerializeField] private int currentChunkIndex = -1;
        [SerializeField] private List<int> loadedChunks = new List<int>();
        [SerializeField] private bool allChunksPreloaded;

        #endregion

        #region Private State

        private SpriteRenderer spriteRenderer;
        private AudioSource audioSource;
        private AudioClip audioClip;
        private double playbackElapsedTime;
        private bool hasAudio;
        private bool framesLoaded;
        private bool audioLoaded;
        private bool hasFinished;
        private bool audioHasStartedPlaying;
        private readonly Dictionary<int, List<Sprite>> chunkFrames = new Dictionary<int, List<Sprite>>();
        private readonly Dictionary<int, AsyncOperationHandle<IList<Texture2D>>> chunkHandles = new Dictionary<int, AsyncOperationHandle<IList<Texture2D>>>();
        private readonly HashSet<int> loadingChunks = new HashSet<int>();
        private bool isResourcesReleased;

        #endregion

        #region Public Properties

        /// <summary>
        /// True when all chunks are loaded, first chunk is in RAM, and audio is ready.
        /// </summary>
        public bool IsLoaded => isLoaded;

        public event System.Action PlaybackCompleted
        {
            add => onPlaybackComplete += value;
            remove => onPlaybackComplete -= value;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (spriteFramesData == null)
            {
                Debug.LogError("[SpriteFramePlayer] spriteFramesData is required!");
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();

            // Apply settings from data asset
            playbackFPS = spriteFramesData.playbackFPS;
            loop = spriteFramesData.loop;

            // Setup audio
            hasAudio = !string.IsNullOrEmpty(spriteFramesData.audioAddress);
            if (hasAudio)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false; // We handle looping manually for completion detection
                audioSource.volume = audioVolume;
            }

            if (preloadOnAwake)
                PreloadAllChunks();
        }

        private void OnDestroy()
        {
            // If ReleaseAllResources() was already called (explicit cleanup before scene unload),
            // skip to avoid double-release which can cause errors.
            if (isResourcesReleased)
                return;

            // Fallback cleanup for cases where the object is destroyed without explicit cleanup.
            // Uses Destroy (deferred) since DestroyImmediate may not be safe in all OnDestroy contexts.
            foreach (var spriteList in chunkFrames.Values)
            {
                foreach (var sprite in spriteList)
                {
                    if (sprite != null)
                        Destroy(sprite);
                }
            }
            chunkFrames.Clear();

            foreach (var handle in chunkHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            chunkHandles.Clear();
        }

        /// <summary>
        /// Immediately release ALL resources: sprites (via DestroyImmediate), Addressable texture handles,
        /// and audio references. This MUST be called before scene unload so that Resources.UnloadUnusedAssets()
        /// can actually reclaim the texture memory. Using Destroy() alone defers sprite destruction to end of
        /// frame, leaving sprite→texture references alive when the cleanup runs.
        /// </summary>
        public void ReleaseAllResources()
        {
            if (isResourcesReleased)
                return;

            isResourcesReleased = true;
            isPlaying = false;
            isLoaded = false;

            // Stop audio immediately
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
            audioClip = null;

            // DestroyImmediate sprites so texture references are gone RIGHT NOW,
            // not deferred to end of frame. This is critical for WebGL memory.
            foreach (var spriteList in chunkFrames.Values)
            {
                foreach (var sprite in spriteList)
                {
                    if (sprite != null)
                        DestroyImmediate(sprite);
                }
            }
            chunkFrames.Clear();
            frames.Clear();
            loadedChunks.Clear();

            // Release Addressable texture handles to decrement ref counts.
            // With sprites already destroyed, nothing references the textures anymore.
            foreach (var handle in chunkHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            chunkHandles.Clear();

            // Clear sprite renderer reference
            if (spriteRenderer != null)
                spriteRenderer.sprite = null;

            Debug.Log($"[SpriteFramePlayer] Released all resources for '{gameObject.name}'");
        }

        #endregion

        #region Playback Controls

        /// <summary>
        /// Start playing the frame sequence (and audio if available).
        /// </summary>
        public void Play()
        {
            if (!isLoaded)
            {
                Debug.LogWarning("[SpriteFramePlayer] Assets not loaded yet.");
                return;
            }

            if (frames.Count == 0)
            {
                Debug.LogWarning("[SpriteFramePlayer] No frames loaded.");
                return;
            }

            audioHasStartedPlaying = false;
            hasFinished = false;

            // Initialize playback state BEFORE starting audio to ensure sync
            isPlaying = true;
            playbackElapsedTime = 0.0;
            currentFrameIndex = 0;
            UpdateSprite();

            // Enable renderer AFTER setting the sprite to avoid a blank frame
            if (spriteRenderer != null && !spriteRenderer.enabled)
                spriteRenderer.enabled = true;

            // Start audio AFTER visual playback is initialized
            if (hasAudio && audioSource != null && audioClip != null)
            {
                audioSource.time = 0f;
                audioSource.Play();
            }
        }

        /// <summary>
        /// Pause playback (preserves position).
        /// </summary>
        public void Pause()
        {
            isPlaying = false;

            if (hasAudio && audioSource != null && audioSource.isPlaying)
                audioSource.Pause();
        }

        /// <summary>
        /// Resume after Pause.
        /// </summary>
        public void Resume()
        {
            if (!isLoaded || frames.Count == 0)
                return;

            isPlaying = true;

            if (hasAudio && audioSource != null && !audioSource.isPlaying)
                audioSource.UnPause();
        }

        /// <summary>
        /// Stop playback and reset to first frame.
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            currentFrameIndex = 0;
            playbackElapsedTime = 0.0;
            audioHasStartedPlaying = false;
            hasFinished = false;
            UpdateSprite();

            if (hasAudio && audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        /// <summary>
        /// Immediately finish playback and fire the completion callback.
        /// </summary>
        public void SkipToEnd()
        {
            if (hasFinished)
                return;

            hasFinished = true;
            Stop();
            onPlaybackComplete?.Invoke();
        }

        /// <summary>
        /// Jump to a specific frame.
        /// </summary>
        public void SetFrame(int frameIndex)
        {
            if (frames.Count == 0)
                return;

            currentFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Count - 1);
            UpdateSprite();
        }

        /// <summary>
        /// Set playback speed.
        /// </summary>
        public void SetPlaybackFPS(float fps)
        {
            playbackFPS = Mathf.Max(1f, fps);
        }

        /// <summary>
        /// Set audio volume (0-1).
        /// </summary>
        public void SetVolume(float volume)
        {
            audioVolume = Mathf.Clamp01(volume);
            if (hasAudio && audioSource != null)
                audioSource.volume = audioVolume;
        }

        #endregion

        #region Queries

        public void SetSpriteRendererEnabled(bool enabled)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = enabled;
        }

        public int GetTotalFrames() => spriteFramesData != null ? spriteFramesData.GetStoredTotalFrames() : 0;
        public int GetCurrentFrame() => currentFrameIndex;
        public bool IsPlaying() => isPlaying;
        public bool AreAllChunksPreloaded() => allChunksPreloaded;
        public bool IsAudioLoaded() => isAudioLoaded;
        public AudioClip GetAudioClip() => audioClip;

        /// <summary>
        /// Get playback progress (0-1).
        /// </summary>
        public float GetProgress()
        {
            int totalFrames = GetTotalFrames();
            return totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0f;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!isPlaying || frames.Count == 0)
                return;

            // Track audio start
            if (hasAudio && audioSource != null && audioSource.isPlaying && !audioHasStartedPlaying)
                audioHasStartedPlaying = true;

            // Handle audio-driven completion: audio determines the "end" when present
            if (hasAudio && audioHasStartedPlaying && audioSource != null && !audioSource.isPlaying)
            {
                isPlaying = false;
                OnVideoFinished();
                return;
            }

            // Calculate target frame from elapsed time (sync to audio clock when available)
            double currentTime;
            if (hasAudio && audioHasStartedPlaying && audioSource != null && audioSource.isPlaying)
            {
                currentTime = audioSource.time;
                playbackElapsedTime = currentTime;
            }
            else
            {
                playbackElapsedTime += Time.deltaTime;
                currentTime = playbackElapsedTime;
            }

            int targetFrame = (int)(currentTime * playbackFPS);
            int totalFrames = spriteFramesData.GetStoredTotalFrames();

            // Handle frame-driven end/loop
            if (targetFrame >= totalFrames)
            {
                if (loop)
                {
                    // Wrap frames with modulo so they keep looping even while audio plays
                    targetFrame = targetFrame % totalFrames;
                }
                else
                {
                    currentFrameIndex = totalFrames - 1;
                    isPlaying = false;
                    UpdateSprite();
                    OnVideoFinished();
                    return;
                }
            }

            if (targetFrame != currentFrameIndex)
            {
                currentFrameIndex = targetFrame;
                HandleChunkedPlayback();
                UpdateSprite();
            }
        }

        /// <summary>
        /// Reset playback to the beginning (frame 0, chunk 0).
        /// </summary>
        private void ResetToStart()
        {
            currentFrameIndex = 0;
            playbackElapsedTime = 0.0;
            currentChunkIndex = 0;

            if (loadedChunks.Contains(0))
                frames = chunkFrames[0];
            else
                LoadChunk(0);
        }

        private void OnVideoFinished()
        {
            if (hasFinished)
                return;

            hasFinished = true;
            onPlaybackComplete?.Invoke();
        }

        #endregion

        #region Chunk Management

        /// <summary>
        /// Load only the first chunk (marks player as loaded once ready).
        /// </summary>
        public void LoadFirstChunk()
        {
            if (spriteFramesData == null || spriteFramesData.chunks.Count == 0)
            {
                Debug.LogError("[SpriteFramePlayer] No spriteFramesData or chunks defined!");
                return;
            }

            LoadChunk(0);
        }

        /// <summary>
        /// Load a specific chunk by index. No-op if already loaded.
        /// </summary>
        public void LoadChunk(int chunkIndex, System.Action<bool> onComplete = null)
        {
            if (spriteFramesData == null || chunkIndex < 0 || chunkIndex >= spriteFramesData.chunks.Count)
            {
                Debug.LogError($"[SpriteFramePlayer] Invalid chunk index: {chunkIndex}");
                onComplete?.Invoke(false);
                return;
            }

            if (loadedChunks.Contains(chunkIndex))
            {
                onComplete?.Invoke(true);
                return;
            }

            // Don't start a new coroutine if this chunk is already being loaded
            if (loadingChunks.Contains(chunkIndex))
            {
                // If caller needs a callback, wait for the existing load to finish
                if (onComplete != null)
                    StartCoroutine(WaitForChunkLoaded(chunkIndex, onComplete));
                return;
            }

            loadingChunks.Add(chunkIndex);
            StartCoroutine(LoadChunkCoroutine(chunkIndex, spriteFramesData.chunks[chunkIndex], onComplete));
        }

        /// <summary>
        /// Wait for an already-in-flight chunk load to finish, then invoke the callback.
        /// Used when emergency loading requests a chunk that preloading already started.
        /// </summary>
        private IEnumerator WaitForChunkLoaded(int chunkIndex, System.Action<bool> onComplete)
        {
            yield return new WaitUntil(() => loadedChunks.Contains(chunkIndex) || !loadingChunks.Contains(chunkIndex));
            onComplete.Invoke(loadedChunks.Contains(chunkIndex));
        }

        private IEnumerator LoadChunkCoroutine(int chunkIndex, SpriteFramesData.FrameChunk chunk, System.Action<bool> onComplete)
        {
            bool loadComplete = false;
            bool loadSuccess = false;
            List<Texture2D> loadedTextures = null;
            AsyncOperationHandle<IList<Texture2D>> loadedHandle = default;

            AddressablesOperationManager.Instance.LoadLabelWithHandle<Texture2D>(
                chunk.addressableLabel,
                (handle, textures) =>
                {
                    // Guard: if resources were released while this chunk was loading
                    // (e.g., player died mid-load), release the handle immediately
                    if (isResourcesReleased)
                    {
                        if (handle.IsValid())
                            Addressables.Release(handle);
                        loadComplete = true;
                        return;
                    }

                    if (textures != null && textures.Count > 0)
                    {
                        loadedTextures = new List<Texture2D>(textures);
                        loadedTextures.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                        loadedHandle = handle;
                        loadSuccess = true;
                    }
                    else
                    {
                        Debug.LogError($"[SpriteFramePlayer] Failed to load chunk {chunkIndex} (label: {chunk.addressableLabel})");
                    }

                    loadComplete = true;
                }
            );

            yield return new WaitUntil(() => loadComplete);

            // If textures loaded successfully, create sprites asynchronously across multiple frames
            if (loadSuccess && loadedTextures != null)
            {
                var sprites = new List<Sprite>();
                yield return CreateSpritesFromSheetsAsync(loadedTextures, chunk, sprites);

                chunkFrames[chunkIndex] = sprites;
                chunkHandles[chunkIndex] = loadedHandle;
                loadedChunks.Add(chunkIndex);

                // First chunk initializes the active frame list
                if (chunkIndex == 0)
                {
                    frames.Clear();
                    frames.AddRange(sprites);
                    currentChunkIndex = 0;
                    framesLoaded = true;
                }
            }

            loadingChunks.Remove(chunkIndex);
            onComplete?.Invoke(loadSuccess);
        }

        /// <summary>
        /// Unload a specific chunk: destroys sprites and releases texture handles.
        /// </summary>
        public void UnloadChunk(int chunkIndex)
        {
            if (!loadedChunks.Contains(chunkIndex))
                return;

            if (chunkFrames.ContainsKey(chunkIndex))
            {
                foreach (var sprite in chunkFrames[chunkIndex])
                {
                    if (sprite != null)
                        Destroy(sprite);
                }
                chunkFrames.Remove(chunkIndex);
            }

            if (chunkHandles.ContainsKey(chunkIndex))
            {
                Addressables.Release(chunkHandles[chunkIndex]);
                chunkHandles.Remove(chunkIndex);
            }

            loadedChunks.Remove(chunkIndex);
        }

        /// <summary>
        /// Preload all chunks sequentially, then load audio if needed.
        /// </summary>
        public void PreloadAllChunks(System.Action<bool> onComplete = null)
        {
            if (spriteFramesData == null)
            {
                Debug.LogError("[SpriteFramePlayer] No spriteFramesData assigned");
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(PreloadAllChunksCoroutine(onComplete));
        }

        private IEnumerator PreloadAllChunksCoroutine(System.Action<bool> onComplete)
        {
            int totalChunks = spriteFramesData.chunks.Count;
            int chunksToPreload = totalChunks;

#if UNITY_WEBGL
            // On WebGL, limit upfront chunk loading to reduce peak memory.
            // Remaining chunks will be loaded on-demand via the sliding window in HandleChunkedPlayback().
            if (webGLMaxPreloadChunks > 0 && totalChunks > webGLMaxPreloadChunks)
            {
                chunksToPreload = webGLMaxPreloadChunks;
                Debug.Log($"[SpriteFramePlayer] WebGL: Limiting preload to {chunksToPreload}/{totalChunks} chunks to save memory");
            }
#endif

            for (int i = 0; i < chunksToPreload; i++)
            {
                bool loaded = false;
                bool success = false;

                LoadChunk(i, result =>
                {
                    success = result;
                    loaded = true;
                });

                yield return new WaitUntil(() => loaded);

                if (!success)
                {
                    Debug.LogError($"[SpriteFramePlayer] Failed to preload chunk {i}");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            allChunksPreloaded = true;

            // Load audio if needed
            if (hasAudio && !string.IsNullOrEmpty(spriteFramesData.audioAddress))
            {
                bool audioLoadComplete = false;

                AddressablesAssetLoader.Instance.LoadAsset<AudioClip>(spriteFramesData.audioAddress, clip =>
                {
                    if (clip != null)
                    {
                        audioClip = clip;
                        if (audioSource != null)
                            audioSource.clip = audioClip;
                        audioLoaded = true;
                        isAudioLoaded = true;
                    }
                    else
                    {
                        Debug.LogError($"[SpriteFramePlayer] Failed to load audio: {spriteFramesData.audioAddress}");
                        audioLoaded = true; // Don't block on failed audio
                    }
                    audioLoadComplete = true;
                });

                yield return new WaitUntil(() => audioLoadComplete);
            }
            else
            {
                audioLoaded = true;
            }

            CheckIfFullyLoaded();
            onComplete?.Invoke(true);
        }

        private void CheckIfFullyLoaded()
        {
            if (!isLoaded && allChunksPreloaded && framesLoaded && (!hasAudio || audioLoaded))
            {
                isLoaded = true;
                if (playOnLoad)
                    Play();
            }
        }

        /// <summary>
        /// Sliding window chunk management during playback.
        /// Switches to the correct chunk, preloads upcoming chunks, and unloads previous chunks.
        /// </summary>
        private void HandleChunkedPlayback()
        {
            int targetChunkIndex = spriteFramesData.GetChunkIndexForFrame(currentFrameIndex);

            // Always preload upcoming chunks regardless of whether we switched chunks
            PreloadUpcomingChunks(targetChunkIndex);

            if (targetChunkIndex == currentChunkIndex)
                return;

            if (loadedChunks.Contains(targetChunkIndex))
            {
                currentChunkIndex = targetChunkIndex;
                frames = chunkFrames[targetChunkIndex];

                // Unload chunks we've moved past
                for (int i = 0; i < targetChunkIndex; i++)
                {
                    if (loadedChunks.Contains(i))
                        UnloadChunk(i);
                }
            }
            else
            {
                Debug.LogError($"[SpriteFramePlayer] Chunk {targetChunkIndex} not loaded! Emergency loading...");
                isPlaying = false;
                LoadChunk(targetChunkIndex, success =>
                {
                    if (success)
                    {
                        currentChunkIndex = targetChunkIndex;
                        frames = chunkFrames[targetChunkIndex];
                        isPlaying = true;
                    }
                });
            }
        }

        /// <summary>
        /// Preload multiple chunks ahead of the current playback position.
        /// </summary>
        private void PreloadUpcomingChunks(int fromChunkIndex)
        {
            int totalChunks = spriteFramesData.chunks.Count;
            for (int offset = 1; offset <= chunksAheadToPreload; offset++)
            {
                int futureChunk = fromChunkIndex + offset;
                if (futureChunk < totalChunks && !loadedChunks.Contains(futureChunk) && !loadingChunks.Contains(futureChunk))
                    LoadChunk(futureChunk);
            }
        }

        #endregion

        #region Sprite Creation

        /// <summary>
        /// Creates Sprite objects from loaded sprite sheet textures using grid layout.
        /// Frames are arranged left-to-right, top-to-bottom within each sheet.
        /// </summary>
        /// <summary>
        /// Creates sprites from texture sheets asynchronously across multiple frames to prevent frame drops.
        /// Yields every 2 sprites to spread work more evenly.
        /// </summary>
        private IEnumerator CreateSpritesFromSheetsAsync(List<Texture2D> sheetTextures, SpriteFramesData.FrameChunk chunk, List<Sprite> sprites)
        {
            int framesPerSheet = spriteFramesData.framesPerSheet;
            int cols = spriteFramesData.sheetColumns;
            int fw = spriteFramesData.frameWidth;
            int fh = spriteFramesData.frameHeight;
            float ppu = spriteFramesData.spritePixelsPerUnit;
            int totalFramesInChunk = chunk.frameCount;
            int framesCreated = 0;
            const int spritesPerFrame = 4; // Create 4 sprites per yield to finish chunks faster (~16 yields per chunk instead of 66)

            foreach (var tex in sheetTextures)
            {
                int texH = tex.height;
                for (int cell = 0; cell < framesPerSheet && framesCreated < totalFramesInChunk; cell++)
                {
                    int col = cell % cols;
                    int row = cell / cols;
                    float x = col * fw;
                    float y = texH - (row + 1) * fh;

                    // Don't read outside texture bounds (handles partial last sheets)
                    if (x + fw > tex.width || y < 0 || y + fh > texH)
                        break;

                    var sprite = Sprite.Create(tex, new Rect(x, y, fw, fh), new Vector2(0.5f, 0.5f), ppu);
                    sprite.name = $"frame_{chunk.startFrame + framesCreated:D5}";
                    sprites.Add(sprite);
                    framesCreated++;

                    // Yield every N sprites to spread work across more frames
                    if (framesCreated % spritesPerFrame == 0)
                        yield return null;
                }
            }
        }

        #endregion

        #region Display

        private void UpdateSprite()
        {
            if (spriteRenderer == null || currentChunkIndex < 0)
                return;

            var chunk = spriteFramesData.chunks[currentChunkIndex];
            int localIndex = currentFrameIndex - chunk.startFrame;
            int maxIndex = Mathf.Min(frames.Count, chunk.frameCount) - 1;

            if (localIndex >= 0 && maxIndex >= 0)
                spriteRenderer.sprite = frames[Mathf.Clamp(localIndex, 0, maxIndex)];
        }

        #endregion
    }
}
