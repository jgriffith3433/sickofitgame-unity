using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject that defines a sprite frame sequence and its chunks for progressive loading
/// </summary>
[CreateAssetMenu(fileName = "SpriteFramesData", menuName = "Video/Sprite Frames Data")]
public class SpriteFramesData : ScriptableObject
{
    [Header("Video Info")]
    [Tooltip("Name of this video (e.g., 'Video3')")]
    public string videoName = "";
    
    [Tooltip("Target playback FPS")]
    public float playbackFPS = 30f;
    
    [Tooltip("If true, video will loop")]
    public bool loop = false;
    
    [Header("Audio")]
    [Tooltip("Addressables address for audio clip (optional)")]
    public string audioAddress = "";
    
    [Header("Chunk Configuration")]
    [Tooltip("List of frame chunks - each chunk has its own addressables label")]
    public List<FrameChunk> chunks = new List<FrameChunk>();
    
    [Header("Info (Read-only)")]
    [SerializeField] private int totalFrames = 0;
    
    /// <summary>
    /// Single chunk of frames
    /// </summary>
    [System.Serializable]
    public class FrameChunk
    {
        [Tooltip("Name of this chunk (e.g., 'Part1')")]
        public string chunkName = "";
        
        [Tooltip("Addressables label for this chunk's frames")]
        public string addressableLabel = "";
        
        [Tooltip("Starting frame index (inclusive)")]
        public int startFrame = 0;
        
        [Tooltip("Ending frame index (inclusive)")]
        public int endFrame = 0;
        
        [Tooltip("Read-only: Number of frames in this chunk")]
        public int frameCount => endFrame - startFrame + 1;
        
        public override string ToString()
        {
            return $"{chunkName}: frames {startFrame}-{endFrame} ({frameCount} frames) - Label: {addressableLabel}";
        }
    }
    
    /// <summary>
    /// Get total number of frames across all chunks
    /// </summary>
    public int GetTotalFrames()
    {
        int total = 0;
        foreach (var chunk in chunks)
        {
            total += chunk.frameCount;
        }
        totalFrames = total;
        return total;
    }
    
    /// <summary>
    /// Get the chunk that contains a specific frame index
    /// </summary>
    public FrameChunk GetChunkForFrame(int frameIndex)
    {
        foreach (var chunk in chunks)
        {
            if (frameIndex >= chunk.startFrame && frameIndex <= chunk.endFrame)
            {
                return chunk;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Get chunk index for a specific frame
    /// </summary>
    public int GetChunkIndexForFrame(int frameIndex)
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            if (frameIndex >= chunks[i].startFrame && frameIndex <= chunks[i].endFrame)
            {
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Validate that chunks are properly configured
    /// </summary>
    public bool Validate(out string error)
    {
        error = "";
        
        if (chunks.Count == 0)
        {
            error = "No chunks defined";
            return false;
        }
        
        // Check for gaps or overlaps
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            if (chunks[i].endFrame + 1 != chunks[i + 1].startFrame)
            {
                error = $"Gap or overlap between {chunks[i].chunkName} and {chunks[i + 1].chunkName}";
                return false;
            }
        }
        
        // Check for empty labels
        foreach (var chunk in chunks)
        {
            if (string.IsNullOrEmpty(chunk.addressableLabel))
            {
                error = $"Chunk {chunk.chunkName} has no addressable label";
                return false;
            }
        }
        
        return true;
    }
}
