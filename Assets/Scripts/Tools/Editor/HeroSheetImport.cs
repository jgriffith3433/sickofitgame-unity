using System;
using UnityEditor;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Imports the hero's sprite sheets (Assets/Art/Sprites/Player) at most <see cref="MaxSize"/> px on a side instead of 2048.
    /// The sheets are 3624x4080: capped at 2048 they come out 1819x2048, and 1819 is not a multiple of 4, so neither DXT nor ASTC
    /// can compress them and every one stayed raw RGBA32 (~14.9 MB in memory, 4-5 MB in a bundle; Docs/BuildSize.md). At 2040 they
    /// come out 1812x2040, both multiples of 4, so desktop gets DXT and phones ASTC, at the same on-screen size (sprites keep
    /// their world size when a texture is scaled down). Done in the importer's own pass, so no sheet or .meta file changes (the sheets
    /// are LFS files); a changed <see cref="GetVersion"/> re-imports them. The build's own cap (AddressablesTextureVariants.MaxTextureSize, 2048) is the
    /// larger of the two, so this one wins.
    /// </summary>
    public class HeroSheetImport : AssetPostprocessor
    {
        /// <summary>Largest side of an imported hero sheet: 4080 / 2 = 2040 and 3624 / 2 = 1812, both multiples of 4.</summary>
        public const int MaxSize = 2040;

        const string Folder = "Assets/Art/Sprites/Player/";

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) || !(assetImporter is TextureImporter importer))
                return;
            if (importer.maxTextureSize > MaxSize)
                importer.maxTextureSize = MaxSize;
            // A platform tab with its own override (none today: the metas follow the default) must not undo it.
            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings != null && settings.overridden && settings.maxTextureSize > MaxSize)
                {
                    settings.maxTextureSize = MaxSize;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
        }
    }
}
