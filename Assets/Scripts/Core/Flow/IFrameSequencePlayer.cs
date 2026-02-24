namespace SickOfIt.Core.Flow
{
    /// <summary>
    /// Interface for chunked sprite frame renderers driven by Unity Timeline.
    /// Provides loading state, visibility control, and resource cleanup.
    /// Playback is controlled by PlayableDirector on the same GameObject.
    /// </summary>
    public interface IFrameSequencePlayer
    {
        bool IsLoaded { get; }
        void SetSpriteRendererEnabled(bool enabled);
        /// <summary>
        /// Immediately release all loaded resources (textures, sprites).
        /// Must be called BEFORE scene unload so textures can be freed by Resources.UnloadUnusedAssets().
        /// Uses DestroyImmediate for sprites to ensure textures lose their references immediately.
        /// </summary>
        void ReleaseAllResources();
    }
}
