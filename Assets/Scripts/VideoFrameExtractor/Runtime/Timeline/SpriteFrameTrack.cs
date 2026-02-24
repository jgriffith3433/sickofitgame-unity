using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Custom Timeline track for playing sprite frame sequences.
    ///
    /// Bind a SpriteFrameRenderer component to this track.
    /// Add SpriteFrameClip clips referencing SpriteFramesData assets.
    ///
    /// The track's mixer automatically discovers all SpriteFramesData
    /// from clips, manages chunk loading/unloading, and drives the
    /// bound renderer.  No separate chunk manager component needed.
    ///
    /// Chunk loading settings are configured here on the track.
    /// </summary>
    [TrackClipType(typeof(SpriteFrameClip))]
    [TrackBindingType(typeof(SpriteFrameRenderer))]
    [TrackColor(0.2f, 0.6f, 0.9f)]
    public class SpriteFrameTrack : TrackAsset
    {
        [Header("Chunk Loading")]
        [Tooltip("How many chunks to preload per data asset before marking as ready")]
        [SerializeField] private int initialChunksToPreload = 3;

        [Tooltip("How many chunks ahead of current playback to keep loaded")]
        [SerializeField] private int chunksAheadToLoad = 2;

        [Tooltip("Maximum chunks per data asset to keep in memory at once. 0 = unlimited.")]
        [SerializeField] private int maxChunksInMemory = 5;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var playable = ScriptPlayable<SpriteFrameMixerBehaviour>.Create(graph, inputCount);
            var mixer = playable.GetBehaviour();
            mixer.InitSettings(initialChunksToPreload, chunksAheadToLoad, maxChunksInMemory);
            return playable;
        }
    }
}
