#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Where each part of GameDefinition lives under Assets/GameConfig.
    ///
    ///   game.json              every GameDefinition field not listed below, plus one ordered list of file
    ///                          names per collection: "levels": ["Level1", "Level2"] means levels/Level1.json, ...
    ///   player.json, ...       one file per large section (see <see cref="Files"/>)
    ///   levels/Level1.json     one file per collection item (see <see cref="Collections"/>)
    ///
    /// List order in game.json is meaningful: GetLevel(n) and GetVideo(n) look items up by position.
    /// Every file starts with "schemaVersion".
    /// </summary>
    public static class GameConfigLayout
    {
        public const string RootFolder = "Assets/GameConfig";
        public const string RootFile = "game.json";
        public const string VersionKey = "schemaVersion";

        public sealed class SplitFile
        {
            public readonly string Field;
            public readonly string File;
            public SplitFile(string field, string file) { Field = field; File = file; }
        }

        public sealed class Collection
        {
            public readonly string Field;
            public readonly string Folder;
            public readonly string KeyField;
            public Collection(string field, string folder, string keyField) { Field = field; Folder = folder; KeyField = keyField; }
            public string FileFor(string id) => Folder + "/" + id + ".json";
        }

        public static readonly SplitFile[] Files =
        {
            new SplitFile("player", "player.json"),
            new SplitFile("pickupConfig", "pickup.json"),
            new SplitFile("mainMenuConfig", "ui/mainMenu.json"),
            new SplitFile("levelSelectionConfig", "ui/levelSelection.json"),
            new SplitFile("videoSelectionConfig", "ui/videoSelection.json"),
            new SplitFile("upgradeShopConfig", "ui/upgradeShop.json"),
            new SplitFile("menuTheme", "ui/theme.json"),
            new SplitFile("audio", "audio.json"),
        };

        public static readonly Collection[] Collections =
        {
            new Collection("levels", "levels", "levelName"),
            new Collection("videos", "videos", "videoName"),
            new Collection("deaths", "deaths", "deathName"),
            new Collection("enemyTypes", "enemies", "type"),
            new Collection("projectileConfigs", "projectiles", "id"),
        };

        /// <summary>
        /// GameDefinition.content holds every file under content/ (entities, sequences, effects) as raw JSON,
        /// parsed at runtime. No manifest: dropping a file in the folder adds it.
        /// </summary>
        public const string ContentField = "content";
        public const string ContentFolder = "content";

        /// <summary>Built by the importer from the content; never written to JSON.</summary>
        public const string RegistryField = "assetRegistry";

        public static SplitFile FileForField(string field) => Files.FirstOrDefault(f => f.Field == field);
        public static Collection CollectionForField(string field) => Collections.FirstOrDefault(c => c.Field == field);

        public static bool IsContentFile(string relativePath) => relativePath.StartsWith(ContentFolder + "/", StringComparison.Ordinal);

        /// <summary>Art specs (art/&lt;id&gt;.json) are read by Tools/Art/art.mjs, not imported (Docs/ArtPipeline.md).</summary>
        public const string ArtFolder = "art";
        public static bool IsArtSpec(string relativePath) => relativePath.StartsWith(ArtFolder + "/", StringComparison.Ordinal);

        /// <summary>Fields of GameDefinition stored outside the root object's own values.</summary>
        public static HashSet<string> SplitFieldNames()
        {
            var names = new HashSet<string>(Files.Select(f => f.Field));
            names.UnionWith(Collections.Select(c => c.Field));
            names.Add(ContentField);
            names.Add(RegistryField);
            return names;
        }

        public static string AbsoluteRoot => Path.GetFullPath(RootFolder);

        public static bool IsConfigAssetPath(string assetPath)
        {
            return assetPath.StartsWith(RootFolder + "/", StringComparison.Ordinal)
                && assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        public static Type ElementType(Collection collection)
        {
            var field = typeof(GameDefinition).GetField(collection.Field);
            UnitySerializationRules.IsCollection(field.FieldType, out var element);
            return element;
        }

        /// <summary>The file name (without .json) an item is exported under: its key field, made filesystem-safe.</summary>
        public static string KeyOf(object item, Collection collection)
        {
            var keyField = item.GetType().GetField(collection.KeyField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return keyField?.GetValue(item)?.ToString() ?? "";
        }

        public static string SafeFileName(string key)
        {
            var chars = key.Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray();
            return new string(chars).Trim('_');
        }

        /// <summary>Throws if this layout no longer matches GameDefinition (a field was renamed or retyped).</summary>
        public static void Validate()
        {
            var fields = UnitySerializationRules.GetSerializedFields(typeof(GameDefinition)).ToDictionary(f => f.Name);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { RootFile };

            foreach (var file in Files)
            {
                if (!fields.TryGetValue(file.Field, out var field))
                    throw new InvalidOperationException($"GameConfigLayout: GameDefinition has no serialized field \"{file.Field}\".");
                if (UnitySerializationRules.IsCollection(field.FieldType, out _) || UnitySerializationRules.IsUnityObject(field.FieldType) || field.FieldType.IsPrimitive)
                    throw new InvalidOperationException($"GameConfigLayout: \"{file.Field}\" must be a serializable class to get its own file.");
                if (!seenPaths.Add(file.File))
                    throw new InvalidOperationException($"GameConfigLayout: \"{file.File}\" is used twice.");
            }

            foreach (var collection in Collections)
            {
                if (!fields.TryGetValue(collection.Field, out var field))
                    throw new InvalidOperationException($"GameConfigLayout: GameDefinition has no serialized field \"{collection.Field}\".");
                if (!UnitySerializationRules.IsCollection(field.FieldType, out var element) || UnitySerializationRules.IsUnityObject(element))
                    throw new InvalidOperationException($"GameConfigLayout: \"{collection.Field}\" must be a list of serializable classes.");
                if (element.GetField(collection.KeyField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null)
                    throw new InvalidOperationException($"GameConfigLayout: {element.Name} has no key field \"{collection.KeyField}\".");
                if (!seenPaths.Add(collection.Folder + "/"))
                    throw new InvalidOperationException($"GameConfigLayout: folder \"{collection.Folder}\" is used twice.");
                if (Files.Any(f => f.File.StartsWith(collection.Folder + "/", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"GameConfigLayout: a single file lives inside collection folder \"{collection.Folder}\".");
            }
        }
    }

    /// <summary>Read access to a set of config files by path relative to the config root ("levels/Level1.json").</summary>
    public interface IConfigFiles
    {
        bool Exists(string relativePath);
        string Read(string relativePath);
        IEnumerable<string> ListAll();
    }

    public sealed class DiskConfigFiles : IConfigFiles
    {
        readonly string root;

        public DiskConfigFiles(string root = null)
        {
            this.root = root ?? GameConfigLayout.AbsoluteRoot;
        }

        public bool Exists(string relativePath) => System.IO.File.Exists(Full(relativePath));
        public string Read(string relativePath) => System.IO.File.ReadAllText(Full(relativePath));

        public IEnumerable<string> ListAll()
        {
            if (!Directory.Exists(root))
                return Enumerable.Empty<string>();
            return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(p => p.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        string Full(string relativePath) => Path.Combine(root, relativePath);
    }

    public sealed class MemoryConfigFiles : IConfigFiles
    {
        readonly IDictionary<string, string> files;

        public MemoryConfigFiles(IDictionary<string, string> files)
        {
            this.files = files;
        }

        public bool Exists(string relativePath) => files.ContainsKey(relativePath);
        public string Read(string relativePath) => files[relativePath];
        public IEnumerable<string> ListAll() => files.Keys.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }
}
#endif
