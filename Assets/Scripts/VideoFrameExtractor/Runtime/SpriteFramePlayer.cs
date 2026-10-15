using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using SickOfIt.Core;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Self-contained sprite frame player driven by GameClock.
    /// Replaces PlayableDirector + SpriteFrameTrack + SpriteFrameMixerBehaviour.
    /// 
    /// Takes all the lessons from SpriteFrameMixerBehaviour:
    ///   - Chunked Addressable loading with sliding window
    ///   - WebGL memory budget (max 2 chunks on WebGL)
    ///   - Sheet-based zero-allocation rendering via MaterialPropertyBlock
    ///   - Reference sprite for mesh geometry
    ///   - Cell offset precomputation
    /// 
    /// But instead of Timeline driving time, GameClock does.
    /// This means:
    ///   - Time can go forward OR backward (rewind)
    ///   - You can seek to any time instantly
    ///   - No PlayableDirector, no PlayableGraph overhead
    /// 
    /// Usage:
    ///   var player = go.AddComponent&lt;SpriteFramePlayer&gt;();
    ///   player.Initialize(spriteFramesData);
    ///   // Player auto-reads GameClock.Time each frame
    ///   // Or manually: player.SetTime(2.5f);
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFramePlayer : MonoBehaviour, IFrameSequencePlayer
    {
        // =====================================================================
        // CONFIG
        // =====================================================================

        [Header("Data")]
        [SerializeField] private SpriteFramesData data;

        [Header("Playback")]
        [SerializeField] private float fps = 30f;
        [SerializeField] private float startTime;
        [SerializeField] private bool loop;
        [SerializeField] private bool useGameClock = false; // Disable GameClock for now, just use Time.deltaTime
        [SerializeField] private bool autoPlay = true;

        [Header("Chunk Loading")]
        [SerializeField] private int initialChunksToPreload = 3;
        [SerializeField] private int chunksAheadToLoad = 2;
        [SerializeField] private int maxChunksInMemory = 5;

        [Header("Debug")]
        [SerializeField] private int currentFrameIndex;
        [SerializeField] private int totalFrames;
        [SerializeField] private bool isReady;

        // =====================================================================
        // INTERNAL STATE
        // =====================================================================

        private SpriteRenderer spriteRenderer;
        private float localTime;
        private bool isPlaying;
        private bool initialized;
        private bool released;

        // Chunk management
        private readonly Dictionary<int, List<Texture2D>> chunkTextures = new();
        private readonly Dictionary<int, AsyncOperationHandle<IList<Texture2D>>> chunkHandles = new();
        private readonly HashSet<int> loadedChunks = new();
        private readonly HashSet<int> loadingChunks = new();
        private int lastChunkIdx = -1;
        private int displayedChunkIdx = -1;
        private readonly List<int> evictCandidates = new();

        // Sheet-based rendering (zero allocation per frame)
        private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        private MaterialPropertyBlock mpb;
        private Vector3 baseLocalPosition;
        private Texture2D currentTexture;
        // One sprite per sheet cell, cut from a loaded sheet. Every sheet has the same layout, so the property
        // block swaps the texture underneath while the cell sprite keeps the frame's rectangle.
        private Sprite[] cellSprites;
        private int referenceSpriteChunkIdx = -1;

        // Ready tracking
        private int targetPreload;
        private System.Action onReady;

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        // IFrameSequencePlayer
        public bool IsLoaded => isReady;
        public void SetSpriteRendererEnabled(bool enabled)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = enabled;
        }
        public void ReleaseAllResources() => Release();

        public bool IsReady => isReady;
        /// <summary>True once a frame has been written onto the renderer. A cut waits for this so the clear color never shows through.</summary>
        public bool HasDrawnFrame => spriteRenderer != null && spriteRenderer.sprite != null;
        public bool IsPlaying => isPlaying;
        public int CurrentFrame => currentFrameIndex;
        public int TotalFrames => totalFrames;
        public float Progress => totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0f;
        public float Duration => totalFrames > 0 ? totalFrames / fps : 0f;

        /// <summary>
        /// Store the sequence on the component. Play mode Start loads it.
        /// Scene generators use this so the reference is saved with the scene.
        /// </summary>
        public void Bind(SpriteFramesData spriteData, bool clock)
        {
            data = spriteData;
            useGameClock = clock;
            autoPlay = true;
        }

        /// <summary>
        /// Initialize with a SpriteFramesData asset. Starts preloading chunks.
        /// </summary>
        public void Initialize(SpriteFramesData spriteData, System.Action onReadyCallback = null)
        {
            if (initialized) Release();

            data = spriteData;
            onReady = onReadyCallback;

            if (data == null)
            {
                Debug.LogError("[SpriteFramePlayer] No SpriteFramesData provided!");
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            mpb = new MaterialPropertyBlock();
            baseLocalPosition = transform.localPosition;

            if (data.playbackFPS > 0f)
                fps = data.playbackFPS;
            loop = data.loop;

            totalFrames = data.GetStoredTotalFrames();
            if (totalFrames <= 0)
                totalFrames = data.GetTotalFrames();

            // Apply WebGL memory constraints
#if UNITY_WEBGL && !UNITY_EDITOR
            maxChunksInMemory = Mathf.Min(maxChunksInMemory, 2);
            chunksAheadToLoad = Mathf.Min(chunksAheadToLoad, 1);
            initialChunksToPreload = Mathf.Min(initialChunksToPreload, maxChunksInMemory);
#endif

            // A label with no packed sheets throws InvalidKeyException if we ask Addressables for it.
            if (!data.HasBuiltFrames)
            {
                Debug.LogWarning($"[SpriteFramePlayer] '{data.videoName}' has no built sprite sheets. Skipping load.");
                initialized = true;
                isReady = true;
                onReady?.Invoke();
                return;
            }

            // Start preloading
            int totalChunks = data.chunks.Count;
            targetPreload = Mathf.Min(initialChunksToPreload, totalChunks);

            for (int i = 0; i < targetPreload; i++)
                LoadChunk(i, isInitial: true);

            initialized = true;

            if (totalChunks == 0)
            {
                isReady = true;
                onReady?.Invoke();
            }

        }

        /// <summary>
        /// Seek to a specific time. Works during forward play or rewind.
        /// </summary>
        public void SetTime(float time)
        {
            localTime = time - startTime;
            UpdateFrame();
        }

        /// <summary>
        /// Set the frame directly by index.
        /// </summary>
        public void SetFrame(int frame)
        {
            currentFrameIndex = Mathf.Clamp(frame, 0, totalFrames - 1);
            RenderFrame(currentFrameIndex);
        }

        public void Play() => isPlaying = true;
        public void SetLoop(bool shouldLoop) => loop = shouldLoop;

        /// <summary>
        /// Clock time at which this clip's frame 0 plays. Local time is the clock minus this.
        /// </summary>
        public void SetClockOrigin(float clockOrigin) => startTime = clockOrigin;

        /// <summary>
        /// Load the clip without following the clock. A sequenced background stays parked
        /// until its window opens, then LevelRuntime turns the clock on.
        /// </summary>
        public void PrepareForSequence(SpriteFramesData frames, float clockOrigin, bool shouldLoop)
        {
            autoPlay = false;
            startTime = clockOrigin;
            useGameClock = false;
            Initialize(frames);
            SetLoop(shouldLoop);
            Pause();
            SetSpriteRendererEnabled(false);
        }

        public void DriveFromClock(bool drive)
        {
            useGameClock = drive;
            if (drive)
                Play();
            else
                Pause();
        }
        public void UseGameClock(bool enabled) => useGameClock = enabled;
        public void Pause() => isPlaying = false;
        public void Stop() { isPlaying = false; localTime = 0; }

        /// <summary>
        /// Release all loaded chunks and handles. Call before destroying.
        /// </summary>
        public void Release()
        {
            if (released) return;
            released = true;

            chunkTextures.Clear();
            DestroyCellSprites();
            currentTexture = null;

            foreach (var kvp in chunkHandles)
            {
                if (kvp.Value.IsValid())
                    Addressables.Release(kvp.Value);
            }
            chunkHandles.Clear();
            loadedChunks.Clear();
            loadingChunks.Clear();
            initialized = false;
            isReady = false;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
                spriteRenderer.SetPropertyBlock(null);
            }
            transform.localPosition = baseLocalPosition;
        }

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mpb = new MaterialPropertyBlock();
            baseLocalPosition = transform.localPosition;
        }

        private void Start()
        {
            if (data != null && !initialized)
                Initialize(data);

            if (autoPlay)
                isPlaying = true;
        }

        private bool wasRewinding;

        private void Update()
        {
            if (!initialized || !isReady) return;

            bool rewinding = useGameClock && GameClock.Instance != null && GameClock.Instance.IsRewinding;
            if (rewinding && !wasRewinding && data != null)
            {
                int chunk = data.GetChunkIndexForFrame(currentFrameIndex);
                if (chunk >= 0)
                    EnsureChunksAround(chunk);
            }
            wasRewinding = rewinding;

            if (useGameClock && GameClock.Instance != null)
            {
                SetTime(GameClock.Instance.Time);
            }
            else if (isPlaying)
            {
                localTime += Time.deltaTime;
                UpdateFrame();
            }
        }

        private void OnDestroy()
        {
            Release();
        }

        // =====================================================================
        // FRAME CALCULATION
        // =====================================================================

        private void UpdateFrame()
        {
            if (totalFrames <= 0) return;

            float duration = totalFrames / fps;

            // Handle time → frame mapping
            float effectiveTime = localTime;

            if (loop && duration > 0)
            {
                effectiveTime = effectiveTime % duration;
                if (effectiveTime < 0) effectiveTime += duration;
            }
            else
            {
                effectiveTime = Mathf.Clamp(effectiveTime, 0, duration - (1f / fps));
            }

            float progress = effectiveTime / duration;
            int frame = Mathf.Clamp((int)(progress * totalFrames), 0, totalFrames - 1);

            if (frame != currentFrameIndex)
            {
                currentFrameIndex = frame;
                RenderFrame(frame);
            }
        }

        private void RenderFrame(int frameIndex)
        {
            if (data == null || spriteRenderer == null) return;

            int chunkIdx = data.GetChunkIndexForFrame(frameIndex);
            if (chunkIdx < 0) return;

            // Sliding window — only when chunk changes
            if (chunkIdx != lastChunkIdx)
            {
                lastChunkIdx = chunkIdx;
                EnsureChunksAround(chunkIdx);
            }

            // Render from loaded chunk
            if (!chunkTextures.ContainsKey(chunkIdx)) return;

            var chunk = data.chunks[chunkIdx];
            int localIndex = frameIndex - chunk.startFrame;
            if (localIndex < 0 || localIndex >= chunk.frameCount) return;

            var textures = chunkTextures[chunkIdx];
            int texIdx = localIndex / data.framesPerSheet;
            if (texIdx >= textures.Count) return;

            int cell = localIndex % data.framesPerSheet;
            if (cellSprites == null || cell >= cellSprites.Length) return;

            if (spriteRenderer.sprite != cellSprites[cell])
                spriteRenderer.sprite = cellSprites[cell];

            var tex = textures[texIdx];
            if (currentTexture != tex)
            {
                currentTexture = tex;
                mpb.SetTexture(MainTexID, tex);
                spriteRenderer.SetPropertyBlock(mpb);
            }

            displayedChunkIdx = chunkIdx;
        }

        // =====================================================================
        // CHUNK LOADING — identical memory management to SpriteFrameMixerBehaviour
        // =====================================================================

        private void LoadChunk(int chunkIndex, bool isInitial = false)
        {
            if (released) return;
            if (chunkIndex < 0 || chunkIndex >= data.chunks.Count) return;
            if (loadedChunks.Contains(chunkIndex)) return;
            if (loadingChunks.Contains(chunkIndex)) return;

            loadingChunks.Add(chunkIndex);
            var chunk = data.chunks[chunkIndex];

            AddressablesOperationManager.Instance.LoadLabelWithHandle<Texture2D>(
                chunk.addressableLabel,
                (handle, textures) =>
                {
                    OnChunkLoaded(chunkIndex, chunk, handle, textures, isInitial);
                },
                out var immediateHandle
            );

            chunkHandles[chunkIndex] = immediateHandle;
        }

        private void OnChunkLoaded(
            int chunkIndex,
            SpriteFramesData.FrameChunk chunk,
            AsyncOperationHandle<IList<Texture2D>> handle,
            IList<Texture2D> textures,
            bool isInitial)
        {
            if (released)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            if (!loadingChunks.Contains(chunkIndex))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            if (textures == null || textures.Count == 0)
            {
                Debug.LogError($"[SpriteFramePlayer] Chunk {chunkIndex} load failed (label: {chunk.addressableLabel})");
                loadingChunks.Remove(chunkIndex);
                return;
            }

            // Sort by name for deterministic order
            var sorted = new List<Texture2D>(textures);
            sorted.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            chunkTextures[chunkIndex] = sorted;
            chunkHandles[chunkIndex] = handle;
            loadedChunks.Add(chunkIndex);
            loadingChunks.Remove(chunkIndex);

            // Evict if over budget
            EvictIfNeeded(chunkIndex);

            // Cut the cell sprites (once)
            if (cellSprites == null && sorted.Count > 0)
            {
                CreateCellSprites(sorted[0]);
                referenceSpriteChunkIdx = chunkIndex;
            }

            // Check if initial preload complete
            if (isInitial && !isReady)
            {
                bool allLoaded = true;
                for (int i = 0; i < targetPreload; i++)
                {
                    if (!loadedChunks.Contains(i))
                    {
                        allLoaded = false;
                        break;
                    }
                }
                if (allLoaded)
                {
                    isReady = true;
                    onReady?.Invoke();
                }
            }

            // The playhead may already be sitting on this chunk (rewind stepped backward).
            if (cellSprites != null && totalFrames > 0)
                UpdateFrame();
        }

        // =====================================================================
        // SLIDING WINDOW — identical to SpriteFrameMixerBehaviour
        // =====================================================================

        /// <summary>
        /// Chunks to keep resident. Forward play keeps the upcoming chunk.
        /// Rewind keeps the chunks behind the playhead, which is the only way
        /// the picture can step backward.
        /// </summary>
        private void GetRetentionWindow(int center, int totalChunks, out int windowStart, out int windowEnd)
        {
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            int budget = Mathf.Max(1, maxChunksInMemory);
            int behind = rewinding ? budget - 1 : (budget > 2 ? 1 : 0);
            int ahead = rewinding ? 0 : Mathf.Min(chunksAheadToLoad, Mathf.Max(0, budget - 1));

            while (behind + ahead + 1 > budget)
            {
                if (!rewinding && ahead > 0)
                    ahead--;
                else if (behind > 0)
                    behind--;
                else if (ahead > 0)
                    ahead--;
                else
                    break;
            }

            windowStart = Mathf.Max(0, center - behind);
            windowEnd = Mathf.Min(totalChunks - 1, center + ahead);
        }

        private void EnsureChunksAround(int chunkIndex)
        {
            int totalChunks = data.chunks.Count;
            GetRetentionWindow(chunkIndex, totalChunks, out int windowStart, out int windowEnd);

            // Evict outside window
            bool protectDisplayed = displayedChunkIdx >= 0
                && displayedChunkIdx != chunkIndex
                && !loadedChunks.Contains(chunkIndex);

            evictCandidates.Clear();
            foreach (int idx in loadedChunks) evictCandidates.Add(idx);
            foreach (int idx in loadingChunks) evictCandidates.Add(idx);

            for (int i = 0; i < evictCandidates.Count; i++)
            {
                int idx = evictCandidates[i];
                if (idx >= windowStart && idx <= windowEnd) continue;
                if (protectDisplayed && idx == displayedChunkIdx) continue;
                if (idx == referenceSpriteChunkIdx) continue; // Protect reference sprite chunk
                UnloadChunk(idx);
            }

            // Load window
            for (int target = windowStart; target <= windowEnd; target++)
            {
                if (target < 0 || target >= totalChunks) continue;
                if (loadedChunks.Contains(target)) continue;
                if (loadingChunks.Contains(target)) continue;
                LoadChunk(target);
            }
        }

        private void EvictIfNeeded(int justLoaded)
        {
            if (maxChunksInMemory <= 0) return;
            if (!isReady) return; // Don't evict during initial preload

            int center = lastChunkIdx >= 0 ? lastChunkIdx : justLoaded;
            GetRetentionWindow(center, data.chunks.Count, out int windowStart, out int windowEnd);

            evictCandidates.Clear();
            foreach (int idx in loadedChunks) evictCandidates.Add(idx);
            foreach (int idx in loadingChunks) evictCandidates.Add(idx);

            for (int i = 0; i < evictCandidates.Count; i++)
            {
                int idx = evictCandidates[i];
                if (idx >= windowStart && idx <= windowEnd) continue;
                if (idx == displayedChunkIdx && !loadedChunks.Contains(center)) continue;
                if (idx == referenceSpriteChunkIdx) continue; // Protect reference sprite chunk from eviction to prevent flash
                UnloadChunk(idx);
            }
        }

        private void UnloadChunk(int chunkIndex)
        {
            chunkTextures.Remove(chunkIndex);

            if (chunkHandles.TryGetValue(chunkIndex, out var handle))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                chunkHandles.Remove(chunkIndex);
            }

            loadedChunks.Remove(chunkIndex);
            loadingChunks.Remove(chunkIndex);

            if (referenceSpriteChunkIdx == chunkIndex)
                RecreateReferenceSprite();
        }

        private void RecreateReferenceSprite()
        {
            DestroyCellSprites();
            referenceSpriteChunkIdx = -1;

            foreach (var kvp in chunkTextures)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    CreateCellSprites(kvp.Value[0]);
                    referenceSpriteChunkIdx = kvp.Key;

                    // Invalidate cached texture so MPB gets refreshed on next RenderFrame
                    currentTexture = null;
                    break;
                }
            }
        }

        /// <summary>
        /// One sprite per cell, rows counted from the top of the sheet, each drawn
        /// <see cref="SpriteFramesData.FrameWorldWidth"/> wide whatever resolution it was baked at.
        /// </summary>
        private void CreateCellSprites(Texture2D sheet)
        {
            DestroyCellSprites();
            int cells = Mathf.Max(1, data.framesPerSheet);
            int columns = Mathf.Max(1, data.sheetColumns);
            float ppu = data.FramePixelsPerUnit;
            cellSprites = new Sprite[cells];
            for (int c = 0; c < cells; c++)
            {
                int col = c % columns;
                int row = c / columns;
                var rect = new Rect(
                    col * data.frameWidth,
                    sheet.height - (row + 1) * data.frameHeight,
                    data.frameWidth,
                    data.frameHeight);
                cellSprites[c] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            }
        }

        private void DestroyCellSprites()
        {
            if (cellSprites == null)
                return;
            if (spriteRenderer != null)
                spriteRenderer.sprite = null;
            for (int i = 0; i < cellSprites.Length; i++)
            {
                if (cellSprites[i] != null)
                    DestroyImmediate(cellSprites[i]);
            }
            cellSprites = null;
        }
    }
}
