using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SickOfIt.Json;
using SickOfIt.Tools.Editor.GameConfig;
using Object = UnityEngine.Object;

namespace SickOfIt.Tests.Editor
{
    public class GameConfigTests
    {
        static SortedDictionary<string, string> ExportRealDefinition()
        {
            var issues = new JsonIssueList();
            var files = GameConfigExporter.BuildFiles(GameConfigSync.LoadDefinition(), new EditorAssetIdResolver(), issues);
            Assert.That(issues.HasErrors, Is.False, string.Join("\n", issues));
            return files;
        }

        static JsonIssueList Load(IDictionary<string, string> files)
        {
            var result = GameConfigImporter.Load(new MemoryConfigFiles(files), new EditorAssetIdResolver());
            Object.DestroyImmediate(result.Definition);
            return result.Issues;
        }

        static string Edit(string text, System.Action<JObject> change)
        {
            var obj = JObject.Parse(text);
            change(obj);
            return JsonText.Format(obj);
        }

        [Test]
        public void LayoutMatchesGameDefinition()
        {
            Assert.DoesNotThrow(GameConfigLayout.Validate);
        }

        [Test]
        public void MigrationStepsMatchTheCurrentVersion()
        {
            Assert.That(GameConfigMigrations.Steps.Length, Is.EqualTo(GameConfigMigrations.CurrentVersion - 1));
        }

        [Test]
        public void RealGameDefinitionRoundTripsExactly()
        {
            var report = GameConfigRoundTrip.Run(writeReport: false);
            Assert.That(report.Success, Is.True, report.ToText());
        }

        [Test]
        public void MissingListedFileAndUnlistedFileAreReported()
        {
            var files = ExportRealDefinition();
            string listedLevel = files.Keys.First(k => k.StartsWith("levels/"));
            files.Remove(listedLevel);
            files["levels/Draft.json"] = "{ \"schemaVersion\": 1 }";

            var issues = Load(files);

            Assert.That(issues.Any(i => i.Severity == JsonIssueSeverity.Error && i.Message.Contains(listedLevel)), Is.True, string.Join("\n", issues));
            Assert.That(issues.Any(i => i.Severity == JsonIssueSeverity.Warning && i.File == "levels/Draft.json" && i.Message.Contains("Add \"Draft\"")), Is.True);
        }

        [Test]
        public void BadAssetIdSuggestsTheRealPath()
        {
            var files = ExportRealDefinition();
            string level = files.Keys.First(k => k.StartsWith("levels/"));
            string music = (string)JObject.Parse(files[level])["music"];
            Assume.That(music, Is.Not.Null.And.Not.Empty, "first level has music");

            files[level] = Edit(files[level], o => o["music"] = "Wrong/" + System.IO.Path.GetFileName(music));
            var error = Load(files).First(i => i.Severity == JsonIssueSeverity.Error);

            Assert.That(error.File, Is.EqualTo(level));
            Assert.That(error.Path, Is.EqualTo("music"));
            Assert.That(error.Message, Does.Contain(music));
        }

        [Test]
        public void DuplicateKeysAcrossFilesAreErrors()
        {
            var files = ExportRealDefinition();
            var levels = files.Keys.Where(k => k.StartsWith("levels/")).Take(2).ToList();
            Assume.That(levels.Count, Is.EqualTo(2));
            string firstName = (string)JObject.Parse(files[levels[0]])["levelName"];
            files[levels[1]] = Edit(files[levels[1]], o => o["levelName"] = firstName);

            Assert.That(Load(files).Any(i => i.Severity == JsonIssueSeverity.Error && i.Path == "levelName"), Is.True);
        }

        [Test]
        public void MigrationsUpgradeOldFilesAndRejectBadVersions()
        {
            var steps = new GameConfigMigrations.Step[] { (file, path) => file["renamed"] = file["old"] };
            var issues = new JsonIssueList();

            var old = JObject.Parse("{ \"schemaVersion\": 1, \"old\": 3 }");
            Assert.That(GameConfigMigrations.Upgrade(old, "a.json", issues, steps, 2, out bool changed), Is.True);
            Assert.That(changed, Is.True);
            Assert.That((int)old["schemaVersion"], Is.EqualTo(2));
            Assert.That((int)old["renamed"], Is.EqualTo(3));
            Assert.That(old.Properties().First().Name, Is.EqualTo("schemaVersion"));

            Assert.That(GameConfigMigrations.Upgrade(JObject.Parse("{ }"), "b.json", issues, steps, 2, out _), Is.False);
            Assert.That(GameConfigMigrations.Upgrade(JObject.Parse("{ \"schemaVersion\": 3 }"), "c.json", issues, steps, 2, out _), Is.False);
            Assert.That(issues.ErrorCount, Is.EqualTo(2));
            Assert.That(issues[1].Message, Does.Contain("newer"));
        }
    }
}
