#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Updates Addressable groups to include all generated scenes.
    /// Ensures every level/video/death scene is addressable and loadable at runtime.
    /// </summary>
    public static class AddressableGroupGenerator
    {
        private const string SCENES_GROUP_NAME = "GameScenes";

        public static void UpdateAddressableGroups(GameDefinition gd)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressableGen] Addressable Asset Settings not found!");
                return;
            }

            // Find or create group
            var group = settings.FindGroup(SCENES_GROUP_NAME);
            if (group == null)
            {
                group = settings.CreateGroup(SCENES_GROUP_NAME, false, false, true,
                    new List<AddressableAssetGroupSchema>
                    {
                        ScriptableObject.CreateInstance<BundledAssetGroupSchema>(),
                        ScriptableObject.CreateInstance<ContentUpdateGroupSchema>()
                    });
            }

            int added = 0;

            // Add level scenes
            foreach (var level in gd.levels)
            {
                string scenePath = $"Assets/Scenes/Levels/{level.levelName}.unity";
                if (AddSceneToGroup(settings, group, scenePath, level.levelName))
                    added++;
            }

            // Add video scenes
            foreach (var video in gd.videos)
            {
                string scenePath = $"Assets/Scenes/Videos/{video.videoName}.unity";
                if (AddSceneToGroup(settings, group, scenePath, video.videoName))
                    added++;
            }

            // Add death scenes
            foreach (var death in gd.deaths)
            {
                string scenePath = $"Assets/Scenes/Deaths/{death.deathName}.unity";
                if (AddSceneToGroup(settings, group, scenePath, death.deathName))
                    added++;
            }

            if (added > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
                Debug.Log($"[AddressableGen] Updated group '{SCENES_GROUP_NAME}': {added} scenes added/updated");
            }
            else
            {
                Debug.Log($"[AddressableGen] Group '{SCENES_GROUP_NAME}' already up to date");
            }
        }

        private static bool AddSceneToGroup(AddressableAssetSettings settings, AddressableAssetGroup group, string scenePath, string address)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[AddressableGen] Scene not found: {scenePath}");
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(guid))
                return false;

            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                // Already exists — ensure correct address and group
                if (entry.parentGroup != group)
                    settings.MoveEntry(entry, group);
                if (entry.address != address)
                    entry.SetAddress(address);
                return false; // Not newly added
            }

            // Add new entry
            entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(address);
            return true;
        }
    }
}
#endif
