// using UnityEngine;
// using System.Collections.Generic;

// /// <summary>
// /// ADDRESSABLES VERSION - Loads sprite frames via Addressables for WebGL optimization
// /// Sprites are loaded by label (e.g., "Video1_Frames") instead of direct references
// /// </summary>
// [RequireComponent(typeof(SpriteRenderer))]
// public class SpriteFramePlayer : MonoBehaviour
// {
//     [Header("Addressables Settings")]
//     [Tooltip("Label for sprite frames in Addressables (e.g., 'Video1_Frames', 'Character_Walk')")]
//     [SerializeField] private string framesLabel = "";
    
//     [Tooltip("Preload frames immediately on Awake (recommended for smooth playback)")]
//     [SerializeField] private bool preloadOnAwake = true;
    
//     [Header("Playback Settings")]
//     [Tooltip("Target playback FPS")]
//     public float playbackFPS = 30f;
    
//     [Header("Playback Controls")]
//     public bool playOnLoad = true; // Play automatically after frames are loaded
//     public bool loop = true;
    
//     [Header("Debug Info")]
//     [SerializeField] private List<Sprite> frames = new List<Sprite>(); // Loaded frames
//     [SerializeField] private int currentFrameIndex = 0;
//     [SerializeField] private bool isPlaying = false;
//     [SerializeField] private bool isLoaded = false;
    
//     private SpriteRenderer spriteRenderer;
//     private float frameTimer = 0f;
//     private float frameDuration = 0f;

//     private void Awake()
//     {
//         spriteRenderer = GetComponent<SpriteRenderer>();
//         frameDuration = 1f / playbackFPS;
        
//         // Preload frames for smooth playback
//         if (preloadOnAwake && !string.IsNullOrEmpty(framesLabel))
//         {
//             LoadFrames();
//         }
//     }

//     /// <summary>
//     /// Load all sprite frames from Addressables by label
//     /// Call this manually if preloadOnAwake is false
//     /// </summary>
//     public void LoadFrames()
//     {
//         if (string.IsNullOrEmpty(framesLabel))
//         {
//             Debug.LogError("SpriteFramePlayer: framesLabel is not set! Please set it in the Inspector.");
//             return;
//         }
        
//         if (isLoaded)
//         {
//             Debug.Log($"Frames already loaded for label: {framesLabel}");
//             return;
//         }
        
//         Debug.Log($"Loading sprite frames with label: {framesLabel}");
        
//         AddressablesAssetLoader.Instance.LoadAssetsByLabel<Sprite>(framesLabel, (loadedSprites) =>
//         {
//             if (loadedSprites != null && loadedSprites.Count > 0)
//             {
//                 frames.Clear();
//                 frames.AddRange(loadedSprites);
                
//                 // Sort frames by name to ensure correct order (frame_0001, frame_0002, etc.)
//                 frames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                
//                 isLoaded = true;
//                 Debug.Log($"Loaded {frames.Count} frames for: {framesLabel}");
                
//                 // Auto-play if enabled
//                 if (playOnLoad)
//                 {
//                     Play();
//                 }
//             }
//             else
//             {
//                 Debug.LogError($"Failed to load frames with label: {framesLabel}. Make sure sprites are marked as Addressable with this label.");
//             }
//         });
//     }

//     private void Update()
//     {
//         if (!isPlaying || frames.Count == 0)
//             return;

//         frameTimer += Time.deltaTime;

//         if (frameTimer >= frameDuration)
//         {
//             frameTimer -= frameDuration;
            
//             currentFrameIndex++;
            
//             if (currentFrameIndex >= frames.Count)
//             {
//                 if (loop)
//                 {
//                     currentFrameIndex = 0;
//                 }
//                 else
//                 {
//                     currentFrameIndex = frames.Count - 1;
//                     isPlaying = false;
//                     return;
//                 }
//             }

//             UpdateSprite();
//         }
//     }

//     /// <summary>
//     /// Start playing the frame sequence
//     /// </summary>
//     public void Play()
//     {
//         if (!isLoaded)
//         {
//             Debug.LogWarning("Frames not loaded yet. Loading now...");
//             LoadFrames();
//             return;
//         }
        
//         if (frames.Count == 0)
//         {
//             Debug.LogWarning($"No frames loaded for label: {framesLabel}");
//             return;
//         }

//         isPlaying = true;
//         frameTimer = 0f;
//         UpdateSprite();
//     }

//     /// <summary>
//     /// Pause playback
//     /// </summary>
//     public void Pause()
//     {
//         isPlaying = false;
//     }

//     /// <summary>
//     /// Stop playback and reset to first frame
//     /// </summary>
//     public void Stop()
//     {
//         isPlaying = false;
//         currentFrameIndex = 0;
//         frameTimer = 0f;
//         UpdateSprite();
//     }

//     /// <summary>
//     /// Jump to a specific frame
//     /// </summary>
//     public void SetFrame(int frameIndex)
//     {
//         if (frames.Count == 0)
//             return;

//         currentFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Count - 1);
//         UpdateSprite();
//     }

//     /// <summary>
//     /// Set playback speed (FPS)
//     /// </summary>
//     public void SetPlaybackFPS(float fps)
//     {
//         playbackFPS = Mathf.Max(1f, fps);
//         frameDuration = 1f / playbackFPS;
//     }

//     private void UpdateSprite()
//     {
//         if (spriteRenderer != null && currentFrameIndex >= 0 && currentFrameIndex < frames.Count)
//         {
//             spriteRenderer.sprite = frames[currentFrameIndex];
//         }
//     }

//     /// <summary>
//     /// Get total number of loaded frames
//     /// </summary>
//     public int GetTotalFrames()
//     {
//         return frames.Count;
//     }

//     /// <summary>
//     /// Get current frame index
//     /// </summary>
//     public int GetCurrentFrame()
//     {
//         return currentFrameIndex;
//     }

//     /// <summary>
//     /// Check if currently playing
//     /// </summary>
//     public bool IsPlaying()
//     {
//         return isPlaying;
//     }

//     /// <summary>
//     /// Check if frames are loaded
//     /// </summary>
//     public bool IsLoaded()
//     {
//         return isLoaded;
//     }

//     /// <summary>
//     /// Get playback progress (0-1)
//     /// </summary>
//     public float GetProgress()
//     {
//         if (frames.Count == 0) return 0f;
//         return (float)currentFrameIndex / frames.Count;
//     }
// }
