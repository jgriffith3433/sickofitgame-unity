#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.VideoFrameExtractor.Editor;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Packs one authoring mp4 into the addressable sprite-sheet sequence the game already plays.
    /// Sheets are overwritten in place so existing texture guids stay attached to their addressable entries.
    /// </summary>
    public static class VideoSpriteSheetBaker
    {
        const int DefaultFramesPerChunk = 66;
        const string VideoFolder = "Assets/Videos";

        class Slot
        {
            public ScriptableObject Current;
            public Action<SpriteFramesData> Set;
        }

        public static bool IsSequence(string group)
        {
            return group == "Videos" || group == "Deaths" || group == "Backgrounds";
        }

        public static void Annotate(GameDefinition gd, SourceMediaEntry entry)
        {
            if (entry == null)
                return;

            entry.SheetsBuilt = false;
            entry.SheetDetail = null;
            if (!IsSequence(entry.Group))
                return;

            var data = FindData(gd, entry.Label, out _);
            if (data == null)
            {
                entry.SheetDetail = "No sprite sheets yet.";
                return;
            }

            string folder = ExistingFolder(data);
            int packed = string.IsNullOrEmpty(folder) ? 0 : CountSheets(folder, data.name);
            entry.SheetsBuilt = data.HasBuiltFrames;
            if (entry.SheetsBuilt)
                entry.SheetDetail = packed + " sprite sheet" + (packed == 1 ? "" : "s") + ".";
            else if (packed > 0)
                entry.SheetDetail = packed + " sheet file" + (packed == 1 ? "" : "s") + " on disk, not chunked yet.";
            else
                entry.SheetDetail = "No sprite sheets yet.";
        }

        public static void Regenerate(GameDefinition gd, string clipId, string mp4Path)
        {
            if (gd == null)
            {
                EditorUtility.DisplayDialog("Sprite Sheets", "No GameDefinition found.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(clipId) || string.IsNullOrEmpty(mp4Path) || !File.Exists(mp4Path))
            {
                EditorUtility.DisplayDialog("Sprite Sheets", "The source mp4 is missing.\n\n" + mp4Path, "OK");
                return;
            }

            if (!FFmpegHelper.IsFFmpegAvailable())
            {
                EditorUtility.DisplayDialog(
                    "FFmpeg Not Found",
                    "FFmpeg was not found.\n\nInstall it with:\nwinget install ffmpeg\n\nThen restart Unity.",
                    "OK");
                return;
            }

            var data = FindData(gd, clipId, out Slot slot);
            bool replacing = data != null && data.HasBuiltFrames;
            string verb = replacing ? "Replace the sprite sheets for " : "Build sprite sheets for ";
            if (!EditorUtility.DisplayDialog("Sprite Sheets", verb + clipId + "?\n\n" + mp4Path, replacing ? "Replace" : "Build", "Cancel"))
                return;

            string tempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "VideoFrames", clipId));
            bool created = false;
            bool committed = false;
            SpriteFramesData backup = null;

            try
            {
                EditorUtility.DisplayProgressBar("Sprite Sheets", "Extracting " + clipId, 0.02f);
                float fps = data != null && data.playbackFPS > 0f ? data.playbackFPS : 30f;
                if (!Extract(mp4Path, fps, tempDir, out string extractError))
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", extractError, "OK");
                    return;
                }

                var frames = Directory.GetFiles(tempDir, "frame_*.jpg");
                Array.Sort(frames, StringComparer.Ordinal);
                if (frames.Length == 0)
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", "ffmpeg wrote no frames.", "OK");
                    return;
                }

                if (!TryReadSize(frames[0], out int srcW, out int srcH))
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", "Could not read the first frame.", "OK");
                    return;
                }

                if (data == null)
                {
                    data = CreateAsset(clipId);
                    created = true;
                }

                backup = ScriptableObject.CreateInstance<SpriteFramesData>();
                EditorUtility.CopySerialized(data, backup);

                if (data.playbackFPS <= 0f)
                    data.playbackFPS = fps;
                if (string.IsNullOrEmpty(data.videoName))
                    data.videoName = string.IsNullOrEmpty(data.name) ? clipId : data.name;

                if (!GridMatches(data, srcW, srcH))
                {
                    data.sourceFrameWidth = srcW;
                    data.sourceFrameHeight = srcH;
                    if (data.maxSheetSize <= 0)
                        data.maxSheetSize = 2048;
                    data.CalculateOptimalGrid();
                }

                if (data.framesPerSheet <= 0 || data.frameWidth <= 0 || data.frameHeight <= 0)
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", "Could not fit frames into a sprite sheet.", "OK");
                    return;
                }

                int framesPerChunk = FramesPerChunk(data);
                string folder = ResolveFolder(data);
                string prefix = data.name;
                int previousSheets = CountSheets(folder, prefix);

                if (!PackSheets(frames, data, folder, prefix, out string packError))
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", packError ?? "Cancelled.", "OK");
                    return;
                }

                int sheetCount = Mathf.CeilToInt((float)frames.Length / data.framesPerSheet);
                AssetDatabase.Refresh();
                ApplyImportSettings(folder, prefix, sheetCount, data.sheetTextureWidth, data.sheetTextureHeight);

                var textures = LoadSheets(folder, prefix, sheetCount, out string loadError);
                if (textures == null)
                {
                    EditorUtility.DisplayDialog("Sprite Sheets", loadError, "OK");
                    return;
                }

                Undo.RecordObject(data, "Build " + clipId + " sprite sheets");
                Undo.RecordObject(gd, "Build " + clipId + " sprite sheets");
                BuildChunks(data, textures, frames.Length, framesPerChunk);
                string groupName = ApplyAddressables(data, textures, out string addressNote);
                DeleteExtraSheets(folder, prefix, sheetCount, previousSheets);
                if (slot != null && slot.Current == null)
                    slot.Set(data);

                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(gd);
                AssetDatabase.SaveAssets();
                committed = true;

                string summary =
                    "Packed " + frames.Length + " frames into " + sheetCount + " sprite sheets (" +
                    data.sheetTextureWidth + "x" + data.sheetTextureHeight + ").\n" +
                    data.chunks.Count + " chunks in " + groupName + ".\n" +
                    folder + "\nExtracted at " + fps.ToString("0.##") + " fps." + addressNote;
                Debug.Log("[VideoSpriteSheetBaker] " + clipId + "\n" + summary);
                EditorUtility.DisplayDialog("Sprite Sheets", summary, "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Sprite Sheets", e.Message, "OK");
            }
            finally
            {
                if (!committed && data != null && backup != null)
                    EditorUtility.CopySerialized(backup, data);
                if (backup != null)
                    UnityEngine.Object.DestroyImmediate(backup);

                if (!committed && created && data != null)
                {
                    string folder = ExistingFolder(data);
                    bool wroteSheets = !string.IsNullOrEmpty(folder) && CountSheets(folder, data.name) > 0;
                    string createdPath = AssetDatabase.GetAssetPath(data);
                    if (!wroteSheets && !string.IsNullOrEmpty(createdPath))
                        AssetDatabase.DeleteAsset(createdPath);
                }

                EditorUtility.ClearProgressBar();
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (Exception e) { Debug.LogWarning("[VideoSpriteSheetBaker] Could not delete " + tempDir + ": " + e.Message); }
                }
            }
        }

        static bool Extract(string mp4Path, float fps, string tempDir, out string error)
        {
            error = null;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            bool ok = false;
            string message = null;
            FFmpegHelper.ExtractFrames(mp4Path, fps, 0, tempDir, (success, msg) =>
            {
                ok = success;
                message = msg;
            });

            if (!ok)
                error = message ?? "ffmpeg failed.";
            return ok;
        }

        static int FramesPerChunk(SpriteFramesData data)
        {
            if (data.chunks != null && data.chunks.Count > 0 && data.chunks[0].frameCount > 0)
                return data.chunks[0].frameCount;
            return DefaultFramesPerChunk;
        }

        static bool PackSheets(string[] frames, SpriteFramesData data, string folder, string prefix, out string error)
        {
            error = null;
            int cols = data.sheetColumns;
            int rows = data.sheetRows;
            int perSheet = data.framesPerSheet;
            int fw = data.frameWidth;
            int fh = data.frameHeight;
            int sheetWidth = data.sheetTextureWidth;
            int sheetHeight = data.sheetTextureHeight;
            int totalSheets = Mathf.CeilToInt((float)frames.Length / perSheet);
            string fullFolder = Path.Combine(Directory.GetCurrentDirectory(), folder);
            Directory.CreateDirectory(fullFolder);

            Color[] clear = new Color[sheetWidth * sheetHeight];
            for (int s = 0; s < totalSheets; s++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Sprite Sheets",
                        prefix + " sheet " + (s + 1) + " / " + totalSheets,
                        (float)s / totalSheets))
                {
                    error = "Stopped early. Run the build again to finish " + prefix + ".";
                    return false;
                }

                var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
                try
                {
                    Array.Clear(clear, 0, clear.Length);
                    sheet.SetPixels(clear);

                    int framesInSheet = Mathf.Min(perSheet, frames.Length - s * perSheet);
                    for (int f = 0; f < framesInSheet; f++)
                    {
                        Texture2D source = null;
                        Texture2D scaled = null;
                        try
                        {
                            source = LoadFrame(frames[s * perSheet + f]);
                            if (source == null)
                            {
                                error = "Could not read " + frames[s * perSheet + f];
                                return false;
                            }

                            scaled = Scale(source, fw, fh);
                            int col = f % cols;
                            int row = f / cols;
                            int x = col * fw;
                            int y = sheetHeight - (row + 1) * fh;
                            sheet.SetPixels(x, y, fw, fh, scaled.GetPixels());
                        }
                        finally
                        {
                            if (scaled != null)
                                UnityEngine.Object.DestroyImmediate(scaled);
                            if (source != null)
                                UnityEngine.Object.DestroyImmediate(source);
                        }
                    }

                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(fullFolder, prefix + "_sheet_" + s.ToString("D4") + ".png"), sheet.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sheet);
                }
            }

            return true;
        }

        static Texture2D LoadFrame(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }

            return tex;
        }

        static bool TryReadSize(string path, out int width, out int height)
        {
            var tex = LoadFrame(path);
            if (tex == null)
            {
                width = 0;
                height = 0;
                return false;
            }

            width = tex.width;
            height = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            return true;
        }

        static Texture2D Scale(Texture source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        static void ApplyImportSettings(string folder, string prefix, int sheetCount, int sheetWidth, int sheetHeight)
        {
            int maxSize = Mathf.Max(sheetWidth, sheetHeight);
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int s = 0; s < sheetCount; s++)
                {
                    string assetPath = folder + "/" + prefix + "_sheet_" + s.ToString("D4") + ".png";
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                        continue;

                    bool dirty =
                        importer.textureType != TextureImporterType.Default ||
                        importer.isReadable ||
                        importer.mipmapEnabled ||
                        importer.maxTextureSize != maxSize ||
                        importer.textureCompression != TextureImporterCompression.Compressed ||
                        importer.npotScale != TextureImporterNPOTScale.None ||
                        importer.filterMode != FilterMode.Bilinear;
                    if (!dirty)
                        continue;

                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = false;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = maxSize;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static List<Texture2D> LoadSheets(string folder, string prefix, int sheetCount, out string error)
        {
            error = null;
            var textures = new List<Texture2D>(sheetCount);
            for (int s = 0; s < sheetCount; s++)
            {
                string assetPath = folder + "/" + prefix + "_sheet_" + s.ToString("D4") + ".png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null)
                {
                    error = "Could not load " + assetPath;
                    return null;
                }

                textures.Add(tex);
            }

            return textures;
        }

        static void BuildChunks(SpriteFramesData data, List<Texture2D> textures, int frameCount, int framesPerChunk)
        {
            int framesPerSheet = data.framesPerSheet;
            int sheetsPerChunk = Mathf.Max(1, Mathf.CeilToInt((float)framesPerChunk / framesPerSheet));
            int chunkCount = Mathf.CeilToInt((float)textures.Count / sheetsPerChunk);
            string prefix = data.name;

            data.chunks.Clear();
            for (int i = 0; i < chunkCount; i++)
            {
                int startSheet = i * sheetsPerChunk;
                int endSheet = Mathf.Min(startSheet + sheetsPerChunk - 1, textures.Count - 1);
                int startFrame = startSheet * framesPerSheet;
                int endFrame = Mathf.Min((endSheet + 1) * framesPerSheet - 1, frameCount - 1);
                var chunkTextures = new List<Texture2D>();
                for (int s = startSheet; s <= endSheet; s++)
                    chunkTextures.Add(textures[s]);

                data.chunks.Add(new SpriteFramesData.FrameChunk
                {
                    chunkName = "Part" + (i + 1),
                    addressableLabel = prefix + "_Part" + (i + 1),
                    startFrame = startFrame,
                    endFrame = endFrame,
                    editorTextures = chunkTextures
                });
            }

            data.SetTotalFrameCount(frameCount);
            data.SetTotalSpriteSheetCount(textures.Count);
            data.GetTotalFrames();
        }

        static string ApplyAddressables(SpriteFramesData data, List<Texture2D> textures, out string note)
        {
            note = "";
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                note = "\nAddressables settings were missing, so the sheets were not labeled.";
                return "no addressable group";
            }

            string folder = ExistingFolder(data);
            string groupName = GroupName(data.name);
            var group = ExistingGroup(settings, folder, data.name) ?? settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                    schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            }

            int framesPerSheet = data.framesPerSheet;
            for (int i = 0; i < textures.Count; i++)
            {
                var chunk = data.GetChunkForFrame(i * framesPerSheet);
                if (chunk == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(textures[i]);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                    continue;

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                    entry = settings.CreateOrMoveEntry(guid, group);
                else if (entry.parentGroup != group)
                    settings.MoveEntry(entry, group);

                if (entry.address != assetPath)
                    entry.SetAddress(assetPath);

                entry.labels.Clear();
                settings.AddLabel(chunk.addressableLabel);
                entry.labels.Add(chunk.addressableLabel);
            }

            EditorUtility.SetDirty(settings);
            return group.name;
        }

        static AddressableAssetGroup ExistingGroup(AddressableAssetSettings settings, string folder, string prefix)
        {
            if (settings == null || string.IsNullOrEmpty(folder) || CountSheets(folder, prefix) <= 0)
                return null;

            string assetPath = folder + "/" + prefix + "_sheet_0000.png";
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return null;
            return settings.FindAssetEntry(guid)?.parentGroup;
        }

        static string GroupName(string assetName)
        {
            if (assetName.StartsWith("MainMenu", StringComparison.Ordinal))
                return "MainMenu_Group";

            var level = Regex.Match(assetName, @"^(Level\d+)");
            if (level.Success)
                return level.Groups[1].Value + "_Group";

            var video = Regex.Match(assetName, @"^(Video\d+)");
            if (video.Success)
                return video.Groups[1].Value + "_Group";

            var death = Regex.Match(assetName, @"^(Death\d+)");
            if (death.Success)
                return death.Groups[1].Value + "_Group";

            return assetName + "_Group";
        }

        static void DeleteExtraSheets(string folder, string prefix, int keep, int previous)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            for (int s = keep; s < previous; s++)
            {
                string assetPath = folder + "/" + prefix + "_sheet_" + s.ToString("D4") + ".png";
                if (settings != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (!string.IsNullOrEmpty(guid) && settings.FindAssetEntry(guid) != null)
                        settings.RemoveAssetEntry(guid);
                }

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                else
                {
                    string absolute = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                    if (File.Exists(absolute))
                        File.Delete(absolute);
                    string meta = absolute + ".meta";
                    if (File.Exists(meta))
                        File.Delete(meta);
                }
            }
        }

        static SpriteFramesData FindData(GameDefinition gd, string clipId, out Slot slot)
        {
            slot = FindSlot(gd, clipId);
            if (slot != null && slot.Current is SpriteFramesData referenced)
                return referenced;
            if (slot != null && slot.Current != null)
                return null;
            return FindAsset(clipId);
        }

        static SpriteFramesData FindAsset(string clipId)
        {
            var direct = AssetDatabase.LoadAssetAtPath<SpriteFramesData>(VideoFolder + "/" + clipId + ".asset");
            if (direct != null)
                return direct;

            if (clipId == "MainMenu_Background")
            {
                var menu = AssetDatabase.LoadAssetAtPath<SpriteFramesData>(VideoFolder + "/MainMenu_Background1.asset");
                if (menu != null)
                    return menu;
            }

            if (!AssetDatabase.IsValidFolder(VideoFolder))
                return null;

            var guids = AssetDatabase.FindAssets("t:SpriteFramesData", new[] { VideoFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<SpriteFramesData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (data != null && (data.videoName == clipId || data.name == clipId))
                    return data;
            }

            return null;
        }

        static SpriteFramesData CreateAsset(string clipId)
        {
            ProjectGenerator.EnsureFolder(VideoFolder);
            var data = ScriptableObject.CreateInstance<SpriteFramesData>();
            data.videoName = clipId;
            data.playbackFPS = 30f;
            AssetDatabase.CreateAsset(data, VideoFolder + "/" + clipId + ".asset");
            return data;
        }

        static Slot FindSlot(GameDefinition gd, string clipId)
        {
            if (gd == null || string.IsNullOrEmpty(clipId))
                return null;

            if (gd.videos != null)
            {
                for (int i = 0; i < gd.videos.Count; i++)
                {
                    var video = gd.videos[i];
                    if (video != null && video.videoName == clipId)
                        return SlotFor(video.spriteFramesData, value => video.spriteFramesData = value);
                }
            }

            if (gd.deaths != null)
            {
                for (int i = 0; i < gd.deaths.Count; i++)
                {
                    var death = gd.deaths[i];
                    if (death != null && death.deathName == clipId)
                        return SlotFor(death.spriteFramesData, value => death.spriteFramesData = value);
                }
            }

            if (gd.levels != null)
            {
                for (int i = 0; i < gd.levels.Count; i++)
                {
                    var level = gd.levels[i];
                    if (level == null)
                        continue;

                    if (level.backgroundLayers != null)
                    {
                        for (int layerIndex = 0; layerIndex < level.backgroundLayers.Count; layerIndex++)
                        {
                            var layer = level.backgroundLayers[layerIndex];
                            if (layer != null && layer.name == clipId)
                                return SlotFor(layer.spriteFramesData, value => layer.spriteFramesData = value);
                        }
                    }

                    int number = level.levelNumber > 0 ? level.levelNumber : i + 1;
                    if (!TryParseLevelBackground(clipId, out int levelNumber, out int backgroundIndex) || number != levelNumber)
                        continue;

                    if (level.backgroundLayers != null &&
                        backgroundIndex < level.backgroundLayers.Count &&
                        level.backgroundLayers[backgroundIndex] != null)
                    {
                        var layer = level.backgroundLayers[backgroundIndex];
                        return SlotFor(layer.spriteFramesData, value => layer.spriteFramesData = value);
                    }

                    if (level.backgrounds != null && backgroundIndex < level.backgrounds.Count)
                    {
                        int captured = backgroundIndex;
                        return SlotFor(level.backgrounds[captured], value => level.backgrounds[captured] = value);
                    }
                }
            }

            var menu = gd.mainMenuConfig;
            if (menu != null && menu.placedSprites != null)
            {
                for (int i = 0; i < menu.placedSprites.Count; i++)
                {
                    var placed = menu.placedSprites[i];
                    if (placed == null)
                        continue;
                    if (placed.name == clipId || MenuClipId(placed.name) == clipId)
                        return SlotFor(placed.spriteFramesData, value => placed.spriteFramesData = value);
                }
            }

            return null;
        }

        static Slot SlotFor(ScriptableObject current, Action<SpriteFramesData> set)
        {
            return new Slot { Current = current, Set = set };
        }

        static bool TryParseLevelBackground(string clipId, out int levelNumber, out int backgroundIndex)
        {
            var match = Regex.Match(clipId, @"^Level(\d+)_Background(\d+)$");
            if (!match.Success)
            {
                levelNumber = 0;
                backgroundIndex = -1;
                return false;
            }

            levelNumber = int.Parse(match.Groups[1].Value);
            backgroundIndex = int.Parse(match.Groups[2].Value) - 1;
            return backgroundIndex >= 0;
        }

        static string MenuClipId(string placedName)
        {
            if (string.IsNullOrEmpty(placedName))
                return null;
            if (placedName == "MainMenu_Background" || placedName == "MainMenu_Background1")
                return "MainMenu_Background";
            if (placedName.Contains("Explode"))
                return "MainMenu_Explode";
            return null;
        }

        static string ResolveFolder(SpriteFramesData data)
        {
            string existing = ExistingFolder(data);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            string sheets = VideoFolder + "/" + data.name + "_SpriteSheets";
            ProjectGenerator.EnsureFolder(sheets);
            return sheets;
        }

        static string ExistingFolder(SpriteFramesData data)
        {
            if (data == null)
                return null;

            string fromChunks = FolderFromChunks(data);
            if (!string.IsNullOrEmpty(fromChunks))
                return fromChunks;

            string prefix = data.name;
            string frames = VideoFolder + "/" + prefix + "_SpriteFrames";
            string sheets = VideoFolder + "/" + prefix + "_SpriteSheets";
            if (CountSheets(frames, prefix) > 0)
                return frames;
            if (CountSheets(sheets, prefix) > 0)
                return sheets;
            if (AssetDatabase.IsValidFolder(frames))
                return frames;
            if (AssetDatabase.IsValidFolder(sheets))
                return sheets;
            return null;
        }

        static string FolderFromChunks(SpriteFramesData data)
        {
            if (data.chunks == null)
                return null;

            for (int c = 0; c < data.chunks.Count; c++)
            {
                var textures = data.chunks[c].editorTextures;
                if (textures == null)
                    continue;
                for (int i = 0; i < textures.Count; i++)
                {
                    if (textures[i] == null)
                        continue;
                    string path = AssetDatabase.GetAssetPath(textures[i]);
                    if (string.IsNullOrEmpty(path))
                        continue;
                    return Path.GetDirectoryName(path)?.Replace('\\', '/');
                }
            }

            return null;
        }

        static int CountSheets(string folder, string prefix)
        {
            if (string.IsNullOrEmpty(folder))
                return 0;

            int count = 0;
            while (count < 100000 && File.Exists(SheetAbsolute(folder, prefix, count)))
                count++;
            return count;
        }

        static string SheetAbsolute(string folder, string prefix, int index)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), folder, prefix + "_sheet_" + index.ToString("D4") + ".png");
        }

        static bool GridMatches(SpriteFramesData data, int srcW, int srcH)
        {
            return data.sourceFrameWidth == srcW &&
                   data.sourceFrameHeight == srcH &&
                   data.framesPerSheet > 0 &&
                   data.sheetColumns > 0 &&
                   data.sheetRows > 0 &&
                   data.frameWidth > 0 &&
                   data.frameHeight > 0 &&
                   data.sheetTextureWidth >= data.sheetColumns * data.frameWidth &&
                   data.sheetTextureHeight >= data.sheetRows * data.frameHeight;
        }

    }
}
#endif
