using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using SickOfIt.Core.Addressables;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Mixer behaviour for the SpriteFrame Timeline track.
    ///
    /// Owns ALL chunk loading/unloading for every SpriteFramesData referenced
    /// by clips on this track.  On the first ProcessFrame it auto-discovers
    /// each unique SpriteFramesData from the clip inputs, preloads initial
    /// chunks via Addressables, then serves sprites synchronously each frame.
    ///
    /// At runtime:  manages a sliding window of loaded chunks and drives the
    ///              bound SpriteFrameRenderer.SetSprite() every frame.
    /// In editor:   loads textures synchronously for real-time scrub preview.
    /// </summary>
    public class SpriteFrameMixerBehaviour : PlayableBehaviour
    {
        private const double DefaultFPS = 30.0;

        #region Settings (from SpriteFrameTrack)

        private int _initialChunksToPreload = 3;
        private int _chunksAheadToLoad = 2;
        private int _maxChunksInMemory = 5;

        #endregion

        #region Per-Data Loading State

        private class DataState
        {
            public SpriteFramesData data;
            // Sorted textures per chunk — only the sheet texture reference is needed
            public readonly Dictionary<int, List<Texture2D>> chunkTextures = new Dictionary<int, List<Texture2D>>();
            public readonly Dictionary<int, AsyncOperationHandle<IList<Texture2D>>> chunkHandles
                = new Dictionary<int, AsyncOperationHandle<IList<Texture2D>>>();
            public readonly HashSet<int> loadedChunks = new HashSet<int>();
            public readonly HashSet<int> loadingChunks = new HashSet<int>();
            public int targetPreload;
            public bool isReady;

            // Sheet-based rendering
            public Sprite referenceSprite;    // ONE full-sheet sprite for mesh geometry
            public int referenceSpriteChunkIdx = -1; // Which chunk provided the reference sprite texture
            public Vector2[] cellOffsets;     // Precomputed per cell (0..framesPerSheet-1)
            public bool cellOffsetsReady;     // True once offsets computed from actual texture
        }

        private readonly Dictionary<SpriteFramesData, DataState> _states
            = new Dictionary<SpriteFramesData, DataState>();

        #endregion

        #region Runtime State

        private bool _initialized;
        private bool _allReady;
        private bool _released;
        private SpriteFrameRenderer _boundRenderer;
        private int _lastChunkIdx = -1;
        private int _displayedChunkIdx = -1; // Chunk currently being rendered — never evict
        private readonly List<int> _evictCandidates = new List<int>();

        #endregion

        #region Edit-Mode Preview State

#if UNITY_EDITOR
        private SpriteRenderer _editSR;
        private SpriteFramesData _editData;
        private int _editCachedChunkIdx = -1;
        private List<Sprite> _editSprites;
#endif

        #endregion

        // ----------------------------------------------------------------
        // Public
        // ----------------------------------------------------------------

        public void InitSettings(int initialPreload, int ahead, int maxInMemory)
        {
            _initialChunksToPreload = initialPreload;
            _chunksAheadToLoad = ahead;
            _maxChunksInMemory = maxInMemory;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL has very limited WASM memory (~200MB usable on mobile).
            // WASM linear memory can only grow, never shrink, so the peak
            // allocation during video playback permanently sets the heap floor.
            // With DXT1 textures (~22MB/chunk), 2 chunks ≈ 44MB leaves
            // enough headroom for scene transitions.
            _maxChunksInMemory = Mathf.Min(_maxChunksInMemory, 2);
            _chunksAheadToLoad = Mathf.Min(_chunksAheadToLoad, 1);
            _initialChunksToPreload = Mathf.Min(_initialChunksToPreload, _maxChunksInMemory);
#endif
        }

        // ----------------------------------------------------------------
        // PlayableBehaviour Overrides
        // ----------------------------------------------------------------

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var renderer = playerData as SpriteFrameRenderer;
            if (renderer == null) return;

            // --- One-time initialization on first frame (play mode only) ---
#if UNITY_EDITOR
            if (!_initialized && Application.isPlaying)
#else
            if (!_initialized)
#endif
            {
                Initialize(playable, renderer);
                _initialized = true;
            }

            // --- Find the first active clip ---
            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;

                var input = (ScriptPlayable<SpriteFrameBehaviour>)playable.GetInput(i);
                var behaviour = input.GetBehaviour();
                if (behaviour.spriteFramesData == null) continue;

                int totalFrames = behaviour.spriteFramesData.GetStoredTotalFrames();
                if (totalFrames <= 0)
                    totalFrames = behaviour.spriteFramesData.GetTotalFrames();
                if (totalFrames <= 0) continue;

                // Calculate frame index from clip-local time
                double localTime = input.GetTime();
                double clipDuration = input.GetDuration();
                if (double.IsInfinity(clipDuration) || double.IsNaN(clipDuration) || clipDuration <= 0)
                    clipDuration = totalFrames / DefaultFPS;

                double progress = System.Math.Max(localTime / clipDuration, 0.0);
                if (behaviour.endBehaviour != SpriteFrameEndBehaviour.Loop)
                    progress = System.Math.Min(progress, 1.0 - 1e-10);

                int frameIndex = Mathf.Clamp((int)(progress * totalFrames), 0, totalFrames - 1);

                // --- Edit-mode preview ---
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    ApplyFrameEditMode(renderer, behaviour.spriteFramesData, frameIndex);
                    break;
                }
#endif

                // --- Runtime ---
                renderer.SetSpriteRendererEnabled(true);
                renderer.SetCurrentFrameIndex(frameIndex);
                renderer.SetTotalFrames(totalFrames);

                if (_states.TryGetValue(behaviour.spriteFramesData, out var state))
                {
                    int chunkIdx = behaviour.spriteFramesData.GetChunkIndexForFrame(frameIndex);
                    if (chunkIdx >= 0)
                    {
                        // Only run sliding window when chunk changes
                        if (chunkIdx != _lastChunkIdx)
                        {
                            _lastChunkIdx = chunkIdx;
                            EnsureChunksAround(state, chunkIdx);
                        }

                        if (state.chunkTextures.ContainsKey(chunkIdx))
                        {
                            if (state.referenceSprite != null)
                                renderer.SetReferenceSprite(state.referenceSprite);

                            var chunk = state.data.chunks[chunkIdx];
                            int localIndex = frameIndex - chunk.startFrame;
                            if (localIndex >= 0 && localIndex < chunk.frameCount)
                            {
                                var textures = state.chunkTextures[chunkIdx];
                                int texIdx = localIndex / state.data.framesPerSheet;
                                if (texIdx < textures.Count)
                                {
                                    int cell = localIndex % state.data.framesPerSheet;
                                    var offset = state.cellOffsets[cell];
                                    renderer.SetFrameOnSheet(textures[texIdx], offset.x, offset.y);
                                    _displayedChunkIdx = chunkIdx;
                                }
                            }
                        }
                        // else: chunk not loaded yet — keep showing last rendered
                        // frame (don't clear the renderer). The sliding window
                        // will load it shortly.
                    }
                }

                break; // only process first active clip
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (!_released)
                ReleaseAllChunks();

#if UNITY_EDITOR
            CleanupEditCache();
#endif
        }

        // ----------------------------------------------------------------
        // Initialization
        // ----------------------------------------------------------------

        private void Initialize(Playable playable, SpriteFrameRenderer renderer)
        {
            _boundRenderer = renderer;

            // Discover unique SpriteFramesData from all clip inputs
            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                var input = (ScriptPlayable<SpriteFrameBehaviour>)playable.GetInput(i);
                var behaviour = input.GetBehaviour();
                if (behaviour.spriteFramesData != null &&
                    !_states.ContainsKey(behaviour.spriteFramesData))
                {
                    var state = new DataState { data = behaviour.spriteFramesData };
                    _states[behaviour.spriteFramesData] = state;
                }
            }

            // Preload initial chunks and pre-compute cell offset array for each data asset
            foreach (var kvp in _states)
            {
                var state = kvp.Value;
                var data = state.data;
                int totalChunks = data.chunks.Count;
                state.targetPreload = Mathf.Min(_initialChunksToPreload, totalChunks);

                // Allocate array; actual values computed once first texture is loaded
                state.cellOffsets = new Vector2[data.framesPerSheet];

                for (int i = 0; i < state.targetPreload; i++)
                    LoadChunk(state, i, isInitialPreload: true);
            }

            // If there are no data assets, mark ready immediately
            if (_states.Count == 0)
            {
                _allReady = true;
                renderer.MarkReady(true);
            }

            // Register cleanup so renderer.ReleaseAllResources() cleans our chunks
            renderer.RegisterReleaseAction(ReleaseAllChunks);
        }

        // ----------------------------------------------------------------
        // Chunk Loading (callback-based, no coroutines needed)
        // ----------------------------------------------------------------

        private void LoadChunk(DataState state, int chunkIndex, bool isInitialPreload = false)
        {
            if (_released) return;
            if (chunkIndex < 0 || chunkIndex >= state.data.chunks.Count) return;
            if (state.loadedChunks.Contains(chunkIndex)) return;
            if (state.loadingChunks.Contains(chunkIndex)) return;

            state.loadingChunks.Add(chunkIndex);
            var chunk = state.data.chunks[chunkIndex];

            // Use the out-handle overload so we can track (and cancel) the
            // AsyncOperationHandle while the load is still in flight.
            AddressablesOperationManager.Instance.LoadLabelWithHandle<Texture2D>(
                chunk.addressableLabel,
                (handle, textures) =>
                {
                    OnChunkTexturesLoaded(state, chunkIndex, chunk,
                        handle, textures, isInitialPreload);
                },
                out var immediateHandle
            );

            // Store the handle NOW so ReleaseAllChunks() can release it even
            // if the load hasn't completed yet. OnChunkTexturesLoaded will
            // overwrite this with the same handle on success, which is fine.
            state.chunkHandles[chunkIndex] = immediateHandle;
        }

        private void OnChunkTexturesLoaded(
            DataState state, int chunkIndex,
            SpriteFramesData.FrameChunk chunk,
            AsyncOperationHandle<IList<Texture2D>> handle,
            IList<Texture2D> textures,
            bool isInitialPreload)
        {
            // Guard against post-release callbacks
            if (_released)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            // Guard against chunks that were evicted/cancelled while in flight.
            // UnloadChunk removes the index from loadingChunks and releases the
            // handle. If the callback still fires (race), discard the result.
            if (!state.loadingChunks.Contains(chunkIndex))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            if (textures == null || textures.Count == 0)
            {
                Debug.LogError(
                    "[SpriteFrameMixer] Chunk " + chunkIndex +
                    " load failed (label: " + chunk.addressableLabel + ")");
                state.loadingChunks.Remove(chunkIndex);
                return;
            }

            // Sort textures by name for deterministic ordering
            var sorted = new List<Texture2D>(textures);
            sorted.Sort((a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // Store textures (sheet-based — no per-frame sprite creation)
            state.chunkTextures[chunkIndex] = sorted;
            state.chunkHandles[chunkIndex] = handle;
            state.loadedChunks.Add(chunkIndex);
            state.loadingChunks.Remove(chunkIndex);

            // Evict if over budget — use window-based eviction centered on
            // the latest known playback chunk.  This catches the edge case
            // where the displayed chunk was temporarily protected and an
            // extra chunk squeezed in.
            if (_maxChunksInMemory > 0)
            {
                int center = _lastChunkIdx >= 0 ? _lastChunkIdx : chunkIndex;
                int totalChunks = state.data.chunks.Count;
                int effectiveAhead = _chunksAheadToLoad;
                if (_maxChunksInMemory > 0)
                    effectiveAhead = Mathf.Min(effectiveAhead, _maxChunksInMemory - 1);
                int windowEnd = Mathf.Min(center + effectiveAhead, totalChunks - 1);

                _evictCandidates.Clear();
                foreach (int idx in state.loadedChunks) _evictCandidates.Add(idx);
                foreach (int idx in state.loadingChunks) _evictCandidates.Add(idx);

                for (int i = 0; i < _evictCandidates.Count; i++)
                {
                    int idx = _evictCandidates[i];
                    if (idx >= center && idx <= windowEnd) continue;
                    if (idx == _displayedChunkIdx && !state.loadedChunks.Contains(center)) continue;
                    UnloadChunk(state, idx);
                }
            }

            // Create one full-sheet reference sprite and compute cell offsets (once per DataState)
            if (state.referenceSprite == null && sorted.Count > 0)
            {
                var firstTex = sorted[0];
                state.referenceSprite = Sprite.Create(
                    firstTex,
                    new Rect(0, 0, firstTex.width, firstTex.height),
                    new Vector2(0.5f, 0.5f),
                    state.data.spritePixelsPerUnit);
                state.referenceSpriteChunkIdx = chunkIndex;
            }

            // Compute cell offsets from actual texture dimensions (once)
            if (!state.cellOffsetsReady && sorted.Count > 0)
            {
                var tex = sorted[0];
                var data = state.data;
                float ppu = data.spritePixelsPerUnit;

                // Sprite pivot is at center of actual texture
                float pivotPxX = tex.width * 0.5f;
                float pivotPxY = tex.height * 0.5f;

                for (int c = 0; c < data.framesPerSheet; c++)
                {
                    int col = c % data.sheetColumns;
                    int row = c / data.sheetColumns;

                    // Cell center in Unity pixel coords (origin = bottom-left)
                    // Cells are packed top-left: row 0 is the TOP row of the texture
                    float cellCenterPxX = col * data.frameWidth + data.frameWidth * 0.5f;
                    float cellCenterPxY = tex.height - (row * data.frameHeight + data.frameHeight * 0.5f);

                    // Offset = move GO so that (cellCenter - pivot) reaches screen center
                    // i.e., shift = -(cellCenter - pivot) in world units
                    state.cellOffsets[c] = new Vector2(
                        -(cellCenterPxX - pivotPxX) / ppu,
                        -(cellCenterPxY - pivotPxY) / ppu);
                }
                state.cellOffsetsReady = true;
            }

            // Check if initial preload is complete for this data asset
            if (isInitialPreload && !state.isReady)
            {
                bool allLoaded = true;
                for (int i = 0; i < state.targetPreload; i++)
                {
                    if (!state.loadedChunks.Contains(i))
                    {
                        allLoaded = false;
                        break;
                    }
                }
                if (allLoaded)
                {
                    state.isReady = true;
                    CheckAllReady();
                }
            }
        }

        private void CheckAllReady()
        {
            if (_allReady) return;

            foreach (var kvp in _states)
            {
                if (!kvp.Value.isReady) return;
            }

            _allReady = true;
            if (_boundRenderer != null)
                _boundRenderer.MarkReady(true);
        }

        // ----------------------------------------------------------------
        // Sliding Window
        // ----------------------------------------------------------------

        /// <summary>
        /// Manage the sliding window of loaded chunks around the current
        /// playback position.  Uses a simple window-based approach:
        ///
        ///   wanted = [chunkIndex .. chunkIndex + effectiveAhead]
        ///
        /// Everything outside the window is evicted (loaded OR in-flight).
        /// Everything inside the window is loaded if not already present.
        /// No complex gating — the window size itself enforces the budget.
        /// </summary>
        private void EnsureChunksAround(DataState state, int chunkIndex)
        {
            int totalChunks = state.data.chunks.Count;

            // Window size = effectiveAhead + 1 ≤ _maxChunksInMemory.
            // This guarantees the window never exceeds the memory budget.
            int effectiveAhead = _chunksAheadToLoad;
            if (_maxChunksInMemory > 0)
                effectiveAhead = Mathf.Min(effectiveAhead, _maxChunksInMemory - 1);
            int windowStart = chunkIndex;
            int windowEnd = Mathf.Min(chunkIndex + effectiveAhead, totalChunks - 1);

            // --- Evict everything outside the window (frees memory first) ---
            // Protect _displayedChunkIdx ONLY if the current chunk isn't
            // loaded yet — we need something valid to show this frame.
            bool protectDisplayed = _displayedChunkIdx >= 0
                && _displayedChunkIdx != chunkIndex
                && !state.loadedChunks.Contains(chunkIndex);

            _evictCandidates.Clear();
            foreach (int idx in state.loadedChunks) _evictCandidates.Add(idx);
            foreach (int idx in state.loadingChunks) _evictCandidates.Add(idx);

            for (int i = 0; i < _evictCandidates.Count; i++)
            {
                int idx = _evictCandidates[i];
                if (idx >= windowStart && idx <= windowEnd) continue; // in window — keep
                if (protectDisplayed && idx == _displayedChunkIdx) continue;
                UnloadChunk(state, idx);
            }

            // --- Load anything in the window that isn't loaded or loading ---
            for (int offset = 0; offset <= effectiveAhead; offset++)
            {
                int target = chunkIndex + offset;
                if (target >= totalChunks) break;
                if (state.loadedChunks.Contains(target)) continue;
                if (state.loadingChunks.Contains(target)) continue;
                LoadChunk(state, target);
            }
        }

        private void UnloadChunk(DataState state, int chunkIndex)
        {
            state.chunkTextures.Remove(chunkIndex);

            if (state.chunkHandles.TryGetValue(chunkIndex, out var handle))
            {
                // Releasing an in-flight handle cancels the download and causes
                // the tracking coroutine's IsValid() check to fail, preventing
                // the OnChunkTexturesLoaded callback from firing.
                if (handle.IsValid()) Addressables.Release(handle);
                state.chunkHandles.Remove(chunkIndex);
            }

            state.loadedChunks.Remove(chunkIndex);
            state.loadingChunks.Remove(chunkIndex); // also cancel in-flight loads

            if (state.referenceSpriteChunkIdx == chunkIndex)
                RecreateReferenceSprite(state);
        }

        /// <summary>
        /// Recreate the reference sprite from any currently loaded chunk.
        /// Called when the chunk that originally provided the sprite's texture is evicted.
        /// </summary>
        private void RecreateReferenceSprite(DataState state)
        {
            if (state.referenceSprite != null)
            {
                Object.DestroyImmediate(state.referenceSprite);
                state.referenceSprite = null;
            }
            state.referenceSpriteChunkIdx = -1;

            // Find any loaded chunk to create a new reference sprite from
            foreach (var kvp in state.chunkTextures)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    var tex = kvp.Value[0];
                    state.referenceSprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        state.data.spritePixelsPerUnit);
                    state.referenceSpriteChunkIdx = kvp.Key;

                    if (_boundRenderer != null)
                        _boundRenderer.SetReferenceSprite(state.referenceSprite);
                    break;
                }
            }
        }

        // ----------------------------------------------------------------
        // Cleanup
        // ----------------------------------------------------------------

        private void ReleaseAllChunks()
        {
            if (_released) return;
            _released = true;
            _allReady = false;

            foreach (var kvp in _states)
            {
                var state = kvp.Value;

                state.chunkTextures.Clear();

                if (state.referenceSprite != null)
                {
                    Object.DestroyImmediate(state.referenceSprite);
                    state.referenceSprite = null;
                }

                foreach (var handleKvp in state.chunkHandles)
                {
                    if (handleKvp.Value.IsValid())
                        Addressables.Release(handleKvp.Value);
                }
                state.chunkHandles.Clear();

                state.loadedChunks.Clear();
                state.loadingChunks.Clear();
                state.isReady = false;
            }

            _states.Clear();

            if (_boundRenderer != null)
                _boundRenderer.MarkReady(false);
        }

        // ----------------------------------------------------------------
        // Edit-Mode Preview
        // ----------------------------------------------------------------

#if UNITY_EDITOR
        private void ApplyFrameEditMode(
            SpriteFrameRenderer renderer, SpriteFramesData data, int frameIndex)
        {
            if (_editSR == null || _editData != data)
            {
                _editSR = renderer.GetComponent<SpriteRenderer>();
                if (_editSR == null) return;
            }

            int chunkIdx = data.GetChunkIndexForFrame(frameIndex);
            if (chunkIdx < 0 || chunkIdx >= data.chunks.Count) return;

            if (_editData != data || _editCachedChunkIdx != chunkIdx || _editSprites == null)
            {
                CleanupEditCache();
                _editData = data;
                _editSprites = LoadChunkSpritesSync(data, data.chunks[chunkIdx]);
                _editCachedChunkIdx = chunkIdx;
            }

            if (_editSprites == null || _editSprites.Count == 0) return;

            var editChunk = data.chunks[chunkIdx];
            int localIndex = Mathf.Clamp(
                frameIndex - editChunk.startFrame, 0, _editSprites.Count - 1);
            _editSR.sprite = _editSprites[localIndex];
        }

        private List<Sprite> LoadChunkSpritesSync(
            SpriteFramesData data, SpriteFramesData.FrameChunk chunk)
        {
            try
            {
                // Use editor-only direct texture references (no Addressables needed)
                if (chunk.editorTextures == null || chunk.editorTextures.Count == 0)
                {
                    Debug.LogWarning("[SpriteFrameMixer] Edit preview: no editorTextures on chunk '"
                        + chunk.chunkName + "'. Re-run the packer to populate them.");
                    return null;
                }

                var sprites = new List<Sprite>();
                int cols = data.sheetColumns;
                int fw = data.frameWidth;
                int fh = data.frameHeight;
                float ppu = data.spritePixelsPerUnit;
                int created = 0;

                foreach (var tex in chunk.editorTextures)
                {
                    if (tex == null) continue;
                    int texH = tex.height;
                    for (int cell = 0; cell < data.framesPerSheet && created < chunk.frameCount; cell++)
                    {
                        int col = cell % cols;
                        int row = cell / cols;
                        float x = col * fw;
                        float y = texH - (row + 1) * fh;

                        if (x + fw > tex.width || y < 0 || y + fh > texH) break;

                        var sprite = Sprite.Create(tex, new Rect(x, y, fw, fh),
                            new Vector2(0.5f, 0.5f), ppu);
                        sprite.name = "edit_preview_" + (chunk.startFrame + created).ToString("D5");
                        sprites.Add(sprite);
                        created++;
                    }
                }

                return sprites;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SpriteFrameMixer] Edit preview failed: " + e.Message);
                return null;
            }
        }

        private void CleanupEditCache()
        {
            if (_editSprites != null)
            {
                foreach (var s in _editSprites)
                    if (s != null) Object.DestroyImmediate(s);
                _editSprites = null;
            }

            _editCachedChunkIdx = -1;
        }
#endif
    }
}
