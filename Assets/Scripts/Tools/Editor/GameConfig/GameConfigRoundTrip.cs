#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using SickOfIt.Data;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Proves the JSON holds everything GameDefinition does: export to memory, import back, and require
    /// Unity's own serialization of both to be identical, and a second export to produce the same files.
    /// Touches no files except the report.
    /// </summary>
    public static class GameConfigRoundTrip
    {
        public static GameConfigReport Run(bool writeReport = true)
        {
            var report = new GameConfigReport("Round trip", "verify that JSON preserves GameDefinition exactly");
            var resolver = new EditorAssetIdResolver();
            var original = GameConfigSync.LoadDefinition();

            var files = GameConfigExporter.BuildFiles(original, resolver, report.Issues);
            if (!report.Issues.HasErrors)
            {
                var result = GameConfigImporter.Load(new MemoryConfigFiles(files), resolver);
                report.Issues.AddRange(result.Issues);
                try
                {
                    if (!result.Issues.HasErrors)
                        Compare(original, result.Definition, files, resolver, report);
                }
                finally
                {
                    Object.DestroyImmediate(result.Definition);
                }
            }

            report.Notes.Add($"{files.Count} files, {files.Values.Sum(t => t.Length)} characters.");
            if (writeReport)
                report.Write();
            return report;
        }

        static void Compare(GameDefinition original, GameDefinition loaded, System.Collections.Generic.SortedDictionary<string, string> files,
            EditorAssetIdResolver resolver, GameConfigReport report)
        {
            loaded.name = original.name;
            loaded.hideFlags = original.hideFlags;

            // Broken references can't be written to JSON and come back as null (each one is a warning).
            // Compare against a copy where they are already null, so anything else that differs is caught.
            var expected = Object.Instantiate(original);
            try
            {
                expected.name = original.name;
                expected.hideFlags = original.hideFlags;
                int broken = ClearBrokenReferences(expected);
                if (broken > 0)
                    report.Notes.Add($"{broken} broken reference(s) become null (see warnings).");

                // The asset registry is rebuilt by every import, not stored in JSON.
                var registry = loaded.assetRegistry;
                expected.assetRegistry = new System.Collections.Generic.List<Data.Config.AssetRegistryEntry>();
                loaded.assetRegistry = new System.Collections.Generic.List<Data.Config.AssetRegistryEntry>();

                string before = EditorJsonUtility.ToJson(expected, true);
                string after = EditorJsonUtility.ToJson(loaded, true);
                if (before != after)
                    report.Issues.Error("", "", "Importing the exported JSON did not reproduce GameDefinition.\n" + FirstDifference(before, after));
                else
                    report.Notes.Add("GameDefinition -> JSON -> GameDefinition is exact.");
                loaded.assetRegistry = registry;
            }
            finally
            {
                Object.DestroyImmediate(expected);
            }

            var again = GameConfigExporter.BuildFiles(loaded, resolver, report.Issues);
            var changed = files.Keys.Union(again.Keys).Where(k => !files.TryGetValue(k, out var a) || !again.TryGetValue(k, out var b) || a != b).ToList();
            if (changed.Count > 0)
                report.Issues.Error("", "", "Exporting twice gave different files: " + string.Join(", ", changed));
            else
                report.Notes.Add("Export is stable.");
        }

        static int ClearBrokenReferences(Object target)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            int count = 0;
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                {
                    property.objectReferenceValue = null;
                    count++;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return count;
        }

        static string FirstDifference(string expected, string actual)
        {
            var a = expected.Split('\n');
            var b = actual.Split('\n');
            int line = 0;
            while (line < a.Length && line < b.Length && a[line] == b[line])
                line++;

            var sb = new StringBuilder();
            sb.AppendLine($"First difference at line {line + 1} of EditorJsonUtility output:");
            for (int i = Math.Max(0, line - 3); i < Math.Min(Math.Max(a.Length, b.Length), line + 4); i++)
            {
                string left = i < a.Length ? a[i] : "<end>";
                string right = i < b.Length ? b[i] : "<end>";
                sb.AppendLine(left == right ? $"    {left}" : $"  - {left}\n  + {right}");
            }
            return sb.ToString();
        }
    }
}
#endif
