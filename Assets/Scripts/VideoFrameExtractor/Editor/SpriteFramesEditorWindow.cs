using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.VideoFrameExtractor.Editor
{
    /// <summary>
    /// Editor window for packing individual frames into sprite sheets,
    /// auto-chunking them, and assigning addressable labels for WebGL-optimized streaming.
    /// </summary>
    public class SpriteFramesEditorWindow : EditorWindow
    {
        private SpriteFramesData spriteFramesData;
        private SpriteFrameRenderer spriteFrameRenderer;
        private Vector2 scrollPosition;
        private int framesPerChunk = 64;
        private string lastOutputFolder = "";

        [MenuItem("Tools/Sprite Frames Manager")]
        public static void ShowWindow()
        {
            GetWindow<SpriteFramesEditorWindow>("Sprite Frames Manager");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Sprite Frames Manager (Sprite Sheet Mode)", EditorStyles.boldLabel);
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

            // Step 2: Pack Frames into Sprite Sheets
            DrawStep2_PackSpriteSheets();

            EditorGUILayout.Space();

            // Step 3: Auto-chunk sprite sheets
            DrawStep3_AutoChunkSpriteSheets();

            EditorGUILayout.Space();

            // Step 4: Display and edit chunks
            EditorGUILayout.LabelField("Step 4: Review Chunks", EditorStyles.boldLabel);

            if (spriteFramesData.chunks.Count == 0)
            {
                EditorGUILayout.HelpBox("No chunks defined yet. Use Step 3 to generate chunks.", MessageType.Warning);
            }
            else
            {
                DisplayChunks();
            }

            EditorGUILayout.Space();

            // Step 5: Assign addressable labels
            DrawStep5_AssignLabels();

            EditorGUILayout.Space();

            // Step 6: Extract first frame & setup player
            DrawStep6_SetupPlayer();

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

        // =====================================================================
        // Step 1: Create SpriteFramesData
        // =====================================================================

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

        // =====================================================================
        // Step 2: Pack individual frames into sprite sheets
        // =====================================================================

        private void DrawStep2_PackSpriteSheets()
        {
            EditorGUILayout.LabelField("Step 2: Pack Frames into Sprite Sheets", EditorStyles.boldLabel);

            // Source frame size detection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source Frame Size", GUILayout.Width(130));
            spriteFramesData.sourceFrameWidth = EditorGUILayout.IntField(spriteFramesData.sourceFrameWidth, GUILayout.Width(60));
            EditorGUILayout.LabelField("x", GUILayout.Width(12));
            spriteFramesData.sourceFrameHeight = EditorGUILayout.IntField(spriteFramesData.sourceFrameHeight, GUILayout.Width(60));

            if (GUILayout.Button("Detect from Selection", GUILayout.Width(140)))
            {
                DetectSourceFrameSize();
            }
            EditorGUILayout.EndHorizontal();

            spriteFramesData.maxSheetSize = EditorGUILayout.IntField("Max Sheet Size", spriteFramesData.maxSheetSize);

            if (GUILayout.Button("Auto-Calculate Optimal Grid"))
            {
                spriteFramesData.CalculateOptimalGrid();
                EditorUtility.SetDirty(spriteFramesData);
            }

            int sheetW = spriteFramesData.sheetColumns * spriteFramesData.frameWidth;
            int sheetH = spriteFramesData.sheetRows * spriteFramesData.frameHeight;
            int texW = spriteFramesData.sheetTextureWidth;
            int texH = spriteFramesData.sheetTextureHeight;
            float sourceAspect = spriteFramesData.sourceFrameWidth > 0 ? (float)spriteFramesData.sourceFrameWidth / spriteFramesData.sourceFrameHeight : 0;
            float cellAspect = spriteFramesData.frameWidth > 0 ? (float)spriteFramesData.frameWidth / spriteFramesData.frameHeight : 0;

            EditorGUILayout.HelpBox(
                $"Source: {spriteFramesData.sourceFrameWidth}x{spriteFramesData.sourceFrameHeight} (aspect {sourceAspect:F4})\n" +
                $"Cell:   {spriteFramesData.frameWidth}x{spriteFramesData.frameHeight} (aspect {cellAspect:F4}, mult of 4 \u2713)\n" +
                $"Grid:   {spriteFramesData.sheetColumns} cols x {spriteFramesData.sheetRows} rows = {spriteFramesData.framesPerSheet} frames/sheet\n" +
                $"Used:   {sheetW}x{sheetH} | Texture: {texW}x{texH} (power of 2 \u2713)\n\n" +
                "Select frame textures in the Project window, then pack.",
                MessageType.Info
            );

            if (!string.IsNullOrEmpty(lastOutputFolder))
            {
                EditorGUILayout.LabelField($"Last output: {lastOutputFolder}", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Pack Selected Frames into Sprite Sheets", GUILayout.Height(30)))
            {
                PackFramesIntoSpriteSheets();
            }
        }

        private void DetectSourceFrameSize()
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    // Read the actual image dimensions from the importer
                    object[] args = new object[2] { 0, 0 };
                    var method = typeof(TextureImporter).GetMethod("GetWidthAndHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(importer, args);
                        spriteFramesData.sourceFrameWidth = (int)args[0];
                        spriteFramesData.sourceFrameHeight = (int)args[1];
                    }
                    else
                    {
                        // Fallback: load texture and read dimensions
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex != null)
                        {
                            spriteFramesData.sourceFrameWidth = tex.width;
                            spriteFramesData.sourceFrameHeight = tex.height;
                        }
                    }
                    spriteFramesData.CalculateOptimalGrid();
                    EditorUtility.SetDirty(spriteFramesData);
                    Debug.Log($"Detected source frame size: {spriteFramesData.sourceFrameWidth}x{spriteFramesData.sourceFrameHeight}");
                    return;
                }
            }
            EditorUtility.DisplayDialog("Error", "Select at least one frame texture to detect dimensions.", "OK");
        }

        private void PackFramesIntoSpriteSheets()
        {
            if (spriteFramesData == null)
            {
                EditorUtility.DisplayDialog("Error", "Please load a SpriteFramesData first.", "OK");
                return;
            }

            // Collect selected textures
            var selectedObjects = Selection.objects;
            var textures = new List<Texture2D>();

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    textures.Add(tex);
            }

            if (textures.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No textures selected. Please select frame textures in the Project window.", "OK");
                return;
            }

            // Sort by name for correct frame ordering
            textures.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // Pick output folder
            string outputFolder = EditorUtility.SaveFolderPanel(
                "Select Output Folder for Sprite Sheets",
                string.IsNullOrEmpty(lastOutputFolder) ? "Assets" : lastOutputFolder,
                "SpriteSheets"
            );
            if (string.IsNullOrEmpty(outputFolder)) return;

            // Convert to relative Assets path
            if (outputFolder.StartsWith(Application.dataPath))
            {
                outputFolder = "Assets" + outputFolder.Substring(Application.dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Output folder must be inside the Assets folder.", "OK");
                return;
            }

            lastOutputFolder = outputFolder;

            // Ensure output directory exists
            string fullOutputDir = Path.Combine(Directory.GetCurrentDirectory(), outputFolder);
            if (!Directory.Exists(fullOutputDir))
            {
                Directory.CreateDirectory(fullOutputDir);
            }

            int cols = spriteFramesData.sheetColumns;
            int rows = spriteFramesData.sheetRows;
            int framesPerSheet = spriteFramesData.framesPerSheet;
            int fw = spriteFramesData.frameWidth;
            int fh = spriteFramesData.frameHeight;
            int sheetWidth = spriteFramesData.sheetTextureWidth;
            int sheetHeight = spriteFramesData.sheetTextureHeight;
            int totalSheets = Mathf.CeilToInt((float)textures.Count / framesPerSheet);
            string prefix = spriteFramesData.name;

            try
            {
                for (int s = 0; s < totalSheets; s++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Packing Sprite Sheets",
                        $"Sheet {s + 1}/{totalSheets}",
                        (float)s / totalSheets
                    );

                    // Create PoT texture (entire sheet is transparent black, frames placed in top-left region)
                    Texture2D sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);

                    // Clear to transparent black
                    Color[] clearPixels = new Color[sheetWidth * sheetHeight];
                    sheet.SetPixels(clearPixels);

                    int framesInSheet = Mathf.Min(framesPerSheet, textures.Count - s * framesPerSheet);

                    for (int f = 0; f < framesInSheet; f++)
                    {
                        int globalFrameIndex = s * framesPerSheet + f;
                        Texture2D sourceTex = textures[globalFrameIndex];

                        // Make readable and scale to cell size
                        Texture2D readableTex = GetReadableScaledTexture(sourceTex, fw, fh);

                        int col = f % cols;
                        int row = f / cols;
                        // Row 0 = top of sheet. In Unity texture coords, Y=0 is bottom.
                        // Place frames starting from top of the PoT texture:
                        // top row in texture = sheetHeight - fh
                        int xOffset = col * fw;
                        int yOffset = sheetHeight - (row + 1) * fh;

                        Color[] pixels = readableTex.GetPixels();
                        sheet.SetPixels(xOffset, yOffset, fw, fh, pixels);

                        Object.DestroyImmediate(readableTex);
                    }

                    sheet.Apply();

                    // Save as PNG
                    byte[] pngData = sheet.EncodeToPNG();
                    string fileName = $"{prefix}_sheet_{s:D4}.png";
                    string filePath = Path.Combine(fullOutputDir, fileName);
                    File.WriteAllBytes(filePath, pngData);

                    Object.DestroyImmediate(sheet);
                }

                AssetDatabase.Refresh();

                // Set import settings for all sheets
                for (int s = 0; s < totalSheets; s++)
                {
                    string fileName = $"{prefix}_sheet_{s:D4}.png";
                    string assetPath = $"{outputFolder}/{fileName}";
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.isReadable = false;
                        importer.mipmapEnabled = false;
                        importer.maxTextureSize = Mathf.Max(sheetWidth, sheetHeight);
                        importer.textureCompression = TextureImporterCompression.Compressed;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.SaveAndReimport();
                    }
                }

                // Update SpriteFramesData
                spriteFramesData.SetTotalFrameCount(textures.Count);
                spriteFramesData.SetTotalSpriteSheetCount(totalSheets);
                EditorUtility.SetDirty(spriteFramesData);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"[SpriteFramesEditor] Set total frames to {textures.Count} (from {textures.Count} input textures)");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Packed {textures.Count} frames into {totalSheets} sprite sheets ({sheetWidth}x{sheetHeight}) at {outputFolder}");
            EditorUtility.DisplayDialog(
                "Success",
                $"Packed {textures.Count} frames into {totalSheets} sprite sheets!\n\n" +
                $"Sheet size: {sheetWidth}x{sheetHeight}\n" +
                $"Frames per sheet: {framesPerSheet}\n" +
                $"Output: {outputFolder}",
                "OK"
            );
        }

        /// <summary>
        /// Read any texture (even non-readable ones) and scale to target size via RenderTexture blit
        /// </summary>
        private Texture2D GetReadableScaledTexture(Texture source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // =====================================================================
        // Step 3: Auto-chunk sprite sheets
        // =====================================================================

        private void DrawStep3_AutoChunkSpriteSheets()
        {
            EditorGUILayout.LabelField("Step 3: Auto-Chunk Sprite Sheets", EditorStyles.boldLabel);

            int sheetsPerChunk = Mathf.CeilToInt((float)framesPerChunk / spriteFramesData.framesPerSheet);
            float secondsPerChunk = framesPerChunk / 30f; // Estimate at default 30 FPS (actual FPS controlled by Timeline)

            EditorGUILayout.HelpBox(
                $"Select sprite sheet textures in the Project window, then auto-chunk them.\n\n" +
                $"At {framesPerChunk} frames/chunk = {sheetsPerChunk} sheets/chunk ≈ {secondsPerChunk:F1}s per chunk (at 30 FPS)\n" +
                $"Labels: '{spriteFramesData.name}_Part1', '{spriteFramesData.name}_Part2', etc.",
                MessageType.Info
            );

            framesPerChunk = EditorGUILayout.IntField("Frames Per Chunk", framesPerChunk);

            if (GUILayout.Button("Auto-Chunk Selected Sprite Sheets", GUILayout.Height(30)))
            {
                AutoChunkSpriteSheets();
            }
        }

        private void AutoChunkSpriteSheets()
        {
            // Get selected sprite sheet textures
            var selectedObjects = Selection.objects;
            var sheetTextures = new List<Texture2D>();

            foreach (var obj in selectedObjects)
            {
                if (obj is Texture2D tex)
                {
                    sheetTextures.Add(tex);
                }
            }

            if (sheetTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No sprite sheet textures selected. Please select the packed sprite sheet textures.", "OK");
                return;
            }

            // Sort sheets by name for correct ordering
            sheetTextures.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            int framesPerSheet = spriteFramesData.framesPerSheet;
            
            // Get stored totalFrames (set during packing step) without recalculating from chunks
            int storedTotalFrames = spriteFramesData.GetStoredTotalFrames();
            
            // Validate stored value against selected sheet count
            int estimatedMaxFrames = sheetTextures.Count * framesPerSheet;
            int estimatedMinFrames = (sheetTextures.Count - 1) * framesPerSheet + 1;
            
            int totalFrames;
            if (storedTotalFrames >= estimatedMinFrames && storedTotalFrames <= estimatedMaxFrames)
            {
                // Stored value is valid, use it
                totalFrames = storedTotalFrames;
            }
            else
            {
                // Estimate from sheet count (assume all sheets full, will clamp in loop)
                totalFrames = estimatedMaxFrames;
                spriteFramesData.SetTotalFrameCount(totalFrames);
                
                if (storedTotalFrames != 0)
                {
                    Debug.LogWarning($"Stored frame count ({storedTotalFrames}) doesn't match sheet count. Estimating {totalFrames} frames from {sheetTextures.Count} sheets.");
                }
            }

            // Calculate sheets per chunk
            int sheetsPerChunk = Mathf.CeilToInt((float)framesPerChunk / framesPerSheet);

            // Clear existing chunks
            spriteFramesData.chunks.Clear();

            int chunkCount = Mathf.CeilToInt((float)sheetTextures.Count / sheetsPerChunk);
            string assetPrefix = spriteFramesData.name;

            for (int i = 0; i < chunkCount; i++)
            {
                int startSheetIndex = i * sheetsPerChunk;
                int endSheetIndex = Mathf.Min(startSheetIndex + sheetsPerChunk - 1, sheetTextures.Count - 1);

                int startFrame = startSheetIndex * framesPerSheet;
                int endFrame = Mathf.Min((endSheetIndex + 1) * framesPerSheet - 1, totalFrames - 1);

                // Collect the texture references for this chunk (editor-only preview)
                var chunkTextures = new List<Texture2D>();
                for (int s = startSheetIndex; s <= endSheetIndex; s++)
                    chunkTextures.Add(sheetTextures[s]);

                var chunk = new SpriteFramesData.FrameChunk
                {
                    chunkName = $"Part{i + 1}",
                    addressableLabel = $"{assetPrefix}_Part{i + 1}",
                    startFrame = startFrame,
                    endFrame = endFrame,
                    editorTextures = chunkTextures
                };

                spriteFramesData.chunks.Add(chunk);
            }

            spriteFramesData.SetTotalSpriteSheetCount(sheetTextures.Count);
            spriteFramesData.GetTotalFrames();
            EditorUtility.SetDirty(spriteFramesData);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created {chunkCount} chunks for {totalFrames} frames ({sheetTextures.Count} sprite sheets, {sheetsPerChunk} sheets/chunk)");
        }

        // =====================================================================
        // Step 4: Display chunks
        // =====================================================================

        private void DisplayChunks()
        {
            int framesPerSheet = spriteFramesData.framesPerSheet;

            for (int i = 0; i < spriteFramesData.chunks.Count; i++)
            {
                var chunk = spriteFramesData.chunks[i];
                int sheetsInChunk = Mathf.CeilToInt((float)chunk.frameCount / framesPerSheet);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Chunk {i + 1}: {chunk} ({sheetsInChunk} sheets)", EditorStyles.boldLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Fill Editor Textures from Addressable Labels"))
            {
                FillEditorTexturesFromLabels();
            }

            // Display and edit totalFrames
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Frames:", GUILayout.Width(100));
            int currentTotal = spriteFramesData.GetStoredTotalFrames();
            int newTotal = EditorGUILayout.IntField(currentTotal, GUILayout.Width(60));
            if (newTotal != currentTotal)
            {
                spriteFramesData.SetTotalFrameCount(newTotal);
                EditorUtility.SetDirty(spriteFramesData);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SpriteFramesEditor] Manually set total frames to {newTotal}");
            }
            EditorGUILayout.LabelField($"(Calculated from chunks: {spriteFramesData.GetTotalFrames()})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Populates editorTextures on every chunk by querying the Addressables settings
        /// for entries matching each chunk's addressableLabel.
        /// </summary>
        private void FillEditorTexturesFromLabels()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Addressables not initialized. Please set up Addressables first.", "OK");
                return;
            }

            int totalFilled = 0;

            foreach (var chunk in spriteFramesData.chunks)
            {
                string label = chunk.addressableLabel;
                if (string.IsNullOrEmpty(label)) continue;

                // Collect all Addressable entries that carry this chunk's label
                var textures = new List<Texture2D>();
                foreach (var group in settings.groups)
                {
                    if (group == null) continue;
                    foreach (var entry in group.entries)
                    {
                        if (entry.labels.Contains(label))
                        {
                            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.AssetPath);
                            if (tex != null)
                                textures.Add(tex);
                        }
                    }
                }

                // Sort by name so sheet order is deterministic
                textures.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

                chunk.editorTextures = textures;
                totalFilled += textures.Count;
            }

            EditorUtility.SetDirty(spriteFramesData);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpriteFramesEditor] Filled editorTextures on {spriteFramesData.chunks.Count} chunks ({totalFilled} textures total) from Addressable labels.");
        }

        // =====================================================================
        // Step 5: Assign addressable labels to sprite sheet textures
        // =====================================================================

        private void DrawStep5_AssignLabels()
        {
            EditorGUILayout.LabelField("Step 5: Assign Addressable Labels", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select the sprite sheet textures, then assign addressable labels based on chunks.\n" +
                "Sheet textures are assigned to chunks based on their sorted order.",
                MessageType.Info
            );

            if (GUILayout.Button("Apply Addressable Labels to Sprite Sheets", GUILayout.Height(30)))
            {
                ApplyAddressableLabelsToSheets();
            }
        }

        private void ApplyAddressableLabelsToSheets()
        {
            if (!EditorUtility.DisplayDialog(
                "Apply Addressable Labels",
                "This will modify addressable settings for all selected sprite sheet textures. Continue?",
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

            // Get selected sprite sheet textures
            var selectedObjects = Selection.objects;
            var sheets = new List<Object>();

            foreach (var obj in selectedObjects)
            {
                if (obj is Texture2D)
                {
                    sheets.Add(obj);
                }
            }

            if (sheets.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No sprite sheet textures selected.", "OK");
                return;
            }

            // Sort sheets by name
            sheets.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            int framesPerSheet = spriteFramesData.framesPerSheet;
            int sheetsPerChunk = Mathf.CeilToInt((float)framesPerChunk / framesPerSheet);
            int processedCount = 0;

            // Assign each sheet to the correct chunk based on its index
            for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                var sheet = sheets[sheetIndex];

                // Determine which chunk this sheet belongs to
                int globalFrameStart = sheetIndex * framesPerSheet;
                var chunk = spriteFramesData.GetChunkForFrame(globalFrameStart);

                if (chunk == null)
                {
                    Debug.LogWarning($"Sheet {sheetIndex} ({sheet.name}) at frame {globalFrameStart} not assigned to any chunk");
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(sheet);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    var group = settings.DefaultGroup;
                    entry = settings.CreateOrMoveEntry(guid, group);
                }

                // Clear existing labels and add chunk label
                entry.labels.Clear();
                settings.AddLabel(chunk.addressableLabel);
                entry.labels.Add(chunk.addressableLabel);

                processedCount++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(settings);

            Debug.Log($"Applied addressable labels to {processedCount} sprite sheets across {spriteFramesData.chunks.Count} chunks");
            EditorUtility.DisplayDialog("Success", $"Applied labels to {processedCount} sprite sheets!", "OK");
        }

        // =====================================================================
        // Step 6: Extract first frame & setup SpriteFrameRenderer
        // =====================================================================

        private void DrawStep6_SetupPlayer()
        {
            EditorGUILayout.LabelField("Step 6: Setup Renderer with First Frame", EditorStyles.boldLabel);

            spriteFrameRenderer = (SpriteFrameRenderer)EditorGUILayout.ObjectField(
                "Sprite Frame Renderer",
                spriteFrameRenderer,
                typeof(SpriteFrameRenderer),
                true
            );

            EditorGUILayout.HelpBox(
                "Drag in the first sprite sheet texture and the SpriteFrameRenderer from the scene.\n\n" +
                "This will:\n" +
                "• Extract the first frame from the sprite sheet\n" +
                "• Save it as a standalone sprite next to the SpriteFramesData asset\n" +
                "• Set the SpriteRenderer sprite on the renderer\n" +
                "• Scale the renderer GameObject to match the original source aspect ratio",
                MessageType.Info
            );

            if (GUILayout.Button("Extract First Frame & Setup Player", GUILayout.Height(30)))
            {
                ExtractFirstFrameAndSetupPlayer();
            }
        }

        private void ExtractFirstFrameAndSetupPlayer()
        {
            if (spriteFramesData == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a SpriteFramesData asset.", "OK");
                return;
            }

            if (spriteFrameRenderer == null)
            {
                EditorUtility.DisplayDialog("Error", "Please drag in a SpriteFrameRenderer from the scene.", "OK");
                return;
            }

            // Find the first sprite sheet texture — look in the last output folder or let user select
            Texture2D firstSheet = null;

            // Try to find it from selected objects
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex)
                {
                    firstSheet = tex;
                    break;
                }
            }

            // If nothing selected, try auto-finding from last output folder
            if (firstSheet == null && !string.IsNullOrEmpty(lastOutputFolder))
            {
                string prefix = spriteFramesData.name;
                string firstSheetPath = $"{lastOutputFolder}/{prefix}_sheet_0000.png";
                firstSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(firstSheetPath);
            }

            if (firstSheet == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find the first sprite sheet. Please select it in the Project window.",
                    "OK");
                return;
            }

            int fw = spriteFramesData.frameWidth;
            int fh = spriteFramesData.frameHeight;
            int cols = spriteFramesData.sheetColumns;
            int rows = spriteFramesData.sheetRows;
            int sheetTexW = spriteFramesData.sheetTextureWidth;
            int sheetTexH = spriteFramesData.sheetTextureHeight;

            // Blit sheet to the PoT texture size, then extract first cell
            // First cell = column 0, row 0 → top-left of texture → y = sheetTexH - fh
            RenderTexture rt = RenderTexture.GetTemporary(sheetTexW, sheetTexH, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(firstSheet, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            // Read only the first cell region directly
            Texture2D firstFrameTex = new Texture2D(fw, fh, TextureFormat.RGBA32, false);
            firstFrameTex.ReadPixels(new Rect(0, sheetTexH - fh, fw, fh), 0, 0);
            firstFrameTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // Save next to SpriteFramesData asset
            string dataPath = AssetDatabase.GetAssetPath(spriteFramesData);
            string directory = Path.GetDirectoryName(dataPath);
            string firstFrameFileName = $"{spriteFramesData.name}_FirstFrame.png";
            string firstFramePath = Path.Combine(directory, firstFrameFileName).Replace("\\", "/");

            byte[] pngData = firstFrameTex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), firstFramePath), pngData);
            Object.DestroyImmediate(firstFrameTex);

            AssetDatabase.Refresh();

            // Set import settings: Sprite 2D, Single mode
            TextureImporter importer = AssetImporter.GetAtPath(firstFramePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = Mathf.Max(fw, fh);
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            // Load the saved sprite
            Sprite firstFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(firstFramePath);
            if (firstFrameSprite == null)
            {
                Debug.LogError($"Failed to load sprite at {firstFramePath}");
                return;
            }

            // Apply to SpriteFrameRenderer's SpriteRenderer
            SpriteRenderer sr = spriteFrameRenderer.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError("SpriteFrameRenderer has no SpriteRenderer component!");
                return;
            }

            Undo.RecordObject(sr, "Set First Frame Sprite");
            sr.sprite = firstFrameSprite;
            EditorUtility.SetDirty(sr);

            // Scale the GameObject to match original source aspect ratio
            if (spriteFramesData.frameWidth > 0 && spriteFramesData.sourceFrameWidth > 0)
            {
                float scaleX = (float)spriteFramesData.sourceFrameWidth / spriteFramesData.frameWidth;
                float scaleY = (float)spriteFramesData.sourceFrameHeight / spriteFramesData.frameHeight;

                Undo.RecordObject(spriteFrameRenderer.transform, "Scale Renderer to Source Aspect Ratio");
                Vector3 currentScale = spriteFrameRenderer.transform.localScale;
                spriteFrameRenderer.transform.localScale = new Vector3(
                    scaleX,
                    scaleY,
                    currentScale.z
                );
                EditorUtility.SetDirty(spriteFrameRenderer.transform);
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"First frame extracted to {firstFramePath}, applied to {spriteFrameRenderer.name}, and scaled to match source aspect ratio.");
            EditorUtility.DisplayDialog("Success",
                $"First frame saved to: {firstFramePath}\n\n" +
                $"SpriteRenderer sprite set on: {spriteFrameRenderer.name}\n" +
                $"Scale adjusted for source aspect ratio.",
                "OK");
        }
    }
}
