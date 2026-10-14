#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Asset ids for config JSON, resolved through the AssetDatabase.
    ///
    /// An id is the asset's project path without the leading "Assets/", exactly as it appears on disk:
    ///   "Videos/Level1_Background1_Audio.mp3", "Prefabs/Camera.prefab"
    /// Package assets keep their full "Packages/..." path. When a file holds several assets of the field's
    /// type (sprites sliced from one sheet), one is picked by name after a '#':
    ///   "Images/Diamond.png#Diamond_0"
    /// Unity built-in resources can't be referenced.
    /// </summary>
    public sealed class EditorAssetIdResolver : IAssetIdResolver
    {
        const string AssetsPrefix = "Assets/";
        readonly Dictionary<string, Object[]> loaded = new Dictionary<string, Object[]>();

        public string GetId(Object asset, Type fieldType, out string error)
        {
            error = null;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                error = $"\"{asset.name}\" ({asset.GetType().Name}) is not a saved project asset, so it can't be referenced from JSON.";
                return null;
            }
            if (!path.StartsWith(AssetsPrefix, StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                error = $"\"{asset.name}\" is a Unity built-in resource ({path}); reference a project asset instead.";
                return null;
            }

            string id = path.StartsWith(AssetsPrefix, StringComparison.Ordinal) ? path.Substring(AssetsPrefix.Length) : path;
            if (Resolve(id, fieldType, out _) == asset)
                return id;
            string subId = id + "#" + asset.name;
            if (Resolve(subId, fieldType, out _) == asset)
                return subId;

            error = $"\"{path}\" holds more than one {fieldType.Name} named \"{asset.name}\", so this reference can't be written as an id.";
            return null;
        }

        public Object Resolve(string id, Type fieldType, out string error)
        {
            error = null;
            string subName = null;
            string pathId = id;
            int hash = id.IndexOf('#');
            if (hash >= 0)
            {
                subName = id.Substring(hash + 1);
                pathId = id.Substring(0, hash);
            }

            string path = pathId.StartsWith("Packages/", StringComparison.Ordinal) ? pathId : AssetsPrefix + pathId;
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (main == null)
            {
                error = $"No asset at \"{pathId}\" (ids are paths under Assets/, like \"Audio/song.mp3\").{SuggestPath(pathId)}";
                return null;
            }

            var candidates = LoadAll(path).Where(o => o != null && fieldType.IsInstanceOfType(o)).ToList();

            if (subName != null)
            {
                var named = candidates.Where(o => o.name == subName).ToList();
                if (named.Count == 1)
                    return named[0];
                error = named.Count == 0
                    ? $"\"{pathId}\" has no {fieldType.Name} named \"{subName}\".{ListNames(candidates)}"
                    : $"\"{pathId}\" has {named.Count} {fieldType.Name} assets named \"{subName}\".";
                return null;
            }

            if (fieldType.IsInstanceOfType(main))
                return main;
            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count == 0)
            {
                error = $"\"{pathId}\" is a {main.GetType().Name}, but this field needs a {fieldType.Name}.";
                return null;
            }
            error = $"\"{pathId}\" contains {candidates.Count} {fieldType.Name} assets; pick one with \"{pathId}#name\".{ListNames(candidates)}";
            return null;
        }

        public string DescribeMissing(Object missing)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(missing.GetEntityId(), out string guid, out long localId))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                return string.IsNullOrEmpty(path)
                    ? $"a deleted asset (guid {guid})"
                    : $"\"{path}\" object {localId}, which doesn't exist (check the file's import settings)";
            }
            return "an asset that no longer exists";
        }

        Object[] LoadAll(string path)
        {
            if (!loaded.TryGetValue(path, out var all))
            {
                all = AssetDatabase.LoadAllAssetsAtPath(path);
                loaded[path] = all;
            }
            return all;
        }

        static string ListNames(List<Object> candidates)
        {
            if (candidates.Count == 0)
                return "";
            var names = candidates.Select(o => o.name).Distinct().Take(12).ToList();
            return " Available: " + string.Join(", ", names) + (candidates.Count > names.Count ? ", ..." : "") + ".";
        }

        static string SuggestPath(string pathId)
        {
            string fileName = Path.GetFileNameWithoutExtension(pathId);
            if (string.IsNullOrEmpty(fileName))
                return "";

            var matches = AssetDatabase.FindAssets(fileName)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), fileName, StringComparison.OrdinalIgnoreCase))
                .Where(p => p.StartsWith(AssetsPrefix, StringComparison.Ordinal))
                .Select(p => "\"" + p.Substring(AssetsPrefix.Length) + "\"")
                .Distinct()
                .Take(3)
                .ToList();
            return matches.Count == 0 ? "" : " Did you mean " + string.Join(" or ", matches) + "?";
        }
    }
}
#endif
