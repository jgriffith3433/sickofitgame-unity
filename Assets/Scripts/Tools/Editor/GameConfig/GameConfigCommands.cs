#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Menu items and command-line entry points for the game config.
    ///
    /// Command line (the editor must be closed; exit code 0 = success, 1 = config errors, 2 = crash):
    ///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; -logFile - -executeMethod SickOfIt.Tools.Editor.GameConfig.GameConfigCommands.Validate
    /// Methods: Validate, Import, Export, VerifyRoundTrip. Each writes Logs/GameConfig.log.
    /// </summary>
    public static class GameConfigCommands
    {
        const string Menu = "SickOfIt/Game Config/";

        [MenuItem(Menu + "Import JSON into GameDefinition", priority = 100)]
        static void ImportMenu() => GameConfigSync.ImportNow("menu");

        [MenuItem(Menu + "Validate JSON", priority = 101)]
        static void ValidateMenu() => GameConfigSync.ValidateNow("menu");

        [MenuItem(Menu + "Export GameDefinition to JSON", priority = 120)]
        static void ExportMenu()
        {
            if (GameConfigSync.ConfigExists && GameConfigSync.HasUnsyncedChanges() &&
                !EditorUtility.DisplayDialog("Export GameDefinition to JSON",
                    "The JSON in Assets/GameConfig has changes that haven't been imported. Exporting will overwrite them with GameDefinition's current values.",
                    "Overwrite JSON", "Cancel"))
                return;
            GameConfigSync.ExportNow("menu");
        }

        [MenuItem(Menu + "Verify Round Trip", priority = 140)]
        static void RoundTripMenu() => GameConfigRoundTrip.Run();

        [MenuItem(Menu + "Open Report", priority = 160)]
        static void OpenReport() => EditorUtility.OpenWithDefaultApp(GameConfigReport.LogPath);

        public static void Validate() => Run(() => GameConfigSync.ValidateNow("command line"));
        public static void Import() => Run(() => GameConfigSync.ImportNow("command line"));
        public static void Export() => Run(() => GameConfigSync.ExportNow("command line"));
        public static void VerifyRoundTrip() => Run(() => GameConfigRoundTrip.Run());
        public static void ConvertLegacy() => Run(LegacyContentConverter.Convert);

        public static void GenerateReference() => Run(() =>
        {
            var report = new GameConfigReport("Generate reference", "command line");
            report.Notes.Add(GameplayReferenceGenerator.Write() ? $"Wrote {GameplayReferenceGenerator.OutputPath}." : "Reference already up to date.");
            report.Write();
            return report;
        });

        public static void BuildContentBundle() => Run(() => RemoteContentBundle.Build(RemoteContentBundle.DefaultPath));

        static void Run(Func<GameConfigReport> operation)
        {
            int exitCode;
            try
            {
                var report = operation();
                Debug.Log(report.ToText());
                exitCode = report.Success ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 2;
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
#endif
