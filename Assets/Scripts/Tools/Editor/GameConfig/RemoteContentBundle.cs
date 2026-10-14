#if UNITY_EDITOR
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using SickOfIt.Gameplay;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Packs the current gameplay content and level scripts into one JSON file that a shipped build can download
    /// (see RemoteContent). Upload it to GameDefinition.remoteContentUrl ("remoteContentUrl" in game.json).
    /// Only content changes travel this way; new art, audio, or code still needs a build.
    /// </summary>
    public static class RemoteContentBundle
    {
        public const string DefaultPath = "Builds/RemoteContent/content.json";

        [MenuItem("SickOfIt/Game Config/Build Remote Content Bundle", priority = 190)]
        static void Menu()
        {
            var report = Build(DefaultPath);
            if (report.Success)
                EditorUtility.RevealInFinder(DefaultPath);
        }

        public static GameConfigReport Build(string path)
        {
            var report = new GameConfigReport("Build bundle", "pack content and level scripts for remote delivery");
            if (!GameConfigSync.ImportIfChanged("building the remote content bundle"))
            {
                report.Issues.Error("", "", "The config has errors; fix them first (see the import report).");
                report.Write();
                return report;
            }

            var definition = GameConfigSync.LoadDefinition();
            var files = new JObject();
            foreach (var file in definition.content)
                files[file.path] = JToken.Parse(file.json);
            var scripts = new JObject();
            foreach (var level in definition.levels)
            {
                if (level.HasScript)
                    scripts[level.levelName] = JToken.Parse(level.script);
            }
            var bundle = new JObject
            {
                ["schemaVersion"] = GameConfigMigrations.CurrentVersion,
                ["files"] = files,
                ["levelScripts"] = scripts,
            };
            string text = JsonText.Compact(bundle);

            // Check it the way a build will: parsed at runtime against the build's asset registry.
            var content = GameContent.FromDefinition(definition, text);
            ContentValidator.Validate(content, content.Issues);
            report.Issues.AddRange(content.Issues);
            if (report.Success)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
                report.Notes.Add($"Wrote {path} ({text.Length / 1024f:0.0} KB): {files.Count} content files, {scripts.Count} level scripts. " +
                                 "Upload it to the URL in game.json \"remoteContentUrl\".");
            }
            report.Write();
            return report;
        }
    }
}
#endif
