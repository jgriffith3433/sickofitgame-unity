using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Editor window to manage Addressables version paths
/// Updates all groups' Load Path URLs with new version numbers
/// </summary>
public class AddressablesVersionManager : EditorWindow
{
    private string baseURL = "https://sickofitgame.com/Game";
    private string currentVersion = "v10";
    private string gameName = "SickOfIt";
    private string subfolder = "Build/Addressables";
    
    private AddressableAssetSettings settings;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Addressables Version Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<AddressablesVersionManager>("Addressables Version");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    private void OnEnable()
    {
        settings = AddressableAssetSettingsDefaultObject.Settings;
        
        // Try to detect current version from existing paths
        if (settings != null && settings.groups.Count > 0)
        {
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    string loadPath = schema.LoadPath.GetValue(settings);
                    if (loadPath.Contains("/Game/"))
                    {
                        // Try to extract version from existing URL
                        int vIndex = loadPath.IndexOf("/v");
                        if (vIndex >= 0)
                        {
                            int endIndex = loadPath.IndexOf("/", vIndex + 1);
                            if (endIndex > vIndex)
                            {
                                currentVersion = loadPath.Substring(vIndex + 1, endIndex - vIndex - 1);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Addressables settings not found. Make sure Addressables package is installed.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Addressables Version Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Update the version path for all Addressables groups at once.", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // URL Configuration
        EditorGUILayout.LabelField("URL Configuration", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        baseURL = EditorGUILayout.TextField("Base URL", baseURL);
        currentVersion = EditorGUILayout.TextField("Version", currentVersion);
        gameName = EditorGUILayout.TextField("Game Name", gameName);
        subfolder = EditorGUILayout.TextField("Subfolder", subfolder);
        
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(10);
        
        // Preview
        string previewPath = BuildLoadPath();
        EditorGUILayout.LabelField("Preview Load Path:", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(previewPath, EditorStyles.textArea, GUILayout.Height(40));
        
        EditorGUILayout.Space(10);
        
        // Groups Preview
        EditorGUILayout.LabelField("Groups to Update:", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        
        int validGroups = 0;
        foreach (var group in settings.groups)
        {
            if (group == null || group.ReadOnly) continue;
            
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                validGroups++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {group.Name}", GUILayout.Width(200));
                
                string currentLoadPath = schema.LoadPath.GetValue(settings);
                EditorGUILayout.LabelField(currentLoadPath, EditorStyles.miniLabel);
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        if (validGroups == 0)
        {
            EditorGUILayout.HelpBox("No groups found with BundledAssetGroupSchema.", MessageType.Warning);
        }
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space(10);
        
        // Action Buttons
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Update All Groups", GUILayout.Height(30)))
        {
            UpdateAllGroups();
        }
        
        if (GUILayout.Button("Set to Local (Embedded)", GUILayout.Height(30)))
        {
            SetAllGroupsLocal();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Quick Version Buttons
        EditorGUILayout.LabelField("Quick Version Change:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("v10"))
        {
            currentVersion = "v10";
        }
        if (GUILayout.Button("v11"))
        {
            currentVersion = "v11";
        }
        if (GUILayout.Button("v12"))
        {
            currentVersion = "v12";
        }
        if (GUILayout.Button("Increment"))
        {
            IncrementVersion();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Help
        EditorGUILayout.HelpBox(
            "1. Set your version (e.g., v10, v11)\n" +
            "2. Click 'Update All Groups' to apply\n" +
            "3. Build Addressables\n" +
            "4. Upload ServerData/WebGL/* to the URL shown above\n" +
            "5. Build WebGL game as usual",
            MessageType.None);
    }

    private string BuildLoadPath()
    {
        // Clean up URL parts
        string cleanBase = baseURL.TrimEnd('/');
        string cleanVersion = currentVersion.Trim();
        string cleanGame = gameName.Trim();
        string cleanSubfolder = subfolder.Trim('/');
        
        return $"{cleanBase}/{cleanVersion}/{cleanGame}/{cleanSubfolder}/[BuildTarget]";
    }

    private void UpdateAllGroups()
    {
        if (settings == null) return;
        
        string newLoadPath = BuildLoadPath();
        int updatedCount = 0;
        
        // Update the RemoteLoadPath profile variable using the public SetValue method
        var profileSettings = settings.profileSettings;
        string profileId = settings.activeProfileId;
        
        // SetValue takes the variable name directly (not ID)
        profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, newLoadPath);
        
        // Update all groups to use RemoteBuildPath and RemoteLoadPath
        foreach (var group in settings.groups)
        {
            if (group == null || group.ReadOnly) continue;
            
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                // Set Build Path to RemoteBuildPath
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                
                // Set Load Path to RemoteLoadPath (which we just updated)
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                
                EditorUtility.SetDirty(group);
                updatedCount++;
            }
        }
        
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"✓ Updated {updatedCount} Addressables groups to:\n{newLoadPath}");
        
        EditorUtility.DisplayDialog(
            "Groups Updated",
            $"Updated {updatedCount} groups to:\n\n{newLoadPath}\n\nDon't forget to:\n1. Build Addressables\n2. Upload ServerData/WebGL/* to your server",
            "OK");
    }

    private void SetAllGroupsLocal()
    {
        if (settings == null) return;
        
        if (!EditorUtility.DisplayDialog(
            "Set to Local Build",
            "This will set all groups to LocalBuildPath/LocalLoadPath.\n\nAddressables will be embedded in the WebGL build.\n\nContinue?",
            "Yes", "Cancel"))
        {
            return;
        }
        
        int updatedCount = 0;
        
        foreach (var group in settings.groups)
        {
            if (group == null || group.ReadOnly) continue;
            
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BuildPath.SetVariableByName(settings, "LocalBuildPath");
                schema.LoadPath.SetVariableByName(settings, "LocalLoadPath");
                
                EditorUtility.SetDirty(group);
                updatedCount++;
            }
        }
        
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"✓ Set {updatedCount} groups to LocalBuildPath/LocalLoadPath (embedded in build)");
        
        EditorUtility.DisplayDialog(
            "Groups Updated to Local",
            $"Updated {updatedCount} groups to local paths.\n\nAddressables will now be embedded in your WebGL build.",
            "OK");
    }

    private void IncrementVersion()
    {
        // Try to extract number from version string (e.g., "v10" -> 10)
        string versionNumber = currentVersion.TrimStart('v', 'V');
        
        if (int.TryParse(versionNumber, out int number))
        {
            currentVersion = $"v{number + 1}";
        }
        else
        {
            EditorUtility.DisplayDialog("Invalid Version", "Version must be in format like 'v10' to increment.", "OK");
        }
    }
}
