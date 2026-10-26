using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SickOfIt.Core.Addressables;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Keeps the game's art and sound out of the WebGL player data (SickOfIt.data, which GitHub refuses over 100 MB).
    /// Everything GameDefinition references (sprite sheets, clips, paintings, prefabs) used to ride in the player
    /// because GameDefinition lives in Resources. For a deploy only, <see cref="Begin"/> moves it out of Resources,
    /// makes it addressable (<see cref="GameDefinition.Address"/>), sorts its dependencies into remote Addressables
    /// groups by folder (each kept under <see cref="GroupBudgetBytes"/>) and writes a Boot scene that loads it at
    /// startup (GameDefinitionBoot); <see cref="End"/> puts everything back, so the editor, tests and the committed
    /// project never see the change. <see cref="Recover"/> undoes a split a crashed build left behind.
    /// The found weapons' grip sheets don't ride with GameDefinition at all (<see cref="SplitGripSheets"/>): loaded with it,
    /// every sheet of the art pass at once ran the WebGL page out of memory at boot (v246). They load on demand (PlayerSheets).
    /// Nor does any other art or sound (<see cref="PlayerDataManifest"/>): each is its own bundle, loaded when a level, video,
    /// screen or sound needs it (GameAssets); only "core" loads at boot. <see cref="End"/> puts the original asset back byte
    /// for byte.
    /// </summary>
    public static class PlayerDataSplit
    {
        public const string ResourcesPath = "Assets/Resources/GameDefinition.asset";
        public const string BuildFolder = "Assets/GameDefinitionBuild";
        public const string BuildPath = BuildFolder + "/GameDefinition.asset";
        public const string BootScenePath = BuildFolder + "/Boot.unity";
        public const string GroupPrefix = "PlayerData_";
        public const string GripGroup = GroupPrefix + "HeroGrips";
        public const string ManifestFieldName = "assetManifest";

        // The definition as it was before the split emptied its references (restored by End).
        static string BackupPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "GameDefinition.beforeSplit.asset");

        /// <summary>Estimated in-memory size per group; its bundle comes out at or under this (LZ4).</summary>
        public const long GroupBudgetBytes = 40L * 1024 * 1024;

        // Videos only come in through editor-only fields (the player plays baked frame sheets).
        static readonly string[] SkipExtensions = { ".cs", ".dll", ".asmdef", ".asmref", ".unity", ".mp4", ".mov", ".webm" };

        /// <summary>True while GameDefinition sits in the build folder (between Begin and End).</summary>
        public static bool Active => File.Exists(BuildPath);

        /// <summary>The scenes for the player build: the Boot scene first while split.</summary>
        public static string[] PlayerScenes(string[] scenes) => Active ? new[] { BootScenePath }.Concat(scenes).ToArray() : scenes;

        public static void Begin(Action<string> log)
        {
            Recover(log);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new Exception("Addressables settings are missing.");

            string moved = AssetDatabase.MoveAsset(ResourcesPath, EnsureFolder());
            if (!string.IsNullOrEmpty(moved))
                throw new Exception($"Couldn't move {ResourcesPath} out of Resources: {moved}");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.Copy(BuildPath, BackupPath, true);

            var template = RemoteTemplate(settings);
            var definitionGroup = CreateGroup(settings, GroupPrefix + "GameDefinition", template);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(BuildPath), definitionGroup);
            entry.SetAddress(GameDefinition.Address);

            // Before the dependencies are sorted (and the grips first: they take entries out of the asset registry, whose
            // indexes the manifest records), so what goes on demand never rides with GameDefinition.
            int grips = SplitGripSheets(settings, template, out long gripBytes);
            log($"  On demand: {grips} found-weapon grip sheets ({gripBytes / (1024 * 1024)} MB in memory) left out of GameDefinition, one bundle each in {GripGroup}.");
            PlayerDataManifest.Write(settings, template, log);

            var byFolder = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in AssetDatabase.GetDependencies(BuildPath, true))
            {
                if (!Movable(settings, path))
                    continue;
                string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                if (!byFolder.TryGetValue(folder, out var list))
                    byFolder[folder] = list = new List<string>();
                list.Add(path);
            }

            int groups = 1, assets = 0;
            long total = 0;
            foreach (var pair in byFolder)
            {
                string baseName = GroupPrefix + GroupName(pair.Key);
                AddressableAssetGroup group = null;
                long inGroup = 0;
                int part = 1;
                foreach (string path in pair.Value.OrderBy(p => p, StringComparer.Ordinal))
                {
                    long size = EstimateBytes(path);
                    if (group == null || (inGroup > 0 && inGroup + size > GroupBudgetBytes))
                    {
                        group = CreateGroup(settings, part == 1 ? baseName : baseName + "_" + part, template);
                        part++;
                        groups++;
                        inGroup = 0;
                    }
                    var assetEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                    assetEntry.SetAddress(path);
                    inGroup += size;
                    total += size;
                    assets++;
                }
                EditorUtility.UnloadUnusedAssetsImmediate();
            }

            WriteBootScene();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            log($"  Player data split: GameDefinition and {assets} assets it uses ({total / (1024 * 1024)} MB in memory) in {groups} Addressables groups ({GroupPrefix}*), Boot scene {BootScenePath}.");
        }

        public static void End(Action<string> log)
        {
            // The split's copy had its references emptied (SplitGripSheets, PlayerDataManifest): the original goes back byte
            // for byte (same GUID: its .meta never changed); without the copy, the JSON is imported over it.
            if (File.Exists(BuildPath) && File.Exists(BackupPath))
            {
                File.Copy(BackupPath, BuildPath, true);
                File.Delete(BackupPath);
                AssetDatabase.ImportAsset(BuildPath, ImportAssetOptions.ForceUpdate);
                log?.Invoke("  GameDefinition restored from its copy before the split.");
            }
            else if (File.Exists(BuildPath))
            {
                try
                {
                    if (!GameConfig.GameConfigSync.ImportNow("putting the player data split back").Success)
                        log?.Invoke($"  WARNING: the game config didn't import after the split; {ResourcesPath} may lack the grip sheets until the next import.");
                }
                catch (Exception e)
                {
                    log?.Invoke($"  WARNING: re-importing the game config after the split failed ({e.Message}); the next import restores it.");
                }
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups.Where(g => g != null && g.Name.StartsWith(GroupPrefix, StringComparison.Ordinal)).ToList())
                    settings.RemoveGroup(group);
                EditorUtility.SetDirty(settings);
            }

            if (File.Exists(BuildPath))
            {
                string error = File.Exists(ResourcesPath) ? "both copies exist" : AssetDatabase.MoveAsset(BuildPath, ResourcesPath);
                if (!string.IsNullOrEmpty(error))
                    throw new Exception($"Couldn't move GameDefinition back to {ResourcesPath}: {error}");
            }
            if (AssetDatabase.IsValidFolder(BuildFolder))
                AssetDatabase.DeleteAsset(BuildFolder);
            AssetDatabase.SaveAssets();
            log?.Invoke("  Player data split undone: GameDefinition is back in Resources.");
        }

        /// <summary>Undo a split an earlier (crashed) build left: GameDefinition back in Resources, its groups gone.</summary>
        public static void Recover(Action<string> log)
        {
            bool groupsLeft = AddressableAssetSettingsDefaultObject.Settings?.groups.Any(g => g != null && g.Name.StartsWith(GroupPrefix, StringComparison.Ordinal)) ?? false;
            if (!File.Exists(BuildPath) && !AssetDatabase.IsValidFolder(BuildFolder) && !groupsLeft)
                return;
            log("  An earlier build left the player data split in place; undoing it first.");
            if (File.Exists(BuildPath) && File.Exists(ResourcesPath))
                AssetDatabase.DeleteAsset(BuildPath); // the checkout's copy in Resources is the real one
            End(log);
        }

        /// <summary>
        /// The found weapons' grip states (a weapon with a pickup that isn't the launcher: ShotgunIdle, WhipLash, EquipHalo, ...):
        /// their sheets come out of the split's GameDefinition (state sheet and asset registry) and become addressable as
        /// PlayerSheets.Address(state), one bundle each, so the player loads them when he gets the weapon. A sheet another state or
        /// weapon also uses stays. Only the build's copy changes; <see cref="End"/> imports the JSON back over it.
        /// </summary>
        static int SplitGripSheets(AddressableAssetSettings settings, AddressableAssetGroup template, out long bytes)
        {
            bytes = 0;
            var definition = AssetDatabase.LoadAssetAtPath<GameDefinition>(BuildPath);
            var states = definition != null && definition.player != null && definition.player.animationConfig != null ? definition.player.animationConfig.states : null;
            if (states == null)
                return 0;

            var onDemand = new HashSet<string>(StringComparer.Ordinal);
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var weapon in GameContent.FromDefinition(definition).Weapons.Values)
            {
                var hands = weapon.hands.Built(weapon);
                if (hands == null)
                    continue;
                var names = new List<string> { hands.idle, hands.run, hands.jump, hands.fall, hands.land, hands.runAttack, hands.airAttack, hands.equip };
                names.AddRange(hands.attack);
                bool found = !string.IsNullOrEmpty(weapon.pickup) && !hands.animator && weapon.id != PlayerWeapons.Launcher;
                (found ? onDemand : kept).UnionWith(names.Where(n => !string.IsNullOrEmpty(n)));
            }
            onDemand.ExceptWith(kept);

            var users = new Dictionary<Texture2D, int>();
            foreach (var state in states)
            {
                if (state != null && state.spriteSheet != null)
                    users[state.spriteSheet] = users.TryGetValue(state.spriteSheet, out int n) ? n + 1 : 1;
            }

            AddressableAssetGroup group = null;
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in states)
            {
                if (state == null || state.spriteSheet == null || !onDemand.Contains(state.stateName) || users[state.spriteSheet] != 1)
                    continue;
                string path = AssetDatabase.GetAssetPath(state.spriteSheet);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (group == null)
                {
                    group = CreateGroup(settings, GripGroup, template);
                    group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                }
                bytes += EstimateBytes(path);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).SetAddress(PlayerSheets.Address(state.stateName));
                state.spriteSheet = null;
                taken.Add(path);
            }
            if (taken.Count == 0)
                return 0;
            definition.assetRegistry.RemoveAll(e => e != null && e.asset != null && taken.Contains(AssetDatabase.GetAssetPath(e.asset)));
            EditorUtility.SetDirty(definition);
            return taken.Count;
        }

        static bool Movable(AddressableAssetSettings settings, string path)
        {
            if (path == BuildPath || !path.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path))
                return false; // package assets go into GameDefinition's bundle with it
            if (SkipExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                return false;
            if (path.Contains("/Resources/") || path.Contains("/Editor/"))
                return false; // in the player anyway / never in a build
            // Already addressable (video and background frame sheets): they keep their own groups and labels.
            return settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) == null;
        }

        static long EstimateBytes(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            long size = asset != null ? Profiler.GetRuntimeMemorySizeLong(asset) : 0;
            return size > 0 ? size : new FileInfo(path).Length;
        }

        /// <summary>A remote group like the level ones (ServerData), so the bundles go to Game/v&lt;n&gt;/ServerData.</summary>
        static AddressableAssetGroup RemoteTemplate(AddressableAssetSettings settings)
        {
            return settings.groups.FirstOrDefault(g => g != null && g.Name.StartsWith("Level", StringComparison.Ordinal) && g.HasSchema<BundledAssetGroupSchema>()
                && g.GetSchema<BundledAssetGroupSchema>().LoadPath.GetName(settings) == AddressableAssetSettings.kRemoteLoadPath);
        }

        /// <summary>A remote group like the split's others (PlayerDataManifest's per-folder groups).</summary>
        internal static AddressableAssetGroup NewGroup(AddressableAssetSettings settings, string name, AddressableAssetGroup template) => CreateGroup(settings, name, template);

        static AddressableAssetGroup CreateGroup(AddressableAssetSettings settings, string name, AddressableAssetGroup template)
        {
            var group = template != null
                ? settings.CreateGroup(name, false, false, false, template.Schemas)
                : settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.IncludeInBuild = true;
            if (template == null)
            {
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            }
            return group;
        }

        static string GroupName(string folder)
        {
            string name = folder.StartsWith("Assets/", StringComparison.Ordinal) ? folder.Substring("Assets/".Length) : folder;
            return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }

        static string EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(BuildFolder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(BuildFolder));
            return BuildPath;
        }

        /// <summary>An empty scene with a black camera: GameDefinitionBoot draws its progress bar over it.</summary>
        static void WriteBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                throw new Exception("Couldn't save the Boot scene to " + BootScenePath);
        }
    }
}
