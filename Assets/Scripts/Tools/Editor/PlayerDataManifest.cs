using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Json;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// The player build's art and sound, loaded on demand (Core/Addressables GameAssets; AGENTS.md "Art and sound load on
    /// demand"). On the split's copy of GameDefinition (<see cref="PlayerDataSplit"/>, between Begin and End only) every
    /// sheet, sprite and clip reference is emptied and its asset made its own Addressables bundle (address = its project
    /// path, groups PlayerData_Assets_*), and <see cref="GameDefinition.assetManifest"/> records where each was and which
    /// scope needs it: "core" (menus, theme, Shaft, Yooba, and the sounds of audio.json's events: loaded at boot),
    /// "hero" (player.json's states), "level/&lt;name&gt;", "video/&lt;name&gt;", "death/&lt;name&gt;", "sound/&lt;id&gt;",
    /// "asset/&lt;registry id&gt;". A level's scope includes "hero" and every registry asset and sound its script names,
    /// directly or through the content it reaches (entities by id, their extends, loot, spawns, sequences, its Circle; and
    /// every weapon). "scene/&lt;id&gt;" is the same for content/scenes, "codex/&lt;id&gt;" one Codex row's picture.
    /// </summary>
    public static class PlayerDataManifest
    {
        public const string GroupPrefix = PlayerDataSplit.GroupPrefix + "Assets_";

        static readonly Regex Indexed = new Regex(@"^(?<field>[A-Za-z_][A-Za-z0-9_.]*?)\.Array\.data\[(?<index>\d+)\]", RegexOptions.Compiled);

        /// <summary>Empties the split copy's art and sound references and writes its manifest. Returns how many bundles.</summary>
        public static int Write(AddressableAssetSettings settings, AddressableAssetGroup template, Action<string> log)
        {
            var definition = AssetDatabase.LoadAssetAtPath<GameDefinition>(PlayerDataSplit.BuildPath);
            if (definition == null)
                throw new Exception("No GameDefinition at " + PlayerDataSplit.BuildPath);

            var eventSounds = EventSounds(definition);
            var entries = new List<AssetManifestEntry>();
            var scopes = new Dictionary<string, (List<int> entries, HashSet<string> include)>(StringComparer.Ordinal);
            var groups = new Dictionary<string, AddressableAssetGroup>(StringComparer.Ordinal);
            var addressed = new HashSet<string>(StringComparer.Ordinal);
            var coreAddresses = new HashSet<string>(StringComparer.Ordinal);
            var lateAddresses = new HashSet<string>(StringComparer.Ordinal);
            long bytes = 0;
            int foreignCount = 0;

            (List<int> entries, HashSet<string> include) Scope(string id)
            {
                if (!scopes.TryGetValue(id, out var scope))
                    scopes[id] = scope = (new List<int>(), new HashSet<string>(StringComparer.Ordinal));
                return scope;
            }

            void Add(AssetManifestEntry entry, UnityEngine.Object asset, bool foreign = false)
            {
                // An asset in a video, death or level group (Takeable's foreign case) stays where it is under its own address: it is
                // only no longer a hard dependency of GameDefinition, so its group's bundles load with the scope that needs it.
                if (foreign)
                    foreignCount++;
                else if (addressed.Add(entry.address))
                {
                    var group = GroupFor(settings, template, groups, entry.address);
                    settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(entry.address), group).SetAddress(entry.address);
                    bytes += Estimate(entry.address);
                }
                entries.Add(entry);
                string scopeId = ScopeOf(entry.path, definition, eventSounds);
                if (!foreign && scopeId == "core")
                    coreAddresses.Add(entry.address);
                else if (!foreign && scopeId == LateCore)
                    lateAddresses.Add(entry.address);
                Scope(scopeId).entries.Add(entries.Count - 1);
            }

            var serialized = new SerializedObject(definition);
            var prefabRefs = new List<(string path, UnityEngine.Object prefab)>();
            var property = serialized.GetIterator();
            bool enter = true;
            while (property.Next(enter))
            {
                enter = true;
                // Text (the content's JSON, level scripts, this manifest) holds no references: never walked into.
                if (property.propertyType == SerializedPropertyType.String || property.name == PlayerDataSplit.ManifestFieldName
                    || (property.depth == 0 && property.name == "content"))
                {
                    enter = false;
                    continue;
                }
                if (property.isArray && property.propertyType == SerializedPropertyType.Generic && property.arrayElementType == "PPtr<$Sprite>")
                {
                    enter = false;
                    TakeSpriteArray(settings, property, (e, a) => Add(e, a));
                    continue;
                }
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                    continue;
                var value = property.objectReferenceValue;
                if (value is GameObject && PrefabLoadsWithItsScope(property.propertyPath))
                {
                    prefabRefs.Add((property.propertyPath, value)); // decided below, once every sheet is known
                    continue;
                }
                int kind = value is Texture2D ? AssetManifestEntry.Texture : value is Sprite ? AssetManifestEntry.Sprite : value is AudioClip ? AssetManifestEntry.Clip : -1;
                if (kind < 0)
                    continue;
                string path = AssetDatabase.GetAssetPath(value);
                if (!Takeable(settings, path, out string foreignAddress))
                    continue;
                var entry = new AssetManifestEntry { path = property.propertyPath, address = foreignAddress ?? path, kind = kind };
                if (kind == AssetManifestEntry.Texture)
                    entry.sliced = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().Any();
                else if (kind == AssetManifestEntry.Sprite)
                    entry.sprite = value.name;
                property.objectReferenceValue = null;
                Add(entry, value, foreignAddress != null);
            }
            // The hero's and the legacy enemies' prefabs: their animation clips point at big sheets, and a prefab left in GameDefinition
            // loads all of them with it (beta v27: ~45 MB of RGBA32 sheets in ~25 bundles before the menu, and again for the menu hero's
            // own copy in the player data). Each becomes an entry of the scope that plays it, put back into its field then.
            int prefabsTaken = 0;
            foreach (var (path, prefab) in prefabRefs)
            {
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                var field = serialized.FindProperty(path);
                if (field == null || !Takeable(settings, assetPath) || !CarriesBigArt(assetPath, addressed))
                    continue;
                field.objectReferenceValue = null;
                Add(new AssetManifestEntry { path = path, address = assetPath, kind = AssetManifestEntry.Prefab }, prefab);
                prefabsTaken++;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The main menu's own hero (the MainMenu scene's copy is taken out for the player build, PlayerDataSplit.StripMenuHero):
            // the same prefab entry again, in a scope of its own, so the menu loads his bundles without the hero's whole scope.
            int heroPrefab = entries.FindIndex(e => e.kind == AssetManifestEntry.Prefab && e.path == "player.playerPrefab");
            if (heroPrefab >= 0)
                Scope(SickOfIt.Core.Addressables.GameAssets.MenuHero).entries.Add(heroPrefab);

            AddClosures(definition, eventSounds, id => Scope(id).include);

            // The menu's own art and sound: a few shared bundles instead of one request each (142 bundles before the menu).
            // An address the menu needs stays in the boot's bundles even when a later screen uses it too.
            lateAddresses.ExceptWith(coreAddresses);
            int coreBundles = PackCore(settings, template, groups, coreAddresses, "");
            int lateBundles = PackCore(settings, template, groups, lateAddresses, "Late");

            var manifest = new AssetManifest
            {
                entries = entries.ToArray(),
                scopes = scopes.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => new AssetManifestScope
                {
                    id = s.Key,
                    entries = s.Value.entries.ToArray(),
                    include = s.Value.include.OrderBy(i => i, StringComparer.Ordinal).ToArray(),
                }).ToArray(),
            };
            definition.assetManifest = JsonUtility.ToJson(manifest);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            int core = scopes.TryGetValue("core", out var c) ? c.entries.Count : 0;
            log($"  On demand: {foreignCount} references to assets in the video, death and level groups (their audio, thumbnails) left out of GameDefinition's boot closure, loaded by their own addresses with their scope.");
            log($"  On demand: {prefabsTaken} prefabs whose clips use big sheets (the hero's, the legacy enemies', ...) left out of GameDefinition's boot closure.");
            log($"  On demand: {entries.Count} references to {addressed.Count} assets ({bytes / (1024 * 1024)} MB in memory) taken out of GameDefinition, " +
                $"one bundle each in {groups.Count} {GroupPrefix}* groups; {scopes.Count} scopes, {core} references in \"core\" (loaded at boot, packed into {coreBundles} shared bundles; {lateAddresses.Count} more in \"{LateCore}\", {lateBundles} bundles, loaded behind the menu).");
            return addressed.Count;
        }

        /// <summary>A Sprite list or array: one entry for the whole of it when every sprite comes from one sheet, else one per element.</summary>
        static void TakeSpriteArray(AddressableAssetSettings settings, SerializedProperty array, Action<AssetManifestEntry, UnityEngine.Object> add)
        {
            int count = array.arraySize;
            if (count == 0)
                return;
            var sprites = new Sprite[count];
            string sheet = null;
            bool one = true;
            for (int i = 0; i < count; i++)
            {
                sprites[i] = array.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                string path = sprites[i] != null ? AssetDatabase.GetAssetPath(sprites[i]) : null;
                if (path == null || (sheet != null && path != sheet))
                    one = false;
                sheet = sheet ?? path;
            }
            if (one && Takeable(settings, sheet))
            {
                add(new AssetManifestEntry { path = array.propertyPath, address = sheet, kind = AssetManifestEntry.SpriteArray, sprites = sprites.Select(s => s.name).ToArray() }, sprites[0]);
                for (int i = 0; i < count; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = null;
                return;
            }
            for (int i = 0; i < count; i++)
            {
                string path = sprites[i] != null ? AssetDatabase.GetAssetPath(sprites[i]) : null;
                if (path == null || !Takeable(settings, path))
                    continue;
                var element = array.GetArrayElementAtIndex(i);
                add(new AssetManifestEntry { path = element.propertyPath, address = path, kind = AssetManifestEntry.Sprite, sprite = sprites[i].name }, sprites[i]);
                element.objectReferenceValue = null;
            }
        }

        /// <summary>An asset of the project that isn't in the player anyway or in someone else's Addressables group.</summary>
        static bool Takeable(AddressableAssetSettings settings, string path) => Takeable(settings, path, out _, false);

        /// <summary>
        /// As above; with <paramref name="allowForeign"/> an asset that lives in another Addressables group (a video's soundtrack in
        /// Video2_Group, a thumbnail, a death scene's audio) is taken too: <paramref name="foreignAddress"/> is the address it has there.
        /// Left as hard references they made GameDefinition's own load fetch those groups' bundles before the menu (phone-load: level,
        /// death and video groups, 42 bundles against a budget of 12). They are not moved, so labels and addresses other code uses stay.
        /// </summary>
        static bool Takeable(AddressableAssetSettings settings, string path, out string foreignAddress, bool allowForeign = true)
        {
            foreignAddress = null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                return false; // built-in and package assets stay with GameDefinition
            if (path.Contains("/Resources/") || path.Contains("/Editor/"))
                return false;
            var existing = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            // Video and background frame sheets have their own groups and labels; ours (an earlier reference) are fine.
            if (existing == null || existing.parentGroup.Name.StartsWith(GroupPrefix, StringComparison.Ordinal))
                return true;
            if (!allowForeign || string.IsNullOrEmpty(existing.address))
                return false;
            foreignAddress = existing.address;
            return true;
        }

        /// <summary>What every level and scene plays with besides its own content (the old enemy and projectile configs).</summary>
        const string Play = "play";

        /// <summary>
        /// What only a screen after the main menu reads (level and video select cards, the Shaft, Yooba, the shop): loaded behind the
        /// menu once it is up (GameAssets.LoadLateInBackground), awaited before those screens open. The boot's bar covers only "core".
        /// </summary>
        public const string LateCore = "core.late";

        static readonly string[] LatePrefixes = { "shaft.", "yooba.", "upgradeShopConfig.", "levelSelectionConfig.", "videoSelectionConfig.", "player.availableUpgrades." };

        /// <summary>
        /// The GameDefinition fields whose prefab only a level uses (LevelRuntime's fallback hero, legacy enemies, the pickup and platform
        /// prefabs): they load with "hero" / "play" instead of riding with the boot. Menus' and cameras' prefabs stay where they are.
        /// </summary>
        static bool PrefabLoadsWithItsScope(string path) =>
            path == "player.playerPrefab" || path == "pickupPrefab" || path == "platformPrefab" || path.StartsWith("enemyTypes.", StringComparison.Ordinal);

        static readonly string[] SheetExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff" };

        /// <summary>The prefab's dependencies include a sheet the manifest took (or any picture of half a megabyte or more).</summary>
        static bool CarriesBigArt(string prefabPath, HashSet<string> taken)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
            {
                if (taken.Contains(dependency))
                    return true;
                if (SheetExtensions.Contains(Path.GetExtension(dependency).ToLowerInvariant()) && File.Exists(dependency) && new FileInfo(dependency).Length >= 512 * 1024)
                    return true;
            }
            return false;
        }

        static string ScopeOf(string path, GameDefinition definition, HashSet<string> eventSounds)
        {
            if (path == "player.playerPrefab")
                return "hero";
            if (path == "pickupPrefab" || path == "platformPrefab")
                return Play;
            if (path.EndsWith(".thumbnail", StringComparison.Ordinal))
                return LateCore; // the level and video select cards
            foreach (string prefix in LatePrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                    return LateCore;
            }
            if (path.StartsWith("player.animationConfig.states.Array.data[", StringComparison.Ordinal))
                return "hero";
            // The old enemy and projectile configs (the levels without a script, the hero's rocket) only ever play in a level.
            if (path.StartsWith("enemyTypes.", StringComparison.Ordinal) || path.StartsWith("projectileConfigs.", StringComparison.Ordinal))
                return Play;
            var match = Indexed.Match(path);
            if (!match.Success)
                return "core";
            int index = int.Parse(match.Groups["index"].Value);
            switch (match.Groups["field"].Value)
            {
                case "levels": return index < definition.levels.Count ? "level/" + definition.levels[index].levelName : "core";
                case "videos": return index < definition.videos.Count ? "video/" + definition.videos[index].videoName : "core";
                case "deaths": return index < definition.deaths.Count ? "death/" + definition.deaths[index].deathName : "core";
                case "assetRegistry": return index < definition.assetRegistry.Count ? "asset/" + definition.assetRegistry[index].id : "core";
                case "audio.sounds":
                    if (index >= definition.audio.sounds.Count)
                        return "core";
                    string id = definition.audio.sounds[index].id;
                    if (!eventSounds.Contains(id))
                        return "sound/" + id;
                    // The extra takes of a sound (SoundDef.takes) are never needed before the menu's first click: behind the menu.
                    if (path.IndexOf(".takes.", StringComparison.Ordinal) >= 0)
                        return LateCore;
                    // The menu only clicks, hovers and lets its own hero move; the rest of the events (voices, pickups, rewind,
                    // banners, stings) first play in a level, which waits for the late core, so they load behind the menu.
                    return id.StartsWith("ui_", StringComparison.Ordinal) || id.StartsWith("player_", StringComparison.Ordinal) ? "core" : LateCore;
                default: return "core";
            }
        }

        /// <summary>audio.json's events: the sounds the game plays from code all the time (jumps, shots, menu clicks).</summary>
        static HashSet<string> EventSounds(GameDefinition definition)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var events = definition.audio != null ? definition.audio.events : null;
            if (events == null)
                return ids;
            foreach (var field in events.GetType().GetFields())
            {
                if (field.FieldType == typeof(string) && field.GetValue(events) is string id && !string.IsNullOrEmpty(id))
                    ids.Add(id);
            }
            return ids;
        }

        // ─── What each level, scene and Codex row reaches ─────────────────

        static void AddClosures(GameDefinition definition, HashSet<string> eventSounds, Func<string, HashSet<string>> includeOf)
        {
            var issues = new JsonIssueList();
            var files = new List<(string id, string folder, JToken json, List<string> strings)>();
            var byId = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (var file in definition.content)
            {
                var json = JsonText.Parse(file.json, file.path, issues);
                if (json == null)
                    continue;
                string id = Path.GetFileNameWithoutExtension(file.path);
                string folder = file.path.Contains("/") ? file.path.Substring(0, file.path.IndexOf('/')) : "";
                files.Add((id, folder, json, Strings(json)));
                if (!byId.TryGetValue(id, out var list))
                    byId[id] = list = new List<int>();
                list.Add(files.Count - 1);
            }
            var registry = new HashSet<string>(definition.assetRegistry.Where(e => e != null && !string.IsNullOrEmpty(e.id)).Select(e => e.id), StringComparer.Ordinal);
            // Effects load with the level (they must play on the frame); spoken lines are many and long once decoded, so
            // they load on their first play (Sfx asks GameAssets) and the least recently used go past the budget.
            var sounds = new HashSet<string>(definition.audio.sounds.Where(s => s != null && !string.IsNullOrEmpty(s.id) && s.kind != SoundKind.Voice).Select(s => s.id), StringComparer.Ordinal);
            var weapons = Enumerable.Range(0, files.Count).Where(i => files[i].folder == "weapons").ToList();
            // What the game's own systems spawn in any level (the hero's settings, pickups, rewind orbs, style). Not feel (its glow
            // rules only name the entities that glow: the Magma King and the lava pulled the whole of his fight into every level,
            // 187 bundles for the prologue) nor replay (the Daily's modifiers name upgrades, whose icons are in "core").
            var global = new List<string>();
            foreach (object part in new object[] { definition.player, definition.pickupConfig, definition.rewindBudget, definition.style, definition.presentation })
            {
                if (part != null)
                    global.AddRange(Strings(JToken.Parse(JsonUtility.ToJson(part))));
            }

            // A Circle's enemy table is the generator's (LevelDirector.Generator: an entry spawns on floors at or below its
            // fromFloor): a level reaches only the entries that can appear on its floors, not the whole Circle's bestiary.
            var circleEnemies = new Dictionary<int, List<(int fromFloor, List<string> strings)>>();
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].folder != "circles" || !(files[i].json is JObject circle) || !(circle["enemies"] is JArray table))
                    continue;
                var rest = (JObject)circle.DeepClone();
                rest.Remove("enemies");
                files[i] = (files[i].id, files[i].folder, files[i].json, Strings(rest));
                circleEnemies[i] = table.Select(e => ((int?)e["fromFloor"] ?? 0, Strings(e))).ToList();
            }

            void Reach(HashSet<string> include, IEnumerable<string> seeds, IEnumerable<int> alsoFiles, int lowestFloor = 0)
            {
                var seen = new HashSet<int>();
                var queue = new Queue<string>(seeds);
                void Enter(int index)
                {
                    foreach (string s in files[index].strings) queue.Enqueue(s);
                    if (!circleEnemies.TryGetValue(index, out var table))
                        return;
                    foreach (var (fromFloor, strings) in table)
                    {
                        if (lowestFloor <= 0 || fromFloor <= 0 || lowestFloor <= fromFloor)
                            foreach (string s in strings) queue.Enqueue(s);
                    }
                }
                foreach (int index in alsoFiles)
                {
                    if (seen.Add(index))
                        Enter(index);
                }
                while (queue.Count > 0)
                {
                    string s = queue.Dequeue();
                    int hash = s.IndexOf('#');
                    string asset = hash >= 0 ? s.Substring(0, hash) : s;
                    if (registry.Contains(asset))
                        include.Add("asset/" + asset);
                    if (sounds.Contains(s) && !eventSounds.Contains(s))
                        include.Add("sound/" + s);
                    if (byId.TryGetValue(s, out var reached))
                    {
                        foreach (int index in reached)
                        {
                            if (seen.Add(index))
                                Enter(index);
                        }
                    }
                }
            }

            foreach (var level in definition.levels)
            {
                var include = includeOf("level/" + level.levelName);
                include.Add("hero");
                include.Add(Play);
                var seeds = new List<string>();
                int lowest = 0;
                if (level.HasScript)
                {
                    var script = JsonText.Parse(level.script, level.levelName, issues);
                    if (script != null)
                    {
                        seeds.AddRange(Strings(script));
                        // The lowest floor the stage reaches (its floors run from start down to end); 0 = unknown, every entry.
                        int start = (int?)script["floors"]?["start"] ?? 0, end = (int?)script["floors"]?["end"] ?? 0;
                        lowest = start > 0 && end > 0 ? Math.Min(start, end) : 0;
                        // A generated or endless floor rolls the table as it climbs: everything.
                        if (script["generateFloors"] != null || script["replay"] != null)
                            lowest = 0;
                    }
                }
                seeds.AddRange(Strings(JToken.Parse(JsonUtility.ToJson(level))));
                seeds.AddRange(global);
                Reach(include, seeds, weapons, lowest);
            }
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].folder == "scenes")
                {
                    includeOf("scene/" + files[i].id).Add(Play);
                    Reach(includeOf("scene/" + files[i].id), files[i].strings.Concat(global), weapons);
                }
            }

            // One Codex row: an entity's picture (its default state's sheet, or its still sprite; else its base's), a weapon's pickup.
            for (int i = 0; i < files.Count; i++)
            {
                var (id, folder, json, _) = files[i];
                if (folder == "entities")
                {
                    var art = new List<string>();
                    var at = json;
                    for (int depth = 0; depth < 8 && at != null && art.Count == 0; depth++)
                    {
                        Picture(at["visual"], art);
                        string extends = (string)at["extends"];
                        at = extends != null && byId.TryGetValue(extends, out var bases) ? files[bases[0]].json : null;
                    }
                    var include = includeOf("codex/" + id);
                    foreach (string s in art)
                    {
                        int hash = s.IndexOf('#');
                        string asset = hash >= 0 ? s.Substring(0, hash) : s;
                        if (registry.Contains(asset))
                            include.Add("asset/" + asset);
                    }
                }
                else if (folder == "weapons" && json["pickup"] is JValue pickup && pickup.Type == JTokenType.String)
                {
                    includeOf("codex/" + id).Add("codex/" + (string)pickup);
                }
            }
        }

        static void Picture(JToken visual, List<string> art)
        {
            if (!(visual is JObject v))
                return;
            if (v["animator"] is JObject animator && animator["states"] is JArray states && states.Count > 0)
            {
                string wanted = (string)animator["defaultStateName"];
                var state = states.OfType<JObject>().FirstOrDefault(s => (string)s["stateName"] == wanted) ?? states[0] as JObject;
                if (state?["spriteSheet"] is JValue sheet && sheet.Type == JTokenType.String)
                    art.Add((string)sheet);
            }
            if (v["sprite"] is JValue sprite && sprite.Type == JTokenType.String)
                art.Add((string)sprite);
        }

        static List<string> Strings(JToken token)
        {
            var list = new List<string>();
            void Walk(JToken t)
            {
                switch (t)
                {
                    case JValue value when value.Type == JTokenType.String:
                        list.Add((string)value);
                        break;
                    case JContainer container:
                        foreach (var child in container.Children())
                            Walk(child);
                        break;
                }
            }
            Walk(token);
            return list;
        }

        static AddressableAssetGroup GroupFor(AddressableAssetSettings settings, AddressableAssetGroup template, Dictionary<string, AddressableAssetGroup> groups, string path)
        {
            // One group per top folder under Assets (Art, Audio, Videos, ...); a bundle per asset inside it.
            string[] parts = path.Split('/');
            string folder = parts.Length > 2 ? parts[1] : "Root";
            if (groups.TryGetValue(folder, out var group))
                return group;
            group = PlayerDataSplit.NewGroup(settings, GroupPrefix + folder, template);
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            groups[folder] = group;
            return group;
        }

        /// <summary>
        /// Everything the boot loads ("core") goes into shared groups packed together, art and sound apart, each split by
        /// size: the browser fetches a handful of files before the menu instead of one per asset. A core asset a level also
        /// uses is the same bundle (the address is shared), already loaded.
        /// </summary>
        static int PackCore(AddressableAssetSettings settings, AddressableAssetGroup template, Dictionary<string, AddressableAssetGroup> groups, HashSet<string> addresses, string prefix)
        {
            const long ChunkBytes = 12L * 1024 * 1024;
            var chunks = new Dictionary<string, (AddressableAssetGroup group, long size)>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int made = 0;
            foreach (string address in addresses.OrderBy(a => a, StringComparer.Ordinal))
            {
                string kind = address.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || address.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                    || address.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? "Audio" : "Art";
                long size = new FileInfo(address).Length;
                if (!chunks.TryGetValue(kind, out var chunk) || chunk.size + size > ChunkBytes)
                {
                    counts.TryGetValue(kind, out int n);
                    counts[kind] = ++n;
                    var group = PlayerDataSplit.NewGroup(settings, GroupPrefix + "Core" + prefix + "_" + kind + n, template);
                    group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                    groups["Core" + prefix + "_" + kind + n] = group;
                    chunk = (group, 0);
                    made++;
                }
                var packed = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(address), chunk.group);
                packed.SetAddress(address);
                if (prefix.Length == 0)
                {
                    settings.AddLabel(SickOfIt.Core.Addressables.GameAssets.CoreLabel);
                    packed.SetLabel(SickOfIt.Core.Addressables.GameAssets.CoreLabel, true, true);
                }
                chunks[kind] = (chunk.group, chunk.size + size);
            }
            return made;
        }

        static long Estimate(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            long size = asset != null ? UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(asset) : 0;
            return size > 0 ? size : new FileInfo(path).Length;
        }
    }
}
