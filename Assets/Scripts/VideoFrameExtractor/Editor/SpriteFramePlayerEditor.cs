#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(SpriteFramePlayer))]
public class SpriteFramePlayerEditor : Editor
{
    private SpriteFramePlayer player;

    private void OnEnable()
    {
        player = (SpriteFramePlayer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Frame Management", EditorStyles.boldLabel);

        // Show frame count
        EditorGUILayout.HelpBox($"Frames Loaded: {player.frames.Count}", 
            player.frames.Count > 0 ? MessageType.Info : MessageType.Warning);

        if (GUILayout.Button("Load Frames from Folder", GUILayout.Height(30)))
        {
            LoadFramesFromFolder();
        }
        
        if (player.frames.Count > 0)
        {
            if (GUILayout.Button("Clear All Frames"))
            {
                if (EditorUtility.DisplayDialog("Clear Frames", 
                    $"Remove all {player.frames.Count} frames from the list?", "Yes", "Cancel"))
                {
                    player.frames.Clear();
                    EditorUtility.SetDirty(player);
                }
            }
        }

        if (Application.isPlaying)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Playback Controls (Runtime)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Play"))
            {
                player.Play();
            }

            if (GUILayout.Button("Pause"))
            {
                player.Pause();
            }

            if (GUILayout.Button("Stop"))
            {
                player.Stop();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Frame scrubber
            int totalFrames = player.GetTotalFrames();
            if (totalFrames > 0)
            {
                int currentFrame = player.GetCurrentFrame();
                int newFrame = EditorGUILayout.IntSlider("Scrub Frame", currentFrame, 0, totalFrames - 1);
                
                if (newFrame != currentFrame)
                {
                    player.SetFrame(newFrame);
                }

                EditorGUILayout.LabelField($"Progress: {player.GetProgress() * 100:F1}%");
            }
        }

        GUILayout.Space(10);

        // Help box
        EditorGUILayout.HelpBox(
            "1. Extract frames using Tools → Video Frame Extractor\n" +
            "2. Set 'Frames Folder' to the extracted frames path\n" +
            "3. Click 'Load Frames from Folder' button\n" +
            "4. Frames will be saved in the Inspector (build-safe!)\n" +
            "5. Adjust 'Playback FPS' for desired speed\n" +
            "6. Enable 'Play On Awake' to auto-play on start",
            MessageType.Info
        );
    }

    private void LoadFramesFromFolder()
    {
        player.frames.Clear();
        
        if (string.IsNullOrEmpty(player.framesFolder))
        {
            EditorUtility.DisplayDialog("Error", "Frames folder path is empty!", "OK");
            return;
        }

        // Load directly from assets
        if (!Directory.Exists(player.framesFolder))
        {
            EditorUtility.DisplayDialog("Error", $"Folder does not exist: {player.framesFolder}", "OK");
            return;
        }

        string[] frameFiles = Directory.GetFiles(player.framesFolder, "frame_*.png");
        
        if (frameFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", $"No frames found in folder: {player.framesFolder}", "OK");
            return;
        }

        System.Array.Sort(frameFiles);

        EditorUtility.DisplayProgressBar("Loading Frames", "Loading texture frames...", 0f);

        for (int i = 0; i < frameFiles.Length; i++)
        {
            string filePath = frameFiles[i];
            string assetPath = filePath.Replace("\\", "/");
            
            if (assetPath.StartsWith(Application.dataPath))
            {
                assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                // Try to load as sprite first (if texture is imported as sprite)
                Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                
                if (existingSprite != null)
                {
                    // Use the existing sprite from the texture import settings
                    player.frames.Add(existingSprite);
                }
                else
                {
                    // Texture isn't set up as sprite, set it up properly
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = 100f;
                        importer.SaveAndReimport();
                        
                        // Now load the sprite
                        existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        if (existingSprite != null)
                        {
                            player.frames.Add(existingSprite);
                        }
                    }
                }
            }

            if (i % 10 == 0)
            {
                EditorUtility.DisplayProgressBar("Loading Frames", 
                    $"Loading frame {i + 1}/{frameFiles.Length}...", 
                    (float)i / frameFiles.Length);
            }
        }

        EditorUtility.ClearProgressBar();
        
        if (player.frames.Count > 0)
        {
            Debug.Log($"✓ Loaded {player.frames.Count} frames into Inspector! Frames are now saved and build-safe.");
            EditorUtility.SetDirty(player);
        }
        else
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load frames from: {player.framesFolder}", "OK");
        }
    }
}
#endif
