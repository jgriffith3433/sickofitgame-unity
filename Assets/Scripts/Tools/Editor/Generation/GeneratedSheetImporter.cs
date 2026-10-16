#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Slices sprite sheets made by the art pipeline (Tools/Art/art.mjs, Docs/ArtPipeline.md). A sheet
    /// "X.png" with a "X.sheet.json" beside it is imported as multiple sprites with the sidecar's rects,
    /// pixels per unit and pivot (the middle of the standing body, the same point in every clip), so the
    /// pipeline never has to open Unity to set import settings.
    /// </summary>
    public sealed class GeneratedSheetImporter : AssetPostprocessor
    {
        [Serializable]
        sealed class Sidecar
        {
            public float pixelsPerUnit = 100f;
            public float[] pivot = { 0.5f, 0.5f };
            public Cell[] sprites = Array.Empty<Cell>();
        }

        [Serializable]
        sealed class Cell
        {
            public string name;
            public int x, y, w, h;
        }

        public static string SidecarPath(string pngPath) => Path.ChangeExtension(pngPath, ".sheet.json");

        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;
            string sidecarPath = SidecarPath(assetPath);
            if (!File.Exists(sidecarPath))
                return;

            Sidecar sheet;
            try
            {
                sheet = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidecarPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[GeneratedSheetImporter] {sidecarPath}: {e.Message}");
                return;
            }
            if (sheet?.sprites == null || sheet.sprites.Length == 0)
                return;

            var importer = (TextureImporter)assetImporter;
            var pivot = sheet.pivot != null && sheet.pivot.Length == 2 ? new Vector2(sheet.pivot[0], sheet.pivot[1]) : new Vector2(0.5f, 0.5f);
            var metas = new SpriteMetaData[sheet.sprites.Length];
            for (int i = 0; i < metas.Length; i++)
            {
                var c = sheet.sprites[i];
                metas[i] = new SpriteMetaData
                {
                    name = c.name,
                    rect = new Rect(c.x, c.y, c.w, c.h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot,
                };
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = sheet.pixelsPerUnit > 0f ? sheet.pixelsPerUnit : 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritesheet = metas;
        }

        /// <summary>A changed sidecar re-slices its sheet.</summary>
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.EndsWith(".sheet.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                string png = path.Substring(0, path.Length - ".sheet.json".Length) + ".png";
                if (File.Exists(png))
                    AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
#endif
