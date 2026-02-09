using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ADDRESSABLES VERSION - Loads sprite frames via Addressables for WebGL optimization
/// Sprites are loaded by label (e.g., "Video1_Frames") instead of direct references
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFramePlayer : MonoBehaviour
{
    [Header("Addressables Settings")]
    [Tooltip("Label for sprite frames in Addressables (e.g., 'Video1_Frames', 'Character_Walk')")]
    [SerializeField] private string framesLabel = "";
    
    [Tooltip("Optional: Label for audio clip in Addressables (e.g., 'Video1_Audio'). Leave empty for no audio.")]
    [SerializeField] private string audioLabel = "";
    
    [Tooltip("Preload frames immediately on Awake (recommended for smooth playback)")]
    [SerializeField] private bool preloadOnAwake = true;
    
    [Header("Playback Settings")]
    [Tooltip("Target playback FPS")]
    public float playbackFPS = 30f;
    
    [Header("Playback Controls")]
    public bool playOnLoad = true; // Play automatically after frames are loaded
    public bool loop = true;
    
    [Header("Level Auto-Play (Video System)")]
    [Tooltip("If true, automatically loads the level after video finishes (non-looping only)")]
    [SerializeField] private bool autoPlayLevelAfterVideo = false;
    
    [Tooltip("If true, returns to the level that triggered this video (uses PlayerPrefs 'ReturnToLevel')")]
    [SerializeField] private bool returnToLevelAfterVideo = false;
    
    [Header("Audio Settings")]
    [Tooltip("Volume for audio playback (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;
    
    [Header("Debug Info")]
    [SerializeField] private List<Sprite> frames = new List<Sprite>(); // Loaded frames
    [SerializeField] private int currentFrameIndex = 0;
    [SerializeField] private bool isPlaying = false;
    [SerializeField] private bool isLoaded = false;
    [SerializeField] private bool isAudioLoaded = false;
    
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private AudioClip audioClip;
    private float frameTimer = 0f;
    private float frameDuration = 0f;
    private bool hasAudio = false;
    private bool framesLoaded = false;
    private bool audioLoaded = false;
    private bool hasFinished = false;

    /// <summary>
    /// Public property to check if all assets are loaded
    /// </summary>
    public bool IsLoaded => isLoaded;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        frameDuration = 1f / playbackFPS;
        
        // Setup AudioSource if audio is needed
        hasAudio = !string.IsNullOrEmpty(audioLabel);
        if (hasAudio)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            audioSource.volume = audioVolume;
        }
        
        // Preload frames and audio for smooth playback
        if (preloadOnAwake && !string.IsNullOrEmpty(framesLabel))
        {
            LoadFrames();
            if (hasAudio)
            {
                LoadAudio();
            }
        }
    }

    /// <summary>
    /// Load all sprite frames from Addressables by label
    /// Call this manually if preloadOnAwake is false
    /// </summary>
    public void LoadFrames()
    {
        if (string.IsNullOrEmpty(framesLabel))
        {
            Debug.LogError("SpriteFramePlayer: framesLabel is not set! Please set it in the Inspector.");
            return;
        }
        
        if (framesLoaded)
        {
            Debug.Log($"Frames already loaded for label: {framesLabel}");
            return;
        }
        
        Debug.Log($"Loading sprite frames with label: {framesLabel}");
        
        AddressablesAssetLoader.Instance.LoadAssetsByLabel<Sprite>(framesLabel, (loadedSprites) =>
        {
            if (loadedSprites != null && loadedSprites.Count > 0)
            {
                frames.Clear();
                frames.AddRange(loadedSprites);
                
                // Sort frames by name to ensure correct order (frame_0001, frame_0002, etc.)
                frames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                
                framesLoaded = true;
                CheckIfFullyLoaded();
                Debug.Log($"Loaded {frames.Count} frames for: {framesLabel}");
            }
            else
            {
                Debug.LogError($"Failed to load frames with label: {framesLabel}. Make sure sprites are marked as Addressable with this label.");
            }
        });
    }
    
    /// <summary>
    /// Load audio clip from Addressables by label
    /// Call this manually if preloadOnAwake is false
    /// </summary>
    public void LoadAudio()
    {
        if (string.IsNullOrEmpty(audioLabel))
        {
            Debug.LogWarning("SpriteFramePlayer: audioLabel is not set. Skipping audio load.");
            audioLoaded = true; // Mark as loaded so it doesn't block playback
            return;
        }
        
        if (audioLoaded)
        {
            Debug.Log($"Audio already loaded for label: {audioLabel}");
            return;
        }
        
        Debug.Log($"Loading audio with label: {audioLabel}");
        
        AddressablesAssetLoader.Instance.LoadAssetsByLabel<AudioClip>(audioLabel, (clips) =>
        {
            if (clips != null && clips.Count > 0)
            {
                audioClip = clips[0]; // Take first audio clip
                if (audioSource != null)
                {
                    audioSource.clip = audioClip;
                }
                
                audioLoaded = true;
                isAudioLoaded = true;
                CheckIfFullyLoaded();
                Debug.Log($"Loaded audio clip: {audioClip.name}");
            }
            else
            {
                Debug.LogError($"Failed to load audio with label: {audioLabel}. Make sure audio clip is marked as Addressable with this label.");
                audioLoaded = true; // Mark as loaded so it doesn't block playback
            }
        });
    }
    
    /// <summary>
    /// Check if both frames and audio (if needed) are loaded, then auto-play if enabled
    /// </summary>
    private void CheckIfFullyLoaded()
    {
        bool allLoaded = framesLoaded && (!hasAudio || audioLoaded);
        
        if (allLoaded && !isLoaded)
        {
            isLoaded = true;
            Debug.Log("SpriteFramePlayer: All assets loaded and ready!");
            
            // Auto-play if enabled
            if (playOnLoad)
            {
                Play();
            }
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
                    OnVideoFinished();
                    return;
                }
            }

            UpdateSprite();
        }
    }

    /// <summary>
    /// Start playing the frame sequence (and audio if available)
    /// </summary>
    public void Play()
    {
        if (!isLoaded)
        {
            Debug.LogWarning("Assets not loaded yet. Loading now...");
            LoadFrames();
            if (hasAudio)
            {
                LoadAudio();
            }
            return;
        }
        
        if (frames.Count == 0)
        {
            Debug.LogWarning($"No frames loaded for label: {framesLabel}");
            return;
        }
        
        // Start audio playback synchronized with video
        if (hasAudio && audioSource != null && audioClip != null)
        {
            audioSource.time = 0f;
            audioSource.Play();
        }

        isPlaying = true;
        frameTimer = 0f;
        currentFrameIndex = 0;
        UpdateSprite();
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
        
        // Pause audio as well
        if (hasAudio && audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
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
        
        // Stop audio as well
        if (hasAudio && audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
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
        if (spriteRenderer != null && currentFrameIndex >= 0 && currentFrameIndex < frames.Count)
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
    
    /// <summary>
    /// Resume playback (for use after Pause)
    /// </summary>
    public void Resume()
    {
        if (!isLoaded || frames.Count == 0)
        {
            Debug.LogWarning("Cannot resume - no frames loaded");
            return;
        }
        
        isPlaying = true;
        
        // Resume audio as well
        if (hasAudio && audioSource != null && !audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }
    
    /// <summary>
    /// Set audio volume (0-1)
    /// </summary>
    public void SetVolume(float volume)
    {
        audioVolume = Mathf.Clamp01(volume);
        if (hasAudio && audioSource != null)
        {
            audioSource.volume = audioVolume;
        }
    }
    
    /// <summary>
    /// Check if audio is loaded
    /// </summary>
    public bool IsAudioLoaded()
    {
        return isAudioLoaded;
    }
    
    /// <summary>
    /// Get the loaded audio clip
    /// </summary>
    public AudioClip GetAudioClip()
    {
        return audioClip;
    }
    
    /// <summary>
    /// Called when video playback finishes (non-looping only)
    /// </summary>
    private void OnVideoFinished()
    {
        if (!hasFinished && autoPlayLevelAfterVideo)
        {
            hasFinished = true;
            Debug.Log("SpriteFramePlayer: Video finished, loading level...");
            ReturnToGame();
        }
    }
    
    /// <summary>
    /// Returns to the game level or main menu after video finishes
    /// </summary>
    private void ReturnToGame()
    {
        if (returnToLevelAfterVideo && PlayerPrefs.HasKey("ReturnToLevel"))
        {
            int levelToReturn = PlayerPrefs.GetInt("ReturnToLevel");
            PlayerPrefs.DeleteKey("ReturnToLevel");
            PlayerPrefs.Save();
            SceneTransitionManager.Instance.LoadLevel(levelToReturn);
        }
        else
        {
            // Return to main menu or level selection
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }
    
    /// <summary>
    /// Manually trigger level load (can be called by a skip button)
    /// </summary>
    public void SkipToLevel()
    {
        if (!hasFinished)
        {
            hasFinished = true;
            Stop();
            ReturnToGame();
        }
    }
}
