#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

/// <summary>
/// Comprehensive Asset Management Window for WebGL optimization
/// Scans assets, manages import settings, and provides WebGL optimization recommendations
/// </summary>
public class AssetManagementWindow : EditorWindow
{
    // UI State
    private Vector2 scrollPosition;
    private int selectedTab = 0;
    private string[] tabNames = { "Asset Scanner", "Batch Settings", "WebGL Optimizer", "Memory Analysis" };
    
    // Asset Scanner
    private List<AssetInfo> allAssets = new List<AssetInfo>();
    private List<AssetInfo> filteredAssets = new List<AssetInfo>();
    private bool isScanning = false;
    private string searchFilter = "";
    private AssetType filterType = AssetType.All;
    private SortMode sortMode = SortMode.SizeDescending;
    private HashSet<string> selectedAssets = new HashSet<string>();
    private bool selectAll = false;
    
    // Batch Settings
    private TextureImporterCompression compressionMode = TextureImporterCompression.Compressed;
    private int maxTextureSize = 2048;
    private TextureImporterFormat textureFormat = TextureImporterFormat.Automatic;
    private bool generateMipmaps = false;
    private bool readWriteEnabled = false;
    
    // Memory Analysis
    private Dictionary<string, long> categorySizes = new Dictionary<string, long>();
    private long totalProjectSize = 0;
    
    [MenuItem("Tools/Asset Management & WebGL Optimizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<AssetManagementWindow>("Asset Manager");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        // Header
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Asset Management & WebGL Optimizer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Comprehensive tool for asset optimization and WebGL platform settings", EditorStyles.miniLabel);
        GUILayout.Space(10);
        
        // Tab Selection
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(30));
        GUILayout.Space(10);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        switch (selectedTab)
        {
            case 0:
                DrawAssetScannerTab();
                break;
            case 1:
                DrawBatchSettingsTab();
                break;
            case 2:
                DrawWebGLOptimizerTab();
                break;
            case 3:
                DrawMemoryAnalysisTab();
                break;
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    #region Asset Scanner Tab
    
    private void DrawAssetScannerTab()
    {
        EditorGUILayout.LabelField("Asset Scanner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Scan all project assets and sort by size. Select assets for batch processing.", MessageType.Info);
        GUILayout.Space(10);
        
        // Scan Button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(isScanning ? "Scanning..." : "Scan All Assets", GUILayout.Height(40)))
        {
            if (!isScanning)
            {
                ScanAllAssets();
            }
        }
        
        if (allAssets.Count > 0)
        {
            if (GUILayout.Button($"Clear ({allAssets.Count} assets)", GUILayout.Height(40), GUILayout.Width(150)))
            {
                allAssets.Clear();
                filteredAssets.Clear();
                selectedAssets.Clear();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        if (allAssets.Count > 0)
        {
            // Filters
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            
            EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
            filterType = (AssetType)EditorGUILayout.EnumPopup(filterType, GUILayout.Width(150));
            
            EditorGUILayout.LabelField("Sort:", GUILayout.Width(40));
            sortMode = (SortMode)EditorGUILayout.EnumPopup(sortMode, GUILayout.Width(150));
            
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                ApplyFilters();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Selection Controls
            EditorGUILayout.BeginHorizontal();
            bool newSelectAll = EditorGUILayout.Toggle("Select All", selectAll);
            if (newSelectAll != selectAll)
            {
                selectAll = newSelectAll;
                if (selectAll)
                {
                    foreach (var asset in filteredAssets)
                        selectedAssets.Add(asset.path);
                }
                else
                {
                    selectedAssets.Clear();
                }
            }
            
            EditorGUILayout.LabelField($"Selected: {selectedAssets.Count}", GUILayout.Width(150));
            
            if (selectedAssets.Count > 0 && GUILayout.Button("Clear Selection", GUILayout.Width(120)))
            {
                selectedAssets.Clear();
                selectAll = false;
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Asset List Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Select", GUILayout.Width(50));
            EditorGUILayout.LabelField("Type", GUILayout.Width(80));
            EditorGUILayout.LabelField("Asset Name", GUILayout.MinWidth(200));
            EditorGUILayout.LabelField("Size", GUILayout.Width(100));
            EditorGUILayout.LabelField("Import Settings", GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            // Asset List
            foreach (var asset in filteredAssets)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = selectedAssets.Contains(asset.path);
                bool newIsSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(50));
                if (newIsSelected != isSelected)
                {
                    if (newIsSelected)
                        selectedAssets.Add(asset.path);
                    else
                        selectedAssets.Remove(asset.path);
                }
                
                EditorGUILayout.LabelField(asset.type.ToString(), GUILayout.Width(80));
                
                if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.MinWidth(200)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.path);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                
                EditorGUILayout.LabelField(FormatFileSize(asset.sizeBytes), GUILayout.Width(100));
                EditorGUILayout.LabelField(asset.importSettings, GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField($"Showing {filteredAssets.Count} of {allAssets.Count} assets");
        }
    }
    
    private void ScanAllAssets()
    {
        isScanning = true;
        allAssets.Clear();
        selectedAssets.Clear();
        
        try
        {
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            
            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string path = allAssetPaths[i];
                
                if (EditorUtility.DisplayCancelableProgressBar("Scanning Assets", path, (float)i / allAssetPaths.Length))
                {
                    break;
                }
                
                // Skip non-asset paths
                if (!path.StartsWith("Assets/"))
                    continue;
                
                // Skip directories
                if (AssetDatabase.IsValidFolder(path))
                    continue;
                
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                    continue;
                
                AssetInfo assetInfo = new AssetInfo
                {
                    path = path,
                    name = Path.GetFileName(path),
                    sizeBytes = fileInfo.Length,
                    type = GetAssetType(path),
                    importSettings = GetImportSettings(path)
                };
                
                allAssets.Add(assetInfo);
            }
            
            ApplyFilters();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isScanning = false;
        }
        
        Debug.Log($"Asset scan complete. Found {allAssets.Count} assets.");
    }
    
    private void ApplyFilters()
    {
        filteredAssets = allAssets.Where(a =>
        {
            // Type filter
            if (filterType != AssetType.All && a.type != filterType)
                return false;
            
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter) && !a.name.ToLower().Contains(searchFilter.ToLower()))
                return false;
            
            return true;
        }).ToList();
        
        // Apply sorting
        switch (sortMode)
        {
            case SortMode.SizeDescending:
                filteredAssets = filteredAssets.OrderByDescending(a => a.sizeBytes).ToList();
                break;
            case SortMode.SizeAscending:
                filteredAssets = filteredAssets.OrderBy(a => a.sizeBytes).ToList();
                break;
            case SortMode.NameAscending:
                filteredAssets = filteredAssets.OrderBy(a => a.name).ToList();
                break;
            case SortMode.NameDescending:
                filteredAssets = filteredAssets.OrderByDescending(a => a.name).ToList();
                break;
            case SortMode.TypeAscending:
                filteredAssets = filteredAssets.OrderBy(a => a.type).ToList();
                break;
        }
    }
    
    #endregion
    
    #region Batch Settings Tab
    
    private void DrawBatchSettingsTab()
    {
        EditorGUILayout.LabelField("Batch Import Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Apply import settings to selected assets from the Asset Scanner tab.", MessageType.Info);
        GUILayout.Space(10);
        
        if (selectedAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("No assets selected. Go to the Asset Scanner tab and select assets to modify.", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Selected Assets: {selectedAssets.Count}", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // Texture Settings
        EditorGUILayout.LabelField("Texture Import Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        compressionMode = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compressionMode);
        maxTextureSize = EditorGUILayout.IntPopup("Max Size", maxTextureSize, 
            new string[] { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" },
            new int[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 });
        textureFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("Format", textureFormat);
        generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", generateMipmaps);
        readWriteEnabled = EditorGUILayout.Toggle("Read/Write Enabled", readWriteEnabled);
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        
        // Quick Preset Buttons
        EditorGUILayout.LabelField("Quick Presets for WebGL", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("High Quality\n(Max 2048, Normal Compression)", GUILayout.Height(50)))
        {
            compressionMode = TextureImporterCompression.Compressed;
            maxTextureSize = 2048;
            textureFormat = TextureImporterFormat.Automatic;
            generateMipmaps = false;
            readWriteEnabled = false;
        }
        
        if (GUILayout.Button("Medium Quality\n(Max 1024, High Compression)", GUILayout.Height(50)))
        {
            compressionMode = TextureImporterCompression.CompressedHQ;
            maxTextureSize = 1024;
            textureFormat = TextureImporterFormat.Automatic;
            generateMipmaps = false;
            readWriteEnabled = false;
        }
        
        if (GUILayout.Button("Low Quality\n(Max 512, Max Compression)", GUILayout.Height(50)))
        {
            compressionMode = TextureImporterCompression.CompressedLQ;
            maxTextureSize = 512;
            textureFormat = TextureImporterFormat.Automatic;
            generateMipmaps = false;
            readWriteEnabled = false;
        }
        
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
        
        // Apply Button
        if (GUILayout.Button("Apply Settings to Selected Assets", GUILayout.Height(40)))
        {
            ApplyBatchSettings();
        }
        
        GUILayout.Space(20);
        
        // Additional Tools
        EditorGUILayout.LabelField("Additional Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        if (GUILayout.Button("Select All JPG/JPEG Files in Project"))
        {
            SelectAssetsByExtension(new[] { ".jpg", ".jpeg" });
        }
        
        if (GUILayout.Button("Select All PNG Files in Project"))
        {
            SelectAssetsByExtension(new[] { ".png" });
        }
        
        if (GUILayout.Button("Select All Large Assets (>500KB)"))
        {
            SelectLargeAssets(500 * 1024);
        }
        
        if (GUILayout.Button("Select All Large Assets (>1MB)"))
        {
            SelectLargeAssets(1024 * 1024);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void ApplyBatchSettings()
    {
        if (selectedAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("No Assets Selected", "Please select assets from the Asset Scanner tab first.", "OK");
            return;
        }
        
        bool confirm = EditorUtility.DisplayDialog("Apply Batch Settings",
            $"Apply import settings to {selectedAssets.Count} selected assets?\n\nThis action can be undone with Ctrl+Z.",
            "Apply", "Cancel");
        
        if (!confirm)
            return;
        
        int processedCount = 0;
        int textureCount = 0;
        
        try
        {
            foreach (var assetPath in selectedAssets)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Applying Settings", assetPath, (float)processedCount / selectedAssets.Count))
                {
                    break;
                }
                
                // Handle textures
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (textureImporter != null)
                {
                    textureImporter.textureCompression = compressionMode;
                    textureImporter.maxTextureSize = maxTextureSize;
                    textureImporter.mipmapEnabled = generateMipmaps;
                    textureImporter.isReadable = readWriteEnabled;
                    
                    // Set WebGL-specific settings
                    var platformSettings = textureImporter.GetPlatformTextureSettings("WebGL");
                    platformSettings.overridden = true;
                    platformSettings.maxTextureSize = maxTextureSize;
                    platformSettings.format = textureFormat;
                    textureImporter.SetPlatformTextureSettings(platformSettings);
                    
                    textureImporter.SaveAndReimport();
                    textureCount++;
                }
                
                processedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Batch Processing Complete",
            $"Processed {processedCount} assets successfully.\n{textureCount} textures were modified.",
            "OK");
        
        // Rescan to update sizes
        if (allAssets.Count > 0)
        {
            ScanAllAssets();
        }
    }
    
    private void SelectAssetsByExtension(string[] extensions)
    {
        if (allAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("No Assets Scanned", "Please scan assets first using the Asset Scanner tab.", "OK");
            return;
        }
        
        selectedAssets.Clear();
        foreach (var asset in allAssets)
        {
            string ext = Path.GetExtension(asset.path).ToLower();
            if (extensions.Any(e => e.Equals(ext, System.StringComparison.OrdinalIgnoreCase)))
            {
                selectedAssets.Add(asset.path);
            }
        }
        
        ApplyFilters();
        selectedTab = 0; // Switch to Asset Scanner tab
        
        EditorUtility.DisplayDialog("Selection Complete", $"Selected {selectedAssets.Count} assets with extensions: {string.Join(", ", extensions)}", "OK");
    }
    
    private void SelectLargeAssets(long minSizeBytes)
    {
        if (allAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("No Assets Scanned", "Please scan assets first using the Asset Scanner tab.", "OK");
            return;
        }
        
        selectedAssets.Clear();
        foreach (var asset in allAssets)
        {
            if (asset.sizeBytes >= minSizeBytes)
            {
                selectedAssets.Add(asset.path);
            }
        }
        
        ApplyFilters();
        selectedTab = 0; // Switch to Asset Scanner tab
        
        EditorUtility.DisplayDialog("Selection Complete", 
            $"Selected {selectedAssets.Count} assets larger than {FormatFileSize(minSizeBytes)}", "OK");
    }
    
    #endregion
    
    #region WebGL Optimizer Tab
    
    private void DrawWebGLOptimizerTab()
    {
        EditorGUILayout.LabelField("WebGL Platform Optimizer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Analyze and optimize Unity Player Settings and Project Settings for maximum WebGL compatibility and performance.", MessageType.Info);
        GUILayout.Space(10);
        
        // Current Settings Analysis
        EditorGUILayout.LabelField("Current WebGL Settings Analysis", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        DrawSettingStatus("Compression Format", GetCompressionFormatStatus());
        DrawSettingStatus("Exception Support", GetExceptionSupportStatus());
        DrawSettingStatus("Memory Size", GetMemorySizeStatus());
        DrawSettingStatus("Code Optimization", GetCodeOptimizationStatus());
        DrawSettingStatus("Managed Stripping Level", GetStrippingLevelStatus());
        DrawSettingStatus("Data Caching", GetDataCachingStatus());
        DrawSettingStatus("Addressables Build Path", GetAddressablesBuildPathStatus());
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        
        // Recommended Settings
        EditorGUILayout.LabelField("Recommended Settings for WebGL", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.HelpBox(
            "For iOS Safari compatibility and best performance:\n\n" +
            "• Compression: Brotli (best compression, Safari compatible)\n" +
            "• Exception Support: Explicitly Thrown Only (smaller build)\n" +
            "• Memory Size: 512MB-768MB (iOS Safari limit ~500MB)\n" +
            "• Code Optimization: Disk Size, Shorter Build Time (best for WebGL)\n" +
            "• Stripping Level: High or Medium (reduce build size)\n" +
            "• Data Caching: ON (faster subsequent loads)\n" +
            "• Use Addressables for large assets (progressive loading)",
            MessageType.Info);
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        
        // Quick Apply Buttons
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Apply Recommended Settings\n(iOS Safari Optimized)", GUILayout.Height(50)))
        {
            ApplyRecommendedWebGLSettings();
        }
        
        if (GUILayout.Button("Apply Performance Settings\n(Maximum Compression)", GUILayout.Height(50)))
        {
            ApplyPerformanceWebGLSettings();
        }
        
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
        
        // Individual Setting Controls
        EditorGUILayout.LabelField("Individual Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        if (GUILayout.Button("Set Compression to Brotli"))
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            Debug.Log("WebGL Compression set to Brotli");
        }
        
        if (GUILayout.Button("Set Exception Support to Explicitly Thrown Only"))
        {
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            Debug.Log("WebGL Exception Support optimized");
        }
        
        if (GUILayout.Button("Set Memory Size to 512MB"))
        {
            PlayerSettings.WebGL.memorySize = 512;
            Debug.Log("WebGL Memory Size set to 512MB");
        }
        
        if (GUILayout.Button("Enable Data Caching"))
        {
            PlayerSettings.WebGL.dataCaching = true;
            Debug.Log("WebGL Data Caching enabled");
        }
        
        if (GUILayout.Button("Optimize Color Space for WebGL (Linear)"))
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("Color Space set to Linear (better for WebGL)");
        }
        
        if (GUILayout.Button("Disable Auto Graphics API (Use WebGL 2.0)"))
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new UnityEngine.Rendering.GraphicsDeviceType[] { 
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 
            });
            Debug.Log("WebGL Graphics API set to WebGL 2.0");
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        
        // Build Size Optimizer
        EditorGUILayout.LabelField("Build Size Optimization", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        if (GUILayout.Button("Set High Stripping Level (Reduce Build Size)"))
        {
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
            Debug.Log("Stripping level set to High");
        }
        
        if (GUILayout.Button("Optimize IL2CPP Code Generation"))
        {
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            Debug.Log("IL2CPP optimized for size");
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        
        // Project Settings Link
        if (GUILayout.Button("Open Project Settings (Player)"))
        {
            SettingsService.OpenProjectSettings("Project/Player");
        }
        
        if (GUILayout.Button("Open Project Settings (Quality)"))
        {
            SettingsService.OpenProjectSettings("Project/Quality");
        }
    }
    
    private void DrawSettingStatus(string settingName, SettingStatus status)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(settingName, GUILayout.Width(200));
        
        Color oldColor = GUI.color;
        GUI.color = status.isOptimal ? Color.green : Color.yellow;
        EditorGUILayout.LabelField(status.currentValue, EditorStyles.boldLabel);
        GUI.color = oldColor;
        
        if (!status.isOptimal)
        {
            EditorGUILayout.LabelField($"→ {status.recommendedValue}", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private SettingStatus GetCompressionFormatStatus()
    {
        var current = PlayerSettings.WebGL.compressionFormat;
        return new SettingStatus
        {
            currentValue = current.ToString(),
            recommendedValue = "Brotli",
            isOptimal = current == WebGLCompressionFormat.Brotli
        };
    }
    
    private SettingStatus GetExceptionSupportStatus()
    {
        var current = PlayerSettings.WebGL.exceptionSupport;
        return new SettingStatus
        {
            currentValue = current.ToString(),
            recommendedValue = "ExplicitlyThrownExceptionsOnly",
            isOptimal = current == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly
        };
    }
    
    private SettingStatus GetMemorySizeStatus()
    {
        var current = PlayerSettings.WebGL.memorySize;
        bool isOptimal = current >= 512 && current <= 768;
        return new SettingStatus
        {
            currentValue = $"{current}MB",
            recommendedValue = "512-768MB",
            isOptimal = isOptimal
        };
    }
    
    private SettingStatus GetCodeOptimizationStatus()
    {
        var codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL);
        return new SettingStatus
        {
            currentValue = codeGeneration.ToString(),
            recommendedValue = "OptimizeSize",
            isOptimal = codeGeneration == UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize
        };
    }
    
    private SettingStatus GetStrippingLevelStatus()
    {
        var level = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
        bool isOptimal = level == ManagedStrippingLevel.High || level == ManagedStrippingLevel.Medium;
        return new SettingStatus
        {
            currentValue = level.ToString(),
            recommendedValue = "High or Medium",
            isOptimal = isOptimal
        };
    }
    
    private SettingStatus GetDataCachingStatus()
    {
        var current = PlayerSettings.WebGL.dataCaching;
        return new SettingStatus
        {
            currentValue = current ? "Enabled" : "Disabled",
            recommendedValue = "Enabled",
            isOptimal = current
        };
    }
    
    private SettingStatus GetAddressablesBuildPathStatus()
    {
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                return new SettingStatus
                {
                    currentValue = "Configured",
                    recommendedValue = "ServerData (Remote)",
                    isOptimal = true
                };
            }
        }
        catch { }
        
        return new SettingStatus
        {
            currentValue = "Not Found",
            recommendedValue = "Configure Addressables",
            isOptimal = false
        };
    }
    
    private void ApplyRecommendedWebGLSettings()
    {
        bool confirm = EditorUtility.DisplayDialog("Apply Recommended Settings",
            "This will apply iOS Safari optimized settings:\n\n" +
            "• Brotli Compression\n" +
            "• Exception Support: Explicitly Thrown Only\n" +
            "• Memory: 512MB\n" +
            "• Code Optimization: Size\n" +
            "• Stripping: High\n" +
            "• Data Caching: Enabled\n\n" +
            "Continue?",
            "Apply", "Cancel");
        
        if (!confirm) return;
        
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.memorySize = 512;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        
        Debug.Log("Applied recommended WebGL settings for iOS Safari compatibility");
        EditorUtility.DisplayDialog("Success", "Recommended WebGL settings applied successfully!", "OK");
    }
    
    private void ApplyPerformanceWebGLSettings()
    {
        bool confirm = EditorUtility.DisplayDialog("Apply Performance Settings",
            "This will apply maximum performance settings:\n\n" +
            "• Brotli Compression\n" +
            "• Exception Support: None (smallest size)\n" +
            "• Memory: 768MB\n" +
            "• Maximum Stripping\n" +
            "• Aggressive Code Optimization\n\n" +
            "Note: May break some debugging features.\n\n" +
            "Continue?",
            "Apply", "Cancel");
        
        if (!confirm) return;
        
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.memorySize = 768;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        
        Debug.Log("Applied maximum performance WebGL settings");
        EditorUtility.DisplayDialog("Success", "Performance WebGL settings applied successfully!", "OK");
    }
    
    #endregion
    
    #region Memory Analysis Tab
    
    private void DrawMemoryAnalysisTab()
    {
        EditorGUILayout.LabelField("Memory & Size Analysis", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Analyze project assets by category to identify memory usage patterns.", MessageType.Info);
        GUILayout.Space(10);
        
        if (GUILayout.Button("Analyze Project Assets", GUILayout.Height(40)))
        {
            AnalyzeMemoryUsage();
        }
        
        GUILayout.Space(10);
        
        if (categorySizes.Count > 0)
        {
            EditorGUILayout.LabelField($"Total Project Size: {FormatFileSize(totalProjectSize)}", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Size by Category:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            var sortedCategories = categorySizes.OrderByDescending(kvp => kvp.Value);
            foreach (var category in sortedCategories)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(category.Key, GUILayout.Width(200));
                EditorGUILayout.LabelField(FormatFileSize(category.Value), EditorStyles.boldLabel, GUILayout.Width(120));
                
                float percentage = (float)category.Value / totalProjectSize * 100f;
                EditorGUILayout.LabelField($"{percentage:F2}%", GUILayout.Width(60));
                
                // Progress bar
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                EditorGUI.ProgressBar(rect, percentage / 100f, "");
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // Recommendations
            EditorGUILayout.LabelField("Recommendations:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            if (categorySizes.ContainsKey("JPG/JPEG") && categorySizes["JPG/JPEG"] > 50 * 1024 * 1024)
            {
                EditorGUILayout.HelpBox($"JPG files total {FormatFileSize(categorySizes["JPG/JPEG"])}. Consider reducing texture size or using more compression.", MessageType.Warning);
            }
            
            if (categorySizes.ContainsKey("PNG") && categorySizes["PNG"] > 50 * 1024 * 1024)
            {
                EditorGUILayout.HelpBox($"PNG files total {FormatFileSize(categorySizes["PNG"])}. PNG is less efficient than JPG for photos. Consider converting to JPG where possible.", MessageType.Warning);
            }
            
            if (totalProjectSize > 500 * 1024 * 1024)
            {
                EditorGUILayout.HelpBox("Total project size exceeds 500MB. For iOS Safari compatibility, ensure Addressables are used to load assets progressively and not all at once.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
    }
    
    private void AnalyzeMemoryUsage()
    {
        categorySizes.Clear();
        totalProjectSize = 0;
        
        if (allAssets.Count == 0)
        {
            ScanAllAssets();
        }
        
        foreach (var asset in allAssets)
        {
            string category = GetCategoryName(asset.type, asset.path);
            
            if (!categorySizes.ContainsKey(category))
            {
                categorySizes[category] = 0;
            }
            
            categorySizes[category] += asset.sizeBytes;
            totalProjectSize += asset.sizeBytes;
        }
        
        Debug.Log($"Memory analysis complete. Total size: {FormatFileSize(totalProjectSize)}");
    }
    
    private string GetCategoryName(AssetType type, string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        
        switch (type)
        {
            case AssetType.Texture:
                if (ext == ".jpg" || ext == ".jpeg")
                    return "JPG/JPEG";
                else if (ext == ".png")
                    return "PNG";
                else if (ext == ".tga")
                    return "TGA";
                else
                    return "Other Textures";
            
            case AssetType.Audio:
                return "Audio";
            
            case AssetType.Video:
                return "Video";
            
            case AssetType.Model:
                return "3D Models";
            
            case AssetType.Script:
                return "Scripts";
            
            case AssetType.Scene:
                return "Scenes";
            
            case AssetType.Prefab:
                return "Prefabs";
            
            case AssetType.Material:
                return "Materials";
            
            default:
                return "Other";
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private AssetType GetAssetType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        
        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".tga" || ext == ".psd" || ext == ".tiff")
            return AssetType.Texture;
        
        if (ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".aiff")
            return AssetType.Audio;
        
        if (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".webm")
            return AssetType.Video;
        
        if (ext == ".fbx" || ext == ".obj" || ext == ".blend" || ext == ".dae")
            return AssetType.Model;
        
        if (ext == ".cs" || ext == ".js" || ext == ".boo")
            return AssetType.Script;
        
        if (ext == ".unity")
            return AssetType.Scene;
        
        if (ext == ".prefab")
            return AssetType.Prefab;
        
        if (ext == ".mat")
            return AssetType.Material;
        
        if (ext == ".shader")
            return AssetType.Shader;
        
        return AssetType.Other;
    }
    
    private string GetImportSettings(string path)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (textureImporter != null)
        {
            return $"{textureImporter.textureCompression}, Max: {textureImporter.maxTextureSize}";
        }
        
        AudioImporter audioImporter = AssetImporter.GetAtPath(path) as AudioImporter;
        if (audioImporter != null)
        {
            return "Audio";
        }
        
        return "N/A";
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F2} KB";
        
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
    
    #endregion
    
    #region Data Classes
    
    private class AssetInfo
    {
        public string path;
        public string name;
        public long sizeBytes;
        public AssetType type;
        public string importSettings;
    }
    
    private enum AssetType
    {
        All,
        Texture,
        Audio,
        Video,
        Model,
        Script,
        Scene,
        Prefab,
        Material,
        Shader,
        Other
    }
    
    private enum SortMode
    {
        SizeDescending,
        SizeAscending,
        NameAscending,
        NameDescending,
        TypeAscending
    }
    
    private struct SettingStatus
    {
        public string currentValue;
        public string recommendedValue;
        public bool isOptimal;
    }
    
    #endregion
}
#endif
