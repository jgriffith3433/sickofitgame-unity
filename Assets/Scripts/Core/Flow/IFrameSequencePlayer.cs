using System;

namespace SickOfIt.Core.Flow
{
    public interface IFrameSequencePlayer
    {
        bool IsLoaded { get; }
        void Play();
        void SkipToEnd();
        void SetSpriteRendererEnabled(bool enabled);
        /// <summary>
        /// Immediately release all loaded resources (textures, sprites, audio).
        /// Must be called BEFORE scene unload so textures can be freed by Resources.UnloadUnusedAssets().
        /// Uses DestroyImmediate for sprites to ensure textures lose their references immediately.
        /// </summary>
        void ReleaseAllResources();
        event Action PlaybackCompleted;
    }
}
