#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;
using SickOfIt.Tools.Editor.Generation;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// THE ONLY PROJECT BUILDER YOU NEED.
    /// 
    /// Reads GameDefinition and generates:
    /// 
    /// Phase 1 - Cleanup:
    ///   - DeleteGeneratedPrefabs() - removes Character, Enemy_*, Projectile_*
    ///   - CleanupOldScenes() - removes old generated scenes
    /// 
    /// Phase 2 - Prefabs (order matters):
    ///   - ProjectilePrefabGenerator - Projectile_Player, Projectile_Enemy, Projectile_Laser
    ///   - PickupPrefabGenerator - Diamond.prefab (point drop pickup)
    ///   - PlatformPrefabGenerator - Platform.prefab (one-way platform)
    ///   - PlayerPrefabGenerator - Character.prefab
    ///   - EnemyPrefabGenerator - Enemy_Chaser, Enemy_Fighter, Enemy_Sentinel, Enemy_Shooter
    /// 
    /// Phase 3 - Scenes:
    ///   - VideoSceneGenerator - Video.unity
    ///   - LevelSceneGenerator - Level.unity
    ///   - DeathSceneGenerator - Death.unity
    ///
    /// MainMenu, LevelSelection, VideoSelection, and UpgradeShop stay hand-authored.
    /// Their generators rebuild a button list and drop the house, download UI, and layout.
    /// 
    /// Phase 4 - Addressables
    /// 
    /// ONE BUTTON. ONE SCRIPT. EVERYTHING GENERATED.
    /// </summary>
    public static class ProjectGenerator
    {
        private const string SCENES_ROOT = "Assets/Scenes";
        
        [MenuItem("SickOfIt/Generate Project")]
        public static void GenerateProject()
        {
            ////Debug.Log("=== GENERATING PROJECT FROM CONFIG ===");
            
            var gd = LoadGameDefinition();
            if (gd == null) return;

            SortingLayerSetup.Ensure();
            
            // ═══════════════════════════════════════════════════════════════
            // PHASE 1: CLEANUP
            // ═══════════════════════════════════════════════════════════════
            ////Debug.Log("Phase 1: Cleanup...");
            DeleteGeneratedPrefabs();
            DeleteGeneratedAnimations();
            CleanupOldScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // ═══════════════════════════════════════════════════════════════
            // PHASE 2: GENERATE PREFABS
            // Order: Projectiles → Player → Enemies (player needed for enemy targeting)
            // ═══════════════════════════════════════════════════════════════
            ////Debug.Log("Phase 2: Generating prefabs...");
            gd = LoadGameDefinition();
            
            try { ProjectilePrefabGenerator.GenerateAll(gd); }
            catch (System.Exception e) { Debug.LogError($"ProjectilePrefabGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { PickupPrefabGenerator.Generate(gd); }
            catch (System.Exception e) { Debug.LogError($"PickupPrefabGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { PlatformPrefabGenerator.Generate(gd); }
            catch (System.Exception e) { Debug.LogError($"PlatformPrefabGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { PlayerPrefabGenerator.Generate(gd); }
            catch (System.Exception e) { Debug.LogError($"PlayerPrefabGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { EnemyPrefabGenerator.GenerateAll(gd); }
            catch (System.Exception e) { Debug.LogError($"EnemyPrefabGenerator failed: {e}"); }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // ═══════════════════════════════════════════════════════════════
            // PHASE 3: GENERATE SCENES
            // ═══════════════════════════════════════════════════════════════
            ////Debug.Log("Phase 3: Generating scenes...");
            gd = LoadGameDefinition();
            
            try { VideoSceneGenerator.GenerateVideoScene(gd); }
            catch (System.Exception e) { Debug.LogError($"VideoSceneGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { LevelSceneGenerator.GenerateLevelScene(gd); }
            catch (System.Exception e) { Debug.LogError($"LevelSceneGenerator failed: {e}"); }
            
            gd = LoadGameDefinition();
            try { DeathSceneGenerator.GenerateDeathScene(gd); }
            catch (System.Exception e) { Debug.LogError($"DeathSceneGenerator failed: {e}"); }

            // Menu scenes are hand-authored (house transition, bundle download, shop layout).
            // MainMenuSceneGenerator and the other UI generators replace those scenes with a
            // button list, so Generate Project leaves them alone.
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // ═══════════════════════════════════════════════════════════════
            // PHASE 4: ADDRESSABLES
            // ═══════════════════════════════════════════════════════════════
            ////Debug.Log("Phase 4: Setting up Addressables...");
            gd = LoadGameDefinition();
            SetupAddressables(gd);
            
            // Force refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            ////Debug.Log("=== PROJECT GENERATION COMPLETE ===");
            ////Debug.Log($"✅ Generated prefabs: Character, Platform, Diamond, {gd.enemyTypes?.Count ?? 0} enemies, {gd.projectileConfigs?.Count ?? 0} projectiles");
            ////Debug.Log($"✅ Generated scenes: Video, Level, Death (menu scenes left as authored)");
            ////Debug.Log($"✅ Config: {gd.levels.Count} levels, {gd.videos.Count} videos, {gd.deaths.Count} deaths");
            ////Debug.Log($"✅ Addressables configured");
            ////Debug.Log($"🎮 Game ready to play!");
            
            // Open MainMenu scene so it's ready to play
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        }
        
        /// <summary>
        /// Delete all generated prefabs (Character, Enemy_*, Projectile_*, Diamond, Platform).
        /// </summary>
        private static void DeleteGeneratedPrefabs()
        {
            ////Debug.Log("Deleting generated prefabs...");
            
            ProjectilePrefabGenerator.DeleteAll();
            PickupPrefabGenerator.Delete();
            PlatformPrefabGenerator.Delete();
            PlayerPrefabGenerator.Delete();
            EnemyPrefabGenerator.DeleteAll();
        }

        /// <summary>
        /// Delete all generated animation assets (controllers, clips).
        /// Wipes the entire Assets/Animations/ tree to prevent orphans.
        /// Each generator recreates its subfolder during generation.
        /// </summary>
        private static void DeleteGeneratedAnimations()
        {
            string animRoot = "Assets/Animations";
            if (AssetDatabase.IsValidFolder(animRoot))
                AssetDatabase.DeleteAsset(animRoot);
        }
        
        private static void CleanupOldScenes()
        {
            ////Debug.Log("Cleaning up old scenes...");
            
            // Delete old scene folders and individual scenes
            string[] foldersToDelete = {
                "Assets/Scenes/Deaths",
                "Assets/Scenes/LevelS", 
                "Assets/Scenes/VideoS",
                "Assets/Scenes/Levels",
                "Assets/Scenes/Videos"
            };
            
            foreach (string folder in foldersToDelete)
            {
                if (Directory.Exists(folder))
                {
                    AssetDatabase.DeleteAsset(folder);
                    ////Debug.Log($"Deleted folder: {folder}");
                }
            }
            
            // Delete any existing individual scenes
            string[] scenePrefixes = { "Video", "Level", "Death", "Unified" };
            foreach (string prefix in scenePrefixes)
            {
                var sceneGuids = AssetDatabase.FindAssets($"{prefix} t:Scene", new[] { SCENES_ROOT });
                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string sceneName = Path.GetFileNameWithoutExtension(path);
                    
                    // Don't delete menu scenes
                    if (sceneName.Contains("Menu") || sceneName.Contains("Selection") || sceneName.Contains("Shop"))
                        continue;
                        
                    AssetDatabase.DeleteAsset(path);
                    ////Debug.Log($"Deleted old scene: {sceneName}");
                }
            }
        }
        
        // --- Commented out: now delegated to VideoSceneGenerator ---
        // private static void GenerateVideoScene(GameDefinition gd) { ... }
        
        // --- Commented out: now delegated to LevelSceneGenerator ---
        // private static void GenerateLevelScene(GameDefinition gd) { ... }
        
        // --- Commented out: now delegated to DeathSceneGenerator ---
        // private static void GenerateDeathScene(GameDefinition gd) { ... }
        
        private static void SetupAddressables(GameDefinition gd)
        {
            ////Debug.Log("Setting up Addressables...");
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    Debug.LogError("Could not create Addressable Asset Settings!");
                    return;
                }
            }
            
            // Create or get scenes group
            var group = GetOrCreateGroup(settings, "GameScenes");
            
            int entriesAdded = 0;
            
            // Add the 3 simple scenes
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Video.unity", "Video"))
                entriesAdded++;
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Level.unity", "Level"))
                entriesAdded++;
            if (AddSceneToAddressables(settings, group, $"{SCENES_ROOT}/Death.unity", "Death"))
                entriesAdded++;
            
            if (entriesAdded > 0)
            {
                EditorUtility.SetDirty(settings);
                ////Debug.Log($"✅ Added {entriesAdded} scenes to Addressables");
            }
        }
        
        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                ////Debug.Log($"Created Addressables group: {groupName}");
            }
            return group;
        }
        
        private static bool AddSceneToAddressables(AddressableAssetSettings settings, AddressableAssetGroup group, string scenePath, string address)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
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
            ////Debug.Log($"Added to Addressables: {address} -> {scenePath}");
            return true;
        }

        /// <summary>
        /// Ensures a folder exists in the Asset database. Used by scene generators.
        /// </summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            
            AssetDatabase.CreateFolder(parent, folderName);
        }
        
        private static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("No GameDefinition found! Create one via: Assets → Create → Sick Of It → Game Definition");
                return null;
            }
            
            var gd = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            ////Debug.Log($"Found GameDefinition: {gd.levels.Count} levels, {gd.videos.Count} videos, {gd.deaths.Count} deaths");
            return gd;
        }
    }
}
#endif