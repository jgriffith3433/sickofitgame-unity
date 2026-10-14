#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tools.Editor.GameConfig
{
    /// <summary>
    /// Resolves through another resolver and remembers every asset that was used, to build
    /// GameDefinition.assetRegistry (how runtime-parsed content finds its assets).
    /// </summary>
    public sealed class RecordingAssetResolver : IAssetIdResolver
    {
        readonly IAssetIdResolver inner;
        readonly SortedSet<string> used = new SortedSet<string>(StringComparer.Ordinal);

        public RecordingAssetResolver(IAssetIdResolver inner)
        {
            this.inner = inner;
        }

        public string GetId(Object asset, Type fieldType, out string error) => inner.GetId(asset, fieldType, out error);
        public string DescribeMissing(Object missing) => inner.DescribeMissing(missing);

        public Object Resolve(string id, Type fieldType, out string error)
        {
            var asset = inner.Resolve(id, fieldType, out error);
            if (asset != null)
            {
                int hash = id.IndexOf('#');
                used.Add(hash >= 0 ? id.Substring(0, hash) : id);
            }
            return asset;
        }

        public List<AssetRegistryEntry> BuildRegistry()
        {
            var entries = new List<AssetRegistryEntry>();
            foreach (string id in used)
            {
                string path = id.StartsWith("Packages/", StringComparison.Ordinal) ? id : "Assets/" + id;
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main == null)
                    continue;
                entries.Add(new AssetRegistryEntry
                {
                    id = id,
                    asset = main,
                    sprites = EditorSpriteSheets.SpritesAt(path),
                });
            }
            return entries;
        }
    }

    /// <summary>Sprites cut from a sheet, in name order (the order the animation generator uses).</summary>
    public sealed class EditorSpriteSheets : ISpriteSheetSource
    {
        readonly Dictionary<Texture2D, Sprite[]> cache = new Dictionary<Texture2D, Sprite[]>();

        public Sprite[] GetSprites(Texture2D sheet)
        {
            if (sheet == null)
                return Array.Empty<Sprite>();
            if (!cache.TryGetValue(sheet, out var sprites))
            {
                sprites = SpritesAt(AssetDatabase.GetAssetPath(sheet));
                cache[sheet] = sprites;
            }
            return sprites;
        }

        public static Sprite[] SpritesAt(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Array.Empty<Sprite>();
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
#endif
