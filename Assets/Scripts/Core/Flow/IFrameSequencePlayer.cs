using System;

namespace SickOfIt.Core.Flow
{
    public interface IFrameSequencePlayer
    {
        bool IsLoaded { get; }
        void Play();
        void SkipToEnd();
        void SetSpriteRendererEnabled(bool enabled);
        event Action PlaybackCompleted;
    }
}
