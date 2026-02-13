using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// ADDRESSABLES VERSION - Loads sprite frames via Addressables using chunked loading for WebGL optimization
    /// Requires SpriteFramesData ScriptableObject to define chunks for progressive loading
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFramePlayer : MonoBehaviour, IFrameSequencePlayer
    {
        [Header("Chunked Loading Configuration")]
        [Tooltip("SpriteFramesData asset defining chunks and playback settings (REQUIRED)")]
        [SerializeField] private SpriteFramesData spriteFramesData;

        [Tooltip("Preload all chunks immediately on Awake (recommended for smooth playback)")]
        [SerializeField] private bool preloadOnAwake = true;

        [Header("Playback Settings")]
        [Tooltip("Target playback FPS")]
        public float playbackFPS = 30f;

        [Header("Playback Controls")]
        public bool playOnLoad = true; // Play automatically after frames are loaded
        public bool loop = true;

        [Header("Completion Callback")]
        [Tooltip("Called when playback finishes (non-looping videos only)")]
        public System.Action onPlaybackComplete;

        [Header("Audio Settings")]
        [Tooltip("Volume for audio playback (0-1)")]
        [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

        [Header("Debug Info")]
        [SerializeField] private List<Sprite> frames = new List<Sprite>(); // Loaded frames
        [SerializeField] private int currentFrameIndex = 0;
        [SerializeField] private bool isPlaying = false;
        [SerializeField] private bool isLoaded = false;
        [SerializeField] private bool isAudioLoaded = false;
        [SerializeField] private int currentChunkIndex = -1;
        [SerializeField] private List<int> loadedChunks = new List<int>();
        [SerializeField] private bool allChunksPreloaded = false; // True when all chunks are cached

        private SpriteRenderer spriteRenderer;
        private AudioSource audioSource;
        private AudioClip audioClip;
        private float frameTimer = 0f;
        private float frameDuration = 0f;
        private bool hasAudio = false;
        private bool framesLoaded = false;
        private bool audioLoaded = false;
        private bool hasFinished = false;
        private Dictionary<int, List<Sprite>> chunkFrames = new Dictionary<int, List<Sprite>>(); // Chunk index -> frames
        private Dictionary<int, AsyncOperationHandle<IList<Sprite>>> chunkHandles = new Dictionary<int, AsyncOperationHandle<IList<Sprite>>>(); // For proper cleanup

        /// <summary>
        /// Public property to check if all assets are loaded
        /// True when all chunks are cached in browser AND first chunk is in RAM AND audio is loaded
        /// </summary>
        public bool IsLoaded => isLoaded;

        public event System.Action PlaybackCompleted
        {
            add => onPlaybackComplete += value;
            remove => onPlaybackComplete -= value;
        }

        private void Awake()
        {
            if (spriteFramesData == null)
            {
                Debug.LogError("SpriteFramePlayer: spriteFramesData is required! Please assign a SpriteFramesData asset in the Inspector.");
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();

            // Setup playback settings from SpriteFramesData
            playbackFPS = spriteFramesData.playbackFPS;
            loop = spriteFramesData.loop;
            frameDuration = 1f / playbackFPS;

            // Setup AudioSource if audio is needed
            hasAudio = !string.IsNullOrEmpty(spriteFramesData.audioAddress);
            if (hasAudio)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.loop = loop;
                audioSource.volume = audioVolume;
            }

            // Preload all chunks for smooth playback
            if (preloadOnAwake)
            {
                PreloadAllChunks();
            }
        }

        /// <summary>
        /// Load only the first chunk - marks player as "loaded" once first chunk is ready
        /// Used for preloading and initial playback
        /// </summary>
        public void LoadFirstChunk()
        {
            if (spriteFramesData == null || spriteFramesData.chunks.Count == 0)
            {
                Debug.LogError("SpriteFramePlayer: No spriteFramesData or chunks defined!");
                return;
            }

            // Loading first chunk
            LoadChunk(0);
        }

        /// <summary>
        /// Load a specific chunk by index
        /// </summary>
        public void LoadChunk(int chunkIndex, System.Action<bool> onComplete = null)
        {
            if (spriteFramesData == null || chunkIndex < 0 || chunkIndex >= spriteFramesData.chunks.Count)
            {
                Debug.LogError($"Invalid chunk index: {chunkIndex}");
                onComplete?.Invoke(false);
                return;
            }

            if (loadedChunks.Contains(chunkIndex))
            {
                // Chunk already loaded
                onComplete?.Invoke(true);
                return;
            }

            var chunk = spriteFramesData.chunks[chunkIndex];
            // Loading chunk

            StartCoroutine(LoadChunkCoroutine(chunkIndex, chunk, onComplete));
        }

        private System.Collections.IEnumerator LoadChunkCoroutine(int chunkIndex, SpriteFramesData.FrameChunk chunk, System.Action<bool> onComplete)
        {
            bool loadComplete = false;
            bool loadSuccess = false;
            AsyncOperationHandle<IList<Sprite>> resultHandle = default;

            // Use AddressablesOperationManager for deduplication and centralized tracking
            AddressablesOperationManager.Instance.LoadLabelWithHandle<Sprite>(
                chunk.addressableLabel, 
                (handle, sprites) =>
                {
                    if (sprites != null && sprites.Count > 0)
                    {
                        // Sort frames by name
                        var sortedSprites = new List<Sprite>(sprites);
                        sortedSprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

                        // Store chunk frames and handle
                        chunkFrames[chunkIndex] = sortedSprites;
                        chunkHandles[chunkIndex] = handle;
                        loadedChunks.Add(chunkIndex);

                        // Chunk loaded

                        // If this is the first chunk, set up for playback
                        if (chunkIndex == 0)
                        {
                            frames.Clear();
                            frames.AddRange(sortedSprites);
                            currentChunkIndex = 0;
                            framesLoaded = true;
                            // DON'T call CheckIfFullyLoaded() here - wait for PreloadAllChunks to complete
                        }

                        loadSuccess = true;
                    }
                    else
                    {
                        Debug.LogError($"Failed to load chunk {chunkIndex} with label: {chunk.addressableLabel}");
                        loadSuccess = false;
                    }

                    loadComplete = true;
                }
            );

            // Wait for load to complete
            yield return new WaitUntil(() => loadComplete);

            onComplete?.Invoke(loadSuccess);
        }

        /// <summary>
        /// Unload a specific chunk to free memory
        /// </summary>
        public void UnloadChunk(int chunkIndex)
        {
            if (!loadedChunks.Contains(chunkIndex))
            {
                return;
            }

            var chunk = spriteFramesData.chunks[chunkIndex];
            // Unloading chunk

            // Release the addressables handle if we have it
            if (chunkHandles.ContainsKey(chunkIndex))
            {
                Addressables.Release(chunkHandles[chunkIndex]);
                chunkHandles.Remove(chunkIndex);
            }

            chunkFrames.Remove(chunkIndex);
            loadedChunks.Remove(chunkIndex);
        }

        /// <summary>
        /// Preload all chunks sequentially (for caching in browser)
        /// </summary>
        public void PreloadAllChunks(System.Action<bool> onComplete = null)
        {
            if (spriteFramesData == null)
            {
                Debug.LogError("No spriteFramesData assigned");
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(PreloadAllChunksCoroutine(onComplete));
        }

        private System.Collections.IEnumerator PreloadAllChunksCoroutine(System.Action<bool> onComplete)
        {
            // Preloading chunks...

            // Load all chunks first
            for (int i = 0; i < spriteFramesData.chunks.Count; i++)
            {
                bool loaded = false;
                bool success = false;

                LoadChunk(i, (result) =>
                {
                    success = result;
                    loaded = true;
                });

                // Wait for chunk to load
                yield return new WaitUntil(() => loaded);

                if (!success)
                {
                    Debug.LogError($"Failed to preload chunk {i}");
                    onComplete?.Invoke(false);
                    yield break;
                }

                // Keep first TWO chunks in RAM for smooth playback start
                // Unload chunks 2 onwards (they're cached in browser)
                if (i > 1)
                {
                    yield return new WaitForSeconds(0.1f); // Brief delay
                    UnloadChunk(i);
                }
            }

            allChunksPreloaded = true;
            // All chunks preloaded

            // Load audio if needed
            if (hasAudio && !string.IsNullOrEmpty(spriteFramesData.audioAddress))
            {
                bool audioLoadComplete = false;

                AddressablesAssetLoader.Instance.LoadAsset<AudioClip>(spriteFramesData.audioAddress, (clip) =>
                {
                    if (clip != null)
                    {
                        audioClip = clip;
                        if (audioSource != null)
                        {
                            audioSource.clip = audioClip;
                        }
                        audioLoaded = true;
                        isAudioLoaded = true;
                        // Audio clip loaded
                    }
                    else
                    {
                        Debug.LogError($"Failed to load audio with address: {spriteFramesData.audioAddress}");
                        audioLoaded = true; // Mark as loaded so it doesn't block
                    }
                    audioLoadComplete = true;
                });

                yield return new WaitUntil(() => audioLoadComplete);
            }
            else
            {
                audioLoaded = true; // No audio needed
            }

            // Check if we're now fully loaded
            CheckIfFullyLoaded();

            onComplete?.Invoke(true);
        }

        /// <summary>
        /// Check if all assets are loaded (all chunks cached + first chunk in RAM + audio loaded)
        /// </summary>
        private void CheckIfFullyLoaded()
        {
            bool assetsReady = allChunksPreloaded && framesLoaded && (!hasAudio || audioLoaded);

            if (assetsReady && !isLoaded)
            {
                isLoaded = true;
                // All assets loaded

                // Auto-play if enabled
                if (playOnLoad)
                {
                    Play();
                }
            }
        }

        private void Update()
        {
            if (!isPlaying || frames.Count == 0)
                return;

            frameTimer += Time.deltaTime;

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;

                currentFrameIndex++;

                // Handle chunked playback
                HandleChunkedPlayback();

                UpdateSprite();
            }
        }

        /// <summary>
        /// Handle progressive chunk loading during playback
        /// Uses sliding window: always keeps current + next chunk in RAM
        /// </summary>
        private void HandleChunkedPlayback()
        {
            int totalFrames = spriteFramesData.GetTotalFrames();

            if (currentFrameIndex >= totalFrames)
            {
                if (loop)
                {
                    currentFrameIndex = 0;
                    // Reload first chunk if needed
                    if (!loadedChunks.Contains(0))
                    {
                        LoadChunk(0);
                    }
                }
                else
                {
                    currentFrameIndex = totalFrames - 1;
                    isPlaying = false;
                    OnVideoFinished();
                    return;
                }
            }

            // Check if we need to switch to next chunk
            int targetChunkIndex = spriteFramesData.GetChunkIndexForFrame(currentFrameIndex);
            if (targetChunkIndex != currentChunkIndex)
            {
                // Switch to next chunk (should already be loaded from sliding window)
                if (loadedChunks.Contains(targetChunkIndex))
                {
                    currentChunkIndex = targetChunkIndex;
                    frames = chunkFrames[targetChunkIndex];
                    // Switched to chunk

                    // Sliding window: Load next chunk if exists
                    int nextChunkIndex = targetChunkIndex + 1;
                    if (nextChunkIndex < spriteFramesData.chunks.Count && !loadedChunks.Contains(nextChunkIndex))
                    {
                        // Loading next chunk
                        LoadChunk(nextChunkIndex);
                    }

                    // Unload chunks outside the sliding window (keep current and next only)
                    for (int i = 0; i < targetChunkIndex; i++)
                    {
                        if (loadedChunks.Contains(i))
                        {
                            UnloadChunk(i);
                        }
                    }
                }
                else
                {
                    // CRITICAL ERROR: Next chunk not in sliding window
                    Debug.LogError($"CRITICAL: Chunk {targetChunkIndex} not in sliding window! This means PreloadAllChunks didn't work correctly.");
                    isPlaying = false;
                    LoadChunk(targetChunkIndex, (success) =>
                    {
                        if (success)
                        {
                            currentChunkIndex = targetChunkIndex;
                            frames = chunkFrames[targetChunkIndex];
                            isPlaying = true;
                            // Emergency loaded chunk
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Start playing the frame sequence (and audio if available)
        /// </summary>
        public void Play()
        {
            if (!isLoaded)
            {
                Debug.LogWarning("Assets not loaded yet. Call PreloadAllChunks() first.");
                return;
            }

            if (frames.Count == 0)
            {
                Debug.LogWarning("No frames loaded yet.");
                return;
            }

            // Start audio playback synchronized with video
            if (hasAudio && audioSource != null && audioClip != null)
            {
                audioSource.time = 0f;
                audioSource.Play();
            }

            isPlaying = true;
            frameTimer = 0f;
            currentFrameIndex = 0;
            UpdateSprite();
        }

        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            isPlaying = false;

            // Pause audio as well
            if (hasAudio && audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        /// <summary>
        /// Stop playback and reset to first frame
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            currentFrameIndex = 0;
            frameTimer = 0f;
            UpdateSprite();

            // Stop audio as well
            if (hasAudio && audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        /// <summary>
        /// Jump to a specific frame
        /// </summary>
        public void SetFrame(int frameIndex)
        {
            if (frames.Count == 0)
                return;

            currentFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Count - 1);
            UpdateSprite();
        }

        /// <summary>
        /// Set playback speed (FPS)
        /// </summary>
        public void SetPlaybackFPS(float fps)
        {
            playbackFPS = Mathf.Max(1f, fps);
            frameDuration = 1f / playbackFPS;
        }

        private void UpdateSprite()
        {
            if (spriteRenderer == null || currentChunkIndex < 0)
                return;

            // Calculate local frame index within current chunk
            var chunk = spriteFramesData.chunks[currentChunkIndex];
            int localFrameIndex = currentFrameIndex - chunk.startFrame;

            if (localFrameIndex >= 0 && localFrameIndex < frames.Count)
            {
                spriteRenderer.sprite = frames[localFrameIndex];
            }
        }

        /// <summary>
        /// Get total number of loaded frames
        /// </summary>
        public int GetTotalFrames()
        {
            return frames.Count;
        }

        /// <summary>
        /// Get current frame index
        /// </summary>
        public int GetCurrentFrame()
        {
            return currentFrameIndex;
        }

        /// <summary>
        /// Check if currently playing
        /// </summary>
        public bool IsPlaying()
        {
            return isPlaying;
        }

        /// <summary>
        /// Get playback progress (0-1)
        /// </summary>
        public float GetProgress()
        {
            if (frames.Count == 0) return 0f;
            return (float)currentFrameIndex / frames.Count;
        }

        /// <summary>
        /// Check if all chunks have been preloaded (cached in browser)
        /// </summary>
        public bool AreAllChunksPreloaded()
        {
            return allChunksPreloaded;
        }

        /// <summary>
        /// Resume playback (for use after Pause)
        /// </summary>
        public void Resume()
        {
            if (!isLoaded || frames.Count == 0)
            {
                Debug.LogWarning("Cannot resume - no frames loaded");
                return;
            }

            isPlaying = true;

            // Resume audio as well
            if (hasAudio && audioSource != null && !audioSource.isPlaying)
            {
                audioSource.UnPause();
            }
        }

        /// <summary>
        /// Set audio volume (0-1)
        /// </summary>
        public void SetVolume(float volume)
        {
            audioVolume = Mathf.Clamp01(volume);
            if (hasAudio && audioSource != null)
            {
                audioSource.volume = audioVolume;
            }
        }

        /// <summary>
        /// Check if audio is loaded
        /// </summary>
        public bool IsAudioLoaded()
        {
            return isAudioLoaded;
        }

        /// <summary>
        /// Get the loaded audio clip
        /// </summary>
        public AudioClip GetAudioClip()
        {
            return audioClip;
        }

        /// <summary>
        /// Called when video playback finishes (non-looping only)
        /// </summary>
        private void OnVideoFinished()
        {
            if (!hasFinished)
            {
                hasFinished = true;
                onPlaybackComplete?.Invoke();
            }
        }

        /// <summary>
        /// Manually trigger playback completion (can be called by a skip button)
        /// </summary>
        public void SkipToEnd()
        {
            if (!hasFinished)
            {
                hasFinished = true;
                Stop();
                onPlaybackComplete?.Invoke();
            }
        }

        /// <summary>
        /// Clean up addressables handles when component is destroyed
        /// </summary>
        private void OnDestroy()
        {
            // Release all loaded chunk handles
            foreach (var handle in chunkHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            chunkHandles.Clear();
        }
    }
}
