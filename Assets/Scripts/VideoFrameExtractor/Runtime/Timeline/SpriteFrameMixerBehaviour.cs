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
                if (!behaviour.loop)
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
                                }
                            }
                        }
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

            AddressablesOperationManager.Instance.LoadLabelWithHandle<Texture2D>(
                chunk.addressableLabel,
                (handle, textures) =>
                {
                    OnChunkTexturesLoaded(state, chunkIndex, chunk,
                        handle, textures, isInitialPreload);
                }
            );
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

            // Create one full-sheet reference sprite and compute cell offsets (once per DataState)
            if (state.referenceSprite == null && sorted.Count > 0)
            {
                var firstTex = sorted[0];
                state.referenceSprite = Sprite.Create(
                    firstTex,
                    new Rect(0, 0, firstTex.width, firstTex.height),
                    new Vector2(0.5f, 0.5f),
                    state.data.spritePixelsPerUnit);
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

        private void EnsureChunksAround(DataState state, int chunkIndex)
        {
            int totalChunks = state.data.chunks.Count;

            for (int offset = 0; offset <= _chunksAheadToLoad; offset++)
            {
                int target = chunkIndex + offset;
                if (target < totalChunks &&
                    !state.loadedChunks.Contains(target) &&
                    !state.loadingChunks.Contains(target))
                {
                    LoadChunk(state, target);
                }
            }

            if (_maxChunksInMemory > 0 && state.loadedChunks.Count > _maxChunksInMemory)
                EvictDistantChunks(state, chunkIndex);
        }

        private void EvictDistantChunks(DataState state, int currentChunk)
        {
            _evictCandidates.Clear();
            _evictCandidates.AddRange(state.loadedChunks);
            _evictCandidates.Sort((a, b) =>
                Mathf.Abs(a - currentChunk).CompareTo(Mathf.Abs(b - currentChunk)));

            for (int i = _maxChunksInMemory; i < _evictCandidates.Count; i++)
                UnloadChunk(state, _evictCandidates[i]);
        }

        private void UnloadChunk(DataState state, int chunkIndex)
        {
            state.chunkTextures.Remove(chunkIndex);

            if (state.chunkHandles.TryGetValue(chunkIndex, out var handle))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                state.chunkHandles.Remove(chunkIndex);
            }

            state.loadedChunks.Remove(chunkIndex);
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
