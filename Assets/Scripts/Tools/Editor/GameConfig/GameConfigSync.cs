#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Keeps Assets/GameConfig (the source of truth) and GameDefinition.asset (what generators and the game read) in step.
    ///
    ///   JSON changed      -> imported into GameDefinition (on asset refresh, editor start, Generate, Play, and build)
    ///   GameDefinition saved by an editor tool or the inspector -> exported to JSON
    ///
    /// If both changed, the JSON wins: the export is skipped, the JSON is imported, and the report says so.
    /// An import with errors changes nothing; the errors go to the console and Logs/GameConfig.log.
    /// Nothing happens until Assets/GameConfig/game.json exists (created by "Export GameDefinition to JSON").
    /// </summary>
    [InitializeOnLoad]
    public static class GameConfigSync
    {
        public const string DefinitionPath = "Assets/Resources/GameDefinition.asset";
        const string StatePath = "Library/GameConfigSyncState.json";

        static bool busy;
        static bool importQueued;

        public static bool IsBusy => busy;
        public static bool ConfigExists => File.Exists(Path.Combine(GameConfigLayout.AbsoluteRoot, GameConfigLayout.RootFile));

        static GameConfigSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!Application.isBatchMode)
                EditorApplication.delayCall += () => QueueImportIfChanged("editor started or scripts reloaded");
        }

        public static GameDefinition LoadDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<GameDefinition>(DefinitionPath);
            if (definition == null)
                throw new FileNotFoundException($"GameDefinition not found at {DefinitionPath}.");
            return definition;
        }

        // =====================================================================
        // IMPORT (JSON -> GameDefinition)
        // =====================================================================

        public static void QueueImportIfChanged(string reason)
        {
            if (importQueued)
                return;
            importQueued = true;
            EditorApplication.delayCall += () =>
            {
                importQueued = false;
                ImportIfChanged(reason);
            };
        }

        /// <summary>Imports if the JSON changed since the last sync. Returns false only when an import ran and failed.</summary>
        public static bool ImportIfChanged(string reason)
        {
            if (!ConfigExists || busy || !HasUnsyncedChanges())
                return true;
            return ImportNow(reason).Success;
        }

        public static GameConfigReport ImportNow(string reason)
        {
            var report = new GameConfigReport("Import", reason);
            if (!ConfigExists)
            {
                report.Issues.Error(GameConfigLayout.RootFile, "", $"{GameConfigLayout.RootFolder}/{GameConfigLayout.RootFile} doesn't exist. Run SickOfIt > Game Config > Export GameDefinition to JSON first.");
                report.Write();
                return report;
            }

            busy = true;
            GameConfigLoadResult result = null;
            try
            {
                var files = new DiskConfigFiles();
                result = GameConfigImporter.Load(files, new EditorAssetIdResolver());
                report.Issues.AddRange(result.Issues);

                if (!result.Issues.HasErrors)
                {
                    bool changed = GameConfigImporter.ApplyTo(LoadDefinition(), result.Definition);
                    report.Notes.Add(changed ? "GameDefinition updated from JSON." : "GameDefinition already matched the JSON.");
                    if (changed)
                        SickOfIt.Gameplay.GameContent.Invalidate();
                    if (GameplayReferenceGenerator.Write())
                        report.Notes.Add($"Updated {GameplayReferenceGenerator.OutputPath}.");

                    if (result.Migrated)
                    {
                        WriteExport(LoadDefinition(), report);
                        report.Notes.Add($"Files in an older format were upgraded to version {GameConfigMigrations.CurrentVersion}.");
                    }
                    SaveState();
                }
                else
                {
                    report.Notes.Add("GameDefinition was NOT changed. Fix the errors below and save the files again.");
                }
            }
            finally
            {
                if (result?.Definition != null)
                    Object.DestroyImmediate(result.Definition);
                busy = false;
            }

            report.Write();
            return report;
        }

        /// <summary>Loads and checks the JSON without changing GameDefinition.</summary>
        public static GameConfigReport ValidateNow(string reason)
        {
            var report = new GameConfigReport("Validate", reason);
            var result = GameConfigImporter.Load(new DiskConfigFiles(), new EditorAssetIdResolver());
            report.Issues.AddRange(result.Issues);
            if (result.Definition != null)
                Object.DestroyImmediate(result.Definition);
            report.Write();
            return report;
        }

        // =====================================================================
        // EXPORT (GameDefinition -> JSON)
        // =====================================================================

        public static GameConfigReport ExportNow(string reason)
        {
            var report = new GameConfigReport("Export", reason);
            busy = true;
            try
            {
                WriteExport(LoadDefinition(), report);
                if (report.Success)
                    SaveState();
            }
            finally
            {
                busy = false;
            }

            if (!Application.isBatchMode)
                AssetDatabase.Refresh();
            report.Write();
            return report;
        }

        /// <summary>Called just before GameDefinition.asset is saved by anything other than this class.</summary>
        internal static void ExportOnSave()
        {
            if (busy || !ConfigExists)
                return;

            if (HasUnsyncedChanges())
            {
                var report = new GameConfigReport("Export", "GameDefinition was saved");
                report.Issues.Error("", "", "The JSON in Assets/GameConfig changed and hasn't been imported yet, so GameDefinition was not exported over it. " +
                    "The JSON wins: it is being imported now, replacing the GameDefinition edits that were just saved.");
                report.Write();
                QueueImportIfChanged("JSON changed before GameDefinition was saved");
                return;
            }

            var exportReport = new GameConfigReport("Export", "GameDefinition was saved");
            busy = true;
            try
            {
                WriteExport(LoadDefinition(), exportReport);
                if (exportReport.Success)
                    SaveState();
            }
            finally
            {
                busy = false;
            }
            // Files were written directly; let Unity notice them after the save finishes.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += AssetDatabase.Refresh;
            exportReport.Write();
        }

        static void WriteExport(GameDefinition definition, GameConfigReport report)
        {
            var files = GameConfigExporter.BuildFiles(definition, new EditorAssetIdResolver(), report.Issues);
            if (report.Issues.HasErrors)
            {
                report.Notes.Add("No files were written.");
                return;
            }
            GameConfigExporter.WriteFiles(files, out var written, out var deleted);
            report.Notes.Add($"{files.Count} files: {written.Count} written, {deleted.Count} deleted, {files.Count - written.Count} unchanged.");
        }

        // =====================================================================
        // CHANGE TRACKING
        // =====================================================================

        /// <summary>True when the JSON on disk differs from what was last imported or exported.</summary>
        public static bool HasUnsyncedChanges()
        {
            var state = LoadState();
            var current = HashFiles();
            return state == null || state.Count != current.Count || current.Any(pair => !state.TryGetValue(pair.Key, out string hash) || hash != pair.Value);
        }

        static void SaveState()
        {
            var obj = new JObject();
            foreach (var pair in HashFiles())
                obj[pair.Key] = pair.Value;
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
            File.WriteAllText(StatePath, JsonText.Format(obj));
        }

        static Dictionary<string, string> LoadState()
        {
            if (!File.Exists(StatePath))
                return null;
            var obj = JsonText.Parse(File.ReadAllText(StatePath), StatePath, new JsonIssueList()) as JObject;
            return obj?.Properties().ToDictionary(p => p.Name, p => (string)p.Value);
        }

        static Dictionary<string, string> HashFiles()
        {
            var disk = new DiskConfigFiles();
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var sha = SHA1.Create())
            {
                foreach (string file in disk.ListAll())
                    hashes[file] = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(disk.Read(file))));
            }
            return hashes;
        }

        // =====================================================================
        // HOOKS
        // =====================================================================

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;
            if (!ImportIfChanged("entering Play mode"))
            {
                EditorApplication.isPlaying = false;
                Debug.LogError($"Play mode cancelled: the game config has errors. See {GameConfigReport.LogPath}.");
            }
        }
    }

    sealed class GameConfigPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (GameConfigSync.IsBusy)
                return;
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(GameConfigLayout.IsConfigAssetPath))
                GameConfigSync.QueueImportIfChanged("JSON files changed");
        }
    }

    sealed class GameConfigSaveHook : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (Array.IndexOf(paths, GameConfigSync.DefinitionPath) >= 0)
                GameConfigSync.ExportOnSave();
            return paths;
        }
    }

    sealed class GameConfigBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!GameConfigSync.ImportIfChanged("building the player"))
                throw new BuildFailedException($"The game config has errors. See {GameConfigReport.LogPath}.");
        }
    }
}
#endif
