using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// All gameplay content, parsed: entities, sequences, status effects, upgrades, circles, scenes, and level scripts.
    ///
    /// At runtime it is built from the JSON embedded in GameDefinition (or a remote bundle that replaces it),
    /// resolving asset ids through the asset registry. The editor builds it from the source files with line
    /// numbers to validate content on import.
    /// </summary>
    public sealed class GameContent
    {
        public const string EntitiesFolder = "entities/";
        public const string SequencesFolder = "sequences/";
        public const string EffectsFolder = "effects/";
        public const string UpgradesFolder = "upgrades/";
        public const string CirclesFolder = "circles/";
        public const string ScenesFolder = "scenes/";
        public const string WeaponsFolder = "weapons/";
        const string ExtendsKey = "extends";
        const string VersionKey = "schemaVersion";

        public readonly Dictionary<string, EntityDef> Entities = new Dictionary<string, EntityDef>(StringComparer.Ordinal);
        public readonly Dictionary<string, SequenceDef> Sequences = new Dictionary<string, SequenceDef>(StringComparer.Ordinal);
        public readonly Dictionary<string, StatusEffectDef> Effects = new Dictionary<string, StatusEffectDef>(StringComparer.Ordinal);
        public readonly Dictionary<string, UpgradeDef> Upgrades = new Dictionary<string, UpgradeDef>(StringComparer.Ordinal);
        /// <summary>The hero's weapons (content/weapons/{id}.json).</summary>
        public readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>(StringComparer.Ordinal);
        public readonly Dictionary<string, CircleDef> Circles = new Dictionary<string, CircleDef>(StringComparer.Ordinal);
        /// <summary>Scenes (content/scenes/{id}.json): level scripts played on a level's stage instead of its own script.</summary>
        public readonly Dictionary<string, LevelScript> Scenes = new Dictionary<string, LevelScript>(StringComparer.Ordinal);
        /// <summary>Level scripts by level name.</summary>
        public readonly Dictionary<string, LevelScript> LevelScripts = new Dictionary<string, LevelScript>(StringComparer.Ordinal);
        /// <summary>Where each definition came from, for messages.</summary>
        public readonly Dictionary<object, string> SourceFiles = new Dictionary<object, string>();
        public readonly JsonIssueList Issues = new JsonIssueList();

        public IAssetIdResolver Assets { get; private set; }
        public ISpriteSheetSource Sheets { get; private set; }

        /// <summary>A content file to parse: its path under content/ and its JSON.</summary>
        public struct FileSource
        {
            public string Path;
            /// <summary>Path shown in messages (e.g. "content/entities/chaser.json").</summary>
            public string DisplayPath;
            public JToken Json;
        }

        /// <summary>A level script to parse.</summary>
        public struct ScriptSource
        {
            public string LevelName;
            public string DisplayPath;
            public string JsonPath;
            public JToken Json;
        }

        // =====================================================================
        // RUNTIME ACCESS
        // =====================================================================

        static GameContent current;
        static string remoteBundle;

        /// <summary>Content for the running game, built on first use from GameDefinition (and the remote bundle, if loaded).</summary>
        public static GameContent Current
        {
            get
            {
                if (current == null)
                    current = FromDefinition(GameDefinition.Instance, remoteBundle);
                return current;
            }
        }

        /// <summary>Raised after content is rebuilt (hot reload in the editor, or a remote bundle arriving).</summary>
        public static event System.Action Changed;

        /// <summary>Drop the parsed content so the next access rebuilds it (after the config is reimported).</summary>
        public static void Invalidate()
        {
            current = null;
            Changed?.Invoke();
        }

        /// <summary>Use a remote content bundle (see <see cref="RemoteContent"/>) instead of the embedded content.</summary>
        public static void UseRemoteBundle(string json)
        {
            remoteBundle = json;
            Invalidate();
        }

        public static GameContent FromDefinition(GameDefinition definition, string bundleJson = null)
        {
            var registry = new RegistryAssetResolver(definition != null ? definition.assetRegistry : null);
            var files = new List<FileSource>();
            var scripts = new List<ScriptSource>();
            var issues = new JsonIssueList();

            JObject bundle = null;
            if (!string.IsNullOrEmpty(bundleJson))
            {
                bundle = JsonText.Parse(bundleJson, "remote bundle", issues) as JObject;
                if (bundle == null)
                    issues.Error("remote bundle", "", "The remote content bundle is not a JSON object; using built-in content.");
            }

            if (bundle != null && bundle["files"] is JObject remoteFiles)
            {
                foreach (var property in remoteFiles.Properties())
                    files.Add(new FileSource { Path = property.Name, DisplayPath = "remote:" + property.Name, Json = property.Value });
            }
            else if (definition != null)
            {
                foreach (var file in definition.content)
                {
                    var token = JsonText.Parse(file.json, file.path, issues);
                    if (token != null)
                        files.Add(new FileSource { Path = file.path, DisplayPath = "content/" + file.path, Json = token });
                }
            }

            var remoteScripts = bundle?["levelScripts"] as JObject;
            if (definition != null)
            {
                foreach (var level in definition.levels)
                {
                    JToken token = remoteScripts?[level.levelName];
                    string display = token != null ? "remote:" + level.levelName : $"levels/{level.levelName}.json";
                    if (token == null && level.HasScript)
                        token = JsonText.Parse(level.script, display, issues);
                    if (token != null && token.Type != JTokenType.Null)
                        scripts.Add(new ScriptSource { LevelName = level.levelName, DisplayPath = display, JsonPath = "script", Json = token });
                }
            }

            var content = Build(files, scripts, registry, registry);
            content.Issues.InsertRange(0, issues);
            foreach (var issue in content.Issues.Where(i => i.Severity == JsonIssueSeverity.Error).Take(20))
                Debug.LogError("[GameContent] " + issue);
            return content;
        }

        // =====================================================================
        // BUILD
        // =====================================================================

        public static GameContent Build(IEnumerable<FileSource> files, IEnumerable<ScriptSource> scripts, IAssetIdResolver assets, ISpriteSheetSource sheets)
        {
            var content = new GameContent { Assets = assets, Sheets = sheets };
            var serializer = new UnityJsonSerializer(assets);
            var skip = new HashSet<string> { VersionKey, ExtendsKey };

            var entityJson = new Dictionary<string, (JObject json, string display)>(StringComparer.Ordinal);
            foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
            {
                if (!(file.Json is JObject obj))
                {
                    content.Issues.Error(file.DisplayPath, "", "The file must contain a JSON object.");
                    continue;
                }
                string id = IdOf(file.Path);
                if (file.Path.StartsWith(EntitiesFolder, StringComparison.Ordinal))
                {
                    entityJson[id] = (obj, file.DisplayPath);
                }
                else if (file.Path.StartsWith(SequencesFolder, StringComparison.Ordinal))
                {
                    var def = new SequenceDef { id = id };
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Sequences[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else if (file.Path.StartsWith(EffectsFolder, StringComparison.Ordinal))
                {
                    var def = new StatusEffectDef { id = id };
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Effects[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else if (file.Path.StartsWith(UpgradesFolder, StringComparison.Ordinal))
                {
                    var def = new UpgradeDef { id = id };
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Upgrades[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else if (file.Path.StartsWith(WeaponsFolder, StringComparison.Ordinal))
                {
                    var def = new WeaponDef { id = id };
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Weapons[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else if (file.Path.StartsWith(CirclesFolder, StringComparison.Ordinal))
                {
                    var def = new CircleDef { id = id };
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Circles[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else if (file.Path.StartsWith(ScenesFolder, StringComparison.Ordinal))
                {
                    var def = new LevelScript();
                    serializer.PopulateFields(obj, def, skip, content.Issues, file.DisplayPath);
                    content.Scenes[id] = def;
                    content.SourceFiles[def] = file.DisplayPath;
                }
                else
                {
                    content.Issues.Error(file.DisplayPath, "", $"Content files go in {EntitiesFolder}, {SequencesFolder}, {EffectsFolder}, {UpgradesFolder}, {WeaponsFolder}, {CirclesFolder}, or {ScenesFolder}.");
                }
            }

            foreach (var pair in entityJson)
            {
                var merged = Merged(pair.Key, entityJson, content.Issues, new List<string>());
                if (merged == null)
                    continue;
                var def = new EntityDef { id = pair.Key };
                serializer.PopulateFields(merged, def, skip, content.Issues, pair.Value.display);
                content.Entities[pair.Key] = def;
                content.SourceFiles[def] = pair.Value.display;
            }

            foreach (var script in scripts)
            {
                if (!(script.Json is JObject obj))
                {
                    content.Issues.Error(script.DisplayPath, script.JsonPath, "The script must be a JSON object.");
                    continue;
                }
                var def = new LevelScript();
                serializer.PopulateFields(obj, def, null, content.Issues, script.DisplayPath, script.JsonPath);
                content.LevelScripts[script.LevelName] = def;
                content.SourceFiles[def] = script.DisplayPath;
            }

            return content;
        }

        /// <summary>An entity's JSON with its "extends" chain applied: objects merge recursively, everything else replaces.</summary>
        static JObject Merged(string id, Dictionary<string, (JObject json, string display)> all, JsonIssueList issues, List<string> chain)
        {
            var (json, display) = all[id];
            if (chain.Contains(id))
            {
                issues.Error(display, ExtendsKey, $"\"extends\" loops: {string.Join(" -> ", chain)} -> {id}.");
                return null;
            }

            var extendsToken = json[ExtendsKey];
            if (extendsToken == null)
                return json;
            if (extendsToken.Type != JTokenType.String)
            {
                issues.Error(display, ExtendsKey, "\"extends\" must be another entity's id.", JsonText.LineOf(extendsToken));
                return null;
            }

            string parentId = (string)extendsToken;
            if (!all.ContainsKey(parentId))
            {
                issues.Error(display, ExtendsKey, $"No entity \"{parentId}\" to extend.{UnityJsonSerializer.SuggestName(parentId, all.Keys)}", JsonText.LineOf(extendsToken));
                return null;
            }

            chain.Add(id);
            var parent = Merged(parentId, all, issues, chain);
            chain.RemoveAt(chain.Count - 1);
            if (parent == null)
                return null;

            var result = (JObject)parent.DeepClone();
            result.Remove(ExtendsKey);
            MergeInto(result, json);
            result.Remove(ExtendsKey);
            return result;
        }

        static void MergeInto(JObject target, JObject source)
        {
            foreach (var property in source.Properties())
            {
                if (property.Value is JObject sourceObject && target[property.Name] is JObject targetObject
                    && sourceObject["type"] == null)
                    MergeInto(targetObject, sourceObject);
                else
                    target[property.Name] = property.Value.DeepClone();
            }
        }

        public static string IdOf(string path)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 5) : name;
        }

        public string SourceOf(object def) => def != null && SourceFiles.TryGetValue(def, out var path) ? path : "";
    }

    /// <summary>Resolves asset ids at runtime from GameDefinition's asset registry.</summary>
    public sealed class RegistryAssetResolver : IDeferredAssetResolver, ISpriteSheetSource
    {
        readonly Dictionary<string, AssetRegistryEntry> byId = new Dictionary<string, AssetRegistryEntry>(StringComparer.Ordinal);
        readonly Dictionary<UnityEngine.Object, string> idOf = new Dictionary<UnityEngine.Object, string>();
        readonly Dictionary<Texture2D, Sprite[]> sheets = new Dictionary<Texture2D, Sprite[]>();
        readonly List<AssetRegistryEntry> list = new List<AssetRegistryEntry>();

        // A player build (GameAssets.Managed): every reference parsed content makes to the registry, by registry id, so it
        // is filled in when the asset loads and emptied when it is released. Only the newest resolver (the content in use) listens.
        readonly Dictionary<string, List<(string id, Type type, Action<UnityEngine.Object> assign)>> bound
            = new Dictionary<string, List<(string, Type, Action<UnityEngine.Object>)>>(StringComparer.Ordinal);
        static RegistryAssetResolver listening;

        public RegistryAssetResolver(IEnumerable<AssetRegistryEntry> entries)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                list.Add(entry);
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;
                byId[entry.id] = entry;
                Index(entry);
            }
            if (SickOfIt.Core.Addressables.GameAssets.Managed)
            {
                if (listening == null)
                {
                    SickOfIt.Core.Addressables.GameAssets.EntryApplied += path => listening?.Changed(path);
                    SickOfIt.Core.Addressables.GameAssets.EntryCleared += path => listening?.Changed(path);
                }
                listening = this;
            }
        }

        void Index(AssetRegistryEntry entry)
        {
            if (entry.asset != null)
                idOf[entry.asset] = entry.id;
            if (entry.asset is Texture2D texture)
                sheets[texture] = entry.sprites ?? Array.Empty<Sprite>();
        }

        const string RegistryPath = "assetRegistry.Array.data[";

        // A registry entry was loaded or released (GameAssets): re-resolve everything bound to it.
        void Changed(string path)
        {
            if (!path.StartsWith(RegistryPath, StringComparison.Ordinal))
                return;
            int end = path.IndexOf(']', RegistryPath.Length);
            if (end < 0 || !int.TryParse(path.Substring(RegistryPath.Length, end - RegistryPath.Length), out int index) || index >= list.Count)
                return;
            var entry = list[index];
            if (entry == null || string.IsNullOrEmpty(entry.id))
                return;
            Index(entry);
            if (!bound.TryGetValue(entry.id, out var bindings))
                return;
            foreach (var (id, type, assign) in bindings)
                assign(entry.asset != null ? Resolve(id, type, out _) : null);
        }

        public bool Defers(string id, Type fieldType)
        {
            return SickOfIt.Core.Addressables.GameAssets.Managed && byId.ContainsKey(PathOf(id));
        }

        public void Bind(string id, Type fieldType, Action<UnityEngine.Object> assign)
        {
            string pathId = PathOf(id);
            if (!bound.TryGetValue(pathId, out var bindings))
                bound[pathId] = bindings = new List<(string, Type, Action<UnityEngine.Object>)>();
            bindings.Add((id, fieldType, assign));
            if (byId.TryGetValue(pathId, out var entry) && entry.asset != null)
                assign(Resolve(id, fieldType, out _));
        }

        static string PathOf(string id)
        {
            int hash = id.IndexOf('#');
            return hash >= 0 ? id.Substring(0, hash) : id;
        }

        public string GetId(UnityEngine.Object asset, Type fieldType, out string error)
        {
            error = null;
            if (idOf.TryGetValue(asset, out string id))
                return id;
            error = $"\"{asset.name}\" is not in the asset registry.";
            return null;
        }

        public UnityEngine.Object Resolve(string id, Type fieldType, out string error)
        {
            error = null;
            string pathId = id;
            string subName = null;
            int hash = id.IndexOf('#');
            if (hash >= 0)
            {
                pathId = id.Substring(0, hash);
                subName = id.Substring(hash + 1);
            }

            if (!byId.TryGetValue(pathId, out var entry) || entry.asset == null)
            {
                error = $"Asset \"{id}\" isn't in this build. Content can only use assets that content inside the project already uses; add it there and reimport the config.";
                return null;
            }

            if (subName != null)
            {
                if (entry.sprites != null && fieldType.IsAssignableFrom(typeof(Sprite)))
                {
                    foreach (var sprite in entry.sprites)
                    {
                        if (sprite != null && sprite.name == subName)
                            return sprite;
                    }
                }
                error = $"\"{pathId}\" has no {fieldType.Name} named \"{subName}\".";
                return null;
            }

            if (fieldType.IsInstanceOfType(entry.asset))
                return entry.asset;
            if (fieldType == typeof(Sprite) && entry.sprites != null && entry.sprites.Length == 1)
                return entry.sprites[0];
            error = $"\"{pathId}\" is a {entry.asset.GetType().Name}, but this field needs a {fieldType.Name}.";
            return null;
        }

        public string DescribeMissing(UnityEngine.Object missing) => "a missing asset";

        public Sprite[] GetSprites(Texture2D sheet)
        {
            if (sheet == null)
                return Array.Empty<Sprite>();
            if (sheets.TryGetValue(sheet, out var sprites) && sprites.Length > 0)
                return sprites;
            // A sheet loaded on demand (GameAssets) that isn't a registry entry of its own.
            return GameDefinition.LoadedSprites?.Invoke(sheet) ?? Array.Empty<Sprite>();
        }
    }
}
