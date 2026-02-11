using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor window for managing SpriteFramesData and automatically chunking frames
/// </summary>
public class SpriteFramesEditorWindow : EditorWindow
{
    private SpriteFramesData spriteFramesData;
    private Vector2 scrollPosition;
    private int framesPerChunk = 250;
    
    [MenuItem("Tools/Sprite Frames Manager")]
    public static void ShowWindow()
    {
        GetWindow<SpriteFramesEditorWindow>("Sprite Frames Manager");
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Sprite Frames Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Step 1: Select or create SpriteFramesData
        EditorGUILayout.LabelField("Step 1: Load SpriteFramesData", EditorStyles.boldLabel);
        spriteFramesData = (SpriteFramesData)EditorGUILayout.ObjectField(
            "Sprite Frames Data", 
            spriteFramesData, 
            typeof(SpriteFramesData), 
            false
        );
        
        if (GUILayout.Button("Create New SpriteFramesData"))
        {
            CreateNewSpriteFramesData();
        }
        
        EditorGUILayout.Space();
        
        if (spriteFramesData == null)
        {
            EditorGUILayout.HelpBox("Please load or create a SpriteFramesData asset to continue.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }
        
        // Step 2: Auto-chunk selected frames
        EditorGUILayout.LabelField("Step 2: Auto-Chunk Frames", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select sprite frames in the Project window, then click 'Auto-Chunk Selected Frames' to automatically create chunks.\n\n" +
            $"Labels will use the asset name as prefix: '{spriteFramesData.name}_Part1', '{spriteFramesData.name}_Part2', etc.",
            MessageType.Info
        );
        
        framesPerChunk = EditorGUILayout.IntField("Frames Per Chunk", framesPerChunk);
        
        if (GUILayout.Button("Auto-Chunk Selected Frames", GUILayout.Height(30)))
        {
            AutoChunkSelectedFrames();
        }
        
        EditorGUILayout.Space();
        
        // Step 3: Display and edit chunks
        EditorGUILayout.LabelField("Step 3: Review Chunks", EditorStyles.boldLabel);
        
        if (spriteFramesData.chunks.Count == 0)
        {
            EditorGUILayout.HelpBox("No chunks defined yet. Use 'Auto-Chunk Selected Frames' to generate chunks.", MessageType.Warning);
        }
        else
        {
            DisplayChunks();
        }
        
        EditorGUILayout.Space();
        
        // Step 4: Assign addressable labels
        EditorGUILayout.LabelField("Step 4: Assign Addressable Labels", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This will automatically assign addressable labels to all frames based on chunk configuration.",
            MessageType.Info
        );
        
        if (GUILayout.Button("Apply Addressable Labels to Frames", GUILayout.Height(30)))
        {
            ApplyAddressableLabels();
        }
        
        EditorGUILayout.Space();
        
        // Validation
        string validationError;
        if (!spriteFramesData.Validate(out validationError))
        {
            EditorGUILayout.HelpBox($"Validation Error: {validationError}", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("✓ Configuration is valid!", MessageType.Info);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void CreateNewSpriteFramesData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create SpriteFramesData",
            "NewSpriteFramesData",
            "asset",
            "Choose where to save the SpriteFramesData asset"
        );
        
        if (!string.IsNullOrEmpty(path))
        {
            var newData = CreateInstance<SpriteFramesData>();
            AssetDatabase.CreateAsset(newData, path);
            AssetDatabase.SaveAssets();
            spriteFramesData = newData;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newData;
        }
    }
    
    private void AutoChunkSelectedFrames()
    {
        // Get selected sprites
        var selectedObjects = Selection.objects;
        var sprites = new List<Sprite>();
        
        foreach (var obj in selectedObjects)
        {
            if (obj is Sprite sprite)
            {
                sprites.Add(sprite);
            }
            else if (obj is Texture2D)
            {
                // Try to load sprite from texture
                string path = AssetDatabase.GetAssetPath(obj);
                var loadedSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                sprites.AddRange(loadedSprites);
            }
        }
        
        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No sprites selected. Please select sprite frames in the Project window.", "OK");
            return;
        }
        
        // Sort sprites by name
        sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        
        // Clear existing chunks
        spriteFramesData.chunks.Clear();
        
        // Create chunks using ScriptableObject name as prefix
        int totalFrames = sprites.Count;
        int chunkCount = Mathf.CeilToInt((float)totalFrames / framesPerChunk);
        string assetPrefix = spriteFramesData.name;
        
        for (int i = 0; i < chunkCount; i++)
        {
            int startFrame = i * framesPerChunk;
            int endFrame = Mathf.Min(startFrame + framesPerChunk - 1, totalFrames - 1);
            
            var chunk = new SpriteFramesData.FrameChunk
            {
                chunkName = $"Part{i + 1}",
                addressableLabel = $"{assetPrefix}_Part{i + 1}",
                startFrame = startFrame,
                endFrame = endFrame
            };
            
            spriteFramesData.chunks.Add(chunk);
        }
        
        spriteFramesData.GetTotalFrames();
        EditorUtility.SetDirty(spriteFramesData);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Created {chunkCount} chunks for {totalFrames} frames");
    }
    
    private void DisplayChunks()
    {
        for (int i = 0; i < spriteFramesData.chunks.Count; i++)
        {
            var chunk = spriteFramesData.chunks[i];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Chunk {i + 1}: {chunk}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.LabelField($"Total Frames: {spriteFramesData.GetTotalFrames()}", EditorStyles.boldLabel);
    }
    
    private void ApplyAddressableLabels()
    {
        if (!EditorUtility.DisplayDialog(
            "Apply Addressable Labels",
            "This will modify addressable settings for all frames. Continue?",
            "Yes",
            "Cancel"))
        {
            return;
        }
        
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Error", "Addressables not initialized. Please set up Addressables first.", "OK");
            return;
        }
        
        // Get selected objects (should be the frame folder or frames)
        var selectedObjects = Selection.objects;
        var frames = new List<Object>();
        
        foreach (var obj in selectedObjects)
        {
            if (obj is Sprite || obj is Texture2D)
            {
                frames.Add(obj);
            }
        }
        
        if (frames.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No frames selected. Please select the frames folder or frames.", "OK");
            return;
        }
        
        // Sort frames
        frames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        
        int processedCount = 0;
        
        // Apply labels based on chunks
        for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            var frame = frames[frameIndex];
            var chunk = spriteFramesData.GetChunkForFrame(frameIndex);
            
            if (chunk == null)
            {
                Debug.LogWarning($"Frame {frameIndex} ({frame.name}) not assigned to any chunk");
                continue;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(frame);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                // Create addressable entry
                var group = settings.DefaultGroup;
                entry = settings.CreateOrMoveEntry(guid, group);
            }
            
            // Clear existing labels
            entry.labels.Clear();
            
            // Add chunk label
            settings.AddLabel(chunk.addressableLabel);
            entry.labels.Add(chunk.addressableLabel);
            
            processedCount++;
        }
        
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(settings);
        
        Debug.Log($"Applied addressable labels to {processedCount} frames across {spriteFramesData.chunks.Count} chunks");
        EditorUtility.DisplayDialog("Success", $"Applied labels to {processedCount} frames!", "OK");
    }
}
