using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFramePlayer : MonoBehaviour
{
    [Header("Frames (Loaded from Inspector)")]
    [Tooltip("All video frames as sprites - use 'Load Frames' button in Inspector")]
    public List<Sprite> frames = new List<Sprite>();
    
    [Header("Playback Settings")]
    [Tooltip("Target playback FPS")]
    public float playbackFPS = 30f;
    
    [Header("Playback Controls")]
    public bool playOnAwake = true;
    public bool loop = true;
    
    [Header("Load Settings (Editor Only)")]
    [Tooltip("Folder containing the extracted video frames (e.g., Assets/Videos/MyVideo_frames)")]
    public string framesFolder = "";
    
    [Header("Debug Info")]
    [SerializeField] private int currentFrameIndex = 0;
    [SerializeField] private bool isPlaying = false;
    
    private SpriteRenderer spriteRenderer;
    private float frameTimer = 0f;
    private float frameDuration = 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        frameDuration = 1f / playbackFPS;
    }

    private void Start()
    {
        if (playOnAwake && frames.Count > 0)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!isPlaying || frames.Count == 0)
            return;

        frameTimer += Time.deltaTime;

        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            
            currentFrameIndex++;
            
            if (currentFrameIndex >= frames.Count)
            {
                if (loop)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    currentFrameIndex = frames.Count - 1;
                    isPlaying = false;
                    return;
                }
            }

            UpdateSprite();
        }
    }

    /// <summary>
    /// Start playing the frame sequence
    /// </summary>
    public void Play()
    {
        if (frames.Count == 0)
        {
            Debug.LogWarning("No frames loaded. Use 'Load Frames' button in Inspector first.");
            return;
        }

        isPlaying = true;
        frameTimer = 0f;
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Stop playback and reset to first frame
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        currentFrameIndex = 0;
        frameTimer = 0f;
        UpdateSprite();
    }

    /// <summary>
    /// Jump to a specific frame
    /// </summary>
    public void SetFrame(int frameIndex)
    {
        if (frames.Count == 0)
            return;

        currentFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Count - 1);
        UpdateSprite();
    }

    /// <summary>
    /// Set playback speed (FPS)
    /// </summary>
    public void SetPlaybackFPS(float fps)
    {
        playbackFPS = Mathf.Max(1f, fps);
        frameDuration = 1f / playbackFPS;
    }

    private void UpdateSprite()
    {
        if (spriteRenderer != null && currentFrameIndex < frames.Count)
        {
            spriteRenderer.sprite = frames[currentFrameIndex];
        }
    }

    /// <summary>
    /// Get total number of loaded frames
    /// </summary>
    public int GetTotalFrames()
    {
        return frames.Count;
    }

    /// <summary>
    /// Get current frame index
    /// </summary>
    public int GetCurrentFrame()
    {
        return currentFrameIndex;
    }

    /// <summary>
    /// Check if currently playing
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying;
    }

    /// <summary>
    /// Get playback progress (0-1)
    /// </summary>
    public float GetProgress()
    {
        if (frames.Count == 0) return 0f;
        return (float)currentFrameIndex / frames.Count;
    }
}
