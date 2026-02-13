using System;

namespace SickOfIt.Core.Flow
{
    public interface IFrameSequencePlayer
    {
        bool IsLoaded { get; }
        void Play();
        event Action PlaybackCompleted;
    }
}
