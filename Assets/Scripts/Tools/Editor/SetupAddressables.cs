using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using System.IO;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Set up Addressables for the unified scenes so they can be loaded properly.
    /// Creates the necessary groups and labels for UnifiedVideoScene, UnifiedLevelScene, UnifiedDeathScene.
    /// </summary>
    public static class AddressablesSetup
    {
        [MenuItem("SickOfIt/Setup Addressables for Unified Scenes")]
        public static void SetupUnifiedScenesInAddressables()
        {
            Debug.Log("=== Setting up Addressables for Unified Scenes ===");
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressables settings not found! Create Addressables settings first.");
                return;
            }
            
            // Create or get the Scenes group
            var scenesGroup = GetOrCreateGroup(settings, "Scenes");
            
            // Add unified scenes to Addressables
            SetupUnifiedScene(settings, scenesGroup, "Assets/Scenes/UnifiedVideoScene.unity", "UnifiedVideoScene");
            SetupUnifiedScene(settings, scenesGroup, "Assets/Scenes/UnifiedLevelScene.unity", "UnifiedLevelScene");  
            SetupUnifiedScene(settings, scenesGroup, "Assets/Scenes/UnifiedDeathScene.unity", "UnifiedDeathScene");
            
            // Mark settings as dirty and save
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            
            Debug.Log("=== Addressables Setup Complete ===");
            Debug.Log("✅ Added unified scenes to Addressables");
            Debug.Log("✅ Main menu should now load scenes properly via Addressables");
        }
        
        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                Debug.Log($"Created Addressables group: {groupName}");
            }
            return group;
        }
        
        private static void SetupUnifiedScene(AddressableAssetSettings settings, AddressableAssetGroup group, string scenePath, string addressableKey)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
                return;
            }
            
            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"Could not get GUID for scene: {scenePath}");
                return;
            }
            
            // Check if already in Addressables
            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                Debug.Log($"Scene already in Addressables: {addressableKey}");
                // Update the address to make sure it's correct
                entry.address = addressableKey;
                return;
            }
            
            // Add to Addressables
            entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressableKey;
            
            // Add labels for different loading contexts
            entry.SetLabel("scene", true);
            entry.SetLabel("unified", true);
            
            Debug.Log($"✅ Added to Addressables: {addressableKey} -> {scenePath}");
        }
        
        [MenuItem("SickOfIt/Debug - List Addressable Scenes")]
        public static void ListAddressableScenes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressables settings not found!");
                return;
            }
            
            Debug.Log("=== Current Addressable Scenes ===");
            
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                
                foreach (var entry in group.entries)
                {
                    if (entry.address.Contains("Scene") || entry.address.Contains("Level") || entry.address.Contains("Video") || entry.address.Contains("Death"))
                    {
                        Debug.Log($"Group: {group.name} | Address: {entry.address} | Asset: {entry.AssetPath}");
                    }
                }
            }
        }
    }
}