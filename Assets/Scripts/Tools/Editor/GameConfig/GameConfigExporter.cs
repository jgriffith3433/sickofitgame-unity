#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>Turns a GameDefinition into the config JSON file set.</summary>
    public static class GameConfigExporter
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Builds every config file in memory, keyed by path relative to the config root. Touches no files.</summary>
        public static SortedDictionary<string, string> BuildFiles(GameDefinition definition, IAssetIdResolver resolver, JsonIssueList issues)
        {
            GameConfigLayout.Validate();
            var serializer = new UnityJsonSerializer(resolver);
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var root = new JObject { [GameConfigLayout.VersionKey] = GameConfigMigrations.CurrentVersion };

            foreach (var field in UnitySerializationRules.GetSerializedFields(typeof(GameDefinition)))
            {
                var split = GameConfigLayout.FileForField(field.Name);
                if (split != null)
                {
                    object value = field.GetValue(definition) ?? UnityJsonSerializer.CreateInstance(field.FieldType);
                    files[split.File] = Versioned(serializer.WriteFields(value, null, issues, split.File));
                    continue;
                }

                var collection = GameConfigLayout.CollectionForField(field.Name);
                if (collection != null)
                {
                    root[field.Name] = WriteCollection(definition, field.GetValue(definition) as IList, collection, serializer, files, issues);
                    continue;
                }

                if (field.Name == GameConfigLayout.RegistryField)
                    continue;
                if (field.Name == GameConfigLayout.ContentField)
                {
                    WriteContent(definition, files, issues);
                    continue;
                }

                root[field.Name] = serializer.WriteValue(field.GetValue(definition), field.FieldType, issues, GameConfigLayout.RootFile, field.Name);
            }

            files[GameConfigLayout.RootFile] = JsonText.Format(root);
            return files;
        }

        static void WriteContent(GameDefinition definition, SortedDictionary<string, string> files, JsonIssueList issues)
        {
            if (definition.content == null)
                return;
            foreach (var file in definition.content)
            {
                if (file == null || string.IsNullOrEmpty(file.path))
                    continue;
                string path = GameConfigLayout.ContentFolder + "/" + file.path;
                var token = JsonText.Parse(file.json, path, issues);
                if (token != null)
                    files[path] = JsonText.Format(token);
            }
        }

        static JArray WriteCollection(GameDefinition definition, IList items, GameConfigLayout.Collection collection,
            UnityJsonSerializer serializer, SortedDictionary<string, string> files, JsonIssueList issues)
        {
            var manifest = new JArray();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var elementType = GameConfigLayout.ElementType(collection);
            int count = items?.Count ?? 0;

            for (int i = 0; i < count; i++)
            {
                object item = items[i] ?? UnityJsonSerializer.CreateInstance(elementType);
                string id = UniqueId(GameConfigLayout.SafeFileName(GameConfigLayout.KeyOf(item, collection)), collection, i, used);
                manifest.Add(id);

                string file = collection.FileFor(id);
                files[file] = Versioned(serializer.WriteFields(item, null, issues, file));
            }
            return manifest;
        }

        static string UniqueId(string key, GameConfigLayout.Collection collection, int index, HashSet<string> used)
        {
            string id = string.IsNullOrEmpty(key) ? $"{collection.Folder}_{index + 1}" : key;
            string candidate = id;
            for (int n = 2; !used.Add(candidate); n++)
                candidate = $"{id}_{n}";
            return candidate;
        }

        static string Versioned(JObject body)
        {
            var file = new JObject { [GameConfigLayout.VersionKey] = GameConfigMigrations.CurrentVersion };
            foreach (var property in body.Properties())
                file.Add(property.Name, property.Value);
            return JsonText.Format(file);
        }

        /// <summary>
        /// Writes <paramref name="files"/> under the config root, touching only files whose text changed.
        /// Item files the previous game.json listed but this export doesn't (renamed or removed items) are deleted.
        /// Files never listed in game.json, like a new level still being written, are left alone.
        /// </summary>
        public static void WriteFiles(IDictionary<string, string> files, out List<string> written, out List<string> deleted)
        {
            string root = GameConfigLayout.AbsoluteRoot;
            var previous = PreviouslyManagedFiles(new DiskConfigFiles(root));

            written = new List<string>();
            foreach (var pair in files)
            {
                string path = Path.Combine(root, pair.Key);
                if (File.Exists(path) && File.ReadAllText(path) == pair.Value)
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, pair.Value, Utf8NoBom);
                written.Add(pair.Key);
            }

            deleted = new List<string>();
            foreach (string relative in previous)
            {
                if (files.ContainsKey(relative))
                    continue;
                string path = Path.Combine(root, relative);
                if (!File.Exists(path))
                    continue;
                File.Delete(path);
                if (File.Exists(path + ".meta"))
                    File.Delete(path + ".meta");
                deleted.Add(relative);
            }
        }

        static HashSet<string> PreviouslyManagedFiles(IConfigFiles disk)
        {
            var managed = new HashSet<string>(StringComparer.Ordinal);
            if (!disk.Exists(GameConfigLayout.RootFile))
                return managed;

            var root = JsonText.Parse(disk.Read(GameConfigLayout.RootFile), GameConfigLayout.RootFile, new JsonIssueList()) as JObject;
            if (root == null)
                return managed;

            foreach (var collection in GameConfigLayout.Collections)
            {
                if (!(root[collection.Field] is JArray list))
                    continue;
                foreach (var id in list.Where(t => t.Type == JTokenType.String))
                    managed.Add(collection.FileFor((string)id));
            }
            return managed;
        }
    }
}
#endif
