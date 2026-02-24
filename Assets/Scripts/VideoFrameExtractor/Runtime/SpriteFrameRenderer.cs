using UnityEngine;
using SickOfIt.Core.Flow;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Thin display component for sprite frame sequences.
    /// Driven by Unity Timeline  the SpriteFrameMixerBehaviour calls
    /// SetSprite() each frame, supplying sprites from its own chunk management.
    ///
    /// Place this on the GameObject with the SpriteRenderer.
    /// Bind it as the track target on SpriteFrameTrack.
    /// No manual configuration needed  the track/mixer handle everything.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameRenderer : MonoBehaviour, IFrameSequencePlayer
    {
        [Header("Debug (Read-Only)")]
        [SerializeField] private int currentFrameIndex;
        [SerializeField] private int totalFrames;

        private SpriteRenderer spriteRenderer;
        private bool isReady;
        private System.Action releaseAction;

        // Sheet-based rendering (zero per-frame allocation)
        private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        private MaterialPropertyBlock _mpb;
        private Vector3 _baseLocalPosition;
        private Texture2D _currentTexture;

        #region Public Properties

        /// <summary>True when the Timeline mixer has preloaded initial chunks.</summary>
        public bool IsLoaded => isReady;

        /// <summary>Total frame count (set by the mixer from clip data).</summary>
        public int TotalFrames => totalFrames;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            _baseLocalPosition = transform.localPosition;
            _mpb = new MaterialPropertyBlock();
        }

        #endregion

        #region IFrameSequencePlayer

        public void SetSpriteRendererEnabled(bool enabled)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = enabled;
        }

        public void ReleaseAllResources()
        {
            releaseAction?.Invoke();
            releaseAction = null;
            isReady = false;
            totalFrames = 0;
            currentFrameIndex = 0;
            _currentTexture = null;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
                spriteRenderer.SetPropertyBlock(null);
            }
            transform.localPosition = _baseLocalPosition;
        }

        #endregion

        #region Called by Timeline Mixer

        /// <summary>
        /// Set the reference sprite (full sheet) that defines the mesh geometry.
        /// Called once per data asset when first chunk loads.
        /// </summary>
        public void SetReferenceSprite(Sprite sheetSprite)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != sheetSprite)
                spriteRenderer.sprite = sheetSprite;
        }

        /// <summary>
        /// Show a frame by swapping the sheet texture via MaterialPropertyBlock
        /// and offsetting the GO position so the correct cell is centered on camera.
        /// Zero allocation per frame.
        /// </summary>
        public void SetFrameOnSheet(Texture2D texture, float offsetX, float offsetY)
        {
            if (spriteRenderer == null) return;

            if (_currentTexture != texture)
            {
                _currentTexture = texture;
                _mpb.SetTexture(MainTexID, texture);
                spriteRenderer.SetPropertyBlock(_mpb);
            }

            // Offsets are in unscaled world units; multiply by local scale
            // since the sprite mesh is scaled up with the transform.
            var s = transform.localScale;
            transform.localPosition = new Vector3(
                _baseLocalPosition.x + offsetX * s.x,
                _baseLocalPosition.y + offsetY * s.y,
                _baseLocalPosition.z);
        }

        /// <summary>Assign a sprite directly (used for edit-mode preview).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (sprite != null && spriteRenderer != null)
                spriteRenderer.sprite = sprite;
        }

        /// <summary>Update debug frame index display.</summary>
        public void SetCurrentFrameIndex(int frame) => currentFrameIndex = frame;

        /// <summary>Set total frame count for progress queries.</summary>
        public void SetTotalFrames(int frames) => totalFrames = frames;

        /// <summary>Mark this renderer as ready (initial chunks loaded by mixer).</summary>
        public void MarkReady(bool ready) => isReady = ready;

        /// <summary>
        /// Register a cleanup action called by ReleaseAllResources().
        /// The mixer registers its chunk cleanup here.
        /// </summary>
        public void RegisterReleaseAction(System.Action action) => releaseAction = action;

        #endregion

        #region Queries

        public int GetTotalFrames() => totalFrames;
        public int GetCurrentFrame() => currentFrameIndex;

        public float GetProgress()
        {
            return totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0f;
        }

        #endregion
    }
}
